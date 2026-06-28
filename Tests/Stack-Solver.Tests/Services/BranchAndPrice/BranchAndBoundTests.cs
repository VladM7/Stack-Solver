using Stack_Solver.Models;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.BranchAndPrice;
using Stack_Solver.Services.Layering;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// Milestone 4: branch-and-bound with per-node column generation. Verifies that a
    /// fractional root is resolved to a proven integer optimum, and that the proven pallet
    /// count is never below the LP lower bound.
    /// </summary>
    public class BranchAndBoundTests
    {
        [Fact]
        public void Solve_FractionalRoot_ResolvesToProvenIntegerOptimum()
        {
            // 153 = 144 + 9: one full 16-layer pallet plus a 1-layer pallet, so the optimum is
            // 2 pallets with no leftover. The root LP (x = 153/144 ≈ 1.06) is fractional, so the
            // search must branch and generate the smaller column.
            var skus = new List<SKU>
            {
                new() { SkuId = "A", Name = "A", Length = 40, Width = 30, Height = 10, Quantity = 153, Rotatable = false },
            };
            var pallet = new Pallet("P", 120, 90, 14);
            var demand = new Dictionary<string, int> { ["A"] = 153 };
            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());

            var solution = BranchAndPriceAssignmentService.Solve(
                layers, demand, pallet, new GenerationOptions(), ct: TestContext.Current.CancellationToken);

            int placed = solution.Result.Assignments.Sum(a => a.Template.SkuCounts.GetValueOrDefault("A") * a.Count);
            Assert.Equal(153, placed);
            Assert.Empty(solution.Result.Leftovers);
            Assert.Equal(2, solution.Pallets);
            Assert.True(solution.LowerBoundCertified);
            Assert.True(solution.Pallets >= Math.Ceiling(solution.LowerBound) - 1e-9);
        }

        [Fact]
        public void Solve_NonTileableRemainder_MinimizesLeftoverThenPallets()
        {
            // Only 9-box layers exist (non-rotatable). 21 = 2×9 + 3: the minimum leftover is 3,
            // and the 18 placed boxes fit one 2-layer pallet — so the proven optimum is 1
            // pallet with 3 leftover (big-M drives leftovers down before trading pallets).
            var skus = new List<SKU>
            {
                new() { SkuId = "A", Name = "A", Length = 40, Width = 30, Height = 10, Quantity = 21, Rotatable = false },
            };
            var pallet = new Pallet("P", 120, 90, 14);
            var demand = new Dictionary<string, int> { ["A"] = 21 };
            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());

            var solution = BranchAndPriceAssignmentService.Solve(
                layers, demand, pallet, new GenerationOptions(), ct: TestContext.Current.CancellationToken);

            int placed = solution.Result.Assignments.Sum(a => a.Template.SkuCounts.GetValueOrDefault("A") * a.Count);
            Assert.Equal(18, placed);
            Assert.Equal(3, solution.Result.Leftovers["A"]);
            Assert.Equal(1, solution.Pallets);
            Assert.True(solution.LowerBoundCertified);
        }
    }
}
