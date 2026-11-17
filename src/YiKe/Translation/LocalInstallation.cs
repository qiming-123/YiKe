using System;
using System.IO;
using System.Linq;
using System.Text;

namespace YiKe.Translation;

public static class LocalInstallation
{
    public const string MissingMessage = "尚未安装所需的本地模型。请重新运行译刻安装包选择下载轻量 / 标准模型，或在上方选择在线 API；不会自动下载或发送原文。";
    public static string Selection(string root)
    {
        string path = Path.Combine(Path.GetFullPath(root), "..", "local-model.ini");
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4096) return "auto";
            bool section = false;
            foreach (string raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith('[')) { section = line.Equals("[LocalModel]", StringComparison.OrdinalIgnoreCase); continue; }
                if (!section || !line.StartsWith("Selection=", StringComparison.OrdinalIgnoreCase)) continue;
                string value = line[10..].Trim().ToLowerInvariant();
                return value is "light" or "standard" or "auto" ? value : "auto";
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        return "auto";
    }
    public static LocalSupport Evaluate(LocalHardwareSnapshot device, string? root = null)
    {
        root ??= LocalModelPackage.DirectoryPath;
        string selection = Selection(root);
        var support = LocalHardware.Evaluate(device, selection);
        if (!support.Supported) return support;
        if (File.Exists(Path.Combine(root, support.Profile!.ModelFile))) return support;
        // A single downloaded light model also works on a powerful computer.
        // Never promote to a heavier model when memory is insufficient.
        if (selection == "auto" && support.Profile.ModelId == LocalModelPackage.ModelId && File.Exists(Path.Combine(root, LocalModelPackage.LightModelFile)))
            return LocalHardware.Evaluate(device, "light");
        return new(null, MissingMessage, false);
    }
    public static bool HasAnyModel(string? root = null) => new[] { LocalModelPackage.LightModelFile, LocalModelPackage.ModelFile }.Any(file => File.Exists(Path.Combine(root ?? LocalModelPackage.DirectoryPath, file)));
    public static string Describe(LocalHardwareSnapshot device)
    {
        string choice = Selection(LocalModelPackage.DirectoryPath);
        var support = Evaluate(device);
        string detail = support.Profile is { } profile ? $"当前使用{profile.Name}模型 {profile.ModelId} · {profile.Threads} 个推理线程。" : support.Error!;
        return $"物理内存 {device.TotalBytes / (double)LocalHardware.GiB:F1} GB · 当前空闲 {device.AvailableBytes / (double)LocalHardware.GiB:F1} GB · {device.Processors} 个逻辑处理器\n安装选择：{(choice == "light" ? "轻量" : choice == "standard" ? "标准" : "自动适配已安装模型")}\n{detail}";
    }
    // Installer-only entry point: no settings, credentials, model loading or network.
    public static void WriteInstallerInfo(string path, LocalHardwareSnapshot device)
    {
        var support = LocalHardware.Evaluate(device);
        string recommended = support.Profile is { } p ? p.ModelId == LocalModelPackage.ModelId ? "standard" : "light" : "none";
        string text = "[Hardware]\r\n" +
            $"Recommended={recommended}\r\nLightEligible={(device.CompatibleCpu && device.Processors >= 4 && device.TotalBytes >= 7 * LocalHardware.GiB ? 1 : 0)}\r\n" +
            $"StandardEligible={(device.CompatibleCpu && device.Processors >= 8 && device.TotalBytes >= 14 * LocalHardware.GiB ? 1 : 0)}\r\n" +
            $"Description=内存 {device.TotalBytes / (double)LocalHardware.GiB:F1} GB，空闲 {device.AvailableBytes / (double)LocalHardware.GiB:F1} GB，{device.Processors} 个逻辑处理器。" +
            (support.Profile is { } profile ? $"推荐{profile.Name}模型。" : support.Error) + "\r\n";
        File.WriteAllText(path, text, Encoding.Unicode);
    }
}
