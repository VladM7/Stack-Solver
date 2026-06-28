using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.Layering;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Augments a layer set with the <em>minimal</em> partial layers needed for branch-and-price
    /// to tile every placeable demand exactly, so a sub-layer remainder lands inside the
    /// certified optimisation instead of an appended greedy pallet.
    ///
    /// <para>For SKU <c>i</c> with grid capacity <c>N_i</c> (the densest homogeneous layer)
    /// and demand <c>d_i = q_i·N_i + r_i</c>, the fewest layers that sum to exactly <c>d_i</c>
    /// is <c>q_i</c> full layers plus <b>one partial layer of exactly <c>r_i</c> boxes</b>
    /// (only when <c>r_i &gt; 0</c>). Every layer of a SKU has the same height regardless of
    /// box count, so minimising pallets means minimising layer count — hence a single
    /// <c>r_i</c> partial is the optimal residual, never a power-of-two ladder. The result is
    /// therefore: at most one extra layer per SKU, and <b>none on layer-divisible demand</b>
    /// (so divisible instances are untouched and incur no search overhead).</para>
    /// </summary>
    public static class ResidualLayerGenerator
    {
        /// <summary>
        /// Returns <paramref name="layers"/> plus one homogeneous partial layer of exactly
        /// <c>d_i mod N_i</c> boxes for each SKU whose demand is not a whole multiple of its
        /// grid capacity. Returns the original list (a copy) when nothing is needed.
        /// </summary>
        public static List<Layer> Augment(
            IReadOnlyList<Layer> layers,
            IReadOnlyDictionary<string, int> demand,
            Pallet pallet)
        {
            ArgumentNullException.ThrowIfNull(layers);
            ArgumentNullException.ThrowIfNull(demand);
            ArgumentNullException.ThrowIfNull(pallet);

            int availHeight = pallet.MaxStackHeight - pallet.Height;

            // Representative SKU and grid capacity (densest fitting homogeneous layer) per SKU,
            // plus the box counts already present so an existing size is never duplicated.
            var skuById = new Dictionary<string, SKU>(StringComparer.Ordinal);
            var capacityById = new Dictionary<string, int>(StringComparer.Ordinal);
            var existingCounts = new HashSet<(string Sku, int Count)>();

            foreach (var layer in layers)
            {
                if (layer.Metrics.UsedSkuTypes.Count != 1 || layer.Items.Count == 0) continue;
                if (layer.Metadata.Height <= 0 || layer.Metadata.Height > availHeight) continue;
                if (layer.Metrics.TotalWeight > pallet.MaxStackWeight) continue;

                var sku = layer.Items[0].SkuType;
                int boxes = layer.Items.Count;
                skuById[sku.SkuId] = sku;
                existingCounts.Add((sku.SkuId, boxes));
                if (!capacityById.TryGetValue(sku.SkuId, out int cap) || boxes > cap)
                    capacityById[sku.SkuId] = boxes;
            }

            if (skuById.Count == 0) return [.. layers];

            var result = new List<Layer>(layers);
            var strategy = new HomogeneousGenerationStrategy();
            var options = new GenerationOptions();

            foreach (var (id, sku) in skuById)
            {
                if (!demand.TryGetValue(id, out int d) || d <= 0) continue;

                int capacity = capacityById[id];
                int remainder = d % capacity;
                if (remainder == 0) continue;                       // demand tiles with full layers alone
                if (!existingCounts.Add((id, remainder))) continue; // a layer of this size already exists

                var clone = CloneWithQuantity(sku, remainder);
                foreach (var partial in strategy.Generate([clone], pallet, options))
                {
                    // One partial layer per SKU is enough: extra orientations add no tiling
                    // power for a fixed remainder count.
                    if (partial.Items.Count == remainder)
                    {
                        result.Add(partial);
                        break;
                    }
                }
            }

            return result;
        }

        private static SKU CloneWithQuantity(SKU sku, int quantity) =>
            new()
            {
                SkuId = sku.SkuId,
                Name = sku.Name,
                Length = sku.Length,
                Width = sku.Width,
                Height = sku.Height,
                Weight = sku.Weight,
                Rotatable = sku.Rotatable,
                Notes = sku.Notes,
                Quantity = quantity,
            };
    }
}
