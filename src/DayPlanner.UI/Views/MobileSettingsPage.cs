using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using DayPlanner.Core;
using static DayPlanner.UI.Views.PlannerShell;

namespace DayPlanner.UI.Views;

public sealed class MobileSettingsPage : Grid
{
    public event Action? Cancelled;
    public event Action? Saved;

    public MobileSettingsPage(PlannerSession session)
    {
        RowDefinitions = new("44,*,Auto");
        var header = new Grid { ColumnDefinitions = new("44,*,44") };
        header.Children.Add(MobileUi.Icon("返回", "back", () => Cancelled?.Invoke()));
        var heading = Label("设置", 17, true); heading.HorizontalAlignment = HorizontalAlignment.Center;
        Place(header, heading, 1); Children.Add(header);

        var content = new StackPanel { Spacing = 20, Margin = new Thickness(0, 20, 0, 0) };
        SetRow(content, 1); Children.Add(content);
        var start = new TextBox { Text = TimeMath.Format(session.Data.DefaultViewStart) };
        var end = new TextBox { Text = TimeMath.Format(session.Data.DefaultViewEnd) };
        foreach (var input in new[] { start, end })
        {
            MobileUi.Input(input); input.FontSize = 23; input.FontWeight = FontWeight.SemiBold; input.Padding = new Thickness(0, 4);
        }
        var startField = new StackPanel { Spacing = 4 }; startField.Children.Add(MobileUi.Caption("开始")); startField.Children.Add(start);
        var endField = new StackPanel { Spacing = 4 }; endField.Children.Add(MobileUi.Caption("结束")); endField.Children.Add(end);
        var times = new Grid { ColumnDefinitions = new("*,16,*") }; times.Children.Add(startField); Place(times, endField, 2);
        var range = new StackPanel { Spacing = 10 }; range.Children.Add(times);
        var hint = MobileUi.Caption("结束时间支持24:00，起止间隔至少2小时"); hint.TextWrapping = TextWrapping.Wrap; range.Children.Add(hint);
        var rangeSection = new StackPanel { Spacing = 8 }; rangeSection.Children.Add(MobileUi.Caption("默认显示时段")); rangeSection.Children.Add(MobileUi.Group(range)); content.Children.Add(rangeSection);

        var gridMinutes = session.Data.GridMinutes;
        var segments = new UniformGrid { Columns = 2 };
        Button? five = null, ten = null;
        void SelectGrid(int minutes)
        {
            gridMinutes = minutes;
            five!.Background = minutes == 5 ? MobileUi.Teal : Brushes.Transparent;
            five.Foreground = minutes == 5 ? Brushes.White : MobileUi.Muted;
            ten!.Background = minutes == 10 ? MobileUi.Teal : Brushes.Transparent;
            ten.Foreground = minutes == 10 ? Brushes.White : MobileUi.Muted;
        }
        five = MobileUi.Flat("5分钟", () => SelectGrid(5)); ten = MobileUi.Flat("10分钟", () => SelectGrid(10));
        segments.Children.Add(five); segments.Children.Add(ten); SelectGrid(gridMinutes);
        var snapSection = new StackPanel { Spacing = 8 }; snapSection.Children.Add(MobileUi.Caption("网格吸附"));
        snapSection.Children.Add(new Border { Background = Brush("#E9ECF1"), CornerRadius = new CornerRadius(12), Padding = new Thickness(3), Child = segments }); content.Children.Add(snapSection);

        var storage = MobileUi.Caption("数据保存在本机，自动保留上一次保存的备份。"); storage.TextWrapping = TextWrapping.Wrap; content.Children.Add(storage);
        var error = new TextBlock { Foreground = Brush("#BE473C"), FontSize = 12, TextWrapping = TextWrapping.Wrap }; content.Children.Add(error);
        var save = Button("保存设置", () =>
        {
            if (!TimeMath.TryParse(start.Text ?? "", out var from) || !TimeMath.TryParse(end.Text ?? "", out var to) || from >= 1440 || to - from < 120)
            { error.Text = "请填写有效时间，起止间隔至少2小时。"; return; }
            if (session.LoadError != null) { error.Text = session.Status; return; }
            session.Data.DefaultViewStart = from; session.Data.DefaultViewEnd = to; session.Data.GridMinutes = gridMinutes;
            if (!session.Save()) { error.Text = session.Status; return; }
            session.Refresh(); Saved?.Invoke();
        }, true, true);
        save.Height = 52; save.CornerRadius = new CornerRadius(16); save.Margin = new Thickness(0, 12, 0, 0);
        SetRow(save, 2); Children.Add(save);
    }
}
