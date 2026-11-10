# H-AP — M7 lane AP: concealment and preparation actions, ambushes, equal sides (D-082 DA6, DA7, DA8)

Date 2026-11-10. Clone `/home/user/m7w/AP`, branch `lane/AP`, base `7f0cb92` (the development branch
`claude/new-session-yavghm` as handed to this lane: lane MOR's final commit). ISOLATED PROTOTYPE AND DOCS ONLY: nothing in
`Sim.Core`, `Sim.Data`, `Sim.Cli`, `Sim.Tests` or `Sim.Ui` changed (D-079 Q5 OPEN); no unit data or cap changed; no permanent
project; no CI file. Files touched: `prototypes/m7-sprint1/` (solution `M7Proto.slnx` only) and `docs/m7-plan/`.

Status words: **MEASURED** (run in this lane, on this tree; the test or command is named), **DERIVED** (exact arithmetic, pinned by a
test), **INFERRED** (argued, not isolated by a run), **PROPOSAL** (lane AP's design under D-082's delegation; decided by nobody),
**READING** (an interpretation, both alternatives stated), **DD** (a quoted Director decision), **PLACEHOLDER** (a magnitude with its
direction, no ratified value).

Authority: D-082 DA1 with DA1-R (a), DA6, DA6-R, DA7, DA7-R (a), DA8, DA8-R, §5; D-079 DR19, DR20, DR21, DR22, DR26, DR30, DR31,
DR34, DR53, §4 D5, D11, §5 Q4, Q5; D-080 WA1; `CLAUDE.md` Laws 1–5; D-069; unit catalogue §4.5. Design:
[`docs/m7-plan/concealment-actions-proposal.md`](../../../docs/m7-plan/concealment-actions-proposal.md) (the PROPOSAL; this report
says what exists and what was measured). Nothing reserved for the Director is decided here.

---

## 0. Summary

| Brief item | State | Evidence |
|---|---|---|
| (1) Design PROPOSAL | **Done**: hide, camouflage, entrench on T3's Action Capacity (costs, preconditions, durations and endings, refusal codes, combination matrix, anti-exploit rules); the mechanism of each (detection distance; works → battle-grid cover and obstacle cells, dt-correct, lost on leaving); levels; the defender (READING AP-1, both alternatives; every existing scenario's value); the ambush (definition; READING AP-3); DR31 thirds; the War Sandbox offering (PROPOSAL); two further actions ("etc.") as PROPOSALS | proposal §§1–6 |
| (2) Equal sides | **Done in both resolvers through one function** (READING AP-2 (a); (b) = AutoResolver only, measured as basis B3). [Round 1, AP-R1-11: the TacticalResolver part goes beyond D-082 DA6-R's applied reading ("The rule is applied to the AutoResolver"); it is now Director question Q-AP4, §9, instead of a reading applied without a question.] "Equal" = exact equality of the two sides' elimination instants (BothEliminated, BothDisengaged); no band (AP-4). Standoff extension: option `TacticalRules.EqualSidesAtStandoff`, OFF by default, measured | §2, §4 |
| Battery effect | **No acceptance number moves** on any basis: 16/29 (held-out 4/9, tuning 12/20), attainability 29/29; the rule changes the reported winner of 57 tactical and 50 auto acceptance battles (every equal end in the 13 scenarios that name a defender; they occur in 5 of them) and nothing else. With lane MOR's rule at c = 200 (information): 16/20 tuning, 16/29 acceptance on both winner bases. Acceptance stays OPEN (Q4); nothing tuned or accepted | §4 (MEASURED) |
| (3) Prototype | **Done**: three action kinds with six refusal codes; per-agent concealment and works state; the war-pulse detection rule; spotting; the ambush flag on an engagement log; the hand-off to a battle setup (roles, works cells, carried camouflage); two terrain classes for the works; `BattleSetup.Defender`/`Ambusher`; `BattleResult.Winner`/`WinBasis`; `BattleWinner.Reported` carries DA6/DA7 winners into lane MOR's DA1 rise. **Not done**: the sandbox edits (design only, proposal §5.6), Reconnoitre and Lie in wait (proposals only, §3.5), the knowledge-seam coefficient (§2.3, after Q5). [Round 2 (§12): an order's legality is judged on what the issuer's polity detects (EnemyInContact, Observed, Retreat's contacts, TargetUndetected before the hit-and-run check), a concealment that an unseen enemy detects is spotted at once with its cost spent, and only a polity sharing a war is an enemy (`AreEnemies`); proposal §3.6, §3.7] | §1, §3, §12 |
| Tests | `M7Proto.slnx`: 1319 → **1382 passed, 0 failed, 0 skipped in Debug (47 s) and in Release (25 s)**, 0 build warnings: 61 new cases in `Proto.M7.Tests/Concealment/` (MEASURED by a filter run: 61 passed), 2 new rows in `PathfinderTests`; seven existing test methods or test helpers had their per-class tables extended for the two appended terrain classes; no golden moved (§7). [Superseded by round 1: 1405 passed, 0 failed, 0 skipped in Debug and in Release; 84 Concealment cases; §11.3] [Superseded by round 2: 1439 passed, 0 failed, 0 skipped in Debug and in Release; 118 Concealment cases in 53 test methods; §12.4] | §3, §6, §12 |
| Mutants | 27 mutants, each a full Debug suite bounded at 10 × the clean test step (511 s); none hung, none failed to build. 26 KILLED on their first run; **1 SURVIVED (M25, Entrench → Move allowed)**: no test pinned the new kinds' combination matrix — a test was added (all 33 ordered pairs) and M25 and a further matrix mutant M27 were then KILLED by it. Every kill names a semantic test; goldens are never the only killer. [Round 1 (§11): the verifier's own 14 mutants on 44161e1 left 13 gaps — 10 SURVIVED the whole suite and V01, V02, V10 were killed only by the golden; after the round-1 tests all 14, and 3 further aim mutants, are KILLED by semantic tests, §11.4] [Round 2 (§12.5): the verifier's R2-M01 and R2-M02 SURVIVED on `16efc0a` (reproduced); after the round-2 tests both, and 22 further mutants of the round-2 rules and aim checks, are KILLED by semantic tests (24 of 24)] | §5, §12 |
| Determinism scan, doc links | `scripts/check-proto-determinism.sh`: **CLEAN**, 107 files under `Proto.M7/`, 0 pattern groups with findings (final tree). `scripts/check-doc-links.py --all`: 464 files, 0 broken links (the proposal and this report included). D-069: no other game named in any file of the lane. [Round 2: the same results on the round-2 tree, §12.4] | §6, §12 |

---

## 1. What exists (files; line counts at the final commit)

**New**
- `Proto.M7/Tactical/EqualSides.cs` (66 lines): `WinBasis` (None, Decisive, DefenderOnEqualSides, AmbusherOnEqualSides);
  `SeparatesNeither(reason, standoffExtension)`; `Decide(reason, side, defender, ambusher, standoffExtension)` — the one function both
  resolvers call; `ValidateRoles` (roles ∈ {−1, 0, 1}, else `ArgumentOutOfRangeException`).
- `Proto.M7/War/Concealment.cs` (267): `ConcealmentRules` (CamouflageFactor 1/2, WorksRatePpmPerYear 360,000,000, cover threshold
  500,000 ppm, obstacle threshold 1,000,000 ppm — all PLACEHOLDER; `Validate`); `ConcealKind` flags; `ConcealEnd` (Moved, Attacked,
  Spotted); the rows `ConcealmentChange`, `EngagementRecord`, `ConcealmentStateRow`; `FieldWorks` (`Diggable`, `WorksAt`, `Realize`,
  `Placement`); `EngagementBattle.Build` (the engagement → battle hand-off, a stand-in for M7.15); `ConcealmentCodec` (version 1).
- `Proto.M7.Bench/EqualSidesBench.cs` (242) and one dispatch line in `Program.cs`: `equalsides derive | verify | mor` (§4).
- `Proto.M7.Tests/Concealment/` — `ConcealmentActionTests.cs` (709), `EqualSidesTests.cs` (364), `FieldWorksBattleTests.cs` (274),
  `ConcealmentCodecTests.cs` (79): 37 test methods, 61 cases.
- `docs/m7-plan/concealment-actions-proposal.md` (452 lines), this report.
- [Round 2] `Proto.M7.Tests/Concealment/ConcealmentKnowledgeTests.cs` (399 lines; 8 test methods, 34 cases; §12.3).

**Changed** (every change marked `[AP]` in the source)
- `Proto.M7/War/ActionWindows.cs` ([round 2: 649 lines; `AreEnemies`, `Sees`, `KnownEnemyInContact`, `WatchedByAKnownEnemy` and the
  rules of §12.3]): `ActionKind` Hide = 4, Camouflage = 5, Entrench = 6 (`Count` 4 → 7; order kinds 154–156; codes
  0–3 unchanged); `AgentProfile` + `HideCost`, `CamouflageCost`, `EntrenchCost` (default 0) and `SightCells` (default `long.MaxValue`
  = sees every agent, T3's behaviour); `ActionLayerSpec` + `LineTerrain`, `Concealment`, `ConcealmentRules`; the 7 × 7 same-agent
  matrix (T3's 4 × 4 block unchanged); `Detects`; the new refusals; the endings; spotting; the engagement log; queries; `StateRows`.
  The book hash appends the new state only when some profile enables a new kind, so every T3 world hashes as before (MEASURED: T3's
  goldens pass unchanged, and `WithoutTheNewKinds_TheBookHashesAsT3_AndTheLayerStateIsTheSame`).
- `Proto.M7/War/WarTheaterWorld.cs`: `WarKinds.ActHide/ActCamouflage/ActEntrench` (154–156); `WarRefusal` TargetUndetected 18,
  NoCover 19, EnemyInContact 20, Observed 21, AlreadyInState 22, CannotDig 23.
- `Proto.M7/Tactical/BattleSetup.cs`: `BattleSetup.Defender`, `BattleSetup.Ambusher` (−1 = none, the default);
  `TacticalRules.EqualSidesAtStandoff` (default false).
- `Proto.M7/Tactical/BattleResult.cs`: `Winner` (−1 default), `WinBasis`; `ResultHash` appends them only for DA6/DA7 winners.
- `Proto.M7/Tactical/TacticalBattle.cs`, `AutoResolver.cs`: `ValidateRoles` in the constructor; the end of the battle calls
  `EqualSides.Decide` (5 and 7 changed lines).
- `Proto.M7/Morale/CampaignMorale.cs`: `BattleWinner.Reported` returns the result's winner when DA6/DA7 decided it; otherwise the end
  reason as before.
- `Proto.M7/Tactical/ParityBatteryP.cs`: `DefenderOf(PScenario)` and `Build` with the defender; `POutcome.Of` with a winner basis
  (`HoldsField` default = the committed rule; `ReportedDecisive`; `Reported`). `ParityScenarios.cs` (#4, #8 → defender 1),
  `MultiDomainScenarios.cs` (Amphibious → 1).
- `Proto.M7/Spatial/Terrain.cs`: `TerrainClass.Earthwork` = 11, `Ditch` = 12 (`ClassCount` 11 → 13; earlier codes unchanged); cost
  2,000 / 3,000; concealment 1/2 / 1; both must be passable. `TerrainEffects.cs`: layer 1,500 mm / 0; transmission 1/4 / 1; footing
  1 / 1/2; order cap 1 / 1/2. `WarEcon/WarEconTables.cs`: the per-class graze and wear tables take Open's values for the two classes
  (they never lie on a campaign route; PROPOSAL). All PLACEHOLDER.

---

## 2. The design in brief (PROPOSAL; reasoning in the document)

- **Action points** = T3's Action Capacity and the polity's two slots per enclosing window (DA8-R, DR22). Costs are profile data.
- **Detection (war pulse):** polity P detects agent a iff a is P's, or some agent o of P has |x_o − x_a| × 2^32 ≤ SightCells(o) ×
  C(a).Raw (Int128), C(a) = (hidden ? terrain concealment of a's cell : 1) × (camouflaged ? 1/2 : 1). Hide needs C < 1 in its cell.
- **Preconditions:** Hide — not hidden (22), the cell conceals (19), no enemy within its attack range (20), no enemy polity detects it
  (21); Camouflage — 22, 20, 21; Entrench — not entrenching (22), diggable ground (23); argument 0 (BadValue). Attack — the target is
  detected by the issuer's polity (18), before the reach check. [Round 2, AP-R2-01: 20 and 21 count only enemy agents the issuer's
  polity DETECTS, Retreat's contacts likewise, and 18 comes before the hit-and-run check, so no refusal depends on an unseen agent
  (proposal §3.6); "enemy" = a polity sharing a war, AP-R2-04 (§3.7).]
- **Endings:** Move/Retreat end hide, camouflage and the works (cause Moved); Attack ends hide and camouflage (Attacked), the works
  stay; after every accepted Move/Retreat of polity P, every concealed agent of another polity that P now detects is spotted (cause
  Spotted), in ascending agent id. Everything at the order's instant, in log order (T1/T3; D-080 WA1). [Round 1: "now detects" is judged
  at the mover's END position — T3's moves are instantaneous, so passing through a concealed agent's cell does not spot it (AP-R1-04, a
  disclosed limitation, proposal §3.2, §9); a polity's own moves never spot (AP-R1-03); both now pinned, §11.] [Round 2: only a polity at
  war with the agent's polity spots it (AP-R2-04); and a Hide or Camouflage that an enemy polity detects at its own instant is spotted
  at once, by its own log index, the cost spent — the paid channel of proposal §3.6 (AP-R2-01).]
- **Ambush:** the attacker held a hide or camouflage AND the target's polity did not detect it at the instant of the attack; logged as
  `EngagementRecord.Ambush`; the hand-off sets `BattleSetup.Ambusher` = the attacker's side and `Defender` = the target's side.
- **Works:** W(t) = min(1,000,000, floor(360,000,000 × (t − since) ÷ 31,104,000,000)) ppm through `RateIntegrator.IntegrateExact`
  (complete after 1 day); W ≥ 500,000 → Earthwork under the defender's ground entities on diggable cells; W = 1,000,000 → a Ditch one
  cell in front (away from its exit edge).
- **Equal sides:** decided end → the other side; equal end → the ambusher, else the defender, else none; Standoff equal only with the
  extension.

**Readings (each applied until the Director corrects it; both alternatives in the proposal).** AP-1 who defends: (a) APPLIED the side
the opening engagement names as attacked / (b) the side that did not initiate contact (proposal §5.1). AP-2 where DA6 applies: (a)
APPLIED both resolvers through one function / (b) the AutoResolver only (§6.3; both measured, §4) [round 1, AP-R1-11: the
TacticalResolver part goes beyond DA6-R's applied reading and is now Director question Q-AP4]. AP-3 where an ambush is judged: (a)
APPLIED at the war-pulse engagement, flagged on the setup / (b) confirmed by the resolver at first contact (§5.2). AP-4 "equal": exact
equality of the elimination instants, APPLIED / a band on surviving strength (§6.1). [Round 1, AP-R1-12] AP-5 what an ambush is: (a)
APPLIED an attack from concealment (DA7-R's wording) / (b) any attack whose attacker the target's polity did not detect, concealed or
not (proposal §5.2). Also read, not asked: "stationary" = the state
belongs to the position where it was taken; the works are lost when the unit leaves (the brief's wording; the alternative of works left
on the map is §4.3); concealment needs no per-window upkeep (alternative §3.1).

**MEASURED vs INFERRED at a glance.** MEASURED (on the final tree unless stated): every pin of §3; the battery effect of §4; the works'
material effect (round 0: the TacticalResolver only; round 1 added the AutoResolver, §11) and the carried camouflage's delay (printed
by their tests); that no earlier golden moved (the whole suite passes and
no golden constant was edited); exact conservation (every ledger account exactly unchanged by the three actions; the duel and field-works battle tests assert
per-formation conservation, `TT.AssertConserved`, and the bench throws on any unconserved formation in all the battles it runs); the
mutants of §5. INFERRED: the concealed-agent invariant (proposal §3.2: argued from the code paths,
exercised by the tests, not proven exhaustively; [round 2] now "undetected by every ENEMY polity", kept by the spotting at an order's
instant); that READING AP-1 (b) would give the same defender as (a) in every existing scenario
((b) is not implemented); that the sandbox's `EditTerrain` can already paint the works classes (code reading); that lane MOR's rule ON
would change no verdict on the control set (not run); the integration with WarFog and the knowledge seam (design only); the
real-world directions (Polybius, Agincourt: CITED by lane TR; the marching camp: [KNOW]).

---

## 3. Tests (MEASURED; all pass in Debug and in Release)

### 3.1 `ConcealmentActionTests` (the war-pulse layer; rig: four profiles, a 60-cell line Open / Forest / Jungle / Marsh / Water / Open)

| Test | What it pins |
|---|---|
| `Hide_ShortensTheDetectionDistanceByTheCellsConcealment_Exactly` (4 cases) | Forest 1/2 × sight 4: spotted at 2 cells, not at 3; Jungle 1/4: at 1, not at 2 — the exact boundary of the detection equation |
| `Camouflage_WorksInTheOpen_AndCombinesWithAHideMultiplicatively` | camouflage halves the distance in Open; hide × camouflage = product of factors |
| `Refusals_AreRecordedWithTheirCode_AndChangeNothing` | every new code (18–23) and BadValue, NotInActionSet, CapacityExhausted for the new kinds: recorded, and the run equals the same run without the refused order (no slot, AC, state or change row; AE5). [Round 1: + TargetUndetected for an undetected target OUT of reach, with the OutOfReach control for the same target visible (AP-R1-06); NoCover for a camouflaged agent in the open (AP-R1-07)] |
| `States_EndOnMoveRetreatAndAttack_WithTheirCause_AndWorksAreLostOnlyByLeaving` | causes Moved / Attacked; works survive an attack, not a move |
| `Ambush_AnAttackFromConcealment_IsFlagged_AndAVisibleAttackIsNot` | the flag, the attacker's states, the target's works and cell on the record |
| `Ambush_NeedsTheHideInAnEarlierWindow_TurnExactAtThePulseStart` (3 cases) | **turn-exact**: the hide 2 days before the pulse start X; the attack at X − 1 ms is in the hide's window → `CombinationForbidden` (still hidden, no engagement); at X and X + 1 ms → the next window, accepted, an ambush |
| `SameInstant_TheLogOrderDecidesWhetherTheHideHolds` (2) | **same instant, log order**: at one T the enemy closes to distance 3 (inside the open distance 4, outside the hidden 2). Hide logged first → accepted and not spotted by the later move; move logged first → the hide is refused `Observed` |
| `EveryNewKind_UsesTheWindowContainingItsInstant` (9) | **turn-exact order-delivery pin for each new kind**: at a pulse boundary − 1 ms the order uses the old window (slots exhausted there → refused), at the boundary and + 1 ms the new one (WA1) |
| `CombinationMatrix_EveryOrderedPairWithANewKind_IsTheProposalsTable` | the 33 ordered same-agent pairs that involve a new kind: the second action's outcome (accepted / CombinationForbidden / KindRepeated) is the proposal's §3.4 table; accepted pairs apply both actions and hold their states; refused ones apply nothing (added after mutant M25 survived, §5) |
| `Entrench_WorksAtAnEngagementAreExact_AndCrossTheThresholdsAtExactInstants` (5) | 43,199,999 ms → 499,999 ppm; 43,200,000 → 500,000; 86,399,999 → 999,999; 86,400,000 → 1,000,000; capped after |
| `Entrench_WorksArePartitionAndTranslationInvariant` | **dt-correctness**: 40 random partitions (CounterRng) equal one integration; translation across a band edge and into a later band changes nothing |
| `Entrench_WorksAreLostAtTheMovesInstant_InLogOrder` | an attack logged before the move at the same instant records complete works; after it, 0 |
| `Spotting_ManyAgentsAtOneMove_EndInAscendingAgentIdOrder` (2) | **tie-dense ordering**: several agents spotted by one move end in ascending id, mirrored ids too |
| `ConcealmentActions_MoveNoStock_ExactLedgerEquality` | **conservation**: every ledger account exactly unchanged by the three actions |
| `Replay_ReproducesTheConcealmentLayer_AndTheGoldenRun` | a scripted run with stops and its replay from the order log without stops give the same book hash, world state hash, trace hash and layer view; the engagements and every change row asserted semantically; golden `BookGolden` `165388CC…4FCF1117F5`, `StateGolden` `38DCB06F…DC467C5B17` (NEW pins) |
| `WithoutTheNewKinds_TheBookHashesAsT3_AndTheLayerStateIsTheSame` | a world whose profiles enable no new kind hashes exactly as T3's book |

### 3.2 `EqualSidesTests` (both resolvers)

| Test | What it pins |
|---|---|
| `Decide_TruthTable_EveryReasonSideRoleAndExtension` | **tie-dense**: every end reason × side × defender × ambusher × extension (all rows); bad roles refused |
| `TacticalDuel_EqualEliminationGoesToTheAmbusherOrTheDefender_OneHpDecides` (1 and 3 pairs) | **at and around the boundary**: margin 0 HP → BothEliminated → ambusher / defender / none; ±1 HP → the survivor wins whatever the roles; the roles never change the battle |
| `ResultHash_AppendsTheWinnerOnlyWhenTheEqualSidesRuleDecided` | a decided or no-winner result hashes as before lane AP; a DA6/DA7 result does not; trace and state hashes unchanged |
| `AutoResolver_MutualKill_EqualSidesGoToTheAmbusherOrTheDefender` | the AutoResolver's same-step mutual kill, every role pair |
| `BothResolvers_SimultaneousWithdrawalIsEqual_OnePulseLaterIsDecided` (3) | the legacy withdrawals at the same pulse end → equal; one pulse apart → decided; both resolvers |
| `StandoffExtension_OffByDefault_OnGivesTheRole_InBothResolvers` | default OFF; ON → defender / ambusher; both resolvers |
| `StandoffExtension_OnLaneNsAmphibiousBattle_ChangesOnlyTheReportedWinner` | a real Standoff: OFF none, ON side 1; identical trace, state, end, pulses |
| `DA1_TheEqualSidesWinnerRises_DefenderOrAmbusher` (3) | **DA1 through lane MOR**: defender on equal sides → its army 800,000 − 100,000 + 200,000 = 900,000, the other 700,000; an ambush → the ambusher's army rises; no role → nobody |
| `BadRoles_AreRefusedByBothResolvers` | roles outside {−1, 0, 1} refused by both constructors |
| `ParityBatteryRoles_AreTheDocumentedOnes` | 58 battery setups (29 × 2 orientations): defender 1 in exactly the 13 documented scenarios, ambusher −1 everywhere; Sprint 1 #4/#8, Amphibious, Slice D meeting scenarios |
| `JungleAmbush_EqualEndsReportTheDefender_AndTheRoleChangesNoBattle` | real battles: tactical seeds 5 and 9 (BothEliminated) report the defender; the committed outcome and hashes equal those without the role; AutoResolver seeds 1–80 |

### 3.3 `FieldWorksBattleTests` (the works in battle, the hand-off) and `ConcealmentCodecTests`

| Test | What it pins |
|---|---|
| `Realize_PlacesCoverAndDitchesAtTheThresholds_UnderAndInFrontOfTheDefender` | 499,999 ppm → no cell; 500,000 and 999,999 → Earthwork under the defender (height kept); 1,000,000 → + a Ditch in front; a 50-entity block → 4 cover cells, 2 ditches; side 0 → ditch to the east; the source grid untouched |
| `Realize_IsIndependentOfTheFormationsOrder` | **tie-dense**: two defender formations sharing a cell, listed in either order and with ids swapped → the same grid and counts |
| `Realize_DigsOnlyDiggableGround` | a forest cell is dug (canopy replaced), a marsh cell is not, no ditch into water; `Diggable` false for Earthwork, Ditch, Building, Mountain, Wall, Ford |
| `Earthwork_IsCoverInTheMissileEquation_Exactly` | the engine's missile chance at a man in an Earthwork cell = the open-ground chance × 1/4, exactly |
| `Ditch_IsAnObstacleInTheMeleeEquation_Exactly` | the attacker's melee hit chance from a Ditch = `TerrainCombat.HitChance(50, 3/8, 45, 1)` exactly (footing 1/2 × cohesion 3/4) |
| `AnEntrenchedLine_FaresMateriallyBetter` | lane P's hold-line-open, tactical seeds 1–8: attacker losses 49 without works / 134 with complete works; defender losses 151 / 133; defender wins 0 / 2 of 8 (printed by the test, Release, final tree); asserted: attacker losses up, defender losses down, wins not fewer. [Superseded in round 1 (AP-R1-05): the test now runs both resolvers; AutoResolver seeds 1–8: 55 / 140, 148 / 128, 0 / 6 (printed, Debug); seeds 1–80 in §11.2] |
| `EngagementBattle_CarriesTheRolesTheWorksAndTheCamouflage` | the hand-off: roles, works cells, carried camouflage only for an ambush with camouflage; the battle's reported winner follows the rule. [Superseded in round 1 (AP-R1-02): "works cells" overclaimed — the test checked the works only by their placement counts, which are equal for both sides, and a mutant digging under the attacker survived; it now pins the Earthwork cells to exactly the cells of the defender's entities (none under the attacker) and each Ditch one cell toward the attacker, with the target on either side] |
| `CarriedCamouflage_NeverHastensTheFirstDetection_InBothResolvers` | common random numbers: never earlier in either resolver (auto seeds 1–20, never-detected handled on both sides; tactical seeds 1–8); first detection later with camouflage in 14 of 28 runs (printed; final tree) |
| `PopulatedTable_RoundTripsBitExact_WithTheExactLength_AndEqualHashes` | **populated table**: rows of a real run (3 states, 4 changes, 1 ambush engagement) + bound rows (11 / 8 / 2 rows): length 1 + 4 + 11 × 29 + 4 + 8 × 23 + 4 + 2 × 46 by hand = `ExpectedLength`; bit-exact round trip; SHA-256 equal; the empty table is 13 bytes |
| `Decode_RefusesMalformedInput` | wrong version, truncation, trailing byte, a bool byte of 2, an oversized count, empty input |

---

## 4. What the equal-sides rule changes on lane P's battery (MEASURED)

### 4.1 Commands and identity checks

All on the final tree, Release bench (`Proto.M7.Bench equalsides …`, `parityp run …`), 2 threads. The tactical CSVs of lane P's three
sets were produced by the base tree `7f0cb92` (`parityp run acceptance|control|tuning`): digests acceptance `12961E15…2CFD0D6D`,
control `F1B471C5…9C61E134`, tuning `90E58DCB…F65D6914` — the digests lane P recorded (H-P §7). Identity of this tree with them
(MEASURED): `equalsides verify base-acc.csv 2` re-ran the 4,640 tactical acceptance battles on this tree (244.4 s):
the HoldsField digest is `12961E15…2CFD0D6D`, **EQUAL** to the base CSV's; the Reported digest read from the resolvers' own
results, `50A7106D7DFAB5EC…CDA5A83D26`, is **EQUAL** to the one derived through `EqualSides.Decide`; the resolvers' win bases:
None 60, Decisive 4,523, DefenderOnEqualSides 57, AmbusherOnEqualSides 0. `parityp run control` and `parityp run tuning` on this tree: digests `F1B471C5…9C61E134`
(1,440 battles, 78.0 s) and `90E58DCB…F65D6914` (1,600 battles, 78.5 s), **EQUAL** to the base. So every tactical outcome row of the
three sets, as lane P's digest covers it (scenario, seed, winner on the field-holder basis, both roles' casualties, pulses, duration, end reason;
`ParityPCsv.Digest`), is
unchanged by this lane, and the cached CSVs are this tree's own.

`equalsides derive base-acc.csv base-ctl.csv base-tun.csv` (13.5 s): tactical winners per basis derived from the cached outcomes
through `EqualSides.Decide` (the resolvers' own function), the AutoResolver run directly on every basis. Auto acceptance digest
(HoldsField) `4038507198FB…ADEEB3B65F` and **battery hash `BEA60C2F89F0D965AC2B7FD3945B5AAFD0FC8C38DB9D80DF53E139025B236442`**:
both equal lane P's round-2 record (H-P §12.2), i.e. the committed outcome basis is unchanged by everything in this lane. The final
derive output is byte-identical (timing masked) to the run made before the terrain classes and the action kinds were added.

### 4.2 Bases (rule P1 exactly as committed; only the winner the outcomes carry differs)

| Basis | Tactical winner | Auto winner | Scenarios passing | Held-out | Tuning (fresh seeds) | Attainability |
|---|---|---|---|---|---|---|
| B0 as committed | field holder | field holder | 16/29 | 4/9 | 12/20 | 29/29 |
| B1 reported, decided ends only (lane MOR before DA6/DA7) | reported | reported | 16/29 | 4/9 | 12/20 | 29/29 |
| B2 DA6/DA7 in both resolvers (AP-2 (a), applied) | reported | reported | 16/29 | 4/9 | 12/20 | 29/29 |
| B3 DA6/DA7 in the AutoResolver only (AP-2 (b)) | decided only | reported | 16/29 | 4/9 | 12/20 | 29/29 |
| B4 B2 + Standoff extension ON | reported | reported | 16/29 | 4/9 | 12/20 | 29/29 |

No scenario's verdict and no scenario's list of failing checks differs from B0 on any basis (the bench prints every difference; none).
Cases whose winner differs (4,640 acceptance cases): tactical B0→B1 0, B1→B2 **57**, B2→B4 0; auto B0→B1 0, B1→B2 **50**, B2→B4 0;
cases whose non-winner fields differ between any two bases: 0 (the rule changes the reported winner only).

The 57 tactical changes are the BothEliminated ends of ford-crossing (13), high-ridge (13), hill-crest (6), jungle-ambush (16) and
marsh-front (9); the 50 auto changes those of ford-crossing (14), jungle-ambush (25) and marsh-front (11) — DERIVED from the bench's
per-(scenario, reason) counts. In those scenarios the "neither" share goes to role B, e.g. jungle-ambush tactical 47.5/42.5/10.0 →
47.5/52.5/0.0 (A/B/neither %), auto 45.0/39.3/15.6 → 45.0/55.0/0.0; ford-crossing auto band 19.5 → 18.3; high-ridge tactical
26.8/65.0/8.1 → 26.8/73.1/0.0, band 19.3 → 17.9. The Standoff extension changes no battery case: the battery's only Standoffs are 2
tactical meeting-asymmetric battles, which name no defender.

**Do the acceptance numbers move? No** — on every basis. Lane P's acceptance stays OPEN (D-079 Q4); the acceptance battery was
consumed by lane P (H-P §5.4), so these numbers are information, not acceptance evidence. Nothing was tuned; parity is not claimed.

### 4.3 With lane MOR's in-battle rule at c = 200 (information only; lane MOR's default stays 0)

`equalsides mor 200 tuning base-tun.csv 2 …` and `equalsides mor 200 acceptance base-acc.csv 2 …` (final tree, Release): lane MOR's
`LocalWinMoraleRise` = 200 is set on every battle's rules for these runs only (the shipped default stays 0), both resolvers; rule P1
judged on four columns per scenario: the field holder or the reported winner (DA6/DA7), each with the rise 0 and 200.

| Set | Tactical digest, rise 0 (cached) / 200 | Auto digest, rise 0 / 200 | Passing: field holder 0 / 200; reported 0 / 200 |
|---|---|---|---|
| tuning, 1,600 cases (tactical 81.0 s) | `90E58DCB…F65D6914` / `1E6D728D…8C76525D` | `2AC31207…A0B68F31` / `CAAEB267…ED37D6E7` | 16/20, 16/20; 16/20, 16/20 |
| acceptance, 4,640 cases (tactical 222.5 s) | `12961E15…2CFD0D6D` / `20C02A32…45ACA2CC` | `40385071…EEB3B65F` / `787A82E5…954F0BF9` | 16/29, 16/29; 16/29, 16/29 |

The tuning digests equal lane MOR's own measurement (H-MOR §5.4: tactical on `1E6D728D…`, auto off `2AC31207…`, on `CAAEB267…`).
No verdict changes on either set or basis. With the rule on, two acceptance scenarios gain a failing check while staying FAIL, on both
winner bases: combined-arms (win, duration) → (win, casB, duration) and combined-arms-vs-mass (duration) → (casA, duration). Both
final runs are byte-identical (timing masked) to the runs made before the terrain classes and the action kinds were added. Information only: the acceptance battery is
consumed (H-P §5.4), lane MOR's default stays 0, and parity is not accepted or tuned (D-079 Q4).

---

## 5. Mutants (bounded; every kill by a named semantic test)

### 5.1 Harness and bound

`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/AP/mut/run.py` (this lane's own script,
outside the clone): it copies `prototypes/m7-sprint1` without bin/obj into a
private tree, builds and runs the full Debug suite of `M7Proto.slnx` once clean, then per mutant applies one exact text replacement
(asserting the old text occurs once), rebuilds, runs the full Debug suite with a hard bound of **10 × the clean test step**, restores
the file and checks its SHA-256; at the end every mutated file is checked byte-identical to the clone. Clean: build 19.3 s, test
51.1 s, 1381 passed / 0 failed / 0 skipped → bound **511 s** per mutant. No mutant hung and none failed to build. Three runs of the same harness: M1–M23 (one run, clean
baseline first), M24–M26 (second run), M25 again and M27 (third run, after the matrix test was added); the bound was the same 511 s
throughout. The harness log is
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/AP/mut/mutants.log` (outside the repository).

### 5.2 Results (MEASURED; "cases" = failing test cases, theories counted per case; the semantic killer is named, goldens are never the only killer)

| # | Mutation | Result | Semantic killer(s) | Property the killer asserts — one the system ought to have? (ADR-015 §7.2) |
|---|---|---|---|---|
| M1 | `EqualSides.Decide`: the defender before the ambusher | KILLED, 54.0 s, 8 cases | `Decide_TruthTable_…`, `TacticalDuel_…_OneHpDecides`, `AutoResolver_MutualKill_…`, `DA1_TheEqualSidesWinnerRises_…` | DA7 overrides DA6 in an ambush (DD; DA7-R (a)): yes |
| M2 | `SeparatesNeither`: BothDisengaged is not equal | KILLED, 54.2 s, 2 | `BothResolvers_SimultaneousWithdrawalIsEqual_…`, `Decide_TruthTable_…` | simultaneous withdrawals separate neither side (this lane's definition of "equal", delegated by DA6-R): yes, under that definition |
| M3 | `SeparatesNeither`: a Standoff is always equal | KILLED, 55.9 s, 3 | `StandoffExtension_OffByDefault_…`, `StandoffExtension_OnLaneNsAmphibiousBattle_…`, `Decide_TruthTable_…` | the extension is OFF by default (DA6-R: a PROPOSAL, not decided): yes |
| M4 | the AutoResolver passes no roles | KILLED, 53.3 s, 4 | `AutoResolver_MutualKill_…`, `BothResolvers_…`, `JungleAmbush_EqualEndsReportTheDefender_…` | "The rule is applied to the AutoResolver" (DA6-R): yes |
| M5 | the TacticalResolver swaps defender and ambusher | KILLED, 52.0 s, 8 | `TacticalDuel_…`, `DA1_…`, `BothResolvers_…`, `JungleAmbush_…` | the roles mean the same in the TacticalResolver (READING AP-2 (a)): yes, under (a) |
| M6 | `BattleWinner.Reported` ignores the DA6/DA7 winner (lane MOR's rule before AP) | KILLED, 55.3 s, 8 | `DA1_TheEqualSidesWinnerRises_…`, `TacticalDuel_…`, `AutoResolver_MutualKill_…` | DA1-R (a): a DA6/DA7 winner is a winner for DA1: yes |
| M7 | `ResultHash` always appends the winner | KILLED, 46.6 s, 2 | `ResultHash_AppendsTheWinnerOnlyWhenTheEqualSidesRuleDecided` (also lane TR's amphibious golden) | a result DA6/DA7 did not decide hashes exactly as before lane AP: yes (no unexplained golden move) |
| M8 | detection boundary strict (`<` for `≤`) | KILLED, 52.1 s, 6 | `Hide_ShortensTheDetectionDistanceByTheCellsConcealment_Exactly`, `Camouflage_…`, `Refusals_…` (Observed at 4 ≤ 4) | an exact, pinned boundary; inclusive is this lane's convention (PROPOSAL) — the aim is exactness, not the side of the convention |
| M9 | a hide uses a fixed 1/2, not its cell's concealment | KILLED, 46.5 s, 9 | `Hide_…_Exactly` (Jungle cases), `Ambush_AnAttackFromConcealment_…`, `Refusals_…` (TargetUndetected) | Law 2: the hide works through the terrain's concealment: yes |
| M10 | no spotting after a Move | KILLED, 49.8 s, 6 | `Hide_…_Exactly` (spotted cases), `Camouflage_…`, `Spotting_ManyAgentsAtOneMove_…` | a concealed agent is found when an enemy comes within the shortened distance: yes |
| M11 | Hide without the Observed precondition | KILLED, 49.3 s, 2 | `Refusals_…` (Observed), `SameInstant_TheLogOrderDecidesWhetherTheHideHolds` | no vanishing while watched (anti-exploit, PROPOSAL): yes |
| M12 | every attack flagged an ambush | KILLED, 48.0 s, 7 | `Ambush_AnAttackFromConcealment_IsFlagged_AndAVisibleAttackIsNot`, `Entrench_WorksAtAnEngagementAreExact_…` | DA7 applies only to an attack from concealment (DD): yes |
| M13 | the works are not lost on a move | KILLED, 55.5 s, 3 | `States_End…_AndWorksAreLostOnlyByLeaving`, `Entrench_WorksAreLostAtTheMovesInstant_InLogOrder` | the works are lost when the unit leaves (the brief): yes |
| M14 | the works counted from 1 ms before the order | KILLED, 58.1 s, 3 | `Entrench_WorksAtAnEngagementAreExact_…`, `Entrench_WorksArePartitionAndTranslationInvariant` | Law 3: the exact integral from the order's instant: yes |
| M15 | the ditch dug behind the line | KILLED, 52.4 s, 3 | `Realize_PlacesCoverAndDitchesAtTheThresholds_…`, `Realize_DigsOnlyDiggableGround`, `AnEntrenchedLine_FaresMateriallyBetter` | the obstacle lies toward the enemy: yes |
| M16 | the cover threshold exclusive | KILLED, 53.5 s, 1 | `Realize_PlacesCoverAndDitchesAtTheThresholds_…` | an exact threshold (500,000 ppm is cover): a PLACEHOLDER convention, pinned |
| M17 | Hide → Attack allowed in one window | KILLED, 55.8 s, 1 | `Ambush_NeedsTheHideInAnEarlierWindow_TurnExactAtThePulseStart` (−1 ms case) | no conceal-and-strike within one window (anti-exploit, PROPOSAL): yes |
| M18 | an undetected target may be attacked | KILLED, 55.6 s, 1 | `Refusals_…` (TargetUndetected) | one cannot strike what one cannot see: yes |
| M19 | the hand-off makes the attacker the defender | KILLED, 52.3 s, 1 | `EngagementBattle_CarriesTheRolesTheWorksAndTheCamouflage` | READING AP-1 (a): the attacked side defends: yes, under (a) |
| M20 | camouflage carried to the defender | KILLED, 57.1 s, 1 | `EngagementBattle_Carries…` | only the ambusher's own camouflage enters the battle: yes |
| M21 | spotting skips camouflage-only agents | KILLED, 58.1 s, 2 | `Camouflage_WorksInTheOpen_AndCombinesWithAHideMultiplicatively` (also the replay golden) | camouflage is found like a hide: yes |
| M22 | the battery forgets the two ambush-posture scenarios' defender | KILLED, 54.4 s, 2 | `ParityBatteryRoles_AreTheDocumentedOnes`, `JungleAmbush_EqualEndsReportTheDefender_…` | the documented roles (proposal §5.4): yes — the waiting side defends |
| M23 | an attack does not reveal the attacker | KILLED, 56.7 s, 6 | `States_EndOnMoveRetreatAndAttack_…` (cause Attacked), `Ambush_AnAttackFromConcealment_…`, `Ambush_NeedsTheHideInAnEarlierWindow_…` (also the replay golden and the codec's real-run rows) | an attack from concealment ends the attacker's concealment: yes |
| M24 | spotting in descending agent id | KILLED, 51.1 s, 2 | `Spotting_ManyAgentsAtOneMove_EndInAscendingAgentIdOrder` (both orientations) | the tie-dense ordering pin: several agents spotted by one move end in ascending id — the order is a stated, stable integer key: yes |
| M25 | Entrench → Move allowed in one window (dig-and-leave) | **SURVIVED** on the first run (45.9 s, 0 failures): no test pinned the matrix cells of the new kinds except Hide → Attack. Test added: `CombinationMatrix_EveryOrderedPairWithANewKind_IsTheProposalsTable` (all 33 pairs). Re-run: **KILLED**, 58.3 s, 1 of 1382 | `CombinationMatrix_EveryOrderedPairWithANewKind_IsTheProposalsTable` | the proposal's matrix (no dig-and-leave; the unit catalogue's "consumes all remaining movement (entrench)"): yes, as a PROPOSAL |
| M26 | Camouflage without the Observed precondition | KILLED, 47.8 s, 1 | `Refusals_AreRecordedWithTheirCode_AndChangeNothing` (Observed for a camouflage) | no vanishing while watched: yes |
| M27 | Attack → Entrench allowed in one window (strike, then dig in) | KILLED, 58.5 s, 1 of 1382 | `CombinationMatrix_EveryOrderedPairWithANewKind_IsTheProposalsTable` | T3's "Attack → anything" bar extended to the new kinds: yes, as a PROPOSAL |

**The survivor.** M25 is not equivalent: with it an agent can dig in and leave in one window. It survived because the new kinds' matrix
cells were pinned only for Hide → Attack. The added test pins every one of the 33 ordered pairs that involve a new kind (the outcome of
the second action, both actions applied when accepted, nothing applied when refused); M25 and the further matrix mutant M27 were then
run against it (both KILLED by it, 1 failing case of 1382 each). Mutants M1–M23 ran on the tree before this test and the strengthened camouflage test were added
(the production code is byte-identical — checked by the harness at the end of each run — and none of their recorded killers changed, apart from a corrected comment in the codec test);
M24–M26 ran with the strengthened camouflage test but before the matrix test; the M25 re-run and M27 ran on the final test files.

---

## 6. What ran, what was NOT RUN, what was deferred

Host: the shared 4-CPU session container (another agent ran concurrently); every build and test with `nice -n 19`, `-m:1`,
`-nodeReuse:false`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, naming the solution file; .NET SDK 10.0.114. Wall times are informative only.

### 6.1 What ran (MEASURED)

| What | Command | Result |
|---|---|---|
| Baseline, base `7f0cb92` | `dotnet build M7Proto.slnx -c Debug …` and `-c Release`; `dotnet test M7Proto.slnx --no-build` in each | 0 warnings, 0 errors; **1319 passed, 0 failed, 0 skipped** in Debug (40 s) and in Release (16 s) |
| Final tree, Debug | `dotnet build M7Proto.slnx -c Debug -m:1 -nodeReuse:false`; `dotnet test M7Proto.slnx -c Debug --no-build` | 0 warnings, 0 errors; **1382 passed, 0 failed, 0 skipped** (47 s) [round 0's final tree 44161e1; superseded by round 1's, §11.3] |
| Final tree, Release | the same with `-c Release` | 0 warnings, 0 errors; **1382 passed, 0 failed, 0 skipped** (25 s) [round 0; superseded, §11.3] |
| The new tests alone (Release) | `dotnet test M7Proto.slnx -c Release --no-build --filter FullyQualifiedName~Proto.M7.Tests.Concealment` | 61 passed, 0 failed, 0 skipped [round 0; round 1: 84, §11.3] |
| The two measuring tests (Release, detailed logger) | `--filter` on `AnEntrenchedLine_FaresMateriallyBetter` and `CarriedCamouflage_NeverHastensTheFirstDetection` | their printed lines (§3.3) |
| Battery | `equalsides derive`, `equalsides verify`, `parityp run control`, `parityp run tuning`, `equalsides mor 200 tuning`, `equalsides mor 200 acceptance` | §4 |
| Mutants | the harness of §5.1, three runs | §5 |
| Determinism scan | `bash scripts/check-proto-determinism.sh` (every `*.cs` under `Proto.M7/`, comments included) | CLEAN: 107 files, 0 pattern groups with findings |
| Doc links | `python3 scripts/check-doc-links.py --all` | 464 files checked, 0 broken links |
| Originality (D-069) | case-insensitive search of every file of the lane for other games' names | no match |

### 6.2 NOT RUN (and why)

- `M7Econ.slnx` and `M7DetMath.slnx`: no file of theirs changed, and neither references `Proto.M7` (read from the `.csproj` files:
  `Proto.M7.Econ` references `Sim.Core` and `Sim.Data`; `Proto.DetMath` references nothing; each test project references its own
  project).
- The `Sim.*` tiers (`scripts/validate.sh targeted|integration|full`): nothing in `Sim.*` changed (D-079 Q5).
- A Windows run; the bench timing modes (no performance claim is made); an independent verifier (ADR-015 §6). [Superseded: the
  independent verifier verify-AP-r1 ran on 44161e1 and reported AP-R1-01..14; round 1 is §11.]
- The tactical CONTROL and TUNING sets with lane MOR's rule at c = 200 beyond what `equalsides mor` runs (tuning only, with the rule
  on); the control set with the rule on was not run.

### 6.3 Deferred (to whom)

- The sandbox `SetRoles` / `Prepare` edits (proposal §5.6): to lane D's sandbox work, after the Director's view on Q-AP1.
- Reconnoitre and Lie in wait (proposal §3.5): to the action-economy packet (C23, M7.3) if wanted; Lie in wait needs a reaction-order
  semantic in T1 with its own turn-exact pin.
- Starting knowledge for an ambusher and lifting carried camouflage on movement (both resolvers): to the M7.15 hand-off and the
  resolvers' owners (lanes TR, P), together, for parity.
- The knowledge-seam coefficient (proposal §2.3): after D-079 Q5.
- A parity re-measurement with DA6/DA7 on a NEW held-out battery: lane P's next round (D-079 Q4); nothing here accepts parity.

---

## 7. Moved pins, changed tests, goldens (with attribution)

- **No existing golden moved.** The new golden pins are `BookGolden` and `StateGolden` (§3.1), both lane AP's.
- **Changed existing tests** (all for `ClassCount` 11 → 13, the two appended classes; none of them occurs on these tests' grids):
  `PathfinderTests.Cost_validator_rejects_violations` (every row extended by the two entries; the "expected 11" row became
  "expected 13"; two rows added — lane TR's 11-entry table is now too short, and a works class of cost 0 is refused as impassable),
  `PathfinderTests.Forest_and_hills_position_is_not_constrained` and `PathfinderTests.Concealment_validator` (arrays extended;
  "expected 11" → "expected 13", plus an 11-entry case), `AirTests` (one concealment table extended), `AutoResolverPTests`,
  `AutoResolverPRuleTests`, `AutoResolverPRound2Tests` (one concealment table each). Attribution (`git log --diff-filter=A`): the originals are sprint 1's
  (`PathfinderTests`, Slice C, `ae04d2c`), lane N's (`AirTests`, `7118ef0`) and lane P's (the three AutoResolver test files,
  `444b85e`); lane AP extended their tables for 11 → 13 classes, each change marked `[AP]`.
- **Consequence of the appended classes** (INFERRED from the code, covered by the suite passing): every validator that checks a class
  against `ClassCount` now accepts 11 and 12 (`TerrainGrid.Set`, the line terrain of the action book, the sandbox's `EditTerrain`).

---

## 8. Limitations

1. Every magnitude is PLACEHOLDER: camouflage 1/2, works rate (one day), thresholds, the two classes' coefficients, the test costs.
2. The war-pulse line is one-dimensional with uniform movement cost; detection uses line distance; the line's terrain stands in for the
   campaign map's cover (the campaign model has no cover terrain today: no "forest", "jungle" or "biome" anywhere in `Sim.Core`'s
   C# sources, MEASURED by a case-insensitive search).
3. An observer of unlimited sight (the default) detects everything, so in a world without sight values no agent can hide (refused as
   Observed) — by design, to keep T3's behaviour, but every polity needs finite sight values for concealment to exist.
4. Carried camouflage in battle is lane N's personal concealment: a volley lifts it for a step, moving does not (at the war-pulse layer
   moving ends camouflage). A battle-level lifting on movement would need a change in both resolvers (proposal §5.3).
5. An ambush has no tactical surprise beyond DA7 and the carried camouflage: both sides start a battle with empty knowledge, and the
   thirds put the ambusher in its own third (proposal §5.3, §5.5).
6. Concealment has no upkeep or decay; works do not decay, are not visible to the enemy, and vanish with the agent's move (the brief's
   wording; the alternative is proposal §4.3).
7. The hand-off maps one engagement to one battle and assumes the caller lays out the forces; it is a stand-in for M7.15.
8. 20 m battle cells: one Earthwork cell is the frontage of a block; works in a forest cell replace the canopy (one class per cell);
   two thresholds turn a continuous W into two steps.
9. No Windows run; no independent verifier (ADR-015 §6): the mutants are this lane's own. [Round 1: an independent verifier ran
   (verify-AP-r1, 14 mutants of its own); still no Windows run.]
10. [Round 1, AP-R1-04] Spotting is judged at the END of a move or retreat (T3's moves are instantaneous): an enemy that marches
    through or past a concealed agent's cell does not find it; a concealed force cannot be found by sweeping past it (proposal §3.2).
11. [Round 1, AP-R1-10] The hide reuses the battle concealment table, where Wall and Building are 0 ("irrelevant" in battle): a hide on
    such a line cell is detected only from its own cell (MEASURED by the round-1 probe; proposal §2.1). No shipped world puts those
    classes on the line.
12. [Round 1, AP-R1-05] The works move both resolvers the same way on hold-line-open, with different magnitudes (defender wins with
    works 29 of 80 tactical, 42 of 80 auto): a parity observation for lane P, not tuned (D-079 Q4 OPEN).
13. [Round 2, AP-R2-01] What an order still reveals (proposal §3.6): the PAID channel — a Hide or Camouflage spotted at once tells its
    polity that an unseen enemy detects the concealed agent, at the price of a slot and its Action Capacity (radius: the watcher's
    sight × the new factor); the number of agents in the world (T3's `OutOfReach` for an id beyond them); whether an enemy that is seen
    again closed in while unseen (T3's hit-and-run lifting). The last two are free (refusals) but reveal no unseen agent's present
    position or state; they are T3's rules, not changed.
14. [Round 2, AP-R2-01] Between sight × the new factor and sight, a SEEN watcher refuses a hide (`Observed`) while an UNSEEN one at the
    same distance loses the agent (proposal §3.6, alternative (b) not applied).
15. [Round 2, AP-R2-04] The enemy relation is static (any war of the world; its span not consulted) and stands in for M8's diplomacy;
    T3's Attack and Retreat still treat every other polity as hostile (proposal §3.7).

---

## 9. For the Director (only what is genuinely open; details in proposal §8)

- **Q-AP1** — who defends: READING AP-1 (a) the attacked side (applied) or (b) the side that did not initiate contact.
- **Q-AP2** — whether a battle opened by an ambush may start inside DR31's middle third (no exception implemented).
- **Q-AP3** — adopt the Standoff extension (depends on P-TR1) or keep it off.
- **Q-AP4** [round 1, AP-R1-11] — where DA6/DA7 apply: (a) both resolvers through one function (applied in the prototype pending the
  ruling; beyond DA6-R's applied reading, which names the AutoResolver) or (b) the AutoResolver only. MEASURED: no acceptance verdict
  changes either way (§4.2, B2 and B3).
- Observation for lane MOR (round 1, AP-R1-14; not a question here): under DA6 with DA1-R (a), a defender whose formations were all
  eliminated (BothEliminated) is the reported winner and its army's morale rises (MEASURED: 900,000 against 700,000 in
  `DA1_TheEqualSidesWinnerRises_DefenderOrAmbusher`); whether a mutual-elimination win should rise as much is lane MOR's (DA2).
- Proposals (not implemented): Reconnoitre and Lie in wait (proposal §3.5); the sandbox SetRoles / Prepare edits (§5.6); starting
  knowledge for an ambusher (§5.3); WarFog observers as agent observers (§2.2); the knowledge-seam coefficient (§2.3, after Q5).
- [Round 2] No new question: what an order may reveal (proposal §3.6) and who is an enemy (§3.7) are design under DA8's delegation
  (D-082 §5 leaves the detection rules undecided), implemented as PROPOSALS with their alternatives.

---

## 10. Git, reproduce

Branch `lane/AP` in the clone `/home/user/m7w/AP` (base `7f0cb92`); nothing was pushed or merged, and nothing was written to the
orchestrator's checkout. Commits (oldest first): `b589f11` the equal-sides rule, roles and bench; `a386c45` the two terrain classes;
`386c1cf` the three action kinds; `a3f1992`, `2cbc4a6`, `2d6f7e3` tests; `331258c` the proposal; `d6d6105` the camouflage-test
strengthening and proposal corrections; `7df0ce8` the matrix test; then the commit that adds this report (`44161e1`, round 0's head).
Round 1 (§11): `6826a23` the semantic tests, `2e6ca5d` the proposal amendments, then the commit that adds §11 (`16efc0a`, round 1's
head). Round 2 (§12): `00e1688` the action-layer rules and their tests, then the commit with the replay check, the proposal amendments
and §12 (the branch head named in the round-2 hand-back). Patch
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/patches/AP.patch` (`git diff 7f0cb92..HEAD`),
bundle `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/bundles/AP.bundle`
(`7f0cb92..HEAD`, verified with `git bundle verify`).

Reproduce (from `prototypes/m7-sprint1`, after `./scripts/bootstrap.sh`; host flags as in §6):

```
dotnet build M7Proto.slnx -c Release -m:1 -nodeReuse:false && dotnet test M7Proto.slnx -c Release --no-build
B=Proto.M7.Bench/bin/Release/net10.0/Proto.M7.Bench.dll
dotnet $B parityp run acceptance acc.csv 2; dotnet $B parityp run control ctl.csv 2; dotnet $B parityp run tuning tun.csv 2
dotnet $B equalsides derive acc.csv ctl.csv tun.csv derive.md      # bases B0-B4 (§4.2)
dotnet $B equalsides verify acc.csv 2                                # the resolver's own winners = the derived ones
dotnet $B equalsides mor 200 tuning tun.csv 2 mor-tuning.md          # §4.3, information only
dotnet $B equalsides mor 200 acceptance acc.csv 2 mor-acc.md
bash scripts/check-proto-determinism.sh; python3 ../../scripts/check-doc-links.py --all
```

---

## 11. Verification round 1 (fix agent, 2026-11-10)

**Inputs.** The independent verifier's findings AP-R1-01..14 (verify-AP-r1, pinned to `44161e1`: 3 major, 8 minor, 3 nits). Clone
`/home/user/m7w/AP`, branch `lane/AP`, starting at `44161e1`. **Rule followed:** every finding was REPRODUCED against `44161e1`
before anything was changed, with this round's own mutant run and probes (§11.1); the finder's word was not used as evidence. **All 14
reproduced; none refuted.** Round 1 changed tests, the two documents and two code comments; it changed no production behaviour (the
round's diff under `Proto.M7/` is two comment edits, MEASURED: every changed line of `git diff 44161e1..HEAD --
prototypes/m7-sprint1/Proto.M7` is a comment), so every number of §4 stands.

### 11.1 How the findings were reproduced (MEASURED, on 44161e1)

- **Baseline:** `M7Proto.slnx` Debug and Release builds 0 warnings, 0 errors; **1382 passed, 0 failed, 0 skipped** in Debug (44 s) and
  in Release (20 s).
- **Mutants:** the verifier's V01–V14, as the same exact text replacements, run by this round's own harness
  `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/AP-fix1/mut/run.py` in a private copy of the
  tree (`…/m7c/AP-fix1/mut/tree-r`): per mutant a Debug build and the FULL Debug suite under a hard bound of 10 × the clean test step
  (clean: build 17.2 s, test 51.1 s → bound 511 s), then the file restored and its SHA-256 checked; at the end every mutated file was
  byte-identical to the clone. None hung, none failed to build. Log `…/m7c/AP-fix1/mut/tree-r.log`.
- **Probes:** `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/AP-fix1/probe/ZzFixProbeR1.cs`
  (12 probe methods, P01–P10, P12 and P13 — there is no P11 — compiled into the private copy only, never into the clone), output `…/m7c/AP-fix1/probe/probe-r.log`.
- **Reading:** the round-0 texts at `44161e1` (`git show 44161e1:…`) for the documentation findings.

### 11.2 Per-finding verdicts

| ID (severity) | Verdict | Reproduction evidence (this round; MEASURED unless marked) | Fix (round 1) |
|---|---|---|---|
| AP-R1-01 (major) | **REPRODUCED** | V01 and V02 KILLED by 1 case each, `Replay_…_AndTheGoldenRun`, on the `BookGolden` equality only (actual `1E5A56B4…` and `29C3295E…`, as the verifier found); probe P01: a Hide and a Camouflage in one window leave 8 and 7 of capacity 10, so the property is observable; no test read `RemainingOf` or `Cost` except through `View()` | new `EveryNewKind_SpendsItsProfileCost_FromActionCapacity` (3 cases): the record's cost and the remaining capacity; at the boundary, capacity left = the cost → accepted with 0 left, = the cost − 1 → CapacityExhausted with nothing changed. The test costs differ (2, 3, 4), so a cost read from another field is caught (X1, §11.4) |
| AP-R1-02 (major) | **REPRODUCED** | V11 SURVIVED (1382 passed, 49.4 s); probe P02 on hold-line-open seed 1: `Realize` for side 1 → (1, 1) with 31,16 Ditch and 32,16 Earthwork; for side 0 → (1, 1) with 14,16 Earthwork and 15,16 Ditch: the counts the test asserted are equal for both sides | `EngagementBattle_CarriesTheRolesTheWorksAndTheCamouflage` strengthened: with the target on either side, the Earthwork cells equal exactly the cells of the defender's entities (read from the engine's own placement), none lies under the attacker, each Ditch is one cell toward the attacker; the measured cells pinned; §3.3's overclaim marked |
| AP-R1-03 (major) | **REPRODUCED** | V04 SURVIVED (50.2 s), V06 SURVIVED (45.9 s); probe P03: a polity-mate moving next to a hide leaves it hidden (1 change row); a Scout retreating to 1 cell from a Forest hide spots it (cause Spotted, the retreat's log index) | new `Spotting_OnlyAnotherPolitysMoveOrRetreatSpots`: (a) a polity-mate moving onto the hidden agent's cell ends nothing; (b) an enemy retreat ending 1 cell away spots it (cause, log index, instant), ending 2 cells away does not; proposal §3.2 "Scope" |
| AP-R1-04 (minor) | **REPRODUCED** | probe P04 (hide in the Jungle at 22; Watcher, sight 4, from 40): ends at 10 or 15 (crossing 22) → still hidden; at 21, 22, 23 → spotted; at 24, 30 → not; no mention of paths, end positions or instantaneous moves in the round-0 proposal or report | disclosed as a LIMITATION (proposal §3.1 Ends, §3.2 "Where it is judged", §9; §2 and §8 item 10 here; the `ConcealEnd.Spotted` comment); the documented end-position rule pinned by `Spotting_IsJudgedFromTheMovesEndPosition_APassThroughDoesNotSpot` (6 cases); a path sweep stated as a PROPOSAL alternative, not implemented (it would change the scripted golden run: INFERRED) |
| AP-R1-05 (minor) | **REPRODUCED** | probe P05, hold-line-open, bare → complete works (attacker losses; defender losses; defender wins): seeds 1–80 tactical 446 → 1,264; 1,486 → 1,343; 0 → 29; auto 538 → 1,333; 1,492 → 1,290; 0 → 42 (the verifier's numbers); seeds 1–8 tactical 49 → 134; 151 → 133; 0 → 2 (round 0's); auto 55 → 140; 148 → 128; 0 → 6; nothing on the AutoResolver in the round-0 texts | `AnEntrenchedLine_FaresMateriallyBetter` runs both resolvers and asserts the direction in each; proposal §4.3 table; §3.3 and §8 item 12 here. The 13-of-80 difference in defender wins is reported to lane P, not normalized or tuned |
| AP-R1-06 (minor) | **REPRODUCED** | V05 SURVIVED (48.8 s); probe P06: an Attack on the hidden Raider 3 cells away (out of reach, undetected) is refused 18 (TargetUndetected); the mutant answers 17 (OutOfReach) | two checks in `Refusals_…`: the undetected target out of reach → TargetUndetected; the same target unconcealed (detected, out of reach) → OutOfReach (control) |
| AP-R1-07 (minor) | **REPRODUCED** | V08 SURVIVED (51.2 s); probe P07: a camouflage in the open (cell 5) is accepted, a hide there in the next window is refused 19 (NoCover) | one check in `Refusals_…` (the camouflage first, then the refused hide) |
| AP-R1-08 (minor) | **REPRODUCED** | V12 (47.2 s), V13 (45.7 s), V14 (50.5 s) SURVIVED; probe P08: a configured factor 1/4 gives `ConcealmentFactor` raw 1,073,741,824; `Validate` throws for factor 0, factor 3/2, rate −1 and cover threshold 0; a profile with a Hide, Camouflage or Entrench cost of −1 or a sight of −1 is refused (ArgumentException) | new `ConfiguredRules_AreTheBooks_AndInvalidRulesOrProfilesAreRefused` (the configured factor changes the detection outcome; `Validate`'s bounds both ways; the book validates its rules; the four profile checks, and 0 accepted) and `BookHash_CoversTheEngagementLog_AndTheConfiguredWorksRateReachesIt` (a configured works rate reaches the engagement record) |
| AP-R1-09 (minor) | **REPRODUCED** | V09 SURVIVED (46.6 s); probe P09: on the equal duel with defender 1, HoldsField −1, ReportedDecisive −1, Reported 1 (= `BattleWinner.Reported`) | new `POutcome_WinnerBases_FieldHolderDecisiveEndOrReportedWinner` (equal, ambush, no-role and decided duels; the two-argument form is HoldsField; the other fields equal on every basis; a bad basis refused) |
| AP-R1-10 (minor) | **REPRODUCED** | probe P10 (line cell 22 set to the class): Wall and Building → the hide accepted, factor 0; Watcher at distance 1: still hidden, not detected; at distance 0: spotted; Forest → spotted at distance 1; no mention in the round-0 texts | disclosed (proposal §2.1, §9; §8 item 11 here) with two alternatives for the campaign integration; no code change (which classes a campaign cell may carry is a design question, not a fix) |
| AP-R1-11 (minor) | **REPRODUCED** | D-082 §2 DA6-R: "The rule is applied to the AutoResolver."; the round-0 proposal §8 listed AP-2 under "Readings applied without a question" | READING AP-2's TacticalResolver part routed to the Director as **Q-AP4** (proposal §6.3, §8; §0, §2, §9 here); the `TacticalBattle` comment labels it; the prototype keeps (a) applied pending the ruling (no behaviour change) |
| AP-R1-12 (nit) | **REPRODUCED** | the round-0 proposal §5.2 defined the ambush by a hide or camouflage state with no reading; probe P12: a Raider attacking from 1 cell an agent whose polity has sight 0 is not detected by that polity, and the engagement is not flagged an ambush, so the alternative is realizable; §5.1 stated the same-defender claim as fact while §2 here says INFERRED | READING AP-5 (what an ambush is) with both alternatives and their consequences (proposal §5.2; §2 here); §5.1 labelled INFERRED |
| AP-R1-13 (nit) | **REPRODUCED** (all four points) | V10 KILLED only by the golden (actual `4B1843DE…`); V07 SURVIVED (50.4 s), and probe P13a shows only the land formation's cell dug when the defender also has an air and an embarked formation; no `war: -1` agent in the round-0 Concealment tests (grep: 0), and probe P13b: a hide in the campaign window at turn 2's start − 1 ms → 10 (SlotsExhausted), at 0 and + 1 ms → WarId −1, window 2; proposal §4.3 said "(DA8 brief; applied)" | `BookHash_CoversTheEngagementLog_…` (two worlds differing only in the works rate: equal actions, refreshes, changes and states; engagements differing only in the works; different hashes); `Realize_DigsOnlyUnderTheDefendersLandFormationsOnTheGround`; `EveryNewKind_InTheCampaignWindow_UsesTheTurnContainingItsInstant` (9 cases); §4.3's attribution corrected to the lane brief |
| AP-R1-14 (nit) | **REPRODUCED** (an observation) | `DA1_TheEqualSidesWinnerRises_DefenderOrAmbusher` passes on `44161e1` asserting 900,000 against 700,000 after both duel entities died (BothEliminated); the round-0 proposal raised the unfought-win concern for the Standoff only (§6.4) | the observation added for lane MOR and the Director (proposal §6.4, §8; §9 here); nothing decided |

### 11.3 What ran on the final tree (MEASURED)

Host as in §6 (the shared 4-CPU container; `nice -n 19`, `-m:1`, `-nodeReuse:false`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, the solution
named). Wall times are informative only.

| What | Command | Result |
|---|---|---|
| Final tree, Release | `dotnet build M7Proto.slnx -c Release -m:1 -nodeReuse:false`; `dotnet test M7Proto.slnx -c Release --no-build` | 0 warnings, 0 errors; **1405 passed, 0 failed, 0 skipped** (17 s) [round 1's tree `16efc0a`; superseded by round 2's, §12.4] |
| Final tree, Debug | the same with `-c Debug` | 0 warnings, 0 errors; **1405 passed, 0 failed, 0 skipped** (45 s) [round 1; superseded, §12.4] |
| The Concealment tests (Release) | `--filter FullyQualifiedName~Proto.M7.Tests.Concealment` | 84 passed, 0 failed, 0 skipped [round 1; 118 in round 2, §12.4] |
| The measuring tests (Release, detailed logger) | `AnEntrenchedLine_FaresMateriallyBetter`, `CarriedCamouflage_NeverHastensTheFirstDetection_InBothResolvers`, `Replay_…_AndTheGoldenRun` | tactical 49 / 134, 151 / 133, 0 / 2; auto 55 / 140, 148 / 128, 0 / 6; camouflage later in 14 of 28 runs; goldens `165388CC…4FCF1117F5` and `38DCB06F…DC467C5B17` (unchanged) |
| Mutants after the fixes | the harness of §11.1 on a fresh copy of `2e6ca5d` | §11.4 |
| Determinism scan | `bash scripts/check-proto-determinism.sh` | CLEAN: 107 files under `Proto.M7/`, 0 pattern groups with findings |
| Doc links | `python3 scripts/check-doc-links.py --all` | 464 files checked, 0 broken links (with this section) |
| Originality (D-069) | case-insensitive, word-bounded search of every file changed in round 1 for other games' names | no match |

The code measured is the round-1 head's: the commit that adds this section changes only this file.

New test methods (8 methods, 23 cases): `EveryNewKind_SpendsItsProfileCost_FromActionCapacity` (3),
`Spotting_OnlyAnotherPolitysMoveOrRetreatSpots` (1), `Spotting_IsJudgedFromTheMovesEndPosition_APassThroughDoesNotSpot` (6),
`EveryNewKind_InTheCampaignWindow_UsesTheTurnContainingItsInstant` (9), `ConfiguredRules_AreTheBooks_AndInvalidRulesOrProfilesAreRefused`
(1), `BookHash_CoversTheEngagementLog_AndTheConfiguredWorksRateReachesIt` (1), `Realize_DigsOnlyUnderTheDefendersLandFormationsOnTheGround`
(1), `POutcome_WinnerBases_FieldHolderDecisiveEndOrReportedWinner` (1). Strengthened: `Refusals_AreRecordedWithTheirCode_AndChangeNothing`
(+3 checks), `EngagementBattle_CarriesTheRolesTheWorksAndTheCamouflage` (cells and side), `AnEntrenchedLine_FaresMateriallyBetter` (both
resolvers). `Proto.M7.Tests/Concealment/`: 37 → 45 test methods, 61 → 84 cases. No golden moved (`BookGolden` `165388CC…4FCF1117F5` and
`StateGolden` `38DCB06F…DC467C5B17` unchanged, printed by the replay test on the final tree); no existing assertion was weakened.

### 11.4 Mutants after the fixes (MEASURED; the same harness, a fresh private copy `tree-v` of the round-1 tree `2e6ca5d`)

Clean copy: build 17.1 s, test 53.5 s, 1405 passed → bound **535 s** per mutant (10 × the clean test step). None hung, none failed to
build; every mutated file was byte-identical to the clone at the end. Log
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/AP-fix1/mut/tree-v.log`. "Cases" = failing test
cases of 1405. X1, X3 and X4 are this round's own aim checks on the new tests (they were not run on `44161e1`).

| Mutant | Mutation | Finding | On `44161e1` | On the round-1 tree | Semantic killer(s) (the golden is never the only one) | Property the killer asserts — one the system ought to have? (ADR-015 §7.2) |
|---|---|---|---|---|---|---|
| V01 | Hide costs 0 | R1-01 | KILLED by the golden only (1 case) | KILLED, 50.9 s, 4 cases | `EveryNewKind_SpendsItsProfileCost_…`, `EveryNewKind_InTheCampaignWindow_…` | DA8: a unit SPENDS action points to hide: yes |
| V02 | Camouflage costs 0 | R1-01 | KILLED by the golden only (1) | KILLED, 45.1 s, 4 | the same two | the same, for camouflage: yes |
| V03 | Entrench costs 0 (control) | — | KILLED (2: `Refusals_…`, the golden) | KILLED, 50.3 s, 5 | the same two and `Refusals_…` (CapacityExhausted) | the same, for entrenching: yes |
| V04 | no spotting after a Retreat | R1-03 | SURVIVED | KILLED, 47.0 s, 1 | `Spotting_OnlyAnotherPolitysMoveOrRetreatSpots` | a retreat ending within the shortened distance finds a concealed agent as a move does: yes |
| V05 | TargetUndetected after the reach | R1-06 | SURVIVED | KILLED, 51.5 s, 1 | `Refusals_…` | an order on an unseen agent fails on detection and says nothing of its distance: yes (proposal §3.3) |
| V06 | a polity's own move spots its own agents | R1-03 | SURVIVED | KILLED, 46.0 s, 1 | `Spotting_OnlyAnotherPolitysMoveOrRetreatSpots` | a polity knows its own agents; concealment stays usable with two agents: yes |
| V07 | works under every defender formation | R1-13 | SURVIVED | KILLED, 55.3 s, 1 | `Realize_DigsOnlyUnderTheDefendersLandFormationsOnTheGround` | aircraft and embarked troops do not dig field works: yes |
| V08 | a camouflaged agent hides without cover | R1-07 | SURVIVED | KILLED, 47.0 s, 1 | `Refusals_…` | a hide needs concealing terrain; camouflage is not cover: yes |
| V09 | the battery's Reported basis reads the field holder | R1-09 | SURVIVED | KILLED, 50.2 s, 1 | `POutcome_WinnerBases_…` | the basis that measures DA6/DA7 (§4) carries the DA6/DA7 winner: yes |
| V10 | the book hash omits the engagement log | R1-13 | KILLED by the golden only (1) | KILLED, 48.7 s, 2 | `BookHash_CoversTheEngagementLog_…` | the layer's replay-divergence check sees every engagement field: yes |
| V11 | the hand-off digs under the attacker | R1-02 | SURVIVED | KILLED, 52.9 s, 1 | `EngagementBattle_CarriesTheRolesTheWorksAndTheCamouflage` | the target's works stand under the defender (READING AP-1 (a)): yes, under (a) |
| V12 | the book ignores its configured rules | R1-08 | SURVIVED | KILLED, 49.3 s, 2 | `ConfiguredRules_…`, `BookHash_…` | configured coefficients are the ones used: yes |
| V13 | `Validate` accepts a camouflage factor of 0 or above 1 | R1-08 | SURVIVED | KILLED, 48.7 s, 1 | `ConfiguredRules_…` | the factor stays in (0, 1]: 0 would hide an agent from everything but its own cell, above 1 would make camouflage help the enemy: yes |
| V14 | negative costs and sight accepted | R1-08 | SURVIVED | KILLED, 54.3 s, 1 | `ConfiguredRules_…` | no capacity farming through a negative cost (remaining −= cost): yes |
| X1 | Hide spends the Camouflage cost | R1-01 (aim) | — | KILLED, 50.3 s, 4 | `EveryNewKind_SpendsItsProfileCost_…`, `EveryNewKind_InTheCampaignWindow_…` | each kind spends its own profile cost: yes |
| X3 | the ReportedDecisive basis reads the reported winner | R1-09 (aim) | — | KILLED, 55.2 s, 1 | `POutcome_WinnerBases_…` | the pre-DA6/DA7 comparison basis (§4.2 B1) excludes equal-sides winners: yes |
| X4 | `Validate` accepts an obstacle threshold below the cover threshold | R1-08 (aim) | — | KILLED, 49.6 s, 1 | `ConfiguredRules_…` | 1 ≤ cover ≤ obstacle ≤ complete works: yes |

### 11.5 NOT RUN, and why

- `M7Econ.slnx`, `M7DetMath.slnx`: no file of theirs changed in round 1 (and neither references `Proto.M7`, §6.2).
- The `Sim.*` tiers: nothing in `Sim.*` changed (D-079 Q5).
- The parity battery (§4) and the bench: not re-run. Round 1 changed no production behaviour (two comments under `Proto.M7/`), so the
  §4 numbers stand as round 0 measured them (INFERRED from the diff; the final tree's full suite, goldens included, passes).
- A Windows run; the bench timing modes.
- Not implemented, by design of this round: a path-sweep spotting rule (AP-R1-04, alternative only) and any line-class rule for Wall or
  Building cells (AP-R1-10, alternatives only); both would be design changes, not fixes.

### 11.6 Deferred (to whom)

- **Q-AP4** (where DA6/DA7 apply): to the Director.
- A path-sweep spotting rule, if wanted: to the owners of the war-pulse movement model (T3's Move has no path today), with its own
  turn-exact pin and the golden it would move.
- A campaign-layer concealment table or a line-class rule: to the campaign integration of the actions (after D-079 Q5).
- The works' magnitude difference between the resolvers: to lane P's next parity re-measurement (D-079 Q4).
- The mutual-elimination win's morale rise: to lane MOR (DA2).

### 11.7 Statements superseded or amended in place

This report: §0 rows (2), Tests and Mutants; §2 Endings, Readings (AP-2 → Q-AP4; AP-5 added) and MEASURED vs INFERRED; §3.1
`Refusals_…` row; §3.3 `AnEntrenchedLine_…` and `EngagementBattle_…` rows; §6.1 final-tree and filter rows; §6.2 independent
verifier; §8 item 9 and new items 10–12; §9 Q-AP4 and the observation; §10 commits. Proposal: header, §0 Equal sides row, §2.1, §3.1
Ends, §3.2 Scope and "Where it is judged", §4.3 (both resolvers; the attribution), §5.1, §5.2 (READING AP-5), §6.3 (routing), §6.4
(observation), §8 (Q-AP4; the applied-readings line), §9. Each is marked "round 1" (or "Superseded") with its finding id.

---

## 12. Verification round 2 (fix agent, 2026-11-10)

**Inputs.** The independent verifier's findings AP-R2-01..04 (verify-AP-r2, pinned to `16efc0a`: 1 major, 3 minor). Clone
`/home/user/m7w/AP`, branch `lane/AP`, starting at `16efc0a`. **Rule followed:** every finding was REPRODUCED against `16efc0a` with this
round's own probes and mutant runs before anything was changed (§12.1); the verifier's probes and logs were read for orientation, not
used as evidence. **All four reproduced; none refuted.** Unlike round 1, round 2 CHANGES production behaviour — in the war-pulse action
layer only: `git diff 16efc0a..HEAD --stat -- prototypes/m7-sprint1/Proto.M7` lists `War/ActionWindows.cs` (the rules) and two comment
edits (`War/Concealment.cs`, `War/WarTheaterWorld.cs`) (MEASURED). No resolver, equal-sides, works, hand-off, codec, battery or bench
code changed.

### 12.1 How the findings were reproduced (MEASURED, on 16efc0a)

- **Baseline:** `M7Proto.slnx` Debug and Release builds 0 warnings, 0 errors; **1405 passed, 0 failed, 0 skipped** in Debug (48 s) and in
  Release (17 s).
- **Probes:** `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/AP-fix2/probe/ZzFixProbeR2.cs` (7 probe
  methods: P01, P01b, P02–P06), compiled into a private `git archive` export of `16efc0a` (`…/m7c/AP-fix2/probe/tree16`), never into the
  clone; output `…/m7c/AP-fix2/probe/probe-r.log`. The same probes with R2-M01 and R2-M02 applied to that export (then restored; SHA-256
  equal to `16efc0a`'s file): `…/m7c/AP-fix2/probe/probe-mut.log`.
- **Mutants:** R2-M01 and R2-M02 (the verifier's exact text replacements) by this round's own harness
  `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/AP-fix2/mut/run.py` on a fresh export `tree-r` of
  `16efc0a`: per mutant a Debug build and the FULL Debug suite under a hard bound of 10 × the clean test step (clean: build 17.1 s, test
  45.0 s, 1405 passed → bound 450 s), then the file restored and its SHA-256 checked against `16efc0a`. Log `…/m7c/AP-fix2/mut/tree-r.log`.

### 12.2 Per-finding verdicts

| ID (severity) | Verdict | Reproduction evidence (MEASURED on `16efc0a`) | Fix (round 2) |
|---|---|---|---|
| AP-R2-01 (major) | **REPRODUCED** | P01 — polity 1's Raider U (sight 6) hides in the Jungle at 22 (NEXT) or the Forest at 12 (FAR); polity 0's Prober (sight 3) marches to 23 and detects U in neither world: Hide → 20 NEXT / 0 FAR; Camouflage → 20 / 0; Retreat to 25 → 0 (accepted only because of U) / 17. P01b (a seen Watcher D at 24 as well): Retreat to 21 → 17 (refused only because it leads toward U) / 0. P02 (the Prober at 26, U 4 cells away and watching it, 4 ≤ 6): Hide → 21 / 0. Every refusal left the Prober's capacity at 60 (inert: free). Found by this round, same class: P03 — R (sight 3) retreats from E; E marches out of sight; an Attack on E within the cooldown → 18 (TargetUndetected) if E then closed to 5 cells unseen (the hit-and-run record is lifted by the approach), 15 (HitAndRun) if it withdrew. No disclosure: a search of the proposal and this report for leak, probe, reveal, unseen finds none on the subject. Control (the Prober's sight unlimited, U seen): the outcomes differ, as they legitimately may | design, PROPOSAL (proposal §3.6): an order's legality depends only on what the issuer's polity detects — `KnownEnemyInContact` (20), `WatchedByAKnownEnemy` (21), Retreat's contacts and its hit-and-run records, `TargetUndetected` before `HitAndRun`; an accepted Hide or Camouflage that an enemy polity detects at its instant is spotted at once (its own log index; the cost spent). Disclosed: the one paid channel and two T3-owned channels (proposal §3.6; §8 items 13–14). Tests §12.3 |
| AP-R2-02 (minor) | **REPRODUCED** | R2-M01 SURVIVED (56.4 s; 0 failed, 1405 passed). Teeth (P05 under R2-M01): a hide at 29 (Jungle, next to Marsh) gets the factor 1 (raw 4,294,967,296) and is spotted by the Watcher 2 cells away; clean: 1/4 (raw 1,073,741,824), hidden. At 19 (Forest, next to Jungle) the reverse: clean spotted at 2 cells (factor 1/2), mutant 1/4 and hidden. Every round-1 hide stood inside a band | tests `Hide_UsesTheConcealmentOfItsOwnCell_AtTheEdgesOfTerrainBands` (4 edges) and `NoCoverAndCannotDig_ReadTheAgentsOwnCell_AtTheEdgesOfTerrainBands` (the two other own-cell reads) |
| AP-R2-03 (minor) | **REPRODUCED** | R2-M02 SURVIVED (43.8 s; 1405 passed). Teeth (P06): a Dim Watcher (sight 1) that the Raider sees and that does not watch it beyond 1 cell: at distance 1 (= the range) → 20, at 2 → 0; under R2-M02 → 20 at 2. With the hider's range 2 (test input): 20 at 2, 0 at 3 (mutant: 20 at 3) | test `EnemyInContact_IsTheHidersOwnAttackRange_RefusedAtItAndAcceptedOneCellBeyond` (Hide, Camouflage; range 1 and 2) |
| AP-R2-04 (minor) | **REPRODUCED** | P04 — three polities, war 0 between 0 and 1: polity 2's campaign Watcher (war −1) moves 40 → 13 next to polity 0's Forest hide: the hide ends (cause Spotted, the move's log index); polity 1 does not detect the agent, polity 2 does. P04b: polity 2's Watcher in contact (13) refuses the hide 20; watching it from 15 refuses it 21. The documents mixed "enemy" (`ConcealEnd.Spotted`; proposal §3.1) with "another polity" (§3.2) and did not disclose the consequence (no mention of a polity outside the war; MEASURED by a search of both at `16efc0a`) | design, PROPOSAL (proposal §3.7): `AreEnemies` — polities sharing a war of the world; `Spot`, the contact and watcher preconditions and the spotting at an order's instant consult it; T3's Attack and Retreat unchanged. Test `OnlyAPolityAtWarWithTheAgentsPolity_SpotsWatchesOrContactsIt` |

### 12.3 The fixes and their tests

**Code** (`Proto.M7/War/ActionWindows.cs`; every change marked `[AP round 2]`):
- `KnownEnemyInContact(agent, range)` replaces `AnyEnemyInContact`: an agent of an ENEMY polity, within the range, that the agent's own
  polity DETECTS.
- `WatchedByAKnownEnemy(agent)` replaces `DetectedByAnEnemy` in the `Observed` precondition: such an agent that detects the agent now,
  with its current factor.
- `Sees(o, agent, f)`: the one-observer test, factored out of `Detects` unchanged (`Detects` now loops over it).
- After an accepted Hide or Camouflage: if `DetectedByAnEnemy(agent)` (now: an enemy polity) holds with the new factor, `EndStates(…,
  Spotted)` at once — the order's log index and instant; the slot and the cost stay spent.
- Retreat (T3's kind): its contacts, and the disengagement records it writes, are the agents its polity detects.
- Attack: `TargetUndetected` before `HitAndRun`.
- `AreEnemies(p, q)` from a `polityCount × polityCount` table built from the war list; `Spot` and `DetectedByAnEnemy` consult it.
- Comments: the `ActionKind` summaries, the legality list, `WarRefusal.EnemyInContact` and `Observed`, `ConcealEnd.Spotted`.

T3 worlds (unlimited sight, no concealment) cannot tell the difference: every agent is detected, nothing is concealed, so every changed
branch takes its old path (MEASURED: T3's tests and goldens, and lane AP's goldens `BookGolden` / `StateGolden`, pass unchanged).

**Tests** — `Proto.M7.Tests/Concealment/ConcealmentKnowledgeTests.cs`, new, **8 test methods, 34 cases** (MEASURED by a filter run and a
count of the `[Fact]`/`[Theory]` attributes; the message of commit `00e1688` said "7 methods", an error corrected in the next commit's
message):

| Test | What it pins |
|---|---|
| `KnowledgeLeak_NoOrderOutcomeDependsOnAnEnemyTheIssuerDoesNotDetect` (10) | **THE LEAK GATE** (metamorphic on hidden state; ADR-037 §4's precedent): one probe order per case — Hide and Camouflage (in contact and watched; watched only), Entrench, Retreat away, Retreat toward with a seen enemy D, Attack U, Move, Recruit — in four worlds that differ only in where polity 1's U stands and whether it hides, unseen by polity 0 in every one (asserted). Identical in all four: the outcome code, the capacity left, the slots used, the Prober's cell, its retreat records. The ONE documented difference, asserted as such: a Hide or Camouflage that U detects with the new factor is begun and spotted at once (the order's own log index, at T2, the cost spent); held in every other world. **Positive control:** with the Prober's sight unlimited, U is seen and the same two placements give the stated different outcomes (Hide 20 / 0, watched 21 / 0, Retreat away 0 / 17, Retreat toward 17 / 0, Attack 0 / 17) |
| `KnowledgeLeak_AnAttackOnAnUnseenAgent_SaysNothingOfItsMovements` | the hit-and-run channel: an Attack on an unseen E within the cooldown → TargetUndetected whether E closed in (record lifted) or withdrew (not lifted); control with E seen: OutOfReach 17 / HitAndRun 15 |
| `Retreat_ContactsAndDisengagementRecords_AreTheEnemiesItsPolitySees` | a retreat away from a seen D toward an unseen U is accepted, records D only, and ends next to U without spotting it (1 > 3 × 1/4) |
| `SpottedAtOnce_AConcealmentIsJudgedAtItsOwnInstant_InLogOrder` (12) | **SAME-INSTANT PIN** of the round-2 delivery rule, Hide and Camouflage: at one instant a Lookout (sight 16) marches to 3 cells (seen), the boundary (4 / 8 cells: unseen, and it detects the new concealment) or one cell beyond, logged before or after the order: refused Observed, nothing spent / spotted at once by the ORDER's log index, cost spent / held, then spotted by the MOVE's log index / held |
| `OnlyAPolityAtWarWithTheAgentsPolity_SpotsWatchesOrContactsIt` | `AreEnemies` for one and two wars (symmetric, never a polity with itself); a non-belligerent's move next to a hide does not end it (it detects the agent, the war enemy does not); its agent in contact or watching does not refuse the hide, and a hide it detects is not spotted at once; with a war between them all three happen; control: the war enemy's move spots it |
| `Hide_UsesTheConcealmentOfItsOwnCell_AtTheEdgesOfTerrainBands` (4) | at 29, 20, 10 and 19 (each with one neighbour of another class): the factor is exactly the own cell's table value, and the Watcher's detection follows it on the side where a neighbour's value would differ |
| `NoCoverAndCannotDig_ReadTheAgentsOwnCell_AtTheEdgesOfTerrainBands` | Hide 9 → NoCover, 10 → 0, 29 → 0, 30 → NoCover; Entrench 29 → 0, 30 → CannotDig, 39 → CannotDig, 40 → 0 |
| `EnemyInContact_IsTheHidersOwnAttackRange_RefusedAtItAndAcceptedOneCellBeyond` (4) | Hide and Camouflage with the hider's range 1 and 2 (test input): a seen, non-watching enemy at the range → 20, nothing spent; one cell beyond → accepted, the state held |

Rig changes: `ConcealmentActionTests.Build` takes an optional war list (`wars`, default the one war as before, now `War0()`); the
`Refusals_…` summary notes the round-2 reading. No existing assertion was changed or weakened; no golden moved.

### 12.4 What ran on the final tree (MEASURED)

Host: the shared 4-CPU session container (another agent may run concurrently); every build and test with `nice -n 19`, `-m:1`,
`-nodeReuse:false`, `--disable-build-servers`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, naming the solution file; .NET SDK 10.0.114. Wall times
are informative only.

| What | Command | Result |
|---|---|---|
| Baseline, `16efc0a` | `dotnet build M7Proto.slnx -c Debug …` and `-c Release`; `dotnet test M7Proto.slnx --no-build` in each | 0 warnings, 0 errors; **1405 passed, 0 failed, 0 skipped** in Debug (48 s) and in Release (17 s) |
| Final tree, Debug | `dotnet build M7Proto.slnx -c Debug -m:1 -nodeReuse:false --disable-build-servers`; `dotnet test M7Proto.slnx -c Debug --no-build` | 0 warnings, 0 errors; **1439 passed, 0 failed, 0 skipped** (41 s) |
| Final tree, Release | the same with `-c Release` | 0 warnings, 0 errors; **1439 passed, 0 failed, 0 skipped** (20 s) |
| The Concealment tests (Release) | `--filter FullyQualifiedName~Proto.M7.Tests.Concealment` | **118 passed** (84 + 34), 0 failed, 0 skipped; 53 test methods (a count of the `[Fact]`/`[Theory]` attributes: 22 + 2 + 8 + 12 + 9) |
| The round-2 tests alone (Release) | `--filter FullyQualifiedName~ConcealmentKnowledgeTests` | 34 passed, 0 failed, 0 skipped |
| Goldens (Release, detailed logger) | `Replay_ReproducesTheConcealmentLayer_AndTheGoldenRun` | book `165388CC…4FCF1117F5`, state `38DCB06F…DC467C5B17`: **unchanged**; T3's goldens pass unchanged (in the full suite) |
| Mutants | the harness of §12.1 on a fresh export of `00e1688` | §12.5: 24 of 24 KILLED |
| Determinism scan | `bash scripts/check-proto-determinism.sh` | CLEAN: 107 files under `Proto.M7/`, 0 pattern groups with findings |
| Doc links | `python3 scripts/check-doc-links.py --all` | 464 files checked, 0 broken links (with this section) |
| Originality (D-069) | case-insensitive search of every line added in round 2 (`git diff 16efc0a`) for other games' titles | no match |

The final runs were made on the working tree that the round-2 head records: its code and tests, plus the documents (which no build
reads). The round's first commit `00e1688` holds the rules and the 34 cases; the second adds one replay equality check to
`SpottedAtOnce_…` (assertions only), the proposal amendments and this section.

### 12.5 Mutants after the fixes (MEASURED)

Harness `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/AP-fix2/mut/run.py` (this round's own) on
a fresh `git archive` export `tree-v` of `00e1688`: clean build 17.8 s, test 51.8 s, 1439 passed → bound **518 s** per mutant (10 × the
clean test step). Per mutant one exact text replacement (asserted to occur once), a Debug build and the FULL Debug suite under the bound,
then the file restored and its SHA-256 checked; at the end every mutated file was byte-identical to `00e1688`. None hung (longest test
step 52.4 s), none failed to build. Mutant list `…/AP-fix2/mut/fixes.json`, log `…/AP-fix2/mut/tree-v.log`, per-mutant output
`…/AP-fix2/mut/tree-v-<id>.out`. "Cases" = failing test cases of 1439. The run used `00e1688`'s test files; the later replay check adds
assertions only.

| Mutant | Mutation | Finding | Result | Semantic killer(s) (no golden is the only one) | Property the killer asserts — one the system ought to have? (ADR-015 §7.2) |
|---|---|---|---|---|---|
| F01 | `EnemyInContact` counts enemies the issuer does not detect (rounds 0–1) | R2-01 | KILLED, 45.5 s, 2 | the gate (Hide, Camouflage in contact) | no refusal depends on an unseen agent in contact: yes (proposal §3.6) |
| F02 | `Observed` counts watchers the issuer does not detect (rounds 0–1) | R2-01 | KILLED, 50.8 s, 14 | the gate, `SpottedAtOnce_…` | no refusal depends on an unseen watcher: yes |
| F03 | Retreat's contacts include unseen agents (rounds 0–1) | R2-01 | KILLED, 44.7 s, 3 | the gate (both Retreat rows), `Retreat_ContactsAndDisengagementRecords_…` | a retreat's legality and direction use the contacts its polity sees: yes |
| F04 | Retreat records disengagements from unseen contacts | R2-01 | KILLED, 44.2 s, 2 | the gate (the retreat records), `Retreat_Contacts…` | no hit-and-run ban from an enemy it could not see: yes |
| F05 | `TargetUndetected` after the hit-and-run check (rounds 0–1) | R2-01 | KILLED, 50.3 s, 1 | `KnowledgeLeak_AnAttackOnAnUnseenAgent_…` | an order on an unseen agent says nothing of its movements: yes |
| F06 | no spotting at the order's instant (Hide) | R2-01 | KILLED, 48.2 s, 2 | the gate (the paid channel; the control's own spotted hide), `SpottedAtOnce_…` | a concealed agent is undetected by every enemy polity between events (the invariant): yes |
| F07 | no spotting at the order's instant (Camouflage) | R2-01 | KILLED, 48.0 s, 2 | the gate, `SpottedAtOnce_…` | the same, for camouflage: yes |
| F08 | aim: the at-once spotting judged with the PRE-order factor (alternative (b) of proposal §3.6) | R2-01 | KILLED, 43.8 s, 3 | the gate ("watched" rows; unseen and 4 cells away), `SpottedAtOnce_…` | the applied PROPOSAL (a) — judged with the new factor: yes under (a); this pins the documented choice, it does not prove (b) wrong |
| F09 | any other polity's move spots (rounds 0–1) | R2-04 | KILLED, 46.4 s, 1 | `OnlyAPolityAtWarWithTheAgentsPolity_…` | a non-belligerent's move does not end a hide: yes, under proposal §3.7 |
| F10 | the at-once spotting by any other polity | R2-04 | KILLED, 46.7 s, 1 | `OnlyAPolityAtWar…` | the same at the order's instant: yes |
| F11 | `EnemyInContact` counts any other polity's agent | R2-04 | KILLED, 45.5 s, 1 | `OnlyAPolityAtWar…` | a non-belligerent in contact does not refuse a hide: yes, under §3.7 |
| F12 | `Observed` counts any other polity's watcher | R2-04 | KILLED, 42.8 s, 1 | `OnlyAPolityAtWar…` | a non-belligerent watching does not refuse a hide: yes, under §3.7 |
| F13 | a polity is its own enemy | R2-04 | KILLED, 49.7 s, 70 | 25 test methods, among them `Spotting_OnlyAnotherPolitysMoveOrRetreatSpots`, `OnlyAPolityAtWar…`, the gate, `Refusals_…` | a polity never spots, watches or contacts itself: yes |
| F14 | the enemy relation from the first war only | R2-04 | KILLED, 43.6 s, 1 | `OnlyAPolityAtWar…` (the second war) | every war of the world makes its participants enemies: yes |
| R2-M01 | the hide's factor read from the NEXT cell (the verifier's text, unchanged) | R2-02 | SURVIVED on `16efc0a` (56.4 s, §12.1); now KILLED, 43.3 s, 2 | `Hide_UsesTheConcealmentOfItsOwnCell_AtTheEdgesOfTerrainBands` | Law 2: a hide acts through the cover of the cell it holds: yes |
| M01b | the hide's factor read from the PREVIOUS cell | R2-02 | KILLED, 44.0 s, 2 | `Hide_UsesTheConcealmentOfItsOwnCell_…` | the same: yes |
| M01c | `NoCover` reads the next cell | R2-02 | KILLED, 42.4 s, 2 | `NoCoverAndCannotDig_…`, `Hide_UsesThe…` | a hide needs cover in its own cell: yes |
| M01d | `NoCover` reads the previous cell | R2-02 | KILLED, 43.6 s, 2 | `NoCoverAndCannotDig_…`, `Hide_UsesThe…` | the same: yes |
| M01e | `CannotDig` reads the next cell | R2-02 | KILLED, 51.6 s, 1 | `NoCoverAndCannotDig_…` | works are dug in the agent's own ground: yes |
| M01f | `CannotDig` reads the previous cell | R2-02 | KILLED, 51.0 s, 1 | `NoCoverAndCannotDig_…` | the same: yes |
| R2-M02 | contact one cell beyond the attack range (the verifier's mutation, on the round-2 function) | R2-03 | SURVIVED on `16efc0a` (43.8 s, §12.1); now KILLED, 46.3 s, 4 | `EnemyInContact_IsTheHidersOwnAttackRange_…` | the exact condition "within the agent's attack range" (proposal §3.1): yes |
| M02b | contact one cell short of the range | R2-03 | KILLED, 51.0 s, 9 | `EnemyInContact_…`, `Refusals_…`, `Replay_…` (its change rows), the gate, `OnlyAPolityAtWar…` | the same, from the refusing side: yes |
| M02c | the ENEMY's attack range instead of the hider's | R2-03 | KILLED, 52.4 s, 2 | `EnemyInContact_…` (range 2) | the hider's own range: yes |
| R2-M04 | aim (the `Sees` refactor): only the polity's lowest-id observer counts (the verifier's R2-M04, on the refactored `Detects`) | — | KILLED, 46.6 s, 1 | `Replay_ReproducesTheConcealmentLayer_AndTheGoldenRun` on its change rows ("1Camouflaged-Spotted" expected, "1Camouflaged-Moved" actual), not on the golden | every observer of a polity counts: yes — the refactor kept the round-1 kill |

### 12.6 NOT RUN, and why

- `M7Econ.slnx`, `M7DetMath.slnx`: no file of theirs changed in round 2, and neither references `Proto.M7` (§6.2).
- The `Sim.*` tiers: nothing in `Sim.*` changed (D-079 Q5).
- The parity battery (§4) and the bench: not re-run. No code they execute changed — the round's diff under `Proto.M7/` is the action
  layer and two comments, and neither the resolvers, `EqualSides`, `ParityBatteryP` nor `Proto.M7.Bench` reference `ActionWindowBook`
  (MEASURED by a search: outside `ActionWindowsT3Tests` and lane AP's Concealment tests, every `WarTheaterWorld` in the prototype —
  T2's tests, lane E's and lane D's worlds and tests, lane MOR's campaign-morale tests, lane E's bench — is built without an action
  layer). So §4's numbers stand (INFERRED from the diff and the search; the full suite, goldens included, passes).
- A Windows run; the bench timing modes.
- Not implemented, by design: the alternatives of proposal §3.6 ((b) the pre-order factor, (d) no at-once spotting) and §3.7 ((a) every
  other polity, (b) a war-span relation); changes to T3's own channels (the agent count, the hit-and-run lifting).

### 12.7 Deferred (to whom), and an observation

- The two T3-owned channels (an Attack on an id beyond the world's agents; the hit-and-run lifting by an approach the retreated agent
  could not see): to T3's owner, or to the knowledge-seam integration after D-079 Q5.
- The enemy relation: to M8's diplomacy model, which replaces it (proposal §3.7).
- Observation (not a round-2 finding, not acted on): the verifier's own mutant log for verify-AP-r2
  (`…/scratchpad/verify-AP-r2/mut/mutants-own.log`) records its mutant R2-M07 — the ditch dug behind the line for a North or South exit —
  as SURVIVED; it was not raised as a finding, so this round neither reproduced nor fixed it. For the orchestrator.

### 12.8 Statements superseded or amended in place

This report: §0 rows (3) Prototype, Tests, Mutants and the scan; §1 (the new test file; `ActionWindows.cs`); §2 Preconditions and
Endings bullets and MEASURED vs INFERRED (the invariant); §8 new items 13–15; §9 (no new question); §10 commits; §11.3's final-tree and
filter rows (round 1's tree). Proposal: header, §0 (Hide and Camouflage rows; two new rows), §2.1 (re-evaluation), §3.1
(preconditions, endings, the anti-exploit note), §3.2 (scope; the invariant), §3.3 (the check before the hit-and-run rule), new §3.6 and
§3.7, §5.2 (the invariant for an enemy target), §8 (the applied-readings paragraph), §9 (two items). Each is marked "round 2" with its
finding id.
