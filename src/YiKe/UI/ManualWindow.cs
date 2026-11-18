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
        var controls = new UIElement[] { Ui.Text("输入或粘贴原文", 16), source, actions, Ui.Text("译文", 16), result, status };
        for (int i = 0; i < controls.Length; i++) { Grid.SetRow(controls[i], i); grid.Children.Add(controls[i]); }
        status.Text = settings.ServiceLabel + " → " + settings.TargetLanguage; Content = grid; Loaded += (_, _) => source.Focus();
        System.Windows.Automation.AutomationProperties.SetName(source, "原文");
        System.Windows.Automation.AutomationProperties.SetName(result, "译文");
    }
    string service = "";
    public void Begin(AppSettings settings)
    {
        service = settings.ServiceLabel + " → " + settings.TargetLanguage;
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
        source.IsReadOnly = false; translate.IsEnabled = true; cancel.IsEnabled = false; status.Text = service + " · " + label;
    }
}
