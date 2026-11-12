# D-087: A tie is a tie, fighting means damage, armies fight in battalions, every AI battle is auto-resolved; three test fixes (Director, 2026-11-12)

**Status: RULED (Director, 2026-11-12), with READINGS flagged.** Administrative record of the Director's message of 2026-11-12
(cited **DF**). It answers items 1–12 of the plain-language list of open questions that the orchestrator put that day (27 items,
drawn from `docs/m7-plan/pass-state/README.md` §5; items 1–12 are quoted in §0). Items 13–27 are not answered here (§6).
Categories as in D-076..D-086: DECIDED, ADMIN, DELEGATED, READING, OPEN.

**Source (the whole message, verbatim, typing unchanged).** Repository copy:
[`m7-plan/pass-state/notes/director-messages/director-answers-2026-11-12.md`](m7-plan/pass-state/notes/director-messages/director-answers-2026-11-12.md);
working copy `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gov/director-answers-2026-11-12.md`.

> 1) yes
> 2) yeah check in normal build
> 3)yes
> 4)keep it
> 5) can this be resolved with multiple csv files? Since we're probably hitting the row or column limit?
> 6) find a way to auto resolve them. Having the engine run for every fight would slow down the game a lot when there's 12 players and maybe 40-50 air/naval fights between every turn.
> 7) if no combat happened and both armies kept circling around or not moving, then it's a no morale change. Just moving around and 0 damage received = no morale changed
> 8) a tie is a tie. Both armies come out with whatever was left. Routing units get converted back to normal units with decreased morale and become part of the army again. By definition yes, the side that was attacked, is the defender. It'll be army specific. Empire A can declare war and initiate zone of control or siege from unit A-1 on empire B's unit B-1, but not engage in combat. But if B-1 initiates combat on A-1, then A-1 is the defender.
>
> 9) Ambush is basically if a unit is hidden in an area, invisible to all enemies and then another enemy unit passes through that region. (Approximately 10 kilometres radius from the unit that's hidden ± 10 more depending on army size)
> 10) ends in a tie
> 11) both. But again, tie is a tie
> 12) it gets broken into batallions. We need to decide how big a batallion is for each unit type. Like 50 archers is an archer batallion. 5 tanks is a tank batallion. 2 jets are a batallion. Basically during war, only a batallion can be directed to make a formation, or attack. Individual units canot be moved. Like total war troy
> An army has a max of 20 batallions in that though. We'll set the cap at 25.

## 0. The questions answered (items 1–12, as the orchestrator put them)

1. Two `Sim.Ui.Tests` classes (`SessionExportTests`, `SessionRecordFolderTests`) drive ImGui frames outside the `ImGui context`
   collection, so they can run at the same moment as other UI tests and crash the test host (CI run 980). Fix: one line each.
2. `PopulationTests.ProductionPipeline_PerPhaseBench_Reported` asserts that 200 founded turns take under 5,000 ms. In the slow
   Debug build it fails now and then, at base `51ab032` too; loosen it, or check it only in the normal (Release) build?
3. Lane RR's new 64-turn city-state test (put as "about 29 seconds") should go on the slow list, so that Tier 1 skips it.
4. Keep lane RR's per-turn hook that writes research-name lines into the chronicle (`UiSession.Step`) under D-086 DE-R (a)?
5. Lane RR's P1: unlock the frozen kernel's `SessionTrace.Parse`, so that a game past 1920 CE (turn 1271) can be continued,
   replayed and inspected again.
6. Lane D's READING A: AI-vs-AI battles with aircraft, vessels or crew-served guns, which the AutoResolver cannot model, run on
   the TacticalResolver without graphics. Keep that, or refuse such battles?
7. D-085's readings: is fighting judged per battle (applied) or per formation, and does any attack count (applied) or only one
   that deals damage?
8. Lane AP's Q-AP1: who is the defender, the side that was attacked (applied) or the side that did not start the contact?
9. Lane AP's Q-AP2: may a battle opened by an ambush start inside DR31's middle third?
10. Lane AP's Q-AP3: the Standoff ending (nobody progresses, both sides holding): adopt it or keep it off?
11. Lane AP's Q-AP4: does the equal-sides rule (D-082 DA6/DA7) apply in both resolvers (applied) or the AutoResolver only?
12. Lane CB's E1 for M7.6: should a battle entity that stands for many persons fight with the strength of all of them?

## 1. Decisions

| ID | Category | Decision | Director's words (DF) |
|---|---|---|---|
| DF1 | DECIDED | The `Sim.Ui.Tests` classes that drive ImGui frames through the headless harness join the `ImGui context` collection (no parallel runs), ending the test-host race. | "1) yes" |
| DF2 | DECIDED | The 5,000 ms wall-clock budget of `PopulationTests.ProductionPipeline_PerPhaseBench_Reported` is asserted in the optimized (Release) build only. A Debug build runs the test and reports its timings without the budget. | "2) yeah check in normal build" |
| DF3 | DECIDED | Lane RR's 64-turn city-state test goes on the slow list, `scripts/test-durations.json`. | "3)yes" |
| DF4 | DECIDED (scope) | Lane RR's per-turn research-name hook (`UiSession.Step`, `NoteResearchNames`) stays: D-086 DE-R (a) covers it. | "4)keep it" |
| DF5 | OPEN | The Director asked whether several CSV files would resolve item 5, supposing a row or column limit. The orchestrator's answer is §3. The change request still needs the Director's yes. | "5) can this be resolved with multiple csv files? Since we're probably hitting the row or column limit?" |
| DF6 | DECIDED (direction); design DELEGATED | Every AI-vs-AI battle is auto-resolved, those with aircraft, vessels, crew-served guns and embarked troops included. The AutoResolver's model is extended to them, so that no AI battle runs the TacticalResolver. The reason given is cost at scale: 12 players and 40–50 air or naval battles between turns. | "6) find a way to auto resolve them. Having the engine run for every fight would slow down the game a lot when there's 12 players and maybe 40-50 air/naval fights between every turn." |
| DF7 | DECIDED (answers D-085 DD1-R2) | Fighting means damage. A battle in which no damage was dealt (the armies circled or stood still) changes no morale: moving around with 0 damage received changes nothing. | "7) if no combat happened and both armies kept circling around or not moving, then it's a no morale change. Just moving around and 0 damage received = no morale changed" |
| DF8 | DECIDED (supersedes D-082 DA6; answers Q-AP1) | A tie is a tie: equal sides produce no winner. Both armies come out with whatever was left. Their routing units turn back into normal units with decreased morale and rejoin their armies. The defender is the army that was attacked, army by army: the army that initiates combat is the attacker. Declaring war, or exerting a zone of control or a siege without combat, does not make an army the attacker. In the Director's example, if B-1 initiates combat on A-1, A-1 is the defender. | "8) a tie is a tie. Both armies come out with whatever was left. Routing units get converted back to normal units with decreased morale and become part of the army again. By definition yes, the side that was attacked, is the defender. It'll be army specific. …" |
| DF9 | DECIDED (definition); mechanics DELEGATED | An ambush happens when an enemy unit passes through the region of a unit that is hidden there and invisible to all enemies. The region is about 10 km around the hidden unit, ±10 km depending on army size. | "9) Ambush is basically if a unit is hidden in an area, invisible to all enemies and then another enemy unit passes through that region. (Approximately 10 kilometres radius from the unit that's hidden ± 10 more depending on army size)" |
| DF10 | DECIDED (answers Q-AP3; ratifies lane TR's Standoff end, P-TR1) | A Standoff ends the battle in a tie. | "10) ends in a tie" |
| DF11 | DECIDED (answers Q-AP4) | The equal-sides rule, now a tie (DF8), applies in both resolvers. | "11) both. But again, tie is a tie" |
| DF12 | DECIDED (direction); sizes DELEGATED | Armies fight in battalions. Each unit type has a battalion size; the Director's examples are 50 archers, 5 tanks and 2 jets. In battle, only a battalion can be ordered to take a formation or to attack; individual units cannot be moved. An army holds at most 25 battalions. The Director names a reference game, whose armies hold 20, as the model; under D-069 it is a reference only, and the design, sizes and numbers are this game's own. | "12) it gets broken into batallions. We need to decide how big a batallion is for each unit type. … An army has a max of 20 batallions in that though. We'll set the cap at 25." |

## 2. Readings (each applied until the Director corrects it; both alternatives stated)

- **DF7-R1 (damage where: in the battle or by the army).**
  - (a) APPLIED: in the battle, as D-085 DD1-R1 (a). If any damage was dealt in the battle, D-083's entry cost, DR29's decrease
    and, for a winner, DA1's rise apply as before to every army in it. If no damage was dealt at all, no army's morale changes
    through it. The Director's words describe both armies ("both armies kept circling around").
  - (b) By the army: an army that dealt no damage and received none has no morale change, even where others fought. This
    would narrow D-083 DB1 to armies that fought.
- **DF7-R2 (what damage is).** (a) APPLIED: any strength lost to an enemy attack, as the resolvers count it (hit points or
  persons). (b) Persons killed only.
- **DF8-R1 (when routing units rejoin).** (a) APPLIED: at the end of a tie, as the Director's words state. After a win or a loss,
  the morale proposal's rally rules stand. (b) After every battle that the army survives.
- **DF8-R2 (several armies in one battle).** (a) APPLIED: each army's role is its own. An army is a defender when another army
  initiated combat against it, and an attacker when it initiated combat. One battle may hold attackers and defenders of several
  empires. (b) One role per side: the role of the side's first army.
- **DF9-R1 (whose army size sets the radius).** (a) APPLIED: the hidden army's. A larger hidden army watches a wider region,
  from about 10 km up to about 20 km, and a smaller one a narrower region, down toward 0; the curve is DELEGATED (a TUNE value).
  (b) The passing army's size.
- **DF9-R2 (where an ambush battle starts; lane AP's Q-AP2).** (a) APPLIED in the prototype: an ambush battle is exempt from
  DR31's deployment thirds, and the hidden side starts where it hid, around the passing army's line of march. (b) The thirds
  hold for ambush battles too. Flagged: the Director defined the ambush, not its battle's deployment.
- **DF12-R1 (battalion and battle entity).** (a) APPLIED: the battalion is the command unit. Inside it, the battle keeps lane
  CB's entity rule: five tanks are five machine entities that take orders as one battalion. (b) A battalion is a single battle
  entity.

## 3. Item 5: the orchestrator's answer to DF5

- **No row or column limit is reached.** The trace file holds one line per turn: 1,630 at the end of the timeline. The
  workbook's largest sheet, Telemetry, holds one row per turn plus continuation rows. At 1,630 turns it was 16,678,978 bytes,
  MEASURED by lane RR (`docs/m6-plan/run-record-research-and-exit-2026-11.md` §4.2). That is far below Excel's 1,048,576 rows
  and 16,384 columns. A cell's 32,767-character limit is already handled by continuation rows (ADR-059).
- **The cause is the number format of the year.** From 1920 CE (world year 5920, turn 1271) a turn is half a year, so the trace
  writes years such as `5920.5`. The frozen kernel's reader, `SessionTrace.Parse`, reads the year as a whole number and refuses
  the line, so "Continue last game", `sim replay` and `sim inspect` refuse the session (MEASURED by lane RR on a 1,630-turn game).
  Several files would hold the same half-year values and be refused in the same way.
- **The fix is lane RR's P1:** read the year as the number written. It is one reader in `Sim.Core/Kernel/SessionTrace.cs`, plus
  the year column that `sim replay` and `sim inspect` print. No world, hash or golden changes, because the trace is an observer.
  The kernel is frozen, so the change needs the Director's sign-off (CLAUDE.md: a change request, `docs/adr/cr-NNN.md`).

## 4. Superseded or narrowed

- **D-082 DA6 is superseded:** equal sides produce a tie, not a defender's win (DF8), in both resolvers (DF11). DA6-R's reading
  of "equal" (a resolver result that separates neither side) stands.
- **D-082 DA7 is superseded as a tie-break:** an equal ambush is a tie too ("a tie is a tie"), so DA7-R (the ambusher wins) falls.
  DF9 defines the ambush itself.
- **D-082 DA1 is unchanged.** A tie has no winner, so it raises no morale.
- **D-085 DD1 stands.** DD1-R2 (a), any attack, is replaced by DF7: fighting is damage. DD1-R1 (a), the battle, stays applied
  (DF7-R1).
- **D-083 DB1 is narrowed again:** the entry cost applies where damage was dealt in the battle.
- **Lane D's READING A is superseded:** AI-vs-AI battles no longer fall back to the TacticalResolver; the AutoResolver models them
  (DF6).
- **Lane AP:** Q-AP1 is answered (DF8), Q-AP3 (DF10) and Q-AP4 (DF11); Q-AP2 is read (DF9-R2). The prototype's equal-sides
  function becomes the tie rule.
- **Lane TR's P-TR1 (the Standoff end) is ratified,** with a tie as its outcome (DF10).
- **Lane CB:** E1 (an entity fights as a block of its persons) is superseded by DF12 at the command level, and D-082 DA5 is
  narrowed: an army's cap counts battalions, at most 25. READING CB-1 and the CAP-5 question are settled where DF12 reaches
  them: machines are commanded in battalions (5 tanks, 2 jets), and every unit type has a battalion size within the army's 25.
  DF12-R1 keeps the entity rule inside a battalion.

## 5. Applied now, and delegated (design; PROPOSALS in their lanes)

- **DF1 and DF2** are applied by the orchestrator (test code only), with Tier 1 and Tier 2 validation. DF1 also covers
  `DiscoveryShortcutTests`, a third class found to drive the same frame harness outside the collection; it has the same race,
  and DF1's purpose covers it.
- **DF3 (ADMIN): nothing to record.** In the Release build that every tier uses, the test takes 14.5 s, MEASURED in the Tier 3
  run of lane RR's integration (`uitests-full.trx`, 2026-11-12). That is under the slow list's 15 s threshold. The "about 29
  seconds" put to the Director was the lane's Debug figure. Under the recording rule (`scripts/tiered_validation.py
  --record-durations`) the test is not slow, so nothing is recorded. A later full run that measures it at 15 s or more records
  it automatically.
- **DF7, DF8, DF10, DF11 and DF9: lane OC (battle outcomes).**
  - Fighting as damage in both resolvers' "fought" report.
  - The tie in both resolvers: no winner; both armies keep what is left; routing units rejoin with lowered morale. The size of
    the decrease is a value in the morale proposal.
  - The Standoff tie; the defender as the attacked army.
  - The ambush trigger on the campaign map (DF9) and the deployment of its battle (DF9-R2).
- **DF12: lane BN (battalions).**
  - A battalion-size table for every unit type in the unit catalogue, with the Director's three examples fixed.
  - The battalion as the command unit in both resolvers and the War Sandbox.
  - The army cap of 25 battalions in the caps prototype.
- **DF6: lane AR (the AutoResolver for every domain).**
  - The AutoResolver's model for aircraft, vessels, crew-served guns and embarked troops, on battalions.
  - A throughput measurement against the Director's figures: 12 players, 40–50 air or naval battles between turns.
  - Parity with the TacticalResolver, measured against D-079 Q4's bar, which is still OPEN.
- **Order:** after the G2 final, one agent at a time: OC, then BN, then AR (AR models battalions), then the final-round
  verification and the pass's report.

## 6. Not decided here (OPEN)

- **DF5:** the kernel change request (lane RR's P1).
- **Items 13–27 of the list, except where §4 settles them:**
  - 15: reinforcement (with battalions, whether a battalion is refilled);
  - 16: the two sandbox scenarios that break the thirds;
  - 17: R6 (ships and drones under their caps);
  - 18: U-Q11 (the player's own assault order and the occupy, sack or raze choice);
  - 19: the parity bar (D-079 Q4);
  - 20: lane MOR3's two PROPOSALS;
  - 21–25: the research tree (the Longbow evidence is unavailable, D-086 DE3);
  - 26: lane RR's P2–P6;
  - 27: lane U's queue line.
- **New with DF12:**
  - whether DR24's per-family caps (for example "Ships 5") stand beside the 25-battalion army cap;
  - how DR25's numbers ("500–1,000 troops readily achievable", "50,000 troops difficult"; the same at every stage, D-082 DA3)
    meet battalions. With 25 battalions of 50 archers, an army holds 1,250 archers.
