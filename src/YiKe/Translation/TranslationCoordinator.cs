using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YiKe.Core;

namespace YiKe.Translation;

// One executing request and one replaceable pending request, even when a provider ignores cancellation.
public sealed class TranslationCoordinator : IDisposable
{
    sealed record Work(TranslationRequest Request, AppSettings Settings, string Key, bool Force);
    sealed record CachedResult(string Text, string ModelNotes);
    readonly object gate = new();
    readonly ITranslationProvider provider;
    readonly Action<TranslationUpdate> publish;
    readonly bool useCache;
    readonly Dictionary<string, CachedResult> cache = new();
    readonly Queue<string> order = new();
    Work? pending;
    Work? executing;
    TranslationUpdate? latestUpdate;
    CancellationTokenSource? active;
    bool running, disposed;
    long sequence;
    public long Sequence { get { lock (gate) return sequence; } }
    public TranslationCoordinator(ITranslationProvider provider, Action<TranslationUpdate> publish, bool useCache = true) { this.provider = provider; this.publish = publish; this.useCache = useCache; }
    public long Invalidate()
    {
        lock (gate) { sequence++; pending = null; latestUpdate = null; active?.Cancel(); return sequence; }
    }
    public long Submit(string text, AppSettings s, string key, bool force = false, string sourceLanguage = "auto", string context = "")
    {
        lock (gate)
        {
            if (disposed) throw new ObjectDisposedException(nameof(TranslationCoordinator));
            if (!TextLimit.Allowed(text)) throw new ArgumentException("请选择 1–2,000 个 Unicode 字符（按码点计数），不会截断原文。");
            if (context.Length > 0 && !TextLimit.Allowed(context)) throw new ArgumentException("语境最多支持 2,000 个字符，请缩短原文。");
            bool Same(Work work) => work.Request.SourceLanguage == sourceLanguage && work.Request.Context == context && work.Request.Text == text && work.Settings == s && work.Key == key && work.Request.Sequence == sequence;
            if (!force && pending is not null && Same(pending)) return sequence;
            if (!force && pending is null && executing is not null && active is { IsCancellationRequested: false } && Same(executing) && latestUpdate?.Error is null && latestUpdate is not { Complete: true, ExplanationPending: false })
            {
                if (latestUpdate is not null) publish(latestUpdate);
                return sequence;
            }
            long id = ++sequence; active?.Cancel();
            latestUpdate = null;
            pending = new(new(id, text, sourceLanguage, s.TargetLanguage, s.ConfigId, s.ServiceId, s.TranslationMode == "local" ? LocalModelPackage.ModelId : s.Model, s.TranslationMode == "local" ? "local-4" : "4", Context: context), s, key, force);
            if (!running) { running = true; _ = Task.Run(Pump); }
            return id;
        }
    }
    void Emit(TranslationUpdate update) { lock (gate) if (!disposed && update.Sequence == sequence) { latestUpdate = update; publish(update); } }
    static string CacheKey(Work w, string modelIdentity) => JsonSerializer.Serialize(new { w.Request.Context, w.Request.Text, w.Request.SourceLanguage, w.Request.TargetLanguage, w.Request.ConfigId, w.Request.ServiceId, ModelIdentity = modelIdentity, w.Request.Model, w.Request.PromptVersion, w.Request.ExplainTechnical, w.Settings.TranslationMode, w.Settings.BaseUrl, w.Settings.Stream, w.Settings.InstructionRole });
    async Task Pump()
    {
        while (true)
        {
            Work w; CancellationTokenSource cts;
            lock (gate) { if (pending is null || disposed) { running = false; return; } w = executing = pending; pending = null; active = cts = new(); }
            var analysis = w.Settings.ExplainProgrammingTerms ? ProgrammingGlossary.Analyze(w.Request.Text) : new ProgrammingGlossary.Analysis("", false);
            bool universal = w.Settings.ExplainProgrammingTerms && analysis.Notes.Length == 0;
            string baseNotes = universal ? ComputingWordNotes.Explain(w.Request.Text) : analysis.Notes;
            var text = new TechnicalOutput(false);
            string retainedNotes = "", noteFailure = "";
            bool explanationPending = false;
            string ModelNotes() => retainedNotes.Length > 0 ? retainedNotes : text.Notes;
            string Notes(string model, bool incomplete = false)
            {
                model = TechnicalOutput.WithoutKnownDefinitions(model, analysis.KnownTerms);
                return baseNotes + (model.Length == 0 ? "" : (baseNotes.Length > 0 ? "\n\n" : "") + (incomplete ? "模型补充 · 尚未完成，请结合语境核对\n" : explanationPending ? "模型补充 · 正在生成，请结合语境核对\n" : "模型补充 · 请结合语境核对\n") + model) + (noteFailure.Length == 0 ? "" : "\n\n" + noteFailure);
            }
            void Send(TranslationUpdate update)
            {
                bool invalid = TranslationQuality.NeedsRepair(w.Request.Text, update.Text, w.Request.TargetLanguage, analysis.KnownTerms);
                Emit(update with { Text = invalid ? "" : update.Text, ProgrammingNotes = Notes(invalid ? "" : ModelNotes(), update.Error is not null), ExplanationPending = explanationPending });
            }
            async Task Generate(TranslationRequest request, CancellationToken token)
            {
                var last = DateTime.UtcNow; bool sentText = false;
                await foreach (var delta in provider.TranslateAsync(request, w.Settings, w.Key, token).ConfigureAwait(false))
                {
                    token.ThrowIfCancellationRequested(); text.Append(delta);
                    bool readable = text.Text.Length > 0 && !TranslationQuality.NeedsRepair(w.Request.Text, text.Text, w.Request.TargetLanguage, analysis.KnownTerms);
                    if ((!sentText && readable) || (DateTime.UtcNow - last).TotalMilliseconds >= 80) { Send(new(w.Request.Sequence, text.Text)); sentText |= readable; last = DateTime.UtcNow; }
                }
                token.ThrowIfCancellationRequested(); text.Finish();
            }
            try
            {
                bool supplement = analysis.RequestModelNotes && w.Settings.SupplementTechnicalTerms && provider.CanExplainTechnical(w.Settings);
                bool canRepair = w.Settings.TranslationMode == "local" && provider.CanRepairTranslation(w.Settings);
                bool separateSupplement = universal && !supplement && w.Settings.SupplementTechnicalTerms && provider.CanExplainAnyWord(w.Settings);
                w = w with { Request = w.Request with { ExplainTechnical = supplement, KnownTechnicalTerms = analysis.KnownTerms, PromptVersion = supplement ? "technical-5" : w.Request.PromptVersion } };
                text = new TechnicalOutput(supplement);
                string cacheKey = CacheKey(w, provider.CacheIdentity(w.Settings));
                bool fromCache = useCache && !w.Force && cache.TryGetValue(cacheKey, out var cached) && !TranslationQuality.NeedsRepair(w.Request.Text, cached.Text, w.Request.TargetLanguage, analysis.KnownTerms);
                if (fromCache)
                {
                    var saved = cache[cacheKey]; text.Append(saved.Text); text.Finish();
                    retainedNotes = w.Settings.ExplainProgrammingTerms && w.Settings.SupplementTechnicalTerms ? saved.ModelNotes : "";
                }
                else
                {
                    Send(new(w.Request.Sequence, ""));
                    bool emptyFailure = false;
                    try { await Generate(w.Request, cts.Token).ConfigureAwait(false); }
                    catch (TranslationException error) when (canRepair && error.Code is "empty" or "local-empty") { emptyFailure = true; }
                    if (canRepair && (emptyFailure || TranslationQuality.NeedsRepair(w.Request.Text, text.Text, w.Request.TargetLanguage, analysis.KnownTerms)))
                    {
                        cts.Token.ThrowIfCancellationRequested(); retainedNotes = text.Notes; text = new TechnicalOutput(false);
                        Send(new(w.Request.Sequence, ""));
                        using var repairBudget = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                        repairBudget.CancelAfter(TimeSpan.FromSeconds(Math.Min(45, w.Settings.TotalSeconds)));
                        await Generate(w.Request with { ExplainTechnical = false, RefineTranslation = true }, repairBudget.Token).ConfigureAwait(false);
                    }
                }
                cts.Token.ThrowIfCancellationRequested();
                if (TranslationQuality.NeedsRepair(w.Request.Text, text.Text, w.Request.TargetLanguage, analysis.KnownTerms))
                    throw new TranslationException("translation-invalid", w.Settings.TranslationMode == "local" ? "本地模型没有生成有效译文。可缩小选区或主动重试；未把原词或单独解释当作翻译成功。" : "服务没有返回有效译文。可主动重试或更换模型；未自动重发请求。");
                void Store()
                {
                    if (!useCache) return;
                    cacheKey = CacheKey(w, provider.CacheIdentity(w.Settings));
                    if (!cache.ContainsKey(cacheKey)) order.Enqueue(cacheKey);
                    cache[cacheKey] = new(text.Text, ModelNotes());
                    while (order.Count > 32) cache.Remove(order.Dequeue());
                }
                Store();
                if (separateSupplement && ModelNotes().Length == 0)
                {
                    explanationPending = true;
                    Send(new(w.Request.Sequence, text.Text, true, Cached: fromCache));
                    try
                    {
                        using var noteBudget = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                        noteBudget.CancelAfter(TimeSpan.FromSeconds(Math.Min(30, w.Settings.TotalSeconds)));
                        var notes = new StringBuilder(); var last = DateTime.UtcNow;
                        await foreach (string delta in provider.TranslateAsync(w.Request with { ExplainTechnical = false, RefineTranslation = false, NotesOnly = true }, w.Settings, w.Key, noteBudget.Token).ConfigureAwait(false))
                        {
                            noteBudget.Token.ThrowIfCancellationRequested();
                            notes.Append(delta.AsSpan(0, Math.Min(delta.Length, TechnicalOutput.MaximumNotes - notes.Length)));
                            retainedNotes = ComputingWordNotes.Clean(notes.ToString());
                            if ((DateTime.UtcNow - last).TotalMilliseconds >= 120) { Send(new(w.Request.Sequence, text.Text, true, Cached: fromCache)); last = DateTime.UtcNow; }
                            if (notes.Length >= TechnicalOutput.MaximumNotes) break;
                        }
                        noteBudget.Token.ThrowIfCancellationRequested();
                        if (!ComputingWordNotes.IsComplete(retainedNotes)) throw new TranslationException("notes-empty", "模型没有给出完整的说明、示例与效果。");
                    }
                    catch (OperationCanceledException) when (!cts.IsCancellationRequested) { retainedNotes = ""; noteFailure = "模型说明等待超时；已保留本机说明和完成的译文。"; }
                    catch (Exception ex) when (ex is TranslationException or HttpRequestException or JsonException or System.IO.IOException or InvalidOperationException or ArgumentException or UnauthorizedAccessException or System.Security.SecurityException or System.ComponentModel.Win32Exception)
                    { retainedNotes = ""; noteFailure = "模型说明暂不可用；已保留本机说明和完成的译文。"; }
                    cts.Token.ThrowIfCancellationRequested(); explanationPending = false; Store();
                }
                Send(new(w.Request.Sequence, text.Text, true, Cached: fromCache));
            }
            catch (OperationCanceledException) { if (!cts.IsCancellationRequested) Send(new(w.Request.Sequence, text.Text, Error: w.Settings.TranslationMode == "local" ? "本地翻译等待超时，请缩短原文或稍后重试。" : "等待服务超时；译文未完成，请主动重试。")); }
            catch (TranslationException ex) { Send(new(w.Request.Sequence, text.Text, Error: ex.Message)); }
            catch (HttpRequestException) { Send(new(w.Request.Sequence, text.Text, Error: w.Settings.TranslationMode == "local" ? "本地引擎连接中断，请重试；未调用在线服务。" : "网络连接失败，请检查网络、服务地址或系统证书。")); }
            catch (Exception ex) when (ex is JsonException or System.IO.IOException or InvalidOperationException or ArgumentException or System.Collections.Generic.KeyNotFoundException or UnauthorizedAccessException or System.Security.SecurityException or System.ComponentModel.Win32Exception)
            { Send(new(w.Request.Sequence, text.Text, Error: w.Settings.TranslationMode == "local" ? "本地翻译未完成，请检查文件权限或重新安装完整离线包。" : "服务响应格式错误或连接中断；译文未完成。")); }
            finally { lock (gate) if (ReferenceEquals(active, cts)) { active = null; executing = null; } cts.Dispose(); }
        }
    }
    public void Dispose() { lock (gate) { disposed = true; sequence++; pending = null; active?.Cancel(); } }
}
