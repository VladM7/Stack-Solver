using Stack_Solver.Models.Metadata;

namespace Stack_Solver.Models.Layering
{
    /// <summary>
    /// A single layer in a stacking solution, containing positioned items, metadata and geometry information.
    /// </summary>
    public class Layer(string name, List<PositionedItem> items, LayerMetadata metadata)
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = name;
        public List<PositionedItem> Items { get; set; } = items;

        public LayerMetadata Metadata { get; set; } = metadata;

        public LayerGeometry? Geometry { get; set; } = null;

        public LayerMetrics Metrics { get; set; } = new();

        public override string ToString()
        {
            return $"{Name} ({Id})\n\n{Metadata}";
        }
    }
}
