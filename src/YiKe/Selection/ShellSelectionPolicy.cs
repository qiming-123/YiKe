using System;
using System.Collections.Generic;
using System.Windows.Automation;
using YiKe.Core;

namespace YiKe.Selection;

// Shell item selection is not text selection. Do not read item names or a previous
// focused address field as a substitute for the user's current mouse selection.
internal static class ShellSelectionPolicy
{
    internal static bool IsShell(string rootClass) => rootClass is "Progman" or "WorkerW" or "CabinetWClass" or "ExploreWClass";
    internal static bool IsNativeEdit(string hitClass) => hitClass.Equals("Edit", StringComparison.OrdinalIgnoreCase) || hitClass.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase);
    internal static bool IgnoreDesktopPress(string rootClass, string hitClass) => (rootClass is "Progman" or "WorkerW") && !IsNativeEdit(hitClass);
    internal static bool ShowReadFailure(string rootClass, string hitClass) => !IsShell(rootClass) || IsNativeEdit(hitClass);
    internal static bool ShowReadFailure(Gesture gesture) => ShowReadFailure(Native.ClassName(new IntPtr(gesture.Window)), Native.ClassName(Native.WindowFromPoint(new() { X = gesture.StartX, Y = gesture.StartY })));

    internal static bool AllowsTextStart(IEnumerable<(int Kind, bool HasText)> ancestors)
    {
        bool text = false;
        foreach (var node in ancestors)
        {
            if (node.Kind == ControlType.Edit.Id) return true; // Rename/search/address editors may live inside an item.
            if (IsItemSurface(node.Kind)) return false;
            text |= node.HasText;
        }
        return text;
    }
    static bool IsItemSurface(int kind) => kind == ControlType.ListItem.Id || kind == ControlType.TreeItem.Id || kind == ControlType.DataItem.Id || kind == ControlType.List.Id || kind == ControlType.Tree.Id || kind == ControlType.DataGrid.Id || kind == ControlType.Button.Id || kind == ControlType.Header.Id || kind == ControlType.HeaderItem.Id || kind == ControlType.ScrollBar.Id || kind == ControlType.TitleBar.Id || kind == ControlType.Thumb.Id || kind == ControlType.MenuItem.Id || kind == ControlType.TabItem.Id;

    internal static bool AllowsTextStart(Gesture gesture, AutomationElement start, Func<AutomationElement, bool> belongs)
    {
        var hit = Native.WindowFromPoint(new() { X = gesture.StartX, Y = gesture.StartY });
        if (Native.GetAncestor(hit, 2).ToInt64() == gesture.Window && IsNativeEdit(Native.ClassName(hit))) return true;
        IEnumerable<(int, bool)> Ancestors()
        {
            var element = start;
            for (int depth = 0; element is not null && depth < 12; depth++, element = TreeWalker.RawViewWalker.GetParent(element))
            {
                if (!belongs(element)) yield break;
                int kind = element.Current.ControlType.Id;
                // Inspect capability only; never read Name, Value or document contents.
                bool hasText = kind != ControlType.Edit.Id && !IsItemSurface(kind) && element.TryGetCurrentPattern(TextPattern.Pattern, out _);
                yield return (kind, hasText);
            }
        }
        return AllowsTextStart(Ancestors());
    }
}
