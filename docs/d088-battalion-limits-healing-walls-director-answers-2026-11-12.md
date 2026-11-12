# D-088: All three new lanes are built; the kernel's trace reader is fixed; a side holds the role; healing, ambush starts, walls and the parity bar (Director, 2026-11-12, second message)

**Status: RULED (Director, 2026-11-12), with READINGS flagged and two questions re-asked (§5).** Administrative record of the
Director's second message of 2026-11-12 (cited **DG**). It answers items 1–14 of the orchestrator's 25-item list of open
questions (§0), which followed D-087. Items 15–25 are not answered here. Categories as in D-076..D-087: DECIDED, ADMIN,
DELEGATED, READING, OPEN.

**Source (the whole message, verbatim, typing unchanged, trailing spaces removed).** Repository copy:
[`m7-plan/pass-state/notes/director-messages/director-answers-2026-11-12b.md`](m7-plan/pass-state/notes/director-messages/director-answers-2026-11-12b.md).

> 1) a
> 2) a , yes
> 3) b , but multiple batallions can engage in a battle. Maximum of 5 batallions per side, including allies. If a player attacking has 4 batallions and the allies have 2, then priority is given to the player instead of the ally . Same for defending side
> 4)a
> 5) consumes resources to heal, can only heal when garissoned or in home territory, and needs one full turn to regenerate, consuming all action points/energy
> 6) a
> 7 to 9 - a
> 10) a side is an attacker or defender. Army is a part of a side. A side can have an empire's armies, or an empire and it's allied armies.
> 11) a
> 12) battle auto starts. Ambusher gets revealed, and they start at their 1/3rd of the BATTLEFIELD, NOT THE MAP, IT'S THE BATTLEFIELD, DON'T GET CONFUSED. MAP IS THE BIG WORLD MAP THING, BATTLEFIELD IS A SMALL CHUNK WHERE THE BATTLE HAPPENS.
> 13) a, try to make it more accurate though.
> 14) b, walls should be counted as if troops are tripled, and quadrupled if archers. And 100% bonus for each wall level improved. (Like ancient walls, medieval walls etc) Until modern era, wherin walls stop adding any help since they can just be busted with an artillery shell

## 0. The questions answered (items 1–14, as the orchestrator put them)

1. How much of the new battle work to build now: (a) build and test lanes OC, BN and AR in this pass; (b) build OC, and
   proposals only for BN and AR; (c) proposals only.
2. Allow the one-line fix to the frozen kernel's trace reader, so games past 1920 CE can be continued, replayed and inspected
   (lane RR's P1; D-087 DF5): (a) yes; (b) no.
3. The old per-type limits beside the 25-battalion army cap: (a) drop them; (b) keep them, counted in battalions, "e.g. at most 5
   ship battalions within the 25". **ADMIN: the question misdescribed DR24.** It called DR24's caps limits per army. They are
   limits on the size of one formation (D-079 DR24: heavy or aggregated infantry 50, archers or light infantry 60, tanks 5,
   helicopters 3, ships 5, fixed-wing aircraft 1, snipers 2, mortars 5; `docs/m7-plan/caps-and-counted-objects-proposal.md` §2).
   The Director's answer is recorded as given, and its scope is re-asked (§5).
4. DR25's "50,000 troops difficult" against battalions: (a) many armies added together, battalions stay small; (b) bigger
   battalions.
5. Reinforcements: (a) damaged battalions refill to full size over time, at a cost; (b) no refill.
6. Inside a battalion: (a) it takes the orders, and each tank, jet or group of soldiers stays its own piece; (b) one piece.
7–9. D-087's readings DF7-R1 (damage judged in the whole battle), DF7-R2 (damage is any strength lost), DF8-R1 (routing units
   rejoin after a tie): (a) keep each.
10. Several armies in one battle: (a) each army its own role; (b) one role per side.
11. The ambush radius: (a) from the hidden army's size; (b) from the passing army's size.
12. Where an ambush battle starts: (a) where the ambushers hid, outside their own third; (b) the thirds rule still applies. The
   question wrote "map" where it meant the battlefield; the Director corrected the term (DG12).
13. The parity bar: (a) the AutoResolver picks the TacticalResolver's winner in at least 95% of test battles, with casualties
   within about 15%; (b) the Director's own numbers.
14. Walls: today, on a walled setup, the AutoResolver fights from the first step while the TacticalResolver has no fighting
   (lane MOR3's PROPOSAL): (a) the AutoResolver respects walls; (b) no.

## 1. Decisions

| ID | Category | Decision | Director's words (DG) |
|---|---|---|---|
| DG1 | DECIDED | Lanes OC, BN and AR (D-087 §5) are built and tested in this pass, not left as proposals. | "1) a" |
| DG2 | DECIDED (kernel sign-off) | The frozen kernel's trace reader, `SessionTrace.Parse`, reads the year as the number the writer writes, so a session past turn 1270 (from 1920 CE, half-year turns) can be continued, replayed and inspected. Recorded as [CR-027](adr/cr-027-session-trace-reads-the-written-year.md). Closes D-087 DF5. | "2) a , yes" |
| DG3 | DECIDED in part; scope OPEN (§5) | Several battalions may fight in one battle. A side brings at most 5 battalions to a battle, its allies' included. When a side has more, its own empire's battalions come first and its allies' fill what is left: in the Director's example, an attacking player's 4 battalions and its allies' 2 make 6, so the player's 4 fight and one of the allies'. The same holds for the defending side. | "3) b , but multiple batallions can engage in a battle. Maximum of 5 batallions per side, including allies. If a player attacking has 4 batallions and the allies have 2, then priority is given to the player instead of the ally . Same for defending side" |
| DG4 | DECIDED | DR25's "50,000 troops difficult" is an empire's total across its armies; battalions stay small. | "4)a" |
| DG5 | DECIDED | A damaged battalion heals (refills) by consuming resources. It can heal only while garrisoned or in its home territory. Healing takes one full turn and uses all of that turn's action points (energy). Answers lane CB's READING CB-4. | "5) consumes resources to heal, can only heal when garissoned or in home territory, and needs one full turn to regenerate, consuming all action points/energy" |
| DG6 | DECIDED (confirms D-087 DF12-R1 (a)) | The battalion takes orders; inside it, each tank, jet or group of soldiers stays its own battle entity. | "6) a" |
| DG7 | DECIDED (confirms DF7-R1 (a), DF7-R2 (a), DF8-R1 (a)) | Damage is judged in the whole battle; damage is any strength lost to an enemy attack; routing units rejoin after a tie. | "7 to 9 - a" |
| DG8 | DECIDED (replaces D-087 DF8-R2 (a) by (b)) | A side is the attacker or the defender, and an army is part of a side. A side holds one empire's armies, or an empire's and its allies' armies. Every army on a side shares the side's role. | "10) a side is an attacker or defender. Army is a part of a side. A side can have an empire's armies, or an empire and it's allied armies." |
| DG9 | DECIDED (confirms DF9-R1 (a)) | The hidden army's size sets the ambush radius. | "11) a" |
| DG10 | DECIDED (replaces DF9-R2 (a) by (b); answers lane AP's Q-AP2) | An ambush battle starts by itself; the ambusher is revealed; the ambusher deploys in its own third of the battlefield (DR31 holds). | "12) battle auto starts. Ambusher gets revealed, and they start at their 1/3rd of the BATTLEFIELD …" |
| DG11 | ADMIN (terms) | The battlefield is the small area where a battle is fought; the map is the world map. Records and questions use the two terms as the Director does. | "… NOT THE MAP, IT'S THE BATTLEFIELD, DON'T GET CONFUSED. MAP IS THE BIG WORLD MAP THING, BATTLEFIELD IS A SMALL CHUNK WHERE THE BATTLE HAPPENS." |
| DG12 | DECIDED (answers D-079 Q4, lane P's acceptance) | The parity bar: the AutoResolver picks the TacticalResolver's winner in at least 95% of the test battles, with casualties within about 15%. The Director asks for better accuracy than that where it can be had. | "13) a, try to make it more accurate though." |
| DG13 | DECIDED (supersedes lane MOR3's walls PROPOSAL) | Walls count as a multiplier of the defenders: as if their troops were tripled, quadrupled for archers. Each improved wall level adds a 100% bonus (ancient walls, medieval walls and so on). In the modern era walls add nothing, because artillery breaches them. | "14) b, walls should be counted as if troops are tripled, and quadrupled if archers. And 100% bonus for each wall level improved. (Like ancient walls, medieval walls etc) Until modern era, wherin walls stop adding any help since they can just be busted with an artillery shell" |

## 2. Readings (each applied until the Director corrects it; both alternatives stated)

- **DG3-R1 (what the limit of 5 counts).** (a) APPLIED: battalions of every kind, 5 per side in total, as the words read.
  (b) Battalions of each unit type, 5 per type per side (the question's example was ships). Flagged and re-asked (§5): the
  question put to the Director misdescribed DR24.
- **DG3-R2 (which battalions of the side's own empire fight first, when it alone has more than 5).** DELEGATED to lane BN's
  PROPOSAL (for example the player's choice, else the strongest).
- **DG5-R1 (how much one turn heals).** (a) APPLIED: one full turn restores the battalion to full strength when its cost is paid
  in full; a shortfall restores it in proportion to what was paid. (b) One turn restores a fixed part.
- **DG5-R2 (what healing consumes).** DELEGATED to lane BN: recruits (people moved through the ledger, Law 1) and the equipment
  or money the battalion's type needs.
- **DG5-R3 ("home territory").** (a) APPLIED: territory the battalion's empire controls. (b) Only the region it was raised in.
- **DG13-R1 (where the multiplier applies).** (a) APPLIED: in both resolvers, to the defenders while they fight from the walls
  (DG12's parity needs the two to agree); the TacticalResolver keeps the walls' geometry for movement. (b) The AutoResolver only.
- **DG13-R2 (what "tripled" multiplies).** (a) APPLIED: the defenders' fighting strength, attack and toughness, as if their
  numbers were tripled (archers quadrupled). (b) Their toughness only.
- **DG13-R3 (the 100% per wall level).** (a) APPLIED: additive: the first level ×3 (archers ×4), each further level +1 (×4 and
  ×5 at the second level). (b) Doubling per level (×3, ×6, ×12; archers ×4, ×8, ×16). Flagged and re-asked (§5).
- **DG13-R4 ("until modern era").** (a) APPLIED: walls add nothing against an attacker that fields artillery able to breach
  them, a capability computed from the attacker's battalions and research. CLAUDE.md Law 4 bans calendar gates ("capability
  derives from computed state, never from dates or era labels"), and the Director's own reason is the artillery shell. (b) From
  a date or an era label, which Law 4 does not allow without the Director's explicit ruling.

## 3. Superseded or narrowed

- **D-087 DF5** is closed by DG2 (CR-027).
- **D-087 DF8-R2 (a)** (each army its own role) is replaced by (b) (one role per side), DG8. DF8's rule stands: the attacker is
  the side that initiates combat; declaring war, a zone of control or a siege without combat does not make a side the attacker.
- **D-087 DF9-R2 (a)** (an ambush battle exempt from the thirds) is replaced by (b), DG10. Lane AP's Q-AP2 is answered.
- **D-079 Q4** (the parity acceptance criteria) is answered by DG12.
- **Lane CB's READING CB-4** (reinforcement) is answered by DG5.
- **Lane MOR3's walls PROPOSAL** (walls in the AutoResolver's contact and line of fire) is superseded by DG13's multiplier.
- **D-087 §6's question** on DR24's caps beside the 25-battalion cap is re-put in its correct form (§5).

## 4. What the orchestrator does now

- **DG2:** CR-027 is written with the Director's sign-off. The orchestrator makes the reader change with its tests and runs
  Tier 3 (the kernel changed), then commits and pushes. Lane RR's test that pinned the old reader's refusal moves, with its
  attribution. The change joins the pass's final-round verification queue.
- **DG1, DG3–DG13:** the lane briefs of OC, BN and AR (D-087 §5) carry these rulings and readings. They run one agent at a time
  after the G2 final, in the order OC, BN, AR.
  - OC: the tie; fighting as damage; the Standoff tie; one role per side; the ambush trigger, which starts its battle by itself
    and reveals the ambusher in its own third.
  - BN: battalion sizes; the 25-battalion army; at most 5 battalions per side in a battle, the side's own empire first; healing.
  - AR: every domain auto-resolved; the parity bar of DG12, measured and beaten where it can be; the walls of DG13 in both
    resolvers.

## 5. Not decided here (OPEN, re-asked)

- **The scope of DG3's limit:** 5 battalions per side in total (DG3-R1 (a), applied), or 5 per unit type per side.
- **Battalion sizes against DR24.** DR24 sets formation sizes of heavy or aggregated infantry 50, archers or light infantry 60,
  tanks 5, helicopters 3, ships 5, fixed-wing aircraft 1, snipers 2 and mortars 5. D-087 DF12's examples are 50 archers, 5 tanks
  and 2 jets. Do the examples replace DR24's values for archers (60 to 50) and fixed-wing aircraft (1 to 2), with DR24 for the
  rest, or do DR24's values stand?
- **DG13-R3:** additive (applied) or doubling per wall level.
- **Items 15–25 of the list:** not answered.
