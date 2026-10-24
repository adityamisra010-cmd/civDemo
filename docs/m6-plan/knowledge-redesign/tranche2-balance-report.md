# Tranche 2 — balance report (D-063 R6, the pincer)

**Status:** measured on the final content and engine of this branch. Measurement:
`Sim.Tests/Kernel/PacingReferenceMeasurement` (skipped by default; run explicitly). It uses the canonical world (1,024 px),
one AI Empire, seeds 1, 2, 7 and 42, and 1,630 turns (−4000 to the present). The raw output is
`data/tranche2/reference-trajectory-final.txt`; it reproduced byte for byte on a second run.

## 1. The pincer — turn at which ALL finite content of an Age is complete, per tree

The target is that Age x is complete by the midpoint of the target entries of Ages x+1 and x+2. The Age rule requires Age x
complete before Age x+2 opens, so the trees must move together.

| Age | Target | Seed 1: Tech / Civics (gap) | Seed 2: Tech / Civics (gap) | Seed 7: Tech / Civics (gap) | Seed 42: Tech / Civics (gap) |
|---|---:|---|---|---|---|
| A1 | 240 | 263 / 265 (2) | 273 / 287 (14) | 266 / 271 (5) | 265 / 269 (4) |
| A2 | 510 | 476 / 464 (12) | 501 / 494 (7) | 481 / 471 (10) | 480 / 470 (10) |
| A3 | 690 | 651 / 639 (12) | 688 / 683 (5) | 658 / 646 (12) | 659 / 647 (12) |
| A4 | 820 | 766 / 767 (1) | 825 / 833 (8) | 775 / 777 (2) | 777 / 779 (2) |
| A5 | 970 | 892 / 908 (16) | 959 / 986 (27) | 890 / 902 (12) | 886 / 898 (12) |
| A6 | 1155 | 1035 / 1029 (6) | 1107 / 1119 (12) | 1001 / 997 (4) | 997 / 993 (4) |
| A7 | 1325 | 1169 / 1114 (55) | 1263 / 1204 (59) | 1128 / 1076 (52) | 1113 / 1067 (46) |
| A8 | 1460 | 1323 / 1321 (2) | 1421 / 1416 (5) | 1279 / 1281 (2) | 1263 / 1266 (3) |
| A9 | 1520 | 1468 / 1409 (59) | 1566 / 1509 (57) | 1424 / 1367 (57) | 1408 / 1349 (59) |

- **Within ±30 turns:** 28 of 36 (seed, Age) cells; seven of the nine Ages are within ±27 turns on every seed.
- **Residual: Ages 7 and 9.** Technology finishes 46–59 turns after civics.
  - The cause is structural, not a cost choice. The technology lane completes at most one node per turn: Research
    Points do not overflow to a second node (D-044 R5). Age 9 holds 141 finite technologies, so it takes about 141 turns
    however cheap it is.
  - The civics lane spends a bankable balance (ADR-043), so it can complete several civics in one turn once the Age
    sweep opens them. Raising civics costs mostly drains the bank rather than adding turns: ×3 on Age 9 moved it 14 turns,
    and ×10 stalled seed 2.
  - Closing it needs an engine ruling, either RP overflow within a lane or a per-turn cap on civic completions. It is
    listed in `docs/queue.md`.
- **"No slower":** every seed now completes both trees. Pre-tranche, no seed reached Age 9 and three of four ended in
  Age 7.
- **Repeatables:** they run in the remaining turns, 213–358 levels per seed by turn 1,630.

## 2. Scale used — `tuning.pacingScale`

BaseCost = content cost (U × K^magnitude) × scale[tree][Age].

| Tree | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| technology | 0.396 | 0.804 | 1.37 | 3.41 | 5.04 | 4.82 | 5.07 | 0.776 | 0.0681 |
| civics | 0.646 | 0.999 | 0.908 | 1.31 | 1.4 | 1.79 | 0.757 | 0.0706 | 0.00869 |

**Calibration history.** Fourteen measured passes; every trajectory is kept under the session's scratch directory, and the
final one is in this package.

1. **First pass** (`tools/calibrate_pacing.py`, from the pre-tranche trajectory):
   - It exposed a cross-lane deadlock: every seed stalled in Age 3 at turn ~550.
   - This is now fixed in the planner: a lane with nothing fundable assists the other lane's route.
2. **Next passes** (`tools/recalibrate_observed.py`): these correct each (tree, Age) from observed completion turns.
   - The slowest seed binds the target.
   - The pincer term pulls each tree to the pair's midpoint.
   - Pass 10 was chosen: all seeds finish, 28/36 cells within 30 turns.
   - Later passes and two manual civics adjustments either oscillated or stalled seed 2. They were measured and rejected.
3. **The loader bound on a scale was raised from 4 to 16.** Technology Ages 4–7 sit at 3.4–5.1 because the knowledge
   effects raise RP far above the pre-tranche economy (§3).

## 3. The economy moved — Director attention

| Seed | Pre-tranche: Age, tech, civics, population at 1,630 | Tranche 2: Age, tech, civics, population at 1,630 | Repeatable levels |
|---|---|---|---:|
| 1 | A8, 141, 51, 2,219,136 | A9, 525, 480, 47,491,235 | 314 |
| 2 | A7, 104, 39, 251,684 | A9, 525, 480, 42,694,874 | 213 |
| 7 | A7, 103, 41, 282,890 | A9, 525, 480, 56,478,370 | 343 |
| 42 | A7, 104, 41, 302,146 | A9, 525, 480, 69,383,665 | 358 |

| Turn | Pre-tranche population (seed 42) | Tranche 2 population (seed 42) | Pre RP/turn | Tranche 2 RP/turn |
|---:|---:|---:|---:|---:|
| 200 | 11,953 | 13,676 | 57 | 62 |
| 400 | 31,219 | 74,245 | 111 | 203 |
| 600 | 67,441 | 728,393 | 190 | 1,002 |
| 800 | 115,069 | 14,181,504 | 276 | 8,244 |
| 1000 | 176,857 | 23,727,450 | 372 | 12,773 |
| 1200 | 232,512 | 35,434,910 | 451 | 18,063 |
| 1400 | 258,133 | 54,046,312 | 485 | 25,816 |
| 1630 | 302,146 | 69,383,665 | 542 | 31,483 |

- **What happened.** The realized knowledge effects compound (food yield, per-farmer output, famine absorption,
  mortality, catchment, colonization), and research is now much faster. An AI empire therefore ends near 40–70 million
  people instead of ~0.25–2 million, and RP per turn is ~60 times the pre-tranche figure.
- **What it implies.** It is arguably closer to history: ~14 million at 950 CE on seed 42, against a pre-tranche
  115,000. But it is a large change to every economic number, and every golden moves (re-pinned with attribution).
- **The control.** The switch `tuning.knowledgeEffects` is the attribution control, `TestConfigs.PreKnowledgeEffects`.
- **The levers if the Director wants a smaller economy:**
  - the per-operand caps (ADR-047 registry);
  - a uniform content scale on the realized deltas (re-run `tools/import_tranche2.py`);
  - or both.
  Pacing would then need one more calibration pass. Not changed here without a ruling.

## 4. Reproduce

```
python3 -I tools/import_tranche2.py              # content (re-runnable from the pristine arrays)
# un-skip Sim.Tests/Kernel/PacingReferenceMeasurement, then:
dotnet test Sim.Tests -c Release --filter FullyQualifiedName~PacingReferenceMeasurement --logger "console;verbosity=detailed"
python3 -I tools/recalibrate_observed.py <that output> --damp 0.5 [--write]
```
