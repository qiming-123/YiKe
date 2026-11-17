using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Automation;
using YiKe.Core;
using YiKe.Selection;

public static class SelectionReaderChecks
{
    static Gesture Gesture(long sequence = 1) => new(sequence, 0, Environment.ProcessId, 0, 0, 0, 0, DateTimeOffset.UtcNow);
    static string Success(string text) => JsonSerializer.Serialize(new SelectionReadResult(ReadStatus.Success, text));
    public static async Task Run(Func<string, Func<Task>, Task> check)
    {
        await check("Desktop file drags, rubber band selection and icon double clicks are ignored before text reading", () =>
        {
            foreach (string root in new[] { "Progman", "WorkerW" })
            foreach (string hit in new[] { "SysListView32", "SHELLDLL_DefView", "DirectUIHWND", "WorkerW", "" })
            {
                Assert(ShellSelectionPolicy.IgnoreDesktopPress(root, hit));
                var tracker = new MouseGestureTracker(); tracker.Down(1, 20, 20);
                if (ShellSelectionPolicy.IgnoreDesktopPress(root, hit)) tracker.Cancel();
                Assert(!tracker.Release(1, 200, 100, 1000, 500, out _));
                tracker.Cancel(); Assert(!tracker.Release(1, 20, 20, 1100, 500, out _));
            }
            Assert(!ShellSelectionPolicy.IgnoreDesktopPress("CabinetWClass", "DirectUIHWND"));
            Assert(!ShellSelectionPolicy.IgnoreDesktopPress("Chrome_WidgetWin_1", "Chrome_RenderWidgetHostHWND"));
            return Task.CompletedTask;
        });
        await check("Explorer item and blank-list surfaces cannot reuse a stale focused text selection", () =>
        {
            foreach (var kind in new[] { ControlType.ListItem, ControlType.TreeItem, ControlType.DataItem, ControlType.List, ControlType.Tree, ControlType.DataGrid, ControlType.Button, ControlType.ScrollBar, ControlType.TitleBar, ControlType.HeaderItem })
                Assert(!ShellSelectionPolicy.AllowsTextStart(new[] { (ControlType.Text.Id, true), (kind.Id, true), (ControlType.Document.Id, true) }));
            Assert(!ShellSelectionPolicy.AllowsTextStart(new[] { (ControlType.Pane.Id, false), (ControlType.Window.Id, false) }));
            Assert(ShellSelectionPolicy.AllowsTextStart(new[] { (ControlType.Text.Id, false), (ControlType.Document.Id, true), (ControlType.Window.Id, false) }));
            return Task.CompletedTask;
        });
        await check("Shell rename, address and search editors retain normal text selection and useful failures", () =>
        {
            foreach (string root in new[] { "Progman", "WorkerW", "CabinetWClass", "ExploreWClass" })
            foreach (string edit in new[] { "Edit", "RICHEDIT50W", "RichEdit20W" })
            {
                Assert(!ShellSelectionPolicy.IgnoreDesktopPress(root, edit));
                Assert(ShellSelectionPolicy.ShowReadFailure(root, edit));
            }
            Assert(ShellSelectionPolicy.AllowsTextStart(new[] { (ControlType.Text.Id, false), (ControlType.Edit.Id, true), (ControlType.ListItem.Id, false) }));
            foreach (string root in new[] { "Progman", "WorkerW", "CabinetWClass", "ExploreWClass" }) Assert(!ShellSelectionPolicy.ShowReadFailure(root, "DirectUIHWND"));
            Assert(ShellSelectionPolicy.ShowReadFailure("Chrome_WidgetWin_1", "Chrome_RenderWidgetHostHWND"));
            return Task.CompletedTask;
        });
        await check("Confirmed non-text selection is silent and is neither retried nor replaced with stale text", async () =>
        {
            int calls = 0;
            using var reader = new SelectionReader(() => new ScriptedTransport(_ => { calls++; return Task.FromResult<string?>(JsonSerializer.Serialize(new SelectionReadResult(ReadStatus.NotTextSelection, "must not escape"))); }), TimeSpan.FromSeconds(1));
            var result = await reader.ReadSettledAsync(Gesture(), default);
            Assert(result.Status == ReadStatus.NotTextSelection && result.Text is null && calls == 1);
        });
        await check("An initially unavailable web accessibility provider is queried once again without another worker", async () =>
        {
            int calls = 0, starts = 0;
            using var reader = new SelectionReader(() => { starts++; return new ScriptedTransport(_ => Task.FromResult<string?>(++calls == 1 ? JsonSerializer.Serialize(new SelectionReadResult(ReadStatus.Unsupported)) : Success("newly available web selection"))); }, TimeSpan.FromSeconds(1));
            var result = await reader.ReadSettledAsync(Gesture(), default);
            Assert(result.Text == "newly available web selection" && starts == 1 && calls == 2);
        });
        await check("An asynchronously published selection is read once more with the same worker", async () =>
        {
            int calls = 0, starts = 0;
            using var reader = new SelectionReader(() => { starts++; return new ScriptedTransport(_ => Task.FromResult<string?>(++calls == 1 ? JsonSerializer.Serialize(new SelectionReadResult(ReadStatus.NoSelection)) : Success("current delayed selection"))); }, TimeSpan.FromSeconds(1));
            var result = await reader.ReadSettledAsync(Gesture(), default);
            Assert(result.Text == "current delayed selection" && calls == 2 && starts == 1);
        });
        await check("Cancellation during selection settling prevents a second query or stale reply", async () =>
        {
            int calls = 0; var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var reader = new SelectionReader(() => new ScriptedTransport(_ => { calls++; first.TrySetResult(); return Task.FromResult<string?>(JsonSerializer.Serialize(new SelectionReadResult(ReadStatus.NoSelection))); }), TimeSpan.FromSeconds(1));
            using var cancel = new CancellationTokenSource(); var reading = reader.ReadSettledAsync(Gesture(), cancel.Token);
            await first.Task; cancel.Cancel(); await ExpectCanceled(reading); Assert(calls == 1);
        });
        await check("Selection failures report a useful visible fallback without inventing a text value", () =>
        {
            foreach (var status in new[] { ReadStatus.Unsupported, ReadStatus.Timeout, ReadStatus.AccessDenied }) Assert(SelectionFeedback.Message(status).Contains("粘贴文本翻译"));
            return Task.CompletedTask;
        });
        await check("Starting a UIA helper cannot synchronously block its caller", async () =>
        {
            using var release = new ManualResetEventSlim();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var returned = new TaskCompletionSource<Task<SelectionReadResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var reader = new SelectionReader(() => { entered.TrySetResult(); release.Wait(); return new ScriptedTransport(_ => Task.FromResult<string?>(Success("startup fixture"))); }, TimeSpan.FromSeconds(2));
            var caller = new Thread(() => { try { returned.TrySetResult(reader.ReadAsync(Gesture(), default)); } catch (Exception error) { returned.TrySetException(error); } }) { IsBackground = true };
            caller.Start(); bool returnedWhileStarting;
            try { await entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); returnedWhileStarting = await Task.WhenAny(returned.Task, Task.Delay(250)) == returned.Task; }
            finally { release.Set(); caller.Join(2000); }
            var reading = await returned.Task.WaitAsync(TimeSpan.FromSeconds(2)); Assert((await reading.WaitAsync(TimeSpan.FromSeconds(2))).Status == ReadStatus.Success);
            Assert(returnedWhileStarting);
        });
        await check("Mouse release requires a matching left press and source window", () =>
        {
            var tracker = new MouseGestureTracker();
            Assert(!tracker.Release(1, 100, 100, 1000, 500, out _));
            tracker.Down(1, 10, 10); Assert(!tracker.Release(2, 100, 100, 1100, 500, out _));
            tracker.Down(1, 10, 10); Assert(tracker.Release(1, 100, 100, 1200, 500, out var start) && start == (10, 10));
            Assert(!tracker.Release(1, 200, 200, 1300, 500, out _)); return Task.CompletedTask;
        });
        await check("Right or middle click cancels pending left coordinates and click history", () =>
        {
            var tracker = new MouseGestureTracker(); tracker.Down(1, 10, 10); tracker.Release(1, 10, 10, 1000, 500, out _);
            tracker.Cancel(); tracker.Down(1, 10, 10); Assert(!tracker.Release(1, 10, 10, 1100, 500, out _));
            tracker.Down(1, 10, 10); tracker.Cancel(); Assert(!tracker.Release(1, 200, 200, 1200, 500, out _)); return Task.CompletedTask;
        });
        await check("Double click uses monotonic time and a matching window, excluding an earlier drag", () =>
        {
            var tracker = new MouseGestureTracker();
            tracker.Down(1, 10, 10); Assert(!tracker.Release(1, 10, 10, 1000, 500, out _));
            tracker.Down(2, 10, 10); Assert(!tracker.Release(2, 10, 10, 1100, 500, out _));
            tracker.Down(2, 10, 10); Assert(tracker.Release(2, 10, 10, 1200, 500, out _));
            tracker.Down(2, 10, 10); Assert(!tracker.Release(2, 10, 10, 900, 500, out _));
            tracker.Down(2, 10, 10); Assert(tracker.Release(2, 100, 100, 2000, 500, out _));
            tracker.Down(2, 100, 100); Assert(!tracker.Release(2, 100, 100, 2100, 500, out _)); return Task.CompletedTask;
        });
        await check("Pointer distance arithmetic does not overflow at extreme coordinates", () =>
        {
            var tracker = new MouseGestureTracker(); tracker.Down(1, int.MinValue, int.MinValue);
            Assert(tracker.Release(1, int.MaxValue, int.MaxValue, 1000, 500, out _)); return Task.CompletedTask;
        });
        await check("The production UIA worker exits after its exact parent exits", async () =>
        {
            using var parent = StartFixture("normal"); using var parentProcess = Process.GetProcessById(parent.ProcessId);
            using var child = StartUiaWorker(parent.ProcessId, parentProcess.StartTime.ToUniversalTime().Ticks);
            await Task.Delay(180); Assert(child.IsAlive);
            parent.Dispose(); await UntilStopped(child); Assert(!child.IsAlive);
        });
        await check("The production UIA worker rejects a reused PID with a different start time", async () =>
        {
            using var parent = StartFixture("normal"); using var parentProcess = Process.GetProcessById(parent.ProcessId);
            using var child = StartUiaWorker(parent.ProcessId, parentProcess.StartTime.ToUniversalTime().Ticks + 1);
            await UntilStopped(child); Assert(parent.IsAlive && !child.IsAlive);
        });
        await check("A UIA logical timeout disposes the stalled transport and uses a fresh worker next time", async () =>
        {
            int starts = 0; var held = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = new ScriptedTransport(_ => held.Task, () => held.TrySetResult(Success("late stale text")));
            using var reader = new SelectionReader(() => ++starts == 1 ? first : new ScriptedTransport(_ => Task.FromResult<string?>(Success("new text"))), TimeSpan.FromMilliseconds(50));
            var timeout = await reader.ReadAsync(Gesture(), default); Assert(timeout.Status == ReadStatus.Timeout && timeout.Text is null && first.Disposed);
            var result = await reader.ReadAsync(Gesture(2), default); Assert(result.Text == "new text" && starts == 2);
        });
        await check("A worker EOF is reset instead of being reused indefinitely", async () =>
        {
            int starts = 0; var eof = new ScriptedTransport(_ => Task.FromResult<string?>(null));
            using var reader = new SelectionReader(() => ++starts == 1 ? eof : new ScriptedTransport(_ => Task.FromResult<string?>(Success("fresh"))), TimeSpan.FromSeconds(1));
            Assert((await reader.ReadAsync(Gesture(), default)).Status == ReadStatus.TargetGone && eof.Disposed);
            Assert((await reader.ReadAsync(Gesture(2), default)).Text == "fresh" && starts == 2);
        });
        await check("Malformed worker JSON and oversize selections do not return cached text", async () =>
        {
            int starts = 0;
            using var reader = new SelectionReader(() => new ScriptedTransport(_ => Task.FromResult<string?>(++starts == 1 ? "bad-json" : Success(new string('x', 2001)))), TimeSpan.FromSeconds(1));
            Assert((await reader.ReadAsync(Gesture(), default)).Status == ReadStatus.Unsupported);
            var result = await reader.ReadAsync(Gesture(2), default); Assert(result.Status == ReadStatus.TooLong && result.Text is null);
        });
        await check("Closing during worker startup does not block shutdown or adopt an orphan transport", async () =>
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); using var release = new ManualResetEventSlim();
            var created = new ScriptedTransport(_ => Task.FromResult<string?>(Success("unused")));
            using var reader = new SelectionReader(() => { entered.TrySetResult(); release.Wait(); return created; }, TimeSpan.FromSeconds(1));
            Task<SelectionReadResult> reading = Task.Run(() => reader.ReadAsync(Gesture(), default)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Task.Run(reader.Dispose).WaitAsync(TimeSpan.FromSeconds(1)); release.Set();
            Assert((await reading.WaitAsync(TimeSpan.FromSeconds(2))).Status == ReadStatus.TargetGone && created.Disposed);
        });
        await check("Canceling a queued read never creates another helper", async () =>
        {
            int starts = 0; var held = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var reader = new SelectionReader(() => { starts++; entered.TrySetResult(); return new ScriptedTransport(_ => held.Task, () => held.TrySetCanceled()); }, TimeSpan.FromSeconds(2));
            using var active = new CancellationTokenSource(); using var queued = new CancellationTokenSource();
            Task<SelectionReadResult> first = reader.ReadAsync(Gesture(), active.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Task<SelectionReadResult> next = reader.ReadAsync(Gesture(2), queued.Token); queued.Cancel(); await ExpectCanceled(next);
            active.Cancel(); await ExpectCanceled(first); Assert(starts == 1);
        });
        await check("A termination failure retains the owned helper and never starts an unbounded replacement", async () =>
        {
            int starts = 0; var stubborn = new TerminationDeniedTransport();
            using var reader = new SelectionReader(() => { starts++; return stubborn; }, TimeSpan.FromMilliseconds(30));
            Assert((await reader.ReadAsync(Gesture(), default)).Status == ReadStatus.AccessDenied);
            Assert((await reader.ReadAsync(Gesture(2), default)).Status == ReadStatus.AccessDenied);
            Assert(starts == 1); stubborn.AllowDisposal = true;
        });
        await check("A real owned helper is terminated on timeout, without killing the caller", async () =>
        {
            ProcessSelectionTransport? helper = null;
            using var reader = new SelectionReader(() => helper = StartFixture("hang"), TimeSpan.FromMilliseconds(300));
            var result = await reader.ReadAsync(Gesture(), default); Assert(result.Status == ReadStatus.Timeout && helper is not null && !helper.IsAlive);
        });
        await check("A real helper EOF recovers into a new process with the current sequence", async () =>
        {
            int starts = 0;
            using var reader = new SelectionReader(() => StartFixture(++starts == 1 ? "eof" : "normal"), TimeSpan.FromSeconds(3));
            Assert((await reader.ReadAsync(Gesture(), default)).Status == ReadStatus.TargetGone);
            var result = await reader.ReadAsync(Gesture(2), default); Assert(result.Text == "synthetic selection 2" && starts == 2);
        });
        await check("A real helper is cleaned up when its caller cancels the request", async () =>
        {
            ProcessSelectionTransport? helper = null; using var cancellation = new CancellationTokenSource(250);
            using var reader = new SelectionReader(() => helper = StartFixture("hang"), TimeSpan.FromSeconds(3));
            await ExpectCanceled(reader.ReadAsync(Gesture(), cancellation.Token)); Assert(helper is not null && !helper.IsAlive);
        });
    }
    static ProcessSelectionTransport StartFixture(string mode)
    { var info = new ProcessStartInfo(Environment.ProcessPath!); info.ArgumentList.Add("--worker-fixture"); info.ArgumentList.Add(mode); return new(info); }
    static ProcessSelectionTransport StartUiaWorker(int parent, long started)
    { var info = new ProcessStartInfo(Environment.ProcessPath!); info.ArgumentList.Add("--uia-worker"); info.ArgumentList.Add(parent.ToString()); info.ArgumentList.Add(started.ToString()); return new(info); }
    static async Task UntilStopped(ProcessSelectionTransport worker)
    { var limit = Stopwatch.StartNew(); while (worker.IsAlive && limit.Elapsed < TimeSpan.FromSeconds(4)) await Task.Delay(20); }
    static void Assert(bool condition) { if (!condition) throw new Exception("selection transport assertion failed"); }
    static async Task ExpectCanceled(Task task) { try { await task; } catch (OperationCanceledException) { return; } throw new Exception("expected cancellation"); }
}
sealed class ScriptedTransport(Func<CancellationToken, Task<string?>> exchange, Action? dispose = null) : ISelectionTransport
{
    public bool Disposed; public bool IsAlive => !Disposed;
    public Task<string?> ExchangeAsync(string request, CancellationToken token) => exchange(token);
    public void Dispose() { if (Disposed) return; Disposed = true; dispose?.Invoke(); }
}
sealed class TerminationDeniedTransport : ISelectionTransport
{
    bool stopping; public bool AllowDisposal; public bool IsAlive { get; private set; } = true;
    public Task<string?> ExchangeAsync(string request, CancellationToken token)
    { if (stopping) throw new InvalidOperationException("stopping"); return new TaskCompletionSource<string?>().Task; }
    public void Dispose() { stopping = true; if (!AllowDisposal) throw new Win32Exception("synthetic termination failure"); IsAlive = false; }
}
