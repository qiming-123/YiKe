using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Threading;
using YiKe.Core;

namespace YiKe.Selection;

internal static class PdfCopyPolicy
{
    internal static bool IsWpsPdf(string process, string title)
    {
        if (!process.Equals("wps", StringComparison.OrdinalIgnoreCase) && !process.Equals("wpspdf", StringComparison.OrdinalIgnoreCase) && !process.Equals("wpsoffice", StringComparison.OrdinalIgnoreCase)) return false;
        foreach (string suffix in new[] { " - WPS Office", " - WPS PDF", " - WPS 365", " - WPS", " - 金山PDF" })
            if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { title = title[..^suffix.Length]; break; }
        title = title.TrimEnd(' ', '*');
        foreach (string suffix in new[] { " [只读]", " （只读）", " (Read-Only)" })
            if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { title = title[..^suffix.Length]; break; }
        return title.TrimEnd(' ', '*').EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
    }
    internal static bool CanWait(ReadStatus status) => status is ReadStatus.Unsupported or ReadStatus.NoSelection or ReadStatus.Timeout or ReadStatus.NotTextSelection;
    internal static bool Fresh(uint baseline, uint sequence, bool sourceCurrent, bool ownerMatches, bool fileDrop, bool unicodeText) =>
        baseline != sequence && sequence != 0 && sourceCurrent && ownerMatches && !fileDrop && unicodeText;
    internal static bool RendererOwner(string process, string? rootImage, string? ownerImage, bool descendant, bool startedAfterRoot) =>
        descendant && startedAfterRoot && process.Equals("wpspdf", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrEmpty(rootImage) && !string.IsNullOrEmpty(ownerImage) &&
        !string.IsNullOrEmpty(Path.GetDirectoryName(rootImage)) &&
        string.Equals(Path.GetDirectoryName(rootImage), Path.GetDirectoryName(ownerImage), StringComparison.OrdinalIgnoreCase);
    internal static SelectionReadResult ValidateText(string text) => string.IsNullOrWhiteSpace(text) ? new(ReadStatus.NoSelection) : TextLimit.Allowed(text) ? new(ReadStatus.Success, text) : new(ReadStatus.TooLong);
}

internal interface IPdfCopySource
{
    uint Sequence { get; }
    long Now { get; }
    string? Capture(Gesture gesture);
    bool IsCurrent(Gesture gesture, string title);
    Task<SelectionReadResult?> Read(Gesture gesture, string title, uint baseline, uint sequence);
}

// A genuine mouse selection candidate must fail normal accessibility reading first.
// Only a subsequent fresh, source-owned copy is read. This class never copies,
// writes, clears or restores the clipboard, and never reads it on startup.
internal sealed class PdfCopyFallback : IDisposable
{
    readonly HwndSource source = new(new HwndSourceParameters("YiKe PDF copy observer") { ParentWindow = new IntPtr(-3) });
    readonly Func<bool> enabled;
    readonly Func<int, bool> excluded;
    readonly IPdfCopySource clipboard;
    readonly DispatcherTimer retry = new() { Interval = TimeSpan.FromMilliseconds(40) };
    Gesture? pending;
    string title = "";
    uint baseline;
    long expires, generation;
    int attempts;
    bool ready, reading, disposed;
    internal event Action<Gesture, SelectionReadResult>? Copied;
    internal bool Available { get; }
    internal bool IsArmed => pending is not null;
    internal ReadStatus? LastCopyStatus { get; private set; }
    public PdfCopyFallback(Func<bool> enabled, Func<int, bool> excluded, IPdfCopySource? clipboard = null)
    {
        this.enabled = enabled; this.excluded = excluded;
        this.clipboard = clipboard ?? new NativePdfCopySource();
        source.AddHook(Message); Available = AddClipboardFormatListener(source.Handle);
        retry.Tick += (_, _) => { retry.Stop(); Check(); };
    }
    public void Clear() { pending = null; title = ""; baseline = 0; expires = 0; attempts = 0; ready = false; generation++; retry.Stop(); }
    public void Arm(Gesture gesture)
    {
        Clear();
        if (!Available || !enabled() || excluded(gesture.ProcessId)) return;
        string? caption = clipboard.Capture(gesture);
        if (caption is null) return;
        pending = gesture; title = caption; baseline = clipboard.Sequence;
        expires = clipboard.Now + 15000; attempts = 0;
    }
    public void CompleteRead(Gesture gesture, ReadStatus status)
    {
        if (pending?.Sequence != gesture.Sequence) return;
        if (!PdfCopyPolicy.CanWait(status)) { Clear(); return; }
        ready = true;
        // Let the automatic-read handler finish hiding its silent result first.
        source.Dispatcher.BeginInvoke(Check, DispatcherPriority.Background);
    }
    IntPtr Message(IntPtr hwnd, int message, IntPtr wp, IntPtr lp, ref bool handled)
    {
        if (message == 0x31D && pending is not null && ready) { retry.Stop(); retry.Start(); }
        return IntPtr.Zero;
    }
    internal void ClipboardChanged() => Check();
    public void Update()
    {
        if (pending is { } gesture && (!enabled() || excluded(gesture.ProcessId) || clipboard.Now > expires || !clipboard.IsCurrent(gesture, title))) Clear();
    }
    async void Check()
    {
        if (disposed || pending is not { } gesture || !ready || reading) return;
        if (!enabled() || excluded(gesture.ProcessId) || clipboard.Now > expires || !clipboard.IsCurrent(gesture, title)) { Clear(); return; }
        uint sequence = clipboard.Sequence;
        if (sequence == 0 || sequence == baseline) return;
        long version = generation; string expectedTitle = title; uint expectedBaseline = baseline;
        reading = true; bool recheck = false;
        try
        {
            var result = await clipboard.Read(gesture, expectedTitle, expectedBaseline, sequence);
            LastCopyStatus = result?.Status;
            if (disposed || version != generation || !enabled() || excluded(gesture.ProcessId) || !clipboard.IsCurrent(gesture, expectedTitle)) return;
            if (clipboard.Now > expires) { Clear(); return; }
            if (clipboard.Sequence != sequence) { recheck = true; return; }
            if (result is null)
            {
                if (++attempts < 5) retry.Start();
                return;
            }
            Clear();
            if (result.Status is ReadStatus.Success or ReadStatus.TooLong) Copied?.Invoke(gesture, result);
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException) { Clear(); }
        finally
        {
            reading = false;
            if (!disposed && pending is not null && ready && (recheck || version != generation)) _ = source.Dispatcher.BeginInvoke(Check, DispatcherPriority.Background);
        }
    }
    static SelectionReadResult? ReadFresh(Gesture gesture, string title, uint baseline, uint sequence)
    {
        if (!Current(gesture, title) || Native.GetClipboardSequenceNumber() != sequence) return new(ReadStatus.TargetGone);
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            if (!PdfCopyPolicy.Fresh(baseline, sequence, Current(gesture, title) && Native.GetClipboardSequenceNumber() == sequence,
                OwnerMatches(GetClipboardOwner(), gesture), IsClipboardFormatAvailable(15), true)) return new(ReadStatus.NoSelection);
            // WPS may publish the format/data just after the clipboard update.
            if (!IsClipboardFormatAvailable(13)) return null;
            IntPtr data = GetClipboardData(13);
            if (data == IntPtr.Zero) return null;
            ulong size = GlobalSize(data).ToUInt64();
            if (size > 16004) return new(ReadStatus.TooLong);
            if (size < 2 || size % 2 != 0) return new(ReadStatus.NoSelection);
            IntPtr pointer = GlobalLock(data);
            if (pointer == IntPtr.Zero) return null;
            try
            {
                int limit = (int)(size / 2), length = 0;
                while (length < limit && Marshal.ReadInt16(pointer, length * 2) != 0) length++;
                if (length == limit) return new(ReadStatus.Unsupported);
                string text = Marshal.PtrToStringUni(pointer, length) ?? "";
                if (!Current(gesture, title) || Native.GetClipboardSequenceNumber() != sequence || !OwnerMatches(GetClipboardOwner(), gesture)) return new(ReadStatus.TargetGone);
                return PdfCopyPolicy.ValidateText(text);
            }
            finally { GlobalUnlock(data); }
        }
        finally { CloseClipboard(); }
    }
    internal static bool OwnerMatches(IntPtr owner, Gesture gesture)
    {
        if (owner == IntPtr.Zero) return false;
        int pid = Native.ProcessId(owner);
        if (pid == gesture.ProcessId || Native.GetAncestor(owner, 2).ToInt64() == gesture.Window || Native.GetAncestor(owner, 3).ToInt64() == gesture.Window) return true;
        // WPS hosts the PDF tab in wps.exe, but its child wpspdf.exe owns a
        // separate hidden clipboard window. Bind that renderer to this host.
        if (!VsCodeSelectionAdapter.IsDescendant(pid, gesture.ProcessId, VsCodeSelectionAdapter.ProcessParents())) return false;
        try
        {
            using var root = Process.GetProcessById(gesture.ProcessId);
            using var renderer = Process.GetProcessById(pid);
            return PdfCopyPolicy.RendererOwner(renderer.ProcessName, root.MainModule?.FileName, renderer.MainModule?.FileName, true, renderer.StartTime >= root.StartTime);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { return false; }
    }
    static bool Current(Gesture gesture, string title)
    {
        var hwnd = new IntPtr(gesture.Window);
        if (!Native.IsWindow(hwnd) || Native.GetForegroundWindow() != hwnd || Native.ProcessId(hwnd) != gesture.ProcessId || Caption(hwnd) != title) return false;
        try { using var process = Process.GetProcessById(gesture.ProcessId); return PdfCopyPolicy.IsWpsPdf(process.ProcessName, title); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }
    static string Caption(IntPtr hwnd) { var text = new StringBuilder(1024); GetWindowText(hwnd, text, text.Capacity); return text.ToString(); }
    sealed class NativePdfCopySource : IPdfCopySource
    {
        public uint Sequence => Native.GetClipboardSequenceNumber();
        public long Now => Environment.TickCount64;
        public string? Capture(Gesture gesture)
        {
            string caption = Caption(new(gesture.Window));
            if (!Current(gesture, caption)) return null;
            var hit = Native.WindowFromPoint(new() { X = gesture.StartX, Y = gesture.StartY });
            return Native.GetAncestor(hit, 2).ToInt64() == gesture.Window || Native.GetAncestor(hit, 3).ToInt64() == gesture.Window ? caption : null;
        }
        public bool IsCurrent(Gesture gesture, string title) => Current(gesture, title);
        public Task<SelectionReadResult?> Read(Gesture gesture, string title, uint baseline, uint sequence) => Task.Run(() => ReadFresh(gesture, title, baseline, sequence));
    }
    public void Dispose()
    {
        if (disposed) return;
        Clear(); disposed = true;
        if (Available) RemoveClipboardFormatListener(source.Handle);
        source.Dispose();
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int maximum);
    [DllImport("user32.dll")] static extern bool AddClipboardFormatListener(IntPtr window);
    [DllImport("user32.dll")] static extern bool RemoveClipboardFormatListener(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr window);
    [DllImport("user32.dll")] static extern bool CloseClipboard();
    [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint format);
    [DllImport("kernel32.dll")] static extern UIntPtr GlobalSize(IntPtr data);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr data);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr data);
}
