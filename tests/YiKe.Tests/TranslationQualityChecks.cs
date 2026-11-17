using YiKe.Core;
using YiKe.Translation;

static class TranslationQualityChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check)
    {
        await check("Ordinary words and short greetings do not trigger technical generation", () =>
        {
            foreach (string source in new[] { "how", "unmark", "squares", "square", "hello", "neighboring", "diagonally", "randomly", "Good morning", "How are you", "你好", "取消标记" })
            {
                var result = ProgrammingGlossary.Analyze(source);
                Assert(result.Notes == "" && !result.RequestModelNotes);
            }
            Assert(ProgrammingGlossary.Analyze("memoization").RequestModelNotes);
            Assert(ProgrammingGlossary.Analyze("Linux uses seqlock.").RequestModelNotes);
            Assert(ProgrammingGlossary.Analyze("DNS").Notes.Length > 0);
            return Task.CompletedTask;
        });
        await check("Language gate catches untranslated ordinary words without rejecting code, acronyms or names", () =>
        {
            foreach (string source in new[] { "how", "unmark", "squares", "neighboring", "randomly" })
                Assert(TranslationQuality.NeedsRepair(source, source, "简体中文", ""));
            Assert(!TranslationQuality.NeedsRepair("how", "如何", "简体中文", ""));
            Assert(!TranslationQuality.NeedsRepair("OpenAI", "OpenAI", "简体中文", ""));
            Assert(!TranslationQuality.NeedsRepair("Alice", "Alice", "简体中文", ""));
            Assert(!TranslationQuality.NeedsRepair("my_count", "my_count", "简体中文", ""));
            Assert(TranslationQuality.NeedsRepair("entries", "entries", "简体中文", ""));
            Assert(!TranslationQuality.NeedsRepair("DNS", "DNS", "简体中文", "DNS · 域名系统"));
            Assert(!TranslationQuality.NeedsRepair("static int x = 0;", "static int x = 0;", "简体中文", ""));
            Assert(!TranslationQuality.NeedsRepair("how", "how", "英语（English）", ""));
            Assert(!TranslationQuality.NeedsRepair("你好", "你好", "简体中文", ""));
            Assert(TranslationQuality.NeedsRepair("word", " ", "法语（Français）", ""));
            return Task.CompletedTask;
        });
        await check("Bundled basic-word translations are exact, context-bounded and need no model or environment", async () =>
        {
            Assert(CommonWordTranslations.Count >= 150);
            Assert(CommonWordTranslations.Find("unmark", "简体中文") == "取消标记");
            Assert(CommonWordTranslations.Find("squares", "简体中文")!.Contains("方格"));
            Assert(CommonWordTranslations.Find("how", "英语（English）") is null);
            Assert(CommonWordTranslations.Find("how", "繁體中文") is null);
            Assert(CommonWordTranslations.Find("yike-how-not-a-real-word", "简体中文") is null);
            Assert(CommonWordTranslations.Find("Press F to unmark a mine.", "简体中文") is null);
            using var local = new LocalTranslationProvider("C:\\YiKe-nonexistent-test-runtime", () => throw new Exception("Word dictionary attempted to inspect model hardware"));
            var request = new TranslationRequest(1, "unmark", "auto", "简体中文", Guid.NewGuid(), Guid.NewGuid(), "");
            var value = new System.Text.StringBuilder();
            await foreach (string delta in local.TranslateAsync(request, new AppSettings(), "", default)) value.Append(delta);
            Assert(value.ToString() == "取消标记" && local.EngineProcessId == 0 && local.VerificationPasses == 0);
        });
        await check("Large embedded word index performs bounded exact lookups without loading the full dictionary", () =>
        {
            Assert(BundledEnglishDictionary.Count >= 700000);
            foreach (string word in new[] { "demonstrates", "entries", "approximately", "surrounding", "suspected", "zoology", "children" })
                Assert(!string.IsNullOrWhiteSpace(BundledEnglishDictionary.Find(word)));
            Assert(BundledEnglishDictionary.Find("not-a-real-word-yike-4f195ca9") is null);
            Assert(BundledEnglishDictionary.Find("print('how')") is null);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 32; index++) Assert(BundledEnglishDictionary.Find("entries")!.Length < 3000);
            Assert(GC.GetAllocatedBytesForCurrentThread() - before < 4 * 1024 * 1024);
            return Task.CompletedTask;
        });
        await check("A local echoed word is hidden, repaired once with translation-only instructions, then cached", async () =>
        {
            var probe = new Probe((request, _) => request.RefineTranslation ? "如何" : "how");
            var updates = new List<TranslationUpdate>(); var done = Completion(); var settings = new AppSettings();
            using var coordinator = new TranslationCoordinator(probe, update => { lock (updates) updates.Add(update); if (update.Complete || update.Error is not null) done.TrySetResult(update); });
            coordinator.Submit("how", settings, "");
            var result = await done.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert(result.Complete && result.Text == "如何" && result.ProgrammingNotes.Contains("不是编程关键字") && probe.Calls == 2);
            Assert(probe.Requests[1].RefineTranslation && !probe.Requests[1].ExplainTechnical);
            lock (updates) Assert(updates.All(update => update.Text != "how"));
            done = Completion(); coordinator.Submit("how", settings, "");
            result = await done.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert(result.Cached && result.Text == "如何" && probe.Calls == 2);
        });
        await check("Notes-only and provider-empty local responses recover without losing the translated result", async () =>
        {
            foreach (bool throws in new[] { false, true })
            {
                var probe = new Probe((request, _) => request.RefineTranslation ? "记忆化" : TechnicalOutput.Marker + "\nmemoization：复用结果。\n示例：缓存计算值。\n效果：减少重复计算。") { ThrowFirstEmpty = throws };
                var done = Completion(); using var coordinator = new TranslationCoordinator(probe, update => { if (update.Complete || update.Error is not null) done.TrySetResult(update); });
                coordinator.Submit("memoization", new AppSettings(), "");
                var result = await done.Task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert(result.Complete && result.Text == "记忆化" && probe.Calls == 2 && !probe.Requests[1].ExplainTechnical);
            }
        });
        await check("A second invalid local result fails visibly, is never cached, and cannot loop", async () =>
        {
            var probe = new Probe((_, _) => "how"); var done = Completion(); var settings = new AppSettings();
            using var coordinator = new TranslationCoordinator(probe, update => { if (update.Complete || update.Error is not null) done.TrySetResult(update); });
            for (int attempt = 0; attempt < 2; attempt++)
            {
                done = Completion(); coordinator.Submit("how", settings, ""); var result = await done.Task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert(!result.Complete && result.Error is not null && result.Text == "" && probe.Calls == 2 * (attempt + 1));
            }
        });
        await check("Online malformed translations are not automatically retried even if a provider advertises repair", async () =>
        {
            var probe = new Probe((_, _) => "how"); var done = Completion();
            using var coordinator = new TranslationCoordinator(probe, update => { if (update.Complete || update.Error is not null) done.TrySetResult(update); });
            coordinator.Submit("how", new AppSettings { TranslationMode = "online", Model = "fixture" }, "fake");
            var result = await done.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert(result.Error is not null && !result.Complete && result.Text == "" && probe.Calls == 1);
        });
        await check("Canceling during local repair releases the pending request and cannot revive the old word", async () =>
        {
            var probe = new Probe((request, _) => request.Text == "hello" ? "你好" : "how") { WaitDuringRepair = true };
            var done = Completion(); var completed = new List<string>();
            using var coordinator = new TranslationCoordinator(probe, update => { if (update.Complete) { completed.Add(update.Text); done.TrySetResult(update); } });
            coordinator.Submit("how", new AppSettings(), ""); await probe.RepairStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            coordinator.Invalidate(); coordinator.Submit("hello", new AppSettings(), "");
            var result = await done.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert(result.Text == "你好" && completed.SequenceEqual(new[] { "你好" }) && probe.Calls == 3);
        });
    }
    static TaskCompletionSource<TranslationUpdate> Completion() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    static void Assert(bool value) { if (!value) throw new Exception("Translation quality assertion failed"); }
    sealed class Probe(Func<TranslationRequest, int, string> response) : ITranslationProvider
    {
        public int Calls; public bool ThrowFirstEmpty, WaitDuringRepair;
        public List<TranslationRequest> Requests = new();
        public TaskCompletionSource RepairStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CanRepairTranslation(AppSettings settings) => true;
        public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            int call = ++Calls; Requests.Add(request); await Task.Yield();
            if (request.RefineTranslation && WaitDuringRepair) { RepairStarted.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }
            if (call == 1 && ThrowFirstEmpty) throw new TranslationException("local-empty", "Synthetic empty result");
            yield return response(request, call);
        }
    }
}
