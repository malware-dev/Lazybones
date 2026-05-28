using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Lazybones.Features.Dashboard;

// Material-style clock-face time picker. Hour on the outer ring (0–23),
// minute on the inner ring (0–59), both visible at once and independently
// draggable; the ring you press locks the drag for the gesture's lifetime.
// The HH:MM at the centre is an embedded TextBox so the user can type a
// value directly; invalid input snaps to the nearest valid (clamped) or to
// 00:00 if completely unparseable.
public class ClockDial : Control
{
    public static readonly StyledProperty<int> HourProperty =
        AvaloniaProperty.Register<ClockDial, int>(nameof(Hour), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> MinuteProperty =
        AvaloniaProperty.Register<ClockDial, int>(nameof(Minute), defaultBindingMode: BindingMode.TwoWay);

    public int Hour
    {
        get => GetValue(HourProperty);
        set => SetValue(HourProperty, value);
    }

    public int Minute
    {
        get => GetValue(MinuteProperty);
        set => SetValue(MinuteProperty, value);
    }

    static ClockDial()
    {
        AffectsRender<ClockDial>(HourProperty, MinuteProperty);
        FocusableProperty.OverrideDefaultValue<ClockDial>(true);
    }

    private enum Ring { None, Hour, Minute }
    private Ring _drag = Ring.None;
    private Ring _active = Ring.Hour;

    private const double OuterThickness = 4;
    private const double InnerThickness = 4;
    private const double RingGap = 22;
    private const double EdgeMargin = 8;
    private const double HourIndicatorRadius = 7;
    private const double MinuteIndicatorRadius = 5;
    private const double CenterFontSize = 22;
    private const double TickFontSize = 9;

    private static readonly IBrush TrackBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
    private static readonly IBrush HourBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF));
    private static readonly IBrush MinuteBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D));
    private static readonly IBrush CenterTextBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
    private static readonly IBrush TickBrush = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93));

    private static readonly int[] HourLabels = [0, 3, 6, 9, 12, 15, 18, 21];
    private static readonly int[] MinuteLabels = [0, 15, 30, 45];

    private readonly TextBox _editor;

    public ClockDial()
    {
        _editor = new TextBox
        {
            FontSize = CenterFontSize,
            FontWeight = FontWeight.Bold,
            Foreground = CenterTextBrush,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            MaxLength = 5,
            FontFamily = new FontFamily("Consolas,Menlo,monospace"),
            CaretBrush = Brushes.White,
            Text = FormatTime(),
        };
        _editor.KeyDown += OnEditorKeyDown;
        _editor.LostFocus += (_, _) => CommitEditor();
        _editor.GotFocus += (_, _) => _editor.SelectAll();
        LogicalChildren.Add(_editor);
        VisualChildren.Add(_editor);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _editor.Measure(new Size(80, 32));
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var s = _editor.DesiredSize;
        var x = finalSize.Width / 2 - s.Width / 2;
        var y = finalSize.Height / 2 - s.Height / 2;
        _editor.Arrange(new Rect(x, y, s.Width, s.Height));
        return finalSize;
    }

    private (double outerR, double innerR, Point center) Geometry()
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var outerR = size / 2 - EdgeMargin - OuterThickness / 2;
        var innerR = outerR - OuterThickness / 2 - RingGap - InnerThickness / 2;
        return (outerR, innerR, center);
    }

    private static double AngleFor(int value, int range) =>
        -Math.PI / 2 + 2 * Math.PI * (value / (double)range);

    public override void Render(DrawingContext context)
    {
        var (outerR, innerR, center) = Geometry();
        if (outerR <= 0 || innerR <= 0) return;

        context.DrawEllipse(null, new Pen(TrackBrush, OuterThickness), center, outerR, outerR);
        context.DrawEllipse(null, new Pen(TrackBrush, InnerThickness), center, innerR, innerR);

        var hourLabelR = outerR - 11;
        foreach (var h in HourLabels)
            DrawTick(context, center, hourLabelR, AngleFor(h, 24), h.ToString("00"));

        var minuteLabelR = innerR - 11;
        foreach (var m in MinuteLabels)
            DrawTick(context, center, minuteLabelR, AngleFor(m, 60), m.ToString("00"));

        var hourAngle = AngleFor(Hour, 24);
        var hourPos = new Point(center.X + outerR * Math.Cos(hourAngle), center.Y + outerR * Math.Sin(hourAngle));
        context.DrawEllipse(HourBrush, null, hourPos, HourIndicatorRadius, HourIndicatorRadius);

        var minuteAngle = AngleFor(Minute, 60);
        var minutePos = new Point(center.X + innerR * Math.Cos(minuteAngle), center.Y + innerR * Math.Sin(minuteAngle));
        context.DrawEllipse(MinuteBrush, null, minutePos, MinuteIndicatorRadius, MinuteIndicatorRadius);
    }

    private static void DrawTick(DrawingContext context, Point center, double radius, double angle, string label)
    {
        var pos = new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
        var ft = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), TickFontSize, TickBrush);
        context.DrawText(ft, new Point(pos.X - ft.Width / 2, pos.Y - ft.Height / 2));
    }

    private Ring DetectRing(Point p, double outerR, double innerR, Point center)
    {
        var dx = p.X - center.X;
        var dy = p.Y - center.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        var boundary = (outerR + innerR) / 2;
        return dist >= boundary ? Ring.Hour : Ring.Minute;
    }

    private static int AngleToValue(Point p, Point center, int range)
    {
        var dx = p.X - center.X;
        var dy = p.Y - center.Y;
        if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01) return 0;
        var angle = Math.Atan2(dy, dx) + Math.PI / 2;
        if (angle < 0) angle += 2 * Math.PI;
        var v = (int)Math.Round(angle / (2 * Math.PI) * range);
        return v >= range ? 0 : v;
    }

    // Clicks that originate inside the embedded TextBox must not drive a
    // ring-drag — the user's setting the caret, not turning a dial.
    private bool IsEventFromEditor(RoutedEventArgs e)
    {
        return e.Source is Visual v && (ReferenceEquals(v, _editor) || _editor.IsVisualAncestorOf(v));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsEventFromEditor(e)) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var (outerR, innerR, center) = Geometry();
        var pos = e.GetPosition(this);
        _drag = DetectRing(pos, outerR, innerR, center);
        _active = _drag;
        e.Pointer.Capture(this);
        Focus();
        ApplyDrag(pos, center);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag == Ring.None) return;
        var (_, _, center) = Geometry();
        ApplyDrag(e.GetPosition(this), center);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag == Ring.None) return;
        _drag = Ring.None;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (IsEventFromEditor(e)) return;
        var (outerR, innerR, center) = Geometry();
        var ring = DetectRing(e.GetPosition(this), outerR, innerR, center);
        Nudge(ring, e.Delta.Y >= 0 ? 1 : -1);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Don't fight the editor: when it has focus, all keys belong to it.
        if (_editor.IsFocused) return;
        switch (e.Key)
        {
            case Key.Up:
            case Key.Right:
                Nudge(_active, 1);
                e.Handled = true;
                break;
            case Key.Down:
            case Key.Left:
                Nudge(_active, -1);
                e.Handled = true;
                break;
        }
    }

    private void ApplyDrag(Point pos, Point center)
    {
        if (_drag == Ring.Hour) Hour = AngleToValue(pos, center, 24);
        else if (_drag == Ring.Minute) Minute = AngleToValue(pos, center, 60);
    }

    private void Nudge(Ring ring, int delta)
    {
        if (ring == Ring.Hour) Hour = ((Hour + delta) % 24 + 24) % 24;
        else if (ring == Ring.Minute) Minute = ((Minute + delta) % 60 + 60) % 60;
    }

    private string FormatTime() => $"{Hour:00}:{Minute:00}";

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                CommitEditor();
                Focus();
                e.Handled = true;
                break;
            case Key.Escape:
                _editor.Text = FormatTime();
                Focus();
                e.Handled = true;
                break;
        }
    }

    private void CommitEditor()
    {
        var (h, m) = ParseClamped(_editor.Text);
        Hour = h;
        Minute = m;
        _editor.Text = FormatTime();
    }

    // Lenient parse. With a colon: "H:M" / "HH:MM" / etc. Without a colon,
    // digit-only input is interpreted by length so the natural shortcut
    // "0630" → 06:30 works: 1–2 digits = hours; 3 = H + MM; 4+ = HH + MM.
    // Each component is clamped to its valid range; completely unparseable
    // input falls back to 00:00.
    private static (int h, int m) ParseClamped(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return (0, 0);
        var t = s.Trim();

        int h = 0, m = 0;
        if (t.Contains(':'))
        {
            var parts = t.Split(':');
            if (parts.Length > 0) int.TryParse(parts[0], out h);
            if (parts.Length > 1) int.TryParse(parts[1], out m);
        }
        else if (int.TryParse(t, out var n) && n >= 0)
        {
            if (t.Length <= 2) { h = n; m = 0; }
            else if (t.Length == 3) { h = n / 100; m = n % 100; }
            else { h = (n / 100) % 100; m = n % 100; }
        }

        return (Math.Clamp(h, 0, 23), Math.Clamp(m, 0, 59));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == HourProperty || change.Property == MinuteProperty) && !_editor.IsFocused)
            _editor.Text = FormatTime();
    }
}
