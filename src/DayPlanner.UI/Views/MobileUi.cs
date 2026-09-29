using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using static DayPlanner.UI.Views.PlannerShell;

namespace DayPlanner.UI.Views;

internal static class MobileUi
{
    public static readonly IBrush Canvas = Brush("#F5F6F8"), Muted = Brush("#788393"), Teal = Brush("#008D94");
    public static Button Flat(string text, Action action)
    {
        var button = Button(text, action, touch: true);
        button.Classes.Add("mobileFlat");
        button.MinHeight = 44; button.Padding = new Thickness(8, 0);
        button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(0);
        button.CornerRadius = new CornerRadius(10); return button;
    }
    public static Button Icon(string name, string kind, Action action)
    {
        var button = Flat("", action); button.Width = 44;
        button.Content = Glyph(kind); AutomationProperties.SetName(button, name); return button;
    }
    public static Control Glyph(string kind, string color = "#34465D") => new Avalonia.Controls.Shapes.Path
    {
        Data = Geometry.Parse(kind switch
        {
            "calendar" => "M5,5 L19,5 Q21,5 21,7 L21,20 Q21,22 19,22 L5,22 Q3,22 3,20 L3,7 Q3,5 5,5 M3,10 L21,10 M8,2 L8,7 M16,2 L16,7 M7,14 L10,14 M14,14 L17,14 M7,18 L10,18",
            "settings" => "M9.59,4.83 Q9.91,4.69 10.01,4.18 L10.33,2.64 Q10.44,2.12 11.06,2.12 L12.94,2.12 Q13.56,2.12 13.67,2.64 L13.99,4.18 Q14.09,4.69 14.41,4.83 L15.37,5.22 Q15.68,5.35 16.12,5.06 L17.44,4.20 Q17.88,3.91 18.32,4.35 L19.65,5.68 Q20.09,6.12 19.80,6.56 L18.94,7.88 Q18.65,8.32 18.78,8.63 L19.17,9.59 Q19.31,9.91 19.82,10.01 L21.36,10.33 Q21.88,10.44 21.88,11.06 L21.88,12.94 Q21.88,13.56 21.36,13.67 L19.82,13.99 Q19.31,14.09 19.17,14.41 L18.78,15.37 Q18.65,15.68 18.94,16.12 L19.80,17.44 Q20.09,17.88 19.65,18.32 L18.32,19.65 Q17.88,20.09 17.44,19.80 L16.12,18.94 Q15.68,18.65 15.37,18.78 L14.41,19.17 Q14.09,19.31 13.99,19.82 L13.67,21.36 Q13.56,21.88 12.94,21.88 L11.06,21.88 Q10.44,21.88 10.33,21.36 L10.01,19.82 Q9.91,19.31 9.59,19.17 L8.63,18.78 Q8.32,18.65 7.88,18.94 L6.56,19.80 Q6.12,20.09 5.68,19.65 L4.35,18.32 Q3.91,17.88 4.20,17.44 L5.06,16.12 Q5.35,15.68 5.22,15.37 L4.83,14.41 Q4.69,14.09 4.18,13.99 L2.64,13.67 Q2.12,13.56 2.12,12.94 L2.12,11.06 Q2.12,10.44 2.64,10.33 L4.18,10.01 Q4.69,9.91 4.83,9.59 L5.22,8.63 Q5.35,8.32 5.06,7.88 L4.20,6.56 Q3.91,6.12 4.35,5.68 L5.68,4.35 Q6.12,3.91 6.56,4.20 L7.88,5.06 Q8.32,5.35 8.63,5.22 L9.59,4.83 Z M15.3,12 A3.3,3.3 0 1 1 8.7,12 A3.3,3.3 0 1 1 15.3,12 Z",
            "undo" => "M9,4 L3,10 L9,16 M3,10 L15,10 C24,10 24,22 15,22 L11,22",
            "redo" => "M15,4 L21,10 L15,16 M21,10 L9,10 C0,10 0,22 9,22 L13,22",
            "back" => "M15,4 L7,12 L15,20",
            "next" => "M9,4 L17,12 L9,20",
            "bell" => "M6,10 C6,2 18,2 18,10 L18,16 L21,19 L3,19 L6,16 Z M9,21 Q12,25 15,21",
            _ => "M12,3 L12,21 M3,12 L21,12"
        }),
        Stroke = Brush(color), StrokeThickness = 1.65, Width = 21, Height = 21, Stretch = Stretch.Uniform,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
    };
    public static Border Group(Control content, double padding = 14) => new()
    {
        Background = Brushes.White, BorderBrush = Brush("#E9EDF2"), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(16), Padding = new Thickness(padding), Child = content
    };
    public static Border Separator() => new() { Background = Brush("#EDF0F4"), Height = 1 };
    public static TextBlock Caption(string text) { var label = Label(text, 12); label.Foreground = Muted; return label; }
    public static void Input(TextBox input)
    {
        input.MinHeight = 44; input.FontSize = 15; input.BorderThickness = new Thickness(0);
        input.Background = Brushes.Transparent; input.CornerRadius = new CornerRadius(8);
        input.Padding = new Thickness(8, 6);
    }
}
