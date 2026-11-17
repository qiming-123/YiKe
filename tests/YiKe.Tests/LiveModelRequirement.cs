using System.IO;
using System.Text.Json;
using YiKe.Translation;

static class LiveModelRequirement
{
    internal static bool LoadedStandard(LocalTranslationProvider provider, string output)
    {
        if (provider.ActiveProfile?.ModelId == LocalModelPackage.ModelId) return true;
        const string reason = "Available memory changed before model loading; the runtime actually selected the lightweight model.";
        File.WriteAllText(output, JsonSerializer.Serialize(new { skipped = true, reason, actualSelectedModel = provider.ActiveProfile?.ModelId }));
        Console.WriteLine("SKIP standard-model live check: " + reason); return false;
    }
    internal static bool StandardAvailable(string directory, string output)
    {
        var device = LocalHardware.Detect();
        var selected = LocalInstallation.Evaluate(device, directory).Profile;
        if (selected?.ModelId == LocalModelPackage.ModelId) return true;
        const string reason = "Current hardware/memory policy does not select the standard model; not tested by forcing a fictitious memory allowance.";
        File.WriteAllText(output, JsonSerializer.Serialize(new { skipped = true, reason, actualSelectedModel = selected?.ModelId, availableBytes = device.AvailableBytes }));
        Console.WriteLine("SKIP standard-model live check: " + reason);
        return false;
    }
}
