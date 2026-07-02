using Stack_Solver.Models;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.BranchAndPrice;
using Stack_Solver.Services.Layering;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// Pallet-count (cardinality) certification closes the bin-packing "+1 round-up" gap that the
    /// ⌈LP-bound⌉ short-circuit and the uncapped tree cannot. The gap needs a large-capacity layer
    /// (so the volumetric LP bound rounds down) together with a hard reason the demand still cannot
    /// share that few pallets. Here the blocker is the distinct-SKU-per-template cap (3): with one
    /// height-limited layer per pallet and a full layer holding nine boxes, the LP meets each unit
    /// of demand with a fraction of a homogeneous column (bound = SKUs/9 ⇒ ⌈⌉ = 1), yet a single
    /// pallet can carry at most three distinct SKUs — so more than three SKUs cannot fit in one.
    /// (Merging different SKUs into one layer is exactly what the constructive pricer now does, so
    /// the old "two single-SKU layers can't share a pallet" premise is no longer a valid blocker.)
    /// </summary>
    public class PalletCountCertificationTests
    {
        // A full 9-box layer fits the pallet (3×3), so a homogeneous column covers 9 — far more than
        // the demand of 1. Quantity 9 lets that full layer be generated. One 10-high layer fits the
        // 24−14 = 10 of stack height; two do not.
        private static SKU Box(string id) => new()
        {
            SkuId = id, Name = id, Length = 40, Width = 30, Height = 10, Quantity = 9, Rotatable = false,
        };

        private static Pallet OneLayerPallet() => new("P", 120, 90, 14) { MaxStackHeight = 24 };

        [Fact]
        public void Solve_FourSkusExceedingTheDistinctCap_CertifiesTwoPallets()
        {
            // Four SKUs, one unit each: a single pallet's lone layer admits at most three distinct
            // SKUs, so the fourth forces a second pallet. The LP bound is only 4/9 ⇒ ⌈⌉ = 1.
            var skus = new List<SKU> { Box("A"), Box("B"), Box("C"), Box("D") };
            var pallet = OneLayerPallet();
            var demand = new Dictionary<string, int> { ["A"] = 1, ["B"] = 1, ["C"] = 1, ["D"] = 1 };
            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());

            var solution = BranchAndPriceAssignmentService.Solve(
                layers, demand, pallet, new GenerationOptions(), ct: TestContext.Current.CancellationToken);

            foreach (var sku in demand.Keys)
            {
                int placed = solution.Result.Assignments.Sum(a => a.Template.SkuCounts.GetValueOrDefault(sku) * a.Count);
                Assert.Equal(1, placed);
            }
            Assert.Empty(solution.Result.Leftovers);

            // The headline: a proven optimum of 2, certified by pallet-count branching (the LP bound
            // alone is 4/9 → ⌈⌉ = 1 and could not certify this).
            string stats = solution.Stats!.ToString();
            Assert.Equal(2, solution.Pallets);
            Assert.True(solution.LowerBoundCertified, stats);
            Assert.True(solution.Stats!.PalletCountCertified, stats);
            Assert.True(solution.Stats.MaxPalletCountTested >= 1, stats);
        }

        [Fact]
        public void Solve_SevenSkusExceedingTheDistinctCap_CertifiesThreePalletsAcrossMultipleCounts()
        {
            // Seven SKUs, one unit each, ≤ 3 distinct per pallet ⇒ ⌈7/3⌉ = 3 pallets. The LP bound is
            // 7/9 ⇒ ⌈⌉ = 1, so the certifier must reject K=1 and K=2 (both leave a SKU uncovered)
            // before concluding the incumbent of 3 is optimal.
            var skus = new List<SKU> { Box("A"), Box("B"), Box("C"), Box("D"), Box("E"), Box("F"), Box("G") };
            var pallet = OneLayerPallet();
            var demand = skus.ToDictionary(s => s.SkuId, _ => 1, StringComparer.Ordinal);
            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());

            var solution = BranchAndPriceAssignmentService.Solve(
                layers, demand, pallet, new GenerationOptions(), ct: TestContext.Current.CancellationToken);

            Assert.Empty(solution.Result.Leftovers);
            string stats = solution.Stats!.ToString();
            Assert.Equal(3, solution.Pallets);
            Assert.True(solution.LowerBoundCertified, stats);
            Assert.True(solution.Stats!.PalletCountCertified, stats);
            Assert.True(solution.Stats.MaxPalletCountTested >= 2, stats); // tested K=1 and K=2
        }
    }
}
