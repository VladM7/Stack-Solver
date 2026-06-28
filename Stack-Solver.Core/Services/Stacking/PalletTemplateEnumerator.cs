using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Stacking
{
    /// <summary>
    /// Generates a pool of pallet templates from a filtered layer set in three classes:
    /// (1) pure homogeneous chains, (2) stacked-homogeneous pairs,
    /// (3) mixed-layer combinations
    /// </summary>
    public static class PalletTemplateEnumerator
    {
        private const int MaxDistinctSkusPerTemplate = 3;
        private const int MaxLayersPerTemplate = 6;

        public static List<PalletTemplate> Enumerate(
            Pallet pallet,
            IReadOnlyList<Layer> layers)
        {
            if (layers.Count == 0)
                return [];

            var templates = new List<PalletTemplate>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var supportCache = new Dictionary<(string, string), bool>();

            var homogeneous = layers.Where(IsHomogeneous).ToList();
            var mixed = layers.Where(l => !IsHomogeneous(l)).ToList();

            foreach (var layer in homogeneous)
                TryAddRepeated(pallet, layer, templates, seen);

            foreach (var bottom in homogeneous)
            {
                foreach (var top in homogeneous)
                {
                    if (ReferenceEquals(bottom, top)) continue;
                    if (SkuOf(bottom) == SkuOf(top)) continue;
                    if (bottom.Metrics.TotalWeight < top.Metrics.TotalWeight) continue;
                    if (!IsTransitionValid(bottom, top, pallet, supportCache)) continue;

                    EnumerateStackedPair(pallet, bottom, top, templates, seen);
                }
            }

            foreach (var m in mixed)
            {
                TryAddRepeated(pallet, m, templates, seen);

                foreach (var hom in homogeneous)
                {
                    if (DistinctSkuUnion(m, hom) > MaxDistinctSkusPerTemplate) continue;

                    if (m.Metrics.TotalWeight >= hom.Metrics.TotalWeight &&
                        IsTransitionValid(m, hom, pallet, supportCache))
                    {
                        EnumerateStackedPair(pallet, m, hom, templates, seen);
                    }

                    if (hom.Metrics.TotalWeight >= m.Metrics.TotalWeight &&
                        IsTransitionValid(hom, m, pallet, supportCache))
                    {
                        EnumerateStackedPair(pallet, hom, m, templates, seen);
                    }
                }
            }

            return templates;
        }

        private static void TryAddRepeated(
            Pallet pallet,
            Layer layer,
            List<PalletTemplate> templates,
            HashSet<string> seen)
        {
            int n = Math.Min(MaxRepeats(pallet, layer), MaxLayersPerTemplate);
            if (n < 1) return;

            var stack = new List<Layer>(n);
            for (int i = 0; i < n; i++) stack.Add(layer);
            AddIfDistinct(pallet, stack, templates, seen);
        }

        private static void EnumerateStackedPair(
            Pallet pallet,
            Layer bottom,
            Layer top,
            List<PalletTemplate> templates,
            HashSet<string> seen)
        {
            int availH = pallet.MaxStackHeight - pallet.Height;
            int availW = pallet.MaxStackWeight;
            int maxBottom = Math.Min(MaxRepeats(pallet, bottom), MaxLayersPerTemplate - 1);
            int maxTop = Math.Min(MaxRepeats(pallet, top), MaxLayersPerTemplate - 1);

            for (int nB = 1; nB <= maxBottom; nB++)
            {
                int hUsedB = nB * bottom.Metadata.Height;
                double wUsedB = nB * bottom.Metrics.TotalWeight;
                if (hUsedB > availH || wUsedB > availW) break;

                int remainingLayers = MaxLayersPerTemplate - nB;
                int topCap = Math.Min(maxTop, remainingLayers);

                for (int nT = 1; nT <= topCap; nT++)
                {
                    int hUsed = hUsedB + nT * top.Metadata.Height;
                    double wUsed = wUsedB + nT * top.Metrics.TotalWeight;
                    if (hUsed > availH || wUsed > availW) break;

                    var stack = new List<Layer>(nB + nT);
                    for (int i = 0; i < nB; i++) stack.Add(bottom);
                    for (int i = 0; i < nT; i++) stack.Add(top);
                    AddIfDistinct(pallet, stack, templates, seen);
                }
            }
        }

        private static int MaxRepeats(Pallet pallet, Layer layer)
        {
            if (layer.Metadata.Height <= 0) return 0;
            int availH = pallet.MaxStackHeight - pallet.Height;
            if (availH <= 0) return 0;

            int byH = availH / layer.Metadata.Height;
            int byW = layer.Metrics.TotalWeight > 0
                ? (int)Math.Floor(pallet.MaxStackWeight / layer.Metrics.TotalWeight)
                : int.MaxValue;
            return Math.Min(byH, byW);
        }

        private static bool IsTransitionValid(
            Layer lower,
            Layer upper,
            Pallet pallet,
            Dictionary<(string, string), bool> cache)
        {
            var key = (lower.Id, upper.Id);
            if (cache.TryGetValue(key, out bool cached)) return cached;

            // Placement-aware: the transition is valid if the upper layer can be shifted onto
            // the lower one within the overhang budget, even if its centered position would overhang.
            bool ok = LayerSupportAnalyzer.FindBestPlacement(lower, upper, pallet, pallet.MaxSkuOverhang).Feasible;
            cache[key] = ok;
            return ok;
        }

        private static int DistinctSkuUnion(Layer a, Layer b)
        {
            var union = new HashSet<string>(a.Metrics.UsedSkuTypes, StringComparer.Ordinal);
            foreach (var s in b.Metrics.UsedSkuTypes) union.Add(s);
            return union.Count;
        }

        private static bool IsHomogeneous(Layer l) =>
            l.Metrics.UsedSkuTypes.Count == 1;

        private static string SkuOf(Layer l) =>
            l.Metrics.UsedSkuTypes.First();

        private static void AddIfDistinct(
            Pallet pallet,
            IReadOnlyList<Layer> layers,
            List<PalletTemplate> templates,
            HashSet<string> seen)
        {
            // Dedup on SKU counts first (position-independent), so we only pay the cost of
            // materializing the placed stack for templates we actually keep.
            var signature = Signature(PalletTemplate.FromLayers(layers));
            if (!seen.Add(signature)) return;

            var placed = StackMaterializer.Materialize(pallet, layers, pallet.MaxSkuOverhang);
            templates.Add(PalletTemplate.FromLayers(placed));
        }

        private static string Signature(PalletTemplate t) =>
            string.Join("|", t.SkuCounts
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}"));
    }
}
