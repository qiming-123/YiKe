using System.Text.RegularExpressions;

namespace YiKe.Translation;

internal static class PdfText
{
    internal static string Clean(string text)
    {
        const string newline = @"(?:\r\n|\r(?!\n)|\n)";
        var blocks = Regex.Split(text, "(" + newline + "(?:[ \\t]*" + newline + ")+)");
        bool insideCode = false;
        for (int i = 0; i < blocks.Length; i += 2)
        {
            string block = blocks[i];
            string lines = block.Replace("\r\n", "\n").Replace('\r', '\n');
            bool preserve = insideCode || lines.Contains("```") || lines.Contains('\t') ||
                Regex.IsMatch(lines, @"(?m)^[ ]*(?:[-*•]|\d+[.)])\s|[{};]|^[ ]{4}|\S[ ]{2,}\S");
            if (Regex.Matches(lines, @"(?m)^[ ]*```").Count % 2 != 0) insideCode = !insideCode;
            if (preserve) continue;
            block = Regex.Replace(block, @"([A-Za-z]+)-" + newline + "([a-z]+)", m =>
                BundledEnglishDictionary.Find(m.Groups[1].Value + m.Groups[2].Value) is not null
                    ? m.Groups[1].Value + m.Groups[2].Value : m.Value);
            blocks[i] = Regex.Replace(block, "(?<=[A-Za-z,])" + newline + "(?=[a-z])", " ");
        }
        return string.Concat(blocks);
    }
}
