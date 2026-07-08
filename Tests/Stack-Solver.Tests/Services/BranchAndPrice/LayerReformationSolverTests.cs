using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Metadata;
using Stack_Solver.Models.Supports;
using Stack_Solver.Services;
using Stack_Solver.Services.BranchAndPrice;
using Stack_Solver.Services.Layering;

namespace Services.BranchAndPrice
{
    /// <summary>
    /// Unit coverage of <see cref="LayerReformationSolver"/> — the Level-2 purity mechanism that breaks
    /// a settled solution's layers open at the box level, rebuilds them as pure single-SKU layers, and
    /// repacks. Fixtures build the pallet templates directly so the reformation decision is exercised in
    /// isolation and deterministically.
    /// </summary>
    public class LayerReformationSolverTests
    {
        private static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

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

        /// <summary>
        /// A hand-built mixed layer: the SKUs laid out left-to-right in a single row (kept within the
        /// pallet width). Only the box multiset matters to reformation — it rebuilds from scratch — but
        /// geometry and metrics are computed so the input's impurity is measured faithfully.
        /// </summary>
        private static Layer MixedLayer(Pallet pallet, params (SKU Sku, int Count)[] spec)
        {
            var placements = new List<PositionedItem>();
            int x = 0;
            foreach (var (sku, count) in spec)
                for (int i = 0; i < count; i++)
                {
                    placements.Add(new PositionedItem(sku, x, 0, false));
                    x += sku.Length;
                }

            int height = placements.Max(p => p.SkuType.Height);
            var layer = new Layer("mixed", placements, new LayerMetadata(0.5, height, "mixed test layer"));
            layer.Geometry = LayerGeometryBuilder.Build(layer, pallet);
            layer.Metrics = LayerMetricsCalculator.Compute(layer, pallet);
            return layer;
        }

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
        public void ReformsAMinoritySkuRidingInEveryLayerIntoPurePallets()
        {
            // The Level-2 defect case in miniature: every layer carries one A box among its B boxes, so
            // no whole-layer move can ever purify a pallet (the per-layer impurity term is stuck > 0).
            // Reformation pulls the A boxes out, rebuilds pure A and B layers, and lands each SKU on its
            // own pallet — impurity 0 — while preserving the box totals.
            var pallet = MakePallet();
            var a = Sku("A", 50, 30);
            var b = Sku("B", 26, 13);

            // Two pallets, each two identical (1×A + 2×B) layers ⇒ 4 boxes of A, 8 of B in total.
            var mixed = MixedLayer(pallet, (a, 1), (b, 2));
            var current = new List<(BnpColumn, int)>
            {
                (Column(mixed, mixed), 1),
                (Column(mixed, mixed), 1),
            };
            Assert.True(PurityMetric.TotalImpurity(current) > 0);

            var reformed = LayerReformationSolver.Solve(current, pallet, Budget, TestContext.Current.CancellationToken, extraPalletBudget: 1);

            Assert.NotNull(reformed);
            Assert.Equal(0, PurityMetric.TotalImpurity(reformed));
            Assert.Equal(SkuTotals(current), SkuTotals(reformed!));
            // Two pure pallets suffice; the ε tie-break must not spend the offered extra pallet.
            Assert.Equal(2, reformed!.Sum(c => c.Count));
        }

        [Fact]
        public void KeepsAlreadyPurePalletsPureWithoutInflatingTheCount()
        {
            // Two already-pure pallets: reformation rebuilds the same pure layers and repacks them; it
            // cannot beat impurity 0 and — offered a budget — must not add a needless pallet.
            var pallet = MakePallet();
            var b = FullLayer(Sku("B", 26, 13), pallet);
            var d = FullLayer(Sku("D", 50, 30), pallet);

            var current = new List<(BnpColumn, int)>
            {
                (Column(b, b), 1),
                (Column(d, d), 1),
            };

            var reformed = LayerReformationSolver.Solve(current, pallet, Budget, TestContext.Current.CancellationToken, extraPalletBudget: 2);

            Assert.NotNull(reformed);
            Assert.Equal(0, PurityMetric.TotalImpurity(reformed));
            Assert.Equal(SkuTotals(current), SkuTotals(reformed!));
            Assert.Equal(2, reformed!.Sum(c => c.Count)); // no gratuitous extra pallet
        }

        [Fact]
        public void ReformsDenselyEnoughToFitTheBudget_UsingTheFullGenerationEnsemble()
        {
            // Regression: the reformed pure layers must be built with the full layer-generation ensemble,
            // not the homogeneous packer alone. Box D (50×30) tessellates 6-per-layer under the ensemble
            // but only 4 under the homogeneous strategy — and at 4-per-layer the reformed D pallets blow
            // past the ⌈10%⌉ extra-pallet budget, so the packer finds nothing and no alternative is
            // offered (the field-reported failure). Here 13 mixed pallets (each 8 layers of 6×D + 1×B)
            // must reform to pure pallets within a +2 budget.
            var pallet = MakePallet();
            var d = Sku("D", 50, 30);
            var b = Sku("B", 26, 13);

            var mixed = MixedLayer(pallet, (d, 6), (b, 1));
            var current = Enumerable.Range(0, 13)
                .Select(_ => ((BnpColumn)Column(Enumerable.Repeat(mixed, 8).ToArray()), 1))
                .ToList();
            Assert.True(PurityMetric.TotalImpurity(current) > 0);

            var reformed = LayerReformationSolver.Solve(current, pallet, Budget, TestContext.Current.CancellationToken, extraPalletBudget: 2);

            Assert.NotNull(reformed);
            Assert.Equal(0, PurityMetric.TotalImpurity(reformed));
            Assert.Equal(SkuTotals(current), SkuTotals(reformed!));
            Assert.True(reformed!.Sum(c => c.Count) <= 15, "must fit within K + budget pallets");
            // Identical pallets are aggregated into one column with a count (not one entry per pallet),
            // so the UI shows "type ×N" — one D-pallet type carrying all 13 D pallets, plus the B pallet.
            Assert.True(reformed.Count < reformed.Sum(c => c.Count), "identical pallets must be grouped");
            Assert.Contains(reformed, c => c.Count == 13);
        }

        [Fact]
        public void ReturnsNullForASinglePallet()
        {
            // One pallet cannot be repacked into anything else — nowhere for reformed layers to go.
            var pallet = MakePallet();
            var a = Sku("A", 50, 30);
            var b = Sku("B", 26, 13);

            var current = new List<(BnpColumn, int)> { (Column(MixedLayer(pallet, (a, 1), (b, 2))), 1) };

            var reformed = LayerReformationSolver.Solve(current, pallet, Budget, TestContext.Current.CancellationToken, extraPalletBudget: 1);

            Assert.Null(reformed);
        }
    }
}
