using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Fast constructive incumbent for branch-and-price: tile each placeable demand into the
    /// fewest homogeneous layers (full layers plus the single residual partial) and first-fit
    /// them into pallets, full layers before partials so a partial never has to support a full
    /// layer above it. The result places every placeable box and gives the search a strong
    /// integer upper bound from the start, so a budget-limited solve never returns short and
    /// branch-and-bound can prune (and often certify via the ⌈LP bound⌉) immediately.
    /// </summary>
    internal static class LayerPackingHeuristic
    {
        /// <summary>
        /// Packs <paramref name="demand"/> into pallet columns, or null when no layer fits the
        /// pallet or a residual cannot be tiled (caller falls back to plain branch-and-bound).
        /// </summary>
        public static IReadOnlyList<(BnpColumn Column, int Count)>? Pack(
            IReadOnlyList<Layer> layers,
            IReadOnlyDictionary<string, int> demand,
            Pallet pallet)
        {
            var rules = new PricingRules(pallet);
            int availHeight = rules.AvailHeight;
            if (availHeight <= 0) return null;

            // Fitting homogeneous layers grouped by SKU.
            var bySku = new Dictionary<string, List<Layer>>(StringComparer.Ordinal);
            foreach (var layer in layers)
            {
                if (layer.Metrics.UsedSkuTypes.Count != 1 || layer.Items.Count == 0) continue;
                if (layer.Metadata.Height <= 0 || layer.Metadata.Height > availHeight) continue;
                if (layer.Metrics.TotalWeight > rules.MaxWeight) continue;

                string sku = layer.Items[0].SkuType.SkuId;
                if (!bySku.TryGetValue(sku, out var list)) bySku[sku] = list = [];
                list.Add(layer);
            }

            // Expand each demand into its minimal layer multiset: q full layers + one r-box partial.
            var needed = new List<Layer>();
            foreach (var (sku, list) in bySku)
            {
                if (!demand.TryGetValue(sku, out int d) || d <= 0) continue;

                Layer full = list[0];
                foreach (var l in list) if (l.Items.Count > full.Items.Count) full = l;
                int n = full.Items.Count;

                int q = d / n, r = d % n;
                for (int i = 0; i < q; i++) needed.Add(full);
                if (r > 0)
                {
                    Layer? partial = list.FirstOrDefault(l => l.Items.Count == r);
                    if (partial == null) return null;   // residual layer absent — cannot tile exactly
                    needed.Add(partial);
                }
            }

            if (needed.Count == 0) return null;

            // Full layers first so partials settle on top (full-on-partial would overhang).
            needed.Sort(static (a, b) => b.Items.Count.CompareTo(a.Items.Count));

            var bins = new List<Bin>();
            foreach (var layer in needed)
            {
                Bin? target = null;
                foreach (var bin in bins)
                {
                    if (bin.UsedHeight + layer.Metadata.Height > availHeight) continue;
                    if (bin.UsedWeight + layer.Metrics.TotalWeight > rules.MaxWeight) continue;
                    if (bin.Top != null && !rules.TransitionValid(bin.Top, layer)) continue;
                    target = bin;
                    break;
                }
                (target ??= AddBin(bins)).Add(layer);
            }

            // Group identical pallets into columns with multiplicities.
            var bySig = new Dictionary<string, (BnpColumn Column, int Count)>(StringComparer.Ordinal);
            foreach (var bin in bins)
            {
                var column = new BnpColumn(PalletTemplate.FromLayers(bin.Layers));
                bySig[column.Signature] = bySig.TryGetValue(column.Signature, out var ex)
                    ? (ex.Column, ex.Count + 1)
                    : (column, 1);
            }
            return [.. bySig.Values];
        }

        private static Bin AddBin(List<Bin> bins)
        {
            var bin = new Bin();
            bins.Add(bin);
            return bin;
        }

        private sealed class Bin
        {
            public List<Layer> Layers { get; } = [];
            public int UsedHeight { get; private set; }
            public double UsedWeight { get; private set; }
            public Layer? Top { get; private set; }

            public void Add(Layer layer)
            {
                Layers.Add(layer);
                UsedHeight += layer.Metadata.Height;
                UsedWeight += layer.Metrics.TotalWeight;
                Top = layer;
            }
        }
    }
}
