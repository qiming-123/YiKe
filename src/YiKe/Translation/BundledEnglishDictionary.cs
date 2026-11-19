using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace YiKe.Translation;

// Binary search the embedded resource, decoding only a few keys and the matching
// translation. No full-dictionary parse, database/runtime dependency or disk cache.
internal static class BundledEnglishDictionary
{
    static readonly Regex Key = new(@"^[a-z][a-z0-9 _+./'-]{0,127}$", RegexOptions.CultureInvariant);
    static readonly UTF8Encoding Utf8 = new(false, true);
    static Stream Open() => typeof(BundledEnglishDictionary).Assembly.GetManifestResourceStream("YiKe.word-dictionary") ?? throw new InvalidDataException("Missing word dictionary");
    static uint Header(BinaryReader reader)
    {
        if (Encoding.ASCII.GetString(reader.ReadBytes(8)) is not ("YIKEDIC1" or "YIKEDIC2")) throw new InvalidDataException("Invalid word dictionary format");
        uint count = reader.ReadUInt32();
        if (count == 0 || count > 2_000_000 || 12L + 4L * (count + 1) >= reader.BaseStream.Length) throw new InvalidDataException("Invalid word dictionary index");
        return count;
    }
    internal static int Count
    {
        get { using var stream = Open(); using var reader = new BinaryReader(stream, Utf8); return checked((int)Header(reader)); }
    }
    internal static string? Find(string source)
    {
        string? value = Lookup(source);
        if (value is null || !value.StartsWith("{", StringComparison.Ordinal)) return value;
        using var json = System.Text.Json.JsonDocument.Parse(value);
        return json.RootElement.GetProperty("translation").GetString();
    }
    internal static string? Details(string source)
    {
        string? value = Lookup(source);
        if (value is null || !value.StartsWith("{", StringComparison.Ordinal)) return value;
        using var json = System.Text.Json.JsonDocument.Parse(value);
        var entry = json.RootElement;
        string phonetic = entry.GetProperty("phonetic").GetString() ?? "";
        var card = new StringBuilder(source.Trim());
        if (phonetic.Length > 0) card.Append("  /").Append(phonetic).Append('/');
        card.Append("\n\n中文释义\n").Append(entry.GetProperty("translation").GetString());
        var definition = entry.GetProperty("definition").GetString();
        if (!string.IsNullOrWhiteSpace(definition)) card.Append("\n\n英文释义\n").Append(definition);
        if (entry.TryGetProperty("exchange", out var exchange))
        {
            foreach (var form in (exchange.GetString() ?? "").Split('/'))
            {
                var pair = form.Split(':', 2);
                if (pair.Length != 2) continue;
                var label = pair[0] switch { "p" => "过去式", "d" => "过去分词", "i" => "现在分词", "3" => "第三人称单数", "s" => "复数", "r" => "比较级", "t" => "最高级", "0" => "原形", _ => "" };
                if (label.Length > 0) card.Append("\n").Append(label).Append("  ").Append(pair[1]);
            }
        }
        var example = source.Trim().ToLowerInvariant() switch
        {
            "light" => "light luggage · 轻便的行李\nTravel light on the return trip. 回程时轻装出行。",
            "run" => "run a test · 运行测试\nWe run the test twice. 我们把测试运行两次。",
            "set" => "set a limit · 设定上限\nSet a limit before downloading. 下载前先设定上限。",
            "save" => "save a copy · 保存副本\nSave a copy before editing. 编辑前保存一份副本。",
            "read" => "read aloud · 朗读\nRead the last line aloud. 把最后一行读出来。",
            "book" => "book a seat · 预订座位\nYou can book a seat online. 你可以在线预订座位。",
            _ => null
        };
        if (example is not null) card.Append("\n\n搭配与例句\n").Append(example);
        return card.ToString();
    }
    static string? Lookup(string source)
    {
        string key = source.Trim().ToLowerInvariant();
        if (!Key.IsMatch(key)) return null;
        try
        {
            using var stream = Open(); using var reader = new BinaryReader(stream, Utf8);
            uint count = Header(reader); long dataStart = 12L + 4L * (count + 1);
            int low = 0, high = checked((int)count) - 1;
            while (low <= high)
            {
                int mid = low + (high - low) / 2;
                stream.Position = 12L + mid * 4L; uint start = reader.ReadUInt32(), end = reader.ReadUInt32();
                if (start < dataStart || end <= start || end > stream.Length) throw new InvalidDataException("Invalid dictionary entry bounds");
                stream.Position = start; int length = reader.ReadUInt16();
                if (length < 1 || length > 128 || stream.Position + length + 2 > end) throw new InvalidDataException("Invalid dictionary key");
                string candidate = Encoding.ASCII.GetString(reader.ReadBytes(length));
                int order = string.CompareOrdinal(key, candidate);
                if (order < 0) high = mid - 1;
                else if (order > 0) low = mid + 1;
                else
                {
                    int textLength = reader.ReadUInt16();
                    if (textLength < 1 || textLength > 8192 || stream.Position + textLength != end) throw new InvalidDataException("Invalid dictionary translation");
                    return Utf8.GetString(reader.ReadBytes(textLength));
                }
            }
        }
        catch (Exception error) when (error is IOException or DecoderFallbackException or OverflowException) { }
        return null;
    }
}
