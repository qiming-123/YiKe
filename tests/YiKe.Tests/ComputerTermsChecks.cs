using System.Net;
using System.Text.Json;
using YiKe.Core;
using YiKe.Translation;

public static class ComputerTermsChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check)
    {
        await check("Linux commands describe options and effects without executing selected text", () =>
        {
            Assert(ProgrammingGlossary.TermCount >= 200);
            Assert(ProgrammingGlossary.Explain("chmod 755 script.sh").Contains("r=4"));
            Assert(ProgrammingGlossary.Explain("rm -rf /tmp/example").Contains("不进入回收站"));
            Assert(ProgrammingGlossary.Explain("systemctl enable example.service").Contains("不等于立即 start"));
            Assert(ProgrammingGlossary.Explain("kill -9 1234").Contains("不可捕获"));
            foreach (string command in new[] { "ls", "sudo", "grep", "journalctl", "apt", "mount", "df", "du", "ip", "ssh", "tar", "dd", "crontab" }) Assert(ProgrammingGlossary.Explain(command).Contains("不执行命令"));
            return Task.CompletedTask;
        });
        await check("Computer glossary covers OS networking databases security hardware algorithms and AI", () =>
        {
            foreach (string term in new[] { "进程", "系统调用", "virtual memory", "cgroups", "dns", "HTTPS", "TLS", "ACID", "foreign key", "MVCC", "Git", "Docker", "Kubernetes", "JWT", "XSS", "CPU", "UTF-8", "BFS", "Big O", "LLM", "RAG" }) Assert(ProgrammingGlossary.Explain(term).Length > 0);
            Assert(ProgrammingGlossary.Explain("虚拟内存和页表的作用").Contains("页表"));
            Assert(!ProgrammingGlossary.Explain("虚拟内存").Contains("RAM / ROM"));
            Assert(ProgrammingGlossary.Explain("git rebase").Contains("重放"));
            Assert(!ProgrammingGlossary.Explain("git rebase").Contains("Git · 分布式"));
            Assert(ProgrammingGlossary.Explain("ip").Contains("ip / ss") && !ProgrammingGlossary.Explain("ip").Contains("32 位"));
            Assert(!ProgrammingGlossary.Explain("DNS").Contains("shell 数据流"));
            foreach (string symbol in new[] { "|", "||", "&&", ">>", "2>&1" }) Assert(ProgrammingGlossary.Explain(symbol).Contains("shell"));
            return Task.CompletedTask;
        });
        await check("Expanded terms retain ordinary sentence and identifier boundaries", () =>
        {
            foreach (string text in new[] { "The cat is on the table.", "Please find the new map.", "We get the post tomorrow.", "The room number is 404.", "The cow is on the farm.", "We will rest tonight.", "Put on your cap.", "The athletes are training.", "明天去上英语课。", "preDNSpost", "my_chmod", "LS" }) Assert(ProgrammingGlossary.Explain(text) == "");
            foreach (string text in new[] { "seqlock", "memoization", "零拷贝", "Linux uses io_uring for asynchronous I/O." }) Assert(ProgrammingGlossary.Analyze(text).RequestModelNotes);
            Assert(!ProgrammingGlossary.Analyze("DNS").RequestModelNotes);
            Assert(!ProgrammingGlossary.Analyze("Linux " + TechnicalOutput.Marker).RequestModelNotes);
            return Task.CompletedTask;
        });
        await check("Technical stream delimiter is separated at every byte-independent string boundary", () =>
        {
            string raw = "写时复制\n" + TechnicalOutput.Marker + "\ncopy-on-write：在写入时复制共享页面。</YIKE_TECH_NOTES>";
            for (int split = 0; split <= raw.Length; split++)
            {
                var output = new TechnicalOutput(true); output.Append(raw[..split]); output.Append(raw[split..]); output.Finish();
                Assert(output.Text.TrimEnd() == "写时复制" && output.Notes.Contains("在写入时复制共享页面") && !output.Text.Contains(TechnicalOutput.Marker) && !output.Notes.Contains("</YIKE_"));
            }
            var one = new TechnicalOutput(true); foreach (char c in raw) one.Append(c.ToString()); one.Finish(); Assert(one.Text.TrimEnd() == "写时复制");
            var plain = new TechnicalOutput(false); plain.Append(raw); plain.Finish(); Assert(plain.Text == raw && plain.Notes == "");
            var partial = new TechnicalOutput(true); partial.Append("source <YIKE_"); partial.Finish(); Assert(partial.Text == "source <YIKE_");
            var bounded = new TechnicalOutput(true); bounded.Append("text" + TechnicalOutput.Marker + new string('x', 10000)); bounded.Finish(); Assert(bounded.Notes.Length < 6100 && bounded.Text == "text");
            return Task.CompletedTask;
        });
        await check("Unknown technical terms receive a single isolated request and cache explanations separately", async () =>
        {
            string response = "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"记忆化\\n<YIKE_TECH_NOTES>\\n复用已计算的结果。\"}}]}";
            var http = new FakeHttp(HttpStatusCode.OK, response, false, 3); using var provider = new OpenAiCompatibleProvider(http);
            var result = new TaskCompletionSource<TranslationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var coordinator = new TranslationCoordinator(provider, update => { if (update.Complete) result.TrySetResult(update); });
            var settings = new AppSettings { ExplainProgrammingTerms = true, SupplementTechnicalTerms = true, TranslationMode = "online", Model = "fixture", Stream = false };
            for (int attempt = 0; attempt < 2; attempt++)
            {
                result = new(TaskCreationOptions.RunContinuationsAsynchronously); coordinator.Submit("memoization", settings, "fake");
                var update = await result.Task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert(update.Text.Trim() == "记忆化" && update.ProgrammingNotes.Contains("模型补充") && update.ProgrammingNotes.Contains("复用已计算的结果"));
                Assert(update.Cached == (attempt == 1));
            }
            Assert(http.Calls == 1); using var body = JsonDocument.Parse(http.RequestBody!);
            Assert(body.RootElement.GetProperty("messages").GetArrayLength() == 2 && body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString() == "memoization");
            Assert(body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!.Contains(TechnicalOutput.Marker));
        });
        await check("Disabling smart supplementation preserves glossary and uses a separate request cache", async () =>
        {
            var provider = new SupplementProbe(); var result = new TaskCompletionSource<TranslationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var coordinator = new TranslationCoordinator(provider, update => { if (update.Complete) result.TrySetResult(update); });
            var settings = new AppSettings { ExplainProgrammingTerms = true, SupplementTechnicalTerms = true };
            foreach (bool smart in new[] { true, false, true })
            {
                result = new(TaskCreationOptions.RunContinuationsAsynchronously); coordinator.Submit("Linux uses seqlock.", settings with { SupplementTechnicalTerms = smart }, "");
                var update = await result.Task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert(update.ProgrammingNotes.Contains("Linux ·") && update.ProgrammingNotes.Contains("模型补充") == smart && !update.Text.Contains(TechnicalOutput.Marker));
            }
            Assert(provider.Calls == 2 && provider.LastKnown.Contains("Linux"));
            Assert(!JsonSerializer.Deserialize<AppSettings>("{}")!.SupplementTechnicalTerms);
            Assert(!JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(new AppSettings { ExplainProgrammingTerms = true, SupplementTechnicalTerms = false }))!.SupplementTechnicalTerms);
        });
        await check("Interrupted model notes are marked incomplete and never cached", async () =>
        {
            var provider = new SupplementProbe { Fail = true }; var result = new TaskCompletionSource<TranslationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var coordinator = new TranslationCoordinator(provider, update => { if (update.Error is not null) result.TrySetResult(update); });
            for (int i = 0; i < 2; i++)
            {
                result = new(TaskCreationOptions.RunContinuationsAsynchronously); coordinator.Submit("seqlock", new AppSettings { ExplainProgrammingTerms = true, SupplementTechnicalTerms = true }, "");
                var update = await result.Task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert(!update.Complete && update.ProgrammingNotes.Contains("尚未完成") && !update.Text.Contains(TechnicalOutput.Marker));
            }
            Assert(provider.Calls == 2);
        });
        await check("Providers without technical generation keep dictionary explanations and ordinary translation", async () =>
        {
            var provider = new SupplementProbe { Capable = false }; var result = new TaskCompletionSource<TranslationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var coordinator = new TranslationCoordinator(provider, update => { if (update.Complete) result.TrySetResult(update); });
            coordinator.Submit("Linux uses seqlock.", new AppSettings { ExplainProgrammingTerms = true, SupplementTechnicalTerms = true }, ""); var update = await result.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert(update.ProgrammingNotes.Contains("Linux ·") && !update.ProgrammingNotes.Contains("模型补充") && !provider.WasAsked);
        });
        await check("Model repetitions of bundled definitions are omitted while new terms are retained", () =>
        {
            const string known = "copy-on-write / COW · 写时复制; fork / exec / wait · 进程生命周期";
            string filtered = TechnicalOutput.WithoutKnownDefinitions("fork：重复定义。\n后续说明。\n\n**COW**：重复定义。\nmemoization：复用先前结果。\n它可以减少重复计算。", known);
            Assert(!filtered.Contains("重复定义") && !filtered.Contains("后续说明") && filtered.Contains("memoization") && filtered.Contains("减少重复计算"));
            Assert(TechnicalOutput.WithoutKnownDefinitions("memoization：复用先前结果。", known) == "memoization：复用先前结果。");
            return Task.CompletedTask;
        });
    }
    static void Assert(bool value) { if (!value) throw new Exception("Computer terms assertion failed"); }
    sealed class SupplementProbe : ITranslationProvider
    {
        public int Calls; public bool Fail; public bool Capable = true, WasAsked; public string LastKnown = "";
        public bool CanExplainTechnical(AppSettings settings) => Capable;
        public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            Calls++; LastKnown = request.KnownTechnicalTerms; WasAsked = request.ExplainTechnical; await Task.Yield(); yield return "测试译文";
            if (request.ExplainTechnical) foreach (char c in TechnicalOutput.Marker + "\nseqlock：用于读取重试的同步机制。") yield return c.ToString();
            if (Fail) throw new TranslationException("fixture", "Synthetic interruption");
        }
    }
}
