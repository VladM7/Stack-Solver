using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.Layering;

namespace Stack_Solver.Services
{
    public static class LayerGenerator
    {
        public static List<Layer> Generate(
            List<SKU> skus,
            SupportSurface pallet,
            GenerationOptions options,
            bool useCpsat = false,
            CancellationToken ct = default)
        {
            var strategies = new List<ILayerGenerationStrategy>
            {
                new BLFGenerationStrategy(),
                new HomogeneousGenerationStrategy(),
                new StripFillGenerationStrategy(),
                new RadialPlacementGenerationStrategy()
            };
            if (useCpsat)
                strategies.Add(new CPSATGenerationStrategy());

            var aggregate = new List<Layer>();
            foreach (var strat in strategies)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var produced = strat.Generate(skus, pallet, options);
                    if (produced?.Count > 0)
                        aggregate.AddRange(produced);
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }

            var valid = aggregate
                .Where(l => l?.Metadata != null &&
                    !double.IsNaN(l.Metadata.Utilization) &&
                    !double.IsInfinity(l.Metadata.Utilization) &&
                    l.Metadata.Utilization > 0.0 &&
                    l.Metadata.Utilization <= 1.0)
                .ToList();

            foreach (var layer in valid)
            {
                LayerGeometryOptimizer.CenterLayer(layer);
                // Centering shifts item positions, so geometry grids and stability metrics must be recomputed.
                layer.Geometry = LayerGeometryBuilder.Build(layer, pallet);
                layer.Metrics = LayerMetricsCalculator.Compute(layer, pallet);
            }

            return valid;
        }
    }
}
