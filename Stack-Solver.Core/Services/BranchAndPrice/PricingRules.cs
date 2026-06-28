using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Shared feasibility and valuation rules for the pricing subproblem, used by both the
    /// heuristic and exact pricers so the two cannot drift apart. Encapsulates the pallet's
    /// available height and weight, the cached inter-layer support test, the dual-weighted
    /// layer value, and the distinct-SKU cap.
    /// </summary>
    internal sealed class PricingRules
    {
        public const int MaxDistinctSkusPerTemplate = 3;

        private readonly Pallet _pallet;
        private readonly Dictionary<(string, string), bool> _transitionCache = new();

        public PricingRules(Pallet pallet)
        {
            _pallet = pallet;
            AvailHeight = pallet.MaxStackHeight - pallet.Height;
        }

        /// <summary>Height available for layers above the pallet surface.</summary>
        public int AvailHeight { get; }

        public double MaxWeight => _pallet.MaxStackWeight;

        /// <summary>True if <paramref name="upper"/> may rest on <paramref name="lower"/> within the overhang limit.</summary>
        public bool TransitionValid(Layer lower, Layer upper)
        {
            var key = (lower.Id, upper.Id);
            if (_transitionCache.TryGetValue(key, out bool cached)) return cached;

            var support = LayerSupportAnalyzer.Analyze(lower, upper, _pallet);
            bool ok = support.MaximumSkuOverhangArea <= _pallet.MaxSkuOverhang;
            _transitionCache[key] = ok;
            return ok;
        }

        /// <summary>Dual-weighted value Σ_i count_i(layer)·π_i of a layer.</summary>
        public static double LayerValue(Layer layer, IReadOnlyDictionary<string, double> duals)
        {
            double value = 0;
            foreach (var item in layer.Items)
                if (duals.TryGetValue(item.SkuType.SkuId, out double pi))
                    value += pi;
            return value;
        }

        /// <summary>False if the layer contains a SKU that is not part of the master.</summary>
        public static bool AllSkusModeled(Layer layer, IReadOnlyDictionary<string, double> duals)
        {
            foreach (var sku in layer.Metrics.UsedSkuTypes)
                if (!duals.ContainsKey(sku)) return false;
            return true;
        }

        /// <summary>Distinct SKU count of <paramref name="skus"/> after adding <paramref name="layer"/>.</summary>
        public static int CountDistinct(HashSet<string> skus, Layer layer)
        {
            int extra = 0;
            foreach (var sku in layer.Metrics.UsedSkuTypes)
                if (!skus.Contains(sku)) extra++;
            return skus.Count + extra;
        }
    }
}
