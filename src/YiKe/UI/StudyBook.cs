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
    readonly Func<string> file;
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
    internal StudyBook(Func<string> file, Action<string> changeFolder)
    {
        this.file = file; Title = "英语收藏本"; Width = 660; Height = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Ui.WindowTheme(this); Ui.ConstrainToWorkArea(this);
        var root = new DockPanel { Margin = new Thickness(18) };
        var buttons = new WrapPanel();
        void Button(string label, Action action) => buttons.Children.Add(Ui.Button(label, () => Run(action)));
        Button("显示释义", () => { if (words.SelectedItem is string word) definition.Text = BundledEnglishDictionary.Details(word) ?? "本地词库未收录。"; });
        Button("删除所选", () =>
        {
            if (words.SelectedItem is not string word) return;
            var entries = Read(file()); entries.Remove(word); Save(file(), entries); Reload();
        });
        Button("导出文本", () =>
        {
            var dialog = new SaveFileDialog { FileName = "译刻英语收藏本.txt", Filter = "文本文件|*.txt" };
            if (dialog.ShowDialog(this) != true) return;
            AtomicFile.Write(dialog.FileName, Encoding.UTF8.GetBytes(string.Join("\n\n", Read(file()).Select(w => BundledEnglishDictionary.Details(w) ?? w))));
            status.Text = "已导出到 " + dialog.FileName;
        });
        Button("更改保存位置", () =>
        {
            var dialog = new OpenFolderDialog { Title = "选择收藏本目录" };
            if (dialog.ShowDialog(this) != true) return;
            var previous = file(); var next = Path.Combine(dialog.FolderName, Path.GetFileName(previous));
            if (string.Equals(previous, next, StringComparison.OrdinalIgnoreCase)) return;
            var merged = Read(next).Union(Read(previous), StringComparer.OrdinalIgnoreCase).ToList();
            Save(next, merged); changeFolder(dialog.FolderName); Reload();
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
    void Reload()
    {
        words.ItemsSource = Read(file()); words.SelectedIndex = 0;
        status.Text = words.Items.Count + " 个单词 · " + file();
    }
    void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { status.Text = "操作失败，请检查收藏本格式和目录权限。"; }
    }
}
