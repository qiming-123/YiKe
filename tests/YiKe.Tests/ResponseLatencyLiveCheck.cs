using System.Diagnostics;
using System.IO;
using System.Text.Json;
using YiKe.Core;
using YiKe.Translation;

static class ResponseLatencyLiveCheck
{
    public static async Task<int> Run(string directory, string output, bool light)
    {
        using var local = new LocalTranslationProvider(directory, light ? () => new(8 * LocalHardware.GiB, 3 * LocalHardware.GiB, 4, true) : null);
        var provider = new Counted(local);
        try
        {
            await local.PrepareAsync(default);
            var first = Done(); var complete = Done(); var watch = Stopwatch.StartNew();
            using var coordinator = new TranslationCoordinator(provider, update =>
            {
                if (update.Text.Length > 0) first.TrySetResult(update);
                if (update.Complete && !update.ExplanationPending) complete.TrySetResult(update);
            }, useCache: false);
            const string paragraph = "The meeting starts at nine tomorrow morning. Please save your changes before closing the window. Keep the file open until the backup finishes.";
            var settings = new AppSettings { ExplainProgrammingTerms = false };
            long sequence = coordinator.Submit(paragraph, settings, "");
            await first.Task.WaitAsync(TimeSpan.FromSeconds(45)); long firstMs = watch.ElapsedMilliseconds;
            for (int i = 0; i < 5; i++) if (coordinator.Submit(paragraph, settings, "") != sequence) throw new Exception("An in-flight repeated paragraph was restarted");
            var result = await complete.Task.WaitAsync(TimeSpan.FromSeconds(60)); long totalMs = watch.ElapsedMilliseconds;
            if (result.Error is not null || !result.Text.Any(ch => ch >= '\u3400' && ch <= '\u9fff') || provider.MainCalls != 1 || provider.Maximum != 1) throw new Exception("Paragraph reuse or output failed");
            object? notesPreemption = null;
            if (!light)
            {
                first = Done(); complete = Done(); coordinator.Submit("how", new AppSettings(), "");
                await provider.NotesStreaming.Task.WaitAsync(TimeSpan.FromSeconds(45));
                first = Done(); complete = Done(); watch.Restart();
                coordinator.Submit("entries", new AppSettings { SupplementTechnicalTerms = false }, "");
                var next = await complete.Task.WaitAsync(TimeSpan.FromSeconds(5));
                notesPreemption = new { cancelToCompletedTranslationMs = watch.ElapsedMilliseconds, provider.CanceledNotes, provider.Maximum, text = next.Text };
                if (!provider.CanceledNotes || next.Error is not null || next.Text.Length == 0 || provider.Maximum != 1) throw new Exception("Old notes blocked the new dictionary selection");
            }
            File.WriteAllText(output, JsonSerializer.Serialize(new { version = typeof(TranslationCoordinator).Assembly.GetName().Version!.ToString(), model = local.ActiveProfile!.ModelId, simulatedLightHardware = light, warmModel = true, firstVisibleTranslationMs = firstMs, paragraphCompleteMs = totalMs, repeatedSelections = 5, paragraphTranslationRequests = 1, noExternalEndpoint = true, translation = result.Text, notesPreemption, semanticAccuracyNotCertified = true }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("PASS real " + (light ? "light" : "standard") + " first-chunk visibility, repeated paragraph reuse and bounded provider execution");
            return 0;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
    }
    static TaskCompletionSource<TranslationUpdate> Done() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    sealed class Counted(ITranslationProvider provider) : ITranslationProvider
    {
        public int MainCalls, Active, Maximum; public bool CanceledNotes;
        public TaskCompletionSource NotesStreaming = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string CacheIdentity(AppSettings settings) => provider.CacheIdentity(settings);
        public bool CanExplainTechnical(AppSettings settings) => provider.CanExplainTechnical(settings);
        public bool CanExplainAnyWord(AppSettings settings) => provider.CanExplainAnyWord(settings);
        public bool CanRepairTranslation(AppSettings settings) => provider.CanRepairTranslation(settings);
        public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            if (!request.NotesOnly) MainCalls++;
            Active++; Maximum = Math.Max(Maximum, Active);
            try
            {
                await foreach (string delta in provider.TranslateAsync(request, settings, key, token))
                {
                    if (request.NotesOnly && delta.Length > 0) NotesStreaming.TrySetResult();
                    yield return delta;
                }
            }
            finally { if (request.NotesOnly && token.IsCancellationRequested) CanceledNotes = true; Active--; }
        }
    }
}
