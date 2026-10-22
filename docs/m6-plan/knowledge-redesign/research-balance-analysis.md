# Research balance analysis — current vs proposed

**Status: MEASURED baseline (runs on seeds 1, 2, 42, 7) and ESTIMATES (static model, `tools/balance.py`).** No figure
here is a simulation result for the PROPOSED graph: the proposal is not implemented. Costs are never changed to hit an
Age duration (D-046 §1; ledger §26 item 19); this document analyses pacing, it does not target it. The RP formula is
unchanged (assignment constraint); the pacing question is put to the Director separately (docket KD-10).

Data: `data/balance-current.json`, `data/balance-proposed.json` (regenerate with
`python3 -I tools/balance.py --graph current|proposed --json …`); measured runs in `measurements/`.

## 1. Four different quantities

| Quantity | What it measures | Current | Proposed |
|---|---|---|---|
| Node count | clicks / decisions | 426 technology + 7 civics = 433 | 494 technology + 67 civics = 561 active (2 retired by merge) |
| Total cost | workload | 11,192,730 RP technology (+18,130 civics) | 13,158,910 RP technology (+17.6 %); civics 335,110 points |
| Dependency depth | serial chain length (≥ 1 turn per node) | A9 CORE depth 19 | 18 |
| Turns to capability | when a capability can first exist | see §3 | see §3 (ESTIMATE) |

## 2. Cost and count by Age (technology; MEASURED for current, PROPOSED for new)

| Age | nodes now → proposed | cost now (RP) | cost proposed (RP) | change | main reasons |
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

Single-node bottleneck: the most expensive node falls from 756,960 RP (`rocket`) to 378,470 RP after the rocket and
satellite splits; nodes above 100k RP rise from 24 to 29 (mostly new A9 nuclear and science nodes, all optional).

## 3. Critical paths and estimated turns (ESTIMATE; seed 42 unless stated)

Model: turns until the cumulative RP of a trajectory covers the cheapest closure of the milestone (only that closure
researched, no Eureka, no overflow) — an optimistic lower bound. Scenarios: **weak** = half the population of a
non-expanding polity (driver RP × 0.5^0.699); **average** = the MEASURED cheapest-first driver (a polity that never
founds); **strong** = the MEASURED AI empire's own population (`sim run --ai-empires 1`, RP reconstructed; it starts with
half the settlements, so it is slower early and much faster late).

| Milestone | closure now (nodes / RP) | proposed | depth | weak | average | strong | average, seeds 1/2/42/7 (proposed) |
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
- **Ordering is repaired.** Iron (A4) now costs more to reach than bronze (A3), as it should (D02: iron needed furnace
  knowledge); A6 no longer requires the university stage (KD-04), so its closure is cheaper than A5's.
- **The CORE critical paths barely move** (A7–A9 within 1 %): the redesign adds breadth, not mandatory length.
- **Measured reality is far slower than the lower bound.** The cheapest-first driver reaches the stage at turns 996–1016
  (bound: ≈340); it explores cheap side nodes. The AI empire reaches the stage at 702–716 and completes 263–344 nodes by
  turn 1630 (`measurements/baseline-progression.md`).
- **Seed sensitivity is small** (≤ 28 turns between seeds, MEASURED): the four canonical seeds differ in pace, not path.

## 4. Sensitivity to research generation

Totals generated over 1,630 turns (MEASURED trajectories; weak = ESTIMATE): weak 0.44–0.46M RP; average (driver)
0.71–0.74M; strong (AI empire) 1.43–3.14M. Against the proposed finite workload (≈13.0M technology + 0.34M civics):

| Scenario | share of proposed workload researchable | reaches A8 CORE | reaches A9 CORE |
|---|---|---|---|
| weak | ≈ 3.4 % | turn ≈1526 (lower bound) | no |
| average | ≈ 5.5 % | ≈1199 | no |
| strong | 11–24 % | ≈1033 | ≈1172 (seed 42) |

**Risk of tedium:** the measured driver's longest single-target waits are dominated by A6–A8 nodes costing 10k–40k RP
at ≈400–800 RP/turn (`measurements/baseline-progression.md` T8). Splitting the largest late nodes (rocket, satellite,
genome sequencing, radio, computer, transistor, fission) shortens the worst waits without lowering the workload.
**Risk of a cheap endgame:** none under measured demography — the endgame is unreachable. If the Director raises RP
(KD-10), the A9 tree must still not complete within a few dozen turns; the A9 workload (10.4M RP) protects against that.

## 5. Overflow interaction (MEASURED baseline, ESTIMATE for the proposal)

Overflow is lost by design: 46.6k–49.7k RP per game, 6.5–6.7 % of all RP generated (MEASURED). Loss per completion
averages roughly half a turn of RP; the proposal adds ≈ 128 nodes, mostly late and expensive, so the expected extra loss
is small for the driver (it never reaches them) but rises for strong polities. Finer granularity raises the stakes of
KD-11 (carry vs lost).

## 6. Civics Points and technology (ESTIMATE)

The civic atom `legal_code_roman` sits in the research stage (KD-03) and the A5 CORE facts. With a separate CP pool,
the technology part of the stage closure costs 24,930 RP and the civic part 3,730 CP. Assuming CP per turn = r × RP per
turn of the same polity (a stand-in until D-061.3 rules CP's inputs) and CP spent on the gating civics first:

| CP/RP ratio r | stage delay (turns, seed 42) | A5 CORE delay | stage delay if 10k CP went to other civics first | if 30k CP first |
|---|---|---|---|---|
| 0.05 | +249 | +131 | +709 | +1,269 |
| 0.10 | +79 | 0 | +433 | +809 |
| 0.15 | 0 | 0 | +307 | +622 |
| ≥ 0.30 | 0 | 0 | small (not computed beyond 0.15 with spending) | — |

The MEASURED one-pool baseline shows 66–67 turns of slack (the civic is done long before `geometry_axiomatic`). **A slow
CP economy can stall all technology** if CP is spent elsewhere first; hence the civics lane's PROPOSED tolerances
(`civics-tree-proposal.md` §6): P2 stage no later than the one-pool twin + 10 turns, P3 civic lag median 0, max ≤ 5
turns, P4 A5 entry + 10 turns, floor CP ≥ 0.30 × RP, measured on seeds 1, 2, 42, 7 with both drivers; and an AI civics lane
that funds gate civics first. If the 60 new civics land on the ONE pool instead (D-061 not ratified), they add 14–26 % to
the A3–A7 workload (`civics-tree-proposal.md` §7).

## 7. Proposed acceptance measurements for content packets (PROPOSED tolerances)

For KR-3/KR-4 on seeds 1, 2, 42, 7 with both drivers, against the current-content twin: the stage turn does not move
later by more than 25 turns; every Age CORE fact that is reachable today stays reachable within the horizon; A4 CORE
does not become reachable before A3 CORE; no single target wait exceeds the baseline maximum; the share of RP lost to
overflow is reported. These are attribution checks with structural remedies, not calibration corridors (S8 §4.1 item 3).
