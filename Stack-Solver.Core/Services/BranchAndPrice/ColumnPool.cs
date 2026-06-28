namespace Stack_Solver.Services.BranchAndPrice
{
    /// <summary>
    /// Holds the master columns generated so far, deduplicated by SKU-count signature.
    /// The pool is shared and cumulative across the whole branch-and-bound search; nodes
    /// restrict which pooled columns they may use rather than removing them.
    /// </summary>
    public sealed class ColumnPool
    {
        private readonly List<BnpColumn> _columns = [];
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

        public IReadOnlyList<BnpColumn> Columns => _columns;
        public int Count => _columns.Count;

        /// <summary>Adds the column if its signature is new. Returns true when added.</summary>
        public bool TryAdd(BnpColumn column)
        {
            ArgumentNullException.ThrowIfNull(column);
            if (!_seen.Add(column.Signature)) return false;
            _columns.Add(column);
            return true;
        }

        /// <summary>Adds every distinct column from <paramref name="columns"/>. Returns the count added.</summary>
        public int AddRange(IEnumerable<BnpColumn> columns)
        {
            ArgumentNullException.ThrowIfNull(columns);
            int added = 0;
            foreach (var c in columns)
                if (TryAdd(c)) added++;
            return added;
        }
    }
}
