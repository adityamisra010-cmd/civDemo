# H-MOR — M7 lane MOR: two-level morale; victory raises morale (D-082 DA1, DA2)

Date 2026-11-10. Clone `/home/user/m7w/MOR`, branch `lane/MOR`, base `c536556` (the development branch
`claude/new-session-yavghm` as handed to this lane). No clone or checkpoint existed for lane MOR at the start: the lane was done from
scratch on a fresh clone (`git clone --shared /home/user/civDemo`). ISOLATED PROTOTYPE AND DOCS ONLY: nothing in `Sim.Core`,
`Sim.Data`, `Sim.Cli`, `Sim.Tests` or `Sim.Ui` changed (D-079 Q5 OPEN); no unit data or cap changed; no permanent project; no CI file.

Status words: **MEASURED** (run in this lane, on this tree; the test or command is named), **DERIVED** (exact arithmetic, pinned by
a test), **INFERRED** (argued, not isolated), **PROPOSAL** (lane MOR's design under D-082 §5's delegation; decided by nobody),
**READING** (an interpretation, both alternatives stated), **DD** (a quoted Director decision). Research labels: **[CODE]**,
**[SEARCH]**, **[KNOW]** (design document, Annex A).

*Verification round 1 (2026-11-10) is §10; statements it superseded are marked in place (~~struck~~ with a bold note).*
*Verification round 2 (2026-11-10) is §11; statements it superseded are marked in place the same way.*

Authority: D-082 DA1, DA1-R (a), DA2, DA6, DA6-R, DA7, DA7-R (a), §3 (lane D's rule superseded for the winning side; READING M
narrowed), §5 (the morale model and every number delegated); D-079 DR19, DR29, DR34, DR53, §4 D8, D11; `CLAUDE.md` Laws 1–5;
D-069. Nothing reserved for the Director is decided here.

---

## 0. Summary

| Brief item | State | Evidence |
|---|---|---|
| (1) Research (DA2), two levels, labelled, genre comparison | **Done, with a stated limit:** ~~every direct page fetch was refused by the session's network policy (§1)~~ **[round 1, MOR-V1-07: no direct page fetch succeeded; only `r2encv2.totalwar.com` is evidenced as a policy refusal, five hosts failed in the fetch tool for an unestablished cause, and a refused GitHub listing was not reported (§1, §10)]**, so the evidence is search summaries [SEARCH] and a public modding schema (table names only); no game file was read ([CODE]: none) | design doc Annex A; §1 |
| (2) Design PROPOSAL `docs/m7-plan/morale-proposal.md` | **Done**: (a) unit morale in battle (keep the model; add "the fight it won"; five later mechanisms with prerequisites); (b) army morale (DR29 decrease + DA1 rise; no extra defeat penalty, PROPOSAL (a)); (c) the two levels' feeds; (d) what a win is (the reported winner; DA6/DA7 via lane AP); (e) Laws 2, 3, 5; (f) READING M (b) presented, OPEN | the document |
| (3) Prototype | **Done.** Lane D's "a battle never raises the campaign morale" replaced by the DA1 rule for the winning side (DR29's decrease kept for every participant); the campaign baseline mechanism in lane E's pulse end (default = lane E exactly); "the fight it won" in **both** resolvers through one shared function, **coefficient 0 by default** (exact identity; its effect at 200 MEASURED on lane P's tuning battery) | §2, §3, §4 |
| Tests | 1235 → **1279** passed, 0 failed, 0 skipped in Debug and in Release (§6); one lane D test renamed and extended where DA1 supersedes it; no golden moved. **[Round 1: 1279 → 1304 passed, 0 failed, 0 skipped in Debug and in Release; 25 cases added (12 tests), 2 tests changed; no golden moved (§10)]** **[Round 2: 1304 → 1319 passed, 0 failed, 0 skipped in Debug and in Release; 15 cases added (6 tests); two round-1 test comments corrected; no golden moved (§11)]** | §4, §6, §10, §11 |
| Mutants | 24 on the final tree, bounded at 10× the clean run: 23 killed by named semantic tests, ~~1 equivalent survivor (M24)~~ **[round 1, MOR-V1-06: M24 is not equivalent — with the rule on it changes the AutoResolver's reported StateHash (MEASURED) — and is now killed; 11 verifier mutants that survived the suite are killed too (§10)]** **[Round 2: the verifier's B1 and W2–W6, which survived the suite (reproduced), are killed; 16 fix-round mutants on the final tree: 15 killed by named semantic tests, 1 survivor (G3, a zero-damage crew-served hit recorded), not claimed equivalent (§11.6)]** | §5, §10, §11 |
| (4) This report | Done | — |

---

## 1. Research provenance (brief item 1)

- **Hosts.** ~~Direct fetches refused by the session's network policy:~~ **[round 1, MOR-V1-07: "refused by the session's network
  policy" is evidenced for `r2encv2.totalwar.com` only (the proxy status's `connect_rejected`, the one research host in its failure
  list); for the five hosts in parentheses the fetch tool reported it could not resolve them, cause not established (INFERRED: a
  fetch-tool failure); also tried once and refused, not listed before: a GitHub listing of `Frodo45127/rpfm-schemas` ("GitHub access
  to this repository is not enabled for this session", scratchpad `mor/research/rpfm-schemas-listing.json`) (§10)]** Direct fetches
  tried: `primagames.com`, `www.pcinvasion.com`,
  `www.gamepressure.com`, `wiki.totalwar.com`, `www.iskmogul.com` (the fetch tool could not resolve them) and `r2encv2.totalwar.com`
  (proxy CONNECT 403, recorded by the proxy status as `connect_rejected`). Not retried: `totalwar.fandom.com`, `gamepur.com` (known
  denied). Each host was tried once. Reachable: web search; `raw.githubusercontent.com` (one file).
- **[CODE]: none.** No game file or script was read. The one file read is the MIT-licensed Rusted PackFile Manager schema of the
  game's database (`schema_troy.ron`, 6,752,098 bytes, SHA-256 `8332875a61825d0c67516ccbd155ab9c38c52f628e1ab664f91074cfbffa45e5`):
  table and field names (e.g. `_kv_morale_tables`, `campaign_group_morale_effects_tables`, `battle_result_types_tables.unit_xp_gain`,
  `land_units_tables.morale`), no values. Labelled [SEARCH].
- **What was found** (design doc Annex A.2–A.5, every row with its URL): at the army level the reference has **no morale number
  outside battle**, only modifiers (titles, items, faction resources, hero motivation) that enter the battle, and battle results
  return as experience, loot and resources; at the unit level each unit has a hidden morale that losses, flanking and a commander's
  death lower, that beating an enemy unit raises ("a scaling morale buff"), that ends in a rout, from which units can rally and
  shatter. Genre patterns: a party morale raised by victories and lowered by hunger, wages and defeats that sets each soldier's
  starting morale; an army morale stock that recovers while resting; discrete morale states changed by event-triggered checks.
- **Not verified:** the reference's morale states and thresholds, its tooltip factor list, its ambush penalty, and whether any
  battle result changes a persistent army-wide morale (not found). Nothing of the reference's names, numbers or text is used (D-069).

---

## 2. What exists (files)

| File | Role |
|---|---|
| `docs/m7-plan/morale-proposal.md` (new) | the PROPOSAL with the research annex |
| `docs/m7-plan/support-capabilities-proposal.md` (lane D's; **a dated note appended**, text above unchanged) | §3.3's never-raises rule superseded for the winning side |
| `Proto.M7/Morale/CampaignMorale.cs` (new) | `MoraleRates` (VictoryRiseMaxPpm 400,000, PROPOSAL); `CampaignMorale` (DR29 decrease, DA1 rise, one clamp, campaign → battle start); `BattleWinner.Reported` |
| `Proto.M7/Morale/MoraleCodec.cs` (new) | `MoraleSettlementRow` (93 bytes) and its codec (version 1) |
| `Proto.M7/Support/SupportWorld.cs`, `SupportTypes.cs`, `SupportMath.cs` (**lane D's files, edited**, [MOR] comments) | `OnBattle` settles DR29 + DA1; `SupportBattle` gains `WinnerSide` (−1 default), `BeatenPersons`, `WinnerSidePersons`; the morale rows in the state hash; superseded statements marked in place |
| `Proto.M7/WarEcon/WarEconomyWorld.cs`, `WarEconTables.cs` (**lane E's files, edited**, [MOR]) | the pulse-end update toward a baseline (`MoraleBaselinePerMille`, default 1,000 = lane E exactly; `MoraleElationDecayPerYear` 600, PROPOSAL); range checks |
| `Proto.M7/Tactical/LocalWinMorale.cs` (new) | "the fight it won": `Rise`, `Fought`, `Apply` — the one implementation both resolvers call |
| `Proto.M7/Tactical/BattleSetup.cs`, `TacticalBattle.cs`, `AutoResolver.cs` (**shared resolver files, edited**, [MOR]) | `TacticalRules.LocalWinMoraleRise` (default 0); hit tracking and the call after each instant's or step's routs, only when the coefficient is above 0; the rise log; state hashes append only when on **[round 2, MOR-V2-03: also `TacticalBattle.DomainCombat.cs` (lane N's file, one [MOR r2] line): a land formation's crew-served hits are recorded too (§11)]** |
| `Proto.M7.Bench/MoraleBench.cs` (new), `Program.cs` (one dispatch line) | `morale-parity`: the rule off and on, both resolvers, lane P's tuning battery, rule P1 per scenario |
| `Proto.M7.Tests/Morale/CampaignMoraleTests.cs`, `LocalWinMoraleTests.cs` (new) | §4 |
| `Proto.M7.Tests/LaneD/SupportCapabilityTests.cs` (**lane D's test file, edited**) | one theory renamed and extended (§4.3) |

`git diff --numstat c536556..HEAD` (MEASURED on the final tree): see §8.

---

## 3. The design in equations (PROPOSAL; the document has the reasoning)

**Campaign (army) level**, at a battle's end instant, for every army that took part:

  decrease = ⌊max(0, start − end − restore) × 1,000,000 ÷ MoraleMax⌋  (DR29; lane D's net loss, unchanged)
  rise = (army's side = reported winner) ? ⌊400,000 × B ÷ (B + W)⌋ : 0  (DA1; B beaten persons, W winning side's persons)
  after = clamp(before − decrease + rise, 0, 1,000,000)

then at each pulse end (lane E, w = the exact pulse length): at or below the baseline B₀, recovery +600 ‰/yr × w up to B₀; above
B₀, decay −600 ‰/yr × w toward B₀ and no recovery; hunger and arrears penalties as before. B₀ = 1,000 by default (lane E's model
exactly); B₀ = 800 is option (b), recommended for M7.14 with a re-based desertion term. The loser takes no extra defeat penalty
(PROPOSAL (a); (b) a mirror term).

**Battle (formation) level**, "the fight it won", both resolvers: when enemy land formation G breaks (in the fight before the
instant's casualties, not after), each other-side formation F still in the fight that dealt G damage within the last pulse length
gains ⌊c × Start(G) ÷ (Start(G) + Start(F))⌋, summed per F and capped at MoraleMax once; c = 0 by default, 200 PROPOSED when the
parity battery is next re-measured.

**What a win is:** the side the battle report names (`BattleWinner.Reported`): SideEliminated or SideDisengaged → the other side;
BothEliminated, BothDisengaged, Standoff → none today. DA6 (defender) and DA7-R (a) (ambusher) arrive as reported winners when lane
AP's resolvers report them; the stand-in passes them now.

**Readings:** DA1-R (a) is applied as the record states (the rise goes to the winning side's army; in a one-stock-per-army model
the army is what holds the participating formations' morale). READING M (b) is OPEN and presented, not decided (proposal §8).
No new reading is introduced; the aggregation over formations (proposal §4.5) is design, not a reading.

---

## 4. Tests (MEASURED; all pass in Debug and Release)

### 4.1 Campaign level — `Proto.M7.Tests/Morale/CampaignMoraleTests.cs` (30 cases)

| Test | What it pins |
|---|---|
| `Equations_ByHand_DecreaseRiseOneClampAndTheBattleStart` | every equation with literal numbers (decrease 160,000 / 0 / 333,333; rise 200,000 / 266,666 / 133,333 / 0; one clamp: 100,000 − 300,000 + 150,000 → 0, not 150,000; start 800 / 999 / 249) and the refusals |
| `BattleWinner_IsTheSideTheReportNames` | the reported winner for every end reason |
| `DA1_TheWinnersArmyRises_TheLosersFalls_ByHand` (5) | **the winner rises, the loser falls**: 900,000 / 966,666 / 833,333 / 1,000,000 / 1,000,000 (clamped) and the loser 250,000, by hand; the settlement rows |
| `DA1_EqualSides_TheDefenderTheReportNames_Rises_ThoughNeitherSideHeldTheField` | **a battle won on equal sides raises the defender's morale**: equal losses (both end at 750), neither side holding the field; the defender 800,000 → 950,000, the attacker → 750,000 |
| `DA1_AnAmbushBetweenEqualSides_TheAmbusherTheReportNames_Rises_NotTheDefender` | **an ambush win raises the ambusher's morale**: equal strengths and losses; the ambusher 800,000 → 950,000, the defender → 750,000 |
| `DA1_NoWinnerReported_BothFallByTheirLossOnly` | no rise without a reported winner |
| `DA1_TheHandOffRefusesABadWinnerOrStrength` | winner outside −1..1, negative strengths refused |
| `DA1_WithoutASideStrength_TheWinnersSideIsTheArmysOwnMenAtTheBattle` | the one-army default W = the army's men at the battle |
| `DA1_TheRiseAppliesAtTheBattlesEndInstant_NotBefore` | **TURN-EXACT**: 800,000 at end − 1 ms, 900,000 at the end instant |
| `DA1_ABattleAtThePulseEndInstant_IsSettledAfterThatPulsesUpdate` (6) | **TURN-EXACT**: a battle at a pulse-end instant settles after that pulse's update, one 1 ms earlier before it; baseline 800: 988,334 vs 1,000,000; at the default the orders differ only through the cap (944,999 both for a smaller win) |
| `Baseline_AnExcessDecaysTowardIt_OverTheExactPulseLength_ClosedForm` | Law 2: the excess decays pulse by pulse by min(excess, ⌊600,000 × w ÷ year⌋) back to 800,000 and stays |
| `Baseline_BelowIt_TheRecoveryStopsAtTheBaseline` (2) | recovery stops at 800,000 (option (b)); at the default it ramps to 1,000,000 (lane E's ramp) |
| `Baseline_TheDecayFollowsThePulseLength` (2) | **Law 3**: 23,333 over a 14-day pulse, 11,666 over a 7-day one |
| `Baseline_OffTheScale_IsRefused` | baseline outside 0..1,000 or a negative decay refused |
| `ManyArmiesBattlesAtOneInstant_SettleInArmyIdOrder_WhateverTheSchedulingOrder` | **TIE-DENSE**: four armies' battles at one instant, six scheduling permutations: army-id order, the same results, the same state hash |
| `TwoBattlesOfOneArmyAtOneInstant_SettleInTheOrderTheyWereScheduled` (2) | **TIE-DENSE**: one army's two battles at one instant settle in scheduling (sequence) order: 600,000 or 700,000, by hand |
| `MoraleRows_PopulatedTable_RoundTripBitExact_WithExactLength` | **POPULATED TABLE**: 5 rows from a real run (winners, losers, no winner, a medical restore): 5 + 93 × 5 bytes, bit-exact round trip, the same bytes and SHA-256 again, equal to the world's encoded table, every same-typed field pair told apart (lane D's guard), a truncated or wrong-version table refused |
| `TheStateHash_CarriesTheMoraleTable` | identical runs give equal hashes; a different winner a different hash **[round 1, MOR-V1-05: this did not show that the morale table is in the hash — a different winner also changes lane D's hashed battle row, and the test passed with the morale table left out of the hash (MEASURED); rewritten: two runs that differ only in a field the morale table alone records (§10)]** |

### 4.2 Battle level — `Proto.M7.Tests/Morale/LocalWinMoraleTests.cs` (14 cases)

| Test | What it pins |
|---|---|
| `Rise_ByHand_TheLargerTheBeatenFormationAgainstOnesOwn` | 100 / 50 / 133 / 57 / 0 / 0, the refusals |
| `Fought_TheWindowIsOnePulseInclusive` | **TURN-EXACT** window: exactly one pulse counts, 1 ms (one step) more does not; future and never-hit do not. **[Round 1, MOR-V1-01: this pins the pure function only; the window each resolver passes and its unit were untested at the resolver level (a window of 0, of 1,000 pulses or in the wrong unit survived the suite: MEASURED); resolver-level pins added in both resolvers (§10)]** **[Round 2, MOR-V2-02: the round-1 resolver pins held the window only to a range (8,000–107,999 ms; 2–50 steps: MEASURED), not to this boundary; the boundary is pinned at the resolver level in round 2 (§11)]** |
| `Apply_TieDense_SimultaneousBreaks_SumAndCapOnce_WhateverTheOrder` | **TIE-DENSE**: two enemy formations break at once: the winner's rises add and cap once; a formation broken at the same instant gains nothing; friendly breaks and a hit 1 ms outside the window give nothing; identical for all 720 orders of six formations |
| `Apply_OffOrNoBreak_ChangesNothing` | coefficient 0 or no break: nothing changes; a malformed table is refused |
| `Tactical_TheFormationThatBrokeItsEnemyRisesAtThatInstant_OthersDoNot` (3 seeds) | **TacticalResolver**, differential and turn-exact: the result equals the rule-off result in every field but the winner's end morale, +33 exactly; one rise row at the end instant; the formation that never fought unchanged |
| `Auto_TheFormationThatBrokeItsEnemyRisesAtThatStep_OthersDoNot` (3 seeds) | the same in the **AutoResolver** (step instants) |
| `BothResolvers_ArchersWhoseVolleysBrokeTheInfantry_RiseWithoutHavingBeenHit` (2 seeds) | both resolvers: the window is about the winner's blows (archers that took no damage rise by 28) |
| `Tactical_StateHash_CarriesTheRulesState_WhenOn` | with the rule on, the tactical state hash carries its hit table and rise log (on ≠ off before any break, on = on) |
| `Default_IsTheIdentity_InBothResolvers` | the default coefficient is 0: equal result and state hashes in both resolvers |

### 4.3 Moved pins and changed tests (with attribution)

- **Golden hashes: none moved** (MEASURED: every golden test of both resolvers, lane N, lane TR, lane P's subsets, lane E, lane S and
  lane W passes unchanged on the final tree; with the in-battle rule off, the tactical outcome digest of lane P's 1,600-case tuning
  battery is `90E58DCB…F65D6914`, the digest lane P recorded, `reports/H-P.md` §4).
- **One lane D test changed, where DA1 supersedes it:** `Morale_ABattleNeverRaisesCampaignMorale_ArchersHoldingWithoutLossEndAboveTheirStart`
  → `Morale_TheInBattleGainIsNeverCarried_OnlyDA1RaisesTheWinners_ArchersHoldingWithoutLossEndAboveTheirStart`. The archers in it
  won (the infantry broke; `BattleWinner.Reported` = 0), so "never raises" no longer holds for them. Attribution (MEASURED): the case
  with no reported winner keeps lane D's assertion unchanged (800,000 before and after, with and without medical staff); the case with
  the report's winner gives 800,000 + ⌊400,000 × B ÷ (B + 60)⌋ exactly (57,142 for 10 enemies, 100,000 for 20) — never 880,000 plus
  anything, so lane D's D-R2-01 point (the in-battle recovery's gain is not carried) stands. Every other lane D test is unchanged and
  passes (their stand-in battles report no winner, so DR29's decrease alone applies, as before).

---

## 5. Mutants (bounded; every kill by a named semantic test) and the in-battle rule switched on

### 5.1 Bound and harness

Bound: each mutant's test run is limited to 10× the clean run of the filter used, measured on the final tree (`Morale` + `LaneD` +
`WarEconE`: 209 tests, 6.8 s wall, so 68 s). A survivor of the filter is re-run against the full suite, bounded at 10× the clean full
Debug test step (46 s, so 460 s). Harness: apply one exact-string change, Debug build, run, restore the file by its saved content
(scratchpad `mor/mutants/run.py`). All 24 were run on the final tree (`9e92dfd`); M1–M22 had also been run once before the last two
test changes (the same 22 verdicts), and M23–M24 were added after a review of the untested statements.

### 5.2 Results

| # | Mutation (file) | Verdict (filter run: time) | Killed by (semantic tests; first three) |
|---|---|---|---|
| M1 | every army of a decided battle rises, the loser too (`SupportWorld.cs`) | KILLED (6.5 s) | `DA1_AnAmbushBetweenEqualSides_TheAmbusherTheReportNames_Rises_NotTheDefender`, `DA1_EqualSides_TheDefenderTheReportNames_Rises_ThoughNeitherSideHeldTheField`, `DA1_TheWinnersArmyRises_TheLosersFalls_ByHand` (+2 more) |
| M2 | the field instead of the reported winner (`SupportWorld.cs`) | KILLED (6.1 s) | `Baseline_AnExcessDecaysTowardIt_OverTheExactPulseLength_ClosedForm`, `Baseline_TheDecayFollowsThePulseLength`, `DA1_ABattleAtThePulseEndInstant_IsSettledAfterThatPulsesUpdate` (+9 more) |
| M3 | rise ratio inverted (own side over both) (`CampaignMorale.cs`) | KILLED (6.4 s) | `DA1_ABattleAtThePulseEndInstant_IsSettledAfterThatPulsesUpdate`, `DA1_TheWinnersArmyRises_TheLosersFalls_ByHand`, `DA1_WithoutASideStrength_TheWinnersSideIsTheArmysOwnMenAtTheBattle` (+3 more) |
| M4 | two clamps: the decrease floored before the rise (`CampaignMorale.cs`) | KILLED (7.3 s) | `Equations_ByHand_DecreaseRiseOneClampAndTheBattleStart` |
| M5 | the winner's DR29 decrease dropped (`SupportWorld.cs`) | KILLED (6.5 s) | `DA1_AnAmbushBetweenEqualSides_TheAmbusherTheReportNames_Rises_NotTheDefender`, `DA1_EqualSides_TheDefenderTheReportNames_Rises_ThoughNeitherSideHeldTheField`, `DA1_TheRiseAppliesAtTheBattlesEndInstant_NotBefore` (+2 more) |
| M6 | the one-army default (own men) not applied (`SupportWorld.cs`) | KILLED (6.5 s) | `DA1_WithoutASideStrength_TheWinnersSideIsTheArmysOwnMenAtTheBattle` |
| M7 | settlement row records the morale before as after (`SupportWorld.cs`) | KILLED (6.7 s) | `DA1_TheWinnersArmyRises_TheLosersFalls_ByHand`, `MoraleRows_PopulatedTable_RoundTripBitExact_WithExactLength`, `TwoBattlesOfOneArmyAtOneInstant_SettleInTheOrderTheyWereScheduled` |
| M8 | decay over a fixed week, not the pulse length (Law 3) (`WarEconomyWorld.cs`) | KILLED (6.6 s) | `Baseline_TheDecayFollowsThePulseLength` |
| M9 | recovery runs past the baseline (`WarEconomyWorld.cs`) | KILLED (6.4 s) | `Baseline_AnExcessDecaysTowardIt_OverTheExactPulseLength_ClosedForm`, `Baseline_BelowIt_TheRecoveryStopsAtTheBaseline` |
| M10 | no decay of an excess (a standing gain) (`WarEconomyWorld.cs`) | KILLED (6.0 s) | `Baseline_AnExcessDecaysTowardIt_OverTheExactPulseLength_ClosedForm`, `Baseline_TheDecayFollowsThePulseLength`, `DA1_ABattleAtThePulseEndInstant_IsSettledAfterThatPulsesUpdate` |
| M11 | a friendly break gives a rise (`LocalWinMorale.cs`) | KILLED (6.7 s) | `Apply_TieDense_SimultaneousBreaks_SumAndCapOnce_WhateverTheOrder` |
| M12 | a formation that broke at the same instant gains (`LocalWinMorale.cs`) | KILLED (6.5 s) | `Apply_TieDense_SimultaneousBreaks_SumAndCapOnce_WhateverTheOrder` |
| M13 | window exclusive (off by one) (`LocalWinMorale.cs`) | KILLED (6.6 s) | `Apply_TieDense_SimultaneousBreaks_SumAndCapOnce_WhateverTheOrder`, `Fought_TheWindowIsOnePulseInclusive` |
| M14 | tactical: who was hit recorded instead of who hit (`TacticalBattle.cs`) | KILLED (6.4 s) | `BothResolvers_ArchersWhoseVolleysBrokeTheInfantry_RiseWithoutHavingBeenHit` |
| M15 | ~~tactical: breaks judged before the instant's routs~~ **[round 1, MOR-V1-06: the edit sets `now` to the before set, so the rule never fires; relabelled "tactical: breaks never judged"; the true before-the-routs mutant (V13) is killed, §10]** (`TacticalBattle.cs`) | KILLED (6.8 s) | `BothResolvers_ArchersWhoseVolleysBrokeTheInfantry_RiseWithoutHavingBeenHit`, `Tactical_TheFormationThatBrokeItsEnemyRisesAtThatInstant_OthersDoNot` |
| M16 | auto: a volley from range not recorded (`AutoResolver.cs`) | KILLED (6.4 s) | `BothResolvers_ArchersWhoseVolleysBrokeTheInfantry_RiseWithoutHavingBeenHit` |
| M17 | auto: the rise's instant in pulses, not steps (`AutoResolver.cs`) | KILLED (6.4 s) | `Auto_TheFormationThatBrokeItsEnemyRisesAtThatStep_OthersDoNot`, `BothResolvers_ArchersWhoseVolleysBrokeTheInfantry_RiseWithoutHavingBeenHit` |
| M18 | ~~auto: breaks judged before the step's routs~~ **[round 1, MOR-V1-06: as M15, the rule never fires; relabelled "auto: breaks never judged"; the true before-the-routs mutant (V14) is killed, §10]** (`AutoResolver.cs`) | KILLED (6.8 s) | `Auto_TheFormationThatBrokeItsEnemyRisesAtThatStep_OthersDoNot`, `BothResolvers_ArchersWhoseVolleysBrokeTheInfantry_RiseWithoutHavingBeenHit` |
| M19 | a withdrawal (SideDisengaged) not counted as a win (`CampaignMorale.cs`) | KILLED (7.0 s) | `BattleWinner_IsTheSideTheReportNames` |
| M20 | codec: two same-typed fields swapped in the encoder (`MoraleCodec.cs`) | KILLED (6.4 s) | `MoraleRows_PopulatedTable_RoundTripBitExact_WithExactLength` |
| M21 | DR29 decrease ignores the medical restore (`CampaignMorale.cs`) | KILLED (6.5 s) | `Equations_ByHand_DecreaseRiseOneClampAndTheBattleStart`, `Morale_ABattleLowersCampaignMoraleByItsNetLossOnly`, `Morale_ACampaignArmyLeavesABattleAtTheTacticalEndMorale_MedicsRestorePart` (+1 more) |
| M22 | local-win ratio inverted (`LocalWinMorale.cs`) | KILLED (6.8 s) | `Apply_TieDense_SimultaneousBreaks_SumAndCapOnce_WhateverTheOrder`, `Auto_TheFormationThatBrokeItsEnemyRisesAtThatStep_OthersDoNot`, `BothResolvers_ArchersWhoseVolleysBrokeTheInfantry_RiseWithoutHavingBeenHit` (+2 more) |
| M23 | tactical: the rule's state left out of the state hash (`TacticalBattle.cs`) | KILLED (6.7 s) | `Tactical_StateHash_CarriesTheRulesState_WhenOn` |
| M24 | auto: the rule's state left out of the end state hash (`AutoResolver.cs`) | SURVIVED (6.3 s); full suite: SURVIVED (37.0 s, 1279 passed) | — |

Verdicts (MEASURED): **23 of 24 KILLED**, each by at least one named semantic test (by-hand values, the DA1 cases, the turn-exact
pins, the tie-dense table, the closed-form baseline tests, the populated table, the differential resolver tests); none by a golden
hash only. No mutant hung; every filter run stayed under the 68 s bound (the slowest 7.3 s); M24's full-suite run took 37.0 s against
the 460 s bound.

### 5.3 The survivor and the mutant not run

~~**M24 survives and is judged EQUIVALENT in effect**~~ **[Superseded in round 1 (MOR-V1-06): M24 is not equivalent. With the rule on it changes the AutoResolver's reported StateHash (MEASURED: `47ADF253…` → `1989D5C4…` for the lane's differential scenario, seed 1, result hash unchanged); it survived because no test pinned a rule-on AutoResolver state hash. It is now killed by `BothResolvers_TheStateHashCarriesTheRiseLog_WhenTheResultsAreEqual` (§10). The message of commit `fd48a26` ("1 equivalent survivor") states the superseded judgement as a fact; history is not rewritten, this note corrects it.]** The lane's original argument (ADR-015 §7.2: is the property one the system ought to have?): the AutoResolver
computes its state hash only at the battle's end, when the hit table is read by nothing any more, and every rise's effect is
already in the hashed end morale. The append is kept for symmetry with the tactical engine, whose mid-battle hash needs it (M23,
killed by `Tactical_StateHash_CarriesTheRulesState_WhenOn`).
Not run as a mutant: "capped once" against "capped at every rise" — the two are the same function for non-negative rises
(min(M, min(M, m + a) + b) = min(M, m + a + b)), so no test can tell them apart (DERIVED).

### 5.4 The in-battle rule switched on: lane P's tuning battery (MEASURED, `morale-parity 200 2`, Release)

Command: `dotnet Proto.M7.Bench/bin/Release/net10.0/Proto.M7.Bench.dll morale-parity 200 2` (lane P's 20 tuning scenarios × seeds
1–80 = 1,600 cases; tactical with the knowledge-only AI on both sides, auto model 2 defaults; the rule off and at c = 200). Output
verbatim (wall times are reporting only):

```
morale-parity: lane P TUNING battery, 1600 cases x (off, LocalWinMoraleRise = 200) x (tactical, auto model 2 defaults)
wall: tactical 145.3 s on 2 threads, auto 2.6 s single thread
digests (lane P's ParityPCsv.Digest): tactical off 90E58DCB18DEA369EB1E2464D09DF590482D7473FFB018B550C62FCBF65D6914, on 1E6D728D4A7FF41D09D9794A3AA1B1FCC667D06A67AF3B9165E57D918C76525D
                                      auto     off 2AC31207D6209E7FE1B4F7AE9119BE70144DB1E1F3988BC6AA609B14A0B68F31, on CAAEB267C406A7399C15E5A2C36CFF7F4E1D59158B2C158BCC448FC9ED37D6E7
rise rows over the battery: tactical off 0 / on 1819; auto off 0 / on 1779
cases whose outcome changed (any field): tactical 64, auto 31; whose winner changed: tactical 40, auto 5
rule P1 (tactical reference vs auto candidate, tuning seeds n = 80): off 16/20, on 16/20
```

- **Identity (MEASURED):** the tactical "off" digest `90E58DCB18DEA369EB1E2464D09DF590482D7473FFB018B550C62FCBF65D6914` equals the
  tuning digest lane P recorded (`reports/H-P.md` §4 and §7): the default-off rule leaves all 1,600 tactical battles unchanged.
- **Effect at c = 200 (MEASURED):** 1,819 rises (tactical) and 1,779 (auto); outcomes changed in 64 tactical and 31 auto cases
  (winner in 40 and 5), all in combined-arms and sniper-screen, the two scenarios with more than one formation on a side; elsewhere
  the last enemy formation's break ends the battle at that instant, so only the winner's end morale changes, which the outcome does
  not include. Rule P1 verdicts: 16/20 both off and on, the same scenarios failing for the same checks.
- **INFERRED:** that a small rule-on change of the distributions in the multi-formation scenarios (combined-arms win A/B/− 36.2 /
  50.0 / 13.7 → 37.5 / 47.5 / 15.0 %, n = 80) is the rule's real effect rather than noise is not established at n = 80; that
  40 tactical winners changed while the distribution barely moved suggests case-level sensitivity to the order of later routs.

| scenario | T off win A/B/- % | T on | Au off | Au on | casA T off/on | casB T off/on | casA Au off/on | casB Au off/on | dur s T off/on | dur s Au off/on | P1 off | P1 on |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| equal-infantry | 35.0/57.5/7.5 | 35.0/57.5/7.5 | 37.5/55.0/7.5 | 37.5/55.0/7.5 | 33.8/33.8 | 32.1/32.1 | 33.8/33.8 | 33.0/33.0 | 207.7/207.7 | 157.4/157.4 | PASS | PASS |
| numbers-2to1 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 17.8/17.8 | 39.0/39.0 | 18.1/18.1 | 36.0/36.0 | 157.5/157.5 | 159.0/159.0 | PASS | PASS |
| low-supply | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 37.9/37.9 | 12.1/12.1 | 36.4/36.4 | 16.7/16.7 | 177.1/177.1 | 158.9/158.9 | FAIL(casB) | FAIL(casB) |
| low-morale | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 15.9/15.9 | 13.6/13.6 | 15.2/15.2 | 15.4/15.4 | 152.9/152.9 | 145.8/145.8 | PASS | PASS |
| leadership-edge | 88.7/11.2/0.0 | 88.7/11.2/0.0 | 97.5/0.0/2.5 | 97.5/0.0/2.5 | 35.8/35.8 | 37.6/37.6 | 37.6/37.6 | 36.3/36.3 | 210.2/210.2 | 160.2/160.2 | PASS | PASS |
| experience-edge | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 20.0/20.0 | 38.3/38.3 | 17.7/17.7 | 37.7/37.7 | 164.8/164.8 | 150.4/150.4 | PASS | PASS |
| archers-vs-infantry-open | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 0.0/0.0 | 38.6/38.6 | 0.0/0.0 | 40.2/40.2 | 141.3/141.3 | 139.1/139.1 | PASS | PASS |
| combined-arms | 36.2/50.0/13.7 | 37.5/47.5/15.0 | 23.7/26.2/50.0 | 20.0/28.7/51.2 | 41.8/43.7 | 41.8/42.8 | 40.7/41.5 | 39.7/40.4 | 208.1/209.2 | 171.4/171.8 | FAIL(win,duration) | FAIL(win,duration) |
| riflemen-vs-infantry | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 0.0/0.0 | 37.9/37.9 | 0.0/0.0 | 37.9/37.9 | 110.7/110.7 | 113.1/113.1 | PASS | PASS |
| sniper-screen | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 27.0/27.5 | 37.0/37.3 | 27.4/27.4 | 35.9/36.2 | 172.6/175.8 | 179.9/197.5 | PASS | PASS |
| shock-infantry | 41.2/52.5/6.2 | 41.2/52.5/6.2 | 40.0/51.2/8.7 | 40.0/51.2/8.7 | 33.5/33.5 | 32.9/32.9 | 33.4/33.4 | 33.7/33.7 | 196.0/196.0 | 157.2/157.2 | PASS | PASS |
| archers-vs-infantry-forest | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 0.0/0.0 | 38.5/38.5 | 0.0/0.0 | 39.3/39.3 | 255.0/255.0 | 212.9/212.9 | FAIL(duration) | FAIL(duration) |
| jungle-ambush | 42.5/43.7/13.7 | 42.5/43.7/13.7 | 32.5/48.7/18.7 | 32.5/48.7/18.7 | 34.3/34.3 | 32.9/32.9 | 34.2/34.2 | 33.3/33.3 | 397.1/397.1 | 337.9/337.9 | PASS | PASS |
| hill-crest | 91.2/7.5/1.2 | 91.2/7.5/1.2 | 95.0/1.2/3.7 | 95.0/1.2/3.7 | 28.7/28.7 | 36.4/36.4 | 24.6/24.6 | 36.1/36.1 | 334.1/334.1 | 327.7/327.7 | FAIL(casA) | FAIL(casA) |
| ford-crossing | 23.7/63.7/12.5 | 23.7/63.7/12.5 | 45.0/47.5/7.5 | 45.0/47.5/7.5 | 35.4/35.4 | 31.4/31.4 | 33.3/33.3 | 32.7/32.7 | 333.1/333.1 | 328.9/328.9 | PASS | PASS |
| marsh-front | 47.5/42.5/10.0 | 47.5/42.5/10.0 | 35.0/58.7/6.2 | 35.0/58.7/6.2 | 33.3/33.3 | 32.6/32.6 | 33.6/33.6 | 31.8/31.8 | 457.2/457.2 | 452.0/452.0 | PASS | PASS |
| forest-hold-archers | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 0.1/0.1 | 36.3/36.3 | 0.0/0.0 | 37.7/37.7 | 313.9/313.9 | 275.4/275.4 | PASS | PASS |
| defile | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 11.1/11.1 | 37.1/37.1 | 13.4/13.4 | 37.3/37.3 | 281.4/281.4 | 275.7/275.7 | PASS | PASS |
| archers-on-hill | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 0.0/100.0/0.0 | 38.8/38.8 | 0.0/0.0 | 39.8/39.8 | 0.0/0.0 | 248.4/248.4 | 244.5/244.5 | PASS | PASS |
| hold-line-open | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 100.0/0.0/0.0 | 11.1/11.1 | 37.1/37.1 | 13.4/13.4 | 37.3/37.3 | 281.4/281.4 | 275.7/275.7 | PASS | PASS |

---

## 6. What ran, what was NOT RUN, what was deferred

**RAN (MEASURED; Linux x64 container, shared 4-CPU host, `nice -n 19`, `-m:1`, `-nodeReuse:false`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`,
always naming the solution):** *(the lane's runs; verification round 1's runs, on the round-1 tree, are in §10.5)*

| What | Command | Result |
|---|---|---|
| Baseline on `c536556` | `dotnet build M7Proto.slnx -c Debug` and `-c Release` with `--no-incremental`; `dotnet test M7Proto.slnx --no-build` | Debug **1235 / 0 / 0** (test step 39 s), Release **1235 / 0 / 0** (16 s); builds 0 warnings, 0 errors |
| Closing runs on the final code (`9e92dfd`; this report is not compiled) | the same commands | Debug **1279 / 0 / 0** (test step 43 s), Release **1279 / 0 / 0** (15 s); builds 0 warnings, 0 errors. An earlier closing pair, before the last test change, gave the same counts (46 s, 18 s) |
| Intermediate runs | full Debug suite after the first code commit (before the shared-rule refactor) and after the first test commit | 1235 / 0 / 0; 1276 / 0 / 0 |
| Determinism scan | `prototypes/m7-sprint1/scripts/check-proto-determinism.sh` | 105 files under `Proto.M7/` (102 + 3 new), **CLEAN** |
| Doc links | `python3 scripts/check-doc-links.py --all`; this report by name (`--all` does not cover `prototypes/`) | 463 files, 0 broken; 1 file, 0 broken |
| Parity effect of the in-battle rule | `Proto.M7.Bench.dll morale-parity 200 2` (Release) | §5.4 (tactical 145.3 s on 2 threads, auto 2.6 s) |
| Mutants | 24 (and the first 22 once before the last test changes), harness `mor/mutants/run.py` | §5 |
| Research | about 20 web searches; 6 ~~refused~~ fetches that returned no page **[round 1, MOR-V1-07: 1 evidenced as a policy refusal, 5 failed in the fetch tool, cause not established; and 1 refused GitHub listing not reported before]**; 1 file download (the schema) | §1, §10 |

The 44 new cases: 30 in `CampaignMoraleTests`, 14 in `LocalWinMoraleTests` (1235 + 44 = 1279); lane D's renamed theory keeps its
2 cases.

**NOT RUN, and why:**
- `M7Econ.slnx`: not touched (`Proto.M7.Econ` has no reference to `Proto.M7`; no economy-prototype file changed).
- `M7DetMath.slnx`: not touched.
- `scripts/validate.sh` and every `Sim.*` suite: no `Sim.*` change (D-079 Q5).
- Lane P's held-out acceptance battery and its control battery with the in-battle rule on: the acceptance battery is lane P's
  held-out record and the parity gate is OPEN (D-079 Q4); the switch-on is proposed for the next re-measurement (§7 item 4).
- `Proto.M7.Bench` timing modes (scheduler, tactical): no performance claim is made. With the rule off, the resolvers' added work
  is one null check per Damage event and per Resolve instant, and one per AutoResolver step (INFERRED negligible; not measured).
- A Windows run; an independent verifier (ADR-015 §6): the mutants above are this lane's own.

**Deferred (to whom):**
- switching "the fight it won" on at c = 200: the next parity re-measurement, after lane AP's AutoResolver change (D-079 Q4, D11);
- the baseline below the top (option (b)) with a re-based desertion term: M7.14's calibration (A-38);
- DA6 and DA7 winners reported by the resolvers: lane AP;
- the start-morale check, the aggregation over an army's formations, the settling of one army's same-instant battles together:
  the M7.15 hand-off;
- READING M (b): the Director (OPEN);
- rout contagion, rally and shatter, commander presence and loss, flank and rear, fatigue: M7.11 / M7.19, each after its
  prerequisite (proposal §3.3).

---

## 7. Limitations

1. The battle → campaign hand-off is lane D's stand-in: one army is one formation; strengths are passed, not derived; the start
   morale passed separately is not checked against the army's stock (M7.15).
2. No resolver reports a DA6 or DA7 winner yet (lane AP); the tests pass the winner such a report will name.
3. Recovery and decay run at pulse ends over the whole pulse (first order in the pulse length, as lane E's recovery already was).
4. "The fight it won" is off by default; its parity effect was measured on the tuning battery only; the held-out acceptance battery
   was not run (lane P's record; D-079 Q4 OPEN).
5. Option (b)'s baseline below the top and the desertion re-basing it needs are not calibrated (M7.14, A-38).
6. Land formations only for the in-battle rule (vessel and aircraft formations are outside it in both resolvers). **[Round 2,
   MOR-V2-03: the TacticalResolver also left out, unstated, the damage of a land formation's crew-served weapons; it now counts (§11)]**
7. Research: no web page could be fetched directly; search summaries and one downloaded schema of table names are the evidence (§1).
8. One army's battles at one instant settle one by one in scheduling order (pinned); M7.15 should settle them together.
9. Observation, not a finding of this lane: in a first version of the engine test (a 50-strong Defend line against 20 attackers),
   the TacticalResolver broke the line first and the AutoResolver broke the attackers (MEASURED for seeds 1–3, both resolvers) — a
   disagreement for lane P's battery; the test scenario was changed so it no longer depends on it.

---

## 8. Git, files, line counts

Branch `lane/MOR` in `/home/user/m7w/MOR`, base `c536556`. Commits (oldest first; every message carries only measured claims):
`556a9c6` code (DA1 hook, baseline, local-win rule; default 0) · `168c6db` tests and the bench mode · `1cbc710` the proposal ·
`1718267` the archers test · `4a23916` the dated note, proposal fixes, the state-hash test · `9e92dfd` the DA6 and DA7 tests with
equal losses · `fd48a26` this report. Verification round 1's commits are listed in §10.7.

`git diff --numstat c536556..9e92dfd` (MEASURED; added / removed lines; this report is not in it):

| File | + | − |
|---|---:|---:|
| `docs/m7-plan/morale-proposal.md` | 395 | 0 |
| `docs/m7-plan/support-capabilities-proposal.md` (dated note) | 12 | 0 |
| `Proto.M7/Morale/CampaignMorale.cs` | 102 | 0 |
| `Proto.M7/Morale/MoraleCodec.cs` | 64 | 0 |
| `Proto.M7/Tactical/LocalWinMorale.cs` | 73 | 0 |
| `Proto.M7/Tactical/TacticalBattle.cs` | 45 | 0 |
| `Proto.M7/Tactical/AutoResolver.cs` | 49 | 0 |
| `Proto.M7/Tactical/BattleSetup.cs` | 8 | 0 |
| `Proto.M7/Support/SupportWorld.cs` | 26 | 2 |
| `Proto.M7/Support/SupportTypes.cs` | 17 | 2 |
| `Proto.M7/Support/SupportMath.cs` | 6 | 0 |
| `Proto.M7/WarEcon/WarEconomyWorld.cs` | 16 | 2 |
| `Proto.M7/WarEcon/WarEconTables.cs` | 9 | 0 |
| `Proto.M7.Bench/MoraleBench.cs` | 112 | 0 |
| `Proto.M7.Bench/Program.cs` | 1 | 0 |
| `Proto.M7.Tests/Morale/CampaignMoraleTests.cs` | 486 | 0 |
| `Proto.M7.Tests/Morale/LocalWinMoraleTests.cs` | 284 | 0 |
| `Proto.M7.Tests/LaneD/SupportCapabilityTests.cs` | 22 | 11 |

Not changed: anything outside `prototypes/m7-sprint1` and `docs/m7-plan`; any `Sim.*` project; unit data; caps; CI; the G2 graphics
prototype. The edits to other lanes' files (lane D's support files and test, lane E's war economy, the shared resolver files) are
marked [MOR] in place; each is the identity at its default except lane D's after-battle rule, which DA1 supersedes for the winner.

Finish artefacts (orchestrator's scratchpad): `m7c/patches/MOR.patch` (`git diff c536556..HEAD`), `m7c/bundles/MOR.bundle`
(`c536556..HEAD`, verified), `m7c/reports/H-MOR.md` (this file).

---

## 9. For the Director

No new question. READING M (b) (whether taking part alone costs morale) stays OPEN, as D-082 left it; the design presents both
alternatives and their consequences (proposal §8): **(a)**, applied — the decrease is the casualty-driven loss (the battle's net
loss), so a battle won without loss raises the winner by DA1's rise alone and a battle lost without loss changes nothing; **(b)** — a
participation cost even without casualties (per battle or per engaged pulse), a new term in both resolvers or in the hand-off, which
moves the parity battery and the tactical goldens, and with DA1 makes a lossless win net rise − cost. Everything else — the size of the rise (400,000 × B ÷ (B + W)), no extra defeat
penalty, the baseline and its decay, "the fight it won" and its switch-on after the next parity re-measurement, the aggregation over
formations — is delegated design (D-082 §5), presented for review, not for decision.

---

## 10. Verification round 1 (2026-11-10)

Fix round on the verifier's findings MOR-V1-01..11 (verifier worktree pinned to `fd48a26`). Clone `/home/user/m7w/MOR`, branch
`lane/MOR`. Every finding was first reproduced in this lane's own clone at `fd48a26` with this round's own harness and probes (the
verifier's mutant edits copied into this round's own specification, scratchpad `mor-fix1/mut/`); a finding was fixed only once
reproduced. Statements this round superseded are marked in place above (§0, §1, §4.1, §4.2, §5.2, §5.3, §6, §8) and in the
proposal (§3.2, §6, Annex A.1).

### 10.1 Verdicts

| ID | Sev. | Verdict | Reproduction (MEASURED, `fd48a26`) | Fix | Pinned by (semantic tests) |
|---|---|---|---|---|---|
| MOR-V1-01 | major | **REPRODUCED** | V4, V4b, V5, V5b, V6 and V8 each survived the full Debug suite (1279 passed each; 38.9–44.6 s; bound 410 s) | tests only, no rule change: the window, its unit and the withdrawal exclusion pinned at the resolver level in both resolvers, on hits observed outside the rule's code (§10.4) **[round 2, MOR-V2-02: the window only to a range, not to its one-pulse boundary; the boundary is pinned in §11]** | `Tactical_TheWindowIsOnePulseOfTheWinnersOwnBlows_InMilliseconds`, `Auto_TheWindowIsOnePulseOfTheWinnersOwnBlows_InSteps`, `Tactical_AnOrderlyWithdrawalOffTheEdgeIsNotABreak_ARoutIs`, `Auto_AnOrderlyWithdrawalOffTheFieldIsNotABreak`, `Auto_AFormationWithdrawingInGoodOrderIsStillInTheFight_ItRisesWhenTheEnemyItStruckBreaks`, `Tactical_AFormationRetreatingInGoodOrderIsStillInTheFight_ItRisesWhenTheEnemyItStruckBreaks` |
| MOR-V1-02 | minor | **REPRODUCED** | V12 (the larger rise instead of the sum) survived (1279; 45.4 s) | test | `Apply_TieDense_TheRisesOfOneWinnerAddUp_BelowTheCap_WhateverTheOrder` |
| MOR-V1-03 | minor | **REPRODUCED** | V1 survived (1279; 41.6 s); probe: the losing army that held the field ends at 900,000 with a rise of 200,000 under V1, at 700,000 with no rise on the clean code | test | `DA1_ALoserThatHeldTheField_DoesNotRise_TheReportDecides` |
| MOR-V1-04 | minor | **REPRODUCED** | V2 and V3 survived (1279; 41.9 s, 52.3 s); probe (baseline 800, a win on day 3 to 1,000,000): an unfed army ends pulse 1 at 988,334 under V2, 941,668 clean; decay 1,200 ‰/yr: 988,334 under V3, 976,667 clean | tests | `Baseline_AboveIt_TheHungerPenaltyStillApplies_ByHand`, `Baseline_TheDecayAndTheRecoveryEachReadTheirOwnRate_ByHand` |
| MOR-V1-05 | minor | **REPRODUCED** | V9 survived (1279; 47.0 s); probe: two runs differing only in the loser's BeatenPersons give equal lane D tables, different morale tables, and state hashes that differ on the clean code and are equal under V9 | test rewritten | `TheStateHash_CarriesTheMoraleTable` |
| MOR-V1-06 | nit | **REPRODUCED** | M15's and M18's edit (lane `mor/mutants/final.json`) sets `now` to the before set, so the rule never fires; probe: with the rule on, M24 changes the AutoResolver's reported StateHash (`47ADF25313C9…` → `1989D5C4124A…`, result hash `3376885DD7E5…` unchanged; the lane's differential scenario, seed 1); commit `fd48a26`'s message states "1 equivalent survivor" | labels and the M24 judgement corrected in place (§0, §5.2, §5.3); a test now kills M24; the true before-the-routs mutants V13 and V14 re-run here: killed | `BothResolvers_TheStateHashCarriesTheRiseLog_WhenTheResultsAreEqual` |
| MOR-V1-07 | nit | **REPRODUCED** | proxy status (`$HTTPS_PROXY/__agentproxy/status`, `recentRelayFailures`, read 2026-11-10): the only research host is `r2encv2.totalwar.com:443`, `connect_rejected` 403 at 2026-11-10T10:05:00.817Z; the five other hosts are recorded by the lane itself as "the fetch tool could not resolve them"; `mor/research/rpfm-schemas-listing.json` holds "GitHub access to this repository is not enabled for this session" and was not reported; the schema file re-checked (6,752,098 bytes, SHA-256 `8332875a…cfbffa45e5`, the cited tables present) | §0, §1, §6 and proposal Annex A.1 corrected in place | — (record) |
| MOR-V1-08 | nit | **REFUTED** | under the DEFAULT inactivity model (lane TR's DIR5 hidden-stationary rule) the five-pulse rule does rout: `TacticalBattle.PulseEnd` returns into `PulseEndHiddenStationary`, which sets `f.Routed = true` and turns the formation's Active members to Routing (`TacticalBattle.cs:1129–1135`; the AutoResolver's model-2 port calls `Rout`); lane TR's `GenuinelyInactiveHiddenFormationRoutsAfterExactlyFivePulses` (RoutedEscaped 10, verdict Routed) and `MutualHiddenIdlenessRoutsBothSidesUnlessTheGuardIsOn` pass on this tree (MEASURED, 2 passed). The code the finding quotes is the legacy side-disengage branch (`InactivityModel.LegacySideDisengage`, selectable), which disengages. The proposal's sentence holds for the default model, and under the legacy rule no break happens either | **none** (a refuted finding gets no change) | — |
| MOR-V1-09 | nit | **REPRODUCED** | D-082's only statement on the field is DA6-R's PROPOSAL for the TacticalResolver's Standoff end ("the defender holds the field and wins, unless the attacker ambushed from concealment"); `morale-proposal.md:275–276` and `SupportTypes.cs:119` stated, unlabelled, that the DA6 defender wins "without holding the field alone" | the premise struck and corrected in place in both; the conclusion (the report's winner, never the field) kept | `DA1_ALoserThatHeldTheField_DoesNotRise_TheReportDecides` (the conclusion) |
| MOR-V1-10 | nit | **REPRODUCED** | probe: `MoraleCodec.Decode` of `[version 1][count int.MaxValue]` threw `System.OverflowException` | `Decode` bounds the count by the bytes present before computing ExpectedLength: `FormatException` | `MoraleCodec_RefusesAHugeOrNegativeCount_AsAFormatError` |
| MOR-V1-11 | nit | **REPRODUCED** | probe: `new SupportBattle(…) { WinnerSide = 0 }` without BeatenPersons was accepted and settled with RisePpm 0 (morale 800,000) | `SupportWorld.ScheduleBattle` refuses a reported winner (WinnerSide ≥ 0) without BeatenPersons, for the winner's and the loser's record; a battle that names no winner needs no strengths (lane D's battles unchanged) | `DA1_TheHandOffRefusesABadWinnerOrStrength` (extended) |

Summary: 10 REPRODUCED and fixed, 1 REFUTED (no change).

### 10.2 Reproduction runs (MEASURED, `fd48a26`, full Debug suite per mutant, bound 10 × the clean test step of 41 s = 410 s)

| Mutant (verifier's edit, copied) | What it does | Result | Test step |
|---|---|---|---|
| V1 | the DA1 rise also for a field holder of the losing side | SURVIVED, 1279 passed | 41.6 s |
| V2 | the hunger and arrears penalties bound to the below-baseline branch only | SURVIVED, 1279 | 41.9 s |
| V3 | the elation decay reads the recovery rate | SURVIVED, 1279 | 52.3 s |
| V4 | auto: the window passed in ms (60,000 "steps") | SURVIVED, 1279 | 39.6 s |
| V4b | auto: a window of 0 steps | SURVIVED, 1279 | 44.6 s |
| V5 | tactical: a window of 1,000 pulses | SURVIVED, 1279 | 38.9 s |
| V5b | tactical: a window of 0 ms | SURVIVED, 1279 | 40.9 s |
| V6 | tactical: the rule called after the instant's escapes | SURVIVED, 1279 | 41.8 s |
| V8 | auto: in the fight = Fighting (a withdrawal in transit out of the fight) | SURVIVED, 1279 | 42.9 s |
| V9 | the support state hash without the morale table | SURVIVED, 1279 | 47.0 s |
| V12 | the larger rise instead of the sum | SURVIVED, 1279 | 45.4 s |

Clean baseline on `fd48a26` (this round): Debug and Release builds 0 warnings, 0 errors; Debug 1279 / 0 / 0 (test step 41 s),
Release 1279 / 0 / 0 (18 s). The probe values in §10.1 come from a temporary probe test of this round (created and deleted by exact
name; its outputs are in the scratchpad, `mor-fix1/probe/probe.txt`), run clean and under V1, V2, V3, V9 and M24.

### 10.3 What changed

- **Code (3 files, each marked [MOR, verification round 1, …]):** `Morale/MoraleCodec.cs` (MOR-V1-10), `Support/SupportWorld.cs`
  (MOR-V1-11), `Support/SupportTypes.cs` (comment only, MOR-V1-09). No rule, coefficient or default changed: the in-battle rule's
  coefficient stays 0 by default and every golden is unchanged (the full suites pass; the new resolver-level tests set c = 200,
  2,000 or 3,000 explicitly).
- **Tests (2 files):** `Morale/LocalWinMoraleTests.cs` (+8 tests, 21 cases), `Morale/CampaignMoraleTests.cs` (+4 tests, 4 cases;
  2 tests changed: one rewritten, one extended). No lane D test changed in this round.
- **Docs:** `docs/m7-plan/morale-proposal.md` (§3.2 note, §6 and Annex A.1 corrected in place); this report.

### 10.4 Tests added or changed (MEASURED: all pass in Debug and in Release)

The resolver-level tests observe the hits from outside the rule's code: the tactical ones drive the battle event by event through
its public scheduler (what `Advance` does) and record every executed Damage event that lowered its target's hit points
(`RunObservingHits`); the AutoResolver ones read the per-step decision log (`RecordLog`: a step at which a formation's dealt damage
grew; one target per formation per step). Every scenario is TEST INPUT geometry; each test asserts the preconditions that give it
teeth (who struck when, who was still in the fight, how the battle ended), so a scenario that drifted would fail, not pass silently.

| Test | Pins |
|---|---|
| `Tactical_TheWindowIsOnePulseOfTheWinnersOwnBlows_InMilliseconds` (seeds 1, 4) | ~~**TURN-EXACT, the window in ms**~~ **[round 2, MOR-V2-02: a range pin, not turn-exact: A's volley lies 112,000 / 108,000 ms back, so any window from 8,000 to 107,999 ms passes (MEASURED); the boundary pin is §11]**: B struck G at the breaking instant, C (snipers) last struck it 2 or 8 s before (strictly before, within 60,000 ms), A's one volley (one-volley archers, TEST INPUT profile) lies more than a pulse back with A still in the fight; exactly B and C rise (94, 191), A (163) does not |
| `Auto_TheWindowIsOnePulseOfTheWinnersOwnBlows_InSteps` (seeds 1, 4) | the same in **steps** (30 per pulse): C last struck 1–30 steps before the break, A more than 30; rows B 94, C 191 **[round 2, MOR-V2-02: A's volley lies 57 / 51 steps back, so any window from 2 to 50 steps passes (MEASURED); the boundary pin is §11]** |
| `Tactical_AnOrderlyWithdrawalOffTheEdgeIsNotABreak_ARoutIs` (seeds 1–3) | G, struck by A within the pulse, retreats off its edge in good order at 32 s: no rise, the rule-on result equals the rule-off result in every field; the same battle without the order: G breaks and A rises by 80 |
| `Auto_AnOrderlyWithdrawalOffTheFieldIsNotABreak` (seeds 1–4) | G withdraws by the doctrine at step 30 and is off the field at step 31 (on its edge; DERIVED from `ExitSteps`), A having struck it at step 29 (observed): no rise for G ever; the battle goes on and A's one rise is for H's later break (66) |
| `Auto_AFormationWithdrawingInGoodOrderIsStillInTheFight_ItRisesWhenTheEnemyItStruckBreaks` (seeds 1–4) | F withdraws at step 30 (below the doctrine's 450), its last blow at step 29; G breaks 1–30 steps later; F, in transit, and B rise by 33; F ends at exactly 450 − 40 × its dead + 33 |
| `Tactical_AFormationRetreatingInGoodOrderIsStillInTheFight_ItRisesWhenTheEnemyItStruckBreaks` (seeds 1–4) | F ordered to Retreat at 62 s, its last blow at 60 s; G breaks within the pulse; F (retreating, Active, not routed) and B rise by 33 |
| `BothResolvers_TheStateHashCarriesTheRiseLog_WhenTheResultsAreEqual` | at c = 2,000 and 3,000 the rises (333, 500) are hidden by the cap: equal results, different state hashes, in both resolvers |
| `Apply_TieDense_TheRisesOfOneWinnerAddUp_BelowTheCap_WhateverTheOrder` | **TIE-DENSE**, 720 orders: A at 500 gains 100 + 75 = 175 → 675 (the larger rise alone would give 600) |
| `DA1_ALoserThatHeldTheField_DoesNotRise_TheReportDecides` | the reported winner rises (900,000) though it did not hold the field; the losing field holder falls by its loss only (700,000, rise 0) |
| `Baseline_AboveIt_TheHungerPenaltyStillApplies_ByHand` | baseline 800, unfed above it: 1,000,000 − 11,666 − 46,666 = 941,668 by hand |
| `Baseline_TheDecayAndTheRecoveryEachReadTheirOwnRate_ByHand` | decay 1,200 ‰/yr: 976,667 above the baseline; recovery 600 ‰/yr: 761,666 below it, in one pulse |
| `MoraleCodec_RefusesAHugeOrNegativeCount_AsAFormatError` | counts int.MaxValue, the overflow boundary, −1, int.MinValue, too many and too few for the bytes: FormatException; the exact count decodes |
| `TheStateHash_CarriesTheMoraleTable` (**rewritten**) | identical runs hash alike; runs differing only in a morale-table-only field: lane D's tables equal, the morale table and the state hash differ |
| `DA1_TheHandOffRefusesABadWinnerOrStrength` (**extended**) | as before, plus: a reported winner without BeatenPersons is refused for the winner's and the loser's record; a no-winner battle needs no strengths and settles DR29 alone (700,000) |

Moved pins and goldens: **none** (MEASURED: both full suites pass; the two changed tests are this lane's own).

### 10.5 What ran (MEASURED; same host and flags as §6)

| What | Command | Result |
|---|---|---|
| Baseline, `fd48a26` | `dotnet build M7Proto.slnx -c Debug` / `-c Release` (`--no-incremental`), `dotnet test M7Proto.slnx --no-build` | Debug 1279 / 0 / 0 (test step 41 s); Release 1279 / 0 / 0 (18 s); builds 0 warnings, 0 errors |
| Reproduction mutants | 11 verifier edits, full Debug suite each, bound 410 s | all SURVIVED (§10.2); no hang |
| Probes | two temporary probe tests (created and deleted by exact name) | values in §10.1; scenario searches for §10.4 |
| Fix-round mutants | 22 (§10.6) against the filter `Morale` + `LaneD` + `WarEconE` (234 tests; clean filter run 7 s; bound 10 × = 70 s) | **22 KILLED**, 6.3–7.3 s each; no survivor, so no full-suite re-run was needed |
| Closing runs, round-1 code (`ca1255c`; this report and the proposal are not compiled) | as the baseline | Debug **1304 / 0 / 0** (test step 43 s), Release **1304 / 0 / 0** (18 s); builds 0 warnings, 0 errors |
| Determinism scan | `prototypes/m7-sprint1/scripts/check-proto-determinism.sh` | 105 files under `Proto.M7/`, **CLEAN** |
| Doc links | `python3 scripts/check-doc-links.py --all`; this report by name | 463 files, 0 broken; 1 file, 0 broken |

### 10.6 Fix-round mutants (bounded; every kill by a named semantic test, none by a golden)

| # | Mutation | Killed by |
|---|---|---|
| V1 | the rise also for a losing field holder | `DA1_ALoserThatHeldTheField_DoesNotRise_TheReportDecides` |
| V2 | penalties bound to the below-baseline branch | `Baseline_AboveIt_TheHungerPenaltyStillApplies_ByHand` |
| V3 | the decay reads the recovery rate | `Baseline_TheDecayAndTheRecoveryEachReadTheirOwnRate_ByHand` |
| F2 | the recovery reads the decay rate | `Baseline_TheDecayAndTheRecoveryEachReadTheirOwnRate_ByHand` |
| V4 | auto: window in ms | `Auto_TheWindowIsOnePulseOfTheWinnersOwnBlows_InSteps` |
| V4b | auto: window 0 | `Auto_TheWindowIsOnePulseOfTheWinnersOwnBlows_InSteps`, `Auto_AFormationWithdrawingInGoodOrderIsStillInTheFight_…` |
| V5 | tactical: window 1,000 pulses | `Tactical_TheWindowIsOnePulseOfTheWinnersOwnBlows_InMilliseconds` |
| V5b | tactical: window 0 | `Tactical_TheWindowIsOnePulseOfTheWinnersOwnBlows_InMilliseconds`, `Tactical_AFormationRetreatingInGoodOrderIsStillInTheFight_…` |
| V6 | tactical: the rule after the escapes | `Tactical_AnOrderlyWithdrawalOffTheEdgeIsNotABreak_ARoutIs` |
| F6 | auto: a step's breaks judged against the previous step's field | `Auto_AnOrderlyWithdrawalOffTheFieldIsNotABreak` |
| V8 | auto: in the fight = Fighting | `Auto_AFormationWithdrawingInGoodOrderIsStillInTheFight_…` |
| F1 | tactical: a Retreat directive out of the fight | `Tactical_AFormationRetreatingInGoodOrderIsStillInTheFight_…` |
| V9 | the support state hash without the morale table | `TheStateHash_CarriesTheMoraleTable` |
| V12 | the larger rise instead of the sum | `Apply_TieDense_TheRisesOfOneWinnerAddUp_BelowTheCap_WhateverTheOrder` |
| M24 | auto: the rule's state out of the end state hash | `BothResolvers_TheStateHashCarriesTheRiseLog_WhenTheResultsAreEqual` |
| V13 | tactical: breaks judged before the instant's routs | `Tactical_TheFormationThatBrokeItsEnemyRisesAtThatInstant_OthersDoNot`, `Tactical_TheWindowIsOnePulseOfTheWinnersOwnBlows_InMilliseconds`, `Tactical_AnOrderlyWithdrawalOffTheEdgeIsNotABreak_ARoutIs` (+2) |
| V14 | auto: breaks judged before the step's routs | `Auto_TheFormationThatBrokeItsEnemyRisesAtThatStep_OthersDoNot`, `Auto_TheWindowIsOnePulseOfTheWinnersOwnBlows_InSteps`, `BothResolvers_ArchersWhoseVolleysBrokeTheInfantry_RiseWithoutHavingBeenHit` (+3) |
| M15 (relabelled) | tactical: breaks never judged (the rule never fires) | `Tactical_TheFormationThatBrokeItsEnemyRisesAtThatInstant_OthersDoNot`, `BothResolvers_ArchersWhoseVolleysBrokeTheInfantry_RiseWithoutHavingBeenHit` (+4) |
| M18 (relabelled) | auto: breaks never judged | `Auto_TheFormationThatBrokeItsEnemyRisesAtThatStep_OthersDoNot`, `BothResolvers_ArchersWhoseVolleysBrokeTheInfantry_RiseWithoutHavingBeenHit` (+4) |
| F3 | codec: the count bound removed (MOR-V1-10 fix reverted) | `MoraleCodec_RefusesAHugeOrNegativeCount_AsAFormatError` |
| F4 | hand-off: the BeatenPersons refusal removed (MOR-V1-11 fix reverted) | `DA1_TheHandOffRefusesABadWinnerOrStrength` |
| F5 | hand-off: the refusal for the winner's own record only | `DA1_TheHandOffRefusesABadWinnerOrStrength` |

The two questions of ADR-015 §7.2, answered per family: every mutant above fails a test (teeth), and each asserted property is one
the design states (aim): the report's winner, never the field (DA1-R (a), proposal §6); the penalties and the two rates of option
(b) (proposal §4.4); the window of one pulse of the winner's own blows, the same in both resolvers (proposal §3.2, D-079 D11) **[round 2,
MOR-V2-02: the round-1 tests did not pin "the same in both resolvers": they held windows from 8,000 to 107,999 ms and from 2 to 50
steps (MEASURED); the boundary pins of §11 do]**; an
orderly withdrawal is not a break and a formation withdrawing or retreating in good order is in the fight (proposal §3.2; the
tactical engine's retreating men are Active); every serialized table and the rule's log in the state hash (CLAUDE.md, determinism
contract); a malformed table is a format error; a reported winner needs the beaten strength (the stand-in's input contract).

### 10.7 Commits and line counts

Round-1 commits on `lane/MOR`: `ca1255c` (code and tests) and the commit that adds this section and the proposal corrections.
`git diff --numstat fd48a26..ca1255c` (MEASURED): `CampaignMoraleTests.cs` +135 −8, `LocalWinMoraleTests.cs` +334 −0,
`MoraleCodec.cs` +3 −1, `SupportTypes.cs` +5 −2, `SupportWorld.cs` +3 −0; the documentation commit: `morale-proposal.md` +23 −3 and
this report.

### 10.8 NOT RUN, deferred, limitations

- **NOT RUN:** `M7Econ.slnx` and `M7DetMath.slnx` (not touched); the `morale-parity` bench (no rule, coefficient or default changed,
  so §5.4 stands; the lane P tests in the full suite pass unchanged); lane P's held-out acceptance battery (lane P's record; D-079 Q4
  OPEN); `scripts/validate.sh` and every `Sim.*` suite (no `Sim.*` change; D-079 Q5); a Windows run; an independent verification of
  this round's own changes (ADR-015 §6: the next verification round). A full-suite re-run of the fix-round mutants was not needed:
  each was killed by the filter run.
- **Limitations:** the resolver-level tests use chosen seeds of TEST INPUT scenarios, with their preconditions asserted; they pin the
  rule's semantics on those battles, not over the whole battery. The arrears penalty above the baseline is not separately pinned
  (it shares the expression that the hunger case pins). Lane D's `SupportCodec` decoders have the same OverflowException path for a
  huge count (inherited pattern, not changed: lane D's file, outside this finding; noted for lane D).
- **Deferred:** as §6, unchanged.

### 10.9 For the Director

No new question. READING M (b) stays OPEN (§9).

---

## 11. Verification round 2 (2026-11-10)

Fix round on the verifier's findings MOR-V2-01..03 (verifier worktree pinned to `0484697`). Clone `/home/user/m7w/MOR`, branch
`lane/MOR`. Every finding was first reproduced in this lane's own clone at `0484697` with this round's own harness and probes (the
verifier's mutant edits copied into this round's own specification, scratchpad `mor-fix2/mut/`; the probes are temporary test
classes of this round, created in the clone and deleted by exact name, outputs in scratchpad `mor-fix2/probe*/`); a finding was
fixed only once reproduced. Statements this round superseded are marked in place above (§0, §2, §4.2, §7, §10.1, §10.4, §10.6),
in the proposal (§3.2, §10 item 6) and in two round-1 test comments (`LocalWinMoraleTests.cs`, the two round-1 window tests).

### 11.1 Verdicts

| ID | Sev. | Verdict | Reproduction (MEASURED, `0484697`) | Fix | Pinned by (semantic tests) |
|---|---|---|---|---|---|
| MOR-V2-01 | major | **REPRODUCED** | B1 (`LocalWinMorale.cs:57`, a break judged on "out of the fight now" alone) survived the full Debug suite (1304 passed, 46.8 s; bound 450 s). Probe at c = 200 on lane P's tuning battery, clean → B1: AutoResolver, all 1,600 cases, 1,779 → 1,858 rise rows (79 duplicate (winner, broken) rows in 57 cases; clean 0); TacticalResolver, combined-arms + sniper-screen (160 cases), 447 → 575 (128 in 77 cases; clean 0). Combined-arms seed 1: B1 adds 4 ← 1 +90 at 172,000 ms in the AutoResolver (clean: 150,000 only) and at 202,000 ms in the TacticalResolver (clean: 160,000 only; 4's last blow on 1's routing men at 184,000 ms); seed 2, tactical: 4 ← 1 at 160,000, 166,000 and 202,000 ms | **tests only** (the rule's code was right; its "before" half was untested) | `Apply_TieDense_AFormationAlreadyOutOfTheFight_DoesNotBreakAgain_NorDoesOneEnteringIt_WhateverTheOrder`, `BothResolvers_AFormationThatBrokeEarlier_IsNotCreditedAgainAtALaterBreak_LanePCombinedArms` |
| MOR-V2-02 | minor | **REPRODUCED** | W2 (tactical 90,000 ms), W3 (58,000 ms), W4 (auto 15 steps), W5 (45 steps) and W6 (29 steps) each survived the full Debug suite (1304 passed; 40.0–52.8 s); W1 (30,000 ms) was killed (by `Tactical_AFormationRetreatingInGoodOrder…`). Probe of the round-1 window scenario, seeds 1–6: tactical, C's last blow 0–8,000 ms and A's volley 108,000–120,000 ms before the break; auto, 0–4 and 51–60 steps. For the tests' seeds 1 and 4 the round-1 tests therefore accept any tactical window in [8,000, 107,999] ms and any auto window in [2, 50] steps; the inclusive boundary was pinned on the pure `Fought` only | **tests only** (boundary pins in both resolvers, at the default and at a 30,000 ms pulse); the round-1 overclaims corrected in place (proposal §3.2, §10.1, §10.4, §10.6, two test comments) | `Tactical_TheWindowsBoundary_ABlowExactlyOnePulseBeforeTheBreakCounts_OneStepMoreDoesNot`, `Auto_TheWindowsBoundary_ABlowExactlyOnePulseBeforeTheBreakCounts_OneStepMoreDoesNot` |
| MOR-V2-03 | minor | **REPRODUCED** | probe: six gun teams (lane N's gun crew with field guns, Defend) against heavy infantry 6, 10 or 20, four geometries, seeds 1–3: in 12 of 12 battles the infantry broke (SideEliminated, side 1; killed 5–20, routed 0–4), it dealt 0 damage, the guns dealt 290–1,000 HP through 11–40 landed DomainHit kind-0 hits and none through the Damage event, and LocalWinRises was empty; the AutoResolver refuses the battle ("domain weapons have no AutoResolver model (TacticalResolver only)") | **code**: a land formation's crew-served hit on a land entity (DomainHit kind 0) is recorded in the rule's hit table while the rule is on (`TacticalBattle.DomainCombat.cs` +1 line, `TacticalBattle.cs` `LocalWinDomainLandHit`); vessels and aircraft stay outside the rule; doc comment in `BattleSetup.cs`; proposal §3.2 and §10 item 6 | `Tactical_GunsThatBreakTheInfantry_Rise_TheirCrewServedWeaponsDealtTheDamage`, `Tactical_GunsAndInfantryThatBreakTheEnemyTogether_BothRise` |

Summary: **3 REPRODUCED and fixed, 0 REFUTED.**

### 11.2 Reproduction runs (MEASURED, `0484697`; full Debug suite per mutant, bound 10 × the clean test step of 45 s = 450 s)

| Mutant (verifier's edit, copied) | What it does | Result | Test step |
|---|---|---|---|
| B1 | a break judged on "out of the fight now" alone | SURVIVED, 1304 passed | 46.8 s |
| W1 | tactical window 30,000 ms | KILLED (4 failed) | 39.9 s |
| W2 | tactical window 90,000 ms | SURVIVED, 1304 | 40.0 s |
| W3 | tactical window 58,000 ms (one step short) | SURVIVED, 1304 | 45.6 s |
| W4 | auto window 15 steps | SURVIVED, 1304 | 40.2 s |
| W5 | auto window 45 steps | SURVIVED, 1304 | 52.8 s |
| W6 | auto window 29 steps (one step short) | SURVIVED, 1304 | 51.5 s |

Clean baseline on `0484697` (this round): Debug and Release builds (`--no-incremental`) 0 warnings, 0 errors; Debug 1304 / 0 / 0
(test step 45.2 s), Release 1304 / 0 / 0 (26.3 s). The probe values in §11.1 come from this round's probes, run clean and (for the
B1 cases and battery) under B1, all in the Debug build.

### 11.3 What changed

- **Code** (2 files, each marked [MOR r2]; nothing runs while the rule is off, so the default stays the exact identity):
  `Proto.M7/Tactical/TacticalBattle.DomainCombat.cs` (lane N's file; +1 line in `DomainHit`, kind 0) and
  `Proto.M7/Tactical/TacticalBattle.cs` (+11: `LocalWinDomainLandHit`, which records the hit only when the source is a land entity).
  Comment only: `Proto.M7/Tactical/BattleSetup.cs` (the rule's summary names every weapon of a land formation).
- **Tests:** `Proto.M7.Tests/Morale/LocalWinMoraleTests.cs` +6 tests (15 cases); two round-1 test comments corrected in place. No
  other test changed. **No golden or pin moved** (MEASURED: both full suites pass; no existing test switches the rule on with
  crew-served weapons).
- **Docs:** `docs/m7-plan/morale-proposal.md` §3.2 (two round-2 notes; the overstated round-1 clause struck) and §10 item 6; this
  report.
- **Effect on lane P's tuning battery** (MEASURED, `morale-parity 200 2`, Release, on the round-2 code): the four digests, the rise
  counts (tactical 1,819, auto 1,779), the changed outcomes (64, 31), the changed winners (40, 5) and rule P1 (16/20 off and on)
  equal §5.4's exactly. The battery has no crew-served weapons (the AutoResolver would refuse them), so the code change cannot
  reach it; the measurement confirms it.

### 11.4 Tests added (MEASURED: all pass in Debug and in Release)

Every scenario is TEST INPUT geometry (one test uses lane P's combined-arms battle as built by `ParityBatteryP`), and each test
asserts the preconditions that give it teeth, observed from outside the rule's code (the tactical hits from the executed Damage and
DomainHit events, the AutoResolver's from its per-step log), so a scenario that drifted fails instead of passing silently.

| Test | Pins |
|---|---|
| `Apply_TieDense_AFormationAlreadyOutOfTheFight_DoesNotBreakAgain_NorDoesOneEnteringIt_WhateverTheOrder` | **TIE-DENSE**, 720 orders of six formations: only G (in the fight before, out now) broke. R (routed earlier) and E (gone in good order earlier), both struck by W within the window, are not broken again; Y (out before, in now: entering the fight) is not a break. W gains exactly 100 → 600; one row (W ← G). B1 gives W 263 and X 100; B2 (any change of state a break) credits Y |
| `BothResolvers_AFormationThatBrokeEarlier_IsNotCreditedAgainAtALaterBreak_LanePCombinedArms` | lane P's combined-arms seed 1, c = 200, both resolvers: rows exactly 4 ← 1 +90 and 2 ← 3 +90 at t1, 4 ← 2 +100 at t2 (tactical 160,000 / 202,000 ms, auto steps 75 / 86); observed: 4 struck 1 within one pulse before t2 (tactical: at 184,000 ms, on 1's routing men), 1 out of the fight before t2 (tactical: no Active entity just before t2; auto: morale below the rout threshold at step 75 and no blow after), 4 in the fight at t2; one row per (winner, broken) pair |
| `Tactical_TheWindowsBoundary_ABlowExactlyOnePulseBeforeTheBreakCounts_OneStepMoreDoesNot` (4 cases) | **TURN-EXACT**: archers (TEST INPUT fire interval) holding behind the line, all ten Active at the break; default pulse: last volley exactly 60,000 ms before the break → A rises (164) with B (95); 62,000 ms → only B. 30,000 ms pulse (TEST INPUT): 30,000 ms → A rises (150) with B (75); 32,000 ms → only B. Every observed hit lands on a 2,000 ms step |
| `Auto_TheWindowsBoundary_ABlowExactlyOnePulseBeforeTheBreakCounts_OneStepMoreDoesNot` (4 cases) | **TURN-EXACT** in steps: default pulse, the archers' one volley 30 steps before the break → A rises (150) with B (75); 31 steps → only B. 30,000 ms pulse (15 steps): 15 steps → A rises; 16 → only B |
| `Tactical_GunsThatBreakTheInfantry_Rise_TheirCrewServedWeaponsDealtTheDamage` (seeds 1–3) | six gun teams break ten heavy infantry; every blow on the infantry through the crew-served path, the infantry dealt nothing; one row (the end instant, guns ← infantry, 125); the rule-on result equals the rule-off result in every field but the guns' end morale (+125); c = 0 is the identity (result and state hashes equal to a setup that never names the rule); the AutoResolver refuses the battle |
| `Tactical_GunsAndInfantryThatBreakTheEnemyTogether_BothRise` (seeds 3, 4) | one gun team (its last blow 46 s / 44 s before the break, crew-served path) and thirty heavy infantry (a blow at the breaking instant) break fifty heavy infantry together: both rise, 196 and 125 |

The round-1 resolver window tests stay as they were (they still pin that the window is about the winner's own blows, that a blow
strictly before the break counts and that a blow ~1.8 pulses back does not); only their comments now say what they pin.

### 11.5 What ran (MEASURED; same host and flags as §6)

| What | Command | Result |
|---|---|---|
| Baseline, `0484697` | `dotnet build M7Proto.slnx -c Debug` / `-c Release` (`--no-incremental`), `dotnet test M7Proto.slnx --no-build` | Debug 1304 / 0 / 0 (test step 45.2 s); Release 1304 / 0 / 0 (26.3 s); builds 0 warnings, 0 errors |
| Reproduction mutants | 7 verifier edits, full Debug suite each, bound 450 s | §11.2; no hang |
| Probes | temporary probe test classes (created and deleted by exact name) | §11.1; the scenario searches behind §11.4 (a periodic-volley search in both resolvers at both pulse lengths; a retreat search; a guns search) |
| Fix-round mutants | 16 on the final tree against the filter `Morale` + `LaneD` + `WarEconE` (266 tests; clean run 7.8 s; bound 10 × = 78 s); the survivor against the full suite (bound 450 s) | §11.6 |
| Closing runs, final code (`0047cda`; this report and the proposal are not compiled) | as the baseline | Debug **1319 / 0 / 0** (test step 43.3 s), Release **1319 / 0 / 0** (18.7 s); builds 0 warnings, 0 errors |
| Determinism scan | `prototypes/m7-sprint1/scripts/check-proto-determinism.sh` | 105 files under `Proto.M7/`, **CLEAN** |
| Doc links | `python3 scripts/check-doc-links.py --all`; this report by name | 463 files, 0 broken; 1 file, 0 broken |
| Parity effect of the round-2 code | `Proto.M7.Bench.dll morale-parity 200 2` (Release, on `4e3b0f6`, whose `Proto.M7` equals the final one) | digests tactical off `90E58DCB…F65D6914` / on `1E6D728D…8C76525D`, auto off `2AC31207…A0B68F31` / on `CAAEB267…ED37D6E7`: all equal to §5.4 (tactical 148.3 s on 2 threads, auto 2.8 s) |

### 11.6 Fix-round mutants (final tree `0047cda`; filter run each, bound 78 s; every kill by a named semantic test, none by a golden)

| # | Mutation | Verdict (filter: time) | Killed by |
|---|---|---|---|
| B1 | a break on "out of the fight now" alone | KILLED (7.1 s) | `Apply_TieDense_AFormationAlreadyOutOfTheFight_…`, `BothResolvers_AFormationThatBrokeEarlier_…` |
| B2 | a break = any change of fight state | KILLED (8.4 s) | `Apply_TieDense_AFormationAlreadyOutOfTheFight_…` |
| W1 | tactical window 30,000 ms | KILLED (8.8 s) | `Tactical_AFormationRetreatingInGoodOrder…` (4), `Tactical_GunsAndInfantry…` (2), `Tactical_TheWindowsBoundary_…` (2) |
| W2 | tactical window 90,000 ms | KILLED (8.2 s) | `Tactical_TheWindowsBoundary_…` (the 62,000 and 32,000 ms cases) |
| W3 | tactical window 58,000 ms | KILLED (9.3 s) | `Tactical_TheWindowsBoundary_…` (the 60,000 and 30,000 ms cases) |
| W7 | tactical window 62,000 ms | KILLED (8.1 s) | `Tactical_TheWindowsBoundary_…` (the 62,000 and 32,000 ms cases) |
| W9 | tactical window fixed at 60,000 ms, not the battle's PulseMs | KILLED (8.3 s) | `Tactical_TheWindowsBoundary_…` (the 32,000 ms case of the 30,000 ms pulse) |
| W4 | auto window 15 steps | KILLED (8.6 s) | `Auto_TheWindowsBoundary_…` (the 30- and 15-step cases) |
| W5 | auto window 45 steps | KILLED (8.1 s) | `Auto_TheWindowsBoundary_…` (the 31- and 16-step cases) |
| W6 | auto window 29 steps | KILLED (7.3 s) | `Auto_TheWindowsBoundary_…` (the 30- and 15-step cases) |
| W8 | auto window 31 steps | KILLED (8.5 s) | `Auto_TheWindowsBoundary_…` (the 31- and 16-step cases) |
| W10 | auto window fixed at 30 steps, not PulseMs ÷ StepMs | KILLED (9.1 s) | `Auto_TheWindowsBoundary_…` (the 16-step case of the 30,000 ms pulse) |
| G1 | the MOR-V2-03 fix reverted (crew-served land hits not recorded) | KILLED (7.8 s) | `Tactical_GunsThatBreakTheInfantry_…` (3), `Tactical_GunsAndInfantry…` (2) |
| G2 | crew-served hit recorded the wrong way round (target fought source) | KILLED (8.5 s) | the same 5 cases |
| G4 | crew-served hit recorded one step late | KILLED (7.7 s) | `Tactical_GunsThatBreakTheInfantry_…` (3) |
| G3 | crew-served hit recorded although it dealt no damage (`amount > 0` dropped) | **SURVIVED** (8.3 s); full suite: **SURVIVED** (1319 passed, 45.3 s; bound 450 s) | — |

Verdicts (MEASURED): **15 of 16 KILLED**, each by at least one named semantic test; no mutant hung; every filter run stayed under the
78 s bound (the slowest 9.3 s). The same set had been run once before the 30,000 ms cases were added (`4e3b0f6`, filter of 262
tests, bound 82 s): 13 of the 14 then defined killed, G3 survived (filter and full suite: 1315 passed, 41.2 s).

**The survivor G3 — not claimed equivalent.** ADR-015 §7.2's two questions. *Teeth:* no test fails. *Aim:* the rule credits a
formation that "dealt damage", and the Damage handler has the same guard (`dmg > 0`), so the property is the stated one. Its
reach, MEASURED by a probe on this code: in 432 battles with crew-served weapons (one to six gun teams, with and without
infantry, three enemy sizes, three distances, seeds 1–6) there were 1,896 zero-damage crew-served hits on land entities, and every
one came at an instant when the same gun formation also landed a damaging hit on the same enemy formation; the rule reads the
hit table only after all of an instant's damage (DERIVED: events run in (time, class) order, Damage is class 40 and Resolve
class 50), so G3 changes nothing the rule reads in any of the 432. It would matter only for a
lone zero-damage hit (a shell whose blast finds only a man already brought down at that instant) that is a formation's last blow on
the enemy within a pulse of a later break: possible in principle, not observed. An information mutant on the Damage handler's own
guard (L1, round-0 code, `dmg > 0` dropped) also survives (filter, and full suite 1315 passed, 39.6 s): the zero-damage guard is
unpinned in both damage paths. Deferred (§11.8).

### 11.7 Commits and line counts

Round-2 commits on `lane/MOR`: `4e3b0f6` (code and tests), `0047cda` (the 30,000 ms pulse cases), and the commit that adds this
section and the proposal corrections. `git diff --numstat 0484697..0047cda` (MEASURED): `LocalWinMoraleTests.cs` +309 −2,
`TacticalBattle.cs` +11 −0, `TacticalBattle.DomainCombat.cs` +1 −0, `BattleSetup.cs` +2 −1; the documentation commit:
`morale-proposal.md` and this report.

### 11.8 NOT RUN, deferred, limitations

- **NOT RUN:** `M7Econ.slnx` and `M7DetMath.slnx` (not touched); lane P's held-out acceptance battery (lane P's record; D-079 Q4
  OPEN); `scripts/validate.sh` and every `Sim.*` suite (no `Sim.*` change; D-079 Q5); a Windows run; an independent verification
  of this round's own changes (ADR-015 §6: the next verification round).
- **Limitations:** the resolver-level pins use chosen seeds of TEST INPUT scenarios (and one lane P case), their preconditions
  asserted; they pin the rule on those battles, not over the battery. The tactical boundary pins hold the window to one pulse at the
  engine's 2,000 ms step granularity (a window of 60,000 to 61,999 ms passes them; every observed hit lands on a step). The
  zero-damage guard is unpinned in both damage paths (G3, L1; §11.6). Battles with crew-served weapons exist in the TacticalResolver
  only, so the round-2 code change has no AutoResolver counterpart to compare (the AutoResolver refuses them: asserted). Lane D's
  `SupportCodec` count overflow (noted in §10.8) is unchanged.
- **Deferred:** as §6, unchanged; plus pinning the zero-damage guard (a battle in which a formation's last blow within the window
  is a lone zero-damage hit) — the next verification round or the switch-on re-measurement, whichever comes first.

### 11.9 For the Director

No new question. READING M (b) stays OPEN (§9). Nothing in this round is a Director decision: the three findings were coverage and
code defects of the delegated design (D-082 §5), fixed within it.
