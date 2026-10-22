# Research balance analysis — current vs proposed

**Status: MEASURED baseline (runs on seeds 1, 2, 42, 7) and ESTIMATES (static model, `tools/balance.py`,
`tools/cp_gate.py`).** No figure here is a simulation result for the PROPOSED graph: the proposal is not implemented
and no simulation of it, or of any Civics Points economy, exists. Costs are never changed to hit an Age duration
(D-046 §1; ledger §26 item 19); this document analyses pacing, it does not target it. The RP formula is unchanged
(assignment constraint); the pacing question is put to the Director separately (docket KD-10).

**Label convention used throughout (2026-10-22):**
- **MEASURED** — read from the current content (`Sim.Data/content`) or from the baseline runs in `measurements/`.
- **PROPOSED** — computed from the proposal data (`technology-tree-proposal.json`, `civics-tree-proposal.json`):
  node counts, costs, closures and depths of the proposed graph. These are design data, not measurements.
- **ESTIMATE (static model)** — every turn figure produced by `tools/balance.py` or `tools/cp_gate.py`, for the current
  graph and the proposed graph alike: an optimistic lower bound, never a gameplay result.

Data: `data/balance-current.json`, `data/balance-proposed.json` (regenerate with
`python3 -I tools/balance.py --graph current|proposed --json …`), `data/balance-cp-gate.json` (`python3 -I
tools/cp_gate.py`); measured runs in `measurements/`.

## 1. Four different quantities

| Quantity | What it measures | Current (MEASURED) | Proposed (PROPOSED) |
|---|---|---|---|
| Node count | clicks / decisions | 426 technology + 7 civics = 433 | 494 technology + 67 civics = 561 active (2 retired by merge) |
| Total cost | workload | 11,192,730 RP technology (+18,130 civics) | 13,158,910 RP technology (+17.6 %); civics 335,110 points |
| Dependency depth | serial chain length (≥ 1 turn per node) | A9 CORE depth 19 | 18 |
| Turns to capability | when a capability can first exist | see §3 — ESTIMATE (static model) | see §3 — ESTIMATE (static model) |

## 2. Cost and count by Age (technology; MEASURED for current, PROPOSED for new)

| Age | nodes now (MEASURED) → proposed (PROPOSED) | cost now (RP, MEASURED) | cost proposed (RP, PROPOSED) | change | main reasons |
|---|---|---|---|---|---|
| A1 | 28 → 28 | 11,080 | 11,190 | +1.0 % | microlith rewire |
| A2 | 51 → 52 | 60,360 | 47,710 | −21.0 % | calendar institutional outliers (3.0 → Age prior), cereal material 2.0 → 0 (KD-09); cotton added |
| A3 | 36 → 41 | 79,660 | 87,080 | +9.3 % | cupellation, ore prospecting, traction/siege and craft relocations |
| A4 | 29 → 27 | 86,350 | 83,870 | −2.9 % | geometry re-dated to A3; iron gains its furnace prerequisite |
| A5 | 32 → 35 | 157,140 | 158,140 | +0.6 % | counting rods, trebuchet |
| A6 | 41 → 42 | 191,080 | 185,020 | −3.2 % | craft relocations, concrete arch edge removed |
| A7 | 31 → 37 | 285,590 | 307,930 | +7.8 % | physiology, rifling, mineralogy, optical telegraph, ocean junk |
| A8 | 53 → 78 | 1,407,020 | 1,831,250 | +30.2 % | the empty military A8 filled; stratigraphy; thermodynamics, periodic system; AC transmission; radio split |
| A9 | 125 → 154 | 8,914,450 | 10,446,720 | +17.2 % | nuclear/geology case study (+1.6M, optional cuts ≈0.43M available, KD-20), immunology/endocrinology, splits |

Single-node bottleneck (MEASURED → PROPOSED): the most expensive node falls from 756,960 RP (`rocket`) to 378,470 RP
after the rocket and satellite splits; nodes above 100k RP rise from 24 to 29 (mostly new A9 nuclear and science
nodes, all optional).

## 3. Critical paths and turns to capability (ESTIMATE (static model); seed 42 unless stated)

Model: turns until the cumulative RP of a trajectory covers the cheapest closure of the milestone (only that closure
researched, no Eureka, no overflow) — an optimistic lower bound, applied to the current graph and to the proposed graph
alike. Trajectories: **weak** = half the population of a non-expanding polity (driver RP × 0.5^0.699, itself an
ESTIMATE); **average** = the MEASURED cheapest-first driver trajectory (a polity that never founds); **strong** = the
MEASURED AI empire's own population (`sim run --ai-empires 1`, RP reconstructed; it starts with half the settlements,
so it is slower early and much faster late). The trajectories are MEASURED; every turn in the table is an ESTIMATE.

| Milestone | closure now (MEASURED nodes / RP) | closure proposed (PROPOSED) | depth now→proposed | weak, turns now→proposed (ESTIMATE) | average (ESTIMATE) | strong (ESTIMATE) | average, seeds 1/2/42/7, proposed (ESTIMATE) |
|---|---|---|---|---|---|---|---|
| research stage | 18 / 28,660 | 18 / 28,660 | 8→8 | 447→447 | 343→343 | 415→415 | 335/337/343/341 |
| A2 CORE | 2 / 3,890 | 2 / 1,250 | 2→2 | 138→56 | 96→37 | 133→55 | 35/36/37/37 |
| A3 CORE | 9 / 15,250 | 9 / 15,250 | 6→6 | 317→317 | 242→242 | 297→297 | 236/237/242/240 |
| A4 CORE | 10 / 14,320 | 12 / 24,990 | 5→5 | 306→415 | 233→318 | 287→386 | 310/312/318/316 |
| A5 CORE | 21 / 36,040 | 27 / 48,170 | 9→8 | 503→580 | 389→455 | 466→537 | 444/446/455/452 |
| A6 CORE | 28 / 59,470 | 21 / 38,240 | 8→6 | 640→518 | 507→402 | 592→480 | 393/395/402/400 |
| A7 CORE | 22 / 98,940 | 24 / 96,890 | 10→10 | 807→800 | 647→641 | 741→734 | 628/631/641/638 |
| A8 CORE | 58 / 389,980 | 57 / 386,450 | 16→15 | 1533→1526 | 1204→1199 | 1035→1033 | 1176/1180/1199/1193 |
| A9 CORE | 73 / 800,940 | 76 / 772,890 | 19→18 | — | — | 1180→1172 | not within 1,630 turns |

Readings:
- **Ordering is repaired (PROPOSED graph; ESTIMATE turns).** Iron (A4) now costs more to reach than bronze (A3), as it
  should (D02: iron needed furnace knowledge); A6 no longer requires the university stage (KD-04), so its closure is
  cheaper than A5's.
- **The CORE critical paths barely move** (ESTIMATE: A7–A9 within 1 %): the redesign adds breadth, not mandatory length.
- **Measured reality is far slower than the lower bound.** MEASURED: the cheapest-first driver reaches the stage at
  turns 996–1016 (ESTIMATE bound: ≈340); it explores cheap side nodes. The AI empire reaches the stage at 702–716 and
  completes 263–344 nodes by turn 1630 (`measurements/baseline-progression.md`; its AI completions are INFERENCE from
  order-log reconstruction).
- **Seed sensitivity is small** (MEASURED: ≤ 28 turns between seeds): the four canonical seeds differ in pace, not path.

## 4. Sensitivity to research generation

Totals generated over 1,630 turns: average (driver) 0.71–0.74M RP and strong (AI empire) 1.43–3.14M RP come from
MEASURED trajectories (the AI's RP is reconstructed from its MEASURED population); weak 0.44–0.46M is an ESTIMATE.
Against the PROPOSED finite workload (≈13.0M technology + 0.34M civics):

| Scenario | share of proposed workload researchable (ESTIMATE) | reaches A8 CORE (ESTIMATE, lower bound) | reaches A9 CORE (ESTIMATE) |
|---|---|---|---|
| weak | ≈ 3.4 % | turn ≈1526 | no |
| average | ≈ 5.5 % | ≈1199 | no |
| strong | 11–24 % | ≈1033 | ≈1172 (seed 42) |

**Risk of tedium:** the measured driver's longest single-target waits are dominated by A6–A8 nodes costing 10k–40k RP
at ≈400–800 RP/turn (MEASURED, `measurements/baseline-progression.md` T8). Splitting the largest late nodes (rocket,
satellite, genome sequencing, radio, computer, transistor, fission) shortens the worst waits without lowering the
workload (PROPOSED). **Risk of a cheap endgame:** none under measured demography — the endgame is unreachable
(ESTIMATE). If the Director raises RP (KD-10), the A9 tree must still not complete within a few dozen turns; the
PROPOSED A9 workload (10.4M RP) protects against that.

## 5. Overflow interaction (MEASURED baseline, ESTIMATE for the proposal)

Overflow is lost by design: 46.6k–49.7k RP per game, 6.5–6.7 % of all RP generated (MEASURED). Loss per completion
averages roughly half a turn of RP (MEASURED); the proposal adds ≈ 128 nodes, mostly late and expensive, so the
expected extra loss is small for the driver (it never reaches them) but rises for strong polities (ESTIMATE,
qualitative). Finer granularity raises the stakes of KD-11 (carry vs lost).

## 6. Civics Points and the cross-pool gate — summary (full analysis: `cp-economy-proposal.md`)

This section replaces the earlier ratio stand-in (CP per turn = r × RP per turn). That stand-in modelled CP as a
fraction of RP, which the Director's intent of 2026-10-22 excludes; its numbers remain in `data/balance-*.json`
(`cpCoupling`) and in the D12 meta (`sensitivityEstimate.supersededRatioStandIn`) for traceability only and are not
used. CP is now modelled from a **candidate population-led equation with its own exponent and PROPOSED placeholder
coefficients** (`cp-economy-proposal.md` §2), never from RP.

- **MEASURED one-pool baseline.** `legal_code_roman` completes 66–67 turns before the research stage for the AI polity
  (635–649 vs 702–716) and 325–331 turns before it for the cheapest-first driver (671–685 vs 996–1016);
  `geometry_axiomatic` binds the stage in every run. University eligibility equals the `legal_code_roman` turn; A5
  CORE is bound by `coinage_electrum`. The tax edict's Age III half binds it (AI A3 entry 412–425 vs taxation
  328–339).
- **ESTIMATE (static model; `tools/cp_gate.py` → `data/balance-cp-gate.json`; 1,280 cases: seeds 1/2/42/7 × AI and
  driver × per-sim-year or per-turn reading × exponent 0.5–0.8 × placeholder scale 0.1–2 × civic detour 0–30,000 CP).**
  Option A (keep the civic in the stage) shows no stage delay against the one-pool twin for placeholder scale ≥ 0.5
  (per-turn reading) or ≥ 1 (per-sim-year reading) with up to a 10,000-CP detour; a slow CP economy combined with a
  detour delays the stage by 9–581 turns or, in 17 of 160 grid cells, never completes the gate civic within 1,630
  turns. Option B (stage without the civic) is 16–17 turns earlier than the twin in every case but leaves university
  eligibility exactly as CP-paced as Option A. Option C (an OR route through civil-service examinations) gains nothing
  in any case. A per-sim-year CP economy yields 20× less CP per turn at dt = 0.5 than at dt = 10 for the same
  population — the main evidence for D-061.2.
- **Recommendation (choice left to the Director):** Option A with the tolerances P1–P7 of `civics-tree-proposal.md` §6
  (P5 now stated in CP terms; P7 added for the tax edict) as T6.3 acceptance, an AI civics lane that funds gate civics
  first, and Option B as the pre-registered remedy. **Costs are never changed to meet these tolerances** (ledger §26
  item 19; P6).
- If the 60 new civics land on the ONE pool instead (D-061 not ratified), they add 14–26 % to the A3–A7 workload
  (PROPOSED data, `civics-tree-proposal.md` §7).

## 7. Proposed acceptance measurements for content packets (PROPOSED tolerances)

For KR-3/KR-4 on seeds 1, 2, 42, 7 with both drivers, against the current-content twin: the stage turn does not move
later by more than 25 turns; every Age CORE fact that is reachable today stays reachable within the horizon; A4 CORE
does not become reachable before A3 CORE; no single target wait exceeds the baseline maximum; the share of RP lost to
overflow is reported. These are attribution checks with structural remedies, not calibration corridors (S8 §4.1 item
3); a failing check is never answered by changing a cost.
