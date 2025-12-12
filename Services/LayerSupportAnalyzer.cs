using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services
{
    public static class LayerSupportAnalyzer
    {
        public static LayerSupportMetrics Analyze(Layer? lowerLayer, Layer upperLayer, SupportSurface supportSurface, int gridStep = 1)
        {
            ArgumentNullException.ThrowIfNull(upperLayer);
            ArgumentNullException.ThrowIfNull(supportSurface);
            if (gridStep <= 0)
                throw new ArgumentOutOfRangeException(nameof(gridStep), "gridStep must be non-zero and positive");

            var upperGeometry = LayerGeometryBuilder.Build(upperLayer, supportSurface, gridStep);
            LayerGeometry? lowerGeometry = null;
            if (lowerLayer != null && lowerLayer.Items.Count > 0)
            {
                lowerGeometry = LayerGeometryBuilder.Build(lowerLayer, supportSurface, gridStep);
            }

            var upperMap = upperGeometry.ItemIndexGrid;
            var lowerGrid = lowerGeometry?.OccupancyGrid;
            double cellArea = gridStep * gridStep;

            var unsupportedCellCounts = new Dictionary<int, int>();
            int width = upperGeometry.Width;
            int length = upperGeometry.Length;

            for (int y = 0; y < width; y++)
            {
                for (int x = 0; x < length; x++)
                {
                    int itemIndex = upperMap[y, x];
                    if (itemIndex < 0)
                        continue;

                    bool hasSupport = lowerGrid != null &&
                        y < lowerGrid.GetLength(0) &&
                        x < lowerGrid.GetLength(1) &&
                        lowerGrid[y, x];

                    if (hasSupport)
                        continue;

                    unsupportedCellCounts[itemIndex] = unsupportedCellCounts.TryGetValue(itemIndex, out var count)
                        ? count + 1
                        : 1;
                }
            }

            double totalUnsupportedArea = 0;
            double maxSkuOverhangArea = 0;
            foreach (var kvp in unsupportedCellCounts)
            {
                double area = kvp.Value * cellArea;
                totalUnsupportedArea += area;
                if (area > maxSkuOverhangArea)
                    maxSkuOverhangArea = area;
            }

            return new LayerSupportMetrics(totalUnsupportedArea, maxSkuOverhangArea);
        }
    }
}
