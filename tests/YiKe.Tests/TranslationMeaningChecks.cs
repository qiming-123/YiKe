using System.IO;
using System.Text.RegularExpressions;
using YiKe.Core;
using YiKe.Translation;

static class TranslationMeaningChecks
{
    sealed record Case(string Name, string Source, string[] Required, string? Forbidden, string[] Accepted, string Rejected)
    {
        public bool Matches(string answer) => Required.All(pattern => Regex.IsMatch(answer, pattern)) &&
            (Forbidden is null || !Regex.IsMatch(answer, Forbidden));
    }

    static readonly Case[] Cases =
    {
        new("negation", "Do not delete the backup.",
            new[] { "(?:不要|不得|请勿|禁止|不能|不可|切勿|勿)\\s*(?:删除|移除|删掉|删去|清除)", "备份" }, null,
            new[] { "请勿删除备份。", "不要移除备份文件。" }, "请删除备份。"),
        new("condition", "Restart the service only if the check fails.",
            new[] { "(?:检查|检测|校验|验证)(?:未通过|不通过|失败|出错|未成功)", "(?:重新启动|重启)服务", "(?:只有|仅当|仅在|只在|除非|才(?:能)?|方可)" },
            "(?:检查|检测|校验|验证)(?:通过|成功)时.{0,5}(?:重新启动|重启)",
            new[] { "仅在检查失败时重启服务。", "如果校验未通过，才重新启动服务。", "除非检查失败，否则请勿重启服务。" }, "检查失败后重启服务。"),
        new("terminology", "The mutex prevents concurrent writes to the shared counter.",
            new[] { "(?:互斥锁|互斥量|互斥体)", "共享(?:的)?计数器", "(?:防止|避免|阻止|防范)", "(?:并发|同时)[^。！？]{0,14}写" },
            "(?:允许|支持)[^。！？]{0,10}(?:并发|同时)[^。！？]{0,10}写",
            new[] { "互斥锁防止对共享计数器的并发写入。", "互斥量避免同时写入共享的计数器。" }, "互斥锁允许同时写入共享计数器。")
    };

    internal static async Task CheckFixtures(Func<string, Func<Task>, Task> check)
    {
        foreach (var item in Cases)
            await check("Translation meaning fixture accepts synonyms and rejects a changed " + item.Name, () =>
            {
                if (!item.Accepted.All(item.Matches) || item.Matches(item.Rejected))
                    throw new InvalidDataException("Meaning fixture classification mismatch: " + item.Name);
                return Task.CompletedTask;
            });
    }

    internal static async Task<int> Run(string directory)
    {
        if (!File.Exists(Path.Combine(directory, "engine", "llama-server.exe")) ||
            !LocalInstallation.Evaluate(LocalHardware.Detect(), directory).Supported)
        {
            Console.WriteLine("SKIP translation meaning: compatible local model unavailable");
            return 77;
        }

        using var provider = new LocalTranslationProvider(directory);
        var settings = new AppSettings();
        int failed = 0;
        foreach (var item in Cases)
        {
            var completed = new TaskCompletionSource<TranslationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var coordinator = new TranslationCoordinator(provider, update =>
            {
                if (!update.Running) completed.TrySetResult(update);
            }, useCache: false);
            coordinator.Submit(item.Source, settings, "");
            try
            {
                var result = await completed.Task.WaitAsync(TimeSpan.FromMinutes(3));
                bool pass = result.Error is null && item.Matches(result.Text);
                Console.WriteLine((pass ? "PASS " : "FAIL ") + item.Name);
                if (!pass) failed++;
            }
            catch (TimeoutException) { Console.WriteLine("FAIL " + item.Name + " (timeout)"); failed++; }
        }
        return failed == 0 ? 0 : 1;
    }
}
