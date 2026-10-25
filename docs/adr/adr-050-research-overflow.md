# ADR-050 — In-step research overflow (switch, shipped OFF)

**Status: PROPOSED — evidence for the Director's ruling D-064 (not yet ruled).** The engine carries the rule behind two
research.json switches, `tuning.researchOverflow` and `tuning.researchOverflowLane`. Both are absent from the shipped
`research.json`, so they are false, and the shipped engine is the one-node rule of D-044 R20-D, byte for byte. No golden
moves. This record states the rule so that the code and the evidence have a contract to cite. Adopting it, and any
pacing recalibration that adoption needs, is the Director's decision.

## 1. The problem

D-044 R20-D: a lane funds ONE node per step. RP past that node's remaining cost is discarded, and so is all of a step's RP
when the lane funds nothing. CP is banked (ADR-043 §5), so CP is never lost, but it too pays one civic per step.
Measured on the shipped content, the AI loses 54.5 % of the RP it generates over 1,630 turns (four seeds). The loss is
72.1 % in Age 9, where one turn's RP buys several nodes. See
`docs/m6-plan/knowledge-redesign/research-cost-pass/overflow-comparison.md`.

## 2. The rule (switch on)

1. **Continuation.** Once a lane's funded node is fully paid, the remainder funds the lane's next node,
   `ResearchSystem.NextFunded`. The order is:
   - the destination, if it is available;
   - else the next node of the destination's route (`PickCurrent`);
   - else the free-lane assist on the other lane's plan.

   A city state takes the cheapest available node of its lane. The technology lane spends RP and the civics lane spends
   banked CP (`SpendCivics`, `balance − spent`).
2. **Read from PREV.** Availability is PREV's. A node whose prerequisites complete in this step is not funded until the
   next step.
3. **Each node at most once per step.** Each node is paid at most once per step (`paidOff`). A levelled repeatable
   therefore gains at most one level per step. The loop terminates because every iteration either retires a node or
   stops.
4. **Banks stay separate.** RP never pays a civic, and CP never pays a technology. The CP identity
   `Balance == Generated − Spent` holds.
5. **Nothing fundable.** When nothing is fundable, the RP remainder is lost, not banked. The CP remainder stays in the CP
   bank.
6. **Lane variant** (`researchOverflowLane`, evidence only). When the route and the assist are exhausted, the remainder
   funds `AiResearchPolicy.Cheapest` among the lane's own tree. This uses the composite key (EffectiveCost, key) and
   includes repeatables.

## 3. Evidence

- **Semantics:** `Sim.Tests/Systems/ResearchOverflowTests.cs` (9 tests) covers rules 1–6, and both repeatable tests run
  real levelled nodes. `Sim.Tests/Systems/ResearchPointLifecycleTests.cs` pins today's OFF behaviour, case by case.
- **Measurement:** `Sim.Tests/Kernel/OverflowComparisonMeasurement.cs` (skipped by default) runs four seeds × 1,630 turns
  on the same content with OFF, ON and ON-LANE. RP lost:

  | Mode | RP lost |
  |---|---:|
  | OFF | 54.5 % |
  | ON | 50.8 % |
  | ON-LANE | 2.9 % |

  Under ON, Age 9 is entered 44–65 turns sooner. ON-LANE collapses Ages 8–9, so it needs a pacing recalibration.
  Structures, institutions and discovery are identical across modes.

## 4. What overflow cannot fix

Most of today's loss in Ages 5 and 7, and every step where a lane has no destination (`fund[0] < 0`), has nothing to
fund. Overflow continues spending; it cannot create a funded node. These cases are listed in `rp-cp-lifecycle.md`
(b, c, d1, d2, f, g1, g2). They need a separate rule (an RP bank, or an idle-lane default), which is not proposed here.

## 5. Blast radius if adopted

- **ON (route).** Ages arrive sooner, so the pacing scales of `research.json` must be re-measured. The golden traces of
  every research-using run move. No save-schema change.
- **ON-LANE.** Has the same consequences, much larger, and also interacts with the repeatables' pricing
  (`repeatables-proposal.md`).

## 6. Trial on the post-fix baseline (2026-10-25)

Director instruction (2026-10-25): trial route overflow against the previous baseline, with `researchOverflow`
disabled by default and the lane variant not enabled. Report: `research-cost-pass/overflow-trial.md`. Baseline:
`9204de7`. The display fixes k2–k5 and CR-025 move no simulation state, so it is the shipped game.

**Measured** (seeds 1, 2, 42 and 7; 1,630 turns):

| Measure | OFF | ON (route) |
|---|---|---|
| RP lost | 54.5 % | 50.8 % |
| Waste in Ages 2, 3, 6 and 8 | — | cut 3–4× |
| Waste in Age 9 | 72.1 % | 66.2 % |
| Waste in Age 7 | 22.5 % | 26.7 % (see below) |
| Age 9 entry | — | 44–65 turns sooner |
| Completion order (Spearman ρ, 992 finite nodes per seed) | — | ≥ 0.999 |
| Most nodes in one turn, per tree | 1 | 2 |

- CP identity holds on every run.
- Repeatables gain at most one level per lane per step.
- Every ON run replays to the same final hash from its order log, and the save/load round trip at turn 815 is equal.

**Found.** Overflow speeds technology more than civics, so the technology lane reaches the Age-sweep gate first and waits
on the civics cohort (heavy_plough in Age 5, germ_theory in Age 7). The RP of those steps is lost: 120 waiting steps in
Age 7 across 4 seeds, against 12 under OFF. This is the empty-lane case, so overflow is not to be adopted alone
(`research-cost-pass/empty-lane-reserve-proposal.md`).

**Status.** Still PROPOSED, still OFF. Recommendation: rule on overflow together with the empty-lane rule, then refresh
the pacing baseline once with both. No cost change accompanies either.
