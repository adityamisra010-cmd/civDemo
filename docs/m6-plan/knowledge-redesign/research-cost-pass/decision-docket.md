# Decision docket for the next research-economy ruling (prepared 2026-10-26)

**Purpose:** each open decision, with the evidence already measured, the options, a recommendation, and what is
pre-staged so that a ruling can be carried out without new design work.

**Unchanged by anything in this docket:** the shipped `research.json` and defaults, `main`, tags and releases.

## Where things stand

| Item | State | Evidence |
|---|---|---|
| Calibrated trial overlay (overflow + reserve {3, 1} + gate-aware goals + calibrated scale) | **Accepted as the experimental baseline** (ruling 2026-10-26, later); not default | `pacing-recalibration.md` |
| Casual-player reserve | Accepted provisionally | `pacing-recalibration.md` §5 |
| Civics gate rule (ADR-053) | Approved for a separate trial; implemented behind `tuning.aiCivicsGateGoals`, off | `civics-gate-trial.md` |
| Technology Age 9 | Deferred; documented throughput limitation | `pacing-recalibration.md` §3 |
| Eight cost batches | Measured, not adopted | `cost-batches.md` |
| 17-node audit (CR-025 §8) | Merged only after the dependency, save and golden checks | CR-025 §8 |

## D1. Make the trial configuration the default?

**Preconditions named in the ruling:** the two structural issues (the civics gate and Age 9) resolved or explicitly accepted,
and the policy and cost changes reviewed together.

**What adoption is, exactly (pre-staged):**
- Run `tools/apply_overlay.py Sim.Data/content/research.json <candidate> trial/research-economy-trial.json`, plus
  `trial/civics-gate-trial.json` if D2 is adopted.
- Diff the candidate, then copy it over the shipped file only on the ruling.
- The overlay is the whole change. It sets four tuning members (`researchOverflow`, `researchReserve` {3, 1},
  `aiGateAwareGoals`, `pacingScale`), plus `aiCivicsGateGoals` with D2. No node cost moves unless D3 adds batches.

**Blast radius (known now):**
- **Golden and CI hash pins.** Every pinned run that uses research moves. That covers the `sim run` / `--founded` legs in
  `ci.yml` and the attribution and golden tests. Each must be re-pinned with an attribution control: the switch-off twin
  returning the old hash, as for CR-025.
- **Saves.**
  - Schema v37 (the reserve table) already exists. Nothing new is serialized unless D2 adds state, and ADR-053 adds none.
  - A session saved under the old defaults diverges on resume. The trace check refuses it with the existing
    "This saved session cannot be resumed with this build" message (ADR-034, D-052 §13). There is no migration and no silent
    reinterpretation.
- **AI behaviour.** The AI's research order changes: gate-aware switches, and civics gate switches with D2. Measured
  effects:
  - all four seeds complete every Age;
  - the AI never holds a reserve;
  - replay and save/load are exact.

**Options:**
- (a) Adopt now.
- (b) Adopt after the ADR-053 trial has been reviewed.
- (c) Keep it as a trial.

**Recommendation:** (b). The configuration is measurably better than the default on waste (46.3 % vs 55.7 %) and pacing
(technology A2–A8 within −7…+20 of target, against −81…+6). The civics-side stall is the one structural issue a policy can
fix, and its trial is the remaining gate.

## D2. Civics gate rule (ADR-053)

**Status:** implemented behind `tuning.aiCivicsGateGoals` and measured as a separate trial on top of the calibrated baseline
(`civics-gate-trial.md`).

**Options:**
- (a) Adopt it into the trial configuration, then recalibrate civics Age 7, which is unfrozen once the stall is gone.
- (b) Keep it as a separate trial.
- (c) Reject it.

**Recommendation:** see `civics-gate-trial.md` §verdict.

## D3. Cost batches

Measured one at a time (`cost-batches.md` §1). None is pacing-relevant beyond ±5 turns.

| Batch | Recommendation |
|---|---|
| b0, the six early cuts | **Reject.** The cuts make the run slower: `fire_making` +38 and writing (A3) +36 on every seed. |
| b1, b2, b3 (increases) and b5 | Eligible on pricing grounds. Adopt together with D1 and confirm with one four-seed pass. |
| b4 | Hold: order effects on seed 1. |
| b6 | Hold: Age 9 is throughput-bound, so the cut buys nothing. |
| b7 | Hold: low value. |

**Staged:** the same `apply_overlay.py` with the batch overlays appended.

## D4. Technology Age 9

**Deferred by ruling.** It needs about 140 turns at any cost, because a lane completes at most about 2 nodes per turn. The fix
belongs in the progression design: either the final-Age repeatables, or a capacity rule for the last Age. Ideas to evaluate
then:
- (i) Repeatables absorbing surplus RP with a growth rate tuned to the target window.
- (ii) Raising per-step completion capacity in the final Age only, as an explicit rule rather than lane-wide overflow.
- (iii) Revising the A9 target to match the measured capacity.

Not to be done: lane-wide overflow, or cost cuts that only move waste.

## D5. Smaller items

- **Age 1 is not cost-bound** (+40 / +65 residual). Options:
  - (a) accept it;
  - (b) move the A1 target;
  - (c) an AI rule to finish the current Age's cohort before chasing the next Age's milestones. This would be new AI
    behaviour, and it would need its own trial.
- **Seed spread.** Seed 2 trails by 40–70 turns, and the slowest-seed rule leaves the other three seeds ahead of target. Options:
  - (a) accept it (D-063 R6 as written);
  - (b) calibrate to the median seed, with a stated maximum lag for the slowest.
- **17-node audit.** Merge as reported in CR-025 §8 once its checks hold. Its (a) real-effect and (c) removal proposals each
  need their own ruling and stay "not simulated yet" until then.
