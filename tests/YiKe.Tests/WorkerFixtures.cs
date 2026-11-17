using System.Text;
using System.Text.Json;
using YiKe.Core;

public static class WorkerFixtures
{
    public static async Task<int> Run(string mode)
    {
        Console.InputEncoding = new UTF8Encoding(false); Console.OutputEncoding = new UTF8Encoding(false);
        string? line;
        while ((line = await Console.In.ReadLineAsync()) is not null)
        {
            if (mode == "hang") { await Task.Delay(Timeout.Infinite); return 0; }
            if (mode == "eof") return 0;
            if (mode == "invalid") { Console.WriteLine("invalid-json"); return 0; }
            var gesture = JsonSerializer.Deserialize<Gesture>(line)!;
            Console.WriteLine(JsonSerializer.Serialize(new SelectionReadResult(ReadStatus.Success, "synthetic selection " + gesture.Sequence)));
            Console.Out.Flush();
        }
        return 0;
    }
}
