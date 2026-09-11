using System.IO;
using System.Windows;

namespace DayPlanner;

public partial class App : Application
{
    private Mutex? instanceMutex;
    private EventWaitHandle? restoreSignal;
    private RegisteredWaitHandle? restoreWait;
    private bool ownsMutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--self-test"))
        {
            Shutdown(SelfTest.Run());
            return;
        }
        var demo = e.Args.Contains("--demo");
        var instanceName = demo ? "DayPlanner.Demo" : "DayPlanner.Main";
        restoreSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\" + instanceName + ".Restore");
        instanceMutex = new Mutex(false, @"Local\" + instanceName);
        try { ownsMutex = instanceMutex.WaitOne(0); }
        catch (AbandonedMutexException) { ownsMutex = true; }
        if (!ownsMutex)
        {
            restoreSignal.Set();
            Shutdown();
            return;
        }
        var path = demo
            ? Path.Combine(AppContext.BaseDirectory, "demo-data.json")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DayPlanner", "schedule.json");
        var window = new MainWindow(path, demo);
        MainWindow = window;
        window.Show();
        restoreWait = ThreadPool.RegisterWaitForSingleObject(restoreSignal, (_, _) =>
        {
            if (!Dispatcher.HasShutdownStarted)
                Dispatcher.BeginInvoke(new Action(() => window.RestoreWindow()));
        }, null, Timeout.Infinite, false);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        restoreWait?.Unregister(null);
        restoreSignal?.Dispose();
        if (ownsMutex) instanceMutex?.ReleaseMutex();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
