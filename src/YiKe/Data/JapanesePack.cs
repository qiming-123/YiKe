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
    internal bool Installed => index.Count > 0;
    internal static readonly (string Code,string Name)[] Languages = { ("ja","日语"), ("ko","韩语"), ("fr","法语"), ("de","德语"), ("es","西班牙语") };
    internal string Code { get; }
    internal string Name { get; }
    internal JapanesePack(string directory,string code="ja",string name="日语")
    {
        Code = code; Name = name; path = Path.Combine(directory, "packs", code+".json");
        if (File.Exists(path)) index = Parse(File.ReadAllBytes(path));
    }
    internal JapaneseEntry? Find(string word) => index.GetValueOrDefault(word.Trim());
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
        Install(await client.GetByteArrayAsync(uri));
    }
}
