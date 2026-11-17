using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YiKe.UI;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        string directory = Path.GetFullPath(args.Length == 1 ? args[0] : "assets");
        Directory.CreateDirectory(directory);
        int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
        var images = new List<byte[]>();
        foreach (int size in sizes)
        {
            var mark = new MaterialMark { Width = size, Height = size };
            var bitmap = Render(mark, size, size);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream(); encoder.Save(stream); images.Add(stream.ToArray());
            if (size == 256) File.WriteAllBytes(Path.Combine(directory, "yike-icon-preview.png"), stream.ToArray());
        }
        using (var writer = new BinaryWriter(File.Create(Path.Combine(directory, "yike.ico"))))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] % 256)); writer.Write((byte)(sizes[i] % 256));
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
            }
            foreach (var bytes in images) writer.Write(bytes);
        }
        // Installer artwork uses the same drawing as the application and selection button.
        foreach (int scale in new[] { 1, 2 })
        {
            var panel = new Canvas { Width = 164, Height = 314, Background = new SolidColorBrush(Color.FromRgb(11, 17, 32)) };
            var mark = new MaterialMark { Width = 70, Height = 70 }; Canvas.SetLeft(mark, 22); Canvas.SetTop(mark, 30); panel.Children.Add(mark);
            void Label(string value, int top, int size, Color color)
            {
                var label = new TextBlock { Text = value, FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = size, Foreground = new SolidColorBrush(color) };
                Canvas.SetLeft(label, 22); Canvas.SetTop(label, top); panel.Children.Add(label);
            }
            Label("译刻", 116, 25, Colors.White); Label("划词即译\n让理解，自然发生。", 166, 12, Color.FromRgb(192, 201, 220));
            Label("YIKE / WINDOWS", 279, 9, Color.FromRgb(192, 201, 220));
            var bitmap = Render(panel, 164 * scale, 314 * scale, scale);
            var bmp = new BmpBitmapEncoder(); bmp.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(directory, $"wizard-{scale}.bmp")); bmp.Save(output);
        }
        Console.WriteLine("Exported shared WPF brand drawing: ICO (16–256 px), PNG and installer artwork.");
    }
    static RenderTargetBitmap Render(FrameworkElement element, int width, int height, int scale = 1)
    {
        var size = new Size(width / scale, height / scale);
        element.Measure(size); element.Arrange(new Rect(size)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(element); return bitmap;
    }
}
