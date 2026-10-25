# Research overflow — off / route / lane comparison (D-064 evidence; NOTHING ENABLED)

**Status:** evidence for the Director's review. `tuning.researchOverflow` and `tuning.researchOverflowLane` are both
absent (off) in the shipped `research.json`; no cost or pacing value changed. Baseline commit: `a37b3fd` content; engine
`d34bed5`.

## What was measured

`Sim.Tests/Kernel/OverflowComparisonMeasurement` (skipped by default): the canonical world (1,024 px), one AI Empire, seeds
1, 2, 42 and 7, 1,630 turns — the PacingReferenceMeasurement setup. Three runs per seed on the **same shipped content**;
only the research rule differs:

| Mode | Rule |
|---|---|
| OFF | today's engine: a lane funds ONE node per step; RP past that node's remaining cost, and all RP of a step with no fundable node, is discarded (D-044 R20-D); CP is banked |
| ON | `researchOverflow`: once the funded node is fully paid, the remainder funds the lane's next node on the destination's route, then the free-lane assist — read from PREV, each node at most once per step |
| ON-LANE | ON + `researchOverflowLane`: when the route and the assist have nothing left, the remainder funds the cheapest available node of the lane's own tree (repeatables included) |

RP lost = RP generated (the per-turn pool) − RP that throughput added to technology progress (Eureka and exposure credits
excluded). Raw output: `data/overflow-measurement-raw.txt`.

## Semantics verified (ResearchOverflowTests, 9 tests)

- each node is paid at most once per step; a repeatable gains at most one level per step and the step terminates;
- a node unlocked this step is not paid until the next step (availability stays PREV's);
- the banks stay separate: RP never pays a civic, civic progress equals the CP spent to the milli-CP, CP identity holds;
- with nothing fundable, RP is lost, never banked; the lane variant pays the cheapest available node of the lane only.

## Results

### Polity Age entered (effective turn)

| Seed | Mode | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | OFF | 67 | 339 | 683 | 717 | 783 | 939 | 1074 | 1204 |
| 1 | ON | 67 | 329 | 664 | 697 | 767 | 904 | 1036 | 1139 |
| 1 | ON-LANE | 67 | 325 | 660 | 694 | 758 | 898 | 1026 | 1127 |
| 2 | OFF | 67 | 355 | 728 | 768 | 847 | 1005 | 1156 | 1290 |
| 2 | ON | 67 | 348 | 713 | 753 | 832 | 962 | 1134 | 1246 |
| 2 | ON-LANE | 67 | 344 | 708 | 747 | 825 | 937 | 1112 | 1220 |
| 7 | OFF | 67 | 342 | 692 | 728 | 793 | 919 | 1037 | 1158 |
| 7 | ON | 67 | 334 | 672 | 708 | 769 | 874 | 997 | 1094 |
| 7 | ON-LANE | 67 | 331 | 667 | 702 | 764 | 869 | 989 | 1084 |
| 42 | OFF | 66 | 341 | 693 | 727 | 793 | 924 | 1030 | 1143 |
| 42 | ON | 66 | 333 | 673 | 707 | 774 | 882 | 992 | 1081 |
| 42 | ON-LANE | 66 | 329 | 669 | 705 | 770 | 881 | 978 | 1063 |

### Technology: turn all finite content of an Age is complete

| Seed | Mode | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | OFF | 263 | 476 | 651 | 770 | 898 | 1039 | 1184 | 1342 | 1483 |
| 1 | ON | 255 | 455 | 629 | 746 | 866 | 982 | 1120 | 1277 | 1422 |
| 1 | ON-LANE | 251 | 451 | 625 | 740 | 857 | 972 | 1108 | 1166 | 1184 |
| 2 | OFF | 273 | 501 | 688 | 829 | 963 | 1109 | 1270 | 1429 | 1574 |
| 2 | ON | 265 | 483 | 669 | 807 | 932 | 1042 | 1226 | 1385 | 1530 |
| 2 | ON-LANE | 261 | 478 | 665 | 799 | 920 | 1014 | 1201 | 1263 | 1282 |
| 7 | OFF | 266 | 481 | 658 | 780 | 894 | 1008 | 1138 | 1288 | 1433 |
| 7 | ON | 256 | 461 | 636 | 754 | 858 | 945 | 1075 | 1224 | 1369 |
| 7 | ON-LANE | 253 | 457 | 632 | 747 | 853 | 934 | 1066 | 1119 | 1135 |
| 42 | OFF | 265 | 480 | 659 | 781 | 890 | 1001 | 1123 | 1273 | 1414 |
| 42 | ON | 256 | 460 | 637 | 757 | 859 | 947 | 1063 | 1211 | 1356 |
| 42 | ON-LANE | 252 | 456 | 633 | 751 | 852 | 941 | 1046 | 1095 | 1110 |

### Civics: turn all finite content of an Age is complete

| Seed | Mode | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | OFF | 265 | 464 | 639 | 769 | 911 | 1033 | 1129 | 1336 | 1430 |
| 1 | ON | 265 | 461 | 625 | 753 | 877 | 1003 | 1074 | 1272 | 1364 |
| 1 | ON-LANE | 250 | 459 | 622 | 744 | 871 | 993 | 1063 | 1139 | 1183 |
| 2 | OFF | 287 | 494 | 683 | 834 | 988 | 1121 | 1211 | 1423 | 1515 |
| 2 | ON | 287 | 492 | 673 | 818 | 945 | 1101 | 1174 | 1379 | 1471 |
| 2 | ON-LANE | 270 | 490 | 668 | 811 | 916 | 1080 | 1152 | 1235 | 1281 |
| 7 | OFF | 271 | 471 | 646 | 777 | 902 | 1004 | 1090 | 1291 | 1375 |
| 7 | ON | 271 | 468 | 632 | 756 | 849 | 968 | 1029 | 1228 | 1310 |
| 7 | ON-LANE | 255 | 465 | 628 | 751 | 844 | 960 | 1022 | 1095 | 1134 |
| 42 | OFF | 269 | 470 | 647 | 781 | 901 | 997 | 1074 | 1276 | 1361 |
| 42 | ON | 269 | 467 | 633 | 761 | 852 | 964 | 1026 | 1216 | 1299 |
| 42 | ON-LANE | 253 | 464 | 630 | 757 | 859 | 950 | 1008 | 1072 | 1109 |

### RP lost (share of RP generated while the polity was in each Age; all four seeds pooled)

| Mode | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | All Ages |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| OFF | 3.5% | 7.5% | 11.6% | 8.2% | 19.2% | 24.1% | 22.5% | 25.9% | 72.1% | 54.5% |
| ON | 3.5% | 2.4% | 2.3% | 6.9% | 14.9% | 7.7% | 26.7% | 8.2% | 66.2% | 50.8% |
| ON-LANE | 0.0% | 0.0% | 0.5% | 0.0% | 11.5% | 4.8% | 23.7% | 0.0% | 0.7% | 2.9% |

### End state (turn 1,630) and the economy around research

| Seed | Mode | Age | Tech | Civics | Levels | Population | Structures | Road developments | Institutions | Surveys | Explorations | Known cells |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | OFF | 9 | 525 | 480 | 301 | 47,018,057 | 55 | 671 | 15 | 20 | 13 | 991 |
| 1 | ON | 9 | 525 | 480 | 339 | 48,516,116 | 55 | 654 | 15 | 20 | 13 | 991 |
| 1 | ON-LANE | 9 | 525 | 480 | 429 | 51,283,375 | 55 | 651 | 15 | 20 | 13 | 991 |
| 2 | OFF | 9 | 525 | 480 | 205 | 42,008,153 | 57 | 907 | 15 | 21 | 8 | 885 |
| 2 | ON | 9 | 525 | 480 | 252 | 44,286,927 | 57 | 903 | 15 | 21 | 8 | 885 |
| 2 | ON-LANE | 9 | 525 | 480 | 404 | 48,288,336 | 57 | 881 | 15 | 21 | 8 | 885 |
| 7 | OFF | 9 | 525 | 480 | 338 | 56,179,099 | 55 | 827 | 15 | 20 | 15 | 1195 |
| 7 | ON | 9 | 525 | 480 | 368 | 57,661,133 | 55 | 826 | 15 | 20 | 15 | 1195 |
| 7 | ON-LANE | 9 | 525 | 480 | 446 | 60,257,952 | 55 | 804 | 15 | 20 | 15 | 1195 |
| 42 | OFF | 9 | 525 | 480 | 353 | 69,210,515 | 57 | 736 | 15 | 21 | 19 | 1232 |
| 42 | ON | 9 | 525 | 480 | 381 | 69,951,973 | 57 | 735 | 15 | 21 | 19 | 1232 |
| 42 | ON-LANE | 9 | 525 | 480 | 460 | 73,645,216 | 57 | 731 | 15 | 21 | 19 | 1232 |

### First five completions per tree (node@turn), seed 42

- **OFF** technology: grinding_stone@8 cereal_cultivation@41 dog_domestication@46 sheep_goat@65 knapping_oldowan@73; civics: oral_tradition@9 kin_council@18 gift_exchange@26 marriage_exchange@34 guest_right@42
- **ON** technology: grinding_stone@8 cereal_cultivation@41 dog_domestication@46 sheep_goat@65 knapping_oldowan@73; civics: oral_tradition@9 kin_council@18 gift_exchange@26 marriage_exchange@34 guest_right@42
- **ON-LANE** technology: grinding_stone@8 cereal_cultivation@41 dog_domestication@46 sheep_goat@65 knapping_oldowan@73; civics: oral_tradition@9 kin_council@18 gift_exchange@26 marriage_exchange@34 guest_right@42


## Findings

1. **The waste is real and mostly late.** With the engine as shipped, 54.5 % of all RP generated over a campaign is
   discarded; 72 % in Age 9, where a node costs a fraction of one turn's RP (Age 9 technology pacing scale 0.068) and only
   one node completes per turn.
2. **Route-only overflow barely helps.** It recovers the early and middle Ages (Age 2–3 loss 7.5–11.6 % → 2.3–2.4 %; Age 6
   24 % → 8 %; Age 8 26 % → 8 %) but the overall loss only falls to 50.8 %: once the destination's route has nothing more to
   pay, the remainder is still discarded — in Age 9 the AI's single chosen repeatable takes one level and the rest is lost.
3. **The lane variant removes the waste** (2.9 % overall). What remains is genuine blocked spending: in polity Ages 5 and 7
   the technology tree has turns with NO eligible technology at all (waiting on the Age-sweep cohort, which includes the
   civics tree) — a structural pincer effect, not a leak.
4. **Overflow changes pacing, so it cannot ship without a recalibration.** The pacing scales were calibrated for one node
   per turn. Under the lane variant, Age 8–9 technology is absorbed in ~20 turns (seed 1: technology Age 9 complete at 1,184
   instead of 1,483; civics 1,183 instead of 1,430). Route-only overflow advances the polity's Age entries by 8–10 turns
   (Age 3) to 44–65 turns (Age 9).
5. **The economy around research is unaffected.** Structures, institutions, surveys, explorations and known cells are
   identical in all three modes on every seed; road developments differ by 0–26; population rises 1–15 % with the faster
   research (more effect operands earlier).
6. **The opening is unchanged by overflow** (first five completions identical OFF / ON on seed 42): early RP is too small to
   overflow. Faster early research needs cost changes, which are a separate decision (decision-table.md).

## Recommendation for review (not applied)

- If overflow is adopted, adopt the **lane variant** (route-only leaves the late-game waste in place), and re-run the
  pacing calibration in the same change, because cost now converts to time instead of node count.
- Keep the measurements of OFF as the baseline for the cost pass, so cost changes and overflow are attributed separately.
