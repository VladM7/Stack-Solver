using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.BranchAndPrice;
using Stack_Solver.Services.Layering;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// Milestone 2: the heuristic pricer must only ever return columns with dual value &gt; 1
    /// (negative reduced cost), find an improving column when one exists, and return nothing
    /// when the duals give every layer zero value.
    /// </summary>
    public class PricingSolverTests
    {
        // A: 40x30 → 9 boxes per layer; B: 30x30 → 12 boxes per layer; both fully cover the
        // 120x90 footprint, so any stacking order is supported and weight ordering is trivial.
        private static (List<Layer> Layers, Pallet Pallet) TwoSkuLayers()
        {
            var skus = new List<SKU>
            {
                new() { SkuId = "A", Name = "A", Length = 40, Width = 30, Height = 10, Quantity = 999, Rotatable = true },
                new() { SkuId = "B", Name = "B", Length = 30, Width = 30, Height = 8,  Quantity = 999, Rotatable = true },
            };
            var pallet = new Pallet("P", 120, 90, 14);
            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());
            return (layers, pallet);
        }

        [Fact]
        public void FindColumns_ZeroDuals_ReturnsNothing()
        {
            var (layers, pallet) = TwoSkuLayers();
            var duals = new Dictionary<string, double> { ["A"] = 0.0, ["B"] = 0.0 };

            var columns = new PricingSolver(layers, pallet).FindColumns(duals);

            Assert.Empty(columns);
        }

        [Fact]
        public void FindColumns_ProfitableDuals_ReturnsOnlyImprovingColumns()
        {
            var (layers, pallet) = TwoSkuLayers();
            var duals = new Dictionary<string, double> { ["A"] = 0.1, ["B"] = 0.1 };

            var columns = new PricingSolver(layers, pallet).FindColumns(duals);

            Assert.NotEmpty(columns);
            foreach (var col in columns)
            {
                double value = col.SkuCounts.Sum(kvp => kvp.Value * duals[kvp.Key]);
                Assert.True(value > 1.0, $"column value {value} must exceed 1");
            }
        }

        [Fact]
        public void FindColumns_CanCombineTwoSkusIntoOneColumn()
        {
            var (layers, pallet) = TwoSkuLayers();
            // High duals so a single mixed A+B stack already clears the threshold.
            var duals = new Dictionary<string, double> { ["A"] = 0.1, ["B"] = 0.1 };

            var columns = new PricingSolver(layers, pallet) { MaxColumns = 32 }.FindColumns(duals);

            Assert.Contains(columns, c => c.SkuCounts.ContainsKey("A") && c.SkuCounts.ContainsKey("B"));
        }

        [Fact]
        public void FindColumns_SkipsLayersWithUnmodeledSkus()
        {
            var (layers, pallet) = TwoSkuLayers();
            // B is not in the master; no returned column may contain it.
            var duals = new Dictionary<string, double> { ["A"] = 0.2 };

            var columns = new PricingSolver(layers, pallet).FindColumns(duals);

            Assert.NotEmpty(columns);
            Assert.All(columns, c => Assert.False(c.SkuCounts.ContainsKey("B")));
        }
    }
}
