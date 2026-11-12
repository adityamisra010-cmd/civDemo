# Pass-state snapshot: 2026-11-12T17:54:26Z

Written by `docs/m7-plan/pass-state/tools/snapshot.sh` every 30 minutes while work is in flight, and committed with
`[skip ci]`. It holds every piece of work that is not yet on this branch as a commit, so that a stopped session or a lost
container costs at most the work since this time. The finished work is in the branch itself (README §1).

Branch head when written: `818e1ba`. In flight: g2 trunk f6ea1c1 g2 polish1 8fb760d +1.

| File | What | Head | Made with | Restore |
|---|---|---|---|---|
| g2-trunk.patch | G2 trunk /home/user/g2w/trunk (branch g2/trunk) | f6ea1c1 | git diff --binary 21a181e f6ea1c1 -- prototypes/m7-gfx2 | `git apply --binary` on this branch's tree (its prototypes/m7-gfx2 equals 21a181e's) |
| g2-polish1.patch | G2 stage clone /home/user/g2w/polish1 (branch g2/polish1), commits not yet in the trunk | 8fb760d | git diff --binary 21a181e 8fb760d -- prototypes/m7-gfx2 | instead of g2-trunk.patch |
| g2-polish1-uncommitted.patch | G2 stage clone /home/user/g2w/polish1: uncommitted edits (1 paths) | 8fb760d | temporary index | after g2-polish1.patch |
| journals/*.jsonl | every workflow run's journal | — | copy | read, to see which agents finished and what they returned |

## Restore test (in a fresh clone of the branch, at this snapshot)

- g2-polish1.patch on this branch's tree: PASS + g2-polish1-uncommitted.patch PASS
- g2-trunk.patch on this branch's tree: PASS

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
