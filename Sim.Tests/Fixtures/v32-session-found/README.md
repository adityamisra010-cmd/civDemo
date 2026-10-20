# A batch-2 (schema v32) session fixture (D-054, fix round 1)

These three files are a REAL session record written by the batch-2 build:
de86ace plus the uncommitted batch-2 tree (`scratchpad/base-b2.tar`, manifest verified by
`mkclone.sh`). That build's worlds were flat square rasters; the cylinder arrived with schema v33.

- `session-7166834e1157f6bc.json` — the manifest (`"schemaVersion": 32`, seed 42, canonical size)
- `orders-7166834e1157f6bc.bin` — the order log
- `trace-7166834e1157f6bc.csv` — the per-turn trace (turns 0 to 8)

The manifest also names chronicle, telemetry and forensic files. They are not checked in: resume and
`sim inspect` without `--answer` read only the manifest, the orders and the trace.

## How it was made

Run with the batch-2 build's `Sim.Cli` and `scripts/gen-sample-orders.py`:

    python3 scripts/gen-sample-orders.py found-orders.bin 300 --found
    sim run --founded --seed 42 --turns 8 --orders found-orders.bin --emit-session s32found

`--found` writes two FoundSettlement orders (kind 9) by the player, polity 1:

- turn 2: from settlement 0 to cell 330985;
- turn 5: from settlement 12, the colony the first order founds, to cell 322801.

Both sites are legal only on the FLAT seed-42 world. The batch-2 build reported:

    run complete: seed 42, 8 turns, hash 6157dfde1b3c9dc9806fca088d2042831bcc3f344692e5076039947d7fe756c4

and `sim inspect --manifest …` on that build printed `reproduction VERIFIED: 9 turns, hash-for-hash.`
The trace's turn-8 row is 14 settlements, hash `6157dfde…`.

SHA-256 of the files:

- orders `3932be388c77197ea0c9094c4be9c46da5edb88926eec087e98dcac9007d5752`
- manifest `0902eb72f922697f3cd057f820c439e7d7a9cedb85676b2771708675d1fc8b74`
- trace `282934229cc48cde03362ec11c69abab65756a9e1132a09e15c9212b71dd4365`

## What it pins

Before fix round 1, the v33 build re-founded every resumed or inspected session on the shipped cylinder,
whatever schema its manifest recorded. For this session that measured as follows (VERIFIED, 2026-10-15):

- the UI resume, `ResumeLaunch.Attempt`, and `sim inspect` both exited 2;
- the error was "error: order (turn 5): FoundSettlement targets settlement 12, which does not exist when
  the order is delivered (turn 5, 12 settlement(s)) …".

The rule now: every world recorded under a schema before v33 was flat (`TopologyFlags.RecordedFlat`), so
such a session is rebuilt flat.

Tests that read this fixture:

- `Sim.Ui.Tests` `Batch2SessionResumeTests`. The resume reaches turn 8 with 14 settlements. The resumed world's
  v32 projection equals the trace's turn-8 hash.
- `Sim.Tests` `CliSaveAndSizeMessagesTests`. `sim inspect` rebuilds the session flat and says the trace was not
  checked. Fix round 2: above that verdict it prints no line about a hash comparison, and its "reproduce with:" line
  names `sim inspect`, not this build's `sim replay` (which founds the cylinder; the batch-2 recipe exits 2 on it). The
  `sim replay` recipe is given as the recording build's: on the batch-2 build it exits 0 with hash `6157dfde…`.
