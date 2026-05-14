using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Stack_Solver.Data;
using Stack_Solver.Data.Repositories;
using Stack_Solver.Infrastructure;
using Stack_Solver.Models;
using Stack_Solver.Models.Inputs;
using Stack_Solver.Services;
using Stack_Solver.Validation;
using Stack_Solver.ViewModels.Pages;
using Stack_Solver.ViewModels.Windows;
using Stack_Solver.Views.Pages;
using Stack_Solver.Views.Windows;
using System.IO;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace Stack_Solver
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        // The.NET Generic Host provides dependency injection, configuration, logging, and other services.
        // https://docs.microsoft.com/dotnet/core/extensions/generic-host
        // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
        // https://docs.microsoft.com/dotnet/core/extensions/configuration
        // https://docs.microsoft.com/dotnet/core/extensions/logging
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .UseSerilog((context, services, loggerConfiguration) =>
            {
                loggerConfiguration.ReadFrom.Configuration(context.Configuration);
            })
            .ConfigureAppConfiguration(c =>
            {
                c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory)!);
                c.AddJsonFile("defaults.json", optional: true, reloadOnChange: false);
                c.AddJsonFile(AppPaths.UserSettingsFile, optional: true, reloadOnChange: false);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddNavigationViewPageProvider();

                services.AddHostedService<ApplicationHostService>();

                // Theme manipulation
                services.AddSingleton<IThemeService, ThemeService>();

                // TaskBar manipulation
                services.AddSingleton<ITaskBarService, TaskBarService>();

                // Service containing navigation, same as INavigationWindow... but without window
                services.AddSingleton<INavigationService, NavigationService>();

                services.AddSingleton<ISnackbarService, SnackbarService>();

                // Main window with navigation
                services.AddSingleton<INavigationWindow, MainWindow>();
                services.AddSingleton<MainWindowViewModel>();

                services.AddSingleton<DashboardPage>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<SKULibraryPage>();
                services.AddSingleton<SKULibraryViewModel>();
                services.AddSingleton<PalletBuilderPage>();
                services.AddSingleton<IUserSettingsService, UserSettingsService>();

                services.AddSingleton<PalletBuilderViewModel>(sp => new PalletBuilderViewModel(
                    sp.GetRequiredService<ISkuRepository>(),
                    sp.GetRequiredService<IEventAggregator>(),
                    sp.GetRequiredService<ILayerVisualizationService>(),
                    sp.GetRequiredService<IOptions<GenerationOptions>>(),
                    sp.GetRequiredService<IOptions<PalletDefaultsOptions>>(),
                    sp.GetRequiredService<IValidator<PalletSettingsDto>>(),
                    sp.GetRequiredService<IValidator<SkuQuantityDto>>(),
                    sp.GetRequiredService<IUserSettingsService>(),
                    sp.GetRequiredService<ISnackbarService>()));
                services.AddSingleton<TruckLoadingPage>();
                services.AddSingleton<TruckLoadingViewModel>();
                services.AddSingleton<JobManagerPage>();
                services.AddSingleton<JobManagerViewModel>();

                services.AddSingleton<IEventAggregator, EventAggregator>();
                services.AddSingleton<ILayerVisualizationService, LayerVisualizationService>();

                services.AddValidatorsFromAssemblyContaining<SkuInputDtoValidator>();

                services.AddDbContextFactory<ApplicationDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={AppPaths.DatabaseFile}");
                });

                services.AddSingleton<ISkuRepository, SkuRepository>();
                services.AddSingleton<DatabaseInitializer>();

                // Bind options from host configuration
                services.Configure<GenerationOptions>(context.Configuration.GetSection("LayerGeneration"));
                services.Configure<PalletDefaultsOptions>(context.Configuration.GetSection("PalletDefaults"));
            }).Build();

        /// <summary>
        /// Gets services.
        /// </summary>
        public static IServiceProvider Services
        {
            get { return _host.Services; }
        }

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            Log.Information("Application starting");

            await _host.StartAsync();
            Log.Information("Host started");

            // Initialize database
            var dbInit = Services.GetRequiredService<DatabaseInitializer>();
            await dbInit.InitializeAsync();
            Log.Information("Database initialized");
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            Log.Information("Application shutting down");
            await _host.StopAsync();
            Log.Information("Host stopped");

            _host.Dispose();
            Log.CloseAndFlush();
        }


        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unhandled dispatcher exception");
        }
    }
}
