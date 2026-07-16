# ADR-001 — Table rows are `unmanaged` structs

**Status:** accepted (director-sanctioned, recorded at T0.3 session setup)
**Context packet:** T0.2 (state infrastructure)

## Decision

`Table<T>`, the base pattern for all `WorldState` tables (kernel contract §3.1),
constrains rows to `T : unmanaged` — pure value data, no object references, no
managed fields.

## Rationale

- **Deep copy by construction.** The §3.2 double-buffer clone (Prev → Next) is a
  single array copy that provably shares no mutable state with the source. With
  reference-typed fields this guarantee would be disciplinary; with `unmanaged`
  rows it is structural — an agent cannot silently introduce shared state.
- **Canonical bytes.** Rows are fixed-layout value data, so the T0.7 snapshot/hash
  serialization has an unambiguous field set with no reference graph to walk.
- **Determinism.** No hidden identity, no reference equality traps, no GC-dependent
  behavior inside sim state.

## Consequences

- **Cross-table references are by typed id only** (`SystemId`, `RegionId`, …) —
  never object references.
- **Names, text, and variable-length data live outside sim tables**, in id-keyed
  registries or relational tables (display layer / data files), never as row fields.
- **memcpy is licensed for `Clone()` ONLY.** Snapshot and hash (T0.7) remain
  field-by-field canonical serialization per m0-kernel-spec §3.8 — fixed schema
  order, fields in declaration order, `long` and raw double bits via
  `BinaryWriter` — never raw struct memory (padding bytes and layout are not part
  of the contract).
