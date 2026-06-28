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
        public void Solve_SubLayerRemainder_IsPlacedAndCertified()
        {
            // Full-grid layers hold 9 boxes (non-rotatable). 21 = 2×9 + 3: the minimal residual
            // layer (one 3-box partial) lets the optimizer tile 21 exactly as a [9,9,3] stack on
            // a single pallet (16 layers of height 10 fit in 166 of stack height) — so every box
            // is placed, with no leftover, and the result is a proven optimum.
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
            Assert.Equal(21, placed);                 // every box placed (3-box remainder tiled inside the model)
            Assert.Empty(solution.Result.Leftovers);
            // The two full layers and the 3-box partial stack onto one pallet.
            Assert.Equal(1, solution.Pallets);
            // The residual is part of the optimization now, so the total is a proven optimum.
            Assert.True(solution.LowerBoundCertified);
        }

        [Fact]
        public void Solve_CrossSkuRemainders_SharePalletAndCertify()
        {
            // Pallet holds exactly 2 layers (stack height 34 − 14 base = 20, layer height 10).
            // A and B each demand 21 = 2×9 + 3, so each contributes two full layers plus a 3-box
            // partial. The proven optimum is 3 pallets: [A9,A9], [B9,B9], and a shared [A3,B3] —
            // only possible because the two residual partials can stack together on one pallet.
            var skus = new List<SKU>
            {
                new() { SkuId = "A", Name = "A", Length = 40, Width = 30, Height = 10, Quantity = 21, Rotatable = false },
                new() { SkuId = "B", Name = "B", Length = 40, Width = 30, Height = 10, Quantity = 21, Rotatable = false },
            };
            var pallet = new Pallet("P", 120, 90, 14) { MaxStackHeight = 34 };
            var demand = new Dictionary<string, int> { ["A"] = 21, ["B"] = 21 };
            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());

            var solution = BranchAndPriceAssignmentService.Solve(
                layers, demand, pallet, new GenerationOptions(), ct: TestContext.Current.CancellationToken);

            Assert.Equal(21, solution.Result.Assignments.Sum(a => a.Template.SkuCounts.GetValueOrDefault("A") * a.Count));
            Assert.Equal(21, solution.Result.Assignments.Sum(a => a.Template.SkuCounts.GetValueOrDefault("B") * a.Count));
            Assert.Empty(solution.Result.Leftovers);
            Assert.Equal(3, solution.Pallets);
            Assert.True(solution.LowerBoundCertified);
        }

        [Fact]
        public void Solve_LargeMixedDemand_PlacesEverythingWithinBudget()
        {
            // The user's 500/50/50 case. 40×30×10 boxes tile 9 to a layer; a pallet holds 16
            // layers (166 / 10). 500 = 55×9 + 5, 50 = 5×9 + 5, so the residual partials (5 boxes
            // each) must be placed too. Every box must end up on a pallet with no leftover.
            var skus = new List<SKU>
            {
                new() { SkuId = "A", Name = "A", Length = 40, Width = 30, Height = 10, Quantity = 500, Rotatable = false },
                new() { SkuId = "B", Name = "B", Length = 40, Width = 30, Height = 10, Quantity = 50, Rotatable = false },
                new() { SkuId = "C", Name = "C", Length = 40, Width = 30, Height = 10, Quantity = 50, Rotatable = false },
            };
            var pallet = new Pallet("P", 120, 90, 14);
            var demand = new Dictionary<string, int> { ["A"] = 500, ["B"] = 50, ["C"] = 50 };
            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());

            var solution = BranchAndPriceAssignmentService.Solve(
                layers, demand, pallet, new GenerationOptions(), ct: TestContext.Current.CancellationToken);

            foreach (var (sku, qty) in demand)
            {
                int placed = solution.Result.Assignments.Sum(a => a.Template.SkuCounts.GetValueOrDefault(sku) * a.Count);
                Assert.Equal(qty, placed);
            }
            Assert.Empty(solution.Result.Leftovers);
            // 68 layers (56 + 6 + 6) over 16 layers/pallet ⇒ 5 pallets is optimal.
            Assert.Equal(5, solution.Pallets);
            // Constructive incumbent (5) matches ⌈LP bound 4.25⌉, so the search proves optimality.
            Assert.True(solution.LowerBoundCertified);
        }
    }
}
