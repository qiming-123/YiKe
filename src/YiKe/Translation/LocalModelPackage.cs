using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace YiKe.Translation;

public sealed record LocalPackageFile(string Path, long Bytes, string Sha256);
public sealed record LocalPackageManifest(string Model, string Engine, LocalPackageFile[] Files);

public static class LocalModelPackage
{
    public const string ModelId = "Qwen3-1.7B-Q4_K_M";
    public const string ModelFile = "models/Qwen3-1.7B-Q4_K_M.gguf";
    public const string LightModelId = "Qwen3-0.6B-Q4_0";
    public const string LightModelFile = "models/Qwen3-0.6B-Q4_0.gguf";
    public static string DirectoryPath => Path.Combine(AppContext.BaseDirectory, "local-runtime");
    public static bool IsPresent => File.Exists(Path.Combine(DirectoryPath, "engine", "llama-server.exe")) && LocalInstallation.HasAnyModel();
    public static LocalPackageManifest Manifest()
    {
        using var stream = typeof(LocalModelPackage).Assembly.GetManifestResourceStream("YiKe.local-package")!;
        return JsonSerializer.Deserialize<LocalPackageManifest>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
    public static async Task VerifyAsync(string root, LocalPackageManifest manifest, CancellationToken token)
    {
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (manifest.Files is null || manifest.Files.Length < 1 || manifest.Files.Length > 100) throw Invalid();
        foreach (var file in manifest.Files)
        {
            token.ThrowIfCancellationRequested();
            if (file.Path is null || file.Path.Contains(':') || file.Path.Split('/', '\\').Any(part => part is ".." or "." or "")) throw Invalid();
            string path = Path.GetFullPath(Path.Combine(root, file.Path));
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(path) || new FileInfo(path).Length != file.Bytes) throw Invalid();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            string hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase)) throw Invalid();
        }
        string engine = Path.Combine(root, "engine");
        if (Directory.Exists(engine)) foreach (string path in Directory.EnumerateFiles(engine))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!manifest.Files.Any(file => string.Equals(file.Path, relative, StringComparison.OrdinalIgnoreCase))) throw Invalid();
        }
    }
    public static TranslationException Invalid() => new("local-files", "本地运行文件缺失或校验失败，请重新运行安装包选择对应模型进行修复；也可完整解压离线便携包。");
}
