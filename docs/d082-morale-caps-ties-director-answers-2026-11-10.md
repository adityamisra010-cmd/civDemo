# D-082: Victory morale, army scale at every stage, caps, ties and concealment actions (Director, 2026-11-10)

**Status: RULED (Director, 2026-11-10), with READINGS flagged.** Administrative record of the Director's message of
2026-11-10 (cited **DA**). It answers questions raised by Phase D lane D (`prototypes/m7-sprint1/reports/H-D.md` §4,
commit d131774) as the orchestrator put them to the Director, and it gives new direction. Categories as in D-076..D-081:
DECIDED, ADMIN, DELEGATED, READING, OPEN.

**Source (the whole message, verbatim, typing unchanged).** Working copy:
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gov/director-answers-2026-11-10.md`.

> Winning a battle should raise morale too. Check out how total war troy handles arny morale ans individual troops morale.
> To answer your questions-
> Same at every stage, caps in sandbox apply. Battlefield units. What do you mean auto resolver can't resolve? If two sides
> are equal, the defending side wins. Unless it's an ambush like from a hidden unit, then the winning side wins. Units can
> spend action points to hide/entrench/camouflage etc.

**The questions answered**, as the orchestrator put them: (1) at which stage DR25's numbers apply: the same at every stage,
at one reference stage, or as a share of the population (lane D Q-S1, `docs/m7-plan/economic-scale-proposal.md` §6);
(2) whether DR24's caps still apply in the War Sandbox (READING S1, `docs/m7-plan/caps-and-counted-objects-proposal.md`
§5); (3) whether a soldier cap counts people or battlefield units (READING C1, the same document §2.2); (4) when the
AI-vs-AI resolver cannot handle a battle, whether the sandbox runs the full battle model or refuses (READING A,
`docs/m7-plan/war-sandbox-proposal.md` §6).

## 1. Decisions

| ID | Category | Decision | Director's words (DA) |
|---|---|---|---|
| DA1 | DECIDED | Winning a battle raises the winning side's morale. With DR29 ("Morale decreases when troops participate in combat"), a battle moves morale both ways: down through combat, up for the side that wins. | "Winning a battle should raise morale too." |
| DA2 | DELEGATED (research input set) | Morale is designed at two levels: the army's morale on the campaign map and each unit's morale in battle. The Director names the morale of Total War Saga: Troy as the research reference for both. Under D-069 it is a reference only: the design, numbers, names and text are our own, and nothing player-facing names another game. | "Check out how total war troy handles arny morale ans individual troops morale." |
| DA3 | DECIDED (answers Q-S1) | DR25's numbers ("500–1,000 troops readily achievable", "50,000 troops difficult") hold the same at every stage of the game: lane D's option (a), absolute at every stage. | "Same at every stage" |
| DA4 | DECIDED (answers READING S1) | DR24's caps apply in the War Sandbox: lane D's READING S1 (a), already applied in the prototype. | "caps in sandbox apply" |
| DA5 | DECIDED (answers READING C1) | A soldier cap counts battlefield units, not people: lane D's READING C1 (b), the battle-entity rule that lane W applies. | "Battlefield units." |
| DA6 | DECIDED (new rule) | When the two sides of a battle are equal, the defending side wins. | "If two sides are equal, the defending side wins." |
| DA7 | DECIDED (exception to DA6) | The exception is an ambush, for example one launched by a hidden unit; who then wins is DA7-R. | "Unless it's an ambush like from a hidden unit, then the winning side wins." |
| DA8 | DECIDED (direction); mechanics DELEGATED | Units can spend action points on concealment and preparation: hiding, entrenching, camouflage and similar actions. | "Units can spend action points to hide/entrench/camouflage etc." |
| DA9 | ADMIN | The Director asked what "the AutoResolver cannot resolve" means. Answered in §4; READING A stays applied and OPEN. | "What do you mean auto resolver can't resolve?" |

## 2. Readings (each applied until the Director corrects it; both alternatives stated)

- **DA1-R (what a win is, who gains).** (a) APPLIED: a win is the battle outcome the resolvers already report (the other
  side routed, retreated, surrendered or eliminated; DA6 and DA7 for equal sides), and the rise applies to the winning
  side's formations that took part and to their army's campaign morale. (b) The rise applies to the whole winning polity's
  armies. The size of the rise and how it combines with DR29's decrease are delegated (DA2).
- **DA5-R (what a "battlefield unit" is).** (a) APPLIED: a battle entity in lane W's rule. A campaign formation of P
  persons fights as n entities, n ≤ the cap (`BattleCollapse`; `reports/H-W.md` §5). The cap bounds how many entities one
  formation fights as; a formation's size in persons is limited by the economy, not by the cap. (b) The cap counts the
  formations of that family on one battlefield. (a) is applied because the question was put for one formation's cap
  (C1), and DR24's caps are per formation.
- **DA6-R (what "equal" is, and where the rule applies).** "Equal" is read as the resolver's result separating neither
  side under its own deterministic arithmetic. The exact test (exact equality of the deciding quantity, or a stated band)
  is delegated design and gets a tie-dense test. The rule is applied to the AutoResolver. Extending it to the
  TacticalResolver's UNRATIFIED Standoff end (lane TR, P-TR1: a whole-field idle battle ends with nobody routed) is a
  PROPOSAL, not decided here: the defender holds the field and wins, unless the attacker ambushed from concealment.
- **DA7-R (who wins an ambush between equal sides).** (a) APPLIED: "the winning side" is read as the ambushing side, so
  equal sides in an ambush launched from concealment give the battle to the ambusher. (b) In an ambush the defender's
  tie advantage does not apply and the battle's own result decides; an exact tie then still needs a rule.
- **DA8-R (which action points).** The action points are T3's Action Capacity (DR22, C23, two-action windows). "etc."
  delegates the list beyond hiding, entrenching and camouflage. Law 2 applies: each action works through a mechanism
  (detection, cover and works in the battle grid, time to build), never through a free-floating buff.

## 3. Superseded or narrowed

- Lane D's after-battle morale rule "A battle never raises the campaign morale" (`docs/m7-plan/support-capabilities-proposal.md`
  §3.3; `SupportMath.MoraleAfterBattle`; H-D §14, D-R2-01): superseded for the winning side by DA1. READING M is narrowed:
  DR29's decrease stands, and whether taking part alone costs morale (M (b)) stays OPEN.
- Lane D Q-S1: answered (a) (DA3). The A-38 calibration targets absolute numbers at every stage. Lane D's stated consequence
  of (a): "a per-soldier cost growing by orders of magnitude with the era"; on HEAD the driver polity numbers 80.1M–90.2M at
  turn 1630 (lane D, MEASURED).
- D-079 §4 D20 and D-076 Q-L1 ("the mechanism that makes 50,000 troops difficult"): narrowed by DA3; the mechanism must
  hold at every stage.
- Lane D READING S1: confirmed (DA4).
- Lane D READING C1 (a), persons, as applied in the caps prototype (`DirectorCaps.CountOf`, `FormationRoster`) and in the
  sandbox's Quick Battle split: superseded by (b) (DA5). Lane D's own consequences of (b) (caps proposal §2.2) follow:
  CAP-1 and CAP-2 dissolve; formation size is limited by the economy; and what one entity stands for (its persons, hit
  points, morale weight and fire) is undesigned and becomes required design for M7.6.

## 4. The AutoResolver question (DA9), answered

The AutoResolver (lane P's model 2) has no model for three kinds of formation: formations outside the land domain (ships,
aircraft), formations with crew-served or domain weapons (guns), and formations embarked on a transport
(`prototypes/m7-sprint1/Proto.M7/Tactical/AutoResolver.cs` lines 300–304; `ResolverPolicy.AutoUnsupportedReason`). For an
AI-vs-AI battle containing any of them, `ResolverPolicy.Default` routes the battle to the TacticalResolver, the full battle
model run without rendering, and that model always produces a result. "Cannot resolve" therefore never leaves a battle
undecided, and it is not about equal sides (that is DA6). READING A asks whether the War Sandbox should do the same
(applied) or refuse such a battle; it stays OPEN until the Director confirms.

## 5. Not decided here

- The morale model and every number: the size of the victory rise, decay and recovery, unit against army morale, and how
  they feed each other. Delegated (DA2); a PROPOSAL in its lane.
- The exact definition of "equal" (DA6-R); the concealment actions beyond DA8, with their costs, durations and detection
  rules; the Standoff extension (DA6-R).
- Still OPEN: READING A; READING M (b); the two thirds-violating sandbox scenarios (lane D, sandbox proposal §5.1); R6;
  lane U's U-Q11, decision-tray placement and queue line; the calibration numbers for DA3 (A-38, M7.14).
- This record authorizes no change to `Sim.*` or `Sim.Data` (D-079 §6 and Q5 stand). Implementation stays in
  `prototypes/` and `docs/`.
