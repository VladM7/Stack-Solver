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
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class LayerAnalyzerViewModel : ObservableObject
    {
        private readonly IEventAggregator _events;
        private readonly LayerSceneBuilder _sceneBuilder = new();
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

        public LayerAnalyzerViewModel(IEventAggregator events)
        {
            _events = events;
            _events.Subscribe<SettingsChangedMessage>(OnSettingsChanged);
            ZoomCommand = new RelayCommand<double>(Zoom);
            BeginPanCommand = new RelayCommand<Point>(BeginPan);
            PanCommand = new RelayCommand<Point>(Pan);
        }

        private int _palletLength;
        private int _palletWidth;
        private double _palletHeight;
        private bool _useCpsat;
        private int _maxCpsatCandidates;
        private int _solverTimeLimit;
        private List<SKU> _selectedSkus = new();

        private void OnSettingsChanged(SettingsChangedMessage msg)
        {
            _palletLength = msg.PalletLength;
            _palletWidth = msg.PalletWidth;
            _palletHeight = msg.PalletHeight;
            _useCpsat = msg.UseCpsat;
            _maxCpsatCandidates = msg.MaxCpsatCandidates;
            _solverTimeLimit = msg.SolverTimeLimit;
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

        private void Zoom(double delta) => ViewportController?.Zoom(delta);
        private void BeginPan(Point p) => ViewportController?.BeginPan(p);
        private void Pan(Point p) => ViewportController?.Pan(p);

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
                    OutputText = "No SKUs with quantity > 0.";
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

                // notify pallet analyzer
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

        private Model3DGroup? _selectionHighlight;

        public void UpdateSelectedItem(PositionedItem? item)
        {
            if (item?.SkuType != null)
            {
                var sku = item.SkuType;
                SelectedItemInfo = $" > {sku.Name} ({sku.Length}x{sku.Width}x{sku.Height}) positioned at {item.X}, {item.Y}";
                HighlightItem(item);
            }
            else
            {
                SelectedItemInfo = string.Empty;
                HighlightItem(null);
            }
        }

        [ObservableProperty]
        private string _selectedItemInfo = string.Empty;

        private void HighlightItem(PositionedItem? item)
        {
            if (_selectionHighlight != null)
            {
                Scene.Children.Remove(_selectionHighlight);
                _selectionHighlight = null;
            }
            if (item == null) return;
            var sku = item.SkuType;
            double boxLength = item.Rotated ? sku.Width : sku.Length;
            double boxWidth = item.Rotated ? sku.Length : sku.Width;
            double boxHeight = sku.Height;
            double inflate = 0.6;
            var origin = new Point3D(item.X - inflate / 2.0, _palletHeight + 0.01, item.Y - inflate / 2.0);
            var fillBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 0));
            var edgeColor = Colors.Yellow;
            _selectionHighlight = GeometryCreator.CreateBoxWithEdges(origin, boxLength + inflate, boxHeight + inflate, boxWidth + inflate, fillBrush, edgeColor, 0.6);
            Scene.Children.Add(_selectionHighlight);
        }

        private void Update2DPreview()
        {
            if (SelectedLayer == null)
            {
                LayerRectangles.Clear();
                return;
            }

            var layer = SelectedLayer;
            var pallet = new Pallet("Pallet", _palletLength, _palletWidth, (int)Math.Round(_palletHeight));
            LayerGeometryBuilder.Build(layer, pallet, 1);

            LayerRectangles.Clear();
            if (layer.Geometry?.ItemRectangles != null)
            {
                double canvasHeight = _palletWidth;
                foreach (var r in layer.Geometry.ItemRectangles)
                {
                    var display = new Rect(r.X, canvasHeight - (r.Y + r.Height), r.Width, r.Height);
                    LayerRectangles.Add(display);
                }
            }
        }
    }

    public record LayersGeneratedMessage(List<Layer> Layers);
}
