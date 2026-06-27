namespace Stack_Solver.Models
{
    /// <summary>
    /// Represents the default configuration options for pallet properties, including dimensions and weight limits.
    /// </summary>
    public class PalletDefaultsOptions
    {
        public string? DefaultCatalog { get; set; }
        public string? DefaultPalletName { get; set; }
        public int PalletLength { get; set; } = 120;
        public int PalletWidth { get; set; } = 80;
        public double PalletHeight { get; set; } = 14.4;
        public int MaxStackHeight { get; set; } = 180;
        public int MaxStackWeight { get; set; } = 950;
        public double MaxSkuOverhang { get; set; } = 0;
    }
}
