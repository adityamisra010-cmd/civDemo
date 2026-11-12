# M7 pass state: what is done, what is in flight, and how to resume

**Living document, rewritten by the orchestrator at each step of the pass.** Last update: 2026-11-12 01:20 UTC, WIP checkpoint (lane RR's implementation finished at `8610ebd`; its verification round 1 is running; CI green on `39cf76a`; throttled to one agent). Its purpose (Director, 2026-11-11: "ensure everything is on the repo so that just in case we stop this pass in
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

Supporting material committed with this README:
- **Research:** the reference-morale research dossier and its claim files, [../research/ref-morale-2026-11-10/](../research/ref-morale-2026-11-10/dossier.md). It is the provenance of the morale proposal's Annex A; the proposal and H-MOR2 cite it by its old scratchpad path, `…/scratchpad/m7c/research/run1`.
- **Playtest:** the Director's failing playtest session 20261108-155547-a1 (four record files and the workbook rebuilt from them), [../../m6-plan/playtest-session-20261108-155547-a1/](../../m6-plan/playtest-session-20261108-155547-a1/README.md).
- **Director's messages:** verbatim working copies of the Director's messages of 2026-11-10 and 2026-11-11, [notes/director-messages/](notes/director-messages/). D-082..D-086 cite them by scratchpad path. The older working copies (D-079..D-081) were lost with an earlier container, and those records' quoted text is the authority.

## 2. In flight

**Throttled to one agent at a time** (Director, 2026-11-11 ~11:20 UTC: "throttle all work by 80%"). Lanes MOR3 (`e052b51`)
and RT (`dc19857`) are integrated.

**Lane RT (D-084)** ran from 13:41 to about 21:00 UTC (`wf_2aee7a53-1fc`, base `51ab032`, [args/RT.json](args/RT.json)):
- its implementation finished at 15:45 UTC (`a7b5faa`);
- verification round 1, pinned to `a7b5faa`, found 6 findings: 4 minor and 2 nit, no blocker or major;
- fix round 1 finished at 20:59 UTC (`943b43f`). Lane claim: all six findings reproduced and fixed; 14 bounded mutants, 13 of
  them killed by semantic tests and 1 equivalent.

The lane workflow stops after a round with no blocker or major finding, so RT's fix round is unverified. It joins the
final-round verification queue below. The orchestrator integrated the lane as `dc19857` after Tier 3 on the squashed tree (§1).

**Running since 21:51 UTC: lane RR (D-086)**, resumed (`wf_b1a17355-cee`, base `51ab032`, [args/RR.json](args/RR.json)).
No agent of its run had completed, so its implementation agent restarted and continued from its clone. The clone then held two
WIP commits (`88f125e`, `0a631db`) and uncommitted edits in 16 files. Its brief gained a resume note (where it stopped, what the
clone holds) and a note on the charters failure of its base, which `93f2d8a` fixed here. Since resuming, the lane has made eight
commits (times from `git log`):
- `955d78c` (22:02): the 16 uncommitted files, byte for byte, with the test pins on telemetry/v6, the current-v6 export
  fixture and the workbook goldens re-pinned;
- `b849773` (22:35): the host's exit decision in `GameUi`, export-log lines on a failed close, a trace check for half-year turns;
- `39621be` (22:46): an end-to-end research test, a final-line-feed removal test, the README recovery note and a D-086 note;
- `a8ca9ef` (22:52), `e974f61` (23:13) and `ab5ec46` (23:17): the main report and the lane report, with the mutant kill
  records and an ADR-059 amendment listed as a PROPOSAL;
- `2ed2a6a` (00:04) and `8610ebd` (00:05): the validation results filled into both reports, and the reports' figures checked
  against the tree (docs only).

The implementation agent finished at 00:08 UTC with head `8610ebd`, and the clone has no uncommitted entries. Lane diff from
`51ab032`: 44 files, +8,636/−111. **Verification round 1**, pinned to `8610ebd`, has been running since 00:08 UTC.

Lane claims, from its handback. They are NOT yet verified; round 1 is checking them. The main report is
`docs/m6-plan/run-record-research-and-exit-2026-11.md` in the lane.
- **DE1:** telemetry/v6 carries each turn's research rows for every holder (the player, every AI polity, every city-state)
  and the Civics bank rows. The codebook goes to v6, and the workbook gains a ninth sheet, Research. An older session exports
  with a "not recorded" row.
- **DE2:** the close runs its five steps, unchanged and in order, on a worker thread while the window draws a saving screen. A
  second close request starts nothing, and the workbook is written whole. The export is streamed. On the Director's 173 MB
  files it took 12.5–13.3 s with a 1,290 MB peak before, and 8.8–9.7 s with 698 MB after, with identical workbook bytes.
- **Found by measurement:** every session past turn 1270 lost its workbook. From world year 5920 the trace holds half-year
  years, which the frozen kernel's `SessionTrace.Parse` refuses. The export now checks the trace itself. Continue last game,
  `sim replay` and `sim inspect` still refuse such sessions. The fix, proposal P1, is a kernel change and needs the
  Director's sign-off.
- **Validation:** Tier 3 passes apart from the known charters false positive on its base. The two full suites then ran under
  the lock: Sim.Tests 3,438 passed, 0 failed, 36 skipped; Sim.Ui.Tests 1,294 passed, 0 failed. The smoke passed, and 18 of
  18 bounded mutants were killed by semantic tests. The lane makes proposals P1–P6 and asks no question.

**Paused**, in this order of resumption (each with `resumeFromRunId`: finished agents replay from cache, and the interrupted
agent restarts and continues from its clone):

| # | Item | Workflow run | Script and args |
|---|---|---|---|
| 1 | G2 final: scenes A–E built, captures in progress | `wf_d6eb0226-44e` | `m7-g2-v2.js`, args `{"base": "1601c74"}` |
| 2 | Final-round verification (findings only) of lanes U, D, MOR, AP, MOR2, CB, MOR3 (`95446ff`) and RT (`943b43f`) | `wf_9b24d7c6-337` | `verify-final-rounds`: run its lanes one at a time, with MOR3 and RT added |
| 3 | Report draft | `wf_2a771902-a97` | `pass-report-draft` |

Lanes MOR3 and RT each ran one verification round and one fix round, and neither round found a blocker or major finding (MOR3:
3 minor and 5 nit; RT: 4 minor and 2 nit). So their fix rounds are unverified, like the final rounds of the six lanes above.

**Checkpoint 2026-11-12 01:20 UTC** (in [wip/](wip/); the previous one was at 23:20):
- **Lane commits after `51ab032`, as bundles:**
  - `RR-wip.bundle` (`8610ebd`, the lane's implementation head; it was `ab5ec46` at 23:20 and `0a631db` from 11:12 to 21:20);
  - `RT-wip.bundle` (`943b43f`, the finished lane's head): retired after the 21:20 checkpoint, because the lane is integrated
    as `dc19857`. It remains in this branch's history at `f94a9b4`;
  - `MOR3-wip.bundle` (`a8ef60a`): retired at 13:45 UTC, because the lane is integrated as `e052b51`.

  Each bundle needs `51ab032`, so restore it into a clone of this branch:
  `git fetch docs/m7-plan/pass-state/wip/<KEY>-wip.bundle HEAD:lane/<KEY>`, then `git checkout lane/<KEY>`. The bundles carry
  only `HEAD`, so the `git clone -b lane/<KEY> …` form this note gave at 11:12 fails ("Remote branch lane/MOR3 not found").
  It was corrected at 13:20 after a restore test.
- **Uncommitted edits of the paused clones.** A binary patch against the head in its file name:
  `g2-scenes-uncommitted-0f8ae2f.patch` (4 files). It is byte-identical to a fresh snapshot taken at 01:20, so unchanged since
  13:20. Apply it with `git apply --binary <patch>` on its head. It snapshots work stopped mid-step, not a finished state.
- **Retired at 23:20:** `RR-uncommitted-0a631db.patch`. Lane RR resumed from those edits and committed them (MEASURED: its
  16 files, patched onto `0a631db`, are byte-identical to `955d78c`'s; at `ab5ec46`, 14 are unchanged and 2 have grown,
  `SessionCloseTests.cs` by 207 lines and `SessionWorkbook.cs` by 39 with 1 removed). The patch remains in this branch's
  history at `46c7a9d`.
- **Retired at 15:21:** `RT-uncommitted-950d6d1.patch`. Lane RT resumed from that edit and committed it. The file as patched at
  `950d6d1` differs from `4de0682`'s only by 81 lines that `4de0682` adds. The patch remains in this branch's history at
  `afc7118`.
- **G2 final, scenes stage**, unchanged since 13:20 (head `0f8ae2f`). `g2-scenes-0f8ae2f.patch` holds the stage's committed work
  since trunk `21a181e`, as a binary patch of `prototypes/m7-gfx2`. It applies onto this branch's tree.
- **Restore test (01:20, MEASURED).** In a fresh clone of this branch (`39cf76a`):
  - the RR bundle verified and fetched to `8610ebd`, with the lane clone's tree (`0174573`);
  - the G2 committed patch alone gives a `prototypes/m7-gfx2` tree equal to `0f8ae2f`'s (`c5c617a`);
  - both G2 patches give a tree equal to the scenes clone's working state (`a0309d3`).

**CI on this branch.** From `0929d28` to `afc7118`, CI's build-and-test job failed at the charter check and skipped its build and
test steps (§1, `93f2d8a`). Run 970 on `8e4a778` (MEASURED, read 16:30 UTC) succeeded in every job: build-and-test's 22 steps,
including the whole Sim.slnx Test step (49 min) and the read-only proof; determinism; determinism-xproc; calibration. `ui-artifact`
also succeeded. Every run since has succeeded in every job too (MEASURED, read 2026-11-12 01:23 UTC): runs 971 to 976, on
`a2025c0`, `b90078b`, `f94a9b4`, `dc19857`, `46c7a9d` and `39cf76a`, each with build-and-test's 22 steps, determinism,
determinism-xproc and calibration. Run 974 is the first on a `Sim.*` change of this pass (lane RT): its whole Sim.slnx Test
step took 48 min and its determinism harness 17 min. `calibration-nightly` was skipped, as it runs only on schedule.
`ui-artifact` runs 476 to 481 succeeded. The orchestrator reads the head's CI result after each push.

## 3. Queue after the items in flight

1. As each item in §2 finishes, the orchestrator verifies it (§4 step 2, or the G2 subtree method), commits, pushes, and
   updates this README.
2. The pass's full report: 10 sections, DR52. It is built from [notes/final-report-notes.md](notes/final-report-notes.md), in
   the style of `docs/m7-plan/m7-execution-pass-report.md`.

If the pass stops, restart any item in §2 that has not been pushed with its listed workflow and brief. Use `base` set to this
branch's head; for G2, use `"from": "final"`.

## 4. How to resume in a fresh session or container

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

- READING A (War Sandbox: run or refuse a battle the AutoResolver cannot model).
- D-085's readings: fighting judged per battle (applied) or per formation; any attack (applied) or only an attack that
  deals damage.
- Lane AP: Q-AP1 who defends; Q-AP2 ambush battles and the deployment thirds; Q-AP3 the Standoff extension; Q-AP4 equal
  sides in both resolvers.
- Lane CB:
  - E1 for M7.6;
  - READING CB-1;
  - a performance cap for uncapped families (cavalry, siege, air defence, missiles, reconnaissance);
  - READING CB-4.
- Earlier open items: the two thirds-violating sandbox scenarios; R6; lane U's U-Q11, the decision-tray placement and its
  queue line; the DA3 calibration numbers (A-38, M7.14); lane P's acceptance (D-079 Q4).
- Lane MOR3 asks no question, since D-085 §4 delegated its design. It leaves two PROPOSALS (H-MOR3 §13):
  - a lane P parity item: walls in the AutoResolver's contact and line of fire. Today, on a walled setup, the TacticalResolver
    has no fighting while the AutoResolver fights from step 0.
  - for M7.15: `Fought` as a required hand-off input. `SupportBattle.Fought` defaults to true, which is fail-open.
- Lane RT ([research-tree-fixes-2026-11.md](../../m6-plan/research-tree-fixes-2026-11.md) §4–§5, H-RT §10.7):
  - Longbow: a screenshot of the detail pane as seen in play, and the build that was run.
  - N1: may the inspector's ENABLED BY and WHY LOCKED labels (`InfoQuery`) be corrected under D-084? That is a `Sim.Core/State`
    change outside the estimate.
  - The content proposals H1, H2, B1 and K1 of the date review; each changes a prerequisite, a definition or a D-047 date.
  - The fix round's three READINGS: no estimate for a civic not yet open; hovered incoming edges cased alike; the "either" head.
  - PROPOSALS: N2 (the hover path's dependent edges), N3 (spine lane spacing), N4 (subheadings under REQUIRES), N6 (a
    route-aware estimate for a node not yet open).
- Lane RR (from its handback; its verification round is still running): no question, and six PROPOSALS.
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
| `args/*.json` | Every lane brief this pass ran (U, D, MOR, AP, MOR2, CB, MOR3, RT) and the one in flight (RR, with its resume note); `G2.json` holds the G2 args |
| `notes/final-report-notes.md` | The orchestrator's running facts per commit, incident and decision: the source of the final report |
| `notes/orchestration-log.md` | The queue and pace log |
| `notes/director-messages/` | Verbatim working copies of the Director's messages cited by D-082..D-086 |

The workflow scripts and notes use this session's absolute paths: `/home/user/civDemo`, `/home/user/m7w`, `/home/user/g2w`,
and the scratchpad under `/tmp/claude-0/…`. Adjust them in a new environment.
