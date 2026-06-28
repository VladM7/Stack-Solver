using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Heuristic pricing subproblem. Given the demand-constraint duals π_i, searches for
    /// valid pallet templates whose dual value Σ_i a_{t,i}·π_i exceeds 1 — i.e. negative
    /// reduced cost (1 − Σ a·π) in the min-pallets master. A beam search stacks layers
    /// bottom-to-top, respecting available height, weight, weight ordering (a layer may
    /// only rest on one at least as heavy), inter-layer support/overhang, and the
    /// distinct-SKU cap. Every partial stack whose value exceeds 1 is a candidate column.
    ///
    /// This finds improving columns quickly but does not prove their absence; proving LP
    /// optimality is the job of the exact pricer in a later milestone.
    /// </summary>
    public sealed class PricingSolver
    {
        private const int MaxDistinctSkusPerTemplate = 3;
        private const int MaxLayersPerTemplate = 6;
        private const double ReducedCostEpsilon = 1e-6;

        private readonly IReadOnlyList<Layer> _layers;
        private readonly Pallet _pallet;
        private readonly int _availHeight;
        private readonly Dictionary<(string, string), bool> _transitionCache = new();

        public PricingSolver(IReadOnlyList<Layer> layers, Pallet pallet)
        {
            _layers = layers ?? throw new ArgumentNullException(nameof(layers));
            _pallet = pallet ?? throw new ArgumentNullException(nameof(pallet));
            _availHeight = pallet.MaxStackHeight - pallet.Height;
        }

        /// <summary>Number of partial stacks retained at each beam-search depth.</summary>
        public int BeamWidth { get; init; } = 64;

        /// <summary>Maximum number of improving columns returned per call.</summary>
        public int MaxColumns { get; init; } = 8;

        /// <summary>
        /// Returns up to <see cref="MaxColumns"/> distinct improving columns (dual value &gt; 1),
        /// highest value first. Empty when none is found.
        /// </summary>
        public IReadOnlyList<BnpColumn> FindColumns(IReadOnlyDictionary<string, double> duals)
        {
            ArgumentNullException.ThrowIfNull(duals);
            if (_availHeight <= 0) return [];

            var scored = ScoreLayers(duals);
            if (scored.Count == 0) return [];

            var best = new Dictionary<string, (BnpColumn Column, double Value)>(StringComparer.Ordinal);

            var beam = new List<StackState>(scored.Count);
            foreach (var s in scored)
                beam.Add(StackState.Start(s));

            for (int depth = 0; depth < MaxLayersPerTemplate && beam.Count > 0; depth++)
            {
                foreach (var st in beam)
                    if (st.Value > 1.0 + ReducedCostEpsilon)
                        Record(best, st);

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
                if (layer.Metadata.Height <= 0 || layer.Metadata.Height > _availHeight) continue;
                if (layer.Metrics.TotalWeight > _pallet.MaxStackWeight) continue;
                if (!AllSkusModeled(layer, duals)) continue;

                double value = LayerValue(layer, duals);
                if (value <= 0) continue;
                scored.Add(new ScoredLayer(layer, value));
            }
            return scored;
        }

        private bool CanExtend(StackState st, ScoredLayer cand)
        {
            if (st.UsedHeight + cand.Layer.Metadata.Height > _availHeight) return false;
            if (st.UsedWeight + cand.Layer.Metrics.TotalWeight > _pallet.MaxStackWeight) return false;
            if (cand.Layer.Metrics.TotalWeight > st.TopWeight) return false;        // weight ordering
            if (CountDistinct(st.Skus, cand.Layer) > MaxDistinctSkusPerTemplate) return false;
            return IsTransitionValid(st.TopLayer, cand.Layer);
        }

        private void Record(Dictionary<string, (BnpColumn, double)> best, StackState st)
        {
            var column = new BnpColumn(PalletTemplate.FromLayers(st.Layers));
            if (!best.TryGetValue(column.Signature, out var existing) || st.Value > existing.Item2)
                best[column.Signature] = (column, st.Value);
        }

        private bool IsTransitionValid(Layer lower, Layer upper)
        {
            var key = (lower.Id, upper.Id);
            if (_transitionCache.TryGetValue(key, out bool cached)) return cached;

            var support = LayerSupportAnalyzer.Analyze(lower, upper, _pallet);
            bool ok = support.MaximumSkuOverhangArea <= _pallet.MaxSkuOverhang;
            _transitionCache[key] = ok;
            return ok;
        }

        private static bool AllSkusModeled(Layer layer, IReadOnlyDictionary<string, double> duals)
        {
            foreach (var sku in layer.Metrics.UsedSkuTypes)
                if (!duals.ContainsKey(sku)) return false;
            return true;
        }

        private static double LayerValue(Layer layer, IReadOnlyDictionary<string, double> duals)
        {
            double value = 0;
            foreach (var item in layer.Items)
                if (duals.TryGetValue(item.SkuType.SkuId, out double pi))
                    value += pi;
            return value;
        }

        private static int CountDistinct(HashSet<string> skus, Layer layer)
        {
            int extra = 0;
            foreach (var sku in layer.Metrics.UsedSkuTypes)
                if (!skus.Contains(sku)) extra++;
            return skus.Count + extra;
        }

        private readonly record struct ScoredLayer(Layer Layer, double Value);

        private sealed class StackState
        {
            public required List<Layer> Layers { get; init; }
            public required Layer TopLayer { get; init; }
            public required int UsedHeight { get; init; }
            public required double UsedWeight { get; init; }
            public required double TopWeight { get; init; }
            public required HashSet<string> Skus { get; init; }
            public required double Value { get; init; }

            public static StackState Start(ScoredLayer s) => new()
            {
                Layers = [s.Layer],
                TopLayer = s.Layer,
                UsedHeight = s.Layer.Metadata.Height,
                UsedWeight = s.Layer.Metrics.TotalWeight,
                TopWeight = s.Layer.Metrics.TotalWeight,
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
                    TopWeight = s.Layer.Metrics.TotalWeight,
                    Skus = skus,
                    Value = Value + s.Value,
                };
            }
        }
    }
}
