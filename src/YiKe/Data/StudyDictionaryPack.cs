using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace YiKe.Data;

internal sealed record StudyDictionaryEntry(string Headword, string Reading, string Meaning, string Example, string[] Forms)
{
    internal string Card(string form) => form + (form == Headword ? "" : "  →  " + Headword) + "\n" + Reading + "\n\n" + Meaning + "\n\n" + Example;
}
internal sealed record StudyDictionaryData(string Language, StudyDictionaryEntry[] Entries);

internal sealed class StudyDictionaryPack
{
    readonly string path;
    Dictionary<string,StudyDictionaryEntry> index = new(StringComparer.OrdinalIgnoreCase);
    string DictionaryPath => Path.ChangeExtension(path, ".ykdict");
    sealed record Source(string Code, string Version, string Url, string Sha256);
    static readonly Source[] Sources = ReadResource<Source[]>("YiKe.study-sources");
    static readonly Lazy<StudyDictionaryEntry[]> FrenchCorrections = new(() => ReadResource<StudyDictionaryData>("YiKe.fr-corrections").Entries);
    static T ReadResource<T>(string name)
    {
        using var resource = typeof(StudyDictionaryPack).Assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<T>(resource)!;
    }
    internal Task DownloadOfficial()
    {
        var source = Sources.Single(s => s.Code == Code);
        return Download(source.Url, source.Sha256);
    }
    internal event Action? InstallationChanged;
    internal int Count { get; private set; }
    internal bool Installed => Count > 0 || index.Count > 0;
    internal string InstallLabel => Installed ? $"已安装{Name}词典" : $"下载{Name}词典";
    static int ReadHeader(BinaryReader r, string code, out int keys)
    {
        if (System.Text.Encoding.ASCII.GetString(r.ReadBytes(10)) != "YIKELEX1"+code) throw new InvalidDataException("词典语言或格式不匹配。");
        keys = r.ReadInt32(); int count = r.ReadInt32();
        if (count <= 0 || keys <= 0 || 18L+keys*4L >= r.BaseStream.Length) throw new InvalidDataException("词典索引不完整。");
        return count;
    }
    internal void InstallDictionary(string source)
    {
        using var reader = new BinaryReader(File.OpenRead(source));
        int count = ReadHeader(reader,Code,out _);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = DictionaryPath+"."+Guid.NewGuid()+".tmp";
        try { File.Copy(source,tmp); File.Move(tmp,DictionaryPath,true); Count=count; InstallationChanged?.Invoke(); } finally { File.Delete(tmp); }
    }
    internal static StudyDictionaryPack[] Selected(StudyDictionaryPack[] packs, YiKe.Core.AppSettings settings) =>
        settings.LearnJapanese ? Array.FindAll(packs,p => !settings.DisabledStudyLanguages.Contains(p.Code)) : Array.Empty<StudyDictionaryPack>();

    internal static readonly (string Code,string Name)[] Languages = { ("ja","日语"), ("ko","韩语"), ("fr","法语"), ("de","德语"), ("es","西班牙语") };
    internal string Code { get; }
    internal string Name { get; }
    internal StudyDictionaryPack(string directory,string code="ja",string name="日语")
    {
        Code = code; Name = name; path = Path.Combine(directory, "packs", code+".json");
        if (File.Exists(DictionaryPath)) { using var reader = new BinaryReader(File.OpenRead(DictionaryPath)); Count = ReadHeader(reader,Code,out _); }
        if (File.Exists(path)) index = Parse(File.ReadAllBytes(path));
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
        byte[] key = System.Text.Encoding.UTF8.GetBytes(word.Normalize().ToLowerInvariant());
        using var reader = new BinaryReader(File.OpenRead(DictionaryPath));
        ReadHeader(reader,Code,out int keys);
        int lo=0, hi=keys-1;
        while (lo <= hi)
        {
            int mid = lo+(hi-lo)/2; reader.BaseStream.Position=18L+4L*mid;
            reader.BaseStream.Position=reader.ReadUInt32();
            int length=reader.ReadInt32(); uint pos=reader.ReadUInt32(); int size=reader.ReadInt32();
            int cmp=key.AsSpan().SequenceCompareTo(reader.ReadBytes(length));
            if (cmp < 0) hi=mid-1;
            else if (cmp > 0) lo=mid+1;
            else { reader.BaseStream.Position=pos; var entry = JsonSerializer.Deserialize<StudyDictionaryEntry>(reader.ReadBytes(size)); return entry is null ? null : entry with { Meaning = entry.Meaning.Replace("unknown · ", "") }; }
        }
        return index.GetValueOrDefault(word);
    }
    Dictionary<string,StudyDictionaryEntry> Parse(byte[] bytes)
    {
        var pack = JsonSerializer.Deserialize<StudyDictionaryData>(bytes);
        if (pack is not { Entries.Length: > 0 } || pack.Language != Code) throw new InvalidDataException("请选择对应语言的研习词包。");
        var entries = new Dictionary<string,StudyDictionaryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in pack.Entries)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Headword) || string.IsNullOrWhiteSpace(item.Reading) || string.IsNullOrWhiteSpace(item.Meaning) || item.Forms is null)
                throw new InvalidDataException("日语词条缺少必要字段。");
            foreach (string form in item.Forms.Prepend(item.Headword).Distinct())
                if (string.IsNullOrWhiteSpace(form) || !entries.TryAdd(form,item)) throw new InvalidDataException("词包中存在空白或重复的词形。");
        }
        return entries;
    }
    internal void Install(byte[] bytes)
    {
        var parsed = Parse(bytes); AtomicFile.Write(path, bytes); index = parsed; InstallationChanged?.Invoke();
    }
    internal async Task Download(string address, string? expectedHash = null)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new InvalidDataException("请输入 HTTPS 词包直链。");
        using var client = new HttpClient { MaxResponseContentBufferSize = 8 * 1024 * 1024, Timeout = TimeSpan.FromSeconds(30) };
        if (!uri.AbsolutePath.EndsWith(".ykdict",StringComparison.OrdinalIgnoreCase)) { Install(await client.GetByteArrayAsync(uri)); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = DictionaryPath + "." + Guid.NewGuid() + ".download";
        try
        {
            using var response = await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using (var output = File.Create(temporary)) await response.Content.CopyToAsync(output);
            if (expectedHash != null)
            {
                await using var input = File.OpenRead(temporary);
                var hash = await System.Security.Cryptography.SHA256.HashDataAsync(input);
                if (!Convert.ToHexString(hash).Equals(expectedHash,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("下载的词典校验失败，请重试。");
            }
            InstallDictionary(temporary);
        }
        finally { File.Delete(temporary); }
    }
}
