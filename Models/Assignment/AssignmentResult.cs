using Stack_Solver.Models.Supports;

namespace Stack_Solver.Models.Assignment
{
    public class AssignmentResult
    {
        public IReadOnlyList<(PalletTemplate Template, int Count)> Assignments { get; init; } = [];
        public IReadOnlyDictionary<string, int> Leftovers { get; init; } = new Dictionary<string, int>();

        public int TotalPallets => Assignments.Sum(a => a.Count);
        public bool HasLeftovers => Leftovers.Values.Any(v => v > 0);
    }
}
