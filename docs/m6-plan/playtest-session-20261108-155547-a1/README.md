# Director playtest session 20261108-155547-a1 (M6 exit build): the files the Director sent

**What this is.** The Director played the `m6-exit` build (`7d5defc`, win-x64, seed 42, one AI empire), starting at
2026-11-08 15:55:51. On closing the game the screen froze, and no `runs\session-*.xlsx` appeared (D-086). On 2026-11-11 the
Director sent five of the session's six record files from `runs\.record\`: four as they were, and the telemetry log zipped. They
are kept here unchanged, under the file names the manifest gives them, with the workbook the orchestrator rebuilt from them.

| File | Bytes | SHA-256 |
|---|---|---|
| `session-20261108-155547-a1.json` (the manifest; Windows line endings) | 752 | `24d889f3292410e912029259ed337c6b335f9efb90038701d81e789dddb5f1a6` |
| `orders-20261108-155547-a1.bin` | 25,496 | `c2e655b39028c3ddc8532c7c5d058fc3c5254e29b0b40ef7b69ae1bb6076a2d3` |
| `chronicle-20261108-155547-a1.txt` | 29,897 | `61ee5dd4c2895934985fa51b79eac4f8e0781195c2172b42b62cde2656b32315` |
| `trace-20261108-155547-a1.csv` | 76,903 | `03a7f26df4288b998e84b81c384cf06087a6fd07db9646f15887fff9f22cd040` |
| `telemetry-20261108-155547-a1.zip` (as the Director sent it) | 12,782,794 | `a2fe10863c3ec2c499523bf43d1ffd38baaddad34f7abe293e67fb2880f6db5f` |
| └ `telemetry-20261108-155547-a1.jsonl` (its one member) | 173,040,889 | `90217ff768d17e363678b52a353ea4fad882506da0964d03294165fc88fdf809` |
| `session-20261108-155547-a1.xlsx` (rebuilt, with telemetry) | 6,841,416 | `01d3c33e8862ef0145caf17c55c9b81e76fe698178b57699d0f3d902b6e529b3` |

**About the telemetry log.** It has 850 lines (turns 1–850) under the tag `telemetry/v5`. The file's time is 2026-11-08 17:19,
its last write, about 83 minutes after the session started.

**Not here:** the forensic record, `forensic-20261108-155547-a1.jsonl`, was not sent. The game writes its run record once at
launch (`UiSession.ExportForensicRun`), so the file should exist. Whether it ends in a close record would show whether the close
got past that step. The workbook's README sheet marks the forensic record NOT PRESENT, with the code NP in its sheet.

**How the workbook was rebuilt (MEASURED, 2026-11-11).**
- Command: `dotnet Sim.Ui.dll --export-session session-20261108-155547-a1.json`.
- Build: a Release build of `Sim.Ui` from this branch, whose `Sim.*` tree equals `m6-exit` (an empty `git diff`).
- Host: the Linux x64 container, at the lowest priority (`nice -n 19`), sharing its CPUs with another agent.
- Result:
  - written and self-checked;
  - 850 telemetry turns with codebook v5, 20 continuation rows, and a longest cell of 32,604 characters;
  - **16.7 s, with the export process's peak resident memory 1,201 MB.**
- Without the telemetry log the same command took 0.54 s.

These are the export's own figures on this machine, not the in-game close on the Director's laptop. The close also saves,
finalizes the telemetry, writes the forensic close record and removes the raw log, all on the game thread (D-086).

**The run (MEASURED from the trace and the chronicle):**
- **Length:** 850 turns, years 0–5100.
- **Population:** peaked at 32,884,347 at turn 790 (year 4920) and ended at 25,245,053 in 26 settlements.
- **Famines:** famine-end events record 7,607,555 deaths.
- **Longbow:** RN196 was completed in year 4710.

The Director says the late-game famine was deliberate ("no need to investigate the famine. i made it happen myself."). It is
not a finding.

**Use.** Lane RR (D-086) reproduces the close at this telemetry size, from these files. Its brief is
[`docs/m7-plan/pass-state/args/RR.json`](../../m7-plan/pass-state/args/RR.json).
