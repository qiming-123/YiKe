using YiKe.Translation;
using YiKe.Selection;

static class TerminalCommandChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check)
    {
        await check("Terminal command catalogue provides offline syntax, parameters, examples and effects", () =>
        {
            Assert(TerminalCommands.Count >= 60);
            foreach (var entry in TerminalCommands.Inventory)
            {
                var result = TerminalCommands.Explain(entry.Name) ?? throw new Exception("Missing command: " + entry.Name);
                Assert(result.Notes.Contains("用途：") && result.Notes.Contains("语法：") && result.Notes.Contains("示例：") && result.Notes.Contains("效果：") && result.Notes.Contains("不执行命令"));
                Assert(result.Notes.Length < 8000 && !result.NeedsModel);
            }
            return Task.CompletedTask;
        });
        await check("Selected command flags and quoted values are explained using their command's semantics", () =>
        {
            string Explain(string text) => TerminalCommands.Explain(text)!.Notes.Split("本次选中参数：")[1];
            Assert(Explain("grep -n -i 'error|warning' ./demo/app.log").Contains("'error|warning'：匹配模式"));
            Assert(!Explain("grep -n -i 'error|warning' ./demo/app.log").Contains("组合符号"));
            Assert(Explain("ls -lah ./demo").Contains("短选项用法"));
            Assert(Explain("tar -tf ./demo/archive.tar").Contains("值为 ./demo/archive.tar"));
            Assert(Explain("head -n5 ./demo/app.log").Contains("值为 5"));
            Assert(Explain("git log --max-count=5").Contains("值为 5"));
            Assert(Explain("GET-CONTENT -LiteralPath '.\\demo a.txt' -Tail 5").Contains("值为 '.\\demo a.txt'"));
            Assert(Explain("ls --invented-flag ./demo").Contains("未收录此选项"));
            return Task.CompletedTask;
        });
        await check("Terminal prompts, pipes and redirection preserve command boundaries", () =>
        {
            Assert(TerminalCommands.Explain("PS C:\\demo> Get-Content -Tail 5 .\\app.log")!.KnownTerms == "Get-Content");
            Assert(TerminalCommands.Explain("alice@host:~$ ls -la")!.KnownTerms == "ls");
            var pipe = TerminalCommands.Explain("cat ./app.log | grep -n error > ./matches.txt")!;
            Assert(pipe.KnownTerms.Contains("cat") && pipe.KnownTerms.Contains("grep") && pipe.Notes.Contains("输出重定向；目标为 ./matches.txt") && pipe.Notes.Contains("覆盖"));
            Assert(TerminalCommands.Explain("grep -e \"a && b\" ./file")!.KnownTerms == "grep");
            Assert(TerminalCommands.Explain("cat 'unterminated") is null);
            Assert(TerminalCommands.Explain("The cat is sleeping.") is null);
            Assert(TerminalCommands.Explain("custom-tool --help") is { NeedsModel: true });
            Assert(TerminalCommands.Explain("cat ./app.log 2>&1")!.Notes.Contains("标准错误合并"));
            return Task.CompletedTask;
        });
        await check("Terminal copy acknowledgements require exact protocol nonce and status", () =>
        {
            foreach (string json in new[] { "{}", "null", "[]", "{broken", "{\"version\":1,\"nonce\":\"n\",\"status\":\"Success\",\"text\":\"old\"}", "{\"version\":1,\"nonce\":\"old\",\"status\":\"CopyRequested\"}" }) Assert(VsCodeSelectionAdapter.ParseCopyAcknowledgement(json, "n") is null);
            Assert(VsCodeSelectionAdapter.ParseCopyAcknowledgement("{\"version\":1,\"nonce\":\"n\",\"status\":\"CopyRequested\"}", "n") is not null);
            return Task.CompletedTask;
        });
        await check("Terminal clipboard gate rejects stale, unrelated and ownerless data", () =>
        {
            var parents = new Dictionary<int, int> { [2] = 1, [3] = 2, [4] = 99 };
            Assert(VsCodeTerminal.FreshCopy(5, 6, 3, 1, parents));
            Assert(VsCodeTerminal.FreshCopy(0, 1, 1, 1, parents));
            Assert(!VsCodeTerminal.FreshCopy(5, 5, 3, 1, parents));
            Assert(!VsCodeTerminal.FreshCopy(5, 0, 3, 1, parents));
            Assert(!VsCodeTerminal.FreshCopy(5, 6, 4, 1, parents));
            Assert(!VsCodeTerminal.FreshCopy(5, 6, 0, 1, parents));
            return Task.CompletedTask;
        });
    }
    static void Assert(bool value) { if (!value) throw new Exception("Terminal command assertion failed"); }
}
