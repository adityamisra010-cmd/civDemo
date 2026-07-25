# civ-sim (M2)

A deterministic, turn-based civilization simulation spanning 6,000 years. One human
director; AI agents build it, one task packet per session.

**M2 — Population & Society**: cohort demographics with historical vital rates
(ADR-011 exponential-survival micro-step integration, dt-invariant across the
era-pacing arc), a class system (Peasants + emergent Artisans, D-020 DSL),
twelve settlements with travel-time-partitioned catchments, migration
(differential-driven + famine flight, stabilized per D-021), needs/grievance
stocks (Sustenance-bound, display-only until M5), chronicle-lite with
procedural settlement names and exported annals, time-series graphs, and an
autoplay batch runner with a corridor-checked calibration battery.

Start with [`CLAUDE.md`](CLAUDE.md) (agent constitution) and
[`docs/m2-spec.md`](docs/m2-spec.md) (current milestone spec).

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

# Per-phase wall time and allocations (clone + each system, first-seen order)
sim bench --seed S --turns N [--json]

# T2.8 calibration data source: N independent canonical founded worlds
# (seeds seed-base..seed-base+N-1, default base 1), T no-order turns each,
# per-seed metrics to OUT.json. Deterministic: same (seed, turns) => same bytes.
sim autoplay --seeds N --turns T --metrics OUT.json [--seed-base S]
```

`sim bench --json` emits one JSON object — the future perf-gate input (no gate
yet: toy systems would make thresholds meaningless):

```json
{ "seed": 42, "turns": 500, "totalMs": 9.19,
  "phases": [ { "name": "clone", "totalMs": 0.88, "allocatedBytes": 335648 }, … ] }
```

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

The window title and debug panel both show `civ-sim M2 (<sha>, <date>)` — the
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
  horizon across the era gate. Awaiting the director's exit session.

## Calibration battery

```bash
# The CI battery members (2 canonical + 2 dev seeds, bands from
# Sim.Data/content/corridors.json — TUNE data; two-sided, no-output-is-failure)
dotnet test Sim.Tests --configuration Release --filter CalibrationBatteryTests

# The >=20-seed sweep (the calibration-nightly CI job; also manual)
dotnet run --project Sim.Cli -c Release -- \
  autoplay --seeds 20 --turns 650 --metrics nightly-metrics.json
```
