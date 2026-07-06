using Stack_Solver.Models;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.BranchAndPrice;
using Stack_Solver.Services.Layering;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// End-to-end coverage of Solution A (<c>BranchAndPriceSearch.TryImprovePurity</c>) through the
    /// public <see cref="BranchAndPriceAssignmentService"/> entry point. The "adopts a strictly
    /// purer alternative" mechanic is proven in isolation by
    /// <see cref="RestrictedMasterHeuristicTests"/> (the columnCost/palletCountCap plumbing this
    /// pass relies on); these tests instead confirm the pass is correctly wired into a real solve —
    /// it always runs, never regresses the pallet count or leftovers, and settles at the impurity
    /// floor that is mathematically forced by each fixture's geometry.
    /// </summary>
    public class PurityPolishIntegrationTests
    {
        // Same height + weight ∝ area ⇒ equal load density, so the top-heavy stacking rule never
        // blocks a stacking order and only support geometry decides which layer may sit on which.
        private static SKU Sku(string id, int length, int width) => new()
        {
            SkuId = id,
            Name = id,
            Length = length,
            Width = width,
            Height = 20,
            Weight = length * width * 0.005,
            Rotatable = true,
        };

        [Fact]
        public void TwoSkusSharingOnePallet_SettlesAtTheProvableImpurityFloor()
        {
            // A single unit each of two differently-sized SKUs: both fit on one pallet (the true
            // minimum), stacked as two pure layers (A's footprint fully supports B's smaller one).
            // With 2 distinct SKUs forced onto 1 pallet, the pallet-level term (distinctOnPallet − 1)
            // is unavoidably 1 regardless of layering, and pure layering (layer term 0) is
            // geometrically achievable here — so impurity 1 is the provable floor; purity polish
            // must run, find nothing better, and report the incumbent unchanged.
            var skus = new List<SKU> { Sku("A", 38, 23), Sku("B", 26, 13) };
            var demand = new Dictionary<string, int> { ["A"] = 1, ["B"] = 1 };
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

            Assert.Equal(1, solution.Pallets);
            Assert.Empty(solution.Result.Leftovers);
            Assert.True(solution.Stats!.PurityPolishRan, solution.Stats.ToString());
            Assert.Equal(1, solution.Stats.FinalImpurity);
            Assert.Equal(1, PurityMetric.TotalImpurity(
                solution.Result.Assignments.Select(a => (new BnpColumn(a.Template), a.Count))));
        }

        [Fact]
        public void ThreeSkusForcedOntoOnePallet_SettlesAtTheProvableImpurityFloor()
        {
            // The known 50/30/3 fixture (see ConstructivePalletPricerTests): all demand fits on one
            // pallet only because leftover boxes of different SKUs may share a layer — laying out
            // every SKU in its own pure layers would need 9 layers (6 for A, 2 for B, 1 for D) but
            // only 8 fit the stack height, so at least one merged layer is unavoidable. With 3
            // distinct SKUs on 1 pallet the pallet-level term is fixed at 2, and merging exactly one
            // pair of SKUs into a single layer (rather than more) is the least the layer-level term
            // can be pushed to (1) — so impurity 3 is the provable floor for this geometry.
            var skus = new List<SKU> { Sku("A", 38, 23), Sku("B", 26, 13), Sku("D", 50, 30) };
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

            Assert.Equal(1, solution.Pallets);
            Assert.Empty(solution.Result.Leftovers);
            Assert.True(solution.Stats!.PurityPolishRan, solution.Stats.ToString());
            Assert.Equal(3, solution.Stats.FinalImpurity);
            Assert.Equal(3, PurityMetric.TotalImpurity(
                solution.Result.Assignments.Select(a => (new BnpColumn(a.Template), a.Count))));
        }
    }
}
