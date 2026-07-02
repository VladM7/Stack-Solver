using Stack_Solver.Models;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.BranchAndPrice;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// The combinatorial bound must be a valid (never-exceeding) lower bound on the pallet count,
    /// and must take the stronger of the weight and volume relaxations.
    /// </summary>
    public class CombinatorialBoundTests
    {
        // A 120×90 surface on a 14-tall pallet capped at 180 tall leaves 166 of stack height.
        private static Pallet Pallet() => new("P", 120, 90, 14) { MaxStackHeight = 180, MaxStackWeight = 1000 };

        private static Dictionary<string, SKU> Skus(params SKU[] skus) =>
            skus.ToDictionary(s => s.SkuId, s => s, StringComparer.Ordinal);

        [Fact]
        public void VolumeBinding_DominatesWhenLight()
        {
            var pallet = Pallet();
            // One box exactly fills the pallet's footprint (120×90) and half its stack height (83).
            // Two such boxes = exactly one pallet of volume → bound 1.0; three → 1.5.
            var sku = new SKU { SkuId = "A", Name = "A", Length = 120, Width = 90, Height = 83, Weight = 0.001 };

            Assert.Equal(1.0, CombinatorialBound.Compute(new Dictionary<string, int> { ["A"] = 2 }, Skus(sku), pallet), 3);
            Assert.Equal(1.5, CombinatorialBound.Compute(new Dictionary<string, int> { ["A"] = 3 }, Skus(sku), pallet), 3);
        }

        [Fact]
        public void WeightBinding_DominatesWhenHeavy()
        {
            var pallet = Pallet(); // MaxStackWeight = 1000
            // Tiny but heavy: volume is negligible, weight binds. 1500 kg / 1000 per pallet = 1.5.
            var sku = new SKU { SkuId = "H", Name = "H", Length = 1, Width = 1, Height = 1, Weight = 500 };

            Assert.Equal(1.5, CombinatorialBound.Compute(new Dictionary<string, int> { ["H"] = 3 }, Skus(sku), pallet), 3);
        }

        [Fact]
        public void NeverExceedsAFeasiblePackedPalletCount()
        {
            var pallet = Pallet();
            // 10 boxes that each occupy a small fraction of a pallet: the bound must be < 1
            // (a single pallet can hold them), i.e. it never claims more pallets than needed.
            var sku = new SKU { SkuId = "S", Name = "S", Length = 20, Width = 20, Height = 20, Weight = 1 };

            double bound = CombinatorialBound.Compute(new Dictionary<string, int> { ["S"] = 10 }, Skus(sku), pallet);
            Assert.True(bound < 1.0, $"bound {bound} must not exceed the 1 pallet that holds 10 small boxes");
        }

        [Fact]
        public void UnknownOrZeroDemandContributesNothing()
        {
            var pallet = Pallet();
            var sku = new SKU { SkuId = "A", Name = "A", Length = 120, Width = 90, Height = 83, Weight = 1 };

            Assert.Equal(0.0, CombinatorialBound.Compute(new Dictionary<string, int> { ["A"] = 0 }, Skus(sku), pallet), 6);
            Assert.Equal(0.0, CombinatorialBound.Compute(new Dictionary<string, int> { ["missing"] = 100 }, Skus(sku), pallet), 6);
        }
    }
}
