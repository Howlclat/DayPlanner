using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DayPlanner;

public static class PlanningDates
{
    public static DateTime WeekStart(DateTime date) => date.Date.AddDays(-((int)date.DayOfWeek + 6) % 7);
    public static DateTime MonthStart(DateTime date) => new(date.Year, date.Month, 1);
    public static int MonthRows(DateTime date) => (((int)MonthStart(date).DayOfWeek + 6) % 7 + DateTime.DaysInMonth(date.Year, date.Month) + 6) / 7;
    public static IReadOnlyList<ScheduleItem> Items(PlannerData data, DateTime date) =>
        data.Days.TryGetValue(PlannerData.DateKey(date), out var items) ? items : Array.Empty<ScheduleItem>();
}

public sealed class WeekPlannerView : Grid
{
    private readonly StackPanel rows = new();
    private readonly WeekRuler ruler = new();
    private readonly List<(Button Label, TimelineControl Timeline)> days = [];
    private DateTime week;
    private bool syncing;
    public event Action<DateTime>? DateSelected;
    public event Action<DateTime>? DayRequested;
    public event Action<DateTime, int, int?>? CreateRequested;
    public event Action<DateTime, ScheduleItem>? EditRequested;
    public event Action<DateTime, ScheduleItem>? DeleteRequested;
    public event Action<DateTime, ScheduleItem, int, int?>? TimeChanged;
    public event Action<double, double>? ViewChanged;

    public WeekPlannerView()
    {
        RowDefinitions.Add(new() { Height = new GridLength(48) });
        RowDefinitions.Add(new());
        ruler.Margin = new Thickness(86, 0, 0, 0);
        Children.Add(ruler);
        var scroll = new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        // Reserve the vertical scrollbar width in the shared ruler as well.
        scroll.ScrollChanged += (_, _) => { ruler.Margin = new Thickness(86, 0, scroll.ComputedVerticalScrollBarVisibility == Visibility.Visible ? SystemParameters.VerticalScrollBarWidth : 0, 0); };
        SetRow(scroll, 1); Children.Add(scroll);
        for (var i = 0; i < 7; i++)
        {
            var index = i;
            var row = new Grid();
            row.ColumnDefinitions.Add(new() { Width = new GridLength(86) });
            row.ColumnDefinitions.Add(new());
            var label = new Button { Padding = new Thickness(4), Margin = new Thickness(4, 3, 0, 3) };
            label.Click += (_, _) => DateSelected?.Invoke(week.AddDays(index));
            label.MouseDoubleClick += (_, e) => { DayRequested?.Invoke(week.AddDays(index)); e.Handled = true; };
            row.Children.Add(label);
            var timeline = new TimelineControl { Compact = true };
            timeline.PreviewMouseDown += (_, _) => DateSelected?.Invoke(week.AddDays(index));
            timeline.CreateRequested += (start, end) => CreateRequested?.Invoke(week.AddDays(index), start, end);
            timeline.EditRequested += item => EditRequested?.Invoke(week.AddDays(index), item);
            timeline.DeleteRequested += item => DeleteRequested?.Invoke(week.AddDays(index), item);
            timeline.TimeChanged += (item, start, end) => TimeChanged?.Invoke(week.AddDays(index), item, start, end);
            timeline.ViewChanged += () => { if (!syncing) ViewChanged?.Invoke(timeline.ViewStart, timeline.ViewSpan); };
            SetColumn(timeline, 1); row.Children.Add(timeline);
            rows.Children.Add(new Border { Child = row, BorderBrush = TimelineControl.BrushOf("#E4EAF1"), BorderThickness = new Thickness(0, 0, 0, 1) });
            days.Add((label, timeline));
        }
    }
    public void Update(PlannerData data, DateTime selected, double start, double span)
    {
        var nextWeek = PlanningDates.WeekStart(selected);
        var changedWeek = nextWeek != week;
        week = nextWeek;
        for (var i = 0; i < 7; i++)
        {
            var date = week.AddDays(i);
            var (label, timeline) = days[i];
            label.Content = date.ToString("ddd\nM/d", CultureInfo.GetCultureInfo("zh-CN")) + (date == DateTime.Today ? "\n今天" : "");
            label.Background = TimelineControl.BrushOf(date == selected ? "#DDF2F2" : i >= 5 ? "#F4F7FA" : "#FFFFFF");
            label.Foreground = TimelineControl.BrushOf(date == selected ? "#008D94" : "#203047");
            timeline.Items = PlanningDates.Items(data, date).ToList();
            timeline.SelectedDate = date;
            timeline.Highlight = date == selected;
            timeline.Weekend = i >= 5;
            timeline.GridMinutes = data.GridMinutes;
            if (changedWeek || date != selected) timeline.ClearSelection();
            timeline.Refresh();
        }
        SetView(start, span);
    }
    public void SetView(double start, double span)
    {
        syncing = true;
        try { foreach (var day in days) day.Timeline.SetView(start, span); }
        finally { syncing = false; }
        ruler.Start = start; ruler.Span = span; ruler.InvalidateVisual();
    }
    public void Tick(DateTime now)
    {
        foreach (var day in days) { day.Timeline.Now = now; day.Timeline.InvalidateVisual(); }
    }
    private sealed class WeekRuler : FrameworkElement
    {
        public double Start { get; set; } = 360;
        public double Span { get; set; } = 960;
        protected override void OnRender(DrawingContext dc)
        {
            var ink = TimelineControl.BrushOf("#203047");
            for (var t = (int)Math.Ceiling(Start / 60) * 60; t <= Start + Span; t += 60)
            {
                var x = 36 + (t - Start) / Span * Math.Max(1, ActualWidth - 72);
                dc.DrawLine(new Pen(TimelineControl.BrushOf("#BFCAD6"), 1), new Point(x, 34), new Point(x, 48));
                if (Span <= 900 || t % 120 == 0) TimelineControl.Text(dc, TimeMath.Format(t), x - 17, 12, 11, ink);
            }
        }
    }
}

public sealed class MonthPlannerView : FrameworkElement
{
    private PlannerData data = new();
    private DateTime selected = DateTime.Today;
    private readonly List<(Rect Bounds, DateTime Date, ScheduleItem? Item)> hits = [];
    public event Action<DateTime>? DateSelected;
    public event Action<DateTime>? DayRequested;
    public event Action<DateTime, ScheduleItem>? EditRequested;
    public MonthPlannerView() { MinHeight = 440; ClipToBounds = true; }
    public void Update(PlannerData value, DateTime date) { data = value; selected = date.Date; InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        hits.Clear();
        var first = PlanningDates.MonthStart(selected);
        var start = PlanningDates.WeekStart(first);
        var rowCount = PlanningDates.MonthRows(selected);
        var cellWidth = ActualWidth / 7;
        var cellHeight = Math.Max(60, (ActualHeight - 38) / rowCount);
        var ink = TimelineControl.BrushOf("#203047");
        var muted = TimelineControl.BrushOf("#8491A2");
        var teal = TimelineControl.BrushOf("#008D94");
        var grid = new Pen(TimelineControl.BrushOf("#DEE6ED"), 1);
        string[] labels = ["周一", "周二", "周三", "周四", "周五", "周六", "周日"];
        for (var col = 0; col < 7; col++)
        {
            dc.DrawRectangle(TimelineControl.BrushOf("#F4F7FA"), grid, new Rect(col * cellWidth, 0, cellWidth, 38));
            TimelineControl.Text(dc, labels[col], col * cellWidth + Math.Max(5, (cellWidth - 30) / 2), 10, 12, ink);
        }
        for (var index = 0; index < rowCount * 7; index++)
        {
            var date = start.AddDays(index);
            var rect = new Rect(index % 7 * cellWidth, 38 + index / 7 * cellHeight, cellWidth, cellHeight);
            dc.DrawRectangle(TimelineControl.BrushOf(date == selected ? "#E6F5F5" : index % 7 >= 5 ? "#F7F9FB" : "#FFFFFF"), grid, rect);
            hits.Add((rect, date, null));
            if (date == DateTime.Today)
            {
                dc.DrawEllipse(teal, null, new Point(rect.Left + 22, rect.Top + 19), 14, 14);
                TimelineControl.Text(dc, date.Day.ToString(), rect.Left + 12, rect.Top + 9, 13, Brushes.White, true);
            }
            else TimelineControl.Text(dc, date.Day.ToString(), rect.Left + 10, rect.Top + 9, 13, date.Month == selected.Month ? ink : muted, true);
            var items = PlanningDates.Items(data, date).OrderBy(item => item.Start).ThenBy(item => item.Id).ToList();
            var capacity = Math.Max(0, (int)((cellHeight - 39) / 24));
            var shown = items.Count > capacity ? Math.Max(0, capacity - 1) : items.Count;
            dc.PushClip(new RectangleGeometry(rect));
            for (var i = 0; i < shown; i++)
            {
                var item = items[i];
                var y = rect.Top + 37 + i * 24;
                dc.DrawEllipse(TimelineControl.BrushOf(item.Color), null, new Point(rect.Left + 12, y + 8), 3.5, 3.5);
                TimelineControl.Text(dc, $"{TimeMath.Format(item.Start)} {item.Title}", rect.Left + 22, y, 11,
                    date.Month == selected.Month ? ink : muted, maxWidth: Math.Max(1, cellWidth - 28), maxHeight: 20);
                hits.Add((new Rect(rect.Left + 3, y - 2, Math.Max(1, cellWidth - 6), 23), date, item));
            }
            if (items.Count > shown)
                TimelineControl.Text(dc, $"+{items.Count - shown}项", rect.Left + 10, rect.Top + 37 + shown * 24, 11, teal, maxHeight: 20);
            dc.Pop();
        }
        var selectedIndex = (selected - start).Days;
        dc.DrawRectangle(null, new Pen(teal, 1.5), new Rect(selectedIndex % 7 * cellWidth + 1, 39 + selectedIndex / 7 * cellHeight,
            Math.Max(1, cellWidth - 2), Math.Max(1, cellHeight - 2)));
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var point = e.GetPosition(this);
        var hit = hits.LastOrDefault(entry => entry.Bounds.Contains(point));
        if (hit.Date == default) return;
        DateSelected?.Invoke(hit.Date);
        if (hit.Item != null) EditRequested?.Invoke(hit.Date, hit.Item);
        else if (e.ClickCount == 2) DayRequested?.Invoke(hit.Date);
        e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = hits.Any(entry => entry.Bounds.Contains(e.GetPosition(this))) ? Cursors.Hand : Cursors.Arrow;
    }
}
