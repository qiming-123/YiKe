using YiKe.Core;

namespace YiKe.Selection;

public static class SelectionFeedback
{
    public static string Message(ReadStatus status, string? processName = null) => status switch
    {
        ReadStatus.Timeout => "读取选区超时。请重新划选；也可从托盘使用“粘贴文本翻译”。",
        ReadStatus.AccessDenied => "无法读取受保护或权限更高的控件。可从托盘使用“粘贴文本翻译”。",
        _ => processName is not null && VsCodeSelectionAdapter.IsCode(processName)
            ? "编辑区请安装 / 更新设置中的 VS Code 适配。Terminal 请选中文字后按 Ctrl+Alt+Y 复制并翻译 / 解释；更新适配后需重新打开 VS Code。"
            : $"未能读取{(string.IsNullOrWhiteSpace(processName) ? "此应用" : processName)}的文字选区。此界面可能未开放选区接口；可从托盘使用“粘贴文本翻译”。"
    };
}
