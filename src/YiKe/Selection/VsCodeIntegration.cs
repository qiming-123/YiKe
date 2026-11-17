using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YiKe.Selection;

public static class VsCodeIntegration
{
    public static void ExportPackage(string path)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);
        foreach (string name in new[] { "package.json", "extension.js", "selection.js", "README.md", "LICENSE" })
        {
            using var input = typeof(VsCodeIntegration).Assembly.GetManifestResourceStream("YiKe.VSCode." + name) ?? throw new IOException("缺少 VS Code 适配资源。");
            using var output = zip.CreateEntry("extension/" + name).Open(); input.CopyTo(output);
        }
        Write("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"utf-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"json\" ContentType=\"application/json\"/><Default Extension=\"js\" ContentType=\"application/javascript\"/><Default Extension=\"md\" ContentType=\"text/markdown\"/><Default Extension=\"vsixmanifest\" ContentType=\"text/xml\"/><Default Extension=\"\" ContentType=\"text/plain\"/></Types>");
        Write("extension.vsixmanifest", "<?xml version=\"1.0\" encoding=\"utf-8\"?><PackageManifest Version=\"2.0.0\" xmlns=\"http://schemas.microsoft.com/developer/vsx-schema/2011\"><Metadata><Identity Language=\"en-US\" Id=\"yike-selection\" Version=\"1.1.0\" Publisher=\"yike-local\"/><DisplayName>译刻 · VS Code 划词适配</DisplayName><Description xml:space=\"preserve\">本机选区适配</Description><Tags/><Categories>Other</Categories><GalleryFlags/><Properties><Property Id=\"Microsoft.VisualStudio.Code.Engine\" Value=\"^1.85.0\"/><Property Id=\"Microsoft.VisualStudio.Code.ExtensionKind\" Value=\"ui\"/></Properties><License>extension/LICENSE</License></Metadata><Installation><InstallationTarget Id=\"Microsoft.VisualStudio.Code\"/></Installation><Dependencies/><Assets><Asset Type=\"Microsoft.VisualStudio.Code.Manifest\" Path=\"extension/package.json\" Addressable=\"true\"/><Asset Type=\"Microsoft.VisualStudio.Services.Content.Details\" Path=\"extension/README.md\" Addressable=\"true\"/></Assets></PackageManifest>");
        void Write(string name, string text) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(text); }
    }
    public static async Task<string> InstallAsync()
    {
        string? executable = FindCode().FirstOrDefault(path => FindCli(path) is not null);
        if (executable is null) throw new IOException("未找到 VS Code 桌面版。启动 VS Code 后重试；网页版暂不支持。");
        string folder = Path.Combine(Path.GetTempPath(), "YiKe.VSCode." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder); string package = Path.Combine(folder, "yike-selection.vsix");
        try
        {
            ExportPackage(package);
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.Environment["ELECTRON_RUN_AS_NODE"] = "1";
            start.ArgumentList.Add(FindCli(executable)!); start.ArgumentList.Add("--install-extension"); start.ArgumentList.Add(package); start.ArgumentList.Add("--force");
            using var process = Process.Start(start) ?? throw new IOException("无法启动 VS Code 扩展安装程序。");
            // Drain without keeping logs or unbounded CLI output in memory.
            Task stdout = Drain(process.StandardOutput), stderr = Drain(process.StandardError);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { try { process.Kill(); } catch { } throw new IOException("VS Code 适配安装超时，请稍后重试。"); }
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            if (process.ExitCode != 0) throw new IOException("VS Code 适配安装失败，请检查扩展安装策略或关闭 VS Code 后重试。");
            return "VS Code 适配已安装。重新打开 VS Code 后，编辑区直接划词；Terminal 选中文字按 Ctrl+Alt+Y 复制并翻译 / 解释。";
        }
        finally { try { File.Delete(package); Directory.Delete(folder); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
    static async Task Drain(StreamReader reader) { var buffer = new char[1024]; while (await reader.ReadAsync(buffer).ConfigureAwait(false) != 0) { } }
    static string? FindCli(string executable)
    {
        string root = Path.GetDirectoryName(executable)!;
        string direct = Path.Combine(root, "resources", "app", "out", "cli.js");
        if (File.Exists(direct)) return direct;
        try { return Directory.EnumerateDirectories(root).Take(32).Select(dir => Path.Combine(dir, "resources", "app", "out", "cli.js")).FirstOrDefault(File.Exists); }
        catch (UnauthorizedAccessException) { return null; }
    }
    static IEnumerable<string> FindCode()
    {
        var paths = new List<string>();
        foreach (string parent in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            foreach (string product in new[] { "Microsoft VS Code", "Microsoft VS Code Insiders" })
                paths.Add(Path.Combine(parent, product, product.EndsWith("Insiders", StringComparison.Ordinal) ? "Code - Insiders.exe" : "Code.exe"));
        foreach (string name in new[] { "Code", "Code - Insiders" })
            foreach (var process in Process.GetProcessesByName(name))
                using (process) { try { if (process.MainModule?.FileName is string path) paths.Add(path); } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { } }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists);
    }
}
