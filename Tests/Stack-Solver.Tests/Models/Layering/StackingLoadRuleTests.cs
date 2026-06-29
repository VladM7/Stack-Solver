using Stack_Solver.Models.Layering;

namespace Models.Layering
{
    public class StackingLoadRuleTests
    {
        [Fact]
        public void Allows_EqualDensity_IsAllowed()
        {
            Assert.True(StackingLoadRule.Allows(lowerDensity: 0.003, upperDensity: 0.003, tolerance: 0));
        }

        [Fact]
        public void Allows_LighterOnHeavier_IsAllowedAtZeroTolerance()
        {
            // A less-dense layer on a denser one is always fine.
            Assert.True(StackingLoadRule.Allows(lowerDensity: 0.005, upperDensity: 0.003, tolerance: 0));
        }

        [Fact]
        public void Allows_DenserOnLighter_IsForbiddenAtZeroTolerance()
        {
            Assert.False(StackingLoadRule.Allows(lowerDensity: 0.003, upperDensity: 0.005, tolerance: 0));
        }

        [Fact]
        public void Allows_DenserOnLighter_WithinTolerance_IsAllowed()
        {
            // Upper is ~16% denser; a 20% tolerance permits it but a 10% one does not.
            double lower = 0.00296, upper = 0.00343;
            Assert.True(StackingLoadRule.Allows(lower, upper, tolerance: 0.20));
            Assert.False(StackingLoadRule.Allows(lower, upper, tolerance: 0.10));
        }

        [Fact]
        public void Allows_ManyLightBoxesVsFewHeavy_ComparesPressureNotTotalWeight()
        {
            // Layer L: 27 boxes, 27 kg over 9126 cm²  → density 0.00296 (high TOTAL weight).
            // Layer H: 10 boxes, 30 kg over 8740 cm²  → density 0.00343 (lower total, higher pressure).
            double lLight = 27.0 / 9126.0;
            double dHeavy = 30.0 / 8740.0;

            // Despite L having the larger total weight, H exerts more pressure, so H may NOT
            // sit on L at zero tolerance — and L (lower pressure) may always sit on H.
            Assert.False(StackingLoadRule.Allows(lLight, dHeavy, tolerance: 0));
            Assert.True(StackingLoadRule.Allows(dHeavy, lLight, tolerance: 0));
        }
    }
}
