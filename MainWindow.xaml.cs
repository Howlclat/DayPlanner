using System.ComponentModel;
using System.Globalization;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DayPlanner;

public partial class MainWindow : Window
{
    private readonly ScheduleStore store;
    private readonly Dictionary<DateTime, PlannerHistory> histories = [];
    private DateTime selectedDate = DateTime.Today;
    private DateTime lastToday = DateTime.Today;
    private bool syncingCalendar;
    private PlannerHistory History
    {
        get { if (!histories.TryGetValue(selectedDate, out var history)) histories[selectedDate] = history = new(); return history; }
    }
    private List<ScheduleItem> CurrentItems
    {
        get => data.ForDate(selectedDate);
        set => data.Days[PlannerData.DateKey(selectedDate)] = value;
    }
    private PlannerData data = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool saveBlocked;
    private bool unsaved;
    private TrayIcon? tray;
    private bool settingsReady;
    private bool exitRequested;
    private bool sessionEnding;
    private WindowState restoredState = WindowState.Normal;
    public MainWindow(string dataPath, bool demo = false)
    {
        InitializeComponent();
        store = new(dataPath);
        try { data = store.Load(); }
        catch (Exception ex)
        {
            saveBlocked = true;
            Loaded += (_, _) => MessageBox.Show(this, $"读取日程时遇到问题：{ex.Message}\n\n原文件保留在：\n{store.FilePath}\n\n请检查该文件后重新打开应用。", "日程文件需要检查", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        if (demo) CurrentItems = DemoItems();
        Timeline.Items = CurrentItems;
        Overview.Timeline = Timeline;
        Timeline.CreateRequested += Create;
        Timeline.EditRequested += item => Edit(item, false);
        Timeline.DeleteRequested += Delete;
        Timeline.TimeChanged += ChangeTime;
        Timeline.ViewChanged += () => Overview.InvalidateVisual();
        PreviewKeyDown += OnShortcut;
        timer.Tick += (_, _) => Tick();
        timer.Start();
        Closing += OnClosing;
        Closed += (_, _) => { timer.Stop(); tray?.Dispose(); Application.Current.SessionEnding -= OnSessionEnding; };
        Application.Current.SessionEnding += OnSessionEnding;
        StateChanged += (_, _) => { if (WindowState != WindowState.Minimized) restoredState = WindowState; };
        CloseToTrayOption.IsChecked = data.CloseToTray;
        Loaded += (_, _) => InitializeTray();
        settingsReady = true;
        UpdateGrid(); Refresh();
        if (saveBlocked) { SaveStatus.Text = "文件读取异常 · 原文件已保留"; SaveStatus.Foreground = TimelineControl.BrushOf("#C24A40"); }
        else SaveStatus.Text = data.Days.Values.Any(items => items.Count > 0) ? "已自动保存" : "本地保存 · 随时开始";
        SaveStatus.ToolTip = store.FilePath;
        if (store.RequiresMigration && !saveBlocked) Save();
        Tick();
    }
    internal static List<ScheduleItem> DemoItems() =>
    [
        new() { Title = "专注工作", Start = 540, End = 630 },
        new() { Title = "给客户回电", Start = 660, Color = "#7564F4" },
        new() { Title = "午餐与休息", Start = 720, End = 780, Color = "#E6A23A" },
        new() { Title = "项目讨论", Start = 840, End = 900 },
        new() { Title = "整理资料", Start = 920, End = 960, Color = "#428BD0" },
        new() { Title = "提交日报", Start = 1020, Color = "#7564F4" }
    ];
    private void Tick()
    {
        var now = DateTime.Now;
        if (now.Date != lastToday)
        {
            lastToday = now.Date;
            UpdateDateTitle();
        }
        ClockText.Text = now.ToString("HH:mm:ss");
        Timeline.Now = now;
        Timeline.InvalidateVisual(); Overview.InvalidateVisual();
        CheckReminders(now);
    }
    private void CheckReminders(DateTime now)
    {
        if (tray is null || saveBlocked) return;
        var due = new List<(DateTime Date, ScheduleItem Item, string Key)>();
        foreach (var date in new[] { now.Date, now.Date.AddDays(1) })
        {
            var dateKey = PlannerData.DateKey(date);
            if (!data.Days.TryGetValue(dateKey, out var items)) continue;
            foreach (var item in items)
            {
                if (!item.ReminderEnabled) continue;
                var start = date.AddMinutes(item.Start);
                var key = $"{dateKey}/{item.Id:N}/{item.Start}/{item.ReminderMinutes}";
                if (now >= start.AddMinutes(-item.ReminderMinutes) && now < start && !data.ReminderReceipts.Contains(key))
                    due.Add((date, item, key));
            }
        }
        if (due.Count == 0) return;
        due.Sort((a, b) => a.Date.AddMinutes(a.Item.Start).CompareTo(b.Date.AddMinutes(b.Item.Start)));
        var lines = due.Take(3).Select(entry =>
        {
            var title = entry.Item.Title.ReplaceLineEndings(" ");
            if (title.Length > 40) title = title[..39] + "…";
            return $"{entry.Date:M月d日} {TimeMath.Format(entry.Item.Start)}  {title}";
        });
        var message = string.Join("\n", lines);
        if (due.Count > 3) message += $"\n另有{due.Count - 3}项安排";
        tray.Notify(due.Count == 1 ? "日程即将开始" : $"{due.Count}项日程即将开始", message, due[0].Date);
        foreach (var entry in due) data.ReminderReceipts.Add(entry.Key);
        Save();
    }
    internal void RestoreWindow()
    {
        Show(); WindowState = restoredState; Activate();
    }
    private bool InitializeTray()
    {
        try
        {
            tray ??= new TrayIcon(data.CloseToTray, RestoreWindow, ExitApplication, value => CloseToTrayOption.IsChecked = value, OpenReminderDate);
            tray.EnsureVisible();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"托盘图标初始化失败：{ex.Message}\n请重新打开应用。", "时间规划局", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }
    private void OpenReminderDate(DateTime date)
    {
        RestoreWindow();
        if (!OwnedWindows.OfType<EventEditor>().Any()) SelectDate(date);
    }
    private void ExitApplication()
    {
        if (OwnedWindows.OfType<EventEditor>().Any()) { RestoreWindow(); return; }
        exitRequested = true;
        Close();
        exitRequested = false;
    }
    private void CloseToTrayChanged(object sender, RoutedEventArgs e)
    {
        if (!settingsReady) return;
        data.CloseToTray = CloseToTrayOption.IsChecked == true;
        data.RememberCloseChoice = true;
        tray?.SetCloseBehavior(data.CloseToTray);
        Save();
    }
    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        sessionEnding = true; exitRequested = true;
        if (unsaved) Save();
    }
    private void Refresh()
    {
        Timeline.Items = CurrentItems;
        Timeline.SelectedDate = selectedDate;
        Timeline.Refresh(); Overview.InvalidateVisual();
        CountText.Text = $"{CurrentItems.Count}项安排";
        UndoButton.IsEnabled = History.CanUndo;
        RedoButton.IsEnabled = History.CanRedo;
        UpdateDateTitle();
    }
    private void UpdateDateTitle()
    {
        var culture = CultureInfo.GetCultureInfo("zh-CN");
        DateTitle.Text = selectedDate.ToString("yyyy年M月d日", culture);
        DateSubtitle.Text = selectedDate.ToString("dddd", culture) + (selectedDate == DateTime.Today ? " · 今天" : "");
    }
    private void SelectDate(DateTime date)
    {
        selectedDate = date.Date;
        Timeline.ClearSelection();
        TimelineScroll.ScrollToTop();
        Refresh();
    }
    private void Calendar_Click(object sender, RoutedEventArgs e)
    {
        syncingCalendar = true;
        DateCalendar.SelectedDate = selectedDate;
        DateCalendar.DisplayDate = selectedDate;
        syncingCalendar = false;
        CalendarPopup.IsOpen = !CalendarPopup.IsOpen;
    }
    private void Calendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (syncingCalendar || e.AddedItems.Count == 0 || e.AddedItems[0] is not DateTime date) return;
        SelectDate(date);
        CalendarPopup.IsOpen = false;
    }
    private bool Save()
    {
        unsaved = true;
        if (saveBlocked) return false;
        try
        {
            store.Save(data); unsaved = false;
            SaveStatus.Text = "✓ 已自动保存";
            SaveStatus.Foreground = TimelineControl.BrushOf("#50969A");
            SaveStatus.ToolTip = store.FilePath;
            return true;
        }
        catch (Exception ex)
        {
            SaveStatus.Text = "保存待重试 · Ctrl+S";
            SaveStatus.Foreground = TimelineControl.BrushOf("#C24A40");
            SaveStatus.ToolTip = ex.Message;
            return false;
        }
    }
    private void Commit(Action action)
    {
        History.Remember(CurrentItems);
        action(); Refresh(); Save();
    }
    private void Create(int start, int? end) => Edit(new ScheduleItem { Start = start, End = end, Color = end is null ? "#7564F4" : "#009DA4" }, true);
    private void Edit(ScheduleItem item, bool isNew)
    {
        var editor = new EventEditor(item.Copy(), isNew, data.CustomColors, selectedDate) { Owner = this };
        editor.ColorAdded += color => { if (!data.CustomColors.Contains(color)) { data.CustomColors.Add(color); Save(); } };
        if (editor.ShowDialog() == true && editor.Result is { } result)
            Commit(() => { if (isNew) CurrentItems.Add(result); else { int index = CurrentItems.FindIndex(x => x.Id == item.Id); if (index >= 0) CurrentItems[index] = result; } });
    }
    private void Delete(ScheduleItem item) => Commit(() => CurrentItems.RemoveAll(x => x.Id == item.Id));
    private void ChangeTime(ScheduleItem item, int start, int? end)
    {
        var target = CurrentItems.FirstOrDefault(x => x.Id == item.Id);
        if (target != null) Commit(() => { target.Start = start; target.End = end; });
    }
    private void UpdateGrid()
    {
        Timeline.GridMinutes = data.GridMinutes;
        foreach (var (button, value) in new[] { (Grid5, 5), (Grid10, 10) })
        {
            button.Background = value == data.GridMinutes ? TimelineControl.BrushOf("#008D94") : Brushes.White;
            button.Foreground = value == data.GridMinutes ? Brushes.White : TimelineControl.BrushOf("#34465D");
        }
        Timeline.Refresh();
    }
    private void Grid5_Click(object sender, RoutedEventArgs e) { data.GridMinutes = 5; UpdateGrid(); Save(); }
    private void Grid10_Click(object sender, RoutedEventArgs e) { data.GridMinutes = 10; UpdateGrid(); Save(); }
    private void New_Click(object sender, RoutedEventArgs e) => Create(Math.Min(1440 - data.GridMinutes, TimeMath.Snap(DateTime.Now.TimeOfDay.TotalMinutes, data.GridMinutes)), null);
    private void Undo_Click(object sender, RoutedEventArgs e) { if (History.CanUndo) { CurrentItems = History.Undo(CurrentItems); Refresh(); Save(); } }
    private void Redo_Click(object sender, RoutedEventArgs e) { if (History.CanRedo) { CurrentItems = History.Redo(CurrentItems); Refresh(); Save(); } }
    private void OnShortcut(object sender, KeyEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        if (e.Key == Key.Z) { Undo_Click(sender, e); e.Handled = true; }
        if (e.Key == Key.Y) { Redo_Click(sender, e); e.Handled = true; }
        if (e.Key == Key.N) { New_Click(sender, e); e.Handled = true; }
        if (e.Key == Key.S) { Save(); e.Handled = true; }
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (sessionEnding) return;
        var minimizeToTray = data.CloseToTray;
        if (!exitRequested && !data.RememberCloseChoice)
        {
            var choice = new CloseChoiceWindow { Owner = this };
            if (choice.ShowDialog() != true) { e.Cancel = true; return; }
            minimizeToTray = choice.MinimizeToTray;
            if (choice.Remember)
            {
                data.CloseToTray = minimizeToTray;
                data.RememberCloseChoice = true;
                settingsReady = false;
                CloseToTrayOption.IsChecked = minimizeToTray;
                settingsReady = true;
                tray?.SetCloseBehavior(minimizeToTray);
                Save();
            }
        }
        if (!exitRequested && minimizeToTray)
        {
            if (!InitializeTray()) { e.Cancel = true; return; }
            if (unsaved) Save();
            e.Cancel = true; CalendarPopup.IsOpen = false; Hide();
            tray?.ShowBackgroundHint();
            return;
        }
        if (unsaved && !Save())
            e.Cancel = MessageBox.Show(this, "部分安排仍待保存。选择“是”返回应用继续处理，选择“否”关闭并放弃本次未保存内容。", "日程保存", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
}
