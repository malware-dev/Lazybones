using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lazybones.Views.Controls;

public class CyclesPerDayBars : Control
{
    public static readonly StyledProperty<IReadOnlyList<int>?> ValuesProperty =
        AvaloniaProperty.Register<CyclesPerDayBars, IReadOnlyList<int>?>(nameof(Values));

    public IReadOnlyList<int>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    static CyclesPerDayBars()
    {
        AffectsRender<CyclesPerDayBars>(ValuesProperty);
    }

    private const double Gap = 2;

    public override void Render(DrawingContext context)
    {
        var values = Values;
        if (values == null || values.Count == 0) return;

        var count = values.Count;
        var w = Bounds.Width;
        var h = Bounds.Height;
        var barWidth = (w - (count - 1) * Gap) / count;
        if (barWidth <= 0) return;

        // Peak-relative scaling so the chart reads as "shape of recent
        // activity" — the tallest day in the window fills the full height.
        var peak = Math.Max(1, values.Max());

        var barBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF));
        var zeroBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));

        for (var i = 0; i < count; i++)
        {
            var v = values[i];
            var x = i * (barWidth + Gap);

            if (v > 0)
            {
                var bh = h * (v / (double)peak);
                context.FillRectangle(barBrush, new Rect(x, h - bh, barWidth, bh), 2);
            }
            else
            {
                // Thin baseline tick so empty days are still visible as cells.
                context.FillRectangle(zeroBrush, new Rect(x, h - 2, barWidth, 2), 1);
            }
        }
    }
}
