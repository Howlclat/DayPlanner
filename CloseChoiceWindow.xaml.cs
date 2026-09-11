using System.Windows;

namespace DayPlanner;

public partial class CloseChoiceWindow : Window
{
    public bool MinimizeToTray { get; private set; }
    public bool Remember => RememberOption.IsChecked == true;
    public CloseChoiceWindow() => InitializeComponent();
    private void Minimize_Click(object sender, RoutedEventArgs e) { MinimizeToTray = true; DialogResult = true; }
    private void Exit_Click(object sender, RoutedEventArgs e) { MinimizeToTray = false; DialogResult = true; }
}
