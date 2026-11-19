using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YiKe.Core;

namespace YiKe.Data;

public static class AtomicFile
{
    public static void Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { using (var s = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { s.Write(bytes); s.Flush(true); } File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

public sealed class SettingsStore
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = false };
    public string UserDirectory { get; private set; }
    public string DefaultDirectory { get; }
    public string UserFile => Path.Combine(UserDirectory, "settings.json");
    public string? PortableFile { get; }
    public string? Notice { get; private set; }
    public bool Found { get; private set; }
    public SettingsStore(string? baseDirectory = null, string? userDirectory = null)
    {
        baseDirectory ??= AppContext.BaseDirectory;
        var rootFile = Path.Combine(baseDirectory, "yike-data-root.txt");
        DefaultDirectory = userDirectory ?? (File.Exists(rootFile) ? File.ReadAllText(rootFile).Trim() : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YiKe"));
        string pointer = Path.Combine(DefaultDirectory, "data-location.txt");
        UserDirectory = File.Exists(pointer) ? File.ReadAllText(pointer).Trim() : DefaultDirectory;
        if (File.Exists(Path.Combine(baseDirectory, "portable.flag"))) PortableFile = Path.Combine(baseDirectory, "data", "settings.json");
    }
    public void ChangeDirectory(string folder, AppSettings current)
    {
        if (string.Equals(folder, UserDirectory, StringComparison.OrdinalIgnoreCase)) return;
        var destination = Path.Combine(folder, "settings.json");
        if (File.Exists(destination)) throw new IOException("该目录已有设置，请选择空目录。");
        Directory.CreateDirectory(folder);
        foreach (var book in StudyDictionaryPack.Languages.Select(l => $"YiKe-vocabulary-{l.Code}.json").Prepend("YiKe-vocabulary.json"))
        {
        string sourceBook = Path.Combine(UserDirectory, book);
        if (string.IsNullOrEmpty(current.StudyDirectory) && File.Exists(sourceBook))
        {
            string nextBook = Path.Combine(folder, book);
            if (File.Exists(nextBook)) throw new IOException("目标目录已有收藏本，请选择空目录。");
            File.Copy(sourceBook, nextBook);
        }
        }
        AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(current, Json));
        AtomicFile.Write(Path.Combine(DefaultDirectory, "data-location.txt"), Encoding.UTF8.GetBytes(folder));
        UserDirectory = folder;
    }
    AppSettings? Read(string? path)
    {
        if (path is null || !File.Exists(path)) return null;
        try { if (new FileInfo(path).Length > 1048576) throw new InvalidDataException(); return JsonSerializer.Deserialize<AppSettings>(File.ReadAllBytes(path), Json)!.Validate(); }
        catch { Notice = "有一份配置无法读取；原文件已保留。可从数据页导入有效配置。"; return null; }
    }
    public AppSettings Load()
    {
        var local = Read(UserFile); var portable = Read(PortableFile);
        var chosen = portable is null ? local : local is not null && local.ConfigId == portable.ConfigId && local.Revision > portable.Revision ? local : portable;
        Found = chosen is not null;
        if (chosen is not null && portable is not null && ReferenceEquals(chosen, portable) && (local is null || local.ConfigId != chosen.ConfigId || local.Revision < chosen.Revision))
        {
            try { AtomicFile.Write(UserFile, JsonSerializer.SerializeToUtf8Bytes(chosen, Json)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Notice = "已加载便携配置，但用户目录不可写，尚未建立本机副本。"; }
        }
        return OnlineProfiles.Upgrade(chosen ?? new AppSettings());
    }
    public AppSettings Save(AppSettings value)
    {
        value = value.Validate() with { Revision = checked(value.Revision + 1) };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        AtomicFile.Write(UserFile, bytes); // Authoritative durable commit; never overwrite it with an older portable copy.
        Notice = null;
        if (PortableFile is not null) try { AtomicFile.Write(PortableFile, bytes); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var portable = Read(PortableFile);
            Notice = "便携目录不可写，已保存到 " + UserDirectory;
            if (portable is not null && portable.ConfigId != value.ConfigId) Notice += "。此目录仍包含另一份便携配置，下次启动会优先加载它；请将便携目录移至可写位置后再次导入。";
        }
        Found = true; return value;
    }
    public static void Export(string path, AppSettings value) => AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(value, Json));
    public static AppSettings Import(string path)
    {
        if (new FileInfo(path).Length > 1048576) throw new InvalidDataException("配置文件过大。");
        try { return OnlineProfiles.Rebind((JsonSerializer.Deserialize<AppSettings>(File.ReadAllBytes(path), Json) ?? throw new InvalidDataException("配置为空。")).Validate()); }
        catch (JsonException) { throw new InvalidDataException("配置文件的 JSON 格式无效；当前设置没有改变。"); }
    }
}

public sealed class CredentialStore
{
    readonly string directory;
    public CredentialStore(string userDirectory) => directory = Path.Combine(userDirectory, "credentials");
    string PathFor(AppSettings s) => Path.Combine(directory, s.ServiceId.ToString("N") + "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Endpoint.Normalize(s.BaseUrl))))[..16] + ".bin");
    public string? Read(AppSettings s)
    {
        string path = PathFor(s);
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 65536) throw new CryptographicException("本机凭据文件大小异常，请更换密钥或导入迁移包。");
        byte[] plain = Dpapi.Unprotect(File.ReadAllBytes(path));
        try { return ValidateSecret(Encoding.UTF8.GetString(plain)); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public static string ValidateSecret(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 8192 || key.Any(char.IsControl)) throw new ArgumentException("API Key 为空、过长或包含控制字符。");
        return key.Trim();
    }
    public void Save(AppSettings s, string key)
    {
        var plain = Encoding.UTF8.GetBytes(ValidateSecret(key));
        try { AtomicFile.Write(PathFor(s), Dpapi.Protect(plain)); } finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public void Delete(AppSettings s) { var p = PathFor(s); if (File.Exists(p)) File.Delete(p); }
}
