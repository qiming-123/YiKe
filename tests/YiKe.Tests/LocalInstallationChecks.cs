using System.IO;
using System.Text;
using YiKe.Core;
using YiKe.Translation;

static class LocalInstallationChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check, string scratch)
    {
        ulong gb = LocalHardware.GiB;
        var powerful = new LocalHardwareSnapshot(32 * gb, 10 * gb, 16, true);
        string Root(string name)
        {
            string root = Path.Combine(scratch, "installation-" + name, "local-runtime");
            Directory.CreateDirectory(Path.Combine(root, "models")); return root;
        }
        void Model(string root, string file) => File.WriteAllText(Path.Combine(root, file), "presence fixture; integrity is tested separately");
        void Choice(string root, string choice) => File.WriteAllText(Path.Combine(root, "..", "local-model.ini"), "[LocalModel]\nSelection=" + choice);
        await check("Installer recommendation uses memory, free memory, CPU count and compatibility without loading a model", () =>
        {
            var devices = new[] { (powerful, "standard"), (new LocalHardwareSnapshot(8 * gb, 3 * gb, 4, true), "light"), (powerful with { AvailableBytes = 3 * gb }, "light"), (powerful with { AvailableBytes = gb }, "none"), (powerful with { TotalBytes = 4 * gb }, "none"), (powerful with { CompatibleCpu = false }, "none"), (powerful with { Processors = 2 }, "none") };
            foreach (var (device, expected) in devices)
            {
                string output = Path.Combine(scratch, "installer-info.ini"); LocalInstallation.WriteInstallerInfo(output, device);
                string text = File.ReadAllText(output); Assert(text.Contains("Recommended=" + expected) && text.Contains("Description="));
            }
            return Task.CompletedTask;
        });
        await check("A light-only installation uses its available model on a powerful computer", () =>
        {
            string root = Root("light-only"); Model(root, LocalModelPackage.LightModelFile);
            Assert(LocalInstallation.Evaluate(powerful, root).Profile?.ModelId == LocalModelPackage.LightModelId);
            return Task.CompletedTask;
        });
        await check("An explicit light installation remains light even when the standard model is also present", () =>
        {
            string root = Root("explicit-light"); Model(root, LocalModelPackage.LightModelFile); Model(root, LocalModelPackage.ModelFile); Choice(root, "light");
            Assert(LocalInstallation.Evaluate(powerful, root).Profile?.ModelId == LocalModelPackage.LightModelId); return Task.CompletedTask;
        });
        await check("Explicit standard models remain subject to permanent and current-memory limits", () =>
        {
            string root = Root("standard-guard"); Model(root, LocalModelPackage.LightModelFile); Model(root, LocalModelPackage.ModelFile); Choice(root, "standard");
            Assert(LocalInstallation.Evaluate(powerful, root).Profile?.ModelId == LocalModelPackage.ModelId);
            Assert(LocalInstallation.Evaluate(powerful with { AvailableBytes = 3 * gb }, root) is { Supported: false, Permanent: false });
            Assert(LocalInstallation.Evaluate(powerful with { TotalBytes = 8 * gb }, root) is { Supported: false, Permanent: true });
            Assert(!LocalInstallation.Evaluate(powerful with { Processors = 4 }, root).Supported); return Task.CompletedTask;
        });
        await check("Automatic selection cannot promote an 8 GB computer to its only installed heavy model", () =>
        {
            string root = Root("no-promotion"); Model(root, LocalModelPackage.ModelFile);
            Assert(!LocalInstallation.Evaluate(new(8 * gb, 3 * gb, 4, true), root).Supported); return Task.CompletedTask;
        });
        await check("Skipping models produces a specific missing-model result before any engine or API request", async () =>
        {
            string root = Root("no-models"); Assert(!LocalInstallation.HasAnyModel(root));
            Assert(LocalInstallation.Evaluate(powerful, root).Error == LocalInstallation.MissingMessage);
            using var provider = new LocalTranslationProvider(root, () => powerful);
            try { await provider.PrepareAsync(default); throw new Exception("Missing model was loaded"); }
            catch (TranslationException error) { Assert(error.Code == "local-model-missing" && provider.EngineProcessId == 0); }
        });
        await check("Model preference parsing is bounded, scoped to its section and rejects arbitrary values", () =>
        {
            string root = Root("invalid-plan"), file = Path.Combine(root, "..", "local-model.ini");
            foreach (string content in new[] { "[Other]\nSelection=standard", "[LocalModel]\nSelection=../../foreign", new string('x', 4097) })
            { File.WriteAllText(file, content); Assert(LocalInstallation.Selection(root) == "auto"); }
            File.WriteAllText(file, "[LocalModel]\r\nSelection=LIGHT\r\n", Encoding.Unicode); Assert(LocalInstallation.Selection(root) == "light"); return Task.CompletedTask;
        });
        await check("Model changes and single-model availability produce distinct cache identities", () =>
        {
            string root = Root("cache"); using var provider = new LocalTranslationProvider(root, () => powerful);
            Assert(provider.CacheIdentity(new AppSettings()) == "local-unavailable");
            Model(root, LocalModelPackage.LightModelFile); Assert(provider.CacheIdentity(new AppSettings()) == LocalModelPackage.LightModelId);
            Model(root, LocalModelPackage.ModelFile); Assert(provider.CacheIdentity(new AppSettings()) == LocalModelPackage.ModelId);
            Choice(root, "light"); Assert(provider.CacheIdentity(new AppSettings()) == LocalModelPackage.LightModelId); return Task.CompletedTask;
        });
    }
    static void Assert(bool value) { if (!value) throw new Exception("Optional local model assertion failed"); }
}
