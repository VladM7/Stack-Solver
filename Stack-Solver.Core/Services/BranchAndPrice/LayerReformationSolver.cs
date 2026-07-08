using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Level-2 purity mechanism: <b>layer reformation</b>. Where Level 1
    /// (<see cref="LayerRepackSolver"/>) treats each settled layer as an atomic, indivisible unit and
    /// can only <i>move</i> whole layers between pallets, Level 2 breaks the layers open at the box
    /// level and rebuilds them. It targets the case Level 1 structurally cannot fix: a minority SKU
    /// riding inside <i>every</i> layer (e.g. one box of A packed with six of B in a repeated layer),
    /// which leaves the per-layer impurity term Σ_layers(distinctInLayer−1) permanently nonzero no
    /// matter how the layers are regrouped.
    ///
    /// <para><b>Mechanism.</b> Decompose the settled assignment to its per-SKU box totals, rebuild
    /// those boxes into <i>pure</i> homogeneous layers (each layer a single SKU — full layers plus one
    /// partial top layer per SKU), then hand the pure-layer multiset to the shared
    /// <see cref="LayerRepackSolver.PackLayers"/> core with a pallet-slot budget. The packer groups the
    /// pure layers onto pallets minimizing impurity and re-validates every pallet's physical
    /// stackability, exactly as for Level 1 — only the input layers differ. Because reformation rebuilds
    /// from the exact box totals with no phantom boxes, per-SKU demand coverage is preserved by
    /// construction.</para>
    ///
    /// <para><b>Budget and safety.</b> As with Level 1's Solution B, up to <c>extraPalletBudget</c>
    /// extra pallet slots are offered and an ε tie-break spends them only when they strictly cut
    /// impurity. If pure reformation cannot fit the offered slots (pure stacks too tall, footprint too
    /// wasteful) the packer returns null and nothing is offered — the mechanism self-limits. The caller
    /// keeps the count-optimal solution untouched and offers the reformed arrangement alongside only
    /// when it is strictly purer.</para>
    ///
    /// <para><b>Scope (v1).</b> This reforms <i>all</i> layers to pure. It does not perform
    /// <i>partial</i> reformation — extracting only a minority SKU while preserving an otherwise-good
    /// mixed layer — so a pair of SKUs that tessellate to fill the footprint perfectly is only reformed
    /// when going fully pure still fits the pallet budget. Selective/partial reformation is a natural
    /// follow-up.</para>
    /// </summary>
    public static class LayerReformationSolver
    {
        /// <summary>
        /// Reforms <paramref name="current"/>'s boxes into pure single-SKU layers and repacks them into
        /// pallets minimizing impurity, using up to <paramref name="extraPalletBudget"/> extra pallets
        /// beyond the incumbent count (Solution-B style, spent only when strictly purer). Returns null
        /// when the instance has a single pallet, a SKU cannot form a homogeneous layer, the instance is
        /// too large, the time budget is exhausted, or no fully-stackable grouping is found. The result
        /// preserves per-SKU box totals; the caller adopts it only when it is strictly purer than the
        /// incumbent.
        /// </summary>
        public static IReadOnlyList<(BnpColumn Column, int Count)>? Solve(
            IReadOnlyList<(BnpColumn Column, int Count)> current,
            Pallet pallet,
            TimeSpan timeLimit,
            CancellationToken ct,
            int extraPalletBudget = 0)
        {
            ArgumentNullException.ThrowIfNull(current);
            ArgumentNullException.ThrowIfNull(pallet);
            if (timeLimit <= TimeSpan.Zero) return null;

            // Decompose to per-SKU box totals, capturing a representative SKU per id (for its
            // dimensions) and the incumbent pallet count K.
            int k = 0;
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            var reps = new Dictionary<string, SKU>(StringComparer.Ordinal);
            foreach (var (col, count) in current)
            {
                if (count <= 0) continue;
                k += count;
                foreach (var layer in col.Template.Layers)
                    foreach (var item in layer.Items)
                    {
                        string id = item.SkuType.SkuId;
                        totals[id] = totals.GetValueOrDefault(id) + count;
                        reps.TryAdd(id, item.SkuType);
                    }
            }

            if (k <= 1 || totals.Count == 0) return null; // a single pallet cannot be repacked

            // Rebuild each SKU's boxes as pure homogeneous layers: as many full layers as fit (all the
            // same instance, so the packer's Id-based stacking-order symmetry pruning still fires) plus
            // one partial top layer for the remainder.
            var options = new GenerationOptions();
            var pureLayers = new List<Layer>();
            foreach (var (id, total) in totals)
            {
                if (total <= 0) continue;

                var full = DensestLayer(reps[id], pallet, options, ct);
                if (full is null || full.Items.Count == 0) return null; // SKU cannot form a layer here
                int cap = full.Items.Count;

                int fullCount = total / cap;
                int remainder = total % cap;
                for (int i = 0; i < fullCount; i++) pureLayers.Add(full);

                if (remainder > 0)
                {
                    // Derive the partial by truncating the dense full layer to exactly the remainder — a
                    // valid subset of a pure single-SKU layer, so box conservation is exact regardless of
                    // how each strategy handles a box-count cap. A distinct instance (new Id), so it is
                    // not collapsed with the full layers by the packer's symmetry pruning.
                    var partial = Truncate(full, remainder, pallet);
                    if (partial.Items.Count != remainder) return null; // box loss guard
                    pureLayers.Add(partial);
                }
            }

            if (pureLayers.Count <= 1) return null;

            int extra = Math.Max(0, extraPalletBudget);
            int slots = k + extra;
            return LayerRepackSolver.PackLayers(pureLayers, pallet, slots, preferFewerPallets: extra > 0, timeLimit, ct);
        }

        /// <summary>
        /// Builds the densest footprint-filling homogeneous layer for <paramref name="sku"/> on the
        /// pallet, using the full layer-generation ensemble (BLF, homogeneous, strip-fill, radial) rather
        /// than a single strategy — the homogeneous packer alone can be far from densest (e.g. it fits
        /// only 4 of a box the ensemble tessellates 6 of), which would inflate the reformed pallet count
        /// past the budget and suppress the alternative. Clones the SKU so the caller's demand model is
        /// never mutated.
        /// </summary>
        private static Layer? DensestLayer(SKU sku, Pallet pallet, GenerationOptions options, CancellationToken ct)
        {
            var clone = new SKU
            {
                SkuId = sku.SkuId,
                Name = sku.Name,
                Length = sku.Length,
                Width = sku.Width,
                Height = sku.Height,
                Weight = sku.Weight,
                Rotatable = sku.Rotatable,
                Quantity = 0, // fill the footprint
            };

            return LayerGenerator.Generate([clone], pallet, options, useCpsat: false, ct)
                .OrderByDescending(l => l.Items.Count)
                .FirstOrDefault();
        }

        /// <summary>
        /// A pure single-SKU layer holding exactly the first <paramref name="count"/> boxes of
        /// <paramref name="full"/>, with geometry and metrics recomputed for the reduced set. Any subset
        /// of a valid pure layer's non-overlapping boxes is itself a valid, lighter layer, so this yields
        /// an exact-count partial without depending on a strategy honouring a box-count cap.
        /// </summary>
        private static Layer Truncate(Layer full, int count, Pallet pallet)
        {
            var items = full.Items.Take(count).ToList();
            var partial = new Layer(
                "reformed-partial", items,
                new LayerMetadata(full.Metadata.Utilization, full.Metadata.Height, "reformed partial layer"));
            partial.Geometry = LayerGeometryBuilder.Build(partial, pallet);
            partial.Metrics = LayerMetricsCalculator.Compute(partial, pallet);
            return partial;
        }
    }
}
