using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using YiKe.Core;

namespace YiKe.Selection;

public sealed class SelectionMonitor : IDisposable
{
    readonly Dispatcher dispatcher;
    readonly Native.HookProc callback;
    readonly Thread thread;
    Dispatcher? hookDispatcher;
    int disposed;
    internal int HookThreadId => thread.ManagedThreadId;
    IntPtr hook;
    readonly MouseGestureTracker tracker = new();
    long sequence;
    DateTimeOffset pressedAt;
    public event Action<int>? MouseDown;
    public event Action<Gesture>? Selected;
    public event Action<int, bool>? Unselected;
    public SelectionMonitor(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher; callback = Handle;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        thread = new Thread(() =>
        {
            try
            {
                hookDispatcher = Dispatcher.CurrentDispatcher;
                if (Volatile.Read(ref disposed) != 0) { ready.TrySetCanceled(); return; }
                hook = Native.SetWindowsHookEx(14, callback, Native.GetModuleHandle(null), 0);
                if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法安装鼠标监听。");
                ready.TrySetResult();
                if (Volatile.Read(ref disposed) == 0) Dispatcher.Run();
            }
            catch (Exception error) { ready.TrySetException(error); }
            finally { if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; } }
        }) { IsBackground = true, Name = "YiKe mouse listener" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        try { ready.Task.WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult(); }
        catch { Dispose(); throw; }
    }
    void Post(Action action)
    {
        if (Volatile.Read(ref disposed) != 0 || dispatcher.HasShutdownStarted) return;
        try { dispatcher.BeginInvoke(() => { if (Volatile.Read(ref disposed) == 0) action(); }, DispatcherPriority.Input); }
        catch (InvalidOperationException) { }
    }
    IntPtr Handle(int code, IntPtr wp, IntPtr lp)
    {
        if (code >= 0 && (wp.ToInt32() == 0x201 || wp.ToInt32() == 0x202 || wp.ToInt32() == 0x204 || wp.ToInt32() == 0x207))
        {
            var p = Marshal.PtrToStructure<Native.MouseData>(lp).Point;
            if (wp.ToInt32() != 0x202)
            {
                var hit = Native.WindowFromPoint(p);
                var window = Native.GetAncestor(hit, 2);
                bool textCandidate = wp.ToInt32() == 0x201 && !ShellSelectionPolicy.IgnoreDesktopPress(Native.ClassName(window), Native.ClassName(hit));
                if (textCandidate)
                {
                    pressedAt = DateTimeOffset.UtcNow;
                    uint dpi = Math.Clamp(Native.GetDpiForWindow(window), 96, 768);
                    tracker.Down(window.ToInt64(), p.X, p.Y, Environment.TickCount64, (int)((6 * dpi + 95) / 96), (int)((5 * dpi + 95) / 96));
                }
                else tracker.Cancel();
                int pid = Native.ProcessId(window);
                Post(() => { MouseDown?.Invoke(pid); if (!textCandidate) Unselected?.Invoke(pid, false); });
            }
            else
            {
                // Mouse capture keeps text selection in the pressed foreground source
                // even when the pointer is released just outside its window.
                var window = Native.GetForegroundWindow();
                if (tracker.Release(window.ToInt64(), p.X, p.Y, Environment.TickCount64, Native.GetDoubleClickTime(), out var start) && window == Native.GetForegroundWindow() && !dispatcher.HasShutdownStarted)
                {
                    var gesture = new Gesture(++sequence, window.ToInt64(), Native.ProcessId(window), p.X, p.Y, start.X, start.Y, DateTimeOffset.UtcNow, pressedAt);
                    Post(() => Selected?.Invoke(gesture));
                }
                else { int pid = Native.ProcessId(window); Post(() => Unselected?.Invoke(pid, true)); }
            }
        }
        return Native.CallNextHookEx(hook, code, wp, lp);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        try { hookDispatcher?.BeginInvokeShutdown(DispatcherPriority.Send); } catch (InvalidOperationException) { }
    }
}
