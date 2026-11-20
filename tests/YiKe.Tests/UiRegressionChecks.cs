using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using YiKe.Core;
using YiKe.Data;
using YiKe.Selection;
using YiKe.Translation;
using YiKe.UI;

public static class UiRegressionChecks
{
    public static int Run(string? filter = null)
    {
        int failed = 0, passed = 0, skipped = 0;
        string root = Path.Combine(Environment.CurrentDirectory, "artifacts", "ui-regression-" + Guid.NewGuid().ToString("N"));
        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            Theme.Apply("light"); Theme.InstallStyles();
            app.Startup += async (_, _) =>
            {
                async Task Check(string name, Func<Task> action)
                {
                    if (filter is not null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) return;
                    try { await action(); Console.WriteLine("PASS " + name); passed++; }
                    catch (DesktopUnavailableException error) { Console.WriteLine("SKIP " + name + " / " + error.Message); skipped++; }
                    catch (Exception error) { Console.WriteLine("FAIL " + name + " / " + error); failed++; }
                    foreach (Window window in app.Windows.Cast<Window>().ToArray()) window.Close();
                }
                try
                {
                    await Check("Settings shows manual entry, skippable guide and live light theme", async () =>
                    {
                        using var fixture = new EditorFixture(root, "theme-guide");
                        var window = fixture.Window; window.Show(); await Idle();
                        bool manual = false, dismissed = false;
                        window.ManualRequested += () => manual = true; window.GuideDismissed += () => dismissed = true;
                        FindButton(window, "手动翻译").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        var done = FindButton(window, "跳过 / 完成教程");
                        done.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Assert(manual && dismissed && !done.IsVisible);
                        FindButton(window, "使用指南").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(done.IsVisible);
                        FindButton(window, "外观与行为").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        var picker = Find<ComboBox>(window, "视觉风格"); picker.SelectedIndex = 1;
                        Assert(!Theme.Dark); window.SaveSettings(); Assert(fixture.Config.Theme == "light");
                    });
                    await Check("Manual English study looks up and collects a selected word", async () =>
                    {
                        var window = new ManualWindow(new AppSettings { LearnEnglish = true }); window.Show(); await Idle();
                        var input = Find<TextBox>(window,"原文"); input.Text = "read the book"; input.Select(0,4);
                        Assert(Walk(window).OfType<TextBlock>().Any(t => t.Text.Contains("英文释义")));
                        string saved = ""; window.FavoriteRequested += w => saved = w;
                        FindButton(window,"收藏单词").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(saved == "read");
                        input.Text = "take off"; await Idle();
                        Assert(BundledEnglishDictionary.Details("take off") is { Length: > 15 });
                        var savePhrase = FindButton(window,"收藏单词"); Assert(savePhrase.IsEnabled);
                        savePhrase.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(saved == "take off");
                        var output = Find<TextBox>(window,"译文"); output.Text = "They take off today."; output.Select(5,8);
                        savePhrase.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(saved == "take off");
                        window.Close();
                        using var fixture = new EditorFixture(root,"caption-position"); fixture.Window.Show(); await Idle();
                        var close = FindButton(fixture.Window,"关闭");
                        Assert(close.TranslatePoint(new Point(),fixture.Window).X > fixture.Window.ActualWidth * .8);
                    });
                    await Check("Alternative dictionary meanings keep the contextual result and reset on next request", async () =>
                    {
                        var settings = new AppSettings { LearnEnglish = false };
                        var window = new ManualWindow(settings); window.Show(); await Idle();
                        var source = Find<TextBox>(window,"原文"); source.Text = "They sat on the bank of the river.";
                        window.Begin(settings,"bank"); window.Update(new(1,"河岸",true)); await Idle();
                        var other = Find<Expander>(window,"其他词义"); Assert(other.IsVisible && !other.IsExpanded);
                        other.IsExpanded = true; await Idle();
                        Assert(Walk(other).OfType<TextBlock>().Any(t => t.Text.Contains("银行") && t.Text.Contains("岸")));
                        Assert(Find<TextBox>(window,"译文").Text == "河岸");
                        window.Begin(settings,"zzzzxunknown"); window.Update(new(2,"测试",true));
                        Assert(!other.IsVisible && !other.IsExpanded); window.Close();
                        var popup = new PopupWindow(); popup.Begin("take off",settings,300,250,0); popup.Update(new(1,"起飞",true)); await Idle();
                        Assert(Find<Expander>(popup,"其他词义").IsVisible); popup.Dismiss();
                    });
                    await Check("Scrollable language choices persist independently", async () =>
                    {
                        using var fixture = new EditorFixture(root,"scrollable-study");
                        var window = fixture.Window;
                        foreach (var (code,name) in StudyDictionaryPack.Languages) window.ShowPackInstaller(new StudyDictionaryPack(root,code,name));
                        window.Show(); await Idle();
                        FindButton(window,"跳过 / 完成教程").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Walk(window).OfType<Expander>().Single(e => Equals(e.Header,"其他选项")).IsExpanded = true; await Idle();
                        var list = Find<ScrollViewer>(window,"其他语言列表"); Assert(list.ScrollableHeight > 0);
                        list.ScrollToBottom(); await Idle(); Assert(list.VerticalOffset > 0);
                        foreach (var (code,name) in StudyDictionaryPack.Languages)
                            Find<CheckBox>(window,"开启"+name+"研习").IsChecked = code is "ja" or "fr";
                        window.SaveSettings();
                        var all = StudyDictionaryPack.Languages.Select(p => new StudyDictionaryPack(root,p.Code,p.Name)).ToArray();
                        Assert(StudyDictionaryPack.Selected(all,fixture.Config).Select(p => p.Code).SequenceEqual(new[] { "ja","fr" }));
                        Assert(StudyDictionaryPack.Selected(all,fixture.Config with { LearnJapanese=false }).Length == 0);
                    });
                    await Check("Ambiguous entries require a language before saving", async () =>
                    {
                        var pack = new StudyDictionaryPack(Path.Combine(root,"ambiguity"),"fr","法语");
                        pack.Install(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new StudyDictionaryData("fr",new[] { new StudyDictionaryEntry("pain","pɛ̃","面包","",Array.Empty<string>()) })));
                        var popup = new PopupWindow { Packs = new[] { pack } }; string saved = "";
                        popup.FavoriteRequested += (code,word) => saved = code+"/"+word;
                        popup.Begin("pain",new AppSettings { LearnEnglish = true },300,250,0); await Idle();
                        var star = FindButton(popup,"收藏单词"); Assert(!star.IsVisible);
                        FindButton(popup,"法语").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); star.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(saved == "fr/pain");
                        FindButton(popup,"英语").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); star.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(saved == "en/pain");
                        popup.Close();
                    });
                    await Check("Japanese pack is optional and retains collected inflections offline", async () =>
                    {
                        var folder = Path.Combine(root,"optional-ja"); Directory.CreateDirectory(folder);
                        File.WriteAllText(Path.Combine(folder,"yike-data-root.txt"), Path.Combine(folder,"workspace-data"));
                        Assert(new SettingsStore(folder).DefaultDirectory == Path.Combine(folder,"workspace-data"));
                        var pack = new StudyDictionaryPack(folder);
                        Assert(!pack.Installed && pack.Find("食べた") is null);
                        foreach (var (code,name) in StudyDictionaryPack.Languages.Skip(1))
                        {
                            var optional = new StudyDictionaryPack(folder,code,name);
                            Assert(!optional.Installed);
                            optional.Install(File.ReadAllBytes(Path.Combine(Environment.CurrentDirectory,"study-packs",code+"-starter.json")));
                            var form = code switch { "fr" => "lis", "ko" => "읽어요", "de" => "liest", _ => "leo" };
                            Assert(new StudyDictionaryPack(folder,code,name).Find(form)?.Meaning.Contains("读") == true);
                        }
                        pack.Install(File.ReadAllBytes(Path.Combine(Environment.CurrentDirectory,"study-packs","ja-starter.json")));
                        Assert(new StudyDictionaryPack(folder).Find("食べた")?.Headword == "食べる");
                        var manual = new ManualWindow(new AppSettings(), pack); manual.Show(); await Idle();
                        Walk(manual).OfType<Expander>().Single(e => Equals(e.Header,"单词研习")).IsExpanded = true;
                        Find<TextBox>(manual,"原文").Text = "食べた"; await Idle();
                        Assert(Walk(manual).OfType<TextBlock>().Any(t => t.Text.Contains("食べる")));
                        Assert(FindButton(manual,"收藏单词").IsEnabled); manual.Close();
                        var book = StudyBook.JapaneseFile(Path.Combine(folder,"YiKe-vocabulary.json"));
                        StudyBook.Add(book,"食べた"); Assert(StudyBook.Read(book).Single() == "食べた");
                        var popup = new PopupWindow { Packs = new[] { pack } }; popup.Begin("食べた",new AppSettings(),300,250,0); await Idle();
                        Assert(FindButton(popup,"收藏单词").IsVisible); popup.Dismiss(); popup.Close();
                    });
                    await Check("Study words persist across data relocation and manual direction reaches the form", async () =>
                    {
                        var store = new SettingsStore(userDirectory: Path.Combine(root, "study-default"));
                        var path = Path.Combine(store.UserDirectory, "YiKe-vocabulary.json");
                        Assert(StudyBook.Add(path, "Light") == "已收藏。" && StudyBook.Add(path, "light") == "已在收藏本中。");
                        var config = new AppSettings();
                        store.ChangeDirectory(Path.Combine(root,"study-moved"), config);
                        Assert(StudyBook.Read(Path.Combine(store.UserDirectory, "YiKe-vocabulary.json")).Single() == "light");
                        Assert(new SettingsStore(userDirectory: store.DefaultDirectory).UserDirectory == store.UserDirectory);
                        Assert(BundledEnglishDictionary.Details("light")!.Contains("搭配与例句"));
                        var window = new ManualWindow(config with { TranslationMode = "online" }); window.Show(); await Idle();
                        Find<ComboBox>(window, "源语言").SelectedIndex = 3; Find<ComboBox>(window,"目标语言").SelectedIndex = 4;
                        Assert(window.From == TranslationLanguages.Presets[2] && window.To == TranslationLanguages.Presets[4]); window.Close();
                    });
                    await Check("Manual window keeps source editable after failures and cancellation", async () =>
                    {
                        var config = new AppSettings { LocalEngine = "nmt" };
                        var window = new ManualWindow(config); window.Show(); await Idle();
                        var input = Find<TextBox>(window, "原文"); input.Text = "hello";
                        window.Begin(config); Assert(input.IsReadOnly);
                        window.Update(new(1, "你好", true));
                        Assert(!input.IsReadOnly && Find<TextBox>(window, "译文").Text == "你好");
                        window.Begin(config); window.Error("请缩短原文。");
                        Assert(input.Text == "hello" && !input.IsReadOnly);
                        bool cancelled = false; window.CancelRequested += () => cancelled = true;
                        window.Begin(config); FindButton(window, "取消").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Assert(cancelled && !input.IsReadOnly); window.Close();
                    });
                    await Check("Popup service, progress, extensions and cancellation", async () =>
                    {
                        var popup = new PopupWindow(); popup.Begin("hello", new AppSettings { LocalEngine = "nmt" }, 300, 250, 0); await Idle();
                        bool Has(string label) => Walk(popup).OfType<TextBlock>().Any(t => t.Text.Contains(label));
                        Assert(Has("本地英译中") && Has("准备引擎"));
                        Assert(FindButton(popup,"翻译扩展").IsVisible);
                        popup.Update(new(1, "")); Assert(Has("翻译中"));
                        bool cancelled = false; popup.CancelRequested += () => cancelled = true;
                        FindButton(popup, "取消").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Assert(cancelled && Has("已取消")); popup.Dismiss(); popup.Close();
                    });
                    await Check("NMT low memory test bypasses generic-model gate and translates locally", async () => {
                        var config = new AppSettings { TranslationMode = "local", LocalEngine = "nmt" };
                        var store = new SettingsStore(userDirectory: Path.Combine(root, "nmt-low-memory"));
                        using var provider = new TranslationProvider();
                        var window = new SettingsWindow(() => config, (_, _) => {}, store, new CredentialStore(store.UserDirectory),
                            () => "synthetic low-memory fixture", testProvider: provider, modelCatalog: new RecordingModelCatalog(),
                            localHardware: () => new(16 * LocalHardware.GiB, LocalHardware.GiB / 2, 8, true));
                        window.Show(); await Idle(); window.TestConnection();
                        for (int i = 0; i < 900 && !window.StatusText.Contains("本地翻译成功"); i++)
                        {
                            if (window.StatusText.Contains("不足") || window.StatusText.Contains("失败")) throw new Exception(window.StatusText);
                            await Task.Delay(100);
                        }
                        Assert(window.StatusText.Contains("本地翻译成功"), window.StatusText);
                        var engine = Walk(window).OfType<ComboBox>().Single(combo => combo.Items.Count == 2 && Equals(combo.Items[0], "英译中节能引擎"));
                        engine.SelectedIndex = 1; await Idle(); window.TestConnection();
                        Assert(window.StatusText.Contains("1.25 GiB"), "Generic model must retain its memory gate");
                        window.Close();
                    });
                    await Check("Prism titlebar maximizes restores minimizes and closes the settings window", async () => {
                        using var fixture=new EditorFixture(root,"prism-window-controls");var window=fixture.Window;window.Show();await Idle();
                        FindButton(window,"最大化 / 还原").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Idle();Assert(window.WindowState==WindowState.Maximized);
                        FindButton(window,"最大化 / 还原").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Idle();Assert(window.WindowState==WindowState.Normal);
                        FindButton(window,"最小化").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Idle();Assert(window.WindowState==WindowState.Minimized);
                        SystemCommands.RestoreWindow(window);await Idle();FindButton(window,"关闭").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert(!window.IsVisible);
                    });
                    await Check("PDF automatically copies once after direct reading fails and does not copy for known controls", async () => {
                        var source = new FakePdfCopySource { AutoReply = _ => Task.FromResult<SelectionReadResult?>(new(ReadStatus.Success,"automatic selection")) };
                        using var fallback = new PdfCopyFallback(() => true, _ => false, source); int received=0;
                        fallback.Copied += (_,result) => { Assert(result.Text=="automatic selection"); received++; };
                        var gesture=new Gesture(1,123,7,30,20,10,20,DateTimeOffset.UtcNow);
                        fallback.Arm(gesture); await Idle(); Assert(source.Copies==0);
                        fallback.CompleteRead(gesture,ReadStatus.Unsupported); await Until(()=>received==1); Assert(source.Copies==1 && source.Reads==0);
                        foreach(var status in new[]{ReadStatus.Success,ReadStatus.AccessDenied,ReadStatus.NotTextSelection}){fallback.Arm(gesture);fallback.CompleteRead(gesture,status);await Idle();Assert(source.Copies==1);}
                    });
                    await Check("PDF automatic copy never publishes after cancellation focus change pause or a new selection", async () => {
                        var release = new TaskCompletionSource<SelectionReadResult?>();
                        var source=new FakePdfCopySource {AutoReply=_=>release.Task}; bool enabled=true;
                        using var fallback=new PdfCopyFallback(()=>enabled,_=>false,source);int received=0;fallback.Copied+=(_,_)=>received++;
                        var gesture=new Gesture(1,123,7,30,20,10,20,DateTimeOffset.UtcNow);
                        fallback.Arm(gesture);fallback.CompleteRead(gesture,ReadStatus.Timeout);await Until(()=>source.Copies==1);
                        fallback.Clear();enabled=false;source.Current=false;release.SetResult(new(ReadStatus.Success,"obsolete"));await Idle();Assert(received==0);
                    });
                    await Check("Native clipboard snapshot restores Unicode HTML and RTF without overwriting a newer copy", async () => {
                        using var owner=new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("YiKe clipboard snapshot fixture"){ParentWindow=new IntPtr(-3)});
                        var original=NativeClipboardSnapshot.Capture(); if(original is null)throw new DesktopUnavailableException("Original clipboard cannot be safely snapshotted; left untouched.");
                        uint owned=0;
                        try {
                            var data=new DataObject();data.SetText("original fixture");data.SetData(DataFormats.Html,"<b>original fixture</b>");data.SetData(DataFormats.Rtf,@"{\rtf1 original fixture}");
                            Clipboard.SetDataObject(data,true);owned=Native.GetClipboardSequenceNumber();var snapshot=NativeClipboardSnapshot.Capture() ?? throw new Exception("Missing native snapshot");
                            Clipboard.SetText("automatic PDF copy");owned=Native.GetClipboardSequenceNumber();Assert(snapshot.Restore(owned,owner.Handle));owned=Native.GetClipboardSequenceNumber();
                            Assert(Clipboard.GetText()=="original fixture" && Clipboard.GetData(DataFormats.Html) as string =="<b>original fixture</b>" && Clipboard.ContainsData(DataFormats.Rtf));
                            Clipboard.SetText("automatic PDF copy");owned=Native.GetClipboardSequenceNumber();uint copied=owned;
                            Clipboard.SetText("newer copy");owned=Native.GetClipboardSequenceNumber();
                            await Until(() => Clipboard.GetText()=="newer copy");
                            bool restored = snapshot.Restore(copied,owner.Handle);
                            Assert(!restored, $"Restored stale snapshot: copy sequence {copied}, newer {owned}.");
                            Assert(Clipboard.GetText()=="newer copy", $"Stale restore declined; expected fixture missing. Newer sequence {owned}, current {Native.GetClipboardSequenceNumber()}.");
                        } finally {original.Restore(owned,owner.Handle);}
                        await Idle();
                    });
                    await Check("Native clipboard snapshot preserves image and file-drop contents", async () => {
                        using var owner=new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("YiKe clipboard formats fixture"){ParentWindow=new IntPtr(-3)});
                        var original=NativeClipboardSnapshot.Capture();if(original is null)throw new DesktopUnavailableException("Original clipboard left untouched.");uint owned=0;
                        try {
                            var image=System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{10,20,30,255},4);Clipboard.SetImage(image);owned=Native.GetClipboardSequenceNumber();
                            var snapshot=NativeClipboardSnapshot.Capture() ?? throw new Exception("Missing image snapshot");Clipboard.SetText("PDF copy");owned=Native.GetClipboardSequenceNumber();
                            bool imageRestored=snapshot.Restore(owned,owner.Handle);
                            if(!imageRestored)throw new Exception($"Image clipboard restoration failed; expected sequence {owned}, current {Native.GetClipboardSequenceNumber()}");
                            owned=Native.GetClipboardSequenceNumber();if(Clipboard.GetImage()?.PixelWidth!=1)throw new Exception("Restored image unavailable; formats: "+string.Join(",",Clipboard.GetDataObject()?.GetFormats() ?? Array.Empty<string>()));
                            var files=new System.Collections.Specialized.StringCollection {Path.Combine(root,"fixture.txt")};Clipboard.SetFileDropList(files);owned=Native.GetClipboardSequenceNumber();snapshot=NativeClipboardSnapshot.Capture() ?? throw new Exception("Missing files snapshot");Clipboard.SetText("PDF copy");owned=Native.GetClipboardSequenceNumber();Assert(snapshot.Restore(owned,owner.Handle));owned=Native.GetClipboardSequenceNumber();Assert(Clipboard.GetFileDropList().Contains(files[0]!));
                        }finally{original.Restore(owned,owner.Handle);}await Idle();
                    });
                    await Check("Selection icon waits for a click and consumes its source exactly once", async () => {
                        var icon = new SelectionIcon(); int calls = 0;
                        var snapshot = new SelectionSnapshot(7, 0, Environment.ProcessId, "hello", 300, 250, DateTimeOffset.UtcNow);
                        icon.Requested += selected => { Assert(selected == snapshot); calls++; };
                        icon.Present(snapshot); await Idle(); Assert(calls == 0 && icon.Selection == snapshot);
                        FindButton(icon, "译").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Assert(calls == 1 && icon.Selection is null && !icon.IsVisible);
                        FindButton(icon, "译").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(calls == 1); icon.Close();
                    });
                    await Check("Saved DeepL preset survives reopen and does not request AI model lists", async () => {
                        var store = new SettingsStore(userDirectory: Path.Combine(root, "deepl-preset"));
                        var config = new AppSettings { TranslationMode = "online", Preset = "DeepL", BaseUrl = "https://api-free.deepl.com/v2", Model = "DeepL" };
                        var catalog = new RecordingModelCatalog();
                        var window = new SettingsWindow(() => config, (draft, secret) => config = store.Save(draft), store, new CredentialStore(store.UserDirectory), () => "fixture", modelCatalog: catalog);
                        window.Show(); await Idle(); Assert((string?)Find<ComboBox>(window, "服务预设").SelectedItem == "DeepL");
                        window.SaveSettings(); Assert(config.Preset == "DeepL" && catalog.Requests.Count == 0);
                    });
                    await Check("English learning exposes word speech but never sentence speech", async () => {
                        var popup = new PopupWindow(); popup.Begin("hello", new AppSettings { LearnEnglish = true }, 300, 250, 0); await Idle();
                        Assert(FindButton(popup, "美音朗读").IsVisible && FindButton(popup,"英音朗读").IsVisible);
                        Assert(Walk(popup).OfType<TextBlock>().Any(t => t.Text.Contains("你好") || t.Text.Contains("喂")));
                        popup.Begin("The meeting starts tomorrow.", new AppSettings { LearnEnglish = true }, 300, 250, 0); await Idle();
                        Assert(!FindButton(popup, "美音朗读").IsVisible);
                        popup.Begin("I love you", new AppSettings { LearnEnglish = true }, 300, 250, 0); await Idle();
                        Assert(!FindButton(popup, "美音朗读").IsVisible); popup.DisposeSpeech(); popup.Close();
                    });
                    await Check("Mouse listener owns a separate message loop from a busy UI dispatcher", async () =>
                    {
                        using var monitor = new SelectionMonitor(Dispatcher.CurrentDispatcher);
                        Assert(monitor.HookThreadId != Environment.CurrentManagedThreadId);
                        var field = typeof(SelectionMonitor).GetField("hookDispatcher", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                        var listener = (Dispatcher)field.GetValue(monitor)!;
                        var probe = Task.Run(async () => { await Task.Delay(40); await listener.InvokeAsync(() => true); });
                        Thread.Sleep(1150); Assert(probe.IsCompletedSuccessfully); await probe;
                    });
                    await Check("Low hardware disables local testing and save without writing config or making requests", async () =>
                    {
                        var store = new SettingsStore(userDirectory: Path.Combine(root, "unsupported-hardware")); var credentials = new CredentialStore(store.UserDirectory); AppSettings config = new();
                        var provider = new RecordingConnectionProvider(); var catalog = new RecordingModelCatalog();
                        var window = new SettingsWindow(() => config, (draft, secret) => config = store.Save(draft), store, credentials, () => "fixture", testProvider: provider, modelCatalog: catalog, localHardware: () => new(4 * LocalHardware.GiB, 3 * LocalHardware.GiB, 2, true));
                        window.Show(); await Idle(); Assert(!FindButton(window, "测试连接").IsEnabled);
                        window.TestConnection(); Assert(window.StatusText.Contains("不支持本地模型") && provider.Keys.Count == 0);
                        window.SaveSettings(); Assert(!File.Exists(store.UserFile));
                        Find<ComboBox>(window, "翻译方式").SelectedIndex = 1;
                        Assert(FindButton(window, "测试连接").IsEnabled && catalog.Requests.Count == 0);
                    });
                    await Check("Settings import refreshes visible fields and preserves migrated secret on save", async () =>
                    {
                        using var fixture = new EditorFixture(root, "import"); var window = fixture.Window; window.Show();
                        var imported = fixture.Config with { ConfigId = Guid.NewGuid(), ServiceId = Guid.NewGuid(), Preset = "自定义兼容服务", BaseUrl = "https://new.example.test/v1", Model = "migrated-model", FontSize = 15.5, Theme = "dark", TargetLanguage = "English" };
                        window.ApplyImported(imported, "fake-migrated-secret"); await Idle();
                        Assert(Find<ComboBox>(window, "模型名称").Text == "migrated-model"); Assert(Find<TextBox>(window, "Base URL").Text == imported.BaseUrl);
                        Assert(Find<PasswordBox>(window, "API Key").Password.Length == 0 && !Find<PasswordBox>(window, "API Key").IsEnabled);
                        window.SaveSettings(); Assert(fixture.Config.Model == "migrated-model" && fixture.Config.FontSize == 15.5 && fixture.Config.TargetLanguage == "English");
                        Assert(fixture.Credentials.Read(fixture.Config) == "fake-migrated-secret"); Assert(window.StatusText.Contains("已保存"));
                    });
                    await Check("Multiple API drafts retain separate fields keys and active selection after save and reload", async () =>
                    {
                        using var fixture = new EditorFixture(root, "multiple-api"); var window = fixture.Window; window.Show(); await Idle();
                        Guid original = fixture.Config.ActiveOnlineProfileId;
                        window.AddOnlineProfile();
                        var selector = Find<ComboBox>(window, "在线 API 配置"); Guid added = (Guid)selector.SelectedValue;
                        Assert(Find<PasswordBox>(window, "API Key").Password == "" && Find<PasswordBox>(window, "API Key").IsEnabled);
                        Find<TextBox>(window, "配置名称").Text = "备用 API";
                        Find<TextBox>(window, "Base URL").Text = "https://backup.example.test/v1";
                        Find<PasswordBox>(window, "API Key").Password = "fake-backup-secret";
                        Find<ComboBox>(window, "模型名称").Text = "backup-model";
                        selector.SelectedValue = original; await Idle();
                        Assert(Find<ComboBox>(window, "模型名称").Text == "original-model" && !Find<PasswordBox>(window, "API Key").IsEnabled);
                        selector.SelectedValue = added; await Idle();
                        Assert(Find<PasswordBox>(window, "API Key").Password == "fake-backup-secret" && Find<ComboBox>(window, "模型名称").Text == "backup-model");
                        window.SaveSettings(); Assert(fixture.Config.OnlineProfiles.Length == 2 && fixture.Config.ActiveOnlineProfileId == added);
                        window.UpdateLayout(); Assert(selector.SelectedItem.ToString() == "备用 API");
                        Assert(!Walk(selector).OfType<TextBlock>().Any(text => text.Text.Contains("ServiceId") || text.Text.Contains("OnlineProfile {")));
                        Assert(fixture.Credentials.Read(fixture.Config) == "fake-backup-secret");
                        Assert(fixture.Credentials.Read(OnlineProfiles.Select(fixture.Config, original)) == "fake-existing-secret");
                        window.ReloadFromSaved(); selector.SelectedValue = original; window.SaveSettings();
                        Assert(fixture.Store.Load().ActiveOnlineProfileId == original && fixture.Config.Model == "original-model");
                    });
                    await Check("Switching API profile cancels stale model results and uses only the selected account key", async () =>
                    {
                        var stale = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var catalog = new RecordingModelCatalog(index => index == 1 ? stale.Task : Task.FromResult<IReadOnlyList<string>>(new[] { "backup-only" }));
                        using var fixture = new EditorFixture(root, "profile-models", catalog: catalog); var window = fixture.Window; window.Show();
                        var initial = window.RefreshModelsAsync(); await Until(() => catalog.Requests.Count == 1);
                        window.AddOnlineProfile();
                        Find<PasswordBox>(window, "API Key").Password = "fake-second-account";
                        Find<ComboBox>(window, "模型名称").Text = "manual-backup";
                        await window.RefreshModelsAsync();
                        stale.TrySetResult(new[] { "stale-original-model" }); await initial;
                        Assert(catalog.Requests.Last().Item2 == "fake-second-account");
                        Assert(Find<ComboBox>(window, "模型名称").Items.Cast<string>().SequenceEqual(new[] { "backup-only" }));
                        Assert(Find<ComboBox>(window, "模型名称").Text == "manual-backup");
                    });
                    await Check("Batch save failure keeps both saved keys and pending profile edits for a successful retry", async () =>
                    {
                        using var fixture = new EditorFixture(root, "profiles-write-failure"); var window = fixture.Window; window.Show(); await Idle();
                        Guid original = fixture.Config.ActiveOnlineProfileId;
                        FindButton(window, "更换密钥").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Find<PasswordBox>(window, "API Key").Password = "fake-replacement-a";
                        window.AddOnlineProfile(); Find<PasswordBox>(window, "API Key").Password = "fake-new-b";
                        Find<ComboBox>(window, "模型名称").Text = "second-model";
                        byte[] before = File.ReadAllBytes(fixture.Store.UserFile); File.SetAttributes(fixture.Store.UserFile, FileAttributes.ReadOnly);
                        try { window.SaveSettings(); Assert(before.SequenceEqual(File.ReadAllBytes(fixture.Store.UserFile))); Assert(fixture.Credentials.Read(fixture.Config) == "fake-existing-secret"); Assert(Find<PasswordBox>(window, "API Key").Password == "fake-new-b"); }
                        finally { File.SetAttributes(fixture.Store.UserFile, FileAttributes.Normal); }
                        window.SaveSettings(); Assert(fixture.Config.OnlineProfiles.Length == 2 && fixture.Credentials.Read(fixture.Config) == "fake-new-b");
                        Assert(fixture.Credentials.Read(OnlineProfiles.Select(fixture.Config, original)) == "fake-replacement-a");
                    });
                    await Check("Removing one API commits its deletion without removing another credential", async () =>
                    {
                        using var fixture = new EditorFixture(root, "profiles-delete"); var window = fixture.Window; window.Show(); await Idle();
                        Guid original = fixture.Config.ActiveOnlineProfileId;
                        window.AddOnlineProfile(); Find<PasswordBox>(window, "API Key").Password = "fake-delete-only-this";
                        Find<ComboBox>(window, "模型名称").Text = "disposable-model"; window.SaveSettings();
                        var removed = fixture.Config; window.RemoveOnlineProfile(); Assert(fixture.Credentials.Read(removed) == "fake-delete-only-this");
                        window.SaveSettings(); Assert(fixture.Config.ActiveOnlineProfileId == original && fixture.Config.OnlineProfiles.Length == 1);
                        Assert(fixture.Credentials.Read(removed) is null && fixture.Credentials.Read(fixture.Config) == "fake-existing-secret");
                        window.RemoveOnlineProfile(); Assert(window.StatusText.Contains("至少保留"));
                    });
                    await Check("Encrypted multi API import binds every key and tests the selected saved account", async () =>
                    {
                        var provider = new RecordingConnectionProvider(); using var fixture = new EditorFixture(root, "profiles-migration", provider); var window = fixture.Window; window.Show(); await Idle();
                        var draft = OnlineProfiles.Add(fixture.Config) with { Model = "imported-model", BaseUrl = "https://import.example.test/v1" };
                        draft = OnlineProfiles.Capture(draft, "导入的 API");
                        var keys = draft.OnlineProfiles.ToDictionary(p => p.Id, p => p.Id == draft.ActiveOnlineProfileId ? "fake-imported-b" : "fake-imported-a");
                        var payload = MigrationService.Decrypt(MigrationService.Encrypt(draft, null, "profiles-password-42", keys), "profiles-password-42");
                        window.ApplyImported(payload.Settings, payload.ApiKey, payload.ProfileKeys); window.TestConnection();
                        await Until(() => window.StatusText.Contains("连接成功")); Assert(provider.Keys.Last() == "fake-imported-b");
                        Guid other = fixture.Config.OnlineProfiles.First(p => p.Id != fixture.Config.ActiveOnlineProfileId).Id;
                        Find<ComboBox>(window, "在线 API 配置").SelectedValue = other; window.TestConnection();
                        await Until(() => window.StatusText.Contains("连接成功")); Assert(provider.Keys.Last() == "fake-imported-a");
                        Assert(!File.ReadAllText(fixture.Store.UserFile).Contains("fake-imported"));
                    });
                    await Check("Saving local mode preserves keys staged for inactive online API profiles", async () =>
                    {
                        using var fixture = new EditorFixture(root, "profiles-local"); var window = fixture.Window; window.Show(); await Idle();
                        Guid original = fixture.Config.ActiveOnlineProfileId; window.AddOnlineProfile();
                        Guid added = (Guid)Find<ComboBox>(window, "在线 API 配置").SelectedValue;
                        Find<PasswordBox>(window, "API Key").Password = "fake-local-retained"; Find<ComboBox>(window, "模型名称").Text = "retained-model";
                        Find<ComboBox>(window, "在线 API 配置").SelectedValue = original;
                        fixture.Config = fixture.Config with { Enabled = false }; // A tray pause while settings remain open must survive saving.
                        Find<ComboBox>(window, "翻译方式").SelectedIndex = 0; window.SaveSettings();
                        Assert(!fixture.Config.Enabled && fixture.Config.TranslationMode == "local" && fixture.Config.OnlineProfiles.Length == 2);
                        Assert(fixture.Credentials.Read(OnlineProfiles.Select(fixture.Config, added)) == "fake-local-retained");
                    });
                    await Check("WPS PDF copy accepts fresh text during UIA reading and expires its intent", async () =>
                    {
                        var clipboard = new FakePdfCopySource(); bool enabled = true, excluded = false;
                        using var fallback = new PdfCopyFallback(() => enabled, _ => excluded, clipboard);
                        var copied = new List<string>(); fallback.Copied += (_, result) => copied.Add(result.Text!);
                        var gesture = new Gesture(1, 123, 7, 30, 20, 10, 20, DateTimeOffset.UtcNow);
                        fallback.Arm(gesture); clipboard.Sequence++;
                        fallback.ClipboardChanged(); await Until(() => copied.Count == 1); Assert(clipboard.Reads == 1);
                        fallback.CompleteRead(gesture, ReadStatus.Unsupported); await Until(() => copied.Count == 1);
                        Assert(copied.Single() == "new PDF selection");
                        fallback.Arm(gesture with { Sequence = 2 }); fallback.CompleteRead(gesture with { Sequence = 2 }, ReadStatus.NoSelection);
                        await Idle(); Assert(clipboard.Reads == 1); // Clipboard unchanged after arming.
                        clipboard.Now = 15001; clipboard.Sequence++; fallback.ClipboardChanged(); await Idle(); Assert(clipboard.Reads == 1);
                        enabled = false; fallback.Arm(gesture); clipboard.Sequence++; fallback.CompleteRead(gesture, ReadStatus.Unsupported); fallback.ClipboardChanged(); await Idle(); Assert(clipboard.Reads == 1);
                        enabled = true; excluded = true; fallback.Arm(gesture); clipboard.Sequence++; fallback.CompleteRead(gesture, ReadStatus.Unsupported); fallback.ClipboardChanged(); await Idle(); Assert(clipboard.Reads == 1);
                    });
                    await Check("WPS PDF copy ignores denied canceled and late reads while allowing the newest gesture", async () =>
                    {
                        var late = new TaskCompletionSource<SelectionReadResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var clipboard = new FakePdfCopySource { Reply = index => index == 1 ? late.Task : Task.FromResult<SelectionReadResult?>(new(ReadStatus.Success, "latest PDF selection")) };
                        using var fallback = new PdfCopyFallback(() => true, _ => false, clipboard);
                        var copied = new List<string>(); fallback.Copied += (_, result) => copied.Add(result.Text!);
                        var gesture = new Gesture(1, 123, 7, 30, 20, 10, 20, DateTimeOffset.UtcNow);
                        fallback.Arm(gesture); fallback.CompleteRead(gesture, ReadStatus.AccessDenied); clipboard.Sequence++; fallback.ClipboardChanged(); await Idle(); Assert(clipboard.Reads == 0);
                        fallback.Arm(gesture); fallback.CompleteRead(gesture, ReadStatus.Unsupported); clipboard.Sequence++; fallback.ClipboardChanged(); await Until(() => clipboard.Reads == 1);
                        var newer = gesture with { Sequence = 2 }; fallback.Arm(newer); fallback.CompleteRead(newer, ReadStatus.Unsupported); clipboard.Sequence++; fallback.ClipboardChanged();
                        late.TrySetResult(new(ReadStatus.Success, "stale PDF selection")); await Until(() => copied.Count == 1);
                        Assert(clipboard.Reads == 2 && copied.Single() == "latest PDF selection");
                        fallback.Arm(gesture); fallback.CompleteRead(gesture, ReadStatus.Unsupported); fallback.Clear(); clipboard.Sequence++; fallback.ClipboardChanged(); await Idle(); Assert(clipboard.Reads == 2);
                    });
                    await Check("WPS PDF copy rechecks an overlapping clipboard update and rejects a changed source", async () =>
                    {
                        var late = new TaskCompletionSource<SelectionReadResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var clipboard = new FakePdfCopySource { Reply = index => index == 1 ? late.Task : Task.FromResult<SelectionReadResult?>(new(ReadStatus.Success, "freshest copy")) };
                        using var fallback = new PdfCopyFallback(() => true, _ => false, clipboard);
                        var copied = new List<string>(); fallback.Copied += (_, result) => copied.Add(result.Text!);
                        var gesture = new Gesture(1, 123, 7, 30, 20, 10, 20, DateTimeOffset.UtcNow);
                        fallback.Arm(gesture); fallback.CompleteRead(gesture, ReadStatus.Unsupported); clipboard.Sequence++; fallback.ClipboardChanged(); await Until(() => clipboard.Reads == 1);
                        clipboard.Sequence++; fallback.ClipboardChanged(); late.TrySetResult(new(ReadStatus.Success, "obsolete copy")); await Until(() => copied.Count == 1);
                        Assert(clipboard.Reads == 2 && copied.Single() == "freshest copy");
                        fallback.Arm(gesture); fallback.CompleteRead(gesture, ReadStatus.Unsupported); clipboard.Current = false; clipboard.Sequence++; fallback.ClipboardChanged(); await Idle(); Assert(clipboard.Reads == 2);
                        clipboard.Current = true; fallback.Arm(gesture); fallback.CompleteRead(gesture, ReadStatus.Unsupported);
                        clipboard.Current = false; fallback.Update(); clipboard.Current = true;
                        clipboard.Sequence++; fallback.ClipboardChanged(); await Idle(); Assert(clipboard.Reads == 2);
                    });
                    await Check("WPS PDF copy preference saves and reloads independently from API credentials", async () =>
                    {
                        using var fixture = new EditorFixture(root, "pdf-copy-setting"); var window = fixture.Window; window.Show(); await Idle();
                        FindButton(window, "外观与行为").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Idle();
                        var checkbox = Find<CheckBox>(window, "WPS PDF 复制后翻译"); Assert(checkbox.IsChecked == true);
                        checkbox.IsChecked = false; window.SaveSettings(); Assert(!fixture.Config.TranslateCopiedPdf && !fixture.Store.Load().TranslateCopiedPdf);
                        Assert(fixture.Credentials.Read(fixture.Config) == "fake-existing-secret");
                        window.ReloadFromSaved(); Assert(checkbox.IsChecked == false);
                        window.ApplyImported(fixture.Config with { TranslateCopiedPdf = true }, null); Assert(checkbox.IsChecked == true);
                    });
                    await Check("Unedited encrypted key remains untouched and is never used as a mask string", async () =>
                    {
                        using var fixture = new EditorFixture(root, "preserve"); fixture.Window.Show(); await Idle();
                        Guid service = fixture.Config.ServiceId; fixture.Window.SaveSettings();
                        Assert(fixture.LastCommittedSecret is null && fixture.Config.ServiceId == service);
                        Assert(fixture.Credentials.Read(fixture.Config) == "fake-existing-secret");
                    });
                    await Check("Settings write failure retains old credentials and unsaved replacement input", async () =>
                    {
                        using var fixture = new EditorFixture(root, "save-failure"); var window = fixture.Window; window.Show(); await Idle();
                        FindButton(window, "更换密钥").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Find<PasswordBox>(window, "API Key").Password = "fake-replacement-secret";
                        File.SetAttributes(fixture.Store.UserFile, FileAttributes.ReadOnly);
                        try
                        {
                            window.SaveSettings(); Assert(fixture.Credentials.Read(fixture.Config) == "fake-existing-secret");
                            Assert(Find<PasswordBox>(window, "API Key").Password == "fake-replacement-secret");
                            Assert(!window.StatusText.Contains("已保存，下次"));
                        }
                        finally { File.SetAttributes(fixture.Store.UserFile, FileAttributes.Normal); }
                    });
                    await Check("Connection cancellation blocks late completion feedback", async () =>
                    {
                        var provider = new BlockingConnectionProvider(); using var fixture = new EditorFixture(root, "cancel", provider); fixture.Window.Show();
                        fixture.Window.TestConnection(); await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
                        fixture.Window.TestConnection(); string feedback = fixture.Window.StatusText;
                        provider.Release.TrySetResult(); await Task.Delay(120);
                        Assert(feedback.Contains("已取消") && fixture.Window.StatusText == feedback && provider.Calls == 1);
                        Assert(fixture.Commits == 0 && fixture.Credentials.Read(fixture.Config) == "fake-existing-secret");
                    });
                    await Check("Editing endpoint cancels the test and cannot send the saved key to the new host", async () =>
                    {
                        var provider = new BlockingConnectionProvider(); using var fixture = new EditorFixture(root, "edit", provider); var window = fixture.Window; window.Show();
                        window.TestConnection(); await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
                        Find<TextBox>(window, "Base URL").Text = "https://different.example.test/v1";
                        provider.Release.TrySetResult(); await Task.Delay(120); Assert(window.StatusText.Contains("配置已更改"));
                        window.TestConnection(); await Task.Delay(60); Assert(provider.Calls == 1 && window.StatusText.Contains("API Key"));
                        Assert(fixture.Config.BaseUrl == "https://api.openai.com/v1" && fixture.Commits == 0);
                    });
                    await Check("Repeated explicit connection checks make two requests and do not save settings", async () =>
                    {
                        var provider = new RecordingConnectionProvider(); using var fixture = new EditorFixture(root, "repeat", provider); fixture.Window.Show();
                        fixture.Window.TestConnection(); await Until(() => fixture.Window.StatusText.Contains("连接成功"));
                        fixture.Window.TestConnection(); await Until(() => fixture.Window.StatusText.Contains("连接成功"));
                        Assert(provider.Calls == 2 && provider.Texts.All(text => text == "Hello, world!") && fixture.Commits == 0);
                    });
                    await Check("Closing settings isolates a late connection error", async () =>
                    {
                        var provider = new BlockingConnectionProvider(fail: true); using var fixture = new EditorFixture(root, "close", provider); fixture.Window.Show();
                        fixture.Window.TestConnection(); await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(3)); fixture.Window.Close();
                        string before = fixture.Window.StatusText; provider.Release.TrySetResult(); await Task.Delay(120); Assert(fixture.Window.StatusText == before);
                    });
                    await Check("Repeated navigation keeps one owner for every settings control", async () =>
                    {
                        using var fixture = new EditorFixture(root, "navigation"); var window = fixture.Window; window.Show();
                        for (int i = 0; i < 6; i++) foreach (string page in new[] { "外观与行为", "数据与迁移", "翻译服务" })
                        { FindButton(window, page).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Idle(); }
                        Assert(Find<ComboBox>(window, "模型名称").Text == fixture.Config.Model);
                    });
                    await Check("The settings footer remains reachable in a constrained window", async () =>
                    {
                        using var fixture = new EditorFixture(root, "small-window"); var window = fixture.Window; window.Show(); await Idle();
                        window.MinWidth = window.MinHeight = 0; window.MaxWidth = 620; window.MaxHeight = 430; window.Width = 620; window.Height = 430; await Idle();
                        var save = FindButton(window, "保存设置"); var bounds = save.TransformToAncestor(window).TransformBounds(new Rect(save.RenderSize));
                        Assert(save.IsVisible && bounds.Top >= 0 && bounds.Bottom <= window.ActualHeight && bounds.Right <= window.ActualWidth);
                        FindButton(window, "外观与行为").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Idle();
                        Assert(Find<ComboBox>(window, "视觉风格").IsVisible);
                    });
                    await Check("Fresh local mode needs no API fields, key, save or model discovery", async () =>
                    {
                        var store = new SettingsStore(Path.Combine(root, "fresh-local", "app"), Path.Combine(root, "fresh-local", "data"));
                        var credentials = new CredentialStore(store.UserDirectory); AppSettings config = new();
                        var provider = new RecordingConnectionProvider(); var catalog = new RecordingModelCatalog();
                        var window = new SettingsWindow(() => config, (draft, secret) => config = ConfigurationService.Commit(config, draft, secret, store, credentials), store, credentials, () => "fixture", testProvider: provider, modelCatalog: catalog, localHardware: () => new(16 * LocalHardware.GiB, 8 * LocalHardware.GiB, 8, true));
                        window.Show(); await Idle();
                        Assert(Find<ComboBox>(window, "翻译方式").SelectedIndex == 0 && !Find<TextBox>(window, "Base URL").IsVisible && !Find<PasswordBox>(window, "API Key").IsVisible);
                        Assert(Find<ComboBox>(window, "目标语言").Text == "简体中文" && !File.Exists(store.UserFile));
                        window.TestConnection(); await Until(() => window.StatusText.Contains("本地翻译成功")); await Task.Delay(900);
                        Assert(provider.Keys.Single() == "" && catalog.Requests.Count == 0 && !File.Exists(store.UserFile));
                        window.SaveSettings(); Assert(config.TranslationMode == "local" && config.Model == "" && window.StatusText.Contains("已保存"));
                    });
                    await Check("Local mode keeps the portable write failure notice while saving the user fallback", async () =>
                    {
                        string appPath = Path.Combine(root, "local-portable", "app"); Directory.CreateDirectory(appPath);
                        File.WriteAllText(Path.Combine(appPath, "portable.flag"), "fixture"); File.WriteAllText(Path.Combine(appPath, "data"), "blocked-directory");
                        var store = new SettingsStore(appPath, Path.Combine(root, "local-portable", "user")); var credentials = new CredentialStore(store.UserDirectory); AppSettings config = new();
                        var window = new SettingsWindow(() => config, (draft, secret) => config = ConfigurationService.Commit(config, draft, secret, store, credentials), store, credentials, () => "fixture", testProvider: new RecordingConnectionProvider(), modelCatalog: new RecordingModelCatalog());
                        window.Show(); await Idle(); window.SaveSettings();
                        Assert(File.Exists(store.UserFile) && config.TranslationMode == "local" && window.StatusText.Contains("便携目录不可写"));
                    });
                    await Check("Switching to local mode cancels an online test and ignores its late reply", async () =>
                    {
                        var provider = new BlockingConnectionProvider(); using var fixture = new EditorFixture(root, "local-switch-cancel", provider);
                        var window = fixture.Window; window.Show(); window.TestConnection(); await provider.Started.Task;
                        Find<ComboBox>(window, "翻译方式").SelectedIndex = 0; string feedback = window.StatusText;
                        provider.Release.TrySetResult(); await Task.Delay(80); Assert(window.StatusText == feedback);
                        window.TestConnection(); await Until(() => window.StatusText.Contains("本地翻译成功"));
                        Assert(provider.Keys.Last() == "" && provider.Calls == 2);
                    });
                    await Check("Local mode save preserves the previous cloud endpoint model and encrypted key", async () =>
                    {
                        using var fixture = new EditorFixture(root, "local-preserve-cloud"); var window = fixture.Window; window.Show();
                        var before = fixture.Config; Find<ComboBox>(window, "翻译方式").SelectedIndex = 0; window.SaveSettings();
                        Assert(fixture.Config.TranslationMode == "local" && fixture.Config.BaseUrl == before.BaseUrl && fixture.Config.Model == before.Model && fixture.Config.ServiceId == before.ServiceId);
                        Assert(fixture.Credentials.Read(fixture.Config) == "fake-existing-secret");
                        Find<ComboBox>(window, "翻译方式").SelectedIndex = 1; await Idle(); window.SaveSettings();
                        Assert(fixture.Config.TranslationMode == "online" && fixture.Credentials.Read(fixture.Config) == "fake-existing-secret");
                    });
                    await Check("Closing settings does not dispose a provider shared with the application", async () =>
                    {
                        var provider = new SharedConnectionProvider(); using var fixture = new EditorFixture(root, "shared-provider", provider);
                        fixture.Window.Show(); await Idle(); fixture.Window.Close(); Assert(!provider.Disposed); provider.Dispose();
                    });
                    await Check("Model inputs debounce into one request for the latest address and explicit key", async () =>
                    {
                        var catalog = new RecordingModelCatalog(); using var fixture = new EditorFixture(root, "model-auto", catalog: catalog);
                        var window = fixture.Window; window.Show(); await Idle();
                        FindButton(window, "更换密钥").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Find<TextBox>(window, "Base URL").Text = "https://first.example.test/v1";
                        Find<PasswordBox>(window, "API Key").Password = "fake-new-key";
                        await Task.Delay(150); Find<TextBox>(window, "Base URL").Text = "https://last.example.test/custom/v1/";
                        await Until(() => catalog.Requests.Count == 1); await Idle();
                        Assert(catalog.Requests[0] == ("https://last.example.test/custom/v1", "fake-new-key"));
                        Assert(Find<ComboBox>(window, "模型名称").Items.Cast<string>().SequenceEqual(new[] { "available-a", "available-b" }));
                        Assert(Find<ComboBox>(window, "模型名称").Text == "original-model" && fixture.Commits == 0);
                        Assert(window.ModelStatusText.Contains("2 个模型"));
                    });
                    await Check("Model discovery never reuses a saved credential at a changed endpoint", async () =>
                    {
                        var catalog = new RecordingModelCatalog(); using var fixture = new EditorFixture(root, "model-key-binding", catalog: catalog);
                        var window = fixture.Window; window.Show();
                        Find<TextBox>(window, "Base URL").Text = "https://other.example.test/v1";
                        await window.RefreshModelsAsync(); Assert(catalog.Requests.Count == 0 && window.ModelStatusText.Contains("API Key"));
                        Find<TextBox>(window, "Base URL").Text = "http://unsafe.example.test/v1";
                        await window.RefreshModelsAsync(); Assert(catalog.Requests.Count == 0);
                        Find<TextBox>(window, "Base URL").Text = fixture.Config.BaseUrl + "/";
                        await window.RefreshModelsAsync(); Assert(catalog.Requests.Single().Item2 == "fake-existing-secret");
                    });
                    await Check("Late model discovery cannot replace the latest list or a manually entered model", async () =>
                    {
                        var old = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var catalog = new RecordingModelCatalog(index => index == 1 ? old.Task : Task.FromResult<IReadOnlyList<string>>(new[] { "new-list-model" }));
                        using var fixture = new EditorFixture(root, "model-stale", catalog: catalog); var window = fixture.Window; window.Show();
                        var firstRequest = window.RefreshModelsAsync(); await Until(() => catalog.Requests.Count == 1);
                        Find<TextBox>(window, "Base URL").Text = "https://new.example.test/v1";
                        FindButton(window, "更换密钥").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Find<PasswordBox>(window, "API Key").Password = "fake-new-key";
                        await window.RefreshModelsAsync(); var model = Find<ComboBox>(window, "模型名称"); model.Text = "typed-later-model";
                        old.TrySetResult(new[] { "stale-model" }); await firstRequest; await Idle();
                        Assert(model.Items.Cast<string>().SequenceEqual(new[] { "new-list-model" }) && model.Text == "typed-later-model");
                        Assert(fixture.Commits == 0);
                    });
                    await Check("Unsupported model APIs preserve manual input and allow explicit refresh", async () =>
                    {
                        var catalog = new RecordingModelCatalog(index => index == 1 ? Task.FromException<IReadOnlyList<string>>(new TranslationException("models-http", "该服务未提供模型列表接口；请手动填写模型名称。")) : Task.FromResult<IReadOnlyList<string>>(new[] { "refreshed-model" }));
                        using var fixture = new EditorFixture(root, "model-unsupported", catalog: catalog); var window = fixture.Window; window.Show();
                        await window.RefreshModelsAsync(); Assert(window.ModelStatusText.Contains("手动填写"));
                        var model = Find<ComboBox>(window, "模型名称"); model.Text = "custom-model";
                        await window.RefreshModelsAsync(); Assert(model.Text == "custom-model" && model.Items.Count == 1 && catalog.Requests.Count == 2);
                    });
                    await Check("Closing settings isolates late model errors", async () =>
                    {
                        var response = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
                        using var fixture = new EditorFixture(root, "model-close", catalog: new RecordingModelCatalog(_ => response.Task));
                        var window = fixture.Window; window.Show(); var request = window.RefreshModelsAsync(); string before = window.ModelStatusText;
                        window.Close(); response.TrySetException(new TranslationException("models-http", "late model error")); await request;
                        Assert(window.ModelStatusText == before);
                    });
                    await Check("Language selector defaults to Chinese and saves preset and custom input", async () =>
                    {
                        using var fixture = new EditorFixture(root, "languages"); var window = fixture.Window; window.Show(); await Idle();
                        await window.RefreshModelsAsync(); window.UpdateLayout();
                        var language = Find<ComboBox>(window, "目标语言"); Assert(language.Text == "简体中文" && language.Items.Count >= 20);
                        var languageBounds = language.TransformToAncestor(window).TransformBounds(new Rect(language.RenderSize));
                        var saveBounds = FindButton(window, "保存设置").TransformToAncestor(window).TransformBounds(new Rect(FindButton(window, "保存设置").RenderSize));
                        Assert(languageBounds.Top > 0 && languageBounds.Bottom < saveBounds.Top - 20);
                        language.SelectedItem = "日语（日本語）"; await Idle(); window.SaveSettings(); Assert(fixture.Config.TargetLanguage == "日语（日本語）");
                        var editor = (TextBox)language.Template.FindName("PART_EditableTextBox", language);
                        var modelEditor = (TextBox)Find<ComboBox>(window, "模型名称").Template.FindName("PART_EditableTextBox", Find<ComboBox>(window, "模型名称"));
                        Assert(editor.IsVisible && editor.MaxLength == 80 && modelEditor.MaxLength == 200);
                        editor.Text = "Esperanto"; await Idle(); window.SaveSettings(); Assert(fixture.Config.TargetLanguage == "Esperanto" && language.Text == "Esperanto");
                        language.IsDropDownOpen = true; await Idle(); Assert(((Popup)language.Template.FindName("PART_Popup", language)).IsOpen); language.IsDropDownOpen = false;
                    });
                    await Check("Compiled control templates preserve dropdown, selection and expansion contracts", async () =>
                    {
                        var combo = new ComboBox { ItemsSource = new[] { "first", "second" }, SelectedIndex = 1, Width = 220 };
                        var expander = new Expander { Header = "details", Content = new TextBlock { Text = "expanded content" } };
                        var panel = new StackPanel { Children = { combo, expander } };
                        var window = new Window { Content = panel, Width = 400, Height = 240 }; Ui.WindowTheme(window); window.Show(); await Idle();
                        combo.IsDropDownOpen = true; await Idle();
                        var dropdown = combo.Template.FindName("PART_Popup", combo) as Popup;
                        Assert(dropdown is { IsOpen: true } && dropdown.Child.IsVisible);
                        Assert(combo.ItemContainerGenerator.ContainerFromIndex(1) is ComboBoxItem { IsSelected: true });
                        combo.IsDropDownOpen = false;
                        var toggle = Walk(expander).OfType<ToggleButton>().Single(); toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, true); await Idle();
                        Assert(expander.IsExpanded && Walk(expander).OfType<TextBlock>().Any(t => t.Text == "expanded content" && t.IsVisible));
                        Theme.Apply("dark"); await Idle(); Assert(Equals(window.Background, app.Resources["Surface"])); Theme.Apply("light");
                    });
                    await Check("Production UIA transport reads a real selected WPF range without clipboard access", async () =>
                    {
                        const string content = "Production UIA fixture.\r\nSecond line 42.";
                        var text = new TextBox { Text = content, AcceptsReturn = true, Margin = new Thickness(24) };
                        var window = new Window { Title = "YiKe owned UIA integration fixture", Content = text, Width = 520, Height = 260 };
                        window.Show(); window.Activate(); text.Focus(); text.SelectAll(); await Idle();
                        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                        if (Native.GetForegroundWindow() != hwnd) throw new DesktopUnavailableException("The execution desktop did not allow the owned test window to become foreground.");
                        var glyph = text.GetRectFromCharacterIndex(0);
                        if (glyph.IsEmpty) throw new InvalidOperationException("The selected fixture text did not have a visible glyph rectangle.");
                        var point = text.PointToScreen(new Point(glyph.Left + 2, glyph.Top + glyph.Height / 2));
                        using var reader = new SelectionReader();
                        var result = await reader.ReadAsync(new Gesture(1, hwnd.ToInt64(), Environment.ProcessId, (int)point.X, (int)point.Y, (int)point.X, (int)point.Y, DateTimeOffset.UtcNow), default);
                        if (Native.GetForegroundWindow() != hwnd) throw new DesktopUnavailableException("The owned test window lost foreground during the UIA query.");
                        if (result.Status != ReadStatus.Success) throw new InvalidOperationException("Production UIA worker returned " + result.Status + " for the owned selected glyph.");
                        Assert(result.Text?.Replace("\r\n", "\n").TrimEnd('\r', '\n') == content.Replace("\r\n", "\n"));
                    });
                    await Check("An automatic popup preserves the foreground focus", async () =>
                    {
                        var anchor = new Window { Title = "YiKe synthetic focus anchor", Width = 400, Height = 180 }; anchor.Show(); anchor.Activate(); await Idle();
                        var popup = new PopupWindow(); IntPtr foreground = Native.GetForegroundWindow();
                        if (foreground == IntPtr.Zero) throw new DesktopUnavailableException("No foreground window is exposed to this execution desktop.");
                        popup.Begin("[synthetic focus regression]", new AppSettings(), 300, 250, foreground.ToInt64());
                        await Idle();
                        if (Native.GetForegroundWindow() != foreground) throw new InvalidOperationException("Automatic popup changed the foreground window.");
                        popup.Dismiss();
                    });
                    await Check("Presenting and updating a popup preserves the clipboard sequence", async () =>
                    {
                        uint clipboard = Native.GetClipboardSequenceNumber();
                        if (clipboard == 0) throw new DesktopUnavailableException("Clipboard sequence unavailable or uninitialized; no clipboard contents were read or changed.");
                        var popup = new PopupWindow(); popup.Begin("[synthetic clipboard regression]", new AppSettings(), 300, 250, 0); await Idle();
                        popup.Update(new(1, "[synthetic text]", true)); Assert(Native.GetClipboardSequenceNumber() == clipboard); popup.Dismiss();
                    });
                    await Check("Selection failure names the source, explains translation has not started and disables empty-source actions", async () =>
                    {
                        var popup = new PopupWindow(); popup.Begin("", new AppSettings(), 300, 250, 0);
                        popup.SelectionError(SelectionFeedback.Message(ReadStatus.Unsupported, "SampleBrowser")); await Idle();
                        var labels = Walk(popup).OfType<TextBlock>().Select(t => t.Text).ToArray();
                        Assert(labels.Contains("未能读取选区") && labels.Contains("原文未发送 · 翻译尚未开始"));
                        Assert(labels.Any(t => t.Contains("SampleBrowser")) && !labels.Any(t => t.Contains("VS Code")));
                        Assert(!FindButton(popup,"查看原文").IsEnabled && !FindButton(popup, "复制译文").IsEnabled);
                        Assert(!FindButton(popup, "重试（会再次调用服务）").IsEnabled); popup.Dismiss();
                    });
                    await Check("Programming preference saves, reloads and imports independently from credentials", async () =>
                    {
                        using var fixture = new EditorFixture(root, "programming-preference"); var window = fixture.Window; window.Show(); await Idle();
                        var box = Find<CheckBox>(window, "计算机用法解释"); Assert(box.IsChecked == false);
                        var smart = Find<CheckBox>(window, "模型补充解释"); Assert(smart.IsChecked == false && !smart.IsEnabled);
                        box.IsChecked = false; window.SaveSettings(); Assert(!fixture.Config.ExplainProgrammingTerms && box.IsChecked == false);
                        Assert(!smart.IsEnabled);
                        window.ApplyImported(fixture.Config with { ExplainProgrammingTerms = true }, null); await Idle();
                        Assert(box.IsChecked == true && fixture.Credentials.Read(fixture.Config) == "fake-existing-secret");
                        smart.IsChecked = false; window.SaveSettings(); Assert(fixture.Config.ExplainProgrammingTerms && !fixture.Config.SupplementTechnicalTerms);
                    });
                    await Check("Programming panel scrolls separately from source and resets between selections", async () =>
                    {
                        var popup = new PopupWindow(); popup.Begin("static", new AppSettings(), 300, 250, 0); await Idle();
                        var notes = Find<Expander>(popup, "计算机用法解释"); Assert(notes.Visibility == Visibility.Collapsed);
                        string definition = ProgrammingGlossary.Explain("static");
                        popup.Update(new(1, "静态", true, ProgrammingNotes: definition)); await Idle();
                        Assert(notes.IsExpanded && notes.IsVisible && ((TextBlock)notes.Content).Text == definition && popup.ActualHeight <= popup.MaxHeight);
                        Assert(((TextBlock)notes.Content).Text.Contains("示例：") && ((TextBlock)notes.Content).Text.Contains("效果："));
                        var bodyScroll = Walk(popup).OfType<ScrollViewer>().Single(view => view.Content is StackPanel panel && panel.Children.Contains(notes));
                        bodyScroll.ScrollToEnd(); await Idle(); Assert(bodyScroll.VerticalOffset > 0);
                        popup.Begin("static", new AppSettings(), 300, 250, 0); popup.Update(new(2, "静态", true, ProgrammingNotes: definition)); await Idle();
                        Assert(bodyScroll.VerticalOffset == 0);
                        notes.IsExpanded = false; popup.Update(new(1, "静态", true, Cached: true, ProgrammingNotes: definition)); Assert(!notes.IsExpanded);
                        popup.Begin("ordinary sentence", new AppSettings(), 300, 250, 0); await Idle(); Assert(notes.Visibility == Visibility.Collapsed && ((TextBlock)notes.Content).Text == "" && bodyScroll.VerticalOffset == 0);
                        popup.Update(new(2, "普通句子", true)); Assert(notes.Visibility == Visibility.Collapsed);
                    });
                    await Check("Unselected pointer activity stays hidden and hiding for re-selection does not signal an explicit close", async () =>
                    {
                        var popup = new PopupWindow(); int closes = 0; popup.Dismissed += () => closes++;
                        popup.Begin("old source", new AppSettings(), 300, 250, 0); popup.Update(new(1, "旧译文", true)); await Idle();
                        popup.HideForSelection(); Assert(!popup.IsVisible && closes == 0);
                        foreach (var status in new[] { ReadStatus.NoSelection, ReadStatus.Unsupported, ReadStatus.Timeout, ReadStatus.AccessDenied, ReadStatus.TargetGone, ReadStatus.NotTextSelection })
                        {
                            Assert(AutomaticSelectionPolicy.Decide(new(status)) == AutomaticSelectionAction.Silent);
                            await Idle(); Assert(!popup.IsVisible && closes == 0);
                        }
                        popup.Begin("new selection", new AppSettings(), 300, 250, 0); popup.Update(new(2, "新译文", true)); await Idle();
                        Assert(popup.IsVisible && Walk(popup).OfType<TextBlock>().Any(block => block.Text == "新译文"));
                        popup.Dismiss(); Assert(closes == 1);
                    });
                    await Check("Completed translation remains copyable while any-word notes are pending", async () =>
                    {
                        var popup = new PopupWindow(); popup.Begin("how", new AppSettings(), 300, 250, 0); await Idle();
                        popup.Update(new(1, "如何", true, ProgrammingNotes: ComputingWordNotes.Explain("how"), ExplanationPending: true)); await Idle();
                        var copy = FindButton(popup,"复制译文");
                        Assert(copy.IsEnabled && Walk(popup).OfType<TextBlock>().Any(block => block.Text == "如何"));
                        Assert(Walk(popup).OfType<TextBlock>().Any(block => block.Text.Contains("译文已完成") && block.Text.Contains("正在补充")));
                        Assert(Find<Expander>(popup, "计算机用法解释").IsVisible);
                        popup.Update(new(1, "如何", true, ProgrammingNotes: ComputingWordNotes.Explain("how") + "\n模型说明暂不可用"));
                        Assert(copy.IsEnabled && Walk(popup).OfType<TextBlock>().Any(block => block.Text == "翻译完成"));
                    });
                    await Check("Popup pin and close events invalidate selection leases without reviving late text", async () =>
                    {
                        var state = new SessionState(true); var popup = new PopupWindow();
                        popup.PinChanged += state.SetPinned; popup.Dismissed += state.Close;
                        long lease = state.BeginSelection(); popup.Begin("fixture", new AppSettings(), 300, 250, 0); await Idle();
                        popup.Update(new(1, "partial")); popup.SetPinned(true); Assert(state.Pinned && !state.Accepts(lease));
                        popup.Left += 45; popup.Top += 25; var moved = new Point(popup.Left,popup.Top);
                        popup.Update(new(1,"更长的译文",true)); await Idle();
                        Assert(Math.Abs(popup.Left-moved.X)<1 && Math.Abs(popup.Top-moved.Y)<1);
                        popup.SetPinned(false); Assert(!state.Accepts(lease)); popup.Dismiss(); popup.Update(new(1, "late completion", true));
                        Assert(!popup.IsVisible && !state.Pinned && state.Current is null);
                    });
                }
                finally
                {
                    Console.WriteLine($"UI RESULT {passed} passed, {failed} failed, {skipped} skipped");
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                    app.Shutdown();
                }
            };
            app.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); return failed == 0 ? 0 : 1;
    }
    static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Walk(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    static T Find<T>(Window window, string name) where T : DependencyObject
    { window.UpdateLayout(); return Walk(window).OfType<T>().Single(control => AutomationProperties.GetName(control) == name); }
    static Button FindButton(Window window, string name)
    { window.UpdateLayout(); return Walk(window).OfType<Button>().Single(button => AutomationProperties.GetName(button) == name); }
    static void Assert(bool condition, string message = "UI regression assertion failed") { if (!condition) throw new Exception(message); }
    static async Task Idle() => await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    static async Task Until(Func<bool> predicate)
    { var limit = DateTime.UtcNow.AddSeconds(3); while (!predicate()) { if (DateTime.UtcNow >= limit) throw new TimeoutException("UI result was not delivered"); await Task.Delay(20); } }

    sealed class EditorFixture : IDisposable
    {
        public AppSettings Config;
        public readonly SettingsStore Store;
        public readonly CredentialStore Credentials;
        public readonly SettingsWindow Window;
        public string? LastCommittedSecret; public int Commits;
        public EditorFixture(string root, string name, ITranslationProvider? provider = null, IModelCatalog? catalog = null)
        {
            Store = new SettingsStore(Path.Combine(root, name, "app"), Path.Combine(root, name, "data")); Credentials = new(Store.UserDirectory);
            Config = ConfigurationService.Commit(new AppSettings(), new AppSettings { TranslationMode = "online", Model = "original-model" }, "fake-existing-secret", Store, Credentials);
            Window = new(() => Config, (draft, key) => { Config = ConfigurationService.Commit(Config, draft, key, Store, Credentials); LastCommittedSecret = key; Commits++; }, Store, Credentials, () => "synthetic QA", testProvider: provider ?? new RecordingConnectionProvider(), modelCatalog: catalog ?? new RecordingModelCatalog(), localHardware: () => new(16 * LocalHardware.GiB, 8 * LocalHardware.GiB, 8, true));
        }
        public void Dispose() { if (Window.IsLoaded) Window.Close(); }
    }
}
sealed class DesktopUnavailableException(string message) : Exception(message);
sealed class FakePdfCopySource : IPdfCopySource
{
    public uint Sequence { get; set; } = 20;
    public long Now { get; set; }
    public bool Current = true;
    public int Reads;
    public int Copies;
    public Func<Func<bool>,Task<SelectionReadResult?>>? AutoReply;
    public Func<int, Task<SelectionReadResult?>>? Reply;
    public string? Capture(Gesture gesture) => Current ? "fixture.pdf - WPS Office" : null;
    public bool IsCurrent(Gesture gesture, string title) => Current;
    public Task<SelectionReadResult?> Read(Gesture gesture, string title, uint baseline, uint sequence)
    { Reads++; return Reply?.Invoke(Reads) ?? Task.FromResult<SelectionReadResult?>(new(ReadStatus.Success, "new PDF selection")); }
    public Task<SelectionReadResult?> Copy(Gesture gesture,string title,Func<bool> active,IntPtr owner) {Copies++;return AutoReply?.Invoke(active) ?? Task.FromResult<SelectionReadResult?>(null);}
}

sealed class BlockingConnectionProvider(bool fail = false) : ITranslationProvider
{
    public int Calls; public readonly List<string> Keys = new();
    public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    { Interlocked.Increment(ref Calls); Keys.Add(key); Started.TrySetResult(); await Release.Task; if (fail) throw new TranslationException("authentication", "late synthetic error"); yield return "synthetic output"; }
}
sealed class RecordingConnectionProvider : ITranslationProvider
{
    public bool CanExplainAnyWord(AppSettings settings) => true;
    public int Calls; public readonly List<string> Texts = new(); public readonly List<string> Keys = new();
    public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    { Interlocked.Increment(ref Calls); Texts.Add(request.Text); Keys.Add(key); await Task.Yield(); token.ThrowIfCancellationRequested(); yield return "synthetic output"; }
}

sealed class SharedConnectionProvider : ITranslationProvider, IDisposable
{
    public bool Disposed;
    public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    { await Task.Yield(); token.ThrowIfCancellationRequested(); yield return "fixture"; }
    public void Dispose() => Disposed = true;
}

sealed class RecordingModelCatalog(Func<int, Task<IReadOnlyList<string>>>? respond = null) : IModelCatalog
{
    public readonly List<(string, string)> Requests = new();
    public Task<IReadOnlyList<string>> ListAsync(string baseUrl, string key, CancellationToken token)
    {
        Requests.Add((baseUrl, key));
        return respond?.Invoke(Requests.Count) ?? Task.FromResult<IReadOnlyList<string>>(new[] { "available-a", "available-b" });
    }
}
