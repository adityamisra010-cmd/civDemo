# Pass-state snapshot: 2026-11-12T16:23:22Z

Written by `docs/m7-plan/pass-state/tools/snapshot.sh` every 30 minutes while work is in flight, and committed with
`[skip ci]`. It holds every piece of work that is not yet on this branch as a commit, so that a stopped session or a lost
container costs at most the work since this time. The finished work is in the branch itself (README §1).

Branch head when written: `fa80ab3`. In flight: g2 trunk f6ea1c1 g2 polish1 f6ea1c1 +13 int KT 821039e.

| File | What | Head | Made with | Restore |
|---|---|---|---|---|
| g2-trunk.patch | G2 trunk /home/user/g2w/trunk (branch g2/trunk) | f6ea1c1 | git diff --binary 21a181e f6ea1c1 -- prototypes/m7-gfx2 | `git apply --binary` on this branch's tree (its prototypes/m7-gfx2 equals 21a181e's) |
| g2-polish1-uncommitted.patch | G2 stage clone /home/user/g2w/polish1: uncommitted edits (13 paths) | f6ea1c1 | temporary index | after g2-trunk.patch |
| int-KT-uncommitted.patch | orchestrator clone /home/user/int/KT: uncommitted edits | 821039e | temporary index | `git apply --binary` on 821039e |
| journals/*.jsonl | every workflow run's journal | — | copy | read, to see which agents finished and what they returned |

## Restore test (in a fresh clone of the branch, at this snapshot)

- g2-trunk.patch on this branch's tree: PASS + g2-polish1-uncommitted.patch PASS
- int-KT-uncommitted.patch on 821039e: PASS

## How to resume from this snapshot in a new session

1. Run `./scripts/bootstrap.sh`; read `CLAUDE.md`, `docs/current-state.md` and `docs/m7-plan/pass-state/README.md`.
2. **G2:** cut a trunk from this branch (`git clone` it to `/home/user/g2w/trunk`, branch `g2/trunk`), apply
   `g2-trunk.patch` with `git apply --binary` and commit. For a stage clone with its own patch, apply that patch instead,
   then its `-uncommitted` patch. Re-fetch the assets (`prototypes/m7-gfx2/scripts/fetch-assets.sh`). Re-run the G2
   workflow from README §2 with `from` set to the segment in flight; its agents continue from a clone that exists.
3. **Lanes:** `git fetch <KEY>-wip.bundle HEAD:lane/<KEY>` in a clone of this branch at `/home/user/m7w/<KEY>`, check
   it out, apply `<KEY>-uncommitted.patch`, and re-run the lane workflow with the brief in `args/<KEY>.json` plus a resume
   note. The journals show which of its agents had finished.
4. **Orchestrator clones:** apply `int-<NAME>-uncommitted.patch` on its head and re-run the validation the README names.
