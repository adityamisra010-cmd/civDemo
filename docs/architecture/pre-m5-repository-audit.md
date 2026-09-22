# PRE-M5 REPOSITORY AUDIT — the Draft-1 concepts against the tree at `de5e00e`

**Reconnaissance, not a spec. Nothing here amends a ratified document, implements a mechanic, or
starts M5.** Tree under audit: `main` = `origin/main` = `de5e00e1958ca8d8c984a170d1cbbce5b9f721f4`
(M4 CLOSED on the Director's 2026-09-21 ruling; tag `m4-exit`; schema v25). Companion:
`docs/architecture/pre-m5-visual-system.md` (Phase 2).

## §0 PROVENANCE — read this before trusting anything below

- **The Draft 1 architecture document was not available to this audit.** It is not in the
  repository, and the only Draft-1 material on hand was the Director's 27-item concept list in the
  task. Every concept is therefore classified **as named**, with the reading stated in its row; where
  a name admits more than one reading the classification is given for the most severe reading and
  the others are flagged. When Draft 1 is supplied, each row's "reading" column is the thing to
  check first.
- **Method.** Eight read-only reconnaissance lanes (time · state/schema · orders/polity ·
  space/war · economy/construction · knowledge/Ages design corpus · UI/rendering · tests/docs/frozen
  register) over the tree, each followed by two adversarial verifiers with distinct lenses — one
  re-opening every cited file:line, one attempting to overturn every classification in both
  directions. No finding below is recorded until its verdict returned (CLAUDE.md:41; ADR-015 §6).
  Lane and verifier reports are working papers, not repository documents.
- **Evidence tags.** MEASURED = a command was run and its output is reported; READ = the file was
  opened and the line is cited; INFERRED = reasoning from the two. Citations are `file:line` at
  `de5e00e`. Where a document disagrees with the tree, §7 records both and the tree wins.
- **What "frozen" means here** (§6 has the register): a CLAUDE.md law, the Spine, the S8 freeze
  perimeter, a closed D-decision, the kernel contract (`docs/m0-kernel-spec.md` §3), the milestone
  ladder as resequenced by D-011 §6, or ground an OPEN CR already contests. A design document, a
  placeholder, a roadmap audit, a code comment or a PROPOSED ADR is **not** frozen, and no
  classification below rests on one.
- The two documents in `docs/architecture/` were authored on the audit branch; lane reports that
  cite them as "untracked" are correct and were right not to treat them as repository truth.

## §1 THE TREE AS IT STANDS (READ unless marked)

### 1.1 Identity and closure state
- `main` = `origin/main` = `de5e00e`, tag `m4-exit`; **M4 CLOSED** on the Director's 2026-09-21 ruling
  (`docs/milestones.md:567` "M4 CLOSES ON THIS BLOCK"). Schema `Version = 25`
  (`Sim.Core/Kernel/CanonicalSchema.cs:104`). Closure suite: Sim.Tests 896 / 0 / 4, Sim.Ui.Tests 296 / 0
  (`docs/m4-exit-inventory.md:359`). No `docs/m5-spec.md` exists; `CLAUDE.md:10` still names M4 as current
  (the S8 §5 pointer is one gate behind — §7 row 3).
- The audit branch adds only `docs/architecture/*` and the visual-foundation files named in the visual
  doc §9. MEASURED: `git diff --stat de5e00e HEAD -- Sim.Core Sim.Data Sim.Cli Sim.Tests scripts .github`
  is **empty**, so every citation in this document is a citation at `de5e00e`.
- 793 `[Fact]/[Theory]` attributes in `Sim.Tests` (Kernel 147, Systems 440, State 60, Observability 58,
  Worldgen 43, Forensic 31, Pathing 7, Chronicle 4, Conventions 2, root 1); 16 live 64-hex golden pins (4
  world goldens + 12 attribution-control constants) with the founded golden duplicated in `ci.yml:146`
  and guarded by `CiPinAgreementTests`; three gate scripts (banned constructs, read isolation,
  readonly proof) — **none enforces Law 2 or Law 4**; the only mechanical Law-2 check is a load-time
  `varietyWeight ∈ [0,1]` refusal (`Sim.Core/Systems/NeedsConfig.cs:166-171`). The one structural
  Law-4 gate is the D-020 predicate registry: `Variables.Names` has no time variable
  (`Sim.Core/State/Variables.cs:93`), so a calendar gate is inexpressible in `sim.json` predicates.

### 1.2 Kernel, turn and time — see §2 in full
One executor, one dt derivation (`TurnExecutor.cs:86`), an atomic `Step` with a read-only observer, a
static era table outside `WorldState` (`EraTable.cs:7-8`), whole-day dt with band spans an exact multiple
of dt (`EraTableLoader.cs:95-109`), 1,630 canonical turns pinned (`EraTableTests.cs:30, :51-53`), and an
order log whose only time coordinate is the integer turn (`OrderLog.cs:70`). Six dts already run in one
campaign; the CR-001 detonator has two rigs with two bars (turn windows 150→250 / 250→450 at ≤ 0.1 per
1000·yr; year windows [1600,2500] vs [2500,3400] at ≤ 0.0001/yr). CR-006 is OPEN.

### 1.3 World state, schema, replay
- **40 row types, 40 tables** (`Sim.Core/State/WorldState.cs:8-780`, MEASURED); six carry a `Conserved`
  field (Biomass, Goods, Buckets, GoodStocks, Housing, Notables) — the only Law-1 stocks. Non-conserved
  `double` stocks are the settled pattern for everything else: `GrievanceRow` "A DOUBLE stock and
  deliberately NOT conserved … law 1 governs people/money/goods; this is none of them"
  (`WorldState.cs:556-560`), `SmoothedAttractivenessRow`, `PathProgressRow.Banked`, the `ClassStateRow`
  latch.
- The clock (Turn, SimDays, DtDays) is inside every hash (`CanonicalSchema.cs:150, :158-161`); saves
  reject any other schema version — "no migration exists" (`Snapshot.cs:44-49`). **Appending even an
  EMPTY table moves every hash** by its 4-byte count prefix (T4.8 re-pin record,
  `DrivenGoldenTests.cs:145-156`), and a new system moves every hash the first turn it draws RNG
  (`RngStreams` rows serialize).
- **What every new Draft-1 row pays** (lane B's checklist, both verifiers CONFIRMED): 6 edits in
  `WorldState.cs` (row, interface member, table, forwarder, ctor, clone); 4 in `CanonicalSchema.cs`
  (Version, width, Write, Read, ExpectedLength — "three edits, same file", ADR-005); three adapters that
  enumerate every table (`Sim.Tests/TestUtil/WorldStates.cs` StateEquals, `Sim.Cli/SnapshotDiff.cs`
  Layout, `PathBuildSystem.NetworkOverlayView` — the second `IReadOnlyWorldState` implementer);
  `SystemCatalog` + `pipeline.json` for a system; a POPULATED-table test (`CLAUDE.md:38`; canonical
  example `DisasterSystemTests.cs:567-600`); 4 golden test files + `ci.yml` + 7 literal
  `Assert.Equal(25, CanonicalSchema.Version)` pins in 5 files; the 12 attribution-control constants
  re-MEASURED. **v25 is claimed twice** — `main` (Disasters) and the unmerged `origin/m5-full-build`
  (TaxPolicies, `SystemId 22`) — so the next table is v26 or v27 depending on merge order
  (`CanonicalSchema.cs:77-85`; §6.3).
- The order-log wire format is `IoVersion = 1`, five fields, no dt (`OrderLog.cs:125, :134-142`); the
  manifest and trace carry no dt either; only the snapshot, the JSONL observers and the forensic record's
  era-band table echo it.

### 1.4 Orders, polity, control, the AI seam
- Four order kinds — `SetRainBias = 1, LaborAllocation = 2, SectorAllocation = 3, EnqueueConstruction = 4`
  (`OrderLog.cs:12, :21, :36, :49`). Every payload extension so far was packed into the five fields
  (batches; `settlementId × 8 + sectorId`; an integer project id carried exactly in the double).
  `SetRainBias` already targets a region, not a settlement (`:9-12`).
- `ActorId` IS the issuing Empire's `PolityId` (`:57-68`, `:77`); the player Empire is `PolityId 1` (CR-011
  RULED); `CommandSource` is a roster byte no system reads, and player-vs-AI command is pinned
  hash-identical (`Sim.Tests/State/EmpireOrderSeamTests.cs:76-95`). **No system and no AI appends an
  order** — `SimContext.Orders` is a read-only `OrderBatch` (`SimContext.cs:9-20`) and the log sits
  outside `WorldState`; an AI must issue orders from outside the pipeline, exactly as the UI does
  (the D-042 §6.5 topology).
- World-dependent validation runs ONCE, before turn 1, against the founding world, from the CLI only
  (`Sim.Cli/Program.cs:194-196`; `OrderValidation.cs:16-18`); the UI never calls it. Control is
  checked for `EnqueueConstruction` only (`:52-53`); `LaborAllocation`/`SectorAllocation` are never
  control-checked at validation or consumption (`PathBuildSystem.cs:71-105`) — an Empire may set a
  rival's labour split; untested either way; `m4-spec.md` has no text on it (§7 row 21). And the
  one-shot pass is already wrong for turns > 0: `RevoltSystem` removes control rows every turn a
  settlement reaches zero happiness (`RevoltSystem.cs:6-8` — `Controls` is a SANCTIONED shared table,
  Colonization appends, Revolt removes), so any evolving-state gate (a budget, an exposure) must be
  consumption-time from the start.
- `PolityRow(Id, Source)` is "DELIBERATELY LIGHTWEIGHT … knowledge, military and institution state are
  SEPARATE domains that key themselves to this id" (`WorldState.cs:694-704`). **No per-polity scalar
  exists**; published variables are settlement-keyed (`VariableRow`, `:394`). `ControlRow` cardinality
  one-or-none is intended, not enforced (`EmpireQuery.cs:109-117`, duplicates resolve to the lowest
  id). `Claims`, `Recognitions`, `Notables` are written only by the deserializer; `NotableLifecycle.Born`
  (the bucket → conserved-row `Ledger.Transfer` shape, `NotableLifecycle.cs:65-67`) has no production
  caller.
- Who traded with whom is exactly one per-turn settlement-pair table, `TradeFlowRow(From, To, Good,
  Quantity)` (`WorldState.cs:459`), cleared each step; `TradeScope` classifies domestic/foreign at read
  time and is consumed by nothing ("this classifies; it does not gate, price, tax or forbid",
  `TradeScope.cs:45`). No border, treaty, diplomacy or contact state exists (MEASURED grep 0).

### 1.5 Space, movement, infrastructure, war
- Every place is an integer: `SettlementRow.SiteCell` on the immutable 1024² terrain at 4.0 km/px;
  lattice/catchment/path rows hold lattice nodes (stride 4 → 16 km/node, a DERIVED pure function of
  terrain). **No real-valued position exists; nothing moves between turns** — migration is one
  `Ledger.Transfer` inside the step (`MigrationSystem.cs:882-884`); no speed or travel-time constant
  exists.
- Distance is a Dijkstra COST over lattice + network edges; the one km↔cost conversion is
  `LatticeGeometry.cs:63-80` and a build gate refuses `KmPerNode` outside it
  (`check-banned-constructs.sh:80-83`). `SettlementDistanceRow` is DERIVED state owned by
  `CatchmentSystem`, rebuilt on the D-016 triggers at a one-turn lag.
- The network IS state: `NetworkNodeRow`, `NetworkEdgeRow(Id, A, B, EdgeType, Cost)` — "Cost … fixed
  when the edge is built" (`WorldState.cs:58-61`) — and `NetworkMetaRow(Revision)`; exactly one edge
  type, `DirtPath = 1` (`Ids.cs:68-72`); `PathBuildSystem` is the sole writer (dt-exact banked labour,
  one lattice step per segment, sole `Revision++` at `:220`); the pathfinder consumes `Cost` as a static
  weight (`Pathfinder.cs:386`).
- **No army, unit, formation, battle, siege, war or resolver type exists** (word-bounded grep: 8 lines,
  all comments or `FlowLeg.Units`). `NotableRow` is the only war-adjacent state and states
  "DELIBERATELY ABSENT: competence, traits, experience, and every battle field … M6"
  (`WorldState.cs:630-633`); the Notables table is empty in every live world. Two per-file scope-fence
  tests pin the absence of battle vocabulary. M4 closed with "no armies; no AutoResolver"
  (`m4-exit-inventory.md:60`) while frozen `d011:59` assigns them to M4 — §7 row 5 and decision DD-10.

### 1.6 Economy and construction
- Labour shares are set by ORDERS only and consumed by `PathBuild`, the sole writer of
  `SectorAllocations`; no system drifts shares endogenously. Construction resolution is a GATE, not a
  rate: the queue head is built whole iff every material is present and `share × adults × dtYears −
  Housing.LastLaborUsed ≥ LaborRequired`; otherwise nothing happens — "no partial draw, no banked
  progress, no percentage … SO THERE IS NO TIMER" (`ConstructionSystem.cs:38-48, :136-141`;
  `WorldState.cs:737-742`). That ruling lives in code docs and packet certification only
  (`queue.md:1310-1312`; `current-state.md:276`) — `m4-spec.md` has no M4-D text (MEASURED).
- Two project types exist as data (granary, workshop); a project has no effect, location, upkeep,
  state or predicate. **Completed `Structures` are read by NOTHING** (`m4-exit-inventory.md:72,
  :220-222`; the lever table: "a completed structure feeds no need", `Levers.cs:30-32`). No structure
  arises without an `EnqueueConstruction` order, and no shipped UI/CLI code issues one. Two built stocks
  ARE endogenous with no order — dwellings and dirt-path edges — with the housing-first split ruled at
  `t3.8-spec.md:44-46`.
- The Ledger is the only mutation channel (25 call sites under `Sim.Core/Systems`); 16 reason ids
  (`Ids.cs:158`). The D-020 emergence tool is a closed predicate DSL over four registered variables
  with a per-(settlement, class) hysteresis latch (`Predicate.cs:10-26`; `ClassMobilitySystem.cs:28-33`);
  recipes carry an optional `requires` predicate, projects do not. Health is need id 4, shipped
  `bound:false` and inert (`needs.json:25-30`).

### 1.7 Knowledge, research, Ages, institutions — zero code
- MEASURED whole-word greps over `Sim.Core`: Research 0 · Technology 0 · University 0 · Veteran 0 ·
  Knowledge 3 (all doc comments) · Institution 1 (a doc comment) · Age 7 (all the demographic sense;
  zero capital-A `Age` identifiers) · Era 17 (**all time pacing**: `EraTable`, `EraTableLoader`, and
  comments about dt at band transitions). No system, predicate, recipe or view reads an era band's
  NAME; the only readers are the executor (dt) and the forensic record (the table).
- The design corpus already holds ratified positions the concepts must clear: D-042 §9 — knowledge
  Empire-scoped, research PARALLEL, generation an allocatable flow, unallocated knowledge ACCUMULATES AS
  A RESERVE, diffusion/open borders permitted sources, "Is an accumulating knowledge/science quantity
  permitted? YES" (`d042:165-175, :292`); D-040 B3 "NO TECHNOLOGY UNLOCK. LAW 4 BINDS" (`d040:59-64`);
  D-039 A5 "Era is a consequence, never an input" (`d039:37-38`); D-035-C's carrier test
  (`d035:91-93`). CR-005 is OPEN; CR-009 (era gates in frozen D-011/D-009 vs Law 4) and CR-010 (what an
  institution IS) were recommended and never written.

### 1.8 UI, rendering, assets, animation — the inventory is the visual doc's §1
One sprite (the settlement marker, constant 20 px); **no animation** (`GameTime` feeds only camera
pan and an FPS EMA); **no hover or tooltip code** (0 hits); three pure procedural bakers returning
`ArtImage` with no MonoGame types (the precedent the glyph grammar generalises); the HUD shows only
`turn N   year Y` with the epoch and year length duplicated as literals beside `SimClock.YearDays`
(`HudModel.cs:148, :208-209`) — no era label, no BCE/CE converter; no projection or forecast exists
in any model (the only forward-looking readout is a READ of `DisasterRow.Multiplier`); `Sim.Ui` is
referenced by nothing but its tests; neither grep gate scans `Sim.Ui`.

## §2 THE FIXED-TURN ARCHITECTURE vs A VARIABLE-Δt ARCHITECTURE — the conflict register

The task asked for this explicitly, so it comes first. The short form: **the tree is already
variable-dt in the sense Law 3 guarantees, and is fixed-turn in three senses that are frozen.** A
Draft-1 "variable strategic calendar" collides with the three, not with the first.

### 2.1 What Law 3 already guarantees (not a conflict — recorded so it is not re-litigated)

Every system integrates against the dt the executor hands it (`Sim.Core/Kernel/SimContext.cs:37-41`
— `DtDays` "turn length in whole days", `DtYears` "every rate integrates against this (law 3)").
MEASURED: 77 `dtYears`/`DtDays` lines (comments included; 45 in code) in 20 files under
`Sim.Core/Systems`; every live integration site reads `ctx.DtYears` fresh per step. **One documented
seam assumes dt is constant across adjacent turns** and was found by verification, not by the lane:
`AppropriationSystem.cs:141-146` takes `DeficitRatio × DemandUnits` from the PREV
`ConsumptionDeficitRow`, a quantity integrated under the *previous* turn's dt with no dt on the row
(`WorldState.cs:297`) — under any dt change the seam fires at every band edge, and its fix is a
schema bump (row 16 below). The campaign
already runs six different dts (10 → 5 → 3 → 2 → 1 → 0.5 sim-years, `Sim.Data/content/era-pacing.json`)
and pins continuity across the 10→5 flip (`Sim.Tests/Systems/DemographyRetuneTests.cs:729`,
"This test exists forever" `:737`). The one sub-turn subdivision in the tree — the 0.5-year demographic
micro-step — is internal to one system's `Step`, documents the non-multiple case ("one shorter final
micro-step; deterministic", `Sim.Core/Systems/Demographics/DemographicsSystem.cs:26-30`, loop
`:285-289`) and needs nothing from the executor. CR-006 §3 records "turn duration may vary by
historical period" as ALREADY SATISFIED (`docs/adr/cr-006-continuous-time-and-campaign-epoch.md:169-172`).

### 2.2 What is PINNED, and by which frozen document

| pinned item | frozen statement | tree |
| --- | --- | --- |
| **dt is a function of the turn-START DATE via the era table** | kernel contract §3.4 "dt from the era table" (`docs/m0-kernel-spec.md:70`); D-006 "the table indexes on world date" (`:25`; D-006 inside the S8 perimeter, `docs/spine-s8-governance-freeze.md:16`); ADR-002 consequences (`docs/adr/adr-002-integer-day-clock.md:26-30`) | `TurnExecutor.cs:53-56` (the pinned ordering), `:86` `dtDays = _eraTable.DtDaysAt(prev.Clock.SimDays)`; `EraTable.cs:27-28` "kernel rule: dt is selected by the date at turn start" |
| **the turn is ATOMIC — the only control boundary** | kernel §3.2–3.3 double buffer + fixed pipeline (`m0-kernel-spec.md:66,68`); Spine `:31` `step(prev, my_tables_rw, orders, rng_stream, dt)`; S8 `:15` | `TurnExecutor.cs:96-108` — no exit between (3) run systems and (4) advance clock; the only hook, `ITurnObserver.OnPhaseState`, is typed `IReadOnlyWorldState` and documented "A READ-ONLY WINDOW AND THAT IS LOAD-BEARING" (`:42-49`; ADR-007 `:15-20`, changes "require a director-approved ADR" `:27`) |
| **orders apply at integer-turn granularity** | kernel §3.9 `{turn, actorId, orderPayload}` (`m0-kernel-spec.md:88`); T1.9 precedent (`CLAUDE.md:39`) | `OrderLog.cs:70` `OrderRecord(long Turn, int ActorId, OrderKind Kind, int TargetId, double Amount)` — "the OrderRecord shape … is fixed" (`:4-5`); `BatchFor(prev.Clock.Turn)` (`TurnExecutor.cs:100`); wire format `IoVersion = 1` carries no dt and no sub-turn coordinate (`:125-142`) |
| **band edges are turn boundaries; dt is whole days** | ADR-002 `:26-30` | `EraTableLoader.cs:106-109` "band edges must land exactly on turn boundaries (ADR-002)"; `:95-101` whole-day dt; `Sim.Tests/Kernel/EraTableTests.cs:30` `FullCampaignTickThrough_Exactly1630Turns_LandsOn2100Exactly` |

Two further ratified statements bear on the shape of any variable-dt design and are not the same
as Draft-1's: the Spine's **dt-authority rule** — "global dt is set by the world's *most advanced*
polity's era band" (`docs/civ-sim-architecture-v3-outline.md:34`; D-006 "from M6",
`m0-kernel-spec.md:25`) — is still an era-BAND lookup keyed to a computed era, not to strategic
circumstance; and the Spine's **crisis zoom** ("era-scaled dt + crisis zoom, retained", `:34`;
D-006 "Crisis-zoom subdivision is an M4+ feature", `:25`) is a SUBDIVISION of the band dt with band
edges preserved. Neither is implemented (INFERRED from `TurnExecutor.cs` in full;
`docs/m4-exit-inventory.md:228-230` "The 10-year atomic turn, the 0.5-year demographic microsteps
and the deterministic fixed pipeline are unchanged").

### 2.3 The register — every assumption a variable-Δt / mid-interval design breaks or strains

| # | assumption in the tree | location | breaks under | severity |
| --- | --- | --- | --- | --- |
| 1 | dt is a pure function of the turn-start date | `TurnExecutor.cs:86`; `EraTable.cs:27-40`; `m0-kernel-spec.md:25,70` | any dt chosen by state, event or player | **frozen** (kernel §3.4, D-006) |
| 2 | dt is fixed BEFORE the order batch is read | `TurnExecutor.cs:86` (dt) vs `:100` (orders) | a player order that sets the current turn's length | frozen ordering |
| 3 | band edges are turn boundaries; the campaign is exactly 1,630 turns | `EraTableLoader.cs:106-109`; ADR-002 `:26-30`; `EraTableTests.cs:30,51-53` | any turn whose length ≠ band dt: a shortened turn leaves `SimDays` off the band lattice; the next lookup still works (half-open bands, start day only — `EraTable.cs:31-40`) but then (a) the NEXT full-dt turn can straddle a band edge, integrating band-i's dt into band-(i+1) time — a silent Law 3 drift, since dt is read at turn START; (b) the campaign no longer lands exactly on `CampaignEndDay`, where `DtDaysAt` throws (`EraTable.cs:38-39`); the 1,630 pin dies. A subdivision that RE-SYNCS to the band grid preserves all three | ADR-002 consequence; Law 3 |
| 4 | applied dt is whole days, ≥ 1 day (the founding clock stores `DtDays = 0` before any turn, and that 0 is hashed) | `SimClock.cs:10`; `EraTableLoader.cs:95-101`; `CanonicalSchema.cs:158-161` | fractional-day or "instant" boundaries | ADR-002 |
| 5 | turn index ↔ calendar date is a fixed map | goldens `SnapshotTests.cs:87,334`, `DrivenGoldenTests.cs:106`; CI `--turns 300/400` (`ci.yml:93-162`); the CR-001 detonator's windows assume the gate at TURN 250 (`DemographyRetuneTests.cs:743-745`; a second hard-coded "at turn 250" at `FamineScenarioTests.cs:767`); battery horizon `RunCanonical(1, 650)` (`CalibrationBatteryTests.cs:589`) | any interrupt-created or circumstance-chosen turn shifts every date-at-turn-N. Under CR-006 Option A EVERY golden re-pins (`cr-006:70-72`); under Option C only worlds that trigger an interrupt re-pin (`:89-91`) — plus the 12 `IntegratedPinAttribution` stripped-control constants, which must be re-MEASURED, not re-typed. Year-windowed tests can go vacuous | test/golden blast radius (REFACTOR-class) |
| 6 | dt is recoverable from `(seed, orderLog)` | `OrderLog.cs:125-142` (no dt field); `m0-kernel-spec.md:88` replay(seed, orderLog); `SessionManifest.cs:46-77` and `SessionTrace.cs:34` carry no dt either; only the snapshot's `Clock.DtDays` (`CanonicalSchema.cs:150,161`), the JSONL observers, and the forensic run record's era-band table (`ForensicSession.cs:70-73`) echo it — the latter reconstructs the sequence only under the CURRENT rule | any dt that is not a deterministic function of state — CR-006 `:100-103` "the shortened dt must be recorded as data, or replay diverges"; a STATE-computed dt (crisis-zoom reading) is recoverable and this row is moot for it. Note the pin that Option C needs is a dt-STAMPING pin — "order at turn t → step t→t+1" is unchanged — whereas Option A needs a new order-delivery pin (`cr-006:49-56`) | **frozen** (§3.9 replay contract) + open CR-006 |
| 7 | the turn is atomic; no control exit inside `Step` | `TurnExecutor.cs:96-108`; `ITurnObserver` `:42-49`; ADR-007 | any mid-interval hand-back (C25; C21 player-in-loop) | **frozen** (kernel §3.2–3.4) + open CR-006 §1 |
| 8 | orders carry one integer TIME coordinate — but `Append` preserves insertion order within a turn and `BatchFor` returns it in log order, and TargetId bit-packing is an existing payload precedent | `OrderLog.cs:70, :53-55, :98-121`; `:27` (`settlementId × 8 + sectorId`); `CLAUDE.md:39` | an order issued at a mid-turn DATE has no representable stamp (CR-006 `:49-51`) — that is the hand-back case; a per-PULSE order sequence inside a turn CAN ride the existing log under a new `OrderKind` (verified against the code; the earlier "sub-turn coordinate required" reading was overstated). Either way a new delivery semantic needs its own turn-exact pin | **frozen** (§3.9) + T1.9 precedent |
| 9 | the demographic micro-step composes identically at every dt | `DemographicsSystem.cs:24-30`; ADR-011 | dt not a multiple of 0.5 yr (still deterministic; the by-construction invariance and the exact-equality pins cover 10/5/2.5 only) | ADR-011 scope |
| 10 | at most one disaster onset per turn; famine classification is dt-dependent (accepted artefact) — **INERT on this tree**: `hazardPerYear` ships at 0.0 (`Sim.Data/content/sim.json:235`) | `DisasterSystem.cs:50-54,104,141-143`; `DisasterSystemTests.cs:379`; CR-016 item D | latent, not live: if the mechanism is armed AND dt is chosen by circumstance, famine OUTCOME becomes a function of the dt choice (Law 2 strain). Recorded; not a driver of any classification below | open CR-016 (latent) |
| 11 | rails and bounds are sized for dt ∈ [0.5, 10] | `SimConfig.cs:299-302, 328-334`; `ProductionTests.cs:817-821`; `MigrationSystem.cs:381` (EMA saturates at dt ≥ window) | dt > 10 — a "long peaceful stretch" turn | TUNE-level, but real |
| 12 | turn-stamped state exists (`FoundedTurn`, `LastRecomputeTurn`) but **no sim logic reads either as a duration** — readers are observability only | `WorldState.cs:64`; `CatchmentSystem.cs:140` | nothing breaks; a future reader that treats a turn stamp as elapsed time would (the shipped discipline for durations is BY DIMENSION in years — `DisasterRow.RemainingYears`, `WorldState.cs:769-771`) | strain only |
| 13 | one `Step` per End Turn / hash-log line / trace line / `TurnRecord` | `UiSession.cs:248-257`; `Sim.Cli/Program.cs:201-205`; `SessionTrace.cs:51-57`; `ObservationLog.cs:96-99` contiguity guard | unaffected by variable dt (the turn stays atomic); broken by mid-turn hand-backs | as #7 |
| 14 | RNG draw count per turn is fixed per system×settlement | e.g. `DisasterSystem.cs:110-112` "ALWAYS taken, even when unused" | a different turn count over the same years changes the draw sequence — determinism intact, goldens move | expected |
| 15 | `Clock` serialises as one (Turn, SimDays, DtDays) per turn | `CanonicalSchema.cs:150, :158-161` | pulses or sub-steps inside a turn have no clock slot; sub-state must be an owned table under the Spine sub-step rule (`outline.md:33`) | schema |
| 16 | `AppropriationSystem` reads a PREV stock integrated under the previous turn's dt, with no dt on the row | `AppropriationSystem.cs:141-146`; `WorldState.cs:297` | any dt that differs between adjacent turns (already true at the six band edges; universal under variable dt) — the fix is a `DtYears` field on the row, i.e. a schema bump | strain today; REFACTOR floor for C01 |
| 17 | three shipped dt-sensitivities are RECORDED residues rather than invariants: tool wear (cumulative wear dt-sensitive), the price step's Euler under-integration ("over 100 sim-years, dt 10/5/2.5/1 …"), and the harvest decade variance (CR-015 G1, escalated) | `docs/queue.md:37-41`, `:164`; `docs/adr/cr-015-famine-is-exceptional.md` (G1) | a circumstance- or player-chosen dt turns each residue into a lever the player can pull by choosing the turn length (Law 2 strain) | queue residues |
| 18 | the RNG registry is keyed by (system, region) only, never by turn or date, and stream state is persisted in `RngStreams` | `RngRegistry.cs:66-69`; `CanonicalSchema.cs` RngStreams block | nothing — variable dt needs no RNG rework; replay safety reduces entirely to whether the dt SEQUENCE is reproducible from (seed, log) (row 6) | none (recorded as a strength) |

**Already dt-robust and NOT breakage** (recorded so nobody lists them): calibration windows are
keyed by sim-year, not turn (`AutoplayMetrics.cs:112-134`); `SettlementVitalsRow.DtYears` caches the
dt a row was computed over precisely so an era-boundary reader forms per-year rates correctly
(`WorldState.cs:536-542`; `FoodHeadroom.cs:36-38`); AR(1) harvest weather is variance-corrected in
dt (`HarvestWeatherSystem.cs:138-139`); the exponential sinks (`MigrationSystem.cs:227,675`;
`DisasterSystem.cs:104`); `DisasterRow.RemainingYears` is "persisted BY DIMENSION, because a disaster
can outlast a late-era turn" (`WorldState.cs:769-771`). These are the shipped patterns any new
stock must follow: **per-year rates plus dt, never per-turn amounts** (Law 3).

### 2.4 What follows for the Draft-1 concepts

- **C01 (variable strategic calendar resolution)** collides with rows 1–6 and is, in the tree's own
  vocabulary, CR-006 §1.4 **Option C** — "interrupt as a turn boundary … dt becomes event-dependent,
  so it must be recorded in the order log for replay" (`cr-006…:85-91`), which is OPEN and unruled.
  Verification narrowed the driver: the frozen texts themselves ANTICIPATE two variable-dt shapes —
  crisis-zoom subdivision (Spine `:34`; D-006 `:25` "an M4+ feature") and a computed-era dt authority
  ("from M6", `:25`) — so a subdivision reading is a REFACTOR of `TurnExecutor.cs:86` under the kernel
  freeze (director-signed ADR), not a contradiction of §3.4/D-006. What makes C01 CONFLICT is (a) the
  open-CR arm and (b) the two variants no frozen text anticipates: a **player-chosen** dt (a new
  external input with no `OrderKind` and a fixed wire format) and a **lengthened** turn beyond the
  band dt (the price rail is "SIZED AGAINST THE COARSEST SHIPPED dt", `SimConfig.cs:298-302` — a calm-
  period dt > 10 can breach a stability bound, not merely leave the tested set).
- **C25 (mid-interval interrupts)** collides with rows 7–8 and 13 and is CR-006 §1 verbatim. Under
  Option C it collapses into C01; under Option B it becomes presentation only; under Option A it
  re-opens dt-correctness across the whole simulation, which CR-006 recommends against.
- **C21 (War Pulses)** in D-011's own definition — "player issues orders to formations → both sides'
  orders resolve simultaneously … next pulse", "player battle orders append to the order log per
  pulse", and the in-phase trigger "armies contact during the military phase → prompt"
  (`docs/d011-battle-layer-addendum.md:10,28,34`) — collides with rows 7 and 15, not with row 8:
  verification showed per-pulse orders can ride the existing log (insertion order preserved; new
  `OrderKind`), and a boundary battle session (contact detected in `Step t`, commands at the boundary,
  pulses re-executed inside `Step t+1` from frozen turn-start state) satisfies D-011 §2 without kernel
  re-entrancy. What remains is D-011 `:34`'s LITERAL in-phase prompt — which is CR-006 §1 ground — versus
  a one-turn lag D-011 does not state; and M6 placement. Auto-resolved pulses inside one `Step`
  collide with nothing (the demographic micro-step is the precedent).
- Every other concept is indifferent to dt provided it is written as a per-year rate (row set 2.3's
  robust list), which is Law 3 and not a new constraint.

## §3 THE 27 CONCEPTS — classification table

One level per concept, at the **most severe verified reading of the concept as named**; readings that
would classify differently are in §4. "Decision" points into §5. Locations are the three-to-six that
carry the verdict; the full verified location lists (627 unique citations, each re-opened) are in the
reconciled working papers.

**Tally: CONFLICT 18 · REFACTOR 2 · EXTENSION 7 · COMPATIBLE 0.** The zero is not a judgement on Draft
1; it is what the scale produces when 13 of 27 concepts are the content of an OPEN CR's contested
ground (CR-005) and the rest touch the atomic turn (CR-006) or a law as named.

| id | concept | class | reading classified | driver | key locations | decision |
| --- | --- | --- | --- | --- | --- | --- |
| C01 | variable strategic calendar resolution | **CONFLICT** | dt chosen by circumstance or player, incl. lengthened turns, instead of by the era band at the turn-start date | OPEN CR-006 §1 Option C ground; §3.9 replay for any dt not derivable from state; player-chosen / lengthened variants anticipated by nothing frozen | `TurnExecutor.cs:53-56,:86`; `EraTable.cs:27-40`; `OrderLog.cs:70,:134-142`; `cr-006:85-91,:100-103`; `m0-kernel-spec.md:25,:70,:118` | DD-01, DD-02 |
| C02 | global Age progression | **CONFLICT** | a world-level Age in state that advances and is consulted | Law 4 — on this tree the only advancing producer is the date band; the computed producer is M7 content (CR-005) | `CLAUDE.md:19`; `outline.md:22,:110`; `EraTable.cs:12,:27-40`; `d011:62` | DD-04, DD-03 |
| C03 | Age spectrum Early/Mid/Late | **CONFLICT** | thirds of C02's Age | inherits C02; thirds of a date band consumed = a calendar sub-gate | as C02 | DD-04 |
| C04 | milestone-based Age advancement | **CONFLICT** | Age advances on achievements AND is read by systems | Law 4 / D-040 B3 ("a calendar gate wearing a tree"); computed-label producer is M7 (CR-005); the frozen corpus already era-gates un-CR'd (D-011 :13/:45/:66, D-009 :12) | `d040:59-70,:223-224`; `d011:13,:45,:66`; `d009-d010:12`; `Predicate.cs:10-26`; `Variables.cs:93` | DD-04, DD-03 |
| C05 | partial Age buffs and penalties | **CONFLICT** | fractional standing modifiers keyed to Age standing | Law 4 (the INPUT is an era label — inescapable); Law 2 secondary (escapable by re-housing the coefficient, and then it is no longer an Age buff); D-035-C carrier test refuses it | `CLAUDE.md:17,:19`; `outline.md:20,:22`; `d039:37-38`; `d035:91-93`; `d042:90-91,:157-158` | DD-04 |
| C06 | civilization Ages differing from global | **CONFLICT** | a per-polity Age as stored, consulted state; at its most benign a computed per-polity label | Law 4 / Law 2 if consumed; the label reading is the named M7 deliverable ("civilizations pulling apart in time") behind CR-005 and closed D-006 ("from M6 when eras diverge"); no per-polity substrate exists | `outline.md:22,:34,:110`; `m0-kernel-spec.md:25`; `WorldState.cs:394,:694-704`; `d042:157-158` | DD-04, DD-03, DD-12 |
| C07 | catch-up through foreign exposure | **CONFLICT** | a diffusion flow into a laggard proportional to computed exposure and a stock difference | OPEN CR-005 (knowledge-milestone content); mechanism unowned (G-09/G-10); no carrier carries knowledge; Age-gap or laggard-bonus forms fail Law 4 / Law 2 / D-035-C | `cr-005:3`; `m5-research…:92,:95,:106-108`; `WorldState.cs:459`; `TradeScope.cs:59-64`; `d042:172-174` | DD-03, DD-05 |
| C08 | knowledge stocks and flows | **CONFLICT** | a polity-keyed accumulating reserve fed by research and diffusion whose thresholds change capability | CR-005 (the CR's literal subject); the unruled Law 4 / D-040 B3 fence on threshold → capability. NOT drivers: accumulation is RULED legal (D-042 §9.4-9.5, §15); storage as a `double` is COMPATIBLE | `cr-005:3,:149-152`; `d042:148-158,:164-175,:292`; `d040:59-64`; `WorldState.cs:394,:556-560` | DD-03, DD-05, DD-12 |
| C09 | parallel research | **CONFLICT** | concurrent lines of inquiry under an allocation directive (new OrderKind → persistent per-polity row → knowledge stock) | CR-005; whether a line COMPLETES and grants (D-040 B3 vs the unratified placeholders). Parallelism itself is RATIFIED (D-042 §9.3, §12) — EXTENSION once placed | `cr-005:3`; `d042:103-106,:167,:200-202`; `OrderLog.cs:4-5,:9-12`; `OrderValidation.cs:42-43` | DD-03, DD-05 |
| C10 | knowledge diffusion | **CONFLICT** | knowledge moving between polities along contact | a RATIFIED deliverable of the knowledge milestone (Spine :84/:110; D-011 :62) → CR-005 ground; carrier unruled (two design lanes disagree on whether a traded good carries a technique) | `outline.md:84,:110`; `d042:172-174`; `TradeScope.cs:33-49`; `d035:91-92` | DD-03, DD-05 |
| C11 | technology transfer | **CONFLICT** | a "technology" object moving between polities | the object is defined nowhere (G-14); as a satisfied predicate it cannot move and collapses into C10; as a stored grant it is B3's banned shape; placement CR-005 | `d042:164`; `d040:59-64`; `ProductionSystem.cs:388-396` | DD-05, DD-03 |
| C12 | institutional emergence | **CONFLICT** | institutions arise from computed state (the D-018 "emerge, not unlocked" shape) | three rulings place institutions at three milestones (D-035-C path 6 M5 by name; Spine M7; D-011 M8); CR-010 (what an institution IS) never written; frozen D-018 predicates read an institution operand nothing publishes | `d035:86`; `outline.md:85`; `d011:63`; `d018:25-26`; `milestone-architecture-governance.md:179-181`; `WorldState.cs:696` | DD-11, DD-03 |
| C13 | university specializations and maturation | **CONFLICT** | a university that specialises by domain and matures into greater effect | downstream of C12 (CR-010) and C08 (domains) and a literacy publisher nobody owns; time-keyed maturation is the defect CR-001 CLOSED against; D-038 H2/H5 bind the display side | `cr-001:3-5`; `d018:26`; `Variables.cs:93`; `WorldState.cs:755,:769-771`; `d038-visual-target.md` H2/H5/H7 | DD-11, DD-03, DD-05 |
| C14 | construction capacity / points | **REFACTOR** | construction labour banked across turns and spent with stored partial progress | reverses M4-D's "gate, not rate / no progress field" — a packet ruling in code docs and certification only, nothing frozen; the gate is itself a live Law-3 strain under the shipped era table | `WorldState.cs:737-742`; `CanonicalSchema.cs:86-90`; `ConstructionSystem.cs:38-48,:136-141`; `GoodsConfig.cs:74-76`; `queue.md:1310-1312` | DD-08 |
| C15 | endogenous building construction | **EXTENSION** | buildings queued/completed when a data-declared predicate over published state holds, with no order | additive: a predicate on `ConstructionProjectEntry` (recipes already carry `requires`; projects do not) or a new system; touches kernel §3.1 ownership by the sanctioned-share route; dwellings and dirt paths are already endogenous built stocks | `SystemCatalog.cs:31-37,:190-198`; `GoodsConfig.cs:33-35,:81-85`; `HousingSystem.cs:150-152`; `PathBuildSystem.cs:207-220`; `Levers.cs:30-32` | DD-08 |
| C16 | unit recruitment / training | **EXTENSION** | a Recruit order moving adults from `BucketRow`s into a conserved formation row via `Ledger.Transfer`; training a per-year rate or an in-equation coefficient | Law 1 by construction (the `NotableLifecycle.Born` shape); frozen D-011 §2, D-037 A1, D-039 B1 conformed to; new OrderKind + table + system + ADR. Constraint: a LEVY is not the D-018 Soldiers class (`d018:24`); placement at M5 is the CR-005 shape (DD-10) | `NotableLifecycle.cs:65-67`; `WorldState.cs:644-648`; `d011:26-27`; `d037:14-16`; `d039:44-49`; `d018:24` | DD-10, DD-12 |
| C17 | persistent veterancy | **CONFLICT** | a stored per-formation experience multiplier that outlives the people who earned it, on an entity proposed pre-M5 | Law 2 (D-040 C3's "DO NOT ADD A loyalty FIELD" precedent); both carrier and field are M6 content under closed D-011 §6 / GOV-2 §1b / m4-spec 1.2 — "additive table" does not lower the level when the entity belongs to a later frozen milestone | `CLAUDE.md:17`; `d040:105-107`; `d011:27,:36-37,:61`; `m4-spec.md:36,:49`; `m4-pre-spec-dependencies.md:102-103`; `WorldState.cs:630-633` | DD-09, DD-10 |
| C18 | Action Capacity | **CONFLICT** | a per-polity count of actions per TURN — the interval is the turn, which runs 10 y → 0.5 y | Law 3 as named ("Never hardcode per-turn amounts"; kernel §3.4 "fails review"). A per-sim-year replenishment on computed state (D-039 A5) in a PolityId-keyed table, gated at consumption, is EXTENSION | `CLAUDE.md:18`; `m0-kernel-spec.md:70`; `outline.md:21,:109`; `d039:37-38,:175-176`; `OrderValidation.cs:16-18`; `RevoltSystem.cs:6-8` | DD-07, DD-12 |
| C19 | continuous-distance movement | **EXTENSION** | formations move a per-sim-year speed × dt along the lattice + network, holding a graph position | nothing positioned moves today; everything needed is additive and D-009 REQUIRES this shape ("One object, three jobs"); D-040 C6 forces travel cost as the metric. Placement of armies: DD-10 | `WorldState.cs:64`; `MigrationSystem.cs:882-884`; `Pathfinder.cs:56`; `LatticeGeometry.cs:71-76`; `d009-d010:17,:51`; `d040:130-131` | DD-10, DD-12 |
| C20 | infrastructure networks | **REFACTOR** | the D-009 multi-modal graph with per-edge attributes and edges upgraded over time | widening `NetworkEdgeRow` is the scale's own REFACTOR example; "upgraded over time" contradicts "Cost … fixed when the edge is built", the static pathfinder weight and the revision-only recompute (D-016 is OPEN, so not CONFLICT). "era-gated" in D-009 ¶2 must be read as predicate-gated | `WorldState.cs:54-61,:71`; `Ids.cs:68-72`; `Pathfinder.cs:386`; `CatchmentSystem.cs:91,:183`; `PathBuildSystem.cs:207-220`; `d009-d010:12,:17` | DD-04(i), DD-08 |
| C21 | War Pulses | **CONFLICT** | a war resolved as pulses INSIDE one turn with the player issuing orders between pulses | D-039 E3 "NOT A FINER-GRAINED TURN" (closed ruling — contradicted whatever CR-006 decides); any in-`Step` hand-back is CR-006 §1; D-011 :34's literal in-phase prompt vs a one-turn-lag boundary session is unruled; TacticalResolver is M6. NOT a demonstrated D-011-vs-kernel contradiction (per-pulse orders ride the log) | `d039:143-145`; `d011:10,:28,:34,:61`; `TurnExecutor.cs:96-109`; `OrderLog.cs:98-121`; `cr-006:33-41` | DD-06, DD-01 |
| C22 | battle radius and reinforcement | **EXTENSION** | formations within a travel-cost radius of a contact join; reinforcements move to a standing engagement | frozen D-011 §3 already supplies trigger, "commit reserve" and command radius INSIDE the Lanchester equation; the metric is forced to travel cost (D-040 C6; the `KmPerNode` gate). Mid-battle arrival with player input inherits C21 | `d011:13,:27,:34,:37`; `d040:130-131`; `CatchmentSystem.cs:47-54`; `check-banned-constructs.sh:80-83` | DD-06 |
| C23 | cross-turn wars | **EXTENSION** | persistent war state between polities spanning turns, resolved at turn granularity | D-039 E2 "A WAR IS NOT A BATTLE, AND NEEDS ITS OWN LAYER … new work"; new tables + system + OrderKind; durations BY DIMENSION in years, stamps in `SimDays`. Ownership contested three ways (Spine M9+ / D-011 §5 M6 / D-039 F3 M6) | `d039:139-141,:175-176`; `d011:45`; `WorldState.cs:769-771`; `SimClock.cs:10`; `outline.md:90` | DD-10 |
| C24 | medical / logistical recovery | **EXTENSION** | (military) attrition and recovery as per-year rates over conserved people and supply; (economy) a bound Health need feeding demography via D-035-C path 5 | frozen D-011 §2/§3 and D-035-C path 5 anticipate both; no frozen text mentions wounded/medical; a fixed "recovery bonus" is Law 2, "heal X % per turn" Law 3; an institution-carried satisfier at M5 is CR-005-shaped | `d011:26-27,:36`; `d035:85,:91-96`; `needs.json:25-27`; `NeedsConfig.cs:73-79`; `DemographicsSystem.cs:228` | DD-10, DD-11 |
| C25 | relevant mid-interval interrupts | **CONFLICT** | the interval stops early on a decision event and control returns before its nominal end | CR-006 §1 verbatim and OPEN: the atomic turn has no yield point; the only hook is read-only; a mid-turn order has no coordinate; the M5 startup fence says "no mid-turn handbacks" | `cr-006:11,:33-41`; `TurnExecutor.cs:42-49,:96-109`; `adr-007:26-28`; `m4-exit-inventory.md:321-322`; `d042:188-190` | DD-01 |
| C26 | predictive end-of-turn advisories | **EXTENSION** | read-only advisory lines derived from a PROJECTION of the completed Next state at the existing boundary | no projection producer exists; a clone-stepped `TurnExecutor.Step` is side-effect-free today, but the observability contract's five-kind rule (RECOMPUTED = a PUBLIC static function) is a ratified-class packet record that must be extended by ADR; Spine Law 7 symmetry and Law 6 (no fabricated ranges) bind | `observability-architecture.md:28-30`; `TurnExecutorTests.cs:119`; `WorldState.cs:1151`; `outline.md:24-25`; `m5-temporal…:144-148` | DD-01 (only if it stops the flow) |
| C27 | trade / open-border knowledge diffusion | **CONFLICT** | diffusion carried by realised foreign trade and by an "open borders" relation | CR-005 (knowledge) plus the M8 diplomacy slot for the relation; D-037 C7 makes recognition govern trade access; T4.6 shipped the predicate and deliberately no rule; the only partner record is the per-turn `TradeFlowRow`; no border object exists | `cr-005:3`; `d011:63`; `d037:119-122`; `t4.6-foreign-trade-decision.md:118-119`; `WorldState.cs:459,:665-669`; `TradeScope.cs:45` | DD-03, DD-05 |

## §4 THE READINGS LADDER — what each concept becomes under each reading

The table in §3 is the most severe reading. Because Draft 1 was not available, the Director's fastest
correction is to say which reading was meant; this ladder gives the level for each. "Placement" means
the milestone-ladder / open-CR ground of DD-03 or DD-10; "law" means a CLAUDE.md law as named.

| id | as named (§3) | benign reading and its level | what survives once placement is ruled |
| --- | --- | --- | --- |
| C01 | CONFLICT (CR-006; player/lengthened) | crisis-zoom SUBDIVISION, state-computed, dt ≤ band dt, re-syncing to band edges → **REFACTOR** + kernel-freeze ADR; "dt differs per era" → already shipped | REFACTOR floor for any variant: every turn-keyed golden, the 1,630 pin, the turn-250 windows, the Appropriation seam (schema bump), three queue dt-sensitivities |
| C02–C04 | CONFLICT (Law 4 if consumed; M7 if computed) | a computed LABEL, output only (the Spine's "computed era labels") → COMPATIBLE shape, **M7 content**; the date-band NAME shown in the HUD → COMPATIBLE today (a producer exists: `EraTable.Band.Name`) but it is a calendar label under the name the Spine reserves for a computed one | EXTENSION at M7 (derived reading, no serialized row) |
| C05 | CONFLICT (Law 4 + Law 2 + carrier test) | none as named; a coefficient inside a domain's own equation driven by a computed stock that correlates with the Age → legal, but then not an Age buff | nothing keyed on an Age |
| C06 | CONFLICT | per-polity computed label → M7 content; substrate: additive polity-keyed row → EXTENSION (v26/v27); widening `VariableRow` → REFACTOR; as dt input → REFACTOR of kernel §3.4 | EXTENSION (label) / REFACTOR (dt authority) |
| C07 | CONFLICT (placement) | diffusion flow from measured exposure (`TradeFlowRow` × `TradeScope`, `SettlementDistances` × `Controls`) into a knowledge stock → EXTENSION on C08 | EXTENSION; never a laggard rate bonus |
| C08 | CONFLICT (placement + fence) | reserve that only feeds rates/allocations, never gates → EXTENSION; pure predicate + latch with no stock → COMPATIBLE (but does not implement D-042 §9.5) | EXTENSION: one table (v26/v27, not `SystemId 22`), one system, a polity-scoped variable substrate |
| C09 | CONFLICT (placement + completion) | allocation directive → new OrderKind + per-polity row → EXTENSION (parallelism is RATIFIED) | EXTENSION |
| C10, C27 | CONFLICT (placement; C27 also M8 diplomacy) | a transmission term in the holding system's rate equation reading existing trade/control tables → EXTENSION; a border relation → new (A,B) relation in the T4.3 shape + OrderKind, M8 material | EXTENSION on C08 |
| C11 | CONFLICT (object undefined) | technology = a satisfied predicate → collapses into C10 | as C10 |
| C12 | CONFLICT (CR-010; three placements) | institution as a published scalar / predicate operand (meaning M-3) → COMPATIBLE today; a latched per-polity module → EXTENSION after the C06 substrate; a bonus bundle → Law 2 | EXTENSION after CR-010 |
| C13 | CONFLICT | a university as a published scalar with computed-input maturity inside the inquiry rate equation → EXTENSION after C08/C12 + a literacy publisher; a maturity scalar on `StructureRow` → REFACTOR-shaped; the map treatment → the inserted visual milestone (D-038 H3/H7) | EXTENSION |
| C14 | REFACTOR (banked labour) | capacity computation only → COMPATIBLE (shipped); a currency Ages can grant → Law 2 CONFLICT; substrate for a mid-turn completion trigger → CR-006 CONFLICT | REFACTOR |
| C15 | EXTENSION | an AI issuing `EnqueueConstruction` → COMPATIBLE; endogenous buildings WITH EFFECTS → a larger question (Structures are read by nothing; effects must enter the lever table) | EXTENSION |
| C16 | EXTENSION | class mobility into a D-018 Soldiers bucket → nearer COMPATIBLE but lawful only for a standing professional force; a strength `double` detached from people → Law 1; era-/tech-gated unit types → Law 4 | EXTENSION at the ruled military milestone |
| C17 | CONFLICT (Law 2 + M6 allocation) | experience as a property of the conserved people, diluted by flows → EXTENSION at M6; general experience on the notable → a frozen REQUIREMENT at M6; display-only badge → COMPATIBLE code on a token that is itself M6 | EXTENSION at M6 |
| C18 | CONFLICT (Law 3 per turn) | per-sim-year replenishment on computed state, PolityId-keyed, gated at consumption → EXTENSION (the Spine's M5 "authority/bandwidth economy"); "bandwidth" as bounded order vocabulary → already partly shipped (m3-spec D-032) | EXTENSION |
| C19, C22, C23 | EXTENSION | raster 2D position off the graph → CONFLICT (D-009); wars in TURN counts → Law 3 strain; mid-battle reinforcement with player input → C21 | EXTENSION at the ruled military milestone |
| C20 | REFACTOR | static side-table keyed by `NetworkEdgeId`, or new `EdgeTypes` rows laid by PathBuild → EXTENSION; per-settlement structures → CONFLICT (D-009); literal era-gated edges → Law 4; route-bearing player order → wire-format REFACTOR | EXTENSION under the side-table reading |
| C21 | CONFLICT (D-039 E3; CR-006) | N auto-resolved pulses inside one `Step` → COMPATIBLE; boundary battle session with the kernel's one-turn lag → EXTENSION; D-039 E3's decision-point campaign → EXTENSION (C23's shape) | EXTENSION at M6 |
| C24 | EXTENSION | goods-carried Health satisfier via the existing `basket` source → clear now; institution-carried → CR-005-shaped | EXTENSION |
| C25 | CONFLICT (CR-006) | boundary-evaluated detection + advisory, no early stop → it is C26 | — |
| C26 | EXTENSION | an advisory that only reads stored records or existing public static functions → COMPATIBLE; stored projections → EXTENSION; sampled ranges → a registered RNG stream; an advisory that STOPS the flow → C25 | EXTENSION (ADR on the observability field kinds) |

## §5 CONFLICTS REQUIRING DIRECTOR DECISIONS (deliverable 3)

Twelve decisions cover all eighteen CONFLICT rows; two are process, not architecture. Order is the
dependency order of §8 — DD-03 and DD-01 gate the most. Each names the frozen ground it sits on so the
ruling can be recorded where it belongs (a CR ruling, a Law reading, or an append-only note).

| # | decision | concepts unblocked | frozen ground |
| --- | --- | --- | --- |
| **DD-03** | **Rule CR-005** (A renumber / B insert / C architecture-only packet in M5; the CR's own crux: must M5 be substantively ABOUT learning and change, `cr-005:149-152`), **together with** the Ladder-A (Spine M6/M7) vs Ladder-B (D-011 §6 M7/M8) reconciliation GOV-2 §1c records as REQUIRED and unperformed — superseded wholesale or line by line? **And the provenance question:** `origin/m5-full-build`'s CR-005 reads "RULED 2026-09-05 — OPTION C ACCEPTED"; `main`'s reads OPEN. Did that ruling happen? Only a Director statement or a merge settles it. | C02–C04, C06–C13, C24, C27 | ladder order (S8 `:17`); D-011 §6 `:62-63`; Spine `:84-86, :110-111`; OPEN CR-005; `CLAUDE.md:28` |
| **DD-01** | **Rule CR-006 §1** — A (sub-step), B (date-stamped presentation, act at the next boundary), or C (interrupt-as-boundary with the shortened dt recorded as data). One blast-radius item CR-006 omits: Option C breaks ADR-002's "band edges land exactly on turn boundaries" unless the shortened turn re-syncs to the band grid (§2 row 3). | C25, C01, C21; C26 and C14 only under their interrupt readings | kernel §3.2–3.4, §3.9; ADR-007; OPEN CR-006; `m4-exit-inventory.md:321-322` |
| **DD-02** | **C01's scope and dt-as-data.** Is "variable strategic calendar" (a) crisis-zoom subdivision (REFACTOR + kernel-freeze ADR), (b) the dt-authority rule (needs a computed per-polity era → DD-03), (c) event-dependent dt (= CR-006 C → DD-01), or (d) player-chosen / LENGTHENED dt (anticipated by nothing; the 1,200–2,000-turn budget and the coarsest-dt rails are in play; new OrderKind + IoVersion bump + T1.9 pin)? If dt may depend on anything but the date, which artifact records it — order log, manifest, forensic record, or a new clock table (no existing artifact has a slot)? | C01 | kernel §3.4 / D-006 interim rule; §3.9; `m0:118`; ADR-002; Spine `:34` |
| **DD-04** | **Age: label or input?** Is a Draft-1 Age (global, per-civ, Early/Mid/Late, milestone-advanced) a computed LABEL that is output only, or a STATE any mechanism reads? If read, Law 4 and Spine S2.4 must be amended by CR. In the same ruling: (i) what "era-gated" means in closed D-011 §1 and D-009/D-010 §2 — the never-filed CR-009 ground D-040 reports and declines to rule; (ii) confirm no Age-keyed buff/penalty (C05) is admissible in any form, and whether "partial capability in DEGREE" is a project concept; (iii) may the HUD show the date-band NAME now; (iv) should a Law-2/Law-4 gate stronger than review be commissioned before M5 introduces any modifier-shaped or era-labelled quantity (none exists today). | C02–C07, C14(b), C20(d) | Law 4 (`CLAUDE.md:19`; `outline.md:22`); Law 2 (`:17`; `outline.md:20`); D-040 B3; D-039 A5; D-042 §8.4/§12/§5.2; D-035-C; closed D-011 `:13,:45,:66`, D-009 `:12` |
| **DD-05** | **The knowledge fence and the knowledge object.** May a threshold on the accumulator be a conjunct of a D-020-style capability predicate (the D-042 §8.2 chain) versus a direct grant (B3-banned)? Must the accumulator be able to FALL; are ordered thresholds tree edges; is a monotone-in-time accumulator a schedule (the unratified decision pass's recommendation) — and does narrowing B3 need a CR? Does a line of inquiry COMPLETE (G-02)? Typing: conserved `long` with a Ledger decay sink, or a `double` accumulation (a Law 7 design ruling)? Is there a TECHNOLOGY object at all (G-14)? Confirm `PolityId` keying (D-042 §9.2) and that a polity-scoped variable substrate is in scope. | C08–C11, C13 | D-040 B3; D-042 §8–§9, §15 (accumulation YES); Laws 4, 7, 1 scope; D-041 B1 |
| **DD-06** | **War Pulse definition and M6 placement.** D-039 E3's decision-point campaign model (EXTENSION, C23's shape) or a sub-turn cadence with the player in the loop (contradicts E3 whatever CR-006 decides)? Does D-011 §3's Command/Delegate prompt occur at the boundary after contact (one-turn lag; EXTENSION) or inside the military phase (CR-006 ground)? Is "War Pulses" at M5 a resequence of D-011 §6's M6? Does reinforcement (C22) include mid-battle arrival during pulses? | C21, C22 | D-039 E3 (closed); D-011 §1–§3, §6, §7; kernel §3.2–3.4/§3.9; Spine `:32-33`; OPEN CR-006 |
| **DD-07** | **Action Capacity vs Law 3.** Is the interval the TURN (a per-turn count — Law 3 conflict as named: rule an exemption or amend `CLAUDE.md:18` / `m0:70` / `outline.md:21`) or SIM-TIME (per-sim-year replenishment on computed state → EXTENSION)? Is it the Spine's M5 "authority/bandwidth economy" (which m3-spec D-032 already reads as a cap on decision surfaces) or D-039's M6-owned command capability? Enforcement per consuming system through a shared table (the `Controls` precedent) or in `TurnExecutor` (frozen kernel — director ADR)? | C18 | Law 3; Law 4 / D-039 A5; kernel freeze; Spine `:109`; D-039 F3; D-042 §3.2 |
| **DD-08** | **Construction points.** (a) the construction sector's adult-years and goods banked at turn granularity — REFACTOR of M4-D and remedial for the live dt strain (workshop threshold 10 adults Neolithic vs 200 Modern at default share; no test pins the gate's dt behaviour); (b) a stock Ages/buffs can grant — Law 2; (c) the substrate for a mid-turn completion trigger — CR-006? Attached (C15): reuse the M4-D queue or complete directly into `Structures`; all-or-nothing materials or Housing's remainder-banked shape; **are Structures to gain EFFECTS at M5 at all** (the exit record accepted "not repaired"; the unratified M5 roadmap assumes yes); should `ConstructionSystem` publish its labour draw? | C14, C15 | none frozen under (a); Law 2 / D-035-C under (b); CR-006 under (c); Law 3 (live strain); kernel §3.1 ownership for C15 |
| **DD-09** | **Veterancy carrier.** A stored per-formation modifier that survives the replacement of the people who earned it (needs a Law 2 ruling, as the rejected loyalty field did), or a derived coefficient over conserved people / the conserved notable (which frozen D-011 §2 already requires and §3 sanctions inside the Lanchester equation)? M6 content (EXTENSION at M6, D-011 §6 unchanged), pulled forward (a CR against D-011 §6 / GOV-2 §1b), or display-only with no state? | C17 | Law 2; D-040 C3; D-011 §2/§3/§6; GOV-2 §1b; m4-spec 1.2 / §2; D-038 F3/H7 |
| **DD-10** | **Placement of strategic armies, the AutoResolver and multi-turn sieges — rule once, in writing.** Frozen D-011 §6 `:59` and D-039 F3 place strategic war at M4; the ratified M4 spec is internally split (`m4-spec.md:40, :275` keep the AutoResolver; `:49` fences it to M6); the certification says "M6 by ruling" with no D-document or ADR recording the move. If Draft 1 schedules C16/C19/C22/C23/C24 at M5, that is closed-M4 or ruled-M6 content moved into the governing loop — every EXTENSION verdict in that group carries this latent placement conflict until ruled. Siege ownership is contested three ways (Spine `:90` M9+, D-011 §5 M6, D-039 F3 M6). Also: does frozen D-018 `:24/:63` already settle unit identity (professional soldiers = the Soldiers class; levies = formation rows from peasant buckets); and movement speed at dt = 10 (D-039 E6 defers to the M6 spec). | C16, C17, C19, C22, C23, C24 | D-011 §5/§6; ladder (S8 `:17`); D-039 F3/E6/B6; Spine `:81,:90,:108`; m4-spec `:40,:49,:275`; D-018 `:24,:63` |
| **DD-11** | **CR-010 — what IS an institution** (never written). Which of the ≥ 6 recorded meanings is canonical and can several coexist; which milestone owns institutions given D-035-C path 6 binds M5 by name while the ladders say M7 / M8; who publishes literacy/education (frozen D-018 predicates read an operand that does not exist; the registry has four names, none literacy); what is a school/university mechanically? | C12, C13, C24 | D-035-C path 6 (RATIFIED); Spine `:85`; D-011 §6 `:63`; D-018 `:8, :25-26`; D-042 §7.3/§5.2; `d042:300` |
| **DD-12** | **Schema-version collision and the canonical branch** (housekeeping). `CanonicalSchema.cs:77-85` records that the unmerged `origin/m5-full-build` also claims v25 (TaxPolicies, `SystemId 22`): "whichever lands on main first keeps v25 and the other becomes v26". Confirm which branch is canonical before any Draft-1 table bumps: a new polity-keyed table on `main` is v26 if `m5-full-build` never merges and v27 if it does, and must avoid `SystemId 22`. Every new table pays the ADR-005 checklist and adds to the per-turn clone (ADR-020's counts are already stale at 34/35 vs 40) — should the Draft-1 tables' clone cost be projected before ratification? | C06, C08, C09, C16–C19, C23, C24 | ADR-005; `CanonicalSchema.cs:77-85, :104`; `CLAUDE.md:38`; gov-4 three-state rule |

**Visual-foundation governance note (X-VIS-1, not a blocker).** D-038 F3/H7 say no asset work before the
inserted visual milestone; the Director's Phase 2 instruction asked for the visual foundation. The glyph
grammar shipped with this audit is primitives and composition rules, not object-tier assets, and it is
removable by deleting one folder (visual doc §9). Recommended: one append-only line on D-038 when the
architecture is ratified, recording that the grammar was established ahead of the milestone by Director
instruction.

**Housekeeping the Director owns (no ruling, but only the Director may move these):** `CLAUDE.md:10`
(the milestone line, one gate behind); `docs/current-state.md` (its header still re-derives to `dbef61a`
v24 — §7 rows 1–2); the fused `## 6.` heading in frozen `d011:54` (S8 §5 forbids editing it — a note in
the addendum is the only remedy); the stale code comments in §7 rows 8–9 (a packet, not this audit).

## §6 THE FROZEN-ITEM REGISTER AND THE OPEN CRs

What a CONFLICT verdict in §3 may rest on, and nothing else. Every line was opened (lane H, both
verifiers CONFIRMED the register; the three refutations were count corrections recorded in §7).

### 6.1 Frozen, by document and line

| item | frozen by |
| --- | --- |
| Laws 1–7 | `CLAUDE.md:16-22` (short form); Spine S2 `docs/civ-sim-architecture-v3-outline.md:19-28` — Law 2 precise form `:20` "Free-floating permanent auras ('+10% happiness') are illegal"; Law 4 `:22` "Capability derives from computed state; era labels are descriptive output only" |
| "The Spine, kernel contract, closed D-decisions, and milestone order are FROZEN" | `CLAUDE.md:25`; S8 §1 `docs/spine-s8-governance-freeze.md:13-17` — Spine S1–S5+S8; the kernel code contract (`ISimSystem`, `SimContext`, double-buffer model, `Ledger` API, RNG regime, snapshot/hash format, banned-constructs list); closed D-001…D-008, D-011, D-009/D-010, D-018; "the milestone ladder order M0→M11+ and each milestone's exit-criteria definitions" |
| Kernel contract §3.1–3.9 | `docs/m0-kernel-spec.md:52-88` — §3.2 double buffer `:66`; §3.3 fixed pipeline as data `:68`; §3.4 clock, "dt from the era table" `:70`; §3.9 order log `{turn, actorId, orderPayload}`, `replay(seed, orderLog)` `:88`; kernel freeze after M0 without a director ADR `:118`, `CLAUDE.md:35` |
| D-006 era-pacing table (the RULE, not the values — values are TUNE `:13`) | `m0-kernel-spec.md:13-25` — "dt-authority rule (Spine S3) applies from M6 when eras diverge; until then the table indexes on world date. Crisis-zoom subdivision is an M4+ feature; M0 only guarantees variable-dt correctness" `:25` |
| Spine S3 turn semantics | `outline.md:34` "era-scaled dt + crisis zoom, retained. dt authority rule: global dt is set by the world's most advanced polity's era band; all systems integrate rate × dt regardless (Law 3)"; sub-step rule `:33` |
| ADR-002 integer-day clock | `docs/adr/adr-002-integer-day-clock.md:8-12`, consequences `:26-30` ("a campaign tick-through therefore hits every band edge exactly") |
| ADR-007 `ITurnObserver` | `docs/adr/adr-007-turn-observer.md:26-28` "part of the frozen kernel surface … changes … require a director-approved ADR"; `TurnExecutor.cs:42-49` |
| The executor's pinned ordering and the order record's shape | `TurnExecutor.cs:53-56`; `OrderLog.cs:4-5` "the OrderRecord shape {Turn, ActorId, Kind, TargetId, Amount} is fixed" (a code statement over §3.9), `IoVersion = 1` `:125` |
| D-011 battle layer, incl. §6 resequence | `docs/d011-battle-layer-addendum.md:8-13` (command pulses), `:15-30` (dual-resolver contract; `:28` per-pulse orders in the log), `:34` (in-phase prompt; "Strategic turn pauses during command"), `:45` (sieges multi-turn), **`:54-66` the resequenced ladder** |
| The ladder after D-011 §6 | M4 trade + strategic war (AutoResolver only) · **M5 governing loop** ("it's a game now") · **M6 Battle Layer v1** (TacticalResolver, ancient units) · **M7 knowledge & divergence** (was M6) · **M8 politics & diplomacy** (was M7) · M9 society · M10 Ancient Vertical Slice · M11+ era expansions (`d011:59-66`). The Spine's own table (`outline.md:82-92`, `:109-113`) is stale by one from M6 onward and was never patched — GOV-2 §1c records the reconciliation as REQUIRED and unperformed (`docs/m4-pre-spec-dependencies.md:143-153`) |
| D-009/D-010 | `docs/d009-d010-map-population-addendum.md:5-24` ("NO CELLS"; the vector infrastructure graph `:12` — which also says "expensive, **era-gated**, terrain-crossing edges"), `:25-38`; closed per S8 `:16` |
| D-018 | `docs/d018-classes-and-needs.md` — "Classes emerge, they are not unlocked" `:8`; Soldiers as a class ("levies = armed peasants, not this class") `:24`; frozen per S8 `:16` |
| Later Director rulings (exempt class under S8 §4; source-of-truth rank 2 under gov-4 §1; NOT S8-frozen in the §1 sense — an open question the design corpus records as Q-73) | D-021 · D-035 (the carrier test `d035:91-93`) · D-037 · D-038 · **D-039** (A5 `:37-40` "LAW 4 BINDS ALL OF IT … Era is a consequence, never an input") · **D-040** (B3 `:59-64` "NO TECHNOLOGY UNLOCK. LAW 4 BINDS … a tech-tree node opening sea travel is a calendar gate wearing a tree") · **D-041** (B1 `:34-36` attachment is an accumulated stock and a lever) · **D-042** (§8.4 `:157-158` capabilities "consume computed state, never hard-coded calendar unlocks"; §9 `:162-175` knowledge Empire-scoped, research PARALLEL, unallocated knowledge accumulates as a reserve; §11 `:188-194` mid-turn control must be an explicit temporal architecture, "never an ad hoc pause"; §15 `:292` "Is an accumulating knowledge/science quantity permitted? YES") |
| Governance procedure | S8 §2 `:25-33` what counts as a contradiction; §4 `:41-49` "Nothing beyond n+1 is ever written"; `CLAUDE.md:24-30`; gov-4 §5–§6; gov-3 B6; ADR-015 §6/§7 |

### 6.2 Open CRs on `de5e00e` (status lines quoted)

| CR | status |
| --- | --- |
| **CR-005** | `docs/adr/cr-005-m5-research-technology-institutions-placement.md:3` "**Status: OPEN — awaiting director ruling.**" — placing Research/Technology/Institutions at M5 vs the frozen order (Spine M5 governing loop / M6 knowledge / M7 institutions; D-011 M7 / M8); options A renumber · B insert · C architecture-only packet inside M5; the CR's own crux `:149-152` |
| **CR-006** | `docs/adr/cr-006-continuous-time-and-campaign-epoch.md:3` "**Status: OPEN — awaiting director ruling.**" — §1 mid-turn hand-backs vs kernel §3.2–3.4; options A sub-step · B date-stamped presentation · C interrupt-as-boundary (recommended, with the replay condition `:100-103`); §2 the 10,000 BCE epoch vs ADR-002 and `CLAUDE.md:3`; §3 "turn duration may vary by historical period" ALREADY SATISFIED `:169-172` |
| **CR-016** | `docs/adr/cr-016-armed-disaster-fallout.md:3` "**Status: OPEN — ESCALATED TO THE DIRECTOR**"; Director ruling 2026-09-21: status unchanged, does not block M4; the mechanism ships INERT (`sim.json:235` `hazardPerYear: 0.0`) |
| CR-015 G1 | escalated and open (the harvest decade variance) — a ruled CR with one open item |
| CR-001 | **CLOSED** — option (a); option (b) rejected because "calendar-gated rates violate law 4" (`cr-001…:3-5`) — the closed precedent for any time-keyed rate (C13 maturation, "research points per turn") |
| CR-003 | RULED; the Malthus corridor quarantine is a standing ruling, not an open CR |
| CR-008, CR-009, CR-010 | **do not exist on `de5e00e`** (MEASURED `ls docs/adr`). CR-009 (era gates in frozen D-011/D-009 vs Law 4) and CR-010 (what an institution IS) were recommended (`docs/milestone-architecture-governance.md:295-300`) and D-042 §15 `:300` lists all three as still open and not settled there — the Director's own most recent record names two CRs that were never written |

### 6.3 A REMOTE-only state that must not be collapsed into MAIN (gov-4 three-state rule)

`origin/m5-full-build` (tip `9a79d1e`, 4 commits not on `main`, merge-base `dbef61a` — the pre-closure
M4 baseline; `main` is 168 commits past it) carries **M5 code** (`GovernanceSystem`, `TaxPolicy`,
`AiGovernance`, a second "schema v25" with `SystemId 22`), a **CR-008** file, and a `cr-005…md` whose
status line reads "**RULED 2026-09-05 — OPTION C ACCEPTED**". On `main`, CR-005 is OPEN and CR-008
does not exist. This audit uses `main`; **whether a Director ruling on CR-005 occurred off-main is
UNKNOWN** and is decision D-2 in §5. Two mechanical consequences regardless: `CanonicalSchema.cs:77-85`
already records the v25 collision ("whichever lands on main first keeps v25 and the other becomes
v26"), so any pre-M5 table on `main` is **v26 at the earliest and must avoid `SystemId 22`**; and the
`OrderKind` seam is already being extended off-main (`SetTaxRate`).

## §7 DOCUMENT DRIFT FOUND (doc says / tree says)

Both sides recorded; the tree wins on facts (gov-4 §1). Frozen documents are never edited (S8 §5);
the rest is housekeeping for whoever next touches each file. Rows the verifiers REFUTED are omitted
(one lane's "design docs cite `Variables.cs` at moved lines" was inverted — the design corpus is right
and the lane was wrong; struck).

| # | doc says | where | tree says | where |
| --- | --- | --- | --- | --- |
| 1 | `main` = `dbef61a`, schema v24; exit candidate `t4.19-glass-box`; six `Skip` entries | `docs/current-state.md:10, :208, :212, :373-377` | `main` = `de5e00e`, tag `m4-exit`, schema **v25**; closure suite 896/0/4 | `Sim.Core/Kernel/CanonicalSchema.cs:104`; `docs/m4-exit-inventory.md:349,:359` |
| 2 | `current-state.md` lists CR-007, CR-008, CR-009, CR-010 among open CRs | `docs/current-state.md:368-371` | CR-007 RESOLVED; CR-008/009/010 absent from `main` (its own header `:13` says so) | `docs/adr/` (MEASURED) |
| 3 | "Current milestone: M4 — active packets: `docs/m4-spec.md` §4" | `CLAUDE.md:10` | M4 is CLOSED (`docs/milestones.md:567`); no `docs/m5-spec.md` exists; the S8 §5 `:204` "next permitted document" pointer is one gate behind — the Director's line to move | tag `m4-exit` |
| 4 | Knowledge & diffusion M6; Politics M7; dt-authority "from M6" | Spine `outline.md:84-85, :110-111`; `m0-kernel-spec.md:25` | D-011 §6 resequenced to M7 / M8; reconciliation REQUIRED and unperformed | `d011:62-63`; `docs/m4-pre-spec-dependencies.md:143-153` |
| 5 | M4 = "trade + strategic war, AutoResolver only — armies, supply, attrition on world map" | `d011:59` (frozen); also `m4-spec.md:40` §1.6 and the T4.8 row `:275` | M4 closed with **no armies and no AutoResolver**; "M6 by ruling" — recorded only in the spec's own scope fence (`m4-spec.md:48-49` "M6 FIGHTS") and certification text, never in a D-doc or ADR | `docs/m4-exit-inventory.md:60, :216-217, :226-230`; `docs/milestones.md:326-328` |
| 6 | "T4.8 (strategic war + AutoResolver + notables-as-generals) has shipped"; "M4 trade + strategic war (shipped, T4.8)" | `docs/m5-roadmap-dependency-audit.md:36-37`; `docs/capability-architecture-decision.md:85-86, :321` | no AutoResolver; notables DORMANT ("no production driver") | `m4-exit-inventory.md:60-61, :93` |
| 7 | "no `PolityRow` type, no `Polities` table … BLOCKING"; "No polity entity exists" | `docs/capability-architecture-decision.md:56-62, :371`; `docs/milestone-architecture-governance.md:101-102`; `docs/queue.md:1221-1222` | `PolityRow(PolityId Id, CommandSource Source)`, `Polities`, `Controls`, `Capitals` shipped at M4-A/C, serialized at v23 | `Sim.Core/State/WorldState.cs:704`; `CanonicalSchema.cs:91` |
| 8 | "no system populates Polities yet … this check does not fire"; "nothing seeds a polity roster yet"; "no system calls them yet" | `Sim.Core/Kernel/OrderValidation.cs:26-32`; `Sim.Ui/ViewModel/LaborOrderFactory.cs:21-24`; `Sim.Core/State/EmpireQuery.cs:13, :101-103` | `WorldFounding` seeds `PolityRow(1, Player)`; `EmpireQuery` is called by `OrderValidation`, `ConstructionSystem`, `ColonizationSystem`, `TradeScope`, `WorldFounding` | `Sim.Core/Worldgen/WorldFounding.cs:283-285`; `OrderValidation.cs:33,53`; `ConstructionSystem.cs:93` |
| 9 | unknown-kind error text: "this build understands kinds 1 (SetRainBias), 2 (LaborAllocation) and 3 (SectorAllocation)" | `Sim.Core/Kernel/OrderLog.cs:216-218` | kind 4 `EnqueueConstruction` exists and validates | `OrderLog.cs:49, :200` |
| 10 | the latch "records that a predicate **has** fired" (used to argue monotonic acquisition) | `docs/m5-roadmap-dependency-audit.md:93-95`; `docs/capability-architecture-decision.md:128-131`; and the code comment `WorldState.cs:397-401` | the latch records CURRENT satisfaction under hysteresis; `recede` clears it | `ClassMobilitySystem.cs:28-33, :183-186` |
| 11 | "the predicate registry ships only three variables" | `docs/capability-architecture-decision.md:227` | four (`trade_volume` added at T4.11) | `Sim.Core/State/Variables.cs:93` |
| 12 | "`CLAUDE.md:10` still reads 'Current milestone: M3'" | `docs/m5-roadmap-dependency-audit.md:366-367` | reads M4 | `CLAUDE.md:10` |
| 13 | "This file is unmerged and unratified" | `docs/m4-pre-spec-dependencies.md:16` | in the tree at `de5e00e`; `m4-spec.md:35` cites its §1a as the source of a closed M4 decision — the document's class is unsettled | `docs/m4-spec.md:35` |
| 14 | "Kernel contract: already supports sub-stepped military phases and order-logged player input" | `d011:70` (frozen) | true for auto-resolved sub-steps (system-internal) and for a per-pulse order SEQUENCE (log order preserved); not for an in-phase pause — `Step` has no yield point | `TurnExecutor.cs:96-108`; `OrderLog.cs:98-121` |
| 15 | kernel §3.4 `SimClock { long Turn; double WorldDateYears; double DtYears; }`; §3.6 Ledger `ref long` | `m0-kernel-spec.md:70, :77-78` | `SimClock(long Turn, long SimDays, long DtDays)` (ADR-002); `ref Conserved` (ADR-004) — sanctioned refinements of an append-only spec | `SimClock.cs:10`; `Ledger.cs:50, :91` |
| 16 | CR-006 §1.1 cites the atomic turn at `TurnExecutor.cs:70-93` | `cr-006…:16` | `Step` is `:84-109` (already so at the CR's own commit — the cite was off when written) | `TurnExecutor.cs` |
| 17 | `queue.md`: "Next blocker is now CR-008 (money has no owner)" | `docs/queue.md:1250` | CR-008 exists only on `origin/m5-full-build` | §6.3 |
| 18 | "The 10-year atomic turn … unchanged" | `docs/m4-exit-inventory.md:73, :229-230` | imprecise: dt steps to 5 at turn 250 and the founded golden crosses it; "10-year" is the Neolithic band only | `era-pacing.json`; `FoundedHarnessTests.cs:16-19` |
| 19 | `docs/d011-battle-layer-addendum.md:54`: the D-040 blockquote is spliced onto the `## 6.` heading ("…the land.## 6. Milestone resequence") | frozen document | a `^## 6` grep misses the heading; formatting artefact — not to be edited (S8 §5) | — |
| 20 | `HistoryBuffer` comment: canonical campaign "~1,730 turns"; D-038 E6 "Sim.Ui is 4,778 lines" | `Sim.Ui/ViewModel/HistoryBuffer.cs:15-18`; `docs/d038-visual-target.md` E6 | exactly 1,630 turns (`EraTableTests.cs:52-54`); Sim.Ui is now 8,565 lines (MEASURED by lane G's verifier) | — |
| 21 | "LaborAllocation / SectorAllocation" control rule | nowhere — `m4-spec.md` has no text on whether kinds 2/3 are control-checked; "M4-D §12" cited in code has no document | an Empire may set a rival's labour split; untested either way — an unspecified omission, not a decision | `OrderValidation.cs:42-53`; `PathBuildSystem.cs:71-105` |

## §8 PROPOSED IMPLEMENTATION SEQUENCE (deliverable 5)

Ordered by what each step needs from the step before it. Nothing in stages 2–5 may start before its
ruling; nothing in stage 0 needed one.

**Stage 0 — done in this audit, no ruling needed.** The procedural glyph grammar and its test contract
(visual doc §2–§9), isolated in `Sim.Ui/Art/Glyphs/` and removable in one commit. Suite and gates green
(§9). No simulation file touched.

**Stage 1 — Director rulings, in this order** (the first two gate eighteen of the twenty-seven rows):
1. **DD-03** CR-005 + the Ladder-A/B reconciliation + the off-main "RULED" provenance. Decides where
   knowledge, institutions and diplomacy live and whether M5 is "about learning" at all.
2. **DD-01** CR-006 §1 (A/B/C), with **DD-02** (C01's scope; which artifact records dt).
3. **DD-04** Age: label or input (+ the D-011/D-009 "era-gated" reading; + whether the HUD may show the
   date-band name; + whether a Law-2/Law-4 gate stronger than review is commissioned).
4. **DD-05** the knowledge fence, completion, typing, the technology object.
5. **DD-10** armies/AutoResolver/sieges placement — one written ruling; **DD-06** War Pulses; **DD-09**
   veterancy carrier.
6. **DD-07** Action Capacity's interval; **DD-08** construction points and Structures-with-effects;
   **DD-11** CR-010 what an institution is; **DD-12** the v25 collision and canonical branch.

**Stage 2 — the M5 spec, written under S8 §4.1** (ADR-014): FOUNDATIONS AUDIT as packet one; the
dimensional declaration; corridor independence; the coupling map. `CLAUDE.md:10` moves at the same
time (Director). The spec inherits from this audit: the readings the Director chose in §4, the
frozen-item register in §6, and the dt register in §2 as the list of pins any variable-dt packet must
re-derive with attribution controls.

**Stage 3 — EXTENSION work that becomes buildable the moment its placement is ruled** (each an ADR; each
new table pays §1.3's checklist at v26/v27):
- the **polity-keyed variable/predicate substrate** (D-042 §8.3 scope) and a **literacy/education
  publisher** — the two things every knowledge, institution and Age concept needs first (arch-N's N1/N3);
- **C26** projection observer (an ADR extending the observability field kinds; clone-stepped `Step`;
  Spine Law 7 symmetry stated; ranges derived or omitted);
- **C15** endogenous construction (predicate on `ConstructionProjectEntry`, or a system; the
  Structures-with-effects question ruled first under DD-08);
- **C18** as a per-sim-year authority stock, if DD-07 takes the sim-time reading;
- **C16 / C19 / C22 / C23 / C24** military tables and orders at the milestone DD-10 names;
- **C20** under its side-table / new-`EdgeTypes` reading, if DD-08 keeps `Cost` fixed;
- **C09 / C07 / C10 / C27** as EXTENSIONs on the knowledge table once DD-03 and DD-05 are ruled.

**Stage 4 — REFACTOR-class, each a director-signed ADR with a golden re-pin and attribution controls:**
**C14** (the construction bank — also remedial for the live Law-3 strain in the gate), **C20** (edge
widening + recompute cadence), **C01** under the subdivision reading (the kernel-freeze ADR over
`TurnExecutor.cs:86`; plus the Appropriation seam's schema bump, the 1,630 pin, the turn-250 windows,
and the three queue dt-sensitivities named in §2 rows 15–17).

**Stage 5 — only after their rulings:** the knowledge layer proper (C08–C13, C27), Ages as computed
labels (C02–C06) with their producer, C17 at M6, C21/C25 under whatever CR-006 becomes.

**The inserted visual milestone (D-038 E1, after M5, before M6)** then composes the object tier — the
settlement sprite (Part H), army tokens, terrain relief — against the grammar's light constant, state
alphabet and palette (visual doc §7), and answers H8's legibility question with parts in hand.

**What not to do in any stage:** ship an Age that anything reads; a per-turn budget; a research line
that completes into a permanent grant; a per-formation experience number that outlives its people; a
turn-count duration; a dt that is not recorded as data if it is not derivable from state.

## §9 WHAT THIS AUDIT DID NOT DO, AND THE VALIDATION RUN

**Not done, by instruction or by scope.** No Draft 1 document was read (none was available — §0). No
simulation behaviour was changed, no schema bumped, no golden moved, no data file edited, no ratified
document amended, no M5 spec or packet written, no mechanism designed beyond what a classification
needed. No mutant runs (nothing was implemented on the simulation side to mutate). No `sim bench`
(no hot path touched). Design questions the corpus already lists (arch-C/D/E/FGH, the G-/Q-/P- items)
were routed to a decision in §5 rather than re-raised.

**Method, measured.** Recon workflow: 8 lanes + 16 adversarial verifiers (24 agents, 0 errors, 1,051
tool uses, 2 h 26 m). Reconcile workflow: 8 reconcilers + 1 merger (9 agents, 0 errors, 272 tool uses,
1 h 12 m). Verifier verdicts, MEASURED from the structured returns: on the lanes' claims **300 CONFIRMED, 39
REFUTED, 1 UNVERIFIABLE** — most refutations were line-number offsets with the substance confirmed in
the verifier's own correction text (four files were off by constant offsets: `OrderLog.cs` ~+90,
`EraTable.cs` +20, `EraTableLoader.cs` ~+70, `Variables.cs` ~+50; the refutations of substance are the
ones recorded in §2 and §4 — the Appropriation dt seam, the intra-turn order preservation, the inert
disaster mechanism, the D-042 §9 accumulation ruling, the `m5-full-build` CR-005 copy, the OrderKind
count); on the concepts **79 UPHELD, 11 ADJUSTED, 6 REFUTED** (C17 twice, C20 twice, C02, C18 — all
adopted); on the dt-register rows **111 REAL, 14 OVERSTATED, 10 UNDERSTATED, 3 NOT_REAL**; **127
MISSED** items raised, the material ones folded into §1–§7. The reconcilers re-opened every citation
they carried (lane A alone corrected 26); the merger spot-checked ≥ 3 per concept; the author then
re-opened every citation that appears in this document (three `sed -n` batches of ~50, ~50 and ~40
lines) and ran one bulk pass over the 627 unique citations in the merged table for existence and
range — one beyond EOF, corrected (`d042:307-310` → `:300`). Working papers (lane, verifier, reconciled and merged
reports) are in the session scratchpad, not the repository.

**Validation of the tree after this audit's one code change (the glyph grammar), Release, 2026-09-22:**

| check | result |
| --- | --- |
| `dotnet build Sim.slnx -c Release` | 0 warnings / 0 errors |
| `Sim.Ui.Tests` | **306 passed / 0 failed** (296 + 10 `GlyphGrammarTests`) |
| `Sim.Tests` | **896 passed / 0 failed / 4 skipped** — identical to the M4 closure baseline (`docs/m4-exit-inventory.md:359`) |
| `check-banned-constructs.sh` | OK |
| `check-read-isolation.sh` | OK |
| `check-readonly-proof.sh` | OK (mutation attempts fail with CS0200, CS1061) |
| `git diff --stat de5e00e HEAD -- Sim.Core Sim.Data Sim.Cli Sim.Tests scripts .github` | **empty** — the determinism surface is byte-identical to `main` |
| `sim-ui --glyph-sheet docs/architecture/glyph-sheet-v0.png` | 456×960, committed |

**One line, as the constitution asks.** The tree at `de5e00e` is a closed M4 with a variable-dt kernel
in Law 3's sense and a fixed-turn kernel in three frozen senses; it has no knowledge, Age, unit, war or
projection code; 18 of the 27 Draft-1 concepts land on ground the Director has already reserved (two
open CRs, four laws as named, one ruling never written), 7 are additive once placement is ruled, 2 change
shipped semantics without touching anything frozen, and the twelve decisions in §5 are the whole of
what stands between Draft 1 and a ratifiable architecture.
