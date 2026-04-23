namespace Stack_Solver.Models.Layering
{
    /// <summary>
    /// Summarizes how much of an upper layer lacks direct support from the layer beneath it.
    /// </summary>
    public readonly record struct LayerSupportMetrics(double TotalUnsupportedArea, double MaximumSkuOverhangArea);
}
