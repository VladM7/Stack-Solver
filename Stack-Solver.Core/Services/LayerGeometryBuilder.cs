using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services
{
    /// <summary>
    /// Provides functionality to construct a LayerGeometry object for a specified layer and support surface,
    /// calculating grid dimensions and item placements based on the provided parameters.
    /// </summary>
    public static class LayerGeometryBuilder
    {
        /// <summary>
        /// Builds a LayerGeometry object that represents the spatial arrangement and occupancy grid of items in the
        /// specified layer on the given support surface.
        /// </summary>
        /// <param name="layer">The layer containing the items to be represented in the geometry. Cannot be null.</param>
        /// <param name="supportSurface">The support surface that defines the width and length for the geometry calculation. Cannot be null.</param>
        /// <param name="gridStep">The size, in units, of each grid cell. Must be a positive integer greater than zero. Defaults to 1.</param>
        /// <returns>A LayerGeometry object that describes the calculated occupancy grid and item placements for the specified
        /// layer and support surface.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if gridStep is less than or equal to zero.</exception>
        public static LayerGeometry Build(Layer layer, SupportSurface supportSurface, int gridStep = 1)
        {
            ArgumentNullException.ThrowIfNull(layer);
            ArgumentNullException.ThrowIfNull(supportSurface);
            if (gridStep <= 0)
                throw new ArgumentOutOfRangeException(nameof(gridStep), "gridStep must be non-zero and positive");

            // Geometry dimensions: Width = pallet Width, Length = pallet Length
            int gridWidth = (int)Math.Ceiling((double)supportSurface.Width / gridStep);
            int gridLength = (int)Math.Ceiling((double)supportSurface.Length / gridStep);

            var geometry = new LayerGeometry(gridWidth, gridLength)
            {
                GridStep = gridStep
            };

            for (int itemIndex = 0; itemIndex < layer.Items.Count; itemIndex++)
            {
                var item = layer.Items[itemIndex];
                var sku = item.SkuType;
                if (sku == null)
                    continue;

                // Spans in pallet coordinates: X along Length, Y along Width
                int xSpan = item.Rotated ? sku.Width : sku.Length;   // same as PositionedItem.GetXSpan()
                int ySpan = item.Rotated ? sku.Length : sku.Width;   // same as PositionedItem.GetYSpan()

                int startX = Math.Clamp(item.X / gridStep, 0, gridLength - 1); // X within Length cells
                int startY = Math.Clamp(item.Y / gridStep, 0, gridWidth - 1);  // Y within Width cells

                int cellsWide = Math.Max(1, (int)Math.Ceiling((double)xSpan / gridStep));
                int cellsLong = Math.Max(1, (int)Math.Ceiling((double)ySpan / gridStep));

                int endX = Math.Min(startX + cellsWide, gridLength);
                int endY = Math.Min(startY + cellsLong, gridWidth);

                for (int x = startX; x < endX; x++)
                {
                    for (int y = startY; y < endY; y++)
                    {
                        // OccupancyGrid indexed as [width, length] => [y, x]
                        geometry.OccupancyGrid[y, x] = true;
                        geometry.ItemIndexGrid[y, x] = itemIndex;
                    }
                }
                // Store rectangle in pallet coordinates (origin bottom-left)
                geometry.ItemRectangles.Add(new RectD(item.X, item.Y, xSpan, ySpan));
            }

            layer.Geometry = geometry;
            return geometry;
        }
    }
}
