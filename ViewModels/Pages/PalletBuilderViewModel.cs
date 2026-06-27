using FluentValidation;
using Microsoft.Extensions.Options;
using Stack_Solver.Data.Repositories;
using Stack_Solver.Infrastructure;
using Stack_Solver.Models;
using Stack_Solver.Models.Inputs;
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
        ISnackbarService snackbarService) : ObservableObject
    {
        public PalletBuilderSettingsViewModel Settings { get; } = new PalletBuilderSettingsViewModel(skuRepository, events, genOptions, palletDefaults, settingsValidator, skuQuantityValidator, userSettings);
        public ResultsViewModel Results { get; } = new ResultsViewModel(events, viz, snackbarService);

        public async Task OnNavigatedToAsync()
        {
            await Settings.InitializeAsync();
        }

        public static Task OnNavigatedFromAsync() => Task.CompletedTask;
    }
}
