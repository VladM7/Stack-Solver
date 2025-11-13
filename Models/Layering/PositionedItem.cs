namespace Stack_Solver.Models.Layering
{
    public class PositionedItem(SKU skuType, int x, int y, bool rotated)
    {
        public SKU SkuType { get; set; } = skuType;
        public int X { get; set; } = x;
        public int Y { get; set; } = y;
        public bool Rotated { get; set; } = rotated;

        public int GetYSpan() => Rotated ? SkuType.Length : SkuType.Width;
        public int GetXSpan() => Rotated ? SkuType.Width : SkuType.Length;
    }
}
