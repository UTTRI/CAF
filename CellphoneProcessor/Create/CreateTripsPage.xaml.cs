using CellphoneProcessor.Utilities;
using System.Windows.Controls;

namespace CellphoneProcessor.Create;

/// <summary>
/// Interaction logic for CreateTripsPage.xaml
/// </summary>
public partial class CreateTripsPage : Page
{
    public CreateTripsPage()
    {
        InitializeComponent();
    }

    private void StaysFilePath_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        this.SelectFile<CreateTripsViewModel>((vm, file) => vm.StaysFilePath = file, true);
    }

    private void OutputFilePath_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        this.SelectFile<CreateTripsViewModel>((vm, file) => vm.OutputPath = file, false);
    }

    private async void StartRun_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        await ((CreateTripsViewModel)DataContext).CreateTripsAsync();
    }
}
