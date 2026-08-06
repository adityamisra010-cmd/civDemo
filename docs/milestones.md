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

---

## M3 — The economy arrives  *(at the exit gate)*

Settlements stop being identical food-machines and become places that make different things,
price them, and want more than bread. T3.1–T3.12 per `docs/m3-spec.md`.

### THE SECOND PRIZE — infrastructure became a differentiator (T3.2b, CR-003 ruling §4)

**One dirt path grows a settlement's hinterland by 16.6 % of its arable land.** Before the CR-002
recalibration, catchments were a ~205 km isochrone: every settlement already reached everything
worth reaching, so building a road changed nothing anyone could see. At a 50 km economic
hinterland, the road is what puts land inside the boundary. **D-009's
infrastructure-as-differentiator premise is live for the first time in the project** — this was
not a designed feature of T3.2b but a consequence of getting the denomination right, and it is
recorded here because it is the milestone's most durable structural gain.

### WHAT M3 DELIVERED

- [x] **Five-sector production over a real goods roster** (D-032, T3.3) — farming, herding,
  extraction, crafting, construction; recipes consume inputs, and the M2 scaffolding (artisan
  tool-multiplier, weighted construction labor) was DEMOLISHED rather than left beside it.
- [x] **The director can rule a settlement's production mix** (T3.9b) — five sector controls over
  `OrderKind.SectorAllocation`, turn-exact delivery pinned.
- [x] **A price solver that settles** (D-033, T3.4) on ADR-016 exact integration of its damped
  step, with per-term attribution; stable over the 500-turn soak, dt-correct, no global solve.
- [x] **Consumption as a class-weighted basket** over six goods with CES needs aggregation
  (D-035, T3.5) — Sustenance, Shelter, Comfort.
- [x] **Housing as a real stock** (T3.8) — dwellings built, maintained and decaying; Shelter is
  no longer a flow reading.
- [x] **Trade & arbitrage implemented** (D-034, T3.6) — the mechanism exists, is dt-correct and
  is proven to move goods when a gap clears its deadband. See the open items: on the canonical
  world it moves nothing, and that is not a defect in this system.
- [x] **Spatial and agronomic denomination corrected** (CR-002/T3.2b) — the yield constant and
  the catchment radius were two compensating errors; both fixed.
- [x] **Founding variation** (ADR-017, T3.6b) — settlements no longer emerge in lockstep;
  variance-floor pinned (CV ≥ 0.22) so a silent return fails the suite.
- [x] **The goldens finally see the goods economy** (T3.11) — a DRIVEN golden, specialised by
  sector orders, red-proven against two distinct price-step perturbations. It also measured that
  the founded golden had ALREADY closed the price half of that gap at T3.2b/T3.5b, and the
  original blocking premise was stale.
- [x] **Market, sector-control and trade UI** (T3.9a/T3.9b) — the trade panel is legible AT ZERO
  FLOW, per good, which is the state the director will actually meet.

### WHAT M3 DELIBERATELY DID NOT DELIVER

This list is unusually long, and every line is measured, named and owned. A milestone entry that
records only what worked is a worse instrument than one that records what is known-open.

- [ ] **THE WORLD CANNOT STARVE — B-2, unbounded stores.** Nothing bounds accumulation, so
  abundant goods ratchet upward forever. **Three costumes, all measured:** grain reserves of
  **~1,240 years**; **11 of 13 non-grain goods pegged at a price band edge** (0.05 floor or 20.0
  ceiling); and timber stores holding **Shelter at 1.0000 for decades** after a farm-100 % order
  (Hikiavur t177, timber store 755 ≈ 16 more turns of cover). A fourth and fifth prediction are
  filed against the same fix. *Owner: B-2, M4 blocking material (`docs/queue.md` Q-B —
  ONE TEST, FIVE PREDICTIONS).*
- [ ] **TRADE IS STRUCTURALLY ZERO on the canonical world.** Two independent causes, both
  escalated, neither this milestone's to fix:
  - **Escalation 1 — the deadband exceeds the maximum expressible price gap.** For bulk ≥ 8 at
    map distances the threshold is ≈ 23–35 while the largest gap the price band can express is
    `BandMax − BandMin = 19.95`. Ores and stone are **structurally untradeable overland at any
    price divergence**. And the water counterfactual says **the model is CORRECT** — bulk goods
    moved by water in the real world, so the finding is a missing transport mode, not a bad
    constant. *Owner: a future transport packet; every surface involved is ruled or frozen.*
  - **Escalation 2 — common band-edge pinning.** Both sides of every pair rest on the SAME band
    edge, so the gap is identically zero however much settlements differ. Suspected to be B-2
    wearing its price costume (Q-B). *Owner: B-2.*
  - A **counterexample worth carrying forward** (T3.11): `bronze` shows a spread of 15.17 against
    a 7.22 deadband — a gap well OVER threshold — and still moves nothing, because `maxStock = 0`.
    The deadband is not always the binding constraint, and a transport packet measured on volume
    alone would misread that either way.
- [ ] **COMFORT IS FLOW-BOUND (Q5).** Shelter got its stock at T3.8; Comfort did not. Pots and
  cloth are durable, yet zero crafting for one period zeroes Comfort (Hikiavur t177: pottery
  demand 59 eaten 0, cloth demand 88 eaten 0 → Comfort 0.0000 in both classes). It is therefore
  the residual grievance accruer on the otherwise-fixed tree. **Not simply Shelter again:** a
  dwelling degrades from lack of MAINTENANCE, a pot breaks from USE, so the honest model is a
  household-goods stock depleted by use — a different equilibrium, not a copy of housing's.
  *Owner: M4, and it depends on B-2 (an unbounded goods stock would saturate at 1.0 forever).*
- [ ] **T3.7 — MERCHANTS: MOVED TO M4 by director ruling.** Merchants emerge on trade volume,
  and trade volume does not exist. Implementing the class first would have produced a mechanism
  with nothing to feed it. *Owner: M4, gated behind the two trade escalations.*
- [ ] **T3.10 — CALIBRATION: MOVED TO M4 by director ruling.** *Owner: M4.*
- [ ] **The Malthus corridors remain quarantined** (CR-003) — the corrected constants leave a
  pre-Malthusian world because nothing fills the frontier. *Owner: colonization / land
  clearance, M4-targeted (CR-003 §5.2(a)).*

### EXIT CRITERIA — STATUS AT HANDBACK

- [x] **All packets accepted** — T3.1–T3.11 each merged to `main` on a director ruling; T3.9b and
  the art-substrate work additionally passed Director Visual Gates on the CI zip.
- [x] **Price solver soak stable over 500 turns** — `PriceSoakTests`, with the oscillation
  detector itself proven against a series known to oscillate before it was trusted.
- [x] **Harness green** — determinism suites on the M3 world, all four xproc legs byte-identical,
  and the built binary's founded run matching the in-test golden absolutely (T3.11).
- [x] **First-reign shape asserts standing** — extinction inside (5, 25], dead world frozen, no
  food mountain; golden at v22, history block repaired at T3.11 (it carried two spliced
  numbering series).
- [x] **Goldens pinned** — toy, founded 300-turn `b9f93d4a…` (pinned absolutely in ci.yml xproc),
  first-reign `144d7e5d…`, and the new DRIVEN golden `e7457fbc…`; every re-pin carries a dated
  history line.
- [x] **milestones.md M3 entry** — this entry.
- [ ] **Calibration battery green across ≥20 seeds** — CI battery and nightly sweep: see the
  T3.12 sweep counts at handback. NOTE the standing qualification: the density corridor is
  QUARANTINED and re-pinned (T3.8), and "including comparative advantage" cannot be met as
  written while trade is structurally zero.
- [ ] **Director exit session played from the CI zip, log replaying hash-identical** — the
  director's, not this packet's. Brief: `docs/m3-exit-session.md`.
- [ ] **`m3-exit` Release with attached zip** — minted by the director publishing the release.
- [ ] **Merged branch sweep** — 21 verified-safe remote branches listed at T3.11; this session's
  credential cannot delete remote refs (HTTP 403), so the deletions are the director's from the
  GitHub UI.

**M3 does not close on this handback.** The exit is the director's play session against the build
above, and the milestone closes on his ruling.
