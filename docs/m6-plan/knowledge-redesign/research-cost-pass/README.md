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
| `tools/cost_audit.py`, `tools/decision_table.py`, `tools/overflow_report.py` | regenerate `baseline-screen.csv`, the decision table and the comparison tables |

## Decisions waiting on the Director

1. Adopt route overflow (ADR-050)? If yes, re-measure the pacing scales in the same change.
2. Adopt a rule for empty-lane steps, such as a default cheapest fund or a destination that persists through repeatable
   levels? This is separate from overflow, and it is where a player's remaining RP is lost.
3. Apply the six early-enabler cuts?
4. Apply the screen's increases and reductions? Each would be a separate, row-by-row change.
5. Approve the repeatables set and growth 1.05? If yes, set the level-1 magnitudes together with the pacing
   recalibration.
6. Fix the display discrepancies k2–k5 and the unread `roads.baselineKmPerDay` operand. These are defects, not balance.
