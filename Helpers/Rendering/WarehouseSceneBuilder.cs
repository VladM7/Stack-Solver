using Stack_Solver.Models.Supports;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Stack_Solver.Helpers.Rendering
{
    /// <summary>One pallet-type block in the warehouse view (its overall bounds), used for highlighting.</summary>
    public readonly record struct WarehouseBlock(string TemplateId, Point3D Origin, double SizeX, double SizeY, double SizeZ);

    /// <summary>
    /// Builds the high-level "warehouse" scene: every pallet instance of the solution, grouped by
    /// type into blocks with aisles between groups. Full detail (every box) by default; above a
    /// threshold each pallet collapses to a single tinted load block for performance.
    /// </summary>
    public class WarehouseSceneBuilder
    {
        private readonly Dictionary<string, Brush> _skuBrushCache = [];
        private readonly Lock _cacheLock = new();

        private Dictionary<GeometryModel3D, string> _geometryToTemplateId = [];
        private List<LabelInfo> _typeLabels = [];
        private List<WarehouseBlock> _blocks = [];

        public bool TryGetTemplateIdForGeometry(GeometryModel3D geo, out string templateId)
            => _geometryToTemplateId.TryGetValue(geo, out templateId!);

        public IReadOnlyList<LabelInfo> TypeLabels => _typeLabels;
        public IReadOnlyList<WarehouseBlock> Blocks => _blocks;
        public Point3D ContentCenter { get; private set; }
        public double FrameDistance { get; private set; } = 300;

        private static readonly Color[] TypePalette =
        [
            Color.FromRgb(120, 160, 210), Color.FromRgb(150, 200, 140), Color.FromRgb(220, 180, 120),
            Color.FromRgb(200, 140, 160), Color.FromRgb(160, 150, 210), Color.FromRgb(140, 200, 200),
            Color.FromRgb(210, 200, 130), Color.FromRgb(190, 160, 140)
        ];

        public async Task BuildAsync(
            Model3DGroup target,
            IReadOnlyList<(PalletTemplate Template, int Count, string Name)> types,
            int palletLength, int palletWidth, int palletHeight,
            int detailThreshold,
            CancellationToken ct = default)
        {
            if (target == null) return;

            int totalPallets = types.Sum(t => Math.Max(0, t.Count));
            bool simplify = totalPallets > detailThreshold;

            var result = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var g = new Model3DGroup();
                var map = new Dictionary<GeometryModel3D, string>();
                var labels = new List<LabelInfo>();
                var blocks = new List<WarehouseBlock>();

                g.Children.Add(new AmbientLight(Colors.DimGray));
                g.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -2, -1)));

                double gap = Math.Max(15.0, 0.12 * Math.Max(palletLength, palletWidth));
                double aisle = gap * 3;
                double cellX = palletLength + gap;
                double cellZ = palletWidth + gap;

                var palletBrush = new SolidColorBrush(Color.FromRgb(160, 120, 80));
                palletBrush.Freeze();

                double cursorX = 0;
                double maxDepth = 0, maxHeight = 0;
                int typeIndex = 0;

                foreach (var (template, count, name) in types)
                {
                    int n = Math.Max(0, count);
                    if (n == 0) { typeIndex++; continue; }
                    ct.ThrowIfCancellationRequested();

                    int cols = (int)Math.Ceiling(Math.Sqrt(n));
                    int rows = (int)Math.Ceiling(n / (double)cols);
                    double blockWidth = cols * cellX - gap;
                    double blockDepth = rows * cellZ - gap;
                    double loadHeight = palletHeight + template.TotalHeight;
                    var typeColor = TypePalette[typeIndex % TypePalette.Length];

                    // Every instance of this type has identical box layout, so compute the culling
                    // mask (and gather box params) once in pallet-local coordinates and reuse it.
                    List<(double X, double Y, double Z, double L, double H, double W, Brush Brush)>? localBoxes = null;
                    BoxFaces[]? masks = null;
                    if (!simplify)
                    {
                        localBoxes = [];
                        var bounds = new List<BoxBounds>();
                        double cy = palletHeight;
                        foreach (var layer in template.Layers)
                        {
                            foreach (var item in layer.Items)
                            {
                                var sku = item.SkuType;
                                double bl = item.Rotated ? sku.Width : sku.Length;
                                double bw = item.Rotated ? sku.Length : sku.Width;
                                localBoxes.Add((item.X, cy, item.Y, bl, sku.Height, bw, GetBrushForSku(sku.SkuId)));
                                bounds.Add(new BoxBounds(item.X, item.X + bl, cy, cy + sku.Height, item.Y, item.Y + bw));
                            }
                            cy += layer.Metadata.Height;
                        }
                        masks = FaceCuller.Compute(bounds, palletTopY: palletHeight, palletMaxX: palletLength, palletMaxZ: palletWidth);
                    }

                    for (int i = 0; i < n; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        int col = i % cols, row = i / cols;
                        double ox = cursorX + col * cellX;
                        double oz = row * cellZ;

                        // pallet base
                        AddMapped(g, map, template.Id, GeometryCreator.CreateBoxWithEdges(
                            new Point3D(ox, 0, oz), palletLength, palletHeight, palletWidth, palletBrush, Colors.Black, 0.4));

                        if (simplify)
                        {
                            var brush = new SolidColorBrush(typeColor);
                            brush.Freeze();
                            AddMapped(g, map, template.Id, GeometryCreator.CreateBoxWithEdges(
                                new Point3D(ox, palletHeight, oz), palletLength, template.TotalHeight, palletWidth, brush, Colors.Black, 0.3));
                        }
                        else
                        {
                            for (int k = 0; k < localBoxes!.Count; k++)
                            {
                                var (lx, cy, lz, l, h, w, brush) = localBoxes[k];
                                AddMapped(g, map, template.Id, GeometryCreator.CreateBoxMerged(
                                    new Point3D(ox + lx, cy, oz + lz), l, h, w, brush, Colors.Black, 0.25, masks![k]));
                            }
                        }
                    }

                    labels.Add(new LabelInfo(new Point3D(cursorX + blockWidth / 2, loadHeight + 12, blockDepth / 2), $"{name}  ×{n}"));
                    blocks.Add(new WarehouseBlock(template.Id, new Point3D(cursorX, 0, 0), blockWidth, loadHeight, blockDepth));

                    maxDepth = Math.Max(maxDepth, blockDepth);
                    maxHeight = Math.Max(maxHeight, loadHeight);
                    cursorX += blockWidth + aisle;
                    typeIndex++;
                }

                double totalWidth = Math.Max(cursorX - aisle, palletLength);
                var center = new Point3D(totalWidth / 2.0, maxHeight / 2.0, maxDepth / 2.0);
                double distance = Math.Sqrt(totalWidth * totalWidth + maxDepth * maxDepth + maxHeight * maxHeight) * 1.1 + Math.Max(totalWidth, maxDepth) * 0.2;

                TryFreezeRecursive(g);
                return (Group: g, Map: map, Labels: labels, Blocks: blocks, Center: center, Distance: distance);
            }, ct).ConfigureAwait(true);

            ct.ThrowIfCancellationRequested();
            target.Children.Clear();
            foreach (var child in result.Group.Children)
                target.Children.Add(child);

            _geometryToTemplateId = result.Map;
            _typeLabels = result.Labels;
            _blocks = result.Blocks;
            ContentCenter = result.Center;
            FrameDistance = result.Distance;
        }

        private static void AddMapped(Model3DGroup g, Dictionary<GeometryModel3D, string> map, string templateId, Model3DGroup boxGroup)
        {
            g.Children.Add(boxGroup);
            foreach (var child in boxGroup.Children)
                if (child is GeometryModel3D geo)
                    map[geo] = templateId;
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
