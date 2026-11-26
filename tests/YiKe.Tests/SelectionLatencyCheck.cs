using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using YiKe.Core;
using YiKe.Selection;
using YiKe.Translation;
using YiKe.UI;

static class SelectionLatencyCheck
{
    internal const string SampleText = "The meeting starts at nine tomorrow morning.";
    internal sealed record Timing(double SelectionReadMs, double EngineReadyMs, double FirstVisibleMs);
    sealed record Sample(string Scenario, Timing Timing, long BeforeWorkingSetBytes, long AfterWorkingSetBytes, double IdleWaitMs);

    internal static async Task<Timing?> Measure(Gesture gesture, PopupWindow popup,
        Func<Gesture, CancellationToken, Task<SelectionReadResult>> read, ITranslationProvider provider, Task<long> engineReady, CancellationToken token)
    {
        // Match the automatic desktop path's delay before querying UI Automation.
        await Task.Delay(80, token);
        var selection = await read(gesture, token);
        var readAt = Stopwatch.GetTimestamp();
        if (selection.Status != ReadStatus.Success || selection.Text != SampleText) return null;

        var settings = new AppSettings();
        var visible = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool waitingForRender = false;
        using var coordinator = new TranslationCoordinator(provider, update => popup.Dispatcher.InvokeAsync(() =>
        {
            popup.Update(update);
            if (!waitingForRender && popup.IsVisible && update.Error is null && !string.IsNullOrWhiteSpace(update.Text))
            {
                waitingForRender = true;
                // Run after WPF processes render-priority work for the updated popup.
                popup.Dispatcher.InvokeAsync(() => visible.TrySetResult(Stopwatch.GetTimestamp()), DispatcherPriority.Loaded);
            }
            if (!update.Running) completed.TrySetResult(update.Error is null && waitingForRender);
        }), useCache: false);
        popup.Begin(SampleText, settings, gesture.X, gesture.Y, gesture.Window);
        coordinator.Submit(SampleText, settings, "");
        if (!await completed.Task.WaitAsync(token)) throw new InvalidOperationException("翻译未完成，无法采集延迟。");
        var readyAt = await engineReady.WaitAsync(token);
        var visibleAt = await visible.Task.WaitAsync(token);
        return new(Elapsed(readAt), Elapsed(readyAt), Elapsed(visibleAt));

        double Elapsed(long at) => Stopwatch.GetElapsedTime(gesture.ReleasedTimestamp, at).TotalMilliseconds;
    }

    internal static int Run(string directory, string output)
    {
        if (!File.Exists(Path.Combine(directory, "engine", "llama-server.exe")) ||
            !LocalInstallation.Evaluate(LocalHardware.Detect(), directory).Supported)
        {
            Console.WriteLine("SKIP：缺少可用的通用多语引擎或模型，或者当前硬件不满足加载条件。");
            return 77;
        }
        int exit = 1;
        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Startup += async (_, _) =>
            {
                try
                {
                    await RunDesktop(directory, output);
                    exit = 0;
                }
                catch (OperationCanceledException) { Console.WriteLine("测量已取消或超时。"); }
                catch (Exception error) { Console.WriteLine("测量未完成 · " + error.GetType().Name); }
                finally { app.Shutdown(); }
            };
            app.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        return exit;
    }

    static async Task RunDesktop(string directory, string output)
    {
        Theme.Apply("dark"); Theme.InstallStyles();
        using var lifetime = new CancellationTokenSource();
        using var monitor = new SelectionMonitor(Dispatcher.CurrentDispatcher);
        using var reader = new SelectionReader();
        TaskCompletionSource<Gesture>? nextGesture = null;
        TaskCompletionSource<long>? ready = null;
        using var provider = new LocalTranslationProvider(directory)
        { EngineReady = () => ready?.TrySetResult(Stopwatch.GetTimestamp()) };
        var prompt = Ui.Text("正在准备取词组件…", 14);
        var text = new TextBox { Text = SampleText, IsReadOnly = true, FontSize = 18, Margin = new Thickness(0, 12, 0, 0) };
        var panel = new StackPanel { Margin = new Thickness(20), Children = { prompt, text } };
        var window = new Window { Title = "译刻 · 划词延迟测试", Width = 620, Height = 160, Content = panel, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        Ui.WindowTheme(window); Ui.ConstrainToWorkArea(window);
        var popup = new PopupWindow();
        var samples = new List<Sample>();
        int coldEngine = 0;
        void Cancel(object? sender, EventArgs args) => lifetime.Cancel();
        window.Closed += Cancel;
        try
        {
            window.Show();
            long handle = new WindowInteropHelper(window).Handle.ToInt64();
            monitor.Selected += gesture => { if (gesture.Window == handle) nextGesture?.TrySetResult(gesture); };
            await reader.PrepareAsync(lifetime.Token);
            foreach (var (scenario, label) in new[] { ("cold", "冷启动"), ("consecutive", "连续使用"), ("after-idle", "空闲后再用") })
            {
                double idleWaitMs = 0;
                if (scenario == "after-idle")
                {
                    prompt.Text = "正在等待引擎按现有空闲策略退出…";
                    var watch = Stopwatch.StartNew();
                    using var idleLimit = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    idleLimit.CancelAfter(TimeSpan.FromMinutes(provider.ActiveProfile!.IdleMinutes) + TimeSpan.FromSeconds(30));
                    while (provider.EngineProcessId != 0) await Task.Delay(500, idleLimit.Token);
                    idleWaitMs = watch.Elapsed.TotalMilliseconds;
                }
                long before = WorkingSet(provider);
                using var limit = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                limit.CancelAfter(TimeSpan.FromMinutes(3));
                prompt.Text = label + " · 请用鼠标拖选下方整句";
                Timing? timing;
                do
                {
                    text.Select(0, 0);
                    nextGesture = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    var gesture = await nextGesture.Task.WaitAsync(limit.Token);
                    ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    timing = await Measure(gesture, popup, (g, token) => reader.ReadSettledAsync(g, token), provider, ready.Task, limit.Token);
                    if (timing is null) prompt.Text = label + " · 未读到完整样文，请重新拖选整句";
                } while (timing is null);
                int currentEngine = provider.EngineProcessId;
                if (scenario == "cold") coldEngine = currentEngine;
                if (scenario == "consecutive" && (before == 0 || currentEngine != coldEngine))
                {
                    Console.WriteLine("连续使用场景中引擎已重新加载，请重跑并在提示出现后及时划词。");
                    throw new InvalidOperationException();
                }
                samples.Add(new(scenario, timing, before, WorkingSet(provider), idleWaitMs));
                popup.HideForSelection();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            using var report = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
            await JsonSerializer.SerializeAsync(report, samples, new JsonSerializerOptions { WriteIndented = true }, lifetime.Token);
            Console.WriteLine("PASS 划词延迟测量 · 3 个场景");
        }
        finally
        {
            window.Closed -= Cancel;
            popup.DisposeSpeech();
            popup.Close();
        }
    }

    static long WorkingSet(LocalTranslationProvider provider)
    {
        int id = provider.EngineProcessId;
        if (id == 0) return 0;
        try { using var process = Process.GetProcessById(id); return process.WorkingSet64; }
        catch (ArgumentException) { return 0; }
        catch (InvalidOperationException) { return 0; }
    }
}
