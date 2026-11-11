# M7 pass state: what is done, what is in flight, and how to resume

**Living document, rewritten by the orchestrator at each step of the pass.** Last update: 2026-11-11 09:02 UTC (WIP checkpoint). Its purpose (Director, 2026-11-11: "ensure everything is on the repo so that just in case we stop this pass in
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

Supporting material committed with this README:
- **Research:** the reference-morale research dossier and its claim files, [../research/ref-morale-2026-11-10/](../research/ref-morale-2026-11-10/dossier.md). It is the provenance of the morale proposal's Annex A; the proposal and H-MOR2 cite it by its old scratchpad path, `…/scratchpad/m7c/research/run1`.
- **Playtest:** the Director's failing playtest session 20261108-155547-a1 (four record files and the workbook rebuilt from them), [../../m6-plan/playtest-session-20261108-155547-a1/](../../m6-plan/playtest-session-20261108-155547-a1/README.md).
- **Director's messages:** verbatim working copies of the Director's messages of 2026-11-10 and 2026-11-11, [notes/director-messages/](notes/director-messages/). D-082..D-086 cite them by scratchpad path. The older working copies (D-079..D-081) were lost with an earlier container, and those records' quoted text is the authority.

## 2. In flight

- **G2 segment 3.** Workflow script [workflows/m7-g2-v2.js](workflows/m7-g2-v2.js), run `wf_d6eb0226-44e`, args
  `{"base": "1601c74", "stopAfter": "stage3"}`.
  - **kit stage:** complete, branch `g2/kit` at `85e918f`: era kit, the hero aspis, combat animation, ancient outfits.
  - **render2 stage:** running, branch `g2/render2`; checkpoint 2026-11-11 09:02 UTC at `82c8198`. Shadows, ambient occlusion,
    contact shadows, the GPU-skinned crowd, occlusion culling and TAA are implemented; tests, costs, captures and the report
    are under way.
  - **Saved here:** both branches are in [wip/g2-kit-and-render2-wip.bundle](wip/g2-kit-and-render2-wip.bundle) (7.2 MB). It
    restores into any clone that has `1601c74`. Uncommitted edits in the render2 clone are not in it.
  - **Later checkpoint:** [wip/g2-render2-inc-82c8198.bundle](wip/g2-render2-inc-82c8198.bundle) (84 KB) holds `g2/render2`'s
    commits after `70e46d0`. Restore the base bundle first, then `git fetch` this one into the render2 clone.
  - **Restore:**
    `git clone -b g2/kit wip/g2-kit-and-render2-wip.bundle /home/user/g2w/kit` and
    `git clone -b g2/render2 wip/g2-kit-and-render2-wip.bundle /home/user/g2w/render2`. Both need `1601c74` in the clone's
    object store, so fetch this branch first.

## 3. Queue (one agent at a time, no gap between items; Director 2026-11-09)

| # | Item | How to run it |
|---|---|---|
| 1 | G2 segment 3: finish render2, then the segment's integration into `g2/trunk`, then integration onto this branch | Resume the G2 workflow with the args above. In a fresh container, see §4. |
| 2 | Lane MOR3: without fighting, no morale change (D-085) | Lane workflow [workflows/m7-lanes-v2.js](workflows/m7-lanes-v2.js) with [args/MOR3.json](args/MOR3.json). Set `base` to the branch head, and first add d085 and lane CB to the script's shared context. |
| 3 | Lane RR: research in the run record; the close keeps the workbook (D-086) | [args/RR.json](args/RR.json), with a lane-script variant whose shared-context rule "nothing in Sim.* changes" is replaced by the D-084/D-086 exception |
| 4 | G2 final: scenes A–E, the critic loop, verification, the Victus AFTER package and the visual-quality review | Resume the G2 workflow with no `stopAfter`. In a fresh container use `"from": "final"`. |
| 5 | Lane RT: research-tree audit fixes (D-084) | [args/RT.json](args/RT.json), with the same lane-script variant as RR |
| 6 | The pass's full report: 10 sections, DR52 | From [notes/final-report-notes.md](notes/final-report-notes.md), in the style of `docs/m7-plan/m7-execution-pass-report.md` |

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
   - `/home/user/g2w/trunk`: branch `g2/trunk` with history from `1601c74`. The trunk at the last integration (`7dd134a`) is
     in the bundle's history.
   - `/home/user/g2w/assets`: run `prototypes/m7-gfx2/scripts/fetch-assets.sh`, which uses pinned sha256 sums and fetches
     about 296 MB from GitHub raw, media and codeload hosts, PyPI and npm.
   - headless rendering: `SDL_VIDEODRIVER=offscreen LIBGL_ALWAYS_SOFTWARE=1`.

   Restore the stage clones from the bundle (§2), then run with `from` set to the first unfinished segment. G2 integrates onto
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

## 6. What this folder holds

| Path | What |
|---|---|
| `workflows/m7-lanes-v2.js` | The lane workflow (implement, then adversarial verify, then fix, up to two rounds), as last run (lane CB) |
| `workflows/m7-g2-v2.js` | The G2 graphics workflow (segments stage1, stage2, stage3, final) |
| `workflows/ref-morale-research.js` | The reference-morale research workflow (sequential sweeps, a provenance check, the dossier) |
| `args/*.json` | Every lane brief this pass ran (U, D, MOR, AP, MOR2, CB) and the queued ones (MOR3, RR, RT); `G2.json` holds the G2 args |
| `notes/final-report-notes.md` | The orchestrator's running facts per commit, incident and decision: the source of the final report |
| `notes/orchestration-log.md` | The queue and pace log |
| `notes/director-messages/` | Verbatim working copies of the Director's messages cited by D-082..D-086 |
| `wip/g2-kit-and-render2-wip.bundle` | Git bundle of `g2/kit` (85e918f) and `g2/render2` (70e46d0), prerequisite `1601c74` |
| `wip/g2-render2-inc-82c8198.bundle` | Incremental bundle: `g2/render2` from `70e46d0` to `82c8198` |

The workflow scripts and notes use this session's absolute paths: `/home/user/civDemo`, `/home/user/m7w`, `/home/user/g2w`,
and the scratchpad under `/tmp/claude-0/…`. Adjust them in a new environment.
