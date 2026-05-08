using Stack_Solver.Helpers.Rendering;
using Stack_Solver.Infrastructure;
using Stack_Solver.Models;
using Stack_Solver.Models.Assignment;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class PalletAnalyzerViewModel : ObservableObject
    {
        private readonly IEventAggregator _events;
        private readonly PalletSceneBuilder _sceneBuilder = new();
        private CancellationTokenSource? _buildCts;
        private CancellationTokenSource? _sceneCts;
        private ViewportController? _viewportController;

        private List<Layer> _availableLayers = [];
        private int _palletLength, _palletWidth, _palletHeight;
        private int _maxStackHeight = 180, _maxStackWeight = 950;
        private double _maxSkuOverhang;
        private List<SKU> _selectedSkus = [];
        private GenerationOptions _generationOptions = new();

        [ObservableProperty]
        private bool _isBuilding;

        [ObservableProperty]
        private string _outputText = "Click 'Generate' to start.";

        [ObservableProperty]
        private ObservableCollection<TemplateAssignmentDisplay> _assignments = [];

        [ObservableProperty]
        private TemplateAssignmentDisplay? _selectedAssignment;

        [ObservableProperty]
        private bool _hasResults;

        [ObservableProperty]
        private bool _hasLayers;

        [ObservableProperty]
        private ObservableCollection<LayerTypeDisplay> _selectedLayerTypes = [];

        [ObservableProperty]
        private ObservableCollection<SolutionDisplay> _solutions = [];

        public Model3DGroup Scene { get; } = new();
        public ViewportController? ViewportController => _viewportController;
        public ICommand ZoomCommand { get; }
        public ICommand BeginPanCommand { get; }
        public ICommand PanCommand { get; }

        public PalletAnalyzerViewModel(IEventAggregator events)
        {
            _events = events;
            _events.Subscribe<LayersGeneratedMessage>(OnLayersGenerated);
            _events.Subscribe<SettingsChangedMessage>(OnSettingsChanged);
            ZoomCommand = new RelayCommand<double>(delta => _viewportController?.Zoom(delta));
            BeginPanCommand = new RelayCommand<Point>(p => _viewportController?.BeginPan(p));
            PanCommand = new RelayCommand<Point>(p => _viewportController?.Pan(p));
        }

        public void AttachCamera(PerspectiveCamera camera)
        {
            if (camera == null) return;
            if (_viewportController == null)
            {
                _viewportController = new ViewportController(camera, CurrentPalletCenter);
                OnPropertyChanged(nameof(ViewportController));
            }
            else
            {
                _viewportController.Target = CurrentPalletCenter;
            }
        }

        private Point3D CurrentPalletCenter => new(_palletLength / 2.0, 0, _palletWidth / 2.0);

        private void OnLayersGenerated(LayersGeneratedMessage msg)
        {
            _availableLayers = msg.Layers;
            HasLayers = _availableLayers.Count > 0;
            HasResults = false;
            Assignments.Clear();
            Solutions.Clear();
            OutputText = $"{_availableLayers.Count} candidate layers ready. Building pallets...";

            _buildCts?.Cancel();
            _buildCts?.Dispose();
            _buildCts = new CancellationTokenSource();
            _ = BuildPalletsAsync(_buildCts.Token);
        }

        private void OnSettingsChanged(SettingsChangedMessage msg)
        {
            _palletLength = msg.PalletLength;
            _palletWidth = msg.PalletWidth;
            _palletHeight = (int)Math.Round(msg.PalletHeight);
            _maxStackHeight = msg.MaxStackHeight;
            _maxStackWeight = msg.MaxStackWeight;
            _maxSkuOverhang = msg.MaxSkuOverhang;
            _selectedSkus = [.. msg.Skus.Where(s => s.IsSelected && s.Quantity > 0)];
            _generationOptions = new GenerationOptions(msg.SolverTimeLimit, msg.MaxCpsatCandidates, msg.BlfAttempts);
            if (_viewportController != null)
                _viewportController.Target = CurrentPalletCenter;
        }

        private async Task BuildPalletsAsync(CancellationToken ct)
        {
            if (_availableLayers.Count == 0 || _selectedSkus.Count == 0)
            {
                OutputText = _availableLayers.Count == 0
                    ? "No layers available."
                    : "No SKUs selected with quantity > 0.";
                return;
            }

            IsBuilding = true;
            try
            {
                var pallet = new Pallet("Pallet", _palletLength, _palletWidth, _palletHeight)
                {
                    MaxStackHeight = _maxStackHeight,
                    MaxStackWeight = _maxStackWeight,
                    MaxSkuOverhang = _maxSkuOverhang
                };
                var demand = _selectedSkus.ToDictionary(s => s.SkuId, s => s.Quantity, StringComparer.Ordinal);
                var options = _generationOptions;
                var skus = _selectedSkus.ToList();
                var layersSnapshot = _availableLayers.ToList();

                var result = await Task.Run(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    var filtered = LayerMetricsCalculator.FilterLayers(layersSnapshot, options);
                    return GreedyAssignmentService.Assign(filtered, demand, pallet);
                }, ct);

                ct.ThrowIfCancellationRequested();

                if (result.HasLeftovers)
                {
                    var tailInput = result;
                    result = await Task.Run(() =>
                    {
                        var leftoverSkus = BuildLeftoverSkus(skus, tailInput.Leftovers);
                        if (leftoverSkus.Count == 0) return tailInput;
                        var tailLayers = LayerGenerator.Generate(leftoverSkus, pallet, options, ct: ct);
                        if (tailLayers.Count == 0) return tailInput;
                        var tailOptions = new GenerationOptions(options.MaxSolverTime, options.MaxCPSATCandidates, options.BLFAttempts)
                        {
                            MaxLayerStability = options.MaxLayerStability,
                            PerSkuTopLayerFraction = 1.0
                        };
                        var filtered = LayerMetricsCalculator.FilterLayers(tailLayers, tailOptions);
                        if (filtered.Count == 0) return tailInput;
                        return MergeResults(tailInput, GreedyAssignmentService.Assign(filtered, tailInput.Leftovers, pallet));
                    }, ct);
                }

                ct.ThrowIfCancellationRequested();

                int index = 1;
                foreach (var (template, count) in result.Assignments)
                    Assignments.Add(new TemplateAssignmentDisplay(template, count, skus, index++, _palletLength, _palletWidth, _palletHeight));

                HasResults = Assignments.Count > 0;
                SelectedAssignment = Assignments.FirstOrDefault();
                if (HasResults)
                    Solutions.Add(new SolutionDisplay(1, result));
                OutputText = BuildSummaryText(result, skus);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                OutputText = $"Error: {ex.Message}";
            }
            finally
            {
                IsBuilding = false;
            }
        }

        partial void OnSelectedAssignmentChanged(TemplateAssignmentDisplay? value)
        {
            SelectedLayerTypes = value != null
                ? new ObservableCollection<LayerTypeDisplay>(value.LayerTypes)
                : [];

            if (value != null && _viewportController != null)
            {
                double totalHeight = _palletHeight + value.Template.TotalHeight;
                var center = new Point3D(_palletLength / 2.0, totalHeight / 2.0, _palletWidth / 2.0);
                double distance = Math.Sqrt(_palletLength * _palletLength + _palletWidth * _palletWidth + totalHeight * totalHeight) * 1.5;
                _viewportController.ResetView(center, distance);
            }

            _sceneCts?.Cancel();
            _sceneCts?.Dispose();
            _sceneCts = new CancellationTokenSource();
            var ct = _sceneCts.Token;

            if (value != null)
                _ = UpdateSceneAsync(value.Template, ct);
            else
                Scene.Children.Clear();
        }

        private async Task UpdateSceneAsync(PalletTemplate template, CancellationToken ct)
        {
            try
            {
                await _sceneBuilder.BuildAsync(Scene, template, _palletLength, _palletWidth, _palletHeight, ct);
            }
            catch (OperationCanceledException) { }
        }

        private static AssignmentResult MergeResults(AssignmentResult main, AssignmentResult extra)
        {
            var merged = main.Assignments.ToList();
            foreach (var (template, count) in extra.Assignments)
            {
                var idx = merged.FindIndex(a => a.Template.Id == template.Id);
                if (idx >= 0)
                    merged[idx] = (template, merged[idx].Count + count);
                else
                    merged.Add((template, count));
            }
            return new AssignmentResult { Assignments = merged, Leftovers = extra.Leftovers };
        }

        private static List<SKU> BuildLeftoverSkus(List<SKU> originalSkus, IReadOnlyDictionary<string, int> leftovers)
        {
            return [.. originalSkus
                .Select(s => (Sku: s, Rem: leftovers.GetValueOrDefault(s.SkuId)))
                .Where(x => x.Rem > 0)
                .Select(x => new SKU
                {
                    SkuId = x.Sku.SkuId,
                    Name = x.Sku.Name,
                    Length = x.Sku.Length,
                    Width = x.Sku.Width,
                    Height = x.Sku.Height,
                    Weight = x.Sku.Weight,
                    Rotatable = x.Sku.Rotatable,
                    Notes = x.Sku.Notes,
                    Quantity = x.Rem
                })];
        }

        private static string BuildSummaryText(AssignmentResult result, List<SKU> skus)
        {
            var skuMap = skus.ToDictionary(s => s.SkuId, s => s.Name, StringComparer.Ordinal);
            var sb = new StringBuilder();
            sb.AppendLine($"Total pallets: {result.TotalPallets}");
            sb.AppendLine($"Distinct templates: {result.Assignments.Count}");
            //if (result.HasLeftovers)
            //{
            //    sb.AppendLine();
            //    sb.AppendLine("Leftover boxes:");
            //    foreach (var (skuId, count) in result.Leftovers)
            //        sb.AppendLine($"  {skuMap.GetValueOrDefault(skuId, skuId)}: {count}");
            //}
            //else
            //{
            //    sb.AppendLine("All boxes packed.");
            //}
            return sb.ToString();
        }
    }

    public class TemplateAssignmentDisplay
    {
        public PalletTemplate Template { get; }
        public int Count { get; }
        public string Name { get; }
        public string SkuSummary { get; }
        public string Contents { get; }
        public string LoadDimensions { get; }
        public string Weight { get; }
        public string Efficiency { get; }
        public IReadOnlyList<LayerTypeDisplay> LayerTypes { get; }

        public TemplateAssignmentDisplay(PalletTemplate template, int count, IEnumerable<SKU> skuLookup, int index, int palletLength, int palletWidth, int palletHeight)
        {
            Template = template;
            Count = count;
            Name = $"Type {index}";
            Efficiency = template.AverageLayerUtilization.ToString("P0");
            Weight = template.TotalWeight.ToString("N0");
            LoadDimensions = $"{palletLength}x{palletWidth}x{(int)Math.Round(palletHeight + template.TotalHeight)}";

            var skuMap = skuLookup.ToDictionary(s => s.SkuId, s => s.Name, StringComparer.Ordinal);
            SkuSummary = string.Join("  |  ", template.SkuCounts
                .OrderBy(kvp => skuMap.GetValueOrDefault(kvp.Key, kvp.Key), StringComparer.Ordinal)
                .Select(kvp => $"{skuMap.GetValueOrDefault(kvp.Key, kvp.Key)} ×{kvp.Value}"));
            Contents = string.Join(", ", template.SkuCounts
                .OrderBy(kvp => skuMap.GetValueOrDefault(kvp.Key, kvp.Key), StringComparer.Ordinal)
                .Select(kvp => $"{kvp.Value}x {skuMap.GetValueOrDefault(kvp.Key, kvp.Key)}"));
            LayerTypes = [.. template.Layers
                .GroupBy(l => l.Id)
                .Select(g => new LayerTypeDisplay(g.First(), g.Count(), skuMap))];
        }
    }

    public class LayerTypeDisplay(Layer layer, int count, IReadOnlyDictionary<string, string> skuNames)
    {
        public string Name { get; } = layer.Name;
        public int Count { get; } = count;
        public string Contents { get; } = string.Join(", ", layer.Items
                .GroupBy(i => i.SkuType.SkuId)
                .Select(g => $"{g.Count()}x {skuNames.GetValueOrDefault(g.Key, g.Key)}"));
        public string Utilization { get; } = layer.Metadata.Utilization.ToString("P0");
    }

    public class SolutionDisplay
    {
        public int Number { get; }
        public string Name { get; }
        public int TotalPallets { get; }
        public int PalletTypes { get; }
        public int TotalItemsPacked { get; }
        public string Efficiency { get; }
        public bool IsActive { get; set; } = true;

        public SolutionDisplay(int number, AssignmentResult result)
        {
            Number = number;
            Name = "Greedy";
            TotalPallets = result.TotalPallets;
            PalletTypes = result.Assignments.Count;
            TotalItemsPacked = result.Assignments.Sum(a => a.Template.TotalBoxCount * a.Count);
            var weightedUtil = result.TotalPallets > 0
                ? result.Assignments.Sum(a => a.Template.AverageLayerUtilization * a.Count) / result.TotalPallets
                : 0;
            Efficiency = weightedUtil.ToString("P0");
        }
    }
}
