using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Stacking
{
    /// <summary>
    /// Turns an ordered list of (shared, centered) layers into a concrete stack whose layers are
    /// positioned so each rests on the one below. Layers are reused by reference across many templates,
    /// so a placement that shifts a layer for support must not mutate the shared instance: instead the
    /// shifted layer is returned as a translated clone. Layers that need no shift are returned as-is.
    /// </summary>
    public static class StackMaterializer
    {
        /// <summary>
        /// Positions <paramref name="ordered"/> bottom-up. The first layer sits centered on the pallet;
        /// every subsequent layer is translated by <see cref="LayerSupportAnalyzer.FindBestPlacement"/>
        /// onto the layer actually placed beneath it.
        /// </summary>
        public static List<Layer> Materialize(
            SupportSurface surface,
            IReadOnlyList<Layer> ordered,
            double maxOverhang,
            int gridStep = 1)
        {
            ArgumentNullException.ThrowIfNull(surface);
            ArgumentNullException.ThrowIfNull(ordered);

            var placed = new List<Layer>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                var layer = ordered[i];
                if (placed.Count == 0)
                {
                    placed.Add(layer);
                    continue;
                }

                var fit = LayerSupportAnalyzer.FindBestPlacement(placed[^1], layer, surface, maxOverhang, gridStep);
                placed.Add(fit.OffsetX == 0 && fit.OffsetY == 0
                    ? layer
                    : Translate(layer, fit.OffsetX, fit.OffsetY, surface, gridStep));
            }
            return placed;
        }

        /// <summary>
        /// Creates a clone of <paramref name="source"/> with every item shifted by (<paramref name="dx"/>,
        /// <paramref name="dy"/>). The clone keeps the source Id (so Id-keyed caches and compatibility
        /// lookups still resolve) and rebuilds its geometry for the new positions.
        /// </summary>
        public static Layer Translate(Layer source, int dx, int dy, SupportSurface surface, int gridStep = 1)
        {
            var items = new List<PositionedItem>(source.Items.Count);
            foreach (var it in source.Items)
                items.Add(new PositionedItem(it.SkuType, it.X + dx, it.Y + dy, it.Rotated));

            var clone = new Layer(source.Name, items, source.Metadata)
            {
                Id = source.Id,
                Metrics = source.Metrics
            };
            clone.Geometry = LayerGeometryBuilder.Build(clone, surface, gridStep);
            return clone;
        }
    }
}
