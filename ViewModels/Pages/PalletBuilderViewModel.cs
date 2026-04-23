using FluentValidation;
using Microsoft.Extensions.Options;
using Stack_Solver.Data.Repositories;
using Stack_Solver.Infrastructure;
using Stack_Solver.Models;
using Stack_Solver.Models.Inputs;
using Stack_Solver.Services;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class PalletBuilderViewModel(
        ISkuRepository skuRepository,
        IEventAggregator events,
        ILayerVisualizationService viz,
        IOptions<GenerationOptions> genOptions,
        IOptions<PalletDefaultsOptions> palletDefaults,
        IValidator<PalletSettingsDto> settingsValidator,
        IValidator<SkuQuantityDto> skuQuantityValidator) : ObservableObject
    {
        public PalletBuilderSettingsViewModel Settings { get; } = new PalletBuilderSettingsViewModel(skuRepository, events, genOptions, palletDefaults, settingsValidator, skuQuantityValidator);
        public LayerAnalyzerViewModel LayerAnalyzer { get; } = new LayerAnalyzerViewModel(events, viz);
        public PalletAnalyzerViewModel PalletAnalyzer { get; } = new PalletAnalyzerViewModel(events);

        public async Task OnNavigatedToAsync()
        {
            await Settings.InitializeAsync();
        }

        public static Task OnNavigatedFromAsync() => Task.CompletedTask;
    }
}
