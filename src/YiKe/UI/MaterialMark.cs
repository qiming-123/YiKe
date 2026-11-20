using System.Windows;
using System.Windows.Media;

namespace YiKe.UI;

// Small, resolution-independent facets keep the selection affordance crisp at any DPI.
internal sealed class MaterialMark : FrameworkElement
{
    static readonly DependencyProperty FinishProperty = DependencyProperty.Register("Finish", typeof(Brush), typeof(MaterialMark), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public MaterialMark() => SetResourceReference(FinishProperty, "RailFinish");
    internal bool SelectionLens { get; init; }
    internal bool Monogram { get; init; }
    static readonly Geometry TranslationGlyph = CreateTranslationGlyph();
    static Geometry CreateTranslationGlyph()
    {
        var text = new FormattedText("译", System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),
            FlowDirection.LeftToRight, new Typeface(new FontFamily("Microsoft YaHei UI"),
                FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 64, Brushes.White, 1);
        var geometry = text.BuildGeometry(new Point());
        var bounds = geometry.Bounds;
        double scale = System.Math.Min(49 / bounds.Width, 50 / bounds.Height);
        var transform = new TransformGroup();
        transform.Children.Add(new TranslateTransform(-bounds.X, -bounds.Y));
        transform.Children.Add(new ScaleTransform(scale, scale));
        transform.Children.Add(new TranslateTransform(60 - bounds.Width * scale / 2, 59 - bounds.Height * scale / 2));
        geometry.Transform = transform; geometry.Freeze(); return geometry;
    }
    internal static LinearGradientBrush Metal(string first, string middle, string last)
    {
        var brush = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 1) };
        brush.GradientStops.Add(new(PrismPanel.Brush(first).Color, 0));
        brush.GradientStops.Add(new(PrismPanel.Brush(middle).Color, .46));
        brush.GradientStops.Add(new(PrismPanel.Brush(last).Color, 1));
        brush.Freeze(); return brush;
    }
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        dc.PushTransform(new ScaleTransform(ActualWidth / 100, ActualHeight / 100));
        void Facet(string path, Brush fill, Brush? stroke = null) =>
            dc.DrawGeometry(fill, stroke is null ? null : new Pen(stroke, .65), Geometry.Parse(path));
        var silver = Metal("#FFFFFF", "#BECBD9", "#68768D");
        var cyan = Metal("#C8FFFF", "#3B93A6", "#122A3D");
        var violet = Metal("#F6E5FF", "#9C7CC7", "#30284B");
        var dark = Metal("#34435C", "#101B2C", "#090E19");
        if (SystemParameters.HighContrast)
        {
            if (Monogram)
                Facet("M6,17 L24,17 40,40 57,17 75,17 48,53 48,85 32,85 32,53 Z M53,51 L80,17 97,17 68,51 98,85 79,85 Z", SystemColors.WindowTextBrush);
            else
            {
                Facet("M7,19 L19,6 77,6 82,12 82,72 72,80 7,80 Z", SystemColors.WindowBrush, SystemColors.WindowTextBrush);
                Facet("M22,36 L36,22 89,22 96,29 96,85 87,94 22,94 Z", SystemColors.WindowBrush, SystemColors.WindowTextBrush);
                dc.DrawGeometry(SystemColors.WindowTextBrush, null, TranslationGlyph);
            }
        }
        else if (SelectionLens)
        {
            // A polished edge encloses a single speech bubble. Keep the lettering clear at tray scale.
            Facet("M24,7 H76 Q94,7 94,25 V60 Q94,76 78,76 H45 L26,93 L27,77 Q7,76 7,60 V25 Q7,7 24,7 Z",
                Metal("#C8FFFF","#808CD5","#EBD1FF"), dark);
            Facet("M25,12 H76 Q89,12 89,26 V59 Q89,70 76,71 H43 L32,81 L33,71 H25 Q12,71 12,59 V26 Q12,12 25,12 Z",
                Theme.Dark ? Metal("#277C94","#263255","#533469") : Metal("#D0FFFA","#B3CAED","#B9A0DC"));
            Facet("M14,28 Q14,14 27,14 H75 Q82,14 85,19 L64,25 L18,40 Z",
                Metal("#BCFFFFFF","#538AEEEF","#00EAF3FB"));
            dc.PushTransform(new TranslateTransform(-9,-15));
            dc.DrawGeometry(Theme.Dark ? Brushes.White : PrismPanel.Brush("#172C4B"),null,TranslationGlyph);
            dc.Pop();
        }
        else if (Monogram)
        {
            Facet("M6,17 L24,17 40,40 57,17 75,17 48,53 48,85 32,85 32,53 Z", dark, PrismPanel.Neon());
            Facet("M6,17 L23,21 40,47 40,80 32,85 32,53 Z", cyan);
            Facet("M23,21 L40,47 60,21 75,17 48,53 40,80 40,47 Z", silver);
            Facet("M53,51 L80,17 97,17 68,51 98,85 79,85 Z", violet, PrismPanel.Brush("#B8A3D9"));
            Facet("M53,51 L82,21 97,17 68,51 88,79 79,85 Z", silver);
            Facet("M53,51 L61,52 79,85 70,81 Z", dark);
        }
        else
        {
            // Rear cyan glass: a broad, quiet face and narrow polished bevel.
            Facet("M7,19 L19,6 77,6 82,12 82,72 72,80 7,80 Z", cyan, PrismPanel.Brush("#94EAF2"));
            Facet("M10,21 L21,10 75,10 78,14 78,70 70,76 10,76 Z", Theme.Dark ? Metal("#22687A", "#103648", "#10202F") : Metal("#EDFDFE", "#8EDBE8", "#627DB4"));
            Facet("M7,19 L19,6 77,6 75,10 21,10 10,21 Z", silver);
            Facet("M10,21 L21,10 75,10 10,65 Z", Metal("#405BB9BD", "#30376F84", "#00132937"));
            // Front violet glass remains dark behind the white translation glyph.
            Facet("M22,36 L36,22 89,22 96,29 96,85 87,94 22,94 Z", violet, PrismPanel.Brush("#D3B2ED"));
            Facet("M25,37 L37,26 88,26 92,30 92,83 85,90 25,90 Z", Theme.Dark ? Metal("#24344F", "#151C32", "#392045") : Metal("#FCFAFF", "#D4CFF1", "#92A6CD"));
            Facet("M22,36 L36,22 89,22 88,26 37,26 25,37 25,90 22,94 Z", silver);
            Facet("M25,37 L37,26 88,26 25,87 Z", Metal("#324DABC2", "#184A6C91", "#00322848"));
            Facet("M92,30 L96,29 96,85 87,94 22,94 25,90 85,90 92,83 Z", Metal("#9A8EC0", "#634D8F", "#C58DE4"));
            // Shallow metal relief, never the heavily distorted lettering of a render.
            dc.PushTransform(new TranslateTransform(.8, 1.25));
            dc.DrawGeometry(PrismPanel.Brush("#090F21"), new Pen(PrismPanel.Brush("#090F21"), .9), TranslationGlyph);
            dc.Pop();
            dc.DrawGeometry(Theme.Dark ? Metal("#FFFFFF", "#EDF4FB", "#ABBBD5") : Metal("#34344E", "#373A72", "#18243F"),
                new Pen(Metal("#D5FFFF", "#FFFFFF", "#C9A6E9"), .45), TranslationGlyph);
        }
        dc.Pop();
    }
}
