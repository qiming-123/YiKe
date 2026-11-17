using System;
using System.Linq;

namespace YiKe.Core;

// Profile identity survives key replacement; ServiceId binds one encrypted credential.
public sealed record OnlineProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "默认 API";
    public Guid ServiceId { get; init; } = Guid.NewGuid();
    public string Preset { get; init; } = "OpenAI";
    public string BaseUrl { get; init; } = "https://api.openai.com/v1";
    public string Model { get; init; } = "";
    public int FirstResponseSeconds { get; init; } = 30;
    public int TotalSeconds { get; init; } = 90;
    public string InstructionRole { get; init; } = "system";
    public override string ToString() => Name;

    public OnlineProfile Validate()
    {
        if (Id == Guid.Empty || ServiceId == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 60 || Name.Any(char.IsControl) ||
            Preset is null || Preset.Length > 80 || Preset.Any(char.IsControl) || Model is null || Model.Trim().Length > 200 || Model.Any(char.IsControl) ||
            FirstResponseSeconds is < 5 or > 120 || TotalSeconds < FirstResponseSeconds || TotalSeconds > 600 || InstructionRole is not ("system" or "developer"))
            throw new ArgumentException("请检查 API 配置名称（1–60 字）、模型和高级网络选项。");
        return this with { Name = Name.Trim(), BaseUrl = Endpoint.Normalize(BaseUrl), Model = Model.Trim() };
    }
    public static OnlineProfile From(AppSettings settings, Guid id, string name) => new()
    {
        Id = id, Name = name, ServiceId = settings.ServiceId, Preset = settings.Preset, BaseUrl = settings.BaseUrl, Model = settings.Model,
        FirstResponseSeconds = settings.FirstResponseSeconds, TotalSeconds = settings.TotalSeconds, InstructionRole = settings.InstructionRole
    };
    public AppSettings Apply(AppSettings settings) => settings with
    {
        ActiveOnlineProfileId = Id, ServiceId = ServiceId, Preset = Preset, BaseUrl = BaseUrl, Model = Model,
        FirstResponseSeconds = FirstResponseSeconds, TotalSeconds = TotalSeconds, InstructionRole = InstructionRole
    };
}

public static class OnlineProfiles
{
    public static AppSettings Upgrade(AppSettings settings)
    {
        settings = settings.Validate();
        if (settings.OnlineProfiles.Length > 0) return settings;
        var profile = OnlineProfile.From(settings, Guid.NewGuid(), "默认 API");
        return settings with { ActiveOnlineProfileId = profile.Id, OnlineProfiles = new[] { profile } };
    }
    public static AppSettings Capture(AppSettings settings, string? name = null)
    {
        settings = Upgrade(settings);
        var active = settings.OnlineProfiles.Single(p => p.Id == settings.ActiveOnlineProfileId);
        var replacement = OnlineProfile.From(settings, active.Id, name ?? active.Name).Validate();
        return (settings with { OnlineProfiles = settings.OnlineProfiles.Select(p => p.Id == active.Id ? replacement : p).ToArray() }).Validate();
    }
    public static AppSettings Select(AppSettings settings, Guid id)
    {
        settings = Capture(settings);
        return (settings.OnlineProfiles.SingleOrDefault(p => p.Id == id) ?? throw new ArgumentException("API 配置不存在。")).Apply(settings);
    }
    public static AppSettings Add(AppSettings settings)
    {
        settings = Capture(settings);
        if (settings.OnlineProfiles.Length >= 32) throw new ArgumentException("在线 API 配置最多保存 32 组。");
        int number = 1;
        while (settings.OnlineProfiles.Any(p => p.Name == "API " + number)) number++;
        var profile = new OnlineProfile { Name = "API " + number };
        return profile.Apply(settings with { OnlineProfiles = settings.OnlineProfiles.Append(profile).ToArray() });
    }
    public static AppSettings Remove(AppSettings settings, Guid id)
    {
        settings = Capture(settings);
        if (!settings.OnlineProfiles.Any(p => p.Id == id)) throw new ArgumentException("API 配置不存在。");
        if (settings.OnlineProfiles.Length == 1) throw new ArgumentException("请至少保留一组 API 配置；可在数据页单独删除密钥。");
        var remaining = settings.OnlineProfiles.Where(p => p.Id != id).ToArray();
        return (id == settings.ActiveOnlineProfileId ? remaining[0] : remaining.Single(p => p.Id == settings.ActiveOnlineProfileId)).Apply(settings with { OnlineProfiles = remaining });
    }
    public static AppSettings Rebind(AppSettings settings)
    {
        settings = Capture(settings);
        var profiles = settings.OnlineProfiles.Select(p => p with { ServiceId = Guid.NewGuid() }).ToArray();
        return profiles.Single(p => p.Id == settings.ActiveOnlineProfileId).Apply(settings with { OnlineProfiles = profiles });
    }
}
