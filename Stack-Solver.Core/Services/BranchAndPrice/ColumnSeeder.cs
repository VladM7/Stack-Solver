using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Builds one feasible homogeneous column per placeable SKU: its best homogeneous
    /// layer repeated as many times as height and weight allow. Because each such column
    /// touches only its own demand constraint, the seed guarantees an initially feasible
    /// RMP (every demand row is coverable by its own variable) without artificial columns,
    /// and yields an integer baseline incumbent.
    ///
    /// A SKU with no valid homogeneous layer that fits the pallet is reported as
    /// unplaceable (it cannot be palletized under the current constraints).
    /// </summary>
    public static class ColumnSeeder
    {
        public static SeedResult Seed(
            IReadOnlyList<Layer> layers,
            IReadOnlyDictionary<string, int> demand,
            Pallet pallet)
        {
            ArgumentNullException.ThrowIfNull(layers);
            ArgumentNullException.ThrowIfNull(demand);
            ArgumentNullException.ThrowIfNull(pallet);

            var columns = new List<BnpColumn>();
            var placeable = new List<string>();
            var unplaceable = new List<string>();

            foreach (var sku in demand.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var best = BestHomogeneousLayer(layers, sku, pallet);
                int repeats = best != null ? MaxRepeats(pallet, best) : 0;
                if (best == null || repeats < 1)
                {
                    unplaceable.Add(sku);
                    continue;
                }

                var stack = new List<Layer>(repeats);
                for (int i = 0; i < repeats; i++) stack.Add(best);

                columns.Add(new BnpColumn(PalletTemplate.FromLayers(stack)));
                placeable.Add(sku);
            }

            return new SeedResult(columns, placeable, unplaceable);
        }

        /// <summary>Homogeneous layer of <paramref name="sku"/> with the most boxes that still fits the pallet.</summary>
        private static Layer? BestHomogeneousLayer(IReadOnlyList<Layer> layers, string sku, Pallet pallet)
        {
            Layer? best = null;
            int bestBoxes = 0;
            int availHeight = pallet.MaxStackHeight - pallet.Height;

            foreach (var layer in layers)
            {
                if (layer.Metrics.UsedSkuTypes.Count != 1) continue;
                if (!layer.Metrics.UsedSkuTypes.Contains(sku)) continue;
                if (layer.Metadata.Height <= 0 || layer.Metadata.Height > availHeight) continue;
                if (layer.Metrics.TotalWeight > pallet.MaxStackWeight) continue;

                int boxes = layer.Items.Count;
                if (boxes > bestBoxes)
                {
                    bestBoxes = boxes;
                    best = layer;
                }
            }
            return best;
        }

        private static int MaxRepeats(Pallet pallet, Layer layer)
        {
            if (layer.Metadata.Height <= 0) return 0;
            int availHeight = pallet.MaxStackHeight - pallet.Height;
            if (availHeight <= 0) return 0;

            int byHeight = availHeight / layer.Metadata.Height;
            int byWeight = layer.Metrics.TotalWeight > 0
                ? (int)Math.Floor(pallet.MaxStackWeight / layer.Metrics.TotalWeight)
                : int.MaxValue;
            return Math.Min(byHeight, byWeight);
        }
    }

    /// <param name="Columns">One homogeneous column per placeable SKU.</param>
    /// <param name="PlaceableSkus">SKUs that received a seed column.</param>
    /// <param name="UnplaceableSkus">SKUs with no valid homogeneous layer on this pallet.</param>
    public sealed record SeedResult(
        IReadOnlyList<BnpColumn> Columns,
        IReadOnlyList<string> PlaceableSkus,
        IReadOnlyList<string> UnplaceableSkus);
}
