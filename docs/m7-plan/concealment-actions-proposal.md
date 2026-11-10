# Concealment and preparation actions, ambushes and equal sides — PROPOSAL (M7 lane AP, 2026-11-10)

**Status: PROPOSAL.** Nothing in this document is a Director decision unless it is quoted from one. The mechanics of the actions
("etc." included), the definition of "equal", the definition of an ambush and every number are lane AP's design: D-082 delegates
them (DA8: "mechanics DELEGATED"; DA8-R: "etc." delegates the list; DA6-R: the exact test "is delegated design"), and its §5 lists
them among what it does not decide ("The exact definition of "equal" (DA6-R); the concealment actions beyond DA8, with their
costs, durations and detection rules; the Standoff extension (DA6-R)"). Every interpretation is a **READING** with both
alternatives. **Verification round 1** (2026-11-10; findings AP-R1-01..14, H-AP §11): amendments are marked "round 1" with the finding
id; round 1 changed tests, wording and routing only, not the prototype's behaviour. **Verification round 2** (2026-11-10; findings
AP-R2-01..04, H-AP §12): amendments are marked "round 2" with the finding id; round 2 CHANGES the prototype's behaviour in the
war-pulse layer only — what an order may reveal (§3.6) and who counts as an enemy (§3.7) — and adds pins; the battle layer, the
works and the equal-sides rule are unchanged. It changes nothing in `Sim.*` or `Sim.Data` (D-079 §6, Q5 open); the prototype is in
`prototypes/m7-sprint1/Proto.M7/` and the lane report is
[`prototypes/m7-sprint1/reports/H-AP.md`](../../prototypes/m7-sprint1/reports/H-AP.md).

**Authority.**
- [D-082](../d082-morale-caps-ties-director-answers-2026-11-10.md) **DA6** (DECIDED): "If two sides are equal, the defending side
  wins." **DA7** (DECIDED, exception to DA6): "Unless it's an ambush like from a hidden unit, then the winning side wins." **DA7-R
  (a)** (APPLIED reading): "the winning side" is the ambushing side. **DA6-R**: "equal" is "the resolver's result separating neither
  side under its own deterministic arithmetic"; "The exact test (exact equality of the deciding quantity, or a stated band) is
  delegated design and gets a tie-dense test"; "The rule is applied to the AutoResolver"; the extension to the TacticalResolver's
  UNRATIFIED Standoff end is a PROPOSAL. **DA8** (DECIDED direction; mechanics DELEGATED): "Units can spend action points to
  hide/entrench/camouflage etc." **DA8-R**: "The action points are T3's Action Capacity (DR22, C23, two-action windows)"; "Law 2
  applies: each action works through a mechanism (detection, cover and works in the battle grid, time to build), never through a
  free-floating buff." **DA1** with **DA1-R (a)**: a win is the outcome the resolvers report, "DA6 and DA7 for equal sides".
- [D-079](../d079-m7-governance-reconciliation-2026-11-05.md) **DR22** ("Maximum two actions per decision window"; "Integrate this
  with the already-ratified agent-specific Action Capacity rule"; "prevent repetitive movement/attack/retreat loops and hit-and-run
  exploits"), **DR20** (the five-pulse rule: "Distinguish genuine inactivity from active concealment, defence or deliberate waiting"),
  **DR21** ("Terrain, concealment, forest cover, elevation and line of sight must materially influence combat"), **DR26** ("Do not
  invent arbitrary costs or duplicate existing resource models"), **DR30** (War Sandbox), **DR31** (deployment thirds), **DR34**
  (parity), **§4 D5** (legal action combinations DELEGATED), **§4 D11** (of terrain influence in both resolvers: "a one-sided
  addition creates a parity gap by construction"), **§5 Q4** (parity acceptance OPEN), **DR53**.
- [D-080](../d080-war-pulse-director-answers-2026-11-05.md) WA1 (an order at a boundary instant applies to the window that starts
  there) and the T2/T3 layers it confirmed.
- `CLAUDE.md` Laws 1–5. Prior work used as given: T2 `WarFog` (`reports/G-war-pulse-t2.md`), T3 `ActionWindowBook`
  (`reports/G-action-windows-t3.md`), lane TR's terrain, deployment thirds and DIR5 inactivity rule (`reports/H-TR.md`), lane P's
  AutoResolver model 2 and battery (`reports/H-P.md`), lane MOR's `BattleWinner.Reported` (`reports/H-MOR.md`), lane S's field works
  (`reports/H-S.md`), lane N's personal concealment (`reports/H-N.md`), lane D's sandbox (`reports/H-D.md`).

Labels: **DD** quoted Director decision; **READING** (both alternatives, the applied one named); **PROPOSAL**; **MEASURED** (a run of
lane AP, named in H-AP); **INFERRED** (argued, not measured); **PLACEHOLDER** (a magnitude with its direction, no ratified value).

**Entities (unit catalogue §4.5).** No new entity. Hide, Camouflage and Entrench are **category D** actions — "an order with
eligibility, cost and resolution, and at most a pending action state" — on an existing category-A formation (agent). Their state
(hidden, camouflaged, entrenching since an instant) is a field of that formation, not an object: it has no position, command or
lifecycle of its own (questions 1–4 of §4.5 all "no"). Field works are the progress state of a multi-turn action in the unit
catalogue's own sense ("Multi-turn actions ... hold an action state with progress. Progress accrues as a rate × dtYears", §6.3),
realized in a battle as terrain cells; they are counted once, as the entrenched formation's works, and lost when it leaves (§4). Serialized rows:
`ConcealmentStateRow`, `ConcealmentChange`, `EngagementRecord` (`ConcealmentCodec`, populated-table tested).

---

## 0. Summary

| Item | Proposal | In the prototype |
|---|---|---|
| Where the action points are | T3's Action Capacity and the two-action window (DA8-R), at the war-pulse / action-window layer | `ActionKind.Hide`, `Camouflage`, `Entrench` (order kinds 154–156) in `ActionWindowBook`, refusal codes 18–23 |
| Hide | multiplies the distance at which an enemy detects the agent by the **terrain concealment of its cell**; needs a concealing cell, no enemy in contact, not observed; ends on moving, retreating, attacking or being spotted. [Round 2: contact and observation are judged on the enemy agents the issuer's polity detects; an unseen enemy that detects the new concealment spots it at once, the cost spent (§3.6); "enemy" = a polity sharing a war (§3.7)] | implemented, tested |
| Camouflage | multiplies the detection distance by a **camouflage factor** (PLACEHOLDER 1/2) in any terrain; combines with a hide by product; same endings [and the same round-2 rules] | implemented, tested |
| Entrench | builds **field works** at a linear per-sim-year rate (PLACEHOLDER: complete in one day), exact over any partition; the works become **Earthwork** (cover) and **Ditch** (obstacle) cells of lane TR's battle grid; lost when the agent leaves | implemented, tested; two terrain classes appended |
| Further actions ("etc.") | Reconnoitre (the observer side of the same equation) and Lie in wait (an automatic reaction from a hide) as PROPOSALS; not implemented | — |
| Defender | READING AP-1 (a) applied: the side the opening engagement names as attacked; (b) the side that did not initiate contact | `BattleSetup.Defender`; set in every scenario that names one (§5.4) |
| Ambush | a battle opened by an attack from concealment: the attacker held a hide or camouflage and the target's polity did not detect it at the instant of the attack | `EngagementRecord.Ambush` → `BattleSetup.Ambusher` |
| Equal sides | the deciding quantity is each side's **elimination instant**; equal = **exact integer equality** (BothEliminated, BothDisengaged); no band | `EqualSides.Decide`, called by **both** resolvers (READING AP-2 (a); the TacticalResolver part goes beyond DA6-R's applied reading and is put to the Director as Q-AP4, §8 — round 1) |
| Standoff extension | PROPOSAL: a Standoff counts as equal (the defender or the ambusher wins) | option `TacticalRules.EqualSidesAtStandoff`, **OFF** by default |
| Battery effect (MEASURED, H-AP §4) | DA6/DA7 change the reported winner of 57 tactical and 50 auto acceptance battles; no acceptance number moves (16/29, held-out 4/9, attainability 29/29 on every basis) | `equalsides derive` / `verify` / `mor` |
| What an order reveals (round 2, AP-R2-01) | no order's outcome (accepted, or its refusal code) depends on an agent the issuer's polity does not detect; one PAID channel remains, disclosed (§3.6) | `KnownEnemyInContact`, `WatchedByAKnownEnemy`, the spotting at the order's instant; leak gate `KnowledgeLeak_…` |
| Who is an enemy (round 2, AP-R2-04) | a polity that shares a war with the agent's polity; only an enemy spots, watches or is in contact with a concealed agent (§3.7) | `ActionWindowBook.AreEnemies` |

---

## 1. Levels: where each action exists, and why

| Action | War-pulse / action-window layer | Tactical battle | Why |
|---|---|---|---|
| Hide | **ordered here** (spends Action Capacity and a slot of the enclosing window) | not an order; its effect is already the tactical model's own: a still formation in concealing terrain is detected with the stationary-concealment factor (lane TR, Slice D) | DA8-R puts the action points at this layer. Inside a battle, "keeping still in cover" needs no order: the tactical detection equation gives it to every formation that does it, so a second, ordered hide would count the same thing twice |
| Camouflage | **ordered here** | carried in by the hand-off (§5.3) as lane N's personal concealment of the camouflaged formations × the factor | camouflage takes hours of preparation (nets, foliage, discipline): far longer than a tactical pulse (PLACEHOLDER 60 s); it is a preparation brought into a battle, not a battle move |
| Entrench | **ordered here**; the works accrue over sim-time | realized as terrain cells of the battle grid (§4.3) | field works take hours to a day at the PLACEHOLDER rate; at the tactical pulse length a formation could not dig anything material during a battle. The unit catalogue (§6.3) lists "entrench" among an army's strategic actions and "hold/entrench" among the D-011 battle verbs; in battle it is read as the Hold of an already entrenched formation |

PROPOSAL for the tactical layer (not implemented): no new order kind. If a tactical "go to ground" order is wanted later, it should
be an explicit Hold that the DIR5 rule counts as active concealment (exemption (b) already does: an accepted explicit Hold exempts),
not a separate concealment bonus.

---

## 2. The mechanism of hide and camouflage: detection

### 2.1 The war-pulse detection rule (implemented)

Agents stand on the T3 line of cells (positions x in cells; the line is the prototype's stand-in for the campaign map). Each agent
profile has a sight `SightCells`; each agent has a **concealment factor**

    C(a) = (hidden(a) ? Concealment(class of a's cell) : 1) × (camouflaged(a) ? CamouflageFactor : 1)      (Q32, floor per product)

and polity P **detects** agent a at instant t iff a is P's own, or some agent o of P satisfies

    |x_o − x_a| ≤ SightCells(o) × C(a)        compared exactly: |x_o − x_a| × 2^32 ≤ SightCells(o) × C(a).Raw   (Int128)

So a hide or a camouflage **shortens the distance at which an observer detects the agent** — the coefficient sits inside the
detection equation, it is held only while the state lasts, and it ends with it (Law 2). The terrain factor is Slice C's
`ConcealmentTable` (Forest 1/2, Jungle 1/4, Open 1 — PLACEHOLDER values of that table, not new ones), so hiding uses the same terrain
the battle uses. An observer of unlimited sight (`long.MaxValue`, the default) detects everything: a T3 world without sight values
behaves exactly as before (the T3 golden is unchanged; MEASURED).

Detection is a pure function of positions and states. Positions change only through Move and Retreat, states only through the actions
and their endings, so the rule needs no stored "track": it is re-evaluated where it matters (an Attack's target, a concealment
precondition, the ambush test, and after every move — §3.2 "spotted"; [round 2] and at the instant a concealment begins, §3.6).

**Limitation of reusing the battle table on the line (round 1, AP-R1-10).** The hide reads the battle `ConcealmentTable`, in which
Wall and Building are 0 — "irrelevant" in battle, because those classes are never an entity's own cell there. The T3 line accepts every
class below `ClassCount`, and Hide refuses only a multiplier of exactly 1, so a hide on a Wall or Building line cell gets the factor 0:
the agent is then detected only from its own cell (MEASURED by the fix round's probe: an observer one cell away does not detect it,
one in the same cell does). No designed value stands behind this; no shipped world puts those classes on the line. Alternatives for the
campaign integration (not implemented): a campaign-layer concealment table with its own values, or refusing on the line the classes
that cannot be an entity's cell in battle.

### 2.2 Relation to T2's WarFog

`WarFog` decides whether a POLITY sees a WAR (a theatre cell inside an observer's sight disk, DR12 locality). The rule above decides
whether a polity sees an AGENT. They are separate questions and both stay: a hidden army is part of a war its enemies can still see;
an outside observer that sees the war (WarFog) does not thereby see a hidden army in it. PROPOSAL for integration: the sight disks of
WarFog observers become observers of the agent rule too, with the same factor C(a) on their radius (not implemented: the T3 line is
one-dimensional, WarFog is two-dimensional).

### 2.3 Relation to the knowledge seam (integration point; no change now, Q5)

`Sim.Core/State/PolityKnowledge.KnowsUnit` answers "a formation the polity owns, or one whose position ... is visible", visibility
being the polity's settlements' nodes and "its formations' sight discs". The proposal's integration is one coefficient in that
question: a formation holding a hide or camouflage is known only within each observer's sight radius × C(formation). Nothing else in
the seam changes; the AI reads the same seam (no privileged information). Not implemented (D-079 Q5).

### 2.4 In the tactical battle (existing mechanisms, both resolvers)

The tactical detection draw (`TacticalBattle`, lane TR and lane N; the AutoResolver mirrors it per formation, lane P) is
p = best clarity × (exposed ? 1 : StationaryConcealment) × (revealed ? 1 : personal concealment), where a target is exposed if it
moved, was revealed by its own volley, or is seen with clarity 1 (no concealment on the best line). A hidden agent that becomes a still formation in concealing terrain
gets the stationary factor with no new code; a camouflaged ambusher gets its camouflage as personal concealment (§5.3). Both are
lifted the way the engine already lifts them (moving removes the stationary factor; a volley reveals for one step).

---

## 3. The actions (PROPOSAL; implemented as specified)

### 3.1 Costs, preconditions, durations

| | Hide (`ActHide`, 154) | Camouflage (`ActCamouflage`, 155) | Entrench (`ActEntrench`, 156) |
|---|---|---|---|
| Action Capacity cost | `HideCost` of the profile (data; test profiles 2) | `CamouflageCost` (test 3) | `EntrenchCost` (test 4) |
| Slot | one of the polity's two in the enclosing window (DR22, T3) | the same | the same |
| Argument B | 0 | 0 | 0 |
| Preconditions (refusal code) | not already hidden (`AlreadyInState` 22); the cell conceals: multiplier < 1 (`NoCover` 19); no enemy agent within the agent's attack range (`EnemyInContact` 20); no enemy polity detects it now (`Observed` 21). [Round 2, AP-R2-01: 20 counts only enemy agents the issuer's polity DETECTS; 21 only such agents that detect it now (an unseen watcher is judged after the order, §3.6)] | not already camouflaged (22); no enemy in contact (20); not observed (21) [round 2: as for Hide] | not already entrenching (22); the ground can be dug: Road, Open, Forest, Jungle, Hills (`CannotDig` 23) |
| Takes effect | at the order's instant T, in log order among the orders at T (T1/T3 semantics) | the same | the works start accruing at T |
| Lasts | until ended; no per-window upkeep (PROPOSAL; alternative below) | the same | until the agent leaves; the works stop growing when complete |
| Ends | the agent moves or retreats (`Moved`); it attacks (`Attacked`); an enemy agent's move or retreat ENDS inside that enemy's shortened detection distance (`Spotted`; judged at the end position only, §3.2); [round 2] or, at the order's own instant, an enemy polity detects the new concealment (`Spotted`, the order's log index, the cost spent; §3.6) | the same | the agent moves or retreats (`Moved`): **the works are lost** |
| Mechanism | detection distance × terrain concealment (§2.1) | detection distance × CamouflageFactor (§2.1) | works → battle-grid cells (§4) |

"Stationary" is read as: the state belongs to the position where it was taken; any move ends it (a move before it in the same window is
allowed: march, then hide or dig in). "Not in contact" applies to hide and camouflage (one cannot vanish from an enemy at arm's
length) but not to entrench (hasty works under threat are realistic). "Observed" is the anti-exploit core: an agent cannot vanish
while an enemy detects it; it must first break contact. [Round 2, AP-R2-01: both rules bind what the issuer's polity can know — an
enemy it SEES in contact or watching; §3.6 says what happens with an enemy it cannot see. "Enemy" is §3.7's relation.]

Alternative to "no upkeep" (not applied): concealment lasts one window and must be re-ordered each window. Consequence: with T3's
per-polity two slots (T3 Q1, OPEN), a polity could keep at most two agents concealed, and concealment would crowd out every other
action; with per-agent slots it would cost one of each agent's two actions per window. The applied rule makes the cost one-off and
puts the limit in the endings (moving, attacking, being found), which is how concealment fails in the field.

### 3.2 Spotting

After every accepted Move or Retreat of an agent of polity P, every agent of another polity [round 2, AP-R2-04: of a polity at war
with P, §3.7] that holds a hide or camouflage and that P
now detects (§2.1, with its concealment factor) is **spotted**: its hide and camouflage end (`Spotted`, with the move's log index and
instant), in ascending agent id. While an agent stays concealed its factor can only shrink (a further state) and its cell cannot
change, so only another agent's move can make a polity detect it: this is the only place detection of a concealed agent can begin;
at the moment a concealment begins no enemy detects the agent (the `Observed` precondition). Hence the invariant used in §5.2: **a concealed agent is
undetected by every other polity** at every instant between events. [Round 2: the `Observed` precondition now covers only the watchers
the agent's polity can see (AP-R2-01); any other enemy polity that detects the new concealment spots it at that same instant (§3.6),
so the invariant holds as: **a concealed agent is undetected by every ENEMY polity** (§3.7) at every instant between events; a polity
at war with neither side may detect it.]

**Scope (round 1, AP-R1-03).** Only ANOTHER polity's move spots: a polity always knows its own agents, so a polity-mate's move never
ends a concealment (otherwise concealment would be unusable for any polity with two agents); and a Retreat spots exactly like a Move.
Both are pinned by `Spotting_OnlyAnotherPolitysMoveOrRetreatSpots` (neither was tested before round 1).

**Where it is judged (round 1, AP-R1-04).** Spotting is judged from the mover's END position: T3's Move and Retreat are instantaneous
(the agent is at its old cell, then at its new one, with no position in between), so an enemy that marches through or past a concealed
agent's cell does not find it unless it stops within the shortened distance (MEASURED and pinned by
`Spotting_IsJudgedFromTheMovesEndPosition_APassThroughDoesNotSpot`: a Watcher crossing a Jungle hide from 40 to 15 or 10 does not
spot it; ending at 21, 22 or 23 does; at 24 it does not). This is a LIMITATION (§9): a concealed force cannot be found by sweeping
past it. Alternative (PROPOSAL, not implemented): spot from every cell between the old and the new position; it would change the
scripted golden run (INFERRED from the script: its Watcher marches from 50 to 18 across the hidden Raider's cell 22, so the ambush of
window 3 would not happen).

### 3.3 Attacks on concealed agents

An Attack needs its target to be detected by the issuer's polity (`TargetUndetected` 18, checked before the reach): an agent cannot
strike what it cannot see. The refusal code tells the issuer only that the order failed on detection; a presentation layer would not
offer undetected agents as targets at all (the refusal is recorded in the world's log, not in another polity's view). [Round 2,
AP-R2-01: the check also comes before T3's hit-and-run rule, whose record is lifted when the enemy closes on the retreated agent —
seen or not — so the order says nothing of an unseen agent's distance or its movements (§3.6).]

### 3.4 Combination matrix and anti-exploit rules (PROPOSAL, extends T3's)

Row = an earlier action of the same agent in the window, column = the next; T3's 4 × 4 block is unchanged; every cell that involves a
new kind is pinned by a test (H-AP §3.1, `CombinationMatrix_EveryOrderedPairWithANewKind_IsTheProposalsTable`).

| first \ next | Move | Attack | Retreat | Recruit | Hide | Camouflage | Entrench |
|---|---|---|---|---|---|---|---|
| Move | K | yes | no | yes | **yes** | **yes** | **yes** |
| Attack | no | K | no | no | **no** | **no** | **no** |
| Retreat | no | no | K | no | **no** | **no** | **no** |
| Recruit | yes | no | yes | K | **yes** | **yes** | **yes** |
| Hide | **no** | **no** | **no** | **yes** | K | **yes** | **yes** |
| Camouflage | **no** | **no** | **no** | **yes** | **yes** | K | **yes** |
| Entrench | **no** | **yes** | **no** | **yes** | **yes** | **yes** | K |

- **No hide-and-strike in one window** (Hide/Camouflage → Attack: no): an ambush needs its concealment from an EARLIER window, so a
  polity cannot conceal and strike from concealment with one window's two slots (pinned at the pulse boundary, H-AP §3).
- **No strike-and-vanish** (Attack → Hide/Camouflage: no; T3's "Attack → anything" bar extended).
- **No dig-and-leave** (Entrench → Move/Retreat: no): it would only waste the works; with linear works there is nothing to farm.
  This is the unit catalogue's own coupling (§6.3: "An action may declare that it consumes all remaining movement (entrench)").
- **Advance then conceal or dig in** (Move → Hide/Camouflage/Entrench: yes): the marching camp.
- **Entrench → Attack: yes** (strike from the works; the works stay).
- Re-ordering a held state is refused (`AlreadyInState`), so capacity cannot be spent for nothing.
- T3's anti-exploit rules AE1–AE7 hold unchanged (no window farming, no carry-over, a refusal is inert — tested for every new code).

### 3.5 Further actions ("etc.", DA8 — delegated; PROPOSALS, not implemented)

- **Reconnoitre** (scout profiles): for one window the agent's `SightCells` is multiplied by a search factor > 1 — the observer side of
  the same detection equation, so concealment and search meet in one rule. Needed so that hiding has a counter-play other than walking
  into the enemy.
- **Lie in wait**: a hidden agent with this order attacks automatically, as a reaction, the first enemy agent that comes within its
  attack range undetected (an ambush without its own window). It needs a reaction-order model in the T1 event layer (an order the world
  issues on the player's behalf at a later instant), which is a new order-delivery semantic and needs its own turn-exact pin.
- Not proposed: feints and decoys (they need false knowledge entries, which the knowledge seam does not have); engineer-built
  fortification (combat engineers are category B support, a PROPOSAL only in lane D's support-capabilities proposal, with their
  effects placed in M7.9).

### 3.6 What an order reveals (round 2, AP-R2-01; PROPOSAL)

**The problem (MEASURED on `16efc0a`, H-AP §12.2).** A refused order is inert: it spends no slot and no Action Capacity (T3's AE5).
Before round 2 four outcomes depended on agents the issuer's polity did not detect, so any polity could probe for them FREE: a Hide or
Camouflage refused `EnemyInContact` only because an unseen enemy stood within the agent's attack range; refused `Observed` only because
an unseen enemy watched it; a Retreat accepted only because of an unseen contact, and refused only because it led toward one; an
Attack refused `HitAndRun` or `TargetUndetected` depending only on whether an unseen enemy had moved toward the attacker (the
hit-and-run record is lifted by the enemy's approach). For DA7 this meant that the intended victim of an ambush — the ambusher waits
in its attack range for at least one window (§3.4) — could locate the ambusher to within that range at no cost and retreat away from it.

**The rule — (a), applied.** An order's LEGALITY — accepted, or its refusal code — depends only on what the issuer's polity knows: its own
agents, and the agents it detects (§2.1). Concretely:
- `EnemyInContact` (20) counts only enemy agents (§3.7) that the issuer's polity detects (`KnownEnemyInContact`);
- `Observed` (21) counts only enemy agents that the issuer's polity detects AND that detect the agent now, with its current factor
  (`WatchedByAKnownEnemy`; an observer's sight is profile data, read as known once the observer is detected);
- T3's Retreat: its contacts are the agents within its attack range that the issuer's polity detects (with T3's default unlimited
  sight, every agent — T3 is unchanged), and its hit-and-run records name only those;
- an Attack checks the target's detection (`TargetUndetected`) before T3's hit-and-run rule (§3.3).

What follows an ACCEPTED order is the world's own rule. A Hide or Camouflage begins at its instant T, in log order; if an enemy polity
detects the concealed agent at T (with its new factor), the new concealment is **spotted at once** (cause `Spotted`, the order's own log
index, at T), and the slot and the cost stay spent. This is §3.2's spotting rule applied at the one other instant at which detection of
a concealed agent can begin, so the invariant of §3.2 holds (a concealed agent is undetected by every enemy polity between events).

**What remains (disclosed).** One PAID channel about an unseen agent's present position, two FREE channels that reveal no unseen
agent's present position or state (both T3's rules), and one event that is not an order:
1. *The paid channel.* A Hide or Camouflage that is spotted at once tells its polity that some unseen enemy detects the concealed agent
   — at the price of one of the polity's two slots of the window and the action's Action Capacity. Its radius is the watcher's sight ×
   the NEW factor (a sight-6 watcher finds a Jungle hide within 1.5 cells, a camouflage within 3), not the watcher's full sight. The leak
   gate asserts this as the one documented difference (H-AP §12.3).
2. *Static: the number of agents (free).* An Attack on an agent id beyond the world's agents is refused `OutOfReach` (T3's line), an
   unseen agent's id `TargetUndetected`: a polity can learn how many agents the world has (not where they are or what they do). Not
   changed: T3's convention; the knowledge seam's own identities (ADR-037) would hide it.
3. *Past movement of an agent seen again (free).* T3 lifts a hit-and-run record when the enemy closes on the retreated agent, seen or
   not. Once that enemy is detected again, an Attack refused `HitAndRun` or not tells whether it closed in while unseen. Not changed
   (T3's lifting rule); the fix would lift only on an approach the retreated agent's polity detects.
4. *Being spotted.* A polity sees its own agent's concealment end (`Spotted`) when an enemy's move finds it, whether or not it sees that
   enemy — by design since round 0 (a polity knows its own agents' states); it costs the observer a move, not the concealed side an order.

**Alternatives (not applied).** (b) Judge the at-once spotting with the agent's PRE-order factor ("watched as it went to ground": an
unseen watcher that detected the agent before the order keeps it, exactly as a seen one refuses it) — physically stricter, but the paid
channel's radius becomes the watcher's full sight. (c) Refuse as in rounds 0–1 — free probes (AP-R2-01). (d) Accept with no at-once
spotting — the concealment would hold although an enemy detects it, and end only at that polity's next move anywhere (an artefact of
spotting on moves). Consequence of (a): between sight × new factor and sight, a SEEN watcher refuses the order (`Observed`) while an
UNSEEN one at the same distance loses the agent; the anti-exploit rule binds what the player knows ("one cannot vanish from an enemy one
sees watching"), and the detection equation decides the rest.

**Gate.** `KnowledgeLeak_NoOrderOutcomeDependsOnAnEnemyTheIssuerDoesNotDetect` and `KnowledgeLeak_AnAttackOnAnUnseenAgent_SaysNothingOfItsMovements`
(metamorphic on hidden state with a positive control — ADR-037 §4's precedent): four placements of an unseen enemy, every order kind;
identical outcome, capacity, slots, position and retreat records, except the documented paid channel. The order-delivery semantic is
pinned by `SpottedAtOnce_AConcealmentIsJudgedAtItsOwnInstant_InLogOrder` (same instant, log order: refused by a seen watcher; spotted at
once by the order's own log index; held, then spotted by the later move; held).

### 3.7 Who is an enemy (round 2, AP-R2-04; PROPOSAL)

**Applied.** Polities p ≠ q are ENEMIES iff some war of the world lists both as participants (`ActionWindowBook.AreEnemies`; fixed at
construction). Only an enemy polity's move spots a concealed agent (§3.2), only an enemy's agent is a contact (`EnemyInContact`) or a
watcher (`Observed`), and only an enemy polity spots a concealment at its instant (§3.6). A polity that shares no war with the agent's
polity may detect it (`Detects`) without ending it: its knowledge does not reach the war enemy, so the concealment against that
enemy survives (MEASURED, H-AP §12.3).

**Alternatives (not applied).** (a) Every other polity (T3's convention; rounds 0–1): a non-belligerent's move ends a hide that the war
enemy has not found, and its agents refuse a hide by contact or watching (MEASURED on `16efc0a`); a third party could blow an ambush,
even by accident. (b) Only co-participants of a war IN PROGRESS: the relation would change at a war's start and end, instants at which no
event judges a concealment already in view (a concealed agent could be detected by a new enemy with nothing to spot it); a static
relation has no such instant.

**Unchanged.** T3's Attack and Retreat keep T3's convention: any other polity's agent is a possible target or contact (whether a polity
may attack a non-belligerent is T3's and M8's question, not lane AP's). When the diplomacy model (M8) exists, its relation replaces
this one.

---

## 4. Entrench: field works

### 4.1 State and rate (implemented)

An entrenching agent holds `EntrenchSince` (the order's instant). Its works at instant t are

    W(t) = min(WorksFull, floor(rate × (t − since) ÷ year))   ppm of complete works, year = 360 × 86,400,000 ms (ADR-002)

computed through T1's `RateIntegrator.IntegrateExact` (the exact integral of a linear per-sim-year rate). Any partition of the elapsed
time into sub-steps — a War Pulse straddling a turn boundary, an era band edge — gives the same value (the integrator's invariant;
MEASURED by a metamorphic test over random partitions), so the works do not depend on where boundaries fall (Law 3, DR13).
PLACEHOLDER rate: 360,000,000 ppm per year = complete works after one day of the formation's own labour (direction: a field force could
fortify its own position within a day — the Roman marching camp, Polybius VI.27–42 [KNOW, not fetched]; the magnitude is not cited).
Linear accrual is the engineering form (works volume ÷ digging rate); lane S's saturating field-works term `dE/dt = bL(1 − E) − eE`
is the siege's lines under bombardment and stays lane S's (DR26: not duplicated; only T1's integrator is reused).

### 4.2 Conservation

The works move no stock: the labour is the formation's own time, already paid for by its upkeep (lane E; INFERRED), and no material is
drawn. MEASURED:
every ledger account is unchanged by the three actions, exactly. PROPOSAL for later: works that need timber or tools draw them through
lane E's existing stock draw (`StockDraw`), never through a new resource model.

### 4.3 The works on lane TR's battle grid (implemented)

Two terrain classes are appended (codes 11, 12; every earlier code and hash is unchanged): **Earthwork** (the parapet and trench line
the formation occupies) and **Ditch** (the obstacle in front). Their coefficients enter lane TR's equations unchanged:

| class | cost | concealment | layer | opaque | missile transmission | footing | order cap |
|---|---|---|---|---|---|---|---|
| Earthwork | 2,000 | 1/2 | 1,500 mm | no | **1/4** (cover for an entity in it) | 1 | 1 |
| Ditch | 3,000 | 1 | 0 | no | 1 | **1/2** | **1/2** |

All PLACEHOLDER; the ditch's footing and order equal lane TR's Ford (direction CITED by lane TR: footing — Agincourt; order — Polybius
XVIII.31, which names ditches among the ground that breaks the phalanx). `FieldWorks.Realize` turns the defender's works into cells at
two thresholds (PLACEHOLDER): at W ≥ 1/2 every diggable cell under its formations' entities becomes Earthwork; at W = complete the
diggable cell one cell in front of each (toward the enemy, away from its exit edge) becomes Ditch. Heights are kept. The cells are a set
(order-independent, tie-dense tested). Then nothing else is special: an arrow at a man in an Earthwork cell is intercepted by the
cell's transmission (MEASURED exact: the open-ground chance × 1/4); an attacker in contact stands in the ditch with footing 1/2 and
cohesion 3/4 (MEASURED exact: attack factor 3/8). MEASURED (lane P's hold-line-open, seeds 1–8, tactical): attacker losses 49 without
works, 134 with; defender losses 151 and 133; the defender won 0 and 2 of 8.

**In both resolvers (round 1, AP-R1-05; D-079 §4 D11: terrain influence "in both resolvers together").** The works are terrain classes,
so the AutoResolver's model 2 reads them through the same `TerrainEffects` table (footing and cohesion of the cells in melee, cover by
`TerrainSight.FirePath` for missiles; INFERRED from lane P's model description and code, not isolated per coefficient). MEASURED, same
scenario, bare → with complete works (attacker losses; defender losses; defender wins):

| Resolver | Seeds 1–8 (pinned in direction by `AnEntrenchedLine_FaresMateriallyBetter`) | Seeds 1–80 (the fix round's probe) |
|---|---|---|
| TacticalResolver | 49 → 134; 151 → 133; 0 → 2 | 446 → 1,264; 1,486 → 1,343; 0 → 29 |
| AutoResolver | 55 → 140; 148 → 128; 0 → 6 | 538 → 1,333; 1,492 → 1,290; 0 → 42 |

Both resolvers move the same way. The defender's win count with works differs by 13 of 80 battles (29 against 42, 16.25 points)
between them: a parity observation for lane P's next re-measurement (D-079 Q4 stays OPEN), not normalized and not tuned here.

The works are **lost when the unit leaves** (the wording of lane AP's brief — the orchestrator's, not a Director decision; applied as a
PROPOSAL; round 1, AP-R1-13, corrected the earlier attribution "DA8 brief"): a move or retreat ends the entrenchment and the works with
it.
Alternative (not applied): the works stay on the map, decay, and serve whoever occupies them (captured trenches) — more realistic, but
it makes works a map object with its own lifecycle (a category question under §4.5) and needs a map-state row.

Limitations: the battle grid's 20 m cells make one cell the whole frontage of a 50-man block; works in a forest cell replace the canopy
(the class grid has one class per cell); the thresholds turn a continuous W into two steps.

---

## 5. Battle roles: the defender and the ambush

### 5.1 The defender — READING AP-1

The prototype's battle setup had no battle-level attacker or defender (lane S's siege rows name one; lane P's Defend posture gives one
side a holding doctrine). DA6 needs one.

- **(a) APPLIED — the ATTACKED side.** The defender is the side the engagement that opened the battle names as attacked: at the
  war-pulse layer, the side of the target of the opening Attack action (T3: every engagement is an Attack by one agent on another);
  for a siege assault, the garrison (lane S: "the siege's defender force is the garrison aggregate"; its siege row already names
  the defender, the installation's controller, and its maximum-duration rule already gives a tie to it: "a tie → the defender
  holds", `reports/H-S.md`); in a hand-built scenario or
  the sandbox, the side whose position the other advances on (a Defend posture, a held block, a waiting ambush in its own third). A
  meeting engagement of two advancing forces names none (-1), and DA6 then has no side to give the battle to.
  Consequence: explicit, recorded at the battle's creation, no movement history needed; the ambusher is always the attacker.
- **(b) The side that did NOT INITIATE CONTACT.** The defender is the side whose position the other's movement entered — the side that
  was still when the two came within reach. Consequence: needs the movement history of the window, and a rule for two forces that
  both moved; an ambusher waiting in place becomes the defender (DA6 and DA7 then agree), a raider that crept up becomes the attacker.

INFERRED (reading (b) is not implemented, so this is argued from each scenario's layout, not run; round 1, AP-R1-12): both readings
give the same defender in every existing scenario (§5.4). Carried on the setup as `BattleSetup.Defender` (-1, 0, 1; default -1), read
only by the equal-sides rule at the battle's end: it never changes the battle (MEASURED: the committed battery digests are unchanged).

### 5.2 The ambush

**Definition (applied).** A battle is an **AMBUSH** iff it was opened by an attack launched from concealment: at the instant T of the
opening Attack, the attacking agent held a hide or a camouflage **and** the target's polity did not detect it at T (§2.1). By the
invariant of §3.2 the second condition follows from the first; it is kept in the definition and in the code as the meaning. [Round 2:
it follows for a target of an ENEMY polity (§3.7); T3 lets an agent attack any other polity's agent, and a polity at war with neither
side may detect a concealed attacker, whose attack on it is then not an ambush.] The
engagement is logged (`EngagementRecord.Ambush`); the hand-off sets `BattleSetup.Ambusher` = the attacker's side. Deterministic:
a pure function of the war-pulse state at T, evaluated in log order.

**READING AP-5 (what an ambush is; round 1, AP-R1-12).** (a) APPLIED: an attack launched FROM CONCEALMENT, as defined above (DA7:
"like from a hidden unit"; DA7-R (a): "an ambush launched from concealment"). (b) Any attack whose attacker the target's polity did not
detect at T, concealed or not (the wording of lane AP's brief: "attacking formations were undetected by the defender at the instant
contact is made"). They differ for an unconcealed attacker its victim does not see, which the prototype allows whenever the attacker's
reach exceeds the victim's sight (MEASURED by the fix round's probe: a Raider attacks from one cell an agent whose polity has sight 0;
that polity does not detect it, and the engagement is not flagged an ambush under (a)). (b) would let DA7 reward reach over sight with
no concealment action and let an ambush arise in the open; (a) ties the flag to an action the attacker paid for (DA8). (a) is applied
because it is DA7-R's own wording; (b) is one line of the ambush test (drop the state condition).

An Ambush **posture** inside a battle (lane P's jungle-ambush and forest-ambush-archers; Sprint 1 #8) is a tactic of a side that holds
its position and waits; at the battle level that side is the defender (§5.1) and the battle is not flagged an ambush.

**READING AP-3 (where the ambush is judged).** (a) APPLIED: at the war-pulse engagement, flagged on the setup by the battle's creator;
the resolvers trust the flag. (b) The resolver confirms it at the first contact of the battle: DA7 applies only if the ambusher's
first contacting formation was not tracked by the victim's side just before that instant. (b) would make a planned ambush that the
victim found first count as a normal battle, at the cost of a resolver-dependent judgement (the two resolvers' first contacts differ by
model, a parity risk) and of reading pre-battle knowledge the resolvers do not have (both start a battle with empty knowledge).

### 5.3 The hand-off (`EngagementBattle.Build`, a stand-in for M7.15)

From an engagement and the battlefield the caller lays out (formations deployed in their thirds, terrain): Defender = target's side;
Ambusher = attacker's side iff ambush; the target's works at T realized as cells; an ambusher's camouflage carried as personal
concealment × CamouflageFactor on its formations' profiles (identity, caps and every other stat unchanged). MEASURED: carried
camouflage never makes the first detection earlier in either resolver (common random numbers) and makes it later in 14 of 28 runs.

PROPOSAL (not implemented): the ambusher's side starts the battle tracking the victim's formations (it observed them before it
struck), the victim's side starts with nothing — surprise as knowledge, not as a damage bonus. Today both sides start a battle with
empty knowledge, so an ambush's battle effect is only its camouflage and DA7.

### 5.4 What every existing scenario sets

| Builder | Defender | Ambusher |
|---|---|---|
| Lane P battery (`ParityBatteryP.DefenderOf`): the terrain-defence family (hill-crest, ford-crossing, marsh-front, forest-hold-archers, defile, archers-on-hill, hold-line-open, high-ridge, narrow-ford-archers, urban-street), archers-vs-infantry-forest, jungle-ambush, forest-ambush-archers | 1 (role B) | -1 |
| Lane P battery: the 16 meeting engagements (equal-infantry, numbers-2to1, low-supply, low-morale, leadership-edge, experience-edge, archers-vs-infantry-open, combined-arms, riflemen-vs-infantry, sniper-screen, shock-infantry, numbers-5to4, mixed-conditions, meeting-asymmetric, combined-arms-vs-mass, riflemen-vs-archers) | -1 | -1 |
| Sprint 1 `ParityScenarios`: #4 archers-vs-infantry-forest, #8 ambush | 1 | -1 |
| Sprint 1 `ParityScenarios`: the other eight | -1 | -1 |
| Slice D `TacticalScenarios.Meeting`, `MeetingAsymmetric`, `CombinedArms` (meeting engagements) | -1 | -1 |
| Lane N `MultiDomainScenarios.Amphibious` (side 1 holds the shore) | 1 | -1 |
| Lane N `LineBattle`, `Duel` (fleets meeting) | -1 | -1 |
| Lane S siege assaults (no battle setup yet: the odds stand-in) | the garrison when built (READING AP-1 (a)) | — |
| Lane D sandbox `BaseSetup` (Quick Battle, Scenario Editor, Battle Test) | -1 (no role edit yet; §5.6) | -1 |
| Lane TR `TerrainBench`, every unit-test setup | -1 unless the test sets it | -1 |

### 5.5 Deployment thirds (DR31) and the ambush

DR31: "Each army deploys within its own one-third." "The middle one-third is undeployable before battle." It has no exception, and none
is proposed here. Consequences: (1) an entrenched defender's works lie under its deployed formations, inside its own third; the ditch in
front may fall in the middle third — it is terrain, not a deployment (lane TR's thirds rule: "The rule constrains start positions
only"; INFERRED that terrain cells are not a deployment). (2) An ambush
battle deploys like any other: the ambusher in its own third, so the war-pulse surprise does not become a tactical close-range
surprise; it acts through DA7, carried camouflage and (proposed) starting knowledge. (3) An Ambush posture inside a battle can only wait
in its own third (lane TR limitation 31). (4) The thirds are enforced only for a setup deployed through lane TR's deployment phase
(`Deployment = Thirds`); the hand-off (§5.3) takes the battlefield its caller lays out and does not deploy it, and two shipped
scenarios (the Sprint 1 `Ambush`, lane N's `Amphibious`) run `Unrestricted` with forces in the middle third (lane TR [r1, V8],
P-TR13). Whether ambush battles may start inside the middle third would amend DR31; lane D's sandbox proposal (§5.1) leaves it to the
Director, and D-082 §5 keeps the two thirds-violating scenarios OPEN — see §8 Q-AP2.

### 5.6 The War Sandbox (lane D) — offer the actions there too (PROPOSAL, not implemented)

After `CloseDeployment` and before `StartBattle`, two logged and replayed edits: **SetRoles**(defender, ambusher) — the user names
them (DR30 "Unrestricted experimental force creation" covers it; default none) — and **Prepare**(formation, Hide | Camouflage |
Entrench, preparation time) — the session applies exactly the campaign hand-off: works W = `FieldWorks.WorksAt(0, time, rate)` realized
by `FieldWorks.Realize`, camouflage as personal concealment. AI-vs-AI battles then run on the AutoResolver (DR30: "AI-vs-AI sandbox
battles use AutoResolver"; the TacticalResolver where the AutoResolver has no model, lane D's READING A), and DA6/DA7 already apply in
both. Caps (DA4) are untouched. A Campaign Encounter would take its roles and preparations from the campaign engagement, not from
edits (its terrain is inherited and locked, DR30).

Already possible without new code (INFERRED from the code, not tested by this lane): the Scenario Editor's `EditTerrain` accepts every
class below `ClassCount`, so it can paint Earthwork and Ditch cells by hand before the deployment opens; the battle then treats them
like any terrain. Prepare would place them from a formation's works instead of by hand.

---

## 6. Equal sides (DA6, DA7)

### 6.1 The deciding quantity and "equal" (DA6-R, delegated design)

Both resolvers end a battle at the first instant a side has no fighting entity left on the field (DR19: "dead, retreated, routed or
surrendered"), or by the inactivity rules. Let E_s be the instant side s is out (the step in the AutoResolver, the event instant in the
TacticalResolver; the pulse end for a withdrawal under the legacy rule; infinite while it still fights). The battle ends at
min(E_0, E_1); the result **separates** the sides iff E_0 ≠ E_1. **Equal** is the exact integer equality E_0 = E_1:

- both finite and equal → `BothEliminated` (the last fighting entities of both sides went in the same step / instant) or
  `BothDisengaged` (both withdrew at the same pulse end, legacy rule);
- both infinite → lane TR's `Standoff` (whole-field idle; UNRATIFIED P-TR1) — equal only under the extension (§6.4).

**No band.** A band on a secondary quantity (casualties, survivors, strength) was considered and not applied: it would override results
the resolvers DID separate (a side that held the field with one entity left would lose inside the band), it would add a victory
criterion neither resolver has (DA1-R (a): a win is the outcome the resolvers report), and it needs a new unratified constant on two
different scales (entities against aggregate strength). Exact equality changes only results that today name no winner. READING
AP-4 (alternative, not applied): among equal results, compare the surviving strength share and let DA6 decide only within a band.

### 6.2 The rule (implemented: `EqualSides.Decide`)

    decided end (SideEliminated, SideDisengaged)      → the other side (WinBasis.Decisive); roles never change it
    equal end and the setup names an Ambusher        → the ambusher (AmbusherOnEqualSides; DA7, DA7-R (a))
    equal end and the setup names a Defender         → the defender (DefenderOnEqualSides; DA6)
    otherwise (no role; a Standoff without the extension; None) → no winner (WinBasis.None)

Tie-dense tests at and around the boundary (H-AP §3): the full truth table; tactical duels where the two sides' last entities die at
the same instant (margin 0 HP) and where one HP more keeps a side alive through it (±1); the AutoResolver's mutual one-blow kill; the
legacy withdrawals at the same pulse end and one pulse apart, in both resolvers.

### 6.3 Where it applies — READING AP-2

- **(a) APPLIED: both resolvers, through the one shared function**, on their simultaneous ends. DA6 is decided as a general rule
  ("If two sides are equal, the defending side wins"); D-079 §4 D11 says of terrain that "a one-sided addition creates a parity gap
  by construction", and the same holds for an end-of-battle rule (INFERRED); lane MOR's report defers "DA6 and DA7 winners reported
  by the resolvers" to this lane (`reports/H-MOR.md`, deferred items).
- **(b) The AutoResolver only** (DA6-R's literal "The rule is applied to the AutoResolver"): tactical equal ends keep no winner.
  MEASURED consequence on the battery: no verdict changes either way (H-AP §4, basis B3), but the tactical and auto "neither" classes
  then count different things.
- **Routing (round 1, AP-R1-11).** DA6-R's applied reading names the AutoResolver ("The rule is applied to the AutoResolver"). (a) goes
  beyond it for the TacticalResolver's BothEliminated and BothDisengaged ends, so the TacticalResolver part is lane AP's PROPOSAL,
  applied in the prototype pending a ruling and put to the Director as **Q-AP4** (§8), not applied without a question as round 0 said.

The reported winner is on the result (`BattleResult.Winner`, `WinBasis`); `BattleWinner.Reported` returns it for DA6/DA7 and reads the
end reason as before otherwise, so lane MOR's DA1 rise follows with no change to its rule (MEASURED: the defender's army rises
800,000 − 100,000 + 200,000 = 900,000 after an equal battle; in an ambush the ambusher's does). The result hash takes the winner only when
DA6/DA7 decided it, so every earlier result hashes as before.

### 6.4 The Standoff extension (DA6-R PROPOSAL; option OFF by default)

Analysis. A Standoff ends a battle in which nobody progressed for five pulses (both sides holding, waiting or idle in view). Read with
DA6's sense — the defender keeps the field when the attacker fails to take it — a Standoff is the clearest "equal" case: the attacker
neither won nor was beaten. Arguments for: it gives every battle with a designated role a winner, so DA1 and lane W's post-battle
outcomes always have one. Against: (1) the Standoff itself is unratified (P-TR1), and the extension would make an unratified end
decide wins; (2) a passive attacker AI would "lose" battles it never fought, and with DA1 the defender's army would gain morale without
a blow struck (lane MOR may want a smaller rise for an unfought win); (3) it also covers a waiting ambusher that never sprang.

Implemented as `TacticalRules.EqualSidesAtStandoff` (default OFF), honoured by both resolvers through the same function. MEASURED: on
lane P's battery it changes nothing (the only Standoffs are 2 tactical meeting-asymmetric battles, which have no defender); on lane N's
amphibious battle (a Standoff under DIR5, the shore side 1 the defender) it changes only the reported winner, from none to side 1.

**Observation (round 1, AP-R1-14; for lane MOR and the Director, not decided).** Argument (2)'s "morale without a blow struck" has a
counterpart without the extension: under DA6 with DA1-R (a), a defender whose battle formations were ALL eliminated (BothEliminated) is
the reported winner, and its army's campaign morale rises (MEASURED: `DA1_TheEqualSidesWinnerRises_DefenderOrAmbusher`, where both
duel entities die at one instant and the defender's army ends at 800,000 − 100,000 + 200,000 = 900,000 against 700,000 for the other
side; the same holds for an ambusher). Whether a win by mutual elimination should raise morale as much as a win that holds the field
belongs to the delegated morale model (DA2; lane MOR's rise size and its recipients, DA1-R); this lane changes nothing there.

---

## 7. Measured effect on lane P's battery (summary; the tables are in H-AP §4)

On the committed basis (rule P1 on the field holder) nothing can change and nothing did: battery hash `BEA60C2F…5B236442` (MEASURED,
unchanged). With the reported winner (DA6/DA7) on both resolvers, 57 tactical and 50 auto acceptance battles change winner: every
equal end in the 13 scenarios that name a defender (they occur in 5 of them); the "neither" class of those scenarios goes to role B.
**No acceptance number moves**: 16/29
(held-out 4/9, tuning 12/20), attainability 29/29, and no scenario's verdict or failing-check list changes — on every basis (reported
both, auto only, with the Standoff extension). With lane MOR's in-battle rule at c = 200 as well (information only): 16/20 on the
tuning set and 16/29 on the acceptance set, again on both bases. The acceptance battery was consumed by lane P (H-P §5.4): these are
information, not acceptance evidence; lane P's acceptance stays OPEN (D-079 Q4).

---

## 8. Questions for the Director (only what is genuinely open)

- **Q-AP1 — Who defends (READING AP-1).** (a) the attacked side, applied; (b) the side that did not initiate contact. They differ only
  when an attacked force had itself moved into the attacker's reach, and for an ambush (under (b) a waiting ambusher is the
  defender). Recommendation: (a), because the engagement records it and it needs no movement history.
- **Q-AP2 — Ambush battles and the thirds (DR31).** Should a battle opened by an ambush start with the forces closer than the thirds
  allow (the ambusher inside the middle third, the victim caught on the march)? DR31 has no exception and this lane implements none;
  the tactical effect of an ambush is therefore modest (DA7, carried camouflage). Lane D's sandbox proposal (§5.1) leaves the same
  question to the Director.
- **Q-AP3 — The Standoff extension (§6.4).** Adopt it (a Standoff goes to the defender or the ambusher) or keep it off. It depends on
  P-TR1 (whether the Standoff end exists at all).
- **Q-AP4 — Where DA6 and DA7 apply (READING AP-2, §6.3; added in round 1, AP-R1-11).** DA6-R's applied reading names the
  AutoResolver; lane AP also applies the rule, through the same function, to the TacticalResolver's BothEliminated and BothDisengaged
  ends. (a) both resolvers (applied in the prototype pending the ruling); (b) the AutoResolver only. MEASURED: no acceptance verdict
  changes either way (H-AP §4, bases B2 and B3); under (b) the two resolvers' "neither" classes count different things.
  Recommendation: (a), so that the rule cannot open a parity gap by construction (D-079 §4 D11's reasoning for terrain, INFERRED to
  carry over to an end-of-battle rule).

Readings applied without a question (each correctable): AP-3 (the ambush judged at the engagement), AP-4 (no band), AP-5 (a) (an ambush
is an attack from concealment: DA7-R's own wording, §5.2); "stationary" = the state belongs to its position; works lost on leaving (the
brief's wording). [Round 1: AP-2 was listed here in round 0; it is now Q-AP4.] For lane MOR (not a Director question here): the
mutual-elimination win of §6.4's observation. [Round 2: §3.6 (what an order may reveal) and §3.7 (who is an enemy) are
lane AP's design under DA8's delegation (D-082 §5: the detection rules are not decided there), PROPOSALS with their alternatives, not
questions.]

## 9. Not decided here; limitations

- Every magnitude: camouflage factor 1/2, works rate (one day), thresholds (1/2, complete), the two new classes' coefficients, the
  action costs (profile data). PLACEHOLDER.
- The T3 line is one-dimensional and uniform in movement cost; detection uses line distance; the line's terrain is the stand-in for the
  campaign map's cover (the campaign model has no cover terrain today: no "forest", "jungle" or "biome" anywhere in `Sim.Core`'s
  C# sources, MEASURED by a case-insensitive search).
- Concealment has no upkeep and no decay; works do not decay and are not visible to the enemy (digging could raise detectability:
  PROPOSAL).
- Spotting is judged at the end of a move or retreat (T3's moves are instantaneous): an enemy that marches through or past a concealed
  agent's cell does not find it (§3.2; round 1, AP-R1-04).
- A hide on a Wall or Building line cell gets the battle table's multiplier 0 and is then detected only from its own cell (§2.1; round 1,
  AP-R1-10).
- [Round 2, AP-R2-01] What an order still reveals (§3.6): the paid channel (a concealment spotted at once tells its polity that an unseen
  enemy detects it, at the price of a slot and its Action Capacity), the number of agents (T3's `OutOfReach` for an id beyond them), and
  whether an enemy seen again closed in while unseen (T3's hit-and-run lifting).
- [Round 2, AP-R2-04] The enemy relation is static (any war of the world, its span not consulted) and stands in for M8's diplomacy
  (§3.7); T3's Attack and Retreat still treat every other polity as hostile.
- The hand-off is a stand-in (M7.15): it maps one engagement to one battle and assumes the caller deploys the forces.
- Not implemented: Reconnoitre, Lie in wait (§3.5); the sandbox edits (§5.6); starting knowledge for ambush battles (§5.3); WarFog
  observers as agent observers (§2.2); the knowledge-seam coefficient (§2.3, Q5).
