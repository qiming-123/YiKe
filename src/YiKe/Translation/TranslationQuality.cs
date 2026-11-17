using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace YiKe.Translation;

internal static class TranslationQuality
{
    // A language/format gate, not a substitute translation. Recognized commands,
    // names, acronyms and explicit code are allowed to remain Latin.
    static readonly HashSet<string> OrdinaryWords = new((
        "a an the this that these those I you he she it we they me us them my your his her its our their " +
        "who what when where why how which whose whom yes no not never always often sometimes " +
        "be am is are was were been being have has had do does did can could may might must shall should will would " +
        "and or but if then else because while before after during until since although however also only either neither " +
        "one two three four five six seven eight nine ten first second third last next previous all any some each every " +
        "more most less least many much few little another other same different new old good bad true false " +
        "please thank thanks help use used using make made get got go come see find found put take give keep let " +
        "add remove mark marks marked marking unmark unmarks unmarked unmarking uncover uncovers uncovered uncovering " +
        "cover covered reveal revealed hide hidden show shown open opened close closed save saved load loaded " +
        "select selected unselect check checked uncheck click press move moved start stop continue retry cancel undo redo " +
        "square squares squared rectangle rectangles circle circles grid grids cell cells board boards mine mines " +
        "neighbor neighbors neighbour neighbours neighboring neighbouring adjacent diagonal diagonally random randomly " +
        "safe safely lose lost win won play player players game games flag flags flagged empty full inside outside " +
        "left right up down above below between around through across along near far here there now later today tomorrow " +
        "text word words sentence sentences meaning example examples instruction instructions detail details information " +
        "correct wrong simple difficult possible impossible available unavailable important required optional success failure " +
        "hello goodbye morning evening time times number numbers name names file files window windows change changes " +
        "clear visible invisible small large long short both either without with into from over under about out off on " +
        "read write written return value values memory process thread function type public private static class")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
    static readonly Regex Han = new(@"[\u3400-\u9fff\uf900-\ufaff]", RegexOptions.CultureInvariant);
    static readonly Regex LowercaseWord = new(@"^[a-z]+(?:[-'][a-z]+)*$", RegexOptions.CultureInvariant);
    static readonly HashSet<string> PreservedNames = new(new[] { "OpenAI", "Windows", "Rust", "Linux", "Python", "Java", "JavaScript", "TypeScript", "GitHub", "GitLab", "Google", "Microsoft", "Debian", "Ubuntu", "Fedora", "OpenSSL", "openssl", "serde", "numpy", "NumPy", "PyTorch", "pytorch", "TensorFlow", "tensorflow", "VSCode", "vscode", "ChatGPT", "Copilot" }, StringComparer.Ordinal);
    internal static bool IsChinese(string target) => target.Contains("中文", StringComparison.Ordinal) || target.Contains("汉语", StringComparison.Ordinal) || target.Contains("漢語", StringComparison.Ordinal) || target.Contains("Chinese", StringComparison.OrdinalIgnoreCase) || target.Equals("zh", StringComparison.OrdinalIgnoreCase) || target.StartsWith("zh-", StringComparison.OrdinalIgnoreCase);
    internal static bool IsVocabulary(string source, string knownTerms)
    {
        string value = source.Trim();
        return knownTerms.Length == 0 && !PreservedNames.Contains(value) && (OrdinaryWords.Contains(value) || (value.Length <= 64 && LowercaseWord.IsMatch(value)));
    }
    internal static bool UntranslatedOrdinaryWord(string source, string translated, string target, string knownTerms) =>
        IsChinese(target) && IsVocabulary(source, knownTerms) && !Han.IsMatch(translated);
    internal static bool NeedsRepair(string source, string translated, string target, string knownTerms) =>
        string.IsNullOrWhiteSpace(translated) || UntranslatedOrdinaryWord(source, translated, target, knownTerms) ||
        (translated.Contains(TechnicalOutput.Marker, StringComparison.Ordinal) && !source.Contains(TechnicalOutput.Marker, StringComparison.Ordinal));
}
