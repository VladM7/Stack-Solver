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
            await using var db = await factory.CreateDbContextAsync(ct);
            await db.Database.EnsureCreatedAsync(ct);
        }
    }
}