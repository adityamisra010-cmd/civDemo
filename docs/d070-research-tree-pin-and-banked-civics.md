# D-070: research trees — click pins a node, double-click orders it, prerequisites shown in orange; banked Civics Points shown (Director, 2026-11-02)

**Status: RULED.** Recorded from the Director's playtest feedback of 2026-11-02 on build `511e70f` (D-068). The Director asked for "hover / click to select / double click to start researching", with the selected node's prerequisites highlighted in orange ("since yellow means it has been researched") on both trees, and for the accumulated Civics Points to be visible.

- **What this record does:** it adds rulings. It edits no earlier record (gov-4 §6).
- **What it does NOT do:**
  - It changes no simulation behaviour, state, hash or golden. Every change is in `Sim.Ui` (presentation, input, theme).
  - It does not close M6 or touch the `CLAUDE.md` milestone line.
  - Commit and push for the playtest build were authorized by the Director on 2026-11-02 ("yeah do it"); no merge, tag or release.

## 1. What the playtest reported

| # | Report | Diagnosis (checked against `511e70f`) |
|---|---|---|
| P1 | Hovering a node shows its prerequisite lines, but scrolling up to find the prerequisite moves the pointer off the node, so the highlight is lost. The parallel lines are too thin to follow. | **Defect.** `ProgressionScreen.Highlight()` and `DetailNode` preferred the hovered card over the selected one, so selection never held the highlight. A single click also ordered research, so there was no "select without ordering". |
| P2 | Only Civics Points per turn are shown; the banked total is nowhere to be seen. | **Defect.** The bank (`ResearchQuery.CivicsPointsBalance`) was painted only in the docked intro panel, which appears only in windows at least 1600 px wide. The top-right block and the Civics chip showed the rate alone. |
| P3 | The two trees are separate, and CP accumulates and is spent instantly on the next civic. | PASS. |

## 2. Rulings

| # | Subject | Ruling |
|---|---|---|
| R1 | Click, double-click | On both research trees, **a single click on a node pins it and orders nothing. A double-click orders it**, exactly as a click did before: same order factory, same destination routing, same admissibility. Double-click means a second click on the same card within 0.4 s of host frame time and 6 px. The detail panel's button still orders. A click on empty canvas, the drawer's close button, or Esc clears the pin. Esc clears the pin first, then closes the screen. |
| R2 | Orange prerequisites | **The pinned node's prerequisites are outlined in orange,** and so are its incoming edges, drawn twice the normal width. This covers required, "one of" (dashed) and cross-tree prerequisites. The highlight stays through scroll, zoom, drag, the overview strip and hovering other cards. The detail panel stays on the pinned node. Hovering another card adds its own transient highlight and tip. The overview strip marks the orange cards. A marker at the canvas edge ("▲ N prerequisites above / below") scrolls to the nearest one. |
| R3 | The orange token | A new semantic token, `Semantic.Pinned`, is added: hue 30, vivid in every Age. It is at least ΔE*ab 25 from every existing orange-family and state token in all nine Age themes; the minimum is 26.5. No orange is both distinct enough and 3:1 against the light card fills, so **every orange stroke is drawn over a dark casing**. The casing is ≥ 7.0:1 against every fill, and the orange is ≥ 5.2:1 against the casing. |
| R4 | Banked Civics Points | **The bank is shown wherever Civics is shown.** On the Civics tab, the top-right figure is the bank ("715.2 CP") with the caption "banked · +17.2 / turn". The Technology tab is unchanged. The top-bar Civics chip ends with "· X CP banked". In narrow windows that shortens to "· X CP", and is dropped only when even that cannot fit. The chip's hover card states the bank, the rate and what the lane is doing. |

## 3. Moved pins

- `EraSeparationTests.A6CanonicalHash` moves from `7e2e3eed…5287b` to `85a96624…709e59`. The cause is the one new canonical line, `Semantic.Pinned=#FF8000`. A new test proves the canonical text without that line still hashes to the old value, so every other A6 token is byte-identical.
- No simulation golden, world hash, content hash or tree-closure pin moves.

## 4. Validation

Measured 2026-11-02 on the combined uncommitted tree (`511e70f` + D-069 + this batch):

- **Build:** `dotnet build Sim.slnx -c Release -nodeReuse:false`: 0 warnings, 0 errors.
- **Static checks:**
  - `check-banned-constructs.sh`: OK.
  - `check-read-isolation.sh`: OK.
  - `check_charters.py`: ALL CHECKS PASS.
  - `check-doc-links.py` over `docs/*.md` and `docs/**/*.md`: 321 files, 0 broken links.
- **Sim.Ui.Tests (full suite):** 1250 passed, 0 failed, 0 skipped (17 m 6 s).
  - The suite has 1229 tests before this batch and 1250 with it.
  - The new tests are `ResearchPinTests` and `ResearchPinHostTests`.
- **Sim.Tests (guard and tree-closure tests):** `ThirdPartyReferenceGuard` and `TreeClosureBaseline`, 44 passed, 0 failed.
- **Smoke:** `--smoke --ai-empires 1 --monkey 200` on 11 states, 3524 checks.
  - 3259 checks passed, 265 were not offered, 0 failed and 0 problems were found.
  - The run reported SMOKE PASSED.
- **Mutants:** three mutants were run on the agent's worktree, each bounded at 3× the clean focused run (27 s). None hung.
  - Highlight follows the hover again: killed.
  - A single click orders: killed.
  - "One of" prerequisites are dropped from the orange set: killed.
