using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using DayPlanner.Core;

namespace DayPlanner.UI.Views;

public sealed partial class PlannerShell
{
    private readonly TextBlock phoneEyebrow = MobileUi.Caption("");
    private readonly UniformGrid phoneDates = new() { Columns = 7, Margin = new Thickness(0, 4, 0, 0) };
    private StackPanel? phonePeriodArrows;
    private Button? phoneCalendarButton;
    private Control BuildPhoneHeader()
    {
        var header = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        var heading = new Grid { ColumnDefinitions = new("*,Auto"), MinHeight = 60 };
        var captions = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        title.FontSize = 27; title.TextTrimming = TextTrimming.CharacterEllipsis;
        captions.Children.Add(phoneEyebrow); captions.Children.Add(title); heading.Children.Add(captions);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        phonePeriodArrows = new StackPanel { Orientation = Orientation.Horizontal };
        phonePeriodArrows.Children.Add(MobileUi.Icon("上一月", "back", () => session.Navigate(-1)));
        phonePeriodArrows.Children.Add(MobileUi.Icon("下一月", "next", () => session.Navigate(1)));
        actions.Children.Add(phonePeriodArrows);
        phoneCalendarButton = MobileUi.Icon("选择日期", "calendar", ChooseDate); actions.Children.Add(phoneCalendarButton);
        actions.Children.Add(MobileUi.Icon("设置", "settings", Settings)); Place(heading, actions, 1); header.Children.Add(heading);
        var tabs = new UniformGrid { Columns = 3 }; AddModes(tabs.Children, true);
        header.Children.Add(new Border { Background = Brush("#E9ECF1"), CornerRadius = new CornerRadius(10), Padding = new Thickness(3), Child = tabs });
        header.Children.Add(phoneDates); return header;
    }
    private Control BuildPhoneToolbar()
    {
        var actions = new Grid { ColumnDefinitions = new("*,44,12,44,*,52"), Margin = new Thickness(0, 8, 0, 0), Height = 56 };
        var today = MobileUi.Flat("今天", () => session.Select(DateTime.Today)); today.Foreground = MobileUi.Teal;
        today.FontWeight = FontWeight.SemiBold; today.HorizontalAlignment = HorizontalAlignment.Left; actions.Children.Add(today);
        var pair = new[] { undo, redo };
        for (var i = 0; i < pair.Length; i++)
        {
            var button = pair[i]; button.Content = MobileUi.Glyph(i == 0 ? "undo" : "redo", "#788393");
            button.Classes.Add("mobileFlat");
            button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(0); button.Width = 44;
            button.Height = 44; button.Padding = new Thickness(10); button.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(button, i == 0 ? "撤销" : "重做"); Place(actions, button, i == 0 ? 1 : 3);
        }
        var add = MobileUi.Icon("新建安排", "plus", () => New(540, null)); add.Content = MobileUi.Glyph("plus", "#FFFFFF");
        add.Background = MobileUi.Teal; add.Width = add.Height = 52; add.CornerRadius = new CornerRadius(26); Place(actions, add, 5);
        return actions;
    }
    private void RefreshPhoneHeader()
    {
        var date = session.Date; var start = PlanningDates.WeekStart(date);
        var monthMode = session.Mode == PlannerMode.Month;
        phoneEyebrow.Text = session.Mode == PlannerMode.Day ? date.ToString("yyyy年M月") : monthMode ? date.ToString("yyyy年") : $"{start.Year}年 · 第{ISOWeek.GetWeekOfYear(start)}周";
        title.Text = session.Mode == PlannerMode.Day ? date.ToString("d日 ddd", CultureInfo.GetCultureInfo("zh-CN")) : monthMode ? date.ToString("M月") : $"{start:M月d日}—{start.AddDays(6):M月d日}";
        title.FontSize = session.Mode == PlannerMode.Week ? 21 : 27;
        phonePeriodArrows!.IsVisible = monthMode;
        phoneCalendarButton!.IsVisible = !monthMode;
        phoneDates.IsVisible = session.Mode == PlannerMode.Day; phoneDates.Children.Clear();
        if (phoneDates.IsVisible)
            for (var i = 0; i < 7; i++) phoneDates.Children.Add(PhoneDateButton(start.AddDays(i), true));
        phoneContent.Spacing = 8;
    }
    private Button PhoneDateButton(DateTime date, bool strip)
    {
        var button = MobileUi.Flat("", () => session.Select(date)); button.Padding = new Thickness(0); button.Height = strip ? 56 : 44;
        var content = new StackPanel { Spacing = 1, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (strip) { var weekday = MobileUi.Caption(date.ToString("ddd", CultureInfo.GetCultureInfo("zh-CN"))); weekday.HorizontalAlignment = HorizontalAlignment.Center; content.Children.Add(weekday); }
        var selected = date == session.Date;
        var label = Label(date.Day.ToString(), 14, true); label.HorizontalAlignment = HorizontalAlignment.Center;
        label.Foreground = selected ? Brushes.White : Brush(!strip && date.Month != session.Date.Month ? "#A2ABBA" : "#203047");
        content.Children.Add(new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = selected ? MobileUi.Teal : Brushes.Transparent, Child = label });
        if (!strip)
        {
            var dots = new StackPanel { Height = 5, Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var color in PlanningDates.Items(session.Data, date).Select(item => item.Color).Distinct().Take(3))
                dots.Children.Add(new Border { Width = 4, Height = 4, Background = Brush(color), CornerRadius = new CornerRadius(2) });
            content.Children.Add(dots);
        }
        button.Content = content; AutomationProperties.SetName(button, date.ToString("yyyy年M月d日")); return button;
    }
    private Control BuildPhoneAgenda()
    {
        var panel = new StackPanel { Spacing = 0 };
        var heading = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new Thickness(0, 0, 0, 8) };
        heading.Children.Add(Label(session.Date.ToString("M月d日 ddd", CultureInfo.GetCultureInfo("zh-CN")), 16, true));
        Place(heading, MobileUi.Caption($"{session.Items.Count}项安排"), 1); panel.Children.Add(heading);
        foreach (var item in session.Items.OrderBy(item => item.Start))
        {
            var row = new Grid { ColumnDefinitions = new("5,10,*,8,Auto") };
            row.Children.Add(new Border { Width = 5, Height = 25, CornerRadius = new CornerRadius(2), Background = Brush(item.Color) });
            var label = Label(item.Title, 14, true); label.TextTrimming = TextTrimming.CharacterEllipsis; Place(row, label, 2);
            var time = MobileUi.Caption(TimeMath.Format(item.Start) + (item.End is int end ? "–" + TimeMath.Format(end) : "")); Place(row, time, 4);
            var button = MobileUi.Flat("", () => Edit(item)); button.Content = row; button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.Padding = new Thickness(0); button.MinHeight = 44;
            panel.Children.Add(button); panel.Children.Add(MobileUi.Separator());
        }
        if (session.Items.Count == 0) panel.Children.Add(MobileUi.Flat("暂无安排，点击添加", () => New(540, null)));
        return MobileUi.Group(panel, 12);
    }
}
