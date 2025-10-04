using Stack_Solver.Data.Repositories;
using Stack_Solver.Models;
using System.Collections.ObjectModel;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class PalletBuilderViewModel : ObservableObject
    {
        private readonly ISkuRepository _skuRepository;
        private bool _isInitialized = false;

        [ObservableProperty]
        private ObservableCollection<SKU> _skus = [];

        public PalletBuilderViewModel(ISkuRepository skuRepository)
        {
            _skuRepository = skuRepository;
            _ = InitializeAsync();
        }

        public async Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                await InitializeAsync();
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private async Task InitializeAsync()
        {
            var list = await _skuRepository.GetAllAsync();
            Skus = new ObservableCollection<SKU>(list);
            _isInitialized = true;
        }

        public async Task UpdateSkuAsync(SKU sku, CancellationToken ct = default)
        {
            if (sku == null) return;
            await _skuRepository.UpdateAsync(sku, ct);
        }
    }
}
