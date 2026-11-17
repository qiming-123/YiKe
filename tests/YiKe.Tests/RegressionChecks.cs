using System.Net;
using System.Net.Http;
using System.Text;
using System.IO;
using System.Text.Json;
using YiKe.Core;
using YiKe.Data;
using YiKe.Translation;
using YiKe.UI;

public static class RegressionChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check, string scratch)
    {
        var settings = new AppSettings { TranslationMode = "online", Model = "regression-model" };
        var request = new TranslationRequest(1, "sample", "auto", "简体中文", settings.ConfigId, settings.ServiceId, settings.Model);
        await check("Window minimum sizes shrink to a high-DPI work area instead of hiding footer controls", () =>
        {
            var normal = WindowSizing.Calculate(800, 760, 680, 570, 1920, 1040, 1, 1);
            Assert(normal.Width == 800 && normal.Height == 760 && normal.MinHeight == 570);
            var largeDpi = WindowSizing.Calculate(800, 760, 680, 570, 1920, 1040, 2, 2);
            Assert(largeDpi.Height == 488 && largeDpi.MinHeight == 488 && largeDpi.MaxHeight == 520);
            var small = WindowSizing.Calculate(800, 760, 680, 570, 1280, 720, 2, 2);
            Assert(small.Width == 608 && small.Height == 328 && small.MinWidth == 608 && small.MinHeight == 328);
            var automatic = WindowSizing.Calculate(420, double.NaN, 0, 0, 1920, 1040, 2, 2);
            Assert(double.IsNaN(automatic.Height) && automatic.MaxHeight == 520);
            return Task.CompletedTask;
        });
        await check("Whitespace-only SSE output is not accepted as a completed translation", async () =>
        {
            using var provider = new OpenAiCompatibleProvider(new FakeHttp(HttpStatusCode.OK, Chunk(" \n\t") + Chunk("", "stop"), true, 1));
            Assert((await ExpectAsync<TranslationException>(async () => await Collect(provider, request, settings))).Code == "empty");
        });
        await check("A foreign read-only portable config gives an explicit next-start warning", () =>
        {
            string app = Path.Combine(scratch, "foreign-readonly"); Directory.CreateDirectory(app); File.WriteAllText(Path.Combine(app, "portable.flag"), "");
            var store = new SettingsStore(app, Path.Combine(scratch, "foreign-local"));
            store.Save(settings); File.SetAttributes(store.PortableFile!, FileAttributes.ReadOnly);
            try
            {
                var imported = store.Save(settings with { ConfigId = Guid.NewGuid(), Model = "imported" });
                Assert(store.Notice is not null && store.Notice.Contains("下次启动"));
                Assert(SettingsStore.Import(store.UserFile).ConfigId == imported.ConfigId);
                Assert(store.Load().ConfigId == settings.ConfigId); // Different portable ID wins by the documented rule.
            }
            finally { File.SetAttributes(store.PortableFile!, FileAttributes.Normal); }
            return Task.CompletedTask;
        });
        await check("Migration rejects an excessive password and invalid secret before deriving a key", () =>
        {
            Expect<ArgumentException>(() => MigrationService.Encrypt(settings, "fake-key", new string('p', 1025)));
            Expect<ArgumentException>(() => MigrationService.Encrypt(settings, "fake\nkey", "long-password-42"));
            return Task.CompletedTask;
        });
        await check("Popup placement handles negative monitors, edge flips and integer extremes", () =>
        {
            var negative = new PixelBounds(-1920, -200, 0, 880);
            Assert(PopupPlacement.Calculate(-100, 850, 600, 480, negative) == new PixelPosition(-600, 354));
            Assert(PopupPlacement.Calculate(-1910, -195, 600, 480, negative) == new PixelPosition(-1898, -175));
            Assert(PopupPlacement.Calculate(100, 100, 9999, 9999, new(0, 0, 800, 600)) == new PixelPosition(0, 0));
            Assert(PopupPlacement.Calculate(int.MaxValue, int.MaxValue, 400, 400, new(0, 0, 1920, 1080)) == new PixelPosition(1520, 680));
            return Task.CompletedTask;
        });
        await check("Model and target language reject embedded control characters", () =>
        {
            Expect<ArgumentException>(() => (settings with { Model = "model\nother" }).Validate());
            Expect<ArgumentException>(() => (settings with { TargetLanguage = "English\nother" }).Validate());
            return Task.CompletedTask;
        });
        await check("Session pin/unpin cannot revive a previous selection lease", () =>
        {
            var session = new SessionState(true); long old = session.BeginSelection();
            session.SetCurrent(new(1, 77, 1, "sample", 0, 0, DateTimeOffset.UtcNow));
            session.SetPinned(true); Assert(!session.Accepts(old) && session.Current is not null);
            Assert(!session.ShouldDismissForForeground(99, false));
            session.SetPinned(false); Assert(!session.Accepts(old));
            long next = session.BeginSelection(); Assert(session.Accepts(next)); return Task.CompletedTask;
        });
        await check("Pause invalidates active selection even when persistence fails", () =>
        {
            var store = new SettingsStore(userDirectory: Path.Combine(scratch, "pause-failure"));
            var saved = store.Save(settings); File.SetAttributes(store.UserFile, FileAttributes.ReadOnly);
            try
            {
                var state = new SessionState(true); long lease = state.BeginSelection(); state.SetPinned(true);
                state.SetEnabled(false);
                Expect<UnauthorizedAccessException>(() => store.Save(saved with { Enabled = false }));
                Assert(!state.Enabled && !state.Pinned && state.Current is null && !state.Accepts(lease));
                Assert(store.Load().Enabled); // Previous persisted preference is unchanged; runtime pause still holds.
            }
            finally { File.SetAttributes(store.UserFile, FileAttributes.Normal); }
            return Task.CompletedTask;
        });
        await check("Foreground dismissal respects pin, own windows, and manual translation", () =>
        {
            var state = new SessionState(true); state.SetCurrent(new(1, 77, 1, "sample", 0, 0, DateTimeOffset.UtcNow));
            Assert(state.ShouldDismissForForeground(88, false)); Assert(!state.ShouldDismissForForeground(77, false));
            Assert(!state.ShouldDismissForForeground(88, true)); state.SetPinned(true);
            Assert(!state.ShouldDismissForForeground(88, false)); state.Close(); Assert(state.Current is null && !state.Pinned);
            state.SetCurrent(new(1, 0, 1, "manual", 0, 0, DateTimeOffset.UtcNow)); Assert(!state.ShouldDismissForForeground(88, false)); return Task.CompletedTask;
        });
        await check("Whitespace target and malformed process exclusions are rejected", () =>
        {
            Expect<ArgumentException>(() => (settings with { TargetLanguage = "  " }).Validate());
            foreach (string item in new[] { @"C:\apps\foo.exe", "foo*", "foo\tbar" }) Expect<ArgumentException>(() => (settings with { ExcludedProcesses = new[] { item } }).Validate());
            var normalized = (settings with { ExcludedProcesses = new[] { " Foo.EXE ", "foo", "tool.exe.helper.exe" } }).Validate();
            Assert(normalized.ExcludedProcesses.SequenceEqual(new[] { "Foo", "tool.exe.helper" })); return Task.CompletedTask;
        });
        await check("Secret validation rejects control characters without echoing the key", () =>
        {
            foreach (string value in new[] { "fake\rkey", "fake\nkey", "fake\tkey", "fake\0key", " " })
            {
                var error = Expect<ArgumentException>(() => CredentialStore.ValidateSecret(value)); Assert(!error.Message.Contains(value, StringComparison.Ordinal) || value == " ");
            }
            Assert(CredentialStore.ValidateSecret(" fake-key ") == "fake-key"); return Task.CompletedTask;
        });
        await check("Corrupt ordinary JSON import leaves valid settings untouched", () =>
        {
            var store = new SettingsStore(userDirectory: Path.Combine(scratch, "bad-json")); store.Save(settings);
            byte[] before = File.ReadAllBytes(store.UserFile); string input = Path.Combine(scratch, "broken.json"); File.WriteAllText(input, "{not-json");
            Expect<InvalidDataException>(() => SettingsStore.Import(input)); Assert(before.SequenceEqual(File.ReadAllBytes(store.UserFile))); return Task.CompletedTask;
        });
        await check("Oversized DPAPI blobs fail before allocation and are preserved", () =>
        {
            var credentials = new CredentialStore(Path.Combine(scratch, "oversized-credential")); credentials.Save(settings, "fake-key");
            string file = Directory.GetFiles(Path.Combine(scratch, "oversized-credential", "credentials")).Single();
            File.WriteAllBytes(file, new byte[65537]); Expect<System.Security.Cryptography.CryptographicException>(() => credentials.Read(settings));
            Assert(new FileInfo(file).Length == 65537); return Task.CompletedTask;
        });
        await check("SSE BOM, CR-only delimiters, comments and fragmented Unicode", async () =>
        {
            string body = "\uFEFF: hello\r\rdata: {\rdata: \"choices\":[{\"delta\":{\"content\":\"汉😀\"},\"finish_reason\":null}]}\r\r" + Chunk("", "stop");
            using var provider = new OpenAiCompatibleProvider(new FakeHttp(HttpStatusCode.OK, body, true, 1));
            Assert(await Collect(provider, request, settings) == "汉😀");
        });
        await check("Oversized SSE line is rejected while streaming rather than after EOF", async () =>
        {
            var stream = new CountingPatternStream((byte)'x', SseReader.MaximumLineCharacters * 8L);
            using var provider = new OpenAiCompatibleProvider(new StreamResponse(stream));
            await ExpectAsync<TranslationException>(async () => await Collect(provider, request, settings));
            Assert(stream.BytesRead < SseReader.MaximumLineCharacters * 2L);
        });
        await check("SSE multiline event and cumulative heartbeat limits", async () =>
        {
            string body = string.Concat(Enumerable.Repeat("data: " + new string(' ', 1000) + "\n", 270)) + "\n";
            using (var provider = new OpenAiCompatibleProvider(new FakeHttp(HttpStatusCode.OK, body, true, 2048))) await ExpectAsync<TranslationException>(async () => await Collect(provider, request, settings));
            string comments = string.Concat(Enumerable.Repeat(": heartbeat\n", SseReader.MaximumStreamCharacters / 12 + 2));
            using var second = new OpenAiCompatibleProvider(new FakeHttp(HttpStatusCode.OK, comments, true, 2048));
            await ExpectAsync<TranslationException>(async () => await Collect(second, request, settings));
        });
        await check("Premature DONE and unterminated SSE finish are incomplete", async () =>
        {
            foreach (string body in new[] { Chunk("partial") + "data: [DONE]\n\n", Chunk("partial") + "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}" })
            {
                using var provider = new OpenAiCompatibleProvider(new FakeHttp(HttpStatusCode.OK, body, true, 3));
                var error = await ExpectAsync<TranslationException>(async () => await Collect(provider, request, settings)); Assert(error.Code == "interrupted");
            }
        });
        await check("A valid stop does not wait on a connection that never sends DONE", async () =>
        {
            var stream = new HangingAfterPrefix(Encoding.UTF8.GetBytes(Chunk("complete") + Chunk("", "stop")));
            using var provider = new OpenAiCompatibleProvider(new StreamResponse(stream));
            Assert(await Collect(provider, request, settings).WaitAsync(TimeSpan.FromSeconds(2)) == "complete");
        });
        await check("Malformed response objects and explicit refusals are classified safely", () =>
        {
            foreach (string body in new[] { "null", "[]", "{\"choices\":null}", "{\"choices\":[null]}", "{\"choices\":[{\"delta\":null}]}" })
                Expect<TranslationException>(() => OpenAiCompatibleProvider.ParseChunk(body, out _));
            foreach (string body in new[] { "null", "{\"choices\":{}}", "{\"choices\":[{\"finish_reason\":3}]}" }) Expect<TranslationException>(() => OpenAiCompatibleProvider.ParseNonStreaming(body));
            Assert(Expect<TranslationException>(() => OpenAiCompatibleProvider.ParseChunk("{\"choices\":[{\"delta\":{\"refusal\":\"declined\"}}]}", out _)).Code == "refusal");
            Assert(Expect<TranslationException>(() => OpenAiCompatibleProvider.ParseNonStreaming("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":null,\"refusal\":\"declined\"}}]}")).Code == "refusal"); return Task.CompletedTask;
        });
        await check("Malformed HTTP error code cannot hide the HTTP authentication status", async () =>
        {
            foreach (string body in new[] { "{\"error\":{\"code\":17}}", "{\"error\":[]}", "null", "<html>error</html>" })
            {
                using var provider = new OpenAiCompatibleProvider(new FakeHttp(HttpStatusCode.Unauthorized, body, false, 32));
                Assert((await ExpectAsync<TranslationException>(async () => await Collect(provider, request, settings))).Code == "authentication");
            }
        });
        await check("Connection checks bypass the translation cache", async () =>
        {
            var provider = new ImmediateProvider(); var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var coordinator = new TranslationCoordinator(provider, update => { if (update.Complete) completion.TrySetResult(); }, useCache: false);
            coordinator.Submit("Hello, world!", settings, "fake-key"); await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); coordinator.Submit("Hello, world!", settings, "fake-key");
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(2)); Assert(provider.Calls == 2);
        });
    }
    public static string Chunk(string text, string? finish = null) => "data: " + JsonSerializer.Serialize(new { choices = new[] { new { delta = new { content = text }, finish_reason = finish } } }) + "\n\n";
    static async Task<string> Collect(ITranslationProvider provider, TranslationRequest request, AppSettings settings)
    { var text = new StringBuilder(); await foreach (string part in provider.TranslateAsync(request, settings, "fake-key", default)) text.Append(part); return text.ToString(); }
    static void Assert(bool condition) { if (!condition) throw new Exception("regression assertion failed"); }
    static T Expect<T>(Action action) where T : Exception { try { action(); } catch (T error) { return error; } throw new Exception("expected " + typeof(T).Name); }
    static async Task<T> ExpectAsync<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T error) { return error; } throw new Exception("expected " + typeof(T).Name); }
}

sealed class ImmediateProvider : ITranslationProvider
{
    public int Calls;
    public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    { Interlocked.Increment(ref Calls); await Task.Yield(); token.ThrowIfCancellationRequested(); yield return "synthetic QA output"; }
}
sealed class StreamResponse(Stream stream) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    { var result = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }; result.Content.Headers.ContentType = new("text/event-stream"); return Task.FromResult(result); }
}
sealed class CountingPatternStream(byte pattern, long length) : Stream
{
    public long BytesRead;
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false; public override long Length => length;
    public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) { int read = (int)Math.Min(count, length - BytesRead); buffer.AsSpan(offset, read).Fill(pattern); BytesRead += read; return read; }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) { token.ThrowIfCancellationRequested(); int read = (int)Math.Min(buffer.Length, length - BytesRead); buffer.Span[..read].Fill(pattern); BytesRead += read; return ValueTask.FromResult(read); }
    public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
sealed class HangingAfterPrefix(byte[] prefix) : MemoryStream(prefix)
{
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    { if (Position < Length) return await base.ReadAsync(buffer, token); await Task.Delay(Timeout.Infinite, token); return 0; }
}
