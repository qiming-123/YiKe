using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YiKe.Core;
using YiKe.Translation;

public static class LocalModelChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check, string scratch)
    {
        await check("Hardware policy selects light or standard and protects low-memory and low-CPU devices", () =>
        {
            ulong gb = LocalHardware.GiB;
            var light = LocalHardware.Evaluate(new(8 * gb, 3 * gb, 4, true));
            Assert(light.Profile?.ModelId == LocalModelPackage.LightModelId && light.Profile.Threads == 2);
            var standard = LocalHardware.Evaluate(new(16 * gb, 7 * gb, 8, true));
            Assert(standard.Profile?.ModelId == LocalModelPackage.ModelId && standard.Profile.Threads <= 6 && standard.Profile.Threads < 8);
            Assert(LocalHardware.Evaluate(new(32 * gb, 3 * gb, 24, true)).Profile?.ModelId == LocalModelPackage.LightModelId);
            Assert(LocalHardware.Evaluate(new(32 * gb, 10 * gb, 4, true)).Profile?.ModelId == LocalModelPackage.LightModelId);
            foreach (var device in new[] { new LocalHardwareSnapshot(4 * gb, 3 * gb, 8, true), new(16 * gb, 8 * gb, 2, true), new(16 * gb, 8 * gb, 8, false) }) Assert(LocalHardware.Evaluate(device) is { Supported: false, Permanent: true });
            Assert(LocalHardware.Evaluate(new(8 * gb, LocalHardware.MinimumAvailable - 1, 4, true)) is { Supported: false, Permanent: false });
            Assert(LocalHardware.Evaluate(new(8 * gb, LocalHardware.MinimumAvailable, 4, true)).Supported);
            Assert(LocalHardware.Evaluate(new(0, 0, 8, true)) is { Supported: false, Permanent: false });
            return Task.CompletedTask;
        });
        await check("Unsupported hardware and temporary memory pressure are rejected before runtime files or engine startup", async () =>
        {
            ulong gb = LocalHardware.GiB;
            foreach (var entry in new[] { (new LocalHardwareSnapshot(4 * gb, 3 * gb, 8, true), "local-hardware"), (new LocalHardwareSnapshot(16 * gb, gb, 8, true), "local-memory") })
            {
                using var provider = new LocalTranslationProvider(Path.Combine(scratch, "nonexistent-runtime"), () => entry.Item1);
                try { await provider.PrepareAsync(default); throw new Exception("Unsupported device loaded"); }
                catch (TranslationException error) { Assert(error.Code == entry.Item2 && provider.EngineProcessId == 0); }
            }
        });
        await check("Changing the actual local model cannot reuse another model's cached translation", async () =>
        {
            var provider = new VariantProvider();
            var settings = new AppSettings();
            var completion = new TaskCompletionSource<TranslationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var coordinator = new TranslationCoordinator(provider, update => { if (update.Complete) completion.TrySetResult(update); });
            foreach (string model in new[] { "light", "standard", "light" })
            {
                provider.Model = model; completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                coordinator.Submit("same source", settings, "");
                Assert((await completion.Task.WaitAsync(TimeSpan.FromSeconds(3))).Text == model);
            }
            Assert(provider.Calls == 2);
        });
        await check("Fresh and legacy settings default to local translation without an API key or model name", () =>
        {
            Assert(new AppSettings().Validate(true).TranslationMode == "local");
            var legacy = JsonSerializer.Deserialize<AppSettings>("{\"Model\":\"previous-cloud-model\",\"BaseUrl\":\"https://example.test/v1\"}")!;
            Assert(legacy.Validate(true).TranslationMode == "local" && legacy.Model == "previous-cloud-model");
            try { (legacy with { TranslationMode = "online", Model = "" }).Validate(true); throw new Exception("Blank online model accepted"); } catch (ArgumentException) { }
            try { (legacy with { TranslationMode = "invalid" }).Validate(); throw new Exception("Invalid mode accepted"); } catch (ArgumentException) { }
            return Task.CompletedTask;
        });
        await check("Local translation is isolated from online credentials and never falls back to the network", async () =>
        {
            var online = new ProbeProvider(); var local = new ProbeProvider(fail: true);
            using var routed = new TranslationProvider(online, local);
            var settings = new AppSettings();
            var request = new TranslationRequest(1, "fixture", "auto", "简体中文", settings.ConfigId, settings.ServiceId, "");
            try { await foreach (var _ in routed.TranslateAsync(request, settings, "fake-private-cloud-key", default)) { } } catch (TranslationException) { }
            Assert(local.Calls == 1 && local.Key == "" && online.Calls == 0);
            await foreach (var _ in routed.TranslateAsync(request, settings with { TranslationMode = "online" }, "fake-private-cloud-key", default)) { }
            Assert(online.Calls == 1 && online.Key == "fake-private-cloud-key");
        });
        await check("Local package verification rejects tampering, missing files, path escapes and unexpected DLLs", async () =>
        {
            string root = Path.Combine(scratch, "local-files"); Directory.CreateDirectory(Path.Combine(root, "engine"));
            byte[] fixture = Encoding.UTF8.GetBytes("owned-fixture"); string file = Path.Combine(root, "engine", "fixture.dll"); File.WriteAllBytes(file, fixture);
            var manifest = new LocalPackageManifest("test", "test", new[] { new LocalPackageFile("engine/fixture.dll", fixture.Length, Convert.ToHexString(SHA256.HashData(fixture))) });
            await LocalModelPackage.VerifyAsync(root, manifest, default);
            File.WriteAllText(Path.Combine(root, "engine", "injected.dll"), "unexpected"); await Reject(() => LocalModelPackage.VerifyAsync(root, manifest, default)); File.Delete(Path.Combine(root, "engine", "injected.dll"));
            File.WriteAllText(file, "tampered"); await Reject(() => LocalModelPackage.VerifyAsync(root, manifest, default));
            File.Delete(file); await Reject(() => LocalModelPackage.VerifyAsync(root, manifest, default));
            await Reject(() => LocalModelPackage.VerifyAsync(root, manifest with { Files = new[] { manifest.Files[0] with { Path = "../outside.dll" } } }, default));
        });
        await check("Local and online results use separate cache entries", async () =>
        {
            var local = new ProbeProvider(); var online = new ProbeProvider(); using var routed = new TranslationProvider(online, local);
            var finished = new TaskCompletionSource<TranslationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var coordinator = new TranslationCoordinator(routed, result => { if (result.Complete) finished.TrySetResult(result); });
            var settings = new AppSettings { Model = LocalModelPackage.ModelId };
            foreach (string mode in new[] { "local", "online", "local" })
            {
                finished = new(TaskCreationOptions.RunContinuationsAsynchronously); coordinator.Submit("fixture", settings with { TranslationMode = mode }, "fake");
                await finished.Task.WaitAsync(TimeSpan.FromSeconds(3));
            }
            Assert(local.Calls == 1 && online.Calls == 1);
        });
        await check("Local engine listener checks reject unrelated ports", () => { Assert(!OwnedLocalProcess.OwnsPort(1, Environment.ProcessId)); return Task.CompletedTask; });
    }
    static void Assert(bool condition) { if (!condition) throw new Exception("Local model assertion failed"); }
    static async Task Reject(Func<Task> action) { try { await action(); } catch (TranslationException) { return; } throw new Exception("Invalid local package accepted"); }
    sealed class ProbeProvider(bool fail = false) : ITranslationProvider
    {
        public int Calls; public string? Key;
        public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
            { Calls++; Key = key; await Task.Yield(); if (fail) throw new TranslationException("local-fixture", "fixture failure"); yield return "夹具译文"; }
    }
    sealed class VariantProvider : ITranslationProvider
    {
        public string Model = "light"; public int Calls;
        public string CacheIdentity(AppSettings settings) => Model;
        public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        { Calls++; await Task.Yield(); yield return Model; }
    }
}
