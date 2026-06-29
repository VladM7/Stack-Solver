namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Diagnostic counters for a single branch-and-price solve. Purely observational — it
    /// influences no decision — and exists to show where the time goes: how much is spent in
    /// the branch-and-bound tree versus column generation, and how often the exact pricer is
    /// truncated by its node budget or deadline (the regime that loses certification). Use it
    /// to confirm bottleneck proportions before tuning.
    /// </summary>
    public sealed class BranchAndPriceStats
    {
        /// <summary>Branch-and-bound nodes entered (root included).</summary>
        public int TreeNodes { get; set; }

        /// <summary>LP nodes solved by column generation (<c>SolveNode</c> calls).</summary>
        public int LpNodesSolved { get; set; }

        /// <summary>Total column-generation iterations across all nodes.</summary>
        public int CgIterations { get; set; }

        /// <summary>Exact-pricer (<c>FindBestColumn</c>) invocations.</summary>
        public int ExactPricerCalls { get; set; }

        /// <summary>Exact-pricer calls that explored the whole tree (certifying, memo-eligible).</summary>
        public int ExactPricerExhaustive { get; set; }

        /// <summary>Exact-pricer calls cut short by the node budget or deadline (non-certifying).</summary>
        public int ExactPricerTruncated { get; set; }

        /// <summary>Total DFS nodes visited across all exact-pricer calls.</summary>
        public long ExactPricerNodes { get; set; }

        /// <summary>True when the ⌈LP-bound⌉ short-circuit proved optimality at the root, skipping the tree.</summary>
        public bool RootCertificationFired { get; set; }

        /// <summary>True when the search exhausted the tree within budget.</summary>
        public bool Completed { get; set; }

        /// <summary>True when every solved node certified its LP optimum.</summary>
        public bool AllCertified { get; set; }

        /// <summary>Root LP lower bound (∞ if the root was infeasible).</summary>
        public double RootBound { get; set; }

        /// <summary>Objective (pallet count) of the returned incumbent.</summary>
        public double BestObjective { get; set; }

        /// <summary>Wall-clock time spent in the solve.</summary>
        public TimeSpan Elapsed { get; set; }

        /// <summary>One-line summary suitable for a debug/trace log.</summary>
        public override string ToString() =>
            $"B&P [{Elapsed.TotalSeconds:0.00}s] " +
            $"tree={TreeNodes} lpNodes={LpNodesSolved} cgIters={CgIterations} " +
            $"pricer={ExactPricerCalls}(exh={ExactPricerExhaustive} trunc={ExactPricerTruncated}) " +
            $"pricerNodes={ExactPricerNodes} " +
            $"rootCert={(RootCertificationFired ? "Y" : "N")} completed={(Completed ? "Y" : "N")} " +
            $"allCertified={(AllCertified ? "Y" : "N")} " +
            $"rootBound={(double.IsInfinity(RootBound) ? "inf" : RootBound.ToString("0.##"))} " +
            $"best={BestObjective:0.##}";
    }
}
