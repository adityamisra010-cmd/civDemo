# D-074: M6 closed at its exit gate; the law court's effect said honestly (Director, 2026-11-03)

**Status: RULED.** Recorded from the Director's instructions of 2026-11-03, after the playtest of build `1d4a4ba` and the D-073 fix:
- "Update the M6 playtest checklist to PASS, including the law-court result. Update the E1 Part G checklist to PASS and make the eight-part cap final. Update the exit inventory and current-state.md to record the completed gates and the M6 exit decision. Record your formal M6 closure ruling."
- "Separately authorize the fast-forward merge, m6-exit tag/release and CLAUDE.md milestone update. and yes the court messages too."
- The Director's answers to the two questions put to them:
  - "Yes, authorized now": M6 is CLOSED, and after the final commit is green, `main` is fast-forwarded, `m6-exit` is tagged and released, and the `CLAUDE.md` milestone line is updated.
  - The milestone line reads "M6 closed; M7 next, spec not ratified".

**How this record works:**
- **It adds a ruling.** It edits no earlier record (gov-4 §6). The checklists and the exit inventory keep their text and carry dated notes pointing here.
- **What R2 changes:** presentation only (`Sim.Ui`, one `Sim.Core` query text, one research description). No simulation behaviour, state, hash or golden moves. The tree-closure TEXT pin moves (TC-5).

## 1. The exit gate

| # | Gate (`m6-exit-inventory.md` §4, §8.2) | Outcome |
|---|---|---|
| G1 | The Director's playtest of the slice (`m6-playtest-checklist.md`, criterion 10) | **PASS** (Director, 2026-11-03, [D-073](d073-research-pin-across-trees.md) R2).<br>- Every row passed on `1d4a4ba`.<br>- The law court (row 3), from the session workbook (seed 42, 368 turns): Written law researched turn 279, court ordered 343, built 344, founded 345, mature 355.<br>- The one defect the playtest found (the pin across trees) is fixed by D-073. |
| G2 | The D-038 Part G visual gate (`e1-visual-acceptance-checklist.md`, criterion 5) | **PASS** (Director, 2026-11-03, D-073 R2). The cap of eight parts per settlement is **final** (it was provisional until this gate, D-067 R3). |
| G3 | Opening an exported workbook | **PASS.** The Director played from, and returned, the exported workbooks of 2026-11-02 and 2026-11-03, and ruled the one-file export row (row I) PASS. |
| G4 | Windows smoke and CI on a pushed tree | **PASS.** Each push since D-068 built the Windows playtest artifact, and its `windows-smoke` job and CI ran green. The latest before this record, D-073 `70b9dc2`, has CI run 41638210816 green, and ui-artifact run 41638210846 green with `sim-ui-win-x64-70b9dc2` and its `windows-smoke`. The final commit's own runs are on GitHub and are named in the `m6-exit` Release notes. The 20-seed nightly runs on `main` on its schedule from the merge on. |
| G5 | The exit ruling and the `CLAUDE.md` milestone line | **RULED: M6 CLOSED (R1).** The milestone line is updated in the same commit as this record (R3). |

**Carried, not waived (open after M6, owners named in the exit inventory):**
- the E1 frame budget over-run on 20 of 48 near-empty-map rows (≤ 2.8 ms) — the frame-budget choice;
- the open items of `m6-exit-inventory.md` §3;
- M6.24 (D-051 §2/§4), whose deferral was "until after the playtest" (D-067 R19) and now needs a milestone;
- the research-economy candidate and pacing (D-066 R4);
- the status headers that still read PROPOSED although their switches ship on (ADR-047, ADR-048, ADR-049; TC-1). This ruling does not mark TC-1 CLOSED.

## 2. Rulings

| # | Subject | Ruling |
|---|---|---|
| R1 | M6 | **M6 Knowledge / Research / Technology is CLOSED at its exit gate (2026-11-03).**<br>- Every exit criterion of `m6-spec` §5 (as amended by D-062, D-066 and D-067) is PASS or deferred by ruling (§1; the exit inventory's dated note).<br>- The branch carries the ratified M7 pass (ADR-055, EC-1/2/3; D-067 R15–R18), and it goes to `main` with M6. |
| R2 | The law court's effect, said honestly | **The court now says what it does.**<br>- **Its panel line** (`PlayerViews.EffectNow`): with no tax, "It lightens how heavily your tax is felt here; you levy no tax, so it has no effect yet." Under a tax, "Your tax is felt more lightly here (public services, this one among them)" with the factor the levy's felt burden is multiplied by there. It was "Its effect is not measured."<br>- **The End Turn that founds an institution announces it** (the toast and the POLICY panel's notices): "founded: Law Court at <town>", then what it does and that choosing laws is not yet part of the game. A university's line names the branch its research cheapens.<br>- **Its information card** says the same in plain words (it named an engine function).<br>- **Written law's description** adds the court's effect and "Choosing laws and forms of government is not yet part of the game." This is TC-5, text only.<br>- Laws and the adoption of government stay M8 by ruling (D-062 R3). Nothing here opens a policies window. |
| R3 | Authorizations | **Authorized, in this order, each only once the one before is green:**<br>1. Commit and push the D-074 tree to `claude/new-session-yavghm`.<br>2. CI and the Windows build with its `windows-smoke` green on that commit.<br>3. Fast-forward `main` to it.<br>4. Tag `m6-exit` and publish the GitHub Release.<br>The `CLAUDE.md` milestone line reads: M6 closed at its exit gate 2026-11-03 (D-074); M7 Battle Layer next, its spec in preparation and not ratified, no active packets. |

**A correction to the playtest analysis of 2026-11-03.** Written law itself has a realized effect: grievance builds more slowly (`society.grievanceRelief` +0.014, from turn 279 in the Director's game). The court's own effect acts only under a tax, so in that game the court had none. The earlier statement that "the court did nothing" stands. Any implication that researching Written law did nothing was wrong.

## 3. Validation

**R2 tests (`Sim.Ui.Tests/LawCourtClarityTests.cs`):**
- **The effect line.** With no tax it reads exactly as above. Under a tax, its factor is 1 − `taxServiceOffsetMax` × `ServiceOffset`, and the court lowers the levy's felt burden at that town.
- **The panel.** No institution panel line says "not measured", at four densities.
- **The founding turn.** It announces the court with its line, with and without a tax, and the next End Turn does not announce it again.

**Tree closure (`Sim.Tests/Systems/TreeClosureBaselineTests.cs`):** TEXT pin `32a6f56f…` → `4399a4099e684337c6fe7a4018992ba85bbf72201d7d07a41fa7c246c60d6256` (TC-5). The new test `ThePreTc5LawCodeDescription_ReproducesTheTc4Text_Tc5IsTextOnly` restores the old description and gets the TC-4 TEXT and the closure STRUCTURE. The four research audit reports are regenerated. The research-economy candidate file (`research-cost-pass/candidate/research.candidate.json`) carries the same text, as at TC-4, so it still differs from the shipped trees only in its tuning, scale and batched costs (`ResearchCandidateTests`).

**Mutants (each against `LawCourtClarityTests`, 3 tests):**
- CM1, the old "not measured" line: 2 fail.
- CM2, no founding notice: 1 fails.
- CM3, the tax ignored: 1 fails.

**Moved pins:** tree-closure TEXT (TC-5) only.

**Measured on this tree (2026-11-03, base `70b9dc2` + this change):**
- Release build of `Sim.slnx`: 0 warnings, 0 errors.
- CI's 17 script checks: all exit 0.
- `UnitProgression` filter: 60 passed. `SessionExportTests` filter: 27 passed.
- Full `dotnet test Sim.slnx`, first run: `Sim.Ui.Tests` 1,272 passed, 0 failed. `Sim.Tests` 3,429 passed, 36 skipped, and 1 failed.
  - The failure was `ResearchCandidateTests.CandidateFile_DiffersFromShippedOnlyInTheTuningSwitches_TheScale_AndTheBatchedNodes`: the candidate file still had Written law's TC-4 text.
  - Fixed by carrying the TC-5 text into the candidate. Re-run: `ResearchCandidateTests` and `TreeClosureBaselineTests` 10/10, `AiSupportingMilestoneTests` (the other reader of the candidate) 9/9.
  - Also re-run: the validation-selector tests, doc links, tree closure, `validate.py --live` and its report check, all exit 0.
- `check-readonly-proof.sh`: rc 0.
- Smoke (`--smoke --ai-empires 1 --monkey 200` under xvfb): 3,524 checks (3,233 pass, 291 not offered, 0 FAIL), "SMOKE PASSED".

