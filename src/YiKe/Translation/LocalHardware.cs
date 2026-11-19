using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace YiKe.Translation;

public sealed record LocalHardwareSnapshot(ulong TotalBytes, ulong AvailableBytes, int Processors, bool CompatibleCpu);
public sealed record LocalRunProfile(string ModelId, string ModelFile, string Name, int Threads, int Context, int OutputTokens, int Batch, int MicroBatch, int IdleMinutes);
public sealed record LocalSupport(LocalRunProfile? Profile, string? Error, bool Permanent)
{
    public bool Supported => Profile is not null;
}

public static class LocalHardware
{
    public const ulong GiB = 1024UL * 1024 * 1024;
    public const ulong MinimumAvailable = GiB * 5 / 4;
    public static LocalHardwareSnapshot Detect()
    {
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref memory)) return new(0, 0, Environment.ProcessorCount, false);
        return new(memory.TotalPhysical, memory.AvailablePhysical, Environment.ProcessorCount,
            RuntimeInformation.OSArchitecture == Architecture.X64 && Sse42.IsSupported);
    }
    public static LocalSupport Evaluate(LocalHardwareSnapshot hardware, string choice = "auto")
    {
        if (hardware.TotalBytes == 0) return new(null, "暂时无法读取电脑配置，已暂停本地模型加载。请稍后重试。", false);
        if (!hardware.CompatibleCpu || hardware.Processors < 4 || hardware.TotalBytes < 7 * GiB)
            return new(null, "此电脑不支持本地模型：需要约 8 GB 内存、至少 4 个逻辑处理器及支持 SSE4.2 的 x64 CPU。请在设置中选择在线 API；不会自动发送原文。", true);
        if (hardware.AvailableBytes < MinimumAvailable)
            return new(null, "当前可用内存不足 1.25 GiB，已暂停加载本地模型以避免卡顿。请关闭占用内存的程序后重试，或选择在线 API。", false);
        bool standard = hardware.TotalBytes >= 14 * GiB && hardware.AvailableBytes >= 5 * GiB && hardware.Processors >= 8;
        if (choice == "standard" && !standard)
            return new(null, "所选标准模型需要约 16 GB 内存、8 个逻辑处理器及至少 5 GB 当前空闲内存。请释放内存，或重新运行安装程序选择轻量模型。", hardware.TotalBytes < 14 * GiB || hardware.Processors < 8);
        if (choice == "light") standard = false;
        return new(standard
            ? new(LocalModelPackage.ModelId, LocalModelPackage.ModelFile, "标准", Math.Clamp(hardware.Processors / 2 - 1, 2, 6), 6144, 3072, 256, 128, 15)
            : new(LocalModelPackage.LightModelId, LocalModelPackage.LightModelFile, "轻量", Math.Clamp(hardware.Processors / 2, 1, 2), 3072, 1536, 16, 8, 1), null, false);
    }
    public static string Describe(LocalHardwareSnapshot hardware)
    {
        var support = Evaluate(hardware);
        string device = $"系统可用物理内存总量 {hardware.TotalBytes / (double)GiB:F1} GB · 当前空闲 {hardware.AvailableBytes / (double)GiB:F1} GB · {hardware.Processors} 个逻辑处理器";
        return device + "\n" + (support.Profile is { } profile
            ? $"自动选择{profile.Name}模型 {profile.ModelId} · {profile.Threads} 个推理线程，优先保持电脑响应。"
            : support.Error);
    }
    [StructLayout(LayoutKind.Sequential)] struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
