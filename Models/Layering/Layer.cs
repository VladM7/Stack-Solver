namespace Stack_Solver.Models
{
    public class Layer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<PositionedItem> Items { get; set; }

        public LayerMetadata Metadata { get; set; }

        public Layer(int id, string name, List<PositionedItem> items, LayerMetadata metadata)
        {
            Id = id;
            Name = name;
            Items = items;
            Metadata = metadata;
        }

        public Layer() { }

        public override string ToString()
        {
            return $"{Name} ({Id})\n\n{Metadata}";
        }
    }
}
