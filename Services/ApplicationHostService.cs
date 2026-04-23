using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stack_Solver.Views.Windows;
using Wpf.Ui;

namespace Stack_Solver.Services
{
    /// <summary>
    /// Managed host of the application.
    /// </summary>
    public class ApplicationHostService(IServiceProvider serviceProvider, ILogger<ApplicationHostService> logger) : IHostedService
    {
        private INavigationWindow? _navigationWindow;

        /// <summary>
        /// Triggered when the application host is ready to start the service.
        /// </summary>
        /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting application host service");
            await HandleActivationAsync();
            logger.LogInformation("Application host service started");
        }

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// </summary>
        /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping application host service");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Creates main window during activation.
        /// </summary>
        private async Task HandleActivationAsync()
        {
            if (!Application.Current.Windows.OfType<MainWindow>().Any())
            {
                logger.LogDebug("Creating main navigation window");
                _navigationWindow = (
                    serviceProvider.GetService(typeof(INavigationWindow)) as INavigationWindow
                )!;
                _navigationWindow!.ShowWindow();

                _navigationWindow.Navigate(typeof(Views.Pages.DashboardPage));
                logger.LogDebug("Main navigation window shown and dashboard navigated");
            }

            await Task.CompletedTask;
        }
    }
}
