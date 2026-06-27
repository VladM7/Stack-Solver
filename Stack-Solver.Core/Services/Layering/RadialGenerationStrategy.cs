using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Layering
{
    public class RadialPlacementGenerationStrategy : ILayerGenerationStrategy
    {
        public string Name => "Radial Placement";

        public List<Layer> Generate(List<SKU> skus, SupportSurface supportSurface, GenerationOptions options)
        {
            // to implement
            return [];
        }
    }
}
