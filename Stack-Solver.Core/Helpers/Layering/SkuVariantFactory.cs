using Stack_Solver.Models;

namespace Stack_Solver.Helpers.Layering
{
    public readonly record struct SkuVariant(string VariantId, SKU Sku, int SpanX, int SpanY, bool Rotated);

    public static class SkuVariantFactory
    {
        public static List<SkuVariant> CreateAllOrientations(IEnumerable<SKU>? skus)
        {
            var variants = new List<SkuVariant>();
            if (skus == null)
                return variants;

            foreach (var sku in skus)
            {
                if (sku == null)
                    continue;

                variants.Add(new SkuVariant($"{sku.SkuId}_n", sku, sku.Length, sku.Width, false));

                if (sku.Rotatable && sku.Length != sku.Width)
                {
                    variants.Add(new SkuVariant($"{sku.SkuId}_r", sku, sku.Width, sku.Length, true));
                }
            }

            return variants;
        }
    }
}
