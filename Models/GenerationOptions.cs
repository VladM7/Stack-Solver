namespace Stack_Solver.Models
{
    public class GenerationOptions
    {
        public int MaxSolverTime { get; set; }
        public int MaxCPSATCandidates { get; set; }

        public int BLFAttempts { get; set; }

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
            return new GenerationOptions(source.MaxSolverTime, source.MaxCPSATCandidates, source.BLFAttempts);
        }
    }
}
