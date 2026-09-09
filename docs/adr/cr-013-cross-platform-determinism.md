# CR-013 — THE SAME SEED PRODUCES DIFFERENT WORLDS ON WINDOWS AND LINUX

**Status: OPEN — awaiting director ruling. No simulation code, constant,
equation, config, golden, corridor, band or quarantine was touched. This file
and the evidence below are the whole change.**

Raised under S8 §3 during T4.18. It was found by the T4.17 session trace on its
**first use against a real played session** — which is what that cross-check was
built for, and the reason the trace is written live rather than derived.

---

## §1 THE FROZEN ITEM IN CONFLICT

**CLAUDE.md law 5 — determinism.** The kernel contract's whole promise is that a
world is a pure function of its seed and its order log. Everything downstream
depends on it: golden pins, replay equivalence, the session record, and the
director's ability to hand a log to anyone and have them see what he saw.

Nothing in the repository states that the promise is scoped to one platform, and
the CI determinism jobs run on `ubuntu-latest` only, so the scope was never
tested either way.

## §2 THE EVIDENCE

The director played seed 42, canonical world, on **Windows x64**, from the CI
artifact built at commit `98f25c1`. The same commit was run on **Linux x64**.
Both were driven through the identical `UiSession` code path with **zero orders
issued before turn 19**, so turns 1–5 are pure simulation on both sides.

| turn | population | food | settlements | hash (Windows) | hash (Linux) |
| --- | --- | --- | --- | --- | --- |
| 0 | 5,140 | 82,041 | 12 | `0ba766a93454…` | `0ba766a93454…` |
| 1 | 4,330 | 5,752 | 12 | `b9d206c1fcf4…` | `b9d206c1fcf4…` |
| 2 | 4,041 | 4,822 | 12 | `545ca00bd148…` | **`28e62ffb26fb…`** |
| 3 | 3,987 | 4,508 | 12 | `605cabce825f…` | **`05a6190349d6…`** |
| 4 | 3,997 | 4,451 | 12 | `efcc28eb9d48…` | **`ec4257d61e7b…`** |
| 5 | 4,035 | 4,459 | 12 | `1ac4c45da9d0…` | **`ff62af93d533…`** |

Two facts make this precise rather than merely alarming:

1. **Every conserved integer quantity agrees exactly, on every turn.**
   Population, food and settlement count are identical to the unit at turns 0–5
   — and the population decomposition measured independently in
   `docs/m4-population-transient-investigation.md` reconciles on both. The
   divergence is confined to something double-valued elsewhere in the canonical
   stream.
2. **Turns 0 and 1 agree completely; turn 2 is the first divergence.** Turn 2 is
   also the first turn that CONSUMES `CatchmentSummaries`: `WorldFounding` writes
   no catchment rows, the table is empty at turn 0, and 12 rows appear at turn 1
   for turn 2 to read. That makes the catchment computation — travel costs and
   effective arable over the lattice, all doubles — the first suspect. **That is
   an inference from the timing, not a measurement**, because the director's
   world state was never captured; only its hashes were.

Determinism WITHIN a platform is intact and was re-verified, not assumed: on
Linux, a live `UiSession` and a headless replay of its log agree hash-for-hash at
every turn, with and without orders.

## §3 WHY IT MATTERS BEYOND THE HASH

- A session log the director records on Windows **will not reproduce on CI**.
  `sim inspect` correctly reports `REPRODUCTION FAILED at turn 2` for his real
  session — the tool is right, the world genuinely differs.
- The goldens are pinned from Linux CI. A Windows build is therefore not
  golden-clean, and nobody would have noticed, because the suite runs on Linux.
- Cross-machine playtest evidence cannot currently be pooled.

It does NOT invalidate the M4 playtest: the trajectory, the population transient
and the famine crises the director saw are all reproduced on Linux to the unit.
What differs is a quantity that has not yet moved an integer stock in the first
five turns — but "has not yet" is not "cannot".

## §4 THE LIKELY MECHANISM, NOT YET MEASURED

`double` arithmetic is IEEE-754 and platform-stable for `+ - * /`. The usual
culprits for exactly this signature are:

- **`Math.Pow` / `Math.Exp` / `Math.Log`**, which are library functions, not
  IEEE-mandated to be correctly rounded, and legitimately differ in the last
  ulp between platform math libraries.
- **FMA contraction**: a JIT fusing `a * b + c` into one instruction on one
  platform and not the other, changing the rounding of the intermediate.

Either produces a last-ulp difference that survives into the serialized stream
and hashes differently while integer stocks, which round to whole units, agree.

## §5 OPTIONS (≤3, per S8 §3)

1. **Scope the promise to one platform.** Declare Linux x64 the reference,
   document that Windows builds are for play and not for evidence, and have
   `sim inspect` say so when a manifest's build differs from the running
   platform. Cheapest; concedes cross-machine reproducibility.
2. **Find and fix the divergent expression.** Requires first MEASURING which
   table differs — capture a Windows snapshot at turn 2 and diff it against a
   Linux one, which is a `sim hash`/`Snapshot` exercise the director can run in
   ten minutes. Then eliminate the offending call (replace a `Math.Pow` with an
   exact form, or forbid FMA contraction). Correct; blast radius unknown until
   the expression is named; may move every golden.
3. **Cross-platform CI as a standing gate.** Add a Windows determinism job that
   runs the founded golden and compares against the Linux pin. Does not fix
   anything, but converts an invisible defect into a visible one and would have
   caught this at the commit that introduced it.

## §6 RECOMMENDATION

**Measure first (the diff in option 2), then rule.** Naming the divergent table
is cheap, and the choice between options 1 and 2 turns entirely on what it
finds: a cosmetic EMA is a different decision from a catchment area that feeds
production. Option 3 is worth doing regardless of that answer, and it is the
only one of the three that prevents a recurrence.

**Do not tune anything to make the hashes agree.** No constant moved for this
CR and none should.

## §7 BLAST RADIUS

Nothing yet — this file is documentation. Whatever is ruled will touch either
the kernel contract's stated scope (option 1), a simulation expression and
plausibly every golden (option 2), or CI only (option 3).

## §8 MEASURED (T4.19 lane E — the instrument, the Linux half, the Windows half pending)

**What this section is.** §6 said "measure first". Lane E of T4.19 built the
measuring instrument, produced the Linux half of the measurement, and put the
Windows half one click away. It did NOT obtain the Windows half — the exact
reason is in §8.4 — so the first divergent field is **not yet named**. Nothing
below is inferred; every number was produced by the commands shown, at the
commit shown, and the artifacts sit outside the tree at the paths shown.
No simulation code, constant or golden moved.

### §8.1 The instrument: `sim diff`

`sim diff A.bin B.bin [--size PX]` (Sim.Cli/SnapshotDiff.cs, commit `39ffd38`)
loads both saves (Snapshot.Load, terrain regenerated from the header seed per
ADR-008 — `sim hash` gained the same loader, because before this commit it
exited 2 on any founded save), serializes each through `CanonicalSchema.Write`,
and walks the two streams block by block in schema order (header, then blocks
3..39). It reports the FIRST divergent table / row / field — doubles as R text
AND their 64-bit pattern AND the signed ulp distance; longs and ints plainly —
then a per-table summary (rowsA, rowsB, compared, differing, first differing
row) so the full extent is visible, not only the first hit. Exit 0 identical,
1 different.

Why a stream walk and not an object-graph walk: the hash that diverged is
SHA-256 over exactly that stream, so a field that hashes differently is by
construction a byte run that compares unequal here — there is no second
opinion about which fields "count". The cost is a duplicated field layout,
guarded by `SnapshotDiffTests`: on a populated founded turn-2 world (928
catchment nodes, 576 buckets, 168 price terms) the walk must consume exactly
`CanonicalSchema.ExpectedLength` bytes on both streams; a +1 ulp flip of
`CatchmentSummaries[7].EffectiveArableKm2` must be reported at that table,
row and field with both bit patterns and "ulp distance B-A = 1" and as one
differing row in one table; a long flip and a −2 ulp flip in a later block
report in stream order; a row-count mismatch reports block-level and the
following blocks stay aligned. 4 tests, 4 pass.

### §8.2 The Linux half (measured on this container)

Commit `39ffd38` (simulation code identical to `feaf218`, the T4.18 merge —
lane E changed Sim.Cli, Sim.Tests, .github and this file only). Release
build, .NET SDK 10.0.110, RID `ubuntu.24.04-x64`, x86_64.

```
CLI="dotnet Sim.Cli/bin/Release/net10.0/Sim.Cli.dll"
for N in 1 2 3; do
  $CLI run --founded --seed 42 --turns $N --save-at $N --save xplat/linux/linux-turn$N.bin
done
for N in 1 2 3; do $CLI hash xplat/linux/linux-turn$N.bin; done
```

| turn | `sim hash` (world hash) | save file sha256 | bytes |
| --- | --- | --- | --- |
| 1 | `b9d206c1fcf43de020d46d2759cd8959c095e381579ab5af2c1b84ac56e7b5b5` | `94879814407ee66451f86ee697a0c791dda1872c8925f0a53dfd1904b90aafdf` | 106,917 |
| 2 | `28e62ffb26fb537432777aaee4bf1585efba5cfafdfcaa14887950f5b3450349` | `036f997f3d373c783238d13b2ed92674a484f2e64277755a1a03d6d21f5d5150` | 107,013 |
| 3 | `05a6190349d6536bd0623211491a3f90787028c1c562c02589af37a0473d40b8` | `56557d168db9eceafa20b903ce16a1ed51096b32bfd6960081ddfe93267c7c7e` | 107,077 |

- Turn 1 equals the director's trace value in §2 (`b9d206c1fcf4…`, full value
  confirmed to all 64 hex digits).
- Turn 2 equals the §2 Linux value (`28e62ffb26fb…`); turn 3 equals `05a6190349d6…`.
- `sim hash` of each save equals the hash `sim run` printed for that run, so
  the terrain-regenerating loader reproduces the running world, not a
  terrain-less one.
- `sim diff linux-turn1.bin linux-turn1.bin`: identical, 106,889 stream bytes
  (= 106,917 − 28 header bytes), 40 blocks, walked == length on both sides,
  exit 0.

The files are at
`/tmp/claude-0/-home-user-civDemo/aba79415-ffd0-5838-8e88-66b12c4a9a0f/scratchpad/xplat/linux/`
(outside the tree; the container is ephemeral, so the sha256 column above is
the durable record — any Linux x64 build of this commit must reproduce them
byte for byte, and `sim diff` will say where if it does not).

**The candidate set.** `sim diff linux-turn1.bin linux-turn2.bin` measures
which blocks CHANGE between turn 1 and turn 2 on Linux; a turn-2 divergence
must live in one of them (a table that does not change cannot diverge):
RngStreams (12/12 rows), LedgerFlows (25/29), CatchmentNodes (697/928 rows
compared, 928 → 934 rows), CatchmentSummaries (12/12), Buckets (192/576),
GoodStocks (144/168), ConsumptionDeficits (12/12), PathProgress (12/12),
Variables (24/48), MigrationFlows, SettlementVitals, SmoothedAttractiveness,
HarvestWeather, Housing (12/12 each), NeedSatisfaction (31/36), Grievance
(12/36), Prices and PriceTerms (112/168 each). Unchanged turn 1 → 2:
Settlements, NetworkMeta, Deposits, ClassStates, SettlementDistances,
Controls, Polities, Capitals. This narrows §4 but does not name anything.

### §8.3 The Windows half: the workflow

`.github/workflows/xplat-diagnostic.yml` (on branch `t4.19-lane-xplat`,
commit `39ffd38`, `workflow_dispatch` only). Two jobs, `windows-latest` and
`ubuntu-latest`, each: checkout → setup-dotnet 10.0.x → `dotnet build
Sim.Cli/Sim.Cli.csproj -c Release` → the same three `sim run` commands →
`sim hash` on each save, echoed into the log and into `hashes.txt` together
with the SDK version and RID → `actions/upload-artifact@v4` as
`xplat-win-x64-<sha>` / `xplat-linux-x64-<sha>`, 30-day retention. Both halves
then come from CI runners: runner-vs-runner, with the container files above as
a third point that must equal the Linux runner's.

Once both artifacts exist, the measurement is:

```
sim diff linux-turn1.bin win-turn1.bin    # expected identical (hashes agree in §2)
sim diff linux-turn2.bin win-turn2.bin    # the first divergent field
sim diff linux-turn3.bin win-turn3.bin
sim diff <ci-linux>/linux-turn2.bin <container>/linux-turn2.bin   # runner == container
```

### §8.4 Why the Windows half is pending — the exact error

Dispatch was attempted twice through the GitHub API, immediately after the
push and again ~8 minutes later:

```
POST https://api.github.com/repos/adityamisra010-cmd/civdemo/actions/workflows/xplat-diagnostic.yml/dispatches
→ 404 Not Found
```

`GET …/actions/workflows` lists two registered workflows (`ci.yml`,
`ui-artifact.yml`) — the two on `main`. GitHub registers a workflow for
dispatch-by-filename only from the default branch; a file that exists solely
on a feature branch is not dispatchable, whatever `ref` is passed. The lane
mandate was `workflow_dispatch` only and no touch of `main`, so the step
stopped here as instructed rather than adding a `push` trigger to force a run.

**One click for the director, in either of two ways:** (a) merge the lane
branch (or cherry-pick `39ffd38`'s workflow file) to `main`, then Actions →
xplat-diagnostic → Run workflow on any ref; or (b) on the lane branch, add
`push: {branches: [t4.19-lane-xplat]}` beside `workflow_dispatch` and push —
the run starts itself. Then download the two artifacts and run the four
`sim diff` lines in §8.3; the output IS the missing measurement, and the
classification below is what to fill in.

### §8.5 Classification — deferred to the measurement

Not made. Two outcomes are possible and `sim diff` distinguishes them in one
line: a first divergent field whose ulp distance is ±1 in a double-valued
column with every long column agreeing across every table (pure last-ulp
floating-point representation — §4's `Math.Pow`/`Exp`/`Log` or FMA
signature), versus a divergence that has already reached a long column or a
row count by turn 3 (propagated into simulation-meaningful state). §2's
"population, food and settlements agree to the unit" bounds the second case
but does not exclude it — `Buckets.Count` is one of 576 long columns in the
stream, and the summary table lists all of them.
