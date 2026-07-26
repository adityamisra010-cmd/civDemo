# Milestones

| M | Scope (one line) | Exit date | Exit commit | Tag |
|---|---|---|---|---|
| **M0 — Simulation kernel** | Turn executor, state tables, PCG32 RNG, integer-day clock + era table, Ledger with exact conservation, canonical snapshots/hash/replay, determinism harness (in-process + cross-process CI gates), headless CLI + bench. *No game.* | 2026-07-20 | `2702293` (+ closure docs) | [`m0-exit`](../../tags/m0-exit) |
| **M1 — Walking skeleton** | Continuous world, one settlement, labor-limited Malthus loop, playable + replayable UI, CI-published builds. | 2026-07-22 | `3b05832` (+ closure docs) | [`m1-exit`](../../tags/m1-exit) |
| **M2 — Population & Society** | Cohort demography on the ADR-011/ADR-012 kernel (exponential-survival micro-step integration, viability-gated migration), eleven-class system with two live classes (Peasants + emergent Artisans, D-020 DSL), twelve settlements with partitioned catchments, needs/grievance stocks (display-only), chronicle-lite with procedural names + annals, time-series graphs, autoplay + corridor-checked calibration battery. | 2026-07-25 | `ff4c5ac` (+ closure docs) | [`m2-exit`](../../tags/m2-exit) |

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

## M2 exit checklist (per `docs/m2-spec.md` §5)

Packets: **T2.1–T2.13 accepted** — the 12 spec packets plus T2.13, the
exit-gate defect packet; including the CR-001 constitution STOP and director
ruling mid-T2.8 (dt-fragile demography → the ADR-011 exponential-survival
micro-step kernel, delivered as T2.7b) and TWO exit-gate rework rounds found
by the director PLAYING the exit build — the gate did its job:

1. **Starvation magnetism + the resurrection cycle** (M2 exit HELD): an
   emptied food-less settlement's per-capita land made it the world's
   strongest migration magnet; famine flight funneled refugees INTO the
   famine (1,520 arrivals / 884 same-turn deaths in one turn), and
   extinction reset the deficit signal, re-arming the ruin as a colonist
   trap every ~9 turns. Fixed by ADR-012 destination viability (deficit
   gate + absolute food gate on every pairwise flow, D-021 Exit valve
   preserved); CollapseStabilityTests pins the regime with detectors
   verified to FAIL on the pre-fix code. The held-exit session's log +
   chronicle are preserved at `docs/orders-20260724-164734-held-exit.bin` /
   `docs/chronicle-20260724-164734-held-exit.txt` as the reproduction
   fixture.
2. **Ghost grievance** (display + state): an extinct settlement showed a
   lingering grievance stock. Fixed at both layers — NeedsGrievance zeroes
   the stock on extinction (grievance is held by people) and the HUD reads
   "—" for every per-capita-meaningless stat at population zero; pinned
   sim-side and UI-side.

- [x] **All packets accepted** — each merged to `main` by the director after
  its packet gate; T2.4 and T2.9+T2.10 additionally passed Director Visual
  Gates on the CI zip; T2.12's exit gate held once and passed on the fix
  evidence (director ruling, 2026-07-25).
- [x] **Calibration battery green across ≥20 seeds with proven teeth** — the
  4-seed CI battery (two-sided corridors.json bands, no-output-is-failure)
  green on every push; the 20-seed nightly sweep + jq corridor teeth green on
  `main` (scheduled 2026-07-24 06:04 UTC run and a manual dispatch on the
  merged main), metrics artifact uploaded.
- [x] **Director exit session played from the CI zip** — build
  `sim-ui-win-x64-3635ee7`: several settlements ruled differently (12 labor
  orders), multiple famines caused, annals read, graphs watched,
  names/selection confirmed. The session EXPOSED the two defects above; its
  log replays deterministically headless (the reproduction evidence for
  ADR-012) and the exit was accepted on the fix evidence against build
  `sim-ui-win-x64-349b2a7`.
- [x] **Harness green on the plural world** — the determinism suites run the
  N = 12 founded 1024² world at 300 turns ACROSS the era-pacing gate (T2.11),
  including the ordered save/load-continue leg; `determinism` +
  `determinism-xproc` + `calibration` all green on `main` at the exit merge.
- [x] **First-reign shape asserts standing** — the director-session fixture
  replays at `--settlements 1` with every shape assert intact (extinction in
  (5, 25], dead world frozen, no food mountain); golden at v9
  `c35a88a8…` (T2.13 dead-world grievance zeroing, history line recorded).
- [x] **Goldens pinned** — toy v11 `ff9519a1…`, founded 300-turn v10
  `a5959cdc…` (pinned absolutely in ci.yml xproc), first-reign v9
  `c35a88a8…`; every re-pin carries a dated history line.
- [x] **milestones.md M2 entry + `m2-exit` Release** — this entry; the tag is
  minted by the director publishing the `m2-exit` Release (git-proxy
  precedent from M0/M1), which auto-attaches the playable zip via
  `ui-artifact.yml`.

**Post-exit record (director ruling, CR-003, 2026-07-26):** M2's exit was declared against a
Malthus corridor now known to have been measuring an artifact of two compensating errors (yield
denomination and travel budget), corrected at T3.2b/CR-003. The mechanism is intact — the land
term still binds in 3.8 % of settlement-turns — but the condition that made it visible was false.
M2 does not reopen; the record states it.

Next: **M3**. Do NOT implement until `docs/m3-spec.md` exists on `main` and
its packets are cut.
