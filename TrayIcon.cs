using System.Windows;
using Forms = System.Windows.Forms;

namespace DayPlanner;

internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly System.Drawing.Icon drawingIcon;
    private readonly Forms.ContextMenuStrip menu;
    private readonly Forms.ToolStripMenuItem closeToTray;
    private DateTime? notificationDate;

    public TrayIcon(bool minimizeOnClose, Action restore, Action exit, Action<bool> changeCloseBehavior, Action<DateTime> openDate)
    {
        using var resource = Application.GetResourceStream(new Uri("pack://application:,,,/DayPlanner;component/Assets/app.ico")).Stream;
        using var original = new System.Drawing.Icon(resource, 32, 32);
        drawingIcon = (System.Drawing.Icon)original.Clone();
        menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开时间规划局", null, (_, _) => restore());
        closeToTray = new Forms.ToolStripMenuItem("关闭时最小化到托盘") { Checked = minimizeOnClose, CheckOnClick = true };
        closeToTray.Click += (_, _) => changeCloseBehavior(closeToTray.Checked);
        menu.Items.Add(closeToTray);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());
        icon = new Forms.NotifyIcon { Icon = drawingIcon, Text = "时间规划局", ContextMenuStrip = menu, Visible = true };
        icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) restore(); };
        icon.BalloonTipClicked += (_, _) => { if (notificationDate is DateTime date) openDate(date); else restore(); };
    }
    public void SetCloseBehavior(bool value) => closeToTray.Checked = value;
    public void EnsureVisible()
    {
        // Re-register with Explorer after the WPF window is loaded and before hiding it.
        icon.Visible = false;
        icon.Icon = drawingIcon;
        icon.Visible = true;
    }
    public void ShowBackgroundHint() => icon.ShowBalloonTip(5000, "时间规划局正在后台运行", "日程提醒会继续运行。点击通知区域的时间规划局图标可恢复窗口，图标也可能位于“显示隐藏的图标”中。", Forms.ToolTipIcon.Info);
    public void Notify(string title, string message, DateTime date)
    {
        notificationDate = date;
        icon.ShowBalloonTip(10000, title, message, Forms.ToolTipIcon.Info);
    }
    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose(); menu.Dispose(); drawingIcon.Dispose();
    }
}
