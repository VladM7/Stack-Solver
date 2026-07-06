using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Realism scoring for a pallet template, grounded in a single unit: an "extra distinct-SKU
    /// handling incidence" — one for every distinct SKU beyond the first that a picker/robot must
    /// deal with, counted once per layer (mixed layers are harder to build) and once more for the
    /// pallet as a whole (mixed pallets are harder to put away/pick). Both terms share this unit,
    /// so they are summed 1:1 — no invented weighting. Zero iff the pallet is a single SKU in
    /// every layer.
    /// </summary>
    public static class PurityMetric
    {
        /// <summary>
        /// Impurity of one pallet template: Σ_layers (distinctSkusInLayer − 1), plus
        /// (distinctSkusOnPallet − 1). An empty or single-SKU pallet scores 0.
        /// </summary>
        public static int Impurity(PalletTemplate template)
        {
            ArgumentNullException.ThrowIfNull(template);

            int layerImpurity = 0;
            foreach (var layer in template.Layers)
            {
                int distinct = layer.Metrics.UsedSkuTypes.Count;
                if (distinct > 1) layerImpurity += distinct - 1;
            }

            int palletDistinct = template.SkuCounts.Count(kvp => kvp.Value > 0);
            int palletImpurity = palletDistinct > 1 ? palletDistinct - 1 : 0;

            return layerImpurity + palletImpurity;
        }

        /// <summary>Impurity of the pallet template backing <paramref name="column"/>.</summary>
        public static int Impurity(BnpColumn column)
        {
            ArgumentNullException.ThrowIfNull(column);
            return Impurity(column.Template);
        }

        /// <summary>Total impurity of an assignment: Σ_t count_t · impurity(t) over chosen columns.</summary>
        public static long TotalImpurity(IEnumerable<(BnpColumn Column, int Count)> assignment)
        {
            ArgumentNullException.ThrowIfNull(assignment);

            long total = 0;
            foreach (var (column, count) in assignment)
                total += (long)Impurity(column) * count;
            return total;
        }
    }
}
