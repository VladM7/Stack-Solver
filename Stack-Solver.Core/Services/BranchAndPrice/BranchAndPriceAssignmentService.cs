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
    /// pallets, with demand enforced as an exact equality. Physically unplaceable SKUs are
    /// reported as leftovers.
    ///
    /// <para><b>Milestone 1 status:</b> this implements the restricted master problem
    /// (GLOP), the homogeneous seed pool, and dual extraction. Pricing and branching arrive
    /// in later milestones; until then <see cref="Assign"/> returns the homogeneous baseline
    /// incumbent (one column per SKU, rounded up), which column generation will replace.</para>
    /// </summary>
    public static class BranchAndPriceAssignmentService
    {
        /// <summary>
        /// Provisional entry point matching the other assignment services. Returns the
        /// homogeneous baseline incumbent plus any unplaceable SKUs as leftovers.
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

            ct.ThrowIfCancellationRequested();
            var relaxation = SolveRelaxation(layers, demand, pallet, ct);

            // Baseline incumbent: each seed column covers one SKU, so ⌈d_i / capacity⌉
            // homogeneous pallets place every box of that SKU. Replaced once pricing and
            // integer branching are in place.
            var assignments = new List<(PalletTemplate Template, int Count)>();
            foreach (var column in relaxation.SeedColumns)
            {
                var sku = column.SkuCounts.Keys.First();
                int capacity = column.CountOf(sku);
                if (capacity <= 0) continue;
                int count = (demand[sku] + capacity - 1) / capacity;
                if (count > 0)
                    assignments.Add((column.Template, count));
            }

            var leftovers = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var sku in relaxation.UnplaceableSkus)
                if (demand.TryGetValue(sku, out int d) && d > 0)
                    leftovers[sku] = d;

            return new AssignmentResult { Assignments = assignments, Leftovers = leftovers };
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
}
