# D-090: The two test battles follow the thirds; battalion sizes are DR24's; ADR-059 amended; the export made faster now; a queue line for older UI overlaps (Director, 2026-11-12, fourth message)

**Status: RULED (Director, 2026-11-12).** Administrative record of the Director's fourth message of 2026-11-12 (cited **DI**).
It answers the five questions still open after D-089 (§0). With it, every item of the orchestrator's lists of that day is
answered. Categories as in D-076..D-089: DECIDED, ADMIN, DELEGATED, READING, OPEN.

**Source (the whole message, verbatim, typing unchanged).** Repository copy:
[`m7-plan/pass-state/notes/director-messages/director-answers-2026-11-12d.md`](m7-plan/pass-state/notes/director-messages/director-answers-2026-11-12d.md).

> 1) B
> 2) mine were just examples. If these are pre decided, prefer all of DR 24. I might've forgotten
> 3) yes
> 4) now
> 5) yes

## 0. The questions answered (as the orchestrator put them)

1. Item 17 (D-089 §3): the shipped test battles "Ambush" (lane P) and "Amphibious" (lane N) start troops in the middle third
   of the battlefield, which DR31 forbids: (a) keep them as labelled special tests; (b) rewrite them so every side starts in
   its own third; (c) both.
2. Battalion sizes against DR24 (D-088 §5): DR24 sets archers or light infantry 60 and fixed-wing aircraft 1; D-087 DF12's
   examples were 50 archers and 2 jets. (a) The examples replace those two; (b) DR24 stands.
3. Item 23: amend ADR-059 with lane RR's changes (lane RR's P6).
4. Item 24: lane RR's export speed and memory work (P2, P3): later or now.
5. Item 25: add lane U's proposed queue line on older UI overlaps to `docs/queue.md`.

## 1. Decisions

| ID | Category | Decision | Director's words (DI) |
|---|---|---|---|
| DI1 | DECIDED (closes D-089 DH3) | Both shipped test battles are rewritten so every side starts in its own third of the battlefield (DR31; D-088 DG10). The ambush and the beach landing come from the terrain and the forces, not from an illegal start. | "1) B" |
| DI2 | DECIDED (closes D-088 §5's size question) | D-087 DF12's battalion sizes were examples. Battalion sizes follow DR24 wherever DR24 sets one: heavy or aggregated infantry 50, archers or light infantry 60, tanks 5, helicopters 3, ships 5, fixed-wing aircraft 1, snipers 2, mortars 5. | "2) mine were just examples. If these are pre decided, prefer all of DR 24. I might've forgotten" |
| DI3 | DECIDED; applied by the orchestrator | ADR-059's amendment log records lane RR's changes (D-086) and CR-027. | "3) yes" |
| DI4 | DECIDED (lane RR's P2 and P3) | The export's memory and speed work is built in this pass: the telemetry read as a stream from the file, and the lines encoded and checked in parallel, in order, giving the same bytes. Lane RR's P4 stays unneeded unless these fall short. | "4) now" |
| DI5 | DECIDED; applied by the orchestrator | Lane U's proposed queue line (three collisions among existing map controls, H-U §11.3) is added to `docs/queue.md`. | "5) yes" |

## 2. Readings and delegated design

- **DI2-R1 (types DR24 does not size).** DELEGATED to lane BN: a battalion size for each unit type DR24 does not size (lane CB's
  CAP-5 families: cavalry, siege, air defence, missiles, reconnaissance), as a PROPOSAL for the Director, in DR24's spirit.
- **DI4-R1 (what may not change).** The workbook's bytes and its self-check: lane RR's P2 and P3 promised the same output, and
  DI4 adopts them as stated. Lane RX measures before and after on the Director's 850-turn files and a 1,630-turn session.

## 3. Superseded or closed

- **D-087 DF12's numbers** (50 archers, 5 tanks, 2 jets) are examples, not values; DR24's values stand (DI2).
- **D-088 §5's battalion-size question** is closed (DI2). D-088 DG3's limit (at most 5 battalions per side, in total, D-089
  DH9) counts these battalions.
- **D-089 DH3** (item 17) is closed (DI1).

## 4. What the orchestrator does now

- **DI3 and DI5 are applied in the commit that records this decision.** ADR-059 gains the amendment "2026-11-12 (D-086, D-088
  DG2, D-090 DI3)", and `docs/queue.md` gains lane U's line under a dated note.
- **DI1:** lane OC (battle outcomes) rewrites the two test battles. The pins they hold move with attribution, and lane AR's
  parity battery runs on the rewritten "Ambush".
- **DI2:** lane BN's brief takes DR24's values, and proposes sizes only for the types DR24 does not size.
- **DI4:** a new lane, RX (the export's memory and speed), after lane RT2.
- **Queue,** one agent at a time after the G2 final: OC, BN, AR, RT2, RX; then the final-round verification and the pass's
  report.

## 5. Not decided here

Nothing of the orchestrator's lists of 2026-11-12 is open. Three readings of D-088 stay applied until the Director corrects
them:
- DG5-R3: "home territory" is land the empire controls;
- DG13-R1: walls count in both resolvers;
- DG13-R2: walls multiply the defenders' attack and toughness.

The Longbow screenshot remains welcome if the Director plays that build again.
