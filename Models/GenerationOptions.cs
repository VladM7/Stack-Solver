namespace Stack_Solver.Models
{
    public class GenerationOptions
    {
        public int MaxSolverTime { get; set; } = 60;
        public int MaxCandidates { get; set; } = 2000;

        public GenerationOptions() { }

        public GenerationOptions(int maxSolverTime, int maxCandidates)
        {
            MaxSolverTime = maxSolverTime;
            MaxCandidates = maxCandidates;
        }
    }
}
