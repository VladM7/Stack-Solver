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
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Stack_Solver.ViewModels.Pages
{
    public enum DrillLevel
    {
        Pallet,
        Layer
    }

    /// <summary>
    /// Single results surface that merges the former Layer/Pallet analyzers into one
    /// top-down drill-down: Solution -> Pallet type -> Layer -> Box, driven by a breadcrumb
    /// and a single shared 3D viewport.
    /// </summary>
    public partial class ResultsViewModel : ObservableObject
    {
        private readonly IEventAggregator _events;
        private readonly ILayerVisualizationService _viz;
        private readonly ISnackbarService _snackbarService;

        private readonly PalletSceneBuilder _palletSceneBuilder = new();
        private readonly LayerSceneBuilder _layerSceneBuilder = new();
        private readonly List<Model3DGroup> _layerHighlights = [];

        private CancellationTokenSource? _generationCts;
        private CancellationTokenSource? _sceneCts;
        private ViewportController? _viewportController;

        // ── Settings snapshot (kept in sync via SettingsChangedMessage) ──────────────
        private int _palletLength, _palletWidth, _palletHeight;
        private int _maxStackHeight = 180, _maxStackWeight = 950;
        private double _maxSkuOverhang;
        private bool _useCpsat;
        private List<SKU> _selectedSkus = [];
        private GenerationOptions _generationOptions = new();

        // ── Observable state ─────────────────────────────────────────────────────────
        [ObservableProperty]
        private bool _isGenerating;

        [ObservableProperty]
        private bool _hasResults;

        [ObservableProperty]
        private string _genStats = "Click Generate to build a solution.";

        [ObservableProperty]
        private DrillLevel _level = DrillLevel.Pallet;

        [ObservableProperty]
        private ObservableCollection<SolutionDisplay> _solutions = [];

        [ObservableProperty]
        private SolutionDisplay? _selectedSolution;

        [ObservableProperty]
        private ObservableCollection<TemplateAssignmentDisplay> _assignments = [];

        [ObservableProperty]
        private TemplateAssignmentDisplay? _selectedAssignment;

        [ObservableProperty]
        private ObservableCollection<LayerTypeDisplay> _selectedLayerTypes = [];

        [ObservableProperty]
        private LayerTypeDisplay? _selectedLayerType;

        [ObservableProperty]
        private string _detailTitle = string.Empty;

        [ObservableProperty]
        private string _detailText = string.Empty;

        [ObservableProperty]
        private string _selectedItemInfo = string.Empty;

        [ObservableProperty]
        private ObservableCollection<BreadcrumbItem> _breadcrumbs = [];

        public Model3DGroup Scene { get; } = new();
        public IReadOnlyList<LabelInfo> PalletDimLabels => _palletSceneBuilder.DimLabels;
        public ViewportController? ViewportController => _viewportController;

        public bool IsLayerLevel => Level == DrillLevel.Layer;

        public ICommand ZoomCommand { get; }
        public ICommand BeginPanCommand { get; }
        public ICommand PanCommand { get; }

        public ResultsViewModel(IEventAggregator events, ILayerVisualizationService viz, ISnackbarService snackbarService)
        {
            _events = events;
            _viz = viz;
            _snackbarService = snackbarService;
            _events.Subscribe<SettingsChangedMessage>(OnSettingsChanged);
            ZoomCommand = new RelayCommand<double>(delta => _viewportController?.Zoom(delta));
            BeginPanCommand = new RelayCommand<Point>(p => _viewportController?.BeginPan(p));
            PanCommand = new RelayCommand<Point>(p => _viewportController?.Pan(p));
        }

        // ── Camera ───────────────────────────────────────────────────────────────────
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

        private void OnSettingsChanged(SettingsChangedMessage msg)
        {
            _palletLength = msg.PalletLength;
            _palletWidth = msg.PalletWidth;
            _palletHeight = (int)Math.Round(msg.PalletHeight);
            _maxStackHeight = msg.MaxStackHeight;
            _maxStackWeight = msg.MaxStackWeight;
            _maxSkuOverhang = msg.MaxSkuOverhang;
            _useCpsat = msg.UseCpsat;
            _selectedSkus = [.. msg.Skus.Where(s => s.IsSelected && s.Quantity > 0)];
            _generationOptions = new GenerationOptions(msg.SolverTimeLimit, msg.MaxCpsatCandidates, msg.BlfAttempts);
            if (_viewportController != null)
                _viewportController.Target = CurrentPalletCenter;
        }

        // ── Generation: layers + solutions in one pass ───────────────────────────────
        [RelayCommand]
        private async Task Generate()
        {
            if (IsGenerating) return;

            var localCts = new CancellationTokenSource();
            _generationCts = localCts;
            var ct = localCts.Token;

            IsGenerating = true;
            HasResults = false;
            Solutions.Clear();
            Assignments.Clear();
            SelectedLayerTypes = [];
            SelectedAssignment = null;
            SelectedLayerType = null;
            Scene.Children.Clear();
            DetailTitle = string.Empty;
            DetailText = string.Empty;
            SelectedItemInfo = string.Empty;
            Breadcrumbs.Clear();
            GenStats = "Generating...";

            try
            {
                if (_selectedSkus.Count == 0)
                {
                    GenStats = "No SKUs selected with quantity > 0.";
                    _snackbarService.Show("Generation failed", "Select at least one SKU with a quantity greater than 0.", ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                    return;
                }

                var pallet = new Pallet("Pallet", _palletLength, _palletWidth, _palletHeight)
                {
                    MaxStackHeight = _maxStackHeight,
                    MaxStackWeight = _maxStackWeight,
                    MaxSkuOverhang = _maxSkuOverhang
                };
                var options = _generationOptions;
                var skus = _selectedSkus.ToList();
                var demand = skus.ToDictionary(s => s.SkuId, s => s.Quantity, StringComparer.Ordinal);
                bool useCpsat = _useCpsat;

                var allLayers = await Task.Run(() =>
                    LayerGenerator.Generate(skus, pallet, options, useCpsat, ct), ct);

                ct.ThrowIfCancellationRequested();

                if (allLayers == null || allLayers.Count == 0)
                {
                    GenStats = "No layers generated.";
                    _snackbarService.Show("Generation failed", "No layers were generated for the selected SKUs.", ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                    return;
                }

                var solutions = await Task.Run(() => BuildSolutions(allLayers, skus, demand, pallet, options, ct), ct);

                ct.ThrowIfCancellationRequested();

                Solutions.Clear();
                foreach (var s in solutions)
                    Solutions.Add(s);

                HasResults = Solutions.Count > 0;
                if (HasResults)
                {
                    GenStats = $"{allLayers.Count} candidate layers · {Solutions.Count} {(Solutions.Count == 1 ? "solution" : "solutions")}.";
                    SelectedSolution = Solutions[0];
                    _snackbarService.Show("Generation complete",
                        $"{allLayers.Count} candidate layers and {Solutions.Count} final {(Solutions.Count == 1 ? "solution" : "solutions")} generated.",
                        ControlAppearance.Success, null, TimeSpan.FromSeconds(5));
                }
                else
                {
                    GenStats = "No assignments produced.";
                }
            }
            catch (OperationCanceledException)
            {
                GenStats = "Generation canceled.";
            }
            catch (Exception ex)
            {
                GenStats = $"Error: {ex.Message}";
                _snackbarService.Show("Generation failed", ex.Message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
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
                GenStats = "Canceling...";
            }
        }

        private List<SolutionDisplay> BuildSolutions(
            List<Layer> allLayers, List<SKU> skus, Dictionary<string, int> demand,
            Pallet pallet, GenerationOptions options, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var filtered = LayerMetricsCalculator.FilterLayers(allLayers, options);
            var greedy = GreedyAssignmentService.Assign(filtered, demand, pallet);

            if (greedy.HasLeftovers)
            {
                var leftoverSkus = BuildLeftoverSkus(skus, greedy.Leftovers);
                if (leftoverSkus.Count > 0)
                {
                    var tailLayers = LayerGenerator.Generate(leftoverSkus, pallet, options, ct: ct);
                    if (tailLayers.Count > 0)
                    {
                        var tailOptions = new GenerationOptions(options.MaxSolverTime, options.MaxCPSATCandidates, options.BLFAttempts)
                        {
                            MaxLayerStability = options.MaxLayerStability,
                            PerSkuTopLayerFraction = 1.0
                        };
                        var tailFiltered = LayerMetricsCalculator.FilterLayers(tailLayers, tailOptions);
                        if (tailFiltered.Count > 0)
                            greedy = MergeResults(greedy, GreedyAssignmentService.Assign(tailFiltered, greedy.Leftovers, pallet));
                    }
                }
            }

            ct.ThrowIfCancellationRequested();

            AssignmentResult? cpsatResult = null;
            try
            {
                var pool = PalletTemplateEnumerator.Enumerate(pallet, filtered);
                var pruned = TemplateFilter.Filter(pool, pallet);
                cpsatResult = CPSATAssignmentService.Assign(pruned, demand, pallet, options, greedy, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* CP-SAT is best-effort; greedy result still shown */ }

            ct.ThrowIfCancellationRequested();

            var result = new List<SolutionDisplay>();
            if (cpsatResult != null && cpsatResult.Assignments.Count > 0)
                result.Add(new SolutionDisplay(result.Count + 1, "CP-SAT", cpsatResult, skus, _palletLength, _palletWidth, _palletHeight));
            if (greedy.Assignments.Count > 0)
                result.Add(new SolutionDisplay(result.Count + 1, "Greedy", greedy, skus, _palletLength, _palletWidth, _palletHeight));
            return result;
        }

        // ── Drill-down: solution -> pallet type ──────────────────────────────────────
        partial void OnSelectedSolutionChanged(SolutionDisplay? value)
        {
            Assignments.Clear();
            if (value == null)
            {
                SelectedAssignment = null;
                return;
            }

            int idx = 1;
            foreach (var (template, count) in value.Result.Assignments)
                Assignments.Add(new TemplateAssignmentDisplay(template, count, value.Skus, idx++, value.PalletLength, value.PalletWidth, value.PalletHeight));

            SelectedAssignment = Assignments.FirstOrDefault();
        }

        partial void OnSelectedAssignmentChanged(TemplateAssignmentDisplay? value)
        {
            SelectedLayerType = null;
            Level = DrillLevel.Pallet;

            SelectedLayerTypes = value != null
                ? new ObservableCollection<LayerTypeDisplay>(value.LayerTypes)
                : [];

            DetailTitle = value != null ? $"Pallet {value.Name}" : string.Empty;
            DetailText = value != null ? BuildAssignmentText(value) : string.Empty;
            SelectedItemInfo = string.Empty;

            RebuildBreadcrumbs();

            if (value != null)
            {
                FramePallet(value.Template);
                RenderPalletSceneAsync(value.Template);
            }
            else
            {
                Scene.Children.Clear();
            }
        }

        public bool HasSelectedLayer => SelectedLayerType != null;

        // ── Pallet level: highlight a layer band when its row/box is selected ────────
        partial void OnSelectedLayerTypeChanged(LayerTypeDisplay? value)
        {
            OnPropertyChanged(nameof(HasSelectedLayer));

            if (Level == DrillLevel.Layer)
            {
                // Already inspecting a layer: selecting another swaps the inspected layer.
                if (value != null) EnterLayerLevel(value);
                return;
            }

            ClearLayerHighlights();
            if (value == null) return;

            const double inflate = 0.1;
            var fillBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 0));
            foreach (var (layerId, y, height) in _palletSceneBuilder.LayerPositions)
            {
                if (layerId != value.LayerId) continue;
                var origin = new Point3D(-inflate / 2.0, y - inflate / 2.0, -inflate / 2.0);
                var highlight = GeometryCreator.CreateBoxWithEdges(
                    origin, _palletLength + inflate, height + inflate, _palletWidth + inflate,
                    fillBrush, Colors.Yellow, 0.6);
                Scene.Children.Add(highlight);
                _layerHighlights.Add(highlight);
            }
        }

        // ── Drill into a single layer ────────────────────────────────────────────────
        [RelayCommand]
        private void ViewLayer(LayerTypeDisplay? layerType)
        {
            if (layerType?.Layer == null) return;
            SelectedLayerType = layerType;
            EnterLayerLevel(layerType);
        }

        private void EnterLayerLevel(LayerTypeDisplay layerType)
        {
            ClearLayerHighlights();
            Level = DrillLevel.Layer;
            SelectedItemInfo = string.Empty;
            DetailTitle = $"Layer {layerType.Name}";
            DetailText = BuildLayerText(layerType.Layer);
            RebuildBreadcrumbs();
            FrameLayer();
            RenderLayerSceneAsync(layerType.Layer);
        }

        [RelayCommand]
        private void Back() => BackToPalletLevel();

        private void BackToPalletLevel()
        {
            if (SelectedAssignment == null) return;
            Level = DrillLevel.Pallet;
            SelectedItemInfo = string.Empty;
            DetailTitle = $"Pallet {SelectedAssignment.Name}";
            DetailText = BuildAssignmentText(SelectedAssignment);
            RebuildBreadcrumbs();
            FramePallet(SelectedAssignment.Template);
            RenderPalletSceneAsync(SelectedAssignment.Template);
        }

        // ── Scene rendering ──────────────────────────────────────────────────────────
        private async void RenderPalletSceneAsync(PalletTemplate template)
        {
            _sceneCts?.Cancel();
            _sceneCts?.Dispose();
            _sceneCts = new CancellationTokenSource();
            var ct = _sceneCts.Token;
            try
            {
                await _palletSceneBuilder.BuildAsync(Scene, template, _palletLength, _palletWidth, _palletHeight, ct);
                OnPropertyChanged(nameof(PalletDimLabels));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { DetailText = $"Scene build error: {ex.Message}"; }
        }

        private async void RenderLayerSceneAsync(Layer layer)
        {
            _sceneCts?.Cancel();
            _sceneCts?.Dispose();
            _sceneCts = new CancellationTokenSource();
            var ct = _sceneCts.Token;
            try
            {
                await _layerSceneBuilder.BuildAsync(Scene, layer, _palletLength, _palletWidth, _palletHeight, ct);
                OnPropertyChanged(nameof(PalletDimLabels));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { DetailText = $"Scene build error: {ex.Message}"; }
        }

        private void FramePallet(PalletTemplate template)
        {
            if (_viewportController == null) return;
            double totalHeight = _palletHeight + template.TotalHeight;
            var center = new Point3D(_palletLength / 2.0, totalHeight / 2.0, _palletWidth / 2.0);
            double distance = Math.Sqrt(_palletLength * _palletLength + _palletWidth * _palletWidth + totalHeight * totalHeight) * 1.5;
            _viewportController.ResetView(center, distance);
        }

        private void FrameLayer()
        {
            if (_viewportController == null) return;
            var center = new Point3D(_palletLength / 2.0, _palletHeight / 2.0, _palletWidth / 2.0);
            double distance = Math.Sqrt(_palletLength * _palletLength + _palletWidth * _palletWidth) * 1.6;
            _viewportController.ResetView(center, distance);
        }

        // ── Hit-testing dispatch (called from the page code-behind) ──────────────────
        public bool TryGetItemFromGeometry(GeometryModel3D geo, out PositionedItem item)
            => _layerSceneBuilder.TryGetItemForGeometry(geo, out item);

        public bool TryGetLayerTypeForGeometry(GeometryModel3D geo, out LayerTypeDisplay? layerType)
        {
            layerType = null;
            if (!_palletSceneBuilder.TryGetLayerIdForGeometry(geo, out var layerId)) return false;
            layerType = SelectedLayerTypes.FirstOrDefault(lt => lt.LayerId == layerId);
            return layerType != null;
        }

        /// <summary>Apply a box selection from a viewport hit-test (Layer level).</summary>
        public void SelectBox(PositionedItem? item) => UpdateSelectedItem(item);

        private void UpdateSelectedItem(PositionedItem? item)
        {
            if (item?.SkuType != null)
            {
                var sku = item.SkuType;
                SelectedItemInfo = $"{sku.Name}  ({sku.Length}×{sku.Width}×{sku.Height})  @ {item.X}, {item.Y}";
                _viz.HighlightItem(Scene, item, _palletHeight);
            }
            else
            {
                SelectedItemInfo = string.Empty;
                _viz.HighlightItem(Scene, null, _palletHeight);
            }
        }

        // ── Breadcrumb ───────────────────────────────────────────────────────────────
        private void RebuildBreadcrumbs()
        {
            Breadcrumbs.Clear();
            if (SelectedSolution == null) return;

            bool atLayer = Level == DrillLevel.Layer;

            Breadcrumbs.Add(new BreadcrumbItem(
                SelectedSolution.Name,
                isCurrent: SelectedAssignment == null,
                showSeparator: false,
                new RelayCommand(BackToPalletLevel)));

            if (SelectedAssignment != null)
            {
                Breadcrumbs.Add(new BreadcrumbItem(
                    SelectedAssignment.Name,
                    isCurrent: !atLayer,
                    showSeparator: true,
                    new RelayCommand(BackToPalletLevel)));
            }

            if (atLayer && SelectedLayerType != null)
            {
                Breadcrumbs.Add(new BreadcrumbItem(
                    SelectedLayerType.Name,
                    isCurrent: true,
                    showSeparator: true,
                    null));
            }

            OnPropertyChanged(nameof(IsLayerLevel));
        }

        partial void OnLevelChanged(DrillLevel value) => OnPropertyChanged(nameof(IsLayerLevel));

        private void ClearLayerHighlights()
        {
            foreach (var h in _layerHighlights)
                Scene.Children.Remove(h);
            _layerHighlights.Clear();
        }

        // ── Text builders ────────────────────────────────────────────────────────────
        private string BuildAssignmentText(TemplateAssignmentDisplay assignment)
        {
            var t = assignment.Template;
            int cargoHeight = (int)Math.Round(t.TotalHeight);
            int totalHeight = _palletHeight + cargoHeight;

            var sb = new StringBuilder();
            sb.AppendLine($"{assignment.Name}  (×{assignment.Count} {(assignment.Count == 1 ? "pallet" : "pallets")})");
            sb.AppendLine();
            sb.AppendLine($"Load dimensions: {_palletLength}×{_palletWidth}×{totalHeight} cm");
            sb.AppendLine($"Cargo height: {cargoHeight} cm");
            sb.AppendLine($"Weight: {t.TotalWeight:N0} kg");
            sb.AppendLine($"Boxes: {t.TotalBoxCount} per pallet");
            sb.AppendLine($"Utilization: {t.AverageLayerUtilization:P0}");
            sb.AppendLine($"Contents: {assignment.Contents}");
            return sb.ToString();
        }

        private static string BuildLayerText(Layer layer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Utilization: {layer.Metadata.Utilization:P1}");
            sb.AppendLine($"Height: {layer.Metadata.Height} cm");
            double totalWeight = layer.Items.Sum(g => g.SkuType.Weight);
            sb.AppendLine($"Total weight: {totalWeight:N0} kg");
            sb.AppendLine($"Placed items: {layer.Items.Count}");
            sb.AppendLine();
            foreach (var g in layer.Items.GroupBy(i => i.SkuType.SkuId))
            {
                var sku = g.First().SkuType;
                sb.AppendLine($"  {sku.Name} × {g.Count()}  [{sku.Length}×{sku.Width}×{sku.Height}]");
            }
            return sb.ToString();
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
    }

    public partial class BreadcrumbItem(string label, bool isCurrent, bool showSeparator, ICommand? command) : ObservableObject
    {
        public string Label { get; } = label;
        public bool IsCurrent { get; } = isCurrent;
        public bool ShowSeparator { get; } = showSeparator;
        public ICommand? Command { get; } = command;
        public bool IsClickable => Command != null && !IsCurrent;
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
        public Layer Layer { get; } = layer;
        public string LayerId { get; } = layer.Id;
        public string Name { get; } = layer.Name;
        public int Count { get; } = count;
        public string Contents { get; } = string.Join(", ", layer.Items
                .GroupBy(i => i.SkuType.SkuId)
                .Select(g => $"{g.Count()}x {skuNames.GetValueOrDefault(g.Key, g.Key)}"));
        public string Utilization { get; } = layer.Metadata.Utilization.ToString("P0");
    }

    public partial class SolutionDisplay : ObservableObject
    {
        public int Number { get; }
        public string Name { get; }
        public AssignmentResult Result { get; }
        public IReadOnlyList<SKU> Skus { get; }
        public int PalletLength { get; }
        public int PalletWidth { get; }
        public int PalletHeight { get; }
        public int TotalPallets { get; }
        public int PalletTypes { get; }
        public int TotalItemsPacked { get; }
        public string Efficiency { get; }

        public SolutionDisplay(
            int number,
            string name,
            AssignmentResult result,
            IReadOnlyList<SKU> skus,
            int palletLength,
            int palletWidth,
            int palletHeight)
        {
            Number = number;
            Name = name;
            Result = result;
            Skus = skus;
            PalletLength = palletLength;
            PalletWidth = palletWidth;
            PalletHeight = palletHeight;
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
