# civ-sim (M3)

A deterministic, turn-based civilization simulation spanning 6,000 years. One human
director; AI agents build it, one task packet per session.

**M3 — The economy arrives.** At M2 every settlement was the same food-machine
running at a different size. At M3 they are *places that make different things*.
What the world can now do that it could not before:

- **Produce across five sectors** — farming, herding, extraction, crafting,
  construction — over a roster of real goods (grain, timber, stone, clay, ores,
  fibre, hides → tools, pottery, cloth, bronze). Recipes consume inputs; a
  workshop with no clay makes no pots.
- **Be ruled into a production mix.** The director allocates labour per
  settlement across the five sectors and the settlement's whole economy follows:
  what it makes, what it runs short of, what its people's needs read.
- **Price things.** A per-settlement, per-good price solver (D-033) runs on the
  exact closed form of its damped step (ADR-016), driven by consumption, input
  demand, production and stock release, and it settles rather than oscillating.
- **Want more than food.** Consumption is a class-weighted basket over six goods
  (D-035), aggregated by CES into Sustenance, Shelter and Comfort — so a
  well-fed settlement can still be poorly housed.
- **House people.** Dwellings are built, maintained and decay; Shelter is a real
  stock, and a settlement that stops maintaining housing degrades (T3.8).
- **Grow its own hinterland.** One dirt path can enlarge a settlement's arable
  catchment by 16.6% — infrastructure as a differentiator between places, live
  for the first time (T3.2b).

Its known-open edges are recorded with the same care as its features — see the
M3 entry in [`docs/milestones.md`](docs/milestones.md), which names what this
milestone deliberately did NOT deliver, each with a measurement and an owner.
The largest: **goods do not yet trade on the canonical world**, for two measured
and escalated reasons.

Start with [`CLAUDE.md`](CLAUDE.md) (agent constitution) and
[`docs/m3-spec.md`](docs/m3-spec.md) (current milestone spec).

## Prerequisites

- .NET 10 SDK (`dotnet --version` → 10.0.x)

## Run commands

```bash
# Build everything
dotnet build Sim.slnx

# Run the test suite (xUnit + FsCheck)
dotnet test Sim.slnx

# Banned-constructs check (determinism gate, m0-kernel-spec §3.7) — run before every commit
./scripts/check-banned-constructs.sh

# Read-only view proof (T0.2 acceptance): passes only when mutation through
# IReadOnlyWorldState FAILS to compile
./scripts/check-readonly-proof.sh

# Headless CLI runner — see "CLI" below
dotnet run --project Sim.Cli --configuration Release -- run --seed 42 --turns 1630 --report
```

## CLI

`sim` is a scripting surface: deterministic output; exit code **0** on success,
**1** on usage errors, **2** on runtime failures — exit codes are its contract.

```bash
# Run a campaign; optionally save a snapshot at turn K, log per-turn hashes
# (one lowercase hex WorldHash per line, \n-terminated), consume an order log
sim run --seed S --turns N [--report] [--save-at K --save PATH]
        [--orders PATH] [--hash-log PATH]

# Recompute and print the canonical hash of a save
sim hash SAVEFILE

# Replay from seed + order log (the D-008 recovery path)
sim replay --seed S --orders PATH --turns N [--hash-log PATH]
        [--report-jsonl PATH [--report-every N]]

# Per-phase wall time and allocations (clone + each system, first-seen order),
# plus the state footprint: bucket row count and clone bytes per turn (T3.11 —
# the instrument for the m0-kernel-spec §3.2 clone-size claim)
sim bench --seed S --turns N [--founded [--settlements N]] [--json]

# T2.8 calibration data source: N independent canonical founded worlds
# (seeds seed-base..seed-base+N-1, default base 1), T no-order turns each,
# per-seed metrics to OUT.json. Deterministic: same (seed, turns) => same bytes.
sim autoplay --seeds N --turns T --metrics OUT.json [--seed-base S]
```

`sim bench --json` emits one JSON object — the future perf-gate input (no gate
yet: toy systems would make thresholds meaningless):

```json
{ "seed": 42, "turns": 500, "totalMs": 9.19,
  "bucketRows": 384, "cloneBytesPerTurn": 82096,
  "phases": [ { "name": "clone", "totalMs": 0.88, "allocatedBytes": 335648 }, … ] }
```

### Replay diagnostic report (schema `replay-report/v1`)

`sim replay --report-jsonl PATH` turns any played session into a **reproducible
dataset**. Without it, an orders `.bin` plus a chronicle `.txt` carry almost no
state — the chronicle records emergence and migration events only, the orders log
records inputs rather than outcomes, and everything between (stocks, prices,
needs, grievance, class counts, sector mixes) is visible only to whoever is
sitting at the machine. That makes the player the measuring instrument.

One JSONL line per reported turn:

```json
{ "schema": "replay-report/v1", "turn": 60, "year": 600.0, "dtYears": 10.0,
  "hash": "…64 hex…", "totalPopulation": 20431, "totalFood": 3120044,
  "totalTradeFlow": 0,
  "settlements": [ {
    "id": 0, "population": 1783,
    "cohorts": [16 counts],
    "classes": [ { "id": 0, "name": "Peasants", "count": 1783, "active": 1,
                   "needs": { "Sustenance": 0.97, "Shelter": 1.0, "Comfort": 0.0 },
                   "grievance": 132.46 } ],
    "sectors": { "farming": 0.55, "herding": 0.15, … },
    "goods":   [ { "name": "grain", "stock": 260003, "demanded": 8915,
                   "eaten": 8915, "produced": 12004, "price": 1.0 }, … ],
    "housing": { "dwellings": 421, "maintenanceFraction": 1.0,
                 "sizeTier": 2, "arableKm2": 4210.6 }
  } ] }
```

**JSONL, not CSV, deliberately.** The data is ragged — variable classes per
settlement, each with variable bound needs, alongside ~13 goods carrying several
numbers each. CSV forces either a column explosion or several files joined on a
composite key, and both bake registry sizes into a header contract that breaks
whenever a good or class is added. JSONL is self-describing per line, streams,
appends without a header, and `jq` reads it directly.

**Volume, measured** (canonical founded world, N = 12, 650 turns):

| interval | size |
|---|---|
| `--report-every 1` (**default**) | **12.5 MiB** |
| `--report-every 10` | 1.25 MiB |

The default is **every turn**: a diagnostic that silently skips turns can hide
the exact turn a finding occurred, so the default is lossless and the flag exists
for when volume matters.

**It is strictly an observer.** The report is written FROM the post-step world and
never feeds back; the step call is identical whether or not reporting is on.
Asserted, not assumed (`ReplayReportTests`): the same log produces the same world
hash with and without `--report-jsonl`, and the report bytes themselves are
identical across runs.

### Autoplay metrics (schema `autoplay-metrics/v1`)

The JSON `sim autoplay --metrics` emits is the calibration battery's **input
contract** — `Sim.Core.Kernel.AutoplayMetrics`/`CalibrationAnalysis` compute
the same objects in-process for the CI battery
(`Sim.Tests/Systems/CalibrationBatteryTests.cs`), and the corridor bands live
in `Sim.Data/content/corridors.json` (TUNE data, D-006):

```json
{ "schema": "autoplay-metrics/v1", "turns": 650,
  "seeds": [ {
    "seed": 1, "worldHash": "…64 hex…",
    "finalPopulation": 115627, "finalYear": 4500.0,
    "settlementCount": 12,
    "arableKm2": 388145.2,          // Σ EffectiveFarmland × lattice-block km²
                                    // (fertility-WEIGHTED arable — the honest
                                    // definition; raw land area would flatter
                                    // density by counting desert as arable)
    "finalCohortTotals": [16, 5-year cohort counts],
    "series": {                     // parallel arrays, one entry per turn
      "year": [...], "dtYears": [...], "population": [...],
      "births": [...], "deaths": [...],        // deaths = base + starvation
      "starvationDeaths": [...],               // per-turn ledger-sink delta
      "migrationGross": [...]                  // Σ settlement outflows
    },
    "derived": {
      "densityPerArableKm2": 0.298,
      "migrationGrossPerDecade": 0.0004,       // fraction of pop per decade
      "crashCount": 0                          // ≥20% peak-to-trough drawdowns
    } } ] }
```

Nightly (`calibration-nightly` job, cron + manual dispatch) sweeps ≥20 seeds:
`sim autoplay --seeds 20 --turns 650 --metrics nightly-metrics.json`.

**Density vs D-015 ("map feels small") verdict, T2.8:** at year 4500 the
canonical world holds ~0.30–0.36 people per fertility-weighted arable km²
(measured across seeds) — three orders of magnitude below mature agrarian
land use (~10–30/km²). The map is **not** small for M2's horizon; the D-015
concern is about *travel scale*, not carrying capacity, and no worldgen
resize is warranted on density grounds.

> **SUPERSEDED AT T3.2b (CR-002) — kept because the reasoning is still
> instructive.** The verdict above was measuring a DENOMINATION BUG: "arable
> km²" was fertility-weighted lattice NODES scaled by block area in one consumer
> and not in the other, and the 205 km catchment radius that made the world look
> full was compensating for a yield constant denominated 256× too coarse. Both
> are fixed. At a 50 km economic hinterland the twelve settlements claim ~2 % of
> the continent, so the honest picture is the opposite of "small": the world is
> overwhelmingly EMPTY, and the open item is that no mechanism lets a growing
> population take the frontier. Reframed in `docs/queue.md` as an expansion
> opportunity, M4-targeted (colonization / land clearance, CR-003 §5.2(a)).

CI runs three jobs on every push: `build-and-test` (gates + full suite),
`determinism` (the T0.8 in-process harness), and `determinism-xproc` (T0.9:
two separate `sim run` processes must produce byte-identical hash logs, and
`sim replay` must reproduce an ordered run byte-identically — separate processes
surface environment/JIT divergence the in-process twins share).

CI (`.github/workflows/ci.yml`) runs the banned-constructs check, build, and tests on
every push and pull request.

## Download & Play

No toolchain needed — download, unzip, run `Sim.Ui.exe`.

**Latest build** (every merge to `main`): Actions → the newest `ui-artifact`
run on `main` → download the `sim-ui-win-x64-<sha>` artifact.
**Stable milestones**: the [Releases page](../../releases) — publishing a
release automatically attaches its zip as a permanent asset.
**Gate builds**: every `t<N>.*` packet-branch push produces the same artifact
for Director Visual Gates.

The window title and debug panel both show `civ-sim M3 (<sha>, <date>)` — the
build you are holding is never ambiguous. Optional flags: `--seed N` (default
42) and `--size PX` (dev-preview world size; a non-canonical size is recorded
in the session-log filename).

Each played session autosaves TWO files next to the exe, twinned by the same
timestamp:

- `runs/orders-<yyyyMMdd-HHmmss>[-sPX][-nN].bin` — the order log (the replay
  input; lexicographic order = chronological, so back-to-back gate logs sort
  and sweep trivially);
- `runs/chronicle-<yyyyMMdd-HHmmss>[-sPX][-nN].txt` — the annals export (T2.9),
  byte-exactly the Annals panel's lines.

A session log + its seed replays hash-identically:

```bash
sim replay --founded --seed S --orders runs/orders-<stamp>.bin --turns N
# played on --size PX? add: --size PX  (the -sPX filename suffix tells you)
# played with --settlements N? add: --settlements N  (the -nN suffix tells you)
```

## Solution layout

| Project | Purpose |
|---|---|
| `Sim.Core/` | Kernel + all simulation systems; zero UI/IO deps beyond data loading. Subfolders: `Kernel/` (turn executor, clock, RNG, state infra, hashing, snapshots), `Systems/` (one folder per system), `State/` (WorldState — single source of truth) |
| `Sim.Data/` | JSON content files + schema validation (era table lives here) |
| `Sim.Cli/` | Headless runner: run / hash / replay / bench |
| `Sim.Tests/` | xUnit + FsCheck: unit, property, determinism, golden-run |
| `docs/` | Specs, addenda, ADRs (`docs/adr/`), amendment queue (`docs/queue.md`) |

**Dependency rule:** systems never reference each other — only `State` and `Kernel`.
Cross-system communication is exclusively through state tables and events.

## Milestone status

- **M0 — Simulation kernel: COMPLETE.** T0.1–T0.9 per `docs/m0-kernel-spec.md`:
  state infrastructure, PCG32 RNG registry, integer-day clock + era pacing,
  turn executor + pipeline-as-data, Ledger + exact conservation, canonical
  serialization + WorldHash + snapshots + order-log replay, the permanent
  determinism harness and cross-process CI jobs, and the `sim` CLI.
- **M1 — Walking skeleton: COMPLETE.** T1.1–T1.10 per
  `docs/m1-walking-skeleton-spec.md`: worldgen fields + hydrology, traversal
  lattice + pathfinding, settlement + catchment, population + food loop, labor
  orders + PathBuild, the Sim.Ui window (terrain, overlays, HUD, End Turn),
  founded-world harness + goldens, and the CI Windows artifact.
- **M2 — Population & Society: at the exit gate.** T2.1–T2.12 per
  `docs/m2-spec.md`: cohort buckets (D-026), class system + D-020 DSL, plural
  worldgen with partitioned catchments (N = 12), per-settlement UI rule,
  migration (D-021, stabilized: gap-closing caps + EMA-smoothed
  attractiveness), historical demographic retune on the ADR-011
  exponential-survival micro-step kernel (dt-invariant growth, era-boundary
  continuity pinned forever), needs registry + grievance stocks (read by
  nothing but UI/chronicle — grep-gated), autoplay + calibration battery with
  two-sided corridors, chronicle-lite + procedural names + annals export,
  time-series graphs on the D-028 UI ring buffer, and the T2.11 determinism
  horizon across the era gate. Exit accepted 2026-07-25 on the T2.13 fix
  evidence. (Post-exit record, CR-003: M2's Malthus corridor was measuring an
  artifact of two compensating errors, corrected at T3.2b — the mechanism is
  intact but the condition that made it visible was false. M2 does not reopen;
  the record states it.)
- **M3 — The economy arrives: AT THE EXIT GATE.** T3.1–T3.12 per
  `docs/m3-spec.md`: worldgen refresh + the goods/recipe roster, five-sector
  production (D-032) with the M2 scaffolding demolished, the CR-002 spatial and
  agronomic recalibration, the D-033 price solver on ADR-016 exact integration,
  D-035 consumption baskets and CES needs, D-034 trade & arbitrage, founding
  variation (ADR-017), settlement size + housing as a real stock, the market and
  sector-control UI with the trade panel, and the T3.11 harness work (a DRIVEN
  golden that finally exercises the goods economy). **What it deliberately did
  NOT deliver is recorded beside what it did** — see `docs/milestones.md`.
  Awaiting the director's exit session.

## Calibration battery

```bash
# The CI battery members (2 canonical + 2 dev seeds, bands from
# Sim.Data/content/corridors.json — TUNE data; two-sided, no-output-is-failure)
dotnet test Sim.Tests --configuration Release --filter CalibrationBatteryTests

# The >=20-seed sweep (the calibration-nightly CI job; also manual)
dotnet run --project Sim.Cli -c Release -- \
  autoplay --seeds 20 --turns 650 --metrics nightly-metrics.json
```
