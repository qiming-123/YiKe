using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using YiKe.Core;
using YiKe.Selection;

namespace YiKe.UI;

public sealed class PopupWindow : Window
{
    readonly TextBlock translation = Ui.Text("", 15), status = Ui.Text("", 12, "Muted"), language = Ui.Text("", 12, "Muted"), source = Ui.Text("", 12, "Muted");
    readonly TextBlock error = Ui.Text("", 12, "Error");
    readonly WordSpeech speech = new();
    internal YiKe.Data.JapanesePack? Japanese { get; set; }
    string speechText = "";
    readonly CheckBox japaneseSwitch = new() { Content = "日语研习" };
    readonly CheckBox technicalSwitch = new() { Content = "技术释义" }, englishSwitch = new() { Content = "英语研习" };
    readonly TextBlock wordNotes = Ui.Text("", 12);
    readonly Button speak, favorite;
    public event Func<string,string>? FavoriteRequested;
    AppSettings? options; bool loadingOptions;
    public event Action<AppSettings>? OptionsChanged;
    readonly TextBlock programmingNotes = Ui.Text("", 12);
    readonly Expander programming;
    readonly Button pin, copy, retry, cancel;
    readonly Expander original;
    readonly ScrollViewer contentScroll;
    int anchorX, anchorY;
    bool positioning;
    public bool Pinned { get; private set; }
    public event Action? Dismissed, Retry, Settings, CancelRequested;
    public event Action<bool>? PinChanged;
    public PopupWindow()
    {
        Ui.WindowTheme(this);
        Title = "译刻 · 翻译"; Width = 398; MaxHeight = 456; SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true; Background = Brushes.Transparent; ShowActivated = false; ShowInTaskbar = false; Topmost = true;
        var outer = new Border { Margin = new Thickness(8), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Padding = new Thickness(16), Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 3, Opacity = .18 } };
        outer.SetResourceReference(Border.BackgroundProperty, "Surface"); outer.SetResourceReference(Border.BorderBrushProperty, "Accent");
        var grid = new Grid(); for (int i = 0; i < 6; i++) grid.RowDefinitions.Add(new RowDefinition { Height = i == 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var close = Ui.Icon("关闭翻译", "M4,4 L20,20 M20,4 L4,20", Dismiss); DockPanel.SetDock(close, Dock.Right); top.Children.Add(close);
        var settings = Ui.Icon("打开设置", "M12,3 L12,7 M12,17 L12,21 M3,12 L7,12 M17,12 L21,12 M5,5 L8,8 M16,16 L19,19 M5,19 L8,16 M16,8 L19,5 M16,12 A4,4 0 1 1 8,12 A4,4 0 1 1 16,12", () => Settings?.Invoke()); DockPanel.SetDock(settings, Dock.Right); top.Children.Add(settings);
        top.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { CyberPrism.Badge(30), new TextBlock { Text = "译刻", FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) } } }); grid.Children.Add(top);
        var body = new StackPanel(); language.Margin = new Thickness(0, 0, 0, 12); body.Children.Add(language);
        translation.LineHeight = 24; body.Children.Add(translation); error.Margin = new Thickness(0, 10, 0, 0); body.Children.Add(error);
        programmingNotes.LineHeight = 20;
        programming = new Expander { Header = "计算机说明 · 用法与示例", Content = programmingNotes, IsExpanded = true, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 14, 0, 0) };
        System.Windows.Automation.AutomationProperties.SetName(programming, "计算机用法解释");
        body.Children.Add(programming);
        contentScroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 254, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(contentScroll, 1); grid.Children.Add(contentScroll);
        status.Margin = new Thickness(0, 12, 0, 4); Grid.SetRow(status, 2); grid.Children.Add(status);
        original = new Expander { Header = "查看原文", Content = new ScrollViewer { Content = source, MaxHeight = 70, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } }; Grid.SetRow(original, 3); grid.Children.Add(original);
        var footer = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        pin = Ui.Button("固定", () => SetPinned(!Pinned)); DockPanel.SetDock(pin, Dock.Right); footer.Children.Add(pin);
        retry = Ui.Icon("重试（会再次调用服务）", "M20,9 A8,8 0 1 0 20,15 M20,3 L20,9 L14,9", () => Retry?.Invoke()); retry.Margin = new Thickness(4, 0, 4, 0); DockPanel.SetDock(retry, Dock.Right); footer.Children.Add(retry);
        cancel = Ui.Button("取消", () => { CancelRequested?.Invoke(); cancel!.Visibility = Visibility.Collapsed; status.Text = "已取消"; if (!copy!.IsEnabled) translation.Text = ""; });
        DockPanel.SetDock(cancel, Dock.Right); footer.Children.Add(cancel);
        copy = Ui.Button("复制译文", Copy); copy.HorizontalAlignment = HorizontalAlignment.Left; footer.Children.Add(copy); Grid.SetRow(footer, 4); grid.Children.Add(footer);
        var enhancements = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        var switches = new StackPanel { Orientation = Orientation.Horizontal }; technicalSwitch.Margin = new Thickness(0, 0, 16, 0); switches.Children.Add(technicalSwitch); switches.Children.Add(englishSwitch); switches.Children.Add(japaneseSwitch); enhancements.Children.Add(switches);
        wordNotes.TextWrapping = TextWrapping.Wrap; body.Children.Add(wordNotes);
        speak = Ui.Button("朗读单词", () => { try { speech.Speak(speechText); } catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { error.Text = ex.Message; } }); body.Children.Add(speak);
        var extensions = new Expander { Header = "翻译扩展", Content = enhancements }; Grid.SetRow(extensions, 5); grid.Children.Add(extensions);
        favorite = Ui.Button("收藏单词", () => { error.Text = FavoriteRequested?.Invoke(speechText) ?? ""; }); body.Children.Add(favorite);
        japaneseSwitch.Click += (_, _) => ChangeOptions(); technicalSwitch.Click += (_, _) => ChangeOptions(); englishSwitch.Click += (_, _) => ChangeOptions();
        outer.Child = grid; Content = outer;
        SourceInitialized += (_, _) => { var h = (HwndSource)PresentationSource.FromVisual(this); Native.SetWindowLongPtr(h.Handle, -20, new IntPtr(Native.GetWindowLongPtr(h.Handle, -20).ToInt64() | 0x08000080L)); h.AddHook((IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled) => { if (msg == 0x21) { handled = true; return new IntPtr(3); } return IntPtr.Zero; }); };
        SizeChanged += (_, _) => Position();
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(Position, System.Windows.Threading.DispatcherPriority.Render);
    }
    public void Begin(string text, AppSettings settings, int x, int y, long hwnd, string? originalText = null)
    {
        loadingOptions = true; options = settings; technicalSwitch.IsChecked = settings.ExplainProgrammingTerms; englishSwitch.IsChecked = settings.LearnEnglish; japaneseSwitch.IsChecked = settings.LearnJapanese; japaneseSwitch.IsEnabled = Japanese?.Installed == true; loadingOptions = false;
        bool word = System.Text.RegularExpressions.Regex.IsMatch(text.Trim(), @"^[A-Za-z]+(?:['-][A-Za-z]+)*$") && text.Length <= 80;
        speechText = text.Trim();
        string? details = settings.LearnEnglish ? YiKe.Translation.BundledEnglishDictionary.Details(text) : null;
        var japanese = settings.LearnJapanese ? Japanese?.Find(text) : null;
        wordNotes.Text = japanese?.Card(speechText) ?? ( settings.LearnEnglish && (word || details is not null) ? details ?? "本地词库未收录此词条。" : "");
        favorite.Visibility = japanese is not null || settings.LearnEnglish && word ? Visibility.Visible : Visibility.Collapsed;
        speak.Visibility = settings.LearnEnglish && word ? Visibility.Visible : Visibility.Collapsed;
        anchorX = x; anchorY = y; source.Text = originalText ?? text; translation.Text = "正在翻译所选文字…"; translation.FontSize = settings.FontSize; translation.LineHeight = settings.FontSize * 1.5;
        programmingNotes.Text = ""; programming.Visibility = Visibility.Collapsed; programming.IsExpanded = true;
        contentScroll.ScrollToTop();
        cancel.Visibility = Visibility.Visible;
        copy.Content = "复制译文";
        pin.IsEnabled = true;
        retry.IsEnabled = text.Length > 0;
        original.IsEnabled = text.Length > 0;
        language.Text = settings.ServiceLabel + " · 自动 → " + settings.TargetLanguage; error.Text = ""; status.Text = "准备引擎"; original.IsExpanded = false; copy.IsEnabled = false;
        if (!IsVisible) { Show(); if (SystemParameters.ClientAreaAnimation) { BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))); } }
        Position();
    }
    public void Update(TranslationUpdate u)
    {
        if (!IsVisible) return;
        programmingNotes.Text = u.ProgrammingNotes;
        programming.Visibility = u.ProgrammingNotes.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        translation.Text = u.Text.Length == 0 ? u.Error is null ? "正在翻译所选文字…" : "暂时无法完成翻译" : u.Text;
        error.Text = u.Error ?? ""; status.Text = u.StatusLabel; cancel.Visibility = u.Running ? Visibility.Visible : Visibility.Collapsed; copy.IsEnabled = u.Text.Length > 0;
    }
    void ChangeOptions() { if (!loadingOptions && options is not null) { speech.Stop(); OptionsChanged?.Invoke(options with { ExplainProgrammingTerms = technicalSwitch.IsChecked == true, LearnJapanese = japaneseSwitch.IsChecked == true, LearnEnglish = englishSwitch.IsChecked == true }); } }
    public void StopSpeech() => speech.Stop();
    public void DisposeSpeech() => speech.Dispose();
    public void HideForSelection() { speech.Stop(); Hide(); } // Keep the in-flight translation available for an identical re-selection.
    public void Error(string message) { translation.Text = "暂时无法完成翻译"; error.Text = message; status.Text = "失败 · 原文未发送"; cancel.Visibility = Visibility.Collapsed; }
    public void SelectionError(string message) { Error(message); translation.Text = "未能读取选区"; status.Text = "原文未发送 · 翻译尚未开始"; }
    void Copy()
    {
        try { System.Windows.Clipboard.SetText(translation.Text); copy.Content = "已复制"; var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) }; timer.Tick += (_, _) => { copy.Content = "复制译文"; timer.Stop(); }; timer.Start(); }
        catch (System.Runtime.InteropServices.COMException) { error.Text = "剪贴板暂时被占用，请再试一次。"; }
    }
    public void SetPinned(bool pinned)
    {
        if (Pinned == pinned) return;
        Pinned = pinned;
        pin.Content = pinned ? "已固定" : "固定";
        pin.ToolTip = pinned ? "解除固定后等待下一次划词" : "固定当前请求，停止接收新划词";
        System.Windows.Automation.AutomationProperties.SetName(pin, pinned ? "解除固定翻译浮窗" : "固定翻译浮窗");
        PinChanged?.Invoke(pinned);
    }
    public void Dismiss() { SetPinned(false); Hide(); Dismissed?.Invoke(); }
    void Position()
    {
        if (!IsVisible || !IsLoaded || positioning) return;
        positioning = true;
        try
        {
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(anchorX, anchorY)).WorkingArea;
            var dpi = VisualTreeHelper.GetDpi(this);
            MaxWidth = Math.Min(398, Math.Max(1, screen.Width / dpi.DpiScaleX));
            MaxHeight = Math.Min(456, Math.Max(1, screen.Height / dpi.DpiScaleY));
            int w = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX), h = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
            var p = PopupPlacement.Calculate(anchorX, anchorY, w, h, new(screen.Left, screen.Top, screen.Right, screen.Bottom));
            Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), p.X, p.Y, 0, 0, 0x11);
        }
        finally { positioning = false; }
    }
}
