# ADR-004 — Conserved stocks behind a get-only wrapper, mutation grep-gated to Ledger.cs

**Status:** accepted (director-sanctioned mechanism, recorded at T0.6)
**Context packet:** T0.6 (Ledger + conservation)

## Decision

Conserved stocks are stored as `Conserved` — a readonly struct wrapping a `long`
with a get-only public surface. The single mutation path is the internal factory
`Conserved.UNSAFE_LedgerSet`, called only by `Sim.Core/Kernel/Ledger.cs`; the
banned-constructs CI gate greps for the name and fails on any occurrence outside
Ledger.cs / the declaration itself. Stocks are born at `Conserved.Zero`; all value
enters and leaves the world through `Ledger.Flow`, whose (quantity, reason)
counterweights live in `WorldState.LedgerFlows` rows. `ConservationAuditor`
checks the identity Σ stocks + Σ sunk − Σ sourced = 0 exactly, per quantity, in
one call.

## Rationale

Law 1 says conserved stocks change ONLY via Ledger; §3.6 mandates a CI grep. A
bare `long` field makes every `+=` a potential silent violation that the grep
cannot distinguish from legal arithmetic. The wrapper inverts the problem: illegal
mutation now requires calling a loudly-named method that CI can grep for exactly,
and casual `+=` does not compile at all.

## Consequences

- Rows holding conserved stocks are field-based structs (not record structs) so
  `Ledger` can take `ref Conserved`; they implement `IEquatable` manually, with
  `GetHashCode` overrides marked `gate:allow-gethashcode` (equality plumbing only).
- All Ledger arithmetic is checked; overflow throws `LedgerOverflowException`
  (S3 overflow discipline at its chokepoint); failed ops mutate nothing.
- Known escape (accepted, test-only by convention): `Ledger.Transfer` into a
  `Conserved` local that is then dropped removes value from audited world totals —
  this is exactly how the auditor's teeth test corrupts a world without bypassing
  the gate. Real systems only ever hold refs into WorldState tables; the M10
  slice-gate review may revisit (see docs/queue.md).
- Scope of the audit (recorded T1.6, per director): the audit proves bookkeeping
  consistency, not rate truth — recorded-but-wrong flows balance. Per-flow
  exactness tests own rate truth (T1.5 precedent).
