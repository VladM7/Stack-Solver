namespace Stack_Solver.Models.Layering
{
    /// <summary>
    /// The inter-layer stacking-order rule, shared by every stacker (greedy template
    /// enumeration, beam pricer, exact pricer) so they cannot drift apart.
    ///
    /// A layer may rest on the one below only when its <see cref="LayerMetrics.LoadDensity"/>
    /// (weight per footprint area — a pressure proxy) does not exceed the lower layer's by more
    /// than the configured tolerance. A tolerance of 0 is strict: the upper layer must be no
    /// denser than the lower one. A positive tolerance permits a denser layer on top — useful
    /// when support/footprint forces a lighter, larger-footprint layer to the bottom.
    /// </summary>
    public static class StackingLoadRule
    {
        private const double Epsilon = 1e-9;

        /// <summary>
        /// True if a layer of <paramref name="upperDensity"/> may rest on one of
        /// <paramref name="lowerDensity"/> given the top-heavy <paramref name="tolerance"/>
        /// (a fraction, e.g. 0.2 = the upper layer may be up to 20% denser; 0 = strict).
        /// </summary>
        public static bool Allows(double lowerDensity, double upperDensity, double tolerance) =>
            upperDensity <= lowerDensity * (1.0 + tolerance) + Epsilon;
    }
}
