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

        /// <summary>
        /// Searches for the on-pallet translation of <paramref name="upperLayer"/> that best rests on
        /// <paramref name="lowerLayer"/>. Unlike <see cref="Analyze"/>, which scores the upper layer at its
        /// current position, this sweeps every offset that keeps the upper layer fully on the pallet and
        /// returns the one that maximizes support (then keeps the load most centered). This is what lets a
        /// layer whose boxes happen to be packed off-centre still stack on a differently-arranged layer:
        /// the boxes are simply shifted onto the support below.
        /// </summary>
        /// <param name="lowerLayer">The layer beneath, already positioned. If null/empty, nothing supports the upper layer.</param>
        /// <param name="upperLayer">The layer to place. Its items are treated as a rigid group; the search finds a translation, not a re-pack.</param>
        /// <param name="supportSurface">The pallet surface bounding the search.</param>
        /// <param name="maxOverhang">The maximum unsupported area allowed for any single SKU; drives the <see cref="PlacementFit.Feasible"/> flag.</param>
        /// <param name="gridStep">The grid cell size, in units. Must be positive.</param>
        public static PlacementFit FindBestPlacement(
            Layer? lowerLayer,
            Layer upperLayer,
            SupportSurface supportSurface,
            double maxOverhang,
            int gridStep = 1)
        {
            ArgumentNullException.ThrowIfNull(upperLayer);
            ArgumentNullException.ThrowIfNull(supportSurface);
            if (gridStep <= 0)
                throw new ArgumentOutOfRangeException(nameof(gridStep), "gridStep must be non-zero and positive");

            int gridWidth = (int)Math.Ceiling((double)supportSurface.Width / gridStep);   // Y cells
            int gridLength = (int)Math.Ceiling((double)supportSurface.Length / gridStep);  // X cells
            double cellArea = (double)gridStep * gridStep;

            bool[,] lowerOcc = lowerLayer != null && lowerLayer.Items.Count > 0
                ? LayerGeometryBuilder.Build(lowerLayer, supportSurface, gridStep).OccupancyGrid
                : new bool[gridWidth, gridLength];
            int[,] prefix = BuildPrefixSum(lowerOcc, gridWidth, gridLength);

            // Per-item cell rectangles (clipped to the pallet, matching LayerGeometryBuilder).
            int n = upperLayer.Items.Count;
            if (n == 0)
                return new PlacementFit(true, 0, 0, new LayerSupportMetrics(0, 0));

            var sx = new int[n];
            var sy = new int[n];
            var w = new int[n];
            var h = new int[n];
            var cells = new int[n];
            double weightedX = 0, weightedY = 0, totalWeightOrArea = 0;

            int minSx = int.MaxValue, minSy = int.MaxValue, maxSxEnd = int.MinValue, maxSyEnd = int.MinValue;
            for (int i = 0; i < n; i++)
            {
                var item = upperLayer.Items[i];
                int xSpan = item.GetXSpan();
                int ySpan = item.GetYSpan();

                int cx = Math.Clamp(item.X / gridStep, 0, gridLength - 1);
                int cy = Math.Clamp(item.Y / gridStep, 0, gridWidth - 1);
                int cw = Math.Min(Math.Max(1, (int)Math.Ceiling((double)xSpan / gridStep)), gridLength - cx);
                int ch = Math.Min(Math.Max(1, (int)Math.Ceiling((double)ySpan / gridStep)), gridWidth - cy);

                sx[i] = cx; sy[i] = cy; w[i] = cw; h[i] = ch; cells[i] = cw * ch;

                minSx = Math.Min(minSx, cx);
                minSy = Math.Min(minSy, cy);
                maxSxEnd = Math.Max(maxSxEnd, cx + cw);
                maxSyEnd = Math.Max(maxSyEnd, cy + ch);

                double weight = item.SkuType?.Weight > 0 ? item.SkuType.Weight : xSpan * (double)ySpan;
                weightedX += (item.X + xSpan / 2.0) * weight;
                weightedY += (item.Y + ySpan / 2.0) * weight;
                totalWeightOrArea += weight;
            }

            double baseComX = totalWeightOrArea > 0 ? weightedX / totalWeightOrArea : supportSurface.Length / 2.0;
            double baseComY = totalWeightOrArea > 0 ? weightedY / totalWeightOrArea : supportSurface.Width / 2.0;
            double palletCenterX = supportSurface.Length / 2.0;
            double palletCenterY = supportSurface.Width / 2.0;

            // Offset range (in cells) that keeps every item on the pallet.
            int dcxLo = -minSx, dcxHi = gridLength - maxSxEnd;
            int dcyLo = -minSy, dcyHi = gridWidth - maxSyEnd;

            int bestDcx = 0, bestDcy = 0;
            double bestMaxOverhang = double.PositiveInfinity, bestTotal = double.PositiveInfinity, bestComDist = double.PositiveInfinity;
            int bestManhattan = int.MaxValue;

            for (int dcy = dcyLo; dcy <= dcyHi; dcy++)
            {
                for (int dcx = dcxLo; dcx <= dcxHi; dcx++)
                {
                    double maxItemOverhang = 0, totalUnsupported = 0;
                    for (int i = 0; i < n; i++)
                    {
                        int ry = sy[i] + dcy;
                        int rx = sx[i] + dcx;
                        int supported = RectSum(prefix, ry, rx, h[i], w[i]);
                        int unsupported = cells[i] - supported;
                        if (unsupported <= 0) continue;

                        double area = unsupported * cellArea;
                        totalUnsupported += area;
                        if (area > maxItemOverhang) maxItemOverhang = area;
                    }

                    double comDist = Math.Sqrt(
                        Math.Pow(baseComX + dcx * gridStep - palletCenterX, 2) +
                        Math.Pow(baseComY + dcy * gridStep - palletCenterY, 2));
                    int manhattan = Math.Abs(dcx) + Math.Abs(dcy);

                    if (IsBetter(maxItemOverhang, totalUnsupported, comDist, manhattan,
                                 bestMaxOverhang, bestTotal, bestComDist, bestManhattan))
                    {
                        bestMaxOverhang = maxItemOverhang;
                        bestTotal = totalUnsupported;
                        bestComDist = comDist;
                        bestManhattan = manhattan;
                        bestDcx = dcx;
                        bestDcy = dcy;
                    }
                }
            }

            bool feasible = bestMaxOverhang <= maxOverhang;
            return new PlacementFit(
                feasible,
                bestDcx * gridStep,
                bestDcy * gridStep,
                new LayerSupportMetrics(bestTotal, bestMaxOverhang));
        }

        // Lexicographic preference: least overhang, then least total unsupported, then most centered, then least shift.
        private static bool IsBetter(
            double maxOverhang, double total, double comDist, int manhattan,
            double bestMaxOverhang, double bestTotal, double bestComDist, int bestManhattan)
        {
            if (maxOverhang != bestMaxOverhang) return maxOverhang < bestMaxOverhang;
            if (total != bestTotal) return total < bestTotal;
            if (comDist != bestComDist) return comDist < bestComDist;
            return manhattan < bestManhattan;
        }

        private static int[,] BuildPrefixSum(bool[,] occupancy, int height, int width)
        {
            var prefix = new int[height + 1, width + 1];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    prefix[y + 1, x + 1] = (occupancy[y, x] ? 1 : 0)
                        + prefix[y, x + 1] + prefix[y + 1, x] - prefix[y, x];
                }
            }
            return prefix;
        }

        // Count of set cells in rows [row, row+h) and cols [col, col+w); region must be within bounds.
        private static int RectSum(int[,] prefix, int row, int col, int h, int w)
        {
            int r2 = row + h;
            int c2 = col + w;
            return prefix[r2, c2] - prefix[row, c2] - prefix[r2, col] + prefix[row, col];
        }
    }
}
