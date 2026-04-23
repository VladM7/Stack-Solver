namespace Stack_Solver.Models.Inputs
{
    public class SkuInputDto
    {
        public required string Name { get; set; }
        public int Length { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Weight { get; set; }
        public bool Rotatable { get; set; }
        public string? Notes { get; set; }
    }
}
