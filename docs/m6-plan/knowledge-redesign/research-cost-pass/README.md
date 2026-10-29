# Research cost pass — index (D-064 review package; NOTHING APPLIED)

**Status:** evidence and proposals for the Director. The shipped `research.json` is unchanged: no cost, pacing or
repeatable value moved, and both overflow switches are absent (off). The engine gained the overflow rule behind those
switches (ADR-050, proposed); with them off it is the shipped one-node engine. Branch `claude/new-session-yavghm`; `main`
and `m5-exit` are untouched.

Overflow and cost calibration were kept apart: each was measured alone against the same shipped baseline (seeds 1, 2, 42
and 7, the canonical world, one AI Empire).

## Read in this order

| # | Document | Question it answers |
|---|---|---|
| 1 | `rp-cp-lifecycle.md` | Where exactly are RP and CP lost today? Cases (a)–(l), each pinned by a test, with the engine lines and whether overflow can recover it |
| 2 | `overflow-comparison.md` | What does overflow change? OFF / ON (route) / ON-LANE over 1,630 turns: RP lost per Age, Age and civic timing, infrastructure, discovery |
| 3 | `data/live-map.md` | Which effects and unlocks are live today, and which are placeholders for M7/M8 |
| 4 | `decision-table.md` | Per flagged node: cost, class, disposition, effect, gate role, dependents, expected impact in turns |
| 5 | `early-game-trial.md` | Does pricing six opening enablers ×0.71 speed the opening, alone (overflow off)? |
| 6 | `online-research.md` | How shipped games price repeatable and late-game research (cited) |
| 7 | `repeatables-proposal.md` | Final-Age repeatables for both trees: set, operands, pricing, and its tie to pacing |
| 8 | `overflow-trial.md` (2026-10-25) | Route overflow against the post-fix baseline: RP by Age, Age and milestone turns, completion order, CP, repeatable levels, replay |
| 9 | `empty-lane-reserve-proposal.md` (2026-10-25) | The empty-lane RP rule: reserve caps compared against an uncapped reserve, a human proxy, bursts and pacing, a UI spec |
| 10 | `combined-trial.md` (2026-10-26) | The Director's trial configuration (overflow + capped reserve + gate-aware goals) against the new baseline: waste, Ages, civics, roads, milestones, casual and idling players, gate waits, reserve saturation, replay |
| 11 | `pacing-recalibration.md` (2026-10-26, later) | The pacing scales recalibrated under the trial configuration (overlay only): passes, held scales and their residuals, per-seed Age turns, waste by Age, reserve time at the cap versus size, replay |
| 12 | `cost-batches.md` (2026-10-26, later) | The eight cost batches, each measured independently against the calibrated trial: per-seed shifts, cheaper-but-later and waste-shift flags, verdicts (none adopted) |
| 13 | `civics-gate-trial.md` (2026-10-26, later) | ADR-053's separate trial: inert on the calibrated baseline (bit-identical), removes the order-driven `caliphate` stall, trims a CP-bound stall |
| 15 | `candidate-trial.md` (2026-10-26, final trial) | The research-economy CANDIDATE: civics A7 recalibrated with the gate rule on (1.479), batches b1/b2/b3/b5 alone and combined on it (b2 and b5 excluded, with the isolating runs), four seeds with and without the approved batches, casual and idling players, determinism, adoption impact on saves and pins, readiness recommendation |
| 16 | `per-turn-cp-recalibration.md` (2026-10-27) | ADR-054 (D-065 R6: CP per turn from the RP base, the average population): the civics pacing scale recalibrated for the shipped content and the candidate, passes, four-seed tables, technology Age 9 diagnosed, RC-1 (aircraft A8) measured |
| — | `candidate/` | The candidate content file (`research.candidate.json`) and its manifest; pinned by `ResearchCandidateTests`; NOT the shipped content |
| 14 | `decision-docket.md` (2026-10-26, later) | The open decisions for the next ruling, with evidence, options, recommendations and what is pre-staged |
| — | `../../../adr/cr-025-unconsumed-overland-speed-operand.md` | The 17 nodes whose speed effect no system consumes: evidence, options, the interim display fix |

## Findings in one screen

- **RP waste.** OFF loses 54.5 % of the RP generated (four seeds, 1,630 turns); 72.1 % in Age 9. Two causes, and they
  need different fixes:
  - **Surplus past a completed node** (the one-node rule). Route overflow cuts the loss to 50.8 % overall and to 2–8 %
    in Ages 2–8. The lane variant cuts it to 2.9 % overall, but it collapses Ages 8–9, so it needs a pacing
    recalibration.
  - **A lane with nothing funded:** no destination, the step after a completion, a destination waiting on the other
    tree, or a repeatable level just bought. Overflow cannot help here. The AI never hits these cases (it re-orders in
    the same step); a player hits them every time a lane is left empty.
- **CP waste.** None, apart from the ceil-to-milli rounding (under 1 milli-CP per civic).
- **Early game.** Six enablers priced ×0.71 with overflow off:
  - each completes 9–20 turns sooner;
  - Age 2 arrives 7–8 turns sooner, Age 3 13–14 sooner, Age 4 11–13 sooner;
  - the gain is a fixed shift; it does not compound.
- **Cost screen.** 138 flagged nodes:
  - 6 early enablers.
  - 29 under-priced (increase candidates).
  - 68 low-value and 7 placeholder-only (reduction candidates).
  - 22 future-dependent (held: priced for the M7/M8 system they wait on).
  - 6 effect-defects (held: their only effect is an operand no system reads).

  Fourteen nodes in all carry only the unread `roads.baselineKmPerDay` effect (`data/live-map.md`). Fix the reader, not
  the price.
- **Repeatables.** Today's final-Age repeatables cost 2–9 % of one Age-9 turn's RP and 12–24 % of one turn's CP, and grow
  ×1.2 per level. The proposal is 11 technology and 9 civics repeatables with growth 1.05 and level-1 anchored at
  1.2–1.5× the costliest finite final-Age node. Military strength is held for M7, which supplies the combat operand.

## Code and tests

| Item | What it is |
|---|---|
| `ResearchSystem.NextFunded`, `ResearchTuning.ResearchOverflow` / `ResearchOverflowLane` | the overflow rule (ADR-050), off |
| `Sim.Tests/Systems/ResearchOverflowTests.cs` (9) | overflow semantics: once per step, PREV availability, separate banks, no repeatable loop |
| `Sim.Tests/Systems/ResearchPointLifecycleTests.cs` (19, one a skipped measurement) | today's OFF behaviour, case by case |
| `Sim.Ui.Tests/ResearchPointDisplayTests.cs` (4) | the research screen's numbers against `ResearchQuery` and the next step |
| `Sim.Tests/Kernel/OverflowComparisonMeasurement.cs` (skipped) | the OFF / ON / ON-LANE and early-trial measurements |
| `Sim.Tests/Kernel/ResearchPacingRunner.cs`, `ResearchEconomyMeasurement.cs` (`SIM_MEASURE=1`) | the overflow trial and the reserve study: per-step funding reconstruction, a human proxy, a reserve model outside the engine, replay and save/load |
| `tools/research_economy_report.py` | regenerate the trial and reserve tables from the run CSVs (`data/trial`, `data/reserve`) |
| `tools/cost_audit.py`, `tools/decision_table.py`, `tools/overflow_report.py` | regenerate `baseline-screen.csv`, the decision table and the comparison tables |
| `trial/research-economy-trial.json`, `trial/batches/b0…b7-*.json` | the calibrated trial overlay (three switches + its own `pacingScale`) and the eight cost-batch overlays; the shipped `research.json` is untouched |
| `tools/recalibrate_trial.py` | the shipped calibration algorithm (D-063 R6) applied to the runner's CSVs, writing only the overlay; bounded step (`--max-step`) and held scales (`--freeze`) |
| `tools/cost_batch_overlays.py` | writes the cost-batch overlays from the decision table |
| `tools/research_economy_report.py pacing \| batch \| batchdetail` | the pacing-pass tables, the batch summary and the per-seed batch effects |
| `Sim.Tests/Kernel/ResearchOverlayTests.cs` (4) | the overlays: shipped `research.json` keeps the switches absent; the trial overlay sets only the switches and its scale; a node patch moves only that node within the factor ranges; the batches are disjoint, load on the overlay and never touch a held node |

## 2026-10-25 sequence (Director instruction): status

1. **Defects. Fixed.**
   - Research screen k2–k5: `9d4788b`, merged in `9204de7`. The screens name the funded node, the assisting and waiting
     lanes, and the next step's CP a turn.
   - The speed operand: CR-025 raised (`63bdf7c`, merged in `cce5d72`). No supported system consumes travel time, and
     the honest content fix conflicts with D-063 R1, so it waits on the ruling. Interim display correction `1ad60d3`: the
     card says "not simulated yet". No cost was lowered.
2. **Route-overflow trial. Done.** It stays OFF. See `overflow-trial.md`: waste falls only from 54.5 % to 50.8 %, Age 9
   arrives 44–65 turns sooner, completion order holds and replay is exact. It also creates sweep waits.
3. **Empty-lane rule. Proposed, not implemented.** `empty-lane-reserve-proposal.md`: cap 3 turns, draw at most 1 turn,
   and drawn RP spent by the same rules. EXACT draws were found to reward idling.
4. **Costs. Unchanged and deferred** until the RP policy is ruled and the pacing baseline is refreshed. The six early
   cuts get reassessed then.
   - FUTURE-DEPENDENT and EFFECT-DEFECT rows stay on hold.
   - Placeholder civics get no blanket reductions.
5. **Final-Age repeatables. Deferred** on the same condition. Military waits on M7.

## 2026-10-26 ruling: status

- **Overflow, reserve and gate-aware goals:** implemented behind switches (ADR-050, -051, -052), all OFF by default. The
  combined trial is measured in `combined-trial.md`.
- **CR-025:** ruled and applied (CR-025 §7; D-063 R1 Amendment 1). Its attribution twin reproduces the old baseline bit
  for bit.
- **Costs and final-Age repeatables:** unchanged, waiting for the review of the combined baseline.

## 2026-10-26 (later) ruling: status

| Decision | Ruling | Reason |
|---|---|---|
| Calibrated pacing overlay | Accept as trial baseline | RP waste falls to 46.3 %, and technology pacing is much closer to target |
| Casual-player reserve behavior | Accept provisionally | The casual scenario finishes within roughly one turn of the AI; idling falls 48 turns behind |
| Civics gate rule | Approve for a separate trial | The caliphate stall is a structural goal-selection problem, not a cost problem |
| Technology Age 9 | Defer | Do not use lane-wide overflow or arbitrary cost cuts to force completion into the target window |
| Eight cost batches | Continue measurement, not default adoption | Each batch must be evaluated independently against the calibrated baseline |

- **Shipped defaults:** unchanged. The calibrated overlay is the experimental baseline, not the default.
- **The 17-node audit** (CR-025 §8) is merged only after dependency, save/migration and golden checks.
- **Age 9** stays a documented throughput limitation; its fix belongs in the eventual repeatable/progression design.
- **Civics gate rule:** ADR-053, a separate trial.

## Decisions waiting on the Director

1. Adopt route overflow (ADR-050) together with the empty-lane reserve (`empty-lane-reserve-proposal.md` §4), then
   refresh the pacing baseline once with both on.
2. The sweep wait that overflow creates: accept it, let the waiting technology lane assist the blocking civics cohort,
   or give sweep waits a larger reserve cap.
3. Apply the six early-enabler cuts?
4. Apply the screen's increases and reductions? Each would be a separate, row-by-row change.
5. Approve the repeatables set and growth 1.05? If yes, set the level-1 magnitudes together with the pacing
   recalibration.
6. CR-025: rule on the per-node disposition of the 17 speed-operand nodes, and on D-063 R1.
