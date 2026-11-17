using System.Text.Json;
using YiKe.Core;
using YiKe.Translation;

public static class ProgrammingGlossaryChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check)
    {
        await check("Programming keywords expose distinct cross-language semantics offline", () =>
        {
            string stat = ProgrammingGlossary.Explain("static");
            Assert(stat.Contains("C/C++") && stat.Contains("Java / C#") && stat.Contains("Rust") && stat.Contains("不表示值不可改变"));
            string constant = ProgrammingGlossary.Explain("const");
            Assert(constant.Contains("对象内部仍可能改变") && constant.Contains("Java 的 const 是保留字"));
            Assert(ProgrammingGlossary.Explain("var").Contains("仍是静态类型"));
            Assert(ProgrammingGlossary.Explain("await").Contains("不等于阻塞整个线程"));
            foreach (string term in new[] { "let", "None", "int", "void", "sizeof", "fn", "func", "泛型", "pointer", "dictionary", "协程" }) Assert(ProgrammingGlossary.Explain(term).Length > 0);
            return Task.CompletedTask;
        });
        await check("Glossary identifies code, marked terms and programming prose without ordinary sentence noise", () =>
        {
            foreach (string text in new[] { "static int count = 0;", "Python uses return to leave a function.", "The `static` modifier", "async function load() { return await fetch(); }", "C++ 的 static 用法", "const value = 42" }) Assert(ProgrammingGlossary.Explain(text).Length > 0);
            foreach (string text in new[] { "Please return before class.", "Let us go to the new class.", "None of us will continue.", "The channel shows a function at the school.", "The static on the radio is loud.", "The map shows the river.", "明天上午九点开会。", "unknownLibrarySpecificWord" }) Assert(ProgrammingGlossary.Explain(text) == "");
            return Task.CompletedTask;
        });
        await check("Glossary respects identifier boundaries, keyword case, deduplication and output bounds", () =>
        {
            foreach (string text in new[] { "static_cast", "my_static", "awaitable", "className", "_return", "asyncé", "ASYNC" }) Assert(ProgrammingGlossary.Explain(text) == "");
            string repeat = ProgrammingGlossary.Explain("static int x; static int y;"); Assert(repeat.Split("static · 静态").Length == 2);
            string many = ProgrammingGlossary.Explain("public static async class Sample { const int value = 1; return await call(); }");
            Assert(many.Contains("另有") && many.IndexOf("可见性", StringComparison.Ordinal) < many.IndexOf("static ·", StringComparison.Ordinal) && many.Length < 5000);
            Assert(ProgrammingGlossary.Explain(new string('x', 2001)) == "");
            return Task.CompletedTask;
        });
        await check("Programming preference defaults on for legacy configs and roundtrips disabled", () =>
        {
            Assert(JsonSerializer.Deserialize<AppSettings>("{}")!.ExplainProgrammingTerms);
            var disabled = new AppSettings { ExplainProgrammingTerms = false };
            Assert(!JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(disabled))!.Validate().ExplainProgrammingTerms);
            return Task.CompletedTask;
        });
        await check("Glossary stays separate from translation, adds no requests and refreshes on cached results", async () =>
        {
            var provider = new Probe();
            var result = new TaskCompletionSource<TranslationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
            var settings = new AppSettings();
            using var coordinator = new TranslationCoordinator(provider, update => { if (update.Complete) result.TrySetResult(update); });
            foreach (bool enabled in new[] { true, false, true })
            {
                result = new(TaskCreationOptions.RunContinuationsAsynchronously);
                coordinator.Submit("static", settings with { ExplainProgrammingTerms = enabled }, "");
                var update = await result.Task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert(update.Text == "静态" && (update.ProgrammingNotes.Length > 0) == enabled);
            }
            Assert(provider.Calls == 1 && provider.Source == "static");
        });
        await check("Glossary remains readable on provider failure without claiming translation completion", async () =>
        {
            var provider = new Probe { Fail = true };
            var result = new TaskCompletionSource<TranslationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var coordinator = new TranslationCoordinator(provider, update => { if (update.Error is not null) result.TrySetResult(update); });
            coordinator.Submit("const", new AppSettings(), "");
            var update = await result.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert(!update.Complete && update.Text == "" && update.ProgrammingNotes.Contains("JavaScript") && provider.Calls == 1);
        });
    }
    static void Assert(bool value) { if (!value) throw new Exception("Programming glossary assertion failed"); }
    sealed class Probe : ITranslationProvider
    {
        public int Calls; public string Source = ""; public bool Fail;
        public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            Calls++; Source = request.Text; await Task.Yield();
            if (Fail) throw new TranslationException("fixture", "Synthetic provider unavailable");
            yield return "静态";
        }
    }
}
