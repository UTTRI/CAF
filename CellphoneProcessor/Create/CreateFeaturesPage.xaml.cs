using System.Windows;
using System.Windows.Controls;
using CellphoneProcessor.Create;

namespace CellphoneProcessor.CreateTrips;

/// <summary>
/// Interaction logic for CreateTripsPage.xaml
/// </summary>
public partial class CreateFeaturesPage : Page
{
    public CreateFeaturesPage()
    {
        InitializeComponent();
    }

    private async void TestCode_Clicked(object sender, RoutedEventArgs e)
    {
        try
        {
            await ((CreateFeaturesViewModel)DataContext).GetOTPData();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
        }
        finally
        {
        }
    }
}
