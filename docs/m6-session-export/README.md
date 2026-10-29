# M6 session export: one file per session, and the telemetry encoding table

**Status: DESIGN INPUT for the M6 spec (Director's instruction, 2026-10-19). Nothing here is built into the game.**
The request and its agenda entry are in `docs/queue.md` (dated note 2026-10-19) and `docs/milestones.md` (M6 row).

## 1. The problem

One played session writes six files into `runs\`: `orders-*.bin`, `session-*.json`, `trace-*.csv`,
`chronicle-*.txt`, `telemetry-*.jsonl`, `forensic-*.jsonl`. AI assistants accept at most five files per upload. The
telemetry alone is ~96,000 characters per turn (the Director's 68-turn session; up to 214,809 in a 300-turn two-AI
game), far over Excel's 32,767-character cell limit.

## 2. The answer: one workbook per session

`session-<stamp>.xlsx`, written BESIDE the six files (they stay the record: resume reads the binary log, the manifest
and the trace). Upload only the workbook.

| Sheet | Content |
|---|---|
| `README` | How to read the workbook; the build, seed and platform; "EMPTY never means 0 — it means the codebook default". |
| `Manifest` | `session-*.json` as key / value rows. |
| `Orders` | The binary log decoded: turn, issuer polity, kind name, target (settlement and sector spelled out), amount. |
| `Trace` | `trace-*.csv` as is (turn, year, population, settlements, food, hash). |
| `Chronicle` | One line per row. |
| `Telemetry` | Column A turn, column B the coded cell (one turn = one cell). |
| `Codebook` | `codebook-telemetry-v4.tsv` (this folder): the encoding table every coded cell is read with. |
| `Forensic` | The run and close records as key / value rows, with each artifact's sha256. |

## 3. The encoding table (`codebook-telemetry-v4.tsv`)

A lookup table from short codes to telemetry fields: 337 rows, 18.7 KB. Columns:
`sheet | code | pos | json field | type | default when EMPTY | notes`.

- **Cell grammar.** `turnSegment ('|' settlementSegment)*`. A segment is space-separated sections `CODE:payload`.
  Tuple payloads list values by position (`,`); tables list rows (`/`). An empty value means the codebook default;
  `~` means absent or empty.
- **What makes it small.** Field names become positions, names become their ids, defaults are left empty, and the
  migration table that is identical for every settlement is written once per turn (`A`).
- **Numbers** are the writer's exact text (`0.` shortened to `.`), so decoding is byte-identical. `N`, `I`, `-I` are
  NaN and ±Infinity.
- **Excel-safe.** Every cell starts with `v4`, never `= + - @`.

### Measured (characters per turn)

| Game | Original median / max | Coded median / max |
|---|---|---|
| Director's Windows session, 68 turns, 1 AI | ≈96,000 / 103,185 | **8,877 / 10,096** |
| seed 42, 1 AI, 600 turns | 153,087 / 190,689 | **13,127 / 18,557** |
| seed 7, 2 AI, 300 turns | 161,190 / 214,809 | **13,788 / 17,828** |

Every line round-trips byte-identical (1,268 lines, including all 68 of the Director's). 98 adversarial edge cases
pass. The coded cells are smaller than gzip+base64 of the same lines, and stay readable.

**Limit.** About 650 coded characters per settlement: a cell overflows at ≈49 settlements (measured on a synthetic
world). A 600-turn two-AI game is projected at ≈40 (INFERRED). The M6 spec must define a continuation cell.

## 4. The prototype (`prototype/`)

Python 3, design reference only (the M6 exporter is C#, in Sim.Ui or Sim.Cli, never Sim.Core).

```
python3 prototype/encode.py telemetry-X.jsonl cells.txt      # one coded cell per line
python3 prototype/decode.py cells.txt back.jsonl             # back.jsonl is byte-identical to the input
python3 prototype/roundtrip.py telemetry-X.jsonl ...         # checks every line
python3 prototype/make_codebook.py                           # regenerates prototype/codebook.tsv from spec.py
```

`spec.py` is the single source of the table: `make_codebook.py` regenerates `codebook-telemetry-v4.tsv` byte for
byte (checked 2026-10-19).

## 5. Required before M6 ships

1. **Size guard:** never truncate; split an over-limit cell at a `|` into a marked continuation cell.
2. **Self-check:** the exporter decodes every cell and refuses (a named error) on any mismatch. No raw fallback.
3. **C# number text:** reuse the writer's exact double text; compare defaults as text (keeps −0 distinct);
   InvariantCulture; no Dictionary iteration; no `float`.
4. **Versioning:** one codebook per telemetry schema; a writer change needs a new codebook before export works.
5. **Goldens:** the prototype's cells for the measured runs become the C# port's golden tests.

---

## 6. Dated note, 2026-10-28: built in M6 (ADR-059; D-066 A14–A17)

The design above is now built, in `Sim.Ui/Export/`. Sections 1–5 are kept as written. This note records what changed
around them; [ADR-059](../adr/adr-059-one-file-session-export.md) has the details.

**Where the export runs.** The game writes `session-STAMP.xlsx` when a session closes, after the forensic close record.
`sim-ui --export-session DIR|MANIFEST` writes the same workbook headlessly.

**Two codebooks (§5.4).**
- `codebook-telemetry-v4.tsv` is unchanged, byte for byte. It reads `telemetry/v2`, `v3` and `v4` lines.
- `codebook-telemetry-v5.tsv` reads `telemetry/v5`, the tag the writer now emits (D-066 A15). It adds:
  - the optional `grain.military` and `grain.fodder` keys;
  - the names of reasons 17–21;
  - 23 goods;
  - order kinds 14–18;
  - constant C2's military-grain clause;
  - the continuation rule.

**`make_codebook.py`.** It now writes four files into this folder: both TSVs and their machine-readable twins
`codebook-telemetry-v4.json` and `-v5.json`, which the C# codec reads. `make_codebook.py --check` fails when any of
them is stale; CI runs it. It no longer writes `prototype/codebook.tsv`.

**Continuation cells (§5.1).** Section 3's "Limit" paragraph asked the M6 spec to define a continuation cell; D-066 A17
defines it. A continuation row has an EMPTY column A, and its cell starts with `>` followed by the `|` that was cut.
`codec.split_cell` and `codec.join_cells` are the prototype's versions.

**Goldens (§5.5).** The prototype's cells for the Director's 68-turn session and for a 16-turn current-tree session
are the C# port's goldens. They are in `Sim.Ui.Tests/Fixtures/session-export/`.
