namespace Stack_Solver.Models
{
    public abstract class SupportSurface(string name, int length, int width, int height)
    {
        public string Name { get; set; } = name;

        public int Length { get; set; } = length;
        public int Width { get; set; } = width;
        public int Height { get; set; } = height;
    }
}
