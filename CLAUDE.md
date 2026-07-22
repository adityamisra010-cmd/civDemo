# Civ-Sim — Agent Constitution

One deterministic, turn-based civilization simulation spanning 6,000 years. One human director; AI agents build it. You are one of those agents. Precision beats ambition: implement exactly the packet you are given.

## Read before any work
1. This file, fully.
2. `docs/m0-kernel-spec.md` — current milestone spec: kernel contract (§3) + task packets (§4).
3. When your task touches them: `docs/civ-sim-architecture-v3-outline.md` (Spine), `docs/spine-s8-governance-freeze.md` (rules), `docs/d009-d010-map-population-addendum.md`, `docs/d011-battle-layer-addendum.md`, `docs/d018-classes-and-needs.md`, latest `docs/adr/*`.

**Current milestone: M1 COMPLETE. Next: M2 — do NOT implement until `docs/m2-spec.md` exists on main and its packets are cut.** (This line changes only at a milestone exit gate.)

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
- Any ordering or argmax over double-valued scores uses a composite key with a stable integer tie-break (score, id) — and ships a tie-dense test proving it.
- Every new serialized row type ships a POPULATED-table test: exact ExpectedLength, bit-exact round-trip, hash equality. Empty-table coverage proves nothing (T1.1/T1.3 precedent).
- Replay equality proves reproducibility, not semantics. Every order-delivery semantic (when an order applies relative to when it was issued) gets its own turn-exact pin — live-vs-replay comparison alone cannot see stamping drift (T1.9 precedent).

## Environment (remote sessions)
- Containers are ephemeral: the .NET SDK does NOT survive between sessions. Run `./scripts/bootstrap.sh`
  before any dotnet work — it installs the .NET 10 SDK from the Ubuntu archive (the direct Microsoft
  download hosts are blocked by the session proxy) and no-ops if a 10.x SDK is present.
- Branch convention: one branch per task packet (`t0.N-<slug>`), cut from `main`. `main` is accepted
  truth — the director merges a packet branch to `main` on acceptance; agents never push to `main`.
  CI on `main` is the director's between-session check.

## Repo map
```
Sim.Core/ (Kernel/ Systems/ State/) · Sim.Data/ · Sim.Cli/ · Sim.Tests/ · docs/ (specs, adr/, queue.md) · CLAUDE.md
```
