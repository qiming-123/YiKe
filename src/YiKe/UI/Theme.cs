using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YiKe.UI;

public static class Theme
{
    public static bool Dark { get; private set; }
    public static void Apply(string choice)
    {
        Dark = true;
        var r = Application.Current.Resources;
        void Set(string key, string value) => r[key] = new SolidColorBrush(SystemParameters.HighContrast ? key switch { "Accent" => SystemColors.HighlightColor, "OnAccent" => SystemColors.HighlightTextColor, "Text" or "Muted" or "Line" or "Good" or "Error" => SystemColors.WindowTextColor, _ => SystemColors.WindowColor } : (Color)ColorConverter.ConvertFromString(value));
        Set("Surface", "#111827"); Set("Canvas", "#090E1A"); Set("Soft", "#192237"); Set("Line", "#30415B");
        Set("Text", "#F1F2F7"); Set("Muted", "#B0B7CC"); Set("Accent", "#67E8DD"); Set("AccentSoft", "#202642"); Set("OnAccent", "#081C24"); Set("Good", "#8FD1BB"); Set("Error", "#FFACB6");
        r["BodyFont"] = new FontFamily("Segoe UI Variable, Segoe UI, Microsoft YaHei UI");
    }
    public static void InstallStyles()
    {
        var styles = new ResourceDictionary { Source = new Uri("/YiKe;component/UI/Styles.xaml", UriKind.Relative) };
        Application.Current.Resources.MergedDictionaries.Add(styles);
    }
}

public static class Ui
{
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    public static void ConstrainToWorkArea(Window window)
    {
        double width = window.Width, height = window.Height, minWidth = window.MinWidth, minHeight = window.MinHeight;
        bool updating = false;
        void Fit(bool initial)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (updating || hwnd == IntPtr.Zero) return;
            updating = true;
            try
            {
                var area = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea;
                var dpi = VisualTreeHelper.GetDpi(window);
                var plan = WindowSizing.Calculate(width, height, minWidth, minHeight, area.Width, area.Height, dpi.DpiScaleX, dpi.DpiScaleY);
                window.MinWidth = window.MinHeight = 0;
                window.MaxWidth = plan.MaxWidth; window.MaxHeight = plan.MaxHeight;
                window.MinWidth = plan.MinWidth; window.MinHeight = plan.MinHeight;
                if (initial) { if (double.IsFinite(plan.Width)) window.Width = plan.Width; if (double.IsFinite(plan.Height)) window.Height = plan.Height; }
            }
            finally { updating = false; }
        }
        window.SourceInitialized += (_, _) => Fit(true);
        window.LocationChanged += (_, _) => Fit(false);
        window.DpiChanged += (_, _) => window.Dispatcher.BeginInvoke(() => Fit(false), System.Windows.Threading.DispatcherPriority.Render);
        window.Loaded += (_, _) =>
        {
            Fit(false);
            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            var area = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea;
            if (!YiKe.Selection.Native.GetWindowRect(hwnd, out var rect)) return;
            int x = Math.Clamp(rect.Left, area.Left, Math.Max(area.Left, area.Right - (rect.Right - rect.Left)));
            int y = Math.Clamp(rect.Top, area.Top, Math.Max(area.Top, area.Bottom - (rect.Bottom - rect.Top)));
            if (x != rect.Left || y != rect.Top) YiKe.Selection.Native.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, 0x15);
        };
    }
    public static void WindowTheme(Window window)
    { window.SetResourceReference(Control.BackgroundProperty, "Surface"); window.SetResourceReference(Control.ForegroundProperty, "Text"); window.SetResourceReference(Control.FontFamilyProperty, "BodyFont"); window.FontSize = 13; window.SourceInitialized += (_, _) => { if (!SystemParameters.HighContrast) { int dark = 1; DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(window).Handle, 20, ref dark, sizeof(int)); } }; }
    public static TextBlock Text(string text, double size = 13, string brush = "Text", bool bold = false)
    { var t = new TextBlock { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal }; t.SetResourceReference(TextBlock.ForegroundProperty, brush); return t; }
    public static Button Button(string text, Action action, bool primary = false)
    {
        var b = new Button { Content = text }; if (primary) { b.SetResourceReference(Control.BackgroundProperty, "Accent"); b.SetResourceReference(Control.ForegroundProperty, "OnAccent"); b.SetResourceReference(Control.BorderBrushProperty, "Accent"); }
        b.Click += (_, _) => action(); System.Windows.Automation.AutomationProperties.SetName(b, text); return b;
    }
    public static Border Card(UIElement child, int padding = 16, string color = "Soft")
    { var b = new Border { Child = child, Padding = new Thickness(padding), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1) }; b.SetResourceReference(Border.BackgroundProperty, color); b.SetResourceReference(Border.BorderBrushProperty, "Line"); return b; }
    public static FrameworkElement Mark(int size = 38)
    {
        var p = new System.Windows.Shapes.Path { Data = Geometry.Parse("M4,5 L15,5 M9,2 L9,5 M6,5 C6,10 10,14 15,15 M13,5 C13,10 9,14 4,16 M15,21 L19,10 L23,21 M16.5,17 L21.5,17"), StrokeThickness = 1.7, Width = 25, Height = 25, Stretch = Stretch.Uniform };
        p.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Accent"); var border = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(10), Child = p }; border.SetResourceReference(Border.BackgroundProperty, "AccentSoft"); return border;
    }
    public static Button Icon(string label, string geometry, Action action)
    {
        var p = new System.Windows.Shapes.Path { Data = Geometry.Parse(geometry), StrokeThickness = 1.6, Width = 16, Height = 16, Stretch = Stretch.Uniform }; p.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Muted");
        var b = Button(label, action); b.Content = p; b.ToolTip = label; b.Padding = new Thickness(8); b.Background = Brushes.Transparent; b.BorderThickness = new Thickness(0); return b;
    }
}
