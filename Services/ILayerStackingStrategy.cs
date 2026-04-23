using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services
{
    /// <summary>
    /// Defines a strategy for arranging layers on a support surface.
    /// </summary>
    /// <remarks>Implementations of this interface should provide specific logic for stacking layers according
    /// to the supplied options and input layers.</remarks>
    public interface ILayerStackingStrategy
    {
        string Name { get; }
        SupportSurface Build(SupportSurface pallet, List<Layer> layers, GenerationOptions options);
    }
}
