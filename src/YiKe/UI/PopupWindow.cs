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
    readonly AlternateMeanings alternatives = new();
    string submittedTerm = "";
    readonly DictionaryCard card = new(separateTools:true);
    YiKe.Data.StudyDictionaryPack[] activePacks = Array.Empty<YiKe.Data.StudyDictionaryPack>();
    internal YiKe.Data.StudyDictionaryPack[] Packs { get; set; } = Array.Empty<YiKe.Data.StudyDictionaryPack>();
    readonly CheckBox japaneseSwitch = new() { Content = "多语研习" };
    readonly CheckBox technicalSwitch = new() { Content = "技术释义" }, englishSwitch = new() { Content = "英语研习" };
    public event Func<string,string,string>? FavoriteRequested;
    internal Func<string,string,bool>? IsSaved { set => card.IsSaved = value; }
    internal event Action<Button>? EnglishShown;
    internal event Action<FrameworkElement,int,string>? FeatureEnabled;
    AppSettings? options; bool loadingOptions;
    public event Action<AppSettings,bool>? OptionsChanged;
    readonly TextBlock programmingNotes = Ui.Text("", 12);
    readonly Expander programming;
    readonly Button pin, copy, retry, cancel;
    readonly Button original;
    readonly StackPanel dragArea;
    readonly ScrollViewer sourcePanel;
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
        dragArea = new StackPanel { Background = Brushes.Transparent, Orientation = Orientation.Horizontal, Children = { CyberPrism.Badge(30), new TextBlock { Text = "译刻", FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) } } }; top.Children.Add(dragArea); grid.Children.Add(top);
        dragArea.MouseLeftButtonDown += (_, e) => { if (Pinned && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) { DragMove(); e.Handled = true; } };
        var body = new StackPanel(); language.Margin = new Thickness(0, 0, 0, 12); body.Children.Add(language);
        translation.LineHeight = 24; body.Children.Add(translation); body.Children.Add(alternatives); error.Margin = new Thickness(0, 10, 0, 0); body.Children.Add(error);
        programmingNotes.LineHeight = 20;
        programming = new Expander { Header = "计算机说明 · 用法与示例", Content = programmingNotes, IsExpanded = true, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 14, 0, 0) };
        System.Windows.Automation.AutomationProperties.SetName(programming, "计算机用法解释");
        body.Children.Add(programming);
        contentScroll = new ScrollViewer { Content = body, ClipToBounds = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 254, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(contentScroll, 1); grid.Children.Add(contentScroll);
        status.Margin = new Thickness(0, 12, 0, 4); var studyFooter = new StackPanel(); studyFooter.Children.Add(card.Tools); studyFooter.Children.Add(status); Grid.SetRow(studyFooter, 2); grid.Children.Add(studyFooter);
        sourcePanel = new ScrollViewer { Content = source, MaxHeight = 70, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed };
        Grid.SetRow(sourcePanel,3); grid.Children.Add(sourcePanel);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,2,0,0) };
        original = Ui.Icon("查看原文","M2,5 L10,5 L12,7 L14,5 L22,5 L22,19 L15,19 L12,21 L9,19 L2,19 Z M12,7 L12,20",
            () => sourcePanel.Visibility = sourcePanel.IsVisible ? Visibility.Collapsed : Visibility.Visible); footer.Children.Add(original);
        pin = Ui.Icon("固定翻译浮窗","M8,3 L18,3 L16,6 L16,11 L19,14 L6,14 L9,11 L9,6 Z M12,14 L12,22", () => SetPinned(!Pinned)); DockPanel.SetDock(pin,Dock.Right); top.Children.Insert(1,pin);
        retry = Ui.Icon("重试（会再次调用服务）", "M20,9 A8,8 0 1 0 20,15 M20,3 L20,9 L14,9", () => Retry?.Invoke()); retry.Margin = new Thickness(4, 0, 4, 0); DockPanel.SetDock(retry, Dock.Right); footer.Children.Add(retry);
        cancel = Ui.Button("取消", () => { CancelRequested?.Invoke(); cancel!.Visibility = Visibility.Collapsed; status.Text = "已取消"; if (!copy!.IsEnabled) translation.Text = ""; });
        DockPanel.SetDock(cancel, Dock.Right); footer.Children.Add(cancel);
        copy = Ui.Icon("复制译文","M8,7 L20,7 L20,21 L8,21 Z M5,16 L3,16 L3,3 L15,3 L15,5",Copy); copy.HorizontalAlignment = HorizontalAlignment.Left; footer.Children.Add(copy); Grid.SetRow(footer, 4); grid.Children.Add(footer);
        var enhancements = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        var switches = new StackPanel { Orientation = Orientation.Horizontal }; technicalSwitch.Margin = new Thickness(0, 0, 16, 0); switches.Children.Add(technicalSwitch); switches.Children.Add(englishSwitch); switches.Children.Add(japaneseSwitch); enhancements.Children.Add(switches);
        body.Children.Add(card);
        card.SaveWord += (code,word) => FavoriteRequested?.Invoke(code,word) ?? "";
        card.EnglishShown += button => EnglishShown?.Invoke(button);
        enhancements.Visibility = Visibility.Collapsed; Grid.SetRow(enhancements,5); grid.Children.Add(enhancements);
        var extensions = Ui.Icon("翻译扩展","M3,7 L8,7 M12,7 L21,7 M3,17 L13,17 M17,17 L21,17 M10,4 L10,10 M15,14 L15,20",
            () => enhancements.Visibility = enhancements.IsVisible ? Visibility.Collapsed : Visibility.Visible);
        footer.Children.Add(extensions);
        englishSwitch.Click += (_,_) => { if (englishSwitch.IsChecked == true) FeatureEnabled?.Invoke(englishSwitch,1,"查看英语释义，点词条旁的星标收藏。"); };
        technicalSwitch.Click += (_,_) => { if (technicalSwitch.IsChecked == true) FeatureEnabled?.Invoke(technicalSwitch,2,"在译文下方展开计算机说明，可查看术语用法和示例。"); };
        japaneseSwitch.Click += (_, _) => ChangeOptions(); technicalSwitch.Click += (_, _) => ChangeOptions(); englishSwitch.Click += (_, _) => ChangeOptions();
        outer.Child = grid; Content = outer;
        SourceInitialized += (_, _) => { var h = (HwndSource)PresentationSource.FromVisual(this); Native.SetWindowLongPtr(h.Handle, -20, new IntPtr(Native.GetWindowLongPtr(h.Handle, -20).ToInt64() | 0x08000080L)); h.AddHook((IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled) => { if (msg == 0x21) { handled = true; return new IntPtr(3); } return IntPtr.Zero; }); };
        SizeChanged += (_, _) => Position();
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(Position, System.Windows.Threading.DispatcherPriority.Render);
    }
    public void Begin(string text, AppSettings settings, int x, int y, long hwnd, string? originalText = null)
    {
        activePacks = YiKe.Data.StudyDictionaryPack.Selected(Packs,settings);
        loadingOptions = true; options = settings; technicalSwitch.IsChecked = settings.ExplainProgrammingTerms; englishSwitch.IsChecked = settings.LearnEnglish; japaneseSwitch.IsChecked = settings.LearnJapanese; japaneseSwitch.IsEnabled = Array.Exists(Packs, p => p.Installed && !Array.Exists(settings.DisabledStudyLanguages,code => code == p.Code)); loadingOptions = false;
        bool studyEnabled = settings.LearnEnglish || settings.LearnJapanese && japaneseSwitch.IsEnabled;
        alternatives.Clear(); submittedTerm = text;
        card.Lookup(studyEnabled ? text : "", activePacks,settings.LearnEnglish);
        anchorX = x; anchorY = y; source.Text = originalText ?? text; translation.Text = "正在翻译所选文字…"; translation.FontSize = settings.FontSize; translation.LineHeight = settings.FontSize * 1.5;
        programmingNotes.Text = ""; programming.Visibility = Visibility.Collapsed; programming.IsExpanded = true;
        contentScroll.ScrollToTop();
        cancel.Visibility = Visibility.Visible;
        copy.ToolTip = "复制译文";
        pin.IsEnabled = true;
        retry.IsEnabled = text.Length > 0;
        original.IsEnabled = text.Length > 0;
        language.Text = settings.ServiceLabel + " · 自动 → " + settings.TargetLanguage; error.Text = ""; status.Text = "准备引擎"; sourcePanel.Visibility = Visibility.Collapsed; copy.IsEnabled = false;
        if (!IsVisible) { Show(); if (SystemParameters.ClientAreaAnimation) { BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))); } }
        Position();
    }
    public void Update(TranslationUpdate u)
    {
        if (!IsVisible) return;
        programmingNotes.Text = u.ProgrammingNotes;
        programming.Visibility = u.ProgrammingNotes.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        translation.Text = u.Text.Length == 0 ? u.Error is null ? "正在翻译所选文字…" : "暂时无法完成翻译" : u.Text;
        if (!u.Running && u.Error is null && u.Text.Length > 0)
        {
            if (!alternatives.ShowTerm(submittedTerm,"auto",activePacks)) alternatives.ShowTerm(u.Text,options!.TargetLanguage,activePacks);
            var matches = StudyWords.Find(u.Text);
            if (matches.Count > 0)
            {
                translation.Inlines.Clear(); int end = 0;
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    translation.Inlines.Add(u.Text[end..match.Index]);
                    var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(match.Value)) { TextDecorations = null, ToolTip = "查看词典释义" };
                    link.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "Text");
                    link.Click += (_,_) =>
                    {
                        alternatives.ShowTerm(match.Value,options!.TargetLanguage,activePacks);
                        error.Text = card.Lookup(match.Value,activePacks) ? "" : "词典未收录此词。";
                        card.OfferPhrases(u.Text,match.Index,activePacks);
                    };
                    translation.Inlines.Add(link); end = match.Index + match.Length;
                }
                translation.Inlines.Add(u.Text[end..]);
                if (options?.LearnEnglish == true && matches.Count > 1 && YiKe.Translation.BundledEnglishDictionary.Details(u.Text) != null)
                    card.Lookup(u.Text,activePacks);
            }
        }
        error.Text = u.Error ?? ""; status.Text = u.StatusLabel; cancel.Visibility = u.Running ? Visibility.Visible : Visibility.Collapsed; copy.IsEnabled = u.Text.Length > 0;
    }
    void ChangeOptions()
    {
        if (loadingOptions || options is null) return;
        bool refreshTranslation = options.ExplainProgrammingTerms != (technicalSwitch.IsChecked == true);
        options = options with { ExplainProgrammingTerms = technicalSwitch.IsChecked == true, LearnJapanese = japaneseSwitch.IsChecked == true, LearnEnglish = englishSwitch.IsChecked == true };
        activePacks = YiKe.Data.StudyDictionaryPack.Selected(Packs,options);
        card.Lookup(submittedTerm,activePacks,options.LearnEnglish);
        OptionsChanged?.Invoke(options,refreshTranslation);
    }
    public void StopSpeech() => card.Stop();
    public void DisposeSpeech() => card.DisposeSpeech();
    public void HideForSelection() { card.Stop(); Hide(); } // Keep the in-flight translation available for an identical re-selection.
    public void Error(string message) { translation.Text = "暂时无法完成翻译"; error.Text = message; status.Text = "失败 · 原文未发送"; cancel.Visibility = Visibility.Collapsed; }
    public void SelectionError(string message) { Error(message); translation.Text = "未能读取选区"; status.Text = "原文未发送 · 翻译尚未开始"; }
    void Copy()
    {
        try { System.Windows.Clipboard.SetText(translation.Text); copy.ToolTip = "已复制"; var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) }; timer.Tick += (_, _) => { copy.ToolTip = "复制译文"; timer.Stop(); }; timer.Start(); }
        catch (System.Runtime.InteropServices.COMException) { error.Text = "剪贴板暂时被占用，请再试一次。"; }
    }
    public void SetPinned(bool pinned)
    {
        if (Pinned == pinned) return;
        Pinned = pinned;
        pin.SetResourceReference(Control.BackgroundProperty,pinned ? "AccentSoft" : "Surface");
        dragArea.Cursor = pinned ? System.Windows.Input.Cursors.SizeAll : null;
        dragArea.ToolTip = pinned ? "拖动标题移动浮窗" : null;
        pin.ToolTip = pinned ? "解除固定后等待下一次划词" : "固定当前请求，停止接收新划词";
        System.Windows.Automation.AutomationProperties.SetName(pin, pinned ? "解除固定翻译浮窗" : "固定翻译浮窗");
        PinChanged?.Invoke(pinned);
    }
    public void Dismiss() { SetPinned(false); Hide(); Dismissed?.Invoke(); }
    void Position()
    {
        if (!IsVisible || !IsLoaded || Pinned || positioning) return;
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
