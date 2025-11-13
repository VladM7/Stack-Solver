using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;

namespace Stack_Solver.Models.Supports
{
    public class Pallet(string name, int length, int width, int height) : SupportSurface(name, length, width, height)
    {
        List<Layer> Layers { get; } = [];

        PalletMetadata Metadata { get; set; } = new();
    }
}
