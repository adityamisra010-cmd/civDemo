# RP / CP lifecycle — every way points are banked, spent, lost, blocked or misdisplayed

**Status:** evidence for the Director's review (research cost pass). No engine, content or UI code changed; the shipped
`research.json` runs with `tuning.researchOverflow` and `tuning.researchOverflowLane` OFF. Engine commit `d34bed5`.
Overflow itself is measured in `overflow-comparison.md` and is not repeated here.

**Tests (all pin the CURRENT behaviour, not a desired one):**

- `Sim.Tests/Systems/ResearchPointLifecycleTests.cs` — 19 facts, cases (a)–(l); (l) is a skipped measurement (about 3.5 minutes; run it explicitly). Rig: one player polity, 10 000 people,
  R = `ResearchQuery.ResearchPointPool` = 1 000 RP a step, G = `ResearchQuery.CivicsPointsMilli` = 1 000 000 milli-CP
  (1 000 CP) a dt-10 step; two lanes, destination routing, overflow off (the shipped switches). "RP lost" in a step =
  R − Σ technology progress added (a node completed or levelled in the step counts its EffectiveCost on PREV).
- `Sim.Ui.Tests/ResearchPointDisplayTests.cs` — 4 facts, case (k) at the view-model level (ResearchSnapshot, the action
  surface's research line) on the stepped real world (`SteppedWorldFixture`, shipped content).

Run: `dotnet test Sim.Tests --filter FullyQualifiedName~ResearchPointLifecycleTests` and
`dotnet test Sim.Ui.Tests --filter FullyQualifiedName~ResearchPointDisplayTests`.

## The rule in one paragraph

RP is per turn and not stored anywhere (D-044 R20-D). Each step, per lane, `ResearchRoutePlanner.Fund` picks ONE node: the
destination if available, else the cheapest eligible route node **of the lane's own tree**, else (no destination, or a
route with no node of its tree) a free-lane assist on the other lane's route. RP goes to that node capped at its
remaining cost (`ResearchSystem.Credit`, lines 405–416); the loop stops after one node with overflow off (line 287).
If no node is picked (`fund[0] < 0`, line 278) nothing runs. CP accrues into the bank every step (lines 298–323), the
Civics lane draws `min(balance, remaining)` from it (one civic per step, line 313), and whatever is not spent stays
banked. A completion (a level of a repeatable included) clears its lane's destination (lines 339–340).

Neither overflow mode changes a step whose lane funds NOTHING: overflow continues only after a first node is paid in full
(`if (fund[0] >= 0)` at line 278 guards the whole loop; `NextFunded` is reached only from inside it). This is why several
losses below are "neither".

## Case table

R = 1 000 RP, G = 1 000 CP in the rig. "Route" = `researchOverflow`; "Lane" = `researchOverflow` + `researchOverflowLane`.

| Case | Test | Classification | Code responsible | Measured | Route | Lane |
|---|---|---|---|---|---|---|
| (a) surplus past a completed node | `A_SurplusPastCompletion_IsDiscarded` | **WASTED** | `ResearchSystem.Credit` caps at the remaining cost (l. 409–411); loop `break` with overflow off (l. 287) | 0.3 R spent, **0.7 R lost** (700 RP) | partly — only when another node of the route / assist is eligible on PREV | yes (cheapest available node of the lane) |
| (b) step with no technology destination | `B_NoTechnologyDestination_…` | **WASTED** | `fund[0] < 0` → throughput skipped (l. 278) | **R lost** per step; CP fully banked | no | no |
| (b) what the player sees | same + `Snapshot_ShowsTheRpPoolUnchanged_…` (UI) | partial signal only | InfoQuery `LearningCard` l. 1913; `ProgressionScreen` capsule "Research idle"; `ResearchSnapshot.PointsPerTurn` | Glass Box: "Idle: 1,000 research points a turn go unused"; screen: "Research idle - choose an available node"; `ResearchSnapshot` has no idle/lost field, shows the full pool as "Research per turn"; `Route` = None | — | — |
| (c) switch destination mid-node | `C_SwitchingOrClearing_…` | **correctly banked** (partials kept on every node) | progress rows are never removed except on completion (l. 338) | x R and y R both kept; returning to x resumes at R | — | — |
| (c) clear the destination mid-node | same | **WASTED** (the cleared step) | `fund[0] < 0` (l. 278) | **R lost** for the step after the clear; no progress lost | no | no |
| (d1) tech destination whose route has only a civic, Civics lane free | `D1_TechDestinationWaitingOnACivic_…` | **blocked spending → WASTED RP**; CP correctly spent (assist) | `PickCurrent` filters by lane (`ResearchRoutePlanner` l. 140) → tech current −1; Civics lane assists (l. 117–123) | **R lost** per step; G → the civic, bank 0 | no | no |
| (d2) same, Civics lane busy with its own destination | `D2_…WithACivicsDestinationOfItsOwn_…` | **blocked spending → WASTED RP**; the civic also waits | assist needs the other lane to have a PLAN (l. 120); an available civic destination has none | **10 R lost** over the 10 steps until cz then cv complete (rig) | no | no |
| (e) civics destination waiting on a technology, tech lane busy | `E_CivicsDestinationWaitingOnATechnology_…` | **correctly banked** (blocked spending, no loss) | same planner rule on lane 1; CP spend loop never runs (l. 309) | CP spent 0, bank +G per step; when the tech lane is freed it assists with RP (R spent) | — | — |
| (f) the step after a destination completes, no new order | `F_TheStepAfterADestinationCompletes_…` | **WASTED** (player) | completion clears the lane (l. 339) → next step `fund[0] < 0` | **R lost** (1.7 R of 2 R over the two steps). The AI re-orders before the next step (see (l)) | no | no |
| (g1) finite tree exhausted, no repeatables | `G1_FiniteTreeExhausted_NoRepeatables_…` | **WASTED** (structural, nothing exists to buy) | `IsOpenTarget` refuses completed nodes (`ResearchQuery` l. 473); order ignored (l. 200) | **R lost** every step forever; CP banked | no | no |
| (g2) finite tree exhausted, levelled repeatable | `G2_…WithALevelledRepeatable_…` | correctly spent while named; **WASTED** the step after each level unless re-ordered | a level is a completion → destination cleared (l. 339) | level 1 after 2 steps (2 R spent); next step R lost; level 2 costs 2.5 R | no | no |
| (h) repeatable level surplus | `H_RepeatableLevelSurplus_…` | **WASTED** | one node per step (l. 287) — a node is paid at most once per step even with overflow | step 1: 0.25 R spent, 0.75 R lost; step 2: 0.3125 R spent, 0.6875 R lost (**1 437.5 of 2 000 RP lost**) | no (destination paid; nothing else on a route) | yes, if ANOTHER node of the lane is available |
| (i1) CP surplus | `I1_Cp_SurplusIsBanked_…` | **correctly banked**; one civic per step = deferred, not lost | spend loop `break` (l. 313) | c1 300 CP: bank 700 000 milli after step 1 (c2 untouched); 1 700 000 after step 2; 2 600 000 after c2; identity holds every step | route: spends further on route nodes | lane: spends further on the tree |
| (i2) CP completion rounding | `I2_Cp_ACompletingSpendRoundsUp…` | **WASTED** (< 1 milli-CP per completion) | `SpendCivics` `ceil(remaining × 1000)` (l. 454–456) | cost 100.0004 CP: 100 001 milli spent, 100.0004 credited → **0.6 milli-CP** gone | no | no |
| (j) save / load | `J_SaveLoad_Preserves…` | correct | `Snapshot.Save` / `Snapshot.Load` | bank 3 000 000 / generated / spent, both partials, both destinations, level 1, completed rows equal; same WorldHash; same routes; same next step hash | — | — |
| (k1) Glass Box research card, destination = funded node | `K1_LearningCard_EveryNumber_…` | correct | InfoQuery l. 1901–1925 | "Researching X: 1,000 of 2,500 research points, +1,000 a turn. Civics: no civics target; 1,000 civics points banked, +1,000 a turn." — every number = ResearchQuery | — | — |
| (k2) Glass Box card with routing | `K2_LearningCard_WithRouting_…` | **DISPLAY DISCREPANCY** | `LearningCard` reports the DESTINATION row's progress (l. 1901–1905); `NodeCard` "Being researched" needs `target && available` (l. 364) | card: "Researching D: 0 of 2,000" while the step put R into x; x's card: "Open to research now", d's: "Locked" | — | — |
| (k3) Glass Box card, free tech lane assisting | `K3_LearningCard_AFreeTechLaneThatAssists_…` | **DISPLAY DISCREPANCY** (false waste claim) | `LearningCard` keys "Idle" on the absence of a target row (l. 1913), not on `Route.Current` | card: "Idle: 1,000 research points a turn go unused" while R is spent on the assisted node | — | — |
| (k4) Glass Box card, lane waiting on the other lane | `K4_LearningCard_ALaneWaiting…` | **DISPLAY DISCREPANCY** (hides the waste) | same (l. 1901) | card: "Researching TD: 0 of 2,000 research points, +1,000 a turn." while R is lost every step. (The progression screen's route lines DO say "Current research: none - waiting on the other lane" — `ProgressionScreen.cs` l. 446.) | — | — |
| (k5) CP a turn shown at turn 0 / first turn of an era band | `K5_CpPerTurnShown_…` (rig), `CpPerTurnShown_AtTheFirstTurnOfANewEraBand_…` (UI) | **DISPLAY DISCREPANCY** | every CP-a-turn figure uses `world.Clock.DtYears` = the dt of the step that PRODUCED the world (`TurnExecutor` l. 138); the next step uses the era table's dt at the date (l. 116). Sites: InfoQuery l. 361 and 1922, `ResearchNodeViews.cs` l. 222, `ActionSurfaceModel.cs` l. 630 | turn 0: shown "+0 a turn", first step accrues 1 000 CP (rig). Real world at −1500 (Bronze/Iron band): shown the dt-10 accrual, the step accrues the dt-5 accrual (half, ±1 milli). Also wrong for the "~N turns" estimates of civics on those turns. 6 turns per campaign (turn 0 + 5 band starts) | — | — |
| (k6) every other RP/CP number on the screens | `Snapshot_EveryRpAndCpNumber_…`, `ActionSurfaceResearchLine_…` (UI) | correct | `ResearchSnapshot.Build`, `ActionSurface` | PointsPerTurn = pool; CivicsBalance = BalanceMilli / 1000; per-node Progress and EffectiveCost; both routes (destination, current, next, remaining, assisting) = `ResearchQuery.Route`; the next production step accrues exactly the CP shown (inside a band) and adds exactly the shown RP to `Route.Current` | — | — |
| (l) AI on the canonical world | `L_Ai_CanonicalWorld_Seed42_400Turns_…` | see below | `AiResearchPolicy.DecideLane`, `AiOrders.For` | see below | — | — |

## (l) The AI — canonical world, 1 AI Empire, seed 42, 400 turns

Each step the engine's funding was reconstructed from PREV plus that step's AI orders (`ResearchQuery.Destinations`, the
orders applied as `ResearchSystem` applies them, `ResearchRoutePlanner.Fund`) and checked against the step's outcome
(0 mismatches in 800 lane-steps). Runtime 3.5 min (one core).

| Lane | no destination, fundable nodes exist | no destination, nothing fundable | waiting (destination, no fundable node) | assisting | funded |
|---|---:|---:|---:|---:|---:|
| Technology | **0** | 0 | 0 | 0 | 400 |
| Civics | **0** | 0 | 0 | 0 | 400 |

- RP generated 31 081.6, spent 27 931.4, **lost 3 150.2 (10.1 %)** — ALL of it surplus past a completed node (case a);
  0 RP lost on idle or waiting steps. (Consistent with `overflow-comparison.md` OFF: 3.5 / 7.5 / 11.6 % in Ages 1–3.)
- CP generated 20 949.895, spent 20 949.895, banked 0 at turn 400 — the Civics lane was never idle, so nothing sat in the
  bank. Civic progress credited 20 949.878 CP: the ceil-to-milli spend rounding (case i2) cost **17.4 milli-CP** in total.
- Why no idle step: `AiOrders.For` runs on the state the step reads, and `DecideLane` re-orders whenever its lane holds
  no live destination; the order is stamped with the current turn, so it applies in the very step after a completion
  (case f's loss does not happen to the AI). A levelled repeatable destination (case g2) is re-ordered the same way.
- The 400-turn window ends in Age 3. `overflow-comparison.md` finding 3 reports technology turns with NO eligible
  technology in polity Ages 5 and 7 (the Age-sweep pincer, i.e. cases d / g) — outside this window; this measurement
  does not refute or extend it.

## Findings

1. **The only material RP loss is per-step surplus (a, h) and the lane funding nothing (b, c, d, f, g).** The AI suffers only
   the first kind (10.1 % of RP in Ages 1–3, all surplus past a completion); a player suffers the second kind on every
   turn they leave a lane without a fundable destination — after every completion (f), after every repeatable level
   (g2), and while a destination waits on the other tree (d1, d2).
2. **Overflow cannot recover a step whose lane funds nothing.** `ResearchSystem` enters the throughput loop only when the
   lane's first node exists (`if (fund[0] >= 0)`, l. 278); `NextFunded` — route-only or the lane variant — is consulted
   only after a node is paid in full. So (b), (c-clear), (d1), (d2), (f), (g1), (g2's post-level step) are lost under
   every overflow mode. The lane variant fixes (a) and (h) only. Fixing the empty-lane cases needs a different rule
   (e.g. a default "cheapest available" fund when the lane has nothing, or a destination that persists through a
   repeatable's levels) — a design decision, not part of ADR-050.
3. **CP is never lost** except the ceil-to-milli rounding of a completing spend (< 1 milli-CP per civic; 17 milli-CP over
   400 AI turns). One civic per step defers spending (the bank carries it); blocked civics (e) bank without loss.
4. **Display discrepancies (4), all in the Glass Box research card or the CP-a-turn figure — none in the progression
   screen's route lines or node numbers:**
   - (k2) with routing the research card reports the DESTINATION's progress ("Researching D: 0 of 2,000") while the step
     funds a route node, and the funded node's own card says "Open to research now";
   - (k3) a free technology lane that assists the civic route is called "Idle: … research points a turn go unused" while
     its RP is spent;
   - (k4) a lane that waits on the other lane (all RP lost) is called "Researching …" with no waiting or idle word;
   - (k5) every CP-a-turn number uses the clock's LAST dt: "+0 a turn" at turn 0, and the previous band's dt on the
     first turn of each era band (shown 2× the real accrual at −1500). Six turns per campaign; it also skews the
     civics "~N turns" estimates on those turns.
   No surface shows an RP-lost figure: surplus past completion (a, h) is invisible everywhere.
5. **Secondary observation — FIXED after this report:** both tests now add their nodes to `Spec.Recursive`, assert the
   nodes are levelled, and the single-node test also checks that a second step buys level 2. The two-node test moved to
   the lane variant, because a finite destination may not require a repeatable (loader refusal). What this report found
   was: in `ResearchOverflowTests`, `Overflow_ARepeatableGainsAtMostOneLevelPerStep`
   and `Overflow_TwoCheapRepeatables_…` give their nodes a `LevelRule` but do not add them to `Spec.Recursive`, and the rig
   writes `levels` only inside the repeatable descriptor of a recursive node — so those nodes are complete-once, not
   levelled, and `Level == 1` holds trivially. The lifecycle tests (G2, H, J) add the node to `Recursive` and assert
   `IsLevelled`.
6. **Theoretical only:** `ResearchQuery.Route` builds its availability without the level cap (`AvailableMask(content,
   completed)`), the engine with it; they differ only for a levelled node at level 1 000 (`MaxRepeatableLevel`),
   unreachable in play.

## Resolution summary

| Loss | Route-only overflow | Lane variant | Neither |
|---|---|---|---|
| (a) surplus past completion | partly (only other eligible route / assist nodes) | yes | |
| (h) repeatable level surplus | no | yes, if another node of the lane is available | |
| (b) no destination, (c) the cleared step, (f) the step after a completion | | | yes |
| (d1, d2) destination waiting on the other tree | | | yes |
| (g1) finite tree exhausted, (g2) step after a level | | | yes |
| (i2) CP rounding | | | yes (negligible) |
| (k2–k5) display | | | yes (UI work) |
