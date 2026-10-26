# Decision docket for the next research-economy ruling (prepared 2026-10-26)

**Purpose:** each open decision, with the evidence already measured, the options, a recommendation, and what is
pre-staged so that a ruling can be carried out without new design work.

**Unchanged by anything in this docket:** the shipped `research.json` and defaults, `main`, tags and releases.

## Where things stand

| Item | State | Evidence |
|---|---|---|
| Calibrated trial overlay (overflow + reserve {3, 1} + gate-aware goals + calibrated scale) | **Accepted as the experimental baseline** (ruling 2026-10-26, later); not default | `pacing-recalibration.md` |
| Casual-player reserve | Accepted provisionally | `pacing-recalibration.md` §5 |
| Civics gate rule (ADR-053) | Approved for a separate trial; implemented behind `tuning.aiCivicsGateGoals`, off; trial measured; on in the candidate | `civics-gate-trial.md` |
| **Research-economy CANDIDATE** (switches + recalibrated scale, civics A7 = 1.479361, + batches b1 and b3) | **Final four-seed trial passed; ready for a separate default-adoption decision**; not default | `candidate-trial.md`, `candidate/` |
| Technology Age 9 | Deferred; documented throughput limitation | `pacing-recalibration.md` §3 |
| Eight cost batches | Measured. b1 and b3 are in the candidate. b2 and b5 are excluded for regressions on the candidate. None is default. | `cost-batches.md`, `candidate-trial.md` §C |
| 17-node audit (CR-025 §8) | Merged only after the dependency, save and golden checks | CR-025 §8 |

## D1. Make the trial configuration the default?

**Preconditions named in the ruling:** the two structural issues (the civics gate and Age 9) resolved or explicitly accepted,
and the policy and cost changes reviewed together.

**Update (2026-10-26, final trial):** the candidate is prepared and measured (`candidate-trial.md`).

**What adoption is, exactly (pre-staged):**
- The file to adopt already exists: `candidate/research.candidate.json` (sha256 prefix `4ed3c69e32bba343`).
- It is the shipped file plus the overlays in `candidate/manifest.json`, written by `tools/apply_overlay.py` and pinned by
  `ResearchCandidateTests`.
- On the ruling, copy it over `Sim.Data/content/research.json`.
- What it changes:
  - five tuning members: `researchOverflow`, `researchReserve` {3, 1}, `aiGateAwareGoals`, `aiCivicsGateGoals`,
    `pacingScale` with civics A7 = 1.479361;
  - the `cost` and `costRationale` of 16 nodes (b1 and b3).
- Nothing else changes, so the tree-closure STRUCTURE and TEXT hashes are the same. `ResearchCandidateTests` proves the
  file differs only in tuning, `cost` and `costRationale`, and both hashes exclude those.

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

**Measured (final trial, `candidate-trial.md`):**
- every seed completes every Age;
- the slowest seed is within −5…+22 of target on technology A2–A8 and within −2…+15 on civics A2–A4 and A6–A8;
- what remains open: civics A5 +57, the Age 1s (+40 / +65) and the Age 9s (+103 / +49) (D4, D5);
- there are no anomaly flags;
- replay and save/load are exact;
- the order invariants hold;
- the AI never holds a reserve;
- RP lost is 46.2 %, against 55.7 % under the shipped default.

**Adoption consequences (measured on a scratch tree):**
- **Founded and AI worlds** differ from turn 1. The M0 toy world is identical.
- **UI sessions saved earlier** are refused (exit 3), with the session files unchanged.
- **CLI `--from-save`** continues an old snapshot under the new rules without warning. Proposed fix: store the content hash
  in the save header.
- **Moved pins:** see `candidate-trial.md` §G.

**Options:**
- (a) Adopt the candidate now, with re-pins and switch-off attribution twins.
- (b) Adopt after the `--from-save` content check is in.
- (c) Keep it as a trial.

**Recommendation:** (b), or (a) if CLI snapshots are not a concern. The candidate is ready for the decision. The
`--from-save` gap is the one compatibility hole, and it predates this candidate: any content change has it.

## D2. Civics gate rule (ADR-053)

**Status:** implemented behind `tuning.aiCivicsGateGoals` and measured as a separate trial on top of the calibrated baseline
(`civics-gate-trial.md`).

**Done (2026-10-26, final trial):** on in the candidate. Civics A7 was recalibrated with it on, from 0.757 to 1.479:
- slowest-seed residual −15;
- the only measured value that keeps every Age on every seed;
- rule on vs off bit-identical at that scale.

The record is in `pacing-recalibration.md` §7 and `candidate-trial.md` §B. Its adoption is part of D1.

**Options (as prepared before the trial):**
- (a) Adopt it into the trial configuration, then recalibrate civics Age 7, which is unfrozen once the stall is gone.
- (b) Keep it as a separate trial.
- (c) Reject it.

**Recommendation:** (a), carried out in the candidate.
- **Arm A:** with the calibrated scales the rule is inert, and the final hashes are bit-identical.
- **Arm B:** with pass-3 scales on seed 2 it removes the order-driven `caliphate` stall: `caliphate` −158 turns, technology
  steps with nothing to fund 184 → 71, Age 8 entered 158 turns sooner.
- **Arm C:** a stall limited by CP flow (civics A7 at 2.06) is trimmed only, so recalibrating civics A7 with the rule on is
  the next step.
- See `civics-gate-trial.md`.

## D3. Cost batches

Measured one at a time (`cost-batches.md` §1). None is pacing-relevant beyond ±5 turns.

| Batch | Recommendation |
|---|---|
| b0, the six early cuts | **Reject.** The cuts make the run slower: `fire_making` +38 and writing (A3) +36 on every seed. |
| b1, b3 (increases) | **In the candidate.** Confirmed on the recalibrated candidate: no flags, every Age on every seed (`candidate-trial.md` §C). |
| b2 (increases, technology A6–A9) | **Excluded.** With b1 and b3, seed 2 loses technology Age 9 within 1,630 turns. b2 alone ends it on the last turn. Revisit after D4. |
| b5 (cuts, Ages 5–7) | **Excluded.** Civics A7 on seed 2 −43 (outside ±30), and cheaper-but-later `steam_atmospheric` / `mine_pumping_engines` flags. |
| b4 | Hold: order effects on seed 1. |
| b6 | Hold: Age 9 is throughput-bound, so the cut buys nothing. |
| b7 | Hold: low value. |

**Staged:** `candidate/manifest.json` lists the overlays in the candidate.

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
- **17-node audit.** MERGED (CR-025 §8). It added inert future declarations only: same save bytes, no golden moved, every
  node unchanged apart from `effects.future`. Proposals for the Director, NOT applied:
  - **papyrus:** realized `governance.authorityDecayCostUnits` +0.04.
  - **telephone:** realized `governance.authorityDecayCostUnits` +0.03.
  - **Operand budget:** the operand sum goes from 2.570 to 2.640 of a cap of 3.0, under the 90 % line, so no other delta is
    rescaled.
  - **Cost of applying:** each moves the default game and needs a pin re-measure.
  - **Removals:** none proposed.
  - **The rest:** the other 15 stay future-only until their system exists, showing "not simulated yet".