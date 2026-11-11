# H-MOR3 — M7 lane MOR3: without fighting, no morale change (D-085 DD1)

Date 2026-11-11. Clone `/home/user/m7w/MOR3`, branch `lane/MOR3`, base `51ab032`. ISOLATED PROTOTYPE AND DOCS ONLY: nothing in
`Sim.Core`, `Sim.Data`, `Sim.Cli`, `Sim.Tests` or `Sim.Ui` changed (D-079 §6, Q5 OPEN; D-085 §5); no unit data or cap changed; no
permanent project; no CI file. Files touched: `prototypes/m7-sprint1/` (solution `M7Proto.slnx` only; M7Econ untouched) and
`docs/m7-plan/morale-proposal.md`.

Status words: **MEASURED** (run in this lane, on this tree; the test, bench or command is named), **DERIVED** (exact arithmetic or a
code-path argument, stated), **INFERRED** (argued, not isolated by a run), **CITED** (read from the code, file named), **PROPOSAL**
(lane MOR3's design under D-085 §4's delegation; decided by nobody), **READING** (an interpretation, both alternatives stated), **DD**
(a quoted Director decision).

Authority: D-085 **DD1** (DECIDED: "No fighting doesn't affect morale at all then. There has to be done fighting to alter morale"),
**DD1-R1 (a)** (fighting read per battle) and **DD1-R2 (a)** (at least one attack resolved against an enemy formation, whether or not
it dealt damage), both APPLIED as D-085 records them; D-085 §3 (DA1 and DB1 narrowed; MOR2's PROPOSAL (a) superseded where no
fighting took place; DR29 unchanged), §4 (DELEGATED: each resolver's fought report and its boundary test; the in-battle part of the
entry cost; the War Sandbox and lane S's siege battles), §5; D-083 DB1 and its readings; D-082 DA1, DA6, DA7; D-079 DR29, DR30, D11,
Q4, Q5; `CLAUDE.md` Laws 1–5; D-069 (nothing player-facing names another game; this lane adds no player-facing text). The design is
`docs/m7-plan/morale-proposal.md` **§12** (PROPOSAL where design); this report says what exists and what was measured. No Director
decision is invented or reopened.

---

## 0. Summary

| Brief item | State | Evidence |
|---|---|---|
| (1a) What fighting is, per resolver and domain | **Done** (PROPOSAL; implemented). `BattleResult.FirstFightMs` (null: no fighting) and `Fought`, set at the code points that count an attack: TacticalResolver melee blow, fired missile shot (hit or miss), crew-served and domain weapon shot at an enemy entity, hull or airframe, boarding fight, ram (breach or glance); AutoResolver melee in contact and volleys. Two edges labelled: **READING MOR3-R1** (a bare-point bombard is fighting only when its blast reaches an enemy entity or hull) and **READING MOR3-R2** (a bow contact that is not a ram is no fighting) | proposal §12.2; §3 below |
| (1b) The settlement under DD1-R1 (a) | **Done** (implemented): a fought battle settles as before; a battle without fighting changes no army's morale (no entry cost, no DR29 decrease, no DA1 rise; the reported winner still recorded); the settlement row gains `Fought`, **codec version 3** (111 bytes) | proposal §12.3; §3 |
| (1c) The in-battle part of the entry cost | **Done** (PROPOSAL **C1**, implemented, default 0): charged at the battle's first fighting instant (at the start of that instant's resolution, before its casualties, morale losses and routs), at its own entry for a later arrival after it, never in a battle without fighting; C2 (dropped) and C3 (refund) weighed | proposal §12.4; §3 |
| (1d) War Sandbox; lane S's sieges | **Sandbox: implemented through the resolvers** (it settles nothing; with the part on, a Quick Battle without fighting charges nothing — MEASURED); its inspection and preview are a PROPOSAL. **Siege: PROPOSAL (a), not implemented** (the stand-in resolves no attack and never reaches the settlement) | proposal §12.5, §12.6 |
| (1e) Superseded statements | **Marked in place**, struck where false, with dated bold notes `[MOR3, 2026-11-11, D-085 DD1 …]`; the list is proposal §12.9 | proposal |
| (2) Prototype and tests | **Done**: `NoFightingTests` (21 methods, 30 cases) and the 10 moved test methods of lanes MOR and MOR2 re-pinned with measured attribution (23 failing cases at the first run, §4). Every brief test exists (§3) | §3, §4 |
| (2b) Lane P's battery | **Re-measured** (the resolvers' outputs change when the part is on): off digests = lane P's and lane MOR's (identity); 0 battles without fighting, 0 report/count disagreements; on-numbers and rule P1 in §5. Lane P's acceptance stays **OPEN** (D-079 Q4) | §5 |
| Mutants | **[MOR3, verification round 1, 2026-11-11: the verifier's seven surviving mutants reproduced, with this round's V1b; V1, V1b, V2, V5a, V5c and V6 now KILLED by new tests; V39 and V40 (state hash) recorded; §13]** **36 bounded mutants run: 35 KILLED**, each by named semantic tests (R1b only after this lane added the melee-miss test it revealed was missing); **1 SURVIVED, argued EQUIVALENT with measurement** (R3: a fired direct shot's line always meets an enemy disc — 0 such misses in the full suite and in 3,200 tuning-battery battles under a throwing instrumentation); 2 more argued equivalent and NOT RUN (E1, E2); no hang | §6 |
| Tests | **[MOR3, verification round 1, 2026-11-11: 1553 → 1559 passed, 0 failed, 0 skipped in Debug and in Release; §13]** `M7Proto.slnx`: **1522 → 1553 passed, 0 failed, 0 skipped in Debug and in Release** (+30 `NoFightingTests` cases; lane MOR2's AutoResolver withdrawal theory 3 → 4 cases) | §8 |
| Checks | determinism scan **CLEAN** (109 files, 0 pattern groups with findings); doc links **0 broken** (`--all`: 480 files) | §8 |

---

## 1. What exists (files)

| File | Role |
|---|---|
| `Proto.M7/Tactical/BattleResult.cs` | `FirstFightMs` (report field outside `ResultHash`) and `Fought` |
| `Proto.M7/Tactical/TacticalBattle.cs` | `_firstFightMs`; `Fought(t)` at the melee and missile attack sites; `ChargeOnFirstFight(t)` at the start of `Resolve`; `EnterIfPlaced` charges only once fighting has begun; `Form.EntryChargedAtMs`; state hash carries the first fight and the charge instants only while the in-battle part is on |
| `Proto.M7/Tactical/TacticalBattle.DomainCombat.cs` | `Fought(t)` for a domain shot with a target, for a bare-point blast that reaches an enemy entity or hull (READING MOR3-R1 (a)), for a boarding fight |
| `Proto.M7/Tactical/TacticalBattle.Ram.cs` | `Fought(t)` for a ram; the collision documented as no attack (READING MOR3-R2 (a)) |
| `Proto.M7/Tactical/TacticalBattle.Domain.cs` | vessel and aircraft entries on the new `EntryMorale.Enter` contract (cost 0: outside the in-battle charge, as before) |
| `Proto.M7/Tactical/AutoResolver.cs` | `_firstFightStep`; `Fought(k)` at its three attack sites; `ChargeOnFirstFight(k)` at the start of phase 5; `FirstFightMs = StartMs + step × StepMs`; state hash as above |
| `Proto.M7/Tactical/EntryMorale.cs` | the shared function: `Enter` records the entry (charges only when `fightingBegun`), `ChargeOnFighting` charges an entered, uncharged formation once |
| `Proto.M7/Tactical/BattleSetup.cs` | `TacticalRules.EntryMoraleCost` doc: the charge's instant under C1 (default still 0) |
| `Proto.M7/Morale/CampaignMorale.cs` | `CampaignMorale.IfFought` (the one place the settlement applies DD1); `BattleFighting.SettlesAsFought` (DD1-R1 (a): the battle's answer for any of its formations) |
| `Proto.M7/Morale/MoraleCodec.cs` | `MoraleSettlementRow.Fought`; codec **version 3**, 111 bytes a row; v1/v2 tables and a Fought byte above 1 refused |
| `Proto.M7/Support/SupportTypes.cs`, `SupportWorld.cs` | `SupportBattle.Fought` (default true); the three settlement terms gated; the row records Fought; an in-battle charge in a battle without fighting refused |
| `Proto.M7.Bench/EntryBench.cs` | `entry-parity` also counts battles without fighting and report/count disagreements |
| `Proto.M7.Tests/Morale/NoFightingTests.cs` (new) | the D-085 tests (§3) |
| `Proto.M7.Tests/Morale/EntryCostTests.cs`, `EntryCostRound1Tests.cs`, `EntryCostRound2Tests.cs`, `CampaignMoraleTests.cs` | lane MOR2's moved pins re-pinned with attribution (§4); `HandOff` reads `Fought` from the report; `ThreeFormations` made internal; MOR2's helper `Distant` removed from `EntryCostRound1Tests` (its one user re-pinned) |
| `docs/m7-plan/morale-proposal.md` | new §12; in-place marks (§12.9) |
| `prototypes/m7-sprint1/reports/H-MOR3.md` | this report |

---

## 2. The design in brief (proposal §12 has the reasoning)

- **Fighting** (DD1-R2 (a)): the first attack resolved against an enemy formation, hit or miss, reported as an instant by both
  resolvers at the code points that count attacks. An instant rather than the attack count because the in-battle part needs "since
  when", and because the count also counts bare-point rounds on empty ground (READING MOR3-R1). On lane P's tuning battery the report
  and the count agree in every battle (MEASURED, §5).
- **READING MOR3-R1** (a) APPLIED: a bare-point bombard is fighting only when its blast reaches an entity or hull of an ENEMY formation;
  (b) every round counts. **READING MOR3-R2** (a) APPLIED: a bow contact resolved as a collision (no hit roll, no damage to the struck
  hull) is not an attack; (b) it counts. **READING MOR3-R3** (a) APPLIED: DD1 governs what a battle does to the armies' morale (the
  settlement) and the in-battle entry charge; the resolvers' in-battle dynamics that need no attack (recovery while holding, lane TR's
  inactivity rout) and lane W's desertion of routed men (persons) are unchanged; (b) DD1 also holds every formation's in-battle morale
  still until the first fight.
- **Settlement** (DD1-R1 (a)): entry = fought ∧ entered ? E : 0; decrease = fought ? DR29 : 0; rise = fought ∧ won ∧ entered ? DA1 :
  0; one clamp; after = before for a battle without fighting. The row records Fought (codec v3).
- **In-battle part, PROPOSAL C1**: at the battle's first fighting instant for every formation that has entered by then (wherever it
  is), at its own entry for a later arrival after it, never in a battle without fighting; once per formation; default 0. C2 (drop it)
  meets D-085 §4 trivially but leaves the battle and the sandbox blind to DB1; C3 (charge at entry, refund if unfought) fails D-085 §4
  because the charged morale has already acted.
- **War Sandbox**: nothing to settle; DD1 reaches it through the resolvers (MEASURED). Inspection/preview: PROPOSAL.
- **Siege**: PROPOSAL (a): DD1 applies when the land module resolves siege battles (C15, ADR-060 §2.5), through the same report;
  lane S's three siege-morale event terms then read it too. Not implemented: the stand-in resolves no attack and never reaches the
  campaign settlement (CITED).

---

## 3. Tests (MEASURED: all pass in Debug and in Release)

`Proto.M7.Tests/Morale/NoFightingTests.cs` — 21 methods, 30 cases. ~~Every oracle of "fighting" is observed from outside the rule: the
tactical engine's event trace (executed `Fire`, `DomainFire`, `Boarding`, `Ram` events) and the AutoResolver's step log (a recorded
attack mode).~~ **[MOR3, verification round 1, 2026-11-11, MOR3-R1-01: struck — it overstated the oracle. The trace's executed `Fire` events include
missile shots that had no line of fire, which are no attacks, and the trace alone cannot tell them apart; no test here had such a
shot. Since round 1 the trace's attack events serve as the oracle only once validated against the battle's own attack count
(`AttackEvents`: every executed `Fire` event and land `DomainFire` event was a counted attack — every battle the tests read it on
passes), and a battle with such shots is observed with a line-of-fire oracle that recomputes each shot's line from outside the rule
(`RunObservingShots`); the AutoResolver's oracle is its step log, with the line of fire recomputed from its axis points where a volley
was due. 26 methods, 36 cases after round 1 (§13).]**

| Test | What it pins (MEASURED values) |
|---|---|
| `TheFirstAttackOfEveryDomain_IsTheBattlesFirstFightingInstant_ObservedFromTheTrace` (7 cases) | first fighting instant = the trace's first attack, with its kind, shooter and target: melee 148,000 ms; a missed arrow 0 (one Fire, no Damage in the battle); coastal guns at a hull 0; vessels' line battle 0; strike aircraft 14,000; boarding 26,000; a glancing ram 198,000 |
| `AutoResolver_TheFirstStepWithAnAttack_IsTheBattlesFirstFightingInstant_ObservedFromItsStepLog` | melee step 74 = 148,000 (the TacticalResolver's instant); one volley step 0; none → null |
| `DD1_R2a_AMeleeBlowThatMisses_IsFighting_AtItsOwnInstant` | 1 v 1 heavy infantry, seed 5 (found by a probe search): both first blows at 148,000 miss (no Damage then), the first damage at 156,000; first fighting instant 148,000 |
| `TheReportAgreesWithTheAttackCount_AndIsTheFirstAttack_OnLanePsTuningBattery_BothResolvers` | 20 scenarios × seed 1, both resolvers: report = first attack observed; Fought ⟺ Σ AttacksMade > 0 |
| `READING_MOR3_R1_ABombardOfABarePoint_IsFightingOnlyWhereItsBlastReachesAnEnemy` | riflemen's point: fought at 6,000 (17 killed), charge row (6,000, 2, 800, 50); empty point: 59 rounds, attack count 59, report null, no charge; the decisive "win" over riflemen who fled by inactivity settles 800,000 → 800,000 for both armies (Fought 0) |
| `READING_MOR3_R1_ABarePointBlastOnAnEnemyHull_IsFighting_OnAFriendlyHull_IsNot` | enemy transport at the point: fought at 6,000; friendly transport: hull hits on it, report null |
| `READING_MOR3_R2_ARamCollisionAlone_IsNoFighting` | lane RAM's RAM2-V2 collision at 60,000: report null at that instant and at the Standoff (360,000); 0 attacks |
| `DD1_TheBoundary_ExactlyOneAttackThatMisses_IsFought_NoAttackIsNot_BothResolvers` | one missed arrow → both armies 800,000 → 750,000 (entry only; Standoff, no winner); no attack → 800,000 (Fought 0); AutoResolver the same; turn-exact with the part on: nothing before 214,000, both formations charged at 214,000 |
| `DD1_AnUnfoughtDecisiveWin_ChangesNeitherArmysMorale_BothResolvers` | a formation at 250 leaves at once, SideEliminated, side 1 decisive: 250,000 and 800,000 unchanged; the winner's row WinnerSide 1, Fought 0, rise 0 |
| `DD1_R1a_AFormationThatEnteredAFoughtBattleWithoutStrikingABlow_PaysTheEntryCost_BothResolvers` | a fourth formation (0 attacks, 0 damage either way) in a battle fought from 7,332,000: entry 50,000, rise 160,000 → 910,000, both resolvers |
| `DD1_TheSettlement_ByHand_ABattleWithoutFightingChangesNothing_WhateverItsReport` | `IfFought` by hand; an unfought row with end 700 < start 800 and its side the winner → after = before 800,000; the same fought → 850,000; never entered and unfought → unchanged; the refusal |
| `DD1_TieDense_FoughtAndUnfoughtBattles_ManyArmiesAtOneInstant_InArmyIdOrder_WhateverTheSchedulingOrder` | 5 armies × 120 orders: army-id order, values (850,000; 800,000; 250,000; 800,000; 650,000), one state hash; a fought and an unfought battle of one army at one instant in both orders → 650,000 |
| `DD1_SettlementRows_Version3_PopulatedTable_RoundTripBitExact_WithExactLength` | six real rows; 5 + 111 × rows; bit-exact round trip; SHA-256; the world's own table; field-swap guard; v2 refused; Fought 2 refused (decode and encode) |
| `BattleFighting_ReadsTheBattleNotTheFormation_AndRefusesAStranger` | the battle's answer for each formation; a stranger refused |
| `C1_TheInBattlePart_ChangesNothingBeforeTheFirstFightingInstant_TurnExact_BothResolvers` | every completed instant before 7,332,000 identical (morale, entities); at it lower by the charge (900/1000/640 → 850/950/590); AutoResolver logs identical before step 66 and differ only by the charge at it |
| `C1_TheChargeActsOnTheRoutCheckOfItsOwnInstant_TurnExact` | infantry at 320 charged to 270 at the arrow's instant (214,000) and routed at that same instant (the battle ends there; 5 routed escapes); part off: Active at 320 |
| `C1_InABattleWithoutFighting_TheInBattlePartChangesNothing_BothResolvers` | MOR2's unfought battles with the part on: same result hash and trace hash as off, no row (320, 349, 500 tactical; 250, 449 auto) |
| `C1_ALaterArrival_IsChargedAtItsOwnEntry_AfterTheFirstFight_AndAtTheFirstFight_BeforeIt_TurnExact` (4 cases) | landing at 2,000 / 128,000: charged at the landing when the fight began at 0, at 258,000 when it began after the landing; 800 before, 750 at the charge instant |
| `C1_OncePerFormation_WithReEntry_InAFoughtBattle` | four formations each placed ≥ 2 times, charged once (at 0 or at their first landing, 4,000); ends 850/650/950/950 |
| `C1_TheInBattleChargeNoLongerMakesAnUnfoughtWithdrawal_EqualFormationsAt480Fight_InTheAutoResolver` | 480 symmetric, part off and on: first fighting step 131 (262,000), mutual rout, DA6 defender; with the part on charged at 262,000; settlement 430,000 / 230,000 either way |
| `WarSandbox_QuickBattle_WithoutFighting_ChargesNothing_WithFighting_ChargesAtTheFirstFight` | players who never order, out of sight: no fight, no charge; AI vs AI: every formation charged once at the first fight **[MOR3, verification round 1, 2026-11-11, MOR3-R1-04: the test read only EntryCharged = 50 and an instant after the start, which shows neither "once" nor "at the first fight"; it now asserts the AutoBattle's rows (232,000, 1, 1000, 50) and (232,000, 2, 1000, 50), §13]** |

Brief items covered: the unfought mutual rout and withdrawal → 0 in both resolvers with attribution (§4 rows 4–5); the boundary
one missed attack against none, turn-exact (above); a formation that entered a fought battle without striking pays (above); a
fought equal-sides win still rises — lane AP's `DA1_TheEqualSidesWinnerRises_DefenderOrAmbusher` unchanged and passing, and the duel
through the hand-off (`DA1_AMutualEliminationWin_TheRiseIsUnchanged_AndTheWinnerHasNobodyLeft`, unchanged, passing); the in-battle
part changes nothing before the first fighting instant (above); POPULATED-table round trips (above, and the two re-pinned v3 tests);
tie-dense (above).

---

## 4. Moved pins and changed tests (with measured attribution)

The first full Debug run after the code change (tests only adjusted to compile) failed **23 cases in 10 test methods, all lane MOR's or
lane MOR2's morale pins**; no golden or pin of any other lane moved (MEASURED: 1499 passed, 23 failed of 1522; scratchpad
`mor3/step1-failures.txt`). Each is re-pinned on this tree; the values below are MEASURED.

| # | Test (file) | Was → now | Attribution |
|---|---|---|---|
| 1 | `MoraleRows_PopulatedTable_RoundTripBitExact_WithExactLength` (CampaignMoraleTests) | length 5 + 110 × rows → 5 + 111 × rows; refused versions {1, 3} → {1, 2, 4} | the row's `Fought` (codec v3; brief: "bump codec version") |
| 2 | `DB1_SettlementRows_PopulatedTable_RoundTripBitExact_WithExactLength` (EntryCostTests) | 5 + 110 × rows → 5 + 111 × rows | same |
| 3 | `ClampAtZero_AFormationThatEntersWithLessMoraleThanTheCost_IsSettled_ItsDR29StartsFromZero` (EntryCostRound1Tests) | the battle had no fighting (no row at all: "Sequence contains no matching element") → re-pinned on MOR2's three-formation battle with formation 1 at 30: FirstFightMs 7,506,000; row (7,506,000, 1, 30, 30) (was at StartMs); settlement before 30,000, EntryInBattle 30, decrease 0, entry 50,000, rise 200,000, after 180,000 (was 0: its side now wins) | DD1 (unfought → nothing) and C1 (charge at the first fight) |
| 4 | `ProposalA_AnUnfoughtMutualRout_WonUnderDA6_…` → `DD1_AnUnfoughtMutualRout_WonUnderDA6_ChangesNeitherArmysMorale_FromAnyStart` (EntryCostRound2Tests, 8 cases) | defender s × 1,000 + 150,000 → s × 1,000; attacker s × 1,000 − 50,000 → s × 1,000; Fought 0; part-on cases (320, 270 at the start) → (320, inactivity, 420), (349, 299) → (349, inactivity, 449), (500, 550) → (500, 600); EntryCharged 50 → 0; desertion unchanged in form | DD1 (D-085 §3: "both become 0") and C1 (no charge without fighting) |
| 5 | `ProposalA_AnUnfoughtMutualWithdrawal_InTheAutoResolver_…` → `DD1_AnUnfoughtMutualWithdrawal_InTheAutoResolver_WonUnderDA6_ChangesNeitherArmysMorale` (3 cases → 4) | defender +150,000 → 0 (and the attacker's −50,000 → 0, newly asserted); the case (480, part on) removed — under C1 those formations fight (MEASURED: a fought mutual rout, step 131; pinned in NoFightingTests) — and replaced by (250, on), (449, on) | DD1 and C1 |
| 6 | `BothResolvers_EveryFormationOnTheFieldAtTheStart_PaysOnce_AtStartMs` → `…_AtTheFirstFightingInstant` (EntryCostTests, 2 seeds) | rows' AtMs 7,200,000 → 7,332,000 (the first attack: trace and step 66); the archers' Before 600 → 640 (two quiet pulse ends of recovery); pre-run morale (850, 950, 550) → (900, 1000, 600) | C1 |
| 7 | `BothResolvers_TheStateHashCarriesTheEntryRecord_WhenTheResultsAreEqual` (EntryCostTests) | row AtMs 7,200,000 → 7,506,000 | C1 |
| 8 | `BothResolvers_WithTheInBattlePartOn_TheBattleFeelsIt_AFormationEnteringBelowTheWithdrawalLineWithdrawsAtOnce` → `…_FromTheFirstFightingInstant_AFormationChargedBelowTheWithdrawalLineWithdrawsAtItsNextDecision` (EntryCostTests) | rebuilt: the old battle (480 charged at entry, withdraws without a blow) is exactly what D-085 §4 excludes; now one missed arrow at 214,000 charges 480 → 430, withdrawal at 240,000; AutoResolver step 77 (154,000) | C1 |
| 9 | `Tactical_AFormationLandingFromATransport_PaysAtTheInstantItsFirstEntityIsPutAshore_TurnExact` → `…_EntersAtTheInstant…` (EntryCostTests, 4 cases) | the battle has no fighting: rows (enemy at StartMs, riflemen at the landing) → none; riflemen at the landing 750 → 800; the entry instant still pinned turn-exact | C1 (the charge at a landing is pinned on a fought shore in NoFightingTests) |
| 10 | `Tactical_AFormationThatLeavesTheFieldAndIsPlacedOnItAgain_PaysOnce` (EntryCostTests, cost-50 case) | unfought swap: one row per formation → none; EntryCharged 50 → 0; end (850, 650) → (900, 700) | C1 (once-with-re-entry pinned on a fought swap in NoFightingTests) |

Also changed: `EntryMorale_Enter_ChargesOncePerFormation_ByHand` rewritten on the new contract (it no longer compiled; its properties —
once, the clamp at 0, nothing at cost 0 — kept, two added: no charge at an entry before the fighting, none for a never-entered
formation); doc notes, unchanged assertions, on `CountedOnce_…`, `TheCostReachesTheCampaignExactly_…` and the Quick Battle test
(they pass unchanged: their battles are fought).

---

## 5. Lane P's tuning battery (MEASURED, Release, `entry-parity`)

Commands (Release build, 2 threads): `dotnet Proto.M7.Bench.dll entry-parity 50 0 2 entry-50-0.md` and `… entry-parity 50 200 2
entry-50-200.md` (scratchpad `mor3/bench/`). 20 tuning scenarios × seeds 1–80 = 1,600 cases per resolver; "off" = the defaults.

| | tactical | AutoResolver |
|---|---|---|
| off digest | `90E58DCB18DEA369EB1E2464D09DF590482D7473FFB018B550C62FCBF65D6914` = lane P's | `2AC31207D6209E7FE1B4F7AE9119BE70144DB1E1F3988BC6AA609B14A0B68F31` = lane MOR's |
| battles without fighting, off / on (both runs) | 0 / 0 | 0 / 0 |
| report/count disagreements, off / on (both runs) | 0 / 0 | 0 / 0 |
| on, entry 50 alone: digest | `F898894876848E9F3F74277BD743BA47CF3FC05869B5E21ACC53E18D666879AE` (lane MOR2: `413CD6C2…88E29A18`) | `1143704411C538F05BD93546D1B1B4D99AB591900200295512705F43D1054425` (MOR2: `048A8EE5…FA33101E`) |
| entry rows (on) | 3,440 | 3,440 |
| outcomes changed / winners changed | 887 / 128 (MOR2: 649 / 134) | 887 / 83 (MOR2: 598 / 103) |
| on, entry 50 + c = 200: digest | `F87F58E1F470A9BB2067BE088A04B32894651B7A0D8BC30D69076F1408A9D847` (MOR2: `D4784D79…E2E26137`) | `BEECA2934A12338C4D5D12589DB04DC6DE9FF4B8AE6794D1FDC1CA9629B7262D` (MOR2: `0FE5ED33…6DA15389`) |
| outcomes changed / winners changed | 872 / 127 (MOR2: 634 / 133) | 856 / 54 (MOR2: 567 / 74) |

**Rule P1** (n = 80 per tuning scenario): **off 16/20; entry 50 alone 14/20; entry 50 + c = 200 13/20** — the same counts and the
same failing scenarios as lane MOR2's (leadership-edge on duration, ford-crossing on the winner; with c = 200 also sniper-screen on
duration). Wall (reporting only; the host was shared, load average 11–40): tactical 352.1 s and 278.9 s on 2 threads, auto 2.7 s and
3.4 s.

- MEASURED: the defaults are the identity over 1,600 battles in each resolver; ~~every battery battle is fought, so DD1's settlement
  gate changes nothing on the battery, and the whole difference from lane MOR2's "on" numbers is the charge's instant (C1).~~
  **[MOR3, verification round 1, 2026-11-11, MOR3-R1-07: struck as MEASURED. MEASURED: every battery battle is fought (0 without fighting, off and on,
  both resolvers). DERIVED, not measured (`EntryBench.cs` runs no settlement): DD1's settlement gate is the identity on a fought battle,
  so it changes nothing on this battery; and of this lane's resolver changes only the charge's instant (C1) acts on a battle with the
  part on (the fought report sets a report field; the state-hash additions are outside the bench's digest), so it is the whole
  difference from lane MOR2's "on" numbers.]**
- INFERRED (not measured): more outcome fields change under C1 because a charge taken at the entry could be made up by the in-battle
  recovery while a formation held before the fight (proposal §11.3 (i)); one taken at the first fight cannot.
- Lane P's acceptance stays OPEN (D-079 Q4); the held-out acceptance battery was not run; these numbers measure the rule's effect,
  not parity; this lane tunes nothing.

---

## 6. Mutants (bounded; every kill by a named semantic test)

Harness `…/scratchpad/mor3/mut/run.py` (kept): one mutant = exact-string edits, each of which must occur exactly once in its file; a
Debug build of the test project; then the filter `Proto.M7.Tests.Morale | Proto.M7.Tests.LaneD | Proto.M7.Tests.Concealment.EqualSidesTests`
(278 test cases; 279 after the melee-miss test) **bounded at 530 s = 10 × its clean run (52.6 s wall)**; a survivor of the filter
re-runs the full Debug suite **bounded at 2,400 s = 10 × the clean full suite (239 s wall)**; the file is restored byte-for-byte after
every mutant (SHA-256 checked), and `git status` showed no source change afterwards. Specs, results, logs and TRX files:
`…/mor3/mut/spec.json`, `spec2.json`, `results.json`, `run.log`, `run2.log`, `run3.log`, `trx/`. **No mutant hung** (the longest
filter run 89.8 s; the two full runs 325.6 s and 91.9 s).

| Id | Mutation | Verdict | Filter run | Killed by (named semantic tests; failing cases in the filter) |
|---|---|---|---|---|
| R1 | tactical: a melee blow is not reported as fighting | KILLED | 63.5 s | `TheFirstAttackOfEveryDomain_IsTheBattlesFirstFightingInstant_ObservedFromTheTrace (melee)`, `C1_TheInBattlePart_ChangesNothingBeforeTheFirstFightingInstant_TurnExact_BothResolvers`, `DD1_R1a_AFormationThatEnteredAFoughtBattleWithoutStrikingABlow_PaysTheEntryCost_BothResolvers` and 6 more (11 failing cases) |
| R2 | tactical: a missile shot is not reported as fighting | KILLED | 53.0 s | `DD1_TheBoundary_ExactlyOneAttackThatMisses_IsFought_NoAttackIsNot_BothResolvers`, `TheFirstAttackOfEveryDomain_IsTheBattlesFirstFightingInstant_ObservedFromTheTrace (missile)`, `C1_ALaterArrival_IsChargedAtItsOwnEntry_AfterTheFirstFight_AndAtTheFirstFight_BeforeIt_TurnExact` and 7 more (16 failing cases) |
| R3 | tactical: a fired missile shot is fighting only when its line meets an enemy disc (projectile outcome Hit, before the hit roll) | SURVIVED | 69.4 s |  (0 failing cases) |
| R4 | tactical: every bare-point round is fighting (READING MOR3-R1 (b)) | KILLED | 89.8 s | `READING_MOR3_R1_ABombardOfABarePoint_IsFightingOnlyWhereItsBlastReachesAnEnemy`, `READING_MOR3_R1_ABarePointBlastOnAnEnemyHull_IsFighting_OnAFriendlyHull_IsNot` (2 failing cases) |
| R5 | tactical: a bare-point blast reaching an enemy land entity is not fighting | KILLED | 36.4 s | `READING_MOR3_R1_ABombardOfABarePoint_IsFightingOnlyWhereItsBlastReachesAnEnemy` (1 failing cases) |
| R6 | tactical: a bare-point blast reaching an enemy hull is not fighting | KILLED | 29.8 s | `READING_MOR3_R1_ABarePointBlastOnAnEnemyHull_IsFighting_OnAFriendlyHull_IsNot` (1 failing cases) |
| R7 | tactical: a bare-point blast reaching a friendly hull is fighting | KILLED | 27.2 s | `READING_MOR3_R1_ABarePointBlastOnAnEnemyHull_IsFighting_OnAFriendlyHull_IsNot` (1 failing cases) |
| R8 | tactical: a boarding fight is not reported as fighting | KILLED | 25.4 s | `TheFirstAttackOfEveryDomain_IsTheBattlesFirstFightingInstant_ObservedFromTheTrace (boarding)` (1 failing cases) |
| R9 | tactical: a ram is not reported as fighting | KILLED | 24.6 s | `TheFirstAttackOfEveryDomain_IsTheBattlesFirstFightingInstant_ObservedFromTheTrace (ram)` (1 failing cases) |
| R10 | tactical: a bow contact that is not a ram is fighting (READING MOR3-R2 (b)) | KILLED | 28.4 s | `READING_MOR3_R2_ARamCollisionAlone_IsNoFighting` (1 failing cases) |
| R11 | tactical: the LAST attack instant is reported, not the first | KILLED | 21.0 s | `TheFirstAttackOfEveryDomain_IsTheBattlesFirstFightingInstant_ObservedFromTheTrace (aircraft)`, `TheFirstAttackOfEveryDomain_IsTheBattlesFirstFightingInstant_ObservedFromTheTrace (boarding)`, `TheFirstAttackOfEveryDomain_IsTheBattlesFirstFightingInstant_ObservedFromTheTrace (landGuns)` and 9 more (13 failing cases) |
| R12 | tactical: the result does not carry the report | KILLED | 21.4 s | `TheFirstAttackOfEveryDomain_IsTheBattlesFirstFightingInstant_ObservedFromTheTrace (aircraft)`, `TheFirstAttackOfEveryDomain_IsTheBattlesFirstFightingInstant_ObservedFromTheTrace (boarding)`, `TheFirstAttackOfEveryDomain_IsTheBattlesFirstFightingInstant_ObservedFromTheTrace (landGuns)` and 20 more (31 failing cases) |
| R13 | auto: melee in contact is not reported as fighting | KILLED | 22.9 s | `AutoResolver_TheFirstStepWithAnAttack_IsTheBattlesFirstFightingInstant_ObservedFromItsStepLog`, `C1_TheInBattleChargeNoLongerMakesAnUnfoughtWithdrawal_EqualFormationsAt480Fight_InTheAutoResolver`, `C1_TheInBattlePart_ChangesNothingBeforeTheFirstFightingInstant_TurnExact_BothResolvers` and 6 more (11 failing cases) |
| R14 | auto: a volley at range is not reported as fighting | KILLED | 22.7 s | `AutoResolver_TheFirstStepWithAnAttack_IsTheBattlesFirstFightingInstant_ObservedFromItsStepLog`, `DD1_TheBoundary_ExactlyOneAttackThatMisses_IsFought_NoAttackIsNot_BothResolvers`, `TheReportAgreesWithTheAttackCount_AndIsTheFirstAttack_OnLanePsTuningBattery_BothResolvers` and 3 more (6 failing cases) |
| R15 | auto: the reported instant leaves out StartMs | KILLED | 25.2 s | `C1_TheInBattlePart_ChangesNothingBeforeTheFirstFightingInstant_TurnExact_BothResolvers`, `DD1_R1a_AFormationThatEnteredAFoughtBattleWithoutStrikingABlow_PaysTheEntryCost_BothResolvers`, `ClampAtZero_AFormationThatEntersWithLessMoraleThanTheCost_IsSettled_ItsDR29StartsFromZero` (3 failing cases) |
| R16 | auto: the LAST attack step is reported, not the first | KILLED | 20.4 s | `AutoResolver_TheFirstStepWithAnAttack_IsTheBattlesFirstFightingInstant_ObservedFromItsStepLog`, `TheReportAgreesWithTheAttackCount_AndIsTheFirstAttack_OnLanePsTuningBattery_BothResolvers`, `C1_TheInBattleChargeNoLongerMakesAnUnfoughtWithdrawal_EqualFormationsAt480Fight_InTheAutoResolver` and 3 more (6 failing cases) |
| S1 | settlement: the entry term is not gated by fighting | KILLED | 24.7 s | `DD1_AnUnfoughtMutualRout_WonUnderDA6_ChangesNeitherArmysMorale_FromAnyStart`, `DD1_TheBoundary_ExactlyOneAttackThatMisses_IsFought_NoAttackIsNot_BothResolvers`, `DD1_AnUnfoughtDecisiveWin_ChangesNeitherArmysMorale_BothResolvers` and 4 more (17 failing cases) |
| S2 | settlement: the DR29 decrease is not gated by fighting | KILLED | 25.2 s | `DD1_TheSettlement_ByHand_ABattleWithoutFightingChangesNothing_WhateverItsReport` (1 failing cases) |
| S3 | settlement: the DA1 rise is not gated by fighting | KILLED | 26.5 s | `DD1_AnUnfoughtMutualRout_WonUnderDA6_ChangesNeitherArmysMorale_FromAnyStart`, `DD1_AnUnfoughtDecisiveWin_ChangesNeitherArmysMorale_BothResolvers`, `DD1_TheSettlement_ByHand_ABattleWithoutFightingChangesNothing_WhateverItsReport` and 3 more (16 failing cases) |
| S4 | settlement: IfFought ignores fighting | KILLED | 27.0 s | `DD1_TheSettlement_ByHand_ABattleWithoutFightingChangesNothing_WhateverItsReport`, `DD1_AnUnfoughtMutualRout_WonUnderDA6_ChangesNeitherArmysMorale_FromAnyStart`, `DD1_AnUnfoughtDecisiveWin_ChangesNeitherArmysMorale_BothResolvers` and 4 more (17 failing cases) |
| S5 | settlement: fighting read per formation (DD1-R1 (b): it struck or was hurt) | KILLED | 25.4 s | `DD1_R1a_AFormationThatEnteredAFoughtBattleWithoutStrikingABlow_PaysTheEntryCost_BothResolvers`, `BattleFighting_ReadsTheBattleNotTheFormation_AndRefusesAStranger`, `DD1_TheBoundary_ExactlyOneAttackThatMisses_IsFought_NoAttackIsNot_BothResolvers` and 2 more (5 failing cases) |
| S6 | settlement row: Fought always recorded as 1 | KILLED | 25.7 s | `DD1_SettlementRows_Version3_PopulatedTable_RoundTripBitExact_WithExactLength`, `DD1_AnUnfoughtMutualRout_WonUnderDA6_ChangesNeitherArmysMorale_FromAnyStart`, `DD1_AnUnfoughtDecisiveWin_ChangesNeitherArmysMorale_BothResolvers` and 5 more (18 failing cases) |
| S7 | codec: a Fought byte of 2 is accepted on decode | KILLED | 26.9 s | `DD1_SettlementRows_Version3_PopulatedTable_RoundTripBitExact_WithExactLength` (1 failing cases) |
| S8 | settlement: an in-battle charge in a battle without fighting is accepted | KILLED | 29.1 s | `DD1_TheSettlement_ByHand_ABattleWithoutFightingChangesNothing_WhateverItsReport` (1 failing cases) |
| C1 | tactical: charged at the entry (lane MOR2), not at the first fighting instant | KILLED | 26.7 s | `C1_TheInBattlePart_ChangesNothingBeforeTheFirstFightingInstant_TurnExact_BothResolvers`, `C1_InABattleWithoutFighting_TheInBattlePartChangesNothing_BothResolvers`, `C1_ALaterArrival_IsChargedAtItsOwnEntry_AfterTheFirstFight_AndAtTheFirstFight_BeforeIt_TurnExact` and 11 more (21 failing cases) |
| C2 | tactical: the first-fight charge runs before any fighting | KILLED | 31.4 s | `C1_TheInBattlePart_ChangesNothingBeforeTheFirstFightingInstant_TurnExact_BothResolvers`, `C1_ALaterArrival_IsChargedAtItsOwnEntry_AfterTheFirstFight_AndAtTheFirstFight_BeforeIt_TurnExact`, `C1_InABattleWithoutFighting_TheInBattlePartChangesNothing_BothResolvers` and 11 more (21 failing cases) |
| C3 | tactical: no charge at the first fighting instant | KILLED | 32.5 s | `BothResolvers_EveryFormationOnTheFieldAtTheStart_PaysOnce_AtTheFirstFightingInstant`, `C1_TheInBattlePart_ChangesNothingBeforeTheFirstFightingInstant_TurnExact_BothResolvers`, `C1_ALaterArrival_IsChargedAtItsOwnEntry_AfterTheFirstFight_AndAtTheFirstFight_BeforeIt_TurnExact` and 10 more (19 failing cases) |
| C4 | tactical: the charge falls after the instant's rout check | KILLED | 32.2 s | `C1_TheChargeActsOnTheRoutCheckOfItsOwnInstant_TurnExact`, `READING_MOR3_R1_ABombardOfABarePoint_IsFightingOnlyWhereItsBlastReachesAnEnemy` (2 failing cases) |
| C5 | auto: charged at the entry (lane MOR2), not at the first fighting step | KILLED | 27.9 s | `C1_InABattleWithoutFighting_TheInBattlePartChangesNothing_BothResolvers`, `C1_TheInBattleChargeNoLongerMakesAnUnfoughtWithdrawal_EqualFormationsAt480Fight_InTheAutoResolver`, `C1_TheInBattlePart_ChangesNothingBeforeTheFirstFightingInstant_TurnExact_BothResolvers` and 3 more (8 failing cases) |
| C6 | auto: the first-fight charge runs before any fighting | KILLED | 24.8 s | `C1_TheInBattlePart_ChangesNothingBeforeTheFirstFightingInstant_TurnExact_BothResolvers`, `C1_InABattleWithoutFighting_TheInBattlePartChangesNothing_BothResolvers`, `C1_TheInBattleChargeNoLongerMakesAnUnfoughtWithdrawal_EqualFormationsAt480Fight_InTheAutoResolver` and 3 more (8 failing cases) |
| C7 | auto: the charge row leaves out StartMs | KILLED | 26.4 s | `BothResolvers_EveryFormationOnTheFieldAtTheStart_PaysOnce_AtTheFirstFightingInstant` (2 failing cases) |
| C8 | shared: a formation is charged again at every later fighting instant (once broken) | KILLED | 30.9 s | `C1_OncePerFormation_WithReEntry_InAFoughtBattle`, `EntryMorale_Enter_ChargesOncePerFormation_ByHand`, `C1_ALaterArrival_IsChargedAtItsOwnEntry_AfterTheFirstFight_AndAtTheFirstFight_BeforeIt_TurnExact` and 5 more (13 failing cases) |
| C9 | shared: ~~a later arrival after the first fight is never charged~~ **[MOR3, verification round 1, 2026-11-11, MOR3-R1-05: `EntryMorale.Enter` never charges a formation that enters after the first fight (its entry-time path removed). In a TacticalResolver battle the same instant's resolution (`ChargeOnFirstFight`) then charges it at its own landing instant anyway, so C9 is behaviourally equivalent in battles, except possibly the order of two rows charged at one instant: MEASURED, the four `C1_ALaterArrival_…` cases pass under it; the AutoResolver has no later arrival. Only the shared function's contract test pins the entry-time path]** | KILLED | 23.5 s | `EntryMorale_Enter_ChargesOncePerFormation_ByHand` (1 failing cases) |
| C10 | shared: a formation that never entered is charged at the first fighting instant | KILLED | 23.0 s | `EntryMorale_Enter_ChargesOncePerFormation_ByHand`, `C1_ALaterArrival_IsChargedAtItsOwnEntry_AfterTheFirstFight_AndAtTheFirstFight_BeforeIt_TurnExact`, `C1_OncePerFormation_WithReEntry_InAFoughtBattle` (4 failing cases) |
| R3b | tactical: a missile shot is fighting only when its hit roll succeeds (DD1-R2 (b) for missiles) | KILLED | 24.7 s | `C1_TheChargeActsOnTheRoutCheckOfItsOwnInstant_TurnExact`, `DD1_TheBoundary_ExactlyOneAttackThatMisses_IsFought_NoAttackIsNot_BothResolvers`, `TheFirstAttackOfEveryDomain_IsTheBattlesFirstFightingInstant_ObservedFromTheTrace (missile)` and 1 more (4 failing cases) |
| R1b | tactical: a melee blow is fighting only when its hit roll succeeds (DD1-R2 (b) for melee); SURVIVED the filter and the full suite (1552 of 1552, 91.9 s) before the melee-miss test was added | KILLED | 23.3 s | `DD1_R2a_AMeleeBlowThatMisses_IsFighting_AtItsOwnInstant` (1 failing cases) |

**R1b — a gap this lane's own tests had, now closed.** "A melee blow is fighting only when its hit roll succeeds" survived the filter
and the full suite (1552 of 1552) at first: no test pinned the first fighting instant of a battle whose first melee blows all missed. A probe
search found a 1 v 1 meeting whose two first blows both miss (seed 5); `DD1_R2a_AMeleeBlowThatMisses_IsFighting_AtItsOwnInstant`
now pins it, and R1b dies by it. (Its missile counterpart R3b was already killed by the boundary test's missed arrow.)

**R3 — SURVIVED, argued EQUIVALENT with measurement.** The mutation moves the missile's report after the projectile-line check
(`FireOutcome.Hit`), before the hit roll. DERIVED: the TacticalResolver builds each side's enemy discs in the Detection event, after
the Step's movement of the same instant ("positions are fixed until the next Step", CITED `TacticalBattle.cs`), and a fired shot's
segment runs from the shooter to the target's own position, the centre of the target's disc in that list, so `Projectiles.FirstHit`
always meets a disc: a fired shot's line outcome is Hit, and a Blocked shot returns before the attack is counted. MEASURED: a
temporary instrumentation that throws on a `FireOutcome.Miss` of a fired shot — the full Debug suite passed (1553 of 1553) and lane
P's tuning battery ran to the end (`entry-parity 50 0 2`, Release: 3,200 tactical battles, the digests unchanged) with no throw; the
file was restored (SHA-256 checked; log `…/mor3/mut/missprobe.log`). The property R3 attacks — an aimed shot is an attack whether or
not its line meets an entity — is one the system ought to have (DD1-R2 (a)), but this engine cannot produce the case.

**Argued equivalent, NOT RUN:**
- **E1** — counting a bare-point blast that reaches a FRIENDLY land entity. `DangerClose` refuses a bare-point round whose aim point
  lies within the blast radius plus twice the dispersion half-width of a friendly land entity, and the impact lies within √2 × the
  half-width of the aim, so no friendly land entity is ever inside a bare-point blast (DERIVED from `DangerClose` and `DomainFire`;
  the round is chosen and resolved at the same instant, CITED). The hull half has no such guard and is pinned (R7, killed).
- **E2** — removing the AutoResolver's in-contact volley `Fought(k)`. A volley in contact needs `!surprised`, as the melee exchange of
  the same formation in the same step does, and that exchange calls `Fought(k)` first, unconditionally (DERIVED from
  `AutoResolver.cs`): the volley's call is always a no-op.

Teeth and aim (ADR-015 §7.2): every kill above is by a test asserting a property D-085 or the brief states — the first fighting
instant per domain and resolver, the boundary, READINGS MOR3-R1/R2, the three gated settlement terms, the row and its codec, C1's
timing, once-per-formation, the later arrival — not by a golden alone. Not mutated: the state-hash appends of the first fight and the
charge instants (they act only while the in-battle part is on). **[MOR3, verification round 1, 2026-11-11, MOR3-R1-06: the verifier mutated them (V39, V40):
both survive the full suite, re-measured in round 1; §9 item 8, §13.]**

---

## 7. MEASURED and INFERRED

MEASURED (named tests and runs above): every per-domain first fighting instant; the boundary; READINGS MOR3-R1 and MOR3-R2 on real
battles; every settlement value; the unfought cases at 0; DD1-R1 (a)'s formation that struck no blow; C1's turn-exact identity before
the first fight, its same-instant rout, its later arrival and its once-with-re-entry; the identity of an unfought battle with the part
on; the sandbox; the battery digests, counts and rule P1; the 23 moved cases.

DERIVED: the danger-close rule keeps a friendly LAND entity out of every bare-point blast (`DangerClose` bounds the blast radius plus
twice the dispersion half-width), so a friendly-land-blast case does not exist (mutant E1 equivalent, §6); in the AutoResolver an
in-contact volley's `Fought(k)` always follows the same formation's melee `Fought(k)` in the same step (both need `!surprised`), so
removing it is equivalent (E2, §6).

INFERRED: why more battery outcomes change under C1 than with the charge at the entry (§5).

---

## 8. What ran, what was NOT RUN, what was deferred

Host: Linux container, .NET 10 SDK, `nice -n 19`, `-m:1 -nodeReuse:false`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`; other agents shared the
machine (load average 11–40), so wall times are reporting only.

- **Baseline on `51ab032`** (before any change): `dotnet build M7Proto.slnx -c Debug -m:1 -nodeReuse:false` 0 warnings, 0 errors;
  `dotnet test M7Proto.slnx -c Debug --no-build` **1522 passed, 0 failed, 0 skipped** (1 m 34 s); Release: 0 warnings, 0 errors,
  **1522 / 0 / 0** (36 s). (Scratchpad `mor3/base-*.txt`.)
- **First run after the code change** (tests only adjusted to compile): Debug 1499 passed, 23 failed of 1522 — the moved pins of §4.
- **Closing, on the final code** (`495ef9a` plus this report; no code changed after it): `dotnet build M7Proto.slnx -c Debug -m:1
  -nodeReuse:false --no-incremental` 0 warnings, 0 errors (16.2 s); `dotnet test M7Proto.slnx -c Debug --no-build` **1553 passed, 0
  failed, 0 skipped** (1 m 31 s); the same in Release: 0 warnings, 0 errors (12.2 s), **1553 / 0 / 0** (29 s). (`mor3/close-*.txt`.)
- `bash prototypes/m7-sprint1/scripts/check-proto-determinism.sh`: **DETERMINISM SCAN: CLEAN** (109 files, 0 pattern groups with
  findings).
- `python3 scripts/check-doc-links.py --all`: **480 files checked, 0 broken links**; on the proposal and this report by name: 2 files,
  0 broken.
- Lane P's tuning battery, `entry-parity 50 0 2` and `entry-parity 50 200 2` (Release, §5); once more under the R3 instrumentation
  (§6; identical digests).
- The bounded mutant runs (§6) and two temporary probes: the melee-miss seed search (`mor3/Mor3Probe6.cs.txt`, `probe6.txt`; the
  probe file was removed from the test project) and the R3 instrumentation (`mor3/mut/missprobe.py`, restored).

**NOT RUN, and why:**
- `M7Econ.slnx` and `M7DetMath`: untouched by this lane (no economy file changed), so not built or run.
- `scripts/validate.sh` and the `Sim.*` suites: nothing in `Sim.*` changed (D-079 §6, Q5 OPEN); not run.
- Lane P's held-out acceptance battery: not run (D-079 Q4 OPEN; the brief asks for the tuning battery).
- Windows: not run.
- Independent verification of this lane: not run here (a verifier's job).
- The equivalent mutants E1 and E2: not run (argued equivalent, §6).

**Deferred:** the in-battle part's switch-on (with the battery's re-measurement and a re-fit; M7.14 / D-079 Q4); the sandbox's
inspection and preview (PROPOSAL); the siege's DD1 (with the land module, C15); the aggregation over several formations (M7.15);
re-measuring lane MOR2's parity gap in persons under C1 (§9 item 5).

---

## 9. Limitations

1. The in-battle part is 0 by default: by default the battle and the War Sandbox do not feel the entry cost at all.
2. Lane S's siege battles do not apply DD1 (they never reach the settlement): PROPOSAL (a) only.
3. The sandbox's inspection and its settlement preview are PROPOSALS.
4. The hand-off is lane D's stand-in; `SupportBattle.Fought` defaults to true for hand-made battles (M7.15 sets it from the report).
   **[MOR3, verification round 1, 2026-11-11, MOR3-R1-08: the default is FAIL-OPEN: a hand-off that leaves it out settles a battle without fighting as
   fought. Lane D's two hand-offs of real TacticalResolver results left it out and settled right only because their battles had kills;
   they now read it from the report. PROPOSAL for M7.15: a required input, with no default (proposal §12.10 item 4).]**
5. Lane MOR2's measurement of the parity gap in persons with the part on (proposal §11.4: a charge that routs a formation in the
   TacticalResolver but withdraws it in the AutoResolver) was made with the charge at the entry; under C1 it falls at the first fight
   and that gap was not re-measured.
6. The battery was measured on the tuning battery only.
7. READING MOR3-R3 (a): lane W still deserts routed men in a battle without fighting (800 of 2,000 per side in the measured unfought
   mutual rout from 500), and the in-battle recovery still runs before the first fight.
8. The state-hash additions (first fight and charge instants, only while the part is on) were not mutated (§6). **[MOR3, verification round 1, 2026-11-11, MOR3-R1-06:
   they are pinned by no test: V39 and V40 survive (MEASURED in round 1, both before and after the fixes). DERIVED: V40 drops values
   that follow from the hashed charge rows (every AutoResolver formation is charged at the first fighting step); V39 likewise, except
   the first fighting instant while no land formation has entered (every one aboard). No pin added (proposal §12.10 item 8).]**
9. **[MOR3, verification round 1, 2026-11-11, MOR3-R1-03] Where a WALL stands between the sides the two resolvers can disagree about whether the battle had
   fighting (the AutoResolver's contact and line of fire ignore walls), and so about every DD1 term; `ResolverPolicy.Default` sends an
   AI-against-AI battle to the AutoResolver. MEASURED on the verifier's walled setup (§13). Lane P parity item (PROPOSAL); this lane's
   evidence of agreement between the resolvers could not see it.**

---

## 10. For the Director

No new question: D-085 §4 delegates every design item of this lane. Observations, not questions (proposal §12.8):
1. READINGS MOR3-R1 and MOR3-R2 apply DD1-R2 (a) at its two edges (a bombard of empty ground; a bow contact that is not a ram), each
   with its alternative.
2. READING MOR3-R3: DD1 is applied to morale only; lane W's desertion (persons) still happens without fighting (800 of 2,000 men per
   side in the measured unfought mutual rout from 500).
3. PROPOSAL C1 for the in-battle part (D-085 §4's first form); C2 (drop it) is the alternative; the default stays 0.
4. Lane P's battery: C1 changes the in-battle part's effect when it is on; at the defaults both digests are unchanged; lane P's
   acceptance stays OPEN.

---

## 11. Git, files, line counts

Commits on `lane/MOR3` (base `51ab032`), oldest first:
- `c882d6d` the fought report in both resolvers, the gated settlement, the row v3, the in-battle charge at the first fighting
  instant (WIP);
- `1dd1fee` `NoFightingTests`; lane MOR and MOR2's moved pins re-pinned with attribution;
- `8126c72` proposal §12 and the in-place marks; the bench's fought counts; the battery re-measured;
- `8f072a8` the enemy-versus-friendly hull pin and the same-instant rout pin;
- `495ef9a` the melee-miss pin; a proposal wording fix;
- ~~the branch head: this report~~ `f84b941`: this report. **[MOR3, verification round 1, 2026-11-11: two more commits follow, §13.]**

`git diff --stat 51ab032..495ef9a`: 19 files, 1,971 insertions, 178 deletions — `docs/m7-plan/morale-proposal.md` (432 lines
changed), `NoFightingTests.cs` (1,040 lines, new), `EntryCostTests.cs`, `EntryCostRound1Tests.cs`, `EntryCostRound2Tests.cs`,
`CampaignMoraleTests.cs`, `EntryBench.cs`, and in `Proto.M7`: `BattleResult.cs`, `BattleSetup.cs`, `EntryMorale.cs`,
`TacticalBattle.cs`, `TacticalBattle.Domain.cs`, `TacticalBattle.DomainCombat.cs`, `TacticalBattle.Ram.cs`, `AutoResolver.cs`,
`CampaignMorale.cs`, `MoraleCodec.cs`, `SupportTypes.cs`, `SupportWorld.cs`. Line counts: proposal 1,478; this report about 360.

---

## 13. Verification round 1 (2026-11-11)

Fix round 1 on the verifier's eight findings (MOR3-R1-01 to MOR3-R1-08; the verifier's clone was `verify-MOR3-r1`). Clone
`/home/user/m7w/MOR3`, branch `lane/MOR3`, round base `f84b941`. Each finding was REPRODUCED on this clone, by this round's own runs, before
anything changed. None was refuted. The runs: a temporary probe class (kept in the scratchpad as `mor3-fix1/probe/Mor3Fix1Probe.cs.txt`,
removed from the test project by name), and this round's bounded mutant harness (`mor3-fix1/mut/mut.py`, with the specs, logs, TRX files
and results beside it). The fixes are tests, an oracle validation (test code) and one doc comment in `Proto.M7` (`SupportTypes.cs`).
No engine behaviour changed, so no golden, pin, digest or battery number moved: every earlier test passes with its values unchanged
(MEASURED, the closing suites below). The scope is unchanged: nothing in `Sim.*` or `Sim.Data`, no unit data, no cap, no permanent
project, no CI file, M7Econ untouched. Labels as in the header. MEASURED means a run of this round, named.

### 13.1 Verdicts

| Finding | Sev. | Verdict | Evidence (MEASURED on `f84b941` unless labelled) | Change (commit) |
|---|---|---|---|---|
| R1-01 | minor | **REPRODUCED** | V1 SURVIVED the full Debug suite (1553 of 1553), and so did this round's V1b (fighting reported before the layer check rather than the projectile-line check). On the verifier's walled battle, seeds 1 to 3: 540 executed missile `Fire` events, every one blocked (DirectFire: Wall) by the line-of-fire recomputation; Σ AttacksMade 0; no Damage; FirstFightMs null; both armies settle Fought 0 at 800,000; `AttackEvents` listed all 540. Under V1: FirstFightMs 0 and, with the part on, 3 charges. V2 SURVIVED too (1553 of 1553). The verifier could not show it non-equivalent; this round shows it on a battle behind BUILDINGS (riflemen and their target 100 m apart, a spotter beyond the buildings): clean, no fighting; under V2, FirstFightMs 0 and 3 charges with the part on | The trace oracle is validated against the battle's attack count (`AttackEvents`, `UncountedLandAttackEvents`), and a line-of-fire oracle (`RunObservingShots`) is added. Two tests: a wall and buildings in the TacticalResolver; buildings in the AutoResolver, where both resolvers agree. V1, V1b and V2 are now KILLED (`a8ef60a`) |
| R1-02 | minor | **REPRODUCED** | V5a, V5c and V6 SURVIVED (1553 of 1553 each). Non-equivalence: V5a moves the helicopter case's first fighting instant from 0 to 18,000, 12,000, 36,000 and 36,000 (seeds 12, 13, 14, 20). V5c, which the verifier could not show, moves a field gun's case from 0 to 24,000 to 104,000 (seeds 1, 2, 4 to 8: the first round, aimed at a lone rifleman 900 m off, lands 21 to 34 m from him, beyond the 15 m blast). V6 moves the small-crew boarding from 26,000 to 30,000 to 48,000 (seeds 6 to 10) | Three pins: a direct rocket at a land entity that misses; an indirect round whose blast reaches nobody; a boarding fight without casualties. V5a, V5c and V6 are now KILLED (`a8ef60a`) |
| R1-03 | minor | **REPRODUCED** | Walled setup, seeds 1 to 3. TacticalResolver with the knowledge AI on both sides: Standoff at 360,000 ms, 540 `Fire` events, 0 attacks, FirstFightMs null. AutoResolver: FirstFightMs 0 (step 0: formation 1 mode 3 at formation 2, formation 2 mode 1 at formation 1), then SideEliminated at 12,000, 316,000 and 316,000 ms. `ResolverPolicy.Default(setup, Ai, Ai)` = Auto. CITED cause: the AutoResolver's contact is a gap along its battle axis, and its `MissileHitV2` calls `TerrainSight.FirePath` alone, which judges opaque layers only (its own comment: "Slice C DirectFire already judged off-grid / Wall"); the TacticalResolver's `Projectiles.DirectFire` blocks on Wall and on ground in the way. INFERRED, not measured: a crest between the two formations is a second case of the same class | The limitation is stated (proposal §12.10 item 7; §9 item 9 here). A lane P parity item is recorded (PROPOSAL; not this lane's to change: it moves lane P's digests and acceptance, D-079 Q4). A cross-resolver test is added where the resolvers agree (buildings). The wall disagreement is not pinned: it is no property the system ought to have |
| R1-04 | nit | **REPRODUCED** | The test read only EntryCharged = 50 and an instant after the start. The claim itself is true: the sandbox's battle, re-run as an AutoBattle, has the same result and trace hashes and the rows (232,000, 1, 1000, 50) and (232,000, 2, 1000, 50). Mutants R04m (the charge one step late) and R04m2 (a charge at every fighting step: lane MOR3's C8) SURVIVED that test | The test asserts FirstFightMs 232,000 and those rows. R04m and R04m2 are now KILLED by it (`a8ef60a`) |
| R1-05 | nit | **REPRODUCED** | C9, filtered to the four `C1_ALaterArrival_…` cases and the by-hand test: 4 pass; only `EntryMorale_Enter_ChargesOncePerFormation_ByHand` fails. DERIVED: the landing runs in the instant's Step (class Arrival), which schedules the same instant's Resolve; its `ChargeOnFirstFight` charges every entered, uncharged formation once fighting has begun | §6's C9 description is corrected in place; proposal §12.4 has a note |
| R1-06 | nit | **REPRODUCED** | V39 and V40 SURVIVED: 1553 of 1553 on `f84b941`, and 1559 of 1559 after the fixes | No code change; recorded (§9 item 8, proposal §12.10 item 8). DERIVED: V40 drops values that follow from the hashed charge rows. V39 does the same, except for the first fighting instant while no land formation has entered |
| R1-07 | nit | **REPRODUCED** | `Proto.M7.Bench/EntryBench.cs` runs the resolvers and digests `POutcome`; it makes no settlement call (CITED) | §5 relabelled in place (DERIVED), and proposal §12.4 likewise |
| R1-08 | nit | **REPRODUCED** | `SupportBattle.Fought` defaults to true. Lane D's two hand-offs of real TacticalResolver results (`SupportCapabilityTests`, then at lines 418 and 734) left it out (CITED). Mutant R08m (the report's answer is always "no fighting") SURVIVED their 3 cases | Both hand-offs now read it from the report, with a premise and the row's Fought asserted. R08m is now KILLED by them (`a8ef60a`). The default is documented as fail-open; PROPOSAL for M7.15: a required input |

### 13.2 What changed

| File | Change |
|---|---|
| `Proto.M7.Tests/Morale/NoFightingTests.cs` | `AttackEvents` now validates its list on the finished battle (`UncountedLandAttackEvents` = 0: every executed `Fire` event and land `DomainFire` event was a counted attack); `AttackEventsSoFar` gives the unvalidated list while a battle runs. The line-of-fire oracle: `RunObservingShots`, `ObservedShot`, `LineOfFire` (Projectiles.DirectFire, then TerrainSight.FirePath, recomputed at each shot's instant). Four tests that read the oracle during the battle now validate it on the finished battle: the boundary test, two C1 tests and the later arrival. The sandbox test asserts its charge rows. Section 5: `Obstructed`, `ImpactOf`, and five new test methods (six cases) |
| `Proto.M7.Tests/LaneD/SupportCapabilityTests.cs` | Lane D's two hand-offs of real battle reports set `Fought = BattleFighting.SettlesAsFought(res, 1)`; a premise; the row's Fought asserted |
| `Proto.M7/Support/SupportTypes.cs` | Doc comment only: the fail-open default; the M7.15 PROPOSAL |
| `docs/m7-plan/morale-proposal.md` | Marks in §12.2 (the oracle note; four measured cells), §12.4 (the battery statement relabelled; the later-arrival note), §12.5, §12.9 and §12.10 (item 4; items 7 and 8 added) |
| this report | Marks in §0, §3, §5, §6 and §9; this §13 |

`git diff --stat f84b941..a8ef60a`: 3 files, 360 insertions, 13 deletions (the code commit). The docs follow in the report commit.

### 13.3 Tests added or strengthened (MEASURED: all pass in Debug and in Release)

| Test | What it pins (MEASURED values) |
|---|---|
| `DD1_R2a_AShotWithNoLineOfFire_IsNoAttack_TheBattleHasNoFighting_TacticalResolver` (wall; buildings) | 540 missile `Fire` events and no blow. The line-of-fire oracle finds all 540 blocked: Wall on the projectile line, or Structure in the fire path. Σ AttacksMade 0, so 540 uncounted (`AttackEvents` refuses this battle); no Damage; FirstFightMs null; Standoff at 360,000. Both armies Fought 0 at 800,000. Part on: no charge, the same result and trace hashes |
| `DD1_R2a_AVolleyWithNoLineOfFire_IsNoAttack_TheBattleHasNoFighting_AutoResolver_AsInTheTacticalResolver` | At step 0 side 0 knows the infantry, the riflemen have not moved, and the gap is 100,000 mm (out of contact, inside the 120,000 mm range); FirePath between their axis points is blocked by a structure. No attack in the step log; Σ AttacksMade 0; FirstFightMs null; Standoff at 360,000. The TacticalResolver has no fighting either. Fought 0 at 800,000; part on: no charge, the same result hash |
| `DD1_R2a_ADirectDomainShotAtALandEntityThatMisses_IsFighting_AtItsOwnInstant` | Attack helicopter, seed 14: the first rocket at 0 ms is aimed at a land entity of formation 2 (rockets: no blast) and misses; the first hit is at 36,000; FirstFightMs 0 |
| `DD1_R2a_AnIndirectRoundAimedAtALandEntity_IsFighting_WhereverItLands` | Field gun, seed 6: the first round at 0 ms is aimed at the lone rifleman and lands 23,343 mm from him, beyond the 15,000 mm blast (recomputed from the trace's aim point and the counter RNG, as `DomainFire` scatters it); the first hit is at 16,000; FirstFightMs 0 |
| `DD1_R2a_ABoardingFightWithoutCasualties_IsFighting_AtItsOwnInstant` | Crews of 6, seed 10: the first boarding fight at 26,000 leaves 6 and 6 aboard; the first casualty is at 48,000; FirstFightMs 26,000 |
| `WarSandbox_QuickBattle_WithoutFighting_ChargesNothing_WithFighting_ChargesAtTheFirstFight` (strengthened) | AI against AI: FirstFightMs 232,000; the AutoBattle run again has the same hashes and the rows (232,000, 1, 1000, 50) and (232,000, 2, 1000, 50) |
| Lane D's `Morale_ACampaignArmyLeavesABattle…` and `Morale_TheInBattleGainIsNeverCarried_…` (3 cases) | Fought read from the report (the battle had fighting); the settlement row records Fought 1. Values unchanged |

`M7Proto.slnx`: 1553 → **1559** test cases (NoFightingTests 21 → 26 methods, 30 → 36 cases). Every earlier use of the trace oracle passes
its new validation, so in every battle these tests read it on, each executed `Fire` event and land `DomainFire` event was an attack
(MEASURED: lane P's 20 tuning scenarios at seed 1, and every domain case).

### 13.4 Mutants

Each mutant is a set of exact-string edits applied to the source; the file is restored byte for byte afterwards (SHA-256 checked;
`git status` clean after every set). A Debug build follows, then the full Debug suite under the machine lock, or the named filter.
**Bounded at 10 × the clean full Debug suite: 665 s before the fixes (clean run 66.5 s) and 680 s after (68.0 s).** The longest run took
75.5 s. No mutant hung.

| Id | Mutation | Before the fixes (`f84b941`) | After (`a8ef60a`): killed by (semantic tests) |
|---|---|---|---|
| V1 | Tactical: fighting reported before the projectile-line check | SURVIVED (1553/1553, 65.0 s) | KILLED (3 cases, 69.1 s): `DD1_R2a_AShotWithNoLineOfFire_…(wall: True)`, `…(wall: False)`, `DD1_R2a_AVolleyWithNoLineOfFire_…` (its TacticalResolver check) |
| V1b | Tactical: fighting reported before the layer check (the structure) | SURVIVED (69.0 s) | KILLED (2 cases, 66.1 s): `DD1_R2a_AShotWithNoLineOfFire_…(wall: False)`, `DD1_R2a_AVolleyWithNoLineOfFire_…` |
| V2 | Auto: fighting reported before the volley's line-of-fire test | SURVIVED (73.6 s) | KILLED (1 case, 62.5 s): `DD1_R2a_AVolleyWithNoLineOfFire_…` |
| V5a | Tactical: a direct domain shot at a land entity is fighting only when it hits | SURVIVED (61.7 s) | KILLED (63.9 s): `DD1_R2a_ADirectDomainShotAtALandEntityThatMisses_…` |
| V5c | Tactical: an indirect round aimed at a land entity is fighting only when its blast reaches an enemy | SURVIVED (65.9 s) | KILLED (63.2 s): `DD1_R2a_AnIndirectRoundAimedAtALandEntity_…` |
| V6 | Tactical: a boarding fight is fighting only when it causes casualties | SURVIVED (71.6 s) | KILLED (67.3 s): `DD1_R2a_ABoardingFightWithoutCasualties_…` |
| V39 | Tactical: the state hash drops the first-fight and charge instants | SURVIVED (74.8 s) | SURVIVED (1559/1559, 75.5 s): recorded, R1-06 |
| V40 | Auto: the same | SURVIVED (61.7 s) | SURVIVED (62.4 s): recorded; DERIVED equivalent |
| R04m | Auto: the charge falls one step after the first fighting step (sandbox test filter) | SURVIVED | KILLED: `WarSandbox_QuickBattle_…` |
| R04m2 | Shared: a formation is charged again at every fighting instant (lane C8; sandbox test filter) | SURVIVED | KILLED: `WarSandbox_QuickBattle_…` |
| R08m | Settlement: the report's fought answer is always "no fighting" (lane D's two tests filter) | SURVIVED (3/3 pass) | KILLED (3 cases): lane D's `Morale_ACampaignArmyLeavesABattle…`, `Morale_TheInBattleGainIsNeverCarried_…` (both cases) |
| C9 | Shared: `Enter` never charges a later arrival (later-arrival filter plus the by-hand test) | 4 pass; only the by-hand test fails | Unchanged (R1-05: documented, not a gap) |

Teeth and aim (ADR-015 §7.2): each kill above is by a test that asserts a property D-085 or this lane's design states: "a shot with no
line of fire is no attack" in both resolvers; "whether or not it dealt damage" for a direct shot, an indirect round and a boarding
fight; "once, at the first fighting instant" (C1); and "fought, read from the report" at the hand-off. No kill rests on a golden.

### 13.5 What ran, NOT RUN, deferred

Host: as in §8 (`nice -n 19`, `-m:1 -nodeReuse:false`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`). Every full suite ran under the machine lock
(`m7c/fullsuite.lock`, the one the Sim lanes use), one at a time. Logs: `mor3-fix1/base/`, `mor3-fix1/close/`, `mor3-fix1/mut/`.
- **Baseline on `f84b941`:**
  - Debug build: 0 warnings, 0 errors (12.3 s).
  - Debug full suite: **1553 passed, 0 failed, 0 skipped** (66.5 s wall).
  - Release build: 0 warnings, 0 errors (10.1 s).
  - Release full suite: **1553 / 0 / 0** (25.7 s).
- **Probes:** the reproduction probes on the clean tree and under V1, V1b, V2, V5a, V5c and V6 (§13.1).
- **Mutants:** the 12-mutant reproduction run, and the closing set (§13.4).
- **Targeted, after the edits:** `NoFightingTests` and lane D's `Morale_` tests, 46 of 46 pass.
- **Closing, on the final tree** (the code of `a8ef60a`; after it only documents and two comment lines of `NoFightingTests.cs`
  changed, so these runs came last):
  - Debug build `--no-incremental`: 0 warnings, 0 errors (11.0 s).
  - Debug full suite: **1559 passed, 0 failed, 0 skipped** (64.8 s wall).
  - Release build `--no-incremental`: 0 warnings, 0 errors (10.0 s).
  - Release full suite: **1559 / 0 / 0** (26.7 s).
  - The same counts on `a8ef60a` itself: Debug 1559 / 0 / 0 (68.0 s before the mutant runs, and 69.0 s after a rebuild that followed
    them), Release 1559 / 0 / 0 (30.0 s).
- `bash prototypes/m7-sprint1/scripts/check-proto-determinism.sh`: **DETERMINISM SCAN: CLEAN** (109 files, 0 pattern groups with
  findings).
- `python3 scripts/check-doc-links.py --all`: **480 files checked, 0 broken links**.

**NOT RUN, and why:**
- Lane P's tuning battery (`entry-parity`). No resolver output can have changed: `Proto.M7`'s only change is a doc comment (DERIVED).
  The §5 numbers stand as measured in the lane run.
- `M7Econ.slnx` and `M7DetMath`: untouched.
- `Sim.*` suites: untouched (D-079 §6, Q5 OPEN).
- Lane P's held-out battery and Windows: as in §8.
- The verifier's other mutants (V3, V5b, V12, V18, V19, V42, V46): not re-run. They were KILLED in the verifier's run (CITED, its
  `vmor3/mut/results.jsonl`) and are not findings.
- E1 and E2: as in §6.

**Deferred:**
- The lane P parity item: walls in the AutoResolver's contact and line of fire (R1-03; lane P's acceptance OPEN, D-079 Q4).
- The required `Fought` input of M7.15's real hand-off (R1-08; PROPOSAL).
- No pin for the state-hash additions (R1-06; recorded).

**For the Director:** nothing new to decide. The parity item is lane P's design work, and the required input is M7.15's; both are
PROPOSALS.

Commits of this round, oldest first:
- `a8ef60a`: the tests, the oracle validation, lane D's hand-offs and the doc comment.
- the branch head: the proposal and report marks, this section, and two comment lines in `NoFightingTests.cs`.
