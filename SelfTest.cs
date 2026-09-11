using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DayPlanner;

internal static class SelfTest
{
    public static int Run()
    {
        var root = Path.Combine(Environment.CurrentDirectory, "artifacts");
        Directory.CreateDirectory(root);
        var work = Path.Combine(root, "test-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var log = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); log.Add("PASS " + name); }
        try
        {
            Check(typeof(object).Assembly.GetName().Name == "mscorlib", "运行于Windows自带的.NET Framework CLR");
            Check(TimeMath.TryParse("24:00", out int end) && end == 1440, "24:00结束边界");
            Check(!TimeMath.TryParse("24:01", out _) && !TimeMath.TryParse("-1:00", out _) && !TimeMath.TryParse("12:60", out _), "无效时间拒绝");
            Check(TimeMath.TryParse("09：35", out int value) && value == 575, "中文冒号输入");
            Check(TimeMath.Snap(577, 5) == 575 && TimeMath.Snap(577, 10) == 580, "5分钟和10分钟吸附");
            var item = new ScheduleItem { Title = "跨小时日程", Start = 575, End = 665 };
            var moved = TimeMath.Move(item, 1000);
            Check(moved.Start == 1350 && moved.End == 1440, "移动至日末保持时长");
            moved = TimeMath.Move(item, -1000);
            Check(moved.Start == 0 && moved.End == 90, "移动至日初保持时长");
            Check(!TimeMath.IsValid(item with { End = 575 }) && !TimeMath.IsValid(item with { Start = 1440, End = null }), "零时长与24:00事件校验");
            var point = item with { End = null };
            Check(TimeMath.Move(point, 1000) == (1439, null), "时间点日末边界");
            var history = new PlannerHistory();
            var items = new List<ScheduleItem> { item.Copy() };
            history.Remember(items); items[0].Title = "已编辑";
            items = history.Undo(items);
            Check(items[0].Title == "跨小时日程" && history.CanRedo, "撤销使用独立快照");
            items = history.Redo(items);
            Check(items[0].Title == "已编辑", "重做恢复编辑");
            var store = new ScheduleStore(Path.Combine(work, "schedule.json"));
            var data = new PlannerData { Items = [item.Copy(), point with { Id = Guid.NewGuid(), Title = "时间点" }], GridMinutes = 5 };
            store.Save(data);
            var loaded = store.Load();
            Check(loaded.Items.Count == 2 && loaded.Items[0].End == 665 && loaded.Items[1].End is null && loaded.GridMinutes == 5, "JSON日程与设置往返");
            data.Items[0].Title = "第二次保存"; store.Save(data);
            Check(File.Exists(store.FilePath + ".bak") && store.Load().Items[0].Title == "第二次保存", "原子替换与备份");
            File.WriteAllText(store.FilePath, "{ damaged");
            bool rejected = false;
            try { store.Load(); } catch { rejected = true; }
            Check(rejected && File.ReadAllText(store.FilePath) == "{ damaged", "损坏文件保留");
            var compatibilityPath = Path.Combine(work, "existing-v2.json");
            const string existingJson = """
                {"Version":2,"GridMinutes":5,"CloseToTray":true,"RememberCloseChoice":true,
                "CustomColors":["#AB12EF"],"ReminderReceipts":["saved-receipt"],"Days":{
                "2026-09-10":[{"Id":"11111111-1111-1111-1111-111111111111","Title":"已有日程","Start":540,"End":630,"Color":"#AB12EF","ReminderEnabled":true,"ReminderMinutes":15}],
                "2026-09-12":[{"Id":"22222222-2222-2222-2222-222222222222","Title":"已有事件","Start":960,"End":null,"Color":"#7564F4","ReminderEnabled":false,"ReminderMinutes":5}]}}
                """;
            File.WriteAllText(compatibilityPath, existingJson);
            var compatibilityStore = new ScheduleStore(compatibilityPath);
            var existing = compatibilityStore.Load();
            var existingItem = existing.ForDate(new DateTime(2026, 9, 10)).Single();
            Check(existingItem.Id == Guid.Parse("11111111-1111-1111-1111-111111111111") && existingItem.ReminderEnabled && existingItem.ReminderMinutes == 15,
                "原有JSON保留标记ID和提前提醒");
            existingItem.Title = "修改已有日程";
            compatibilityStore.Save(existing);
            var reloaded = compatibilityStore.Load();
            Check(reloaded.Days.Count == 2 && reloaded.ForDate(new DateTime(2026, 9, 12)).Single().Title == "已有事件" && reloaded.CloseToTray && reloaded.RememberCloseChoice
                && reloaded.CustomColors.Contains("#AB12EF") && reloaded.ReminderReceipts.Contains("saved-receipt"), "跨日期保存保留其他日期及全部设置");
            Check(File.ReadAllText(compatibilityPath + ".bak") == existingJson, "原有日程备份完整保留");
            var legacyPath = Path.Combine(work, "existing-v1.json");
            const string legacyJson = """{"Version":1,"Items":[{"Id":"33333333-3333-3333-3333-333333333333","Title":"旧版日程","Start":600,"End":660}]}""";
            File.WriteAllText(legacyPath, legacyJson);
            var legacyStore = new ScheduleStore(legacyPath);
            var legacyData = legacyStore.Load();
            legacyStore.Save(legacyData);
            Check(legacyStore.Load().Items.Single().Title == "旧版日程" && File.ReadAllText(legacyPath + ".v1.bak") == legacyJson, "旧版日程迁移及备份");
            var previewPath = Path.Combine(work, "preview.json");
            new ScheduleStore(previewPath).Save(new PlannerData { RememberCloseChoice = true });
            var window = new MainWindow(previewPath, true) { Width = 1440, Height = 820 };
            window.Show(); window.UpdateLayout();
            var timeline = (TimelineControl)window.FindName("Timeline");
            timeline.Now = DateTime.Today.AddHours(10).AddMinutes(24);
            ((TextBlock)window.FindName("ClockText")).Text = "10:24:36";
            timeline.Refresh(); window.UpdateLayout();
            Snapshot(window, Path.Combine(root, "app-preview.png"));
            Check(timeline.ActualWidth > 1000 && timeline.ActualHeight >= 400, "主窗口布局与渲染");
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var dialog = window.OwnedWindows.OfType<EventEditor>().Single();
                ((Button)dialog.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(((TextBlock)dialog.FindName("ErrorText")).Text == "请填写日程内容。", "编辑窗口空内容校验");
                ((TextBox)dialog.FindName("TitleInput")).Text = "自动化创建测试";
                ((TextBox)dialog.FindName("HexInput")).Text = "#ab12ef";
                ((Button)dialog.FindName("AddColorButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var swatches = ((WrapPanel)dialog.FindName("ColorSwatches")).Children.OfType<RadioButton>().ToList();
                Check(swatches.Count(x => x.IsChecked == true) == 1 && (string?)swatches.Single(x => x.IsChecked == true).Tag == "#AB12EF", "自定义颜色添加并单选");
                ((TextBox)dialog.FindName("StartInput")).Text = "23:50";
                ((RadioButton)dialog.FindName("SpanOption")).IsChecked = true;
                ((TextBox)dialog.FindName("EndInput")).Text = "23:40";
                ((Button)dialog.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(((TextBlock)dialog.FindName("ErrorText")).Text.Contains("晚于"), "编辑窗口逆序时间校验");
                ((TextBox)dialog.FindName("EndInput")).Text = "24:00";
                ((Button)dialog.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
            ((Button)window.FindName("NewScheduleButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(timeline.Items.Count == 7 && timeline.Items.Last().End == 1440, "创建弹窗到主界面完整流程");
            var integrationStore = new ScheduleStore(Path.Combine(work, "preview.json"));
            Check(integrationStore.Load().Items.Count == 7, "界面创建后自动落盘");
            Check(integrationStore.Load().CustomColors.Contains("#AB12EF") && integrationStore.Load().Items.Last().Color == "#AB12EF", "自定义色板和日程颜色重新加载");
            Check(ScheduleStore.NormalizeColor("#abc") == "#AABBCC" && ScheduleStore.NormalizeColor("#xyzxyz") is null, "颜色代码校验和规范化");
            ((Button)window.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(timeline.Items.Count == 6 && integrationStore.Load().Items.Count == 6, "界面撤销并持久化");
            ((Button)window.FindName("RedoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(timeline.Items.Count == 7 && integrationStore.Load().Items.Count == 7, "界面重做并持久化");
            ((Button)window.FindName("Grid5")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(timeline.GridMinutes == 5 && integrationStore.Load().GridMinutes == 5, "网格切换并持久化");
            timeline.SetView(-100, 100);
            Check(timeline.ViewStart == 0 && timeline.ViewSpan == 120, "视区最小范围");
            timeline.SetView(1400, 600);
            Check(timeline.ViewStart == 840, "视区日末钳制");
            timeline.SetView(0, 1440);
            Check(timeline.ViewSpan == 1440, "全天范围");
            timeline.Items = Enumerable.Range(0, 25).Select(i => new ScheduleItem { Title = "重叠日程" + i, Start = 600, End = 660 }).ToList();
            timeline.Refresh(); window.UpdateLayout();
            Check(timeline.DesiredSize.Height > 2000, "密集重叠日程纵向扩展");
            Snapshot(window, Path.Combine(root, "overlap-preview.png"));
            var editor = new EventEditor(item, true) { Owner = window };
            editor.Show(); editor.UpdateLayout();
            Snapshot(editor, Path.Combine(root, "editor-preview.png"));
            ((Button)((WrapPanel)editor.FindName("ColorSwatches")).Children.Cast<UIElement>().Last()).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((TextBox)editor.FindName("HexInput")).Text = "#AB12EF";
            editor.UpdateLayout();
            Snapshot(editor, Path.Combine(root, "custom-color-preview.png"));
            Check(editor.ActualHeight > 400 && editor.ActualHeight <= editor.MaxHeight + 1, "编辑窗口适应屏幕工作区");
            editor.Close();
            window.Close();
            log.Add($"通过：{log.Count}项检查");
            File.WriteAllLines(Path.Combine(root, "test-results.txt"), log);
            return 0;
        }
        catch (Exception ex)
        {
            log.Add("FAIL " + ex);
            File.WriteAllLines(Path.Combine(root, "test-results.txt"), log);
            return 1;
        }
    }
    private static void Snapshot(Window window, string path)
    {
        var visual = (FrameworkElement)window.Content;
        var width = (int)Math.Ceiling(visual.ActualWidth + visual.Margin.Left + visual.Margin.Right);
        var height = (int)Math.Ceiling(visual.ActualHeight + visual.Margin.Top + visual.Margin.Bottom);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); png.Save(file);
    }
}
