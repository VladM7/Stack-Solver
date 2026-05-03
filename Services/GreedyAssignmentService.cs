using Stack_Solver.Models.Assignment;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services
{
    /// <summary>
    /// Builds pallets one layer at a time (Strategy B from the greedy palletization plan).
    /// At each step the highest-scoring candidate layer is selected from the pool: it must
    /// fit within height/weight limits and must not exceed remaining per-SKU demand.
    /// Opens new pallets until demand is exhausted or no layer can be placed.
    /// Identical pallets (same layer sequence) are grouped in the output.
    /// </summary>
    public static class GreedyAssignmentService
    {
        public static AssignmentResult Assign(
            IReadOnlyList<Layer> layers,
            IReadOnlyDictionary<string, int> demand,
            Pallet pallet)
        {
            var remaining = new Dictionary<string, int>(demand, StringComparer.Ordinal);
            var builtPallets = new List<PalletTemplate>();

            while (remaining.Values.Any(v => v > 0))
            {
                var template = PackOnePallet(layers, remaining, pallet);
                if (template == null) break;
                builtPallets.Add(template);
            }

            var assignments = builtPallets
                .GroupBy(PalletSignature, StringComparer.Ordinal)
                .Select(g => (Template: g.First(), Count: g.Count()))
                .ToList();

            var leftovers = remaining
                .Where(kvp => kvp.Value > 0)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);

            return new AssignmentResult { Assignments = assignments, Leftovers = leftovers };
        }

        private static PalletTemplate? PackOnePallet(
            IReadOnlyList<Layer> layers,
            Dictionary<string, int> remaining,
            Pallet pallet)
        {
            var stackedLayers = new List<Layer>();
            double usedHeight = pallet.Height;
            double usedWeight = 0;

            while (true)
            {
                var best = SelectBestLayer(layers, stackedLayers, remaining, pallet, usedHeight, usedWeight);
                if (best == null) break;

                foreach (var item in best.Items)
                {
                    var skuId = item.SkuType.SkuId;
                    if (remaining.ContainsKey(skuId))
                        remaining[skuId] = Math.Max(0, remaining[skuId] - 1);
                }

                stackedLayers.Add(best);
                usedHeight += best.Metadata.Height;
                usedWeight += best.Metrics.TotalWeight;
            }

            return stackedLayers.Count > 0 ? PalletTemplate.FromLayers(stackedLayers) : null;
        }

        private static Layer? SelectBestLayer(
            IReadOnlyList<Layer> layers,
            IReadOnlyList<Layer> stackedLayers,
            IReadOnlyDictionary<string, int> remaining,
            Pallet pallet,
            double usedHeight,
            double usedWeight)
        {
            Layer? best = null;
            double bestScore = double.MinValue;

            foreach (var layer in layers)
            {
                if (!CanAddLayer(layer, stackedLayers, pallet, usedHeight, usedWeight)) continue;
                if (!FitsWithinDemand(layer, remaining)) continue;

                double score = ScoreLayer(layer);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = layer;
                }
            }

            return best;
        }

        private static bool CanAddLayer(
            Layer layer,
            IReadOnlyList<Layer> stackedLayers,
            Pallet pallet,
            double usedHeight,
            double usedWeight)
        {
            if (usedHeight + layer.Metadata.Height > pallet.MaxStackHeight) return false;
            if (usedWeight + layer.Metrics.TotalWeight > pallet.MaxStackWeight) return false;

            if (stackedLayers.Count > 0)
            {
                var support = LayerSupportAnalyzer.Analyze(stackedLayers[^1], layer, pallet);
                if (support.MaximumSkuOverhangArea > pallet.MaxSkuOverhang) return false;
            }

            return true;
        }

        private static bool FitsWithinDemand(Layer layer, IReadOnlyDictionary<string, int> remaining)
        {
            foreach (var g in layer.Items.GroupBy(i => i.SkuType.SkuId, StringComparer.Ordinal))
            {
                if (!remaining.TryGetValue(g.Key, out int rem) || rem < g.Count())
                    return false;
            }
            return true;
        }

        private static double ScoreLayer(Layer layer)
        {
            // Maximize boxes packed per layer; break ties by utilization
            return layer.Items.Count * 1000.0 + layer.Metrics.Utilization;
        }

        private static string PalletSignature(PalletTemplate t) =>
            string.Join("|", t.Layers.Select(l => l.Id));
    }
}
