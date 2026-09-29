using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using DayPlanner.UI;

namespace DayPlanner.Android;

[Activity(Label = "时间规划局", Theme = "@style/AppTheme", MainLauncher = true, Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.KeyboardHidden,
    WindowSoftInputMode = global::Android.Views.SoftInput.AdjustResize)]
public class MainActivity : AvaloniaMainActivity
{
    private AndroidReminders? reminders;
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        App.DataPath = AndroidReminders.DataPath(this);
        reminders = new AndroidReminders(this);
        App.Reminders = reminders;
        base.OnCreate(savedInstanceState);
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            Window?.DecorView.SetOnApplyWindowInsetsListener(new InsetsListener());
        OpenNotification(Intent);
    }
    protected override void OnNewIntent(Intent? intent) { base.OnNewIntent(intent); OpenNotification(intent); }
    private static void OpenNotification(Intent? intent)
    {
        if (DateTime.TryParse(intent?.GetStringExtra("date"), out var date))
            Avalonia.Threading.Dispatcher.UIThread.Post(() => App.Session?.Select(date));
    }
    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (App.Session != null) reminders?.Reschedule(App.Session.Data);
    }
    public override void OnBackPressed()
    {
        if (App.Shell?.TryDismiss() == true) return;
        if (!OperatingSystem.IsAndroidVersionAtLeast(33)) base.OnBackPressed();
        else MoveTaskToBack(true);
    }
    private sealed class InsetsListener : Java.Lang.Object, global::Android.Views.View.IOnApplyWindowInsetsListener
    {
        public global::Android.Views.WindowInsets OnApplyWindowInsets(global::Android.Views.View? view, global::Android.Views.WindowInsets? insets)
        {
            if (insets != null && OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var bars = insets.GetInsets(global::Android.Views.WindowInsets.Type.SystemBars());
                view?.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
            }
            return insets!;
        }
    }
}
