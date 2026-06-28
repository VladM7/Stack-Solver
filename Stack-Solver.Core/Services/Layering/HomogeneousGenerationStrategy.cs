using Stack_Solver.Helpers.Layering;
using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Layering
{
    public class HomogeneousGenerationStrategy : ILayerGenerationStrategy
    {
        public string Name => "Homogeneous Grid";

        public List<Layer> Generate(List<SKU> skus, SupportSurface supportSurface, GenerationOptions options)
        {
            if (skus == null || skus.Count == 0 || supportSurface == null)
                return [];

            int px = supportSurface.Length;
            int py = supportSurface.Width;
            if (px <= 0 || py <= 0)
                return [];

            double area = px * py;
            if (area <= 0)
                return [];

            var variants = SkuVariantFactory.CreateAllOrientations(skus);
            if (variants.Count == 0)
                return [];

            var candidateLayers = new List<Layer>();

            foreach (var variant in variants)
            {
                var layer = BuildLayerForVariant(variant, px, py, area, supportSurface);
                if (layer != null)
                    candidateLayers.Add(layer);
            }

            return candidateLayers;
        }

        private static Layer? BuildLayerForVariant(SkuVariant variant, int palletLength, int palletWidth, double palletArea, SupportSurface supportSurface)
        {
            if (variant.Sku == null || variant.SpanX <= 0 || variant.SpanY <= 0)
                return null;

            int nx = palletLength / variant.SpanX;
            int ny = palletWidth / variant.SpanY;
            int capacity = nx * ny;
            if (capacity <= 0)
                return null;

            int allowed = variant.Sku.Quantity > 0 ? Math.Min(capacity, variant.Sku.Quantity) : capacity;
            if (allowed <= 0)
                return null;

            var placements = new List<PositionedItem>(allowed);
            int placed = 0;
            for (int ix = 0; ix < nx && placed < allowed; ix++)
            {
                for (int iy = 0; iy < ny && placed < allowed; iy++)
                {
                    placements.Add(new PositionedItem(variant.Sku, ix * variant.SpanX, iy * variant.SpanY, variant.Rotated));
                    placed++;
                }
            }

            if (placements.Count == 0)
                return null;

            double usedArea = placements.Count * variant.SpanX * variant.SpanY;
            double utilization = palletArea > 0 ? usedArea / palletArea : 0;
            string orientation = variant.Rotated ? "rotated" : "normal";
            string description = $"homogeneous {variant.Sku.Name} ({orientation}) {nx}x{ny}";

            var metadata = new LayerMetadata(utilization, variant.Sku.Height, description);
            var layer = new Layer($"hom_grid_{variant.Sku.Name}_{orientation}", placements, metadata);
            layer.Geometry = LayerGeometryBuilder.Build(layer, supportSurface);
            layer.Metrics = LayerMetricsCalculator.Compute(layer, supportSurface);
            return layer;
        }
    }
}
