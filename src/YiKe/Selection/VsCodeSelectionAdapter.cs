using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using YiKe.Core;

namespace YiKe.Selection;

// Runs in the disposable UIA worker. No listener, clipboard, settings or document-wide reads.
internal static class VsCodeSelectionAdapter
{
    internal static SelectionReadResult? TryRead(Gesture gesture) => Exchange(gesture, "editor", ParseResponse);
    internal static bool TryCopyTerminal(Gesture gesture) => Exchange(gesture, "terminalCopy", ParseCopyAcknowledgement) is not null;
    internal sealed record CopyAcknowledgement;
    internal static CopyAcknowledgement? ParseCopyAcknowledgement(string json, string nonce)
    {
        try
        {
            using var doc = JsonDocument.Parse(json); var value = doc.RootElement;
            return value.GetProperty("version").GetInt32() == 1 && value.GetProperty("nonce").GetString() == nonce &&
                value.GetProperty("status").GetString() == "CopyRequested" ? new() : null;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { return null; }
    }
    static T? Exchange<T>(Gesture gesture, string operation, Func<string, string, T?> parse) where T : class
    {
        try
        {
            using var root = Process.GetProcessById(gesture.ProcessId);
            if (!IsCode(root.ProcessName)) return null;
            string? image = root.MainModule?.FileName;
            if (image is null) return null;
            var parents = ProcessParents();
            using var budget = new CancellationTokenSource(TimeSpan.FromMilliseconds(operation == "terminalCopy" ? 1000 : 650));
            foreach (int pid in parents.Keys.Where(pid => IsDescendant(pid, root.Id, parents)).Take(64))
            {
                if (budget.IsCancellationRequested) break;
                try
                {
                    using var candidate = Process.GetProcessById(pid);
                    if (!string.Equals(candidate.MainModule?.FileName, image, StringComparison.OrdinalIgnoreCase) || candidate.StartTime < root.StartTime) continue;
                    using var pipe = new NamedPipeClientStream(".", "YiKe.VSCode.Selection.v1." + pid, PipeDirection.InOut,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, TokenImpersonationLevel.Identification);
                    pipe.ConnectAsync(12, budget.Token).GetAwaiter().GetResult();
                    // Authenticate the actual pipe server, rather than trusting a PID in a message/name.
                    if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint server) || server != pid || candidate.HasExited) continue;
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(budget.Token); timeout.CancelAfter(operation == "terminalCopy" ? 450 : 220);
                    string nonce = Guid.NewGuid().ToString("N");
                    byte[] request = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { version = 1, nonce, operation,
                        started = (gesture.StartedAt ?? gesture.Time).ToUnixTimeMilliseconds(), released = gesture.Time.ToUnixTimeMilliseconds() }) + "\n");
                    pipe.WriteAsync(request, timeout.Token).AsTask().GetAwaiter().GetResult();
                    var bytes = new byte[16384]; int count = 0;
                    while (count < bytes.Length)
                    {
                        int read = pipe.ReadAsync(bytes.AsMemory(count), timeout.Token).AsTask().GetAwaiter().GetResult();
                        if (read == 0) break;
                        int newline = Array.IndexOf(bytes, (byte)10, count, read); count += read;
                        if (newline >= 0)
                        {
                            var result = parse(Encoding.UTF8.GetString(bytes, 0, newline), nonce);
                            if (result is not null) return result;
                            break;
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or Win32Exception or UnauthorizedAccessException or InvalidOperationException or ArgumentException) { }
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or UnauthorizedAccessException) { }
        return null;
    }
    internal static bool IsCode(string name) => name.Equals("Code", StringComparison.OrdinalIgnoreCase) || name.Equals("Code - Insiders", StringComparison.OrdinalIgnoreCase);
    internal static SelectionReadResult? ParseResponse(string json, string nonce)
    {
        try
        {
            using var doc = JsonDocument.Parse(json); var value = doc.RootElement;
            if (value.GetProperty("version").GetInt32() != 1 || value.GetProperty("nonce").GetString() != nonce) return null;
            return value.GetProperty("status").GetString() switch
            {
                "Success" => Validate(value.GetProperty("text").GetString()),
                "NoSelection" => new(ReadStatus.NoSelection),
                "TooLong" => new(ReadStatus.TooLong),
                _ => null
            };
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { return null; }
    }
    static SelectionReadResult Validate(string? text) => string.IsNullOrWhiteSpace(text) ? new(ReadStatus.NoSelection) :
        TextLimit.Allowed(text) ? new(ReadStatus.Success, text) : new(ReadStatus.TooLong);
    internal static bool IsDescendant(int pid, int root, IReadOnlyDictionary<int, int> parents)
    {
        var visited = new HashSet<int>();
        for (int depth = 0; depth < 12 && pid > 0 && visited.Add(pid); depth++)
        { if (pid == root) return true; if (!parents.TryGetValue(pid, out pid)) break; }
        return false;
    }
    internal static Dictionary<int, int> ProcessParents()
    {
        var result = new Dictionary<int, int>();
        using var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot.IsInvalid) return result;
        var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
        if (Process32First(snapshot, ref entry)) do { result[(int)entry.Pid] = (int)entry.Parent; } while (Process32Next(snapshot, ref entry));
        return result;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ProcessEntry
    {
        public uint Size, Usage, Pid; public UIntPtr Heap; public uint Module, Threads, Parent; public int Priority; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Exe;
    }
    [DllImport("kernel32.dll", SetLastError = true)] static extern SafeFileHandle CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode)] static extern bool Process32First(SafeFileHandle snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode)] static extern bool Process32Next(SafeFileHandle snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint pid);
}
