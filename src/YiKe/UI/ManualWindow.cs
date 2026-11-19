using System;
using System.Windows;
using System.Windows.Controls;
using YiKe.Core;

namespace YiKe.UI;

public sealed class ManualWindow : Window
{
    readonly TextBox source = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    readonly TextBox result = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    readonly TextBlock status = Ui.Text("", 13, "Muted");
    readonly ComboBox from = new(), to = new();
    public string From => from.SelectedIndex == 0 ? "auto" : (string)from.SelectedItem;
    public string To => (string)to.SelectedItem;
    readonly Button translate, cancel;
    internal Func<string,string,bool>? IsSaved { get; set; }
    public event Action<string>? Requested;
    internal event Action<string,string>? ContextRequested;
    public event Func<string,string>? FavoriteRequested;
    public event Action? CancelRequested;
    internal event Func<string,string,string>? LanguageFavoriteRequested;
    public ManualWindow(AppSettings settings) : this(settings,null) { }
    internal ManualWindow(AppSettings settings, YiKe.Data.StudyDictionaryPack? pack, YiKe.Data.StudyDictionaryPack[]? packs = null)
    {
        Title = "译刻 · 手动翻译"; Width = 560; Height = 520; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.WindowTheme(this); Ui.ConstrainToWorkArea(this);
        var grid = new Grid { Margin = new Thickness(20) };
        foreach (var height in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
            grid.RowDefinitions.Add(new RowDefinition { Height = height });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
        translate = Ui.Button("翻译", () => Requested?.Invoke(source.Text), true);
        cancel = Ui.Button("取消", () => { CancelRequested?.Invoke(); Finish("已取消"); });
        cancel.IsEnabled = false; actions.Children.Add(translate); actions.Children.Add(cancel);
        var direction = new WrapPanel { Margin = new Thickness(0,0,0,8) };
        bool restricted = settings.TranslationMode == "local" && settings.LocalEngine == "nmt";
        var contextAction = Ui.Button("按原句译选词", () => ContextRequested?.Invoke(source.SelectedText,source.Text));
        contextAction.ToolTip = "把原文作为语境，只翻译选中的词语"; contextAction.Visibility = Visibility.Collapsed;
        actions.Children.Add(contextAction);
        source.SelectionChanged += (_,_) => contextAction.Visibility = !restricted && source.SelectionLength > 0 && source.SelectionLength < source.Text.Length ? Visibility.Visible : Visibility.Collapsed;
        contextAction.SetBinding(IsEnabledProperty,new System.Windows.Data.Binding("IsEnabled") { Source = translate });
        from.Items.Add("自动识别");
        foreach (var lang in TranslationLanguages.Presets)
        {
            if (!restricted || lang.StartsWith("英语")) from.Items.Add(lang);
            if (!restricted || lang == TranslationLanguages.Default) to.Items.Add(lang);
        }
        if (!restricted && !to.Items.Contains(settings.TargetLanguage)) to.Items.Add(settings.TargetLanguage);
        from.SelectedIndex = restricted ? 1 : 0; to.SelectedItem = restricted ? TranslationLanguages.Default : settings.TargetLanguage;
        from.IsEnabled = to.IsEnabled = !restricted;
        direction.Children.Add(from); direction.Children.Add(Ui.Text("  →  ",16)); direction.Children.Add(to);
        System.Windows.Automation.AutomationProperties.SetName(from,"源语言"); System.Windows.Automation.AutomationProperties.SetName(to,"目标语言");
        dictionaries = YiKe.Data.StudyDictionaryPack.Selected(packs ?? (pack == null ? Array.Empty<YiKe.Data.StudyDictionaryPack>() : new[] { pack }),settings);
        var card = new DictionaryCard { IsSaved = (code,word) => IsSaved?.Invoke(code,word) == true };
        var hint = Ui.Text("",12,"Muted");
        card.SaveWord += (code, word) => code == "en" ? FavoriteRequested?.Invoke(word) ?? "" : LanguageFavoriteRequested?.Invoke(code, word) ?? "";
        var panel = new StackPanel(); panel.Children.Add(hint); panel.Children.Add(card);
        var study = new Expander { Header = "单词研习", IsExpanded = settings.LearnEnglish, Content = new ScrollViewer { Content = panel, MaxHeight = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        void Lookup(string term)
        {
            if (study.IsExpanded) hint.Text = card.Lookup(term,dictionaries) ? "" : "输入或选中原文、译文中的单词或词组。当前词典未命中。";
        }
        void SourceLookup() => Lookup(source.SelectionLength > 0 ? source.SelectedText : source.Text);
        study.Expanded += (_,_) => SourceLookup(); study.Collapsed += (_,_) => card.Stop();
        source.TextChanged += (_,_) => SourceLookup(); source.SelectionChanged += (_,_) => SourceLookup();
        result.SelectionChanged += (_,_) => { if (result.SelectionLength > 0) { study.IsExpanded = true; Lookup(result.SelectedText); alternatives.ShowTerm(result.SelectedText,To,dictionaries); } };
        result.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (result.SelectionLength > 0) return;
            int index = result.GetCharacterIndexFromPoint(e.GetPosition(result), false);
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(result.Text, @"[\p{L}\p{M}]+(?:['’-][\p{L}\p{M}]+)*"))
                if (index >= match.Index && index < match.Index + match.Length) { result.Select(match.Index, match.Length); break; }
        };
        Closed += (_,_) => card.DisposeSpeech(); SourceLookup();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        source.TextChanged += (_,_) => alternatives.Clear();
        var controls = new UIElement[] { direction, source, actions, Ui.Text("译文", 16), result, alternatives, study, status };
        for (int i = 0; i < controls.Length; i++) { Grid.SetRow(controls[i], i); grid.Children.Add(controls[i]); }
        status.Text = settings.ServiceLabel + " → " + settings.TargetLanguage; Content = grid; Loaded += (_, _) => source.Focus();
        System.Windows.Automation.AutomationProperties.SetName(source, "原文");
        System.Windows.Automation.AutomationProperties.SetName(result, "译文");
    }
    readonly AlternateMeanings alternatives = new();
    readonly YiKe.Data.StudyDictionaryPack[] dictionaries;
    string submittedTerm = "", submittedFrom = "", submittedTo = "", service = "";
    public void Begin(AppSettings settings,string? term = null)
    {
        alternatives.Clear(); submittedTerm = term ?? source.Text; submittedFrom = From; submittedTo = To;
        service = settings.ServiceLabel + " → " + settings.TargetLanguage;
        from.IsHitTestVisible = to.IsHitTestVisible = false; from.Focusable = to.Focusable = false;
        source.IsReadOnly = true; result.Clear(); translate.IsEnabled = false; cancel.IsEnabled = true;
        status.Text = service + " · 准备引擎";
    }
    public void Error(string message) => Finish("失败 · 原文未发送。" + message);
    public void Update(TranslationUpdate update)
    {
        result.Text = update.Text;
        if (update.Running) status.Text = service + " · " + update.StatusLabel;
        else
        {
            if (update.Error == null && update.Text.Length > 0 && !alternatives.ShowTerm(submittedTerm,submittedFrom,dictionaries))
                alternatives.ShowTerm(update.Text,submittedTo,dictionaries);
            Finish(update.StatusLabel + (update.Error is null ? "" : " · " + update.Error));
        }
    }
    void Finish(string label)
    {
        from.IsHitTestVisible = to.IsHitTestVisible = true; from.Focusable = to.Focusable = true;
        source.IsReadOnly = false; translate.IsEnabled = true; cancel.IsEnabled = false; status.Text = service + " · " + label;
    }
}
