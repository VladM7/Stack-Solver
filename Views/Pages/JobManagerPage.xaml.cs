using Stack_Solver.ViewModels.Pages;
using System.Windows.Controls;

namespace Stack_Solver.Views.Pages
{
    public partial class JobManagerPage : Page
    {
        public JobManagerViewModel ViewModel { get; set; }

        public JobManagerPage(JobManagerViewModel viewModel)
        {
            ViewModel = viewModel;

            InitializeComponent();
        }
    }
}
