# ADR-032 — THE TRANSPORT / INTER-CITY ROAD FOUNDATION

**Status:** ACCEPTED for implementation under the Director's transport instruction of 2026-10-02 (LOCKED rulings
1–25 and the closing note on trade speed). Contract changes listed in §2 await the Director's acceptance ruling;
nothing is merged. Branch `transport-roads-foundation`, cut from `research-progression-foundation` @ `c8ceb5f`.

**Implements:** the Director's transport rulings 1–25; D-047 ruling 8 (road nodes are infrastructure-CLASS unlocks,
realized by construction; basic paths are baseline); D-009 (one multi-modal network, path → road → highway, edges
carry mode, capacity, condition); D-040 B4 (new modes extend the network's edge types, not a separate movement
system).

**AMENDED 2026-10-02 by the Director's FINAL transport rulings 1–34** (the "finalize" pass on this branch).
The final model is §10; it SUPERSEDES the parts of §1–§9 marked **[SUPERSEDED — §10]**. Nothing above §10 was
deleted, so the record of the first implementation stays readable.

**Touches the kernel contract:** schema v28 → **v29**, one system (SystemId 27), one order kind (8), two tables,
one sim.json section. This record is the ADR the constitution requires for a touched contract.

---

## §1 — LAW CONFORMANCE

- **Law 1 (conservation).** **[Payer and all-or-nothing SUPERSEDED — §10.4/§10.3]** Road materials leave good stocks only through `Ledger.Flow` (sink,
  `ReasonIds.ConstructionMaterials` — a road is a structure, the M4-D argument that one reason answers "where did
  the timber go?"). A step is paid in full or not at all; the check precedes the first draw. No debt exists.
- **Law 2 (mechanisms over modifiers).** A road class's speed and capacity are coefficients inside the travel-time
  resolution (class-weighted route length, bottleneck capacity), never free-floating buffs.
- **Law 3 (dt).** Road development is a discrete action (no rate, no dt). The travel-time hook turns per-YEAR
  capacities into durations (`tonnes / capacityPerYear × 365.25`); it never takes a per-turn amount.
- **Law 4 (no calendar gates).** A class becomes buildable when the issuer's COMPLETED knowledge satisfies the
  class's research entity (`ResearchQuery.IsKnowledgeEligible`) — computed state. Research completion builds
  nothing (ruling 15): it only widens what the next order may choose.
- **Law 5 (determinism).** No RNG; no dictionary or hash-set iteration; explicit composite ordering everywhere
  (`(usage DESC, A ASC, B ASC)` for routes; `(cost, settlement index)` in Dijkstra); tie-dense test shipped.
- **Law 6 (isolation).** RoadDevelopmentSystem references no system. It reads PREV tables (research, controls,
  `SettlementDistances`, `TradeFlows`) and owns its two tables plus the sanctioned `GoodStocks` share. The
  selection is the pure static `RoadDevelopmentQuery` (the AgeQuery/ResearchQuery precedent).

## §2 — CONTRACT CHANGES

### 2.1 Schema v29

`CanonicalSchema.Version` 28 → 29. Appended after `UnitConversions`, in this order:

| table | row | width | owner |
|---|---|---|---|
| `TransportEdges` | **[layout SUPERSEDED — §10.1: 80 bytes]** `TransportEdgeRow(int Id, SettlementId A, SettlementId B, int EdgeType, int Mode, int State, long CapacityTonnesPerYear, double LengthKm, int Condition, long BuiltTurn, long UpgradedTurn)` | 60 | RoadDevelopmentSystem |
| `RoadDevelopments` | **[layout SUPERSEDED — §10.1: 68 bytes]** `RoadDevelopmentRow(long Turn, PolityId Polity, int Edge, SettlementId A, SettlementId B, int FromClass, int ToClass, int Kind, long Usage, long MaterialUnits)` | 52 | RoadDevelopmentSystem |

No existing row changed width. `NetworkEdges` (the free DirtPath baseline, PathBuild's lattice segments) is
untouched. `SnapshotDiff` gains both layouts; `WorldStates.StateEquals` compares both.

### 2.2 Edge types (`EdgeTypes`)

`DirtPath = 1` (existing) plus `Trackway = 2`, `BuiltRoad = 3`, `PavedRoad = 4`, `MacadamRoad = 5`,
`Highway = 6` (`MotorRoad` is a synonym of `Highway`). Tiers (`RoadTiers`): PATH = DirtPath, Trackway; ROAD =
BuiltRoad, PavedRoad, MacadamRoad; HIGHWAY = Highway. Exactly the ratified hierarchy (ruling 5); no other tier.
`TransportModes.Road = 1` is the only mode; `TransportEdgeStates.Complete = 1` the only state.

### 2.3 Order kind 8 — `DevelopRoads`

`TargetId` reserved, must be 0; `Amount` = the percentage in (0, 100] (the slider). Load-validated. Player and AI
issue the same order via `RoadDevelopmentQuery.DevelopOrder`. One `DevelopRoads` per Empire per turn takes effect
(the first in log order). **Delivery:** stamped turn t → applied by the step t → t+1 against the state of turn t;
the built edge first exists in the state of turn t+1 (`BuiltTurn` = t+1, the log row's `Turn` = t). Pinned
turn-exactly by `RoadDevelopmentTests.PlayerAction_ConstructsRoads_TurnExact_…`.

### 2.4 System 27 — `roaddevelopment`

Last in `pipeline.json` (after `agetransition`), so its stock draw follows every other GoodStocks holder in the
step. Sole owner of `TransportEdges` and `RoadDevelopments`; **sixth holder of the GoodStocks share** (Amount via
Ledger SINK only, reason ConstructionMaterials, at the paying settlement; recorded in SystemCatalog's ownership
paragraph). A turn without a `DevelopRoads` order returns at the first loop — no scan of anything.

### 2.5 sim.json `roads` (optional section)

`baselineKmPerDay`, `maxRouteKm`, and one `classes[]` entry per road class: `edgeType`, `entity` (the research.json
infrastructure entity, null only for DirtPath), `speedFactor` (0,1], `capacityTonnesPerYear`, `materialsPerKm`.
Validated at load (every class exactly once; DirtPath has no entity and no materials; entity and goods names
resolve in research.json and goods.json). Absent ⇒ the system is inert (toy configs). **Every number is TUNE.**

## §3 — THE EDGE MODEL (rulings 4, 6, 10, 19, 20, 21)

- **A multigraph.** **["of different classes" by upgrade SUPERSEDED — §10.2]** `TransportEdgeRow.Id` identifies the PHYSICAL route; any number of rows may join the same two
  settlements, of different classes. Nothing in the model, the queries or the selection assumes one edge per pair.
- **Endpoints are settlements** — the inter-city scope of ruling 7. No intra-city street exists anywhere.
- **No owner field** (ruling 6). The log row's `Polity` is who ORDERED and PAID, not ownership. A road may end in a
  foreign or unruled settlement and crosses any territory; nothing reads claims or territory.
- **Condition** is written 0 and read by no system (ruling 8): no wear, decay, maintenance or repair.
- **Junctions are derived** (ruling 10): `TransportQuery.Degree / EdgesAt / EdgesBetween / Neighbors /
  SharedNeighbors / ModesAt / IsJunction (degree ≥ 3) / Junctions`. No junction entity, no node table.
- **Future modes** (rail, canal, sea, air) join as rows with their own `Mode` and edge types on the same table —
  D-040 B4's "one network". No speculative entity was created (ruling 21).
- **Why not `NetworkEdges`.** Those rows are lattice-step fast lanes between lattice anchors, fed to every
  pathfinding query (catchment, PathBuild routing). Putting inter-city roads there would (a) give the free baseline
  and the built roads one row type, against the gap-audit instruction that higher tiers be NEW edge types "never
  the existing dirt path", and (b) silently feed catchment partitioning with inter-city lanes. A separate table
  keeps the baseline free and untouched and makes the built network a distinct, queryable layer.

## §4 — THE DEVELOPMENT OPERATION (rulings 1, 2, 9, 12–15)

`RoadDevelopmentQuery.Plan` is THE selection; the system applies it, the AI policy and any UI preview only read it.

1. **Candidate routes.** Every settlement pair (A < B) with a finite cached baseline cost in `SettlementDistances`
   (derived by CatchmentSystem on the D-016 triggers only), a baseline length ≤ `roads.maxRouteKm`, and at least one
   endpoint the issuer CONTROLS. Length = travel cost × ideal-ground km per cost unit
   (`LatticeGeometry.KmPerCostUnitOnIdealGround(terrain)`, the chokepoint; 1.0 on a terrain-less toy world).
2. **Usage proxy (ruling 11).** Units of every good that crossed the pair, both directions, in PREV `TradeFlows` —
   the only measured inter-city transport in the simulation. No traffic model. One pass over the flows into a
   settlement-index matrix, paid only on a turn carrying an order.
3. **[SUPERSEDED — §10.2: every modernization is in place]** **Target class.** The best class whose entity the issuer is knowledge-eligible for. If an existing edge of the
   target's TIER is below the target, it is **upgraded in place** (same id, `UpgradedTurn` set); otherwise a **new
   parallel edge** is laid — reaching a better tier is a new alignment, and the old route stays (ruling 14). A pair
   already at the target class is ineligible (`AtBestKnownClass`); with no class known, `NoKnownClass`.
4. **Ranking.** Eligible routes by `(usage DESC, A ASC, B ASC)` — integers only, a total order.
5. **Percentage.** With total usage U > 0: the shortest ranked prefix whose cumulative usage ≥ pct% × U (pct > 0
   always takes the first). With U = 0 (no trade yet): ceil(pct% × n) routes.
6. **[SUPERSEDED — §10.3/§10.4]** **Cost (ruling 12, PLACEHOLDER).** Per good: `ceil(LengthKm × max(0, qty/km(target) − qty/km(replaced)))`; a
   new route replaces nothing. Paid by the lowest-id endpoint the issuer controls.
7. **[SUPERSEDED — §10.3]** **Affordability (ruling 13).** In plan order against the live stocks; **the first step that cannot be paid in
   full ends the order.** The result is the longest affordable PREFIX — a cheaper route further down is never
   substituted, nothing is overspent, no debt.
8. **Two Empires, one step.** The plan is computed against the system's NEXT edge table, so a second order in the
   same step sees the first's roads and never double-builds a pair.

**AI (ruling 9).** `RoadDevelopmentPolicy` (not a system; writes no state) returns `DevelopRoads` orders built by
the same `DevelopOrder` constructor: 50 % when the AI's routes carry trade, else 25 %, issued only when the plan is
non-empty and its first step affordable. Player-commanded polities are never touched.

## §5 — RESEARCH GATING AND CONTENT (rulings 15–18)

| class | entity | requires |
|---|---|---|
| Trackway | `infra.road_track` | `track_road` |
| BuiltRoad | `infra.road_built` | `track_road AND stone_dry` |
| PavedRoad | `infra.road_paved` | `road_paved` |
| MacadamRoad | `infra.road_macadam` | `macadam` |
| Highway | `infra.road_highway` (NEW) | `motor_road` (NEW) |

- **`motor_road`** — key **425** (verified free: technology keys were 1–424, civics 1001–1006), A9, engineering,
  prereqs `automobile_mass AND reinforced_concrete AND petroleum_refining`, depth 23 (reinforced_concrete 22 + 1),
  emerged "1924 CE Milano–Laghi autostrada; 1932 CE Cologne–Bonn" (after 1913 / 1867 / 1859 — the causal
  chronology test passes). ADR-029 cost: novelty 0.5, difficulty 2.5, material 1.5, institutional 2.5, breadth 0.5,
  prereq complexity 1.0 → magnitude 8.5 → 92.4 × 2^8.5 = 33,449 → **33,450 RP** (tier T2, like macadam).
  Appended after the corpus (no key renumbered); the engineering subtree's `finite_nodes_to_exhaust` moves
  124 → 125 on its five frontier repeatables, and the finite set 420 → 421.
- **`track_road`** — key, name ("Trackway") and requirement unchanged; the description and capabilities now say
  "an IMPROVED PATH, not an engineered road" (path tier). It still lists `infra.road_built` because that entity's
  authoritative requirement names it (`track_road AND stone_dry`) and the loader enforces the reverse index; the
  wording says so.
- **`building.dry_dock`** — `canal_lock` → **`carrack`**. A pound lock is canal hydraulics; a dry dock is a
  shipbuilding facility needed once hulls are too large to beach or careen. The first European graving docks
  (Portsmouth 1495) were built for the large full-rigged ships the `carrack` node represents (A7, naval, ~1,450 CE).
  `tech-graph-v0.6.json` itself records how the canal link arose: its v0.5 critique note on `ship_of_line` names a
  "fold-redirect bug: dry_dock had been folded under canal_navigation" — the canal requirement is that bug's residue.
  `carrack` is an existing node; nothing new was invented. The listing moved from `canal_lock.unlocks.buildings`
  to `carrack.unlocks.buildings`.
- `docs/research-corpus-audit.md` and `docs/research-calibration-report.md` regenerated (`--check` green).

## §6 — GOLDENS

Four pins moved, ONCE, for the layout alone — two EMPTY count prefixes (no golden run issues a `DevelopRoads`
order). `IntegratedPinAttributionTests.*_MovedForTheTransportLayoutAlone` / `…V29TransportTrailerAlone` strip the
two tables, drop their prefixes and return every previous value byte for byte, which also proves the research
content change (motor_road, dry dock, track_road wording) moved no research choice in those runs.

| pin | old (v28) | new (v29) |
|---|---|---|
| toy `SnapshotTests.GoldenHash` (seed 42, 200) | `498635bf…fdd4296` | `4c051fd40e9b86610ea7e2245daaff55d6503074ace491a986b41959f4f73151` |
| founded `SnapshotTests.FoundedGolden` + `ci.yml FOUNDED_GOLDEN` | `15c63d65…f83fcd` | `b2c0032f9e0a726627e85e6b4856ff454624d7f89d11492ae8cf963ae2a50ea0` |
| `FirstReignTests` turn 40 | `259c13cf…7014050` | `c805ca10e1e24ae686f5a0d59564e77ceecb9fd50de5aad51489887fc61455c3` |
| `DrivenGoldenTests` seed 42, 300 | `f94b01eb…c2083f` | `0460e6e916d1b2bb0d39595c1daa5772879ca3d39d90334474b32da003aeee3a` |

The founded value was derived twice: the in-test harness and two CLI processes
(`sim run --founded --seed 42 --turns 300 --hash-log`) with byte-identical logs.

## §7 — THE TRAVEL-TIME HOOK, AND HOW TRADE WILL CONSUME IT

`TransportQuery.EstimateFreight(world, roads, from, to, tonnes)` — deterministic Dijkstra over settlements whose
links are (a) the free baseline for every `SettlementDistances` pair (length × DirtPath speed factor 1.0, DirtPath
capacity) and (b) every built Road edge (`LengthKm × speedFactor(class)`, the class capacity). It returns the
class-weighted length, `TransitDays = CostKm / baselineKmPerDay`, the bottleneck capacity along the chosen path,
`ThroughputDays = tonnes / bottleneck × 365.25`, the total, the settlement path and the edge taken per hop
(-1 = baseline). Calibrated to the Director's figure: 5,000 t over 3,000 km on the baseline = 75 days transit +
18.3 days throughput ≈ 93 days ≈ 3 months; each better class is strictly faster (pinned).

**Not consumed yet.** No system reads it; the trade system is unchanged. The intended consumption, for the later
trade packet (not built here): (1) a trade CONTRACT ("buy 100 steel every month") becomes a standing flow whose
deliveries arrive `TotalDays` after dispatch, converted to turns with the era's dt (a per-year rate × dtYears, so
dt-correct), so poor roads delay and bunch deliveries and construction that waits on them; (2) the landed price
gains a carriage term proportional to `CostKm` (a coefficient inside the price resolution, Law 2), so better roads
widen the profitable trade radius; (3) the bottleneck capacity caps the tonnage per year a route can carry. These
are recorded as design intent, not ratified mechanics.

**[SUPERSEDED — §10.5: roads ARE in the lattice Pathfinder now]** **Why not the lattice Pathfinder.** Feeding inter-city lanes into `Pathfinder`'s overlay would change catchment
partitions and PathBuild routing — a gameplay change outside this packet and a golden move with a behavioural
cause. The hook is the inter-city pathfinding layer; the lattice stays the local layer. Joining them is §9 item 1.

## §8 — PERFORMANCE

No per-turn work: the system returns immediately on a turn with no `DevelopRoads` order. Plan cost on an order turn
is O(settlements² + trade flows + candidate routes × edges) using the cached `SettlementDistances` (no lattice
build, no Dijkstra over the lattice).

**Bench** (`sim bench --founded --seed 42 --turns 300`, Release, same container, runs alternated, ms):
- c8ceb5f (base, temporary worktree, removed after): 32279.0 / 32453.5 / 32960.4 — median **32453.5**
- this branch: 32576.1 / 31495.7 / 33156.2 — median **32576.1**
- Delta of medians +122.6 ms (+0.4 %), inside run-to-run variance. The `roaddevelopment` phase itself measures
  **0.90 ms over 300 turns** (no order in a bench run: the early return).

**Suite** (Release, this tree): Sim.Tests 1199 passed / 0 failed / 4 skipped; Sim.Ui.Tests 298/298; the three gate
scripts and both research `--check`s green.

## §9 — OPEN QUESTIONS FOR THE DIRECTOR (batched; recommendation first)

1. **[RULED — §10.5: roads affect pathfinding now]** **Catchment / lattice effect of built roads.** Recommend: keep built roads OUT of the lattice overlay until the
   trade packet, so a road changes trade speed (via the hook) but not farmland catchments. Alternative: add road
   lanes to `Pathfinder`'s overlay and bump the network revision on development, so roads also extend catchments.
2. **[RULED — last-turn trade, no smoothing table]** **Usage proxy.** Recommend: last turn's realised `TradeFlows` (shipped). Alternative: a smoothed multi-turn
   average stored as a new derived table (steadier ranking, new state).
3. **[RULED — §10.4: the issuing civilization]** **Payer.** Recommend: the issuer's lowest-id endpoint (shipped). Alternative: the capital, or split per
   endpoint by population.
4. **[RULED — placeholders kept, ruling 14]** **Cost numbers.** Every `materialsPerKm` value is a placeholder; on the founded dev world stone is single-digit,
   so only trackways are affordable early. Recommend: leave until the extraction economy is tuned. Alternative:
   price roads in construction labour (adult-years) like PathBuild and ConstructionSystem capacity.
5. **[RULED — §10.2: in place, never a parallel road]** **Upgrade across tiers.** Recommend: a better TIER is always a new parallel alignment (shipped). Alternative:
   allow an in-place Trackway → BuiltRoad rebuild.

---

## §10 — THE FINAL MODEL (the Director's final transport rulings 1–34, 2026-10-02)

This section is authoritative; where §1–§9 disagree, this wins. Ratified by the Director and implemented here:

- **Roads are unowned.** `TransportEdgeRow` has no owner field; nothing reads territory to decide anything about a
  road. A road may join cities of one civilization or of two, cross foreign or unclaimed territory.
- **The civilization that issues `DevelopRoads` pays for its own development** — and owns nothing by paying.
- **Basic paths are the free baseline**: PathBuild's DirtPath lanes and the pairwise baseline cost, no research, no
  order, no resources. An order never charges for DirtPath.
- **Road upgrades happen in place; partial modernization is proportional; no parallel road is ever created merely
  by upgrading** (§10.2, §10.3).
- **Road class affects authoritative pathfinding immediately** (§10.5), and **freight travel time and pathfinding
  share the same road-performance model** (§10.6).
- **Last-turn realised trade is the provisional usage proxy** (no smoothing table); no-trade fallback is the
  deterministic route count. Ranking `(usage DESC, A ASC, B ASC)`, unchanged.
- **Road costs are tunable placeholders** (sim.json `roads.classes[].materialsPerKm`: Trackway 1 timber; BuiltRoad
  2 stone + 0.5 timber; PavedRoad 4 stone + 0.5 timber; MacadamRoad 5 stone + 0.5 timber; Highway 12 stone + 1
  tools, per km) — not calibrated, not labour.
- **Deterioration is intentionally ignored**: `Condition` is written 0 and read by nothing.
- **No builder agents; no multi-turn construction projects**: an order stamped t takes effect in the state of t+1.
- Unchanged: `DevelopRoads` (kind 8) and its percentage semantics; AI policy 50 % (routes carry trade) / 25 % via
  the same order; `motor_road` key 425 (A9, engineering, `automobile_mass AND reinforced_concrete AND
  petroleum_refining`, unlocks `infra.road_highway`); `building.dry_dock` requires `carrack`; `track_road` describes
  an improved path ("an IMPROVED PATH, not an engineered road"); junctions derived; capacity is a physical attribute
  read by no route choice.

### 10.1 Schema v29 — amended row layouts (unmerged branch; v29 never existed on main otherwise)

| row | fields | width |
|---|---|---|
| `TransportEdgeRow` | `Id, A, B, EdgeType, Mode, State, CapacityTonnesPerYear, LengthKm, Condition, BuiltTurn, UpgradedTurn,` **`TargetClass, Modernization, CostFactor`** | **80** |
| `RoadDevelopmentRow` | `Turn, Polity, Edge, A, B, FromClass, ToClass, Kind, Usage, MaterialUnits,` **`ProgressBefore, ProgressAfter`** | **68** |

`EdgeType` = the class fully achieved; `TargetClass` = the class being modernized toward (= EdgeType when idle);
`Modernization` = fraction in [0, 1) of EdgeType → TargetClass performed; `CostFactor` = the effective travel-cost
multiplier per km; `CapacityTonnesPerYear` = the effective capacity. `RoadDevelopmentRow.Kind`: 1 = `Modernize` (an
existing route row, in place), 2 = `FromBaseline` (a bare baseline path's first development writes its route row —
the SAME physical route acquiring its stable id, not a second road). Populated-table test:
`TransportSchemaTests.SchemaV29_PopulatedTransportTables_LengthAndRoundTripExact` (3 × 80 + 2 × 68, bit-exact
round trip incl. −0.0 and NaN payloads in the new doubles, hash equality).

### 10.2 In-place upgrades

The route a pair modernizes is its **lowest-id travelled road row whose class is below the issuer's best known
class**; a pair with no row modernizes its baseline path (row written on first development, `Kind` 2). The route id,
endpoints, `LengthKm` and `BuiltTurn` never change; `UpgradedTurn` records the last modernization. Repeated orders
continue the same row. Genuinely separate physical routes between one pair remain supported (the multigraph), but
development itself never adds one (pinned: `T07_08_12_13_27_30_…`, `T29_…`, and the founded replay test asserts no
pair ever carries two rows).

**Raising the target mid-modernization** (a route part-way to PavedRoad when the issuer now knows Highway): the
route's fraction is re-expressed toward the new target so that its **current effective speed is preserved**
(`RoadPerformance.RetargetFraction`); the remaining work is costed against the new target. A route part-way to a
class the issuer has NOT researched (another civilization started it) is ineligible for that issuer
(`TargetBeyondIssuerKnowledge`) — it can neither continue nor lower it.

### 10.3 Proportional modernization and partial affordability

Cost of completing a route: per good `ceil(LengthKm × max(0, qty/km(target) − qty/km(current class)) × (1 −
fraction))`. The issuer's affordable proportion `p = min over goods (civilization holding / units)`, capped at 1.
`p = 1`: pay in full, the route reaches the target class. `p < 1`: charge `floor(p × units)` per good, perform
exactly the modernization those units buy (the minimum charged/full ratio — never free, never more than paid), log
it, **and the order ends** (the purse is exhausted in at least one good). Nothing cheaper further down is
substituted; no overspend; no debt; no cost moves to another civilization. Example pinned by `T14_26_…`: 100 km
Macadam → Highway (7 stone + 1 tools/km = 700 + 100); 350 stone held → 50 %, 350 stone + 50 tools charged; the next
order completes the same route for the remaining 350 + 50.

### 10.4 Payment — the issuing civilization's resource base (a NEW minimal mechanism; see §11 Q1) [draw ORDER SUPERSEDED — §12.1]

The repository has no civilization-level drawing mechanism: resources belong economically to the controlling polity
but stay physically at its settlements (D-042 §10), and the only polity-level reading was `AgeQuery`'s sum over
controlled settlements. This packet adds the minimal deterministic draw, in `RoadDevelopmentQuery`:

- `PayingSettlements(world, issuer)` = the settlements the issuer CONTROLS (`ControlRow`, D-042 §5's single source of
  truth), **ascending settlement id** (a toy world with no Controls: every settlement).
- `CivilizationStock` = the sum of their `GoodStocks` rows for a good; affordability is checked against it.
- The system draws each good's units from those settlements in ascending id, a `Ledger.Flow` sink
  (`ConstructionMaterials`) per row touched, until covered. No treasury, no second resource system.

Endpoint ids play no part: a route from the issuer's city to a foreign or unruled settlement is paid from the
issuer's purse, drawn from a non-endpoint settlement when the endpoints hold nothing (`T18_19_…`, `T20_21_…`).

### 10.5 Pathfinding integration (one road-performance definition)

`RoadPerformance` (Sim.Core/State) is the single definition: a class's relative speed is `1 / speedFactor`;
a route modernized `f` from `old` to `target` travels at `speed_old + f × (speed_target − speed_old)`; its cost
factor is `1 / effective speed` (exact class values at f = 0 and f = 1); capacity interpolates likewise (floored).
`RoadDevelopmentSystem` evaluates it when writing a route and stores `CostFactor` / capacity on the row.

`Pathfinder.BuildOverlay` (the authoritative lattice A*/Dijkstra used by catchment partitions, the pairwise
`SettlementDistances`, and PathBuild routing) now adds, after PathBuild's lanes, one fast lane per travelled route
row between its two settlements' origin lattice nodes, costing `RoadPerformance.TravelCostUnits(row, km per cost
unit)` = `LengthKm × CostFactor / km-per-cost-unit`. The heuristic's admissibility bound includes these lanes. With
no route rows the overlay is byte-for-byte the previous one, which is why no golden moved.

**Cache/revision.** `CatchmentSystem` keys staleness on `RoadPerformance.NetworkRevision(world)` = PathBuild's
`NetworkMeta.Revision` + `RoadDevelopments.Count`. Both terms only grow and every modernization appends exactly one
log row, so any road change triggers the D-016 recompute on the next turn (the same one-turn lag as a dirt path),
and a world without road development has exactly the old revision. No other cache exists (Pathfinder is pure).
Monotone ladder enforced at config load (`speedFactor` strictly decreasing DirtPath → Highway).

### 10.6 Freight travel-time integration

`TransportQuery.EstimateFreight` relaxes (a) the baseline pairwise links from `SettlementDistances` — themselves the
authoritative Pathfinder costs, which already route over road lanes — and (b) each travelled route row at
`RoadPerformance.TravelKm(row)` = `LengthKm × CostFactor`, the same stored value Pathfinder uses. Pinned:
`T17_37_…` shows Pathfinder's pair cost, the catchment's `SettlementDistances` row and the freight `CostKm` are the
same number (to 1e-9) on the founded world.

### 10.7 Read-only UI surface

`RoadDevelopmentQuery.Describe(world, research, roads, goods, polity)` → `RoadRouteView[]`: route (endpoints, row
id or −1, current class, row target and progress, the issuer's target, usage, kind, eligibility/reason),
modernization %, effective cost factor and relative speed, capacity, estimated cost of completion. Pure; no state.

### 10.8 Explicitly DEFERRED

Detailed traffic; congestion; trade contracts; trade pricing; economic cost calibration; rail; ports; canals;
aviation; urban transit. Also not done: deterioration/maintenance, builders, multi-turn projects (by ruling, not
deferral).

### 10.9 Known approximations (recorded, not decisions)

- **[RESOLVED — §12.3]** A bare baseline pair's length was its cached `SettlementDistances` cost × km per cost unit.
  Once road lanes exist that cost may run over other roads, so a NEW route's length (and cost) could read shorter
  than the physical ground route. Existing rows keep their fixed `LengthKm`.
- A partial step charges `floor(p × units)`, so up to one unit per good of rounding is left unspent, never overspent.

### 10.10 Goldens and performance

**No golden moved.** No golden run issues `DevelopRoads`, so `TransportEdges` and `RoadDevelopments` stay empty,
the Pathfinder overlay is identical, and the combined network revision equals `NetworkMeta.Revision`; the v29 count
prefixes are unchanged. The amended row widths only matter for populated tables. Measured: the full suite passes
with every §6 pin untouched, and `sim run --founded --seed 42 --turns 300 --hash-log` in two processes gives
`b2c0032f…a50ea0` = `ci.yml FOUNDED_GOLDEN` (unchanged).

**Bench** (`sim bench --founded --seed 42 --turns 300`, Release, same container, the three builds alternated per
round, temporary worktrees for the first two, removed after; ms):

| build | run 1 | run 2 | run 3 | median | catchment phase median | roaddevelopment phase median |
|---|---|---|---|---|---|---|
| `c8ceb5f` (base) | 32420.2 | 34332.3 | 32294.2 | **32420.2** | 14639.8 | — |
| `e4c3c53` | 31615.0 | 33039.3 | 32786.2 | **32786.2** | 14593.9 | 0.90 |
| final | 34083.1 | 32789.0 | 32586.2 | **32789.0** | 14736.9 | 1.11 |

Final vs base +368.9 ms (+1.1 %); final vs `e4c3c53` +2.8 ms (+0.01 %) — inside the run-to-run spread (≈ 2 s here).
Attributable to this pass: the overlay builder now reads `TransportEdges.Count` (0 in the bench) once per
pathfinding query, and the road phase stays at ~1 ms over 300 turns (no order: early return). Not material; nothing
was optimized.

## §11 — NEW DECISIONS BATCHED FOR THE DIRECTOR

1. **Civilization purse draw order.** Q: when the issuer pays from its controlled settlements, in what order are
   their stocks drawn? Recommended (shipped): ascending settlement id — deterministic, simple. Alternative: nearest
   first by pairwise travel cost from the route (honours D-042 §10.1 "control does not imply instantaneous
   empire-wide access" more literally). Consequence: the shipped rule may empty a remote low-id settlement's stone
   for a road elsewhere; the alternative changes only which rows are drawn, not totals or conservation.
2. **Retargeting a partially modernized route.** Q: when a higher class becomes known mid-modernization, how is
   progress carried? Recommended (shipped): preserve current effective speed (fraction re-expressed toward the new
   target). Alternative: finish the old target first, then start the new one. Consequence: the shipped rule never
   loses performance and never strands paid work; the alternative costs a little less in total materials but
   delays the better class.

## §12 — THE DIRECTOR'S FINAL RULINGS ON §11 (2026-10-02)

Everything else in the transport model stays FROZEN: no logistics, shipments, transport time mechanics, congestion,
new route planner or builder agents.

### 12.1 Payment order — nearest first (§11 Q1, ruled)

The issuing civilization remains the only payer; the SET of paying settlements is unchanged (§10.4). The DRAW ORDER for
one development step is `RoadDevelopmentQuery.PayingSettlements(world, issuer, A, B)`:

- **Distance (implementation choice, documented here):** the exact INTEGER squared pixel distance, on the terrain
  raster, from the settlement's site cell to the NEARER of the two endpoints' site cells. It is purely geographic
  (site cells never move; no travel cost, road class or modernization enters it) and exact, so ties are exact. An
  endpoint the issuer controls is at distance 0. Distance to the segment interior was not used: the nearer-endpoint
  measure is simpler and needs no floating point.
- **Ties:** settlement id ascending. **Terrain-less world** (hand-built toy): no positions, every distance 0, so the
  order is ascending id (the previous rule).
- Each good is drawn in that order through `Ledger.Flow` (sink, `ConstructionMaterials`) until covered or the
  civilization's holding is exhausted; affordability is unchanged (an order-free sum). No debt, no overspend.
- The AI policy's affordability check uses the sum, so it is unaffected by order.

Tests: `FinalRuling1_PayingSettlements_AreRankedNearestToTheRouteFirst_TiesById_TieDense`,
`FinalRuling1_Draw_NearestFirst_ExhaustionFallsThrough_NoOverspend_Conserved_Deterministic`.

### 12.2 Mid-upgrade target change — shipped behaviour retained (§11 Q2, ruled)

Unchanged code: the row keeps its id, paid work is kept (re-expressed via `RoadPerformance.RetargetFraction` so the
effective speed is preserved), nothing is refunded, effective speed never regresses. Added test:
`FinalRuling2_TargetChangeWhilePartial_KeepsIdAndPaidWork_NoSpeedRegression_SaveLoadAndReplay` (partial step toward
the new target, exact material accounting, save/load mid-change continues identically, deterministic).

### 12.3 Road length invariant (ruled)

**Invariant:** a road's physical length never depends on current network performance.

**Investigation:** the length of a NEW route row (bare baseline pair) came from `SettlementDistances.TravelCost ×
km per cost unit`. Since §10.5, CatchmentSystem computes `SettlementDistances` with the authoritative Pathfinder,
whose overlay includes every road row's lane at its `CostFactor`. So modernizing road (X, Y) shortened the cached cost
of other pairs routed over it, and a new road's `LengthKm` — and therefore its fixed material cost and all its future
travel costs — depended on other roads' speed. The invariant was violated; a code change WAS required.

**Fix (minimal):** `RoadDevelopmentQuery.GeographicKm` — on a world with terrain, the straight-line (flat-raster
Euclidean) distance between the two settlements' site cells × `KmPerPx`. The map is a flat square raster (no wrap),
so this is the existing geometry. A terrain-less toy world (no pathfinder ever runs; distances are hand-written) keeps
its hand-written baseline km. `SettlementDistances` is still used for candidacy (a finite, reachable pair) but no longer
for length; `roads.maxRouteKm` now bounds this geographic length. Existing rows keep their fixed `LengthKm`.
Consequence: a new route's length (and so its cost) is the crow-flies distance, shorter than a ground path through
rough terrain — a TUNE-level effect on costs only.

Tests: `FinalRuling3_NewRoadLength_IsGeographic_AndNoOtherRoadsSpeedCanChangeIt` (cached costs collapsed by a
highway elsewhere; the new row's `LengthKm` is unchanged) and
`FinalRuling3_FoundedWorld_AHighwayElsewhereShortensCachedCosts_ButNotAnyCandidateRoutesLength` (the real catchment
recompute: the cached cost falls, every bare candidate's length is identical).

---

**Coupling note (2026-10-02, ADR-033 D4 — appended; no transport rule or code changed).** Since §10.5 the
road-aware `SettlementDistances` feed FOUR consumers, recorded together with file:line evidence in
`docs/m5-governing-loop-port.md` §5 B: (1) administrative reach — `ControlRow.Strength`, the M5 tax's
collection and burden (new; D-040 C6's road–control coupling realized); (2) migration damping
(`MigrationSystem.cs:404-411`); (3) the trade arbitrage threshold (`TradeArbitrageSystem.cs:126, :141,
:238-243`); (4) harvest-weather spatial correlation (`HarvestWeatherSystem.cs:121, :211-226`) —
ESCALATED: should weather correlation use geographic distance rather than road-aware travel cost? §7's
"the trade system is unchanged" refers to the freight travel-time hook (`TransportQuery`) only.
