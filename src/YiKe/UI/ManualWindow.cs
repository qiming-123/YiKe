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
    public event Action? CancelRequested;
    public ManualWindow(AppSettings settings)
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
        var controls = new UIElement[] { direction, source, actions, Ui.Text("译文", 16), result, status };
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
