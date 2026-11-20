using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using YiKe.Data;
using YiKe.Translation;

namespace YiKe.UI;

internal sealed class StudyBook : Window
{
    readonly Func<string> baseFile;
    readonly StudyDictionaryPack[] packs;
    StudyDictionaryPack pack => packs[language.SelectedIndex-1];
    readonly ComboBox language = new();
    bool UsesLanguagePack => language.SelectedIndex > 0;
    internal static string JapaneseFile(string path) => Path.Combine(Path.GetDirectoryName(path)!, "YiKe-vocabulary-ja.json");
    internal static string LanguageFile(string path,string code) => Path.Combine(Path.GetDirectoryName(path)!, $"YiKe-vocabulary-{code}.json");
    string FilePath => UsesLanguagePack ? LanguageFile(baseFile(),pack.Code) : baseFile();
    string? Details(string word) => UsesLanguagePack ? pack.Find(word)?.Card(word) : BundledEnglishDictionary.Details(word);
    readonly ListBox words = new();
    readonly TextBlock definition = Ui.Text("", 15);
    readonly TextBlock status = Ui.Text("", 12, "Muted");
    internal static List<string> Read(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<List<string>>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("收藏本内容为空。")
        : new();
    internal static bool Contains(string basePath,string code,string word)
    {
        try { return Read(code == "en" ? basePath : LanguageFile(basePath,code)).Contains(word,StringComparer.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException) { return false; }
    }
    static void Save(string path, List<string> entries) => AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(entries));
    internal static string Add(string path, string word)
    {
        try
        {
            var entries = Read(path);
            if (entries.Contains(word, StringComparer.OrdinalIgnoreCase)) return "已在收藏本中。";
            entries.Add(word.Trim().ToLowerInvariant()); Save(path, entries); return "已收藏。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { return "无法保存收藏，请检查收藏本文件和目录权限。"; }
    }
    internal StudyBook(Func<string> file, Action<string> changeFolder, StudyDictionaryPack[] packs,YiKe.Core.AppSettings settings)
    {
        this.baseFile = file; this.packs = packs; Title = "单词收藏本"; Width = 660; Height = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Ui.WindowTheme(this); Ui.ConstrainToWorkArea(this);
        var root = new DockPanel { Margin = new Thickness(18) };
        var buttons = new WrapPanel();
        void Button(string label, Action action) => buttons.Children.Add(Ui.Button(label, () => Run(action)));
        var tools = new StackPanel(); DockPanel.SetDock(tools,Dock.Top); root.Children.Add(tools);
        language.Items.Add("英语收藏本"); foreach(var item in packs) language.Items.Add(item.Name+"收藏本"); language.SelectedIndex = 0; tools.Children.Add(language);
        var japaneseTools = new StackPanel { Visibility = Visibility.Collapsed }; tools.Children.Add(japaneseTools);
        language.SelectionChanged += (_,_) => { japaneseTools.Visibility = UsesLanguagePack ? Visibility.Visible : Visibility.Collapsed; Run(Reload); };
        var query = new TextBox { ToolTip = "输入单词或词组，检索所有已安装词典" };
        var search = new StackPanel(); tools.Children.Insert(0,search); search.Children.Add(Ui.Text("查词")); search.Children.Add(query);
        var card = new DictionaryCard { IsSaved = (code,word) => Contains(baseFile(),code,word) };
        var lookupResult = new ScrollViewer { Content = card, MaxHeight = 148, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var activePacks = StudyDictionaryPack.Selected(packs,settings);
        search.Children.Add(Ui.Button("查词", () => status.Text = card.Lookup(query.Text,activePacks) ? "" : "当前词典未收录。")); search.Children.Add(lookupResult);
        card.SaveWord += (code,word) =>
        {
            var message = Add(code == "en" ? baseFile() : LanguageFile(baseFile(),code),word);
            language.SelectedIndex = code == "en" ? 0 : Array.FindIndex(packs,p => p.Code == code)+1;
            Run(Reload); return message;
        };
        Closed += (_,_) => card.DisposeSpeech();
        var lookup = new WrapPanel(); japaneseTools.Children.Add(lookup);
        var installers = new StackPanel(); foreach(var item in packs) installers.Children.Add(InstallButton(item,status));
        language.SelectionChanged += (_,_) => { for(int i=0;i<packs.Length;i++) installers.Children[i].Visibility = i+1==language.SelectedIndex ? Visibility.Visible : Visibility.Collapsed; };
        lookup.Children.Add(installers);        lookup.Children.Add(Ui.Button("从文件安装", () => Run(() =>
        {
            var dialog = new OpenFileDialog { Filter = "离线词典 (*.ykdict;*.json)|*.ykdict;*.json" };
            if (dialog.ShowDialog(this) == true) { if (Path.GetExtension(dialog.FileName).Equals(".ykdict",StringComparison.OrdinalIgnoreCase)) pack.InstallDictionary(dialog.FileName); else pack.Install(File.ReadAllBytes(dialog.FileName)); status.Text = "词包已安装，可以离线查词。"; }
        })));
        var address = new TextBox { ToolTip = "HTTPS 词包直链" };
        japaneseTools.Children.Add(Ui.Text("从网址安装（HTTPS 词包直链）")); japaneseTools.Children.Add(address);
        var download = new Button { Content = "下载并安装词包" }; japaneseTools.Children.Add(download);
        download.Click += async (_,_) =>
        {
            download.IsEnabled = false; status.Text = "正在下载词包…";
            try { await pack.Download(address.Text); status.Text = "词包已安装，可以离线查词。"; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or System.Net.Http.HttpRequestException or OperationCanceledException)
            { status.Text = "安装失败：" + ex.Message; }
            finally { download.IsEnabled = true; }
        };
        Button("显示释义", () => { if (words.SelectedItem is string word) definition.Text = Details(word) ?? "本地词库未收录。"; });
        Button("删除所选", () =>
        {
            if (words.SelectedItem is not string word) return;
            var entries = Read(FilePath); entries.Remove(word); Save(FilePath, entries); Reload();
        });
        Button("导出文本", () =>
        {
            var dialog = new SaveFileDialog { FileName = UsesLanguagePack ? "译刻"+pack.Name+"收藏本.txt" : "译刻英语收藏本.txt", Filter = "文本文件|*.txt" };
            if (dialog.ShowDialog(this) != true) return;
            AtomicFile.Write(dialog.FileName, Encoding.UTF8.GetBytes(string.Join("\n\n", Read(FilePath).Select(w => Details(w) ?? w))));
            status.Text = "已导出到 " + dialog.FileName;
        });
        Button("更改保存位置", () =>
        {
            var dialog = new OpenFolderDialog { Title = "选择收藏本目录" };
            if (dialog.ShowDialog(this) != true) return;
            var previous = baseFile(); var next = Path.Combine(dialog.FolderName, Path.GetFileName(previous));
            if (string.Equals(previous, next, StringComparison.OrdinalIgnoreCase)) return;
            foreach (var (source, target) in packs.Select(p => (LanguageFile(previous,p.Code),LanguageFile(next,p.Code))).Prepend((previous,next)))
                if (File.Exists(source)) Save(target, Read(target).Union(Read(source),StringComparer.OrdinalIgnoreCase).ToList()); changeFolder(dialog.FolderName); Reload();
            status.Text = "现保存到 " + next + "。原目录保留副本。";
        });
        DockPanel.SetDock(buttons,Dock.Top); root.Children.Add(buttons);
        status.TextWrapping = TextWrapping.Wrap; DockPanel.SetDock(status,Dock.Bottom); root.Children.Add(status);
        words.Width = 150; DockPanel.SetDock(words,Dock.Left); root.Children.Add(words);
        definition.Margin = new Thickness(16); definition.TextWrapping = TextWrapping.Wrap;
        root.Children.Add(new ScrollViewer { Content = definition, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        words.SelectionChanged += (_,_) => definition.Text = words.SelectedItem is string word ? word + "\n\n先回想词义，再点「显示释义」。" : "";
        Content = root; Run(Reload);
    }
    internal static Button InstallButton(StudyDictionaryPack pack, TextBlock status,bool compact = false)
    {
        string Label() => compact ? (pack.Count > 0 ? "已安装" : "安装") : pack.InstallLabel;
        var starter = new Button { Content = Label() };
        if (compact)
        {
            starter.SetResourceReference(StyleProperty,"GlyphAction"); starter.Width = 46; starter.Height = 24; starter.FontSize = 12; starter.SetResourceReference(Control.ForegroundProperty,"Accent");
            starter.ToolTip = pack.Name+"词典"; System.Windows.Automation.AutomationProperties.SetName(starter,"安装"+pack.Name+"词典");
        }
        starter.IsEnabled = pack.Count == 0;
        starter.Click += async (_,_) =>
        {
            starter.IsEnabled = false;
            try
            {
                string catalog = Path.Combine(AppContext.BaseDirectory, pack.Code+"-starter-source.txt");
                if (!File.Exists(catalog)) { status.Text = "此构建尚未配置该词包来源。"; return; }
                string location = (await File.ReadAllTextAsync(catalog)).Trim();
                if (Path.IsPathFullyQualified(location)) await System.Threading.Tasks.Task.Run(() => pack.InstallDictionary(location));
                else await pack.Download(location);
                starter.Content = Label(); status.Text = "已安装，可以离线查词。";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or System.Net.Http.HttpRequestException or OperationCanceledException)
            { status.Text = "安装失败：" + ex.Message; }
            finally { starter.IsEnabled = pack.Count == 0; }
        };
        starter.IsVisibleChanged += (_,_) => { if (pack.Count > 0) { starter.Content = Label(); starter.IsEnabled = false; } };
        return starter;
    }
    void Reload()
    {
        words.ItemsSource = Read(FilePath); words.SelectedIndex = 0;
        status.Text = words.Items.Count + " 个单词 · " + FilePath;
    }
    void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { status.Text = "操作失败，请检查收藏本格式和目录权限。"; }
    }
}
