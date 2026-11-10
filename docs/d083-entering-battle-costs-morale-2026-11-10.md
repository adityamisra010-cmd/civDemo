# D-083: Entering a battle costs morale (Director, 2026-11-10)

**Status: RULED (Director, 2026-11-10), with READINGS flagged.** Administrative record of the Director's second message of
2026-11-10 (cited **DB**). It answers READING M (b), which D-082 left OPEN (§3, §5) and lane MOR presented without choosing
(`docs/m7-plan/morale-proposal.md` §8; `prototypes/m7-sprint1/reports/H-MOR.md` §9; commit 7f0cb92). Categories as in
D-076..D-082: DECIDED, ADMIN, DELEGATED, READING, OPEN.

**The question answered**, as the orchestrator put it: "should simply taking part in a battle cost morale, even with no
casualties? Right now it doesn't, so a battle won without losses only raises morale."

**Source (the whole message, verbatim, typing unchanged).** Working copy:
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gov/director-answers-2026-11-10b.md`.

> Simply entering battke should cost morale. I thought you researched total war troy!

## 1. Decisions

| ID | Category | Decision | Director's words (DB) |
|---|---|---|---|
| DB1 | DECIDED (answers READING M (b)) | Entering a battle costs morale, with or without casualties: READING M (b). With DR29 ("Morale decreases when troops participate in combat") and D-082 DA1 (winning raises the winner's morale), a battle changes morale through three terms: a cost for entering it, the decrease through combat, and the rise for the side that wins. | "Simply entering battke should cost morale." |
| DB2 | ADMIN | The Director expected the reference research (D-082 DA2) to have answered this. It had not. Lane MOR found no evidence either way on whether the reference game charges morale for entering a battle. It read none of the game's files and reached few of the sources it tried (H-MOR §1). A deeper research pass follows. Until then the reference neither supports nor contradicts DB1, which is our own design (D-069). | "I thought you researched total war troy!" |

## 2. Readings (each applied until the Director corrects it; both alternatives stated)

- **DB1-R1 (what entering is).** (a) APPLIED: a formation enters a battle when it is placed on the battlefield. That happens
  at the battle's start, in its deployment third (DR31), or later, at the instant it arrives as a reinforcement. A formation
  that never reaches the field, such as a reserve that is not committed, does not enter and pays nothing. (b) Every
  formation of every army the battle involves pays at the battle's start, whether or not it reaches the field.
- **DB1-R2 (how often).** (a) APPLIED: once per formation per battle, at its entry instant. The act of entering is what
  costs, not the time spent in the battle. (b) A cost that grows with the time spent in battle (per engaged pulse), a drain
  like fatigue.
- **DB1-R3 (DB1 with DA1).** (a) APPLIED: the entry cost and DA1's rise are separate terms, and both apply. A battle won
  without loss changes the winner's morale by the rise less the cost, which can be negative after beating a small force,
  because DA1's rise ⌊400,000 × B ÷ (B + W)⌋ (PROPOSAL) is then small. (b) A win always nets a rise: the cost a winner pays
  is limited to its rise.

## 3. Superseded or narrowed

- READING M is closed: (b) applies. This answers D-082 §3 ("whether taking part alone costs morale (M (b)) stays OPEN") and the
  "READING M (b)" item of D-082 §5.
- Lane MOR's applied (a) is superseded (morale proposal §8; H-MOR §3 and §9: "a battle won without loss raises the winner by
  DA1's rise alone and a battle lost without loss changes nothing"). Under DB1, a battle won without loss changes the winner's
  morale by DA1's rise less the entry cost, and a battle lost without loss costs the entry cost.
- DR29 stands: the decrease through combat is kept alongside the entry cost.
- The prototype at 7f0cb92 has no entry cost: not in `Proto.M7/Morale/CampaignMorale.cs`, not in
  `Proto.M7/Support/SupportWorld.cs` (`OnBattle`), and not in either resolver. Adding it is prototype work under D-082 §5's
  delegation (§4).

## 4. Delegated (design; a PROPOSAL in its lane)

- **The size of the cost.** D-082 §5 delegates every number of the morale model.
- **Where the cost is charged.** There are three options:
  - in the battle at the entry instant, which also weakens that battle;
  - only in the after-battle settlement;
  - both.

  The morale proposal §8 names the places: "a new term in both resolvers (or in the hand-off)". If the cost is taken only
  from the battle's start morale, the in-battle recovery can make it up before the battle ends. Lane D's net-loss settlement
  (start − end − restore, floored at 0) would then carry none of it to the campaign. The design must say how the cost reaches
  the army's campaign morale exactly.

## 5. Not decided here

- Still OPEN: READING A; the two thirds-violating sandbox scenarios (lane D, sandbox proposal §5.1); R6; lane U's U-Q11,
  decision-tray placement and queue line; the calibration numbers for DA3 (A-38, M7.14). D-082's DA1-R, DA5-R, DA6-R and
  DA7-R stay applied until the Director corrects them.
- This record authorizes no change to `Sim.*` or `Sim.Data` (D-079 §6 and Q5 stand). Implementation stays in `prototypes/`
  and `docs/`.
