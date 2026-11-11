# M7 pass state: what is done, what is in flight, and how to resume

**Living document, rewritten by the orchestrator at each step of the pass.** Last update: 2026-11-11, after G2 segment 3 (09cbc9d). Its purpose (Director, 2026-11-11: "ensure everything is on the repo so that just in case we stop this pass in
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

Full power since 2026-11-11 ~09:10 UTC (Director: "go full power now"). Four workflows run at once; Tier-3 full suites of the
two `Sim.*` lanes are serialized by `flock` on the scratchpad's `m7c/fullsuite.lock`.

| Item | Workflow run | Base | Brief |
|---|---|---|---|
| G2 final: scenes A–E, critic loop, verify, AFTER package, visual review | `wf_d6eb0226-44e` (resume, args `{"base": "1601c74"}`) | trunk `21a181e` | [workflows/m7-g2-v2.js](workflows/m7-g2-v2.js) |
| Lane MOR3 (D-085) | `wf_edd83092-8a1` | `51ab032` | [args/MOR3.json](args/MOR3.json) |
| Lane RR (D-086) | `wf_b1a17355-cee` (Sim variant) | `51ab032` | [args/RR.json](args/RR.json) |
| Lane RT (D-084) | `wf_2aee7a53-1fc` (Sim variant) | `51ab032` | [args/RT.json](args/RT.json) |

G2 segment 3 is on this branch (`94fa0dc` kit, `09cbc9d` render2 merged with it); its WIP bundles were retired. If the G2
trunk is lost, recreate it from this branch: a new `g2/trunk` whose `prototypes/m7-gfx2` equals this branch's (trunk `21a181e`'s
subtree, `ea87bc3e`), then resume with `"from": "final"`. Lanes keep WIP bundles in the scratchpad (`m7c/bundles/<KEY>-wip.bundle`);
the two-hourly checkpoint copies them here.

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

The workflow scripts and notes use this session's absolute paths: `/home/user/civDemo`, `/home/user/m7w`, `/home/user/g2w`,
and the scratchpad under `/tmp/claude-0/…`. Adjust them in a new environment.
