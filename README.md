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

# Headless CLI runner (run/hash/replay/bench commands land with packet T0.9)
dotnet run --project Sim.Cli
```

CI (`.github/workflows/ci.yml`) runs the banned-constructs check, build, and tests on
every push and pull request.

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
  toy Weather/Growth systems proving one-turn lag and dt-correct integration).
