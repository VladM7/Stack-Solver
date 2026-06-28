using Stack_Solver.Helpers.Layering;
using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.Layering
{
    public class StripFillGenerationStrategy : ILayerGenerationStrategy
    {
        private const int DefaultMaxRows = 50;

        public string Name => "Strip-Fill";

        public List<Layer> Generate(List<SKU> skus, SupportSurface supportSurface, GenerationOptions options)
        {
            if (skus == null || skus.Count == 0)
                return [];


            int px = supportSurface.Length;
            int py = supportSurface.Width;
            if (px <= 0 || py <= 0)
                return [];


            double area = px * py;
            var variants = SkuVariantFactory.CreateAllOrientations(skus);
            if (variants.Count == 0)
                return [];


            var candidateLayers = new List<Layer>();
            int maxRows = Math.Max(1, Math.Min(py, DefaultMaxRows));

            for (int rowCount = 1; rowCount <= maxRows; rowCount++)
            {
                foreach (var sequence in BuildCandidateSequences(variants, rowCount))
                {
                    if (!SequenceFitsSurface(sequence, py))
                        continue;

                    var layer = TryBuildLayer(sequence, px, area, supportSurface);
                    if (layer != null)
                        candidateLayers.Add(layer);
                }
            }

            var selectedLayers = LayerCandidateHelper.SelectBestBySkuCounts(candidateLayers);
            return selectedLayers;
        }


        private static IEnumerable<List<SkuVariant>> BuildCandidateSequences(IReadOnlyList<SkuVariant> variants, int rowCount)
        {
            if (variants.Count == 0)
                yield break;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var variant in variants)
            {
                var seq = Enumerable.Repeat(variant, rowCount).ToList();
                if (seen.Add(BuildSequenceFingerprint(seq)))
                    yield return seq;
            }

            for (int i = 0; i < variants.Count - 1; i++)
            {
                for (int j = i + 1; j < variants.Count; j++)
                {
                    var seq = BuildAlternatingSequence(variants[i], variants[j], rowCount);
                    if (seen.Add(BuildSequenceFingerprint(seq)))
                        yield return seq;
                }
            }

            var ordered = variants
                .OrderByDescending(v => v.SpanX * v.SpanY)
                .ThenByDescending(v => v.SpanX)
                .ThenBy(v => v.Sku.SkuId, StringComparer.Ordinal)
                .ToList();

            for (int start = 0; start < ordered.Count; start++)
            {
                var seq = BuildWindowSequence(ordered, start, rowCount);
                if (seen.Add(BuildSequenceFingerprint(seq)))
                    yield return seq;
            }
        }

        private static List<SkuVariant> BuildAlternatingSequence(SkuVariant first, SkuVariant second, int length)
        {
            var seq = new List<SkuVariant>(length);
            for (int idx = 0; idx < length; idx++)
            {
                seq.Add(idx % 2 == 0 ? first : second);
            }

            return seq;
        }

        private static List<SkuVariant> BuildWindowSequence(IReadOnlyList<SkuVariant> ordered, int startIndex, int length)
        {
            var seq = new List<SkuVariant>(length);
            for (int idx = 0; idx < length; idx++)
            {
                int sourceIndex = (startIndex + idx) % ordered.Count;
                seq.Add(ordered[sourceIndex]);
            }

            return seq;
        }

        private static bool SequenceFitsSurface(IEnumerable<SkuVariant> sequence, int maxDepth)
        {
            int accumulated = 0;
            foreach (var variant in sequence)
            {
                accumulated += variant.SpanY;
                if (accumulated > maxDepth)
                    return false;
            }

            return true;
        }

        private static Layer? TryBuildLayer(IReadOnlyList<SkuVariant> sequence, int palletLength, double area, SupportSurface supportSurface)
        {
            var placements = new List<PositionedItem>();
            int yOffset = 0;
            double usedArea = 0;

            foreach (var variant in sequence)
            {
                int perRow = palletLength / variant.SpanX;
                if (perRow <= 0)
                    return null;

                for (int ix = 0; ix < perRow; ix++)
                {
                    placements.Add(new PositionedItem(variant.Sku, ix * variant.SpanX, yOffset, variant.Rotated));
                }

                usedArea += perRow * variant.SpanX * variant.SpanY;
                yOffset += variant.SpanY;
            }

            if (placements.Count == 0)
                return null;

            double utilization = usedArea / area;
            int layerHeight = sequence.Count != 0 ? sequence.Max(v => v.Sku.Height) : 0;
            string description = BuildSequenceDescription(sequence);
            string layerId = BuildLayerId(sequence);

            var metadata = new LayerMetadata(utilization, layerHeight, description);
            var layer = new Layer(layerId, placements, metadata);
            layer.Geometry = LayerGeometryBuilder.Build(layer, supportSurface);
            layer.Metrics = LayerMetricsCalculator.Compute(layer, supportSurface);
            return layer;
        }

        private static string BuildSequenceDescription(IReadOnlyList<SkuVariant> sequence)
        {
            var parts = sequence
                .Select(v => $"{v.Sku.Name}:{v.SpanX}x{v.SpanY}");

            return $"rows={sequence.Count} seq=" + string.Join(",", parts);
        }

        private static string BuildLayerId(IReadOnlyList<SkuVariant> sequence)
        {
            var fingerprint = BuildSequenceFingerprint(sequence);
            int hash = 17;
            foreach (char ch in fingerprint)
            {
                hash = (hash * 23) + ch;
            }

            hash = Math.Abs(hash);
            return $"strip_r{sequence.Count}_{hash}";
        }

        private static string BuildSequenceFingerprint(IEnumerable<SkuVariant> sequence)
        {
            return string.Join("|", sequence.Select(v => $"{v.VariantId}:{v.SpanX}x{v.SpanY}"));
        }
    }
}
