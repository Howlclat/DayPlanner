using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DayPlanner.Core;
using DayPlanner.UI.Controls;

namespace DayPlanner.UI.Views;

public sealed partial class PlannerShell : UserControl
{
    private readonly PlannerSession session;
    private readonly bool mobile;
    private readonly Grid root = new();
    private readonly TextBlock title = Label("", 24, true), clock = Label("", 22, true), status = Label("", 12);
    private readonly TextBlock subtitle = Label("", 12), count = Label("", 12);
    private readonly TextBlock detailsTitle = Label("", 19, true), detailsCount = Label("", 12);
    private Button? periodButton;
    private Grid? overviewPanel;
    private readonly Grid body = new();
    private readonly TimelineControl timeline = new();
    private readonly WeekPlannerView week = new();
    private readonly MonthPlannerView month = new();
    private readonly OverviewControl overview = new();
    private readonly StackPanel details = new() { Spacing = 10 };
    private readonly StackPanel phoneContent = new() { Spacing = 16 };
    private readonly ScrollViewer? phoneScroll;
    private readonly MobileTimelineView? phoneTimeline;
    private readonly List<Button> modes = [];
    private readonly Button undo, redo;
    private readonly DispatcherTimer timer;
    private Control? overlay;
    private readonly ScrollViewer dayScroll;
    private readonly ScrollViewer monthScroll;
    private readonly Border sidePanel;
    private DateTime lastToday = DateTime.Today;
    public static Action<string, string, DateTime>? Notify { get; set; }
    public PlannerShell(PlannerSession session, bool mobile)
    {
        this.session = session; this.mobile = mobile;
        FontFamily = Typography.Body; FontSize = 13; Foreground = Brush("#203047");
        UseLayoutRounding = true;
        Background = mobile ? MobileUi.Canvas : Brush("#F4F7FA");
        var page = new Grid { RowDefinitions = new("Auto,*,Auto,Auto"), Margin = mobile ? new Thickness(16, 8, 16, 8) : new Thickness(22, 18, 22, 12) };
        root.Children.Add(page); Content = root;
        page.Children.Add(BuildHeader());
        Grid.SetRow(body, 1); page.Children.Add(body);
        undo = Button("↶", session.Undo); redo = Button("↷", session.Redo);
        overview.Timeline = timeline;
        timeline.SetView(session.Data.DefaultViewStart, session.Data.DefaultViewEnd - session.Data.DefaultViewStart);
        timeline.CreateRequested += New;
        timeline.EditRequested += item => Edit(item);
        timeline.DeleteRequested += item => session.Delete(item);
        timeline.TimeChanged += session.ChangeTime;
        timeline.ViewChanged += () => { overview.InvalidateVisual(); week.SetView(timeline.ViewStart, timeline.ViewSpan); };
        week.ViewChanged += (start, span) => timeline.SetView(start, span);
        week.DateSelected += date => session.Select(date);
        week.DayRequested += OpenDay;
        week.CreateRequested += (date, start, end) => { session.Select(date); New(start, end); };
        week.EditRequested += (date, item) => { session.Select(date); Edit(item); };
        week.DeleteRequested += (date, item) => { session.Select(date); session.Delete(item); };
        week.TimeChanged += (date, item, start, end) => { session.Select(date); session.ChangeTime(item, start, end); };
        month.DateSelected += date => session.Select(date);
        month.DayRequested += OpenDay;
        month.EditRequested += (date, item) => { session.Select(date); Edit(item); };
        dayScroll = Scroll(timeline); monthScroll = Scroll(month);
        var sideContent = new Grid { RowDefinitions = new("Auto,*,Auto") };
        var sideHeading = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 16) };
        detailsCount.Foreground = Brush("#8491A2");
        sideHeading.Children.Add(detailsTitle); sideHeading.Children.Add(detailsCount); sideContent.Children.Add(sideHeading);
        var sideItems = Scroll(details); Grid.SetRow(sideItems, 1); sideContent.Children.Add(sideItems);
        var sideActions = new StackPanel { Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        sideActions.Children.Add(Button("＋添加安排", () => New(540, null), true));
        sideActions.Children.Add(Button("打开日视图", () => OpenDay(session.Date)));
        Grid.SetRow(sideActions, 2); sideContent.Children.Add(sideActions);
        sidePanel = Panel(sideContent); sidePanel.Padding = new Thickness(16);
        if (mobile)
        {
            phoneScroll = Scroll(phoneContent); body.Children.Add(phoneScroll);
            phoneTimeline = new MobileTimelineView(); body.Children.Add(phoneTimeline);
            phoneTimeline.CreateRequested += (date, start, end) => { session.Select(date); New(start, end); };
            phoneTimeline.EditRequested += (date, item) => { session.Select(date); Edit(item); };
            phoneTimeline.DayRequested += OpenDay;
            var actions = BuildPhoneToolbar();
            Grid.SetRow(actions, 2); page.Children.Add(actions);
        }
        else
        {
            body.ColumnDefinitions = new("*,284");
            var center = new Grid { RowDefinitions = new("48,*") };
            var tools = new Grid { ColumnDefinitions = new("*,Auto,Auto,Auto"), Margin = new Thickness(18, 0), VerticalAlignment = VerticalAlignment.Center };
            var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, VerticalAlignment = VerticalAlignment.Center };
            count.Foreground = Brush("#66758B"); legend.Children.Add(count);
            var pointLegend = Label("● 时间点", 12); pointLegend.Foreground = Brush("#7564F4"); legend.Children.Add(pointLegend);
            var spanLegend = Label("━ 时间段", 12); spanLegend.Foreground = Brush("#008D94"); legend.Children.Add(spanLegend);
            tools.Children.Add(legend);
            foreach (var action in new[] { undo, redo }) { action.MinHeight = 30; action.Width = 32; action.Padding = new Thickness(0); }
            undo.Margin = new Thickness(0, 0, 5, 0); redo.Margin = new Thickness(0, 0, 12, 0);
            Place(tools, undo, 1); Place(tools, redo, 2); Place(tools, Button("＋新建", () => New(540, null), true), 3);
            center.Children.Add(tools);
            foreach (var view in new Control[] { dayScroll, week, monthScroll }) { Grid.SetRow(view, 1); center.Children.Add(view); }
            body.Children.Add(Panel(center));
            sidePanel.Margin = new Thickness(14, 0, 0, 0); Place(body, sidePanel, 1);
            overviewPanel = new Grid { ColumnDefinitions = new("60,*"), Height = 78, Margin = new Thickness(4, 12, 4, 4) };
            overviewPanel.Children.Add(Label("全天", 13, true)); Place(overviewPanel, overview, 1);
            Grid.SetRow(overviewPanel, 2); page.Children.Add(overviewPanel);
            var footer = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new Thickness(0, 12, 0, 0) };
            var hint = Label("单击创建事件 · 拖动创建日程 · 双击日程块编辑 · 右键更多操作 · Ctrl+滚轮缩放 · Shift+滚轮平移", 11);
            hint.Foreground = Brush("#8491A2"); footer.Children.Add(hint);
            status.Foreground = Brush("#50969A"); status.FontSize = 11;
            Place(footer, status, 1); Grid.SetRow(footer, 3); page.Children.Add(footer);
        }
        session.Changed += Refresh;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && TryDismiss()) { e.Handled = true; return; }
            if (overlay != null) return;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (e.Key == Key.N) New(540, null);
                if (e.Key == Key.Z) session.Undo();
                if (e.Key == Key.Y) session.Redo();
                if (e.Key == Key.S) { session.Save(); Refresh(); }
            }
        };
        timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Tick());
        AttachedToVisualTree += (_, _) => timer.Start();
        DetachedFromVisualTree += (_, _) => timer.Stop();
        Refresh(); Tick();
        if (session.LoadError != null) ShowMessage("读取日程失败", session.Status + "\n" + session.Store.FilePath);
    }
    private Control BuildHeader()
    {
        if (mobile) return BuildPhoneHeader();
        var head = new Grid { ColumnDefinitions = new(mobile ? "*,Auto" : "*,Auto,*"), Margin = new Thickness(mobile ? 0 : 3, 0, mobile ? 0 : 3, 22) };
        var date = new StackPanel { Spacing = mobile ? 8 : 5, VerticalAlignment = VerticalAlignment.Center };
        var dateRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        title.FontSize = mobile ? 24 : 22; dateRow.Children.Add(title);
        if (!mobile)
        {
            var calendar = Button("", ChooseDate); calendar.Width = 38; calendar.Height = 38; calendar.Padding = new Thickness(9);
            calendar.Content = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M5,5 L19,5 Q21,5 21,7 L21,20 Q21,22 19,22 L5,22 Q3,22 3,20 L3,7 Q3,5 5,5 M3,10 L21,10 M8,2 L8,7 M16,2 L16,7 M7,14 L10,14 M14,14 L17,14 M7,18 L10,18 M14,18 L17,18"), Stroke = Brush("#34465D"), StrokeThickness = 1.6, Width = 18, Height = 18, Stretch = Stretch.Uniform };
            Avalonia.Automation.AutomationProperties.SetName(calendar, "选择日期"); dateRow.Children.Add(calendar);
        }
        date.Children.Add(dateRow);
        var nav = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        Button NavigateButton(string text, Action action)
        {
            var button = Button(text, action, touch: mobile);
            if (!mobile) { button.MinHeight = 22; button.Height = 22; button.Padding = new Thickness(9, 0); button.CornerRadius = new CornerRadius(4); button.FontSize = 12; }
            return button;
        }
        nav.Children.Add(NavigateButton("‹", () => session.Navigate(-1)));
        periodButton = NavigateButton("今天", () => session.Select(DateTime.Today)); nav.Children.Add(periodButton);
        nav.Children.Add(NavigateButton("›", () => session.Navigate(1)));
        if (mobile) nav.Children.Add(Button("日历", ChooseDate, touch: true));
        else { subtitle.Margin = new Thickness(6, 0, 0, 0); subtitle.Foreground = Brush("#8491A2"); nav.Children.Add(subtitle); }
        date.Children.Add(nav);
        if (mobile) { clock.FontSize = 12; date.Children.Add(clock); }
        head.Children.Add(date);
        if (!mobile)
        {
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            AddModes(tabs.Children, false); Place(head, tabs, 1);
        }
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = mobile ? VerticalAlignment.Top : VerticalAlignment.Center };
        if (!mobile)
        {
            var clockPanel = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            var caption = Label("当前时间", 11); caption.Foreground = Brush("#7F8B9D"); clockPanel.Children.Add(caption);
            clock.FontSize = 29; clock.FontWeight = FontWeight.SemiBold; clock.FontFamily = Typography.Clock;
            clockPanel.Children.Add(clock); right.Children.Add(clockPanel);
        }
        var settings = Button("设置", Settings, touch: mobile); settings.VerticalAlignment = VerticalAlignment.Center; right.Children.Add(settings);
        Place(head, right, mobile ? 1 : 2); return head;
    }
    private void AddModes(Avalonia.Controls.Controls controls, bool phone)
    {
        foreach (var mode in Enum.GetValues<PlannerMode>())
        {
            var label = mode == PlannerMode.Day ? "日" : mode == PlannerMode.Week ? "周" : "月";
            var button = Button(label, () => { session.Mode = mode; Refresh(); }, touch: phone);
            if (phone) { button.MinHeight = 36; button.Height = 36; button.Padding = new Thickness(0); button.BorderThickness = new Thickness(0); button.CornerRadius = new CornerRadius(8); }
            if (!phone) { button.Height = 36; button.Padding = new Thickness(20, 0); }
            button.Tag = mode; button.MinWidth = phone ? 0 : 56; modes.Add(button); controls.Add(button);
        }
    }
    private void Refresh()
    {
        var date = session.Date;
        var start = PlanningDates.WeekStart(date);
        title.Text = session.Mode == PlannerMode.Month ? $"{date:yyyy年M月}" : session.Mode == PlannerMode.Week
            ? mobile ? $"{start:M月d日}—{start.AddDays(6):M月d日}" : $"{start:yyyy年M月d日}—{start.AddDays(6):M月d日}" : $"{date:yyyy年M月d日}";
        subtitle.Text = date.ToString("dddd", CultureInfo.GetCultureInfo("zh-CN")) + (date == DateTime.Today ? " · 今天" : "");
        if (periodButton != null) periodButton.Content = session.Mode == PlannerMode.Day ? "今天" : session.Mode == PlannerMode.Week ? "本周" : "本月";
        foreach (var button in modes)
        {
            var active = (PlannerMode)button.Tag! == session.Mode;
            button.Background = mobile ? active ? Brushes.White : Brushes.Transparent : active ? Brush("#008D94") : Brushes.White;
            button.Foreground = mobile ? active ? Brush("#203047") : MobileUi.Muted : active ? Brushes.White : Brush("#203047");
        }
        status.Text = session.Status;
        undo.IsEnabled = session.History.CanUndo; redo.IsEnabled = session.History.CanRedo;
        if (mobile) RefreshPhone();
        else
        {
            timeline.Items = session.Items.ToList(); timeline.SelectedDate = date; timeline.GridMinutes = session.Data.GridMinutes; timeline.Refresh();
            week.Update(session.Data, date, timeline.ViewStart, timeline.ViewSpan);
            month.Update(session.Data, date);
            dayScroll.IsVisible = session.Mode == PlannerMode.Day; week.IsVisible = session.Mode == PlannerMode.Week; monthScroll.IsVisible = session.Mode == PlannerMode.Month;
            sidePanel.IsVisible = session.Mode != PlannerMode.Day;
            body.ColumnDefinitions[1].Width = new GridLength(sidePanel.IsVisible ? 284 : 0);
            if (overviewPanel != null) overviewPanel.IsVisible = session.Mode != PlannerMode.Month;
            overview.DisplayItems = session.Mode == PlannerMode.Week ? Enumerable.Range(0, 7).SelectMany(i => PlanningDates.Items(session.Data, start.AddDays(i))).ToList() : null;
            overview.InvalidateVisual();
            var total = session.Mode == PlannerMode.Week ? Enumerable.Range(0, 7).Sum(i => PlanningDates.Items(session.Data, start.AddDays(i)).Count)
                : session.Mode == PlannerMode.Month ? Enumerable.Range(1, DateTime.DaysInMonth(date.Year, date.Month)).Sum(day => PlanningDates.Items(session.Data, new(date.Year, date.Month, day)).Count) : session.Items.Count;
            count.Text = (session.Mode == PlannerMode.Day ? "" : session.Mode == PlannerMode.Week ? "本周 · " : "本月 · ") + $"{total}项安排";
            detailsTitle.Text = date.ToString("M月d日 ddd", CultureInfo.GetCultureInfo("zh-CN"));
            detailsCount.Text = $"{session.Items.Count}项安排" + (date == DateTime.Today ? " · 今天" : "");
            details.Children.Clear();
            AddCards(details, date, false);
        }
    }
    private void RefreshPhone()
    {
        RefreshPhoneHeader();
        var isMonth = session.Mode == PlannerMode.Month;
        phoneScroll!.IsVisible = isMonth; phoneTimeline!.IsVisible = !isMonth;
        if (!isMonth)
        {
            phoneTimeline.Update(session.Data, session.Date, session.Mode == PlannerMode.Week);
            return;
        }
        phoneContent.Children.Clear();
        phoneContent.Children.Add(BuildPhoneCalendar());
        phoneContent.Children.Add(BuildPhoneAgenda());
        if (session.HasUnsavedChanges || session.LoadError != null) phoneContent.Children.Add(Label(session.Status, 12));
    }
    private Control BuildPhoneCalendar()
    {
        var grid = new UniformGrid { Columns = 7, Rows = PlanningDates.MonthRows(session.Date) + 1 };
        foreach (var name in new[] { "一", "二", "三", "四", "五", "六", "日" }) grid.Children.Add(new TextBlock { Text = name, Foreground = MobileUi.Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        var start = PlanningDates.WeekStart(PlanningDates.MonthStart(session.Date));
        for (var i = 0; i < PlanningDates.MonthRows(session.Date) * 7; i++)
        {
            var date = start.AddDays(i);
            grid.Children.Add(PhoneDateButton(date, false));
        }
        grid.Height = (PlanningDates.MonthRows(session.Date) + 1) * 44;
        return MobileUi.Group(grid, 4);
    }
    private void AddCards(StackPanel panel, DateTime date, bool phone)
    {
        var items = PlanningDates.Items(session.Data, date).OrderBy(item => item.Start).ToList();
        if (items.Count == 0)
        {
            panel.Children.Add(Button("暂无安排，点击添加", () => { session.Select(date); New(540, null); }, touch: phone)); return;
        }
        foreach (var item in items)
        {
            var text = new StackPanel { Spacing = 8 };
            text.Children.Add(new TextBlock { Text = item.Title, FontSize = phone ? 18 : 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            var time = Label(item.TimeLabel, phone ? 14 : 11); time.Foreground = Brush("#738297"); text.Children.Add(time);
            if (item.ReminderEnabled) text.Children.Add(Label($"提前{item.ReminderMinutes}分钟提醒", 12));
            var button = Button("", () => { session.Select(date); Edit(item); }, touch: phone);
            button.Content = text; button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Background = Brushes.White; button.Padding = new Thickness(phone ? 16 : 12);
            var delete = new MenuItem { Header = "删除安排" }; delete.Click += (_, _) => { session.Select(date); session.Delete(item); };
            button.ContextMenu = new ContextMenu { ItemsSource = new[] { delete } };
            panel.Children.Add(new Border { BorderBrush = Brush(item.Color), BorderThickness = new Thickness(4, 0, 0, 0), Child = button, CornerRadius = new CornerRadius(8) });
        }
    }
    private void OpenDay(DateTime date) { session.Mode = PlannerMode.Day; session.Select(date); }
    private void New(int start, int? end) => Edit(new ScheduleItem { Start = start, End = end, Color = end == null ? "#7564F4" : "#009DA4" }, true);
    private void Edit(ScheduleItem item, bool isNew = false)
    {
        if (session.LoadError != null) { ShowMessage("数据读取失败", session.Status); return; }
        if (mobile)
        {
            var page = new MobileEventPage(item, isNew, session); page.Completed += () => DismissOverlay(); PresentPage(page); return;
        }
        var editor = new EventForm(item, isNew, session, mobile);
        editor.Completed += () => TryDismiss();
        if (mobile) PresentPage(editor); else Present(editor);
    }
    private void ChooseDate()
    {
        var calendar = new Avalonia.Controls.Calendar { SelectedDate = session.Date, DisplayDate = session.Date, FirstDayOfWeek = DayOfWeek.Monday, HorizontalAlignment = HorizontalAlignment.Center };
        var stack = new StackPanel { Spacing = 16 };
        stack.Children.Add(Label("选择日期", 22, true)); stack.Children.Add(calendar);
        stack.Children.Add(Button("确定", () => { if (calendar.SelectedDate is DateTime date) session.Select(date); TryDismiss(); }, true, mobile));
        stack.Children.Add(Button("取消", () => TryDismiss(), touch: mobile)); Present(stack);
    }
    private void Settings()
    {
        if (mobile)
        {
            var page = new MobileSettingsPage(session);
            page.Cancelled += () => DismissOverlay();
            page.Saved += () => { timeline.SetView(session.Data.DefaultViewStart, session.Data.DefaultViewEnd - session.Data.DefaultViewStart); DismissOverlay(); Refresh(); };
            PresentPage(page); return;
        }
        var form = new SettingsForm(session, mobile, () => { timeline.SetView(session.Data.DefaultViewStart, session.Data.DefaultViewEnd - session.Data.DefaultViewStart); TryDismiss(); Refresh(); });
        form.Cancelled += () => TryDismiss(); Present(form);
    }
    public bool TryDismiss()
    {
        if (overlay is Border { Child: MobileEventPage page } && page.TryDismissTransient()) return true;
        return DismissOverlay();
    }
    private bool DismissOverlay()
    {
        if (overlay == null) return false;
        root.Children.Remove(overlay); overlay = null; root.Children[0].IsVisible = true; return true;
    }
    private void PresentPage(Control content)
    {
        DismissOverlay(); root.Children[0].IsVisible = false;
        overlay = new Border { Background = MobileUi.Canvas, Padding = new Thickness(16, 8, 16, 12), Child = content };
        root.Children.Add(overlay);
    }
    private void Present(Control content)
    {
        TryDismiss();
        var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(16), Padding = new Thickness(mobile ? 20 : 28),
            Margin = new Thickness(mobile ? 8 : 24), MaxWidth = 480, MaxHeight = mobile ? double.PositiveInfinity : 760,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = mobile ? VerticalAlignment.Stretch : VerticalAlignment.Center,
            Child = Scroll(content) };
        var shade = new Grid { Background = Brush("#80203047") }; shade.Children.Add(card);
        overlay = shade; root.Children.Add(shade);
    }
    public void ShowMessage(string heading, string message)
    {
        var stack = new StackPanel { Spacing = 16 }; stack.Children.Add(Label(heading, 20, true));
        stack.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(Button("知道了", () => TryDismiss(), true, mobile)); Present(stack);
    }
    private void Tick()
    {
        var now = DateTime.Now;
        clock.Text = mobile ? now.ToString("dddd · HH:mm", CultureInfo.GetCultureInfo("zh-CN")) : now.ToString("HH:mm:ss");
        timeline.Now = now; timeline.InvalidateVisual(); week.Tick(now); overview.InvalidateVisual();
        phoneTimeline?.Tick(now);
        if (now.Date != lastToday) { lastToday = now.Date; Refresh(); }
        if (App.Reminders != null || session.LoadError != null) return;
        var due = session.Due(now).ToList();
        foreach (var entry in due)
        {
            if (Notify != null) Notify("日程即将开始", $"{entry.Item.Title} · {entry.Item.TimeLabel}", entry.Date);
            else status.Text = $"即将开始：{entry.Item.Title}";
            session.Data.ReminderReceipts.Add(entry.Key);
        }
        if (due.Count > 0) session.Save();
    }
    public static SolidColorBrush Brush(string color) => new(Color.Parse(color));
    public static TextBlock Label(string text, double size = 14, bool bold = false) => new() { Text = text, FontSize = size, FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal, Foreground = Brush("#203047"), VerticalAlignment = VerticalAlignment.Center };
    public static Button Button(string title, Action click, bool accent = false, bool touch = false)
    {
        var button = new Button { Content = title, MinHeight = touch ? 48 : 36, Cursor = new Cursor(StandardCursorType.Hand), HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Background = accent ? Brush("#008D94") : Brushes.White, Foreground = accent ? Brushes.White : Brush("#203047"), BorderBrush = Brush("#DEE6ED"), BorderThickness = new Thickness(1) };
        button.Click += (_, _) => click(); return button;
    }
    public static Border Panel(Control child) => new() { Background = Brushes.White, BorderBrush = Brush("#DEE6ED"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Child = child };
    public static ScrollViewer Scroll(Control child) => new() { Content = child, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    public static void Place(Grid grid, Control child, int column) { Grid.SetColumn(child, column); grid.Children.Add(child); }
}
