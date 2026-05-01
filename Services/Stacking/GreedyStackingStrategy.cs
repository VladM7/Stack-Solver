using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Stacking
{
    /// <summary>
    /// Builds a single pallet template by greedily stacking layers in the order provided,
    /// skipping any that would violate the pallet's height or weight limits.
    /// The caller controls quality by choosing the layer ordering.
    /// </summary>
    public class GreedyStackingStrategy : ILayerStackingStrategy
    {
        public string Name => "Greedy";

        public PalletTemplate? Build(SupportSurface pallet, IReadOnlyList<Layer> layers, GenerationOptions options)
        {
            if (layers.Count == 0)
                return null;

            var stackedLayers = new List<Layer>();
            double usedHeight = pallet.Height;
            double usedWeight = 0;

            int maxHeight = pallet is Pallet p ? p.MaxStackHeight : int.MaxValue;
            int maxWeight = pallet is Pallet p2 ? p2.MaxStackWeight : int.MaxValue;

            foreach (var layer in layers)
            {
                double layerHeight = layer.Metadata.Height;
                double layerWeight = layer.Metrics.TotalWeight;

                if (usedHeight + layerHeight > maxHeight)
                    continue;
                if (usedWeight + layerWeight > maxWeight)
                    continue;

                stackedLayers.Add(layer);
                usedHeight += layerHeight;
                usedWeight += layerWeight;
            }

            return stackedLayers.Count > 0 ? PalletTemplate.FromLayers(stackedLayers) : null;
        }
    }
}
