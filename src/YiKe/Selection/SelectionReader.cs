using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using YiKe.Core;

namespace YiKe.Selection;

public sealed class SelectionReader : IDisposable
{
    ISelectionTransport? worker;
    readonly SemaphoreSlim serial = new(1);
    readonly object lifecycle = new();
    readonly Func<ISelectionTransport> startWorker;
    readonly TimeSpan queryTimeout;
    volatile bool disposed;
    public SelectionReader() : this(ProcessSelectionTransport.Start, TimeSpan.FromMilliseconds(2500)) { }
    internal SelectionReader(Func<ISelectionTransport> startWorker, TimeSpan queryTimeout) { this.startWorker = startWorker; this.queryTimeout = queryTimeout; }
    public Task<SelectionReadResult> ReadAsync(Gesture gesture, CancellationToken token) =>
        Task.Run(() => ReadCoreAsync(gesture, token), token);
    internal const int SettleBudgetMilliseconds = 2200;
    public Task PrepareAsync(CancellationToken token) => Task.Run(async () =>
    {
        await serial.WaitAsync(token).ConfigureAwait(false);
        try { token.ThrowIfCancellationRequested(); _ = GetWorker(); }
        finally { serial.Release(); }
    }, token);
    public async Task<SelectionReadResult> ReadSettledAsync(Gesture gesture, CancellationToken token)
    {
        // Accessibility providers may publish a selection over several UI turns.
        // Recheck this exact gesture, never the clipboard, within one total deadline.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(SettleBudgetMilliseconds);
        var last = new SelectionReadResult(ReadStatus.NoSelection);
        int[] delays = { 0, 80, 140, 220, 320, 480 };
        try
        {
            foreach (int delay in delays)
            {
                if (delay > 0) await Task.Delay(delay, budget.Token).ConfigureAwait(false);
                last = await ReadAsync(gesture, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
                if (last.Status is not (ReadStatus.NoSelection or ReadStatus.Unsupported)) return last;
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { return new(ReadStatus.Timeout); }
        token.ThrowIfCancellationRequested();
        return last;
    }
    ISelectionTransport? GetWorker()
    {
        ISelectionTransport? current;
        lock (lifecycle) { if (disposed) return null; current = worker; }
        if (current?.IsAlive == true) return current;
        ResetWorker(current);
        lock (lifecycle) { if (disposed) return null; }
        var created = startWorker(); // Starting an EXE must not hold the UI shutdown lock.
        bool adopted;
        lock (lifecycle) { adopted = !disposed; if (adopted) worker = current = created; }
        if (!adopted) { created.Dispose(); return null; }
        return current;
    }
    async Task<SelectionReadResult> ReadCoreAsync(Gesture gesture, CancellationToken token)
    {
        await serial.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var current = GetWorker();
            if (current is null) return new(ReadStatus.TargetGone);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(queryTimeout);
            try
            {
                var exchange = current!.ExchangeAsync(JsonSerializer.Serialize(gesture), timeout.Token);
                _ = exchange.ContinueWith(task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                var line = await exchange.WaitAsync(timeout.Token).ConfigureAwait(false);
                if (line is null) { ResetWorker(current); return new(ReadStatus.TargetGone); }
                var result = JsonSerializer.Deserialize<SelectionReadResult>(line) ?? new(ReadStatus.Unsupported);
                if (result.Status == ReadStatus.Success && !TextLimit.Allowed(result.Text ?? "")) return new(string.IsNullOrWhiteSpace(result.Text) ? ReadStatus.NoSelection : ReadStatus.TooLong);
                return result;
            }
            catch (OperationCanceledException) { ResetWorker(current); if (token.IsCancellationRequested) throw; return new(ReadStatus.Timeout); }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException) { ResetWorker(current); return new(disposed ? ReadStatus.TargetGone : ReadStatus.Unsupported); }
        }
        catch (System.ComponentModel.Win32Exception) { return new(ReadStatus.AccessDenied); }
        finally { serial.Release(); }
    }
    void ResetWorker(ISelectionTransport? expected)
    {
        if (expected is null) return;
        lock (lifecycle) { if (ReferenceEquals(worker, expected)) worker = null; }
        try { expected.Dispose(); }
        catch { lock (lifecycle) { if (!disposed && worker is null) worker = expected; } throw; }
    }
    public void Dispose()
    {
        ISelectionTransport? owned;
        lock (lifecycle) { if (disposed) return; disposed = true; owned = worker; worker = null; }
        owned?.Dispose();
    }
}

public static class UiaWorker
{
    public static void Run(int parentId, long parentStartedUtcTicks)
    {
        Console.InputEncoding = new UTF8Encoding(false); Console.OutputEncoding = new UTF8Encoding(false);
        _ = Task.Run(async () => { try { using var p = Process.GetProcessById(parentId); if (p.StartTime.ToUniversalTime().Ticks != parentStartedUtcTicks) { Environment.Exit(0); return; } await p.WaitForExitAsync(); } catch { } Environment.Exit(0); });
        string? line;
        while ((line = Console.ReadLine()) is not null)
        {
            SelectionReadResult result;
            try { var g = JsonSerializer.Deserialize<Gesture>(line)!; result = Read(g); }
            catch (ElementNotAvailableException) { result = new(ReadStatus.TargetGone); }
            catch (UnauthorizedAccessException) { result = new(ReadStatus.AccessDenied); }
            catch { result = new(ReadStatus.Unsupported); }
            Console.WriteLine(JsonSerializer.Serialize(result)); Console.Out.Flush();
        }
    }
    static SelectionReadResult Read(Gesture g)
    {
        if (!IsCurrent(g)) return new(ReadStatus.TargetGone);
        SelectionReadResult result;
        try { result = ReadUia(g); }
        catch (UnauthorizedAccessException) { return new(ReadStatus.AccessDenied); }
        catch (COMException error) when (error.ErrorCode == unchecked((int)0x80070005)) { return new(ReadStatus.AccessDenied); }
        catch (Exception error) when (error is ElementNotAvailableException or InvalidOperationException or NotSupportedException or COMException)
        { result = new(ReadStatus.Unsupported); }
        if (result.Status is ReadStatus.Success or ReadStatus.TooLong or ReadStatus.AccessDenied or ReadStatus.TargetGone or ReadStatus.NotTextSelection) return result;
        var office = WpsSelectionAdapter.TryRead(g);
        if (office is not null) return IsCurrent(g) ? office : new(ReadStatus.TargetGone);
        var legacy = LegacyTextReader.TryRead(g);
        return IsCurrent(g) ? legacy ?? result : new(ReadStatus.TargetGone);
    }
    static bool IsCurrent(Gesture g) => Native.IsWindow(new IntPtr(g.Window)) && Native.ProcessId(new IntPtr(g.Window)) == g.ProcessId && Native.GetForegroundWindow().ToInt64() == g.Window;
    static SelectionReadResult ReadUia(Gesture g)
    {
        var hwnd = new IntPtr(g.Window);
        if (!Native.IsWindow(hwnd) || Native.ProcessId(hwnd) != g.ProcessId || Native.GetForegroundWindow() != hwnd) return new(ReadStatus.TargetGone);
        var adapted = VsCodeSelectionAdapter.TryRead(g);
        if (adapted is not null)
            return Native.GetForegroundWindow() == hwnd && Native.ProcessId(hwnd) == g.ProcessId ? adapted : new(ReadStatus.TargetGone);
        Dictionary<int, int>? parents = null;
        bool Belongs(AutomationElement element)
        {
            int pid = element.Current.ProcessId;
            int native = element.Current.NativeWindowHandle;
            if (native != 0) return Native.GetAncestor(new IntPtr(native), 2) == hwnd;
            return pid == g.ProcessId || VsCodeSelectionAdapter.IsDescendant(pid, g.ProcessId, parents ??= VsCodeSelectionAdapter.ProcessParents());
        }
        var startElement = AutomationElement.FromPoint(new Point(g.StartX, g.StartY));
        IEnumerable<int> StartKinds()
        {
            var element = startElement;
            for (int depth = 0; element is not null && depth < 16; depth++, element = TreeWalker.RawViewWalker.GetParent(element))
            {
                if (!Belongs(element)) yield break;
                yield return element.Current.ControlType.Id;
            }
        }
        if (!AutomaticSelectionPolicy.AllowsStart(StartKinds())) return new(ReadStatus.NotTextSelection);
        if (ShellSelectionPolicy.IsShell(Native.ClassName(hwnd)) && !ShellSelectionPolicy.AllowsTextStart(g, startElement, Belongs)) return new(ReadStatus.NotTextSelection);
        var endElement = AutomationElement.FromPoint(new Point(g.X, g.Y));
        foreach (var hit in new[] { startElement, endElement })
        {
            var candidate = hit;
            for (int i = 0; candidate is not null && i < 12; i++, candidate = TreeWalker.RawViewWalker.GetParent(candidate))
            {
                if (!Belongs(candidate)) break;
                if (candidate.Current.IsPassword) return new(ReadStatus.AccessDenied);
                var kind = candidate.Current.ControlType;
                if (kind == ControlType.ScrollBar || kind == ControlType.TitleBar || kind == ControlType.Slider || kind == ControlType.Thumb) return new(ReadStatus.NotTextSelection);
            }
        }
        var seeds = new List<AutomationElement>();
        var focus = AutomationElement.FocusedElement;
        // The release point is often just outside the selected glyphs; keep the start point as a second bounded candidate.
        seeds.Add(startElement);
        seeds.Add(endElement);
        if (focus is not null && Belongs(focus)) seeds.Add(focus);
        bool supported = false;
        var visited = new HashSet<AutomationElement>();
        foreach (var seed in seeds)
        {
            var e = seed;
            for (int depth = 0; e is not null && depth < 16; depth++, e = TreeWalker.RawViewWalker.GetParent(e))
            {
                if (!Belongs(e)) break;
                if (!visited.Add(e)) break;
                if (e.Current.IsPassword) return new(ReadStatus.AccessDenied);
                // Ignore UI chrome and range controls: their ancestors may expose a stale document selection.
                var kind = e.Current.ControlType;
                if (depth == 0 && (kind == ControlType.ScrollBar || kind == ControlType.TitleBar || kind == ControlType.Slider || kind == ControlType.Button)) break;
                if (!e.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)) continue;
                supported = true;
                var ranges = ((TextPattern)pattern).GetSelection();
                if (ranges is null || ranges.Length == 0) continue;
                if (ranges.Length > 32) return new(ReadStatus.Unsupported);
                var parts = new List<string>(); bool nearGesture = false;
                foreach (var range in ranges)
                {
                    if (range.CompareEndpoints(TextPatternRangeEndpoint.Start, range, TextPatternRangeEndpoint.End) == 0) continue;
                    // Never query DocumentRange or read the entire control. A bounded selected range can detect oversize safely.
                    string text = range.GetText(8002);
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var rects = range.GetBoundingRectangles();
                    foreach (var rect in rects)
                    {
                        var r = new Rect(rect.X - 12, rect.Y - 12, rect.Width + 24, rect.Height + 24);
                        if (r.Contains(new Point(g.X, g.Y)) || r.Contains(new Point(g.StartX, g.StartY))) nearGesture = true;
                    }
                    parts.Add(text);
                }
                string selection = string.Join("\n", parts);
                if (!nearGesture || string.IsNullOrWhiteSpace(selection)) continue;
                if (Native.GetForegroundWindow() != hwnd || Native.ProcessId(hwnd) != g.ProcessId) return new(ReadStatus.TargetGone);
                return TextLimit.Allowed(selection) ? new(ReadStatus.Success, selection) : new(ReadStatus.TooLong);
            }
        }
        return new(supported ? ReadStatus.NoSelection : ReadStatus.Unsupported);
    }
}
