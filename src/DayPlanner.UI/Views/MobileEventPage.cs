using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using DayPlanner.Core;
using static DayPlanner.UI.Views.PlannerShell;

namespace DayPlanner.UI.Views;

public sealed class MobileEventPage : Grid
{
    private readonly MobilePalette palette;
    private Action? closePicker;
    public event Action? Completed;
    public bool TryDismissTransient()
    {
        if (closePicker != null) { closePicker(); return true; }
        return palette.ExitEditing();
    }
    public MobileEventPage(ScheduleItem source, bool isNew, PlannerSession session)
    {
        RowDefinitions = new("44,*,Auto");
        var header = new Grid { ColumnDefinitions = new("44,*,44") };
        header.Children.Add(MobileUi.Icon("返回", "back", () => { if (!TryDismissTransient()) Completed?.Invoke(); }));
        var heading = Label(isNew ? "新建安排" : "编辑安排", 17, true); heading.HorizontalAlignment = HorizontalAlignment.Center; Place(header, heading, 1); Children.Add(header);
        var fields = new StackPanel { Spacing = 12, Margin = new Thickness(0, 12, 0, 0) }; SetRow(fields, 1); Children.Add(fields);
        var title = new TextBox { Text = source.Title, PlaceholderText = "想做些什么？", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 120,
            Height = 64, VerticalContentAlignment = VerticalAlignment.Top, FontWeight = FontWeight.SemiBold };
        MobileUi.Input(title); title.FontSize = 18;
        var titleGroup = MobileUi.Group(title, 6);
        var titleSection = new StackPanel { Spacing = 6 }; titleSection.Children.Add(MobileUi.Caption("安排内容")); titleSection.Children.Add(titleGroup); fields.Children.Add(titleSection);

        var schedule = new StackPanel { Spacing = 8 };
        var dateRow = new Grid { ColumnDefinitions = new("Auto,*,Auto"), Height = 36 }; dateRow.Children.Add(Label("日期", 14, true));
        var chosenDate = session.Date;
        var dateValue = MobileUi.Flat(chosenDate.ToString("yyyy年M月d日"), () => { }); dateValue.FontSize = 14; dateValue.Padding = new Thickness(0);
        // Existing items keep their date; new items can choose a date without mutating the calendar until saved.
        if (isNew)
        {
            var calendar = new Calendar { SelectedDate = chosenDate, DisplayDate = chosenDate, FirstDayOfWeek = DayOfWeek.Monday };
            var popup = new Popup { PlacementTarget = dateValue, Placement = PlacementMode.Bottom, IsLightDismissEnabled = true, Child = MobileUi.Group(calendar, 4) };
            Children.Add(popup);
            dateValue.Click += (_, _) => popup.IsOpen = !popup.IsOpen;
            calendar.SelectedDatesChanged += (_, _) => { if (calendar.SelectedDate is DateTime date) { chosenDate = date; dateValue.Content = date.ToString("yyyy年M月d日"); popup.IsOpen = false; } };
        }
        Place(dateRow, dateValue, 2); schedule.Children.Add(dateRow); schedule.Children.Add(MobileUi.Separator());
        var typeButtons = new UniformGrid { Columns = 2 };
        var isSpan = !source.IsPoint;
        var start = new TextBox { Text = TimeMath.Format(source.Start) };
        var end = new TextBox { Text = TimeMath.Format(source.End ?? Math.Min(1440, source.Start + 60)) };
        MobileUi.Input(start); MobileUi.Input(end); start.FontSize = end.FontSize = 23; start.FontWeight = end.FontWeight = FontWeight.SemiBold;
        start.Padding = end.Padding = new Thickness(0, 4);
        var from = new StackPanel { Spacing = 0 }; from.Children.Add(MobileUi.Caption("开始")); from.Children.Add(start);
        var to = new StackPanel { Spacing = 0 }; to.Children.Add(MobileUi.Caption("结束")); to.Children.Add(end);
        var timeRow = new Grid { ColumnDefinitions = new("*,16,*") }; timeRow.Children.Add(from); Place(timeRow, to, 2);
        Button? point = null, span = null;
        void Kind(bool range)
        {
            isSpan = range; to.IsVisible = range;
            point!.Background = range ? Brushes.Transparent : MobileUi.Teal; point.Foreground = range ? MobileUi.Muted : Brushes.White;
            span!.Background = range ? MobileUi.Teal : Brushes.Transparent; span.Foreground = range ? Brushes.White : MobileUi.Muted;
        }
        point = MobileUi.Flat("时间点", () => Kind(false)); span = MobileUi.Flat("时间段", () => Kind(true));
        point.MinHeight = span.MinHeight = 36; point.CornerRadius = span.CornerRadius = new CornerRadius(8);
        typeButtons.Children.Add(point); typeButtons.Children.Add(span); Kind(isSpan);
        schedule.Children.Add(new Border { Background = Brush("#EEF0F4"), CornerRadius = new CornerRadius(10), Padding = new Thickness(3), Child = typeButtons });
        schedule.Children.Add(timeRow); var timeHint = MobileUi.Caption("24小时制，结束时间支持24:00"); timeHint.FontSize = 10; schedule.Children.Add(timeHint);
        var scheduleGroup = MobileUi.Group(schedule, 12); fields.Children.Add(scheduleGroup);

        var knob = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Brushes.White };
        var track = new Border { Width = 48, Height = 28, CornerRadius = new CornerRadius(14), Padding = new Thickness(3), Child = knob };
        var reminder = new ToggleButton { IsChecked = source.ReminderEnabled, Content = track, MinHeight = 44, Width = 52, Background = Brushes.Transparent,
            BorderThickness = new Thickness(0), Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right };
        void ReminderAppearance() { track.Background = reminder.IsChecked == true ? MobileUi.Teal : Brush("#D8DDE4"); knob.HorizontalAlignment = reminder.IsChecked == true ? HorizontalAlignment.Right : HorizontalAlignment.Left; }
        ReminderAppearance(); reminder.IsCheckedChanged += (_, _) => ReminderAppearance();
        AutomationProperties.SetName(reminder, "提前提醒");
        var reminderRows = new StackPanel { Spacing = 4 };
        var reminderRow = new Grid { ColumnDefinitions = new("22,10,*,Auto"), MinHeight = 36 }; reminderRow.Children.Add(MobileUi.Glyph("bell"));
        Place(reminderRow, Label("提前提醒", 14, true), 2); Place(reminderRow, reminder, 3); reminderRows.Children.Add(reminderRow); reminderRows.Children.Add(MobileUi.Separator());
        var lead = new TextBox { Text = source.ReminderMinutes.ToString(), Width = 54, TextAlignment = TextAlignment.Right, IsReadOnly = !source.ReminderEnabled }; MobileUi.Input(lead);
        lead.Foreground = source.ReminderEnabled ? Brush("#203047") : MobileUi.Muted;
        reminder.IsCheckedChanged += (_, _) => { lead.IsReadOnly = reminder.IsChecked != true; lead.Foreground = reminder.IsChecked == true ? Brush("#203047") : MobileUi.Muted; };
        var leadRow = new Grid { ColumnDefinitions = new("*,Auto,Auto"), Height = 36 }; leadRow.Children.Add(Label("提前时间", 14));
        lead.MinHeight = 36; Place(leadRow, lead, 1); Place(leadRow, Label("分钟", 14), 2); reminderRows.Children.Add(leadRow);
        var reminderGroup = MobileUi.Group(reminderRows, 10); fields.Children.Add(reminderGroup);

        var error = new TextBlock { FontSize = 12, Foreground = Brush("#BE473C"), TextWrapping = TextWrapping.Wrap, IsVisible = false };
        void Error(string? message) { error.Text = message; error.IsVisible = !string.IsNullOrEmpty(message); }
        palette = new MobilePalette(session, source.Color);
        palette.PickerRequested += picker =>
        {
            var shade = new Grid { Background = Brush("#66203047"), Margin = new Thickness(-16, -8, -16, -12) };
            var card = MobileUi.Group(picker, 16); card.MaxWidth = 360; card.Margin = new Thickness(20);
            card.HorizontalAlignment = HorizontalAlignment.Stretch; card.VerticalAlignment = VerticalAlignment.Center; shade.Children.Add(card);
            closePicker = () => { Children.Remove(shade); closePicker = null; };
            picker.Closed += () => closePicker?.Invoke();
            shade.PointerPressed += (_, e) => { if (ReferenceEquals(e.Source, shade)) { picker.Cancel(); e.Handled = true; } };
            SetRowSpan(shade, 3); shade.ZIndex = 10; Children.Add(shade);
        };
        fields.Children.Add(palette); fields.Children.Add(error);

        var save = Button(isNew ? "创建安排" : "保存安排", () => {
            if (string.IsNullOrWhiteSpace(title.Text)) { Error("请填写日程内容。"); return; }
            if (!TimeMath.TryParse(start.Text ?? "", out var fromMinute) || fromMinute >= 1440) { Error("开始时间请填写00:00至23:59。"); return; }
            int? endMinute = null;
            if (isSpan) { if (!TimeMath.TryParse(end.Text ?? "", out var value) || value <= fromMinute) { Error("结束时间应晚于开始时间，最晚为24:00。"); return; } endMinute = value; }
            if (!int.TryParse(lead.Text, out var minutes) || minutes is < 1 or > 1440) { if (reminder.IsChecked == true) { Error("提前时间请填写1至1440分钟。"); return; } minutes = 5; }
            if (isNew) session.Select(chosenDate);
            session.Put(source with { Title = title.Text.Trim(), Start = fromMinute, End = endMinute, Color = palette.SelectedColor, ReminderEnabled = reminder.IsChecked == true, ReminderMinutes = minutes });
            Completed?.Invoke(); if (App.Reminders?.Status is string notificationStatus) App.Shell?.ShowMessage("提醒设置", notificationStatus);
        }, true, true);
        save.CornerRadius = new CornerRadius(16); save.Height = 52;
        var actions = new Grid { ColumnDefinitions = new(isNew ? "*" : "64,12,*"), Margin = new Thickness(0, 8, 0, 0) };
        if (isNew) actions.Children.Add(save);
        else { var delete = MobileUi.Flat("删除", () => { session.Delete(source); Completed?.Invoke(); }); delete.Foreground = Brush("#BE473C"); actions.Children.Add(delete); Place(actions, save, 2); }
        SetRow(actions, 2); Children.Add(actions);
        // Compact phones use tighter group spacing while keeping touch targets readable.
        SizeChanged += (_, _) =>
        {
            var compact = Bounds.Height < 710;
            fields.Spacing = compact ? 6 : 12; fields.Margin = new Thickness(0, compact ? 4 : 12, 0, 0);
            title.Height = compact ? 44 : 64; titleGroup.Padding = new Thickness(compact ? 4 : 6);
            titleSection.Spacing = compact ? 4 : 6;
            schedule.Spacing = compact ? 4 : 8; scheduleGroup.Padding = new Thickness(compact ? 8 : 12);
            reminderGroup.Padding = new Thickness(compact ? 8 : 10);
            start.MinHeight = end.MinHeight = compact ? 36 : 44; start.FontSize = end.FontSize = compact ? 21 : 23;
        };
    }
}
