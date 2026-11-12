# M7 pass state: what is done, what is in flight, and how to resume

**Living document, rewritten by the orchestrator at each step of the pass.** Last update: 2026-11-12 19:45 UTC (G2: polish round 1 on the trunk, critic round 2 running; CI run 991 on `737e66b` green). **Pace (Director, 2026-11-12 17:40 UTC, "Work 8 h, then stop"): the work stops at about 01:40 UTC on 2026-11-13 and pauses until the Director writes; a scheduled STOP message does it after a final snapshot.** Since 17:20 UTC: CR-027 applied (`d794358`, 17:22 UTC; Tier 3 green; CI run 990 and ui-artifact run 494 green) and on the final-round verification queue; the briefs of lanes OC, BN, AR, RT2 and RX written (`86ced60`); D-089 DH8's third launcher made (`737e66b`) and checked on the CI artifact. The Director answered the last open items earlier today (D-090); no question is open. The G2 final runs (first polish round); throttled to one agent. Its purpose (Director, 2026-11-11: "ensure everything is on the repo so that just in case we stop this pass in
between, things arent lost and re started again"): if this session or its container is lost, a new session can pick up the
pass from the repository alone, without redoing finished work.

- **Branch:** `claude/new-session-yavghm` (the only branch this pass pushes).
- **`main`:** `7d5defc` = tag `m6-exit`, untouched by this pass.
- **Tags and releases:** none added by this pass.

## 1. Done and on this branch

Every item below is a pushed commit. Lane reports are in `prototypes/m7-sprint1/reports/H-*.md` and the G2 reports in
`prototypes/m7-gfx2/reports/`. The running facts for the pass's final report are in
[notes/final-report-notes.md](notes/final-report-notes.md).

| Commit | What |
|---|---|
| `0929d28` | Governance records D-075..D-079, ADR-060/061/062, CR-013 reopened |
| `ae04d2c`, `f98869a` | Sprint-1 simulation prototypes and reports; G1 graphics prototype (the BEFORE baseline) |
| `f0fc281`, `f58d45b`, `0299bfa` | T1 time carry, T2 War Pulse, T3 two-action decision windows |
| `fc5230c`, `4914eb6` | D-080 (War Pulse answers), D-081 (ramming) |
| `7118ef0`, `6af83e9`, `6bd3aa2` | Lanes N (naval, air, combined arms, snipers), TR (terrain, deployment thirds), RAM (ramming) |
| `444b85e`, `9503d2b`, `ffcbcb8` | Lanes P (AutoResolver model 2), E (war economics), F (food comparison) |
| `0041f68`, `dfdbc2d`, `1601c74` | Lanes S (siege, garrisons), W (war outcomes), X report (CR-013 DetMath) |
| `0e756d7`, `bef4d49`, `a7ab42e` | G2 segment 1: setup, Victus harness and BEFORE package scripts, renderer core |
| `45c3313`, `d131774` | Lanes U (notification proposal), D (caps, support, War Sandbox, economic scale) |
| `535dc8e` | D-082 (victory morale, army scale, caps, ties, concealment) |
| `db2ef2b`, `c087a10`, `c536556` | G2 segment 2: characters, environment, perf-harness fix |
| `7f0cb92`, `4593fc9` | Lane MOR (two-level morale, DA1 rise); D-083 (entering a battle costs morale) |
| `9be7c18`, `7cacc3a` | Lane AP (hide, camouflage, entrench, ambush, equal sides); D-084 (research-tree audit, queued) |
| `0d2db74`, `47eac9a` | Lane MOR2 (entry cost, research annex rebuilt); lane CB (caps count battlefield units, army scale) |
| `259b739`, `d715de2` | D-085 (no fighting, no morale change); D-086 (run record keeps research; the close keeps the workbook) |
| `e052b51` | Lane MOR3 (D-085 DD1: without fighting, a battle changes no morale; settlement row v3; the in-battle part charged at the first fighting instant) |
| `93f2d8a` | The M7 charter check reads a backticked `file.md:LINE` token as a citation, not an id. CI's build-and-test job had failed at that check since `0929d28`, which skipped its build and test steps on this branch. |
| `dc19857` | Lane RT (D-084, the research-tree audit's fixes): grouped prerequisite expressions kept in the graph and the pane; completion-coloured edges; one bank-aware estimate (`InfoQuery.ResearchTurns`); the Longbow diagnosis; the tier-gap marker; the date review, with three `emerged` texts corrected. Tier 3 on the squashed tree: Sim.Tests 3,449 passed, 0 failed, 36 skipped; Sim.Ui.Tests 1,316 passed, 0 failed. It is this pass's first `Sim.*` change. |
| `cc351e5` | Lane RR (D-086, the run record keeps research; the close keeps the workbook): each turn's research rows in telemetry/v6 and a ninth workbook sheet, Research; the close on a worker thread behind a saving screen, recoverable after an interruption; a faster, leaner export; sessions past turn 1270 export again. Tier 3 on the squashed tree: Sim.Tests 3,462 passed, 0 failed, 36 skipped; Sim.Ui.Tests 1,345 passed, 0 failed; the UI smoke passed. |

Supporting material committed with this README:
- **Research:** the reference-morale research dossier and its claim files, [../research/ref-morale-2026-11-10/](../research/ref-morale-2026-11-10/dossier.md). It is the provenance of the morale proposal's Annex A; the proposal and H-MOR2 cite it by its old scratchpad path, `…/scratchpad/m7c/research/run1`.
- **Playtest:** the Director's failing playtest session 20261108-155547-a1 (four record files and the workbook rebuilt from them), [../../m6-plan/playtest-session-20261108-155547-a1/](../../m6-plan/playtest-session-20261108-155547-a1/README.md).
- **Director's messages:** verbatim working copies of the Director's messages of 2026-11-10 and 2026-11-11, [notes/director-messages/](notes/director-messages/). D-082..D-086 cite them by scratchpad path. The older working copies (D-079..D-081) were lost with an earlier container, and those records' quoted text is the authority.

## 2. In flight

**Throttled to one agent at a time** (Director, 2026-11-11 ~11:20 UTC: "throttle all work by 80%"). Lanes MOR3 (`e052b51`),
RT (`dc19857`) and RR (`cc351e5`) are integrated.

**Lane RT (D-084)** ran from 13:41 to about 21:00 UTC (`wf_2aee7a53-1fc`, base `51ab032`, [args/RT.json](args/RT.json)):
- its implementation finished at 15:45 UTC (`a7b5faa`);
- verification round 1, pinned to `a7b5faa`, found 6 findings: 4 minor and 2 nit, no blocker or major;
- fix round 1 finished at 20:59 UTC (`943b43f`). Lane claim: all six findings reproduced and fixed; 14 bounded mutants, 13 of
  them killed by semantic tests and 1 equivalent.

The lane workflow stops after a round with no blocker or major finding, so RT's fix round is unverified. It joins the
final-round verification queue below. The orchestrator integrated the lane as `dc19857` after Tier 3 on the squashed tree (§1).

**Lane RR (D-086)**, resumed, ran from 21:51 to 12:27 UTC (`wf_b1a17355-cee`, base `51ab032`, [args/RR.json](args/RR.json)).
No agent of its run had completed, so its implementation agent restarted and continued from its clone. The clone then held two
WIP commits (`88f125e`, `0a631db`) and uncommitted edits in 16 files. Its brief gained a resume note (where it stopped, what the
clone holds) and a note on the charters failure of its base, which `93f2d8a` fixed here. Since resuming, the lane made
fifteen commits (times from `git log`):
- `955d78c` (22:02): the 16 uncommitted files, byte for byte, with the test pins on telemetry/v6, the current-v6 export
  fixture and the workbook goldens re-pinned;
- `b849773` (22:35): the host's exit decision in `GameUi`, export-log lines on a failed close, a trace check for half-year turns;
- `39621be` (22:46): an end-to-end research test, a final-line-feed removal test, the README recovery note and a D-086 note;
- `a8ca9ef` (22:52), `e974f61` (23:13) and `ab5ec46` (23:17): the main report and the lane report, with the mutant kill
  records and an ADR-059 amendment listed as a PROPOSAL;
- `2ed2a6a` (00:04) and `8610ebd` (00:05): the validation results filled into both reports, and the reports' figures checked
  against the tree (docs only);
- `fe45f0b` (02:23, fix round 1): tests for repeatable-level completions, exposure credit, the Research sheet's NX row, the
  trace check's column rule and the keep-raw close; the headless export logs every failure (7 files, +348/−14);
- `861f9f9` (06:09, fix round 1): the lane report's section 10 (verdicts, mutants, validation) and four lines of the main
  report (docs only; 2 files, +220);
- `b2e7710` (07:32, fix round 2): tests for a city-state's research rows end to end, a node whose cost fell below its
  progress, and the workbook step's progress on the saving screen, with the `GameUi` and `SessionClose` code they reach
  (6 files, +387/−76);
- `1e385ca` (07:43, fix round 2): the city-state test's docstring (1 file, +2/−2);
- `1cdd101` (07:45) and `4ba7f4b` (08:28), fix round 2: the main report and the lane report, with section 11 (verdicts,
  changes, tests, mutants) (docs only; together 2 files, +176/−11);
- `c038b95` (12:25, fix round 2): its validation results, in the lane report's §11.6 and §11.7 and the main report's §7 (docs
  only; 2 files, +103/−3).

The implementation agent finished at 00:08 UTC with head `8610ebd` (lane diff from `51ab032`: 44 files, +8,636/−111).
**Verification round 1**, pinned to `8610ebd`, ran from 00:08 to 01:49 UTC. Its verifier reports that DE1 and DE2 are
implemented as claimed, and that the figures it re-measured reproduce: both full suites, the export on the Director's files,
the golden attribution and the half-year defect. It did not re-run the lane's in-game close harness or its 1,630-turn session.
It also closed a real window under Xvfb, which the lane had not tested: the saving screen drew, a second close request was
refused, and the window then closed by itself. The orchestrator has not re-measured any of this. Its four findings:
- **RR-R1-01 (major):** the research-row tests never reach two paths that the shipped content uses: a repeatable node's level
  completions, and exposure credit paid over several steps. Nor do they reach an empty v6 research array. Six bounded mutants
  survived every lane test. The verifier's own tests pass on the clean tree and kill all six, so the gap was in the tests, not
  the code.
- **RR-R1-02 (minor):** the reports say that the close changes only the research rows. In fact each turn now also writes
  research-name lines into the chronicle record (95 extra lines on an 850-turn session), and nothing attributes that change.
- **RR-R1-03 (minor):** three new branches have no test: the Research sheet's NX row, the trace check's column count, and
  the close's keep-raw guard. A bounded mutant of each survived.
- **RR-R1-04 (nit):** the headless `--export-session` logs only two exception types. For any other failure, the recovery
  note's promise breaks: the export log's last line does not give the reason.

**Fix round 1** ran from 01:49 to 06:11 UTC and committed `fe45f0b` and `861f9f9`. Lane claims:
- all four findings reproduced on `8610ebd` before any change;
- RR-R1-01 and RR-R1-03 fixed with tests, RR-R1-02 with documentation (the chronicle's name lines attributed: +15 lines on a
  300-turn game, every other line identical), and RR-R1-04 with a catch-all in the headless export;
- 12 of 12 bounded mutants killed by semantic tests, and no golden, pin or world hash moved;
- Debug full suites: Sim.Tests 3,441 passed, 0 failed, 36 skipped; Sim.Ui.Tests 1,298 passed, 0 failed. `validate.sh
  integration` under the lock gave the same counts in Release.

Lane diff from `51ab032` to `861f9f9`: 44 files, +9,193/−114 (MEASURED).

**Verification round 2**, pinned to `861f9f9`, ran from 06:11 to 07:04 UTC. Its verifier reports that the four round-1 fixes
hold. It re-measured:
- the export on the Director's files: lane 9.29–9.33 s and 698–704 MB; base 12.98–13.48 s and 1,290 MB;
- a 1,630-turn session: the base build refuses the export, and the lane writes it in 18.85 s with a 1,556 MB peak.

The orchestrator has not re-measured any of this. The round found no blocker and no major finding. Three findings are minor:
- **RR-R2-01:** no test or fixture holds a city-state's research rows, and two mutants survive. The fixture's README and one
  test's docstring say that they do.
- **RR-R2-02:** a completion where the node's cost fell below its progress is untested; one mutant survives.
- **RR-R2-03:** the saving screen's workbook progress ("N% of the telemetry read") has no test; two mutants survive.

The other six findings (RR-R2-04 to RR-R2-09) are nits:
- two number formats are pinned only by a golden;
- the NX legend and the code disagree;
- the headless export still crashes when the record folder cannot be listed;
- a failure at step 5, after the workbook was written, tells the player that it was not;
- the trace check accepts a header on any line;
- two reporting slips: a cached pass is counted without its label, and the main report's counts were not updated.

**Fix round 2** ran from 07:04 to 12:27 UTC and committed `b2e7710` to `c038b95` (above). The workflow runs at most two
rounds, so the fix round is unverified, and RR joins the final-round verification queue below. Lane claims, from its
handback (not re-measured by the orchestrator):
- the three minor findings were reproduced on `861f9f9` with the verifier's own mutants, then fixed: an end-to-end test of a
  city-state's rows (a settlement released before the first turn, as the R2a preview does: a founded game has no city-state,
  CITED from the founding, colonization and revolt code), rig tests of the bank-row order and of a cost that fell below the
  stored progress, and a test seam (null in the game) with two tests of the saving screen's progress;
- 7 of 7 bounded mutants killed, each by a semantic test; no `Sim.Core` change, and no golden, pin or world hash moved;
- Release, under the lock: `validate.sh integration`, Sim.Tests 3,443 passed, 0 failed, 36 skipped and Sim.Ui.Tests 1,301
  passed, 0 failed; `validate.sh full`, 13 PASS with the known charters false positive of its base. The smoke passed;
- Debug: Sim.Ui.Tests 1,301 passed, 0 failed. Sim.Tests failed one test, the wall-clock bench
  `PopulationTests.ProductionPipeline_PerPhaseBench_Reported` (5,359 ms against a 5,000 ms limit). Run alone three times on
  each tree, it took 4,880, 4,845 and 5,261 ms on the lane and 5,002, 4,845 and 5,041 ms on base `51ab032`, so it fails at
  base too.

The workflow passes nits to a fix round only in round 1, so round 2's six nits were not addressed. They go to the final
round with the fix round's tree.

Lane diff from `51ab032` to `c038b95`: 44 files, +9,769/−114 (MEASURED).

**Integrated as `cc351e5`** (from 12:27 UTC; MEASURED in a shared clone of `6a5b56a`). The squash gives tree `82c03fe`, equal to
`6a5b56a` plus the lane's diff, and no file overlaps what this branch changed since `51ab032`. Every path is inside D-086's
set, CI's 16 non-.NET steps pass, and the Release build has 0 warnings and 0 errors. Tier 3 ran under the lock from 12:30 to
13:23 UTC: 16 PASS, 0 failed, 6 NOT RUN (heavy, CI-only or manual). Sim.Tests: 3,462 passed, 0 failed, 36 skipped (35 min 44 s).
Sim.Ui.Tests: 1,345 passed, 0 failed (16 min 10 s). The UI smoke: SMOKE PASSED, 13 states and 4,123 checks with 0 failing (238 s). The commit's tree is the tested tree.

Lane claims, from its handbacks. Verification rounds 1 and 2 report them reproduced where they re-measured them (above). The
orchestrator measured the suites again at integration (above). The main report is `docs/m6-plan/run-record-research-and-exit-2026-11.md` in the
lane.
- **DE1:** telemetry/v6 carries each turn's research rows for every holder (the player, every AI polity, every city-state;
  a founded game has no city-state, so a city-state's rows are tested on the rigs and on a session with one released, RR-R2-01)
  and the Civics bank rows. The codebook goes to v6, and the workbook gains a
  ninth sheet, Research. An older session exports with a "not recorded" row.
- **DE2:** the close runs its five steps, unchanged and in order, on a worker thread while the window draws a saving screen. A
  second close request starts nothing, and the workbook is written whole. The export is streamed. On the Director's 173 MB
  files it took 12.5–13.3 s with a 1,290 MB peak before, and 8.8–9.7 s with 698 MB after, with identical workbook bytes.
- **Found by measurement:** every session past turn 1270 lost its workbook. From world year 5920 the trace holds half-year
  years, which the frozen kernel's `SessionTrace.Parse` refuses. The export now checks the trace itself. Continue last game,
  `sim replay` and `sim inspect` still refuse such sessions. The fix, proposal P1, is a kernel change and needs the
  Director's sign-off.
- **Validation:** Tier 3 passes apart from the known charters false positive on its base. The two full suites then ran under
  the lock: Sim.Tests 3,438 passed, 0 failed, 36 skipped; Sim.Ui.Tests 1,294 passed, 0 failed. The smoke passed, and 18 of
  18 bounded mutants were killed by semantic tests. The fix rounds' counts are above. The lane makes proposals P1–P6 and asks
  the Director no question (§5).

**Queue, in order, one agent at a time** (a paused workflow resumes with `resumeFromRunId`: finished agents replay from cache,
and the interrupted agent restarts and continues from its clone):

| # | Item | Workflow run | Script and args |
|---|---|---|---|
| 0 | DONE: CR-027 (D-088 DG2), the frozen kernel's trace reader reads the written year. Applied in `d794358` with two new tests and lane RR's pin moved. Tier 3: 16 passed, 0 failed, 6 NOT RUN (bench not requested, no hot path; CI-only and manual checks); the 1,630-turn session reproduces hash-for-hash through `sim inspect` | — | committed `d794358`; it joins row 2 |
| 1 | G2 final: RUNNING since 13:31 UTC (resumed; background task `wtticwedf`). The scenes stage finished at `65c99ec` (14:51) and is on the G2 trunk (`f6ea1c1`); critic round 1 listed 11 issues; polish round 1 fixed them (`ffd8abd`..`b11e14b`, its report `polish1.md`, 18:58) and is on the trunk; critic round 2 runs since 19:39; then the verifier and the AFTER package | `wf_d6eb0226-44e` | `m7-g2-v2.js`, args `{"base": "1601c74"}` |
| 1a | Lane OC, battle outcomes (D-087 DF7–DF11, D-088 and D-089 DH1, DH2: a tie is a tie, fighting means damage, the Standoff tie, one role per side, the ambush trigger and its battle, "fought" a required input, the player's capture choice; D-090 DI1: the "Ambush" and "Amphibious" test battles rewritten inside the thirds) | new | `workflows/m7-lanes-v3.js`, brief [`args/OC.json`](args/OC.json) (written 2026-11-12) |
| 1b | Lane BN, battalions (D-087 DF12, D-088 and D-090 DI2: DR24's formation sizes as battalion sizes, PROPOSED sizes for the types DR24 does not size, the battalion as the command unit, at most 25 per army, at most 5 per side in a battle, healing) | new | `workflows/m7-lanes-v3.js`, brief [`args/BN.json`](args/BN.json) (written 2026-11-12) |
| 1c | Lane AR, the AutoResolver for every domain (D-087 DF6 and D-088 DG12: aircraft, vessels, crew-served guns, embarked troops; 12 players and 40–50 such battles between turns; the parity bar, beaten where it can be; D-088 DG13: walls in both resolvers, as D-088 §4 assigns them) | new | `workflows/m7-lanes-v3.js`, brief [`args/AR.json`](args/AR.json) (written 2026-11-12) |
| 1d | Lane RT2, the research screen and an accurate tree (D-089 DH4, DH6, DH7: the inspector's labels; polish N2, N3, N4, N6; the date review's H1, H2, B1, K1, W1, W2 with sources, the tree's shape changed where accuracy needs it) | new | `workflows/m7-lanes-v3-sim.js`, brief [`args/RT2.json`](args/RT2.json) (written 2026-11-12) |
| 1e | Lane RX, the export's memory and speed (D-090 DI4: lane RR's P2 and P3, the same workbook bytes, measured before and after) | new | `workflows/m7-lanes-v3-sim.js`, brief [`args/RX.json`](args/RX.json) (written 2026-11-12) |
| 2 | Final-round verification (findings only) of lanes U, D, MOR, AP, MOR2, CB, MOR3 (`95446ff`), RT (`943b43f`) and RR (`c038b95`), and of CR-027 (`d794358`) | `wf_9b24d7c6-337` | `verify-final-rounds`: run its lanes one at a time, with MOR3, RT, RR and CR-027 added |
| 3 | Report draft | `wf_2a771902-a97` | `pass-report-draft` |

Lanes OC, BN, AR, RT2 and RX each run as a workflow of their own, one at a time, in that order. Each starts from the development branch's head after the previous lane's integration: the briefs' `base` reads `SET-AT-LAUNCH` and is set when the lane is launched. The v3 scripts are the v2 scripts with their shared context brought up to D-090 and CR-027 (the decisions D-087 to D-090, what they supersede, and the work integrated since lane CB); the protocol (implement, two adversarial verification rounds, fix rounds on reproduced findings only) is unchanged. Walls (D-088 DG13) are in lane AR's brief, as D-088 §4 assigns them; this table listed them under lane OC until 2026-11-12 17:34 UTC (`86ced60`).

Lanes MOR3 and RT each ran one verification round and one fix round, and neither round found a blocker or major finding (MOR3:
3 minor and 5 nit; RT: 4 minor and 2 nit). So their fix rounds are unverified, like the final rounds of the six lanes above.
Lane RR ran two rounds (round 2: 3 minor and 6 nit), and its fix round 2 is unverified because the workflow stops after two.
The final round also carries RR's six round-2 nits, which no fix round received.

**Snapshots of the work in flight: automatic every 30 minutes since 2026-11-12 16:22 UTC** (Director, 2026-11-12: "Do
stuff in a way that if i stop you, you are able to re continue without losing in your work").
[`tools/snapshot.sh`](tools/snapshot.sh) writes every piece of unfinished work into [wip/](wip/) and pushes it as
`docs: pass-state auto-snapshot … [skip ci]`; the commits touch only this folder's `wip/`, `notes/` and `tools/`, and skip CI.
- **What it holds:** the G2 trunk's work since `21a181e` and every G2 stage clone's commits and uncommitted edits as binary
  patches; every lane in flight as a bundle and an uncommitted-edits patch; the orchestrator's own unfinished clones (for
  example CR-027's fix) as patches; every workflow's journal (`wip/journals/`, each finished agent's result); and the notes.
- **[wip/SNAPSHOT.md](wip/SNAPSHOT.md)** names each file, its head, how it was made and how to restore it, with a restore
  test run in a fresh clone of the branch at each snapshot.
- **A stop loses at most the work since the last snapshot.** The finished work is on the branch (§1).
- **Earlier checkpoints:** the manual checkpoints of 2026-11-11 and 2026-11-12 (to 15:20 UTC) and their retired bundles and
  patches are in this branch's history; this README's earlier versions describe each.

**CI on this branch.** From `0929d28` to `afc7118`, CI's build-and-test job failed at the charter check and skipped its build and
test steps (§1, `93f2d8a`). Run 970 on `8e4a778` (MEASURED, read 16:30 UTC) succeeded in every job: build-and-test's 22 steps,
including the whole Sim.slnx Test step (49 min) and the read-only proof; determinism; determinism-xproc; calibration. `ui-artifact`
also succeeded. Every run since has succeeded in every job too (MEASURED, read 2026-11-12 09:22 UTC): runs 971 to 979, on
`a2025c0`, `b90078b`, `f94a9b4`, `dc19857`, `46c7a9d`, `39cf76a`, `7364c86`, `43a66ca` and `01b0509`, each with build-and-test's 22 steps,
determinism, determinism-xproc and calibration. Run 974 is the first on a `Sim.*` change of this pass (lane RT): its whole Sim.slnx Test
step took 48 min and its determinism harness 17 min. `calibration-nightly` was skipped, as it runs only on schedule.
`ui-artifact` runs 476 to 484 succeeded. The orchestrator reads the head's CI result after each push.

**Run 980 on `cf0b7f2` failed** (MEASURED, read 2026-11-12 11:21 UTC). Its build-and-test job's Test step (`dotnet test
Sim.slnx`) was aborted when the Sim.Ui.Tests test host crashed, after 342 passed tests, on an ImGui assertion: "Invalid
DisplaySize value!" (`imgui.cpp:11040`). Sim.Tests passed in the same step (3,449 passed, 0 failed, 36 skipped), and
determinism, determinism-xproc, calibration and `ui-artifact` run 485 succeeded. The commit changes only this folder; its code
is `01b0509`'s, on which run 979 passed every job. The cause is a test race that has been on `main` since `m6-exit`:
`SessionExportTests` and `SessionRecordFolderTests` each close the real `GameUi` through the headless frame harness, which
runs ImGui frames on its process-wide context, but neither class joins the `ImGui context` collection (no parallel runs) that
27 other test classes join. So xUnit 2.9.3, which runs test collections in parallel by default, can run the two at once. Lane
RR's fix round 2 caught the same crash in a blame run, with a test of each class in flight. Its one-line fix is lane RR's
proposal and waits for the Director (§5). The next push runs CI again on the same code.

**Run 982 on `6a5b56a`**, which has the same code as `cf0b7f2`, succeeded in every job (MEASURED, read 2026-11-12 13:23 UTC):
build-and-test's 22 steps, with a whole Sim.slnx Test step of 37 min; determinism; determinism-xproc; calibration. `ui-artifact`
run 486 succeeded too. The crash did not recur on the same code, which fits an intermittent race (INFERRED).

## 3. Queue after the items in flight

1. As each item in §2 finishes, the orchestrator verifies it (§4 step 2, or the G2 subtree method), commits, pushes, and
   updates this README. Lanes OC, BN, AR (D-087 §5), RT2 (D-089) and RX (D-090) run after the G2 final and before the
   final-round verification, in that order: AR's AutoResolver models BN's battalions. The orchestrator made D-089 DH8's packaging
   change in `737e66b` (a third launcher, `Rebuild the run workbook.cmd`, in the game zip), checked on the CI artifact: ui-artifact run 495
   printed the launcher, its "zip root" line lists it, and the Windows smoke of the shipped exe passed.
2. The pass's full report: 10 sections, DR52. It is built from [notes/final-report-notes.md](notes/final-report-notes.md), in
   the style of `docs/m7-plan/m7-execution-pass-report.md`.

If the pass stops, restart any item in §2 that has not been pushed with its listed workflow and brief. Use `base` set to this
branch's head; for G2, use `"from": "final"`.

## 4. How to resume in a fresh session or container

0. **Start from the latest snapshot:** [wip/SNAPSHOT.md](wip/SNAPSHOT.md) lists what was in flight and how to restore each
   piece. Restore those first, then continue each item from where its files and journals show it stopped, instead of
   starting it again.
1. **Bootstrap.** Run `./scripts/bootstrap.sh` (the .NET 10 SDK; containers do not keep it). Read `CLAUDE.md`,
   `docs/current-state.md`, this README, then D-079..D-086.
2. **Lanes.** The lane workflow takes `{base, lanes:[{key, title, brief}]}` from `args/<KEY>.json`. It:
   - clones into `/home/user/m7w/<key>`;
   - runs implementation, then verification and fix rounds;
   - writes patches, bundles and reports to the session scratchpad (`m7c/`).

   The orchestrator then integrates each lane:
   1. a shared clone of this branch;
   2. `git merge --squash` of the lane;
   3. a check that the tree equals the lane's tree;
   4. Release builds and tests, `check-proto-determinism.sh`, `check-doc-links.py --all`, `git diff --cached --check`, an
      originality grep and a scope check;
   5. a commit with only measured claims, then a fast-forward and push.
3. **G2.** The G2 workflow expects:
   - `/home/user/g2w/trunk`: branch `g2/trunk` with history from `1601c74`. Its last integrated commit, `21a181e`, exists
     only in this container. Its `prototypes/m7-gfx2` tree equals this branch's (tree `ea87bc3`, checked 13:20), so a new
     trunk can be cut from this branch.
   - `/home/user/g2w/assets`: run `prototypes/m7-gfx2/scripts/fetch-assets.sh`, which uses pinned sha256 sums and fetches
     about 296 MB from GitHub raw, media and codeload hosts, PyPI and npm.
   - headless rendering: `SDL_VIDEODRIVER=offscreen LIBGL_ALWAYS_SOFTWARE=1`.

   To restore the stage's work, apply the §2 patches onto that trunk: the committed patch first, then the uncommitted one. Then
   run with `from` set to the first unfinished segment. G2 integrates onto
   this branch by replacing the `prototypes/m7-gfx2` subtree with the trunk's: a temporary index, `read-tree`, `commit-tree`.
4. **Pace and authority.**
   - One agent at a time.
   - Commits and pushes to this branch only; no merge to `main`, no tag, no release.
   - Report the commit scope before each commit.
   - `Sim.*` changes are allowed only under D-084 and D-086, for their own work. D-079 Q5 stays open for everything else.

## 5. Open questions for the Director (as of this update)

**Answered on 2026-11-12, recorded as [D-087](../../d087-ties-battalions-autoresolve-director-answers-2026-11-12.md)** (items
1–12 of the orchestrator's plain-language list): lane D's READING A (superseded: every AI battle is auto-resolved); D-085's
second reading (fighting means damage); lane AP's Q-AP1, Q-AP3 and Q-AP4 (the attacked army defends; a Standoff and equal sides
end in a tie, in both resolvers); lane CB's E1, READING CB-1 and the performance cap (armies fight in battalions, at most 25);
lane RR's research-name hook (kept); the ImGui test race (fixed), the Debug bench budget (Release only) and the slow-test
record (nothing to record: the test takes 14.5 s in Release).

**Answered on 2026-11-12 (second message), recorded as [D-088](../../d088-battalion-limits-healing-walls-director-answers-2026-11-12.md)**
(items 1–14 of the orchestrator's second list): lanes OC, BN and AR are built in this pass; lane RR's P1 is signed off
([CR-027](../../adr/cr-027-session-trace-reads-the-written-year.md)); D-087's readings DF7-R1, DF7-R2, DF8-R1, DF9-R1 and DF12-R1
are confirmed, and DF8-R2 and DF9-R2 are replaced (one role per side; the ambusher deploys in its own third of the battlefield);
DR25's 50,000 is an empire's total; a side brings at most 5 battalions to a battle, its own empire's first; healing (lane CB's
READING CB-4), the parity bar (D-079 Q4, lane P's acceptance) and walls (lane MOR3's PROPOSAL) are ruled. Still open:

**Answered on 2026-11-12 (third message), recorded as [D-089](../../d089-fought-capture-choice-research-tree-launcher-director-answers-2026-11-12.md)**
(items 15–22): "fought" a required input; the player's capture choice at the moment of capture (U-Q11); the research
inspector's labels fixed (lane RT's N1) and its polish N2, N3, N4, N6 built now; lane RT's three readings kept; the tree made
historically accurate, its shape changed where needed; the rebuild launcher in the game zip (lane RR's P5); and D-088's
readings DG3-R1 (5 battalions in total per side), DG13-R3 (walls additive), DG13-R4 (breaching artillery) and DG5-R1 (one
turn heals) confirmed.

**Answered on 2026-11-12 (fourth message), recorded as [D-090](../../d090-test-battles-dr24-battalions-export-director-answers-2026-11-12.md):**
the two test battles are rewritten inside the thirds; battalion sizes are DR24's (D-087's numbers were examples); ADR-059 is
amended (done); the export's memory and speed work is built in this pass (lane RX); lane U's queue line is added (done). No
question of the orchestrator's lists of 2026-11-12 is open. D-088's readings DG5-R3 (home territory: land the empire
controls), DG13-R1 (walls in both resolvers) and DG13-R2 (walls multiply attack and toughness) stay applied until the
Director corrects them.
- Earlier open items: R6 (ships and drones under their formation caps); lane U's decision-tray placement and its queue line;
  the DA3 calibration numbers (A-38, M7.14).
- Lane MOR3 asks no question, since D-085 §4 delegated its design. Its two PROPOSALS (H-MOR3 §13) are settled: walls by
  D-088 DG13, and `Fought` as a required hand-off input by D-089 DH1.
- Lane RT ([research-tree-fixes-2026-11.md](../../m6-plan/research-tree-fixes-2026-11.md) §4–§5, H-RT §10.7):
  - Longbow: a screenshot of the detail pane as seen in play, and the build that was run.
  - N1: may the inspector's ENABLED BY and WHY LOCKED labels (`InfoQuery`) be corrected under D-084? That is a `Sim.Core/State`
    change outside the estimate.
  - The content proposals H1, H2, B1 and K1 of the date review; each changes a prerequisite, a definition or a D-047 date.
  - The fix round's three READINGS: no estimate for a civic not yet open; hovered incoming edges cased alike; the "either" head.
  - PROPOSALS: N2 (the hover path's dependent edges), N3 (spine lane spacing), N4 (subheadings under REQUIRES), N6 (a
    route-aware estimate for a node not yet open).
- Lane RR (from its handbacks; integrated as `cc351e5`): no question for the Director, and six PROPOSALS. Its
  fix rounds also raised items for the orchestrator. The Director has since ruled on the first four below (D-087 DF1–DF4) and
  asked about P1 (DF5); the bullets keep the evidence:
  - Its question: is the hook that writes research-name lines into the chronicle each turn (`UiSession.Step`) in scope?
    That is the per-turn record path, not one of the paths the brief names. The orchestrator's reading is yes: DE1 asks for
    every code in the Codebook, and the chronicle's name lines are the record's only legend (ADR-059). The change is
    attributed in the lane's reports, and the Director may rule otherwise at acceptance.
  - Its proposal: a one-line `[Collection("ImGui context")]` on two `Sim.Ui.Tests` classes, to stop a test-host crash on an
    ImGui assertion. The crash predates this pass: on base `51ab032`, verification round 2 reproduced it in 2 of 3 Debug runs
    of the grouped filter. On 2026-11-12 it also failed CI run 980 on this branch, and run 982 on the same code passed (§2).
    The fix is outside D-086's own work, so it waits for the Director under D-079 §6, whose rule stands outside the work
    D-084 and D-086 authorize.
  - From fix round 2: `PopulationTests.ProductionPipeline_PerPhaseBench_Reported` holds a 5,000 ms wall-clock budget that
    fails intermittently in Debug on this machine, at base `51ab032` too (lane-measured: two of three runs there took 5,002
    and 5,041 ms). Changing a test's budget is outside D-086's work too, so it waits for the Director in the same way.
  - Also from fix round 2: the new 64-turn city-state test (about 29 s in Debug, lane-measured) is not recorded as slow in
    `scripts/test-durations.json`, which was outside the lane's paths, so Tier 1 runs it.
  - P1 needs the Director's sign-off: a change to the frozen kernel's `SessionTrace.Parse`, so that sessions past turn 1270
    can be continued, replayed and inspected again.
  - P2: stream the telemetry from the file.
  - P3: a parallel encode with identical bytes.
  - P4: alternatives to the Telemetry sheet as the log's only copy, only if P2 and P3 fall short.
  - P5: a rebuild-workbook launcher in the game zip.
  - P6: the ADR-059 amendment-log entry.

## 6. What this folder holds

| Path | What |
|---|---|
| `workflows/m7-lanes-v2.js` | The lane workflow (implement, then adversarial verify, then fix, up to two rounds), as last run (lane CB) |
| `workflows/m7-g2-v2.js` | The G2 graphics workflow (segments stage1, stage2, stage3, final) |
| `workflows/ref-morale-research.js` | The reference-morale research workflow (sequential sweeps, a provenance check, the dossier) |
| `args/*.json` | Every lane brief this pass ran (U, D, MOR, AP, MOR2, CB, MOR3, RT, and RR with its resume note); `G2.json` holds the G2 args |
| `notes/final-report-notes.md` | The orchestrator's running facts per commit, incident and decision: the source of the final report |
| `notes/orchestration-log.md` | The queue and pace log |
| `notes/director-messages/` | Verbatim working copies of the Director's messages cited by D-082..D-086 |

The workflow scripts and notes use this session's absolute paths: `/home/user/civDemo`, `/home/user/m7w`, `/home/user/g2w`,
and the scratchpad under `/tmp/claude-0/…`. Adjust them in a new environment.
