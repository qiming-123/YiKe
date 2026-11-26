using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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
    readonly Button translate, cancel, partial;
    internal Func<string,string,bool>? IsSaved { get; set; }
    public event Action<string>? Requested;
    internal event Action<string,string>? ContextRequested;
    public event Func<string,string>? FavoriteRequested;
    public event Action? CancelRequested;
    public event Action? ServiceChangeRequested;
    internal event Func<string,string,string>? LanguageFavoriteRequested;
    public ManualWindow(AppSettings settings) : this(settings,null) { }
    internal ManualWindow(AppSettings settings, YiKe.Data.StudyDictionaryPack? pack, YiKe.Data.StudyDictionaryPack[]? packs = null)
    {
        Title = "译刻 · 手动翻译"; Width = 560; Height = 520; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.WindowTheme(this); Ui.ConstrainToWorkArea(this);
        var grid = new Grid { Margin = new Thickness(20) };
        foreach (var height in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
            grid.RowDefinitions.Add(new RowDefinition { Height = height });
        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        var limitHint = Ui.Text("第 2,001 个码点起超出整段翻译上限。可选中一部分翻译。", 12, "Muted");
        limitHint.Visibility = Visibility.Collapsed;
        var actionArea = new StackPanel(); actionArea.Children.Add(actions); actionArea.Children.Add(limitHint);
        translate = Ui.Button("翻译", () => Requested?.Invoke(source.Text), true);
        cancel = Ui.Button("取消", () => { CancelRequested?.Invoke(); Finish("已取消"); });
        translate.ToolTip = "翻译（Ctrl+Enter）"; cancel.ToolTip = "取消（Esc）";
        PreviewKeyDown += (_, e) => { if (!e.IsRepeat && HandleShortcut(e.Key, e.KeyboardDevice.Modifiers)) e.Handled = true; };
        cancel.IsEnabled = false; actions.Children.Add(translate); actions.Children.Add(cancel);
        partial = Ui.Button("翻译选中部分", () => { if (TextLimit.Allowed(source.SelectedText)) Requested?.Invoke(source.SelectedText); });
        partial.Visibility = Visibility.Collapsed; actions.Children.Add(partial);
        void UpdatePartial() => partial.Visibility = limitHint.Visibility == Visibility.Visible && TextLimit.Allowed(source.SelectedText) ? Visibility.Visible : Visibility.Collapsed;
        source.TextChanged += (_, _) =>
        {
            limitHint.Visibility = source.Text.Length > TextLimit.Maximum && TextLimit.Count(source.Text) > TextLimit.Maximum ? Visibility.Visible : Visibility.Collapsed;
            UpdatePartial();
        };
        source.SelectionChanged += (_, _) => UpdatePartial();
        var direction = new WrapPanel { Margin = new Thickness(0,0,0,8) };
        bool restricted = settings.TranslationMode == "local" && settings.LocalEngine == "nmt";
        var contextAction = Ui.Button("按原句译选词", () => ContextRequested?.Invoke(source.SelectedText,SelectionSentence.Extract(source.Text,source.SelectionStart,source.SelectionLength)));
        var contextScope = Ui.Text("在线发送：选词 + 所在句",12,"Muted"); contextScope.Margin = new Thickness(10,0,0,0); contextScope.VerticalAlignment = VerticalAlignment.Center;
        System.Windows.Automation.AutomationProperties.SetName(contextScope,"在线发送范围");
        contextAction.Visibility = contextScope.Visibility = Visibility.Collapsed;
        actions.Children.Add(contextAction); actions.Children.Add(contextScope);
        void UpdateContextAction()
        {
            bool available = !restricted && source.SelectionLength > 0 && source.SelectionLength < source.Text.Length;
            contextAction.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
            contextScope.Visibility = available && settings.TranslationMode == "online" ? Visibility.Visible : Visibility.Collapsed;
            if (available) contextAction.ToolTip = "只翻译选中的词语，语境仅取其所在句。\n在线发送的原文范围：\n" + SelectionSentence.Extract(source.Text,source.SelectionStart,source.SelectionLength);
        }
        source.SelectionChanged += (_,_) => UpdateContextAction(); source.TextChanged += (_,_) => UpdateContextAction();
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
        if (restricted)
        {
            var limit = Ui.Text("节能引擎仅支持英语 → 简体中文。",12,"Muted"); limit.Margin = new Thickness(12,0,12,0); limit.VerticalAlignment = VerticalAlignment.Center;
            System.Windows.Automation.AutomationProperties.SetName(limit,"节能引擎限制");
            direction.Children.Add(limit); direction.Children.Add(Ui.Button("切换翻译服务",() => ServiceChangeRequested?.Invoke()));
            from.ToolTip = to.ToolTip = "其他语言方向请先切换翻译服务。";
        }
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
        DispatcherOperation? sourceLookup = null;
        void ScheduleSourceLookup()
        {
            if (!study.IsExpanded || sourceLookup?.Status == DispatcherOperationStatus.Pending) return;
            sourceLookup = Dispatcher.InvokeAsync(SourceLookup, DispatcherPriority.DataBind);
        }
        study.Expanded += (_,_) => ScheduleSourceLookup(); study.Collapsed += (_,_) => { sourceLookup?.Abort(); card.Stop(); };
        source.TextChanged += (_,_) => ScheduleSourceLookup(); source.SelectionChanged += (_,_) => ScheduleSourceLookup();
        result.SelectionChanged += (_,_) =>
        {
            if (result.SelectionLength == 0) return;
            study.IsExpanded = true; sourceLookup?.Abort();
            Lookup(result.SelectedText); alternatives.ShowTerm(result.SelectedText,To,dictionaries);
        };
        result.PreviewMouseLeftButtonUp += (_, e) => SelectResultWord(result.GetCharacterIndexFromPoint(e.GetPosition(result), false));
        Closed += (_,_) => { sourceLookup?.Abort(); card.DisposeSpeech(); }; SourceLookup();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        source.TextChanged += (_,_) => alternatives.Clear();
        var controls = new UIElement[] { direction, source, actionArea, Ui.Text("译文", 16), result, alternatives, study, status };
        for (int i = 0; i < controls.Length; i++) { Grid.SetRow(controls[i], i); grid.Children.Add(controls[i]); }
        status.Text = settings.ServiceLabel + " → " + settings.TargetLanguage; Content = grid; Loaded += (_, _) => source.Focus();
        System.Windows.Automation.AutomationProperties.SetName(source, "原文");
        System.Windows.Automation.AutomationProperties.SetName(result, "译文");
    }
    readonly AlternateMeanings alternatives = new();
    readonly YiKe.Data.StudyDictionaryPack[] dictionaries;
    internal bool SelectResultWord(int index)
    {
        if (index < 0 || result.SelectionLength > 0) return false;
        foreach (System.Text.RegularExpressions.Match word in StudyWords.Find(result.Text))
            if (index >= word.Index && index < word.Index + word.Length) { result.Select(word.Index, word.Length); return true; }
        return false;
    }
    internal bool HandleShortcut(Key key, ModifierKeys modifiers)
    {
        if (key == Key.Enter && modifiers == ModifierKeys.Control && translate.IsEnabled)
            translate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        else if (key == Key.Escape && modifiers == ModifierKeys.None && cancel.IsEnabled)
            cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        else return false;
        return true;
    }
    internal sealed record Draft(string Text, int SelectionStart, int SelectionLength, double VerticalOffset);
    internal Draft CaptureDraft() => new(source.Text, source.SelectionStart, source.SelectionLength, source.VerticalOffset);
    internal void RestoreDraft(Draft draft)
    {
        source.Text = draft.Text;
        Loaded += RestoreSelection;
        void RestoreSelection(object sender, RoutedEventArgs args)
        {
            Loaded -= RestoreSelection;
            source.Select(draft.SelectionStart, draft.SelectionLength);
            // WPF brings the restored selection into view during layout.
            Dispatcher.InvokeAsync(() => source.ScrollToVerticalOffset(draft.VerticalOffset), DispatcherPriority.Background);
        }
    }
    string submittedTerm = "", submittedFrom = "", submittedTo = "", service = "";
    public void Begin(AppSettings settings,string? term = null)
    {
        alternatives.Clear(); submittedTerm = term ?? source.Text; submittedFrom = From; submittedTo = To;
        service = settings.ServiceLabel + " → " + settings.TargetLanguage;
        from.IsHitTestVisible = to.IsHitTestVisible = false; from.Focusable = to.Focusable = false;
        source.IsReadOnly = true; result.Clear(); translate.IsEnabled = partial.IsEnabled = false; cancel.IsEnabled = true;
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
        source.IsReadOnly = false; translate.IsEnabled = partial.IsEnabled = true; cancel.IsEnabled = false; status.Text = service + " · " + label;
    }
}
