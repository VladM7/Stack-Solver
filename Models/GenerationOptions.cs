namespace Stack_Solver.Models
{
    public class GenerationOptions
    {
        public int MaxSolverTime { get; set; }
        public int MaxCandidates { get; set; }

        public GenerationOptions() { }

        public GenerationOptions(int maxSolverTime, int maxCandidates)
        {
            MaxSolverTime = maxSolverTime;
            MaxCandidates = maxCandidates;
        }

        public static GenerationOptions From(GenerationOptions? source)
        {
            if (source == null) return new GenerationOptions();
            return new GenerationOptions(source.MaxSolverTime, source.MaxCandidates);
        }
    }
}
