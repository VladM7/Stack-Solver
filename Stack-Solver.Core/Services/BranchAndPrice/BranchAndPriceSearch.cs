using System.Diagnostics;
using Google.OrTools.LinearSolver;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Branch-and-price: branch-and-bound in which every node's LP relaxation is solved by
    /// column generation. Branching bounds a fractional column variable — the down-branch
    /// x_t ≤ ⌊v⌋ additionally forbids the column when ⌊v⌋ = 0, so the pricers cannot
    /// regenerate it (both the heuristic and exact pricers honour the forbidden set, keeping
    /// the per-node bound valid). Column variables and bounds are restored on backtrack, and
    /// the column pool is shared across the whole tree.
    ///
    /// <para>Yields a provably optimal integer solution (minimum pallets, all placeable
    /// demand placed) when the exact-equality master is feasible and both the tree and every
    /// node's exact pricing complete within budget. Otherwise it returns the best integer
    /// solution found, with <see cref="ProvedOptimal"/> false.</para>
    /// </summary>
    internal sealed class BranchAndPriceSearch : IDisposable
    {
        private const int MaxCgIterations = 500;
        private const double IntTol = 1e-6;

        private readonly RestrictedMasterProblem _rmp;
        private readonly ColumnPool _pool = new();
        private readonly PricingSolver _heuristic;
        private readonly ExactPricingSolver _exact;
        private readonly HashSet<string> _forbidden = new(StringComparer.Ordinal);
        private readonly CancellationToken _ct;

        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly TimeSpan _timeBudget;
        private long _nodeBudget;
        private bool _completed = true;
        private bool _allCertified = true;

        private double _bestObjective = double.PositiveInfinity;
        private List<(BnpColumn Column, int Count)>? _best;
        private IReadOnlyDictionary<string, int> _bestLeftovers = new Dictionary<string, int>(StringComparer.Ordinal);

        public BranchAndPriceSearch(
            IReadOnlyList<Layer> layers,
            IReadOnlyList<string> placeableSkus,
            IReadOnlyDictionary<string, int> placeableDemand,
            IReadOnlyList<BnpColumn> seedColumns,
            Pallet pallet,
            long nodeBudget,
            TimeSpan timeBudget,
            CancellationToken ct)
        {
            _ct = ct;
            _nodeBudget = nodeBudget;
            _timeBudget = timeBudget;
            _rmp = new RestrictedMasterProblem(placeableSkus, placeableDemand);
            _pool.AddRange(seedColumns);
            _rmp.AddColumns(seedColumns);
            _heuristic = new PricingSolver(layers, pallet);
            _exact = new ExactPricingSolver(layers, pallet);
        }

        /// <summary>Certified-or-not LP lower bound at the root (∞ if the root is infeasible).</summary>
        public double RootBound { get; private set; }

        public bool RootCertified { get; private set; }

        /// <summary>The optimal (or best-found) integer column multiset, or null if none was found.</summary>
        public IReadOnlyList<(BnpColumn Column, int Count)>? OptimalColumns => _best;

        /// <summary>Leftover (unmet) units per SKU in the returned solution.</summary>
        public IReadOnlyDictionary<string, int> OptimalLeftovers => _bestLeftovers;

        /// <summary>True only when the returned solution is a proven optimum.</summary>
        public bool ProvedOptimal => _completed && _allCertified && _best != null;

        public void Run()
        {
            var root = SolveNode();
            RootBound = root.Feasible ? root.Primal.Sum(p => p.Value) : double.PositiveInfinity;
            RootCertified = root.Certified;
            BranchAndBound(root);
        }

        private void BranchAndBound(NodeSolve node)
        {
            _ct.ThrowIfCancellationRequested();
            if (!node.Feasible) return;

            // Prune: this node's LP objective cannot beat the incumbent objective.
            if (node.Objective >= _bestObjective - IntTol) return;

            if (node.Integral)
            {
                _bestObjective = node.Objective;
                _best = node.Primal
                    .Select(p => (p.Column, Count: (int)Math.Round(p.Value)))
                    .Where(p => p.Count > 0)
                    .ToList();
                _bestLeftovers = node.Leftovers;
                return;
            }

            var (col, value) = MostFractional(node.Primal);
            string sig = col.Signature;
            int floorV = (int)Math.Floor(value);

            // Down branch: x_t ≤ ⌊v⌋ (forbid the column entirely when ⌊v⌋ = 0).
            if (TakeNode())
            {
                var (lb, ub) = _rmp.GetBounds(sig);
                _rmp.SetBounds(sig, lb, floorV);
                bool forbade = floorV == 0 && _forbidden.Add(sig);
                BranchAndBound(SolveNode());
                if (forbade) _forbidden.Remove(sig);
                _rmp.SetBounds(sig, lb, ub);
            }

            // Up branch: x_t ≥ ⌈v⌉.
            if (TakeNode())
            {
                var (lb, ub) = _rmp.GetBounds(sig);
                _rmp.SetBounds(sig, floorV + 1, ub);
                BranchAndBound(SolveNode());
                _rmp.SetBounds(sig, lb, ub);
            }
        }

        private bool TakeNode()
        {
            if (_nodeBudget <= 0 || _stopwatch.Elapsed > _timeBudget) { _completed = false; return false; }
            _nodeBudget--;
            return true;
        }

        private static (BnpColumn Column, double Value) MostFractional(IReadOnlyList<(BnpColumn Column, double Value)> primal)
        {
            BnpColumn? best = null;
            double bestDist = -1, bestVal = 0;
            foreach (var (col, val) in primal)
            {
                double dist = Math.Abs(val - Math.Round(val));
                if (dist > bestDist) { bestDist = dist; best = col; bestVal = val; }
            }
            return (best!, bestVal);
        }

        /// <summary>
        /// Solves the LP relaxation at the current node (current variable bounds and forbidden
        /// set) by column generation: heuristic pricing drives progress, the exact pricer
        /// supplies missed columns or certifies optimality.
        /// </summary>
        private NodeSolve SolveNode()
        {
            for (int iter = 0; iter < MaxCgIterations; iter++)
            {
                _ct.ThrowIfCancellationRequested();

                var status = _rmp.Solve();
                if (status is not (Solver.ResultStatus.OPTIMAL or Solver.ResultStatus.FEASIBLE))
                    return NodeSolve.Infeasible;

                var duals = _rmp.Duals();

                var added = new List<BnpColumn>();
                foreach (var c in _heuristic.FindColumns(duals, _forbidden))
                    if (_pool.TryAdd(c)) added.Add(c);

                if (added.Count > 0)
                {
                    _rmp.AddColumns(added);
                    continue;
                }

                var exactColumn = _exact.FindBestColumn(duals, _forbidden);
                if (exactColumn != null && _pool.TryAdd(exactColumn))
                {
                    _rmp.AddColumns([exactColumn]);
                    continue;
                }

                bool certified = _exact.LastSearchExhaustive;
                if (!certified) _allCertified = false;

                return new NodeSolve(true, _rmp.ObjectiveValue, _rmp.PrimalSolution(), _rmp.Leftovers(), _rmp.IsIntegral(), certified);
            }

            // CG iteration cap hit: treat as an uncertified feasible node.
            _allCertified = false;
            bool solved = _rmp.Solve() is Solver.ResultStatus.OPTIMAL or Solver.ResultStatus.FEASIBLE;
            return solved
                ? new NodeSolve(true, _rmp.ObjectiveValue, _rmp.PrimalSolution(), _rmp.Leftovers(), _rmp.IsIntegral(), false)
                : NodeSolve.Infeasible;
        }

        public void Dispose() => _rmp.Dispose();

        private readonly record struct NodeSolve(
            bool Feasible,
            double Objective,
            IReadOnlyList<(BnpColumn Column, double Value)> Primal,
            IReadOnlyDictionary<string, int> Leftovers,
            bool Integral,
            bool Certified)
        {
            public static NodeSolve Infeasible =>
                new(false, double.PositiveInfinity, [], new Dictionary<string, int>(StringComparer.Ordinal), false, true);
        }
    }
}
