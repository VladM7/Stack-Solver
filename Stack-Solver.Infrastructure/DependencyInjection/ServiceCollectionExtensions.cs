using Microsoft.EntityFrameworkCore;
using Stack_Solver.Data;
using Stack_Solver.Data.Repositories;
using Stack_Solver.Infrastructure;
using Stack_Solver.Services;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Registers the Stack-Solver.Infrastructure services: the SQLite-backed
    /// database context, repositories and persistence helpers.
    /// </summary>
    public static class InfrastructureServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddDbContextFactory<ApplicationDbContext>(options =>
            {
                options.UseSqlite($"Data Source={AppPaths.DatabaseFile}");
            });

            services.AddSingleton<ISkuRepository, SkuRepository>();
            services.AddSingleton<DatabaseInitializer>();
            services.AddSingleton<IUserSettingsService, UserSettingsService>();

            return services;
        }
    }
}
