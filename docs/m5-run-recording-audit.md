# M5 run recording — audit of the six session files, "encoding", "encryption" and the one-file export (2026-10-20)

**Status: AUDIT (read-only, verified against the tree at `f80dce6`). Nothing in the recording path was changed.** The
Director asked (2026-10-20) whether run recording is "encoded and encrypted" and whether an earlier implementation
tried to merge six files into one. This record answers with evidence and stops before any redesign: no approved design
for consolidation-as-replacement or for encryption exists (§6), so the Director rules first (§8).

## 1. The six files

One played session writes six files into `runs\` (`Sim.Ui/Program.cs`, `Sim.Ui/GameUi.cs`). The headless CLI
`--emit-session DIR` writes the same set through `Sim.Cli/SessionEmitter.cs`, except the chronicle.

| File | Contents | Writer | Readers | Protection today | Consolidation status |
|---|---|---|---|---|---|
| `orders-<stamp>.bin` | The binary order log: magic `CIVORDR\0`, `IoVersion` 1, count, then per order turn, actor, kind, target and amount (`Sim.Core/Kernel/OrderLog.cs`) | `UiSession.Save`, after every turn | Resume (replays it); `sim replay --orders`; `sim inspect`; the schema guard; CI `cmp` and replay (`.github/workflows/ci.yml`); fixtures | Serialized only. No CRC, compression or encryption. Its sha256 sits in the forensic close record. | Separate file. Not merged. |
| `session-<stamp>.json` | The manifest (`session-manifest/v2`): seed, size, settlements, AI count, schema version, build, platform and the other files' names | `UiSession.ExportManifest`, once at launch | The resume entry point; `--resume runs` (newest manifest); `Continue last game.cmd`; `sim inspect --manifest`; CI | Plain JSON. Its sha256 sits in the close record. | Separate file. |
| `trace-<stamp>.csv` | `turn,year,population,settlements,food,hash`: one row per turn, ending in the world hash | `UiSession.ExportTrace` | Resume checks every replayed turn's world hash against it; `sim inspect` verdict; the close record's final hash | Plain CSV. The per-turn world hash is a determinism check, not file integrity. | Separate file. |
| `chronicle-<stamp>.txt` | The annals, one line each (world-wide; D-060 §6) | `UiSession.ExportChronicle` | No parser. Hashed in the close record; copied aside on an unchecked resume. | Plain text. | Separate file. |
| `telemetry-<stamp>.jsonl` | `telemetry/v4`: one JSON line per turn (world and per-settlement records) | `UiSession.ExportTelemetry` (appends), finalized on exit | `sim inspect` question answering. Resume does not read it. | Plain JSON lines (≈96,000 characters per turn in the Director's session). | Separate file. The compact encoding is designed for M6, not built. |
| `forensic-<stamp>.jsonl` | `forensic/v1`: the run record at launch, then the close record with each companion file's role, name, size and **sha256** | `ExportForensicRun` / `ExportForensicClose` (`ForensicSession.cs`) | `sim inspect`; tests (`SessionEmitterTests`) | Plain JSON lines. Unkeyed SHA-256 of the other five. | Separate file. |

## 2. Encoding, compression, integrity, encryption — four different properties

| Property | Meaning | In the session files today |
|---|---|---|
| **Serialization / encoding** | A defined format for the data: the binary order log, JSON, CSV. The M6 codebook (`docs/m6-session-export/`) is a further, lossless *encoding* that shortens telemetry. It is readable by anyone with the codebook. | Serialized: yes. The M6 codebook encoding: designed and prototyped in Python, **not built**. |
| **Compression** | Fewer bytes, reversible, with no secrecy (gzip, Brotli). | **None.** `System.IO.Compression` is used only for PNG art. |
| **Integrity** | Detects change. Unkeyed hashes detect accidents. Only a keyed MAC or a signature detects deliberate tampering. | Unkeyed SHA-256 of five companions in the close record. **No production code checks it** (only a test compares it). The world hash per turn proves replay determinism, not file integrity. No MAC, no signature. |
| **Encryption** | Secrecy: unreadable without a key. | **None.** No AES, ChaCha, DPAPI, cipher or key anywhere in `Sim.*`, `scripts/` or the workflows. |

Anyone with the files can read every one of them; that is how resume, `sim inspect`, CI and the Director's uploads to
AI assistants work today.

## 3. What was asked, and what happened

- **2026-10-19, merge request** (`docs/queue.md`, dated note): "Make it all into one single excel sheet if possible.
  Since both claude and gpt do not accept more than 5 files at a time. Merge it all into one single excel in M6. add this
  into future agenda". Recorded as an M6 agenda item: commit `0c448e8` ("Nothing is built for it in M5").
- **2026-10-19, size request** (`docs/queue.md`): "figure out a code/encryption to reduce the size of telemetry. Make
  internalized codes. Each turn then becomes one single cell." The same note records it as **"Encoding, not
  encryption"**: a 337-row codebook and one coded cell per turn, lossless. Commits `a5cac13` (measured design) and
  `827337e` (`docs/m6-session-export/`: README, `codebook-telemetry-v4.tsv`, Python prototype).
- **The Director's later instruction** (2026-10-19, in session; not in any repository document): "Do not start M6 or
  modify the telemetry encoding/codebook."
- **Branches.** These commits are on `claude/new-session-yavghm` only (pushed; not on `main`). No other branch carries
  an implementation.

## 4. Status of the single-file consolidation

**Designed (on paper and in a Python prototype), deferred to M6, never started in C#.** No xlsx, workbook, codebook
reader or spreadsheet library exists in `Sim.*` or any project file. It did not *fail*: it was deliberately placed in
M6 by the Director, and M6 has not started (`docs/milestones.md`). The design writes the workbook **beside** the six
files ("they stay the record: resume reads the binary log, the manifest and the trace", `docs/m6-session-export/README.md`
§2). Whether M6 adds an export beside the record or replaces the record is left open for the M6 spec (`docs/queue.md`).

## 5. What depends on separate files (why replacing them is a contract change)

- **Resume** reads the manifest, then `orders-*.bin`, then `trace-*.csv`, and checks every replayed turn's hash.
  `--resume runs` finds the newest `session-*.json` by name; D-056 copies the order log, trace and chronicle aside by path.
- **Replay / determinism**: `sim replay --orders <file>.bin` needs a bare order log; the schema guard finds the manifest
  from the order log's sibling name; `sim inspect --manifest` opens each file.
- **Forensic record**: the close record hashes five named files; a container would change what is hashed.
- **CI** (`ci.yml`): `cmp` of two `orders-*.bin`, `replay --orders`, `inspect --manifest`, a Python parse of the order
  log. The Windows zip's `Continue last game.cmd` needs `session-*.json` in `runs\`.
- **Save compatibility**: the v32 and v33 fixtures are separate files with recorded SHA-256s; manifest v1/v2,
  telemetry v2–v4 and order log `IoVersion` 1 readers would all need migration.
- **Tests**: about 20 test classes across `Sim.Tests` and `Sim.Ui.Tests` build or read the separate files.
- **Governance**: replacing the files changes the session-record contract (D-008 "replay is the recovery path",
  ADR-034, ADR-042, CR-022) and needs an ADR (`docs/queue.md`).

An export written **beside** the six files breaks none of these.

## 6. No approved encryption design exists

No document in `docs/`, `.github/`, `scripts/` or `CLAUDE.md` describes encryption, a threat model or key management.
The only "encryption" in the record is the Director's word, which the same note recast as encoding. So encryption is
**not authorized by an existing design**, and none was built.

## 7. What was implemented in this pass

Nothing in the recording path. Two stale texts were found and left for the M6 spec, as noted: the `SessionEmitter` doc
comment says a session "writes five files" (`Sim.Cli/SessionEmitter.cs:12`), and `docs/queue.md` cites a moved line
number for `SessionLogPath`.

## 8. Recommendation and the ruling needed

Recommended (option 1). The other options are listed so the ruling is explicit.

1. **M6 export beside the record, no encryption (recommended).** Build the one-file workbook as designed: one upload,
   lossless, readable by the Director and by AI assistants; the six files stay the save and replay record. No save,
   replay, CI or hash change. Encryption adds nothing here, because the goal is to share the file with AI tools, which
   need it readable.
2. **Export beside the record, plus optional encryption of the export only.** Needs a threat model first (who must be
   kept out: other users of the PC? anyone the file is mailed to?). A recognized AEAD (AES-GCM in .NET's
   `System.Security.Cryptography`) with a key derived from a passphrase the Director types (PBKDF2/Argon2id), never
   stored in the file or the build. An encrypted export cannot be read by an AI assistant without the passphrase, which
   defeats option 1's purpose.
3. **Replace the six files with one container** (encrypted or not). Changes the session-record contract; breaks resume
   of every existing session, CI's replay legs and the fixtures unless a versioned migration is built. Needs an ADR and
   a CR. Not recommended.

**Ruling needed:** (a) confirm that "encryption" in the 2026-10-19 request meant the size-reducing encoding (option 1),
or name the threat that encryption must answer (option 2); (b) confirm that the M6 export sits beside the six files
(not option 3). Integrity against tampering (a keyed MAC or signature) is a separate question, only needed if session
files must be trusted when they come from someone else.

## 9. Director's ruling — 2026-10-20 (dated; §1–§8 unchanged)

The Director, verbatim: "Encryption is supposed to reduce the size. Like Instead of "bigen had a population of 560
children 780 adults and 450 old" It becomes reduced/encoded/encrypted to B1PC560A780O450 That reduces number of
characters. The core issue was that we have too many files right? We're trying to bring all the runs data into one excel
sheet. So it's easier to upload here. Since claude and gpt have a limit on number of files".

- **§8 (a) is answered:** "encryption" means a size-reducing **encoding** (short codes in place of words, decodable
  with the codebook). No secrecy is wanted, so no cipher, key or threat model is needed. This is option 1, and it is
  what `docs/m6-session-export/` already designs. Example: the codebook writes a settlement's population by age band
  as a short positional tuple, not field names. Measured: the Director's session dropped from ≈96,000 to 8,877
  characters per turn (median).
- **§8 (b) is answered:** the goal is one uploadable file (an Excel workbook) per session, because AI assistants limit
  the number of files per upload. The workbook is written **beside** the six files, which stay the save and replay
  record, so resume, replay, CI and saves are unaffected.
- **Milestone unchanged: M6.** Nothing is built in M5. The 2026-10-19 instruction "Do not start M6 or modify the
  telemetry encoding/codebook" still stands; this note records the ruling for the M6 spec.
