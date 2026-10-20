# M5 — Next-turn forecast comparison under Decision C (automated, 2026-10-20)

**Status:** evidence record for the Director's review (R7, R9 of the 2026-10-20 rulings). Automated, headless; the human
runs of checklist 21 and 22 remain the Director's. Produced on the Decision C tree (the commit this file ships in).

## 1. What was run
- `UiSession.Start(42, aiEmpiresOverride: 1)` — the shipped launcher configuration (seed 42, one AI rival), stepped 12 turns
  headlessly through the same `UiSession` the game uses. A granary order was issued at the capital on turn 1.
- Before each End Turn: `EmpireStores.Build(... next: PreviewNextWorld())` — the strip groups, the table's columns (Empire
  total, Change, Set aside, **Next turn (expected weather)**) and the hover-card lines, exactly as the UI formats them.
- After End Turn: the player's actual stores of every good (the sum of the player's own GoodStocks rows, the test suite's
  independent oracle), compared with the figure shown before.

## 2. Result
| Rows | Exact (actual = forecast) | Differ | Of which non-food goods |
|---:|---:|---:|---:|
| 100 | 76 | 24 | **0** |

- **Every non-food good matched exactly on every turn** (timber, stone, clay, copper-ore, tin-ore, hides: 66 of 66 rows).
- **Only the weather-carrying food goods differ** — livestock and fish on every turn from turn 1, grain on turns 6 and 9 —
  and they differ in both directions (a good year leaves more than forecast, a poor year less). That is Decision C
  working as ruled: the forecast assumes an ordinary harvest (multiplier 1.0) and never reads the pending draw; End Turn
  resolves with the actual draw. Grain usually matches because spoilage caps grain at storage capacity in this early
  world, so a better-than-expected harvest shows in the produced units, not in the stock.
- Turn 0's figure (grain 34,714 → 2,467 next turn) is the founding endowment settling to storage capacity on the first
  step; the forecast showed the capped figure and End Turn produced exactly it.
- The live world is untouched by the preview (pinned by `ThePreview_ChangesNoWorld_NoOrder_AndNoHash`); the 60-turn CLI
  hash is unchanged (`827d9b00…3878a0`).

## 3. The comparison, every row
| Turn | Good | Table total | Change | Set aside | Next turn (expected weather) | Actual after End Turn | Actual − forecast |
|---|---|---:|---:|---:|---:|---:|---:|
| 0 | grain | 34714 | - | 0 | 2467 | 2467 | 0 |
| 1 | grain | 2467 | -32247 | 0 | 2453 | 2453 | 0 |
| 1 | livestock | 1453 | 1453 | 0 | 2935 | 2842 | -93 |
| 1 | fish | 1193 | 1193 | 0 | 2407 | 2336 | -71 |
| 1 | timber | 689 | 689 | 0 | 1459 | 1459 | 0 |
| 1 | stone | 10 | 10 | 0 | 2 | 2 | 0 |
| 1 | clay | 771 | 771 | 0 | 1552 | 1552 | 0 |
| 1 | copper-ore | 8 | 8 | 0 | 20 | 20 | 0 |
| 1 | tin-ore | 4 | 4 | 0 | 12 | 12 | 0 |
| 1 | hides | 1022 | 1022 | 0 | 2058 | 2058 | 0 |
| 2 | grain | 2453 | -14 | 0 | 2561 | 2561 | 0 |
| 2 | livestock | 2842 | 1389 | 0 | 4404 | 4664 | 260 |
| 2 | fish | 2336 | 1143 | 0 | 3616 | 3809 | 193 |
| 2 | timber | 1459 | 770 | 0 | 2288 | 2288 | 0 |
| 2 | stone | 2 | -8 | 0 | 15 | 15 | 0 |
| 2 | clay | 1552 | 781 | 0 | 2374 | 2374 | 0 |
| 2 | copper-ore | 20 | 12 | 0 | 30 | 30 | 0 |
| 2 | tin-ore | 12 | 8 | 0 | 22 | 22 | 0 |
| 2 | hides | 2058 | 1036 | 0 | 3146 | 3146 | 0 |
| 3 | grain | 2561 | 108 | 0 | 2660 | 2660 | 0 |
| 3 | livestock | 4664 | 1822 | 0 | 6292 | 6803 | 511 |
| 3 | fish | 3809 | 1473 | 0 | 5143 | 5532 | 389 |
| 3 | timber | 2288 | 829 | 0 | 3138 | 3138 | 0 |
| 3 | stone | 15 | 13 | 0 | 28 | 28 | 0 |
| 3 | clay | 2374 | 822 | 0 | 3230 | 3230 | 0 |
| 3 | copper-ore | 30 | 10 | 0 | 41 | 41 | 0 |
| 3 | tin-ore | 22 | 10 | 0 | 30 | 30 | 0 |
| 3 | hides | 3146 | 1088 | 0 | 4279 | 4279 | 0 |
| 4 | grain | 2660 | 99 | 0 | 2740 | 2740 | 0 |
| 4 | livestock | 6803 | 2139 | 0 | 8479 | 8182 | -297 |
| 4 | fish | 5532 | 1723 | 0 | 6903 | 6684 | -219 |
| 4 | timber | 3138 | 850 | 0 | 4026 | 4026 | 0 |
| 4 | stone | 28 | 13 | 0 | 41 | 41 | 0 |
| 4 | clay | 3230 | 856 | 0 | 4110 | 4110 | 0 |
| 4 | copper-ore | 41 | 11 | 0 | 51 | 51 | 0 |
| 4 | tin-ore | 30 | 8 | 0 | 39 | 39 | 0 |
| 4 | hides | 4279 | 1133 | 0 | 5446 | 5446 | 0 |
| 5 | grain | 2740 | 80 | 0 | 2788 | 2788 | 0 |
| 5 | livestock | 8182 | 1379 | 0 | 9882 | 10274 | 392 |
| 5 | fish | 6684 | 1152 | 0 | 8078 | 8370 | 292 |
| 5 | timber | 4026 | 888 | 0 | 4939 | 4939 | 0 |
| 5 | stone | 41 | 13 | 0 | 55 | 55 | 0 |
| 5 | clay | 4110 | 880 | 0 | 5006 | 5006 | 0 |
| 5 | copper-ore | 51 | 10 | 0 | 62 | 62 | 0 |
| 5 | tin-ore | 39 | 9 | 0 | 51 | 51 | 0 |
| 5 | hides | 5446 | 1167 | 0 | 6634 | 6634 | 0 |
| 6 | grain | 2788 | 48 | 0 | 2821 | 2783 | -38 |
| 6 | livestock | 10274 | 2092 | 0 | 11975 | 11263 | -712 |
| 6 | fish | 8370 | 1686 | 0 | 9765 | 9228 | -537 |
| 6 | timber | 4939 | 913 | 0 | 5865 | 5865 | 0 |
| 6 | stone | 55 | 14 | 0 | 68 | 68 | 0 |
| 6 | clay | 5006 | 896 | 0 | 5917 | 5917 | 0 |
| 6 | copper-ore | 62 | 11 | 0 | 71 | 71 | 0 |
| 6 | tin-ore | 51 | 12 | 0 | 59 | 59 | 0 |
| 6 | hides | 6634 | 1188 | 0 | 7839 | 7839 | 0 |
| 7 | grain | 2783 | -5 | 0 | 2829 | 2829 | 0 |
| 7 | livestock | 11263 | 989 | 0 | 12957 | 12949 | -8 |
| 7 | fish | 9228 | 858 | 0 | 10620 | 10616 | -4 |
| 7 | timber | 5865 | 926 | 0 | 6803 | 6803 | 0 |
| 7 | stone | 68 | 13 | 0 | 80 | 80 | 0 |
| 7 | clay | 5917 | 911 | 0 | 6830 | 6830 | 0 |
| 7 | copper-ore | 71 | 9 | 0 | 80 | 80 | 0 |
| 7 | tin-ore | 59 | 8 | 0 | 70 | 70 | 0 |
| 7 | hides | 7839 | 1205 | 0 | 9050 | 9050 | 0 |
| 8 | grain | 2829 | 46 | 0 | 2877 | 2877 | 0 |
| 8 | livestock | 12949 | 1686 | 0 | 14669 | 14212 | -457 |
| 8 | fish | 10616 | 1388 | 0 | 12028 | 11678 | -350 |
| 8 | timber | 6803 | 938 | 0 | 7749 | 7749 | 0 |
| 8 | stone | 80 | 12 | 0 | 94 | 94 | 0 |
| 8 | clay | 6830 | 913 | 0 | 7756 | 7756 | 0 |
| 8 | copper-ore | 80 | 9 | 0 | 91 | 91 | 0 |
| 8 | tin-ore | 70 | 11 | 0 | 81 | 81 | 0 |
| 8 | hides | 9050 | 1211 | 0 | 10277 | 10277 | 0 |
| 9 | grain | 2877 | 48 | 0 | 2898 | 2671 | -227 |
| 9 | livestock | 14212 | 1263 | 0 | 15943 | 15058 | -885 |
| 9 | fish | 11678 | 1062 | 0 | 13100 | 12432 | -668 |
| 9 | timber | 7749 | 946 | 0 | 8708 | 8708 | 0 |
| 9 | stone | 94 | 14 | 0 | 106 | 106 | 0 |
| 9 | clay | 7756 | 926 | 0 | 8695 | 8695 | 0 |
| 9 | copper-ore | 91 | 11 | 0 | 101 | 101 | 0 |
| 9 | tin-ore | 81 | 11 | 0 | 91 | 91 | 0 |
| 9 | hides | 10277 | 1227 | 0 | 11520 | 11520 | 0 |
| 10 | grain | 2671 | -206 | 0 | 2937 | 2937 | 0 |
| 10 | livestock | 15058 | 846 | 0 | 16808 | 17407 | 599 |
| 10 | fish | 12432 | 754 | 0 | 13873 | 14324 | 451 |
| 10 | timber | 8708 | 959 | 0 | 9685 | 9685 | 0 |
| 10 | stone | 106 | 12 | 0 | 121 | 121 | 0 |
| 10 | clay | 8695 | 939 | 0 | 9647 | 9647 | 0 |
| 10 | copper-ore | 101 | 10 | 0 | 110 | 110 | 0 |
| 10 | tin-ore | 91 | 10 | 0 | 102 | 102 | 0 |
| 10 | hides | 11520 | 1243 | 0 | 12782 | 12782 | 0 |
| 11 | grain | 2937 | 266 | 0 | 2972 | 2972 | 0 |
| 11 | livestock | 17407 | 2349 | 0 | 19161 | 19311 | 150 |
| 11 | fish | 14324 | 1892 | 0 | 15767 | 15881 | 114 |
| 11 | timber | 9685 | 977 | 0 | 10662 | 10662 | 0 |
| 11 | stone | 121 | 15 | 0 | 135 | 135 | 0 |
| 11 | clay | 9647 | 952 | 0 | 10605 | 10605 | 0 |
| 11 | copper-ore | 110 | 9 | 0 | 119 | 119 | 0 |
| 11 | tin-ore | 102 | 11 | 0 | 113 | 113 | 0 |
| 11 | hides | 12782 | 1262 | 0 | 14052 | 14052 | 0 |

## 4. Strip, table and card figures (checklist 21 cross-check, automated)
The strip value is the group total; the card's first line repeats it with the change sentence; each good line carries the
table's Empire total, Change, Set aside (when any) and the Next-turn figure. Turns 0, 1, 2 and 11 below.

```
== turn 0
strip Food = 34,714 (-)
  Your empire holds 34,714. No turn played yet: nothing to compare with.
  grain: 34,714; 2,467 next turn (expected weather); most at Bigen 7,813, Zareshu 7,471, Gisabul 5,604 and 3 others
== turn 1
strip Food = 5,113 (-29,601)
  Your empire holds 5,113. 29,601 fewer than at the start of last turn.
  grain: 2,467 (-32,247 since last turn); 2,453 next turn (expected weather); most at Bigen 506, Zareshu 505, Shekasur 471 and 3 others
  livestock: 1,453 (+1,453 since last turn); 2,935 next turn (expected weather); most at Zareshu 298, Bigen 294, Shekasur 271 and 3 others
  fish: 1,193 (+1,193 since last turn); 2,407 next turn (expected weather); most at Zareshu 257, Bigen 236, Shekasur 229 and 3 others
strip Materials = 2,504 (+2,504)
  Your empire holds 2,504. 2,504 more than at the start of last turn.
  timber: 689 (+689 since last turn); 1,459 next turn (expected weather); most at Zareshu 148, Bigen 146, Shekasur 125 and 3 others
  stone: 10 (+10 since last turn); 2 next turn (expected weather); most at Shekasur 5, Libur 3, Gisabul 1 and one other
  clay: 771 (+771 since last turn); 1,552 next turn (expected weather); most at Zareshu 168, Bigen 159, Shekasur 145 and 3 others
  copper-ore: 8 (+8 since last turn); 20 next turn (expected weather); most at Libur 8
  tin-ore: 4 (+4 since last turn); 12 next turn (expected weather); most at Gisabul 2, Pishemul 1, Shekasur 1
  hides: 1,022 (+1,022 since last turn); 2,058 next turn (expected weather); most at Zareshu 216, Bigen 214, Shekasur 189 and 3 others
== turn 2
strip Food = 7,631 (+2,518)
  Your empire holds 7,631. 2,518 more than at the start of last turn.
  grain: 2,453 (-14 since last turn); 2,561 next turn (expected weather); most at Bigen 513, Zareshu 488, Shekasur 463 and 3 others
  livestock: 2,842 (+1,389 since last turn); 4,404 next turn (expected weather); most at Shekasur 611, Libur 510, Zareshu 489 and 3 others
  fish: 2,336 (+1,143 since last turn); 3,616 next turn (expected weather); most at Shekasur 510, Zareshu 429, Libur 403 and 3 others
strip Materials = 5,103 (+2,599)
  Your empire holds 5,103. 2,599 more than at the start of last turn.
  timber: 1,459 (+770 since last turn); 2,288 next turn (expected weather); most at Zareshu 316, Bigen 278, Shekasur 276 and 3 others
  stone: 2 (-8 since last turn); 15 next turn (expected weather); most at Pishemul 2
  clay: 1,552 (+781 since last turn); 2,374 next turn (expected weather); most at Zareshu 334, Bigen 323, Shekasur 292 and 3 others
  copper-ore: 20 (+12 since last turn); 30 next turn (expected weather); most at Libur 17, Bigen 1, Zareshu 1 and one other
  tin-ore: 12 (+8 since last turn); 22 next turn (expected weather); most at Gisabul 4, Pishemul 3, Shekasur 3 and 2 others
  hides: 2,058 (+1,036 since last turn); 3,146 next turn (expected weather); most at Bigen 434, Zareshu 430, Shekasur 382 and 3 others
== turn 11
strip Food = 34,668 (+4,507)
  Your empire holds 34,668. 4,507 more than at the start of last turn.
  grain: 2,937 (+266 since last turn); 2,972 next turn (expected weather); most at Zareshu 619, Bigen 610, Gisabul 580 and 3 others
  livestock: 17,407 (+2,349 since last turn); 19,161 next turn (expected weather); most at Zareshu 3,612, Bigen 3,365, Shekasur 3,019 and 3 others
  fish: 14,324 (+1,892 since last turn); 15,767 next turn (expected weather); most at Zareshu 3,129, Bigen 2,722, Shekasur 2,529 and 3 others
strip Materials = 32,447 (+3,226)
  Your empire holds 32,447. 3,226 more than at the start of last turn.
  timber: 9,685 (+977 since last turn); 10,662 next turn (expected weather); most at Zareshu 2,170, Bigen 2,029, Gisabul 1,685 and 3 others
  stone: 121 (+15 since last turn); 135 next turn (expected weather); most at Shekasur 52, Libur 22, Gisabul 21 and 3 others
  clay: 9,647 (+952 since last turn); 10,605 next turn (expected weather); most at Zareshu 2,172, Bigen 1,991, Gisabul 1,683 and 3 others
  copper-ore: 110 (+9 since last turn); 119 next turn (expected weather); most at Libur 82, Shekasur 9, Bigen 8 and 3 others
  tin-ore: 102 (+11 since last turn); 113 next turn (expected weather); most at Gisabul 36, Pishemul 25, Shekasur 21 and 3 others
  hides: 12,782 (+1,262 since last turn); 14,052 next turn (expected weather); most at Zareshu 2,796, Bigen 2,674, Gisabul 2,239 and 3 others
```

## 5. What this does not prove
- It is not the human check of checklist 21 or 22 (the Director plays those on the Decision C build).
- It does not evaluate the forecast's accuracy for food goods — by ruling, the forecast is the expectation, not a prediction
  of the draw.
