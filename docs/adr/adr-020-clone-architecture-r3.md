# ADR-020 — CLONE ARCHITECTURE (R-3): DESIGN AND MEASUREMENT

**T4.16, per `docs/m4-spec.md`. DESIGN AND MEASUREMENT ONLY — no implementation in this ADR.** This
is a director-override ADR under `spine-s8-governance-freeze.md` §1/§2: `WorldState.Clone()` and the
double-buffer model are inside the M0 kernel freeze, so any eventual implementation requires this ADR
plus a director ruling on it, not a Contradiction Report **yet**. Cut from `main` `ba96b1c`.

**Latent CR, acknowledged (was previously mis-stated as "nothing here conflicts with a frozen
item").** `docs/spine-s8-governance-freeze.md` §1 names *"perf gate unreachable at charter scale"* as
an EMPIRICAL contradiction of a frozen commitment, and `docs/m4-spec.md` §7 has already ruled
`docs/m0-kernel-spec.md` §3.2's *"at M0–M9 scale this is a few MB"* **wrong at the far end**. The
projection in §3 below is exactly that class of evidence. This ADR therefore sits ON TOP of a live,
already-recognised contradiction; it does not report one only because the ruling in `m4-spec.md` §7
already recorded it and scheduled T4.16 as the response. If a director rules that the frozen §3.2
text must be amended rather than superseded in place, a CR is owed — that is a director call, not
this ADR's to make.

Structured on the §3 Contradiction Report template (frozen items, evidence, options, blast radius,
recommendation) because it is the natural shape for this content, even though no contradiction is
being reported.

---

## 0. CORRECTIONS APPLIED (post-review)

This ADR was independently reviewed at commit `080e97f` and **REJECTED**. The following corrections
were applied. Nothing was re-run; every number below is verified from code or git in the reviewed
tree.

1. **Table baseline corrected 33 → 31.** The pre-T4.3 baseline was **31** `Table<T>` fields, not 33.
   Verified: `git show 7fd4004:Sim.Core/State/WorldState.cs | grep -c "public Table<"` = 31,
   `d8d8f48` = 31, `18ba393` (T4.3) = 34. T4.3 added three tables: **31 → 34**. §7's supersession
   line said "33-table"; corrected.
2. **The copy-on-write analysis was wrong and is withdrawn, not quietly amended.** `Table<T>.Clone()`
   (`Sim.Core/State/Table.cs:85`) allocates `new Table<T>(_count)` and `Array.Copy`s
   **unconditionally**, so three added EMPTY tables cost strictly MORE than zero — the previous text
   used them to EXPLAIN a 340-byte DECREASE, which is self-refuting. That attribution is withdrawn
   and the decrease is now reported as **UNEXPLAINED**. The written-table set was recounted from
   `Sim.Core/SystemCatalog.cs` and `Sim.Core/Kernel/TurnExecutor.cs` (the clone is once-per-turn,
   before the system loop, so the relevant set is the pipeline's UNION, not any per-system set): the
   claim that "most tables are untouched" is **false** and removed. `RngStreams` and `LedgerFlows`
   were cited as untouched examples; both are written **every turn** and the examples are removed.
   "SystemCatalog already declares per-system table ownership statically" is **downgraded** — no
   machine-readable write-set exists.
3. **Memory projection: the discontinuity is now disclosed and the arithmetic corrected.** The
   ≈6,094 B/settlement marginal and the 153,600-row projection are **not one chain** — they differ by
   6× in bucket cardinality per settlement.
4. **Blast radius extended** with `Sim.Ui.Tests` (entirely), `StateEquals`, `TurnExecutorTests`,
   `PluralWorldTests`, `TableTests`, the read-only-violation gate, ADR-008 and `m4-spec.md` §7.
5. **The "nothing here conflicts with a frozen item" parenthetical is removed** and replaced with an
   explicit latent-CR acknowledgement (see header).
6. **The recommendation changed** as a consequence of (2): copy-on-write is **no longer** presented
   as the strongest candidate. See §7.

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
(`m4-spec.md` §7's cited baseline) — a **340-byte decrease**.

**This decrease is UNEXPLAINED. The previous attribution was wrong and is withdrawn.** The earlier
text claimed the three T4.3 tables (`Claims`, `Controls`, `Recognitions`) are empty and therefore
"cost nothing", explaining the drop. That is self-refuting on two counts. First, an empty table's
marginal clone cost is not zero: `Table<T>.Clone()` (`Sim.Core/State/Table.cs:85`) is

```csharp
public Table<T> Clone()
{
    var copy = new Table<T>(_count);
    Array.Copy(_rows, copy._rows, _count);
    copy._count = _count;
    return copy;
}
```

— it allocates a `Table<T>` object **unconditionally**, for every table, empty or not (at `_count == 0`
the backing store is `Array.Empty<T>()`, so the row array is free, but the object header + two fields
are not). Three added tables are therefore three added allocations: **strictly greater than zero**.
Second, even if the cost were zero, zero added cost cannot produce a *decrease*. Something else
accounts for the 340 bytes; this ADR does not know what, did not re-run to find out (design-only
scope: T4.16 authorises the headline clone measurement reported in §3, but not a second diagnostic run to chase this delta), and **reports it as unexplained rather than rationalised**. A
follow-up packet that touches clone internals should resolve it before trusting either figure as a
regression baseline, since a 340-byte unexplained delta is the same order as the effect an
empty-table accounting change would have.

`bucketRows = 384` is unchanged from the original measurement (same founded-world population
structure; T4.3 added no population rows).

**Table-count history, verified from git** (`grep -c "public Table<"` on
`Sim.Core/State/WorldState.cs`): `7fd4004` = **31**, `d8d8f48` = **31**, `18ba393` (T4.3) = **34**,
HEAD = **34**. The pre-T4.3 baseline is **31**, not the "33" the first draft of this ADR asserted;
T4.3 added three, so the correct arithmetic is **31 → 34**.

**Scaling — the projection is INTERNALLY INCONSISTENT and is corrected here.** `m4-spec.md` §7
derives ≈6,094 B/settlement as a marginal by varying settlements 12 → 24 (73,906 → 147,036 B) while
bucket rows were **384 total at N=12**, i.e. **32 bucket rows per settlement**. The Charter
projection in the same table uses **153,600 rows / 800 settlements = 192 rows per settlement** — a
**6× cardinality change**, presented as if it were one continuous chain. It is not. Consequences:

- Applying the measured marginal literally gives **800 × 6,094 ≈ 4.9 MB/turn**, *not* 16 MB. The
  16 MB figure silently assumes the marginal scales with the 6× denser bucket cardinality, which the
  12→24 experiment never exercised.
- The two published projections do not agree with each other either: **16 MB / 153,600 ≈ 105 B per
  bucket row**, while **200 MB / 2.46M ≈ 81 B per bucket row**. Same row type, two different implied
  per-row costs — so at most one of them can be right, and neither was derived from a measurement at
  that cardinality.

**Therefore:** the ≈16 MB and ≈200 MB figures are hereby marked **un-re-derived M3-era arithmetic**,
carried forward for shape only. The defensible statements are (a) the measured marginal is
≈6,094 B/settlement **at 32 bucket rows/settlement**, giving ≈4.9 MB at 800 settlements of that
density, and (b) clone cost grows linearly in total row count, so a 6× denser bucket table costs
proportionally more — the exact constant is **unmeasured**. Any scheduling decision that leans on
"16 MB" or "200 MB" must re-derive them first.

**Conclusion of the measurement: still not urgent by TODAY's number** (81,756 B/turn is negligible),
**and the projection, once corrected, is weaker than previously stated but still directionally
alarming — the "cheap now, expensive later" argument stands on the linear-growth mechanism, not on
the specific megabyte figures** — every
milestone after this one adds tables (T4.8's notable stock, war state, later M5-M9 systems), and each
one grows the blast-radius inventory below before this ADR is written, exactly as the original
sequencing argument predicted. Measuring now, immediately after T4.3, is confirmed as the right
timing in retrospect — waiting further would have meant measuring against an even larger, less stable
table set.

## 4. THE THREE CANDIDATE APPROACHES — EVALUATED, NOT CHOSEN

| approach | mechanism | read-isolation | determinism | golden stability | measured/reasoned cost shape |
| --- | --- | --- | --- | --- | --- |
| **Copy-on-write per table** | Share every table the turn's pipeline does not write; copy the rest. **CORRECTED**: there is no existing static declaration to read. `SystemRegistration` (`Sim.Core/Kernel/TurnExecutor.cs:10-27`) carries only `Id`, `Name`, and an `Invoke` delegate; the table handles are expressions inside the closure bodies in `Sim.Core/SystemCatalog.cs`, evaluated at invoke time. **No machine-readable write-set exists** — the declaration mechanism must be BUILT, not read | **Preserved exactly** — `Prev` is a set of references, some shared, some copied; a system that owns a table for writing still only ever sees its own writable handle, never a mutable `Prev`. This is the scheme's one clear strength | Preserved — same values move through the same code paths, only the allocation pattern changes | Preserved by construction — the WorldHash function serializes VALUES, not object identity | **CORRECTED, and much weaker than first claimed.** The clone is **once per turn**, before the system loop (`TurnExecutor.cs:80`, `WorldState next = prev.Clone();`), so the relevant set is the pipeline's **UNION** of written tables, not any per-system set. Counted from `SystemCatalog.cs`: **27 distinct `next.*` tables**, plus **`RngStreams`**, written every turn via `new RngRegistry(next)` (`TurnExecutor.cs:83`; `RngRegistry.cs:59` takes `_world.RngStreams` and `RngStream.NextUInt32` mutates the row in place) — **28 of 34 written**. At most **6** tables (`Regions`, `Settlements`, `Deposits`, `Claims`, `Controls`, `Recognitions`) can be shared, and three of those six are T4.3's empty schema-only tables that cost almost nothing to copy anyway. The earlier claim that "most of the 34 tables are untouched" is **FALSE and withdrawn**, as are its two named examples: `RngStreams` is written every turn (above) and `LedgerFlows` is written by **15** registrations, each constructing `new Ledger(next.LedgerFlows)`. **Most damningly: `next.Buckets` is written every turn** (`SystemCatalog.cs:166`, `:200`, `:215` — demographics, class mobility, migration). Buckets is the ENTIRE growth term of the projection in §3 (153,600 rows at Charter scale). **Copy-on-write cannot avoid copying the one table this ADR exists to worry about.** Its ceiling is a small constant saving on six mostly-tiny tables, not "an order of magnitude" |
| **Lazy clone on first write** | Same sharing effect as copy-on-write, but decided dynamically at first-write time inside the turn rather than declared statically from the pipeline's table ownership | Preserved, with a caveat: the copy-on-first-write decision must happen strictly before any read of the value that write would affect, within the SAME turn — an easy invariant to state, a genuinely new invariant to prove and gate in CI (today's read-isolation check is purely structural: it checks that `IReadOnlyWorldState`'s API compiles read-only; a lazy scheme adds a RUNTIME invariant a structural compile-time check cannot see) | Preserved if the invariant above holds | Preserved, same reasoning as copy-on-write | Similar potential upside to copy-on-write, but with LESS static guarantee — the "no dictionaries/no dynamic dispatch in hot paths" discipline (law 5) is naturally in tension with a scheme whose behavior depends on runtime write order rather than a fixed pipeline table |
| **Delta journal** | `Next = Prev` + a change list; reads consult the journal on top of the base | Preserved in principle (nothing physically mutates `Prev`) | Preserved if journal replay order is itself deterministic (an additional ordering surface to pin, not free) | Preserved | **Explicitly flagged by the director as the weakest candidate but not to be dismissed without measuring**: read cost is paid on every read that must consult the journal, and this is a read-heavy simulation (every system's `Step` reads most of `Prev` every turn) — the spec's own framing ("probably wrong... but MEASURE it") is the correct posture; this ADR does not have a working prototype to measure against and reports the theoretical shape only, per its design-only scope |

No candidate is chosen here, and — **corrected** — copy-on-write is no longer the promising one.
Its case rested on two claims that do not survive checking the code: that `SystemCatalog` already
declares write-ownership machine-readably (it does not — see the row above), and that most tables go
untouched per turn (they do not — 28 of 34 are written, including `Buckets`). What remains of
copy-on-write is a real but small win — up to six shareable tables, half of them empty — bought with
a new declaration mechanism that must be built and kept correct forever, against the exact table
whose growth is the problem. On the corrected evidence, **none of the three candidates has been shown
to address the projected cost**, because all three are asked to solve "copying 153,600 bucket rows
every turn" and only the delta journal even attempts to avoid copying a table that IS written.

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

- **`Sim.Ui.Tests` ENTIRELY** (missing from the first draft). The UI retains `WorldState` objects
  ACROSS turns — `UiSessionReplayTests` and `HistoryBufferTests` in particular — which is precisely
  where any sharing/aliasing scheme bites: a table shared by reference between a retained historical
  `WorldState` and the live one silently rewrites history. Every test in that project is in the blast
  radius, not just the two named.
- **`Sim.Tests/TestUtil/WorldStates.cs`'s `StateEquals`** — it compares by VALUE, so it would return
  `true` for a state whose table is an alias of another state's rather than a copy. **It cannot
  detect the bug any sharing scheme risks introducing.** It is the helper that must be EXTENDED (with
  reference-identity assertions for tables expected to be distinct) before any candidate is
  implemented — not a test that will catch the problem as written.
- **`Sim.Tests/Kernel/TurnExecutorTests.cs`** — double-buffer isolation is asserted here; these are
  the assertions any sharing scheme must be reconciled with first.
- **`Sim.Tests/State/TableTests.cs`** — `Table<T>.Clone()`'s own contract tests.
- **`Sim.Tests/State/WorldStateCloneTests.cs`** — the round-trip tests named above.
- **`Sim.Tests/Worldgen/PluralWorldTests.cs`** — plural culture/religion worlds are the high-bucket
  -cardinality case, i.e. the configuration the projection is about.
- **The read-only-violation compile-time gate** (`Sim.Tests.ReadOnlyViolation`, referenced from
  `Sim.Core/State/Table.cs`'s `IReadOnlyTable<T>` doc comment) — it proves `IReadOnlyWorldState`
  exposes no mutation surface STRUCTURALLY. Sharing a table by reference does not violate that gate
  while still breaking read-isolation in fact, so the gate must be extended or supplemented.

**Docs that would change**: `docs/m0-kernel-spec.md` §3.2 (the kernel contract's double-buffer
description, already ruled wrong at the far end by `docs/m4-spec.md` §7); **`docs/m4-spec.md` §7**
itself (its 16 MB / 200 MB figures are corrected in §3 above and its measurement table needs the
cardinality discontinuity noted); **ADR-008** (`Terrain` shared by reference — the existing precedent
and prior art for any by-reference sharing, and the doc that would have to generalise from one
special-cased table to a policy); and this ADR's own eventual "RULED" follow-up entry.

**Packets touched**: none retroactively — T4.1 through T4.9 are all already merged/certified and
none of their content depends on clone internals (only on `Clone()`'s external contract: identical
`WorldState`, no shared mutable state with the source — which any of the three candidates preserves
by construction). Forward: any packet adding a new table (T4.8's notable stock is the next one named
in the spec) should be written with copy-on-write's static write-ownership declaration in mind if
that candidate is later chosen, so the declaration mechanism doesn't need retrofitting.

## 6. SCHEDULE PRICE

Not implemented, so no measured implementation cost — reasoned only, per this ADR's design-only
scope. **CORRECTED — the "mechanical pass" claim was false.** Copy-on-write would require (a)
**building** a machine-readable write-set declaration that does not exist today: `SystemRegistration`
carries only `Id`/`Name`/`Invoke`, and each system's tables are expressions inside closure bodies in
`SystemCatalog.cs` evaluated at invoke time, so there is nothing to "read" — every registration must
be given an explicit, hand-written, reviewed write-set, and a gate must exist to keep it in sync with
the closure body forever or the scheme silently corrupts state; (b) restructuring `Clone()` to accept
that write-set and share the rest; (c) the test coverage in §5, which is now materially larger than
the first draft assumed (all of `Sim.Ui.Tests`, plus `StateEquals` gaining reference-identity
assertions it does not have). This is **not** bounded mechanical work: it adds a permanent
hand-maintained invariant to the kernel, and the cost is dominated by that invariant plus the test
extension — for a ceiling of six shareable tables (§4).

## 7. RECOMMENDATION

**REWRITTEN post-review. The previous recommendation rested on the copy-on-write claims corrected in
§4 and does not survive them.**

**Do not implement in this ADR.** Recommend:

**(a) Accept the corrected baseline**: 81,756 B/turn against **34** tables, superseding the M3-era
82,096 B / **31**-table figure (T4.3 took it 31 → 34). **With the 340-byte decrease recorded as
UNEXPLAINED** (§3) — it is not attributable to the three empty tables, which cost strictly more than
zero, and no re-measurement was authorised to chase it.

**(b) Treat the projection figures as un-re-derived.** ≈16 MB and ≈200 MB are M3-era arithmetic
carried across a 6× bucket-cardinality discontinuity and are mutually inconsistent (≈105 vs ≈81 B per
row). The literal application of the measured marginal is ≈**4.9 MB** at 800 settlements. **The first
thing any follow-up packet should do is re-derive the projection at the target cardinality** — before
any architecture is chosen, because at 4.9 MB/turn the case for a kernel-freeze override is much
weaker than at 16 MB, and nobody currently knows which number is right.

**(c) Copy-on-write is NO LONGER the recommended spike.** Corrected evidence: 28 of 34 tables are
written every turn; at most 6 are shareable and 3 of those are empty; `Buckets` — the whole growth
term — is written every turn, so CoW cannot touch the actual problem; and the static write-set it
needs must be **built and permanently maintained**, not read. It buys a small constant and adds a
standing correctness hazard. **Recommend it be de-prioritised, not spiked.**

**(d) On the corrected evidence the strongest remaining candidate is the one the spec called
weakest — but only conditionally.** Only the delta journal even attempts to avoid copying a table
that IS written, which is what the projection demands. Its cost is on the read side, in a read-heavy
simulation, and this ADR has no prototype and no authority to build one. So: **recommend no
architecture yet.** Recommend instead that the next bounded packet be a **measurement** packet — (i)
re-derive the projection at Charter bucket cardinality, (ii) measure what fraction of clone bytes is
`Buckets` alone. If `Buckets` dominates — which §4's growth argument MOTIVATES but does not establish, since §4 shows only that `Buckets` is written every turn and is the growth TERM, not that it dominates clone bytes at today's 384 bucket rows — then the problem is a **bucket
representation** problem (sparsity, compression, or delta-journalling that one table) rather than a
whole-`WorldState` clone-architecture problem, and the three candidates in §4 are the wrong menu.
**That reframing is the main substantive change this correction pass produces and it should be ruled
on before any spike is scheduled.**

**(e) Lazy clone and delta journal remain named, unmeasured alternatives**, not eliminated, per the
spec's own instruction to measure rather than dismiss.

**Timing note for whoever schedules the follow-up**: this ADR's own reasoning about staleness
applies to itself — the longer implementation is deferred, the larger the blast-radius inventory in
§5 grows as more tables and systems are added. This is not urgency by TODAY's byte count; it is the
same "cheap now, expensive later" argument the original R-3 ruling made, re-confirmed rather than
resolved.

---

**STATUS: DESIGN AND MEASUREMENT COMPLETE. No implementation. Awaiting director ruling on which
candidate, if any, is scheduled and when.**
