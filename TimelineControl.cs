using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DayPlanner;

public sealed class TimelineControl : FrameworkElement
{
    public List<ScheduleItem> Items { get; set; } = [];
    public int GridMinutes { get; set; } = 10;
    public double ViewStart { get; private set; } = 360;
    public double ViewSpan { get; private set; } = 960;
    public Guid? SelectedId { get; private set; }
    public DateTime Now { get; set; } = DateTime.Now;
    public DateTime SelectedDate { get; set; } = DateTime.Today;
    public bool ShowsToday => SelectedDate.Date == Now.Date;
    public event Action<int, int?>? CreateRequested;
    public event Action<ScheduleItem>? EditRequested;
    public event Action<ScheduleItem>? DeleteRequested;
    public event Action<ScheduleItem, int, int?>? TimeChanged;
    public event Action? ViewChanged;
    private enum DragMode { None, Create, Move, Start, End, Pan }
    private DragMode drag;
    private Point origin;
    private double originalView;
    private int anchor;
    private int current;
    private ScheduleItem? dragItem;
    private ScheduleItem? preview;
    private bool moved;
    private Point? hoverPosition;
    private readonly List<LayoutItem> layout = [];
    private sealed record LayoutItem(ScheduleItem Item, Rect Bar, Rect Card, double Anchor, int Lane);
    private static readonly Brush Ink = BrushOf("#203047");
    private static readonly Brush Muted = BrushOf("#738297");
    private double PlotWidth => Math.Max(1, ActualWidth - 72);
    private double X(double time) => 36 + (time - ViewStart) / ViewSpan * PlotWidth;
    private double Minute(double x) => ViewStart + (x - 36) / PlotWidth * ViewSpan;
    public TimelineControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }
    public void Refresh() { InvalidateMeasure(); InvalidateVisual(); }
    public void SetView(double start, double span)
    {
        ViewSpan = Math.Clamp(span, 120, 1440);
        ViewStart = Math.Clamp(start, 0, 1440 - ViewSpan);
        Refresh();
        ViewChanged?.Invoke();
    }
    public void ClearSelection() { SelectedId = null; hoverPosition = null; CancelDrag(); }
    private double BuildLayout(double width)
    {
        layout.Clear();
        var plot = Math.Max(1, width - 72);
        double Map(double t) => 36 + (t - ViewStart) / ViewSpan * plot;
        var visible = Items.Select(i => preview?.Id == i.Id ? preview : i)
            .Where(i => (i.End ?? i.Start) >= ViewStart && i.Start <= ViewStart + ViewSpan).OrderBy(i => i.Start).ThenBy(i => i.Id).ToList();
        var bars = new List<double>();
        var staged = new List<(ScheduleItem Item, Rect Bar, double Anchor, int Lane)>();
        foreach (var item in visible)
        {
            double left = Math.Max(36, Map(item.Start)), right = Math.Min(width - 36, Map(item.End ?? item.Start));
            var lane = bars.FindIndex(end => end + 14 < left);
            if (lane < 0) { lane = bars.Count; bars.Add(0); }
            bars[lane] = Math.Max(left + 12, right);
            var bar = new Rect(item.IsPoint ? left - 7 : left, 92 + lane * 27, item.IsPoint ? 14 : Math.Max(6, right - left), 19);
            staged.Add((item, bar, item.IsPoint ? left : (left + right) / 2, lane));
        }
        var cards = new List<List<Rect>>();
        var cardTop = 151 + Math.Max(0, bars.Count - 1) * 27;
        foreach (var (item, bar, at, barLane) in staged)
        {
            var cardWidth = Math.Min(152, Math.Max(140, width - 80));
            var left = Math.Clamp(at - cardWidth / 2, 20, Math.Max(20, width - 20 - cardWidth));
            var row = 0;
            while (row < cards.Count && cards[row].Any(r => left < r.Right + 16 && left + cardWidth + 16 > r.Left)) row++;
            if (row == cards.Count) cards.Add([]);
            var rect = new Rect(left, cardTop + row * 108, cardWidth, 78);
            cards[row].Add(rect);
            layout.Add(new(item, bar, rect, at, barLane));
        }
        return Math.Max(400, cardTop + Math.Max(1, cards.Count) * 108 + 48);
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 1200 : availableSize.Width;
        return new Size(width, BuildLayout(width));
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
        BuildLayout(ActualWidth);
        var pastX = ShowsToday ? Math.Clamp(X(Now.TimeOfDay.TotalMinutes), 36, ActualWidth - 36) : 36;
        dc.DrawRectangle(BrushOf("#FAFBFD"), null, new Rect(36, 64, Math.Max(0, pastX - 36), Math.Max(0, ActualHeight - 76)));
        int first = (int)Math.Ceiling(ViewStart / GridMinutes) * GridMinutes;
        var pixelsPerGrid = PlotWidth / ViewSpan * GridMinutes;
        for (int t = first; t <= ViewStart + ViewSpan; t += GridMinutes)
        {
            var x = X(t);
            bool hour = t % 60 == 0;
            if (hour || pixelsPerGrid >= 6)
            {
                dc.DrawLine(new Pen(BrushOf(hour ? "#E4EAF1" : "#F1F4F8"), 1), new Point(x, 64), new Point(x, ActualHeight - 12));
                dc.DrawLine(new Pen(BrushOf(hour ? "#8190A5" : "#BFCAD6"), 1), new Point(x, hour ? 47 : 55), new Point(x, 64));
            }
            if (hour && (ViewSpan <= 900 || t % 120 == 0)) Text(dc, TimeMath.Format(t), x - 19, 23, 12, Ink);
        }
        dc.DrawLine(new Pen(BrushOf("#DDE5ED"), 1), new Point(22, 64), new Point(ActualWidth - 22, 64));
        var nowX = X(Now.TimeOfDay.TotalMinutes);
        if (ShowsToday && nowX >= 36 && nowX <= ActualWidth - 36)
        {
            var orange = BrushOf("#F17A45");
            dc.DrawLine(new Pen(orange, 1.3) { DashStyle = DashStyles.Dash }, new Point(nowX, 64), new Point(nowX, ActualHeight - 12));
            dc.DrawEllipse(orange, null, new Point(nowX, 64), 3.5, 3.5);
            var labelLeft = Math.Clamp(nowX - 39, 2, Math.Max(2, ActualWidth - 82));
            dc.DrawRoundedRectangle(orange, null, new Rect(labelLeft, 1, 80, 20), 4, 4);
            Text(dc, $"现在 {Now:HH:mm}", labelLeft + 9, 3, 10, Brushes.White);
        }
        if (hoverPosition is Point hover && IsMouseOver && drag != DragMode.Pan && hover.X >= 36 && hover.X <= ActualWidth - 36)
        {
            var hoverMinute = TimeMath.Snap(Minute(hover.X), GridMinutes);
            if (drag == DragMode.None || drag == DragMode.Create && !moved)
                hoverMinute = Math.Min(1440 - GridMinutes, hoverMinute);
            var hoverX = X(hoverMinute);
            if (hoverX >= 36 && hoverX <= ActualWidth - 36)
            {
                var guide = BrushOf("#52738F");
                dc.DrawLine(new Pen(guide, 1.3) { DashStyle = new DashStyle([4, 3], 0) },
                    new Point(hoverX, 64), new Point(hoverX, ActualHeight - 12));
                var labelLeft = Math.Clamp(hoverX - 29, 3, Math.Max(3, ActualWidth - 61));
                dc.DrawRoundedRectangle(guide, null, new Rect(labelLeft, 40, 58, 23), 4, 4);
                Text(dc, TimeMath.Format(hoverMinute), labelLeft + 10, 43, 12, Brushes.White);
            }
        }
        foreach (var entry in layout)
        {
            var item = entry.Item;
            var color = BrushOf(item.Color);
            var selected = item.Id == SelectedId;
            var card = entry.Card;
            double endX = Math.Clamp(entry.Anchor, card.Left + 20, card.Right - 20);
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new(entry.Anchor, entry.Bar.Bottom), false, false);
                ctx.PolyLineTo([new(entry.Anchor, card.Top - 16), new(endX, card.Top - 7), new(endX, card.Top)], true, false);
            }
            dc.DrawGeometry(null, new Pen(color, 1.2), geometry);
            if (item.IsPoint) dc.DrawEllipse(color, new Pen(Brushes.White, 2), new Point(entry.Anchor, entry.Bar.Top + 9), 8, 8);
            else
            {
                dc.DrawRoundedRectangle(color, null, entry.Bar, 6, 6);
                if (selected || IsMouseOver)
                {
                    if (item.Start >= ViewStart) dc.DrawRoundedRectangle(Brushes.White, new Pen(color, 1), new Rect(entry.Bar.Left - 3, entry.Bar.Top + 2, 7, 15), 3, 3);
                    if (item.End <= ViewStart + ViewSpan) dc.DrawRoundedRectangle(Brushes.White, new Pen(color, 1), new Rect(entry.Bar.Right - 4, entry.Bar.Top + 2, 7, 15), 3, 3);
                }
            }
            dc.DrawRoundedRectangle(BrushOf("#0A203047"), null, new Rect(card.X, card.Y + 3, card.Width, card.Height), 7, 7);
            dc.DrawRoundedRectangle(Brushes.White, new Pen(selected ? color : BrushOf("#DFE6ED"), selected ? 1.5 : 1), card, 7, 7);
            dc.DrawEllipse(color, null, new Point(endX, card.Top), 2.4, 2.4);
            Text(dc, item.Title, card.X + 10, card.Y + 12, 14, Ink, true, card.Width - 40, 23);
            Text(dc, item.TimeLabel, card.X + 10, card.Y + 46, 10.5, Muted, maxWidth: card.Width - 20, maxHeight: 26);
            Text(dc, "⋯", card.Right - 27, card.Y + 6, 19, Muted);
        }
        if (drag == DragMode.Create && moved)
        {
            var left = X(Math.Min(anchor, current));
            var right = X(Math.Max(anchor, current));
            var teal = BrushOf("#008D94");
            dc.DrawRoundedRectangle(BrushOf("#26009DA4"), new Pen(teal, 1.5) { DashStyle = DashStyles.Dash }, new Rect(left, 83, Math.Max(2, right - left), 35), 5, 5);
            var label = $"{TimeMath.Format(Math.Min(anchor, current))}—{TimeMath.Format(Math.Max(anchor, current))} · {Math.Abs(current - anchor)}分钟";
            var labelX = Math.Clamp(left, 8, Math.Max(8, ActualWidth - 240));
            dc.DrawRoundedRectangle(Brushes.White, new Pen(teal, 1), new Rect(labelX, 123, 222, 28), 5, 5);
            Text(dc, label, labelX + 9, 129, 12, teal);
        }
        if (Items.Count == 0 && drag != DragMode.Create)
        {
            Text(dc, "从一个时间点开始", ActualWidth / 2 - 90, 190, 20, Ink, true);
            Text(dc, "单击刻度记录一件事，或拖出一段专注时间", ActualWidth / 2 - 145, 228, 13, Muted);
        }
    }
    internal static SolidColorBrush BrushOf(string color) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); b.Freeze(); return b; }
    internal static void Text(DrawingContext dc, string text, double x, double y, double size, Brush color, bool bold = false, double maxWidth = 1000, double maxHeight = 100)
    {
        var ft = new FormattedText(text, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, color, 1)
        { MaxTextWidth = Math.Max(1, maxWidth), MaxTextHeight = maxHeight, Trimming = TextTrimming.CharacterEllipsis };
        dc.DrawText(ft, new Point(x, y));
    }
    private LayoutItem? Hit(Point p) => layout.LastOrDefault(l => l.Card.Contains(p) || new Rect(l.Bar.X - 6, l.Bar.Y - 6, l.Bar.Width + 12, l.Bar.Height + 12).Contains(p));
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        origin = e.GetPosition(this);
        if (e.ChangedButton == MouseButton.Middle)
        {
            drag = DragMode.Pan; originalView = ViewStart; CaptureMouse(); e.Handled = true; return;
        }
        var hit = Hit(origin);
        if (e.ChangedButton == MouseButton.Right)
        {
            if (hit != null) { SelectedId = hit.Item.Id; ShowMenu(hit.Item); Refresh(); }
            e.Handled = true; return;
        }
        if (e.ChangedButton != MouseButton.Left) return;
        SelectedId = hit?.Item.Id;
        if (hit != null && e.ClickCount == 2) { EditRequested?.Invoke(hit.Item); e.Handled = true; return; }
        if (hit != null && hit.Card.Contains(origin) && origin.X > hit.Card.Right - 32 && origin.Y < hit.Card.Top + 36)
        { ShowMenu(hit.Item); Refresh(); e.Handled = true; return; }
        moved = false;
        anchor = TimeMath.Snap(Minute(origin.X), GridMinutes);
        current = anchor;
        if (hit is null) { drag = DragMode.Create; dragItem = null; }
        else
        {
            dragItem = hit.Item.Copy(); preview = dragItem.Copy(); drag = DragMode.Move;
            if (!hit.Item.IsPoint && !hit.Card.Contains(origin))
            {
                if (Math.Abs(origin.X - hit.Bar.Left) < 9 && hit.Item.Start >= ViewStart) drag = DragMode.Start;
                else if (Math.Abs(origin.X - hit.Bar.Right) < 9 && hit.Item.End <= ViewStart + ViewSpan) drag = DragMode.End;
            }
        }
        CaptureMouse(); Refresh(); e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this);
        hoverPosition = p;
        InvalidateVisual();
        if (drag == DragMode.None)
        {
            var hit = Hit(p);
            Cursor = hit is null ? Cursors.Cross : !hit.Item.IsPoint && !hit.Card.Contains(p) && (Math.Abs(p.X - hit.Bar.Left) < 9 || Math.Abs(p.X - hit.Bar.Right) < 9) ? Cursors.SizeWE : Cursors.Hand;
            return;
        }
        if ((p - origin).Length > 4) moved = true;
        if (drag == DragMode.Pan) { SetView(originalView - (p.X - origin.X) / PlotWidth * ViewSpan, ViewSpan); return; }
        current = TimeMath.Snap(Minute(p.X), GridMinutes);
        if (preview != null && dragItem != null && moved)
        {
            if (drag == DragMode.Move)
            {
                var snapped = TimeMath.Snap(dragItem.Start + (p.X - origin.X) / PlotWidth * ViewSpan, GridMinutes);
                var result = TimeMath.Move(dragItem, snapped - dragItem.Start);
                preview.Start = result.Start; preview.End = result.End;
            }
            else if (drag == DragMode.Start) preview.Start = Math.Clamp(current, 0, dragItem.End!.Value - Math.Min(GridMinutes, dragItem.End.Value - dragItem.Start));
            else if (drag == DragMode.End) preview.End = Math.Clamp(current, dragItem.Start + Math.Min(GridMinutes, dragItem.End!.Value - dragItem.Start), 1440);
        }
        Refresh();
    }
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        hoverPosition = e.GetPosition(this);
        InvalidateVisual();
    }
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        hoverPosition = null;
        InvalidateVisual();
    }
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (drag == DragMode.None || (e.ChangedButton != MouseButton.Left && e.ChangedButton != MouseButton.Middle)) return;
        var mode = drag; var changed = preview; var original = dragItem; var didMove = moved;
        CancelDrag();
        if (mode == DragMode.Create)
        {
            if (didMove && current != anchor) CreateRequested?.Invoke(Math.Min(anchor, current), Math.Max(anchor, current));
            else CreateRequested?.Invoke(Math.Min(1440 - GridMinutes, anchor), null);
        }
        else if (mode != DragMode.Pan && didMove && changed != null && original != null && (changed.Start != original.Start || changed.End != original.End))
            TimeChanged?.Invoke(original, changed.Start, changed.End);
        e.Handled = true;
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); drag = DragMode.None; preview = null; dragItem = null; Refresh(); }
    private void CancelDrag() { drag = DragMode.None; preview = null; dragItem = null; ReleaseMouseCapture(); Refresh(); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) { CancelDrag(); e.Handled = true; }
        var selected = Items.FirstOrDefault(i => i.Id == SelectedId);
        if (selected != null && e.Key == Key.Delete) { DeleteRequested?.Invoke(selected); e.Handled = true; }
        if (selected != null && e.Key == Key.Enter) { EditRequested?.Invoke(selected); e.Handled = true; }
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            if (drag == DragMode.None)
            {
                var position = e.GetPosition(this);
                hoverPosition = position;
                var ratio = Math.Clamp((position.X - 36) / PlotWidth, 0, 1);
                var anchorMinute = ViewStart + ratio * ViewSpan;
                var span = Math.Clamp(ViewSpan * Math.Pow(0.8, e.Delta / 120.0), 120, 1440);
                SetView(anchorMinute - ratio * span, span);
            }
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { SetView(ViewStart - Math.Sign(e.Delta) * ViewSpan * 0.1, ViewSpan); e.Handled = true; }
        base.OnMouseWheel(e);
    }
    private void ShowMenu(ScheduleItem item)
    {
        var menu = new ContextMenu();
        var edit = new MenuItem { Header = "编辑日程", InputGestureText = "Enter" };
        edit.Click += (_, _) => EditRequested?.Invoke(item);
        var delete = new MenuItem { Header = "删除日程", InputGestureText = "Delete" };
        delete.Click += (_, _) => DeleteRequested?.Invoke(item);
        menu.Items.Add(edit); menu.Items.Add(new Separator()); menu.Items.Add(delete);
        menu.PlacementTarget = this; menu.IsOpen = true;
    }
}

public sealed class OverviewControl : FrameworkElement
{
    public TimelineControl? Timeline { get; set; }
    private int mode;
    private double origin;
    private double start;
    private double span;
    private double W => Math.Max(1, ActualWidth - 26);
    private double X(double minute) => 13 + minute / 1440 * W;
    public OverviewControl() { Cursor = Cursors.Hand; ClipToBounds = true; }
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        if (Timeline is not { } t) return;
        var rail = new Rect(13, 19, W, 24);
        dc.DrawRoundedRectangle(TimelineControl.BrushOf("#EAF0F5"), new Pen(TimelineControl.BrushOf("#D5DFE8"), 1), rail, 5, 5);
        var left = X(t.ViewStart); var right = X(t.ViewStart + t.ViewSpan);
        dc.DrawRoundedRectangle(TimelineControl.BrushOf("#22008D94"), null, new Rect(left, 19, right - left, 24), 4, 4);
        foreach (var item in t.Items)
        {
            var brush = TimelineControl.BrushOf(item.Color);
            if (item.IsPoint) dc.DrawEllipse(brush, null, new Point(X(item.Start), 31), 3, 3);
            else dc.DrawRoundedRectangle(brush, null, new Rect(X(item.Start), 28, Math.Max(2, X(item.End!.Value) - X(item.Start)), 6), 2, 2);
        }
        foreach (var x in new[] { left, right }) dc.DrawRoundedRectangle(Brushes.White, new Pen(TimelineControl.BrushOf("#699899"), 1), new Rect(x - 4, 19, 8, 24), 3, 3);
        var now = X(t.Now.TimeOfDay.TotalMinutes);
        if (t.ShowsToday) dc.DrawLine(new Pen(TimelineControl.BrushOf("#F17A45"), 1.5), new Point(now, 15), new Point(now, 48));
        for (int minute = 0; minute <= 1440; minute += 360)
            TimelineControl.Text(dc, TimeMath.Format(minute), Math.Clamp(X(minute) - 16, 0, Math.Max(0, ActualWidth - 34)), 51, 10, TimelineControl.BrushOf("#738297"));
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (Timeline is not { } t) return;
        origin = e.GetPosition(this).X; start = t.ViewStart; span = t.ViewSpan;
        mode = Math.Abs(origin - X(start)) <= 10 ? 2 : Math.Abs(origin - X(start + span)) <= 10 ? 3 : 1;
        if (mode == 1 && (origin < X(start) || origin > X(start + span))) { t.SetView((origin - 13) / W * 1440 - span / 2, span); start = t.ViewStart; }
        CaptureMouse(); e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (Timeline is not { } t || mode == 0) return;
        var delta = (e.GetPosition(this).X - origin) / W * 1440;
        if (mode == 1) t.SetView(start + delta, span);
        else if (mode == 2) { var next = Math.Clamp(start + delta, 0, start + span - 120); t.SetView(next, start + span - next); }
        else t.SetView(start, Math.Clamp(span + delta, 120, 1440 - start));
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { mode = 0; ReleaseMouseCapture(); e.Handled = true; }
    protected override void OnLostMouseCapture(MouseEventArgs e) { mode = 0; base.OnLostMouseCapture(e); }
}
