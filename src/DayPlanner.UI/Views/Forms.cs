using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DayPlanner.Core;
using static DayPlanner.UI.Views.PlannerShell;

namespace DayPlanner.UI.Views;

public sealed class EventForm : Grid
{
    public event Action? Completed;
    public EventForm(ScheduleItem source, bool isNew, PlannerSession session, bool mobile)
    {
        var fields = new StackPanel { Spacing = 10 };
        Children.Add(fields);
        var heading = Label(isNew ? "新建安排" : "编辑安排", 22, true);
        var dateLabel = Label(session.Date.ToString("yyyy年M月d日"), 12); dateLabel.Foreground = Brush("#8491A2");
        if (mobile)
        {
            RowDefinitions = new("Auto,*,Auto"); Grid.SetRow(fields, 1);
            var header = new Grid { ColumnDefinitions = new("44,12,*"), Margin = new Thickness(0, 0, 0, 16) };
            var back = Button("‹", () => Completed?.Invoke(), touch: true); back.Width = 44; back.FontSize = 26; back.Padding = new Thickness(0);
            Avalonia.Automation.AutomationProperties.SetName(back, "返回"); header.Children.Add(back);
            var captions = new StackPanel { Spacing = 4 }; captions.Children.Add(heading); captions.Children.Add(dateLabel);
            Place(header, captions, 2); Children.Add(header);
        }
        else { fields.Children.Add(heading); fields.Children.Add(dateLabel); }
        fields.Children.Add(Label("内容"));
        var title = new TextBox { Text = source.Title, PlaceholderText = "想做些什么？", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = mobile ? 64 : 72, MaxLength = 120, VerticalContentAlignment = VerticalAlignment.Top };
        fields.Children.Add(title);
        var types = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
        var point = new RadioButton { Content = "时间点", GroupName = "Type", IsChecked = source.IsPoint, MinHeight = mobile ? 44 : 36, VerticalContentAlignment = VerticalAlignment.Center };
        var span = new RadioButton { Content = "时间段", GroupName = "Type", IsChecked = !source.IsPoint, MinHeight = mobile ? 44 : 36, VerticalContentAlignment = VerticalAlignment.Center };
        types.Children.Add(point); types.Children.Add(span); fields.Children.Add(types);
        var start = new TextBox { Text = TimeMath.Format(source.Start), PlaceholderText = "09:00" };
        var end = new TextBox { Text = TimeMath.Format(source.End ?? Math.Min(1440, source.Start + 60)), PlaceholderText = "10:00" };
        var startField = new StackPanel { Spacing = 6 }; startField.Children.Add(Label("开始时间")); startField.Children.Add(start);
        var endField = new StackPanel { Spacing = 6 }; endField.Children.Add(Label("结束时间")); endField.Children.Add(end);
        var times = new Grid { ColumnDefinitions = new("*,12,*") }; times.Children.Add(startField); Place(times, endField, 2); fields.Children.Add(times);
        void Kind() { endField.IsVisible = span.IsChecked == true; }
        point.IsCheckedChanged += (_, _) => Kind(); span.IsCheckedChanged += (_, _) => Kind(); Kind();
        var timeHint = Label("24小时制，结束时间支持24:00", 11); timeHint.Foreground = Brush("#8491A2"); fields.Children.Add(timeHint);
        var reminder = new CheckBox { Content = "提前提醒", IsChecked = source.ReminderEnabled, MinHeight = mobile ? 44 : 40, VerticalContentAlignment = VerticalAlignment.Center };
        var lead = new TextBox { Text = source.ReminderMinutes.ToString(), PlaceholderText = "提前分钟数", IsEnabled = source.ReminderEnabled };
        reminder.IsCheckedChanged += (_, _) => lead.IsEnabled = reminder.IsChecked == true;
        lead.Width = 76;
        var reminderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        reminderRow.Children.Add(reminder); reminderRow.Children.Add(lead); reminderRow.Children.Add(Label("分钟")); fields.Children.Add(reminderRow);
        var colorHeading = new Grid { ColumnDefinitions = new("*,Auto,Auto") }; colorHeading.Children.Add(Label("标记颜色")); fields.Children.Add(colorHeading);
        var selectedColor = source.Color;
        var colors = ScheduleStore.AvailableColors(session.Data, source.Color);
        Panel swatches = mobile ? new Avalonia.Controls.Primitives.UniformGrid { Rows = 1, Columns = 6, Height = 48 } : new WrapPanel { Orientation = Orientation.Horizontal };
        var colorPage = 0;
        Button? previousColors = null, nextColors = null;
        void Swatches()
        {
            swatches.Children.Clear();
            if (previousColors != null && nextColors != null)
            {
                previousColors.IsVisible = nextColors.IsVisible = colors.Count > 6;
                previousColors.IsEnabled = colorPage > 0; nextColors.IsEnabled = (colorPage + 1) * 6 < colors.Count;
            }
            foreach (var color in mobile ? colors.Skip(colorPage * 6).Take(6) : colors)
            {
                var swatch = Button(selectedColor == color ? "✓" : "", () => { selectedColor = color; Swatches(); }, touch: true);
                swatch.Width = 44; swatch.Height = 44; swatch.MinHeight = 44; swatch.Padding = new Thickness(0); swatch.Margin = mobile ? new Thickness(0) : new Thickness(0, 0, 8, 8);
                swatch.Background = Brush(color); swatch.Foreground = Brushes.White; swatch.FontSize = 20;
                Avalonia.Automation.AutomationProperties.SetName(swatch, color);
                swatches.Children.Add(swatch);
            }
        }
        if (mobile)
        {
            previousColors = Button("‹", () => { colorPage--; Swatches(); }, touch: true);
            nextColors = Button("›", () => { colorPage++; Swatches(); }, touch: true);
            foreach (var button in new[] { previousColors, nextColors }) { button.Width = 44; button.MinHeight = 44; button.Padding = new Thickness(0); }
            Avalonia.Automation.AutomationProperties.SetName(previousColors, "上一组颜色");
            Avalonia.Automation.AutomationProperties.SetName(nextColors, "下一组颜色");
            Place(colorHeading, previousColors, 1); Place(colorHeading, nextColors, 2);
            colorPage = Math.Max(0, colors.IndexOf(selectedColor)) / 6;
        }
        Swatches(); fields.Children.Add(swatches);
        var custom = new TextBox { PlaceholderText = mobile ? "自定义颜色，如#4A90E2" : "自定义颜色，例如#4A90E2" };
        var error = new TextBlock { Foreground = Brush("#BE473C"), TextWrapping = TextWrapping.Wrap, FontSize = 12, MinHeight = mobile ? 32 : 0 };
        var customRow = new Grid { ColumnDefinitions = new("*,8,Auto") }; customRow.Children.Add(custom);
        Place(customRow, Button("添加", () => {
            var value = ScheduleStore.NormalizeColor(custom.Text);
            if (value == null) { error.Text = "请输入有效颜色代码。"; return; }
            error.Text = ""; if (!colors.Contains(value)) colors.Add(value); selectedColor = value;
            session.Data.HiddenColors.Remove(value);
            if (mobile) colorPage = colors.IndexOf(value) / 6;
            Swatches();
        }, touch: mobile), 2); fields.Children.Add(customRow); fields.Children.Add(error);
        var save = Button(isNew ? "创建安排" : "保存", () => {
            if (string.IsNullOrWhiteSpace(title.Text)) { error.Text = "请填写日程内容。"; return; }
            if (!TimeMath.TryParse(start.Text ?? "", out var startMinute) || startMinute >= 1440) { error.Text = "开始时间请填写00:00至23:59。"; return; }
            int? endMinute = null;
            if (span.IsChecked == true) {
                if (!TimeMath.TryParse(end.Text ?? "", out var value) || value <= startMinute) { error.Text = "结束时间应晚于开始时间，最晚为24:00。"; return; }
                endMinute = value;
            }
            if (!int.TryParse(lead.Text, out var minutes) || minutes is < 1 or > 1440) { if (reminder.IsChecked == true) { error.Text = "提前时间请填写1至1440分钟。"; return; } minutes = 5; }
            if (!ScheduleStore.Palette.Contains(selectedColor) && !session.Data.CustomColors.Contains(selectedColor)) session.Data.CustomColors.Add(selectedColor);
            session.Put(source with { Title = title.Text.Trim(), Start = startMinute, End = endMinute, Color = selectedColor, ReminderEnabled = reminder.IsChecked == true, ReminderMinutes = minutes });
            Completed?.Invoke();
            if (App.Reminders?.Status is string notificationStatus) App.Shell?.ShowMessage("提醒设置", notificationStatus);
        }, true, mobile);
        var cancel = Button("取消", () => Completed?.Invoke(), touch: mobile);
        if (mobile)
        {
            var actions = new Grid { ColumnDefinitions = new(isNew ? "*" : "Auto,12,*"), Margin = new Thickness(0, 12, 0, 0) };
            if (!isNew)
            {
                var delete = Button("删除", () => { session.Delete(source); Completed?.Invoke(); }, touch: true); delete.Foreground = Brush("#BE473C");
                actions.Children.Add(delete); Place(actions, save, 2);
            }
            else actions.Children.Add(save);
            Grid.SetRow(actions, 2); Children.Add(actions);
            foreach (var input in new[] { start, end, lead, custom }) { input.MinHeight = 44; input.FontSize = 14; }
        }
        else
        {
            var actions = new Grid { ColumnDefinitions = new("Auto,*,Auto,10,Auto"), Margin = new Thickness(0, 8, 0, 0) };
            if (!isNew) actions.Children.Add(Button("删除安排", () => { session.Delete(source); Completed?.Invoke(); }));
            cancel.MinWidth = 80; save.MinWidth = 100; Place(actions, cancel, 2); Place(actions, save, 4); fields.Children.Add(actions);
        }
    }
}

public sealed class SettingsForm : StackPanel
{
    public event Action? Cancelled;
    public SettingsForm(PlannerSession session, bool mobile, Action saved)
    {
        Spacing = 12; Children.Add(Label("设置", 24, true));
        Children.Add(Label("默认显示时段"));
        var start = new TextBox { Text = TimeMath.Format(session.Data.DefaultViewStart) };
        var end = new TextBox { Text = TimeMath.Format(session.Data.DefaultViewEnd) };
        if (mobile) { Children.Add(start); Children.Add(end); }
        else
        {
            var range = new Grid { ColumnDefinitions = new("*,36,*") }; range.Children.Add(start);
            var separator = Label("—"); separator.HorizontalAlignment = HorizontalAlignment.Center; Place(range, separator, 1); Place(range, end, 2); Children.Add(range);
        }
        var rangeHint = Label("结束时间支持24:00，至少保留2小时", 12); rangeHint.Foreground = Brush("#8491A2"); Children.Add(rangeHint);
        Children.Add(Label("网格吸附"));
        var five = new RadioButton { Content = "5分钟", GroupName = "Grid", IsChecked = session.Data.GridMinutes == 5, MinHeight = 44 };
        var ten = new RadioButton { Content = "10分钟", GroupName = "Grid", IsChecked = session.Data.GridMinutes == 10, MinHeight = 44 };
        var grids = new StackPanel { Orientation = mobile ? Orientation.Vertical : Orientation.Horizontal, Spacing = mobile ? 0 : 24 };
        grids.Children.Add(five); grids.Children.Add(ten); Children.Add(grids);
        var tray = new CheckBox { Content = "关闭时最小化到托盘", IsChecked = session.Data.CloseToTray, IsVisible = !mobile, MinHeight = 44 };
        Children.Add(tray);
        Children.Add(new TextBlock { Text = "数据保存在本机，自动保留上一次保存的备份。", TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var error = new TextBlock { Foreground = Brush("#BE473C"), TextWrapping = TextWrapping.Wrap }; Children.Add(error);
        var save = Button("保存设置", () => {
            if (!TimeMath.TryParse(start.Text ?? "", out var from) || !TimeMath.TryParse(end.Text ?? "", out var to) || from >= 1440 || to - from < 120) { error.Text = "请填写有效时间，起止间隔至少2小时。"; return; }
            if (session.LoadError != null) { error.Text = session.Status; return; }
            session.Data.DefaultViewStart = from; session.Data.DefaultViewEnd = to; session.Data.GridMinutes = five.IsChecked == true ? 5 : 10;
            if (!mobile) { session.Data.CloseToTray = tray.IsChecked == true; session.Data.RememberCloseChoice = true; }
            session.Save(); session.Refresh(); saved();
        }, true, mobile);
        var cancel = Button("取消", () => Cancelled?.Invoke(), touch: mobile);
        if (mobile) { Children.Add(save); Children.Add(cancel); }
        else
        {
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            cancel.MinWidth = 80; save.MinWidth = 100; actions.Children.Add(cancel); actions.Children.Add(save); Children.Add(actions);
        }
    }
}
