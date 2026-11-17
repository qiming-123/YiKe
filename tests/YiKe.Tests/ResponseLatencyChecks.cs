using System.Diagnostics;
using System.Text.Json;
using YiKe.Core;
using YiKe.Selection;
using YiKe.Translation;

static class ResponseLatencyChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check)
    {
        await check("A selection published after several UI turns succeeds without a second mouse gesture", async () =>
        {
            var gesture = new Gesture(1, 123, 456, 90, 50, 10, 50, DateTimeOffset.UtcNow);
            var transport = new DelayedSelection(4);
            using var reader = new SelectionReader(() => transport, TimeSpan.FromSeconds(3));
            var result = await reader.ReadSettledAsync(gesture, default);
            Assert(result.Text == "delayed selected text" && transport.Requests.Count == 5);
            Assert(transport.Requests.All(json => JsonSerializer.Deserialize<Gesture>(json) == gesture));
        });
        await check("Empty selection settling stops after a bounded number of reads", async () =>
        {
            var transport = new DelayedSelection(1000);
            using var reader = new SelectionReader(() => transport, TimeSpan.FromSeconds(3));
            var watch = Stopwatch.StartNew();
            var result = await reader.ReadSettledAsync(Gesture(), default);
            Assert(result.Status == ReadStatus.NoSelection && transport.Requests.Count <= 6 && watch.ElapsedMilliseconds < 3200);
        });
        await check("A stuck selection query respects the overall deadline and never returns stale text", async () =>
        {
            var held = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var transport = new ScriptedTransport(_ => held.Task, () => held.TrySetResult(JsonSerializer.Serialize(new SelectionReadResult(ReadStatus.Success, "stale"))));
            using var reader = new SelectionReader(() => transport, TimeSpan.FromSeconds(10));
            var watch = Stopwatch.StartNew(); var result = await reader.ReadSettledAsync(Gesture(), default);
            Assert(result.Status == ReadStatus.Timeout && result.Text is null && watch.ElapsedMilliseconds < 3300);
            await Task.Delay(80); Assert(transport.Disposed);
        });
        await check("Selection warm-up starts one helper without querying text and reuses it for the first selection", async () =>
        {
            int starts = 0; var transport = new DelayedSelection(0);
            using var reader = new SelectionReader(() => { starts++; return transport; }, TimeSpan.FromSeconds(1));
            await reader.PrepareAsync(default); await reader.PrepareAsync(default);
            Assert(starts == 1 && transport.Requests.Count == 0);
            Assert((await reader.ReadSettledAsync(Gesture(), default)).Status == ReadStatus.Success && starts == 1);
        });
        await check("An early first translation chunk is visible even if the next chunk is held", async () =>
        {
            var provider = new StreamingProbe(); var visible = Done(); var completed = Done();
            using var coordinator = new TranslationCoordinator(provider, update => { if (update.Text == "首段译文") visible.TrySetResult(update); if (update.Complete) completed.TrySetResult(update); });
            coordinator.Submit("first paragraph", Settings(), "");
            var update = await visible.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert(!update.Complete && !completed.Task.IsCompleted);
            provider.Release.TrySetResult(); Assert((await completed.Task.WaitAsync(TimeSpan.FromSeconds(1))).Text == "首段译文，后续译文");
        });
        await check("Repeated selection of the same in-flight paragraph replays progress without canceling or restarting", async () =>
        {
            var provider = new StreamingProbe(); var visible = Done(); var completed = Done(); int deliveries = 0;
            using var coordinator = new TranslationCoordinator(provider, update => { if (update.Text.Length > 0) { Interlocked.Increment(ref deliveries); visible.TrySetResult(update); } if (update.Complete) completed.TrySetResult(update); });
            var settings = Settings(); long first = coordinator.Submit("first paragraph", settings, "");
            await visible.Task.WaitAsync(TimeSpan.FromSeconds(1));
            for (int i = 0; i < 4; i++) Assert(coordinator.Submit("first paragraph", settings, "") == first);
            Assert(provider.Calls == 1 && !provider.Canceled && deliveries >= 5);
            provider.Release.TrySetResult(); Assert((await completed.Task.WaitAsync(TimeSpan.FromSeconds(1))).Sequence == first);
        });
        await check("Explicit retry bypasses completed cache while ordinary re-selection uses it", async () =>
        {
            var provider = new StreamingProbe(); provider.Release.TrySetResult(); var done = Done();
            using var coordinator = new TranslationCoordinator(provider, update => { if (update.Complete) done.TrySetResult(update); });
            var settings = Settings(); coordinator.Submit("first paragraph", settings, ""); await done.Task.WaitAsync(TimeSpan.FromSeconds(1));
            done = Done(); coordinator.Submit("first paragraph", settings, ""); Assert((await done.Task.WaitAsync(TimeSpan.FromSeconds(1))).Cached && provider.Calls == 1);
            done = Done(); coordinator.Submit("first paragraph", settings, "", force: true); Assert(!(await done.Task.WaitAsync(TimeSpan.FromSeconds(1))).Cached && provider.Calls == 2);
        });
        await check("Closing invalidates reuse and an identical next selection starts a new request", async () =>
        {
            var provider = new StreamingProbe(); var visible = Done(); var done = Done();
            using var coordinator = new TranslationCoordinator(provider, update => { if (update.Text.Length > 0) visible.TrySetResult(update); if (update.Complete) done.TrySetResult(update); });
            var settings = Settings(); long first = coordinator.Submit("first paragraph", settings, ""); await visible.Task.WaitAsync(TimeSpan.FromSeconds(1));
            coordinator.Invalidate(); long second = coordinator.Submit("first paragraph", settings, ""); provider.Release.TrySetResult();
            var result = await done.Task.WaitAsync(TimeSpan.FromSeconds(2)); Assert(second != first && result.Sequence == second && provider.Calls == 2);
        });
        await check("Reading a new selection blocks old translation updates even when its request is held for reuse", () =>
        {
            var state = new SessionState(true); state.BeginSelection(); state.ShowTranslation(7);
            Assert(state.AcceptsTranslation(7, 7));
            state.Close(); state.BeginSelection(); Assert(!state.AcceptsTranslation(7, 7));
            state.ShowTranslation(7); Assert(state.AcceptsTranslation(7, 7));
            state.SetEnabled(false); Assert(!state.AcceptsTranslation(7, 7));
            return Task.CompletedTask;
        });
    }
    static AppSettings Settings() => new() { ExplainProgrammingTerms = false };
    static Gesture Gesture() => new(1, 123, 456, 30, 30, 10, 30, DateTimeOffset.UtcNow);
    static TaskCompletionSource<TranslationUpdate> Done() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    static void Assert(bool value) { if (!value) throw new Exception("Response latency regression failed"); }
    sealed class DelayedSelection(int emptyReads) : ISelectionTransport
    {
        public bool IsAlive { get; private set; } = true;
        public List<string> Requests = new();
        public Task<string?> ExchangeAsync(string request, CancellationToken token)
        {
            Requests.Add(request); token.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(JsonSerializer.Serialize(Requests.Count <= emptyReads ? new SelectionReadResult(ReadStatus.NoSelection) : new SelectionReadResult(ReadStatus.Success, "delayed selected text")));
        }
        public void Dispose() => IsAlive = false;
    }
    sealed class StreamingProbe : ITranslationProvider
    {
        public int Calls; public bool Canceled;
        public TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            Interlocked.Increment(ref Calls); await Task.Yield(); yield return "首段译文";
            try { await Release.Task.WaitAsync(token); } catch (OperationCanceledException) { Canceled = true; throw; }
            yield return "，后续译文";
        }
    }
}
