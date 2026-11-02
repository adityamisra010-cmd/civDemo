# D-071: one Excel file in `runs`; plain-language research descriptions (Director, 2026-11-02)

**Status: RULED.** Recorded from the Director's playtest messages of 2026-11-02 on build `511e70f`:
- "next run onwards, i want only one excel sheet. not these multiple folders ok?";
- the Director's approval ("yeah do it") to rewrite the research descriptions that show developer text.

**How this record works:**
- **It adds rulings.** It edits no earlier record (gov-4 §6).
- **Supersession:** R1 changes D-068 R3's file layout. D-068 kept the manifest, orders, trace, chronicle and forensic files beside the workbook. Under R1 they move into a hidden record folder.
- **What it does NOT do:**
  - It moves no simulation state, world hash or simulation golden.
  - It does not close M6 or touch the `CLAUDE.md` milestone line.
  - **Authorized:** commit and push for the playtest build (Director, 2026-11-02). No merge, tag or release.

## 1. What the playtest reported

| # | Report | Diagnosis (checked against the Director's session `20261102-140558-a1`) |
|---|---|---|
| P1 | After quitting, `runs` held the workbook plus the chronicle, trace, orders and telemetry files. The Director wants one Excel file only. | **Two parts.**<br>(a) The manifest, orders, trace, chronicle and forensic files were there by design (D-068 R3): "Continue last game" reads them.<br>(b) The 16 MB `telemetry-*.jsonl` should not have been there. The workbook verified (its README lists the log at 16,074,749 bytes, sha256 `94edafc4…`), so `RemoveRawTelemetry` should have deleted it. The reason it did not was written to the console, which the Windows launcher discards, so the cause is **not proven**. The likely cause is a transient Windows lock on the freshly written file, such as a Defender scan. |
| P2 | Can the workbook be compressed further? | **Measured:** deflate at maximum level saves about 1% (549 KB → 544 KB). Rounding long decimals to 4 places would save about 19% of the Telemetry sheet, but the workbook would no longer be an exact record. Not adopted. |
| P3 | Written law's description reads "Reclassified from registry institution inst.law_code (architecture §5.7)…". | **Defect.** A full sweep found 87 node descriptions that carry developer language: 20 technologies and 67 civics. Of these, 60 government-form civics share one generated tail that named a milestone ("…arrives with the government layer (M8)"). |

## 2. Rulings

| # | Subject | Ruling |
|---|---|---|
| R1 | One Excel file in `runs` | **After a session closes, `runs\` shows only `session-<stamp>.xlsx`.**<br>- The record files (manifest, orders, trace, chronicle, forensic, and the telemetry log until it is removed) are written to `runs\.record\`. On Windows that folder has the Hidden attribute.<br>- File names are unchanged, so the forensic record, the exporter and the hashes keep their meaning.<br>- `--resume runs` ("Continue last game") reads `.record`. An older flat `runs\` still resumes.<br>- The raw telemetry delete is retried up to 5 times (waits of 250, 500, 750 and 1000 ms). If it still fails, the reason is also appended to `.record\export-<stamp>.log`, so the next failure can be diagnosed. |
| R2 | Plain descriptions | **Every research node's player-visible text is plain, historically accurate language.**<br>- No decision or ADR ids, registry keys, "Reclassified", "§" or implementation notes.<br>- Developer notes that were removed are kept in docs.<br>- A guard test fails if developer language returns.<br>- This is a TEXT-only tree-closure change (precedent D-069 R2). The STRUCTURE pin must not move. |

## 3. Validation

**What R2 changed:**
- 27 descriptions are rewritten whole. The 60 government-form civics now end in "Your society cannot adopt it yet."
- Only `desc` fields changed. No id, name, Age, prerequisite, unlock, effect or cost changed.
- The text is mirrored in `research-pre-tranche2.json` and `research.candidate.json`.
- The old texts and the developer notes they carried are kept in [research-description-notes.md](m6-plan/research-description-notes.md).

**Moved pins:**
- Tree-closure TEXT pin: `0b6dad3c…` → `32a6f56fe0f03160d10d0803c9a7590b71641e025a50d6ec14f2302d44a0e931` (TC-4, `tree-closure.md` §5).
- The STRUCTURE pin (`489dc44e…`) is unchanged.
- Attribution: restoring the 87 old texts reproduces the TC-3 TEXT hash (`ThePreTc4Descriptions_ReproduceTheTc3Text_Tc4IsTextOnly`).

**Also in this batch:** a fix for the CI failure on `9623ba1`. `MapTopologyGuardTests` flagged the D-070 double-click's screen-pixel `Math.Abs(x - _x)` as raw map geometry. The line is now allowlisted with that reason. CI reproduced the failure on `9623ba1`, and the guard passes after the fix.

**Validation:** the results measured on the combined tree are listed below.

Measured 2026-11-02 on the combined uncommitted tree (`9623ba1` + this batch), in the order CI runs it:

- **Build:** `dotnet build Sim.slnx -c Release -nodeReuse:false`: 0 warnings, 0 errors.
- **CI script steps:** all 16 pass. This includes the four research audits with `--check`, after their reports were regenerated for the new text, plus `tree_closure_check`, `validate.py --live`, `export_runtime_texts --check` and `check-doc-links --all`.
- **Sim.Tests, `UnitProgression` filter:** 60 passed, 0 failed.
- **Sim.Ui.Tests, `SessionExportTests` filter:** 27 passed, 0 failed.
- **`dotnet test Sim.slnx`:**
  - Sim.Ui.Tests: 1260 passed, 0 failed, 0 skipped (39 m 40 s). The new tests include `SessionRecordFolderTests`.
  - Sim.Tests: 3429 passed, 0 failed, 36 skipped (3465 total, 1 h 13 m). The new tests include `DeveloperLanguageGuardTests`. This batch adds no skip.
- **`check-readonly-proof.sh`:** rc 0.
- **Smoke:** `--smoke --ai-empires 1 --monkey 200` on 11 states, 3524 checks.
  - 3259 checks passed, 265 were not offered, 0 failed and 0 problems were found.
  - The run reported SMOKE PASSED.
- **Not verifiable here:** whether Windows now deletes the telemetry log. The Linux suite proves the retry path and the `.record\export-<stamp>.log` entry by test seams. The Director's next Windows quit is the real check.
