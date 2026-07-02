using Stack_Solver.Models;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.BranchAndPrice;
using Stack_Solver.Services.Layering;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// End-to-end check of the Phase-1 fix for the user's 50/30/3 consolidation case (A ×50,
    /// B ×30, D ×3). The fixed layer pool holds only single-SKU layers, so before the constructive
    /// pricer the solver returned 2 pallets and (falsely) called it optimal — it could not merge the
    /// leftover B and D with spare A capacity. The constructive pricer builds the merged single
    /// pallet from boxes, which the pallet-count certifier then proves optimal at K=1 (a Found-at-K
    /// existence witness, sound regardless of layer completeness).
    /// </summary>
    public class ConstructivePricerIntegrationTests
    {
        // Same height + uniform material ⇒ weight ∝ base area ⇒ equal load density (top-heavy rule
        // slack) and a slack 950 kg stack limit — matching the mergeable pallet the user observed.
        private static SKU Sku(string id, int length, int width, int qty) => new()
        {
            SkuId = id,
            Name = id,
            Length = length,
            Width = width,
            Height = 20,
            Weight = length * width * 0.005,
            Rotatable = true,
            Quantity = qty,
        };

        [Fact]
        public void Solve_FiveThirtyThree_ConsolidatesToOneCertifiedPallet()
        {
            var skus = new List<SKU> { Sku("A", 38, 23, 50), Sku("B", 26, 13, 30), Sku("D", 50, 30, 3) };
            var demand = new Dictionary<string, int> { ["A"] = 50, ["B"] = 30, ["D"] = 3 };
            var pallet = new Pallet("P", 120, 80, 14)
            {
                MaxStackHeight = 180,
                MaxStackWeight = 950,
                MaxTopHeavyPercent = 50,
                MaxSkuOverhang = 0,
            };

            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());

            var solution = BranchAndPriceAssignmentService.Solve(
                layers, demand, pallet, new GenerationOptions(), ct: TestContext.Current.CancellationToken);

            string stats = solution.Stats!.ToString();

            // All demand placed, nothing left over.
            foreach (var (sku, qty) in demand)
            {
                int placed = solution.Result.Assignments.Sum(a => a.Template.SkuCounts.GetValueOrDefault(sku) * a.Count);
                Assert.Equal(qty, placed);
            }
            Assert.Empty(solution.Result.Leftovers);

            // The headline: a single pallet, proven optimal — not the old false "2, optimal".
            Assert.Equal(1, solution.Pallets);
            Assert.True(solution.LowerBoundCertified, stats);
            Assert.True(solution.Stats!.ConstructivePricerColumns > 0, stats);
        }
    }
}
