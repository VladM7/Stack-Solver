using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.Layering;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Augments a layer set with small partial "filler" layers for each placeable SKU, so
    /// branch-and-price can place a sub-layer remainder on an extra (possibly sparse) pallet
    /// instead of reporting it as a leftover. Without these, the only homogeneous layers are
    /// full grids of <c>N</c> boxes, so a remainder smaller than <c>N</c> has no column to go
    /// into.
    ///
    /// Filler sizes are powers of two (1, 2, 4, …) below the grid capacity: any remainder
    /// <c>r &lt; N</c> is then the sum of a few distinct sizes, so it lands on one pallet whose
    /// height grows only logarithmically with the capacity. Big-M leftover penalties mean
    /// these are used solely to mop up the remainder; full pallets are still preferred.
    /// </summary>
    public static class FillerLayerGenerator
    {
        public static List<Layer> Augment(IReadOnlyList<Layer> layers, Pallet pallet)
        {
            ArgumentNullException.ThrowIfNull(layers);
            ArgumentNullException.ThrowIfNull(pallet);

            // Representative SKU and grid capacity (largest homogeneous box count) per SKU id,
            // plus the box counts already present so we never duplicate an existing size.
            var skuById = new Dictionary<string, SKU>(StringComparer.Ordinal);
            var capacityById = new Dictionary<string, int>(StringComparer.Ordinal);
            var existingCounts = new HashSet<(string Sku, int Count)>();

            foreach (var layer in layers)
            {
                if (layer.Metrics.UsedSkuTypes.Count != 1 || layer.Items.Count == 0) continue;
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
                int capacity = capacityById[id];
                for (int size = 1; size < capacity; size *= 2)
                {
                    if (!existingCounts.Add((id, size))) continue;   // already have a layer of this size

                    var clone = CloneWithQuantity(sku, size);
                    foreach (var filler in strategy.Generate([clone], pallet, options))
                    {
                        // Keep one layer per (SKU, box count); orientations beyond the first add
                        // no tiling power for a remainder.
                        if (filler.Items.Count == size)
                        {
                            result.Add(filler);
                            break;
                        }
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
