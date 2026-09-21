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
- [ ] **D-017's FARMLAND-CONSUMPTION HALF IS NOT IMPLEMENTED, and "D-017 CLOSED" overstates.**
  Found at T3.12 while verifying D-038 Part H's citations, and MEASURED against the tree.
  `m3-spec.md:15` closes D-017 with the size stock driving housing capacity, catchment radius
  bonus and "(render-side) footprint", deferring sprawl VISUALS to the visual milestone. But
  D-009:15 says footprints *"consume real farmland as they expand (the food-ceiling tension made
  visible)"* — and **consuming farmland is not render-side.** Measured: a settlement's growth
  **never reduces arable, its own or a neighbour's.** `EffectiveArableKm2` sums `BlockArableKm2`
  (`CatchmentSystem.cs:204`), a pure function of TERRAIN — immutable after worldgen, ADR-008 —
  and the lattice; no writer subtracts for settlement size or dwellings. `SizeTier` does the
  OPPOSITE, raising the travel budget so growth ADDS reachable nodes. A neighbour's arable can
  shrink only via catchment partition competition, which is reachability, not consumption. So the
  size stock, housing capacity and catchment bonus are closed; **the food-ceiling tension D-009
  wanted made visible is not built and is scheduled nowhere.** *Owner: unassigned — it belongs
  with colonization / land clearance (CR-003 §5.2(a)), which is the other half of the same
  land-pressure question. Not fixed at T3.12; the m3-spec entry is left as written and this is
  the record.*
- [x] **Calibration battery green across ≥20 seeds with proven teeth** — CI battery 6/6, nightly
  20-seed sweep green with BOTH deviating corridors quarantined-and-reported rather than
  silently gating (T3.12 item B). Density `[0.15, 0.6]` band vs measured 1.1428–1.6501, and
  migration floor 0.001 vs seed 9's 0.000980, are each declared in `corridors.json` with window,
  owner and history; the nightly prints their measured range every run.
- [~] **"…including comparative advantage" — WITHDRAWN, NOT FAILED** (director ruling, M3 exit,
  on the **T3.4b precedent**: the chronicle-famine criterion was withdrawn rather than failed
  because the world genuinely could not exercise it). **The difference matters and is stated
  explicitly: a FAILED criterion means the system did the wrong thing; a WITHDRAWN one means the
  criterion asked for evidence the world cannot produce, so no run of it could be informative.**
  Comparative advantage requires trade; trade is structurally zero for two measured, escalated
  reasons — the deadband exceeds the maximum expressible price gap (≈23–35 vs 19.95), and 11 of
  13 non-grain goods are pinned gap-zero on common band edges. Nothing was tuned to avoid the
  criterion and nothing regressed to breach it. **Travels to M4 with T3.10.**
- [ ] **Director exit session played from the CI zip, log replaying hash-identical** — the
  director's, not this packet's. Brief: `docs/m3-exit-session.md`.
- [ ] **`m3-exit` Release with attached zip** — minted by the director publishing the release.
- [ ] **Merged branch sweep** — 21 verified-safe remote branches listed at T3.11; this session's
  credential cannot delete remote refs (HTTP 403), so the deletions are the director's from the
  GitHub UI.

### THE PRE-EXIT SWEEP SAW ONE UNREPRODUCIBLE RED, AND THE DIRECTOR RULED THE EXIT PROCEEDS

Recorded so a future reader sees the judgement rather than a gap. An early T3.12 sweep run
reported `Sim.Ui.Tests` **150 passed / 1 failed**. That run's output had been piped through
`grep` at capture time, so the failing test's NAME and ASSERTION TEXT were discarded before
anything reached disk — three summary lines survived and **the observation is permanently
unrecoverable.** The most likely account is a `--no-build` race against that packet's
`BuildInfo` M2→M3 edit and its exact-match pin, but that is an INFERENCE and is recorded as one.

The authoritative run on the final tree is green: **`Sim.Ui.Tests` 151/151, `Sim.Tests` 440/444
(4 skipped)**, banned-constructs clean, build clean.

**Director's ruling (2026-08-06): the exit proceeds.** A LOST observation is not evidence of a
defect, and treating an unrecoverable log as a stop condition would mean any lost output halts a
milestone — not a rule worth having. The process fix is filed in `docs/queue.md`: a sweep whose
purpose is catching failures must capture full output to disk and filter at READ time.

### THE KNOWN-OPEN LIST, COMPLETED AT THE EXIT RULING

Each with its measurement and its owner. **A milestone entry recording only what worked is a
worse instrument than one recording what is known-open** — and for M3 the second list is the more
valuable one.

| # | known-open | measurement | owner |
| --- | --- | --- | --- |
| 1 | **The world cannot starve — B-2, FOUR costumes** | grain ~1,240 yr reserve; 11/13 goods pegged at band edges; timber holds Shelter at 1.0000 for decades after a farm-100 % order; **and now a fourth: the migration transmission channel measures ZERO starvation deaths in 20/20 seeds** | B-2, M4 blocking material (Q-B: one test, five predictions) |
| 2 | **Trade is structurally zero** | escalation 1: deadband ≈23–35 vs max expressible gap 19.95 — and the water counterfactual says the MODEL IS CORRECT, bulk moves by water; escalation 2: 11/13 goods gap-zero on common band edges | a future transport packet; escalation 2 → B-2 |
| 3 | **Comfort is flow-bound (Q5)** | Hikiavur t177: pottery demand 59 eaten 0, cloth demand 88 eaten 0 → Comfort 0.0000 both classes | M4; depends on B-2 |
| 4 | **Density corridor ~2.3× above its band** | 20 seeds, 1.1428–1.6501, mean 1.3952 vs band [0.15, 0.6]; **dated to T3.2b** by run history (last green nightly 2026-07-26, first red 2026-07-27) | M4 CR-002 packet (travelled with T3.10) |
| 5 | **World population varies 1.63× across seeds by an UNBISECTED mechanism** | 81,160–132,280 over 20 seeds; drives the density spread (corr with population **+0.858**, with arable **+0.018**); migration EXCLUDED by measurement | unassigned; belongs with M4 CR-002. **Named candidate: ADR-017's endowment jitter at 0.69 — not bisected** |
| 6 | **Artisans emerge with exactly 1 member in 10 of 12 settlements** | **OBSERVED, NOT DIAGNOSED** (director ruling) | M4 |
| 7 | **The Thiathiariath oscillation is UNREPRODUCED** | director's session: 13/24/24/10/10 % between consecutive decades. T3.12b's diagnosis runs measured a **1.96 % maximum** share swing (seeds 3/6/9, different seed and order log) — **so they do not explain it** | M4; now investigable, since T3.12a makes his own session log a dataset |
| 8 | **Kunaetho's late grievance rise** | **observed, not diagnosed**, by director ruling | M4 |
| 9 | **T3.7 (merchants) and T3.10 (calibration) MOVED TO M4** | T3.7 because merchants emerge on a trade volume that does not exist | M4 |

**M3 CLOSES on the director's exit ruling, 2026-08-06.** The exit is the director's play session against the build
above, and the milestone closes on his ruling.

---

## M4 — Neighbours, conflict, and a world that can run short  *(at the exit gate)*

M4's spec (`docs/m4-spec.md`) was the first written under S8 §4.1, and its own conformance to that
section was part of the deliverable. It conforms on all four requirements: the foundations audit ran
as packet one and its findings were promoted into T4.3's named prohibitions; the dimensional
declaration states its limit rather than papering over it; corridor independence carries its
standing self-referential example; and the coupling map was revised by T4.1 under the living clause,
which is the mechanism §4.1 specifies.

### WHAT M4 DELIVERED

The **Empire Control Foundation** — `PolityRow`, `ControlRow`, `CapitalRow`, `CommandSource` and
`EmpireQuery`. Founding now instantiates the world's one player-commanded Empire in the same
operation that creates its settlements, so `Found` never returns a playable world whose settlements
answer to nobody. An order's issuing strategic actor is the **existing** `PolityId` — a projection,
not a second identity, needing no new field and no serialization change. The **settlement
construction queue**: an ordered queue whose head is either built whole this turn or waits
unchanged, competing for the existing construction sector against housing's published draw. Schema
moved v22 → v23 → **v24** across these.

Beyond the Empire spine: **store bounding** (T4.2), whose pre-registered prediction that stores
would sit near 1.5 years of demand was met to four decimals; **colonization** (T4.4); **transport and
the river-aware lattice** (T4.7); **migration** rework (T4.10, T4.12); **comfort as a stock**
(T4.13); and the **clone architecture** measurement (T4.16).

Four post-certification packets sit on the exit candidate rather than in the certified baseline:
**T4.17** session records and `sim inspect`; **T4.18** the founding population transient; **T4.19**
the Glass Box, which ruled CR-013 and CR-014, corrected the founding demographic vector and retired
the Artisan timing window in favour of structural latch tests; and **T4.20** food legibility, which
was ruled observability-only after its audit established no defect.

A fifth post-certification packet, **T4.21** — famine semantics, bounded migration and shock
integration — sits in the same place (spec `docs/t4.21-architecture.md`; governance
`docs/adr/cr-015-famine-is-exceptional.md`, ruled from the director's 2026-09-17 mandate, with
ADR-024/025/026; G1, the harvest-weather decade variance, is escalated and open). It is IN PROGRESS
on `claude/civdemo-work-b1z2y4` and is not part of the certified baseline or of this entry's
measurements; "No food-supported population cap" below is superseded by CR-015 N3 for T4.21 (the
feedable food-influx limit is a fertility multiplier, not a serialized cap).

### WHAT M4 DELIBERATELY DID NOT DELIVER

**No AutoResolver and no armies.** D-011 §6 resequences the battle layer to M6, and under GOV-4 §1 a
later decision outranks the Spine's earlier "Conflict v1 at M4". Recorded as a certified exclusion,
not an omission.

**No money, no treasury, no taxation.** **No research, technology or institutions** — CR-005 places
them in M5 and remains open without blocking M4. **No food trade**: food is excluded by construction
today, and T4.20 declined to change that. **No food-supported population cap** — the measurement
found every shortfall to be a production shock rather than growth overshoot, so a cap would have had
nothing to correct.

**No second food-variety channel.** ADR-023 rules food variety to be represented exclusively by
D-035-A; a proposed additive dietary-diversity happiness bonus was rejected as double counting of
the same signal, measured over the same good set from the same field.

### EXIT CRITERIA — STATUS AT HANDBACK

| § 6 criterion | status |
| --- | --- |
| All packets accepted, each merged on a director ruling | **MET** for T4.1–T4.16; the four post-certification packets await the director's merge |
| Scarcity can bite | **MET** — starvation is reachable on the dev world; CR-003 records the corridor disposition and quarantines the two seeds |
| Determinism suites green; xproc; first-reign shape | **MET** — measured on the candidate, all green |
| Goldens pinned with dated history; driven golden extended | **MET** — every M4 movement carries control-arm attribution |
| Calibration battery green across ≥20 seeds, quarantined corridors reported rather than silently gating | **MET at the exit gate** — density's quarantine is re-activated over its measured envelope with teeth in both directions, so the corridor is reported with its measured range rather than gating. The band was NOT re-tuned. See known-open item 1 |
| The nightly has been green, and someone has read it | **INSTRUMENT MET, READING IS THE DIRECTOR'S** — dry-run against the real gate with the six observations now prints QUARANTINED with the measured range and exits 0, where before it breached and exited 1. Someone still has to read it: see known-open item 11 |
| Director exit session from the CI zip, replaying hash-identical, with a T3.12a replay report | **AWAITING THE DIRECTOR** — the machinery is complete and tested |
| `milestones.md` M4 entry with its known-open list; `m4-exit` Release | this entry closes the first half; **the tag is the director's**, at the merge |

### THE KNOWN-OPEN LIST

| # | item | disposition |
| --- | --- | --- |
| 1 | **Density out of band on 6/20 seeds** (max 0.74211 against a 0.60 ceiling), quarantine inactive | The director ruled the measurement held for the in-process battery (T4.19 record §9). That ruling did not consider the **nightly**, which reads the same corridor and would breach. **Needs a ruling**, not a re-band — CR-002 and CR-003 both forbid fitting the instrument to the artifact |
| 2 | **CR-003 Malthus corridor** | Quarantined by standing ruling; two dev seeds red by design |
| 3 | **Migration below the historical corridor** | Accepted as measured; quarantine active, so it reports rather than gates |
| 4a | **`FamineAtOneOfTwelve_ExitCrossesTheFractionBeforeDeathDoes`** (`MigrationTests.cs:378`) | MEASURED at the exit gate: still red when un-skipped, but on a **different assertion than its Skip reason blames**. The gross-exit side crosses; the **starvation control** never does — an ordered total-harvest famine at one of twelve no longer kills 8 % of the settlement within 40 turns. The blocking observable is famine lethality under T4.2 store bounding, not migration, so the recorded owner "M4 migration" is **wrong** and T4.10/T4.12 could not have discharged it. **OPEN, needs reassignment; currently unowned.** Not named in `m4-spec` §6 → **OWNED BY T4.21-4 (CR-015 N9, 2026-09-17): re-aimed as famine lethality under the four-state ladder, `S_Abandonment_TriggersFamine`; the skip lifts with a re-derived assertion, never a moved threshold** |
| 4b | **`MagnitudeCorridor_FedPhaseDrift_WithTeeth`** (`MigrationTests.cs:532`) | MEASURED at the exit gate: still red when un-skipped. The corridor band itself passes; the **rate-lever tooth** fails at exactly the recorded ×1.07 (0.69 % → 0.74 %/decade against a ≥1.5× requirement), confirming T4.1g's diagnosis live — the base rate cancels out of the gap-closing expression, so the assertion's premise is structurally false. T4.12 closed OUTCOME C and **consciously declined** the re-derivation. Governed by the `migrationGrossPerDecade` quarantine ruling, which accepts current migration behaviour and states §6's calibration criterion is explicitly **not** discharged for migration. **OPEN, needs an owner to re-aim the teeth onto the gap-driven observable.** Does not block the candidate → **OWNED BY T4.21-4 (CR-015 N9, 2026-09-17): the teeth are re-aimed onto the gap-driven observable when T4.21-2's bounded flight lands** |
| 5 | **ADR-017 reads "director certification pending"** while the spec cites its ruling as settled and ADR-018 amends the same decision | Ambiguous; needs a ruling |
| 6 | **ADR-020 clone architecture** awaiting a director ruling | Packet complete; blocks nothing |
| 7 | **T4.19-E structural test set S1–S5** flagged for the director's confirmation | The director's own list never arrived; the set is the implementer's reading |
| 8 | **Grain is storage-bounded; livestock and fish are not** | The repository records no intent either way (T4.20). Needs a ruling before it is either unified or declared deliberate |
| 9 | **ADR-019 exists only on an unmerged branch** — the ADR sequence on the candidate jumps 018 → 020 | Housekeeping |
| 10 | **`docs/current-state.md` is stale in four load-bearing claims** | Housekeeping; deliberately not repaired during the closure pass |

**M4 does not close on this entry.** It closes on the director's play session against the candidate
and his merge ruling, as M3 did.


### RECONCILIATION OF THE KNOWN-OPEN LIST (M4 exit-process packet)

Every item above carries exactly one disposition. Nothing was deleted.

| # | disposition | basis |
| --- | --- | --- |
| 1 Density out of band / nightly breach | **CLOSED / VERIFIED** | Process gap, not a density defect. The T3.12 quarantine mechanism already expressed the missing state; it was re-activated over the measured envelope with an attributed cause and a stated lift condition. Dry-run against the real gate: QUARANTINED with measured range, **exit 0**; control against the pre-change corridor on the same numbers: breach, **exit 1**. Band never moved |
| 2 CR-003 Malthus corridor | **OPEN / FUTURE MILESTONE** | Quarantined by standing ruling; two dev seeds red by design. The crash model is not M4 work |
| 3 Migration below the historical corridor | **CLOSED / VERIFIED** | Accepted as measured by director ruling (2026-09-04) with 20-seed evidence on record; the corridor is retained as a record, not an acceptance gate |
| 4a `FamineAtOneOfTwelve…` | **OPEN / REQUIRES DIRECTOR RULING** | Measured still red. Its recorded owner is wrong — the blocking observable is famine lethality, not migration. Needs reassignment; not named in `m4-spec` §6. **OWNED BY T4.21-4 (CR-015 N9)** |
| 4b `MagnitudeCorridor_FedPhaseDrift…` | **OPEN / REQUIRES DIRECTOR RULING** | Measured still red at exactly its recorded ×1.07. The premise is structurally false; the re-derivation was consciously declined. Needs an owner to re-aim the teeth. **OWNED BY T4.21-4 (CR-015 N9)** |
| 5 ADR-017 "certification pending" vs the spec citing it settled | **OPEN / REQUIRES DIRECTOR RULING** | A status contradiction only the director can resolve |
| 6 ADR-020 clone architecture | **OPEN / REQUIRES DIRECTOR RULING** | Packet complete, awaiting a ruling; blocks nothing |
| 7 T4.19-E structural set S1–S5 | **OPEN / REQUIRES DIRECTOR RULING** | Flagged for confirmation; the director's own list never arrived, so the set is the implementer's reading |
| 8 Grain bounded, livestock and fish not | **OPEN / REQUIRES DIRECTOR RULING** | The repository records no intent either way. ADR-023 settles *variety*; it does not settle the *storage* asymmetry |
| 9 ADR-019 only on an unmerged branch | **OPEN / REQUIRES DIRECTOR RULING** | Merging a branch is the director's call under `CLAUDE.md`; the ADR sequence on the candidate jumps 018 → 020 until then |
| 10 `docs/current-state.md` stale | **CLOSED / DOCUMENTATION ONLY** | Four load-bearing claims corrected inline and marked; nothing else rewritten |
| 11 The nightly gate never consults the window *(new, found by this packet)* | **OPEN / REQUIRES DIRECTOR RULING** | `gated()` short-circuits on `quarantine.active` alone, so a quarantined corridor drifting far outside its recorded envelope is caught only by the in-process battery's two seeds, never by the 20-seed nightly. Pre-existing T3.12 behaviour, deliberately not changed here. This is also the unclosed half of the M3 process defect: the instrument reports, but no mechanism makes anyone read it |

**Nothing in this list blocks the candidate.** Items 2, 3 and 10 are disposed; item 1 is closed by
process; the remainder are rulings the director takes at, or after, the playtest.

---

### M4 FINAL CLOSURE AUDIT (2026-09-21) — DISPOSITIONS AGAINST THE CANDIDATE

**APPEND-ONLY. Nothing above this line was edited or deleted.** The entry above is the record
as it stood at the exit gate; this block is the record of what the tree says now. Where the two
disagree, this block is the later measurement and the entry above is the history. Every row was
MEASURED on `claude/civdemo-work-b1z2y4` at **`52d0e1b`** unless stated.

**The candidate has moved since the entry above was written.** `main` is `dbef61a` (v24); the
candidate is 161+ commits ahead and is schema **v25** (`CanonicalSchema.cs` — T4.21-1's
`Disasters` table). Suite on the candidate: **Sim.Tests 885 passed / 0 failed / 4 skipped**,
**Sim.Ui.Tests 296 / 0**; all three gate scripts PASS; Release build 0 warnings / 0 errors.

| claim above | disposition, 2026-09-21 |
| --- | --- |
| `:302` "Schema moved v22 → v23 → **v24**" | ACCURATE as the history of M4-A…M4-D and as `main`. The CANDIDATE is **v25**. |
| `:305` store bounding "met to four decimals" | OVERSTATED. `docs/t4.2-review-record.md` measures **1.275–1.320 years** against a 1.5-year prediction and calls it CORROBORATED with one refinement — stores settle at ≈88 % of capacity, not at it. Agreement to ~12 %, not to four decimals. |
| `:319` T4.21 "is IN PROGRESS" | **COMPLETE.** T4.21-0…-8 all landed, plus the director's report and two closure audits. |
| `:317` T4.21's governance list | INCOMPLETE: it names CR-015 and G1 but not **CR-016**, which is OPEN and is the reason the famine mechanism ships INERT at `hazardPerYear = 0.0`. |
| `:331` "**No food trade**" | OVERBROAD. Only **grain** is excluded, as the numeraire — *"structural, not incidental"* (`TradeArbitrageSystem.cs:133-137`). Livestock and fish are food goods and DO trade. |
| exit row 1 "the **four** post-certification packets" | **FIVE**: T4.17, T4.18, T4.19, T4.20, T4.21 — which `:316` already says. |
| exit row 2 "Scarcity can bite — starvation is reachable on the **dev** world" | The ratified criterion (`m4-spec.md:377`) names the **canonical** world under a stated condition, not the dev world. **MET, and re-measured on the canonical world at the SHIPPED config**: seed 42, settlement 0 ordered to 0 % farm at turn 1 → FAMINE/Abandonment, **139 starvation deaths at turn 4**, 159 across 40 turns, ledger reconciling exactly (discrepancy 0) at every turn. The orderless twin never reaches FAMINE there. |
| exit row 5 "quarantined corridors reported with measured ranges" | **NOT SATISFIED ON THIS TREE, and now visible.** The band is untouched at [0.15, 0.6] and the quarantine is active, but the recorded WINDOW no longer covers the candidate — see B5 below. |
| exit row 6 "prints QUARANTINED with the measured range and exits 0" | ACCURATE as to the exit code, but the range printed was the STALE window, and nothing compared the two. Repaired — see B5. |
| exit rows 3, 4, 7, 8 | ACCURATE and unchanged. The `m4-exit` tag still does not exist (`git tag` → `m3-exit` only). |
| known-open 1 "quarantine **inactive** … the nightly **would breach**" | Both facts are stale: the quarantine is `active: true`, and the nightly did NOT breach — `gated()` short-circuited on `quarantine.active` and exited 0. That is the blind spot, not a pass. |
| known-open 2 "two dev seeds **red by design**" | **GREEN.** T4.21-4's arming turned them red; T4.21-7's disarm resolved them and re-instated the quarantine, which now asserts the ABSENCE of starvation. The suite's red set is empty. |
| known-open 4a / 4b, and their reconciliation rows | **DISCHARGED by T4.21-4.** Neither test carries a `Skip` attribute any longer; both are live and green in the 885. 4b's line number was also wrong — the test is at `MigrationTests.cs:666`, not `:532`. One narrower residue survives: 4b's upward rate-lever tooth is quarantined in place pending CR-016. |
| known-open 10 "`current-state.md` … deliberately not repaired" | Superseded: `current-state.md` now opens with a dated STALENESS CORRECTION block covering those four claims. Six FURTHER stale claims in that file are recorded in the audit and remain unrepaired. |
| known-open 3, 5, 6, 7, 8, 9 | ACCURATE and unchanged. |
| reconciliation closing line "**Nothing in this list blocks the candidate.**" | Stale as written. B5 below does bear on a ratified §6 criterion, and CR-016 is OPEN. Neither is a simulation defect. |

**B5 — THE DENSITY QUARANTINE'S WINDOW, AND THE INSTRUMENT THAT COULD NOT SEE IT.**
`canonical.densityPerArableKm2` at seed 3 measures **0.35415668759623087** against a recorded
window floor of **0.3685744951368359** — **3.9118 % below**. Attributed: T4.21 moved the
population (133,750 → 128,518, the same −3.9117757 % to every digit) with arable bit-identical.
No shipped instrument could see it: the nightly's `gated()` never read `quarantine.window`, and
the battery's canonical theory runs seeds 1 and 2, both comfortably inside.

The instrument is repaired (`Sim.Core/Kernel/CorridorStatus.cs`, `sim corridors`, 11 tests,
5 mutants killed; **ADR-027**). Run against the same 20-seed sweep, the old gate exits 0 in
silence and the new one reports **two** window breaches — density at 3.9118 % and **migration at
74.1210 %**, the second of which no instrument had ever shown.

**AND THE FULLER PICTURE, WHICH THE FIRST DRAFT OF THIS BLOCK DID NOT STATE.** An adversarial
review of this audit refuted the framing of B5 as purely a window question, and it was right.
Measured per seed on the candidate:

| corridor | inside the TARGET band | inside the recorded window |
| --- | --- | --- |
| `densityPerArableKm2` | **17/20** — seeds 2 (0.71647), 13 (0.62273), 1 (0.60732) above the 0.6 ceiling | **19/20** — seed 3 below the floor |
| `migrationGrossPerDecade` | **0/20** | **0/20** |

The band is the TARGET and the window is the RECORDED DEVIATION; they are different objects, and
reporting only one of them is how the previous blind spot was made. `sim corridors` now prints
both. Nothing is repaired: per T3.12 a quarantined corridor REPORTS and does not gate, and
**a corridor's disposition is the director's**. CR-002 and CR-003 both forbid fitting the
instrument to the artifact, and the quarantine's own `liftCondition` reserves re-derivation to an
explicit director ruling.

**WHAT REMAINS, AND WHOSE IT IS.** The exit session (`m4-spec.md:384`) names an act with a named
actor, and `CLAUDE.md:34` forbids an agent's own tests substituting for a stated acceptance
criterion. Its measurement half is done and attached (`docs/m4-exit-session.md`, written here as
T4.15's missing session brief); the play-and-judge half, the merge ruling, the `m4-exit` tag, and
the B5 / CR-016 / G1 rulings are the director's. `docs/gov-4-repository-freshness.md:89-91`
forbids an agent merging branches as ordinary implementation work, so no merge was performed.

**M4 still does not close on this entry.** It closes where the entry above says it closes.
