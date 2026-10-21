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

> **2026-10-12 — the last limitation above is CLOSED** (D-052 §13, the playtest-blocker pass on `m5w-integration`). The trace check now throws `ResumeDivergedException` (same message). `UiSession.Resume` turns a
> same-schema divergence into `SessionIncompatibleException`, whose message tells the player which build recorded the
> session, which build this is, the turn the worlds differ from, and what to do. `ResumeLaunch.Attempt`
> (`Sim.Ui/ResumeRefusal.cs`, called by Program.cs) reports both refusals before any window opens:
> - an `OrderValidationException` keeps "error: <message>" and exit code 2;
> - an incompatible session gives "error: This saved session cannot be resumed with this build." plus the reasons, and
>   exit code 3.
>
> Sim.Ui is a GUI exe, so both are also shown in a best-effort native error dialog: SDL2's
> `SDL_ShowSimpleMessageBox`, suppressed by `CIV_UI_NO_DIALOG` for the tests that launch the exe. That dialog is NOT
> yet observed on Windows. The trace check itself, the D-008 schema-mismatch path and every hash are unchanged.
>
> MEASURED: a real `2dc6af2` session resumed by the unpatched `f0427d2` build crashed (exit 134); the patched build
> exits 3 with the report and leaves every session file byte-identical. Other `--resume` failures (a corrupt manifest,
> a missing order log) still end unhandled; they are listed in `docs/m5-playtest-known-issues.md`.

> **2026-10-15 dated note (D-054, ADR-039 §7; batch 3, uncommitted; the text above is kept as written).** Schema v33
> made the world an east-west cylinder, and every world recorded before v33 was flat.
> - **Topology on resume.** `UiSession.Resume` founds a session whose manifest records a schema before v33
>   (`TopologyFlags.RecordedFlat`) FLAT, on the map it was played on, and any other session with the shipped worldgen.
>   The D-008 path above (no trace-hash check, the order checks still applied) is otherwise unchanged.
> - **The notice, line 2.** For a pre-v33 session the second of the four lines now reads "Replayed from its seed and
>   orders on the flat map v32 used (D-008)." (with that session's schema number). For any other schema mismatch it
>   stays "Resumed by replaying its seed and orders (D-008)." Lines 1, 3 and 4 are unchanged.
> - **The limitation "`sim inspect` is unchanged … compares hashes regardless" is superseded.** `sim inspect` compares
>   hashes only when the manifest's schema is this build's (`SessionSchemaNotice.HashesChecked`). Otherwise it prints
>   "trace not checked: the session was recorded under save schema vN and this build is v33, so no replayed hash can
>   equal a recorded one (D-008). Replayed from its seed and orders on the flat map vN used; the rows below are this
>   replay's." (the "on the flat map" clause only before v33), with only the plain platform line and no `sim replay`
>   recipe of this build. It rebuilds a pre-v33 session flat as well.
> - **Tests.** `ChronicleAndResumeTests.Resume_OfASessionRecordedUnderAnOlderSchema…` now records its older-schema
>   session flat, as a v32 build would have; every assertion stands. `ResumeNoticeUiTests` (same name) now records the
>   older-schema half flat and asserts the resumed world IS the recorded one (WrapX false, an equal hash) before the
>   toast; its own-schema half records a separate session on the shipped cylinder and asserts an equal hash and no
>   notice. The real batch-2 session fixture is pinned by `Batch2SessionResumeTests`.
> - Record: `docs/d054-batch3-incremental-construction-allocation-cylinder.md` §4.

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


## Dated note — 2026-10-21 (M6.1; appended, the text above is unchanged)

STATUS CORRECTION (dated note, M6.1): the header above reads PROPOSED; line 7 of this record reads "RATIFIED by the Director (D-050 ruling 1)". The header is stale; the ratification line governs.
