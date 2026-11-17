using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YiKe.Selection;

internal interface ISelectionTransport : IDisposable
{
    bool IsAlive { get; }
    Task<string?> ExchangeAsync(string request, CancellationToken token);
}

internal sealed class ProcessSelectionTransport : ISelectionTransport
{
    readonly Process process;
    int disposed;
    int unusable;
    internal int ProcessId => process.Id;
    public bool IsAlive
    {
        get { try { return !process.HasExited; } catch (InvalidOperationException) { return false; } }
    }
    public static ProcessSelectionTransport Start()
    {
        using var parent = Process.GetCurrentProcess();
        var info = new ProcessStartInfo(Environment.ProcessPath!);
        info.ArgumentList.Add("--uia-worker");
        info.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        info.ArgumentList.Add(parent.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
        return new(info);
    }
    internal ProcessSelectionTransport(ProcessStartInfo info)
    {
        info.UseShellExecute = false; info.CreateNoWindow = true;
        info.RedirectStandardInput = true; info.RedirectStandardOutput = true; info.RedirectStandardError = true;
        info.StandardInputEncoding = new UTF8Encoding(false); info.StandardOutputEncoding = new UTF8Encoding(false, true);
        process = Process.Start(info) ?? throw new IOException("无法启动取词辅助进程。");
        _ = DiscardErrorsAsync();
    }
    async Task DiscardErrorsAsync()
    {
        try { var buffer = new char[512]; while (await process.StandardError.ReadAsync(buffer.AsMemory()).ConfigureAwait(false) > 0) { } }
        catch (Exception ex) when (ex is IOException or InvalidOperationException) { }
    }
    public async Task<string?> ExchangeAsync(string request, CancellationToken token)
    {
        if (Volatile.Read(ref disposed) != 0 || Volatile.Read(ref unusable) != 0) throw new InvalidOperationException("取词辅助进程正在退出。");
        await process.StandardInput.WriteLineAsync(request.AsMemory(), token).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(token).ConfigureAwait(false);
        return await process.StandardOutput.ReadLineAsync(token).ConfigureAwait(false);
    }
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref disposed, 1, 0) != 0) return;
        Volatile.Write(ref unusable, 1);
        bool stopped = false;
        try
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: false); if (!process.WaitForExit(1000)) throw new System.ComponentModel.Win32Exception("取词辅助进程尚未退出。"); }
            stopped = true;
        }
        catch (InvalidOperationException) { stopped = true; }
        catch (System.ComponentModel.Win32Exception)
        {
            if (process.HasExited) stopped = true;
            else { Volatile.Write(ref disposed, 0); throw; }
        }
        finally { if (stopped) { process.Dispose(); Volatile.Write(ref disposed, 2); } }
    }
}
