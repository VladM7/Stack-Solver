using Stack_Solver.Models;

namespace Stack_Solver.Services.Strategies
{
    public class BLFGenerationStrategy : ILayerGenerationStrategy
    {
        public string Name => "BLF";

        public List<Layer> Generate(List<SKU> skus, GenerationOptions options)
        {
            // Implementation
            return new List<Layer>();
        }
    }
}
