# H-RR: M7 lane RR. The run record keeps research; closing the game keeps the run workbook (D-086)

**Lane:** RR. **Branch:** `lane/RR`, cut from `51ab032` on the development branch `claude/new-session-yavghm`.
**Authority:** [D-086](../../../docs/d086-run-record-research-and-exit-export-2026-11-11.md), readings DE1-R1..R3 (a),
DE2-R1 (a) and DE-R (a). DE-R (a) is the scoped `Sim.*` exception: `Sim.Ui`'s exit path and session export with its
codebook, and `Sim.Core/Observability`. Nothing in `Sim.Core/Kernel` changes, and no simulation state or world hash
changes.

**This report is the M7 pass's record of the lane.** The full design, every measurement and the Director's proposals are
in [`docs/m6-plan/run-record-research-and-exit-2026-11.md`](../../../docs/m6-plan/run-record-research-and-exit-2026-11.md)
("the main report"). The recovery note for the Director is in
[`docs/m6-session-export/README.md` §8](../../../docs/m6-session-export/README.md#8-dated-note-2026-11-11-d-086-research-in-the-run-record-and-a-close-that-keeps-the-workbook).

**Resumed run.** The lane was stopped by the Director's throttle at about 11:35 UTC, mid-step. This run took over:
- two WIP commits (`88f125e`, `0a631db`);
- uncommitted edits in 16 files.

They were treated as unreviewed. Every claim the WIP carried was re-measured here, or is labelled as the interrupted
run's.

## 0. Summary

- **DE1, research in the run record: built and tested.**
  - `telemetry/v6` carries every holder's research rows and Civics Points bank rows, for the player's empire, each AI
    empire and every city-state. **Corrected in part (verification round 2, RR-R2-01):** a founded game has no
    city-state, so the sessions recorded here hold the empires' rows only. City-state rows are tested on the rigs and on
    a session with a released settlement (§11).
  - Codebook v6 reads them.
  - The workbook's ninth sheet, `Research`, shows them coded.
  - Older sessions export with an `NR` row.
- **DE2, the close.**
  - **Diagnosed:** the whole close ran on the game thread, so no frame was drawn for 14.2 s with a 3.0 GB peak (MEASURED,
    850 turns).
  - **Fixed:** a worker thread and a saving screen; a recoverable, whole-write close; failures logged.
  - **Measured faster and leaner:**
    - the Director's own files: 12.5–13.3 s / 1,290 MB → 8.8–9.7 s / 698 MB;
    - the in-game close at 850 turns: 14.2 s / 3,050 MB → 9.5 s / 939 MB.
- **Found by measurement:** every game longer than 1,270 turns lost its workbook, because the kernel's trace reader
  refuses half-year years such as `5920.5`. The export is fixed in scope. The resume (kernel) is a PROPOSAL.
- **No simulation pin moved.** MEASURED: the 850-turn and 1,630-turn traces, which hold every turn's world hash, are
  byte-identical at base and in the lane.
- **Verification round 1 (2026-11-12), §10.** The verifier's four findings were all reproduced, then fixed:
  - tests for a repeatable's levels, exposure credit over several steps, an empty v6 step, the `NX` row, the trace
    column rule and the `--keep-raw-telemetry` close;
  - every failure of a headless export now reaches the export log;
  - the chronicle's research name lines are attributed in the reports.

  Twelve bounded mutants were killed, and the Debug and Release full suites pass.
- **Verification round 2 (2026-11-12), §11.** The verifier's three findings were all reproduced, then fixed:
  - tests for a city-state end to end (a released settlement, 64 turns), the bank rows' order, a cost that fell below
    the stored progress, and the workbook step's progress as reported and as the saving screen shows it (a test seam,
    null in the game);
  - the city-state claims of the fixture README, a test's docstring and the reports corrected: a founded game has no
    city-state.

  Seven bounded mutants were killed. The Release full suites pass. In Debug, `Sim.Ui.Tests` passes; `Sim.Tests` had
  one failure, a wall-clock bench budget that fails at base too (§11.6).

## 1. What exists (files)

The main report's §2 table lists each file with its exact `git diff --numstat` figures. In short:

- **Research rows:**
  - `Sim.Core/Observability/ResearchObservation.cs` (new);
  - `TurnRecord.cs` (`ResearchStep`, `CivicsBankStep`);
  - `Observer.cs`;
  - `TelemetryWriter.cs` (tag `telemetry/v6`);
  - `Forensic/TelemetryRecordReader.cs` (`TelemetryResearch`, `Recorded` false on v2–v5).
- **Codebook v6:** `docs/m6-session-export/prototype/spec.py` and `make_codebook.py`, and the generated
  `codebook-telemetry-v6.tsv` and `.json`.
- **The export:**
  - `Sim.Ui/Export/SessionWorkbook.cs` (the streamed reader, `CheckTrace`, the README rows, the whole write);
  - `SessionWorkbook.Research.cs` (new);
  - `TelemetryCellCodec.cs`, `TelemetryCodebook.cs`, `WorkbookCodes.cs`, `SessionExportCommand.cs`.
- **The close:**
  - `Sim.Ui/SessionClose.cs` (new);
  - `GameUi.Closing.cs` (new: the saving screen);
  - `GameUi.cs` (`BeginClose`, `HostExiting`, `HostShouldExit`);
  - `SimUiGame.cs`;
  - `UiSession.cs` (the workbook and removal steps; the research nodes' names);
  - `FrameText.cs`;
  - `Headless/UiFrameHarness.cs` (Host mode).
- **Tests:**
  - `Sim.Tests/Observability/ResearchObservationTests.cs`;
  - `Sim.Ui.Tests/RunRecordResearchTests.cs`;
  - `Sim.Ui.Tests/SessionCloseTests.cs`;
  - the fixture `Sim.Ui.Tests/Fixtures/session-export/current-v6/`;
  - pins moved in six files (§5), and a `current-v6` case added to `SessionExportCodesTests`.

## 2. Design

The main report holds the detail; this is the short form.

- **Where the rows travel: the telemetry line** (main report §3.1). It is one observation, one writer and one prefix-safe
  append. The resume rebuild, `sim inspect`, the export's self-check and the raw log's removal and recovery cover the
  rows with no new code. MEASURED cost: +0.35–0.41 % of telemetry. The T4.20 rule moved the tag to `telemetry/v6`.
- **A row:** `ResearchStep(Holder, Node, Tree, Received, Credit, Progress, Cost, Level, Completed)`.
  - `Received` = progress after − progress before;
  - `Credit` = Σ over sources of `ResearchCredits` next − prev;
  - `Progress` = the stored row after the step, or `max(before, cost)` on a completion, since the system removes the
    row;
  - `Cost` = `ResearchQuery.EffectiveCost(prev, …)`;
  - `Completed` = a completed row appeared, or the level rose.

  A row is written for every node that moved, completed or levelled. A bank row, `CivicsBankStep(Holder, GeneratedMilli,
  DrawnMilli, BalanceMilli)`, is written for every account that changed. The rows are ordered by an integer composite
  key (roster polities, then city-states, then the node), looked up by binary search. There is no Dictionary, float or
  RNG.
- **The Research sheet:** columns `T HD KD RN LN PR AC PG CS LV GN DR BL`; kinds `RP RC BK NR NX`; every code in the
  Codebook legend. Node names are in `Chronicle:names`.
- **The close:**
  - `SessionClose` runs save → finalize → forensic close → workbook → removal on one worker thread.
  - `GameUi` takes no input and draws only the saving screen.
  - `HostExiting` cancels every window exit until the workbook is written. After a failure it waits for the player's
    next request.
  - The workbook is written to `.partial` and then renamed.
  - A failure goes to the screen and to `runs\.record\export-STAMP.log`.
- **The export's speed-up:**
  - the log is streamed line by line;
  - each line is parsed once;
  - the sha256 is computed once;
  - self-check 3 compares the re-joined cells with the verified cells instead of decoding them again. The decoder is a
    pure function, so the guarantee is unchanged.

## 3. Evidence: MEASURED and INFERRED (main claims)

| Claim | Label | Figure, source |
|---|---|---|
| The base close drew no frame for its whole duration | CITED + MEASURED | base `OnExiting` → `SaveOnExit` on the game thread; 14.23 s for 850 turns |
| Base close peak memory | MEASURED | 3,050 MB resident in the workbook step (+2,664 MB), 12,449 MB allocated |
| The Director's freeze was this close, killed during the workbook or removal step | INFERRED | the telemetry log was not removed and no workbook appeared; no log exists |
| Windows marks the window "Not Responding"; Defender scans the 173 MB log | INFERRED | not measurable on this host |
| Lane close, 850 turns | MEASURED | 9.49 s in all (workbook 9.11 s), peak 939 MB (`SessionClose` timed by the measurement harness; the saving screen drawn while it runs is tested on the headless frame harness) |
| Lane close, 1,630 turns (year 6100, 357 MB telemetry) | MEASURED | 19.92 s (workbook 19.08 s), peak 1,570 MB; workbook 16,678,978 bytes |
| Base close, 1,630 turns | MEASURED | refused after 0.43 s, `record-unreadable` `'5920.5'`, no workbook |
| Headless export of the Director's files | MEASURED | base 12.49–13.26 s / 1,290 MB; lane 8.84–9.66 s / 698 MB (3 runs each) |
| Headless export, 1,630 turns | MEASURED | base refused (exit 2) in 0.53 s; lane 18.97 / 19.07 s, 1,559 / 1,408 MB |
| "Continue last game" past turn 1270 | MEASURED | refused by base and lane (`SessionUnreadableException`, the trace); the 850-turn control resumes in 69.2 s |
| No simulation change | MEASURED | the traces are identical at base and in the lane (850 and 1,630 turns); `current-v6`'s trace = `current-v5`'s |
| The second decode was about a fifth of the base export | MEASURED | `SelfCheck` 21.6 % of the traced base export, the read-back 3.6 % of that |
| The research rows equal the world's research, turn by turn | MEASURED | `RunRecordResearchTests` (30 turns, the session's own worlds) and `ResearchObservationTests` (60 turns). Since verification round 2, a city-state's too: 64 turns with a released settlement (§11.4) |

## 4. Tests

The main report §5 holds the list. In short:
- `ResearchObservationTests`: 8 tests;
- `RunRecordResearchTests`: 3 methods, 4 cases;
- `SessionCloseTests`: 10 methods, 14 cases;
- `SessionExportTests` and `SessionExportCodesTests`: the `current-v6` fixture and the moved pins.

**Superseded in part (verification round 1, §10):** the counts are now 11 tests, 4 methods with 5 cases, and 12 methods
with 17 cases. The new tests cover a repeatable's later levels, exposure credit paid over several steps, a v6 step with
no research, the `NX` row, a seven-column trace line, the `--keep-raw-telemetry` close, and any headless export failure.
**Superseded again (verification round 2, §11):** 13 tests; 5 methods with 6 cases; 14 methods with 19 cases. The new
tests cover a city-state end to end, the bank rows' order, a cost that fell below the stored progress, and the workbook
step's progress as reported and as the saving screen shows it.

They cover:
- exact integer rows;
- the turn-exact completion row;
- the turn-exact order delivery (T1.9);
- a populated round trip (T1.1/T1.3);
- older vintages;
- the five close steps in order;
- the saving screen;
- the host loop;
- recovery after an interruption at each step;
- the whole write;
- the logs;
- the exact-rebuild removal rule;
- the half-year trace.

## 5. Moved pins and goldens (measured attribution: main report §6)

- **`telemetry/v5` → `v6`** in `SessionRecordTests`, `InspectionTests` (readable set, unknown vintage v7),
  `ForensicIdentityTests`, `TaxGlassBoxTests` and `TelemetryTests`. In `SessionExportTests`: the unknown schema (v7) and
  the close test's cell head (`v6 `). Cause: the writer's new field set (T4.20).
- **`DirectorWorkbookSha`** `806e0947…` → `97b13e5d…`, and **`CurrentWorkbookSha`** `c5ec8abd…` → `5499123d…`. The
  base and lane exports were compared part by part:
  - added: `sheet9.xml` (the Research sheet);
  - changed: `[Content_Types].xml`, `workbook.xml` and `workbook.xml.rels` (its registration); README (sheet1:
    "nine sheets", the `codes` row, the new `close record` and `sheet Research` rows); Codebook (sheet7: +23 legend
    rows);
  - every other part, every Telemetry cell included, is byte-identical.
- **`CurrentV6WorkbookSha`** `e2e22311…` is new. **`EntryOrder`** gains `sheet9.xml`.
- **No world-hash pin moved.**
- **Added in verification round 1 (RR-R1-02): a record file's content, not a pin.** The coded chronicle also gains one
  `RN<key>=name` line per research node the research rows hold (`UiSession.Step` → `NoteResearchNames`), and the
  workbook's `Chronicle:names` gains the same names. MEASURED on a 300-turn game played by base and lane: +15 name lines
  (254 → 269), every other line identical; `Chronicle:names` 115 → 130 rows (main report §6, §10 here).

## 6. Limitations

These are the main report's §8:
- a completion row's `Progress` is `max(before, cost)`, so in a theoretical case it can be 1 ulp below the system's
  instant value (the other branch, a cost that fell below the stored progress, is tested since round 2: §11.4);
- the RP reserve (off) is not itemized;
- a kill in the middle of a file write is not covered;
- Windows is not measured here;
- the whole log is held in memory;
- the resume past turn 1270 is still refused (kernel).

## 7. Validation: what ran, what was NOT RUN, what was deferred

All runs used the same environment: Release unless marked, `nice -n 19`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, the solution
named, and logs in `TestResults/validation/<stamp>-<tier>/`.

**Baseline (before), on `51ab032`.**

| Run | Result |
|---|---|
| Release suites, run by the interrupted run of this lane (10:10–12:05 UTC) | `Sim.Ui.Tests` 1,272 passed / 0 failed / 0 skipped; `Sim.Tests` 3,430 passed / 0 failed / 36 skipped of 3,466 |
| `validate.sh full` at base | 13 PASS; `charters` FAIL (12 `unknown id` lines); 8 NOT RUN |
| Debug | built with 0 errors; the suites were NOT RUN at base |

The baseline suites' trx files are in `scratchpad/rr/baseline/release/`. They contain none of the lane's new test names,
so they were run on base binaries.

**After (this run).**

| Tier / check | Command | Result (MEASURED) |
|---|---|---|
| Tier 1 targeted | `scripts/validate.sh targeted --base 51ab032` | doc-links PASS; export-codebook PASS; build PASS; `simtests-selected` PASS, 757 passed / 0 failed / 22 skipped of 779 (126 classes; the 97 recorded slow tests left out, as the policy says); `uitests-full` PASS, 1,294 / 0 / 0 |
| Tier 3 full, its checks | `flock <shared lock> scripts/validate.sh full` | 13 PASS: selector self-test, banned constructs, read isolation, research tool tests, research audit, calibration report, unlock audit, consequence audit, audit tests, tree closure, doc-links-all, readonly proof, build. `charters` FAIL (see below). 8 NOT RUN: the two heavy checks (bench, multi-seed trial), not requested; three CI-only; one manual; `simtests-full` and `uitests-full`, blocked behind `charters` |
| Tier 2 integration, which runs Tier 3's two blocked suites with validate's own command | `flock <shared lock> scripts/validate.sh integration --base 51ab032` | 7 PASS, 1 NOT RUN (manual GUI acceptance): banned constructs, read isolation, doc-links, export-codebook and build PASS; **`simtests-full` PASS, 3,438 passed / 0 failed / 36 skipped of 3,474 (28 m 38 s)**; **`uitests-full` PASS, 1,294 / 0 / 0 (14 m 37 s)** |
| UI smoke (the exit path changed) | `dotnet Sim.Ui.dll --smoke --monkey 300 --report … --timeout-minutes 30` | SMOKE PASSED: 13 states, 4,123 checks (3,785 pass, 338 not offered, 0 FAIL), 0 problems, 139,775 frames, 3,900 monkey actions, largest draw list 129,382 vertices, 220.3 s |
| Codebooks | `python3 -I docs/m6-session-export/prototype/make_codebook.py --check` | current: v4, v5, v6 |
| Doc links | `scripts/check-doc-links.py` on the changed Markdown | 0 broken |
| Prototype determinism scan | `prototypes/m7-sprint1/scripts/check-proto-determinism.sh` | CLEAN (109 files; no prototype code changed) |
| Debug, targeted | `dotnet test -c Debug` with filters | 75 / 75 UI (export, close, research, record); 45 / 45 Sim (observation, telemetry, forensic); 18 / 18 (research and close classes after the last tests were added) |
| Mutants | §8 | 18 of 18 killed |

The `charters` FAIL is the known checker false positive on `51ab032`: 12 `unknown id` lines in
`docs/m7-plan/unit-catalogue-and-progression.md`. It is fixed on the development branch by `93f2d8a`, is not this lane's,
and is identical at base.

**Against the baseline:** `Sim.Tests` 3,430 → 3,438 passed (+8: `ResearchObservationTests`), 36 skipped at both;
`Sim.Ui.Tests` 1,272 → 1,294 (+22: the lane's new tests and cases). No test that passed at base fails.

**The Director's files, re-exported with the final binary** (the Release binary built at 23:16 UTC, after the last code commit; run once the integration suites had finished): 9.23 / 9.25 s,
698 / 704 MB. The workbook was byte-identical to the earlier lane measurement (sha256 `40ba3d35…`).

**NOT RUN, and why.**
- **The bench (`--heavy`).** It times `TurnExecutor.Step` with its own phase observer, which this lane does not touch: the
  telemetry observer is not in it. The play with the research observation, MEASURED:
  - 850 turns: 74 s at base, 73 s in the lane;
  - 1,630 turns: 147 s and 141 s.

  These are single runs, so the difference is noise.
- **The multi-seed trial (`--heavy`).** No simulation state changed: the traces are identical.
- **The CI-only rows** (cross-process determinism, platform smoke, nightly calibration) run on push, not here.
- **Manual GUI acceptance.** The saving screen was tested on the headless frame harness only, never in a real window.
- **The Debug full suites.** Machine time: `validate.sh` runs Release, and Debug ran the targeted filters above.
  **Superseded (verification round 1):** both Debug full suites ran in §10.6.
- **A Windows close.** No Windows host here.

**Deferred** to the Director or the coordinator, as proposals in §9:
- P1: the kernel's trace reader;
- P6: the ADR-059 amendment;
- P2–P5.

## 8. Mutants (bounded; each kill backed by a semantic test)

**Method (MEASURED).** `scratchpad/rr2/bin/mutants.py` ran in a copy of the lane tree at `a8ca9ef`. For each mutant it:
1. ran the named test on the clean tree, in Debug (the clean time);
2. applied one textual edit and rebuilt;
3. ran the same test with a bound of **max(60 s, 3 × the clean time)**;
4. restored the file byte for byte.

A non-terminating mutant would be recorded as such; none occurred. The full records are in
`scratchpad/rr2/mutants/mutants.json`.

**18 of 18 KILLED**, each by a semantic test whose property the system ought to have. The clean and mutant times are in
seconds.

| # | Mutant (file) | Killed by | Clean / mutant |
|---|---|---|---|
| M01 | no completion row: a completed node written as received points (`ResearchObservation`) | `ResearchObservationTests.TheRowsAreThePointsEachTurn_AndTheCompletionRowIsOnTheTurnTheNodeCompletes` | 3.0 / 2.9 |
| M02 | points received written as the accumulated progress | same | 2.7 / 2.7 |
| M03 | the city-states' research dropped | `…TheRowsAreTheRosterPolitiesThenTheCityStates_EachByNodeKey` | 2.6 / 2.7 |
| M04 | CP drawn read from the balance, not the spent account | `…TheCivicsBankRows_AreTheGeneratedDrawnAndLeftMilliCp_OfEachTurn` | 2.5 / 2.6 |
| M05 | the reader loses the completion flag (`TelemetryRecordReader`) | `…ThePopulatedResearchRows_RoundTripThroughTheWriterAndTheReader_BitForBit` | 6.0 / 5.4 |
| M06 | the Research sheet swaps `RP` and `RC` (`SessionWorkbook.Research`) | `RunRecordResearchTests.TheResearchSheet_IsEveryTelemetryResearchRow_…` | 26.3 / 26.4 |
| M07 | a v2–v5 session's sheet loses its `NR` row | `…ASessionWrittenBeforeV6_ExportsWithTheResearchSheetMarkedNotRecorded` (both cases) | 3.4 / 3.8 |
| M08 | the streamed reader stops checking the final line feed | `SessionCloseTests.ALogWithoutItsFinalLineFeed_IsKept_EvenWhenTheCloseRecordListsIt` | 2.6 / 2.7 |
| M09 | a split cell loses its last continuation part (`TelemetryCellCodec.Split`) | `SessionExportTests.ALoweredCellLimit_…`: the export refused by self-check 3, `telemetry-roundtrip-mismatch: … the workbook's cell is not the cell decoded back to the line` (re-run to read the message) | 3.1 / 3.0 |
| M10 | the close skips its forensic close record (`SessionClose`) | `SessionCloseTests.TheCloseRunsItsFiveStepsInOrder_…` | 8.5 / 5.6 |
| M11 | a second close request starts a second close (`GameUi.BeginClose`) | `…WhileTheCloseRuns_…ASecondRequestRunsNothingTwice` | 5.9 / 6.5 |
| M12 | the window draws the game under the saving screen (`GameUi.Draw`) | same (every frame-text entry must be on the closing surface) | 5.7 / 5.8 |
| M13 | the host lets the window exit at the first request (`GameUi.HostExiting`) | `…TheHostCancelsTheExitWhileTheRunIsSaved_…` | 5.2 / 5.5 |
| M14 | the workbook written in place, not through `.partial` | `…APartialWorkbookLeftByAnInterruptedWrite_IsReplaced_…` | 5.5 / 5.4 |
| M15 | a refused headless export leaves no export-log line (`SessionExportCommand`) | `…ARefusedWorkbook_KeepsTheSavingScreenUp_…` | 5.1 / 5.6 |
| M16 | the export checks the trace with the kernel reader again | `…ATraceWithHalfYearTurns_Exports_AndAMalformedTraceIsStillRefusedByLine` | 2.4 / 2.5 |
| M17 | the record does not name the research rows' nodes (`UiSession`) | `RunRecordResearchTests.TheResearchSheet_…` (every `RN` code named) | 25.6 / 25.4 |
| M18 | the observer records no research (`Observer`) | `RunRecordResearchTests.TheTelemetryFilesResearchRows_AreTheWorldsResearchTurnByTurn_EveryHolder` | 17.1 / 17.3 |

After the run, the workspace's sources were compared with `git archive a8ca9ef` (`diff -r`, build output excluded): they
are identical, so every mutant was restored.

## 9. Proposals for the Director (main report §10)

- **P1:** the kernel's `SessionTrace.Parse` should read the year as a number: a change request against the frozen
  kernel, restoring the resume, `sim replay` and `sim inspect` past turn 1270.
- **P2:** stream the log in the export (memory).
- **P3:** encode the lines in parallel, in order (time).
- **P4:** alternatives to the Telemetry sheet as the log's only copy, only if P2 and P3 fall short.
- **P5:** a third launcher, `Rebuild the run workbook.cmd` (a CI change).
- **P6:** an ADR-059 amendment-log entry (nine sheets, codebook v6, the close on a worker, the trace check), left to
  the coordinator (`docs/adr` is outside this lane's paths).

No question for the Director is genuinely open: the readings applied are D-086's (a) readings.

## 10. Verification round 1 (fix round, 2026-11-12)

**What this round did.** The verifier's four findings on `8610ebd` (RR-R1-01..04) were each reproduced on that tree,
in this lane's clone, before any change. A reproduced finding was then fixed in scope: Sim.Ui's session export, and
tests in `Sim.Tests` and `Sim.Ui.Tests`. Nothing in `Sim.Core` changed this round, and no simulation state, world hash
or golden moved. Every figure below is MEASURED here unless it is labelled otherwise. The logs and records are listed
in §10.9.

### 10.1 Verdicts

| Finding | Severity | Verdict | Reproduction on `8610ebd` (MEASURED) | Change |
|---|---|---|---|---|
| RR-R1-01 | major | **REPRODUCED** | The verifier's mutants V01–V05 and V11, applied one at a time with its exact edits, each passed `ResearchObservationTests` 8/8 and `RunRecordResearchTests` 4/4. `research.json` has `tuning.repeatableLevels` true, 13 nodes with `repeatable.levels`, and `tuning.foreignExposure` enabled at 0.01 a year | three rig tests in `ResearchObservationTests`; they kill all six (§10.4) |
| RR-R1-02 | minor | **REPRODUCED** | The same 300-turn game played by a base build and by the lane build: the chronicle goes from 254 to 269 lines. The 15 added lines are all `RN<key>=` name lines, and every other line is identical. `Chronicle:names` goes from 115 to 130 rows (main report §6) | the change is attributed in README §8, main report §0, §4.3 and §6, and §5 here, with the superseded statements marked; the hook's place is a question for the coordinator (§10.8). No code change |
| RR-R1-03 | minor | **REPRODUCED** | V32, V25 and V21 each passed every class named: `RunRecordResearchTests`, `SessionCloseTests`, `SessionExportTests` and `SessionExportCodesTests`, plus `SessionRecordTests` and `SessionRecordFolderTests` for V25 and V21 | three tests (§10.3); they kill all three |
| RR-R1-04 | nit | **REPRODUCED** | Run as an unprivileged user (`setpriv`, uid 65534) with the trace mode 000, `--export-session` ended with exit 134 and `Unhandled exception. System.UnauthorizedAccessException: Access to the path '…/trace-20261111-120000-a1.csv' is denied.`, and wrote no export log | `SessionExportCommand.Run` reports every other exception as it reports an I/O failure: on stderr and in the export log, with exit 2. A test seam and a two-case test come with it. After the fix the same run exits 2, and the log's only line names the reason (§10.5) |

### 10.2 What changed

Commit `fe45f0b` (measured `git diff --numstat 8610ebd fe45f0b`):

| File | +/− | What |
|---|---|---|
| `Sim.Ui/Export/SessionExportCommand.cs` | +19/−3 | the catch-all (stderr, export log, exit 2); the `export` test seam (null in the launcher); the exit-code doc |
| `Sim.Tests/Observability/ResearchObservationTests.cs` | +146/−5 | three tests and the `Offer` helper; the linear-scan reading's doc says what it can and cannot check |
| `Sim.Ui.Tests/RunRecordResearchTests.cs` | +42 | the `NX` test |
| `Sim.Ui.Tests/SessionCloseTests.cs` | +87 | the seven-column case, the `--keep-raw-telemetry` close, the headless failure theory |
| `docs/m6-session-export/README.md` | +11/−3 | §8: the chronicle's name lines in what the close writes; a failed `--export-session` is logged |
| `docs/m6-plan/run-record-research-and-exit-2026-11.md` | +35/−3 | §6: the chronicle attribution; superseded statements marked in §0, §4.3 and §5 |
| this report | +8 | §4 and §5 marked; this section follows in the report commit |

### 10.3 The new tests (each a semantic property, with the rig's or the record's own numbers)

- `ResearchObservationTests.EveryLevelOfARepeatable_IsACompletionRow_OnTheTurnThatLevelCompletes_WithThatLevelsCost`
  - The rig is `ResearchLevelTests`': rep, levels costing 1,000, 1,250 and 1,562.5, at 1,000 RP a turn.
  - The rows are received 1,000 / 1,000 / 250 / 1,000 / 562.5, progress 1,000 / 1,000 / 1,250 / 1,000 / 1,562.5, cost
    1,000 / 1,250 / 1,250 / 1,562.5 / 1,562.5, and level 1 / 1 / 2 / 2 / 3.
  - The completion rows fall on turns 1, 3 and 5 only, which is turn-exact for every level. The world holds one
    completed row.
- `ResearchObservationTests.ForeignExposureCredit_PaidOverSeveralSteps_IsEachStepsCredit_WithinWhatTheNodeReceived`
  - The exposure offered is 100, then 300, then 300 (the DiffusionSystem table, set directly). Node x is also the RP
    target.
  - The rows are credit 100 / 200 / 0 and received 1,100 / 1,200 / 200, with the completion on turn 3.
  - The world's provenance row for the source holds 300.
- `ResearchObservationTests.AV6LineWhoseStepResearchedNothing_ReadsAsRecorded_WithNoRows`
  - A line with `"research":[],"civicsBank":[]` reads as `Recorded` true, with no rows.
- `RunRecordResearchTests.AV6SessionWithNoResearchRowOnAnyLine_ExportsOneNxRow_NotTheNotRecordedRow`
  - The `current-v6` session is used with both arrays emptied on all 16 lines, and its close record lists the emptied
    file.
  - The Research sheet is its header and `NX 16`. The README counts the 16 turns, `NX` is defined in the legend, and the
    workbook stays coded.
- `SessionCloseTests.ATraceWithHalfYearTurns_…` (extended)
  - A seven-column line with an integer year is refused by the frozen kernel reader.
  - The export refuses the same line by its number, with "expected 6 columns" and "found 7".
- `SessionCloseTests.WithKeepRawTelemetry_TheCloseKeepsTheLog_AndReportsNothing`
  - With the developer switch, the close writes the workbook and keeps the raw log.
  - It reports nothing: no error line and no export log.
- `SessionCloseTests.AnyHeadlessExportFailure_IsTheExportLogsLastLine_AndTheCommandExits2` (two cases:
  `UnauthorizedAccessException`, `InvalidOperationException`)
  - The command resolves the session's own manifest and exits 2.
  - It names the failure on stderr, and the export log's last line is `export-session failed - <type>: <message>`.
  - No workbook is written.

### 10.4 Mutants (bounded; every kill by a semantic test)

**Method.** `scratchpad/m7c/RR-fix1/tools/mutants.py` ran in this clone. For each mutant it:
1. applied one textual edit (the old text must occur exactly once) and built Debug;
2. ran each named test class on its own, bounded by **max(60 s, 5 × the class's clean time)**;
3. restored the file byte for byte and checked its sha256.

Each class runs on its own because two classes that drive the process-wide ImGui context must not run at once (§10.7).
No mutant hung. `git status` was clean after each run.

**Phase A, the reproduction, on `8610ebd`.** The edits are the verifier's, verbatim. Clean times: `ResearchObservationTests`
29.7 s (8), `RunRecordResearchTests` 41.5 s (4), `SessionCloseTests` 39.0 s (14), `SessionExportTests` 36.6 s (30),
`SessionExportCodesTests` 14.5 s (9), `SessionRecordTests` 5.6 s (7), `SessionRecordFolderTests` 8.6 s (10).

| Mutant | What | Verdict on `8610ebd` |
|---|---|---|
| V01 | a level completion gets no row | SURVIVED: 8/8 and 4/4 |
| V02 | a level completion is not marked completed | SURVIVED: 8/8 and 4/4 |
| V03 | the cost read on NEXT instead of PREV | SURVIVED: 8/8 and 4/4 |
| V04 | foreign-exposure credit dropped from `AC` | SURVIVED: 8/8 and 4/4 |
| V05 | `AC` written as the cumulative credit | SURVIVED: 8/8 and 4/4 |
| V11 | the reader reads an empty v6 research array as not recorded | SURVIVED: 8/8 and 4/4 |
| V21 | `KeepRawTelemetry` no longer skips the removal step | SURVIVED: 14/14, 30/30, 9/9, 7/7, 10/10 |
| V25 | the trace check accepts seven or more columns | SURVIVED: 14/14, 30/30, 9/9, 7/7, 10/10 |
| V32 | the Research sheet loses its `NX` row | SURVIVED: 4/4, 30/30, 9/9, 14/14 |

**Phase B, the kills, on `fe45f0b`.** Clean times: `ResearchObservationTests` 32.6 s (11), `SessionCloseTests` 45.2 s
(17), `RunRecordResearchTests` 42.2 s (5). The bounds were 163 s, 226 s and 211 s.

| Mutant | Killed by | Failure | Clean / mutant (s) |
|---|---|---|---|
| V01 | `EveryLevelOfARepeatable_…` | "no row for rep on turn 3" | 32.6 / 32.8 |
| V02 | same | a value differs (turn 3: not completed, progress 0) | 32.6 / 34.4 |
| V03 | same | a value differs (the next level's cost) | 32.6 / 34.5 |
| V04 | `ForeignExposureCredit_…` | a value differs (credit 0) | 32.6 / 33.1 |
| V05 | same | a value differs (credit 300 on turn 2) | 32.6 / 33.4 |
| V11 | `AV6LineWhoseStepResearchedNothing_…` | `Recorded` false | 32.6 / 32.6 |
| V21 | `WithKeepRawTelemetry_…` | the error writer holds a "raw telemetry log kept" line | 45.2 / 45.6 |
| V25 | `ATraceWithHalfYearTurns_…` | no exception for the seven-column line | 45.2 / 44.4 |
| V32 | `AV6SessionWithNoResearchRowOnAnyLine_…` | the sheet has no `NX` row | 42.2 / 43.8 |
| N1 | the catch-all's log line removed | `AnyHeadlessExportFailure_…` (both cases): no export log | 45.2 / 45.3 |
| N2 | the catch-all narrowed to `UnauthorizedAccessException` | `AnyHeadlessExportFailure_…` (`InvalidOperationException`): the exception escapes `Run` | 45.2 / 45.7 |
| N3 | exit 0 on such a failure | `AnyHeadlessExportFailure_…` (both cases): exit code | 45.2 / 46.9 |

**12 of 12 KILLED**, each by the test written for that property. The properties are ones the system ought to have:
- the record says the truth about levels, credit and an empty step;
- a refusal or failure is reported where the Director can read it;
- the developer switch is not a problem report;
- the trace check keeps the kernel's column rule.

### 10.5 The RR-R1-04 fix, end to end (MEASURED, Debug builds, this container)

| Run | `8610ebd` | `fe45f0b` |
|---|---|---|
| uid 65534, the trace mode 000 (the `current-v6` session) | exit 134, `Unhandled exception. System.UnauthorizedAccessException`, no export log | exit 2; stderr `error: session export failed - UnauthorizedAccessException: Access to the path '…' is denied.`; `export-20261111-120000-a1.log` holds that line |
| The Director's files, `DOTNET_GCHeapHardLimit` 256 MB | exit 134, stderr `Out of memory.`, no export log | exit 2; the export log's line `export-session failed - OutOfMemoryException: …` |
| The Director's files, limit 384 MB and 512 MB | written; sha256 `40ba3d35…`, the lane's earlier figure | not re-run |

So the recovery note's promise (README §8, step 4) now holds for both failures. A process that is killed from outside
still writes no line (INFERRED: nothing runs to write it).

### 10.6 Validation: what ran, what was NOT RUN, what was deferred

**Run conditions.** Every run used `nice -n 19` and `DOTNET_CLI_USE_MSBUILD_SERVER=0`, with the solution or project
named. Each full suite ran under the shared lock `scratchpad/m7c/fullsuite.lock`, one at a time. No other agent's
process was running.

**Baseline (before), `8610ebd`:**
- Release: the lane's own integration run (§7), whose code equals `8610ebd`'s (the last code commit is `39621be`).
  `Sim.Tests` 3,438 passed, 0 failed, 36 skipped of 3,474; `Sim.Ui.Tests` 1,294 / 0 / 0.
- Debug: the full suites were not run before this round. The touched classes were run in Debug (§10.4 phase A).

**After (`fe45f0b` code; the docs commit that follows changes no code):**

| Tier / check | Command | Result (MEASURED) |
|---|---|---|
| Debug build | `dotnet build Sim.slnx -c Debug -m:1 -nodeReuse:false` | 0 warnings, 0 errors |
| Debug, `Sim.Tests` full | `dotnet test Sim.Tests -c Debug --no-build`, under the lock | **3,441 passed, 0 failed, 36 skipped of 3,477** (1 h 43 m) |
| Debug, `Sim.Ui.Tests` full | the same for `Sim.Ui.Tests`, under the lock | **1,298 / 0 / 0** (33 m 22 s). The first attempt was stopped by the session's 2-hour background limit while it ran: no result, and not counted as a pass |
| Release build | `validate.sh`'s `build` | 0 warnings, 0 errors |
| Tier 1 targeted | `scripts/validate.sh targeted --base 8610ebd` | doc-links, export-codebook and build PASS. `simtests-selected` PASS, 472 / 0 / 22 skipped of 494. **`uitests-selected` FAIL, twice:** the test host crashed on an ImGui assertion after 17 and then 14 passing tests. Its four classes together are `SessionExportTests`, `SessionRecordFolderTests`, `SessionCloseTests` and `RunRecordResearchTests`; the crash is the §10.7 race. Each of the four, run on its own in Release with the same build, passes: 30/30, 10/10, 17/17, 5/5 |
| Tier 2 integration, which runs both full suites with validate's own command | `flock <lock> scripts/validate.sh integration --base 51ab032` | 7 PASS, 1 NOT RUN (manual GUI acceptance): banned constructs, read isolation, doc-links, export-codebook, build; **`simtests-full` 3,441 / 0 / 36 of 3,477 (1,961 s)**; **`uitests-full` 1,298 / 0 / 0 (911 s)** |
| Tier 3 full | `flock <lock> scripts/validate.sh full` | 13 PASS. `charters` FAIL with the known 12 `unknown id` lines, the base's false positive (§7). 8 NOT RUN: bench and multi-seed trial (heavy, not requested), three CI-only rows, manual GUI acceptance, and the two full suites (blocked behind `charters`; they ran in the integration row above) |
| Doc links | `python3 scripts/check-doc-links.py --all` | 481 files, 0 broken links |
| Prototype determinism scan | `prototypes/m7-sprint1/scripts/check-proto-determinism.sh` | CLEAN (109 files) |
| Banned constructs | `scripts/check-banned-constructs.sh` | OK |
| D-069 | the round's diff searched for other games' names | none |
| Mutants | §10.4 | phase A 9/9 SURVIVED on `8610ebd` (the reproduction); phase B 12/12 KILLED on `fe45f0b` |

**Against the baseline.**
- `Sim.Tests` went from 3,438 to 3,441 passed (+3, the round's `ResearchObservationTests`), with 36 skipped both times.
- `Sim.Ui.Tests` went from 1,294 to 1,298 (+4: the `NX` test, the `--keep-raw-telemetry` close and the two failure
  cases).
- No test that passed before fails. No golden, pin or world hash moved.

**NOT RUN, and why:**
- **The UI smoke (`--smoke`).** This round did not change the exit path. `SessionExportCommand` is the headless command,
  which the smoke does not drive. The lane's smoke passed on the same exit-path code (§7).
- **The bench and the multi-seed trial (heavy).** No hot path and no simulation state changed.
- **The CI-only rows.** They run on push.
- **Manual GUI acceptance and a Windows close.** There is no window or Windows host here.
- **A re-run of the verifier's 850-turn harness.** The RR-R1-02 measurement used a 300-turn game. The 850-turn figure is
  the verifier's, cited.

**Deferred:**
- the §10.7 ImGui-race proposal and the §10.8 question, both for the coordinator;
- the lane's P1–P6 (§9), unchanged.

### 10.7 Observed outside the findings: an ImGui race between two export test classes (not fixed here)

- **MEASURED on `8610ebd`.** The grouped filter `SessionExportTests|SessionExportCodesTests|SessionRecordTests|SessionRecordFolderTests`,
  Debug, crashed the test host in 2 of 3 runs. The failures were ImGui assertions (`imgui.cpp:11039`
  `ErrorCheckNewFrameSanityChecks`, and `imgui.cpp:8381` `BringWindowToFocusFront`). The third run passed 56/56, and
  each class run on its own passes.
- **CITED.** `SessionExportTests` (`PlayAndClose`) and `SessionRecordFolderTests` both start a `UiFrameHarness`, which
  draws an ImGui frame. Neither is in the `ImGui context` collection, so xUnit may run them at once on the process-wide
  ImGui context.
- **Pre-existing.** It is the same at base `51ab032`: both classes used the harness outside the collection (CITED from
  `git show 51ab032`, not run at base).
- **PROPOSAL for the coordinator.** Put both classes in `[Collection("ImGui context")]`, one line each. It was not done
  here because it is not one of the findings.

### 10.8 Question for the coordinator

`NoteResearchNames` (RR-R1-02) runs in `UiSession.Step`, the per-turn record path. That path is not one the brief
names: the exit path, `Sim.Ui/Export`, or `Sim.Core/Observability`. It is the one place that can name the Research
sheet's `RN` codes from the six record files alone (ADR-059), which the brief's "every code in the Codebook" needs. The
alternatives are:
- name them from the build's content at export time, which ties the workbook to the build, not the record;
- leave the AI's and the city-states' nodes unnamed.

It is kept and now attributed. Does D-086 DE-R (a)'s "the session export with its codebook" cover it?

### 10.9 Raw outputs

All under `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/RR-fix1/`, which is
scratch and is lost with the container:
- `mutants/repro.json`, `mutants/kill.json`: every mutant run (edit, times, bound, failed tests, first message);
- `tools/mutants.py`, `tools/spec_repro.py`, `tools/spec_kill.py`: the runner and the specs;
- `logs/chronicle-diff-300.txt`, `tools/chronicle_diff.py`, `tools/xlsx_rows.py`, `tools/gen/`: the RR-R1-02
  measurement;
- `logs/`: the builds, the class runs, the full suites and the validator runs.

The RR-R1-04 runs used `/dev/shm/rr-fix1/`, which this round removes at its end.

## 11. Verification round 2 (fix round, 2026-11-12)

**What this round did.** The verifier's three findings on `861f9f9` (RR-R2-01..03) were each reproduced on that tree,
in this lane's clone, before any change:
- the verifier's five mutants (W01, W04, W06, W10, W11), applied one at a time with its exact edits;
- the data claims, checked against the files.

Each finding was then fixed in scope:
- tests in `Sim.Tests` and `Sim.Ui.Tests`;
- a test seam in `Sim.Ui`'s close;
- the documents that claimed city-state coverage.

Nothing in `Sim.Core` changed, and no simulation state, world hash, golden or pin moved. Every figure below is MEASURED
here unless it is labelled otherwise. The raw outputs are listed in §11.7.

### 11.1 Verdicts

| Finding | Severity | Verdict | Reproduction on `861f9f9` (MEASURED unless CITED) | Change |
|---|---|---|---|---|
| RR-R2-01 | minor | **REPRODUCED** | W01 survived `ResearchObservationTests` 11/11 and `RunRecordResearchTests` 5/5. W06 survived `RunRecordResearchTests` 5/5, `SessionExportTests` 30/30 and `SessionExportCodesTests` 9/9. The `current-v6` telemetry holds research rows of holders 1 (16 rows) and 2 (32 rows, 2 completions) and bank rows of 1 and 2 (16 each), no holder below 0; its README said the city-states' rows are in it. The docstring of `TheTelemetryFilesResearchRows_…` listed the city-states; the test asserts holders 1 and 2 only. A 300-turn founded game played here with the CLI (seed 42, one AI empire): research rows of holder 2 only (603, the CLI's player sets no target), bank rows of 1 and 2 (300 each), no holder below 0 | an end-to-end test with a released settlement; a rig test of the bank order; the fixture README, the docstring and the report statements about recorded data (main report §0 and §6, this report's §0) corrected (§11.3) |
| RR-R2-02 | minor | **REPRODUCED** | W04 survived `ResearchObservationTests` 11/11 and `RunRecordResearchTests` 5/5. CITED: `ResearchSystem.Credit` pays nothing when the cost left is not positive, and its completion step completes any available node whose progress is at least `EffectiveCost(prev)` | a rig test in which the cost falls below the stored progress |
| RR-R2-03 | minor | **REPRODUCED** | W10 survived `SessionCloseTests` 17/17 and `SessionExportTests` 30/30. W11 survived `SessionCloseTests` 17/17. No file in `Sim.Ui.Tests` reads `WorkbookProgress` or the "% of the telemetry read" text (searched) | a progress test seam (null in the game) and two tests |

### 11.2 Why the recorded sessions hold no city-state (CITED from the code)

- `WorldFounding.FoundInitialEmpire` writes a control row for every founded settlement.
- `ColonizationSystem` gives a colony its parent's controller. A colony is stateless only when its parent is.
- `RevoltSystem` (R3) passes a revolted place to a new AI polity, with a control row, in the same step.
- Nothing else removes a control row, and `SettlementKnowledge.ResearchesLocally` needs a settlement that no empire
  controls.

So the city-state path is live in the shipped content (pace 0.25), but a game founded on this tree never reaches it.
A city-state exists only in a world built with one: the research rigs, and the R2a preview's released settlement
(`R2aPreview.CityState`), which the new end-to-end test copies. The format statements ("every city-state") stand; the
statements about recorded data were corrected.

### 11.3 What changed

Commits `b2e7710` and `1e385ca`, measured with `git diff --numstat 861f9f9 1e385ca`:

| File | +/− | What |
|---|---|---|
| `Sim.Ui/SessionClose.cs` | +12/−3 | the `afterProgress` test seam: called on the worker after each progress report; null in the game |
| `Sim.Ui/GameUi.cs` | +5/−1 | `CloseProgressHook`, passed to the close like `CloseStepHook` |
| `Sim.Tests/Observability/ResearchObservationTests.cs` | +79/−1 | two rig tests |
| `Sim.Ui.Tests/RunRecordResearchTests.cs` | +199/−67 | the city-state end-to-end test; the state comparison moved, unchanged, into a helper both tests use; the docstring corrected |
| `Sim.Ui.Tests/SessionCloseTests.cs` | +83/−2 | two progress tests |
| `Sim.Ui.Tests/Fixtures/session-export/README.md` | +9/−2 | the `current-v6` holders, corrected |

The reports are marked in place:
- the main report §0 (items 1 and 3), §3.2, §4.3, §5, §6 (the chronicle names: "the AI empire or city-states" became
  "the AI empire"), §7 and §8;
- this report's §0, §3, §4 and §6.

What the game writes is unchanged. The seam is null in the game; the close's callback stores the progress exactly as
before, then calls it.

### 11.4 The new tests (each a semantic property, with the rig's or the record's own numbers)

- `ResearchObservationTests.TheBankRows_AreTheRosterPolitiesThenTheCityStates_NotTheAccountsTablesOrder`
  - The two-lane rig: the roster in the order 2, 1 and city-states at settlements 2 and 3, 10,000 people each; CP
    1.0 × P^0.5 a sim-year; city-state pace 0.25.
  - After one step the accounts table holds −4, −3, 1, 2 (ADR-043's ascending key puts the city-states first): the
    premise.
  - The bank rows are exactly (1, 1,000,000, 0, 1,000,000), (2, 1,000,000, 0, 1,000,000), (−3, 250,000, 0, 250,000),
    (−4, 250,000, 0, 250,000): the documented order, each the account's change.
- `ResearchObservationTests.ANodeWhoseCostFellBelowItsStoredProgress_CompletesWithTheProgressItHad_ReceivingNothing`
  - The rig: a, b and c complete, eng the RP target. Turn 1: received 1,000, progress 1,000, cost 2,500.
  - Before turn 2 an engineering-university modifier of 0.3 (the table InstitutionsSystem writes, set directly) brings
    the cost to 750.
  - Turn 2 is eng's completion row: progress 1,000 (what the world stored), received 0, credit 0, cost 750. The world
    completes eng in that step.
- `RunRecordResearchTests.ACityStatesResearch_IsInTheRunRecord_AfterTheRosterPolities_CodedStByItsSettlement`
  - Seed 42, one AI empire, the last settlement (11) released before the first turn; its local key is −12. The session
    plays 64 turns through `PlayToDisk`, the player researching. The city-state's first completion is on turn 63.
  - The telemetry rows equal the worlds' research for every holder, through the shared helper. The city-state has
    research rows, bank rows and that completion row.
  - In every turn the empires' rows come before the city-state's, research and bank rows alike. Premise: the final
    accounts table holds −12 first.
  - The Research sheet codes the city-state `ST11` (the settlement's id, read from the world) and the empires `PL1`
    and `PL2`, row for row with the telemetry. The workbook stays coded.
- `RunRecordResearchTests.TheTelemetryFilesResearchRows_AreTheWorldsResearchTurnByTurn_EveryHolder`: the same checks,
  through the shared helper; its docstring now says its session has the two empires only.
- `SessionCloseTests.TheWorkbookStep_ReportsTheShareOfTheTelemetryRead_AfterEachLine_EndingAtTheWholeLog`
  - The `current-v6` session: 16 reports, the k-th equal to (the offset of the k-th line feed in the file + 1) / the
    file's length, found in the file's own bytes; the last is 1.
- `SessionCloseTests.WhileTheWorkbookIsBuilt_TheSavingScreenShowsTheShareOfTheTelemetryRead`
  - A three-turn session's close, held in its workbook step just after the second telemetry line.
  - `WorkbookProgress` is (the second line feed + 1) / the log's length: 0.66084… here.
  - The frame drawn there shows `[now] Building and checking the workbook: 66% of the telemetry read`.
  - Released, the close has reported three times, the last 1, and has written its workbook.

### 11.5 Mutants (bounded; every kill by a semantic test)

**Method.** `scratchpad/m7c/RR-fix2/tools/mutants.py` ran in this clone. For each mutant it:
1. applied its textual edits (each old text must occur exactly once) and built Debug;
2. ran each named test class on its own, bounded by **max(60 s, 5 × the class's clean time)**;
3. restored the files byte for byte and checked their sha256.

No mutant hung, and `git status` was clean after each phase.

**Phase A, the reproduction, on `861f9f9`.** The edits are the verifier's, verbatim. Clean times: `ResearchObservationTests`
33.5 s (11 tests), `RunRecordResearchTests` 41.0 s (5), `SessionCloseTests` 43.3 s (17), `SessionExportTests` 34.2 s
(30), `SessionExportCodesTests` 14.4 s (9).

| Mutant | What | Verdict on `861f9f9` |
|---|---|---|
| W01 | the bank rows' sort removed (`ResearchObservation`) | SURVIVED: 11/11 and 5/5 |
| W06 | a city-state coded `ST<−h>` instead of `ST<−1−h>` (`SessionWorkbook.Research`) | SURVIVED: 5/5, 30/30, 9/9 |
| W04 | a completion's progress always the cost (`ResearchObservation`) | SURVIVED: 11/11 and 5/5 |
| W10 | the workbook step's progress report removed (`SessionWorkbook.Build`) | SURVIVED: 17/17 and 30/30 |
| W11 | the saving screen's percentage × 10 instead of × 100 (`GameUi.ClosingLines`) | SURVIVED: 17/17 |

**Phase B, the kills, on `b2e7710`.** Clean times: `ResearchObservationTests` 32.5 s (13), `RunRecordResearchTests`
70.5 s (6), `SessionCloseTests` 47.1 s (19); `SessionExportTests` and `SessionExportCodesTests` as in phase A (their
code did not change). The bounds were 162.5 s, 352.5 s, 235.5 s, 171.0 s and 72.0 s.

| Mutant | Killed by | Failure | Clean / mutant (s) |
|---|---|---|---|
| W01 | `TheBankRows_…` and `ACityStatesResearch_…` | the rig's bank rows in the table's order (−4 first); "turn 1: bank row … Holder = −12 … before … Holder = 1" | 32.5 / 32.2; 70.5 / 69.4 |
| W06 | `ACityStatesResearch_…` | expected `1 ST11 RP RN9`, actual `1 ST12 RP RN9` (`SessionExportTests` 30/30 and `SessionExportCodesTests` 9/9 still pass) | 70.5 / 70.0 |
| W04 | `ANodeWhoseCostFellBelowItsStoredProgress_…` | progress expected 1,000, actual 750 (`RunRecordResearchTests` 6/6 still pass) | 32.5 / 32.0 |
| W10 | both progress tests | no report at all; the close never held after the second line | 47.1 / 47.0 |
| W11 | `WhileTheWorkbookIsBuilt_…` | "…workbook: 66% of the telemetry read" expected, "6%" drawn | 47.1 / 46.6 |
| X1 | `ACityStatesResearch_…` | a new mutant: every holder coded `PL<key>`; `PL-12` drawn for `ST11` | 70.5 / 71.8 |
| X2 | both progress tests | a new mutant: the report made before the reader advances (the share up to the start of each line); the reports start at 0, and the held share is 0.3249 for 0.6608 | 47.1 / 46.0 |

**7 of 7 KILLED**, each by the test written for that property. The properties are ones the system ought to have:
- the record's rows follow its documented order, whatever order a table keeps;
- a city-state is named by its own settlement;
- a completion row never records less progress than the world held, nor a negative amount received;
- the saving screen tells how much of the log has been read, line by line, up to the whole.

### 11.6 Validation: what ran, what was NOT RUN, what was deferred

**Run conditions.** Every run used `nice -n 19` and `DOTNET_CLI_USE_MSBUILD_SERVER=0`, with the solution or project
named. Each full suite ran under the shared lock `scratchpad/m7c/fullsuite.lock`, one at a time, from the driver
`scratchpad/m7c/RR-fix2/tools/closing-runs.sh`.

**Baseline (before), `861f9f9`:**
- the round's classes in Debug, run here (§11.5's clean times): `ResearchObservationTests` 11/11,
  `RunRecordResearchTests` 5/5, `SessionCloseTests` 17/17, `SessionExportTests` 30/30, `SessionExportCodesTests` 9/9;
- the full suites: CITED from round 1 (§10.6), run on `fe45f0b`, whose code `861f9f9` carries unchanged. Debug:
  `Sim.Tests` 3,441 passed, 0 failed, 36 skipped of 3,477; `Sim.Ui.Tests` 1,298 / 0 / 0. Release (integration): the
  same counts. They were not re-run before this round's change.

**After (the code of `1e385ca`; the later commits change documents only):**

| Tier / check | Command | Result (MEASURED) |
|---|---|---|
| Debug build | `dotnet build Sim.slnx -c Debug -m:1 -nodeReuse:false` | 0 warnings, 0 errors |
| Debug, `Sim.Ui.Tests` full | `dotnet test Sim.Ui.Tests -c Debug --no-build`, under the lock | **1,301 passed, 0 failed, 0 skipped** (34 m 49 s) |
| Debug, `Sim.Tests` full | `dotnet test Sim.Tests -c Debug --no-build`, under the lock | **3,442 passed, 1 failed, 36 skipped of 3,479** (2 h 37 m). The failure is a wall-clock budget, `PopulationTests.ProductionPipeline_PerPhaseBench_Reported`: "200 founded turns took 5359 ms" against `< 5000`. See below |
| Release build | `validate.sh`'s `build` | PASS |
| Tier 1 targeted | `scripts/validate.sh targeted --base 861f9f9` | doc-links, export-codebook and build PASS. `simtests-selected` PASS, 479 passed / 0 failed / 22 skipped of 501 (183 s). **`uitests-selected` FAIL:** the test host crashed after 61 passing tests (21 s). See below |
| Tier 2 integration, which runs both full suites with validate's own command | `flock <lock> scripts/validate.sh integration --base 51ab032` | 7 PASS, 1 NOT RUN (manual GUI acceptance): banned constructs, read isolation, doc-links, export-codebook, build; **`simtests-full` 3,443 / 0 / 36 of 3,479 (2,409 s)**; **`uitests-full` 1,301 / 0 / 0 (992 s)** |
| UI smoke (the exit path's files changed: the seam) | `dotnet Sim.Ui.dll --smoke --monkey 300 --report … --timeout-minutes 30` | SMOKE PASSED: 13 states, 4,123 checks (3,785 pass, 338 not offered, 0 FAIL), 0 problems, 139,775 frames, 3,900 monkey actions, largest draw list 129,382 vertices, 240.6 s |
| Tier 3 full | `flock <lock> scripts/validate.sh full` | 13 PASS. `charters` FAIL with the known 12 `unknown id` lines, the base's false positive (§7). 8 NOT RUN: bench and multi-seed trial (heavy, not requested), three CI-only rows, manual GUI acceptance, and the two full suites (blocked behind `charters`; they ran in the integration row above) |
| Doc links | `python3 scripts/check-doc-links.py --all` | 481 files, 0 broken links |
| Prototype determinism scan | `prototypes/m7-sprint1/scripts/check-proto-determinism.sh` | CLEAN (109 files) |
| Banned constructs | `scripts/check-banned-constructs.sh` | OK |
| D-069 | the round's diff searched for other games' names | none |
| Mutants | §11.5 | phase A 5/5 SURVIVED on `861f9f9` (the reproduction); phase B 7/7 KILLED on `b2e7710` |

**The `uitests-selected` crash is the ImGui race of §10.7, measured again.**
- With `--blame`, the same selection crashed again, after 31 passing tests, on the assertion
  `imgui.cpp:11039 … ErrorCheckNewFrameSanityChecks … "Forgot to call Render() or EndFrame() at the end of the previous
  frame?"`.
- The same selection without `SessionExportTests` and `SessionRecordFolderTests`, the two classes that draw ImGui frames
  outside the `ImGui context` collection, passed twice: 331 / 0 / 0 (6 m 46 s and 6 m 52 s).
- Each of the five export, record and close classes, run on its own in Release with the same build, passes: `SessionExportTests`
  30/30, `SessionRecordFolderTests` 10/10, `SessionCloseTests` 19/19, `RunRecordResearchTests` 6/6,
  `SessionExportCodesTests` 9/9.
- This round added no ImGui use outside the collection: the two new close tests are in it, and the city-state test draws
  no frame.

So the FAIL is reported as a FAIL, not a pass; it is the pre-existing race, not this round's change (INFERRED from the
above). The proposal of §10.7 stands.

**Against the baseline.**
- Release: `Sim.Tests` went from 3,441 to 3,443 passed (+2, the round's rig tests), with 36 skipped both times;
  `Sim.Ui.Tests` from 1,298 to 1,301 (+3: the city-state test and the two progress tests).
- Debug: `Sim.Ui.Tests` from 1,298 to 1,301. `Sim.Tests` from 3,441 passed to 3,442 passed and 1 failed: the two new
  tests pass, and the bench budget above failed.
- No golden, pin or world hash moved.

**The Debug `Sim.Tests` failure is a wall-clock budget at its edge, at base too.** The test times 200 founded turns of
the production pipeline with its own phase observer and asserts under 5,000 ms. MEASURED here, Debug, the test alone on
the idle machine, three runs each:
- this tree: 4,880, 4,845 and 5,261 ms (pass, pass, fail);
- base `51ab032`, built in a temporary worktree: 5,002, 4,845 and 5,041 ms (fail, pass, fail).

CITED: the timed path is `TurnExecutor.Step` with the test's own observer; the research observation runs only in the
telemetry `Observer`, which this test does not use, and this round changed nothing in `Sim.Core`. It passed in Release
(the integration row). It is reported as a FAIL of this run, not as a pass; it is not this lane's to change.

**NOT RUN, and why:**
- **The bench and the multi-seed trial (heavy).** No hot path and no simulation state changed.
- **The CI-only rows** (cross-process determinism, platform smoke, nightly calibration). They run on push.
- **Manual GUI acceptance and a Windows close.** There is no window or Windows host here; the saving screen is tested
  through the headless frame harness only.
- **A refresh of `scripts/test-durations.json`.** The new city-state test takes about 29 s in Debug alone (45 s inside
  the Debug full suite), so it would count as slow; the file is outside this lane's paths and is left to the
  coordinator's integrated run (`--record-durations`).

**Deferred:**
- the ImGui-race proposal (§10.7) and the §10.8 question, both for the coordinator;
- the lane's P1–P6 (§9), unchanged.

### 11.7 Raw outputs

All under `/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7c/RR-fix2/`, which is
scratch and is lost with the container:
- `mutants/repro.json`, `mutants/kill.json`: every mutant run (edits, times, bound, failed tests, first messages);
- `tools/mutants.py`, `tools/spec_repro.json`, `tools/spec_kill.json`, `tools/clean-861f9f9.json`,
  `tools/clean-b2e7710.json`, `tools/runclass.sh`, `tools/closing-runs.sh`: the runners and the specs;
- `logs/`: the builds, the class runs, the 300-turn CLI game's log, the validator runs, the full suites (`.trx`), the
  smoke, and `closing.status` (each closing step's start, end, exit code and seconds).

The 300-turn CLI game and the smoke ran in `/dev/shm/rr-fix2/`, which this round removes at its end.
