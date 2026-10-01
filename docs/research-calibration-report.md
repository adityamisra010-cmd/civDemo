# Research calibration report

**GENERATED** by `scripts/research-calibration-report.py` (do not edit by hand) from `Sim.Data/content/research.json`, `scripts/research-calibration/nodes.json` and the measured population trace `scripts/research-calibration/population-trace-seed42.csv`. Everything here is **PROVISIONAL CALIBRATION** (Director ruling 2026-10-01 §2–§4); nothing in it is ratified architecture.

## 1. The model being calibrated

- **Research capacity:** RP(P) = 2 × (P / 100)^0.69897 per turn of 10 sim-years (anchors 100 → 2, 1000 → 10). A step credits RP(P) × dtYears / 10 (law 3). P = the polity's total population. The per-turn vs per-sim-year reading is OPEN (CR-018).
- **Eureka:** a fully satisfied Eureka credits 40% of BASE cost; each condition its normalized share; capped at the remaining cost; never twice.
- **Cost:** cost = round(BAND_WEIGHT[band] × AGE_UNIT[age]). BAND_WEIGHT = `{"M1": 1.0, "M2": 2.0, "M3": 3.5, "M4": 6.0, "M5": 10.0}`. AGE_UNIT = `{"A1": 10.99, "A2": 9.44, "A3": 133.49, "A4": 130.08, "A5": 178.08, "A6": 261.85, "A7": 129.85, "A8": 33.74, "A9": 39.01}`.
- **Throughput loss:** CLP past a node's completion is lost (ADR-029 §13 R-2, no general bank), so many cheap nodes cost more turns than their summed cost suggests. The projections below simulate it turn by turn.

## 2. Assumptions (all of them)

1. **Population:** the canonical world (seed 42), polity 1, measured every turn to turn 1630. Founding population 5245; at the final-Age start (year 1920, turn 1270) 442503; at turn 1630 506031. It is the most developed polity the simulation currently produces.
2. **Age pacing windows** (reference civilization; calendar year each Age's content should be done by): A1 -3600, A2 -3000, A3 -1200, A4 -300, A5 500, A6 1450, A7 1800, A8 1920; A9 from 1920. The Age → calendar/dt mapping is OPEN (architecture §12.3); these are calibration targets, never gates (Age stays metadata, law 4).
3. **Reference civilization** (Ages A1–A8): that measured population, no future modifiers (×1.0), every currently evaluable Eureka condition credited when its node becomes available.
4. **Research-optimized civilization** (the final-Age target): mean population over the first 200 final-Age turns = 459,623; mean dt 0.5 (current Modern / Information+ bands) → mean 36.298 RP/turn; FUTURE university specialization and research-efficiency modifiers assumed to divide EffectiveCost by **1.5** (they cannot be simulated yet); every counted Eureka condition satisfied (the 40 % upper bound).
5. **Order of research:** cheapest available first, ties by key — the `sim research --auto cheapest` driver.
6. **Finite vs recursive:** the final-Age target applies to the FINITE corpus: nodes with a repeatable descriptor (recursive-research candidates) are excluded and reported separately.

## 3. Cost by Age

| Age | nodes | total cost | average | median | min | max | finite nodes | finite total | AGE_UNIT (in use) | AGE_UNIT (derived) |
|---|---|---|---|---|---|---|---|---|---|---|
| A1 | 28 | 1,047 | 37.4 | 30.0 | 11 | 110 | 28 | 1,047 | 10.99 | 10.99 |
| A2 | 51 | 1,573 | 30.8 | 19.0 | 9 | 94 | 51 | 1,573 | 9.44 | 9.44 |
| A3 | 30 | 15,750 | 525.0 | 467.0 | 133 | 1335 | 30 | 15,750 | 133.49 | 133.49 |
| A4 | 30 | 14,302 | 476.7 | 455.0 | 130 | 1301 | 30 | 14,302 | 130.08 | 130.08 |
| A5 | 35 | 20,650 | 590.0 | 623.0 | 178 | 1781 | 35 | 20,650 | 178.08 | 178.08 |
| A6 | 40 | 40,189 | 1004.7 | 916.0 | 262 | 2618 | 40 | 40,189 | 261.85 | 261.85 |
| A7 | 33 | 19,081 | 578.2 | 454.0 | 260 | 1298 | 33 | 19,081 | 129.85 | 129.85 |
| A8 | 51 | 7,206 | 141.3 | 118.0 | 34 | 337 | 51 | 7,206 | 33.74 | 33.74 |
| A9 | 126 | 18,331 | 145.5 | 137.0 | 39 | 390 | 116 | 17,941 | 39.01 | 39.01 |
| Civics | 6 | 3,147 | | | 260 | 916 | | | | |

- **Total research workload:** 138,129 RP over 424 technologies (+ 3,147 for 6 civics). **Finite** (no repeatable descriptor): 137,739 RP over 414 technologies.
- **Bands:** M1 ×1.0 — 29 nodes, M2 ×2.0 — 136 nodes, M3 ×3.5 — 146 nodes, M4 ×6.0 — 88 nodes, M5 ×10.0 — 25 nodes

## 4. Eureka credit under the current model

| Age | nodes with an evaluable condition | current credit (RP) | current credit % of cost | upper bound (all counted conditions) % |
|---|---|---|---|---|
| A1 | 14 | 177.8 | 17.0% | 40.0% |
| A2 | 18 | 155.4 | 9.9% | 40.0% |
| A3 | 14 | 1,930.8 | 12.3% | 40.0% |
| A4 | 10 | 953.3 | 6.7% | 40.0% |
| A5 | 11 | 1,174.9 | 5.7% | 40.0% |
| A6 | 11 | 2,172.6 | 5.4% | 40.0% |
| A7 | 2 | 142.8 | 0.7% | 40.0% |
| A8 | 3 | 43.8 | 0.6% | 40.0% |
| A9 | 0 | 0.0 | 0.0% | 36.0% |

## 5. Whole-campaign projection (reference civilization)

Turn-by-turn from founding with zero knowledge, the measured RP per turn, earliest Age first then cheapest (a historically-paced player), current Eurekas, no future modifiers. 390 of 430 nodes complete by turn 1630.

| Age | nodes | done by turn (year) | window ends turn (year) | verdict |
|---|---|---|---|---|
| A1 | 28 | 39 (-3610) | 40 (-3600) | within window |
| A2 | 51 | 99 (-3010) | 100 (-3000) | within window |
| A3 | 30 | 297 (-1265) | 310 (-1200) | within window |
| A4 | 30 | 485 (-325) | 490 (-300) | within window |
| A5 | 35 | 650 (500) | 650 (500) | within window |
| A6 | 40 | 974 (1448) | 975 (1450) | within window |
| A7 | 33 | 1149 (1798) | 1150 (1800) | within window |
| A8 | 51 | 1270 (1920) | 1270 (1920) | within window |
| A9 | 126 | 86 done by turn 1630 | 1630 | incomplete |

## 6. Final-Age pacing (the hard target)

Start: every non-A9 node complete. Research the 116 finite A9 nodes (17,941 RP base) at a constant 36.298 RP/turn, EffectiveCost = cost / modifier.

| scenario | modifier | Eurekas | turns | band |
|---|---|---|---|---|
| **research-optimized (the calibration target)** | ×1.5 | full | **215** | 150–250 |
| research-optimized, current Eurekas only | ×1.5 | current | **397** | >250 |
| strongly optimized (modifiers x2.0), all Eurekas | ×2.0 | full | **138** | <150 |
| well developed, no future modifiers (x1.0), all Eurekas | ×1.0 | full | **377** | >250 |
| poorly developed: no modifiers, current Eurekas | ×1.0 | current | **555** | >250 |

- **Result:** the research-optimized civilization needs **215 turns** for the finite final-Age corpus — **150–250** (target 200, band 150–250).
- Including the 10 repeatable-descriptor nodes once each (recursive-research candidates, completed once under idempotent completion): 225 turns.
- **What drives it:** (1) AGE_UNIT[A9] — derived from the target, it scales every final-Age cost; (2) the final-Age population and dt (RP per turn ∝ P^0.699 × dt); (3) the assumed future-modifier multiplier; (4) Eureka satisfaction (up to −40 % of each node's base cost); (5) completion-overflow loss, which grows as per-turn RP approaches node costs; (6) the band mix (M1 ×1 … M5 ×10). Prerequisites order the work but add none, because research is serial (one target).
- **Structural floor — a finding for the Director:** research is serial (one target) and CLP past a completion is lost (ADR-029 R-2), so every node costs at least one turn: the 116 finite final-Age nodes can never take fewer than 116 turns, whatever their cost. At the final-Age RP per turn (36.3) most final-Age nodes take one or two turns, so turns move in STEPS as costs cross whole-turn multiples, not smoothly with cost:

  | AGE_UNIT[A9] | optimized-scenario turns |
  |---|---|
  | 19.50 | 133 |
  | 29.26 | 169 |
  | 38.99 | 177 |
  | 39.01 | 215 |
  | 48.76 | 252 |
  | 58.52 | 263 |
  | 78.02 | 387 |

  The unit in use is the one whose projection is closest to the 200-turn target. Pacing in this regime is governed by node count, RP per turn and the overflow rule more than by cost; whether completion overflow should carry to the next target (an R-2 change) is listed as OPEN.
- The pacing was NOT solved by making final-Age nodes enormous: final-Age costs are the same bands × one Age unit, and the Age total is what was calibrated.

