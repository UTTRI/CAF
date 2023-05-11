using System.Windows;
using System.Windows.Controls;
using CellphoneProcessor.Utilities;

namespace CellphoneProcessor.Create;

/// <summary>
/// Interaction logic for CreateHomeLocationPage.xaml
/// </summary>
public partial class CreateHomeLocationPage : Page
{
    public CreateHomeLocationPage()
    {
        InitializeComponent();
    }

    private void StaysFile_Click(object sender, RoutedEventArgs e)
    {
        FileSelection.SelectFile<CreateHomeLocationViewModel>(this, (vm, filePath) => vm.StaysFilePath = filePath, true);
    }

    private void ShapeFile_Click(object sender, RoutedEventArgs e)
    {
        FileSelection.SelectFile<CreateHomeLocationViewModel>(this, (vm, filePath) => vm.ShapeFilePath = filePath, true);
    }

    private void OutputFile_Click(object sender, RoutedEventArgs e)
    {
        FileSelection.SelectFile<CreateHomeLocationViewModel>(this, (vm, filePath) => vm.OutputPath = filePath, false);
    }

    private async void StartRun_Click(object sender, RoutedEventArgs e)
    {
        await ((CreateHomeLocationViewModel)DataContext).RunAsync();
    }
}
