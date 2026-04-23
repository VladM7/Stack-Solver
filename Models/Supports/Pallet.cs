using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;

namespace Stack_Solver.Models.Supports
{
    /// <summary>
    /// Represents a pallet that provides a surface for stacking multiple layers of items.
    /// </summary>
    /// <param name="name">The unique identifier for the pallet, used to distinguish it from other pallets.</param>
    /// <param name="length">The length of the pallet.</param>
    /// <param name="width">The width of the pallet.</param>
    /// <param name="height">The height of the pallet.</param>
    public class Pallet(string name, int length, int width, int height) : SupportSurface(name, length, width, height)
    {
        List<Layer> Layers { get; } = [];

        PalletMetadata Metadata { get; set; } = new();
    }
}
