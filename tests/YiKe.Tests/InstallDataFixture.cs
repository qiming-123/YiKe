using System.IO;
using YiKe.Core;
using YiKe.Data;
using YiKe.UI;

public static class InstallDataFixture
{
    const string Marker="YiKe installer QA fixture";
    public static int Run(string mode, string? destination = null)
    {
        var store=new SettingsStore(baseDirectory:Environment.CurrentDirectory);var credentials=new CredentialStore(store.UserDirectory);
        if(mode=="seed")
        {
            if(File.Exists(store.UserFile))throw new Exception("Refusing to overwrite pre-existing user settings.");
            var config=new AppSettings{Model=Marker,BaseUrl="https://not-a-service.invalid/v1",Preset="自定义兼容服务",Theme="dark",Enabled=false,ExcludedProcesses=new[]{"fixture-excluded-process"}};
            credentials.Save(config,"fake-installer-test-key");store.Save(config);Console.WriteLine("Seeded synthetic installer data; automatic translation disabled.");
        }
        else if(mode=="verify")
        {
            var config=store.Load();if(!store.Found || config.Model!=Marker || config.Theme!="dark" || config.Enabled || !config.ExcludedProcesses.Contains("fixture-excluded-process") || credentials.Read(config)!="fake-installer-test-key")throw new Exception("Upgrade did not preserve QA settings/key.");Console.WriteLine("PASS installer upgrade/reinstall preserved synthetic settings and DPAPI credential.");
        }
        else if(mode=="move-custom")
        {
            if(string.IsNullOrWhiteSpace(destination)||!Path.IsPathFullyQualified(destination))throw new Exception("Custom fixture directory must be absolute.");
            var config=store.Load();if(!store.Found||config.Model!=Marker)throw new Exception("Refusing to move non-fixture settings.");
            store.ChangeDirectory(destination,config);new CredentialStore(store.UserDirectory).Save(config,"fake-installer-test-key");
            StudyBook.Add(Path.Combine(store.UserDirectory,"YiKe-vocabulary.json"),"bank");File.WriteAllText(Path.Combine(store.UserDirectory,"keep.txt"),Marker);
            Console.WriteLine("Moved synthetic settings/key and added a favorite plus an unrelated file in the custom directory.");
        }
        else if(mode=="cleanup")
        {
            var config=store.Load();if(config.Model!=Marker)throw new Exception("Refusing to remove non-fixture settings.");credentials.Delete(config);File.Delete(store.UserFile);string dir=Path.Combine(store.UserDirectory,"credentials");if(Directory.Exists(dir)&&!Directory.EnumerateFileSystemEntries(dir).Any())Directory.Delete(dir);if(Directory.Exists(store.UserDirectory)&&!Directory.EnumerateFileSystemEntries(store.UserDirectory).Any())Directory.Delete(store.UserDirectory);Console.WriteLine("Removed only the synthetic QA fixture.");
        }
        return 0;
    }
}
