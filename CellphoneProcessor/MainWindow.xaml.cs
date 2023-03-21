using System.Security.Principal;
using System.Windows.Controls;
using System.Windows.Shapes;
using Wpf.Ui.Controls;

namespace CellphoneProcessor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : UiWindow
    {
        /// <summary>
        /// A shared reference for use
        /// by pages to help with navigation.
        /// </summary>
        internal static MainWindow Shared { get; private set; } = null!;

        public MainWindow()
        {
            Shared = this;
            Wpf.Ui.Appearance.Accent.ApplySystemAccent();
            InitializeComponent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="page"></param>
        public static void NavigateTo(NavigationItem page)
        {
            var index = 0;
            var items = Shared.RootNavigation.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == page)
                {
                    index = i;
                    break;
                }
            }
            Shared.RootNavigation.Navigate(index);
        }
    }
}
