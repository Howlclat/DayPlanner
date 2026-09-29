using Avalonia.Media;

namespace DayPlanner.UI;

public static class Typography
{
    public static FontFamily Body { get; } = OperatingSystem.IsWindows()
        ? new FontFamily("Microsoft YaHei UI") : FontFamily.Default;
    public static FontFamily Clock { get; } = OperatingSystem.IsWindows()
        ? new FontFamily("Cascadia Mono, Consolas") : FontFamily.Default;
}
