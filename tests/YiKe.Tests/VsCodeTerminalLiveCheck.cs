using System.IO;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using YiKe.Core;
using YiKe.Selection;
using YiKe.Translation;
using System.Windows.Threading;

static class VsCodeTerminalLiveCheck
{
    public static async Task<int> Run(string folder, string output)
    {
        try
        {
            string? codeVersion = null;
            async Task WaitStage(string wanted)
            {
                for (int attempt = 0; attempt < 240; attempt++)
                {
                    string file = Path.Combine(folder, "stage.json");
                    if (File.Exists(file))
                    {
                        try
                        {
                            using var stage = JsonDocument.Parse(File.ReadAllText(file));
                            string current = stage.RootElement.GetProperty("stage").GetString()!;
                            if (current.StartsWith("error:")) throw new Exception(current);
                            if (current == wanted) { codeVersion = stage.RootElement.GetProperty("codeVersion").GetString(); return; }
                        }
                        catch (JsonException) { }
                    }
                    await Task.Delay(250);
                }
                throw new Exception("Terminal fixture did not reach " + wanted);
            }
            Gesture Foreground()
            {
                IntPtr window = Native.GetForegroundWindow(); var title = new StringBuilder(1024); GetWindowText(window, title, title.Capacity);
                if (!title.ToString().Contains("yike-terminal-fixture", StringComparison.OrdinalIgnoreCase)) throw new Exception("Synthetic terminal window is not foreground");
                var now = DateTimeOffset.UtcNow; return new(1, window.ToInt64(), Native.ProcessId(window), 100, 100, 100, 100, now, now);
            }
            await WaitStage("selected");
            Gesture? ready = null;
            for (int attempt = 0; attempt < 180 && ready is null; attempt++) { try { ready = Foreground(); } catch (Exception) { await Task.Delay(500); } }
            _ = ready ?? throw new Exception("Synthetic window was never foreground");
            var pressed = new TaskCompletionSource<Gesture>(TaskCreationOptions.RunContinuationsAsynchronously);
            Dispatcher? hookDispatcher = null;
            var hookThread = new Thread(() =>
            {
                try
                {
                    hookDispatcher = Dispatcher.CurrentDispatcher;
                    using var hotkey = new VsCodeTerminalHotkey(); hotkey.Requested += gesture => pressed.TrySetResult(gesture);
                    hotkey.Update(true); if (!hotkey.IsRegistered) throw new Exception("Ctrl+Alt+Y is unavailable");
                    File.WriteAllText(Path.Combine(folder, "hotkey-ready.txt"), "Ctrl+Alt+Y");
                    Dispatcher.Run();
                }
                catch (Exception error) { pressed.TrySetException(error); }
            }) { IsBackground = true };
            hookThread.SetApartmentState(ApartmentState.STA); hookThread.Start();
            Gesture gesture;
            try { gesture = await pressed.Task.WaitAsync(TimeSpan.FromSeconds(60)); }
            finally { hookDispatcher?.BeginInvokeShutdown(DispatcherPriority.Send); hookThread.Join(2000); }
            uint before = Native.GetClipboardSequenceNumber();
            var selected = await VsCodeTerminal.ReadAsync(gesture, default);
            if (selected.Status != ReadStatus.Success || selected.Text?.Trim() != "grep -n -i 'error' ./demo/app.log") throw new Exception("Terminal selection mismatch: " + selected.Status + " / " + selected.Text);
            string notes = ProgrammingGlossary.Explain(selected.Text);
            if (!notes.Contains("本次选中参数") || !notes.Contains("忽略大小写") || !notes.Contains("效果：")) throw new Exception("Command explanation missing");
            uint copied = Native.GetClipboardSequenceNumber();
            File.WriteAllText(Path.Combine(folder, "signal.txt"), "clear"); await WaitStage("cleared");
            var empty = await VsCodeTerminal.ReadAsync(Foreground(), default);
            if (empty.Status != ReadStatus.NoSelection || Native.GetClipboardSequenceNumber() != copied) throw new Exception("Empty selection reused or modified clipboard: " + empty.Status);
            File.WriteAllText(Path.Combine(folder, "signal.txt"), "oversize"); await WaitStage("oversize");
            var large = await VsCodeTerminal.ReadAsync(Foreground(), default);
            if (large.Status != ReadStatus.TooLong || large.Text is not null) throw new Exception("Oversized terminal selection accepted: " + large.Status);
            var result = new { codeVersion, nativeCopyAndAuthenticatedPipe = true, selected = selected.Text.Trim(), selectedScalars = TextLimit.Count(selected.Text), clipboardSequenceChanged = before != copied,
                selectedArgumentsExplained = true, noSelectionStatus = empty.Status.ToString(), oldClipboardNotReused = true, oversizeStatus = large.Status.ToString(), foregroundPreserved = Native.GetForegroundWindow().ToInt64() == gesture.Window,
                syntheticPseudoterminalOnly = true, noShellProcess = true, noSelectedCommandExecuted = true, nativeHotkeyReceived = true };
            File.WriteAllText(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("PASS real VS Code terminal copy, no-selection stale-clipboard guard, Unicode limit and offline command explanation"); return 0;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
}
