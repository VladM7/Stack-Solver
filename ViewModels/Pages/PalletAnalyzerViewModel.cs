using Stack_Solver.Helpers.Rendering;
using Stack_Solver.Infrastructure;
using Stack_Solver.Models;
using Stack_Solver.Models.Assignment;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services;
using Stack_Solver.Services.Stacking;
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
            _selectedSkus = [.. msg.Skus.Where(s => s.Quantity > 0)];
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
                    : "No SKUs with quantity > 0.";
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

                var (templates, result) = await Task.Run(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    var filtered = LayerMetricsCalculator.FilterLayers(layersSnapshot, options);
                    var tmpl = PalletTemplateEnumerator.Enumerate(pallet, filtered, options);
                    var assignment = GreedyAssignmentService.Assign(tmpl, demand);
                    return (tmpl, assignment);
                }, ct);

                ct.ThrowIfCancellationRequested();

                int index = 1;
                foreach (var (template, count) in result.Assignments)
                    Assignments.Add(new TemplateAssignmentDisplay(template, count, skus, index++));

                HasResults = Assignments.Count > 0;
                SelectedAssignment = Assignments.FirstOrDefault();
                if (HasResults)
                    Solutions.Add(new SolutionDisplay(1, result));
                OutputText = BuildSummaryText(result, templates.Count, skus);
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

        private string BuildSummaryText(AssignmentResult result, int templateCount, List<SKU> skus)
        {
            var skuMap = skus.ToDictionary(s => s.SkuId, s => s.Name, StringComparer.Ordinal);
            var sb = new StringBuilder();
            sb.AppendLine($"Templates generated: {templateCount}");
            sb.AppendLine($"Total pallets:       {result.TotalPallets}");
            sb.AppendLine($"Distinct templates:  {result.Assignments.Count}");
            if (result.HasLeftovers)
            {
                sb.AppendLine();
                sb.AppendLine("Leftover boxes:");
                foreach (var (skuId, count) in result.Leftovers)
                    sb.AppendLine($"  {skuMap.GetValueOrDefault(skuId, skuId)}: {count}");
            }
            else
            {
                sb.AppendLine("All boxes packed.");
            }
            return sb.ToString();
        }
    }

    public class TemplateAssignmentDisplay
    {
        public PalletTemplate Template { get; }
        public int Count { get; }
        public string Name { get; }
        public string SkuSummary { get; }
        public string Efficiency { get; }
        public IReadOnlyList<LayerTypeDisplay> LayerTypes { get; }

        public TemplateAssignmentDisplay(PalletTemplate template, int count, IEnumerable<SKU> skuLookup, int index)
        {
            Template = template;
            Count = count;
            Name = $"Type {index}";
            Efficiency = template.AverageLayerUtilization.ToString("P0");

            var skuMap = skuLookup.ToDictionary(s => s.SkuId, s => s.Name, StringComparer.Ordinal);
            SkuSummary = string.Join("  |  ", template.SkuCounts
                .OrderBy(kvp => skuMap.GetValueOrDefault(kvp.Key, kvp.Key), StringComparer.Ordinal)
                .Select(kvp => $"{skuMap.GetValueOrDefault(kvp.Key, kvp.Key)} ×{kvp.Value}"));
            LayerTypes = template.Layers
                .GroupBy(l => l.Id)
                .Select(g => new LayerTypeDisplay(g.First(), g.Count(), skuMap))
                .ToList();
        }
    }

    public class LayerTypeDisplay
    {
        public string Name { get; }
        public int Count { get; }
        public string Contents { get; }
        public string Utilization { get; }

        public LayerTypeDisplay(Layer layer, int count, IReadOnlyDictionary<string, string> skuNames)
        {
            Name = layer.Name;
            Count = count;
            Utilization = layer.Metadata.Utilization.ToString("P0");
            Contents = string.Join(", ", layer.Items
                .GroupBy(i => i.SkuType.SkuId)
                .Select(g => $"{g.Count()}x {skuNames.GetValueOrDefault(g.Key, g.Key)}"));
        }
    }

    public class SolutionDisplay
    {
        public int Number { get; }
        public int TotalPallets { get; }
        public int PalletTypes { get; }
        public int TotalItemsPacked { get; }
        public string Efficiency { get; }
        public bool IsActive { get; set; } = true;

        public SolutionDisplay(int number, AssignmentResult result)
        {
            Number = number;
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
