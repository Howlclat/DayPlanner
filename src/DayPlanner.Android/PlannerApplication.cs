using Android.App;
using Android.Runtime;
using Avalonia.Android;
using DayPlanner.UI;

namespace DayPlanner.Android;
[Application]
public class PlannerApplication(nint javaReference, JniHandleOwnership transfer) : AvaloniaAndroidApplication<App>(javaReference, transfer)
{
    public override void OnCreate()
    {
        App.DataPath = AndroidReminders.DataPath(this);
        App.Reminders = new AndroidReminders(this);
        base.OnCreate();
    }
}
