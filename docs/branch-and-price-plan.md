# Branch-and-Price Assignment — Implementation Plan

Status: proposed · Branch: `feat/branch-and-price`

This document specifies a third pallet-assignment algorithm, **branch-and-price (B&P)**,
to sit alongside the existing Greedy and CP-SAT assignment services. It is a
self-contained plan: formulation, architecture, algorithms, integration, testing,
and a de-risked delivery order.

---

## 1. Background and motivation

### 1.1 The problem in OR terms

The assignment stage is a **cutting-stock / bin-packing-with-patterns** problem:

- **Items** = SKUs, each with an exact demand `d_i` (you have exactly `d_i` physical boxes).
- **Columns / patterns** = pallet templates. Each template is a valid stack of layers and
  has a SKU-count vector `a_t` (how many of each SKU it contains). Validity =
  height, weight, and inter-layer support/overhang constraints.
- **Master** = choose how many of each template to build so that demand is met,
  minimizing the number of pallets.

### 1.2 Why the current approach is limited

The current pipeline (`PalletTemplateEnumerator` → `TemplateFilter` → `CPSATAssignmentService`)
**pre-enumerates a capped pool** of templates (≤ 3 SKUs, ≤ 6 layers, homogeneous chains +
stacked pairs + limited mixed), prunes it (utilization floor `0.60`, Pareto dominance),
then optimizes over that fixed pool. Two consequences:

1. **The optimum may use a template the enumerator never produced.** The caps and pruning
   artificially restrict the solution space.
2. **Spurious leftovers.** `TemplateFilter` deletes low-utilization and dominated templates —
   exactly the "near-empty pallet holding a couple of boxes" needed to place the last few
   residual boxes. With no such column available, the solver reports those boxes as
   leftovers even though a valid arrangement exists.

### 1.3 Why branch-and-price

The set of valid pallet templates is exponential. B&P (Dantzig–Wolfe decomposition with
delayed column generation, embedded in branch-and-bound) generates **only the templates
that improve the objective**, on demand, via a *pricing subproblem*. It removes the
enumerator caps, eliminates spurious leftovers (the pricer can always generate a
near-empty pallet when the duals call for it), and yields an **LP lower bound** for
provable optimality / optimality-gap reporting.

---

## 2. Scope decisions (locked)

These were decided during planning and define the algorithm's behavior:

1. **Single objective: minimize total pallets.** The earlier 3-tier lexicographic
   objective is dropped:
   - *Tier 3 (minimize distinct templates)* — **dropped.** It is a per-pattern fixed
     charge with no clean reduced-cost structure (the "pattern minimization" variant of
     cutting stock); not worth the disproportionate B&P effort.
   - *Tier 1 (minimize leftovers)* — **dropped as an objective.** With unlimited pallets,
     any *physically placeable* SKU can always be placed (worst case: a single-box
     homogeneous pallet). Leftovers are therefore only possible for *physically
     unplaceable* SKUs, which are handled by an up-front feasibility check (§4.2), not by
     the optimizer.
2. **Demand is a hard equality.** You have exactly `d_i` boxes, cannot place more than you
   have, and can always place all placeable boxes — so `Σ_t a_{t,i}·x_t = d_i`. No
   leftover slack variable in the model.
3. **Unplaceable SKUs are reported as leftovers with a warning** (not a hard validation
   error). Generation proceeds for the rest; the unplaceable SKUs appear in
   `AssignmentResult.Leftovers` and surface in the existing leftovers UI.
4. **Added as a third selectable algorithm.** Greedy and CP-SAT assignment remain
   untouched. B&P is selectable and shown as an additional solution.

This collapses the problem to the canonical Gilmore–Gomory cutting-stock B&P — the
cleanest possible form.

---

## 3. Mathematical formulation

### 3.1 Restricted Master Problem (RMP) — LP relaxation

Over a current subset `R` of columns (templates), solved with **GLOP** (the LP solver in
`Google.OrTools`, which exposes dual values; CP-SAT cannot):

```
minimize    Σ_{t∈R} x_t
subject to  Σ_{t∈R} a_{t,i}·x_t = d_i      ∀ placeable SKU i      (dual π_i, free sign)
            x_t ≥ 0
```

- `x_t` — number of pallets of template `t` (continuous in the RMP, integer in the IP).
- `a_{t,i}` — count of SKU `i` in template `t`.
- `d_i` — exact demand for SKU `i` (placeable SKUs only).
- `π_i` — dual price of the demand constraint for SKU `i` (unrestricted in sign, because
  the constraint is an equality).

### 3.2 Pricing subproblem

Find a new valid pallet template `t` with **negative reduced cost**:

```
reduced_cost(t) = 1 − Σ_i a_{t,i}·π_i
```

Add the column if `reduced_cost(t) < 0`, i.e. if there exists a valid pallet whose
dual-weighted value `Σ_i a_{t,i}·π_i` exceeds `1`. The pricer therefore **maximizes the
dual-weighted value of a valid pallet** (§5).

### 3.3 Feasible start (no artificial variables)

Seed `R` with one **homogeneous max-stack pallet per placeable SKU** (the enumerator
already builds these via `TryAddRepeated`). This guarantees:

- the RMP is feasible from the first solve (every demand constraint is coverable), and
- an immediate integer **incumbent** (essentially the homogeneous baseline), which column
  generation then improves.

No Big-M / artificial columns are required.

---

## 4. Algorithm

### 4.1 Top-level flow

```
Assign(layers, demand, pallet, options, warmStart, ct):
    (placeable, unplaceable) = SkuPlaceabilityCheck(demand, layers, pallet)   // §4.2
    if placeable is empty: return AssignmentResult{ Leftovers = unplaceable }

    pool = SeedHomogeneousColumns(placeable, layers, pallet)                   // §3.3
    incumbent = RoundToInteger(pool, demand)                                   // baseline

    root = new BnpNode(constraints = none)
    best = BranchAndBound(root, pool, incumbent, ...)                          // §4.3

    return ToAssignmentResult(best, leftovers = unplaceable)
```

### 4.2 SKU placeability pre-check

A SKU is **placeable** iff at least one valid single-layer pallet containing it exists:
there is a layer using only that SKU whose height ≤ `MaxStackHeight − Height`, whose
weight ≤ `MaxStackWeight`, and which forms a valid layer on the flat pallet surface
(footprint / overhang OK). Reuse `LayerGenerator` / existing homogeneous layers to test.

- Placeable SKUs → enter the model.
- Unplaceable SKUs → added directly to `AssignmentResult.Leftovers`, surfaced as a warning.

### 4.3 Branch-and-bound with column generation at each node

```
BranchAndBound(node, pool, incumbent):
    lp = ColumnGeneration(node, pool)          // §4.4 — solve LP relaxation at this node
    if lp infeasible or lp.objective ≥ incumbent.pallets: prune
    if lp.solution is integer:
        update incumbent if better; return
    (branchA, branchB) = BranchingRule.Branch(lp)   // §6
    BranchAndBound(branchA, pool, incumbent)
    BranchAndBound(branchB, pool, incumbent)
```

The column pool is **shared and cumulative** across nodes. Branch constraints restrict
which columns the pricer may generate (§6); they do not delete pooled columns, they
disable them per node.

### 4.4 Column-generation loop (per node)

```
ColumnGeneration(node, pool):
    loop:
        (x, π) = RMP.Solve(pool ∩ node.allowedColumns)     // GLOP, returns primal + duals
        cols   = PricingSolver.Find(π, node.constraints)    // §5 — heuristic first, exact to confirm
        if cols is empty: return (x, RMP.objective)         // LP optimum at this node
        pool.AddDistinct(cols); RMP.AddColumns(cols)
        respect ct and iteration cap
```

Generate the top-k improving columns per round to reduce iterations. Apply **dual
stabilization** (smoothing / interior-point duals) to curb tailing-off.

---

## 5. Pricing solver (algorithmic core)

Layers already exist (from `LayerGenerator`). A layer's dual-weighted value is
`value(layer) = Σ_i count_i(layer)·π_i`. Pricing = choose an **ordered, valid stack of
layers** maximizing total value, subject to:

- total height ≤ `MaxStackHeight − Height`
- total weight ≤ `MaxStackWeight`
- consecutive-layer support via `LayerSupportAnalyzer` (overhang ≤ `MaxSkuOverhang`)
- weight ordering (heavier layers below), matching the enumerator
- `MaxDistinctSkusPerTemplate` cap (kept configurable; treat as a real constraint unless
  confirmed to be only a perf hack)
- any active **branch constraints** from the current B&B node (§6)

Because support depends only on the **immediately adjacent** layer, the problem has
optimal substructure → solve as a **resource-constrained longest path / DP**:

- State: `(topLayer, usedHeight, usedWeight, distinctSkuSet)`
- Transition: append layer `L` if `IsTransitionValid(top, L)`, weight ordering holds, caps
  not exceeded, and branch constraints allow it
- Objective: maximize accumulated layer value

Provide **two pricers**:

1. **Heuristic pricer** (default each round): best-value-first / beam search over the DP.
   Fast; finds *some* negative-reduced-cost column to make progress.
2. **Exact pricer** (only when the heuristic returns nothing): full DP with height/weight
   **bucketing** + dominance pruning, or a CP-SAT formulation of the stack. Required to
   *prove* no improving column exists (LP optimality). Honors a slice of the time budget.

The exact pricer guarantees correctness; the heuristic guarantees speed.

---

## 6. Branching scheme (highest-risk component)

Variable branching on `x_t` is **incompatible** with column generation: forcing `x_t = 0`
does not stop the pricer from regenerating the same column. Use **Ryan–Foster-style
branching adapted to integer demands**:

- From the fractional LP solution, pick a pair of SKU units `(i, j)` that are *fractionally
  together* across columns.
- **"Together" branch**: every generated column must contain `i` and `j` with the linked
  multiplicity — enforced in the pricer DP transitions.
- **"Apart" branch**: no column may contain both — forbidden in the pricer DP.

The pricer must honor active branch constraints; branching and pricing are co-designed.

**De-risking:** build the root node first with *no branching* (LP + round-the-LP heuristic
to integer). That already removes the enumerator caps and beats CP-SAT-over-pool. Add
branching afterward to close the gap to provable optimality.

---

## 7. Incumbent, timeout, cancellation

- Respect `GenerationOptions.MaxSolverTime` as a global wall-clock budget, with a sub-budget
  for exact pricing (mirrors `CPSATAssignmentService`'s phase budgeting).
- Maintain an **incumbent** at all times: seed from the homogeneous baseline (§3.3) or from
  the `warmStart` Greedy result; update whenever B&B finds a better integer solution.
- On timeout or `CancellationToken` cancellation, return the incumbent as a feasible
  `AssignmentResult` — never discard work, never throw (except `OperationCanceledException`
  semantics already used by callers).
- Check `ct` at every RMP solve, pricing call, and node expansion.

---

## 8. Architecture and files

New namespace `Stack_Solver.Services.BranchAndPrice` in `Stack-Solver.Core`, parallel to
the existing assignment services. Output is the existing `AssignmentResult`, so
`SolutionDisplay`, 3D rendering, and drill-down work unchanged.

```
Stack-Solver.Core/Services/BranchAndPrice/
  BranchAndPriceAssignmentService.cs   // public entry; mirrors CPSATAssignmentService.Assign signature
  RestrictedMasterProblem.cs           // GLOP LP wrapper: build/extend, solve, expose primal x + duals π
  PricingSolver.cs                     // duals (+ branch constraints) → improving pallet columns (heuristic + exact)
  ColumnPool.cs                        // dedup by SKU-signature (reuse enumerator's Signature scheme)
  BranchingRule.cs                     // pick fractional branch; emit child constraints + pricer modifications
  BnpNode.cs                           // node = active branch constraints + allowed-column predicate
  BnpColumn.cs                         // generated pallet template + SkuCounts vector + layer list
  SkuPlaceabilityCheck.cs              // §4.2 — split demand into placeable / unplaceable
  BnpOptions.cs                        // tolerances, iteration/node caps, pricing time fraction, stabilization
```

Proposed entry signature (matches existing services for a drop-in call site):

```csharp
public static AssignmentResult Assign(
    IReadOnlyList<Layer> layers,
    IReadOnlyDictionary<string, int> demand,
    Pallet pallet,
    GenerationOptions options,
    AssignmentResult? warmStart = null,
    CancellationToken ct = default)
```

---

## 9. Integration into the app

1. **Algorithm selector.** Today selection is a single `UseCpsat` boolean that actually
   controls CP-SAT *layer generation* (`LayerGenerator.Generate`), while
   `ResultsViewModel.BuildSolutions` always runs both Greedy and CP-SAT *assignment*.
   Add a separate assignment-algorithm choice for B&P:
   - Simplest: an additional `UseBranchAndPrice` toggle.
   - Cleaner: an enum `AssignmentAlgorithm { Greedy, CPSAT, BranchAndPrice }`.
   - Keep the existing layer-gen CP-SAT toggle as-is.
   - Flows through `PalletSettingsDto` → `PalletBuilderSettingsViewModel` → the
     settings-changed message → `ResultsViewModel`.
2. **`ResultsViewModel.BuildSolutions`** (~line 309): add a third block, guarded by the
   selector, calling `BranchAndPriceAssignmentService.Assign(filtered, demand, pallet,
   options, greedy /* warm-start */, ct)` and appending
   `new SolutionDisplay(result.Count + 1, "Branch & Price", bnpResult, …)`. Wrap in the
   same best-effort try/catch so a B&P failure never breaks Greedy/CP-SAT.
3. **`GenerationOptions`**: add B&P knobs (max nodes, max CG iterations, pricing time
   fraction, stabilization on/off, distinct-SKU cap), following the existing `From` / ctor
   pattern. Surface the important ones in settings; default the rest.
4. **UI**: add the toggle/selector in `PalletBuilderPage.xaml` next to the existing
   `UseCpsat` switch.
5. **`defaults.json`**: add the new defaults next to `MaxSolverTime` / `MaxCPSATCandidates`.

No changes needed to `AssignmentResult`, `SolutionDisplay`, rendering, or drill-down.

---

## 10. Testing

Mirror the existing `Tests/Stack-Solver.Tests/Services` layout.

- **Unit**
  - `PricingSolver` reduced-cost correctness: hand-computed duals → expected best column.
  - `RestrictedMasterProblem` returns correct primal + duals on a tiny instance.
  - `ColumnPool` dedup matches the enumerator's SKU-signature.
  - `SkuPlaceabilityCheck` classifies oversized / overweight / oversized-footprint SKUs as
    unplaceable and everything else as placeable.
- **Quality / correctness**
  - LP lower bound ≤ integer B&P objective ≤ baseline (sandwich check).
  - On instances where the capped enumerator provably misses the optimum, B&P finds it.
  - B&P pallet count ≤ CP-SAT-over-enumerator count on shared instances.
  - **Zero spurious leftovers**: every placeable SKU is fully placed; only unplaceable SKUs
    appear in `Leftovers`.
- **Robustness**
  - Determinism: fixed seed → stable output.
  - Cancellation mid-run returns a valid incumbent.
  - Timeout returns the incumbent, not an exception.

---

## 11. Delivery milestones (de-risked order)

1. ✅ **GLOP RMP + seeded pool + duals.** Solve the LP over the homogeneous-seed pool;
   expose duals. Validates GLOP wiring and the master math. *(small)* — **Done.** Implemented
   `BnpColumn`, `ColumnPool`, `RestrictedMasterProblem` (GLOP), `ColumnSeeder`, and
   `BranchAndPriceAssignmentService.SolveRelaxation` / provisional `Assign`. Covered by
   `BranchAndPriceRelaxationTests` (analytic objective + duals, unplaceable SKUs, baseline
   incumbent). Not yet wired to the UI (milestone 5).
2. ✅ **Heuristic pricer + column generation, root node only**, integer solution by LP
   rounding. *(large)* — **Done.** Implemented `PricingSolver` (beam-search heuristic
   respecting height/weight/weight-ordering/support/distinct-SKU caps), the root
   column-generation loop in `RunColumnGeneration`, and `BuildIncumbent` (⌊LP⌋ full pallets
   + greedy layer-stacker for the residual). Covered by `PricingSolverTests` and
   `ColumnGenerationTests`. **Note:** with only full-grid layers available, exact demand
   cannot always be tiled, so a sub-layer remainder is reported as a leftover (same
   semantics as the existing greedy); the LP stays pure-equality and feasible. Heuristic
   pricer caps stacks at 6 layers (matching the enumerator) — the exact pricer lifts this.
3. **Exact pricer** → true LP lower bounds and optimality-gap reporting. *(medium)*
4. **Ryan–Foster branching** → provable integer optimality. *(large, highest risk)*
5. **Integration**: SKU placeability + leftovers warning, UI selector, settings, defaults.
   *(medium)*
6. **Stabilization, perf tuning, full test suite, docs.** *(medium)*

Milestone 2 is the point of diminishing risk: if effort runs out there, the result is
already strictly better than today's capped enumerate-then-CP-SAT path.

---

## 12. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Pricer state-space blowup (height/weight resources) | Bucketing + dominance pruning + DP; beam-search heuristic for the common case. |
| Branching/pricing co-design bugs (classic B&P failure mode) | Build root-only first; isolate behind unit tests; add branching last. |
| Tailing-off in column generation | Dual stabilization, iteration caps, multi-column rounds. |
| Interactive latency (B&P heavier than current path) | Incumbent-on-timeout; global wall-clock budget; runs on `Task.Run` like the others. |
| GLOP numerical issues on equality constraints | Tolerances in `BnpOptions`; validate duals; round near-integer LP values. |

---

## 13. Dependencies

- `Google.OrTools` 9.15 (already referenced) — provides GLOP via
  `Google.OrTools.LinearSolver`. **No new package required.**
