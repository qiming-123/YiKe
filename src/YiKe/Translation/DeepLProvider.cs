using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using YiKe.Core;

namespace YiKe.Translation;

internal sealed class DeepLProvider : ITranslationProvider, IDisposable
{
    readonly HttpClient client;
    internal DeepLProvider(HttpMessageHandler? handler=null) => client=new(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
    public string CacheIdentity(AppSettings settings) => "deepl-v2";
    public bool CanExplainTechnical(AppSettings settings) => false;
    public bool CanExplainAnyWord(AppSettings settings) => false;
    public bool CanRepairTranslation(AppSettings settings) => false;
    static string Language(string target) => target.Split('（')[0] switch {
        "简体中文" => "ZH-HANS", "繁體中文" or "繁体中文" => "ZH-HANT", "英语" => "EN", "日语" => "JA", "韩语" => "KO",
        "法语" => "FR", "德语" => "DE", "西班牙语" => "ES", "俄语" => "RU", "葡萄牙语" => "PT", "意大利语" => "IT",
        "印地语" => "HI", "泰语" => "TH", "越南语" => "VI", "印度尼西亚语" => "ID", "瑞典语" => "SV", "马来语" => "MS",
        "阿拉伯语" => "AR", "荷兰语" => "NL", "波兰语" => "PL", "土耳其语" => "TR", "乌克兰语" => "UK",
        _ when System.Text.RegularExpressions.Regex.IsMatch(target, "^[A-Za-z]{2}(-[A-Za-z]{2,4})?$") => target.ToUpperInvariant(),
        _ => throw new TranslationException("deepl-language", "此目标语言尚未映射到 DeepL，请填写语言代码或选择兼容 AI 服务。") };
    public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest request, AppSettings settings, string key, [EnumeratorCancellation] CancellationToken token)
    {
        var endpoint = new Uri(Endpoint.Normalize(settings.BaseUrl));
        if (endpoint.Host is not ("api.deepl.com" or "api-free.deepl.com") || endpoint.AbsolutePath.TrimEnd('/') != "/v2")
            throw new TranslationException("deepl-endpoint", "DeepL 地址请选择 https://api.deepl.com/v2 或 https://api-free.deepl.com/v2。");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(settings.TotalSeconds));
        using var message = new HttpRequestMessage(HttpMethod.Post, settings.BaseUrl.TrimEnd('/') + "/translate");
        message.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", key);
        var payload = new Dictionary<string,object> { ["text"] = new[] { request.Text }, ["target_lang"] = Language(request.TargetLanguage) };
        if (request.Context.Length > 0) payload["context"] = request.Context;
        if (request.SourceLanguage != "auto") payload["source_lang"] = Language(request.SourceLanguage).Split('-')[0];
        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new TranslationException("deepl-http", $"DeepL 返回 {(int)response.StatusCode}，请核对密钥、额度和语言支持；未自动重试。");
        using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        using var memory = new System.IO.MemoryStream(); var buffer = new byte[8192]; int count;
        while ((count = await stream.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) > 0) { if (memory.Length + count > 262144) throw new TranslationException("deepl-size", "DeepL 响应过大。"); memory.Write(buffer, 0, count); }
        using var json = JsonDocument.Parse(memory.ToArray());
        string text = json.RootElement.GetProperty("translations")[0].GetProperty("text").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(text)) throw new TranslationException("empty", "DeepL 没有返回译文。");
        yield return text;
    }
    public void Dispose() => client.Dispose();
}
