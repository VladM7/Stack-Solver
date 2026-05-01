using Stack_Solver.Models.Supports;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Stack_Solver.Helpers.Rendering
{
    public class PalletSceneBuilder
    {
        private readonly Dictionary<string, Brush> _skuBrushCache = [];
        private readonly Lock _cacheLock = new();

        public async Task BuildAsync(
            Model3DGroup target,
            PalletTemplate template,
            int palletLength, int palletWidth, double palletHeight,
            CancellationToken ct = default)
        {
            if (target == null || template == null) return;

            var tempGroup = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var g = new Model3DGroup();

                g.Children.Add(new AmbientLight(Colors.DimGray));
                g.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -2, -1)));

                var palletBrush = new SolidColorBrush(Color.FromRgb(160, 120, 80));
                palletBrush.Freeze();
                g.Children.Add(GeometryCreator.CreateBoxWithEdges(
                    new Point3D(0, 0, 0), palletLength, palletHeight, palletWidth,
                    palletBrush, Colors.Black, 0.4));

                double currentY = palletHeight;
                foreach (var layer in template.Layers)
                {
                    ct.ThrowIfCancellationRequested();

                    foreach (var item in layer.Items)
                    {
                        var sku = item.SkuType;
                        double boxLength = item.Rotated ? sku.Width : sku.Length;
                        double boxWidth = item.Rotated ? sku.Length : sku.Width;
                        var origin = new Point3D(item.X, currentY, item.Y);
                        var brush = GetBrushForSku(sku.SkuId);
                        g.Children.Add(GeometryCreator.CreateBoxWithEdges(
                            origin, boxLength, sku.Height, boxWidth, brush, Colors.Black, 0.25));
                    }

                    currentY += layer.Metadata.Height;
                }

                TryFreezeRecursive(g);
                return g;
            }, ct).ConfigureAwait(true);

            ct.ThrowIfCancellationRequested();
            target.Children.Clear();
            foreach (var child in tempGroup.Children)
                target.Children.Add(child);
        }

        private Brush GetBrushForSku(string skuId)
        {
            lock (_cacheLock)
            {
                if (_skuBrushCache.TryGetValue(skuId, out var b)) return b;
                int hash = skuId.GetHashCode();
                byte r = (byte)(50 + (hash & 0x7F));
                byte g = (byte)(50 + ((hash >> 7) & 0x7F));
                byte bl = (byte)(50 + ((hash >> 14) & 0x7F));
                var brush = new SolidColorBrush(Color.FromRgb(r, g, bl));
                if (brush.CanFreeze) brush.Freeze();
                _skuBrushCache[skuId] = brush;
                return brush;
            }
        }

        private static void TryFreezeRecursive(Model3D model)
        {
            if (model is Model3DGroup group)
                foreach (var child in group.Children)
                    TryFreezeRecursive(child);
            if (model is Freezable f && f.CanFreeze && !f.IsFrozen)
                try { f.Freeze(); } catch { }
        }
    }
}
