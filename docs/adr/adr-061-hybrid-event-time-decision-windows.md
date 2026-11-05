# ADR-061 — Hybrid event-driven integer time: four clocks, decision windows, War Pulse sub-stepping and the proportional carry (kernel time-contract amendment)

**Status: ACCEPTED as to the Director decisions of 2026-11-05 (DIR5 §2.4, §2.5; D-079 DR11–DR18). The mechanism in §3
(labelled [DELEG]) is the engineering agent's PROPOSAL under delegation. NOT IMPLEMENTED in `Sim.Core`.** The
implementation stays in the isolated prototype `prototypes/m7-sprint1/` until the Director's architecture review
(DIR5:56, "Keep prototypes isolated for now"; DIR5:686, "Do not silently change frozen contracts"). Whether it may land
in frozen `Sim.Core/Kernel` before that review is **OPEN** (D-079 §5 Q5; `docs/m7-plan/m7-2-m7-3-revision-2026-11-05.md`
OD-1), and with it whether DIR5 is the Director sign-off the kernel freeze requires (`docs/m0-kernel-spec.md:118`).
This ADR changes no code, data, schema, golden, test, CI file or frozen text. State: LOCAL and uncommitted on branch
`claude/new-session-yavghm`, HEAD `7d5defc` (= tag `m6-exit` = `github/main` as of the last fetch; there is no local
`main`).

**ADR number.** 061 was reserved by D-076 A-13 ("Kernel ADR (ADR-061, proposed)", `docs/d076-m7-decision-reconciliation.md:232`)
and is listed by D-079 as A-31. A-13's "Director sign-off required" is superseded as to the decision: the decision is
DIR5's, and writing this record is administrative (D-079 S109 on `docs/m0-kernel-spec.md:156`).

**Nature of this record.** Administrative. The Director states that where an ADR update "is administratively necessary,
perform that documentation work. Do not treat it as requiring renewed Director approval" (DIR5:21). This ADR is therefore:
- the ADR the kernel freeze asks for before a change to `Sim.Core/Kernel` (`docs/m0-kernel-spec.md:118`) and to
  `ITurnObserver`'s observed-phase set (`docs/adr/adr-007-turn-observer.md:26–28`). Its decisions are the Director's; its
  mechanism is proposed under delegation. Whether DIR5 is the "director-approved" sign-off that lets the code land now is
  OPEN (D-079 §5 Q5; OD-1), so this ADR alone authorizes no kernel change;
- the written, priced ADR the S8 Director override requires (`docs/spine-s8-governance-freeze.md:37`) for the frozen
  kernel code contract (`:15`: `SimContext`, double-buffer model, RNG regime, snapshot/hash format) — §10 below.

It decides nothing the Director did not decide. Where DIR5 delegates, the mechanism is recorded as the engineering
agent's design, with its evidence, and is labelled so. Where nothing has decided a point, it is listed as OPEN.

**Citation forms.**
- **DIR5:n** — line n of the Director's instruction of 2026-11-05, working copy
  `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gov/director-decisions-2026-11-05.md`.
- **DRn, Sn, An, Dn, Qn, Rn** — rows of `docs/d079-m7-governance-reconciliation-2026-11-05.md` (decision register,
  supersession matrix, administrative consequences, delegated decisions, open questions, readings).
- **C1…C38, OD-n, RDG-n** — contracts, open items and readings of the M7.2/M7.3 plan revision
  `docs/m7-plan/m7-2-m7-3-revision-2026-11-05.md` (W2's record of the same pass).
- **B:n** — line of `prototypes/m7-sprint1/reports/B-foundation.md` (Slice B, the time and event prototype).
- Repository citations are `path:line` at `7d5defc`, plus the LOCAL uncommitted M7 records. Every code citation below was
  opened on 2026-11-05 when this ADR was written.

**Categories, used in every section:**
- **[DIR]** DIRECTOR DECISION — quoted from DIR5, or carried from D-075/D-076 where DIR5 confirms it.
- **[ADMIN]** ADMINISTRATIVE CONSEQUENCE — documentation or bookkeeping a decision makes necessary.
- **[DELEG]** DELEGATED TECHNICAL DECISION — the engineering agent's design under DIR5:150, D-076 N5 or D-079 §4.
  "Taken" means recorded here as the design; "pending" means the delegation stands but the evidence is not in yet.
- **[OPEN]** needs the Director.
- **[READING]** an interpretation this record had to make; the Director may correct it.

---

## §1 Context: the frozen time contract as it is today (verified in code)

| Clause | Text or code today | Where |
|---|---|---|
| Double buffering | "At turn start the kernel clones `Prev → Next` … Systems read `Prev`, write `Next`." | `docs/m0-kernel-spec.md:66` (§3.2) |
| Pipeline | "Fixed ordered `SystemId[]` … real systems slot in from M1 without executor changes." | `m0-kernel-spec.md:68` (§3.3) |
| Clock | `SimClock { long Turn; double WorldDateYears; double DtYears; }`; "A system that hardcodes per-turn amounts fails review." Stored per ADR-002 as `SimClock(long Turn, long SimDays, long DtDays)`, 360-day year, derived `DtYears` | `m0-kernel-spec.md:70` (§3.4); `Sim.Core/Kernel/SimClock.cs:10`; `docs/adr/adr-002-integer-day-clock.md` |
| dt rule | dt is the era-table band's dt at the turn-START date | `Sim.Core/Kernel/EraTable.cs:31` (`DtDaysAt`); `TurnExecutor.cs:116` |
| One step per turn | `Step(prev)`: dt at turn start (:116), clone (:126), one `OrderBatch` = `BatchFor(prev.Clock.Turn)` (:130), every system once in data order, clock advanced by one whole turn (:138) | `Sim.Core/Kernel/TurnExecutor.cs:114–151` |
| Context | `SimContext` carries `DtDays`, `DtYears` (the turn's) and the turn's `Orders` | `Sim.Core/Kernel/SimContext.cs:38`, `:41`, `:43` |
| RNG | PCG32 streams per (system × region), state in `WorldState` | `m0-kernel-spec.md:72` (§3.5); `Sim.Core/Kernel/RngRegistry.cs:57` |
| Snapshot and hash | header `{magic, version, seed, turn}`; `WorldHash` = SHA-256 of the canonical stream; schema v39; clock serialized as three longs | `m0-kernel-spec.md:86` (§3.8); `Snapshot.cs:10`; `CanonicalSchema.cs:231`, `:365`, `:373–376` |
| Order log | `{turn, actorId, orderPayload}`; `OrderRecord(long Turn, int ActorId, OrderKind Kind, int TargetId, double Amount)`, "shape … is fixed"; append-only in turn order; `IoVersion = 1`, and `Load` reads only version 1 | `m0-kernel-spec.md:88` (§3.9); `OrderLog.cs:5`, `:330`, `:359`, `:369`, `:385`, `:414` |
| Delivery | "an order with Turn = t is delivered to the step executing from turn-t state" | `docs/adr/adr-005-canonical-schema.md:40` |
| Sub-step rule | sub-stepped systems "read only frozen turn-start state plus their own sub-state. No mid-turn reads of other systems' fresh writes." | `docs/civ-sim-architecture-v3-outline.md:33` |
| Observer | the observed-phase set is frozen kernel surface | `docs/adr/adr-007-turn-observer.md:26–28`; `TurnExecutor.cs:44` |

Earlier decisions already moved part of this as decisions (not in code): D-076 N2–N5 and S62–S64 (sub-stepped turn,
sub-turn coordinate, "turn-start" read as "sub-step-start"). DIR5 confirms them and adds the proportional carry, the
four-clock vocabulary, per-pulse economics and the War Pulse locality rules.

The prototype evidence is Slice B (`prototypes/m7-sprint1/Proto.M7/Time/`, `Events/`, `Rng/`, `Demo/`; report B:1–206).
It kept the campaign turn atomic and integrated only linear processes (B:127). §8 states exactly what it proves.

---

## §2 Director decisions recorded [DIR]

| # | Decision (verbatim) | DIR5 | D-079 |
|---|---|---|---|
| T-D1 | "Use four clocks: 1. Campaign turn. 2. Operational War Pulse. 3. Tactical battle pulse. 4. Internal simulation step." | :118–122 | DR11 |
| T-D2 | Operational War Pulses "Have era-varying fixed durations"; "Are local to wars and their participants"; "Do not automatically expose distant wars to uninvolved civilizations"; "Become known through observation by units or cities within explored areas"; "Must respect fog of war." | :124–129 | DR12 |
| T-D3 | "The exact era-specific pulse durations remain open. Propose and test sensible values for Director review." | :131 | DR12; Q1 |
| T-D4 | "War Pulses may cross campaign-turn boundaries. Use sub-stepping. Every golden replay must be re-pinned following the relevant changes." | :133 | DR13 |
| T-D5 | "Economic effects occur per operational pulse, not exclusively at campaign-turn boundaries." | :135 | DR14 |
| T-D6 | "Use a hybrid event-driven and integer-time architecture." "Event-triggered decision windows may interrupt a campaign turn at non-integer time." "Players must be able to act on significant events before the next normal turn boundary." "Preserve deterministic event ordering, order logs, replay and scheduling." | :139–143 | DR15 |
| T-D7 | "Preserve fractional time proportionally when campaign-turn duration changes. Do not discard leftover time." | :144 | DR16 |
| T-D8 | "Prepare a proposed list of significant events that can interrupt a turn and present it for Director approval." | :146 | DR17; Q2 |
| T-D9 | Canonical example: "a major construction project such as the Taj Mahal completes one-third of the way through a six-year campaign turn. The game pauses and opens a decision window at that point." | :148 | DR15 |
| T-D10 | "You have delegated authority to determine deterministic event priority, tie-breaking, collision handling, interruption rules and RNG-consumption rules." | :150 | DR18 |
| T-D11 | "Maximum two actions per decision window." (The decision window defined here is the unit; the budget's rules are A-35 and contract C23.) | :170 | DR22 |
| T-D12 | Cosmetic animation "must never … Consume simulation RNG. Alter simulation timing. Affect deterministic state or hashes." | :438–446 | DR42 |
| T-D13 | "Do not silently change frozen contracts." "Do not weaken determinism, Ledger conservation, replay integrity or canonical serialization." | :686–687 | DR53 |

Two readings this ADR depends on, both recorded in D-079 §1.5:
- **[READING] R2** — "era" in "era-varying" means the computed state that sets dt (the dt-authority rule,
  `civ-sim-architecture-v3-outline.md:34`), not a calendar label, so Law 4 is untouched.
- **[READING] R11** — the six-year turn in T-D9 is illustrative; the shipped era table has no six-year dt
  (3,600/1,800/1,080/720/360/180 days, `Sim.Data/content/era-pacing.json`). One third of every shipped turn is a whole
  number of days (B:65).

---

## §3 The amended time contract

### 3.1 One integer timeline [DIR: integer time, T-D6; DELEG taken: the quantum]
- Every instant is a `long` count of milliseconds on one global timeline: `SimInstant(long Ms)`, 86,400,000 ms per day,
  360-day year (ADR-002's calendar is retained), instant 0 = campaign day 0 (4000 BCE). The shipped campaign
  (4000 BCE–2100 CE, 2,196,000 days) ends at 189,734,400,000,000 ms, far inside `long`.
- One quantum serves every clock, so campaign instants, War Pulse boundaries and battle instants compare without
  conversion (contract C1; B:24). No `double` holds time state. The derived `DtYears` stays the Law 3 rate basis, computed
  from exact integer endpoints by one IEEE division (C20 rule T5).
- ADR-002's integer days remain the storage of the campaign clock at turn boundaries. ADR-002's "clock is three longs"
  and "tick-through hits every band edge exactly" stay true at turn boundaries; a pointer note on ADR-002 is owed (A-44).

### 3.2 The four clocks and where each lives [DIR: the four clocks, T-D1; ADMIN: their homes follow DR6 and this ADR]

| Clock | Lives in | Duration | Status of the duration |
|---|---|---|---|
| Campaign turn | `Sim.Core/Kernel` (this ADR) | era-table dt at the turn start, unchanged rule (`EraTable.cs:31`); a turn's length is fixed at its start | Data (D-006), unchanged |
| Operational War Pulse | War state (campaign tables owned by the M7.14 systems); scheduled by the kernel scheduler (§3.4) | era-varying, fixed for each era (keyed per READING R2), kept for the whole pulse (§3.11) | **[OPEN]** values: a proposal for Director review (T-D3; D-079 Q1). The prototype's 7 d / 1 d are PLACEHOLDERs (B:45) |
| Tactical battle pulse | The battle framework (`Sim.Battle` direction, ADR-062) | fixed | **[OPEN]** (D-076 Q-B1). DIR5's two-second figure (:440) is an example, not a ruling (OD-4 (v)) |
| Internal simulation step | Inside a system or the battle framework | implementation | **[DELEG]** the hybrid scheme (D-076 N3) |

No battle-pulse coordinate and no battle clock enters `Sim.Core/Kernel` (DR6; ADR-062 §3). The kernel's new time
coordinate is the campaign one only: an instant inside the campaign turn.

### 3.3 The campaign turn as a sequence of sub-steps [DIR: sub-stepping, T-D4, T-D6; DELEG taken: the shape]
- Campaign turn n is the half-open interval `[S_n, S_{n+1})`, with `S_{n+1} = S_n + L_n` and `L_n` the era-table dt at
  `S_n`. Turns tile the timeline with no gap and no overlap (C1; B:62–66, measured over all 1,630 turns).
- A turn executes as an ordered sequence of **sub-steps**. Its boundaries are `S_n`, every decision-window instant in the
  turn (§3.6), every settlement instant that §3.11 requires, and `S_{n+1}`.
- Each sub-step is a full step over its sub-interval: clone `Prev → Next`, run the data-ordered pipeline (§3.3 of the
  kernel spec is kept: order is data, executed sequentially), each system reads sub-step-start state and writes only its
  own tables, with the sub-step's length as dt. The sub-step rule holds with "turn-start" read as "sub-step-start"
  (D-076 S64): no system reads another's fresh writes inside one sub-step.
- **A turn with no interior boundary is exactly one sub-step, and is today's `Step`.** At integration this is proven by a
  switch-off twin and by the schema projection (`WorldHash.ComputeHexAsSchema`, the D-056 precedent), not asserted.
- The clock advances only at the turn end: `Turn + 1`, `SimDays + DtDays` (unchanged, `TurnExecutor.cs:138`).
- **[DELEG pending] Locality.** Splitting a turn for one war or one polity's event must not change polities that have no
  interaction with it: contract C19's isolated-polity test, which also carries ADR-019's retained sentence "it is not a
  second global clock" (`adr-019-architecture-constitution-addendum.md:81`). Two mechanisms are admissible (C21): (a)
  participant-scoped sub-steps, or (b) world-level sub-steps whose integrations are proven piecewise-invariant for
  non-participants. Slice B showed that the campaign's nonlinear integrations (demography's `Exp`, compounding) are not
  piecewise-invariant today (B:127), so (b) needs new integration forms. The choice is made on Phase C item 2 evidence
  and recorded in an amendment log of this ADR.
- **[DELEG pending] Per-turn quantities.** Grants denominated per campaign turn (ADR-030 research points per strategic
  turn, ADR-054 civics points per turn, the per-step grants of ADR-050 and ADR-051) are keyed to the campaign turn, so a
  turn split into k sub-steps grants exactly what the unsplit turn grants (C21 test "grant non-multiplication"). Pointer
  notes on those ADRs are owed once the keying is implemented (A-44).
- **Clone cost.** ADR-020's clone happens once per sub-step instead of once per turn. That is a measured cost (`sim bench`
  at integration), not a contract change.

### 3.4 Deterministic event ordering [DELEG taken under T-D10, as prototyped and verified in Slice B]
- **Key.** Every scheduled event carries the lexicographic key `(TimeMs, Class, Priority, SourceId, Seq)`.
  - `Seq` is a scheduler-global counter, strictly increasing, assigned when the event is scheduled. Keys are therefore
    unique, and extraction order is fully determined: the composite key with a stable integer tie-break that
    `CLAUDE.md:48` requires.
  - `SourceId` is a stable integer identity (settlement, polity, formation, war or process id, packed without collision).
    It is never derived from `GetHashCode` or from any process-dependent value (Law 5).
  - `Priority` is an integer set by the scheduling rule, never a `double` score. Where a score orders events, it enters as
    `(score, id)` with the stable tie-break and a tie-dense test.
- **Classes, ascending within one instant** (`prototypes/m7-sprint1/Proto.M7/Events/EventKey.cs:19–29`):
  System 0 < Arrival/Movement 10 < Detection 20 < Fire 30 < Damage 40 < Resolve 50 < CampaignProcess 60 <
  Significant 70 < PlayerOrder 250. The gaps leave room for new classes without renumbering. The campaign kernel uses
  60, 70 and 250 (and 0 for pulse bookkeeping); classes 10–50 serve the battle framework on the same timeline.
- **Causality.** A newly scheduled key must be greater than the key that is executing. The executed key sequence is
  therefore strictly increasing and no phase can be inverted at an instant (B:26). Class 250 is reserved: only the runner
  places orders there (B:27); `Schedule` rejects every class at or above it (verification fix F9, B:170).
- **Trace.** A SHA-256 trace hash over a canonical little-endian encoding of every executed event, excluding the raw
  `Seq`. A live run and its replay allocate `Seq` differently; the proof that the trace still agrees is in B:35–38.
- **Heap.** An explicit binary min-heap; no `SortedSet`, `PriorityQueue` tie order or LINQ ordering (Law 5).

### 3.5 Collision handling [DELEG taken]
- **Same instant.** Events at one instant execute in class order, then priority, source and sequence. All `Fire` events at
  an instant act on the pre-damage world, all `Damage` lands, and only then is anything resolved: two units that shoot
  each other at one instant both get their shot (B:28–31).
- **Invalidation.** An event names its target and its source with a generation. If either generation is stale when the
  event is due, the event is dropped and traced as `Dropped`, never silently skipped (B:15, mutant M5).
- **Cancellation.** By a tombstone keyed by `Seq`, traced (mutant M11; B:113 records that its kill is load-bearing only in
  its direct test).
- **Several significant events at one instant** produce one stop and one decision window that lists all of them (B:33).
- **Refused orders** are recorded as refused at their instant and change no state (verification fix F6, B:167).

### 3.6 Event-triggered decision windows and interruption rules [DIR: windows, T-D6, T-D9; DELEG taken: the rules; OPEN: the list]
- **Stop rule.** When a significant event executes at instant T, the runner finishes every event at T with class below
  `PlayerOrder` (250), then stops and returns `Interrupted(T)`. The world is then exactly the world at T after everything
  that happened at T (B:33; mutant M6). Orders issued while stopped are delivered at T (§3.7). The runner then resumes the
  same turn.
- **Significance is data.** A set of event kinds (`SignificantKinds`), independent of class (B:32). Which kinds are in
  it is **[OPEN]**: an engineering proposal goes to the Director for approval (T-D8; D-079 Q2). Until the Director
  approves a list, no shipped content kind is significant; tests use synthetic kinds, as Slice B did (B:17).
- **The split is a function of world state only** (D-076 Q-T4 alternative (a), taken as [DELEG]; C2). A turn is split at
  every significant event whether or not anyone looks at the window or issues an order. The sub-step schedule, and so
  every RNG draw (§3.9), never depends on a human's presence, the UI, a presentation setting, or whether an order was
  issued. The Director already accepted the cost: every golden re-pins (T-D4).
- **Who gets a window [DELEG taken, D-079 D1; plan revision RDG-5, recorded there as DELEGATED].** A window opens for each polity for which the
  event is significant **and known** under its fog of war (T-D2: no exposure of distant wars; knowledge through
  observation). AI polities receive the same windows under the same rules as the player: Spine Law 7, "AI actors use
  player-identical verbs and information class" (`civ-sim-architecture-v3-outline.md:25`). A window that opens for no
  human polity is invisible to the human: the UI pauses only for the human's own window, so a pause can never reveal an
  event the human cannot know.
- **Windows do not multiply actions.** The two-action maximum per decision window (T-D11) is integrated with Action
  Capacity under A-35 and contract C23; event frequency must not become an action multiplier (D-079 R8, a reading flagged
  for Director confirmation; plan revision RDG-10). The kernel's part
  is only to give each window a stable identity: (turn, instant, polity).
- **Replay never stops** (B:16, B:34). Interrupts are recorded on the runner, not in the trace, because the replay
  re-creates the same split from state.

### 3.7 Order delivery at an interrupt [DELEG taken; each semantic gets its own instant-exact pin]
- An order issued at a decision window at instant T is **stamped T** and executes in class 250 at T, after every world
  event at T, which is exactly the state the issuer saw (`Proto.M7/Events/EventKey.cs:14–16`; B:16).
- It is consumed by the sub-step that starts at T: its consumer writes `Next` of that sub-step, and other systems first
  read the effect in the following sub-step (the one-turn lag becomes a one-sub-step lag for window orders).
- **Boundary orders keep ADR-005's semantics.** An order stamped at the turn start (all orders today) is delivered to the
  first sub-step of turn t, the step executing from turn-t state (`adr-005-canonical-schema.md:40`, retained). In a turn
  with no interior boundary nothing changes. In a split turn, documented lags such as `SetTaxRate`'s "first effect … lands
  in the step t+1 → t+2" (`OrderLog.cs:66–68`) are restated per sub-step, and every existing delivery pin is re-checked.
- **Validation.** World-dependent validation runs at delivery, per sub-step (ADR-034's deferral rules carried to the
  sub-step). A logged order whose instant is not a decision-window instant for its actor in that turn is refused as an
  `OrderValidationException` with an actionable message. A hand-made log therefore cannot force a split that state did
  not produce.
- **Pins (`CLAUDE.md:50`, T1.9 precedent).** One instant-exact delivery pin per delivery semantic: window order, boundary
  order, War Pulse order. Slice B's model is the Taj Mahal literal test: the rushed process completes at
  `291_600_000_000` ms, which a turn-end or 1-ms-late delivery cannot produce (B:70–71; mutant M2).

### 3.8 Order log, replay and persistence [DIR: preserve order logs, replay, scheduling, T-D6; DELEG taken unless marked]
- **Stamp.** The order log keeps one artifact and one append order (the D-008 recovery path). `OrderRecord` gains the
  instant within its turn (an integer ms offset `0 ≤ o < L_turn`; 0 for a boundary order) and keeps its other five
  fields. That changes the fixed shape (`OrderLog.cs:5`), which this ADR proposes (landing subject to D-079 §5 Q5), and bumps `OrderLog.IoVersion`
  from 1 to 2. **Version-1 logs stay readable as offset 0**, because the fixtures are version 1:
  `Sim.Tests/Fixtures/first-reign-orders.bin`, `t38-director-orders.bin`, `v32-fixture-orders.bin`,
  `v33-fixture-orders.bin`, and the recorded sessions under `Sim.Tests/Fixtures/` and `Sim.Ui.Tests/Fixtures/`.
  `Append` orders on (turn, offset). Slice B's sibling `SubTurnOrderLog` (`Proto.M7/Events/SubTurnOrderLog.cs:11`) is
  prototype-local and is not adopted. B:125 named both options; a single log avoids two append orders and two replay
  paths.
- **Replay.** `replay(seed, orderLog)` pre-schedules every logged order at its instant and never stops (B:16). It must
  reproduce the run hash for hash at every turn boundary and at every decision window. Slice B measured it: a live run with
  3 interrupts and orders in 3 places (one refused) and its replay give equal state and trace hashes while their raw
  `Seq` differ; moving the same orders to the turn end changes both hashes (B:72–74).
- **Session trace and guards.** `SessionTrace` gains a row per decision window, so the first drifting window is named, not
  only the first drifting turn (recommended). `ReplayGuard` (ADR-041) and `ReplayProof` (ADR-042) check each delivered
  batch per sub-step instead of per turn.
- **Saving while stopped at a window.** Two designs are admissible: (i) the save is the last turn-boundary snapshot plus
  the order log, and resume replays to the window (no snapshot format change); (ii) a snapshot at the window instant,
  which puts the in-turn instant in the clock and the header. **Recommended: (i)** for the first integration. Both
  satisfy C11's test "save mid-window, reload, identical continuation".
- **Schema.** Any state that must survive a turn boundary (an in-progress War Pulse, carries of §3.10, the in-turn instant
  under design (ii)) is serialized through `CanonicalSchema` with a version bump (v39 → v40), new sections appended,
  `ExpectedLength`, `BytesAppendedSince` and the readable set updated (D-056 logic, `CanonicalSchema.cs:246`;
  `ReplayGuard.cs:34`), and a POPULATED-table test per new row type (`CLAUDE.md:49`). **Recommended:** the campaign
  scheduler's pending events are re-derived from world state at each sub-step start, so the queue itself is not
  serialized.

### 3.9 RNG-consumption rules [DELEG taken under T-D10]
1. **Campaign streams are unchanged.** `RngRegistry.Get(system, region)` keeps one PCG32 stream per (system × region)
   with its state in `WorldState` (D-007; `RngRegistry.cs:57`). A system draws when it runs, in fixed pipeline order, so
   streams are created and consumed in a deterministic order inside each sub-step.
2. **Draws never depend on presentation or player presence.** Because the sub-step schedule is a function of state only
   (§3.6), the number and order of draws does not depend on whether a window is shown, whether an order is issued at it,
   or whether the polity is human. C2's "window-independence" test pins it.
3. **Event-scheduled randomness is counter-based.** Randomness consumed by scheduled events (War Pulse resolution,
   battles) uses `Draw(seed, stream, a, b, c, index)`, which folds its inputs through the SplitMix64 finalizer with the
   constants of `Sim.Core/Kernel/SplitMix64.cs`, pinned by a test with Vigna's reference output (B:18). A draw is a pure
   function of its keys, so event reordering cannot change it (B:82, measured over 10k draws in 3 shuffled orders).
   Retries use an attempt fold, never `index + k` (B:39). **The seed is drawn once from an `RngRegistry` stream**, so all
   randomness still originates in a stream whose state lives in `WorldState` (Law 5). This is an addition to the RNG
   regime that S8 freezes (`spine-s8-governance-freeze.md:15`), proposed here under delegation; it lands only with the rest
   of this ADR (D-079 §5 Q5).
4. **Presentation never draws simulation RNG** (T-D12): the camera, cosmetic animation, the avatar and cinematics use
   their own generators, which never touch `WorldState` or a hash.
5. **No platform-dependent seed material.** Seeds and stream keys are integers mixed explicitly. Nothing derives from
   string hashing, time, addresses or `GetHashCode` (Law 5).

### 3.10 The proportional fractional carry [DIR: T-D7; DELEG taken: the exact rule, contract C20]
- **The decision.** "Preserve fractional time proportionally when campaign-turn duration changes. Do not discard leftover
  time." It closes ADR-019 §1.3's recorded gap as to leftover time (`adr-019-architecture-constitution-addendum.md:74`).
  That record's "immediate, with notification" stays a stated preference, not a ratified rule.
- **The rule adopted is C20 (rules T1–T5), recorded there in full and adopted here by reference:**
  - T1: time is never fractional. Every instant is an integer, and a turn's length is fixed at its start, so a change of
    duration takes effect at the next turn start.
  - T2: elapsed time is carried by the instant. A decision window consumes zero simulated time and does not move
    `S_{n+1}`. A War Pulse keeps its length W whatever it straddles, and its parts sum to W exactly.
  - T3: every integer quantity accrued from a per-sim-year rate integrates as `Q = ⌊(ρ·Δt + c)/D⌋`, carry
    `c′ = (ρ·Δt + c) mod D`, in `Int128` intermediates, with D independent of the turn length. A duration change
    therefore leaves the carry unchanged: proportional preservation with zero loss. The same identity makes integration
    split-invariant over any partition of the turn.
  - T4: for the few quantities denominated per turn (agency budgets under A-35, the prototype's demonstration rates), the
    carry is held over `D_T` = the lcm of every turn length the era table can produce. For the shipped table that is
    lcm{3600, 1800, 1080, 720, 360, 180} days = 10,800 days = 933,120,000,000 ms, so `c/D_T` survives any change of
    turn length exactly. The era-table loader computes `D_T` from data and rejects a table whose `D_T` does not fit in
    `long`.
  - T5: systems that stay `double` (D-004, retained for campaign systems) receive dtYears from exact integer endpoints.
    Their banked remainders are fractions of a unit, not of time, and are never rescaled.
- **Superseded mechanism.** Slice B's `⌊carry · L_new / L_old⌋` (B:40; `Proto.M7/Demo/CampaignDemoWorld.cs:131`) drops up
  to one numerator unit at each band change. It answered the prototype's Open question 4 ("rescaled … or dropped", B:136),
  which DIR5 closes. It is replaced by T3/T4, and a mutant that restores it must be killed by a semantic test (C20).
- **[READING]** "Proportionally" is read as: the leftover keeps the same proportion of the quantity it measures. On the
  global timeline nothing is converted, so nothing can be lost; for remainders denominated per turn, the proportion is
  carried exactly.

### 3.11 War Pulses across turn boundaries, and per-pulse economics [DIR: T-D2, T-D4, T-D5; DELEG: mechanism]
- **Schedule [DELEG taken, as prototyped].** A war's pulses are scheduled on the global timeline from the war's own start
  instant, independently of turn boundaries; two wars have independent pulse phases (C19). A pulse takes the length of
  the era band that contains its **start** instant and keeps it to its end (B:41; verification F4, B:165). No pulse
  starts at or after the campaign end, but one may run past it (F7, B:168).
- **Crossing [DIR: "Use sub-stepping"].** A pulse that straddles `S_{n+1}` is split at the boundary into one part in each
  turn. Its identity and in-progress state persist in war state across the boundary (serialized per §3.8). Action Capacity
  for the involved military agents refreshes at **pulse start, not at the turn boundary** (ledger §19,
  `docs/design/director-architecture-and-decision-ledger.md:949`; B:80; mutant M12). Slice B measured crossing
  invariance: identical trace and state hashes for one span, for turn-by-turn runs, and for a calendar with entirely
  different (1-year) turn boundaries (B:77–80).
- **Locality [DIR: T-D2].** Pulses belong to a war and its participants. A non-participant learns of a pulse's events only
  through observation by its own units or cities within explored areas. C19's fog metamorphic test (a non-observing
  polity's knowledge and windows are identical with the war present or absent) and its isolated-polity test are
  acceptance conditions.
- **Per-pulse economics [DIR: T-D5].** Economic effects of the war — supply, upkeep, attrition, movement consumption,
  siege supply — settle at each operational pulse through `Ledger.Transfer`/`Ledger.Flow`, with exact conservation at
  every sub-step boundary (Law 1). ADR-019 §1.2's prohibition of intra-turn economic causal chains is superseded in part
  (D-079 S118, the dated note on `adr-019:51` appended in the same pass); its retained part is that sub-calculation inside one step stays private and
  no system keeps a private economic clock (ADR-019 §1.1, as narrowed). **[DELEG pending]** Which systems settle per
  pulse, and the interrupt cost model (D-079 D4), are decided with Phase C evidence.
- **Durations [OPEN]**, proposal due (D-079 Q1). The key the duration varies on follows READING R2.

### 3.12 What stays private, and what presentation may do
- Sub-calculation inside one system's step (for example `DemographicsSystem`'s half-year micro-step,
  `adr-019-architecture-constitution-addendum.md:58–63`) is not a sub-step. It stays private and unobservable, as ADR-019
  §1.2 says; only sub-step boundaries are observable states.
- Presentation reads published state between authoritative steps and never writes it. Cosmetic animation may fill the
  time between steps but never alters timing, RNG, state or hashes (T-D12; ADR-062 §6, R-6).

---

## §4 Golden re-pin obligation [DIR: T-D4; DELEG taken: the procedure]

- **Decision.** "Every golden replay must be re-pinned following the relevant changes" (DIR5:133).
- **Procedure.**
  - One re-pin per landed change. Each move gets a row in the golden attribution register
    (`docs/m6-plan/golden-attribution-register.md`, or its M7 successor) naming the measured cause and a control that
    returns the old value exactly: a switch-off twin, or a schema projection that strips the new sections. That is the
    register's own practice (rows 7 and 10 are layout-only moves).
  - Each value is measured on Linux and on Windows. The CR-013 reopening record (`docs/adr/cr-013-reopened-2026-11-05.md`)
    requires Windows/Linux equality; until it is measured, Linux x64 values are CI's pin instrument (D-079 R14, DELEG).
  - A claim in a commit message or report is one the writing agent measured (`CLAUDE.md`, ADR-015 §6).
- **Golden files that will move.** Which ones move depends on the change. A schema bump that appends sections moves
  every literal computed with `ComputeHex`, but not schema projections. Behavioural splits move only the worlds in which
  a split occurs: the shipped goldens have no significant kinds and no wars today, so that set may start empty. This is
  INFERRED, not measured.
  - **Headline pins:**
    - `Sim.Tests/Kernel/SnapshotTests.cs:1184` `FoundedGoldenHash` `d5ebd0b2…`, and its CI copy
      `.github/workflows/ci.yml:266` `FOUNDED_GOLDEN`, kept equal by `CiPinAgreementTests`.
    - `Sim.Tests/Kernel/DrivenGoldenTests.cs:118` `Golden` `55afd498…`, `:121` `GoldenPreEconomy`, `:127` `GoldenAtV32`.
    - `Sim.Tests/Systems/FirstReignTests.cs:58` `PostR1Golden` `064ed6b7…`.
    - `Sim.Tests/Kernel/AiFoundingAttributionTests.cs:129` `S1cTurn300` `5458cb3d…`.
    - `Sim.Tests/Kernel/SnapshotTests.cs:1201` `ToyGoldenHash` `ff4ea411…` and `:1214` `PreD054FoundedGoldenHash`.
  - **Every other pinned world hash:** 139 distinct 64-hex literals in 22 files of `Sim.Tests` and 14 in 5 files of
    `Sim.Ui.Tests`, counted by grep on 2026-11-05. They include the attribution twins (`*AttributionTests`), the session
    and export tests (`SessionExportTests`, `Batch2SessionResumeTests`, `CylinderBakeTests`, `EraSeparationTests`,
    `SettlementCompositionTests`) and the secondary pins of the register's §3.
  - **Order-log byte pins:** if the writer emits version 2, the four `sha256` values at `.github/workflows/ci.yml:448–451`
    (`alloc*.bin`, written by `scripts/gen-sample-orders.py`) and the same bytes in `AllocationCiLegTests` move. They hold
    if version-1 bytes stay the writer's form for logs with no window orders (DELEG pending).
  - **Fixtures:** `Sim.Tests/Fixtures/*.civsnap` (v32, v33) and the recorded sessions must still load and replay under the
    readable-set rules, or be refused with a clear message (D-056); they are not re-recorded.
  - **Prototype goldens:** the Slice B replay state and trace hash literals (B:75, measured on Linux only; Windows
    UNTESTED).
  - **Nightly corridors and the calibration battery** are re-measured, not re-pinned blindly.
- Semantic pins with turn numbers (the AI decision turns in `AiEmpireIntegrationTests`, `AiPolicyTests`) are re-checked as
  semantics. A moved semantic turn needs its own attributed cause, not a silent update.

---

## §5 Kernel files that will change at integration (read on 2026-11-05)

"Certain" = required by a decision above. "Conditional" = depends on a delegated choice not yet taken. Nothing below is
changed by this ADR.

| File (`Sim.Core/Kernel/`) | Today | Change at integration | |
|---|---|---|---|
| `TurnExecutor.cs` | `Step(prev[, observer])` runs one whole turn (:114–151); one batch from `BatchFor(prev.Clock.Turn)` (:130); `SystemRegistration.Invoke(prev, next, rng, dtDays, dtYears, batch)` (:17) | Becomes the sub-step runner: scheduler, `RunUntil` with `Interrupted(T)` and resume, one clone per sub-step, a batch per sub-step, the sub-step's length passed as dt. `ITurnObserver` phases tagged per sub-step (ADR-007's observed-phase set changes; proposed here, landing subject to D-079 §5 Q5) | Certain |
| `SimContext.cs` | `DtDays`, `DtYears`, `Orders` of the turn (:38, :41, :43) | Carries the sub-step's length (ms and years) and keeps the campaign-turn length for turn-keyed grants (§3.3); `OrderBatch` per sub-step; the window identity where a consumer needs it | Certain |
| `OrderLog.cs` | `OrderRecord` five fields (:330), "shape … is fixed" (:5); `Append` in turn order (:359); `BatchFor(turn)` (:369); `IoVersion = 1` (:385); v1 only (:414) | In-turn offset added; `IoVersion` 2; v1 read as offset 0; append order on (turn, offset); batches per (turn, instant); the documented delivery lags restated per sub-step | Certain |
| `OrderValidation.cs` | turn-0 world validation, future-actor deferral for `Turn ≥ 1` | Offset range check; window-instant check at delivery (§3.7); deferral rules carried to sub-steps | Certain |
| `ReplayGuard.cs` | checks a batch against the session trace's row for its turn; readable-schema list (:34) | Checks per sub-step batch; readable list gains v40 | Certain |
| `ReplayProof.cs` | `BeforeDelivery` per turn's batch | Per sub-step batch | Certain |
| `CanonicalSchema.cs` | v39 (:231); clock = three longs (:365, :373–376); readable set (:246) | v40 with appended sections for state that crosses a turn boundary (war-pulse progress, carries); readable set, `ExpectedLength`, `BytesAppendedSince` | Certain once such state exists |
| `SessionTrace.cs` | one row per turn | A row per decision window (recommended) | Conditional |
| `ReplayReport.cs` | per-turn report rows | Window rows, if `SessionTrace` gains them | Conditional |
| `EraTable.cs` | `DtDaysAt` (:31), `AdvanceTurn` (:46) | Gains turn-start, turn-of and position-in-turn mapping and `D_T` (C20 T4), or a sibling calendar file; the dt rule itself is unchanged | Certain (here or a new file) |
| `EraTableLoader.cs` | exact-number band validation | Computes and bounds `D_T`; rejects a table whose `D_T` overflows `long` (C20) | Certain |
| `SimClock.cs` | `(Turn, SimDays, DtDays)` (:10) | Unchanged under save design (i); gains the in-turn instant under (ii) | Conditional |
| `Snapshot.cs` | header `{magic, version, seed, turn}` (:10) | Unchanged under (i); header must locate the in-turn instant under (ii) | Conditional |
| `ConservationAuditor.cs`, `FoodAudit.cs` | one-call exact identity on a world (the harness runs it every turn); the food audit works across a turn or any pair of phase checkpoints | Probably no code change: the harnesses call them at every sub-step boundary, so conservation is asserted exactly there (Law 1). The food audit gains sub-step checkpoints if its reports need them | Conditional |
| `PipelineLoader.cs` | pipeline order as data | A per-system declaration of per-pulse settlement, if C21 chooses data over code | Conditional |
| `RngRegistry.cs`, `SplitMix64.cs`, `Pcg32.cs` | as §1 | API unchanged; the counter generator (§3.9 rule 3) reuses `SplitMix64` constants | No change |
| `WorldHash.cs` | SHA-256 of the canonical stream | Code unchanged; values move (§4) | No change |
| `Ledger.cs`, `ConservedMath.cs`, `TopologyFlags.cs`, `CorridorStatus.cs`, `AutoplayMetrics.cs`, `SessionManifest.cs` | — | No change expected from the time model | — |

**New kernel files** (ported from the prototype under the packet's own acceptance tests; names indicative): the integer
timeline and calendar (`Proto.M7/Time/SimInstant.cs`, `CampaignCalendar.cs`, `ExactNumberText.cs`); the exact accrual
(`RateIntegrator.cs`, rewritten to C20 T3/T4); the scheduler (`Proto.M7/Events/EventKey.cs`, `EventHeap.cs`,
`EventScheduler.cs`, `EventTrace.cs`, `TombstoneSet.cs`, `SignificantKinds.cs`, the runner logic of `Runner.cs`); the
counter generator (`Proto.M7/Rng/CounterRng.cs`); and the exact integer helpers they need (`Proto.M7/Fixed/FixedMath.cs`).
`SubTurnOrderLog.cs` is not ported (§3.8).

**Outside `Sim.Core/Kernel`, in the same blast radius:**
- `Sim.Core/SystemCatalog.cs`: builds every `Invoke` lambda with `dtDays, dtYears` (:138 onward).
- `Sim.Core/Observability/SplitProbe.cs`, `TurnRecord.cs` and `Forensic/ForensicSchema.cs`: observer phases and
  per-turn records.
- `Sim.Cli/Program.cs` and `Sim.Cli/ResearchCli.cs`: the run, replay and inspect loops.
- `Sim.Ui/UiSession.cs` and `Sim.Ui/Headless/PlayabilityGate.cs`: the step loop must handle `Interrupted(T)` and issue
  window orders.
- `scripts/gen-sample-orders.py`: writes version-1 logs.
- The systems that own per-turn grants (ADR-030, ADR-050, ADR-051, ADR-054).

---

## §6 Related records and the notes they need [ADMIN]

Dated notes on the superseded clauses were appended by the D-079 pass (W1; D-079 §7) to `docs/m0-kernel-spec.md`
(S105–S109), `docs/adr/adr-005-canonical-schema.md`, `docs/adr/adr-019-architecture-constitution-addendum.md`,
`docs/adr/cr-006-continuous-time-and-campaign-epoch.md` and the Spine. This ADR edits none of them. Pointer notes still owed
when the corresponding code lands (D-079 A-44):
- ADR-002 (the clock at turn boundaries; §3.1);
- ADR-007 (the observed-phase set; §5);
- ADR-020 (one clone per sub-step);
- ADR-030, ADR-050, ADR-051, ADR-054 (per-turn keying; §3.3);
- ADR-034, ADR-041, ADR-042 (per-sub-step validation and guards; §3.7, §3.8).

CR-006 §1 and CR-017 §6 are ruled (D-076 N2, confirmed by DIR5 §2.4). CR-006 §2 (the 10,000 BCE epoch) is untouched.

---

## §7 What is not decided here

| Item | Category | Where it is decided |
|---|---|---|
| The significant-event list | [OPEN] | Engineering proposal, then Director approval (T-D8; D-079 Q2; OD-4 (i)) |
| Era-specific War Pulse durations | [OPEN] | Proposal after the Phase C tests (T-D3; D-079 Q1; OD-4 (ii)) |
| Tactical battle pulse length | [OPEN] | D-076 Q-B1; OD-4 (v) |
| Landing in `Sim.Core/Kernel` before the architecture review | [OPEN] | D-079 Q5 / OD-1 (one question). Engineering recommendation, the same in both: (b), keep the code in the prototype until the Director answers; (a) and (c) are stated there |
| Locality mechanism (§3.3), which systems settle per pulse (§3.11), interrupt cost model | [DELEG pending] | Phase C items 1–2 evidence; amendment log of this ADR (D-079 D3, D4) |
| Nonlinear integration under sub-steps | [DELEG pending] | D-079 D3 |
| Saving at a window: design (i) or (ii) | [DELEG pending] | §3.8; recommended (i) |
| Version-1 writer form for logs with no window orders | [DELEG pending] | §4 |
| Legal action combinations within a window | [DELEG] | A-35, contract C23 (D-079 D5); not this ADR |

---

## §8 Prototype evidence: what Slice B proves, and what it does not

- **Proves (measured on Linux x64, .NET 10):**
  - integer timeline and tiling over 1,630 turns;
  - the ordering key, with a tie-dense heap test: 6,000 events, about 167 per prefix, 5 permuted insertion orders,
    checked against an independent multi-key sort;
  - simultaneity, invalidation and cancellation;
  - the stop rule;
  - the Taj Mahal literal and the instant-exact delivery pin;
  - replay equality with teeth;
  - piecewise invariance of linear accrual over 200 random cut sets;
  - War Pulse crossing invariance;
  - counter-RNG order independence;
  - exact integer arithmetic against Python big-integer goldens (B:53–86).
- **Mutants:** 14 of 14 author mutants killed, each by at least one semantic test. In the verification round, 4
  survivors of the original suite (MC, MD, MH, MK) and 4 fix-reversion mutants are all killed by new semantic tests
  (B:90–107, B:187–198). Each mutant run was bounded at 5× the clean run; none hung.
- **Current count:** Slice B's 123 tests are part of `Proto.M7.Tests`. That suite passed 335 / 0 / 0 in Debug and in two
  Release runs on 2026-11-05 (Phase B audit, D-079 §1.6). Performance: 3.3–5.6 M events/s steady state, and the
  1,630-turn campaign demo with 1,000 processes and 777 interrupts in about 425–530 ms (B:115–121, B:202–205).
- **Does not prove:**
  - **Windows equality** (UNTESTED, B:75);
  - sub-stepping of the real campaign pipeline (B:127: the campaign was kept turn-atomic and only linear processes were
    integrated);
  - the C20 exact carry (the prototype still has the superseded floor);
  - per-pulse economic settlement;
  - integration with `RngRegistry` and `OrderRecord`;
  - the locality tests of C19.

  These are Phase C items 1–2 (DIR5:637–638).

---

## §9 Acceptance conditions for the integration packet (when authorized)

These are added to the packet's own acceptance tests; they do not replace them.
1. Tie-dense ordering test (`CLAUDE.md:48`), plus causality-rejection, mutual-kill, stale-drop and cancellation tests.
2. One instant-exact delivery pin per delivery semantic (window order, boundary order, War Pulse order) (`CLAUDE.md:50`).
3. Replay equality at every turn and every window, with teeth (the same orders moved change both hashes).
4. An unsplit turn equals today's step, proven by the switch-off twin and by schema projection.
5. War Pulse crossing invariance (one span = turn by turn = a different calendar).
6. C20: zero-loss carry across every band edge, checked against an independent big-integer computation; the floor mutant
   killed by a semantic test.
7. Window independence: with no orders, a run with windows shown equals a run with windows suppressed by a presentation
   setting, in state and trace hashes.
8. Exact Ledger conservation at every sub-step boundary; grant non-multiplication under k sub-steps.
9. C19's fog metamorphic and isolated-polity tests.
10. A POPULATED-table test for every new serialized row type (`CLAUDE.md:49`); order logs of version 1 still load.
11. Windows and Linux both measured for every re-pinned value (CR-013 reopening record).
12. Mutants bounded at a stated multiple of the clean run, with at least one semantic kill each (`CLAUDE.md:51`, `:53`;
    ADR-015 §7).
13. `sim bench` before and after (the hot-path rule).

---

## §10 Priced-override fields (`docs/spine-s8-governance-freeze.md:37`), recorded as administrative

1. **Frozen items that change at integration** (proposed; landing OPEN, D-079 §5 Q5). Kernel contract §3.2, §3.3, §3.4 and §3.9 (`m0-kernel-spec.md:66`, `:68`, `:70`, `:88`) and
   ADR-005's turn semantics (`:38–41`), to the extents in §3. The S8 kernel code contract (`spine-s8-governance-freeze.md:15`):
   `SimContext`, the double-buffer model (once per sub-step), the RNG regime (one derived generator, §3.9) and the
   snapshot format (v40). ADR-007's observed-phase set. The Spine's sub-step rule, read as "sub-step-start" (D-076 S64).
   **Not changed:** the kernel freeze itself (`m0-kernel-spec.md:118`), Laws 1, 3, 4, 5 and 7, the `Ledger` API, the ban
   list, era-table dt selection, and ADR-002's integer-day storage at turn boundaries.
2. **Evidence.** A Director decision (DIR5 §2.4–§2.5), not a measured defect. Feasibility evidence is Slice B (§8).
3. **Options.** None are offered: the Director has ruled. The delegated mechanism is §3.
4. **What breaks, and which tests and docs change.** §4 (every golden that moves, the fixtures, the CI byte pins) and §5
   (the files). Docs: the pointer notes of §6, the M7.3 contracts C1, C2, C5, C11 and C19–C21, the golden attribution
   register.
5. **Schedule price.** Stated as what changes, not as a duration, since no packet duration has been measured. Two
   packets carry it: the kernel integration (M7.3 contracts, then M7.14 for War Pulses and per-pulse economics), and one
   attributed re-pin of every moved golden, measured on both platforms. M7.14 and every campaign decision window are
   gated on this integration (plan revision DC-9). The battle framework's internal clock is not gated by it. No other
   milestone moves.

## Amendment log

- 2026-11-05 — written (D-079 A-31; W3). Not yet reviewed by an independent verifier.
- 2026-11-05 — Phase A fix pass, after an independent verification of the Phase A records: the status separates the
  accepted Director decisions from the proposed mechanism and the OPEN landing (no claim of kernel-freeze sign-off);
  the two-action rules are contract C23, not C4; the D_T value is 933,120,000,000 ms; the landing recommendation is worded
  as in D-079 Q5 and OD-1; RDG-5 is DELEGATED; R8 is flagged for confirmation; the `main` reference names `github/main`.

## Dated note — 2026-11-05 (D-080; appended, the text above is unchanged)

- D-080 records the Director's answers to the T2 War Pulse questions: boundary-instant orders apply to the pulse or turn starting at that instant (WA1); a pulse crossing an era change keeps its starting era's length, and era bands change only at turn boundaries (WA2, WA2-F); non-participant observers may see battle results as a click-to-open notification (WA3); per-pulse economics and insolvency are delegated with a realism criterion (WA4), narrowing §3.11 "[DELEG pending]". See `docs/d080-war-pulse-director-answers-2026-11-05.md`.
