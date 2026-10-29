# ADR-059: The one-file session export

**Status: ADOPTED UNDER DELEGATION (D-066 A14–A17; Director confirmation requested at the exit gate).** This ADR records the M6 closure track EXPORT under the D-066 adoptions A14–A17, which the
Director confirms at the exit gate. Every choice below is reversible: nothing in the simulation reads the workbook, and
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

This keeps A14's intent: no package, stored entries, the same bytes on every OS. It departs from A14's letter on one
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
- **Order kinds 14–18:** EX, PR, RF, RE and DF.
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
`runIdInputs` states. Six test pins that named `telemetry/v4` as the writer's tag now name v5:
- `SessionRecordTests`;
- `InspectionTests` (the T4.21-5 test; v6 is now the unknown vintage);
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
