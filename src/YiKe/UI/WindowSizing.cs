using System;

namespace YiKe.UI;

internal readonly record struct WindowSizePlan(double Width, double Height, double MinWidth, double MinHeight, double MaxWidth, double MaxHeight);
internal static class WindowSizing
{
    internal static WindowSizePlan Calculate(double width, double height, double minWidth, double minHeight, int pixelsWide, int pixelsHigh, double scaleX, double scaleY)
    {
        double maxW = Math.Max(1, pixelsWide / scaleX), maxH = Math.Max(1, pixelsHigh / scaleY);
        double roomW = Math.Max(1, maxW - 32), roomH = Math.Max(1, maxH - 32);
        return new(Math.Min(width, roomW), Math.Min(height, roomH), Math.Min(minWidth, roomW), Math.Min(minHeight, roomH), maxW, maxH);
    }
}
