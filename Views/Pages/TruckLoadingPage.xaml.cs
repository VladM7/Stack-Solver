using Stack_Solver.ViewModels.Pages;
using System.Windows.Controls;

namespace Stack_Solver.Views.Pages
{
    public partial class TruckLoadingPage : Page
    {
        public TruckLoadingViewModel ViewModel { get; set; }

        public TruckLoadingPage(TruckLoadingViewModel viewModel)
        {
            ViewModel = viewModel;

            InitializeComponent();
        }
    }
}
