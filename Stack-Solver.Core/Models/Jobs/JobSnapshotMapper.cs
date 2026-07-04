using Stack_Solver.Models.Assignment;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stack_Solver.Models.Jobs
{
    /// <summary>
    /// Converts produced solutions to and from their persisted <see cref="JobResultsSnapshot"/>, and
    /// (de)serializes the settings/results snapshots to JSON. The rebuild path reuses the domain
    /// constructors and <see cref="PalletTemplate.FromLayers"/> so aggregates are recomputed exactly
    /// as they were during generation.
    /// </summary>
    public static class JobSnapshotMapper
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        // ── JSON ────────────────────────────────────────────────────────────────────
        public static string Serialize(JobSettingsSnapshot settings) => JsonSerializer.Serialize(settings, Json);
        public static string Serialize(JobResultsSnapshot results) => JsonSerializer.Serialize(results, Json);
        public static JobSettingsSnapshot? DeserializeSettings(string json) => JsonSerializer.Deserialize<JobSettingsSnapshot>(json, Json);
        public static JobResultsSnapshot? DeserializeResults(string? json) =>
            string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<JobResultsSnapshot>(json, Json);

        // ── Results: domain → snapshot ───────────────────────────────────────────────
        public static JobResultsSnapshot ToResultsSnapshot(IReadOnlyList<JobSolutionData> solutions)
        {
            var snapshot = new JobResultsSnapshot();
            foreach (var solution in solutions)
            {
                var solutionSnapshot = new JobSolutionSnapshot
                {
                    Name = solution.Name,
                    IsProvenOptimal = solution.IsProvenOptimal,
                    LowerBound = solution.LowerBound
                };

                foreach (var (template, count) in solution.Result.Assignments)
                {
                    var palletSnapshot = new JobPalletSnapshot { Count = count };
                    foreach (var layer in template.Layers)
                    {
                        var layerSnapshot = new JobLayerSnapshot
                        {
                            LayerId = layer.Id,
                            Name = layer.Name,
                            Height = layer.Metadata.Height,
                            Utilization = layer.Metadata.Utilization
                        };
                        foreach (var item in layer.Items)
                        {
                            layerSnapshot.Items.Add(new JobItemSnapshot
                            {
                                SkuId = item.SkuType.SkuId,
                                X = item.X,
                                Y = item.Y,
                                Rotated = item.Rotated
                            });
                        }
                        palletSnapshot.Layers.Add(layerSnapshot);
                    }
                    solutionSnapshot.Pallets.Add(palletSnapshot);
                }

                snapshot.Solutions.Add(solutionSnapshot);
            }
            return snapshot;
        }

        // ── Results: snapshot → domain ───────────────────────────────────────────────
        public static IReadOnlyList<JobSolutionData> FromResultsSnapshot(
            JobResultsSnapshot snapshot, IReadOnlyDictionary<string, SKU> skusById)
        {
            var solutions = new List<JobSolutionData>();
            foreach (var solutionSnapshot in snapshot.Solutions)
            {
                var assignments = new List<(PalletTemplate Template, int Count)>();
                foreach (var palletSnapshot in solutionSnapshot.Pallets)
                {
                    var layers = new List<Layer>();
                    foreach (var layerSnapshot in palletSnapshot.Layers)
                    {
                        var items = new List<PositionedItem>();
                        foreach (var itemSnapshot in layerSnapshot.Items)
                        {
                            // A SKU deleted from the library since the run is simply skipped; the rest
                            // of the layer still renders.
                            if (!skusById.TryGetValue(itemSnapshot.SkuId, out var sku)) continue;
                            items.Add(new PositionedItem(sku, itemSnapshot.X, itemSnapshot.Y, itemSnapshot.Rotated));
                        }

                        var layer = new Layer(layerSnapshot.Name, items,
                            new LayerMetadata(layerSnapshot.Utilization, layerSnapshot.Height, string.Empty))
                        {
                            Id = layerSnapshot.LayerId
                        };
                        // FromLayers reads this back for the template's total weight.
                        layer.Metrics.TotalWeight = items.Sum(i => i.SkuType.Weight);
                        layers.Add(layer);
                    }
                    assignments.Add((PalletTemplate.FromLayers(layers), palletSnapshot.Count));
                }

                var result = new AssignmentResult { Assignments = assignments };
                solutions.Add(new JobSolutionData(
                    solutionSnapshot.Name, solutionSnapshot.IsProvenOptimal, solutionSnapshot.LowerBound, result));
            }
            return solutions;
        }

        /// <summary>Rebuilds a SKU lookup (by id) from a settings snapshot, for use with <see cref="FromResultsSnapshot"/>.</summary>
        public static Dictionary<string, SKU> BuildSkuLookup(JobSettingsSnapshot settings)
            => settings.Skus.ToDictionary(
                s => s.SkuId,
                s => new SKU
                {
                    SkuId = s.SkuId,
                    Name = s.Name,
                    Length = s.Length,
                    Width = s.Width,
                    Height = s.Height,
                    Weight = s.Weight,
                    Rotatable = s.Rotatable,
                    Notes = s.Notes,
                    Quantity = s.Quantity
                },
                StringComparer.Ordinal);
    }
}
