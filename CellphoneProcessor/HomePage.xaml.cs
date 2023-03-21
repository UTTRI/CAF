using Wpf.Ui.Controls;

namespace CellphoneProcessor;

/// <summary>
/// Interaction logic for HomePage.xaml
/// </summary>
public partial class HomePage : UiPage
{
    public HomePage()
    {
        InitializeComponent();
    }

    private void GoToAWS_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        MainWindow.NavigateTo(MainWindow.Shared.AWSDownload);
    }

    private void GoToStays_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        MainWindow.NavigateTo(MainWindow.Shared.CreateStays);
    }

    private void GoToTrips_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        MainWindow.NavigateTo(MainWindow.Shared.CreateTrips);
    }
}
