using Google.OrTools.LinearSolver;
using Stack_Solver.Models;
using Stack_Solver.Models.Assignment;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Assigns pallet templates to demand by branch-and-price (Dantzig–Wolfe decomposition
    /// with delayed column generation, embedded in branch-and-bound). Objective: minimize the
    /// pallet count, with big-M leftover slack so the master is always feasible. Physically
    /// unplaceable SKUs — and any remainder that no column can tile — are reported as
    /// leftovers; the big-M penalty drives leftovers to their minimum before trading pallets.
    ///
    /// <para>Pipeline: GLOP restricted master (<see cref="RestrictedMasterProblem"/>),
    /// homogeneous seed (<see cref="ColumnSeeder"/>), heuristic + exact pricing
    /// (<see cref="PricingSolver"/>, <see cref="ExactPricingSolver"/>), and branch-and-bound
    /// over fractional column variables (<see cref="BranchAndPriceSearch"/>). The result is a
    /// proven optimum when the search and every node's exact pricing complete within budget;
    /// otherwise the best solution found, with <see cref="BranchAndPriceSolution.LowerBoundCertified"/>
    /// false. <see cref="GenerateColumns"/> exposes root-only column generation.</para>
    /// </summary>
    public static class BranchAndPriceAssignmentService
    {
        private const int DefaultMaxIterations = 500;
        private const long DefaultNodeBudget = 50_000;

        /// <summary>
        /// Runs branch-and-price and returns the integer assignment: a proven minimum-pallet
        /// solution when the exact-equality master is feasible and the search completes,
        /// otherwise the best incumbent found. Unplaceable SKUs (and, on the fallback path,
        /// any untileable remainder) are reported as leftovers.
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
        /// Full branch-and-price solve. Runs the branch-and-bound search; on success returns
        /// the proven optimum, otherwise the milestone-2 incumbent (which allows leftovers).
        /// Always reports the root LP lower bound and whether the result is a certified optimum.
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

            var seed = ColumnSeeder.Seed(layers, demand, pallet);
            if (seed.PlaceableSkus.Count == 0)
            {
                // Nothing can be palletized — trivially optimal with everything left over.
                return new BranchAndPriceSolution(
                    new AssignmentResult { Leftovers = UnplaceableLeftovers(demand, seed.UnplaceableSkus) }, 0, true);
            }

            var placeableDemand = seed.PlaceableSkus.ToDictionary(s => s, s => demand[s], StringComparer.Ordinal);

            var timeBudget = TimeSpan.FromSeconds(options.MaxSolverTime > 0 ? options.MaxSolverTime : 30);

            ct.ThrowIfCancellationRequested();
            using var search = new BranchAndPriceSearch(
                layers, seed.PlaceableSkus, placeableDemand, seed.Columns, pallet, DefaultNodeBudget, timeBudget, ct);
            search.Run();

            double bound = double.IsInfinity(search.RootBound) ? 0 : search.RootBound;

            var assignments = (search.OptimalColumns ?? [])
                .Select(c => (c.Column.Template, c.Count))
                .ToList();

            // Any sub-layer remainder B&P could not tile with full layers is placed on extra
            // pallets by a fast filler-aware greedy pass, so every placeable box ends up
            // somewhere (see FillerLayerGenerator). Feeding fillers into the optimizer itself
            // would explode the pricing search, so they are confined to this mop-up step.
            var placeableLeftovers = search.OptimalLeftovers.Where(kvp => kvp.Value > 0)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
            bool remainderPlaced = false;
            if (placeableLeftovers.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var fillerLayers = FillerLayerGenerator.Augment(layers, pallet);
                var remainder = GreedyAssignmentService.Assign(fillerLayers, placeableLeftovers, pallet);
                if (remainder.Assignments.Count > 0)
                {
                    assignments.AddRange(remainder.Assignments.Select(a => (a.Template, a.Count)));
                    remainderPlaced = true;
                }
                placeableLeftovers = remainder.Leftovers.Where(kvp => kvp.Value > 0)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
            }

            var merged = assignments
                .GroupBy(a => BnpColumn.BuildSignature(a.Template), StringComparer.Ordinal)
                .Select(g => (g.First().Template, Count: g.Sum(a => a.Count)))
                .ToList();

            var leftovers = new Dictionary<string, int>(placeableLeftovers, StringComparer.Ordinal);
            foreach (var (sku, count) in UnplaceableLeftovers(demand, seed.UnplaceableSkus))
                leftovers[sku] = count;

            // Appending heuristic remainder pallets means the total is no longer a proven optimum.
            bool certified = search.ProvedOptimal && !remainderPlaced;
            return new BranchAndPriceSolution(
                new AssignmentResult { Assignments = merged, Leftovers = leftovers }, bound, certified);
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

        private static Dictionary<string, int> UnplaceableLeftovers(
            IReadOnlyDictionary<string, int> demand, IReadOnlyList<string> unplaceable)
        {
            var leftovers = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var sku in unplaceable)
                if (demand.TryGetValue(sku, out int d) && d > 0) leftovers[sku] = d;
            return leftovers;
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
