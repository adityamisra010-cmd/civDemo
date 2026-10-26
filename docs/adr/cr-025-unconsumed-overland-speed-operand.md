# CR-025 — `roads.baselineKmPerDay` has no consuming system: D-063 R1 ("every node a real effect") vs "do not invent gameplay"

**Status: RULED 2026-10-26 — option 1, the §2.2 disposition, subject to a dependency and persistence audit; D-063 R1
amended. Applied on branch `t5-cr025` (§7).** Raised 2026-10-25; the text from here to §6 is the record as raised
(status at raising: "Awaiting the Director's ruling. NO content, registry or engine change made."). Format per
CLAUDE.md "Governance": (1) items in conflict, (2) evidence, (3) ≤3 minimal fix options, (4) blast radius,
(5) recommendation. Tree: branch `t4-roads`, cut from `2ca21a9`. This record adds evidence pins only (§2.3), and they
change no simulation outcome.

**The defect.** Seventeen shipped nodes declare the realized operand `roads.baselineKmPerDay` ("faster overland
travel"). For 14 of them it is the only declared effect. No simulation system reads the operand, so completing any of
these nodes changes nothing in play. The tranche-2 effect-coverage report counts all 17 as realized effects. In-game,
the research panel labels each node "simulated" and shows "+x %: faster overland travel — read by TransportQuery"
(`Sim.Core/State/InfoQuery.cs:427-433`).

**The Director's instruction for the fix** (2026-10-25): "Either wire the setting into the correct supported system
or, if that behavior is not yet implemented, document and correct the misleading effect without inventing unrelated
gameplay. Do not lower these nodes' costs as a substitute for fixing them."

## 1. Items in conflict

- **A. D-063 R1** (`docs/d063-tranche2-director-direction.md`, RULED): "Every Technology and Civics node produces a
  real gameplay effect … No text-only nodes." Enforced by `Tranche2ContentTests.EveryNode_HasARealEffect`: each node
  needs a realized operand effect, a live-prefixed entity unlock, a level rule, or one of the two order gates.
- **B. ADR-047 §2.2 and tranche-2 contract §1.1**: "Every operand is a parameter of a live equation in its owning
  system." The contract row `roads.baselineKmPerDay | TransportQuery` breaks this. The equation it scales,
  `TransitDays = CostKm / baselineKmPerDay`, feeds no system. ADR-047 §5 already says so: "no system consumes freight
  time yet (ADR-032 §7)". So does `docs/queue.md` (the 2026-10-24 ADR-047 entry, item 3).
- **C. ADR-032 §7**: the consumer of freight time (trade contracts with delivery delay, a landed-price carriage term,
  route tonnage caps) is "recorded as design intent, not ratified mechanics". CLAUDE.md also applies: "Never implement
  ahead of the ratified spec". M6 is not ratified.
- **D. The Director's fix instruction** (above): wire the operand only into a system that already supports it,
  otherwise correct the claim without inventing gameplay, and do not touch costs.

No live system supports the operand's meaning (§2.1), so the D-compliant fix is the brief's option B. Option B has two
parts. Nodes whose own meaning is an existing live operand's meaning are retargeted to that operand. Every other node
is declared future-dependent. For six of the 14 nodes no live operand matches the node's meaning (§2.2). Declared
future-dependent, those six would carry no live effect, which A forbids. Keeping A means one of two things. Either
those nodes get an operand whose meaning is not theirs, which repeats this defect in another form and breaks D. Or a
travel-time consumer is invented, which breaks C and D.

## 2. Evidence

### 2.1 Every system that models overland movement, travel cost or distance (tree `2ca21a9`)

| System / read site | What it consumes | A speed (km/day) or a time? | Could `roads.baselineKmPerDay` be read here with its meaning, without new gameplay? |
|---|---|---|---|
| TransportQuery.EstimateFreight (`Sim.Core/State/TransportQuery.cs:205-213`, `:283`) | class-weighted km ÷ km/day = transit days | **yes, the only one** | Already reads it. **No caller** in Sim.Core, Sim.Cli or Sim.Ui (measured, §2.3). |
| TradeArbitrageSystem (`Sim.Core/Systems/Trade/TradeArbitrageSystem.cs:31-34`, `:152`, `:168`) | pair travel cost (cost units) × bulk × `trade.costPerBulkCostUnit` = carriage threshold | **no**: "INSTANTANEOUS within the turn", no delivery time | No. A transit-time term is ADR-032 §7's unratified design intent (C). |
| CatchmentSystem (`Sim.Core/Systems/Catchment/CatchmentSystem.cs:53-54`, `:84-85`) | hinterland radius km → Dijkstra cost budget | no: a distance budget ("the country whose produce can profitably flow", sim.json `catchment._doc`) | No. Turning it into a time budget is a new mechanic and would double-count `catchment.hinterlandRadiusKm`. |
| MigrationSystem (`Sim.Core/Systems/Migration/MigrationSystem.cs:304`, `:422`) | `exp(−TravelCost / dampingDecayCostUnits)` | no: travel-effort damping | No. Already carries `migration.dampingDecayCostUnits`. |
| Governance.AdministrativeReach (`Sim.Core/State/Governance.cs:105-115`) | `exp(−TravelCost / authorityDecayCostUnits)` | no | No. Already carries `governance.authorityDecayCostUnits`. |
| Institutions (university health reach) (`Sim.Core/State/Institutions.cs:290-296`) | travel cost / `healthDecayCostUnits` | no | No. |
| HarvestWeatherSystem spatial correlation (`Sim.Core/Systems/Harvest/HarvestWeatherSystem.cs:101`, `:109`) | travel-cost or geographic kernel | no | No (weather, not travel). |
| Pathfinder road overlay (`Sim.Core/Pathing/Pathfinder.cs:635`; `Sim.Core/State/RoadPerformance.cs:28`, `:75-78`) | relative class speed factors (cost per km as a fraction of the baseline) | relative speed only, dimensionless | No. The absolute km/day never enters a cost. Built-road speed is already live through `roads.classes[].speedFactor`, unlocked by the `infra.road_*` entities. |
| RoadDevelopmentQuery / RoadDevelopmentSystem (`Sim.Core/State/RoadDevelopmentQuery.cs:162`, `:192`; `Sim.Core/Systems/Roads/RoadDevelopmentSystem.cs:161`) | route length km ≤ `roads.maxRouteKm`; class speed factor | no | No. |
| PathBuildSystem (`Sim.Core/Systems/PathBuild/PathBuildSystem.cs:188`, `:251-260`) | labour accrual; NEW dirt-edge cost factor ÷ (1 + Σ `pathBuild.dirtPathSpeedFactor`) | a relative path cost factor, stored at build time | No. That is path engineering on edges laid after the completion, not traveller speed. |
| Colonization / FoundingQuery (`Sim.Core/State/FoundingQuery.cs:70-71`; `Sim.Core/Systems/Colonization/ColonizationSystem.cs:324`) | binary reachability, "no travel budget"; the colony exists next turn | no | No. |
| DiscoveryQuery / DiscoverySystem Explore (`Sim.Core/State/DiscoveryQuery.cs:254-255`, `:304`) | radius in lattice nodes per order | no | No. Already carries `fog.explore*`. |
| Formations (`Sim.Core/State/AvailableActionsQuery.cs:607`; `Sim.Core/State/InfoQuery.cs:1348`; `Sim.Core/Worldgen/WorldFounding.cs:290`) | stationary at capitals; "movement … not yet simulated" (M7) | no | No (M7 Battle Layer). |

**Conclusion.** No live system computes overland travel time or uses an absolute speed. Wiring the operand would mean
building ADR-032 §7's consumer, which is new gameplay ahead of a ratified spec.

### 2.2 Per-node disposition, if every node is treated honestly

Σ figures are measured on the shipped content: catchment.hinterlandRadiusKm 0.61 / cap 1.0; roads.maxRouteKm
0.29 / 3.0; governance.authorityDecayCostUnits 2.49 / 3.0; trade.costPerBulkCostUnit 0.719 / 0.8 (already at the 90 %
saturation line, so adding to it would rescale 41 nodes). Delta values and costs stay as authored.

| Node (Age, delta) | Honest disposition | Fit | One-line rationale |
|---|---|---|---|
| horse_domestication (A3, 0.05) | → `catchment.hinterlandRadiusKm` | strong | Pack and draught horses widen the country whose produce can profitably reach a settlement (the catchment's stated meaning). |
| wheel_spoked (A3, 0.04) | **future**: M7 Battle Layer (chariot) / the ADR-032 §7 freight consumer | none live | Its historical consequence is the light chariot. Bulk carriage kept solid wheels, so no live haulage or reach operand fits. |
| saddle (A4, 0.02) | **future**: M7 Battle Layer (mounted movement, cavalry) | none live (`fog.exploreStepNodes` is a weak proxy) | Rider stability for cavalry and long rides. Formations do not move before M7. |
| bridge_stone (A5, 0.04) | → `roads.maxRouteKm` | moderate | Permanent multi-span crossings of major rivers make longer inter-city routes buildable. |
| horseshoe (A6, 0.03) | → `catchment.hinterlandRadiusKm` | strong | Shod horses sustain haulage on hard and wet roads. Medieval horse haulage widened market hinterlands. |
| internal_combustion (A8, 0.04) | → `catchment.hinterlandRadiusKm` | strong | Motor lorries extend the profitable hinterland. |
| aluminium (A9, 0.03) | **future**: no consumer; aircraft is M7 (unit.*) | none live | Cheap light metal (aircraft, transmission lines). No live equation is about light metal. |
| sprung_coach (A7, 0.05) | → `governance.authorityDecayCostUnits` | moderate–strong | Timetabled mail and passenger coaches carry officials and post, so administrative reach extends (Σ → 2.54). |
| integrated_alliance_command (A9 civic, 0.03) | **future**: M8 Politics / Diplomacy (alliances), M7 (joint command of formations) | none live | A peacetime joint military command of allies. It has nothing to do with travel speed. |
| road_houses (A3 civic, 0.03) | → `governance.authorityDecayCostUnits` | strong | Staffed stations a day's march apart for official travellers (Achaemenid road stations, Inca tambo) extend administrative reach. |
| pilgrimage (A4 civic, 0.02) | **future**: M9 Society | none live | Pilgrim travel under a sacred truce. It is not migration, trade or administration. |
| bridge_trusts (A6 civic, 0.03) | → `roads.maxRouteKm` | moderate | Endowed upkeep keeps long routes' river crossings standing, so longer routes stay viable. |
| turnpike_trusts (A7 civic, 0.04) | → `roads.maxRouteKm` | moderate | Toll finance resurfaced and extended the trunk-road network. Its cheaper-carriage effect would land on the saturated `trade.costPerBulkCostUnit`. |
| road_traffic_regulation (A9 civic, 0.02) | **future**: the ADR-032 §7 freight-time/throughput consumer | none live | Safe, regulated motor-traffic flow is about road speed and throughput. Nothing live reads either. |
| *road_paved* (A5; also governance 0.04 and live `infra.road_paved`) | drop the dead delta | — | The paved class's speed is already live through its `speedFactor`, so the operand double-claims it. |
| *wheellock* (A7; also unrest 0.0042) | drop the dead delta | — | Mounted firearms are not overland travel. |
| *motor_road* (A9; live `infra.road_highway`) | drop the dead delta | — | The highway class's speed is already live through its `speedFactor`. |

So **8 retargets, 6 future-dependent, 3 drops**. The 6 future nodes have no entity unlock, so
`EveryNode_HasARealEffect` would fail on each of them. A related, already-shipped gap: the test counts any `infra.`
or `building.` unlock as live, including placeholders. The live map
(`research-cost-pass/data/live-map.md`) lists about 16 more nodes, among them horse_domestication's
`infra.courier_relay` and bridge_stone's `infra.bridge`, that satisfy the test without a live effect.

### 2.3 Measured (this branch; the pins are committed)

- `UnconsumedOperandTests.TheSpeedOperandsOnlyReadSite_HasNoCallerOutsideTests`: `KnowledgeOperand.RoadsBaselineKmPerDay`
  is referenced only in `Sim.Core/State/TransportQuery.cs`. No file in Sim.Core, Sim.Cli or Sim.Ui outside
  TransportQuery.cs contains `EstimateFreight(`.
- `UnconsumedOperandTests.StrippingTheSpeedOperand_ChangesNoWorld_WhenEveryDeclaringNodeIsComplete`: the canonical
  founded world is seed 42 with one AI. Every holder is granted all 17 declaring nodes plus one control node carrying
  `demographics.mortalityPerYear`, then the AI runs 40 turns. Removing every `roads.baselineKmPerDay` declaration
  gives the **same world hash and state**. Removing every `demographics.mortalityPerYear` declaration gives a
  **different** hash, so the method has teeth. The 3 drops in §2.2 are therefore outcome-neutral.
- On the shipped content, the canonical 300-turn AI run (seed 42, one AI) completes **none** of the 17 nodes. This was
  measured: the first version of the pin, which relied on the AI to complete them, failed its non-vacuity guard. So
  retargets are unlikely to move the canonical 300-turn pins, but each pin must be measured when the change is applied.
- `KnowledgeEffectsTeethTests` row `roads.baselineKmPerDay` passes because it measures the query itself
  (`EstimateFreight(...).TransitDays`). That is teeth without aim (ADR-015 §7.2), and it is how the defect passed the
  ADR-047 gate. The row now carries a comment saying so.
- `Tranche2ContentTests.NoNewNode_DeclaresTheUnconsumedSpeedOperand` freezes the 17 declarations until the ruling.

## 3. Options

1. **Honest split, with a dated amendment to R1 (recommended).**
   - Apply §2.2: 8 retargets, 3 drops, and 6 nodes declared future-dependent. Future-dependent means a content field,
     for example `effects.future: [{ "operand"|"system": …, "milestone": "M7" }]`. The loader validates it. No system
     reads it, it is never counted as realized, and the research panel shows "arrives with M…" instead of "simulated".
   - Mark the registry row `roads.baselineKmPerDay` as having no consumer (reader "none: ADR-032 §7 hook"). The loader
     then refuses a REALIZED delta on a no-consumer operand. That is the recurrence guard.
   - The coverage report counts the 6 separately, and R1 reads "every node a real effect, or a declared future effect
     naming its milestone".
   - Costs are unchanged.
2. **Keep R1 literally.** Retarget all 14, giving the 6 the nearest live operand even where the meaning differs: saddle
   → `fog.exploreStepNodes`, integrated_alliance_command → `fog.formationSightNodes`, pilgrimage →
   `migration.baseRatePerYear`, wheel_spoked, aluminium and road_traffic_regulation → `catchment.hinterlandRadiusKm`.
   **Not recommended.** It replaces a dead effect with a mislabelled one, which is the same defect in another form, and
   it is contrary to D.
3. **Ratify a travel-time consumer first.** Write ADR-032 §7 (trade contracts with delivery delay, a landed-price
   carriage term) into a ratified spec, build it, and keep the 17 declarations. **Not now.** It is new gameplay that no
   ratified spec covers, every golden that runs trade moves, and the misleading claim stands until it lands. Option 1's
   registry and UI honesty would still be needed in the meantime.

## 4. Blast radius

- **Option 1.**
  - research.json: 17 nodes' `effects` change; no costs change.
  - Loader and schema: one optional `effects.future` field, plus refusing realized deltas on a no-consumer operand.
    This needs an ADR-047 amendment.
  - KnowledgeOperands: one new flag, or a reader string.
  - InfoQuery: one display branch.
  - Tranche2ContentTests: R1 and the freeze test.
  - Coverage report and live map.
  - Goldens: the 3 drops are measured outcome-neutral. The 8 retargets change gameplay only for holders that complete
    those nodes. The canonical AI does not reach them in 300 turns, but every golden must be re-measured and any moved
    pin attributed. The attribution control is a twin with only the retargets reverted.
- **Option 2.** Content only. Same golden profile as option 1.
- **Option 3.** A new mechanic in TradeArbitrageSystem (and/or a contract table, which means a schema bump). Every
  trade-bearing golden moves.

## 5. Recommendation

Option 1. Until the ruling: the operand stays declared and the 17 declarations are frozen. The coverage report carries
a dated correction (2026-10-25 note in `tranche2-effect-coverage.md`).

## 6. Interim display correction (2026-10-25, commit 1ad60d3)

The misleading player-facing claim is corrected without touching content, as the Director's instruction allows:
- The operand registry now carries `NotConsumed` for `roads.baselineKmPerDay`, so `ConsumedInPlay` is false.
- The research card lists the effect as "(not simulated yet)", gives this CR as the reason, and no longer counts it as a
  simulated effect.
- The 14 nodes whose only effect it is now read research-only or knowledge-only, which is the truth today.

Content, loader, caps and every system are unchanged. The per-node disposition in §2.2 and the R1 question still await
the ruling.

## 7. Ruling and application (2026-10-26)

**The Director's ruling** (2026-10-26, essentials verbatim): "Approve the proposed disposition of all 17 nodes, subject to a
complete dependency and persistence audit: retarget the eight nodes to real, implemented effects; mark the six nodes as
explicitly future-dependent, without claiming their effects are already simulated; drop the three nodes only after
confirming they are not required by prerequisites, saves, migrations, tests or external content references. Approve the
D-063 R1 amendment so future-dependent content is permitted when clearly identified and documented. Do not weaken the
invariant that every currently implemented effect must be real and testable." Recorded as D-063 Amendment 1
(`docs/d063-tranche2-director-direction.md`) and the ADR-047 amendment note.

**"Drop the three"** is applied as §2.2 proposes it: the dead `roads.baselineKmPerDay` ENTRY is dropped from road_paved,
wheellock and motor_road. The nodes themselves stay. Each keeps its other effects and its live unlock. The audit below
covers all 17 nodes anyway.

### 7.1 Applied content (research.json; no cost, costRationale, prerequisite, Age or key changed)

| Node (key) | Before | After |
|---|---|---|
| horse_domestication (96) | roads 0.05 | `catchment.hinterlandRadiusKm` 0.05 |
| horseshoe (177) | roads 0.03 | `catchment.hinterlandRadiusKm` 0.03 |
| internal_combustion (291) | roads 0.04 | `catchment.hinterlandRadiusKm` 0.04 |
| bridge_stone (151) | roads 0.04 | `roads.maxRouteKm` 0.04 |
| bridge_trusts (1285) | roads 0.03 | `roads.maxRouteKm` 0.03 |
| turnpike_trusts (1308) | roads 0.04 | `roads.maxRouteKm` 0.04 |
| sprung_coach (516) | roads 0.05 | `governance.authorityDecayCostUnits` 0.05 |
| road_houses (1242) | roads 0.03 | `governance.authorityDecayCostUnits` 0.03 |
| wheel_spoked (99) | roads 0.04 | no realized effect; future: M7 Battle Layer (chariots); unscheduled freight-time consumer (operand `roads.baselineKmPerDay`) |
| saddle (123) | roads 0.02 | no realized effect; future: M7 Battle Layer (mounted movement) |
| aluminium (306) | roads 0.03 | no realized effect; future: M7 Battle Layer (aircraft) |
| integrated_alliance_command (1178) | roads 0.03 | no realized effect; future: M8 Politics / Diplomacy (alliances); M7 Battle Layer (joint command) |
| pilgrimage (1248) | roads 0.02 | no realized effect; future: M9 Society (religion and pilgrimage) |
| road_traffic_regulation (1451) | roads 0.02 | no realized effect; future: unscheduled freight-time consumer (operand `roads.baselineKmPerDay`) |
| road_paved (150) | governance 0.04 + roads 0.05 | governance 0.04 (live `infra.road_paved` unchanged) |
| wheellock (227) | unrest 0.0042 + roads 0.01 | unrest 0.0042 |
| motor_road (425) | roads 0.06 | no realized effect (live `infra.road_highway` unchanged) |

Σ against the caps after the change: catchment.hinterlandRadiusKm 0.73 / 1.0, roads.maxRouteKm 0.40 / 3.0,
governance.authorityDecayCostUnits 2.57 / 3.0. All three are below the 90 % saturation line, so no other node's delta is
rescaled. `Tranche2ContentTests.NoOperand_SumsAboveItsCap` passes. "Unscheduled" is used where the realizing system is
ADR-032 §7's freight-time consumer, which is recorded design intent with no milestone. It names no milestone rather than
inventing one.

### 7.2 Dependency and persistence audit (all 17 nodes; this tree)

No node is deleted, renamed or re-keyed. Only `effects` changed. So every reference by id or key still resolves.

| Concern | What was checked | Finding | Blocker? |
|---|---|---|---|
| Prerequisites / dependents | Every `prereq` expression and entity `requires` in research.json; `researchStage.requires` | Unchanged. The 17 nodes still gate 24 dependents (horse_domestication 5, internal_combustion 6, aluminium 3, wheel_spoked 2, road_paved 2, one each for saddle, bridge_stone, horseshoe, turnpike_trusts, wheellock and motor_road; the other six none). Entity unlocks are unchanged (`infra.courier_relay`, `infra.bridge`, `infra.road_paved`, `infra.road_highway`). None is a stage atom. `AgeSweep_ReachesEveryNode_FromScratch` passes. | No |
| Saves (completed / progress / level / target rows) | `ResearchCompletedRow`, `ResearchProgressRow`, `ResearchLevelRow` and targets key by `ResearchNodeId` (the content key); `Snapshot` / `CanonicalSchema` | Keys unchanged, so every save loads. The save format carries no research-content fingerprint (only the terrain hash), and none of the 17 is repeatable. One consequence on load: a holder that has completed a node retargeted to `catchment.hinterlandRadiusKm` gets a different `CatchmentSystem.Revision` (it counts such completions), so its catchments recompute once at the next step. That recompute is correct under the new content. | No |
| Schema / migrations | CanonicalSchema version (36), WorldState tables | No state, table, order or row type changed. No schema bump and no migration. The `effects.future` field is content only: the research loader reads it and nothing serializes it. | No |
| Replays / goldens | Old order logs replayed on the new content | A replay reproduces as long as the run never completes a retargeted node. A run that does complete one diverges from that turn on. That is the intended gameplay change, attributed in §7.4. | No (attributed) |
| Tests | Every Sim.Tests / Sim.Ui.Tests reference to the 17 ids or the operand | Updated in this change: the freeze test, the Saddle card test, `UnconsumedOperandTests`, the teeth row, and the probe/early rigs (the loader now refuses a realized delta on the operand). Other references use ids or keys only, for road classes or milestones: `RoadDevelopmentTests`, `ResearchContentTests`, `WorldMapStateTests`, `TaxationGateTraceMeasurement`, `IntegratedPinAttributionTests`. They are unaffected. | No |
| External content | ages.json, sim.json, goods.json, unit-families.json, knowledge-texts.json, research-pre-tranche2.json, the test fixture research-pre-adr045.json, government-packages.json | ages.json milestones `a5_roads` (road_paved) and `a9_combustion` (internal_combustion) name node ids, which are unchanged. sim.json names only `infra.road_paved` (a road class entity). knowledge-texts.json carries history texts that make no effect claim. The pre-tranche files carry no realized effects. Nothing else names the 17. | No |
| Tools, UI, CI | scripts/*.py, docs tools, Sim.Ui, .github/workflows | The three research `--check` scripts went stale on the content hash only and were regenerated. `tools/tranche2_reports.py` had a registry regex that silently dropped the `NotConsumed` row; it is fixed and the report regenerated (§7.5). Sim.Ui renders the card's lines and has no node-specific code. CI pins no research.json hash. | No |

**No blocker was found.**

### 7.3 R1 under actual liveness: the known gaps

The pre-ruling R1 test counted any `building.` or `infra.` unlock as live, placeholders included. The test now judges
liveness by the research card's own verdict (InfoQuery, which `InfoQueryTests.EveryNodesEffect_IsKnowledgeOnly_…` pins
against the systems), plus the two sim.json order gates. Judged that way, **17 nodes outside this CR** have neither a
real effect nor a future declaration. Each unlocks only placeholder entities:

kiln_updraft, oil_press, papyrus, plank_boat, fulling_mill, sulphuric_acid, telephone, petroleum_refining,
integrated_circuit, nuclear_power, solar_pv, wind_turbine_modern, cloud_computing, desalination, fusion_power,
trace_italienne, rocket.

The ruling does not cover their content, and it is unchanged. They are pinned as an exact-match list
(`Tranche2ContentTests.KnownGapsWithoutARealEffect`), so the test stays honest. A node leaves the list only when it gains a
real effect or a future declaration, and no node can join it silently. The independently derived coverage report
(§7.5) finds the same 17. They await a content decision: retarget, declare future, or accept.

Related findings, not fixed:
- The card does not count the assay order gate (`discovery.assayRequires`), so `ore_prospecting`'s card under-claims; the
  node is live. Recorded in queue.md.
- The card counts the label-only sector activities (herding, logging, mining) as realized. No node depends on them
  alone: cattle, sheep_goat, ground_stone_early and mining_shaft each also carry a realized effect.

### 7.4 Gameplay attribution (moved pins)

**No pin moved.** Measured on this tree, with the content and loader commit and the test commit applied: the full
`Sim.Tests` Release suite was run in two halves. The `Sim.Tests.Kernel` half gave 528 passed, 0 failed, 17 skipped; the
other half gave 2425 passed, 0 failed, 14 skipped. Every skip is a pre-existing `[Fact(Skip)]` measurement harness. Those
runs include every golden, attribution and pin test, `CiPinAgreementTests` (ci.yml `FOUNDED_GOLDEN`) and
`AllocationCiLegTests` (the ci.yml allocation log hashes, run `--without-tranche2`). Every hard-coded hash and turn pin
holds at its pre-ruling value.

Why no pin moved:
- The AI's research choice reads costs and prerequisites, never effects, and no cost changed. So a pinned run diverges
  only once some holder completes one of the 8 retargeted nodes.
- The 3 dropped deltas and the 6 removed ones sat on an operand nothing consumes. CR-025 §2.3 measured them
  outcome-neutral.
- No pinned run completes a retargeted node within its horizon: CR-025 §2.3 measured that the canonical 300-turn AI run
  completes none of the 17. The full suite above is the measurement for every other pin.

The retargets are not vacuous: `FutureEffectsTests.InPlay_TheRetargetsChangeTheWorld_AndTheFutureDeclarationsDoNot`
grants every holder of the canonical world (seed 42, one AI) the 8 retargeted and the 6 future nodes and runs the AI
for 40 turns. Removing the future declarations gives the identical hash. Removing the 8 retargeted effects gives a
different hash. That third content, the shipped content without the 8 retargeted effects, is the attribution twin for
any pin the retargets move later. It reproduces pre-ruling behaviour because the pre-ruling deltas were inert.

Note for integration: schema v37 lands on a parallel branch, and its own re-pins are independent of this change.

### 7.5 Enforcement (this tree)

- Loader recurrence guard: `UnconsumedOperandTests.TheLoader_RefusesARealizedDelta_OnAnOperandNotConsumedInPlay` (3 rows)
  and `…ALevelEffect…`. Mutant "guard off": the 3 rows fail.
- Future schema: `FutureEffectsTests` covers milestone, system and description, the operand (unregistered or consumed),
  a delta member, a missing operand member, and the same system twice. It also shows a future effect enters no index,
  no Σ and no snapshot.
- Display: `InfoQueryTests.Saddle_ItsOnlyEffectIsAFutureOne_…` and `EveryFutureEffect_IsOnTheCard_AsNotSimulated_…`.
  Mutant "the card counts future effects as simulated": both fail, and so does `EveryNodesEffect_IsKnowledgeOnly_…`.
- Retargets reach their live readers at the shipped delta:
  `KnowledgeEffectsTeethTests.ACr025RetargetedNode_MovesItsOperandsLiveReader_AtItsShippedDelta`, one row per node:
  CatchmentSystem's catchment, RoadDevelopmentQuery.Routes, Governance.AdministrativeReach.
- R1 and the disposition: `Tranche2ContentTests.EveryNode_HasARealEffect_OrADeclaredFutureEffect` and
  `TheCr025Disposition_IsApplied_AndNoNodeDeclaresTheUnconsumedSpeedOperand`.
- In play: `FutureEffectsTests.InPlay_TheRetargetsChangeTheWorld_AndTheFutureDeclarationsDoNot` (§7.4).
- Reports: `tranche2-effect-coverage.md` is regenerated, with its generator fixed to keep the `NotConsumed` registry row,
  count only realized effects on consumed operands, judge unlocks by actual liveness, and list future declarations
  separately. It reports 972 nodes with a realized effect on a consumed operand, 6 future-only nodes, and 17 without
  either. That supersedes the 2026-10-25 hand note. `research-cost-pass/data/live-map.*` carry a dated correction.
