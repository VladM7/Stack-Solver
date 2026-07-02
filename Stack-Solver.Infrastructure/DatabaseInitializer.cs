using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stack_Solver.Data;
using System.IO;

namespace Stack_Solver.Services
{
    /// <summary>
    /// Provides functionality to initialize the application DB.
    /// </summary>
    /// <param name="factory">The factory used to create instances of the application's database context.</param>
    /// <param name="logger">The logger used to record informational messages and errors during database initialization.</param>
    public class DatabaseInitializer(IDbContextFactory<ApplicationDbContext> factory, ILogger<DatabaseInitializer> logger)
    {
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            logger.LogInformation("Initializing database");
            // SQLite creates the database file but not its parent directory; ensure the app-data
            // folder exists first, or MigrateAsync throws "unable to open database file" on a
            // machine where it does not yet exist (e.g. a clean install).
            AppPaths.EnsureAppData();

            try
            {
                await MigrateAsync(ct);
            }
            catch (SqliteException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                // The on-disk database has application tables but no matching migration-history
                // entry — a limbo state left by an interrupted earlier run (schema created but not
                // recorded), which migrations cannot advance over. Back the file up and rebuild from
                // scratch: the local DB only caches the user's SKU library, and the backup preserves
                // it for manual recovery.
                logger.LogWarning(ex, "Database is in an inconsistent migration state; rebuilding it (previous file backed up)");
                BackupDatabaseFile();
                await MigrateAsync(ct);
            }

            logger.LogInformation("Database initialization completed");
        }

        // Apply any pending EF Core migrations. This both creates the database on first run and
        // upgrades an existing user's database in-place on app updates, preserving data. (Do not use
        // EnsureCreated here — it cannot evolve an existing schema.)
        private async Task MigrateAsync(CancellationToken ct)
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await db.Database.MigrateAsync(ct);
        }

        // Rename the inconsistent database (and its journal files) aside so a fresh one can be built.
        private void BackupDatabaseFile()
        {
            // Release the file handle the connection pool is holding, or the move fails.
            SqliteConnection.ClearAllPools();

            string path = AppPaths.DatabaseFile;
            if (!File.Exists(path))
                return;

            string backup = Path.Combine(AppPaths.AppDataDirectory, $"stacksolver.corrupt-{DateTime.Now:yyyyMMddHHmmss}.db");
            File.Move(path, backup, overwrite: true);
            logger.LogWarning("Backed up inconsistent database to {Backup}", backup);

            // The old write-ahead-log/shared-memory files belong to the backed-up database; drop the
            // stragglers so they cannot attach to the freshly created one.
            foreach (string sidecar in new[] { path + "-wal", path + "-shm" })
                if (File.Exists(sidecar))
                    File.Delete(sidecar);
        }
    }
}
