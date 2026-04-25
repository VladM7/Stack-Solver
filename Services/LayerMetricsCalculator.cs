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
                UsedSkuTypes = [.. distinctSkuIds]
            };
        }

        public static void ComputeCompatibility(IReadOnlyList<Layer> layers)
        {
            ArgumentNullException.ThrowIfNull(layers);

            foreach (var baseLayer in layers)
            {
                var compatibleTopLayerIds = new List<string>();

                foreach (var topLayer in layers)
                {
                    if (ReferenceEquals(baseLayer, topLayer))
                        continue;

                    if (CanStackWithoutOverhang(baseLayer, topLayer))
                        compatibleTopLayerIds.Add(topLayer.Id);
                }

                baseLayer.Metrics.CompatibleTopLayerIds = compatibleTopLayerIds;
            }
        }

        private static bool CanStackWithoutOverhang(Layer bottomLayer, Layer topLayer)
        {
            if (topLayer.Geometry == null)
                return false;

            if (bottomLayer.Geometry == null)
                return false;

            var topMap = topLayer.Geometry.ItemIndexGrid;
            var bottomGrid = bottomLayer.Geometry.OccupancyGrid;

            int width = topLayer.Geometry.Width;
            int length = topLayer.Geometry.Length;

            for (int y = 0; y < width; y++)
            {
                for (int x = 0; x < length; x++)
                {
                    int itemIndex = topMap[y, x];
                    if (itemIndex < 0)
                        continue;

                    bool hasSupport = y < bottomGrid.GetLength(0) &&
                        x < bottomGrid.GetLength(1) &&
                        bottomGrid[y, x];

                    if (!hasSupport)
                        return false;
                }
            }

            return true;
        }
    }
}