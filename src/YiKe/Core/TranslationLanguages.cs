using System;
using System.Collections.Generic;

namespace YiKe.Core;

public static class TranslationLanguages
{
    public const string Default = "简体中文";
    public static IReadOnlyList<string> Presets { get; } = Array.AsReadOnly(new[]
    {
        Default, "繁體中文", "英语（English）", "日语（日本語）", "韩语（한국어）",
        "法语（Français）", "德语（Deutsch）", "西班牙语（Español）", "葡萄牙语（Português）",
        "俄语（Русский）", "意大利语（Italiano）", "阿拉伯语（العربية）", "印地语（हिन्दी）",
        "泰语（ไทย）", "越南语（Tiếng Việt）", "印度尼西亚语（Bahasa Indonesia）",
        "土耳其语（Türkçe）", "荷兰语（Nederlands）", "波兰语（Polski）", "乌克兰语（Українська）",
        "瑞典语（Svenska）", "马来语（Bahasa Melayu）"
    });
}
