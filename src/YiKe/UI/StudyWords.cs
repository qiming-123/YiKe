using System.Text.RegularExpressions;

namespace YiKe.UI;

internal static class StudyWords
{
    internal static MatchCollection Find(string text) => Regex.Matches(text, @"[\p{L}\p{M}]+(?:['’-][\p{L}\p{M}]+)*");
}
