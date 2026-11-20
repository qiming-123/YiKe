using System.Net;
using System.Net.Http;
using System.IO;
using System.Text;
using System.Text.Json;
using YiKe.Core;
using YiKe.Data;
using YiKe.Translation;

if(args.Length>0&&args[0]=="--uia-worker")return YiKe.Program.Main(args);
if(args.Length>0&&args[0]=="--installer-info")return YiKe.Program.Main(args);
if(args.Length>0&&(args[0]=="--export-vscode"||args[0]=="--install-vscode"))return YiKe.Program.Main(args);
if(args.Length==2 && args[0]=="--verify-study-packs")
{
    string scratchPath = Path.Combine(Path.GetTempPath(),"YiKe-lex-"+Guid.NewGuid().ToString("N"));
    try
    {
        foreach(var (code,name) in StudyDictionaryPack.Languages)
        {
            var pack = new StudyDictionaryPack(scratchPath,code,name);
            pack.InstallDictionary(Path.Combine(args[1],code+".ykdict"));
            var word = code switch { "ja"=>"学校","ko"=>"학교","fr"=>"maison","de"=>"Haus",_=>"casa" };
            var entry = new StudyDictionaryPack(scratchPath,code,name).Find(word);
            if(pack.Count<10000 || string.IsNullOrWhiteSpace(entry?.Meaning)) throw new InvalidDataException(code+" lookup failed");
            Console.WriteLine($"{code} {pack.Count} {word} {entry.Meaning.Split('\n')[0]}");
        }
    }
    finally { if(Directory.Exists(scratchPath)) Directory.Delete(scratchPath,true); }
    return 0;
}
if(args.Length==2&&args[0]=="--vscode-desktop-check")return VsCodeDesktopCheck.Run(args[1]);
if(args.Length==3&&args[0]=="--vscode-terminal-live")return await VsCodeTerminalLiveCheck.Run(args[1],args[2]);
if(args.Length==2&&args[0]=="--worker-fixture")return await WorkerFixtures.Run(args[1]);
if(args.Length==3&&args[0]=="--legacy-live")return LegacyLiveCheck.Run(args[1],args[2]);
if(args.Length==3&&args[0]=="--selection-live")return await LegacyLiveCheck.RunWorker(args[1],args[2]);
if(args.Length==3&&args[0]=="--wps-live")return LegacyLiveCheck.Run(args[1],args[2],true);
if(args.Length==3&&args[0]=="--local-live")return await LocalModelLiveCheck.Run(args[1], args[2]);
if(args.Length==3&&args[0]=="--local-live-light")return await LocalModelLiveCheck.Run(args[1], args[2], true);
if(args.Length==4&&args[0]=="--technical-live")return await TechnicalLiveCheck.Run(args[1], args[2], args[3]=="light");
if(args.Length==4&&args[0]=="--word-live")return await WordTranslationLiveCheck.Run(args[1],args[2],args[3]=="light");
if(args.Length==4&&args[0]=="--any-word-live")return await AnyWordNotesLiveCheck.Run(args[1],args[2],args[3]=="light");
if(args.Length==4&&args[0]=="--latency-live")return await ResponseLatencyLiveCheck.Run(args[1],args[2],args[3]=="light");
if(args.Length==3&&args[0]=="--word-probe")return await WordTranslationLiveCheck.Run(args[1],args[2],false,true);
if(args.Length==3&&args[0]=="--local-parent-fixture")return await LocalModelLiveCheck.ParentFixture(args[1], args[2]);
if(args.Length==3&&args[0]=="--local-parent-light")return await LocalModelLiveCheck.ParentFixture(args[1], args[2], true);
if(args.Contains("--visual"))return VisualChecks.Run();
if(args.Contains("--ui-regression"))return UiRegressionChecks.Run(args.ElementAtOrDefault(1));
if(args.Length==2&&args[0]=="--pdf-copy-live")return PdfCopyLiveCheck.Run(args[1]);
if(args.Length==2&&args[0]=="--install-data")return InstallDataFixture.Run(args[1]);
if(args.Length==2&&args[0]=="--desktop-data")return DesktopDataFixture.Run(args[1]);
if(args.Length==3 && args[0]=="--memory-180") return await Upgrade180Checks.Memory(args[1],args[2]);
if(args.Length==2 && args[0]=="--nmt-180") return await Upgrade180Checks.Nmt(args[1]);
int passed = 0, failed = 0;
async Task Check(string name, Func<Task> test) { try { await test(); Console.WriteLine("PASS " + name); passed++; } catch (Exception e) { Console.WriteLine("FAIL " + name + " / " + e); failed++; } }
void Assert(bool value, string why = "assertion failed") { if (!value) throw new Exception(why); }
void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("expected " + typeof(T).Name); }
await Upgrade180Checks.Run(Check);
var s = new AppSettings { TranslationMode = "online", Model = "test-model" };
var req = new TranslationRequest(1, "Hello\n\n42", "auto", "简体中文", s.ConfigId, s.ServiceId, s.Model);
await Check("HTTPS endpoint validation and path", () => { Assert(Endpoint.Completions("https://example.test/v1/").AbsolutePath == "/v1/chat/completions"); foreach (var u in new[] { "http://localhost", "https://a@b/v1", "https://x/v1?key=x", "https://x/#frag", "https://x/v1/chat/completions" }) Throws<ArgumentException>(() => Endpoint.Normalize(u)); return Task.CompletedTask; });
await Check("Unicode scalar limit without truncation", () => { Assert(TextLimit.Count("汉😀a") == 3); Assert(TextLimit.Allowed(string.Concat(Enumerable.Repeat("😀", 2000)))); Assert(!TextLimit.Allowed(new string('a', 2001))); Assert(!TextLimit.Allowed(" \r\n")); return Task.CompletedTask; });
var scratch = Path.Combine(Environment.CurrentDirectory, "artifacts", "test-scratch-" + Guid.NewGuid()); Directory.CreateDirectory(scratch);
await Check("DPAPI save/reload/update and endpoint binding", () => { var c = new CredentialStore(scratch); c.Save(s, "fake-test-key"); Assert(c.Read(s) == "fake-test-key"); Assert(c.Read(s with { BaseUrl = "https://other.test/v1" }) is null); c.Save(s, "fake-replacement"); Assert(new CredentialStore(scratch).Read(s) == "fake-replacement"); Assert(!Encoding.UTF8.GetString(Directory.GetFiles(Path.Combine(scratch, "credentials")).SelectMany(File.ReadAllBytes).ToArray()).Contains("fake-test-key")); return Task.CompletedTask; });
await Check("Failed configuration commit preserves previous credential and revision",()=>{var st=new SettingsStore(userDirectory:Path.Combine(scratch,"failed-commit"));var cred=new CredentialStore(st.UserDirectory);var original=ConfigurationService.Commit(s,s,"fake-original",st,cred);File.SetAttributes(st.UserFile,FileAttributes.ReadOnly);try{Throws<UnauthorizedAccessException>(()=>ConfigurationService.Commit(original,original with{Model="changed"},"fake-new-key",st,cred));Assert(st.Load().Revision==original.Revision);Assert(st.Load().Model==original.Model);Assert(cred.Read(st.Load())=="fake-original");}finally{File.SetAttributes(st.UserFile,FileAttributes.Normal);}return Task.CompletedTask;});
await Check("Ordinary export excludes key; import creates fresh credential binding", () => { var file = Path.Combine(scratch, "export.json"); SettingsStore.Export(file, s); Assert(!File.ReadAllText(file).Contains("fake-")); Assert(SettingsStore.Import(file).ServiceId != s.ServiceId); return Task.CompletedTask; });
await Check("Portable reconciliation selects newer local revision", () => { var b = Path.Combine(scratch, "portable"); Directory.CreateDirectory(b); File.WriteAllText(Path.Combine(b, "portable.flag"), ""); var user = Path.Combine(scratch, "user"); var st = new SettingsStore(b, user); var v = st.Save(s); SettingsStore.Export(st.UserFile, v with { Revision = 8, Model = "new" }); Assert(st.Load().Model == "new"); var foreign = s with { ConfigId = Guid.NewGuid(), Model = "foreign", Revision = 1 }; SettingsStore.Export(st.PortableFile!, foreign); Assert(st.Load().Model == "foreign"); return Task.CompletedTask; });
await Check("Portable unwritable copy commits durable user fallback", () => { var b = Path.Combine(scratch, "blocked"); Directory.CreateDirectory(b); File.WriteAllText(Path.Combine(b, "portable.flag"), ""); File.WriteAllText(Path.Combine(b, "data"), "block-directory"); var st = new SettingsStore(b, Path.Combine(scratch, "fallback")); st.Save(s); Assert(st.Notice is not null); Assert(st.Load().Model == s.Model); return Task.CompletedTask; });
await Check("Portable-only load establishes EXE-only move fallback",()=>{var b=Path.Combine(scratch,"portable-only");Directory.CreateDirectory(b);File.WriteAllText(Path.Combine(b,"portable.flag"),"");var u=Path.Combine(scratch,"mirror");var st=new SettingsStore(b,u);SettingsStore.Export(st.PortableFile!,s);Assert(st.Load().ConfigId==s.ConfigId);Assert(new SettingsStore(Path.Combine(scratch,"exe-only"),u).Load().ConfigId==s.ConfigId);return Task.CompletedTask;});
byte[] encrypted = Array.Empty<byte>();
await Check("Password migration roundtrip", () => { encrypted = MigrationService.Encrypt(s, "fake-migration-key", "correct-password-42"); Assert(!Encoding.UTF8.GetString(encrypted).Contains("fake-migration-key")); var p = MigrationService.Decrypt(encrypted, "correct-password-42"); Assert(p.ApiKey == "fake-migration-key" && p.Settings.Model == s.Model); return Task.CompletedTask; });
await Check("Wrong password/tampering never touches saved config", () => { var st = new SettingsStore(Path.Combine(scratch, "plain"), Path.Combine(scratch, "atomic")); st.Save(s); byte[] before = File.ReadAllBytes(st.UserFile); Throws<InvalidDataException>(() => MigrationService.Decrypt(encrypted, "wrong-password")); var obj = JsonNodeHelper.Tamper(encrypted); Throws<InvalidDataException>(() => MigrationService.Decrypt(obj, "correct-password-42")); Assert(before.SequenceEqual(File.ReadAllBytes(st.UserFile))); return Task.CompletedTask; });
string chunk(string delta, string? finish = null) => "data: " + JsonSerializer.Serialize(new { choices = new[] { new { delta = new { content = delta, reasoning_content = "MUST NOT DISPLAY" }, finish_reason = finish } } }) + "\r\n\r\n";
await Check("SSE arbitrary UTF8 boundaries, empty delta, multiline event, DONE", async () => { string body = ": heartbeat\r\n\r\n" + chunk("") + "data: {\r\ndata: \"choices\": [{\"delta\": {\"content\": \"你好😀\"}, \"finish_reason\": null}]}\r\n\r\n" + chunk("\n42") + chunk("", "stop") + "data: [DONE]\n\n"; using var p = new OpenAiCompatibleProvider(new FakeHttp(HttpStatusCode.OK, body, true, 1)); var values = new List<string>(); await foreach(var d in p.TranslateAsync(req, s, "fake", default)) values.Add(d); Assert(string.Concat(values) == "你好😀\n42"); });
await Check("SSE disconnect marks incomplete", async () => { using var p = new OpenAiCompatibleProvider(new FakeHttp(HttpStatusCode.OK, chunk("partial"), true, 2)); try { await foreach(var _ in p.TranslateAsync(req, s, "fake", default)) {} } catch(TranslationException e) { Assert(e.Code == "interrupted"); return; } throw new Exception("partial accepted"); });
await Check("Truncation/refusal/empty responses rejected", () => { foreach (string finish in new[] { "length", "content_filter", "tool_calls" }) Throws<TranslationException>(() => OpenAiCompatibleProvider.ParseNonStreaming(JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = finish, message = new { content = "partial" } } } }))); Throws<TranslationException>(() => OpenAiCompatibleProvider.ParseNonStreaming("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"\"}}]}")); return Task.CompletedTask; });
await Check("Nonstream completion and request prompt isolation", async () => { var h = new FakeHttp(HttpStatusCode.OK, "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"你好\"}}]}", false, 3); using var p = new OpenAiCompatibleProvider(h); var value = new StringBuilder(); await foreach(var d in p.TranslateAsync(req, s with { Stream = false }, "fake-key", default)) value.Append(d); Assert(value.ToString() == "你好"); using var doc = JsonDocument.Parse(h.RequestBody!); Assert(doc.RootElement.GetProperty("messages").GetArrayLength()==2); Assert(doc.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()==req.Text); Assert(h.Auth=="Bearer fake-key"); Assert(!h.RequestBody!.Contains("temperature")); });
await Check("HTTP error taxonomy and redirect denial", async () => { foreach(var code in new[] { HttpStatusCode.Unauthorized,HttpStatusCode.Forbidden,HttpStatusCode.NotFound,HttpStatusCode.TooManyRequests,HttpStatusCode.InternalServerError,HttpStatusCode.Redirect }) { var h = new FakeHttp(code, "{\"error\":{\"code\":\"insufficient_quota\"}}", false, 2); using var p = new OpenAiCompatibleProvider(h); try { await foreach(var _ in p.TranslateAsync(req,s,"fake",default)){} throw new Exception("HTTP accepted"); } catch(TranslationException e) { if(code==HttpStatusCode.TooManyRequests) Assert(e.Code=="quota"); if(code==HttpStatusCode.Redirect) Assert(e.Code=="redirect"); } Assert(h.Calls==1); } });
await Check("First-response timeout cancels once without retry",async()=>{var h=new SlowHttp();using var p=new OpenAiCompatibleProvider(h);try{await foreach(var _ in p.TranslateAsync(req,s with{FirstResponseSeconds=1},"fake",default)){}throw new Exception("timeout ignored");}catch(OperationCanceledException){Assert(h.Calls==1);}});
await Check("A/B/C bounded queue and late A isolation", async () => { var fake = new ControlledProvider(); var updates = new List<TranslationUpdate>(); using var c = new TranslationCoordinator(fake,u=> {lock(updates)updates.Add(u);}); c.Submit("A",s,"fake"); await fake.Started.Task.WaitAsync(TimeSpan.FromSeconds(3)); c.Submit("B",s,"fake"); long final = c.Submit("C",s,"fake"); fake.Release.TrySetResult(); await Task.Delay(300); lock(updates)Assert(updates.Where(u=>u.Sequence==final).Last().Text=="C"); Assert(!fake.Calls.Contains("B") && fake.Maximum==1); lock(updates)Assert(!updates.Any(u=>u.Text=="A")); });
await Check("Close/invalidate cannot reopen via late provider update", async () => { var f = new ControlledProvider(); var updates = new List<TranslationUpdate>(); using var c = new TranslationCoordinator(f,u=>{lock(updates)updates.Add(u);}); c.Submit("A",s,"fake"); await f.Started.Task.WaitAsync(TimeSpan.FromSeconds(3)); c.Invalidate(); int count;lock(updates)count=updates.Count; f.Release.TrySetResult(); await Task.Delay(150); lock(updates)Assert(updates.Count==count); });
await Check("Failure contracts discard stale text, including serialized worker results", () => { foreach(var status in Enum.GetValues<ReadStatus>().Where(x=>x!=ReadStatus.Success)) { var r = new SelectionReadResult(status,"stale selection"); Assert(r.Text is null); Assert(JsonSerializer.Deserialize<SelectionReadResult>(JsonSerializer.Serialize(new {Status=status,Text="stale selection"}))!.Text is null); } return Task.CompletedTask; });
await RegressionChecks.Run(Check, scratch);
await SelectionReaderChecks.Run(Check);
await LegacyTextChecks.Run(Check);
await WpsSelectionChecks.Run(Check);
await VsCodeChecks.Run(Check);
await ModelCatalogChecks.Run(Check);
await LocalModelChecks.Run(Check, scratch);
await LocalInstallationChecks.Run(Check, scratch);
await ProgrammingGlossaryChecks.Run(Check);
await ComputerTermsChecks.Run(Check);
await GlossaryExampleChecks.Run(Check);
await TerminalCommandChecks.Run(Check);
await TranslationQualityChecks.Run(Check);
await AnyWordNotesChecks.Run(Check);
await ResponseLatencyChecks.Run(Check);
await AutomaticSelectionChecks.Run(Check);
await OnlineProfileChecks.Run(Check, scratch);
await PdfCopyChecks.Run(Check, scratch);
Console.WriteLine($"RESULT {passed} passed, {failed} failed");
Directory.Delete(scratch,true);
return failed == 0 ? 0 : 1;

sealed class FakeHttp(HttpStatusCode status,string body,bool sse,int block) : HttpMessageHandler
{
 public string? RequestBody, Auth; public int Calls;
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct) { Calls++;RequestBody=await r.Content!.ReadAsStringAsync(ct);Auth=r.Headers.Authorization?.ToString();var response=new HttpResponseMessage(status){Content=new StreamContent(new ChunkedStream(Encoding.UTF8.GetBytes(body),block))};response.Content.Headers.ContentType=new(sse?"text/event-stream":"application/json");return response; }
}
sealed class ChunkedStream(byte[] bytes,int block) : MemoryStream(bytes)
{ public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default)=>base.ReadAsync(buffer[..Math.Min(block,buffer.Length)],ct); public override int Read(byte[] buffer,int offset,int count)=>base.Read(buffer,offset,Math.Min(block,count)); }
sealed class ControlledProvider : ITranslationProvider
{
 public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);public List<string> Calls=new();public int Maximum,current;
 public async IAsyncEnumerable<string> TranslateAsync(TranslationRequest r,AppSettings s,string key,[System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken ct) { Calls.Add(r.Text);Maximum=Math.Max(Maximum,Interlocked.Increment(ref current));try {if(r.Text=="A"){Started.TrySetResult();await Release.Task;}yield return r.Text;}finally{Interlocked.Decrement(ref current);} }
}
static class JsonNodeHelper {public static byte[] Tamper(byte[] bytes){var o=System.Text.Json.Nodes.JsonNode.Parse(bytes)!;var cipher=Convert.FromBase64String(o["Ciphertext"]!.GetValue<string>());cipher[0]^=1;o["Ciphertext"]=Convert.ToBase64String(cipher);return Encoding.UTF8.GetBytes(o.ToJsonString());}}
sealed class SlowHttp:HttpMessageHandler{public int Calls;protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;await Task.Delay(Timeout.Infinite,token);return new(HttpStatusCode.OK);}}
