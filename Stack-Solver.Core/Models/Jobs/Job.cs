namespace Stack_Solver.Models.Jobs
{
    /// <summary>
    /// A single stored generation run: the settings it used, the results it produced and
    /// lifecycle metadata. Persisted so past runs survive restarts and can be revisited.
    /// </summary>
    /// <remarks>
    /// The bulky <see cref="SettingsJson"/> and <see cref="ResultsJson"/> payloads are serialized
    /// snapshots (see <c>JobSettingsSnapshot</c> / <c>JobResultsSnapshot</c>); listing screens should
    /// project <see cref="JobSummary"/> instead of loading them.
    /// </remarks>
    public class Job
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>When the run started, in UTC.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When the run reached a terminal status, in UTC; null while ongoing.</summary>
        public DateTime? CompletedAt { get; set; }

        public JobStatus Status { get; set; } = JobStatus.Ongoing;

        /// <summary>Serialized <c>JobSettingsSnapshot</c>: pallet, limits, algorithm flags and the SKUs used.</summary>
        public string SettingsJson { get; set; } = string.Empty;

        /// <summary>Serialized <c>JobResultsSnapshot</c>: the produced solutions; null until the run finishes.</summary>
        public string? ResultsJson { get; set; }

        /// <summary>Number of solutions produced (cached for quick listing).</summary>
        public int SolutionCount { get; set; }

        /// <summary>Total pallets in the best/first solution (cached for quick listing).</summary>
        public int TotalPallets { get; set; }

        /// <summary>Failure message when <see cref="Status"/> is <see cref="JobStatus.Failed"/>.</summary>
        public string? Error { get; set; }
    }
}
