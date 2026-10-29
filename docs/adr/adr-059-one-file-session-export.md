# ADR-059: The one-file session export

**Status: ACCEPTED — the Director CONFIRMED D-066 A14 (D-067 R6, with its correction: the container is the hand-written
STORED writer `StoredZip`, not `ZipArchive`, and byte identity across operating systems is PREDICTED) and A15, A16 and
A17 (D-067 R1), 2026-10-28.** First recorded as ADOPTED UNDER DELEGATION (D-066 A14–A17). Stale facts are corrected in
place and listed in the amendment log at the end. This ADR records the M6 closure track EXPORT under the D-066 adoptions
A14–A17, which the Director confirmed at the exit gate (D-067 R1, R6). Every choice below is reversible: nothing in the simulation reads the workbook, and
the six session files stay the record.

**Sources:**
- `docs/d066-m6-closure-rulings.md`: R2 (the C# one-file session export is an M6 obligation), and the adoptions A14
  (format and dependencies), A15 (telemetry tag), A16 (placement) and A17 (continuation cells);
- `docs/m6-session-export/README.md`: the design input, with its cell grammar (§3) and its "required before M6 ships"
  list (§5);
- `docs/m6-session-export/prototype/`: the Python prototype, in which `spec.py` is the single source of the codebook.

## 1. What is built

One played session produces one workbook, `session-STAMP.xlsx`, written beside the six session files. Its eight sheets,
in order:

1. **README:** how to read the workbook; the build, seed, platform and schema; "EMPTY never means 0"; the continuation
   rule; and every file with its sha256.
2. **Manifest:** `session-*.json` as key / value / JSON-type rows.
3. **Orders:** `orders-*.bin` read by `OrderLog.Load` and decoded. Each row gives the turn, the issuing polity, the kind
   (id and name, kinds 1–18; kind 10 is reserved and unused), the target spelled out (settlement and sector named), the
   amount and what it means, and the amount's 64-bit pattern.
4. **Trace:** `trace-*.csv` as it is, one column per field.
5. **Chronicle:** one line per row.
6. **Telemetry:** column A holds the turn and column B one coded cell per turn.
7. **Codebook:** the TSV of every codebook the cells use.
8. **Forensic:** the run and close records as key / value rows, then every session file with its sha256 at export, its
   sha256 in the close record, and whether the two agree.

**Sources.** The exporter reads the six record files and nothing else (`Sim.Ui/Export/SessionWorkbook.cs`). Each file
goes through its own reader: `SessionManifest.Read`, `OrderLog.Load`, `SessionTrace.Parse`, the chronicle text, the
telemetry JSONL and `ForensicRecordFile.Parse`. The exporter never replays a session, never founds a world and never
reads simulation state.

**Missing files.** If the manifest names a file that the directory does not hold, that file's sheet says so in plain
words and quotes the close record's entry for it. Nothing is invented. Only the manifest is required, because it names
every other file.

## 2. Placement, the headless command and the close hook (A16)

The exporter lives in `Sim.Ui` (`Sim.Ui/Export/`). Nothing goes into `Sim.Core`, and there is no new project and no
Sim.Cli→Sim.Ui reference.

**Headless.** `sim-ui --export-session DIR|MANIFEST` (`SessionExportCommand`) needs no window, no GPU and no world. It
exits with 0 once the workbook is written and self-checked. It exits with 2 on a refusal and prints the named reason on
stderr. `UiArgs.Usage` lists the flag.

**At session close.** `GameUi.SaveOnExit` runs in this order:
1. `SaveSession`;
2. `FinalizeTelemetry`;
3. `ExportForensicClose`;
4. **then** `UiSession.ExportWorkbook`.

The export only reads the six files. So each file keeps the hash the close record gave it, the close record does not
list the workbook, and the forensic file is never reopened. The test pins all three through the real `GameUi`, using
the headless harness: the workbook's Forensic sheet contains the close record, which proves the export ran after the
close.

**A failed export does not block the exit.** A refusal at close writes no workbook, prints its message on stderr and
lets the exit go on, because the six files are already complete. The headless command can rerun the export at any time.

## 3. The container: a hand-written ZIP of STORED entries (A14, with one measured refinement)

A14 adopts "a hand-rolled minimal OOXML writer on `System.IO.Compression`" with STORED entries, a fixed order and fixed
timestamps, "so the bytes are identical on every runtime and OS". Measurement showed that `ZipArchive` cannot deliver
that property. On Linux (.NET 10) its central directory writes "version made by" `0x0314`, which names the **host OS**
(3 = Unix); on Windows the same field names Windows. That holds even with `CompressionLevel.NoCompression` (which does
store), a 1980-01-01 timestamp and zero external attributes. The field cannot be set.

**Decision.** `StoredZip` (`Sim.Ui/Export/OoxmlWorkbook.cs`) writes the container by hand into a seekable
`MemoryStream`:
- every entry STORED, with a CRC-32 (IEEE);
- a fixed entry order;
- DOS time 00:00 and date 1980-01-01;
- "version made by" and "version needed" both 2.0 / MS-DOS;
- flags 0, external attributes 0;
- no extra fields, no data descriptors, no Zip64.

`System.IO.Compression.ZipArchive` still plays its part as the **reader**. The self-check reads the workbook back with
it, which gives an independent implementation's opinion of every archive written.

This keeps A14's intent: no package, stored entries, the same bytes on every OS. **That last property is PREDICTED, not
measured** (D-067 R6): the fields that would name the host OS are fixed by hand, but every workbook golden (§7) was exported
on Linux (the Director's 68-turn session was recorded on Windows; its export was not run there), CI runs only on
`ubuntu-latest`, and no export has run on Windows. It departs from A14's letter on one
point, the container is not written by `ZipArchive`, and the measured field above is the reason. Reversal costs one
class.

**The OOXML parts:**
- `[Content_Types].xml`;
- `_rels/.rels`;
- `xl/workbook.xml`;
- `xl/_rels/workbook.xml.rels`;
- `xl/styles.xml`;
- `xl/worksheets/sheetN.xml`.

Every cell is an inline string (`t="inlineStr"`) carrying `xml:space="preserve"`. XML escaping is applied, plus the
ST_Xstring `_xHHHH_` convention for the characters XML cannot carry: control characters other than tab and line feed,
carriage return included (an XML parser would normalize it), U+FFFE, U+FFFF and unpaired surrogates. An underscore that
would itself start such a sequence is written `_x005F_`. Inline strings avoid two risks: a shared-string table whose
order could drift, and a number cell that a spreadsheet could reformat.

## 4. The codebook: one per telemetry schema, generated from `spec.py`

**v4 is unchanged.** `make_codebook.py` still writes `codebook-telemetry-v4.tsv` byte for byte (checked: 18,725
bytes, identical). It reads `telemetry/v2`, `v3` and `v4` lines, whose cell heads are `v2`, `v3` and `v4`.

**v5 (A15)** reads `telemetry/v5` lines (head `v5`). It is codebook v4 plus the following:
- **Optional keys:** `turn.grain.military` (ADR-055) and `turn.grain.fodder` (ADR-056). The writer emits them only
  when they are non-zero. The new default `(key absent from the line)` means an EMPTY field says "not in the line", and
  a written value means present, including an explicit `0`.
- **Reason names 17–21:** prospecting, expedition, recruitment, formationUpkeep and reequipment. Reasons 22 and 23 print
  as `reason-22` and `reason-23` (`ReasonNames.Of`), which the fallback reproduces.
- **Goods:** 23 goods (15–23: iron-ore … explosives), so 25 stock rows, 22 turn-goods rows and 23 economy-goods rows. A
  new good's price default is `1`.
- **Order kinds 14–18:** EX, PR, RF, RE and DF. Codebook v4 ends at kind 13 (CE), so all five are new to the
  codebook. The writer's own note on v5 (`TelemetryWriter.cs`, the `ReadableSchemas` summary) says "order kinds 16–18":
  kinds 14 and 15 (Explore and Prospect, ADR-044) were already written under `telemetry/v4`, and only 16–18 (ADR-055)
  belong to the M7 line-shape change that A15 retagged. The two ranges describe different deltas; both are correct.
- **Constant C2:** C2 (`food.storeLossesIdentity`) now carries the M7 pass's military-grain clause. This was found by
  measurement. The M7 pass changed the constant's text in `Observer.cs`, and without the new constant every settlement
  segment would carry the text as a 1,300-character `!` escape: the v5 session's cells measured 24,188 characters
  instead of 10,206.
- **Grammar rows:** the optional-key rule and the A17 continuation rule (§5).

**A machine-readable twin.** `make_codebook.py` now also writes `codebook-telemetry-v4.json` and `-v5.json`, the same
codebook as data. The C# codec reads the JSON and does not transcribe it, so `spec.py` stays the single source for both
the sheet and the program. `Sim.Ui.csproj` embeds both the TSV and the JSON from `docs/m6-session-export/`.
`make_codebook.py --check` fails when any of the four files is stale. CI runs it, and the validation map runs it for
every change under that folder.

**The tag (A15).** `TelemetryWriter.Schema` is now `telemetry/v5`, and `ReadableSchemas` is
`[v5, v4, v3, v2]`. This changes the telemetry text and the forensic run record's `schemas.telemetry` value. It changes
no world state, no canonical schema and no hash. The run id's preimage does not cover the telemetry tag, as
`runIdInputs` states. Five test pins, one test in each of five files, that named `telemetry/v4` as the writer's tag
now name v5:
- `SessionRecordTests`;
- `InspectionTests` (the T4.21-5 test, which also moves its readable set to include v5 and its unknown vintage to v6);
- `ForensicIdentityTests`;
- `TaxGlassBoxTests`;
- `TelemetryTests`.

## 5. The cell grammar, the size guard and continuation cells (A17)

The grammar is README §3, unchanged, plus the v5 optional keys.

**Numbers.** Telemetry numbers are the writer's own token text, never re-formatted (`JsonText` keeps number tokens as
text). Defaults are compared as text, so −0 stays distinct from 0. The exporter formats one kind of number itself: an
order's amount (`CellNumber`). It uses the same convention: the shortest round-trip text, InvariantCulture, `0.`→`.`,
`-0.`→`-.`, and `N`/`I`/`-I`. A test proves that this text equals `Utf8JsonWriter`'s for more than 400 values (−0, subnormals, exponent forms
and ties among them), and that it parses back bit for bit.

**The continuation rule (A17).**
- A turn's coded cell longer than 32,767 characters is split at a `|` into further rows. The split is greedy: each part
  takes as many whole segments as fit.
- A continuation row has an EMPTY column A, and its cell starts with the reserved marker `>`, followed by the `|` that
  was cut.
- To join, drop the `>` and append the rest to the cell above. Repeat for each continuation row.
- No coded cell otherwise starts with `>`, because a coded cell starts with its schema code.
- A single segment that is itself over the limit cannot be split. That is the named refusal `cell-too-large`, and the
  cell is never truncated.
- The value sheets (Manifest, Chronicle, Forensic) use the same marker rule for a column-B value over the limit, cut
  between characters and never inside a surrogate pair. Every real row of those sheets has a non-empty column A. A cell
  in any other column over the limit is refused.
- A sheet over 1,048,576 rows is the named refusal `row-limit`.

**Measured.** On a world founded with 64 settlements (seed 42, canonical size), the coded turns were 24,068, 44,524 and
45,905 characters. Two of the three turns overflow, so the workbook has continuation rows, no cell is over 32,767, and
every turn joins and decodes back to its line exactly. README §3 gave the overflow threshold as ≈49 settlements, which
this is consistent with.

## 6. The self-check and the named refusals

**Before a byte is written,** `SessionWorkbook.Build` runs three checks:
1. It encodes each telemetry line and decodes it at once (`EncodeChecked`).
2. It writes the workbook, then reads it back through `ZipArchive` and `XmlReader`, which also checks well-formedness
   and the required parts, relationships and content types. It checks that every cell of every sheet is exactly the text
   it wrote.
3. It re-joins the Telemetry sheet's continuation rows and decodes every cell back to its source line byte for byte.

Any difference is a `SessionExportException` with a stable `Code`, and no workbook is written: there is no raw
fallback. The codes:

| Code | Raised when |
|---|---|
| `manifest-unreadable` | the manifest cannot be read |
| `record-unreadable` | another record file cannot be read |
| `telemetry-schema-unknown` | a telemetry line's schema has no codebook. README §5.4: a writer change needs a new codebook first. The prototype escapes an unknown tag; the exporter refuses it. |
| `telemetry-shape` | a telemetry line's key order or shape differs from its codebook |
| `cell-undecodable` | a cell cannot be decoded |
| `telemetry-roundtrip-mismatch` | a cell does not decode back to its source line |
| `cell-too-large` | a cell is over 32,767 characters and cannot be split |
| `row-limit` | a sheet is over 1,048,576 rows |
| `workbook-readback-mismatch` | the workbook read back differs from what was written |

**The C# port.** `TelemetryCellCodec` is a port of the prototype's `codec.py`. There is one deliberate difference: a
table default is compared in key ORDER, where the prototype compares Python dicts without regard to order. The two
differ only on a line whose row keys are out of order, and the self-check refuses such a line either way.

**Determinism.**
- No clock: the README sheet carries the session's own build, not the exporter's.
- No culture, no `float`, no `System.Random`.
- No dictionary iteration: objects are ordered key lists, and ids are BigInteger.
- The line-level attractiveness table goes to the most common list, with the first one seen winning a tie. A tie-dense
  test pins this.

## 7. Goldens and checks

All the tests are in `Sim.Ui.Tests/SessionExportTests.cs`, and the fixtures are in
`Sim.Ui.Tests/Fixtures/session-export/` (README there).

**The codec against the prototype.** For the Director's 68 turns (v4) and the 16 turns of a current-tree session (v5),
every C# cell equals the prototype's and decodes back to its line byte for byte.

**The workbook goldens:**

| Workbook | sha256 | Size |
|---|---|---|
| The Director's 68-turn session | `136e853c72d8596cb81785dd438dfeb6dbe527ca0e1ce53fd8532e8bf0c365e5` | 887,862 bytes |
| The current-tree session | `6d488a2c97bcbb143f274ddf97784cf4daf170d301b3e1e340718b532139560d` | 416,059 bytes |
| A small two-sheet escape workbook | `e65373b07c7eee9160c39bcbdad022f7c13d306fd399715be01cb883118a82b5` | |

Each one is built twice, and both builds give the same bytes.

**The container.** A test walks the ZIP by hand and checks the stored method, the fixed headers, the CRCs, the entry
order and the agreement between the central directory and the local headers. Every part also loads as XML.

**The prototype.** On the 68-turn v4 session and on the 16-turn v5 session, `encode.py` and then `decode.py` give the
telemetry back byte for byte. A 20-turn `sim run --emit-session` session, relabelled v5, gives back all 20 lines.

**An independent reader.** Python's `openpyxl` read three workbooks the headless command wrote: the Director's, the
current-tree session's and a 64-settlement `sim run --emit-session` session's (3 turns, 2 continuation rows, longest
cell 32,560 characters). For each one it found the eight sheets in order and every value as text, and the prototype's
`decode_to_line`, applied to the joined Telemetry cells, gave back every telemetry line exactly: 68/68, 16/16 and
3/3.

**CI.** `.github/workflows/ci.yml` gains two steps:
- the codebook check (`make_codebook.py --check`);
- the session-export golden (`dotnet test Sim.Ui.Tests --filter FullyQualifiedName~SessionExportTests`).

`scripts/validation-map.json` gains a `session-export` category with the check `export-codebook`.

**Line endings.** A new `.gitattributes` marks the embedded codebooks and the fixture files `-text`. The workbook
goldens hash their exact bytes, and the Director's manifest has Windows line endings, so a checkout must not convert
line endings.

## 8. Human checks: not performed and not claimed

1. **Opening the workbook in Microsoft Excel, LibreOffice Calc and Google Sheets.** The checks needed: every sheet
   opens with no repair prompt, the cells show as text, and a cell of 32,767 characters is accepted. Only machine readers
   were run: the exporter's own (`ZipArchive` + `XmlReader`) and Python's `zipfile` and `openpyxl`, which read every
   sheet and every value.
2. **The `_xHHHH_` convention in Excel.** `openpyxl` does not decode `_xHHHH_` in inline strings, so it could not
   confirm this. No telemetry cell can contain the sequence: coded cells are printable ASCII, and the codec escapes `_`
   inside strings. It concerns only exotic text in the chronicle or the forensic record.
3. **The exit hook in a real window.** It was exercised only through the headless harness.

## 9. Decisions made here (new rationale, under D-066's rules: simplest, deterministic, testable, reversible)

- **D1:** the hand-written container (§3).
- **D2:** the codebook's machine-readable JSON twin, generated from `spec.py` (§4).
- **D3:** v5's constant C2 (§4).
- **D4:** an unknown telemetry schema is refused (§6).
- **D5:** a missing companion file is stated in its sheet rather than refused; only the manifest is required (§1).
- **D6:** a failed export at close is reported and does not block the exit (§2).
- **D7:** the continuation marker is `>` and the cut `|` stays in the continuation cell, so joining is plain
  concatenation; the same rule applies to over-limit values in the value sheets (§5).
- **D8:** the Orders sheet adds the amount's 64-bit pattern, so the decoded value can be checked exactly.
- **D9:** the generator of the current-tree fixture is a test method that does nothing unless
  `CIVSIM_WRITE_EXPORT_FIXTURE` is set, following the precedent of the `CIVSIM_*` measurement tools. It is a tool, not a
  check.

## Amendment log — 2026-10-28 (D-067)

Authority: `docs/d067-m6-exit-gate-director-rulings.md` R1 (A15, A16, A17 CONFIRMED), R6 (A14 CONFIRMED with
corrections) and §7 ("status CONFIRMED, with stale facts corrected"). This ADR was uncommitted and not yet
confirmed, so stale FACTS were corrected in place; no design decision is changed. Line numbers marked "was" are those of
the staged pre-amendment text; evidence was read from the live tree on 2026-10-28. D-067 is cited by ruling and section, not by line: it is untracked and was being amended concurrently.

| # | Where | Old | New | Evidence |
|---|---|---|---|---|
| 1 | Status line (was :3–4) | "Status: ADOPTED UNDER DELEGATION (D-066 A14–A17; Director confirmation requested at the exit gate). … which the Director confirms at the exit gate." | "Status: ACCEPTED — the Director CONFIRMED D-066 A14 (D-067 R6, with its correction …) and A15, A16 and A17 (D-067 R1), 2026-10-28." "… which the Director confirmed at the exit gate (D-067 R1, R6)." | D-067 §1 (R1, R6) |
| 2 | §3 (was :84) | "This keeps A14's intent: no package, stored entries, the same bytes on every OS." (no evidential status) | Same sentence, plus: that property is PREDICTED, not measured; every workbook golden was exported on Linux, CI runs only on `ubuntu-latest`, no export has run on Windows | D-067 §1 (R6) ("Byte identity across operating systems is PREDICTED; nothing has run on Windows"); `.github/workflows/ci.yml:12, :85, :119, :536, :553` (every job `runs-on: ubuntu-latest`); `Sim.Ui.Tests/Fixtures/session-export/README.md:11` (the Director's session was recorded on Windows) |
| 3 | §4 "Order kinds 14–18" (was :115) | "Order kinds 14–18: EX, PR, RF, RE and DF." (the writer's comment says "16–18") | Kept 14–18 for the codebook, and added why the writer's note says 16–18: codebook v4 ends at kind 13; kinds 14–15 (ADR-044) were already written under `telemetry/v4`; only 16–18 (ADR-055) are the M7 line-shape change A15 retagged | `docs/m6-session-export/codebook-telemetry-v4.tsv:325` (last kind `CE`); `codebook-telemetry-v5.tsv:353–357` (`EX`, `PR`, `RF`, `RE`, `DF`); `Sim.Core/Observability/TelemetryWriter.cs:47` ("order kinds 16–18"), `:460` (the kind is written by name); `Sim.Core/Kernel/OrderLog.cs:232, :247, :268, :292, :302` (kinds 14–18); at `0bf7626`, `OrderLog.cs:232, :247` already define `Explore = 14`, `Prospect = 15` and `TelemetryWriter.cs:35` is `telemetry/v4` (`git show HEAD:…`) |
| 4 | §4 "The tag (A15)" (was :131) | "Six test pins that named `telemetry/v4` as the writer's tag now name v5:" followed by five files | "Five test pins, one test in each of five files, that named `telemetry/v4` as the writer's tag now name v5:"; the `InspectionTests` item now also says the test moves its readable set to include v5 | `git diff HEAD` of the five files: `Sim.Tests/Forensic/InspectionTests.cs:623` (writer tag; same test also `:679–684`, readable set and unknown vintage v6), `Sim.Tests/Forensic/ForensicIdentityTests.cs:149`, `Sim.Tests/Observability/TaxGlassBoxTests.cs:102`, `Sim.Tests/Observability/TelemetryTests.cs:192`, `Sim.Ui.Tests/SessionRecordTests.cs:121` |

## Amendment — 2026-10-29 (Director playtest): the raw telemetry log is dropped after a verified export, and the workbook is deflated

Authority: the Director's playtest findings of 2026-10-29, approved changes A and B. The governance record (a D-record)
is the coordinator's; this section records what changed in the exporter and why. It supersedes the text above where
the two disagree, and only there.

**Why.** After quitting, the six session files stayed beside the workbook, and the Director wants less disk used.
Measured on a 150-turn session: `telemetry-*.jsonl` 18.5 MB, the workbook 1.8 MB, trace 13 KB, forensic 11 KB, orders
1.6 KB, manifest 575 B. The raw telemetry is 91% of the folder, and the workbook already holds all of it: the self-check
(§6) decodes every Telemetry cell back to its line byte for byte before a byte is written. The STORED workbook also
compressed about 3.4 times.

### A. The raw telemetry log is removed after a verified export (the game, at session close)

**At session close** (`GameUi.SaveOnExit`), the order of §2 is unchanged: `SaveSession`, `FinalizeTelemetry`,
`ExportForensicClose`, then `UiSession.ExportWorkbook`. After the workbook is written, `ExportWorkbook` removes
`telemetry-STAMP.jsonl` (`SessionWorkbook.RemoveRawTelemetry`). It does so only when all of the following hold:
1. The export returned, so the self-check passed (§6: every cell read back, every telemetry cell decoded to its line
   byte for byte).
2. The export judged the log removable (`RawTelemetry.RemovedAfterExport`). The log was present. Its decoded lines,
   each followed by a line feed, rebuild the file byte for byte (`TelemetryBytesOf`). The forensic close record lists the
   file with the same sha256. Otherwise the export falls back to `RawTelemetry.Kept`, and its README says the log stays.
3. The workbook on disk is, byte for byte, the workbook that was self-checked.
4. The log on disk has the sha256 of the log the workbook holds.

If any of these fails, or the export is refused or fails, nothing is removed, which is the behaviour before this
amendment. A log that cannot be removed is kept and reported on stderr. The manifest, orders, trace, chronicle and
forensic files are never touched, because resume reads them. The close record is not rewritten: it still lists the
telemetry file with its sha256, and the Telemetry sheet holds exactly those bytes.

**The README** still lists every file with its sha256, telemetry included. For telemetry it adds that the raw log was
removed after the workbook was written and verified, that its whole content is the Telemetry sheet, and that the lines,
each followed by a line feed, are the file. The README's first row then names the five files that stay the record. With
`RawTelemetry.Kept`, the default and the headless command's mode, the README is exactly the README written before this
amendment.

**The switch (decision E1).** `sim-ui --keep-raw-telemetry` (`UiArgs.KeepRawTelemetry`, read into
`UiSession.KeepRawTelemetry` for a new or a resumed session) keeps the raw log, for developers. By default the log is
removed. It is a launch flag rather than a settings file, following `--dev`, and `UiArgsUsageTests` checks that it is
listed. It is not part of the world, the log or the replay.

**What else reads the telemetry file after close (checked against the tree):**
- **`--resume`** (`UiSession.Resume` and `ReplayTo`) reads the manifest, the order log and the trace, and never the
  telemetry. The replay re-observes every turn, so the first save after a resume writes the telemetry file again from
  record zero (`ExportTelemetry` starts a path it has not written from the beginning). Pinned: a closed session whose
  log was removed resumes with every turn checked against the trace (no resume notice), and the resumed session's
  telemetry export has the sha256 the close record lists for the removed file
  (`ASessionWhoseRawTelemetryWasRemoved_Resumes_AndRebuildsTheLogExactly`).
- **The headless `sim-ui --export-session` (decision E2).** It never removes a file. When the manifest's telemetry file
  is absent and a workbook is beside it, the telemetry is read back from that workbook: the Telemetry sheet's cells are
  joined and decoded, and the lines rebuild the bytes. The result is used only if its sha256 equals the one the forensic
  close record lists for the file. The README and the Forensic sheet then say "recovered from the earlier workbook's
  Telemetry sheet"; every other sheet is identical. Otherwise the export is the named refusal `telemetry-unrecoverable`
  (exit 2). The refusal covers a sheet that does not decode, a sha256 that differs, and a close record that is missing,
  and it leaves the existing workbook untouched. A re-export can therefore never replace a workbook with one that lacks
  the telemetry. This was chosen over a plain refusal because a re-export stays useful (a newer exporter, a damaged
  workbook copy elsewhere) and the close record makes the read-back exact. The game's close path applies the same rule.
- **`sim inspect`.** `SessionInspector.Open` (Sim.Core) reads the raw log if it is there. Without it, the topics that
  need telemetry (settlements, polities, movements, resources, happiness and others) answer NOT RECORDED, naming the file.
  They do not answer from a fresh run, as `SessionInspector` already guaranteed. Sim.Core cannot read the workbook,
  because the codec is in Sim.Ui and Sim.Core may not reference it, and Sim.Core is unchanged here. To get those answers,
  play with `--keep-raw-telemetry`, or rebuild the file by replay with the build that recorded the session:
  `sim inspect --manifest M --telemetry DIR/telemetry-STAMP.jsonl`. `sim inspect` makes no forensic sha check of the
  companions: the close record is read, not verified against the folder. The workbook's Forensic sheet compares the two,
  and a recovered export shows "same".
- **Test fixtures** are unaffected. They are file sets that the tests read through `SessionWorkbook.Build`, which never
  removes anything.

### B. The workbook is DEFLATE-compressed

`OoxmlZip` (formerly `StoredZip`, in `Sim.Ui/Export/OoxmlWorkbook.cs`) now writes every entry as follows:
- **DEFLATE** (method 8), raw deflate from `System.IO.Compression.DeflateStream` at `CompressionLevel.SmallestSize`;
- the CRC-32 of the uncompressed bytes;
- the compressed and uncompressed sizes in both the local header and the central directory.

Everything else in §3 stays: the hand-written container, the fixed entry order, DOS time 00:00 1980-01-01, "version made
by" and "version needed" 2.0 / MS-DOS, flags 0 (so no data descriptor), external attributes 0, no extra fields, no
Zip64. The hand-written container is kept because `ZipArchive` would still stamp the host OS and the time of export into
every entry. Keeping the headers fixed costs nothing. `OoxmlZip.WriteStored` keeps the STORED form of before this
amendment, and the tests use it to re-derive the old goldens.

**A14's "stored, byte-identical" is superseded for the container. The content stays pinned.** A runtime's zlib may
deflate the same bytes differently, so the bytes of the `.xlsx` container are no longer a goal across runtimes. Two
builds on one runtime still give the same bytes, and the tests check that. The goldens of §7 now compare the DECODED
PART CONTENTS. A workbook's entries are inflated by `ZipArchive` (`OoxmlWorkbook.Entries`) and re-packed with
`OoxmlZip.WriteStored`. That container is a pure function of the entry names, their order and their uncompressed bytes,
and its sha256 must equal the golden (`ExportFixtures.ContentSha`). The goldens of §7 hold unchanged under this reading:

| Workbook | sha256 of the decoded parts re-packed STORED (= the pre-amendment byte golden) |
|---|---|
| The Director's 68-turn session | `136e853c72d8596cb81785dd438dfeb6dbe527ca0e1ce53fd8532e8bf0c365e5` |
| The current-tree session | `6d488a2c97bcbb143f274ddf97784cf4daf170d301b3e1e340718b532139560d` |
| The small two-sheet escape workbook | `e65373b07c7eee9160c39bcbdad022f7c13d306fd399715be01cb883118a82b5` |

So every byte of every part is exactly what the STORED exporter wrote. The container test now walks the ZIP by hand and
checks the following for every entry:
- method 8;
- flags 0, time and date fixed, no extra field;
- the version fields;
- each entry inflates to exactly its stated size and CRC-32 and is smaller than its uncompressed size;
- the central directory agrees with the local headers (method, CRC, both sizes, offset).

The self-check of §6 is unchanged: `ZipArchive` inflates every entry, `XmlReader` parses it, and every cell and every
telemetry line is compared.

**Excel compatibility.** The container uses standard deflate (method 8, version needed 2.0) with a CRC-32 and correct
sizes, which is what Excel, LibreOffice and Python write and read. Checked on 2026-10-29:
1. **Python `zipfile`.** `testzip()` passed (it reads every entry and checks its CRC) on the deflated Director, current-tree
   and 150-turn workbooks. Every entry's name, order, size and sha256 equal those of the STORED workbook of the same
   files: 39 of 39 entries.
2. **LibreOffice 24.2.7.2, headless.** `soffice --convert-to csv` was run with all sheets on four deflated workbooks: the
   current-tree session, the Director's session, the 150-turn session, and that session's re-export recovered from its
   workbook. Every cell of every sheet equals the inline-string text in the parts (8 of 8 sheets, all four workbooks).
   LibreOffice's CSV of the current-tree workbook is byte-identical to its CSV of the STORED one.
3. **The prototype on LibreOffice's cells.** The prototype's `decode.py`, run on the 150 Telemetry cells as LibreOffice
   read them, gave back the 150-turn `telemetry-*.jsonl` byte for byte (18,551,061 bytes).

Microsoft Excel and Google Sheets were not run, as in §8, and nothing is claimed for them.

### Measured (2026-10-29, seed 42, 150 turns, one AI empire, `sim run --founded --emit-session`, then `sim-ui --export-session`)

| | Before (STORED, all files kept) | After (deflated, raw telemetry removed) |
|---|---|---|
| workbook | 1,793,722 bytes | 615,866 bytes (2.9 times smaller) |
| `telemetry-*.jsonl` | 18,551,061 bytes | removed (its 150 lines are the Telemetry sheet) |
| manifest + orders + trace + forensic | 26,401 bytes | 26,401 bytes |
| **folder** | **20,371,184 bytes** | **642,267 bytes (31.7 times smaller)** |

A session the game writes also has a chronicle. That session has none (`sim run` writes no chronicle). Other workbooks,
STORED then deflated: the Director's 887,862 → 274,855 bytes, and the current-tree session's 416,059 → 87,164 bytes.
Removing the raw log was simulated on this CLI session by deleting the file, because only the game's close path removes
it. The re-export then read the telemetry back from the workbook and matched the close record's sha256 `475bf0ec…`.
Its Manifest through Codebook parts are identical to the first export's, and only the README and Forensic parts differ,
which state the origin. The removal itself is pinned through the real `GameUi` by
`AtSessionClose_TheWorkbookIsWrittenAfterTheForensicClose_AndTheRawTelemetryIsRemovedOnceVerified`.

### Decisions made here

- **E1:** the developer switch is the launch flag `--keep-raw-telemetry`. The default removes the log.
- **E2:** the headless re-export of a session without its raw log reads the telemetry back from the existing workbook,
  verified against the close record's sha256. Otherwise it refuses with `telemetry-unrecoverable` and never replaces the
  workbook.
- **E3:** removal requires the close record to list the log with the same sha256, which is what makes E2 exact. A
  session closed without a forensic run record keeps its log.
- **E4:** the hand-written container stays, with deflate. The goldens are content goldens, expressed as the
  pre-amendment byte goldens of the decoded parts re-packed STORED.
- **E5:** Sim.Core is unchanged. `sim inspect` answers NOT RECORDED for the telemetry topics once the log is removed, and
  the remedy is `--keep-raw-telemetry` or a rebuild with `sim inspect --telemetry`. No simulation state, hash or pin moves.

## Amendment — 2026-10-29 (Director): coded sheets, legend-only words

Authority: the Director's request of 2026-10-29, verbatim: "the workbook should have 0 real words in anything that is
not a key or a legend. For example the word settlement appears. it should be shortened to somehting like ST. The
chronicle also. it has the word souls. make its SL. The chronicle should come out shortened. The telemetry is the
perfect example of this. Orders need to be shortened too." The coordinator's ruling fixed the assumptions; the
governance record (a D-record) is the coordinator's. This section supersedes the text above where the two disagree, and
only there.

### What stays prose, and what becomes codes

- **Legend sheets keep words:** `README` (prose; its new `codes` row says where codes are defined) and `Codebook`.
- **Every other sheet** (`Manifest`, `Orders`, `Trace`, `Chronicle`, `Telemetry`, `Forensic`) holds codes, numbers and
  identifiers only, its column headers included. Identifiers are hashes and hex (seven or more hex digits, one case),
  file names, version tags (`session-export/v1`, `telemetry/v5`, `chronicle-codes/v1`) and, in the key column of
  `Manifest` and `Forensic`, JSON key paths.
- **The Codebook is the single legend.** Its first rows are the telemetry codebook(s), unchanged. A `legend` header row
  follows, then one row per code: column A the section (`all`, `type`, `Manifest:col`, `Orders:col`, `Orders:kind`,
  `Orders:read`, `Trace:col`, `Chronicle:col`, `Chronicle:event`, `Chronicle:field`, `Telemetry:col`, `Forensic:col`,
  `Forensic:file`), column B the code, column D its meaning. After them come the session's `Chronicle:names` rows (what a
  named chronicle code names, e.g. `ST12` → `Bigen`) and `QT:texts` rows (the exact text of each `QT` code). The legend
  is defined once, in `Sim.Ui/Export/WorkbookCodes.cs`.
- **The in-game ANNALS panel stays prose** (`UiSession.AnnalLines`, unchanged), and so does the in-memory prose record
  `RecordAnnalLines`.

### The code scheme

| Sheet | Column headers | Values |
|---|---|---|
| all | — | `NP` file named but not present (row: `NP`, file name, then the close record's bytes, sha256 and state), `NN` the manifest names no such file, `EM` the file holds no line, `QT<n>` a text (see below), `>` continuation |
| Manifest | `KY VL TY` | key path (`$` the whole document); value: a string without letters or an identifier as is, any other string `QT<n>`, a number as written, bool `1`/`0`, null `~`, `{}` / `[]` empty; type `S N B O A Z` (`X` an exporter note) |
| Orders | `I T PL K KC TG TD AM AD AB` | `KC` the kind code, the telemetry codebooks' own: `RB LA SA EC TX RT AA DR FS CC RA CE EX PR RF RE DF`, `MF` reserved kind 10, `UK` unknown; `TD`/`AD` the decoded target/amount: `ST12`, `ST12/S3` (sector), `RG4`, `PL1`, `RN1069`, `LT-1` / `LC-2` (clear a lane), `AG3`, `SK7`, `FM40`, `ZR0` (reserved field), `PC45`, `MM-12.5`, `PJ1`, `CI462102`, `QS1`, `QS2/PJ1/T7`, `NV1.5`, `SV` / `AS` / `MT2` (prospect method), `ID301`, `CN`. `TG`, `AM` and `AB` (the 64-bit pattern) keep the exact values |
| Trace | `T Y PO SN FD H` (the file's header line) | data rows as in the file |
| Chronicle | `L EV` | one coded event line per row (below) |
| Telemetry | `T TC` | the coded cells, byte-identical |
| Forensic | `KY VL TY`, then `XF FN FS BY HN HC AG` | records as on Manifest; `close NC X` when there is no close record; the files table: `XF.MN/OR/TR/CH/TE/FR`, state `PS`/`NP`/`RW` (rebuilt from an earlier workbook), agreement `EQ`/`NE`/`NC`/`NL`/`LN`/`SF` |

**`QT` texts.** A string a session file holds is kept verbatim when it has no ASCII letter or is an identifier; any other
is `QT<n>`, numbered by first use in sheet order, equal texts sharing a code, its exact text in the `QT:texts` row. This
covers the manifest's `buildSha`/`buildDate` (`dev`, `local`), `platform` and `economyPackets`, and the forensic
record's prose and closed-set values (`rec`, every `*State`, `hashAlgorithm`, `runIdInputs`, `dtRule`, `rngDerivation`,
`aiNote`, the pipeline system names, the era band names, `limitations[*]`, `artifacts[*].role/state`, `gatesState`).
Identifiers that are real words (a system name such as `catchment`, a runtime id such as `ubuntu.24.04-x64`, a limitation
token such as `no-ai-at-m4`) are coded too: the Director's rule ("0 real words") outranks keeping them verbatim. Nothing
is lost: every `QT` code reads back to its text exactly.

### The chronicle: coded at the source

`chronicle-*.txt` (written by `UiSession.ExportChronicle`; `Sim.Cli` writes none) is now the CODED record:

```
chronicle-codes/v1
ST0=Bigen
ST10=Libur
ST2=Zareshu
…
Y0 FN ST0 SL438
Y0 FN ST2 SL456
…
Y20 MG ST10 SL22 PC7
Y70 AR ST2 AT26
```

(An excerpt of the seed-42 session of the `current-v5` fixture, replayed and written by this tree: 368 bytes coded, against
1,106 bytes of prose, with the same 16 events.)

Line 1 is the format tag `ChronicleCode.FormatTag`. The name lines `CODE=name` follow, in ordinal order of their code,
for exactly the codes the event lines use. They are LEGEND lines: the exporter moves them to the Codebook's
`Chronicle:names` rows. Then one event line per event, in record order: `Y<year> <EV> <fields>`. The core events
(`Sim.Core/Chronicle/ChronicleCode.Render`) are `FN` founding, `FA`/`FE` famine onset/end, `XT` extinction, `AR` first
artisans, `MG` migration surge, `DS` disaster, `SO`/`SE` food shortfall onset/end. The UI-side events
(`Sim.Ui/ViewModel/StateChronicle`, given the record) are `RM` research, `AE` Age, `TL` tax levy, `RD`/`RU` road
built/upgraded, `SC` structure, `IF`/`IM` institution founded/matured, `CP`/`RV` changed hands/revolt, `ME` merchant
class, `SR`/`SS` a class rose/subsided, `RQ` re-equipped. Fields: `ST` settlement id, `SL` souls, `PC` share (`PC<1`,
`PC>99`), `RS` famine reason ordinal, `YR` years, `DD` deaths, `AT` artisans, `PL`/`PF`/`PT` polity, `TB`/`TN` tax rate
before/now, `RN` `AG` `RC` `PJ` `IN` `CL` `ID` content keys (named in the name lines), `FC` formations.

- **Built from the event's values, never from prose.** The coded line takes the very values the prose binds: the same
  year and rounding, and the share rule shared with `ChronicleProse` (`ShareText`), so the two cannot disagree on a
  figure. A settlement is its id. Its name, as the `NameRegistry` gives it, appears only in a name line.
- **Same events.** Each coded line is appended in the same call that appends its prose line to the record, so P7's
  hidden-settlement filter and the event order are exactly as before. `RecordCodeLines[i]` is `RecordAnnalLines[i]`
  coded.
- **Names: the cleanest deterministic option.** The exporter reads the session files only. No other file holds the
  settlement names, and deriving them would mean re-founding the world, which §1 forbids. So the chronicle file carries
  them as legend lines, only for the codes it uses. A hidden settlement (P7) is never named, because its events are never
  appended.
- **Readers.** Resume never reads the chronicle (it copies the file aside on a schema change). `sim inspect` does not
  read it. The forensic close record hashes whatever bytes are written. The exporter reads it as above. A
  pre-amendment PROSE file (no tag, e.g. the `current-v5` fixture) is not parsed: each line is shown as its `QT` code,
  and its text is in `QT:texts`.

### Goldens (§7) re-pinned, with attribution

Diffing the decoded parts, old (the deflate amendment above) against new, for both fixtures: `[Content_Types].xml`,
`_rels/.rels`, `xl/workbook.xml`, `xl/_rels/workbook.xml.rels` and `xl/styles.xml` are byte-identical. The eight sheet
parts change:
- README: the `codes` row, and sheet descriptions naming the codes.
- Manifest, Orders, Trace, Chronicle, Forensic: coded as above.
- Telemetry: the header row only (`turn`, `cell …` → `T`, `TC`). Every data row, turn and cell, is byte-identical:
  68 of 68 rows for `director-68`, 16 of 16 for `current-v5`.
- Codebook: the telemetry codebook rows are unchanged as its first rows (339 for `director-68`, 371 for `current-v5`).
  The legend and the session's names and texts are appended after them.

| Golden | Was | Now |
|---|---|---|
| `DirectorWorkbookSha` (director-68) | `136e853c…c365e5` | `806e0947fec8a0e6a126d1dca37a94aa3afa98d71784c5a822cb03af240f1fb1` |
| `CurrentWorkbookSha` (current-v5) | `6d488a2c…139560d` | `c5ec8abd82a11de7bdead82d1e342e21055de74a88c50ad125ba8dd3df578313` |
| `SmallWorkbookSha` (the writer, two fixed sheets) | `e65373b0…18a82b5` | unchanged: `OoxmlWorkbook` and its input are untouched |

Deflated workbook sizes: the Director's 274,855 → 281,467 bytes, and the current-tree session's 87,164 → 94,229 bytes.
Most of the growth is the forensic prose, which moved to `QT:texts`, plus the legend.

`RecoverRemovedTelemetry` and `JoinTelemetry` are unchanged: they skip the Telemetry sheet's one header row and read
column B. The self-check still reads every cell of every sheet back, legend included, and decodes every telemetry cell.

### Tests

`Sim.Ui.Tests/SessionExportCodesTests.cs`:
- The Director's criterion as a strict guard. No cell of a non-legend sheet may hold three or more ASCII letters in a
  row, unless it is an identifier or a JSON key.
- Every letter run of every such cell must be a code that the legend defines for that sheet.
- The guards have teeth.
- A new session writes the coded file, and its workbook shows it.
- The coded record pairs the prose record one for one, P7 included. The prose record is byte for byte the frozen
  `current-v5` chronicle, so the prose did not move. The ANNALS panel is that record under D-060's filter.
- The coded Orders rows keep every amount's exact bits, and each kind has its coded reading.
- The state chronicle's events (AE, TL, SC, IF, RV, CP, IM, SR, SS) are coded field for field, one coded line per
  prose line, with the prose unchanged.

`Sim.Tests/Systems/ChronicleTests.cs`:
- Every core event kind's coded line, field for field, against its prose.
- The record file: tag, then names in ordinal order, then events.

`AnnalsTests.Annals_FoundingLinesAtStart_ExportMatchesPanel` now pins the FILE as the coded record, because the file
format changed. The panel half of that test is unchanged.

### Decisions made here

- **F1:** the codes are two letters (one for the Manifest/Forensic types and a few column keys). No code has three
  letters, so any run of three letters in a data cell is a defect, and the guard can be strict.
- **F2:** the order-kind codes are the telemetry codebooks' `enums kind` codes, plus `MF` and `UK`.
- **F3:** names live in the chronicle file as name lines (legend lines). No settlement name appears in a data cell.
- **F4:** a pre-amendment prose chronicle is shown through `QT` codes, never parsed.
- **F5 (amends E5):** Sim.Core gains `Sim.Core/Chronicle/ChronicleCode.cs`, and `ChronicleProse.FormatYear` and
  `ShareText` become `internal` shared helpers (prose output unchanged). This is the observational chronicle, outside
  the determinism surface. No simulation state, hash or pin moves.
