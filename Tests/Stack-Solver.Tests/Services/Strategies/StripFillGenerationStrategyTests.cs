using Stack_Solver.Models;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.Layering;

namespace Stack_Solver.Tests.Strategies
{
    public class StripFillGenerationStrategyTests
    {
        [Fact]
        public void Name_IsExpected()
        {
            var strat = new StripFillGenerationStrategy();
            Assert.Equal("Strip-Fill", strat.Name);
        }

        [Fact]
        public void Generate_SingleNonRotatableSKU_DeterministicCountsAndUtilization()
        {
            var skus = new List<SKU>
            {
                new()
                {
                    SkuId = "A",
                    Name = "A",
                    Length = 40,
                    Width = 30,
                    Height = 10,
                    Quantity = 999,
                    Rotatable = false
                }
            };

            var pallet = new Pallet("Test Pallet", 120, 90, 14);
            var strat = new StripFillGenerationStrategy();
            var layers = strat.Generate(skus, pallet, new GenerationOptions());

            Assert.Equal(3, layers.Count);
            var counts = layers.Select(l => l.Items.Count).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { 3, 6, 9 }, counts);

            var best = layers.MaxBy(l => l.Metadata.Utilization)!;
            Assert.Equal(9, best.Items.Count);
            Assert.Equal(1.0, best.Metadata.Utilization, 6);
            Assert.Equal(10, best.Metadata.Height);

            Assert.NotNull(best.Geometry);
            Assert.Equal(90, best.Geometry!.Width);
            Assert.Equal(120, best.Geometry!.Length);

            var trueCells = 0;
            foreach (var v in best.Geometry!.OccupancyGrid) if (v) trueCells++;
            var sumAreas = best.Geometry!.ItemRectangles.Sum(r => (int)(r.Width * r.Height));
            Assert.Equal(sumAreas, trueCells);

            Assert.All(best.Items, it => Assert.False(it.Rotated));
            Assert.All(best.Items, it => Assert.InRange(it.X + (it.Rotated ? it.SkuType.Width : it.SkuType.Length), 0, pallet.Length));
            Assert.All(best.Items, it => Assert.InRange(it.Y + (it.Rotated ? it.SkuType.Length : it.SkuType.Width), 0, pallet.Width));
        }

        [Fact]
        public void Generate_RotationFlagRespected_WhenRotatableFalse_NoRotatedPlacements()
        {
            var skus = new List<SKU>
            {
                new()
                {
                    SkuId = "B",
                    Name = "B",
                    Length = 50,
                    Width = 30,
                    Height = 7,
                    Quantity = 999,
                    Rotatable = false
                }
            };
            var pallet = new Pallet("Pallet", 200, 120, 14);
            var strat = new StripFillGenerationStrategy();
            var layers = strat.Generate(skus, pallet, new GenerationOptions());

            Assert.NotEmpty(layers);
            Assert.All(layers.SelectMany(l => l.Items), it => Assert.False(it.Rotated));
        }

        [Fact]
        public void Generate_MultipleSKUs_UniqueCompositionsAndInBounds()
        {
            var skus = new List<SKU>
            {
                new()
                {
                    SkuId = "A",
                    Name = "A",
                    Length = 40,
                    Width = 30,
                    Height = 10,
                    Quantity = 999,
                    Rotatable = true
                },
                new()
                {
                    SkuId = "C",
                    Name = "C",
                    Length = 30,
                    Width = 20,
                    Height = 8,
                    Quantity = 999,
                    Rotatable = true
                }
            };
            var pallet = new Pallet("Pallet", 180, 100, 14);
            var strat = new StripFillGenerationStrategy();
            var layers = strat.Generate(skus, pallet, new GenerationOptions());

            Assert.NotEmpty(layers);

            var keys = layers.Select(l => string.Join(",", l.Items
                .GroupBy(i => i.SkuType.SkuId)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key}:{g.Count()}")));
            Assert.Equal(keys.Count(), keys.Distinct().Count());

            foreach (var layer in layers)
            {
                Assert.NotNull(layer.Geometry);
                Assert.Equal(pallet.Width, layer.Geometry!.Width);
                Assert.Equal(pallet.Length, layer.Geometry!.Length);

                foreach (var it in layer.Items)
                {
                    var xSpan = it.Rotated ? it.SkuType.Width : it.SkuType.Length;
                    var ySpan = it.Rotated ? it.SkuType.Length : it.SkuType.Width;
                    Assert.InRange(it.X, 0, pallet.Length - xSpan);
                    Assert.InRange(it.Y, 0, pallet.Width - ySpan);
                }

                var trueCells = 0;
                foreach (var v in layer.Geometry!.OccupancyGrid) if (v) trueCells++;
                var sumAreas = layer.Geometry!.ItemRectangles.Sum(r => (int)(r.Width * r.Height));
                Assert.Equal(sumAreas, trueCells);
            }
        }

        [Fact]
        public void Generate_SKUTooLarge_NoLayers()
        {
            var skus = new List<SKU>
            {
                new()
                {
                    SkuId = "X",
                    Name = "Huge",
                    Length = 200,
                    Width = 200,
                    Height = 50,
                    Rotatable = true
                }
            };
            var pallet = new Pallet("Small", 100, 100, 14);
            var strat = new StripFillGenerationStrategy();

            var layers = strat.Generate(skus, pallet, new GenerationOptions());
            Assert.Empty(layers);
        }
    }
}
