namespace Stack_Solver.Models.Layering
{
    public class LayerGeometry(int width, int length)
    {
        public int Width { get; set; } = width;
        public int Length { get; set; } = length;

        public bool[,] OccupancyGrid { get; set; } = new bool[width, length];

        public List<Rect> ItemRectangles { get; set; } = [];
    }
}
