using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.Stacking;

namespace Services.Stacking
{
    public class StackMaterializerTests
    {
        [Fact]
        public void Materialize_ShiftsUpperLayerOntoLower_AndLeavesLowerUntouched()
        {
            var pallet = new Pallet("Test", 100, 100, 10);
            var sku = CreateSku("A", 40, 40);

            var lower = CreateLayer("Lower", new PositionedItem(sku, 30, 30, rotated: false));
            var upper = CreateLayer("Upper", new PositionedItem(sku, 0, 0, rotated: false));

            var placed = StackMaterializer.Materialize(pallet, [lower, upper], new OverhangRule(OverhangMode.AbsoluteCm, 0), gridStep: 10);

            Assert.Equal(2, placed.Count);
            // Lower placed as-is (same reference, untouched).
            Assert.Same(lower, placed[0]);
            // Upper shifted onto the support below.
            Assert.Equal(30, placed[1].Items[0].X);
            Assert.Equal(30, placed[1].Items[0].Y);
            // Original upper layer is not mutated.
            Assert.Equal(0, upper.Items[0].X);
            Assert.Equal(0, upper.Items[0].Y);
        }

        [Fact]
        public void Translate_ClonePreservesIdAndDoesNotMutateSource()
        {
            var pallet = new Pallet("Test", 100, 100, 10);
            var sku = CreateSku("A", 40, 40);
            var source = CreateLayer("Src", new PositionedItem(sku, 10, 10, rotated: false));

            var clone = StackMaterializer.Translate(source, 20, 30, pallet, gridStep: 10);

            Assert.Equal(source.Id, clone.Id);
            Assert.NotSame(source.Items[0], clone.Items[0]);
            Assert.Equal(30, clone.Items[0].X);
            Assert.Equal(40, clone.Items[0].Y);
            Assert.Equal(10, source.Items[0].X);
            Assert.Equal(10, source.Items[0].Y);
            Assert.NotNull(clone.Geometry);
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
            => new(name, [.. items], new LayerMetadata(1.0, 1, name));
    }
}
