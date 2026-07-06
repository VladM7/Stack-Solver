using Google.OrTools.LinearSolver;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Primal "price-and-branch" heuristic: solves the set-partition master as an integer program
    /// restricted to the columns generated so far. The LP relaxation gives a (weak) lower bound;
    /// this gives a strong integer <i>upper</i> bound — a real pallet assignment — that the search
    /// can adopt as its incumbent. When that incumbent matches the ⌈LP / combinatorial bound⌉, the
    /// root short-circuit certifies optimality without ever entering the branch-and-bound tree.
    ///
    /// <para>It is a heuristic only because it is limited to the current column pool: it cannot
    /// invent a pallet pattern that column generation has not produced. It never returns an
    /// infeasible or demand-violating assignment — leftover slack keeps it feasible, and the caller
    /// inspects the leftovers.</para>
    ///
    /// <para>Two optional knobs turn this same IP into the building block for lexicographic
    /// (priority-ordered, no invented weight) multi-objective solves: <paramref name="columnCost"/>
    /// [see <see cref="Solve"/>] swaps the per-column objective coefficient from the implicit "1
    /// pallet" to any other per-column cost (e.g. <see cref="PurityMetric.Impurity(BnpColumn)"/>),
    /// and <paramref name="palletCountCap"/> bounds Σx_t so a later stage cannot regress a bound
    /// already established by an earlier one. A caller runs this twice to get a lexicographic
    /// optimum: solve once for the primary metric, then again with that metric's optimal value
    /// capped and the secondary metric as the cost.</para>
    /// </summary>
    public static class RestrictedMasterHeuristic
    {
        /// <summary>
        /// Solves the integer master over <paramref name="columns"/> within <paramref name="timeLimit"/>.
        /// Returns the chosen pallet multiset and any unmet demand, or null when no MIP backend is
        /// available or no feasible integer solution is found in time.
        /// </summary>
        /// <param name="columnCost">
        /// Per-column objective coefficient, e.g. <see cref="PurityMetric.Impurity(BnpColumn)"/> to
        /// minimize impurity instead of pallet count. Defaults to a uniform 1.0 per column — the
        /// original "minimize pallets" objective — when omitted.
        /// </param>
        /// <param name="palletCountCap">
        /// When set, constrains Σx_t (total pallets, excluding leftover slack) to at most this
        /// value — e.g. a prior stage's proven-minimum pallet count, so a subsequent stage
        /// optimizing a different <paramref name="columnCost"/> cannot trade away that optimum.
        /// </param>
        public static Result? Solve(
            IReadOnlyList<BnpColumn> columns,
            IReadOnlyList<string> skuOrder,
            IReadOnlyDictionary<string, int> demand,
            TimeSpan timeLimit,
            CancellationToken ct,
            Func<BnpColumn, double>? columnCost = null,
            int? palletCountCap = null)
        {
            ArgumentNullException.ThrowIfNull(columns);
            ArgumentNullException.ThrowIfNull(skuOrder);
            ArgumentNullException.ThrowIfNull(demand);

            if (columns.Count == 0 || skuOrder.Count == 0) return null;
            if (timeLimit <= TimeSpan.Zero) return null;
            ct.ThrowIfCancellationRequested();

            columnCost ??= static _ => 1.0;

            // SCIP is OR-Tools' default MIP backend; fall back to CBC. Either may be absent in a
            // trimmed build, in which case the heuristic is simply skipped.
            using var solver = Solver.CreateSolver("SCIP") ?? Solver.CreateSolver("CBC");
            if (solver is null) return null;

            var objective = solver.Objective();
            objective.SetMinimization();

            // Big-M leftover penalty, larger than any achievable weighted column cost. Each pallet
            // holds ≥ 1 box, so the pallet count can never exceed Σ demand; the weighted cost of any
            // assignment is therefore bounded by maxColumnCost · Σ demand, and one more than that
            // strictly dominates it — so leftovers are eliminated before any cost (pallets, impurity,
            // or otherwise) is traded. With the default uniform cost of 1.0 this reduces to exactly
            // the original totalDemand + 1.
            long totalDemand = 0;
            foreach (var sku in skuOrder) totalDemand += demand.GetValueOrDefault(sku);
            double maxColumnCost = 0.0;
            foreach (var c in columns) maxColumnCost = Math.Max(maxColumnCost, columnCost(c));
            double leftoverPenalty = maxColumnCost * totalDemand + 1;

            var rows = new Dictionary<string, Constraint>(skuOrder.Count, StringComparer.Ordinal);
            foreach (var sku in skuOrder)
            {
                int d = demand.GetValueOrDefault(sku);
                var row = solver.MakeConstraint(d, d, $"demand_{sku}");
                var slack = solver.MakeIntVar(0, d, $"l_{sku}");
                row.SetCoefficient(slack, 1.0);
                objective.SetCoefficient(slack, leftoverPenalty);
                rows[sku] = row;
            }

            // Optional Σx_t ≤ cap (leftover slack excluded, matching RestrictedMasterProblem's
            // cardinality cap): bounds total pallets so a secondary-objective stage cannot regress a
            // pallet count already proven by an earlier stage.
            Constraint? cap = palletCountCap.HasValue
                ? solver.MakeConstraint(double.NegativeInfinity, palletCountCap.Value, "pallet_cap")
                : null;

            var vars = new Variable[columns.Count];
            for (int t = 0; t < columns.Count; t++)
            {
                var x = solver.MakeIntVar(0, double.PositiveInfinity, $"x{t}");
                objective.SetCoefficient(x, columnCost(columns[t]));
                cap?.SetCoefficient(x, 1.0);
                foreach (var sku in skuOrder)
                {
                    int a = columns[t].CountOf(sku);
                    if (a != 0) rows[sku].SetCoefficient(x, a);
                }
                vars[t] = x;
            }

            solver.SetTimeLimit((long)Math.Max(1, timeLimit.TotalMilliseconds));
            var status = solver.Solve();
            if (status is not (Solver.ResultStatus.OPTIMAL or Solver.ResultStatus.FEASIBLE))
                return null;

            var chosen = new List<(BnpColumn Column, int Count)>();
            for (int t = 0; t < columns.Count; t++)
            {
                int count = (int)Math.Round(vars[t].SolutionValue());
                if (count > 0) chosen.Add((columns[t], count));
            }

            var leftovers = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var sku in skuOrder)
            {
                int covered = 0;
                foreach (var (col, count) in chosen) covered += col.CountOf(sku) * count;
                int missing = demand.GetValueOrDefault(sku) - covered;
                if (missing > 0) leftovers[sku] = missing;
            }

            return new Result(chosen, leftovers);
        }

        /// <param name="Columns">Chosen pallet templates with their multiplicities.</param>
        /// <param name="Leftovers">Unmet demand per SKU (empty when the assignment covers everything).</param>
        public sealed record Result(
            IReadOnlyList<(BnpColumn Column, int Count)> Columns,
            IReadOnlyDictionary<string, int> Leftovers);
    }
}
