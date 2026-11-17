using System.Windows.Automation;
using YiKe.Core;
using YiKe.Selection;

static class AutomaticSelectionChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check)
    {
        await check("Automatic read failures and absent selections stay silent without inventing selected text", () =>
        {
            foreach (var status in new[] { ReadStatus.NoSelection, ReadStatus.NotTextSelection, ReadStatus.Unsupported, ReadStatus.Timeout, ReadStatus.AccessDenied, ReadStatus.TargetGone })
                Assert(AutomaticSelectionPolicy.Decide(new(status, "stale focused document")) == AutomaticSelectionAction.Silent);
            return Task.CompletedTask;
        });
        await check("Only confirmed non-empty selected text or overlong selection can present an automatic card", () =>
        {
            foreach (string text in new[] { "", " ", "\r\n\t" }) Assert(AutomaticSelectionPolicy.Decide(new(ReadStatus.Success, text)) == AutomaticSelectionAction.Silent);
            foreach (string text in new[] { "how", "选中文字", "x", "😀" }) Assert(AutomaticSelectionPolicy.Decide(new(ReadStatus.Success, text)) == AutomaticSelectionAction.Translate);
            Assert(AutomaticSelectionPolicy.Decide(new(ReadStatus.TooLong)) == AutomaticSelectionAction.TooLong);
            Assert(AutomaticSelectionPolicy.Decide(new(ReadStatus.Success, new string('x', 2001))) == AutomaticSelectionAction.TooLong);
            return Task.CompletedTask;
        });
        await check("Buttons and other action controls cannot reuse stale focused document text; embedded editors remain readable", () =>
        {
            foreach (var control in new[] { ControlType.Button, ControlType.CheckBox, ControlType.RadioButton, ControlType.ComboBox, ControlType.MenuItem, ControlType.TabItem, ControlType.ToolBar, ControlType.HeaderItem, ControlType.ScrollBar, ControlType.Slider, ControlType.TitleBar })
            {
                Assert(!AutomaticSelectionPolicy.AllowsStart(new[] { ControlType.Text.Id, control.Id, ControlType.Document.Id }));
                Assert(AutomaticSelectionPolicy.AllowsStart(new[] { ControlType.Text.Id, ControlType.Edit.Id, control.Id }));
            }
            Assert(AutomaticSelectionPolicy.AllowsStart(new[] { ControlType.Text.Id, ControlType.ListItem.Id, ControlType.Document.Id }));
            return Task.CompletedTask;
        });
        await check("Click jitter below the DPI-scaled drag threshold does not become a selection gesture", () =>
        {
            foreach (int threshold in new[] { 6, 9, 12 })
            {
                var tracker = new MouseGestureTracker(); tracker.Down(1, 20, 20, 1000, threshold);
                Assert(!tracker.Release(1, 20 + threshold - 1, 21, 1100, 500, out _));
                tracker.Cancel(); tracker.Down(1, 20, 20, 2000, threshold);
                Assert(tracker.Release(1, 20 + threshold, 20, 2100, 500, out _));
            }
            return Task.CompletedTask;
        });
        await check("A click after a long held press is not falsely treated as an operating-system double click", () =>
        {
            var tracker = new MouseGestureTracker(); tracker.Down(1, 10, 10, 1000);
            Assert(!tracker.Release(1, 10, 10, 2000, 500, out _));
            tracker.Down(1, 10, 10, 2100);
            Assert(!tracker.Release(1, 10, 10, 2150, 500, out _));
            return Task.CompletedTask;
        });
        await check("Genuine short-word double clicks and clear text drags remain selection candidates", () =>
        {
            var tracker = new MouseGestureTracker(); tracker.Down(1, 10, 10, 1000);
            Assert(!tracker.Release(1, 11, 10, 1030, 500, out _));
            tracker.Down(1, 10, 10, 1200);
            Assert(tracker.Release(1, 11, 10, 1230, 500, out _));
            tracker.Cancel(); tracker.Down(1, 10, 10, 3000);
            Assert(tracker.Release(1, 150, 30, 3300, 500, out var start) && start == (10, 10));
            return Task.CompletedTask;
        });
    }
    static void Assert(bool value) { if (!value) throw new Exception("Automatic selection regression failed"); }
}
