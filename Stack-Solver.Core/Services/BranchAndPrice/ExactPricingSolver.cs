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
    /// weight, load-density ordering, support, distinct-SKU cap), with no artificial layer-count
    /// cap. Layers are sorted by non-increasing load density and equal-density layers are explored
    /// in non-decreasing index order to prune permutations; this loses no optimum when
    /// support is symmetric within an equal-density group (true for full-coverage layers). A
    /// positive top-heavy tolerance additionally permits a denser layer on a less-dense one,
    /// which relaxes the canonical pruning (more permutations explored) but only when enabled.
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

        /// <summary>
        /// Node budget used when the transposition table is unavailable (a forbidden set is active,
        /// i.e. inside branch-and-bound). Without memoization the DFS is exponential, so this caps a
        /// single deep-node pricing call to stop it monopolizing the solver's whole time budget; such
        /// a call is reported non-exhaustive, never wrongly certifying optimality.
        /// </summary>
        public long UncertifiedNodeBudget { get; init; } = 250_000;

        /// <summary>True iff the most recent <see cref="FindBestColumn"/> explored the whole tree.</summary>
        public bool LastSearchExhaustive { get; private set; }

        /// <summary>DFS nodes visited by the most recent <see cref="FindBestColumn"/> (diagnostics).</summary>
        public long LastNodesExplored { get; private set; }

        /// <param name="duals">Demand-constraint duals π_i.</param>
        /// <param name="forbidden">Column signatures barred by branching; never returned, but still traversed as prefixes.</param>
        /// <param name="ct">Cancelled cooperatively from inside the search; on cancellation the search stops early and is reported non-exhaustive.</param>
        /// <param name="deadlineUtc">Absolute wall-clock cutoff. When reached the search stops early and is reported non-exhaustive, so a long pricing call cannot overrun the solver's time budget.</param>
        public BnpColumn? FindBestColumn(
            IReadOnlyDictionary<string, double> duals,
            IReadOnlySet<string>? forbidden = null,
            CancellationToken ct = default,
            DateTime? deadlineUtc = null)
        {
            ArgumentNullException.ThrowIfNull(duals);
            LastSearchExhaustive = true;
            LastNodesExplored = 0;
            if (_rules.AvailHeight <= 0) return null;

            var cands = BuildCandidates(duals);
            if (cands.Count == 0) return null;

            // Admissible value-per-resource ratios for the optimistic completion bound: every
            // candidate satisfies value ≤ valuePerHeight·height and value ≤ valuePerWeight·weight,
            // so the extra value reachable in the remaining height/weight is bounded by the smaller
            // of valuePerHeight·remainingHeight and valuePerWeight·remainingWeight (a fractional-
            // knapsack bound on two resources). A zero-weight layer makes the weight side
            // non-binding (∞).
            double valuePerHeight = 0, valuePerWeight = 0;
            foreach (var c in cands)
            {
                valuePerHeight = Math.Max(valuePerHeight, c.Value / c.Height);
                valuePerWeight = Math.Max(valuePerWeight, c.Weight > 0 ? c.Value / c.Weight : double.PositiveInfinity);
            }

            // The transposition table is only sound with no forbidden columns (see SearchContext);
            // when it is unavailable, cap the (then exponential) DFS so one call can't eat the budget.
            bool memoEligible = forbidden == null || forbidden.Count == 0;
            long budget = memoEligible ? NodeBudget : Math.Min(NodeBudget, UncertifiedNodeBudget);

            var ctx = new SearchContext(cands, _rules, valuePerHeight, valuePerWeight, budget, forbidden, ct, deadlineUtc);
            for (int i = 0; i < cands.Count; i++)
            {
                ctx.Descend(i);
                if (ctx.BudgetExhausted) break;
            }

            LastSearchExhaustive = !ctx.BudgetExhausted;
            LastNodesExplored = Math.Max(0, budget - ctx.RemainingBudget);

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
                cands.Add(new Cand(layer, value, layer.Metadata.Height, layer.Metrics.TotalWeight, layer.Metrics.LoadDensity));
            }
            // Non-increasing load density makes density tiers contiguous so the equal-density
            // tie-break (by index) canonicalises permutations; within a tier, higher value
            // first so strong stacks (and tighter pruning bounds) are reached earlier.
            cands.Sort(static (a, b) =>
            {
                int byDensity = b.Density.CompareTo(a.Density);
                return byDensity != 0 ? byDensity : b.Value.CompareTo(a.Value);
            });
            return cands;
        }

        private readonly record struct Cand(Layer Layer, double Value, int Height, double Weight, double Density);

        private sealed class SearchContext
        {
            // How often (in nodes) the wall-clock / cancellation cutoff is polled.
            private const int CheckStride = 8192;
            // Hard cap on transposition-table entries, bounding its memory (~50 MB at this size).
            private const int MemoCap = 1_000_000;

            private readonly List<Cand> _cands;
            private readonly PricingRules _rules;
            private readonly double _valuePerHeight;
            private readonly double _valuePerWeight;
            private readonly double _tolerance;
            private readonly int _availH;
            private readonly IReadOnlySet<string>? _forbidden;
            private long _budget;

            private readonly CancellationToken _ct;
            private readonly bool _hasDeadline;
            private readonly DateTime _deadline;
            private int _checkCountdown = CheckStride;

            // Transposition table: best accumulated value seen at each equivalent partial-stack
            // state. Two stacks are equivalent for future exploration when they share the same top
            // layer, used height, used weight, and set of present SKUs — they then admit exactly the
            // same extensions with the same values, so a state revisited at no-greater value can be
            // pruned. Disabled when a forbidden set is in play (forbidden-ness depends on the exact
            // column signature, which two state-equivalent stacks can differ on) to stay sound.
            private readonly Dictionary<StateKey, double>? _memo;
            private readonly Dictionary<string, long> _skuBit = new(StringComparer.Ordinal);
            private long _skuMask;

            private readonly List<int> _stack = [];                                 // candidate indices, bottom→top
            private readonly Dictionary<string, int> _skuRefs = new(StringComparer.Ordinal);
            private int _distinct;
            private double _value;
            private int _usedHeight;
            private double _usedWeight;

            public double BestValue { get; private set; }
            public List<int> BestStack { get; } = [];
            public bool BudgetExhausted { get; private set; }

            /// <summary>Node budget left after the search (diagnostics; may be slightly negative on the aborting node).</summary>
            public long RemainingBudget => _budget;

            public SearchContext(List<Cand> cands, PricingRules rules, double valuePerHeight, double valuePerWeight, long budget, IReadOnlySet<string>? forbidden, CancellationToken ct, DateTime? deadlineUtc)
            {
                _cands = cands;
                _rules = rules;
                _valuePerHeight = valuePerHeight;
                _valuePerWeight = valuePerWeight;
                _tolerance = rules.LoadDensityTolerance;
                _budget = budget;
                _availH = rules.AvailHeight;
                _forbidden = forbidden;
                _ct = ct;
                _hasDeadline = deadlineUtc.HasValue;
                _deadline = deadlineUtc ?? default;

                // The transposition table keys on a SKU-presence bitmask; build the SKU→bit map and
                // enable memoization only when there is no forbidden set and the layers use ≤ 63
                // distinct SKUs (so the mask fits a long).
                if (forbidden == null || forbidden.Count == 0)
                {
                    int bit = 0;
                    bool fits = true;
                    foreach (var c in cands)
                    {
                        foreach (var sku in c.Layer.Metrics.UsedSkuTypes)
                        {
                            if (_skuBit.ContainsKey(sku)) continue;
                            if (bit >= 63) { fits = false; break; }
                            _skuBit[sku] = 1L << bit++;
                        }
                        if (!fits) break;
                    }
                    if (fits) _memo = new Dictionary<StateKey, double>();
                }
            }

            public void Descend(int i)
            {
                if (BudgetExhausted) return;
                var c = _cands[i];

                if (_stack.Count > 0)
                {
                    var top = _cands[_stack[^1]];
                    if (!StackingLoadRule.Allows(top.Density, c.Density, _tolerance)) return;  // load ordering
                    if (c.Density == top.Density && i < _stack[^1]) return;                    // canonical tie-break
                    if (!_rules.TransitionValid(top.Layer, c.Layer)) return;
                }
                if (_usedHeight + c.Height > _availH) return;
                if (_usedWeight + c.Weight > _rules.MaxWeight) return;
                if (DistinctAfter(c.Layer) > PricingRules.MaxDistinctSkusPerTemplate) return;

                if (_budget-- <= 0) { BudgetExhausted = true; return; }

                if (--_checkCountdown <= 0)
                {
                    _checkCountdown = CheckStride;
                    if (_ct.IsCancellationRequested || (_hasDeadline && DateTime.UtcNow >= _deadline))
                    {
                        BudgetExhausted = true;
                        return;
                    }
                }

                Push(i, c);

                if (_value > BestValue && !IsForbidden())
                {
                    BestValue = _value;
                    BestStack.Clear();
                    BestStack.AddRange(_stack);
                }

                // Transposition-table prune: if this same state was already explored at an equal or
                // greater accumulated value, everything reachable from here was already found; skip
                // it. Otherwise record this (better) value and explore. i is the current top.
                if (_memo != null)
                {
                    var key = new StateKey(i, _usedHeight, (long)Math.Round(_usedWeight * 1000.0), _skuMask);
                    if (_memo.TryGetValue(key, out double seen))
                    {
                        if (seen >= _value - Epsilon) { Pop(c); return; }
                        _memo[key] = _value;
                    }
                    else if (_memo.Count < MemoCap)
                    {
                        _memo[key] = _value;
                    }
                }

                // Optimistic bound: fill the remaining height and weight at the best value
                // densities, taking the binding (smaller) of the two resource limits.
                double bound = _value + Math.Min(
                    _valuePerHeight * (_availH - _usedHeight),
                    _valuePerWeight * (_rules.MaxWeight - _usedWeight));
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
                    if (n == 0) { _distinct++; if (_memo != null) _skuMask |= _skuBit[sku]; }
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
                    if (n == 0) { _distinct--; if (_memo != null) _skuMask &= ~_skuBit[sku]; }
                }
            }

            // Identifies partial stacks that admit the same future extensions at the same values:
            // same top layer (support + density + canonical tie-break), remaining height and weight,
            // and set of present SKUs (distinct-SKU cap). Weight is quantised to milligrams so that
            // floating-point summation order does not split otherwise-identical states.
            private readonly record struct StateKey(int Top, int UsedHeight, long UsedWeightMg, long SkuMask);
        }
    }
}
