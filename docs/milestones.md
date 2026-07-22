# Milestones

| M | Scope (one line) | Exit date | Exit commit | Tag |
|---|---|---|---|---|
| **M0 — Simulation kernel** | Turn executor, state tables, PCG32 RNG, integer-day clock + era table, Ledger with exact conservation, canonical snapshots/hash/replay, determinism harness (in-process + cross-process CI gates), headless CLI + bench. *No game.* | 2026-07-20 | `2702293` (+ closure docs) | [`m0-exit`](../../tags/m0-exit) |
| **M1 — Walking skeleton** | Continuous world, one settlement, labor-limited Malthus loop, playable + replayable UI, CI-published builds. | 2026-07-22 | `3b05832` (+ closure docs) | [`m1-exit`](../../tags/m1-exit) |

## M1 exit checklist (per `docs/m1-walking-skeleton-spec.md` §5)

Packets: **T1.1–T1.10 accepted** (10 packets + 2 Director Visual Gate rework
rounds: T1.7 vector rivers, T1.8 Leontief farming / HUD text / extinction
ruling). Two mandatory adversarial workflow passes (T1.5, T1.9).

- [x] **All ten packets accepted** — each merged to `main` by the director
  after its packet gate; T1.7/T1.8/T1.10 additionally passed Director Visual
  Gates on the CI zip.
- [x] **Director played a session from the CI zip** — the T1.10 gate
  playthrough (build `sim-ui-win-x64-e36f2cc`) was ruled the M1 exit session;
  its order log is preserved at `docs/orders-20260722-153834.bin` (6 labor
  orders across ~100 turns).
- [x] **That session's order log replays hash-identical headless** — twin
  `sim replay --founded --seed 42 --orders docs/orders-20260722-153834.bin
  --turns 120` runs validate against the founded world and produce
  byte-identical per-turn hash logs (final hash `35d89a25c65e6e2a…`).
- [x] **Harness green on main** — `determinism` (8 legs incl. founded 1024²
  200-turn twin/ordered/replay/per-turn Population+Food conservation) and
  `determinism-xproc` (toy 400-turn + founded 200-turn cross-process runs,
  replay diffs, absolute founded-golden pin) both pass on `main`.
- [x] **Golden hashes pinned** — toy v6 `8f3a1986…`, founded no-order v1
  `a9ae0ba0…` (also pinned absolutely in `ci.yml` xproc), first-reign ordered
  v1 `6c32ed53…` (permanent director-session fixture
  `Sim.Tests/Fixtures/first-reign-orders.bin`).
- [x] **Worldgen < 5 s; turn time reported** — worldgen bounds enforced by
  `WorldgenPerfTests`; `sim bench --founded`: 35.9 ms for 200 founded turns
  (~0.18 ms/turn).
- [x] **ADR-008 (terrain content-hash) and ADR-009 (Sim.Ui stack) on main.**
- [x] **milestones.md M1 entry + `m1-exit` tag** — this entry; the tag is
  minted by the director publishing the `m1-exit` Release (the container's git
  proxy cannot push tags — M0 precedent), which also auto-attaches the
  playable zip via `ui-artifact.yml`.

Next: **M2**. Do NOT implement until `docs/m2-spec.md` exists on `main` and
its packets are cut.
