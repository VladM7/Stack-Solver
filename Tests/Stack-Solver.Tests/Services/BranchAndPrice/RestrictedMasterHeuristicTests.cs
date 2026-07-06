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

        [Fact]
        public void ColumnCost_PrefersLowerImpurityEvenAtMorePallets()
        {
            // Two pure pallets (impurity 0 each) vs one mixed pallet covering the same demand
            // (impurity 1). The default (pallet-count) objective would pick the 1-pallet mixed
            // solution; under columnCost = impurity the cheaper total is the 2 pure pallets
            // (0 + 0 = 0 < 1), even though that costs an extra pallet.
            var columns = new[] { Col(("A", 10)), Col(("B", 10)), Col(("A", 10), ("B", 10)) };
            var demand = new Dictionary<string, int> { ["A"] = 10, ["B"] = 10 };

            var result = RestrictedMasterHeuristic.Solve(
                columns, ["A", "B"], demand, Limit, TestContext.Current.CancellationToken,
                columnCost: c => PurityMetric.Impurity(c));

            Assert.NotNull(result);
            Assert.Empty(result.Leftovers);
            Assert.Equal(2, result.Columns.Sum(c => c.Count));
            Assert.All(result.Columns, c => Assert.Equal(0, PurityMetric.Impurity(c.Column)));
        }

        [Fact]
        public void ColumnCost_StillEliminatesLeftoverBeforeMinimizingCost()
        {
            // Even under an impurity objective that is 0 for every available column, the solver
            // must still place all coverable demand rather than leave any avoidable leftover —
            // the leftover penalty must dominate regardless of how the column cost is defined.
            var columns = new[] { Col(("A", 10)) };
            var demand = new Dictionary<string, int> { ["A"] = 10, ["B"] = 7 };

            var result = RestrictedMasterHeuristic.Solve(
                columns, ["A", "B"], demand, Limit, TestContext.Current.CancellationToken,
                columnCost: c => PurityMetric.Impurity(c));

            Assert.NotNull(result);
            Assert.Equal(7, result.Leftovers["B"]);
            Assert.False(result.Leftovers.ContainsKey("A"));
        }

        [Fact]
        public void PalletCountCap_BoundsTotalPalletsRegardlessOfObjective()
        {
            // Same instance as above, but capped at 1 pallet: minimizing impurity alone would
            // prefer the 2 pure pallets (cost 0 < 1), but the cap permits only 1 pallet, so the
            // solver must fall back to the single mixed pallet.
            var columns = new[] { Col(("A", 10)), Col(("B", 10)), Col(("A", 10), ("B", 10)) };
            var demand = new Dictionary<string, int> { ["A"] = 10, ["B"] = 10 };

            var result = RestrictedMasterHeuristic.Solve(
                columns, ["A", "B"], demand, Limit, TestContext.Current.CancellationToken,
                columnCost: c => PurityMetric.Impurity(c), palletCountCap: 1);

            Assert.NotNull(result);
            Assert.Empty(result.Leftovers);
            Assert.Equal(1, result.Columns.Sum(c => c.Count));
        }

        [Fact]
        public void PalletCountCap_ForcesLeftoverWhenNoAssignmentFitsTheCap()
        {
            // Only pure pallets available (no single pallet covers both SKUs); capping at 1
            // pallet makes full coverage infeasible, so leftover must appear rather than the
            // cap being violated.
            var columns = new[] { Col(("A", 10)), Col(("B", 10)) };
            var demand = new Dictionary<string, int> { ["A"] = 10, ["B"] = 10 };

            var result = RestrictedMasterHeuristic.Solve(
                columns, ["A", "B"], demand, Limit, TestContext.Current.CancellationToken,
                palletCountCap: 1);

            Assert.NotNull(result);
            Assert.True(result.Columns.Sum(c => c.Count) <= 1);
            Assert.NotEmpty(result.Leftovers);
        }
    }
}
