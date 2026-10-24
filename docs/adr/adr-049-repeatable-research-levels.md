# ADR-049 — Repeatable research levels (both trees, final Age)

**Status: PROPOSED for the M6 tranche-2 merge, under the Director's direction D-063 R4 (2026-10-24) and change request
CR-024 (ruled).** Implemented on branch `t2-levels` behind the research.json switch `tuning.repeatableLevels`, shipped
**false**, so no golden moves on this branch. It extends D-044 R11 ("completion is immediate and idempotent; a node
completes once") for repeatable levels and resolves the open item in D-044 Part D T6. Working contract:
`docs/m6-plan/knowledge-redesign/tranche2-contract.md` §4.

## 1. The decision

1. **Content.** A repeatable node's descriptor keeps its documentary fields and gains machine fields:
   `"levels": true`, `"costGrowth": g` (g ∈ [1.10, 1.30]) and `"effects": [ { "operand": id, "gapFraction": k } ]`
   (k ∈ (0, 0.5], at least one effect, no operand twice, every id in the tranche-2 operand registry of contract §1.1).
   A descriptor without `"levels"` stays documentary: the node completes once. A levelled node may not unlock an
   entity (the loader refuses it): its benefit is its operand effects only, so a level can never re-grant anything.
2. **Which nodes may repeat.** `researchSets.recursive` is still exactly the set of nodes with a repeatable descriptor,
   cost tier `TR` and only it.
   - A **subtree technology** repeatable keeps ruling 3: own prerequisites AND every finite node of its own subtree.
   - A **trunk technology** repeatable (new): own prerequisites only.
   - A **civics** repeatable (new): own prerequisites AND every finite civic of Age A9 complete (the end-of-tree safety
     valve). A civics repeatable must itself be A9. This reads the node's content Age label (`ResearchContentLoader.FinalAge`)
     — never the polity's Age and never a date — which is what CR-024 option 1 rules for these rules.
3. **Completion (switch on).** Completing a levelled node raises the holder's level by one and keeps the node
   AVAILABLE. Only the **first** level appends the `ResearchCompleted` row, so prerequisites, the research stage, entity
   eligibility, the completion event (`ResearchQuery.CompletedBetween`) and every other completed-knowledge reader see
   the node once; later levels append nothing. Each level is otherwise an ordinary completion: the progress row is
   removed (progress restarts at 0 for the next level) and the node stops being the target (a player re-orders it; the
   AI re-picks).
4. **Cost.** Level n is priced from `BaseCost × g^(n−1)` (`ResearchQuery.LevelBaseCost`); the university modifiers and
   the effective-cost floor apply to that base exactly as they apply to BaseCost (`ResearchQuery.EffectiveCost` and
   `EffectiveCostBreakdown` price the holder's NEXT level). Level 1 is BaseCost exactly.
5. **Credits and Eurekas** stay **once per node**: each Eureka fires once, and the one acceleration pool (ceiling ×
   BaseCost of level 1) is shared across all levels. A credit lands on whichever level is in progress when it is paid —
   in practice level 1. Later levels are paid in RP (technology) or CP (civics). Reason: a Eureka is a circumstance,
   and a circumstance that held once does not hold "again" for the next level; re-arming it per level would be a flat,
   free-floating cost cut (law 2).
6. **Benefit.** Level i adds `cap × k × (1 − k)^(i−1)` to Σ of its operand; n levels add
   `ResearchQuery.RepeatableContribution(n, k, cap) = cap × (1 − (1 − k)^n)`, exactly the geometric sum
   `cap × Σ_{i=1..n} k(1 − k)^(i−1)`. It never exceeds the cap: in exact arithmetic it is strictly below it for every n;
   in double precision it equals the cap once `(1 − k)^n` drops below half an ulp of 1 (n ≈ 350 for k = 0.1). The effects
   engine (track t2-effects) adds `ResearchQuery.LevelContribution(world, content, holder, operand, cap)` — the sum over
   the holder's levelled nodes naming that operand, index order — to the operand's Σ before the clamp to [0, cap]
   (contract §1 step 1). This branch consumes no operand; the numbers are ready for that seam.
7. **Level cap.** `ResearchQuery.MaxRepeatableLevel = 1000`. At the cap a node is no longer available (the AI, the
   order constructor and the UI all see it). 1,000 keeps every level cost finite for the steepest legal growth
   (1.30^999 ≈ 2.6 × 10^113, far below double's 1.8 × 10^308; 10,000 would overflow from about level 2,700) and is
   unreachable in play (level 100 already costs g^99 ≥ 1.3 × 10^4 × level 1). The level is an `int`.
8. **Level of a node.** `ResearchQuery.Level(world, polity, node)` is the holder's level row, or 1 when the node is
   completed without a row (a v35 save, or a repeatable completed with the switch off), else 0.

## 2. Schema v36, section 67

`ResearchLevelRow(PolityId Polity, ResearchNodeId Node, int Level)` — 12 bytes; at most one row per (holder, node);
`1 ≤ Level ≤ 1000`. Owned by ResearchSystem; a row is appended at the holder's first level and replaced in place after,
so the table's order depends only on the order of first completions (the ResearchCompleted precedent). Appended after
section 66 (SettlementSurveys); no existing row changed width. Readable set {32, 33, 34, 35, 36} (`CanonicalSchema.IsReadable`,
the one helper Snapshot.Load, SaveFiles.MigrationNote, SnapshotDiff.LayoutFor and ReplayGuard read).

**Migration ("empty tables").** A v35 stream stops after section 66 and loads with an EMPTY ResearchLevels table. This
is exact: no level existed under v35; a repeatable completed there reads level 1 through its completed row (§1.8).
`Snapshot.Load` refuses a level table no rule could write (a level outside [1, 1000], two rows for one (holder, node)).

**Goldens.** The new section adds one empty count prefix (4 bytes) to every world's stream, so raw stream hashes move for
the layout alone. With the switch off no level row is ever written, so every world is its v35 stream plus that prefix:
`SchemaProjection.V35` (strip section 67, assert it empty, drop its prefix) reproduces every v35 pin byte for byte.

## 3. AI and city-states

No new rule. A levelled node stays in the available set, priced at its next level, so once the AI's goal closures are
exhausted its existing fallback — the cheapest available node anywhere, composite key (EffectiveCost, node key),
ascending-key scan replacing only on a strictly lower cost — reaches the repeatable levels instead of idling. A
city-state's autonomous pick is the same `AiResearchPolicy.Cheapest`, so it reaches them the same way. Tie-dense test:
`ResearchLevelTests.Ai_WithEveryFiniteNodeDone_TargetsTheCheapestNextLevel_TiesToTheLowerKey`.

## 4. UI

The research panel never shows a levelled repeatable as complete. The card's state line reads
"Available · Researched N× · level N+1 · ~T turns"; the detail panel's LEVELS section shows "Researched N×", the next
level's cost, the cost growth, and per operand the registry meaning, the next level's benefit
(`RepeatableLevelBenefit(N+1)`), the cumulative benefit (`RepeatableContribution(N)`) and the cap, as percentages of the
operand's improvement Σ. The Glass Box node card says "Researched N×; next level: …" in place of "Known".

## 5. Shipped content

- The ten `frontier_*` descriptors keep every documentary field, gain `"levels": true`, `"costGrowth": 1.2`, one effect
  with `"gapFraction": 0.1`, and an `_effects` note of the operand choice:

  | node | operand | honesty |
  |---|---|---|
  | frontier_crops | farming.yieldPerArableKm2PerYear | causal |
  | frontier_medicine | demographics.mortalityPerYear | causal |
  | frontier_materials | housing.buildTimberPerDwelling | causal (lighter, cheaper structures) |
  | frontier_energy | production.outputPerExtractorPerYear | causal (more output per unit of fuel) |
  | frontier_computation | research.researchPointsCoefficient | causal (automation, administration) |
  | frontier_cyber | society.unrestOutputDrag | causal (information defence) |
  | frontier_climate | harvestVariance.sigmaLogYield | causal (remediation steadies harvests) |
  | frontier_military | fog.formationSightNodes | **proxy** — no combat before M7; sensing range is the nearest live operand |
  | frontier_launch | trade.costPerBulkCostUnit | **proxy** — no space programme; cheaper lift as cheaper bulk haulage (roads.baselineKmPerDay is not causal) |
  | frontier_propulsion | fog.exploreMaxRadiusNodes | **proxy** — no interplanetary transit; faster probes as longer exploration reach |

- Three **civics repeatables** in A9, keys **1990–1992** at the END of the civics array (keys 1068–1989 are reserved for
  the parallel civics expansion, which is inserted before them keeping keys ascending; nothing here assumes repeatables
  are a tree's last nodes): `institutional_reform` (requires `parliamentary_government AND central_planning`; depth 16;
  33,450; governance.authorityDecayCostUnits), `social_policy_research` (requires `mass_political_party`; depth 13;
  16,730; society.grievanceRelief), `civic_participation` (requires `mass_suffrage AND parliamentary_government`;
  depth 12; 23,650; research.civicsPointsCoefficient). Tier TR, costGrowth 1.2, gapFraction 0.1, in
  `researchSets.recursive`. Costs are U × K^magnitude from their factors, as every node's.
- `tuning.repeatableLevels: false`.

## 6. The operand registry (to be unified at merge)

`Sim.Core/Systems/Research/RepeatableOperands.cs` holds the 47 rows of contract §1.1 (id, direction, cap, meaning) — the
contract table has 47 rows, not the 49 the coordination brief mentions. It is the one place the loader validates
operand ids against and the UI reads caps and meanings from. Track t2-effects builds the canonical C# operand registry;
at merge this list is replaced by a read of that registry (the rows were copied from the same table and must agree).

## 7. The attribution switch

`tuning.repeatableLevels` false (or absent) is the complete-once engine exactly: `IsLevelled` is false for every node,
availability, cost and completion take their pre-ADR paths, and no ResearchLevels row is ever written. Pinned by
`ResearchLevelTests.SwitchOff_*` and by every existing golden reached through `SchemaProjection.V35` unchanged.

## 8. Deferred

- The effects engine reading `LevelContribution` (t2-effects); the sweep's exclusion of repeatables (t2-routing:
  contract §2 "Repeatables are not sweep members").
- Whether a player's target should persist across levels (auto-repeat) — today a level clears the target like any
  completion.
- Turning the switch on and re-pinning the goldens it moves (a tranche-merge decision).
