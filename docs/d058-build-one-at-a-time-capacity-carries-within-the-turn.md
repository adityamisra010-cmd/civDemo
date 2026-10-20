# D-058 — BUILD ONE AT A TIME, BUT NOT ONE A TURN: A TURN'S CONSTRUCTION CAPACITY CARRIES DOWN THE QUEUE

**Status: IMPLEMENTED 2026-10-19 on branch `b5-cons` (clone `/home/user/m5w/b5-cons`), LOCAL only — not pushed, not
merged, not published.** It awaits the Director's acceptance like every packet. Base: `827337e`.

> **Status note — 2026-10-19 (later; supersedes the "LOCAL … not pushed, not merged" status above, which is kept as written):**
> merged into `claude/new-session-yavghm` at `6d1988f` and pushed to that branch; CI run 39270568141 is green on every
> required job. Not on `main`. The Director's acceptance is still pending.

## 1. The ruling (verbatim, 2026-10-19)

> "Build one at a time, so that if 6 can be built and an order of 9 is give, and in that turn, resources are available
> for 1 more and then some, it should show in the next turn as 7 build, 1 under construction, 1 not started"

- **Context.** In the Director's playtest three granaries ordered at one settlement completed one per turn although one
  turn's capacity could have finished more ("they built one by one turn by turn";
  `docs/m5-resources-view-proposal.md` §5).
- **Meaning implemented.** Construction stays SEQUENTIAL: one project of a settlement's queue at a time, in queue order.
  But one End Turn's building capacity (builder labour × dt, and the materials the purse may spend under the batch-4
  set-aside rules and the building/roads split) is no longer capped at one project. When the head completes, what is
  left of that turn's capacity continues to the next entry, and so on until the capacity or the materials run out. The
  entry that receives some but not all is part-built ("under construction"); later entries are not started.
- **Standing.** A Director ruling on a game rule. It supersedes, for completions, D-055 §1.5's row "Next slot promoted
  after a completion — the promoted slot holds from the NEXT step". No frozen item is in conflict (§6), so no CR.

## 2. Root cause of "one project per turn" (base `827337e`)

- `Sim.Core/State/ConstructionQuery.Incremental.cs:121` — `LabourBound` is `min(N − p, floor(C / L × N))`: the head's
  progress is clamped at completion, and the capacity it did not use, `C − L × (N − p) / N`, is passed to nothing.
- `Sim.Core/State/ConstructionQuery.Holds.cs:244` — the holds walk (the one walk the step, the UI estimate and the
  split's forecast share) walks `HeadsInBuildingOrder` once: ONE entry per settlement, its lowest slot
  (`HeadIndex`, `ConstructionQuery.Incremental.cs:536`). An entry promoted by a completion is not a head when the walk
  starts, so it is first resolved in the NEXT step (D-055 §1.5).
- The capacity a head did not use was not destroyed: ADR-033 D10 publishes only the labour used
  (`ConstructionLaborRow`), and PathBuild banks the rest toward dirt paths. It never reached the next project.
- **Measured** (the switch-off twin of the new tests, `TestConfigs.PreContinueWithinTurn`): nine works ordered at one
  settlement with capacity for 7⅑ projects and materials for nine — the batch-4 rule builds 1 and leaves 8 not
  started (`ContinueWithinTurnTests.TheDirectorsExample_…`). The CLI's founded seed-42 colony (settlement 12, turn 3):
  capacity for about 5.2 granaries a step, 1 built a step.

## 3. The rule as built

- **Switch.** `sim.json construction.continueWithinTurn`, shipped `true` (IDENTITY: a rule, not a magnitude). The
  loader refuses it without `construction.incremental`. Control: `TestConfigs.PreContinueWithinTurn` (single layer).
  `TestConfigs.PreReservations` and `PreIncrementalConstruction` strip it too (the R4 rule: an older twin strips every
  newer layer), so a `PreReservations` world is still batch 3's byte for byte.
- **The walk** (`ConstructionQuery.HoldsWalk`; code `ConstructionQuery.Holds.cs`, loop end):
  1. A head that COMPLETES leaves `left = C − LaborUsed` adult-years (C: the settlement's capacity this step, the
     existing `CapacityAdultYears`, which integrates `dtYears`; for a continued entry, what the earlier ones left).
  2. The next entry of the same queue (the lowest slot above it) becomes a head IN THE SAME STEP if `left` can move it
     by at least one progress unit (`LabourBound(left, L', p) > 0`). Otherwise it is not started, exactly as before.
  3. It is inserted into the walk at ITS OWN place in the building order — the composite integer key (EnqueuedTurn,
     SettlementId, Slot), the D-055/D-057 priority contract; no double is ordered. Within one queue slots are issued in
     order-age order, so its key is above the completed head's and it lands after it.
  4. It is then resolved, labelled and set aside like any head: against the purse budget every older head's draws AND
     set-asides left (or the building grant under a split), drawing through the Ledger, and holding its remaining need.
- **The set-aside priority rule, restated for a continuation.** A continued entry may spend exactly what a head at its
  key may spend: the purse less every draw and set-aside of heads ahead of it in the building order. It never takes a
  unit an older head holds; an older-ordered continuation outranks a younger head of the same purse in that very step
  (as a cancel-promoted slot already did). The oldest unreleased head of each purse is still resolved against the whole
  budget (invariant vi): a continuation is never the first head of its purse's walk.
- **Not continued:** a project that founds an institution (its host's viability is read on PREV, which cannot count a
  founding completed in the same step; it is the head next step, as before); and anything behind it — sequential.
- **Labour publication** (ADR-033 D10): still ONE `ConstructionLaborRow` per settlement, now the sum of what its entries
  used in walk order. PathBuild's bank receives `C − Σ used`.
- **Road works and the split.** Road development still runs after construction and reads this step's published holds
  (the D-056 executor-enforced same-step edge, unchanged). Under a split the walk starts at G_b as before, so road works
  keep `≥ min(D_r, E_r)`. New: the claim D_b (the walk on S) counts continuations, and the walk under the grant can reach
  fewer entries than the claim's walk (another good stopped a head short of completion). Then building work spends its
  heads' need R < G_b and road works get the rest. `SplitReportCheck` P2 is restated as build + holds = min(G_b, R)
  (R = the row's unreleased heads' needs), which is G_b whenever R ≥ G_b — always under D-055 alone. MEASURED: the old
  P2 clause fails on the real S42-ALLOC-H report under the new rule, the restated one passes.
- **Coupling map (D-057 §9).** No new same-step read. Construction still owns `ConstructionHolds`, road development
  reads it after construction as before, and the published table is still the walk's per-purse sum.
- **UI.** The queue reflects the next state directly: completed entries leave, the part-built one shows "Being built",
  later ones "not started". `InfoQuery`'s card now locates a queued entry's own place in the walk
  (`SetAsideQuery.EntryIndexOf`), so a continuation's set-aside is not read as "ahead of" itself. The head line's
  estimate text is unchanged (no per-entry forecast for continuations: §9).
- **AI.** Unchanged code. The AI orders only into an EMPTY queue, one project at a time, so its queues never chain.
- **Unchanged:** the kernel, the save schema (v34), every serialized row type, the order kinds, the whole-or-wait gate
  (`incremental` false).

## 4. Laws

- Conservation: every unit moves through `EmpireStockpile.Draw` → `Ledger.Flow`; tests assert exact sinks and
  `ConservationAuditor`. Long stocks only.
- dt: capacity is `CapacityAdultYears(…, dtYears)`; carrying it changes no rate.
- Determinism: integer composite key for insertion; arrays/lists in walk order; no dictionary, no LINQ in the walk.
- Isolation: no system references another; the walk is the existing pure State query.

## 5. Tests added (Sim.Tests unless named) and mutation results

`Sim.Tests/Systems/ContinueWithinTurnTests.cs` (12):
- the Director's example, turn-exact: 9 ordered in one batch at turn 0, capacity 64/9 projects: at turn 1 seven built,
  slot 7 at exactly 111,111, slot 8 at 0; 285 timber and 143 stone sunk; set-aside 35/17; one labour row; at turn 2
  nine built, exactly 360/180 sunk; premise under the twin: 1 built;
- capacity exactly finishing k (C/L = 4 → 4 built, the fifth not started, nothing held; C/L = 1 → byte-identical to the
  batch-4 rule);
- materials binding mid-queue (7 built, the 8th at 500,000, the 9th not started, set-aside 10 stone);
- a set-aside held by an older project elsewhere binds the continuation (premise: without holds it takes them);
- the continuing entry takes its own order's place (ordered before / after a part-built head elsewhere);
- tie-dense: twelve entries tied on the turn, against an independent oracle of the composite key;
- a founding project is never continued, nor anything behind it;
- replay and a Snapshot resume mid-chain equal the uninterrupted run, hash for hash;
- road works under a split of 50 in the same step (road gets 50 ≥ min(D_r, E_r); stone spent = R = 40 < G_b = 60);
- the switch: shipped on, twins, loader refusal.

Also: `ConstructionReservationTests.ACompletion_D058_…` (shipped arm of #9), `SplitReportCheckTests.P2_AStepWhoseHeads…`,
`MixLegConstructionPremiseTests` (the ci.yml AI leg's cancel premise, §7), `AllocationScenarioD058AttributionTests` (§7).

Mutants (each killed by at least one semantic test; the suite under test ran < 2 s clean, each mutant bounded at 120 s;
none hung):

| Mutant | Killed by |
|---|---|
| M1 no carry (switch forced off) | 8 tests incl. the Director's example, materials mid-queue, set-aside, own place, roads, tie-dense |
| M2 carry the full capacity, not the leftover | Director's example, exactly-k, replay |
| M3 insert at h + 1, ignoring the order key | own-place (turn 5 row) |
| M4 a continued entry sets nothing aside | Director's example, materials mid-queue, roads, replay |
| M5 continue after any progress, not only completion | materials mid-queue, roads |
| M6 labour rows appended, not summed | Director's example |
| M7 founding projects continued | founding test |
| M8 continue with no capacity left | exactly-k (both rows) |
| P2 restatement reverted (`claimed < G_b`) | `P2_AStepWhoseHeads…`, `AllocationCiLegTests.SplitCheck_…` (the real report) |
| mix-leg generator at 3 granaries | `MixLegConstructionPremiseTests` |

## 6. Frozen items

None in conflict. The kernel contract, schema v34, the order kinds and closed D-decisions' frozen parts are untouched.
The game rule D-055 §1.5 recorded (a completion's promoted slot holds from the next step) is superseded by the
Director's ruling. Neither D-055 nor D-057 is rewritten: they carry no edit; this record and ADR-039's dated note say what
changed.

## 7. Moved pins, each with its cause

**No 64-hex golden moved.** The founded golden (`0f29efc7…`, ci.yml `FOUNDED_GOLDEN`; `CiPinAgreementTests` passes), the
toy, driven, first-reign and AI goldens all pass unchanged: in their worlds no completing head has a queued successor
(the AI never queues behind a head). ci.yml's pinned order-log sha256s are unchanged.

Moved (every one re-pinned with a dated D-058 cause block in place; the batch-4 value is reproduced under
`PreContinueWithinTurn` or `PreReservations`):

- **S42-ALLOC-H (the ci.yml allocation leg's scenario).** `AllocationScenarioD058AttributionTests` runs the scenario as
  shipped and under the twin. Worlds 0..118 are hash-identical; the first difference is world 119. In step 118 → 119
  settlement 0's first granary completes on its last stone. Its slot 1 (key 117, 0, 1, ahead of settlement 2's head 117,
  2, 0) continues in that step and takes the 17 stone the batch-4 rule gave settlement 2. The control (b = 100) first
  differs at world 118 (64 stone to building, not 40). On the twin every batch-4 literal holds.
  - Re-pinned: the stone table (D_b from 117; spending from 119), the heads of the hold window, holds at 130 and 133,
    progress at 130 and 132, Built at Z + 1, the control rows, X's D_b, the binding list (+120, −132).
  - Further: **Y moved 120 → 123**, Z's figures, W's D_b and its control (which now has a held continuation, s2:1),
    V's heads (settlement 2's two 128 granaries completed together at 131), and one timber unit in the kind-12 finding.
  - The ci.yml step's literals (P 116, X 117, W 128, K 130, V 132, the 136 twin's 51 vs 21) and its split-check
    properties hold. P2 needed the restatement in §3.
- **The integrated save/load battery.** Three granaries at turn 36 were all built in one step, so the turn-37 cancel had
  no entry. It now orders seven: 5 built, slot 5 at 150,000, slot 6 cancelled. This is after the last save point, so no
  save-point pin moved.
- **The deferred-colony cancel (CLI and UI).** Two granaries were both built in step 3 → 4. Now seven: 5 built, slot 5
  at 200,000, slot 6 cancelled, 6 built at turn 5.
- **The v32 fixture's migrated granaries** (both keyed −1) complete in one step (`ConstructionSchemaV33Tests`,
  `ConstructionFixtureContinuationTests` A/F/S; V32_A's promoted slot holds its 40 timber in the completing step).
- **`BuildingOrderTieDenseTests` (−1 arm).** Every entry keys −1, so settlement s's second entry outranks settlement
  s + 1's head. The batch-4 arm runs under the twin; the shipped arm is checked against an oracle.
- **`ConstructionReservationTests`.** #9 runs under the twin, with a new shipped arm. `APromotedSlot`'s completion rows
  run under the twin.
- **`ConstructionLifecycleResumeTests.AHeadWaitingForMaterials`.** s0's second granary continues to 150,000 and s2
  waits at 0. The batch-3 restatements in that file strip the new switch (R4).
- **Loader and twin-structure tests.** The new refusal and the R4 strip.
- **The ci.yml AI leg (`--founded-mix`).** Its world differs from world 11 (measured, 20 turns, old vs new CLI): the
  three turn-10 granaries are all built in step 10 → 11, and the turn-11 cancel named nothing (world 12 identical with
  and without it). The generator now queues 12 and cancels the last; `MixLegConstructionPremiseTests` pins it. The
  leg asserts no hash, so no CI literal moves. Its comment's AI stamps are batch 3's and were not re-measured.
- **Calibration (CR-020, owned elsewhere).** `Dev_MalthusCorridors_AllInBand`: seed 42 8.35755E-05 and seed 7 123,950,
  before and after alike — unchanged.

## 8. Session compatibility

- **Old sessions.** A session recorded by `231485e` (or any build before this) in which some completing head had a
  queued successor and capacity to spare diverges at that step. A resume or `sim inspect` refuses it with the existing
  incompatibility path. MEASURED: an old-CLI session with seven granaries at the colony verifies on the old build
  ("reproduction VERIFIED: 7 turns") and fails on this one ("REPRODUCTION FAILED at turn 4").
- **The Director's 68-turn session** (`session-20261019-144516-a1`, win-x64, recorded by 231485e). Its log holds 27
  orders. The player's are sector orders (kind 3) only; every construction order is the AI's, into empty queues.
  - The old build's and this build's Linux replays are identical on all 68 turns (final `7eac5d8c…`).
  - The rule change alone does not move it. Its only divergence remains the ADR-022 platform one (win-x64 against
    Linux from turn 2).

## 9. Limitations and what was not done

- No UI forecast says how far a continuation will get this turn; the queue shows the result after End Turn.
- A continuation's label (HeadStatus) is computed but the head line describes only the head.
- Windows build, GitHub CI and a human playtest were not run. The mix leg's 600-turn AI stamps were not re-measured.

## 10. Results (MEASURED on this tree, 2026-10-19, Linux; scratch results, not a certification — gov-4 §9)

- `dotnet build -c Release`: 0 warnings, 0 errors. Gates: check-banned-constructs, check-read-isolation and
  check-readonly-proof all OK.
- Sim.Tests: 2,643 tests, 2,628 passed, 13 skipped, 2 failed. The two are exactly CR-020's
  `Dev_MalthusCorridors_AllInBand`: seed 42 8.35755E-05 and seed 7 123,950, the base's values.
- Sim.Ui.Tests: 1,040 / 1,040.
- `Sim.Ui --smoke --ai-empires 1 --monkey 300`: SMOKE PASSED — 2,692 checks, 0 FAIL, 0 problems.
- `sim bench --seed 42 --turns 60 --founded`, three alternating rounds, quiet host (load about 1):

  | Build | Total, ms (3 runs) | Construction phase |
  |---|---|---|
  | base `827337e` | 7,482 / 7,174 / 6,999 | 12–17 ms, 31,400 B |
  | this tree | 7,022 / 7,113 / 6,780 | 12.6–12.7 ms, 35,240 B |

  There is no measurable time change. The construction phase allocates 3,840 B more over 60 turns (the walk's lists).


> **2026-10-20 — ACCEPTED by the Director (closure ruling R5; Director-reported Pass on the inferred `f80dce6` build, test
> evidence §5–§7).** The status notes above are superseded. Record: `docs/m5-exit-inventory.md` §13.
