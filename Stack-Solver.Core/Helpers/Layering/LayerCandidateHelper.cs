using Stack_Solver.Models.Layering;

namespace Stack_Solver.Helpers.Layering
{
    public static class LayerCandidateHelper
    {
        private const double UtilizationTolerance = 1e-6;

        public static List<Layer> SelectBestBySkuCounts(IEnumerable<Layer> layers)
        {
            if (layers == null)
                return [];

            var bestByKey = new Dictionary<string, Layer>(StringComparer.Ordinal);

            foreach (var layer in layers)
            {
                if (layer == null)
                    continue;

                var key = BuildSkuCountKey(layer);
                if (!bestByKey.TryGetValue(key, out var incumbent) || IsBetter(layer, incumbent))
                    bestByKey[key] = layer;
            }

            return [.. bestByKey.Values
                .OrderByDescending(l => l.Metadata.Utilization)
                .ThenByDescending(l => l.Items.Count)];
        }

        public static bool IsBetter(Layer candidate, Layer incumbent, double tolerance = UtilizationTolerance)
        {
            if (incumbent == null)
                return true;
            if (candidate == null)
                return false;

            double delta = candidate.Metadata.Utilization - incumbent.Metadata.Utilization;
            if (delta > tolerance)
                return true;

            if (Math.Abs(delta) <= tolerance)
                return candidate.Items.Count > incumbent.Items.Count;

            return false;
        }

        public static string BuildSkuCountKey(Layer layer)
        {
            if (layer == null)
                return string.Empty;

            return string.Join(",", layer.Items
                .GroupBy(i => i.SkuType.SkuId)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"{g.Key}:{g.Count()}"));
        }
    }
}
