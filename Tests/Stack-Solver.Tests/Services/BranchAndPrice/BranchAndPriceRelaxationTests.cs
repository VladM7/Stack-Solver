using Google.OrTools.LinearSolver;
using Stack_Solver.Models;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.BranchAndPrice;
using Stack_Solver.Services.Layering;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// Milestone 1: validates GLOP wiring and the master LP math (objective and duals)
    /// over the homogeneous seed pool. With only homogeneous columns, each demand
    /// constraint is covered solely by its own variable, so the optimum is analytic:
    /// x_i = d_i / c_i, objective = Σ d_i / c_i, and dual π_i = 1 / c_i (giving a zero
    /// reduced cost 1 − c_i·π_i for every basic column).
    /// </summary>
    public class BranchAndPriceRelaxationTests
    {
        private static List<SKU> TwoSkus(int demandA, int demandB) =>
        [
            new() { SkuId = "A", Name = "A", Length = 40, Width = 30, Height = 10, Quantity = demandA, Rotatable = true },
            new() { SkuId = "B", Name = "B", Length = 30, Width = 30, Height = 8,  Quantity = demandB, Rotatable = true },
        ];

        [Fact]
        public void SolveRelaxation_HomogeneousSeed_IsOptimalWithAnalyticDualsAndObjective()
        {
            int demandA = 288, demandB = 240;
            var skus = TwoSkus(demandA, demandB);
            var pallet = new Pallet("P", 120, 90, 14);
            var demand = new Dictionary<string, int> { ["A"] = demandA, ["B"] = demandB };

            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());

            var r = BranchAndPriceAssignmentService.SolveRelaxation(layers, demand, pallet, TestContext.Current.CancellationToken);

            Assert.Equal(Solver.ResultStatus.OPTIMAL, r.Status);
            Assert.Empty(r.UnplaceableSkus);
            Assert.Equal(2, r.SeedColumns.Count);

            // Capacities come from the seed columns themselves, so the assertion checks the
            // LP solved consistently with the columns rather than re-deriving the geometry.
            double expectedObjective = 0;
            foreach (var col in r.SeedColumns)
            {
                var sku = col.SkuCounts.Keys.Single();
                int capacity = col.CountOf(sku);
                Assert.True(capacity > 0);

                double dual = r.Duals[sku];
                Assert.Equal(1.0 / capacity, dual, 9);          // π_i = 1 / c_i
                Assert.Equal(0.0, 1.0 - capacity * dual, 9);    // zero reduced cost for the basic column

                expectedObjective += (double)demand[sku] / capacity;
            }

            Assert.Equal(expectedObjective, r.Objective, 9);
        }

        [Fact]
        public void SolveRelaxation_OversizedSku_IsReportedUnplaceable()
        {
            var skus = new List<SKU>
            {
                new() { SkuId = "BIG", Name = "Big", Length = 500, Width = 500, Height = 50, Quantity = 5, Rotatable = true },
            };
            var pallet = new Pallet("P", 120, 90, 14);
            var demand = new Dictionary<string, int> { ["BIG"] = 5 };

            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());

            var r = BranchAndPriceAssignmentService.SolveRelaxation(layers, demand, pallet, TestContext.Current.CancellationToken);

            Assert.Empty(r.SeedColumns);
            Assert.Equal(["BIG"], r.UnplaceableSkus);
        }

        [Fact]
        public void Assign_LayerDivisibleDemand_PlacesEveryBoxAndFlagsUnplaceable()
        {
            // A homogeneous layer of A holds 9 boxes, so a multiple of 9 tiles exactly.
            int demandA = 9 * 30;
            var skus = new List<SKU>
            {
                new() { SkuId = "A", Name = "A", Length = 40, Width = 30, Height = 10, Quantity = demandA, Rotatable = true },
                new() { SkuId = "BIG", Name = "Big", Length = 500, Width = 500, Height = 50, Quantity = 4, Rotatable = true },
            };
            var pallet = new Pallet("P", 120, 90, 14);
            var demand = new Dictionary<string, int> { ["A"] = demandA, ["BIG"] = 4 };

            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());

            var result = BranchAndPriceAssignmentService.Assign(layers, demand, pallet, new GenerationOptions(), ct: TestContext.Current.CancellationToken);

            int placedA = result.Assignments.Sum(a => a.Template.SkuCounts.GetValueOrDefault("A") * a.Count);
            Assert.Equal(demandA, placedA);                       // every placeable box is placed
            Assert.False(result.Leftovers.ContainsKey("A"));

            Assert.Equal(4, result.Leftovers["BIG"]);             // unplaceable SKU → leftover
        }

        [Fact]
        public void Assign_SubLayerRemainder_IsReportedAsLeftover()
        {
            // 9-box layers cannot tile 290 exactly: 288 are placed, 2 remain as leftover.
            int demandA = 290;
            var skus = new List<SKU>
            {
                new() { SkuId = "A", Name = "A", Length = 40, Width = 30, Height = 10, Quantity = demandA, Rotatable = true },
            };
            var pallet = new Pallet("P", 120, 90, 14);
            var demand = new Dictionary<string, int> { ["A"] = demandA };

            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());

            var result = BranchAndPriceAssignmentService.Assign(layers, demand, pallet, new GenerationOptions(), ct: TestContext.Current.CancellationToken);

            int placedA = result.Assignments.Sum(a => a.Template.SkuCounts.GetValueOrDefault("A") * a.Count);
            Assert.Equal(288, placedA);
            Assert.Equal(2, result.Leftovers["A"]);
        }
    }
}
