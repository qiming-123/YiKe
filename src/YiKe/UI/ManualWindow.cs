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
    public event Action<string>? Requested;
    public event Func<string,string>? FavoriteRequested;
    public event Action? CancelRequested;
    internal event Func<string,string,string>? LanguageFavoriteRequested;
    public ManualWindow(AppSettings settings) : this(settings,null) { }
    internal ManualWindow(AppSettings settings, YiKe.Data.JapanesePack? pack, YiKe.Data.JapanesePack[]? packs = null)
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
        var study = new Expander { Header = "英语研习", IsExpanded = settings.LearnEnglish };
        var card = Ui.Text("", 14); card.TextWrapping = TextWrapping.Wrap;
        var speech = new WordSpeech(); Closed += (_,_) => speech.Dispose();
        string word = "";
        var save = Ui.Button("收藏单词", () => status.Text = FavoriteRequested?.Invoke(word) ?? "");
        var speak = Ui.Button("朗读单词", () =>
        {
            try { speech.Speak(word); }
            catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { status.Text = "无法朗读：" + ex.Message; }
        });
        var tools = new WrapPanel(); tools.Children.Add(save); tools.Children.Add(speak);
        var studyPanel = new StackPanel(); studyPanel.Children.Add(tools); studyPanel.Children.Add(card);
        study.Content = new ScrollViewer { Content = studyPanel, MaxHeight = 145, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        void Lookup()
        {
            if (!study.IsExpanded) return;
            word = (source.SelectionLength > 0 ? source.SelectedText : source.Text).Trim();
            bool single = word.Length <= 80 && System.Text.RegularExpressions.Regex.IsMatch(word, @"^[A-Za-z]+(?:['-][A-Za-z]+)*$");
            save.IsEnabled = speak.IsEnabled = single;
            card.Text = single ? YiKe.Translation.BundledEnglishDictionary.Details(word) ?? "本地词库未收录此词。" : "输入一个英文单词，或在原文中选中一个词。";
        }
        study.Expanded += (_,_) => Lookup(); source.TextChanged += (_,_) => Lookup(); source.SelectionChanged += (_,_) => Lookup();
        study.Collapsed += (_,_) => speech.Stop(); Lookup();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var japanese = new Expander { Header = "日语研习" };
        var notes = Ui.Text("",14); notes.TextWrapping = TextWrapping.Wrap;
        string form = "";
        var picker = new ComboBox();
        foreach(var item in packs ?? (pack == null ? Array.Empty<YiKe.Data.JapanesePack>() : new[] { pack })) picker.Items.Add(item.Name);
        picker.SelectedIndex = 0;
        var collect = Ui.Button("收藏日语词条", () => status.Text = LanguageFavoriteRequested?.Invoke(pack!.Code,form) ?? "");
        var panel = new StackPanel(); panel.Children.Add(picker); panel.Children.Add(collect); panel.Children.Add(notes);
        japanese.Content = new ScrollViewer { Content = panel, MaxHeight = 145, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        void LookupJapanese()
        {
            if (!japanese.IsExpanded) return;
            form = (source.SelectionLength > 0 ? source.SelectedText : source.Text).Trim();
            var entry = pack?.Find(form);
            notes.Text = entry?.Card(form) ?? (pack?.Installed != true ? "请先到翻译服务 → 英语研习下方的「其他选项」安装对应词包。" : form.Length == 0 ? "输入单词或词形，或在原文中选中一个词。" : "当前词包未收录所选内容。");
            collect.IsEnabled = entry is not null;
        }
        picker.SelectionChanged += (_,_) => { if (packs == null) return; pack = packs[picker.SelectedIndex]; japanese.Header = pack.Name + "研习"; collect.Content = "收藏" + pack.Name + "词条"; LookupJapanese(); };
        if (packs != null) japanese.Header = "更多语言研习（日语等）";
        japanese.Expanded += (_,_) => { study.IsExpanded = false; LookupJapanese(); };
        study.Expanded += (_,_) => japanese.IsExpanded = false;
        source.TextChanged += (_,_) => LookupJapanese(); source.SelectionChanged += (_,_) => LookupJapanese();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var controls = new UIElement[] { direction, source, actions, Ui.Text("译文", 16), result, study, japanese, status };
        for (int i = 0; i < controls.Length; i++) { Grid.SetRow(controls[i], i); grid.Children.Add(controls[i]); }
        status.Text = settings.ServiceLabel + " → " + settings.TargetLanguage; Content = grid; Loaded += (_, _) => source.Focus();
        System.Windows.Automation.AutomationProperties.SetName(source, "原文");
        System.Windows.Automation.AutomationProperties.SetName(result, "译文");
    }
    string service = "";
    public void Begin(AppSettings settings)
    {
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
        else Finish(update.StatusLabel + (update.Error is null ? "" : " · " + update.Error));
    }
    void Finish(string label)
    {
        from.IsHitTestVisible = to.IsHitTestVisible = true; from.Focusable = to.Focusable = true;
        source.IsReadOnly = false; translate.IsEnabled = true; cancel.IsEnabled = false; status.Text = service + " · " + label;
    }
}
