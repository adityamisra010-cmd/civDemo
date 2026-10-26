# Combined trial: route overflow + capped reserve + gate-aware goals (Director ruling 2026-10-26)

**Status:** measured for the Director's review. **The defaults are unchanged:** `researchOverflow`, `researchReserve`
and `aiGateAwareGoals` are all absent from the shipped `research.json`. Node costs and the final-Age repeatables are
unchanged. Not merged, not released.

## What was built (behind switches)

| Piece | Record | Switch | Default |
|---|---|---|---|
| Route overflow | ADR-050 | `tuning.researchOverflow` | off |
| Capped technology-lane RP reserve: cap 3 turns of RP, draw at most 1 turn a step, LOSSY, never for civics or an assist, CP untouched | ADR-051 (schema v37 `ResearchReserves`) | `tuning.researchReserve {capTurns, drawTurns}` | off |
| Gate-aware AI technology goals: a held destination the planner cannot fund is re-decided, preferring fundable goals, then the cheapest available technology, else nothing | ADR-052 | `tuning.aiGateAwareGoals` | off |
| CR-025 disposition (content, ruled) | CR-025 §7, D-063 R1 Amendment 1 | — | **on: changes the default game** |

**The trial configuration** is all three switches on: overflow + reserve {3, 1} + gate-aware goals.

**The age-gate stall** (ruling decision 2) is handled in two layers:
- The AI's technology planner never holds a goal it cannot fund. Measured result: 0 waiting steps.
- A lane that genuinely has nothing to fund (a human who has not re-ordered, or no technology available at all) falls
  to the capped reserve. RP never pays a civic, and RP and CP never mix.
- A human player's research is never chosen for them. Their blocked lane waits, the screen says so, and the reserve
  catches the RP up to its cap.

## Attribution of the new baseline

The default game ("off") changed because of CR-025's eight retargets, and only because of them:

- Shipped content with those eight realized effects stripped (`Cr025AttributionTwin_FourSeeds`) reproduces the
  pre-CR-025 baseline's final world hashes bit for bit, on all four seeds, as schema v36: `9d487f5a…`, `16f6253e…`,
  `7af2de23…`, `17b16e11…`.
- Therefore schema v37, the display fixes and the three new switches, all off, change nothing in a 1,630-turn game. The
  CR-025 future declarations and dropped deltas are inert.
- The retargets move Age 9 entry 9–26 turns earlier in the default game: seed 1 1,204 → 1,184, seed 2 1,290 → 1,264,
  seed 42 1,143 → 1,134, seed 7 1,158 → 1,145.
- **The "off" column below is the new baseline.**

## Required measurements (seeds 1, 2, 42, 7; 1,630 turns; canonical world, one AI Empire)

Full tables are generated below (§ Tables). In summary:

1. **RP waste, overall and by Age (both off vs all on).**
   - Overall: 55.7 % → 49.5 %.
   - Ages 2–8: 3–23 % → 1.7–9.7 %. Age 7: 22.1 % → 8.0 %.
   - Age 9: 72.0 % → 63.4 %. The remainder is structural: surplus past a funded node. In the final Age one level per
     repeatable per step cannot absorb a turn's RP, and repeatable pricing (deferred) is the lever for that.
   - Without the gate rule (overflow + reserve only) the total is 51.5 %, and Age 7 waste is 26.5 %: the reserve alone
     cannot cover a ~20-turn sweep wait.
2. **Age entry.** Earlier on every seed:
   - Age 3: 7–10 turns.
   - Age 4: 19–34.
   - Age 7: 24–52.
   - Age 9: 76–98 (seed 1 1,184 → 1,097; seed 2 1,264 → 1,166; seed 42 1,134 → 1,058; seed 7 1,145 → 1,062).

   That is about 6–8 % of a 1,630-turn game, larger than overflow alone (44–65), because the stalls are removed rather
   than paid for. Completion order holds, and there are no bursts beyond 2 nodes a turn.
3. **Civic completion.** Every Age's finite civics complete sooner: Age 5 by 15–54 turns, Age 9 by 79–100.
4. **Roads.** Road developments at the end change by −19 to −1, so the network is essentially unchanged. Structures,
   institutions and technologies/civics completed by the end are identical. Known cells are identical except seed 42
   (−19). Population is up 1–7 %.
5. **Major research milestones.**
   - Age milestones are met sooner from Age 3 on: the Age-9 median 74–97 turns sooner.
   - Repeatable levels at the end rise 32–90, because Age 9 lasts longer.
6. **Casual player, and no advantage for leaving a lane empty** (the human proxy re-targets d turns after a lane
   empties; Age 9 entry, mean of 4 seeds):

   | Configuration | Player | Age 9 entry | vs the AI |
   |---|---|---:|---:|
   | Shipped | AI | 1,182 | — |
   | Shipped | d = 1 | 1,190 | +9 |
   | Shipped | d = 3 | 1,218 | +36 |
   | Trial | AI | 1,096 | — |
   | Trial | d = 1 | 1,093 | −3 |
   | Trial | d = 3 | 1,098 | +2 |
   | Trial | d = 6 (deliberate idling) | 1,154 | **+58** |

   - **The casual player's lag is gone:** +36 → +2.
   - **Leaving a lane empty gives no advantage.** The attentive and casual proxies are within ±3 turns of the AI at
     Age 9, and deliberate idling loses 58 turns.
   - At intermediate Ages the d = 1 and d = 3 proxies run up to 7 turns ahead of the AI (Ages 5 and 7). That is within
     seed noise and closes by Age 9.
7. **Age-gate waiting, reserve saturation, alternative goals:**

   | Run | Technology waiting steps | Reserve deposited / drawn | Turns at the reserve cap | Goal switches away from an incomplete destination |
   |---|---:|---:|---:|---:|
   | Shipped | 66 | — | — | 0 |
   | Trial | **0** | 0 / 0 (the AI's lanes never empty) | 0 | 14 |
   | Trial without the gate rule | 163 | 251,389 / 251,389 | 98 | 0 |
   | Trial, d = 3 proxy | 0 | 6.34 M / 6.25 M | 35 | 7 |
   | Trial, d = 6 proxy | 0 | 3.76 M / 3.70 M | 125 | 4 |

   - The technology lane successfully selects alternative goals (14 switches across 4 seeds) and never waits.
   - Without the gate rule the reserve saturates for 98 turns and the waits remain: the planner rule is what removes
     the stall.
   - The gate agent's open item (Age 7 later with the gate rule, seeds 1 and 2, measured with overflow only) does **not**
     reproduce in the combined configuration: Age 7 is identical with and without the gate rule (888, 940). Per-Age
     differences of ±15 turns between the two remain at Ages 6 and 8.
8. **Determinism.**
   - Every trial run replays EQUAL from its order log, and the save/load round trip at turn 815 is equal.
   - 0 reconstruction mismatches in every run.
   - Rig tests pin save/load and replay with a non-zero reserve (ADR-051).

## Review points (no change made)

- **Pacing.** The trial configuration makes the game about 80–100 turns shorter to Age 9. If the configuration is
  adopted, the pacing scales (or costs) need recalibrating against this baseline. That is the deferred cost work, and
  it starts only after this review.
- **Age 9 waste of 63 %** remains. It is the repeatables' question (deferred, `repeatables-proposal.md`).
- **The AI never uses the reserve** in the trial configuration. The reserve exists for human players and city states.
  Its cost to the AI is nil.
- **CR-025 R1 known gaps.** 17 nodes outside CR-025 unlock only placeholder entities, so they have neither a real nor a
  future effect. They are listed exactly in `Tranche2ContentTests.KnownGapsWithoutARealEffect` and CR-025 §7.3, content
  unchanged. A disposition is needed (future declarations, or real effects).

## Data

- `data/combined/combined-runs.tar.xz`: the per-turn, per-node and milestone CSVs of all 32 runs.
- `data/combined/combined-summaries.txt`: run summaries, including the CR-025 twin.
- Regenerate the tables: `python3 -I tools/research_economy_report.py combined <dir>`.
- Runs: `Sim.Tests/Kernel/ResearchEconomyMeasurement.CombinedTrial_FourSeeds` and `Cr025AttributionTwin_FourSeeds`
  (`SIM_MEASURE=1`).

## Tables (generated)

### RP waste by polity Age (four seeds pooled): lost / generated

| Age | off | trial | trial without the gate rule |
|---|---:|---:|---:|
| A1 | 3.5% | 3.5% | 3.5% |
| A2 | 7.5% | 2.4% | 2.4% |
| A3 | 11.6% | 1.7% | 2.0% |
| A4 | 6.6% | 6.9% | 8.2% |
| A5 | 19.9% | 7.5% | 11.3% |
| A6 | 23.8% | 5.3% | 4.9% |
| A7 | 22.1% | 8.0% | 26.5% |
| A8 | 26.8% | 9.7% | 9.1% |
| A9 | 72.0% | 63.4% | 65.6% |
| **All** | 55.7% | 49.5% | 51.5% |

### Polity Age entered (effective turn): off → trial

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---|---|---|---|---|---|---|
| 1 | 67 → 67 (+0) | 339 → 329 (-10) | 683 → 664 (-19) | 716 → 697 (-19) | 783 → 768 (-15) | 934 → 888 (-46) | 1060 → 1003 (-57) | 1184 → 1097 (-87) |
| 2 | 67 → 67 (+0) | 355 → 348 (-7) | 728 → 694 (-34) | 767 → 744 (-23) | 844 → 836 (-8) | 992 → 940 (-52) | 1136 → 1097 (-39) | 1264 → 1166 (-98) |
| 42 | 66 → 66 (+0) | 341 → 333 (-8) | 693 → 673 (-20) | 727 → 711 (-16) | 793 → 773 (-20) | 920 → 896 (-24) | 1023 → 969 (-54) | 1134 → 1058 (-76) |
| 7 | 67 → 67 (+0) | 342 → 334 (-8) | 691 → 671 (-20) | 723 → 707 (-16) | 792 → 769 (-23) | 918 → 871 (-47) | 1031 → 972 (-59) | 1145 → 1062 (-83) |

### Civics: turn each Age's finite civics are complete (off → trial)

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 265 → 265 | 464 → 461 | 638 → 625 | 768 → 755 | 906 → 862 | 1020 → 989 | 1108 → 1034 | 1317 → 1231 | 1405 → 1317 |
| 2 | 287 → 287 | 494 → 492 | 682 → 673 | 831 → 823 | 975 → 923 | 1103 → 1085 | 1183 → 1097 | 1397 → 1301 | 1489 → 1389 |
| 42 | 269 → 269 | 470 → 467 | 647 → 633 | 780 → 760 | 897 → 882 | 992 → 953 | 1067 → 1001 | 1267 → 1194 | 1351 → 1272 |
| 7 | 271 → 271 | 471 → 468 | 645 → 631 | 777 → 756 | 900 → 846 | 998 → 959 | 1080 → 999 | 1278 → 1197 | 1363 → 1278 |

### Technology: turn each Age's finite technologies are complete (off → trial)

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 263 → 255 | 476 → 455 | 651 → 629 | 770 → 746 | 895 → 862 | 1026 → 961 | 1164 → 1078 | 1317 → 1230 | 1462 → 1375 |
| 2 | 273 → 265 | 501 → 483 | 688 → 669 | 826 → 805 | 956 → 923 | 1089 → 1016 | 1244 → 1147 | 1403 → 1303 | 1548 → 1448 |
| 42 | 265 → 256 | 480 → 460 | 659 → 637 | 781 → 757 | 889 → 855 | 996 → 943 | 1114 → 1041 | 1264 → 1186 | 1409 → 1331 |
| 7 | 266 → 256 | 481 → 461 | 658 → 635 | 779 → 753 | 891 → 855 | 1002 → 938 | 1125 → 1044 | 1275 → 1192 | 1420 → 1337 |

### Age milestones (median first-met turn over milestones met in both runs; off → trial)

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---|---|---|---|---|---|---|
| 1 | 41 → 41 | 170 → 168 | 545 → 531 | 683 → 664 | 776 → 760 | 907 → 863 | 982 → 940 | 1162 → 1076 |
| 2 | 41 → 41 | 180 → 178 | 574 → 561 | 728 → 709 | 836 → 828 | 912 → 881 | 1049 → 1014 | 1252 → 1155 |
| 42 | 41 → 41 | 171 → 170 | 551 → 537 | 693 → 673 | 786 → 766 | 848 → 830 | 959 → 930 | 1113 → 1039 |
| 7 | 41 → 41 | 172 → 170 | 551 → 537 | 691 → 671 | 784 → 762 | 848 → 814 | 961 → 917 | 1124 → 1042 |

### End state: research, roads, infrastructure (off → trial)

| Seed | Tech | Civics | Levels | Road developments | Structures | Institutions | Known cells | Population |
|---|---|---|---|---|---|---|---|---|
| 1 | 525 → 525 | 480 → 480 | 321 → 364 | 692 → 678 | 55 → 55 | 15 → 15 | 991 → 991 | 52,270,070 → 54,216,139 |
| 2 | 525 → 525 | 480 → 480 | 234 → 324 | 950 → 931 | 57 → 57 | 15 → 15 | 885 → 885 | 47,744,127 → 51,101,922 |
| 42 | 525 → 525 | 480 → 480 | 361 → 393 | 768 → 767 | 57 → 57 | 15 → 15 | 1232 → 1213 | 76,872,583 → 77,780,342 |
| 7 | 525 → 525 | 480 → 480 | 348 → 383 | 853 → 852 | 55 → 55 | 15 → 15 | 1195 → 1195 | 61,276,426 → 62,880,395 |

### Casual and idling players: polity Age entered, mean over four seeds (shift vs the AI under the same rules)

| Configuration | Player | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---|---|---|---|---|---|---|---|
| off (shipped) | AI (no delay) | 67 | 344 | 699 | 733 | 803 | 941 | 1062 | 1182 |
| off (shipped) | proxy, 1-turn delay | 68 (+2) | 348 (+4) | 705 (+6) | 736 (+3) | 816 (+13) | 944 (+2) | 1072 (+10) | 1190 (+9) |
| off (shipped) | proxy, 3-turn delay | 72 (+5) | 356 (+11) | 716 (+17) | 750 (+17) | 836 (+33) | 969 (+28) | 1096 (+34) | 1218 (+36) |
| trial | AI (no delay) | 67 | 336 | 676 | 715 | 786 | 899 | 1010 | 1096 |
| trial | proxy, 1-turn delay | 67 (+0) | 336 (-0) | 675 (-1) | 709 (-6) | 784 (-2) | 892 (-7) | 1012 (+2) | 1093 (-3) |
| trial | proxy, 3-turn delay | 66 (-0) | 336 (-0) | 674 (-2) | 708 (-6) | 783 (-4) | 894 (-5) | 1010 (-1) | 1098 (+2) |
| trial | proxy, 6-turn delay | 72 (+5) | 346 (+10) | 690 (+15) | 732 (+17) | 814 (+28) | 930 (+31) | 1051 (+40) | 1154 (+58) |

### Age-gate waiting, reserve and goal switching (four seeds pooled)

| Run | Technology waiting steps | Empty-lane RP lost | Reserve deposited / drawn | Turns at the reserve cap | Technology goal switches away from an incomplete destination |
|---|---:|---:|---|---:|---:|
| off | 66 | 1.1% | 0 / 0 | 0 | 0 |
| trial | 0 | 0.0% | 0 / 0 | 0 | 14 |
| trial-nogate | 163 | 2.2% | 251,389 / 251,389 | 98 | 0 |
| d1-off | 35 | 3.8% | 0 / 0 | 0 | 0 |
| d3-off | 4 | 6.5% | 0 / 0 | 0 | 0 |
| d1-trial | 0 | 0.0% | 4,208,186 / 4,208,186 | 0 | 10 |
| d3-trial | 0 | 0.7% | 6,344,257 / 6,251,714 | 35 | 7 |
| d6-trial | 0 | 6.3% | 3,763,474 / 3,696,860 | 125 | 4 |

### Determinism (trial runs)

| Seed | Reconstruction mismatches | Replay from the order log | Save/load at mid-run |
|---|---:|---|---|
| 1 | 0 | EQUAL | equal |
| 2 | 0 | EQUAL | equal |
| 42 | 0 | EQUAL | equal |
| 7 | 0 | EQUAL | equal |
