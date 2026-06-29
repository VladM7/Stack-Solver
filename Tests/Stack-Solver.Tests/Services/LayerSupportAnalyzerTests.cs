using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services;

namespace Services
{
    public class LayerSupportAnalyzerTests
    {
        [Fact]
        public void Analyze_NoLowerLayer_AllUpperCellsUnsupported()
        {
            var pallet = new Pallet("Test", 100, 100, 10);
            var sku = CreateSku("A", 20, 20);
            var upperLayer = CreateLayer("Upper",
                new PositionedItem(sku, 0, 0, rotated: false));

            var metrics = LayerSupportAnalyzer.Analyze(null, upperLayer, pallet, gridStep: 10);

            Assert.Equal(400, metrics.TotalUnsupportedArea, 3);
            Assert.Equal(400, metrics.MaximumSkuOverhangArea, 3);
        }

        [Fact]
        public void Analyze_PartialSupport_ComputesUnsupportedArea()
        {
            var pallet = new Pallet("Test", 100, 100, 10);
            var lowerSku = CreateSku("Lower", 20, 20);
            var upperSku = CreateSku("Upper", 40, 20);

            var lowerLayer = CreateLayer("Lower",
                new PositionedItem(lowerSku, 0, 0, rotated: false));
            var upperLayer = CreateLayer("Upper",
                new PositionedItem(upperSku, 0, 0, rotated: false));

            var metrics = LayerSupportAnalyzer.Analyze(lowerLayer, upperLayer, pallet, gridStep: 10);

            Assert.Equal(400, metrics.TotalUnsupportedArea, 3);
            Assert.Equal(400, metrics.MaximumSkuOverhangArea, 3);
        }

        [Fact]
        public void Analyze_MultipleSkus_TracksWorstOverhang()
        {
            var pallet = new Pallet("Test", 120, 40, 10);
            var supportSku = CreateSku("Support", 20, 20);
            var skuA = CreateSku("A", 20, 20);
            var skuB = CreateSku("B", 30, 20);
            var skuC = CreateSku("C", 10, 20);

            var lowerLayer = CreateLayer("Lower",
                new PositionedItem(supportSku, 0, 0, rotated: false));

            var upperLayer = CreateLayer("Upper",
                new PositionedItem(skuA, 0, 0, rotated: false),
                new PositionedItem(skuB, 40, 0, rotated: false),
                new PositionedItem(skuC, 80, 0, rotated: false));

            var metrics = LayerSupportAnalyzer.Analyze(lowerLayer, upperLayer, pallet, gridStep: 10);

            Assert.Equal(800, metrics.TotalUnsupportedArea, 3);
            Assert.Equal(600, metrics.MaximumSkuOverhangArea, 3);
        }

        [Fact]
        public void FindBestPlacement_OffCentreUpperOverCentredLower_ShiftsOntoSupport()
        {
            // Upper layer's box is packed in the corner; lower layer's box is centred.
            // At the upper's own position there is zero support, but a shift makes it fully supported.
            var pallet = new Pallet("Test", 100, 100, 10);
            var sku = CreateSku("A", 40, 40);

            var lowerLayer = CreateLayer("Lower",
                new PositionedItem(sku, 30, 30, rotated: false));
            var upperLayer = CreateLayer("Upper",
                new PositionedItem(sku, 0, 0, rotated: false));

            var fit = LayerSupportAnalyzer.FindBestPlacement(lowerLayer, upperLayer, pallet, new OverhangRule(OverhangMode.AbsoluteCm, 0), gridStep: 10);

            Assert.True(fit.Feasible);
            Assert.Equal(30, fit.OffsetX);
            Assert.Equal(30, fit.OffsetY);
            Assert.Equal(0, fit.Metrics.MaximumSkuOverhangArea, 3);
        }

        [Fact]
        public void FindBestPlacement_AlreadySupported_PrefersNoShift()
        {
            var pallet = new Pallet("Test", 100, 100, 10);
            var baseSku = CreateSku("Base", 100, 100);
            var topSku = CreateSku("Top", 40, 40);

            var lowerLayer = CreateLayer("Lower",
                new PositionedItem(baseSku, 0, 0, rotated: false));
            var upperLayer = CreateLayer("Upper",
                new PositionedItem(topSku, 30, 30, rotated: false));

            var fit = LayerSupportAnalyzer.FindBestPlacement(lowerLayer, upperLayer, pallet, new OverhangRule(OverhangMode.AbsoluteCm, 0), gridStep: 10);

            Assert.True(fit.Feasible);
            Assert.Equal(0, fit.OffsetX);
            Assert.Equal(0, fit.OffsetY);
        }

        [Fact]
        public void FindBestPlacement_UpperLargerThanAnySupport_Infeasible()
        {
            // Lower box is too small to ever support the larger upper box within a zero-overhang budget.
            var pallet = new Pallet("Test", 100, 100, 10);
            var smallSku = CreateSku("Small", 20, 20);
            var bigSku = CreateSku("Big", 40, 40);

            var lowerLayer = CreateLayer("Lower",
                new PositionedItem(smallSku, 40, 40, rotated: false));
            var upperLayer = CreateLayer("Upper",
                new PositionedItem(bigSku, 0, 0, rotated: false));

            var fit = LayerSupportAnalyzer.FindBestPlacement(lowerLayer, upperLayer, pallet, new OverhangRule(OverhangMode.AbsoluteCm, 0), gridStep: 10);

            Assert.False(fit.Feasible);
            // Best it can do is rest the upper box squarely over the small support: 400 supported, 1200 over.
            Assert.Equal(1200, fit.Metrics.MaximumSkuOverhangArea, 3);
        }

        [Fact]
        public void FindBestPlacement_MinSupportedPercent_RejectsBelowThreshold()
        {
            // Upper 40x40 over a 20x20 support: best placement covers 400 of 1600 cm² ≈ 25%.
            var pallet = new Pallet("Test", 100, 100, 10);
            var support = CreateSku("S", 20, 20);
            var top = CreateSku("T", 40, 40);

            var lower = CreateLayer("Lower", new PositionedItem(support, 40, 40, rotated: false));
            var upper = CreateLayer("Upper", new PositionedItem(top, 0, 0, rotated: false));

            var clears = LayerSupportAnalyzer.FindBestPlacement(lower, upper, pallet, new OverhangRule(OverhangMode.MinSupportedPercent, 20), gridStep: 10);
            var fails = LayerSupportAnalyzer.FindBestPlacement(lower, upper, pallet, new OverhangRule(OverhangMode.MinSupportedPercent, 50), gridStep: 10);

            Assert.True(clears.Feasible);   // 25% clears a 20% floor
            Assert.False(fails.Feasible);   // ...but not a 50% floor
        }

        [Fact]
        public void FindBestPlacement_Auto_SupportedCentroidIsFeasible()
        {
            // Box centred on a central support cannot tip, even though most of it overhangs.
            var pallet = new Pallet("Test", 100, 100, 10);
            var support = CreateSku("S", 20, 20);
            var top = CreateSku("T", 40, 40);

            var lower = CreateLayer("Lower", new PositionedItem(support, 40, 40, rotated: false));
            var upper = CreateLayer("Upper", new PositionedItem(top, 0, 0, rotated: false));

            var auto = LayerSupportAnalyzer.FindBestPlacement(lower, upper, pallet, new OverhangRule(OverhangMode.Auto, 0), gridStep: 10);

            Assert.True(auto.Feasible);
        }

        [Fact]
        public void FindBestPlacement_Auto_NoSupportTips()
        {
            var pallet = new Pallet("Test", 100, 100, 10);
            var top = CreateSku("T", 40, 40);
            var upper = CreateLayer("Upper", new PositionedItem(top, 0, 0, rotated: false));

            var auto = LayerSupportAnalyzer.FindBestPlacement(null, upper, pallet, new OverhangRule(OverhangMode.Auto, 0), gridStep: 10);

            Assert.False(auto.Feasible);
        }

        private static SKU CreateSku(string id, int length, int width) => new()
        {
            SkuId = id,
            Name = id,
            Length = length,
            Width = width,
            Height = 10,
            Rotatable = false,
            Quantity = 1
        };

        private static Layer CreateLayer(string name, params PositionedItem[] items)
        {
            return new Layer(name, [.. items], new LayerMetadata(1.0, 1, name));
        }
    }
}
