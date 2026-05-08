using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Stacking
{
    /// <summary>
    /// Builds a single pallet template by greedily stacking layers in the order provided,
    /// skipping any layer that violates height, weight, or inter-layer support constraints.
    /// The first layer always sits on the flat pallet surface (no support check needed).
    /// Each subsequent layer is checked via LayerSupportAnalyzer: any item whose unsupported
    /// footprint area exceeds MaxSkuOverhang causes the layer to be skipped.
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
            double maxSkuOverhang = pallet is Pallet p3 ? p3.MaxSkuOverhang : 0;

            // After each successful placement, restart from the top of the ordered list.
            // This lets the same layer pattern repeat as many times as the height/weight
            // limits allow (e.g. five identical homogeneous layers stacked on each other).
            bool anyAdded;
            do
            {
                anyAdded = false;
                foreach (var layer in layers)
                {
                    if (usedHeight + layer.Metadata.Height > maxHeight) continue;
                    if (usedWeight + layer.Metrics.TotalWeight > maxWeight) continue;

                    // First layer sits on the flat pallet surface — always supported.
                    // Every subsequent layer must be supported by the current top layer.
                    // A layer trivially supports itself (identical item positions), so
                    // same-pattern repetition always passes this check with MaxSkuOverhang=0.
                    if (stackedLayers.Count > 0)
                    {
                        var support = LayerSupportAnalyzer.Analyze(stackedLayers[^1], layer, pallet);
                        if (support.MaximumSkuOverhangArea > maxSkuOverhang) continue;
                    }

                    stackedLayers.Add(layer);
                    usedHeight += layer.Metadata.Height;
                    usedWeight += layer.Metrics.TotalWeight;
                    anyAdded = true;
                    break;
                }
            } while (anyAdded);

            return stackedLayers.Count > 0 ? PalletTemplate.FromLayers(stackedLayers) : null;
        }
    }
}
