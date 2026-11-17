using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using YiKe.Core;
using YiKe.Translation;

public static class LocalModelLiveCheck
{
    public static async Task<int> Run(string directory, string output, bool light = false)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        var results = new List<object>(); var startup = Stopwatch.StartNew();
        LocalHardwareSnapshot? deviceOverride = light ? new(8 * LocalHardware.GiB, 3 * LocalHardware.GiB, 4, true) : null;
        var provider = new LocalTranslationProvider(directory, () => deviceOverride ?? LocalHardware.Detect());
        try
        {
            await provider.PrepareAsync(default); long startupMs = startup.ElapsedMilliseconds;
            int engineId = provider.EngineProcessId;
            var profile = provider.ActiveProfile!;
            if (light && profile.ModelId != LocalModelPackage.LightModelId) throw new Exception("Light hardware did not select the lightweight model");
            bool appLocalCrt;
            using (var process = Process.GetProcessById(engineId))
            {
                if (process.PriorityClass != ProcessPriorityClass.BelowNormal) throw new Exception("Engine priority did not leave room for foreground applications");
                var names = new HashSet<string>(new[] { "msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll" }, StringComparer.OrdinalIgnoreCase);
                var modules = process.Modules.Cast<ProcessModule>().Where(module => names.Contains(module.ModuleName)).ToArray();
                appLocalCrt = modules.Length == 3 && modules.All(module => Path.GetFullPath(module.FileName).StartsWith(Path.GetFullPath(Path.Combine(directory, "engine")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                if (!appLocalCrt) Console.WriteLine("CRT module paths: " + string.Join("; ", modules.Select(module => module.FileName)));
            }
            if (!appLocalCrt) throw new Exception("Engine loaded CRT from outside its package");
            var cases = new[]
            {
                ("The meeting starts at nine tomorrow morning.", "简体中文"),
                ("明天上午九点开会。", "英语（English）"),
                ("Please save your changes before closing the window.", "日语（日本語）"),
                ("Thank you for your help.", "法语（Français）"),
                ("Connection timeout.\nRetry after 10 seconds.\nDo not close the window.", "简体中文")
            };
            foreach (var (text, language) in cases)
            {
                var settings = new AppSettings { TargetLanguage = language, Stream = !text.StartsWith("Connection timeout.", StringComparison.Ordinal) }; var timer = Stopwatch.StartNew(); long first = -1; var answer = new StringBuilder();
                var request = new TranslationRequest(1, text, "auto", language, settings.ConfigId, settings.ServiceId, LocalModelPackage.ModelId);
                await foreach (string delta in provider.TranslateAsync(request, settings, "", default)) { if (first < 0 && !string.IsNullOrWhiteSpace(delta)) first = timer.ElapsedMilliseconds; answer.Append(delta); }
                if (string.IsNullOrWhiteSpace(answer.ToString()) || answer.ToString().Contains("<think>")) throw new Exception("Empty translation or visible reasoning");
                results.Add(new { source = text, target = language, stream = settings.Stream, translation = answer.ToString(), firstTokenMs = first, totalMs = timer.ElapsedMilliseconds });
            }
            using (var cancel = new CancellationTokenSource())
            {
                var settings = new AppSettings();
                var request = new TranslationRequest(1, string.Join("\n", Enumerable.Repeat("Save the document before closing the window.", 15)), "auto", "简体中文", settings.ConfigId, settings.ServiceId, LocalModelPackage.ModelId);
                bool cancelled = false;
                try { await foreach (string delta in provider.TranslateAsync(request, settings, "", cancel.Token)) { if (delta.Length > 0) cancel.Cancel(); } }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested) { cancelled = true; }
                if (!cancelled) throw new Exception("Local generation did not cancel");
                var retry = request with { Text = "Hello, world!" }; var text = new StringBuilder();
                await foreach (string delta in provider.TranslateAsync(retry, settings, "", default)) text.Append(delta);
                if (text.Length == 0) throw new Exception("Local engine did not recover after cancellation");
            }
            var previousDevice = deviceOverride;
            deviceOverride = new(16 * LocalHardware.GiB, LocalHardware.GiB / 2, 8, true);
            bool memoryBlocked = false;
            try { await provider.PrepareAsync(default); } catch (TranslationException error) when (error.Code == "local-memory") { memoryBlocked = true; }
            if (!memoryBlocked || provider.EngineProcessId != 0) throw new Exception("Memory pressure did not release the real engine");
            int verificationPasses = provider.VerificationPasses;
            deviceOverride = previousDevice; await provider.PrepareAsync(default);
            if (provider.VerificationPasses != verificationPasses) throw new Exception("Unchanged model was hashed again after engine release");
            engineId = provider.EngineProcessId;
            provider.Dispose();
            bool stopped;
            try { using var engine = Process.GetProcessById(engineId); stopped = engine.WaitForExit(5000); } catch (ArgumentException) { stopped = true; }
            if (!stopped) throw new Exception("Owned engine remained running");
            await VerifyParentExit(directory, output, light);
            File.WriteAllText(output, JsonSerializer.Serialize(new { model = profile.ModelId, profile = profile.Name, threads = profile.Threads, context = profile.Context, batch = profile.Batch, microBatch = profile.MicroBatch, processPriority = "BelowNormal", pollingDisabled = true, simulatedHardware = light, memoryPressureSimulated = true, engineReleasedOnMemoryPressure = memoryBlocked, reloadWithoutFullRehash = true, startupMs, noExternalEndpoint = true, appLocalCrt, cancellationAndRetry = true, engineStoppedOnDispose = stopped, engineStoppedOnParentCrash = true, cases = results }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            Console.WriteLine("PASS real local CPU translations, app-local CRT, cancellation/retry and normal/crash engine cleanup"); return 0;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
        finally { provider.Dispose(); }
    }
    public static async Task<int> ParentFixture(string directory, string ready, bool light = false)
    {
        using var provider = new LocalTranslationProvider(directory, light ? () => new(8 * LocalHardware.GiB, 3 * LocalHardware.GiB, 4, true) : null); await provider.PrepareAsync(default);
        File.WriteAllText(ready, provider.EngineProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Console.In.ReadLineAsync(); return 0;
    }
    static async Task VerifyParentExit(string directory, string output, bool light)
    {
        string ready = Path.Combine(Path.GetDirectoryName(output)!, "local-parent-" + Guid.NewGuid().ToString("N") + ".txt");
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { light ? "--local-parent-light" : "--local-parent-fixture", directory, ready }) start.ArgumentList.Add(arg);
        using var parent = Process.Start(start)!; Process? engine = null;
        try
        {
            var limit = Stopwatch.StartNew();
            while (!File.Exists(ready)) { if (parent.HasExited || limit.Elapsed > TimeSpan.FromSeconds(30)) throw new Exception("Owned parent fixture failed to load"); await Task.Delay(50); }
            int id = int.Parse(File.ReadAllText(ready), System.Globalization.CultureInfo.InvariantCulture); engine = Process.GetProcessById(id);
            if (!Path.GetFullPath(engine.MainModule!.FileName).Equals(Path.GetFullPath(Path.Combine(directory, "engine", "llama-server.exe")), StringComparison.OrdinalIgnoreCase)) throw new Exception("Unexpected fixture engine identity");
            parent.Kill(entireProcessTree: false); await parent.WaitForExitAsync();
            if (!engine.WaitForExit(5000)) throw new Exception("Job did not stop its engine after the parent crashed");
        }
        finally
        {
            if (!parent.HasExited) { parent.Kill(false); parent.WaitForExit(5000); }
            if (engine is not null) { if (!engine.HasExited) { engine.Kill(false); engine.WaitForExit(5000); } engine.Dispose(); }
            if (File.Exists(ready)) File.Delete(ready);
        }
    }
}
