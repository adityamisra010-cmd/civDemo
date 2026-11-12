# CR-027: the frozen trace reader refuses the half-year values the frozen trace writer writes (sessions past turn 1270)

**Status: RULED (Director, 2026-11-12): option 1 is adopted** ([D-088](../d088-battalion-limits-healing-walls-director-answers-2026-11-12.md)
DG2: "2) a , yes"). Raised from lane RR's PROPOSAL P1 (`docs/m6-plan/run-record-research-and-exit-2026-11.md` §4.6 and §10;
D-087 DF5 and §3). The kernel is frozen after M0 (CLAUDE.md), so the change needed the Director's sign-off.

## 1. Frozen items in conflict

1. **The trace writer**, `Sim.Core/Kernel/SessionTrace.Line`: the year column is `world.Clock.WorldDateYears`, a `double`,
   formatted with `InvariantCulture`.
2. **The trace reader**, `Sim.Core/Kernel/SessionTrace.Parse`: it reads that column with `long.Parse` into `Row.Year` (`long`).
3. **The calendar:** from world year 5920 (1920 CE, the Modern band) a turn is half a year (`dtYears` 0.5), so from turn 1271 the
   writer writes years such as `5920.5`.

Writer and reader cannot both stand for a session longer than 1,270 turns: the reader refuses every line the writer writes from
then on.

## 2. Evidence (MEASURED by lane RR, `docs/m6-plan/run-record-research-and-exit-2026-11.md` §4.6)

- On a 1,630-turn session (year 6100, the end of the timeline):
  - `UiSession.Resume` ("Continue last game") throws `SessionUnreadableException` naming the trace;
  - before lane RR, the close refused the workbook (`record-unreadable`) and `--export-session` exited 2;
  - the 850-turn control session resumes (to turn 850 in 69.2 s).
- `sim replay` and `sim inspect` read the trace with the same reader.
- Lane RR made the export check the trace with its own reader (`SessionWorkbook.CheckTrace`, the kernel's rules with the year
  read as written), so the workbook is written. Resume, replay and inspect stayed refused, and a test pins the kernel reader's
  refusal (`Sim.Ui.Tests/SessionCloseTests.cs`, the half-year test).

## 3. Options

1. **The reader reads the year as the number written:** `Row.Year` becomes a `double`, parsed with `NumberStyles.Float` and
   `InvariantCulture`; a year that is not a finite number is refused with its line number.
2. **The writer writes a whole year:** it changes the trace file's format and every trace written so far, loses the half-year,
   and leaves today's long sessions unreadable.
3. **No change:** a game past 1920 CE cannot be continued, replayed or inspected.

## 4. Blast radius (option 1)

- `Sim.Core/Kernel/SessionTrace.cs`: `Row.Year` (`long` → `double`) and the year's parse.
- Every reader of `Row.Year`: one, the year column that `sim replay` and `sim inspect` print (`Sim.Cli/Program.cs`), whose text
  is unchanged for a whole year (`5919` prints `5919`) and shows the half for a half-year (`5920.5`).
- `ReplayProof` compares hashes, not years; the trace files, the world, every hash and every golden are unchanged (the trace is
  an observer).
- One test moves: lane RR's pin that the kernel reader refuses a half-year trace.

## 5. Recommendation

Option 1: the smallest change, and the writer's format is the record that already exists in every session.

## 6. Resolution (2026-11-12)

The Director adopted option 1 (D-088 DG2). The orchestrator applies it with tests and a Tier 3 run; its commit is recorded in
the pass-state README and joins the pass's final-round verification queue.
