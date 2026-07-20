# Milestones

| M | Scope (one line) | Exit date | Exit commit | Tag |
|---|---|---|---|---|
| **M0 — Simulation kernel** | Turn executor, state tables, PCG32 RNG, integer-day clock + era table, Ledger with exact conservation, canonical snapshots/hash/replay, determinism harness (in-process + cross-process CI gates), headless CLI + bench. *No game.* | 2026-07-20 | `2702293` (+ closure docs) | [`m0-exit`](../../tags/m0-exit) |

Next: **M1 — walking skeleton** (raster terrain, one settlement, dirt path,
pop/food loop, end-turn button; per D-009 redefinition). Spec must exist as
`docs/m1-walking-skeleton-spec.md` on main with packets cut before any
implementation begins.
