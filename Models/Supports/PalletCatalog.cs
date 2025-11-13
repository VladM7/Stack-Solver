namespace Stack_Solver.Models.Supports
{
    /// <summary>
    /// Central catalog of commonly used pallet dimensions grouped by region.
    /// </summary>
    public static class PalletCatalog
    {
        public static IReadOnlyList<Pallet> International { get; } =
        [
            new("EUR (EPAL 1/ISO1)", 120, 80, 0),
            new("EUR 2 (ISO2)", 120, 100, 0),
            new("EUR 3", 100, 120, 0),
            new("UPL Pallet", 120, 110, 0),
            new("HPL (Half Pallet, EUR 6/ISO0)", 60, 80, 0),
            new("QPL (Quarter Pallet)", 60, 40, 0),
            new("ASIA Standard", 110, 110, 0),
            new("AUS Standard", 117, 117, 0)
        ];

        public static IReadOnlyList<Pallet> America { get; } =
        [
            new("US Standard", 48, 40, 0),
            new("US Square Pallet 42x42", 42, 42, 0),
            new("US Square Pallet 48x48", 48, 48, 0)
        ];
    }
}
