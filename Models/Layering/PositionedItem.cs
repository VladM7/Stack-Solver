namespace Stack_Solver.Models.Layering
{
    /// <summary>
    /// Represents an item placed at a specific position in a 2D layer, defined by its SKU type,
    /// coordinates, and whether it's rotated or not.
    /// </summary>
    /// <param name="skuType">The SKU type.</param>
    /// <param name="x">The x-coordinate of the item's position in 2D.</param>
    /// <param name="y">The y-coordinate of the item's position in 2D.</param>
    /// <param name="rotated">A value indicating whether the item is rotated. If <see langword="true"/>, the item's length and width are
    /// swapped when determining its span.</param>
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
