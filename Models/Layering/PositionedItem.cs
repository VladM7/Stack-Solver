namespace Stack_Solver.Models
{
    public class PositionedItem(SKU skuType, int x, int y, bool rotated)
    {
        public SKU SkuType { get; set; } = skuType;
        public int X { get; set; } = x;
        public int Y { get; set; } = y;
        public bool Rotated { get; set; } = rotated;
    }
}
