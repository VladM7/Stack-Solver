using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services
{
    /// <summary>
    /// Defines a strategy for generating layers based on a collection of SKUs, a support surface, and generation options.
    /// </summary>
    /// <remarks>Implementations of this interface should provide specific logic for generating layers.</remarks>
    public interface ILayerGenerationStrategy
    {
        string Name { get; }
        List<Layer> Generate(List<SKU> skus, SupportSurface supportSurface, GenerationOptions options);
    }
}
