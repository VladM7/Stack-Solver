using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Stacking
{
    /// <summary>
    /// Prunes the enumerated template pool before it reaches the assignment stage.
    /// Applies a complexity cap (≤3 distinct SKUs) and a minimum-utilization floor,
    /// then drops Pareto-dominated templates: T dominates U iff T packs ≥ U's count
    /// of every SKU at ≤ U's height and weight, with at least one strict inequality.
    /// </summary>
    public static class TemplateFilter
    {
        private const int MaxDistinctSkusPerTemplate = 3;
        private const double MinUtilization = 0.60;

        public static List<PalletTemplate> Filter(IReadOnlyList<PalletTemplate> templates, Pallet pallet)
        {
            ArgumentNullException.ThrowIfNull(templates);
            ArgumentNullException.ThrowIfNull(pallet);
            if (templates.Count == 0) return [];

            double palletVolume = (double)pallet.Length * pallet.Width * Math.Max(0, pallet.MaxStackHeight - pallet.Height);
            double maxWeight = pallet.MaxStackWeight;

            var afterCaps = new List<PalletTemplate>(templates.Count);
            foreach (var t in templates)
            {
                if (t.SkuCounts.Count == 0 || t.SkuCounts.Count > MaxDistinctSkusPerTemplate) continue;
                if (!PassesUtilizationFloor(t, palletVolume, maxWeight)) continue;
                afterCaps.Add(t);
            }

            return RemoveDominated(afterCaps);
        }

        private static bool PassesUtilizationFloor(PalletTemplate t, double palletVolume, double maxWeight)
        {
            // A heavy, dense template can have low volume utilization while being at weight capacity.
            // Pass if either volume or weight utilization clears the floor.
            double volumeUtil = palletVolume > 0 ? UsedVolume(t) / palletVolume : 0;
            double weightUtil = maxWeight > 0 ? t.TotalWeight / maxWeight : 0;
            return Math.Max(volumeUtil, weightUtil) >= MinUtilization;
        }

        private static double UsedVolume(PalletTemplate t)
        {
            double v = 0;
            foreach (var layer in t.Layers)
                foreach (var item in layer.Items)
                    v += (double)item.SkuType.Length * item.SkuType.Width * item.SkuType.Height;
            return v;
        }

        private static List<PalletTemplate> RemoveDominated(List<PalletTemplate> templates)
        {
            var kept = new List<PalletTemplate>(templates.Count);
            for (int i = 0; i < templates.Count; i++)
            {
                bool dominated = false;
                for (int j = 0; j < templates.Count; j++)
                {
                    if (i == j) continue;
                    if (Dominates(templates[j], templates[i]))
                    {
                        dominated = true;
                        break;
                    }
                }
                if (!dominated) kept.Add(templates[i]);
            }
            return kept;
        }

        private static bool Dominates(PalletTemplate t, PalletTemplate u)
        {
            if (t.TotalHeight > u.TotalHeight) return false;
            if (t.TotalWeight > u.TotalWeight) return false;

            bool strict = t.TotalHeight < u.TotalHeight || t.TotalWeight < u.TotalWeight;

            foreach (var (sku, uCount) in u.SkuCounts)
            {
                int tCount = t.SkuCounts.GetValueOrDefault(sku);
                if (tCount < uCount) return false;
                if (tCount > uCount) strict = true;
            }

            if (!strict)
            {
                foreach (var sku in t.SkuCounts.Keys)
                {
                    if (!u.SkuCounts.ContainsKey(sku))
                    {
                        strict = true;
                        break;
                    }
                }
            }

            return strict;
        }
    }
}
