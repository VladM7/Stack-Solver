namespace Stack_Solver.Models.Jobs
{
    /// <summary>
    /// A compact copy of the solutions a run produced — just enough placement data to re-display them
    /// instantly without re-solving. Stored as <see cref="Job.ResultsJson"/>. The 3D meshes, geometry
    /// and per-layer metrics are recomputed on open from this data (they are already rebuilt on every
    /// navigation), so only box placements and headline stats are persisted.
    /// </summary>
    public class JobResultsSnapshot
    {
        /// <summary>Payload format version, so future changes can be migrated or rejected gracefully.</summary>
        public int SchemaVersion { get; set; } = 1;

        public List<JobSolutionSnapshot> Solutions { get; set; } = [];
    }

    /// <summary>One produced solution (e.g. "Branch &amp; Price") within a <see cref="JobResultsSnapshot"/>.</summary>
    public class JobSolutionSnapshot
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Optimality flag when the solver reports one (Branch &amp; Price); null otherwise.</summary>
        public bool? IsProvenOptimal { get; set; }

        /// <summary>LP lower bound on the pallet count, when provided.</summary>
        public double LowerBound { get; set; }

        public List<JobPalletSnapshot> Pallets { get; set; } = [];
    }

    /// <summary>A distinct pallet type and how many identical copies of it the solution uses.</summary>
    public class JobPalletSnapshot
    {
        public int Count { get; set; }
        public List<JobLayerSnapshot> Layers { get; set; } = [];
    }

    /// <summary>One layer in a pallet, in bottom-to-top stack order.</summary>
    public class JobLayerSnapshot
    {
        /// <summary>Preserved so identical layers regroup as one "×N" layer type on reload.</summary>
        public string LayerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Height { get; set; }
        public double Utilization { get; set; }
        public List<JobItemSnapshot> Items { get; set; } = [];
    }

    /// <summary>One placed box: which SKU, where, and whether rotated 90°.</summary>
    public class JobItemSnapshot
    {
        public string SkuId { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public bool Rotated { get; set; }
    }
}
