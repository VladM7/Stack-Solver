using Google.OrTools.LinearSolver;
using Stack_Solver.Models;
using Stack_Solver.Models.Assignment;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Assigns pallet templates to demand by branch-and-price (Dantzig–Wolfe decomposition
    /// with delayed column generation). Single objective: minimize the total number of
    /// pallets, with demand enforced as an equality in the LP. Physically unplaceable SKUs
    /// — and any sub-layer remainder that no available layer can tile — are reported as
    /// leftovers.
    ///
    /// <para><b>Status:</b> milestones 1–2 are implemented: the GLOP restricted master, the
    /// homogeneous seed pool, dual extraction, the heuristic pricing loop (column
    /// generation at the root), and an integer incumbent obtained by rounding the LP and
    /// greedily packing the residual. Exact pricing and branch-and-bound (provable
    /// optimality) arrive in later milestones.</para>
    /// </summary>
    public static class BranchAndPriceAssignmentService
    {
        private const int DefaultMaxIterations = 500;

        /// <summary>
        /// Runs root-node column generation and returns the best integer incumbent found by
        /// rounding the LP optimum and packing the residual, with leftovers for unplaceable
        /// SKUs and any untileable remainder.
        /// </summary>
        public static AssignmentResult Assign(
            IReadOnlyList<Layer> layers,
            IReadOnlyDictionary<string, int> demand,
            Pallet pallet,
            GenerationOptions options,
            AssignmentResult? warmStart = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(layers);
            ArgumentNullException.ThrowIfNull(demand);
            ArgumentNullException.ThrowIfNull(pallet);
            ArgumentNullException.ThrowIfNull(options);

            if (layers.Count == 0 || demand.Count == 0)
                return new AssignmentResult { Leftovers = ToLeftovers(demand) };

            return Solve(layers, demand, pallet, options, warmStart, ct).Result;
        }

        /// <summary>
        /// Full root solve: runs column generation, builds the integer incumbent, and returns
        /// it together with the certified LP lower bound and optimality gap.
        /// </summary>
        public static BranchAndPriceSolution Solve(
            IReadOnlyList<Layer> layers,
            IReadOnlyDictionary<string, int> demand,
            Pallet pallet,
            GenerationOptions options,
            AssignmentResult? warmStart = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(layers);
            ArgumentNullException.ThrowIfNull(demand);
            ArgumentNullException.ThrowIfNull(pallet);
            ArgumentNullException.ThrowIfNull(options);

            if (layers.Count == 0 || demand.Count == 0)
                return new BranchAndPriceSolution(new AssignmentResult { Leftovers = ToLeftovers(demand) }, 0, false);

            ct.ThrowIfCancellationRequested();
            var cg = RunColumnGeneration(layers, demand, pallet, ct);
            var result = BuildIncumbent(cg, demand, layers, pallet);

            double bound = double.IsNaN(cg.Objective) ? 0 : cg.Objective;
            return new BranchAndPriceSolution(result, bound, cg.BoundCertified);
        }

        /// <summary>Runs root-node column generation and returns the LP optimum and final pool.</summary>
        public static ColumnGenerationResult GenerateColumns(
            IReadOnlyList<Layer> layers,
            IReadOnlyDictionary<string, int> demand,
            Pallet pallet,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(layers);
            ArgumentNullException.ThrowIfNull(demand);
            ArgumentNullException.ThrowIfNull(pallet);
            return RunColumnGeneration(layers, demand, pallet, ct);
        }

        /// <summary>
        /// Seeds the pool, then alternates RMP solve ↔ pricing until no improving column
        /// exists. The fast heuristic pricer drives most iterations; when it is exhausted the
        /// exact pricer either supplies a missed column or certifies (when its search is
        /// exhaustive) that the LP optimum — a valid lower bound — has been reached.
        /// </summary>
        private static ColumnGenerationResult RunColumnGeneration(
            IReadOnlyList<Layer> layers,
            IReadOnlyDictionary<string, int> demand,
            Pallet pallet,
            CancellationToken ct)
        {
            var seed = ColumnSeeder.Seed(layers, demand, pallet);
            if (seed.PlaceableSkus.Count == 0)
                return new ColumnGenerationResult(new ColumnPool(), [], double.NaN, false, [], seed.UnplaceableSkus);

            var placeableDemand = seed.PlaceableSkus.ToDictionary(s => s, s => demand[s], StringComparer.Ordinal);

            var pool = new ColumnPool();
            pool.AddRange(seed.Columns);

            using var rmp = new RestrictedMasterProblem(seed.PlaceableSkus, placeableDemand);
            rmp.AddColumns(seed.Columns);

            var heuristic = new PricingSolver(layers, pallet);
            var exact = new ExactPricingSolver(layers, pallet);
            bool certified = false;

            for (int iter = 0; iter < DefaultMaxIterations; iter++)
            {
                ct.ThrowIfCancellationRequested();

                var status = rmp.Solve();
                if (status is not (Solver.ResultStatus.OPTIMAL or Solver.ResultStatus.FEASIBLE))
                    break;

                var duals = rmp.Duals();

                var added = new List<BnpColumn>();
                foreach (var c in heuristic.FindColumns(duals))
                    if (pool.TryAdd(c)) added.Add(c);

                if (added.Count > 0)
                {
                    rmp.AddColumns(added);
                    continue;
                }

                // Heuristic exhausted — call the exact pricer to either find a missed column
                // or certify LP optimality.
                ct.ThrowIfCancellationRequested();
                var exactColumn = exact.FindBestColumn(duals);
                if (exactColumn != null && pool.TryAdd(exactColumn))
                {
                    rmp.AddColumns([exactColumn]);
                    continue;
                }

                certified = exact.LastSearchExhaustive;
                break;
            }

            ct.ThrowIfCancellationRequested();
            bool solved = rmp.Solve() is Solver.ResultStatus.OPTIMAL or Solver.ResultStatus.FEASIBLE;

            return new ColumnGenerationResult(
                pool,
                solved ? rmp.PrimalSolution() : [],
                solved ? rmp.ObjectiveValue : double.NaN,
                certified && solved,
                seed.PlaceableSkus,
                seed.UnplaceableSkus);
        }

        /// <summary>
        /// Constructs an integer assignment: take ⌊x_t⌋ of each LP column (never exceeding
        /// remaining demand) to lay down the full pallets the relaxation favours — including
        /// mixed columns the greedy stacker would miss — then pack whatever remains with the
        /// greedy layer-stacker, which can build partial pallets down to layer granularity.
        /// Any sub-layer remainder, plus the unplaceable SKUs, becomes the leftovers.
        /// </summary>
        private static AssignmentResult BuildIncumbent(
            ColumnGenerationResult cg,
            IReadOnlyDictionary<string, int> demand,
            IReadOnlyList<Layer> layers,
            Pallet pallet)
        {
            var remaining = cg.PlaceableSkus.ToDictionary(s => s, s => demand[s], StringComparer.Ordinal);
            var assignments = new List<(PalletTemplate Template, int Count)>();

            foreach (var (column, value) in cg.Primal)
            {
                int copies = Math.Min((int)Math.Floor(value + 1e-9), MaxCopies(column, remaining));
                if (copies <= 0) continue;
                assignments.Add((column.Template, copies));
                Apply(column, copies, remaining);
            }

            // Residual: greedy layer-stacking handles granularity the LP columns cannot.
            var residual = GreedyAssignmentService.Assign(layers, remaining, pallet);
            assignments.AddRange(residual.Assignments);

            var merged = assignments
                .GroupBy(a => BnpColumn.BuildSignature(a.Template), StringComparer.Ordinal)
                .Select(g => (g.First().Template, Count: g.Sum(a => a.Count)))
                .ToList();

            var leftovers = new Dictionary<string, int>(residual.Leftovers, StringComparer.Ordinal);
            foreach (var sku in cg.UnplaceableSkus)
                if (demand.TryGetValue(sku, out int d) && d > 0) leftovers[sku] = d;

            return new AssignmentResult { Assignments = merged, Leftovers = leftovers };
        }

        /// <summary>Largest number of copies of <paramref name="column"/> that fits remaining demand.</summary>
        private static int MaxCopies(BnpColumn column, IReadOnlyDictionary<string, int> remaining)
        {
            int max = int.MaxValue;
            foreach (var (sku, count) in column.SkuCounts)
            {
                if (count <= 0) continue;
                int avail = remaining.GetValueOrDefault(sku);
                max = Math.Min(max, avail / count);
            }
            return max == int.MaxValue ? 0 : max;
        }

        private static void Apply(BnpColumn column, int copies, Dictionary<string, int> remaining)
        {
            foreach (var (sku, count) in column.SkuCounts)
                if (remaining.ContainsKey(sku))
                    remaining[sku] -= count * copies;
        }

        /// <summary>
        /// Builds the seed pool, solves the LP relaxation of the master over it, and returns
        /// the objective, demand-constraint duals, primal columns, and SKU placeability. This
        /// is the core of milestone 1 and the foundation for the column-generation loop.
        /// </summary>
        public static RelaxationResult SolveRelaxation(
            IReadOnlyList<Layer> layers,
            IReadOnlyDictionary<string, int> demand,
            Pallet pallet,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(layers);
            ArgumentNullException.ThrowIfNull(demand);
            ArgumentNullException.ThrowIfNull(pallet);

            var seed = ColumnSeeder.Seed(layers, demand, pallet);
            if (seed.PlaceableSkus.Count == 0)
            {
                return new RelaxationResult(
                    Solver.ResultStatus.OPTIMAL, 0.0,
                    new Dictionary<string, double>(StringComparer.Ordinal),
                    [], seed.Columns, seed.UnplaceableSkus);
            }

            ct.ThrowIfCancellationRequested();

            var placeableDemand = seed.PlaceableSkus.ToDictionary(
                s => s, s => demand[s], StringComparer.Ordinal);

            using var rmp = new RestrictedMasterProblem(seed.PlaceableSkus, placeableDemand);
            rmp.AddColumns(seed.Columns);

            var status = rmp.Solve();
            bool solved = status is Solver.ResultStatus.OPTIMAL or Solver.ResultStatus.FEASIBLE;

            return new RelaxationResult(
                status,
                solved ? rmp.ObjectiveValue : double.NaN,
                solved ? rmp.Duals() : new Dictionary<string, double>(StringComparer.Ordinal),
                solved ? rmp.PrimalSolution() : [],
                seed.Columns,
                seed.UnplaceableSkus);
        }

        private static IReadOnlyDictionary<string, int> ToLeftovers(IReadOnlyDictionary<string, int> demand) =>
            demand.Where(kvp => kvp.Value > 0)
                  .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
    }

    /// <param name="Status">GLOP result status of the relaxation solve.</param>
    /// <param name="Objective">LP objective Σ x_t (a lower bound on the pallet count); NaN if unsolved.</param>
    /// <param name="Duals">Demand-constraint duals π_i, keyed by SKU.</param>
    /// <param name="Primal">Columns with x_t &gt; 0 in the LP optimum, with their values.</param>
    /// <param name="SeedColumns">The homogeneous seed columns (one per placeable SKU).</param>
    /// <param name="UnplaceableSkus">SKUs that cannot be palletized under the constraints.</param>
    public sealed record RelaxationResult(
        Solver.ResultStatus Status,
        double Objective,
        IReadOnlyDictionary<string, double> Duals,
        IReadOnlyList<(BnpColumn Column, double Value)> Primal,
        IReadOnlyList<BnpColumn> SeedColumns,
        IReadOnlyList<string> UnplaceableSkus);

    /// <param name="Pool">All columns generated during root column generation.</param>
    /// <param name="Primal">Columns with x_t &gt; 0 in the LP optimum, with their values.</param>
    /// <param name="Objective">LP objective Σ x_t at the root (a lower bound on the pallet count); NaN if unsolved.</param>
    /// <param name="BoundCertified">True when the exact pricer proved no improving column exists, so <paramref name="Objective"/> is the true LP optimum.</param>
    /// <param name="PlaceableSkus">SKUs included in the master.</param>
    /// <param name="UnplaceableSkus">SKUs that cannot be palletized under the constraints.</param>
    public sealed record ColumnGenerationResult(
        ColumnPool Pool,
        IReadOnlyList<(BnpColumn Column, double Value)> Primal,
        double Objective,
        bool BoundCertified,
        IReadOnlyList<string> PlaceableSkus,
        IReadOnlyList<string> UnplaceableSkus);

    /// <param name="Result">The integer pallet assignment.</param>
    /// <param name="LowerBound">LP lower bound on the pallet count for the modeled (placeable) demand.</param>
    /// <param name="LowerBoundCertified">True when <paramref name="LowerBound"/> is the proven LP optimum.</param>
    public sealed record BranchAndPriceSolution(
        AssignmentResult Result,
        double LowerBound,
        bool LowerBoundCertified)
    {
        /// <summary>Pallets used by the integer solution.</summary>
        public int Pallets => Result.TotalPallets;

        /// <summary>Relative optimality gap (integer pallets vs LP bound); 0 when no bound is available.</summary>
        public double OptimalityGap =>
            LowerBound > 1e-9 ? (Pallets - LowerBound) / LowerBound : 0;
    }
}
