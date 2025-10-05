namespace Stack_Solver.Models
{
    public class LayerMetadata(double utilization, int height, string description)
    {
        public double Utilization { get; set; } = utilization;
        public int Height { get; set; } = height;
        public string Description { get; set; } = description;

        public override string ToString()
        {
            return $"Utilization: {Utilization:P3}\nHeight: {Height}\nDesc: {Description}";
        }
    }
}
