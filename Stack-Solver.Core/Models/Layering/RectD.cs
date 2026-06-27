namespace Stack_Solver.Models.Layering
{
    /// <summary>
    /// A framework-agnostic axis-aligned rectangle in pallet coordinates.
    /// Mirrors the shape of <c>System.Windows.Rect</c> without taking a WPF dependency,
    /// so layer geometry can live in the Core layer.
    /// </summary>
    public readonly record struct RectD(double X, double Y, double Width, double Height);
}
