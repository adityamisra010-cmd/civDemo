# ADR-034 — ORDER VALIDATION: DEFERRING CHECKS FOR IDS FOUNDED MID-GAME, AND THE DELIVERY CHECK THAT BOUNDS IT

**Status:** PROPOSED — awaits the Director's acceptance ruling. Written 2026-10-05 by the F3 fix stream of the M5
hardening pass (branch `m5h-f3-ui-docs-fixes`), after a verifier found that two kernel changes had landed without an
ADR.

> **2026-10-06: RATIFIED by the Director (D-050 ruling 1).**

**Touches the kernel contract:** `Sim.Core/Kernel/OrderValidation.cs` (frozen after M0 without Director sign-off).
No schema change, no new order kind, no change to `TurnExecutor.Step`, no golden hash moves. This record is the ADR
the constitution requires for a touched contract. It covers two earlier commits made without one, plus the
tightening this stream added.

---

## 1. Context

`OrderValidation.ValidateAgainstWorld` (T1.6) is the up-front, world-dependent check every replay runner performs
right after it builds the **turn-0** world: `sim run --orders`, `sim replay`, `sim inspect`, `sim research --orders`
and the UI's session tests. Its rejection must be actionable and happen before the run starts, never as a silent skip
partway through a turn. It checks that the issuing actor is a registered Empire, that a construction or labour target
exists, and that the actor controls it.

Two M5 mechanisms create ids **after** turn 0:

| mechanism | id allocated | commit that deferred the check |
|---|---|---|
| colonization (ColonizationSystem) | settlement `max settlement id + 1` | `7100c77` (M5 integration, stream V) |
| revolt (RevoltSystem, D-048) | polity `max polity id + 1` | `9bcad5b` (M5 hardening H4) |

A live session applies orders for these ids, because the UI and the AI producer (ADR-033 D5) issue them from the
state that holds them. Checked against the turn-0 world, the same log was rejected. Measured on `9bb7423`: canonical
seed 42 with the FoundedHarness labour orders revolts settlement 0 at turn 58, and `sim replay` of that run log
exited 2 with "polity 2 is not a registered Empire". So a played session with a colony or a revolt could not be
replayed or inspected.

## 2. The rule

### 2.1 Up-front deferral (7100c77, 9bcad5b — unchanged here)

`ValidateAgainstWorld` **defers** the world-dependent checks (existence and control) for an order when **all** of
the following hold:

- **Actor:** the roster is non-empty, `record.Turn >= 1`, and `record.ActorId` is above every polity id in the
  turn-0 roster.
- **Settlement target** (LaborAllocation, SectorAllocation decoded `>> 3`, EnqueueConstruction): `record.Turn >= 1`,
  the world has at least one settlement, and the target id is above every turn-0 settlement id.

Turn-0 orders are never deferred, because the first batch reads exactly the validated world. Every turn-0 id is
checked as before. The world-independent check that an Empire legislates only its own tax
(`SetTaxRate.TargetId == ActorId`) applies to every actor, deferred or not.

### 2.2 What this weakened

Before these commits, any actor or settlement id missing from the turn-0 world was rejected up front. Since then, a
**forged** id above the turn-0 maxima, such as actor 999 or settlement 999, passes `ValidateAgainstWorld`. Until this
ADR the only protection was at delivery. Each consumer applies its predicate on PREV: ResearchSystem and AgeQuery
read the roster, RoadDevelopmentSystem uses `IsRosterPolity`, ConstructionQuery and LabourActivities check the
control relation, and GovernanceSystem enacts only self-targeted edicts. An order that fails its predicate changes
nothing. `RevoltPolityOrderValidationTests.OrdersFromAnActorThatNeverExists_ChangeNothingAtDelivery` pins this: the
world hash is identical, turn by turn. The deferral was therefore **safe**, but a forged log was **accepted
silently**, and that broke the T1.6 principle that a bad log is rejected with an actionable message.

### 2.3 The tightening: a delivery-time check (this ADR, F3)

`OrderValidation.ValidateAtDelivery(OrderBatch batch, IReadOnlyWorldState turnZero, IReadOnlyWorldState prev)`
re-applies exactly the deferred checks at the moment the batch is delivered:

- A **deferred actor** must be a registered Empire in PREV.
- A **deferred settlement target** must exist in PREV.

Otherwise it throws `OrderValidationException`. The message names the order, the id, the delivery turn and the size
of PREV's roster or settlement table, and cites ADR-034. Orders that the up-front pass already checked are not checked
again. The function only observes: `Step` is unchanged and no hash moves.

Every replay runner calls it for each turn's batch, against the world it is about to step, before stepping:

- `sim run --orders`: the loaded log's batch.
- `sim replay` and `sim inspect`: the log's batch.
- `sim research --orders`.

**Why a live-played log always passes.** The UI and the AI producer stamp each order with the turn being played,
and they issue it from the very state the order is delivered to. A polity the AI drives is in that state's roster
(the roster never shrinks: no code removes a PolityRow). A settlement the player or AI targets exists in that state.
So a revolt or colony replay is unaffected. `CliRevoltPolityProducerTests` runs a real turn-58 revolt log through
`sim run` and `sim replay`, with every hash equal, and still passes with the check in place.

**Considered and not taken: a tighter up-front bound** (for example, a deferred actor id at most
`roster max + number of settlements`). Revolts can recur, because a revolt-founded polity's settlement can revolt
again, and colonies add settlements. So no bound computable from the turn-0 world is both sound and tight. The
delivery check is exact, and it costs one scan of the batch per turn.

## 3. Tests

| test | pins |
|---|---|
| `ColonyOrderValidationTests` (7100c77) | colony-target deferral; turn-0 targets still checked |
| `RevoltPolityOrderValidationTests` (9bcad5b) | actor deferral; turn-0 rejection; registered actors still control-checked; tax authority not deferred; a never-founded actor's orders change nothing (hash per turn) |
| `CliRevoltPolityProducerTests` (H4) | a real revolt log replays through the CLI with the delivery check in place |
| `OrderDeliveryValidationTests` (F3) | forged actor 999 passes the up-front pass and is rejected when its turn (2) is delivered, with a turn-exact message; a deferred actor that exists at delivery is accepted; a deferred settlement target that does not exist is rejected while a turn-0 target is not re-checked; `sim replay` of a forged-actor log exits non-zero with the ADR-034 diagnostic |

Mutant (measured): deleting the `ValidateAtDelivery` call from `sim replay` fails
`TheCli_RejectsAReplayOfAForgedActorLog_WithTheDeliveryDiagnostic` (1 of 4 F3 tests).

## 4. Blast radius

- `Sim.Core/Kernel/OrderValidation.cs`: one new public static method. The behaviour of `ValidateAgainstWorld` is
  unchanged by this ADR.
- `Sim.Cli/Program.cs` (run, replay, inspect) and `Sim.Cli/ResearchCli.cs`: one call per turn before `Step`.
- The UI's `--resume` replays its own session log through `UiSession` and does not call the new check (INFERRED to be
  safe for the same reason a live log passes, and outside the F3 scope). It can adopt the check later.
- No schema, golden, order-kind or system change.

---

## S0 — the UI's `--resume` adopts the checks; a session recorded under another schema (appended 2026-10-10)

> **2026-10-10 — dated append note (M5 map/exploration workstream, packet S0, branch `m5w-s0-resume-hardening`
> cut from `82bde01`). Sections 1–4 above are kept unchanged as history.** The §4 bullet "The UI's `--resume` …
> does not call the new check … It can adopt the check later" is **superseded** by this note: it now does.

**What changed (Sim.Ui only; no kernel, schema, order-kind, system or golden change).**

- **The checks.** `UiSession.ReplayTo` (the replay loop of `UiSession.Resume`) calls
  `OrderValidation.ValidateAgainstWorld(saved, turnZero)` once against the founded world, then
  `OrderValidation.ValidateAtDelivery(batch, turnZero, prev)` for each turn's batch against the world it is about to be
  delivered to, before that step — the loop `sim replay` runs. The batch it checks is the session log's own
  `BatchFor(turn)`, which is what the executor then delivers. The orders stamped with the last played turn, which the
  resume re-queues for the next End Turn, are checked as well, against the world that End Turn delivers them to.
  `sim replay --turns N` meets such an order only when it replays one turn further, and it gives the same message.
  `ReplayTo` now refuses to run unless the session is at turn 0 with an empty log, because the up-front pass is only
  meaningful against the founded world. `Resume` is its only caller and always meets that condition.
- **The diagnostic.** A rejected log raises the same `OrderValidationException`, with the same message, as
  `sim replay`. The `--resume` launch (`Sim.Ui/Program.cs`) prints it as `Cli.Guard` does (`error: <message>` on
  stderr, exit code 2) before any window opens. Before this change the order was silently ignored by its consumer,
  because a forged record changes no hash, so the resume "succeeded".
- **A session recorded under another schema (D-008).** `UiSession.Resume` now reads `SessionManifest.SchemaVersion`.
  Trace hashes are `WorldHash` over the whole canonical stream, so after any schema bump no saved trace line can equal
  a replayed one, and every older session used to fail with `resume diverged at turn 1`. When the manifest's version is
  not `CanonicalSchema.Version`, the session is resumed by the D-008 recovery path: re-founded from the seed and
  replayed from the order log **without the trace-hash check**. The trace's turn column still gives the turn to replay
  to. The order checks above still apply. `UiSession.ResumeNotice` then carries an explicit four-line notice
  (`SchemaMismatchNotice`): "Recorded under save schema vN; this build is vM." / "Resumed by replaying its seed and
  orders (D-008)." / "The saved trace was not checked against this replay." / "The world may differ from the one
  originally played." The game opens with it in the notice toast, under the kicker `RESUMED SESSION`
  (`NoticeToast.Show` gained an optional kicker; End Turn notices keep `THIS TURN`). It stays on the developer BUILD
  tab after the toast fades, and the launch prints it under `resumed … at turn N`. A session resumed under its own
  schema keeps the trace check exactly as before and shows no notice.

**Observer-only.** Validation reads PREV and the turn-0 world. The notice is UI state that no system reads. `Step` is
unchanged. MEASURED on this branch (Release): `sim run --founded --seed 42 --turns 300` ends at
`937577ffb0e73c2b051d8d7832cfb5028c2154d01c9afef4d976b71ccb5e7f54`, the founded golden at `82bde01`.

**Known limitations (not changed here).**
- The manifest is written once and never rewritten (Program.cs: it describes the session's founding). A session
  resumed under another schema therefore takes the no-trace-check path on every later resume too. This holds even
  after its first save, when the trace has been rewritten from this build's replay and would match.
- `sim inspect` is unchanged. It prints the manifest's schema in its header and compares hashes regardless.
- Only `OrderValidationException` is turned into the CLI-style diagnostic at launch. A genuine `resume diverged`
  under the same schema still ends the launch with an unhandled exception, as before.

**Tests (Sim.Ui.Tests).**

| test | pins |
|---|---|
| `UiSessionReplayTests.Resume_RejectsALogWithAnIllegalDeferredId_WithTheDiagnosticSimReplayGives` (4 cases) | Covers a forged actor 999 delivered at turn 2, a ghost colony target delivered at turn 3, a ghost settlement at turn 0 (rejected by the up-front pass), and a forged actor in the re-queued batch. In each case the resume's message **equals** the message from `sim replay`'s loop, run over the same founding and executor recipes. As a control, the same session without the forged record resumes hash-for-hash. |
| `UiSessionReplayTests.TheUiResumeLaunch_PrintsTheCliDiagnostic_AndExitsLikeSimReplay_BeforeAnyWindowOpens` | `dotnet Sim.Ui.dll --resume DIR`, run as a separate process with a bound: exit code 2 and stderr exactly `error: <the CLI message>`. |
| `UiSessionReplayTests.ReplayTo_RunsOnlyFromTheFoundedWorld_WhereItsUpFrontCheckIsMeaningful` | the turn-0 / empty-log precondition |
| `ChronicleAndResumeTests.Resume_OfASessionRecordedUnderAnOlderSchema_ReplaysWithTheNotice_AndContinuesLikeAFreshReplay` | The session (with an AI empire) is recorded under `Version − 1` with a trace whose hashes cannot match. It resumes to the trace's turn with the exact notice and the world hash of a fresh trace-less replay and of the live session. It continues 4 turns hash-identically to both, with equal orders. As a control, the same doctored trace under this build's schema is still refused at turn 1. |
| `ResumeNoticeUiTests.AGameOpenedOnASessionResumedUnderAnotherSchema_ShowsTheNotice_AndOneUnderItsOwnSchemaDoesNot` | Through the live UI (`UiFrameHarness`, a real ImGui frame): the toast shows the notice under `RESUMED SESSION`, and opening the UI leaves the world hash unchanged. A same-schema resume shows no notice. |

**Mutants (MEASURED 2026-10-10 on this branch, Release).** The targeted filter
`ChronicleAndResumeTests|ResumeNoticeUiTests|UiSessionReplayTests` (20 tests) took 18–25 s clean. Each mutant run
was bounded at 300 s, and none hung. Every mutant was killed by at least one semantic test:

| mutant | killed by |
|---|---|
| M1: delete the per-turn `ValidateAtDelivery` in `ReplayTo` | the forged-actor and ghost-colony cases, and the launch test (3 failed) |
| M2: delete the up-front `ValidateAgainstWorld` | the turn-0 ghost-settlement case (1) |
| M3: delete the re-queued batch's `ValidateAtDelivery` | the re-queued case (1) |
| M4: always check the trace (pre-S0 behaviour) | the older-schema resume test and the UI notice test (2) |
| M5: never check the trace | the older-schema resume test's same-schema control (1) |
| M6: never set `ResumeNotice` | the older-schema resume test and the UI notice test (2) |
| M7: `GameUi` does not show the notice | the UI notice test (1) |
| M8: remove the launch's `catch (OrderValidationException)` | the launch test (1) |
