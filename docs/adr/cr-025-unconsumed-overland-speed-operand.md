# CR-025 — `roads.baselineKmPerDay` has no consuming system: D-063 R1 ("every node a real effect") vs "do not invent gameplay"

**Status: RAISED 2026-10-25. Awaiting the Director's ruling. NO content, registry or engine change made.** Format per
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
