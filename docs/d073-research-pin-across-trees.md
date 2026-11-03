# D-073: a research pin holds across the two trees (Director, 2026-11-03)

**Status: RULED.** Recorded from the Director's playtest of 2026-11-03 on build `1d4a4ba`:
- the report: "when there are cross pre requisites, such as from civics to knowledge, it should still hold when single clicked. i single clicked the written law node, went over to the knowledge tree. and couldnt see the pre requisites. totally ruins the whole orange line and box highlight scene";
- the Director's result for the playtest as a whole: "as far as my playtest is concerned, all pass";
- the Director's answer on the two human checklists still marked "not yet held": **both passed**;
- the Director's authorization: commit and push the fix as the next playtest build once the full validation passes. No merge, tag or release.

**How this record works:**
- **It adds a ruling.** It edits no earlier record (gov-4 §6).
- **Supersession:** it extends D-070's pin, which held only while the pinned node had a card in the tree on show.
- **What it does NOT do:** change any simulation behaviour, state, hash or golden. Every change is in `Sim.Ui`. It does not close M6 or touch the `CLAUDE.md` milestone line.

## 1. What the playtest reported

| # | Report | Diagnosis (checked against `640f1a5`) |
|---|---|---|
| P1 | Written law, pinned on the Civics tab, showed none of its prerequisites on the Knowledge & Technology tab. | **Defect.** `ProgressionScreen.Pin` was the selected node only while it had a visible card in the tree on show. Written law is a civic with no card in the Technology tree, so on that tab the pin was −1: no orange outline, no off-screen marker, no strip mark, and the detail panel fell back to the tree summary. A civic shown in the Technology tree only as a "From Civics" token had the related problem: pinned there, its orange set was empty, because a token has no incoming edges. |
| P2 | All playtest rows | PASS (Director, 2026-11-03), including rows A–H, I, J, K and row 3. Row 3, the law court, from the session workbook (seed 42, 368 turns): Written law researched turn 279, court ordered 343, built 344, founded 345, mature 355. |

## 2. Ruling

| # | Subject | Ruling |
|---|---|---|
| R1 | The pin across trees | **A pinned node stays pinned when the other tab is opened. That tab outlines in the orange its direct prerequisites that have a card there.**<br>- The off-screen markers and the overview strip find those prerequisites.<br>- The detail panel stays on the pinned node.<br>- The cards that are not its prerequisites recede, as for a pin on its own tab.<br>- Escape, a click on empty canvas or the drawer's × clears it, on either tab.<br>- A pinned node shown on a tab only as a "From …" token lights up its own prerequisites that have a card there.<br>- On its own tab nothing changes: the pin card, its orange prerequisites and its orange edges.<br>- No orange edge is drawn on the other tab, because the pinned card is not there to draw an edge to. The detail panel names it. |
| R2 | The two human checklists | **Both PASS (Director, 2026-11-03), from the playtest of `1d4a4ba`.**<br>- [`m6-playtest-checklist.md`](m6-plan/m6-playtest-checklist.md): the M6 playtest, the human gate for exit criterion 10 (the vertical slice).<br>- [`e1-visual-acceptance-checklist.md`](m6-plan/e1-visual-acceptance-checklist.md): the D-038 Part G visual gate on the E1 object layer (exit criterion 5). It is now held and passed, so the cap of eight parts per settlement is no longer provisional (D-067 R3).<br>- Both documents keep their text. Each carries a dated note pointing here. |

## 3. Validation

**Changed:** `ProgressionScreen.Pin` (a node of the other tree is carried), `PinnedPrerequisites` (for a carried pin or a token, the content's own prerequisites that have a card on the tab), and the tree painting, off-screen markers and strip (they draw the orange set whenever a pin is held, with or without a pin card). No other code changed.

**Tests (`Sim.Ui.Tests/ResearchPinTests.cs`):**
- `ThePin_IsCarriedToTheOtherTab_ForEveryNode_AndItsOrangeSetThereIsItsPrerequisitesWithACardThere`: every node, on both tabs. The pin and the detail panel hold, and the orange set is exactly its prerequisites with a card on that tab. There are anti-vacuity counts for carried pins and for tokens.
- `WrittenLaw_PinnedOnCivics_KeepsItsTechnologyPrerequisitesOrange_OnTheTechnologyTab`: the Director's case.
  - The pin is carried, with Written law's writing prerequisites (all technologies, "one of").
  - The painted orange outlines are exactly the visible prerequisite cards.
  - The strip has two marks per prerequisite.
  - Unpinned, nothing is orange. Escape clears the pin.
  - Back on Civics, the pin holds with its orange edges.
- `ACarriedPin_ShowsTheOffscreenMarker_ForItsPrerequisitesOnThisTab_AndItsClickBringsOneIn`.

**Mutants (each run against `ResearchPinTests`, 21 tests):**
- M1, the old `Pin`: 3 fail.
- M2, no carried prerequisites: 3 fail.
- M3, the orange painted only with a pin card: 1 fails (`WrittenLaw_…`).

**Moved pins:** none.

**Measured on this tree (2026-11-03, base `640f1a5` + this change):**
- Release build of `Sim.slnx`: 0 warnings, 0 errors.
- CI's 17 script checks: all exit 0.
- `UnitProgression` filter: 60 passed. `SessionExportTests` filter: 27 passed.
- Full `dotnet test Sim.slnx`: `Sim.Ui.Tests` 1,269 passed, 0 failed. `Sim.Tests` 3,429 passed, 36 skipped, 0 failed.
- `check-readonly-proof.sh`: rc 0.
- Smoke (`--smoke --ai-empires 1 --monkey 200` under xvfb): 3,524 checks (3,233 pass, 291 not offered, 0 FAIL), "SMOKE PASSED".
