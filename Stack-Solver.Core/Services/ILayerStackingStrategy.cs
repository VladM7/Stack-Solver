using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services
{
    public interface ILayerStackingStrategy
    {
        string Name { get; }
        PalletTemplate? Build(SupportSurface pallet, IReadOnlyList<Layer> layers, GenerationOptions options);
    }
}
