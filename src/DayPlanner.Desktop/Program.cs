using Avalonia;
using DayPlanner.UI;

namespace DayPlanner.Desktop;
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        DayPlanner.UI.Views.PlannerShell.Notify = DesktopNotifications.Show;
        App.PreviewMobile = args.Contains("--mobile-preview");
        var index = Array.IndexOf(args, "--data");
        if (index >= 0 && index + 1 < args.Length) App.DataPath = Path.GetFullPath(args[index + 1]);
        var instanceKey = App.DataPath == null ? "DayPlanner.Main" : "DayPlanner." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(App.DataPath)))[..16];
        using var instance = new Mutex(false, instanceKey);
        bool owns;
        try { owns = instance.WaitOne(0); } catch (AbandonedMutexException) { owns = true; }
        if (!owns) return;
        try { BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        finally { instance.ReleaseMutex(); }
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
