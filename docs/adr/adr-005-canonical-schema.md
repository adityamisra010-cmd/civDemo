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

## Dated note — 2026-10-15 (D-054, ADR-039; appended, the decision above is unchanged)

"version mismatch rejected outright (D-008: no migration)" is superseded for ONE step only.
- Schema v33 (incremental construction, the building/roads split) is the first schema with an EXPLICIT migration.
  `Snapshot.Load` reads a v32 save through `CanonicalSchema.Read(reader, 32, …)`, which leaves the two appended tables
  empty and treats the world as flat.
- The v33 snapshot also carries an int32 topology trailer after the stream. The trailer is outside the stream and the
  hash.
- Every other version mismatch is still refused with the D-008 message.
- Contract: `docs/adr/adr-039-schema-v33-orders-11-12-and-cylinder-topology.md` §2, §4 and §7.

## Dated note — 2026-10-16 (D-055, schema v34; appended, the decision above is unchanged)

Schema v34 (D-055 — the Director's batch-4 instruction, item 1: material reservations across turns) appends ONE table,
section 61 `ConstructionHolds` (Purse, Good, Units; 16 bytes), after ResourceAllocations. No existing row changed width.
- The readable versions are the SET {32, 33, 34}, through one helper, `CanonicalSchema.IsReadable` (CanonicalSchema.Read,
  Snapshot.Load, Sim.Cli's SaveFiles.MigrationNote and SnapshotDiff.LayoutFor). A v33 stream stops after section 60 and a
  v32 stream after section 58; every missing table is left empty. The v33 → v34 step is exact IN THE SHIPPED PIPELINE
  ORDER (construction before roaddevelopment, pinned by
  `ConstructionHoldsSchemaV34Tests.TheShippedPipeline_RunsConstructionBeforeRoadDevelopment`), because ConstructionSystem
  clears the table at the start of every step and rebuilds it before road development reads it. In an order that runs
  road development first, road works in the first step after a v33 load see no set-asides, while a v34 save of the same
  state would carry its rows (`SystemCatalog.RoadDevelopment`'s ownership paragraph; pinned by
  `ConstructionReservationTests.TheV33Migration_IsExactInTheShippedOrder_ARoadFirstOrderReadsTheMigratedWorldsEmptyTable`).
- `Snapshot.Load` refuses a ConstructionHolds table no rule writes (unsorted or duplicate (Purse, Good), Units ≤ 0,
  Good < 0) with `SnapshotFormatException`.
- Every other version is still refused with the D-008 message. The topology trailer is unchanged (v33 and v34 carry it).
- Contract: D-055 and ADR-040 (assembled at the batch-4 integration).
