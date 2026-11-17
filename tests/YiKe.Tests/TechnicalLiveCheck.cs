using System.Diagnostics;
using System.IO;
using System.Text.Json;
using YiKe.Core;
using YiKe.Translation;

public static class TechnicalLiveCheck
{
    public static async Task<int> Run(string directory, string output, bool light)
    {
        if (!light && !LiveModelRequirement.StandardAvailable(directory, output)) return 77;
        using var local = new LocalTranslationProvider(directory, light ? () => new(8 * LocalHardware.GiB, 3 * LocalHardware.GiB, 4, true) : null);
        var provider = new Counted(local); var rows = new List<object>(); int maximumCallsObserved = 0;
        var result = new TaskCompletionSource<TranslationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updates = new List<TranslationUpdate>();
        using var coordinator = new TranslationCoordinator(provider, update => { lock (updates) updates.Add(update); if (update.Complete || update.Error is not null) result.TrySetResult(update); });
        try
        {
            await local.PrepareAsync(default);
            if (!light && !LiveModelRequirement.LoadedStandard(local, output)) return 77;
            foreach (var sample in new[]
            {
                (Text: "chmod 755 script.sh", Smart: true, Stream: true, Expected: "dictionary"),
                (Text: "g++ -std=c++20 -Wall main.cpp -o demo-app", Smart: true, Stream: true, Expected: "dictionary"),
                (Text: "Get-Content -LiteralPath .\\demo\\app.log -Tail 5", Smart: true, Stream: false, Expected: "dictionary"),
                (Text: "DNS", Smart: true, Stream: false, Expected: "dictionary"),
                (Text: "memoization", Smart: true, Stream: true, Expected: "model"),
                (Text: "Linux uses copy-on-write when fork creates a child process.", Smart: true, Stream: false, Expected: "dictionary"),
                (Text: "The meeting starts at nine tomorrow morning.", Smart: true, Stream: true, Expected: "fallback"),
                (Text: "memoization", Smart: false, Stream: true, Expected: "fallback")
            })
            {
                result = new(TaskCreationOptions.RunContinuationsAsynchronously); int before = provider.Calls; var watch = Stopwatch.StartNew();
                coordinator.Submit(sample.Text, new AppSettings { ExplainProgrammingTerms = true, SupplementTechnicalTerms = sample.Smart, Stream = sample.Stream }, "");
                var update = await result.Task.WaitAsync(TimeSpan.FromMinutes(2));
                if (update.Error is not null) throw new Exception(sample.Text + ": " + update.Error);
                if (string.IsNullOrWhiteSpace(update.Text) || update.Text.Contains(TechnicalOutput.Marker) || update.Text.Contains("<think>") || update.Text.Contains("完整译文") || update.ProgrammingNotes.Contains("</YIKE_")) throw new Exception("Translation leaked a format label or was empty");
                bool dictionary = update.ProgrammingNotes.Contains("本机词库") || update.ProgrammingNotes.Contains("本机命令用法"), model = update.ProgrammingNotes.Contains("模型补充");
                if ((sample.Expected is "dictionary" or "both") && !dictionary) throw new Exception("Bundled definition missing");
                if (dictionary && (!update.ProgrammingNotes.Contains("示例：") || !update.ProgrammingNotes.Contains("效果："))) throw new Exception("Bundled example or effect missing");
                if (!light && (sample.Expected is "model" or "both") && !model) throw new Exception("Real local model did not separate its technical explanation: " + update.Text);
                if (!light && sample.Expected == "model" && (!update.ProgrammingNotes.Contains("示例") || !update.ProgrammingNotes.Contains("效果"))) throw new Exception("Real local model did not provide a use case and outcome: " + update.ProgrammingNotes);
                if (light && model) throw new Exception("Light model attempted unreliable technical generation");
                if (sample.Expected == "fallback" && (update.ProgrammingNotes.Length == 0 || model)) throw new Exception("Disabled or ordinary sentence created notes");
                if (provider.Calls < before + 1 || provider.Calls > before + 2) throw new Exception("Unbounded local translation repair");
                maximumCallsObserved = Math.Max(maximumCallsObserved, provider.Calls - before);
                lock (updates) if (updates.Any(item => item.Text.Contains(TechnicalOutput.Marker))) throw new Exception("Stream exposed an internal delimiter");
                rows.Add(new { source = sample.Text, smart = sample.Smart, stream = sample.Stream, expected = sample.Expected, translation = update.Text, notes = update.ProgrammingNotes, providerCalls = provider.Calls - before, totalMs = watch.ElapsedMilliseconds });
            }
            File.WriteAllText(output, JsonSerializer.Serialize(new { model = local.ActiveProfile!.ModelId, simulatedHardware = light, modelSupplementationEnabled = !light, glossaryGroups = ProgrammingGlossary.TermCount, noExternalEndpoint = true, oneRequestPerSelection = maximumCallsObserved == 1, maximumCallsObserved, maximumAllowedCallsPerSelection = 2, boundedLocalRepair = true, delimiterNeverDisplayed = true, cases = rows }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("PASS technical glossary and model supplementation with real " + (light ? "light" : "standard") + " local model"); return 0;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
    }
    sealed class Counted(ITranslationProvider provider) : ITranslationProvider
    {
        public int Calls;
        public string CacheIdentity(AppSettings settings) => provider.CacheIdentity(settings);
        public bool CanExplainTechnical(AppSettings settings) => provider.CanExplainTechnical(settings);
        public bool CanRepairTranslation(AppSettings settings) => provider.CanRepairTranslation(settings);
        public IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, CancellationToken token) { Calls++; return provider.TranslateAsync(request, settings, key, token); }
    }
}
