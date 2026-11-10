# Two-level morale — PROPOSAL (M7 lane MOR, 2026-11-10)

**Status: PROPOSAL.** Nothing in this document is a Director decision unless it is quoted from one. Every formula and every number
is lane MOR's design under the delegation of D-082 §5 ("The morale model and every number: the size of the victory rise, decay and
recovery, unit against army morale, and how they feed each other. Delegated (DA2); a PROPOSAL in its lane."). Every interpretation
is a **READING** with both alternatives. It changes nothing in `Sim.*` or `Sim.Data` (D-079 §6, Q5 open); the prototype is in
`prototypes/m7-sprint1/Proto.M7/Morale/`, `Proto.M7/Tactical/LocalWinMorale.cs` and default-identity hooks in lane D's support world,
lane E's war economy and both battle resolvers. The lane report is `prototypes/m7-sprint1/reports/H-MOR.md`.

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
**INFERRED** (argued, not measured); in Annex A also **[CODE]**, **[SEARCH]**, **[KNOW]** (the research provenance labels).

**Entities.** No new entity is proposed (charter §4.5): morale is a field of existing objects — of a category-A formation in battle
and of an army on the campaign — and `MoraleSettlementRow` is a record of a settlement, not an entity.

---

## 0. Summary

| Level | Today (prototype, before this lane) | This proposal | In the prototype |
|---|---|---|---|
| Unit (formation) in battle | casualty-driven loss, rout below a threshold, recovery while holding or rallying without losses; no rise for winning | keep all of it; add **"the fight it won"**: a formation whose enemy breaks under its blows gains morale scaled by the size of what it broke (§3.2); five more mechanisms proposed for later, each with its prerequisite (§3.3) | implemented in **both** resolvers through one shared function; coefficient **0 by default** (exact identity) until the parity battery is re-measured with it on (§3.2) |
| Army on the campaign | lane E's stock (micro units): recovery when fed, paid and loyal; hunger and arrears penalties; lane D: a battle lowers it by its net loss and **never raises it** | DR29's decrease for every participant **plus DA1's rise for the winning side's army**, floor(400,000 × B ÷ (B + W)); no extra defeat penalty for the loser (PROPOSAL (a)); the stock then moves toward a **baseline** at per-sim-year rates (§4) | implemented and on; the baseline is a parameter whose default (the top of the scale) leaves lane E's model unchanged; a baseline below the top is measured as option (b) |
| The link | stand-in hand-off (lane D): start morale passed separately, unchecked | campaign → battle: start = floor(campaign × MoraleMax ÷ 1,000,000), nothing added; battle → campaign: decrease, rise, desertion, medical restore (§5) | the mapping as a pure function; the start check stays M7.15's |
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

The reference keeps two separate things. **On the campaign map there is no army morale number** [SEARCH]: armies carry modifiers
from generals' titles and skills, unique items, faction mechanics (a per-army resource earned in engagements that raises morale
and movement; a hero's motivation that rises and falls with what the hero likes), and those modifiers enter the battle. **In
battle every unit has a hidden morale** [SEARCH] that losses, flanking and a commander's death lower, that beating an enemy unit or
hero raises ("a scaling morale buff"), and that ends in a rout; routed units can stop running and return, and can shatter
[SEARCH]. A battle's result returns to the campaign as experience, loot and faction resources, not as a morale stock
[SEARCH, partly unverified]. The genre patterns studied (Annex A.5) differ on exactly the campaign half: one keeps a party morale
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
| Rout contagion | the reference and the genre: a friendly rout nearby lowers morale (Annex A.3, A.5) | a formation loses ⌊k × Start(routed) ÷ (Start(routed) + Start(own))⌋ when a friendly formation within its sight range routs | a proximity term in the AutoResolver's one-axis geometry (formation gap) |
| Rally of routed men | routed units stop running and return, with a window in which they do not rout again, and shatter on a second rout (Annex A.3) | a routed formation whose entities are out of enemy reach for a time regains Rally morale and returns to Active; a second rout makes its men Escaped for good | an engine state Routing → Active, and lane W's collapse categories (a returned router is Fit) |
| Commander presence and loss | a commander's death lowers the whole army's morale; a commander rallies (Annex A.3, A.4) | a commander entity's radius scales the morale loss; its death is an army-wide loss | a commander entity (generals as commanders, D-079 Q13: open) |
| Flank and rear | flank and rear attacks lower morale (Annex A.3, A.4) | the loss coefficient rises with the angle of attack | formation facing in both resolvers (not modelled) |
| Fatigue | fatigue lowers morale (Annex A.4) | a fatigue stock from exertion, recovered at rest | a fatigue model (none yet) |
| Fire without casualties | missile fire lowers morale (genre) | a loss per incoming volley | **this is READING M (b) in another form ("taking part alone costs morale"), OPEN: not proposed here** (§8) |

---

## 4. Army morale on the campaign

### 4.1 The after-battle change (implemented)

For every army that took part in a battle, at the battle's end instant (the stand-in event, class CampaignProcess):

  decrease = ⌊max(0, start − end − restore) × 1,000,000 ÷ MoraleMax⌋  — DR29, every participant (lane D's net loss, unchanged);
  rise = won ? ⌊R × B ÷ (B + W)⌋ : 0  — DA1, the army of the side the report names the winner;
  after = clamp(before − decrease + rise, 0, 1,000,000)  — one clamp over the sum, so the two terms have no order.

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
about morale-neutral, a cheap win lifts, and a costly win lowers: "down through combat, up for the side that wins" (DA1).

Alternative sizes, considered and not proposed: a constant rise (ignores what was beaten); a rise from the enemy's losses (0 for a
win where the enemy withdrew intact, so "winning raises morale" would fail for that win).

### 4.3 The loser: no extra defeat penalty (PROPOSAL (a))

- **(a) PROPOSED, applied:** the loser takes DR29's decrease only. Its defeat is already in its battle morale — a formation that
  routed ended below the rout threshold, so its decrease is at least start − 300 — and in persons through lane W's desertion of
  the routed men of the side that lost the field. An extra term would count the defeat twice.
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
  desertion of the losing side's routed men, lane D's medical save; then lane E's pulse-end dynamics (§4.4).
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

## 8. READING M (b) — OPEN, presented, not decided

"Morale decreases when troops participate in combat" (DR29).
- **(a) applied:** the decrease is the casualty-driven loss (as the net loss of the battle). A battle won without loss raises the
  winner's morale by DA1's rise alone; a battle lost without loss changes nothing.
- **(b) open:** taking part costs morale even without casualties (per battle, or per engaged pulse). It is a new term in both
  resolvers (or in the hand-off), moves the parity battery and the tactical goldens, and with DA1 a lossless win would net
  rise − cost. A "fire without casualties" term (§3.3) would be one form of (b).
The Director's D-082 left (b) OPEN; this proposal does not choose.

---

## 9. For the Director

No new question. READING M (b) stays OPEN (§8). Everything else here — the size of the rise, the baseline and its decay, the
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
   schema of table names; no game file was read.

---

## Annex A — Reference research (D-069: reference only; nothing here is shipped or player-facing)

### A.1 Method, provenance, and what could not be verified

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
- **[CODE]: none.** No game file or script was read. The only file-level evidence is a public, MIT-licensed modding-tool schema of
  the game's database tables (Rusted PackFile Manager, `schema_troy.ron`, downloaded from
  `https://raw.githubusercontent.com/Frodo45127/rpfm-schemas/master/schema_troy.ron`, 6,752,098 bytes, SHA-256
  `8332875a61825d0c67516ccbd155ab9c38c52f628e1ab664f91074cfbffa45e5`): table and field NAMES only, no values. It is labelled
  [SEARCH] (a fetched file), not [CODE].
- **[SEARCH]** claims are summaries a search engine returned for the URL given (the page itself could not be read); **[KNOW]** is
  unverified background knowledge. Anything not found is said to be not found. No number below is used in our design.

### A.2 The reference, army level (campaign map)

| Claim | Label and source |
|---|---|
| Troops have no morale number outside the battlefield; morale is shown under a unit's health bar during battle | [SEARCH] gamepur guide "How troop morale and routing works in A Total War Saga: Troy", `https://www.gamepur.com/guides/how-troop-morale-and-routing-works-in-a-total-war-saga-troy` (summary only; host denied) |
| Army-wide morale comes as percentage modifiers: a general's title (+10 % morale (army), with motivation and melee bonuses), a hero's unique shield that "buffs his army's morale", a camp follower giving +10 % morale for gold, +10 % morale for a hero's units in his own territory | [SEARCH] `https://www.pcinvasion.com/total-war-saga-troy-agamemnon-guide/2`, `https://primagames.com/gaming/total-war-saga-troy-agamemnon-guide/2`, `https://www.pcinvasion.com/total-war-saga-troy-memnon-guide-pharaohs-servant-and-camp-followers/2`, `https://www.gry-online.pl/poradniki/total-war-troy/hektor-hector/z91a205` (summaries) |
| A per-army faction resource ("Battle Glory") is gained after engagements (and by killing captives, razing, a camp stance), raises the army's morale and movement range, unlocks summoned units at thresholds (500, 1,800, 4,000) and resets after use | [SEARCH] `https://primagames.com/gaming/total-war-saga-troy-penthesilea-amazons-guide/2`, `https://pcinvasion.com/total-war-saga-troy-penthesilea-amazons-guide/2`, `https://totalwar.com/?p=11899` (summaries) |
| Heroes have a "motivation" that rises and falls with their preferences (a god, raiding or sacking, fighting with reinforcements) and falls when idle; higher motivation means better performance; exact effects not found | [SEARCH] `https://www.pcinvasion.com/total-war-saga-troy-agamemnon-guide/2`, `https://wireframe.raspberrypi.com/articles/a-total-war-saga-troy-review-empowerment-and-obstacle` (summaries) |
| One hero follows a "mood" that events (wounded or killed heroes and allies among them) move gradually toward thresholds | [SEARCH] `https://primagames.com/gaming/total-war-saga-troy-achilles-guide/2` (summary) |
| Fleeing from battle loses a war god's favour (a campaign consequence of conduct in battle) | [SEARCH] designer interview, `https://wccftech.com/total-war-saga-troy-interview-with-lead-designer-todor-nikolov/2/` (summary) |
| Battle results return as unit experience per result type, post-battle loot, and captive options with effect bundles; the database has a table of campaign-group morale effects (effect bundle, group, key, name) | [SEARCH] schema tables `battle_result_types_tables` (`unit_xp_gain`), `campaign_post_battle_captive_options_tables`, `campaign_group_morale_effects_tables` (RPFM schema, A.1); loot: `https://www.redbull.com/gb-en/a-total-war-saga-troy-tips-guide` (summary) |
| Whether a won or lost battle changes any persistent army-wide morale | **not found**; the sources found describe modifiers, faction resources and experience, not a morale stock |

### A.3 The reference, unit level (battle)

| Claim | Label and source |
|---|---|
| Each unit has a hidden morale value; when it drops too low the unit breaks and flees | [SEARCH] gamepur guide (A.2) |
| Losses and "bad events" lower it; flanking especially; the death of the army's commander (hero) inflicts a heavy penalty on the whole army, making troops likelier to flee or surrender | [SEARCH] gamepur guide; `https://www.gamepressure.com/total-war-saga-troy/battles/z0d813` (summaries) |
| Beating an enemy hero or unit gives "a scaling morale buff" to one's units; reserves restore hope in wavering units; inflicting losses keeps morale high | [SEARCH] gamepur guide; gamepressure guide (summaries) |
| Routing units cannot be controlled; they may stop routing (a hero's feat, or their side destroying an enemy unit) and return after a delay; reviewers saw units rout and rally within a minute | [SEARCH] gamepur guide; `https://explorminate.org/total-war-saga-troy-review/`, `https://saveorquit.com/2021/08/22/review-a-total-war-saga-troy/` (summaries) |
| A community overhaul's changelog implies base-game mechanics: a timed charge morale bonus (90 s, cut to 15 s), a timed post-rally window without rout (60 s, cut to 20 s), separate flank and rear penalties, shattering after a rout | [SEARCH] `https://catalogue.smods.ru/?p=151662` (summary of a mod page; the mod's numbers are its author's claims about the base game) |
| Each unit type has a base morale stat (patch notes cut one monster's base morale from 90 to 65 and lowered high-tier units' base morale by about 5–10 %) | [SEARCH] `https://support.sega.com/hc/en-gb/articles/360015921757-A-TOTAL-WAR-SAGA-TROY-PATCH-1-4-0` (summary); schema field `land_units_tables.morale` (A.1) |
| The database keeps morale constants as key–value pairs (`_kv_morale_tables`, `_kv_naval_morale_tables`) and fatigue effects by fatigue level and stat (`unit_fatigue_effects_tables`) | [SEARCH] RPFM schema (A.1): names only; the keys and values were not available |
| An ambush stance conceals an army; an enemy passing it starts the battle surrounded; a hero's stance turns a battle into an ambush | [SEARCH] `https://prodigygamers.com/2020/08/20/total-war-troy-recruitment-and-hero-stance-guide/` (summary); an ambushed army's morale penalty in this game: **not found** |
| The morale states up to a rout and their thresholds; the exact tooltip factors (winning or losing the battle, casualties recent and total, fire, fatigue, fear) | **not verified for this game** [KNOW: the series shows a morale bar with warning colours and a list of factors]; see A.4 for the series' official manual |

### A.4 The same developer's earlier title (official manual; NOT the reference game)

[SEARCH] Rome II's official encyclopedia (summaries of `https://r2encv2.totalwar.com/en/manual/single-player/0087_enc_page_battle_play_phase_conflict_morale/index.html`;
host denied for a direct read): the morale bar turns yellow under a penalty and red when wavering; a general's presence or rally
ability can rally routers within his radius; shattered units do not respond; if a general dies while units are routing they
shatter. Labelled as another title of the series; used only to read A.3's patterns.

### A.5 Genre patterns (three other games)

| Game | Campaign level | Battle level | Label and source |
|---|---|---|---|
| Mount & Blade II: Bannerlord | party morale raised by victories and raids, food variety and leadership; lowered by casualties, sacrificed troops in retreats, unpaid wages, hunger; low morale causes desertion | each soldier starts a battle with a morale set from the party's; losses and fleeing nearby lower it, enemies killed nearby raise it; a later update let agents regain up to half their initial morale | [SEARCH] `https://www.gamebanshee.com/3wppf` (developer blog mirror), `https://www.taleworlds.com/en/Games/Bannerlord/Blog/68`, `https://gamerguides.com/mount-and-blade-ii-bannerlord/guide/introduction/useful-gameplay-tips/how-parties-clans-and-kingdoms-work` (summaries) |
| Europa Universalis IV | an army's morale is a stock with a maximum set by modifiers; it recovers while resting | battle damage drains it army-wide; an army at zero retreats; starting morale relative to its maximum; a winner can end disorganised | [SEARCH] `https://forum.paradoxplaza.com/forum/threads/europa-universalis-iv-developer-diary-19-war-has-never-been-so-much-fun.665825/`, `https://www.gamepressure.com/europauniversalisiv/the-basics-of-war/z95488` (summaries) |
| Battle Brothers | — | five discrete morale states starting at steady; events trigger morale checks for both sides, positive and negative; a passed check moves a fleeing character back up | [SEARCH] `https://battlebrothersgame.com/?p=883` (developer blog, summary); triggers not confirmed |

Also seen (same series, not used): in Three Kingdoms a duel's winner lifts his army's morale and the loser's retinue loses it
[SEARCH `https://segmentnext.com/2019/05/28/total-war-three-kingdoms-duels-guide/`, summary].

### A.6 What we take and what we do not

- **Taken as patterns:** a morale stock at the battle level that events raise and lower (our "fight it won", our casualty loss);
  a campaign stock that a victory raises and the army's condition returns toward a baseline (the party-morale and army-morale
  patterns of A.5; DA1); the battle starting from the campaign value (A.5's starting morale).
- **Not taken:** the reference's campaign design (no stock, percentage modifiers, faction resources): our Law 2 bans standing
  modifiers, and DA1 asks for a rise of the army's morale itself. Its names, numbers, tooltips and text: none is used.

## Dated note — 2026-11-10 (D-083; appended, the text above is unchanged)

- §8's READING M (b) is no longer OPEN. The Director ruled that entering a battle costs morale, with or without casualties
  (`docs/d083-entering-battle-costs-morale-2026-11-10.md`, DB1). The statements that (b) is OPEN or (a) applied are superseded:
  §3.3 (the "Fire without casualties" row), §8 and §9 here, and H-MOR §3, §9, §10.9 and §11.9. Under DB1, a battle won without
  loss changes the winner's morale by DA1's rise less the entry cost, and a battle lost without loss costs the entry cost. The
  prototype at 7f0cb92 has no entry cost yet.
