using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace YiKe.Core;

public enum ReadStatus { Success, NoSelection, Unsupported, Timeout, AccessDenied, TargetGone, TooLong, NotTextSelection }
public sealed record Gesture(long Sequence, long Window, int ProcessId, int X, int Y, int StartX, int StartY, DateTimeOffset Time, DateTimeOffset? StartedAt = null);
public sealed record SelectionSnapshot(long Sequence, long Window, int ProcessId, string Text, int X, int Y, DateTimeOffset Time, string? OriginalText = null);
public sealed record SelectionReadResult
{
    public ReadStatus Status { get; }
    public string? Text { get; }
    [System.Text.Json.Serialization.JsonConstructor]
    public SelectionReadResult(ReadStatus status, string? text = null) { Status = status; Text = status == ReadStatus.Success ? text : null; }
}
public sealed record TranslationRequest(long Sequence, string Text, string SourceLanguage, string TargetLanguage, Guid ConfigId, Guid ServiceId, string Model, string PromptVersion = "1", bool ExplainTechnical = false, string KnownTechnicalTerms = "", bool RefineTranslation = false, bool NotesOnly = false);
public sealed record TranslationUpdate(long Sequence, string Text, bool Complete = false, string? Error = null, bool Cached = false, string ProgrammingNotes = "", bool ExplanationPending = false)
{
    public bool Running => Error is null && (!Complete || ExplanationPending);
    public string StatusLabel => Error is not null ? "失败" : ExplanationPending ? "译文已完成 · 正在补充计算机说明…" : Complete ? Cached ? "翻译完成 · 缓存" : "翻译完成" : "翻译中";
}

public sealed record AppSettings
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string ServiceLabel => TranslationMode == "local" ? LocalEngine == "nmt" ? "本地英译中" : "本地多语" : "在线 · " + Preset;
    public int SchemaVersion { get; init; } = 1;
    public int EnhancementDefaultsVersion { get; init; }
    public Guid ConfigId { get; init; } = Guid.NewGuid();
    public long Revision { get; init; }
    public Guid ServiceId { get; init; } = Guid.NewGuid();
    public string TranslationMode { get; init; } = "local";
    public string LocalEngine { get; init; } = "generic";
    public string Preset { get; init; } = "OpenAI";
    public string BaseUrl { get; init; } = "https://api.openai.com/v1";
    public string Model { get; init; } = "";
    public Guid ActiveOnlineProfileId { get; init; }
    public OnlineProfile[] OnlineProfiles { get; init; } = Array.Empty<OnlineProfile>();
    public string TargetLanguage { get; init; } = TranslationLanguages.Default;
    public string Theme { get; init; } = "dark";
    public double FontSize { get; init; } = 15;
    public bool Enabled { get; init; } = true;
    public bool Stream { get; init; } = true;
    public bool TranslateCopiedPdf { get; init; } = true;
    public bool ExplainProgrammingTerms { get; init; } = false;
    public bool LearnEnglish { get; init; }
    public bool SelectionIconMode { get; init; }
    public bool CleanPdfText { get; init; } = true;
    public bool SupplementTechnicalTerms { get; init; } = false;
    public int FirstResponseSeconds { get; init; } = 30;
    public int TotalSeconds { get; init; } = 90;
    public string InstructionRole { get; init; } = "system";
    public string[] ExcludedProcesses { get; init; } = Array.Empty<string>();

    public AppSettings Validate(bool requireModel = false)
    {
        if (BaseUrl is null || Model is null || TargetLanguage is null || Theme is null || Preset is null || InstructionRole is null || ExcludedProcesses is null || ExcludedProcesses.Any(p => p is null)) throw new ArgumentException("配置包含无效的空字段。");
        if (SchemaVersion != 1 || ConfigId == Guid.Empty || ServiceId == Guid.Empty || Revision < 0) throw new ArgumentException("配置格式无效或版本较新，请升级译刻。");
        if (OnlineProfiles is null || OnlineProfiles.Length > 32 || OnlineProfiles.Any(p => p is null)) throw new ArgumentException("在线 API 配置最多保存 32 组。");
        var profiles = OnlineProfiles.Select(p => p.Validate()).ToArray();
        if (profiles.Select(p => p.Id).Distinct().Count() != profiles.Length || profiles.Select(p => p.ServiceId).Distinct().Count() != profiles.Length ||
            profiles.Length > 0 && !profiles.Any(p => p.Id == ActiveOnlineProfileId) || profiles.Length == 0 && ActiveOnlineProfileId != Guid.Empty) throw new ArgumentException("在线 API 配置标识无效。");
        var endpoint = Endpoint.Normalize(BaseUrl);
        string model = Model.Trim(), target = TargetLanguage.Trim();
        if (LocalEngine is not ("generic" or "nmt")) throw new ArgumentException("离线引擎设置无效。");
        if (TranslationMode is not ("local" or "online")) throw new ArgumentException("翻译方式无效。");
        if ((requireModel && TranslationMode == "online" && model.Length == 0) || model.Length > 200 || target.Length is < 1 or > 80 || model.Any(char.IsControl) || target.Any(char.IsControl)) throw new ArgumentException("请填写有效的模型名称和目标语言，不要包含换行或控制字符。");
        if (Theme is not ("system" or "light" or "dark") || FontSize is < 12 or > 24 || !double.IsFinite(FontSize)) throw new ArgumentException("外观设置无效。");
        if (FirstResponseSeconds is < 5 or > 120 || TotalSeconds < FirstResponseSeconds || TotalSeconds > 600) throw new ArgumentException("首响应超时应为 5–120 秒，总超时应介于首响应和 600 秒之间。");
        if (InstructionRole is not ("system" or "developer")) throw new ArgumentException("指令角色仅支持 system 或 developer。");
        if (ExcludedProcesses is null || ExcludedProcesses.Length > 100) throw new ArgumentException("排除项过多。");
        return this with { BaseUrl = endpoint, Model = model, OnlineProfiles = profiles, TargetLanguage = target, ExcludedProcesses = ExcludedProcesses.Select(NormalizeProcess).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() };
    }
    static string NormalizeProcess(string value)
    {
        string name = value.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        if (name.Length > 128 || name.Any(char.IsControl) || name.IndexOfAny(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
            throw new ArgumentException("排除应用请填写进程名称，例如 msedge.exe，不要填写路径或通配符。");
        return name;
    }
}

public static class Endpoint
{
    public static string Normalize(string value)
    {
        if (value is null || value.Length > 2048 || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var u) || u.Scheme != "https" || string.IsNullOrEmpty(u.Host) || u.UserInfo.Length > 0 || u.Query.Length > 0 || u.Fragment.Length > 0)
            throw new ArgumentException("Base URL 必须是 HTTPS API 根地址，不能包含账号、查询参数或片段。");
        if (u.AbsolutePath.TrimEnd('/').EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("请填写 API 根地址，例如 https://api.openai.com/v1，不要包含 chat/completions。");
        return u.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }
    public static Uri Completions(string root) => new(Normalize(root) + "/chat/completions");
    public static Uri Models(string root) => new(Normalize(root) + "/models");
}

public static class TextLimit
{
    public const int Maximum = 2000;
    public static int Count(string value) => value.EnumerateRunes().Count();
    public static bool Allowed(string text) => !string.IsNullOrWhiteSpace(text) && Count(text) <= Maximum;
}
