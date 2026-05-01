using Stack_Solver.Models.Layering;

namespace Stack_Solver.Models.Supports
{
    public class PalletTemplate
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public IReadOnlyList<Layer> Layers { get; init; } = [];
        public double TotalHeight { get; init; }
        public double TotalWeight { get; init; }
        public IReadOnlyDictionary<string, int> SkuCounts { get; init; } = new Dictionary<string, int>();
        public int TotalBoxCount { get; init; }
        public double AverageLayerUtilization { get; init; }

        public static PalletTemplate FromLayers(IReadOnlyList<Layer> layers)
        {
            var skuCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var layer in layers)
                foreach (var item in layer.Items)
                    skuCounts[item.SkuType.SkuId] = skuCounts.GetValueOrDefault(item.SkuType.SkuId) + 1;

            return new PalletTemplate
            {
                Layers = layers,
                TotalHeight = layers.Sum(l => l.Metadata.Height),
                TotalWeight = layers.Sum(l => l.Metrics.TotalWeight),
                SkuCounts = skuCounts,
                TotalBoxCount = layers.Sum(l => l.Items.Count),
                AverageLayerUtilization = layers.Count > 0 ? layers.Average(l => l.Metadata.Utilization) : 0
            };
        }
    }
}
