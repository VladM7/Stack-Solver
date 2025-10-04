using Stack_Solver.ViewModels.Pages;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Wpf.Ui.Abstractions.Controls;

namespace Stack_Solver.Views.Pages
{
    public partial class SKULibraryPage : INavigableView<SKULibraryViewModel>
    {
        public SKULibraryViewModel ViewModel { get; set; }

        public SKULibraryPage(SKULibraryViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }

        private void skuDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (DataContext is SKULibraryViewModel vm)
                        vm.SaveSkus();
                }), DispatcherPriority.Background);
            }
        }

        private void skuDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            // Ensure generated columns write back immediately and TwoWay
            if (e.Column is DataGridBoundColumn boundColumn)
            {
                if (boundColumn.Binding is Binding binding)
                {
                    binding.Mode = BindingMode.TwoWay;
                    binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
                    // reassign binding to column
                    boundColumn.Binding = binding;
                }
            }
        }
    }
}
