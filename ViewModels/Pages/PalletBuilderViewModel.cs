using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Options;
using Stack_Solver.Data.Repositories;
using Stack_Solver.Infrastructure;
using Stack_Solver.Models;
using Stack_Solver.Services;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class PalletBuilderViewModel : ObservableObject
    {
        public PalletBuilderSettingsViewModel Settings { get; }
        public LayerAnalyzerViewModel LayerAnalyzer { get; }
        public PalletAnalyzerViewModel PalletAnalyzer { get; }

        public PalletBuilderViewModel(ISkuRepository skuRepository, IEventAggregator events, ILayerVisualizationService viz, IOptions<GenerationOptions> genOptions, IOptions<PalletDefaultsOptions> palletDefaults)
        {
            Settings = new PalletBuilderSettingsViewModel(skuRepository, events, genOptions, palletDefaults);
            LayerAnalyzer = new LayerAnalyzerViewModel(events, viz);
            PalletAnalyzer = new PalletAnalyzerViewModel(events);
        }

        public async Task OnNavigatedToAsync()
        {
            await Settings.InitializeAsync();
        }

        public static Task OnNavigatedFromAsync() => Task.CompletedTask;
    }
}
