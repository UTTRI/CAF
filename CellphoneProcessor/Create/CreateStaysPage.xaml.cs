using Ookii.Dialogs.Wpf;
using System.Windows.Controls;

using static CellphoneProcessor.Utilities.FileSelection;

namespace CellphoneProcessor.Create;

/// <summary>
/// Interaction logic for CreateStaysPage.xaml
/// </summary>
public partial class CreateStaysPage : Page
{
    public CreateStaysPage()
    {
        InitializeComponent();
    }

    private void ChunkFolder_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        this.SelectDirectory<CreateStaysViewModel>
            ((vm, directoryPath) => vm.ChunkFolder = directoryPath);
    }

    private void ShapeFile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        this.SelectFile<CreateStaysViewModel>((vm, fileName) => vm.ShapeFile = fileName, true);
    }

    private void RoadNetwork_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        this.SelectFile<CreateStaysViewModel>((vm, fileName) => vm.RoadNetwork = fileName, true);
    }

    private void OutputFile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        this.SelectFile<CreateStaysViewModel>((vm, fileName) => vm.OutputFile = fileName, false);
    }

    private async void StartRun_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is CreateStaysViewModel vm)
        {
            await vm.StartRun();
        }
    }
}
