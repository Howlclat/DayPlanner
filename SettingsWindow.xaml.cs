using System.Windows;

namespace DayPlanner;

public partial class SettingsWindow : Window
{
    public int ViewStart { get; private set; }
    public int ViewEnd { get; private set; }
    public int GridMinutes => Grid5Option.IsChecked == true ? 5 : 10;
    public bool CloseToTray => CloseToTrayOption.IsChecked == true;

    public SettingsWindow(PlannerData data)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;
        StartInput.Text = TimeMath.Format(data.DefaultViewStart);
        EndInput.Text = TimeMath.Format(data.DefaultViewEnd);
        Grid5Option.IsChecked = data.GridMinutes == 5;
        Grid10Option.IsChecked = data.GridMinutes == 10;
        CloseToTrayOption.IsChecked = data.CloseToTray;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TimeMath.TryParse(StartInput.Text, out int start) || start >= 1440)
        { ErrorText.Text = "开始时间请填写00:00至23:59。"; StartInput.Focus(); return; }
        if (!TimeMath.TryParse(EndInput.Text, out int end) || end <= start)
        { ErrorText.Text = "结束时间应晚于开始时间，最晚为24:00。"; EndInput.Focus(); return; }
        if (end - start < 120)
        { ErrorText.Text = "显示范围请至少保留2小时。"; EndInput.Focus(); return; }
        ViewStart = start;
        ViewEnd = end;
        DialogResult = true;
    }
}
