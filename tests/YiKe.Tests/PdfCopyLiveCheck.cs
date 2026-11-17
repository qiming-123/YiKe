using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using YiKe.Core;
using YiKe.Selection;

static class PdfCopyLiveCheck
{
    internal static int Run(string output)
    {
        int exit = 1;
        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            using var reader = new SelectionReader();
            using var copied = new PdfCopyFallback(() => true, _ => false);
            using var monitor = new SelectionMonitor(Dispatcher.CurrentDispatcher);
            const string expected = "The meeting starts at nine tomorrow morning.";
            string? clipboardTextBefore = Clipboard.ContainsText() ? Clipboard.GetText() : null;
            Gesture? last = null;
            void Write(object value) => File.WriteAllText(output, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
            Write(new { phase = "waiting", listenerAvailable = copied.Available, syntheticPdfOnly = true });
            monitor.MouseDown += _ => copied.Clear();
            monitor.Selected += async gesture =>
            {
                var title = new StringBuilder(1024); GetWindowText(new(gesture.Window), title, title.Capacity);
                if (!title.ToString().StartsWith("wps-selection-test.pdf", StringComparison.OrdinalIgnoreCase)) return;
                last = gesture;
                copied.Arm(gesture);
                var timer = Stopwatch.StartNew();
                var result = await reader.ReadSettledAsync(gesture, CancellationToken.None, pdf:true);
                copied.CompleteRead(gesture, result.Status);
                Write(new { phase = "selected", directReadStatus = result.Status.ToString(), elapsedMs = timer.ElapsedMilliseconds,
                    directExactText = result.Text?.Trim() == expected, rootProcessId = gesture.ProcessId, rootClass = Native.ClassName(new(gesture.Window)),
                    canWaitForCopy = PdfCopyPolicy.CanWait(result.Status), armed = copied.IsArmed, syntheticPdfOnly = true });
            };
            copied.Copied += (gesture, result) =>
            {
                bool exact = result.Text?.Trim() == expected;
                Write(new { phase = "copied", status = result.Status.ToString(), exactText = exact, scalars = TextLimit.Count(result.Text ?? ""),
                    clipboardOwnerProcessId = Native.ProcessId(GetClipboardOwner()), rootProcessId = gesture.ProcessId,
                    syntheticPdfOnly = true, automaticCopyWithoutManualCtrlC = true, clipboardTextRestored = clipboardTextBefore == (Clipboard.ContainsText() ? Clipboard.GetText() : null), realNativeReader = true, checkedUtc = DateTimeOffset.UtcNow });
                if (exact) {exit = 0; app.Dispatcher.BeginInvoke(app.Shutdown);}
            };
            var limit = new DispatcherTimer { Interval = TimeSpan.FromSeconds(180) };
            uint observed = Native.GetClipboardSequenceNumber();
            var metadata = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            metadata.Tick += (_, _) =>
            {
                uint sequence = Native.GetClipboardSequenceNumber();
                if (sequence == observed || last is not { } gesture) return;
                observed = sequence;
                IntPtr owner = GetClipboardOwner(); int ownerPid = Native.ProcessId(owner);
                string ownerName = "", rootName = "";
                try { using var p = Process.GetProcessById(ownerPid); ownerName = p.ProcessName; } catch { }
                try { using var p = Process.GetProcessById(gesture.ProcessId); rootName = p.ProcessName; } catch { }
                File.WriteAllText(output + ".clipboard.json", JsonSerializer.Serialize(new { sequence, rootPid = gesture.ProcessId, rootName,
                    ownerPid, ownerName, root = gesture.Window, ownerRoot = Native.GetAncestor(owner, 2).ToInt64(), ownerRootOwner = Native.GetAncestor(owner, 3).ToInt64(),
                    unicodeText = IsClipboardFormatAvailable(13), fileDrop = IsClipboardFormatAvailable(15), armed = copied.IsArmed,
                    copyStatus = copied.LastCopyStatus?.ToString(), foreground = Native.GetForegroundWindow().ToInt64(),
                    ownerTrusted = PdfCopyFallback.OwnerMatches(owner, gesture),
                    ownerDescendsFromRoot = VsCodeSelectionAdapter.IsDescendant(ownerPid, gesture.ProcessId, VsCodeSelectionAdapter.ProcessParents()) }, new JsonSerializerOptions { WriteIndented = true }));
            };
            metadata.Start();
            limit.Tick += (_, _) => app.Shutdown(); limit.Start();
            app.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); return exit;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maximum);
    [DllImport("user32.dll")] static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint format);
}
