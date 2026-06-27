namespace Stack_Solver.Models.Supports
{
    /// <summary>
    /// Represents a generic support surface used for stacking cargo.
    /// </summary>
    /// <param name="name">The name of the support surface.</param>
    /// <param name="length">The length of the support surface.</param>
    /// <param name="width">The width of the support surface.</param>
    /// <param name="height">The height of the support surface (without the cargo).</param>
    public abstract class SupportSurface(string name, int length, int width, int height)
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = name;

        public int Length { get; set; } = length;
        public int Width { get; set; } = width;
        public int Height { get; set; } = height;
    }
}
