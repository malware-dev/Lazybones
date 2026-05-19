using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lazybones.Views.Controls;

public class ProgressRings : Control
{
    public static readonly StyledProperty<double> OuterValueProperty =
        AvaloniaProperty.Register<ProgressRings, double>(nameof(OuterValue));

    public static readonly StyledProperty<double> InnerValueProperty =
        AvaloniaProperty.Register<ProgressRings, double>(nameof(InnerValue));

    public static readonly StyledProperty<bool> IsStandingProperty =
        AvaloniaProperty.Register<ProgressRings, bool>(nameof(IsStanding));

    public double OuterValue
    {
        get => GetValue(OuterValueProperty);
        set => SetValue(OuterValueProperty, value);
    }

    public double InnerValue
    {
        get => GetValue(InnerValueProperty);
        set => SetValue(InnerValueProperty, value);
    }

    public bool IsStanding
    {
        get => GetValue(IsStandingProperty);
        set => SetValue(IsStandingProperty, value);
    }

    static ProgressRings()
    {
        AffectsRender<ProgressRings>(OuterValueProperty, InnerValueProperty, IsStandingProperty);
    }

    private const double OuterThickness = 3;
    private const double InnerThickness = 6;
    private const double RingGap = 3;
    private const double EdgeMargin = 4;

    private static readonly IBrush TrackBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
    private static readonly IBrush OuterFillBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF));
    private static readonly IBrush InnerStandingBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0x6A));
    private static readonly IBrush InnerRestingBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D));

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var outerRadius = size / 2 - EdgeMargin - OuterThickness / 2;
        var innerRadius = outerRadius - OuterThickness / 2 - RingGap - InnerThickness / 2;

        DrawRing(context, center, outerRadius, OuterThickness, TrackBrush, OuterFillBrush, Clamp01(OuterValue));

        var innerFill = IsStanding ? InnerStandingBrush : InnerRestingBrush;
        DrawRing(context, center, innerRadius, InnerThickness, TrackBrush, innerFill, Clamp01(InnerValue));
    }

    private static void DrawRing(DrawingContext context, Point center, double radius, double thickness,
        IBrush trackBrush, IBrush fillBrush, double value)
    {
        if (radius <= 0) return;

        var trackPen = new Pen(trackBrush, thickness);
        context.DrawEllipse(null, trackPen, center, radius, radius);

        if (value <= 0) return;

        var fillPen = new Pen(fillBrush, thickness, lineCap: PenLineCap.Round);

        if (value >= 1.0 - 1e-6)
        {
            context.DrawEllipse(null, new Pen(fillBrush, thickness), center, radius, radius);
            return;
        }

        const double startAngle = -Math.PI / 2;
        var sweep = 2 * Math.PI * value;
        var endAngle = startAngle + sweep;
        var start = new Point(center.X + radius * Math.Cos(startAngle), center.Y + radius * Math.Sin(startAngle));
        var end = new Point(center.X + radius * Math.Cos(endAngle), center.Y + radius * Math.Sin(endAngle));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false);
            ctx.ArcTo(end, new Size(radius, radius), 0, value > 0.5, SweepDirection.Clockwise);
        }

        context.DrawGeometry(null, fillPen, geometry);
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
