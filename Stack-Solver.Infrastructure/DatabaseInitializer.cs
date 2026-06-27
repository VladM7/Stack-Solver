using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stack_Solver.Data;

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
            await using var db = await factory.CreateDbContextAsync(ct);
            // Apply any pending EF Core migrations. This both creates the database on first run
            // and upgrades an existing user's database in-place on app updates, preserving data.
            // (Do not use EnsureCreated here — it cannot evolve an existing schema.)
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("Database initialization completed");
        }
    }
}