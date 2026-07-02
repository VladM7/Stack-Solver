using Stack_Solver.ViewModels.Pages;
using System.Windows;
using Wpf.Ui.Abstractions.Controls;

namespace Stack_Solver.Views.Pages
{
    public partial class JobManagerPage : INavigableView<JobManagerViewModel>
    {
        public JobManagerViewModel ViewModel { get; set; }

        public JobManagerPage(JobManagerViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.InitializeAsync();
        }
    }
}
