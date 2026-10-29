# Session-export fixtures (ADR-059)

Two played sessions, as their files. `SessionExportTests` exports each to a workbook and pins it byte for byte. It also
checks that the C# codec writes exactly the prototype's cells (`prototype-cells.txt.gz`).

Telemetry is stored gzip'd (`telemetry-*.jsonl.gz`); the tests un-gzip it in memory under its original name. Every
other file is byte-for-byte the file the session wrote.

## `director-68/`: the Director's 68-turn session

This is the Windows session of 2026-10-19: build `231485e`, seed 42, one AI empire, schema v34, `telemetry/v4`. The
files are the five the Director uploaded, renamed from the uploader's prefixed names back to their original names. Their
sha256 values equal the ones in the forensic close record:

| File | Bytes | sha256 |
|---|---|---|
| `session-20261019-144516-a1.json` | 716 | `302f13b5…94fc497` |
| `orders-20261019-144516-a1.bin` | 772 | `51b162d0…a61bc` |
| `trace-20261019-144516-a1.csv` | 5,888 | `e0bbef04…d90176` |
| `telemetry-20261019-144516-a1.jsonl` (gzip'd here) | 6,676,826 | `a3aadef5…3721` |
| `forensic-20261019-144516-a1.jsonl` | 10,464 | `1ad6b5b8…8e26b` |

The chronicle file is not in the set. The manifest names it, and the close record lists it as 3,349 bytes with sha256
`3b0b4eba…c38a`, so the build did write one. It was not among the uploaded files. The workbook's Chronicle sheet says
so and invents no line.

`prototype-cells.txt.gz` is the output of `python3 -I docs/m6-session-export/prototype/encode.py` on the telemetry file:
68 cells, codebook v4, the longest 10,096 characters. `decode.py` gives the file back byte for byte.

## `current-v5/`: a current-tree session (`telemetry/v5`)

The M6 closure tree (the M7 pass plus D-066) wrote this session through the game's own session paths. The generator is
`SessionExportTests.Tool_WritesTheCurrentTreeFixture_OnlyWhenAsked`, run with `CIVSIM_WRITE_EXPORT_FIXTURE=DIR`. It
plays seed 42 on the canonical world with one AI empire for 16 turns. The player sets a research target on turn 0 and
labour splits on turns 1 and 2; the AI's orders include Prospect. The files are written as `GameUi` writes them: the
manifest and the forensic run record at launch, then log, chronicle, trace and telemetry after every End Turn, then the
telemetry finalize and the forensic close. The build identity is `dev`/`local` and the platform `ubuntu.24.04-x64`.
Every telemetry line carries `grain.military`, an optional key that is new in `telemetry/v5`.

This set has six files, a chronicle among them. `prototype-cells.txt.gz` is `encode.py`'s output: 16 cells, codebook
v5, the longest 10,206 characters, no `!` escape. `decode.py` gives the file back byte for byte.

These files are a frozen record. A later change to the simulation does not make them wrong: the export reads files,
never a replay.
