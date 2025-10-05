using Stack_Solver.Data.Repositories;
using Stack_Solver.Models;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.Strategies;
using System.Collections.ObjectModel;
using System.Text;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class PalletBuilderViewModel : ObservableObject
    {
        private readonly ISkuRepository _skuRepository;
        private bool _isInitialized = false;
        private CancellationTokenSource? _generationCts;

        [ObservableProperty]
        private ObservableCollection<SKU> _skus = [];

        [ObservableProperty]
        private int _palletLength = 120;

        [ObservableProperty]
        private int _palletWidth = 80;

        [ObservableProperty]
        private double _palletHeight = 14.4;

        [ObservableProperty]
        private string _outputText = "Output will be visible here.";

        [ObservableProperty]
        private bool _isGenerating;

        [ObservableProperty]
        private ObservableCollection<Layer> _layers = [];

        [ObservableProperty]
        private Layer? _selectedLayer;

        [ObservableProperty]
        private bool _hasLayers;

        public ObservableCollection<Pallet> CommonPalletsInternational { get; } = [];
        public ObservableCollection<Pallet> CommonPalletsAmerica { get; } = [];

        partial void OnSelectedLayerChanged(Layer? value)
        {
            if (value != null)
            {
                OutputText = BuildLayerText(value);
            }
        }

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

        public static Task OnNavigatedFromAsync() => Task.CompletedTask;

        private async Task InitializeAsync()
        {
            var list = await _skuRepository.GetAllAsync();
            Skus = new ObservableCollection<SKU>(list);

            if (CommonPalletsInternational.Count == 0)
            {
                CommonPalletsInternational.Add(new Pallet("EUR (EPAL 1/ISO1)", 120, 80, 0));
                CommonPalletsInternational.Add(new Pallet("EUR 2 (ISO2)", 120, 100, 0));
                CommonPalletsInternational.Add(new Pallet("EUR 3", 100, 120, 0));
                CommonPalletsInternational.Add(new Pallet("UPL Pallet", 120, 110, 0));
                CommonPalletsInternational.Add(new Pallet("HPL (Half Pallet, EUR 6/ISO0)", 60, 80, 0));
                CommonPalletsInternational.Add(new Pallet("QPL (Quarter Pallet)", 60, 40, 0));
                CommonPalletsInternational.Add(new Pallet("ASIA Standard", 110, 110, 0));
                CommonPalletsInternational.Add(new Pallet("AUS Standard", 117, 117, 0));
            }

            if (CommonPalletsAmerica.Count == 0)
            {
                CommonPalletsAmerica.Add(new Pallet("US Standard", 48, 40, 0));
                CommonPalletsAmerica.Add(new Pallet("US Square Pallet 42x42", 42, 42, 0));
                CommonPalletsAmerica.Add(new Pallet("US Square Pallet 48x48", 48, 48, 0));
            }

            _isInitialized = true;
        }

        [RelayCommand]
        private void SelectPallet(Pallet? pallet)
        {
            if (pallet is null) return;
            PalletLength = pallet.Length;
            PalletWidth = pallet.Width;
        }

        public async Task UpdateSkuAsync(SKU sku, CancellationToken ct = default)
        {
            if (sku == null) return;
            await _skuRepository.UpdateAsync(sku, ct);
        }

        [RelayCommand]
        private async Task Generate()
        {
            if (IsGenerating)
                return;

            var localCts = new CancellationTokenSource();
            _generationCts = localCts;
            IsGenerating = true;
            HasLayers = false;
            Layers.Clear();
            SelectedLayer = null;
            OutputText = "Generating...";
            try
            {
                var selectedSkus = Skus.Where(s => s.Quantity > 0).ToList();
                if (selectedSkus.Count == 0)
                {
                    OutputText = "No SKUs with quantity > 0.";
                    return;
                }

                var pallet = new Pallet("Pallet", PalletLength, PalletWidth, (int)Math.Round(PalletHeight));
                var strategy = new BLFGenerationStrategy();
                var options = new GenerationOptions();
                var ct = localCts.Token;

                var layers = await Task.Run(() => strategy.Generate(selectedSkus, pallet, options), ct);
                if (ct.IsCancellationRequested) return;

                if (layers == null || layers.Count == 0)
                {
                    OutputText = "No layers generated.";
                    return;
                }

                var topLayers = layers
                    .OrderByDescending(l => l.Metadata.Utilization)
                    .ThenBy(l => l.Name)
                    .Take(10)
                    .ToList();

                foreach (var layer in topLayers)
                    Layers.Add(layer);

                HasLayers = Layers.Count > 0;

                SelectedLayer = Layers.OrderByDescending(l => l.Metadata.Utilization).FirstOrDefault();

                if (SelectedLayer == null)
                {
                    OutputText = "No layers after filtering.";
                }
            }
            catch (OperationCanceledException)
            {
                OutputText = "Generation canceled.";
            }
            catch (Exception ex)
            {
                OutputText = $"Error: {ex.Message}";
            }
            finally
            {
                IsGenerating = false;
                _generationCts?.Dispose();
                _generationCts = null;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            if (_generationCts != null && !_generationCts.IsCancellationRequested)
            {
                _generationCts.Cancel();
                OutputText = "Canceling...";
            }
        }

        private static string BuildLayerText(Layer layer)
        {
            var sb = new StringBuilder();
            sb.AppendLine("! Disclaimer: The rendering may not accurately reflect reality.\n");
            sb.AppendLine($"Layer: {layer.Name}");
            sb.AppendLine($"Utilization: {layer.Metadata.Utilization:F3}");
            sb.AppendLine($"Height: {layer.Metadata.Height}");
            sb.AppendLine("Items:");
            foreach (var g in layer.Items.GroupBy(i => i.SkuType.SkuId))
            {
                var sku = g.First().SkuType;
                sb.AppendLine($"  {sku.Name} x {g.Count()} [{sku.Length}x{sku.Width}x{sku.Height}]");
            }
            sb.AppendLine("Placements (first 20):");
            foreach (var item in layer.Items.Take(20))
            {
                sb.AppendLine($"  {item.SkuType.Name} at ({item.X},{item.Y}) Rot={(item.Rotated ? 'Y' : 'N')}");
            }
            if (layer.Items.Count > 20)
                sb.AppendLine($"  ... {layer.Items.Count - 20} more");
            return sb.ToString();
        }
    }
}
