using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace CellphoneProcessor.Compare;

/// <summary>
/// Interaction logic for CompareTripGenerationPage.xaml
/// </summary>
public partial class CompareTripGenerationPage : Page
{
    public CompareTripGenerationPage()
    {
        InitializeComponent();
    }

    private void Select_OurFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog()
        {
            Multiselect = false
        };
        if (dialog.ShowDialog(MainWindow.Shared) == true)
        {
            var name = dialog.FileName;
            if (DataContext is CompareTripGenerationViewModel vm)
            {
                vm.OurTripFile = name;
            }
        }
    }

    private void Add_File(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog()
        {
            Multiselect = true
        };
        if (dialog.ShowDialog(MainWindow.Shared) == true)
        {
            var names = dialog.FileNames;
            if (names is not null)
            {
                foreach (var name in names)
                {
                    AddFile(name);
                }
            }
        }
    }

    private void AddFile(string name)
    {
        if (DataContext is CompareTripGenerationViewModel vm)
        {
            vm.AddTheirFile(name);
        }
    }

    private void Subtract_File(object sender, RoutedEventArgs e)
    {
        var selected = TheirFiles.SelectedItems;
        var toRemove = new List<string>();
        foreach (var item in selected)
        {
            if (item is string itemString)
            {
                toRemove.Add(itemString);
            }
        }
        foreach (var item in toRemove)
        {
            RemoveFile(item);
        }
    }

    private void RemoveFile(string name)
    {
        if (DataContext is CompareTripGenerationViewModel vm)
        {
            vm.RemoveTheirFile(name);
        }
    }

    private async void Run_Clicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is CompareTripGenerationViewModel vm)
        {
            OutputProgress.IsIndeterminate = true;
            MainWindow.Shared.IsEnabled = false;
            try
            {
                await vm.CompareFiles();
                OutputProgress.IsIndeterminate = false;
                MessageBox.Show(MainWindow.Shared, "Compare Complete!", "Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(MainWindow.Shared, ex.Message + "\r\n" + ex.StackTrace, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                MainWindow.Shared.IsEnabled = true;
                OutputProgress.IsIndeterminate = false;
            }

        }
    }

    private void Select_OutputFile(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog();
        if (dialog.ShowDialog(MainWindow.Shared) == true)
        {
            var name = dialog.FileName;
            if (DataContext is CompareTripGenerationViewModel vm)
            {
                vm.OutputFile = name;
            }
        }
    }
}
