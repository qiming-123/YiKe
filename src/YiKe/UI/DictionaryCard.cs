using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using YiKe.Data;
using YiKe.Translation;

namespace YiKe.UI;

internal sealed class DictionaryCard : StackPanel
{
    readonly WrapPanel choices = new(), actions = new();
    readonly TextBlock details = Ui.Text("", 14), feedback = Ui.Text("", 12, "Muted");
    readonly Button favorite, voiceSettings;
    readonly WrapPanel voices = new();
    internal Func<string,string,bool>? IsSaved { get; set; }
    readonly WordSpeech speech = new();
    string term = "", language = "";
    internal event Func<string,string,string>? SaveWord;
    internal event Action<Button>? EnglishShown;

    internal DictionaryCard()
    {
        favorite = Ui.Icon("收藏单词", "M12,2 L15,9 L23,10 L17,15 L18,23 L12,19 L5,23 L7,15 L1,10 L9,9 Z",
            () =>
            {
                feedback.Text = SaveWord?.Invoke(language,term) ?? "";
                RefreshStar();
            });
        voiceSettings = Ui.Button("打开 Windows 语音设置", () =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:speech") { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception ex) { feedback.Text = ex.Message; }
        });
        voiceSettings.Visibility = Visibility.Collapsed;
        foreach (bool british in new[] { true,false })
        {
            string label = british ? "英音" : "美音";
            var button = Ui.Icon(label+"朗读","M3,9 L7,9 L12,4 L12,20 L7,15 L3,15 Z M15,8 Q20,12 15,16",() =>
            {
                try { speech.Speak(term,british); voiceSettings.Visibility = Visibility.Collapsed; }
                catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { feedback.Text = ex.Message; voiceSettings.Visibility = Visibility.Visible; }
            });
            var icon = (FrameworkElement)button.Content; button.Content = null; icon.Width = icon.Height = 12;
            var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(Ui.Text(label,12)); row.Children.Add(icon);
            button.Content = row; button.Width = 48; voices.Children.Add(button);
        }
        actions.Children.Add(favorite); actions.Children.Add(voices);
        details.TextWrapping = TextWrapping.Wrap;
        Children.Add(choices); Children.Add(actions); Children.Add(feedback); Children.Add(voiceSettings); Children.Add(details);
        actions.Visibility = Visibility.Collapsed;
    }

    void RefreshStar()
    {
        bool saved = IsSaved?.Invoke(language,term) == true;
        var star = (System.Windows.Shapes.Path)favorite.Content;
        star.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,saved ? "Star" : "Muted");
        if (saved) star.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,"Star"); else star.Fill = null;
        favorite.ToolTip = saved ? "已收藏 · "+term : "收藏单词";
    }
    internal bool Lookup(string text, IEnumerable<StudyDictionaryPack> packs, bool english = true)
    {
        Stop(); voiceSettings.Visibility = Visibility.Collapsed; term = text.Trim(); choices.Children.Clear(); actions.Visibility = Visibility.Collapsed;
        feedback.Text = ""; details.Text = "";
        var hits = new List<(string Code,string Name,string Details)>();
        if (term.Length is > 0 and <= 256)
        {
            if (english && BundledEnglishDictionary.Details(term) is string entry) hits.Add(("en","英语",entry));
            foreach (var pack in packs)
                if (pack.Installed && pack.Find(term) is { } match) hits.Add((pack.Code,pack.Name,match.Card(term)));
        }
        void Select((string Code,string Name,string Details) hit)
        {
            Stop(); voiceSettings.Visibility = Visibility.Collapsed; language = hit.Code; details.Text = hit.Details; feedback.Text = hit.Name;
            actions.Visibility = Visibility.Visible;
            RefreshStar(); voices.Visibility = language == "en" && !term.Contains(' ') ? Visibility.Visible : Visibility.Collapsed;
            if (language == "en") EnglishShown?.Invoke(favorite);
        }
        if (hits.Count == 1) Select(hits[0]);
        else if (hits.Count > 1)
        {
            feedback.Text = "请选择词条语言";
            foreach (var hit in hits) choices.Children.Add(Ui.Button(hit.Name, () => Select(hit)));
        }
        return hits.Count > 0;
    }
    internal void OfferPhrases(string sentence,int at,StudyDictionaryPack[] packs)
    {
        var words = System.Text.RegularExpressions.Regex.Matches(sentence,@"[A-Za-z]+(?:['’-][A-Za-z]+)*");
        int end = 0;
        while (end < words.Count && words[end].Index != at) end++;
        if (end == words.Count) return;
        for (int start = Math.Max(0,end-4); start <= end; start++)
            for (int last = Math.Max(start+1,end); last < Math.Min(words.Count,start+5); last++)
            {
                var phrase = sentence[words[start].Index..(words[last].Index+words[last].Length)];
                if (BundledEnglishDictionary.Find(phrase) != null)
                    choices.Children.Add(Ui.Button(phrase,() => Lookup(phrase,packs)));
            }
    }
    internal void Stop() => speech.Stop();
    internal void DisposeSpeech() => speech.Dispose();
}
