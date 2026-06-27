using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Stack_Solver.Data
{
    /// <summary>
    /// Used only by the EF Core tools at design time (e.g. <c>dotnet ef migrations add</c>).
    /// Having this factory lets the tooling create the context without spinning up the
    /// WPF application host.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            // No SQLitePCLRaw bundle is referenced, so the provider must be registered
            // explicitly here just as the app does at startup.
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={AppPaths.DatabaseFile}")
                .Options;

            return new ApplicationDbContext(options);
        }
    }
}
