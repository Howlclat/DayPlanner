using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using DayPlanner.Core;
using DayPlanner.UI;

namespace DayPlanner.Android;

public sealed class AndroidReminders(Context context) : IReminderService
{
    public string? Status { get; private set; }
    private bool requestedPermission;
    public static string DataPath(Context context) => Path.Combine(context.FilesDir!.AbsolutePath, "DayPlanner", "schedule.json");
    public void Reschedule(PlannerData data)
    {
        try { RescheduleCore(data); }
        catch (Exception ex)
        {
            Status = "日程已保存，系统提醒设置失败，请检查通知和闹钟权限后重试。";
            global::Android.Util.Log.Warn("DayPlanner", ex.ToString());
        }
    }
    private void RescheduleCore(PlannerData data)
    {
        Status = null;
        var alarm = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var prefs = context.GetSharedPreferences("reminders", FileCreationMode.Private)!;
        var delivered = prefs.GetStringSet("delivered", new HashSet<string>()) ?? new HashSet<string>();
        foreach (var uri in prefs.GetStringSet("scheduled", new HashSet<string>()) ?? [])
        {
            var pending = PendingIntent.GetBroadcast(context, 0, IntentFor(uri), PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);
            if (pending != null) { alarm.Cancel(pending); pending.Cancel(); }
        }
        var scheduled = new HashSet<string>();
        var enabled = data.Days.SelectMany(day => day.Value.Where(item => item.ReminderEnabled).Select(item => (Date: DateTime.ParseExact(day.Key, "yyyy-MM-dd", null), Item: item))).ToList();
        if (enabled.Count > 0 && OperatingSystem.IsAndroidVersionAtLeast(33) && context.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            Status = "请允许通知权限，以便接收日程提醒。";
            if (context is Activity activity && !requestedPermission) { requestedPermission = true; activity.RequestPermissions([global::Android.Manifest.Permission.PostNotifications], 101); }
        }
        foreach (var (date, item) in enabled)
        {
            var receipt = $"{date:yyyy-MM-dd}/{item.Id:N}/{item.Start}/{item.ReminderMinutes}";
            if (delivered.Contains(receipt)) continue;
            var start = date.AddMinutes(item.Start);
            if (start <= DateTime.Now) continue;
            var when = start.AddMinutes(-item.ReminderMinutes);
            if (when <= DateTime.Now) when = DateTime.Now.AddSeconds(2);
            var uri = $"dayplanner://reminder/{date:yyyy-MM-dd}/{item.Id:N}";
            var intent = IntentFor(uri).PutExtra("title", item.Title).PutExtra("time", item.TimeLabel).PutExtra("date", date.ToString("yyyy-MM-dd")).PutExtra("receipt", receipt);
            var pending = PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
            var millis = new DateTimeOffset(when).ToUnixTimeMilliseconds();
            if (!OperatingSystem.IsAndroidVersionAtLeast(31) || alarm.CanScheduleExactAlarms()) alarm.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, millis, pending);
            else { alarm.SetAndAllowWhileIdle(AlarmType.RtcWakeup, millis, pending); Status ??= "已设置提醒。系统未授予精确闹钟权限，提醒可能延迟；可在系统设置的“闹钟和提醒”中授权。"; }
            scheduled.Add(uri);
        }
        prefs.Edit()!.PutStringSet("scheduled", scheduled)!.Apply();
    }
    private Intent IntentFor(string uri) => new Intent(context, typeof(ReminderReceiver)).SetData(global::Android.Net.Uri.Parse(uri));
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public class ReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && context.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) != Permission.Granted) return;
        var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        var prefs = context.GetSharedPreferences("reminders", FileCreationMode.Private)!;
        var delivered = new HashSet<string>(prefs.GetStringSet("delivered", new HashSet<string>()) ?? []);
        var receipt = intent.GetStringExtra("receipt") ?? "";
        if (delivered.Contains(receipt)) return;
        manager.CreateNotificationChannel(new NotificationChannel("schedule", "日程提醒", NotificationImportance.High));
        var open = new Intent(context, typeof(MainActivity)).PutExtra("date", intent.GetStringExtra("date")).SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop).SetData(intent.Data);
        var pending = PendingIntent.GetActivity(context, 0, open, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var notification = new Notification.Builder(context, "schedule").SetSmallIcon(Resource.Drawable.ic_reminder)
            .SetContentTitle(intent.GetStringExtra("title") ?? "日程即将开始").SetContentText(intent.GetStringExtra("time"))
            .SetContentIntent(pending).SetAutoCancel(true).Build();
        manager.Notify(intent.DataString, 1, notification);
        delivered.Add(receipt);
        prefs.Edit()!.PutStringSet("delivered", delivered)!.Apply();
    }
}

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter([Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced, Intent.ActionTimeChanged, Intent.ActionTimezoneChanged])]
public class RescheduleReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;
        try { new AndroidReminders(context).Reschedule(new ScheduleStore(AndroidReminders.DataPath(context)).Load()); }
        catch (Exception ex) { global::Android.Util.Log.Warn("DayPlanner", ex.ToString()); }
    }
}
