# D-068: M6 playtest fixes: a separate Civics button, roads kept under the fog, a smaller and fully coded session record, a stronger Age look, corrected title and explore text (Director, 2026-10-29)

**Status: RULED.** Recorded from the Director's playtest feedback of 2026-10-29 on build `sim-ui-win-x64-ebcaa0c`, and from the Director's choices made the same day:
- the selection of four fixes: "Separate Civics button", "Hide roads over fog", "Smaller session output", "Title + explore message";
- the coded-workbook instruction, quoted in R5;
- "Panels only, now" for the Age look (R6);
- "Yes, commit + push when green" for the playtest build.

**How this record works:**
- **It adds rulings.** It edits no earlier record (gov-4 §6). Where it changes the meaning of an earlier record, that record gets an appended, dated pointer note.
- **Precedence:** an explicit Director ruling ranks above D-records, ADRs and the spec (gov-4 §1).

**What this record does NOT do:**
- It does not close M6. The playtest continues on the build carrying these fixes, and the formal exit ruling is the Director's.
- The `CLAUDE.md` current-milestone line is unchanged.
- No merge, tag or release is authorized.
- **Authorized:** one commit of this batch on `claude/new-session-yavghm` and its push, so CI builds the Windows playtest artifact. This was the Director's answer of 2026-10-29.
- **No simulation state, world hash or simulation golden moves.**
  - Every change is one of: presentation (Sim.Ui); one player-facing string in Sim.Core (the Explore blocker); the observe-only chronicle module (`Sim.Core/Chronicle`); or the session exporter.
  - Only the session-workbook content goldens move (R5).

---

## 1. What the playtest reported

| # | Report | Diagnosis (checked against the tree) |
|---|---|---|
| P1 | Roads are drawn through unexplored fog | **Defect.** `RoadLens` drew every route in full. The fog mask applied to terrain and settlements but not to road strokes and labels, so row 5 ("nothing unseen leaks") FAILED on `ebcaa0c`. |
| P2 | Exploration stops at 240 km | **Intended limit, poorly explained.** Explore widens the known land by up to 15 knowledge-lattice nodes of 16 km each (240 km). Reaching further needs movement-dependent fog, which is deferred to M7 (D-066 R1). The blocker text did not say so. |
| P3 | "What is assay?" | **Explained, not changed.** Assay is the Prospect method that reveals concealed deposits in known land. |
| P4 | No Civics button in the top bar | **Defect against ADR-043's two-lane model.** The top bar had one Knowledge chip, and Civics was reachable only as a tab inside it. |
| P5 | Completed civics read correctly | PASS (row 2). |
| P6 | After quitting, six session files sit beside the workbook; the workbook could be smaller | **Confirmed.** The raw telemetry log is 91% of the folder, and the workbook already holds all of it. The STORED workbook compresses about 3×. |
| P7 | The window title reads M5 | **Defect.** `BuildInfo.Milestone` was not advanced at M6. |
| P8 | Reaching Age 2 changes nothing visible | **Working as built, but too weak to notice.** The interface theme follows the player's Age (panels, frames, type, ornament). But A1 (stone, panel `#D4C6AB`) and A2 (clay, `#DFC8A2`) were two similar beiges, and the map's parchment background is never themed (style bible §1). |
| P9 | The workbook is full of words ("settlement", "souls") | **Confirmed.** Only the Telemetry sheet was coded. Orders, Chronicle, Manifest and Forensic carried prose. |

## 2. Rulings

| # | Subject | Ruling |
|---|---|---|
| R1 | Civics button (P4) | **RULED: two top-bar chips.**<br>- Technology `[K]` and Civics `[C]` are separate chips. Each shows its own lane's state ("Civics idle - choose [C]" and similar).<br>- `[K]` opens the Technology tab and `[C]` opens the Civics tab. Pressing the key of the tab already shown closes the screen.<br>- Three playability-gate rows are added (`docs/m5-playability-gate.md`, dated note). |
| R2 | Roads over fog (P1) | **RULED: roads are drawn and labelled only over known land.**<br>- `RoadLens.KnownSpans` cuts each route at knowledge-lattice block boundaries and keeps only the known spans.<br>- With fog off, the output is byte-identical to before.<br>- This is a display clip only. Road routing through unknown land stays deferred to M7 (D-067 R10). |
| R3 | Session output (P6) | **RULED: the raw telemetry log is removed after a verified export, and the workbook is deflated** (ADR-059 amendment of 2026-10-29, decisions E1–E5).<br>- The manifest, orders, trace, chronicle and forensic files stay.<br>- `sim-ui --keep-raw-telemetry` keeps the log, for developers.<br>- **Supersedes D-066 A14 / D-067 R6, for the container only:** the `.xlsx` bytes are no longer a cross-runtime goal. The decoded part contents stay pinned (R5). |
| R4 | Title and explore text (P2, P7) | **RULED.**<br>- `BuildInfo.Milestone` = `"M6"`.<br>- The Explore blocker (`DiscoveryQuery.ExploreBlocker`) now ends "- further exploring needs mobile scouts (M7)".<br>- The 240 km cap itself is unchanged. |
| R5 | Coded workbook (P9) | **RULED (Director, verbatim): "the workbook should have 0 real words in anything that is not a key or a legend."**<br>- **Legend sheets:** README and Codebook keep words. The Codebook is the single legend; it defines every code used anywhere in the workbook.<br>- **Coded sheets:** Manifest, Orders, Trace, Chronicle, Telemetry and Forensic hold only codes, numbers and identifiers, headers included. Every code is two letters or fewer, so a run of three letters in a data cell is a defect, and a test enforces this.<br>- **Orders:** the prose columns are dropped. The exact values stay (target id, amount, amount bits), with coded readings such as `ST12/S3`, `PC45`, and `SV`/`AS` for survey and assay.<br>- **Chronicle:** the session chronicle file `chronicle-*.txt` is written as coded lines from the events themselves (`chronicle-codes/v1`, e.g. `Y0 FN ST0 SL438`). Name lines (`ST0=Bigen`) are legend lines, and the exporter moves them to the Codebook. The in-game Annals panel stays prose, unchanged.<br>- **Text values:** a session-file value that is a real word becomes `QT<n>`, and its exact text is in the legend.<br>- **Goldens moved:** the Director's 68-turn workbook `136e853c…` → `806e0947…`, and the current-tree workbook `6d488a2c…` → `c5ec8abd…`. The writer golden `e65373b0…` is unchanged.<br>- **What stayed byte-identical:** all package parts other than the eight sheets, and every Telemetry data cell (68/68 and 16/16).<br>- **Details:** ADR-059 amendment "coded sheets". |
| R6 | Age look (P8) | **RULED: panels only, now.** The panel, frame and chrome tokens of A1–A5 and A7–A9 are re-tuned, so advancing an Age is visibly different.<br>- **Progression:** grey stone → terracotta clay → golden sandstone with a verdigris bar → iron and plaster → marble with a classical red bar → vellum (A6, unchanged) → laid paper → blue-white drafting film → clean coated stock with a dark slate bar and no grain.<br>- **Pinned separation** (`EraSeparationTests`): every consecutive pair is ≥ 12 ΔE on the panel or the field, and ≥ 15 ΔE on the chrome bar. A1→A2 is now 17.3 on the panel (was 7.0) and 30.7 on the chrome (was 11.4).<br>- **Unchanged:** the map's parchment background stays unthemed (style bible §1 stands), and A6 is byte-identical (canonical hash pinned).<br>- **Cross-fade:** the Age-change fade goes from 1.6 s to 2.4 s.<br>- **No readability floor was weakened.** |

## 3. Measured effects

**R3** (seed 42, 150 turns, one AI empire):

| | Before | After |
|---|---|---|
| workbook | 1,793,722 bytes | 615,866 bytes |
| `telemetry-*.jsonl` | 18,551,061 bytes | removed (its lines are the Telemetry sheet) |
| session folder | 20,371,184 bytes | 642,267 bytes (31.7× smaller) |

**R5** (deflated workbooks):
- **Workbook size:** the Director's 68-turn workbook goes from 274,855 to 281,467 bytes, and the current-tree workbook from 87,164 to 94,229 bytes. The legend adds a little.
- **Chronicle file:** 368 bytes coded, against 1,106 bytes of prose, for the same 16 events.

## 4. Known leftovers (disclosed, not fixed here)

- **`sim inspect`** answers NOT RECORDED for the telemetry topics once the raw log is removed, because Sim.Core cannot read the workbook. Remedy: `--keep-raw-telemetry`, or rebuild the log by replay with `sim inspect --telemetry` (ADR-059 E5).
- **The in-game session-files panel** still lists the telemetry path after the file is removed.
- **Road clip granularity:** the clip follows 16 km lattice blocks. A road can stop a little short of the visible fog edge, and a stroke can end mid-block.
- **Frozen fixture chronicle:** the frozen `current-v5` fixture predates R5, so its prose chronicle appears as `QT` texts. Only sessions written from now on carry the coded chronicle.
- **Field-for-field tests:** five UI-side chronicle event codes (`RM`, `RD`, `RU`, `ME`, `RQ`) share the tested code path but have no test of their own.
- **Not updated:** `docs/m6-session-export/README.md` §2 (a design-input record) still describes the old prose Orders sheet.
- **Excel** has still not been run on a workbook. The deflated, coded container was checked with Python `zipfile` and LibreOffice 24.2. The playtest's Excel step now covers this form.
- **Age names** (e.g. A9 "Glass and graphite") were not renamed. The Age-change message still uses them.

## 5. Effect on the playtest

- **Re-check on the new build:** row 1 (the separate Civics button) and row 5 ("nothing unseen leaks"; roads now stop at the fog).
- **Row 10 passes when:**
  - the workbook appears;
  - the folder holds the manifest, orders, trace, chronicle and forensic files but **no** `telemetry-*.jsonl`;
  - the Orders and Chronicle sheets read in codes, with the Codebook explaining them.
- **New observation:** advance an Age and judge whether the change is now obvious (R6).
