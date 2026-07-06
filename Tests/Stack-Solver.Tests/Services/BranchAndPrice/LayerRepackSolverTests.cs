using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services.BranchAndPrice;
using Stack_Solver.Services.Layering;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// Unit coverage of <see cref="LayerRepackSolver"/> — the Level-1 purity mechanism that regroups
    /// a settled solution's exact layer multiset into the same number of pallets to minimize impurity.
    /// These fixtures build the pallet templates directly (rather than driving the whole solve) so the
    /// regrouping decision is exercised in isolation and deterministically.
    /// </summary>
    public class LayerRepackSolverTests
    {
        private static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

        // Equal height and weight ∝ footprint area ⇒ every layer has the same load density, so the
        // top-heavy stacking rule never constrains order and only support geometry can block a group.
        private static SKU Sku(string id, int length, int width, int height = 20) => new()
        {
            SkuId = id,
            Name = id,
            Length = length,
            Width = width,
            Height = height,
            Weight = length * width * 0.005,
            Rotatable = true,
        };

        private static Pallet MakePallet() => new("P", 120, 80, 14)
        {
            MaxStackHeight = 180,
            MaxStackWeight = 950,
            MaxTopHeavyPercent = 50,
            MaxSkuOverhang = 0,
        };

        /// <summary>Densest homogeneous layer for a single SKU on the pallet.</summary>
        private static Layer FullLayer(SKU sku, Pallet pallet) =>
            new HomogeneousGenerationStrategy()
                .Generate([sku], pallet, new GenerationOptions())
                .OrderByDescending(l => l.Items.Count)
                .First();

        private static BnpColumn Column(params Layer[] layers) =>
            new(PalletTemplate.FromLayers(layers));

        private static Dictionary<string, int> SkuTotals(IEnumerable<(BnpColumn Column, int Count)> a)
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (col, count) in a)
                foreach (var (sku, n) in col.SkuCounts)
                    totals[sku] = totals.GetValueOrDefault(sku) + n * count;
            return totals;
        }

        [Fact]
        public void MovesAStrandedTailLayerOntoTheUnderFullSameSkuPallet()
        {
            // The defect case in miniature: a pure-B pair of layers carries a lone D tail layer
            // (mixed pallet, impurity 1), while a second pallet is pure D but under-full (impurity 0).
            // No single pool column expresses the fix — the D layer must be *lifted* onto the D pallet.
            var pallet = MakePallet();
            var b = FullLayer(Sku("B", 26, 13), pallet);
            var d = FullLayer(Sku("D", 50, 30), pallet);

            // Pallet 1: [B, B, D] (distinct {B,D} ⇒ impurity 1). Pallet 2: [D, D] (pure ⇒ impurity 0).
            var current = new List<(BnpColumn, int)>
            {
                (Column(b, b, d), 1),
                (Column(d, d), 1),
            };
            Assert.Equal(1, PurityMetric.TotalImpurity(current));

            var repacked = LayerRepackSolver.Solve(current, pallet, Budget, TestContext.Current.CancellationToken);

            Assert.NotNull(repacked);
            // Same pallet count, all layers preserved (so same per-SKU totals), and now perfectly pure:
            // the D layer moved onto the D pallet, leaving one pure-B and one pure-D pallet.
            Assert.Equal(2, repacked!.Sum(c => c.Count));
            Assert.Equal(0, PurityMetric.TotalImpurity(repacked));
            Assert.Equal(SkuTotals(current), SkuTotals(repacked));
        }

        [Fact]
        public void ReturnsNoStrictlyPurerRegroupingWhenAlreadyPure()
        {
            // Two already-pure pallets: the optimal regrouping cannot beat impurity 0, so the solver
            // returns a grouping no worse than the input (impurity still 0) and the caller adopts
            // nothing. Proven by the total impurity staying 0 and SKU totals being preserved.
            var pallet = MakePallet();
            var b = FullLayer(Sku("B", 26, 13), pallet);
            var d = FullLayer(Sku("D", 50, 30), pallet);

            var current = new List<(BnpColumn, int)>
            {
                (Column(b, b), 1),
                (Column(d, d), 1),
            };

            var repacked = LayerRepackSolver.Solve(current, pallet, Budget, TestContext.Current.CancellationToken);

            // A result may or may not be returned, but it can never be more pure-negative than input.
            if (repacked != null)
            {
                Assert.Equal(0, PurityMetric.TotalImpurity(repacked));
                Assert.Equal(SkuTotals(current), SkuTotals(repacked));
            }
        }

        [Fact]
        public void SpendsAnExtraPalletToPurifyAMixThatTheSameCountCannotFix()
        {
            // Tall layers (height 70) so at most two fit the 166-unit stack height — a pallet holds ≤2
            // layers. Three single-SKU layers of three distinct SKUs then need 2 pallets at minimum,
            // and *any* 2-pallet grouping strands two distinct SKUs together (impurity 1). Only a third
            // pallet lets every layer sit pure (impurity 0) — a fix no same-count regrouping can reach.
            var pallet = MakePallet();
            var a = FullLayer(Sku("A", 38, 23, height: 70), pallet);
            var b = FullLayer(Sku("B", 26, 13, height: 70), pallet);
            var c = FullLayer(Sku("C", 50, 30, height: 70), pallet);

            // Incumbent: [A, B] on one pallet (impurity 1), [C] on another (impurity 0). K = 2.
            var current = new List<(BnpColumn, int)>
            {
                (Column(a, b), 1),
                (Column(c), 1),
            };
            Assert.Equal(1, PurityMetric.TotalImpurity(current));

            // No extra budget: the best 2-pallet regrouping still strands a pair — impurity stays 1.
            var sameCount = LayerRepackSolver.Solve(current, pallet, Budget, TestContext.Current.CancellationToken, extraPalletBudget: 0);
            if (sameCount != null)
            {
                Assert.Equal(2, sameCount.Sum(x => x.Count));
                Assert.Equal(1, PurityMetric.TotalImpurity(sameCount));
            }

            // One extra pallet: each SKU gets its own pure pallet — impurity 0 at 3 pallets.
            var purer = LayerRepackSolver.Solve(current, pallet, Budget, TestContext.Current.CancellationToken, extraPalletBudget: 1);

            Assert.NotNull(purer);
            Assert.Equal(3, purer!.Sum(x => x.Count));
            Assert.Equal(0, PurityMetric.TotalImpurity(purer));
            Assert.Equal(SkuTotals(current), SkuTotals(purer));
        }

        [Fact]
        public void DoesNotSpendExtraPalletsWhenTheIncumbentCountAlreadyReachesTheBestImpurity()
        {
            // Two already-pure pallets: impurity is already 0, so even offered a budget the solver must
            // not inflate the pallet count — the ε tie-break keeps it at the fewest pallets (2).
            var pallet = MakePallet();
            var b = FullLayer(Sku("B", 26, 13), pallet);
            var d = FullLayer(Sku("D", 50, 30), pallet);

            var current = new List<(BnpColumn, int)>
            {
                (Column(b, b), 1),
                (Column(d, d), 1),
            };

            var purer = LayerRepackSolver.Solve(current, pallet, Budget, TestContext.Current.CancellationToken, extraPalletBudget: 2);

            if (purer != null)
            {
                Assert.Equal(2, purer.Sum(x => x.Count));   // no needless extra pallet
                Assert.Equal(0, PurityMetric.TotalImpurity(purer));
                Assert.Equal(SkuTotals(current), SkuTotals(purer));
            }
        }

        [Fact]
        public void ReturnsNullForASingleUnsplittablePallet()
        {
            // One pallet cannot be regrouped: nowhere to move a layer to.
            var pallet = MakePallet();
            var b = FullLayer(Sku("B", 26, 13), pallet);
            var d = FullLayer(Sku("D", 50, 30), pallet);

            var current = new List<(BnpColumn, int)> { (Column(b, d), 1) };

            var repacked = LayerRepackSolver.Solve(current, pallet, Budget, TestContext.Current.CancellationToken);

            Assert.Null(repacked);
        }
    }
}
