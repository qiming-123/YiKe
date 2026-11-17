using YiKe.Translation;

public static class GlossaryExampleChecks
{
    public static async Task Run(Func<string, Func<Task>, Task> check)
    {
        await check("Every bundled computer term includes a bounded use case and outcome", () =>
        {
            var all = ProgrammingGlossary.ExampleInventory;
            Assert(all.Length == ProgrammingGlossary.TermCount && all.Length == 219);
            foreach (var item in all)
                if (string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Result) || item.Code.Length > 500 || item.Result.Length > 300)
                    throw new Exception("Missing or excessive example: " + item.Name);
            foreach (var item in all)
                Assert(!new[] { "rm -rf", "chmod 777", "/dev/sda", "git reset --hard" }.Any(danger => item.Code.Contains(danger, StringComparison.Ordinal)));
            return Task.CompletedTask;
        });
        await check("Examples follow the matching explanation and retain language and effect context", () =>
        {
            foreach (string term in new[] { "const", "let", "static", "chmod", "systemctl", "DNS", "SELECT", "COW", "JWT", "RAG" })
            {
                string notes = ProgrammingGlossary.Explain(term);
                Assert(notes.IndexOf("示例：", StringComparison.Ordinal) > notes.IndexOf('·') && notes.IndexOf("效果：", StringComparison.Ordinal) > notes.IndexOf("示例：", StringComparison.Ordinal));
            }
            Assert(ProgrammingGlossary.Explain("const").Contains("JavaScript：const user"));
            Assert(ProgrammingGlossary.Explain("let").Contains("Rust：let mut"));
            Assert(ProgrammingGlossary.Explain("chmod").Contains("chmod 755 ./script.sh"));
            Assert(!ProgrammingGlossary.Explain("The meeting starts tomorrow.").Contains("示例："));
            return Task.CompletedTask;
        });
        await check("Known model definitions also omit their examples without removing a new term's example", () =>
        {
            string notes = "fork：重复。\n\n示例：旧示例。\n效果：旧效果。\n\nmemoization：复用结果。\n示例：新示例。\n效果：新效果。";
            string filtered = TechnicalOutput.WithoutKnownDefinitions(notes, "fork / exec / wait · 进程生命周期");
            Assert(!filtered.Contains("旧示例") && !filtered.Contains("旧效果") && filtered.Contains("新示例") && filtered.Contains("新效果"));
            return Task.CompletedTask;
        });
    }
    static void Assert(bool value) { if (!value) throw new Exception("Glossary examples assertion failed"); }
}
