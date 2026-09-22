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
MEASURED: 77 `dtYears`/`DtDays` sites in 20 files under `Sim.Core/Systems`; every live site reads
`ctx.DtYears` fresh per step and **no caller assumes dt is constant across turns**. The campaign
already runs six different dts (10 → 5 → 3 → 2 → 1 → 0.5 sim-years, `Sim.Data/content/era-pacing.json`)
and pins continuity across the 10→5 flip (`Sim.Tests/Systems/DemographyRetuneTests.cs:729`,
"This test exists forever"). The one sub-turn subdivision in the tree — the 0.5-year demographic
micro-step — is internal to one system's `Step`, documents the non-multiple case ("one shorter final
micro-step; deterministic", `Sim.Core/Systems/Demographics/DemographicsSystem.cs:26-30`, loop
`:286-290`) and needs nothing from the executor. CR-006 §3 records "turn duration may vary by
historical period" as ALREADY SATISFIED (`docs/adr/cr-006-continuous-time-and-campaign-epoch.md:169-172`).

### 2.2 What is PINNED, and by which frozen document

| pinned item | frozen statement | tree |
| --- | --- | --- |
| **dt is a function of the turn-START DATE via the era table** | kernel contract §3.4 "dt from the era table" (`docs/m0-kernel-spec.md:70`); D-006 "the table indexes on world date" (`:25`; D-006 inside the S8 perimeter, `docs/spine-s8-governance-freeze.md:16`); ADR-002 consequences (`docs/adr/adr-002-integer-day-clock.md:26-30`) | `TurnExecutor.cs:53-56` (the pinned ordering), `:86` `dtDays = _eraTable.DtDaysAt(prev.Clock.SimDays)`; `EraTable.cs:46-49` "kernel rule: dt is selected by the date at turn start" |
| **the turn is ATOMIC — the only control boundary** | kernel §3.2–3.3 double buffer + fixed pipeline (`m0-kernel-spec.md:66,68`); Spine `:31` `step(prev, my_tables_rw, orders, rng_stream, dt)`; S8 `:15` | `TurnExecutor.cs:96-108` — no exit between (3) run systems and (4) advance clock; the only hook, `ITurnObserver.OnPhaseState`, is typed `IReadOnlyWorldState` and documented "A READ-ONLY WINDOW AND THAT IS LOAD-BEARING" (`:42-49`; ADR-007 `:15-20`, changes "require a director-approved ADR" `:27`) |
| **orders apply at integer-turn granularity** | kernel §3.9 `{turn, actorId, orderPayload}` (`m0-kernel-spec.md:88`); T1.9 precedent (`CLAUDE.md:39`) | `OrderLog.cs:70` `OrderRecord(long Turn, int ActorId, OrderKind Kind, int TargetId, double Amount)` — "the OrderRecord shape … is fixed" (`:4-5`); `BatchFor(prev.Clock.Turn)` (`TurnExecutor.cs:100`); wire format `IoVersion = 1` carries no dt and no sub-turn coordinate (`:125-142`) |
| **band edges are turn boundaries; dt is whole days** | ADR-002 `:26-30` | `EraTableLoader.cs:98-101` "band edges must land exactly on turn boundaries (ADR-002)"; `:90-96` whole-day dt; `Sim.Tests/Kernel/EraTableTests.cs:29` `FullCampaignTickThrough_Exactly1630Turns_LandsOn2100Exactly` |

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
| 1 | dt is a pure function of the turn-start date | `TurnExecutor.cs:86`; `EraTable.cs:51-60`; `m0-kernel-spec.md:25,70` | any dt chosen by state, event or player | **frozen** (kernel §3.4, D-006) |
| 2 | dt is fixed BEFORE the order batch is read | `TurnExecutor.cs:86` (dt) vs `:100` (orders) | a player order that sets the current turn's length | frozen ordering |
| 3 | band edges are turn boundaries; the campaign is exactly 1,630 turns | `EraTableLoader.cs:98-101`; ADR-002 `:26-30`; `EraTableTests.cs:29,52-54` | any turn whose length ≠ band dt: a shortened turn lands mid-band; the next lookup still works (half-open bands) but the edge-landing guarantee and the 1,630 pin die | ADR-002 consequence |
| 4 | dt is whole days, ≥ 1 day | `SimClock.cs:10`; `EraTableLoader.cs:90-96` | fractional-day or "instant" boundaries | ADR-002 |
| 5 | turn index ↔ calendar date is a fixed map | goldens `SnapshotTests.cs:87,334`, `DrivenGoldenTests.cs:106`; CI `--turns 300/400` (`ci.yml:93-162`); the CR-001 detonator's windows assume the gate at TURN 250 (`DemographyRetuneTests.cs:741-745`); battery horizon `RunCanonical(1, 650)` (`CalibrationBatteryTests.cs:590`) | any interrupt-created or circumstance-chosen turn shifts every date-at-turn-N: every golden re-pins; year-windowed tests can go vacuous | test/golden blast radius (REFACTOR-class) |
| 6 | dt is recoverable from `(seed, orderLog)` | `OrderLog.cs:125-142` (no dt field); `m0-kernel-spec.md:88` replay(seed, orderLog); `SessionManifest.cs:46-77` and `SessionTrace.cs:34` carry no dt either; only the snapshot's `Clock.DtDays` (`CanonicalSchema.cs:150,161`) and the JSONL observers echo it | any dt that is not a deterministic function of state — CR-006 `:100-103` "the shortened dt must be recorded as data, or replay diverges" | **frozen** (§3.9 replay contract) + open CR-006 |
| 7 | the turn is atomic; no control exit inside `Step` | `TurnExecutor.cs:96-108`; `ITurnObserver` `:42-49`; ADR-007 | any mid-interval hand-back (C25; C21 player-in-loop) | **frozen** (kernel §3.2–3.4) + open CR-006 §1 |
| 8 | orders carry one integer time coordinate | `OrderLog.cs:70, :53-55, :98-121`; `CLAUDE.md:39` | a mid-turn order or a per-pulse order: a NEW delivery semantic needing its own turn-exact pin, on a clock with no sub-turn coordinate | **frozen** (§3.9) + T1.9 precedent |
| 9 | the demographic micro-step composes identically at every dt | `DemographicsSystem.cs:24-30`; ADR-011 | dt not a multiple of 0.5 yr (still deterministic; the by-construction invariance and the exact-equality pins cover 10/5/2.5 only) | ADR-011 scope |
| 10 | at most one disaster onset per turn; famine classification is dt-dependent (accepted artefact) | `DisasterSystem.cs:52,105,142-143`; `DisasterSystemTests.cs:379`; CR-016 item D | a strategically chosen dt makes famine OUTCOME a function of the dt choice — a modifier by another name (Law 2 strain) | open CR-016 |
| 11 | rails and bounds are sized for dt ∈ [0.5, 10] | `SimConfig.cs:299-302, 328-334`; `ProductionTests.cs:817-821`; `MigrationSystem.cs:381` (EMA saturates at dt ≥ window) | dt > 10 — a "long peaceful stretch" turn | TUNE-level, but real |
| 12 | turn-stamped state means "turns since" | `WorldState.cs:64` `FoundedTurn`; `CatchmentSystem.cs:140` `LastRecomputeTurn` | already era-dependent; meaningless in years under variable dt | strain |
| 13 | one `Step` per End Turn / hash-log line / trace line / `TurnRecord` | `UiSession.cs:248-257`; `Sim.Cli/Program.cs:201-205`; `SessionTrace.cs:51-57`; `ObservationLog.cs:96-99` contiguity guard | unaffected by variable dt (the turn stays atomic); broken by mid-turn hand-backs | as #7 |
| 14 | RNG draw count per turn is fixed per system×settlement | e.g. `DisasterSystem.cs:110-112` "ALWAYS taken, even when unused" | a different turn count over the same years changes the draw sequence — determinism intact, goldens move | expected |
| 15 | `Clock` serialises as one (Turn, SimDays, DtDays) per turn | `CanonicalSchema.cs:150, :158-161` | pulses or sub-steps inside a turn have no clock slot; sub-state must be an owned table under the Spine sub-step rule (`outline.md:33`) | schema |

**Already dt-robust and NOT breakage** (recorded so nobody lists them): calibration windows are
keyed by sim-year, not turn (`AutoplayMetrics.cs:112-134`); `SettlementVitalsRow.DtYears` caches the
dt a row was computed over precisely so an era-boundary reader forms per-year rates correctly
(`WorldState.cs:536-542`; `FoodHeadroom.cs:36-38`); AR(1) harvest weather is variance-corrected in
dt (`HarvestWeatherSystem.cs:138-139`); the exponential sinks (`MigrationSystem.cs:227,675`;
`DisasterSystem.cs:105`); `DisasterRow.RemainingYears` is "persisted BY DIMENSION, because a disaster
can outlast a late-era turn" (`WorldState.cs:769-771`). These are the shipped patterns any new
stock must follow: **per-year rates plus dt, never per-turn amounts** (Law 3).

### 2.4 What follows for the Draft-1 concepts

- **C01 (variable strategic calendar resolution)** collides with rows 1–6 and is, in the tree's own
  vocabulary, CR-006 §1.4 **Option C** — "interrupt as a turn boundary … dt becomes event-dependent,
  so it must be recorded in the order log for replay" (`cr-006…:85-91`), which is OPEN and unruled.
- **C25 (mid-interval interrupts)** collides with rows 7–8 and 13 and is CR-006 §1 verbatim. Under
  Option C it collapses into C01; under Option B it becomes presentation only; under Option A it
  re-opens dt-correctness across the whole simulation, which CR-006 recommends against.
- **C21 (War Pulses)** in D-011's own definition — "player issues orders to formations → both sides'
  orders resolve simultaneously … next pulse", "player battle orders append to the order log per
  pulse", "Strategic turn pauses during command" (`docs/d011-battle-layer-addendum.md:10,28,34`) —
  collides with rows 7–8 and 15. Auto-resolved pulses inside one `Step` collide with nothing (the
  demographic micro-step is the precedent).
- Every other concept is indifferent to dt provided it is written as a per-year rate (row set 2.3's
  robust list), which is Law 3 and not a new constraint.

## §3 THE 27 CONCEPTS — classification table

[SECTION PENDING]

## §4 CONCEPT FINDINGS, BY AREA

[SECTION PENDING]

## §5 CONFLICTS REQUIRING DIRECTOR DECISIONS (deliverable 3)

[SECTION PENDING]

## §6 THE FROZEN-ITEM REGISTER AND THE OPEN CRs

[SECTION PENDING]

## §7 DOCUMENT DRIFT FOUND (doc says / tree says)

[SECTION PENDING]

## §8 PROPOSED IMPLEMENTATION SEQUENCE (deliverable 5)

[SECTION PENDING]

## §9 WHAT THIS AUDIT DID NOT DO, AND THE VALIDATION RUN

[SECTION PENDING]
