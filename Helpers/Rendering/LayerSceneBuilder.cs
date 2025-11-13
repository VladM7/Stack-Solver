using Stack_Solver.Models.Layering;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Stack_Solver.Helpers.Rendering
{
    /// <summary>
    /// Builds a 3D scene (single pallet layer) into a target Model3DGroup.
    /// Can prepare geometry off the UI thread, then apply to the visual tree.
    /// </summary>
    public class LayerSceneBuilder
    {
        private readonly Dictionary<string, Brush> _skuBrushCache = [];
        private readonly Lock _cacheLock = new();

        private Dictionary<GeometryModel3D, PositionedItem> _geometryMap = [];
        public bool TryGetItemForGeometry(GeometryModel3D geometry, out PositionedItem item) => _geometryMap.TryGetValue(geometry, out item!);

        /// <summary>
        /// Synchronous build
        /// </summary>
        public void Build(Model3DGroup target, Layer layer, int palletLength, int palletWidth, double palletHeight)
        {
            if (target == null || layer == null) return;
            target.Children.Clear();
            PopulateGroup(target, layer, palletLength, palletWidth, palletHeight);
        }

        /// <summary>
        /// Asynchronous build
        /// </summary>
        public async Task BuildAsync(Model3DGroup target, Layer layer, int palletLength, int palletWidth, double palletHeight, CancellationToken ct = default)
        {
            if (target == null || layer == null) return;

            var itemsSnapshot = layer.Items.ToList();
            double maxItemHeight = itemsSnapshot.Count == 0 ? 0 : itemsSnapshot.Max(i => (double)i.SkuType.Height);

            var tempGroup = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var g = new Model3DGroup();
                var mapping = new Dictionary<GeometryModel3D, PositionedItem>();

                g.Children.Add(new AmbientLight(Colors.DimGray));
                g.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -2, -1)));

                var palletBrush = new SolidColorBrush(Color.FromRgb(160, 120, 80));
                g.Children.Add(GeometryCreator.CreateBoxWithEdges(new Point3D(0, 0, 0), palletLength, palletHeight, palletWidth, palletBrush, Colors.Black, 0.4));

                foreach (var item in itemsSnapshot)
                {
                    ct.ThrowIfCancellationRequested();
                    var sku = item.SkuType;
                    double boxLength = item.Rotated ? sku.Width : sku.Length;
                    double boxWidth = item.Rotated ? sku.Length : sku.Width;
                    double boxHeight = sku.Height;
                    var origin = new Point3D(item.X, palletHeight, item.Y);
                    var brush = GetBrushForSku(sku.SkuId);
                    var boxGroup = GeometryCreator.CreateBoxWithEdges(origin, boxLength, boxHeight, boxWidth, brush, Colors.Black, 0.25);
                    g.Children.Add(boxGroup);
                    if (boxGroup is Model3DGroup boxModelGroup)
                    {
                        foreach (var child in boxModelGroup.Children)
                        {
                            if (child is GeometryModel3D geo)
                            {
                                mapping[geo] = item;
                            }
                        }
                    }
                }

                TryFreezeRecursive(g);
                return (Group: g, Map: mapping);
            }, ct).ConfigureAwait(true);

            ct.ThrowIfCancellationRequested();
            target.Children.Clear();
            foreach (var child in tempGroup.Group.Children)
                target.Children.Add(child);
            _geometryMap = tempGroup.Map;
        }

        private void PopulateGroup(Model3DGroup target, Layer layer, int palletLength, int palletWidth, double palletHeight)
        {
            _geometryMap = [];
            target.Children.Add(new AmbientLight(Colors.DimGray));
            target.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -2, -1)));
            var palletBrush = new SolidColorBrush(Color.FromRgb(160, 120, 80));
            target.Children.Add(GeometryCreator.CreateBoxWithEdges(new Point3D(0, 0, 0), palletLength, palletHeight, palletWidth, palletBrush, Colors.Black, 0.4));

            double maxItemHeight = 0;
            foreach (var item in layer.Items)
            {
                var sku = item.SkuType;
                double boxLength = item.Rotated ? sku.Width : sku.Length;
                double boxWidth = item.Rotated ? sku.Length : sku.Width;
                double boxHeight = sku.Height;
                if (boxHeight > maxItemHeight) maxItemHeight = boxHeight;
                var origin = new Point3D(item.X, palletHeight, item.Y);
                var brush = GetBrushForSku(sku.SkuId);
                var boxGroup = GeometryCreator.CreateBoxWithEdges(origin, boxLength, boxHeight, boxWidth, brush, Colors.Black, 0.25);
                target.Children.Add(boxGroup);
                if (boxGroup is Model3DGroup boxModelGroup)
                {
                    foreach (var child in boxModelGroup.Children)
                    {
                        if (child is GeometryModel3D geo)
                        {
                            _geometryMap[geo] = item;
                        }
                    }
                }
            }
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
            {
                foreach (var child in group.Children)
                    TryFreezeRecursive(child);
            }
            if (model is Freezable f && f.CanFreeze && !f.IsFrozen)
            {
                try { f.Freeze(); } catch { }
            }
        }
    }
}
