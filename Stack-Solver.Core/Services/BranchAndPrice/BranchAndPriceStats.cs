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

        /// <summary>Improving columns contributed by the constructive box-built pricer.</summary>
        public int ConstructivePricerColumns { get; set; }

        /// <summary>True when the ⌈LP-bound⌉ short-circuit proved optimality at the root, skipping the tree.</summary>
        public bool RootCertificationFired { get; set; }

        /// <summary>True when pallet-count (cardinality) certification proved the returned optimum.</summary>
        public bool PalletCountCertified { get; set; }

        /// <summary>Largest pallet count K tested by the cardinality certification loop (0 if it never ran).</summary>
        public int MaxPalletCountTested { get; set; }

        /// <summary>True when the restricted-master IP heuristic was invoked at the root.</summary>
        public bool RestrictedMasterIpRan { get; set; }

        /// <summary>Pallet count of the restricted-master IP's assignment (NaN if it produced none).</summary>
        public double RestrictedMasterIpObjective { get; set; } = double.NaN;

        /// <summary>True when the Solution-A purity re-selection pass ran.</summary>
        public bool PurityPolishRan { get; set; }

        /// <summary>True when the purity re-selection pass adopted a purer same-count assignment.</summary>
        public bool PurityImproved { get; set; }

        /// <summary>True when the Solution-B purity-first re-packing pass ran (extra-pallet budget).</summary>
        public bool PurityAlternativeRan { get; set; }

        /// <summary>True when Solution B found a strictly purer regrouping worth offering alongside the optimum.</summary>
        public bool PurityAlternativeOffered { get; set; }

        /// <summary>Pallet count of the Solution-B alternative (0 when none was offered).</summary>
        public int PurityAlternativePallets { get; set; }

        /// <summary>Total impurity of the Solution-B alternative (0 when none was offered).</summary>
        public long PurityAlternativeImpurity { get; set; }

        /// <summary>True when the Level-2 layer-reformation pass ran (box-level pure-layer rebuild).</summary>
        public bool ReformationRan { get; set; }

        /// <summary>True when reformation found a strictly purer arrangement worth offering alongside the optimum.</summary>
        public bool ReformationOffered { get; set; }

        /// <summary>Pallet count of the reformed alternative (0 when none was offered).</summary>
        public int ReformationPallets { get; set; }

        /// <summary>Total impurity of the reformed alternative (0 when none was offered).</summary>
        public long ReformationImpurity { get; set; }

        /// <summary>Total impurity (see <see cref="PurityMetric"/>) of the returned incumbent.</summary>
        public long FinalImpurity { get; set; }

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
            $"pricerNodes={ExactPricerNodes} constructiveCols={ConstructivePricerColumns} " +
            $"ip={(RestrictedMasterIpRan ? (double.IsNaN(RestrictedMasterIpObjective) ? "none" : RestrictedMasterIpObjective.ToString("0.##")) : "off")} " +
            $"rootCert={(RootCertificationFired ? "Y" : "N")} " +
            $"palletCountCert={(PalletCountCertified ? $"Y(K={MaxPalletCountTested})" : "N")} " +
            $"completed={(Completed ? "Y" : "N")} " +
            $"allCertified={(AllCertified ? "Y" : "N")} " +
            $"rootBound={(double.IsInfinity(RootBound) ? "inf" : RootBound.ToString("0.##"))} " +
            $"best={BestObjective:0.##} " +
            $"purityPolish={(PurityPolishRan ? (PurityImproved ? "improved" : "kept") : "off")} " +
            $"finalImpurity={FinalImpurity} " +
            $"purityAlt={(PurityAlternativeRan ? (PurityAlternativeOffered ? $"offered(p={PurityAlternativePallets},imp={PurityAlternativeImpurity})" : "none") : "off")} " +
            $"reform={(ReformationRan ? (ReformationOffered ? $"offered(p={ReformationPallets},imp={ReformationImpurity})" : "none") : "off")}";
    }
}
