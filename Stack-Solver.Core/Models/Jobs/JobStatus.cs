namespace Stack_Solver.Models.Jobs
{
    /// <summary>
    /// Lifecycle state of a generation <see cref="Job"/>.
    /// </summary>
    public enum JobStatus
    {
        /// <summary>Generation is currently running.</summary>
        Ongoing,

        /// <summary>Generation completed and its results were stored.</summary>
        Finished,

        /// <summary>Generation stopped because of an error.</summary>
        Failed,

        /// <summary>Generation was canceled by the user before it completed.</summary>
        Canceled
    }
}
