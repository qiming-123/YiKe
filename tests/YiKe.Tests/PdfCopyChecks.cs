using System.IO;
using YiKe.Core;
using YiKe.Data;
using YiKe.Selection;

static class PdfCopyChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check, string scratch)
    {
        void Assert(bool value) { if (!value) throw new Exception("PDF copy safety assertion failed"); }
        await check("PDF copy fallback recognizes WPS and Edge PDF titles and excludes ordinary document windows", () =>
        {
            foreach (string name in new[] { "wps", "WPSPDF", "wpsoffice" })
            {
                Assert(PdfCopyPolicy.IsWpsPdf(name, "sample.PDF - WPS Office"));
                Assert(PdfCopyPolicy.IsWpsPdf(name, "sample.pdf* - WPS PDF"));
                Assert(PdfCopyPolicy.IsWpsPdf(name, "sample.pdf [只读] - WPS Office"));
                Assert(!PdfCopyPolicy.IsWpsPdf(name, "sample.pdf.docx - WPS Office"));
                Assert(!PdfCopyPolicy.IsWpsPdf(name, "打开文件") && !PdfCopyPolicy.IsWpsPdf(name, "sample.pdf - notes.docx - WPS Office"));
            }
            Assert(!PdfCopyPolicy.IsWpsPdf("chrome", "sample.pdf - WPS Office"));
            Assert(PdfCopyPolicy.IsSupportedPdf("msedge", "remark.pdf - Microsoft Edge"));
            Assert(PdfCopyPolicy.IsSupportedPdf("msedge", "remark.pdf - 个人 - Microsoft\u200b Edge"));
            Assert(!PdfCopyPolicy.IsSupportedPdf("msedge", "首页 - Microsoft Edge"));
            Assert(!PdfCopyPolicy.IsWpsPdf("explorer", "sample.pdf")); return Task.CompletedTask;
        });
        await check("PDF copy fallback never accepts unchanged clipboard data including startup and zero sequences", () =>
        {
            Assert(!PdfCopyPolicy.Fresh(20, 20, true, true, false, true));
            Assert(!PdfCopyPolicy.Fresh(20, 0, true, true, false, true));
            Assert(PdfCopyPolicy.Fresh(20, 21, true, true, false, true));
            Assert(PdfCopyPolicy.Fresh(uint.MaxValue, 1, true, true, false, true)); return Task.CompletedTask;
        });
        await check("PDF copy fallback rejects foreign clipboard owners focus changes files and nontext copies", () =>
        {
            Assert(!PdfCopyPolicy.Fresh(1, 2, false, true, false, true));
            Assert(!PdfCopyPolicy.Fresh(1, 2, true, false, false, true));
            Assert(!PdfCopyPolicy.Fresh(1, 2, true, true, true, true));
            Assert(!PdfCopyPolicy.Fresh(1, 2, true, true, false, false)); return Task.CompletedTask;
        });
        await check("PDF renderer clipboard ownership requires a child WPS PDF process from the same installation", () =>
        {
            const string root = @"C:\WPS\office6\wps.exe", owner = @"C:\WPS\office6\wpspdf.exe";
            Assert(PdfCopyPolicy.RendererOwner("WPSPDF", root, owner, true, true));
            Assert(!PdfCopyPolicy.RendererOwner("wpspdf", root, owner, false, true));
            Assert(!PdfCopyPolicy.RendererOwner("wpspdf", root, owner, true, false));
            Assert(!PdfCopyPolicy.RendererOwner("chrome", root, owner, true, true));
            Assert(!PdfCopyPolicy.RendererOwner("wpspdf", root, @"C:\Other\wpspdf.exe", true, true));
            Assert(!PdfCopyPolicy.RendererOwner("wpspdf", root, null, true, true));
            Assert(!PdfCopyPolicy.RendererOwner("wpspdf", "wps.exe", "wpspdf.exe", true, true));
            return Task.CompletedTask;
        });
        await check("PDF copy fallback waits only after silent unavailable selection reading, never after denied or confirmed text", () =>
        {
            foreach (var status in Enum.GetValues<ReadStatus>())
                Assert(PdfCopyPolicy.CanWait(status) == (status is ReadStatus.Unsupported or ReadStatus.NoSelection or ReadStatus.Timeout or ReadStatus.NotTextSelection));
            return Task.CompletedTask;
        });
        await check("PDF automatic copy excludes confirmed nontext controls, denied reads and completed text", () =>
        {
            foreach (var status in Enum.GetValues<ReadStatus>()) Assert(PdfCopyPolicy.CanAutoCopy(status) == (status is ReadStatus.Unsupported or ReadStatus.NoSelection or ReadStatus.Timeout));
            return Task.CompletedTask;
        });
        await check("PDF copy text preserves complete paragraphs Unicode and the 2000-scalar limit without fabrication", () =>
        {
            Assert(PdfCopyPolicy.ValidateText(" \n\t ").Status == ReadStatus.NoSelection);
            const string paragraph = "First paragraph.\r\nSecond paragraph 😀";
            Assert(PdfCopyPolicy.ValidateText(paragraph).Text == paragraph);
            string allowed = string.Concat(Enumerable.Repeat("😀", 2000));
            Assert(PdfCopyPolicy.ValidateText(allowed).Text == allowed);
            Assert(PdfCopyPolicy.ValidateText(allowed + "x").Status == ReadStatus.TooLong);
            return Task.CompletedTask;
        });
        await check("PDF copy preference defaults on and roundtrips without changing API configurations", () =>
        {
            var store = new SettingsStore(userDirectory: Path.Combine(scratch, "pdf-copy-preference"));
            var settings = OnlineProfiles.Add(OnlineProfiles.Upgrade(new AppSettings()));
            Assert(settings.TranslateCopiedPdf);
            store.Save(settings with { TranslateCopiedPdf = false });
            var restored = store.Load(); Assert(!restored.TranslateCopiedPdf && restored.OnlineProfiles.Length == 2 && restored.ServiceId == settings.ServiceId);
            return Task.CompletedTask;
        });
    }
}
