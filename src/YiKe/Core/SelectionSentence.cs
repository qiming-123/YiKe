using System;

namespace YiKe.Core;

internal static class SelectionSentence
{
    public static string Extract(string text, int selectionStart, int selectionLength)
    {
        if (selectionStart < 0 || selectionStart > text.Length || selectionLength < 0 || selectionLength > text.Length - selectionStart) throw new ArgumentOutOfRangeException(nameof(selectionStart));
        int start = selectionStart, end = selectionStart + selectionLength;
        while (start > 0 && !IsBoundary(text[start - 1])) start--;
        while (end < text.Length && !IsBoundary(text[end])) end++;
        if (end < text.Length && IsSentenceEnd(text[end])) end++;
        return text[start..end].Trim();
    }
    static bool IsBoundary(char value) => IsSentenceEnd(value) || value is '\r' or '\n';
    static bool IsSentenceEnd(char value) => value is '.' or '!' or '?' or '。' or '！' or '？' or '…';
}
