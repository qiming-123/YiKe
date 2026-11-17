using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace YiKe.Translation;

// A bounded learning aid, not a shell interpreter. Never resolves paths, expands
// variables, invokes help programs or executes selected text.
internal static partial class TerminalCommands
{
    internal sealed record Option(string[] Keys, string Description, bool Value);
    internal sealed record Command(string Name, string Platform, string Purpose, string Syntax, Option[] Options, string[] Operands, string Example, string Effect, string Note, bool IgnoreCase);
    internal sealed record Explanation(string Notes, string KnownTerms, bool NeedsModel);
    static Command C(string name, string platform, string purpose, string syntax, string options, string operands, string example, string effect, string note = "", bool ignoreCase = false) =>
        new(name, platform, purpose, syntax, options.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(row =>
        {
            int split = row.IndexOf('='); string keys = row[..split];
            return new Option(keys.Replace(" <值>", "", StringComparison.Ordinal).Split(','), row[(split + 1)..], keys.Contains(" <值>", StringComparison.Ordinal));
        }).ToArray(), operands.Split(';', StringSplitOptions.RemoveEmptyEntries), example, effect, note, ignoreCase);
    internal static int Count => Commands.Length;
    internal static Command[] Inventory => Commands.ToArray();
    static readonly Regex Prompt = new(@"^(?:PS [^\r\n>]{1,240}>[ \t]*|[A-Za-z]:\\[^\r\n>]{0,240}>[ \t]*|[\w.-]+@[\w.-]+:[^\r\n$#]{0,120}[$#][ \t]+|[$#][ \t]+)", RegexOptions.CultureInvariant);
    internal static Explanation? Explain(string source)
    {
        if (!Core.TextLimit.Allowed(source)) return null;
        var segments = new List<List<string>>(); var operators = new HashSet<string>();
        foreach (string line in source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Take(8))
        {
            string value = Prompt.Replace(line.Trim(), "");
            if (!Tokenize(value, segments, operators)) return null;
        }
        if (segments.Count == 0) return null;
        var found = new List<(Command Spec, List<string> Args)>(); bool unknown = false;
        foreach (var tokens in segments.Take(8))
        {
            var match = Find(tokens);
            if (match.Spec is null) { unknown = true; continue; }
            found.Add((match.Spec, tokens.Skip(match.Prefix).ToList()));
        }
        // Do not interpret a prose sentence merely because a later word is a command.
        if (Find(segments[0]).Spec is null || found.Count == 0)
        {
            string first = Prompt.Replace(source.Trim(), "");
            return Regex.IsMatch(first, @"^[A-Za-z][A-Za-z0-9_.+-]{0,60}[ \t]+--?[A-Za-z]", RegexOptions.CultureInvariant)
                ? new("本机命令用法：此命令尚未收录详细资料。可用目标工具的 --help / -h 或 PowerShell 的 Get-Help 查询；具体支持的帮助形式依工具而异。标准模型或在线服务可按当前设置补充，轻量模式仅显示此提示。", "", true) : null;
        }
        var output = new StringBuilder("本机命令用法 · 语法、参数与示例（仅说明，不执行命令；不是完整 shell 解析器）\n");
        foreach (var item in found.Take(4))
        {
            var spec = item.Spec;
            output.Append('\n').Append(spec.Name).Append(" · ").Append(spec.Platform).Append('\n');
            output.Append("用途：").Append(spec.Purpose).Append("\n语法：").Append(spec.Syntax).Append('\n');
            if (spec.Options.Length > 0)
            {
                output.Append("常用参数：\n");
                foreach (var option in spec.Options) output.Append("  ").AppendJoin(" / ", option.Keys).Append(option.Value ? " <值>" : "").Append("：").Append(option.Description).Append('\n');
            }
            DescribeArguments(output, spec, item.Args);
            output.Append("示例：").Append(spec.Example).Append("\n效果：").Append(spec.Effect).Append('\n');
            if (spec.Note.Length > 0) output.Append("注意：").Append(spec.Note).Append('\n');
        }
        if (operators.Count > 0)
        {
            output.Append("\n组合符号：\n");
            foreach (string symbol in operators)
                output.Append(symbol).Append("：").Append(symbol switch
                {
                    "|" => "将前一命令的输出交给后一命令；Bash / CMD 通常传文本，PowerShell 常传对象。",
                    "||" => "前一命令失败才继续；PowerShell 需要 7 或以上版本。",
                    "&&" => "前一命令成功才继续；PowerShell 需要 7 或以上版本。",
                    "2>&1" => "将标准错误送到当前标准输出的目标；与文件重定向组合时，先后顺序可能影响结果。",
                    ";" => "在 Bash / PowerShell 分隔命令，通常不依赖上一条成功；CMD 语法不同。",
                    "&" => "含义随 shell 改变：Bash 可后台执行，CMD 可分隔命令，PowerShell 可作调用运算符。",
                    ">>" or "2>>" => "追加输出到文件，可能创建或修改文件；2 指错误流。编码和对象转换依 shell 而异。",
                    _ => "重定向输出，> 通常覆盖目标文件；2> 通常处理错误流。"
                }).Append('\n');
        }
        if (found.Count > 4 || segments.Count > 8) output.Append("\n已限制显示数量，请缩小选区查看其他命令。");
        output.Append("\n尖括号表示需替换的值，方括号表示可选内容，不必照抄。终端是界面，实际可用命令取决于 shell、已安装工具、系统和版本；选区未提供的环境不会被自动检测。");
        return new(output.ToString(), string.Join("; ", found.Take(4).Select(item => item.Spec.Name)), unknown);
    }
    static (Command? Spec, int Prefix) Find(List<string> tokens)
    {
        foreach (var spec in Commands.OrderByDescending(item => item.Name.Length))
        {
            string[] prefix = spec.Name.Split(' ');
            if (tokens.Count >= prefix.Length && prefix.Select((part, i) => string.Equals(part, Unquote(tokens[i]), spec.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)).All(value => value)) return (spec, prefix.Length);
        }
        return (null, 0);
    }
    static string Unquote(string text) => text.Length >= 2 && text[0] == text[^1] && text[0] is '\'' or '"' ? text[1..^1] : text;
    static void DescribeArguments(StringBuilder output, Command spec, List<string> args)
    {
        if (args.Count == 0) return;
        output.Append("本次选中参数：\n"); int positional = 0;
        for (int index = 0; index < args.Count && index < 16; index++)
        {
            string raw = args[index], value = Unquote(raw);
            if (value == "2>&1") { output.Append("  2>&1：将标准错误合并到当前标准输出目标。\n"); continue; }
            if (value is ">" or ">>" or "2>" or "2>>")
            {
                output.Append("  ").Append(value).Append("：输出重定向；目标为 ").Append(index + 1 < args.Count ? args[++index] : "（选区未包含目标）").Append("，可能写入或覆盖文件。\n"); continue;
            }
            int equal = value.IndexOf('='); string key = equal > 0 ? value[..equal] : value;
            var option = spec.Options.FirstOrDefault(item => item.Keys.Contains(key, spec.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal));
            if (option is not null)
            {
                output.Append("  ").Append(raw).Append("：").Append(option.Description);
                if (option.Value)
                {
                    if (equal > 0) output.Append("；值为 ").Append(value[(equal + 1)..]);
                    else if (index + 1 < args.Count) output.Append("；值为 ").Append(args[++index]);
                    else output.Append("；选区中缺少其值");
                }
                output.Append('\n'); continue;
            }
            if (value == "--") { output.Append("  --：在支持此约定的命令中结束选项，后面的内容作为位置参数。\n"); for (index++; index < args.Count && index < 16; index++) Operand(args[index]); break; }
            if (value.Length > 2 && value[0] == '-' && value[1] != '-' && !spec.IgnoreCase)
            {
                var descriptions = new List<string>(); string? attached = null; bool needsValue = false, recognized = true;
                for (int part = 1; part < value.Length; part++)
                {
                    var shortOption = spec.Options.FirstOrDefault(item => item.Keys.Contains("-" + value[part], StringComparer.Ordinal));
                    if (shortOption is null) { recognized = false; break; }
                    descriptions.Add("-" + value[part] + "：" + shortOption.Description);
                    if (shortOption.Value) { needsValue = true; attached = part + 1 < value.Length ? value[(part + 1)..] : null; break; }
                }
                if (recognized)
                {
                    output.Append("  ").Append(raw).Append("：短选项用法；").AppendJoin("；", descriptions);
                    if (needsValue) output.Append("；值为 ").Append(attached ?? (index + 1 < args.Count ? args[++index] : "（选区中缺少值）"));
                    output.Append('\n'); continue;
                }
            }
            if (value.StartsWith('-') || (spec.IgnoreCase && value.StartsWith('/'))) output.Append("  ").Append(raw).Append("：本机资料未收录此选项，需核对目标版本帮助；不猜测含义。\n");
            else Operand(raw);
        }
        if (args.Count > 16) output.Append("  参数较多，仅展开前 16 项。\n");
        void Operand(string value)
        {
            string meaning = spec.Operands.Length == 0 ? "位置参数；需结合目标命令的子命令或输入说明" : spec.Operands[Math.Min(positional++, spec.Operands.Length - 1)];
            output.Append("  ").Append(value).Append("：").Append(meaning).Append('\n');
        }
    }
    static bool Tokenize(string value, List<List<string>> segments, HashSet<string> operators)
    {
        var tokens = new List<string>(); var word = new StringBuilder(); char quote = '\0';
        void Flush() { if (word.Length > 0) { tokens.Add(word.ToString()); word.Clear(); } }
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            if (quote != '\0') { word.Append(ch); if (ch == quote) quote = '\0'; else if ((ch == '`' || ch == '\\') && i + 1 < value.Length && value[i + 1] == quote) word.Append(value[++i]); continue; }
            if (ch is '\'' or '"') { quote = ch; word.Append(ch); continue; }
            if (ch is '`' or '\\' && i + 1 < value.Length && (char.IsWhiteSpace(value[i + 1]) || value[i + 1] is '|' or ';' or '&' or '>' or '\'' or '"')) { word.Append(ch).Append(value[++i]); continue; }
            if (ch is '|' or '&' or ';')
            {
                Flush(); if (tokens.Count > 0) { segments.Add(tokens); tokens = new(); }
                string symbol = ch.ToString(); if (i + 1 < value.Length && value[i + 1] == ch && ch != ';') { symbol += ch; i++; } operators.Add(symbol); continue;
            }
            if (ch == '>')
            {
                string symbol = word.ToString() == "2" ? "2>" : ">";
                if (symbol == "2>") word.Clear(); else Flush();
                if (symbol == "2>" && i + 2 < value.Length && value[i + 1] == '&' && value[i + 2] == '1') { symbol += "&1"; i += 2; }
                else if (i + 1 < value.Length && value[i + 1] == '>') { symbol += '>'; i++; }
                operators.Add(symbol); tokens.Add(symbol); continue;
            }
            if (char.IsWhiteSpace(ch)) Flush(); else word.Append(ch);
        }
        if (quote != '\0') return false;
        Flush(); if (tokens.Count > 0) segments.Add(tokens);
        return true;
    }
}
