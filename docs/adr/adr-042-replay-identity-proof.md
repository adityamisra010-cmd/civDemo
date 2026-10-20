# ADR-042 — The identity proof of a replayed order; unreadable sessions; strict snapshot fields

**Status: IMPLEMENTED 2026-10-18 on `claude/new-session-yavghm` under the Director's delegated authority (D-057). Not
merged; awaiting the Director's acceptance.** The conflict it resolves is CR-022; the decisions and their compatibility
impact are D-057. It extends ADR-041 §2 (the legacy-cancel guard), which keeps its rule unchanged.

## 1. `ReplayProof` (`Sim.Core/Kernel/ReplayProof.cs`)

One instance per replay of a RECORDED session. It is fed each turn's batch in turn order, before the batch is appended
or delivered: `BeforeDelivery(batch, prev)`.

- **What an order names.** `AddressedSettlement(order)`:
  - TargetId for kinds 2, 4, 9, 11 and 13;
  - TargetId >> 3 for kind 3;
  - none for the rest (their target is the issuer, a research node, an Age or a region).

  The issuer is `ActorId`.
- **What is proven.**
  - **A matching turn.** `prev` is compared with the trace's row for its turn: the recorded schema's form first
    (`WorldHash.ComputeHexAsSchema`), then every readable form (`ReplayGuard.MatchesRecorded`). A match proves every
    settlement and polity that world holds. Ids are allocated in order and never reused, so it also proves every id
    up to that world's highest.
  - **The bounds** are those of the last matching turn: `ProvenTurn`, `ProvenSettlementMax`, and the polity maximum.
  - **Until the first difference.** Each turn is compared until one differs (`FirstDifference`). After that, a batch
    naming an id above the bounds is compared against its own turn's row: proven on a match, refused otherwise.
  - **The founded roster** (polity ids up to `foundedRosterMax` = 1 + the founding AI Empires) is proven by
    construction.
  - **An id the delivered-to world does not hold is skipped.** It names nothing to misapply to, and ADR-034's
    `ValidateAtDelivery` refuses it.
  - **A batch carrying kind 11** must itself be delivered to a matching world (ADR-041 §2, unchanged).
- **No trace.** Nothing is proven but the founded roster, so a batch naming any settlement is refused.
- **A refusal** is an `OrderValidationException`, raised before the batch is appended. Its message names:
  - the session, the turn, the order's kind and issuer, and the settlement (with its founding turn in the replay) or
    the revolt-born polity;
  - the evidence: no trace, a founding that differs, "matches through turn P and differs from turn F on", or
    "proven only through P";
  - that nothing was applied or changed;
  - the remedy.
- **Cost.** One hash per replayed turn until the first difference, one per later batch that names an unproven id, and
  four at a refusal.

## 2. The runners

- **`UiSession.ReplayTo` / `Resume`.**
  - A recorded session runs under the proof: a trace was found, or `missingTraceFile` names the trace file the
    manifest lists ("" when it lists none). The proof uses `recordedSchema` = the manifest's schema.
  - The every-turn hash check of this schema's sessions is unchanged (D-052).
  - A bare `ReplayTo` (no trace and no session: tests) keeps D-056's kind-11 rule only.
- **`sim inspect`.**
  - It runs the proof; a refusal exits 2 with nothing written.
  - **Its verdict line.** For a session of another readable schema (32 or 33) the line is
    `SessionSchemaNotice.CheckedInRecordedForm`: "trace checked in the v32 form (D-057): … the replay matches the
    recorded game through turn P and differs from turn F on" (or "reproduces … through turn N").
  - That line replaces `NotChecked`, whose "no replayed hash can equal a recorded one" stopped being true at D-056.
    `NotChecked` remains for a schema outside the readable set.
- **The bare-log runners.** These are `run --orders`, `run --from-save --orders`, `replay --orders` (with or without
  `--emit-session`) and `research --orders`.
  - A log with no session beside it, or the log of a session of this schema, is replayed as this build's own run. The
    runner asserts nothing about a recording.
  - The log of a session recorded under another schema is refused before anything is read further or written
    (`Cli.LoadOrders` → `RefuseOtherSchemasSessionLog`). Such a log sits as `orders-X.bin` beside a `session-X.json`
    that names it.
  - ADR-041's kind-11 refusal of a bare log is unchanged.

## 3. Unreadable session files (`Sim.Ui/UiSession.cs`, `Sim.Ui/ResumeRefusal.cs`, `Sim.Ui/Program.cs`)

- **The cases:** a manifest that is not one, a truncated or foreign order log, a missing log, a malformed trace line,
  and a `--resume` argument naming no manifest.
- **How each is reported.** Each is a `SessionUnreadableException` naming the file. `ResumeLaunch` reports it like
  every refusal: "error: <message>" on stderr, the dialog once, and `SessionUnreadableExitCode` (2). The files are only
  read.
- **Before:** an unhandled exception ended the launch (exit 134).

## 4. Strict snapshot fields (`Sim.Core/Kernel/CanonicalSchema.cs`)

- **Boolean fields.** The terrain flag and `AgeEligibility.Eligible` must hold the byte 0 or 1 (`ReadFlag`).
- **Command source.** A polity's `CommandSource` must be 0 (AI) or 1 (player) (`ReadCommandSource`).
- **Anything else** is an `InvalidDataException`, which `Snapshot.Load` reports as a corrupt save.
- **Layout, hash and every well-formed save are unchanged.**

## 5. Tests

| Area | Test |
|---|---|
| The proof (hand-built worlds), `inspect` on a real drift, every bare-log runner | `Sim.Tests/Kernel/IdentityDriftGuardTests.cs` |
| The UI resume: the hazard, the refusal (files byte-identical, dialog, exit 2), the compatible game in v33 and v34 form, the missing trace | `Sim.Ui.Tests/IdentityDriftResumeTests.cs` |
| The missing-trace rule against D-056's sessions; a log naming no settlement resumes with the notice | `Sim.Ui.Tests/LegacyCancelResumeTests` |
| An older schema's session resumed from a real v32-form trace; a trace no form matches refused | `Sim.Ui.Tests/ChronicleAndResumeTests` |
| `inspect` of the real batch-2 (v32) and batch-3 (v33) fixtures | `CliSaveAndSizeMessagesTests`, `ConstructionHoldsV33FixtureTests` |
| Unreadable files, in process and in the real exe | `Sim.Ui.Tests/SessionUnreadableResumeTests.cs` |
| Strict fields | `Sim.Tests/State/SnapshotCorruptInputTests.cs` |


> **2026-10-20 — ACCEPTED by the Director with CR-022 option 2 (closure ruling R10).** The status line above is superseded.
> Record: `docs/m5-exit-inventory.md` §13.
