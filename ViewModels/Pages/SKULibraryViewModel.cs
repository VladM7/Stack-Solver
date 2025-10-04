using Stack_Solver.Data.Repositories;
using Stack_Solver.Models;
using System.Collections.ObjectModel;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class SKULibraryViewModel : ObservableObject
    {
        private readonly ISkuRepository _skuRepository;
        private bool _isInitialized = false;

        [ObservableProperty]
        private ObservableCollection<SKU> _skus = [];

        public SKULibraryViewModel(ISkuRepository skuRepository)
        {
            _skuRepository = skuRepository;
            _ = InitializeViewModelAsync();
        }

        [RelayCommand]
        private async Task AddSkuAsync()
        {
            var newSku = new SKU
            {
                Name = "New SKU",
                Length = 0,
                Width = 0,
                Height = 0,
                Weight = 0,
                Notes = "",
                Rotatable = true
            };
            await _skuRepository.AddAsync(newSku);
            Skus.Add(newSku);
        }

        [RelayCommand]
        private async Task SaveSkuAsync(SKU sku)
        {
            await _skuRepository.UpdateAsync(sku);
        }

        [RelayCommand]
        private async Task DeleteSkuAsync(SKU sku)
        {
            if (sku == null) return;
            await _skuRepository.DeleteAsync(sku.SkuId);
            Skus.Remove(sku);
        }

        public async Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                await InitializeViewModelAsync();
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private async Task InitializeViewModelAsync()
        {
            var list = await _skuRepository.GetAllAsync();
            Skus = new ObservableCollection<SKU>(list);
            _isInitialized = true;
        }
    }
}
