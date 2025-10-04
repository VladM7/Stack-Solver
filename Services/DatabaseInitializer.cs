using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stack_Solver.Data;

namespace Stack_Solver.Services
{
    public class DatabaseInitializer
    {
        private readonly IDbContextFactory<ApplicationDbContext> _factory;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(IDbContextFactory<ApplicationDbContext> factory, ILogger<DatabaseInitializer> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            await db.Database.EnsureCreatedAsync(ct);
        }
    }
}