using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace YiKe.Translation;

// A job handle is never inherited: closing YiKe, including an abnormal exit, kills its engine.
internal sealed class OwnedLocalProcess : IDisposable
{
    readonly SafeFileHandle job;
    public Process Process { get; }
    public OwnedLocalProcess(ProcessStartInfo start)
    {
        job = CreateJobObject(IntPtr.Zero, null);
        if (job.IsInvalid) throw new Win32Exception();
        var limits = new ExtendedLimits(); limits.Basic.LimitFlags = 0x2000;
        if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimits>())) { job.Dispose(); throw new Win32Exception(); }
        try
        {
            Process = Process.Start(start) ?? throw new InvalidOperationException();
            if (!AssignProcessToJobObject(job, Process.Handle)) { Process.Kill(true); Process.Dispose(); throw new Win32Exception(); }
            try { Process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (Win32Exception) { }
            Process.OutputDataReceived += (_, _) => { }; Process.ErrorDataReceived += (_, _) => { };
            Process.BeginOutputReadLine(); Process.BeginErrorReadLine();
        }
        catch { job.Dispose(); throw; }
    }
    public void Dispose() { job.Dispose(); Process.Dispose(); }
    [StructLayout(LayoutKind.Sequential)] struct BasicLimits { public long ProcessTime, JobTime; public uint LimitFlags; public UIntPtr MinWorkingSet, MaxWorkingSet; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass, SchedulingClass; }
    [StructLayout(LayoutKind.Sequential)] struct IoCounters { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits { public BasicLimits Basic; public IoCounters Io; public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetInformationJobObject(SafeFileHandle job, int info, ref ExtendedLimits limits, uint bytes);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);

    // Verify loopback listener ownership before sending any text or bearer token.
    public static bool OwnsPort(int port, int processId)
    {
        int size = 0; GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2, 3, 0);
        if (size is < 4 or > 16 * 1024 * 1024) return false;
        IntPtr table = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(table, ref size, false, 2, 3, 0) != 0) return false;
            int count = Marshal.ReadInt32(table);
            if (count < 0 || count > (size - 4) / 24) return false;
            for (int i = 0; i < count; i++)
            {
                IntPtr row = IntPtr.Add(table, 4 + i * 24);
                int localPort = (ushort)IPAddress.NetworkToHostOrder((short)Marshal.ReadInt32(row, 8));
                if (Marshal.ReadInt32(row) == 2 && Marshal.ReadInt32(row, 4) == 0x0100007f && localPort == port && Marshal.ReadInt32(row, 20) == processId) return true;
            }
            return false;
        }
        finally { Marshal.FreeHGlobal(table); }
    }
    [DllImport("iphlpapi.dll", SetLastError = true)] static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool ordered, int family, int tableClass, uint reserved);
}
