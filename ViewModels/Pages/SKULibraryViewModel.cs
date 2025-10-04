using Stack_Solver.Models;
using System.Collections.ObjectModel;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class SKULibraryViewModel : ObservableObject
    {
        private bool _isInitialized = false;

        [ObservableProperty]
        private ObservableCollection<SKU> _skus = [];

        public SKULibraryViewModel()
        {
            InitializeViewModel();
        }

        [RelayCommand]
        private void AddSku()
        {
            var newSku = new SKU();
            Skus.Add(newSku);
        }

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void InitializeViewModel()
        {
            Skus = SKU.LoadSKUs();

            _isInitialized = true;
        }

        public void SaveSkus()
        {
            SKU.SaveSKUs(Skus);
        }
    }
}
