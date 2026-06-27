using FluentValidation;
using Microsoft.Extensions.Configuration;
using Stack_Solver.Infrastructure;
using Stack_Solver.Models;
using Stack_Solver.Validation;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Registers the Stack-Solver.Core domain services: messaging, input validation
    /// and the strongly-typed options bound from configuration.
    /// </summary>
    public static class CoreServiceCollectionExtensions
    {
        public static IServiceCollection AddCore(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IEventAggregator, EventAggregator>();

            services.AddValidatorsFromAssemblyContaining<SkuInputDtoValidator>();

            services.Configure<GenerationOptions>(configuration.GetSection("LayerGeneration"));
            services.Configure<PalletDefaultsOptions>(configuration.GetSection("PalletDefaults"));

            return services;
        }
    }
}
