using Stack_Solver.Data.Repositories;
using Stack_Solver.Infrastructure;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class PalletBuilderViewModel(ISkuRepository skuRepository, IEventAggregator events) : ObservableObject
    {
        public PalletBuilderSettingsViewModel Settings { get; } = new PalletBuilderSettingsViewModel(skuRepository, events);
        public LayerAnalyzerViewModel LayerAnalyzer { get; } = new LayerAnalyzerViewModel(events);
        public PalletAnalyzerViewModel PalletAnalyzer { get; } = new PalletAnalyzerViewModel(events);

        public async Task OnNavigatedToAsync()
        {
            await Settings.InitializeAsync();
        }

        public static Task OnNavigatedFromAsync() => Task.CompletedTask;
    }
}
