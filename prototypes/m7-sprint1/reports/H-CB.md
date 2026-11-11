# H-CB — M7 lane CB: caps count battlefield units (D-082 DA5) and army scale at every stage (DA3)

Date 2026-11-11. Clone `/home/user/m7w/CB`, branch `lane/CB`, base `0d2db74` (the development branch
`claude/new-session-yavghm` as handed to this lane). ISOLATED PROTOTYPE AND DOCS ONLY: nothing in `Sim.Core`, `Sim.Data`,
`Sim.Cli`, `Sim.Tests` or `Sim.Ui` changed (D-079 Q5 OPEN); no unit data or cap value changed (D-079 §4 D10); no permanent project;
no CI file.

**Provenance.** The clone existed at the start of this run, on `lane/CB` at `0d2db74` with no commit of its own and a clean tree:
an earlier run of this lane had created it, built and tested the baseline (01:42–01:47Z, logs in
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/CB-work/base-0d2db74/`) and was interrupted
when the container restarted (about 01:53Z). No `CB-wip.bundle` existed. This run continued in that clone; the baseline it records
(§7) is that earlier run's measurement of the same commit in the same clone, not re-run.

Status words: **MEASURED** (run in this lane, on this branch), **DERIVED** (exact arithmetic, pinned by a test or shown by hand),
**INFERRED** (argued, not isolated), **PROPOSAL** (this lane's design under a delegation; decided by nobody), **READING** (an
interpretation, both alternatives stated), **DD** (a quoted Director decision).

Authority: [D-082](../../../docs/d082-morale-caps-ties-director-answers-2026-11-10.md) DA3, DA4, DA5 and their readings (DA5-R (a)),
§3 and §5; D-083 (the entry cost, unchanged here); D-079 DR23, DR24, DR25, DR26, DR27 (Q3 OPEN), §4 D10, D11, D20, §6, Q5; D-069
(no other game is named in anything this lane wrote).

---

## 0. Summary

| Brief item | State | Where |
|---|---|---|
| (1) Caps count battlefield units: ONE rule for lanes W and D | **Done.** `DirectorCaps.EntitiesOf` (soldier class min(P, cap); machine class one per machine, the crewed tube kept; uncapped one per person, lane N's CAP-5 treatment kept) used by the caps prototype, the roster, the sandbox's force creation and its Campaign Encounter (`SandboxSession.Fielded`); a formation's persons are no longer capped for a soldier class; `FormationSpec.Persons` carries the persons behind the entities, validated by both resolvers (`EntityShare`) and read as the formation's weight in lane MOR's in-battle rise. 18 moved pins, each with its measured attribution; 18 new test cases *(round 1: 15 of the 18 pins are forced by DA5 and 3 by READING CB-4 (a), this lane's choice, CB-R1-01; 24 new test cases after round 1; a force the sandbox mint cannot cover is refused, CB-R1-02; §14)* | §2 |
| (2) Design proposal | **Done (design; the combat part is a PROPOSAL, not built).** New [battle-entity-proposal.md](../../../docs/m7-plan/battle-entity-proposal.md), linked from the caps proposal, which is marked in place "(D-082 DA5)" with the text each change replaces (the sandbox proposal too) | §3 |
| (3) Army scale at every stage (DA3) | **Done as measurement and calibration path; the calibration itself is A-38/M7.14.** Research twins at turn 100 (full, six seeds) and turns 325 and 400 (capped, six seeds) MEASURED; the full twin at 325/400 is NOT RUN (cubic cost, MEASURED and extrapolated); late stages (turns 500–1630, six seeds) MEASURED: the shipped support test refuses every recruitment at 29 of 30 points (no grain in the purse); [economic-scale-proposal.md](../../../docs/m7-plan/economic-scale-proposal.md) §8 | §4 |
| (4) This report | Done | — |

---

## 1. What exists (files)

| File | Change |
|---|---|
| `Proto.M7/Caps/DirectorCaps.cs` | `EntitiesOf` (the one rule), `CountOf` = `EntitiesOf`, `MaxPersons` (soldier class: no persons cap), `FullStrengthPersons`; documentation |
| `Proto.M7/Caps/FormationRoster.cs` | `ReinforceToCaps` fills machine classes only; documentation (`SplitSizes` kept, no longer used by the sandbox) |
| `Proto.M7/Sandbox/SandboxSession.cs` | `Fielded` (the one rule for both ways a formation enters the sandbox); `AddForce` makes one formation per force; the encounter uses `Fielded`; formations carry `Persons`; the column layout of a split force (`SplitAnchors`, `SplitSpacingCells`) removed with the split |
| `Proto.M7/Tactical/BattleSetup.cs` (**lane TR's shared record, one property added**) | `FormationSpec.Persons` (null = one person per entity) |
| `Proto.M7/Tactical/EntityShare.cs` (new) | `PersonsOf`, `Validate`: the one place both resolvers read persons from |
| `Proto.M7/Tactical/TacticalBattle.cs`, `AutoResolver.cs` (**shared files: one line added and one line changed in each, with a comment**) | `EntityShare.Validate` at construction; the local-win sizes are `EntityShare.PersonsOf` |
| `Proto.M7/Tactical/LocalWinMorale.cs` (lane MOR's) | documentation only |
| `Proto.M7.Tests/LaneCB/BattleEntityRuleTests.cs` (new) | 14 test cases (§2.3) |
| `Proto.M7.Tests/LaneD/CapsRosterTests.cs`, `SandboxTests.cs` | 18 moved pins re-based, each with its attribution in the test (§2.2); one new theory (3 cases) |
| `Proto.M7.Bench/EntityBench.cs` (new), `Program.cs` (one dispatch line) | bench mode `entities` (§3) |
| `Proto.M7.Econ/ScaleProbe.cs` | `--twin-caps`: capped recruitment twins and their timing (§4) |
| `Proto.M7.Econ.Tests/ScaleProbeTests.cs` | one new test case |
| `docs/m7-plan/battle-entity-proposal.md` (new) | item (2) |
| `docs/m7-plan/caps-and-counted-objects-proposal.md`, `war-sandbox-proposal.md`, `economic-scale-proposal.md` | marked in place "(D-082 DA5)", "(D-082 DA4, DA5)", "(D-082 DA3)"; §8 of the economic proposal is new |

*Verification round 1 (§14): `Proto.M7/Sandbox/SandboxTypes.cs` (`SandboxRefusal.MintShort = 11`) and `SandboxSession.AddForce` (the mint check); `BattleEntityRuleTests.cs` 20 cases (two new tests, two new cases of `TheFightItWon_…`, two tests strengthened); comments only in `FormationRoster.cs`, `EntityShare.cs` and `CapsRosterTests.cs`; the three proposals marked in place, and one note in `war-sandbox-proposal.md` §2 (the mint check).*

---

## 2. Item (1): the one rule

### 2.1 The rule (READING DA5-R (a), applied)

A DR24 cap bounds the battle entities a formation fights as; a formation's size in persons is limited by the economy, not by the
cap. `DirectorCaps.EntitiesOf(class, P)`:
- soldier classes (heavy/aggregated infantry 50, archers and lighter infantry 60, snipers 2): **min(P, cap)**;
- machine classes (tanks, helicopters, ships, fixed-wing, mortars): **one entity per machine**, ⌈P ÷ crew⌉, refused above the cap
  (READING CB-1 (a); lane D §2.1's crewed tube and lane N's hulls and airframes kept); no crew value is shipped (CAP-3, M7.6);
- uncapped classes: **one entity per person** (lane N's CAP-5 treatment kept).

Every path that turns persons into a battle formation uses it: `DirectorCaps.CountOf`/`Check` (so `FormationRoster` refuses no soldier
formation for size), `SandboxSession.AddForce` (one formation per force) and `SandboxSession.FromEncounter` (lane W's campaign
formations), both through `SandboxSession.Fielded`. Lane W's world has no persons-to-entities step of its own (its tests field
battles by hand and collapse them with `BattleCollapse`, unchanged); M7.15's hand-off is where it will be called (proposal §9).

### 2.2 Moved pins (18 test cases), each with its measured attribution

The DA5 code (commit `c1cd31d`'s non-test changes) was first run against the unchanged tests: **18 of 1,502 failed in Release, every
one in lane D's `CapsRosterTests` or `SandboxTests`** (MEASURED, log `…/m7c/CB-work/step1/test-release-after-rule.log`). Each failure
message below is from that run; each test was then re-based to keep the property it pinned, with the attribution written in the
test.

| Test (cases) | Failure on the DA5 code (MEASURED) | Cause | Re-based to |
|---|---|---|---|
| `Create_AcceptedAtTheCap_RefusedOnePersonAbove` (heavy infantry 50, archers 60, snipers 2) | "Expected 50 / 60 / 2, Actual 9223372036854775807" (`MaxPersons`) | a soldier class has no persons cap | the three rows moved to the new `Create_SoldierClasses_AreNeverRefusedForSize_…_DA5` (accepted at any size, counted min(P, cap)); the five machine rows unchanged |
| `Create_RefusalCodes` (1) | "Expected ExceedsCap, Actual DepotShort" (51 heavy infantry, empty depot) | 51 persons fight as 50 entities | the same request now pins DepotShort; the cap-before-depot order is pinned on tanks (21 crew = 6 tanks: ExceedsCap, counted 6) |
| `Merge_RefusedAboveTheCap_…` (1) | "Expected ExceedsCap, Actual None" (30 + 21 heavy infantry) | as above | the heavy-infantry merge is accepted (counted 50, 51 persons); the over-cap merge is pinned on tanks (12 + 9 crew = 6 tanks) |
| `ReinforceToCaps_TieDense_…` (1) | "[2, 2, 2, 2, 2, ···] vs [0, 0, 0, 0, 0, ···]" | soldier formations have no persons cap to fill *(superseded attribution, round 1, CB-R1-01: READING CB-4 (a), this lane's choice, which DA5 does not force; this row and the next two moved because of it)* | the same tie-dense round robin on eight tank formations at 4 of 20; a heavy-infantry formation present gets 0 |
| `ReinforceToCaps_ExactFractionTies…` (1) | "[0, 11, 0, 12] vs [0, 0, 0, 0]" | as above | the exact cross-class tie 4/20 (tanks) = 200/1,000 (ships), helicopters last; the oracle unchanged in method |
| `ReinforceToCaps_StopsAtEveryCap…` (1) | "[5, 5] vs [0, 0]" | as above | both stopping rules on tanks ([2, 2] at the caps, [1, 1] at the empty depot); heavy infantry: [0, 0] and a direct reinforcement past 50 accepted |
| `SandboxAccounting_ConservesExactly_…` (1) | "Expected 2, Actual 1" (formations of a 90-person force) | a force is one formation | the base's two formations of 45 created explicitly at the split's anchors; the battle equals the base's (result hash pinned, below) |
| `SandboxAccounting_PrisonersAndDead…` (1) | "Expected 50, Actual 150" (the battle's start entities against the formation's persons) | the 150 were one formation of 50 entities | the base's three formations of 50 created explicitly; base hash pinned |
| `SandboxAccounting_MatchesTheBattleResult_PerFormation` (3 seeds) | "Expected 4, Actual 3" (casualty rows) | the 100 were one formation | the base's two formations of 50; base hashes pinned per seed |
| `CampaignEncounter_FieldsLaneWsFormationsByTheEntityRule_ReadingC1b` (1) | "Expected (ExceedsCap, 3000, 50), Actual (None, 50, 50)" | READING C1 decided (b) | rewritten as `CampaignEncounterAndForceCreation_FieldTheSameFormation_TheCapCountsBattleEntities_DA5` |
| `Codec_PopulatedTables_RoundTripBitExact` (1) | "Assert.True() Failure" (8 casualty rows expected) | its two sessions had fewer formations | none of its own: its sessions (above) are the base's again; unchanged and passing |
| `Replay_ReproducesAWholeSession_BitForBit` (1) | IndexOutOfRangeException (the second part, `e[1]`, of a split force) | as above | the base's five formations created explicitly; base hash of seed 4 pinned |
| `ForceCreation_LargeForce_IsLaidOutInsideTheField_AndDeploys` (1) | "Expected 20, Actual 1" | no split, nothing to lay out | rewritten as `ForceCreation_LargeForce_IsOneFormation_AndDeploys_TheBattleDoesNotYetScaleEntities_DA5` |
| `ForceCreation_SplitsAtTheDirectorCap` (1) | "[44, 43, 43] vs [50]" | as above | rewritten as `ForceCreation_IsOneFormation_WithinTheCapInBattleEntities_DA5` |

*Verification round 1 (CB-R1-01): 15 of these 18 moved pins are forced by DA5 (the cap counts entities, so a soldier formation is never refused for size and a force is one formation); the three `ReinforceToCaps` rows moved because of READING CB-4 (a). MEASURED: with alternative (b) in `ReinforceToCaps`, lane D's `CapsRosterTests` of `0d2db74` fail only in the five DA5-forced cases (§14.1).*

**The base battles (MEASURED).** The sessions re-based by explicit formations were run on the base tree `0d2db74` (a scratchpad copy
made with `git archive`, with a temporary probe test; `…/m7c/CB-work/base-probe.txt`; the copy and the probe are outside the
repository). Their result hashes are pinned in `SandboxTests` and the re-based sessions reproduce them: prisoners
`FD7F7EBC…CBC6E`; conserves `19781057…7BC8F`; AutoResolver battles seeds 11, 13, 4 `21080F44…08404`, `C7F4A428…FF841`,
`E7DE351B…D4648`; replay seed 4 `76812828…B0E06`. So those tests' other assertions keep their meaning on the same battles.

**Tests affected but not moved.** `AnyEditSequence_ConservesPeopleExactly_…` mixes soldier and machine classes; under DA5 its soldier
edits are no longer refused for size, so it accepts more edits than before (INFERRED from the rule; the count is not pinned and was not
measured), and every assertion (exact conservation, every formation within its cap, ascending ids, accepted count in [200, 3,900])
still holds (MEASURED: it passes). Lane W's tests, lane MOR/MOR2's and every resolver golden pass
unchanged (no persons are set on their formations).

### 2.3 New tests (MEASURED, all passing in Debug and Release)

`Proto.M7.Tests/LaneCB/BattleEntityRuleTests.cs` (14 cases; 20 after round 1, the new and strengthened ones marked below, §14):
- `EntitiesOf_SoldierMinOfPersonsAndCap_MachineOnePerMachine_UncappedOnePerPerson`: every class at its boundaries, 10¹² included;
- `Roster_ASoldierFormationGrowsPastItsCapInPersons_ByEveryEdit_AndStaysAtItsCapInEntities`: create, reinforce, split, transfer, merge,
  detach, conservation after each; tanks still refused past 5 × crew;
- `SandboxCatalogue_HoldsNoMachineCountedClass_AndFieldedRefusesOne`;
- `ForceCreationAndCampaignEncounter_FieldTheSameFormation_ForEveryFamilyAndSize`: lane D's path = lane W's path, five families × five sizes;
- `TheCollapse_IntegerizesTheShareOnce_ByTheLargestRemainder_Exactly` (3 seeds): 1,013 against 997, every category within one person
  of its quota, rows sum exactly, ledger conserved *(round 1, CB-R1-07: and every category equal to an independent largest-remainder oracle; before, only lane W's tests pinned the method)*;
- `ABillionPersonsPerSide_FightAsFiftyEntities_AndCollapseExactly`;
- `BothResolvers_RefusePersonsThatCannotStandBehindTheEntities_AndNullOrEqualIsTheIdentity` (and a vessel with persons refused; *round 1, CB-R1-06: and an aircraft, each refusal matched by its message*);
- `TheFightItWon_WeighsTheFormationsByTheirPersons_InBothResolvers` (5 cases): 33 (one person per entity, lane MOR's number), 88 and 0,
  each in both resolvers, the battle otherwise identical. *(Round 1, CB-R1-05: 7 cases; 88 is a resolver-level test input the one rule never produces, since 40 heavy infantry fight as 40 entities; the rule-produced cases are 33, 28 (50 entities standing for 60 persons, added) and 0.)*
- *(Round 1, CB-R1-03)* `SandboxPersons_ReachTheBattle_AndTheFightItWonWeighsThem_InBothResolvers` (3 cases): the sandbox's persons pass through lane TR's deployment into the battle; rises 33, 28 and 0 in sandbox battles of both resolvers.
- *(Round 1, CB-R1-02)* `AddForce_AForceTheMintCannotCover_IsRefusedMintShort_LoggedAndChangingNothingElse`.

`CapsRosterTests.Create_SoldierClasses_AreNeverRefusedForSize_TheyCountMinOfPersonsAndCapEntities_DA5` (3 cases).
`Proto.M7.Econ.Tests/ScaleProbeTests.CappedTwin_IsALowerBound_AndACapAboveEverySettlementIsTheFullTwin` (1 case).

---

## 3. Item (2): the design (summary; the document holds the detail)

[battle-entity-proposal.md](../../../docs/m7-plan/battle-entity-proposal.md), PROPOSAL:
- **The share and the remainder rule (implemented):** an entity stands for the rational share P ÷ n; persons become integers once,
  at the collapse, by the largest remainder (lane W's `Apportion`, ties to the lowest category): exact (Law 1), within one person of
  each quota, no per-entity state (DR23). READING CB-3: per-slot integers (the first r slots q + 1) are the alternative; they bias by
  slot (DERIVED example in the document).
- **Hit points and output (PROPOSAL E1, not built):** an entity is a block of its persons, its hit points the persons × the per-person
  MaxHp, its output proportional to its living persons; at P ≤ cap this is today's engine exactly. E2 (weighted exchange) and E3
  (today: a representative sample) are the alternatives, with consequences. **The gap today (MEASURED):** an entity fights as one
  person whatever its share, so 1,000 against 1,000 (and 10⁹ against 10⁹) is the battle of 50 against 50 (equal result hashes).
- **The AutoResolver (PROPOSAL):** its aggregate counts become persons, its frontage stays in entities, re-fitted with E1.
- **Morale weight:** an entity has none of its own; the settlement's terms (DR29's decrease, DA1's rise, D-083's entry cost, the
  army aggregation, lane W's desertion) are per formation or army and in persons already: **unchanged**. In battle the casualty-driven
  loss is exact for equal shares; lane MOR's "fight it won" sizes were entity counts and are now persons (implemented, both
  resolvers).
- **Consequences, every data row cited, none changed:** CAP-1 (306–308, `unit-families.json` :81–:83) and CAP-2 (204, :64)
  dissolve; no soldier row needs a retune; CAP-3 unchanged; CAP-4 narrows (the sniper identity's personnel is no longer bounded by
  the cap); CAP-5 unchanged in rule, with a measured performance limit.
- **Performance at large P (MEASURED, bench `entities`):** under DA5 a battle's cost depends on its entities, which the caps bound: 100
  entities and 17.8–22.8 ms per tactical pulse for 250 to 2,000 persons a side; under the superseded persons rule 500, 1,000, 2,000
  and 4,000 entities cost 336, 1,363, 4,331 and 20,332 ms per pulse (Release, `nice -n 19`, shared host, first 10 pulses).

---

## 4. Item (3): DR25's numbers at every stage (summary; the document holds the detail)

[economic-scale-proposal.md](../../../docs/m7-plan/economic-scale-proposal.md) §8:
- **Turn 100, full research twins, six seeds (MEASURED):** 13,140; 14,380; 14,200; 13,440; 10,360; 11,240 soldiers accepted at once
  (seeds 42, 1, 2, 7, 3, 5); adults bind; equal to lane D's formula in five seeds, one formation above it in seed 2.
- **Turns 325 and 400, capped twins, six seeds (MEASURED):** with at most 220 and then 1,000 orders per settlement, every order was
  accepted in every seed: 52,800 and 240,000 soldiers at once. 50,000 is recruitable at once at both stages.
- **The full twin at turns 325 and 400: NOT RUN.** Its cost grows with the cube of the orders it accepts (`Sim.Core`'s
  `RecruitmentQuery.OwnerPersonnel` and `UnitIndex` scans): 0.18, 0.39, 2.16, 14.3 and 51.1 s for 600 to 6,742 orders at turn 200
  (MEASURED, Release), and 291–303 s for each 12,000-order capped twin (twelve runs) (the cubic fit predicts 288 s). Extrapolated (INFERRED): 44–69
  hours at turn 325 and 173–247 days at turn 400, per twin. *(Superseded, round 1, CB-R1-04: those are lane D's four seeds; over the six seeds 13.6–69.4 hours and 43.2–247.0 days.)*
- **Late stages, turns 500, 650, 1000, 1629 and 1630, six seeds (MEASURED):** the purse holds no grain at 29 of the 30 points, and
  there the predicate refuses every recruitment ("cannot carry the upkeep: 40 grain needed for a year of every formation (the purse
  has 0 free)") although the adults (12.4M–61.9M) and a positive food surplus are there. The one exception is seed 42 at turn 500
  (20,140,268 grain): its twin capped at 50 orders per settlement accepted all 600 orders, 12,000 soldiers. Lane D's §2 finding 2
  (four seeds) holds in six, and lane D's turn-1630 populations reproduce exactly; seeds 3 and 5 widen the turn-1630 range to
  69.1M–104.6M. No twin can run at turn 1630, the campaign's last turn (`Sim.Core` refuses a step from day 2,196,000, the campaign's end); the first
  run of these measurements stopped there and was re-run with the last twin at 1629.
- **The calibration path (PROPOSAL):** L3 recurring maintenance sized per identity by its equipment (Law 4 holds through computed
  state), L1's support test fixed to read the soldiers' real ration sources, L4 an early brake, L6 the campaign limit. Under an example
  operationalization (50,000 costs half the surplus, 1,000 costs 1 %), the per-soldier-year cost must grow from about 2.6–2.9 at turn 200
  (about today's labour cost, ℓ = 1.99–2.86) to 28–75 at turn 325 and 243–314 at turn 1630 (DERIVED from the measured surplus: six seeds, lane D's
  four at turn 200).
- **Food orderings:** the twins and the probe points do not depend on the ordering (DERIVED from what they measure; *round 1, CB-R1-10: INFERRED, argued from what the step reads, not isolated*); lane F's
  both-ordering results stand where food is short (turns 25 and 200); at the late research stages an army's ration is 0.2–1.8 % of the
  surplus (INFERRED: both orderings feed it); lane F's comparison on research-driven worlds NOT RUN (§12).

---

## 5. Readings applied (each with both alternatives in its document)

| Reading | Applied | Alternative | Where |
|---|---|---|---|
| DA5-R (a) (D-082): a battlefield unit is a battle entity | (a): n ≤ cap entities per formation; persons by the economy | (b) the cap counts the formations of a family on one battlefield | D-082 §2 |
| **CB-1** (this lane): what a machine-class entity is | (a) one machine per entity; persons ≤ cap × crew | (b) machine entities aggregate several machines | battle-entity proposal §1 |
| **CB-2** (this lane): how many entities below the cap | (a) n = min(P, cap): one person per entity up to the cap | (b) a fixed persons-per-entity k per identity (TUNE) | §1 |
| **CB-3** (this lane): where persons become integers | (a) once, at the collapse (largest remainder) | (b) per entity slot | §2 |
| **CB-4** (this lane; labelled in round 1, CB-R1-01): which formations `ReinforceToCaps` fills | (a) machine classes only (soldier formations take no part) | (b) soldier formations too, up to their full complement of entities at one person each (keeps lane D's three pins) | battle-entity proposal §1 |
| DA4 / S1 (a) (lane D): caps apply in the sandbox | confirmed by D-082 DA4; under DA5 a force is one formation | — (decided) | sandbox proposal §4 |

---

## 6. MEASURED vs INFERRED (the main claims)

| Claim | Label | Evidence |
|---|---|---|
| The one rule: soldier min(P, cap), machine one per machine, uncapped one per person; both sandbox paths field the same formation | MEASURED | §2.3 tests |
| 18 pins moved, all in lane D's two test files, each because of DA5 *(superseded, round 1, CB-R1-01: 15 because of DA5, 3 because of READING CB-4 (a))* | MEASURED | the failing run on the DA5 code (§2.2); the alternative-(b) probe (§14.1) |
| The re-based sandbox sessions fight the base's battles | MEASURED | base result hashes from `0d2db74`, pinned and reproduced |
| Persons are conserved exactly; each category within one person of its quota *(round 1: and equal to an independent largest-remainder oracle, CB-R1-07)* | MEASURED (3 seeds, remainders) and DERIVED (largest remainder) | §2.3 |
| Today an entity fights as one person whatever its share | MEASURED (equal result hashes at 50, 1,000, 2,000, 10⁹ persons a side) | §3 |
| E1 would make numbers count in battle while the caps bound the cost | INFERRED (design, not built) | proposal §3 |
| Battle cost is bounded by entities, not persons, under DA5; quadratic in entities | MEASURED (bench, one seed, first 10 pulses; indicative wall times on a shared host) | proposal §6 |
| The local-win rise weighs persons in both resolvers | MEASURED (33, 88, 0) *(round 1: 88 is a resolver-level input the rule never produces, CB-R1-05; rule-produced 33, 28 and 0, also in War Sandbox battles through lane TR's deployment, CB-R1-03)* | §2.3, §14 |
| Nothing in the morale settlement changes under DA5 | DERIVED (each term's inputs are per formation or in persons) | proposal §4 |
| 50,000 recruitable at once at turns 325 and 400 (six seeds) | MEASURED (capped twins) | §4 |
| The full twin at 325/400 would take 44 hours to 247 days *(superseded, round 1, CB-R1-04: 13.6 hours to 247 days over the six seeds)* | INFERRED (cubic fit of MEASURED timings) | economic proposal §8.2.1 |
| The required per-soldier cost path (≈ 100× from turn 200 to 1630) | DERIVED from the MEASURED surplus (six seeds; lane D's four at turn 200) under an example operationalization (PROPOSAL thresholds) | economic proposal §8.3 |
| Late-stage recruitment refused at 29 of 30 points (turns 500–1630, six seeds) by the empty grain purse | MEASURED (probe and capped twins; the refusal text at each point) | economic proposal §8.2.2 |
| Why the purse holds no grain at late stages | INFERRED by lane D (the food baskets consume the grain stock); not isolated here either | economic proposal §2 |
| 50,000 recruitable at once for seed 42 at turn 500 | INFERRED (lane D's formula bound 16,222,940; the capped twin's 12,000 is MEASURED) | economic proposal §8.2.2 |
| An army's ration does not create a shortfall at late stages, so both orderings feed it | INFERRED | economic proposal §8.5 |
| (Round 1) A force the sandbox mint cannot cover is refused, logged, and changes nothing else; the replay reproduces the session | MEASURED | §14.1; mutants M1–M3 (§14.2) |

---

## 7. Tests (MEASURED; `nice -n 19`, `-m:1`, `-nodeReuse:false`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, solutions named, forced rebuild)

**Baseline on `0d2db74`** (the interrupted first run of this lane, same clone, 01:42–01:47Z, `CB-work/base-0d2db74/summary.txt`):
M7Proto Debug 1502/0/0 and Release 1502/0/0; M7Econ Debug 111/0/0 and Release 111/0/0; every build 0 warnings, 0 errors; the
determinism scan CLEAN (108 files); doc links 467 files, 0 broken.

**Closing runs** (script `CB-work/suite.sh`, tag `close-1`, logs `CB-work/close-1/`):

Code of commit `fc8fe1c` (the code is final there; later commits change documents only), 2026-11-11 02:41:56–02:48:58Z:

| Solution | Config | Build | Tests (passed / failed / skipped) | Test run |
|---|---|---|---|---|
| M7Proto.slnx | Debug | 0 warnings, 0 errors | **1516 / 0 / 0** (1502 baseline + 14 lane CB; the 18 moved cases are re-based, not added) | 86 s |
| M7Proto.slnx | Release | 0 warnings, 0 errors | **1516 / 0 / 0** | 34 s |
| M7Econ.slnx | Debug | 0 warnings, 0 errors | **112 / 0 / 0** (111 baseline + 1 lane CB) | 105 s |
| M7Econ.slnx | Release | 0 warnings, 0 errors | **112 / 0 / 0** | 109 s |

The M7Proto count: `LaneCB` adds 14 cases; `CapsRosterTests` loses 3 cases from one theory and gains 3 in another (net 0);
`SandboxTests` keeps its count (three tests renamed). **Determinism scan** (`scripts/check-proto-determinism.sh`): 109 files under
`Proto.M7/` (108 at the base, plus `EntityShare.cs`), no finding, CLEAN; it does not cover `Proto.M7.Econ` or the bench, as before.
**Doc links** (`python3 scripts/check-doc-links.py --all`): 468 files (467 at the base, plus the battle-entity proposal), 0 broken; and
this report by name (`--all` does not cover `prototypes/`), re-run after the last edit (§12).

**Repeated on commit `9a1fa5c`** (tag `close-2`, logs `CB-work/close-2/`, 2026-11-11 03:05:27–03:10:57Z), the same script: M7Proto
Debug and Release **1516 / 0 / 0** (test runs 48 s and 26 s), M7Econ Debug and Release **112 / 0 / 0** (92 s and 91 s), every build 0
warnings and 0 errors, the scan CLEAN (109 files), doc links 468 files and 0 broken. `9a1fa5c`'s code is `fc8fe1c`'s (`git diff
--name-only fc8fe1c 9a1fa5c` lists only `.md` files). During that run two documents were edited (the battle-entity proposal's
wording and the order of two paragraphs in the caps proposal, committed after it); the commits after `9a1fa5c` change documents
only, and no test reads them (the prototype tests read `docs/m7-spec.md` and `docs/m7-plan/unit-catalogue-and-progression.md`,
which this lane did not change).

(`M7Econ.slnx` builds `Sim.Core` and `Sim.Data` as project references, read-only; nothing in them changed. A Release build of the
solution compiles them in Debug, as lane D recorded.)

*Round 1 closing runs (on `cc3ca5a`): §14.3.*

---

## 8. Mutants (bounded; each kill backed by a semantic test)

Bound: 240 s for build and test together, 10 × the measured clean run of the same procedure (a null mutant that rewrites a source file
so the project rebuilds: 24.1 s and 14.2 s, filter `LaneD|LaneCB|LocalWinMorale`, 148 tests); the economy mutants 290 s (10 × 29.3 s,
the first of two null runs, 29.3 s and 15.6 s; filter `ScaleProbeTests.CappedTwin`). Each mutant: one exact string replaced in one file (asserted unique), a Release build, the
filtered run, the file restored from memory (`CB-work/mut/mutants.py`, `mutants_econ.py`; results `results.jsonl`,
`results-econ.jsonl`). No mutant hung; the tree was clean after each run.

| # | Mutation | Result | Killed by (semantic) | Run |
|---|---|---|---|---|
| C1 | soldier entities = persons (the superseded count) | KILLED | `EntitiesOf_…`, `Roster_ASoldierFormation…`, `TheCollapse_…` (3), `ABillionPersons…` | 22.9 s |
| C2 | soldier entities = the cap even below it | KILLED | `EntitiesOf_…`, `Roster_…`, `Create_SoldierClasses_…_DA5` | 16.5 s |
| C3 | soldier entities one below the cap | KILLED | `EntitiesOf_…`, `Roster_…`, `TheCollapse_…`, `ABillionPersons…` | 15.0 s |
| C4 | a soldier class's MaxPersons = its cap again | KILLED | `EntitiesOf_…`, `Create_SoldierClasses_…_DA5` (3) | 15.7 s |
| C5 | the check counts soldier persons | KILLED | `EntitiesOf_…`, `Roster_…`, `Create_RefusalCodes`, `Create_SoldierClasses_…_DA5` | 15.4 s |
| C6 | uncapped classes capped at 50 entities (an invented CAP-5 cap) | KILLED | `EntitiesOf_…`, `ForceCreation_IsOneFormation_…_DA5` | 14.5 s |
| R1 | `ReinforceToCaps` fills soldier formations | KILLED | the three `ReinforceToCaps_…` tests *(they pin READING CB-4 (a), round 1)* | 14.2 s |
| S1 | the sandbox fields one entity per person | KILLED | `ForceCreationAndCampaignEncounter_…`, `SandboxCatalogue_…`, `TheCollapse_…`, `ABillionPersons…` | 19.5 s |
| S2 | the encounter keeps its own rule (one per person) | KILLED | `ForceCreationAndCampaignEncounter_…`, `CampaignEncounterAndForceCreation_…_DA5`, `CampaignWorld_StateHash…`, `Replay_…` | 14.2 s |
| S3 | formations carry no persons | KILLED | `ForceCreationAndCampaignEncounter_…`, `CampaignEncounterAndForceCreation_…_DA5`, `ForceCreation_IsOneFormation_…`, `ForceCreation_LargeForce_…` | 14.5 s |
| S4 | force creation computes min(P, cap) itself | **SURVIVED: EQUIVALENT** (below) | — | 14.2 s |
| E1 | both resolvers weigh the local win by entities | KILLED | `TheFightItWon_…` (the 88 and 0 cases) | 15.4 s |
| E2 | the TacticalResolver weighs by entities | KILLED | `TheFightItWon_…` (tactical branch) | 19.9 s |
| E3 | the AutoResolver weighs by entities | KILLED | `TheFightItWon_…` (auto branch) | 13.9 s |
| E4 | no validation of persons | BUILD-FAIL (the change made code unreachable, a warning treated as an error) → re-formed as E4b | — | 7.5 s |
| E4b | no validation of persons (re-formed) | KILLED | `BothResolvers_RefusePersons…` | 13.8 s |
| E5 | persons equal to the count refused | KILLED | `BothResolvers_RefusePersons…`, `ABillionPersons…`, `TheFightItWon_…` | 13.8 s |
| E6 | persons accepted on a vessel | KILLED | `BothResolvers_RefusePersons…` | 15.4 s |
| E7 | the TacticalResolver skips the validation | KILLED | `BothResolvers_RefusePersons…` | 14.7 s |
| E8 | the AutoResolver skips the validation | KILLED | `BothResolvers_RefusePersons…` | 13.5 s |
| P1 | the probe ignores the twin cap | KILLED | `CappedTwin_…` | 15.1 s |
| P2 | the twin does not record its cap | KILLED | `CappedTwin_…` | 14.0 s |
| P3 | only the first cap of the list runs | KILLED | `CappedTwin_…` | 13.3 s |

**S4 is EQUIVALENT (DERIVED).** It replaces `Fielded(fam, persons)` in `AddForce` with `min(persons, CapOf(class))`. For every class
the sandbox can field that is the same number: a soldier class's `EntitiesOf` is min(persons, cap), and an uncapped class's cap is
long.MaxValue, so the min is persons, as `EntitiesOf` gives. A machine class, where the two differ, cannot reach `AddForce`
(`SandboxCatalogue.Find` returns only catalogue families, none machine-counted, pinned by `SandboxCatalogue_HoldsNoMachineCountedClass_…`).

Teeth and aim (ADR-015 §7.2): every property killed is one the system ought to have under D-082 DA5 (the cap counts entities; persons
are not capped for soldier classes; one rule for both sandbox paths; persons behind every entity; the local win weighs persons in both
resolvers) or under the probe's documented contract (a capped twin is capped and records its cap). *Superseded in part (round 1, CB-R1-01): R1's property, soldier formations taking no part in `ReinforceToCaps`, is READING CB-4 (a), this lane's applied reading, not one DA5 requires; under alternative (b) the system ought to have the opposite. The round's own mutants are in §14.2.*

---

## 9. Measurements (raw outputs under `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/CB-work/`)

| Run | Command | Build | Output |
|---|---|---|---|
| Base battles | temporary probe test in a `git archive` copy of `0d2db74`'s prototype | Release | `base-probe.txt` |
| M2 | `scale-probe --seeds 42,1,2,7,3,5 --driver research --turns 25,100 --twin-at 100` | `bin-1f80b6f` (the solution's Release build, Debug `Sim.Core`) | `econ/m2/` |
| M1 (abandoned) | the timing series with the Debug `Sim.Core`; stopped during the full twin (6 to 21 times slower) | `bin-1f80b6f` | `econ/m1-debug-simcore/` |
| M1R | `scale-probe --seeds 42 --driver research --turns 100,200 --twin-at 100,200 --twin-caps 50,100,200,400,none` | `binR-1f80b6f` (Release `Sim.Core`) | `econ/m1r/` |
| M3 | `--seeds 42,1,2` and `7,3,5`, `--turns 325,400 --twin-at 325,400 --twin-caps 220,1000` | `binR-1f80b6f` | `econ/m3a/`, `econ/m3b/` |
| M4 (first run, stopped) | `--seeds 42,1,2` and `7,3,5`, `--turns 500,650,1000,1630 --twin-at 500,650,1000,1630 --twin-caps 50` (script `econ/run-m4.sh`): both processes ended with `ArgumentOutOfRangeException` at the turn-1630 twin (exit 134) | `binR-1f80b6f` | `econ/m4a-crashed-at-1630/`, `econ/m4b-crashed-at-1630/` |
| M4 (rerun) | `--seeds 42,1,2` and `7,3,5`, `--turns 500,650,1000,1629,1630 --twin-at 500,650,1000,1629 --twin-caps 50` (typed at the shell, not a script, after the first run's two directories were renamed); exit 0 | `binR-1f80b6f` | `econ/m4a/`, `econ/m4b/` |
| Bench | `Proto.M7.Bench.dll entities 50,250,500,1000,2000 10` | Release, from commit `c1cd31d` plus the bench file (committed in `3ae4eec`, a commit typed `docs(m7)` that adds code: round 1, CB-R1-09) | `bench/entities-bench.txt` |

The probe's builds are copies of commit `1f80b6f`'s `Proto.M7.Econ` (unchanged since). The research driver is `sim research --auto
cheapest`'s, step for step (lane D; its traces equal the CLI's).

---

## 10. Limitations

1. The entity's combat design (E1) is a PROPOSAL, not built: until it is, a formation above its cap fights as its cap of one-person
   entities in both resolvers (MEASURED gap). The sandbox shows it most: a 3,000-person force is now one formation of 50 entities.
2. Lane W's world has no persons-to-entities step; the rule is applied where the prototype fields formations (the roster, both
   sandbox paths). M7.15's hand-off must call it.
3. `ReinforceToCaps` serves machine classes only; a reinforcement policy for soldier formations (whose persons are uncapped) is not
   designed. *(Round 1, CB-R1-01: this is READING CB-4 (a); alternative (b), filling soldier formations to their full complement of
   entities, is stated in the battle-entity proposal §1.)*
4. Uncapped families still field one entity per person: a formation of 1,000 or more cannot be fielded with generated entity ids
   (prototype engine limit), and their battle cost grows with their persons (proposal §5, §6; for the Director).
5. The bench is one seed, the first 10 pulses, on a shared host: wall times are indicative; the scaling (flat under DA5, about
   quadratic under the persons rule) is the claim.
6. The full recruitment twin at turns 325 and 400 is not measured; the capped twins are lower bounds. No twin can run at the
   campaign's last turn (1630); the latest is at 1629. The late stages are sampled at five turns per seed; lane D's denser seed-42
   sampling shows the purse switching on and off between such turns, and the other seeds were not sampled more densely.
7. The DA3 cost path uses the food surplus as the measure of what a polity can spare and an example operationalization of
   "readily" and "difficult" (PROPOSAL).
8. No Windows run; no hardware GPU (none needed).

---

## 11. Proposals and questions for the Director (only genuinely undecided ones)

1. **E1 for M7.6** (battle-entity proposal §3, §8 item 1): an entity standing for many persons fights as a block of them. Delegated
   design (D10, M7.6), flagged because it decides whether DR25's numbers count on the battlefield.
2. **READING CB-1** (machine classes: one machine per entity): confirm (a) or choose (b).
3. **CAP-5 performance** (proposal §5, §6): the measurement D10 asks for now exists; whether uncapped families get a performance cap
   (a value only the Director can set) or aggregate like soldier classes is the Director's.
4. Reported for the DA3 calibration (no decision asked; D-082 §5 keeps the numbers OPEN for A-38 and M7.14): late-stage recruitment
   under the shipped support test (economic proposal §8.2.2) and the size of the per-soldier cost path (§8.3).

*Round 1: READING CB-4 (battle-entity proposal §1) is applied like CB-2 and CB-3 and needs no decision unless the Director prefers (b).*

---

## 12. What ran, what was NOT RUN, what was deferred

**RAN (MEASURED):**
- the baseline (the interrupted first run, same clone and commit) and the closing runs of §7;
- the DA5 code against the unchanged tests (18 failures, §2.2) and the re-based suite;
- the base battles on a scratchpad copy of `0d2db74`;
- 27 bounded mutant runs (§8): 22 on the prototype (2 null, 19 mutants, 1 re-formed) and 5 on the economy prototype (2 null,
  3 mutants); none hung;
- the economy probe runs M2, M1R, M3, M4 (M1 with the Debug `Sim.Core` abandoned and kept as a record; M4's first run stopped
  at the turn-1630 twin and was re-run with the last twin at turn 1629, §9);
- the `entities` bench;
- the determinism scan and the doc links (§7);
- the originality check (D-069): no other game is named in any file this lane changed (grep of the diff, §13).

**NOT RUN:**
- the full recruitment twin at turns 325 and 400 (§4: 44 hours to 247 days per twin, extrapolated; *round 1, CB-R1-04: 13.6 hours to 247 days over the six seeds*);
- a twin at turn 1630: the campaign has no next turn for the twin's step (§4);
- lane F's food-ordering comparison on research-driven worlds (its fixtures are order-less; its labour twins at those turns carry the
  same cubic cost); the orderings are reported from lane F where they bind;
- E1 in either resolver, and so the parity battery with it (D-079 D11, Q4 OPEN): design only;
- `M7DetMath.slnx` (not touched), the G2 graphics prototype (not this lane's), `scripts/validate.sh` (no `Sim.*` change), a Windows
  run.

**Deferred:**
- E1, its geometry and the AutoResolver's re-fit: M7.6 with M7.9/M7.11;
- the campaign → battle hand-off calling the rule: M7.15;
- the DA3 calibration (L3 per identity, L1's support test): A-38 and M7.14, after Q3;
- a research-fixture generator for lane F's comparison: with A-38's calibration.

---

## 13. Observations (not changed)

- `M7Econ.slnx`'s Release build compiles `Sim.Core` in Debug (lane D recorded it); the same twins ran 6 to 21 times slower with it
  (M1 against M1R, a shared host). The accepted counts are identical (seed 42 at turn 100: 13,140 in both builds). Measurement runs of the probe should
  use a Release `Sim.Core` (build the project, not the solution).
- `Sim.Core`'s `RecruitmentQuery.OwnerPersonnel` scans every personnel row and finds each unit by a linear scan, so one step of n
  accepted recruitment orders costs about n³/6 row visits (§4). A campaign polity that issues thousands of recruitment orders in one
  turn would meet it too (INFERRED). Not changed (D-079 Q5).
- `scale-probe --twin-at` at the campaign's last turn ends the process with `Sim.Core`'s `ArgumentOutOfRangeException` ("sim day
  2196000 is outside the campaign [0, 2196000)") at that seed's turn-1630 twin, and the seeds after it never run (MEASURED, the first
  M4 run). The `--twin-at` path is lane D's, the twin lane F's; a check of `--twin-at` against the last turn before the run starts
  would make it a usage error. Not changed (not needed for this brief's measurements).
- D-069: a case-insensitive grep of every line this lane added (`git diff 0d2db74 -U0`: 1,829 lines, this report included) for 45
  names of other games and studios (`CB-work/d069-pattern.txt`) found none; nothing this lane wrote names another game.

---

## 14. Verification round 1 (fix round, 2026-11-11)

**Provenance.** Ten verifier findings (CB-R1-01..10) on `f454bec`. This round worked in the lane clone `/home/user/m7w/CB` on
`lane/CB`; every reproduction and every mutant ran in scratchpad copies made with `git archive` (`f454bec` for reproduction,
`cc3ca5a` for the round's mutants), never in the clone, and the copies were compared file by file with their commit after each
mutant. Raw outputs: `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/CB-fix1/`. Each finding
was reproduced before anything was changed; a finding's word alone changed nothing. Commits of this round: `1c6f69c` (code and
tests), `cc3ca5a` (documents; comments only in code), then documents only: every commit after `cc3ca5a` (this section, the marks
in this report, a note in the sandbox proposal and the counts; `git diff --name-only cc3ca5a HEAD` lists `.md` files
only).

**Baseline on `f454bec`** (MEASURED, `base-f454bec/summary.txt`, 03:46–03:52Z): M7Proto Debug and Release 1516 / 0 / 0; M7Econ
Debug and Release 112 / 0 / 0; every build 0 warnings, 0 errors; determinism scan CLEAN (109 files); doc links 468 files, 0 broken.

### 14.1 Verdicts

| Finding | Severity | Verdict | Reproduction (MEASURED unless marked) | Fix |
|---|---|---|---|---|
| CB-R1-01 ReinforceToCaps choice attributed to DA5 | minor | **REPRODUCED** | Alternative (b) (`ReinforceToCaps` selects `Class != Uncapped && FullStrengthPersons > 0`, max = `FullStrengthPersons`) with lane D's `CapsRosterTests` of `0d2db74`: Failed 5, Passed 22 of 27; the five are the pins DA5 forces (`Create_AcceptedAtTheCap_RefusedOnePersonAbove` × 3, `Merge_RefusedAboveTheCap_…`, `Create_RefusalCodes`); the three `ReinforceToCaps` tests pass (`a1-test.log`). With (b) and this lane's tests: exactly the three re-based `ReinforceToCaps` tests fail (`a1b-test.log`). | Labelled **READING CB-4** with both alternatives ([battle-entity-proposal.md](../../../docs/m7-plan/battle-entity-proposal.md) §1); (a) stays applied (a reading, no Director decision invented). Attributions corrected in place: `FormationRoster` (two comments), the three tests' comments and inline notes, caps proposal "Ordering", battle-entity proposal §5 and §8, this report §0, §2.2, §5, §6, §8, §10, §11. No code behaviour changed. |
| CB-R1-02 AddForce above the mint throws after mutating | minor | **REPRODUCED** | At `f454bec`: heavy infantry, `MintOpening + 1`: `InvalidOperationException: overdraft`; log rows 0; ledger accounts 5 → 6; the next force got id 2 (fresh: 1); `Replay(session)` hash ≠ the session's; two forces of 6 × 10¹¹: the second threw; the uncapped gun crews the same (`probe-run1.log`). | `SandboxRefusal.MintShort = 11`: `AddForce` refuses a force above the mint's balance before anything is minted (logged). After the fix, the same probe: `MintShort`, 1 log row, accounts 5 → 5, next id 1, replay equal (`probe-fix-run.log`). New test `AddForce_AForceTheMintCannotCover_IsRefusedMintShort_LoggedAndChangingNothingElse` (also the exact boundary). |
| CB-R1-03 no test follows the sandbox's persons into its battle | minor | **REPRODUCED** | Verifier mutant V1 (deployment close drops `Persons`) on `f454bec`: full M7Proto Release suite 1516 / 0, SURVIVED (25.8 s; bound 336 s = 10 × the 33.6 s null run) (`mut-repro/`). | New theory `SandboxPersons_ReachTheBattle_AndTheFightItWonWeighsThem_InBothResolvers` (3 cases): the deployed setup carries each formation's persons, and with lane MOR's c = 200 the rise is 33, 28 and 0 for 50, 60 and 5,000 persons (entity weights would give 33 each time), AutoResolver (seeds 1, 3) and TacticalResolver (seed 3). |
| CB-R1-04 four-seed extrapolation in six-seed sections | minor | **REPRODUCED** | A fresh probe run without twins (Release `Sim.Core` built from `f454bec`, six seeds, turns 325 and 400) reproduces this lane's M3 rows 12 of 12 (`econ-m3probe/`). Six-seed formula bound: 1,330,040–2,289,080 (turn 325) and 5,638,120–10,078,380 (turn 400); the same cubic fit (DERIVED): 66,502–114,454 orders and 13.6–69.4 hours; 281,906–503,919 orders and 43.2–247.0 days. | Six-seed ranges written beside the four-seed ones, marked in place: economic proposal §8.2 and §8.2.1; this report §4, §6, §12. The conclusion (the full twin is not feasible here) stands. |
| CB-R1-05 "10 entities for 40 persons → 88" is not a rule-produced pairing | minor | **REPRODUCED** | `EntitiesOf(HeavyInfantry, 40)` = 40; a heavy-infantry formation of Count 10 and Persons 40 is accepted by both `TacticalBattle` and `AutoBattle` (`probe-run1.log`); `EntityShare.Validate` checks P ≥ n and the land domain only. | The 88 case labelled a resolver-level TEST INPUT (test comment, battle-entity proposal §4, this report §2.3, §6); a rule-produced case above the cap added to `TheFightItWon_…`: the winner's 50 entities standing for 60 persons → 28 (seeds 1 and 3, both resolvers); the boundary gap stated in `EntityShare.Validate`'s comment and as a PROPOSAL with both options for M7.15 (battle-entity proposal §9). No check added to the battle (a design choice for M7.15). |
| CB-R1-06 only the vessel half of `Validate` is tested | nit | **REPRODUCED** | Verifier mutant V6 (refuse naval only): full suite 1516 / 0, SURVIVED (28.8 s) (`mut-repro/`). | `BothResolvers_RefusePersons…` also refuses `Persons` on an aircraft formation (strike aircraft 13 of lane N's amphibious scenario, Persons 2 ≥ Count 1, so only the domain can refuse it), matches each refusal by its message and constructs both battles without `Persons`. |
| CB-R1-07 the collapse test does not pin the largest remainder | nit | **REPRODUCED** | Verifier mutant V13 (every leftover to Fit): KILLED only by lane W's `Apportion_AlwaysSumsExactly_…` and `Apportion_TieDense_…`; the three `TheCollapse_…` cases passed (26.4 s) (`mut-repro/`). In each seed the 1,013-person formation has one leftover person whose largest remainder is Dead (34 of 50 against 16), and "within one of its quota" accepts it on Fit (quota 0) (design probe). | `TheCollapse_…` compares every category with an independent largest-remainder oracle (a sort, not `Apportion`'s scan) and asserts that some leftover goes past Fit. |
| CB-R1-08 "mortars" and the stale 10/50 = 12/60 tie | nit | **REPRODUCED** | The three moved `ReinforceToCaps` tests use tanks, ships and helicopters (and heavy infantry, uncapped taking no part); no mortar (grep). The cross-class tie is 4/20 = 200/1,000. | Caps proposal "Ordering" note corrected in place (the replaced text quoted). |
| CB-R1-09 `3ae4eec` typed `docs(m7)` but adds code | nit | **REPRODUCED** | `git diff --name-only 3ae4eec~1 3ae4eec` lists `Proto.M7.Bench/EntityBench.cs` and `Program.cs` with the three documents. | Recorded here and in §9; history is not rewritten (the commit is an ancestor of `f454bec`, already handed back in the lane bundle `CB.bundle` of 03:11Z, whose head is `f454bec`; a rewrite would invalidate it). |
| CB-R1-10 two DERIVED labels on an argued statement | nit | **REPRODUCED** | economic proposal §8.2.2 last bullet and §8.5 first bullet, this report §4; this report defines DERIVED as exact arithmetic or work shown by hand. | Relabelled INFERRED in place, with the reason (argued from what the step reads; the DR27 orderings are lane F's model and cannot run in `Sim.Core`). |

No finding was refuted.

### 14.2 Mutants of this round (bounded; each kill backed by a semantic test)

Procedure: `CB-fix1/scripts/mut.py` on a scratchpad copy of `cc3ca5a`: one exact string replaced per site (asserted unique), a
Release build of `M7Proto.slnx` and the full suite, the files restored from memory and compared, under one wall-clock bound for build
and test. Null runs: 44.2 s and 27.3 s (1522 / 0); bound **442 s** (10 × the first null run). No mutant hung.

| # | Mutation | Result | Killed by (semantic) | Run |
|---|---|---|---|---|
| V1 | lane TR's deployment close drops `Persons` (CB-R1-03) | KILLED | `SandboxPersons_ReachTheBattle_…` (3 cases) | 31.5 s |
| V1b | V1 with the test's two deployed-`Persons` assertions removed (teeth check, filter `LaneCB`) | KILLED | the same theory by its morale part alone: A's end morale 873 against 868 expected (the 60-person case weighed by entities) | 18.4 s |
| V6 | `Validate` refuses naval formations only (CB-R1-06) | KILLED | `BothResolvers_RefusePersons…` (the aircraft case) | 29.2 s |
| V13 | every leftover person to Fit (CB-R1-07) | KILLED | `TheCollapse_…` (3 seeds) and lane W's two `Apportion_…` tests | 34.3 s |
| V12 | equal remainders to the highest category (extra, not a finding) | KILLED | lane W's `Apportion_TieDense_…` only (these battles have no tie at a leftover choice) | 30.2 s |
| M1 | the `MintShort` check never refuses (CB-R1-02) | KILLED | `AddForce_AForceTheMintCannotCover_…` ("overdraft") | 31.0 s |
| M2 | `MintShort` off by one (a force of exactly the balance refused) | KILLED | `AddForce_AForceTheMintCannotCover_…` (the boundary) | 31.2 s |
| M3 | checked against the mint's opening, not its balance | KILLED | `AddForce_AForceTheMintCannotCover_…` (the second force: "overdraft") | 29.8 s |

Teeth and aim (ADR-015 §7.2): V1 and V1b pin that the persons the sandbox fields reach its battle and weigh lane MOR's rise there
(DA5: one formation's persons stand behind its entities; lane MOR's rule weighs sizes); V6 pins `Validate`'s documented contract for
aircraft; V13 pins the largest-remainder method lane W specified; M1–M3 pin that a sandbox edit is refused, logged and changes nothing
when the mint cannot cover it (lane D's invariant that only logged edits reach the session). R1 of §8 pins READING CB-4 (a), a reading,
not a requirement of DA5.

### 14.3 Closing runs (MEASURED; `CB-fix1/suite.sh`, tag `close-cc3ca5a`, the same flags as §7)

Code and documents of commit `cc3ca5a`, 2026-11-11 04:13:44–04:19:20Z (the commits after it change `.md` files only, so the runs stand
for the code; the doc links were re-run after the last document edit: 468 files, 0 broken, and this report by name):

| Solution | Config | Build | Tests (passed / failed / skipped) | Test run |
|---|---|---|---|---|
| M7Proto.slnx | Debug | 0 warnings, 0 errors | **1522 / 0 / 0** | 59 s |
| M7Proto.slnx | Release | 0 warnings, 0 errors | **1522 / 0 / 0** | 23 s |
| M7Econ.slnx | Debug | 0 warnings, 0 errors | **112 / 0 / 0** | 92 s |
| M7Econ.slnx | Release | 0 warnings, 0 errors | **112 / 0 / 0** | 88 s |

Determinism scan (`scripts/check-proto-determinism.sh`): 109 files under `Proto.M7/`, no finding, CLEAN (no file added under
`Proto.M7/` in this round). Doc links (`python3 scripts/check-doc-links.py --all`): 468 files, 0 broken; and this report by name
(`--all` does not cover `prototypes/`), after its last edit.

The M7Proto count: 1516 + 6 (`TheFightItWon_…` + 2 cases, `SandboxPersons_…` 3 cases, `AddForce_AForceTheMintCannotCover_…` 1).
`LaneCB` holds 20 cases. M7Econ is unchanged in this round (no economy code touched; run for completeness).

### 14.4 Observation (not a verifier finding; not fixed)

- **An uncapped force above 2,147,483,647 persons still throws after mutating (MEASURED, pre-existing).** `AddForce` of the gun
  crews (uncapped: one entity per person) with 2,147,483,648 persons passes the mint check, the transfer moves the persons to a new
  account, and then the entity count's `checked((int)…)` in `NewFormation` throws `OverflowException`: no formation, no log row,
  2,147,483,648 persons in an account no formation owns, the session hash no longer a replay's (`probe-run1.log` on `f454bec`,
  `probe-fix-run.log` on `cc3ca5a`). The order of operations is lane D's (before lane CB), and DA5 did not change the uncapped
  count; a soldier force cannot reach it (at most 60 entities). Not fixed here because no finding names it; a one-line remedy for a
  later round: take the entity count before the formation's id and account (or refuse a count above the engine's limit).

### 14.5 What ran, NOT RUN, deferred (this round)

**RAN (MEASURED):** the baseline on `f454bec` (both solutions, Debug and Release, scan, doc links); the reproduction probes
(CB-R1-01 alternative (b) twice; CB-R1-02 and -05 probe, re-run on `cc3ca5a` after the fix; mutants V1, V6, V13 with a null run on
`f454bec`; a fresh scale probe at turns 325 and 400, six seeds, with a Release `Sim.Core` built from this branch by building the
`Proto.M7.Econ` project, as §13 advises); design probes for the new tests (sandbox rises and collapse remainders); the mutants of
§14.2 with two null runs and the V1b teeth check; the closing runs of §14.3; the determinism scan and the doc links; this report's
links by name; the D-069 grep of every line this round added (`git diff f454bec -U0` at the round's last commit, 390 lines, the 45 names of
`CB-work/d069-pattern.txt` as an extended regular expression, a control line matching): no other game is named.

**NOT RUN:** the full recruitment twin (unchanged, §12); `M7DetMath.slnx` (not touched); `scripts/validate.sh` (no `Sim.*`
change); a Windows run. No probe was re-run for CB-R1-08, -09 and -10 (they are text and history findings, reproduced by reading).

**Deferred:** the choice between READING CB-4 (a) and (b) (applied (a); no decision asked); the battle-boundary check of the one rule
(M7.15, battle-entity proposal §9); the uncapped overflow of §14.4 (a later round, if a verifier confirms it).
