using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Exact pricing by depth-first branch-and-bound. Returns the maximum-dual-value valid
    /// pallet column, or null when the best value does not exceed 1 (no improving column).
    /// When the search finishes within <see cref="NodeBudget"/> the result is exhaustive and
    /// <see cref="LastSearchExhaustive"/> is true, certifying that the column generation has
    /// reached the true LP optimum; otherwise the absence of a column is not a proof.
    ///
    /// Stacks obey the same <see cref="PricingRules"/> as the heuristic pricer (height,
    /// weight, weight ordering, support, distinct-SKU cap), with no artificial layer-count
    /// cap. Layers are sorted by non-increasing weight and equal-weight layers are explored
    /// in non-decreasing index order to prune permutations; this loses no optimum when
    /// support is symmetric within an equal-weight group (true for full-coverage layers).
    /// </summary>
    public sealed class ExactPricingSolver
    {
        private const double Epsilon = 1e-6;

        private readonly IReadOnlyList<Layer> _layers;
        private readonly PricingRules _rules;

        public ExactPricingSolver(IReadOnlyList<Layer> layers, Pallet pallet)
        {
            _layers = layers ?? throw new ArgumentNullException(nameof(layers));
            ArgumentNullException.ThrowIfNull(pallet);
            _rules = new PricingRules(pallet);
        }

        /// <summary>Maximum number of search nodes before the search aborts as non-exhaustive.</summary>
        public long NodeBudget { get; init; } = 5_000_000;

        /// <summary>True iff the most recent <see cref="FindBestColumn"/> explored the whole tree.</summary>
        public bool LastSearchExhaustive { get; private set; }

        /// <param name="duals">Demand-constraint duals π_i.</param>
        /// <param name="forbidden">Column signatures barred by branching; never returned, but still traversed as prefixes.</param>
        public BnpColumn? FindBestColumn(
            IReadOnlyDictionary<string, double> duals,
            IReadOnlySet<string>? forbidden = null)
        {
            ArgumentNullException.ThrowIfNull(duals);
            LastSearchExhaustive = true;
            if (_rules.AvailHeight <= 0) return null;

            var cands = BuildCandidates(duals);
            if (cands.Count == 0) return null;

            double density = 0;
            foreach (var c in cands) density = Math.Max(density, c.Value / c.Height);

            var ctx = new SearchContext(cands, _rules, density, NodeBudget, forbidden);
            for (int i = 0; i < cands.Count; i++)
            {
                ctx.Descend(i);
                if (ctx.BudgetExhausted) break;
            }

            LastSearchExhaustive = !ctx.BudgetExhausted;

            if (ctx.BestValue > 1.0 + Epsilon && ctx.BestStack.Count > 0)
            {
                var layers = ctx.BestStack.Select(i => cands[i].Layer).ToList();
                return new BnpColumn(PalletTemplate.FromLayers(layers));
            }
            return null;
        }

        private List<Cand> BuildCandidates(IReadOnlyDictionary<string, double> duals)
        {
            var cands = new List<Cand>();
            foreach (var layer in _layers)
            {
                if (layer.Metadata.Height <= 0 || layer.Metadata.Height > _rules.AvailHeight) continue;
                if (layer.Metrics.TotalWeight > _rules.MaxWeight) continue;
                if (!PricingRules.AllSkusModeled(layer, duals)) continue;

                double value = PricingRules.LayerValue(layer, duals);
                if (value <= 0) continue;
                cands.Add(new Cand(layer, value, layer.Metadata.Height, layer.Metrics.TotalWeight));
            }
            // Non-increasing weight makes weight tiers contiguous so the equal-weight
            // tie-break (by index) canonicalises permutations.
            cands.Sort(static (a, b) => b.Weight.CompareTo(a.Weight));
            return cands;
        }

        private readonly record struct Cand(Layer Layer, double Value, int Height, double Weight);

        private sealed class SearchContext
        {
            private readonly List<Cand> _cands;
            private readonly PricingRules _rules;
            private readonly double _density;
            private readonly int _availH;
            private readonly IReadOnlySet<string>? _forbidden;
            private long _budget;

            private readonly List<int> _stack = [];                                 // candidate indices, bottom→top
            private readonly Dictionary<string, int> _skuRefs = new(StringComparer.Ordinal);
            private int _distinct;
            private double _value;
            private int _usedHeight;
            private double _usedWeight;

            public double BestValue { get; private set; }
            public List<int> BestStack { get; } = [];
            public bool BudgetExhausted { get; private set; }

            public SearchContext(List<Cand> cands, PricingRules rules, double density, long budget, IReadOnlySet<string>? forbidden)
            {
                _cands = cands;
                _rules = rules;
                _density = density;
                _budget = budget;
                _availH = rules.AvailHeight;
                _forbidden = forbidden;
            }

            public void Descend(int i)
            {
                if (BudgetExhausted) return;
                var c = _cands[i];

                if (_stack.Count > 0)
                {
                    var top = _cands[_stack[^1]];
                    if (c.Weight > top.Weight) return;                              // weight ordering
                    if (c.Weight == top.Weight && i < _stack[^1]) return;          // canonical tie-break
                    if (!_rules.TransitionValid(top.Layer, c.Layer)) return;
                }
                if (_usedHeight + c.Height > _availH) return;
                if (_usedWeight + c.Weight > _rules.MaxWeight) return;
                if (DistinctAfter(c.Layer) > PricingRules.MaxDistinctSkusPerTemplate) return;

                if (_budget-- <= 0) { BudgetExhausted = true; return; }

                Push(i, c);

                if (_value > BestValue && !IsForbidden())
                {
                    BestValue = _value;
                    BestStack.Clear();
                    BestStack.AddRange(_stack);
                }

                // Optimistic bound: fill the remaining height at the best value density.
                double bound = _value + _density * (_availH - _usedHeight);
                if (bound > BestValue + Epsilon)
                {
                    for (int j = 0; j < _cands.Count; j++)
                    {
                        Descend(j);
                        if (BudgetExhausted) break;
                    }
                }

                Pop(c);
            }

            private bool IsForbidden()
            {
                if (_forbidden == null || _forbidden.Count == 0) return false;
                return _forbidden.Contains(CurrentSignature());
            }

            // Signature of the current stack, matching BnpColumn.BuildSignature (per-item SKU
            // counts aggregated over all layers, ordered by SKU id).
            private string CurrentSignature()
            {
                var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (int idx in _stack)
                    foreach (var item in _cands[idx].Layer.Items)
                        counts[item.SkuType.SkuId] = counts.GetValueOrDefault(item.SkuType.SkuId) + 1;
                return string.Join("|", counts.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
            }

            private int DistinctAfter(Layer layer)
            {
                int extra = 0;
                foreach (var sku in layer.Metrics.UsedSkuTypes)
                    if (_skuRefs.GetValueOrDefault(sku) == 0) extra++;
                return _distinct + extra;
            }

            private void Push(int i, in Cand c)
            {
                _stack.Add(i);
                _value += c.Value;
                _usedHeight += c.Height;
                _usedWeight += c.Weight;
                foreach (var sku in c.Layer.Metrics.UsedSkuTypes)
                {
                    int n = _skuRefs.GetValueOrDefault(sku);
                    if (n == 0) _distinct++;
                    _skuRefs[sku] = n + 1;
                }
            }

            private void Pop(in Cand c)
            {
                _stack.RemoveAt(_stack.Count - 1);
                _value -= c.Value;
                _usedHeight -= c.Height;
                _usedWeight -= c.Weight;
                foreach (var sku in c.Layer.Metrics.UsedSkuTypes)
                {
                    int n = _skuRefs[sku] - 1;
                    _skuRefs[sku] = n;
                    if (n == 0) _distinct--;
                }
            }
        }
    }
}
