using Stack_Solver.Models.Layering;

namespace Stack_Solver.Services
{
    public static class LayerGeometryOptimizer
    {
        public static void CenterLayer(Layer layer)
        {
            ArgumentNullException.ThrowIfNull(layer);
            if (layer.Geometry == null)
                throw new InvalidOperationException("Layer geometry is not built. Please build the geometry before centering.");

            var items = layer.Items;
            if (items == null || items.Count == 0) return;

            int containerX = layer.Geometry.Length;
            int containerY = layer.Geometry.Width;

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            foreach (var it in items)
            {
                int xSpan = it.GetXSpan();
                int ySpan = it.GetYSpan();
                if (it.X < minX) minX = it.X;
                if (it.Y < minY) minY = it.Y;
                if (it.X + xSpan > maxX) maxX = it.X + xSpan;
                if (it.Y + ySpan > maxY) maxY = it.Y + ySpan;
            }

            int usedX = Math.Max(0, maxX - minX);
            int usedY = Math.Max(0, maxY - minY);

            double desiredDx = (containerX - usedX) / 2.0 - minX;
            double desiredDy = (containerY - usedY) / 2.0 - minY;

            int minDx = -minX;
            int maxDx = containerX - maxX;
            int minDy = -minY;
            int maxDy = containerY - maxY;

            int lowDx = Math.Min(minDx, maxDx);
            int highDx = Math.Max(minDx, maxDx);
            int lowDy = Math.Min(minDy, maxDy);
            int highDy = Math.Max(minDy, maxDy);

            int dx = Math.Clamp((int)Math.Round(desiredDx), lowDx, highDx);
            int dy = Math.Clamp((int)Math.Round(desiredDy), lowDy, highDy);

            if (dx == 0 && dy == 0) return;

            foreach (var it in items)
            {
                it.X += dx;
                it.Y += dy;
            }
        }
    }
}
