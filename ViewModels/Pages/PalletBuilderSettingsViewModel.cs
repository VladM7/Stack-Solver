using FluentValidation;
using Microsoft.Extensions.Options;
using Stack_Solver.Data.Repositories;
using Stack_Solver.Infrastructure;
using Stack_Solver.Models;
using Stack_Solver.Models.Inputs;
using Stack_Solver.Models.Layering;
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

        // Last-selected common pallet, persisted so the selection survives a restart.
        // Null once the user types custom dimensions that no longer match any named pallet.
        private string? _defaultCatalog;
        private string? _defaultPalletName;

        // True while restoring persisted settings, so selecting the saved pallet does not
        // clobber the persisted dimensions. True while a pallet selection is applying its
        // dimensions, so that dimension change is not mistaken for a manual edit.
        private bool _isRestoring;
        private bool _isApplyingPalletSelection;

        [ObservableProperty]
        private ObservableCollection<SKU> _skus = [];

        [ObservableProperty]
        private int _palletLength;

        [ObservableProperty]
        private int _palletWidth;

        [ObservableProperty]
        private double _palletHeight;

        /// <summary>Use CP-SAT (rather than the heuristic packer) to generate candidate layers. Distinct from <see cref="UseCpsatSolution"/>.</summary>
        [ObservableProperty]
        private bool _useCpsat;

        /// <summary>Offer a Greedy assignment as one of the produced solutions.</summary>
        [ObservableProperty]
        private bool _useGreedy = true;

        /// <summary>Offer a CP-SAT assignment as one of the produced solutions.</summary>
        [ObservableProperty]
        private bool _useCpsatSolution = true;

        /// <summary>Offer a Branch &amp; Price assignment as one of the produced solutions.</summary>
        [ObservableProperty]
        private bool _useBranchAndPrice = true;

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

        [ObservableProperty]
        private OverhangMode _overhangMode;

        [ObservableProperty]
        private double _maxTopHeavyPercent;

        /// <summary>Choices for the overhang-rule selector; <see cref="MaxSkuOverhang"/> is ignored for Auto.</summary>
        public IReadOnlyList<OverhangModeOption> OverhangModeOptions { get; } =
        [
            new(OverhangMode.AbsoluteCm, "Max overhang per side"),
            new(OverhangMode.MinSupportedPercent, "Min supported"),
            new(OverhangMode.Auto, "Auto"),
        ];

        /// <summary>True when the overhang value field applies (every mode except Auto).</summary>
        public bool IsOverhangValueVisible => OverhangMode != OverhangMode.Auto;

        /// <summary>Header for the overhang value field, reflecting the unit of the selected rule.</summary>
        public string OverhangValueHeader => OverhangMode == OverhangMode.MinSupportedPercent
            ? "Min supported (%)"
            : "Max overhang per side (cm)";

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
                    _defaultCatalog = "International";
                    _defaultPalletName = value.Name;
                    ClearAmericanSelection();
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
                    _defaultCatalog = "America";
                    _defaultPalletName = value.Name;
                    ClearInternationalSelection();
                    SelectPallet(value);
                }
            }
        }

        // Deselect the other catalog's grid (bypassing the setters, so no re-entrancy)
        // so that at most one common pallet is ever highlighted.
        private void ClearInternationalSelection()
        {
            if (_selectedInternationalPallet is null) return;
            _selectedInternationalPallet = null;
            OnPropertyChanged(nameof(SelectedInternationalPallet));
        }

        private void ClearAmericanSelection()
        {
            if (_selectedAmericanPallet is null) return;
            _selectedAmericanPallet = null;
            OnPropertyChanged(nameof(SelectedAmericanPallet));
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
            UseCpsat = _defaults.UseCpsat;
            UseGreedy = _defaults.UseGreedy;
            UseCpsatSolution = _defaults.UseCpsatSolution;
            UseBranchAndPrice = _defaults.UseBranchAndPrice;

            _defaultCatalog = _palletDefaults.DefaultCatalog;
            _defaultPalletName = _palletDefaults.DefaultPalletName;

            PalletLength = _palletDefaults.PalletLength;
            PalletWidth = _palletDefaults.PalletWidth;
            PalletHeight = _palletDefaults.PalletHeight;

            MaxStackHeight = _palletDefaults.MaxStackHeight;
            MaxStackWeight = _palletDefaults.MaxStackWeight;
            MaxSkuOverhang = _palletDefaults.MaxSkuOverhang;
            OverhangMode = _palletDefaults.OverhangMode;
            MaxTopHeavyPercent = _palletDefaults.MaxTopHeavyPercent;
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

            if (!string.IsNullOrWhiteSpace(_defaultPalletName))
            {
                _isRestoring = true;
                try
                {
                    if (string.Equals(_defaultCatalog, "America", StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedAmericanPallet = CommonPalletsAmerica.FirstOrDefault(p => string.Equals(p.Name, _defaultPalletName, StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        SelectedInternationalPallet = CommonPalletsInternational.FirstOrDefault(p => string.Equals(p.Name, _defaultPalletName, StringComparison.OrdinalIgnoreCase));
                    }
                }
                finally
                {
                    _isRestoring = false;
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
            // While restoring, keep the persisted dimensions (which may be user-customised)
            // instead of snapping back to the named pallet's size.
            if (!_isRestoring)
            {
                _isApplyingPalletSelection = true;
                try
                {
                    PalletLength = pallet.Length;
                    PalletWidth = pallet.Width;
                }
                finally
                {
                    _isApplyingPalletSelection = false;
                }
            }
            PublishSettingsChanged();
        }

        public void NotifySelectionChanged() => PublishSettingsChanged();

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

        partial void OnPalletLengthChanged(int value)
        {
            ClearPalletSelectionIfManual();
            PublishSettingsChanged();
        }
        partial void OnPalletWidthChanged(int value)
        {
            ClearPalletSelectionIfManual();
            PublishSettingsChanged();
        }
        partial void OnPalletHeightChanged(double value) => PublishSettingsChanged();
        partial void OnUseCpsatChanged(bool value) => PublishSettingsChanged();
        partial void OnUseGreedyChanged(bool value) => PublishSettingsChanged();
        partial void OnUseCpsatSolutionChanged(bool value) => PublishSettingsChanged();
        partial void OnUseBranchAndPriceChanged(bool value) => PublishSettingsChanged();

        // A hand-typed length/width no longer matches any named pallet, so drop the
        // remembered selection. Ignored when the change came from restore or from
        // applying a pallet's own dimensions.
        private void ClearPalletSelectionIfManual()
        {
            if (_isRestoring || _isApplyingPalletSelection) return;
            _defaultCatalog = null;
            _defaultPalletName = null;
            ClearInternationalSelection();
            ClearAmericanSelection();
        }
        partial void OnMaxCpsatCandidatesChanged(int value) => PublishSettingsChanged();
        partial void OnBlfAttemptsChanged(int value) => PublishSettingsChanged();
        partial void OnSolverTimeLimitChanged(int value) => PublishSettingsChanged();
        partial void OnMaxStackHeightChanged(int value) => PublishSettingsChanged();
        partial void OnMaxStackWeightChanged(int value) => PublishSettingsChanged();
        partial void OnOverhangModeChanged(OverhangMode value)
        {
            OnPropertyChanged(nameof(IsOverhangValueVisible));
            OnPropertyChanged(nameof(OverhangValueHeader));
            PublishSettingsChanged();
        }
        partial void OnMaxTopHeavyPercentChanged(double value) => PublishSettingsChanged();

        private void PublishSettingsChanged()
        {
            var dto = new PalletSettingsDto
            {
                PalletLength = PalletLength,
                PalletWidth = PalletWidth,
                PalletHeight = PalletHeight,
                UseCpsat = UseCpsat,
                UseGreedy = UseGreedy,
                UseCpsatSolution = UseCpsatSolution,
                UseBranchAndPrice = UseBranchAndPrice,
                MaxCpsatCandidates = MaxCpsatCandidates,
                BlfAttempts = BlfAttempts,
                SolverTimeLimit = SolverTimeLimit,
                MaxStackHeight = MaxStackHeight,
                MaxStackWeight = MaxStackWeight,
                MaxSkuOverhang = MaxSkuOverhang,
                OverhangMode = OverhangMode,
                MaxTopHeavyPercent = MaxTopHeavyPercent
            };
            var result = _settingsValidator.Validate(dto);
            if (!result.IsValid)
            {
                var _ = ValidationErrorFormatter.Format(result.Errors);
                return;
            }
            _events.Publish(new SettingsChangedMessage(
                PalletLength, PalletWidth, PalletHeight,
                UseCpsat, UseGreedy, UseCpsatSolution, UseBranchAndPrice,
                MaxCpsatCandidates, BlfAttempts, SolverTimeLimit,
                MaxStackHeight, MaxStackWeight, MaxSkuOverhang, OverhangMode, MaxTopHeavyPercent,
                [.. Skus], _defaultCatalog, _defaultPalletName));

            if (_isInitialized)
            {
                var palletOpts = new PalletDefaultsOptions
                {
                    DefaultCatalog = _defaultCatalog,
                    DefaultPalletName = _defaultPalletName,
                    PalletLength = PalletLength,
                    PalletWidth = PalletWidth,
                    PalletHeight = PalletHeight,
                    MaxStackHeight = MaxStackHeight,
                    MaxStackWeight = MaxStackWeight,
                    MaxSkuOverhang = MaxSkuOverhang,
                    OverhangMode = OverhangMode,
                    MaxTopHeavyPercent = MaxTopHeavyPercent
                };
                var genOpts = new GenerationOptions
                {
                    MaxSolverTime = SolverTimeLimit,
                    MaxCPSATCandidates = MaxCpsatCandidates,
                    BLFAttempts = BlfAttempts,
                    MaxLayerStability = _defaults.MaxLayerStability,
                    PerSkuTopLayerFraction = _defaults.PerSkuTopLayerFraction,
                    UseCpsat = UseCpsat,
                    UseGreedy = UseGreedy,
                    UseCpsatSolution = UseCpsatSolution,
                    UseBranchAndPrice = UseBranchAndPrice
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
        bool UseGreedy,
        bool UseCpsatSolution,
        bool UseBranchAndPrice,
        int MaxCpsatCandidates,
        int BlfAttempts,
        int SolverTimeLimit,
        int MaxStackHeight,
        int MaxStackWeight,
        double MaxSkuOverhang,
        OverhangMode OverhangMode,
        double MaxTopHeavyPercent,
        List<SKU> Skus,
        string? DefaultCatalog,
        string? DefaultPalletName);

    /// <summary>A selectable overhang rule with a human-readable label for the settings combo box.</summary>
    public record OverhangModeOption(OverhangMode Mode, string Label);
}
