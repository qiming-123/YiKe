using System;
using System.Collections.Generic;
using System.Linq;
using YiKe.Core;

namespace YiKe.Data;

public static class ConfigurationService
{
    public static AppSettings Commit(AppSettings current, AppSettings draft, string? newKey, SettingsStore settings, CredentialStore credentials)
    {
        draft = OnlineProfiles.Upgrade(draft);
        var binding = draft.OnlineProfiles.Single(p => p.Id == draft.ActiveOnlineProfileId);
        if (newKey is not null) { draft = draft with { ServiceId = Guid.NewGuid() }; credentials.Save(draft, newKey); }
        else if (draft.BaseUrl != binding.BaseUrl || draft.ServiceId == current.ServiceId && draft.BaseUrl != current.BaseUrl) draft = draft with { ServiceId = Guid.NewGuid() };
        return settings.Save(OnlineProfiles.Capture(draft) with { Revision = Math.Max(current.Revision, draft.Revision) });
    }
    // Stage fresh bindings before the authoritative settings commit. A failed write
    // leaves all previously saved profiles and credentials usable.
    public static AppSettings StageProfileKeys(AppSettings draft, IReadOnlyDictionary<Guid, string> keys, CredentialStore credentials)
    {
        draft = OnlineProfiles.Capture(draft);
        if (keys.Keys.Any(id => !draft.OnlineProfiles.Any(p => p.Id == id))) throw new ArgumentException("密钥对应的 API 配置不存在。");
        foreach (var key in keys.Values) CredentialStore.ValidateSecret(key);
        var profiles = draft.OnlineProfiles.Select(profile =>
        {
            if (!keys.TryGetValue(profile.Id, out var key)) return profile;
            var fresh = profile with { ServiceId = Guid.NewGuid() };
            credentials.Save(fresh.Apply(draft), key);
            return fresh;
        }).ToArray();
        return profiles.Single(p => p.Id == draft.ActiveOnlineProfileId).Apply(draft with { OnlineProfiles = profiles });
    }
}
