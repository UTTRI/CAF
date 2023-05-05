using Ookii.Dialogs.Wpf;
using System.Windows.Controls;

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

    /// <summary>
    /// Launch a GUI to select a file.
    /// </summary>
    /// <param name="assignment">The process for what to do after the file is selected.</param>
    /// <param name="fileExists">Does the file need to already exist?</param>
    private void SelectFile(Action<CreateStaysViewModel, string> assignment, bool fileExists)
    {
        VistaOpenFileDialog dialog = new()
        {
            Multiselect = false,
            CheckFileExists = fileExists,
        };
        if (dialog.ShowDialog() == true
            && DataContext is CreateStaysViewModel vm)
        {
            assignment(vm, dialog.FileName);
        }
    }

    private void SelectDirectory(Action<CreateStaysViewModel, string> assignment)
    {
        VistaFolderBrowserDialog dialog = new()
        {
            Multiselect = false,
        };
        if (dialog.ShowDialog() == true
            && DataContext is CreateStaysViewModel vm)
        {
            assignment(vm, dialog.SelectedPath);
        }
    }

    private void ChunkFolder_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        SelectDirectory((vm, directoryPath) => vm.ChunkFolder = directoryPath);
    }

    private void ShapeFile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        SelectFile((vm, fileName) => vm.ShapeFile = fileName, true);
    }

    private void RoadNetwork_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        SelectFile((vm, fileName) => vm.RoadNetwork = fileName, true);
    }

    private void OutputFile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        SelectFile((vm, fileName) => vm.OutputFile = fileName, false);
    }

    private async void StartRun_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is CreateStaysViewModel vm)
        {
            await vm.StartRun();
        }
    }
}
