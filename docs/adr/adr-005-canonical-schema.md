# ADR-005 — Canonical serialization via one explicit schema chokepoint

**Status:** accepted (director-sanctioned constraints, recorded at T0.7)
**Context packet:** T0.7 (snapshots, hash, order log)

## Decision

All canonical serialization lives in `Sim.Core/Kernel/CanonicalSchema.cs`: a
static class whose `Write`/`Read` methods list every table and every field of
`WorldState` explicitly, in fixed order, plus an `ExpectedLength` that sums the
schema's declared field widths. One integer `Version`, bumped on any schema
change. `WorldHash` = SHA-256 over this stream; `Snapshot` = header
{magic, version, seed, turn} + stream, with the header excluded from the hash and
version mismatch rejected outright (D-008: no migration).

## Rules the mechanism enforces

- **Field-by-field, restated as law:** raw struct memory, `MemoryMarshal`, and
  unsafe copies are banned in the serializer — padding bytes are a determinism
  hazard and a schema dead end. memcpy remains licensed for `Clone()` only
  (ADR-001). The structural anti-padding test (stream length == ExpectedLength)
  fails on any raw-memory shortcut, because struct layouts pad and the schema
  does not.
- **No reflection:** .NET does not guarantee member enumeration order; canonical
  order is what this one reviewable file says it is.
- **Doubles as raw IEEE-754 bits** (`BitConverter.DoubleToInt64Bits`), with NO
  normalization of −0.0 or NaN — bit-exactness is the point; normalization would
  mask divergence the hash exists to detect.
- **Adding state = three edits in one file** (Write, Read, ExpectedLength); the
  anti-padding test and the pinned golden hash break loudly until all three agree.
- **Conserved reconstitution:** `Conserved.FromSnapshot` is the deserializer's
  counterpart to `UNSAFE_LedgerSet`, grep-gated to CanonicalSchema.cs the same way
  (loading is not mutation: flow counterweights load in the same stream, so the
  conservation identity is preserved).

## Order log

A separate artifact (own magic/version/IO): append-only {turn, actorId, kind,
target, amount} records; `TurnExecutor` consumes `BatchFor(Prev.Clock.Turn)` into
`OrderBatch`. Turn semantics: an order with Turn = t is delivered to the step
executing from turn-t state. M0 defines exactly one order kind (SetRainBias,
consumed by WeatherSystem) to prove the pipe end-to-end.
