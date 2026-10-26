# ADR-053 — Civics gate goals for the AI (switch, shipped OFF)

**Status: PROPOSED — behind `research.json` `tuning.aiCivicsGateGoals`.** The switch is absent from the shipped
`research.json`, so it is false and the AI's research rule is unchanged (§5). It is enabled only by the trial overlay
`docs/m6-plan/knowledge-redesign/research-cost-pass/trial/civics-gate-trial.json` (`{"tuning": {"aiCivicsGateGoals": true}}`,
applied by `ResearchPacingRunner.ApplyOverlay`). Adopting it is the Director's decision.

## 1. The problem (measured)

`research-cost-pass/pacing-recalibration.md` (Civics Age 7 row): with the two lanes, destination routing and the Age
sweep (ADR-048), a node of content Age a ≥ 3 is eligible only when every sweep-cohort node of Age a − 2 in BOTH trees is
complete. With no milestone goal the AI's Civics lane takes the `Cheapest` available civic. Age-7 civics open with Age 5
and all 46 are cheaper than `caliphate`, the dearest Age-6 civic, so `caliphate` is bought last — 100–277 turns after the
rest of Age 6 in pass 3. Meanwhile every Age-8 technology is swept on it and the technology lane has nothing to fund: 157
turns at the reserve cap, 22.9 % of Age-7 RP lost. ADR-052 (gate-aware technology goals) cannot help: there is no
technology to switch to.

## 2. The ruling (Director, 2026-10-26)

"Civics gate rule — Approve for a separate trial. The caliphate stall is a structural goal-selection problem, not a cost
problem." "When technology cannot advance because an Age requires a specific civic, the civics planner should recognize
that civic as a high-priority goal. Keep the policy generic and derived from actual eligibility rules, rather than
hard-coding `caliphate`. Test that it selects the correct blocker, respects civic prerequisites, and does not starve
essential economic development." Defaults unchanged: a new switch, absent in the shipped `research.json`; no golden moves.

## 3. The rule (switch on)

It acts only with the two-lane model (`tuning.civicsPoints`), `tuning.destinationRouting` and `tuning.ageSweep`, on an AI
polity's CIVICS lane (`CommandSource.Ai`). The Technology lane, the one-pool model and player polities are untouched. No
node id appears in code.

1. **Trigger** (`AiResearchPolicy.CivicsGateBlockers`) — "technology cannot advance because an Age requires a civic":
   - the polity has NO available technology node (its availability mask, repeatable levels included); and
   - some technology fails its eligibility ONLY on the Age sweep, and its sweep cohort (content Age a − 2) still holds an
     incomplete sweep-cohort CIVIC. "Only on the sweep" is THE eligibility predicate,
     `ResearchQuery.IsAvailable(content, i, completed, stage, sweptAges)`, evaluated with every Age treated as swept (so
     completion, the subtree stage, the authored prerequisites and the repeatable exhaustion rule all still apply), plus
     the level cap. Nothing is re-implemented: the sweep is the only input replaced.
2. **Blockers**: the incomplete sweep-cohort civics (`IsSweepCohortMember`: never a repeatable) of the LOWEST such cohort
   Age — the cohort whose completion unblocks technology soonest.
3. **Choice** (`CivicsGateDestination`): the blocker with the lowest remaining route cost — an available blocker costs its
   remaining cost; otherwise `ResearchRoutePlanner.Plan(...).RemainingCost`, its prerequisites included — composite key
   (cost, index) via `OrderByCost` (ADR-052). The DESTINATION is chosen, not a node, so the engine's planner funds the
   blocker's prerequisites first: civic prerequisites are respected by construction.
4. **Re-decide while held** (`DecideLane`): while the trigger holds, a held civic destination that is not a blocker is
   switched to the chosen blocker (its progress stays on its nodes, as in ADR-052); a held blocker is kept (no churn
   between blockers). An empty Civics lane takes the chosen blocker (`ChooseDestination`).
5. **Otherwise** — the trigger does not hold — the decision is exactly today's (on and off identical). This is the
   "does not starve" guarantee: the Civics lane is diverted only while the technology tree is FULLY stalled on the sweep,
   and only to that bounded cohort. Its goals (core milestones, taxation, supporting milestones) resume the moment a
   technology is available again.
6. **Never**: RP pays a civic (the rule only names a Civics-lane destination, funded by CP); a player's research is
   chosen; a repeatable or a node outside the lowest gating cohort is a blocker.

Timing: `DecideAll` decides the Technology lane before the Civics lane on the same state; the trigger reads only
completed knowledge and availability, so the order does not matter to it.

## 4. Evidence — `Sim.Tests/Systems/AiCivicsGateGoalTests.cs` (16)

Rigs: an AI Empire of 10,000 people (1,000 RP and 1,000 CP per dt-10 turn), the sweep, routing and two lanes on, no Age
content, no road goal.

| Test | Pins |
|---|---|
| Blocker: Age-1 cohort | technology stalled on law (A1, 3000): on → law; off → pol (A2, 500), the cheaper unrelated civic |
| Blocker: two cohorts | t3 swept by {law}, t4 by {pol, pol2}: the lowest cohort (law) wins; with law known t3 is available and nothing triggers; with t3 known the Age-2 cohort becomes the blockers |
| Blocker: tie-dense (2) | six bit-equal blockers → lowest index; dense ties at two costs → cheaper cost, then lower index; progress lowers remaining cost; a routed blocker (prerequisite + node) bit-equal to an available one → lower index, both orders |
| Blocker: exclusions | only the cohort civic is a blocker — never a trunk repeatable, a civic repeatable, a technology or another Age's civic |
| Prerequisites (2) | the blocker canon (A2) needs rite (A1, not a blocker): the destination is canon; the engine funds rite (turn 0), then canon (turns 1–2); full traces of both lanes; completion turns rite 0, canon 2, fair 3, t4 4 |
| Does not starve: every state | exhaustive: every prerequisite-consistent knowledge state of a 6-node rig × held technology / civic destinations × a civic tax goal in progress or not (≥ 100 states with an available technology): whenever the trigger does not hold (always when a technology is available), `DecideAll` on == off; the rule does act in trigger states |
| Does not starve: civic goal | a held, in-progress civic tax goal is kept while a technology is available; diverted to the blocker only once the technology tree stalls; its progress is kept |
| Held | on: a held non-blocker is re-decided to the blocker; a held blocker is kept, even a dearer one; off: both kept |
| End to end (off pinned, on) | off: pol, pol2, then law; the technology lane funds nothing until turn 5 (today's stall); on: law first, t3 funded from turn 3 — two turns sooner, turn-exact |
| Trigger: authored prerequisite | a technology failing its prerequisite AND the sweep does not trigger; once its prerequisite is known, it does |
| Trigger: no cohort civic | a swept technology whose cohort holds only an outstanding technology does not trigger; an available technology never triggers |
| Player / technology lane | a player polity gets no order; the AI's Technology lane is never given a civic |
| Switches | without the sweep, or without routing, today's choice |
| Loader | parsed true; absent = false; the shipped `research.json` has no `aiCivicsGateGoals` key; the trial overlay sets exactly that key |

**Mutants** (each a focused run of the 16 new + 12 ADR-052 tests, bound 18 s = 3 × the ~6 s clean focused-suite
wall-clock; none hung; every one killed by a semantic test):

| Mutant | Killed by |
|---|---|
| M1 trigger's "no available technology" condition dropped | Does not starve (every state), Does not starve (civic goal), Two cohorts |
| M2 highest instead of lowest cohort | Two cohorts |
| M3 no re-decide while held | Held, Does not starve (civic goal) |
| M4 trigger ignores the rest of the eligibility predicate (any incomplete unavailable technology) | Trigger: authored prerequisite |
| M5 cost ignored (first blocker by index) | Tie-dense (equal-cost), Held |
| M6 cohort-civic condition dropped | Trigger: no cohort civic |
| M7 a held blocker is re-decided too (churn) | Held |

## 5. Switch-off identity

- **Code.** `CivicsGate` is false unless the switch is on, so `DecideLane`'s held-target branch and `ChooseDestination`
  run exactly the code they ran before. The rig writes `aiCivicsGateGoals` only when on (null otherwise); the runner never
  writes it.
- **Suites.** Every existing AI and research test and every golden passes unchanged (Release; see the packet hand-back).

## 6. Blast radius if adopted

- Only AI Civics-lane research orders change, and only on steps where the AI's technology tree has no available node and
  is swept on a civic. The engine, the save schema and the player's research are unchanged.
- Civics may be completed in a different order; Ages may move. The pacing baseline must be refreshed with the switches
  that are adopted. Golden traces of AI runs on a configuration that enables it move.
- The trigger is late by design: it fires only once the technology tree is fully stalled, so the rule shortens a stall
  but does not prevent its first step — the blocker is first chased on the first step with no available technology (the
  held non-blocker civic is switched that same step; its progress stays on its node). The stall that remains is the
  blocker's own route cost at the Civics lane's rate.

## 7. How to measure

Run `ResearchPacingRunner` on the canonical world (seeds 1, 2, 42, 7; 1,630 turns) with the trial overlays —
`research-economy-trial.json` alone, and `research-economy-trial.json` + `civics-gate-trial.json` — and compare: the
`caliphate` completion turn against the rest of civics Age 6, technology steps with nothing fundable (and reserve-at-cap
steps) by polity Age, RP lost by Age, Age entry turns (A7–A9), and replay / save-load equality. Not run in this packet
(CPU reserved for the Director's four-seed run).

## 8. Status

PROPOSED, OFF. Queue: `docs/queue.md` (2026-10-26, ADR-053).
