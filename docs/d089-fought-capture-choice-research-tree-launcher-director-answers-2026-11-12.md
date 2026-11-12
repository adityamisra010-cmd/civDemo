# D-089: "Fought" required, the player's capture choice, the research screen and an accurate tree, the rebuild launcher; D-088's readings confirmed (Director, 2026-11-12, third message)

**Status: RULED (Director, 2026-11-12), with READINGS flagged.** Administrative record of the Director's third message of
2026-11-12 (cited **DH**). It answers items 15–22 of the orchestrator's 25-item list (§0) and confirms the readings the
orchestrator had applied after D-088. Item 17 is explained again; items 23–25 are not answered. Categories as in
D-076..D-088: DECIDED, ADMIN, DELEGATED, READING, OPEN.

**Source (the whole message, verbatim, typing unchanged, trailing spaces removed).** Repository copy:
[`m7-plan/pass-state/notes/director-messages/director-answers-2026-11-12c.md`](m7-plan/pass-state/notes/director-messages/director-answers-2026-11-12c.md).

> 15) a
> 16)a
> 17) explain in more detail? I didn't get it.
> 18) fix it
> 19)a
> 20) b
> 21)  fix things, change the tree shape if needed. Make it accurate.
> 22) a
>
> You understand everything well.
> Lemme know what's left

## 0. The questions answered (items 15–22, as the orchestrator put them)

15. "Did they fight?" defaults to "yes" when missing (`SupportBattle.Fought`, lane MOR3's second PROPOSAL): make it a required
    input? (a) yes; (b) no.
16. Lane U's U-Q11: a city taken by the player's own immediate assault never offers "Occupy, sack or raze?" (the AI's capture
    does): (a) always ask the player at the moment of capture; (b) apply a default.
17. The two shipped test battles that break the "own third" rule: (a) keep them as labelled special tests; (b) rewrite them.
18. Lane RT's N1: the research inspector's "Enabled by" and "Why locked" labels (`InfoQuery`) sometimes say the wrong thing;
    the fix touches `Sim.Core/State` outside D-084: (a) yes; (b) no.
19. Lane RT's three readings (no estimate for a civic not yet open; hovered incoming edges drawn as pinned ones; the "either"
    heading): (a) keep; (b) change.
20. Lane RT's polish PROPOSALS N2 (the hover path's dependent edges), N3 (spine lane spacing), N4 (subheadings under REQUIRES),
    N6 (a route-aware estimate for a node not yet open): (a) later; (b) now; (c) never.
21. The research date review's content PROPOSALS H1, H2, B1, K1 and the wording items W1, W2
    (`docs/m6-plan/research-date-review-2026-11.md` §5); the orchestrator's pick was text-only fixes that keep the tree's shape.
22. Lane RR's P5: a double-click "Rebuild the run workbook" file in the game zip (a build-pipeline change): (a) yes; (b) no.

## 1. Decisions

| ID | Category | Decision | Director's words (DH) |
|---|---|---|---|
| DH1 | DECIDED (adopts lane MOR3's second PROPOSAL) | Whether a battle was fought is a required input of the battle hand-off: a missing value is refused, never read as "yes". | "15) a" |
| DH2 | DECIDED (answers U-Q11) | When the player's own immediate assault takes a city, the player is asked "Occupy, sack or raze?" at the moment of capture. | "16)a" |
| DH3 | OPEN (explained again) | Item 17 is explained in §3 and asked again. | "17) explain in more detail? I didn't get it." |
| DH4 | DECIDED (answers lane RT's N1) | The research inspector's "Enabled by" and "Why locked" labels are corrected. This allows the `Sim.Core/State` change (`InfoQuery`) that the fix needs. | "18) fix it" |
| DH5 | DECIDED | Lane RT's three readings stay. | "19)a" |
| DH6 | DECIDED | Lane RT's polish PROPOSALS N2, N3, N4 and N6 are built now. | "20) b" |
| DH7 | DECIDED (direction); choices DELEGATED with sources | The research tree is made historically accurate: the date review's proposals H1, H2, B1 and K1, and the wording items W1 and W2, are fixed, changing prerequisites and the tree's shape where accuracy needs it. This allows moving a date D-047 set, and the test pins that hold such dates, where the cited evidence requires. | "21)  fix things, change the tree shape if needed. Make it accurate." |
| DH8 | DECIDED (answers lane RR's P5) | The game zip gains a third launcher, "Rebuild the run workbook", which runs `start /wait "" "app\Sim.Ui.exe" --export-session runs`. This allows the build-pipeline change it needs. | "22) a" |
| DH9 | DECIDED (confirmation) | The readings the orchestrator stated as applied in its message after D-088 stand: D-088 DG3-R1 (a), at most 5 battalions in total per side; DG13-R3 (a), each wall level adds one more multiple (×3, ×4, ×5; archers ×4, ×5, ×6); DG13-R4 (a), walls add nothing against an attacker with artillery able to breach them; DG5-R1 (a), one full turn heals a battalion fully when its cost is paid, in proportion when part is paid. | "You understand everything well." |

## 2. Readings (each applied until the Director corrects it; both alternatives stated)

- **DH9-R (what "You understand everything well" confirms).** (a) APPLIED: the four readings the orchestrator's message named as
  applied, listed in DH9. (b) A general acknowledgement that confirms no reading. It does not settle the question that message
  re-asked without a default (battalion sizes against DR24, §4).
- **DH7-R1 (what "fix things" covers).** (a) APPLIED: the date review's §5 proposals, each resolved to the option the cited
  evidence supports. Prerequisites and depths change where needed; the layout, the generated research reports and the
  calibration report are regenerated; moved goldens carry their attribution. (b) A new audit of every date in the tree.
- **DH2-R1 (the AI).** Unchanged: an AI taker disposes of the city by its doctrine at the moment of capture (lane U's MEASURED
  behaviour).

## 3. Item 17, explained again (the orchestrator's text)

The War Sandbox ships ready-made test battles. Two of them start some troops in the middle third of the battlefield, which
DR31 forbids ("Each army deploys within its own one-third"; "The middle one-third is undeployable before battle"):

- **"Ambush"** (lane P's parity battery): the ambushers start in the middle third.
- **"Amphibious"** (lane N): troops landing from the sea start on the shore, in the middle third.

They run only because the test setup switches the deployment rule off (lane TR's finding P-TR13). The Director's ruling D-088
DG10 ("they start at their 1/3rd of the BATTLEFIELD") now says an ambusher starts in its own third, so the shipped "Ambush"
test contradicts the game's rule. The choices:

- (a) Keep both as labelled special tests that only the Battle Test screen can run, never ordinary play.
- (b) Rewrite both so every side starts in its own third; the ambush and the beach landing come from the terrain and the forces
  instead of an illegal start.
- (c) Both: keep the originals as labelled tests and add rule-following versions beside them.

The orchestrator's pick: (b), since D-088 DG10 made the ambush rule explicit.

## 4. What the orchestrator does now, and what stays open

- **Lane OC** gains DH1 and DH2.
- **A new lane, RT2** (research screen and tree, after lane RT): DH4, DH6 and DH7, with sources for every content change.
- **DH8** is a packaging change that the orchestrator makes: the third launcher in the game zip, checked on the CI artifact.
- **Queue,** one agent at a time after the G2 final: OC, BN, AR, RT2; then the final-round verification and the pass's report.
- **OPEN:**
  - DH3, item 17 (§3);
  - battalion sizes against DR24 (D-088 §5): archers 60 or 50, fixed-wing aircraft 1 or 2;
  - items 23 (ADR-059's amendment), 24 (the export's memory and speed, later or now) and 25 (a queue line for older UI
    overlaps);
  - the Longbow screenshot, if the Director plays that build again.
