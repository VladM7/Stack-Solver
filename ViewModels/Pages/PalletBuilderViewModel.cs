using Stack_Solver.Models;
using System.Collections.ObjectModel;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class PalletBuilderViewModel : ObservableObject
    {
        private bool _isInitialized = false;

        [ObservableProperty]
        private ObservableCollection<SKU> _skus = [
            new SKU { Name = "SKU 1", Length = 10, Width = 5, Height = 2, Weight = 1.5 },
            new SKU { Name = "SKU 2", Length = 15, Width = 10, Height = 5, Weight = 3.0 },
            new SKU { Name = "SKU 3", Length = 20, Width = 15, Height = 10, Weight = 5.0 }
            ];

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void InitializeViewModel()
        {
            _isInitialized = true;
        }
    }
}
