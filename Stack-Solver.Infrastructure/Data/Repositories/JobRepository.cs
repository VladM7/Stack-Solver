using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stack_Solver.Models.Jobs;

namespace Stack_Solver.Data.Repositories
{
    public class JobRepository(IDbContextFactory<ApplicationDbContext> factory, ILogger<JobRepository> logger) : IJobRepository
    {
        public event EventHandler<JobSummary>? JobAdded;
        public event EventHandler<JobSummary>? JobUpdated;

        public async Task<IList<JobSummary>> GetSummariesAsync(CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            // Project in the query so the large SettingsJson/ResultsJson columns are never read.
            return await db.Jobs
                .AsNoTracking()
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => new JobSummary(j.Id, j.CreatedAt, j.Status, j.SolutionCount, j.TotalPallets))
                .ToListAsync(ct);
        }

        public async Task<Job?> GetAsync(string id, CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            return await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct);
        }

        public async Task AddAsync(Job job, CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            db.Jobs.Add(job);
            await db.SaveChangesAsync(ct);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Job added: {JobId}", job.Id);
            }
            JobAdded?.Invoke(this, ToSummary(job));
        }

        public async Task UpdateAsync(Job job, CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            db.Jobs.Update(job);
            await db.SaveChangesAsync(ct);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Job updated: {JobId} -> {Status}", job.Id, job.Status);
            }
            JobUpdated?.Invoke(this, ToSummary(job));
        }

        public async Task DeleteAsync(string id, CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            var entity = await db.Jobs.FindAsync([id], ct);
            if (entity != null)
            {
                db.Jobs.Remove(entity);
                await db.SaveChangesAsync(ct);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Job deleted: {JobId}", id);
                }
            }
        }

        public async Task<int> FailOrphanedOngoingAsync(CancellationToken ct = default)
        {
            using var db = await factory.CreateDbContextAsync(ct);
            var orphans = await db.Jobs.Where(j => j.Status == JobStatus.Ongoing).ToListAsync(ct);
            if (orphans.Count == 0) return 0;

            foreach (var job in orphans)
            {
                job.Status = JobStatus.Failed;
                job.Error = "Interrupted: the application closed before this run finished.";
                job.CompletedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Marked {Count} orphaned ongoing job(s) as failed", orphans.Count);
            return orphans.Count;
        }

        private static JobSummary ToSummary(Job job)
            => new(job.Id, job.CreatedAt, job.Status, job.SolutionCount, job.TotalPallets);
    }
}
