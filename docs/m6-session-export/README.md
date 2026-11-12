# M6 session export: one file per session, and the telemetry encoding table

**Status: DESIGN INPUT for the M6 spec (Director's instruction, 2026-10-19). Nothing here is built into the game.**
The request and its agenda entry are in `docs/queue.md` (dated note 2026-10-19) and `docs/milestones.md` (M6 row).

## 1. The problem

One played session writes six files into `runs\`: `orders-*.bin`, `session-*.json`, `trace-*.csv`,
`chronicle-*.txt`, `telemetry-*.jsonl`, `forensic-*.jsonl`. AI assistants accept at most five files per upload. The
telemetry alone is ~96,000 characters per turn (the Director's 68-turn session; up to 214,809 in a 300-turn two-AI
game), far over Excel's 32,767-character cell limit.

## 2. The answer: one workbook per session

`session-<stamp>.xlsx`, written BESIDE the six files (they stay the record: resume reads the binary log, the manifest
and the trace). Upload only the workbook.

| Sheet | Content |
|---|---|
| `README` | How to read the workbook; the build, seed and platform; "EMPTY never means 0 — it means the codebook default". |
| `Manifest` | `session-*.json` as key / value rows. |
| `Orders` | The binary log decoded: turn, issuer polity, kind name, target (settlement and sector spelled out), amount. |
| `Trace` | `trace-*.csv` as is (turn, year, population, settlements, food, hash). |
| `Chronicle` | One line per row. |
| `Telemetry` | Column A turn, column B the coded cell (one turn = one cell). |
| `Codebook` | `codebook-telemetry-v4.tsv` (this folder): the encoding table every coded cell is read with. |
| `Forensic` | The run and close records as key / value rows, with each artifact's sha256. |

## 3. The encoding table (`codebook-telemetry-v4.tsv`)

A lookup table from short codes to telemetry fields: 337 rows, 18.7 KB. Columns:
`sheet | code | pos | json field | type | default when EMPTY | notes`.

- **Cell grammar.** `turnSegment ('|' settlementSegment)*`. A segment is space-separated sections `CODE:payload`.
  Tuple payloads list values by position (`,`); tables list rows (`/`). An empty value means the codebook default;
  `~` means absent or empty.
- **What makes it small.** Field names become positions, names become their ids, defaults are left empty, and the
  migration table that is identical for every settlement is written once per turn (`A`).
- **Numbers** are the writer's exact text (`0.` shortened to `.`), so decoding is byte-identical. `N`, `I`, `-I` are
  NaN and ±Infinity.
- **Excel-safe.** Every cell starts with `v4`, never `= + - @`.

### Measured (characters per turn)

| Game | Original median / max | Coded median / max |
|---|---|---|
| Director's Windows session, 68 turns, 1 AI | ≈96,000 / 103,185 | **8,877 / 10,096** |
| seed 42, 1 AI, 600 turns | 153,087 / 190,689 | **13,127 / 18,557** |
| seed 7, 2 AI, 300 turns | 161,190 / 214,809 | **13,788 / 17,828** |

Every line round-trips byte-identical (1,268 lines, including all 68 of the Director's). 98 adversarial edge cases
pass. The coded cells are smaller than gzip+base64 of the same lines, and stay readable.

**Limit.** About 650 coded characters per settlement: a cell overflows at ≈49 settlements (measured on a synthetic
world). A 600-turn two-AI game is projected at ≈40 (INFERRED). The M6 spec must define a continuation cell.

## 4. The prototype (`prototype/`)

Python 3, design reference only (the M6 exporter is C#, in Sim.Ui or Sim.Cli, never Sim.Core).

```
python3 prototype/encode.py telemetry-X.jsonl cells.txt      # one coded cell per line
python3 prototype/decode.py cells.txt back.jsonl             # back.jsonl is byte-identical to the input
python3 prototype/roundtrip.py telemetry-X.jsonl ...         # checks every line
python3 prototype/make_codebook.py                           # regenerates prototype/codebook.tsv from spec.py
```

`spec.py` is the single source of the table: `make_codebook.py` regenerates `codebook-telemetry-v4.tsv` byte for
byte (checked 2026-10-19).

## 5. Required before M6 ships

1. **Size guard:** never truncate; split an over-limit cell at a `|` into a marked continuation cell.
2. **Self-check:** the exporter decodes every cell and refuses (a named error) on any mismatch. No raw fallback.
3. **C# number text:** reuse the writer's exact double text; compare defaults as text (keeps −0 distinct);
   InvariantCulture; no Dictionary iteration; no `float`.
4. **Versioning:** one codebook per telemetry schema; a writer change needs a new codebook before export works.
5. **Goldens:** the prototype's cells for the measured runs become the C# port's golden tests.

---

## 6. Dated note, 2026-10-28: built in M6 (ADR-059; D-066 A14–A17)

The design above is now built, in `Sim.Ui/Export/`. Sections 1–5 are kept as written. This note records what changed
around them; [ADR-059](../adr/adr-059-one-file-session-export.md) has the details.

**Where the export runs.** The game writes `session-STAMP.xlsx` when a session closes, after the forensic close record.
`sim-ui --export-session DIR|MANIFEST` writes the same workbook headlessly.

**Two codebooks (§5.4).**
- `codebook-telemetry-v4.tsv` is unchanged, byte for byte. It reads `telemetry/v2`, `v3` and `v4` lines.
- `codebook-telemetry-v5.tsv` reads `telemetry/v5`, the tag the writer now emits (D-066 A15). It adds:
  - the optional `grain.military` and `grain.fodder` keys;
  - the names of reasons 17–21;
  - 23 goods;
  - order kinds 14–18;
  - constant C2's military-grain clause;
  - the continuation rule.

**`make_codebook.py`.** It now writes four files into this folder: both TSVs and their machine-readable twins
`codebook-telemetry-v4.json` and `-v5.json`, which the C# codec reads. `make_codebook.py --check` fails when any of
them is stale; CI runs it. It no longer writes `prototype/codebook.tsv`.

**Continuation cells (§5.1).** Section 3's "Limit" paragraph asked the M6 spec to define a continuation cell; D-066 A17
defines it. A continuation row has an EMPTY column A, and its cell starts with `>` followed by the `|` that was cut.
`codec.split_cell` and `codec.join_cells` are the prototype's versions.

**Goldens (§5.5).** The prototype's cells for the Director's 68-turn session and for a 16-turn current-tree session
are the C# port's goldens. They are in `Sim.Ui.Tests/Fixtures/session-export/`.

---

## 7. Dated note, 2026-11-02: Director: one Excel file in runs

The Director's instruction, after the playtest of `511e70f`: "Next run onwards, I want only one excel sheet [in the runs
folder], not these multiple files." Sections 1–6 are kept as written. This note records what changed. The governance
record (a D-record) is the coordinator's.

**The layout.** From this build on, a played session writes its record into a record folder, `runs\.record\`. Only the
workbook is written into `runs\` itself:

```
runs\session-STAMP.xlsx                  the one visible file per session (upload this)
runs\.record\session-STAMP.json          the manifest
runs\.record\orders-STAMP.bin            the order log
runs\.record\trace-STAMP.csv             the turn trace
runs\.record\chronicle-STAMP.txt         the coded chronicle
runs\.record\forensic-STAMP.jsonl        the forensic run and close records
runs\.record\telemetry-STAMP.jsonl       only while the session runs; removed after a verified export
runs\.record\export-STAMP.log            only when that removal failed: why (see below)
```

- **Names unchanged.** Every file keeps its name, so the forensic record, the exporter and every sha256 mean what they
  meant. Only the directory moved. `UiSession.SessionLogPath` is now `runs/.record/orders-…`, and every companion path
  is twinned from it.
- **Hidden on Windows.** The game marks `.record` Hidden on Windows (`UiSession.PrepareRecordFolder`, called when the
  manifest is written at launch), so Explorer shows only the workbooks. With "Show hidden items" switched on, it is there.
- **Where the workbook goes.** `SessionWorkbook.WorkbookPathFor` puts it in the record folder's parent when the manifest
  is in a folder named `.record`. Otherwise it goes beside the manifest, as before: an older flat `runs\`, or a copied
  session. The self-check, and the removal's check that the workbook on disk is the verified one, are unchanged.
- **Resume.** `Continue last game.cmd` still passes `--resume runs`. `UiArgs.ResolveManifest` now looks in
  `runs\.record\` and in `runs\` itself, and takes the newest stamp. If the same stamp is in both, the record folder's
  copy wins. An explicit manifest path and an older flat folder still resume. `--export-session runs` finds the session
  the same way.
- **Workbook content unchanged.** The workbook's sheets are byte for byte what they were for the same six files. Its
  README still says the files are "beside" it. In the new layout that means the hidden folder next to it.

**Why the 16 MB telemetry log was still there.** In the Director's Windows session `20261102-140558-a1`, the workbook
verified (its README lists the telemetry present, 16,074,749 bytes), but `telemetry-*.jsonl` was not removed. The
reason went to stderr, which the `start ""` launcher discards. The probable cause is a transient Windows lock on a
freshly written 16 MB file (a Defender scan, an Explorer preview, the search indexer or cloud sync) that made
`File.Delete` throw. That is NOT PROVEN: no log of the failure exists. The code closes every handle it opens.

**What changed in the removal** (`SessionWorkbook.RemoveRawTelemetry`):
- **Retries.** An `IOException` or `UnauthorizedAccessException` is retried. There are up to 5 attempts in all, with
  waits of 250, 500, 750 and 1000 ms (2.5 s at most).
- **Checks on every attempt.** All three verification conditions are checked again before each attempt.
- **No retry for a failed condition.** A condition that does not hold is final at once.
- **The log.** If the log still cannot be removed, it is kept, and the reason goes to stderr AND is appended to
  `runs\.record\export-STAMP.log`. The reason names the number of attempts and the last error. The next failure can
  then be diagnosed.
- **The exit.** A failed delete never blocks the exit; the workbook is already written.

**CI.** No Windows job performs a real quit and export. The `windows-smoke` job runs `--smoke` on a temporary flat
folder and never closes a session through `SaveOnExit`. So no CI assertion on the Windows runs folder was added. The
layout is asserted in `Sim.Ui.Tests/SessionRecordFolderTests.cs`, through the real GameUi close on the headless
harness. The Windows-only real-lock test in that file runs only on Windows.

---

## 8. Dated note, 2026-11-11: D-086, research in the run record, and a close that keeps the workbook

Sections 1–7 are kept as written. The ruling is [D-086](../d086-run-record-research-and-exit-export-2026-11-11.md). The
measurements and the evidence are in lane RR's report,
[run-record-research-and-exit-2026-11.md](../m6-plan/run-record-research-and-exit-2026-11.md).

### When no workbook appears: what to do

The game writes `runs\session-STAMP.xlsx` while it closes. It writes the record files into the hidden folder
`runs\.record\` first, so the workbook can always be rebuilt from them if the close did not finish.

1. Leave `runs\.record` where it is. Do not delete or move it: "Continue last game" reads it too.
2. Open a command prompt in the game folder, the folder that holds `Play civ-sim.cmd` and the `app` folder. In
   Explorer, click the address bar of that folder, type `cmd` and press Enter.
3. Run this command:

   ```
   start /wait "" "app\Sim.Ui.exe" --export-session runs
   ```

   It reads the newest session in `runs\.record` and writes `runs\session-STAMP.xlsx`. The game is a window
   application, so the command prints nothing. `start /wait` makes the prompt wait until the workbook is written.
   - For an older session, name its manifest instead of `runs`:
     `start /wait "" "app\Sim.Ui.exe" --export-session runs\.record\session-STAMP.json`.
4. If no workbook appears, the reason is the last line of `runs\.record\export-STAMP.log`. Send the whole
   `runs\.record` folder, zipped: it holds everything the workbook is built from. Explorer shows the folder when
   View, Show, Hidden items is switched on.

The game's saving screen gives the same command when a close ends without a workbook.

### Research in the run record (D-086 DE1)

- **`telemetry/v6`** is `telemetry/v5` plus two arrays at the end of each turn object (the writer moved the tag under
  the T4.20 rule):
  - `research`: one row per knowledge holder and research node that received points or completed in the step. Each
    row holds the holder, the node, its tree (Technology or Civics, the lane in the two-lane model), the points it
    received, the part of them that was acceleration credit, its progress after the step, its cost, its repeatable
    level and whether it completed. The holders are every polity (the player's and each AI empire's) and every
    city-state.
  - `civicsBank`: one row per Civics Points bank that changed in the step, with the milli-CP generated, drawn and
    left.
  - The rows are read or differenced from the research tables. No simulation state, schema or world hash changes.
- **Codebook v6** (`codebook-telemetry-v6.tsv` and `.json`, generated from `prototype/spec.py`) reads `telemetry/v6`.
  It is codebook v5 plus the tables `r` and `b` and the enum `tree`. `make_codebook.py --check` now covers six files.
  Codebooks v4 and v5 are unchanged, byte for byte.
- **The Research sheet.** The workbook has a ninth sheet, `Research`. It comes last, so every earlier sheet keeps its
  position. It holds:
  - one row per research row of the telemetry lines, kind `RP` (received points) or `RC` (completed on that turn);
  - one `BK` row per Civics Points bank row.

  Its columns are `T HD KD RN LN PR AC PG CS LV GN DR BL`. Like every non-legend sheet it holds codes and numbers only,
  and the Codebook defines every code (sections `Research:col`, `Research:kind` and `Research:read`). A node's name is
  its `RN<key>` row in the Codebook's `Chronicle:names`: the coded chronicle now names every node the research rows
  hold.
- **Older sessions** still export. A session whose telemetry predates `telemetry/v6` gets a Research sheet with one
  `NR` row, which counts the telemetry lines that carry no research rows. Nothing is invented.

### The close (D-086 DE2)

- **Same steps, same order, same files.** The close still runs the save, the telemetry finalize, the forensic close
  record, the workbook and the raw log's removal, in that order (`Sim.Ui/SessionClose.cs`). It writes the same files as
  before. Their content changes only with the research rows above:
  - the `telemetry/v6` lines;
  - the coded chronicle's name lines, which now also name every research node the research rows hold: one more
    `RN<key>=name` line per such node, in the sorted name block, every other line unchanged (measured on a 300-turn
    game: 15 more lines);
  - in the workbook, the Research sheet, its README rows, its Codebook legend and those names in `Chronicle:names`.

  *Corrected on 2026-11-12 in lane RR's verification round 1: this list first left out the chronicle's name lines.* Run
  on the worker, the close writes byte for byte what the same close writes on the calling thread (tested).
- **On a worker thread, with a saving screen.** The window keeps running its loop and draws a saving screen. The
  screen shows each step, its time and the workbook step's progress. The window closes by itself once the workbook is
  written. A second close request while the run is saved starts nothing.
- **A close without a workbook.** If the export is refused or a step fails, the saving screen stays up with the reason
  and the recovery command until the player closes the window again. The reason is also appended to
  `runs\.record\export-STAMP.log`, and so is the reason when `--export-session` refuses a session or fails, for example
  on a record file it may not read or when memory runs out (both measured, 2026-11-12).
- **The workbook is written whole.** It is first written to `runs\.record\session-STAMP.xlsx.partial` and then moved
  into place in one rename, so `runs\` never holds part of a workbook.
- **A faster export, the same self-check.** The export reads the telemetry log line by line and parses each line once.
  The self-check still guarantees what it did:
  1. every cell decodes back to its line, byte for byte, when it is encoded;
  2. the workbook read back through `ZipArchive` and `XmlReader` holds exactly the text written, in every cell;
  3. the Telemetry sheet's cells, continuation rows joined, are the verified cells.

  Check 3 used to decode every cell a second time. The decoder is a pure function of the cell, so a cell equal to a
  verified cell decodes to the same line.
- **Games past world year 5920.** From 1920 CE a turn is half a year, and the trace's year column holds values such as
  `5920.5`. The export checked the trace with the kernel's reader, which reads the year as an integer. Every session
  played past turn 1270 was therefore refused (`record-unreadable`) and lost its workbook. The export now checks the
  trace with its own reader, which reads the year as the number written. The kernel's reader is frozen and unchanged:
  "Continue last game" on such a session is still refused (a proposal for the Director, in the report).
