# D-085: Without fighting, a battle changes no morale (Director, 2026-11-11)

**Status: RULED (Director, 2026-11-11), with READINGS flagged.** Administrative record of the Director's message of 2026-11-11
(cited **DD**). It answers the question the orchestrator put after lane MOR2: what DA1's rise does for an equal-sides "win" in
which no blow was struck (`docs/m7-plan/morale-proposal.md` §11.8, alternative (c'); `prototypes/m7-sprint1/reports/H-MOR2.md`
§14; commit 0d2db74). It bounds D-082 DA1 and D-083 DB1. Categories as in D-076..D-084: DECIDED, ADMIN, DELEGATED, READING, OPEN.

**The question answered**, as the orchestrator put it: "suppose both sides' units all run at the same instant without fighting.
The tie rule gives the win to the defender, so the defender's army gains morale: +15% net against an equal force, from any
starting morale." The package kept that rise; the alternative put to the Director was "no rise when the winner never struck a
blow", and the orchestrator recommended it.

**Source (the whole message, verbatim, typing unchanged).** Working copy:
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gov/director-answers-2026-11-11.md`.

> No fighting doesn't affect morale at all then. There has to be done fighting to alter morale

## 1. Decisions

| ID | Category | Decision | Director's words (DD) |
|---|---|---|---|
| DD1 | DECIDED | A battle without fighting does not affect morale at all. There is no rise for its reported winner, so an equal-sides "win" with no blow struck raises nothing. There is no cost either: no entry cost (D-083) and no decrease (DR29). Morale changes only where there was some fighting. | "No fighting doesn't affect morale at all then. There has to be done fighting to alter morale" |

## 2. Readings (each applied until the Director corrects it; both alternatives stated)

- **DD1-R1 (fighting in what: the battle or the formation).**
  - (a) APPLIED: the battle.
    - If any fighting took place in a battle, DA1's rise, DR29's decrease and D-083's entry cost apply as before. That includes a
      formation that entered the field but itself struck no blow (D-083 DB1: "Simply entering battke should cost morale").
    - If no fighting took place at all, no army's morale changes through that battle.
    - This reading keeps both of the Director's statements whole.
  - (b) The formation: a formation that neither struck nor received a blow has no morale change, even where others fought. This
    would narrow D-083 DB1 to formations that fought.
- **DD1-R2 (what fighting is).**
  - (a) APPLIED: at least one attack resolved against an enemy formation, whether or not it dealt damage. An attack is a melee
    blow, a missile volley, or a shot of a crew-served or domain weapon. The battle results already count attacks per formation
    (`BattleResult` formations, `AttacksMade`).
  - (b) At least one attack that dealt damage.

## 3. Superseded or narrowed

- **D-082 DA1 is narrowed:** a win with no fighting raises nothing.
- **D-083 DB1 is narrowed:** entering a battle costs morale where the battle had fighting; an unfought battle costs nothing. DB1's
  "with or without casualties" stands, so fighting without casualties still costs the entry.
- **Lane MOR2's PROPOSAL (a)** for the mutual-elimination win (morale proposal §11.8) is superseded where no fighting took place.
  Its alternative (c') is adopted in a wider form: no rise and no cost. The measured +150,000 for the defender of an unfought
  mutual rout, and the attacker's −50,000, both become 0.
- **DR29 is unchanged.**

## 4. Delegated (design; a PROPOSAL in its lane)

- **Each resolver's "fought" report:** how each resolver reports whether the battle had fighting under DD1-R2 (a), and its
  boundary test (exactly one attack against none).
- **The in-battle part of the entry cost** (D-083 §4; 0 by default since lane MOR2). If it is switched on, it must not change
  morale in a battle that turns out to have no fighting. It could be charged at the battle's first fighting instant (and at
  entry for a later arrival), or dropped so that the settlement alone charges the cost.
- **Other battle kinds:** how the War Sandbox's Quick Battle and lane S's siege battles apply DD1.

## 5. Not decided here

- Still OPEN:
  - READING A;
  - the two thirds-violating sandbox scenarios;
  - R6;
  - lane U's U-Q11, decision-tray placement and queue line;
  - the DA3 calibration numbers (A-38, M7.14);
  - lane AP's Q-AP1..Q-AP4;
  - lane CB's items: E1 for M7.6, READING CB-1, a CAP-5 performance cap and READING CB-4.
- This record authorizes no change to `Sim.*` or `Sim.Data` (D-079 §6 and Q5 stand). Implementation stays in `prototypes/` and
  `docs/`.
