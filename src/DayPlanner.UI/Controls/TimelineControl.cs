using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using DayPlanner.Core;

namespace DayPlanner.UI.Controls;

public sealed class TimelineControl : Control
{
    public List<ScheduleItem> Items { get; set; } = [];
    public bool Compact { get; set; }
    public bool Highlight { get; set; }
    public bool Weekend { get; set; }
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
    private sealed record LayoutItem(ScheduleItem Item, Rect Bar, double Anchor, Rect Card);
    private static readonly IBrush Ink = BrushOf("#203047");
    private static readonly IBrush Muted = BrushOf("#738297");
    private double PlotWidth => Math.Max(1, Bounds.Width - 72);
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
        var occupied = new List<Rect>();
        double bottom = 0;
        foreach (var item in visible)
        {
            double left = Math.Max(36, Map(item.Start)), right = Math.Min(width - 36, Map(item.End ?? item.Start));
            var cardWidth = Math.Min(152, plot);
            var cardLeft = Math.Clamp(left - cardWidth / 2, 36, Math.Max(36, width - 36 - cardWidth));
            var extentLeft = item.IsPoint && !Compact ? Math.Min(cardLeft, left - 8) : left;
            var extentRight = item.IsPoint && !Compact ? Math.Max(cardLeft + cardWidth, left + 8) : left + (item.IsPoint ? Math.Min(144, Math.Max(6, width - 36 - left)) : Math.Max(6, right - left));
            var connectedPoint = item.IsPoint && !Compact;
            var top = Compact ? 10.0 : 92.0;
            Rect extent;
            while (true)
            {
                extent = new Rect(extentLeft - 4, top - 4, extentRight - extentLeft + 8, connectedPoint ? 134 : Compact ? 52 : 72);
                if (!occupied.Any(r => r.Intersects(extent))) break;
                top += Compact ? 60 : 80;
            }
            occupied.Add(extent);
            var bar = connectedPoint ? new Rect(left - 8, 92, 16, 18)
                : new Rect(left, top, extentRight - left, Compact ? 44 : 64);
            var card = connectedPoint ? new Rect(cardLeft, top + 48, cardWidth, 78) : default(Rect);
            layout.Add(new(item, bar, left, card));
            bottom = Math.Max(bottom, extent.Bottom);
        }
        return Compact ? Math.Max(64, bottom + 10) : Math.Max(400, bottom + 48);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 1200 : availableSize.Width;
        return new Size(width, BuildLayout(width));
    }
    public override void Render(DrawingContext dc)
    {
        base.Render(dc);
        dc.DrawRectangle(Compact ? BrushOf(Highlight ? "#EDF8F8" : Weekend ? "#F7F9FB" : "#FFFFFF") : Brushes.White, null, new Rect(Bounds.Size));
        BuildLayout(Bounds.Width);
        var pastX = ShowsToday ? Math.Clamp(X(Now.TimeOfDay.TotalMinutes), 36, Bounds.Width - 36) : 36;
        if (!Compact) dc.DrawRectangle(BrushOf("#FAFBFD"), null, new Rect(36, 64, Math.Max(0, pastX - 36), Math.Max(0, Bounds.Height - 76)));
        var axisTop = Compact ? 0 : 64;
        int first = (int)Math.Ceiling(ViewStart / GridMinutes) * GridMinutes;
        var pixelsPerGrid = PlotWidth / ViewSpan * GridMinutes;
        for (int t = first; t <= ViewStart + ViewSpan; t += GridMinutes)
        {
            var x = X(t);
            bool hour = t % 60 == 0;
            if (hour || pixelsPerGrid >= 6)
            {
                dc.DrawLine(new Pen(BrushOf(hour ? "#E4EAF1" : "#F1F4F8"), 1), new Point(x, axisTop), new Point(x, Bounds.Height - (Compact ? 0 : 12)));
                if (!Compact) dc.DrawLine(new Pen(BrushOf(hour ? "#8190A5" : "#BFCAD6"), 1), new Point(x, hour ? 47 : 55), new Point(x, 64));
            }
            if (!Compact && hour && (ViewSpan <= 900 || t % 120 == 0)) Text(dc, TimeMath.Format(t), x - 19, 23, 12, Ink);
        }
        if (!Compact) dc.DrawLine(new Pen(BrushOf("#DDE5ED"), 1), new Point(22, 64), new Point(Bounds.Width - 22, 64));
        var nowX = X(Now.TimeOfDay.TotalMinutes);
        if (ShowsToday && nowX >= 36 && nowX <= Bounds.Width - 36)
        {
            var orange = BrushOf("#F17A45");
            dc.DrawLine(new Pen(orange, 1.3) { DashStyle = DashStyle.Dash }, new Point(nowX, axisTop), new Point(nowX, Bounds.Height - (Compact ? 0 : 12)));
            if (!Compact) dc.DrawEllipse(orange, null, new Point(nowX, 64), 3.5, 3.5);
            var labelLeft = Math.Clamp(nowX - 39, 2, Math.Max(2, Bounds.Width - 82));
            if (!Compact) dc.DrawRectangle(orange, null, new Rect(labelLeft, 1, 80, 20), 4, 4);
            if (!Compact) Text(dc, $"现在 {Now:HH:mm}", labelLeft + 9, 3, 10, Brushes.White);
        }
        if (hoverPosition is Point hover && IsPointerOver && drag != DragMode.Pan && hover.X >= 36 && hover.X <= Bounds.Width - 36)
        {
            var hoverMinute = TimeMath.Snap(Minute(hover.X), GridMinutes);
            if (drag == DragMode.None || drag == DragMode.Create && !moved)
                hoverMinute = Math.Min(1440 - GridMinutes, hoverMinute);
            var hoverX = X(hoverMinute);
            if (hoverX >= 36 && hoverX <= Bounds.Width - 36)
            {
                var guide = BrushOf("#52738F");
                dc.DrawLine(new Pen(guide, 1.3) { DashStyle = new DashStyle([4, 3], 0) },
                    new Point(hoverX, axisTop), new Point(hoverX, Bounds.Height - (Compact ? 0 : 12)));
                var labelLeft = Math.Clamp(hoverX - 29, 3, Math.Max(3, Bounds.Width - 61));
                dc.DrawRectangle(guide, null, new Rect(labelLeft, Compact ? 0 : 40, 58, 23), 4, 4);
                Text(dc, TimeMath.Format(hoverMinute), labelLeft + 10, Compact ? 3 : 43, 12, Brushes.White);
            }
        }
        foreach (var entry in layout)
        {
            var item = entry.Item;
            var color = BrushOf(item.Color);
            var selected = item.Id == SelectedId;
            var block = entry.Bar;
            var foreground = Brushes.White;
            if (item.IsPoint && !Compact)
            {
                var card = entry.Card;
                var connectionX = Math.Clamp(entry.Anchor, card.Left + 16, card.Right - 16);
                var connection = new StreamGeometry();
                using (var context = connection.Open())
                {
                    context.BeginFigure(new Point(entry.Anchor, block.Bottom), false);
                    context.LineTo(new Point(entry.Anchor, card.Top - 16));
                    context.LineTo(new Point(connectionX, card.Top - 7));
                    context.LineTo(new Point(connectionX, card.Top));
                }
                dc.DrawGeometry(null, new Pen(color, 1.2), connection);
                dc.DrawEllipse(color, new Pen(Brushes.White, 2), new Point(entry.Anchor, block.Top + 9), 8, 8);
                dc.DrawRectangle(BrushOf("#0A203047"), null,
                    new Rect(card.X, card.Y + 3, card.Width, card.Height), 7, 7);
                dc.DrawRectangle(Brushes.White,
                    new Pen(selected ? color : BrushOf("#DFE6ED"), selected ? 1.5 : 1), card, 7, 7);
                dc.DrawEllipse(color, null, new Point(connectionX, card.Top), 2.4, 2.4);
                Text(dc, item.Title, card.X + 10, card.Y + 12, 14, Ink, true, card.Width - 40, 23);
                Text(dc, item.TimeLabel, card.X + 10, card.Y + 46, 10.5, Muted,
                    maxWidth: card.Width - 20, maxHeight: 26);
                Text(dc, "⋯", card.Right - 27, card.Y + 6, 19, Muted);
                continue;
            }
            if (item.IsPoint && Compact)
            {
                if (selected) dc.DrawRectangle(BrushOf("#EDF0F8"), new Pen(color, 1), block, 5, 5);
                dc.DrawEllipse(color, new Pen(Brushes.White, 1), new Point(entry.Anchor + 5, block.Top + 21), 5, 5);
                Text(dc, TimeMath.Format(item.Start) + " " + item.Title, block.Left + 16, block.Top + 12, 12, Ink,
                    maxWidth: Math.Max(1, block.Width - 20), maxHeight: 22);
                continue;
            }
            dc.DrawRectangle(color, selected ? new Pen(Ink, 2) : null, block, 6, 6);
            var padding = block.Width < 60 ? 4 : 10;
            var textLeft = block.Left + padding;
            if (block.Width >= 22)
            {
                if (block.Width < 90)
                    Text(dc, item.Title, textLeft, block.Top + 4, 13, foreground, true,
                        block.Right - padding - textLeft, block.Height - 8, centered: true);
                else
                    Text(dc, item.Title, textLeft, block.Top + (Compact ? 4 : 10), 13, foreground, true,
                        block.Right - padding - textLeft, 22);
                if (block.Width >= 90)
                {
                    var time = item.IsPoint ? TimeMath.Format(item.Start) + " · 时间点"
                        : TimeMath.Format(item.Start) + "—" + TimeMath.Format(item.End!.Value);
                    Text(dc, time, textLeft, block.Top + (Compact ? 25 : 38), 10.5, foreground,
                        maxWidth: block.Right - padding - textLeft, maxHeight: 18);
                }
            }
            var hovered = hoverPosition is Point p && block.Contains(p);
            if (!item.IsPoint && (selected || hovered))
            {
                var handlePen = new Pen(foreground, 2);
                if (item.Start >= ViewStart)
                    dc.DrawLine(handlePen, new Point(block.Left + 2, block.Top + (Compact ? 15 : 25)), new Point(block.Left + 2, block.Bottom - (Compact ? 15 : 25)));
                if (item.End <= ViewStart + ViewSpan)
                    dc.DrawLine(handlePen, new Point(block.Right - 2, block.Top + (Compact ? 15 : 25)), new Point(block.Right - 2, block.Bottom - (Compact ? 15 : 25)));
            }
        }

        if (drag == DragMode.Create && moved)
        {
            var left = X(Math.Min(anchor, current));
            var right = X(Math.Max(anchor, current));
            var teal = BrushOf("#008D94");
            var previewTop = Compact ? Math.Clamp(origin.Y - 20, 0, Math.Max(0, Bounds.Height - 46)) : Math.Max(83, origin.Y - 20);
            dc.DrawRectangle(BrushOf("#26009DA4"), new Pen(teal, 1.5) { DashStyle = DashStyle.Dash }, new Rect(left, previewTop, Math.Max(2, right - left), Compact ? 44 : 64), 5, 5);
            var label = $"{TimeMath.Format(Math.Min(anchor, current))}—{TimeMath.Format(Math.Max(anchor, current))} · {Math.Abs(current - anchor)}分钟";
            var labelX = Math.Clamp(left, 8, Math.Max(8, Bounds.Width - 240));
            dc.DrawRectangle(Brushes.White, new Pen(teal, 1), new Rect(labelX, previewTop + (Compact ? 8 : 72), 222, 28), 5, 5);
            Text(dc, label, labelX + 9, previewTop + (Compact ? 14 : 78), 12, teal);
        }
        if (!Compact && Items.Count == 0 && drag != DragMode.Create)
        {
            Text(dc, "从一个时间点开始", Bounds.Width / 2 - 90, 190, 20, Ink, true);
            Text(dc, "单击刻度记录一件事，或拖出一段专注时间", Bounds.Width / 2 - 145, 228, 13, Muted);
        }
    }
    internal static SolidColorBrush BrushOf(string color) => new(Color.Parse(color));
    internal static double Text(DrawingContext dc, string text, double x, double y, double size, IBrush color, bool bold = false, double maxWidth = 1000, double maxHeight = 100, bool centered = false)
    {
        using var textLayout = new TextLayout(text,
            new Typeface(Typography.Body, FontStyle.Normal, bold ? FontWeight.SemiBold : FontWeight.Normal), size, color,
            textAlignment: centered ? TextAlignment.Center : TextAlignment.Left,
            textWrapping: TextWrapping.Wrap, textTrimming: TextTrimming.CharacterEllipsis,
            maxWidth: Math.Max(1, maxWidth), maxHeight: maxHeight);
        textLayout.Draw(dc, new Point(x, centered ? y + Math.Max(0, (maxHeight - textLayout.Height) / 2) : y));
        return textLayout.Height;
    }
    private static double ResizeGrip(Rect block) => Math.Min(8, block.Width / 4);
    private LayoutItem? Hit(Point p) => layout.LastOrDefault(l => l.Bar.Contains(p) || l.Card.Contains(p));
    private IPointer? captured;
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus(); origin = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsMiddleButtonPressed) { drag = DragMode.Pan; originalView = ViewStart; Capture(e.Pointer); e.Handled = true; return; }
        var hit = Hit(origin);
        if (properties.IsRightButtonPressed) { if (hit != null) { SelectedId = hit.Item.Id; ShowMenu(hit.Item); Refresh(); } e.Handled = true; return; }
        SelectedId = hit?.Item.Id;
        if (hit != null && e.ClickCount == 2) { EditRequested?.Invoke(hit.Item); e.Handled = true; return; }
        if (hit != null && hit.Item.IsPoint && hit.Card.Contains(origin) && origin.X > hit.Card.Right - 32 && origin.Y < hit.Card.Top + 36)
        { ShowMenu(hit.Item); Refresh(); e.Handled = true; return; }
        moved = false; anchor = TimeMath.Snap(Minute(origin.X), GridMinutes); current = anchor;
        if (hit == null) { drag = DragMode.Create; dragItem = null; }
        else {
            dragItem = hit.Item.Copy(); preview = dragItem.Copy(); drag = DragMode.Move;
            if (!hit.Item.IsPoint) {
                if (Math.Abs(origin.X - hit.Bar.Left) < ResizeGrip(hit.Bar) && hit.Item.Start >= ViewStart) drag = DragMode.Start;
                else if (Math.Abs(origin.X - hit.Bar.Right) < ResizeGrip(hit.Bar) && hit.Item.End <= ViewStart + ViewSpan) drag = DragMode.End;
            }
        }
        Capture(e.Pointer); Refresh(); e.Handled = true;
    }
    private void Capture(IPointer pointer) { captured = pointer; pointer.Capture(this); }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); var p = e.GetPosition(this); hoverPosition = p; InvalidateVisual();
        if (drag == DragMode.None) {
            var hit = Hit(p);
            Cursor = new Cursor(hit == null ? StandardCursorType.Cross : !hit.Item.IsPoint && (Math.Abs(p.X-hit.Bar.Left)<ResizeGrip(hit.Bar) || Math.Abs(p.X-hit.Bar.Right)<ResizeGrip(hit.Bar)) ? StandardCursorType.SizeWestEast : StandardCursorType.Hand);
            return;
        }
        if (Math.Abs(p.X-origin.X) + Math.Abs(p.Y-origin.Y) > 4) moved = true;
        if (drag == DragMode.Pan) { SetView(originalView - (p.X-origin.X)/PlotWidth*ViewSpan, ViewSpan); return; }
        current = TimeMath.Snap(Minute(p.X), GridMinutes);
        if (preview != null && dragItem != null && moved) {
            if (drag == DragMode.Move) {
                var snapped = TimeMath.Snap(dragItem.Start+(p.X-origin.X)/PlotWidth*ViewSpan,GridMinutes);
                var result = TimeMath.Move(dragItem,snapped-dragItem.Start); preview.Start=result.Start; preview.End=result.End;
            }
            else if (drag == DragMode.Start) preview.Start=Math.Clamp(current,0,dragItem.End!.Value-Math.Min(GridMinutes,dragItem.End.Value-dragItem.Start));
            else if (drag == DragMode.End) preview.End=Math.Clamp(current,dragItem.Start+Math.Min(GridMinutes,dragItem.End!.Value-dragItem.Start),1440);
        }
        Refresh();
    }
    protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); hoverPosition=null; InvalidateVisual(); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); if (drag==DragMode.None) return;
        var mode=drag; var changed=preview; var original=dragItem; var didMove=moved; CancelDrag();
        if (mode==DragMode.Create) {
            if (didMove && current!=anchor) CreateRequested?.Invoke(Math.Min(anchor,current),Math.Max(anchor,current));
            else CreateRequested?.Invoke(Math.Min(1440-GridMinutes,anchor),null);
        } else if (mode!=DragMode.Pan && didMove && changed!=null && original!=null && (changed.Start!=original.Start || changed.End!=original.End))
            TimeChanged?.Invoke(original,changed.Start,changed.End);
        e.Handled=true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { base.OnPointerCaptureLost(e); drag=DragMode.None; preview=null; dragItem=null; Refresh(); }
    private void CancelDrag() { drag=DragMode.None; preview=null; dragItem=null; captured?.Capture(null); captured=null; Refresh(); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); if(e.Key==Key.Escape) { CancelDrag(); e.Handled=true; }
        var selected=Items.FirstOrDefault(i=>i.Id==SelectedId);
        if(selected!=null && e.Key==Key.Delete) { DeleteRequested?.Invoke(selected); e.Handled=true; }
        if(selected!=null && e.Key==Key.Enter) { EditRequested?.Invoke(selected); e.Handled=true; }
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if(e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
            e.Handled=true;
            if(drag==DragMode.None) { var p=e.GetPosition(this); var ratio=Math.Clamp((p.X-36)/PlotWidth,0,1); var minute=ViewStart+ratio*ViewSpan;
                var span=Math.Clamp(ViewSpan*Math.Pow(0.8,e.Delta.Y),120,1440); SetView(minute-ratio*span,span); }
        } else if(e.KeyModifiers.HasFlag(KeyModifiers.Shift)) { SetView(ViewStart-Math.Sign(e.Delta.Y)*ViewSpan*0.1,ViewSpan); e.Handled=true; }
        base.OnPointerWheelChanged(e);
    }
    private void ShowMenu(ScheduleItem item)
    {
        var edit=new MenuItem { Header="编辑日程" }; edit.Click+=(_,_)=>EditRequested?.Invoke(item);
        var delete=new MenuItem { Header="删除日程" }; delete.Click+=(_,_)=>DeleteRequested?.Invoke(item);
        var menu=new ContextMenu { ItemsSource=new object[] {edit,new Separator(),delete} }; menu.Open(this);
    }

}

public sealed class OverviewControl : Control
{
    public TimelineControl? Timeline { get; set; }
    public IReadOnlyList<ScheduleItem>? DisplayItems { get; set; }
    private int mode;
    private double origin;
    private double start;
    private double span;
    private double W => Math.Max(1, Bounds.Width - 26);
    private double X(double minute) => 13 + minute / 1440 * W;
    public OverviewControl() { Cursor = new Cursor(StandardCursorType.Hand); ClipToBounds = true; }
    public override void Render(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        if (Timeline is not { } t) return;
        var rail = new Rect(13, 19, W, 24);
        dc.DrawRectangle(TimelineControl.BrushOf("#EAF0F5"), new Pen(TimelineControl.BrushOf("#D5DFE8"), 1), rail, 5, 5);
        var left = X(t.ViewStart); var right = X(t.ViewStart + t.ViewSpan);
        dc.DrawRectangle(TimelineControl.BrushOf("#22008D94"), null, new Rect(left, 19, right - left, 24), 4, 4);
        foreach (var item in DisplayItems ?? t.Items)
        {
            var brush = TimelineControl.BrushOf(item.Color);
            if (item.IsPoint) dc.DrawEllipse(brush, null, new Point(X(item.Start), 31), 3, 3);
            else dc.DrawRectangle(brush, null, new Rect(X(item.Start), 28, Math.Max(2, X(item.End!.Value) - X(item.Start)), 6), 2, 2);
        }
        foreach (var x in new[] { left, right }) dc.DrawRectangle(Brushes.White, new Pen(TimelineControl.BrushOf("#699899"), 1), new Rect(x - 4, 19, 8, 24), 3, 3);
        var now = X(t.Now.TimeOfDay.TotalMinutes);
        if (t.ShowsToday) dc.DrawLine(new Pen(TimelineControl.BrushOf("#F17A45"), 1.5), new Point(now, 15), new Point(now, 48));
        for (int minute = 0; minute <= 1440; minute += 360)
            TimelineControl.Text(dc, TimeMath.Format(minute), Math.Clamp(X(minute) - 16, 0, Math.Max(0, Bounds.Width - 34)), 51, 10, TimelineControl.BrushOf("#738297"));
    }
    private IPointer? captured;
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if(Timeline is not {} t) return;
        origin=e.GetPosition(this).X; start=t.ViewStart; span=t.ViewSpan;
        mode=Math.Abs(origin-X(start))<=10?2:Math.Abs(origin-X(start+span))<=10?3:1;
        if(mode==1 && (origin<X(start)||origin>X(start+span))) {t.SetView((origin-13)/W*1440-span/2,span);start=t.ViewStart;}
        captured=e.Pointer; e.Pointer.Capture(this); e.Handled=true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if(Timeline is not {} t || mode==0) return;
        var delta=(e.GetPosition(this).X-origin)/W*1440;
        if(mode==1)t.SetView(start+delta,span);
        else if(mode==2){var next=Math.Clamp(start+delta,0,start+span-120);t.SetView(next,start+span-next);}
        else t.SetView(start,Math.Clamp(span+delta,120,1440-start));
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e){mode=0;captured?.Capture(null);captured=null;e.Handled=true;}
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e){mode=0;base.OnPointerCaptureLost(e);}
}
