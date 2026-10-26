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

## 8. The 17 known gaps — audit and disposition (2026-10-26)

**The Director's ruling** (2026-10-26, verbatim): "Audit all 17 nodes against current simulation behavior, planned M7/M8
mechanics, prerequisites, save compatibility and content references. For each, propose a real implementation, an
explicit future dependency, or retargeting/removal. Do not blanket-classify them. Preserve the strict loader guard and
regression tests. Future-dependent effects must not be represented as active gameplay."

Tree: branch `t6-gaps`, cut from `06f127a`. The §7.3 list is the subject. **Applied:** only the gameplay-inert
disposition (b), an `effects.future` declaration, on all 17 nodes, each with its own milestone and system. **Proposed,
not applied:** (a) a live realized effect for papyrus and telephone (§8.3), because a realized effect changes the default
game while pacing is being recalibrated. **(c) removal:** proposed for none.

### 8.1 Evidence common to every row

- **Why each unlock is a placeholder (current simulation).** A research entity is live only if a system builds, founds
  or crafts it, or reads its eligibility. The research card applies that rule (InfoQuery.Consumers), and so does the
  report generator (`tools/tranche2_reports.py` `live_entities`). The sources are goods.json construction projects
  (only `building.granary`, `building.workshop` and `building.university`), goods.json recipes, the research sector
  activities, sim.json road classes (only `infra.road_*`) and the trade entity. `unit.*` gates nothing:
  unit-families.json says so, formations are founded at worldgen and modernized by AgeTransitionSystem, and recruitment
  is M7. No goods.json project names a `project.*` entity. None of the 17 nodes has any other effect: no realized
  effect, no level, no Age-milestone fact, no stage atom, no tax or assay gate. On this tree the card's verdict for each
  of them is research-only (dependents > 0) or knowledge-only (none), never Simulated. That verdict is pinned before and
  after this change (`InfoQueryTests.TheGapAuditNodes_…`).
- **Save compatibility (the same for every row, so it is not repeated in the table).** Saves key completed, progress,
  level and target rows by `ResearchNodeId` (the content key). Every key is unchanged, and the test pins all 17. The
  change adds a content-only `effects.future` list. The loader reads it, the research card displays it, and nothing
  serializes it. The save format carries no research-content fingerprint (Snapshot carries only the terrain hash).
  The schema stays at version 37 (no table, row or order change, and no migration). None of the 17 is repeatable.
  Old saves and order logs load and replay unchanged (§8.4).
- **Content references outside research.json** (grep over the tree, all 17 ids). ages.json, sim.json, goods.json,
  Sim.Ui and `.github/workflows` name none of them. knowledge-texts.json holds one display-only history text per node
  ("Not read by the simulation", its `_doc`); none claims an effect. unit-families.json `ballistic_missiles` (key 1201)
  is `realizedBy` `unit.missile`, rocket's unit. Three tests use an id as a fixture or label only, and are unaffected:
  `ConstructionAvailabilityTests` (a test-only kiln project gated by kiln_updraft), `RoadDevelopmentTests`
  (petroleum_refining in motor_road's prerequisite) and `ResearchContentTests` (nuclear_power's display name).
- **Where the planned realizations sit.** The source is `docs/deferred-entity-realization-plan.md` (PLAN R2c; tier
  membership "INFERRED and OPEN to Director revision"). Tier E (military) is owned by **M7 Battle Layer**: §E, M7
  handoff register H7-03 and H7-08, and H7-07 for siege. Tiers A–D and G have **no milestone in any ratified record**.
  The draft M6 spec (T6.11, "realization of tiers A–D, G") is NOT ratified. The deferred-work register DW-011 reads
  "M6 / M7 / M11+ per tier" with a Director decision still owed. The loader admits M7–M11 or `unscheduled`, and M6
  cannot be named because M6 ships this content. So those rows say `unscheduled` and cite their tier. The rows invent
  no milestone. If T6.11 is ratified and realizes an entity, that node gains a realized effect or a live unlock, and
  its future line is removed at that time.
- **Operand room for any (a).** Σ over all nodes against the cap, measured on this tree, with the 90 % saturation line
  of `tools/import_tranche2.py`: above that line one more delta means rescaling every node on the operand. Measured:
  - governance.authorityDecayCostUnits 2.570 / 3.0 (85.7 %);
  - foodState.adaptationAbsorbableShortfall 0.900 / 1.0 (90.0 %);
  - trade.gapClosingFraction 1.349 / 1.5 (90.0 %);
  - trade.costPerBulkCostUnit 0.719 / 0.8 (89.9 %);
  - research.researchPointsCoefficient 0.277 / 0.3 (92.2 %);
  - institutions.universities.maxResearchCostReduction 0.451 / 0.5 (90.1 %);
  - consumption.grainSpoilagePerYear 0.510 / 0.85 (60.0 %);
  - colonization.orderPartyFraction 0.350 / 0.5 (70.0 %);
  - transport.riverCostFactor 0.360 / 0.8 (45.0 %).

### 8.2 Per-node audit

"Deps" counts the nodes whose prerequisite names the node (the entity `requires` that names it is the unlock). Removing
a node with Deps > 0 would break those prerequisite expressions; removal is also not needed anywhere, because every
node keeps a historical meaning and a prerequisite role. The Age sweep cannot deadlock on any of them
(`AgeSweep_ReachesEveryNode_FromScratch` passes).

| Node (key, Age) | Unlock — why it is a placeholder | Deps / other references | Planned realization (doc) | (a) considered | Recommended | Applied? |
|---|---|---|---|---|---|---|
| kiln_updraft (48, A2) | `building.kiln`. No construction project. Pottery firing is live but gated by pottery_open_fired, and no recipe has a capacity or fuel term a kiln could feed. | 8 (copper_smelting, fired_brick, glass_glaze, iron_bloomery, bellows, wootz, movable_type_ceramic, portland_cement); `ConstructionAvailabilityTests` fixture | Tier A ("kiln as the capacity/fuel-efficiency term of the existing firing recipe"; needs a per-building capacity mechanism packet). Unscheduled. | `consumption.grainSpoilagePerYear` (high-fired, less porous jars): weak. pottery_open_fired (0.04) and glass_glaze (0.02) already claim storage jars, and the kiln's own consequence is firing capacity, which no operand models. Rejected. | (b) unscheduled: Construction - kiln (tier A) | yes (b) |
| oil_press (57, A2) | `building.oil_press`. No project, and no oil good exists (goods.json has 14 goods, none an oil). | 1 (olive_press_beam) | Tier D (production chains). Unscheduled. | `foodState.adaptationAbsorbableShortfall` ("storable calories" as a household buffer): moderate meaning, but the operand sits at the 90 % line (0.900 / 1.0), so adding to it rescales 33 nodes. No other operand is about oil. Rejected for now. | (b) unscheduled: Production chains - oil press (tier D) | yes (b) |
| papyrus (90, A3) | `building.library`. No project. inst.library, not building.library, is the university chain atom. | 0 node deps; building.library `requires` "cuneiform OR hieroglyphic OR chinese_script OR papyrus OR paper OR mesoamerican_script" | Tier B (library, research stage). Unscheduled. | **`governance.authorityDecayCostUnits` +0.04: strong.** It is a light, portable writing surface for administration at distance (the Egyptian bureaucracy). Its writing-surface family carries the same operand at the same Age-scaled delta: cuneiform 0.04 (A3), paper 0.04 (A5). Room 85.7 % → 87.0 %. | (b) now; (a) PROPOSED as an additional live effect (§8.3) | (b) yes; (a) no |
| plank_boat (95, A3) | `building.harbour`. No project. `activity.coastal_shipping` and `activity.ocean_shipping` have no consumer. | 4 (naval_ram, mortise_hull, stern_rudder, cog); building.harbour `requires` plank_boat AND stone_dry | Tier B (harbour, trade). Unscheduled. | `trade.costPerBulkCostUnit` (its family's operand, mortise_hull 0.0112) and `trade.gapClosingFraction`: both at the 90 % line. `transport.riverCostFactor` has room, but its only reader is the path builder's segment lattice (live-map item 3), not haulage. `colonization.orderPartyFraction`: founding is overland reachability, not by sea. Rejected. | (b) unscheduled: Construction - harbour (tier B) | yes (b) |
| fulling_mill (182, A6) | `building.fulling_mill`. No project. Cloth comes from the weaving recipe, which has no finishing or capacity term. | 0 | Tier D. Unscheduled. | None: no live operand concerns textile finishing or water power. `production.outputPerExtractorPerYear` covers extractors, not cloth. | (b) unscheduled: Production chains - fulling mill (tier D) | yes (b) |
| sulphuric_acid (275, A8) | `building.chemical_works`. No project and no chemical good. | 5 (soda_leblanc, bleach_chlorine, dynamite, chemical_fertilizer, uranium_milling) | Tier D. Unscheduled. | None genuine. Its consequences (fertiliser +0.15 farming yield, dynamite's extraction and path cost, bleach, soda) are already realized on its dependents, so an effect of its own would double-count them. | (b) unscheduled: Production chains - chemical works (tier D) | yes (b) |
| telephone (290, A8) | `infra.telephone_network`. The only live `infra.` ids are the road classes. | 3 (internet, automatic_switching, universal_telephone_service) | Tier C (infrastructure). Unscheduled. | **`governance.authorityDecayCostUnits` +0.03: moderate.** Real-time voice between a capital and provincial offices extends administrative reach (Governance.AdministrativeReach's e-fold). Comparable: optical_telegraph 0.05 (A7), vacuum_tube 0.03 and communications_satellite 0.03 (A9). Its market effects already sit on its dependents (automatic_switching price 0.02, universal_telephone_service catchment 0.03), and trade.gapClosingFraction (telegraph's operand) is saturated. | (b) now; (a) PROPOSED as an additional live effect (§8.3) | (b) yes; (a) no |
| petroleum_refining (292, A8) | `building.refinery`. No project and no oil or fuel good. | 4 (internal_combustion, automobile_mass, plastics, motor_road); `RoadDevelopmentTests` (motor_road's prereq string) | Tier D. Unscheduled. | None genuine. Kerosene lighting has no operand. Motor fuel's consequence is realized on internal_combustion and automobile_mass (catchment); a delta here would double-claim it. | (b) unscheduled: Production chains - oil refinery (tier D) | yes (b) |
| integrated_circuit (317, A9) | `building.semiconductor_fab`. No project. | 3 (microprocessor, internet, lunar_flight) | Tier G. Unscheduled. | `research.researchPointsCoefficient` ("cheap computation"): 92.2 %, above the line. `institutions.universities.maxResearchCostReduction`: 90.1 %. Rejected (saturation, and internet already carries computation into RP). | (b) unscheduled: Construction - semiconductor fab (tier G) | yes (b) |
| nuclear_power (322, A9) | `building.nuclear_power_station`. No project and no energy or electricity system at all. | 0; `ResearchContentTests` (name) | Tier G. D-062 R10: "no new nuclear simulation mechanics in M6". Spine: the nuclear layer belongs to the modern-era expansion. Unscheduled. | Excluded by D-062 R10; no energy operand exists. | (b) unscheduled: Energy - nuclear power station (tier G) | yes (b) |
| solar_pv (346, A9) | `building.solar_farm`. No project and no energy system. | 3 (space_solar, frontier_energy (repeatable), communications_satellite). Removal would break the final-Age repeatable's prerequisite. | Tier G. Unscheduled. | None: no generation or energy operand. | (b) unscheduled: Energy - solar farm (tier G) | yes (b) |
| wind_turbine_modern (355, A9) | `building.wind_farm`. No project and no energy system. | 0 | Tier G. Unscheduled. | None: no energy operand. | (b) unscheduled: Energy - wind farm (tier G) | yes (b) |
| cloud_computing (368, A9) | `building.data_centre`. No project. | 0 | Tier G. Unscheduled. | `research.researchPointsCoefficient`: 92.2 %. `governance.taxExtractionResponseMax` (relational_database's operand): 89.9 %. Rejected. | (b) unscheduled: Construction - data centre (tier G) | yes (b) |
| desalination (393, A9) | `building.desalination_plant`. No project. Settlement water supply is not modelled: tier A's `infra.well` blocker is "needs a water-supply term in the population/health model". | 0 | Tier G. Unscheduled. | None: no water-supply operand. Mortality or catchment would mislabel the effect. | (b) unscheduled: Settlement water supply - desalination plant (tier G) | yes (b) |
| fusion_power (409, A9) | `building.fusion_plant`. No project and no energy system. | 1 (fusion_propulsion) | Tier G. Unscheduled. | None: no energy operand. | (b) unscheduled: Energy - fusion power plant (tier G) | yes (b) |
| trace_italienne (229, A7) | `building.bastion_fort`, tier E. No project. Siege and fortification are not simulated. | 1 (military_engineers) | **M7 Battle Layer.** Deferred-entity plan §E ("castle, bastion_fort … owned by M7 Battle Layer (do not pull forward)"); H7-08; H7-07 (siege, D-039 Part D). | `governance.authorityDecayCostUnits` (stone_fortification's proxy): weak. A bastion's meaning is siege resistance, which is M7. Rejected. | (b) M7: Battle Layer - siege and fortification | yes (b) |
| rocket (308, A9) | `unit.missile` (tier E, which gates nothing), `building.spaceport` and `project.orbital_launch` (tier G). None has a realizing system. | 3 (icbm, guided_missile, orbital_launch_vehicle); unit-families.json `ballistic_missiles` realizedBy unit.missile | **M7 Battle Layer** for the missile unit (§E, H7-03 recruitment, H7-08). Unscheduled for the space programme (tier G; the Spine rescoped the space era to "a note"). | None: the dependents already carry the space-surveillance proxies (`fog.exploreMaxRadiusNodes` on the satellite family, 90.0 %). | (b) M7: Battle Layer - missile forces; and (b) unscheduled: Space programme - spaceport and orbital launch (tier G) | yes (b), 2 entries |

### 8.3 Proposals for the Director (NOT applied)

**(a) Additional live effects: papyrus and telephone.** Exact content diff in `Sim.Data/content/research.json`. Each
node keeps its `future` entry, because its placeholder unlock still belongs to a future system. Keys, costs,
prerequisites and Ages are unchanged.

```diff
   "id": "papyrus",  … "effects": {
     "immediate": [],
+    "realized": [{"operand": "governance.authorityDecayCostUnits", "delta": 0.04}],
     "future": [{"milestone": "unscheduled", "system": "Construction - library (deferred-entity plan, tier B)", …}]
   "id": "telephone",  … "effects": {
     "immediate": [],
+    "realized": [{"operand": "governance.authorityDecayCostUnits", "delta": 0.03}],
     "future": [{"milestone": "unscheduled", "system": "Infrastructure - telephone network (deferred-entity plan, tier C)", …}]
```

- **Reader:** Governance.AdministrativeReach (`Sim.Core/State/Governance.cs:94-115`, via GovernanceSystem): reach
  `exp(−travelCost / (authorityDecayCostUnits × (1 + Σ)))`, which feeds `ControlRow.Strength`.
- **Σ:** 2.570 → 2.640 / 3.0 (88.0 %). That stays below the 90 % line, so no other delta is rescaled and
  `NoOperand_SumsAboveItsCap` still holds.
- **Deltas:** they follow the comparable nodes in §8.2.
- **Consequences:** both nodes would turn Simulated on the card. The tests to update when it is applied are
  `GapAuditDisposition` (realized entries), the card test's verdict expectation for these two, and
  `TheShippedFutureNodes_AddNothingToAnySigma`, which grants every future-declaring node, so it would see the new Σ.
  `KnowledgeEffectsTeethTests` would also need a row each.
- **Not measured here:** whether any pinned run completes papyrus or telephone within its horizon, and so whether any
  golden moves. Every pin must be re-measured on application. The attribution twin is this branch's content (the same
  content without the two realized entries).
- **Why the other 15 get no (a):** the per-row reasons are in §8.2. For four of them the closest operand is at or
  above the 90 % line (oil_press, plank_boat, integrated_circuit, cloud_computing). For three, the meaning is already
  realized on their dependents (sulphuric_acid, petroleum_refining, kiln_updraft's smelting line). For six, no
  operand of that meaning exists (fulling_mill and the energy, water and space rows). nuclear_power is excluded by
  D-062 R10. trace_italienne's meaning is M7 siege.

**(c) Removal: none proposed.** Every node is a real historical technology, and its unlock has a recorded realization
path (tier A–G). Eleven of the 17 gate dependents. solar_pv gates a final-Age repeatable, and the Age-sweep cohorts
include all of them.

### 8.4 Proof the applied change is gameplay-inert (measured on this tree)

- **Structural.** The only reader of `ResearchNode.FutureEffects` in Sim.Core, Sim.Cli and Sim.Ui is
  `InfoQuery` (the card). The research.json diff only replaces the 17 lines `"immediate": []` with `"immediate": [],`
  plus a `future` list. No realized effect, level, cost, prerequisite, Age or key changed, and the 17 nodes' entity
  unlocks are the same.
- **In play (the twin method of §7.4).**
  - Test: `FutureEffectsTests.InPlay_TheGapAuditDeclarations_ChangeNoWorld`.
  - Setup: the canonical founded world (seed 42, one AI empire). Every holder is granted the 17 nodes plus the control
    sanitary_engineering, whose only effect is a realized `demographics.mortalityPerYear` delta. The AI then runs 40
    turns.
  - Twin: the shipped content against the shipped content with the 17 `future` lists removed. That second content is
    exactly the pre-audit research.json. The twin gives the **identical world hash**.
  - Control: removing the control's realized effect gives a **different** hash, so the test has teeth.
  - The existing `InPlay_TheRetargetsChangeTheWorld_AndTheFutureDeclarationsDoNot` now grants all 23 future nodes and
    still shows no change.
- **Pins.** The golden, CI-pin and snapshot tests (`--filter "FullyQualifiedName~Golden|FullyQualifiedName~CiPin|
  FullyQualifiedName~SnapshotTests"`) and the full suites were run on this tree. Results are in §8.5.

### 8.5 Enforcement and validation (this tree)

- **`KnownGapsWithoutARealEffect` is now empty.** The exact-match R1 test
  (`EveryNode_HasARealEffect_OrADeclaredFutureEffect`) is unchanged. It fails if any node loses its declaration, or if
  a new node has neither a real nor a future effect.
- **`TheGapAuditDisposition_IsApplied_FutureOnly_KeysAndUnlocksUnchanged`** pins, for each of the 17: its key, no
  realized effect, no level, its milestones, operand null, and its unlocks.
- **The loader guard is unchanged.** It still refuses a realized or level effect on an operand no system consumes, and
  a future entry naming a consumed operand.
- **The card.** `InfoQueryTests.TheGapAuditNodes_ShowTheirFutureEffects_AsNotSimulated_AndKeepTheirVerdict` checks the
  future lines ("no milestone yet" for unscheduled systems, "arrives with M7" for the Battle Layer), the placeholder
  entity lines ("not built in this build" or "Battle Layer (M7)"), and the unchanged verdict. In
  `EveryFutureEffect_IsOnTheCard_…` the expected counts become 23 nodes and 26 lines.
- **Reports.**
  - `tranche2-effect-coverage.md` is regenerated: 972 nodes with a realized effect on a consumed operand, 23 future-only
    nodes, 26 future declarations, 0 nodes without either.
  - The three research `--check` scripts went stale on the content hash only. They were regenerated and pass.
