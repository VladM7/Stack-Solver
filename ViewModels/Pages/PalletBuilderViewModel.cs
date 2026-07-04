using FluentValidation;
using Microsoft.Extensions.Options;
using Stack_Solver.Data.Repositories;
using Stack_Solver.Infrastructure;
using Stack_Solver.Models;
using Stack_Solver.Models.Inputs;
using Stack_Solver.Models.Jobs;
using Stack_Solver.Services;
using Wpf.Ui;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class PalletBuilderViewModel(
        ISkuRepository skuRepository,
        IEventAggregator events,
        ILayerVisualizationService viz,
        IOptions<GenerationOptions> genOptions,
        IOptions<PalletDefaultsOptions> palletDefaults,
        IValidator<PalletSettingsDto> settingsValidator,
        IValidator<SkuQuantityDto> skuQuantityValidator,
        IUserSettingsService userSettings,
        ISnackbarService snackbarService,
        IJobRepository jobRepository) : ObservableObject
    {
        public PalletBuilderSettingsViewModel Settings { get; } = new PalletBuilderSettingsViewModel(skuRepository, events, genOptions, palletDefaults, settingsValidator, skuQuantityValidator, userSettings);
        public ResultsViewModel Results { get; } = new ResultsViewModel(events, viz, snackbarService, jobRepository);

        public async Task OnNavigatedToAsync()
        {
            await Settings.InitializeAsync();
        }

        /// <summary>
        /// Opens a saved job: mirrors the settings it ran with into the setup rail and displays its
        /// solutions in the results view. Called before navigating to this page.
        /// </summary>
        public async Task OpenJobAsync(Job job)
        {
            var settings = JobSnapshotMapper.DeserializeSettings(job.SettingsJson);
            if (settings is null) return;

            await Settings.ApplyJobAsync(settings);
            Results.DisplayJob(job, settings);
        }

        public static Task OnNavigatedFromAsync() => Task.CompletedTask;
    }
}
