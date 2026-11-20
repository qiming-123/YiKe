using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shell;

namespace YiKe.UI;

internal enum PrismKind { Window, Card, Button, Badge }

// Vector-only ornamentation: no bitmap background, animation, WebView or timer.
internal sealed class PrismPanel : Border
{
    public PrismPanel() { }
    internal static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(PrismKind), typeof(PrismPanel), new FrameworkPropertyMetadata(PrismKind.Card, FrameworkPropertyMetadataOptions.AffectsRender));
    public PrismKind Kind { get => (PrismKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    internal static SolidColorBrush Brush(string value) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); brush.Freeze(); return brush; }
    internal static LinearGradientBrush Neon()
    {
        var brush = new LinearGradientBrush { StartPoint = new(0,0), EndPoint = new(1,1) };
        brush.GradientStops.Add(new(Brush("#6DF5F5").Color,0)); brush.GradientStops.Add(new(Brush("#8A68ED").Color,.5)); brush.GradientStops.Add(new(Brush("#F276D9").Color,1)); brush.Freeze(); return brush;
    }
    internal static Geometry Outline(double width, double height, double inset, PrismKind kind)
    {
        double l=inset, t=inset, r=Math.Max(l,width-inset), b=Math.Max(t,height-inset), cut=Math.Min(kind==PrismKind.Window?24:kind==PrismKind.Button?8:16,Math.Min(r-l,b-t)/3);
        Point[] points = kind==PrismKind.Badge
            ? new[] {new Point(width*.5,t),new Point(r,height*.28),new Point(r,height*.68),new Point(width*.5,b),new Point(l,height*.72),new Point(l,height*.32)}
            : new[] {new Point(l+cut,t),new Point(r-cut,t),new Point(r,t+cut),new Point(r,b-cut),new Point(r-cut,b),new Point(l,b),new Point(l,t+cut)};
        var shape=new StreamGeometry(); using(var context=shape.Open()){context.BeginFigure(points[0],true,true); context.PolyLineTo(points[1..],true,false);} shape.Freeze(); return shape;
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info); Clip=Outline(ActualWidth,ActualHeight,0,Kind);
    }
    protected override void OnRender(DrawingContext dc)
    {
        if(ActualWidth<1 || ActualHeight<1)return;
        var outline=Outline(ActualWidth,ActualHeight,1,Kind);
        bool contrast=SystemParameters.HighContrast;
        Brush fill=contrast?SystemColors.WindowBrush:Background ?? Brush("#121729");
        Brush edge=contrast?SystemColors.WindowTextBrush:Kind==PrismKind.Button?BorderBrush ?? Neon():Neon();
        dc.DrawGeometry(fill,new Pen(edge,Kind==PrismKind.Window?1.6:1),outline);
        if(contrast)return;
        if(Kind==PrismKind.Button && Background==Brushes.Transparent && BorderBrush==Brushes.Transparent)return;
        // Titanium bevel surrounds recessed glass, with restrained edge reflections.
        double bevel = Kind == PrismKind.Window ? 6 : Kind == PrismKind.Card ? 4 : 3;
        dc.DrawGeometry(MaterialMark.Metal("#8EACBA", "#323B51", "#AC85BC"),
            new Pen(Brush("#C1C7D3"), .55), Outline(ActualWidth,ActualHeight,2,Kind));
        var glass = Outline(ActualWidth,ActualHeight,bevel,Kind);
        dc.DrawGeometry(fill, new Pen(Brush(Theme.Dark ? "#080E1C" : "#A6AEC8"),1.2),glass);
        dc.DrawGeometry(null, new Pen(MaterialMark.Metal("#92F0F1", "#45536E", "#BE91D9"),.65),
            Outline(ActualWidth,ActualHeight,bevel+2,Kind));
        dc.PushClip(glass);
        var reflection = new LinearGradientBrush { StartPoint=new(0,0), EndPoint=new(0,1) };
        reflection.GradientStops.Add(new(Color.FromArgb(26,190,219,245),0));
        reflection.GradientStops.Add(new(Color.FromArgb(5,190,219,245),.45));
        reflection.GradientStops.Add(new(Colors.Transparent,.46));
        dc.DrawRectangle(reflection,null,new Rect(0,0,ActualWidth,ActualHeight));
        if(Kind is PrismKind.Window or PrismKind.Card)
        {
            var glow=new RadialGradientBrush(Brush("#222BA4B0").Color,Colors.Transparent);
            dc.DrawEllipse(glow,null,new(ActualWidth*.12,0),ActualWidth*.7,ActualHeight*.8);
            var violet=new RadialGradientBrush(Brush("#226F448D").Color,Colors.Transparent);
            dc.DrawEllipse(violet,null,new(ActualWidth,ActualHeight*.9),ActualWidth*.6,ActualHeight*.8);
        }
        dc.Pop();
    }
}

internal static class CyberPrism
{
    internal static FrameworkElement Badge(int size=52) => new MaterialMark
    { Width=size, Height=size, HorizontalAlignment=HorizontalAlignment.Left };
    internal static FrameworkElement Monogram(int size=76) => new MaterialMark
    { Width=size, Height=size, Monogram=true, HorizontalAlignment=HorizontalAlignment.Left };
    internal static Border Card(UIElement child,int padding=16) { var panel = new PrismPanel { Kind=PrismKind.Card, Child=child, Padding=new Thickness(padding+3) }; panel.SetResourceReference(Border.BackgroundProperty,"CardFinish"); return panel; }
    internal static Style ButtonStyle()
    {
        var style=new Style(typeof(Button),(Style)Application.Current.FindResource(typeof(Button)));
        var panel=new FrameworkElementFactory(typeof(PrismPanel));panel.Name="Prism";panel.SetValue(PrismPanel.KindProperty,PrismKind.Button);
        foreach(var property in new[]{Border.BackgroundProperty,Border.BorderBrushProperty,Border.PaddingProperty})panel.SetBinding(property,new Binding(property.Name){RelativeSource=RelativeSource.TemplatedParent});
        var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
        content.SetBinding(FrameworkElement.HorizontalAlignmentProperty,new Binding("HorizontalContentAlignment"){RelativeSource=RelativeSource.TemplatedParent});
        panel.AppendChild(content);
        var template=new ControlTemplate(typeof(Button)){VisualTree=panel};
        var hover=new Trigger {Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(UIElement.OpacityProperty,.84,"Prism"));template.Triggers.Add(hover);
        var focus=new Trigger {Property=UIElement.IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,PrismPanel.Brush("#C3FEFF"),"Prism"));template.Triggers.Add(focus);
        var disabled=new Trigger {Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.45,"Prism"));template.Triggers.Add(disabled);
        style.Setters.Add(new Setter(Control.TemplateProperty,template));return style;
    }
    internal static UIElement Frame(Window window,UIElement body)
    {
        window.WindowStyle=WindowStyle.None;window.AllowsTransparency=true;window.Background=Brushes.Transparent;
        WindowChrome.SetWindowChrome(window,new WindowChrome {CaptionHeight=34,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false});
        var grid=new Grid();grid.RowDefinitions.Add(new(){Height=new GridLength(34)});grid.RowDefinitions.Add(new());
        var caption=new DockPanel {LastChildFill=false,Margin=new Thickness(24,0,22,0)};
        var controls=new StackPanel {Orientation=Orientation.Horizontal};DockPanel.SetDock(controls,Dock.Right);
        foreach (var (label, geometry, action) in new[] {
            ("最小化", "M2,8 H16 V10 H2 Z", (Action)(() => SystemCommands.MinimizeWindow(window))),
            ("最大化 / 还原", "F0 M2,2 H16 V16 H2 Z M4,4 V14 H14 V4 Z", () => { if (window.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(window); else SystemCommands.MaximizeWindow(window); }),
            ("关闭", "M2,3.5 L3.5,2 L9,7.5 L14.5,2 L16,3.5 L10.5,9 L16,14.5 L14.5,16 L9,10.5 L3.5,16 L2,14.5 L7.5,9 Z", window.Close)
        })
        {
            var button = Ui.Icon(label,geometry,action);
            var glyph = (System.Windows.Shapes.Path)button.Content;
            glyph.Stroke = null; glyph.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,"Text");
            glyph.Stretch = Stretch.None; glyph.Width = glyph.Height = 18;
            button.Width = 34; button.Height = 26; button.Margin = new Thickness(2,0,2,0);
            button.SetResourceReference(Control.BackgroundProperty,"Soft");
            WindowChrome.SetIsHitTestVisibleInChrome(button,true); controls.Children.Add(button);
        }
        caption.Children.Add(controls);grid.Children.Add(caption);
        var divider=new Border{BorderBrush=PrismPanel.Brush("#324063"),BorderThickness=new Thickness(0,1,0,0),Child=body};Grid.SetRow(divider,1);grid.Children.Add(divider);
        var frame = new PrismPanel {Kind=PrismKind.Window,Padding=new Thickness(10,7,10,10),Child=grid}; frame.SetResourceReference(Border.BackgroundProperty,"FrameFinish"); return frame;
    }
}
