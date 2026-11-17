using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace YiKe.Translation;

public static class SseReader
{
    public const int MaximumLineCharacters = 131072;
    public const int MaximumEventCharacters = 262144;
    public const int MaximumStreamCharacters = 2097152;

    public static async IAsyncEnumerable<string> ReadAsync(Stream stream, [EnumeratorCancellation] CancellationToken token)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), false, 4096, leaveOpen: true);
        var buffer = new char[2048];
        var line = new StringBuilder();
        var data = new StringBuilder();
        bool afterCarriageReturn = false, firstCharacter = true, hasDataField = false;
        int received = 0;

        string? EndLine()
        {
            if (line.Length == 0)
            {
                if (!hasDataField) return null;
                string payload = data.ToString();
                data.Clear(); hasDataField = false;
                return payload;
            }
            if (line.ToString().StartsWith("data:", StringComparison.Ordinal) || line.ToString() == "data")
            {
                int start = Math.Min(5, line.Length);
                if (start < line.Length && line[start] == ' ') start++;
                int extra = line.Length - start + (hasDataField ? 1 : 0);
                if (data.Length + extra > MaximumEventCharacters) throw TooLarge();
                if (hasDataField) data.Append('\n');
                for (int i = start; i < line.Length; i++) data.Append(line[i]);
                hasDataField = true;
            }
            line.Clear();
            return null;
        }

        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) > 0)
        {
            if ((received += count) > MaximumStreamCharacters) throw TooLarge();
            for (int i = 0; i < count; i++)
            {
                char c = buffer[i];
                if (firstCharacter) { firstCharacter = false; if (c == '\uFEFF') continue; }
                if (afterCarriageReturn) { afterCarriageReturn = false; if (c == '\n') continue; }
                if (c is '\r' or '\n')
                {
                    afterCarriageReturn = c == '\r';
                    string? payload = EndLine();
                    if (payload is not null) yield return payload;
                }
                else
                {
                    if (line.Length >= MaximumLineCharacters) throw TooLarge();
                    line.Append(c);
                }
            }
        }
        // An SSE event is dispatched by its blank line. An unterminated final event is incomplete.
    }

    static TranslationException TooLarge() => new("format", "服务响应超过读取上限，已停止；译文可能不完整。");
}
