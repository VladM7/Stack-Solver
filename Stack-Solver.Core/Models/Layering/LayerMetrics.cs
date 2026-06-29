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

        /// <summary>Combined base area of the layer's boxes (the load-bearing footprint), in cm².</summary>
        public double FootprintArea { get; set; }

        /// <summary>
        /// Average areal load — weight per unit footprint area (kg/cm²). This is the pressure
        /// proxy used to decide stacking order: a layer may rest on another only when its load
        /// density does not exceed the one below by more than the configured tolerance. Normalizing
        /// by area (rather than comparing total weight) stops a many-box light layer from reading
        /// as "heavy". Zero for an empty or weightless layer.
        /// </summary>
        public double LoadDensity => FootprintArea > 0 ? TotalWeight / FootprintArea : 0;

        public IReadOnlyCollection<string> UsedSkuTypes { get; set; } = [];
    }
}