using Stack_Solver.Models.Layering;

namespace Stack_Solver.Models.Inputs
{
    public class PalletSettingsDto
    {
        public int PalletLength { get; set; }
        public int PalletWidth { get; set; }
        public double PalletHeight { get; set; }
        public bool UseCpsat { get; set; }
        public bool UseGreedy { get; set; }
        public bool UseCpsatSolution { get; set; }
        public bool UseBranchAndPrice { get; set; }
        public int MaxCpsatCandidates { get; set; }
        public int BlfAttempts { get; set; }
        public int SolverTimeLimit { get; set; }
        public int MaxStackHeight { get; set; }
        public int MaxStackWeight { get; set; }
        public double MaxSkuOverhang { get; set; }
        public OverhangMode OverhangMode { get; set; }
        public double MaxTopHeavyPercent { get; set; }
    }
}
