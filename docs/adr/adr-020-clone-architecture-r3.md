# ADR-020 — CLONE ARCHITECTURE (R-3): DESIGN AND MEASUREMENT

**T4.16, per `docs/m4-spec.md`. DESIGN AND MEASUREMENT ONLY — no implementation in this ADR.** This
is a director-override ADR under `spine-s8-governance-freeze.md` §1/§2: `WorldState.Clone()` and the
double-buffer model are inside the M0 kernel freeze, so any eventual implementation requires this ADR
plus a director ruling on it, not a Contradiction Report (nothing here conflicts with a frozen item —
this is forward measurement feeding a future scheduling decision). Cut from `main` `ba96b1c`.

Structured on the §3 Contradiction Report template (frozen items, evidence, options, blast radius,
recommendation) because it is the natural shape for this content, even though no contradiction is
being reported.

---

## 1. WHAT THIS ANSWERS

R-3 asked what it costs to count population densely: bucket rows are instantiated for every
`(Settlement, Culture, Religion, Class, CohortIdx)` combination whether or not anyone lives there.
The director's ruling on the bucket-cap half of R-3 (`docs/m4-spec.md` §7) raised the cap but
explicitly separated it from the clone question: **"Raising the cap permits more rows. It does
nothing about every row being COPIED EVERY TURN, and that is the actual constraint at late-game
scale... change how state is carried between turns so a big world is AFFORDABLE, not merely
PERMITTED. Scheduled as T4.16."**

This ADR measures the clone cost against the CURRENT schema (not the M3-era baseline the original
spec text assumed — T4.3's three tables have since merged), evaluates the three candidate
architectures against the three non-negotiables the spec states, and reports what breaks, what
changes, and the schedule price — without choosing an architecture or writing any implementation.

## 2. THE THREE NON-NEGOTIABLES (constraints, not trade-offs, per `m4-spec.md`)

1. **Read-isolation is why the copy exists.** Systems read `Prev`, write `Next` — that independence
   from execution order is what CI's read-isolation gate enforces. Any scheme that weakens `Prev`
   being genuinely immutable for the whole turn is REJECTED, not traded off.
2. **Determinism is absolute.** Same seed, same order log, same world hash, before and after —
   asserted with a vacuity guard, per T3.12a's precedent, not assumed.
3. **The goldens must not move.** A clone-architecture change is a change of representation, not of
   behaviour. If a golden moves, that is a finding and the packet implementing this ADR stops.

## 3. EVIDENCE — RE-MEASURED AGAINST THE CURRENT SCHEMA

**Table count, verified directly** (`Sim.Core/State/WorldState.cs`, `grep -c "public Table<"`):
**34** `Table<T>` fields, plus `Terrain` (shared by reference, never copied — ADR-008). This already
includes T4.3's three new tables (`Claims`, `Controls`, `Recognitions`), which merged after the
original R-3 measurement was taken — the earlier "0.078 MiB, measured while the table set is still
M3's" framing (`m4-spec.md`'s T4.16 prose) is stale; this ADR supersedes it with a live number.

**Re-run command** (the same instrument the original R-3 figure came from — `sim bench`'s
`Observe("clone")` phase, which brackets exactly `prev.Clone()` with
`GC.GetAllocatedBytesForCurrentThread`):

```
dotnet Sim.Cli/bin/Release/net10.0/Sim.Cli.dll bench --seed 42 --turns 300 --founded --json
```

**Result** (canonical founded world, 1024², N=12, seed 42, 300 turns):

```json
{"bucketRows":384,"cloneBytesPerTurn":81756,
 "phases":[{"name":"clone","totalMs":17.72,"allocatedBytes":24526800}, ...]}
```

`cloneBytesPerTurn` = **81,756 B** (≈0.078 MiB), against the original measurement's **82,096 B**
(`m4-spec.md` §7's cited baseline) — a **340-byte decrease**, not an increase, because `Claims`,
`Controls`, and `Recognitions` all carry **zero rows** in current gameplay (no system writes them
yet — T4.3 is schema-only). This confirms empirically what the schema implies: an EMPTY table's
marginal clone cost is the fixed per-`Table<T>` allocation overhead (a new empty backing array/list),
not proportional to any row count, and is small enough to be within run-to-run GC noise at this
scale. `bucketRows = 384` is unchanged from the original measurement (same founded-world population
structure; T4.3 added no population rows).

**Scaling still holds as previously derived** (`m4-spec.md` §7, not re-derived here since the
mechanism — bytes scale with row count — is unaffected by three new empty tables): ≈6,094 B per
settlement at the current bucket-cardinality; Charter late-game (800 settlements × 12 classes ×
16 cohorts = 153,600 rows) projects to **≈16 MB/turn**, already past the ratified ~150k bucket-row
cap, or ≈200 MB/turn at 4 cultures × 4 religions.

**Conclusion of the measurement: still not urgent by TODAY's number** (81,756 B/turn is negligible),
**but the projection is unchanged and the "cheap now, expensive later" argument stands** — every
milestone after this one adds tables (T4.8's notable stock, war state, later M5-M9 systems), and each
one grows the blast-radius inventory below before this ADR is written, exactly as the original
sequencing argument predicted. Measuring now, immediately after T4.3, is confirmed as the right
timing in retrospect — waiting further would have meant measuring against an even larger, less stable
table set.

## 4. THE THREE CANDIDATE APPROACHES — EVALUATED, NOT CHOSEN

| approach | mechanism | read-isolation | determinism | golden stability | measured/reasoned cost shape |
| --- | --- | --- | --- | --- | --- |
| **Copy-on-write per table** | Law 6 already declares which systems write which tables (`SystemCatalog`'s per-system table handles) — the kernel can know STATICALLY which of the 34 tables a given turn's pipeline can mutate; every unmodified table is shared by reference, exactly as `Terrain` already is | **Preserved exactly** — `Prev` is a set of references, some shared, some copied; a system that owns a table for writing still only ever sees its own writable handle, never a mutable `Prev` | Preserved — same values move through the same code paths, only the allocation pattern changes | Preserved by construction — output values are identical, only which arrays get freshly allocated changes; the WorldHash function serializes VALUES, not object identity, so no observable change is possible from this alone | **Best-case dramatic win**: at canonical scale, most of the 34 tables are untouched by most systems in most turns (e.g. `RngStreams`, `NetworkMeta`, `Deposits` after worldgen) — copying only the handful of tables a turn's active systems actually write could cut clone allocation by an order of magnitude or more. Requires no change to any system's read/write pattern, only to `Clone()`'s internals and to how `SimContext` hands out table references. |
| **Lazy clone on first write** | Same sharing effect as copy-on-write, but decided dynamically at first-write time inside the turn rather than declared statically from the pipeline's table ownership | Preserved, with a caveat: the copy-on-first-write decision must happen strictly before any read of the value that write would affect, within the SAME turn — an easy invariant to state, a genuinely new invariant to prove and gate in CI (today's read-isolation check is purely structural: it checks that `IReadOnlyWorldState`'s API compiles read-only; a lazy scheme adds a RUNTIME invariant a structural compile-time check cannot see) | Preserved if the invariant above holds | Preserved, same reasoning as copy-on-write | Similar potential upside to copy-on-write, but with LESS static guarantee — the "no dictionaries/no dynamic dispatch in hot paths" discipline (law 5) is naturally in tension with a scheme whose behavior depends on runtime write order rather than a fixed pipeline table |
| **Delta journal** | `Next = Prev` + a change list; reads consult the journal on top of the base | Preserved in principle (nothing physically mutates `Prev`) | Preserved if journal replay order is itself deterministic (an additional ordering surface to pin, not free) | Preserved | **Explicitly flagged by the director as the weakest candidate but not to be dismissed without measuring**: read cost is paid on every read that must consult the journal, and this is a read-heavy simulation (every system's `Step` reads most of `Prev` every turn) — the spec's own framing ("probably wrong... but MEASURE it") is the correct posture; this ADR does not have a working prototype to measure against and reports the theoretical shape only, per its design-only scope |

No candidate is chosen here. Copy-on-write is the most promising on paper (it reuses law 6's existing
static write-ownership declarations with no new runtime invariant), but "promising on paper" is not a
measurement — an actual prototype spike would be required to validate the projected allocation
reduction before a director ruling picks one, and building that spike is implementation, out of this
ADR's scope.

## 5. BLAST RADIUS — WHAT BREAKS, WHAT CHANGES

**Tests that would need to move or gain new assertions**, if any candidate above is later
implemented:
- The clone round-trip tests this file's own header warns about (`WorldState.cs`: "every added field
  MUST be included in Clone — the clone round-trip tests guard this") — these assert full-copy
  semantics today and would need to additionally assert NO cross-turn aliasing bug for whichever
  tables become conditionally-shared.
- T3.12a's two-axis determinism assertion (named precedent in the spec text) would need a
  vacuity-guarded extension proving the new scheme cannot silently share a table that WAS written.
- Every golden-hash test (`SnapshotTests`, `DrivenGoldenTests`, `FirstReignTests`,
  `CiPinAgreementTests`) is a live tripwire already — per non-negotiable 3, none of them should need
  re-pinning if the implementation is correct; if any moves, that is the STOP condition the spec
  names, not a re-pin to absorb.
- A new bench/regression test asserting the measured clone-cost reduction, to prevent silent
  regression back toward full-copy cost.

**Docs that would change**: `docs/m0-kernel-spec.md` §3.2 (the kernel contract's double-buffer
description) and this ADR's own eventual "RULED" follow-up entry once a director ruling picks an
approach.

**Packets touched**: none retroactively — T4.1 through T4.9 are all already merged/certified and
none of their content depends on clone internals (only on `Clone()`'s external contract: identical
`WorldState`, no shared mutable state with the source — which any of the three candidates preserves
by construction). Forward: any packet adding a new table (T4.8's notable stock is the next one named
in the spec) should be written with copy-on-write's static write-ownership declaration in mind if
that candidate is later chosen, so the declaration mechanism doesn't need retrofitting.

## 6. SCHEDULE PRICE

Not implemented, so no measured implementation cost — reasoned only, per this ADR's design-only
scope: copy-on-write requires (a) a mechanical pass over `SystemCatalog`'s existing per-system table
declarations to derive a static write-set per pipeline position, (b) restructuring `Clone()` to
accept that write-set and share the rest, (c) the new determinism/round-trip test coverage in §5.
This is bounded, mechanical work — not a kernel redesign — because law 6 already provides the
information the scheme needs; the schedule price is dominated by test coverage, not new logic.

## 7. RECOMMENDATION

**Do not implement in this ADR.** Recommend: (a) accept this ADR's re-measurement as the current
baseline (81,756 B/turn, 34 tables, superseding the M3-era 82,096 B/33-table figure); (b) a future
director ruling schedules a prototype spike of copy-on-write specifically (the only candidate with
both a strong theoretical case and no new runtime invariant to prove), sized as its own bounded
packet with the test coverage in §5 as its acceptance criteria; (c) lazy clone and delta journal
remain named, unmeasured alternatives, not eliminated, per the spec's own instruction to measure
rather than dismiss.

**Timing note for whoever schedules the follow-up**: this ADR's own reasoning about staleness
applies to itself — the longer implementation is deferred, the larger the blast-radius inventory in
§5 grows as more tables and systems are added. This is not urgency by TODAY's byte count; it is the
same "cheap now, expensive later" argument the original R-3 ruling made, re-confirmed rather than
resolved.

---

**STATUS: DESIGN AND MEASUREMENT COMPLETE. No implementation. Awaiting director ruling on which
candidate, if any, is scheduled and when.**
