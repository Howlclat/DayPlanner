using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using DayPlanner.Core;
using DayPlanner.UI.Views;

namespace DayPlanner.UI;

public interface IReminderService
{
    string? Status { get; }
    void Reschedule(PlannerData data);
}
public partial class App : Application
{
    public static string? DataPath { get; set; }
    public static bool PreviewMobile { get; set; }
    public static IReminderService? Reminders { get; set; }
    public static PlannerSession Session { get; private set; } = null!;
    public static PlannerShell? Shell { get; private set; }
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        Session = new(DataPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DayPlanner", "schedule.json"));
        if (Reminders != null) { Session.Saved += () => Reminders.Reschedule(Session.Data); Reminders.Reschedule(Session.Data); }
        var mobile = PreviewMobile || ApplicationLifetime is ISingleViewApplicationLifetime;
        Shell = new PlannerShell(Session, mobile);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var window = new Window { Title = "时间规划局", Width = mobile ? 412 : 1320, Height = mobile ? 840 : 780, FontFamily = Typography.Body, FontSize = 13,
                MinWidth = mobile ? 360 : 1040, MinHeight = 620, Content = Shell,
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://DayPlanner.UI/Assets/app.ico"))) };
            desktop.MainWindow = window;
            var tray = new TrayIcon { Icon = window.Icon, ToolTipText = "时间规划局" };
            var menu = new NativeMenu();
            var open = new NativeMenuItem("打开时间规划局");
            void Restore() { window.Show(); window.WindowState = WindowState.Normal; window.Activate(); }
            open.Click += (_, _) => Restore(); tray.Clicked += (_, _) => Restore();
            var exiting = false;
            var exit = new NativeMenuItem("退出"); exit.Click += (_, _) => { if (!Session.HasUnsavedChanges || Session.Save()) { exiting = true; tray.Dispose(); desktop.Shutdown(); } else Restore(); };
            menu.Items.Add(open); menu.Items.Add(new NativeMenuItemSeparator()); menu.Items.Add(exit); tray.Menu = menu;
            TrayIcon.SetIcons(this, new TrayIcons { tray });
            window.Closing += (_, e) =>
            {
                if (exiting) return;
                if (Session.HasUnsavedChanges && !Session.Save()) { e.Cancel = true; Session.Refresh(); return; }
                if (Session.Data.CloseToTray && !mobile) { e.Cancel = true; window.Hide(); }

            };
            window.Closed += (_, _) => { exiting = true; tray.Dispose(); desktop.Shutdown(); };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single) single.MainView = Shell;
        base.OnFrameworkInitializationCompleted();
    }
}
