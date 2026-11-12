# The run record keeps research, and closing the game keeps the workbook (D-086): lane RR report, 2026-11-11

**Status: lane report.** It was built under [D-086](../d086-run-record-research-and-exit-export-2026-11-11.md), with the
readings DE1-R1..R3 (a), DE2-R1 (a) and DE-R (a) applied. Sections marked PROPOSAL are for the Director. Each claim is
labelled MEASURED (run on this tree, with the figure given) or INFERRED (argued, not run). "Base" is the lane's base
commit `51ab032`. Its `Sim.*` tree equals the `m6-exit` build `7d5defc`, the build the Director played, per D-086's
record. The measurements ran on the Linux x64 session container: 4 CPUs, Release builds, `nice -n 19`, no other agent
running. The raw outputs are listed in §9.

The lane's other report, for the M7 pass, is
[`prototypes/m7-sprint1/reports/H-RR.md`](../../prototypes/m7-sprint1/reports/H-RR.md). The Director's recovery note is in
[`docs/m6-session-export/README.md` §8](../m6-session-export/README.md#8-dated-note-2026-11-11-d-086-research-in-the-run-record-and-a-close-that-keeps-the-workbook).

## 0. Summary

1. **Research is in the run record (DE1).** Every telemetry line now carries the step's research
   (`telemetry/v6`). There is one row per knowledge holder and research node that received points or completed: the
   player's empire, each AI empire and every city-state. **Corrected in part (verification round 2, RR-R2-01):** a
   founded game has no city-state, so the sessions recorded here hold the empires' rows only (MEASURED: the
   `current-v6` fixture, holders 1 and 2; a 300-turn game, no holder below 0). City-state rows are tested on the rigs and
   on a session with one settlement released before the first turn (H-RR §11). Each row gives:
   - the node and its lane (Technology or Civics);
   - the points received, and the part of them that was acceleration credit;
   - the progress after the turn, and the cost;
   - the repeatable level, and whether the node completed on that turn.

   One more row per holder whose Civics Points bank changed gives the CP generated, drawn and left. The workbook has a
   ninth sheet, **Research**: those rows, coded, with every code in the Codebook. A session recorded before `telemetry/v6`
   still exports, with one `NR` row (not recorded) and nothing invented.
2. **Why the close looked frozen (DE2).** At base, the whole close ran inside MonoGame's `OnExiting` on the game
   thread, so the window drew nothing and answered no input until it was done (CITED from the code). MEASURED here on
   an 850-turn session like the Director's:
   - 14.2 s for the close, all of it without a frame;
   - a peak of 3,050 MB resident while the workbook was built;
   - 12,449 MB allocated.

   On the Director's laptop it was likely longer, and Windows treats a window that stops answering as "Not
   Responding" (both INFERRED). That the workbook step was the one being killed is INFERRED too: no log of that session
   exists.
3. **The fix (DE2-R1 (a)).** The close keeps its steps and their order, and writes the same files: their content changes
   only by DE1's research rows. **Superseded in part (verification round 1, RR-R1-02):** DE1 also adds name lines to
   the coded chronicle, one per research node the rows hold (MEASURED, §6). It now runs on a worker thread while the
   window keeps its loop and draws a saving screen:
   - the steps done, with their times;
   - the workbook step's progress (tested since verification round 2, RR-R2-03: no test read it before);
   - that the window closes by itself.

   A second close request starts nothing. A close that ends without a workbook keeps the screen up with the reason and
   the recovery command, and writes the reason to the export log. The workbook is written whole: first a `.partial`
   file, then one rename. The record files are complete before the workbook step starts, and `--export-session`
   rebuilds the workbook after a close interrupted at any step (tested at each of the five).
4. **The export is faster and smaller.** The self-check is kept. The telemetry is streamed, each line is parsed once,
   and the second decode of every cell is replaced by an equivalent comparison (§4.5). MEASURED:

   | Session | Base | Lane |
   |---|---|---|
   | The Director's 850-turn files, headless | 12.5–13.3 s, 1,290 MB peak | 8.8–9.7 s, 698 MB peak |
   | The in-game close of an 850-turn session | 14.2 s, 3,050 MB peak | 9.5 s, 939 MB peak |

5. **A found defect: a full-length game lost its workbook.** From 1920 CE (world year 5920, turn 1271) a turn is half a
   year, and the trace writes years such as `5920.5`. The export checked the trace with the kernel's reader, which reads
   the year as an integer, so it refused every session longer than 1,270 turns. MEASURED on a 1,630-turn game (year 6100,
   telemetry 356 MB):
   - base: refused at close and with `--export-session` (`record-unreadable`), no workbook;
   - lane: the workbook is written, the close taking 19.9 s with a peak of 1,570 MB.

   The kernel reader is frozen and unchanged. "Continue last game" on such a session is still refused (MEASURED, base
   and lane), which is a PROPOSAL for the Director (§10).
6. **No simulation state changed.** MEASURED: the trace, which holds every turn's world hash, is byte for byte the
   same at base and in the lane, for 850 turns and for 1,630 turns. The moved pins are the telemetry tag and the
   workbook content goldens, each attributed part by part in §6.

## 1. What the Director asked, and the readings applied

D-086 (verbatim in the record):
- **DE1:** "it should record which node was researched and how much and which turn also".
- **DE2:** "I've closed the game and runs excel sheet didn't form. The screen froze and i think the excel didn't come."

The readings applied are D-086's (a) readings:
- every polity's research, the AI's included (DE1-R1);
- the points received, the progress and the cost, plus the banked CP drawn (DE1-R2);
- one row per holder, turn and node, with a completion row (DE1-R3);
- all three parts of "fixed": a responsive window, a recoverable close, a measured and reduced export (DE2-R1);
- the scoped `Sim.*` exception (DE-R).

Nothing in `Sim.Core/Kernel` changed, no simulation state or world hash changed, and nothing was merged, tagged or
released.

## 2. What changed (files, measured `git diff --stat 51ab032..HEAD`)

| Area | Files (lines +/−) |
|---|---|
| Research rows (`Sim.Core/Observability`) | `ResearchObservation.cs` (new, +190), `TurnRecord.cs` (+43/−1), `Observer.cs` (+4/−1), `TelemetryWriter.cs` (+39/−3), `Forensic/TelemetryRecordReader.cs` (+34/−3) |
| Codebook v6 (`docs/m6-session-export`) | `prototype/spec.py` (+36/−4), `prototype/make_codebook.py` (+16/−8), `codebook-telemetry-v6.tsv` (new, 388 lines), `codebook-telemetry-v6.json` (new, 4,549 lines); v4 and v5 unchanged |
| The export (`Sim.Ui/Export`) | `SessionWorkbook.cs` (+179/−46), `SessionWorkbook.Research.cs` (new, +163), `TelemetryCellCodec.cs` (+22/−3), `TelemetryCodebook.cs` (+17/−5), `WorkbookCodes.cs` (+25), `SessionExportCommand.cs` (+29); `Sim.Ui.csproj` (+4/−1) embeds codebook v6 |
| The close (`Sim.Ui`) | `SessionClose.cs` (new, +192), `GameUi.Closing.cs` (new, +109), `GameUi.cs` (+86/−6), `SimUiGame.cs` (+28/−3), `UiSession.cs` (+58/−8), `FrameText.cs` (+2), `Headless/UiFrameHarness.cs` (+21/−1) |
| Tests | `Sim.Tests/Observability/ResearchObservationTests.cs` (new, +437); `Sim.Ui.Tests/RunRecordResearchTests.cs` (new, +325); `Sim.Ui.Tests/SessionCloseTests.cs` (new, +530); `SessionExportTests.cs` (+81/−10); pins moved in `ForensicIdentityTests`, `InspectionTests`, `TaxGlassBoxTests`, `TelemetryTests`, `SessionRecordTests` (§6); `SessionExportCodesTests.cs` (+1); fixture `Fixtures/session-export/current-v6/` (new, seven files) |
| Docs | `docs/m6-session-export/README.md` §8 (+87: the recovery note and the dated note); the fixture README (+24); this report; `H-RR.md` |

The figures are `git diff --numstat 51ab032..HEAD` at the commit this table was written on (the reports themselves are
not counted). The two verification rounds changed more files after it, each listed with its own figures:
[`H-RR.md` §10.2](../../prototypes/m7-sprint1/reports/H-RR.md#102-what-changed) (round 1: `SessionExportCommand.cs` and
tests) and [§11.3](../../prototypes/m7-sprint1/reports/H-RR.md#113-what-changed) (round 2: a progress test seam in
`SessionClose.cs` and `GameUi.cs`, tests, the fixture README).

## 3. DE1: research in the run record

### 3.1 Where the rows travel: the telemetry line (`telemetry/v6`), not a file of their own

The brief offered two carriers: the telemetry line under a new tag, or a seventh record file listed by the forensic close
record. **Chosen: the telemetry line.** The reasons:

- **One observation, one writer.** The research rows are an observation of the step, like every other telemetry field.
  `Observer.Observe` already builds each step's `TurnRecord` from the two worlds the step joined. The rows are two more
  arrays on that record, written by the same writer in the same append. A second file would need its own append path,
  its own crash-safety argument, its own reader and its own entry in the close record.
- **Everything that already travels with the telemetry comes free.** The writer's prefix-safe append, the resume rebuild
  (a resumed session's telemetry is re-observed from replay), `sim inspect`'s reader, the export's self-check (every cell
  decoded back to its line) and the raw log's removal and recovery all cover the research rows with no new code.
- **The cost is small.** MEASURED:

  | Session | Telemetry `v5` | Telemetry `v6` | Growth |
  |---|---|---|---|
  | 850 turns | 162,102,844 bytes | 162,761,552 bytes | +658,708 bytes (+0.41 %) |
  | 1,630 turns | 356,083,779 bytes | 357,314,698 bytes | +1,230,919 bytes (+0.35 %) |

- **The T4.20 rule is honoured.** The field set changed, so the tag moved to `telemetry/v6`. The reader accepts v6 and
  every vintage it accepted before. The export has a codebook for each vintage (README §5.4: one codebook per schema).

### 3.2 The rows

`ResearchObservation.Observe(prev, next, content)` produces the rows. It is called from `Observer.Observe`, reads the
research tables before and after the step, and writes nothing. There are two row types:
- `ResearchStep(Holder, Node, Tree, Received, Credit, Progress, Cost, Level, Completed)`;
- `CivicsBankStep(Holder, GeneratedMilli, DrawnMilli, BalanceMilli)`.

| Field | Kind | Rule |
|---|---|---|
| `Holder` | READ | the research rows' `PolityId`: 0 or more is a polity (`PL<id>`); below 0 is a city-state researching under its local key (−1 − settlement id; `ST<id>`) |
| `Node`, `Tree` | READ | the node key; its tree, which is its lane in the two-lane model |
| `Received` | DIFFERENCED | the progress after the step minus the progress before (0 when there was no row) |
| `Credit` | DIFFERENCED | the part of `Received` that was acceleration credit: Σ over the sources (Eureka, foreign exposure) of `ResearchCredits` next − prev |
| `Progress` | READ | the progress row after the step. On a completion row: `max(progress before, cost)`, because the system removes the row of a completed node (§8, limitation 1). Both cases are tested: a credit that reaches the cost, and (since verification round 2, RR-R2-02) a cost that fell below the stored progress, where the row keeps that progress and received 0 |
| `Cost` | RECOMPUTED | `ResearchQuery.EffectiveCost(prev, …)`: the same function, on the same world, that the system used in the step |
| `Level` | READ | `ResearchLevels` after the step (repeatable levels; 0 = none) |
| `Completed` | DIFFERENCED | a `ResearchCompleted` row appeared, or the level rose |
| bank fields | DIFFERENCED / READ | `CivicsPointsRow` next − prev for generated and spent (drawn); the balance after the step |

**Which rows.** A row is written for every node whose stored progress moved, every node that completed and every level
that rose. That covers a node paid only by credit, the nodes an overflow paid, and a completion with no points that turn.
A bank row is written for every account that changed.

**Order.** The rows are sorted by an integer composite key: the roster polities (ascending id), then the city-states
(ascending settlement id), each holder's nodes by key. Every lookup is a binary search over sorted integer keys: no
`Dictionary`, no hash, no `float`, no RNG. Every (holder, node) pair appears once, so the order is total.

**Determinism and isolation.** The code is observability. No system references it, it writes no table and it is in no
hash. MEASURED: the 850-turn and the 1,630-turn traces are byte-identical at base and in the lane.

### 3.3 The Research sheet

The ninth sheet comes after Forensic, so every earlier sheet keeps its index. Its columns:

| Column | Holds |
|---|---|
| `T` | the turn |
| `HD` | the holder (`PL<id>`, `ST<id>`) |
| `KD` | the kind: `RP` received points, `RC` completed this turn, `BK` a bank row; `NR n` (n lines without research rows) and `NX n` (research recorded, no row on any line) |
| `RN` | the node (`RN<key>`) |
| `LN` | the lane (`LT`, `LC`) |
| `PR AC PG CS` | the numbers, as the writer wrote them |
| `LV` | the level |
| `GN DR BL` | for bank rows, the CP generated, drawn and left, exact to the thousandth |

The legend defines every code (`Research:col`, `Research:kind`, `Research:read`). A node's name is its `RN<key>` row in
`Chronicle:names`: `UiSession` notes the name of every node the research rows hold in the coded chronicle's name lines,
the same lines the `RM` event already used. MEASURED: the Research sheet held 5,007 rows for the 850-turn session (2,259
`RP`, 1,048 `RC`, 1,700 `BK`) and 9,216 rows for the 1,630-turn one.

### 3.4 Older sessions

A `telemetry/v2`–`v5` line has no research array. The reader reports it as `Recorded = false`, never as "nothing
researched". The sheet holds one `NR n` row, and the README says research was not recorded. A session whose telemetry
file is missing gets the same `NP` row the Telemetry sheet holds. MEASURED by tests on the Director's 68-turn (`v4`) and
the `current-v5` fixtures (§5).

## 4. DE2: the close

### 4.1 What made the close look frozen

| Factor | Evidence |
|---|---|
| **The window drew nothing for the whole close.** `SimUiGame.OnExiting` called `GameUi.SaveOnExit`, which ran the five steps on the game thread. MonoGame raises `OnExiting` at the end of a tick, so no frame was drawn and no window message answered until it returned. | CITED: base `SimUiGame.cs`; MonoGame 3.8.5 `Game.Tick` |
| **The close took 14.2 s on this host** for an 850-turn session (the export step 13.84 s). The export alone of the Director's own files took 12.5–13.3 s. | MEASURED (§4.2) |
| **Memory.** The base export read the 162–173 MB log, held it as one string and as an array of lines, and parsed every line three times. The workbook step peaked at 3,050 MB resident (+2,664 MB over the game's 386 MB) and allocated 12,449 MB. | MEASURED (the peak and the allocation); CITED (the three parses: `CodebookOf`, `TurnOf`, `EncodeChecked`) |
| **Windows ghosts a window that stops answering.** After a few seconds without message processing, the window is marked "Not Responding" and offers to close the program. | INFERRED (Windows behaviour; not run here: no Windows host) |
| **Antivirus and indexing.** The close reads a freshly written 173 MB log twice (the export, then the removal's sha256) and writes a 7 MB workbook. Real-time scanning on open can add time. | INFERRED (not measurable here) |
| **The removal's retries** wait up to 2.5 s when Windows holds the log (Director 2026-11-02). | CITED (`RemovalAttempts`, `RemovalWaitStepMs`) |
| **No message at all.** Nothing told the player the run was being saved. | CITED (base code) |

What happened to the Director's session is INFERRED: the window froze long enough to be ended, and it was ended during the
workbook or removal step. Three facts are consistent with this:
- the telemetry log was still there (a verified export removes it);
- no workbook appeared;
- the four other files were whole: the Director sent them, and they export.

The forensic file was not sent, so whether the close record was written is unknown.

### 4.2 Measured close timings, before and after

The in-game close was measured with a scratch harness (`closebench`, §9) that plays a session through the real
`UiSession` with the game's End Turn saves:
- **base:** the harness times the base `SaveOnExit` sequence call by call;
- **lane:** the harness runs `Sim.Ui.SessionClose`, the code the game runs, and times it through its own step hook.

Peak = the resident set's high-water mark after a reset at the start of each step (`/proc/self/clear_refs`). Seed 42,
one AI empire, the player researching the cheapest node of each lane.

**850 turns (year 5100; the Director's session was 850 turns, year 5100).**

| Step | Base: s / peak MB | Lane: s / peak MB |
|---|---|---|
| 1 save | 0.00 / 386 | 0.01 / 393 |
| 2 telemetry finalize | 0.02 / 386 | 0.02 / 392 |
| 3 forensic close | 0.14 / 386 | 0.16 / 375 |
| 4 workbook | **13.84 / 3,050** (allocated 12,449 MB) | **9.11 / 939** (allocated 6,129 MB) |
| 5 raw-log removal | 0.23 / 3,016 | 0.19 / 788 |
| close in all | 14.23 s, no frame drawn (CITED: on the game thread) | 9.49 s (the harness ran `SessionClose` on its own thread; in the game it runs on the worker while the window keeps drawing, tested in §5) |

The sizes:

| Session | Telemetry | Workbook |
|---|---|---|
| Base | 162,102,844 bytes | 6,884,224 bytes |
| Lane | 162,761,552 bytes | 7,188,032 bytes |

The lane's workbook is larger by the Research sheet and the research rows in the cells.

**1,630 turns (year 6100, the end of the timeline), "a session whose telemetry reaches the hundreds of MB".**

| Step | Base | Lane |
|---|---|---|
| 1–3 | 0.00 / 0.06 / 0.30 s | 0.00 / 0.05 / 0.30 s |
| 4 workbook | **refused after 0.43 s**: `record-unreadable: trace-…csv: The input string '5920.5' was not in a correct format.` No workbook. | **19.08 s, peak 1,570 MB** (+1,001 MB over 570), allocated 13,416 MB |
| 5 raw-log removal | not run (no workbook) | 0.48 s |

- The telemetry was 356,083,779 bytes at base and 357,314,698 bytes in the lane.
- The lane's workbook was 16,678,978 bytes: 1,630 turns, 11 continuation rows, the longest cell 32,739 characters.

**The export alone, headless** (`dotnet Sim.Ui.dll --export-session`; wall time and peak resident memory of the process).

| Input | Base | Lane |
|---|---|---|
| The Director's files (850 turns, `v5`, 173,040,889 bytes), 3 runs each | 13.26 / 12.83 / 12.49 s; 1,290 MB | 8.84 / 8.92 / 9.66 s; 698 MB |
| Base's 850-turn session (`v5`), 2 runs each | 12.04 / 12.27 s; 1,228 MB | 8.38 / 8.45 s; 705 MB |
| The lane's 850-turn session (`v6`) | — | 9.20 / 9.01 s; 702 MB |
| The 1,630-turn session (`v6`) | refused after 0.53 s (exit 2, the trace) | 18.97 / 19.07 s; 1,559 / 1,408 MB |

The workbooks:
- the base export of the Director's files is byte for byte the workbook in `docs/m6-plan/playtest-session-20261108-155547-a1/`
  (sha256 `01d3c33e…`);
- the lane's export of the same files is 6,843,388 bytes. It adds the Research sheet, with one `NR 850` row, and two
  README rows.

**Is a workbook that holds every telemetry line reasonable at this size?** On this host, yes: the longest game the
timeline allows closes in about 20 s with a peak of 1.6 GB in all. The window stays responsive and shows the progress.
The time and memory grow roughly linearly with the telemetry: about 5 s and 0.2–0.4 GB more per 100 MB of log
(MEASURED between the 850-turn and the 1,630-turn sessions). A slower laptop
multiplies the time (INFERRED). §10 lists ways to cut both further, as PROPOSALS; none is needed to stop the freeze.

### 4.3 The fix: the close on a worker, the window drawing a saving screen

- **`SessionClose`** runs the five steps on one worker thread (not a background thread) in the unchanged order: save,
  telemetry finalize, forensic close, workbook, raw-log removal. The game thread reads only the close's progress:
  - the step;
  - each step's seconds;
  - the workbook step's share of the log read;
  - the failure.

  It never touches the session again: `GameUi.Update` takes no game input, and `GameUi.Draw` draws only the saving
  screen.
- **The host's decision** is in `GameUi`: `HostExiting` for MonoGame's `OnExiting`, and `HostShouldExit` after `Update`.
  - Any exit request (the close button, Alt+F4, Escape) starts the close and is cancelled (MonoGame 3.8.5's
    `ExitingEventArgs.Cancel`).
  - Once the workbook is written, the host exits by itself.
  - If the close ended without a workbook, the window waits for the player's next request.

  `SimUiGame` only forwards; the headless harness drives the same two methods (Host mode).
- **What the screen says:**
  - while the close runs: "Saving the run", each step with `[done]` and its seconds, `[now]` with the workbook's
    percentage, and "The window closes by itself when the record is complete". The percentage had no test until
    verification round 2 (RR-R2-03); it is now tested held mid-step, against the bytes read;
  - from the second request on, a line that ending the game by force could lose the workbook, with the recovery command;
  - when the close ends without a workbook, the reason and the recovery command.
- **What is written is unchanged, but for DE1.** The close writes the same files, in the same order. Their content
  differs from base only by the research rows: the `telemetry/v6` lines, the Research sheet and its README rows (§6
  attributes every changed part). **Superseded in part (verification round 1, RR-R1-02):** the list left out a record
  file. The coded chronicle gains one name line per research node the rows hold, and the workbook's
  `Chronicle:names` gains the same names. §6 now attributes it, MEASURED.
- **Threading changes nothing in the files.** MEASURED by test: run on the worker, the close writes byte for byte the
  files the same close writes on the calling thread, for the same session (§5).

### 4.4 An interrupted close is recoverable

- **The record files are complete before the workbook step starts.** Steps 1–3 write and flush them, and the close
  record hashes them. The workbook step is about 96–97 % of the close (MEASURED: 13.84 of 14.23 s at base, 9.11 of 9.49 s
  and 19.08 of 19.92 s in the lane), so an interruption almost always falls after the files are complete.
- **An interruption at any step leaves files that `--export-session` rebuilds the workbook from.** TESTED at each of the
  five steps: a test seam throws before the step, standing in for the process ending there. The README's new
  `close record` row says plainly whether the close record was written, and so whether the files are the closed
  session's or the last End Turn's.
- **The workbook is written whole.** It goes first to `runs\.record\session-STAMP.xlsx.partial`, then moves into place in
  one rename. A partial file left by a write cut short is replaced by the next export, and `runs\` never holds part of
  a workbook.
- **A second close request during the work** starts nothing: one close per session, guarded by an atomic state. TESTED:
  each step runs exactly once, and the forensic file holds exactly one close record.
- **A failure is reported where it can be read.** The reason is shown on the saving screen and appended to
  `runs\.record\export-STAMP.log`, and so is the refusal of a headless `--export-session`. The game is a window
  application on Windows, so stderr is lost there.
- **Not covered: a kill in the middle of writing a file.** This means steps 1–3, or an End Turn save. Those writes take
  milliseconds (MEASURED: steps 1–3 at most 0.36 s in all at 1,630 turns), and the telemetry append is prefix-safe by
  design (P0). §8 lists it as a limitation.

### 4.5 The export's time and memory, with the self-check kept

**What changed** (`SessionWorkbook.Build`):

1. The telemetry is read line by line from the file's bytes. It is never held as one string or as an array of strings.
2. Each line is parsed once: its schema, turn, cell and research rows all come from one parse. At base, the line was
   parsed three times.
3. The file's sha256 is computed once. At base it was recomputed on every access.
4. Self-check 3 compares the re-joined Telemetry cells with the cells verified at encode time. At base it decoded every
   cell a second time.

**The self-check still guarantees what it did:**
- (1) every cell decodes back to its line, byte for byte, at encode time;
- (2) the workbook read back through `ZipArchive` and `XmlReader` holds exactly the text written, in every cell of every
  sheet;
- (3) the Telemetry sheet's cells, continuation rows joined, are the verified cells in order.

`TelemetryCellCodec.Decode` is a pure function of the cell: it holds no mutable state, only `readonly` tables. So
decode(joined) = decode(verified) = the line, and the second decode added time, not assurance.
- MEASURED: at base, `SelfCheck` was 21.6 % of the export of the Director's files, the second decode about 18 % of it
  (`dotnet-trace`, §9).
- TESTED: a mutant that drops a split cell's last part is refused by check 3 (M09, §7).

**Where the time goes now.** MEASURED by `dotnet-trace` on the Director's files, lane build:

| Part | Share |
|---|---|
| Encode + immediate decode (check 1), JSON parse included | ≈75 % |
| Workbook write | 12.5 % (deflate 10 %) |
| Read-back (check 2) | ≈4 % |
| GC pauses | 0.98 s of ≈9.2 s |

The export is now compute-bound in the codec. §10 lists the next steps as PROPOSALS: a streamed log, and a parallel
encode.

### 4.6 Found by measurement: a game played past turn 1270 lost its workbook

`SessionTrace.Line` writes the year as `WorldDateYears`, a double. `SessionTrace.Parse` reads it with `long.Parse`. From
world year 5920 (1920 CE, the Modern band, `dtYears` 0.5) the year is fractional, and the reader throws. Both are in the
frozen kernel (`Sim.Core/Kernel/SessionTrace.cs`).

MEASURED effects at base, on the 1,630-turn session:
- the close refuses the workbook (`record-unreadable`);
- `--export-session` refuses with exit 2;
- `UiSession.Resume` ("Continue last game") throws `SessionUnreadableException` naming the trace;
- the 850-turn control resumes, to turn 850 in 69.2 s.

**In this lane's scope (the export), fixed.** `SessionWorkbook.CheckTrace` checks the trace with its own reader. It is
the kernel reader's rules exactly, except that it reads the year as the number written:
- six columns;
- the integer columns as `NumberStyles.Integer`;
- a malformed line named by its number.

TESTED: a half-year trace exports, the frozen reader throws on the same lines, and four kinds of malformed line are still
refused by number.

**Outside the scope (the kernel), not fixed.** Resume, `sim replay` and `sim inspect` still use the kernel reader. That
is a PROPOSAL (§10, P1).

## 5. Tests (new and changed; all passing, MEASURED; counts in §7)

**`Sim.Tests/Observability/ResearchObservationTests.cs` (new, 8 tests).**
- On the research rigs, exact integers: 1,000 / 1,000 / 500 RP received, progress 1,000 / 2,000 / 2,500, cost 2,500.
  The third row is the completion row, on the turn the node completes, and turn 4 has no row.
- The bank rows: 1,000,000 milli-CP generated, then drawn 1,000,000 / 1,000,000 / 500,000 and left 0 / 0 / 500,000,
  equal to the account.
- Turn-exact delivery (T1.9): a target order stamped turn t gives its first row on turn t + 1, never on turn t.
- Credit on a non-target node: its own row, all of it credit (500 = 0.20 × 2,500).
- The order: the roster polities by id, then the city-states, each holder's nodes by key.
- The production world for 60 turns: every row equals an independent linear-scan reading of the tables, bit for bit.
  Rows of the player (1) and the AI (2), and completions and bank rows of both, are present (non-vacuity).
  **Superseded in part (verification round 1, RR-R1-01):** the reading is independent in its lookups only. It applies
  the observer's own definitions (`max(before, cost)` on a completion, the sum of the credit deltas), so it cannot
  check them. No test reached a repeatable's later levels or credit paid over several steps, and six mutants of those
  paths survived. Three rig tests now pin them with the rig's own numbers (H-RR §10).
- A populated round trip through the writer and the reader:
  - the exact JSON is pinned;
  - every field reads back bit for bit;
  - the line re-written gives the same sha256;
  - a `v5` line reads as `Recorded = false`.
- The tag: the writer writes `telemetry/v6`; the reader reads v6 to v2 and refuses v7.

**`Sim.Ui.Tests/RunRecordResearchTests.cs` (new, 3 methods, 4 test cases).**
- *The telemetry FILE's research rows are the world's research, turn by turn, every holder.* A session is played as the
  game plays it (30 turns, the player researching, every End Turn saved). For each turn, each row's progress is the
  world's stored progress bit for bit, and its points the difference from the world before. A completion row falls
  exactly on the turn the world completed the node. Every node that moved or completed has its row, and every changed
  account has its bank row.
- The Research sheet is every telemetry research row, coded and exact (40 turns), PL1 and PL2 included. The player's `RC`
  rows fall on the turns of the chronicle's own `RM` events, an independent writer. Every node code is named, and no
  word or undefined code appears.
- The Director's `v4` session and `current-v5` export with one `NR` row. Without a telemetry file the sheet holds the
  `NP` row.

**`Sim.Ui.Tests/SessionCloseTests.cs` (new, 10 methods, 14 test cases, through the real `GameUi` on the headless
harness).**
- The five steps in order, seen from inside the close; the worker close writes byte for byte what the synchronous close
  writes.
- The saving screen drawn alone (every frame-text entry is on the closing surface): no game input taken, End Turn and
  Escape ignored, a second request running nothing twice, one close record.
- The host loop:
  - Escape starts the close and the exit is cancelled;
  - the close button and Alt+F4 are cancelled while the run is saved;
  - the host exits by itself once the workbook is written;
  - after a close without a workbook, the host waits, and the next request closes the window.
- An interrupted close at each of the five steps, recovered by `--export-session`, with the README `close record` row.
- A partial workbook replaced, none left in `runs\`.
- A refused workbook keeps the saving screen up with the reason and the recovery command. The reason is in the export
  log, and the headless refusal appends one line too. A successful close or export writes no log.
- A log without its final line feed is kept even when the close record lists it (the streamed reader keeps the removal's
  exact-rebuild rule).
- A half-year trace exports, and malformed lines are refused by number.

**`SessionExportTests` (changed).**
- The `current-v6` fixture: its content golden, its cells equal to the prototype's, and its Research sheet's rows.
- The pins listed in §6.

**`SessionExportCodesTests` (changed).** The coded-sheet guards (no word, every code defined) run on `current-v6` too.

**Verification round 2 (RR-R2-01..03): five tests.** The detail is in
[`H-RR.md` §11](../../prototypes/m7-sprint1/reports/H-RR.md#11-verification-round-2-fix-round-2026-11-12).
- A city-state end to end: a 64-turn session with one settlement released before the first turn. Its rows are the
  worlds' research for every holder, they come after the empires' rows in every turn (the bank rows too), and the
  Research sheet codes the city-state `ST11`.
- On a rig, the bank rows are 1, 2, `ST2`, `ST3`, while the accounts table holds −4, −3, 1, 2.
- On a rig, a cost that fell below the stored progress: the completion row keeps progress 1,000, received 0, cost 750.
- The workbook step reports the share of the log read after each line, and the saving screen shows it, held mid-step.
- **Corrected:** the 30-turn session of the `RunRecordResearchTests` state test above has the two empires as its only
  holders, because a founded game has no city-state. The test's own description listed "the city-states" and now says
  so; the check itself covers every holder the worlds have.

## 6. Moved pins and goldens, each with its measured attribution

**The telemetry tag `telemetry/v5` → `telemetry/v6`.**
- Cause: the writer's field set gained `research` and `civicsBank` (the T4.20 rule).
- Measured: the written line contains `"schema":"telemetry/v6"`, and the forensic run record's `schemas.telemetry` is
  `TelemetryWriter.Schema`.
- The pins (the same five tests that moved for v5 under D-066 A15):
  - `SessionRecordTests`;
  - `InspectionTests` (the T4.21-5 test; its readable set now includes v6, and its unknown vintage moved to v7);
  - `ForensicIdentityTests`;
  - `TaxGlassBoxTests`;
  - `TelemetryTests`.
- In `SessionExportTests`, the unknown-schema refusal now uses v7, and the close test's cell head reads `v6 `.

**The workbook content goldens** (`SessionExportTests`; the sha256 of the decoded parts re-packed STORED):

| Golden | Was | Now |
|---|---|---|
| `DirectorWorkbookSha` (`director-68`) | `806e0947…40f1fb1` | `97b13e5d…d8392242` |
| `CurrentWorkbookSha` (`current-v5`) | `c5ec8abd…df578313` | `5499123d…5f62e89d0` |
| `CurrentV6WorkbookSha` (`current-v6`) | — | new: `e2e22311…5ad5f74` |
| `EntryOrder` | 13 entries | 14: `xl/worksheets/sheet9.xml` |

The attribution of the two moved goldens is MEASURED part by part. Each fixture was exported with the base and the lane
Release builds, and every decoded entry was compared (§9, `attribution.py`). For both fixtures:

- **Added:** `xl/worksheets/sheet9.xml`, the Research sheet: the header plus `NR 68` / `NR 16`.
- **Changed, the ninth sheet's registration:** `[Content_Types].xml`, `xl/workbook.xml` and `xl/_rels/workbook.xml.rels`.
- **Changed, the README (sheet1):**
  - "eight sheets" became "nine sheets (…; D-086)";
  - the `codes` row names the three Research legend sections;
  - two rows were added: `close record` and `sheet Research`.
- **Changed, the Codebook (sheet7):** 23 legend rows were inserted (`Research:col`, `Research:kind`, `Research:read`).
- **Byte-identical:** `_rels/.rels`, `xl/styles.xml`, and sheets 2–6 and 8 (Manifest, Orders, Trace, Chronicle,
  Telemetry, Forensic).

Every Telemetry cell is therefore unchanged, and the v4 and v5 codebooks are byte-identical (`make_codebook.py --check`).

**A record file's content: the coded chronicle's name lines.** This paragraph was added in verification round 1
(RR-R1-02). It completes the lists in §0 item 3 and §4.3.
- **Cause.** `UiSession.Step` calls `NoteResearchNames` after every step, in live play and in the resume's replay. For
  every node the step's research rows hold, it notes `RN<key>=name` in the coded chronicle's name table. That is how the
  Research sheet's `RN` codes get their names in the Codebook (`Chronicle:names`). The first note wins, and the name is
  the one the player's `RM` event already notes (`ResearchNode.Name`), so no existing line changes.
- **MEASURED on one 300-turn game.** Seed 42, one AI empire, the player researching the cheapest node of each lane,
  every End Turn saved. The same game was played by a base build and by the lane build:
  - the chronicle file went from 254 to 269 lines (5,200 to 5,549 bytes). The 15 added lines are all `RN<key>=` name
    lines, and removing them gives the base file line for line. The 138 event lines are identical;
  - at base, the 109 name lines are exactly the 109 `RN` codes the event lines use. In the lane, the 124 name lines are
    exactly the nodes the research rows hold. 14 of the 15 added nodes were researched only by the AI empire. **Corrected
    (verification round 2, RR-R2-01):** this line said "the AI empire or city-states". A founded game has no city-state
    (CITED from the founding, colonization and revolt code; the verifier's run of this recipe holds holders 1 and 2 only).
    The 15th was researched by the player too, but not completed by turn 300;
  - the workbook's `Chronicle:names` rows went from 115 to 130 (the same 15 codes);
  - the trace and the order log are byte-identical.
- **Secondary evidence.** The verifier measured the same rule on an 850-turn game: 581 name lines for 486 event codes.
- **Fixtures.** The `current-v6` fixture's chronicle has five `RN` name lines and no `RM` event: all five come from this
  hook, as its fixture README says. The `director-68` and `current-v5` fixtures predate it and are not regenerated.
- **Scope.** The hook runs in the per-turn record path (`UiSession.Step`), not in the exit path or `Sim.Ui/Export`, the
  paths the brief names. It is there because the coded chronicle's name lines are the record's only legend: the workbook
  is built from the six record files alone (ADR-059). The alternatives each break a rule. Naming the nodes from the
  build's content at export time would make the workbook depend on the build rather than the record. Leaving the AI's
  and the city-states' nodes unnamed would break the brief's "every code in the Codebook". The code is kept. Whether the
  path reading stands is a question for the coordinator (H-RR §10).

**No simulation pin moved.**
- MEASURED: the trace (every turn's world hash) is identical at base and in the lane: the 850-turn and 1,630-turn
  sessions, and the 16-turn `current-v6` trace, which equals `current-v5`'s byte for byte.
- No golden in `Sim.Tests` that hashes a world changed. The full suite is in §7.

## 7. Validation, mutants: what ran, what was NOT RUN, what was deferred

The tables and the commands are in [`H-RR.md` §7 and §8](../../prototypes/m7-sprint1/reports/H-RR.md#7-validation-what-ran-what-was-not-run-what-was-deferred).
All MEASURED, on this tree:

- **Baseline** (`51ab032`, Release, run by the interrupted run, base binaries):
  - `Sim.Ui.Tests` 1,272 / 0 / 0;
  - `Sim.Tests` 3,430 passed, 0 failed, 36 skipped of 3,466.

  Debug: built, not tested.
- **Tier 1 targeted:** every check PASS.
  - `Sim.Tests` selection: 757 passed, 0 failed, 22 skipped of 779;
  - `Sim.Ui.Tests` full: 1,294 / 0 / 0. That is 22 more than at base: the lane's new tests and cases.
- **Tier 3 full** (under the shared lock):
  - 13 checks PASS;
  - `charters` FAIL, the known false positive at base, fixed on the development branch by `93f2d8a`, not this lane's;
  - the two full suites blocked behind it.
- **Tier 2 integration** (under the shared lock), which runs those two full suites with validate's own command: 7 PASS
  and 1 NOT RUN (manual GUI acceptance).
  - `Sim.Tests` full: 3,438 passed, 0 failed, 36 skipped of 3,474. That is 8 more passed than at base: the new
    `ResearchObservationTests`.
  - `Sim.Ui.Tests` full: 1,294 / 0 / 0.
  - No test that passed at base fails.
- **The Director's files, re-exported with the final binary:** 9.23 and 9.25 s, 698 and 704 MB. The workbook is
  byte-identical to the earlier measurement.
- **Smoke:** SMOKE PASSED: 13 states, 4,123 checks, 0 FAIL, 0 problems.
- **Mutants:** 18 of 18 killed, bound max(60 s, 3 × the clean run), each by a semantic test. M09 shows that check 3 of
  the self-check still catches a split cell that loses a part.
- **NOT RUN:**
  - the bench and the multi-seed trial (heavy, not requested: the bench does not time the telemetry observer, and no
    simulation state changed);
  - the CI-only rows;
  - manual GUI acceptance;
  - the Debug full suites;
  - a Windows close.
- **Verification round 1 (2026-11-12).** The verifier's four findings were all reproduced on `8610ebd`. Nine of its
  mutants survived every lane test. The round added tests and one export fix, then killed 12 of 12 mutants (those nine
  and three of the fix). It re-ran validation, the Debug full suites included. All of this is in
  [`H-RR.md` §10](../../prototypes/m7-sprint1/reports/H-RR.md#10-verification-round-1-fix-round-2026-11-12).
- **Verification round 2 (2026-11-12).** The verifier's three findings were all reproduced on `861f9f9`: five of its
  mutants survived every lane test. The round added five tests and a test seam for the close's progress, and corrected
  the city-state claims. The five mutants and two more were then killed, 7 of 7, and validation was run again: the
  Release full suites pass (`Sim.Tests` 3,443 / 0 / 36 skipped, `Sim.Ui.Tests` 1,301 / 0 / 0), and so does the Debug
  `Sim.Ui.Tests`. The Debug `Sim.Tests` had one failure, a wall-clock bench budget that also fails at base when run alone
  (MEASURED). All of this is in
  [`H-RR.md` §11](../../prototypes/m7-sprint1/reports/H-RR.md#11-verification-round-2-fix-round-2026-11-12).

## 8. Limitations

1. **A completion row's `Progress` is `max(progress before, cost)`.** The system removes a completed node's progress row,
   so the record cannot read the final stored value. The system sets it to exactly the cost when a credit reaches it.
   In one theoretical rounding case, `have + amount` can exceed the cost by one ulp; the row then says the cost, one ulp
   below what the system held for that instant. INFERRED from the code; not observed. A node whose cost fell below
   its stored progress completes with that progress and receives nothing; the row says so (tested since verification
   round 2, RR-R2-02).
2. **The ADR-051 RP reserve (off in the shipped content) is not itemized.** A reserve draw shows only in `Received`.
3. **A kill in the middle of a file write** (an End Turn save, or close steps 1–3, each milliseconds) can still leave a
   record file the export refuses. The telemetry append is prefix-safe, but the trace and chronicle are rewritten whole.
4. **Windows is not measured here.** The ghosting, Defender and the laptop's speed are INFERRED, and the saving screen was
   tested through the headless harness, not in a real window. CI's Windows smoke does not close a session.
5. **The export still holds the whole log in memory** as bytes (356 MB at 1,630 turns). See P2.
6. **The resume and the CLI replay of games past turn 1270 are still refused** (kernel; P1).

## 9. Raw outputs

All under `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/rr2/` (scratch: lost with the
container):
- `logs/close850-{base,new}.log`, `logs/close1630-{base,new}.log`: the harness runs;
- `closebench/Program.cs`: the harness;
- `bin/timeit.py`: the wall time and peak RSS of a process;
- `bin/attribution.py`: the part-by-part workbook diff;
- `export-base.speedscope.json`, `export-new.speedscope.json`: the profiles of the export of the Director's files;
- `evidence/trace-1630-{base,new}.csv`: the two 1,630-turn traces, byte-identical.

The interrupted run's base measurements (`scratchpad/rr/notes/measurements.md`) agree with these within load noise: an
in-process export of 18.08 s with a peak of 2,786 MB at load ≈ 11.

## 10. Proposals and questions for the Director

- **P1 (kernel, needs the Director's sign-off).** `SessionTrace.Parse` should read the year as the number the writer
  writes: `Row.Year` as a `double`, parsed with `NumberStyles.Float`.
  - Blast radius: `Sim.Core/Kernel/SessionTrace.cs`, and the year column `sim replay`/`sim inspect` print in
    `Sim.Cli/Program.cs`. `ReplayProof` does not compare years.
  - No world, hash or golden changes: the trace is an observer.
  - Without it, "Continue last game", `sim replay` and `sim inspect` refuse every session past turn 1270 (MEASURED for
    the resume).
  - Under CLAUDE.md this is a change request against the frozen kernel. It is not made here.
- **P2 (export memory).** Stream the telemetry from the file instead of reading it whole. The sha256 and the line reader
  would both run over the stream. This saves roughly the log's size in peak memory (INFERRED: 356 MB at 1,630 turns),
  with no change to what is written.
- **P3 (export time).** Encode and check the lines in parallel, in order. Each line's work is independent, and the results
  would be placed by index, so the output is the same bytes. INFERRED: about 2–3× faster on 4 cores.
- **P4 (only if P2 and P3 are not enough).** Alternatives to "the Telemetry sheet is the log's only copy" (ADR-059's
  amendment):
  - keep the raw log compressed beside the workbook in `runs\.record` (the Director's 173,040,889-byte log zipped to
    12,782,794 bytes, per the zip sent);
  - or split or summarize the Telemetry sheet. A summary drops exactness, and is not recommended.
- **P5 (packaging, a CI change, not made).** A third launcher, `Rebuild the run workbook.cmd`, in the game zip, running
  `start /wait "" "app\Sim.Ui.exe" --export-session runs`. The Director would then need no command prompt.
- **P6 (governance record, not made).** ADR-059 still describes eight sheets, `telemetry/v5` and the close on the game
  thread. The change is recorded in `docs/m6-session-export/README.md` §8 and here. An amendment-log entry in ADR-059
  (the ninth sheet, codebook v6, the close on a worker, the trace check) is left to the coordinator: `docs/adr` is
  outside this lane's paths.
