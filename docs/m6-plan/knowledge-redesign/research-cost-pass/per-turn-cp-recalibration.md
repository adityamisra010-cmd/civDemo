# Per-turn Civics Points: pacing recalibration and trials (ADR-054; 2026-10-27)

**Why.** The Director's binding rule D-065 R6 ("Every Research Point (RP) and Civics Point (CP) calculation is per turn and
based on average population") makes CP per turn from the RP base function (ADR-054). With equal population, a turn's CP is
now 10 ÷ dtYears times the ADR-043 §3 amount: ×1 in the 10-year band (turns 1–250), ×2 at dt 5 (251–650), ×3.33 at dt 3
(651–950), ×5 at dt 2 (951–1,150), ×10 at dt 1 (1,151–1,270) and ×20 at dt 0.5 (1,271–1,630). Unrecalibrated, civics
would run far ahead of technology. Only the **civics** `pacingScale` is recalibrated, for the shipped content and for the
research-economy candidate. Technology scales are unchanged (the CP rule does not touch the RP lane; RC-1 did not make
technology Age 8 slip past its target, so RC-1a was not triggered).

**Acceptance (D-063 R6, as the task states it).** At a given population the two trees finish each Age within about ±25
turns of each other (the pincer), and every seed completes every Age within 1,630 turns. Seeds 1, 2, 42 and 7, 1,630
turns, `OverlayRun_FourSeeds` (the `ResearchPacingRunner`), the canonical world, one AI Empire.

## 1. Result

| Content | Civics scale A1 … A9 (old → new) | Every seed completes every Age | Pincer within ±25 | Outside ±25 |
|---|---|---|---|---|
| **Shipped** (`research.json`) | 0.645983 → 0.70283, 0.998856 → 1.867397, 0.907987 → 1.970853, 1.306316 → 4.071051, 1.396621 → 4.535408, 1.791944 → 6.21533, 0.757433 → 4.016667, 0.070609 → 0.666521, 0.008686 → 0.172235 | **yes** (seed 2 technology A9 at 1,532; was 1,548) | A1–A6, A8 (−7 … +14) | A7 −47 … −61, A9 −57 … −59 — both present before ADR-054 (−45 … −61, −57 … −59) |
| **Candidate** (`trial/research-economy-candidate.json` → `candidate/research.candidate.json`) | 0.645983 → 0.740297, 0.989987 → 1.979974, 0.715481 → 1.50251, 0.9204 → 3.073267, 1.504901 → 5.35, 1.729102 → 7.0, 1.479361 → 10.218397, 0.044319 → 0.633717, 0.016684 → 0.33368 | **yes** (seed 2 technology A9 at 1,597; was 1,623) | A1, A4, A6–A8 (−19 … +4); A3 +20 … +22 | A2 +26 … +28 (marginal); A5 +36 on seeds 1 and 2 (−6 on 42, 7); A9 −53 … −58 (before ADR-054: A1 up to +25, A5 up to +57, A7 −52, A9 −58) |

Technology scales: unchanged in both. RP lost (four seeds pooled, 1,630 turns): shipped 55.7 % → 56.3 %; candidate
46.2 % → 47.1 %.

## 2. Method

1. **Pass 1 — unit conversion (no fit).** For every civics Age, the scale was multiplied by the CP-weighted mean of
   10 ÷ dtYears over the turns between the completion of the previous civics Age and its own, measured on the ARCHIVED
   pre-ADR-054 runs (shipped: `data/combined` `off-s*`; candidate: `data/candidate-trial` `c-13-s*`), geometric mean of the
   four seeds. Shipped multipliers A1…A9: 1.088, 2.000, 2.069, 3.288, 3.390, 4.281, 5.303, 9.437, 19.829; candidate: 1.146,
   2.000, 2.100, 3.289, 3.466, 4.602, 6.616, 14.299, 20.000.
2. **Passes 2+ — `tools/recalibrate_trial.py --pincer-only`** (added for this change; documented in the tool): technology
   frozen, the target term dropped, each civics scale moved by the full pincer toward the technology completion turn
   (geometric mean over the seeds, `--damp 0.75 --max-step 1.5`). The standard step (half pincer + target) would pull civics
   toward the targets while technology, frozen, cannot follow, and so open the pincer. The frozen technology lane keeps its
   calibrated pace; its target residuals are what they were.
3. **Held scales** (the pacing-recalibration.md §3 precedent): civics A1 after pass 1 (not cost-bound); shipped civics A7 and
   A9 at pass 1 (pass 2's ×1.48 on A7 left the A7 pincer at −43 … −57 and pushed A6 to +36 … +46 — the AI buys the cheaper
   A7 civics before the last A6 civic; ×1.5 on A9 did not move A9 at all, which is throughput-bound like technology A9, §5).
4. **The candidate is bistable in A2–A3.** With civics A2/A3 at 1.98/1.50 (pass 1, c5a) technology runs ~45 turns faster
   through A3–A8 than at 1.67/1.18 (c2–c4) or 1.82/1.33 (c5b); at the lower values seed 2 misses technology A9 within 1,630
   turns (never in c2, c3, c5b; on turn 1,630 in c4). c5a is the only measured candidate setting that completes every Age
   on every seed with slack; its price is the marginal A2 pincer (+26 … +28).

### Passes (pincer range over the four seeds, civics minus technology)

| Pass | Every seed completes | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | seed 2 tech A9 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| shipped pre-ADR-054 | yes | +2…+14 | -12…-7 | -13…-6 | -2…+5 | +8…+19 | -6…+14 | -61…-45 | -6…+3 | -59…-57 | 1548 |
| s1 | yes | +4…+8 | +8…+13 | -7…-2 | +3…+6 | +5…+8 | +27…+37 | -61…-43 | -7…+3 | -59…-53 | 1560 |
| s2 | yes | -2…+2 | +0…+5 | -7…-3 | +0…+3 | +11…+14 | +36…+46 | -57…-43 | -6…+5 | -59…-53 | 1574 |
| **s3 (shipped)** | yes | -2…+2 | +0…+5 | -7…-3 | +0…+3 | +11…+14 | -3…+8 | -61…-47 | -4…+3 | -59…-57 | 1532 |
| candidate pre-ADR-054 | yes | +12…+25 | +0…+3 | -9…+9 | -3…+1 | -7…+57 | +1…+16 | -52…+3 | -10…+2 | -58…-54 | 1623 |
| c1 | yes | +3…+4 | +26…+28 | +20…+21 | -3…+0 | -35…-15 | +4…+36 | -10…-4 | -19…-7 | -58…-53 | 1584 |
| c2 | **no** | +0…+1 | +2…+2 | -8…-6 | +0…+0 | +59…+73 | -6…+38 | -5…-2 | -27…-10 | -59…-53 | never |
| c3 | **no** | +0…+1 | +2…+2 | -8…-6 | -1…+0 | -6…+40 | -4…+17 | -13…-4 | -25…-8 | -59…-53 | never |
| c4 | yes | +0…+1 | +2…+2 | -8…-6 | -1…-1 | -12…+35 | -7…-6 | -19…-9 | -22…-8 | -59…-53 | 1630 |
| **c5a (candidate)** | yes | +3…+4 | +26…+28 | +20…+22 | -1…-1 | -6…+36 | -7…-3 | -17…-6 | -19…-7 | -58…-53 | 1597 |
| c5b | **no** | +7…+10 | +9…+10 | -11…-9 | -2…-1 | -8…+35 | -7…-5 | -19…-8 | -25…-9 | -58…-55 | never |

Scales of every pass: `data/per-turn-cp/scales/`. The pass-1 overlay scales are the pass-0 (pre-ADR-054) scales × the multipliers above.

## 3. Measured tables

#### Shipped, calibrated (pass s3, now in research.json): technology, turn each Age's finite nodes are complete (residual vs the D-063 R6 target)

| Seed | A1 (240) | A2 (510) | A3 (690) | A4 (820) | A5 (970) | A6 (1155) | A7 (1325) | A8 (1460) | A9 (1520) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 265 (+25) | 479 (-31) | 653 (-37) | 775 (-45) | 902 (-68) | 1039 (-116) | 1174 (-151) | 1327 (-133) | 1472 (-48) |
| 2 | 273 (+33) | 501 (-9) | 689 (-1) | 827 (+7) | 956 (-14) | 1081 (-74) | 1231 (-94) | 1387 (-73) | 1532 (+12) |
| 42 | 267 (+27) | 483 (-27) | 662 (-28) | 785 (-35) | 893 (-77) | 1004 (-151) | 1124 (-201) | 1274 (-186) | 1419 (-101) |
| 7 | 267 (+27) | 482 (-28) | 660 (-30) | 782 (-38) | 895 (-75) | 1003 (-152) | 1139 (-186) | 1289 (-171) | 1434 (-86) |
| slowest | +33 | -9 | -1 | +7 | -14 | -74 | -94 | -73 | +12 |

#### Shipped, calibrated (pass s3, now in research.json): civics

| Seed | A1 (240) | A2 (510) | A3 (690) | A4 (820) | A5 (970) | A6 (1155) | A7 (1325) | A8 (1460) | A9 (1520) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 263 (+23) | 479 (-31) | 648 (-42) | 777 (-43) | 916 (-54) | 1036 (-119) | 1118 (-207) | 1327 (-133) | 1413 (-107) |
| 2 | 275 (+35) | 506 (-4) | 686 (-4) | 830 (+10) | 968 (-2) | 1088 (-67) | 1170 (-155) | 1383 (-77) | 1475 (-45) |
| 42 | 265 (+25) | 485 (-25) | 656 (-34) | 786 (-34) | 905 (-65) | 1005 (-150) | 1077 (-248) | 1277 (-183) | 1360 (-160) |
| 7 | 266 (+26) | 485 (-25) | 653 (-37) | 782 (-38) | 906 (-64) | 1011 (-144) | 1087 (-238) | 1292 (-168) | 1376 (-144) |
| slowest | +35 | -4 | -4 | +10 | -2 | -67 | -155 | -77 | -45 |

#### Shipped, calibrated (pass s3, now in research.json): pincer (civics minus technology completion turn)

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | -2 | +0 | -5 | +2 | +14 | -3 | -56 | +0 | -59 |
| 2 | +2 | +5 | -3 | +3 | +12 | +7 | -61 | -4 | -57 |
| 42 | -2 | +2 | -6 | +1 | +12 | +1 | -47 | +3 | -59 |
| 7 | -1 | +3 | -7 | +0 | +11 | +8 | -52 | +3 | -58 |

#### Shipped, before ADR-054 (archived runs `off`, the CR-025 baseline): technology, turn each Age's finite nodes are complete (residual vs the D-063 R6 target)

| Seed | A1 (240) | A2 (510) | A3 (690) | A4 (820) | A5 (970) | A6 (1155) | A7 (1325) | A8 (1460) | A9 (1520) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 263 (+23) | 476 (-34) | 651 (-39) | 770 (-50) | 895 (-75) | 1026 (-129) | 1164 (-161) | 1317 (-143) | 1462 (-58) |
| 2 | 273 (+33) | 501 (-9) | 688 (-2) | 826 (+6) | 956 (-14) | 1089 (-66) | 1244 (-81) | 1403 (-57) | 1548 (+28) |
| 42 | 265 (+25) | 480 (-30) | 659 (-31) | 781 (-39) | 889 (-81) | 996 (-159) | 1114 (-211) | 1264 (-196) | 1409 (-111) |
| 7 | 266 (+26) | 481 (-29) | 658 (-32) | 779 (-41) | 891 (-79) | 1002 (-153) | 1125 (-200) | 1275 (-185) | 1420 (-100) |
| slowest | +33 | -9 | -2 | +6 | -14 | -66 | -81 | -57 | +28 |

#### Shipped, before ADR-054 (archived runs `off`, the CR-025 baseline): civics

| Seed | A1 (240) | A2 (510) | A3 (690) | A4 (820) | A5 (970) | A6 (1155) | A7 (1325) | A8 (1460) | A9 (1520) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 265 (+25) | 464 (-46) | 638 (-52) | 768 (-52) | 906 (-64) | 1020 (-135) | 1108 (-217) | 1317 (-143) | 1405 (-115) |
| 2 | 287 (+47) | 494 (-16) | 682 (-8) | 831 (+11) | 975 (+5) | 1103 (-52) | 1183 (-142) | 1397 (-63) | 1489 (-31) |
| 42 | 269 (+29) | 470 (-40) | 647 (-43) | 780 (-40) | 897 (-73) | 992 (-163) | 1067 (-258) | 1267 (-193) | 1351 (-169) |
| 7 | 271 (+31) | 471 (-39) | 645 (-45) | 777 (-43) | 900 (-70) | 998 (-157) | 1080 (-245) | 1278 (-182) | 1363 (-157) |
| slowest | +47 | -16 | -8 | +11 | +5 | -52 | -142 | -63 | -31 |

#### Shipped, before ADR-054 (archived runs `off`, the CR-025 baseline): pincer (civics minus technology completion turn)

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +2 | -12 | -13 | -2 | +11 | -6 | -56 | +0 | -57 |
| 2 | +14 | -7 | -6 | +5 | +19 | +14 | -61 | -6 | -59 |
| 42 | +4 | -10 | -12 | -1 | +8 | -4 | -47 | +3 | -58 |
| 7 | +5 | -10 | -13 | -2 | +9 | -4 | -45 | +3 | -57 |

#### Candidate, calibrated (pass c5a): technology, turn each Age's finite nodes are complete (residual vs the D-063 R6 target)

| Seed | A1 (240) | A2 (510) | A3 (690) | A4 (820) | A5 (970) | A6 (1155) | A7 (1325) | A8 (1460) | A9 (1520) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 269 (+29) | 481 (-29) | 607 (-83) | 736 (-84) | 884 (-86) | 1077 (-78) | 1226 (-99) | 1387 (-73) | 1532 (+12) |
| 2 | 281 (+41) | 510 (+0) | 645 (-45) | 792 (-28) | 942 (-28) | 1129 (-26) | 1290 (-35) | 1452 (-8) | 1597 (+77) |
| 42 | 271 (+31) | 486 (-24) | 614 (-76) | 745 (-75) | 873 (-97) | 1018 (-137) | 1149 (-176) | 1299 (-161) | 1440 (-80) |
| 7 | 272 (+32) | 486 (-24) | 612 (-78) | 742 (-78) | 875 (-95) | 1029 (-126) | 1170 (-155) | 1326 (-134) | 1467 (-53) |
| slowest | +41 | +0 | -45 | -28 | -28 | -26 | -35 | -8 | +77 |

#### Candidate, calibrated (pass c5a): civics

| Seed | A1 (240) | A2 (510) | A3 (690) | A4 (820) | A5 (970) | A6 (1155) | A7 (1325) | A8 (1460) | A9 (1520) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 272 (+32) | 507 (-3) | 627 (-63) | 735 (-85) | 920 (-50) | 1072 (-83) | 1211 (-114) | 1369 (-91) | 1474 (-46) |
| 2 | 285 (+45) | 538 (+28) | 667 (-23) | 791 (-29) | 978 (+8) | 1122 (-33) | 1273 (-52) | 1433 (-27) | 1540 (+20) |
| 42 | 274 (+34) | 512 (+2) | 635 (-55) | 744 (-76) | 867 (-103) | 1015 (-140) | 1143 (-182) | 1292 (-168) | 1387 (-133) |
| 7 | 275 (+35) | 512 (+2) | 633 (-57) | 741 (-79) | 869 (-101) | 1023 (-132) | 1161 (-164) | 1313 (-147) | 1413 (-107) |
| slowest | +45 | +28 | -23 | -29 | +8 | -33 | -52 | -27 | +20 |

#### Candidate, calibrated (pass c5a): pincer (civics minus technology completion turn)

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +3 | +26 | +20 | -1 | +36 | -5 | -15 | -18 | -58 |
| 2 | +4 | +28 | +22 | -1 | +36 | -7 | -17 | -19 | -57 |
| 42 | +3 | +26 | +21 | -1 | -6 | -3 | -6 | -7 | -53 |
| 7 | +3 | +26 | +21 | -1 | -6 | -6 | -9 | -13 | -54 |

#### Candidate, before ADR-054 (archived runs `c-13`, candidate-trial.md): technology, turn each Age's finite nodes are complete (residual vs the D-063 R6 target)

| Seed | A1 (240) | A2 (510) | A3 (690) | A4 (820) | A5 (970) | A6 (1155) | A7 (1325) | A8 (1460) | A9 (1520) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 269 (+29) | 480 (-30) | 649 (-41) | 758 (-62) | 905 (-65) | 1086 (-69) | 1238 (-87) | 1400 (-60) | 1541 (+21) |
| 2 | 280 (+40) | 510 (+0) | 693 (+3) | 821 (+1) | 970 (+0) | 1157 (+2) | 1320 (-5) | 1482 (+22) | 1623 (+103) |
| 42 | 271 (+31) | 485 (-25) | 657 (-33) | 770 (-50) | 902 (-68) | 1054 (-101) | 1178 (-147) | 1329 (-131) | 1474 (-46) |
| 7 | 271 (+31) | 485 (-25) | 655 (-35) | 766 (-54) | 900 (-70) | 1060 (-95) | 1194 (-131) | 1350 (-110) | 1495 (-25) |
| slowest | +40 | +0 | +3 | +1 | +0 | +2 | -5 | +22 | +103 |

#### Candidate, before ADR-054 (archived runs `c-13`, candidate-trial.md): civics

| Seed | A1 (240) | A2 (510) | A3 (690) | A4 (820) | A5 (970) | A6 (1155) | A7 (1325) | A8 (1460) | A9 (1520) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 281 (+41) | 480 (-30) | 640 (-50) | 757 (-63) | 898 (-72) | 1102 (-53) | 1186 (-139) | 1390 (-70) | 1486 (-34) |
| 2 | 305 (+65) | 513 (+3) | 702 (+12) | 821 (+1) | 1027 (+57) | 1158 (+3) | 1323 (-2) | 1475 (+15) | 1569 (+49) |
| 42 | 286 (+46) | 486 (-24) | 648 (-42) | 771 (-49) | 899 (-71) | 1062 (-93) | 1137 (-188) | 1331 (-129) | 1416 (-104) |
| 7 | 287 (+47) | 486 (-24) | 646 (-44) | 763 (-57) | 903 (-67) | 1071 (-84) | 1148 (-177) | 1346 (-114) | 1438 (-82) |
| slowest | +65 | +3 | +12 | +1 | +57 | +3 | -2 | +15 | +49 |

#### Candidate, before ADR-054 (archived runs `c-13`, candidate-trial.md): pincer (civics minus technology completion turn)

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +12 | +0 | -9 | -1 | -7 | +16 | -52 | -10 | -55 |
| 2 | +25 | +3 | +9 | +0 | +57 | +1 | +3 | -7 | -54 |
| 42 | +15 | +1 | -9 | +1 | -3 | +8 | -41 | +2 | -58 |
| 7 | +16 | +1 | -9 | -3 | +3 | +11 | -46 | -4 | -57 |

## 4. Polity Age entered (effective turn)

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Shipped before → s3: 1 | 67 → 67 | 339 → 341 | 683 → 686 | 716 → 720 | 783 → 790 | 934 → 943 | 1060 → 1071 | 1184 → 1194 |
| 2 | 67 → 67 | 355 → 355 | 728 → 729 | 767 → 767 | 844 → 843 | 992 → 985 | 1136 → 1122 | 1264 → 1251 |
| 42 | 66 → 66 | 341 → 343 | 693 → 696 | 727 → 731 | 793 → 798 | 920 → 928 | 1023 → 1033 | 1134 → 1140 |
| 7 | 67 → 67 | 342 → 343 | 691 → 694 | 723 → 726 | 792 → 795 | 918 → 922 | 1031 → 1040 | 1145 → 1159 |
| Candidate before → c5a: 1 | 74 → 74 | 348 → 349 | 571 → 630 | 705 → 683 | 788 → 765 | 941 → 952 | 1126 → 1116 | 1261 → 1248 |
| 2 | 75 → 75 | 367 → 368 | 608 → 670 | 761 → 732 | 851 → 822 | 1048 → 1003 | 1198 → 1170 | 1343 → 1313 |
| 42 | 74 → 74 | 351 → 352 | 578 → 638 | 716 → 691 | 801 → 773 | 934 → 892 | 1088 → 1053 | 1197 → 1165 |
| 7 | 74 → 74 | 352 → 353 | 576 → 636 | 712 → 689 | 795 → 770 | 933 → 895 | 1095 → 1065 | 1213 → 1189 |

- **Shipped:** every Age is entered within −14 … +14 turns of before.
- **Candidate:** Age 4 is entered 59–62 turns LATER (the dearer civics A2–A3, which c5a needs for the technology pace in
  A3–A8, slow Age IV's civic milestones), and Ages 5–9 21–45 turns EARLIER. Reported, not compensated.

## 5. Technology Age 9 throughput (D4): the bottleneck, measured

Re-measured on this change's runs (shipped s2) and on the archived candidate runs (`c-13`); the same in both.

| Run, seed | Technology A8 done | A9 done | Turns | Finite A9 nodes left at A8 | Turns with no finite A9 completion | Most finite A9 a turn | RP lost in the window |
|---|---:|---:|---:|---:|---:|---:|---:|
| shipped s2, 1 | 1371 | 1512 | 141 | 76 | 65 | 1 | 85.0 % |
| shipped s2, 2 | 1429 | 1574 | 145 | 76 | 69 | 1 | 84.2 % |
| shipped s2, 42 | 1306 | 1451 | 145 | 76 | 69 | 1 | 86.7 % |
| shipped s2, 7 | 1328 | 1473 | 145 | 76 | 69 | 1 | 86.0 % |
| candidate c-13, 1 | 1400 | 1541 | 141 | 76 | 65 | 1 | 85.1 % |
| candidate c-13, 2 | 1482 | 1623 | 141 | 76 | 65 | 1 | 84.4 % |
| candidate c-13, 42 | 1329 | 1474 | 145 | 76 | 69 | 1 | 86.7 % |
| candidate c-13, 7 | 1350 | 1495 | 145 | 76 | 69 | 1 | 86.1 % |

**What binds.**
1. **One finite node a step.** The lane never completes more than one finite A9 technology in a step, on any seed, with
   route overflow on (candidate) or off (shipped). With no goal left, the AI's destination is the cheapest AVAILABLE node
   (`AiResearchPolicy.ChooseDestination` → `Cheapest`); an available destination is its own route, so ADR-050's route
   overflow stops at it and the rest of the step's RP is lost (84–87 % of the window's RP).
2. **Repeatable levels take ~half the turns.** Once the first final-Age repeatable opens, the cheapest available node is
   often a repeatable LEVEL (a few % of a turn's RP): on 65–69 of the 141–145 turns no finite A9 node completes.

**What does not bind (measured):** RP (84–87 % lost); cost (pacing-recalibration.md §3: scales 0.025–0.068 did not move the
finish); the Age sweep (the A7 cohort is done long before); prerequisite depth (the longest chain inside the 140 finite A9
technologies is 9; 2–6 are available at any time after Age 8).

**Model** (`tools/a9_throughput_model.py`: the run's own pools and completion turns, the content's prerequisites; partial progress
carried): one finite node a turn and no repeatable interleaving → A9 done 77–79 turns after Age 8; every affordable
available node a turn → 22–30 turns (s2, s3 and c-13 alike). The 60-turn target window (A8 1,460 → A9 1,520) is reachable only by the second.

**Proposed corrections (NOT implemented — new AI behaviour needs its own ADR and switch, the ADR-052/053 precedent; queue):**
(a) the technology lane prefers finite nodes over repeatable levels while its finite tree is unfinished (modelled
+77–79 turns); (b) an end-game destination whose route covers the remaining available finite nodes (the deepest
remaining node), so the existing route overflow pays every affordable one in a step (modelled +22–30). Neither is a cost
cut or a lane-wide overflow.

## 6. RC-1 (aircraft A9 → A8): E1 and E2 on the calibrated configurations

| Configuration, seed | glider | aircraft | A9 entered | aircraft − A9 entry |
|---|---:|---:|---:|---:|
| shipped before (A9 tag), 1 / 2 / 42 / 7 | 1214 / 1294 / 1164 / 1175 | 1215 / 1295 / 1165 / 1176 | 1184 / 1264 / 1134 / 1145 | +31 / +31 / +31 / +31 |
| shipped s3 (A8 tag), 1 / 2 / 42 / 7 | 1224 / 1281 / 1174 / 1189 | 1300 / 1358 / 1224 / 1262 | 1194 / 1251 / 1140 / 1159 | +106 / +107 / +84 / +103 |
| candidate before, 1 / 2 / 42 / 7 | 1291 / 1373 / 1227 / 1243 | 1292 / 1374 / 1228 / 1244 | 1261 / 1343 / 1197 / 1213 | +31 / +31 / +31 / +31 |
| candidate c5a, 1 / 2 / 42 / 7 | 1278 / 1343 / 1190 / 1219 | 1355 / 1420 / 1272 / 1269 | 1248 / 1313 / 1165 / 1189 | +107 / +107 / +107 / +80 |

- **E1 (Age 8):** technology A8 is complete before its target on every seed in both configurations (shipped slowest −73,
  candidate −8); the A8 pincer is −4 … +3 (shipped) and −19 … −7 (candidate). RC-1a (an A8 recalibration) is not needed.
- **E2 (aircraft):** the A8 tag removes the sweep wait, but aircraft now completes 80–107 turns AFTER Age 9 entry (before:
  31). At the A8 scale its effective cost is 33,450 × 0.775796 = 25,950 RP (shipped) and × 0.926249 = 30,983 RP
  (candidate), against 66,900 × 0.068141 = 4,559 RP at the A9 scale — 5.7–6.8× dearer — and no AI goal asks for it, so
  the AI's cheapest-first order defers it 50–82 turns past `glider`. RC-1's aim (Flight I in late A8) is NOT met by the
  retag alone. Options and their measurement: ADR-054 §8 (a Director decision).
- **Measured option (B, shipped s3 + `data/per-turn-cp/scales/aircraft-m25.json`, aircraft magnitude 8.5 → 6.0, content
  5,910):** aircraft one turn after `glider` (1225 / 1282 / 1175 / 1190), +31 … +35 after A9 entry — the pre-RC-1 timing;
  technology A8 and A9 within ±1 turn of s3; pincer unchanged. `glider` then binds. Recorded for the Director, not applied.

## 7. Determinism and order invariants (final configurations)

| Configuration | Seed | Reconstruction mismatches | Replay | Save/load at turn 815 | Final hash | CP identity |
|---|---|---:|---|---|---|---|
| shipped s3 (`s3r`) | 1 / 2 / 42 / 7 | 0 / 0 / 0 / 0 | EQUAL ×4 | equal ×4 | `f2e55291…` / `c3abf7eb…` / `2cc7c986…` / `60828e40…` (= the s3 run) | holds ×4 |
| candidate c5a (`c5ar`) | 1 / 2 / 42 / 7 | 0 / 0 / 0 / 0 | EQUAL ×4 | equal ×4 | `6dcf47be…` / `57befa74…` / `cb233c24…` / `08d508fd…` (= the c5a run) | holds ×4 |

`tools/check_research_order.py` (prerequisites and Age-sweep cohorts against every completion turn): **HOLD** on both.

## 8. Limits, residuals and what was not done

- **Not cost-reachable pincer residuals** (present before ADR-054): shipped A7 (−47 … −61: civics A7 is order-bound to A6,
  pacing-recalibration.md §3) and A9 (−57 … −59); candidate A9 (−53 … −58) — both A9s throughput-bound (§5). Candidate A5
  is seed-split (+36 on seeds 1, 2; −6 on 42, 7; before: −7 … +57) and A2 is marginal (+26 … +28), the price of the A2–A3
  setting that keeps seed 2's technology A9 inside 1,630 turns (§2.4).
- **Technology targets** were not re-fitted (technology frozen): their residuals are the pre-ADR-054 calibration's.
- **Pass 0** (ADR-054 on, the old scale) was not run: pass 1 converts the scale by the measured dt mix instead, so the size of
  the unrecalibrated drift is computed (the multipliers in §2), not measured.
- **Other overlays** (`trial/research-economy-trial.json`, `trial/civics-gate-trial.json`, the eight batch overlays) keep the
  pre-ADR-054 civics scales: they are archived trials and were not re-run; only the CANDIDATE overlay is recalibrated.
- The runs used a test build with the shipped content plus the scale overlays (the runner's mechanism); the shipped
  `research.json` and `candidate/research.candidate.json` carry the same scales (pinned by `ResearchCandidateTests`).
