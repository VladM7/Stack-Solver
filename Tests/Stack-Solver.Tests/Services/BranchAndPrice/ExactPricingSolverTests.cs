using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.BranchAndPrice;
using Stack_Solver.Services.Layering;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// Milestone 3: the exact pricer must return the maximum-value valid column (never worse
    /// than the heuristic), report nothing when no column beats the threshold, and — when its
    /// search is exhaustive — certify LP optimality so the bound is valid.
    /// </summary>
    public class ExactPricingSolverTests
    {
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

        private static double Value(BnpColumn c, IReadOnlyDictionary<string, double> duals) =>
            c.SkuCounts.Sum(kvp => kvp.Value * duals[kvp.Key]);

        [Fact]
        public void FindBestColumn_ZeroDuals_ReturnsNull()
        {
            var (layers, pallet) = TwoSkuLayers();
            var duals = new Dictionary<string, double> { ["A"] = 0.0, ["B"] = 0.0 };

            var exact = new ExactPricingSolver(layers, pallet);
            Assert.Null(exact.FindBestColumn(duals));
            Assert.True(exact.LastSearchExhaustive);
        }

        [Fact]
        public void FindBestColumn_ReturnsImprovingColumnAndCompletesExhaustively()
        {
            var (layers, pallet) = TwoSkuLayers();
            var duals = new Dictionary<string, double> { ["A"] = 0.1, ["B"] = 0.1 };

            var exact = new ExactPricingSolver(layers, pallet);
            var column = exact.FindBestColumn(duals);

            Assert.NotNull(column);
            Assert.True(Value(column!, duals) > 1.0);
            Assert.True(exact.LastSearchExhaustive);
        }

        [Fact]
        public void FindBestColumn_RaisedThreshold_SuppressesAColumnThatPricesOutAtOne()
        {
            var (layers, pallet) = TwoSkuLayers();
            var duals = new Dictionary<string, double> { ["A"] = 0.1, ["B"] = 0.1 };
            var exact = new ExactPricingSolver(layers, pallet);

            // At the default threshold the best column is improving (value > 1).
            var atOne = exact.FindBestColumn(duals, reducedCostThreshold: 1.0);
            Assert.NotNull(atOne);
            double bestValue = Value(atOne!, duals);
            Assert.True(bestValue > 1.0);

            // Raising the threshold above that value (as a cardinality dual would) makes it
            // non-improving, so nothing is returned — but the search is still exhaustive.
            var raised = exact.FindBestColumn(duals, reducedCostThreshold: bestValue + 1.0);
            Assert.Null(raised);
            Assert.True(exact.LastSearchExhaustive);
        }

        [Fact]
        public void FindBestColumn_IsNeverWorseThanHeuristic()
        {
            var (layers, pallet) = TwoSkuLayers();
            var duals = new Dictionary<string, double> { ["A"] = 0.1, ["B"] = 0.1 };

            var exactColumn = new ExactPricingSolver(layers, pallet).FindBestColumn(duals);
            var heuristicBest = new PricingSolver(layers, pallet) { MaxColumns = 64 }
                .FindColumns(duals)
                .Select(c => Value(c, duals))
                .DefaultIfEmpty(0)
                .Max();

            Assert.NotNull(exactColumn);
            // The exact pricer is unbounded in layer count, so it finds at least as much value.
            Assert.True(Value(exactColumn!, duals) >= heuristicBest - 1e-9);
        }

        [Fact]
        public void Solve_SmallInstance_CertifiesLowerBoundAndReportsNonNegativeGap()
        {
            // One full pallet of each SKU → the root LP is integer, so the proven optimum is 2.
            var skus = new List<SKU>
            {
                new() { SkuId = "A", Name = "A", Length = 40, Width = 30, Height = 10, Quantity = 144, Rotatable = true },
                new() { SkuId = "B", Name = "B", Length = 30, Width = 30, Height = 8,  Quantity = 240, Rotatable = true },
            };
            var pallet = new Pallet("P", 120, 90, 14);
            var demand = new Dictionary<string, int> { ["A"] = 144, ["B"] = 240 };
            var layers = new HomogeneousGenerationStrategy().Generate(skus, pallet, new GenerationOptions());

            var solution = BranchAndPriceAssignmentService.Solve(
                layers, demand, pallet, new GenerationOptions(), ct: TestContext.Current.CancellationToken);

            Assert.True(solution.LowerBoundCertified);
            Assert.True(solution.LowerBound > 0);
            Assert.Equal(2, solution.Pallets);
            Assert.Empty(solution.Result.Leftovers);
            Assert.True(solution.OptimalityGap >= -1e-9);
        }
    }
}
