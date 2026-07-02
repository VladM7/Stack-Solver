using Stack_Solver.Models.Assignment;

namespace Stack_Solver.Models.Jobs
{
    /// <summary>
    /// The domain-level essence of one produced solution, independent of any UI display type.
    /// Bridges the app's solution objects and the persisted <see cref="JobSolutionSnapshot"/>.
    /// </summary>
    public record JobSolutionData(string Name, bool? IsProvenOptimal, double LowerBound, AssignmentResult Result);
}
