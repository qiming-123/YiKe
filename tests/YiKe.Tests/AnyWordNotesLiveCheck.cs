using System.Diagnostics;
using System.IO;
using System.Text.Json;
using YiKe.Core;
using YiKe.Translation;

static class AnyWordNotesLiveCheck
{
    public static async Task<int> Run(string directory, string output, bool light)
    {
        using var local = new LocalTranslationProvider(directory, light ? () => new(8 * LocalHardware.GiB, 3 * LocalHardware.GiB, 4, true) : null);
        var provider = new Counted(local); var rows = new List<object>();
        var done = Completion(); var watch = new Stopwatch(); long firstCompletedMs = -1;
        using var coordinator = new TranslationCoordinator(provider, update =>
        {
            if (update.Complete && firstCompletedMs < 0) firstCompletedMs = watch.ElapsedMilliseconds;
            if ((update.Complete && !update.ExplanationPending) || update.Error is not null) done.TrySetResult(update);
        }, useCache: false);
        try
        {
            foreach (string word in new[] { "how", "unmark", "squares", "banana" })
            {
                done = Completion(); firstCompletedMs = -1; int before = provider.Calls; watch.Restart();
                coordinator.Submit(word, new AppSettings(), "");
                var result = await done.Task.WaitAsync(TimeSpan.FromSeconds(60));
                bool model = result.ProgrammingNotes.Contains("模型补充");
                if (result.Error is not null || !result.Complete || !result.Text.Any(ch => ch >= '\u3400' && ch <= '\u9fff') || !result.ProgrammingNotes.Contains("示例") || !result.ProgrammingNotes.Contains("效果")) throw new Exception("Any-word note missing: " + word + " / " + result.Error);
                if (light ? model || provider.Calls - before != 1 || local.EngineProcessId != 0 : !model || provider.Calls - before != 2) throw new Exception("Wrong explanation route: " + word + " / " + result.ProgrammingNotes);
                if (firstCompletedMs < 0 || firstCompletedMs > watch.ElapsedMilliseconds) throw new Exception("Translation completion was lost");
                rows.Add(new { word, translation = result.Text, notes = result.ProgrammingNotes, firstTranslationCompleteMs = firstCompletedMs, totalMs = watch.ElapsedMilliseconds, calls = provider.Calls - before, explanationModelUsed = model });
            }
            File.WriteAllText(output, JsonSerializer.Serialize(new { version = typeof(TranslationCoordinator).Assembly.GetName().Version!.ToString(), lightHardwareSnapshotSimulated = light, dictionaryEntries = BundledEnglishDictionary.Count, translationShownBeforeExplanation = true, noModelStartedForLightNotes = light && local.EngineProcessId == 0, noExternalEndpoint = true, cases = rows }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("PASS any-word computing notes with " + (light ? "offline fallback and no loaded model" : "a separate real standard-model explanation")); return 0;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
    }
    static TaskCompletionSource<TranslationUpdate> Completion() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    sealed class Counted(ITranslationProvider provider) : ITranslationProvider
    {
        public int Calls;
        public string CacheIdentity(AppSettings settings) => provider.CacheIdentity(settings);
        public bool CanExplainTechnical(AppSettings settings) => provider.CanExplainTechnical(settings);
        public bool CanRepairTranslation(AppSettings settings) => provider.CanRepairTranslation(settings);
        public bool CanExplainAnyWord(AppSettings settings) => provider.CanExplainAnyWord(settings);
        public IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, CancellationToken token) { Calls++; return provider.TranslateAsync(request, settings, key, token); }
    }
}
