using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services;

namespace Services
{
    public class LayerMetricsCalculatorTests
    {
        [Fact]
        public void Compute_SetsFootprintAreaAndLoadDensity()
        {
            var pallet = new Pallet("P", 100, 100, 10);
            // Two 40x30 boxes (1200 cm² each), 6 kg each → 2400 cm² footprint, 12 kg, density 0.005.
            var sku = new SKU { SkuId = "A", Name = "A", Length = 40, Width = 30, Height = 10, Weight = 6, Quantity = 2 };
            var layer = new Layer("L",
                [new PositionedItem(sku, 0, 0, rotated: false), new PositionedItem(sku, 50, 0, rotated: false)],
                new LayerMetadata(10, 1, "L"));

            var metrics = LayerMetricsCalculator.Compute(layer, pallet);

            Assert.Equal(2400, metrics.FootprintArea, 3);
            Assert.Equal(12, metrics.TotalWeight, 3);
            Assert.Equal(12.0 / 2400.0, metrics.LoadDensity, 6);
        }

        [Fact]
        public void Compute_EmptyOrWeightlessLayer_HasZeroLoadDensity()
        {
            var pallet = new Pallet("P", 100, 100, 10);
            var sku = new SKU { SkuId = "A", Name = "A", Length = 40, Width = 30, Height = 10, Weight = 0, Quantity = 1 };
            var layer = new Layer("L",
                [new PositionedItem(sku, 0, 0, rotated: false)],
                new LayerMetadata(10, 1, "L"));

            var metrics = LayerMetricsCalculator.Compute(layer, pallet);

            Assert.Equal(1200, metrics.FootprintArea, 3);
            Assert.Equal(0, metrics.LoadDensity, 6);
        }
    }
}
