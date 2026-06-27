using Google.OrTools.Sat;
using Stack_Solver.Models;
using Stack_Solver.Models.Assignment;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services
{
    /// <summary>
    /// Assigns pre-built pallet templates to demand using CP-SAT with strict
    /// three-tier lexicographic optimization: minimize leftovers (Phase 1),
    /// then total pallets (Phase 2), then distinct templates (Phase 3). Each
    /// phase pins the previous objective as an equality constraint before the
    /// next solve. A greedy AssignmentResult may be supplied as a warm-start;
    /// hints are best-effort and silently ignored by the solver if infeasible.
    /// </summary>
    public static class CPSATAssignmentService
    {
        public static AssignmentResult Assign(
            IReadOnlyList<PalletTemplate> templates,
            IReadOnlyDictionary<string, int> demand,
            Pallet pallet,
            GenerationOptions options,
            AssignmentResult? warmStart = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(templates);
            ArgumentNullException.ThrowIfNull(demand);
            ArgumentNullException.ThrowIfNull(pallet);
            ArgumentNullException.ThrowIfNull(options);

            if (templates.Count == 0 || demand.Count == 0)
                return new AssignmentResult();

            // Drop templates that pack SKUs not present in demand. Should not
            // arise in normal flow, but keeps the model self-consistent.
            var validTemplates = templates
                .Where(t => t.SkuCounts.Keys.All(demand.ContainsKey))
                .ToList();
            if (validTemplates.Count == 0)
                return new AssignmentResult();

            var skuOrder = demand.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            int dMax = Math.Max(1, demand.Values.Sum());

            var model = new CpModel();
            var x = new IntVar[validTemplates.Count];
            var y = new BoolVar[validTemplates.Count];

            for (int t = 0; t < validTemplates.Count; t++)
            {
                x[t] = model.NewIntVar(0, dMax, $"x_{t}");
                y[t] = model.NewBoolVar($"y_{t}");
                // y_t = 1  ⟺  x_t ≥ 1
                model.Add(x[t] >= 1).OnlyEnforceIf(y[t]);
                model.Add(x[t] == 0).OnlyEnforceIf(y[t].Not());
            }

            var l = new Dictionary<string, IntVar>(StringComparer.Ordinal);
            foreach (var sku in skuOrder)
                l[sku] = model.NewIntVar(0, demand[sku], $"l_{sku}");

            // Demand satisfaction with leftover slack: Σ_t a_{t,i} · x_t  +  l_i  =  d_i
            foreach (var sku in skuOrder)
            {
                var terms = new List<LinearExpr> { l[sku] };
                for (int t = 0; t < validTemplates.Count; t++)
                {
                    int count = validTemplates[t].SkuCounts.GetValueOrDefault(sku);
                    if (count > 0)
                        terms.Add(LinearExpr.Term(x[t], count));
                }
                model.Add(LinearExpr.Sum(terms) == demand[sku]);
            }

            if (warmStart != null)
                ApplyWarmStart(model, warmStart, validTemplates, x, y, l);

            int totalBudget = options.MaxSolverTime > 0 ? options.MaxSolverTime : 30;
            int phaseBudget = Math.Max(5, totalBudget / 3);

            var solver = new CpSolver
            {
                StringParameters = $"max_time_in_seconds:{phaseBudget},num_search_workers:8"
            };

            // Phase 1: minimize leftovers.
            ct.ThrowIfCancellationRequested();
            var leftoversSum = LinearExpr.Sum(l.Values);
            model.Minimize(leftoversSum);
            if (!Solved(solver.Solve(model))) return new AssignmentResult();
            long leftoversStar = (long)Math.Round(solver.ObjectiveValue);
            model.Add(leftoversSum == leftoversStar);

            // Phase 2: minimize total pallets.
            ct.ThrowIfCancellationRequested();
            var palletsSum = LinearExpr.Sum(x);
            model.Minimize(palletsSum);
            if (!Solved(solver.Solve(model))) return new AssignmentResult();
            long palletsStar = (long)Math.Round(solver.ObjectiveValue);
            model.Add(palletsSum == palletsStar);

            // Phase 3: minimize distinct templates.
            ct.ThrowIfCancellationRequested();
            model.Minimize(LinearExpr.Sum(y));
            if (!Solved(solver.Solve(model))) return new AssignmentResult();

            return ExtractResult(solver, validTemplates, x, l);
        }

        private static bool Solved(CpSolverStatus status) =>
            status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible;

        private static AssignmentResult ExtractResult(
            CpSolver solver,
            IReadOnlyList<PalletTemplate> templates,
            IntVar[] x,
            IReadOnlyDictionary<string, IntVar> l)
        {
            var assignments = new List<(PalletTemplate Template, int Count)>();
            for (int t = 0; t < templates.Count; t++)
            {
                int count = (int)solver.Value(x[t]);
                if (count > 0)
                    assignments.Add((templates[t], count));
            }

            var leftovers = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (sku, lVar) in l)
            {
                int count = (int)solver.Value(lVar);
                if (count > 0)
                    leftovers[sku] = count;
            }

            return new AssignmentResult { Assignments = assignments, Leftovers = leftovers };
        }

        private static void ApplyWarmStart(
            CpModel model,
            AssignmentResult warmStart,
            IReadOnlyList<PalletTemplate> templates,
            IntVar[] x,
            BoolVar[] y,
            IReadOnlyDictionary<string, IntVar> l)
        {
            // Greedy templates won't share IDs with the enumerator pool, so match
            // by SKU-count signature (the same signature the enumerator dedups on).
            var greedyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (template, count) in warmStart.Assignments)
            {
                var sig = Signature(template);
                greedyCounts[sig] = greedyCounts.GetValueOrDefault(sig) + count;
            }

            for (int t = 0; t < templates.Count; t++)
            {
                int hint = greedyCounts.GetValueOrDefault(Signature(templates[t]));
                model.AddHint(x[t], hint);
                model.AddHint(y[t], hint > 0 ? 1 : 0);
            }

            foreach (var (sku, count) in warmStart.Leftovers)
                if (l.TryGetValue(sku, out var lVar))
                    model.AddHint(lVar, count);
        }

        private static string Signature(PalletTemplate t) =>
            string.Join("|", t.SkuCounts
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}"));
    }
}
