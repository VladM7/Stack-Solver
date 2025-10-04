using Stack_Solver.Models;

namespace Stack_Solver.Services
{
    public interface ILayerGenerationStrategy
    {
        string Name { get; }
        List<Layer> Generate(List<SKU> skus, GenerationOptions options);
    }
}
