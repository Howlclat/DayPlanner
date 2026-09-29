using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DayPlanner.Core;

namespace DayPlanner.UI.Controls;

/// <summary>A fixed time ruler and weekday header surround a touch-scrollable timeline.</summary>
public sealed class MobileTimelineView : Grid
{
    private readonly MobileTimelineSurface surface = new();
    private readonly ScrollViewer scroll;
    private readonly TimeRuler ruler;
    private readonly DayHeader header;
    private readonly TextBlock hint;
    private DateTime period;
    private bool weekly;
    private int from, to;
    private int revision;
    public event Action<DateTime, int, int?>? CreateRequested;
    public event Action<DateTime, ScheduleItem>? EditRequested;
    public event Action<DateTime>? DayRequested;

    public MobileTimelineView()
    {
        RowDefinitions = new("0,56,*,Auto"); ColumnDefinitions = new("44,*");
        Background = Ink("#F5F6F8"); ClipToBounds = true;
        hint = new TextBlock { FontSize = 10, Foreground = Ink("#8491A2"), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        SetRow(hint, 3); SetColumnSpan(hint, 2); Children.Add(hint);
        header = new DayHeader(surface) { ClipToBounds = true }; SetRow(header, 1); SetColumn(header, 1); Children.Add(header);
        header.DayRequested += date => DayRequested?.Invoke(date);
        ruler = new TimeRuler(surface) { ClipToBounds = true }; SetRow(ruler, 2); Children.Add(ruler);
        scroll = new ScrollViewer { Content = surface, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.ScrollChanged += (_, _) =>
        {
            ruler.Offset = scroll.Offset.Y; ruler.InvalidateVisual();
            header.Offset = scroll.Offset.X; header.InvalidateVisual();
        };
        SetColumn(scroll, 1); SetRow(scroll, 2); Children.Add(scroll);
        surface.CreateRequested += (date, start, end) => CreateRequested?.Invoke(date, start, end);
        surface.EditRequested += (date, item) => EditRequested?.Invoke(date, item);
    }

    public void Update(PlannerData data, DateTime date, bool week)
    {
        var anchor = week ? PlanningDates.WeekStart(date) : date.Date;
        var reset = anchor != period || weekly != week || from != data.DefaultViewStart || to != data.DefaultViewEnd;
        period = anchor; weekly = week; from = data.DefaultViewStart; to = data.DefaultViewEnd;
        surface.Update(data, date, week);
        scroll.HorizontalScrollBarVisibility = week ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
        RowDefinitions[1].Height = new GridLength(week ? 56 : 0);
        header.IsVisible = week;
        hint.Text = week ? "左右滑动查看整周" : "点击新建 · 长按拖出时间段";
        header.InvalidateVisual(); ruler.InvalidateVisual();
        if (!reset) return;
        var currentRevision = ++revision;
        Dispatcher.UIThread.Post(() =>
        {
            if (currentRevision != revision || !IsVisible) return;
            var items = Enumerable.Range(0, week ? 7 : 1).SelectMany(day => PlanningDates.Items(data, anchor.AddDays(day)))
                .Where(item => (item.End ?? item.Start) >= from && item.Start < to).ToList();
            var firstHour = items.Count == 0 ? from : Math.Max(from, Math.Floor(items.Min(item => item.Start) / 60.0) * 60 - 60);
            scroll.Offset = new Vector(week ? Math.Max(0, (date.Date - anchor).Days - 1) * MobileTimelineSurface.WeekColumnWidth : 0,
                Math.Max(0, firstHour - from) * MobileTimelineSurface.PixelsPerMinute);
        }, DispatcherPriority.Loaded);
    }

    public void Tick(DateTime now)
    {
        surface.Now = now; surface.InvalidateVisual(); ruler.InvalidateVisual(); header.InvalidateVisual();
    }
    private static IBrush Ink(string color) => TimelineControl.BrushOf(color);

    private sealed class TimeRuler(MobileTimelineSurface surface) : Control
    {
        public double Offset { get; set; }
        public override void Render(DrawingContext dc)
        {
            dc.DrawRectangle(Ink("#F5F6F8"), null, new Rect(Bounds.Size));
            var now = surface.Now;
            var showNow = surface.IncludesDate(now.Date) && now.TimeOfDay.TotalMinutes >= surface.Start && now.TimeOfDay.TotalMinutes <= surface.End;
            var nowY = surface.Y(now.TimeOfDay.TotalMinutes) - Offset;
            for (var minute = surface.Start; minute <= surface.End; minute = (minute / 60 + 1) * 60)
            {
                var y = surface.Y(minute) - Offset;
                if (y < -12 || y > Bounds.Height + 12 || showNow && Math.Abs(nowY - y) < 20) continue;
                TimelineControl.Text(dc, TimeMath.Format(minute), 4, y - 8, 11, Ink("#738297"), maxWidth: 42, maxHeight: 18);
            }
            if (showNow && nowY >= 0 && nowY <= Bounds.Height)
                TimelineControl.Text(dc, now.ToString("HH:mm"), 4, nowY - 8, 11, Ink("#F17A45"), true, 42, 18);
        }
    }

    private sealed class DayHeader(MobileTimelineSurface surface) : Control
    {
        public double Offset { get; set; }
        public event Action<DateTime>? DayRequested;
        private Point? pressed;
        public override void Render(DrawingContext dc)
        {
            dc.DrawRectangle(Ink("#F5F6F8"), null, new Rect(Bounds.Size));
            for (var i = 0; i < 7; i++)
            {
                var date = surface.FirstDate.AddDays(i);
                var x = i * MobileTimelineSurface.WeekColumnWidth - Offset;
                var width = MobileTimelineSurface.WeekColumnWidth;
                if (x + width < 0 || x > Bounds.Width) continue;
                var active = date == surface.SelectedDate;
                if (active) dc.DrawEllipse(Ink("#008D94"), null, new Point(x + width / 2, 35), 15, 15);
                var color = Ink(active || date == surface.Now.Date ? "#008D94" : "#203047");
                TimelineControl.Text(dc, date.ToString("ddd", CultureInfo.GetCultureInfo("zh-CN")), x + 4, 2, 11, color, maxWidth: width - 8, maxHeight: 18, centered: true);
                TimelineControl.Text(dc, date.Day.ToString(), x + 4, 25, 14, active ? Brushes.White : color, true, width - 8, 20, true);
            }
        }
        protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); pressed = e.GetPosition(this); }
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (pressed is not Point origin) return;
            pressed = null; var point = e.GetPosition(this);
            if (Math.Abs(point.X - origin.X) + Math.Abs(point.Y - origin.Y) > 10) return;
            var column = (int)((point.X + Offset) / MobileTimelineSurface.WeekColumnWidth);
            if (column is >= 0 and < 7) { DayRequested?.Invoke(surface.FirstDate.AddDays(column)); e.Handled = true; }
        }
    }
}

/// <summary>Uses real time coordinates; overlapping items share horizontal lanes.</summary>
public sealed class MobileTimelineSurface : Control
{
    public const double PixelsPerMinute = 1.0;
    public const double WeekColumnWidth = 96;
    private const double TopPadding = 12;
    public int Start { get; private set; } = 360;
    public int End { get; private set; } = 1320;
    public DateTime FirstDate { get; private set; } = DateTime.Today;
    public DateTime SelectedDate { get; private set; } = DateTime.Today;
    public DateTime Now { get; set; } = DateTime.Now;
    private bool weekly;
    private int grid = 10;
    private PlannerData data = new();
    private readonly List<Placement> placements = [];
    private sealed record Placement(DateTime Date, ScheduleItem Item, Rect Bounds, double Anchor);
    private sealed record Slot(ScheduleItem Item, double Top, double Bottom) { public int Lane { get; set; } }
    public event Action<DateTime, int, int?>? CreateRequested;
    public event Action<DateTime, ScheduleItem>? EditRequested;
    private Point? pressed;
    private IPointer? pointer;
    private IDisposable? hold;
    private bool selecting;
    private int startMinute, endMinute, pressedColumn;
    private Placement? pressedItem;

    public MobileTimelineSurface() { ClipToBounds = true; Focusable = true; }
    public void Update(PlannerData source, DateTime date, bool week)
    {
        CancelPress(); data = source; SelectedDate = date.Date; weekly = week;
        FirstDate = week ? PlanningDates.WeekStart(date) : date.Date;
        Start = source.DefaultViewStart; End = source.DefaultViewEnd; grid = source.GridMinutes;
        Width = week ? WeekColumnWidth * 7 : double.NaN;
        Height = (End - Start) * PixelsPerMinute + TopPadding * 2;
        InvalidateMeasure(); InvalidateVisual();
    }
    public double Y(double minute) => TopPadding + (minute - Start) * PixelsPerMinute;
    public bool IncludesDate(DateTime date) => date >= FirstDate && date < FirstDate.AddDays(weekly ? 7 : 1);
    private double ColumnWidth => weekly ? WeekColumnWidth : Math.Max(1, Bounds.Width);
    private int Snap(double y) => Math.Clamp(TimeMath.Snap(Start + (y - TopPadding) / PixelsPerMinute, grid), Start, End);

    private void LayoutItems()
    {
        placements.Clear();
        for (var day = 0; day < (weekly ? 7 : 1); day++)
        {
            var date = FirstDate.AddDays(day);
            var slots = PlanningDates.Items(data, date).Where(item => item.Start < End && (item.End ?? item.Start) >= Start)
                .Select(item =>
                {
                    var pointHeight = weekly ? 44 : 32;
                    var top = item.IsPoint ? Math.Clamp(Y(item.Start) - pointHeight / 2.0, 2, Height - pointHeight - 2) : Y(Math.Max(Start, item.Start));
                    var bottom = item.IsPoint ? top + pointHeight : Math.Max(top + 4, Y(Math.Min(End, item.End!.Value)));
                    return new Slot(item, top, bottom);
                }).OrderBy(item => item.Top).ThenBy(item => item.Item.Start).ThenBy(item => item.Item.Id).ToList();
            var group = new List<Slot>(); var laneEnds = new List<double>(); var groupEnd = double.NegativeInfinity;
            void FinishGroup()
            {
                var laneWidth = (ColumnWidth - 8) / Math.Max(1, laneEnds.Count);
                foreach (var slot in group)
                    placements.Add(new(date, slot.Item, new Rect(day * ColumnWidth + 4 + slot.Lane * laneWidth, slot.Top,
                        Math.Max(3, laneWidth - 4), slot.Bottom - slot.Top), Y(slot.Item.Start)));
                group.Clear(); laneEnds.Clear(); groupEnd = double.NegativeInfinity;
            }
            foreach (var slot in slots)
            {
                if (slot.Top >= groupEnd) FinishGroup();
                var lane = laneEnds.FindIndex(end => end <= slot.Top);
                if (lane < 0) { lane = laneEnds.Count; laneEnds.Add(slot.Bottom); } else laneEnds[lane] = slot.Bottom;
                slot.Lane = lane; group.Add(slot); groupEnd = Math.Max(groupEnd, slot.Bottom);
            }
            FinishGroup();
        }
    }

    public override void Render(DrawingContext dc)
    {
        base.Render(dc); LayoutItems();
        dc.DrawRectangle(TimelineControl.BrushOf("#FAFBFD"), null, new Rect(Bounds.Size));
        for (var day = 0; day < (weekly ? 7 : 1); day++)
        {
            var date = FirstDate.AddDays(day); var x = day * ColumnWidth;
            if (weekly && date == SelectedDate) dc.DrawRectangle(TimelineControl.BrushOf("#F5FBFB"), null, new Rect(x, 0, ColumnWidth, Height));
            dc.DrawLine(new Pen(TimelineControl.BrushOf("#E4EAF1"), 1), new Point(x, 0), new Point(x, Height));
        }
        for (var minute = Start; minute <= End; minute += grid)
        {
            var color = minute % 60 == 0 ? "#DAE3EC" : minute % 30 == 0 ? "#E9EEF4" : "#F4F6F9";
            dc.DrawLine(new Pen(TimelineControl.BrushOf(color), 1), new Point(0, Y(minute)), new Point(Bounds.Width, Y(minute)));
        }
        foreach (var entry in placements)
        {
            var rect = entry.Bounds; var color = TimelineControl.BrushOf(entry.Item.Color);
            var point = entry.Item.IsPoint;
            if (point)
            {
                var lead = weekly ? 14 : Math.Min(64, rect.Width * .2);
                dc.DrawLine(new Pen(color, 1), new Point(rect.Left + 5, entry.Anchor), new Point(rect.Left + lead, entry.Anchor));
                dc.DrawEllipse(color, null, new Point(rect.Left + 5, entry.Anchor), 3.5, 3.5);
                rect = new Rect(rect.Left + lead, rect.Top, Math.Max(1, Math.Min(weekly ? rect.Width : 220, rect.Width - lead)), rect.Height);
                dc.DrawRectangle(Brushes.White, new Pen(color, 1), rect, 5, 5);
                if (!weekly)
                {
                    TimelineControl.Text(dc, $"{entry.Item.Title} · {TimeMath.Format(entry.Item.Start)}", rect.Left + 8, rect.Top + 7, 12,
                        TimelineControl.BrushOf("#203047"), true, Math.Max(1, rect.Width - 16), 18);
                    continue;
                }
            }
            else dc.DrawRectangle(color, null, rect, Math.Min(6, rect.Height / 2), Math.Min(6, rect.Height / 2));
            IBrush ink = point ? TimelineControl.BrushOf("#203047") : Brushes.White;
            var padding = rect.Width < 60 ? 4 : 8;
            if (rect.Width < 20 || rect.Height < 24) continue;
            var showTime = rect.Height >= (point ? 44 : 60) && rect.Width >= 50;
            var textHeight = Math.Max(16, Math.Min(showTime ? rect.Height - 28 : rect.Height - 4, weekly ? 36 : 48));
            var titleY = showTime ? rect.Top + 4 : rect.Top + (rect.Height - textHeight) / 2;
            var titleHeight = TimelineControl.Text(dc, entry.Item.Title, rect.Left + padding, titleY, weekly ? 12 : 14, ink, true,
                Math.Max(1, rect.Width - padding * 2), textHeight);
            if (showTime)
            {
                var time = weekly || point ? TimeMath.Format(entry.Item.Start) : $"{TimeMath.Format(entry.Item.Start)}—{TimeMath.Format(entry.Item.End!.Value)}";
                TimelineControl.Text(dc, time, rect.Left + padding, titleY + titleHeight + 4, 11, point ? TimelineControl.BrushOf("#738297") : ink,
                    maxWidth: Math.Max(1, rect.Width - padding * 2), maxHeight: 18);
            }
        }
        if (IncludesDate(Now.Date) && Now.TimeOfDay.TotalMinutes >= Start && Now.TimeOfDay.TotalMinutes <= End)
        {
            var x = (Now.Date - FirstDate).Days * ColumnWidth; var y = Y(Now.TimeOfDay.TotalMinutes);
            var orange = TimelineControl.BrushOf("#F17A45");
            dc.DrawLine(new Pen(orange, 1.5), new Point(x, y), new Point(x + ColumnWidth, y));
            dc.DrawEllipse(orange, null, new Point(x + 3, y), 3, 3);
        }
        if (selecting)
        {
            var start = Math.Min(startMinute, endMinute); var end = Math.Min(End, Math.Max(Math.Max(startMinute, endMinute), start + grid));
            var rect = new Rect(pressedColumn * ColumnWidth + 3, Y(start), ColumnWidth - 6, Math.Max(4, Y(end) - Y(start)));
            var teal = TimelineControl.BrushOf("#008D94");
            dc.DrawRectangle(TimelineControl.BrushOf("#33008D94"), new Pen(teal, 1.5), rect, 4, 4);
            TimelineControl.Text(dc, $"{TimeMath.Format(start)}\n{TimeMath.Format(end)}", rect.Left + 6, rect.Top + 4, 11, teal, true, rect.Width - 12, 36);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(this);
        if (point.Y < Y(Start) || point.Y > Y(End)) return;
        CancelPress(); LayoutItems(); pressed = point; pointer = e.Pointer;
        pressedColumn = Math.Clamp((int)(point.X / ColumnWidth), 0, weekly ? 6 : 0);
        startMinute = endMinute = Math.Min(End - grid, Snap(point.Y));
        pressedItem = Hit(point);
        if (pressedItem != null) return;
        hold = DispatcherTimer.RunOnce(() =>
        {
            if (pressed == null || pointer == null) return;
            selecting = true; pointer.Capture(this); InvalidateVisual();
        }, TimeSpan.FromMilliseconds(450));
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (pressed is not Point origin) return;
        var point = e.GetPosition(this);
        if (selecting) { endMinute = Snap(point.Y); InvalidateVisual(); e.Handled = true; }
        else if (Math.Abs(point.X - origin.X) + Math.Abs(point.Y - origin.Y) > 10) CancelPress();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (pressed == null) return;
        var date = FirstDate.AddDays(pressedColumn); var item = pressedItem;
        var isRange = selecting; var start = Math.Min(startMinute, endMinute); var end = Math.Max(startMinute, endMinute);
        CancelPress();
        if (isRange) CreateRequested?.Invoke(date, start, Math.Min(End, Math.Max(end, start + grid)));
        else if (item != null) EditRequested?.Invoke(item.Date, item.Item);
        else CreateRequested?.Invoke(date, start, null);
        e.Handled = true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { base.OnPointerCaptureLost(e); CancelPress(); }
    private Placement? Hit(Point point)
    {
        var exact = placements.LastOrDefault(item => item.Bounds.Contains(point));
        return exact ?? placements.Where(item => point.X >= item.Bounds.Left && point.X <= item.Bounds.Right
            && Math.Abs(point.Y - item.Bounds.Center.Y) <= Math.Max(22, item.Bounds.Height / 2))
            .OrderBy(item => Math.Abs(point.Y - item.Bounds.Center.Y)).FirstOrDefault();
    }
    private void CancelPress()
    {
        var releaseSelection = selecting;
        hold?.Dispose(); hold = null; pressed = null; pressedItem = null; selecting = false;
        var captured = pointer; pointer = null;
        // Touch implicitly captures the surface; leave that capture intact for the parent scroll recognizer.
        if (releaseSelection && captured?.Captured == this) captured.Capture(null);
        InvalidateVisual();
    }
}
