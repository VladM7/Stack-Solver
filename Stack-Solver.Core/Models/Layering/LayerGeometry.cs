namespace Stack_Solver.Models.Layering
{
    /// <summary>
    /// Represents the geometric configuration of a layer, including its dimensions, occupancy grid, and item placement
    /// information.
    /// </summary>
    /// <param name="width">The number of columns in the layer's grid. Must be a positive integer.</param>
    /// <param name="length">The number of rows in the layer's grid. Must be a positive integer.</param>
    public class LayerGeometry(int width, int length)
    {
        public int Width { get; } = width;
        public int Length { get; } = length;
        public int GridStep { get; set; } = 1;

        public bool[,] OccupancyGrid { get; } = new bool[width, length];
        public int[,] ItemIndexGrid { get; } = CreateItemIndexGrid(width, length);

        public List<RectD> ItemRectangles { get; } = [];

        private static int[,] CreateItemIndexGrid(int width, int length)
        {
            var grid = new int[width, length];
            for (int y = 0; y < width; y++)
            {
                for (int x = 0; x < length; x++)
                {
                    grid[y, x] = -1;
                }
            }
            return grid;
        }
    }
}
