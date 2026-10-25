# Early-game trial — six enablers cheaper (D-064 PROPOSAL; research.json NOT changed)

`OverflowComparisonMeasurement.EarlyGameProposal_FourSeeds` (skipped; run explicitly): the shipped content with the six
EARLY-ENABLER nodes of `decision-table.md` at magnitude −0.5 (breadth first, then novelty; cost ×0.71), overflow OFF, the
canonical world with one AI Empire, seeds 1, 2, 42, 7, 700 turns, against the unchanged content in the same run.
Raw output: `data/early-trial-raw.txt`.

## Completion turn of each proposed node (baseline → proposal)

| Node | Seed 1 | Seed 2 | Seed 42 | Seed 7 |
|---|---|---|---|---|
| kin_council | 19 → 8 | 19 → 8 | 18 → 7 | 19 → 8 |
| cereal_cultivation | 41 → 32 | 41 → 32 | 41 → 32 | 41 → 32 |
| ground_stone_early | 81 → 72 | 82 → 72 | 80 → 71 | 82 → 72 |
| cordage | 95 → 82 | 97 → 83 | 94 → 81 | 96 → 83 |
| fire_making | 173 → 162 | 179 → 167 | 174 → 161 | 175 → 163 |
| pottery_open_fired | 219 → 201 | 227 → 209 | 221 → 202 | 222 → 203 |

## Opening milestones (baseline → proposal)

| Measure | Seed 1 | Seed 2 | Seed 42 | Seed 7 |
|---|---|---|---|---|
| Polity Age 2 entered | 67 → 59 | 67 → 59 | 66 → 58 | 67 → 59 |
| Polity Age 3 entered | 339 → 325 | 355 → 342 | 341 → 327 | 342 → 329 |
| Polity Age 4 entered | 683 → 672 | - → - | 693 → 680 | 692 → 680 |
| Technology Age 1 complete | 263 → 249 | 273 → 258 | 265 → 250 | 266 → 251 |
| Technology Age 2 complete | 476 → 464 | 501 → 490 | 480 → 468 | 481 → 469 |
| Technology Age 3 complete | 651 → 640 | 688 → 678 | 659 → 648 | 658 → 648 |
| Civics Age 1 complete | 265 → 248 | 287 → 267 | 269 → 250 | 271 → 251 |
| Civics Age 2 complete | 464 → 456 | 494 → 487 | 470 → 461 | 471 → 462 |
| Civics Age 3 complete | 639 → 629 | 683 → 667 | 647 → 637 | 646 → 636 |

## Reading

- All six nodes complete sooner on every seed: the civics root kin_council 10–11 turns sooner (turn ~19 → ~8), cereal
  cultivation 9 turns sooner (41 → 32), the four technology enablers 9–20 turns sooner.
- The polity enters Age 2 seven to eight turns sooner, Age 3 13–14 turns sooner and Age 4 11–13 turns sooner (where
  reached by turn 700): the opening gain carries forward as a fixed shift, it does not compound into the later Ages.
- Everything else in Ages 1–2 keeps its price, so the rest of the opening still costs what it did and the order of
  choice is unchanged; only these six high-fan-out roots get cheaper.
- Measured with overflow OFF so the effect is the costs' alone; the overflow evidence is in `overflow-comparison.md`.
