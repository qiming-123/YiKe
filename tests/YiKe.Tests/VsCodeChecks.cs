using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using YiKe.Core;
using YiKe.Selection;

static class VsCodeChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check)
    {
        await check("VS Code adapter rejects other processes, ancestry cycles and unrelated trees", () =>
        {
            Assert(VsCodeSelectionAdapter.IsCode("Code") && VsCodeSelectionAdapter.IsCode("Code - Insiders") && !VsCodeSelectionAdapter.IsCode("Codex"));
            var parents = new Dictionary<int,int> { [1] = 0, [2] = 1, [3] = 2, [4] = 5, [5] = 4 };
            Assert(VsCodeSelectionAdapter.IsDescendant(3, 1, parents)); Assert(!VsCodeSelectionAdapter.IsDescendant(4, 1, parents)); Assert(!VsCodeSelectionAdapter.IsDescendant(6, 1, parents)); return Task.CompletedTask;
        });
        await check("VS Code pipe response requires protocol version, exact nonce and a known status", () =>
        {
            foreach (string json in new[] { "{}", "[]", "null", "{broken", "{\"version\":2,\"nonce\":\"n\",\"status\":\"Success\",\"text\":\"stale\"}", "{\"version\":1,\"nonce\":\"old\",\"status\":\"Success\",\"text\":\"stale\"}", "{\"version\":1,\"nonce\":\"n\",\"status\":\"Other\"}" }) Assert(VsCodeSelectionAdapter.ParseResponse(json, "n") is null);
            return Task.CompletedTask;
        });
        await check("VS Code responses preserve text and bound Unicode scalars without leaking failure text", () =>
        {
            SelectionReadResult? Parse(string status, string text) => VsCodeSelectionAdapter.ParseResponse(JsonSerializer.Serialize(new { version = 1, nonce = "n", status, text }), "n");
            Assert(Parse("Success", "selected\n中文 😀") is { Status: ReadStatus.Success, Text: "selected\n中文 😀" });
            Assert(Parse("Success", string.Concat(Enumerable.Repeat("😀",2000)))?.Status == ReadStatus.Success);
            Assert(Parse("Success", string.Concat(Enumerable.Repeat("😀",2001))) is { Status: ReadStatus.TooLong, Text: null });
            Assert(Parse("NoSelection", "stale")?.Text is null && Parse("TooLong", "stale")?.Text is null && Parse("Success", "  ")?.Status == ReadStatus.NoSelection); return Task.CompletedTask;
        });
        await check("Embedded VSIX contains exact extension resources and matching identity", () =>
        {
            string path = Path.Combine(Path.GetTempPath(), "YiKe-vsix-test-" + Guid.NewGuid().ToString("N") + ".vsix");
            try
            {
                VsCodeIntegration.ExportPackage(path); using var zip = ZipFile.OpenRead(path);
                Assert(zip.Entries.Count == 7);
                foreach (string name in new[] { "extension.js", "selection.js", "package.json", "README.md", "LICENSE" }) Assert(zip.GetEntry("extension/" + name) is not null);
                using var package = JsonDocument.Parse(zip.GetEntry("extension/package.json")!.Open());
                var xml = XDocument.Load(zip.GetEntry("extension.vsixmanifest")!.Open());
                XNamespace ns = "http://schemas.microsoft.com/developer/vsx-schema/2011";
                Assert((string?)xml.Descendants(ns + "Identity").Single().Attribute("Version") == package.RootElement.GetProperty("version").GetString());
                Assert(package.RootElement.GetProperty("extensionKind")[0].GetString() == "ui");
            }
            finally { File.Delete(path); }
            return Task.CompletedTask;
        });
    }
    static void Assert(bool value) { if (!value) throw new Exception("VS Code adapter assertion failed"); }
}
