using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using YiKe.Core;
using YiKe.Data;
using YiKe.Translation;

namespace YiKe.UI;

public sealed class SettingsWindow : Window
{
    readonly Func<AppSettings> get;
    readonly Action<AppSettings, string?> commit;
    readonly Action? credentialDeleted;
    readonly CredentialStore credentials;
    readonly SettingsStore store;
    readonly Func<string> diagnostics;
    readonly TextBox url = new() { MaxLength = 2048 };
    readonly ComboBox profileChoice = new() { DisplayMemberPath = nameof(OnlineProfile.Name), SelectedValuePath = nameof(OnlineProfile.Id) };
    readonly TextBox profileName = new() { MaxLength = 60 };
    AppSettings onlineDraft = new();
    readonly Dictionary<Guid, string> pendingKeys = new();
    readonly ComboBox model = new() { IsEditable = true, IsTextSearchEnabled = false, StaysOpenOnEdit = true },
        target = new() { IsEditable = true, IsTextSearchEnabled = false, StaysOpenOnEdit = true };
    readonly TextBox exclusions = new() { MaxLength = 16384 }, first = new() { MaxLength = 3 }, total = new() { MaxLength = 3 };
    readonly PasswordBox key = new() { MaxLength = 8192 };
    readonly CheckBox stream = new() { Content = "逐步显示译文" }, startup = new() { Content = "登录 Windows 时启动译刻" };
    readonly CheckBox copiedPdf = new() { Content = "WPS PDF 自动划词翻译（保留 Ctrl+C 兼容方式）" };
    readonly CheckBox programming = new() { Content = "技术释义：计算机术语、命令与示例" };
    readonly CheckBox supplement = new() { Content = "用模型补充计算机用法与示例" };
    readonly CheckBox learnJapanese = new() { Content = "多语研习（使用已安装的词典）" };
    readonly CheckBox learnEnglish = new() { Content = "英语研习：单词多释义和点击朗读" }, iconMode = new() { Content = "划词后显示图标，点击才翻译（关闭则自动翻译）" }, cleanPdf = new() { Content = "智能整理 PDF 正文断行" };
    readonly ComboBox localEngine = new();
    readonly ComboBox preset = new(), theme = new(), font = new(), role = new(), mode = new();
    readonly StackPanel onlineFields = new();
    readonly TextBlock servicePrivacy = Ui.Text("", 11, "Muted");
    FrameworkElement? localCard;
    readonly Func<LocalHardwareSnapshot> localHardware;
    readonly TextBlock hardwareInfo = Ui.Text("", 11, "Muted"), localTitle = Ui.Text("", 15, "Accent", true);
    readonly DispatcherTimer hardwareTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    readonly bool ownsTestProvider;
    readonly TextBlock feedback = Ui.Text("", 12, "Accent"), keyState = Ui.Text("", 12, "Good");
    readonly StackPanel pages = new();
    readonly Dictionary<int, UIElement[]> cachedPages = new();
    readonly List<Button> navigation = new();
    readonly ITranslationProvider testProvider;
    readonly TranslationCoordinator connectionTests;
    readonly IModelCatalog modelCatalog;
    readonly DispatcherTimer modelDelay = new() { Interval = TimeSpan.FromMilliseconds(800) };
    readonly TextBlock modelState = Ui.Text("填写地址和 API Key 后自动获取模型；也可手动填写。", 11, "Muted");
    CancellationTokenSource? modelCancellation;
    long modelVersion;
    bool updatingModels;
    Button? refreshModelsButton;
    TextBlock? diagnosticsText;
    Button? testButton;
    bool replaceKey, hasKey, loading, closed, testing, adapting;
    string? credentialNotice;

    readonly UniformGrid otherOptions = new() { Columns = 2 };
    readonly Dictionary<string,CheckBox> languageChoices = new();
    internal void ShowPackInstaller(StudyDictionaryPack pack)
    {
        var message = Ui.Text("", 12, "Muted"); message.TextWrapping = TextWrapping.Wrap;
        var choice = new CheckBox { Content = pack.Name, Margin = new Thickness(0), MinHeight = 24, FontSize = 12, IsChecked = !get().DisabledStudyLanguages.Contains(pack.Code), VerticalAlignment = VerticalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(choice,"开启"+pack.Name+"研习");
        languageChoices.Add(pack.Code,choice);
        choice.SetBinding(IsEnabledProperty,new System.Windows.Data.Binding("IsChecked") { Source = learnJapanese });
        var row = new DockPanel { Margin = new Thickness(2,1,12,1) };
        var install = StudyBook.InstallButton(pack,message,compact:true); DockPanel.SetDock(install,Dock.Right); row.Children.Add(install); row.Children.Add(choice);
        var cell = new StackPanel(); cell.Children.Add(row); cell.Children.Add(message); otherOptions.Children.Add(cell);
        var messageStyle = new Style(typeof(TextBlock));
        var empty = new Trigger { Property = TextBlock.TextProperty, Value = "" }; empty.Setters.Add(new Setter(VisibilityProperty,Visibility.Collapsed));
        messageStyle.Triggers.Add(empty); message.Style = messageStyle;
    }
    public event Action? ManualRequested, StudyRequested, GuideDismissed;
    public SettingsWindow(Func<AppSettings> get, Action<AppSettings, string?> commit, SettingsStore store,
        CredentialStore credentials, Func<string> diagnostics, Action? credentialDeleted = null, ITranslationProvider? testProvider = null, IModelCatalog? modelCatalog = null, Func<LocalHardwareSnapshot>? localHardware = null)
    {
        this.get = get; this.commit = commit; this.store = store; this.credentials = credentials;
        this.diagnostics = diagnostics; this.credentialDeleted = credentialDeleted;
        this.localHardware = localHardware ?? LocalHardware.Detect;
        hardwareTimer.Tick += (_, _) => RefreshHardware();
        Loaded += (_, _) => { RefreshHardware(); hardwareTimer.Start(); };
        Closed += (_, _) => hardwareTimer.Stop();
        ownsTestProvider = testProvider is null;
        this.testProvider = testProvider ?? new TranslationProvider();
        this.modelCatalog = modelCatalog ?? new ModelCatalog();
        modelDelay.Tick += async (_, _) => { modelDelay.Stop(); await RefreshModelsAsync(); };
        connectionTests = new(this.testProvider, update => Dispatcher.BeginInvoke(() => ReceiveTestResult(update)), useCache: false);
        Ui.WindowTheme(this);
        Title = "译刻 · 设置"; Width = 800; Height = 760; MinWidth = 680; MinHeight = 570;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.ConstrainToWorkArea(this);
        foreach (var value in new[] { "OpenAI", "自定义兼容服务", "DeepL" }) preset.Items.Add(value);
        foreach (var value in new[] { "本地离线（选择引擎）", "在线 API" }) mode.Items.Add(value);
        foreach (var value in new[] { "英译中节能引擎", "通用多语模型" }) localEngine.Items.Add(value);
        localEngine.SelectionChanged += (_, _) => { if (!loading) { DraftChanged(); RefreshHardware(); } };
        foreach (var value in new[] { "深色 · 赛博朋克", "浅色 · Y2K 银晶" }) theme.Items.Add(value);
        foreach (double size in new double[] { 12, 13, 14, 15, 16, 18, 20, 22, 24 }) font.Items.Add(size);
        foreach (var value in new[] { "system", "developer" }) role.Items.Add(value);
        foreach (var value in TranslationLanguages.Presets) target.Items.Add(value);
        model.Loaded += (_, _) => SetEditorLimit(model, 200);
        target.Loaded += (_, _) => SetEditorLimit(target, 80);
        ReloadFromSaved();
        Theme.Apply(get().Theme);
        Resources[typeof(Button)] = CyberPrism.ButtonStyle();

        var root = new Grid();
        root.ColumnDefinitions.Add(new() { Width = new GridLength(196) });
        root.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var sidebar = new Grid { Margin = new Thickness(20, 26, 16, 20) };
        sidebar.RowDefinitions.Add(new() { Height = GridLength.Auto }); sidebar.RowDefinitions.Add(new());
        sidebar.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var brand = new StackPanel(); brand.Children.Add(CyberPrism.Monogram(82));
        var name = Ui.Text("Y I K E", 23, bold: true); name.Margin = new Thickness(0, 8, 0, 6);
        brand.Children.Add(name); brand.Children.Add(Ui.Text("译刻 · 让理解自然发生", 11, "Muted")); sidebar.Children.Add(brand);
        var nav = new StackPanel { Margin = new Thickness(0, 42, 0, 0) };
        string[] titles = { "翻译服务", "外观与行为", "数据与迁移" };
        for (int i = 0; i < titles.Length; i++)
        {
            int index = i;
            var button = Ui.Button(titles[i], () => ShowPage(index));
            button.Margin = new Thickness(0, 0, 0, 8); button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.BorderThickness = new Thickness(0); navigation.Add(button); nav.Children.Add(button);
        }
        var manual = Ui.Button("手动翻译", () => ManualRequested?.Invoke(), true);
        manual.Margin = new Thickness(0, 12, 0, 8); nav.Children.Add(manual);
        var book = Ui.Button("单词收藏本", () => StudyRequested?.Invoke()); nav.Children.Add(book);
        var guide = new StackPanel { MaxWidth = 340 };
        var heading = Ui.Text("", 20, bold:true); var tips = Ui.Text("",14); tips.TextWrapping = TextWrapping.Wrap;
        guide.Children.Add(heading); guide.Children.Add(tips);
        var guideCard = Ui.Card(guide,18,"Surface"); guideCard.VerticalAlignment = VerticalAlignment.Center;
        var backdrop = new Grid { Visibility = Visibility.Collapsed };
        backdrop.SetResourceReference(Panel.BackgroundProperty,"Canvas");
        Grid.SetColumn(backdrop,1); backdrop.Children.Add(guideCard);
        Panel.SetZIndex(backdrop,10); root.Children.Add(backdrop);
        var steps = new (string Title,string Body,FrameworkElement Target)[] {
            ("欢迎使用译刻","先用半分钟熟悉划词翻译。你可以随时跳过，以后在「使用指南」里重新打开。", brand),
            ("划词翻译","在浏览器或文档中拖选一句话，松开鼠标。开启图标模式时点击「译」查看译文。选完整句子，才能让引擎根据语境判断词义。",navigation[0]),
            ("手动翻译","点击左侧入口，粘贴原文并指定语言方向。翻译完成后可点击英文单词，或拖选整个词组查释义。",manual),
            ("研习与收藏","在翻译服务中开启英语研习或计算机释义。词条旁的星标可收藏，之后从这里复习、删除和导出。",book),
            ("选择适用的应用","在「外观与行为」里可关闭某些应用的划词响应。设置改完后，点击「保存设置」。",navigation[1])
        };
        int step = 0;
        var marker = Ui.Text("◀",20,"Accent"); marker.HorizontalAlignment = HorizontalAlignment.Right; marker.VerticalAlignment = VerticalAlignment.Top;
        marker.Visibility = Visibility.Collapsed; Panel.SetZIndex(marker,11); root.Children.Add(marker);
        var next = Ui.Button("下一步", () => { },true); next.Click += (_,_) => Advance();
        void ShowStep(int index)
        {
            step = index; heading.Text = steps[index].Title; tips.Text = steps[index].Body;
            tips.Margin = new Thickness(0,12,0,16); next.Content = index == steps.Length-1 ? "开始使用" : "下一步";
            backdrop.Visibility = Visibility.Visible; root.UpdateLayout();
            marker.Visibility = index == 0 ? Visibility.Collapsed : Visibility.Visible;
            var target = steps[index].Target;
            marker.Margin = new Thickness(0,Math.Max(0,target.TranslatePoint(new Point(0,target.ActualHeight/2),root).Y-14),2,0);
        }
        void CloseGuide() { backdrop.Visibility = marker.Visibility = Visibility.Collapsed; GuideDismissed?.Invoke(); }
        void Advance() { if (step+1 < steps.Length) ShowStep(step+1); else CloseGuide(); }
        var guideActions = new WrapPanel(); guideActions.Children.Add(next); guideActions.Children.Add(Ui.Button("跳过 / 完成教程", CloseGuide)); guide.Children.Add(guideActions);
        nav.Children.Add(Ui.Button("使用指南", () => ShowStep(0)));
        Loaded += (_,_) => { if (!get().GuideSeen) ShowStep(0); };
        Grid.SetRow(nav, 1); sidebar.Children.Add(nav);
        var bottom = Ui.Text("YiKe " + System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(SettingsWindow).Assembly)?.InformationalVersion.Split('+')[0] + "\n本地离线 · 可选在线\nWindows x64", 11, "Muted");
        bottom.LineHeight = 20; Grid.SetRow(bottom, 2); sidebar.Children.Add(bottom);
        var sideBorder = new Border { Child = new ScrollViewer { Content = sidebar, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, BorderThickness = new Thickness(0, 0, 1, 0) };
        sideBorder.SetResourceReference(Border.BackgroundProperty, "RailFinish"); sideBorder.SetResourceReference(Border.BorderBrushProperty, "Line");
        root.Children.Add(sideBorder);
        var right = new Grid { Margin = new Thickness(30, 26, 30, 20) };
        Grid.SetColumn(right, 1); right.RowDefinitions.Add(new()); right.RowDefinitions.Add(new() { Height = GridLength.Auto });
        right.Children.Add(new ScrollViewer { Content = pages, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0) });
        var footer = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
        feedback.Margin = new Thickness(0, 0, 0, 10); footer.Children.Add(feedback);
        var actions = new DockPanel(); var save = Ui.Button("保存设置", SaveSettings, true); save.MinWidth = 112;
        DockPanel.SetDock(save, Dock.Right); actions.Children.Add(save); actions.Children.Add(Ui.Text("保存后，下次启动自动使用", 12, "Muted"));
        footer.Children.Add(actions); Grid.SetRow(footer, 1); right.Children.Add(footer); root.Children.Add(right); Content = CyberPrism.Frame(this, root);
        ShowPage(0);
        ApplyMode();

        preset.SelectionChanged += (_, _) => { if (loading) return; if (preset.SelectedIndex == 0) url.Text = "https://api.openai.com/v1"; if ((string?)preset.SelectedItem == "DeepL") { url.Text = "https://api-free.deepl.com/v2"; model.Text = "DeepL"; } DraftChanged(); ScheduleModels(); };
        mode.SelectionChanged += (_, _) => { if (loading) return; DraftChanged(); ApplyMode(); ScheduleModels(); };
        theme.SelectionChanged += (_, _) => { if (!loading) Theme.Apply(ThemeChoice()); };
        foreach (var field in new[] { url, first, total }) field.TextChanged += (_, _) => DraftChanged();
        foreach (var field in new[] { model, target })
        {
            field.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => { if (!updatingModels) DraftChanged(); }));
            field.SelectionChanged += (_, _) => { if (!updatingModels) DraftChanged(); };
        }
        url.TextChanged += (_, _) => { if (!loading) ScheduleModels(); };
        role.SelectionChanged += (_, _) => DraftChanged();
        stream.Checked += (_, _) => DraftChanged(); stream.Unchecked += (_, _) => DraftChanged();
        programming.Checked += (_, _) => { supplement.IsEnabled = true; DraftChanged(); }; programming.Unchecked += (_, _) => { supplement.IsEnabled = false; DraftChanged(); };
        supplement.Checked += (_, _) => DraftChanged(); supplement.Unchecked += (_, _) => DraftChanged();
        key.PasswordChanged += (_, _) => { if (!loading) { replaceKey = true; DraftChanged(); ScheduleModels(); } };
        profileName.TextChanged += (_, _) => DraftChanged();
        profileChoice.SelectionChanged += (_, _) =>
        {
            if (loading || profileChoice.SelectedValue is not Guid id || id == onlineDraft.ActiveOnlineProfileId) return;
            Guard(() => { CaptureOnlineDraft(); onlineDraft = OnlineProfiles.Select(onlineDraft, id); LoadOnlineProfile(); feedback.Text = "已切换编辑配置；点击“保存设置”后使用此 API，其他配置一并保留。"; });
            loading = true; try { profileChoice.SelectedValue = onlineDraft.ActiveOnlineProfileId; } finally { loading = false; }
        };
        learnEnglish.Click += (_,_) => { if (learnEnglish.IsChecked == true) FeatureHelp(learnEnglish,1,"选中英语单词或词组查看本地释义，点星标加入单词收藏本。"); };
        programming.Click += (_,_) => { if (programming.IsChecked == true) FeatureHelp(programming,2,"计算机释义会在译文下方补充术语作用、用法和示例。"); };
        Loaded += (_, _) => ScheduleModels();
        Closed += (_, _) => { closed = true; CancelModels(); connectionTests.Dispose(); if (ownsTestProvider) (this.testProvider as IDisposable)?.Dispose(); (this.modelCatalog as IDisposable)?.Dispose(); key.Clear(); pendingKeys.Clear(); Theme.Apply(get().Theme); };
        feedback.Text = (IsLocal ? null : credentialNotice) ?? store.Notice ?? "";
    }

    internal event Action<int>? TipShown;
    void FeatureHelp(FrameworkElement target,int flag,string message)
    {
        if ((get().StudyTipsSeen & flag) != 0) return;
        Ui.Tip(target,message); TipShown?.Invoke(flag);
    }

    internal void ReloadFromSaved()
    {
        CancelConnectionTest(false);
        CancelModels();
        SetModels(Array.Empty<string>());
        loading = true;
        try
        {
            var settings = get();
            onlineDraft = OnlineProfiles.Upgrade(settings); pendingKeys.Clear();
            mode.SelectedIndex = settings.TranslationMode == "local" ? 0 : 1;
            localEngine.SelectedIndex = settings.LocalEngine == "nmt" ? 0 : 1;
            preset.SelectedIndex = settings.Preset == "OpenAI" ? 0 : settings.Preset == "DeepL" ? 2 : 1;
            url.Text = settings.BaseUrl; model.Text = settings.Model; target.Text = settings.TargetLanguage;
            exclusions.Text = string.Join(Environment.NewLine, settings.ExcludedProcesses);
            first.Text = settings.FirstResponseSeconds.ToString(CultureInfo.InvariantCulture);
            total.Text = settings.TotalSeconds.ToString(CultureInfo.InvariantCulture);
            theme.SelectedIndex = settings.Theme == "light" ? 1 : 0;
            foreach (var (code,choice) in languageChoices) choice.IsChecked = !settings.DisabledStudyLanguages.Contains(code);
            learnJapanese.IsChecked = settings.LearnJapanese; learnEnglish.IsChecked = settings.LearnEnglish; iconMode.IsChecked = settings.SelectionIconMode; cleanPdf.IsChecked = settings.CleanPdfText;
            if (!font.Items.Contains(settings.FontSize)) font.Items.Add(settings.FontSize);
            font.SelectedItem = settings.FontSize; role.SelectedItem = settings.InstructionRole;
            stream.IsChecked = settings.Stream; startup.IsChecked = StartupRegistration.Enabled;
            copiedPdf.IsChecked = settings.TranslateCopiedPdf;
            programming.IsChecked = settings.ExplainProgrammingTerms;
            supplement.IsChecked = settings.SupplementTechnicalTerms; supplement.IsEnabled = settings.ExplainProgrammingTerms;
            hasKey = false; credentialNotice = null;
            try { hasKey = !string.IsNullOrEmpty(credentials.Read(settings)); }
            catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException)
            { credentialNotice = "本机密钥暂时无法读取；原文件已保留，可更换密钥或导入迁移包。"; }
            replaceKey = !hasKey; key.Clear(); key.IsEnabled = replaceKey;
            keyState.Text = credentialNotice ?? (hasKey ? "密钥已加密保存 · 未编辑即保留" : "尚未保存密钥，请填写 API Key");
        }
        finally { loading = false; }
        LoadOnlineProfile();
        ApplyMode();
        RefreshDiagnostics();
        if (IsLoaded) ScheduleModels();
    }

    void CaptureOnlineDraft()
    {
        var candidate = onlineDraft with { Preset = (string)preset.SelectedItem, BaseUrl = url.Text, Model = model.Text,
            FirstResponseSeconds = int.Parse(first.Text, CultureInfo.InvariantCulture), TotalSeconds = int.Parse(total.Text, CultureInfo.InvariantCulture), InstructionRole = (string)role.SelectedItem };
        candidate = OnlineProfiles.Capture(candidate, profileName.Text);
        if (replaceKey && key.Password.Length > 0) pendingKeys[candidate.ActiveOnlineProfileId] = CredentialStore.ValidateSecret(key.Password);
        else if (replaceKey) pendingKeys.Remove(candidate.ActiveOnlineProfileId);
        onlineDraft = candidate;
    }
    void LoadOnlineProfile()
    {
        CancelConnectionTest(false); CancelModels(); SetModels(Array.Empty<string>());
        loading = true;
        try
        {
            var active = onlineDraft.OnlineProfiles.Single(p => p.Id == onlineDraft.ActiveOnlineProfileId);
            profileChoice.ItemsSource = onlineDraft.OnlineProfiles; profileChoice.SelectedValue = active.Id; profileName.Text = active.Name;
            preset.SelectedIndex = active.Preset == "OpenAI" ? 0 : active.Preset == "DeepL" ? 2 : 1; url.Text = active.BaseUrl; model.Text = active.Model;
            first.Text = active.FirstResponseSeconds.ToString(CultureInfo.InvariantCulture); total.Text = active.TotalSeconds.ToString(CultureInfo.InvariantCulture); role.SelectedItem = active.InstructionRole;
            hasKey = false; credentialNotice = null;
            try { hasKey = !string.IsNullOrEmpty(credentials.Read(active.Apply(onlineDraft))); }
            catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException)
            { credentialNotice = "此配置的本机密钥暂时无法读取；原文件已保留，可更换密钥。"; }
            bool pending = pendingKeys.TryGetValue(active.Id, out var value);
            replaceKey = pending || !hasKey; key.Password = pending ? value! : ""; key.IsEnabled = replaceKey;
            keyState.Text = pending ? "此配置的新密钥待保存" : credentialNotice ?? (hasKey ? "此配置密钥已加密保存 · 未编辑即保留" : "此配置尚未保存密钥，请填写 API Key");
        }
        finally { loading = false; }
        if (IsLoaded) ScheduleModels();
    }
    internal void AddOnlineProfile() => Guard(() =>
    {
        CaptureOnlineDraft(); onlineDraft = OnlineProfiles.Add(onlineDraft); LoadOnlineProfile();
        feedback.Text = "已新增 API 配置；填写名称、地址、密钥及模型后保存。";
    });
    internal void RemoveOnlineProfile() => Guard(() =>
    {
        // Deleting an invalid unfinished draft must still allow returning to a usable profile.
        Guid id = onlineDraft.ActiveOnlineProfileId;
        onlineDraft = OnlineProfiles.Remove(onlineDraft, id); pendingKeys.Remove(id); LoadOnlineProfile();
        feedback.Text = "此配置已从编辑列表移除；保存设置后生效，其他 API 不受影响。";
    });
    string? OnlineSecret(string endpoint)
    {
        if (replaceKey && key.Password.Length > 0) return CredentialStore.ValidateSecret(key.Password);
        // Never fall back to the application-wide active credential while editing another profile.
        return endpoint == Endpoint.Normalize(onlineDraft.BaseUrl) ? credentials.Read(onlineDraft) : null;
    }

    void ShowPage(int index)
    {
        pages.Children.Clear();
        for (int i = 0; i < navigation.Count; i++)
        {
            navigation[i].SetResourceReference(Control.BackgroundProperty, i == index ? "AccentSoft" : "Canvas");
            navigation[i].FontWeight = i == index ? FontWeights.SemiBold : FontWeights.Normal;
            navigation[i].BorderBrush = i == index && !SystemParameters.HighContrast ? PrismPanel.Neon() : System.Windows.Media.Brushes.Transparent;
            System.Windows.Automation.AutomationProperties.SetHelpText(navigation[i], i == index ? "当前设置页" : "打开此设置页");
        }
        if (cachedPages.TryGetValue(index, out var cached)) foreach (var child in cached) pages.Children.Add(child);
        else
        {
            string[] titles = { "翻译服务", "外观与行为", "数据与迁移" };
            Heading(titles[index], index == 0 ? "本地模型可在安装时选择，也可使用在线 API。" : index == 1 ? "按你的阅读习惯，调整每一次出现。" : "设置留在本机，换电脑也从容。");
            if (index == 0) ServicePage(); else if (index == 1) BehaviorPage(); else DataPage();
            cachedPages[index] = pages.Children.Cast<UIElement>().ToArray();
        }
        RefreshDiagnostics();
    }
    void Heading(string title, string subtitle)
    {
        var text = Ui.Text(title, 24, bold: true); text.Margin = new Thickness(0, 0, 0, 6); pages.Children.Add(text);
        var detail = Ui.Text(subtitle, 13, "Muted"); detail.Margin = new Thickness(0, 0, 0, 24); pages.Children.Add(detail);
    }
    void Field(string label, FrameworkElement control, string? hint = null, StackPanel? container = null)
    {
        var host = container ?? pages;
        var text = Ui.Text(label, 12, bold: true); text.Margin = new Thickness(0, 0, 0, 7); var caption = new WrapPanel(); caption.Children.Add(text); host.Children.Add(caption);
        control.Margin = new Thickness(0, 0, 0, hint is null ? 18 : 5);
        System.Windows.Automation.AutomationProperties.SetName(control, label); host.Children.Add(control);
        if (hint is not null)
        {
            var detail = Ui.Text(hint,12,"Muted"); detail.Visibility = Visibility.Collapsed; detail.TextWrapping = TextWrapping.Wrap;
            var help = Ui.Icon(label+"说明", "M12,2 A10,10 0 1 1 11.99,2 Z M12,11 L12,17 M12,7 L12,8", () => detail.Visibility = detail.IsVisible ? Visibility.Collapsed : Visibility.Visible);
            help.ToolTip = label + "说明"; System.Windows.Automation.AutomationProperties.SetName(help,label+"说明");
            help.Width = help.Height = 20; ((FrameworkElement)help.Content).Width = ((FrameworkElement)help.Content).Height = 12; help.Margin = new Thickness(6,0,0,4);
            caption.Children.Add(help); host.Children.Add(detail);
        }
    }
    void ServicePage()
    {
        Field("翻译方式", mode);
        var localInfo = new StackPanel(); localInfo.Children.Add(localTitle);
        Field("离线引擎", localEngine, container: localInfo);
        var localHint = Ui.Text("安装时可选择轻量、标准或两种模型；不下载也能安装程序。\n已下载模型无需地址、API Key、Ollama 或显卡驱动。需要更换或补充模型时重新运行安装包；已有模型会校验后复用。", 12, "Muted"); localHint.Margin = new Thickness(0, 8, 0, 0); localInfo.Children.Add(new Expander { Header = "离线模型说明", Content = localHint });
        hardwareInfo.Margin = new Thickness(0, 10, 0, 0); localInfo.Children.Add(hardwareInfo);
        localCard = CyberPrism.Card(localInfo); localCard.Margin = new Thickness(0, 0, 0, 20); pages.Children.Add(localCard);
        Field("目标语言", target, "默认简体中文，自动识别原文语言；可选择其他语言，或直接输入语言名称。"); pages.Children.Add(stream);
        pages.Children.Add(learnEnglish);
        var list = new ScrollViewer { Content = otherOptions, MaxHeight = 64, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, PanningMode = PanningMode.VerticalOnly };
        System.Windows.Automation.AutomationProperties.SetName(list,"其他语言列表");
        var multiStudy = new StackPanel(); multiStudy.Children.Add(learnJapanese); multiStudy.Children.Add(list);
        pages.Children.Add(new Expander { Header = "其他选项", Content = multiStudy, Margin = new Thickness(0,8,0,8) });
        programming.Margin = new Thickness(0, 12, 0, 5); pages.Children.Add(programming);
        System.Windows.Automation.AutomationProperties.SetName(programming, "计算机用法解释");
        supplement.Margin = new Thickness(0, 8, 0, 5); pages.Children.Add(supplement);
        System.Windows.Automation.AutomationProperties.SetName(supplement, "模型补充解释");
        pages.Children.Add(new Expander { Header = "补充说明与在线用量", Content = Ui.Text("开启后，标准本地模型或兼容的在线 AI 可为选中文字补充计算机场景和示例，例如 port 在网络中的“端口”。普通词也可能触发说明。在线服务每次翻译最多增加一次说明请求，可能计费。关闭后仍可查看本地术语说明。", 11, "Muted") });
        onlineFields.Margin = new Thickness(0, 16, 0, 0); pages.Children.Add(onlineFields);
        Field("在线 API 配置", profileChoice, "可同时保存多组 API；切换保留本窗口内的编辑，点击“保存设置”后统一生效。", onlineFields);
        var profileActions = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        profileActions.Children.Add(Ui.Button("新增 API", AddOnlineProfile));
        var removeProfile = Ui.Button("删除此配置", () =>
        {
            if (MessageBox.Show(this, "保存设置后将删除此 API 配置及其本机密钥，其他 API 不受影响。确定从编辑列表移除？", "删除 API 配置", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) RemoveOnlineProfile();
        });
        removeProfile.Margin = new Thickness(10, 0, 0, 0); profileActions.Children.Add(removeProfile); onlineFields.Children.Add(profileActions);
        Field("配置名称", profileName, "例如工作账号、备用服务；同一地址可使用多组独立密钥。", onlineFields);
        Field("服务预设", preset, container: onlineFields); Field("Base URL", url, "填写 API 根地址，例如 https://api.openai.com/v1", onlineFields);
        Field("API Key", key, container: onlineFields); key.Margin = new Thickness(0, 0, 0, 5);
        var change = Ui.Button("更换密钥", () => { replaceKey = true; key.IsEnabled = true; key.Focus(); keyState.Text = "保存时更新密钥；留空不会清除旧值"; ScheduleModels(); });
        change.Padding = new Thickness(8, 3, 8, 3); change.MinHeight = 28; change.FontSize = 11;
        change.Margin = new Thickness(10, 0, 0, 0);
        var keyLine = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(change, Dock.Right); keyLine.Children.Add(change); keyState.VerticalAlignment = VerticalAlignment.Center; keyLine.Children.Add(keyState); onlineFields.Children.Add(keyLine);
        refreshModelsButton = Ui.Button("刷新模型", async () => await RefreshModelsAsync());
        refreshModelsButton.Padding = new Thickness(10, 5, 10, 5); refreshModelsButton.Margin = new Thickness(10, 0, 0, 0);
        var modelLine = new Grid(); modelLine.ColumnDefinitions.Add(new()); modelLine.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        modelLine.Children.Add(model); Grid.SetColumn(refreshModelsButton, 1); modelLine.Children.Add(refreshModelsButton);
        Field("模型名称", modelLine, container: onlineFields); System.Windows.Automation.AutomationProperties.SetName(modelLine, "模型选择与刷新");
        System.Windows.Automation.AutomationProperties.SetName(model, "模型名称"); modelLine.Margin = new Thickness(0, 0, 0, 5);
        modelState.Margin = new Thickness(0, 0, 0, 16); onlineFields.Children.Add(modelState);
        var advanced = new StackPanel();
        AddAdvanced("首个译文等待上限（秒）", first); AddAdvanced("整个请求上限（秒）", total); AddAdvanced("固定指令角色", role);
        void AddAdvanced(string label, Control control) { advanced.Children.Add(Ui.Text(label, 12, "Muted")); System.Windows.Automation.AutomationProperties.SetName(control, label); advanced.Children.Add(control); }
        onlineFields.Children.Add(new Expander { Header = "高级网络选项", Content = advanced });
        testButton = Ui.Button("测试连接", TestConnection); testButton.HorizontalAlignment = HorizontalAlignment.Left;
        testButton.Margin = new Thickness(0, 8, 0, 12); pages.Children.Add(testButton);
        pages.Children.Add(servicePrivacy);
    }
    bool IsLocal => mode.SelectedIndex == 0;
    string TestLabel => IsLocal ? "测试本地翻译" : "测试连接";
    void ApplyMode()
    {
        onlineFields.Visibility = IsLocal ? Visibility.Collapsed : Visibility.Visible;
        if (localCard is not null) localCard.Visibility = IsLocal ? Visibility.Visible : Visibility.Collapsed;
        if (testButton is not null && !testing) testButton.Content = TestLabel;
        servicePrivacy.Text = IsLocal ? "原文仅由随包的本地模型处理，不会自动转发在线服务。无需保存设置，即可使用默认简体中文翻译。"
            : "模型检测仅读取模型列表，不发送原文。划词原文将发送到上方服务；测试仅发送固定的 Hello, world!，可能计费。";
        RefreshHardware();
    }
    void RefreshHardware()
    {
        if (IsLocal && localEngine.SelectedIndex == 0) { localTitle.Text = NmtTranslationProvider.Available ? "英译中节能引擎已安装" : "节能翻译组件未安装"; hardwareInfo.Text = "适用于英文 → 简体中文；按需运行，翻译完成后自动释放。其他方向请选择通用多语模型或在线服务。"; servicePrivacy.Text = "原文仅在本机处理，不会自动发送到在线服务。"; if (testButton is not null) testButton.IsEnabled = !testing && NmtTranslationProvider.Available; return; }
        var device = localHardware(); var support = LocalHardware.Evaluate(device);
        var installed = LocalInstallation.Evaluate(device);
        localTitle.Text = support.Permanent ? "此电脑不支持本地模型" : !LocalInstallation.HasAnyModel() ? "尚未安装本地模型 · 安装时可选下载" : installed.Supported ? "本地模型已安装" : "所选模型暂不可用";
        hardwareInfo.Text = LocalInstallation.Describe(device);
        if (IsLocal) servicePrivacy.Text = support.Supported
            ? installed.Supported ? "原文仅由已安装的本地模型处理，不会自动转发在线服务。" : installed.Error!
            : "本地模型未加载。可关闭占用内存的程序后重试，或自行选择在线 API；不会自动切换或发送原文。";
        if (testButton is not null) testButton.IsEnabled = !IsLocal || testing || support.Supported;
    }
    void BehaviorPage()
    {
        Field("视觉风格", theme); Field("译文字号", font);
        pages.Children.Add(iconMode); pages.Children.Add(cleanPdf);
        exclusions.AcceptsReturn = true; exclusions.Height = 114; exclusions.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Field("在这些应用中暂停划词", exclusions, "例如填入 Code 后，在 VS Code 里选中文字不会弹出图标或自动翻译。每行一个进程名，留空表示所有支持的应用均可划词。"); pages.Children.Add(startup);
        copiedPdf.Margin = new Thickness(0, 14, 0, 6); pages.Children.Add(copiedPdf);
        System.Windows.Automation.AutomationProperties.SetName(copiedPdf, "WPS PDF 复制后翻译");
        pages.Children.Add(new Expander { Header = "PDF 取词说明", Content = Ui.Text("WPS PDF 划选正文后自动取词，按所选响应模式翻译或显示图标。直接取词不可用时执行一次复制，并尽量恢复原剪贴板；其他应用已复制新内容时不覆盖。特殊格式或超过 64 MiB 的剪贴板保留手动 Ctrl+C 方式。暂停、固定及排除应用时不取词。扫描图片仍需先识别文字。", 11, "Muted") });
        var adapter = Ui.Button("安装 / 更新 VS Code 适配", async () =>
        {
            if (adapting) return;
            adapting = true;
            feedback.Text = "正在安装 VS Code 本机选区适配…";
            try { feedback.Text = await System.Threading.Tasks.Task.Run(YiKe.Selection.VsCodeIntegration.InstallAsync); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or TimeoutException) { feedback.Text = ex.Message; }
            finally { adapting = false; }
        });
        adapter.HorizontalAlignment = HorizontalAlignment.Left; adapter.Margin = new Thickness(0, 16, 0, 6); pages.Children.Add(adapter);
        pages.Children.Add(Ui.Text("VS Code 编辑区安装适配后可直接划词。Terminal：选中文字，按 Ctrl+Alt+Y 复制并翻译 / 解释（会更新剪贴板）。更新适配后重新打开 VS Code；无需开启读屏模式。", 11, "Muted"));
        var detail = Ui.Text("仅在确认实际非空文字选区后显示翻译；空选区保持安静。\n自动选区上限：2,000 个 Unicode 码点。\n固定浮窗后暂停接收新划词，关闭或暂停会取消请求。\n不支持自动取词时，可从托盘打开“粘贴文本翻译”。", 12, "Muted");
        detail.LineHeight = 23; detail.Margin = new Thickness(0, 20, 0, 0); pages.Children.Add(detail);
    }
    void DataPage()
    {
        var location = Ui.Text("数据目录：" + store.UserDirectory, 12, "Muted"); location.TextWrapping = TextWrapping.Wrap; pages.Children.Add(location);
        AddAction("更改数据目录", () =>
        {
            var dialog = new OpenFolderDialog { Title = "选择新的数据目录（设置和默认收藏本）" };
            if (dialog.ShowDialog(this) != true) return;
            store.ChangeDirectory(dialog.FolderName, get());
            location.Text = "数据目录：" + store.UserDirectory + "。旧文件已保留。";
        });
        pages.Children.Add(CyberPrism.Card(Ui.Text("凭据采用 Windows DPAPI CurrentUser 加密。普通导出不包含密钥；加密迁移包可在新电脑导入后重新绑定本机账户。", 12, "Muted")));
        AddAction("导出普通配置", () => { var dialog = new SaveFileDialog { Filter = "译刻配置 (*.json)|*.json", FileName = "YiKe-settings.json" }; if (dialog.ShowDialog(this) == true) { SettingsStore.Export(dialog.FileName, get()); feedback.Text = "普通配置已导出，不含 API Key。"; } });
        AddAction("导入普通配置", () => { var dialog = new OpenFileDialog { Filter = "译刻配置 (*.json)|*.json" }; if (dialog.ShowDialog(this) == true) ApplyImported(SettingsStore.Import(dialog.FileName), null); });
        AddAction("导出加密迁移包", () =>
        {
            var settings = OnlineProfiles.Capture(get());
            var keys = new Dictionary<Guid, string>();
            foreach (var profile in settings.OnlineProfiles) { string? saved = credentials.Read(profile.Apply(settings)); if (saved is not null) keys.Add(profile.Id, saved); }
            if (keys.Count == 0) throw new ArgumentException("当前未保存密钥，请使用普通配置导出。");
            string? password = AskPassword("设置迁移密码", true); if (password is null) return;
            var dialog = new SaveFileDialog { Filter = "译刻加密迁移包 (*.yike)|*.yike", FileName = "YiKe-migration.yike" };
            if (dialog.ShowDialog(this) == true) { AtomicFile.Write(dialog.FileName, MigrationService.Encrypt(settings, null, password, keys)); feedback.Text = "所有 API 配置及密钥已导出为加密迁移包，请单独保管密码。"; }
        });
        AddAction("导入加密迁移包", () =>
        {
            var dialog = new OpenFileDialog { Filter = "译刻加密迁移包 (*.yike)|*.yike" }; if (dialog.ShowDialog(this) != true) return;
            string? password = AskPassword("输入迁移密码", false); if (password is null) return;
            if (new FileInfo(dialog.FileName).Length > MigrationService.MaximumFileBytes) throw new InvalidDataException("迁移包过大。");
            var payload = MigrationService.Decrypt(File.ReadAllBytes(dialog.FileName), password); ApplyImported(payload.Settings, payload.ApiKey, payload.ProfileKeys);
        });
        AddAction("删除当前服务的本机密钥", () =>
        {
            if (MessageBox.Show(this, "删除后需重新填写当前服务的 API Key。确定删除？", "删除密钥", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            credentials.Delete(get()); credentialDeleted?.Invoke(); ReloadFromSaved(); feedback.Text = "当前服务密钥已删除。";
        });
        diagnosticsText = Ui.Text("", 11, "Muted"); diagnosticsText.Margin = new Thickness(0, 20, 0, 0); diagnosticsText.LineHeight = 20;
        pages.Children.Add(diagnosticsText); RefreshDiagnostics();
    }
    internal void ApplyImported(AppSettings settings, string? secret, Dictionary<Guid, string>? profileKeys = null)
    {
        if (profileKeys is not null)
        {
            settings = ConfigurationService.StageProfileKeys(settings, profileKeys.Where(p => p.Key != settings.ActiveOnlineProfileId).ToDictionary(p => p.Key, p => p.Value), credentials);
            if (profileKeys.TryGetValue(settings.ActiveOnlineProfileId, out var activeKey)) secret = activeKey;
        }
        commit(settings with { StudyDirectory = get().StudyDirectory }, secret);
        ReloadFromSaved();
        feedback.Text = credentialNotice ?? store.Notice ?? (secret is null ? "配置已导入，页面已更新；请填写此服务的 API Key。" : "迁移成功，密钥已加密保存，页面已更新。下次启动自动使用。");
    }
    void RefreshDiagnostics() { if (diagnosticsText is not null) diagnosticsText.Text = "数据位置\n" + store.UserDirectory + "\n\n本次诊断（不含原文、译文或密钥）\n" + diagnostics(); }
    void AddAction(string text, Action action)
    {
        var button = Ui.Button(text, () => Guard(action)); button.HorizontalAlignment = HorizontalAlignment.Left;
        button.Margin = new Thickness(0, 12, 0, 0); pages.Children.Add(button);
    }
    string? AskPassword(string title, bool confirm)
    {
        var window = new Window { Title = title, Owner = this, Width = 420, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Ui.WindowTheme(window);
        Ui.ConstrainToWorkArea(window);
        var panel = new StackPanel { Margin = new Thickness(24) }; panel.Children.Add(Ui.Text(title, 20, bold: true));
        var hint = Ui.Text("密码至少 10 个字符；不保存此密码。", 12, "Muted"); hint.Margin = new Thickness(0, 8, 0, 14); panel.Children.Add(hint);
        var password = new PasswordBox { MaxLength = 1024 }; var confirmation = new PasswordBox { MaxLength = 1024, Margin = new Thickness(0, 10, 0, 0) };
        System.Windows.Automation.AutomationProperties.SetName(password, "迁移密码"); System.Windows.Automation.AutomationProperties.SetName(confirmation, "再次输入迁移密码");
        panel.Children.Add(password); if (confirm) panel.Children.Add(confirmation);
        var message = Ui.Text("", 12, "Error"); panel.Children.Add(message);
        var ok = Ui.Button("确定", () =>
        {
            if (password.Password.Length < 10 || confirm && password.Password != confirmation.Password) { message.Text = "密码不足 10 个字符或两次输入不一致。"; return; }
            window.DialogResult = true;
        }, true);
        ok.IsDefault = true; ok.Margin = new Thickness(0, 16, 0, 0); panel.Children.Add(ok); window.Content = panel;
        window.PreviewKeyDown += (_, args) => { if (args.Key == Key.Escape) { window.DialogResult = false; args.Handled = true; } };
        bool accepted = window.ShowDialog() == true; string? result = accepted ? password.Password : null; password.Clear(); confirmation.Clear(); return result;
    }
    static void SetEditorLimit(ComboBox combo, int maximum)
    {
        if (combo.Template.FindName("PART_EditableTextBox", combo) is TextBox editor) editor.MaxLength = maximum;
    }
    void SetModels(IReadOnlyList<string> values)
    {
        string current = model.Text;
        var editor = model.Template?.FindName("PART_EditableTextBox", model) as TextBox;
        int caret = editor?.SelectionStart ?? 0, selection = editor?.SelectionLength ?? 0;
        updatingModels = true;
        try
        {
            model.ItemsSource = values; model.SelectedIndex = -1; model.Text = current;
            editor?.Select(Math.Min(caret, current.Length), Math.Min(selection, Math.Max(0, current.Length - caret)));
        }
        finally { updatingModels = false; }
    }
    void CancelModels()
    {
        modelDelay.Stop(); modelVersion++;
        modelCancellation?.Cancel(); modelCancellation = null;
        if (refreshModelsButton is not null) refreshModelsButton.IsEnabled = true;
    }
    void ScheduleModels()
    {
        if (closed || loading) return;
        CancelModels(); SetModels(Array.Empty<string>());
        if (IsLocal) return;
        if ((string?)preset.SelectedItem == "DeepL") { modelState.Text = "DeepL 使用专用文本翻译接口，不获取模型列表。Free 地址 api-free.deepl.com/v2；Pro 地址 api.deepl.com/v2。"; return; }
        modelState.Text = "填写地址和 API Key 后自动获取模型；也可手动填写。";
        modelDelay.Start();
    }
    internal async Task RefreshModelsAsync()
    {
        if (closed || IsLocal || (string?)preset.SelectedItem == "DeepL") return;
        CancelModels();
        long version = modelVersion;
        try
        {
            string endpoint = Endpoint.Normalize(url.Text);
            // A saved credential is only usable at the endpoint to which it was bound.
            string? secret = OnlineSecret(endpoint);
            if (string.IsNullOrWhiteSpace(secret)) { modelState.Text = "请填写该地址的 API Key，即可自动获取模型；也可手动填写模型名称。"; return; }
            using var cancellation = new CancellationTokenSource(); modelCancellation = cancellation;
            if (refreshModelsButton is not null) refreshModelsButton.IsEnabled = false;
            modelState.Text = "正在获取可用模型…";
            var values = await modelCatalog.ListAsync(endpoint, secret, cancellation.Token);
            if (closed || version != modelVersion) return;
            SetModels(values);
            modelState.Text = values.Count == 0 ? "服务未返回可用模型；可手动填写模型名称。"
                : $"已获取 {values.Count} 个模型，请在上方选择；保留当前填写的模型。列表不保证支持文本翻译，可用“测试连接”确认。";
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) when (closed || version != modelVersion) { }
        catch (TranslationException error) { if (!closed && version == modelVersion) modelState.Text = error.Message; }
        catch (Exception error) when (error is ArgumentException or CryptographicException or IOException or UnauthorizedAccessException or SecurityException)
        { if (!closed && version == modelVersion) modelState.Text = "请检查 HTTPS API 根地址和密钥；仍可手动填写模型名称。"; }
        finally
        {
            if (version == modelVersion)
            {
                modelCancellation = null;
                if (refreshModelsButton is not null) refreshModelsButton.IsEnabled = true;
            }
        }
    }
    internal string ModelStatusText => modelState.Text;
    string ThemeChoice() => theme.SelectedIndex == 1 ? "light" : "dark";
    AppSettings Draft() => (onlineDraft with
    {
        StudyDirectory = get().StudyDirectory, Enabled = get().Enabled, GuideSeen = get().GuideSeen, StudyTipsSeen = get().StudyTipsSeen,
        TranslationMode = IsLocal ? "local" : "online",
        LocalEngine = localEngine.SelectedIndex == 0 ? "nmt" : "generic",
        Preset = IsLocal ? onlineDraft.Preset : (string)preset.SelectedItem, BaseUrl = IsLocal ? onlineDraft.BaseUrl : url.Text, Model = IsLocal ? onlineDraft.Model : model.Text, TargetLanguage = target.Text,
        Stream = stream.IsChecked == true, Theme = ThemeChoice(), FontSize = (double)(font.SelectedItem ?? 15d),
        TranslateCopiedPdf = copiedPdf.IsChecked == true,
        DisabledStudyLanguages = languageChoices.Count == 0 ? get().DisabledStudyLanguages : languageChoices.Where(p => p.Value.IsChecked != true).Select(p => p.Key).ToArray(),
        LearnJapanese = learnJapanese.IsChecked == true, LearnEnglish = learnEnglish.IsChecked == true, SelectionIconMode = iconMode.IsChecked == true, CleanPdfText = cleanPdf.IsChecked == true,
        ExplainProgrammingTerms = programming.IsChecked == true,
        SupplementTechnicalTerms = supplement.IsChecked == true,
        ExcludedProcesses = exclusions.Text.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries),
        FirstResponseSeconds = IsLocal ? onlineDraft.FirstResponseSeconds : int.Parse(first.Text, CultureInfo.InvariantCulture), TotalSeconds = IsLocal ? onlineDraft.TotalSeconds : int.Parse(total.Text, CultureInfo.InvariantCulture),
        InstructionRole = (string)role.SelectedItem
    }).Validate(true);
    void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or CryptographicException or FormatException or OverflowException or SecurityException)
        { feedback.Text = ex is FormatException or OverflowException ? "请检查超时秒数等数字设置。" : ex.Message; }
    }
    internal void SaveSettings() => Guard(() =>
    {
        var draft = Draft(); bool startWithWindows = startup.IsChecked == true;
        if (IsLocal && localEngine.SelectedIndex != 0 && LocalHardware.Evaluate(localHardware()) is { Permanent: true } unsupported) throw new ArgumentException(unsupported.Error);
        string? newKey = !IsLocal && replaceKey && key.Password.Length > 0 ? CredentialStore.ValidateSecret(key.Password) : null;
        if (!IsLocal) { CaptureOnlineDraft(); draft = OnlineProfiles.Capture(draft, profileName.Text); }
        var otherKeys = pendingKeys.Where(p => IsLocal || p.Key != draft.ActiveOnlineProfileId).ToDictionary(p => p.Key, p => p.Value);
        draft = ConfigurationService.StageProfileKeys(draft, otherKeys, credentials);
        var before = OnlineProfiles.Upgrade(get());
        commit(draft, newKey);
        bool cleanupFailed = false;
        foreach (var removed in before.OnlineProfiles.Where(p => !draft.OnlineProfiles.Any(current => current.Id == p.Id)))
        {
            try { credentials.Delete(removed.Apply(before)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { cleanupFailed = true; }
        }
        ReloadFromSaved();
        try { if (startWithWindows != StartupRegistration.Enabled) StartupRegistration.Set(startWithWindows); startup.IsChecked = StartupRegistration.Enabled; }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        { feedback.Text = "翻译设置已保存，但开机启动设置失败；请检查当前用户的注册表权限。"; return; }
        feedback.Text = IsLocal ? store.Notice ?? "已保存，使用本地离线翻译；无需 API Key。" : credentialNotice ?? store.Notice ?? (hasKey ? "已保存，下次启动自动使用。" : "设置已保存；请填写 API Key 后开始翻译。");
        if (cleanupFailed) feedback.Text += " 部分已删除配置的旧密钥文件未能清理；配置删除已生效。";
    });
    void DraftChanged()
    {
        if (loading || closed) return;
        if (testing) { CancelConnectionTest(false); feedback.Text = "服务配置已更改，连接测试已取消；可重新测试。"; }
        else connectionTests.Invalidate();
    }
    void CancelConnectionTest(bool showFeedback)
    {
        connectionTests?.Invalidate(); testing = false;
        if (testButton is not null) testButton.Content = TestLabel;
        if (showFeedback) feedback.Text = IsLocal ? "本地测试已取消。" : "测试已取消；取消不保证服务商不计费。";
    }
    internal void TestConnection()
    {
        if (testing) { CancelConnectionTest(true); return; }
        Guard(() =>
        {
            var draft = Draft();
            if (IsLocal && draft.LocalEngine != "nmt" && LocalHardware.Evaluate(localHardware()) is { Supported: false } unsupported) throw new ArgumentException(unsupported.Error);
            string? secret = IsLocal ? "" : OnlineSecret(draft.BaseUrl);
            if (!IsLocal && string.IsNullOrWhiteSpace(secret)) throw new ArgumentException("请先填写该服务的 API Key。");
            testing = true; if (testButton is not null) testButton.Content = "取消测试";
            feedback.Text = IsLocal ? "正在加载本地模型并翻译固定样例…" : "正在测试连接…（固定样例）";
            connectionTests.Submit("Hello, world!", draft with { ExplainProgrammingTerms = false, SupplementTechnicalTerms = false }, secret ?? "");
        });
    }
    void ReceiveTestResult(TranslationUpdate update)
    {
        if (closed || update.Sequence != connectionTests.Sequence) return;
        if (update.Error is null && !update.Complete) return;
        testing = false; if (testButton is not null) testButton.Content = TestLabel;
        feedback.Text = update.Error ?? (IsLocal ? "本地翻译成功，已就绪；无需配置或密钥。" : "连接成功，固定样例已返回；配置尚未因此保存。");
    }
    internal string StatusText => feedback.Text;
}

public static class StartupRegistration
{
    const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled
    {
        get { try { using var key = Registry.CurrentUser.OpenSubKey(Run); return key?.GetValue("YiKe") is string; } catch (SecurityException) { return false; } }
    }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Run);
        if (enabled) key.SetValue("YiKe", "\"" + Environment.ProcessPath + "\""); else key.DeleteValue("YiKe", false);
    }
}
