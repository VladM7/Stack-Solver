using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

namespace Stack_Solver.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "Stack Solver";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Home",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(Views.Pages.DashboardPage)
            },
            new NavigationViewItem()
            {
                Content = "SKU Library",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Box24 },
                TargetPageType = typeof(Views.Pages.SKULibraryPage)
            },
            new NavigationViewItem()
            {
                Content = "Pallet Builder",
                Icon = new SymbolIcon { Symbol = SymbolRegular.SlideGrid24 },
                TargetPageType = typeof(Views.Pages.PalletBuilderPage)
            },
            new NavigationViewItem()
            {
                Content = "Truck Loading",
                Icon = new SymbolIcon { Symbol = SymbolRegular.VehicleTruckProfile24 },
                TargetPageType = typeof(Views.Pages.TruckLoadingPage)
            }
        };

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Job Manager",
                Icon = new SymbolIcon { Symbol = SymbolRegular.DocumentBulletListMultiple24 },
                TargetPageType = typeof(Views.Pages.JobManagerPage)
            },
            new NavigationViewItem()
            {
                Content = "Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage)
            }
        };

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new()
        {
            new MenuItem { Header = "Home", Tag = "tray_home" }
        };
    }
}
