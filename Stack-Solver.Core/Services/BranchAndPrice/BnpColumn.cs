using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// A single master-problem column: one valid pallet template together with its
    /// SKU-count vector a_t. Column identity is the SKU-count signature, matching the
    /// dedup scheme used by <see cref="Services.Stacking.PalletTemplateEnumerator"/> so
    /// columns coming from different sources (seed, pricing) deduplicate consistently.
    /// </summary>
    public sealed class BnpColumn
    {
        public PalletTemplate Template { get; }
        public IReadOnlyDictionary<string, int> SkuCounts => Template.SkuCounts;
        public string Signature { get; }

        public BnpColumn(PalletTemplate template)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Signature = BuildSignature(template);
        }

        /// <summary>Count of <paramref name="sku"/> packed by this column (a_{t,i}).</summary>
        public int CountOf(string sku) => Template.SkuCounts.GetValueOrDefault(sku);

        public static string BuildSignature(PalletTemplate t) =>
            string.Join("|", t.SkuCounts
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}"));
    }
}
