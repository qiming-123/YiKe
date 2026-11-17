using System;

namespace YiKe.Selection;

// Monotonic time and a matching left-button pair prevent unrelated releases from using old coordinates.
internal sealed class MouseGestureTracker
{
    bool pressed, previousClick;
    long downWindow, previousWindow, previousTime;
    long? downTime;
    int dragThreshold = 6, doubleClickDistance = 5;
    int downX, downY, previousX, previousY;

    internal void Down(long window, int x, int y, long? milliseconds = null, int dragDistance = 6, int clickDistance = 5)
    { pressed = true; downWindow = window; downX = x; downY = y; downTime = milliseconds; dragThreshold = Math.Max(1, dragDistance); doubleClickDistance = Math.Max(1, clickDistance); }
    internal void Cancel() { pressed = previousClick = false; }

    internal bool Release(long window, int x, int y, long milliseconds, uint doubleClickMilliseconds, out (int X, int Y) start)
    {
        start = (downX, downY);
        bool matched = pressed && window != 0 && downWindow == window;
        pressed = false;
        if (!matched) { previousClick = false; return false; }
        bool drag = Math.Abs((long)x - downX) >= dragThreshold || Math.Abs((long)y - downY) >= dragThreshold;
        long clickTime = downTime ?? milliseconds;
        long elapsed = clickTime - previousTime;
        bool doubleClick = !drag && previousClick && previousWindow == window && elapsed >= 0 && elapsed <= doubleClickMilliseconds &&
            Math.Abs((long)x - previousX) < doubleClickDistance && Math.Abs((long)y - previousY) < doubleClickDistance;
        previousClick = !drag;
        previousWindow = window; previousX = x; previousY = y; previousTime = clickTime;
        return drag || doubleClick;
    }
}
