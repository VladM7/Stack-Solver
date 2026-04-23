using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services
{
    public static class LayerSupportAnalyzer
    {
        /// <summary>
        /// Analyzes the support of a layer placed on top of another layer and a support surface.
        /// Computes the SKU overhang and the total unsupported area.
        /// </summary>
        /// <remarks>If lowerLayer is null or contains no items, only the support surface is considered
        /// for support calculations.</remarks>
        /// <param name="lowerLayer">The optional lower layer to consider for support analysis. If provided and contains items, its geometry will
        /// be used to determine the support for the upper layer.</param>
        /// <param name="upperLayer">The upper layer.</param>
        /// <param name="supportSurface">The support surface on which the layers are analyzed.</param>
        /// <param name="gridStep">The size, in units, of each grid cell used for the analysis. Must be a positive, non-zero integer.</param>
        /// <returns>A LayerSupportMetrics object containing the total unsupported area and the maximum unsupported area for any
        /// single SKU in the upper layer.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when gridStep is less than or equal to zero.</exception>
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
