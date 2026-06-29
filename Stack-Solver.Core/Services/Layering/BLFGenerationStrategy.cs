using Stack_Solver.Helpers.Layering;
using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Layering
{
    public class BLFGenerationStrategy : ILayerGenerationStrategy
    {
        public string Name => "BLF";

        public List<Layer> Generate(List<SKU> skus, SupportSurface supportSurface, GenerationOptions options)
        {
            if (skus == null || skus.Count == 0 || supportSurface == null)
                return [];

            int px = supportSurface.Length;
            int py = supportSurface.Width;
            if (px <= 0 || py <= 0)
                return [];

            int attempts = options?.BLFAttempts > 0 ? options.BLFAttempts : 200;
            double area = px * py;
            var variants = SkuVariantFactory.CreateAllOrientations(skus);
            if (variants.Count == 0)
                return [];

            var rand = new Random(Environment.TickCount);
            var candidateLayers = new List<Layer>();

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var layer = BuildLayerAttempt(variants, skus, px, py, area, attempt, rand, supportSurface);
                if (layer != null)
                    candidateLayers.Add(layer);
            }

            var selectedLayers = LayerCandidateHelper.SelectBestBySkuCounts(candidateLayers);
            return selectedLayers;
        }

        private static Layer? BuildLayerAttempt(IReadOnlyList<SkuVariant> variants, List<SKU> skus, int maxWidth, int maxDepth, double area, int attemptIndex, Random rand, SupportSurface supportSurface)
        {
            if (variants.Count == 0)
                return null;

            var randomized = variants.OrderBy(_ => rand.Next()).ToList();
            var counts = skus.ToDictionary(s => s.SkuId, _ => 0);
            var placements = new List<PositionedItem>();
            int y = 0;
            double usedArea = 0;

            while (y < maxDepth)
            {
                if (!TrySelectRowSeed(randomized, maxDepth - y, maxWidth, counts, out var seed))
                    break;

                int rowHeight = seed.SpanY;
                if (rowHeight <= 0)
                    break;

                double rowArea = FillRow(randomized, rowHeight, maxWidth, y, counts, placements);
                if (rowArea <= 0)
                    break;

                usedArea += rowArea;
                y += rowHeight;
            }

            if (placements.Count == 0)
                return null;

            double utilization = area <= 0 ? 0 : usedArea / area;
            int layerHeight = placements.Max(p => p.SkuType.Height);
            int boxes = placements.Count;

            var metadata = new LayerMetadata(utilization, layerHeight, $"BLF attempt {attemptIndex}, boxes={boxes}, util={utilization:F3}");
            var layer = new Layer($"blf_attempt_{attemptIndex}", placements, metadata);
            layer.Geometry = LayerGeometryBuilder.Build(layer, supportSurface);
            layer.Metrics = LayerMetricsCalculator.Compute(layer, supportSurface);
            return layer;
        }

        private static bool TrySelectRowSeed(IEnumerable<SkuVariant> variants, int remainingDepth, int maxWidth, IDictionary<string, int> counts, out SkuVariant seed)
        {
            foreach (var variant in variants)
            {
                if (!Fits(variant, remainingDepth, maxWidth))
                    continue;

                if (!HasInventory(variant, counts))
                    continue;

                seed = variant;
                return true;
            }

            seed = default;
            return false;
        }

        private static double FillRow(IReadOnlyList<SkuVariant> order, int rowHeight, int maxWidth, int yOffset, IDictionary<string, int> counts, ICollection<PositionedItem> placements)
        {
            int x = 0;
            double area = 0;

            while (x < maxWidth)
            {
                if (!TryFindPlacementCandidate(order, rowHeight, maxWidth - x, counts, out var candidate))
                    break;

                placements.Add(new PositionedItem(candidate.Sku, x, yOffset, candidate.Rotated));
                counts[candidate.Sku.SkuId]++;
                x += candidate.SpanX;
                area += candidate.SpanX * candidate.SpanY;
            }

            return area;
        }

        private static bool TryFindPlacementCandidate(IEnumerable<SkuVariant> variants, int rowHeight, int remainingWidth, IDictionary<string, int> counts, out SkuVariant candidate)
        {
            foreach (var variant in variants)
            {
                if (!Fits(variant, rowHeight, remainingWidth))
                    continue;

                if (!HasInventory(variant, counts))
                    continue;

                candidate = variant;
                return true;
            }

            candidate = default;
            return false;
        }

        private static bool Fits(SkuVariant variant, int maxHeight, int maxWidth)
        {
            return variant.Sku != null && variant.SpanY <= maxHeight && variant.SpanX <= maxWidth && variant.SpanY > 0 && variant.SpanX > 0;
        }

        private static bool HasInventory(SkuVariant variant, IDictionary<string, int> counts)
        {
            if (!counts.TryGetValue(variant.Sku.SkuId, out var placed))
                return false;

            if (variant.Sku.Quantity <= 0)
                return true;

            return placed < variant.Sku.Quantity;
        }
    }
}
