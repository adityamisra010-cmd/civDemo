# Pause of 2026-11-13 13:12 UTC: everything needed to resume the third full-power hour

The Director asked at about 12:02 UTC on 2026-11-13: "go full power for one hour". Three workflows ran in
parallel: G2's polish round 3, lane OC's completion stage and lane RT2.

The scheduled 13:03 UTC STOP message had not fired by 13:11 UTC, so the orchestrator stopped the work itself at
13:12 UTC and deleted the late message. The final snapshot is `c680ba4` (13:14 UTC; restore test 4 PASS).

This folder copies into the repository the work in progress that lived only in the session's clones, scratchpad
or RAM disk. It contains no new work. It builds on [`../pause-2026-11-13/`](../pause-2026-11-13/MANIFEST.md),
whose G2 history bundle ends at `4f8b953`. [`../tools/snapshot.sh`](../tools/snapshot.sh) replaces `../wip/` on
each run and never touches this folder. Each patch and lane bundle here is byte-identical to the copy in snapshot
`c680ba4`'s `wip/`.

| File | What | Bytes | SHA-256 |
|---|---|---|---|
| `g2-polish3-4f8b953-9dbf72d.bundle` | Polish round 3's four commits of this hour, `4f8b953..9dbf72d`: 153b688, 20954de, a4ab145, 9dbf72d. Its prerequisite, `4f8b953`, is the head of `../pause-2026-11-13/g2-history-4f8b953.bundle`. | 26,630 | `1dad3fe43862724f340bdae1d150f217f5a389ba8c4593c1b70f66d3f120ea14` |
| `g2-polish3-uncommitted-9dbf72d.patch` | Polish round 3's uncommitted edits on `9dbf72d`: PROVENANCE, report draft, review-sheet script, and the review captures `reports/captures/polish3/`. | 120,200 | `99f78d68bdc6818cc10cf7ca57956cd4424a4d72ba16e15338f082b2cc21fc9e` |
| `OC-f19d255.bundle` | Lane OC, `3f4f8e1..f19d255`: impl, fix round 1, and the completion stage's items 8, 5 and 6. Its prerequisite, `3f4f8e1`, is on this branch. | 124,337 | `4eb300971e99abc6fa7bf66b1c92ff1366c4151bd4751c01fac252757ba4c6e3` |
| `OC-uncommitted-f19d255.patch` | Lane OC's item 7 in progress, the player's capture choice: 7 modified files in Proto.M7 Events and War/Outcomes, plus the new `CaptureChoiceTests.cs`. | 43,735 | `a16c8a3bd094213d61705a306a99518fe38bc4fcc7dca44c515662322b907abc` |
| `RT2-50e4642.bundle` | Lane RT2, `3bdea00..50e4642`: f4b0e8d (the date review's H1, H2, B1, K1, W1 and W2 applied, D-089 DH7) and 50e4642 (inspector prerequisite lines keep their grouping, DH4). Its prerequisite, `3bdea00`, is on this branch. | 35,760 | `7ddf6f6dc4f127b1b42ee9708f25a384f023cc84b1b6c698af00cc60e5045b23` |
| `RT2-uncommitted-50e4642.patch` | Lane RT2's polish items in progress (N2-N6): 6 modified files in Sim.Core/State, Sim.Ui/Progression and Sim.Ui.Tests, plus the new `ResearchScreenPolishTests.cs`. | 55,969 | `f3e5f9b641c452fc593a911ef2e543c9735767b50865dc67bca2caea784b1ee6` |
| `RT2.launch.json` | The exact args of run `wf_792dc232-e18`: `args/RT2.json` with base `3bdea00`. | 4,378 | `39bcc16daa48c49b4246e090f03a1f2a1cb2c73a6d8d4a2dc51ef3b6bbbdfeb1` |
| `OC.launch.json` | The exact args of run `wf_42ee07c9-d2f`; the same file as in `../pause-2026-11-13/`. | 8,943 | `e83bacf0dbd35abceebfabb1bdd055a2140ebc41c61bb86defd32e4ade8d239c` |
| `journals/wf_d6eb0226-44e.jsonl` | The G2 run's journal. | 276,682 | `f569c8dd17e753cab7b1f6c67a2ecabbd3833c9b7e2c2e3534b2aee9d0f3846c` |
| `journals/wf_42ee07c9-d2f.jsonl` | Lane OC's journal: impl, verification round 1 and fix round 1 finished; the completion stage started. | 43,130 | `3e097985b18a78633e0c406973ea88fe966763346e5a6b22bbc5ba40554bb4ea` |
| `journals/wf_792dc232-e18.jsonl` | Lane RT2's journal: launched, impl started. | 184 | `5f19044649c5b16bce9e1fef501cf4624fcde7ffdd735601d5a8ba5dce6cb20b` |

**Not kept**, because the stage that made them remakes them:
- the full-size polish-3 captures (243 MB, on the RAM disk);
- build outputs;
- RT2's two read-only worktrees, `RT2-base` at `3bdea00` and `RT2-t2` at `50e4642` (both clean);
- the scratchpad's logs.

## Restore

**A. The container kept its disk** (`/home/user/g2w/trunk` and `/home/user/m7w/OC` exist; only `/dev/shm` was
emptied): run `bash docs/m7-plan/pass-state/tools/ramdisk-restore.sh`.
- It recreates the RAM-disk folders behind the symlinks.
- It restores `/home/user/g2w/polish3` at `9dbf72d` and `/home/user/m7w/RT2` at `50e4642`, each with its uncommitted edits.
- It takes the bundles and patches from the scratchpad: `m7gfx2/bundles/polish3-9dbf72d.bundle`, the full history `1601c74..9dbf72d`, and `m7c/bundles/RT2-50e4642.bundle`.
- It is idempotent, deletes nothing, and was tested on a scratch RAM root: heads, uncommitted paths and source trees identical to the live clones.

Lane OC's clone is on the disk; its uncommitted item-7 edits are in it as left.

**B. A fresh container** (no `/home/user/g2w`, no scratchpad):

1. Run `./scripts/bootstrap.sh` to install the .NET 10 SDK.
2. If the new disk is short of space, first link each clone path below to an empty folder under `/dev/shm/ram`, as on 2026-11-13.
3. Run from `/home/user/civDemo` on `claude/new-session-yavghm`:

```bash
P1=$PWD/docs/m7-plan/pass-state/pause-2026-11-13
P2=$PWD/docs/m7-plan/pass-state/pause-2026-11-13b
G2W=/home/user/g2w; M7W=/home/user/m7w
git clone -q /home/user/civDemo "$G2W/trunk"
git -C "$G2W/trunk" fetch -q "$P1/g2-history-4f8b953.bundle" HEAD:refs/remotes/bundle/g2-polish3
git -C "$G2W/trunk" checkout -q -b g2/trunk f677707
git clone -q "$G2W/trunk" "$G2W/polish3"
git -C "$G2W/polish3" fetch -q "$P1/g2-history-4f8b953.bundle" HEAD:refs/remotes/bundle/g2-polish3-4f8b953
git -C "$G2W/polish3" fetch -q "$P2/g2-polish3-4f8b953-9dbf72d.bundle" HEAD:g2/polish3
git -C "$G2W/polish3" checkout -q g2/polish3
git -C "$G2W/polish3" apply --binary "$P2/g2-polish3-uncommitted-9dbf72d.patch"
git clone -q /home/user/civDemo "$M7W/OC"
git -C "$M7W/OC" fetch -q "$P2/OC-f19d255.bundle" HEAD:lane/OC
git -C "$M7W/OC" checkout -q lane/OC
git -C "$M7W/OC" apply --binary "$P2/OC-uncommitted-f19d255.patch"
git clone -q /home/user/civDemo "$M7W/RT2"
git -C "$M7W/RT2" fetch -q "$P2/RT2-50e4642.bundle" HEAD:lane/RT2
git -C "$M7W/RT2" checkout -q lane/RT2
git -C "$M7W/RT2" apply --binary "$P2/RT2-uncommitted-50e4642.patch"
```

4. Check the result:
   - `g2/trunk` is `f677707`;
   - polish3's HEAD is `9dbf72d`, with 4 uncommitted paths;
   - lane OC's HEAD is `f19d255`, with 8 uncommitted paths;
   - lane RT2's HEAD is `50e4642`, with 7 uncommitted paths.

## Resume, in this order (or in parallel, as in the third hour)

1. **G2 final.**
   - Workflow `resumeFromRunId: wf_d6eb0226-44e`, script `../workflows/m7-g2-v2.js`, args `{"base": "1601c74"}`.
   - Polish round 3 restarts from its clone. It is in its last stage: builds, test groups, costs, report, commit, bundle.
   - Then come its integration into the trunk (seg7), the verifier, and the AFTER package with the visual quality review.
2. **Lane OC.**
   - Workflow `resumeFromRunId: wf_42ee07c9-d2f`, script `../workflows/m7-lanes-v3.js`, args `OC.launch.json`.
   - Replayed from cache: impl, verification round 1 and fix round 1.
   - The completion stage `complete:OC:r1` restarts from the clone, finishes item 7 and its closing work, then verification round 2 and fix round 2 run.
3. **Lane RT2.**
   - Workflow `resumeFromRunId: wf_792dc232-e18`, script `../workflows/m7-lanes-v3-sim.js`, args `RT2.launch.json`.
   - Its implementation agent restarts from the clone: items 1-3 are partly committed, the polish edits are uncommitted, and validation, mutants and the report remain.
4. **The rest of the queue.** Lanes BN and AR (after OC), lane RX (on a quiet machine), the final-round verification, then the report: [`../README.md`](../README.md) queue rows 1b-3.
