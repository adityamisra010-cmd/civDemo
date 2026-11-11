# Two-level morale — PROPOSAL (M7 lane MOR, 2026-11-10)

**Status: PROPOSAL.** Nothing in this document is a Director decision unless it is quoted from one. Every formula and every number
is lane MOR's design under the delegation of D-082 §5 ("The morale model and every number: the size of the victory rise, decay and
recovery, unit against army morale, and how they feed each other. Delegated (DA2); a PROPOSAL in its lane."). Every interpretation
is a **READING** with both alternatives. It changes nothing in `Sim.*` or `Sim.Data` (D-079 §6, Q5 open); the prototype is in
`prototypes/m7-sprint1/Proto.M7/Morale/`, `Proto.M7/Tactical/LocalWinMorale.cs` and default-identity hooks in lane D's support world,
lane E's war economy and both battle resolvers. The lane report is `prototypes/m7-sprint1/reports/H-MOR.md`.
**[Lane MOR2, 2026-11-10: §11 adds D-083 DB1's entry cost (PROPOSAL; `Proto.M7/Tactical/EntryMorale.cs`, the settlement term in
`Morale/CampaignMorale.cs`); Annex A is rebuilt from the research dossier; lane MOR's open round-2 items are closed in the lane report
`prototypes/m7-sprint1/reports/H-MOR2.md`. Statements §11 supersedes are marked in place with dated notes.]**
**[MOR2 verification round 1, 2026-11-11: the round-1 findings MOR2-R1-01..13 are corrected in place (§11.9 lists where), two
READINGS are now labelled (§11.2: MOR2-R1, MOR2-R2), §11.12 adds lane S's siege battles; the round's record is H-MOR2 §13.]**
**[Lane MOR3, D-085 DD1, 2026-11-11: §12 adds the Director's rule that a battle without fighting changes no morale (each resolver's
fought report, the gated settlement, the in-battle part of the entry cost moved to the first fighting instant (PROPOSAL C1), the War
Sandbox, lane S's siege battles; READINGS MOR3-R1..R3). Statements §12 supersedes or narrows are marked in place (§12.9 lists where);
lane report `prototypes/m7-sprint1/reports/H-MOR3.md`.]**

**Authority.**
- [D-082](../d082-morale-caps-ties-director-answers-2026-11-10.md) **DA1** (DECIDED): "Winning a battle should raise morale too." With
  DR29, "a battle moves morale both ways: down through combat, up for the side that wins". **DA1-R (a)** (APPLIED reading): a win is
  the outcome the resolvers already report ("the other side routed, retreated, surrendered or eliminated; DA6 and DA7 for equal
  sides"), and "the rise applies to the winning side's formations that took part and to their army's campaign morale".
- D-082 **DA2** (DELEGATED, research input set): "Morale is designed at two levels: the army's morale on the campaign map and each
  unit's morale in battle", with Total War Saga: Troy named as the research reference, "a reference only: the design, numbers,
  names and text are our own, and nothing player-facing names another game" (D-069).
- D-082 **DA6** ("If two sides are equal, the defending side wins"), **DA7** with **DA7-R (a)** (an ambush between equal sides goes
  to the ambusher), **DA6-R** (the exact test of "equal" is delegated design; the TacticalResolver Standoff extension is a PROPOSAL).
- D-082 **§3**: lane D's rule "A battle never raises the campaign morale" is "superseded for the winning side by DA1. READING M is
  narrowed: DR29's decrease stands, and whether taking part alone costs morale (M (b)) stays OPEN."
- [D-079](../d079-m7-governance-reconciliation-2026-11-05.md) **DR29** ("Morale decreases when troops participate in combat.
  Support capabilities modify relevant effects."), **DR19** ("A battle ends when one side's units are dead, retreated, routed or
  surrendered."), **DR34** (morale among the AutoResolver's parity factors), **§4 D11** (a term in one resolver only "creates a
  parity gap by construction"), **DR53** (no reopening, no invented Director constraints).
- `CLAUDE.md` Laws 1–5; D-069 and [ip-originality.md](../ip-originality.md) for the reference annex.

Labels: **DD** quoted Director decision; **READING**; **PROPOSAL**; **MEASURED** (a test or run of lane MOR, named);
**INFERRED** (argued, not measured); in Annex A also **[CODE]**, **[SEARCH]**, **[KNOW]** (the research provenance labels) **[MOR2: and
the dossier's **[FETCH]**, which no claim of this pass carries (Annex A.0); MEASURED in §11 means a test or run of lane MOR2]**.

**Entities.** No new entity is proposed (charter §4.5): morale is a field of existing objects — of a category-A formation in battle
and of an army on the campaign — and `MoraleSettlementRow` is a record of a settlement, not an entity.

---

## 0. Summary

| Level | Today (prototype, before this lane) | This proposal | In the prototype |
|---|---|---|---|
| Unit (formation) in battle | casualty-driven loss, rout below a threshold, recovery while holding or rallying without losses; no rise for winning | keep all of it; add **"the fight it won"**: a formation whose enemy breaks under its blows gains morale scaled by the size of what it broke (§3.2); five more mechanisms proposed for later, each with its prerequisite (§3.3) | implemented in **both** resolvers through one shared function; coefficient **0 by default** (exact identity) until the parity battery is re-measured with it on (§3.2) |
| Army on the campaign | lane E's stock (micro units): recovery when fed, paid and loyal; hunger and arrears penalties; lane D: a battle lowers it by its net loss and **never raises it** | DR29's decrease for every participant **plus DA1's rise for the winning side's army**, floor(400,000 × B ÷ (B + W)); no extra defeat penalty for the loser (PROPOSAL (a)); the stock then moves toward a **baseline** at per-sim-year rates (§4) **[MOR2, 2026-11-10: plus D-083 DB1's entry cost, 50,000 for every army whose formation entered the battle, a third term in the same clamp (§11)]** **[MOR3, 2026-11-11, D-085 DD1: all three terms only in a battle that had fighting; a battle without fighting changes no army's morale, and its reported winner gains nothing (§12.3)]** | implemented and on; the baseline is a parameter whose default (the top of the scale) leaves lane E's model unchanged; a baseline below the top is measured as option (b) **[MOR2: the entry cost is on in the settlement; its in-battle part is 0 by default (§11.3)]** |
| The link | stand-in hand-off (lane D): start morale passed separately, unchecked | campaign → battle: start = floor(campaign × MoraleMax ÷ 1,000,000), nothing added; battle → campaign: decrease, rise, desertion, medical restore (§5) **[MOR3, D-085 DD1: the morale terms only for a battle with fighting, which the battle report now states (§12.2); desertion (persons) unchanged (READING MOR3-R3)]** | the mapping as a pure function; the start check stays M7.15's |
| What a win is | — | the side the battle report names; today: the side left when the other is eliminated or withdrew; DA6/DA7 as lane AP's resolvers will report them (§6) | `BattleWinner.Reported`; the hand-off carries the reported winner |

---

## 1. What the prototype does today (CITED from the code at `c536556`)

### 1.1 Unit (formation) morale in battle

- **Scale and start.** Morale lies on [0, MoraleMax], MoraleMax = 1,000 (`Tactical/BattleSetup.cs:49`, PLACEHOLDER); a formation
  starts at `FormationSpec.Morale`, or MoraleMax when none is given.
- **Casualty-driven loss (both resolvers, the same equation).** Each resolution step, a formation that lost k entities loses
  `ConditionMath.MoraleLoss(k, F, start, L)` = ⌊k × F × 100 ÷ (start × (100 + L))⌋, F = MoraleLossFullStrength = 2,000 (losing
  the whole start costs twice the scale), L = leadership 0..100 (`FormationCondition.cs:54`; tactical `TacticalBattle.cs:918`;
  auto `AutoResolver.cs:796`; vessel formations on their personnel, `TacticalBattle.DomainCombat.cs:558`).
- **Rout.** Below RoutThreshold = 300 every Active entity of the formation routs (`TacticalBattle.cs:923`; auto `:800`, where a
  trapped formation surrenders entirely and otherwise a fitted share surrenders). A routed formation never rallies.
- **Recovery.** TacticalResolver: at a pulse end, a formation not routed, with an Active entity and no casualty in the pulse gains
  HoldRecovery 20 under a Hold directive or RallyRecovery 60 under Rally, capped at MoraleMax, not at its start
  (`TacticalBattle.cs:1003`; vessels `TacticalBattle.Domain.cs:870`). AutoResolver: +HoldRecovery at a pulse end only for a
  formation that neither fought nor took damage in the pulse (`AutoResolver.cs:817`).
- **Withdrawal.** The knowledge-only AI orders Retreat below RetreatMorale 450; the AutoResolver's doctrine withdraws below
  DoctrineRetreatMorale 450.
- **Not modelled:** a rise for winning a fight; contagion from friendly routs; flank and rear attacks; a commander's presence or
  loss; morale loss from fire without casualties; fatigue; rallying after a rout.

### 1.2 Army morale on the campaign

- **Stock.** Lane E's `WarEconomyWorld` keeps one morale per army in micro units, 0..1,000,000, opening at 800,000
  (`WarEcon/WarEconomyWorld.cs:280`).
- **Pulse end** (`:929–936`, per-sim-year rates times the exact pulse length w, Law 3): +600 ‰/yr when the army is fed, paid and
  loyal; −2,400 ‰/yr × the hunger share; −60 ‰/yr per month in arrears; clamped to the scale. Morale drives desertion: the
  low-morale term 400 ‰/yr × (1 − morale) (`TroopLossRates.cs:20`; rates `WarEconTables.cs:230`, PROPOSAL, `reports/H-E.md` §4).
- **After a battle** (lane D, `Support/SupportMath.cs:103`, `SupportWorld.cs:303`): campaign morale after = max(0, before −
  ⌊max(0, start − end − restore) × 1,000,000 ÷ MoraleMax⌋), so "a battle never raises the campaign morale" (support proposal §3.3,
  D-R2-01). Superseded for the winning side by DA1 (D-082 §3).
- **Lane W's collapse** (`War/Outcomes/BattleCollapse.cs:108`): on a side that did not hold the field, the share (MoraleMax − end)
  ÷ MoraleMax of the routed men desert. Formation end morale reaches the campaign through this desertion and through lane D's net
  loss; nothing else.

---

## 2. What the reference shows, in one paragraph (Annex A has the evidence and its labels)

The reference keeps two separate things. ~~**On the campaign map there is no army morale number** [SEARCH]~~ **[MOR2, 2026-11-10: not
established either way — no source shows one, and its data defines an inherited army-morale family whose use is unknown (rebuilt
Annex A.2, data-14)]**: armies carry modifiers
from generals' titles and skills, unique items, faction mechanics (a per-army resource earned in engagements that raises morale
and movement; a hero's motivation that rises and falls with what the hero likes), and those modifiers enter the battle. **In
battle every unit has a hidden morale** [SEARCH] that losses, flanking and a commander's death lower, that beating an enemy unit or
hero raises ("a scaling morale buff"), and that ends in a rout; routed units can stop running and return, and can shatter
[SEARCH]. A battle's result returns to the campaign as experience, loot and faction resources, ~~not as a morale stock~~
[SEARCH, partly unverified] **[MOR2: "not as a morale stock" is not established: the data defines win and loss terms of the inherited
army family (data-14), use unknown]**. The genre patterns studied (Annex A.5) differ on exactly the campaign half: one keeps a party morale
that victories raise and hunger, unpaid wages and defeats lower, and that sets each soldier's starting morale; one keeps an army
morale stock that recovers monthly and that battle damage drains; one keeps discrete states changed by event-triggered checks.
**What this proposal takes is the pattern, not the content:** a stock at each level, an event that changes it (our victory rise,
our "fight it won"), and dynamics that bring it back; the numbers, names and equations are our own.

---

## 3. Unit (formation) morale in battle

### 3.1 What stays

The casualty-driven loss, the leadership coefficient, the rout threshold, the in-battle recovery and the withdrawal doctrine stay
as they are. They are mechanisms (Law 2), they are the same equation in both resolvers, and the parity battery is calibrated on
them (lane P, `reports/H-P.md`). Any change to them moves that battery.

### 3.2 Adopted: "the fight it won" (PROPOSAL; implemented in both resolvers; default 0)

**Rule.** When an enemy land formation G **breaks** at an instant — it was in the fight (not routed, with an Active entity) before
the instant's casualties and is not after them (routed, or no Active entity left) — each land formation F of the other side that is
still in the fight and **dealt damage to G within the last pulse length** gains

  rise(F, G) = ⌊c × Start(G) ÷ (Start(G) + Start(F))⌋, c = `TacticalRules.LocalWinMoraleRise` (morale-scale units),

applied after every rout of that instant. The rises of one winner add up and its morale is capped at MoraleMax once; a formation
that breaks at the same instant gains nothing; a friendly break gives nothing; an orderly withdrawal (escape) is not a break; and
a rout by the five-pulse rule is never evaluated (it happens at a pulse end, outside the resolution step that applies the rule, and
an idle formation was not hit within the window anyway). The larger the beaten formation against one's own, the larger the lift (an
equal formation gives c/2).

**[Verification round 2, 2026-11-10: (MOR-V2-01) a formation already out of the fight before the instant — routed earlier, or
gone in good order — does not break again at a later break, and a formation entering the fight is not a break; the "before" half
of the definition was untested until this round (MEASURED: judging a break on "out of the fight now" alone survived the whole
suite and, at c = 200 on lane P's tuning battery, added 79 duplicate (winner, broken) rows over the AutoResolver's 1,600 cases
and 128 over the TacticalResolver's 160 combined-arms and sniper-screen cases); it is now pinned on the shared function and in both resolvers (H-MOR §11). (MOR-V2-03) "Dealt damage" means
damage by any of F's weapons: until this round the TacticalResolver recorded only its Damage event's blows (melee and missiles), so a land formation's
crew-served weapons (lane N's field guns) never earned the rise (MEASURED: 12 of 12 probe battles in which guns broke infantry
logged no rise); their hits on land entities are now recorded too. The AutoResolver refuses battles with crew-served weapons, so
the change opens no parity gap.]**

**Why.** Seeing the enemy line in front break is the commonest source of confidence in a fight, and the reference raises a unit's
morale when it beats an enemy unit (Annex A.3); a fighting formation in our model can today only lose morale or recover it slowly
while idle. It is an event-driven change of the formation's stock (Law 2), needs no rate (Law 3 does not apply to an event), is
integer and order-independent (Law 5).

**One implementation for both resolvers.** `LocalWinMorale.Apply` is the rule; the TacticalResolver calls it at the end of every
Resolve instant (time in ms, window PulseMs) and the AutoResolver at the end of every step's casualties (time in steps, window
PulseMs ÷ StepMs steps), both after their rout loop. A term in one resolver only would open a parity gap by construction (D-079 D11).
**[Verification round 1, 2026-11-10 (MOR-V1-01): the shared function made the rule's arithmetic the same in both resolvers, but
the window each resolver passes, its time unit, and the exclusion of an orderly withdrawal were argued per resolver and pinned on
the pure function only (MEASURED: a window of 0 or of 1,000 pulses, the window in the wrong unit, the rule judged after the
tactical escapes, and a withdrawal in transit counted out of the fight each survived the whole suite). They are now pinned at the resolver level in both resolvers, on hits observed
from outside the rule's code: the window is one pulse of the winner's own blows ~~(a blow strictly before the break counts, one more
than a pulse back does not)~~ in milliseconds and in steps; an orderly withdrawal off the field is not a break; a formation
withdrawing or retreating in good order is still in the fight and can rise (H-MOR §10). The TacticalResolver calls the rule after
its rout loop and before its escape loop, which is what keeps an orderly escape from counting as a break.]**
**[Verification round 2, 2026-11-10 (MOR-V2-02): the struck clause overstated what round 1 pinned. Its resolver tests had a blow
strictly before the break (counts) and a volley about 1.8 pulses back (does not), so they held any tactical window from 8,000 to
107,999 ms and any AutoResolver window from 2 to 50 steps (MEASURED: windows of 58,000 and 90,000 ms and of 15, 29 and 45 steps
each survived the whole suite), and the two resolvers could have carried different windows with no failing test. The boundary is
now pinned in both: a blow exactly one pulse before the break (60,000 ms; 30 steps) counts, one step more (62,000 ms; 31 steps)
does not; with a 30,000 ms pulse (TEST INPUT) 30,000 ms and 15 steps count and 32,000 ms and 16 steps do not, so the window is the
battle's own pulse length in both resolvers (H-MOR §11).]**

**Default 0 in the prototype — why.** With c > 0 every battle in which a formation breaks under a surviving enemy changes: the
trace and state goldens of both resolvers, lane P's parity subsets and battery hash, and lane P's pass count of rule P1. Lane AP
is about to change the AutoResolver (DA6, DA7) and the parity gate itself is OPEN (D-079 Q4). Switching the rule on is therefore
proposed for the next re-measurement of the parity battery, together with lane AP's change, so that the battery moves once and is
measured once. PROPOSAL value when switched on: c = 200 (an equal formation broken: +100, the morale lost to 5 % casualties).
At c = 0 the rule is the exact identity: no hit tracking, no rise, nothing appended to any hash (MEASURED: every golden of both
resolvers unchanged, the full suite passes; `Default_IsTheIdentity_InBothResolvers`).

**Measured effect at c = 200 on lane P's tuning battery** (20 scenarios × seeds 1–80 = 1,600 cases per resolver; `morale-parity`
bench mode, Release; table in H-MOR §5.4). MEASURED:
- rule off: the tactical outcome digest is `90E58DCB…F65D6914`, the digest lane P recorded for the same battery
  (`reports/H-P.md` §4): the default is the identity over 1,600 tactical battles;
- rule on: 1,819 rises in the tactical engine, 1,779 in the AutoResolver; outcomes (winner, casualties, pulses, duration, end
  reason) changed in 64 tactical and 31 auto cases, the winner in 40 and 5 of them — all in the two scenarios with more than one
  formation per side (combined-arms, sniper-screen). In the one-formation-a-side scenarios the last enemy formation's break ends the
  battle at that instant, so only the winner's end morale changes;
- lane P's rule P1 (tactical reference against the auto candidate) passes 16 of 20 tuning scenarios both off and on, with the same
  verdict per scenario.
This is a measurement of the rule's effect, not a parity claim: the held-out acceptance battery was not run (§10).

### 3.3 Proposed for later, not implemented (each needs both resolvers and a parity re-measurement)

| Mechanism | Pattern seen | Our proposed mechanism | Prerequisite |
|---|---|---|---|
| Rout contagion | ~~the reference and the genre: a friendly rout nearby lowers morale (Annex A.3, A.5)~~ **[MOR2, 2026-11-10, MOR-V2-05: the cited annex rows held no rout-contagion evidence. Now: the reference's data defines a "friends routing" concern fed by the balance of enemy and friendly routs within set distances (data-06: CODE, definition only, verdict WEAK — the friendly half is supported, a rise from enemy routs is not); no report of it in play; genre: fleeing spreads to allies in one developer blog (official-42: SEARCH, WEAK). Annex A.3, A.5]** | a formation loses ⌊k × Start(routed) ÷ (Start(routed) + Start(own))⌋ when a friendly formation within its sight range routs | a proximity term in the AutoResolver's one-axis geometry (formation gap) |
| Rally of routed men | routed units stop running and return, with a window in which they do not rout again, and shatter on a second rout (Annex A.3) | a routed formation whose entities are out of enemy reach for a time regains Rally morale and returns to Active; a second rout makes its men Escaped for good | an engine state Routing → Active, and lane W's collapse categories (a returned router is Fit) |
| Commander presence and loss | a commander's death lowers the whole army's morale; a commander rallies (Annex A.3, A.4) | a commander entity's radius scales the morale loss; its death is an army-wide loss | a commander entity (generals as commanders, D-079 Q13: open) |
| Flank and rear | flank and rear attacks lower morale (Annex A.3, A.4) | the loss coefficient rises with the angle of attack | formation facing in both resolvers (not modelled) |
| Fatigue | ~~fatigue lowers morale (Annex A.4)~~ **[MOR2, 2026-11-10, MOR-V2-05: A.4 (the sibling's manual) has no fatigue content. Now: the reference's data defines three fatigue morale concerns and a table of stat multipliers per fatigue level, its morale rows unknown (data-07: CODE, CONFIRMED, definitions only), and fatigue built by activity deltas (data-17: CODE, CONFIRMED); no report for the reference links fatigue to morale; genre: fatigue feeds morale in two wargames (community-21: SEARCH, WEAK). Annex A.3, A.5]** | a fatigue stock from exertion, recovered at rest | a fatigue model (none yet) |
| Fire without casualties | ~~missile fire lowers morale (genre)~~ **[MOR2, 2026-11-10, MOR-V2-05: no source was named. Now: the reference's data defines being shot at, and artillery near misses and hits, as morale terms that can act before any loss (data-05: CODE, CONFIRMED, definitions only, no size); players count missile fire as a morale factor (community-08: SEARCH, WEAK); a ranged modifier lowers enemy morale (official-13: SEARCH, WEAK); genre: artillery fire and "volley shocks" (community-21: SEARCH, WEAK). Annex A.3, A.5]** | a loss per incoming volley | ~~**this is READING M (b) in another form ("taking part alone costs morale"), OPEN: not proposed here** (§8)~~ **[MOR2, 2026-11-10: READING M (b) is decided (D-083 DB1) as a cost for ENTERING a battle, once per formation (DB1-R2 (a)), implemented in §11; a loss per incoming volley stays a separate later mechanism (fire without casualties), PROPOSAL for later, needing a fire-exposure term in the AutoResolver's one-axis model]** |

---

## 4. Army morale on the campaign

### 4.1 The after-battle change (implemented)

For every army that took part in a battle, at the battle's end instant (the stand-in event, class CampaignProcess):

  decrease = ⌊max(0, start − end − restore) × 1,000,000 ÷ MoraleMax⌋  — DR29, every participant (lane D's net loss, unchanged);
  rise = won ? ⌊R × B ÷ (B + W)⌋ : 0  — DA1, the army of the side the report names the winner;
  ~~after = clamp(before − decrease + rise, 0, 1,000,000)  — one clamp over the sum, so the two terms have no order.~~
  **[MOR2, 2026-11-10, D-083 DB1: a third term, the entry cost E = 50,000 for every army whose formation entered the battle, and the
  decrease measured from the start less the in-battle entry charge: after = clamp(before − entry − decrease + rise, 0, 1,000,000), one
  clamp over the three terms (§11.2).]**
  **[MOR3, 2026-11-11, D-085 DD1: "for every army that took part" and "every participant" now hold for a battle that had fighting
  only; for a battle without fighting every term is 0 and after = before, whatever winner the report names (§12.3).]**

B = the persons of the beaten side and W = the persons of the winning side at the battle start (every army of it that took part);
the hand-off passes both. R = `MoraleRates.VictoryRiseMaxPpm` = **400,000** (PROPOSAL). Code: `Morale/CampaignMorale.cs`; the hook
in `Support/SupportWorld.cs` `OnBattle`; every settlement is a `MoraleSettlementRow` (new serialized row, `Morale/MoraleCodec.cs`).

### 4.2 The size of the rise (PROPOSAL)

The rise grows with the force beaten against one's own: beating an equal force gives R/2, a force twice one's size 2R/3, half
one's size R/3; it is positive for every win over somebody and below R always. The argument (INFERRED): a win is the more
heartening the less it was to be expected, and the strength ratio is the plainest expectation a soldier has. Worked numbers
(MEASURED by `DA1_TheWinnersArmyRises_TheLosersFalls_ByHand`; start 800, opening campaign morale 800,000):

| Battle | Decrease (DR29) | Rise (DA1) | After |
|---|---:|---:|---:|
| won against an equal force, ending at 700 | 100,000 | 200,000 | 900,000 |
| won against twice one's force, ending at 700 | 100,000 | 266,666 | 966,666 |
| won against half one's force, ending at 700 | 100,000 | 133,333 | 833,333 |
| won against six times one's force, ending at 700 | 100,000 | 342,857 | 1,000,000 (clamped) |
| lost, routed at 250 | 550,000 | 0 | 250,000 |

On the battle scale a 10 % casualty rate costs 200 (MoraleLossFullStrength 2,000), so a hard-fought, even win (≈10 % losses) is
~~about morale-neutral~~, a cheap win lifts, and a costly win lowers: "down through combat, up for the side that wins" (DA1).
**[MOR2, 2026-11-10, D-083 DB1: every row of the table above now also pays the 50,000 entry cost (the winners' afters 850,000,
916,666, 783,333 and 992,857 — 1,042,857 less 50,000, no longer clamped — and the loser 200,000: MEASURED,
`DA1_TheWinnersArmyRises_TheLosersFalls_ByHand` as updated), so the hard-fought even win nets about −50,000 rather than about 0, and a
lossless win over a force smaller than a seventh of the winning side nets a loss (§11.4).]**

Alternative sizes, considered and not proposed: a constant rise (ignores what was beaten); a rise from the enemy's losses (0 for a
win where the enemy withdrew intact, so "winning raises morale" would fail for that win).

### 4.3 The loser: no extra defeat penalty (PROPOSAL (a))

- **(a) PROPOSED, applied:** the loser takes DR29's decrease only. Its defeat is already in its battle morale — a formation that
  routed ended below the rout threshold, so its decrease is at least start − 300 — and in persons through lane W's desertion of
  the routed men of the side that lost the field. An extra term would count the defeat twice.
  **[MOR3, 2026-11-11, D-085 DD1: and only in a battle that had fighting; a loser of a battle without fighting takes nothing (§12.3).
  The premise "a formation that routed ended below the rout threshold" is not general — lane MOR2's round 2 measured routs at any
  morale and without loss (MOR2-R2-01, §11.8); those are the unfought routs DD1 now settles at 0.]**
- **(b) alternative:** a mirror term, decrease += ⌊P × W_loser ÷ (W_loser + B_winner)⌋ (losing to a weaker force hurts more).
  Consequence: an orderly withdrawal before any rout would cost morale too; one more coefficient to calibrate; no code today.

### 4.4 Toward a baseline at per-sim-year rates (Law 2, Law 3)

The rise is a change of the stock; nothing standing is added anywhere. After it, lane E's pulse-end update moves the stock toward
a **baseline** B₀ (`LogisticsRates.MoraleBaselinePerMille`): at or below B₀, a fed, paid, loyal army recovers by 600 ‰/yr × w, up to
B₀ and never past it; above B₀ (an excess a victory left), there is no recovery and the excess decays by
`MoraleElationDecayPerYear` (600 ‰/yr, PROPOSAL) × w toward B₀; the hunger and arrears penalties apply as before. w is the exact
pulse length (Law 3). Code: `WarEcon/WarEconomyWorld.cs` pulse end, marked [MOR].

- **(a) applied in the prototype: B₀ = the top of the scale (1,000).** This is lane E's model exactly (MEASURED: every lane E and
  lane S test unchanged and passing; the identity is also argued in the code comment: min(rec, cap − M) = rec whenever the clamp
  does not bind). The victory rise then shortens the army's recovery and cannot lift a fed, paid army above full morale: a winner
  already at the top gains nothing it can keep (MEASURED, `DA1_ABattleAtThePulseEndInstant_…`, baseline 1,000 cases).
- **(b) measured option: B₀ below the top, e.g. 800** (lane E's opening value, "steady"). A victory lifts the army above its steady
  level and the lift fades at 600 ‰/yr (MEASURED: `Baseline_AnExcessDecaysTowardIt_…` 1,000,000 → 988,334 → … → 800,000;
  `Baseline_TheDecayFollowsThePulseLength`: 11,666 per 7-day pulse, 23,333 per 14-day pulse). Consequence for lane E (INFERRED,
  not measured): a fed, paid army would sit at 800,000 instead of climbing to 1,000,000, and lane E's low-morale desertion term
  would add 80 ‰/yr to every such army unless the term is measured from the baseline instead of the top. This is a calibration
  change to lane E's economy, so it belongs with M7.14's calibration (A-38), not with this lane. **Recommended (PROPOSAL):** (b) at
  M7.14, with the desertion term re-based on B₀; (a) until then.
- **Limitation (stated, pinned):** the update runs at pulse ends over the whole pulse, so an excess created mid-pulse decays over the
  full pulse — first order in the pulse length, as lane E's recovery already is (`reports/H-E.md` §14).

### 4.5 "The winner's participating formations and their army" (DA1-R (a)) in a one-stock-per-army model

The prototype keeps one morale per army (lane E); its stand-in battle maps an army to one formation. For M7.15 (PROPOSAL, not
built): an army's campaign morale is the person-weighted mean of its formations' morale; a battle changes it by the participation
share times the participants' mean change: Δ_army = Σ_f persons_f × (rise − decrease_f) ÷ persons_army over the participating
formations (non-participants change by 0). In the one-formation stand-in the share is 1.
**[MOR2, verification round 1, 2026-11-11 (MOR2-R1-09): extended by §11.6, which was not marked here before: an entry term per
formation, Δ_army = Σ_f persons_f × (rise_f − decrease_f − entry_f) ÷ persons_army, and the persons counted AFTER the battle — a
choice that also re-weights each formation's DR29 decrease (a formation that lost most of its men weighs less); §11.6 states the
consequence and the alternative (persons at the battle's start).]**

### 4.6 Several battles of one army at one instant

They settle in the order they were scheduled (the event key's sequence, a stable integer tie-break), each from the morale the
previous one left; order matters only where the scale's top or bottom binds (MEASURED, `TwoBattlesOfOneArmyAtOneInstant_…`). Battles
of different armies at one instant settle in army-id order whatever order they were scheduled in (MEASURED, tie-dense,
`ManyArmiesBattlesAtOneInstant_…`). PROPOSAL for M7.15: settle one army's battles of one instant together (sum, one clamp).

---

## 5. How the two levels feed each other

- **Campaign → battle (M7.15 hand-off).** A formation enters a battle at its army's campaign morale on the battle scale,
  ⌊campaign × MoraleMax ÷ 1,000,000⌋ (`CampaignMorale.BattleStartMorale`); nothing else is added (no standing modifier). Leadership
  and experience act through their own coefficients (`ConditionMath`), supply through the attack factor. The stand-in still passes
  the start separately and nothing checks it against the army's stock (lane D's limitation 4; M7.15).
- **Battle → campaign.** DR29's decrease (the net loss start − end, less the medical restore), DA1's rise for the winner, lane W's
  desertion of the losing side's routed men, lane D's medical save; then lane E's pulse-end dynamics (§4.4). **[MOR2, 2026-11-10,
  D-083 DB1: and the entry cost for every army whose formation entered, its own term; the battle reports each formation's entry
  instant and in-battle charge (§11.6).]** **[MOR3, 2026-11-11, D-085 DD1: the three morale terms only where the battle had fighting;
  the battle also reports its first fighting instant (null: none), and a battle without fighting changes no army's morale (§12.2,
  §12.3). Desertion and the medical save move persons and are unchanged (READING MOR3-R3 (a)).]**
- **What does not cross.** The in-battle recovery's gain above the start is never carried (lane D's D-R2-01 fix stands, MEASURED
  again by the renamed lane D test). A local-win rise (§3.2, when switched on) is part of the formation's end state, so it reduces
  the winner's carried net loss as the in-battle recovery does, but it can never raise the campaign morale by itself (the net loss is
  floored at 0). Alternative (not proposed): carry the gross casualty-driven loss instead, which needs both resolvers to report it.

---

## 6. What counts as a win

The morale layer reads the winner the battle report names (`Morale/CampaignMorale.cs` `BattleWinner.Reported`) and decides nothing
itself (DA1-R (a)):

| Battle end (`BattleEndReason`) | Reported winner today |
|---|---|
| SideEliminated: the other side has no Active entity (dead, surrendered, routed or withdrawn) | the side that still has one |
| SideDisengaged (legacy rule): the other side withdrew | the side that stayed |
| BothEliminated, BothDisengaged, Standoff | none (−1) |

DA6 gives a battle between equal sides to the defender and DA7 an ambush to the ambusher (DA7-R (a)); lane AP implements both in
the AutoResolver, and the Standoff extension to the TacticalResolver is a PROPOSAL (DA6-R). **Until a resolver reports that winner,
no rise is given for those ends; once it does, the hand-off passes it and the rule is unchanged.** The prototype's tests pass the
winner such a report will name (MEASURED: `DA1_EqualSides_TheDefenderTheReportNames_Rises_ThoughNeitherSideHeldTheField`,
`DA1_AnAmbushBetweenEqualSides_TheAmbusherTheReportNames_Rises_NotTheDefender`). The field is not the winner: ~~under DA6 the defender
of an equal battle wins without holding the field alone, so~~ the stand-in's `HeldField` stays lane W's desertion input only.
**[Superseded in verification round 1 (MOR-V1-09): the struck premise is not in D-082 and is withdrawn. D-082 says nothing about
the field for DA6 in the AutoResolver; its only statement on the field is DA6-R's PROPOSAL for the TacticalResolver's Standoff end,
"the defender holds the field and wins, unless the attacker ambushed from concealment". The conclusion does not need the premise:
the morale layer reads the winner the report names (DA1-R (a)), whether or not that winner holds the field, and a losing army that
held the field takes no rise (MEASURED: `DA1_ALoserThatHeldTheField_DoesNotRise_TheReportDecides`).]**
**[MOR3, 2026-11-11, D-085 DD1: a win still is the winner the report names, but a win in a battle without fighting raises nothing
(DA1 narrowed, D-085 §3): the winner is recorded on the settlement row with no rise (§12.3).]**

---

## 7. The laws

- **Law 1.** The morale layer moves no person, good or coin; the collapse's flows are lane W's and lane D's ledger transfers.
- **Law 2.** Each rise is a change of a stock at an event (a battle won; an enemy formation broken); each return is lane E's
  dynamics toward a baseline. The coefficients R, c, B₀ and the decay rate are inside equations. Nothing is a standing buff.
- **Law 3.** Recovery and decay are per-sim-year rates times the exact pulse length (MEASURED for two pulse lengths). Event-driven
  changes carry no rate.
- **Law 4.** Nothing is keyed to a date or an era label; the pulse length is T2's War Pulse clock (computed Age state).
- **Law 5.** Integer micro units with `Int128` intermediates and floors; no IEEE type; arrays only, in index order; ties broken by
  the event key (time, class, priority, source id, sequence) or made irrelevant (a sum and one cap). The determinism scan is clean.

---

## 8. ~~READING M (b) — OPEN, presented, not decided~~ **[MOR2, 2026-11-10: decided by D-083 DB1 — (b) applies; see §11]**

"Morale decreases when troops participate in combat" (DR29).
- ~~**(a) applied:** the decrease is the casualty-driven loss (as the net loss of the battle). A battle won without loss raises the
  winner's morale by DA1's rise alone; a battle lost without loss changes nothing.~~ **[MOR2: superseded by DB1 (D-083 §3).]**
- **(b)** ~~**open:**~~ **[MOR2: DECIDED, D-083 DB1, read per battle, at the entry instant (DB1-R2 (a))]** **[MOR3, 2026-11-11:
  narrowed by D-085 DD1 to battles that had fighting; the in-battle part's instant is the first fighting instant (§12.4)]** taking part
  costs morale even without casualties (per battle, or per engaged pulse). It is a new term in both resolvers (or in the hand-off), moves the
  parity battery and the tactical goldens, and with DA1 a lossless win would net rise − cost. A "fire without casualties" term (§3.3)
  would be one form of (b).
~~The Director's D-082 left (b) OPEN; this proposal does not choose.~~ **[MOR2: D-083 decided it; the design is §11.]**

---

## 9. For the Director

No new question. ~~READING M (b) stays OPEN (§8).~~ **[MOR2, 2026-11-10: READING M (b) is decided (D-083 DB1); its design is §11, which
adds no new question either (§11.10).]** **[MOR3, 2026-11-11: D-085 DD1 bounds DA1 and DB1 (no morale change without fighting);
its design is §12, which adds no new question (§12.8).]** Everything else here — the size of the rise, the baseline and its decay, the
in-battle rule and its switch-on, the aggregation over formations — is delegated design (D-082 §5), presented for review.

---

## 10. Limitations

1. The hand-off is a stand-in (lane D's `SupportBattle`): one army is one formation; strengths are passed, not derived; the start
   morale is not checked (M7.15).
2. DA6 and DA7 winners are passed by the tests; no resolver reports them yet (lane AP).
3. Pulse-end granularity of the recovery and decay (§4.4).
4. The in-battle rule is off by default; its effect on parity was measured on the tuning battery only (H-MOR §5.4), never on the
   held-out acceptance battery.
5. The baseline below the top (option (b)) and the desertion re-basing are not calibrated (M7.14).
6. Vessel and aircraft formations are outside the in-battle rule (land formations only, in both resolvers). **[Verification
   round 2 (MOR-V2-03): until round 2 the TacticalResolver also, without saying so, left out the damage of a LAND formation's
   crew-served weapons (lane N's field guns); it now counts. The AutoResolver has no crew-served weapons (it refuses such battles).]**
7. The reference research could fetch no web page directly (Annex A.1): its evidence is search summaries and one downloaded
   schema ~~of table names~~; no game file was read. **[MOR2, 2026-11-10: the schema also holds the key lists with in-house
   descriptions, CODE at definition level (rebuilt Annex A.1); the second research pass reached no web page either (90 of 93 hosts
   refused).]**

---

## 11. Entering a battle costs morale (D-083 DB1) — PROPOSAL (lane MOR2, 2026-11-10)

**Status: PROPOSAL** (lane MOR2's design under D-083 §4 and D-082 §5; decided by nobody). The prototype is in
`Proto.M7/Tactical/EntryMorale.cs`, `TacticalRules.EntryMoraleCost`, the two resolvers, `FormationResult.EnteredAtMs` and
`EntryCharged`, `Morale/CampaignMorale.cs` (`MoraleRates.EntryCostPpm`, `EntryPpm`, the four-term `After`), lane D's stand-in
(`SupportBattle.Entered`, `EntryChargedInBattle`) and the settlement row (codec version 2). Lane report:
`prototypes/m7-sprint1/reports/H-MOR2.md`.
**[MOR3, 2026-11-11, D-085 DD1: everything in this section now holds for battles that had fighting only — a battle without fighting
changes no army's morale (no entry cost, no DR29 decrease, no DA1 rise), and the in-battle part charges at the battle's first fighting
instant, never in a battle without fighting (PROPOSAL C1). Codec version 3 (the row's `Fought`). §12; the statements it changes are
marked below.]**

### 11.1 Authority

- [D-083](../d083-entering-battle-costs-morale-2026-11-10.md) **DB1** (DECIDED, answers READING M (b)): "Simply entering battke should
  cost morale." With DR29 and DA1, "a battle changes morale through three terms: a cost for entering it, the decrease through
  combat, and the rise for the side that wins."
- **DB1-R1 (a)** (APPLIED): "a formation enters a battle when it is placed on the battlefield. That happens at the battle's start, in
  its deployment third (DR31), or later, at the instant it arrives as a reinforcement. A formation that never reaches the field, such
  as a reserve that is not committed, does not enter and pays nothing." Alternative (b): every formation of every army the battle
  involves pays at the start, whether or not it reaches the field.
- **DB1-R2 (a)** (APPLIED): "once per formation per battle, at its entry instant. The act of entering is what costs, not the time
  spent in the battle." Alternative (b): a cost growing with the time spent in battle (per engaged pulse), a drain like fatigue.
  **[MOR3, 2026-11-11, D-085: "once per formation per battle" stands; for the in-battle part the instant is now the later of the
  entry and the battle's first fighting instant — the timing D-085 §4 names ("charged at the battle's first fighting instant (and at
  entry for a later arrival)"), PROPOSAL C1 (§12.4). DB1 itself is narrowed by D-085 §3: "entering a battle costs morale where the
  battle had fighting".]**
- **DB1-R3 (a)** (APPLIED): "the entry cost and DA1's rise are separate terms, and both apply. A battle won without loss changes the
  winner's morale by the rise less the cost, which can be negative after beating a small force". Alternative (b): a win always nets a
  rise (the cost a winner pays is limited to its rise).
- **D-083 §4** (DELEGATED): the size of the cost, and where it is charged — "in the battle at the entry instant, which also weakens
  that battle; only in the after-battle settlement; both" — with the requirement: "The design must say how the cost reaches the
  army's campaign morale exactly." **DB2** (ADMIN): the reference neither supports nor contradicts DB1, "which is our own design
  (D-069)". D-082 §5 delegates every number of the morale model.

### 11.2 The rule (PROPOSAL; implemented)

1. **What entering is** (DB1-R1 (a)). A land formation enters the battle at the first instant one of its entities is placed on
   the battlefield: at the battle's start for every formation deployed on the ground (its deployment third); later, for a formation
   that starts aboard a transport, at the instant its first entity is put ashore (lane N's landing, `TransportOps`). That landing is
   the only way a land formation reaches the field after the start in the prototype: neither resolver has a reinforcement that
   marches onto the field at a set instant (INFERRED from the code: no arrival field in `FormationSpec`, no such state in either
   engine). When M7.15 adds one, the rule needs no change: the charge goes at the first placement. A formation that never reaches
   the field (it stays aboard; a reserve never committed) does not enter.
   **[MOR2, verification round 1, 2026-11-11 (MOR2-R1-02): what "placed on the battlefield" means for a formation that is CARRIED or
   BASED is an interpretation of DB1-R1 (a), unlabelled until now (vessels and aircraft were reported entered at the start, "they are
   part of the battle from its start", §11.6, struck). It is now READING MOR2-R2:**
   - **(a) APPLIED: on the field by the engine's own definition** — a land formation when its first entity stands on the ground (at
     the start, or put ashore), a vessel formation when present (the battle's start), an aircraft formation at the first take-off of
     one of its airframes (an airframe at its base is not on the field: `DEnt.OnField`); a later sortie is no new entry (DB1-R2 (a)).
     A formation that stays aboard, or whose airframes never leave their base, never enters.
   - **(b) at the battle's start for every formation the battle carries or bases** (aboard a vessel in the battle, at the battle's
     air base), whether or not it lands or takes off: no carried or based formation is ever "never entered", the entry cost reaches
     every army with a formation aboard or at base, and the prototype has no later arrival at all (lane N's landing becomes no entry).
   - Why (a): it is DB1-R1 (a) applied with one definition of "the field" for all domains, the one the engine already uses for sight,
     fire and movement; (b) is DB1-R1 (b)'s rule (every formation pays at the start) for the carried and based only. MEASURED (a):
     `Aircraft_AFormationThatNeverLeavesItsBase_NeverEnters_AndItsArmyPaysNothing`, `Aircraft_EntersAtItsFirstTakeOff_TurnExact`
     (three delivery instants), `Aircraft_ASecondSortieIsNoNewEntry` (`EntryCostRound1Tests`).**]**
     **[MOR2, verification round 2, 2026-11-11 (MOR2-R2-02): those tests had one domain formation each, or pinned only the lowest-id
     one, so a take-off recorded on the battle's first domain formation, or a start that entered only that one, survived the whole
     suite (MEASURED: 1488 of 1488 each). Now pinned with several in one battle: lane N's amphibious battle (vessel formations 10 and
     11 at the start; aircraft formations 13, 14 and 24 each at its own first take-off, 24 a pulse after the others; the riflemen 12 at
     their landing) and two aircraft formations of one side, only the second launched (turn-exact): `SeveralDomainFormations_…`,
     `TwoAircraftFormationsOfOneSide_…` (`EntryCostRound2Tests`).]**
2. **Once per formation per battle** (DB1-R2 (a)). The first placement records the entry instant ~~and charges~~; a formation that leaves
   the field (it re-embarks) and is placed on it again pays nothing more (MEASURED: `Tactical_AFormationThatLeavesTheFieldAndIs…`).
   **[MOR3, 2026-11-11, D-085 DD1: the first placement records the entry; the in-battle charge falls at the battle's first fighting
   instant, or at the entry when that comes later, and never in a battle without fighting (PROPOSAL C1, §12.4). The cited test's
   battle has no fighting, so it now pins the entry instants with nothing charged at either cost (MEASURED); "once, with re-entry" is
   pinned on the same swap made a fought battle (`C1_OncePerFormation_WithReEntry_InAFoughtBattle`).]**
3. **Separate terms** (DB1-R3 (a)). The settlement has three terms in one clamp:

       entry    = entered ? E : 0                                                     DB1 (E = MoraleRates.EntryCostPpm)
       decrease = floor(max(0, (start − entryInBattle) − end − restore) × 1,000,000 ÷ MoraleMax)    DR29 (lane D's net loss)
       rise     = won ∧ entered ? floor(R × B ÷ (B + W)) : 0                           DA1 (lane MOR's rise, unchanged)
       after    = clamp(before − entry − decrease + rise, 0, 1,000,000)              one clamp: no order between the terms

   `entryInBattle` is what the battle itself charged at the entry (§11.3); 0 while the in-battle part is off.
   **[MOR3, 2026-11-11, D-085 DD1: each of the three terms only where the battle had fighting — entry = fought ∧ entered ? E : 0,
   decrease = fought ? … : 0, rise = fought ∧ won ∧ entered ? … : 0 — so after = before for a battle without fighting (§12.3);
   `entryInBattle` is what the battle charged at its first fighting instant or the later entry (§12.4), 0 in a battle without
   fighting.]**
4. ~~**A formation that never entered takes no part.** Its army pays no entry cost, takes no DR29 decrease (its end morale equals its
   start: the stand-in refuses anything else) and no DA1 rise even when its side won (DA1-R (a): the rise is for "the winning side's
   formations that took part"; DERIVED from DB1-R1 (a), under which a formation that never reached the field did not take part).~~
   MEASURED: `DB1_AFormationThatNeverReachedTheField_PaysNothing_AndTakesNoRise`.
   **[MOR2, verification round 1, 2026-11-11 (MOR2-R1-01, MOR2-R1-04): the struck premise is false and the label was wrong.
   A formation that never reaches the field CAN be in the battle's combat: lane N's troops aboard a transport under fire are killed
   aboard and their formation's morale falls (MEASURED: 7 of 20 killed, 800 → 100, EnteredAtMs null), and the stand-in refused that
   real battle report. Now:**
   - **No entry cost** (DB1-R1 (a): it never entered).
   - **DR29's decrease of the morale the battle cost it** (DR29: "Morale decreases when troops participate in combat"; the troops
     killed aboard took part in the combat): (800 − 100) × 1,000 = 700,000 in the real battle; an untouched formation (a reserve
     never committed) has none. Lane W's collapse moves its dead and captured as for any formation (700 of 2,000 persons).
   - **The DA1 rise: READING MOR2-R1** (what "took part" in DA1-R (a), "the winning side's formations that took part", means once
     DB1-R1 (a) defines entering; D-082 does not define it): **(a) APPLIED: took part = entered** (placed on the field): a formation
     that never reached the field takes no rise even when it fought aboard and its side won; **(b): took part = entered, or engaged
     without reaching the field** (it took casualties or lost morale in the battle): such a formation of the winning side takes the
     rise too (still no entry cost). Consequences: under (a) a formation hit aboard takes DR29's decrease but not DA1's rise, so the
     two terms read "taking part" differently (DR29 for the combat, DA1 for the field); under (b) a winner's troops that never set
     foot ashore are lifted by a victory they did not fight on the field, and the rise needs one more input (a loss or a casualty)
     from the hand-off. (a) is kept as applied because it is the reading the round-0 code and tests already applied, and the rise
     is for the battle won on the field. This was labelled DERIVED; it is a READING.
   - MEASURED: `NeverEntered_TroopsKilledAboardTheirTransport_TheHandOffSettlesIt_DR29Applies_NoEntryCost` (the real battle) and
     `DB1_AFormationThatNeverReachedTheField_PaysNothing_AndTakesNoRise` (extended: a never-entered formation with casualties whose
     side won settles at 600,000 under (a), the same battle entered at 750,000).]**

### 11.3 Where the cost is charged (D-083 §4): the three options

| Option | The battle feels it | The cost reaches the campaign | Consequences |
|---|---|---|---|
| (i) in the battle only, at the entry instant | yes | **not exactly**: the in-battle recovery (HoldRecovery +20 per casualty-free pulse, RallyRecovery +60) can make it up, and lane D's net loss (start − end − restore, floored at 0) then carries none of it — MEASURED: 60 archers holding without loss enter at 750 and end at or above 800, so the net loss is 0 (`DB1_TheCostReachesTheCampaignExactly_…`) | fails D-083 §4's requirement unless the settlement changes |
| (ii) only in the settlement | no | exactly (its own term) | the two levels disagree about the same formation (the battle says it is at its start, the campaign that it paid); a later arrival has no consequence at its arrival instant; the War Sandbox, which has no campaign stock, never shows the cost at all (§11.7) |
| **(iii) both, one cost counted once (PROPOSED)** | yes | **exactly**: the settlement's own entry term, whatever the battle did; DR29's decrease is measured from the start less the in-battle charge, so the charge is not counted twice | the battle moves (both resolvers, one function, D-079 D11); the parity battery and the goldens move when it is switched on |

**Why (iii).** It is the reading of the two levels together (DA2): the formation's morale in battle and its army's campaign morale
pay the same cost for the same act. Counted once, by construction (DERIVED, with s the start, c the in-battle charge, L the
casualty-driven loss, H the in-battle recovery, so end = s − c − L + H on the battle scale, floored and capped): the settlement's
decrease is max(0, L − H − restore) on the battle scale and its entry term is E, so the campaign pays E + the uncovered loss — the
recovery can cover casualty loss (lane D's D-R2-01 rule, unchanged) but never the entry cost. Pinned with real battles and the charge
off and on: the campaign pays exactly E when the recovery made up the whole in-battle charge (MEASURED, four cases).
**[MOR2, verification round 1, 2026-11-11 (MOR2-R1-12): those four real battles end at or above their start, where DR29's base
(start or start − c) changes nothing, so "counted once" was pinned only by a hand-made stand-in row. It is now pinned in real battles
with losses too, in both resolvers: 30 heavy infantry at 800 against 40 with the charge on, ending well below 750; the settlement's
decrease is exactly (800 − 50 − end) × 1,000, not (800 − end) × 1,000 (the charge counted twice) nor (700 − end) × 1,000 (MEASURED:
`CountedOnce_InRealBattlesWithLosses_DR29IsMeasuredFromTheStartLessTheCharge`, two seeds); and a formation that enters with less
morale than the cost (charged all of it, 30 of 30) is settled with DR29 measured from 0 (`ClampAtZero_…`).]**
**[MOR3, 2026-11-11, D-085 DD1: `CountedOnce_…` passes unchanged (its battles are fought; the charge now falls at their first
fighting instant). `ClampAtZero_…`'s battle had no fighting, so DD1 settles it at 0 and charges nothing; it is re-pinned on a fought
battle (lane MOR2's three-formation battle with the formation at 30, charged 30 of 30 at the first fighting instant, 7,506,000 ms;
its side now wins, so the settlement adds the rise: 180,000; MEASURED), the DR29-from-0 boundary unchanged.]**

**The prototype's default is staged.** The settlement term is **on** (E = 50,000): DB1 is decided, and its campaign part is the level
the Director's question was asked at ("a battle won without losses only raises morale"). The in-battle part is implemented in both
resolvers through one function (`EntryMorale.Enter`) and is **0 by default**: with it off every golden of both resolvers and lane
P's battery digest are unchanged (MEASURED: the full suite passes with no golden edited; the tuning battery's off digests equal lane
P's, §11.6). PROPOSAL: switch it on (50 on the battle scale, the image of E at MoraleMax 1,000:
`CampaignMorale.EntryBattleScale`) at the next re-measurement of the parity battery, together with lane MOR's "fight it won"
(c = 200, §3.2), so the battery moves once and is measured once (D-079 Q4 OPEN, D11). Until then the prototype behaves as option (ii):
the campaign pays exactly, the battle does not feel it.

**Does the battle itself feel it?** When the in-battle part is on, yes: ~~the charge is the formation's morale from its entry instant,
before anything reads it. MEASURED in both resolvers: a formation that starts at 480, above the withdrawal line 450 (the
knowledge-only AI's RetreatMorale, the AutoResolver's doctrine), enters at 430 and withdraws at its first decision; with the part off
it stays and fights (`BothResolvers_WithTheInBattlePartOn_TheBattleFeelsIt_…`).~~ By default (off), no.
**[MOR3, 2026-11-11, D-085 DD1 and §4 ("it must not change morale in a battle that turns out to have no fighting"): the struck
behaviour changed morale before any fighting — the charge itself sent the formation away from a battle that then had none. Under
PROPOSAL C1 (§12.4) the battle feels the charge from its first fighting instant: a formation at 480 that holds is charged to 430 at
the battle's first attack (one arrow, which misses) and withdraws at its next decision; the formation of the struck test, no longer
charged at its entry, advances and fights (MEASURED, both resolvers: the test is rebuilt as
`BothResolvers_WithTheInBattlePartOn_TheBattleFeelsIt_FromTheFirstFightingInstant_…`).]**

### 11.4 The size (PROPOSAL; D-082 §5 delegates every number)

**E = 50,000 micro units on the campaign scale; 50 on the battle scale** (MoraleMax 1,000). The reasoning, each relation DERIVED from
the prototype's numbers:

- **Against DA1's rise** (R = 400,000; rise = floor(R × B ÷ (B + W))): E is an eighth of R and a quarter of the rise for beating an
  equal force. A lossless win nets rise − E: +150,000 over an equal force, +83,333 over half one's size, **0 at exactly a seventh**
  (B ÷ W = 1/7: the rise is 50,000), −7 just below (W = 7,001 against B = 1,000), −30,953 over a twentieth. **A lossless win over a
  force smaller than a seventh of the winning side nets a loss** (DB1-R3 (a): "negative after beating a small force"). MEASURED, by
  hand: `DB1_ALosslessWin_NetsTheRiseLessTheCost_Exactly` (six cases, the boundary included).
  **[MOR3, 2026-11-11, D-085 DD1: a lossless win in a battle WITH fighting (the winner struck, or was struck at); a win without any
  fighting nets 0, whatever the sizes (§12.3).]**
- **Against the army's recovery** (lane E: 600 ‰ per sim-year for a fed, paid, loyal army below the baseline): E is exactly 30 days
  of recovery (600,000 × 30 ÷ 360). A lossless battle costs an army a month of rest; an army that fights an easy battle every week
  against forces a tenth of its size (rise 36,363) loses about 13,637 per battle and recovers 11,666 per 7-day pulse (INFERRED from
  the two rates), so endless easy skirmishing slowly wears it down rather than lifting it to the top.
- **Against MoraleMax** (when the in-battle part is on): 5 % of the scale; a fresh formation ~~enters at 950~~ **[MOR3, 2026-11-11,
  PROPOSAL C1: is charged to 950 at the battle's first fighting instant (§12.4)]**; on the battle scale E is
  the morale lost to 2.5 % casualties (MoraleLossFullStrength 2,000).
- **Against the rout threshold** (300) **and the withdrawal line** (450), when the in-battle part is on: ~~entering alone~~ **[MOR3:
  the charge]** routs no
  formation that starts at 350 or more and makes none withdraw that starts at 500 or more. In the TacticalResolver a formation
  starting in [300, 350) then routs, as one starting below 300 already does today; the AutoResolver withdraws such a formation in
  good order instead (MEASURED on one test-input battle at each of the starts 280 and 299 without the cost and 320 and 349 with it,
  a pre-existing difference, §11.10): the cost moves both lines by 50 (MEASURED for the withdrawal line, §11.3). The campaign
  consequence: an army whose campaign morale is below 500,000 withdraws ~~at once~~ when an AI commands it.
  **[MOR3, 2026-11-11, D-085 DD1, PROPOSAL C1 (§12.4): the charge now falls at the battle's first fighting instant, not at the entry,
  so these consequences come at that instant, and never in a battle without fighting: a formation in [300, 350) is charged below the
  rout threshold when the battle's first attack is resolved, and one below 500 withdraws at its next decision after it, not at once
  (MEASURED for the withdrawal line: §11.3's rebuilt test). Lane MOR2's measurements in this bullet were made with the charge at the
  entry and were not repeated under C1 (§12.10 item 5).]**
  **[MOR2, verification round 1, 2026-11-11 (MOR2-R1-08): and a consequence in PERSONS that was not reported: in the TacticalResolver
  the formation that the cost routs leaves the field routed, and lane W's collapse then moves the share (1,000 − end) ÷ 1,000 of a
  routed formation of a side that did not hold the field to the deserters — 1,402 to 1,500 of 2,000 persons for starts 300 to 349
  with the cost on, none with it off; the AutoResolver withdraws the same formation in good order (all fit, no deserter) with or
  without the cost (MEASURED, a temporary probe on the battle above, scratchpad `mor2-fix1/probe-P8.txt`). Entering alone can thus
  cost an army most of its men in one resolver and none in the other: a parity gap in persons that rule P1, which compares the
  resolvers' outcomes, does not measure. It is the pre-existing rout-versus-withdrawal difference (§11.10), widened by the cost.]**
- **With DR29 together** (DA1's worked table, §4.2, now with E): a hard-fought even win (≈10 % losses, decrease 200,000) nets
  −50,000 instead of about 0; a cheap win (5 %) +50,000; a lossless loss −50,000; a routed loser −E − (start − end) × 1,000.
  **[MOR3, 2026-11-11, D-085 DD1: the lossless loss and the routed loser in a battle with fighting; in a battle without fighting
  both settle at 0 (§12.3).]**

**Considered and not proposed:** E = 25,000 (a lossless win nets a loss only below a fifteenth: DB1-R3 (a)'s "small force" would be
very small); E = 100,000 (below a third: beating a third of one's strength without a loss would cost morale, and an even lossless win
would lift only 100,000); a cost scaled by the formation's size or by the enemy's (DB1-R2 (a) charges the act of entering, once; the
army's share comes from the aggregation, §11.6); a cost per engaged pulse (DB1-R2 (b), not applied).

**What the reference research says** (dossier, Annex A; reference only, D-069). The reference's data defines **no** morale term for
entering a battle, deploying or time in battle among its 129 battle-morale and 36 army-morale keys (data-01, data-14, data.not_found:
CODE, CONFIRMED; Q1-a ESTABLISHED at definition level); whether the game charges one in play is UNKNOWN (Q1-b), so the reference
neither supports nor contradicts DB1 (DB2). Its battle morale is described as a computed value (a starting point plus situational
terms, moved toward each tick: data-01), in which a cost would be a conditional term, not a drawn-down stock; our cost is an event
change of a stock (Law 2), not a copy of that form. Patterns for both readings exist only outside the reference: a genre game charges
deployed troops a daily morale cost (official-40, SEARCH, genre, WEAK) — the per-time form, DB1-R2 (b), not applied — and two sibling
titles have a minimum morale loss per auto-resolve round (CODE, sibling; absent from the reference). The size has no support in the
evidence: no value of any key is known (the schema holds none). A genre player's warning about "feedback loops from the linkage of
unit morale to combat victories" (community-19, WEAK) bears on DA1 with DB1: the entry cost damps the loop by which a strong army
gains morale from easy wins. The final number belongs to the M7.14 calibration (A-38).

### 11.5 The laws

- **Law 1.** ~~Nothing moves a person, good or coin; a formation that never entered moves nobody (its collapse is all fit).~~
  **[MOR2, verification round 1, 2026-11-11 (MOR2-R1-01, MOR2-R1-08): both halves overstated. The entry cost moves no person, good or
  coin by itself, and every person still moves only by lane W's and lane D's ledger transfers (conservation holds, exact). But (i) a
  formation that never entered can lose men aboard its transport, and lane W's collapse moves them (MEASURED: 700 of 2,000 to the
  dead, `NeverEntered_TroopsKilledAboard…`); and (ii) with the in-battle part on, the charge can rout a formation in the
  TacticalResolver, and lane W's collapse then moves most of its men to the deserters, where the AutoResolver moves none (MEASURED,
  §11.4): through the end morale, the cost does change how many persons move.]**
- **Law 2.** Two event changes of stocks, at events: the formation's morale at its entry instant; the army's campaign morale at the
  settlement. Nothing standing; the coefficient E sits inside the settlement equation.
  **[MOR3, 2026-11-11, D-085 DD1: the formation's morale at the battle's first fighting instant or its later entry (PROPOSAL C1); both
  changes happen only in a battle that had fighting (§12.4, §12.7).]**
- **Law 3.** An event, not a rate (DB1-R2 (a)): no part of it integrates over time. The army's later recovery is lane E's per-year
  rate times the exact pulse length, unchanged.
- **Law 4.** No date or era: the cost is the same at every stage (DA3's rule for DR25 does not apply to it; nothing here reads an
  era).
- **Law 5.** Integers (micro units; battle scale), `Int128` intermediates, floors and clamps; no IEEE type; formations and rows in
  ascending formation id; same-instant settlements in the event key's order (army id, then scheduling sequence): tie-dense, all 120
  scheduling orders of five armies (MEASURED). The determinism scan is clean.

### 11.6 The battle → campaign hand-off and the settlement row

- **What the battle reports** (both resolvers): `FormationResult.EnteredAtMs` (null = never entered) and `EntryCharged` (the
  in-battle charge it took; 0 while off). They are report fields, not part of `ResultHash` (they follow from the trace; every earlier
  result hashes as before); the tactical and auto state hashes carry the entry record when the in-battle part is on.
- **What the settlement reads** (lane D's stand-in; M7.15 builds the real hand-off): `SupportBattle.Entered` = EnteredAtMs is set;
  `SupportBattle.EntryChargedInBattle` = EntryCharged. The row `MoraleSettlementRow` (codec version 2, 110 bytes) records Entered,
  EntryInBattle and EntryPpm, so the after value is recomputable by hand from the row.
  **[MOR3, 2026-11-11, D-085 DD1: the battle also reports `BattleResult.FirstFightMs` (null: no fighting), the settlement also reads
  `SupportBattle.Fought` (the battle's answer, `BattleFighting.SettlesAsFought`), and the row records it: codec version 3, 111 bytes
  (§12.2, §12.3).]**
- **Several formations in one army** (§4.5, PROPOSAL extended, not built): Δ_army = Σ_f persons_f × (rise_f − decrease_f − entry_f)
  ÷ persons_army, with entry_f = E for a formation that entered and 0 otherwise, and the persons counted AFTER the battle (the men who
  remain with the army), so a formation that was annihilated carries nothing into its army's average (§11.8).
  **[MOR2, verification round 1, 2026-11-11 (MOR2-R1-09): counting the persons after the battle also re-weights each formation's DR29
  decrease, which was not discussed. Two formations of 1,000 men; one loses 900 men and 700 on the battle scale, the other nothing;
  taking DR29's decrease alone (no rise, the entry terms left aside): counted after the battle the army falls by 100 × 700,000 ÷ 1,100
  ≈ 63,636 (the few survivors' loss, averaged over the men who remain); counted at the start, by 1,000 × 700,000 ÷ 2,000 = 350,000
  (DERIVED, by hand). After-battle counting is the person-weighted
  mean of the men who remain (the dead carry no morale), but it makes a heavy loss weigh LESS in the army's morale than a moderate
  one. Alternative (not proposed here, open for M7.15): persons at the battle's START for the decrease and the entry term (every man
  who fought shares the shock and the cost), persons AFTER it for the rise (the dead are not lifted). §4.5 is marked in place.]**
- **Vessels and aircraft.** ~~Reported as entered at the battle's start (they are part of the battle from its start; an aircraft's later
  sorties are not entries);~~ **[MOR2, verification round 1, 2026-11-11 (MOR2-R1-02): that was an unlabelled reading and it charged
  the army of an aircraft formation that never left its base. Now READING MOR2-R2 (a) (§11.2 item 1): a vessel formation enters at
  the battle's start, an aircraft formation at its first take-off (a later sortie is no new entry), and one whose airframes never
  leave their base never enters (EnteredAtMs null).]** Outside the in-battle charge, which is for land formations only, as lane MOR's
  in-battle rule; their armies pay the settlement term like any other army whose formation entered.
- **Measured on lane P's tuning battery** (20 scenarios × seeds 1–80; `entry-parity` bench mode, Release; H-MOR2 §5):
  - **off** (the defaults): the tactical outcome digest is `90E58DCB…F65D6914`, the one lane P recorded (`reports/H-P.md` §4), and the
    AutoResolver's `2AC31207…A0B68F31`, lane MOR's off digest (H-MOR §5.4): the in-battle default is the identity over 1,600 battles
    in each resolver (MEASURED);
  - **on at 50, alone**: 3,440 entry rows in each resolver (every land formation of every case, once each); outcomes changed in 649
    tactical and 598 auto cases, the winner in 134 and 103; **rule P1 passes 16/20 off and 14/20 on** (leadership-edge newly fails on
    duration, ford-crossing on the winner distribution). Digests on: tactical `413CD6C2…88E29A18`, auto `048A8EE5…FA33101E`;
  - **on at 50 with lane MOR's "fight it won" at c = 200** (the joint switch-on proposed): outcomes changed in 634 and 567 cases, the
    winner in 133 and 74; **rule P1 13/20** (leadership-edge and sniper-screen on duration, ford-crossing on the winner). Digests on:
    tactical `D4784D79…E2E26137`, auto `0FE5ED33…6DA15389`.

  INFERRED: the AutoResolver's coefficients were fitted with no entry charge (lane P's model 2, "re-fit, never hand-tune" whenever a
  tactical placeholder moves), and the entry charge lowers every start morale, so switching it on belongs with a re-fit and the
  battery's re-measurement; this lane tunes nothing and claims no parity (D-079 Q4 OPEN; the held-out acceptance battery was not
  run). These numbers are a measurement of the rule's effect, not acceptance evidence.
  **[MOR3, 2026-11-11, D-085 DD1: the "on" numbers above were measured with the charge at the entry. Under PROPOSAL C1 the charge
  falls at the first fighting instant and never in a battle without fighting, so the "on" digests, row counts, outcome and winner
  changes and rule P1 are re-measured in §12.4; the "off" digests are unchanged (MEASURED).]**

### 11.7 Lane D's War Sandbox: how Quick Battle charges the cost

The sandbox runs the campaign's resolvers with the campaign's rules (DR30: "Campaign resolver rules remain unchanged") and keeps no
campaign morale stock (its isolation, D15: a Campaign Encounter copies a value snapshot and writes nothing back). So:

- **The in-battle part** charges every sandbox formation exactly as in a campaign battle, through the same resolvers: nothing while it
  is 0 (the prototype's default); once it is switched on, each formation pays ~~at its entry instant (a Quick Battle formation starts at
  full morale unless edited, so it enters at 950)~~ **[MOR3, 2026-11-11, D-085 DD1, PROPOSAL C1: at the battle's first fighting
  instant (a Quick Battle formation starts at full morale unless edited, so it is charged to 950 then), and nothing in a Quick Battle
  without fighting (MEASURED, §12.5)]**.
  **[MOR2, verification round 1, 2026-11-11 (MOR2-R1-11): "once it is switched on" holds only if the switch-on is a change of the
  rules' DEFAULT. The code stated two mechanisms (`TacticalRules.EntryMoraleCost`: "PROPOSAL value when switched on: 50"; and
  `CampaignMorale.EntryBattleScale`: "the value the M7.15 hand-off would set" per battle), and the sandbox builds
  `rules ?? new TacticalRules()`, so a per-battle setting by the campaign hand-off would never reach a Quick Battle. PROPOSAL, now
  stated once (both doc comments corrected): **the switch-on changes `TacticalRules`' default `EntryMoraleCost`** to
  `EntryBattleScale(MoraleRates.EntryCostPpm, MoraleMax)` = 50 — one value for the campaign's battles and the sandbox alike (DR30,
  "Campaign resolver rules remain unchanged"); `EntryBattleScale` derives that value and lets M7.15's hand-off check it; it is not a
  per-battle setting. Alternative (not proposed): the hand-off sets it per battle, and the sandbox then needs its own setting to
  follow DR30. MEASURED: a Quick Battle charges nothing at the default and 50 per formation when its rules carry 50, in the
  AutoResolver (AI against AI) and the TacticalResolver (a player side) (`WarSandbox_QuickBattle_ChargesTheInBattlePartThroughItsRules_NothingAtTheDefault`).]**
- **The settlement term** has nothing to settle in the sandbox: there is no army stock to lower. The sandbox's casualty accounting
  (persons only) is unchanged.
- **PROPOSAL (not implemented):** the sandbox's result inspection lists each formation's entry charge (the resolvers' entry log) and,
  for a Campaign Encounter, shows as a preview the settlement the same battle would give its armies in the campaign, computed with
  the same `CampaignMorale` functions and written nowhere. **[MOR3, 2026-11-11, D-085 DD1: the preview applies DD1 with the same
  functions — an encounter without fighting previews no change for either army — and the inspection shows the battle's first fighting
  instant or "no fighting" (§12.5).]**
- **Consequence of the staging (§11.3):** until the in-battle part is switched on, a sandbox battle shows nothing of DB1 — one more
  reason to switch it on at the battery's re-measurement. **[MOR3: and, switched on, a sandbox battle without fighting shows nothing of
  it either, by DD1 (§12.5).]**

### 11.8 The mutual-elimination win (lane AP's observation AP-R1-14)

Under DA6 a defender whose battle formations were all eliminated in the same instant as the attacker's (BothEliminated) is the
reported winner, and DA1 raises its army (lane AP MEASURED 900,000 against 700,000 with stand-in numbers; with DB1's cost 850,000
against 650,000, MEASURED: lane AP's updated test).
**[MOR3, 2026-11-11, D-085 DD1 (D-085 §3: lane MOR2's PROPOSAL (a) "is superseded where no fighting took place. Its alternative (c')
is adopted in a wider form: no rise and no cost"): what follows stands for a FOUGHT mutual elimination or rout (lane AP's duel and the
fought mutual rout still settle as below; MEASURED, their tests pass unchanged). For a battle WITHOUT fighting it is superseded: no
army's morale changes, whoever the report names (§12.3). The prototype applies DD1, not (a), to unfought battles; (b), (b′), (c) and
(c′) below are kept as the record of what was weighed.]**

- **PROPOSAL (a), applied: the rise does not depend on what the winner has left.** (1) Morale is a per-man quantity: a victory lifts
  each man who remains; the dead do not dilute the elation of the living, and the shock of the losses is DR29's own term, already in
  the settlement. (2) When nobody is left there is nobody to lift: in the aggregation over formations (§11.6) the persons are counted
  after the battle, so an annihilated formation carries no rise into its army; in the one-formation stand-in such an army has no men
  (MEASURED with lane AP's real duel, both entities killed in one instant, through the collapse: the defender's army ends at
  clamp(800,000 − 50,000 − 800,000 + 200,000) = 150,000 with 0 men left, the attacker's at 0: `DA1_AMutualEliminationWin_…`).
  ~~(3) A defender whose formations routed in the same instant as the attacker's ended below the rout threshold, so DR29 takes at least
  (start − 300) × 1,000 and dominates the rise.~~ (4) DA1-R (a) reads DA6's and DA7's winners as wins ("DA6 and DA7 for equal sides"),
  and a rule that zeroed their rise would make those wins raise nothing, against DA1.
  **[MOR2, verification round 1, 2026-11-11 (MOR2-R1-03): argument (3) is false in general and is withdrawn. ~~A formation that routed
  ends below 300, so DR29 takes at least (start − 299) × 1,000; against an equal force the rise is 200,000 and the entry cost 50,000,
  so an army whose campaign morale is its battle start × 1,000 (the hand-off's mapping, §5) ends the settlement at 150,000 + end ×
  1,000 whatever its start (449,000 for an end of 299; no medical restore, no clamp), and a defender whose formation routed at 299
  nets a RISE from any start below 449 on the battle scale (campaign morale below 449,000): +49,000 from 400, 0 from 449, −51,000
  from 500; up to +149,000 for a start of 300 (MEASURED: `ProposalA_AMutualRoutWonUnderDA6_NetsARiseFromALowStart_ByHand`; DERIVED
  for the bound). This is the case lane AP raised (AP-R1-14): under (a), an army of low morale whose men ran in the same instant as the
  enemy's gains morale when DA6 names it the winner. (a) stays the PROPOSAL on arguments (1), (2) and (4) —~~ (4) is the decisive one:
  DA1-R (a), as recorded, counts DA6's and DA7's winners as winners, and a rule that withholds their rise departs from that reading —
  and the consequence ~~above~~ is now stated with it, for the Director to see.]**
  **[MOR2, verification round 2, 2026-11-11 (MOR2-R2-01): the round-1 note rested on a false premise, and the consequence it gave the
  Director was wrong; the struck part is replaced by the following (MEASURED in real battles through the hand-off, `EntryCostRound2Tests`).**
  - **The premise.** A formation that routs does not have to end below 300, and DR29 does not have to take anything from it. Lane TR's
    inactivity rule (DIR5 §2.6, the default) routs a formation that has been hidden and stationary for five pulses at ANY morale, and
    in those five quiet pulses HoldRecovery lifts it above its start (320 → 420, 500 → 600, 800 → 900); a formation that starts below
    the rout threshold routs at once without loss (250, 299); and with the in-battle part on, the entry charge itself routs a start in
    [300, 350) at once (320 → 270, 349 → 299). In each case the end is not below the start less the charge, so DR29's decrease is 0.
  - **The consequence of (a).** When both sides rout in one instant with no blow struck, the battle ends BothEliminated and DA6 names
    the defender (in the TacticalResolver through lane AP's Q-AP4 (a), applied pending the ruling); its army nets DA1's rise less DB1's
    cost, 200,000 − 50,000 = **+150,000 against an equal force, from ANY start** (MEASURED for starts 250, 299, 320, 500 and 800 with
    the in-battle part off and 320, 349 and 500 with it on: `ProposalA_AnUnfoughtMutualRout_WonUnderDA6_…`; above a campaign morale of
    850,000 the top clamp binds), not a rise only "from a start below 449, up to +149,000" (struck). The attacker's army pays the entry
    cost alone (−50,000). The same holds in the AutoResolver, where DA6 is applied as DA6-R records it (not pending): two formations
    below its withdrawal line (450), or charged below it (480 → 430), withdraw in good order at their first decision, and when both
    leave the field at the same step (placed symmetrically) the battle ends BothEliminated with the defender the winner: +150,000 again
    (`…_AnUnfoughtMutualWithdrawal_InTheAutoResolver_…`). So, with the in-battle part on, DB1's cost can itself produce a "win" that
    lifts the defender by three times the cost. In the TacticalResolver neither side held the field (HoldsField −1), so lane W's
    collapse deserts the share (1,000 − end) ÷ 1,000 of the routed men of BOTH sides: the "winner" rises by 150,000 while, from a start
    of 500, 800 of its 2,000 men desert (MEASURED, the same test; in the AutoResolver's withdrawal nobody routed, so nobody deserts).
  - **A fought mutual rout is different.** Under the knowledge-only AI the same two formations close and fight (3 of 20 killed on each
    side, damage dealt by both) and rout in one instant at 200: DR29 takes 300,000, more than the rise, and the defender's army nets
    −150,000 (MEASURED: `ProposalA_AFoughtMutualRout_WonUnderDA6_DR29OutweighsTheRise`). Round 0's argument (3) is true of such a
    battle only, and round 1's +49,000 / 0 / −51,000 from 400 / 449 / 500 (the by-hand row of a formation whose morale FELL to 299)
    is one fought case, not the general one.
  - **The arguments for (a), re-examined.** (1) ("a victory lifts each man who remains") and (2) ("when nobody is left there is nobody
    to lift") do not reach the unfought mutual rout: its men remain (lane W returns the share end ÷ 1,000 of the routed), and none of
    them struck a blow. Only (4) does: DA1 is DECIDED ("Winning a battle raises the winning side's morale") and DA1-R (a) counts DA6's
    and DA7's winners as winners. Each alternative below gives some of those wins no rise, an exception to a decided rule for a class
    of wins; the lane does not make that exception on its own reading.
  - **PROPOSAL (a) is kept, on (4) alone; the prototype applies it, unchanged.** Its consequence is stated for the Director: under
    DA1, DA1-R (a) and DA6 as they stand, a defender whose formations ran, with no blow struck, in the same instant as the attacker's
    gains +150,000 against an equal force from any start, in both resolvers. If the Director wants such a win to raise nothing, the
    targeted form is (c′) below; answering Q-AP4 (b) would remove the TacticalResolver's case only.**]**
  **[MOR3, 2026-11-11, D-085 DD1: the Director answered — "No fighting doesn't affect morale at all then" — and D-085 §3 records it:
  "The measured +150,000 for the defender of an unfought mutual rout, and the attacker's −50,000, both become 0". (a) is no longer
  applied to unfought battles: the prototype now settles both cases above at 0 for both armies, in both resolvers, from every start
  measured (MEASURED: the renamed `DD1_AnUnfoughtMutualRout_…` and `DD1_AnUnfoughtMutualWithdrawal_InTheAutoResolver_…`). The
  in-battle charge no longer routs a start in [300, 350) at the start (it falls only at a first fighting instant, PROPOSAL C1), so the
  three part-on cases rout by inactivity at 420, 449 and 600, as with the part off. Lane W's desertion of the routed men (persons) is
  unchanged: 800 of 2,000 per side from 500 (READING MOR3-R3, §12.3).]**
- **Alternative (b), not applied:** scale the rise by the winner's share of persons still in good order at the end (Active, or
  withdrawn in good order): 0 for a mutual rout or annihilation. It changes every decisive win too — a winner that lost a third of its
  men would get two thirds of the rise — so the losses would be counted twice (DR29 and the scaling), and it needs the end-state
  counts in the hand-off. Not implemented (the design does not adopt it).
  **[MOR2, verification round 2 (MOR2-R2-01): "0 for a mutual rout or annihilation" holds; but (b) gives the FULL rise to the
  AutoResolver's unfought mutual withdrawal above (every man withdrew in good order: a share of 1), so it does not remove that lift.]**
  **[MOR2, verification round 1: a narrower form (b′), not applied either: no rise when none of the winner's formations that took part
  is left in good order (a mutual rout or annihilation), every other win unchanged. It removes the ~~low-morale~~ lift above without
  touching decisive wins, and it departs from DA1-R (a) only for wins with nobody left in good order. MEASURED that the prototype
  applies (a), not (b′): a mutant that withholds the rise from a winner left with nobody fit or wounded fails
  `ProposalA_AMutualRoutWonUnderDA6_…` and `DA1_AMutualEliminationWin_…` (H-MOR2 §13.6, R1-M12).]**
  **[MOR2, verification round 2 (MOR2-R2-01): corrected. (b′) removes the TacticalResolver's unfought mutual rout but not the
  AutoResolver's unfought mutual withdrawal, whose men withdrew in good order. And in the TacticalResolver "nobody left in good order"
  is what nearly every DA6/DA7 win is (BothEliminated: neither side keeps an Active entity, so only an orderly withdrawal at that
  instant leaves the winner anyone in good order), so (b′) withholds the rise from fought equal-sides wins too, such as lane AP's
  duel and the fought mutual rout above (DERIVED). MEASURED with round 1's encoding of (b′) in lane W's categories (the mutant R2-M03
  = R1-M12: no rise when the winner's formation has nobody fit or wounded): it fails `ProposalA_AnUnfoughtMutualRout_…` and lane AP's
  duel through the hand-off (`DA1_AMutualEliminationWin_…`) and passes `…_AnUnfoughtMutualWithdrawal_InTheAutoResolver_…`; it also
  passes `ProposalA_AFoughtMutualRout_…`, but only because lane W files a wounded man who fled as Wounded (`BattleCollapse.CategoryOf`),
  which that encoding counts as good order — a limit of the stand-in: its collapse input cannot tell the wounded who fled from the
  wounded who held (H-MOR2 §14.6).]**
- **Alternative (c′), not applied [MOR2, verification round 2 (MOR2-R2-01)]:** no rise for an equal-sides win (DA6 or DA7) in which
  the winning side's formations that took part dealt no damage — an unfought tie. It removes exactly the cases of the round-2 note
  above (the three TacticalResolver routes and the AutoResolver's mutual withdrawal) and leaves every fought win as it is, fought
  equal-sides wins included (lane AP's duel; the fought mutual rout); it needs one more hand-off input (whether the winning side's
  formations dealt damage: `FormationResult.DamageDealt`) and departs from DA1 for unfought equal-sides wins only. A wider form (c),
  no rise for ANY win in which the winner struck no blow, would also reach an AutoResolver mutual withdrawal that ends SideEliminated
  (MEASURED by a temporary probe, H-MOR2 §14.2: with the two formations one column off symmetry, both withdraw in good order without a
  blow, side 0's formation — one column nearer its exit edge — is reported eliminated and side 1 the decisive winner, whichever side
  defends; the exit order as the cause is INFERRED from the geometry) — at the cost of no rise for a win over an enemy that fled before
  contact. Neither is implemented: each is an exception to DA1 (DECIDED) that only the Director can make (argument (4)).
  **[MOR3, 2026-11-11, D-085 DD1: the Director made it, wider than either: no rise AND no cost (no entry term, no DR29 decrease) for
  ANY battle without fighting, decisive wins included. It differs from (c) in what it reads: DD1-R1 (a) reads the BATTLE, so a win in
  which the winner struck no blow but the loser did (an attack resolved against the winner's formation, hit or miss) is a fought battle
  and the winner rises; (c) would have read the winner's own blows. The AutoResolver's one-column-off mutual withdrawal (a decisive win
  without a blow) now settles at 0 for both armies; MEASURED on a battle of that kind in both resolvers,
  `DD1_AnUnfoughtDecisiveWin_ChangesNeitherArmysMorale_BothResolvers` (§12.3). The input is the battle report's first fighting instant,
  not `DamageDealt` (DD1-R2 (a): "whether or not it dealt damage").]**

### 11.9 Statements superseded or narrowed (marked in place, 2026-11-10)

The header and the label key; §0 (the army row); §2 (two statements the dossier does not establish); §3.3 (the "Fire without
casualties" row; the three "Pattern seen" cells, MOR-V2-05); §4.1 (the settlement equation); §4.2 (the worked table and the "about
morale-neutral" sentence); §5 (battle → campaign); §8 (READING M) and §9 (READING M OPEN); §10 item 7; and, in the earlier annex,
five rows the dossier corrected or does not establish (A.8 lists every earlier row's fate). The dated D-083 note below the annexes
is left as written; this section is what it announced.
**[MOR2, verification round 1, 2026-11-11: the list above omitted §4.5, which §11.6 extends (MOR2-R1-09); it is now marked in place.
Round 1 marked, in place: §4.5 (MOR2-R1-09); §11.2 items 1 and 4 (READINGS MOR2-R2 and MOR2-R1; MOR2-R1-01, -02, -04); §11.3 (the
counted-once pin, MOR2-R1-12); §11.4 (the rout-threshold bullet's consequence in persons, MOR2-R1-08); §11.5 Law 1 (MOR2-R1-01,
-08); §11.6 (the aggregation's weighting, MOR2-R1-09; vessels and aircraft, MOR2-R1-02); §11.7 (the switch-on, MOR2-R1-11); §11.8
argument (3) (MOR2-R1-03); §11.10 and §11.11; the new §11.12 (lane S's siege battles, MOR2-R1-13); Annex A.4, A.7 and A.8
(MOR2-R1-10). The lane report's round-1 section is H-MOR2 §13.]**
**[MOR2, verification round 2, 2026-11-11: round 2 marked, in place: §11.2 item 1 (several domain formations, MOR2-R2-02); §11.8 (the
round-1 note's premise and consequence struck and corrected; (b) and (b′) corrected; (c′) added; MOR2-R2-01); §11.10 (observation
(2)); §11.11 (item 10). The lane report's round-2 section is H-MOR2 §14.]**
**[MOR3, D-085 DD1, 2026-11-11: lane MOR3 marked, in place, the statements D-085 supersedes or narrows; §12.9 lists them.]**

### 11.10 For the Director

No new question. Where the cost is charged and its size are delegated (D-083 §4, D-082 §5) and presented here for review: (iii)
with the in-battle part switched on at the next battery re-measurement; E = 50,000 (50 on the battle scale). Two observations, not
questions: the prototype has no reinforcement that marches onto the field (only lane N's landing), so DB1-R1 (a)'s reinforcement case
is pinned on a landing (M7.15); and, for lane P, a formation that starts below the rout threshold routs in the
TacticalResolver (its men leave the field routed) but withdraws in good order in the AutoResolver (MEASURED by a temporary probe on
one test-input battle at each of the starts 280 and 299 without the cost and 320 and 349 with it, while 350 with it routs in
neither; the cause, the AutoResolver's pulse-start withdrawal running before its rout check, INFERRED from the code; pre-existing),
a band the in-battle cost widens from [0, 300) to [0, 350) when it is on.
**[MOR2, verification round 1, 2026-11-11: still no new question. Three more observations, not questions: (1) the band above also
shows in PERSONS — through lane W's collapse, a formation the TacticalResolver routs on a side that did not hold the field loses
most of its men to the deserters (1,402 to 1,500 of 2,000 for starts 300 to 349 with the cost on), where the AutoResolver's orderly
withdrawal loses none; rule P1 does not see it (MOR2-R1-08, §11.4); (2) under PROPOSAL (a) for the mutual-elimination win (§11.8),
~~a defender of low morale whose formation routed in the same instant as the attacker's gains morale (+49,000 from 400; up to
+149,000), the case of AP-R1-14, with (b) and (b′) stated as the alternatives~~ **[round 2, MOR2-R2-01: corrected — a defender
whose formations ran, with no blow struck, in the same instant as the attacker's gains +150,000 against an equal force from ANY start
(DIR5 §2.6's inactivity rout at any morale; a start below the rout threshold; with the in-battle part on, the entry charge itself),
in the TacticalResolver (through Q-AP4 (a)) and in the AutoResolver (an equal mutual withdrawal, DA6-R) alike; the attacker pays
−50,000; a FOUGHT mutual rout nets the defender less (−150,000 in the measured battle). (a) is kept on argument (4) alone (DA1 is
decided; the alternatives except some of its wins); (b) and (b′) do not remove the AutoResolver's case, and the targeted alternative
is (c′), no rise for an unfought equal-sides win, §11.8]**; (3) two new READINGS are applied and labelled, MOR2-R1
(what "took part" is for DA1's rise) and MOR2-R2 (when a carried or based formation is placed on the field), §11.2.]**
**[MOR3, 2026-11-11: observation (2) is answered by D-085 DD1 — a battle without fighting changes no morale, so those +150,000 and
−50,000 are 0 (§12.3) — and the band of the first paragraph, with the part on, now opens at the battle's first fighting instant, not
at the entry (PROPOSAL C1, §12.4). Observation (1)'s persons were measured with the charge at the entry (§12.10 item 5).]**

### 11.11 Limitations

1. The in-battle part is 0 by default: by default the battle and the War Sandbox do not feel the cost (§11.3, §11.7). **[MOR3,
   2026-11-11: switched on, they feel it from the battle's first fighting instant, and a battle without fighting not at all (§12.4).]**
2. The only later arrival is lane N's landing (TacticalResolver only; the AutoResolver refuses formations that start aboard).
3. Vessels and aircraft are outside the in-battle charge (§11.6).
4. The hand-off is lane D's stand-in (one army, one formation); the tests' `HandOff` maps a battle report to it as M7.15 would.
5. The size is not calibrated (M7.14, A-38); the reference gives no number for it (§11.4).
6. The effect of the in-battle part was measured on the tuning battery only, never on the held-out acceptance battery (lane P's
   record; D-079 Q4 OPEN).
7. **[MOR2, verification round 1, 2026-11-11]** Lane S's siege battles (assaults, relief battles, sallies) do not charge the entry
   cost: they are resolved by lane S's stand-in odds model, not by the two resolvers or lane D's settlement (§11.12).
8. **[Round 1]** With the in-battle part on, the cost widens a pre-existing parity gap in persons (rout and desertion in the
   TacticalResolver, orderly withdrawal in the AutoResolver) that rule P1 does not measure (§11.4, MOR2-R1-08).
9. **[Round 1]** The aggregation over several formations (§11.6) is not built; how its persons are counted re-weights DR29's decrease
   (MOR2-R1-09), and the choice is open for M7.15.
10. **[Round 2, MOR2-R2-01]** ~~Under PROPOSAL (a), as applied, an equal-sides win with no blow struck lifts the defender's army by
    +150,000 against an equal force from any start, in both resolvers,~~ while lane W's collapse (in the TacticalResolver) deserts part
    of its routed men (§11.8). ~~The targeted alternative (c′) is stated, not implemented: it is an exception to DA1 for the Director.~~
    **[MOR3, 2026-11-11: resolved by D-085 DD1 — the Director made the exception, wider (no rise and no cost for any battle without
    fighting), and the prototype implements it (§12.3). The desertion of the routed men (persons) remains, READING MOR3-R3.]**

### 11.12 Lane S's siege battles (PROPOSAL, not implemented) [MOR2, verification round 1, 2026-11-11 (MOR2-R1-13)]

§11 did not mention them. What the prototype does (CITED from `Proto.M7/War/Siege/`): an assault, a relief battle and a sally are
battles by contract ("an assault is a land battle", `IAssaultResolver`), but in the prototype they are resolved by lane S's
`FormationAssaultStandIn`, a formation-level odds model labelled a stand-in for the land module; it has no formations on a field, no
entry instant and no per-formation morale. Each camp of a siege keeps its OWN morale (the garrison's and the besiegers', Q32 shares in
`SiegeWorld`), which relaxes toward state-derived targets at per-year rates (`MoraleStep`, `DefenderMoraleTarget`,
`AttackerMoraleTarget`) and takes event changes of its own (a repelled assault lifts the garrison and shocks the attackers; a failed
relief shocks the garrison). That stock is not lane E's campaign morale, the one DB1's settlement term lowers. A relief army "arrives
at ArrivalMs" (`SiegeTypes.cs`) and acts at the first siege pulse start at or after it.

- **PROPOSAL (a), applied by omission today: DB1 reaches siege battles when they are resolved by the land module** (C15, ADR-060
  §2.5: the stand-in is to be replaced by `BattleResolution.Resolve` on a `BattleSetup` built from the siege). Then each formation of
  an assault, relief battle or sally pays at its placement on that battle's field (the besiegers' storming parties and the garrison's
  defenders on the walls at the battle's start; a sally party or a relief army at its arrival on the field, a later entry), the
  settlement charges each army whose formation entered, and nothing siege-specific is needed. A relief army's arrival at the siege
  (ArrivalMs) is a campaign movement, not an entry: its formations enter the relief battle when placed on its field.
- **Alternative (b), not proposed:** add an entry term to lane S's stand-in now (each assault, relief battle and sally lowers both
  camps' siege morale by a fixed amount). It would be a third model of the same battle beside lane S's calibrated event terms and the
  resolvers' own, with a separate number to calibrate, and the stock it lowers is not the campaign stock DB1's settlement term
  charges.
- **Consequence of (a) until then (a limitation, §11.11 item 7):** in the prototype a siege battle costs nobody the entry cost.
  **[MOR3, D-085 DD1, 2026-11-11: and under (a) DD1 reaches them by the same road — the fought report gates their settlement like any
  other battle's (§12.6).]**

---

## 12. Without fighting, no morale change (D-085 DD1) — PROPOSAL where design (lane MOR3, 2026-11-11)

**Status.** The rule is a Director decision (D-085 **DD1**, DECIDED) read through two READINGS that D-085 applies (DD1-R1 (a),
DD1-R2 (a)). Everything D-085 §4 delegates — each resolver's "fought" report, the in-battle part of the entry cost, the War Sandbox
and lane S's siege battles — is lane MOR3's **PROPOSAL**, decided by nobody. Three interpretations of this lane are labelled
**READING MOR3-R1, MOR3-R2, MOR3-R3**, each with both alternatives. The prototype: `BattleResult.FirstFightMs` and `Fought`
(`Proto.M7/Tactical/BattleResult.cs`); the attack sites of both resolvers (`TacticalBattle.cs`, `TacticalBattle.DomainCombat.cs`,
`TacticalBattle.Ram.cs`, `AutoResolver.cs`); `EntryMorale.ChargeOnFighting` (`Tactical/EntryMorale.cs`); `CampaignMorale.IfFought`
and `BattleFighting.SettlesAsFought` (`Morale/CampaignMorale.cs`); `SupportBattle.Fought` and the settlement (`Support/`); the
settlement row's `Fought` (`Morale/MoraleCodec.cs`, codec version 3). Tests: `Proto.M7.Tests/Morale/NoFightingTests.cs` and lane
MOR2's re-pinned tests. Lane report: `prototypes/m7-sprint1/reports/H-MOR3.md` (every moved pin with its measured attribution).
Nothing in `Sim.*` or `Sim.Data` changes (D-079 §6, Q5 open; D-085 §5). Labels as in the header; MEASURED here means a test or run of
lane MOR3, named.

### 12.1 Authority

- [D-085](../d085-no-fighting-no-morale-change-2026-11-11.md) **DD1** (DECIDED): "No fighting doesn't affect morale at all then.
  There has to be done fighting to alter morale" — recorded as: "A battle without fighting does not affect morale at all. There is
  no rise for its reported winner, so an equal-sides "win" with no blow struck raises nothing. There is no cost either: no entry cost
  (D-083) and no decrease (DR29). Morale changes only where there was some fighting."
- **DD1-R1 (a)** (APPLIED): fighting is read per BATTLE — "If any fighting took place in a battle, DA1's rise, DR29's decrease and
  D-083's entry cost apply as before. That includes a formation that entered the field but itself struck no blow"; "If no fighting
  took place at all, no army's morale changes through that battle." Alternative (b): per formation, which "would narrow D-083 DB1 to
  formations that fought".
- **DD1-R2 (a)** (APPLIED): fighting is "at least one attack resolved against an enemy formation, whether or not it dealt damage. An
  attack is a melee blow, a missile volley, or a shot of a crew-served or domain weapon." Alternative (b): at least one attack that
  dealt damage.
- **D-085 §3**: DA1 narrowed ("a win with no fighting raises nothing"); DB1 narrowed ("entering a battle costs morale where the
  battle had fighting"; "with or without casualties" stands); lane MOR2's PROPOSAL (a) of §11.8 superseded where no fighting took
  place and (c′) "adopted in a wider form: no rise and no cost" ("The measured +150,000 for the defender of an unfought mutual rout,
  and the attacker's −50,000, both become 0"); DR29 unchanged.
- **D-085 §4** (DELEGATED): "Each resolver's 'fought' report ... and its boundary test (exactly one attack against none)"; the
  in-battle part of the entry cost: "If it is switched on, it must not change morale in a battle that turns out to have no fighting.
  It could be charged at the battle's first fighting instant (and at entry for a later arrival), or dropped so that the settlement
  alone charges the cost"; "how the War Sandbox's Quick Battle and lane S's siege battles apply DD1".

### 12.2 What fighting is: each resolver's report (DD1-R2 (a); PROPOSAL; implemented)

**The report.** Both resolvers report the battle's **first fighting instant**, `BattleResult.FirstFightMs`: the instant of the first
attack resolved against an enemy formation, hit or miss; null when there was none (`Fought`: it is set). Each resolver sets it at the
code points where it counts an attack (`FormationResult.AttacksMade`), next to the count, so that "fighting" is what the engine
already calls an attack. Like `EnteredAtMs` it is a report field outside `ResultHash` (every earlier result hashes as before), and
the state hashes carry it only while the in-battle part is on (§12.4), so at the defaults no golden of any lane moved (MEASURED: after
the change the full suite's only failures were 23 cases of lane MOR's and lane MOR2's morale pins, every one re-pinned with its
attribution in H-MOR3).

**Why an instant, not the count.** D-085 points at the count. The count says whether, not when: the in-battle part needs the instant
(§12.4). And in one place the count counts what DD1-R2 (a) does not — a bombard round on empty ground (READING MOR3-R1). Where both
apply they agree: on lane P's tuning battery the report is set exactly when Σ AttacksMade > 0 and equals the first attack observed
from outside the rule (the tactical event trace; the AutoResolver's step log) — MEASURED on its 20 scenarios at seed 1 in both
resolvers (`TheReportAgreesWithTheAttackCount_AndIsTheFirstAttack_OnLanePsTuningBattery_BothResolvers`), and on the whole battery
(1,600 cases per resolver, the in-battle part off and on, with and without lane MOR's c = 200): 0 battles whose report disagrees with
the count, and 0 battles without fighting — every battle of the battery is fought (MEASURED, `entry-parity`, §12.4).
**[MOR3, verification round 1, 2026-11-11, MOR3-R1-01: "the first attack observed from outside the rule" in the tactical trace
was every executed attack event, and a missile shot with no line of fire is executed but is no attack (the table below); the tests now
use those events as the oracle only once validated against the battle's own attack count, which these 20 battles pass, and observe a
battle with such shots with a line-of-fire oracle that recomputes each shot's line from outside the rule (H-MOR3 §13).]**

| Resolver · attack | Where it is reported (the code point that counts the attack) | Fighting? | Why this source | First fighting instant, MEASURED (`TheFirstAttackOfEveryDomain_…`; test inputs) |
|---|---|---|---|---|
| Tactical · melee blow | the `Fire` event, mode 1, before the hit roll | yes, hit or miss | the blow is resolved against the target entity by the hit roll; DD1-R2 (a): "whether or not it dealt damage" | 148,000 ms (heavy infantry meeting) |
| Tactical · missile volley | the `Fire` event, mode 2, once the shot is fired (a shot with no line of fire is not fired, so it is no attack) | yes, hit or miss | the arrow or bullet is resolved against the target | 0 (one arrow that missed: one Fire event, no Damage event in the battle); **[round 1, MOR3-R1-01]** a shot with no line of fire is no attack: 540 shots blocked by a wall, or by buildings, and the battle has no fighting (`DD1_R2a_AShotWithNoLineOfFire_…`) |
| Tactical · crew-served or domain weapon aimed at an enemy entity, hull or airframe (land guns, vessel guns, aircraft munitions; direct or indirect) | the `DomainFire` event with a target | yes, hit or miss, wherever its dispersion lands | it is aimed at an enemy formation's entity | 0 (coastal guns at a hull), 0 (vessels' line battle), 14,000 (strike aircraft); **[round 1, MOR3-R1-02]** 0 for an attack helicopter's rocket at a land entity that missed (its first hit at 36,000), 0 for a field gun's round aimed at a lone rifleman that fell 23,343 mm from him, beyond its 15,000 mm blast (`DD1_R2a_ADirectDomainShot…`, `DD1_R2a_AnIndirectRound…`) |
| Tactical · bombard of a bare ground point | the `DomainFire` event's blast loop | **READING MOR3-R1 (a)**: only when the blast reaches an enemy entity or hull | below | 6,000 on the riflemen's own point; none on empty ground |
| Tactical · boarding fight | the `Boarding` event | yes | both crews strike | 26,000; **[round 1, MOR3-R1-02]** 26,000 for a fight that cost neither crew of six a man (the first casualty at 48,000; `DD1_R2a_ABoardingFightWithoutCasualties_…`) |
| Tactical · ram (breach or glance) | the `Ram` event | yes | the ram is resolved against the struck hull | 198,000 (an oblique, glancing ram) |
| Tactical · bow contact that is not a ram | the `RamCollision` event | **READING MOR3-R2 (a)**: no | below | none (lane RAM's RAM2-V2 contact at 60,000) |
| Auto · melee in contact | the step's melee exchange (Attacks += engaged) | yes, whatever its expected damage | the model's attack is the step's exchange | step 74 = 148,000 (the same meeting: the same instant as the TacticalResolver) |
| Auto · volley, in contact or at range | the step's volley (Attacks += shots), which the model looses only with a line of fire (expected damage above 0) | yes | as the TacticalResolver's fired shot | step 0 = 0 (one volley in the battle); **[round 1, MOR3-R1-01]** a volley due with no line of fire (buildings) is loosed at nobody: no fighting, as in the TacticalResolver (`DD1_R2a_AVolleyWithNoLineOfFire_…`) |

The AutoResolver is land only: it refuses vessels, aircraft, crew-served weapons and formations that start aboard (CITED,
`AutoResolver.cs`), so the domain rows exist in the TacticalResolver only. The AutoResolver's instant is StartMs + k × StepMs for the
first step k with an attack, the instant of the TacticalResolver's same attack where the two resolve one (MEASURED: 148,000 in both).

**THE BOUNDARY (D-085 §4: "exactly one attack against none"), MEASURED in both resolvers through the hand-off**
(`DD1_TheBoundary_ExactlyOneAttackThatMisses_IsFought_NoAttackIsNot_BothResolvers`): one arrow, loosed at the start, that MISSES,
and then a Standoff with no winner — fought: both armies pay the entry cost (800,000 → 750,000 each); the same battle with the archer
one column further back (never in range), no attack — unfought: both armies unchanged (800,000; the rows record Fought 0); the
AutoResolver gives the same, one volley against none. Turn-exact with the in-battle part on: the single arrow loosed at 214,000 ms
(the trace's only Fire event) — at every completed instant before it both formations have exactly the morale of the same battle with
the part off, at 214,000 each is lower by the charge.

**READING MOR3-R1 — a bombard of a bare ground point.** The engine lets a weapon bombard a POINT (lane N's `OrderKind.Bombard`):
the round is aimed at no entity and its blast is rolled against every entity and hull within its radius. The attack count counts
every round.
- **(a) APPLIED: it is fighting only when its blast reaches an entity or hull of an enemy formation** (whether or not the blast roll
  then hurts it). Why: DD1-R2 (a) says "resolved against an enemy formation"; a round on empty ground is resolved against none, and
  counting it would let a side make a battle "fought" — and so charge both armies the entry cost and lift its own by DA1's rise —
  by shelling ground no enemy stands on. Lane TR's inactivity rule already treats such a round as no progress (CITED,
  `UndoBarePointShotProgress`).
- **(b): every round counts**, as the attack count does: the report and the count would agree everywhere; shelling empty ground is
  then fighting.
- MEASURED (`READING_MOR3_R1_ABombardOfABarePoint_IsFightingOnlyWhereItsBlastReachesAnEnemy`): on the riflemen's own point the first
  round (6,000 ms) is the first fighting instant (17 riflemen die); on an empty point the warship fires 59 rounds and its attack
  count is 59, but the report stays null — and when the riflemen rout by lane TR's inactivity rule, the warship's side's decisive
  "win" changes neither army's morale. Under (b) both armies would pay the entry cost and the winner's would rise.

**READING MOR3-R2 — a bow contact that is not a ram.** Lane RAM's engine resolves a contact without the conditions of a ram (no
normal speed, or a hull already rammed) as a collision: no hit roll, no damage to the struck hull, only to the rammer's own bow; it
counts no attack (CITED, `TacticalBattle.Ram.cs`; RAM2-V2: "no ram is credited ... nobody is damaged").
- **(a) APPLIED: no attack, so no fighting** — as the attack count and the inactivity rule already read it.
- **(b): a ram attempt resolved as a contact counts as an attack**; two hulls bumping without effect would make a fought battle.
- MEASURED (`READING_MOR3_R2_ARamCollisionAlone_IsNoFighting`): lane RAM's RAM2-V2 battle, whose only contact is a collision at
  60,000 ms, reports no fighting at the collision's own instant and at its end (a Standoff at 360,000).

### 12.3 The settlement (DD1, DD1-R1 (a); implemented)

Each of §11.2's three terms applies only where the battle had fighting (`CampaignMorale.IfFought`):

    entry    = fought ∧ entered ? E : 0                                                              DB1, narrowed by DD1
    decrease = fought ? floor(max(0, (start − entryInBattle) − end − restore) × 1,000,000 ÷ MoraleMax) : 0    DR29, gated by DD1
    rise     = fought ∧ won ∧ entered ? floor(R × B ÷ (B + W)) : 0                                       DA1, narrowed by DD1
    after    = clamp(before − entry − decrease + rise, 0, 1,000,000)        (= before for a battle without fighting)

- **fought** is the battle's answer (DD1-R1 (a)): `BattleFighting.SettlesAsFought(report, formation)` returns the report's
  `Fought` whichever of the battle's formations asks (and refuses a formation that is not the battle's). The hand-off passes it as
  `SupportBattle.Fought` (lane D's stand-in; M7.15 builds the real hand-off). Its default is true, so every hand-made stand-in battle
  written before D-085 settles exactly as before (MEASURED: they pass unchanged); the tests' hand-off from a real battle report sets
  it from the report.
- **The reported winner is still recorded** on the row, with no rise: an unfought "win" is visible and lifts nobody.
- **The row** (`MoraleSettlementRow`) gains `Fought` (u8, 0 or 1) after `EntryPpm`: codec version 3, 111 bytes a row; version-1 and
  version-2 tables and a Fought byte other than 0 or 1 are refused. MEASURED: a populated table of six real rows (fought and unfought,
  entered and never entered, a rise, an in-battle charge, a clamp at 0), exact length 5 + 111 × rows, bit-exact round trip, the same
  SHA-256, lane D's field-swap guard (`DD1_SettlementRows_Version3_PopulatedTable_RoundTripBitExact_WithExactLength`).
- **The stand-in refuses** an in-battle entry charge for a battle without fighting (no resolver produces one, §12.4).
- **MEASURED in real battles through the hand-off:**
  - lane MOR2's unfought mutual rout (TacticalResolver; DA6 names the defender) from 250, 299, 320, 500 and 800 with the in-battle
    part off and 320, 349 and 500 with it on: both armies unchanged, where lane MOR2 measured the defender +150,000 and the attacker
    −50,000 (`DD1_AnUnfoughtMutualRout_WonUnderDA6_ChangesNeitherArmysMorale_FromAnyStart`, renamed from lane MOR2's `ProposalA_…`);
  - lane MOR2's unfought mutual withdrawal (AutoResolver) from 250 and 449, part off and on: both armies unchanged
    (`DD1_AnUnfoughtMutualWithdrawal_InTheAutoResolver_…`);
  - an unfought DECISIVE win, both resolvers (a formation at 250 leaves at once; SideEliminated, side 1 the decisive winner): both
    armies unchanged — DD1 reaches every win without fighting, not only the equal-sides ones (c′) targeted ("adopted in a wider
    form") (`DD1_AnUnfoughtDecisiveWin_ChangesNeitherArmysMorale_BothResolvers`);
  - **DD1-R1 (a)**: a formation that entered a fought battle and neither struck nor received a blow pays the entry cost, and its side
    having won it takes the rise too: 800,000 − 50,000 + 160,000 = 910,000, both resolvers (`DD1_R1a_…`); under (b) it would settle
    as unfought (800,000);
  - **a fought equal-sides win still rises**: lane AP's `DA1_TheEqualSidesWinnerRises_DefenderOrAmbusher` passes unchanged (its
    settlement rows are hand-made, fought by default), and so do the tests that take lane AP's duel and the fought mutual rout through
    the hand-off, which now reads the report's first fighting instant: `DA1_AMutualEliminationWin_TheRiseIsUnchanged_…` (the
    defender's army 150,000 with a rise of 200,000) and `ProposalA_AFoughtMutualRout_WonUnderDA6_DR29OutweighsTheRise`.
- **Tie-dense** (CLAUDE.md): five armies settling at one instant, fought and unfought battles mixed, in all 120 scheduling orders —
  army-id order, the same values and the same state hash; one army with a fought and an unfought battle at one instant, in both
  orders: the unfought battle is the identity, so the order cannot matter (MEASURED, `DD1_TieDense_…`).
- **Several formations in one army** (§11.6, not built): under DD1-R1 (a) the formations of one battle share its answer, so the
  aggregation needs one more input per battle, not per formation: Δ_army = (fought ? 1 : 0) × Σ_f … (PROPOSAL, M7.15).

**READING MOR3-R3 — what DD1 reaches.** DD1 says a battle without fighting "does not affect morale at all".
- **(a) APPLIED: what the battle does to the armies' morale** — the settlement's three terms — and the in-battle part of the entry
  cost, which D-085 §4 names. The resolvers' own in-battle dynamics that need no attack stay as they were: recovery while holding or
  rallying without losses, and lane TR's inactivity rout (a change of state at any morale). In a battle without fighting none of it
  reaches an army's morale, since every settlement term is 0. Persons are not morale (Law 1: lane W's and lane D's transfers): lane
  W's collapse still deserts the share (1,000 − end) ÷ 1,000 of the routed men of a side that did not hold the field, whether or not
  there was fighting — MEASURED: the unfought mutual rout from 500 ends at 600 (five quiet pulses of recovery) and deserts 800 of
  2,000 men on EACH side (`DD1_AnUnfoughtMutualRout_…`).
- **(b): DD1 also holds every formation's in-battle morale still until the battle's first fighting instant** (no recovery before it),
  in every battle, since a battle cannot know that it will stay unfought. It changes both resolvers' pre-fight phase, moves goldens and
  the parity battery, and still does not reach persons unless lane W's desertion is gated too.
- Why (a): it is the smallest reading that gives D-085's measured cases ("The measured +150,000 ... and the attacker's −50,000, both
  become 0"), and the battle-scale morale of a battle without fighting reaches nothing outside the battle except through lane W's
  desertion, a rule about persons that D-085 does not mention (observation for the Director, §12.8).

### 12.4 The in-battle part of the entry cost (D-085 §4; PROPOSAL C1; implemented, 0 by default)

| Option | What it does | D-085 §4 ("must not change morale in a battle that turns out to have no fighting") | Consequences |
|---|---|---|---|
| **C1 (PROPOSED, implemented): at the battle's first fighting instant** | every land formation that has entered by then pays the charge at that instant, wherever it is (on the field, or already gone — it entered); a formation that enters later pays at its own entry; a battle without fighting charges nobody | met by construction: before the first fighting instant nothing is charged, so the battle is the battle with the part off | the battle still feels the cost — from the first fight, where it acts on the fight; the settlement still counts it once (§11.3 (iii)) |
| C2 (not proposed): dropped | the settlement alone charges the cost | met trivially | the battle never feels the cost (§11.3 (ii): the two levels disagree about the same formation), and the War Sandbox never shows DB1 at all (§11.7) |
| C3 (not proposed): at the entry, refunded if the battle ends without fighting | as lane MOR2's charge, undone at the end | **not met**: the charged morale has already acted — lane MOR2 measured formations at 480 charged to 430 at the entry withdraw at their first decision, so the charge itself made the battle unfought (§11.8) — and an end-of-battle refund cannot undo a decision | — |

**Why C1.** (1) It keeps lane MOR2's option (iii) (§11.3) for every battle DB1 still charges (D-085: a battle with fighting): the
formation's morale and its army's campaign morale pay the same cost for the same battle, counted once. (2) It meets D-085 §4 by
construction, MEASURED in both resolvers: in lane MOR2's three-formation battle, at every completed instant before the first attack
(7,332,000 ms) the part-on battle has the same morale and the same entity states as the part-off battle, and at 7,332,000 every
formation is lower by exactly the charge; the AutoResolver's step logs are identical record for record before its first fighting step
(66), and at it differ only by the charge (`C1_TheInBattlePart_ChangesNothingBeforeTheFirstFightingInstant_TurnExact_BothResolvers`).
A battle without fighting is the SAME battle with the part on: the same result hash and trace hash, no entry row (MEASURED on lane
MOR2's unfought battles, both resolvers: `C1_InABattleWithoutFighting_TheInBattlePartChangesNothing_BothResolvers`). (3) It removes the
case where the cost itself made a battle unfought: lane MOR2's equal formations at 480 in the AutoResolver now advance and fight with
the part on as with it off (first fighting step 131, 262,000 ms; both rout in one instant; DA6 names the defender), and the
settlement is the same either way, 430,000 for the defender's army and 230,000 for the attacker's — the charge counted once
(MEASURED, `C1_TheInBattleChargeNoLongerMakesAnUnfoughtWithdrawal_…`). (4) One function for both resolvers (D-079 D11):
`EntryMorale.Enter` records the entry (and charges only after the first fight), `EntryMorale.ChargeOnFighting` charges at it.

**Where in the instant.** At the start of the instant's resolution: in the TacticalResolver the Resolve event (class 50), after the
instant's attack events (class 30) and their damage to hit points (class 40), before deaths, casualty-driven morale loss and routs;
in the AutoResolver at the start of the step's phase 5, after the step's attacks, before damage, casualties, morale and rout (CITED,
`TacticalBattle.Resolve`, `AutoBattle`). The attacks of the first fighting instant are thus resolved at the morale before the charge,
and that instant's casualty losses and routs act on the morale after it. In ascending formation id; once per formation (DB1-R2 (a));
all of a formation's morale when it has less than the cost.

**A later arrival** (DB1-R1 (a): "later, at the instant it arrives"), MEASURED turn-exact on lane N's shore made a fought battle
(`C1_ALaterArrival_IsChargedAtItsOwnEntry_AfterTheFirstFight_AndAtTheFirstFight_BeforeIt_TurnExact`): riflemen put ashore at 2,000 or
128,000 ms are charged AT THEIR LANDING when the fighting began at 0, and AT THE FIRST FIGHTING INSTANT (258,000) when they landed
before it — never before. Once with re-entry, in a fought battle: four formations each leave the field and are placed on it again at
least twice and pay once (`C1_OncePerFormation_WithReEntry_InAFoughtBattle`).
**[MOR3, verification round 1, 2026-11-11, MOR3-R1-05: in the TacticalResolver the landing's own instant would charge such a formation
even without the charge in `EntryMorale.Enter`: the landing runs in the instant's Step (class Arrival), which schedules that instant's
resolution, and its `ChargeOnFirstFight` charges every entered, uncharged formation once fighting has begun (DERIVED, `TacticalBattle.cs`).
MEASURED: with the entry-time charge removed (lane MOR3's mutant C9) the four later-arrival cases still pass; only the shared function's
contract test (`EntryMorale_Enter_ChargesOncePerFormation_ByHand`) pins that path. The instant is the same either way; only the order of
two rows charged at one instant could differ.]**

**What C1 narrows.** D-083's DB1-R2 (a) reads the cost as charged "once per formation per battle, at its entry instant". For the
in-battle part the instant is now the later of the entry and the battle's first fighting instant — the timing D-085 §4 itself names
("charged at the battle's first fighting instant (and at entry for a later arrival)"). Once per formation per battle stands; the
settlement's term, an after-battle term, is not affected.

**The battle feels it from the first fight.** With the part on, a formation at 480 that holds is charged to 430 at the first attack
of the battle (one arrow, which misses, at 214,000 ms) and withdraws at its next decision (240,000), untouched; with the part off it
stays at 480 (MEASURED in both resolvers, the AutoResolver at its step 77: `BothResolvers_WithTheInBattlePartOn_TheBattleFeelsIt_
FromTheFirstFightingInstant_…`, which replaces lane MOR2's test of a charge at the entry).

**The default stays 0** (the exact identity; the PROPOSAL switch-on value 50 at the battery's re-measurement, §11.3, is unchanged).
**Measured on lane P's tuning battery** (`entry-parity`, Release, 2 threads; 20 scenarios × seeds 1–80 = 1,600 cases per resolver;
"off" = the defaults; H-MOR3 §5). Because both resolvers' outputs change when the part is on (the charge moved), the battery was
re-measured; lane MOR2's numbers (charge at the entry, §11.6) are given beside it:

| | tactical, C1 | AutoResolver, C1 | lane MOR2 (charge at the entry): tactical / auto |
|---|---|---|---|
| off digest (lane P's ParityPCsv.Digest) | `90E58DCB…F65D6914` = lane P's | `2AC31207…A0B68F31` = lane MOR's | the same |
| on, entry 50 alone: digest | `F8988948…666879AE` | `11437044…D1054425` | `413CD6C2…88E29A18` / `048A8EE5…FA33101E` |
| entry rows (on) | 3,440 | 3,440 | 3,440 / 3,440 |
| battles without fighting (off / on) | 0 / 0 | 0 / 0 | — |
| outcomes changed / winners changed | 887 / 128 | 887 / 83 | 649 / 134 · 598 / 103 |
| on, entry 50 + c = 200: digest | `F87F58E1…08A9D847` | `BEECA293…29B7262D` | `D4784D79…E2E26137` / `0FE5ED33…6DA15389` |
| outcomes changed / winners changed | 872 / 127 | 856 / 54 | 634 / 133 · 567 / 74 |

Rule P1 (n = 80 per tuning scenario): **off 16/20; entry 50 alone 14/20; entry 50 + c = 200 13/20** — the same counts and the same
failing scenarios as lane MOR2 measured (leadership-edge on duration, ford-crossing on the winner; with c = 200 also sniper-screen on
duration). MEASURED: the identity at the defaults holds over 1,600 battles in each resolver; ~~every battery battle is fought, so DD1's
settlement gate changes nothing there, and the whole difference from lane MOR2's "on" numbers is the charge's instant~~
**[MOR3, verification round 1, 2026-11-11, MOR3-R1-07: struck as a MEASURED statement. MEASURED: every battery battle is fought (0 battles
without fighting, off and on, both resolvers). DERIVED, not measured (the bench runs no settlement): DD1's settlement gate is the
identity on a fought battle, so it changes nothing on this battery; and of this lane's resolver changes only the charge's instant (C1)
acts on a battle with the part on (the fought report only sets a report field; the state-hash additions are outside the bench's
digest), so it is the whole difference from lane MOR2's "on" numbers.]** INFERRED (not
measured): more outcome fields change under C1 than with the charge at the entry because a charge taken at the entry could be made up
by the in-battle recovery while a formation held before the fight (§11.3 (i)), and one taken at the first fight cannot. Lane P's
acceptance stays OPEN (D-079 Q4); the held-out acceptance battery was not run; these numbers measure the rule's effect, not parity.

### 12.5 The War Sandbox (D-085 §4; implemented through the resolvers)

The sandbox keeps no campaign morale and settles nothing (D15: a Campaign Encounter writes nothing back), so DD1's settlement gate has
nothing to gate there; it runs the campaign's resolvers with its own rules (DR30), so the fought report and the in-battle part reach
it unchanged. MEASURED with rules carrying the part (50) (`WarSandbox_QuickBattle_WithoutFighting_ChargesNothing_WithFighting_
ChargesAtTheFirstFight`): a Quick Battle between two player sides who never order anything, out of each other's sight, has no
fighting — no first fighting instant, nothing charged; the same forces AI against AI (the AutoResolver) fight, and every formation is
charged once, at the battle's first fighting instant. **[MOR3, verification round 1, 2026-11-11, MOR3-R1-04: now observed on the AutoResolver's charge rows,
exactly one per formation, at the first fighting instant (232,000 ms); the test used to read only each formation's EntryCharged and an
instant after the start, which a charge one step late or a charge at every fighting step also pass (H-MOR3 §13).]** Lane MOR2's Quick
Battle test (fought battles) passes unchanged.
- **PROPOSAL (not implemented):** the result inspection shows the battle's first fighting instant, or "no fighting: no morale
  change"; the Campaign Encounter's settlement preview (§11.7) applies DD1 with the same functions (`BattleFighting.SettlesAsFought`,
  `CampaignMorale.IfFought`), so an encounter without fighting previews no change for either army.

### 12.6 Lane S's siege battles (D-085 §4; PROPOSAL, not implemented)

What the prototype does (CITED from `Proto.M7/War/Siege/`): an assault, a relief battle and a sally are resolved by lane S's
`FormationAssaultStandIn`, one odds draw per battle that returns loss shares — it resolves no attack, so DD1-R2 (a)'s test has nothing
to read; and none of them reaches the campaign settlement (`SupportWorld.OnBattle` is the only code that sets an army's campaign
morale after a battle), so in the prototype DD1, like DB1 and DA1 (§11.12), never applies to a siege battle. Each camp's siege morale
(lane S's own stock) takes three event terms: a repelled assault lifts the garrison (`RepelledAssaultBoostPermille`, 50 ‰) and shocks
the besiegers (`FailedAssaultAttackerShockPermille`, 100 ‰); a beaten relief shocks the garrison (`FailedReliefShockPermille`, 400 ‰).
- **PROPOSAL (a): DD1 reaches siege battles with the land module** (C15, ADR-060 §2.5; §11.12 (a)). Each assault, relief battle and
  sally is then a battle with its own report; one that had no fighting changes no army's campaign morale, and — the same reading
  (DD1-R1 (a): "no army's morale changes through that battle") — moves neither camp's siege morale: no repelled-assault lift or shock
  and no failed-relief shock from a siege battle without fighting. Nothing siege-specific beyond reading the report's `Fought`.
- **Alternative (b), not proposed:** a fought test in lane S's stand-in now (for example, both sides' powers above 0). It would be a
  third definition of fighting beside the two resolvers', in a model that is to be replaced, for a stock DD1's settlement does not
  touch.

### 12.7 The laws

- **Law 1.** DD1 moves no person, good or coin; persons move by lane W's and lane D's ledger transfers as before, in a battle without
  fighting too (READING MOR3-R3 (a)). Conservation holds, exact (every suite test of it passes).
- **Law 2.** No standing modifier: the fought gate is a 0/1 factor inside the settlement equation, the in-battle charge an event
  change of the formation's stock at an instant.
- **Law 3.** Events only; no rate.
- **Law 4.** Nothing reads a date or an era: the report is computed state (the first attack).
- **Law 5.** Integers; the first fighting instant is an event time (the TacticalResolver's first executed attack in the scheduler's
  total order; several attacks at one instant give the same instant); the charge runs in ascending formation id; settlements in the
  event key's order (tie-dense, MEASURED); no IEEE type. The determinism scan is clean.

### 12.8 For the Director

No new question: D-085 §4 delegates every design item here. Observations, not questions:
1. **READINGS MOR3-R1 and MOR3-R2** apply DD1-R2 (a) at its two edges (a bombard of empty ground is not fighting; a bow contact that
   is not a ram is not fighting), each with its alternative (§12.2).
2. **READING MOR3-R3**: DD1 is applied to morale only. Lane W's desertion of routed men (persons) still happens in a battle without
   fighting: in the measured unfought mutual rout from 500, 800 of 2,000 men desert on each side; and the in-battle recovery still
   runs before the first fight (§12.3).
3. **C1** is proposed for the in-battle part (D-085 §4's first form); C2 (dropped) is the alternative; the default stays 0.
4. **Lane P's battery**: C1 changes the in-battle part's effect when it is on (the "on" digests and counts above replace lane MOR2's);
   at the defaults both resolvers' digests are unchanged. Lane P's acceptance stays OPEN (D-079 Q4).

### 12.9 Statements superseded or narrowed (marked in place, 2026-11-11)

The header; §0 (the army row and the link row); §4.1; §4.3; §5 (battle → campaign); §6; §8 item (b); §9; §11 (its opening
paragraph); §11.1 (DB1-R2 (a)'s instant); §11.2 items 2 and 3; §11.3 (the "battle feels it" paragraph and the counted-once note);
§11.4 (the lossless win; the MoraleMax and rout-threshold bullets; the worked "With DR29" bullet); §11.5 Law 2; §11.6 (the row's
version; the battery numbers); §11.7 (the sandbox's charge at entry; the preview; the staging); §11.8 (its opening, the round-2
conclusion, (c) and (c′)); §11.9 (a pointer here); §11.10 (the band and observation (2)); §11.11 (items 1 and 10); §11.12. The dated D-085 note below the annexes is left as written; this section is what it announced.
**[MOR3, verification round 1, 2026-11-11] Marked in place: §12.2 (the oracle note; four measured cells of the table), §12.4 (the
battery statement relabelled; the later-arrival note), §12.5 (the sandbox note), §12.10 (item 4; items 7 and 8 added). The verdicts are
in H-MOR3 §13.**

### 12.10 Limitations

1. The in-battle part is 0 by default: by default the battle and the War Sandbox do not feel the cost at all.
2. Lane S's siege battles do not apply DD1 (they never reach the settlement): PROPOSAL (a) only (§12.6).
3. The sandbox's inspection and its settlement preview are PROPOSALS (§12.5).
4. The hand-off is lane D's stand-in; `SupportBattle.Fought` defaults to true for hand-made battles (M7.15 sets it from the report).
   **[MOR3, verification round 1, 2026-11-11, MOR3-R1-08: that default is FAIL-OPEN: a hand-off that leaves it out settles a battle without fighting
   as fought (the entry cost, DR29's decrease, DA1's rise). Every hand-off of a real battle report in the prototype now sets it from
   the report (the tests' `HandOff`, and since round 1 lane D's two hand-offs, which used to rely on the default). PROPOSAL for M7.15:
   the real hand-off takes it as a required input, with no default.]**
5. Lane MOR2's measurement of the parity gap in persons with the part on (§11.4: a charge that routs a formation in the
   TacticalResolver but withdraws it in the AutoResolver) was made with the charge at the entry; under C1 the charge falls at the
   first fight and that gap was not re-measured.
6. The battery was measured on the tuning battery only, never on the held-out acceptance battery (lane P's record; D-079 Q4 OPEN).
7. **[MOR3, verification round 1, 2026-11-11, MOR3-R1-03] The two resolvers can disagree about whether a battle had fighting, and so about every
   DD1 term, where a WALL stands between the sides: the AutoResolver's formation-level contact and its line of fire ignore Wall
   cells, which block the TacticalResolver's direct fire. MEASURED on the verifier's walled setup (archers behind a wall, a spotter
   beyond it; seeds 1 to 3): the TacticalResolver (the knowledge AI on both sides) ends in a Standoff at 360,000 ms after 540 blocked
   shots, with no fighting; the AutoResolver fights from step 0 (melee and volleys through the wall) and ends with a side eliminated;
   and `ResolverPolicy.Default` sends an AI-against-AI battle to the AutoResolver. The same battle thus settles with no change in one
   resolver and with the entry cost, DR29's decrease and DA1's rise in the other. Behind BUILDINGS the two agree (no fighting;
   MEASURED, `DD1_R2a_AVolleyWithNoLineOfFire_…`). The cause is lane P's AutoResolver model, which predates this lane, not DD1's
   report: its volley's line of fire is `TerrainSight.FirePath` alone, which judges opaque layers only (CITED), so INFERRED, not
   measured, a crest between the sides is a second case of the same class. Lane P parity item (PROPOSAL): walls (and ground in the
   way) in the AutoResolver's contact and line of fire. Lane P's acceptance stays OPEN (D-079
   Q4). This lane's evidence of agreement between the resolvers (one meeting battle; the battery, with no battle without fighting;
   the buildings battle) cannot see this class of battle.**
8. **[MOR3, verification round 1, 2026-11-11, MOR3-R1-06] The state-hash additions (the first fighting instant and each formation's charge instant,
   appended only while the in-battle part is on) are pinned by no test: the verifier's mutants V39 and V40, which drop them, survive the
   full suite (re-measured in round 1). DERIVED: in the AutoResolver every formation enters at the start and is charged at the first
   fighting step, so both values follow from the charge rows the hash already carries; in the TacticalResolver each charge instant is
   its row's, and the first fighting instant follows from the rows except while no land formation has entered (every one aboard), the
   only case in which the dropped value is hashed nowhere else (INFERRED low impact; not pinned).**

---

## Annex A — Reference research, rebuilt from the research dossier (lane MOR2, 2026-11-10; D-069: reference only)

**[MOR2, 2026-11-10: this annex is rebuilt from a separate, provenance-checked research pass (the research dossier, run 1) and
replaces lane MOR's earlier annex as the evidence for this proposal. The earlier annex is kept, unchanged in substance, after this one
("Annex A (earlier pass)"), with each of its rows marked by what the dossier made of it (A.8); none of its rows stands as a fact
unless A.8 says the dossier re-established it. Nothing here is shipped or player-facing; none of the reference's names, numbers or
text enters our design (D-069, `docs/ip-originality.md`).]**

### A.0 How to read this annex

- **Sources.** The research dossier and its inputs, in the orchestrator's scratchpad
  (`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/research/run1/`): `dossier.md` (the
  answers), the claim files `official.json`, `data.json`, `community.json`, `targeted.json` (101 claims), the check step's verdicts
  `check.json` (147 verdicts, 15 recorded contradictions) and the host log `hosts.json`. Every row below cites the dossier's **claim
  id** and the check step's **verdict**; the dossier itself is the authority for both (this lane did not redo the research).
  **[MOR2, verification round 1, 2026-11-11 (MOR2-R1-10): that was not yet true of every row. MEASURED at `8ff9816` by a script that
  pairs every claim id of this annex (struck text excluded) with the verdict written beside it in its group and compares it with
  `check.json` (scratchpad `mor2-fix1/tools/annex_check2.py`): 145 pairs, 0 mismatches, but 20 claim ids in 12 groups (A.2, A.4, A.8)
  stood without a verdict — among them the two A.4 rows, which named data-01 and data-08 only as hosts of the check step's
  observations, and A.8's ambush row, whose one verdict, WEAK, read as data-18's although data-18 is CONFIRMED — and two A.7 cells
  (the Q1-b row and the genre row's sibling observation) cited no claim at all. Corrected in place: rows that rest on no claim of their
  own (the check step's observations, a dossier sub-answer) say so and cite the claims they rest on, each with its verdict. After the
  fix: 175 pairs, 0 mismatches, 0 ids without a verdict.]**
- **Labels** (lane MOR's, kept, with the dossier's FETCH added): **[CODE]** definitions or values read in the game's own data, or in a
  faithful extraction of it, with a path and a hash; **[FETCH]** text read on a fetched web page — **no claim of this pass carries it**:
  no web page could be read (A.1); under a stricter reading of the pack-file schema (a downloaded file held to be a page rather than a
  faithful extraction) its CODE rows would be FETCH from a single source, and every "ESTABLISHED (definition level)" below would drop
  to LIKELY (the dossier's own caveat; check contradiction 15); **[SEARCH]** a web-search result summary with its URL (weaker: no page
  behind it was read); **[KNOW]** unverified background knowledge, a hypothesis only.
- **Scope**: *reference* (the reference game itself), *sibling* (another title of the same series and developer, named), *genre*
  (another developer's game).
- **Verdicts** (`check.json`): **CONFIRMED** (re-measured by the checker against a re-fetched file), **WEAK** (consistent with the
  finder's log or the data, not independently confirmed — every SEARCH claim, because no search could be re-run), **UNSUPPORTED**,
  **CONTRADICTED** (both kept apart and not used as evidence).
- **Confidence** (the dossier's): **ESTABLISHED** (two independent CONFIRMED sources, or CODE), **LIKELY** (one CONFIRMED source),
  **UNKNOWN** (anything else). "Definition level" means: the data defines a key with the description quoted; no value, sign or use in
  the game is known (the schema holds no values at all).

### A.1 Method, provenance and limits (dossier §1)

- **What could be read.** Of 93 hosts tested once each through the session's proxy on 2026-11-10, 90 were refused (CONNECT 403), two
  answered without a usable source, and one served files: `raw.githubusercontent.com`, which served the open-source pack-file tool's
  schemas of the reference's database and of three siblings (dossier §1.2; `hosts.json`). Web search answered about 197 queries before
  the session's search budget ran out; the check step could re-run none of them, so **no SEARCH claim could be CONFIRMED**. No web page,
  no game file with values, no official modding kit, no tooltip text and no forum thread was read.
- **The CODE evidence** is one file for the reference: the pack-file tool's schema `schema_troy.ron`, 6,752,098 bytes, SHA-256
  `8332875a61825d0c67516ccbd155ab9c38c52f628e1ab664f91074cfbffa45e5` (the same file the earlier pass downloaded), re-fetched
  byte-identical by the check step; and three sibling schemas (dossier §1.5 gives their hashes). The file holds key lists with in-house
  descriptions (129 battle-morale keys, 36 army-morale keys among 1,138 campaign variables, 29 fatigue keys, 207 rules keys) and table
  definitions, and **no values**.
- **Corrections to the earlier pass** (A.8): it held the same file and called it "table and field NAMES only"; the key lists with
  descriptions were in it (check contradiction 15). Its host list is superseded by the dossier's (the four hosts it reported as "could
  not resolve" are among those refused by the proxy in this pass).

### A.2 The reference, army level (campaign map) (dossier Q2, Q5)

| Claim (in our words) | Label · scope | Claim id · verdict | Confidence |
|---|---|---|---|
| The data defines an army-wide morale family (36 keys): a starting value, a minimum and a maximum, "low" and "very low" levels, a bonus for winning a battle that lasts some turns and falls off with the armies' relative strength, a penalty for losing one, a penalty scaled by casualties, terms for idleness, foreign soil, being led by the faction leader, the leader's death, looting, raiding, released captives, bankruptcy and attrition; and a table mapping a morale level to an effect bundle. The family is inherited from a predecessor (identical in a sibling), and nothing shows the reference uses it | [CODE] · reference | data-14 · CONFIRMED; data-16 · CONFIRMED (food cap); lineage data-21 · CONFIRMED | definitions ESTABLISHED; use UNKNOWN (Q2-a, Q2-b) |
| No interface description, guide, review or patch note found shows an army morale number on the campaign map | [SEARCH] · reference | official.not_found, community.not_found · WEAK | UNKNOWN |
| Structures exist to carry campaign effects into battle: stance effect bundles, effects bought before a battle, campaign bonus values that act in battle by battle context (one clamp found only in this title) | [CODE] · reference | data-20 · CONFIRMED; data-15 · WEAK (definitions confirmed, the reading is the finder's) | ESTABLISHED (definition level) |
| Conditional morale effects carried into battle: from heroes, traits, items, buildings, gods, agents, events and a camp stance | [SEARCH] · reference | official-23, official-22, official-24, official-20, community-02, community-03, community-04 · WEAK | UNKNOWN |
| Morale effects are shown as battle effects; universal effects were made contextual in a patch | [SEARCH] · reference | official-06, official-07 · WEAK | UNKNOWN |
| Three stocks that battles move are reported: a DLC faction's per-army resource gained after engagements (said to lift morale); one hero's mood; every generic hero's "motivation", moved by wins, losses and, for some traits, by fighting in his own territory or being reinforced, and said to feed the hero's morale | [SEARCH] · reference | official-18 · WEAK; official-19, community-06 · WEAK; targeted-01, targeted-02, targeted-03 · WEAK; structures targeted-04 · CONFIRMED (that motivation is loyalty: INFERRED) | UNKNOWN |
| A battle's result returns to the campaign as experience by result type, captives, loot, item theft and inter-faction loyalty by nine result grades | [CODE] · reference | data-19 · WEAK (definitions confirmed; nine grades, not eight; a sign assumed) | ESTABLISHED (definition level) |
| Reported returns: a resource payout per victory; favour gained or lost by conduct (fleeing loses a god's favour); traits from battle records | [SEARCH] · reference | official-37, official-21, targeted-09 · WEAK | UNKNOWN |
| No key names a "glory", "mood" or "motivation" stock; no value anywhere | [CODE] · reference | data.not_found · CONFIRMED | ESTABLISHED |
| *Not used:* a campaign AI table "found only in this title" taken as evidence of an army morale configured for it | [CODE] · reference | targeted-05 · **CONTRADICTED** (the table is inherited: two siblings define it identically) | — |
| *A mod, not the base game:* a battle-driven campaign morale scale | [SEARCH] · reference (a mod) | community-07 · WEAK | — |

### A.3 The reference, unit level (battle) (dossier Q3, Q4, Q7, Q8)

| Claim (in our words) | Label · scope | Claim id · verdict | Confidence |
|---|---|---|---|
| Each unit's battle morale is a computed value: a starting point plus situational terms, the current value moving toward the computed one each tick; apart from the start, the only unconditional side-wide terms are difficulty modifiers | [CODE] · reference | data-01 · CONFIRMED | ESTABLISHED (definition level) |
| Seven threshold states up to "broken", and a time spent wavering | [CODE] · reference | data-10 · CONFIRMED | ESTABLISHED (definition level); thresholds UNKNOWN |
| Casualty terms in three time windows (a few seconds, a minute, the whole battle) and a "shock" on heavy recent losses | [CODE] · reference | data-03 · CONFIRMED | ESTABLISHED (definition level); sizes UNKNOWN |
| The direction of first contact, exposed or secure flanks, enemy numbers nearby, uphill position, defending a fort or a plaza | [CODE] · reference | data-04 · CONFIRMED | ESTABLISHED (definition level) |
| Being shot at, artillery near misses and hits, friendly fire — terms that can act before any loss | [CODE] · reference | data-05 · CONFIRMED | ESTABLISHED (definition level); size UNKNOWN |
| A "friends routing" concern fed by the balance of enemy and friendly routs within set distances | [CODE] · reference | data-06 · WEAK (only its friendly half is supported; a rise from enemy routs is not) | ESTABLISHED as a definition; effect UNKNOWN |
| Winning or losing a combat, a bonus for killing a share of the enemy quickly, a charge bonus with a timeout, the army on the brink of destruction, a bonus for pending reinforcements | [CODE] · reference | data-08 · CONFIRMED | ESTABLISHED (definition level) |
| Fear and terror (short and long shock routs, then immunity) | [CODE] · reference | data-09 · CONFIRMED | ESTABLISHED (definition level) |
| Rally after a base timeout, a no-rout window after a rally, shattering after a number of routs, a flag making the next rout a shatter once the commander died | [CODE] · reference | data-11 · CONFIRMED | ESTABLISHED (definition level); values UNKNOWN |
| A commander's passive aura and inspiration; the general dead, recently dead or fled; no key for a wounded commander; the title's own hero mechanics (rage, a hero's special state) | [CODE] · reference | data-12, data-13 · CONFIRMED | ESTABLISHED (definition level) |
| Fatigue: activity deltas (marching, running, fighting, standing ready, idling, slopes), seven levels, three fatigue morale concerns, a table of stat multipliers per level (its morale rows unknown) | [CODE] · reference | data-17, data-07 · CONFIRMED | ESTABLISHED (definition level); effect UNKNOWN |
| Reported in play: a morale rating under the health bar; flank attacks damage morale; a hero's death costs the whole army; mythical units cause fear; low-tier units rout easily; players count missile fire; a no-rout window after a rally of about a minute; routers stop after a hero's feat or a destroyed enemy unit and may return | [SEARCH] · reference | official-09, official-10, official-11, official-12, community-10, community-08, community-12, data-22, official-27, community-01 · WEAK | UNKNOWN |
| Reported in play: base morale per unit type tuned in patches; ability effects; a ranged modifier lowering enemy morale | [SEARCH] · reference | official-08, official-13, official-14, community-11 · WEAK | UNKNOWN |
| Reported, mixed: one player and one mod author against a strong flank penalty; a mod author whose table edits took no effect; a battle script toggling the army-destruction term | [SEARCH] · reference | community-09, data-22, data-23, targeted-08 · WEAK | UNKNOWN |
| Fatigue in play: units tire after fighting; mud tires heavy infantry; forced-march fatigue carries into battle | [SEARCH] · reference | official-26, official-25, community-13 · WEAK | UNKNOWN |

### A.4 Siblings (same series and developer; NOT the reference) (dossier §4)

| Claim (in our words) | Label · scope | Claim id · verdict |
|---|---|---|
| A predecessor has the identical battle-morale and fatigue key sets (the reference kept them) | [CODE] · sibling | data-21 · CONFIRMED |
| Two siblings define a minimum morale loss per auto-resolve combat round (and per second of a predicted duel), absent from the reference | [CODE] · sibling | ~~the check step's observation on data-01, re-read by the dossier~~ **[MOR2 r1, MOR2-R1-10: no claim id of its own — the check step's observation in its verdict on data-01 (data-01 · CONFIRMED); re-read by the dossier step: "re-read and found as stated (not a finder claim)" (dossier §4)]** |
| A later sibling defines a battle-balance morale term (relative combat power) the reference lacks | [CODE] · sibling | ~~the check step's observation on data-08, re-read by the dossier~~ **[MOR2 r1, MOR2-R1-10: no claim id of its own — the check step's observation in its verdict on data-08 (data-08 · CONFIRMED); re-read by the dossier step: "re-read and found as stated" (dossier §4)]** |
| An official manual of an earlier sibling: rallying needs the general's radius; shattered units do not respond; a general killed while units rout makes them shatter; a marching army attacked suffers reduced morale (the battle is an ambush); a garrison defending gets a morale bonus | [SEARCH] · sibling | official-28, official-29 · WEAK |
| Siblings: an army attacked on the march starts tired; attrition lowers morale at a battle's start; supplies raise or lower morale; night battles bring a penalty without a night-capable general; an ambushed army's penalty decays over about a minute; a duel's winner lifts his army | [SEARCH] · sibling | official-30, data-27, official-34, community-16, official-32, official-31 · WEAK |
| Siblings: a force morale with a forecast in the script interface; ambush in two stages (detection, then a roll); legacy modders' readings of thresholds; a predecessor's "fightiness" | [SEARCH] · sibling | targeted-06, community-18, community-17, data-25, data-26, data-28, community-15, official-33 · WEAK |

### A.5 Genre patterns (other developers' games; all WEAK) (dossier §3)

| Game | Pattern (in our words) | Claim id · verdict |
|---|---|---|
| Europa Universalis IV | an army morale stock with a maximum; troops deployed in a battle lose morale every day whatever the casualties (the wording of the patch note conflicts with the developer diary) | official-40 · WEAK (check contradiction 10) |
| Mount & Blade II: Bannerlord | a party morale; each soldier's starting morale in battle set from it; desertion below a threshold; agents regain part of their morale | official-41 · WEAK ("losses and fleeing nearby lower it" was not re-found) |
| Battle Brothers | five discrete states changed by event-triggered checks; rally only with no enemy adjacent; fleeing spreads to allies | official-42 · WEAK |
| Gary Grigsby's War in the East 2 | a persistent unit morale moved by results (gated by the leader), not by taking part; a player warns of "feedback loops" between morale and victories | community-19 · WEAK |
| Field of Glory II | discrete cohesion levels changed by tests that shooting losses and charges trigger | community-20 · WEAK |
| Ultimate General: Gettysburg; John Tiller's Civil War Battles | morale moved by flank attacks, casualties, volley shocks, artillery fire and fatigue; fatigue built by activity feeds morale checks | community-21 · WEAK |
| *Not evidence:* a hypothesis about a tactics game's will stat | — | targeted-10 · **UNSUPPORTED** (KNOW, not searched) |

### A.6 What we take and what we do not

- **Taken as patterns** (never content): a stock at each level that events raise and lower (our casualty loss, "the fight it won",
  DA1's rise, DB1's entry cost); a campaign stock that the army's condition returns toward a baseline (A.5's party and army morale;
  DA1); the battle starting from the campaign value (A.5's starting morale); a win term that falls off with the armies' relative
  strength (A.2's inherited definition — a pattern our DA1 sizing B ÷ (B + W) shares; it was designed before this evidence, and is not
  a copy of it).
- **Not taken:** the reference's carry of campaign effects into battle as conditional bonus values and effect bundles (A.2) — the form
  our Law 2 bans as standing modifiers; its computed battle model's per-tick form (A.3), which our resolvers do not use; every name,
  number, description and text of the reference, its siblings and the genre games.
- **For D-083 DB1** (A.7): the reference neither supports nor contradicts an entry cost; the design is our own (DB2).

### A.7 Entering a battle (dossier Q1; the question D-083 DB2 asked of the research)

| Claim (in our words) | Label · scope | Claim id · verdict | Confidence |
|---|---|---|---|
| No battle-morale key (of 129) and no army-morale key (of 36) is a cost for entering a battle, deploying, the battle's start or time spent in battle; the 69 naval morale keys hold none either | [CODE] · reference | data-01 · CONFIRMED; data-14 · CONFIRMED; data.not_found · CONFIRMED | **ESTABLISHED (definition level)** (Q1-a) |
| Whether the game charges morale for entering a battle or being in one | ~~—~~ **[MOR2 r1: [CODE] and [SEARCH] · reference (an absence)]** | ~~—~~ **[MOR2 r1, MOR2-R1-10: no claim of its own (the dossier's sub-answer Q1-b); its bases: data-01 · CONFIRMED, data-14 · CONFIRMED, data.not_found · CONFIRMED (absent from the definitions); official.not_found, community.not_found, targeted.not_found · WEAK (absent from the search summaries)]** | **UNKNOWN** (Q1-b): absent from the definitions and from about 197 search summaries; values, engine behaviour, effect-bundle rows and fatigue-effect rows are not visible |
| Every term that can lower morale with no casualties is conditional: surprise with a timeout (its link to an ambush INFERRED), unpreparedness for a night battle, fatigue states, exposed flanks, enemy numbers nearby, being shot at, a frightening enemy, the direction of first contact, a dead or fled general | [CODE] · reference | data-02 · WEAK (definitions confirmed; readings), data-04, data-05, data-07, data-09, data-12 · CONFIRMED | ESTABLISHED (definition level) (Q1-c) |
| The campaign can set how a battle starts (units start tired after some stances, a landing, a scripted battle or a paid pre-battle action) | [SEARCH] · reference | official-01 … official-05, community-13 · WEAK | UNKNOWN (Q1-e) |
| The one trigger tied to taking part sits on a commander's campaign stock: one motivation trait costs motivation whenever its hero fights a battle in his own territory; another dislikes reinforcing or being reinforced; motivation is said to feed the hero's morale | [SEARCH] · reference | targeted-01, targeted-02, targeted-03 · WEAK | UNKNOWN (Q1-f) |
| No source describes an entry cost (official, community and targeted sweeps) | [SEARCH] · reference | community-01, official.not_found, community.not_found, targeted.not_found · WEAK | UNKNOWN |
| *Not evidence:* "the series recomputes morale rather than drawing it down, so entering has no cost of its own" | [KNOW] · reference | official-39 · **UNSUPPORTED** | — |
| Genre and sibling forms of a cost for taking part: a daily cost for deployed troops (A.5); a minimum loss per auto-resolve round in two siblings (A.4); states at a battle's start for a campaign condition (caught on the march, attrition, night, ambush) in siblings (A.4) | [SEARCH] / [CODE] · genre, sibling | official-40 · WEAK; ~~the sibling CODE observation~~ **[MOR2 r1: the check step's observation on data-01 (data-01 · CONFIRMED), re-read by the dossier, A.4]**; official-29, data-27, community-16, official-32 · WEAK | — |

### A.8 The earlier pass's rows, and what the dossier made of each (dossier §5.4)

**[MOR2, verification round 1, 2026-11-11 (MOR2-R1-10): the verdicts of the Evidence column completed in place (A.0 note).]**

| Earlier row (lane MOR's annex and §2, quoted or summarised) | Now | Evidence |
|---|---|---|
| A.1: "[CODE]: none … table and field NAMES only, no values … labelled [SEARCH]" | **Corrected**: the same file carries the key lists with in-house descriptions (CODE, definition level; FETCH under the stricter reading); still no values | data-01 · CONFIRMED; check contradiction 15 |
| A.1: the host list (six hosts, five "could not resolve", one policy refusal) | **Superseded** by the dossier's 93-host sweep (90 refused at the proxy) | dossier §1.2 (MEASURED host logs, not a claim about the game) |
| A.2 and §2: "Troops have no morale number outside the battlefield"; "On the campaign map there is no army morale number" | **Not established either way** — struck below: no source shows one, and the data defines an inherited army family whose use is unknown | data-14 · CONFIRMED; check contradiction 3 |
| A.2: army-wide percentage modifiers (titles, an item, a camp follower, territory) | **Still WEAK** (reports); the structures that would carry them are defined | official-23, community-03, community-04, official-22 · WEAK; data-15 · WEAK |
| A.2: a per-army faction resource gained after engagements | **Still WEAK**; no such string in the schema | official-18 · WEAK; data.not_found · CONFIRMED |
| A.2: hero motivation, "exact effects not found" | **Still WEAK**, now with battle triggers per trait and a link to the hero's morale | targeted-01, targeted-02, targeted-03 · WEAK |
| A.2: one hero's mood | **Still WEAK**, battle triggers added | community-06, official-19 · WEAK |
| A.2: battle results as experience, loot and captive options; a table of campaign-group morale effects | **Re-established at definition level**, with more channels (theft, loyalty by nine grades) | data-19 · WEAK (definitions confirmed); data-14 · CONFIRMED |
| A.2: "Whether a won or lost battle changes any persistent army-wide morale: not found" | **Defined, use unknown** (win and loss terms lasting turns) | data-14 · CONFIRMED |
| A.3: losses, flanking and the commander's death lower it; beating an enemy unit raises it ("a scaling morale buff"); rout and rally | **Re-established at definition level**; sizes unknown | data-03, data-04, data-08, data-11, data-12 · CONFIRMED |
| A.3: a mod changelog's timed charge bonus and post-rally window | **Still WEAK** (community and mod statements) | community-12, data-22, targeted-07 · WEAK |
| A.3: base morale per unit type (patch notes; a schema field) | **Still WEAK** for the patch values; the per-unit field is defined | official-08 · WEAK |
| A.3: "The database keeps morale constants … names only; the keys and values were not available" | **Corrected** — struck below: the 129 keys and their descriptions are available; the values are not | data-01 · CONFIRMED |
| A.3: an ambush stance; "an ambushed army's morale penalty in this game: not found" | **Still not found** for the reference; the candidate term is "surprised" (link INFERRED); siblings have one | ~~data-02, data-18; official-35, official-36 · WEAK~~ **[MOR2 r1, MOR2-R1-10: data-02 · WEAK (definitions confirmed; readings); data-18 · CONFIRMED ("Definitions only; inherited"); official-35, official-36 · WEAK]** |
| A.3: "The morale states … the exact tooltip factors: not verified [KNOW]" | **Re-established at definition level** (seven states, the factor list); the tooltip text was not read | data-10, data-01 · CONFIRMED |
| A.4: an earlier sibling's official manual (rally radius, shatter) | **Kept as sibling, WEAK**; add the marching-army and garrison terms; the "chain reaction" wording is not that manual's | official-28, official-29 · WEAK |
| A.5: three genre games | **Still WEAK**; the daily cost for deployed troops added to one; one row's "losses and fleeing nearby lower it" not re-found; four games added | official-40, official-41, official-42, community-19, community-20, community-21 · WEAK |
| A.6: "Not taken: the reference's campaign design (no stock, …)" | **The "no stock" premise is not established** — struck below: hero and faction stocks are reported, an army stock is defined | data-14 · CONFIRMED; official-18, official-19, targeted-01, targeted-02 · WEAK |
| §3.3: the three "Pattern seen" cells (rout contagion, fatigue, missile fire) | **Corrected in place** (MOR-V2-05) with A.3's rows | data-06 · WEAK, data-07 · CONFIRMED, data-17 · CONFIRMED, data-05 · CONFIRMED |

---

## Annex A (earlier pass, lane MOR, 2026-11-10) — kept for the record; superseded by the rebuilt Annex A above

**[MOR2, 2026-11-10: superseded by the rebuilt Annex A above. Every row below is at best WEAK (a search summary of a page that could not
be read) unless A.8 above records that the dossier re-established it; rows struck below are corrected or not established. The
original heading of this annex was "Annex A — Reference research (D-069: reference only; nothing here is shipped or player-facing)".]**

### A.1 (earlier pass) Method, provenance, and what could not be verified

- **Question** (D-082 DA2): how the reference handles the army's morale on the campaign map and each unit's morale in battle.
- **Tools.** Web search (allowed). ~~Direct page fetches were refused by the session's network policy for every host tried:~~
  `primagames.com`, `www.pcinvasion.com`, `www.gamepressure.com`, `wiki.totalwar.com`, `www.iskmogul.com` (the fetch tool could not
  resolve them) and `r2encv2.totalwar.com` (proxy: CONNECT refused, 403). `totalwar.fandom.com` and `gamepur.com` were known to be
  denied on 2026-11-10 and were not retried. One host was reachable for a file download: `raw.githubusercontent.com`.
  **[Corrected in verification round 1 (MOR-V1-07): no direct page fetch succeeded, but only one refusal is evidenced as the
  session's network policy: `r2encv2.totalwar.com:443`, recorded by the proxy status as `connect_rejected` (403) at
  2026-11-10T10:05:00Z, the only research host in its failure list. For the other five hosts the fetch tool reported that it could
  not resolve them; the cause is not established (INFERRED: a fetch-tool-side failure, not shown to be a policy denial). Not
  listed before: one GitHub listing request for the schema repository (`Frodo45127/rpfm-schemas`) was refused with "GitHub access
  to this repository is not enabled for this session" (the response is kept in the lane's scratchpad,
  `mor/research/rpfm-schemas-listing.json`; the request URL was not recorded, INFERRED to be GitHub's API host).]**
- ~~**[CODE]: none.** No game file or script was read. The only file-level evidence is a public, MIT-licensed modding-tool schema of
  the game's database tables (Rusted PackFile Manager, `schema_troy.ron`, downloaded from
  `https://raw.githubusercontent.com/Frodo45127/rpfm-schemas/master/schema_troy.ron`, 6,752,098 bytes, SHA-256
  `8332875a61825d0c67516ccbd155ab9c38c52f628e1ab664f91074cfbffa45e5`): table and field NAMES only, no values. It is labelled
  [SEARCH] (a fetched file), not [CODE].~~ **[MOR2, 2026-11-10: corrected by the dossier (data-01, check contradiction 15): the same file
  (same SHA-256) also holds the key lists with their in-house descriptions — 129 battle-morale keys, 36 army-morale keys — which are
  CODE at definition level (no values); see the rebuilt A.1.]**
- **[SEARCH]** claims are summaries a search engine returned for the URL given (the page itself could not be read); **[KNOW]** is
  unverified background knowledge. Anything not found is said to be not found. No number below is used in our design.

### A.2 (earlier pass) The reference, army level (campaign map)

| Claim | Label and source |
|---|---|
| ~~Troops have no morale number outside the battlefield~~; morale is shown under a unit's health bar during battle **[MOR2: the first half is not established either way (data-14, CONFIRMED: an inherited army family is defined, its use unknown); the second is official-09, WEAK]** | [SEARCH] gamepur guide "How troop morale and routing works in A Total War Saga: Troy", `https://www.gamepur.com/guides/how-troop-morale-and-routing-works-in-a-total-war-saga-troy` (summary only; host denied) |
| Army-wide morale comes as percentage modifiers: a general's title (+10 % morale (army), with motivation and melee bonuses), a hero's unique shield that "buffs his army's morale", a camp follower giving +10 % morale for gold, +10 % morale for a hero's units in his own territory | [SEARCH] `https://www.pcinvasion.com/total-war-saga-troy-agamemnon-guide/2`, `https://primagames.com/gaming/total-war-saga-troy-agamemnon-guide/2`, `https://www.pcinvasion.com/total-war-saga-troy-memnon-guide-pharaohs-servant-and-camp-followers/2`, `https://www.gry-online.pl/poradniki/total-war-troy/hektor-hector/z91a205` (summaries) |
| A per-army faction resource ("Battle Glory") is gained after engagements (and by killing captives, razing, a camp stance), raises the army's morale and movement range, unlocks summoned units at thresholds (500, 1,800, 4,000) and resets after use | [SEARCH] `https://primagames.com/gaming/total-war-saga-troy-penthesilea-amazons-guide/2`, `https://pcinvasion.com/total-war-saga-troy-penthesilea-amazons-guide/2`, `https://totalwar.com/?p=11899` (summaries) |
| Heroes have a "motivation" that rises and falls with their preferences (a god, raiding or sacking, fighting with reinforcements) and falls when idle; higher motivation means better performance; exact effects not found | [SEARCH] `https://www.pcinvasion.com/total-war-saga-troy-agamemnon-guide/2`, `https://wireframe.raspberrypi.com/articles/a-total-war-saga-troy-review-empowerment-and-obstacle` (summaries) |
| One hero follows a "mood" that events (wounded or killed heroes and allies among them) move gradually toward thresholds | [SEARCH] `https://primagames.com/gaming/total-war-saga-troy-achilles-guide/2` (summary) |
| Fleeing from battle loses a war god's favour (a campaign consequence of conduct in battle) | [SEARCH] designer interview, `https://wccftech.com/total-war-saga-troy-interview-with-lead-designer-todor-nikolov/2/` (summary) |
| Battle results return as unit experience per result type, post-battle loot, and captive options with effect bundles; the database has a table of campaign-group morale effects (effect bundle, group, key, name) | [SEARCH] schema tables `battle_result_types_tables` (`unit_xp_gain`), `campaign_post_battle_captive_options_tables`, `campaign_group_morale_effects_tables` (RPFM schema, A.1); loot: `https://www.redbull.com/gb-en/a-total-war-saga-troy-tips-guide` (summary) |
| Whether a won or lost battle changes any persistent army-wide morale | **not found**; the sources found describe modifiers, faction resources and experience, ~~not a morale stock~~ **[MOR2: defined at definition level — win and loss terms lasting some turns (data-14, CONFIRMED); use unknown; hero and faction stocks reported (WEAK)]** |

### A.3 (earlier pass) The reference, unit level (battle)

| Claim | Label and source |
|---|---|
| Each unit has a hidden morale value; when it drops too low the unit breaks and flees | [SEARCH] gamepur guide (A.2) |
| Losses and "bad events" lower it; flanking especially; the death of the army's commander (hero) inflicts a heavy penalty on the whole army, making troops likelier to flee or surrender | [SEARCH] gamepur guide; `https://www.gamepressure.com/total-war-saga-troy/battles/z0d813` (summaries) |
| Beating an enemy hero or unit gives "a scaling morale buff" to one's units; reserves restore hope in wavering units; inflicting losses keeps morale high | [SEARCH] gamepur guide; gamepressure guide (summaries) |
| Routing units cannot be controlled; they may stop routing (a hero's feat, or their side destroying an enemy unit) and return after a delay; reviewers saw units rout and rally within a minute | [SEARCH] gamepur guide; `https://explorminate.org/total-war-saga-troy-review/`, `https://saveorquit.com/2021/08/22/review-a-total-war-saga-troy/` (summaries) |
| A community overhaul's changelog implies base-game mechanics: a timed charge morale bonus (90 s, cut to 15 s), a timed post-rally window without rout (60 s, cut to 20 s), separate flank and rear penalties, shattering after a rout | [SEARCH] `https://catalogue.smods.ru/?p=151662` (summary of a mod page; the mod's numbers are its author's claims about the base game) |
| Each unit type has a base morale stat (patch notes cut one monster's base morale from 90 to 65 and lowered high-tier units' base morale by about 5–10 %) | [SEARCH] `https://support.sega.com/hc/en-gb/articles/360015921757-A-TOTAL-WAR-SAGA-TROY-PATCH-1-4-0` (summary); schema field `land_units_tables.morale` (A.1) |
| The database keeps morale constants as key–value pairs (`_kv_morale_tables`, `_kv_naval_morale_tables`) and fatigue effects by fatigue level and stat (`unit_fatigue_effects_tables`) | [SEARCH] RPFM schema (A.1): names only; ~~the keys and values were not available~~ **[MOR2: the keys and their descriptions are available (data-01, CONFIRMED); the values are not]** |
| An ambush stance conceals an army; an enemy passing it starts the battle surrounded; a hero's stance turns a battle into an ambush | [SEARCH] `https://prodigygamers.com/2020/08/20/total-war-troy-recruitment-and-hero-stance-guide/` (summary); an ambushed army's morale penalty in this game: **not found** |
| The morale states up to a rout and their thresholds; the exact tooltip factors (winning or losing the battle, casualties recent and total, fire, fatigue, fear) | **not verified for this game** [KNOW: the series shows a morale bar with warning colours and a list of factors]; see A.4 for the series' official manual |

### A.4 (earlier pass) The same developer's earlier title (official manual; NOT the reference game)

[SEARCH] Rome II's official encyclopedia (summaries of `https://r2encv2.totalwar.com/en/manual/single-player/0087_enc_page_battle_play_phase_conflict_morale/index.html`;
host denied for a direct read): the morale bar turns yellow under a penalty and red when wavering; a general's presence or rally
ability can rally routers within his radius; shattered units do not respond; if a general dies while units are routing they
shatter. Labelled as another title of the series; used only to read A.3's patterns.

### A.5 (earlier pass) Genre patterns (three other games)

| Game | Campaign level | Battle level | Label and source |
|---|---|---|---|
| Mount & Blade II: Bannerlord | party morale raised by victories and raids, food variety and leadership; lowered by casualties, sacrificed troops in retreats, unpaid wages, hunger; low morale causes desertion | each soldier starts a battle with a morale set from the party's; losses and fleeing nearby lower it, enemies killed nearby raise it; a later update let agents regain up to half their initial morale | [SEARCH] `https://www.gamebanshee.com/3wppf` (developer blog mirror), `https://www.taleworlds.com/en/Games/Bannerlord/Blog/68`, `https://gamerguides.com/mount-and-blade-ii-bannerlord/guide/introduction/useful-gameplay-tips/how-parties-clans-and-kingdoms-work` (summaries) |
| Europa Universalis IV | an army's morale is a stock with a maximum set by modifiers; it recovers while resting | battle damage drains it army-wide; an army at zero retreats; starting morale relative to its maximum; a winner can end disorganised | [SEARCH] `https://forum.paradoxplaza.com/forum/threads/europa-universalis-iv-developer-diary-19-war-has-never-been-so-much-fun.665825/`, `https://www.gamepressure.com/europauniversalisiv/the-basics-of-war/z95488` (summaries) |
| Battle Brothers | — | five discrete morale states starting at steady; events trigger morale checks for both sides, positive and negative; a passed check moves a fleeing character back up | [SEARCH] `https://battlebrothersgame.com/?p=883` (developer blog, summary); triggers not confirmed |

Also seen (same series, not used): in Three Kingdoms a duel's winner lifts his army's morale and the loser's retinue loses it
[SEARCH `https://segmentnext.com/2019/05/28/total-war-three-kingdoms-duels-guide/`, summary].

### A.6 (earlier pass) What we take and what we do not

- **Taken as patterns:** a morale stock at the battle level that events raise and lower (our "fight it won", our casualty loss);
  a campaign stock that a victory raises and the army's condition returns toward a baseline (the party-morale and army-morale
  patterns of A.5; DA1); the battle starting from the campaign value (A.5's starting morale).
- **Not taken:** the reference's campaign design (~~no stock,~~ percentage modifiers, faction resources): our Law 2 bans standing
  modifiers, and DA1 asks for a rise of the army's morale itself. Its names, numbers, tooltips and text: none is used. **[MOR2: the "no
  stock" premise is not established — hero and faction stocks are reported (WEAK) and an army stock is defined (data-14, CONFIRMED);
  see the rebuilt A.6.]**

## Dated note — 2026-11-10 (D-083; appended, the text above is unchanged)

- §8's READING M (b) is no longer OPEN. The Director ruled that entering a battle costs morale, with or without casualties
  (`docs/d083-entering-battle-costs-morale-2026-11-10.md`, DB1). The statements that (b) is OPEN or (a) applied are superseded:
  §3.3 (the "Fire without casualties" row), §8 and §9 here, and H-MOR §3, §9, §10.9 and §11.9. Under DB1, a battle won without
  loss changes the winner's morale by DA1's rise less the entry cost, and a battle lost without loss costs the entry cost. The
  prototype at 7f0cb92 has no entry cost yet.

## Dated note — 2026-11-11 (D-085; appended, the text above is unchanged)

- The Director ruled that a battle without fighting does not affect morale at all
  (`docs/d085-no-fighting-no-morale-change-2026-11-11.md`, DD1). §11.8's PROPOSAL (a) is superseded where no fighting took place,
  and alternative (c') is adopted in a wider form: an unfought battle gives no rise and no cost (no entry term, no DR29 decrease).
  The rule of §11.2 now applies only to battles with fighting (DD1-R1 (a): if anyone fought, every formation that entered pays).
  The prototype at 47eac9a does not implement DD1 yet.
