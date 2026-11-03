# D-072: the research detail panel is a column of its own (Director, 2026-11-03)

**Status: RULED.** Recorded from the Director's playtest of 2026-11-03 on build `1d4a4ba` (D-071):
- the report: "the details kinda overlap on the nodes. Once i click the nodes, the detail box opens and then it hides the right side of the tree. This needs its own separate secion" (screenshot: the Civics tree, "Oath alliances" pinned, the panel over the tree's right edge);
- the Director's choice between the options put to them: "Always its own column", not a column that opens only while a node is selected;
- the Director's authorization: commit and push the fix as the next playtest build once the full validation passes. No merge, tag or release.

**How this record works:**
- **It adds a ruling.** It edits no earlier record (gov-4 §6).
- **Supersession:** it changes one number of UR-4 (M5 polish): the window width from which the detail panel docks.
- **What it does NOT do:** change any simulation behaviour, state, hash or golden. Every change is in `Sim.Ui`. It does not close M6 or touch the `CLAUDE.md` milestone line.

## 1. What the playtest reported

| # | Report | Diagnosis (checked against `1d4a4ba`) |
|---|---|---|
| P1 | Clicking a node opens the detail box over the right side of the tree, hiding nodes. | **Defect.** UR-4 docked the panel beside the tree only from 1600 px at UI scale 1 (`ProgressionScreen.DockMinWidth`, compared with the window width ÷ UI scale). Below that, the panel was an overlay drawer over the tree's right edge. The UI scale follows Windows display scaling (`UiScale.Auto`), so a 1920 px screen at 125% is 1536 logical px and at 150% is 1280: both got the drawer. So did 1366 × 768 and 1280 × 800 at 100%. |
| P2 | Rows I and J (one Excel file; "Continue last game") and rows A–H (pin, double-click, orange prerequisites, banked Civics Points, "Idea") | PASS (Director, 2026-11-03). |

## 2. Ruling

| # | Subject | Ruling |
|---|---|---|
| R1 | The detail panel's column | **The detail panel is docked in its own column right of the tree from 1000 px at UI scale 1 (it was 1600).**<br>- The tree canvas and its overview strip end where the panel begins. No card the tree shows is under the panel.<br>- The column is always there: with nothing pinned it shows the tree's summary and how to use it, as wide windows already did. Pinning or clearing a node neither narrows nor reflows the tree.<br>- The tree lays out to the narrower width (it fits without sideways scrolling at 1920 × 1080 at 125% and 150%, 1366 × 768 and 1280 × 800).<br>- The overlay drawer is kept only as the fallback for windows below 1000 logical px, too narrow for two columns. |

## 3. Validation

**Changed:** `ProgressionScreen.DockMinWidth` 1600 → 1000. No other code changed.

**Tests:**
- New: `ReadabilityStageBTests.TheDetailPanel_IsItsOwnColumn_AndCoversNoCard_AtCommonWindowsAndScales`. It covers 1920 × 1080 at scales 1.25, 1.5 and 1, 1366 × 768, 1280 × 800 and 1000 × 700, on both tabs. For each it checks that the panel is docked, the canvas ends where the panel begins, the pin does not change the tree's width, no visible card lies under the panel, and one pixel below the threshold the drawer is the fallback.
- Re-rigged to keep testing the fallback drawer at 960 × 640 (they used 1366 × 768, which now docks): `ReadabilityStageBTests.BelowOneThousand_TheDetailIsADrawer_…` (renamed from `BelowSixteenHundred_…`), `ResearchPinTests.ADoubleClick_OnACardTheFirstClicksDrawerCovers_StillOrdersThatCard`, and the close-button part of `ResearchPinTests` that clears the pin.
- `ResearchPinHostTests.ThroughTheHost_EscapeClearsThePinFirst_ThenClosesTheScreen` runs in the host's 1280 px window, which now docks; it asserts the docked panel instead of the drawer.
- **Mutant:** with the old threshold (1600) the new test fails at 1920 × 1080 at 1.25 and 1.5, 1366 × 768, 1280 × 800 and 1000 × 700 (5 of 6; 1920 × 1080 at scale 1 docked before too).

**Gate:** the gate's windows are 1280 × 800 and 1920 × 1080, so the row `detail drawer close button (narrow windows: …)` is now "not offered" in every state. The drawer is exercised by the unit tests above at 960 × 640.

**Moved pins:** none.

**Measured on this tree (2026-11-03, base `1d4a4ba` + this change):**
- Release build of `Sim.slnx`: 0 warnings, 0 errors.
- CI's 17 script checks: all exit 0. These are the banned constructs, read isolation, the four research audits and their unit tests, tree closure, charters, `validate.py --live` with the report unchanged, its fixtures and mutants, the sourcing queue, runtime texts, codebooks, tiered validation and doc links.
- `UnitProgression` filter: 60 passed. `SessionExportTests` filter: 27 passed.
- Full `dotnet test Sim.slnx`: `Sim.Ui.Tests` 1,266 passed, 0 failed. `Sim.Tests` 3,429 passed, 36 skipped, 0 failed.
- `check-readonly-proof.sh`: rc 0.
- Smoke (`--smoke --ai-empires 1 --monkey 200` under xvfb): 11 states, 3,524 checks (3,233 pass, 291 not offered, 0 FAIL), 0 problems, "SMOKE PASSED".
