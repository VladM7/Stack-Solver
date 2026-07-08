using Google.OrTools.LinearSolver;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Level-1 purity mechanism: <b>layer re-packing</b>. Takes a settled, zero-leftover assignment
    /// and regroups its <i>exact</i> layer multiset into pallets so as to minimize impurity (see
    /// <see cref="PurityMetric"/>). Because every layer is placed exactly once, the per-SKU box totals
    /// — and therefore demand coverage — are preserved by construction; only the assignment of layers
    /// to pallets (and, under a budget, the number of pallets) changes.
    ///
    /// <para>Why this is more general than whole-pallet re-selection: the movable unit is the
    /// <em>layer</em>, not a pre-built pallet. A small tail layer stranded on a mixed pallet can be
    /// lifted onto an under-full same-SKU pallet even when no single pool column expresses either
    /// resulting pallet — the re-selection pass could never reach that solution.</para>
    ///
    /// <para><b>Objective.</b> Impurity splits into a per-layer term Σ_layers(distinctInLayer−1) and
    /// a per-pallet term Σ_pallets(distinctOnPallet−1). Regrouping a fixed layer multiset leaves the
    /// per-layer term invariant (it is a property of the layers, not their grouping), so minimizing
    /// total impurity is exactly minimizing the per-pallet term — a clean linear assignment
    /// objective. Note this means adding a pallet can only reduce the per-pallet term (by separating
    /// pure layers of distinct SKUs onto their own pallets); it can never touch the per-layer term.</para>
    ///
    /// <para><b>Solution A (count-preserving).</b> With <c>extraPalletBudget = 0</c> the layers are
    /// regrouped into exactly the same number of pallets — never adding a pallet nor introducing
    /// leftover, so the pallet-count optimality proof is preserved.</para>
    ///
    /// <para><b>Solution B (purity-first).</b> With <c>extraPalletBudget &gt; 0</c> up to that many
    /// extra pallet slots are allowed. A small ε tie-break on used slots makes the objective strictly
    /// lexicographic — minimize impurity first, then pallet count — so an extra pallet is spent only
    /// when it strictly reduces impurity, and never gratuitously (a solution reachable without the
    /// extra pallet is always preferred). The caller keeps the count-optimal solution and offers this
    /// as a separate, purer alternative only when it is strictly purer.</para>
    ///
    /// <para><b>Model.</b> The layer→pallet assignment IP and per-pallet stackability validation live
    /// in <see cref="PackLayers"/>, which is shared with the Level-2 reformation mechanism (see
    /// <see cref="LayerReformationSolver"/>): both hand it a layer multiset and a pallet-slot budget;
    /// only the layers differ (Level 1 keeps the settled layers, Level 2 substitutes freshly-formed
    /// pure ones). An assignment IP places each layer on one of the pallet slots subject to the
    /// additive per-pallet limits (stack height, stack weight, the 3-distinct-SKU cap) and minimizes
    /// Σ_slots(distinctSkus − slotUsed). The additive limits are necessary but not sufficient for a
    /// pallet to be physically stackable (inter-layer support and the top-heavy load-density order
    /// depend on layer <i>ordering</i>, which an assignment model cannot see), so each proposed pallet
    /// is validated by searching for a valid bottom-up stacking order; an unstackable group is excluded
    /// by a no-good cut and the IP re-solved (a bounded branch-and-check loop). The incumbent grouping
    /// is always feasible for the model, so the returned regrouping never has higher impurity than the
    /// input — the caller adopts it only when it is strictly lower.</para>
    /// </summary>
    public static class LayerRepackSolver
    {
        // Keep the polish cheap: skip instances whose assignment matrix (layers × slots) is large,
        // cap the per-pallet stacking-order search, and bound the no-good cut loop.
        internal const long MaxAssignmentCells = 6000;
        internal const int MaxStackOrderLayers = 14;
        internal const int MaxCutIterations = 64;

        /// <summary>
        /// Regroups <paramref name="current"/>'s layers to minimize the per-pallet impurity term
        /// (each physical pallet as its own <c>(column, 1)</c> entry). With
        /// <paramref name="extraPalletBudget"/> = 0 the pallet count is preserved exactly (Solution A);
        /// with a positive budget up to that many extra pallets may be used, but only when they
        /// strictly reduce impurity (Solution B). Returns null when no MIP backend is available, the
        /// instance is too large, the time budget is exhausted, or no fully-stackable regrouping is
        /// found. The result never regresses impurity, but may equal the input — the caller compares
        /// and adopts only a strict improvement.
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

            // Flatten to the layer multiset (K identical pallets contribute K copies of each layer)
            // and count the incumbent pallet count.
            var layers = new List<Layer>();
            int k = 0;
            foreach (var (col, count) in current)
            {
                if (count <= 0) continue;
                k += count;
                for (int c = 0; c < count; c++)
                    layers.AddRange(col.Template.Layers);
            }

            if (k <= 1 || layers.Count <= 1) return null;         // a single pallet cannot be regrouped

            // Available pallet slots: the incumbent count plus the purity-first budget. Extra slots may
            // be left empty; the ε tie-break inside PackLayers prefers the fewest that achieve the best
            // impurity.
            int extra = Math.Max(0, extraPalletBudget);
            int slots = k + extra;
            return PackLayers(layers, pallet, slots, preferFewerPallets: extra > 0, timeLimit, ct);
        }

        /// <summary>
        /// Shared layer→pallet packing core: assigns the <paramref name="layers"/> multiset onto up to
        /// <paramref name="slots"/> pallet slots minimizing the per-pallet impurity term, validates each
        /// proposed pallet for physical stackability, and returns one <c>(column, 1)</c> entry per
        /// non-empty pallet. When <paramref name="preferFewerPallets"/> is true a tiny ε tie-break makes
        /// the objective lexicographic (impurity first, then pallet count) so surplus slots are used only
        /// when they strictly cut impurity. Returns null when no MIP backend is available, the instance
        /// exceeds the size caps, the time budget is exhausted, or no fully-stackable grouping is found.
        /// Used by both Level 1 (<see cref="Solve"/>) and Level 2 (<see cref="LayerReformationSolver"/>).
        /// </summary>
        internal static IReadOnlyList<(BnpColumn Column, int Count)>? PackLayers(
            IReadOnlyList<Layer> layers,
            Pallet pallet,
            int slots,
            bool preferFewerPallets,
            TimeSpan timeLimit,
            CancellationToken ct)
        {
            int layerCount = layers.Count;
            if (layerCount <= 1 || slots < 1) return null;
            if ((long)layerCount * slots > MaxAssignmentCells) return null;

            var rules = new PricingRules(pallet);
            int availHeight = rules.AvailHeight;
            double maxWeight = rules.MaxWeight;

            // Distinct SKUs per layer and the global SKU index.
            var skuIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            var layerSkus = new List<int[]>(layerCount);
            foreach (var layer in layers)
            {
                var ids = new List<int>(layer.Metrics.UsedSkuTypes.Count);
                foreach (var sku in layer.Metrics.UsedSkuTypes)
                {
                    if (!skuIndex.TryGetValue(sku, out int idx))
                        skuIndex[sku] = idx = skuIndex.Count;
                    ids.Add(idx);
                }
                layerSkus.Add(ids.ToArray());
            }
            int skuCount = skuIndex.Count;

            using var solver = Solver.CreateSolver("SCIP") ?? Solver.CreateSolver("CBC");
            if (solver is null) return null;

            var x = new Variable[layerCount, slots];   // layer ℓ on slot s
            var y = new Variable[skuCount, slots];       // SKU i present on slot s
            var used = new Variable[slots];              // slot s carries ≥ 1 layer

            // Impurity objective is Σ_slots(distinctSkus − slotUsed). When surplus slots are offered we
            // add a tiny ε reward for leaving a slot unused (equivalently, a penalty for using it) so
            // that, among equal-impurity regroupings, the one with the fewest pallets wins. ε·slots < 1
            // keeps it strictly below any one-unit impurity gain, so it only ever breaks ties.
            double eps = preferFewerPallets ? 1.0 / (slots + 1) : 0.0;
            double usedCoeff = -(1.0 - eps);

            var objective = solver.Objective();
            objective.SetMinimization();

            for (int s = 0; s < slots; s++)
            {
                used[s] = solver.MakeIntVar(0, 1, $"u{s}");
                objective.SetCoefficient(used[s], usedCoeff);
                for (int i = 0; i < skuCount; i++)
                {
                    y[i, s] = solver.MakeIntVar(0, 1, $"y{i}_{s}");
                    objective.SetCoefficient(y[i, s], 1.0);
                }
            }

            for (int l = 0; l < layerCount; l++)
                for (int s = 0; s < slots; s++)
                    x[l, s] = solver.MakeIntVar(0, 1, $"x{l}_{s}");

            // Each layer is placed on exactly one slot.
            for (int l = 0; l < layerCount; l++)
            {
                var assign = solver.MakeConstraint(1, 1, $"assign{l}");
                for (int s = 0; s < slots; s++) assign.SetCoefficient(x[l, s], 1.0);
            }

            for (int s = 0; s < slots; s++)
            {
                // Stack height and weight are additive over the layers on a slot.
                var height = solver.MakeConstraint(double.NegativeInfinity, availHeight, $"h{s}");
                var weight = solver.MakeConstraint(double.NegativeInfinity, maxWeight, $"w{s}");
                for (int l = 0; l < layerCount; l++)
                {
                    height.SetCoefficient(x[l, s], layers[l].Metadata.Height);
                    weight.SetCoefficient(x[l, s], layers[l].Metrics.TotalWeight);
                }

                // Presence link y[i,s] ≥ x[l,s] for every layer l containing SKU i; the minimizing
                // objective drives y to exactly the SKUs present.
                for (int l = 0; l < layerCount; l++)
                    foreach (int i in layerSkus[l])
                    {
                        var link = solver.MakeConstraint(double.NegativeInfinity, 0, $"pres{l}_{i}_{s}");
                        link.SetCoefficient(x[l, s], 1.0);
                        link.SetCoefficient(y[i, s], -1.0);
                    }

                // Distinct-SKU cap per pallet.
                var cap = solver.MakeConstraint(double.NegativeInfinity, PricingRules.MaxDistinctSkusPerTemplate, $"skucap{s}");
                for (int i = 0; i < skuCount; i++) cap.SetCoefficient(y[i, s], 1.0);

                // used[s] ≤ Σ_l x[l,s] — a slot only counts as used when it carries a layer.
                var usedLink = solver.MakeConstraint(double.NegativeInfinity, 0, $"used{s}");
                usedLink.SetCoefficient(used[s], 1.0);
                for (int l = 0; l < layerCount; l++) usedLink.SetCoefficient(x[l, s], -1.0);
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();

            for (int iteration = 0; iteration < MaxCutIterations; iteration++)
            {
                ct.ThrowIfCancellationRequested();

                var remaining = timeLimit - watch.Elapsed;
                if (remaining <= TimeSpan.Zero) return null;
                solver.SetTimeLimit((long)Math.Max(1, remaining.TotalMilliseconds));

                var status = solver.Solve();
                if (status is not (Solver.ResultStatus.OPTIMAL or Solver.ResultStatus.FEASIBLE))
                    return null;

                // Gather the layers assigned to each slot.
                var slotLayers = new List<int>[slots];
                for (int s = 0; s < slots; s++) slotLayers[s] = new List<int>();
                for (int l = 0; l < layerCount; l++)
                    for (int s = 0; s < slots; s++)
                        if (x[l, s].SolutionValue() > 0.5) { slotLayers[s].Add(l); break; }

                // Validate each non-empty slot is physically stackable; collect the ordered layers, or
                // a no-good cut for any group that cannot be stacked in any order.
                var pallets = new List<List<Layer>>(slots);
                var cuts = new List<List<int>>();
                foreach (var group in slotLayers)
                {
                    if (group.Count == 0) continue;
                    if (group.Count > MaxStackOrderLayers) return null; // beyond the validation budget

                    var ordered = TryFindStackingOrder(group, layers, rules, pallet.LoadDensityTolerance);
                    if (ordered != null) pallets.Add(ordered);
                    else cuts.Add(group);
                }

                if (cuts.Count == 0)
                {
                    // Every pallet is stackable. Aggregate identical pallets (same SKU-count signature)
                    // into one column with a count — matching how the main solution's columns are
                    // grouped — so the UI shows "pallet type ×N" rather than N separate identical types.
                    var order = new List<string>();
                    var grouped = new Dictionary<string, (BnpColumn Column, int Count)>(StringComparer.Ordinal);
                    foreach (var ordered in pallets)
                    {
                        var column = new BnpColumn(PalletTemplate.FromLayers(ordered));
                        if (grouped.TryGetValue(column.Signature, out var existing))
                            grouped[column.Signature] = (existing.Column, existing.Count + 1);
                        else
                        {
                            grouped[column.Signature] = (column, 1);
                            order.Add(column.Signature);
                        }
                    }
                    return order.Select(sig => grouped[sig]).ToList();
                }

                // Forbid each unstackable group from co-occupying any slot, then re-solve.
                foreach (var group in cuts)
                {
                    for (int s = 0; s < slots; s++)
                    {
                        var cut = solver.MakeConstraint(double.NegativeInfinity, group.Count - 1, "nogood");
                        foreach (int l in group) cut.SetCoefficient(x[l, s], 1.0);
                    }
                }
            }

            return null; // cut loop exhausted without a fully-stackable regrouping
        }

        /// <summary>
        /// Searches for a bottom-up order of <paramref name="group"/> (layer indices into
        /// <paramref name="layers"/>) in which every layer may rest on the one below it: its load
        /// density does not exceed the lower layer's beyond <paramref name="tolerance"/>
        /// (<see cref="StackingLoadRule"/>) and it is supported within the overhang limit
        /// (<see cref="PricingRules.TransitionValid"/>). Returns the ordered layers, or null when no
        /// order is feasible.
        /// </summary>
        private static List<Layer>? TryFindStackingOrder(
            List<int> group, IReadOnlyList<Layer> layers, PricingRules rules, double tolerance)
        {
            int n = group.Count;
            var order = new int[n];
            var placed = new bool[n];

            bool Place(int depth, int belowLocal)
            {
                if (depth == n) return true;

                // Prune symmetric branches: identical layers (same Id) are interchangeable at a level.
                var tried = new HashSet<string>(StringComparer.Ordinal);
                for (int j = 0; j < n; j++)
                {
                    if (placed[j]) continue;
                    var upper = layers[group[j]];
                    if (!tried.Add(upper.Id)) continue;

                    if (belowLocal >= 0)
                    {
                        var lower = layers[group[belowLocal]];
                        if (!StackingLoadRule.Allows(lower.Metrics.LoadDensity, upper.Metrics.LoadDensity, tolerance))
                            continue;
                        if (!rules.TransitionValid(lower, upper))
                            continue;
                    }

                    placed[j] = true;
                    order[depth] = j;
                    if (Place(depth + 1, j)) return true;
                    placed[j] = false;
                }
                return false;
            }

            if (!Place(0, -1)) return null;

            var result = new List<Layer>(n);
            foreach (int local in order) result.Add(layers[group[local]]);
            return result;
        }
    }
}
