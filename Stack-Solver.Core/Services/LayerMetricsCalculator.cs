using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services
{
    public static class LayerMetricsCalculator
    {
        public static LayerMetrics Compute(Layer layer, SupportSurface supportSurface)
        {
            ArgumentNullException.ThrowIfNull(layer);
            ArgumentNullException.ThrowIfNull(supportSurface);

            double palletArea = supportSurface.Length * supportSurface.Width;
            double palletCenterX = supportSurface.Length / 2.0;
            double palletCenterY = supportSurface.Width / 2.0;
            double maxCenterDistance = Math.Sqrt((palletCenterX * palletCenterX) + (palletCenterY * palletCenterY));

            double usedArea = 0;
            double totalWeight = 0;
            double weightedCenterX = 0;
            double weightedCenterY = 0;
            var distinctSkuIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var item in layer.Items)
            {
                var sku = item.SkuType;
                if (sku == null)
                    continue;

                int xSpan = item.GetXSpan();
                int ySpan = item.GetYSpan();

                double itemArea = xSpan * ySpan;
                double itemWeight = sku.Weight;
                double itemCenterX = item.X + (xSpan / 2.0);
                double itemCenterY = item.Y + (ySpan / 2.0);

                usedArea += itemArea;
                totalWeight += itemWeight;
                weightedCenterX += itemCenterX * itemWeight;
                weightedCenterY += itemCenterY * itemWeight;
                distinctSkuIds.Add(sku.SkuId);
            }

            double fillPercent = palletArea <= 0 ? 0 : (usedArea / palletArea) * 100.0;
            double centerX = totalWeight > 0 ? (weightedCenterX / totalWeight) : palletCenterX;
            double centerY = totalWeight > 0 ? (weightedCenterY / totalWeight) : palletCenterY;
            double cogDistance = Math.Sqrt(Math.Pow(centerX - palletCenterX, 2) + Math.Pow(centerY - palletCenterY, 2));
            double stabilityPercent = maxCenterDistance <= 0 ? 0 : (cogDistance / maxCenterDistance) * 100.0;

            return new LayerMetrics
            {
                Utilization = Math.Clamp(fillPercent, 0, 100),
                Stability = Math.Clamp(stabilityPercent, 0, 100),
                TotalWeight = totalWeight,
                FootprintArea = usedArea,
                UsedSkuTypes = [.. distinctSkuIds]
            };
        }

        public static List<Layer> FilterLayers(IReadOnlyList<Layer> layers, GenerationOptions options)
        {
            ArgumentNullException.ThrowIfNull(layers);
            ArgumentNullException.ThrowIfNull(options);

            var stabilityFiltered = FilterByStability(layers, options.MaxLayerStability);
            var deduplicated = RemoveIdenticalLayers(stabilityFiltered);
            return SelectTopFractionPerSkuByUtilization(deduplicated, options.PerSkuTopLayerFraction);
        }

        private static List<Layer> FilterByStability(IEnumerable<Layer> layers, double maxLayerStability)
        {
            return [.. layers.Where(layer => layer.Metrics.Stability <= maxLayerStability)];
        }

        private static List<Layer> RemoveIdenticalLayers(IEnumerable<Layer> layers)
        {
            var bestBySignature = new Dictionary<string, Layer>(StringComparer.Ordinal);

            foreach (var layer in layers)
            {
                string signature = BuildLayerSignature(layer);
                if (!bestBySignature.TryGetValue(signature, out var currentBest))
                {
                    bestBySignature[signature] = layer;
                    continue;
                }

                if (IsBetterCandidate(layer, currentBest))
                    bestBySignature[signature] = layer;
            }

            return [.. bestBySignature.Values];
        }

        private static List<Layer> SelectTopFractionPerSkuByUtilization(List<Layer> layers, double topFraction)
        {
            if (layers.Count == 0)
                return [];

            double normalizedFraction = Math.Clamp(topFraction, 0, 1);
            if (normalizedFraction <= 0)
                return [];

            var layerById = layers.ToDictionary(l => l.Id, StringComparer.Ordinal);
            var selectedIds = new HashSet<string>(StringComparer.Ordinal);

            var allSkuIds = layers
                .SelectMany(layer => layer.Metrics.UsedSkuTypes)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            foreach (var skuId in allSkuIds)
            {
                var containingLayers = layers
                    .Where(layer => layer.Metrics.UsedSkuTypes.Contains(skuId))
                    .OrderByDescending(layer => layer.Metrics.Utilization)
                    .ThenBy(layer => layer.Id, StringComparer.Ordinal)
                    .ToList();

                if (containingLayers.Count == 0)
                    continue;

                int keepCount = Math.Max(1, (int)Math.Ceiling(containingLayers.Count * normalizedFraction));
                foreach (var layer in containingLayers.Take(keepCount))
                    selectedIds.Add(layer.Id);
            }

            return [.. selectedIds
                .Select(id => layerById[id])
                .OrderByDescending(layer => layer.Metrics.Utilization)
                .ThenBy(layer => layer.Id, StringComparer.Ordinal)];
        }

        private static string BuildLayerSignature(Layer layer)
        {
            var parts = layer.Items
                .Select(item => $"{item.SkuType.SkuId}:{item.X}:{item.Y}:{item.Rotated}")
                .OrderBy(part => part, StringComparer.Ordinal);

            return string.Join("|", parts);
        }

        private static bool IsBetterCandidate(Layer candidate, Layer incumbent)
        {
            int utilizationComparison = candidate.Metrics.Utilization.CompareTo(incumbent.Metrics.Utilization);
            if (utilizationComparison != 0)
                return utilizationComparison > 0;

            int stabilityComparison = candidate.Metrics.Stability.CompareTo(incumbent.Metrics.Stability);
            if (stabilityComparison != 0)
                return stabilityComparison < 0;

            return string.Compare(candidate.Id, incumbent.Id, StringComparison.Ordinal) < 0;
        }
    }
}