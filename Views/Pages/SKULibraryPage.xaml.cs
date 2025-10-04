using Stack_Solver.Models;
using Stack_Solver.ViewModels.Pages;
using System.Windows.Controls;
using System.Windows.Data;
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

        private void skuDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            // Cancel generation of hidden columns
            if (e.PropertyName == nameof(SKU.SkuId) || e.PropertyName == nameof(SKU.Quantity))
            {
                e.Cancel = true;
                return;
            }

            if (e.Column is DataGridBoundColumn boundColumn)
            {
                if (boundColumn.Binding is Binding binding)
                {
                    binding.Mode = BindingMode.TwoWay;
                    binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
                    boundColumn.Binding = binding;
                }
            }
        }

        private void SkuDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
                return;

            if (e.Row.Item is SKU sku)
            {
                if (e.EditingElement is FrameworkElement fe && fe.DataContext == sku)
                {
                    if (fe is TextBox tb && tb.GetBindingExpression(TextBox.TextProperty) is { } be)
                        be.UpdateSource();
                    else if (fe is CheckBox cb && cb.GetBindingExpression(CheckBox.IsCheckedProperty) is { } be2)
                        be2.UpdateSource();
                }

                try
                {
                    if (ViewModel.SaveSkuCommand is IRelayCommand cmd && cmd.CanExecute(sku))
                        cmd.Execute(sku);
                }
                catch
                {

                }
            }
        }
    }
}
