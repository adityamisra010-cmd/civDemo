# D-056 — THE DIRECTOR'S MANDATE "FIX THE GAME": IDENTITY CANCELS, THE REPLAY GUARD, THE SAME-STEP EDGE, THE SCHEMA RECORD

**Status: IMPLEMENTED 2026-10-18 on `claude/new-session-yavghm`, in the protected development clone. Not merged to
`main`, not published. It awaits the Director's acceptance like every packet.**

- **Authority.** The Director's mandate of 2026-10-18 authorised changing:
  - D-008, D-052, D-054, ADR-034, ADR-039 and ADR-040;
  - the schema version, `CanonicalSchema.cs`, `Snapshot.cs` and the kernel;
  - the frozen boundaries;
  - CLI and UI behaviour;
  - the governance procedures.

  It required that superseding decisions be recorded without rewriting history. The records named below are therefore
  left as written, and this record supersedes the parts it names.
- **The technical contract** is `docs/adr/adr-041-identity-cancel-replay-guard-same-step-edge.md`.

## 1. A recorded cancel never redirects (supersedes D-054 WS1's cancel encoding)

**The defect.**
- Order kind 11 named its queue entry by (settlement, slot), and slots are reused.
- Replayed into a world that had drifted (another build, schema or platform), the same slot could hold a different
  project. The cancel then removed that project silently, and the resume was accepted.
- `ConstructionLifecycleResumeTests` pinned this as passing. It is now re-rigged: refused as kind 11, correct as kind 13.

**Decision.**
- Every emitter now writes **order kind 13, `CancelConstructionEntry`**. It carries the entry's identity: slot, project,
  and the turn its order was accepted. It resolves by identity and never redirects. An ambiguous identity is refused at
  delivery.
- **Kind 11 is legacy.** It is read and resolved by slot as before, and never written.

## 2. The replay guard (narrows D-052 §13 and the D-008 recovery path; extends ADR-034 S0)

- **The rule.** A batch containing a kind-11 cancel is delivered only to a world the session trace proves is the
  recorded one, in any readable schema form. Otherwise the replay is refused with an actionable diagnostic, before
  anything is mutated or written.
- **Where it applies.** The UI resume (including the re-queued batch) and `sim inspect`.
- **Bare logs** reach the CLI with no trace: `run --orders`, `run --from-save --orders`, `replay --orders` (with or
  without `--emit-session`), and `research --orders`.
  - A bare log containing kind 11 is refused before any world is built or any file is opened.
  - This also stops a legacy log being converted into a valid-looking new session.
- **Compatible historical sessions keep resuming.** That means:
  - a session of this schema whose trace check passes;
  - a session of another schema whose kind-11 batches match the trace in that schema's form;
  - any session without kind 11, unchanged.
- **What D-008 still covers.** Recovery through seed and order replay still holds for sessions without kind 11. For a
  diverged session containing kind 11, the recovery path is the build that recorded it, and the refusal names it.
- **D-052 §13 is extended.** A refused resume leaves the session files untouched. After any resume the trace did not
  check turn by turn, the first save copies the recorded files aside first (`*.as-recorded.*`). This addresses the
  review's finding that the first save overwrote the as-played trace and log.
- **A missing trace** no longer passes silently. A session without kind 11 resumes with a four-line notice saying the
  replay was not checked. A session with kind 11 is refused.

## 3. The same-step reservations edge (supersedes ADR-040 / D-055 §11 "order is data" for the ConstructionHolds read)

- **The read is now declared.** Road development's read of construction's NEXT ConstructionHolds is a declared
  same-step edge (`SystemRegistration.ReadsThisStepFrom`).
- **The executor enforces it.** While the hold rule applies, `TurnExecutor` refuses any pipeline that runs road
  development before construction, when the pipeline is composed.
- **Consequences:**
  - The shipped order (construction 10th, roaddevelopment 22nd) is unchanged, and no hash moves.
  - The non-shipped orders, in which the read saw the previous step's or a loaded save's rows, can no longer be built.
  - The v33 → v34 migration is therefore exact in every pipeline that composes, not "only in the shipped order".
  - This closes the review's governance gap that no permitted handoff covered this non-owner NEXT read.

## 4. The reservation contract (records what D-055 left with delegated standing only)

The contract is now recorded here as the rule:
- **Building order.** Enqueue age: the turn the order was accepted, then settlement id, then slot. There is no
  progress-first rule.
- **Release and reacquire.** A head that is cancelled, completed or invalid releases in that step. A re-enqueued
  project is a new entry and holds only once it is a head.
- **No banking.** Holds are recomputed from scratch every step.
- **Same-step consistency.** Section 3 above.

The behaviour is pinned by `ConstructionReservationTests` (contention, priority, blocked, cancel, release and
re-enqueue, the edge) and by the allocation CI leg.

## 5. The schema (supersedes D-052 §5 "Version stays 32"; confirms ADR-039 and ADR-040; amends ADR-005's migration exception)

- **v33 (ADR-039) and v34 (ADR-040) stand.** They were taken by agent-recorded supersession; this record confirms them
  under the mandate.
- **The readable set {32, 33, 34} and its explicit two-step migration** (`CanonicalSchema.Read(reader, version, …)`)
  is the migration contract. It supersedes ADR-005's one-step exception.
- **No version bump here.** Kind 13 is an order-log change: IoVersion stays 1, as for kinds 9 and 12. A build before
  this one refuses a kind-13 log as an unknown kind. The snapshot reader is hardened (ADR-041 §4): a truncated stream,
  a row count no writer emits, and trailing bytes are refused. No layout changed.

## 6. Kernel changes (the M0 freeze is lifted for these, under the mandate)

| Kernel file | Change |
|---|---|
| `TurnExecutor.cs` | `SystemRegistration.ReadsThisStepFrom` and the composition check |
| `ReplayGuard.cs` | New |
| `WorldHash.cs` | `ComputeHexAsSchema` |
| `CanonicalSchema.cs` | `BytesAppendedSince`, `ReadCount` |
| `Snapshot.cs` | `Load`'s corrupt-input refusals |
| `OrderLog.cs` | Kind 13 and its packing |
| `OrderValidation.cs` | Kind 13 in the world and delivery checks, the ambiguity refusal, `DescribeCancelTarget` |

## 7. Smaller items

- `Pathfinder.DistancesTo(…, out int settled)` makes the O5 early exit observable. It is pinned by
  `DistancesToEarlyExitTests`.
- `sim run --from-save S --save S` is refused, so it no longer overwrites the save it continues.
- `sim inspect` prints a kind-13 cancel as the entry it names.

## 8. What this record does not change

- **CR-020's calibration failures** stay as they were: baseline failures with D-052 §9's attribution, not new and not
  resolved.
- **M5 stays not complete** (D-052 §15).
- **Not covered by the guard.** After a cross-schema divergence, recorded orders that address a colony or polity id
  allocated after turn 0 can still land on a different colony or polity. The guard covers cancels, the defect the
  mandate named. This residual was measured in the review (item 27) as possible, not observed. Refusing such sessions
  would also refuse every diverged old session with colonies or revolts, so it is left as a recorded residual.

## Dated note — 2026-10-18 (D-057; appended, the record above is unchanged)

D-057 (`docs/d057-identity-proof-and-validated-handoff.md`, contract ADR-042, conflict CR-022) continues this work under
the Director's instruction "Fix the Game: Independent Audit, Remediation, and Validated Handoff". Read these parts of the
record above with it:
- **§8's residual is closed, not left open.** An order naming a colony or revolt-born polity founded after turn 0 is
  now delivered only when the replay is proven to be the recorded game up to that founding (ADR-042 §1). The residual
  was reproduced before it was closed (D-057 §1).
- **§2's missing-trace rule is superseded.** A session without its trace now resumes only if its log names no
  settlement; otherwise it is refused, and the remedy names the missing file. "A session without kind 11 resumes
  with a four-line notice" was true only of the kind-11 check.
- **§5 states the readable set {32, 33, 34} as "the migration contract".** Its standing is this record's — the
  mandate's authorisation, pending the Director's acceptance — not a prior approval. D-057 §8 gives the technical
  evidence: real v32 and v33 traces reproduce in their own forms.
- **§6's kernel list grows.** D-057 §11 names the two further kernel files: `ReplayProof.cs` (new) and
  `CanonicalSchema.cs` (strict fields).
