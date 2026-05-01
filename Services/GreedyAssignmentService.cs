using Stack_Solver.Models.Assignment;
using Stack_Solver.Models.Supports;

namespace Stack_Solver.Services
{
    /// <summary>
    /// Greedily assigns pallet templates to cover SKU demand. Each iteration picks the template
    /// that satisfies the most remaining boxes, uses as many copies as the demand allows, and
    /// decrements remaining quantities. Stops when demand is fully covered or no template helps.
    /// </summary>
    public static class GreedyAssignmentService
    {
        public static AssignmentResult Assign(
            IReadOnlyList<PalletTemplate> templates,
            IReadOnlyDictionary<string, int> demand)
        {
            var remaining = new Dictionary<string, int>(demand, StringComparer.Ordinal);
            var assignments = new List<(PalletTemplate Template, int Count)>();
            const int maxIterations = 10_000;

            for (int iter = 0; iter < maxIterations && remaining.Values.Any(v => v > 0); iter++)
            {
                PalletTemplate? best = null;
                int bestScore = 0;

                foreach (var template in templates)
                {
                    int score = template.SkuCounts.Sum(kvp =>
                        remaining.TryGetValue(kvp.Key, out int rem) ? Math.Min(rem, kvp.Value) : 0);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = template;
                    }
                }

                if (best == null || bestScore == 0)
                    break;

                int count = ComputeCount(best, remaining);
                MergeAssignment(assignments, best, count);

                foreach (var kvp in best.SkuCounts)
                {
                    if (remaining.ContainsKey(kvp.Key))
                        remaining[kvp.Key] = Math.Max(0, remaining[kvp.Key] - kvp.Value * count);
                }
            }

            var leftovers = remaining
                .Where(kvp => kvp.Value > 0)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);

            return new AssignmentResult { Assignments = assignments, Leftovers = leftovers };
        }

        private static int ComputeCount(PalletTemplate template, IReadOnlyDictionary<string, int> remaining)
        {
            int count = int.MaxValue;
            foreach (var kvp in template.SkuCounts)
            {
                if (remaining.TryGetValue(kvp.Key, out int rem) && rem > 0 && kvp.Value > 0)
                    count = Math.Min(count, rem / kvp.Value);
            }
            return count == int.MaxValue || count == 0 ? 1 : count;
        }

        private static void MergeAssignment(
            List<(PalletTemplate Template, int Count)> assignments,
            PalletTemplate template,
            int count)
        {
            for (int i = 0; i < assignments.Count; i++)
            {
                if (assignments[i].Template.Id == template.Id)
                {
                    assignments[i] = (template, assignments[i].Count + count);
                    return;
                }
            }
            assignments.Add((template, count));
        }
    }
}
