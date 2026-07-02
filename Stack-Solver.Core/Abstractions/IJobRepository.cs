using Stack_Solver.Models.Jobs;

namespace Stack_Solver.Data.Repositories
{
    /// <summary>
    /// Persistence for generation <see cref="Job"/>s. Raises change events so screens (e.g. the
    /// Job Manager grid) can update live, mirroring <see cref="ISkuRepository"/>.
    /// </summary>
    public interface IJobRepository
    {
        /// <summary>Lightweight list for grids — projected, so the settings/results JSON is not loaded.</summary>
        Task<IList<JobSummary>> GetSummariesAsync(CancellationToken ct = default);

        /// <summary>The full job including its settings/results JSON, or null if not found.</summary>
        Task<Job?> GetAsync(string id, CancellationToken ct = default);

        Task AddAsync(Job job, CancellationToken ct = default);
        Task UpdateAsync(Job job, CancellationToken ct = default);
        Task DeleteAsync(string id, CancellationToken ct = default);

        /// <summary>
        /// Marks every job still flagged <see cref="JobStatus.Ongoing"/> as <see cref="JobStatus.Failed"/>.
        /// Used at startup to clean up runs orphaned by a crash. Returns the number healed.
        /// </summary>
        Task<int> FailOrphanedOngoingAsync(CancellationToken ct = default);

        event EventHandler<JobSummary>? JobAdded;
        event EventHandler<JobSummary>? JobUpdated;
    }
}
