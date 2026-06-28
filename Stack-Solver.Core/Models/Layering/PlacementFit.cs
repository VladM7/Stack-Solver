namespace Stack_Solver.Models.Layering
{
    /// <summary>
    /// Result of searching for the best position of an upper layer on top of a lower one.
    /// <see cref="OffsetX"/>/<see cref="OffsetY"/> are the translation (in pallet units) that
    /// should be applied to the upper layer's items to reach the returned support quality.
    /// <see cref="Feasible"/> indicates whether that best placement keeps every SKU's overhang
    /// within the allowed maximum.
    /// </summary>
    public readonly record struct PlacementFit(
        bool Feasible,
        int OffsetX,
        int OffsetY,
        LayerSupportMetrics Metrics)
    {
        public static PlacementFit None => new(false, 0, 0, new LayerSupportMetrics(double.PositiveInfinity, double.PositiveInfinity));
    }
}
