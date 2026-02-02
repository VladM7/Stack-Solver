using FluentValidation;
using Stack_Solver.Data.Repositories;
using Stack_Solver.Models;
using Stack_Solver.Models.Inputs;
using System.Collections.ObjectModel;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class SKULibraryViewModel : ObservableObject
    {
        private readonly ISkuRepository _skuRepository;
        private readonly IValidator<SkuInputDto> _skuValidator;
        private bool _isInitialized = false;

        [ObservableProperty]
        private ObservableCollection<SKU> _skus = [];

        public SKULibraryViewModel(ISkuRepository skuRepository, IValidator<SkuInputDto> skuValidator)
        {
            _skuRepository = skuRepository;
            _skuValidator = skuValidator;
            _ = InitializeViewModelAsync();
        }

        [RelayCommand]
        private async Task AddSkuAsync()
        {
            var newSku = new SKU
            {
                Name = "New SKU",
                Length = 1,
                Width = 1,
                Height = 1,
                Weight = 0,
                Notes = "",
                Rotatable = true
            };
            ValidateSku(newSku);
            await _skuRepository.AddAsync(newSku);
            Skus.Add(newSku);
        }

        [RelayCommand]
        private async Task SaveSkuAsync(SKU sku)
        {
            ValidateSku(sku);
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

        public static Task OnNavigatedFromAsync() => Task.CompletedTask;

        private async Task InitializeViewModelAsync()
        {
            var list = await _skuRepository.GetAllAsync();
            Skus = new ObservableCollection<SKU>(list);
            _isInitialized = true;
        }

        private void ValidateSku(SKU sku)
        {
            if (sku == null) return;
            var dto = new SkuInputDto
            {
                Name = sku.Name,
                Length = sku.Length,
                Width = sku.Width,
                Height = sku.Height,
                Weight = sku.Weight,
                Rotatable = sku.Rotatable,
                Notes = sku.Notes
            };
            var result = _skuValidator.Validate(dto);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }
    }
}
