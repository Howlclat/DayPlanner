using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DayPlanner.Core;
using DayPlanner.UI;

// Renders the real desktop control tree with the platform font and window backend.
// Preview data is separate from the user's calendar.
var mobile = args.Contains("--mobile");
App.PreviewMobile = mobile;
var compact = args.Contains("--compact");
var output = Path.GetFullPath(args.FirstOrDefault(arg => !arg.StartsWith("--")) ?? (mobile ? "artifacts/avalonia-mobile" : "artifacts/avalonia-native"));
Directory.CreateDirectory(output);
App.DataPath = Path.Combine(output, "preview-data.json");
var sample = new PlannerData { DefaultViewStart = 480, DefaultViewEnd = 1200 };
var monday = PlanningDates.WeekStart(DateTime.Today);
for (var offset = 0; offset < 7; offset++)
{
    var date = monday.AddDays(offset);
    sample.ForDate(date).AddRange([
        new() { Title = "专注工作", Start = 540, End = 660 },
        new() { Title = "午餐与休息", Start = 720, End = 780, Color = "#E6A23A" },
        new() { Title = "项目讨论", Start = 840, End = 930, Color = "#428BD0" },
        new() { Title = "整理资料", Start = 960, End = 1020 },
        new() { Title = "回访客户", Start = 1050, Color = "#7564F4" }
    ]);
    if (mobile) sample.ForDate(date).AddRange([
        new() { Title = "同步进度", Start = 570, End = 600, Color = "#428BD0" },
        new() { Title = "确认方案", Start = 690, Color = "#7564F4" }
    ]);
}
new ScheduleStore(App.DataPath).Save(sample);
AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime([], lifetime =>
{
    lifetime.Startup += (_, _) =>
    {
        var index = 0;
        var prefix = mobile ? "mobile" : "desktop";
        var names = mobile
            ? new[] { $"{prefix}-day", $"{prefix}-week", $"{prefix}-month", $"{prefix}-editor", $"{prefix}-editor-span", $"{prefix}-color-picker", $"{prefix}-settings" }
            : new[] { $"{prefix}-day", $"{prefix}-week", $"{prefix}-month", $"{prefix}-editor", $"{prefix}-editor-span", $"{prefix}-settings" };
        void Capture()
        {
            var window = lifetime.MainWindow!;
            window.UpdateLayout();
            var scale = window.RenderScaling;
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width * scale), (int)Math.Ceiling(window.ClientSize.Height * scale)), new Vector(96 * scale, 96 * scale));
            bitmap.Render(window); bitmap.Save(Path.Combine(output, names[index] + ".png"), PngBitmapEncoderOptions.Default);
            Console.WriteLine(names[index]);
            index++;
            if (index == names.Length) { lifetime.Shutdown(); return; }
            if (index <= 2) { App.Session.Mode = (PlannerMode)index; App.Session.Refresh(); }
            else if (index == 4)
            {
                if (mobile) App.Shell!.GetVisualDescendants().OfType<Button>().First(button => button.Content as string == "时间段").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else App.Shell!.GetVisualDescendants().OfType<RadioButton>().First(button => button.Content as string == "时间段").IsChecked = true;
            }
            else if (mobile && index == 5)
            {
                App.Shell!.GetVisualDescendants().OfType<Button>().First(button => Avalonia.Automation.AutomationProperties.GetName(button) == "添加颜色").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            else
            {
                if (mobile && index == 6) App.Shell!.TryDismiss();
                App.Shell!.TryDismiss(); App.Session.Mode = PlannerMode.Day; App.Session.Refresh();
                var label = index == 3 ? mobile ? "新建安排" : "＋新建" : "设置";
                App.Shell.GetVisualDescendants().OfType<Button>().First(button => button.Content as string == label || Avalonia.Automation.AutomationProperties.GetName(button) == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            DispatcherTimer.RunOnce(Capture, TimeSpan.FromMilliseconds(650));
        }
        DispatcherTimer.RunOnce(() =>
        {
            if (compact) { lifetime.MainWindow!.Width = 360; lifetime.MainWindow.Height = 640; }
            DispatcherTimer.RunOnce(Capture, TimeSpan.FromMilliseconds(650));
        }, TimeSpan.FromSeconds(1));
    };
});
