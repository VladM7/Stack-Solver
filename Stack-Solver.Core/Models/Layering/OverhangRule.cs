namespace Stack_Solver.Models.Layering
{
    /// <summary>
    /// How the maximum allowed overhang of a box on the layer below is interpreted.
    /// </summary>
    public enum OverhangMode
    {
        /// <summary>A box may hang at most <see cref="OverhangRule.Value"/> cm past the supporting load on any side.</summary>
        AbsoluteCm = 0,

        /// <summary>At least <see cref="OverhangRule.Value"/> percent of a box's footprint must rest on support.</summary>
        MinSupportedPercent = 1,

        /// <summary>A box's centre of weight must sit over support, so it cannot tip — the value is ignored.</summary>
        Auto = 2
    }

    /// <summary>
    /// The rule deciding whether a box is adequately supported by the layer beneath it.
    /// <see cref="Value"/> is centimetres for <see cref="OverhangMode.AbsoluteCm"/>, a percentage in
    /// [0, 100] for <see cref="OverhangMode.MinSupportedPercent"/>, and unused for <see cref="OverhangMode.Auto"/>.
    /// </summary>
    public readonly record struct OverhangRule(OverhangMode Mode, double Value)
    {
        /// <summary>No overhang on any side — equivalent to the original full-support default.</summary>
        public static OverhangRule FullSupport => new(OverhangMode.AbsoluteCm, 0);
    }
}
