using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using YiKe.Core;

namespace YiKe.Selection;

// Registered only while Code is foreground. The explicit shortcut intentionally copies
// the terminal selection using Code's own command; automatic mouse reads never copy.
internal sealed class VsCodeTerminalHotkey : IDisposable
{
    const int Id = 0x594B;
    readonly HwndSource source = new(new HwndSourceParameters("YiKe terminal shortcut") { ParentWindow = new IntPtr(-3) });
    bool registered, disposed;
    internal bool IsRegistered => registered;
    public event Action<Gesture>? Requested;
    public VsCodeTerminalHotkey() { source.AddHook(Message); }
    public void Update(bool enabled)
    {
        if (disposed) return;
        bool wanted = enabled && IsCodeForeground(out _, out _);
        if (wanted && !registered) registered = RegisterHotKey(source.Handle, Id, 0x4000 | 0x0001 | 0x0002, 0x59);
        else if (!wanted && registered) { UnregisterHotKey(source.Handle, Id); registered = false; }
    }
    static bool IsCodeForeground(out IntPtr window, out int pid)
    {
        window = Native.GetForegroundWindow(); pid = Native.ProcessId(window);
        try { using var process = Process.GetProcessById(pid); return VsCodeSelectionAdapter.IsCode(process.ProcessName); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }
    IntPtr Message(IntPtr window, int message, IntPtr wp, IntPtr lp, ref bool handled)
    {
        if (message == 0x312 && wp.ToInt32() == Id && registered && IsCodeForeground(out var target, out int pid))
        {
            handled = true; GetCursorPos(out var point); var time = DateTimeOffset.UtcNow;
            Requested?.Invoke(new(-time.ToUnixTimeMilliseconds(), target.ToInt64(), pid, point.X, point.Y, point.X, point.Y, time, time));
        }
        return IntPtr.Zero;
    }
    public void Dispose() { if (disposed) return; disposed = true; if (registered) UnregisterHotKey(source.Handle, Id); source.Dispose(); }
    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Native.Point point);
}

internal static class VsCodeTerminal
{
    internal static bool FreshCopy(uint before, uint after, int owner, int target, System.Collections.Generic.IReadOnlyDictionary<int, int> parents) =>
        after != 0 && before != after && owner > 0 && VsCodeSelectionAdapter.IsDescendant(owner, target, parents);

    public static async Task<SelectionReadResult> ReadAsync(Gesture gesture, CancellationToken ct)
    {
        uint before = Native.GetClipboardSequenceNumber();
        if (!Current(gesture) || ct.IsCancellationRequested) return new(ReadStatus.TargetGone);
        bool acknowledged = await Task.Run(() => VsCodeSelectionAdapter.TryCopyTerminal(gesture), ct).ConfigureAwait(false);
        if (!acknowledged) return new(ReadStatus.Unsupported);
        var parents = VsCodeSelectionAdapter.ProcessParents();
        for (int attempt = 0; attempt < 16; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            if (!Current(gesture)) return new(ReadStatus.TargetGone);
            uint sequence = Native.GetClipboardSequenceNumber();
            int owner = Native.ProcessId(GetClipboardOwner());
            if (FreshCopy(before, sequence, owner, gesture.ProcessId, parents))
            {
                var value = ReadBounded(sequence, gesture, parents);
                if (value is not null) return value;
            }
            await Task.Delay(25, ct).ConfigureAwait(false);
        }
        return new(ReadStatus.NoSelection);
    }
    static bool Current(Gesture gesture) => Native.GetForegroundWindow().ToInt64() == gesture.Window && Native.ProcessId(new(gesture.Window)) == gesture.ProcessId;
    static SelectionReadResult? ReadBounded(uint sequence, Gesture gesture, System.Collections.Generic.IReadOnlyDictionary<int, int> parents)
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            if (!Current(gesture) || sequence != Native.GetClipboardSequenceNumber() || !VsCodeSelectionAdapter.IsDescendant(Native.ProcessId(GetClipboardOwner()), gesture.ProcessId, parents)) return null;
            IntPtr data = GetClipboardData(13); if (data == IntPtr.Zero) return new(ReadStatus.NoSelection);
            ulong size = GlobalSize(data).ToUInt64();
            // Do not materialize arbitrarily large copied output before applying the limit.
            if (size > 16004) return new(ReadStatus.TooLong);
            IntPtr pointer = GlobalLock(data); if (pointer == IntPtr.Zero) return null;
            try
            {
                int limit = (int)(size / 2), length = 0;
                while (length < limit && Marshal.ReadInt16(pointer, length * 2) != 0) length++;
                if (length == limit) return new(ReadStatus.Unsupported);
                string text = Marshal.PtrToStringUni(pointer, length) ?? "";
                if (!Current(gesture) || Native.GetClipboardSequenceNumber() != sequence) return new(ReadStatus.TargetGone);
                return string.IsNullOrWhiteSpace(text) ? new(ReadStatus.NoSelection) : TextLimit.Allowed(text) ? new(ReadStatus.Success, text) : new(ReadStatus.TooLong);
            }
            finally { GlobalUnlock(data); }
        }
        finally { CloseClipboard(); }
    }
    [DllImport("user32.dll")] static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr window);
    [DllImport("user32.dll")] static extern bool CloseClipboard();
    [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint format);
    [DllImport("kernel32.dll")] static extern UIntPtr GlobalSize(IntPtr data);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr data);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr data);
}
