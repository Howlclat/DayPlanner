using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;

namespace DayPlanner;

public partial class EventEditor : Window
{
    private readonly ScheduleItem source;
    public ScheduleItem? Result { get; private set; }
    private readonly List<string> colors = [];
    private string selectedColor = "#009DA4";
    private bool syncingColor;
    public event Action<string>? ColorAdded;
    public EventEditor(ScheduleItem item, bool isNew, IEnumerable<string>? customColors = null, DateTime? date = null)
    {
        InitializeComponent();
        source = item;
        MaxHeight = SystemParameters.WorkArea.Height;
        ReminderOption.IsChecked = item.ReminderEnabled;
        ReminderLead.Text = item.ReminderMinutes.ToString();
        EditorDate.Text = (date ?? DateTime.Today).ToString("yyyy年M月d日", System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));
        Title = Heading.Text = isNew ? item.IsPoint ? "新建事件" : "新建日程" : "编辑安排";
        SaveButton.Content = isNew ? "创建" : "保存";
        TitleInput.Text = item.Title;
        StartInput.Text = TimeMath.Format(item.Start);
        EndInput.Text = TimeMath.Format(item.End ?? Math.Min(1440, item.Start + 30));
        selectedColor = ScheduleStore.NormalizeColor(item.Color) ?? "#009DA4";
        colors.AddRange(ScheduleStore.Palette.Concat(customColors ?? []).Append(selectedColor).Select(ScheduleStore.NormalizeColor).OfType<string>().Distinct());
        RenderColors();
        PointOption.IsChecked = item.IsPoint;
        SpanOption.IsChecked = !item.IsPoint;
        Loaded += (_, _) => { TitleInput.Focus(); TitleInput.SelectAll(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Save_Click(this, new RoutedEventArgs()); e.Handled = true; } };
    }
    private void RenderColors()
    {
        ColorSwatches.Children.Clear();
        foreach (var color in colors)
        {
            var brush = TimelineControl.BrushOf(color);
            var rgb = brush.Color;
            var swatch = new RadioButton
            {
                Style = (Style)ColorSwatches.FindResource("ColorSwatch"), GroupName = "MarkerColor", Tag = color,
                Background = brush, Foreground = (rgb.R * 0.299 + rgb.G * 0.587 + rgb.B * 0.114) > 160 ? Brushes.Black : Brushes.White,
                IsChecked = color == selectedColor
            };
            AutomationProperties.SetName(swatch, color);
            swatch.Checked += (_, _) => selectedColor = color;
            ColorSwatches.Children.Add(swatch);
        }
        var add = new Button { Content = "+", Width = 42, Height = 42, FontSize = 23, Padding = new Thickness(0), Margin = new Thickness(0, 0, 9, 8) };
        AutomationProperties.SetName(add, "添加颜色");
        add.Click += (_, _) =>
        {
            CustomColorPanel.Visibility = CustomColorPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            if (CustomColorPanel.Visibility == Visibility.Visible) { HexInput.Text = selectedColor; HexInput.Focus(); HexInput.SelectAll(); }
        };
        ColorSwatches.Children.Add(add);
    }
    private void HexChanged(object sender, TextChangedEventArgs e)
    {
        if (syncingColor || RedSlider is null || BlueSlider is null) return;
        var color = ScheduleStore.NormalizeColor(HexInput.Text);
        ColorError.Text = color is null ? "请输入颜色代码，例如#4A90E2。" : "";
        if (color is null) return;
        syncingColor = true;
        var brush = TimelineControl.BrushOf(color);
        ColorPreview.Background = brush;
        RedSlider.Value = brush.Color.R; GreenSlider.Value = brush.Color.G; BlueSlider.Value = brush.Color.B;
        syncingColor = false;
    }
    private void RgbChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (syncingColor || BlueSlider is null || HexInput is null) return;
        HexInput.Text = $"#{(int)RedSlider.Value:X2}{(int)GreenSlider.Value:X2}{(int)BlueSlider.Value:X2}";
    }
    private void AddColor_Click(object sender, RoutedEventArgs e)
    {
        var color = ScheduleStore.NormalizeColor(HexInput.Text);
        if (color is null) { ColorError.Text = "请输入有效颜色代码，例如#4A90E2。"; HexInput.Focus(); return; }
        selectedColor = color;
        if (!colors.Contains(color)) { colors.Add(color); ColorAdded?.Invoke(color); }
        RenderColors();
        CustomColorPanel.Visibility = Visibility.Collapsed;
    }
    private void KindChanged(object sender, RoutedEventArgs e)
    {
        if (EndPanel == null) return;
        EndPanel.Visibility = PointOption.IsChecked == true ? Visibility.Hidden : Visibility.Visible;
        StartLabel.Text = PointOption.IsChecked == true ? "事件时间" : "开始时间";
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleInput.Text)) { ErrorText.Text = "请填写日程内容。"; TitleInput.Focus(); return; }
        if (!TimeMath.TryParse(StartInput.Text, out int start) || start >= 1440) { ErrorText.Text = "开始时间请填写00:00至23:59。"; StartInput.Focus(); return; }
        int? end = null;
        if (SpanOption.IsChecked == true)
        {
            if (!TimeMath.TryParse(EndInput.Text, out int value) || value <= start) { ErrorText.Text = "结束时间应晚于开始时间，最晚为24:00。"; EndInput.Focus(); return; }
            end = value;
        }
        var reminderEnabled = ReminderOption.IsChecked == true;
        var reminderMinutes = source.ReminderMinutes;
        if (reminderEnabled && (!int.TryParse(ReminderLead.Text, out reminderMinutes) || reminderMinutes is < 1 or > 1440))
        { ErrorText.Text = "提前时间请填写1至1440分钟。"; ReminderLead.Focus(); return; }
        Result = source with { Title = TitleInput.Text.Trim(), Start = start, End = end, Color = selectedColor,
            ReminderEnabled = reminderEnabled, ReminderMinutes = reminderMinutes };
        DialogResult = true;
    }
}
