# D-057 — THE IDENTITY PROOF, UNREADABLE SESSIONS, STRICT SNAPSHOT FIELDS, THE SMOKE AGE RIG, AND THE VALIDATED HANDOFF

**Status: IMPLEMENTED 2026-10-18 on `claude/new-session-yavghm`, in the development clone `/home/user/m5w/dev`.** It is
not merged to `main`, not pushed and not published, and it awaits the Director's acceptance like every packet.
- **Authority.** The Director's instruction of 2026-10-18, "Fix the Game: Independent Audit, Remediation, and Validated
  Handoff", delegated implementation authority to inspect, implement, test and commit locally. It said: "Where an
  architectural decision is genuinely required, follow the established governance process and document the decision
  and rationale. Do not misrepresent an unratified decision as previously approved."
- **Nothing in this record was approved by the Director before it was built.**
- **The governance trail:**
  - the conflict between frozen items is `docs/adr/cr-022-identity-proof-vs-d008-replay-recovery.md` (raised,
    implemented under the delegation, awaiting a ruling);
  - the contract is `docs/adr/adr-042-replay-identity-proof.md`;
  - D-056 and ADR-041 carry dated notes naming what this record supersedes. They are not rewritten.

## 1. The replay-integrity defect (D-056 §8's residual), reproduced and closed

- **The defect.** An order names its settlement (kinds 2, 3, 4, 9, 11, 13) and its issuing polity by id. An id allocated
  after turn 0 is the next free one: a colony gets settlement max + 1 (`ColonizationSystem.Found`), and a revolt-born
  polity gets roster max + 1 (`RevoltSystem`). Ids are never reused. In a replay that drifts from the recorded game,
  the same id can name a different colony or polity.
  - Before this record, the UI resume (for another schema's session) and `sim inspect` delivered such an order to
    whatever held the id.
  - The control check at consumption catches only the case where the player does not control the other entity.
- **Reproduced in-process** (`Sim.Ui.Tests/IdentityDriftResumeTests`, seed 42). The game is recorded under another
  build's siting floor, a TUNE number:
  - turn 2: a colony from the least populous settlement, refused there;
  - turn 3: a colony from the most populous, which becomes settlement 12;
  - turn 5: a sector order for 12.

  This build founds both, the first as 12. A replay without an identity check set the sectors of that OTHER colony,
  which the player also controls, without a word.
- **Measured on real older builds' sessions.** The replay drifts from the recording:

  | Session | Recorded by | This build's replay |
  |---|---|---|
  | v32, 300 turns, 2 AI | `de86ace` | Matches the recording through turn 205; differs from turn 206 |
  | v33 fixture | batch-3 build | Matches through turn 2; differs from turn 3 (the fixture's own premise W = 3) |
  | v32 fixture | batch-2 build | Reproduces through turn 8 |
- **The fix: the identity proof** (`Sim.Core/Kernel/ReplayProof.cs`; ADR-042 §1).
  - Before each recorded batch, every settlement and polity it names must be shown to be the recorded one.
  - Proof is a world matching the trace in some readable schema form at or after the id's allocation, or, for a
    polity, membership of the founded roster.
  - Otherwise the replay is refused before the batch is appended, with the order, the evidence and the remedy.
  - Kind 11 keeps D-056's stricter rule.
  - The runners are `UiSession.ReplayTo`/`Resume` and `sim inspect`.

## 2. Which older sessions resume, and which are refused

- **Resume unchanged:**
  - every session of this schema (v34) whose trace checks turn by turn. All its ids are proven every turn; the
    D-052 every-turn check is unchanged.
- **Resume (or inspect) with the D-008 notice:**
  - a v32 or v33 session in which every order names only settlements and polities that existed by the last turn the
    replay matched the trace in that schema's form. All three real sessions above `sim inspect` cleanly. The UI resumes
    of the two fixtures are pinned by `Batch2SessionResumeTests` and `V33SessionResumeTests`; the 300-turn de86ace
    session was checked with `inspect` only.
- **Refused, before anything is applied or written, with the diagnostic and exit 2:**
  - an order naming a colony or revolt-born polity founded after the replay first differs ("This replay matches the
    recorded game (its saved trace) through turn P and differs from turn F on, so the order could land on a different
    settlement …");
  - any order naming a settlement in a session whose founding differs (turn 0 does not match in any form);
  - any order naming a settlement in a session whose trace file is missing (§3).
- **The remedy named:** open the session with the build that recorded it (named from the manifest), put the missing
  trace file back, or start a new game. For `inspect`, inspect it with the recording build.
- **Compatibility impact.** It is narrow: it falls only on sessions in which a recorded order would otherwise have been
  delivered to an entity the replay cannot show is the right one. Sessions of this schema are unaffected.

## 3. A session without its trace (supersedes D-056 §2's missing-trace rule)

- **What changed.** With no trace, nothing shows that any settlement id names the recorded settlement, not even the
  founding's. A session whose log names a settlement is refused, and the remedy names the missing file. A session
  whose log names none (for example, research orders only and no AI) resumes with D-056's `MissingTraceNotice`.
- **Why it is stricter than D-056.** D-056 refused only kind 11 here. A missing trace is abnormal: every build since
  T4.17 writes it beside the log. Applying orders that cannot be proven to whatever holds their ids is the defect of
  §1.

## 4. The CLI's bare-log runners

- **Which runners.** `run --orders`, `run --from-save --orders`, `replay --orders` (with or without `--emit-session`)
  and `research --orders`.
- **What a bare log is.** A log with no session beside it names no recorded world. These runners replay it as this
  build's own run, assert nothing about any recording, and so have nothing to prove. That is unchanged, and the
  ci.yml legs depend on it.
- **Other schemas' session logs: refused** (new, `Cli.LoadOrders`). The log of a session recorded under ANOTHER schema
  (an `orders-X.bin` beside the `session-X.json` that names it) is refused before anything is read further or
  written.
  - **Before:** the `fd1fa39` CLI given the de86ace v32 session's log, `replay --emit-session`, founded the cylinder,
    wrote a manifest labelled v34 with the old log, telemetry and forensic files, and only then was refused by ADR-034
    at turn 42. That left a partial, valid-looking v34 session.
  - **Now:** exit 2, nothing written, and the message points to `sim inspect --manifest` and the game's resume.
- **This schema's session logs: unchanged** (the ci.yml pattern `replay --orders found-a/orders-*.bin`). They are
  replayed as this build's run.
- **Recorded residual.** A same-schema session from another build that drifted is replayed as-is by these runners.
  `inspect` and the UI resume check it.
- **Pre-existing, not changed here.** A bare-log `--emit-session` run that ADR-034 refuses mid-run still leaves its
  partial session directory. Such a directory has no trace, so §3 makes it unresumable whenever its log names a
  settlement.

## 5. `sim inspect` of an older readable schema reports the readable-form check

- **What it reports.** For a v32 or v33 session, the verdict line is now `SessionSchemaNotice.CheckedInRecordedForm`.
  It reports how far the replay matches the recorded game in that schema's form, and where it first differs.
- **What it replaces.** It replaces `NotChecked`'s "no replayed hash can equal a recorded one", which stopped being true
  when D-056 gave the build the older schemas' hash forms.
- **Unchanged.** `NotChecked` stays for a schema outside the readable set, and the platform lines are unchanged.

## 6. A saved session whose files cannot be read (new)

- **The cases.** A manifest that is not one, a truncated or foreign order log, a missing log, a malformed trace line, or
  a `--resume` argument naming no manifest.
- **Before.** Each ended the game launch as an unhandled exception. Measured on the D-056 tree: exit 134,
  "Unhandled exception. System.IO.InvalidDataException" / "System.IO.FileNotFoundException". A Windows player sees
  nothing.
- **Now.** Each is a `SessionUnreadableException` naming the file, reported like every refusal: the stderr line, the
  dialog once, exit 2. The files are only read.

## 6b. `--emit-session` never overwrites a session record (new)

- **The defect.** The run id is derived from the run's identity at launch, not from its length, so two different runs
  can share it. Measured: `run --founded --seed 42 --turns 2` and `--turns 3` both emit run 70ec7bc67542fcda. Into one
  directory, the second run silently overwrote the first's manifest, log, trace, telemetry and forensic record.
- **The fix.** A directory already holding `session-<runid>.json` is refused, as a usage error, before any file is
  written. `replay` now creates its emitter before its `--report-jsonl` file.
- **Unaffected.** ci.yml emits into fresh directories.

## 7. The snapshot reader refuses two more malformed fields

- **Boolean fields.** The two (the terrain flag, `AgeEligibility.Eligible`) must hold 0 or 1. Any non-zero byte used to
  load as true.
- **Command source.** A polity's command source must be 0 or 1. Any int used to be cast.
- **Unchanged.** The layout, the hash and every well-formed save. D-056's reader refusals stand: truncation, negative or
  impossible counts, trailing bytes, header disagreement.
- **Not checked by the reader.** The simulation's semantic invariants (duplicate ids, negative stocks) are outside its
  scope. A save is trusted to have been written by a build, as before.

## 8. The schema: v33/v34, the readable set, and no version bump

- **v33 and v34 stand.** They were taken by agent-recorded supersession (ADR-039, ADR-040) and confirmed by D-056 under
  its mandate. That confirmation is itself pending the Director's acceptance.
- **The readable set {32, 33, 34} is technically sound.** Each newer schema only APPENDS sections, so an older form is a
  prefix of this build's canonical stream (`CanonicalSchema.BytesAppendedSince`).
  - **New evidence here:** real traces written by the batch-2 (v32) and batch-3 (v33) builds, and by `de86ace` (v32),
    are reproduced hash for hash by this build in their own forms, up to the turn where the rules differ.
  - **The migrations are explicit and tested:** v32 and v33 fixture saves; `ConstructionSchemaV33Tests`;
    `ConstructionHoldsSchemaV34Tests`; `ConstructionHoldsV33FixtureTests`.
- **Governance status.** ADR-005's "no migration" rule was superseded for one step by D-054's dated note, and for the
  set {32, 33, 34} by D-055's dated note. D-056 §5 recorded the set as the migration contract under its mandate. This
  record adds no version, migration or layout change. The set's standing remains D-056's: authorised by the mandate,
  awaiting the Director's acceptance.
- **No version bump.** Kind 13 and the identity proof change no table, row or field; IoVersion stays 1. The canonical
  hash of every world is unchanged; every golden passes.

## 9. Same-step reads: the edge stands; the coupling map

- **The holds edge is right as built.**
  - **What it is.** Road development's read of construction's NEXT `ConstructionHolds` (D-056, declared by
    `SystemRegistration.ReadsThisStepFrom` and enforced by `TurnExecutor` at composition) stays as built.
  - **The alternative.** A kernel-managed handoff would make road development read PREV's holds. That would change
    what road works may draw in every step after a hold changes, which moves hashes. The direct read with an enforced
    edge changes no hash and refuses the only orders in which it would mean something else.
  - **Where it is enforced.** Every supported pipeline is composed through the `TurnExecutor` constructor (UI, CLI,
    tests), so the check covers all of them.
  - **Tests.** `ConstructionReservationTests.TheSameStepEdge_…`, `TheV33Migration_…`, and
    `ConstructionHoldsSchemaV34Tests.TheShippedPipeline_…`.
- **The coupling map.** A read-only audit of every system's reads of shared NEXT tables, with the lead verifying the two
  claims below against the code, found:
  - **The architecture's sanctioned pattern.** SystemCatalog hands one NEXT table to several systems, ORDER AS DATA,
    and documents each split (GoodStocks, Buckets, Controls, ResearchCompleted, AgeStates, ConstructionHolds). Within
    that pattern these reads depend on pipeline order and were already documented:
    - production/appropriation → consumption (GoodStocks);
    - several writers → construction and → colonization (GoodStocks);
    - migration/class mobility → colonization (Buckets, `UnplacedDeparture`);
    - class mobility/migration/colonization → demographics (Buckets);
    - colonization/revolt → governance and colonization → revolt (Controls);
    - the earlier GoodStocks writers → road development (with the hold rule off too);
    - appropriation, trade and migration's `ClampToAvailable` safety limits.
  - **Two comments contradicted the code and are corrected** (comment-only; no behaviour change):
    - HousingSystem said "all signals from PREV", but timber and clay are read from the shared NEXT GoodStocks;
    - RevoltSystem said its outcome "cannot depend on where this system sits in the pipeline", but the knowledge and Age
      a revolt-born polity inherits are copied from NEXT before research and agetransition run, so the child inherits
      them as they stood at the start of the step.
  - **Only the holds read is a declared, executor-enforced edge.** The others are sanctioned shared tables whose order
    is the shipped pipeline.json, pinned by the goldens.

## 10. The reservation priority rule is the contract (retained)

- **The rule.** `ConstructionQuery.HeadsInBuildingOrder` sorts queue heads by (EnqueuedTurn ascending, −1 for an entry
  with no progress row; SettlementId ascending; Slot ascending). All three are integers and unique per head, so the
  order is total and deterministic. The priority is the ORDER'S AGE, not building progress.
- **Its standing.** It was introduced by D-054 WS1, refined by D-055 (T1a round 2: a promoted slot takes its own order's
  place), and recorded as the contract by D-056 §4. Nothing here changes it, and no progress-first rule is introduced.
- **Its consequences, each pinned by `ConstructionReservationTests`:**
  - **Head-tenure fairness (FIFO).** The oldest order holds first. A younger head gets only the surplus (`Q17_…`,
    `AnOlderHeadAtALabourPace_…`).
  - **Partial progress is preserved.** A part-built head keeps its progress and its remaining need; a cancel refunds
    nothing (`ACancelledPartBuiltHead_…`).
  - **Reclamation.** Holds are recomputed from scratch every step: no banking. A cancelled, completed or invalid head
    releases its hold in that step (`AnInvalidReservation_IsReleased_…`, `ACompletion_FreesTheLeftoverInTheSameWalk_…`).
  - **A project that cannot progress.** An older head with no builders keeps holding its remaining need, and the
    younger progresses on whatever surplus exists (`AnOlderHeadThatNeverGetsBuilders_…`). If there is no surplus, the
    younger waits and the UI names the holder. The AI's structural-stall cancel covers the AI; the player decides.
  - **Ties.** Twenty heads tied on the enqueue turn across two purses set aside in ascending settlement id
    (`TwentyTiedHeads…`). A property test covers random splits, heads and waits over every step.

## 11. Kernel files changed (the post-M0 freeze, by name and reason)

D-056 §6 lists its kernel changes under its mandate. This record adds two:

| Kernel file | Change | Why it must be kernel |
|---|---|---|
| `Sim.Core/Kernel/ReplayProof.cs` | New: the identity proof | It is a property of replaying an order log against a trace, the kernel's replay contract (§3.9, D-008), shared by the UI and the CLI. No system may hold it: systems never see logs or traces. |
| `Sim.Core/Kernel/CanonicalSchema.cs` | `ReadFlag`, `ReadCommandSource` | The reader is the only place a malformed field can be refused before it becomes state. No layout or hash change. |

Non-kernel files changed: `Sim.Ui/UiSession.cs`, `Sim.Ui/ResumeRefusal.cs`, `Sim.Ui/Program.cs`, `Sim.Cli/Program.cs`,
`Sim.Cli/SessionSchemaNotice.cs`, `Sim.Cli/SessionEmitter.cs`, `Sim.Ui/Headless/PlayabilityGate.cs`, and the comments of
`Sim.Core/Systems/Housing/HousingSystem.cs` and `Sim.Core/Systems/Revolt/RevoltSystem.cs`.

## 12. The Contradiction Report requirement

- **Which conflict needed a CR.** Narrowing D-008's replay recovery path (frozen) so that an order is not applied to an
  entity it cannot be shown to name is a genuine conflict between frozen items, so it is CR-022: the frozen items,
  evidence, three options, blast radius and a recommendation.
- **Why it was built before a ruling.** The instruction delegated implementation and forbade stopping at a report. The
  recommended option is built and labelled "implemented under delegated authority, awaiting the Director's ruling".
  Nothing claims a prior approval. If the Director rules otherwise, the change is confined to `ReplayProof` and its
  three callers.
- **What did not need a CR:** the unreadable-session refusal, the emit-session overwrite refusal, the strict snapshot
  fields, the inspect verdict and the smoke rig bound. These fix defects within existing contracts.

## 13. The single-AI smoke rig (`--smoke --ai-empires 1`): a harness defect, fixed

- **Classification.** The problem "age rig: not eligible for Age II" was a HARNESS defect.
- **Evidence (seed 42).** The rig completes Age II's research milestones. They give one category; Age II needs two, so
  the second comes from play.
  - With no AI, the player is eligible at turn 23.
  - With one AI Empire, the player holds 6 of 12 settlements and is eligible at turn 133, by the same rules.
  - The rig's bound was 120 turns.
- **The fix.** The bound is now 200 (`PlayabilityGate.AgeEligible`). A world eligible sooner stops sooner, so the 0-AI
  state is unchanged.
- **Measured after:** `--smoke --ai-empires 1 --monkey 300`: SMOKE PASSED, 0 FAIL, 0 problems.

## 13b. Pathfinding: correctness and performance, re-verified on this tree

- **Correctness.** The six batch-4 optimisations (D-055 §6: O1 and O2 in `PathBuildSystem.Targets`, O3–O6 in
  `Pathfinder`) claim byte-identical results. The batch-3 code is kept verbatim as Sim.Tests' oracle
  (`LegacyPathfinder`, `LegacyPathBuild`). The differential tests compare every entry point against it on lattices
  chosen to reach every perturbable branch:
  - walls (an unreachable half);
  - even sizes with exact ties everywhere;
  - duplicate, self-anchored and seam-crossing lanes;
  - the 64×64 seam world.

  O5's early exit is observable and pinned (`DistancesToEarlyExitTests`): the stop fires; a target behind a wall runs
  the full search and reads infinity; on the cylinder it is reached across the seam.

  All of them ran on this tree in the full suite (the handoff report).
- **Performance.** `sim bench --seed 42 --turns 200 --founded`, Release builds, one quiet machine, three alternating
  rounds:

  | Build | Total, ms (3 runs) | Median | catchment ms / alloc | pathbuild ms / alloc |
  |---|---|---|---|---|
  | `fa3c2a6` (batch 3, before O1–O6) | 28,607 / 28,394 / 28,956 | 28,607 | 12,212 / 6.43 GB | 13,303 / 9.85 GB |
  | `fd1fa39` (batch 4) | 16,000 / 15,762 / 15,171 | 15,762 | 5,857 / 2.06 GB | 6,958 / 3.93 GB |
  | this tree | 15,963 / 15,532 / 16,047 | 15,963 | 6,095 / 2.06 GB | 6,875 / 3.93 GB |

  - **Batch 3 → batch 4:** about 45 % less time. The gain is in the two phases the optimisations touch, with about a
    third and two fifths of the allocation. Batch 3 and batch 4 are different builds whose worlds also differ where
    the hold rule binds, so the totals compare the builds, and the phase columns show where the time went.
  - **Batch 4 → this tree:** no measurable change. The ranges overlap, and allocation per phase is equal to within
    about 0.01 %.

## 14. What this record does not change, and what remains

- **CR-020's two calibration failures stay as they were.** These are `CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand`
  seed 42 ("8.35755E-05 fell below 0.75× its recorded value 0.000248624") and seed 7 ("Range: (71000 - 119000)
  Actual: 123950"). Both were re-run this round on the pre-change base `fd1fa39`, with the same two failures and the same
  messages. They are not new and not resolved.
- **M5 stays not complete** (D-052 §15).
- **Remaining limits:**
  - **Kind-13 packing bounds.** Slot below 2^18, project id below 65,536 (the goods loader refuses larger) and turn
    below 2^19 − 1 (ADR-041 §1).
  - **CLI.** The bare-log runners replay a same-schema session log from another build as this build's run (§4).
  - **A no-trace resume** replays to the last turn its log names, which is pre-existing.
  - **Run-id collisions.** Founded `--emit-session` runs can still share a run id, but emitting over an existing record
    is now refused (§6b), so nothing is lost.
  - **Partial emitted sessions.** A refused bare-log `--emit-session` leaves its partial directory (§4).
- **Deferred to `docs/queue.md`:** carrying a colony's identity in its orders (CR-022 option 3), for logs recorded after
  a future order-log version.
- **External checks not run:** GitHub CI on these commits, the Windows build and windows-smoke, and a human playtest.
  The handoff report lists the exact commands.
