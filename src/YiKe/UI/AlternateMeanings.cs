using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YiKe.Data;
using YiKe.Translation;

namespace YiKe.UI;

internal sealed class AlternateMeanings : Expander
{
    readonly TextBlock definitions = Ui.Text("",13);
    internal AlternateMeanings()
    {
        Header = "其他词义"; Visibility = Visibility.Collapsed;
        definitions.TextWrapping = TextWrapping.Wrap;
        Content = new ScrollViewer { Content = definitions, MaxHeight = 128, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        System.Windows.Automation.AutomationProperties.SetName(this,"其他词义");
    }

    internal void Clear()
    {
        IsExpanded = false; Visibility = Visibility.Collapsed; definitions.Text = "";
    }

    internal bool ShowTerm(string text,string language,IEnumerable<StudyDictionaryPack> packs)
    {
        Clear();
        string term = text.Trim();
        if (term.Length is 0 or > 256) return false;
        var groups = new List<(string Name,string Meaning)>();
        bool Matches(string name,string code) => language == "auto" || language.StartsWith(name,StringComparison.Ordinal) || language.Equals(code,StringComparison.OrdinalIgnoreCase);
        if (Matches("英语","en") && BundledEnglishDictionary.Find(term) is { Length: > 0 } english)
            groups.Add(("英语",english));
        foreach (var pack in packs)
            if (Matches(pack.Name,pack.Code) && pack.Installed && pack.Find(term) is { } entry)
                groups.Add((pack.Name,entry.Meaning));
        // Keep part-of-speech and specialist labels exactly as supplied by the dictionaries.
        if (groups.Count == 0 || groups.Count == 1 && groups[0].Meaning.IndexOfAny(new[] { '\n', ';', '；', ',', '，', '、' }) < 0) return false;
        definitions.Text = string.Join("\n\n",groups.Select(g => g.Name + " · " + term + "\n" + g.Meaning.Trim()));
        Visibility = Visibility.Visible;
        return true;
    }
}
