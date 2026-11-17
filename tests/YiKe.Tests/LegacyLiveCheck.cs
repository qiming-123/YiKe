using System.IO;
using System.Text.Json;
using YiKe.Core;
using YiKe.Selection;

static class LegacyLiveCheck
{
    public static int Run(string input, string output, bool wps = false)
    {
        int exit = 1;
        var thread = new Thread(() =>
        {
            try
            {
                var gesture = JsonSerializer.Deserialize<Gesture>(File.ReadAllText(input))!;
                var result = (wps ? WpsSelectionAdapter.TryRead(gesture) : LegacyTextReader.TryRead(gesture)) ?? new(ReadStatus.Unsupported);
                File.WriteAllText(output, JsonSerializer.Serialize(result));
                exit = result.Status == ReadStatus.Success ? 0 : 1;
            }
            catch (Exception error) { File.WriteAllText(output, error.GetType().Name); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start();
        return thread.Join(5000) ? exit : 2;
    }
    public static async Task<int> RunWorker(string input, string output)
    {
        var gesture = JsonSerializer.Deserialize<Gesture>(File.ReadAllText(input))!;
        using var reader = new SelectionReader();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await reader.ReadSettledAsync(gesture, CancellationToken.None);
        File.WriteAllText(output, JsonSerializer.Serialize(new { result.Status, result.Text, elapsedMs = watch.ElapsedMilliseconds }));
        return result.Status == ReadStatus.Success ? 0 : 1;
    }
}
