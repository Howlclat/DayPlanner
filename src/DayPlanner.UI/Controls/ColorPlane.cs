using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace DayPlanner.UI.Controls;

/// <summary>Opaque HSV color selection with a touch-controlled saturation/value plane.</summary>
public sealed class ColorPlane : Control
{
    public double Hue { get; private set; }
    private double saturation = 1, value = 1;
    public Color Color => FromHsv(Hue, saturation, value);
    public event Action<Color>? Changed;
    public ColorPlane() { Height = 200; ClipToBounds = true; }
    public void SetColor(Color color)
    {
        var r = color.R / 255.0; var g = color.G / 255.0; var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b)); var delta = max - min;
        Hue = delta == 0 ? 0 : max == r ? 60 * (((g - b) / delta + 6) % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        saturation = max == 0 ? 0 : delta / max; value = max; InvalidateVisual();
    }
    public void SetHue(double hue) { Hue = hue; InvalidateVisual(); Changed?.Invoke(Color); }
    public override void Render(DrawingContext dc)
    {
        var rect = new Rect(Bounds.Size);
        dc.DrawRectangle(new SolidColorBrush(FromHsv(Hue, 1, 1)), null, rect);
        dc.DrawRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = new GradientStops { new(Colors.White, 0), new(Colors.Transparent, 1) } }, null, rect);
        dc.DrawRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops { new(Colors.Transparent, 0), new(Colors.Black, 1) } }, null, rect);
        var point = new Point(Math.Clamp(saturation * Bounds.Width, 7, Math.Max(7, Bounds.Width - 7)), Math.Clamp((1 - value) * Bounds.Height, 7, Math.Max(7, Bounds.Height - 7)));
        dc.DrawEllipse(null, new Pen(Brushes.Black, 4), point, 6, 6); dc.DrawEllipse(null, new Pen(Brushes.White, 2), point, 6, 6);
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(this); Pick(e.GetPosition(this)); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); if (e.Pointer.Captured == this) { Pick(e.GetPosition(this)); e.Handled = true; } }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); if (e.Pointer.Captured == this) { Pick(e.GetPosition(this)); e.Pointer.Capture(null); e.Handled = true; } }
    private void Pick(Point point)
    {
        saturation = Math.Clamp(point.X / Math.Max(1, Bounds.Width), 0, 1); value = 1 - Math.Clamp(point.Y / Math.Max(1, Bounds.Height), 0, 1);
        InvalidateVisual(); Changed?.Invoke(Color);
    }
    public static Color FromHsv(double hue, double saturation, double value)
    {
        var h = (hue % 360 + 360) % 360 / 60; var c = value * saturation; var x = c * (1 - Math.Abs(h % 2 - 1)); var m = value - c;
        var (r, g, b) = h switch { < 1 => (c, x, 0d), < 2 => (x, c, 0d), < 3 => (0d, c, x), < 4 => (0d, x, c), < 5 => (x, 0d, c), _ => (c, 0d, x) };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
