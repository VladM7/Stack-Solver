using Stack_Solver.Models.Supports;
using Stack_Solver.Services.BranchAndPrice;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// The restricted-master IP must return the minimum-pallet integer assignment expressible in
    /// the given column pool, and honestly report any demand the pool cannot cover. These tests
    /// also confirm a MIP backend (SCIP/CBC) is present in the build.
    /// </summary>
    public class RestrictedMasterHeuristicTests
    {
        private static BnpColumn Col(params (string Sku, int Count)[] counts) =>
            new(new PalletTemplate
            {
                SkuCounts = counts.ToDictionary(c => c.Sku, c => c.Count, StringComparer.Ordinal)
            });

        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

        [Fact]
        public void PrefersTheSinglePalletThatCoversAllDemand()
        {
            // Two pure pallets (2 pallets) vs one mixed full-capacity pallet (1 pallet).
            var columns = new[] { Col(("A", 10)), Col(("B", 10)), Col(("A", 10), ("B", 10)) };
            var demand = new Dictionary<string, int> { ["A"] = 10, ["B"] = 10 };

            var result = RestrictedMasterHeuristic.Solve(columns, ["A", "B"], demand, Limit, TestContext.Current.CancellationToken);

            Assert.NotNull(result); // a null here would mean no MIP backend loaded
            Assert.Empty(result.Leftovers);
            Assert.Equal(1, result.Columns.Sum(c => c.Count));
        }

        [Fact]
        public void MinimizesPalletCountOverTheColumnChoices()
        {
            // Covering 10 of A: one big pallet beats two small ones.
            var columns = new[] { Col(("A", 5)), Col(("A", 10)) };
            var demand = new Dictionary<string, int> { ["A"] = 10 };

            var result = RestrictedMasterHeuristic.Solve(columns, ["A"], demand, Limit, TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Empty(result.Leftovers);
            Assert.Equal(1, result.Columns.Sum(c => c.Count));
        }

        [Fact]
        public void ReportsLeftoverWhenThePoolCannotCoverDemand()
        {
            // No column carries B, so B is reported as unmet rather than silently dropped.
            var columns = new[] { Col(("A", 10)) };
            var demand = new Dictionary<string, int> { ["A"] = 10, ["B"] = 7 };

            var result = RestrictedMasterHeuristic.Solve(columns, ["A", "B"], demand, Limit, TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal(7, result.Leftovers["B"]);
            Assert.False(result.Leftovers.ContainsKey("A"));
        }
    }
}
