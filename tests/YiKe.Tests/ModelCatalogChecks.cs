using System.Net;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using YiKe.Core;
using YiKe.Translation;

public static class ModelCatalogChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check)
    {
        await check("Model discovery uses authenticated GET at the normalized API root", async () =>
        {
            using var catalog = new ModelCatalog(new Handler((request, _) =>
            {
                Assert(request.Method == HttpMethod.Get && request.RequestUri == new Uri("https://example.test/custom/v1/models"));
                Assert(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == "fake-catalog-secret" && request.Content is null);
                return Task.FromResult(Json("{\"data\":[{\"id\":\"model-b\"},{\"id\":\"model-a\"},{\"id\":\"model-b\"},{\"id\":\"\"},{\"id\":3},{\"id\":\"bad\\nname\"},{\"id\":\" spaced \"}]}"));
            }));
            Assert((await catalog.ListAsync("https://example.test/custom/v1/", "fake-catalog-secret", default)).SequenceEqual(new[] { "model-a", "model-b" }));
        });
        await check("Model discovery rejects unsafe endpoints before sending credentials", async () =>
        {
            int calls = 0;
            using var catalog = new ModelCatalog(new Handler((_, _) => { calls++; return Task.FromResult(Json("{}")); }));
            foreach (string endpoint in new[] { "http://example.test/v1", "https://user@example.test/v1", "https://example.test/v1?key=secret", "https://example.test/v1/chat/completions" })
                await Throws<ArgumentException>(() => catalog.ListAsync(endpoint, "fake", default));
            Assert(calls == 0);
        });
        await check("Model HTTP errors are safe and do not retry or expose response bodies", async () =>
        {
            foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed, HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError, HttpStatusCode.Redirect })
            {
                int calls = 0;
                using var catalog = new ModelCatalog(new Handler((_, _) => { calls++; return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("fake-private-body-secret"), Headers = { Location = new Uri("https://other.test/models") } }); }));
                var error = await Throws<TranslationException>(() => catalog.ListAsync("https://example.test/v1", "fake", default));
                Assert(calls == 1 && !error.Message.Contains("private-body") && error.Code == "models-http");
            }
        });
        await check("Model lists reject malformed schemas, excessive entries and oversized streamed bodies", async () =>
        {
            foreach (string body in new[] { "not json", "[]", "{}", "{\"data\":{}}", JsonSerializer.Serialize(new { data = Enumerable.Repeat(new { id = "model" }, 4097) }), new string(' ', 1048577) })
            {
                using var catalog = new ModelCatalog(new Handler((_, _) =>
                {
                    // No Content-Length: the reader must enforce its bound while streaming, too.
                    var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(body))) };
                    return Task.FromResult(response);
                }));
                Assert((await Throws<TranslationException>(() => catalog.ListAsync("https://example.test", "fake", default))).Code == "models-format");
            }
        });
        await check("Model discovery timeout and caller cancellation remain distinct", async () =>
        {
            using var catalog = new ModelCatalog(new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Json("{}"); }), TimeSpan.FromMilliseconds(60));
            Assert((await Throws<TranslationException>(() => catalog.ListAsync("https://example.test", "fake", default))).Code == "models-timeout");
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Throws<OperationCanceledException>(() => catalog.ListAsync("https://example.test", "fake", cancel.Token));
        });
        await check("Empty model list succeeds without inventing account access", async () =>
        {
            using var catalog = new ModelCatalog(new Handler((_, _) => Task.FromResult(Json("{\"data\":[]}"))));
            Assert((await catalog.ListAsync("https://example.test", "fake", default)).Count == 0);
        });
        await check("Language defaults to Chinese and presets and custom languages reach the translation prompt", async () =>
        {
            Assert(new AppSettings().TargetLanguage == "简体中文" && TranslationLanguages.Presets.Count >= 20);
            foreach (string language in TranslationLanguages.Presets.Concat(new[] { "English", "Esperanto" }))
            {
                var settings = new AppSettings { Model = "fake-model", TargetLanguage = language, Stream = false }.Validate(true);
                using var provider = new OpenAiCompatibleProvider(new Handler(async (request, token) =>
                {
                    using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                    var messages = payload.RootElement.GetProperty("messages");
                    Assert(messages[0].GetProperty("content").GetString()!.Contains(language));
                    Assert(messages[1].GetProperty("content").GetString() == "Original text");
                    return Json("{\"choices\":[{\"message\":{\"content\":\"synthetic translation\"},\"finish_reason\":\"stop\"}]}");
                }));
                var translation = new TranslationRequest(1, "Original text", "auto", language, settings.ConfigId, settings.ServiceId, settings.Model);
                await foreach (string text in provider.TranslateAsync(translation, settings, "fake", default)) Assert(text == "synthetic translation");
            }
        });
        await check("Changing translation language cannot reuse a cached result in the previous language", async () =>
        {
            var provider = new LanguageProvider();
            var complete = new TaskCompletionSource<TranslationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var coordinator = new TranslationCoordinator(provider, update => { if (update.Complete) complete.TrySetResult(update); });
            var settings = new AppSettings { Model = "fake-model" };
            foreach (string language in new[] { "简体中文", "English", "English" })
            {
                complete = new(TaskCreationOptions.RunContinuationsAsynchronously);
                coordinator.Submit("Same original", settings with { TargetLanguage = language }, "fake");
                var result = await complete.Task.WaitAsync(TimeSpan.FromSeconds(3)); Assert(result.Text == language);
                if (provider.Calls == 2 && language == "English" && result.Cached) Assert(result.Text != "简体中文");
            }
            Assert(provider.Calls == 2);
        });
    }
    static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    static void Assert(bool condition) { if (!condition) throw new Exception("Model catalog assertion failed"); }
    static async Task<T> Throws<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T error) { return error; } throw new Exception("Expected " + typeof(T).Name); }
    sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token); }
    sealed class LanguageProvider : ITranslationProvider
    {
        public int Calls;
        public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        { Calls++; await Task.Yield(); token.ThrowIfCancellationRequested(); yield return request.TargetLanguage; }
    }
}
