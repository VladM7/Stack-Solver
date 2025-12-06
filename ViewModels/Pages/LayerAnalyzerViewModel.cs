using Stack_Solver.Helpers.Rendering;
using Stack_Solver.Infrastructure;
using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services;
using Stack_Solver.Services.Layering;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class LayerAnalyzerViewModel : ObservableObject
    {
        private readonly IEventAggregator _events;
        private readonly LayerSceneBuilder _sceneBuilder = new();
        private readonly ILayerVisualizationService _viz;
        private CancellationTokenSource? _sceneBuildCts;
        private CancellationTokenSource? _generationCts;

        [ObservableProperty]
        private string _layerGenStats = "Click on 'Generate' to start layer generation.";

        [ObservableProperty]
        private bool _isGenerating;

        [ObservableProperty]
        private ObservableCollection<Layer> _layers = [];

        [ObservableProperty]
        private Layer? _selectedLayer;

        [ObservableProperty]
        private bool _hasLayers;

        [ObservableProperty]
        private string _outputText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Rect> _layerRectangles = [];

        [ObservableProperty]
        private bool _showGeometryOptimized;

        public bool CanOptimizeGeometry
        {
            get
            {
                var l = SelectedLayer;
                if (l == null) return false;
                var name = l.Name?.ToLowerInvariant() ?? string.Empty;
                bool isBlf = name.Contains("blf");
                bool isStrip = name.Contains("strip");
                return isBlf || isStrip;
            }
        }

        public Model3DGroup Scene { get; } = new();
        private ViewportController? _viewportController;
        public ViewportController? ViewportController => _viewportController;

        private Layer? _optimizedViewLayer;
        private static List<Layer>? _allLayers = [];

        public LayerAnalyzerViewModel(IEventAggregator events, ILayerVisualizationService viz)
        {
            _events = events;
            _viz = viz;
            _events.Subscribe<SettingsChangedMessage>(OnSettingsChanged);
            ZoomCommand = new RelayCommand<double>(Zoom);
            BeginPanCommand = new RelayCommand<Point>(BeginPan);
            PanCommand = new RelayCommand<Point>(Pan);
        }

        public bool TryGetItemFromGeometry(GeometryModel3D geo, out PositionedItem item) => _sceneBuilder.TryGetItemForGeometry(geo, out item);

        private int _palletLength;
        private int _palletWidth;
        private double _palletHeight;
        private bool _useCpsat;
        private int _maxCpsatCandidates;
        private int _solverTimeLimit;
        private int _maxStackHeight;
        private int _maxStackWeight;
        private List<SKU> _selectedSkus = [];

        private void OnSettingsChanged(SettingsChangedMessage msg)
        {
            _palletLength = msg.PalletLength;
            _palletWidth = msg.PalletWidth;
            _palletHeight = msg.PalletHeight;
            _useCpsat = msg.UseCpsat;
            _maxCpsatCandidates = msg.MaxCpsatCandidates;
            _solverTimeLimit = msg.SolverTimeLimit;
            _maxStackHeight = msg.MaxStackHeight;
            _maxStackWeight = msg.MaxStackWeight;
            _selectedSkus = [.. msg.Skus.Where(s => s.Quantity > 0)];
            RecenterCameraTarget();
            if (SelectedLayer != null)
            {
                _ = UpdateSceneForLayerAsync(SelectedLayer);
                Update2DPreview();
            }
        }

        public ICommand ZoomCommand { get; }
        public ICommand BeginPanCommand { get; }
        public ICommand PanCommand { get; }

        private void Zoom(double delta) => _viewportController?.Zoom(delta);
        private void BeginPan(Point p) => _viewportController?.BeginPan(p);
        private void Pan(Point p) => _viewportController?.Pan(p);

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

        private void RecenterCameraTarget()
        {
            if (_viewportController != null)
            {
                _viewportController.Target = CurrentPalletCenter;
            }
        }

        partial void OnSelectedLayerChanged(Layer? value)
        {
            _optimizedViewLayer = null;
            OnPropertyChanged(nameof(CanOptimizeGeometry));
            if (value != null)
            {
                if (!CanOptimizeGeometry && ShowGeometryOptimized)
                    ShowGeometryOptimized = false;

                if (SelectedLayer != null)
                {
                    OutputText = BuildLayerText(SelectedLayer);
                    _ = UpdateSceneForLayerAsync(SelectedLayer);
                    Update2DPreview();
                }
            }
        }

        partial void OnShowGeometryOptimizedChanged(bool value)
        {
            if (SelectedLayer != null)
            {
                _optimizedViewLayer = null;
                _ = UpdateSceneForLayerAsync(SelectedLayer);
                OutputText = BuildLayerText(SelectedLayer);
                Update2DPreview();
            }
        }

        private async Task UpdateSceneForLayerAsync(Layer layer)
        {
            _sceneBuildCts?.Cancel();
            _sceneBuildCts?.Dispose();
            _sceneBuildCts = new CancellationTokenSource();
            var ct = _sceneBuildCts.Token;
            try
            {
                await _sceneBuilder.BuildAsync(Scene, layer, _palletLength, _palletWidth, _palletHeight, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                OutputText = $"Scene build error: {ex.Message}";
            }
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
                if (_selectedSkus.Count == 0)
                {
                    OutputText = "No SKUs with quantity greater than 0.";
                    return;
                }

                var pallet = new Pallet("Pallet", _palletLength, _palletWidth, (int)Math.Round(_palletHeight));
                var options = new GenerationOptions(_solverTimeLimit, _maxCpsatCandidates);
                var ct = localCts.Token;

                var strategiesList = new List<ILayerGenerationStrategy>
                {
                    new BLFGenerationStrategy(),
                    new HomogeneousGenerationStrategy(),
                    new StripFillGenerationStrategy(),
                    new RadialPlacementGenerationStrategy()
                };

                if (_useCpsat)
                {
                    strategiesList.Add(new CPSATGenerationStrategy());
                }
                var strategies = strategiesList.ToArray();

                _allLayers = await Task.Run(() =>
                {
                    var aggregate = new List<Layer>();
                    foreach (var strat in strategies)
                    {
                        if (ct.IsCancellationRequested) break;
                        try
                        {
                            var produced = strat.Generate(_selectedSkus, pallet, options);
                            if (produced != null && produced.Count > 0)
                                aggregate.AddRange(produced);
                        }
                        catch (OperationCanceledException) { throw; }
                        catch { }
                    }
                    return aggregate;
                }, ct);

                if (ct.IsCancellationRequested) return;

                if (_allLayers == null || _allLayers.Count == 0)
                {
                    OutputText = "No layers generated.";
                    return;
                }

                _allLayers = [.. _allLayers
                    .Where(l =>
                        l?.Metadata != null &&
                        !double.IsNaN(l.Metadata.Utilization) &&
                        !double.IsInfinity(l.Metadata.Utilization) &&
                        l.Metadata.Utilization > 0.0 &&
                        l.Metadata.Utilization <= 1.0)];

                foreach (var layer in _allLayers)
                    LayerGeometryOptimizer.CenterLayer(layer);

                var topLayers = _allLayers
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

                LayerGenStats = $"Generated {_allLayers.Count} candidate layers using";
                foreach (var strat in strategies)
                {
                    LayerGenStats += $" {strat.Name},";
                }
                LayerGenStats = LayerGenStats.TrimEnd(',') + ".";

                _events.Publish(new LayersGeneratedMessage(_allLayers));
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
            sb.AppendLine($"{layer.Name}\n");
            sb.AppendLine($"Utilization: {layer.Metadata.Utilization:F3}");
            sb.AppendLine($"Height: {layer.Metadata.Height}");
            double totalWeight = 0;
            foreach (var g in layer.Items)
            {
                totalWeight += g.SkuType.Weight;
            }
            sb.AppendLine($"Total weight: {totalWeight} kg");
            sb.AppendLine($"Total placed items: {layer.Items.Count}");
            foreach (var g in layer.Items.GroupBy(i => i.SkuType.SkuId))
            {
                var sku = g.First().SkuType;
                sb.AppendLine($"  {sku.Name} x {g.Count()} [{sku.Length}x{sku.Width}x{sku.Height}]");
            }
            sb.AppendLine("==================");
            sb.AppendLine("Full details are included in the PDF report.");

            return sb.ToString();
        }

        [ObservableProperty]
        private string _selectedItemInfo = string.Empty;

        public void UpdateSelectedItem(PositionedItem? item)
        {
            if (item?.SkuType != null)
            {
                var sku = item.SkuType;
                SelectedItemInfo = $" > {sku.Name} ({sku.Length}x{sku.Width}x{sku.Height}) positioned at {item.X}, {item.Y}";
                _viz.HighlightItem(Scene, item, _palletHeight);
            }
            else
            {
                SelectedItemInfo = string.Empty;
                _viz.HighlightItem(Scene, null, _palletHeight);
            }
        }

        private void Update2DPreview()
        {
            if (SelectedLayer == null)
            {
                LayerRectangles.Clear();
                return;
            }
            var pallet = new Pallet("Pallet", _palletLength, _palletWidth, (int)Math.Round(_palletHeight));
            _viz.Build2DRectangles(SelectedLayer, pallet, 1, LayerRectangles);
        }
    }

    public record LayersGeneratedMessage(List<Layer> Layers);
}
