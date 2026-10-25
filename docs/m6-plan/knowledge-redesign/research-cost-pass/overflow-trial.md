# Route-overflow trial (D-064 evidence; `researchOverflow` stays OFF in the shipped content)

**Status:** trial for the Director's review. The shipped `research.json` is unchanged and both overflow switches stay
absent (off). The lane variant (`researchOverflowLane`) was not used. No node cost changed.

**Baseline.** The tree after the defect fixes: `9204de7` (display fixes k2–k5, display only) and `cce5d72` (CR-025:
tests and docs only). Neither moves simulation state, so this baseline is the shipped game.

## Setup

`Sim.Tests/Kernel/ResearchEconomyMeasurement.RouteOverflowTrial_FourSeeds` (run with `SIM_MEASURE=1`), on the shared
runner `ResearchPacingRunner`:
- The canonical world, one AI Empire, seeds 1, 2, 42 and 7, 1,630 turns.
- OFF is the shipped engine. ON sets `tuning.researchOverflow` and nothing else.
- **Determinism and replay.** Every ON run is replayed from its recorded order log in a fresh executor (no AI, no
  harness), with a `Snapshot` save/load round trip at turn 815, and the final world hashes are compared.
- **RP accounting.** Each step's funding is reconstructed with the engine's own planner and checked against the outcome.
  - RP spent = technology progress added + RP that assisted a civic, credits excluded.
  - RP lost is split into SURPLUS (the lane funded a node) and EMPTY (the lane funded nothing).
- **Regenerate the tables:** `python3 -I tools/research_economy_report.py trial <dir>`, after extracting
  `data/trial/trial-runs.tar.xz` into `<dir>`. Raw summaries: `data/trial/trial-summaries.txt`.

## Findings

1. **RP waste falls, but less than the headline suggests.** RP lost overall goes from 54.5 % to 50.8 % (four seeds
   pooled):
   - In Ages 2, 3, 6 and 8, waste drops by 3 to 4 times: 7.5 % → 2.4 %, 11.6 % → 2.3 %, 24.1 % → 7.7 %,
     25.9 % → 8.2 %.
   - Age 9 stays high, 72.1 % → 66.2 %. One turn's RP buys several final-Age nodes, but each node is paid at most once a
     step and a repeatable gains at most one level a step, so the rest has nowhere to go.
   - Age 7 gets *worse*, 22.5 % → 26.7 %. See finding 3.
2. **Acceleration is moderate and concentrated late.**
   - Age entry moves 7–10 turns earlier at Age 3, 15–20 at Ages 4–6, 35–45 at Age 7, and 44–65 at Age 9 (about 4–5 %
     of a 1,630-turn game).
   - Completion order is essentially unchanged: Spearman ρ is 0.9991–0.9996 over 992 finite nodes per seed, and the
     median node completes 31–40 turns sooner.
   - Bursts stay small: at most 2 technologies or 2 civics complete in one turn (1 under OFF).
   - Milestones move with the Ages (Age 9 milestones are met 44–64 turns sooner).
   - Nothing in the measurement suggests a runaway. The acceleration is the recovered surplus, compounding mildly
     through earlier Age-gated income.
3. **Unexpected: overflow makes technology wait on the Age sweep.**
   - Under ON, the technology lane spends 120 Age-7 turns (4 seeds) holding a destination it cannot fund, against 12 under
     OFF, plus 24 in Age 5.
   - The waiting probe (seeds 1 and 42, `data/trial/waiting-probe.txt`) shows every such step is the Age sweep. The next
     Age's technology destination (heavy_plough in Age 5, germ_theory in Age 7) needs the current Age's civics cohort
     complete first.
   - Overflow speeds technology more than civics, so technology reaches the gate first and its RP is lost while it
     waits: the Age-7 EMPTY loss goes from 2.3 % to 23.0 %.
   - This is not an overflow defect. It is the empty-lane case, and it is the strongest argument for deciding overflow
     and the empty-lane rule together (`empty-lane-reserve-proposal.md`).
4. **CP is sound.**
   - The CP identity (Generated − Spent = Balance) holds on every run.
   - The bank ends at 0 on every run.
   - CP generated rises 5–9 %, because Ages are reached sooner and CP accrual grows with them. The peak bank rises by the
     same order.
   - Civics complete at most 2 per turn under ON.
5. **Repeatable levels behave.**
   - The first level comes 44–64 turns sooner.
   - End-of-game levels rise 28–47, because Age 9 lasts longer.
   - At most 2 levels in a turn (one per lane), the same under OFF and ON. The once-per-step rule holds at scale.
6. **The rest of the economy is unchanged.**
   - Structures, institutions, known cells, and technologies and civics completed by the end are identical OFF vs ON on
     every seed.
   - Road developments differ by 1–17.
   - Population is up 1–5 %.
7. **Determinism.**
   - All four ON runs replay to the same final hash from their order logs, and the save/load round trip at turn 815 is
     equal.
   - The four OFF runs reproduce their hashes bit for bit across separate processes and runs (seed 42: `7af2de23…` in
     the timing run, the trial and the waiting probe).
   - 0 reconstruction mismatches in 13,040 technology lane-steps (8 runs × 1,630 turns).

## Verdict for review

Route overflow does what it claims, deterministically, with no excessive acceleration or bursts. On its own, though, it
recovers only 3.7 points of the RP lost, and it creates a new loss: technology waiting on the sweep. Recommendation:

- Keep `researchOverflow` OFF until the empty-lane rule is ruled on.
- Then adopt both together and refresh the pacing baseline with both on.
- Adopting overflow alone would shift Ages 7–9 by 35–65 turns, and that shift would have to be re-measured again once
  the empty-lane rule lands.

## Tables (generated)

### Polity Age entered (effective turn): OFF → ON, and the shift

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---|---|---|---|---|---|---|
| 1 | 67 → 67 (+0) | 339 → 329 (-10) | 683 → 664 (-19) | 717 → 697 (-20) | 783 → 767 (-16) | 939 → 904 (-35) | 1074 → 1036 (-38) | 1204 → 1139 (-65) |
| 2 | 67 → 67 (+0) | 355 → 348 (-7) | 728 → 713 (-15) | 768 → 753 (-15) | 847 → 832 (-15) | 1005 → 962 (-43) | 1156 → 1134 (-22) | 1290 → 1246 (-44) |
| 42 | 66 → 66 (+0) | 341 → 333 (-8) | 693 → 673 (-20) | 727 → 707 (-20) | 793 → 774 (-19) | 924 → 882 (-42) | 1030 → 992 (-38) | 1143 → 1081 (-62) |
| 7 | 67 → 67 (+0) | 342 → 334 (-8) | 692 → 672 (-20) | 728 → 708 (-20) | 793 → 769 (-24) | 919 → 874 (-45) | 1037 → 997 (-40) | 1158 → 1094 (-64) |

### Technology: turn all finite content of an Age is complete (OFF → ON)

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 263 → 255 | 476 → 455 | 651 → 629 | 770 → 746 | 898 → 866 | 1039 → 982 | 1184 → 1120 | 1342 → 1277 | 1483 → 1422 |
| 2 | 273 → 265 | 501 → 483 | 688 → 669 | 829 → 807 | 963 → 932 | 1109 → 1042 | 1270 → 1226 | 1429 → 1385 | 1574 → 1530 |
| 42 | 265 → 256 | 480 → 460 | 659 → 637 | 781 → 757 | 890 → 859 | 1001 → 947 | 1123 → 1063 | 1273 → 1211 | 1414 → 1356 |
| 7 | 266 → 256 | 481 → 461 | 658 → 636 | 780 → 754 | 894 → 858 | 1008 → 945 | 1138 → 1075 | 1288 → 1224 | 1433 → 1369 |

### Civics: turn all finite content of an Age is complete (OFF → ON)

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 265 → 265 | 464 → 461 | 639 → 625 | 769 → 753 | 911 → 877 | 1033 → 1003 | 1129 → 1074 | 1336 → 1272 | 1430 → 1364 |
| 2 | 287 → 287 | 494 → 492 | 683 → 673 | 834 → 818 | 988 → 945 | 1121 → 1101 | 1211 → 1174 | 1423 → 1379 | 1515 → 1471 |
| 42 | 269 → 269 | 470 → 467 | 647 → 633 | 781 → 761 | 901 → 852 | 997 → 964 | 1074 → 1026 | 1276 → 1216 | 1361 → 1299 |
| 7 | 271 → 271 | 471 → 468 | 646 → 632 | 777 → 756 | 902 → 849 | 1004 → 968 | 1090 → 1029 | 1291 → 1228 | 1375 → 1310 |

### RP generated, spent and lost by polity Age (four seeds pooled)

| Age | OFF generated | OFF lost | of which surplus / empty | ON generated | ON lost | of which surplus / empty |
|---|---:|---:|---|---:|---:|---|
| A1 | 6,143 | 3.5% | 3.5% / 0.0% | 6,143 | 3.5% | 3.5% / 0.0% |
| A2 | 78,214 | 7.5% | 7.5% / 0.0% | 74,080 | 2.4% | 2.4% / 0.0% |
| A3 | 1,045,355 | 11.6% | 11.6% / 0.0% | 945,103 | 2.3% | 1.6% / 0.7% |
| A4 | 398,339 | 8.2% | 8.2% / 0.0% | 378,742 | 6.9% | 6.9% / 0.0% |
| A5 | 1,376,151 | 19.2% | 17.1% / 2.1% | 1,322,910 | 14.9% | 4.7% / 10.2% |
| A6 | 5,128,532 | 24.1% | 13.6% / 10.5% | 4,197,049 | 7.7% | 2.7% / 5.0% |
| A7 | 5,590,591 | 22.5% | 20.2% / 2.3% | 5,899,968 | 26.7% | 3.7% / 23.0% |
| A8 | 6,382,822 | 25.9% | 25.9% / 0.0% | 5,183,575 | 8.2% | 8.2% / 0.0% |
| A9 | 36,055,608 | 72.1% | 72.1% / 0.0% | 42,642,106 | 66.2% | 66.2% / 0.0% |
| **All** | 56,061,755 | 54.5% | 53.3% / 1.2% | 60,649,676 | 50.8% | 48.0% / 2.8% |

### Node completion order (finite nodes completed in both runs)

| Seed | Nodes | Spearman ρ (completion turns) | Median shift (turns, ON − OFF) | Nodes earlier / later / same | Largest earlier shifts |
|---|---:|---:|---:|---|---|
| 1 | 992 | 0.9996 | -36 | 931 / 13 / 48 | central_banking -115, fusion_power -81, cellular_digital -74 |
| 2 | 992 | 0.9996 | -31 | 939 / 13 / 40 | blast_furnace_water -70, cannon_early -69, microscope -68 |
| 42 | 992 | 0.9993 | -39 | 927 / 17 / 48 | central_banking -117, fast_breeder_reactor -80, geological_disposal -80 |
| 7 | 992 | 0.9991 | -40 | 937 / 18 / 37 | central_banking -123, fusion_power -80, cellular_digital -73 |

### Age milestones: turn each Age's milestones were first met (median over the milestones met in both runs; OFF → ON)

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---|---|---|---|---|---|---|
| 1 | 41 → 41 | 170 → 168 | 545 → 531 | 683 → 664 | 776 → 760 | 912 → 878 | 990 → 955 | 1182 → 1118 |
| 2 | 41 → 41 | 180 → 178 | 574 → 561 | 728 → 713 | 840 → 824 | 920 → 891 | 1064 → 1033 | 1278 → 1234 |
| 42 | 41 → 41 | 171 → 170 | 551 → 537 | 693 → 673 | 786 → 766 | 902 → 860 | 964 → 924 | 1131 → 1071 |
| 7 | 41 → 41 | 172 → 170 | 551 → 537 | 692 → 672 | 786 → 762 | 850 → 816 | 964 → 922 | 1146 → 1083 |

### CP, repeatable levels and bursts

| Seed | Mode | CP generated / spent / banked at end | CP identity | Max CP banked | Levels at end | First level turn | Most tech / civics / levels in one turn |
|---|---|---|---|---:|---:|---:|---|
| 1 | OFF | 1,637,806 / 1,637,806 / 0 | holds | 483,635 | 301 | 1369 | 1 / 1 / 2 |
| 1 | ON | 1,761,463 / 1,761,463 / 0 | holds | 524,726 | 339 | 1305 | 2 / 2 / 2 |
| 2 | OFF | 1,349,247 / 1,349,247 / 0 | holds | 298,833 | 205 | 1457 | 1 / 1 / 2 |
| 2 | ON | 1,421,952 / 1,421,952 / 0 | holds | 321,466 | 252 | 1413 | 2 / 2 / 2 |
| 42 | OFF | 2,015,978 / 2,015,978 / 0 | holds | 703,543 | 353 | 1301 | 1 / 1 / 2 |
| 42 | ON | 2,194,376 / 2,194,376 / 0 | holds | 784,418 | 381 | 1238 | 2 / 2 / 2 |
| 7 | OFF | 1,872,365 / 1,872,365 / 0 | holds | 620,971 | 338 | 1315 | 1 / 1 / 2 |
| 7 | ON | 2,039,368 / 2,039,368 / 0 | holds | 696,244 | 368 | 1252 | 2 / 2 / 2 |

### End state and determinism

| Seed | Mode | Age | Tech | Civics | Population | Structures | Road developments | Institutions | Known cells | Reconstruction mismatches | Replay | Save/load |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|
| 1 | OFF | 9 | 525 | 480 | 47,018,057 | 55 | 671 | 15 | 991 | 0 | - | - |
| 1 | ON | 9 | 525 | 480 | 48,516,116 | 55 | 654 | 15 | 991 | 0 | EQUAL | equal |
| 2 | OFF | 9 | 525 | 480 | 42,008,153 | 57 | 907 | 15 | 885 | 0 | - | - |
| 2 | ON | 9 | 525 | 480 | 44,286,927 | 57 | 903 | 15 | 885 | 0 | EQUAL | equal |
| 42 | OFF | 9 | 525 | 480 | 69,210,515 | 57 | 736 | 15 | 1232 | 0 | - | - |
| 42 | ON | 9 | 525 | 480 | 69,951,973 | 57 | 735 | 15 | 1232 | 0 | EQUAL | equal |
| 7 | OFF | 9 | 525 | 480 | 56,179,099 | 55 | 827 | 15 | 1195 | 0 | - | - |
| 7 | ON | 9 | 525 | 480 | 57,661,133 | 55 | 826 | 15 | 1195 | 0 | EQUAL | equal |
