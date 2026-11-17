using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YiKe.Core;
using YiKe.Data;

namespace YiKe.Translation;

public interface IModelCatalog
{
    Task<IReadOnlyList<string>> ListAsync(string baseUrl, string key, CancellationToken token);
}

// Lists account-visible IDs; this endpoint cannot certify Chat Completions capability.
public sealed class ModelCatalog : IModelCatalog, IDisposable
{
    const int MaximumBytes = 1024 * 1024, MaximumEntries = 4096;
    readonly HttpClient client;
    readonly TimeSpan timeout;
    public ModelCatalog(HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        this.timeout = timeout ?? TimeSpan.FromSeconds(12);
    }

    public async Task<IReadOnlyList<string>> ListAsync(string baseUrl, string key, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint.Models(baseUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CredentialStore.ValidateSecret(key));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout);
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw Error(response.StatusCode);
            if (response.Content.Headers.ContentLength > MaximumBytes) throw InvalidList();
            using var body = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            using var memory = new MemoryStream();
            var buffer = new byte[8192]; int read;
            while ((read = await body.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) > 0)
            {
                if (memory.Length + read > MaximumBytes) throw InvalidList();
                memory.Write(buffer, 0, read);
            }
            using var document = JsonDocument.Parse(memory.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() > MaximumEntries) throw InvalidList();
            var models = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String) continue;
                string value = id.GetString()!;
                if (value.Length is > 0 and <= 200 && value == value.Trim() && !value.Any(char.IsControl)) models.Add(value);
            }
            return models.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ThenBy(value => value, StringComparer.Ordinal).ToArray();
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new TranslationException("models-timeout", "获取模型超时；可点击刷新，或手动填写模型名称。"); }
        catch (JsonException) { throw InvalidList(); }
        catch (HttpRequestException)
        { throw new TranslationException("models-network", "无法获取模型，请检查地址、网络或服务证书；也可手动填写模型名称。"); }
        catch (IOException)
        { throw new TranslationException("models-network", "模型列表接收中断；可点击刷新，或手动填写模型名称。"); }
    }

    static TranslationException InvalidList() => new("models-format", "模型列表格式不兼容或过大；请按服务商文档手动填写模型名称。");
    static TranslationException Error(HttpStatusCode status) => new("models-http", status switch
    {
        HttpStatusCode.Unauthorized => "获取模型失败：API Key 无效或已过期，请检查密钥。",
        HttpStatusCode.Forbidden => "当前密钥没有读取模型列表的权限；仍可手动填写已获授权的模型。",
        HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented => "该服务未提供模型列表接口；请手动填写模型名称。",
        HttpStatusCode.TooManyRequests => "获取模型被限流，请稍后点击刷新；仍可手动填写模型名称。",
        _ => "获取模型失败，请检查 API 根地址及服务状态；仍可手动填写模型名称。"
    });
    public void Dispose() => client.Dispose();
}
