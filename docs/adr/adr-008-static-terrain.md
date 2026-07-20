# ADR-008 — Static terrain sharing: immutable TerrainSet outside the double buffer

**Status:** accepted (pre-authorized by D-024, m1-walking-skeleton-spec §1)
**Context packet:** T1.1 (worldgen fields)

## Decision

Terrain rasters (elevation, water, temperature, moisture, fertility,
movement-cost — D-015: 1024², ~50 MB) are **immutable after worldgen**. They live
in a `TerrainSet` referenced by `WorldState.Terrain` and are **excluded from the
per-turn `Clone()`** — the clone copies the reference, never the arrays. The
terrain **content hash** (SHA-256, computed exactly once at construction, field
by field per ADR-005 discipline) is folded into the canonical state stream
(schema v2: presence flag + 32 hash bytes after the clock), so it participates
in every `WorldHash`, every save, and every determinism-harness comparison.

## Rationale

Cloning ~50 MB of never-changing data every turn buys nothing (the Spine's own
"revisit if profiling demands" clause, exercised by director sanction). The
frozen mutable-state contract is untouched: everything that can change still
double-buffers; the exclusion applies only to data with no mutation path at all
(`TerrainSet` exposes read-only spans; the arrays are private and no writer
exists after construction).

## Consequences

- **Hash integrity:** two worlds on different terrain can never hash equal;
  same seed + config regenerates byte-identical terrain (worldgen twin-test),
  so the hash is stable across runs.
- **Saves bind to terrain without storing it:** a snapshot records the terrain
  hash only; `Snapshot.Load` requires the regenerated `TerrainSet` and rejects a
  mismatch actionably (wrong seed/config) — terrain is derived data, seed +
  worldgen config is its source of truth (D-008 replay philosophy extended).
- **Schema Version 1 → 2**; the M0 golden hash constant was updated deliberately
  with this packet (per the T0.7 rule: loudly, never casually).
- **Upgrade path (anti-scope now):** late-era terrain mutation (D-022 explicitly
  excludes erosion/climate simulation) would move the mutated layers into
  cloned, canonically-serialized state — a director-approved ADR at that
  milestone, reversing this exclusion only for the layers that gain writers.
