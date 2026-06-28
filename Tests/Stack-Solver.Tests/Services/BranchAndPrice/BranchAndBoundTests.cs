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
        public void Solve_SubLayerRemainder_IsPlacedViaFillerLayers()
        {
            // Full-grid layers hold 9 boxes (non-rotatable). 21 = 2×9 + 3: the 3-box remainder
            // has no full layer, but filler layers (1, 2, 4 boxes) place it — so every box is
            // placed with no leftover, fitting one pallet.
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
            Assert.Equal(21, placed);                 // every box placed (3-box remainder on an extra pallet)
            Assert.Empty(solution.Result.Leftovers);
            // The 18 full-layer boxes plus the 3-box filler remainder fall on separate pallets.
            Assert.Equal(2, solution.Pallets);
            // Appending the heuristic remainder pallet means the total is no longer certified optimal.
            Assert.False(solution.LowerBoundCertified);
        }
    }
}
