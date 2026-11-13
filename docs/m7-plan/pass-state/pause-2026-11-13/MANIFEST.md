# Pause of 2026-11-13: everything needed to resume the pass

The Director stopped work for a day at about 09:41 UTC on 2026-11-13: "okay pause everything. save all you can. we are
stopping work for a day". It came right after the scheduled 09:34 UTC stop of the second hour at full power.
Everything had stopped by 09:36 UTC. The final snapshot is `d6092cd` (09:43 UTC).

This folder copies into the repository every piece of work in progress that otherwise lived only in the session's
clones, its scratchpad or its RAM disk. It contains no new work. [`../tools/snapshot.sh`](../tools/snapshot.sh) replaces
`../wip/` on each run and never touches this folder.

| File | What | Bytes | SHA-256 |
|---|---|---|---|
| `g2-history-4f8b953.bundle` | The whole G2 history: 101 commits, `1601c74..4f8b953`. It contains `g2/trunk` at `f677707` and `g2/polish3` at `4f8b953`, which is polish round 3's five commits on `f677707`. Its prerequisite, `1601c74`, is on this branch. | 8,782,031 | `872bdcabade13fe53fb7d17306c3c7742c9715022429583b76cf5b2b4c8ab64d` |
| `g2-polish3-uncommitted-4f8b953.patch` | Polish round 3's uncommitted edits on `4f8b953`: the report draft `prototypes/m7-gfx2/reports/polish3.md`, untracked. | 11,558 | `b31e04a0335a6465534a4444eb4be2a06f8f51ef0c9b5c98954f987c9d00f9f1` |
| `OC-816b4b2.bundle` | Lane OC, `3f4f8e1..816b4b2` (impl, then fix round 1). Its prerequisite, `3f4f8e1`, is on this branch. | 71,450 | `0ee0c8573dc79e772fc01da5c2dd041d2d733d48028e81757b18cd536df5f5d0` |
| `OC.launch.json` | The exact args of run `wf_42ee07c9-d2f`: `args/OC.json` with base `3f4f8e1`. Identical args make the resume replay the finished agents from cache. | 8,943 | `e83bacf0dbd35abceebfabb1bdd055a2140ebc41c61bb86defd32e4ade8d239c` |
| `verify-OC-r2b-uncommitted-816b4b2.patch` | Verification round 2's in-progress mutant (`AutoResolver.cs`, one line), captured when the run stopped. No verdict was returned, so it is **not a finding**. Verification round 2 restarts on resume. | 1,072 | `96eddb2173b0f176624c0e1dda6f5a874943d31588463b333cc69c7749fa8a47` |
| `journals/wf_d6eb0226-44e.jsonl` | The G2 run's journal: every finished agent's result, up to critic round 3. | 276,514 | `8d4ce1b4a87d6de49b1304b5b7a4d85fb6d0ca0d0ce2cacae64dc178db232bdd` |
| `journals/wf_42ee07c9-d2f.jsonl` | Lane OC's journal: impl, verification round 1 and fix round 1. | 42,960 | `ac1a44a96043e9aaf2aef388c05acadac685ca63be4e22ec6425a053e8fed9ae` |

**Not kept**, because the stage that made them remakes them:
- the partial polish-3 captures (scenes a, b, c and d-shown; d-cutaway cut off), which were on the RAM disk;
- build outputs;
- the scratchpad's closing logs and mutant result files, whose numbers are in the lane reports;
- the earlier rounds' full-size capture PNGs, whose review JPEGs are in the G2 history.

## Restore

**A. The container kept its disk** (`/home/user/g2w/trunk` exists; only `/dev/shm` was emptied): run
`bash docs/m7-plan/pass-state/tools/ramdisk-restore.sh`.
- It recreates the RAM-disk folders behind the symlinks.
- It restores `/home/user/g2w/polish3`: branch `g2/polish3` at `4f8b953` plus the report draft.
- It takes the bundle and patch from the scratchpad.
- It is idempotent and deletes nothing.

**B. A fresh container** (no `/home/user/g2w`, no scratchpad):

1. Run `./scripts/bootstrap.sh` to install the .NET 10 SDK.
2. Rebuild the clones from this folder. Run from `/home/user/civDemo` on `claude/new-session-yavghm`:

```bash
P=$PWD/docs/m7-plan/pass-state/pause-2026-11-13
G2W=/home/user/g2w; M7W=/home/user/m7w
git clone -q /home/user/civDemo "$G2W/trunk"
git -C "$G2W/trunk" fetch -q "$P/g2-history-4f8b953.bundle" HEAD:refs/remotes/bundle/g2-polish3
git -C "$G2W/trunk" checkout -q -b g2/trunk f677707
git clone -q "$G2W/trunk" "$G2W/polish3"
git -C "$G2W/polish3" fetch -q "$P/g2-history-4f8b953.bundle" HEAD:g2/polish3
git -C "$G2W/polish3" checkout -q g2/polish3
git -C "$G2W/polish3" apply --binary "$P/g2-polish3-uncommitted-4f8b953.patch"
git clone -q /home/user/civDemo "$M7W/OC"
git -C "$M7W/OC" fetch -q "$P/OC-816b4b2.bundle" HEAD:lane/OC
git -C "$M7W/OC" checkout -q lane/OC
```

3. Check the result:
   - `g2/trunk` is `f677707` in the trunk clone;
   - the polish3 clone's HEAD is `4f8b953`, and `git status` shows only `?? prototypes/m7-gfx2/reports/polish3.md`;
   - lane OC's HEAD is `816b4b2`.

## Resume, in this order

1. **G2 final.**
   - Workflow `resumeFromRunId: wf_d6eb0226-44e`, script `../workflows/m7-g2-v2.js` (identical to the run's own copy), args `{"base": "1601c74"}`.
   - Replayed from cache: everything up to critic round 3.
   - Run live:
     - polish round 3, which restarts from its clone (five commits plus the report draft; it had reached its capture stage);
     - its integration into the trunk (seg7);
     - the verifier;
     - the AFTER package and the visual quality review.
   - If the session's own journal is gone, `journals/wf_d6eb0226-44e.jsonl` holds every finished result to hand-author a continuation from.
2. **Lane OC.**
   - Workflow `resumeFromRunId: wf_42ee07c9-d2f`, script `../workflows/m7-lanes-v3.js`, args `OC.launch.json`.
   - Replayed from cache: impl, verification round 1 and fix round 1.
   - Fix round 1 fixed 10 of the 11 findings. It deferred OC-R1-02: brief items 5–8 are **not built**:
     - (5) roles, including two empires and their allies on one side;
     - (6) the ambush on the map;
     - (7) the player's capture choice;
     - (8) the Ambush and Amphibious test battles rewritten inside the thirds.
   - Until item 8 is built, those two battles are not deployment-legal, and lane AR must not use them as such.
   - The orchestrator therefore adds an implementation stage for items 5–8 before verification round 2. Verification and fix round 2 then run on the complete lane.
3. **The rest of the queue.** Lanes BN, AR, RT2 and RX, the final-round verification, then the report draft: [`../README.md`](../README.md) queue, rows 1b–3.
