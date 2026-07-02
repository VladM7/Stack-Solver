# Box-level pricing: build pallets from boxes (support-aware finder + CP-SAT proof)

> Status: APPROVED plan, not yet implemented. Builds on the (uncommitted) Stage-1 pallet-count
> branching on branch `perf/bnp-pallet-count`. Saved here for durability; working copy was at
> `~/.claude/plans/mossy-sleeping-nebula.md`.

## Context

Branch-and-price proves optimality only over a **fixed pre-generated layer set** — the pricers stack
existing layers, never building new ones. So leftovers of different SKUs can't share a layer, and the
solver returned **2 pallets "proven optimal"** for the user's 50/30/3 case (A 38×23×20 ×50,
B 26×13×20 ×30, D 50×30×20 ×3; pallet 120×80×14, max stack 180, max weight 950, 50% top-heavy,
0 overhang / 100% support) when 1 pallet is achievable by merging the leftover B and D — a **false
certificate**, the one outcome this project must never produce.

The fix makes the **pricing subproblem build pallets from boxes**, guided by the SKU duals π_i, so it
can synthesize any support-valid layer/pallet — generalizing column generation to the full space of
physical pallets so the bound and certificate are sound over *all* pallets, not a subset.

Decisions taken across review:
- **Finder = heuristic, support-aware, owns result geometry.** A constructive dual-guided pallet
  builder generates each layer's boxes constrained to the occupancy of the layer below (so
  arrangements like "2 side-by-side + 1 over the gap" are exploited), producing clean, support-valid
  geometry. No CP-SAT in the result path.
- **Proof = CP-SAT, proof-only.** An exact dual-weighted CP-SAT solve runs sparingly to *certify*;
  its arrangements are never displayed.
- **Bounded + honest.** Keep the ≤10-min budget; report `proven optimal = false` if the exact check
  can't finish.

Two phases. **Phase 1** (the constructive finder) fixes the wrong answer and — via Stage-1's
**Found-at-K** existence witness, which needs no layer-completeness — soundly certifies consolidation
cases like 50/30/3. **Phase 2** (CP-SAT exact check) makes the *infeasibility/LP-bound* certification
path globally sound. Builds on the uncommitted Stage-1 pallet-count branching (`perf/bnp-pallet-count`).

## Phase 1 — Constructive support-aware dual-guided pallet pricer (the finder)

New `ConstructivePalletPricer` (`Services/BranchAndPrice/`) that, given the duals, builds a pallet
**bottom-up from boxes**, returning improving columns (pallets where Σ_i a_i π_i > 1). It augments —
does not replace — the existing stacking pricers (keep those over the app's nice layer pool; the
constructive pricer adds the box-built columns the pool lacks). It fires when the pool pricers stall,
per node.

Algorithm (reusing existing geometry machinery):
- Track a **support region** = occupancy grid of the current top (start = full pallet), plus
  `usedHeight`, `usedWeight`, pallet SKU set, density-of-layer-below, and per-SKU remaining cap = d_i.
- **Build the next layer**: greedily place box variants (`SkuVariantFactory.CreateAllOrientations`),
  preferring high-π_i SKUs, at grid positions (grid = gcd of dims, as `CPSATGenerationStrategy` /
  `LayerGeometryBuilder` use) where the box's footprint cells lie **entirely within the support
  region** (100% support) and don't overlap boxes already in this layer — respecting remaining cap,
  the `MaxDistinctSkusPerTemplate` cap, `usedWeight ≤ MaxStackWeight`, `usedHeight + boxHeight ≤
  availHeight`, and the top-heavy density rule (`StackingLoadRule`: new layer density ≤ below ×
  (1+tol)). Row-major / shelf placement keeps the geometry clean for display.
- Add the layer (`Layer` built via `LayerGeometryBuilder` + `LayerMetricsCalculator`, so it
  materializes and renders like any other), set support region = its occupancy, repeat until height
  is exhausted or no box can be placed.
- Emit the pallet as a `BnpColumn`; if Σ a_i π_i > 1 it is an improving column. Run a few attempts
  (varying SKU priority / orientation order) for diversity.

**Support-awareness** is the occupancy-region constraint above: layer k+1's boxes must sit on layer
k's occupied cells. This is the same notion `LayerSupportAnalyzer` already encodes (occupancy grids
+ cell coverage); the finder enforces it *while placing*, so it discovers support-coupled
arrangements the old isolated generation missed.

**Wiring.** Derive the `SKU` set from the layers via the existing `BuildSkuMap(layers)` pattern (box
dims/weight/rotatable) and hand it + pallet + demand caps to `BranchAndPriceSearch`; call the
constructive pricer inside `SolveNode`'s CG loop after the stacking pricers find nothing. Columns
flow through the master unchanged.

**Why 50/30/3 is fixed soundly.** Under the K=1 cap, the constructive pricer builds the single pallet
(5 full A layers, 1 full B layer, a merged 5A+6B layer on top, a 3D layer) — zero leftover →
**Found at K=1 → optimum 1**. Found is an existence witness, so the certificate is globally sound
regardless of layer completeness.

## Phase 2 — CP-SAT exact dual-weighted check (global certificate, proof-only)

At node convergence, run **one** CP-SAT solve that bounds the maximum pallet dual value Σ_i a_i π_i
over all valid stacked pallets (reuse `CPSATGenerationStrategy`'s candidate/non-overlap/quantity
model with objective `Maximize(Σ π·use)`, extended to the stacked-pallet constraints). If the proven
max ≤ 1 → no improving column → the node's LP bound is a valid *global* lower bound → certify. If it
finds a pallet > 1 → a missed improving column → add and continue CG. Timeout → uncertified (honest).
Dropping the hardest constraint (100% inter-layer support) only *loosens* an upper bound, so the
check stays **sound** even relaxed (certifies fewer nodes, never falsely); tightening support is an
incremental follow-up. This replaces the pool-relative exhaustiveness signal
(`ExactPricingSolver.LastSearchExhaustive` → `_allCertified`) for the LP-bound certification paths.

## Soundness scope (stated honestly)

After both phases, "proven optimal" is sound over the **grid-based packing model** (placements on the
gcd-of-dimensions grid) — far richer than today's fixed layer set and matching the app's existing
packing fidelity, but not full continuous-geometry optimality (out of scope throughout this codebase).

## Critical files

- `Stack-Solver.Core/Services/BranchAndPrice/ConstructivePalletPricer.cs` (new, Phase 1) — the
  support-aware constructive finder; reuses `SkuVariantFactory`, `LayerGeometryBuilder`,
  `LayerMetricsCalculator`, `StackingLoadRule`, `PricingRules`, occupancy grids.
- `Stack-Solver.Core/Services/BranchAndPrice/BranchAndPriceSearch.cs` — call the constructive pricer
  in `SolveNode`'s CG loop; (Phase 2) swap the certification signal.
- `Stack-Solver.Core/Services/BranchAndPrice/BranchAndPriceAssignmentService.cs` — pass SKUs/pallet/
  demand caps to the search.
- `Stack-Solver.Core/Services/BranchAndPrice/ExactPalletPricer.cs` (new, Phase 2) — CP-SAT
  certification model, reusing `CPSATGenerationStrategy`'s formulation with a dual objective.
- Existing stacking pricers (`PricingSolver`, `ExactPricingSolver`) stay as-is (additive change).

## Verification

- **Unit** — `ConstructivePalletPricer`: for the 50/30/3 SKUs and demand caps, with duals favoring
  B/D it builds a single zero-leftover pallet placing all demand; every layer is support-valid (each
  box's cells lie on the layer below per `LayerSupportAnalyzer.Analyze`); deterministic across runs.
- **Integration (headline)** — the exact 50/30/3 instance via `Solve` returns **1 pallet, all demand
  placed, no leftover, `LowerBoundCertified == true`** (Phase 1, Found-at-K). Box weights weren't
  given; the test sets them so 950 kg is non-binding (matching the observed mergeable pallet) —
  confirm real weights if the limit should bind.
- **Support-coupling** — a small crafted instance where the optimum needs boxes placed over a partial
  layer's gap (the 2+1 case): the constructive pricer finds it; a from-isolation generation does not.
- **Differential** — same instance with the constructive pricer disabled still returns 2, pinning the
  fix; **Phase 2**: an optimum certified only by lower-count infeasibility is `LowerBoundCertified`
  only with the CP-SAT check, and the check rejects a hand-built infeasible "improving" pallet.
- **Regression** — `dotnet test`, all existing tests (74 + Stage-1) pass (additive).
- **Manual** — rerun 50/30/3 in the app: 1 pallet, proven optimal, clean geometry; confirm a medium
  instance (3 SKUs ~100 each) stays within the 10-min budget.

## Out of scope / follow-ups

- Tightening Phase-2 support encoding toward exact; continuous (non-grid) optimality.
- CP-SAT latency tuning (time slices, caching across nodes/duals) for the 10-min target.
- `LayerPackingHeuristic` using the constructive builder for a tighter initial incumbent.
- Commit Stage-1 pallet-count branching together with Phase 1.

## Resume notes (where we left off)

- **Stage 1 (pallet-count branching): implemented, all 74 tests green, NOT committed**, on branch
  `perf/bnp-pallet-count` (off `perf/bnp-pricing`). Files touched: `RestrictedMasterProblem`
  (cardinality cap), `PricingSolver`/`ExactPricingSolver` (reducedCostThreshold param),
  `BranchAndPriceSearch` (`CertifyByPalletCount` + `SearchCappedZeroLeftover`), `BranchAndPriceStats`,
  plus tests (`RestrictedMasterProblemTests`, `PalletCountCertificationTests`, threshold test).
- **Phase 1 of THIS plan: not started.** APIs already gathered for the constructive pricer —
  `SkuVariant`(VariantId,Sku,SpanX,SpanY,Rotated) via `SkuVariantFactory.CreateAllOrientations`;
  `PositionedItem(SKU,x,y,rotated)` with `GetXSpan`/`GetYSpan` (X=Length, Y=Width unless rotated);
  `LayerGeometryBuilder.Build(layer, supportSurface, gridStep)` → `OccupancyGrid[y,x]`;
  `LayerMetricsCalculator.Compute` → `LoadDensity = TotalWeight/FootprintArea`;
  `LayerMetadata(utilization,height,description)`; `Layer(name, List<PositionedItem>, metadata)`;
  `StackingLoadRule.Allows(lowerDensity, upperDensity, tolerance)`; `Pallet.AvailHeight =
  MaxStackHeight - Height`, `MaxStackWeight`, `LoadDensityTolerance = MaxTopHeavyPercent/100`,
  `OverhangRule`; `PricingRules.MaxDistinctSkusPerTemplate = 3`; `BnpColumn(PalletTemplate)` with
  SKU-count signature; `PalletTemplate.FromLayers(layers)`. Box weights for the 50/30/3 instance were
  NOT provided — set non-binding in tests unless the user supplies them.
