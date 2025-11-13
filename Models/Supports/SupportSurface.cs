namespace Stack_Solver.Models.Supports
{
    public abstract class SupportSurface(string name, int length, int width, int height)
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = name;

        public int Length { get; set; } = length;
        public int Width { get; set; } = width;
        public int Height { get; set; } = height;
    }
}
