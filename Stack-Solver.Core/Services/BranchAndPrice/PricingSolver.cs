using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Heuristic pricing subproblem. Given the demand-constraint duals π_i, searches for
    /// valid pallet templates whose dual value Σ_i a_{t,i}·π_i exceeds 1 — i.e. negative
    /// reduced cost (1 − Σ a·π) in the min-pallets master. A beam search stacks layers
    /// bottom-to-top under <see cref="PricingRules"/>: available height, weight, load-density
    /// ordering (a layer may rest on one no more than the tolerance denser), inter-layer support,
    /// and the distinct-SKU cap. Every partial stack whose value exceeds 1 is a candidate column.
    ///
    /// This finds improving columns quickly but does not prove their absence; proving LP
    /// optimality is the job of <see cref="ExactPricingSolver"/>.
    /// </summary>
    public sealed class PricingSolver
    {
        private const int MaxLayersPerTemplate = 6;
        private const double ReducedCostEpsilon = 1e-6;

        private readonly IReadOnlyList<Layer> _layers;
        private readonly PricingRules _rules;

        public PricingSolver(IReadOnlyList<Layer> layers, Pallet pallet)
        {
            _layers = layers ?? throw new ArgumentNullException(nameof(layers));
            ArgumentNullException.ThrowIfNull(pallet);
            _rules = new PricingRules(pallet);
        }

        /// <summary>Number of partial stacks retained at each beam-search depth.</summary>
        public int BeamWidth { get; init; } = 64;

        /// <summary>Maximum number of improving columns returned per call.</summary>
        public int MaxColumns { get; init; } = 8;

        /// <summary>
        /// Returns up to <see cref="MaxColumns"/> distinct improving columns (dual value &gt;
        /// <paramref name="reducedCostThreshold"/>), highest value first. Columns whose signature is
        /// in <paramref name="forbidden"/> (forbidden by a branching constraint) are never returned.
        /// Empty when none is found. The threshold defaults to 1 (the unit pallet cost); a
        /// cardinality cap raises it to 1 − μ.
        /// </summary>
        public IReadOnlyList<BnpColumn> FindColumns(
            IReadOnlyDictionary<string, double> duals,
            IReadOnlySet<string>? forbidden = null,
            double reducedCostThreshold = 1.0)
        {
            ArgumentNullException.ThrowIfNull(duals);
            if (_rules.AvailHeight <= 0) return [];

            var scored = ScoreLayers(duals);
            if (scored.Count == 0) return [];

            var best = new Dictionary<string, (BnpColumn Column, double Value)>(StringComparer.Ordinal);

            var beam = new List<StackState>(scored.Count);
            foreach (var s in scored)
                beam.Add(StackState.Start(s));

            for (int depth = 0; depth < MaxLayersPerTemplate && beam.Count > 0; depth++)
            {
                foreach (var st in beam)
                    if (st.Value > reducedCostThreshold + ReducedCostEpsilon)
                        Record(best, st, forbidden);

                if (depth == MaxLayersPerTemplate - 1) break;

                var next = new List<StackState>();
                foreach (var st in beam)
                    foreach (var cand in scored)
                        if (CanExtend(st, cand))
                            next.Add(st.Extend(cand));

                if (next.Count == 0) break;
                next.Sort(static (a, b) => b.Value.CompareTo(a.Value));
                if (next.Count > BeamWidth) next.RemoveRange(BeamWidth, next.Count - BeamWidth);
                beam = next;
            }

            return best.Values
                .OrderByDescending(e => e.Value)
                .Take(MaxColumns)
                .Select(e => e.Column)
                .ToList();
        }

        private List<ScoredLayer> ScoreLayers(IReadOnlyDictionary<string, double> duals)
        {
            var scored = new List<ScoredLayer>();
            foreach (var layer in _layers)
            {
                if (layer.Metadata.Height <= 0 || layer.Metadata.Height > _rules.AvailHeight) continue;
                if (layer.Metrics.TotalWeight > _rules.MaxWeight) continue;
                if (!PricingRules.AllSkusModeled(layer, duals)) continue;

                double value = PricingRules.LayerValue(layer, duals);
                if (value <= 0) continue;
                scored.Add(new ScoredLayer(layer, value, layer.Metrics.LoadDensity));
            }
            return scored;
        }

        private bool CanExtend(StackState st, ScoredLayer cand)
        {
            if (st.UsedHeight + cand.Layer.Metadata.Height > _rules.AvailHeight) return false;
            if (st.UsedWeight + cand.Layer.Metrics.TotalWeight > _rules.MaxWeight) return false;
            // Load-density ordering: the upper layer may be at most LoadDensityTolerance denser than the one below.
            if (!StackingLoadRule.Allows(st.TopDensity, cand.Density, _rules.LoadDensityTolerance)) return false;
            if (PricingRules.CountDistinct(st.Skus, cand.Layer) > PricingRules.MaxDistinctSkusPerTemplate) return false;
            return _rules.TransitionValid(st.TopLayer, cand.Layer);
        }

        private static void Record(Dictionary<string, (BnpColumn, double)> best, StackState st, IReadOnlySet<string>? forbidden)
        {
            var column = new BnpColumn(PalletTemplate.FromLayers(st.Layers));
            if (forbidden != null && forbidden.Contains(column.Signature)) return;
            if (!best.TryGetValue(column.Signature, out var existing) || st.Value > existing.Item2)
                best[column.Signature] = (column, st.Value);
        }

        private readonly record struct ScoredLayer(Layer Layer, double Value, double Density);

        private sealed class StackState
        {
            public required List<Layer> Layers { get; init; }
            public required Layer TopLayer { get; init; }
            public required int UsedHeight { get; init; }
            public required double UsedWeight { get; init; }
            public required double TopDensity { get; init; }
            public required HashSet<string> Skus { get; init; }
            public required double Value { get; init; }

            public static StackState Start(ScoredLayer s) => new()
            {
                Layers = [s.Layer],
                TopLayer = s.Layer,
                UsedHeight = s.Layer.Metadata.Height,
                UsedWeight = s.Layer.Metrics.TotalWeight,
                TopDensity = s.Density,
                Skus = new HashSet<string>(s.Layer.Metrics.UsedSkuTypes, StringComparer.Ordinal),
                Value = s.Value,
            };

            public StackState Extend(ScoredLayer s)
            {
                var layers = new List<Layer>(Layers) { s.Layer };
                var skus = new HashSet<string>(Skus, StringComparer.Ordinal);
                foreach (var sku in s.Layer.Metrics.UsedSkuTypes) skus.Add(sku);

                return new StackState
                {
                    Layers = layers,
                    TopLayer = s.Layer,
                    UsedHeight = UsedHeight + s.Layer.Metadata.Height,
                    UsedWeight = UsedWeight + s.Layer.Metrics.TotalWeight,
                    TopDensity = s.Density,
                    Skus = skus,
                    Value = Value + s.Value,
                };
            }
        }
    }
}
