using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DayPlanner.Core;
using DayPlanner.UI.Controls;
using static DayPlanner.UI.Views.PlannerShell;

namespace DayPlanner.UI.Views;

public sealed class MobilePalette : StackPanel
{
    private readonly PlannerSession session;
    private readonly UniformGrid swatches = new() { Columns = 6, Rows = 1, Height = 56 };
    private readonly Button done, previous, next;
    private readonly TextBlock error = new() { FontSize = 12, Foreground = Brush("#BE473C"), TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private int page;
    private bool editing;
    private IDisposable? hold;
    private Point? pressed;
    public string SelectedColor { get; private set; }
    public event Action<MobileColorPicker>? PickerRequested;
    public MobilePalette(PlannerSession session, string selected)
    {
        this.session = session; SelectedColor = selected; Spacing = 0;
        var header = new Grid { ColumnDefinitions = new("*,32,32,Auto"), Height = 40 };
        header.Children.Add(MobileUi.Caption("标记颜色"));
        previous = MobileUi.Flat("‹", () => { page--; RenderColors(); }); next = MobileUi.Flat("›", () => { page++; RenderColors(); });
        previous.FontSize = next.FontSize = 22; previous.Padding = next.Padding = new Thickness(0);
        AutomationProperties.SetName(previous, "上一组颜色"); AutomationProperties.SetName(next, "下一组颜色");
        Place(header, previous, 1); Place(header, next, 2);
        done = MobileUi.Flat("完成", () => { editing = false; RenderColors(); }); done.Foreground = MobileUi.Teal; Place(header, done, 3);
        Children.Add(header); Children.Add(swatches); Children.Add(error);
        page = Math.Max(0, ScheduleStore.AvailableColors(session.Data, selected).IndexOf(selected)) / 5; RenderColors();
        DetachedFromVisualTree += (_, _) => CancelHold();
    }
    public bool ExitEditing()
    {
        if (!editing) return false; editing = false; RenderColors(); return true;
    }
    private void CancelHold() { hold?.Dispose(); hold = null; pressed = null; }
    private void RenderColors()
    {
        CancelHold(); swatches.Children.Clear();
        var colors = ScheduleStore.AvailableColors(session.Data, SelectedColor);
        page = Math.Clamp(page, 0, Math.Max(0, (colors.Count - 1) / 5));
        done.IsVisible = editing; previous.IsVisible = next.IsVisible = colors.Count > 5;
        previous.IsEnabled = page > 0; next.IsEnabled = (page + 1) * 5 < colors.Count;
        foreach (var color in colors.Skip(page * 5).Take(5))
        {
            var cell = new Grid(); var suppressClick = false;
            var swatch = MobileUi.Flat(SelectedColor == color ? "✓" : "", () => {
                if (suppressClick || editing) return; SelectedColor = color; RenderColors();
            });
            swatch.Width = swatch.Height = 44; swatch.Padding = new Thickness(0); swatch.Background = Brush(color); swatch.Foreground = Brushes.White; swatch.FontSize = 22;
            AutomationProperties.SetName(swatch, color);
            swatch.AddHandler(PointerPressedEvent, (_, e) => {
                CancelHold(); if (!e.GetCurrentPoint(swatch).Properties.IsLeftButtonPressed) return;
                pressed = e.GetPosition(swatch);
                hold = DispatcherTimer.RunOnce(() => { suppressClick = true; editing = true; RenderColors(); }, TimeSpan.FromMilliseconds(500));
            }, RoutingStrategies.Tunnel);
            swatch.AddHandler(PointerMovedEvent, (_, e) => {
                if (pressed is Point origin) { var point = e.GetPosition(swatch); if (Math.Abs(point.X - origin.X) + Math.Abs(point.Y - origin.Y) > 10) CancelHold(); }
            }, RoutingStrategies.Tunnel);
            swatch.AddHandler(PointerReleasedEvent, (_, _) => CancelHold(), RoutingStrategies.Tunnel);
            swatch.PointerCaptureLost += (_, _) => CancelHold();
            cell.Children.Add(swatch);
            if (editing)
            {
                var minus = Label("−", 15, true); minus.Foreground = Brushes.White; minus.HorizontalAlignment = HorizontalAlignment.Center;
                var badge = new Border { Width = 18, Height = 18, Background = Brush("#DA5853"), BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(9), Child = minus };
                var remove = MobileUi.Flat("", () => Delete(color)); remove.Content = badge; remove.Width = 28; remove.Height = remove.MinHeight = 28;
                remove.Padding = new Thickness(0); remove.HorizontalAlignment = HorizontalAlignment.Right; remove.VerticalAlignment = VerticalAlignment.Top;
                AutomationProperties.SetName(remove, "删除颜色" + color); cell.Children.Add(remove);
            }
            swatches.Children.Add(cell);
        }
        while (swatches.Children.Count < 5) swatches.Children.Add(new Border());
        var add = MobileUi.Flat("+", OpenPicker); add.Width = add.Height = 44; add.Background = Brushes.White; add.FontSize = 24;
        AutomationProperties.SetName(add, "添加颜色"); swatches.Children.Add(add);
    }
    private void Delete(string color)
    {
        session.Data.CustomColors.RemoveAll(value => value == color);
        if (!session.Data.HiddenColors.Contains(color)) session.Data.HiddenColors.Add(color);
        Persist(); RenderColors();
    }
    private void Persist()
    {
        var saved = session.Save(); error.IsVisible = !saved; error.Text = saved ? "" : session.Status;
    }
    private void OpenPicker()
    {
        editing = false; RenderColors();
        var picker = new MobileColorPicker(SelectedColor);
        picker.Confirmed += color => {
            session.Data.HiddenColors.RemoveAll(value => value == color);
            if (!ScheduleStore.Palette.Contains(color) && !session.Data.CustomColors.Contains(color)) session.Data.CustomColors.Add(color);
            SelectedColor = color; Persist(); page = Math.Max(0, ScheduleStore.AvailableColors(session.Data, color).IndexOf(color)) / 5; RenderColors();
        };
        PickerRequested?.Invoke(picker);
    }
}

public sealed class MobileColorPicker : StackPanel
{
    public event Action<string>? Confirmed;
    public event Action? Closed;
    public void Cancel() => Closed?.Invoke();
    public MobileColorPicker(string initial)
    {
        Spacing = 12;
        var heading = new Grid { ColumnDefinitions = new("*,Auto") }; heading.Children.Add(Label("选择颜色", 19, true));
        Place(heading, MobileUi.Flat("取消", Cancel), 1); Children.Add(heading);
        var plane = new ColorPlane(); plane.SetColor(Color.Parse(initial));
        Children.Add(new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = plane });
        var hue = new Slider { Minimum = 0, Maximum = 359.9, Value = plane.Hue, Height = 44 };
        AutomationProperties.SetName(hue, "色相");
        var rainbow = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative) };
        for (var i = 0; i <= 6; i++) rainbow.GradientStops.Add(new GradientStop(ColorPlane.FromHsv(i * 60, 1, 1), i / 6.0));
        var hueRow = new Grid(); hueRow.Children.Add(new Border { Height = 12, Margin = new Thickness(8, 0), CornerRadius = new CornerRadius(6), Background = rainbow }); hueRow.Children.Add(hue); Children.Add(hueRow);
        var preview = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(10) };
        var code = Label("", 14); var previewRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        previewRow.Children.Add(preview); previewRow.Children.Add(code); Children.Add(previewRow);
        string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        void Update(Color color) { preview.Background = new SolidColorBrush(color); code.Text = Hex(color); }
        plane.Changed += Update; hue.ValueChanged += (_, _) => plane.SetHue(hue.Value); Update(plane.Color);
        var confirm = Button("使用此颜色", () => { Confirmed?.Invoke(Hex(plane.Color)); Closed?.Invoke(); }, true, true); confirm.CornerRadius = new CornerRadius(12); Children.Add(confirm);
    }
}
