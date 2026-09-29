using Avalonia;
using Avalonia.iOS;
using DayPlanner.UI;
using Foundation;
using UIKit;

namespace DayPlanner.iOS;
[Register("AppDelegate")]
public class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) => base.CustomizeAppBuilder(builder);
}
public static class Program
{
    public static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
