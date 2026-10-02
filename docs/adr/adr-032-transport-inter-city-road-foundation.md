# ADR-032 — THE TRANSPORT / INTER-CITY ROAD FOUNDATION

**Status:** ACCEPTED for implementation under the Director's transport instruction of 2026-10-02 (LOCKED rulings
1–25 and the closing note on trade speed). Contract changes listed in §2 await the Director's acceptance ruling;
nothing is merged. Branch `transport-roads-foundation`, cut from `research-progression-foundation` @ `c8ceb5f`.

**Implements:** the Director's transport rulings 1–25; D-047 ruling 8 (road nodes are infrastructure-CLASS unlocks,
realized by construction; basic paths are baseline); D-009 (one multi-modal network, path → road → highway, edges
carry mode, capacity, condition); D-040 B4 (new modes extend the network's edge types, not a separate movement
system).

**Touches the kernel contract:** schema v28 → **v29**, one system (SystemId 27), one order kind (8), two tables,
one sim.json section. This record is the ADR the constitution requires for a touched contract.

---

## §1 — LAW CONFORMANCE

- **Law 1 (conservation).** Road materials leave good stocks only through `Ledger.Flow` (sink,
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
| `TransportEdges` | `TransportEdgeRow(int Id, SettlementId A, SettlementId B, int EdgeType, int Mode, int State, long CapacityTonnesPerYear, double LengthKm, int Condition, long BuiltTurn, long UpgradedTurn)` | 60 | RoadDevelopmentSystem |
| `RoadDevelopments` | `RoadDevelopmentRow(long Turn, PolityId Polity, int Edge, SettlementId A, SettlementId B, int FromClass, int ToClass, int Kind, long Usage, long MaterialUnits)` | 52 | RoadDevelopmentSystem |

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

- **A multigraph.** `TransportEdgeRow.Id` identifies the PHYSICAL route; any number of rows may join the same two
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
3. **Target class.** The best class whose entity the issuer is knowledge-eligible for. If an existing edge of the
   target's TIER is below the target, it is **upgraded in place** (same id, `UpgradedTurn` set); otherwise a **new
   parallel edge** is laid — reaching a better tier is a new alignment, and the old route stays (ruling 14). A pair
   already at the target class is ineligible (`AtBestKnownClass`); with no class known, `NoKnownClass`.
4. **Ranking.** Eligible routes by `(usage DESC, A ASC, B ASC)` — integers only, a total order.
5. **Percentage.** With total usage U > 0: the shortest ranked prefix whose cumulative usage ≥ pct% × U (pct > 0
   always takes the first). With U = 0 (no trade yet): ceil(pct% × n) routes.
6. **Cost (ruling 12, PLACEHOLDER).** Per good: `ceil(LengthKm × max(0, qty/km(target) − qty/km(replaced)))`; a
   new route replaces nothing. Paid by the lowest-id endpoint the issuer controls.
7. **Affordability (ruling 13).** In plan order against the live stocks; **the first step that cannot be paid in
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

**Why not the lattice Pathfinder.** Feeding inter-city lanes into `Pathfinder`'s overlay would change catchment
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

1. **Catchment / lattice effect of built roads.** Recommend: keep built roads OUT of the lattice overlay until the
   trade packet, so a road changes trade speed (via the hook) but not farmland catchments. Alternative: add road
   lanes to `Pathfinder`'s overlay and bump the network revision on development, so roads also extend catchments.
2. **Usage proxy.** Recommend: last turn's realised `TradeFlows` (shipped). Alternative: a smoothed multi-turn
   average stored as a new derived table (steadier ranking, new state).
3. **Payer.** Recommend: the issuer's lowest-id endpoint (shipped). Alternative: the capital, or split per
   endpoint by population.
4. **Cost numbers.** Every `materialsPerKm` value is a placeholder; on the founded dev world stone is single-digit,
   so only trackways are affordable early. Recommend: leave until the extraction economy is tuned. Alternative:
   price roads in construction labour (adult-years) like PathBuild and ConstructionSystem capacity.
5. **Upgrade across tiers.** Recommend: a better TIER is always a new parallel alignment (shipped). Alternative:
   allow an in-place Trackway → BuiltRoad rebuild.
