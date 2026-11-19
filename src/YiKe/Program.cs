using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using YiKe.Core;
using YiKe.Data;
using YiKe.Selection;
using YiKe.Translation;
using YiKe.UI;
using Forms = System.Windows.Forms;

namespace YiKe;

public static class Program
{
    static async Task<int> VerifyNmt() {
        try { using var provider=new NmtTranslationProvider(); var s=new AppSettings(); var r=new TranslationRequest(1,"Do not close this window until the download has finished.","auto",TranslationLanguages.Default,s.ConfigId,s.ServiceId,""); var result=new System.Text.StringBuilder(); await foreach(var part in provider.TranslateAsync(r,s,"",default)) result.Append(part); return result.ToString().Contains("之前")&&result.ToString().Contains("不要")?0:1; } catch { return 1; }
    }
    public const string MutexName = @"Local\YiKe.3C0A80D1.App";
    const string StopEvent = @"Local\YiKe.3C0A80D1.Stop", SettingsEvent = @"Local\YiKe.3C0A80D1.Settings";
    [STAThread] public static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--verify-nmt") return VerifyNmt().GetAwaiter().GetResult();
        if (args.Length == 1 && args[0] == "--verify-dictionary")
        {
            try { return BundledEnglishDictionary.Count >= 700000 && CommonWordTranslations.Find("unmark", "简体中文") == "取消标记" && CommonWordTranslations.Find("demonstrates", "简体中文") is not null ? 0 : 1; }
            catch { return 1; }
        }
        if (args.Length == 2 && args[0] == "--installer-info") { try { LocalInstallation.WriteInstallerInfo(args[1], LocalHardware.Detect()); return 0; } catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { return 1; } }
        if (args.Length == 2 && args[0] == "--export-vscode") { VsCodeIntegration.ExportPackage(args[1]); return 0; }
        if (args.Length == 1 && args[0] == "--install-vscode") { try { VsCodeIntegration.InstallAsync().GetAwaiter().GetResult(); return 0; } catch { return 1; } }
        if (args.Length > 0 && args[0] == "--uia-worker")
        {
            if (args.Length != 3 || !int.TryParse(args[1], out int parentId) || !long.TryParse(args[2], out long parentStart) || parentId <= 0 || parentStart <= 0) return 2;
            var t = new Thread(() => UiaWorker.Run(parentId, parentStart)); t.SetApartmentState(ApartmentState.MTA); t.Start(); t.Join(); return 0;
        }
        if (args.Contains("--shutdown")) { try { using var e = EventWaitHandle.OpenExisting(StopEvent); e.Set(); } catch (WaitHandleCannotBeOpenedException) { } return 0; }
        using var mutex = new Mutex(true, MutexName, out bool owned);
        if (!owned) { try { using var e = EventWaitHandle.OpenExisting(SettingsEvent); e.Set(); } catch (WaitHandleCannotBeOpenedException) { } return 0; }
        // Small text windows favor predictable rendering on remote desktops and diverse display drivers.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var app = new YiKeApplication();
        using var stop = new EventWaitHandle(false, EventResetMode.AutoReset, StopEvent);
        using var settings = new EventWaitHandle(false, EventResetMode.AutoReset, SettingsEvent);
        var stopWait = ThreadPool.RegisterWaitForSingleObject(stop, (_, _) => app.Dispatcher.BeginInvoke(app.Shutdown), null, -1, false);
        var settingsWait = ThreadPool.RegisterWaitForSingleObject(settings, (_, _) => app.Dispatcher.BeginInvoke(app.OpenSettings), null, -1, false);
        try { return app.Run(); } finally { stopWait.Unregister(null); settingsWait.Unregister(null); mutex.ReleaseMutex(); }
    }
}

public sealed class YiKeApplication : Application
{
    readonly SettingsStore store = new();
    readonly SelectionReader reader = new();
    readonly TranslationProvider provider = new();
    readonly CredentialStore credentials;
    AppSettings config;
    ManualWindow? manual;
    bool AcceptSelection => manual is null && session.CanReceiveSelection;
    PopupWindow? popup;
    SelectionIcon? selectionIcon;
    AppSettings? selectionOptions;
    SettingsWindow? settings;
    Forms.NotifyIcon? tray;
    Forms.ToolStripMenuItem? enabledItem;
    SelectionMonitor? monitor;
    VsCodeTerminalHotkey? terminalHotkey;
    PdfCopyFallback? pdfCopy;
    TranslationCoordinator? coordinator;
    CancellationTokenSource? selecting;
    DispatcherTimer? foregroundTimer;
    readonly SessionState session;
    string diagnostic = "尚未取词。";
    public YiKeApplication()
    {
        config = store.Load();
        if (config.EnhancementDefaultsVersion == 0) config = config with { EnhancementDefaultsVersion = 1, LocalEngine = NmtTranslationProvider.Available && config.TargetLanguage == TranslationLanguages.Default ? "nmt" : config.LocalEngine, ExplainProgrammingTerms = false, SupplementTechnicalTerms = false, LearnEnglish = false };
        session = new(config.Enabled); credentials = new(store.DefaultDirectory); ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Startup += Start; Exit += Cleanup;
        DispatcherUnhandledException += (_, e) => { diagnostic = "界面操作失败：" + e.Exception.GetType().Name; e.Handled = true; if (settings is not null) MessageBox.Show(settings, "操作失败，现有配置已保留。请检查文件权限或重新启动译刻。", "译刻"); };
    }
    void Start(object sender, StartupEventArgs e)
    {
        Theme.Apply(config.Theme); Theme.InstallStyles();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
        selectionIcon = new(); selectionIcon.Requested += snapshot => { if (AcceptSelection && !IsExcluded(snapshot.ProcessId) && Native.GetForegroundWindow().ToInt64() == snapshot.Window) Translate(snapshot); };
        popup = new(); popup.FavoriteRequested += word => StudyBook.Add(StudyFile, word); popup.OptionsChanged += options => { selectionOptions = options; if (session.Current is { } current) Translate(current); }; popup.CancelRequested += () => coordinator!.Invalidate(); popup.Dismissed += Cancel; popup.Retry += () => { if (session.Current is not null) Translate(session.Current, true); }; popup.Settings += OpenSettings;
        popup.PinChanged += pinned => { pdfCopy?.Clear(); session.SetPinned(pinned); selecting?.Cancel(); };
        coordinator = new(provider, u => Dispatcher.BeginInvoke(() => { if (manual is not null && u.Sequence == coordinator!.Sequence) manual.Update(u); else if (session.AcceptsTranslation(u.Sequence, coordinator!.Sequence) && popup.IsVisible) popup.Update(u); }));
        var menu = new Forms.ContextMenuStrip(); enabledItem = new Forms.ToolStripMenuItem(config.Enabled ? "暂停自动翻译" : "恢复自动翻译"); enabledItem.Click += (_, _) => Toggle(); menu.Items.Add(enabledItem);
        menu.Items.Add("设置", null, (_, _) => OpenSettings()); menu.Items.Add("手动翻译", null, (_, _) => Paste()); menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("退出译刻", null, (_, _) => Shutdown());
        using var iconStream = typeof(Program).Assembly.GetManifestResourceStream("YiKe.icon")!;
        tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(iconStream), Text = "译刻 · 划词即译", Visible = true, ContextMenuStrip = menu }; tray.DoubleClick += (_, _) => OpenSettings();
        try { monitor = new(Dispatcher); monitor.MouseDown += OnMouseDown; monitor.Selected += OnGesture; monitor.Unselected += OnUnselected; }
        catch (Exception ex) { diagnostic = "鼠标监听未启动：" + ex.GetType().Name; tray.ShowBalloonTip(6000, "译刻", "无法启动自动划词，可从托盘粘贴文本翻译。", Forms.ToolTipIcon.Warning); }
        terminalHotkey = new(); terminalHotkey.Requested += OnTerminal;
        pdfCopy = new(() => config.TranslateCopiedPdf && AcceptSelection, IsExcluded); pdfCopy.Copied += OnPdfCopied;
        foregroundTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        foregroundTimer.Tick += (_, _) => { terminalHotkey.Update(AcceptSelection); pdfCopy.Update(); if (selectionIcon.Selection is { } iconSelection && (DateTimeOffset.UtcNow - iconSelection.Time > TimeSpan.FromSeconds(15) || Native.GetForegroundWindow().ToInt64() != iconSelection.Window && Native.ProcessId(Native.GetForegroundWindow()) != Environment.ProcessId)) selectionIcon.Clear(); var foreground = Native.GetForegroundWindow(); if (session.ShouldDismissForForeground(foreground.ToInt64(), Native.ProcessId(foreground) == Environment.ProcessId)) popup.Dismiss(); }; foregroundTimer.Start();
        var localSupport = LocalInstallation.Evaluate(LocalHardware.Detect());
        if (config.TranslationMode == "local" && !LocalInstallation.HasAnyModel() && !NmtTranslationProvider.Available)
        {
            diagnostic = localSupport.Error!;
            tray.ShowBalloonTip(8000, "本地翻译暂不可用", localSupport.Error!, Forms.ToolTipIcon.Warning);
            if (localSupport.Permanent || !LocalInstallation.HasAnyModel()) OpenSettings();
        }
        else if (!store.Found) tray.ShowBalloonTip(6000, "译刻已准备好", "已按电脑配置选择本地模型。选中文字即可使用，无需配置；首次加载模型需要稍候。", Forms.ToolTipIcon.Info);
        else if (store.Notice is not null) tray.ShowBalloonTip(6000, "译刻", store.Notice, Forms.ToolTipIcon.Warning);
        // Models load on the first actual sentence request, never on startup.
        OpenSettings();
        _ = PrepareReader();
    }
    async Task PrepareReader()
    {
        try { await reader.PrepareAsync(CancellationToken.None); }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { /* The next genuine gesture can start a fresh owned helper. No text is read for warm-up. */ }
    }
    async Task PrepareLocal()
    {
        try { await provider.PrepareLocalAsync(CancellationToken.None); }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is TranslationException or IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { if (!Dispatcher.HasShutdownStarted) _ = Dispatcher.BeginInvoke(() => diagnostic = ex is TranslationException error ? error.Message : "本地模型尚未就绪；翻译时可重新加载。"); }
    }
    void OnMouseDown(int processId)
    {
        if (manual is not null || processId == Environment.ProcessId || session.Pinned) return;
        selectionIcon?.Clear(); selectionOptions = null;
        pdfCopy?.Clear();
        selecting?.Cancel(); session.Close(); popup!.HideForSelection();
    }
    async void OnUnselected(int processId, bool doubleClickGrace)
    {
        if (manual is not null || processId == Environment.ProcessId || session.Pinned) return;
        long epoch = session.SelectionEpoch;
        if (doubleClickGrace) await Task.Delay((int)Math.Min(1000, Native.GetDoubleClickTime()));
        if (session.SelectionEpoch == epoch && session.DisplayedTranslation == 0 && !session.Pinned) coordinator!.Invalidate();
    }
    void SystemPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(() => { if (!Dispatcher.HasShutdownStarted) Theme.Apply(config.Theme); });
    }
    async void OnGesture(Gesture g)
    {
        if (!AcceptSelection || g.ProcessId == Environment.ProcessId) return;
        string sourceName;
        try
        {
            using var process = Process.GetProcessById(g.ProcessId);
            sourceName = process.ProcessName;
            if (config.ExcludedProcesses.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase)) { coordinator!.Invalidate(); diagnostic = "已跳过排除应用。"; return; }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { coordinator!.Invalidate(); diagnostic = "来源应用已关闭或不可访问。"; return; }
        selecting?.Cancel(); selecting?.Dispose(); selecting = new(); var ct = selecting.Token; long epoch = session.BeginSelection();
        pdfCopy?.Arm(g);
        try
        {
            await Task.Delay(80, ct); if (!session.Accepts(epoch)) return;
            if (Native.GetForegroundWindow().ToInt64() != g.Window) { coordinator!.Invalidate(); return; }
            var sw = Stopwatch.StartNew(); var result = await reader.ReadSettledAsync(g, ct, pdfCopy?.IsArmed == true);
            if (ct.IsCancellationRequested || !session.Accepts(epoch)) return;
            if (Native.GetForegroundWindow().ToInt64() != g.Window || Native.ProcessId(new IntPtr(g.Window)) != g.ProcessId) { coordinator!.Invalidate(); popup!.HideForSelection(); return; }
            diagnostic = $"来源进程：{sourceName} · 取词状态：{result.Status} · 耗时 {sw.ElapsedMilliseconds} ms · {DateTime.Now:HH:mm:ss}";
            pdfCopy?.CompleteRead(g, result.Status);
            var action = AutomaticSelectionPolicy.Decide(result);
            if (action == AutomaticSelectionAction.Translate) PresentSelection(new(g.Sequence, g.Window, g.ProcessId, (config.CleanPdfText && PdfCopyFallback.IsPdfWindow(g) ? PdfText.Clean(result.Text!) : result.Text!), g.X, g.Y, g.Time, result.Text));
            else
            {
                coordinator!.Invalidate();
                if (action == AutomaticSelectionAction.TooLong) { session.SetCurrent(new(g.Sequence, g.Window, g.ProcessId, "", g.X, g.Y, g.Time)); popup!.Begin("", config, g.X, g.Y, g.Window); popup.Error("选区超过 2,000 个 Unicode 码点，请缩短选区；未发送请求。"); }
                else popup!.HideForSelection();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { diagnostic = "取词失败：" + ex.GetType().Name; }
    }
    void PresentSelection(SelectionSnapshot snapshot)
    {

        if (config.SelectionIconMode) { coordinator!.Invalidate(); session.SetCurrent(snapshot); selectionIcon!.Present(snapshot); }
        else Translate(snapshot);
    }
    void Translate(SelectionSnapshot snapshot, bool force = false)
    {
        session.ShowTranslation(0);
        var activeOptions = selectionOptions ?? config;
        session.SetCurrent(snapshot); popup!.Begin(snapshot.Text, activeOptions, snapshot.X, snapshot.Y, snapshot.Window, snapshot.OriginalText);
        Submit(snapshot.Text, activeOptions, force, session.ShowTranslation, popup.Error);
    }
    void Submit(string text, AppSettings options, bool force, Action<long> started, Action<string> failed, string sourceLanguage = "auto")
    {
        try
        {
            options.Validate(true);
            var key = options.TranslationMode == "local" ? "" : credentials.Read(options);
            if (options.TranslationMode == "online" && string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("请在设置中填写当前服务的密钥。");
            started(coordinator!.Submit(text, options, key ?? "", force, sourceLanguage));
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException or IOException or UnauthorizedAccessException)
        {
            coordinator!.Invalidate();
            failed(ex is CryptographicException ? "本机密钥无法解密，请在设置中重新填写密钥。" : ex is ArgumentException ? ex.Message : "无法读取服务配置，请检查配置目录的访问权限。");
        }
    }
    bool IsExcluded(int processId)
    {
        try { using var process = Process.GetProcessById(processId); return config.ExcludedProcesses.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return true; }
    }
    void OnPdfCopied(Gesture gesture, SelectionReadResult result)
    {
        if (!AcceptSelection || !config.TranslateCopiedPdf || IsExcluded(gesture.ProcessId)) return;
        selecting?.Cancel(); session.BeginSelection();
        diagnostic = "来源：WPS PDF · 已读取本次新复制的文字 · " + DateTime.Now.ToString("HH:mm:ss");
        if (result.Status == ReadStatus.Success && TextLimit.Allowed(result.Text ?? "")) PresentSelection(new(gesture.Sequence, gesture.Window, gesture.ProcessId, (config.CleanPdfText ? PdfText.Clean(result.Text!) : result.Text!), gesture.X, gesture.Y, DateTimeOffset.UtcNow, result.Text));
        else if (result.Status == ReadStatus.TooLong)
        {
            coordinator!.Invalidate(); session.SetCurrent(new(gesture.Sequence, gesture.Window, gesture.ProcessId, "", gesture.X, gesture.Y, gesture.Time));
            popup!.Begin("", config, gesture.X, gesture.Y, gesture.Window); popup.Error("本次复制的 PDF 文字超过 2,000 个 Unicode 码点，请缩小选区；未发送请求。");
        }
    }
    async void OnTerminal(Gesture gesture)
    {
        if (!AcceptSelection) return;
        try { using var source = Process.GetProcessById(gesture.ProcessId); if (config.ExcludedProcesses.Contains(source.ProcessName, StringComparer.OrdinalIgnoreCase)) return; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return; }
        selecting?.Cancel(); selecting?.Dispose(); selecting = new(); var ct = selecting.Token;
        long epoch = session.BeginSelection(); coordinator!.Invalidate();
        try
        {
            var result = await VsCodeTerminal.ReadAsync(gesture, ct);
            if (ct.IsCancellationRequested || !session.Accepts(epoch) || Native.GetForegroundWindow().ToInt64() != gesture.Window) return;
            diagnostic = $"VS Code 终端 · 显式复制选区 · {result.Status}";
            if (result.Status == ReadStatus.Success && result.Text is not null) Translate(new(gesture.Sequence, gesture.Window, gesture.ProcessId, result.Text, gesture.X, gesture.Y, gesture.Time));
            else
            {
                session.SetCurrent(new(gesture.Sequence, gesture.Window, gesture.ProcessId, "", gesture.X, gesture.Y, gesture.Time));
                popup!.Begin("", config, gesture.X, gesture.Y, gesture.Window);
                popup.SelectionError(result.Status == ReadStatus.TooLong ? "终端选区超过 2,000 个 Unicode 码点，请缩小选区。" : result.Status == ReadStatus.Unsupported ? "请在设置安装 / 更新 VS Code 适配并重新打开 VS Code，然后在终端选中文字，按 Ctrl+Alt+Y 复制并翻译。" : "未取得新的终端选区。请在终端拖选文字后按 Ctrl+Alt+Y；没有读取旧剪贴板内容。");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { diagnostic = "终端取词失败：" + ex.GetType().Name; }
    }
    void Cancel() { selectionIcon?.Clear(); selectionOptions = null; popup?.StopSpeech(); pdfCopy?.Clear(); session.Close(); selecting?.Cancel(); if (manual is null) coordinator?.Invalidate(); }
    void Toggle()
    {
        bool enabled = !session.Enabled;
        session.SetEnabled(enabled);
        config = config with { Enabled = enabled };
        enabledItem!.Text = enabled ? "暂停自动翻译" : "恢复自动翻译";
        if (!enabled) popup!.Dismiss(); // Stop now even if the disk is full or the settings file is read-only.
        try { config = store.Save(config); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { tray!.ShowBalloonTip(5000, "译刻", enabled ? "本次已恢复，但启用状态未能保存。下次启动将使用原来的状态。" : "本次已暂停并取消请求，但暂停状态未能保存。下次启动将使用原来的状态。", Forms.ToolTipIcon.Warning); }
    }
    void Commit(AppSettings value, string? key)
    {
        // Write a new immutable credential binding before committing the pointer in settings.
        // If the config write fails, the previous credential and configuration remain valid.
        config = ConfigurationService.Commit(config, value, key, store, credentials); manual?.Close(); session.SetEnabled(config.Enabled); Theme.Apply(config.Theme); popup!.Dismiss(); enabledItem!.Text = config.Enabled ? "暂停自动翻译" : "恢复自动翻译";
    }
    string StudyFile => Path.Combine(string.IsNullOrEmpty(config.StudyDirectory) ? store.UserDirectory : config.StudyDirectory, "YiKe-vocabulary.json");
    public void OpenSettings()
    {
        if (settings is not null) { settings.Activate(); return; }
        popup?.Dismiss(); settings = new(() => config, Commit, store, credentials, () => diagnostic, () => popup!.Dismiss(), testProvider: provider); settings.ManualRequested += Paste;
        settings.StudyRequested += () => new StudyBook(() => StudyFile, folder => config = store.Save(config with { StudyDirectory = folder })) { Owner = settings }.ShowDialog();
        settings.GuideDismissed += () =>
        {
            try { config = store.Save(config with { GuideSeen = true }); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { MessageBox.Show(settings, "本次已关闭教程。设置无法保存，下次启动时将再次显示。", "译刻"); }
        };
        settings.Closed += (_, _) => settings = null; settings.Show();
    }
    void Paste()
    {
        if (manual is not null) { manual.Activate(); return; }
        popup!.Dismiss();
        var window = new ManualWindow(config);
        manual = window;
        window.Requested += text => { var options = config with { TargetLanguage = window.To }; window.Begin(options); Submit(text, options, false, _ => { }, window.Error, window.From); };
        window.CancelRequested += () => coordinator!.Invalidate();
        window.Closed += (_, _) => { coordinator!.Invalidate(); manual = null; };
        window.Show();
    }
    void Cleanup(object sender, ExitEventArgs e) { Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged; Cancel(); pdfCopy?.Dispose(); foregroundTimer?.Stop(); terminalHotkey?.Dispose(); monitor?.Dispose(); coordinator?.Dispose(); reader.Dispose(); provider.Dispose(); selectionIcon?.Close(); popup?.DisposeSpeech(); selecting?.Dispose(); if (tray is not null) { tray.Visible = false; tray.Dispose(); } }
}
