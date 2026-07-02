using Stack_Solver.Models;
using Stack_Solver.Models.Assignment;
using Stack_Solver.Models.Jobs;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;

namespace Models.Jobs
{
    public class JobSnapshotMapperTests
    {
        private static (SKU a, SKU b) Skus() =>
        (
            new SKU { SkuId = "A", Name = "Alpha", Length = 40, Width = 30, Height = 20, Weight = 5, Rotatable = true },
            new SKU { SkuId = "B", Name = "Beta", Length = 20, Width = 20, Height = 10, Weight = 2, Rotatable = false }
        );

        private static AssignmentResult SampleResult(SKU a, SKU b)
        {
            var items = new List<PositionedItem>
            {
                new(a, 0, 0, false),
                new(b, 40, 0, true)
            };
            var layer = new Layer("Layer 1", items, new LayerMetadata(0.75, 20, "")) { Id = "layer-1" };
            var template = PalletTemplate.FromLayers([layer]);
            return new AssignmentResult { Assignments = [(template, 3)] };
        }

        [Fact]
        public void Results_RoundTrip_PreservesPlacementsAndStats()
        {
            var (a, b) = Skus();
            var result = SampleResult(a, b);
            var solutions = new List<JobSolutionData>
            {
                new("Greedy", null, 0, result),
                new("Branch & Price", true, 2.5, result)
            };

            var json = JobSnapshotMapper.Serialize(JobSnapshotMapper.ToResultsSnapshot(solutions));
            var snapshot = JobSnapshotMapper.DeserializeResults(json)!;
            var lookup = new Dictionary<string, SKU> { ["A"] = a, ["B"] = b };
            var rebuilt = JobSnapshotMapper.FromResultsSnapshot(snapshot, lookup);

            Assert.Equal(2, rebuilt.Count);

            Assert.Equal("Greedy", rebuilt[0].Name);
            Assert.Null(rebuilt[0].IsProvenOptimal);
            Assert.Equal(0, rebuilt[0].LowerBound);

            var bnp = rebuilt[1];
            Assert.Equal("Branch & Price", bnp.Name);
            Assert.True(bnp.IsProvenOptimal);
            Assert.Equal(2.5, bnp.LowerBound);

            var (template, count) = Assert.Single(bnp.Result.Assignments);
            Assert.Equal(3, count);

            var layer = Assert.Single(template.Layers);
            Assert.Equal("layer-1", layer.Id);
            Assert.Equal("Layer 1", layer.Name);
            Assert.Equal(20, layer.Metadata.Height);
            Assert.Equal(0.75, layer.Metadata.Utilization);

            Assert.Equal(2, template.TotalBoxCount);
            Assert.Equal(7, template.TotalWeight);   // 5 + 2, recomputed from item SKUs
            Assert.Equal(20, template.TotalHeight);

            var placedA = layer.Items.Single(i => i.SkuType.SkuId == "A");
            Assert.Equal((0, 0, false), (placedA.X, placedA.Y, placedA.Rotated));
            var placedB = layer.Items.Single(i => i.SkuType.SkuId == "B");
            Assert.Equal((40, 0, true), (placedB.X, placedB.Y, placedB.Rotated));
        }

        [Fact]
        public void Results_RoundTrip_UnknownSkuIsSkipped()
        {
            var (a, b) = Skus();
            var result = SampleResult(a, b);
            var solutions = new List<JobSolutionData> { new("Greedy", null, 0, result) };

            var snapshot = JobSnapshotMapper.DeserializeResults(
                JobSnapshotMapper.Serialize(JobSnapshotMapper.ToResultsSnapshot(solutions)))!;

            // "B" was deleted from the library since the run.
            var lookup = new Dictionary<string, SKU> { ["A"] = a };
            var rebuilt = JobSnapshotMapper.FromResultsSnapshot(snapshot, lookup);

            var (template, _) = Assert.Single(rebuilt[0].Result.Assignments);
            var layer = Assert.Single(template.Layers);
            var item = Assert.Single(layer.Items);
            Assert.Equal("A", item.SkuType.SkuId);
        }

        [Fact]
        public void Settings_RoundTrip_PreservesFieldsAndSkus()
        {
            var settings = new JobSettingsSnapshot
            {
                DefaultCatalog = "International",
                DefaultPalletName = "EUR 120x80",
                PalletLength = 120,
                PalletWidth = 80,
                PalletHeight = 14.4,
                MaxStackHeight = 180,
                MaxStackWeight = 950,
                OverhangMode = OverhangMode.MinSupportedPercent,
                MaxSkuOverhang = 100,
                MaxTopHeavyPercent = 25,
                UseCpsat = true,
                UseGreedy = true,
                UseCpsatSolution = false,
                UseBranchAndPrice = true,
                SolverTimeLimit = 60,
                MaxCpsatCandidates = 2000,
                BlfAttempts = 200,
                Skus =
                [
                    new JobSkuSnapshot { SkuId = "A", Name = "Alpha", Length = 40, Width = 30, Height = 20, Weight = 5, Rotatable = true, Quantity = 12 }
                ]
            };

            var back = JobSnapshotMapper.DeserializeSettings(JobSnapshotMapper.Serialize(settings))!;

            Assert.Equal("EUR 120x80", back.DefaultPalletName);
            Assert.Equal(OverhangMode.MinSupportedPercent, back.OverhangMode);
            Assert.Equal(100, back.MaxSkuOverhang);
            Assert.False(back.UseCpsatSolution);
            Assert.True(back.UseBranchAndPrice);
            var sku = Assert.Single(back.Skus);
            Assert.Equal("A", sku.SkuId);
            Assert.Equal(12, sku.Quantity);
            Assert.True(sku.Rotatable);
        }
    }
}
