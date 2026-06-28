using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Stacking
{
    /// <summary>
    /// Builds a single pallet template by greedily stacking layers in the order provided,
    /// skipping any layer that violates height, weight, or inter-layer support constraints.
    /// The first layer always sits on the flat pallet surface (no support check needed).
    /// Each subsequent layer is positioned via LayerSupportAnalyzer.FindBestPlacement, which
    /// shifts it onto the support below; a layer is skipped only when no on-pallet shift keeps
    /// every SKU's overhang within MaxSkuOverhang. The chosen shift is baked into the stored
    /// layer so the placement is the one actually rendered.
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
                    // Every subsequent layer is shifted onto the current top layer; if no
                    // on-pallet shift keeps it supported, skip it. A layer trivially supports
                    // itself, so same-pattern repetition always passes with offset (0,0).
                    Layer toPlace = layer;
                    if (stackedLayers.Count > 0)
                    {
                        var fit = LayerSupportAnalyzer.FindBestPlacement(stackedLayers[^1], layer, pallet, maxSkuOverhang);
                        if (!fit.Feasible) continue;
                        if (fit.OffsetX != 0 || fit.OffsetY != 0)
                            toPlace = StackMaterializer.Translate(layer, fit.OffsetX, fit.OffsetY, pallet);
                    }

                    stackedLayers.Add(toPlace);
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
