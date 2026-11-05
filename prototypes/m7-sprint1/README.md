# M7 Sprint 1 — isolated, reversible prototype

**What this is.** Prototype code for the Director's M7 decisions of 3–4 November 2026 (brief:
`m7s1/director-brief-2026-11-04.md`; authoritative agent spec: `m7s1/implementation-spec.md`, both in the
session scratchpad). It demonstrates the deterministic foundation (integer time, event scheduler, interruption,
sub-turn orders, counter-based RNG), and — in other lanes — spatial, tactical, auto-resolve, logistics and
cross-platform-math work.

**What this is NOT.** It is not the battle kernel and it does **not** decide O-14 (where the authoritative battle
framework lives). Namespaces are `Proto.M7.*`; nothing is named `Sim.Battle`. It changes no `Sim.*` project, no
kernel contract, no `OrderRecord`, no schema, no golden, no CI file and no document outside this folder.

**Reversal.** Deleting `prototypes/m7-sprint1/` fully reverses this sprint's code. Nothing outside the folder
references it.

## Layout

| path | contents |
|---|---|
| `Directory.Build.props` | self-contained settings copied from the repo root (LangVersion latest, Nullable, ImplicitUsings, TreatWarningsAsErrors, InvariantGlobalization) + `CheckForOverflowUnderflow=true` for `Proto.M7`. It does NOT import the root props, so the folder builds anywhere. |
| `M7Proto.slnx` | `Proto.M7` (authoritative, integer-only), `Proto.M7.Tests`, `Proto.M7.Bench` — no `Sim.*` reference |
| `M7Econ.slnx` | economy lane (`Proto.M7.Econ*`, reads `Sim.Core`/`Sim.Data` read-only) — owned by the Slice F agent |
| `M7DetMath.slnx` | O-17 lane (`Proto.DetMath*`) — owned by the O-17 agent |
| `fixtures/era-pacing.json` | byte copy of `Sim.Data/content/era-pacing.json` (a test asserts equality when the repo file exists) |
| `scripts/check-proto-determinism.sh` | determinism scan of `Proto.M7/` (spec §1) |
| `reports/` | per-slice reports |

## Build / test / scan (always by explicit path; never build `Sim.slnx` for this)

```bash
cd prototypes/m7-sprint1
dotnet build M7Proto.slnx -c Release -nologo --disable-build-servers
dotnet test  M7Proto.slnx -c Release --no-build -nologo
./scripts/check-proto-determinism.sh          # exit 0 = clean
dotnet Proto.M7.Bench/bin/Release/net10.0/Proto.M7.Bench.dll   # timings (reporting only)
dotnet Proto.M7.Bench/bin/Release/net10.0/Proto.M7.Bench.dll spatial   # Slice C: A* 250x250, LOS, separation
dotnet Proto.M7.Bench/bin/Release/net10.0/Proto.M7.Bench.dll tactical  # Slice D: 50v50 / 60v60 battles (see reports/D-tactical.md)
dotnet Proto.M7.Bench/bin/Release/net10.0/Proto.M7.Bench.dll parity 40 4 out.md out.csv  # Slice E: full parity battery + calibration (reports/parity-report.md)
```

Test packages (xunit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, xunit.runner.visualstudio 3.1.4) restore from the
offline NuGet cache. No other packages.

## Determinism regime (spec §1)

`Proto.M7/` contains no IEEE floating point, no `decimal`, no wall clock, no hash-code logic, no unordered
collections and no LINQ ordering; `unchecked` appears only in `Rng/CounterRng.cs` (the mixer). Overflow is
checked; intermediates that can exceed `long` use `Int128`. Tests and the bench may use the IEEE type for
statistics/reporting only. The scan checks comments too (stricter than code-only).

## Open Director questions this prototype does NOT answer

Which events are "significant" (implemented as data), War-Pulse lengths per era (placeholder inputs only),
the action budget 2 vs 3 (input), the location of the battle framework (O-14), and every kernel/OrderRecord
change needed for real integration. See `reports/B-foundation.md` §Dependencies.
