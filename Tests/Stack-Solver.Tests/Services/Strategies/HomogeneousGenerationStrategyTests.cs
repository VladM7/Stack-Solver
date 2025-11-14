using Stack_Solver.Models;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.Layering;
using Xunit;

namespace Stack_Solver.Tests.Strategies
{
    public class HomogeneousGenerationStrategyTests
    {
        [Fact]
        public void Name_IsExpected()
        {
            var strat = new HomogeneousGenerationStrategy();
            Assert.Equal("Homogeneous Grid", strat.Name);
        }

        [Fact]
        public void Generate_SingleRotatableSKU_ProducesNormalAndRotatedLayers()
        {
            var skus = new List<SKU>
            {
                new()
                {
                    SkuId = "A",
                    Name = "Box A",
                    Length = 40,
                    Width = 30,
                    Height = 10,
                    Quantity = 999,
                    Rotatable = true
                }
            };

            var pallet = new Pallet("Test Pallet", 120, 90, 14);

            var strat = new HomogeneousGenerationStrategy();
            var layers = strat.Generate(skus, pallet, new GenerationOptions());

            Assert.Equal(2, layers.Count);

            var normal = layers.Single(l => l.Name.EndsWith("_normal"));
            var rotated = layers.Single(l => l.Name.EndsWith("_rotated"));

            Assert.Equal(9, normal.Items.Count);
            Assert.Equal(1.0, normal.Metadata.Utilization, 5);
            Assert.Equal(10, normal.Metadata.Height);
            Assert.NotNull(normal.Geometry);
            Assert.Equal(90, normal.Geometry!.Width);
            Assert.Equal(120, normal.Geometry!.Length);
            int normalTrue = 0;
            foreach (var v in normal.Geometry!.OccupancyGrid) if (v) normalTrue++;
            Assert.Equal(120 * 90, normalTrue);
            Assert.All(normal.Items, it => Assert.False(it.Rotated));
            Assert.Contains(normal.Items, it => it.X == 0 && it.Y == 0);
            Assert.Contains(normal.Items, it => it.X == 80 && it.Y == 60);

            Assert.Equal(8, rotated.Items.Count);
            Assert.Equal(9600.0 / 10800.0, rotated.Metadata.Utilization, 6);
            Assert.Equal(10, rotated.Metadata.Height);
            Assert.NotNull(rotated.Geometry);
            int rotTrue = 0;
            foreach (var v in rotated.Geometry!.OccupancyGrid) if (v) rotTrue++;
            Assert.Equal(9600, rotTrue);
            Assert.All(rotated.Items, it => Assert.True(it.Rotated));
            Assert.Contains(rotated.Items, it => it.X == 0 && it.Y == 0);
            Assert.Contains(rotated.Items, it => it.X == 90 && it.Y == 40);
        }

        [Fact]
        public void Generate_NonRotatableSKU_ProducesSingleLayer()
        {
            var skus = new List<SKU>
            {
                new()
                {
                    SkuId = "B",
                    Name = "Box B",
                    Length = 25,
                    Width = 20,
                    Height = 7,
                    Quantity = 999,
                    Rotatable = false
                }
            };
            var pallet = new Pallet("Test Pallet", 100, 60, 14);

            var strat = new HomogeneousGenerationStrategy();
            var layers = strat.Generate(skus, pallet, new GenerationOptions());

            Assert.Single(layers);
            var layer = layers.Single();
            Assert.Equal(12, layer.Items.Count);
            Assert.All(layer.Items, it => Assert.False(it.Rotated));
        }

        [Fact]
        public void Generate_SquareSKU_NoRotatedVariant()
        {
            var skus = new List<SKU>
            {
                new()
                {
                    SkuId = "C",
                    Name = "Box C",
                    Length = 30,
                    Width = 30,
                    Height = 8,
                    Quantity = 999,
                    Rotatable = true
                }
            };
            var pallet = new Pallet("Test Pallet", 120, 90, 14);

            var strat = new HomogeneousGenerationStrategy();
            var layers = strat.Generate(skus, pallet, new GenerationOptions());

            Assert.Single(layers);
            var layer = layers.Single();
            Assert.Equal(12, layer.Items.Count);
            Assert.All(layer.Items, it => Assert.False(it.Rotated));
        }

        [Fact]
        public void Generate_SKUTooLarge_NoLayers()
        {
            var skus = new List<SKU>
            {
                new()
                {
                    SkuId = "D",
                    Name = "Huge",
                    Length = 200,
                    Width = 200,
                    Height = 50,
                    Rotatable = true
                }
            };
            var pallet = new Pallet("Small Pallet", 100, 100, 14);

            var strat = new HomogeneousGenerationStrategy();
            var layers = strat.Generate(skus, pallet, new GenerationOptions());

            Assert.Empty(layers);
        }

        [Fact]
        public void Generate_MultipleSKUs_ProducesLayersPerSKU()
        {
            var skus = new List<SKU>
            {
                new()
                {
                    SkuId = "A",
                    Name = "A",
                    Length = 20,
                    Width = 10,
                    Height = 5,
                    Rotatable = true
                },
                new()
                {
                    SkuId = "B",
                    Name = "B",
                    Length = 30,
                    Width = 30,
                    Height = 10,
                    Rotatable = true
                }
            };
            var pallet = new Pallet("Test Pallet", 100, 60, 14);

            var strat = new HomogeneousGenerationStrategy();
            var layers = strat.Generate(skus, pallet, new GenerationOptions());

            Assert.Equal(3, layers.Count);

            Assert.Contains(layers, l => l.Name.StartsWith("hom_grid_A_normal"));
            Assert.Contains(layers, l => l.Name.StartsWith("hom_grid_A_rotated"));
            Assert.Contains(layers, l => l.Name.StartsWith("hom_grid_B_normal"));
        }
    }
}
