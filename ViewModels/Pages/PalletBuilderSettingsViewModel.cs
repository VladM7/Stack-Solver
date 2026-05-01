using FluentValidation;
using Microsoft.Extensions.Options;
using Stack_Solver.Data.Repositories;
using Stack_Solver.Infrastructure;
using Stack_Solver.Models;
using Stack_Solver.Models.Inputs;
using Stack_Solver.Models.Supports;
using Stack_Solver.Validation;
using System.Collections.ObjectModel;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class PalletBuilderSettingsViewModel : ObservableObject
    {
        private readonly ISkuRepository _skuRepository;
        private readonly IEventAggregator _events;
        private readonly IValidator<PalletSettingsDto> _settingsValidator;
        private readonly IValidator<SkuQuantityDto> _skuQuantityValidator;
        private readonly IUserSettingsService _userSettings;
        private readonly GenerationOptions _defaults;
        private readonly PalletDefaultsOptions _palletDefaults;
        private bool _isInitialized;

        [ObservableProperty]
        private ObservableCollection<SKU> _skus = [];

        [ObservableProperty]
        private int _palletLength;

        [ObservableProperty]
        private int _palletWidth;

        [ObservableProperty]
        private double _palletHeight;

        [ObservableProperty]
        private bool _useCpsat;

        [ObservableProperty]
        private int _maxCpsatCandidates;

        [ObservableProperty]
        private int _blfAttempts;

        [ObservableProperty]
        private int _solverTimeLimit;

        [ObservableProperty]
        private int _maxStackHeight;

        [ObservableProperty]
        private int _maxStackWeight;

        private double _maxSkuOverhang;
        public double MaxSkuOverhang
        {
            get => _maxSkuOverhang;
            set
            {
                if (SetProperty(ref _maxSkuOverhang, value))
                {
                    PublishSettingsChanged();
                }
            }
        }

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

        public PalletBuilderSettingsViewModel(
            ISkuRepository skuRepository,
            IEventAggregator events,
            IOptions<GenerationOptions> genOptions,
            IOptions<PalletDefaultsOptions> palletDefaults,
            IValidator<PalletSettingsDto> settingsValidator,
            IValidator<SkuQuantityDto> skuQuantityValidator,
            IUserSettingsService userSettings)
        {
            _skuRepository = skuRepository;
            _events = events;
            _settingsValidator = settingsValidator;
            _skuQuantityValidator = skuQuantityValidator;
            _userSettings = userSettings;
            _defaults = GenerationOptions.From(genOptions.Value);
            _palletDefaults = palletDefaults.Value ?? new PalletDefaultsOptions();
            _skuRepository.SkuAdded += OnSkuAdded;
            _skuRepository.SkuUpdated += OnSkuUpdated;
            _skuRepository.SkuDeleted += OnSkuDeleted;

            SolverTimeLimit = _defaults.MaxSolverTime;
            MaxCpsatCandidates = _defaults.MaxCPSATCandidates;
            BlfAttempts = _defaults.BLFAttempts;

            PalletLength = _palletDefaults.PalletLength;
            PalletWidth = _palletDefaults.PalletWidth;
            PalletHeight = _palletDefaults.PalletHeight;

            MaxStackHeight = _palletDefaults.MaxStackHeight;
            MaxStackWeight = _palletDefaults.MaxStackWeight;
            MaxSkuOverhang = _palletDefaults.MaxSkuOverhang;
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

            if (!string.IsNullOrWhiteSpace(_palletDefaults.DefaultPalletName))
            {
                if (string.Equals(_palletDefaults.DefaultCatalog, "America", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedAmericanPallet = CommonPalletsAmerica.FirstOrDefault(p => string.Equals(p.Name, _palletDefaults.DefaultPalletName, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    SelectedInternationalPallet = CommonPalletsInternational.FirstOrDefault(p => string.Equals(p.Name, _palletDefaults.DefaultPalletName, StringComparison.OrdinalIgnoreCase));
                }
            }

            SolverTimeLimit = _defaults.MaxSolverTime;
            MaxCpsatCandidates = _defaults.MaxCPSATCandidates;

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
            var dto = new SkuQuantityDto
            {
                SkuId = sku.SkuId,
                Quantity = sku.Quantity
            };
            var result = _skuQuantityValidator.Validate(dto);
            if (!result.IsValid)
            {
                throw new ValidationException(ValidationErrorFormatter.Format(result.Errors));
            }
            await _skuRepository.UpdateAsync(sku, ct);
            PublishSettingsChanged();
        }

        partial void OnPalletLengthChanged(int value) => PublishSettingsChanged();
        partial void OnPalletWidthChanged(int value) => PublishSettingsChanged();
        partial void OnPalletHeightChanged(double value) => PublishSettingsChanged();
        partial void OnUseCpsatChanged(bool value) => PublishSettingsChanged();
        partial void OnMaxCpsatCandidatesChanged(int value) => PublishSettingsChanged();
        partial void OnBlfAttemptsChanged(int value) => PublishSettingsChanged();
        partial void OnSolverTimeLimitChanged(int value) => PublishSettingsChanged();
        partial void OnMaxStackHeightChanged(int value) => PublishSettingsChanged();
        partial void OnMaxStackWeightChanged(int value) => PublishSettingsChanged();

        private void PublishSettingsChanged()
        {
            var dto = new PalletSettingsDto
            {
                PalletLength = PalletLength,
                PalletWidth = PalletWidth,
                PalletHeight = PalletHeight,
                UseCpsat = UseCpsat,
                MaxCpsatCandidates = MaxCpsatCandidates,
                BlfAttempts = BlfAttempts,
                SolverTimeLimit = SolverTimeLimit,
                MaxStackHeight = MaxStackHeight,
                MaxStackWeight = MaxStackWeight,
                MaxSkuOverhang = MaxSkuOverhang
            };
            var result = _settingsValidator.Validate(dto);
            if (!result.IsValid)
            {
                var _ = ValidationErrorFormatter.Format(result.Errors);
                return;
            }
            _events.Publish(new SettingsChangedMessage(
                PalletLength, PalletWidth, PalletHeight,
                UseCpsat, MaxCpsatCandidates, BlfAttempts, SolverTimeLimit,
                MaxStackHeight, MaxStackWeight, MaxSkuOverhang,
                [.. Skus]));

            if (_isInitialized)
            {
                var palletOpts = new PalletDefaultsOptions
                {
                    DefaultCatalog = _palletDefaults.DefaultCatalog,
                    DefaultPalletName = _palletDefaults.DefaultPalletName,
                    PalletLength = PalletLength,
                    PalletWidth = PalletWidth,
                    PalletHeight = PalletHeight,
                    MaxStackHeight = MaxStackHeight,
                    MaxStackWeight = MaxStackWeight,
                    MaxSkuOverhang = MaxSkuOverhang
                };
                var genOpts = new GenerationOptions
                {
                    MaxSolverTime = SolverTimeLimit,
                    MaxCPSATCandidates = MaxCpsatCandidates,
                    BLFAttempts = BlfAttempts,
                    MaxLayerStability = _defaults.MaxLayerStability,
                    PerSkuTopLayerFraction = _defaults.PerSkuTopLayerFraction
                };
                _ = _userSettings.SaveAsync(palletOpts, genOpts);
            }
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
        int BlfAttempts,
        int SolverTimeLimit,
        int MaxStackHeight,
        int MaxStackWeight,
        double MaxSkuOverhang,
        List<SKU> Skus);
}
