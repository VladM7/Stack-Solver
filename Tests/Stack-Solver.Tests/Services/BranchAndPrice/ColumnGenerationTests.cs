using Stack_Solver.Models;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.BranchAndPrice;
using Stack_Solver.Services.Layering;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// Milestone 2: root column generation must converge to an LP optimum that is no worse
    /// than (and never above) the seed-only relaxation, and must stay a valid lower bound on
    /// the integer pallet count produced by the incumbent.
    /// </summary>
    public class ColumnGenerationTests
    {
        private static List<SKU> TwoSkus(int demandA, int demandB) =>
        [
            new() { SkuId = "A", Name = "A", Length = 40, Width = 30, Height = 10, Quantity = demandA, Rotatable = true },
            new() { SkuId = "B", Name = "B", Length = 30, Width = 30, Height = 8,  Quantity = demandB, Rotatable = true },
        ];

        [Fact]
        public void GenerateColumns_NeverRaisesObjectiveAboveSeedAndGrowsPool()
        {
            var skus = TwoSkus(300, 250);
            var pallet = new Pallet("P", 120, 90, 14);
            var demand = new Dictionary<string, int> { ["A"] = 300, ["B"] = 250 };
            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());

            var seed = BranchAndPriceAssignmentService.SolveRelaxation(layers, demand, pallet, TestContext.Current.CancellationToken);
            var cg = BranchAndPriceAssignmentService.GenerateColumns(layers, demand, pallet, TestContext.Current.CancellationToken);

            Assert.True(cg.Pool.Count >= seed.SeedColumns.Count);
            Assert.True(cg.Objective <= seed.Objective + 1e-6,
                $"CG objective {cg.Objective} must not exceed seed objective {seed.Objective}");
        }

        [Fact]
        public void Assign_PalletCount_IsAtLeastTheLpLowerBound()
        {
            // Demand equal to one full pallet each (A: 144 = 16×9, B: 240 = 20×12) so the root
            // LP is integer and the search is instant; every box places with no leftover.
            var skus = TwoSkus(144, 240);
            var pallet = new Pallet("P", 120, 90, 14);
            var demand = new Dictionary<string, int> { ["A"] = 144, ["B"] = 240 };
            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());

            var cg = BranchAndPriceAssignmentService.GenerateColumns(layers, demand, pallet, TestContext.Current.CancellationToken);
            var result = BranchAndPriceAssignmentService.Assign(layers, demand, pallet, new GenerationOptions(), ct: TestContext.Current.CancellationToken);

            Assert.Empty(result.Leftovers);
            // The integer pallet count can never beat the LP relaxation lower bound.
            Assert.True(result.TotalPallets >= Math.Ceiling(cg.Objective) - 1e-9,
                $"integer pallets {result.TotalPallets} must be ≥ LP bound {cg.Objective}");
        }
    }
}
