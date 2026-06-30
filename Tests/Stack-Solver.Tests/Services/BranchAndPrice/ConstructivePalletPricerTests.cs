using Stack_Solver.Models;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services;
using Stack_Solver.Services.BranchAndPrice;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// The constructive pricer builds a pallet bottom-up from boxes, so it can synthesize the merged
    /// leftover layers the fixed layer pool never contains. The headline case is the user's 50/30/3
    /// instance (A ×50, B ×30, D ×3): a single pallet holds all demand, but only if leftovers of
    /// different SKUs may share a layer — exactly what this pricer enables.
    /// </summary>
    public class ConstructivePalletPricerTests
    {
        // Same height + uniform material ⇒ weight ∝ base area ⇒ equal load density, so the
        // top-heavy stacking rule never blocks (and the 950 kg stack limit stays slack).
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

        private static (List<SKU> skus, Dictionary<string, int> demand, Pallet pallet) Instance()
        {
            var skus = new List<SKU> { Sku("A", 38, 23), Sku("B", 26, 13), Sku("D", 50, 30) };
            var demand = new Dictionary<string, int> { ["A"] = 50, ["B"] = 30, ["D"] = 3 };
            var pallet = new Pallet("P", 120, 80, 14)
            {
                MaxStackHeight = 180,
                MaxStackWeight = 950,
                MaxTopHeavyPercent = 50,
                MaxSkuOverhang = 0,
            };
            return (skus, demand, pallet);
        }

        [Fact]
        public void FindColumns_FiveThirtyThree_WithDualsFavoringLeftovers_BuildsSingleZeroLeftoverPallet()
        {
            var (skus, demand, pallet) = Instance();
            var pricer = new ConstructivePalletPricer(skus, demand, pallet);

            // Duals favour the small-count leftovers (B, D) — the regime where the merged pallet
            // is improving (Σ a·π = 50·0.02 + 30·0.05 + 3·0.05 = 2.65 > 1).
            var duals = new Dictionary<string, double> { ["A"] = 0.02, ["B"] = 0.05, ["D"] = 0.05 };

            var columns = pricer.FindColumns(duals, forbidden: null, reducedCostThreshold: 1.0);

            Assert.NotEmpty(columns);

            // A zero-leftover pallet placing all demand must be among the built columns.
            var full = columns.FirstOrDefault(c =>
                c.CountOf("A") == 50 && c.CountOf("B") == 30 && c.CountOf("D") == 3);
            Assert.True(full != null,
                "Expected a single pallet placing all demand; got: " +
                string.Join("; ", columns.Select(c => c.Signature)));

            // Every layer is support-valid: each box rests entirely on the layer below.
            var layers = full!.Template.Layers;
            for (int i = 1; i < layers.Count; i++)
            {
                var support = LayerSupportAnalyzer.Analyze(layers[i - 1], layers[i], pallet);
                Assert.Equal(0, support.TotalUnsupportedArea);
            }
        }

        [Fact]
        public void FindColumns_IsDeterministicAcrossRuns()
        {
            var (skus, demand, pallet) = Instance();
            var duals = new Dictionary<string, double> { ["A"] = 0.02, ["B"] = 0.05, ["D"] = 0.05 };

            var first = new ConstructivePalletPricer(skus, demand, pallet).FindColumns(duals);
            var second = new ConstructivePalletPricer(skus, demand, pallet).FindColumns(duals);

            Assert.Equal(
                first.Select(c => c.Signature).ToList(),
                second.Select(c => c.Signature).ToList());
        }
    }
}
