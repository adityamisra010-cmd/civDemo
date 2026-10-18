# ADR-041 — Identity cancels, the replay guard, the same-step edge, the hardened snapshot reader

**Status: IMPLEMENTED 2026-10-18 under the Director's mandate "Fix the game. Change what needs changing." Awaiting the
Director's acceptance like every packet.** Decisions and supersessions: `docs/d056-director-mandate-fix-the-game.md`.

The mandate authorised changing the kernel, `CanonicalSchema.cs`, `Snapshot.cs`, the schema version, the frozen
boundaries, and CLI and UI behaviour. This ADR states the contract that now holds. **The schema stays v34: no table,
row or field moved.** The order-log wire format keeps IoVersion 1, as it did when kinds 9 and 12 were added.

## 1. Order kind 13 — `CancelConstructionEntry` (`Sim.Core/Kernel/OrderLog.cs`)

- **Fields.** `TargetId` = the settlement. `Amount` = the entry's identity, packed exactly in a double by
  `OrderLog.PackCancelTarget`:
  - formula: `slot + 2^18 · (projectId + 2^16 · (enqueuedTurn + 1))`;
  - field ranges: slot 0..2^18−1, project 0..2^16−1 (the goods loader refuses larger project ids), enqueuedTurn −1..2^19−2;
  - `enqueuedTurn` −1 means an entry with no progress row (migrated from v32).
- **Validated at load.** `OrderLog.ValidateRecord` rejects a negative settlement, and any Amount that is not a whole
  number in [0, 2^53).
- **Resolution.** `ConstructionQuery.CancelTargetIndex` is the one resolver: ConstructionSystem, PendingBook,
  PendingCancels, the action surface and the delivery check all use it. Among the settlement's queue entries it looks
  for those whose project and enqueue turn match the order:
  - the entry at the recorded slot, if it matches;
  - otherwise the only match;
  - otherwise nothing (−1; the order changes nothing);
  - or `CancelTargetAmbiguous` (−2), when two or more match and none is at the recorded slot.

  A kind-13 cancel never redirects to an entry with a different identity.
- **Ambiguity at delivery.** `OrderValidation.ValidateAtDelivery` refuses an ambiguous kind 13, judged against PREV,
  with an `OrderValidationException`. Live play cannot produce one, because an entry keeps its slot for its whole life.
- **Emitters.** `ConstructionQuery.CancelOrder` is used by both the UI's Cancel and the AI's structural-stall cancel. It
  issues kind 13, carrying the identity read from the world in which it is issued.
- **Kind 11 (`CancelConstruction`) is legacy.** It is read and resolved by slot as before, and never written.

## 2. The replay guard (`Sim.Core/Kernel/ReplayGuard.cs`)

A kind-11 cancel is delivered only to the world it was recorded in.

- **The check.** Before a batch containing kind 11 is appended or delivered, the world it is delivered to (turn t) must
  match the session trace's row for turn t in some readable schema form. `ReplayGuard.MatchesRecorded` tries three
  forms in turn:
  - `WorldHash.ComputeHexAsSchema(world, 34)`;
  - the same at 33;
  - the same at 32.

  Each form is SHA-256 over the canonical stream, less the sections appended after that version
  (`CanonicalSchema.BytesAppendedSince`). A match proves that every table the recording schema serialized is identical,
  including the queue and the progress tables.
- **Every refusal** is an `OrderValidationException`, the type every runner already reports:
  - `Cli.Guard`: exit 2;
  - `ResumeLaunch`: exit 2, plus the refusal dialog.

  Its message says what was found, why the order is not applied, that nothing was changed, and what to do: open the
  session with the build that recorded it (named from the manifest), or start a new game.
- **Runners:**
  - `UiSession.ReplayTo`: before every batch, including the re-queued one. `UiSession.Resume` always passes the trace.
    The every-turn hash check stays limited to sessions of this schema.
  - `sim inspect`: before every batch. The `--report-jsonl` report is buffered and written only after the replay, so a
    refusal leaves no file.
  - Bare logs, through `Cli.LoadOrders` (`run --orders`, `run --from-save --orders`, `replay --orders` with or without
    `--emit-session`, `research --orders`): a log containing kind 11 is refused by `ReplayGuard.RefuseUnverifiable`
    before any world is built or any file is opened. With no trace there is nothing to verify against. This also keeps a
    legacy log from being converted into a valid-looking new session.
  - `inspect` reads its session's log through `Cli.LoadSessionOrders`, so it uses the trace check instead.
- **Missing trace.** A session with no trace file resumes only if its log contains no kind 11. It then carries
  `UiSession.MissingTraceNotice`.
- **Recorded files are kept.** After a resume that the trace did not check turn by turn (another schema, or no trace),
  the first `UiSession.Save` copies the log, trace and chronicle beside them as `*.as-recorded.*`. An existing copy is
  never replaced.

## 3. The same-step edge (`Sim.Core/Kernel/TurnExecutor.cs`, `Sim.Core/SystemCatalog.cs`)

- **Declaration.** `SystemRegistration.ReadsThisStepFrom` names the systems whose NEXT tables of the current step a
  system reads. `SystemCatalog.RoadDevelopment(cfg)` declares `construction` whenever `Reservations.Enabled(cfg)`.
- **Enforcement.** The `TurnExecutor` constructor throws `PipelineFormatException` when a consumer is placed before its
  producer. The message names both systems and their positions.
- **Pipelines without the producer** are accepted (test compositions only). There, the consumer reads the table as the
  world carries it.
- **Effect.** Every pipeline that composes reads the current step's set-asides. The v33 → v34 migration (ConstructionHolds
  left empty) is therefore exact in every one of them, not only in the shipped order.

## 4. The snapshot reader (`Sim.Core/Kernel/CanonicalSchema.cs`, `Snapshot.cs`)

- **Row counts.** `CanonicalSchema.ReadCount` reads each of the 61 table counts. It throws `InvalidDataException` for a
  negative count, or (on a seekable stream) for more rows than there are bytes remaining.
- **What `Snapshot.Load` now refuses**, each as a `SnapshotFormatException` with a diagnostic:
  - a header cut short;
  - a stream that ends inside a table;
  - a row count no writer emits;
  - any bytes after the last field.
- **The layout, the hash and every well-formed save are unchanged.**

## 5. Smaller items

- **Pathfinder.** `Pathfinder.DistancesTo(…, out int settled)` reports how many nodes the O5 search settled. It is the
  only way to observe the early exit; the four-argument form calls it.
- **`sim run --from-save S --save S`** is refused as a usage error, and S is untouched.
- **CI.** `scripts/gen-sample-orders.py` writes kind 13 (`cancel_target`). The ci.yml kind check expects
  {3..9, 12, 13}, and the allocation leg's twin files are `alloc-no13.bin` and `alloc-t13.log`.

## 6. Tests

| Area | Test |
|---|---|
| Payload, load checks, populated round trip, resolution, ambiguity, emitter equivalence, readable forms, the check, every CLI path out of process, `inspect` | `Sim.Tests/Kernel/LegacyCancelGuardTests.cs` |
| UI emitter, compatible sessions (this schema; v33 form), missing trace, the re-queued batch, `.as-recorded` copies | `Sim.Ui.Tests/LegacyCancelResumeTests.cs` |
| The shifted-slot case: kind 11 refused, kind 13 correct | `ConstructionLifecycleResumeTests.ARecordedCancel_WhoseSlotShiftsInTheReplay_…` |
| The edge | `ConstructionReservationTests.TheSameStepEdge_…`, `TheV33Migration_IsExactInEveryPipelineThatComposes_…` |
| The reader | `Sim.Tests/State/SnapshotCorruptInputTests.cs` |
| O5 | `Sim.Tests/Pathing/DistancesToEarlyExitTests.cs` |

## Dated note — 2026-10-18 (ADR-042; appended, the contract above is unchanged except where named here)

- **§2 "Missing trace" is superseded by ADR-042 §1.** With no trace, a batch naming any settlement is refused. A
  session resumes with `MissingTraceNotice` only if its log names no settlement.
- **§2's kind-11 rule is unchanged.** `ReplayProof` applies it before its own identity check.
- **§2 "Runners: `sim inspect`".** Inspect now runs `ReplayProof`. For a v32 or v33 session its verdict line reports
  the readable-form check (`SessionSchemaNotice.CheckedInRecordedForm`), no longer "trace not checked".
- **§2 "Bare logs".** A bare-log runner given the log of a session recorded under another schema refuses it, before
  any file is written (ADR-042 §2).
