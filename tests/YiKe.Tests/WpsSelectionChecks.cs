using YiKe.Core;
using YiKe.Selection;

static class WpsSelectionChecks
{
    static Gesture Gesture(int x = 135, int y = 20) => new(1, 1, 1, x, y, 100, 20, DateTimeOffset.UtcNow);
    public static async Task Run(Func<string, Func<Task>, Task> check)
    {
        await check("WPS adapter attaches only to Writer and preserves selected paragraph boundaries", () =>
        {
            Assert(WpsSelectionAdapter.IsWriter("WPS") && !WpsSelectionAdapter.IsWriter("wpspdf") && !WpsSelectionAdapter.IsWriter("et") && !WpsSelectionAdapter.IsWriter("notepad"));
            var range = new SelectedRange { Value = "first\rsecond\vthird", End = 18 };
            var result = WpsSelectionAdapter.ReadSelected(range, Gesture());
            Assert(result.Status == ReadStatus.Success && result.Text == "first\nsecond\nthird" && range.Reads == 1); return Task.CompletedTask;
        });
        await check("WPS insertion points cannot leak the character next to the caret", () =>
        {
            foreach (var range in new[] { new SelectedRange { Type = 1 }, new SelectedRange { Type = 0 }, new SelectedRange { End = 0 } })
                Assert(WpsSelectionAdapter.ReadSelected(range, Gesture()).Status == ReadStatus.NoSelection && range.Reads == 0 && range.BoundsCalls == 0);
            return Task.CompletedTask;
        });
        await check("WPS column, table, shape and malformed selection ranges are rejected before text access", () =>
        {
            foreach (var range in new[] { new SelectedRange { Type = 4 }, new SelectedRange { Type = 5 }, new SelectedRange { Type = 6 }, new SelectedRange { Type = 8 }, new SelectedRange { Start = -1 }, new SelectedRange { Start = 10, End = 5 } })
                Assert(WpsSelectionAdapter.ReadSelected(range, Gesture()).Status == ReadStatus.Unsupported && range.Reads == 0);
            return Task.CompletedTask;
        });
        await check("WPS stale selection away from either mouse endpoint is not read", () =>
        {
            foreach (var gesture in new[] { Gesture(x: 800), Gesture(y: 150), Gesture() with { StartX = -400 } })
            {
                var range = new SelectedRange(); Assert(WpsSelectionAdapter.ReadSelected(range, gesture).Status == ReadStatus.NoSelection && range.Reads == 0);
            }
            return Task.CompletedTask;
        });
        await check("WPS missing or invalid range geometry prevents unbounded selection access", () =>
        {
            foreach (var range in new[] { new SelectedRange { HasBounds = false }, new SelectedRange { Width = 0 }, new SelectedRange { Height = -1 } })
                Assert(WpsSelectionAdapter.ReadSelected(range, Gesture()).Status == ReadStatus.Unsupported && range.Reads == 0);
            var extreme = new SelectedRange { Left = int.MaxValue, Width = int.MaxValue };
            Assert(WpsSelectionAdapter.ReadSelected(extreme, Gesture()).Status == ReadStatus.NoSelection && extreme.Reads == 0); return Task.CompletedTask;
        });
        await check("WPS foreground, window, document or selection changes prevent stale text from escaping", () =>
        {
            foreach (int stopAt in new[] { 1, 2, 3 })
            {
                var range = new SelectedRange { StopAtCurrentCheck = stopAt };
                var result = WpsSelectionAdapter.ReadSelected(range, Gesture());
                Assert(result.Status == ReadStatus.TargetGone && result.Text is null && range.Reads == (stopAt == 3 ? 1 : 0));
            }
            return Task.CompletedTask;
        });
        await check("WPS rejects surrounding-text responses, object placeholders and null characters", () =>
        {
            foreach (var range in new[] { new SelectedRange { Value = "unselected surrounding text" }, new SelectedRange { Value = "a\uFFFCbcd" }, new SelectedRange { Value = "ab\0cd" } })
                Assert(WpsSelectionAdapter.ReadSelected(range, Gesture()).Status == ReadStatus.Unsupported);
            return Task.CompletedTask;
        });
        await check("WPS has a pre-read size bound and does not truncate 2000-scalar Unicode selections", () =>
        {
            var large = new SelectedRange { End = int.MaxValue }; Assert(WpsSelectionAdapter.ReadSelected(large, Gesture()).Status == ReadStatus.TooLong && large.Reads == 0);
            foreach (int count in new[] { 2000, 2001 })
            {
                var range = new SelectedRange { End = count * 2, Value = string.Concat(Enumerable.Repeat("😀", count)) };
                var result = WpsSelectionAdapter.ReadSelected(range, Gesture());
                Assert(result.Status == (count == 2000 ? ReadStatus.Success : ReadStatus.TooLong) && (result.Text is null || result.Text.Length == count * 2));
            }
            return Task.CompletedTask;
        });
        await check("WPS whitespace selections do not manufacture a translation request", () =>
        {
            var result = WpsSelectionAdapter.ReadSelected(new SelectedRange { Value = " \r\v\t " }, Gesture());
            Assert(result.Status == ReadStatus.NoSelection && result.Text is null); return Task.CompletedTask;
        });
    }
    static void Assert(bool value) { if (!value) throw new Exception("WPS selection assertion failed"); }
    sealed class SelectedRange : WpsSelectionAdapter.ISelectedRange
    {
        public int Type = 2, Start, End = 5, Left = 95, Top = 10, Width = 50, Height = 20;
        public bool HasBounds = true;
        public string Value = "hello";
        public int Reads, BoundsCalls, CurrentChecks, StopAtCurrentCheck = int.MaxValue;
        public WpsSelectionAdapter.SelectionState State => new(Type, Start, End, 1);
        public bool IsCurrent => ++CurrentChecks < StopAtCurrentCheck;
        public bool Bounds(out int left, out int top, out int width, out int height) { BoundsCalls++; left = Left; top = Top; width = Width; height = Height; return HasBounds; }
        public string Text() { Reads++; return Value; }
    }
}
