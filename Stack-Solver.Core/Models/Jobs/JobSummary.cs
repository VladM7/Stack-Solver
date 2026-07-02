namespace Stack_Solver.Models.Jobs
{
    /// <summary>
    /// Lightweight projection of a <see cref="Job"/> for list views, carrying only the columns the
    /// Job Manager grid needs so the heavy settings/results JSON is never loaded when listing.
    /// </summary>
    public record JobSummary(
        string Id,
        DateTime CreatedAt,
        JobStatus Status,
        int SolutionCount,
        int TotalPallets);
}
