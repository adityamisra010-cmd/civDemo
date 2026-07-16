# Civ-Sim — Agent Constitution

One deterministic, turn-based civilization simulation spanning 6,000 years. One human director; AI agents build it. You are one of those agents. Precision beats ambition: implement exactly the packet you are given.

## Read before any work
1. This file, fully.
2. `docs/m0-kernel-spec.md` — current milestone spec: kernel contract (§3) + task packets (§4).
3. When your task touches them: `docs/civ-sim-architecture-v3-outline.md` (Spine), `docs/spine-s8-governance-freeze.md` (rules), `docs/d009-d010-map-population-addendum.md`, `docs/d011-battle-layer-addendum.md`, `docs/d018-classes-and-needs.md`, latest `docs/adr/*`.

**Current milestone: M0.** Active packets: `docs/m0-kernel-spec.md` §4. (This line changes only at a milestone exit gate.)

## Non-negotiable laws (short form)
1. **Conservation:** people/money/goods change ONLY via `Ledger.Transfer`/`Ledger.Flow`. Conserved stocks are `long`. Exact equality in tests — no epsilon.
2. **Mechanisms over modifiers:** coefficients inside resolution equations are fine; free-floating permanent buffs are banned.
3. **dt-correctness:** every rate is per-sim-year; integrate with `dtYears`. Never hardcode per-turn amounts.
4. **No calendar gates:** capability derives from computed state, never from dates or era labels.
5. **Determinism — banned constructs:** `System.Random` · `DateTime.Now/UtcNow` in sim code · `float` · `AsParallel`/unordered `Parallel.*` · iterating `Dictionary`/`HashSet` in sim logic (use arrays or sort keys) · `GetHashCode()` as logic input · culture-sensitive parse/format (always `InvariantCulture`) · LINQ in hot paths. All randomness via `RngRegistry` streams; RNG state lives in `WorldState`.
6. **Isolation:** systems never reference each other — only `State` and `Kernel`. Communication is through tables and events.
7. **Types:** conserved stocks `long`; rates/prices/ratios `double`.

## Governance (architecture frozen post-M0)
- The Spine, kernel contract, closed D-decisions, and milestone order are FROZEN. You may not redesign them.
- If implementation reveals a genuine conflict between frozen items, STOP and write `docs/adr/cr-NNN.md`: (1) frozen items in conflict, (2) evidence — failing test/bench/derivation, (3) ≤3 minimal fix options, (4) blast radius, (5) recommendation. Await director ruling.
- "A better way exists" is NOT a conflict. Add one line to `docs/queue.md` and proceed as specified.
- Never write or modify specs beyond the current milestone + 1. Never implement ahead of the ratified spec.
- Tuning data files and `TUNE` parameters is always allowed.

## Workflow per session
- Execute ONE task packet. Do not exceed its scope, even to "help."
- Definition of done = the packet's stated acceptance criteria. Your own unit tests are additive, never a substitute.
- Touched a contract? Write an ADR (`docs/adr/adr-NNN-title.md`). The kernel is frozen after M0 acceptance without director sign-off.
- Before finishing: run the banned-constructs grep, `dotnet build`, `dotnet test`; if you touched hot paths, `sim bench`. Show the results, then a one-paragraph summary of what now exists.

## Repo map
```
Sim.Core/ (Kernel/ Systems/ State/) · Sim.Data/ · Sim.Cli/ · Sim.Tests/ · docs/ (specs, adr/, queue.md) · CLAUDE.md
```
