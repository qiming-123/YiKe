using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using YiKe.Core;
using YiKe.Selection;
using YiKe.UI;

static class VsCodeDesktopCheck
{
    public static int Run(string output)
    {
        int code = 1;
        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Startup += (_, _) =>
            {
                Theme.Apply("light"); Theme.InstallStyles();
                var reader = new SelectionReader(); var monitor = new SelectionMonitor(app.Dispatcher); bool handling = false;
                var stop = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
                stop.Tick += (_, _) => app.Shutdown(); stop.Start();
                app.Exit += (_, _) => { monitor.Dispose(); reader.Dispose(); stop.Stop(); };
                monitor.Selected += async g =>
                {
                    var title = new StringBuilder(512); GetWindowText(new IntPtr(g.Window), title, title.Capacity);
                    if (handling || title.ToString() != "yike-selection-fixture.txt - Visual Studio Code") return;
                    handling = true;
                    try
                    {
                        await Task.Delay(250); var watch = Stopwatch.StartNew(); var result = await reader.ReadAsync(g, default);
                        long milliseconds = watch.ElapsedMilliseconds;
                        string fixture = File.ReadAllText(Path.Combine(Path.GetDirectoryName(output)!, "yike-selection-fixture.txt"));
                        bool matches = result.Text is not null && fixture.Contains(result.Text, StringComparison.Ordinal);
                        bool focused = Native.GetForegroundWindow().ToInt64() == g.Window;
                        if (matches && result.Status == ReadStatus.Success)
                        {
                            var popup = new PopupWindow(); popup.Begin(result.Text!, new AppSettings(), g.X, g.Y, g.Window);
                            popup.Error("适配验收：本机选区已读取，未调用在线翻译。");
                            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        }
                        bool preserved = Native.GetForegroundWindow().ToInt64() == g.Window;
                        var record = new { status = result.Status.ToString(), selectedScalars = result.Text?.EnumerateRunes().Count(), matchesSyntheticFixture = matches,
                            milliseconds, foregroundBeforePopup = focused, foregroundPreserved = preserved, started = g.StartedAt, released = g.Time };
                        File.WriteAllText(output, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
                        Console.WriteLine(JsonSerializer.Serialize(record)); code = result.Status == ReadStatus.Success && matches && focused && preserved ? 0 : 1;
                        await Task.Delay(15000); app.Shutdown();
                    }
                    catch (Exception ex) { Console.WriteLine(ex.GetType().Name); app.Shutdown(); }
                };
            };
            app.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); return code;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
}
