using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Layering
{
    public class StripFillGenerationStrategy : ILayerGenerationStrategy
    {
        public string Name => "Strip-Fill";

        public List<Layer> Generate(List<SKU> skus, SupportSurface supportSurface, GenerationOptions options)
        {
            var layers = new List<Layer>();
            int px = supportSurface.Length;
            int py = supportSurface.Width;
            double area = px * py;

            int maxRows = 50;
            int randomSequences = 50;
            int seed = Environment.TickCount;
            var rand = new Random(seed);

            var variants = new List<(string sid, int w, int h, SKU sref)>();
            foreach (var s in skus)
            {
                variants.Add((s.SkuId, s.Length, s.Width, s));
                if (s.Rotatable && s.Length != s.Width)
                    variants.Add((s.SkuId, s.Width, s.Length, s));
            }

            for (int nrows = 1; nrows <= maxRows; nrows++)
            {
                var candidateSequences = new List<List<(string sid, int w, int h, SKU sref)>>();

                foreach (var v in variants)
                {
                    candidateSequences.Add([.. Enumerable.Repeat(v, nrows)]);
                }

                if (variants.Count >= 2)
                {
                    for (int i = 0; i < variants.Count; i++)
                    {
                        for (int j = i + 1; j < variants.Count; j++)
                        {
                            var seq = new List<(string sid, int w, int h, SKU sref)>();
                            for (int k = 0; k < nrows; k++)
                                seq.Add(k % 2 == 0 ? variants[i] : variants[j]);
                            candidateSequences.Add(seq);
                        }
                    }
                }

                for (int r = 0; r < randomSequences; r++)
                {
                    var seq = new List<(string sid, int w, int h, SKU sref)>();
                    for (int k = 0; k < nrows; k++)
                        seq.Add(variants[rand.Next(variants.Count)]);
                    candidateSequences.Add(seq);
                }

                foreach (var seq in candidateSequences)
                {
                    int totalRowHeight = seq.Sum(v => v.h);
                    if (totalRowHeight > py)
                        continue;

                    var itemCounts = new Dictionary<string, int>();
                    var placements = new List<PositionedItem>();
                    double usedArea = 0;
                    int boxes = 0;
                    int yOffset = 0;
                    bool feasible = true;

                    foreach (var (sid, w, h, sref) in seq)
                    {
                        int nx = px / w;
                        if (nx <= 0)
                        {
                            feasible = false;
                            break;
                        }

                        for (int ix = 0; ix < nx; ix++)
                        {
                            placements.Add(new PositionedItem(sref, ix * w, yOffset, sref.Length != w));
                        }

                        int count = nx;
                        if (itemCounts.ContainsKey(sid))
                            itemCounts[sid] += count;
                        else
                            itemCounts[sid] = count;

                        usedArea += count * w * h;
                        boxes += count;
                        yOffset += h;
                    }

                    if (!feasible)
                        continue;

                    double util = usedArea / area;
                    string desc = $"rows={nrows} seq=" + string.Join(",", seq.Select(v => $"{v.sref.Name}:{v.w}x{v.h}"));
                    string lid = $"strip_r{nrows}";

                    int layerHeight = seq.Count != 0 ? seq.Max(v => v.sref.Height) : 0;
                    var metadata = new LayerMetadata(util, layerHeight, desc);

                    var layer = new Layer(lid, placements, metadata);
                    layer.Geometry = LayerGeometryBuilder.Build(layer, supportSurface);

                    layers.Add(layer);
                }
            }

            var unique = new Dictionary<string, Layer>();
            foreach (var layer in layers)
            {
                var key = string.Join(",", layer.Items
                    .GroupBy(i => i.SkuType.SkuId)
                    .OrderBy(g => g.Key)
                    .Select(g => $"{g.Key}:{g.Count()}"));

                if (!unique.TryGetValue(key, out Layer? value) || value.Metadata.Utilization < layer.Metadata.Utilization)
                    unique[key] = layer;
            }

            return [.. unique.Values];
        }
    }
}
