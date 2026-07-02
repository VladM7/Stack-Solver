using Stack_Solver.Models.Supports;
using Stack_Solver.Services.BranchAndPrice;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// The cardinality cap Σ_t x_t ≤ K underpins pallet-count certification: it must bind when
    /// set (forcing leftover when fewer pallets cannot cover demand), yield a dual that raises the
    /// pricer's improving threshold (1 − μ ≥ 1), apply to columns added after it is set, and be
    /// fully reversible.
    /// </summary>
    public class RestrictedMasterProblemTests
    {
        private static BnpColumn Col(params (string Sku, int Count)[] counts) =>
            new(new PalletTemplate
            {
                SkuCounts = counts.ToDictionary(c => c.Sku, c => c.Count, StringComparer.Ordinal)
            });

        [Fact]
        public void CardinalityCap_BindsAndReverses()
        {
            // Two pure pallets cover A=10, B=10 in exactly 2 pallets.
            var demand = new Dictionary<string, int> { ["A"] = 10, ["B"] = 10 };
            using var rmp = new RestrictedMasterProblem(["A", "B"], demand);
            rmp.AddColumns([Col(("A", 10)), Col(("B", 10))]);

            rmp.Solve();
            Assert.False(rmp.IsCardinalityCapped);
            Assert.Equal(2.0, rmp.PalletSum(), 6);
            Assert.Empty(rmp.Leftovers());

            // Cap to one pallet: only one SKU can be fully covered, the other is forced leftover,
            // and the cap binds (Σx ≤ 1) so its dual makes columns price harder (1 − μ ≥ 1).
            rmp.SetCardinalityCap(1);
            rmp.Solve();
            Assert.True(rmp.IsCardinalityCapped);
            Assert.True(rmp.PalletSum() <= 1.0 + 1e-6);
            Assert.NotEmpty(rmp.Leftovers());
            Assert.True(rmp.CardinalityDual() <= 1e-6);          // μ ≤ 0 ⇒ threshold 1 − μ ≥ 1

            // Relaxing the cap recovers the uncapped two-pallet optimum.
            rmp.ClearCardinalityCap();
            rmp.Solve();
            Assert.False(rmp.IsCardinalityCapped);
            Assert.Equal(2.0, rmp.PalletSum(), 6);
            Assert.Empty(rmp.Leftovers());
        }

        [Fact]
        public void CardinalityCap_AppliesToColumnsAddedAfterItIsSet()
        {
            var demand = new Dictionary<string, int> { ["A"] = 10, ["B"] = 10 };
            using var rmp = new RestrictedMasterProblem(["A", "B"], demand);
            rmp.AddColumns([Col(("A", 10)), Col(("B", 10))]);

            rmp.SetCardinalityCap(1);
            // A mixed pallet covering all demand is added AFTER the cap; it must still be counted
            // by the cap, so a single one of it satisfies Σx ≤ 1 with no leftover.
            rmp.AddColumn(Col(("A", 10), ("B", 10)));
            rmp.Solve();

            Assert.True(rmp.PalletSum() <= 1.0 + 1e-6);
            Assert.Equal(1.0, rmp.PalletSum(), 6);
            Assert.Empty(rmp.Leftovers());
        }
    }
}
