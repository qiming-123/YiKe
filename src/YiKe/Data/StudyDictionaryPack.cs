using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace YiKe.Data;

internal sealed record StudyDictionaryEntry(string Headword, string Reading, string Meaning, string Example, string[] Forms)
{
    internal string Card(string form) => form + (form == Headword ? "" : "  →  " + Headword) + "\n" + Reading + "\n\n" + Meaning + "\n\n" + Example;
}
internal sealed record StudyDictionaryData(string Language, StudyDictionaryEntry[] Entries);
internal readonly record struct StudyDownloadProgress(long ReceivedBytes, long? TotalBytes);

internal sealed class StudyDictionaryPack
{
    readonly string path;
    Dictionary<string,StudyDictionaryEntry> index = new(StringComparer.OrdinalIgnoreCase);
    bool binaryDamaged, jsonDamaged;
    string DictionaryPath => Path.ChangeExtension(path, ".ykdict");
    sealed record Source(string Code, string Version, string Url, string Sha256);
    static readonly Source[] Sources = ReadResource<Source[]>("YiKe.study-sources");
    static readonly Lazy<StudyDictionaryEntry[]> FrenchCorrections = new(() => ReadResource<StudyDictionaryData>("YiKe.fr-corrections").Entries);
    static T ReadResource<T>(string name)
    {
        using var resource = typeof(StudyDictionaryPack).Assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<T>(resource)!;
    }
    internal Task DownloadOfficial(IProgress<StudyDownloadProgress>? progress = null, CancellationToken token = default)
    {
        var source = Sources.Single(s => s.Code == Code);
        return Download(source.Url, source.Sha256, progress, token);
    }
    internal event Action? InstallationChanged;
    internal int Count { get; private set; }
    internal bool Installed => Count > 0 || index.Count > 0;
    internal bool NeedsRepair => binaryDamaged || jsonDamaged && Count == 0;
    internal string InstallLabel => NeedsRepair ? $"重新安装{Name}词典" : Installed ? $"已安装{Name}词典" : $"下载{Name}词典";
    static int ReadHeader(BinaryReader r, string code, out int keys)
    {
        if (System.Text.Encoding.ASCII.GetString(r.ReadBytes(10)) != "YIKELEX1"+code) throw new InvalidDataException("词典语言或格式不匹配。");
        keys = r.ReadInt32(); int count = r.ReadInt32();
        if (count <= 0 || keys <= 0 || 18L+keys*4L >= r.BaseStream.Length) throw new InvalidDataException("词典索引不完整。");
        return count;
    }
    internal void InstallDictionary(string source, CancellationToken token = default)
    {
        using var reader = new BinaryReader(File.OpenRead(source));
        int count = ReadHeader(reader,Code,out _);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = DictionaryPath+"."+Guid.NewGuid()+".tmp";
        try { File.Copy(source,tmp); token.ThrowIfCancellationRequested(); File.Move(tmp,DictionaryPath,true); Count=count; binaryDamaged=false; InstallationChanged?.Invoke(); } finally { File.Delete(tmp); }
    }
    internal static StudyDictionaryPack[] Selected(StudyDictionaryPack[] packs, YiKe.Core.AppSettings settings) =>
        settings.LearnJapanese ? Array.FindAll(packs,p => !settings.DisabledStudyLanguages.Contains(p.Code)) : Array.Empty<StudyDictionaryPack>();

    internal static readonly (string Code,string Name)[] Languages = { ("ja","日语"), ("ko","韩语"), ("fr","法语"), ("de","德语"), ("es","西班牙语") };
    internal string Code { get; }
    internal string Name { get; }
    internal StudyDictionaryPack(string directory,string code="ja",string name="日语")
    {
        Code = code; Name = name; path = Path.Combine(directory, "packs", code+".json");
        try { if (File.Exists(DictionaryPath)) { using var reader = new BinaryReader(File.OpenRead(DictionaryPath)); Count = ReadHeader(reader,Code,out _); } }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { binaryDamaged = true; }
        try { if (File.Exists(path)) index = Parse(File.ReadAllBytes(path)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException) { jsonDamaged = true; }
    }
    internal StudyDictionaryEntry? Find(string word)
    {
        word = word.Trim();
        if (Count == 0 || word.Length == 0 || word.Length > 256) return index.GetValueOrDefault(word);
        if (Code == "fr")
        {
            var entry = Array.Find(FrenchCorrections.Value, e => e.Headword.Equals(word,StringComparison.OrdinalIgnoreCase) || e.Forms.Contains(word,StringComparer.OrdinalIgnoreCase));
            if (entry != null) return entry;
        }
        try { return FindInDictionary(word) ?? index.GetValueOrDefault(word); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            Count = 0; binaryDamaged = true; InstallationChanged?.Invoke();
            return index.GetValueOrDefault(word);
        }
    }
    StudyDictionaryEntry? FindInDictionary(string word)
    {
        byte[] key = System.Text.Encoding.UTF8.GetBytes(word.Normalize().ToLowerInvariant());
        using var reader = new BinaryReader(File.OpenRead(DictionaryPath));
        ReadHeader(reader,Code,out int keys);
        long keyStart = 18L + 4L * keys, fileLength = reader.BaseStream.Length;
        int lo=0, hi=keys-1;
        while (lo <= hi)
        {
            int mid = lo+(hi-lo)/2; reader.BaseStream.Position=18L+4L*mid;
            long row = reader.ReadUInt32();
            if (row < keyStart || row > fileLength - 12) throw new InvalidDataException("词典索引偏移无效。");
            reader.BaseStream.Position=row;
            int length=reader.ReadInt32(); uint pos=reader.ReadUInt32(); int size=reader.ReadInt32();
            if (length <= 0 || row + 12L + length > fileLength || size <= 0 || pos < row + 12L + length || pos + (long)size > fileLength)
                throw new InvalidDataException("词典词条范围无效。");
            int cmp=key.AsSpan().SequenceCompareTo(reader.ReadBytes(length));
            if (cmp < 0) hi=mid-1;
            else if (cmp > 0) lo=mid+1;
            else
            {
                reader.BaseStream.Position=pos;
                var entry = JsonSerializer.Deserialize<StudyDictionaryEntry>(reader.ReadBytes(size));
                if (entry is not { Meaning: not null, Headword: not null, Reading: not null, Forms: not null, Example: not null })
                    throw new InvalidDataException("词典词条内容无效。");
                return entry with { Meaning = entry.Meaning.Replace("unknown · ", "") };
            }
        }
        return null;
    }
    Dictionary<string,StudyDictionaryEntry> Parse(byte[] bytes)
    {
        var pack = JsonSerializer.Deserialize<StudyDictionaryData>(bytes);
        if (pack is not { Entries.Length: > 0 } || pack.Language != Code) throw new InvalidDataException("请选择对应语言的研习词包。");
        var entries = new Dictionary<string,StudyDictionaryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in pack.Entries)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Headword) || string.IsNullOrWhiteSpace(item.Reading) || string.IsNullOrWhiteSpace(item.Meaning) || item.Forms is null)
                throw new InvalidDataException("词条缺少必要字段。");
            foreach (string form in item.Forms.Prepend(item.Headword).Distinct())
                if (string.IsNullOrWhiteSpace(form) || !entries.TryAdd(form,item)) throw new InvalidDataException("词包中存在空白或重复的词形。");
        }
        return entries;
    }
    internal void Install(byte[] bytes)
    {
        var parsed = Parse(bytes); AtomicFile.Write(path, bytes); index = parsed; jsonDamaged = false; InstallationChanged?.Invoke();
    }
    internal async Task Download(string address, string? expectedHash = null, IProgress<StudyDownloadProgress>? progress = null,
        CancellationToken token = default, HttpMessageHandler? handler = null)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new InvalidDataException("请输入 HTTPS 词包直链。");
        bool binary = uri.AbsolutePath.EndsWith(".ykdict", StringComparison.OrdinalIgnoreCase);
        using var duration = CancellationTokenSource.CreateLinkedTokenSource(token);
        if (!binary) duration.CancelAfter(TimeSpan.FromSeconds(30));
        token = duration.Token;
        using var client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(30);
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (!binary)
        {
            using var buffer = new MemoryStream();
            await Transfer(buffer, 8 * 1024 * 1024);
            token.ThrowIfCancellationRequested();
            Install(buffer.ToArray());
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = DictionaryPath + "." + Guid.NewGuid() + ".download";
        try
        {
            await using (var output = File.Create(temporary)) await Transfer(output, null);
            if (expectedHash != null)
            {
                await using var input = File.OpenRead(temporary);
                var hash = await System.Security.Cryptography.SHA256.HashDataAsync(input, token);
                if (!Convert.ToHexString(hash).Equals(expectedHash,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("下载的词典校验失败，请重试。");
            }
            InstallDictionary(temporary, token);
        }
        finally { File.Delete(temporary); }

        async Task Transfer(Stream output, int? maximum)
        {
            await using var input = await response.Content.ReadAsStreamAsync(token);
            long? total = response.Content.Headers.ContentLength;
            var bytes = new byte[64 * 1024];
            long received = 0, nextUpdate = 0;
            int count;
            while ((count = await input.ReadAsync(bytes.AsMemory(), token)) > 0)
            {
                received += count;
                if (received > maximum) throw new InvalidDataException("自选词包超过 8 MiB。");
                await output.WriteAsync(bytes.AsMemory(0, count), token);
                long now = Environment.TickCount64;
                if (now >= nextUpdate) { progress?.Report(new(received, total)); nextUpdate = now + 100; }
            }
            progress?.Report(new(received, total));
        }
    }
}
