using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace DayPlanner.Desktop;

internal static class DesktopNotifications
{
    private static Timer? expiry;
    private static NotifyIconData last;
    public static void Show(string title, string text, DateTime date)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Clear();
                var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                var handle = window?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (handle == IntPtr.Zero) return;
                ExtractIconEx(Environment.ProcessPath!, 0, out var largeIcon, out var icon, 1);
                if (largeIcon != IntPtr.Zero) DestroyIcon(largeIcon);
                last = new NotifyIconData { Size = Marshal.SizeOf<NotifyIconData>(), Window = handle, Id = 7201, Flags = 2 | 4 | 16,
                    Icon = icon, Tip = "时间规划局", Info = text.Length > 255 ? text[..255] : text, Title = title, InfoFlags = 1 };
                ShellNotifyIcon(0, ref last);
                expiry = new Timer(_ => Clear(), null, TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan);
            }
            else
            {
                var command = new ProcessStartInfo { UseShellExecute = false, CreateNoWindow = true };
                if (OperatingSystem.IsMacOS())
                {
                    command.FileName = "osascript";
                    command.ArgumentList.Add("-e"); command.ArgumentList.Add("on run argv\ndisplay notification (item 2 of argv) with title (item 1 of argv)\nend run");
                }
                else command.FileName = "notify-send";
                command.ArgumentList.Add(title); command.ArgumentList.Add(text);
                using var process = Process.Start(command);
            }
        }
        catch (Exception ex) { Trace.WriteLine("Reminder: " + ex.Message); }
    }
    private static void Clear()
    {
        expiry?.Dispose(); expiry = null;
        if (last.Window != IntPtr.Zero) { ShellNotifyIcon(2, ref last); if (last.Icon != IntPtr.Zero) DestroyIcon(last.Icon); last = default; }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int Size; public IntPtr Window; public uint Id; public uint Flags; public uint Message; public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State; public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
        public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string path, int index, out IntPtr large, out IntPtr small, uint count);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
}
