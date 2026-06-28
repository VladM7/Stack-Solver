using Stack_Solver.Helpers.Rendering;

namespace Helpers.Rendering
{
    public class FaceCullerTests
    {
        [Fact]
        public void Compute_SingleBox_AllFacesVisible()
        {
            var boxes = new[] { new BoxBounds(0, 10, 0, 10, 0, 10) };

            var masks = FaceCuller.Compute(boxes);

            Assert.Equal(BoxFaces.All, masks[0]);
        }

        [Fact]
        public void Compute_TwoBoxesTouchingAlongX_SharedFacesCulled()
        {
            var boxes = new[]
            {
                new BoxBounds(0, 10, 0, 10, 0, 10),  // left box
                new BoxBounds(10, 20, 0, 10, 0, 10), // right box, touching at x = 10
            };

            var masks = FaceCuller.Compute(boxes);

            Assert.False(masks[0].HasFlag(BoxFaces.Right)); // covered by right box
            Assert.False(masks[1].HasFlag(BoxFaces.Left));  // covered by left box
            // every other face stays visible
            Assert.True(masks[0].HasFlag(BoxFaces.Left) && masks[0].HasFlag(BoxFaces.Top) && masks[0].HasFlag(BoxFaces.Front));
            Assert.True(masks[1].HasFlag(BoxFaces.Right) && masks[1].HasFlag(BoxFaces.Top) && masks[1].HasFlag(BoxFaces.Back));
        }

        [Fact]
        public void Compute_FullyEnclosedBox_NoFacesVisible()
        {
            var boxes = new[]
            {
                new BoxBounds(1, 2, 1, 2, 1, 2), // center
                new BoxBounds(2, 3, 1, 2, 1, 2), // +X
                new BoxBounds(0, 1, 1, 2, 1, 2), // -X
                new BoxBounds(1, 2, 2, 3, 1, 2), // +Y
                new BoxBounds(1, 2, 0, 1, 1, 2), // -Y
                new BoxBounds(1, 2, 1, 2, 2, 3), // +Z
                new BoxBounds(1, 2, 1, 2, 0, 1), // -Z
            };

            var masks = FaceCuller.Compute(boxes);

            Assert.Equal(BoxFaces.None, masks[0]);
        }

        [Fact]
        public void Compute_BottomRestingOnPallet_BottomCulled()
        {
            var boxes = new[] { new BoxBounds(10, 30, 10, 20, 10, 30) };

            var masks = FaceCuller.Compute(boxes, palletTopY: 10, palletMaxX: 100, palletMaxZ: 100);

            Assert.False(masks[0].HasFlag(BoxFaces.Bottom));
            Assert.True(masks[0].HasFlag(BoxFaces.Top));
            Assert.True(masks[0].HasFlag(BoxFaces.Left) && masks[0].HasFlag(BoxFaces.Right));
        }

        [Fact]
        public void Compute_FaceCoveredByTwoNeighborsJointly_Culled()
        {
            // Self's +X face spans Y[0,2]; neither neighbor covers it alone, together they do.
            var boxes = new[]
            {
                new BoxBounds(0, 1, 0, 2, 0, 1),  // self
                new BoxBounds(1, 2, 0, 1, 0, 1),  // covers lower half
                new BoxBounds(1, 2, 1, 2, 0, 1),  // covers upper half
            };

            var masks = FaceCuller.Compute(boxes);

            Assert.False(masks[0].HasFlag(BoxFaces.Right));
        }

        [Fact]
        public void Compute_FacePartiallyCovered_StaysVisible()
        {
            // Single neighbor only covers half of self's +X face -> face must remain.
            var boxes = new[]
            {
                new BoxBounds(0, 1, 0, 2, 0, 1), // self, face spans Y[0,2]
                new BoxBounds(1, 2, 0, 1, 0, 1), // covers only Y[0,1]
            };

            var masks = FaceCuller.Compute(boxes);

            Assert.True(masks[0].HasFlag(BoxFaces.Right));
        }

        [Fact]
        public void Compute_NeighborOnSameSideNotTouching_DoesNotCull()
        {
            // Gap between boxes -> no occlusion.
            var boxes = new[]
            {
                new BoxBounds(0, 10, 0, 10, 0, 10),
                new BoxBounds(11, 20, 0, 10, 0, 10), // 1-unit gap
            };

            var masks = FaceCuller.Compute(boxes);

            Assert.True(masks[0].HasFlag(BoxFaces.Right));
            Assert.True(masks[1].HasFlag(BoxFaces.Left));
        }
    }
}
