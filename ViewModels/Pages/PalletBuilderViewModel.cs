using Stack_Solver.Data.Repositories;
using Stack_Solver.Helpers.Rendering;
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
    public partial class PalletBuilderViewModel : ObservableObject
    {
        private readonly ISkuRepository _skuRepository;
        private bool _isInitialized = false;
        private CancellationTokenSource? _generationCts;

        private readonly LayerSceneBuilder _sceneBuilder = new();
        private CancellationTokenSource? _sceneBuildCts;

        [ObservableProperty]
        private ObservableCollection<SKU> _skus = [];

        [ObservableProperty]
        private int _palletLength = 120;

        [ObservableProperty]
        private int _palletWidth = 80;

        [ObservableProperty]
        private double _palletHeight = 14.4;

        [ObservableProperty]
        private string _outputText = string.Empty;

        [ObservableProperty]
        private bool _isGenerating;

        [ObservableProperty]
        private ObservableCollection<Layer> _layers = [];

        [ObservableProperty]
        private Layer? _selectedLayer;

        [ObservableProperty]
        private bool _hasLayers;

        [ObservableProperty]
        private bool _useCpsat;

        [ObservableProperty]
        private int _maxCpsatCandidates = 2000;

        [ObservableProperty]
        private int _solverTimeLimit = 60;

        [ObservableProperty]
        private string _layerGenStats = "Click on 'Generate' to start layer generation.";

        [ObservableProperty]
        private string _selectedItemInfo = string.Empty;

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

        // Rectangles for 2D preview (in pallet coordinate units)
        [ObservableProperty]
        private ObservableCollection<Rect> _layerRectangles = [];

        private ViewportController? _viewportController;
        public ViewportController? ViewportController => _viewportController;

        public Model3DGroup Scene { get; } = new();

        private Pallet? _selectedInternationalPallet;
        private Pallet? _selectedAmericanPallet;
        private bool _suppressCrossClear;

        private Layer? _optimizedViewLayer;

        private Point3D CurrentPalletCenter => new(PalletLength / 2.0, 0, PalletWidth / 2.0);

        partial void OnPalletLengthChanged(int value)
        {
            RecenterCameraTarget();
            if (SelectedLayer != null)
            {
                _ = UpdateSceneForLayerAsync(SelectedLayer);
                Update2DPreview();
            }
        }

        partial void OnPalletWidthChanged(int value)
        {
            RecenterCameraTarget();
            if (SelectedLayer != null)
            {
                _ = UpdateSceneForLayerAsync(SelectedLayer);
                Update2DPreview();
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

        private void RecenterCameraTarget()
        {
            if (_viewportController != null)
            {
                _viewportController.Target = CurrentPalletCenter;
            }
        }

        public Pallet? SelectedInternationalPallet
        {
            get => _selectedInternationalPallet;
            set
            {
                if (SetProperty(ref _selectedInternationalPallet, value))
                {
                    if (value != null)
                    {
                        SelectPallet(value);
                        if (!_suppressCrossClear && _selectedAmericanPallet != null)
                        {
                            try
                            {
                                _suppressCrossClear = true;
                                _selectedAmericanPallet = null;
                                OnPropertyChanged(nameof(SelectedAmericanPallet));
                            }
                            finally { _suppressCrossClear = false; }
                        }
                    }
                }
            }
        }

        public Pallet? SelectedAmericanPallet
        {
            get => _selectedAmericanPallet;
            set
            {
                if (SetProperty(ref _selectedAmericanPallet, value))
                {
                    if (value != null)
                    {
                        SelectPallet(value);
                        if (!_suppressCrossClear && _selectedInternationalPallet != null)
                        {
                            try
                            {
                                _suppressCrossClear = true;
                                _selectedInternationalPallet = null;
                                OnPropertyChanged(nameof(SelectedInternationalPallet));
                            }
                            finally { _suppressCrossClear = false; }
                        }
                    }
                }
            }
        }

        public ObservableCollection<Pallet> CommonPalletsInternational { get; } = [];
        public ObservableCollection<Pallet> CommonPalletsAmerica { get; } = [];

        private static List<Layer>? allLayers = [];

        partial void OnSelectedLayerChanged(Layer? value)
        {
            _optimizedViewLayer = null;
            OnPropertyChanged(nameof(CanOptimizeGeometry));
            if (value != null)
            {
                if (!CanOptimizeGeometry && ShowGeometryOptimized)
                    ShowGeometryOptimized = false;

                OutputText = BuildLayerText(SelectedLayer);
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

        public PalletBuilderViewModel(ISkuRepository skuRepository)
        {
            ZoomCommand = new RelayCommand<double>(Zoom);
            BeginPanCommand = new RelayCommand<Point>(BeginPan);
            PanCommand = new RelayCommand<Point>(Pan);

            _skuRepository = skuRepository;
            _skuRepository.SkuAdded += OnSkuAdded;
            _skuRepository.SkuUpdated += OnSkuUpdated;
            _skuRepository.SkuDeleted += OnSkuDeleted;
            _ = InitializeAsync();
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

        private async Task UpdateSceneForLayerAsync(Layer layer)
        {
            _sceneBuildCts?.Cancel();
            _sceneBuildCts?.Dispose();
            _sceneBuildCts = new CancellationTokenSource();
            var ct = _sceneBuildCts.Token;
            try
            {
                await _sceneBuilder.BuildAsync(Scene, layer, PalletLength, PalletWidth, PalletHeight, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                OutputText = $"Scene build error: {ex.Message}";
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
                foreach (var p in PalletCatalog.International)
                    CommonPalletsInternational.Add(p);
            }

            if (CommonPalletsAmerica.Count == 0)
            {
                foreach (var p in PalletCatalog.America)
                    CommonPalletsAmerica.Add(p);
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
                var options = new GenerationOptions(SolverTimeLimit, MaxCpsatCandidates);
                var ct = localCts.Token;

                var strategiesList = new List<ILayerGenerationStrategy>
                {
                    new BLFGenerationStrategy(),
                    new HomogeneousGenerationStrategy(),
                    new StripFillGenerationStrategy(),
                    new RadialPlacementGenerationStrategy()
                };

                if (UseCpsat)
                {
                    strategiesList.Add(new CPSATGenerationStrategy());
                }
                var strategies = strategiesList.ToArray();

                allLayers = await Task.Run(() =>
                {
                    var aggregate = new List<Layer>();
                    foreach (var strat in strategies)
                    {
                        if (ct.IsCancellationRequested) break;
                        try
                        {
                            var produced = strat.Generate(selectedSkus, pallet, options);
                            if (produced != null && produced.Count > 0)
                                aggregate.AddRange(produced);
                        }
                        catch (OperationCanceledException) { throw; }
                        catch { }
                    }
                    return aggregate;
                }, ct);

                if (ct.IsCancellationRequested) return;

                if (allLayers == null || allLayers.Count == 0)
                {
                    OutputText = "No layers generated.";
                    return;
                }

                allLayers = [.. allLayers
                    .Where(l =>
                        l?.Metadata != null &&
                        !double.IsNaN(l.Metadata.Utilization) &&
                        !double.IsInfinity(l.Metadata.Utilization) &&
                        l.Metadata.Utilization > 0.0 &&
                        l.Metadata.Utilization <= 1.0)];

                foreach (var layer in allLayers)
                    LayerGeometryOptimizer.CenterLayer(layer);

                var topLayers = allLayers
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

                LayerGenStats = $"Generated {allLayers.Count} candidate layers using";
                foreach (var strat in strategies)
                {
                    LayerGenStats += $" {strat.Name},";
                }
                LayerGenStats = LayerGenStats.TrimEnd(',') + ".";
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
            var origin = new Point3D(item.X - inflate / 2.0, PalletHeight + 0.01, item.Y - inflate / 2.0);
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
            var pallet = new Pallet("Pallet", PalletLength, PalletWidth, (int)Math.Round(PalletHeight));
            // Ensure geometry is built for the current layer/pallet
            LayerGeometryBuilder.Build(layer, pallet, 1);

            LayerRectangles.Clear();
            if (layer.Geometry?.ItemRectangles != null)
            {
                // Flip Y so UI top-left origin matches pallet bottom-left origin
                double canvasHeight = PalletWidth;
                foreach (var r in layer.Geometry.ItemRectangles)
                {
                    var display = new Rect(r.X, canvasHeight - (r.Y + r.Height), r.Width, r.Height);
                    LayerRectangles.Add(display);
                }
            }
        }
    }
}
