using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Layering
{
    public class BLFGenerationStrategy : ILayerGenerationStrategy
    {
        public string Name => "BLF";

        public List<Layer> Generate(List<SKU> skus, SupportSurface supportSurface, GenerationOptions options)
        {
            var px = supportSurface.Length;
            var py = supportSurface.Width;
            int attempts = 200; // to be replaced with a value from generation options
            int seed = Environment.TickCount;

            var rand = new Random(seed);

            double area = px * py;

            var variants = new List<(string skuId, int w, int h, bool Rotated, SKU refSku)>();
            foreach (var s in skus)
            {
                variants.Add((s.SkuId, s.Length, s.Width, false, s));
                if (s.Rotatable && s.Length != s.Width)
                    variants.Add((s.SkuId, s.Width, s.Length, true, s));
            }

            var foundLayers = new Dictionary<string, Layer>();

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var order = variants.OrderBy(_ => rand.Next()).ToList();
                var placements = new List<PositionedItem>();
                var counts = skus.ToDictionary(s => s.SkuId, _ => 0);
                int y = 0;

                while (true)
                {
                    var startVar = order.FirstOrDefault(v => v.h <= py - y && v.w <= px);
                    if (startVar == default)
                        break;

                    int rowH = startVar.h;
                    int x = 0;

                    while (true)
                    {
                        bool placedAny = false;
                        foreach (var (skuId, w, h, Rotated, refSku) in order)
                        {
                            if (h <= rowH && w <= px - x)
                            {
                                var skuObj = skus.First(s => s.SkuId == skuId);
                                if (counts[skuId] + 1 > skuObj.Quantity)
                                    continue;

                                placements.Add(new PositionedItem(refSku, x, y, Rotated));

                                counts[skuId]++;
                                x += w;
                                placedAny = true;
                                break;
                            }
                        }
                        if (!placedAny) break;
                    }

                    y += rowH;
                    if (y >= py)
                        break;
                }

                int boxes = counts.Values.Sum();
                if (boxes == 0)
                    continue;

                double usedArea = placements.Sum(p => p.SkuType.Length * p.SkuType.Width);
                double util = usedArea / area;
                var usedSkus = skus.Where(s => counts[s.SkuId] > 0).ToList();
                int layerHeight = usedSkus.Count != 0 ? usedSkus.Max(s => s.Height) : 0;

                var key = string.Join(",", counts.Where(kv => kv.Value > 0)
                                                 .OrderBy(kv => kv.Key)
                                                 .Select(kv => $"{kv.Key}:{kv.Value}"));

                if (!foundLayers.TryGetValue(key, out Layer? value) || value.Metadata.Utilization < util)
                {
                    value = new Layer($"blf_attempt_{attempt}", placements, new LayerMetadata(util, layerHeight, $"BLF attempt {attempt}, boxes={boxes}, util={util:F3}"));
                    value.Geometry = LayerGeometryBuilder.Build(value, supportSurface);
                    foundLayers[key] = value;
                }
            }

            return [.. foundLayers.Values];
        }
    }
}
