using Stack_Solver.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Stack_Solver.Views.Pages
{
    public partial class PalletBuilderPage : INavigableView<PalletBuilderViewModel>
    {
        public PalletBuilderViewModel ViewModel { get; set; }

        public PalletBuilderPage(PalletBuilderViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }
    }
}
