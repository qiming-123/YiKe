using System;

namespace YiKe.UI;

internal readonly record struct PixelBounds(int Left, int Top, int Right, int Bottom);
internal readonly record struct PixelPosition(int X, int Y);

internal static class PopupPlacement
{
    // All inputs and offsets are physical pixels. WPF dimensions are converted by the caller.
    internal static PixelPosition Calculate(int anchorX, int anchorY, int width, int height, PixelBounds work)
    {
        long w = Math.Max(1, width), h = Math.Max(1, height);
        long x = Math.Clamp((long)anchorX + 12, work.Left, Math.Max(work.Left, (long)work.Right - w));
        long below = (long)anchorY + 20;
        long y = below + h <= work.Bottom ? below : (long)anchorY - h - 16;
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, (long)work.Bottom - h));
        return new((int)x, (int)y);
    }
}
