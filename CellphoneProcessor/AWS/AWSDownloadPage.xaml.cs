using Amazon.S3.Model;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace CellphoneProcessor.AWS
{
    /// <summary>
    /// Interaction logic for AWSDownloadWindow.xaml
    /// </summary>
    public partial class AWSDownloadPage : UiPage
    {
        public AWSDownloadPage()
        {
            InitializeComponent();
        }

        private async void StartProcessing_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AWSViewModel vm)
            {
                IsEnabled = false;
                try
                {
                    await vm.Save();
                    await vm.StartProcessingAsync();
                }
                catch(Exception ex)
                {
                    System.Windows.MessageBox.Show(MainWindow.Shared, ex.Message + "\r\n" + ex.StackTrace, "Error Downloading", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsEnabled = true;
                }
            }
        }

        private void Select_Directory(object sender, RoutedEventArgs e)
        {
            if (DataContext is AWSViewModel vm)
            {
                var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog()
                {
                    Multiselect = false,
                };
                if(dialog.ShowDialog() == true)
                {
                    vm.DownloadFolder = dialog.SelectedPath;
                }
            }
        }
    }
}
