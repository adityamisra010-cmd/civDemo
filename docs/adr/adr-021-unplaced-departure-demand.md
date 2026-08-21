# ADR-021 — Unplaced departure demand: the migration→colonization carrier (schema v22)

**Status: PROPOSED, awaiting director ruling.** Written because T4.4 changes the serialized
kernel contract, and CLAUDE.md requires an ADR for a contract change plus director sign-off for
anything the M0 kernel freeze covers. No part of this is self-certified.

**ADR number:** 021. `adr-020-clone-architecture-r3.md` is the highest on `main`; **019 is taken
by an unmerged branch** (`a36b94d`, architecture constitution addendum), so 019 is deliberately
skipped rather than reused.

## Context

D-037 B1: *"Migration currently runs settlement-to-settlement, and ADR-012 rules that with no
viable destination people die at home. **Extend it**: groups may depart into UNCLAIMED land and
found new settlements."* The thing being extended is migration, and the condition named is
ADR-012's — no viable destination.

`ColonizationSystem` therefore has to consume a quantity `MigrationSystem` determines. **That
quantity does not exist in the tree.** `MigrationSystem` forms the flight term only inside its
destination loop, always as `damping(i→j) × viability(j) × FamineFlightFactor × deficit_i`. When
no destination is both reachable and viable, every product is zero and the desire is never
expressed at all — it is **structurally absent, not discarded**. There is nothing to read.

## Decision

`BucketRow` gains two `double` fields; `CanonicalSchema` goes **v21 → v22**.

| field | meaning | lifetime |
|---|---|---|
| `UnplacedDeparture` | this turn's departure demand for this bucket that found NO reachable, viable destination | written by `MigrationSystem` every turn (including to 0), consumed and zeroed by `ColonizationSystem` the same turn |
| `UnplacedRemainder` | the D-004 sub-person accumulator for the colonization draw | persists across turns; **< 1 by construction** |

### Why existing state cannot express it
- `MigrationFlowRow(Settlement, Inflow, Outflow)` is a per-settlement **aggregate of what moved**.
  It carries no destination and no unmet quantity, and it is settlement-scoped where the party
  must be **bucket-scoped**: migration requires key-for-key arrival (D-026), so a party assembled
  from a settlement-level scalar would have to **invent** an allocation across buckets.
- `ConsumptionDeficitRow` is the quantity T4.4's first implementation used and is exactly what
  this replaces: a scale-free ratio that emigration cannot clear, and one that cannot distinguish
  "cannot feed its people" from "was founded three turns ago".
- No table in the tree is keyed by `(Settlement, Culture, Religion, Class, CohortIdx)` except
  `BucketRow` itself, which is why the fields go there rather than into a new table.

### Conservation semantics (law 1)
- **`UnplacedDeparture` is a DESIRE, not a stock.** No person is held in it, none is conserved
  there, and it is not a `ConservedQuantityId`. It is rewritten from scratch every turn, so it
  cannot accumulate or leak. People move only by `Ledger.Transfer`.
- **`UnplacedRemainder` is a D-004 accumulator**, the same pattern and the same justification as
  the existing `MigrationRemainder` on the same row: fractional people cannot move, and without
  banking, `floor()` biases every sub-person demand to zero permanently.
- **The bank is taken BEFORE the availability clamp**, so it holds only `exact − floor(exact)`.
  Banking after the clamp would carry **whole people** forward — measured at 475 person-units on
  one bucket during implementation — and discharge them later as one oversized party. Desire the
  population cannot satisfy is **dropped**: nobody moved, so nobody is owed a move.
- **No double-spend is structurally possible.** `UnplacedDeparture` is non-zero only when the
  source had no viable destination, which is exactly when migration moved nobody from it. The
  two draws are mutually exclusive per source. Colonization additionally reads **live**
  post-migration counts and clamps the party to them.

### Determinism
Array scans only; no dictionary iteration, no RNG stream, no `GetHashCode` in logic, no
culture-sensitive formatting. The readout is a pure write placed after every input it reads is
final and before any transfer, so no ordering between it and the transfer loop exists.

## What this ADR does NOT change

`MigrationSystem`'s equations, flows, caps, gates, EMA, attractiveness terms, pair selection,
remainder handling, overdraw discipline and pipeline position are **untouched**. That is not a
claim: with only these two fields removed from the serialized stream, all four goldens reproduce
`main`'s values **byte for byte**, shape asserts included.

## Consequences

- Three goldens move by **schema only** (driven, founded, FirstReign); `GoldenHash_Seed42Turn200`
  does not move at all, because that synthetic world holds no bucket rows.
- `ci.yml`'s `FOUNDED_GOLDEN` moves with the founded pin.
- 16 bytes per bucket added to every snapshot.

## The open question this ADR cannot settle

The carrier is only worth its schema cost if the mechanism it feeds does something.
**Measured, it never fires in any shipped world** (see `docs/t4.4-review-record.md` §7 F1). If
the director rules that D-037 B1's condition should be read more broadly than "no viable
destination at all", the carrier stays and only `MigrationSystem`'s readout changes. If B1 is
ruled to be a collapse-only mechanism and frontier closure is to come from elsewhere, **this
schema change should be reverted rather than shipped**, because the tree would be paying 16 bytes
per bucket for a mechanism that cannot fire.
