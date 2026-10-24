# ADR-048 — The Age sweep and destination research (routing)

**Status: IMPLEMENTED on branch `t2-routing` (M6 tranche 2, routing track), both switches shipped OFF; awaiting the
Director's review and the integration step that flips them.** Authority: the tranche-2 working contract
(`docs/m6-plan/knowledge-redesign/tranche2-contract.md` §2, §3, §9), governance record CR-024 and Director ruling D-063.
No table, no schema version and no kernel surface changes: the Age sweep is a predicate, and the destination is the
existing `ResearchTargets` row. One order kind (6, `SetResearchTarget`) gains a meaning under a switch (§3.1).

## 1. Decision

1. **Age sweep.** A node of content Age a ≥ 3 is ELIGIBLE only if every sweep-cohort node of Age a − 2, in BOTH trees, is
   complete. Ages 1 and 2 are exempt. Nodes carrying a repeatable descriptor (the `researchSets.recursive` set) are not
   cohort members. It reads the node's content `age` label — never the polity's Age, never a date.
2. **Destination research.** `SetResearchTarget` may name any INCOMPLETE node; the target row becomes the lane's
   persistent DESTINATION; each step the lane funds the destination when it is available, otherwise the CURRENT node of
   the destination's route, planned by `ResearchRoutePlanner`. `ResearchQuery.Route` is the only route calculator.

## 2. The Age sweep — semantics

- **One predicate.** `ResearchQuery.SweepSatisfied(content, node, completed)` is evaluated inside
  `ResearchQuery.IsAvailable` — THE eligibility predicate — and `AvailableMask` evaluates it through a precomputed
  `SweptAges` (identical node by node; exhaustively tested over all 2^11 masks of the rig). Every availability path reads
  it: `ResearchSystem` (player and AI targets, credits, completion), city states (`AiResearchPolicy.Cheapest` over the
  mask), `ResearchQuery.TargetOrder` (the player's and the AI's order builder), `AvailableActionsQuery`, `InfoQuery`, the
  research screen (`ResearchSnapshot`), the CLI and the route planner. Nothing else decides availability.
- **Apart from prerequisites.** No prerequisite edge is generated. `ResearchQuery.SweepBlock` reports, for a node, whether
  the sweep applies, whether it blocks now, the cohort Age and the outstanding cohort nodes (key order). `InfoQuery`'s
  "why locked", the screen's lock reason (`LockReason.AgeSweep`) and its line ("Age 1 sweep: 4 nodes outstanding: …") read
  it; the missing-prerequisite reason is never used for it.
- **Monotone.** Completion only grows, so a node the sweep has released is never blocked again.
- **Accessor.** `ResearchNode.AgeNumber` (1..9) — the only Age reading added to the research engine.

### 2.1 D-044 R13 supersession

D-044 R13 ("research never reads Age") is superseded **for the sweep predicate only** (contract §9; D-063). The node's
`Age` doc comment and the `ResearchSystem` header now say so. Law 4 (no calendar gates) holds: the predicate reads
content labels and computed completion state, never a date or the polity's Age.

## 3. Destination research — semantics

### 3.1 Orders and the row

- With `tuning.destinationRouting`, `SetResearchTarget` (kind 6) admits any incomplete node
  (`ResearchQuery.IsDestinationAdmissible`; unknown or completed keys still change nothing). −1 and −2 still clear the
  Technology / Civics lane (CANCEL DESTINATION). The row is the destination; no new table, so saves keep their shape and
  the current node is re-derived from saved state.
- **Delivery is unchanged:** an order stamped turn t is applied by the step t → t+1, and that step already funds the new
  destination's current node (turn-exact pin: `TurnExactPin_ADestinationStampedT_FundsItsRouteInTheStepFromT`).
- The row is removed only when the DESTINATION completes. A destination that cannot be reached (impossible with a sweep-
  safe graph, but defended) is kept, and the query reports it infeasible with its blockers.
- City states issue no orders and keep their rule (cheapest available node) — they obey the sweep through the mask.

### 3.2 The route (`ResearchRoutePlanner.Plan`)

The route of destination D is the minimum-total-remaining-EffectiveCost SET of incomplete nodes whose completion makes D
eligible. A node's requirements: its authored prerequisite (AND / OR, cross-tree atoms included), the stage gate when it
is in a subtree and the stage is not reached, a recursive node's finite subtree, and — with the sweep — the outstanding
cohort of Age a − 2. Each required node brings its own requirements; shared nodes count once.

- **Exact up to 4,096 combinations.** The OR groups reachable from D over every alternative and not already satisfied
  are numbered breadth-first; if the product of their alternative counts is ≤ 4,096, every assignment is enumerated
  (lexicographic, first group most significant, alternatives in listed order) and its closure costed. Winner: composite
  key (total remaining cost, the ascending key sequence compared lexicographically, enumeration position).
- **Greedy beyond.** Each OR takes the alternative whose own closure is cheapest; ties to the alternative listed first —
  (closure cost, listed position). Closures are memoized per node.
- **Remaining cost** = EffectiveCost − progress (≥ 0), summed in ascending index order (bit-reproducible).
- **Labels.** PREREQUISITE when a route node is reached from D over authored-prerequisite (and recursive-subtree) edges
  inside the route; else STAGE when stage edges reach it too; else SWEEP(k), k the cohort Age that brings it in. A sweep
  node is never labelled a prerequisite. A node of the other lane while that lane has its own destination is shown as
  OTHERLANE (its structural cause is kept beside it).
- **Complexity bound.** Exact: at most 4,096 closures, each O(N + E) over N content nodes and E requirement atoms
  (≈ 4,096 × (N + E) atom visits; one plan from scratch on the shipped 563-node graph with the sweep on averages under
  20 ms in a debug build — 563 plans + orders in ≈ 10 s). Greedy: one memoized pass, O(N · (N + E)) set unions in the
  worst case. The ordered route (query only) is O(R² · c) for R route nodes.

### 3.3 Funding each step (`ResearchRoutePlanner.Fund`)

Per lane (one slot in the ADR-043 one-pool model): an available destination is funded itself; otherwise the CURRENT node
= the eligible route node of THIS lane with the smallest composite key (remaining EffectiveCost, key) — an ascending-key
scan replacing only on a strictly lower cost (tie-dense tested). A lane with no destination of its own funds the other
lane's route nodes of its tree by the same key (free-lane assist); while it has its own destination, those nodes are
otherLane blockers and the first lane may wait. Funding is computed from PREV, before credits — so `ResearchSystem` and
`ResearchQuery.Route` (which calls the same `Fund` over the same inputs) agree by construction.

### 3.4 `ResearchQuery.Route(world, content, polity, lane)`

Returns the destination, CURRENT (what the next step funds), NEXT (what the lane funds once the current node is
complete: the destination if it then becomes eligible), the ORDERED route (the funding rule applied repeatedly, both lanes
pooled), the BLOCKERS (every route node with reason ∈ {prerequisite, sweep(Age k), stage, otherLane}, its structural
cause and whether it is eligible now), the remaining cost (route + destination), feasibility, and — for a free lane — the
destination it assists. The UI and the AI read it; there is no other route calculator.

### 3.5 The AI

With routing on, an AI lane orders only when it holds no destination. Its goal choice is unchanged
(`ChooseGoal`); the goal's NODE — `AiResearchPolicy.GoalDestination`: the closure's sinks (incomplete closure nodes with no
incomplete dependent in the closure), the one with the cheapest planner route, ties to the lower key — becomes the
destination, and the same planner picks what to fund. With no goal, the cheapest available node of the lane (as before).

## 4. Switches and twins

| research.json `tuning.` | shipped | off (or absent) = |
|---|---|---|
| `ageSweep` | `false` | the pre-sweep availability exactly (exhaustive mask test; shipped-content mask test) |
| `destinationRouting` | `false` | the available-only target exactly: an order naming an unavailable node changes nothing |

Both ship false on this branch, so no golden moves; the integration step flips them. With both off the full suites are
byte-identical (every golden unchanged).

## 5. UI semantics (research screen, `Sim.Ui/Progression`)

- Every node's state, eligibility and lock reasons come from `ResearchSnapshot` (ResearchQuery); the sweep shows as its
  own lock line, never as a missing prerequisite.
- With routing on: the capsule reads DESTINATION — "<destination> - now: <current>"; the panel's ROUTE block shows
  Destination, Current research, Next on route, Remaining, then the blockers grouped by reason ("Prerequisites: …",
  "Research stage: …", "Age 2 sweep: 7 nodes outstanding: …", "Other lane (Civics): …"), all verbatim from
  `ResearchQuery.Route`; a click (or the panel button "Set as research destination") on any incomplete node issues the
  order `ResearchOrderFactory.SetTarget` builds (= `ResearchQuery.TargetOrder`); CANCEL DESTINATION issues the lane's
  existing clear order (−1 / −2) once (pending until End Turn).
- `AvailableActionsQuery` lists the destination action (Research Id 4, `research.set-destination`) — exactly the
  incomplete nodes not available now — only with routing on.

## 6. Tests

`Sim.Tests/Systems/ResearchAgeSweepTests.cs`, `Sim.Tests/Systems/ResearchDestinationTests.cs`,
`Sim.Ui.Tests/DestinationResearchUiTests.cs` — every sweep boundary, the exhaustive off/on mask equivalence, city states,
one missing / several AND prerequisites, the adversarial OR (cheapest route ≠ cheapest immediate prerequisite), shared
prerequisites (also across OR groups: exact beats greedy), the 4,096 bound, the sweep on the route, automatic
advancement, retargeting, cancel, save/load, replay equality, the turn-exact pin, tie-dense keys, two-lane assist and
otherLane, the AI's goal destination and planner, UI = engine.

## 7. Deferred

- The action-surface "Learning" card still shows the lane's target row (the destination) and its progress; the route
  detail lives on the research screen.
- Repeatable levels (contract §4) are another track; the sweep already excludes repeatable-descriptor nodes.
