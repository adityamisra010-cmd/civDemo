# M0 — SIMULATION KERNEL: SPEC & TASK PACKETS
### Stack: C#/.NET (approved). This document is execution-ready for Claude Code, one packet per session.

---

## 1. DECISION LOG ENTRIES CLOSED THIS DOCUMENT

- **D-001 Language/stack: C#/.NET — APPROVED by director.** Target **.NET 10 LTS**, C# latest. `Sim.Ui` (M1) may pin .NET 8 if MonoGame tooling requires; kernel projects stay on 10.
- **D-002 UI stack (for M1+, locked now to prevent drift): MonoGame + ImGui.NET.** MonoGame renders the map surface; ImGui renders every panel, table, graph, and decision queue — the "debug UI is the game UI" doctrine in its native form. Alternative logged: ASP.NET local web dashboard (rejected: splits stack into C#+JS).
- **D-003 Test stack: xUnit + FsCheck** (property-based). Dependency philosophy: these two NuGet packages are the *only* dependencies at M0.
- **D-004 Numeric policy (refines Spine S3):** conserved stocks (**people, money, goods**) are `long` integers in base units; rates, prices, and ratios are `double`. Fractional flows convert to integer transfers via a per-entity **remainder accumulator** (deterministic; no stochastic rounding). Consequence: Law 1 conservation tests assert **exact equality**, not epsilon tolerance. `float` is banned project-wide.
- **D-005 Units:** population stored as persons (`long`); no "pop unit" abstraction in state (display-layer may aggregate). Money as `long` minor-units of an abstract currency; goods as `long` base units defined per-good in data.
- **D-006 Era-pacing table v1 (data file, all values `TUNE`):**

| Era band | Sim years | dt/turn | Turns |
|---|---|---|---|
| Neolithic | 4000–1500 BCE | 10 y | 250 |
| Bronze/Iron | 1500 BCE–500 CE | 5 y | 400 |
| Medieval | 500–1400 | 3 y | 300 |
| Early Modern | 1400–1800 | 2 y | 200 |
| Industrial | 1800–1920 | 1 y | 120 |
| Modern | 1920–2000 | 0.5 y | 160 |
| Information+ | 2000–2100 | 0.5 y | 200 |

Total ≈ **1,630 turns** (inside the 1,200–2,000 budget). dt-authority rule (Spine S3) applies from M6 when eras diverge; until then the table indexes on world date. Crisis-zoom subdivision is an M4+ feature; M0 only guarantees variable-dt correctness.
- **D-007 RNG: PCG32**, one named stream per (system × region), **stream states live inside WorldState** so save/load/replay preserve randomness exactly.
- **D-008 Save format:** custom versioned binary snapshots (deterministic canonical serialization, doubles as raw bits). Saves may break between milestones; `seed + order log` replay is the recovery path. No third-party serializer.

**Still open:** D-009 map substrate & cell count and D-010 pop-bucket key/cap (both close with the M1 spec); D-011 battles resolved-only vs watchable (director's call, gates M4 military spec effort).

---

## 2. SOLUTION LAYOUT

```
/sim
  Sim.Core/        // kernel + all simulation systems; zero UI/IO deps beyond data loading
    Kernel/        // turn executor, clock, rng, state infra, hashing, snapshots
    Systems/       // one folder per system (M0: toy systems only)
    State/         // WorldState + tables (single source of truth)
  Sim.Data/        // JSON content files + schema validation (era table lives here)
  Sim.Cli/         // headless runner: run / hash / replay / bench
  Sim.Tests/       // xUnit + FsCheck: unit, property, determinism, golden-run
  docs/            // spine docs, ADRs (adr-NNN-title.md), this spec
  CLAUDE.md        // agent constitution (§6 below)
```

**Dependency rule (mechanically enforced by project references):** `Systems/*` reference only `State` and `Kernel` — never each other. Cross-system communication is exclusively through state tables and events.

---

## 3. KERNEL CONTRACT (C# SHAPE)

**3.1 State & ownership.** `WorldState` is plain data: a set of tables (arrays/lists of structs), each owned by exactly one system, plus RNG stream states and the clock. Read/write enforcement **by construction**:

```csharp
public interface ISimSystem {
    SystemId Id { get; }
    void Step(SimContext ctx);   // ctx exposes: IReadOnlyWorldState Prev,
}                                // writable refs ONLY to this system's own tables,
                                 // RngStream rng, double dtYears, OrderBatch orders
```

The kernel constructs each system's `SimContext` with typed writable handles to its owned tables only. An agent cannot silently write another system's state — the reference does not exist.

**3.2 Double buffering.** At turn start the kernel clones `Prev → Next` (full copy; at M0–M9 scale this is a few MB — simplicity beats cleverness, revisit only if profiling gates fail). Systems read `Prev`, write `Next`. One-turn lag is therefore the default; deliberate same-turn edges (later milestones) are explicit kernel-ordered handoffs listed in the interaction matrix.

**3.3 Turn pipeline.** Fixed ordered `SystemId[]` from Spine S3, loaded as data, executed sequentially. M0 ships the executor plus toy systems; real systems slot in from M1 without executor changes.

**3.4 Clock.** `SimClock { long Turn; double WorldDateYears; double DtYears; }` — dt from the era table. **Discipline enforced from day one:** every rate in the codebase is per-sim-year; every integration is `stock += rate * dtYears` (or the integer-flow equivalent). A system that hardcodes per-turn amounts fails review.

**3.5 RNG.** PCG32 (~30 lines, implement from the reference constants; no `System.Random` anywhere). `RngRegistry.Get(systemId, regionId)` returns the stream; `NextDouble()` builds a 53-bit double from two 32-bit draws. Stream states serialize with WorldState.

**3.6 Conserved-transfer pattern (Law 1 made mechanical).** All mutations of conserved stocks go through one API:

```csharp
Ledger.Transfer(ref long from, ref long to, long amount);      // throws if amount<0 or from<amount (policy per call site)
Ledger.Flow(ref long stock, ref long sinkOrSource, long amount); // births/deaths/mint/burn hit tracked ledgers
```

Direct `+=`/`-=` on conserved fields is banned by convention + CI grep. Total = initial + sources − sinks holds **exactly** at every turn; the property suite fuzzes random operation sequences and asserts it.

**3.7 Determinism rules — banned constructs list (agents check before every commit):**
`System.Random` · `DateTime.Now`/`UtcNow` in sim code · `float` · `AsParallel`/unordered `Parallel.*` · iteration over `Dictionary`/`HashSet` for sim logic (use arrays or sort keys first) · `GetHashCode()` as logic input · culture-sensitive parse/format (always `InvariantCulture`) · LINQ in hot paths (allocations; loops in kernel/systems).

**3.8 Snapshot & hash.** Canonical serialization: tables in fixed schema order, fields in declaration order, `long` and raw double bits via `BinaryWriter`; header `{magic, version, seed, turn}`. `WorldHash` = SHA-256 of the canonical stream. This single function powers saves, the determinism harness, and golden-run regression.

**3.9 Order log.** Every external input to the sim (player/AI orders — at M0, none or synthetic) is appended to a per-run order log `{turn, actorId, orderPayload}`. `replay(seed, orderLog)` must reproduce the world hash-for-hash. The order log is the second half of determinism and the save-recovery path (D-008).

---

## 4. TASK PACKETS (one Claude Code session each; do in order)

**T0.1 — Scaffold + CI.** Create solution per §2; GitHub Actions: `dotnet build` + `dotnet test` on push; add banned-constructs grep step (regex for the greppable items in §3.7). *Accept:* CI green on empty test suite; README with run commands.

**T0.2 — State infrastructure.** `WorldState`, table base pattern, typed Id structs (`SystemId`, `RegionId`, …), double-buffer clone, `IReadOnlyWorldState` views. *Accept:* clone round-trip equality test; mutation through read-only view does not compile (compile-time test via separate project or documented attempt).

**T0.3 — RNG.** PCG32 + stream registry + state-in-world serialization hooks. *Accept:* reference-vector test (known seed → known outputs); two streams with different names never correlate on first 1k draws; stream state survives snapshot round-trip.

**T0.4 — Clock + era table.** `SimClock`, JSON era-pacing loader (D-006) with schema validation and loud errors. *Accept:* full-campaign tick-through lands at ~1,630 turns and end date 2100 ± table tolerance; malformed JSON fails with actionable message.

**T0.5 — Turn executor + toy systems.** `ISimSystem`, `SimContext`, pipeline runner; toy `WeatherSystem` (writes rainfall from its RNG stream) and `GrowthSystem` (reads *Prev* rainfall, integrates a `long` biomass stock via remainder accumulator). *Accept:* one-turn-lag proven by test (growth at turn t uses rain t−1); dt-halving test — running 2× turns at dt/2 yields biomass within analytic tolerance of 1× at dt (documents integration error behavior).

**T0.6 — Ledger + conservation.** `Ledger.Transfer/Flow`, sources/sinks tables, toy `TradeSystem` shuffling goods between two regions via RNG amounts. *Accept:* FsCheck property — arbitrary generated sequences of transfers/flows conserve totals **exactly**; negative/overdraw policies covered.

**T0.7 — Snapshot, hash, order log.** §3.8–3.9 in full. *Accept:* save→load→continue equals uninterrupted run hash-for-hash at every subsequent turn.

**T0.8 — Determinism harness.** Twin-run test (same seed, 1,000 turns, per-turn hash equality); replay test (seed + order log reproduces hashes); both wired into CI as the permanent regression gate. *Accept:* both green; a deliberately injected `System.Random` in a scratch branch makes them fail (prove the harness has teeth).

**T0.9 — CLI + bench.** `sim run --seed 42 --turns 1630 [--report]`, `sim hash <save>`, `sim replay <seed> <orderlog>`, `sim bench` (per-phase ms, allocations). *Accept:* 1,630-turn toy world runs < 5 s; bench output per phase; README updated.

**M0 exit criteria:** T0.1–T0.9 accepted; CI enforces determinism + conservation on every push; ADR-001 (this kernel design) committed. Then and only then: M1 spec (worldgen, walking skeleton — needs D-009/D-010 closed).

---

## 5. GOVERNANCE

- **Kernel freeze:** after M0 acceptance, changes to `Sim.Core/Kernel` or the `ISimSystem` contract require a director-approved ADR.
- Agent-written unit tests never gate acceptance alone; the packet's stated acceptance tests do.
- Every packet ends with: tests green locally, CI green, one-paragraph ADR-style note if any contract was touched.

---

## 6. CLAUDE.md (place at repo root — the agent constitution)

```markdown
# Civ-Sim — Agent Rules
You are building a deterministic, turn-based civilization simulation. One director, agents build.

## Read first
docs/spine/* (Design Laws, Kernel Contract, Scale Charter), docs/m0-kernel-spec.md, latest ADRs.

## Non-negotiable laws (short form)
1. Conservation: people/money/goods change ONLY via Ledger.Transfer/Flow. Exact equality, no epsilon.
2. Mechanisms over modifiers: coefficients inside resolution equations OK; free-floating permanent buffs banned.
3. All rates are per-sim-year; integrate with dtYears. Never hardcode per-turn amounts.
4. No calendar gates; capability derives from computed state.
5. Determinism: banned — System.Random, DateTime.Now/UtcNow, float, AsParallel/unordered Parallel,
   Dictionary/HashSet iteration in sim logic, GetHashCode as logic, culture-sensitive parse/format.
   All randomness via RngRegistry streams. RNG state lives in WorldState.
6. Systems never reference each other — only State and Kernel. Communicate through tables/events.
7. Conserved stocks are long; rates/prices are double.

## Workflow
- One task packet per session; do not exceed packet scope.
- Acceptance tests in the packet are the definition of done; your own unit tests are additive.
- Touched a contract? Write an ADR (docs/adr/). Kernel is frozen post-M0 without director sign-off.
- Before commit: run the banned-constructs grep, dotnet test, sim bench if you touched hot paths.
```
