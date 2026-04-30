namespace Stack_Solver.Models.Layering
{
    /// <summary>
    /// Layer-level metrics
    /// </summary>
    public class LayerMetrics
    {
        /// <summary>
        /// Fill of the support surface used by this layer, expressed as a percentage in range [0, 100].
        /// </summary>
        public double Utilization { get; set; }

        /// <summary>
        /// Distance of layer center of gravity from pallet center, normalized to [0, 100], where 0 is centered.
        /// </summary>
        public double Stability { get; set; }

        public double TotalWeight { get; set; }

        public IReadOnlyCollection<string> UsedSkuTypes { get; set; } = [];

        public IReadOnlyCollection<string> CompatibleTopLayerIds { get; set; } = [];
    }
}