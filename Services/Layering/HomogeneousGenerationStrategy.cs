using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Layering
{
    public class HomogeneousGenerationStrategy : ILayerGenerationStrategy
    {
        public string Name => "Homogeneous Grid";

        public List<Layer> Generate(List<SKU> skus, SupportSurface supportSurface, GenerationOptions options)
        {
            var layers = new List<Layer>();

            int px = supportSurface.Length;
            int py = supportSurface.Width;
            double area = px * py;

            foreach (var s in skus)
            {
                var orientations = new List<(int bw, int bh, string desc)>
                {
                    (s.Length, s.Width, "normal")
                };

                if (s.Rotatable && s.Length != s.Width)
                    orientations.Add((s.Width, s.Length, "rotated"));

                foreach (var (bw, bh, desc) in orientations)
                {
                    int nx = px / bw;
                    int ny = py / bh;
                    int count = nx * ny;

                    if (count <= 0)
                        continue;

                    double usedArea = count * bw * bh;
                    var placements = new List<PositionedItem>();

                    for (int ix = 0; ix < nx; ix++)
                    {
                        for (int iy = 0; iy < ny; iy++)
                        {
                            int x = ix * bw;
                            int y = iy * bh;
                            bool rotated = (desc == "rotated");
                            placements.Add(new PositionedItem(s, x, y, rotated));
                        }
                    }

                    double utilization = usedArea / area;
                    string description = $"homogeneous {s.Name} ({desc}) {nx}x{ny}";
                    int height = s.Height;

                    var metadata = new LayerMetadata(utilization, height, description);

                    var layer = new Layer($"hom_grid_{s.Name.Replace(' ', '_')}_{desc}", placements, metadata);
                    layer.Geometry = LayerGeometryBuilder.Build(layer, supportSurface);

                    layers.Add(layer);
                }
            }

            return layers;
        }
    }
}
