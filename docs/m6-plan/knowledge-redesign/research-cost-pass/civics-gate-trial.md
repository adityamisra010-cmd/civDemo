# Civics gate rule: separate trial (ADR-053; 2026-10-26; switch OFF by default)

**Director ruling 2026-10-26 (later):** "Civics gate rule — Approve for a separate trial. The caliphate stall is a structural
goal-selection problem, not a cost problem." The rule is `tuning.aiCivicsGateGoals`, ADR-053. It is off in the shipped
`research.json` and on only through `trial/civics-gate-trial.json`. Every run: one AI Empire on the canonical world, 1,630
turns, the same binary throughout.

## 1. Results

| Arm | Configuration | Seeds | What it shows |
|---|---|---|---|
| A. Calibrated baseline + rule | pass 8 + `civics-gate-trial.json` | 1, 2, 42, 7 | **Inert.** The rule never triggers. All four final hashes are bit-identical to pass 8 without it (`f227a575…`, `d761689f…`, `639b5e16…`, `b93cc1ce…`), so nothing is diverted when technology is not stalled. Replay EQUAL and save/load equal on all four. |
| B. Order-driven stall (pass-3 scales, seed 2) | pass 3 ± rule | 2 | **Fixes the `caliphate` stall.** See the table below. |
| C. CP-bound stall (pass 8 with civics A7 raised to 2.06) | ± rule | 1, 2, 42, 7 | **Trims, cannot fix.** Seeds 1, 42 and 7 never trigger and are bit-identical. On seed 2, technology waits from turn 1,443 on the whole Age-7 civics cohort, which the civics lane is already paying for, so CP flow, not goal order, is the limit. With the rule on, the technology steps with nothing to fund fall from 98 to 92, turns at the reserve cap from 42 to 36, and civics A7 completes and Age 9 is entered 6 turns sooner. Civics A8, which gates nothing, slips past turn 1,630. Replay and save/load are exact. |

**Arm B (seed 2, the worst `caliphate` stall measured):**

| Measure | Rule off | Rule on |
|---|---:|---:|
| `caliphate` completed | 1,460 | **1,302** (−158) |
| Civics Age 6 complete | 1,460 | **1,302** |
| Technology steps with nothing to fund | 184 | **71** |
| Turns at the reserve cap | 157 | **44** |
| Age 8 entered | 1,476 | **1,318** (−158) |
| Technology Age 8 complete | never | **1,474** |
| Civics Age 7 / Age 8 complete | 1,433 / never | 1,519 / **1,588** |
| RP lost (whole run) | 37.0 % | **34.2 %** |

**How to read it.** The rule does what the ruling asked. When technology cannot advance because the sweep needs a civic, the
civics lane goes to that civic, and only then. Arm A is the starvation check: bit-identical runs. Arm C shows a stall it
cannot fix by design. When the whole blocking cohort is already being paid for, reordering cannot add CP. That case is a
pricing problem, so the civics A7 scale has to fit the CP flow.

## 2. Verdict and next step (the Director's decision)

- **Recommendation:**
  - Add the rule to the trial configuration as a safety net. It is free when it does not trigger (arm A) and removes the
    order-driven stall where it occurs (arm B).
  - Then unfreeze civics Age 7 and recalibrate it with the rule on: bounded passes, as in `pacing-recalibration.md`.
  - Arm C bounds that recalibration. At 2.06 the Age-7 civics cohort is too dear for seed 2's CP flow, so the calibrated
    value lies between the shipped 0.757 and 2.06.
- **Staged:**
  - The overlay pair `trial/research-economy-trial.json` + `trial/civics-gate-trial.json`.
  - The recalibration: `tools/recalibrate_trial.py <dir> <label> <overlay> --damp 0.5 --max-step 1.25 --freeze 1,technology:9`,
    with civics:7 no longer frozen.
  - The arm-C stress overlay: `data/civics-gate/stress-overlay-civics-a7-2.06.json`.
- **Not done:** no default changes, and no civics A7 recalibration yet. It waits on the ruling.

## 3. Tests (ADR-053 §4)

- **Suite:** 16 rig tests, covering correct blocker, lowest cohort, tie-dense composite key, respects prerequisites (turn by
  turn), does not starve (exhaustive on/off identity whenever the trigger does not hold), re-decide while held, sweep-only
  trigger, off pinned and loader.
- **Mutants:** 7 mutants, each killed by a semantic test.
- **Regression:** the golden/research/AI filter passed (455 passed, 7 skipped measurement facts) with no golden moved.

Data: `data/civics-gate/` (summaries of every arm; CSVs of arms B and C).
