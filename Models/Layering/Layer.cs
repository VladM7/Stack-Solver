namespace Stack_Solver.Models
{
    public class Layer
    {
        private int Id { get; set; }
        private string Name { get; set; }
        private List<PositionedItem> Items { get; set; }

        private LayerMetadata Metadata { get; set; }

        public Layer(int id, string name, List<PositionedItem> items, LayerMetadata metadata)
        {
            Id = id;
            Name = name;
            Items = items;
            Metadata = metadata;
        }

        public Layer() { }
    }
}
