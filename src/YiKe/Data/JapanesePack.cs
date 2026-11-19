using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace YiKe.Data;

internal sealed record JapaneseEntry(string Headword, string Reading, string Meaning, string Example, string[] Forms)
{
    internal string Card(string form) => form + (form == Headword ? "" : "  →  " + Headword) + "\n" + Reading + "\n\n" + Meaning + "\n\n" + Example;
}
internal sealed record JapaneseData(string Language, JapaneseEntry[] Entries);

internal sealed class JapanesePack
{
    readonly string path;
    Dictionary<string,JapaneseEntry> index = new(StringComparer.OrdinalIgnoreCase);
    string DictionaryPath => Path.ChangeExtension(path, ".ykdict");
    internal int Count { get; private set; }
    internal bool Installed => Count > 0 || index.Count > 0;
    internal string InstallLabel => Count > 0 ? $"已安装{Name}词典 · {Count:N0} 词条" : $"安装{Name}词典";
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
        try { File.Copy(source,tmp); File.Move(tmp,DictionaryPath,true); Count=count; } finally { File.Delete(tmp); }
    }
    internal static readonly (string Code,string Name)[] Languages = { ("ja","日语"), ("ko","韩语"), ("fr","法语"), ("de","德语"), ("es","西班牙语") };
    internal string Code { get; }
    internal string Name { get; }
    internal JapanesePack(string directory,string code="ja",string name="日语")
    {
        Code = code; Name = name; path = Path.Combine(directory, "packs", code+".json");
        if (File.Exists(DictionaryPath)) { using var reader = new BinaryReader(File.OpenRead(DictionaryPath)); Count = ReadHeader(reader,Code,out _); }
        if (File.Exists(path)) index = Parse(File.ReadAllBytes(path));
    }
    internal JapaneseEntry? Find(string word)
    {
        word = word.Trim();
        if (Count == 0 || word.Length == 0 || word.Length > 256) return index.GetValueOrDefault(word);
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
            else { reader.BaseStream.Position=pos; return JsonSerializer.Deserialize<JapaneseEntry>(reader.ReadBytes(size)); }
        }
        return index.GetValueOrDefault(word);
    }
    Dictionary<string,JapaneseEntry> Parse(byte[] bytes)
    {
        var pack = JsonSerializer.Deserialize<JapaneseData>(bytes);
        if (pack is not { Entries.Length: > 0 } || pack.Language != Code) throw new InvalidDataException("请选择对应语言的研习词包。");
        var entries = new Dictionary<string,JapaneseEntry>(StringComparer.OrdinalIgnoreCase);
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
        var parsed = Parse(bytes); AtomicFile.Write(path, bytes); index = parsed;
    }
    internal async Task Download(string address)
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
            await Task.Run(() => InstallDictionary(temporary));
        }
        finally { File.Delete(temporary); }
    }
}
