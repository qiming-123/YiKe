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
    readonly JapanesePack[] packs;
    JapanesePack pack => packs[language.SelectedIndex-1];
    readonly ComboBox language = new();
    bool IsJapanese => language.SelectedIndex > 0;
    internal static string JapaneseFile(string path) => Path.Combine(Path.GetDirectoryName(path)!, "YiKe-vocabulary-ja.json");
    internal static string LanguageFile(string path,string code) => Path.Combine(Path.GetDirectoryName(path)!, $"YiKe-vocabulary-{code}.json");
    string FilePath => IsJapanese ? LanguageFile(baseFile(),pack.Code) : baseFile();
    string? Details(string word) => IsJapanese ? pack.Find(word)?.Card(word) : BundledEnglishDictionary.Details(word);
    readonly ListBox words = new();
    readonly TextBlock definition = Ui.Text("", 15);
    readonly TextBlock status = Ui.Text("", 12, "Muted");
    internal static List<string> Read(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<List<string>>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("收藏本内容为空。")
        : new();
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
    internal StudyBook(Func<string> file, Action<string> changeFolder, JapanesePack[] packs)
    {
        this.baseFile = file; this.packs = packs; Title = "研习收藏本"; Width = 660; Height = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Ui.WindowTheme(this); Ui.ConstrainToWorkArea(this);
        var root = new DockPanel { Margin = new Thickness(18) };
        var buttons = new WrapPanel();
        void Button(string label, Action action) => buttons.Children.Add(Ui.Button(label, () => Run(action)));
        var tools = new StackPanel(); DockPanel.SetDock(tools,Dock.Top); root.Children.Add(tools);
        language.Items.Add("英语收藏本"); foreach(var item in packs) language.Items.Add(item.Name+"收藏本"); language.SelectedIndex = 0; tools.Children.Add(language);
        var japaneseTools = new StackPanel { Visibility = Visibility.Collapsed }; tools.Children.Add(japaneseTools);
        language.SelectionChanged += (_,_) => { japaneseTools.Visibility = IsJapanese ? Visibility.Visible : Visibility.Collapsed; Run(Reload); };
        var query = new TextBox { ToolTip = "输入单词或词形" }; japaneseTools.Children.Add(Ui.Text("词包查词")); japaneseTools.Children.Add(query);
        var lookup = new WrapPanel(); japaneseTools.Children.Add(lookup);
        lookup.Children.Add(Ui.Button("查词", () => definition.Text = pack.Find(query.Text)?.Card(query.Text.Trim()) ?? (pack.Installed ? "试用词包未收录。" : "请先安装对应词包。")));
        lookup.Children.Add(Ui.Button("收藏此词", () => status.Text = pack.Find(query.Text) is null ? "请先查询已收录的词形。" : SaveSelected()));
        string SaveSelected() { var message = Add(FilePath,query.Text); Reload(); return message; }
        var installers = new StackPanel(); foreach(var item in packs) installers.Children.Add(InstallButton(item,status));
        language.SelectionChanged += (_,_) => { for(int i=0;i<packs.Length;i++) installers.Children[i].Visibility = i+1==language.SelectedIndex ? Visibility.Visible : Visibility.Collapsed; };
        lookup.Children.Insert(0,installers);        lookup.Children.Add(Ui.Button("从文件安装", () => Run(() =>
        {
            var dialog = new OpenFileDialog { Filter = "研习词包 (*.json)|*.json" };
            if (dialog.ShowDialog(this) == true) { pack.Install(File.ReadAllBytes(dialog.FileName)); status.Text = "词包已安装，可以离线查词。"; }
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
            var dialog = new SaveFileDialog { FileName = IsJapanese ? "译刻"+pack.Name+"收藏本.txt" : "译刻英语收藏本.txt", Filter = "文本文件|*.txt" };
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
    internal static Button InstallButton(JapanesePack pack, TextBlock status)
    {
        var starter = new Button { Content = pack.Installed ? "已安装"+pack.Name+"词包" : "一键安装"+pack.Name+"试用包" };
        starter.IsEnabled = !pack.Installed;
        starter.Click += async (_,_) =>
        {
            starter.IsEnabled = false;
            try
            {
                string catalog = Path.Combine(AppContext.BaseDirectory, pack.Code+"-starter-source.txt");
                if (!File.Exists(catalog)) { status.Text = "此构建尚未配置该词包来源。"; return; }
                string location = (await File.ReadAllTextAsync(catalog)).Trim();
                if (Path.IsPathFullyQualified(location)) pack.Install(await File.ReadAllBytesAsync(location));
                else await pack.Download(location);
                starter.Content = "已安装"+pack.Name+"词包"; status.Text = "已安装，可以离线查词。";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or System.Net.Http.HttpRequestException or OperationCanceledException)
            { status.Text = "安装失败：" + ex.Message; }
            finally { starter.IsEnabled = !pack.Installed; }
        };
        starter.IsVisibleChanged += (_,_) => { if (pack.Installed) { starter.Content = "已安装"+pack.Name+"词包"; starter.IsEnabled = false; } };
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
