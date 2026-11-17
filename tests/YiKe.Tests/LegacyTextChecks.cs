using YiKe.Core;
using YiKe.Selection;

static class LegacyTextChecks
{
    static Gesture Gesture(int endX = 130, int endY = 20) => new(1, 0, 0, endX, endY, 100, 20, DateTimeOffset.UtcNow);
    public static async Task Run(Func<string, Func<Task>, Task> check)
    {
        await check("Legacy selected-text fallback reads only explicit positive selection offsets", () =>
        {
            var text = new SelectedText { Start = 7, End = 12, Value = "world" };
            var result = LegacyTextReader.ReadSelected(text, Gesture());
            Assert(result.Text == "world" && text.Reads.SequenceEqual(new[] { (7, 12) })); return Task.CompletedTask;
        });
        await check("Legacy fallback rejects whole-document sentinels, invalid ranges, oversize and ambiguous multiple selections before reading", () =>
        {
            foreach (var text in new[] { new SelectedText { Start = -1 }, new SelectedText { Start = 5, End = 4 }, new SelectedText { End = int.MaxValue }, new SelectedText { Count = 2 } })
            {
                Assert(LegacyTextReader.ReadSelected(text, Gesture()).Status is ReadStatus.Unsupported or ReadStatus.TooLong);
                Assert(text.Reads.Count == 0);
            }
            return Task.CompletedTask;
        });
        await check("Legacy fallback cannot accept a nearby partial paragraph or selection elsewhere on screen", () =>
        {
            foreach (var gesture in new[] { Gesture(endY: 200), Gesture(endX: 600) })
            {
                var text = new SelectedText(); Assert(LegacyTextReader.ReadSelected(text, gesture).Status == ReadStatus.NoSelection && text.Reads.Count == 0);
            }
            return Task.CompletedTask;
        });
        await check("Legacy collapsed or absent selections return no text and never query control values", () =>
        {
            foreach (var text in new[] { new SelectedText { Count = 0 }, new SelectedText { Start = 4, End = 4 } }) Assert(LegacyTextReader.ReadSelected(text, Gesture()).Text is null && text.Reads.Count == 0);
            return Task.CompletedTask;
        });
        await check("Legacy fallback rejects embedded-object placeholders and counts Unicode scalars without truncation", () =>
        {
            Assert(LegacyTextReader.ReadSelected(new SelectedText { Value = "partial\uFFFC" }, Gesture()).Status == ReadStatus.Unsupported);
            Assert(LegacyTextReader.ReadSelected(new SelectedText { Start = 7, End = 12, Value = "unselected surrounding text" }, Gesture()).Status == ReadStatus.Unsupported);
            foreach (int length in new[] { 2000, 2001 })
            {
                var text = new SelectedText { Start = 0, End = length * 2, Value = string.Concat(Enumerable.Repeat("😀", length)) };
                Assert(LegacyTextReader.ReadSelected(text, Gesture()).Status == (length == 2000 ? ReadStatus.Success : ReadStatus.TooLong));
            }
            return Task.CompletedTask;
        });
        await check("Failure feedback names the actual source and limits editor advice to Code", () =>
        {
            string generic = SelectionFeedback.Message(ReadStatus.Unsupported, "SampleBrowser");
            Assert(generic.Contains("SampleBrowser") && !generic.Contains("VS Code") && SelectionFeedback.Message(ReadStatus.Unsupported, "Code").Contains("VS Code"));
            return Task.CompletedTask;
        });
    }
    static void Assert(bool value) { if (!value) throw new Exception("Legacy selected-text assertion failed"); }
    sealed class SelectedText : LegacyTextReader.ISelectedText
    {
        public int Count { get; set; } = 1;
        public int Start, End = 5;
        public string Value = "text";
        public List<(int, int)> Reads = new();
        public bool Range(int index, out int start, out int end) { start = Start; end = End; return true; }
        public bool Bounds(int offset, out int x, out int y, out int width, out int height) { x = offset == Start ? 100 : 125; y = 10; width = 10; height = 20; return true; }
        public string Text(int start, int end) { Reads.Add((start, end)); return Value; }
    }
}
