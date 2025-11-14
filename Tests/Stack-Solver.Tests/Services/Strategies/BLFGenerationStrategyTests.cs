using Stack_Solver.Models;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.Layering;
using Xunit;


namespace Stack_Solver.Tests.Strategies
{
    public class BLFGenerationStrategyTests
    {
        [Fact]
        public void Generate_SingleSKU()
        {
            var skus = new List<SKU>
            {
                new() {
                    SkuId = "A",
                    Name = "Box A",
                    Length = 50,
                    Width = 30,
                    Height = 20,
                    Quantity = 500,
                    Rotatable = true
                }
            };

            var pallet = new Pallet("Standard Pallet", 800, 60, 14);

            var strategy = new BLFGenerationStrategy();
            var options = new GenerationOptions { };

            var layers = strategy.Generate(skus, pallet, options);

            Assert.NotEmpty(layers);
            layers.Sort((l1, l2) => l1.Items.Count.CompareTo(l2.Items.Count));
            Assert.Equal(32, layers.Last().Items.Count);
        }

        [Fact]
        public void Generate_MultipleSKUs()
        {
            var skus = new List<SKU>
            {
                new() {
                    SkuId = "A",
                    Name = "Box A",
                    Length = 21,
                    Width = 16,
                    Height = 20,
                    Quantity = 500,
                    Rotatable = true
                },
                new() {
                    SkuId = "B",
                    Name = "Box B",
                    Length = 52,
                    Width = 33,
                    Height = 20,
                    Quantity = 500,
                    Rotatable = true
                }
            };

            var pallet = new Pallet("Standard Pallet", 120, 100, 14);

            var strategy = new BLFGenerationStrategy();
            var options = new GenerationOptions { };

            var layers = strategy.Generate(skus, pallet, options);

            Assert.NotEmpty(layers);
            layers.Sort((l1, l2) => l1.Items.Count.CompareTo(l2.Items.Count));
            Assert.Equal(33, layers.Last().Items.Count);
            layers.Sort((l1, l2) => l1.Metadata.Utilization.CompareTo(l2.Metadata.Utilization));
            Assert.Equal(0.942, layers.Last().Metadata.Utilization, 3);
        }

        [Fact]
        public void Generate_ShouldRespectSKUQuantityLimits()
        {
            var skus = new List<SKU>
            {
                new SKU
                {
                    SkuId = "X",
                    Name = "Tiny Box",
                    Length = 100,
                    Width = 100,
                    Height = 50,
                    Quantity = 2,
                    Rotatable = true
                }
            };

            var pallet = new Pallet("Interesting Pallet", 300, 200, 14);
            var strategy = new BLFGenerationStrategy();

            var layers = strategy.Generate(skus, pallet, new GenerationOptions { });
            var totalPlaced = 0;

            foreach (var layer in layers)
                totalPlaced += layer.Items.Count(i => i.SkuType.SkuId == "X");

            Assert.True(totalPlaced <= 2);
        }
    }
}
