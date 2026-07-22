# civ-sim

A deterministic, turn-based civilization simulation spanning 6,000 years. One human
director; AI agents build it, one task packet per session.

Start with [`CLAUDE.md`](CLAUDE.md) (agent constitution) and
[`docs/m0-kernel-spec.md`](docs/m0-kernel-spec.md) (current milestone spec).

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
```

`sim bench --json` emits one JSON object — the future perf-gate input (no gate
yet: toy systems would make thresholds meaningless):

```json
{ "seed": 42, "turns": 500, "totalMs": 9.19,
  "phases": [ { "name": "clone", "totalMs": 0.88, "allocatedBytes": 335648 }, … ] }
```

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
**Gate builds**: every `t1.*` branch push produces the same artifact for
Director Visual Gates.

The window title and debug panel both show `civ-sim M1 (<sha>, <date>)` — the
build you are holding is never ambiguous. Optional flags: `--seed N` (default
42) and `--size PX` (dev-preview world size; a non-canonical size is recorded
in the session-log filename).

Each played session autosaves its order log to
`runs/orders-<yyyyMMdd-HHmmss>[-sPX].bin` next to the exe — the filename stamp
makes lexicographic order chronological, so back-to-back gate logs sort and
sweep trivially. A session log + its seed replays hash-identically:

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

- **M0 — Simulation kernel: in progress.** Task packets T0.1–T0.9 per
  `docs/m0-kernel-spec.md` §4. Completed: T0.1 (scaffold + CI), T0.2 (state
  infrastructure: WorldState, tables, typed ids, double-buffer clone, read-only views),
  T0.3 (PCG32 RNG: stream registry keyed system×region, states in WorldState),
  T0.4 (integer-day SimClock per ADR-002; era-pacing table loader, D-006),
  T0.5 (turn executor, typed system contexts per ADR-003, pipeline-as-data,
  toy Weather/Growth systems proving one-turn lag and dt-correct integration),
  T0.6 (Ledger + Conserved wrapper per ADR-004, sources/sinks tables,
  ConservationAuditor, toy TradeSystem; conservation exact and grep-gated),
  T0.7 (canonical serialization per ADR-005, SHA-256 WorldHash, versioned
  snapshots, order log + replay with the SetRainBias toy order),
  T0.8 (determinism harness: 1,000-turn twin-runs, replay, per-turn conservation
  audit — the required "determinism" CI job, ADR-006),
  T0.9 (CLI: run/hash/replay/bench, exit-code contract, per-phase bench via
  executor observer; cross-process determinism CI job). M0 packets complete.
