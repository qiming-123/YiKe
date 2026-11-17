using System.IO;
using YiKe.Core;
using YiKe.Data;

// Explicit, reversible desktop acceptance fixture: never contains a service key or sends a request.
public static class DesktopDataFixture
{
    static readonly Guid Id = Guid.Parse("4791290b-68de-4c40-bc3d-853148ff8297");
    const string Model = "YiKe desktop selection QA - no API key";
    public static int Run(string mode)
    {
        var store = new SettingsStore();
        if (mode == "seed")
        {
            if (File.Exists(store.UserFile) || Directory.Exists(Path.Combine(store.UserDirectory, "credentials"))) throw new InvalidOperationException("Existing user data must be preserved.");
            store.Save(new AppSettings { ConfigId = Id, Model = Model, BaseUrl = "https://not-a-service.invalid/v1", Preset = "自定义兼容服务", Theme = "light", Enabled = true });
            Console.WriteLine("Desktop fixture seeded without any API credential.");
        }
        else if (mode == "cleanup")
        {
            var value = store.Load();
            if (!store.Found || value.ConfigId != Id || value.Model != Model) throw new InvalidOperationException("Refusing to remove unrelated settings.");
            File.Delete(store.UserFile);
            if (!Directory.EnumerateFileSystemEntries(store.UserDirectory).Any()) Directory.Delete(store.UserDirectory);
            Console.WriteLine("Removed only the identified desktop fixture.");
        }
        else throw new ArgumentException("Expected seed or cleanup.");
        return 0;
    }
}
