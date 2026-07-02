using Stack_Solver.Models.Layering;

namespace Stack_Solver.Models.Jobs
{
    /// <summary>
    /// A self-contained copy of every setting a generation run used, including the SKUs (with their
    /// dimensions and quantities). Stored as <see cref="Job.SettingsJson"/> so a run can be reviewed
    /// or reproduced even after the live SKU library or default settings have changed.
    /// </summary>
    public class JobSettingsSnapshot
    {
        /// <summary>Payload format version, so future changes can be migrated or rejected gracefully.</summary>
        public int SchemaVersion { get; set; } = 1;

        // Pallet
        public string? DefaultCatalog { get; set; }
        public string? DefaultPalletName { get; set; }
        public int PalletLength { get; set; }
        public int PalletWidth { get; set; }
        public double PalletHeight { get; set; }

        // Load limits
        public int MaxStackHeight { get; set; }
        public int MaxStackWeight { get; set; }
        public OverhangMode OverhangMode { get; set; }
        public double MaxSkuOverhang { get; set; }
        public double MaxTopHeavyPercent { get; set; }

        // Algorithms
        public bool UseCpsat { get; set; }
        public bool UseGreedy { get; set; }
        public bool UseCpsatSolution { get; set; }
        public bool UseBranchAndPrice { get; set; }
        public int SolverTimeLimit { get; set; }
        public int MaxCpsatCandidates { get; set; }
        public int BlfAttempts { get; set; }

        /// <summary>The SKUs that participated in the run (selected, quantity &gt; 0).</summary>
        public List<JobSkuSnapshot> Skus { get; set; } = [];
    }

    /// <summary>A SKU as it was at the moment of the run, embedded in a <see cref="JobSettingsSnapshot"/>.</summary>
    public class JobSkuSnapshot
    {
        public string SkuId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Length { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Weight { get; set; }
        public bool Rotatable { get; set; }
        public string? Notes { get; set; }
        public int Quantity { get; set; }
    }
}
