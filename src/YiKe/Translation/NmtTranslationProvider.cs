using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YiKe.Core;

namespace YiKe.Translation;

internal sealed class NmtTranslationProvider : ITranslationProvider, IDisposable
{
    readonly string root;
    readonly SemaphoreSlim operation = new(1);
    readonly CancellationTokenSource lifetime = new();
    (string Path,long Length,DateTime Modified)[]? verified;
    internal NmtTranslationProvider(string? directory=null) => root=Path.GetFullPath(directory ?? Path.Combine(AppContext.BaseDirectory,"translation-runtime"));
    internal static bool Available => File.Exists(Path.Combine(AppContext.BaseDirectory,"translation-runtime","python.exe"));
    public string CacheIdentity(AppSettings settings)=>"opus-mt-en-zh-int8-1";
    public bool CanExplainTechnical(AppSettings settings)=>false;
    public bool CanExplainAnyWord(AppSettings settings)=>false;
    public bool CanRepairTranslation(AppSettings settings)=>false;
    async Task Verify(CancellationToken token)
    {
        if(verified is not null && Array.TrueForAll(verified,s=>File.Exists(s.Path)&&new FileInfo(s.Path).Length==s.Length&&File.GetLastWriteTimeUtc(s.Path)==s.Modified)) return;
        using var resource=typeof(NmtTranslationProvider).Assembly.GetManifestResourceStream("YiKe.nmt-package") ?? throw new IOException("Missing runtime manifest");
        using var json=await JsonDocument.ParseAsync(resource,cancellationToken:token);
        var stamps=new List<(string,long,DateTime)>();
        foreach(var item in json.RootElement.GetProperty("files").EnumerateArray()) {
            var path=Path.GetFullPath(Path.Combine(root,item.GetProperty("path").GetString()!));
            if(!path.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid runtime path");
            await using var file=File.OpenRead(path);
            var hash=Convert.ToHexString(await SHA256.HashDataAsync(file,token));
            if(!hash.Equals(item.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase)) throw new IOException("Translation runtime integrity failure");
            stamps.Add((path,file.Length,File.GetLastWriteTimeUtc(path)));
        }
        verified=stamps.ToArray();
    }
    public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest request,AppSettings settings,string key,[EnumeratorCancellation] CancellationToken token)
    {
        if(!TranslationQuality.IsChinese(request.TargetLanguage) || request.TargetLanguage.Contains("繁",StringComparison.Ordinal)) throw new TranslationException("nmt-language","节能引擎提供英语到简体中文翻译；其他方向请在设置选择通用多语模型或在线服务。");
        if(System.Text.RegularExpressions.Regex.IsMatch(request.Text,@"[\u3400-\u9fff\uf900-\ufaff]")) throw new TranslationException("nmt-source","节能引擎用于英译中。中文到其他语言请选择通用多语模型或在线服务。");
        if(CommonWordTranslations.Find(request.Text,request.TargetLanguage) is { } word) {yield return word;yield break;}
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token);deadline.CancelAfter(TimeSpan.FromSeconds(settings.TotalSeconds));
        await operation.WaitAsync(deadline.Token);
        try {
            if(!File.Exists(Path.Combine(root,"python.exe"))) throw new TranslationException("nmt-missing","缺少节能翻译组件，请安装完整更新；也可选择已安装的通用模型。");
            await Verify(deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            var output=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var start=new ProcessStartInfo(Path.Combine(root,"python.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new UTF8Encoding(false),WorkingDirectory=root};
            foreach(var a in new[]{"-I","-B",Path.Combine(root,"nmt-worker.py")})start.ArgumentList.Add(a);
            foreach(var name in new[]{"OPENBLAS_NUM_THREADS","OMP_NUM_THREADS","MKL_NUM_THREADS"})start.Environment[name]="2";
            start.Environment["CT2_PACKED_GEMM"]="0";
            start.Environment["CT2_VERBOSE"]="-3";
            using var child=new OwnedLocalProcess(start,line=>{if(line.Length>131072)output.TrySetException(new IOException("Oversized translation output"));else output.TrySetResult(line);});
            using var cancel=deadline.Token.Register(()=>{try{if(!child.Process.HasExited)child.Process.Kill(true);}catch(Exception error) when(error is InvalidOperationException or System.ComponentModel.Win32Exception){} });
            await child.Process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{text=request.Text}).AsMemory(),deadline.Token);child.Process.StandardInput.Close();
            await child.Process.WaitForExitAsync(deadline.Token);
            if(child.Process.ExitCode!=0)throw new TranslationException("nmt-failed","节能翻译未完成，原文没有截断；请缩短单行或选择其他引擎。");
            using var result=JsonDocument.Parse(await output.Task.WaitAsync(deadline.Token));
            string text=result.RootElement.GetProperty("text").GetString() ?? "";
            if(string.IsNullOrWhiteSpace(text))throw new TranslationException("empty","节能引擎未返回译文。");
            yield return text;
        } finally {operation.Release();}
    }
    public void Dispose()=>lifetime.Cancel();
}
