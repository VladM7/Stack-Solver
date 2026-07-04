using Stack_Solver.ViewModels.Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        private async void JobsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid { SelectedItem: JobRowViewModel row })
            {
                await ViewModel.OpenJobAsync(row.Id);
            }
        }
    }
}
