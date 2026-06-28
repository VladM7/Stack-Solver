using Google.OrTools.LinearSolver;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// LP relaxation of the set-partition master, solved with GLOP. Demand is enforced as
    /// an equality per SKU and each column is a non-negative variable with unit cost (one
    /// pallet). Exposes the primal solution and the demand-constraint duals π_i that drive
    /// pricing.
    ///
    /// <code>
    ///   minimize    Σ_t x_t
    ///   subject to  Σ_t a_{t,i}·x_t = d_i   ∀ SKU i      (dual π_i)
    ///               x_t ≥ 0
    /// </code>
    ///
    /// Columns are added incrementally: column generation re-solves after each batch of
    /// new columns is appended.
    /// </summary>
    public sealed class RestrictedMasterProblem : IDisposable
    {
        private readonly Solver _solver;
        private readonly Objective _objective;
        private readonly IReadOnlyList<string> _skuOrder;
        private readonly Dictionary<string, Constraint> _demand;
        private readonly List<(BnpColumn Column, Variable Var)> _columns = [];

        /// <param name="skuOrder">Placeable SKUs that form the demand constraints.</param>
        /// <param name="demand">Exact demand per SKU; must contain every SKU in <paramref name="skuOrder"/>.</param>
        public RestrictedMasterProblem(IReadOnlyList<string> skuOrder, IReadOnlyDictionary<string, int> demand)
        {
            ArgumentNullException.ThrowIfNull(skuOrder);
            ArgumentNullException.ThrowIfNull(demand);

            _solver = Solver.CreateSolver("GLOP")
                ?? throw new InvalidOperationException("GLOP linear solver is unavailable.");
            _objective = _solver.Objective();
            _objective.SetMinimization();

            _skuOrder = skuOrder;
            _demand = new Dictionary<string, Constraint>(skuOrder.Count, StringComparer.Ordinal);
            foreach (var sku in skuOrder)
            {
                int d = demand[sku];
                // Equality: d ≤ Σ a·x ≤ d.
                _demand[sku] = _solver.MakeConstraint(d, d, $"demand_{sku}");
            }
        }

        public int ColumnCount => _columns.Count;

        /// <summary>Adds a column as a new non-negative variable with unit objective cost.</summary>
        public Variable AddColumn(BnpColumn column)
        {
            ArgumentNullException.ThrowIfNull(column);

            var v = _solver.MakeNumVar(0.0, double.PositiveInfinity, $"x{_columns.Count}");
            _objective.SetCoefficient(v, 1.0);
            foreach (var sku in _skuOrder)
            {
                int a = column.CountOf(sku);
                if (a != 0) _demand[sku].SetCoefficient(v, a);
            }
            _columns.Add((column, v));
            return v;
        }

        public void AddColumns(IEnumerable<BnpColumn> columns)
        {
            ArgumentNullException.ThrowIfNull(columns);
            foreach (var c in columns) AddColumn(c);
        }

        public Solver.ResultStatus Solve() => _solver.Solve();

        /// <summary>Objective value Σ x_t of the most recent solve.</summary>
        public double ObjectiveValue => _objective.Value();

        /// <summary>Dual price π_i of each demand constraint, keyed by SKU.</summary>
        public IReadOnlyDictionary<string, double> Duals()
        {
            var duals = new Dictionary<string, double>(_demand.Count, StringComparer.Ordinal);
            foreach (var (sku, c) in _demand)
                duals[sku] = c.DualValue();
            return duals;
        }

        /// <summary>Columns with primal value x_t &gt; <paramref name="tolerance"/>, paired with that value.</summary>
        public IReadOnlyList<(BnpColumn Column, double Value)> PrimalSolution(double tolerance = 1e-9)
        {
            var result = new List<(BnpColumn, double)>();
            foreach (var (col, var) in _columns)
            {
                double val = var.SolutionValue();
                if (val > tolerance) result.Add((col, val));
            }
            return result;
        }

        public void Dispose() => _solver.Dispose();
    }
}
