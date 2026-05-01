using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Stacking
{
    /// <summary>
    /// Generates a diverse pool of pallet templates from a filtered layer set by running
    /// the greedy stacking strategy with different layer orderings.
    /// </summary>
    public static class PalletTemplateEnumerator
    {
        private static readonly GreedyStackingStrategy _strategy = new();

        public static List<PalletTemplate> Enumerate(
            Pallet pallet,
            IReadOnlyList<Layer> layers,
            GenerationOptions options)
        {
            if (layers.Count == 0)
                return [];

            var templates = new List<PalletTemplate>();

            // Ordering 1: heaviest layers first (plan §5: heavy on bottom)
            TryAdd(pallet, layers.OrderByDescending(l => l.Metrics.TotalWeight).ToList(), options, templates);

            // Ordering 2: highest utilization first
            TryAdd(pallet, layers.OrderByDescending(l => l.Metadata.Utilization).ToList(), options, templates);

            // Ordering 3: each layer as mandatory base, rest sorted by weight
            foreach (var baseLayer in layers)
            {
                var ordered = layers
                    .Where(l => !ReferenceEquals(l, baseLayer))
                    .OrderByDescending(l => l.Metrics.TotalWeight)
                    .Prepend(baseLayer)
                    .ToList();
                TryAdd(pallet, ordered, options, templates);
            }

            return templates;
        }

        private static void TryAdd(
            Pallet pallet,
            IReadOnlyList<Layer> ordered,
            GenerationOptions options,
            List<PalletTemplate> templates)
        {
            var t = _strategy.Build(pallet, ordered, options);
            if (t != null && IsDistinct(t, templates))
                templates.Add(t);
        }

        private static bool IsDistinct(PalletTemplate candidate, IReadOnlyList<PalletTemplate> existing)
        {
            var sig = Signature(candidate);
            return existing.All(t => Signature(t) != sig);
        }

        private static string Signature(PalletTemplate t) =>
            string.Join("|", t.SkuCounts
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}"));
    }
}
