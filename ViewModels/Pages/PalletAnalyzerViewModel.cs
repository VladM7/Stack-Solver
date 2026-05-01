using Stack_Solver.Infrastructure;
using Stack_Solver.Models;
using Stack_Solver.Models.Assignment;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services;
using Stack_Solver.Services.Stacking;
using System.Collections.ObjectModel;
using System.Text;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class PalletAnalyzerViewModel : ObservableObject
    {
        private readonly IEventAggregator _events;

        private List<Layer> _availableLayers = [];
        private int _palletLength, _palletWidth, _palletHeight;
        private int _maxStackHeight = 180, _maxStackWeight = 950;
        private double _maxSkuOverhang;
        private List<SKU> _selectedSkus = [];
        private GenerationOptions _generationOptions = new();

        [ObservableProperty]
        private bool _isBuilding;

        [ObservableProperty]
        private string _outputText = "Click 'Generate' to create candidate layers, then 'Build Pallets'.";

        [ObservableProperty]
        private ObservableCollection<TemplateAssignmentDisplay> _assignments = [];

        [ObservableProperty]
        private TemplateAssignmentDisplay? _selectedAssignment;

        [ObservableProperty]
        private bool _hasResults;

        [ObservableProperty]
        private bool _hasLayers;

        public PalletAnalyzerViewModel(IEventAggregator events)
        {
            _events = events;
            _events.Subscribe<LayersGeneratedMessage>(OnLayersGenerated);
            _events.Subscribe<SettingsChangedMessage>(OnSettingsChanged);
        }

        private void OnLayersGenerated(LayersGeneratedMessage msg)
        {
            _availableLayers = msg.Layers;
            HasLayers = _availableLayers.Count > 0;
            HasResults = false;
            Assignments.Clear();
            OutputText = $"{_availableLayers.Count} candidate layers ready. Click 'Build Pallets'.";
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
        }

        [RelayCommand]
        private async Task BuildPallets()
        {
            if (IsBuilding) return;

            if (_availableLayers.Count == 0)
            {
                OutputText = "No layers available. Click 'Generate' first.";
                return;
            }

            if (_selectedSkus.Count == 0)
            {
                OutputText = "No SKUs with quantity > 0.";
                return;
            }

            IsBuilding = true;
            HasResults = false;
            Assignments.Clear();
            OutputText = "Building pallets...";

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

                var result = await Task.Run(() =>
                {
                    var filtered = LayerMetricsCalculator.FilterLayers(_availableLayers, options);
                    var templates = PalletTemplateEnumerator.Enumerate(pallet, filtered, options);
                    return (
                        templates,
                        assignment: GreedyAssignmentService.Assign(templates, demand)
                    );
                });

                foreach (var (template, count) in result.assignment.Assignments)
                    Assignments.Add(new TemplateAssignmentDisplay(template, count, skus));

                SelectedAssignment = Assignments.FirstOrDefault();
                HasResults = Assignments.Count > 0;
                OutputText = BuildSummaryText(result.assignment, result.templates.Count, skus);
            }
            catch (Exception ex)
            {
                OutputText = $"Error: {ex.Message}";
            }
            finally
            {
                IsBuilding = false;
            }
        }

        private string BuildSummaryText(AssignmentResult result, int templateCount, List<SKU> skus)
        {
            var skuMap = skus.ToDictionary(s => s.SkuId, s => s.Name, StringComparer.Ordinal);
            var sb = new StringBuilder();
            sb.AppendLine($"Templates generated: {templateCount}");
            sb.AppendLine($"Total pallets:       {result.TotalPallets}");
            sb.AppendLine($"Distinct templates:  {result.Assignments.Count}");
            sb.AppendLine();
            if (result.HasLeftovers)
            {
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
        public string Header { get; }
        public string SkuSummary { get; }
        public IReadOnlyList<string> LayerLines { get; }

        public TemplateAssignmentDisplay(PalletTemplate template, int count, IEnumerable<SKU> skuLookup)
        {
            Template = template;
            Count = count;

            var skuMap = skuLookup.ToDictionary(s => s.SkuId, s => s.Name, StringComparer.Ordinal);

            Header = $"{count}× — {template.TotalBoxCount} boxes, {template.Layers.Count} layer(s), " +
                     $"H={template.TotalHeight:F0}, W={template.TotalWeight:F1} kg, " +
                     $"{template.AverageLayerUtilization:P0} avg util";

            SkuSummary = string.Join("  |  ", template.SkuCounts
                .OrderBy(kvp => skuMap.GetValueOrDefault(kvp.Key, kvp.Key), StringComparer.Ordinal)
                .Select(kvp => $"{skuMap.GetValueOrDefault(kvp.Key, kvp.Key)} ×{kvp.Value}"));

            LayerLines = template.Layers
                .Select((l, i) => $"Layer {i + 1}  {l.Name}  —  {l.Items.Count} items, H={l.Metadata.Height}, {l.Metadata.Utilization:P0} util")
                .ToList();
        }
    }
}
