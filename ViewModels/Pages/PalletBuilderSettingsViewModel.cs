using Stack_Solver.Data.Repositories;
using Stack_Solver.Infrastructure;
using Stack_Solver.Models;
using Stack_Solver.Models.Supports;
using System.Collections.ObjectModel;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class PalletBuilderSettingsViewModel : ObservableObject
    {
        private readonly ISkuRepository _skuRepository;
        private readonly IEventAggregator _events;
        private bool _isInitialized;

        [ObservableProperty]
        private ObservableCollection<SKU> _skus = [];

        [ObservableProperty]
        private int _palletLength = 120;

        [ObservableProperty]
        private int _palletWidth = 80;

        [ObservableProperty]
        private double _palletHeight = 14.4;

        [ObservableProperty]
        private bool _useCpsat;

        [ObservableProperty]
        private int _maxCpsatCandidates = 2000;

        [ObservableProperty]
        private int _solverTimeLimit = 60;

        public ObservableCollection<Pallet> CommonPalletsInternational { get; } = [];
        public ObservableCollection<Pallet> CommonPalletsAmerica { get; } = [];

        private Pallet? _selectedInternationalPallet;
        public Pallet? SelectedInternationalPallet
        {
            get => _selectedInternationalPallet;
            set
            {
                if (SetProperty(ref _selectedInternationalPallet, value) && value is not null)
                {
                    SelectPallet(value);
                }
            }
        }

        private Pallet? _selectedAmericanPallet;
        public Pallet? SelectedAmericanPallet
        {
            get => _selectedAmericanPallet;
            set
            {
                if (SetProperty(ref _selectedAmericanPallet, value) && value is not null)
                {
                    SelectPallet(value);
                }
            }
        }

        public PalletBuilderSettingsViewModel(ISkuRepository skuRepository, IEventAggregator events)
        {
            _skuRepository = skuRepository;
            _events = events;
            _skuRepository.SkuAdded += OnSkuAdded;
            _skuRepository.SkuUpdated += OnSkuUpdated;
            _skuRepository.SkuDeleted += OnSkuDeleted;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;
            var list = await _skuRepository.GetAllAsync();
            Skus = new ObservableCollection<SKU>(list);

            if (CommonPalletsInternational.Count == 0)
            {
                foreach (var p in PalletCatalog.International)
                    CommonPalletsInternational.Add(p);
            }

            if (CommonPalletsAmerica.Count == 0)
            {
                foreach (var p in PalletCatalog.America)
                    CommonPalletsAmerica.Add(p);
            }

            _isInitialized = true;
            PublishSettingsChanged();
        }

        [RelayCommand]
        private void SelectPallet(Pallet? pallet)
        {
            if (pallet is null) return;
            PalletLength = pallet.Length;
            PalletWidth = pallet.Width;
            PublishSettingsChanged();
        }

        public async Task UpdateSkuAsync(SKU sku, CancellationToken ct = default)
        {
            if (sku == null) return;
            await _skuRepository.UpdateAsync(sku, ct);
            PublishSettingsChanged();
        }

        partial void OnPalletLengthChanged(int value) => PublishSettingsChanged();
        partial void OnPalletWidthChanged(int value) => PublishSettingsChanged();
        partial void OnPalletHeightChanged(double value) => PublishSettingsChanged();
        partial void OnUseCpsatChanged(bool value) => PublishSettingsChanged();
        partial void OnMaxCpsatCandidatesChanged(int value) => PublishSettingsChanged();
        partial void OnSolverTimeLimitChanged(int value) => PublishSettingsChanged();

        private void PublishSettingsChanged()
        {
            _events.Publish(new SettingsChangedMessage(
                PalletLength, PalletWidth, PalletHeight,
                UseCpsat, MaxCpsatCandidates, SolverTimeLimit, [.. Skus]));
        }

        private void OnSkuAdded(object? sender, SKU sku)
        {
            if (!Skus.Any(s => s.SkuId == sku.SkuId))
            {
                App.Current?.Dispatcher.BeginInvoke(() => Skus.Add(sku));
            }
        }

        private void OnSkuUpdated(object? sender, SKU sku)
        {
            var existing = Skus.FirstOrDefault(s => s.SkuId == sku.SkuId);
            if (existing != null)
            {
                App.Current?.Dispatcher.BeginInvoke(() =>
                {
                    existing.Name = sku.Name;
                    existing.Length = sku.Length;
                    existing.Width = sku.Width;
                    existing.Height = sku.Height;
                    existing.Weight = sku.Weight;
                    existing.Notes = sku.Notes;
                    existing.Rotatable = sku.Rotatable;
                });
            }
            else
            {
                OnSkuAdded(sender, sku);
            }
        }

        private async void OnSkuDeleted(object? sender, string skuId)
        {
            try
            {
                await App.Current!.Dispatcher.InvokeAsync(async () =>
                {
                    var existing = Skus.FirstOrDefault(s => string.Equals(s.SkuId, skuId, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        Skus.Remove(existing);
                    }
                    else
                    {
                        var latest = await _skuRepository.GetAllAsync();
                        SyncSkuCollection(latest);
                    }
                });
            }
            catch { }
        }

        private void SyncSkuCollection(IList<SKU> latest)
        {
            for (int i = Skus.Count - 1; i >= 0; i--)
            {
                if (!latest.Any(s => s.SkuId == Skus[i].SkuId))
                    Skus.RemoveAt(i);
            }
            foreach (var sku in latest)
            {
                var existing = Skus.FirstOrDefault(s => s.SkuId == sku.SkuId);
                if (existing == null)
                {
                    Skus.Add(sku);
                }
                else
                {
                    existing.Name = sku.Name;
                    existing.Length = sku.Length;
                    existing.Width = sku.Width;
                    existing.Height = sku.Height;
                    existing.Weight = sku.Weight;
                    existing.Notes = sku.Notes;
                    existing.Rotatable = sku.Rotatable;
                }
            }
        }
    }

    public record SettingsChangedMessage(
        int PalletLength,
        int PalletWidth,
        double PalletHeight,
        bool UseCpsat,
        int MaxCpsatCandidates,
        int SolverTimeLimit,
        List<SKU> Skus);
}
