using System.Text;
using System.IO;
using System.Text.Json;
using YiKe.Core;
using YiKe.Data;

static class OnlineProfileChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check, string scratch)
    {
        void Assert(bool condition) { if (!condition) throw new Exception("Online profile invariant failed"); }
        void Invalid(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Invalid profile accepted"); }
        var store = new SettingsStore(userDirectory: Path.Combine(scratch, "multiple-api"));
        var credentials = new CredentialStore(store.UserDirectory);
        AppSettings original = new() { TranslationMode = "online", Model = "model-a" };
        AppSettings saved = original;
        Guid first = Guid.Empty, second = Guid.Empty;
        await check("Legacy single API upgrades without changing its encrypted credential binding", () =>
        {
            credentials.Save(original, "fake-profile-a"); store.Save(original);
            saved = store.Load(); first = saved.ActiveOnlineProfileId;
            Assert(saved.OnlineProfiles.Length == 1 && saved.ServiceId == original.ServiceId && credentials.Read(saved) == "fake-profile-a");
            Assert(!File.ReadAllText(store.UserFile).Contains("fake-profile-a"));
            return Task.CompletedTask;
        });
        await check("Two accounts at the same endpoint persist independently across reload and selection", () =>
        {
            var draft = OnlineProfiles.Add(saved) with { Model = "model-b", FirstResponseSeconds = 45, TotalSeconds = 120, InstructionRole = "developer" };
            second = draft.ActiveOnlineProfileId; draft = OnlineProfiles.Capture(draft, "备用账号");
            saved = ConfigurationService.Commit(saved, draft, "fake-profile-b", store, credentials);
            saved = store.Load(); Assert(saved.ActiveOnlineProfileId == second && saved.Model == "model-b" && credentials.Read(saved) == "fake-profile-b");
            var a = OnlineProfiles.Select(saved, first);
            Assert(a.Model == "model-a" && a.FirstResponseSeconds == 30 && a.InstructionRole == "system" && credentials.Read(a) == "fake-profile-a");
            Assert(a.ServiceId != saved.ServiceId && a.BaseUrl == saved.BaseUrl);
            return Task.CompletedTask;
        });
        await check("Replacing one profile key does not rotate or overwrite another profile", () =>
        {
            var a = OnlineProfiles.Select(saved, first); Guid binding = a.ServiceId;
            saved = ConfigurationService.Commit(saved, saved, "fake-profile-b-replaced", store, credentials);
            Assert(OnlineProfiles.Select(saved, first).ServiceId == binding && credentials.Read(a) == "fake-profile-a");
            Assert(credentials.Read(saved) == "fake-profile-b-replaced"); return Task.CompletedTask;
        });
        await check("Editing an endpoint cannot obtain either saved account credential", () =>
        {
            var draft = OnlineProfiles.Select(saved, first) with { BaseUrl = "https://different.example.test/v1" };
            Assert(credentials.Read(draft) is null);
            var committed = ConfigurationService.Commit(saved, draft, null, store, credentials);
            Assert(committed.ServiceId != original.ServiceId && credentials.Read(committed) is null);
            Assert(credentials.Read(OnlineProfiles.Select(committed, second)) == "fake-profile-b-replaced");
            saved = ConfigurationService.Commit(committed, saved, null, store, credentials);
            return Task.CompletedTask;
        });
        await check("Batch credential staging plus failed settings write leaves every saved profile intact", () =>
        {
            var before = File.ReadAllBytes(store.UserFile);
            var pending = new Dictionary<Guid, string> { [first] = "fake-new-a", [second] = "fake-new-b" };
            var staged = ConfigurationService.StageProfileKeys(saved, pending, credentials);
            File.SetAttributes(store.UserFile, FileAttributes.ReadOnly);
            try
            {
                try { ConfigurationService.Commit(saved, staged, null, store, credentials); throw new Exception("Read-only settings accepted"); }
                catch (UnauthorizedAccessException) { }
                Assert(before.SequenceEqual(File.ReadAllBytes(store.UserFile)));
                Assert(credentials.Read(OnlineProfiles.Select(store.Load(), first)) == "fake-profile-a");
                Assert(credentials.Read(OnlineProfiles.Select(store.Load(), second)) == "fake-profile-b-replaced");
            }
            finally { File.SetAttributes(store.UserFile, FileAttributes.Normal); }
            return Task.CompletedTask;
        });
        await check("Ordinary export includes all profiles but no keys and imports fresh bindings for all", () =>
        {
            string path = Path.Combine(scratch, "multiple-api-export.json"); SettingsStore.Export(path, saved);
            var imported = SettingsStore.Import(path);
            Assert(imported.OnlineProfiles.Length == 2 && !File.ReadAllText(path).Contains("fake-profile"));
            foreach (var p in imported.OnlineProfiles) Assert(credentials.Read(p.Apply(imported)) is null && saved.OnlineProfiles.Single(old => old.Id == p.Id).ServiceId != p.ServiceId);
            return Task.CompletedTask;
        });
        await check("Encrypted migration roundtrips all profile keys without plaintext and rebinds locally", () =>
        {
            var keys = saved.OnlineProfiles.ToDictionary(p => p.Id, p => credentials.Read(p.Apply(saved))!);
            byte[] encrypted = MigrationService.Encrypt(saved, null, "multi-profile-password-42", keys);
            Assert(!Encoding.UTF8.GetString(encrypted).Contains("fake-profile"));
            var payload = MigrationService.Decrypt(encrypted, "multi-profile-password-42");
            Assert(payload.ProfileKeys!.Count == 2 && payload.Settings.ActiveOnlineProfileId == second);
            var staged = ConfigurationService.StageProfileKeys(payload.Settings, payload.ProfileKeys, credentials);
            Assert(credentials.Read(OnlineProfiles.Select(staged, first)) == "fake-profile-a" && credentials.Read(staged) == "fake-profile-b-replaced");
            Assert(payload.Settings.OnlineProfiles.All(p => !saved.OnlineProfiles.Any(old => old.ServiceId == p.ServiceId)));
            return Task.CompletedTask;
        });
        await check("Profile deletion retains the other account and rejects deleting the final profile", () =>
        {
            var remaining = OnlineProfiles.Remove(saved, second);
            Assert(remaining.ActiveOnlineProfileId == first && remaining.OnlineProfiles.Length == 1 && credentials.Read(remaining) == "fake-profile-a");
            Invalid(() => OnlineProfiles.Remove(remaining, first)); return Task.CompletedTask;
        });
        await check("Malformed duplicate over-limit profiles and foreign migration keys are rejected", () =>
        {
            var p = saved.OnlineProfiles[0];
            Invalid(() => (saved with { OnlineProfiles = new[] { p, p } }).Validate());
            Invalid(() => (saved with { OnlineProfiles = new[] { p, p with { Id = Guid.NewGuid() } } }).Validate());
            Invalid(() => (saved with { OnlineProfiles = Enumerable.Range(0, 33).Select(_ => new OnlineProfile()).ToArray() }).Validate());
            Invalid(() => (p with { Name = "bad\nname" }).Validate());
            Invalid(() => (p with { BaseUrl = "http://unsafe.example.test" }).Validate());
            Invalid(() => ConfigurationService.StageProfileKeys(saved, new Dictionary<Guid, string> { [Guid.NewGuid()] = "fake" }, credentials));
            Invalid(() => MigrationService.Encrypt(saved, null, "multi-profile-password-42", new() { [Guid.NewGuid()] = "fake" }));
            return Task.CompletedTask;
        });
    }
}
