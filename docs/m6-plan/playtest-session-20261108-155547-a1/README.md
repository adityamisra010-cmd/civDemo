# Director playtest session 20261108-155547-a1 (M6 exit build): the files the Director sent

**What this is.** The Director played the `m6-exit` build (`7d5defc`, win-x64, seed 42, one AI empire), starting at
2026-11-08 15:55:51. On closing the game the screen froze, and no `runs\session-*.xlsx` appeared (D-086). On 2026-11-11 the
Director sent four of the session's six record files from `runs\.record\`. They are kept here unchanged, under the file names
the manifest gives them, with the workbook the orchestrator rebuilt from them.

| File | Bytes | SHA-256 |
|---|---|---|
| `session-20261108-155547-a1.json` (the manifest) | 752 | `24d889f3292410e912029259ed337c6b335f9efb90038701d81e789dddb5f1a6` |
| `orders-20261108-155547-a1.bin` | 25,496 | `c2e655b39028c3ddc8532c7c5d058fc3c5254e29b0b40ef7b69ae1bb6076a2d3` |
| `chronicle-20261108-155547-a1.txt` | 29,897 | `61ee5dd4c2895934985fa51b79eac4f8e0781195c2172b42b62cde2656b32315` |
| `trace-20261108-155547-a1.csv` | 76,903 | `03a7f26df4288b998e84b81c384cf06087a6fd07db9646f15887fff9f22cd040` |
| `session-20261108-155547-a1.xlsx` (rebuilt) | 147,720 | `93ebbf59ddd1ddf6900d45ad3ec8099bd8d87c768da7d52d682e56a616e07e65` |

**Not here:**
- **The telemetry log, `telemetry-20261108-155547-a1.jsonl`.** The Director reports it as 168 MB, too big to upload. That is
  about 198 KB a turn over 850 turns.
- **The forensic record, `forensic-20261108-155547-a1.jsonl`.** It was not sent.

The rebuilt workbook's README sheet marks both as NOT PRESENT, with the code NP in their sheets.

**How the workbook was rebuilt (MEASURED, 2026-11-11).**
- Command: `dotnet Sim.Ui.dll --export-session session-20261108-155547-a1.json`.
- Build: a Release build of `Sim.Ui` from this branch, whose `Sim.*` tree equals `m6-exit` (an empty `git diff`).
- Result: written and self-checked in 0.54 s; 0 telemetry turns.

**The run (MEASURED from the trace and the chronicle):**
- **Length:** 850 turns, years 0–5100.
- **Population:** peaked at 32,884,347 at turn 790 (year 4920) and ended at 25,245,053 in 26 settlements.
- **Famines:** famine-end events record 7,607,555 deaths.
- **Longbow:** RN196 was completed in year 4710.

The Director says the late-game famine was deliberate ("no need to investigate the famine. i made it happen myself."). It is
not a finding.

**Use.** Lane RR (D-086) reproduces the close at this telemetry size. Its brief is
[`docs/m7-plan/pass-state/args/RR.json`](../../m7-plan/pass-state/args/RR.json).
