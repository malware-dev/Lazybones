using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lazybones.Views.Controls;

public class Heatmap : Control
{
    public static readonly StyledProperty<IReadOnlyDictionary<DateOnly, int>?> MinutesByDayProperty =
        AvaloniaProperty.Register<Heatmap, IReadOnlyDictionary<DateOnly, int>?>(nameof(MinutesByDay));

    public static readonly StyledProperty<int> DailyGoalMinutesProperty =
        AvaloniaProperty.Register<Heatmap, int>(nameof(DailyGoalMinutes), defaultValue: 60);

    public IReadOnlyDictionary<DateOnly, int>? MinutesByDay
    {
        get => GetValue(MinutesByDayProperty);
        set => SetValue(MinutesByDayProperty, value);
    }

    public int DailyGoalMinutes
    {
        get => GetValue(DailyGoalMinutesProperty);
        set => SetValue(DailyGoalMinutesProperty, value);
    }

    static Heatmap()
    {
        AffectsRender<Heatmap>(MinutesByDayProperty, DailyGoalMinutesProperty);
    }

    private const int Weeks = 13;
    private const int Days = 7;
    private const double Cell = 12;
    private const double Gap = 1;

    public override void Render(DrawingContext context)
    {
        var data = MinutesByDay;
        var goal = Math.Max(1, DailyGoalMinutes);

        var totalWidth = Weeks * Cell + (Weeks - 1) * Gap;
        var totalHeight = Days * Cell + (Days - 1) * Gap;
        var offsetX = (Bounds.Width - totalWidth) / 2;
        var offsetY = (Bounds.Height - totalHeight) / 2;

        var today = DateOnly.FromDateTime(DateTime.Now);
        // Anchor the most recent column on today; rows are weekdays (Mon..Sun).
        // Find the most recent Monday on or before today so columns align cleanly.
        var dow = ((int)today.DayOfWeek + 6) % 7; // 0 = Monday
        var lastMonday = today.AddDays(-dow);
        var firstMonday = lastMonday.AddDays(-(Weeks - 1) * 7);

        var emptyBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
        var fullR = 0x4C; var fullG = 0xC2; var fullB = 0xFF; // bright blue
        var emptyR = 0x2A; var emptyG = 0x2A; var emptyB = 0x2A;

        for (var w = 0; w < Weeks; w++)
        {
            for (var d = 0; d < Days; d++)
            {
                var day = firstMonday.AddDays(w * 7 + d);
                if (day > today) continue;

                var minutes = data != null && data.TryGetValue(day, out var m) ? m : 0;
                var intensity = Math.Clamp((double)minutes / goal, 0, 1);

                IBrush brush;
                if (intensity <= 0)
                {
                    brush = emptyBrush;
                }
                else
                {
                    var r = (byte)(emptyR + (fullR - emptyR) * intensity);
                    var g = (byte)(emptyG + (fullG - emptyG) * intensity);
                    var b = (byte)(emptyB + (fullB - emptyB) * intensity);
                    brush = new SolidColorBrush(Color.FromRgb(r, g, b));
                }

                var rect = new Rect(
                    offsetX + w * (Cell + Gap),
                    offsetY + d * (Cell + Gap),
                    Cell, Cell);

                context.FillRectangle(brush, rect, 2);
            }
        }
    }
}
