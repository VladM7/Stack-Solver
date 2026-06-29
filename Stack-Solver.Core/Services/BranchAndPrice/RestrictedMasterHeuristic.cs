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
    /// </summary>
    public static class RestrictedMasterHeuristic
    {
        /// <summary>
        /// Solves the integer master over <paramref name="columns"/> within <paramref name="timeLimit"/>.
        /// Returns the chosen pallet multiset and any unmet demand, or null when no MIP backend is
        /// available or no feasible integer solution is found in time.
        /// </summary>
        public static Result? Solve(
            IReadOnlyList<BnpColumn> columns,
            IReadOnlyList<string> skuOrder,
            IReadOnlyDictionary<string, int> demand,
            TimeSpan timeLimit,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(columns);
            ArgumentNullException.ThrowIfNull(skuOrder);
            ArgumentNullException.ThrowIfNull(demand);

            if (columns.Count == 0 || skuOrder.Count == 0) return null;
            if (timeLimit <= TimeSpan.Zero) return null;
            ct.ThrowIfCancellationRequested();

            // SCIP is OR-Tools' default MIP backend; fall back to CBC. Either may be absent in a
            // trimmed build, in which case the heuristic is simply skipped.
            using var solver = Solver.CreateSolver("SCIP") ?? Solver.CreateSolver("CBC");
            if (solver is null) return null;

            var objective = solver.Objective();
            objective.SetMinimization();

            // Big-M leftover penalty, larger than any achievable pallet count (≤ Σ demand), so the
            // optimizer drives leftovers to their minimum before trading any pallets.
            long totalDemand = 0;
            foreach (var sku in skuOrder) totalDemand += demand.GetValueOrDefault(sku);
            double leftoverPenalty = totalDemand + 1;

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

            var vars = new Variable[columns.Count];
            for (int t = 0; t < columns.Count; t++)
            {
                var x = solver.MakeIntVar(0, double.PositiveInfinity, $"x{t}");
                objective.SetCoefficient(x, 1.0);
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
