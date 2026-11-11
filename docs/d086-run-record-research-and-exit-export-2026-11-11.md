# D-086: The run record keeps every research step; closing the game must not lose the run workbook (Director, 2026-11-11)

**Status: RULED (Director, 2026-11-11), with READINGS flagged.** Administrative record of the Director's second message of
2026-11-11 (cited **DE**). It reports a playtest failure on the M6 exit build and asks for research to be recorded in the runs.
Categories as in D-076..D-085: DECIDED, ADMIN, DELEGATED, READING, OPEN.

**Source (the whole message, verbatim, typing unchanged).** Working copy:
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gov/director-answers-2026-11-11b.md`.

> I've closed the game and runs excel sheet didn't form. The screen froze and i think the excel didn't come. I was playing the
> m6 exit version. So I can't send the longbow one.
>
> Also do the runs record the researches? If not, it should record which node was researched and how much and which turn also

**What the record holds today (CITED from the code at 259b739 by the orchestrator, 2026-11-11; `Sim.*` there equals the `m6-exit`
tag 7d5defc, MEASURED by an empty `git diff`).**
- The session workbook (`runs\session-STAMP.xlsx`, ADR-059, D-066 A14–A17, D-071 R1) is built only from the six record files in
  `runs\.record\`.
- **Research choices:** the player's research choices are kept as `SetResearchTarget` orders (Orders sheet, the node's key, the
  turn it was ordered).
- **Completions:** the player's completed research is kept as a chronicle event (`RM`: "research completed (the player's)",
  with the node and the in-game year).
- **Not kept:**
  - the research points a node received each turn;
  - a node's progress and cost;
  - the Civics bank;
  - any AI empire's research.
  The telemetry writer (`telemetry/v5`) has no research field.

**What happens at close (CITED from the code).** When the window closes, `SimUiGame.OnExiting` calls
`GameUi.SaveOnExit` on the game thread. That call does the following in order:
1. a final save;
2. finalizing the telemetry;
3. the forensic close record, which hashes every record file;
4. the workbook export, which builds the workbook, reads all of it back and decodes every telemetry cell before writing;
5. the removal of the raw telemetry log.

The window draws nothing during all of that. That the Director's freeze was this work running, and that the workbook was lost
because the game was closed before the export finished, is INFERRED: no log of the Director's session is available. The six
record files the export reads may still be in `runs\.record\`, and `app\Sim.Ui.exe --export-session runs` rebuilds the
workbook from them headlessly (ADR-059, D-066 A16; `docs/m6-session-export/README.md`).

## 1. Decisions

| ID | Category | Decision | Director's words (DE) |
|---|---|---|---|
| DE1 | DECIDED | The run record keeps research: which node was researched, how much, and on which turn. | "it should record which node was researched and how much and which turn also" |
| DE2 | DECIDED (defect) | Closing the game must leave the run workbook. The freeze at close and the missing workbook are a defect to diagnose and fix. | "I've closed the game and runs excel sheet didn't form. The screen froze and i think the excel didn't come." |
| DE3 | ADMIN | The Director cannot send the Longbow evidence that D-084 asked for. D-084's Longbow item proceeds as a diagnosis from the repository alone (DC1-R1 (a)). | "So I can't send the longbow one." |

## 2. Readings (each applied until the Director corrects it; both alternatives stated)

- **DE1-R1 (whose research).** (a) APPLIED: every polity's, the player's and each AI empire's. (b) The player's only.
- **DE1-R2 ("how much").**
  - (a) APPLIED: per node and turn, the research points the node received, its accumulated progress and its cost. For Civics this
    also covers the banked Civics Points drawn, so a reader can follow the pace and see why a node took as long as it did.
  - (b) Only the node's share complete each turn.
- **DE1-R3 (rows).**
  - (a) APPLIED: one row per polity, turn and node that received points or was completed, with the turn written in every row.
    A completion row says it completed.
  - (b) Completions only, each with its turn and total points.
- **DE2-R1 (what "fixed" means).**
  - (a) APPLIED, all three:
    - the window shows that the run is being saved and stays responsive while the work at close runs;
    - a close that is interrupted still leaves the six record files complete enough for `--export-session` to rebuild the
      workbook;
    - the export's time on a long game is measured and reduced where it can be without dropping its self-check.
  - (b) Only the recovery path is documented, and the close is unchanged.
- **DE-R (which projects may change).**
  - (a) APPLIED: for this work only, the following may change, with their tests:
    - `Sim.Ui`: the exit path and the session export with its codebook;
    - `Sim.Core/Observability`: the observation and telemetry records that would carry the research rows, and their readers;
    - `Sim.Core/State` read-only queries if a research reading needs one.
  - Nothing in `Sim.Core/Kernel` changes, and no simulation state or world hash changes: the research rows are an observation of
    state that already exists. A new telemetry field set moves the telemetry tag (the T4.20 rule in `TelemetryWriter`), and every
    moved golden or pin gets a measured attribution. This is an exception to the pass's rule that work stays in `prototypes/`
    and `docs/` (D-079 §6), as D-084 DC1-R2 (a) made for the research trees. D-079 Q5 stays OPEN for everything else.
  - (b) A patch for the Director's review, landing nothing in `Sim.*`.

## 3. Not decided here

- **Delegated design:** the sheet and the coded columns of the research rows; whether they travel in the telemetry line or in a
  record file of their own; how the window shows the saving.
- **Order of work.** The orchestrator queues this lane after lane MOR3 and before the final graphics package, because the run
  record serves the Director's playtests. The Director may reorder it.
- **Still OPEN, as in D-085 §5.**
