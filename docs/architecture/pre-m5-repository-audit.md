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

[SECTION PENDING — filled after lanes C–H return]

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

[SECTION PENDING]

## §4 CONCEPT FINDINGS, BY AREA

[SECTION PENDING]

## §5 CONFLICTS REQUIRING DIRECTOR DECISIONS (deliverable 3)

[SECTION PENDING]

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

[SECTION PENDING]

## §9 WHAT THIS AUDIT DID NOT DO, AND THE VALIDATION RUN

[SECTION PENDING]
