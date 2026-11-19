using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using YiKe.Core;
using YiKe.Selection;

namespace YiKe.UI;

internal sealed class SelectionIcon : Window
{
    internal SelectionSnapshot? Selection { get; private set; }
    internal event Action<SelectionSnapshot>? Requested;
    internal SelectionIcon()
    {
        Title = "译刻 · 点击翻译"; Width = 44; Height = 38;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        Ui.WindowTheme(this);
        AllowsTransparency = true; Background = System.Windows.Media.Brushes.Transparent;
        var button = Ui.Button("译", () => { var selected = Selection; Clear(); if (selected is not null) Requested?.Invoke(selected); });
        var template = new ControlTemplate(typeof(System.Windows.Controls.Button));
        var presenter = new FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter)); presenter.SetValue(System.Windows.Controls.ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); presenter.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); template.VisualTree = presenter;
        button.Template = template; button.Content = new MaterialMark { SelectionLens = true, Width = 38, Height = 38 }; button.Padding = new Thickness(0); button.ToolTip = "点击翻译所选文字";
        button.MouseEnter += (_, _) => button.Opacity = .78; button.MouseLeave += (_, _) => button.Opacity = 1;
        Content = button;
        SourceInitialized += (_, _) => {
            var h = (HwndSource)PresentationSource.FromVisual(this);
            Native.SetWindowLongPtr(h.Handle, -20, new IntPtr(Native.GetWindowLongPtr(h.Handle, -20).ToInt64() | 0x08000080L));
            h.AddHook((IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled) => { if (msg == 0x21) { handled = true; return new IntPtr(3); } return IntPtr.Zero; });
        };
    }
    internal void Present(SelectionSnapshot snapshot)
    {
        Selection = snapshot; Show();
        var area = System.Windows.Forms.Screen.FromPoint(new(snapshot.X, snapshot.Y)).WorkingArea;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        var p = PopupPlacement.Calculate(snapshot.X, snapshot.Y, (int)(Width * dpi.DpiScaleX), (int)(Height * dpi.DpiScaleY), new(area.Left, area.Top, area.Right, area.Bottom));
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, new(-1), p.X, p.Y, 0, 0, 0x11);
    }
    internal void Clear() { Selection = null; Hide(); }
}
