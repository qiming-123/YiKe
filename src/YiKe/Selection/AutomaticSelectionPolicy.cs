using System.Collections.Generic;
using System.Windows.Automation;
using YiKe.Core;

namespace YiKe.Selection;

internal enum AutomaticSelectionAction { Silent, Translate, TooLong }

// A mouse gesture is only a candidate. Errors and absent selections are not
// evidence that the user selected text, so automatic operation stays quiet.
internal static class AutomaticSelectionPolicy
{
    internal static AutomaticSelectionAction Decide(SelectionReadResult result)
    {
        if (result.Status == ReadStatus.TooLong) return AutomaticSelectionAction.TooLong;
        if (result.Status != ReadStatus.Success || string.IsNullOrWhiteSpace(result.Text)) return AutomaticSelectionAction.Silent;
        return TextLimit.Allowed(result.Text) ? AutomaticSelectionAction.Translate : AutomaticSelectionAction.TooLong;
    }

    internal static bool AllowsStart(IEnumerable<int> kinds)
    {
        foreach (int kind in kinds)
        {
            // A search/address editor may be nested in a toolbar or combo box.
            if (kind == ControlType.Edit.Id || kind == ControlType.Document.Id) return true;
            if (kind == ControlType.Button.Id || kind == ControlType.CheckBox.Id || kind == ControlType.RadioButton.Id || kind == ControlType.ComboBox.Id || kind == ControlType.MenuItem.Id || kind == ControlType.MenuBar.Id || kind == ControlType.TabItem.Id || kind == ControlType.ToolBar.Id || kind == ControlType.HeaderItem.Id || kind == ControlType.ScrollBar.Id || kind == ControlType.Slider.Id || kind == ControlType.Thumb.Id || kind == ControlType.TitleBar.Id) return false;
        }
        return true; // Unknown/custom text surfaces still need a real selected range.
    }
}
