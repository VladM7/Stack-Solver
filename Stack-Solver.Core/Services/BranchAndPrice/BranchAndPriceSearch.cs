using System.Diagnostics;
using Google.OrTools.LinearSolver;
using Stack_Solver.Models;
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

        // Wall-clock slice handed to the restricted-master IP heuristic at the root: a fraction of
        // the time left after root column generation, capped so it can never starve the tree search.
        private const double IpHeuristicTimeFraction = 0.3;
        private static readonly TimeSpan IpHeuristicMaxTime = TimeSpan.FromSeconds(10);

        // The final-polish IP runs after the tree, so it may use most of the remaining time.
        private const double IpFinalTimeFraction = 0.9;
        private static readonly TimeSpan IpFinalMaxTime = TimeSpan.FromSeconds(60);

        private readonly RestrictedMasterProblem _rmp;
        private readonly ColumnPool _pool = new();
        private readonly PricingSolver _heuristic;
        private readonly ExactPricingSolver _exact;
        private readonly HashSet<string> _forbidden = new(StringComparer.Ordinal);
        private readonly IReadOnlyList<string> _placeableSkus;
        private readonly IReadOnlyDictionary<string, int> _placeableDemand;
        private readonly double _combinatorialBound;
        private readonly CancellationToken _ct;

        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly TimeSpan _timeBudget;
        private long _nodeBudget;
        private bool _completed = true;
        private bool _allCertified = true;
        private bool _provenByBound;
        private bool _rootBoundUsable;
        private readonly BranchAndPriceStats _stats = new();

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
            _placeableSkus = placeableSkus;
            _placeableDemand = placeableDemand;
            _combinatorialBound = CombinatorialBound.Compute(placeableDemand, BuildSkuMap(layers), pallet);
            _rmp = new RestrictedMasterProblem(placeableSkus, placeableDemand);
            _pool.AddRange(seedColumns);
            _rmp.AddColumns(seedColumns);
            _heuristic = new PricingSolver(layers, pallet);
            _exact = new ExactPricingSolver(layers, pallet);
        }

        /// <summary>Maps each SKU id appearing in the layers to its <see cref="SKU"/> (box dimensions/weight).</summary>
        private static Dictionary<string, SKU> BuildSkuMap(IReadOnlyList<Layer> layers)
        {
            var map = new Dictionary<string, SKU>(StringComparer.Ordinal);
            foreach (var layer in layers)
                foreach (var item in layer.Items)
                    map.TryAdd(item.SkuType.SkuId, item.SkuType);
            return map;
        }

        /// <summary>Certified-or-not LP lower bound at the root (∞ if the root is infeasible).</summary>
        public double RootBound { get; private set; }

        public bool RootCertified { get; private set; }

        /// <summary>The optimal (or best-found) integer column multiset, or null if none was found.</summary>
        public IReadOnlyList<(BnpColumn Column, int Count)>? OptimalColumns => _best;

        /// <summary>Leftover (unmet) units per SKU in the returned solution.</summary>
        public IReadOnlyDictionary<string, int> OptimalLeftovers => _bestLeftovers;

        /// <summary>True only when the returned solution is a proven optimum — either certified by a
        /// valid lower bound, or by a fully-completed, all-certified tree search.</summary>
        public bool ProvedOptimal => _best != null && (_provenByBound || (_completed && _allCertified));

        /// <summary>Diagnostic counters describing where the solve spent its effort.</summary>
        public BranchAndPriceStats Stats => _stats;

        /// <summary>
        /// Seeds a starting integer incumbent (e.g. from <see cref="LayerPackingHeuristic"/>) so
        /// the search has an upper bound to prune against and never returns short on timeout.
        /// Columns are added to the pool/RMP so the tree can also reach this solution.
        /// </summary>
        public void SeedIncumbent(IReadOnlyList<(BnpColumn Column, int Count)> columns)
        {
            ArgumentNullException.ThrowIfNull(columns);
            if (columns.Count == 0) return;

            var added = new List<BnpColumn>();
            foreach (var (col, _) in columns)
                if (_pool.TryAdd(col)) added.Add(col);
            if (added.Count > 0) _rmp.AddColumns(added);

            _best = columns.Select(c => (c.Column, c.Count)).Where(c => c.Count > 0).ToList();
            _bestObjective = columns.Sum(c => c.Count);
            _bestLeftovers = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Certified combinatorial (physics) lower bound on the pallet count — valid regardless of
        /// the LP, and the reportable bound when the LP optimum is weaker. See <see cref="CombinatorialBound"/>.
        /// </summary>
        public double CombinatorialLowerBound => _combinatorialBound;

        public void Run()
        {
            var root = SolveNode();
            RootBound = root.Feasible ? root.Primal.Sum(p => p.Value) : double.PositiveInfinity;
            RootCertified = root.Certified;
            // The LP optimum is a valid integer lower bound only when the pricer proved it exhaustive
            // and the root itself leaves no untiled demand.
            _rootBoundUsable = RootCertified && root.Feasible && AllZero(root.Leftovers);

            // Strengthen the incumbent with the restricted-master IP before deciding whether to
            // branch: a tight integer solution over the root column pool often matches ⌈bound⌉ and
            // certifies optimality here, skipping the tree entirely.
            TryImproveIncumbentWithIp(IpHeuristicTimeFraction, IpHeuristicMaxTime);
            if (TryCertifyByBound())
            {
                FinalizeStats();
                return;
            }

            BranchAndBound(root);

            // Final polish: the tree has built a far richer column pool than the root had. If the
            // search stopped early (node/time budget) without proving optimality, an exact IP over
            // all generated columns may assemble an incumbent that matches ⌈bound⌉ — certifying an
            // optimum the column-variable branching never reached — using any leftover wall-clock.
            if (!ProvedOptimal)
            {
                TryImproveIncumbentWithIp(IpFinalTimeFraction, IpFinalMaxTime);
                TryCertifyByBound();
            }

            FinalizeStats();
        }

        /// <summary>
        /// Marks the incumbent proven optimal when it places all demand with no more pallets than
        /// ⌈max(certified LP bound, combinatorial bound)⌉. Both inputs are valid lower bounds on the
        /// integer optimum, so a matching incumbent is optimal. Idempotent; safe to call repeatedly.
        /// </summary>
        private bool TryCertifyByBound()
        {
            if (_best == null || !AllZero(_bestLeftovers)) return false;

            int lpLb = _rootBoundUsable ? (int)Math.Ceiling(RootBound - IntTol) : int.MinValue;
            int comboLb = (int)Math.Ceiling(_combinatorialBound - IntTol);
            int certifiedLb = Math.Max(lpLb, comboLb);

            if (_bestObjective <= certifiedLb + IntTol)
            {
                _provenByBound = true;
                _stats.RootCertificationFired = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Runs the restricted-master IP over the current column pool and adopts its assignment as
        /// the incumbent when it covers all demand with fewer pallets than the current best. Bounded
        /// to <paramref name="fraction"/> of the remaining time (capped at <paramref name="cap"/>) so
        /// it cannot starve the branch-and-bound search.
        /// </summary>
        private void TryImproveIncumbentWithIp(double fraction, TimeSpan cap)
        {
            var remaining = _timeBudget - _stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero) return;

            var slice = TimeSpan.FromMilliseconds(remaining.TotalMilliseconds * fraction);
            if (slice > cap) slice = cap;

            var ip = RestrictedMasterHeuristic.Solve(_pool.Columns, _placeableSkus, _placeableDemand, slice, _ct);
            _stats.RestrictedMasterIpRan = true;
            if (ip == null || !AllZero(ip.Leftovers)) return;

            double objective = ip.Columns.Sum(c => c.Count);
            _stats.RestrictedMasterIpObjective = objective;
            if (objective >= _bestObjective - IntTol) return;

            // The IP found a strictly better full-coverage assignment — adopt it, and make sure its
            // columns are in the pool/RMP so the tree can also reach (and branch around) it.
            var added = new List<BnpColumn>();
            foreach (var (col, _) in ip.Columns)
                if (_pool.TryAdd(col)) added.Add(col);
            if (added.Count > 0) _rmp.AddColumns(added);

            _best = ip.Columns.Where(c => c.Count > 0).Select(c => (c.Column, c.Count)).ToList();
            _bestObjective = objective;
            _bestLeftovers = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private void FinalizeStats()
        {
            _stats.Completed = _completed;
            _stats.AllCertified = _allCertified;
            _stats.RootBound = RootBound;
            _stats.BestObjective = _best != null ? _bestObjective : double.NaN;
            _stats.Elapsed = _stopwatch.Elapsed;
        }

        private static bool AllZero(IReadOnlyDictionary<string, int> leftovers)
        {
            foreach (var v in leftovers.Values) if (v > 0) return false;
            return true;
        }

        private void BranchAndBound(NodeSolve node)
        {
            _ct.ThrowIfCancellationRequested();
            if (!node.Feasible) return;
            _stats.TreeNodes++;

            // Prune: no completion of this node can beat the incumbent. The node bound is the
            // stronger of its LP objective and the global combinatorial bound (both valid lower
            // bounds on the integer optimum reachable here).
            if (Math.Max(node.Objective, _combinatorialBound) >= _bestObjective - IntTol) return;

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
            _stats.LpNodesSolved++;
            for (int iter = 0; iter < MaxCgIterations; iter++)
            {
                _ct.ThrowIfCancellationRequested();
                _stats.CgIterations++;

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

                var remaining = _timeBudget - _stopwatch.Elapsed;
                var exactColumn = _exact.FindBestColumn(
                    duals, _forbidden, _ct,
                    DateTime.UtcNow + (remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero));
                _stats.ExactPricerCalls++;
                _stats.ExactPricerNodes += _exact.LastNodesExplored;
                if (_exact.LastSearchExhaustive) _stats.ExactPricerExhaustive++;
                else _stats.ExactPricerTruncated++;
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
