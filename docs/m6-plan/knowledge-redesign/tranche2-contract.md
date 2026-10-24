# Tranche 2 contract: effects, Age sweep, destination research, repeatables, expansion

**Status: WORKING CONTRACT for the M6 tranche-2 build (Director direction 2026-10-24).** Every agent on this tranche
follows it. If it is wrong, report the problem; do not silently deviate. The governance record is CR-024 and the
Director ruling D-063 (see §9).

## 0. Authoritative content

- **Runtime truth** is `Sim.Data/content/research.json`: 496 technologies (keys 1–496) and 67 civics (keys 1001–1067).
  All other content files under `Sim.Data/content/` are runtime truth too.
- **Proposal data** (`docs/m6-plan/knowledge-redesign/*-tree-proposal.json`) is design input only.
  - The proposal shows 494 technologies because it retires 2 through merges. Those merges are DEFERRED (non-breaking
    tranche, ADR-045), so runtime still has 496.
  - The D13 lane (121 civics, keys 1068–1188 in the proposal) is not in runtime yet.
- **Stability.**
  - Existing ids and keys never change.
  - New technology keys are appended at 497 and up.
  - New civic keys are appended after the last civic key.
  - The proposal's D13 keys are kept when D13 is imported.

## 1. Research effects ("every node changes gameplay")

Completion still builds nothing. ResearchSystem only records completion; owning systems read the polity's knowledge and
change their own equations (D-044 R14 is kept). A node declares this in research.json:

```json
"effects": { "immediate": [], "realized": [ { "operand": "farming.outputPerFarmerPerYear", "delta": 0.04 } ] }
```

- **`realized`** is a list of typed effects. In tranche 2 the only kind is the *operand effect* (`operand`, `delta`).
- **`delta`** is an IMPROVEMENT fraction, 0 < delta ≤ 0.5. The registry turns improvements into parameter values.
- **Combining effects.** For a holder (polity, or a city state's local key) with completed set S:
  1. `Σ = Σ delta over S`, plus repeatable levels (§4).
  2. Clamp Σ to `[0, cap]`.
  3. For a `higher` operand: `value = base × (1 + Σ)`.
  4. For a `lower` operand: `value = base × (1 − Σ)`, with `cap < 1`.
  5. Integer operands round down after scaling.
- **Effects are per holder.** A settlement's equations read the holder that knows for it: its controlling Empire, or
  its own local key when no Empire controls it (`SettlementKnowledge`).
- **Switch.** research.json `tuning.knowledgeEffects` (bool). When it is false, every operand returns its base exactly,
  byte-identical to before. This gives the attribution twin `TestConfigs.PreKnowledgeEffects`.
- **Entity and order unlocks.** These count as real effects only where a live system realizes them today:
  - entity kinds `building`, `infrastructure`, `recipe`, `activity`;
  - `inst.university`;
  - the tax edict (`taxation`);
  - the assay (`ore_prospecting`).

  Units and other institutions are NOT live yet (M7/M8) and do not count as effects.

### 1.1 Operand registry (the only legal operand ids)

The engine track implements every row with a semantic "teeth" test: the operand changes its system's output in a
fixture. Content tracks use only these ids.

| Operand id | Read by | Dir | Cap Σ | Meaning when improved |
|---|---|---|---|---|
| farming.yieldPerArableKm2PerYear | ProductionSystem | higher | 3.0 | land yields more grain |
| farming.outputPerFarmerPerYear | ProductionSystem | higher | 3.0 | each farmer produces more |
| production.outputPerHerderPerYear | ProductionSystem | higher | 2.0 | herds yield more |
| production.outputPerExtractorPerYear | ProductionSystem | higher | 3.0 | mining, logging, quarrying and fishing yield more |
| production.toolYieldBonusMax | ProductionSystem | higher | 1.0 | equipped farmers gain more from tools |
| production.toolWearPerEquippedFarmerPerYear | ProductionSystem | lower | 0.8 | tools last longer |
| consumption.grainSpoilagePerYear | ConsumptionSystem | lower | 0.85 | less grain rots in store |
| consumption.granaryYearsOfDemand | ConsumptionSystem | higher | 1.0 | granaries hold more |
| harvestVariance.sigmaLogYield | HarvestWeatherSystem | lower | 0.6 | steadier harvests |
| granaries.famineReductionMax | GranaryEffects | higher | 1.0 | granaries relieve famine more |
| granaries.farmYieldBonusMax | GranaryEffects | higher | 1.0 | stored seed and fodder raise yields |
| foodState.adaptationAbsorbableShortfall | FoodState | higher | 1.0 | households absorb shortfalls |
| housing.buildLaborAdultYearsPerDwelling | HousingSystem | lower | 0.8 | homes take less labour |
| housing.buildTimberPerDwelling | HousingSystem | lower | 0.7 | homes take less timber |
| housing.upkeepTimberPerDwellingYear | HousingSystem | lower | 0.8 | cheaper upkeep |
| housing.tauYears | HousingSystem | higher | 1.5 | buildings last longer |
| housing.personsPerDwelling | HousingSystem and readers | higher | 0.5 | denser housing |
| pathBuild.laborPerAdultPerYear | PathBuildSystem | higher | 1.0 | more path-building per worker |
| pathBuild.buildCostMultiplier | PathBuildSystem | lower | 0.7 | paths cheaper to build |
| pathBuild.dirtPathSpeedFactor | PathBuildSystem | higher | 0.8 | faster unpaved paths |
| transport.riverCostFactor | transport readers | lower | 0.8 | cheaper river haulage |
| roads.baselineKmPerDay | TransportQuery | higher | 3.0 | faster overland travel |
| roads.maxRouteKm | RoadDevelopmentQuery | higher | 3.0 | longer roads possible |
| catchment.hinterlandRadiusKm | CatchmentSystem | higher | 1.0 | settlements draw on a wider hinterland |
| trade.gapClosingFraction | TradeArbitrageSystem | higher | 1.5 | trade closes price gaps faster (result ≤ 0.95) |
| trade.costPerBulkCostUnit | TradeArbitrageSystem | lower | 0.8 | cheaper trade haulage |
| price.stockReleaseRatePerYear | Price / Trade | higher | 1.0 | stocks reach markets faster |
| demographics.mortalityPerYear | DemographicsSystem | lower | 0.7 | lower baseline mortality (all cohorts) |
| demographics.starvationMortalityMaxPerYear | DemographicsSystem | lower | 0.6 | fewer famine deaths |
| demographics.starvationChildMultiplier | DemographicsSystem | lower | 0.5 | children better protected in famine |
| institutions.universities.maxMortalityReduction | Institutions | higher | 2.0 | university medicine saves more lives |
| institutions.universities.maxResearchCostReduction | Institutions | higher | 0.5 | universities cut research cost more (result < 0.75) |
| institutions.universities.maturityTauYears | Institutions | lower | 0.5 | universities mature faster |
| governance.authorityDecayCostUnits | Governance | higher | 3.0 | administrative reach extends further |
| governance.taxExtractionResponseMax | Governance | higher | 1.5 | taxes extract more output |
| migration.baseRatePerYear | MigrationSystem | higher | 1.0 | freer movement of people |
| migration.dampingDecayCostUnits | MigrationSystem | higher | 2.0 | migrants travel further |
| mobility.promoteRatePerYear | ClassMobilitySystem | higher | 1.5 | faster social promotion |
| mobility.targetShareCap | ClassMobilitySystem | higher | 1.0 | larger specialist classes possible |
| colonization.orderPartyFraction | FoundingQuery | higher | 0.5 | larger founding parties (result ≤ 0.35) |
| fog.exploreStepNodes | DiscoveryQuery | higher | 1.0 | each Explore order reaches further (integer) |
| fog.exploreMaxRadiusNodes | DiscoveryQuery | higher | 1.0 | larger exploration range (integer) |
| fog.formationSightNodes | DiscoveryQuery | higher | 2.0 | formations see further (integer) |
| research.civicsPointsCoefficient | ResearchQuery (CP) | higher | 0.3 | more Civics Points per person |
| research.researchPointsCoefficient | ResearchQuery (RP) | higher | 0.3 | more Research Points per person |
| society.grievanceRelief | NeedsGrievanceSystem | higher | 0.6 | grievance builds more slowly (engine maps to the accumulation term) |
| society.unrestOutputDrag | Governance / Unrest | lower | 0.6 | unrest costs less output |

Design rules for content:

- Use the operand the node causally improves.
- Prefer one or two effects per node, with a typical delta of 0.02–0.08. Larger deltas are reserved for landmark
  nodes (plough, steam, vaccination, …).
- Neighbouring nodes must differ in operand or in role. Never give two nodes in the same lane and Age the same single
  effect.
- A node whose capability is genuinely a live entity unlock may rely on that alone.
- The validator checks: every node has ≥ 1 realized effect or live unlock; operands exist; deltas are in range; and
  ΣDelta per operand is reported against its cap.

## 2. Age sweep rule

- **Rule.** A node of Age a ≥ 3 is ELIGIBLE only if every node of Age a − 2, in both trees, is complete.
- **Applies to** player, AI and city states alike, through the same `ResearchQuery` eligibility.
- **Data.** It reads the node's content Age label (`age`), never the polity's Age and never dates.
- **Switch.** `tuning.ageSweep` (bool). Off gives the twin.
- **Kept apart from prerequisites.** Authored prerequisites stay separate. The sweep is its own predicate, reported
  separately ("blocked by the Age 2 sweep: 7 nodes outstanding"). It never mass-generates prerequisite edges.
- **Repeatables are exempt** from counting toward a sweep cohort, as their own Age's finite set already counts.

## 3. Destination research (routing)

- **Orders.** `SetResearchTarget` (kind 6) may name ANY incomplete, non-repeatable-exhausted node, available or not,
  when `tuning.destinationRouting` is on.
  - The target row is the persistent DESTINATION. No new table is needed, so save compatibility holds.
  - −1 and −2 still clear a lane.
- **Each step, per lane.**
  1. If the destination is available, fund it.
  2. Otherwise compute the ROUTE with `ResearchRoutePlanner`: the minimum-total-remaining-EffectiveCost set of
     incomplete nodes whose completion makes the destination available. The route:
     - respects AND/OR, the stage gate (subtrees), the Age sweep and cross-tree prerequisites;
     - deduplicates shared nodes (the route is a SET);
     - is exact over OR choices up to 4,096 combinations, then greedy by (closure cost, listed position);
     - breaks ties by (cost, key) and then by listed position.
  3. Fund the CURRENT node: the eligible route node in this lane with the smallest (remaining EffectiveCost, key).
     Sweep blockers count as route nodes but are labelled as sweep, never as prerequisites.
- **Cross-lane prerequisites** are blockers of the other lane. They are funded by the other lane only when that lane
  has no destination of its own (free-lane assist).
- **Completion.** When the destination completes, the target row is removed as today.
- **Infeasible destinations.** If a destination becomes infeasible (cannot happen with the shipped graph, but is
  defended against), it is kept, and the query reports the blockers.
- **Shared code.** The query `ResearchQuery.Route(world, content, polity, lane)` returns destination, current, next,
  ordered route, blockers (node, reason ∈ {prerequisite, sweep(Age k), stage, otherLane}) and remaining cost. The UI
  and AI call the same planner, so there is no UI-only calculator.
- **AI.** The AI sets its goal node as the destination and lets the planner pick (the same rules). Its goal choice is
  unchanged otherwise.
- **Switch.** `tuning.destinationRouting` (bool). Off gives the twin: an order naming an unavailable node changes
  nothing.

## 4. Repeatable research (both trees, final Age)

- **Content.** A repeatable node carries `"repeatable": { "levels": true, "costGrowth": g, "effects": [ { "operand":
  …, "gapFraction": k } ] }`.
- **Benefit per level.** Level n adds `cap × k × (1 − k)^(n−1)` to Σ of its operand. This is diminishing and never
  exceeds the cap (DD-14), so there is no runaway.
- **Cost.** Level n costs `BaseCost × g^(n−1)`, with g ∈ [1.10, 1.30].
- **State.** A new table `ResearchLevels(polity, node, level)`, appended to the canonical schema with a version bump.
  Old saves read with no levels.
- **Completion.** Completing a level increments the level and keeps the node available. It never appends a second
  completed row and never re-grants one-time unlocks; repeatables carry operand effects only.
- **Availability.**
  - Technology repeatables keep their ruling-3 subtree-exhaustion rule, or for trunk repeatables "own prerequisites".
  - Civics repeatables need their prerequisites AND every finite civic of Age 9 complete: the end-of-tree safety
    valve.
- **Sweep.** Repeatables are not sweep members.
- **AI.** After its goal closures are exhausted, the AI picks the cheapest next level (composite key).
- **UI.** Shows "researched N×", the current per-level effect and the cumulative effect.

## 5. Pacing ("pincer")

- **Parity.** For a reference empire trajectory (population and dtYears by turn, measured), the turns needed to
  research ALL of Age x in the technology tree and in the civics tree must agree within ±25 turns, for every Age.
- **Speed.** Expansion must not slow progression: the cost of each Age scales so that more nodes means cheaper nodes,
  not longer Ages.
- **Calibration.** Costs are calibrated by the integration step from the rubric magnitudes times a per-(tree, Age)
  scale that is recorded in research.json. This Director pacing ruling supersedes C-23 for tranche 2.

## 6. Content records (new nodes)

- **Id.** snake_case, unique across both trees and against every entity short id.
- **Fields.** name, Age (A1–A9), lane, emerged (with an era-marked year, e.g. "~1,100 CE", "~3,000 BCE"), prereq
  (atoms of either tree, AND/OR/parentheses, no NOT), magnitude factors (rubric), effects (§1), description (≤ 260
  characters).
- **Comparison.** Each record also carries:
  - `civ6` and `civ7`: each an object `{relation, analog}`. relation ∈ {direct, partial, different, none-by-design,
    not-reviewed}; analog is the reference node's name or null.
  - `rationale` for every prerequisite.
- **Chronology.** No must-hold prerequisite may be dated later than the node.
- **Age order.** No prerequisite may come from a later Age.
- **Sweep safety.** No prerequisite may come from two or more Ages later in a way that would deadlock the sweep. The
  integrator's fixpoint check enforces this.

## 7. Node audit record (existing nodes)

One record per node:

- **Identity:** `{id, name, tree, age, lane, cost, prereq, dependents[], prereqAssessment[{atom, verdict:
  keep|replace|remove, why}], andOr: ok|change(new expr), ageGap: note, crossTree: note, leafStatus:
  deliberate-leaf|missing-bridge|n/a}`.
- **Effects:** `{effectsNow: {implemented|deferred|none}, effectsProposed: [...], costConcern}`.
- **Proposed action and analogues:** `{action: keep|add-prereq|remove-prereq|replace-prereq|change-andor|move-age|
  adjust-cost|add-dependency|merge|redesign, newPrereq, newAge, rationale, civ6, civ7}`.

Dependency changes must be justified individually. A crossing of Ages or trees is not by itself a reason.

## 8. Lanes

- **Technology:** the existing domains.
- **Civics:** the 16 strands of D12 and D13 (`civic.strand`). Lanes may be subdivided when warranted, for example
  public-works, labour and justice. Any new lane must be declared with a name and scope.

## 9. Governance

The tranche touches frozen items. CR-024 records them and D-063 rules on them:

- **D-044 R13** (research never reads Age): superseded for the sweep predicate only.
- **D-044 R11** (completion once): extended for repeatable levels, resolving the open item in D-044 Part D T6.
- **Loader text on `effects.immediate`:** unchanged. Realized effects are a separate, ratified kind.
- **Package rule C-23:** superseded by the pacing ruling in §5.
