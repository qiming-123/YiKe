using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using YiKe.Core;
using YiKe.Translation;

static class WordTranslationLiveCheck
{
    public static async Task<int> Run(string directory, string output, bool light, bool probe = false)
    {
        if (!light && !LiveModelRequirement.StandardAvailable(directory, output)) return 77;
        using var local = new LocalTranslationProvider(directory, light ? () => new(8 * LocalHardware.GiB, 3 * LocalHardware.GiB, 4, true) : null);
        var rows = new List<object>(); var knownLightIssues = new List<string>();
        try
        {
            await local.PrepareAsync(default);
            if (!light && !LiveModelRequirement.LoadedStandard(local, output)) return 77;
            if (probe)
            {
                foreach (string source in new[] { "how", "unmark", "squares", "square" })
                foreach (bool technical in new[] { false, true })
                {
                    var request = new TranslationRequest(1, source, "auto", "简体中文", Guid.NewGuid(), Guid.NewGuid(), LocalModelPackage.ModelId, ExplainTechnical: technical);
                    var result = new TechnicalOutput(technical); var watch = Stopwatch.StartNew();
                    await foreach (string delta in local.TranslateAsync(request, new AppSettings { ExplainProgrammingTerms = true, SupplementTechnicalTerms = true }, "", default)) result.Append(delta);
                    result.Finish();
                    rows.Add(new { source, technical, text = result.Text, notes = result.Notes, totalMs = watch.ElapsedMilliseconds });
                }
            }
            else
            {
                var provider = new Counted(local);
                var complete = new TaskCompletionSource<TranslationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var coordinator = new TranslationCoordinator(provider, update => { if (update.Complete || update.Error is not null) complete.TrySetResult(update); }, useCache: false);
                foreach (var sample in new[]
                {
                    (Text: "how", Meaning: new[] { "如何", "怎样", "怎么", "多么" }),
                    (Text: "unmark", Meaning: new[] { "取消", "去除", "移除", "解除" }),
                    (Text: "squares", Meaning: new[] { "方", "平方", "广场" }),
                    (Text: "square", Meaning: new[] { "方", "平方", "广场" }),
                    (Text: "neighboring", Meaning: new[] { "邻", "附近" }),
                    (Text: "uncover", Meaning: new[] { "揭", "露", "发现", "翻" }),
                    (Text: "diagonally", Meaning: new[] { "斜", "对角" }),
                    (Text: "randomly", Meaning: new[] { "随机", "随意" }),
                    (Text: "surrounding", Meaning: new[] { "周围", "围绕", "环绕" }),
                    (Text: "suspected", Meaning: new[] { "怀疑", "疑", "可疑" }),
                    (Text: "approximately", Meaning: new[] { "大约", "约", "近似" }),
                    (Text: "demonstrates", Meaning: new[] { "演示", "展示", "显示", "示范", "表明", "证明" }),
                    (Text: "entries", Meaning: new[] { "项", "条", "入口", "进入" }),
                    (Text: "Press F to mark or unmark a suspected mine.", Meaning: new[] { "标记" }),
                    (Text: "The board has 9 × 9 squares, with 10 randomly placed mines hidden inside.", Meaning: new[] { "方格", "格子", "正方形" })
                })
                {
                    complete = new(TaskCreationOptions.RunContinuationsAsynchronously); int before = provider.Calls;
                    var watch = Stopwatch.StartNew(); coordinator.Submit(sample.Text, new AppSettings { ExplainProgrammingTerms = true, SupplementTechnicalTerms = false }, "");
                    var update = await complete.Task.WaitAsync(TimeSpan.FromSeconds(90));
                    bool sentence = sample.Text.StartsWith("Press F", StringComparison.Ordinal) || sample.Text.StartsWith("The board", StringComparison.Ordinal);
                    bool meaning = sample.Meaning.Any(update.Text.Contains);
                    if (update.Error is not null || !update.Complete || (!sentence && !meaning) || !update.Text.Any(ch => ch >= '\u3400' && ch <= '\u9fff') || update.ProgrammingNotes.Length == 0)
                        throw new Exception("Ordinary word failed: " + sample.Text + " / " + update.Text + " / " + update.Error + " / " + update.ProgrammingNotes);
                    if (provider.Calls - before > 2) throw new Exception("Unbounded word repair");
                    bool contentCheck = meaning;
                    if (sample.Text.StartsWith("Press F", StringComparison.Ordinal)) contentCheck &= update.Text.Contains('F') && update.Text.Contains("取消");
                    if (sample.Text.StartsWith("The board", StringComparison.Ordinal)) contentCheck &= update.Text.Contains('9') && update.Text.Contains("10") && update.Text.Contains("雷");
                    if (sentence && !contentCheck)
                    {
                        if (!light) throw new Exception("Standard model lost the checked action, key or numbers: " + update.Text);
                        knownLightIssues.Add(sample.Text + " -> " + update.Text);
                    }
                    rows.Add(new { source = sample.Text, text = update.Text, notes = update.ProgrammingNotes, checkedContentPreserved = contentCheck, origin = CommonWordTranslations.Find(sample.Text, "简体中文") is not null ? "bundled-dictionary" : "local-model", calls = provider.Calls - before, totalMs = watch.ElapsedMilliseconds });
                }
            }
            File.WriteAllText(output, JsonSerializer.Serialize(new { model = local.ActiveProfile!.ModelId, probe, dictionaryEntries = BundledEnglishDictionary.Count, reviewedOverrides = CommonWordTranslations.Count, lightHardwareSnapshotSimulated = light, knownLightSentenceContentFailures = knownLightIssues, cases = rows }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(probe ? "Recorded ordinary-word translation probe" : "PASS dictionary words and model response format; known light sentence-content failures: " + knownLightIssues.Count); return 0;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
    }
    sealed class Counted(ITranslationProvider provider) : ITranslationProvider
    {
        public int Calls;
        public string CacheIdentity(AppSettings settings) => provider.CacheIdentity(settings);
        public bool CanExplainTechnical(AppSettings settings) => provider.CanExplainTechnical(settings);
        public bool CanRepairTranslation(AppSettings settings) => provider.CanRepairTranslation(settings);
        public IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, CancellationToken token)
        { Calls++; return provider.TranslateAsync(request, settings, key, token); }
    }
}
