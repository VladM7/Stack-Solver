namespace Stack_Solver.Models
{
    public class PositionedItem
    {
        private SKU SkuType { get; set; }
        private int X { get; set; }
        private int Y { get; set; }
        private bool Rotated { get; set; }

        public PositionedItem(SKU skuType, int x, int y, bool rotated)
        {
            SkuType = skuType;
            X = x;
            Y = y;
            Rotated = rotated;
        }
    }
}
