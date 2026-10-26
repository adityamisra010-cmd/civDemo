# Cost batches against the calibrated trial baseline (2026-10-26; MEASURED, NOT ADOPTED)

**Director ruling 2026-10-26 (later):** "Eight cost batches — continue measurement, not default adoption. Each batch must be
evaluated independently against the calibrated baseline." Flag any change that makes a milestone LATER although it made it
cheaper, or that moves a large amount of waste into a later Age. "The six early cuts should not receive automatic approval
simply because they were first in the sequence."

**What was run.** Each batch is a node overlay (`trial/batches/*.json`, from `decision-table.csv` by
`tools/cost_batch_overlays.py`). It was applied on top of the calibrated trial overlay (pass 8, `pacing-recalibration.md`) and
run alone: seeds 1, 2, 42 and 7, 1,630 turns, one AI Empire.
- **Baseline:** pass 8 itself. Every number below is batch minus baseline.
- **Unchanged:** `research.json` and the shipped defaults.
- **Not batched:** 35 decision-table rows are held — FUTURE-DEPENDENT, EFFECT-DEFECT, PLACEHOLDER-ONLY, and any node in CR-025 or
  the 17-node audit.
- **Integrity:** all 32 runs had 0 reconstruction mismatches.
- **Regenerate:** `tools/research_economy_report.py batch|batchdetail <dir> p8 …`. Data: `data/batches/`.

## 1. Verdicts

| Batch | Nodes | Direction | Age 9 entered (mean shift) | RP lost | Flags | Verdict |
|---|---:|---|---:|---|---|---|
| b0 early six | 6 | −0.5 (×0.71) | **+3.5 (later)** | 46.3 → 46.6 % | **cheaper-but-later on all four seeds**: `fire_making` +37…+38; `a3_writing` +36…+37; 21 research milestones later on seeds 1, 42, 7 (up to +25) | **Do not adopt.** The cuts make the run SLOWER. |
| b1 under-priced, Ages 3–5 | 7 | +0.25 / +0.5 | +0.2 | 46.3 → 46.3 % | none | Pacing-neutral. Eligible on pricing grounds (power vs price). |
| b2 under-priced technologies, Ages 6–9 | 13 | +0.25 / +0.5 | +3.5 | 46.3 → 46.0 % | none | Eligible. A small, expected slowdown with slightly less waste. |
| b3 under-priced civics, Ages 6–9 | 9 | +0.25 / +0.5 | +0.0 | 46.3 → 46.3 % | none | Pacing-neutral. Eligible on pricing grounds. Civics A4–A6 is +13 on the slowest seed. |
| b4 low-value, Ages 1–4 | 19 | −0.25 / −0.5 | −1.2 | 46.3 → 46.2 % | cheaper-but-later, seed 1 only: 6 nodes +2…+5 (`regalia` also +2…+3 on seeds 42 and 7); 13 milestones later, seed 1 only (`a7_printing` +15) | **Hold.** The gain is 1 turn and the order effects on seed 1 are not. |
| b5 low-value, Ages 5–7 | 16 | −0.25 / −0.5 | −2.5 | 46.3 → 46.4 % | 1 node +1, 2 milestones +1…+4 (seed 1) | Eligible. A small speed-up; the flags are within one-turn noise. |
| b6 low-value technologies, Ages 8–9 | 10 | −0.25 / −0.5 | −0.5 | 46.3 → 46.4 % | 4 nodes +1 turn (noise) | **Hold.** It buys nothing: technology Age 9 is throughput-bound, and the ruling forbids cost cuts that only move waste around. |
| b7 low-value civics, Ages 8–9 | 23 | −0.25 / −0.5 | −0.2 | 46.3 → 46.3 % | 2 civics +2…+3 on every seed (`liberation_theology`, `vanguard_party`) | Hold, low value. Civics A7–A9 −2.8 turns. |

No batch moves 2 points or more of waste into any Age.

## 2. What the measurement says

1. **Price changes of ±0.25–0.5 magnitude on 6–23 nodes move pacing by at most ±5 turns on average.** The calibrated scale
   dominates. Cost batches are a pricing-honesty instrument, not a pacing one. Adopting the eligible batches would not need a
   recalibration beyond one confirming pass.
2. **Cheaper is not faster when it changes the AI's order.**
   - **b0:** the cheaper Age-2 enablers (`cereal_cultivation` 46 → 36, `pottery_open_fired` 232 → 191, seed 1) pull the AI's
     goals forward.
   - Two Age-1 technologies slip as a result: `fire_making` 181 → 219 and `knapping_levallois` 166 → 209.
   - That delays writing (Age 3), and through it most later research milestones.
   - The same mechanism is visible, smaller, on seed 1 under b4.
   - **Rule of thumb for any future cut:** measure the node itself AND its downstream milestones, never the node's price alone.
3. **Increases (b1–b3) never produced the mirror flag** (a dearer node's milestone EARLIER). They behave as plain price changes.
4. **Recorded only while pending.** The runner records a milestone only while its Age is the NEXT Age. A supporting milestone
   unmet when its Age is entered has no turn, and is not counted as a delay.

## 3. If the Director later adopts batches (staged, not done)

`tools/apply_overlay.py research.json <candidate> trial/research-economy-trial.json trial/batches/<b>.json …` writes a
candidate content file. It reproduces the shipped file byte for byte with no overlay. Every candidate moves the content hash,
so every golden and CI hash pin moves. See `decision-docket.md`.

## 4. Summary table (generated)

#### Cost batches, each against the calibrated trial baseline (four seeds; mean shift in turns, slowest seed in brackets)

| Batch | Tech A1–A3 complete | Tech A4–A6 | Tech A7–A9 | Civics A1–A3 | Civics A4–A6 | Civics A7–A9 | Age 9 entered | RP lost (base → batch) |
|---|---|---|---|---|---|---|---|---|
| b0-early-six | -3.7 [+4] | +3.6 [+8] | +3.4 [+10] | -3.3 [+5] | +4.9 [+22] | +2.4 [+10] | +3.5 | 46.3% → 46.6% |
| b1-under-a3-5 | +0.0 [+0] | +0.7 [+3] | +0.1 [+4] | +1.2 [+4] | +0.8 [+4] | +0.8 [+5] | +0.2 | 46.3% → 46.3% |
| b2-under-a6-9-tech | +0.0 [+0] | +0.8 [+4] | +3.5 [+6] | +0.0 [+0] | +0.9 [+4] | +3.6 [+6] | +3.5 | 46.3% → 46.0% |
| b3-under-a6-9-civ | +0.0 [+0] | -0.2 [+0] | +0.0 [+0] | +0.0 [+0] | +1.2 [+13] | +0.0 [+0] | +0.0 | 46.3% → 46.3% |
| b4-low-a1-4 | -0.7 [+2] | -1.8 [+3] | -0.8 [+4] | -1.8 [+9] | -2.4 [+3] | -1.8 [+4] | -1.2 | 46.3% → 46.2% |
| b5-low-a5-7 | +0.0 [+0] | -0.7 [+2] | -2.5 [+0] | +0.0 [+0] | +0.2 [+13] | -2.3 [+1] | -2.5 | 46.3% → 46.4% |
| b6-low-a8-9-tech | +0.0 [+0] | +0.0 [+0] | -1.2 [+0] | +0.0 [+0] | +0.0 [+0] | -0.3 [+0] | -0.5 | 46.3% → 46.4% |
| b7-low-a8-9-civ | +0.0 [+0] | +0.0 [+0] | -0.2 [+0] | +0.0 [+0] | +0.0 [+0] | -2.8 [+0] | -0.2 | 46.3% → 46.3% |


## 5. Per-batch, per-seed detail (generated)

### b0-early-six

the six early enablers, -0. 6 nodes: `cereal_cultivation` -0.5, `cordage` -0.5, `fire_making` -0.5, `ground_stone_early` -0.5, `kin_council` -0.5, `pottery_open_fired` -0.5.

Polity Age entered, shift against the baseline (turns):

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | RP lost (base → batch) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | -9 | -6 | +2 | +4 | +5 | +7 | +9 | +9 | 45.8% → 45.2% |
| 2 | -9 | -9 | -3 | -3 | -3 | -7 | -8 | -11 | 36.5% → 38.1% |
| 42 | -9 | -5 | +2 | +4 | +6 | +7 | +8 | +11 | 50.1% → 50.8% |
| 7 | -8 | -4 | +4 | +6 | +6 | +23 | +6 | +5 | 49.4% → 49.5% |

Technology Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | -12 | +0 | +3 | +5 | +5 | +8 | +9 | +9 | +9 |
| 2 | -14 | -5 | -3 | -3 | -4 | -8 | -11 | -11 | -7 |
| 42 | -12 | +1 | +3 | +6 | +6 | +7 | +8 | +10 | +6 |
| 7 | -11 | +2 | +4 | +7 | +7 | +7 | +5 | +5 | +9 |

Civics Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | -11 | +4 | +2 | +6 | +9 | +8 | +6 | +9 | +8 |
| 2 | -30 | +0 | -2 | -3 | -6 | -8 | -9 | -11 | -12 |
| 42 | -11 | +5 | +5 | +7 | +6 | +7 | +5 | +8 | +10 |
| 7 | -10 | +5 | +3 | +4 | +22 | +7 | +6 | +6 | +3 |

RP lost by polity Age, four seeds pooled (baseline → batch):

| Age | Baseline | Batch | Change (points) |
|---|---:|---:|---:|
| A1 | 3.0 % | 3.7 % | +0.7 |
| A2 | 2.4 % | 2.2 % | -0.2 |
| A3 | 1.2 % | 1.3 % | +0.0 |
| A4 | 3.6 % | 3.6 % | -0.1 |
| A5 | 3.9 % | 3.9 % | +0.1 |
| A6 | 4.0 % | 4.1 % | +0.2 |
| A7 | 3.0 % | 3.0 % | +0.0 |
| A8 | 6.5 % | 6.5 % | -0.1 |
| A9 | 75.9 % | 76.9 % | +1.0 |

**Flags**

- Cheaper node completed LATER: seed 1: `fire_making` 181 → 219; seed 2: `fire_making` 188 → 226; seed 42: `fire_making` 182 → 220; seed 7: `fire_making` 182 → 221.
- Research milestone LATER although a node it needs got cheaper: seed 1: 21 milestones, largest A3|supporting|a3_writing 192 → 228, A7|core|a7_printing 927 → 947, A9|core|a9_combustion 1217 → 1227; seed 2: 1 milestones, largest A3|supporting|a3_writing 199 → 235; seed 7: 21 milestones, largest A3|supporting|a3_writing 193 → 230, A7|supporting|a7_powder 899 → 924, A7|core|a7_printing 917 → 941; seed 42: 21 milestones, largest A3|supporting|a3_writing 193 → 229, A9|supporting|a9_radio 1189 → 1197, A9|core|a9_electricity 1182 → 1190.
- Waste moved into an Age (+2 points or more, pooled): none.

### b1-under-a3-5

under-priced, Ages 3-5, both trees (increases). 7 nodes: `astronomy_records` +0.25, `corvee_labour` +0.5, `family_law` +0.25, `seed_drill` +0.25, `taxation` +0.5, `trade` +0.25, `water_commission` +0.25.

Polity Age entered, shift against the baseline (turns):

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | RP lost (base → batch) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | -1 | 45.8% → 45.7% |
| 2 | +0 | +0 | +0 | +0 | +0 | +2 | +0 | +0 | 36.5% → 36.5% |
| 42 | +0 | +0 | +0 | +0 | +2 | +2 | +1 | +1 | 50.1% → 50.2% |
| 7 | +0 | +0 | +0 | +0 | +0 | +15 | +2 | +1 | 49.4% → 49.3% |

Technology Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +0 | +0 | +0 | +0 | +0 | +0 | -1 | -1 | -5 |
| 2 | +0 | +0 | +0 | +0 | -1 | -1 | +0 | +0 | +0 |
| 42 | +0 | +0 | +0 | +0 | +2 | +1 | +1 | +1 | +1 |
| 7 | +0 | +0 | +0 | +1 | +3 | +3 | +1 | +0 | +4 |

Civics Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +0 | +3 | -1 | +1 | +3 | +0 | +0 | -1 | -2 |
| 2 | +0 | +4 | +2 | +0 | +1 | -1 | +0 | +0 | +0 |
| 42 | +0 | +4 | +1 | +4 | -2 | +1 | +4 | +1 | +1 |
| 7 | +0 | +3 | -1 | -3 | +3 | +3 | +5 | +1 | +0 |

RP lost by polity Age, four seeds pooled (baseline → batch):

| Age | Baseline | Batch | Change (points) |
|---|---:|---:|---:|
| A1 | 3.0 % | 3.0 % | +0.0 |
| A2 | 2.4 % | 2.4 % | +0.0 |
| A3 | 1.2 % | 1.2 % | -0.0 |
| A4 | 3.6 % | 3.6 % | -0.1 |
| A5 | 3.9 % | 4.0 % | +0.1 |
| A6 | 4.0 % | 4.0 % | +0.1 |
| A7 | 3.0 % | 2.7 % | -0.3 |
| A8 | 6.5 % | 6.3 % | -0.3 |
| A9 | 75.9 % | 76.0 % | +0.1 |

**Flags**

- Cheaper node completed LATER: none.
- Research milestone LATER although a node it needs got cheaper: none.
- Research milestone EARLIER although a node it needs got dearer: none.
- Waste moved into an Age (+2 points or more, pooled): none.

### b2-under-a6-9-tech

under-priced technologies, Ages 6-9 (increases). 13 nodes: `automobile_mass` +0.5, `crop_rotation_norfolk` +0.25, `green_revolution` +0.5, `horse_collar` +0.5, `lunar_flight` +0.5, `macadam` +0.5, `mechanised_agriculture` +0.25, `new_world_crops` +0.5, `operating_system` +0.5, `oral_rehydration` +0.5, `rice_champa` +0.5, `scientific_plant_breeding` +0.5, `space_telescope` +0.5.

Polity Age entered, shift against the baseline (turns):

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | RP lost (base → batch) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | +0 | +0 | +0 | +0 | +0 | +0 | +2 | +3 | 45.8% → 45.4% |
| 2 | +0 | +0 | +0 | +0 | +0 | +1 | +4 | +6 | 36.5% → 35.8% |
| 42 | +0 | +0 | +0 | +0 | +0 | +0 | +1 | +2 | 50.1% → 50.2% |
| 7 | +0 | +0 | +0 | +0 | +0 | +0 | +2 | +3 | 49.4% → 49.2% |

Technology Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +0 | +0 | +0 | +0 | +0 | +2 | +3 | +3 | +3 |
| 2 | +0 | +0 | +0 | +0 | +0 | +4 | +6 | +6 | +6 |
| 42 | +0 | +0 | +0 | +0 | +0 | +1 | +2 | +2 | +2 |
| 7 | +0 | +0 | +0 | +0 | +0 | +3 | +3 | +3 | +3 |

Civics Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +0 | +0 | +0 | +0 | +0 | +2 | +3 | +3 | +3 |
| 2 | +0 | +0 | +0 | +0 | +1 | +4 | +6 | +6 | +6 |
| 42 | +0 | +0 | +0 | +0 | +0 | +1 | +5 | +2 | +2 |
| 7 | +0 | +0 | +0 | +0 | +0 | +3 | +1 | +3 | +3 |

RP lost by polity Age, four seeds pooled (baseline → batch):

| Age | Baseline | Batch | Change (points) |
|---|---:|---:|---:|
| A1 | 3.0 % | 3.0 % | +0.0 |
| A2 | 2.4 % | 2.4 % | +0.0 |
| A3 | 1.2 % | 1.2 % | +0.0 |
| A4 | 3.6 % | 3.6 % | +0.0 |
| A5 | 3.9 % | 3.9 % | +0.0 |
| A6 | 4.0 % | 4.3 % | +0.3 |
| A7 | 3.0 % | 2.8 % | -0.2 |
| A8 | 6.5 % | 6.2 % | -0.3 |
| A9 | 75.9 % | 76.0 % | +0.1 |

**Flags**

- Cheaper node completed LATER: none.
- Research milestone LATER although a node it needs got cheaper: none.
- Research milestone EARLIER although a node it needs got dearer: none.
- Waste moved into an Age (+2 points or more, pooled): none.

### b3-under-a6-9-civ

under-priced civics, Ages 6-9 (increases). 9 nodes: `abolition` +0.5, `commodity_exchange` +0.25, `copyright` +0.5, `disaster_management` +0.25, `joint_stock` +0.5, `public_housing` +0.25, `systematic_theology` +0.5, `withholding_tax` +0.5, `womens_suffrage` +0.5.

Polity Age entered, shift against the baseline (turns):

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | RP lost (base → batch) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | 45.8% → 45.8% |
| 2 | +0 | +0 | +0 | +0 | +0 | +2 | +0 | +0 | 36.5% → 36.6% |
| 42 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | 50.1% → 50.1% |
| 7 | +0 | +0 | +0 | +0 | +0 | +16 | +0 | +0 | 49.4% → 49.4% |

Technology Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 |
| 2 | +0 | +0 | +0 | +0 | -1 | -1 | +0 | +0 | +0 |
| 42 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 |
| 7 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 |

Civics Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +0 | +0 | +0 | +0 | +4 | +0 | +0 | +0 | +0 |
| 2 | +0 | +0 | +0 | +0 | +2 | -1 | +0 | +0 | +0 |
| 42 | +0 | +0 | +0 | +0 | -4 | +0 | +0 | +0 | +0 |
| 7 | +0 | +0 | +0 | +0 | +13 | +0 | +0 | +0 | +0 |

RP lost by polity Age, four seeds pooled (baseline → batch):

| Age | Baseline | Batch | Change (points) |
|---|---:|---:|---:|
| A1 | 3.0 % | 3.0 % | +0.0 |
| A2 | 2.4 % | 2.4 % | +0.0 |
| A3 | 1.2 % | 1.2 % | +0.0 |
| A4 | 3.6 % | 3.6 % | +0.0 |
| A5 | 3.9 % | 3.9 % | +0.0 |
| A6 | 4.0 % | 3.9 % | -0.1 |
| A7 | 3.0 % | 3.1 % | +0.1 |
| A8 | 6.5 % | 6.5 % | +0.0 |
| A9 | 75.9 % | 75.9 % | +0.0 |

**Flags**

- Cheaper node completed LATER: none.
- Research milestone LATER although a node it needs got cheaper: none.
- Research milestone EARLIER although a node it needs got dearer: none.
- Waste moved into an Age (+2 points or more, pooled): none.

### b4-low-a1-4

low-value, Ages 1-4, both trees (reductions). 19 nodes: `big_man_polity` -0.5, `campaign_provisioning` -0.25, `charcoal` -0.25, `division_of_labour` -0.25, `dyeing` -0.25, `ethical_religion` -0.5, `frontier_fortresses` -0.25, `herbal_remedies` -0.25, `household_land_tenure` -0.25, `hunt_season_leaders` -0.25, `initiation_rites` -0.5, `letter_schools` -0.25, `lifting_crane` -0.25, `overkingship` -0.25, `pantheon_hierarchy` -0.5, `regalia` -0.25, `succession_oaths` -0.25, `tyranny` -0.25, `victory_stelae` -0.25.

Polity Age entered, shift against the baseline (turns):

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | RP lost (base → batch) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | +0 | +0 | +2 | +2 | +1 | +2 | +3 | +4 | 45.8% → 45.5% |
| 2 | +0 | -1 | -2 | -2 | -2 | -5 | -4 | -5 | 36.5% → 37.1% |
| 42 | +0 | -1 | -1 | -1 | -2 | -2 | -2 | -2 | 50.1% → 50.0% |
| 7 | +0 | -1 | +0 | -1 | -2 | -1 | -8 | -2 | 49.4% → 49.2% |

Technology Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +0 | +0 | +2 | +2 | +0 | +3 | +3 | +4 | +4 |
| 2 | +0 | -2 | -2 | -3 | -3 | -6 | -5 | -5 | -1 |
| 42 | -1 | -1 | -1 | -2 | -2 | -2 | -2 | -2 | -2 |
| 7 | -1 | -1 | -1 | -1 | +0 | -7 | -2 | -3 | +1 |

Civics Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +9 | -3 | +2 | +3 | +0 | +3 | -1 | +3 | +4 |
| 2 | -5 | -4 | -2 | -5 | -5 | -6 | -4 | -5 | -4 |
| 42 | -6 | -4 | +2 | -1 | -5 | -2 | -2 | -2 | -2 |
| 7 | -5 | -5 | +0 | -4 | +0 | -7 | -4 | -2 | -3 |

RP lost by polity Age, four seeds pooled (baseline → batch):

| Age | Baseline | Batch | Change (points) |
|---|---:|---:|---:|
| A1 | 3.0 % | 3.0 % | +0.0 |
| A2 | 2.4 % | 2.9 % | +0.4 |
| A3 | 1.2 % | 1.5 % | +0.2 |
| A4 | 3.6 % | 3.4 % | -0.3 |
| A5 | 3.9 % | 3.1 % | -0.8 |
| A6 | 4.0 % | 3.7 % | -0.3 |
| A7 | 3.0 % | 3.0 % | +0.1 |
| A8 | 6.5 % | 6.6 % | +0.1 |
| A9 | 75.9 % | 75.7 % | -0.2 |

**Flags**

- Cheaper node completed LATER: seed 1: `division_of_labour` 277 → 282; seed 1: `hunt_season_leaders` 281 → 286; seed 1: `initiation_rites` 272 → 275; seed 1: `letter_schools` 729 → 732; seed 1: `lifting_crane` 737 → 739; seed 1: `regalia` 443 → 447; seed 42: `regalia` 449 → 451; seed 7: `regalia` 449 → 452.
- Research milestone LATER although a node it needs got cheaper: seed 1: 13 milestones, largest A7|core|a7_printing 927 → 942, A9|supporting|a9_computing 1257 → 1261, A9|supporting|a9_radio 1253 → 1256.
- Waste moved into an Age (+2 points or more, pooled): none.

### b5-low-a5-7

low-value, Ages 5-7, both trees (reductions). 16 nodes: `agronomic_treatises` -0.25, `confessionalization` -0.5, `crucible_cast_steel` -0.25, `holy_war_doctrine` -0.25, `imperial_cult` -0.5, `manorialism` -0.25, `mendicant_orders` -0.5, `mercantilism` -0.25, `mine_pumping_engines` -0.25, `plate_armour` -0.5, `primogeniture` -0.5, `reason_of_state` -0.5, `rhetoric_schools` -0.5, `steam_atmospheric` -0.25, `tributary_relations` -0.5, `westphalian_sovereignty` -0.25.

Polity Age entered, shift against the baseline (turns):

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | RP lost (base → batch) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | +0 | +0 | +0 | +0 | +0 | +2 | +1 | +0 | 45.8% → 46.0% |
| 2 | +0 | +0 | +0 | +0 | +0 | -5 | -1 | -2 | 36.5% → 37.1% |
| 42 | +0 | +0 | +0 | +0 | +0 | +0 | -2 | -3 | 50.1% → 50.0% |
| 7 | +0 | +0 | +0 | +0 | +0 | -1 | -10 | -5 | 49.4% → 49.3% |

Technology Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +0 | +0 | +0 | +0 | +0 | +2 | +0 | +0 | +0 |
| 2 | +0 | +0 | +0 | +0 | +0 | -1 | -2 | -2 | -2 |
| 42 | +0 | +0 | +0 | +0 | +0 | -1 | -3 | -3 | -3 |
| 7 | +0 | +0 | +0 | +0 | +0 | -8 | -5 | -5 | -5 |

Civics Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +0 | +0 | +0 | +0 | +13 | +2 | +1 | +0 | +0 |
| 2 | +0 | +0 | +0 | +0 | -7 | -1 | -1 | -2 | -2 |
| 42 | +0 | +0 | +0 | +0 | +1 | -1 | +1 | -3 | -3 |
| 7 | +0 | +0 | +0 | +0 | +4 | -8 | -9 | -5 | -5 |

RP lost by polity Age, four seeds pooled (baseline → batch):

| Age | Baseline | Batch | Change (points) |
|---|---:|---:|---:|
| A1 | 3.0 % | 3.0 % | +0.0 |
| A2 | 2.4 % | 2.4 % | +0.0 |
| A3 | 1.2 % | 1.2 % | +0.0 |
| A4 | 3.6 % | 3.6 % | +0.0 |
| A5 | 3.9 % | 3.9 % | -0.0 |
| A6 | 4.0 % | 4.5 % | +0.5 |
| A7 | 3.0 % | 3.3 % | +0.4 |
| A8 | 6.5 % | 6.6 % | +0.1 |
| A9 | 75.9 % | 75.5 % | -0.4 |

**Flags**

- Cheaper node completed LATER: seed 1: `steam_atmospheric` 1113 → 1114.
- Research milestone LATER although a node it needs got cheaper: seed 1: 2 milestones, largest A9|core|a9_combustion 1217 → 1221, A8|core|a8_steam 1122 → 1123.
- Waste moved into an Age (+2 points or more, pooled): none.

### b6-low-a8-9-tech

low-value technologies, Ages 8-9 (reductions). 10 nodes: `communications_satellite` -0.25, `electric_traction` -0.25, `fault_tolerant_quantum` -0.5, `fusion_propulsion` -0.5, `gas_engine` -0.25, `geological_disposal` -0.25, `metric_system` -0.25, `space_solar` -0.25, `structural_iron` -0.25, `submarine_cable` -0.25.

Polity Age entered, shift against the baseline (turns):

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | RP lost (base → batch) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | -2 | 45.8% → 45.9% |
| 2 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | 36.5% → 36.7% |
| 42 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | 50.1% → 50.3% |
| 7 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | 49.4% → 49.5% |

Technology Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +0 | +0 | +0 | +0 | +0 | +0 | -2 | -2 | -6 |
| 2 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 |
| 42 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | -4 |
| 7 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 |

Civics Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | -2 | -2 |
| 2 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 |
| 42 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 |
| 7 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 |

RP lost by polity Age, four seeds pooled (baseline → batch):

| Age | Baseline | Batch | Change (points) |
|---|---:|---:|---:|
| A1 | 3.0 % | 3.0 % | +0.0 |
| A2 | 2.4 % | 2.4 % | +0.0 |
| A3 | 1.2 % | 1.2 % | +0.0 |
| A4 | 3.6 % | 3.6 % | +0.0 |
| A5 | 3.9 % | 3.9 % | +0.0 |
| A6 | 4.0 % | 4.0 % | +0.0 |
| A7 | 3.0 % | 3.0 % | +0.0 |
| A8 | 6.5 % | 6.6 % | +0.0 |
| A9 | 75.9 % | 76.1 % | +0.2 |

**Flags**

- Cheaper node completed LATER: seed 2: `submarine_cable` 1413 → 1414; seed 42: `space_solar` 1464 → 1465; seed 42: `submarine_cable` 1266 → 1267; seed 7: `submarine_cable` 1282 → 1283.
- Research milestone LATER although a node it needs got cheaper: none.
- Waste moved into an Age (+2 points or more, pooled): none.

### b7-low-a8-9-civ

low-value civics, Ages 8-9 (reductions). 23 nodes: `agricultural_colleges` -0.25, `bonapartist_regime` -0.5, `civic_religion` -0.5, `clerical_republic` -0.5, `colonial_regiments` -0.25, `constitutional_court` -0.25, `dominant_party_system` -0.5, `ecumenism` -0.25, `electoral_commission` -0.5, `imperialism_doctrine` -0.25, `internet_governance` -0.5, `liberal_internationalism` -0.25, `liberation_theology` -0.25, `missionary_societies` -0.25, `overseas_departments` -0.25, `pan_movements` -0.5, `populism_modern` -0.5, `public_libraries` -0.5, `semi_presidential_republic` -0.25, `social_christianity` -0.5, `social_democracy` -0.5, `telegraph_news_agencies` -0.25, `vanguard_party` -0.25.

Polity Age entered, shift against the baseline (turns):

| Seed | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | RP lost (base → batch) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | 45.8% → 45.8% |
| 2 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | 36.5% → 36.6% |
| 42 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | 50.1% → 50.1% |
| 7 | +0 | +0 | +0 | +0 | +0 | +0 | -1 | -1 | 49.4% → 49.3% |

Technology Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 |
| 2 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 |
| 42 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | +0 |
| 7 | +0 | +0 | +0 | +0 | +0 | +0 | -1 | -1 | -1 |

Civics Age complete, shift against the baseline (turns; never = not completed in 1,630 turns):

| Seed | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | +0 | +0 | +0 | +0 | +0 | +0 | -1 | -7 | +0 |
| 2 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | -7 | +0 |
| 42 | +0 | +0 | +0 | +0 | +0 | +0 | +0 | -8 | +0 |
| 7 | +0 | +0 | +0 | +0 | +0 | +0 | -1 | -8 | -1 |

RP lost by polity Age, four seeds pooled (baseline → batch):

| Age | Baseline | Batch | Change (points) |
|---|---:|---:|---:|
| A1 | 3.0 % | 3.0 % | +0.0 |
| A2 | 2.4 % | 2.4 % | +0.0 |
| A3 | 1.2 % | 1.2 % | +0.0 |
| A4 | 3.6 % | 3.6 % | +0.0 |
| A5 | 3.9 % | 3.9 % | +0.0 |
| A6 | 4.0 % | 4.0 % | +0.0 |
| A7 | 3.0 % | 2.8 % | -0.2 |
| A8 | 6.5 % | 6.5 % | -0.0 |
| A9 | 75.9 % | 75.9 % | -0.0 |

**Flags**

- Cheaper node completed LATER: seed 1: `liberation_theology` 1374 → 1377; seed 1: `vanguard_party` 1375 → 1378; seed 2: `liberation_theology` 1457 → 1460; seed 2: `vanguard_party` 1458 → 1461; seed 42: `liberation_theology` 1313 → 1316; seed 42: `vanguard_party` 1314 → 1317; seed 7: `liberation_theology` 1330 → 1332; seed 7: `vanguard_party` 1331 → 1333.
- Research milestone LATER although a node it needs got cheaper: none.
- Waste moved into an Age (+2 points or more, pooled): none.


