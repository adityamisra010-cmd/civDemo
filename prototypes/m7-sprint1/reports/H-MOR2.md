# H-MOR2 — M7 lane MOR2: entering a battle costs morale (D-083 DB1); Annex A rebuilt; lane MOR's round-2 items

Date 2026-11-10. Clone `/home/user/m7w/MOR2`, branch `lane/MOR2`, base `7cacc3a` (the development branch
`claude/new-session-yavghm` as handed to this lane). No clone or checkpoint of lane MOR2 existed at the start: the lane was done from a
fresh clone (`git clone --shared /home/user/civDemo`). ISOLATED PROTOTYPE AND DOCS ONLY: nothing in `Sim.Core`, `Sim.Data`, `Sim.Cli`,
`Sim.Tests` or `Sim.Ui` changed (D-079 Q5 OPEN); no unit data or cap changed; no permanent project; no CI file. Files touched:
`prototypes/m7-sprint1/` (solution `M7Proto.slnx` only) and `docs/m7-plan/morale-proposal.md`.

Status words: **MEASURED** (run in this lane, on this tree; the test, bench or command is named), **DERIVED** (exact arithmetic,
pinned by a test), **INFERRED** (argued, not isolated by a run), **PROPOSAL** (lane MOR2's design under D-083 §4 and D-082 §5's
delegation; decided by nobody), **READING** (an interpretation, both alternatives stated), **DD** (a quoted Director decision).
Research labels: **[CODE]**, **[FETCH]**, **[SEARCH]**, **[KNOW]** (the morale proposal's rebuilt Annex A.0).

Authority: D-083 DB1, DB2, DB1-R1 (a), DB1-R2 (a), DB1-R3 (a) (applied), §3, §4 (delegated: the size and where the cost is charged),
§5; D-082 DA1, DA1-R (a), DA2, DA6, DA7, §5 (every number delegated); D-079 DR19, DR29, DR30, D11, Q4, Q5; `CLAUDE.md` Laws 1–5;
D-069. The design is `docs/m7-plan/morale-proposal.md` §11 (PROPOSAL); this report says what exists and what was measured. Nothing
reserved for the Director is decided here.

---

## 0. Summary

| Brief item | State | Evidence |
|---|---|---|
| (1) Entry cost design (DB1) | **Done**: proposal §11 (PROPOSAL). Entry = placed on the field (the start, or the first landing from a transport) **[r1, MOR2-R1-02: and for a vessel formation the start, for an aircraft formation its first take-off, READING MOR2-R2 (a)]**; once per formation per battle; three separate terms in one clamp; where charged analysed for (i), (ii), (iii) — **(iii) proposed**: one cost felt in battle and carried to the campaign by its own settlement term, DR29's decrease measured from the start less the in-battle charge, so it reaches the campaign exactly and counts once. **Staged default**: settlement term on; in-battle part implemented in both resolvers, 0 by default until the parity battery's re-measurement. Size **50,000 ppm** (50 on the battle scale) with its relations to DA1's rise (a lossless win nets a loss below a seventh), MoraleMax, the rout threshold and lane E's recovery (30 days). Laws 2, 3, 5; the War Sandbox's Quick Battle; superseded statements struck in place | proposal §11; §2 below |
| (2) Prototype and tests | **Done**: `EntryMorale` (one function, both resolvers), `TacticalRules.EntryMoraleCost` (default 0), `FormationResult.EnteredAtMs`/`EntryCharged`, `MoraleRates.EntryCostPpm` (50,000, on), the four-term settlement, `SupportBattle.Entered`/`EntryChargedInBattle`, `MoraleSettlementRow` codec **v2** (110 bytes). Every brief test exists (§3). Lane AP's DA6/DA7 morale test, lane MOR's and lane D's settlement tests updated with the cost, each change attributed (§4). Battery off/on measured (§5) | §3, §4, §5 |
| (2b) Mutual-elimination win (AP-R1-14) | **Done, PROPOSAL (a) applied: no change** — the rise does not depend on what the winner has left; alternative (b) (scale by the share still in good order) stated with its consequences; not implemented (not adopted). Evidence: the real duel through the hand-off (MEASURED, §3) **[r1, MOR2-R1-03: one of (a)'s four arguments was false and is withdrawn; (a) kept on the other three, ~~its low-morale consequence now stated and pinned (a routed DA6 winner nets a rise from a start below 449)~~, and a narrower alternative (b′) added; §13]** **[r2, MOR2-R2-01: that consequence rested on a false premise ("a formation that routed ends below 300"). Corrected and MEASURED in real battles: an equal-sides win with no blow struck (an inactivity rout at any morale, a start below the threshold, or the in-battle charge itself; and the AutoResolver's equal mutual withdrawal) nets the defender's army +150,000 from ANY start; (a) kept on argument (4) alone (DA1 is decided); (b) and (b′) corrected; the targeted alternative (c′) added, not implemented; §14]** | proposal §11.8 |
| (3) Lane MOR's round-2 items | **MOR-V2-04: H1 cannot die — it is an equivalent mutant** (DERIVED: every in-contact volley record is preceded in the same step by the melee record of the same cell and value; MEASURED: H1 and A1 survive the bounded filter and the full suite, 1474 of 1474 each; a temporary instrumentation found the cell already holding k at every one of H1's line's 41 executions in 10,200 AutoResolver battles, and dm > 0 at every one of A1's 147,688). **G3 and L1: each killed by a semantic test** (a zero-damage shell and a zero-damage arrow found by a probe search, `ZeroDamageGuardTests`). **MOR-V2-05: the three cells corrected** with the dossier's claims | §6, §7 |
| (4) Annex A rewrite | **Done**: rebuilt from the dossier (A.0–A.8), lane MOR's labels kept, FETCH added (no claim carries it), ~~every row with its claim id and verdict~~ **[r1, MOR2-R1-10: not yet true: 20 claim ids in 12 groups and two A.7 cells stood without a verdict, and data-18 (CONFIRMED) read as WEAK; corrected, 175 id–verdict pairs, 0 mismatches, 0 unpaired, §13]**; the earlier annex kept and marked, five of its rows struck, every earlier row's fate listed (A.8) | proposal Annex A; §8 |
| (5) This report | Done | — |
| Tests | `M7Proto.slnx`: **1439 → 1474 passed, 0 failed, 0 skipped in Debug and in Release** **[r1: → 1488, §13.4]** **[r2: → 1502, §14.4]** (35 new cases: `EntryCostTests` 29, `ZeroDamageGuardTests` 4, two added rows of lane MOR's DA1 theory; 42 existing cases' expectations updated with the entry cost) | §3, §4, §9 |
| Mutants | **[r1: three of the verifier's survived this round's reproduction too (V02, V09, V10) and V11 died only by goldens; after the fixes, 19 fix-round mutants all KILLED by named semantic tests, §13.6]** **[r2: the verifier's V2-06 and V2-13 survived (reproduced); with the round-2 tests they and 5 more round-2 mutants are KILLED by named semantic tests, §14.6]** 30 lane mutants, each bounded at 10× its clean run: **28 KILLED**, each by named semantic tests (four of them — B2, B5, B7, B8 — first failed to build under warnings-as-errors and were killed in a compile-valid form, B2b, B5b, B7b, B8b); **2 SURVIVED, argued EQUIVALENT with measurement** (H1, A1); no hang | §6 |
| Checks | determinism scan CLEAN (108 files); doc links 0 broken (`--all`) **[r1: both re-run, §13.5, §13.7]** **[r2: both re-run, §14.5]** | §9 |

---

## 1. What exists (files)

| File | Role |
|---|---|
| `Proto.M7/Tactical/EntryMorale.cs` (new) | `EntryCharge` (row) and `EntryMorale.Charge`/`Enter`: the in-battle entry charge, once per formation, clamped at 0 — the one function both resolvers call |
| `Proto.M7/Tactical/BattleSetup.cs` | `TacticalRules.EntryMoraleCost` (battle scale, default 0, validated 0..MoraleMax) |
| `Proto.M7/Tactical/BattleResult.cs` | `FormationResult.EnteredAtMs` (null = never entered) and `EntryCharged` (report fields, not in `ResultHash`) |
| `Proto.M7/Tactical/TacticalBattle.cs`, `TacticalBattle.DomainMove.cs`, `TacticalBattle.Domain.cs` (shared resolver files, lane N's two, each change marked [MOR2]) | land formations on the field at the start enter at StartMs (constructor); a formation aboard enters at its first landing (`TransportOps`); the entry log and accessors; the result fields; the entry record in the state hash only when the cost is above 0; vessels and aircraft reported entered at the start |
| `Proto.M7/Tactical/AutoResolver.cs` (shared, [MOR2]) | every formation enters at StartMs through the same function; the entry log; the result fields; the state hash append only when on |
| `Proto.M7/Morale/CampaignMorale.cs` (lane MOR's, [MOR2]) | `MoraleRates.EntryCostPpm` = 50,000 (PROPOSAL); `CampaignMorale.EntryPpm`, `EntryBattleScale`; `After` with the entry term (one clamp) |
| `Proto.M7/Morale/MoraleCodec.cs` (lane MOR's, [MOR2]) | `MoraleSettlementRow` + `Entered`, `EntryInBattle`, `EntryPpm`; codec version 2 (110 bytes); a version-1 table and an Entered byte above 1 refused |
| `Proto.M7/Support/SupportTypes.cs`, `SupportWorld.cs` (lane D's, [MOR2]) | `SupportBattle.Entered` (default true), `EntryChargedInBattle`; the settlement's entry term; DR29 from the start less the in-battle charge; no rise for an army that never entered; the stand-in's refusals |
| `Proto.M7.Bench/EntryBench.cs` (new), `Program.cs` (one dispatch line) | `entry-parity [cost] [rise] [threads] [out.md]`: lane P's tuning battery off (the defaults) and on, both resolvers, rule P1 per scenario |
| `Proto.M7.Tests/Morale/EntryCostTests.cs` (new) | 17 test methods, 29 cases (§3.1) |
| `Proto.M7.Tests/Morale/ZeroDamageGuardTests.cs` (new) | 2 test methods, 4 cases (§7) |
| `Proto.M7.Tests/Morale/CampaignMoraleTests.cs` (lane MOR's), `LaneD/SupportCapabilityTests.cs` (lane D's), `Concealment/EqualSidesTests.cs` (lane AP's) | expectations updated with the entry cost, each change attributed in place (§4); a `NeverEntered` rig helper |
| `docs/m7-plan/morale-proposal.md` (lane MOR's) | new §11; superseded statements struck with dated notes; §3.3's three cells corrected; Annex A rebuilt, the earlier annex kept and marked |

---

## 2. The design in brief (PROPOSAL; proposal §11 has the reasoning)

**Entry** (DB1-R1 (a), applied). A land formation enters the battle at the first instant one of its entities is on the field: StartMs
for every formation deployed on the ground; for a formation that starts aboard a transport, the instant its first entity is put ashore
(lane N's landing — the prototype's only later arrival; INFERRED from the code: neither resolver has a reinforcement that marches on).
Never on the field: never entered. **[r1, MOR2-R1-02: a vessel formation enters at the start, an aircraft formation at its first
take-off (one that never leaves its base never enters) — READING MOR2-R2 (a), with (b) stated, proposal §11.2.]** **Once** per formation per battle (DB1-R2 (a)): the first placement records the instant; a
formation that re-embarks and lands again pays nothing more.

**The settlement** (DB1-R3 (a), separate terms; one clamp):

    entry    = entered ? 50,000 : 0
    decrease = floor(max(0, (start − entryInBattle) − end − restore) × 1,000,000 ÷ MoraleMax)        DR29 (lane D)
    rise     = won ∧ entered ? floor(400,000 × B ÷ (B + W)) : 0                                     DA1 (lane MOR)
    after    = clamp(before − entry − decrease + rise, 0, 1,000,000)

**In battle** (the in-battle part, 0 by default): at its entry instant a land formation's morale falls by min(cost, morale); PROPOSAL
cost 50 = `EntryBattleScale(50,000, 1,000)`. Because the settlement measures DR29 from start − entryInBattle and adds its own entry
term, the campaign pays exactly 50,000 whatever the in-battle recovery did, and nothing is counted twice (DERIVED; ~~MEASURED in real
battles, §3.1~~ **[r1, MOR2-R1-12: the real battles of §3.1 measured only the first half (the cost reaches the campaign); the
counted-once half was pinned by one hand-made stand-in row until round 1, and is now MEASURED in real battles with losses in both
resolvers, §13.4]**).

**Where charged** (D-083 §4): (iii) proposed — the battle feels it and the settlement carries it. **Prototype default staged**: the
settlement term on (DB1 is decided, and its campaign part is the level the Director's question was asked at); the in-battle part 0
until the parity battery's next re-measurement, together with lane MOR's "fight it won" (c = 200), so the battery moves once
(MEASURED: with it off, every golden and lane P's battery digest are unchanged; with it on, rule P1 moves, §5).

**Readings applied, ~~none introduced~~.** DB1-R1 (a), DB1-R2 (a), DB1-R3 (a) as D-083 states them. ~~One consequence DERIVED, not a new
reading: an army whose formation never reached the field "did not take part", so DA1-R (a)'s rise for "the winning side's formations
that took part" does not reach it either.~~ **[r1, MOR2-R1-04, MOR2-R1-02: two readings were introduced and are now labelled with
both alternatives (proposal §11.2): READING MOR2-R1, what "took part" in DA1-R (a) means — (a) APPLIED, entered the field; (b),
entered or engaged without reaching it (troops killed aboard their transport) — and READING MOR2-R2, when a carried or based
formation is placed on the field — (a) APPLIED, when one of its entities is on the field by the engine's definition (put ashore;
taken off); (b) at the start for every formation the battle carries or bases. And a formation that never entered still takes
DR29's decrease of whatever the battle cost it (MOR2-R1-01).]**

**The mutual-elimination win** (2b): PROPOSAL (a) applied — no change (proposal §11.8); (b) stated. **[r2, MOR2-R2-01: still (a), now on
argument (4) alone, with its corrected consequence (+150,000 for an equal-sides win with no blow struck, from any start, both
resolvers) and the alternatives (b′) and (c′); §14.]**

**The War Sandbox** (proposal §11.7): Quick Battle charges the in-battle part through the campaign's resolvers and rules (DR30): nothing
today, every formation at its entry instant once on; no settlement (no campaign stock, D15); a preview of the settlement in the
sandbox's inspection is PROPOSED, not implemented.

---

## 3. Tests (MEASURED: all pass in Debug and in Release)

### 3.1 `Proto.M7.Tests/Morale/EntryCostTests.cs` (17 methods, 29 cases)

| Test | What it pins |
|---|---|
| `Equations_ByHand_TheEntryTermTheOneClampAndTheBattleScaleImage` | every equation with literal numbers: 950,000 / 750,000 / 650,000; **the clamp at 0** (30,000 → 0, not −20,000); one clamp (30,000 − 50,000 + 200,000 = 180,000, not 200,000); the top; entry 0 = lane MOR's rule; the battle-scale image 50 / 50 / 37 / 0; the in-battle charge 50 / 30 / 0 / 0; the refusals |
| `EntryMorale_Enter_ChargesOncePerFormation_ByHand` | **once**: a second call is a no-op; cost 0 records the instant only (no row); the clamp at 0 |
| `DB1_ALosslessWin_NetsTheRiseLessTheCost_Exactly` (6) | **a lossless win nets rise − cost exactly**: +150,000; +83,333; **0 at W = 7B**; −7 at W = 7B + 1; +6 at W = 7B − 1; −30,953 at a twentieth |
| `DB1_ALosslessLoss_CostsExactlyTheCost` | **a lossless loss costs exactly the cost** (750,000), as does a lossless battle without a winner; the winner 950,000 |
| `DB1_TheCostReachesTheCampaignExactly_WhenTheInBattleRecoveryMakesUpTheStartLoss` (4) | **real tactical battles**, in-battle part off and on (50): lossless archers end at or above 800 (the recovery made up the start loss), DR29 carries 0, and the campaign pays exactly 50,000 (800,000 − 50,000 + rise); lane D's net loss alone would have lost it |
| `DB1_AFormationThatNeverReachedTheField_PaysNothing_AndTakesNoRise` | **a formation that never reaches the field pays nothing**: no entry, no decrease, no rise though its side won; the stand-in's refusals of ~~a loss, a casualty or~~ a charge for it **[r1, MOR2-R1-01: a never-entered formation with casualties and a loss is now accepted and settled — DR29's decrease, no entry, no rise (READING MOR2-R1 (a)) — beside the same battle entered (§13.4)]** |
| `DB1_TheClampAtZero_OneClampOverTheThreeTerms` | **the clamp at 0** in the world: a ruined army (30,000) loses a lossless battle → 0; another wins one → 180,000 |
| `DB1_TieDense_ManyArmiesSettleAtOneInstant_InArmyIdOrder_WhateverTheSchedulingOrder` | **TIE-DENSE**: five armies (entered winner, entered loser, never-entered winner and loser, an in-battle charge) at one instant, **all 120 scheduling orders**: army-id order, the same results by hand, the same state hash |
| `DB1_SettlementRows_PopulatedTable_RoundTripBitExact_WithExactLength` | **POPULATED TABLE** (v2): 6 real rows (entered and not, a charge, a rise, a clamp at 0): ExpectedLength 5 + 110 × 6, bit-exact round trip, the same bytes and SHA-256, equal to the world's table, every same-typed field pair told apart |
| `BothResolvers_EveryFormationOnTheFieldAtTheStart_PaysOnce_AtStartMs` (2) | **both resolvers, one function**: three formations, StartMs 7,200,000: one row each at StartMs (900 → 850, 1000 → 950, 600 → 550) before the battle runs, the same rows in both resolvers, none added over the battle; the report's EnteredAtMs and EntryCharged |
| `BothResolvers_TheInBattleDefault_IsTheIdentity` | the default 0: ~~result, state and trace hashes equal to a setup that never names it~~ **[r1, MOR2-R1-07: that comparison could not fail (the "unnamed" setup was the same rule record); the test now compares four battles' hashes with those recorded on the base commit 7cacc3a, §13.4]**, no rows, EnteredAtMs still reported |
| `BothResolvers_WithTheInBattlePartOn_TheBattleFeelsIt_…` | **the battle feels it** (on): a formation at 480 enters at 430 and withdraws at its first decision in both resolvers (Retreat at pulse 0; withdrawn untouched, in good order); off, it stays and fights |
| `BothResolvers_TheStateHashCarriesTheEntryRecord_WhenTheResultsAreEqual` | starts of 30 and 40 both charged to 0 (the clamp): equal results and entities, different state hashes, both resolvers |
| `Tactical_AFormationLandingFromATransport_PaysAtTheInstantItsFirstEntityIsPutAshore_TurnExact` (4) | **TURN-EXACT arrival**: riflemen aboard are ordered ashore at pulse 0 step 1 or 3, pulse 2 step 0 or 4; observed from outside the rule (entity states at each completed instant): their one row stands at exactly the first instant one of them is ashore (StartMs + pulse × PulseMs + k × StepMs) — not at StartMs, not one step later; morale 800 at the completed instant before, 750 at that instant; the enemy paid at StartMs; the transport pays nothing; the AutoResolver refuses the battle |
| `Tactical_AFormationThatLeavesTheFieldAndIsPlacedOnItAgain_PaysOnce` **[r1: (2), also at cost 0, MOR2-R1-06]** | **once, with re-entry**: lane N's swap (two formations a side taking turns aboard every pulse): each of the four left the field and came back (observed, at least twice), each has exactly one row at its first placement, morale charged once (850, 650 at the end, no casualties) |
| `Tactical_AFormationThatNeverLeavesItsTransport_NeverEntersAndPaysNothing` | **never on the field**: no row, EnteredAtMs null, EntryCharged 0, end morale = start; its army's settlement (through the hand-off) changes nothing; the vessel reported entered at the start |
| `DA1_AMutualEliminationWin_TheRiseIsUnchanged_AndTheWinnerHasNobodyLeft` | 2b evidence: lane AP's real duel through lane W's collapse: the defender's army 150,000 (rise 200,000, decrease 800,000, entry 50,000), the attacker's 0; the defender's army has 0 men |

### 3.2 `Proto.M7.Tests/Morale/ZeroDamageGuardTests.cs` (2 methods, 4 cases) — §7

---

## 4. Moved pins and changed tests (with measured attribution)

**No golden hash moved** (MEASURED: every golden test of both resolvers, lane N, lane TR, lane P's subsets, lane E, lane S, lane W, lane
AP and lane MOR passes unchanged; the in-battle part is 0 by default and the settlement term reaches no resolver).

**42 existing test cases failed when the settlement term was switched on** (MEASURED on commit `ef3f59c`: 1397 of 1439 passed; the 42
failures were all campaign-morale settlement assertions) and were updated in commit `60d1bb1`, each expectation ~~**less exactly 50,000**~~
**[r1, MOR2-R1-05: 37 of the 42 less exactly 50,000; the other 5 by other amounts, below]** (the entry cost of an army whose formation
entered), attributed in place with a `[MOR2, D-083 DB1]` note:

| File (owner) | Tests (cases) | Change |
|---|---|---|
| `Morale/CampaignMoraleTests.cs` (lane MOR) | `DA1_TheWinnersArmyRises_TheLosersFalls_ByHand` (5 → 7), `DA1_EqualSides_…`, `DA1_AnAmbushBetweenEqualSides_…`, `DA1_NoWinnerReported_…`, `DA1_TheHandOffRefusesABadWinnerOrStrength`, `DA1_ALoserThatHeldTheField_…`, `DA1_WithoutASideStrength_…`, `DA1_TheRiseAppliesAtTheBattlesEndInstant_NotBefore`, `DA1_ABattleAtThePulseEndInstant_…` (6), `Baseline_AnExcessDecaysTowardIt_…`, `Baseline_TheDecayFollowsThePulseLength` (2), `Baseline_AboveIt_TheHungerPenaltyStillApplies_ByHand`, `Baseline_TheDecayAndTheRecoveryEachReadTheirOwnRate_ByHand`, `ManyArmiesBattlesAtOneInstant_…`, `TwoBattlesOfOneArmyAtOneInstant_…` (2), `MoraleRows_PopulatedTable_…`, `TheStateHash_CarriesTheMoraleTable` (30 cases) | −50,000 per settlement (the pulse-end pins re-derived by hand: 938,334 / 950,000 / 961,666 ×2 / 894,999 ×2); **two inputs changed**: `Baseline_BelowIt_TheRecoveryStopsAtTheBaseline` (2) now reaches 750,000 through the entry cost of a LOSSLESS battle, so the recovery sequence it pins is unchanged, and the DA1 theory gains two rows (14,000 and 30,000 beaten) that keep its top-of-scale cases; the populated table is now codec v2 (110 bytes) with a never-entered row and an in-battle charge |
| `LaneD/SupportCapabilityTests.cs` (lane D) | `Morale_ABattleLowersCampaignMoraleByItsNetLossOnly` (6 cases, 5 changed), `Morale_TheInBattleGainIsNeverCarried_…` (2), `Morale_MedicalRestore_ByHand_TenDeadOfFifty`, `Morale_ACampaignArmyLeavesABattleAtTheTacticalEndMorale_MedicsRestorePart` (9 cases) | −50,000 (640,000 → 590,000; 800,000 → 750,000 …); the floor at 0 unchanged |
| `Concealment/EqualSidesTests.cs` (lane AP) | `DA1_TheEqualSidesWinnerRises_DefenderOrAmbusher` (3) | **lane AP's equal-sides and ambush morale test**: 900,000 / 700,000 → 850,000 / 650,000; asserts both duel formations EnteredAtMs = StartMs and the 50,000 entry term |

~~Every one of these 42 cases had failed by exactly 50,000 before its update (MEASURED by the failing run's assertion values for the
cases listed; the two input changes and the two added rows are explained in the tests' notes).~~ **[r1, MOR2-R1-05, MEASURED again on
`ef3f59c` (1397 passed, 42 failed, as before), every first failing assertion parsed: 37 cases differ by exactly 50,000 in every
value that differs; 5 differ by another amount, each explained in its test note — `DA1_ABattleAtThePulseEndInstant_…` at baseline
1,000, two cases: 38,334 (1,000,000 against 961,666: the top clamp no longer binds); `DA1_TheWinnersArmyRises_TheLosersFalls_ByHand`
with 12,000 beaten: 7,143 (1,000,000 against 992,857: the same); `TwoBattlesOfOneArmyAtOneInstant_…` (winFirst false): 100,000 (two
entry costs); `MoraleRows_PopulatedTable_…`: length 470 against 555 (codec v2). Commit `60d1bb1`'s message ("Each changed
expectation is the earlier value less exactly 50,000") overstates in the same way; the commit is history and is not rewritten.]**

---

## 5. The in-battle part on lane P's tuning battery (MEASURED, Release, `entry-parity`)

Commands (Release build, 2 threads): `dotnet Proto.M7.Bench.dll entry-parity 50 0 2 entry-50-0.md` and `… entry-parity 50 200 2
entry-50-200.md` (scratchpad `mor2/bench/`). 20 tuning scenarios × seeds 1–80 = 1,600 cases per resolver; "off" = the defaults
(entry 0, local win 0).

| | tactical | AutoResolver |
|---|---|---|
| off digest (lane P's ParityPCsv.Digest) | `90E58DCB18DEA369EB1E2464D09DF590482D7473FFB018B550C62FCBF65D6914` = lane P's (H-P §7) | `2AC31207D6209E7FE1B4F7AE9119BE70144DB1E1F3988BC6AA609B14A0B68F31` = lane MOR's off (H-MOR §5.4) |
| on, entry 50 alone: digest | `413CD6C2ECD76DC716187FC2B0E790DAFEA0DA7AFCFD94A5B9900C7A88E29A18` | `048A8EE59DBFAF60A653FD0680284CB5A5B04960488282E949B13709FA33101E` |
| entry rows (on) | 3,440 | 3,440 |
| outcomes changed / winners changed | 649 / 134 | 598 / 103 |
| on, entry 50 + local win 200: digest | `D4784D7988FF67BCE3C0FF3074B03399CEC175DBC9443A94DBF173FAE2E26137` | `0FE5ED33C521B1FAE8AB9A9B106169CE9C292F70A75602D61F51EA556DA15389` |
| outcomes changed / winners changed | 634 / 133 | 567 / 74 |

**Rule P1** (tactical reference against the auto candidate, n = 80 per tuning scenario): **off 16/20; entry 50 alone 14/20**
(leadership-edge newly fails on duration, ford-crossing on the winner distribution); **entry 50 + c = 200: 13/20** (leadership-edge and
sniper-screen on duration, ford-crossing on the winner). Wall times (reporting only): tactical 145.6 s and 143.5 s on 2 threads, auto
2.7 s and 2.6 s.

- **Identity (MEASURED):** with the in-battle part off, both resolvers' digests equal the recorded ones: the default changes no battle.
- **Effect (MEASURED):** on, every land formation pays once (3,440 rows = the battery's land formations), about 40 % of the battles
  change in some outcome field and 6–8 % change winner; the parity check moves by two or three scenarios.
- **INFERRED:** the AutoResolver's model-2 coefficients were fitted with no entry charge ("re-fit, never hand-tune" whenever a tactical
  placeholder moves, lane P); the charge lowers every start morale, so the drop in rule P1 is what a re-fit would have to absorb.
  Switching the in-battle part on therefore belongs with the battery's re-measurement and a re-fit; this lane tunes nothing and claims
  no parity. Lane P's acceptance stays OPEN (D-079 Q4); the held-out acceptance battery was not run.

---

## 6. Mutants (bounded; every kill by a named semantic test)

Harness `…/scratchpad/mor2/mut/run.py` (kept): one exact-string replacement per mutant (it must occur exactly once), a Debug build
of the test project, then the filter `Proto.M7.Tests.Morale | Proto.M7.Tests.LaneD | Concealment.EqualSidesTests |
MultiDomain.TransportTests` (237 test cases) **bounded at 100 s = 10× its clean run (9.5 s)**; a survivor of the filter re-runs the
full suite **bounded at 450 s = 10× the clean Debug suite (45 s)**; the file is restored from its saved content after every mutant
and the restored tree rebuilt (afterwards `git status` showed no source change). Specs and results: `…/mor2/mut/spec.json`,
`spec2.json`, `results.json`, `results2.json`. **No mutant hung** (the longest filter run 11.3 s, the longest full run 48.5 s).

| Id | File | Mutation | Verdict | Filter run | Killed by (semantic tests; failing test cases in the filter) |
|---|---|---|---|---|---|
| E1 | `SupportWorld.cs` | settlement: the entry term dropped (lane MOR's two-term clamp) | KILLED | 8.8 s | `DA1_TheEqualSidesWinnerRises_DefenderOrAmbusher`, `Morale_ABattleLowersCampaignMoraleByItsNetLossOnly`, `Morale_ACampaignArmyLeavesABattleAtTheTacticalEndMorale_MedicsRestorePart` and 25 more (57 failing) |
| E2 | `SupportWorld.cs` | settlement: a never-entered army pays the entry cost | KILLED | 9.4 s | `MoraleRows_PopulatedTable_RoundTripBitExact_WithExactLength`, `DB1_AFormationThatNeverReachedTheField_PaysNothing_AndTakesNoRise`, `DB1_TieDense_ManyArmiesSettleAtOneInstant_InArmyIdOrder_WhateverTheSchedulingOrder` and 1 more (4 failing) |
| E3 | `SupportWorld.cs` | settlement: DR29 measured from the start, ignoring the in-battle charge (counted twice) | KILLED | 10.3 s | `DB1_TieDense_ManyArmiesSettleAtOneInstant_InArmyIdOrder_WhateverTheSchedulingOrder` (1 failing) |
| E4 | `SupportWorld.cs` | settlement: a never-entered army of the winning side rises | KILLED | 9.4 s | `DB1_AFormationThatNeverReachedTheField_PaysNothing_AndTakesNoRise`, `DB1_TieDense_ManyArmiesSettleAtOneInstant_InArmyIdOrder_WhateverTheSchedulingOrder` (2 failing) |
| E5 | `CampaignMorale.cs` | settlement: the entry term clamped at 0 before the other terms (two clamps) | KILLED | 8.9 s | `DB1_TheClampAtZero_OneClampOverTheThreeTerms`, `Equations_ByHand_TheEntryTermTheOneClampAndTheBattleScaleImage` (2 failing) |
| E6 | `SupportWorld.cs` | settlement row: the entry term recorded as 0 | KILLED | 9.8 s | `DA1_TheEqualSidesWinnerRises_DefenderOrAmbusher`, `DA1_TheHandOffRefusesABadWinnerOrStrength`, `DA1_TheWinnersArmyRises_TheLosersFalls_ByHand` and 6 more (25 failing) |
| E7 | `SupportWorld.cs` | stand-in: the never-entered consistency refusal removed | KILLED | 9.5 s | `DB1_AFormationThatNeverReachedTheField_PaysNothing_AndTakesNoRise` (1 failing) |
| E8 | `SupportWorld.cs` | stand-in: the in-battle charge range refusal removed | KILLED | 9.0 s | `DB1_AFormationThatNeverReachedTheField_PaysNothing_AndTakesNoRise` (1 failing) |
| E9 | `CampaignMorale.cs` | settlement: the entry cost taken as the battle-scale image (50 instead of 50,000) | KILLED | 9.9 s | `DA1_TheEqualSidesWinnerRises_DefenderOrAmbusher`, `Morale_ABattleLowersCampaignMoraleByItsNetLossOnly`, `Morale_ACampaignArmyLeavesABattleAtTheTacticalEndMorale_MedicsRestorePart` and 26 more (58 failing) |
| C1 | `MoraleCodec.cs` | codec: version not bumped (1) | KILLED | 8.7 s | `MoraleRows_PopulatedTable_RoundTripBitExact_WithExactLength` (1 failing) |
| C2 | `MoraleCodec.cs` | codec: EntryInBattle and EntryPpm swapped in the encoder | KILLED | 9.2 s | `MoraleRows_PopulatedTable_RoundTripBitExact_WithExactLength`, `DB1_SettlementRows_PopulatedTable_RoundTripBitExact_WithExactLength` (2 failing) |
| C3 | `MoraleCodec.cs` | codec: the Entered byte not validated on decode | KILLED | 10.0 s | `MoraleRows_PopulatedTable_RoundTripBitExact_WithExactLength` (1 failing) |
| B1 | `EntryMorale.cs` | in battle: the once-per-formation guard dropped | KILLED | 9.0 s | `EntryMorale_Enter_ChargesOncePerFormation_ByHand`, `Tactical_AFormationLandingFromATransport_PaysAtTheInstantItsFirstEntityIsPutAshore_TurnExact`, `Tactical_AFormationThatLeavesTheFieldAndIsPlacedOnItAgain_PaysOnce` (6 failing) |
| B2 | `TacticalBattle.cs` | tactical: a formation aboard a transport enters at the start (placement ignored) | BUILD-FAILED | — | did not compile (CS0219, warnings as errors): re-expressed as B2b |
| B3 | `TacticalBattle.DomainMove.cs` | tactical: a landing's entry recorded one step late | KILLED | 9.6 s | `Tactical_AFormationLandingFromATransport_PaysAtTheInstantItsFirstEntityIsPutAshore_TurnExact`, `Tactical_AFormationThatLeavesTheFieldAndIsPlacedOnItAgain_PaysOnce` (5 failing) |
| B4 | `TacticalBattle.DomainMove.cs` | tactical: a landing never enters | KILLED | 9.7 s | `Tactical_AFormationLandingFromATransport_PaysAtTheInstantItsFirstEntityIsPutAshore_TurnExact`, `Tactical_AFormationThatLeavesTheFieldAndIsPlacedOnItAgain_PaysOnce` (5 failing) |
| B5 | `AutoResolver.cs` | auto: no entry at all | BUILD-FAILED | — | did not compile (CS0649, warnings as errors): re-expressed as B5b |
| B6 | `EntryMorale.cs` | in battle: the charge not clamped at the formation's morale | KILLED | 8.7 s | `BothResolvers_TheStateHashCarriesTheEntryRecord_WhenTheResultsAreEqual`, `EntryMorale_Enter_ChargesOncePerFormation_ByHand`, `Equations_ByHand_TheEntryTermTheOneClampAndTheBattleScaleImage` (3 failing) |
| B7 | `TacticalBattle.cs` | tactical: the entry record left out of the state hash | BUILD-FAILED | — | did not compile (CS0162, warnings as errors): re-expressed as B7b |
| B8 | `AutoResolver.cs` | auto: the entry record left out of the state hash | BUILD-FAILED | — | did not compile (CS0162, warnings as errors): re-expressed as B8b |
| B9 | `TacticalBattle.cs` | tactical: EnteredAtMs not reported | KILLED | 8.7 s | `DA1_TheEqualSidesWinnerRises_DefenderOrAmbusher`, `BothResolvers_EveryFormationOnTheFieldAtTheStart_PaysOnce_AtStartMs`, `BothResolvers_TheInBattleDefault_IsTheIdentity` and 4 more (13 failing) |
| B10 | `TacticalBattle.cs` | tactical: EntryCharged not reported | KILLED | 8.9 s | `BothResolvers_EveryFormationOnTheFieldAtTheStart_PaysOnce_AtStartMs`, `DB1_TheCostReachesTheCampaignExactly_WhenTheInBattleRecoveryMakesUpTheStartLoss`, `Tactical_AFormationThatLeavesTheFieldAndIsPlacedOnItAgain_PaysOnce` (5 failing) |
| B11 | `TacticalBattle.Domain.cs` | tactical: vessels and aircraft not reported as entered | KILLED | 8.6 s | `Tactical_AFormationThatNeverLeavesItsTransport_NeverEntersAndPaysNothing` (1 failing) |
| B12 | `AutoResolver.cs` | auto: EnteredAtMs not reported | KILLED | 9.3 s | `BothResolvers_EveryFormationOnTheFieldAtTheStart_PaysOnce_AtStartMs`, `BothResolvers_TheInBattleDefault_IsTheIdentity` (3 failing) |
| B13 | `BattleSetup.cs` | rules: the EntryMoraleCost range check removed | KILLED | 9.7 s | `Equations_ByHand_TheEntryTermTheOneClampAndTheBattleScaleImage` (1 failing) |
| B14 | `TacticalBattle.cs` | tactical: start formations enter at StartMs + StepMs | KILLED | 9.2 s | `DA1_TheEqualSidesWinnerRises_DefenderOrAmbusher`, `BothResolvers_EveryFormationOnTheFieldAtTheStart_PaysOnce_AtStartMs`, `BothResolvers_TheInBattleDefault_IsTheIdentity` and 5 more (17 failing) |
| L1 | `TacticalBattle.cs` | tactical Damage handler: the zero-damage guard dropped (lane MOR's L1) | KILLED | 10.6 s | `Tactical_AZeroDamageArrow_IsNoBlow_TheArcherDoesNotRise` (2 failing) |
| G3 | `TacticalBattle.DomainCombat.cs` | tactical crew-served hit: the zero-damage guard dropped (lane MOR's G3) | KILLED | 11.3 s | `Tactical_AZeroDamageShell_IsNoBlow_TheGunsDoNotRise` (2 failing) |
| H1 | `AutoResolver.cs` | auto: the in-contact ranged volley not recorded (the verifier's H1) | SURVIVED | 10.5 s; full suite 1474/1474 pass, 48.5 s | EQUIVALENT (§7): DERIVED, and MEASURED by the instrumentation |
| A1 | `AutoResolver.cs` | auto: the melee record's zero-damage guard dropped | SURVIVED | 8.8 s; full suite 1474/1474 pass, 43.3 s | EQUIVALENT (§7): DERIVED, and MEASURED by the instrumentation |
| B2b | `TacticalBattle.cs` | tactical: a formation aboard a transport enters at the start (placement ignored) (compile-valid form of B2) | KILLED | 9.9 s | `Tactical_AFormationLandingFromATransport_PaysAtTheInstantItsFirstEntityIsPutAshore_TurnExact`, `Tactical_AFormationThatLeavesTheFieldAndIsPlacedOnItAgain_PaysOnce`, `Tactical_AFormationThatNeverLeavesItsTransport_NeverEntersAndPaysNothing` (6 failing) |
| B5b | `AutoResolver.cs` | auto: no entry at all (compile-valid form of B5) | KILLED | 9.4 s | `BothResolvers_EveryFormationOnTheFieldAtTheStart_PaysOnce_AtStartMs`, `BothResolvers_TheInBattleDefault_IsTheIdentity`, `BothResolvers_TheStateHashCarriesTheEntryRecord_WhenTheResultsAreEqual` and 1 more (5 failing) |
| B7b | `TacticalBattle.cs` | tactical: the entry record left out of the state hash (compile-valid form of B7) | KILLED | 9.5 s | `BothResolvers_TheStateHashCarriesTheEntryRecord_WhenTheResultsAreEqual` (1 failing) |
| B8b | `AutoResolver.cs` | auto: the entry record left out of the state hash (compile-valid form of B8) | KILLED | 9.7 s | `BothResolvers_TheStateHashCarriesTheEntryRecord_WhenTheResultsAreEqual` (1 failing) |

- **[r1, MOR2-R1-12: E3 was killed by one hand-made stand-in row only; in round 1 it is also killed by real battles with losses,
  `CountedOnce_InRealBattlesWithLosses_…`, §13.6.]**
- **Teeth and aim (ADR-015 §7.2).** Every killing test asserts a property the design states: the E-series the settlement's terms
  (DB1-R3 (a): separate terms, one clamp; an army that never entered pays nothing and takes no rise; the in-battle charge counted
  once), the C-series the codec's contract (a version-1 table refused, Entered 0 or 1, the field order), the B-series once per
  formation, the arrival instant, the report fields and the state hash, L1 and G3 lane MOR's rule that a zero-damage blow is no
  blow. No kill rests on a golden hash alone: every kill list above names at least one semantic test.
- **The four build failures are the harness's, not verdicts.** The tree builds with warnings as errors: B2 left `placed` unused
  (CS0219), B5 left two fields never assigned (CS0649), B7 and B8 made code unreachable (CS0162). Each was re-expressed with the
  same effect in a form that compiles — B2b `if (placed || f.Members.Length >= 0)`, B5b the two fields set to "never entered"
  instead of the call, B7b and B8b `if (_r.EntryMoraleCost < 0)` (never true for a validated rule) — and each was killed.
- **H1 and A1** survive and are equivalent: §7 has the derivation and the measurement. The volley record now carries a comment
  saying so (`AutoResolver.cs`, comment only).

---

## 7. Lane MOR's open round-2 items (brief item 3)

- **MOR-V2-04 / mutant H1** (the AutoResolver's in-contact volley not recorded in the hit table). **H1 is an equivalent mutant and no
  test can kill it.** DERIVED from `AutoResolver.cs` step 4: the in-contact branch runs the melee first; `engaged` is at least 1
  whenever both formations have someone alive (`Share` returns at least 1 for n > 0), so the melee's expected damage is at least
  1 milli-HP (model 2 floors a valid attack at 1; model 1 throws on 0), and the melee record `_lastHitStep[i × n + m] = k` executes
  (its guard `dm > 0` always holds); the in-contact volley is gated by the same `!surprised` and writes the same value k to the same
  cell m in the same step. So whenever H1's line would execute, the table already holds exactly what it would write. MEASURED:
  the mutant survives the bounded filter run (237 tests) and the full Debug suite (1474 of 1474 pass), as does A1. A TEMPORARY
  instrumentation of `AutoResolver.cs` (applied and restored byte for byte by the probe script, the probe test classes created and
  deleted by exact name; sources kept in the scratchpad `mor2/probeH1/`) threw unless the cell already held k at H1's line and
  unless dm > 0 at A1's guard, and counted both: lane P's tuning battery with "the fight it won" at 100, 200 and 400 and the
  in-battle cost at 0 and 50 (9,600 battles) executed H1's line once and A1's 147,608 times; a targeted set (archers holding against
  heavy infantry, 600 battles) executed H1's line 40 times and A1's 80; no violation, and the full suite under the instrumentation
  passed (1476 of 1476 with the two probe tests). The line executes rarely in these battles (an in-contact volley needs a ranged
  formation in contact that did not move, can fire and is not under an Attack order; INFERRED from its guard). The record is kept
  (it states the intent that a volley is a blow, and it would matter if the melee path ever stopped recording), now with a comment
  saying it is redundant; the brief's "H1 must die" cannot be met, and this is the reason.
- **The zero-damage guard (mutants G3 and L1).** Both are **killed by semantic tests** (`ZeroDamageGuardTests`): a probe search (720 and
  640 battles; scratchpad `mor2/probe-zero-L1.txt`, `probe-zero-G3.txt`; the probe was a temporary test class, created and deleted by
  exact name, its source kept as `mor2/ProbeMor2Zero.cs.txt`) found battles in which a formation's ONLY blow on an enemy formation
  within the pulse before it breaks is a zero-damage blow — the target already brought to 0 HP by the line's blows at the same instant
  (all damage of an instant lands before any death is resolved, and the lower attacker id's fire is scheduled first). L1: one archer
  that looses one arrow in the battle (seeds 3, 24); G3: one gun team with a one-shot direct-fire gun of 40 m range (seeds 3, 28). The
  formation dealt nothing, so it must not rise; with either guard dropped the test fails on both seeds (MEASURED, §6). H-MOR §11.6's measurement (1,896
  zero-damage crew-served hits in 432 battles, each beside a damaging hit of the same guns) showed why the suite could not see G3; a
  lone zero-damage blow is rare, not impossible. A third guard of the same kind, the AutoResolver's melee record (`dm > 0`, mutant A1),
  is equivalent by the same derivation as H1 (it always holds in contact).
- **MOR-V2-05** (three "Pattern seen" cells citing annex sections without the pattern). **Corrected in place** in proposal §3.3 with the
  dossier's claims: rout contagion (data-06, WEAK; official-42, WEAK), fatigue (data-07, data-17, CONFIRMED; community-21, WEAK), fire
  without casualties (data-05, CONFIRMED; community-08, official-13, community-21, WEAK); the old cells struck, dated.

---

## 8. Annex A rewrite (brief item 4)

The morale proposal's Annex A is rebuilt from the research dossier (`…/scratchpad/m7c/research/run1/dossier.md` and its claim files and
`check.json`): A.0 (labels: lane MOR's [CODE], [SEARCH], [KNOW] kept, the dossier's [FETCH] added — no claim of the pass carries it; the
schema's CODE rows would be FETCH from one source under a stricter reading), A.1 (method and limits: 93 hosts, 90 refused; the one
readable file and its hash), A.2 army level, A.3 unit level, A.4 siblings, A.5 genre, A.6 what we take, A.7 entering a battle (Q1), A.8
the fate of every earlier row. ~~Every row carries its claim id and the check step's verdict;~~ **[r1, MOR2-R1-10: not yet true:
20 claim ids in 12 groups and two A.7 cells stood without a verdict, and data-18 (CONFIRMED) read as WEAK; corrected in place, now
true: §13]** no SEARCH claim is CONFIRMED (the search
budget was exhausted before the check). The earlier annex is kept, marked superseded; five of its rows (and two statements of §2) are
struck where the dossier corrected them or does not establish them ("no army morale number", "not as a morale stock", "names only",
"the keys were not available", "no stock"); the rest stand only as WEAK. No name, number or text of the reference enters the design
(D-069): the annex cites claim ids and paraphrases; it names games only as research references.

---

## 9. What ran, what was NOT RUN, what was deferred

**RAN (MEASURED; Linux x64 container, shared 4-CPU host, `nice -n 19`, `-m:1`, `-nodeReuse:false`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`,
always naming the solution or project):**

- **Baseline** on `7cacc3a`, before any change: `M7Proto.slnx` Debug and Release builds 0 warnings, 0 errors; tests **1439
  passed, 0 failed, 0 skipped** in both (Debug 51 s, Release 18 s).
- **Closing**, on the code committed at the branch head (no code changed after these runs; only this report): `dotnet build
  M7Proto.slnx -c Debug -m:1 -nodeReuse:false --no-incremental` 0 warnings, 0 errors (7.5 s); `dotnet test M7Proto.slnx -c Debug
  --no-build` **1474 passed, 0 failed, 0 skipped** (42 s); the same in Release: 0 warnings, 0 errors (7.6 s), **1474 passed, 0
  failed, 0 skipped** (15 s).
- `bash prototypes/m7-sprint1/scripts/check-proto-determinism.sh`: **DETERMINISM SCAN: CLEAN** (108 files under `Proto.M7/`, 0
  pattern groups with findings).
- `python3 scripts/check-doc-links.py --all`: **467 files checked, 0 broken links** (docs/ and the root; it does not walk
  `prototypes/`); the same script on this report and the proposal by name: 2 files checked, 0 broken links.
- `dotnet Proto.M7.Bench.dll entry-parity 50 0 2` and `entry-parity 50 200 2` (Release, §5).
- The mutants (§6): 30, and 4 re-expressed, bounded.
- Temporary probes, each a test class created and deleted by exact name, sources kept in the scratchpad: the zero-damage search
  (`…/mor2/ProbeMor2Zero.cs.txt`, 720 + 640 battles), the H1/A1 instrumentation with the rout probe and the targeted H1 set
  (`…/mor2/probeH1/`: `run_probe.py`, `run_probe_b.py`, the sources and their outputs `probe-H1.txt`, `probe-H1b.txt`,
  `probe-rout.txt`). The full suite also passed under the H1/A1 instrumentation (1476 of 1476 with the two probe tests).

**NOT RUN, and why:**
- `M7Econ.slnx`, `M7DetMath.slnx`: none of their files changed, and neither `Proto.M7.Econ` nor `Proto.DetMath` references `Proto.M7`
  (MEASURED from the project files).
- `scripts/validate.sh` and every `Sim.*` suite: no `Sim.*` change (D-079 Q5).
- Lane P's held-out acceptance battery and control battery with the in-battle part on: lane P's record; the parity gate is OPEN
  (D-079 Q4); the switch-on is proposed for the battery's re-measurement with a re-fit (§5).
- `Proto.M7.Bench` timing modes: no performance claim. With the in-battle part off the resolvers' added work is one loop over the land
  formations at construction and one check per landed entity (INFERRED negligible; not measured).
- A Windows run; an independent verification of this lane (ADR-015 §6: the next verification round).

**Deferred (to whom):**
- switching the in-battle part on (50), with lane MOR's c = 200: the next parity re-measurement and AutoResolver re-fit (D-079 Q4, D11);
- a reinforcement that marches onto the field, and the start-morale check against the campaign stock: the M7.15 hand-off;
- the size's calibration: M7.14 (A-38);
- vessels' and aircraft's in-battle entry: lane N's model (they are reported entered at the start);
- the sandbox's entry-charge inspection and settlement preview (PROPOSAL, proposal §11.7): lane D's sandbox follow-up.

---

## 10. Limitations

1. The in-battle part is 0 by default: by default neither the battle nor the War Sandbox feels the cost (the campaign pays it exactly).
2. The only later arrival is lane N's landing (TacticalResolver only); the AutoResolver refuses formations that start aboard.
3. Vessels and aircraft are outside the in-battle charge; their armies pay the settlement term **[r1: when their formation entered — a
   vessel formation at the start, an aircraft formation at its first take-off; one that never left its base pays nothing]**.
4. The hand-off is lane D's stand-in (one army, one formation); the tests' `HandOff` maps a battle report onto it as M7.15 would.
5. The size is not calibrated; the reference gives no number for it (no value of any key is known).
6. The battery effect was measured on the tuning battery only.
7. Pre-existing, for lane P: a formation starting below the rout threshold routs in the TacticalResolver (its men leave the field
   routed) but withdraws in good order in the AutoResolver — MEASURED by the temporary rout probe on one test-input battle (20 heavy
   infantry against a distant enemy) at each of the starts 280 and 299 without the cost and 320 and 349 with it (cost 50), while 350
   with it and 320 without it rout in neither; the cause (the AutoResolver's pulse-start withdrawal runs before its rout check) is
   INFERRED from the code. The in-battle cost widens that band from [0, 300) to [0, 350) when on (MEASURED on the same battle).
   **[r1, MOR2-R1-08: and the band shows in persons — through lane W's collapse the routed formation of a side that did not hold the
   field loses 1,402 to 1,500 of 2,000 persons to the deserters (starts 300 to 349 with the cost on), the AutoResolver's orderly
   withdrawal none (MEASURED, round-1 probe, §13.2); rule P1 does not measure it.]**
8. The zero-damage tests rest on chosen seeds of TEST INPUT scenarios (found by a probe), their preconditions asserted from outside the
   rule's code; they pin the guard on those battles.

---

## 11. For the Director

No new question. Where the cost is charged and its size are delegated (D-083 §4; D-082 §5) and presented for review: (iii), with the
in-battle part switched on at the next battery re-measurement; 50,000 (50 on the battle scale). The mutual-elimination win: PROPOSAL (a)
(no change), with (b) stated **[r2: and (b′), (c′); its consequence corrected, §14.8]**. Observations, not questions: the prototype has no marching reinforcement (DB1-R1 (a)'s reinforcement case
is pinned on a landing); switching the in-battle part on moves rule P1 on the tuning battery from 16/20 to 14/20 (13/20 with c = 200),
so it needs the AutoResolver re-fit (§5); the round-2 verifier's mutant H1 cannot be killed because it is equivalent (§7), so the
brief's "H1 must die" is not met and cannot be; and for lane P, the pre-existing rout-versus-withdrawal difference of the two
resolvers at a start below the rout threshold (§10 item 7, MEASURED on one battle).
**[r1: still no new question; round 1's observations are in §13.9.]**

---

## 12. Git, files, line counts

Branch `lane/MOR2` in `/home/user/m7w/MOR2`, cut from `7cacc3a`; nothing pushed and nothing merged (the Director rules on
acceptance; the finish protocol hands back a patch and a bundle of `7cacc3a..HEAD`). Commits, oldest first:

- `ef3f59c` the entry-cost code (settlement term on, in-battle part default 0);
- `60d1bb1` the 42 settlement test cases updated with the 50,000 entry cost (§4);
- `cc1f0d0` the entry-cost tests;
- `e584b7e` proposal §11, the rebuilt Annex A, the zero-damage guard tests, the `entry-parity` bench;
- `8efaace` one once-per-formation guard (`EntryMorale.Enter`) instead of two;
- `2273771` proposal §11.4 wording;
- `d810a5b` the comment on the AutoResolver's in-contact volley record; the measured rout wording (§11.4, §11.10);
- this report's commit, which also rewraps proposal §11.4's rout bullet (wording only): the branch head (its id is in the lane's
  structured result).

Line counts (`git diff --numstat 7cacc3a..d810a5b`, before this report's commit):

| File | + | − |
|---|---|---|
| `docs/m7-plan/morale-proposal.md` | 450 | 34 |
| `prototypes/m7-sprint1/Proto.M7.Bench/EntryBench.cs` | 115 | 0 |
| `prototypes/m7-sprint1/Proto.M7.Bench/Program.cs` | 1 | 0 |
| `prototypes/m7-sprint1/Proto.M7.Tests/Concealment/EqualSidesTests.cs` | 7 | 1 |
| `prototypes/m7-sprint1/Proto.M7.Tests/LaneD/SupportCapabilityTests.cs` | 28 | 15 |
| `prototypes/m7-sprint1/Proto.M7.Tests/Morale/CampaignMoraleTests.cs` | 105 | 53 |
| `prototypes/m7-sprint1/Proto.M7.Tests/Morale/EntryCostTests.cs` | 602 | 0 |
| `prototypes/m7-sprint1/Proto.M7.Tests/Morale/ZeroDamageGuardTests.cs` | 108 | 0 |
| `prototypes/m7-sprint1/Proto.M7/Morale/CampaignMorale.cs` | 38 | 4 |
| `prototypes/m7-sprint1/Proto.M7/Morale/MoraleCodec.cs` | 18 | 4 |
| `prototypes/m7-sprint1/Proto.M7/Support/SupportTypes.cs` | 10 | 0 |
| `prototypes/m7-sprint1/Proto.M7/Support/SupportWorld.cs` | 21 | 4 |
| `prototypes/m7-sprint1/Proto.M7/Tactical/AutoResolver.cs` | 20 | 1 |
| `prototypes/m7-sprint1/Proto.M7/Tactical/BattleResult.cs` | 11 | 0 |
| `prototypes/m7-sprint1/Proto.M7/Tactical/BattleSetup.cs` | 9 | 0 |
| `prototypes/m7-sprint1/Proto.M7/Tactical/EntryMorale.cs` | 56 | 0 |
| `prototypes/m7-sprint1/Proto.M7/Tactical/TacticalBattle.Domain.cs` | 4 | 1 |
| `prototypes/m7-sprint1/Proto.M7/Tactical/TacticalBattle.DomainMove.cs` | 1 | 0 |
| `prototypes/m7-sprint1/Proto.M7/Tactical/TacticalBattle.cs` | 28 | 1 |

19 files, 1,632 lines added and 118 removed, plus this report (392 lines). Every path is under `prototypes/m7-sprint1/` or
`docs/m7-plan/` (MEASURED: `git diff --name-only 7cacc3a..HEAD` lists nothing else).


---

## 13. Verification round 1 (2026-11-11)

Fix round on the verifier's findings MOR2-R1-01..13 (verifier worktree pinned to `8ff9816`). Clone `/home/user/m7w/MOR2`, branch
`lane/MOR2`, from `8ff9816`. Every finding was first reproduced in this clone at `8ff9816` with this round's own harness and probes
(the verifier's mutant edits and probe file copied into this round's own specification and a temporary test file, created and deleted
by exact name; scratchpad `mor2-fix1/`); a finding was fixed only once reproduced. Statements this round superseded are marked in place
above (§0, §2, §3.1, §4, §6, §8, §10, §11) with `[r1, …]` notes, and in the proposal (header; §4.5; §11.2–§11.11; Annex A.0, A.4,
A.7, A.8; the new §11.12). Labels as in the header: MEASURED (run in this round, on this tree), DERIVED, INFERRED, PROPOSAL, READING.

### 13.1 Verdicts

| ID | Sev. | Verdict | Reproduction (MEASURED, `8ff9816`) | Fix | Pinned by (semantic tests) |
|---|---|---|---|---|---|
| MOR2-R1-01 | major | **REPRODUCED** | the verifier's probe P2 re-run here: SideEliminated; formation 2 (20 riflemen aboard, morale 800): killed 7, escaped 13, morale end 100, EnteredAtMs null, charged 0; lane W's collapse [13, 0, 0, 7, 0]; `SupportWorld.ScheduleBattle` threw ArgumentOutOfRangeException ("a formation that never reached the field has no charge, loss or casualty") | the stand-in accepts a never-entered formation's casualties and loss and refuses only an in-battle charge for it; the settlement (unchanged in form) gives it no entry cost, DR29's decrease of what the battle cost it, and no rise (READING MOR2-R1 (a)); proposal §11.2 item 4 and §11.5 Law 1 struck and corrected; `SupportTypes`, `SupportWorld`, `CampaignMorale` comments marked | `NeverEntered_TroopsKilledAboardTheirTransport_TheHandOffSettlesIt_DR29Applies_NoEntryCost` (the real battle: decrease 700,000, after 100,000; 700 of 2,000 persons dead); `DB1_AFormationThatNeverReachedTheField_PaysNothing_AndTakesNoRise` (extended) |
| MOR2-R1-02 | minor | **REPRODUCED** | probe P1 re-run: 150 completed instants, the airframe Based at every one, the battle SideEliminated at 300,000 ms, formation 3 EnteredAtMs = 0 = StartMs; `DomainResults` set `EnteredAtMs = _setup.StartMs` for every vessel and aircraft formation | each domain formation records the instant it is first on the field (`DEnt.OnField`) through `EntryMorale.Enter` at cost 0: vessels at the start, aircraft at their first take-off (`TakeOff`); null when no airframe leaves its base; `TacticalBattle.EnteredAtMs(id)` answers for domain formations too; READING MOR2-R2 labelled with (a) applied and (b) stated (proposal §11.2) | `Aircraft_AFormationThatNeverLeavesItsBase_NeverEnters_AndItsArmyPaysNothing`, `Aircraft_EntersAtItsFirstTakeOff_TurnExact` (3 delivery instants), `Aircraft_ASecondSortieIsNoNewEntry`, `BothResolvers_TheInBattleDefault_IsTheIdentity` (its aircraft battle) |
| MOR2-R1-03 | minor | **REPRODUCED** | probe P3 re-run: before 800,000, decrease 101,000, rise 200,000, entry 50,000, after 849,000 (+49,000) | argument (3) of proposal §11.8 struck; the measured statement in its place ~~(a routed DA6 winner against an equal force ends at 150,000 + end × 1,000, a rise from any start below 449)~~; (a) kept on arguments ~~(1), (2),~~ (4) with the consequence stated; a narrower alternative (b′) added **[r2, MOR2-R2-01: the struck statement rested on the false premise "a formation that routed ends below 300"; it holds for a formation whose morale fell to its end, not in general (an unfought equal-sides win nets +150,000 from any start), and (1) and (2) do not reach that case; §14]** | `ProposalA_AMutualRoutWonUnderDA6_NetsARiseFromALowStart_ByHand` (+49,000 / 0 / −51,000 from 400 / 449 / 500) **[r2: one fought case, by hand]** |
| MOR2-R1-04 | minor | **REPRODUCED** | proposal §11.2 item 4 and §2 above, at `8ff9816`: "DERIVED, not a new reading"; D-082 DA1-R (a) says "took part" and does not define it; P2 is a real battle where entering and taking part differ | labelled READING MOR2-R1 with both alternatives and their consequences: (a) APPLIED, took part = entered; (b) entered or engaged without reaching the field | `DB1_AFormationThatNeverReachedTheField_…`: a never-entered formation with casualties whose side won settles at 600,000 (no rise under (a)); the same battle entered at 750,000 |
| MOR2-R1-05 | minor | **REPRODUCED** | `ef3f59c` built and run in a temporary worktree (scratchpad, removed after): 1397 passed, 42 failed; every first failing assertion parsed (this round's script): 37 differ by exactly 50,000 in every differing value, 5 by another amount (38,334 twice; 7,143; 100,000; a length of 470 against 555) | §4's two statements struck and the measured breakdown written in place, each of the 5 explained; commit `60d1bb1`'s message overstates in the same way and is noted, not rewritten (history) | — (a record) |
| MOR2-R1-06 | minor | **REPRODUCED** | V02, V09 and V10 (the verifier's edits) each survived this round's filter run (352 tests) and the full Debug suite (1474 of 1474; 48.1 s, 48.8 s, 43.7 s; bound 460 s) | V02: the re-entry test now runs at cost 0 too; V10: a real clamp-at-0 entry (charged 30 of 30) handed to the settlement in both resolvers; V09: the casualty loop it weakened is removed by the MOR2-R1-01 fix (the refusal itself was wrong); the remaining never-entered refusal (an in-battle charge) is killed by R1-M02 | `Tactical_AFormationThatLeavesTheFieldAndIsPlacedOnItAgain_PaysOnce(cost: 0)`; `ClampAtZero_AFormationThatEntersWithLessMoraleThanTheCost_IsSettled_ItsDR29StartsFromZero` |
| MOR2-R1-07 | minor | **REPRODUCED** | V11 survived the filter run (352, the identity test included); in the full suite it was killed only by two golden tests (`DomainOrdersAndGoldenTests.AmphibiousBattleReplaysExactlyFromItsOrderLogAndMatchesTheGolden`, `ReplayAndMetamorphicTests.ReplayFromRecordedOrdersReproducesTraceAndResult`); the test's "unnamed" setup was `EntryMoraleCost = new TacticalRules().EntryMoraleCost`, i.e. 0 again | the identity test now compares four battles' result, state and trace hashes with those recorded on the base commit `7cacc3a` (no entry code): three formations entering at the start in both resolvers, a formation landing later, an aircraft formation taking off; recorded by this round's temporary probe in a temporary worktree (source kept, scratchpad `mor2-fix1/Fix1BaseHashes.cs.txt`) | `BothResolvers_TheInBattleDefault_IsTheIdentity` (rewritten) |
| MOR2-R1-08 | minor | **REPRODUCED** | probes P5 and P6 re-run (the numbers of the finding, exactly); this round's own probe P8 (§13.2), lane W's collapse of 2,000 persons in both resolvers: with the cost on, starts 300 / 320 / 349 → tactical all routed, 1,500 / 1,460 / 1,402 deserted; with it off, 0 from 300 up; AutoResolver: all fit, 0 deserted at every start, with and without | proposal §11.4 (the consequence in persons), §11.5 Law 1 struck and corrected, §11.10 observation, §11.11 item 8; §10 item 7 above | — (a documentation finding; the person-level gap is MEASURED by the probe and deliberately not pinned as a test: it is a gap, not a property the system ought to have) |
| MOR2-R1-09 | nit | **REPRODUCED** | proposal at `8ff9816`: §4.5 had no note; §11.9's list omitted §4.5; §11.6 did not discuss how after-battle persons weight the decrease | §4.5 marked in place; §11.6 states the re-weighting with a DERIVED example (≈63,636 after against 350,000 at the start) and the alternative (start persons for the decrease and entry, after-battle persons for the rise); §11.9 corrected | — (documentation) |
| MOR2-R1-10 | nit | **REPRODUCED** | A.8's ambush row read "data-02, data-18; official-35, official-36 · WEAK" with data-18 CONFIRMED in `check.json`; two A.4 rows and the A.7 Q1-b row had no claim id or verdict of their own; this round's script (struck text excluded) at `8ff9816`: 145 id–verdict pairs, 0 mismatches, 20 claim ids in 12 groups without a verdict beside them (the verifier's 1 mismatch is the same ambush row read with its trailing WEAK applying to the whole cell) | rows corrected in place with dated notes; rows resting on no claim of their own (the check step's observations, a dossier sub-answer) say so and cite the claims they rest on | — (documentation); MEASURED after the fix: 175 id–verdict pairs, 0 mismatches, 0 unpaired (`mor2-fix1/tools/annex_check2.py`) |
| MOR2-R1-11 | nit | **REPRODUCED** | `SandboxSession.cs:91` `_rules = rules ?? new TacticalRules();`; `TacticalRules.EntryMoraleCost`'s comment ("PROPOSAL value when switched on: 50") and `CampaignMorale.EntryBattleScale`'s ("the value the M7.15 hand-off would set") stated two mechanisms | PROPOSAL stated once: the switch-on changes the rules' DEFAULT (one value for the campaign's battles and the sandbox, DR30); both comments corrected; proposal §11.7 | `WarSandbox_QuickBattle_ChargesTheInBattlePartThroughItsRules_NothingAtTheDefault` |
| MOR2-R1-12 | nit | **REPRODUCED** | V14 killed only by `DB1_TieDense_…` (a hand-made row) in the filter run; the four real battles of `DB1_TheCostReachesTheCampaignExactly_…` end at or above 800, where DR29's base changes nothing | real battles with losses added in both resolvers; §2's wording corrected | `CountedOnce_InRealBattlesWithLosses_DR29IsMeasuredFromTheStartLessTheCharge` (2 seeds × 2 resolvers) |
| MOR2-R1-13 | nit | **REPRODUCED** | `git grep -n -i siege 8ff9816 -- docs/m7-plan/morale-proposal.md prototypes/m7-sprint1/reports/H-MOR2.md prototypes/m7-sprint1/reports/H-MOR.md`: no match; lane S's code read (`SiegeEquations.cs`, `SiegeWorld.cs`, `SiegeTypes.cs`) | proposal §11.12 (PROPOSAL (a): DB1 reaches siege battles when the land module resolves them; (b) a term in lane S's stand-in, not proposed) and §11.11 item 7 | — (documentation) |

Summary: **13 REPRODUCED and fixed; 0 REFUTED.** No golden hash moved; lane P's tuning battery is unchanged (§13.5).

### 13.2 Reproduction runs (MEASURED, `8ff9816`; filter run of 352 tests bounded at 200 s ≈ 10 × its clean 19.7 s; a survivor
re-runs the full Debug suite bounded at 460 s = 10 × the clean 46 s)

| Run | What it does | Result |
|---|---|---|
| V02 (verifier's edit, copied) | once-guard only while the cost is on | SURVIVED: filter 352/352 (19.0 s), full 1474/1474 (48.1 s) |
| V09 | never-entered casualty check skips the wounded category | SURVIVED: 352/352 (20.3 s), 1474/1474 (48.8 s) |
| V10 | an in-battle charge equal to the start refused | SURVIVED: 352/352 (19.5 s), 1474/1474 (43.7 s) |
| V11 | the tactical entry record hashed also at cost 0 | filter 352/352 (21.5 s); full suite KILLED by two goldens only (43.8 s) |
| V14 | DR29 from the start less twice the charge | KILLED in the filter by `DB1_TieDense_…` alone (19.6 s) |
| probes P1–P6 (the verifier's file, copied, deleted by exact name) | the numbers of MOR2-R1-01, -02, -03, -08 | identical to the finding's (scratchpad `mor2-fix1/repro-probes.txt`) |
| probe P8 (this round's, `mor2-fix1/Fix1Probe-P8.cs.txt`, deleted by exact name) | persons moved to the deserters, both resolvers, starts 299–350, cost 0 and 50 | §13.1 MOR2-R1-08 (`mor2-fix1/probe-P8.txt`) |
| `ef3f59c` in a temporary worktree | the 42 failures before `60d1bb1` | 1397 passed, 42 failed; 37 + 5 as in §13.1 |
| `7cacc3a` in a temporary worktree | the identity test's reference hashes | 4 battles recorded (`mor2-fix1/base-hashes.txt`) |

Clean baseline on `8ff9816` (this round): `M7Proto.slnx` Debug and Release builds 0 warnings, 0 errors; Debug 1474 / 0 / 0 (test step
43 s), Release 1474 / 0 / 0 (17 s). The temporary worktrees (`git worktree add` in the scratchpad) were removed with
`git worktree remove`; `git worktree list` shows only the clone.

### 13.3 What changed

- **Code (7 files, each change marked `[MOR2, verification round 1, …]`):** `Support/SupportWorld.cs` (MOR2-R1-01: the never-entered
  refusal narrowed to the in-battle charge; comments), `Support/SupportTypes.cs` (comment), `Morale/CampaignMorale.cs` (comments:
  READING MOR2-R1; MOR2-R1-11), `Tactical/TacticalBattle.Domain.cs` (MOR2-R1-02: `DForm.EnteredAtMs`, `DomainEnterIfPlaced`,
  `DomainEnterAtStart`, the take-off hook, the result field), `Tactical/TacticalBattle.cs` (the start hook; `EnteredAtMs(id)` for
  domain formations), `Tactical/BattleResult.cs` and `Tactical/BattleSetup.cs` (comments). No coefficient or default changed; the
  in-battle part stays 0 and the settlement term 50,000. Behaviour changed in two places only: the stand-in now settles a
  never-entered formation's losses (it refused them), and an aircraft formation's reported `EnteredAtMs` is its first take-off (a
  report field, in no hash).
- **Tests (2 files):** `Morale/EntryCostTests.cs` (3 tests changed, each marked: the never-entered test extended; the re-entry test a
  theory over the cost; the identity test rewritten; `HandOff` made internal); `Morale/EntryCostRound1Tests.cs` (new: 8 methods, 13
  cases). No test of another lane changed.
- **Docs:** `docs/m7-plan/morale-proposal.md` (the places listed in its §11.9 round-1 note; new §11.12); this report.

### 13.4 Tests added or changed (MEASURED: all pass in Debug and in Release)

| Test | What it pins |
|---|---|
| `NeverEntered_TroopsKilledAboardTheirTransport_TheHandOffSettlesIt_DR29Applies_NoEntryCost` | MOR2-R1-01 with lane N's own battle: never on the field yet 7 of 20 killed aboard, morale 800 → 100; the hand-off settles it: Entered 0, entry 0, restore 0, decrease 700,000, rise 0, after 100,000; lane W's collapse: 700 of 2,000 dead, 1,300 left; the transport entered at the start |
| `DB1_AFormationThatNeverReachedTheField_PaysNothing_AndTakesNoRise` (changed) | untouched never-entered armies unchanged (800,000); never-entered with casualties and a loss (800 → 600): decrease 200,000, no entry, **no rise although its side won (READING MOR2-R1 (a))**, 600,000, persons dead 400, wounded 200, captured 80, deserted 48; the same battle entered: 750,000; refusals: an in-battle charge for a never-entered formation (50; 1), a charge above the start or below 0 |
| `Aircraft_AFormationThatNeverLeavesItsBase_NeverEnters_AndItsArmyPaysNothing` | MOR2-R1-02: Based at every completed instant; EnteredAtMs null in the report and the accessor; its army's settlement 800,000 (no entry, decrease, rise) |
| `Aircraft_EntersAtItsFirstTakeOff_TurnExact` (3) | TURN-EXACT, observed from outside the rule: launch orders delivered at (pulse 0, step 5), (1, 0), (2, 3): EnteredAtMs null at the completed instant before the airframe is airborne, equal to that instant at it (not StartMs, not a step later); no in-battle charge even with the part on |
| `Aircraft_ASecondSortieIsNoNewEntry` | ONCE: two take-offs observed (both sorties flown), EnteredAtMs the first (StartMs + StepMs) after the second |
| `ClampAtZero_AFormationThatEntersWithLessMoraleThanTheCost_IsSettled_ItsDR29StartsFromZero` | MOR2-R1-06 (V10): real battles in both resolvers, start 30, cost 50: charged 30, end 0; the hand-off with EntryChargedInBattle = MoraleStart is accepted (31 refused); settlement from 30,000: entry 50,000, decrease 0, after 0 |
| `Tactical_AFormationThatLeavesTheFieldAndIsPlacedOnItAgain_PaysOnce` (now 2 cases) | MOR2-R1-06 (V02): at cost 0 too, each formation's EnteredAtMs is its FIRST placement (no row, no charge, morale 900 / 700) |
| `BothResolvers_TheInBattleDefault_IsTheIdentity` (rewritten) | MOR2-R1-07: at the default (named 0 or unnamed) four battles reproduce the hashes recorded on `7cacc3a`: three formations in both resolvers, a landing at StartMs + StepMs, an aircraft taking off at StartMs + 5 StepMs |
| `CountedOnce_InRealBattlesWithLosses_DR29IsMeasuredFromTheStartLessTheCharge` (2) | MOR2-R1-12: 30 heavy infantry at 800 against 40, cost 50, both resolvers, end below 700: decrease exactly (750 − end) × 1,000, entry 50,000, after by hand |
| `ProposalA_AMutualRoutWonUnderDA6_NetsARiseFromALowStart_ByHand` (3) | MOR2-R1-03: a routed DA6 winner over an equal force from 400 / 449 / 500: after 449,000 each, net +49,000 / 0 / −51,000 **[r2, MOR2-R2-01: a hand-made row of a formation whose morale FELL to 299 (a fought mutual rout), not the general case; its comment is corrected in place, its assertions unchanged; the general case is pinned in real battles, §14.4]** |
| `WarSandbox_QuickBattle_ChargesTheInBattlePartThroughItsRules_NothingAtTheDefault` | MOR2-R1-11: Quick Battle, AI against AI (AutoResolver) and a player side (TacticalResolver): EntryCharged 0 for every formation at the default rules, 50 with rules carrying 50; EnteredAtMs = StartMs |

Counts: `M7Proto.slnx` **1474 → 1488** (14 new cases: 13 in the new class, 1 theory row).

### 13.5 What ran (MEASURED; host and flags as in §9)

- Baseline on `8ff9816` (§13.2). Closing, on the code at `b19f1ac` (no code changed after it): `dotnet build M7Proto.slnx -c Debug
  -m:1 -nodeReuse:false --no-incremental` 0 warnings, 0 errors (16 s); `dotnet test M7Proto.slnx -c Debug --no-build` **1488 passed, 0
  failed, 0 skipped** (39 s); the same in Release: 0 warnings, 0 errors (16 s), **1488 passed, 0 failed, 0 skipped** (19 s).
- `bash prototypes/m7-sprint1/scripts/check-proto-determinism.sh`: **DETERMINISM SCAN: CLEAN** (108 files, 0 pattern groups with
  findings).
- `python3 scripts/check-doc-links.py --all` on the final docs: **467 files checked, 0 broken links**; on the proposal and this report by
  name: 2 files checked, 0 broken links.
- The annex check (`mor2-fix1/tools/annex_check2.py` against `check.json`): 175 id–verdict pairs, 0 mismatches, 0 unpaired.
- **Lane P's tuning battery, because the TacticalResolver changed** (the domain entry hook): `dotnet Proto.M7.Bench.dll entry-parity 50
  0 2` (Release, scratchpad `mor2-fix1/bench/entry-50-0.md`): byte-identical to round 0's output except the wall-time line — digests
  tactical off `90E58DCB…F65D6914` (lane P's), on `413CD6C2…88E29A18`; auto off `2AC31207…A0B68F31`, on `048A8EE5…FA33101E`; 3,440
  entry rows on; outcomes changed 649 / 598, winners 134 / 103; rule P1 off 16/20, on 14/20. The joint switch-on `entry-parity 50 200
  2` (`mor2-fix1/bench/entry-50-200.md`): byte-identical to round 0's except the wall-time line — on: tactical `D4784D79…E2E26137`,
  auto `0FE5ED33…6DA15389`; outcomes changed 634 / 567, winners 133 / 74; rule P1 13/20. The battery has land formations only (the
  verifier's P4: 3,440 of 3,440), which is why the domain change cannot move it.
  No golden or pin moved (MEASURED: the full suites pass with no golden edited). Lane P's acceptance stays OPEN (D-079 Q4).

### 13.6 Fix-round mutants (final code `b19f1ac`; filter run of 366 tests bounded at 200 s ≈ 10 × its clean 19.3 s; full Debug suite
for a survivor bounded at 460 s; every kill by a named semantic test)

Harness `mor2-fix1/tools/mut.py` (this round's own): one exact-string replacement that must occur exactly once, a Debug build, the
filter run; the file restored byte for byte after each mutant and the restored tree rebuilt (`git status` clean afterwards).
Specification `mor2-fix1/fix-spec.json`, results `fix-results.json`. **19 mutants, 19 KILLED in the filter run (18.3–24.1 s each),
none survived, none hung.**

| Id | Mutation | Killed by (failing cases) |
|---|---|---|
| R1-M01 | stand-in: a never-entered formation with a loss refused again (round 0) | `NeverEntered_TroopsKilledAboard…`, `DB1_AFormationThatNeverReachedTheField_…` (2) |
| R1-M02 | stand-in: the never-entered in-battle charge refusal dropped | `DB1_AFormationThatNeverReachedTheField_…` (1) |
| R1-M03 | settlement: a never-entered formation takes no DR29 decrease | `NeverEntered_TroopsKilledAboard…`, `DB1_AFormationThatNeverReachedTheField_…` (2) |
| R1-M04 | settlement: READING MOR2-R1 (b) instead of (a) | `DB1_AFormationThatNeverReachedTheField_…` (1) |
| R1-M05 | settlement: a never-entered army pays the entry cost | `NeverEntered_TroopsKilledAboard…`, `Aircraft_AFormationThatNeverLeavesItsBase_…`, `Tactical_AFormationThatNeverLeavesItsTransport_…`, `DB1_TieDense_…` and 2 more (6) |
| R1-M06 | tactical: vessels and aircraft reported entered at the start again | `Aircraft_AFormationThatNeverLeavesItsBase_…`, `Aircraft_EntersAtItsFirstTakeOff_TurnExact` (3), the identity test (5) |
| R1-M07 | tactical: a take-off is never an entry | `Aircraft_EntersAtItsFirstTakeOff_TurnExact` (3), `Aircraft_ASecondSortieIsNoNewEntry`, the identity test (5) |
| R1-M08 | tactical: an aircraft entry recorded one step late | `Aircraft_EntersAtItsFirstTakeOff_TurnExact` (3), `Aircraft_ASecondSortieIsNoNewEntry`, the identity test (5) |
| R1-M09 | tactical: a later take-off overwrites the aircraft entry | `Aircraft_ASecondSortieIsNoNewEntry` (1) |
| R1-M10 | tactical: no domain formation enters at the start | `Tactical_AFormationThatNeverLeavesItsTransport_…`, `NeverEntered_TroopsKilledAboard…` (2) |
| R1-M11 | tactical: an aircraft at its base counts as placed (Present, not OnField) | the aircraft tests (5), the identity test (6) |
| R1-M12 | settlement: alternative (b′), no rise for a winner left with nobody fit or wounded | `ProposalA_AMutualRoutWonUnderDA6_…` (3), `DA1_AMutualEliminationWin_…` (4) **[r2: re-run as R2-M03, §14.6]** |
| R1-M13 | sandbox: the rules it is given ignored | `WarSandbox_QuickBattle_…` (1) |
| V02 | verifier's: once-guard only while the cost is on | `Tactical_AFormationThatLeavesTheFieldAndIsPlacedOnItAgain_PaysOnce(cost: 0)`, `Aircraft_ASecondSortieIsNoNewEntry`, the identity test (3) |
| V10 | verifier's: a charge equal to the start refused | `ClampAtZero_…` (1) |
| V11 | verifier's: the tactical entry record hashed at cost 0 | the identity test (1) |
| V11a | V11's AutoResolver twin | the identity test (1) |
| V14 | verifier's: DR29 from the start less twice the charge | `CountedOnce_InRealBattlesWithLosses_…` (2), `DB1_TieDense_…` (3) |
| E3 | round 0's: DR29 from the start (the charge counted twice) | `CountedOnce_InRealBattlesWithLosses_…` (2), `ClampAtZero_…`, `DB1_TieDense_…` (4) |

- **Teeth and aim (ADR-015 §7.2).** Each killing test asserts a property the design states: the settlement's terms for a formation
  that never entered (DB1-R1 (a), DR29, READING MOR2-R1 (a)); when a carried or based formation enters, turn-exact and once (READING
  MOR2-R2 (a), DB1-R2 (a)); the legal clamp-at-0 boundary; counted once; the sandbox following its rules (DR30); PROPOSAL (a) of
  §11.8 (R1-M12 is the not-adopted alternative (b′): its kill shows the prototype applies (a), not that (b′) is wrong).
- **V11 and V11a are killed by the identity test, whose oracle is a recorded reference.** The default's identity is a compatibility
  property — "the engine at the default is the engine before the entry code" — so its only oracle is that engine's output, recorded
  on `7cacc3a` inside the test; the test names that property and nothing else, and is not one of the goldens of other lanes that
  caught V11 before. It is stated here so the reviewer can judge it.
- **V09 was not re-run**: the line it mutates no longer exists (removed by the MOR2-R1-01 fix).

### 13.7 Commits, line counts, closing checks

Commits on `lane/MOR2` this round, oldest first: `b19f1ac` (code and tests), `d11fa48` (proposal and report corrections in place),
`0ca965e` (this section, the annex-count note), and a last commit that adds only the line counts below (its id is in the lane's
structured result). Nothing pushed and nothing merged (the Director rules on acceptance).

Line counts, `git diff --numstat 8ff9816..0ca965e` (MEASURED): 11 files, 885 lines added and 92 removed.

| File | + | − |
|---|---|---|
| `docs/m7-plan/morale-proposal.md` | 167 | 20 |
| `prototypes/m7-sprint1/Proto.M7.Tests/Morale/EntryCostRound1Tests.cs` (new) | 328 | 0 |
| `prototypes/m7-sprint1/Proto.M7.Tests/Morale/EntryCostTests.cs` | 95 | 30 |
| `prototypes/m7-sprint1/Proto.M7/Morale/CampaignMorale.cs` | 8 | 2 |
| `prototypes/m7-sprint1/Proto.M7/Support/SupportTypes.cs` | 7 | 3 |
| `prototypes/m7-sprint1/Proto.M7/Support/SupportWorld.cs` | 13 | 9 |
| `prototypes/m7-sprint1/Proto.M7/Tactical/BattleResult.cs` | 3 | 1 |
| `prototypes/m7-sprint1/Proto.M7/Tactical/BattleSetup.cs` | 5 | 1 |
| `prototypes/m7-sprint1/Proto.M7/Tactical/TacticalBattle.Domain.cs` | 35 | 4 |
| `prototypes/m7-sprint1/Proto.M7/Tactical/TacticalBattle.cs` | 9 | 2 |
| `prototypes/m7-sprint1/reports/H-MOR2.md` | 215 | 20 |

Every path is under `prototypes/m7-sprint1/` or `docs/m7-plan/`: nothing in `Sim.*`, `Sim.Data`, CI or any other lane's report; no unit
data or cap changed; no new project.

### 13.8 NOT RUN, deferred, limitations

- **NOT RUN:** `M7Econ.slnx`, `M7DetMath.slnx` (no file of theirs changed; neither references `Proto.M7`); `scripts/validate.sh` and
  every `Sim.*` suite (no `Sim.*` change; D-079 Q5); lane P's held-out acceptance battery (lane P's record; D-079 Q4 OPEN); a Windows
  run; `Proto.M7.Bench` timing modes (no performance claim); an independent verification of this round (ADR-015 §6: the next round).
- **Not pinned as a test, on purpose:** the person-level parity gap of MOR2-R1-08 (a gap, not a property; MEASURED by probe P8).
- **Deferred:** the aggregation over several formations and its person count (M7.15; proposal §11.6); DB1 for siege battles (when
  the land module resolves them; proposal §11.12); switching the in-battle part on (the next battery re-measurement with a re-fit,
  D-079 Q4, D11); the size's calibration (M7.14, A-38).
- **Limitations:** READINGS MOR2-R1 and MOR2-R2 are this lane's, applied until the Director corrects them; the hand-off is still lane
  D's one-formation stand-in; the identity test's oracle is four recorded battles, not every battle (the goldens of the other lanes
  pin more); the aircraft entry is measured in the TacticalResolver only (the AutoResolver refuses aircraft).

### 13.9 For the Director

No new question; nothing reserved for the Director is decided here. Observations, not questions: (1) under PROPOSAL (a) for the
mutual-elimination win, ~~a defender of low morale whose formation routed in the same instant as the attacker's gains morale when DA6
names it the winner (+49,000 from a start of 400, up to +149,000; MEASURED, DERIVED) — the case of AP-R1-14; (b) and the narrower
(b′) are the alternatives (proposal §11.8)~~ **[r2, MOR2-R2-01: corrected, §14.8 — a defender whose formations ran, with no blow
struck, in the same instant as the attacker's gains +150,000 against an equal force from ANY start, in both resolvers]**. (2) With the in-battle part on, the cost widens a pre-existing parity gap in persons:
rout and desertion in the TacticalResolver against an orderly withdrawal in the AutoResolver (MOR2-R1-08; for lane P). (3) Two new
READINGS are applied and labelled with both alternatives: MOR2-R1, what "took part" means for DA1's rise; MOR2-R2, when a carried or
based formation is placed on the field (proposal §11.2).

---

## 14. Verification round 2 (2026-11-11)

Fix round on the verifier's findings MOR2-R2-01 and MOR2-R2-02 (verifier worktree pinned to `1efda25`). Clone `/home/user/m7w/MOR2`,
branch `lane/MOR2`, from `1efda25`. Each finding was first reproduced in this clone at `1efda25` with this round's own harness and
probes: the verifier's probe sources copied into this round's own temporary test files (created and deleted by exact name), the
verifier's two mutant edits copied into this round's own specification (scratchpad `mor2-fix2/`). A finding was fixed only once
reproduced. Statements this round superseded are marked in place above (§0, §2, §11, §13.1, §13.4, §13.6, §13.9) with `[r2, …]`
notes, and in the proposal (§11.2 item 1, §11.8, §11.9, §11.10, §11.11). Labels as in the header: MEASURED (run in this round, on this
tree), DERIVED, INFERRED, PROPOSAL, READING.

### 14.1 Verdicts

| ID | Sev. | Verdict | Reproduction (MEASURED, `1efda25`) | Fix | Pinned by (semantic tests) |
|---|---|---|---|---|---|
| MOR2-R2-01 | major | **REPRODUCED** | this round's probe (the verifier's `VerifyR2Probe2c` copied and extended: `mor2-fix2/Fix2ProbeR201.cs.txt`) gave every number of the finding: BothEliminated, reported winner 1 (DefenderOnEqualSides), 0 killed and 20 of 20 routed-escaped on both sides; the defender's army +150,000 at starts 250, 299, 320, 349, 500 and 800 with the in-battle part off and 320, 349 and 500 with it on (50), every settlement row entry 50,000, decrease 0, rise 200,000 (800: 750,000 → 900,000). The routes: at the start instant below the threshold (250, 299; 320 and 349 charged to 270 and 299), otherwise the inactivity rule's verdicts at pulse 4 (ends 420, 449, 600, 900; 550). Beyond the finding: the attacker's army −50,000 in every case; neither side held the field (HoldsField −1); and a second probe (`Fix2ProbeR201b.cs.txt`) found the same lift in the AutoResolver, where DA6 is not pending: two formations placed symmetrically below its withdrawal line withdraw in good order and end BothEliminated, the defender the winner (250, 400, 449) | proposal §11.8: the round-1 note's premise and consequence struck; the measured consequence in their place; arguments (1) and (2) re-examined (they do not reach the unfought case); (a) kept on (4) alone (DA1 is DECIDED; every alternative excepts some of its wins); (b) and (b′) corrected; the targeted alternative (c′) added, not implemented; §11.10 observation (2) and §11.11 item 10; this report §0, §2, §11, §13.1, §13.4, §13.9; the round-1 by-hand test's comment corrected in place (its assertions unchanged). **No production code changed** | `ProposalA_AnUnfoughtMutualRout_WonUnderDA6_TheDefendersArmyNetsTheRiseLessTheCost_FromAnyStart` (8), `ProposalA_AnUnfoughtMutualWithdrawal_InTheAutoResolver_WonUnderDA6_NetsTheRiseLessTheCost` (3), `ProposalA_AFoughtMutualRout_WonUnderDA6_DR29OutweighsTheRise` |
| MOR2-R2-02 | minor | **REPRODUCED** | V2-06 and V2-13 (the verifier's edits, copied) each SURVIVED the full Debug suite: 1488 of 1488 (44.4 s and 48.7 s; bound 450 s = 10 × the clean 45 s). Not equivalent: this round's copy of the verifier's probe, extended to assert vessels (`Fix2ProbeR202.cs.txt`), on lane N's amphibious battle: formations 10 and 11 report 0 (= StartMs), 12 reports 270,000, 13 and 14 report 0 and 24 reports 60,000, each equal to its own observed first take-off; under V2-06, 13, 14 and 24 report null and, in the two-flights battle, formation 4 null (observed take-off 10,000); under V2-13, formation 11 reports null | two tests on battles with several domain formations, each formation's entry observed from outside the rule | `SeveralDomainFormations_EachAircraftFormationEntersAtItsOwnFirstTakeOff_EveryVesselFormationAtTheStart`, `TwoAircraftFormationsOfOneSide_OnlyTheOneThatTakesOffEnters_AtItsOwnTakeOff_TurnExact` |

Summary: **2 REPRODUCED and fixed; 0 REFUTED.** No production code changed (MEASURED: `git diff --stat 1efda25..HEAD --
prototypes/m7-sprint1/Proto.M7` is empty); no golden or pin of another lane moved.

### 14.2 Reproduction runs and probes (MEASURED, `1efda25`)

| Run | What it does | Result |
|---|---|---|
| baseline | `M7Proto.slnx` builds and full suites | Debug and Release: 0 warnings, 0 errors (8 s each); Debug 1488 / 0 / 0 (42 s), Release 1488 / 0 / 0 (19 s) |
| V2-06, V2-13 (`tools/mut2.py`, full Debug suite, bound 450 s) | the verifier's two edits of `TacticalBattle.Domain.cs` | both SURVIVED, 1488 of 1488 (44.4 s, 48.7 s); restored tree rebuilt, `git status` clean (`mut/repro-results.json`) |
| probe R202 (`Fix2ProbeR202.cs.txt`) on `1efda25` and under each mutant (`tools/probe_under.py`) | each aircraft formation's observed first take-off against its EnteredAtMs; vessels against StartMs | as in §14.1 (`probe-R201b-R202.txt`, `probe-R202-under-mutants.txt`) |
| probe R201 (`Fix2ProbeR201.cs.txt`) | the verifier's seven cases plus starts 400, 449, 1000 and 350 with the part on; both armies' settlements; lane W's collapse; the AutoResolver on the same (asymmetric) setup | as in §14.1; 350 with the part on is charged to 300, not below the threshold, and routs by inactivity (end 400; +150,000); the 1000 case's settlement step failed in the probe's own set-up (its setup battle cannot bring an army to 1,000,000), its battle: BothEliminated, winner 1, end 1000. In the AutoResolver on the verifier's setup (side 1 one column farther from its exit edge) both formations withdrew in good order at starts 250 to 449 and the battle ended **SideEliminated**, side 0 reported eliminated and side 1 the decisive winner (+150,000 for its army, decrease 0); at 500 and 800 the formations fought (`probe-R201.txt`) |
| probe R201b (`Fix2ProbeR201b.cs.txt`) | the AutoResolver placed symmetrically and one column off, each defender role; the TacticalResolver under the knowledge-only AI | symmetric: BothEliminated with the defender the winner and no damage dealt (side 1 defending: starts 250, 400 and 449; side 0 defending: 250; no defender: no winner, 250); one column off: SideEliminated side 0, side 1 decisive whichever side defends; knowledge-only AI: 250 → a mutual rout at the start (DA6); 500 at column 40 → a FOUGHT mutual rout (3 killed a side, damage 330 and 270, end 200; DA6); 500 and 800 at column 41 → SideEliminated after fighting |

The probe files were created in `Proto.M7.Tests/Morale/` as `Fix2ProbeR201.cs`, `Fix2ProbeR201b.cs` and `Fix2ProbeR202.cs` and deleted by
exact name; their sources are kept in the scratchpad `mor2-fix2/`. `git status` was clean after each deletion.

### 14.3 What changed

- **Code:** none in `Proto.M7`. No coefficient, default or rule changed; the in-battle part stays 0 and the settlement term 50,000.
- **Tests (2 files):** `Morale/EntryCostRound2Tests.cs` (new: 5 methods, 14 cases); `Morale/EntryCostRound1Tests.cs` (the by-hand
  `ProposalA_AMutualRoutWonUnderDA6_…` comment marked and corrected in place: one fought case, not the general consequence; assertions
  unchanged). No test of another lane changed.
- **Docs:** `docs/m7-plan/morale-proposal.md` (§11.2 item 1, §11.8, §11.9, §11.10, §11.11 item 10); this report (§0, §2, §11, §13.1,
  §13.4, §13.6, §13.9, this section).

### 14.4 Tests added (MEASURED: all pass in Debug and in Release)

| Test | What it pins |
|---|---|
| `ProposalA_AnUnfoughtMutualRout_WonUnderDA6_TheDefendersArmyNetsTheRiseLessTheCost_FromAnyStart` (8) | MOR2-R2-01, TacticalResolver, passive commanders, starts 250 / 299 / 320 / 500 / 800 (part off) and 320 / 349 / 500 (part on): BothEliminated, DA6 winner 1, HoldsField −1; on each side 0 killed, 0 damage dealt, 20 routed-escaped, EnteredAtMs StartMs, the charge; the route observed: below the threshold at the start instant (no verdict; end = start − charge &lt; 300) or the inactivity rule (two Routed verdicts at pulse 4, idle 5, no exemption; end = start − charge + 5 × HoldRecovery, by hand); the defender's settlement entry 50,000, decrease 0, rise 200,000, after = s × 1,000 + 150,000; the attacker's s × 1,000 − 50,000; lane W's split of the defender's 2,000: deserted 2,000 × (1,000 − end) ÷ 1,000 |
| `ProposalA_AnUnfoughtMutualWithdrawal_InTheAutoResolver_WonUnderDA6_NetsTheRiseLessTheCost` (3) | the same in the AutoResolver (DA6-R): mirrored placement, starts 250 / 449 (off) and 480 (on, charged to 430): BothEliminated, DA6 winner 1; on each side 0 killed, 0 damage, 20 escaped in good order (none routed); the defender's army +150,000 |
| `ProposalA_AFoughtMutualRout_WonUnderDA6_DR29OutweighsTheRise` | the fought case: knowledge-only AI, start 500: 3 killed a side, damage dealt by both, both rout at 200, DA6 winner 1; the defender's decrease 300,000, rise 200,000, entry 50,000, after 350,000 |
| `SeveralDomainFormations_EachAircraftFormationEntersAtItsOwnFirstTakeOff_EveryVesselFormationAtTheStart` | MOR2-R2-02, lane N's amphibious battle: two vessel formations (10, 11) and three aircraft formations (13, 14, 24); observed at every completed instant: each aircraft formation's first take-off (13 and 14 at StartMs, 24 later) and the riflemen 12's first entity ashore (later still); every formation's EnteredAtMs, in the report and the accessor, equals its own observed instant (vessels StartMs, land on the ground StartMs); nothing charged |
| `TwoAircraftFormationsOfOneSide_OnlyTheOneThatTakesOffEnters_AtItsOwnTakeOff_TurnExact` | MOR2-R2-02, TURN-EXACT: fighter formation 3 kept at base, formation 4 launched at pulse 0 step 5: 4's airframe leaves its base at exactly StartMs + 5 StepMs; 4's EnteredAtMs null at the completed instant before, that instant at it; 3 never leaves its base and never enters |

Counts: `M7Proto.slnx` **1488 → 1502** (14 new cases).

- **Teeth and aim (ADR-015 §7.2).** The MOR2-R2-01 tests pin what the prototype does under PROPOSAL (a) — the consequence the proposal
  now states for the Director — not that (a) is right. Their oracle is the settlement's terms by hand (entry 50,000; decrease 0, or
  (500 − 200) × 1,000; rise ⌊400,000 × 2,000 ÷ 4,000⌋) and, for the route of each rout, observations from outside the settlement (the
  inactivity verdicts, the end instant, the end morale by hand). The MOR2-R2-02 tests pin READING MOR2-R2 (a) with an oracle observed
  from outside the rule (each airframe's phase, each land entity's state).

### 14.5 What ran (MEASURED; host and flags as in §9)

- Baseline on `1efda25` (§14.2).
- Closing, on the code at `7ec133e` (no code changed after it; the later commits are docs): `dotnet build M7Proto.slnx -c Debug -m:1
  -nodeReuse:false --no-incremental` 0 warnings, 0 errors (8 s); `dotnet test M7Proto.slnx -c Debug --no-build` **1502 passed, 0
  failed, 0 skipped** (42 s); the same in Release: 0 warnings, 0 errors (8 s), **1502 passed, 0 failed, 0 skipped** (18 s).
- `bash prototypes/m7-sprint1/scripts/check-proto-determinism.sh`: **DETERMINISM SCAN: CLEAN** (108 files, 0 pattern groups with
  findings).
- `python3 scripts/check-doc-links.py --all`: **467 files checked, 0 broken links**; on the proposal and this report by name: 2 files,
  0 broken links.
- The annex check (round 1's `mor2-fix1/tools/annex_check2.py`, run unchanged against `check.json`): 175 id–verdict pairs, 0 mismatches,
  0 unpaired (Annex A is untouched this round).
- Lane P's tuning battery: **NOT RUN** — neither resolver changed this round (no production code changed).

### 14.6 Fix-round mutants (final code `7ec133e`; filter run of 279 tests bounded at 100 s ≈ 10 × its clean 9.7 s; a survivor of the filter would re-run the full Debug suite bounded at 450 s = 10 × 45 s; every kill by a named semantic test)

Harness `mor2-fix2/tools/mut2.py` (this round's own): one exact-string replacement that must occur exactly once, a Debug build, the
filter run (`Morale`, `LaneD`, `Concealment.EqualSidesTests`, `MultiDomain.TransportTests`, `MultiDomain.AirTests`); the file restored
byte for byte after each mutant and the restored tree rebuilt (`git status` afterwards: no source change; only this round's then uncommitted edit of the proposal). Specification
`mor2-fix2/mut/fix2-spec.json`, results `fix2-results.json`. **7 mutants, 7 KILLED in the filter run (9.8–12.7 s each), none survived,
none hung.**

| Id | Mutation | Killed by (failing cases) |
|---|---|---|
| V2-06 | verifier's: a take-off recorded on the first domain formation | `SeveralDomainFormations_…`, `TwoAircraftFormationsOfOneSide_…` (2) |
| V2-13 | verifier's: only the first domain formation enters at the start | `SeveralDomainFormations_…` (1) |
| R2-M01 | a take-off recorded on the last domain formation | `SeveralDomainFormations_…` (1) |
| R2-M02 | every domain formation but the first enters at the start | `SeveralDomainFormations_…`, `Tactical_AFormationThatNeverLeavesItsTransport_…`, `NeverEntered_TroopsKilledAboard…` (3) |
| R2-M03 | (b′) as round 1 encoded it (R1-M12: no rise for a winner left with nobody fit or wounded; not adopted) | `ProposalA_AnUnfoughtMutualRout_…` (8), `ProposalA_AMutualRoutWonUnderDA6_…` (3), `DA1_AMutualEliminationWin_…` (1); **not** `…_AnUnfoughtMutualWithdrawal_InTheAutoResolver_…` (those men withdrew in good order) **nor** `ProposalA_AFoughtMutualRout_…` (lane W files a wounded man who fled as Wounded, which this encoding counts): the measurement quoted in proposal §11.8 |
| R2-M04 | DR29's net loss not floored at 0 (an in-battle gain carried) | `ProposalA_AnUnfoughtMutualRout_…` (4: the inactivity cases, whose end is above the start), `Morale_ABattleLowersCampaignMoraleByItsNetLossOnly` (2), `DB1_TheCostReachesTheCampaignExactly_…` (4), `Morale_TheInBattleGainIsNeverCarried_…` (2), `Equations_ByHand_DecreaseRiseOneClampAndTheBattleStart` (1); 13 failing |
| R2-M05 | DR29 measured from the start less the in-battle charge for a losing army only (a winner's charge counted twice) | `ProposalA_AnUnfoughtMutualRout_…` (2: 320 and 349 charged), `…_AnUnfoughtMutualWithdrawal_InTheAutoResolver_…` (1: 480 charged) |

- **Teeth and aim (ADR-015 §7.2).** V2-06, V2-13, R2-M01 and R2-M02 are killed by tests asserting READING MOR2-R2 (a) — each domain
  formation's own entry instant, observed from outside the rule. R2-M04 and R2-M05 are killed by tests asserting DR29's decrease as
  lane D and lane MOR2 define it (the net loss floored at 0, D-R2-01; measured from the start less the in-battle charge for every army,
  counted once, DB1-R3 (a)). R2-M03 is the not-adopted alternative (b′): its kill shows the prototype applies (a), not that (b′) is
  wrong, and its two passes are the measurement the proposal quotes.

### 14.7 Commits, line counts

Commits on `lane/MOR2` this round, oldest first: `7ec133e` (tests), `689cbe3` (proposal and report corrections in place), and this
section's commit (its id is in the lane's structured result). Nothing pushed and nothing merged (the Director rules on acceptance).

Line counts, `git diff --numstat 1efda25..689cbe3` (MEASURED; this section is not counted): 4 files, 377 lines added and 15 removed.

| File | + | − |
|---|---|---|
| `docs/m7-plan/morale-proposal.md` | 79 | 6 |
| `prototypes/m7-sprint1/Proto.M7.Tests/Morale/EntryCostRound1Tests.cs` | 9 | 1 |
| `prototypes/m7-sprint1/Proto.M7.Tests/Morale/EntryCostRound2Tests.cs` (new) | 278 | 0 |
| `prototypes/m7-sprint1/reports/H-MOR2.md` | 11 | 8 |

Every path is under `prototypes/m7-sprint1/` or `docs/m7-plan/`: nothing in `Sim.*`, `Sim.Data`, CI or any other lane's report; no unit
data or cap changed; no new project.

### 14.8 NOT RUN, deferred, limitations, for the Director

- **NOT RUN:** `M7Econ.slnx`, `M7DetMath.slnx` (no file of theirs changed; neither references `Proto.M7`); `scripts/validate.sh` and
  every `Sim.*` suite (no `Sim.*` change; D-079 Q5); lane P's tuning and held-out batteries (no resolver or production code changed this
  round; D-079 Q4 OPEN); a Windows run; `Proto.M7.Bench` timing modes (no performance claim); an independent verification of this round
  (ADR-015 §6: the next round).
- **Not pinned as a test, on purpose:** the AutoResolver reporting a decisive winner for a mutual withdrawal by which side leaves the
  field first (MEASURED by probes R201 and R201b, one column off symmetry; the exit order as the cause INFERRED from the geometry) — a
  property of lane P's model, not one the system ought to have; for lane P.
- **Not implemented:** (c′) and (c) (proposal §11.8): each is an exception to DA1 (DECIDED), the Director's to make; (b) and (b′) as
  before.
- **Deferred:** as §13.8.
- **Limitations:** the MOR2-R2-01 tests use one geometry (20 heavy infantry a side; 34 cells apart in the TacticalResolver, mirrored in
  the AutoResolver) and one seed each; lane W's collapse categories, which the stand-in settles from, cannot tell a wounded man who fled
  from one who held (proposal §11.8, R2-M03).
- **For the Director:** no new question; nothing reserved for the Director is decided here. The corrected observation (it replaces
  §13.9 (1)): under PROPOSAL (a), as the decided DA1 and the applied DA1-R (a) and DA6 make it, a defender whose formations ran, with no
  blow struck, in the same instant as the attacker's gains +150,000 against an equal force from ANY start (up to the top clamp), in
  both resolvers — through lane TR's inactivity rule (at any morale), a start below the rout threshold, or, with the in-battle part on,
  DB1's entry charge itself — while in the TacticalResolver lane W's collapse deserts part of its routed men (800 of 2,000 from a start
  of 500). If the Director wants such an unfought equal-sides win to raise nothing, (c′) is the targeted exception (proposal §11.8);
  answering Q-AP4 (b) would remove the TacticalResolver's case only. For lane P: the AutoResolver's mutual withdrawal by exit order
  (above).
