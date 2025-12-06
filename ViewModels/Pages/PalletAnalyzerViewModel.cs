using Stack_Solver.Infrastructure;
using Stack_Solver.Models.Layering;
using System.Collections.ObjectModel;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class PalletAnalyzerViewModel : ObservableObject
    {
        private readonly IEventAggregator _events;

        [ObservableProperty]
        private string _outputText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Layer> _candidateLayers = [];

        public PalletAnalyzerViewModel(IEventAggregator events)
        {
            _events = events;
            _events.Subscribe<LayersGeneratedMessage>(OnLayersGenerated);
        }

        private void OnLayersGenerated(LayersGeneratedMessage msg)
        {
            CandidateLayers.Clear();
            foreach (var l in msg.Layers)
                CandidateLayers.Add(l);

            OutputText = $"{CandidateLayers.Count} candidate layers received.";
        }
    }
}
