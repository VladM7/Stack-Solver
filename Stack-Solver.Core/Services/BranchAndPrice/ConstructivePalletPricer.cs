using Stack_Solver.Helpers.Layering;
using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Constructive, support-aware, dual-guided pricing subproblem. Unlike the stacking pricers
    /// (<see cref="PricingSolver"/>, <see cref="ExactPricingSolver"/>) — which only combine
    /// pre-generated layers from a fixed pool — this builds a pallet <b>bottom-up from boxes</b>,
    /// so it can synthesize layers the pool never contained (e.g. a single layer merging the
    /// leftovers of two SKUs). Guided by the demand-constraint duals π_i, it tiles each layer with
    /// boxes, then stacks the next layer on the occupied cells of the one below (100% inter-layer
    /// support), respecting the same physical rules as the other pricers: available height, stack
    /// weight, the top-heavy load-density order, and the distinct-SKU cap.
    ///
    /// <para>Each layer is packed SKU-by-SKU: a SKU is laid down in whichever orientation tiles the
    /// most boxes on the still-free support, then the next SKU fills the remaining gaps. This keeps
    /// layers dense and their geometry clean for display. A handful of attempts with different SKU
    /// fill orders give diversity.</para>
    ///
    /// <para>It <b>augments</b> the stacking pricers — every pallet it builds becomes a candidate
    /// column whose dual value Σ_i a_i·π_i, when it exceeds the reduced-cost threshold, is an
    /// improving column. Like <see cref="PricingSolver"/> it is a heuristic finder: it never proves
    /// the absence of improving columns, so it does not certify LP optimality (that remains the job
    /// of the exact pricer / the global certificate). Deterministic across runs.</para>
    /// </summary>
    public sealed class ConstructivePalletPricer
    {
        private const double Epsilon = 1e-6;

        private readonly IReadOnlyList<SKU> _skus;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<SkuVariant>> _variantsBySku;
        private readonly IReadOnlyDictionary<string, int> _demand;
        private readonly Pallet _pallet;
        private readonly int _gridStep;
        private readonly int _gridWidth;   // Y cells (along pallet Width)
        private readonly int _gridLength;  // X cells (along pallet Length)
        private readonly int _availHeight;
        private readonly double _maxWeight;
        private readonly double _tolerance;

        /// <summary>SKU fill orders tried per call, for column diversity.</summary>
        private enum FillStrategy { CoverageFirst, DualFirst, DemandFirst }
        private static readonly FillStrategy[] Strategies =
            [FillStrategy.CoverageFirst, FillStrategy.DualFirst, FillStrategy.DemandFirst];

        public ConstructivePalletPricer(
            IReadOnlyCollection<SKU> skus,
            IReadOnlyDictionary<string, int> demand,
            Pallet pallet)
        {
            ArgumentNullException.ThrowIfNull(skus);
            _demand = demand ?? throw new ArgumentNullException(nameof(demand));
            _pallet = pallet ?? throw new ArgumentNullException(nameof(pallet));

            // Only SKUs with positive demand can be packed; dedupe by id.
            _skus = skus.Where(s => s != null && demand.GetValueOrDefault(s.SkuId) > 0)
                        .GroupBy(s => s.SkuId, StringComparer.Ordinal)
                        .Select(g => g.First())
                        .ToList();

            _gridStep = ComputeGridStep(_skus);
            _variantsBySku = _skus.ToDictionary(
                s => s.SkuId,
                s => (IReadOnlyList<SkuVariant>)SkuVariantFactory.CreateAllOrientations([s]),
                StringComparer.Ordinal);
            _gridWidth = (int)Math.Ceiling((double)pallet.Width / _gridStep);
            _gridLength = (int)Math.Ceiling((double)pallet.Length / _gridStep);
            _availHeight = pallet.MaxStackHeight - pallet.Height;
            _maxWeight = pallet.MaxStackWeight;
            _tolerance = pallet.LoadDensityTolerance;
        }

        /// <summary>Maximum number of distinct improving columns returned per call.</summary>
        public int MaxColumns { get; init; } = 8;

        /// <summary>
        /// Builds pallets from boxes under the duals and returns up to <see cref="MaxColumns"/>
        /// distinct improving columns (dual value &gt; <paramref name="reducedCostThreshold"/>),
        /// highest value first. A handful of build attempts with different SKU fill priorities give
        /// diversity. Columns whose signature is in <paramref name="forbidden"/> (forbidden by a
        /// branching constraint) are never returned. Empty when none improves.
        /// </summary>
        public IReadOnlyList<BnpColumn> FindColumns(
            IReadOnlyDictionary<string, double> duals,
            IReadOnlySet<string>? forbidden = null,
            double reducedCostThreshold = 1.0)
        {
            ArgumentNullException.ThrowIfNull(duals);
            if (_availHeight <= 0 || _skus.Count == 0) return [];

            var best = new Dictionary<string, (BnpColumn Column, double Value)>(StringComparer.Ordinal);

            foreach (var strategy in Strategies)
            {
                var layers = BuildPallet(strategy, duals);
                if (layers.Count == 0) continue;

                var column = new BnpColumn(PalletTemplate.FromLayers(layers));
                if (forbidden != null && forbidden.Contains(column.Signature)) continue;

                double value = ColumnValue(layers, duals);
                if (value <= reducedCostThreshold + Epsilon) continue;

                if (!best.TryGetValue(column.Signature, out var existing) || value > existing.Value)
                    best[column.Signature] = (column, value);
            }

            return best.Values
                .OrderByDescending(e => e.Value)
                .Take(MaxColumns)
                .Select(e => e.Column)
                .ToList();
        }

        /// <summary>
        /// Builds one pallet bottom-up: each layer is packed on the occupancy of the layer below,
        /// then committed only if it satisfies the top-heavy load-density order. Stops when height is
        /// exhausted or no further box can be placed.
        /// </summary>
        private List<Layer> BuildPallet(FillStrategy strategy, IReadOnlyDictionary<string, double> duals)
        {
            var layers = new List<Layer>();
            // Remaining per-SKU cap (the demand) — a single pallet never carries more than total demand.
            var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var s in _skus) remaining[s.SkuId] = _demand.GetValueOrDefault(s.SkuId);

            // Support region starts as the whole pallet surface (everything supported).
            var support = new bool[_gridWidth, _gridLength];
            for (int y = 0; y < _gridWidth; y++)
                for (int x = 0; x < _gridLength; x++)
                    support[y, x] = true;

            int usedHeight = 0;
            double usedWeight = 0;
            double lowerDensity = double.PositiveInfinity; // first layer rests on the pallet: no density constraint
            var distinctSkus = new HashSet<string>(StringComparer.Ordinal);

            while (usedHeight < _availHeight)
            {
                var plan = BuildLayer(support, remaining, usedWeight, _availHeight - usedHeight, distinctSkus, strategy, duals);
                if (plan.Items.Count == 0) break;

                var layer = MaterializeLayer(plan);

                // Top-heavy load-density order: a layer may rest on the one below only when its load
                // density does not exceed it by more than the tolerance. The plan touched no shared
                // state, so a rejected layer is simply dropped and building stops.
                if (!StackingLoadRule.Allows(lowerDensity, layer.Metrics.LoadDensity, _tolerance)) break;

                foreach (var (sku, count) in plan.PlacedBySku) remaining[sku] -= count;
                usedHeight += plan.Height;
                usedWeight += layer.Metrics.TotalWeight;
                lowerDensity = layer.Metrics.LoadDensity;
                distinctSkus.UnionWith(plan.PlacedBySku.Keys);
                support = layer.Geometry!.OccupancyGrid;
                layers.Add(layer);
            }

            return layers;
        }

        /// <summary>
        /// Packs a single layer SKU-by-SKU. SKUs are ordered by the strategy; each is laid down in
        /// whichever orientation tiles the most boxes on the still-free support, the next filling the
        /// gaps. Every box sits entirely on the support region (100% support) without overlapping a
        /// box already placed, and respects the remaining cap, distinct-SKU cap, stack-weight limit,
        /// and remaining height. Touches no shared state — it reports the plan for the caller to commit.
        /// </summary>
        private LayerPlan BuildLayer(
            bool[,] support,
            IReadOnlyDictionary<string, int> remaining,
            double usedWeight,
            int heightBudget,
            IReadOnlySet<string> distinctSkus,
            FillStrategy strategy,
            IReadOnlyDictionary<string, double> duals)
        {
            var supportPrefix = BuildPrefixSum(support);
            var layerOcc = new bool[_gridWidth, _gridLength];
            var items = new List<PositionedItem>();
            var placed = new Dictionary<string, int>(StringComparer.Ordinal);
            var runningDistinct = new HashSet<string>(distinctSkus, StringComparer.Ordinal);
            double runningWeight = usedWeight;
            int layerHeight = 0;

            foreach (var sku in OrderSkus(support, supportPrefix, remaining, strategy, duals))
            {
                int cap = remaining.GetValueOrDefault(sku.SkuId);
                if (cap <= 0 || sku.Height > heightBudget) continue;
                if (!runningDistinct.Contains(sku.SkuId) && runningDistinct.Count >= PricingRules.MaxDistinctSkusPerTemplate) continue;

                var fill = TileBestOrientation(sku, layerOcc, supportPrefix, support, cap, runningWeight, heightBudget);
                if (fill.Count == 0) continue;

                layerOcc = fill.Occupancy;
                items.AddRange(fill.Items);
                placed[sku.SkuId] = placed.GetValueOrDefault(sku.SkuId) + fill.Count;
                runningWeight += fill.Weight;
                runningDistinct.Add(sku.SkuId);
                if (sku.Height > layerHeight) layerHeight = sku.Height;
            }

            return new LayerPlan(items, placed, layerHeight);
        }

        /// <summary>Orders the still-demanded SKUs for this layer per the diversity strategy.</summary>
        private IEnumerable<SKU> OrderSkus(
            bool[,] support,
            int[,] supportPrefix,
            IReadOnlyDictionary<string, int> remaining,
            FillStrategy strategy,
            IReadOnlyDictionary<string, double> duals)
        {
            var emptyOcc = new bool[_gridWidth, _gridLength];

            double Pi(SKU s) => duals.GetValueOrDefault(s.SkuId, 0.0);
            int Remaining(SKU s) => remaining.GetValueOrDefault(s.SkuId);
            // Area this SKU could cover on the empty support — its best single-SKU layer footprint.
            long Coverage(SKU s)
            {
                int cap = Remaining(s);
                if (cap <= 0) return 0;
                var fit = TileBestOrientation(s, emptyOcc, supportPrefix, support, cap, 0, s.Height);
                return (long)fit.Count * s.Length * s.Width;
            }

            var live = _skus.Where(s => Remaining(s) > 0).ToList();

            return strategy switch
            {
                FillStrategy.DualFirst => live
                    .OrderByDescending(Pi).ThenByDescending(Coverage).ThenBy(s => s.SkuId, StringComparer.Ordinal),
                FillStrategy.DemandFirst => live
                    .OrderByDescending(Remaining).ThenByDescending(Pi).ThenBy(s => s.SkuId, StringComparer.Ordinal),
                _ => live // CoverageFirst
                    .OrderByDescending(Coverage).ThenByDescending(Pi).ThenBy(s => s.SkuId, StringComparer.Ordinal),
            };
        }

        /// <summary>
        /// Tiles a SKU into the free support in whichever orientation places the most boxes (ties
        /// resolved to the non-rotated orientation for determinism). Returns the resulting occupancy
        /// grid (a fresh copy), the placed items, the count, and the added weight.
        /// </summary>
        private TileResult TileBestOrientation(
            SKU sku, bool[,] occupancy, int[,] supportPrefix, bool[,] support,
            int cap, double runningWeight, int heightBudget)
        {
            TileResult best = TileResult.Empty;
            foreach (var variant in _variantsBySku[sku.SkuId])
            {
                var result = TileVariant(variant, occupancy, supportPrefix, support, cap, runningWeight, heightBudget);
                if (result.Count > best.Count) best = result; // first (non-rotated) wins ties
            }
            return best;
        }

        /// <summary>Places one box variant repeatedly, row-major, into the free + supported cells.</summary>
        private TileResult TileVariant(
            SkuVariant variant, bool[,] occupancy, int[,] supportPrefix, bool[,] support,
            int cap, double runningWeight, int heightBudget)
        {
            if (variant.Sku.Height > heightBudget) return TileResult.Empty;

            int cellsX = Math.Max(1, (int)Math.Ceiling((double)variant.SpanX / _gridStep));
            int cellsY = Math.Max(1, (int)Math.Ceiling((double)variant.SpanY / _gridStep));
            double weight = variant.Sku.Weight;

            var occ = (bool[,])occupancy.Clone();
            var items = new List<PositionedItem>();
            int count = 0;
            double added = 0;

            for (int cy = 0; cy + cellsY <= _gridWidth; cy++)
            {
                for (int cx = 0; cx + cellsX <= _gridLength; cx++)
                {
                    if (count >= cap || runningWeight + added + weight > _maxWeight) goto done;
                    if (occ[cy, cx] || !support[cy, cx]) continue;
                    // 100% support: every footprint cell must lie on the support region.
                    if (RectSum(supportPrefix, cy, cx, cellsY, cellsX) != cellsX * cellsY) continue;
                    if (Overlaps(occ, cy, cx, cellsY, cellsX)) continue;

                    for (int y = cy; y < cy + cellsY; y++)
                        for (int x = cx; x < cx + cellsX; x++)
                            occ[y, x] = true;

                    items.Add(new PositionedItem(variant.Sku, cx * _gridStep, cy * _gridStep, variant.Rotated));
                    count++;
                    added += weight;
                }
            }

        done:
            return new TileResult(occ, items, count, added);
        }

        private Layer MaterializeLayer(LayerPlan plan)
        {
            var layer = new Layer("constructive", plan.Items, new LayerMetadata(0, plan.Height, "constructive box-built layer"));
            layer.Geometry = LayerGeometryBuilder.Build(layer, _pallet, _gridStep);
            layer.Metrics = LayerMetricsCalculator.Compute(layer, _pallet);
            layer.Metadata.Utilization = layer.Metrics.Utilization / 100.0;
            return layer;
        }

        /// <summary>Dual value Σ_i a_i·π_i of the built pallet.</summary>
        private static double ColumnValue(IReadOnlyList<Layer> layers, IReadOnlyDictionary<string, double> duals)
        {
            double value = 0;
            foreach (var layer in layers)
                foreach (var item in layer.Items)
                    value += duals.GetValueOrDefault(item.SkuType.SkuId, 0.0);
            return value;
        }

        private static bool Overlaps(bool[,] occ, int row, int col, int h, int w)
        {
            for (int y = row; y < row + h; y++)
                for (int x = col; x < col + w; x++)
                    if (occ[y, x]) return true;
            return false;
        }

        private int[,] BuildPrefixSum(bool[,] occupancy)
        {
            var prefix = new int[_gridWidth + 1, _gridLength + 1];
            for (int y = 0; y < _gridWidth; y++)
                for (int x = 0; x < _gridLength; x++)
                    prefix[y + 1, x + 1] = (occupancy[y, x] ? 1 : 0)
                        + prefix[y, x + 1] + prefix[y + 1, x] - prefix[y, x];
            return prefix;
        }

        // Count of set cells in rows [row, row+h) and cols [col, col+w); region must be within bounds.
        private static int RectSum(int[,] prefix, int row, int col, int h, int w)
        {
            int r2 = row + h;
            int c2 = col + w;
            return prefix[r2, c2] - prefix[row, c2] - prefix[r2, col] + prefix[row, col];
        }

        private static int ComputeGridStep(IEnumerable<SKU> skus)
        {
            int gcd = 0;
            foreach (var s in skus)
            {
                if (s == null) continue;
                gcd = Gcd(gcd, s.Length);
                gcd = Gcd(gcd, s.Width);
            }
            return Math.Max(gcd, 1);
        }

        private static int Gcd(int a, int b)
        {
            while (b != 0) { int t = b; b = a % b; a = t; }
            return a;
        }

        private readonly record struct LayerPlan(
            List<PositionedItem> Items,
            Dictionary<string, int> PlacedBySku,
            int Height);

        private readonly record struct TileResult(
            bool[,] Occupancy,
            List<PositionedItem> Items,
            int Count,
            double Weight)
        {
            public static TileResult Empty => new(new bool[0, 0], [], 0, 0);
        }
    }
}
