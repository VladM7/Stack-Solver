using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.BranchAndPrice;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// Impurity must be zero exactly for pallets that are a single SKU in every layer, must count
    /// one "extra distinct-SKU incidence" per additional SKU in a layer and per additional SKU on
    /// the pallet as a whole (the two terms sharing one unit, summed 1:1), and an assignment's
    /// total must be the count-weighted sum over its columns.
    /// </summary>
    public class PurityMetricTests
    {
        private static Layer FakeLayer(params string[] skus) =>
            new("test", [], new LayerMetadata(0, 10, "test"))
            {
                Metrics = new LayerMetrics { UsedSkuTypes = skus }
            };

        private static PalletTemplate Template(IReadOnlyList<Layer> layers, params (string Sku, int Count)[] palletCounts) =>
            new()
            {
                Layers = layers,
                SkuCounts = palletCounts.ToDictionary(c => c.Sku, c => c.Count, StringComparer.Ordinal)
            };

        private static BnpColumn Col(PalletTemplate template) => new(template);

        [Fact]
        public void SingleSkuPallet_SingleLayer_IsZero()
        {
            var template = Template([FakeLayer("A")], ("A", 10));
            Assert.Equal(0, PurityMetric.Impurity(template));
        }

        [Fact]
        public void SingleSkuPallet_MultipleLayers_IsZero()
        {
            // Same SKU repeated across several pure layers: still a pure pallet.
            var template = Template([FakeLayer("A"), FakeLayer("A"), FakeLayer("A")], ("A", 30));
            Assert.Equal(0, PurityMetric.Impurity(template));
        }

        [Fact]
        public void EmptyPallet_IsZero()
        {
            var template = Template([]);
            Assert.Equal(0, PurityMetric.Impurity(template));
        }

        [Fact]
        public void MixedLayer_ChargesExtraDistinctSkusInThatLayerAndOnThePallet()
        {
            // One layer mixing A and B: layer term = 2 - 1 = 1; pallet also sees 2 distinct SKUs,
            // so pallet term = 1. Total = 2.
            var template = Template([FakeLayer("A", "B")], ("A", 5), ("B", 5));
            Assert.Equal(2, PurityMetric.Impurity(template));
        }

        [Fact]
        public void ThreeSkuLayer_ChargesTwoForThatLayer()
        {
            var template = Template([FakeLayer("A", "B", "D")], ("A", 1), ("B", 1), ("D", 1));
            // Layer term = 3 - 1 = 2; pallet term = 3 - 1 = 2. Total = 4.
            Assert.Equal(4, PurityMetric.Impurity(template));
        }

        [Fact]
        public void MixedPalletOfPureLayers_OnlyChargesThePalletTerm()
        {
            // Two SKUs, each confined to its own pure layer: no layer is mixed (layer term 0),
            // but the pallet as a whole still carries 2 distinct SKUs (pallet term 1). This is
            // the case R2 (layer purity) distinguishes from R1 (pallet purity) — a pallet that
            // is easy to build layer-by-layer but still requires two SKU refills.
            var template = Template([FakeLayer("A"), FakeLayer("B")], ("A", 10), ("B", 10));
            Assert.Equal(1, PurityMetric.Impurity(template));
        }

        [Fact]
        public void ZeroCountSkuCountEntries_DoNotInflateThePalletTerm()
        {
            // Defensive: a zero-count entry (e.g. left over from column bookkeeping) must not be
            // treated as a real distinct SKU on the pallet.
            var template = Template([FakeLayer("A")], ("A", 10), ("B", 0));
            Assert.Equal(0, PurityMetric.Impurity(template));
        }

        [Fact]
        public void ColumnOverload_MatchesTemplateImpurity()
        {
            var template = Template([FakeLayer("A", "B")], ("A", 5), ("B", 5));
            Assert.Equal(PurityMetric.Impurity(template), PurityMetric.Impurity(Col(template)));
        }

        [Fact]
        public void TotalImpurity_SumsColumnsWeightedByCount()
        {
            var pure = Col(Template([FakeLayer("A")], ("A", 10)));
            var mixed = Col(Template([FakeLayer("A", "B")], ("A", 5), ("B", 5))); // impurity 2

            long total = PurityMetric.TotalImpurity([(pure, 5), (mixed, 3)]);

            Assert.Equal(6, total); // 5*0 + 3*2
        }

        [Fact]
        public void TotalImpurity_EmptyAssignment_IsZero()
        {
            Assert.Equal(0, PurityMetric.TotalImpurity([]));
        }

        [Fact]
        public void Impurity_NullTemplate_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => PurityMetric.Impurity((PalletTemplate)null!));
        }

        [Fact]
        public void Impurity_NullColumn_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => PurityMetric.Impurity((BnpColumn)null!));
        }
    }
}
