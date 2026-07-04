using Stack_Solver.Data.Repositories;
using Stack_Solver.Helpers.Rendering;
using Stack_Solver.Infrastructure;
using Stack_Solver.Models;
using Stack_Solver.Models.Assignment;
using Stack_Solver.Models.Jobs;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services;
using Stack_Solver.Services.BranchAndPrice;
using Stack_Solver.Services.Stacking;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        Solution,
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
        private readonly IJobRepository _jobRepository;

        private readonly PalletSceneBuilder _palletSceneBuilder = new();
        private readonly LayerSceneBuilder _layerSceneBuilder = new();
        private readonly WarehouseSceneBuilder _warehouseSceneBuilder = new();
        private readonly List<Model3DGroup> _sceneHighlights = [];

        private const int WarehouseDetailThreshold = 60;

        private CancellationTokenSource? _generationCts;
        private CancellationTokenSource? _sceneCts;
        private ViewportController? _viewportController;

        // Set while a level transition is mutating selection state, so the selection change
        // handlers don't re-interpret programmatic changes as user-driven navigation.
        private bool _isNavigating;

        // ── Settings snapshot (kept in sync via SettingsChangedMessage) ──────────────
        private int _palletLength, _palletWidth, _palletHeight;
        private double _palletHeightExact = 14.4;
        private int _maxStackHeight = 180, _maxStackWeight = 950;
        private double _maxSkuOverhang;
        private OverhangMode _overhangMode;
        private double _maxTopHeavyPercent;
        private bool _useCpsat;
        private bool _useGreedy = true;
        private bool _useCpsatSolution = true;
        private bool _useBranchAndPrice = true;
        private string? _defaultCatalog;
        private string? _defaultPalletName;

        // Label of the job the currently displayed results belong to; shown as the top breadcrumb.
        private string _currentJobLabel = string.Empty;

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
        private DrillLevel _level = DrillLevel.Solution;

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
        public ViewportController? ViewportController => _viewportController;

        /// <summary>Floating labels for the current scene: type labels (Solution), dimensions (Pallet), none (Layer).</summary>
        public IReadOnlyList<LabelInfo> SceneLabels => Level switch
        {
            DrillLevel.Solution => _warehouseSceneBuilder.TypeLabels,
            DrillLevel.Pallet => _palletSceneBuilder.DimLabels,
            _ => []
        };

        public bool IsLayerLevel => Level == DrillLevel.Layer;
        public bool CanGoBack => Level != DrillLevel.Solution;
        public bool HasSelectedAssignment => SelectedAssignment != null;
        public bool HasSelectedLayer => SelectedLayerType != null;

        public ICommand ZoomCommand { get; }
        public ICommand BeginPanCommand { get; }
        public ICommand PanCommand { get; }

        public ResultsViewModel(IEventAggregator events, ILayerVisualizationService viz, ISnackbarService snackbarService, IJobRepository jobRepository)
        {
            _events = events;
            _viz = viz;
            _snackbarService = snackbarService;
            _jobRepository = jobRepository;
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
            _palletHeightExact = msg.PalletHeight;
            _maxStackHeight = msg.MaxStackHeight;
            _maxStackWeight = msg.MaxStackWeight;
            _maxSkuOverhang = msg.MaxSkuOverhang;
            _overhangMode = msg.OverhangMode;
            _maxTopHeavyPercent = msg.MaxTopHeavyPercent;
            _useCpsat = msg.UseCpsat;
            _useGreedy = msg.UseGreedy;
            _useCpsatSolution = msg.UseCpsatSolution;
            _useBranchAndPrice = msg.UseBranchAndPrice;
            _defaultCatalog = msg.DefaultCatalog;
            _defaultPalletName = msg.DefaultPalletName;
            _selectedSkus = [.. msg.Skus.Where(s => s.IsSelected && s.Quantity > 0)];
            _generationOptions = new GenerationOptions(msg.SolverTimeLimit, msg.MaxCpsatCandidates, msg.BlfAttempts);
            if (_viewportController != null)
                _viewportController.Target = CurrentPalletCenter;

            // Displayed results no longer reflect the current settings.
            if (HasResults && !IsGenerating)
                GenStats = "Settings changed. Re-generate to view updated results.";
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
            SelectedSolution = null;
            Assignments.Clear();
            SelectedLayerTypes = [];
            SelectedAssignment = null;
            SelectedLayerType = null;
            Scene.Children.Clear();
            DetailTitle = string.Empty;
            DetailText = string.Empty;
            SelectedItemInfo = string.Empty;
            Breadcrumbs.Clear();
            _currentJobLabel = string.Empty;
            GenStats = "Generating...";
            var stopwatch = Stopwatch.StartNew();

            // Recorded as a Job the moment real work starts; finalized on every exit path below.
            Job? job = null;

            try
            {
                if (_selectedSkus.Count == 0)
                {
                    GenStats = "No SKUs selected with quantity > 0.";
                    _snackbarService.Show("Generation failed", "Select at least one SKU with a quantity greater than 0.", ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                    return;
                }

                job = await StartJobAsync(ct);
                if (job is not null)
                    _currentJobLabel = $"Job @ {job.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}";

                var pallet = new Pallet("Pallet", _palletLength, _palletWidth, _palletHeight)
                {
                    MaxStackHeight = _maxStackHeight,
                    MaxStackWeight = _maxStackWeight,
                    MaxSkuOverhang = _maxSkuOverhang,
                    OverhangMode = _overhangMode,
                    MaxTopHeavyPercent = _maxTopHeavyPercent
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
                    await FinishJobAsync(job, []);
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
                    stopwatch.Stop();
                    GenStats = $"{Solutions.Count} {(Solutions.Count == 1 ? "solution" : "solutions")} generated in {stopwatch.ElapsedMilliseconds} ms.";
                    SelectedSolution = Solutions[0];
                    _snackbarService.Show("Generation complete",
                        $"{allLayers.Count} candidate layers and {Solutions.Count} final {(Solutions.Count == 1 ? "solution" : "solutions")} generated.",
                        ControlAppearance.Success, null, TimeSpan.FromSeconds(5));
                }
                else
                {
                    GenStats = "No assignments produced.";
                }

                await FinishJobAsync(job, [.. Solutions]);
            }
            catch (OperationCanceledException)
            {
                GenStats = "Generation canceled.";
                await EndJobAsync(job, JobStatus.Canceled);
            }
            catch (Exception ex)
            {
                GenStats = $"Error: {ex.Message}";
                _snackbarService.Show("Generation failed", ex.Message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                await EndJobAsync(job, JobStatus.Failed, ex.Message);
            }
            finally
            {
                IsGenerating = false;
                _generationCts?.Dispose();
                _generationCts = null;
            }
        }

        // ── Open a saved job: rebuild solutions from the snapshot and display them instantly ─────
        /// <summary>
        /// Loads a persisted job's results into the drill-down and shows its default (first) solution
        /// without re-solving. Pallet dimensions come from the job's own settings snapshot, so the
        /// scene is correct even if the live setup has since changed.
        /// </summary>
        public void DisplayJob(Job job, JobSettingsSnapshot settings)
        {
            // A job may be opened while a generation is still running on this (singleton) view model.
            if (_generationCts is { IsCancellationRequested: false })
                _generationCts.Cancel();

            var skusById = JobSnapshotMapper.BuildSkuLookup(settings);
            var resultsSnapshot = JobSnapshotMapper.DeserializeResults(job.ResultsJson);
            var data = resultsSnapshot is null
                ? []
                : JobSnapshotMapper.FromResultsSnapshot(resultsSnapshot, skusById);
            var skuList = skusById.Values.ToList();

            _palletLength = settings.PalletLength;
            _palletWidth = settings.PalletWidth;
            _palletHeight = (int)Math.Round(settings.PalletHeight);
            _palletHeightExact = settings.PalletHeight;
            if (_viewportController != null)
                _viewportController.Target = CurrentPalletCenter;

            _currentJobLabel = $"Job @ {job.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}";

            Solutions.Clear();
            SelectedSolution = null;
            Assignments.Clear();
            SelectedLayerTypes = [];
            SelectedAssignment = null;
            SelectedLayerType = null;
            ClearHighlights();
            Scene.Children.Clear();
            Breadcrumbs.Clear();
            SelectedItemInfo = string.Empty;

            int number = 1;
            foreach (var d in data)
            {
                if (d.Result.Assignments.Count == 0) continue;
                Solutions.Add(new SolutionDisplay(number++, d.Name, d.Result, skuList,
                    _palletLength, _palletWidth, _palletHeight, d.IsProvenOptimal, d.LowerBound));
            }

            HasResults = Solutions.Count > 0;
            if (HasResults)
            {
                GenStats = $"Loaded {Solutions.Count} {(Solutions.Count == 1 ? "solution" : "solutions")} from this job.";
                // Selecting the first solution enters the Solution level, which rebuilds the
                // breadcrumbs (now led by the job) and renders the overview scene.
                SelectedSolution = Solutions[0];
            }
            else
            {
                GenStats = "This job has no displayable solutions.";
                DetailTitle = string.Empty;
                DetailText = string.Empty;
            }
        }

        /// <summary>
        /// Re-renders (and re-frames) the scene for the current level. Used when the viewport's camera
        /// only attaches after results were already loaded (e.g. a job opened before the page's first
        /// visit), so the initial render happened with no controller to frame it.
        /// </summary>
        public void RefreshCurrentScene()
        {
            if (!HasResults) return;
            switch (Level)
            {
                case DrillLevel.Pallet when SelectedAssignment != null:
                    FramePallet(SelectedAssignment.Template);
                    RenderPalletSceneAsync(SelectedAssignment.Template);
                    break;
                case DrillLevel.Layer when SelectedLayerType != null:
                    FrameLayer();
                    RenderLayerSceneAsync(SelectedLayerType.Layer);
                    break;
                default:
                    RenderWarehouseSceneAsync();
                    break;
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

        // ── Job lifecycle ────────────────────────────────────────────────────────────
        // Job tracking is best-effort: a persistence hiccup must never break a generation, so
        // failures here are swallowed and generation continues with a null job.
        private async Task<Job?> StartJobAsync(CancellationToken ct)
        {
            try
            {
                var job = new Job
                {
                    Status = JobStatus.Ongoing,
                    CreatedAt = DateTime.UtcNow,
                    SettingsJson = JobSnapshotMapper.Serialize(BuildSettingsSnapshot())
                };
                await _jobRepository.AddAsync(job, ct);
                return job;
            }
            catch (OperationCanceledException) { throw; }
            catch { return null; }
        }

        private async Task FinishJobAsync(Job? job, IReadOnlyList<SolutionDisplay> solutions)
        {
            if (job is null) return;
            try
            {
                var data = solutions
                    .Select(s => new JobSolutionData(s.Name, s.IsProvenOptimal, s.LowerBound, s.Result))
                    .ToList();
                job.ResultsJson = JobSnapshotMapper.Serialize(JobSnapshotMapper.ToResultsSnapshot(data));
                job.SolutionCount = solutions.Count;
                job.TotalPallets = solutions.Count > 0 ? solutions[0].TotalPallets : 0;
                job.Status = JobStatus.Finished;
                job.CompletedAt = DateTime.UtcNow;
                // Finalize with None: the generation token may already be canceled/disposed.
                await _jobRepository.UpdateAsync(job, CancellationToken.None);
            }
            catch { /* best-effort */ }
        }

        private async Task EndJobAsync(Job? job, JobStatus status, string? error = null)
        {
            if (job is null) return;
            try
            {
                job.Status = status;
                job.Error = error;
                job.CompletedAt = DateTime.UtcNow;
                await _jobRepository.UpdateAsync(job, CancellationToken.None);
            }
            catch { /* best-effort */ }
        }

        private JobSettingsSnapshot BuildSettingsSnapshot() => new()
        {
            DefaultCatalog = _defaultCatalog,
            DefaultPalletName = _defaultPalletName,
            PalletLength = _palletLength,
            PalletWidth = _palletWidth,
            PalletHeight = _palletHeightExact,
            MaxStackHeight = _maxStackHeight,
            MaxStackWeight = _maxStackWeight,
            OverhangMode = _overhangMode,
            MaxSkuOverhang = _maxSkuOverhang,
            MaxTopHeavyPercent = _maxTopHeavyPercent,
            UseCpsat = _useCpsat,
            UseGreedy = _useGreedy,
            UseCpsatSolution = _useCpsatSolution,
            UseBranchAndPrice = _useBranchAndPrice,
            SolverTimeLimit = _generationOptions.MaxSolverTime,
            MaxCpsatCandidates = _generationOptions.MaxCPSATCandidates,
            BlfAttempts = _generationOptions.BLFAttempts,
            Skus = [.. _selectedSkus.Select(s => new JobSkuSnapshot
            {
                SkuId = s.SkuId,
                Name = s.Name,
                Length = s.Length,
                Width = s.Width,
                Height = s.Height,
                Weight = s.Weight,
                Rotatable = s.Rotatable,
                Notes = s.Notes,
                Quantity = s.Quantity
            })]
        };

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
            if (_useCpsatSolution)
            {
                try
                {
                    var pool = PalletTemplateEnumerator.Enumerate(pallet, filtered);
                    var pruned = TemplateFilter.Filter(pool, pallet);
                    cpsatResult = CPSATAssignmentService.Assign(pruned, demand, pallet, options, greedy, ct);
                }
                catch (OperationCanceledException) { throw; }
                catch { /* CP-SAT is best-effort; greedy result still shown */ }
            }

            ct.ThrowIfCancellationRequested();

            BranchAndPriceSolution? bnp = null;
            if (_useBranchAndPrice)
            {
                try
                {
                    bnp = BranchAndPriceAssignmentService.Solve(filtered, demand, pallet, options, greedy, ct);
                }
                catch (OperationCanceledException) { throw; }
                catch { /* Branch-and-price is best-effort; other results still shown */ }
            }

            ct.ThrowIfCancellationRequested();

            var result = new List<SolutionDisplay>();
            if (bnp != null && bnp.Result.Assignments.Count > 0)
                result.Add(new SolutionDisplay(result.Count + 1, "Branch & Price", bnp.Result, skus, _palletLength, _palletWidth, _palletHeight,
                    isProvenOptimal: bnp.LowerBoundCertified, lowerBound: bnp.LowerBound));
            if (cpsatResult != null && cpsatResult.Assignments.Count > 0)
                result.Add(new SolutionDisplay(result.Count + 1, "CP-SAT", cpsatResult, skus, _palletLength, _palletWidth, _palletHeight));
            // Greedy always runs above as the warm-start seed for CP-SAT and B&P; it is only
            // offered as its own solution when the user has enabled it.
            if (_useGreedy && greedy.Assignments.Count > 0)
                result.Add(new SolutionDisplay(result.Count + 1, "Greedy", greedy, skus, _palletLength, _palletWidth, _palletHeight));
            return result;
        }

        // ── Drill-down: solution → pallet type → layer ───────────────────────────────
        partial void OnSelectedSolutionChanged(SolutionDisplay? value)
        {
            Assignments.Clear();
            if (value != null)
            {
                int idx = 1;
                foreach (var (template, count) in value.Result.Assignments)
                    Assignments.Add(new TemplateAssignmentDisplay(template, count, value.Skus, idx++, value.PalletLength, value.PalletWidth, value.PalletHeight));
            }
            EnterSolutionLevel();
        }

        // Selection handlers: at the current level a selection is a *preview*; a level
        // below it is *navigation*. Programmatic changes during a transition are ignored
        // via _isNavigating so they don't recurse.
        partial void OnSelectedAssignmentChanged(TemplateAssignmentDisplay? value)
        {
            OnPropertyChanged(nameof(HasSelectedAssignment));
            if (_isNavigating) return;

            if (Level == DrillLevel.Solution)
                PreviewAssignment(value);
            else if (value != null)
                EnterPalletLevel(value);
            else
                EnterSolutionLevel();
        }

        partial void OnSelectedLayerTypeChanged(LayerTypeDisplay? value)
        {
            OnPropertyChanged(nameof(HasSelectedLayer));
            if (_isNavigating) return;

            if (Level == DrillLevel.Layer)
            {
                if (value != null) EnterLayerLevel(value); // swap inspected layer
            }
            else if (Level == DrillLevel.Pallet)
            {
                PreviewLayer(value);
            }
        }

        // ── Level transitions ────────────────────────────────────────────────────────
        private void EnterSolutionLevel()
        {
            _isNavigating = true;
            try
            {
                Level = DrillLevel.Solution;
                SelectedLayerType = null;
                SelectedAssignment = null;
                SelectedLayerTypes = [];
                ClearHighlights();
                DetailTitle = SelectedSolution != null ? $"Solution ({SelectedSolution.Name})" : "Solution";
                DetailText = SelectedSolution != null ? BuildSolutionText(SelectedSolution) : string.Empty;
                SelectedItemInfo = PlaceholderForLevel();
                RebuildBreadcrumbs();
            }
            finally { _isNavigating = false; }

            RenderWarehouseSceneAsync();
        }

        private void EnterPalletLevel(TemplateAssignmentDisplay assignment)
        {
            _isNavigating = true;
            try
            {
                Level = DrillLevel.Pallet;
                SelectedAssignment = assignment;
                SelectedLayerType = null;
                SelectedLayerTypes = new ObservableCollection<LayerTypeDisplay>(assignment.LayerTypes);
                ClearHighlights();
                DetailTitle = $"Pallet ({assignment.Name})";
                DetailText = BuildAssignmentText(assignment);
                SelectedItemInfo = PlaceholderForLevel();
                RebuildBreadcrumbs();
                FramePallet(assignment.Template);
            }
            finally { _isNavigating = false; }

            RenderPalletSceneAsync(assignment.Template);
        }

        private void EnterLayerLevel(LayerTypeDisplay layerType)
        {
            _isNavigating = true;
            try
            {
                Level = DrillLevel.Layer;
                SelectedLayerType = layerType;
                ClearHighlights();
                DetailTitle = $"Layer ({layerType.Name})";
                DetailText = BuildLayerText(layerType.Layer);
                SelectedItemInfo = PlaceholderForLevel();
                RebuildBreadcrumbs();
                FrameLayer();
            }
            finally { _isNavigating = false; }

            RenderLayerSceneAsync(layerType.Layer);
        }

        [RelayCommand]
        private void ViewPallet(TemplateAssignmentDisplay? assignment)
        {
            if (assignment != null) EnterPalletLevel(assignment);
        }

        [RelayCommand]
        private void ViewLayer(LayerTypeDisplay? layerType)
        {
            if (layerType?.Layer != null) EnterLayerLevel(layerType);
        }

        [RelayCommand]
        private void Back()
        {
            if (Level == DrillLevel.Layer && SelectedAssignment != null)
                EnterPalletLevel(SelectedAssignment);
            else if (Level == DrillLevel.Pallet)
                EnterSolutionLevel();
        }

        // ── Preview highlights (selection at the current level) ──────────────────────
        private void PreviewAssignment(TemplateAssignmentDisplay? value)
        {
            ClearHighlights();
            if (value == null) { SelectedItemInfo = PlaceholderForLevel(); return; }

            const double inflate = 3.0;
            var fill = new SolidColorBrush(Color.FromArgb(36, 255, 255, 0));
            foreach (var block in _warehouseSceneBuilder.Blocks)
            {
                if (block.TemplateId != value.Template.Id) continue;
                var origin = new Point3D(block.Origin.X - inflate / 2.0, block.Origin.Y - inflate / 2.0, block.Origin.Z - inflate / 2.0);
                var hi = GeometryCreator.CreateBoxWithEdges(origin, block.SizeX + inflate, block.SizeY + inflate, block.SizeZ + inflate, fill, Colors.Yellow, 0.8);
                Scene.Children.Add(hi);
                _sceneHighlights.Add(hi);
            }
            SelectedItemInfo = AssignmentInfoLine(value);
        }

        private void PreviewLayer(LayerTypeDisplay? value)
        {
            ClearHighlights();
            if (value == null) { SelectedItemInfo = PlaceholderForLevel(); return; }

            const double inflate = 0.1;
            var fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 0));
            foreach (var (layerId, y, height) in _palletSceneBuilder.LayerPositions)
            {
                if (layerId != value.LayerId) continue;
                var origin = new Point3D(-inflate / 2.0, y - inflate / 2.0, -inflate / 2.0);
                var hi = GeometryCreator.CreateBoxWithEdges(origin, _palletLength + inflate, height + inflate, _palletWidth + inflate, fill, Colors.Yellow, 0.6);
                Scene.Children.Add(hi);
                _sceneHighlights.Add(hi);
            }
            SelectedItemInfo = LayerInfoLine(value);
        }

        private string PlaceholderForLevel() => Level switch
        {
            DrillLevel.Solution => "No pallet selected",
            DrillLevel.Pallet => "No layer selected",
            _ => "No box selected"
        };

        private static string AssignmentInfoLine(TemplateAssignmentDisplay a)
            => $"{a.Name}  ×{a.Count}  ·  {a.Template.TotalBoxCount} boxes  ·  {a.Contents}";

        private static string LayerInfoLine(LayerTypeDisplay l)
            => $"{l.Name}  ×{l.Count}  ·  {l.Contents}";

        // ── Scene rendering ──────────────────────────────────────────────────────────
        private async void RenderWarehouseSceneAsync()
        {
            _sceneCts?.Cancel();
            _sceneCts?.Dispose();
            _sceneCts = new CancellationTokenSource();
            var ct = _sceneCts.Token;

            var types = Assignments.Select(a => (a.Template, a.Count, a.Name)).ToList();
            try
            {
                await _warehouseSceneBuilder.BuildAsync(Scene, types, _palletLength, _palletWidth, _palletHeight, WarehouseDetailThreshold, ct);
                _viewportController?.ResetView(_warehouseSceneBuilder.ContentCenter, _warehouseSceneBuilder.FrameDistance);
                OnPropertyChanged(nameof(SceneLabels));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { DetailText = $"Scene build error: {ex.Message}"; }
        }

        private async void RenderPalletSceneAsync(PalletTemplate template)
        {
            _sceneCts?.Cancel();
            _sceneCts?.Dispose();
            _sceneCts = new CancellationTokenSource();
            var ct = _sceneCts.Token;
            try
            {
                await _palletSceneBuilder.BuildAsync(Scene, template, _palletLength, _palletWidth, _palletHeight, ct);
                OnPropertyChanged(nameof(SceneLabels));
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
                OnPropertyChanged(nameof(SceneLabels));
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

        public bool TryGetAssignmentForGeometry(GeometryModel3D geo, out TemplateAssignmentDisplay? assignment)
        {
            assignment = null;
            if (!_warehouseSceneBuilder.TryGetTemplateIdForGeometry(geo, out var id)) return false;
            assignment = Assignments.FirstOrDefault(a => a.Template.Id == id);
            return assignment != null;
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
                SelectedItemInfo = PlaceholderForLevel();
                _viz.HighlightItem(Scene, null, _palletHeight);
            }
        }

        // ── Breadcrumb ───────────────────────────────────────────────────────────────
        private void RebuildBreadcrumbs()
        {
            Breadcrumbs.Clear();
            if (SelectedSolution == null) return;

            bool atSolution = Level == DrillLevel.Solution;
            bool atLayer = Level == DrillLevel.Layer;
            bool hasJob = _currentJobLabel.Length > 0;

            // Job segment sits above everything; clicking it returns to the solution overview.
            if (hasJob)
            {
                Breadcrumbs.Add(new BreadcrumbItem(
                    _currentJobLabel,
                    isCurrent: false,
                    showSeparator: false,
                    new RelayCommand(EnterSolutionLevel)));
            }

            // Solution segment carries an Explorer-style dropdown of the sibling solutions.
            var choices = Solutions
                .Select(s => new BreadcrumbChoice(
                    s.Name,
                    ReferenceEquals(s, SelectedSolution),
                    new RelayCommand(() => SelectedSolution = s)))
                .ToList();

            Breadcrumbs.Add(new BreadcrumbItem(
                SelectedSolution.Name,
                isCurrent: atSolution,
                showSeparator: hasJob,
                new RelayCommand(EnterSolutionLevel),
                choices));

            if (!atSolution && SelectedAssignment != null)
            {
                Breadcrumbs.Add(new BreadcrumbItem(
                    SelectedAssignment.Name,
                    isCurrent: !atLayer,
                    showSeparator: true,
                    new RelayCommand(() => { if (SelectedAssignment != null) EnterPalletLevel(SelectedAssignment); })));
            }

            if (atLayer && SelectedLayerType != null)
            {
                Breadcrumbs.Add(new BreadcrumbItem(
                    SelectedLayerType.Name,
                    isCurrent: true,
                    showSeparator: true,
                    null));
            }
        }

        partial void OnLevelChanged(DrillLevel value)
        {
            OnPropertyChanged(nameof(IsLayerLevel));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(SceneLabels));
        }

        private void ClearHighlights()
        {
            foreach (var h in _sceneHighlights)
                Scene.Children.Remove(h);
            _sceneHighlights.Clear();
        }

        // ── Text builders ────────────────────────────────────────────────────────────
        private string BuildSolutionText(SolutionDisplay s)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Total pallets: {s.TotalPallets}");
            sb.AppendLine($"Pallet types: {s.PalletTypes}");
            sb.AppendLine($"Items packed: {s.TotalItemsPacked}");
            sb.AppendLine($"Avg. utilization: {s.Efficiency}");
            if (s.IsProvenOptimal is bool optimal)
            {
                if (optimal)
                    sb.AppendLine("Solution quality: proven optimal");
                else if (s.LowerBound > 1e-9)
                    sb.AppendLine($"Solution quality: best found (not proven optimal; lower bound ⌈{s.LowerBound:0.##}⌉ = {Math.Ceiling(s.LowerBound - 1e-9):0} pallets)");
                else
                    sb.AppendLine("Solution quality: best found (not proven optimal)");
            }
            sb.AppendLine();
            foreach (var a in Assignments)
                sb.AppendLine($"  {a.Name}  ×{a.Count}  —  {a.Contents}");
            return sb.ToString();
        }

        private string BuildAssignmentText(TemplateAssignmentDisplay assignment)
        {
            var t = assignment.Template;
            int cargoHeight = (int)Math.Round(t.TotalHeight);
            int totalHeight = _palletHeight + cargoHeight;

            var sb = new StringBuilder();
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

    public partial class BreadcrumbItem(
        string label, bool isCurrent, bool showSeparator, ICommand? command,
        IReadOnlyList<BreadcrumbChoice>? choices = null) : ObservableObject
    {
        public string Label { get; } = label;
        public bool IsCurrent { get; } = isCurrent;
        public bool ShowSeparator { get; } = showSeparator;
        public ICommand? Command { get; } = command;

        /// <summary>Alternatives reachable from this segment via an Explorer-style dropdown (e.g. sibling solutions).</summary>
        public IReadOnlyList<BreadcrumbChoice>? Choices { get; } = choices;
        public bool HasDropdown => Choices is { Count: > 0 };

        public bool IsClickable => Command != null && !IsCurrent;
    }

    /// <summary>One entry in a breadcrumb segment's dropdown.</summary>
    public sealed record BreadcrumbChoice(string Label, bool IsSelected, ICommand Command);

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

        /// <summary>True/false when the solver reports optimality status (Branch &amp; Price); null when not applicable.</summary>
        public bool? IsProvenOptimal { get; }

        /// <summary>LP lower bound on the pallet count, when the solver provides one.</summary>
        public double LowerBound { get; }

        public SolutionDisplay(
            int number,
            string name,
            AssignmentResult result,
            IReadOnlyList<SKU> skus,
            int palletLength,
            int palletWidth,
            int palletHeight,
            bool? isProvenOptimal = null,
            double? lowerBound = null)
        {
            Number = number;
            Name = name;
            Result = result;
            Skus = skus;
            PalletLength = palletLength;
            PalletWidth = palletWidth;
            PalletHeight = palletHeight;
            IsProvenOptimal = isProvenOptimal;
            LowerBound = lowerBound ?? 0;
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
