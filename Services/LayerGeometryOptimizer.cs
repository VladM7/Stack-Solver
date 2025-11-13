using Stack_Solver.Models.Layering;

namespace Stack_Solver.Services
{
    public static class LayerGeometryOptimizer
    {
        public static void OptimizeGeometry(Layer layer)
        {
            ArgumentNullException.ThrowIfNull(layer);
            if (layer.Geometry == null)
                throw new InvalidOperationException("Layer geometry is not built. Please build the geometry before optimizing.");

            var items = layer.Items;
            if (items == null || items.Count == 0) return;

            var colGroups = BuildGroups(items, true);
            var rowGroups = BuildGroups(items, false);

            int colMultiItems = colGroups.Where(g => g.Items.Count >= 2).Sum(g => g.Items.Count);
            int rowMultiItems = rowGroups.Where(g => g.Items.Count >= 2).Sum(g => g.Items.Count);
            bool byColumns = colMultiItems > rowMultiItems || (colMultiItems == rowMultiItems && colGroups.Count <= rowGroups.Count);

            var groups = byColumns ? colGroups : rowGroups;
            if (groups.Count == 0) return;

            int targetSpan = groups.Max(g => g.SumSpan);

            foreach (var g in groups)
            {
                int n = g.Items.Count;
                if (n <= 1) continue;
                if (g.SumSpan >= targetSpan) continue;

                int newGapsTotal = targetSpan - g.SumSpan;
                double gapBetween = (double)newGapsTotal / (n - 1);

                double pos = GetVariableCoord(g.Items[0], byColumns);
                var planned = new double[n];
                for (int idx = 0; idx < n; idx++)
                {
                    planned[idx] = pos;
                    pos += GetSpan(g.Items[idx], byColumns);
                    if (idx < n - 1) pos += gapBetween;
                }
                for (int idx = 0; idx < n; idx++)
                    SetVariableCoord(g.Items[idx], (int)Math.Round(planned[idx]), byColumns);
            }
        }

        private static List<Group> BuildGroups(List<PositionedItem> items, bool byColumns)
        {
            return [.. items
                .GroupBy(i => byColumns ? i.X : i.Y)
                .Select(g =>
                {
                    var list = g.OrderBy(i => GetVariableCoord(i, byColumns)).ToList();
                    int sumSpan = list.Sum(i => GetSpan(i, byColumns));
                    return new Group(g.Key, list, sumSpan);
                })];
        }

        private readonly record struct Group(int Key, List<PositionedItem> Items, int SumSpan);

        private static int GetSpan(PositionedItem i, bool byColumns) => byColumns ? i.GetYSpan() : i.GetXSpan();
        private static int GetVariableCoord(PositionedItem i, bool byColumns) => byColumns ? i.Y : i.X;
        private static void SetVariableCoord(PositionedItem i, int v, bool byColumns)
        {
            if (byColumns) i.Y = v; else i.X = v;
        }

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
