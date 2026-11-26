using System.Diagnostics;
using System.Text;
using System.Text.Json;
using YiKe.Core;
using YiKe.Translation;

internal static class Upgrade180Checks
{
    internal static async Task Run(Func<string, Func<Task>, Task> check)
    {
        await check("180 defaults are translation only and options survive serialization", () => {
            var s = new AppSettings(); if (s.ExplainProgrammingTerms || s.SupplementTechnicalTerms || s.LearnEnglish || s.SelectionIconMode) throw new Exception("Defaults");
            var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s with { LearnEnglish = true, SelectionIconMode = true, CleanPdfText = false }))!;
            if (!restored.LearnEnglish || !restored.SelectionIconMode || restored.CleanPdfText) throw new Exception("Roundtrip"); return Task.CompletedTask;
        });
        await check("180 PDF cleanup preserves paragraphs, code and uncertain hyphens", () => {
            if (PdfText.Clean("The selected\ntext is ready.\n\nNext paragraph.") != "The selected text is ready.\n\nNext paragraph.") throw new Exception("Paragraph");
            foreach (var text in new[] { "git commit\n--amend", "var a = 1;\nreturn a;", "1. First\n2. Second", "-12\n-34", "a\tb\nc\td" }) if (PdfText.Clean(text) != text) throw new Exception("Changed structured text");
            if (BundledEnglishDictionary.Details("hello") is not { Length: > 0 }) throw new Exception("Dictionary"); return Task.CompletedTask;
        });
        await check("PDF cleanup joins prose around code and tables without changing their line endings", () => {
            const string preserved = "```\r\nfirst\r\nsecond\r\n\r\nthird\r\nfourth\r\n```\r\n\r\nname  count\r\nitem  four\r\n\r\na\tb\r\nc\td";
            string original = "This sentence\r\ncontinues here.\r\n\r\n" + preserved + "\r\n\r\nThe next\r\nparagraph ends here.";
            string expected = "This sentence continues here.\r\n\r\n" + preserved + "\r\n\r\nThe next paragraph ends here.";
            if (PdfText.Clean(original) != expected) throw new Exception("Mixed PDF blocks were changed or left unjoined.");
            return Task.CompletedTask;
        });
        await check("180 DeepL uses its dedicated protocol and never retries a billable request", async () => {
            var handler=new DeepLHttp(); using var provider=new DeepLProvider(handler);
            var s=new AppSettings { TranslationMode="online", Preset="DeepL", BaseUrl="https://api-free.deepl.com/v2", Model="DeepL" };
            var r=new TranslationRequest(1,"Hello","auto","英语（English）",s.ConfigId,s.ServiceId,"DeepL");
            var answer=new StringBuilder();await foreach(var part in provider.TranslateAsync(r,s,"fake-test-key",default))answer.Append(part);
            if(answer.ToString()!="Hello"||handler.Calls!=1||handler.Authorization!="DeepL-Auth-Key"||handler.Target!="EN")throw new Exception("DeepL protocol mismatch");
            handler.Fail=true;
            try {await foreach(var part in provider.TranslateAsync(r,s,"fake-test-key",default)) {} throw new Exception("Expected rate limit error");}
            catch(TranslationException) {if(handler.Calls!=2)throw new Exception("Retried billable request");}
        });
        await check("180 offline engine selection persists and invalid values fail before use", () => {
            var s=new AppSettings {LocalEngine="nmt"};
            if(JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s))!.Validate().LocalEngine!="nmt")throw new Exception("Engine persistence");
            if(ProgrammingGlossary.TranslateTerm("class")!="类")throw new Exception("Computing sense");
            try{(s with {LocalEngine="invalid"}).Validate();throw new Exception("Accepted invalid engine");}catch(ArgumentException){}
            return Task.CompletedTask;
        });
    }
    internal static async Task<int> Nmt(string root)
    {
        using var provider=new NmtTranslationProvider(root); var s=new AppSettings { LocalEngine="nmt" };
        var r=new TranslationRequest(1,"Do not close this window until the download has finished.","auto","简体中文",s.ConfigId,s.ServiceId,"");
        var answer=new StringBuilder(); await foreach(var part in provider.TranslateAsync(r,s,"",default)) answer.Append(part);
        if(!answer.ToString().Contains("之前") || !answer.ToString().Contains("不要")) throw new Exception("Negation translation failed: "+answer);
        Console.WriteLine(answer); return 0;
    }
    internal static async Task<int> Memory(string directory, string output)
    {
        using var provider = new LocalTranslationProvider(directory, () => new(8 * LocalHardware.GiB, 3 * LocalHardware.GiB, 4, true));
        await provider.PrepareAsync(default);
        using var process = Process.GetProcessById(provider.EngineProcessId);
        long peakWorking=0, peakPrivate=0;
        var results = new List<object>();
        foreach (var source in new[] { "The meeting starts at nine tomorrow morning.", "Do not close this window until the download has finished.", "Unmarking a square does not uncover it.", "There are three mines next to this square.", "Only restart the service if the configuration is valid." }) {
            var settings=new AppSettings(); var r=new TranslationRequest(1,source,"auto","简体中文",settings.ConfigId,settings.ServiceId,LocalModelPackage.LightModelId);
            var text=new StringBuilder(); var timer=Stopwatch.StartNew();
            await foreach(var delta in provider.TranslateAsync(r,settings,"",default)) {text.Append(delta); process.Refresh(); peakWorking=Math.Max(peakWorking,process.WorkingSet64);peakPrivate=Math.Max(peakPrivate,process.PrivateMemorySize64);}
            results.Add(new {source,translation=text.ToString(),milliseconds=timer.ElapsedMilliseconds});
        }
        System.IO.File.WriteAllText(output,JsonSerializer.Serialize(new {simulatedHardware=true,actualMemoryPressure=false,peakWorkingBytes=peakWorking,peakPrivateBytes=peakPrivate,profile=provider.ActiveProfile,results},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Engine peak working {peakWorking/1048576} MiB; private {peakPrivate/1048576} MiB");return 0;
    }
}

internal sealed class DeepLHttp : System.Net.Http.HttpMessageHandler
{
    internal int Calls;internal string? Authorization,Target;internal bool Fail;
    protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken token)
    {
        Calls++;Authorization=request.Headers.Authorization?.Scheme;
        if(request.RequestUri?.AbsolutePath!="/v2/translate")throw new Exception("Wrong endpoint");
        using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));Target=json.RootElement.GetProperty("target_lang").GetString();
        return new(Fail?System.Net.HttpStatusCode.TooManyRequests:System.Net.HttpStatusCode.OK){Content=new System.Net.Http.StringContent("{\"translations\":[{\"text\":\"Hello\"}]}")};
    }
}
