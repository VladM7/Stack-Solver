namespace Stack_Solver.Models
{
    /// <summary>
    /// Provides configuration options for the generation process.
    /// </summary>
    /// <remarks>This class stores the generation parameters for easier accessibility.</remarks>
    public class GenerationOptions
    {
        public int MaxSolverTime { get; set; }
        public int MaxCPSATCandidates { get; set; }

        public int BLFAttempts { get; set; }

        public double MaxLayerStability { get; set; } = 50;

        public double PerSkuTopLayerFraction { get; set; } = 0.5;

        /// <summary>Use CP-SAT (rather than the heuristic packer) to generate candidate layers.</summary>
        public bool UseCpsat { get; set; }

        /// <summary>Produce a Greedy assignment solution. Greedy always runs internally as a warm-start seed; this only controls whether it is offered as a result.</summary>
        public bool UseGreedy { get; set; } = true;

        /// <summary>Produce a CP-SAT assignment solution.</summary>
        public bool UseCpsatSolution { get; set; } = true;

        /// <summary>Produce a Branch &amp; Price assignment solution.</summary>
        public bool UseBranchAndPrice { get; set; } = true;

        public GenerationOptions() { }

        public GenerationOptions(int maxSolverTime, int maxCandidates, int blfAttempts)
        {
            MaxSolverTime = maxSolverTime;
            MaxCPSATCandidates = maxCandidates;
            BLFAttempts = blfAttempts;
        }

        public static GenerationOptions From(GenerationOptions? source)
        {
            if (source == null) return new GenerationOptions();
            return new GenerationOptions(source.MaxSolverTime, source.MaxCPSATCandidates, source.BLFAttempts)
            {
                MaxLayerStability = source.MaxLayerStability,
                PerSkuTopLayerFraction = source.PerSkuTopLayerFraction,
                UseCpsat = source.UseCpsat,
                UseGreedy = source.UseGreedy,
                UseCpsatSolution = source.UseCpsatSolution,
                UseBranchAndPrice = source.UseBranchAndPrice
            };
        }
    }
}
