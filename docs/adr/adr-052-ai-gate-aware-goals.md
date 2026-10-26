# ADR-052 — Gate-aware AI technology goals (switch, shipped OFF)

**Status: PROPOSED — behind `research.json` `tuning.aiGateAwareGoals`.** The switch is absent from the shipped
`research.json`, so it is false and the AI's research rule is unchanged, byte for byte (§4.3). It is enabled only in the
trial configuration (`ResearchPacingRunner`, `RunOptions.GateAware`). Adopting it is the Director's decision.

## 1. The problem (measured)

`research-cost-pass/overflow-trial.md` finding 3 and `research-cost-pass/data/trial/waiting-probe.txt`: with the two
lanes, destination routing and the Age sweep (ADR-048 R2), the AI's technology lane holds a destination of the next Age
(heavy_plough in Age 5, corned_powder in Age 6, germ_theory in Age 7) whose route cannot be funded until the current
Age's CIVICS cohort completes (`RouteReason.Sweep`). The engine's planner gives the lane no current node; the lane
"waits" and the step's RP is lost. With `researchOverflow` on, Age 7 shows 120 waiting steps over four seeds (12 with
overflow off).

## 2. The ruling (Director, 2026-10-26)

"Do not let RP pay for civics or combine RP and CP. First, make the technology planner choose another eligible research
goal when its current target is blocked by civic prerequisites. If no eligible goal exists, use the capped reserve rule.
Test long waits, repeated age gates, reserve saturation and the first turns after a gate opens. If the planner cannot
avoid the stall, document the remaining waste rather than introducing an unlimited reserve." The default configuration
stays unchanged: the rule is behind a switch. A HUMAN player's research is never chosen automatically, so the rule is the
AI's goal selection (`AiResearchPolicy`), not the engine's funding of a chosen destination. The capped reserve is another
branch's work; this record covers the planner half only.

## 3. The rule (switch on)

It acts only with the two-lane model (`tuning.civicsPoints`) and `tuning.destinationRouting`, on an AI polity's
TECHNOLOGY lane (`CommandSource.Ai`; the Civics lane and the one-pool model are untouched).

1. **Fundability** (`AiResearchPolicy.TechnologyFundable`). A technology destination is fundable now when
   `ResearchRoutePlanner.Fund` gives the technology lane a CURRENT node. Fund runs over the polity's completed mask,
   available mask (levels included) and remaining costs, with the Civics lane's destination as it stands. These are the
   inputs ResearchSystem and `ResearchQuery.Route` use. A current node can be the destination itself, a route node, or a
   free-lane assist.
2. **Re-decision** (`DecideLane`). A held technology destination that is open but not fundable is no longer live, so the
   AI decides again. Its progress stays on its nodes, because progress rows are per node and never cleared. If the new
   choice is -1 (or the same node), no order is issued and the held destination stays. A fundable destination is never
   re-decided, so the rule cannot churn.
3. **Choice** (`ChooseDestination` → `FundableDestination`):
   - The goals that `ChooseGoal` admits (a closure node AVAILABLE) are walked in ChooseGoal's order, the composite key
     (remaining closure cost, ordinal).
   - Each goal's sinks are walked in `GoalDestination`'s order, the composite key (route cost, key).
   - The first sink the lane can fund now is the destination.
   - The walk starts at the existing choice, so a fundable existing choice is the answer unchanged.
   - If no sink is fundable, the cheapest AVAILABLE technology is chosen (`Cheapest`; available means fundable).
   - If nothing is available, the result is -1. The lane holds no new destination and funds nothing; that RP is the
     empty-lane rule's case (the reserve branch).
   - Goals and the fallback are restricted to the Technology tree, so a civic is never chosen and RP never pays a civic.
4. **Ordering** (`OrderByCost`): the included indices by (cost, index), a stable insertion that moves past strictly
   higher costs only. Its first element is the argmin of the existing ascending scans.
5. **After the gate opens**, nothing forces a return. The lane keeps a fundable filler until it completes. The next
   decision then finds the gated goal fundable and takes it by the existing order. The lag is bounded by the remaining
   cost of the filler the lane holds when the gate opens (§4.1, test E).

Timing note: the technology lane decides before the Civics lane in `DecideAll`, so fundability reads the Civics
destination from the state before this turn's civics order. This matters only for a free-lane assist and is the order the
AI already uses.

## 4. Evidence

### 4.1 Rig tests — `Sim.Tests/Systems/AiGateAwareGoalTests.cs` (12)

AI Empire of 10,000 people (1,000 RP and 1,000 CP per dt-10 turn). Goals come from the tax requirement and road classes.
The sweep is on.

| Test | Pins |
|---|---|
| A blocked by sweep cohort | off: the destination is kept and the lane waits (today); on: re-targets to the road goal, funded the same step |
| A within a goal | a gated cheapest sink (route via a civic) is skipped for the same goal's fundable sink; off picks the gated one |
| A no churn | a fundable held destination (goal or filler) is never switched |
| A from an empty lane | when the existing choice is fundable, on = off |
| B nothing available | no order; the lane funds nothing; the gated destination is kept; law receives CP only; empty lane → -1 |
| C long wait (30-turn gate) | on: no step funds nothing while a technology is available; off: 29 waiting steps; on: 0 |
| D two successive gates | full traces pinned; off waits 5 + 5 steps; on 0; the residual is steps with no technology available at all |
| E first turns after the gate opens | off: lag 0 (held through the gate); on: lag 1 turn (the held filler completes first) |
| F tie-dense (2) | `OrderByCost` over dense ties; bit-equal goals → lower ordinal; bit-equal sinks → lower key |
| G player | a Player polity holding a waiting destination gets no order |
| G civics lane | a waiting civic destination is kept; an empty Civics lane takes its goal sink on and off |

**Mutants** (each a focused run of the 12 tests, bound 600 s, against a clean ~1 s run; all killed by semantic tests):

| Mutant | Killed by |
|---|---|
| M1 held-destination fundability check removed | A blocked, A within a goal, C, D, E, F behaviour |
| M2 `TechnologyFundable` always true | A blocked, A within a goal, C, D, E, F behaviour |
| M3 the walk ignores fundability | A within a goal |
| M4 `OrderByCost` non-strict (`<=`) | F ordering, F behaviour |
| M5 the switch acts on any lane | G civics lane |
| M6 fallback over both trees | B |

### 4.2 Canonical world — `ResearchEconomyMeasurement.GateAwareGoals_FourSeeds` (SIM_MEASURE=1)

Setup:
- The canonical world (shipped worldgen, one AI Empire), seeds 1, 2, 42 and 7, 1,630 turns, on `ResearchPacingRunner`.
- **route** is overflow ON with the switch absent; **route-gate** is overflow ON with `aiGateAwareGoals: true`. Nothing
  else differs.
- A step is WAITING when the technology lane holds a destination and `Fund` gives it no current node. EMPTY RP is a
  step's RP when the lane funds nothing. Both are reconstructed with the engine's planner; there were 0 reconstruction
  mismatches on every run.
- Raw summaries: `research-cost-pass/data/gate-aware/summaries.txt`.

Technology waiting steps by polity Age (four seeds pooled; no other Age has any):

| Polity Age | 3 | 5 | 6 | 7 | Total |
|---|---:|---:|---:|---:|---:|
| route (switch absent) | 4 | 24 | 24 | 120 | **172** |
| route-gate (switch on) | 0 | 0 | 0 | 0 | **0** |

Per seed, route: s1 7 / 11 / 21 (Ages 5, 6, 7); s2 4 / 11 / 13 / 59 (Ages 3, 5, 6, 7); s42 4 / 17 (Ages 5, 7);
s7 2 / 23 (Ages 5, 7). Every one of these steps is a `civic-sweep-cohort` wait. The destinations were road_paved,
heavy_plough, corned_powder and germ_theory. route-gate: 0 on every seed.

RP lost to EMPTY technology lanes (pooled, and as a share of the RP generated in that Age):

| Polity Age | 3 | 5 | 6 | 7 | All Ages |
|---|---:|---:|---:|---:|---:|
| route | 6,337 (0.7 %) | 135,280 (10.2 %) | 211,427 (5.0 %) | 1,356,097 (23.0 %) | 1,709,141 (2.82 %) |
| route-gate | 0 | 0 | 0 | 0 | **0** |

In route-gate the technology lane never had "no destination, nothing available" and never had "no destination, a
fundable node" (0 steps on every seed). The capped reserve would therefore receive no technology RP in this
configuration.

Total RP lost, surplus included (pooled): route 50.78 % → route-gate 48.58 %. By Age, route → route-gate:

| Age | A1 | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---|---|---|---|---|---|---|---|
| route | 3.5 % | 2.4 % | 2.3 % | 6.9 % | 14.9 % | 7.7 % | 26.7 % | 8.2 % | 66.2 % |
| route-gate | 3.5 % | 2.4 % | 1.8 % | 5.9 % | 8.0 % | 7.4 % | 8.4 % | 9.1 % | 63.6 % |

The remaining loss is SURPLUS: RP past a funded node, chiefly in Age 9, where each node and level is paid at most once
per step (ADR-050 §2.3).

Age entered (effective turn), route → route-gate:

| Seed | A4 | A5 | A6 | A7 | A8 | A9 |
|---|---|---|---|---|---|---|
| 1 | 664 → 664 | 697 → 697 | 767 → 768 | 904 → 920 (+16) | 1036 → 1017 (−19) | 1139 → 1116 (−23) |
| 2 | 713 → 695 (−18) | 753 → 745 (−8) | 832 → 840 (+8) | 962 → 991 (+29) | 1134 → 1119 (−15) | 1246 → 1198 (−48) |
| 42 | 673 → 673 | 707 → 707 | 774 → 780 (+6) | 882 → 882 | 992 → 976 (−16) | 1081 → 1065 (−16) |
| 7 | 672 → 672 | 708 → 708 | 769 → 770 (+1) | 874 → 874 | 997 → 980 (−17) | 1094 → 1072 (−22) |

- Age 9 comes 16–48 turns sooner.
- Age 7 comes LATER on seeds 1 (+16) and 2 (+29). The Age-7 core milestone (printing_press) and corned_powder complete
  later, because the Age-5 civics cohort they are swept on completes later (s1 877 → 896, s2 945 → 975). The civics lane
  funds a node on every step in both arms, so the civics are completed in a different order. The cause of that
  reordering (the Civics lane's goal choice reads the technology state) was NOT traced in this packet.
- End state: 525 technologies and 480 civics on every run in both arms. Repeatable levels are higher in route-gate on
  every seed (339 → 352, 252 → 301, 381 → 388, 368 → 377).

**Determinism:**
- Every route-gate run replays to the same final hash from its order log in a fresh executor, with no AI and no
  harness.
- The save/load round trip at turn 815 is equal on every run.

**Context, overflow OFF** (the "off-gate" job, switch on, against the recorded OFF trial in
`research-cost-pass/data/trial/trial-summaries.txt`):
- Waiting steps fall from 74 (13, 42, 11, 8) to 0.
- EMPTY RP falls from 696,361 to 0.
- Total RP lost falls from 54.5 % to 53.7 %.

### 4.3 Switch-off identity

- **Code.** The off path returns before any new code runs. `DecideLane` keeps a live target when `GateAware` is false,
  exactly as before. `ChooseDestination` calls `FundableDestination` only when `GateAware` is true. The rig content and
  the runner write `aiGateAwareGoals` only when it is on.
- **Canonical world.** The four route runs of §4.2 (switch absent, current tree) reproduce, bit for bit, the final world
  hashes recorded by the overflow trial before this change: `657cd933…` (s1), `bcd6d7f1…` (s2), `64a231ec…` (s42) and
  `07a6c037…` (s7) (`data/trial/trial-summaries.txt`).
- **Suites** (Release, switch absent everywhere except the new tests). Every existing AI and research test and every
  golden passes unchanged:
  - Sim.Tests Kernel partition: 528 passed, 18 skipped, 0 failed;
  - Sim.Tests other partition: 2,416 passed, 14 skipped, 0 failed (the 12 new tests included).
  - Sim.Ui.Tests: 1,115 passed, 0 failed.

## 5. Remaining waste

On the canonical world, the switch eliminates the waiting completely: 172 → 0 steps, and 1,709,141 → 0 EMPTY RP with
overflow ON. A technology was always available during every gate, so the planner never stalled there.

A stall remains possible by construction. When no technology at all is available while a gate is shut, the lane funds
nothing. This is test B, and the residual steps of test D (turn 5; turns 13–17). That RP is the capped reserve's case
(the other branch); this rule adds no reserve and no unlimited bank.

This rule cannot recover:
- the SURPLUS loss (48.58 % of RP generated, overflow ON; mostly Age 9);
- the lag after a gate opens. The lane finishes the fundable destination it holds before returning; this is 1 turn in
  test E, and is bounded by that destination's remaining cost.

## 6. Blast radius if adopted

- Only AI research orders change, and only on steps where the technology lane would otherwise wait. The engine, the
  save schema and the player's research are unchanged.
- Adopted with overflow (or alone), Ages move, so the pacing baseline must be refreshed with the switches that are adopted.
- Golden traces of AI runs on the configuration that enables it move.

## 7. Status

PROPOSED, OFF. Raw run summaries: `docs/m6-plan/knowledge-redesign/research-cost-pass/data/gate-aware/summaries.txt`.

## Combined trial (2026-10-26)

Measured together with the other two switches of the Director's 2026-10-26 trial configuration (overflow + capped
reserve {3, 1} + gate-aware goals), against the new default baseline (CR-025 content):
`docs/m6-plan/knowledge-redesign/research-cost-pass/combined-trial.md`.

| Measure | Default | Trial configuration |
|---|---|---|
| RP lost | 55.7 % | 49.5 % |
| Technology waiting steps | 66 | 0 |
| Casual player's lag (Age 9) | +36 turns | +2 turns |
| Deliberate idling (Age 9) | — | −58 turns (no advantage) |
| Age 9 entry | — | 76–98 turns sooner |

Replay and save/load are exact. **Status:** still PROPOSED and OFF by default. Adoption and the pacing recalibration
wait for the Director's review of the combined baseline.
