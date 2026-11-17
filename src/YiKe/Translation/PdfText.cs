using System;
using System.Text.RegularExpressions;

namespace YiKe.Translation;

internal static class PdfText
{
    internal static string Clean(string text)
    {
        string value = text.Replace("\r\n", "\n").Replace('\r', '\n');
        // Keep code, tabular material, lists and paragraph boundaries untouched.
        if (value.Contains('\t') || value.Contains("```") || Regex.IsMatch(value, @"(?m)^\s*(?:[-*•]|\d+[.)])\s|[{};]|^\s{4}")) return text;
        value = Regex.Replace(value, @"([A-Za-z]+)-\n([a-z]+)", m =>
            BundledEnglishDictionary.Find(m.Groups[1].Value + m.Groups[2].Value) is not null
                ? m.Groups[1].Value + m.Groups[2].Value : m.Value);
        return Regex.Replace(value, @"(?<=[A-Za-z,])\n(?=[a-z])", " ");
    }
}
