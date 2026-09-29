using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DayPlanner.Core;
using DayPlanner.UI;
using DayPlanner.UI.Views;

var output = Path.GetFullPath("artifacts/avalonia-validation");
Directory.CreateDirectory(output);
var log = new List<string>();
void Check(bool ok, string description) { if (!ok) throw new Exception(description); log.Add("PASS " + description); }
try
{
    var path = Path.Combine(output, Guid.NewGuid() + ".json");
    var store = new ScheduleStore(path);
    var data = new PlannerData { RememberCloseChoice = true };
    var today = DateTime.Today;
    var item = new ScheduleItem { Title = "专注工作", Start = 540, End = 660 };
    data.ForDate(today).Add(item);
    data.ForDate(today.AddDays(1)).Add(new() { Title = "方案评审", Start = 900, Color = "#7564F4" });
    store.Save(data);
    var session = new PlannerSession(path);
    session.Put(item with { Title = "修改安排", ReminderEnabled = true });
    Check(store.Load().ForDate(today.AddDays(1)).Count == 1 && store.Load().Items[0].ReminderEnabled, "兼容WPF数据，保存保留其他日期与提醒");
    Check(File.Exists(path + ".bak") && new ScheduleStore(path + ".bak").Load().Items[0].Title == "专注工作", "日程备份保留原内容");
    session.Undo(); Check(session.Items[0].Title == "专注工作", "撤销");
    session.Redo(); Check(session.Items[0].Title == "修改安排", "重做");
    session.Select(today.AddDays(1)); session.Delete(session.Items[0]);
    Check(store.Load().Items.Count == 1 && session.Items.Count == 0, "跨日期删除隔离");
    session.Undo(); Check(session.Items.Count == 1, "按日期独立撤销");
    Check(TimeMath.Move(item, 2000) == (1320, 1440) && TimeMath.TryParse("24:00", out _), "时间边界");
    Check(PlanningDates.MonthRows(new(2021, 2, 1)) == 4 && PlanningDates.MonthRows(new(2026, 8, 1)) == 6, "月历行数");
    Check(PlanningDates.WeekStart(new(2026, 10, 4)) == new DateTime(2026, 9, 28), "跨月周");
    var badPath = Path.Combine(output, "damaged.json"); File.WriteAllText(badPath, "damaged");
    var bad = new PlannerSession(badPath); bad.Put(item);
    Check(bad.LoadError != null && File.ReadAllText(badPath) == "damaged", "损坏文件保护");
    var legacy = Path.Combine(output, "v1.json"); File.WriteAllText(legacy, """{"Version":1,"Items":[{"Title":"旧版安排","Start":540,"End":600,"Color":"#009DA4"}]}""");
    var migration = new PlannerSession(legacy);
    Check(migration.Items.Count == 1 && File.Exists(legacy + ".v1.bak"), "旧格式迁移保留备份");
    App.DataPath = Path.Combine(output, "app-test.json");
    AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
    session.Select(today); session.Put(item);
    session.Put(new() { Title = "午餐与休息", Start = 720, End = 780, Color = "#E6A23A" });
    session.Put(new() { Title = "项目讨论", Start = 840, End = 930, Color = "#428BD0" });
    session.Put(new() { Title = "回访客户", Start = 1050, Color = "#7564F4" });
    var shell = new PlannerShell(session, false);
    var window = new Window { Content = shell, Width = 1320, Height = 820 }; window.Show();
    void Pump() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    void Capture(string name)
    {
        Pump(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame();
        if (frame == null) throw new Exception("截图为空");
        frame.Save(Path.Combine(output, name + ".png"), PngBitmapEncoderOptions.Default);
    }
    Capture("desktop-day");
    foreach (var mode in new[] { PlannerMode.Week, PlannerMode.Month }) { session.Mode = mode; session.Refresh(); Capture("desktop-" + mode.ToString().ToLower()); }
    Check(shell.GetVisualDescendants().OfType<DayPlanner.UI.Controls.MonthPlannerView>().Any(x => x.IsVisible), "桌面月视图显示");
    var mobile = new PlannerShell(session, true); window.Content = mobile; window.Width = 412; window.Height = 840;
    session.Mode = PlannerMode.Day; session.Refresh(); Capture("android-day-layout");
    var create = mobile.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "＋新建安排");
    create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); Capture("android-editor-layout");
    var form = mobile.GetVisualDescendants().OfType<EventForm>().Single();
    var inputs = form.GetVisualDescendants().OfType<TextBox>().ToList();
    var save = form.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "创建安排");
    var count = session.Items.Count;
    save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(session.Items.Count == count, "空标题校验");
    inputs[0].Text = "手机新建测试"; inputs[1].Text = "10:30";
    save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(store.Load().Items.Any(x => x.Title == "手机新建测试" && x.Start == 630), "手机编辑表单创建并持久化");
    session.Mode = PlannerMode.Week; session.Refresh(); Capture("android-week-layout");
    session.Mode = PlannerMode.Month; session.Refresh(); Capture("android-month-layout");
    Check(mobile.TryDismiss() == false, "保存后关闭编辑页");
    var axis = new DayPlanner.UI.Controls.TimelineControl();
    window.Content = axis; window.Width = 1200; window.Height = 500; Pump();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    Point At(int minute, double y = 300) => new(36 + (minute - axis.ViewStart) / axis.ViewSpan * (axis.Bounds.Width - 72), y);
    (int Start, int? End)? created = null;
    axis.CreateRequested += (start, end) => created = (start, end);
    window.MouseDown(At(543), MouseButton.Left); window.MouseUp(At(543), MouseButton.Left);
    Check(created == (540, null), "时间轴单击创建时间点并吸附");
    window.MouseDown(At(600), MouseButton.Left); window.MouseMove(At(660), RawInputModifiers.LeftMouseButton); window.MouseUp(At(660), MouseButton.Left);
    Check(created == (600, 660), "时间轴拖动创建时间段");
    var moving = new ScheduleItem { Title = "拖动校验", Start = 600, End = 660 };
    axis.Items = [moving]; axis.Refresh(); Pump(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    (int Start, int? End)? changed = null;
    axis.TimeChanged += (_, start, end) => changed = (start, end);
    window.MouseDown(At(630, 110), MouseButton.Left); window.MouseMove(At(690, 110), RawInputModifiers.LeftMouseButton); window.MouseUp(At(690, 110), MouseButton.Left);
    Check(changed == (660, 720), "时间段拖动保留时长");
    axis.Refresh(); Pump(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    window.MouseDown(At(659, 110), MouseButton.Left); window.MouseMove(At(720, 110), RawInputModifiers.LeftMouseButton); window.MouseUp(At(720, 110), MouseButton.Left);
    Check(changed == (600, 720), "时间段边缘拖动调整时长");
    var originalSpan = axis.ViewSpan;
    window.MouseWheel(At(900), new Vector(0, 1), RawInputModifiers.Control);
    Check(axis.ViewSpan < originalSpan, "Ctrl+滚轮缩放时间轴");
    window.Close();
    log.Add($"通过：{log.Count}项检查"); File.WriteAllLines(Path.Combine(output, "results.txt"), log);
    Console.WriteLine(string.Join(Environment.NewLine, log));
    return 0;
}
catch (Exception ex) { log.Add("FAIL " + ex); File.WriteAllLines(Path.Combine(output, "results.txt"), log); Console.WriteLine(ex); return 1; }
