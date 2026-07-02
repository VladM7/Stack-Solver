using Stack_Solver.Models;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// A certified combinatorial lower bound on the number of pallets needed to hold a demand —
    /// computed from physics alone, independent of the LP relaxation. It strengthens the weak
    /// volumetric LP bound in the large-demand regime, where it lets the ⌈bound⌉ root
    /// short-circuit certify optimality without entering the tree.
    ///
    /// <para>Two resource relaxations, each provably a lower bound (their max is taken):</para>
    /// <list type="bullet">
    ///   <item><b>Weight</b>: Σ demand·boxWeight cannot exceed pallets · maxStackWeight.</item>
    ///   <item><b>Volume</b>: Σ demand·boxVolume cannot exceed pallets · (footprint · stack height).</item>
    /// </list>
    ///
    /// <para>Deliberately omitted: a per-SKU "layer count" (partial-layer-waste) bound. That would
    /// be stronger but is <i>unsound</i> here — layers may mix SKUs (see <c>BLFGenerationStrategy</c>),
    /// so a single mixed layer can pack two SKUs' partial layers together, making a per-SKU sum
    /// exceed the true optimum. Only resource bounds that hold regardless of layer composition are
    /// used, so the result can never exceed the real pallet minimum.</para>
    /// </summary>
    public static class CombinatorialBound
    {
        /// <summary>
        /// Fractional lower bound on pallets to hold <paramref name="demand"/>. Callers take
        /// ⌈value⌉ for the integer bound. SKUs missing from <paramref name="skus"/> contribute
        /// nothing (already filtered as unplaceable upstream).
        /// </summary>
        public static double Compute(
            IReadOnlyDictionary<string, int> demand,
            IReadOnlyDictionary<string, SKU> skus,
            Pallet pallet)
        {
            ArgumentNullException.ThrowIfNull(demand);
            ArgumentNullException.ThrowIfNull(skus);
            ArgumentNullException.ThrowIfNull(pallet);

            int availHeight = pallet.MaxStackHeight - pallet.Height;
            double palletVolume = (double)pallet.Length * pallet.Width * Math.Max(0, availHeight);
            double maxWeight = pallet.MaxStackWeight;

            double totalWeight = 0, totalVolume = 0;
            foreach (var (sku, d) in demand)
            {
                if (d <= 0 || !skus.TryGetValue(sku, out var box)) continue;
                totalWeight += d * box.Weight;
                totalVolume += (double)d * box.Length * box.Width * box.Height;
            }

            double weightBound = maxWeight > 0 ? totalWeight / maxWeight : 0;
            double volumeBound = palletVolume > 0 ? totalVolume / palletVolume : 0;
            return Math.Max(weightBound, volumeBound);
        }
    }
}
