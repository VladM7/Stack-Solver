using Google.OrTools.Sat;
using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Layering
{
    public class CPSATGenerationStrategy : ILayerGenerationStrategy
    {
        public string Name => "CP-SAT";

        public List<Layer> Generate(List<SKU> skus, SupportSurface supportSurface, GenerationOptions options)
        {
            if (skus == null || skus.Count == 0)
                return [];

            int px = supportSurface.Length;
            int py = supportSurface.Width;
            int gridStep = ComputeGridStep(skus);
            int maxTime = options.MaxSolverTime > 0 ? options.MaxSolverTime : 60;
            int maxCandidates = options.MaxCandidates > 0 ? options.MaxCandidates : 2000;

            var candidates = new List<(int skuIndex, string skuId, int x, int y, int w, int h, bool rotated)>();
            for (int si = 0; si < skus.Count; si++)
            {
                var s = skus[si];
                var orientations = new List<(int w, int h, bool rot)>
                {
                    (s.Length, s.Width, false)
                };

                if (s.Rotatable && s.Length != s.Width)
                    orientations.Add((s.Width, s.Length, true));

                foreach (var (bw, bh, rotated) in orientations)
                {
                    for (int x = 0; x <= px - bw; x += gridStep)
                    {
                        for (int y = 0; y <= py - bh; y += gridStep)
                        {
                            candidates.Add((si, s.SkuId, x, y, bw, bh, rotated));
                        }
                    }
                }
            }

            if (candidates.Count == 0)
                return [];

            if (candidates.Count > maxCandidates)
            {
                var rnd = new Random();
                candidates = [.. candidates.OrderBy(_ => rnd.Next()).Take(maxCandidates)];
            }

            var model = new CpModel();
            var use = candidates.Select((_, i) => model.NewBoolVar($"use_{i}")).ToList();

            for (int i = 0; i < candidates.Count; i++)
            {
                var (si, sid_i, xi, yi, wi, hi, _) = candidates[i];
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    var (sj, sid_j, xj, yj, wj, hj, _) = candidates[j];
                    bool canOverlap = !(xi + wi <= xj || xj + wj <= xi || yi + hi <= yj || yj + hj <= yi);
                    if (canOverlap)
                        model.Add(use[i] + use[j] <= 1);
                }
            }

            for (int si = 0; si < skus.Count; si++)
            {
                var sku = skus[si];
                var idxs = candidates
                    .Select((c, idx) => (c, idx))
                    .Where(x => x.c.skuIndex == si)
                    .Select(x => x.idx)
                    .ToList();

                if (idxs.Count > 0)
                    model.Add(LinearExpr.Sum(idxs.Select(idx => use[idx])) <= sku.Quantity);
            }

            model.Maximize(LinearExpr.Sum(use));

            var solver = new CpSolver
            {
                StringParameters = $"max_time_in_seconds:{maxTime},num_search_workers:8"
            };

            var status = solver.Solve(model);
            if (status != CpSolverStatus.Optimal && status != CpSolverStatus.Feasible)
                return [];

            var placements = new List<PositionedItem>();
            var counts = skus.ToDictionary(s => s.SkuId, _ => 0);

            for (int i = 0; i < candidates.Count; i++)
            {
                if (solver.Value(use[i]) == 1)
                {
                    var (si, sid, x, y, w, h, rotated) = candidates[i];
                    var sku = skus[si];
                    placements.Add(new PositionedItem(sku, x, y, rotated));
                    counts[sid]++;
                }
            }

            int totalBoxes = counts.Values.Sum();
            if (totalBoxes == 0)
                return [];

            double usedArea = placements.Sum(p => p.SkuType.Length * p.SkuType.Width);
            double util = usedArea / (px * py);
            int layerHeight = placements.Count != 0 ? placements.Max(p => p.SkuType.Height) : 0;

            var desc = $"CP-SAT multi-layer (boxes={totalBoxes}, util={util:F3})";

            var metadata = new LayerMetadata(util, layerHeight, desc);
            var layer = new Layer("CPSAT", placements, metadata);
            layer.Geometry = LayerGeometryBuilder.Build(layer, supportSurface);

            return [layer];
        }

        private static int ComputeGridStep(List<SKU> skus)
        {
            var dims = new List<int>();
            foreach (var s in skus)
            {
                dims.Add(s.Length);
                dims.Add(s.Width);
            }

            int gcd = dims.Aggregate((a, b) => Gcd(a, b));
            return Math.Max(gcd, 1);
        }

        private static int Gcd(int a, int b)
        {
            while (b != 0)
            {
                int t = b;
                b = a % b;
                a = t;
            }
            return a;
        }
    }
}
