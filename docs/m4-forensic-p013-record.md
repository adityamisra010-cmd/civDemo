# M4 Forensic Observability — P0 / P1 / P3 implementation record

**Branch `m4-forensic-p013`, cut from `2807155` on `t4.19-glass-box`. NOT MERGED. `main` = `dbef61a` untouched.**
**Basis: `docs/m4-forensic-observability-proposal.md` on `origin/m4-forensic-proposal`, read in full before any code.**

**Numbering caveat, stated the way the proposal stated its own.** The director's literal eighteen-item
report list was not in my packet, and no document in `docs/` at `2807155` contains it. The eighteen
sections below are reconstructed from the packet's own structure — the approval, the fence, the two
information boundaries, the reconstruction contract, the three phase briefs, the test list and the
validation list. Every item is stated by SUBSTANCE as well as by number, so a renumbering costs a heading
edit and nothing else.

---

## 1. What was approved, what landed, what was refused

Approved and implemented: **P0** (telemetry write path), **P1** (identity + provenance), **P3** (headless
inspection / explainability).

**Refused, and not implemented — no part of any of these is in the diff:**

| refused item | status in this branch |
|---|---|
| any new authoritative serialized state | none. `CanonicalSchema.Version` is **24**, unchanged; no table added |
| pairwise migration destination attribution | **not implemented, and explicitly refused in code** — see §13 |
| granary-capacity extraction | not implemented; recorded as a limitation (§15) |
| any happiness extraction that changes architecture | **not implemented** — see §14. The proposal's copied per-factor calculator is NOT in this branch |
| any schema-24 change | none |
| any M5+ work | none |
| any Class B visibility extraction (S15–S18) | none. No file under `Sim.Core/Systems` or `Sim.Core/State` was touched |
| the telemetry v3 null convention change (S20/S21) | **not touched.** `telemetry/v2` still writes the strings `"NaN"`/`"Infinity"`; the new reader accepts that convention rather than changing it |

**Fence compliance, measured (`git diff 2807155..HEAD --stat`, i.e. this branch against the candidate it
was cut from):** the diff is EMPTY under
`Sim.Core/Systems`, `Sim.Core/State`, `Sim.Core/Worldgen` and `Sim.Data`. Under `Sim.Core/Kernel` exactly
one file changed — `SessionManifest.cs`, which the fence names as artifact code — and that change is one
additive optional field (§8). `SessionTrace.cs` was NOT changed. No golden moved. No file under
`Sim.Tests/Goldens` or any corridor pin was touched.

---

## 2. P0 — the defect, verified at source

Verified at `2807155`, `Sim.Ui/UiSession.cs:322-327`:

```csharp
public void ExportTelemetry(string path)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    using FileStream file = File.Create(path);
    TelemetryWriter.WriteAll(file, _observations);
}
```

`SimUiGame.SaveSession()` calls it on **every End Turn** (`SimUiGame.cs:419`), and `WriteAll` walks the
entire accumulated history. So each save truncated the artifact and re-serialized every turn played so far.
Two consequences, not one:

1. **Write amplification.** Total bytes written over an N-turn session is the sum of every intermediate
   file size — quadratic in N.
2. **A validity window as long as the file.** `File.Create` TRUNCATES FIRST. A process death during a save
   left a file missing *every* turn already played, not just the last one. The method's own doc comment
   claimed "a crash mid-write loses at most the last End Turn"; that was not true of the code beneath it.

**The fix.** `telemetry/v2` has **no header line** — the schema tag rides on every line, written inside
`TelemetryWriter.WriteTurn` — so an append is a legal telemetry file by construction and nothing has to be
written once. `ExportTelemetry` now opens `FileMode.Append`, writes only the records observed since the last
call, and **flushes each record before starting the next**, counting it as written only after the flush. A
path this session has not written before is written whole, so an export is still an export.
`FinalizeTelemetry` appends what remains and forces the bytes past the OS buffers; `SimUiGame.OnExiting`
calls it. No buffering-in-batches compromise was needed: true append is safe here.

**No simulation state is touched.** The method reads the observation log and writes a file.

---

## 3. P0 — the measurement (650-turn representative run)

Seed 42, size 256, 4 settlements, 650 turns, one save per End Turn. The candidate's body and the new one
run in the same process on the same tree, so the only variable is the write path. Bytes written are counted
by the write path itself (`UiSession.TelemetryBytesWritten`), and cross-checked against the file size.

| | candidate `2807155` (rewrite-all) | this branch (append) |
|---|---|---|
| artifact size | **18,758,500 B** | **18,758,500 B** (byte-identical file) |
| **TOTAL BYTES WRITTEN over the run** | **6,081,585,283 B** | **18,758,500 B** |
| **write amplification** | **324.20x** | **1.00x** |
| wall clock, whole run | 24,590 ms | 2,892 ms |
| of which telemetry export | 22,392 ms | 184 ms |

The artifacts were compared byte for byte and are identical, so no reader of `telemetry/v2` can tell which
path wrote the file. Nothing beyond this defect was optimised.

---

## 4. P0 — crash semantics, and the test that proves them

`Sim.Ui.Tests/TelemetryAppendTests.cs` (5 tests):

- **`AppendingEachTurnProducesEXACTLYTheFileTheWriteAllPathProduced`** — byte equality against
  `TelemetryWriter.WriteAll`. The artifact contract did not move.
- **`WriteAmplification_...IsTheSizeOfTheNEWRecordNotOfTheFile`** — asserts, for every save, that the bytes
  written equal the byte count of exactly that turn's line, and that total bytes written equal the final
  file size EXACTLY. Not a ratio: the append path has no second writer, so there is nothing to round.
- **`KilledMidWrite_EveryCOMPLETEDRecordBeforeTheDeathIsStillValid`** — the artifact is truncated at eleven
  points across the file, including inside records, and every line completed before the cut still parses as
  a whole `telemetry/v2` record with the right turn number.
- **`SavingTwiceWithNoNewTurnWritesNOTHING`** and **`AFreshPathIsWrittenWHOLE`**.

---

## 5. P1 — the artifact

**ONE new file: `runs/forensic-<stamp>.jsonl`, tag `forensic/v1`.** Append-only JSONL, one record per line,
tag on every line. Two record kinds land in this packet:

- **`run`** — written ONCE, before the first turn, for the manifest's own reason: a session that ends in a
  crash is still identified, because the identity is on disk before anything else happens.
- **`close`** — APPENDED at the end. **Its absence is itself the evidence that a session did not close
  cleanly**, which no other artifact in the set can express.

Versioned **independently** of schema 24 and of `telemetry/v2`. Written by the session alongside the
existing five files. Everything in it is a READ of what is already in memory at launch, or a content digest
of it — no simulation state is read, written or recomputed.

New files, all under `Sim.Core/Observability/Forensic/` (a placement that is load-bearing, not cosmetic:
`check-read-isolation.sh` allowlists `Sim.Core/Observability/` by path prefix): `ForensicSchema.cs`,
`ForensicRecords.cs`, `ForensicIdentity.cs`, `ForensicWriter.cs`, `ForensicReader.cs`, `ForensicSession.cs`.

---

## 6. P1 — identity, and the choice the director asked to be stated

**`runId` is content-derived, NOT a wall-clock timestamp.**

```
runId = first 16 lowercase hex of
        sha256( "civ-sim/forensic/v1" | seed | sizePx | settlements | founded
                | canonicalSchemaVersion | configDigest | ordersDigest )
        each field length-prefixed, UTF-8
```

The recipe is printed IN the artifact (`runIdInputs`), so a reviewer holding only the file can recompute it.

**Excluded, each for a stated reason, and each asserted by a test:**

- **the wall clock** — so the id is reproducible. Two runs of the same world with the same log produce the
  same id and byte-identical artifacts (`TWOIdenticalHeadlessRunsProduceTheSAMEIdentityAndBYTEIDENTICALArtifacts`).
- **the build sha** — it is the literal string `"dev"` on every locally built binary, so including it would
  collide every locally played session with every other. The build is recorded BESIDE the id, with
  `buildShaRecorded:false` when it is the fallback.
- **the turn count** — an outcome, not an input. It lives on the `close` record.

**Filename vs identity.** The played session keeps the existing wall-clock stamp convention, so the forensic
file is twinned with `orders-<stamp>.bin` like the other four. The headless CLI names its files by **runId**
instead — `Sim.Cli` is sim code and may not read a clock at all, and a content-addressed filename means a
set that should be identical *is* identical.

**Cross-artifact binding is by CONTENT.** The `close` record carries `{role, file, bytes, sha256}` for
manifest, orders, trace, chronicle and telemetry. Today the manifest names its companions by filename only,
so a swapped same-named file is undetectable and surfaces downstream as `REPRODUCTION FAILED` — a
determinism verdict on what is actually a provenance defect. A file that is not present at close is recorded
as absent with explicit nulls, never as a zero-byte artifact.

---

## 7. P1 — provenance, field by field

All from information already in memory. Present on the `run` record:

| what | how |
|---|---|
| build SHA / build date / `buildShaRecorded` | READ, caller-supplied (`Sim.Core` interrogates no runtime) |
| platform | READ, caller-supplied RID |
| seed, sizePx, settlements, founded | READ |
| schema version | `CanonicalSchema.Version` = **24** |
| **worldgen configuration** | **content digest (sha256 + byte count) of EVERY embedded config resource** — measured: 9 files (`chronicle, corridors, era-pacing, goods, needs, pipeline, pipeline.toy, sim, worldgen`), enumerated by reflection in ORDINAL NAME ORDER so the digest is stable, plus one `configDigest` over the ordered pairs |
| **AI configuration, honestly** | `aiEmpiresConfigured` READ from `WorldgenConfig.AiEmpires` and emitted **explicitly as 0**, never omitted, with a note saying there is no AI actor in this build: no system emits an order and the sole polity carries `CommandSource.Player`. "There is no AI" is a recorded fact about the configuration; it is a different thing from an AI decision that went unrecorded |
| **turn configuration** | the full era band table (name, startDay, endDay, dtDays) + the pipeline **in order** (position, systemId, name — measured: 16 systems) + the dt rule stated in words (dt chosen at TURN START, integrated with `dtYears`, clock advances LAST) |
| artifact schema versions | canonical 24, `session-manifest/v2`, `telemetry/v2`, the trace header, `forensic/v1` |
| **replay identity** | `hashAlgorithm: "sha256/canonical-stream"` **and** `hashCoversSchemaVersion: false` — the stated fact that `CanonicalSchema.Write` begins at the seed, so a world hash does NOT self-identify its schema; plus the content-addressed hashes of every companion at close |
| terrain content hash | READ from `world.Terrain.ContentHash` (measured present on a founded run); null + stated reason on a terrain-less world |
| RNG provenance | the stream-derivation rule stated, so the seed is known to be the whole of it |

**Nulls are explicit and structural.** Every nullable field is written as JSON `null` beside a sibling
`<field>State` string saying why. There is no path in `ForensicWriter` that can emit a non-finite double as
a string, a sentinel integer standing for absence, or a bare zero standing for "no row". Asserted by
`NullsAreEXPLICIT_neverNaNStrings_neverSentinels_andAlwaysPairedWithAState`. **This applies to the NEW
artifact only** — `telemetry/v2`'s `"NaN"` convention is untouched, as ruled.

---

## 8. P1 — the manifest field, named exactly

**The manifest gains exactly one field: `forensicFile` (a string; `SessionManifest.ForensicFile`).**

**The tag does NOT move — it is still `session-manifest/v2`.** `SessionManifest.Read` is a tag WHITELIST
that throws on an unknown vintage, so a `v3` file would be REJECTED by every binary already built, including
the shipped UI and any previously built `sim inspect`. An additive key inside v2 is read by those binaries
exactly as before: they ask for the keys they know by name and ignore the rest. A manifest written before
this packet reads back with an empty `ForensicFile`, on the same argument `TelemetryFile` made in T4.19 —
the session it describes is still fully reproducible without it. Both directions are asserted
(`TheManifestGainsAFORENSICREFERENCEWithoutMovingItsTag`).

---

## 9. P1 — the CLI side: `--emit-session`

Before this packet **no CLI verb could write a manifest, a trace, a telemetry file or a chronicle**;
`--telemetry` existed only on `sim inspect`, which *requires* a manifest and takes its turn count from the
trace beside it. A reproducible headless forensic run was therefore impossible without hand-authoring
session metadata first.

`sim run … --founded --emit-session DIR` and `sim replay … --founded --emit-session DIR` now write the whole
set: `session-<runId>.json`, `orders-<runId>.bin`, `trace-<runId>.csv`, `telemetry-<runId>.jsonl`,
`forensic-<runId>.jsonl`. **Through the same writers the played session uses** — `SessionManifest.Write`,
`SessionTrace.Line`, `OrderLog.Save`, `TelemetryWriter.WriteTurn`, `ForensicSession` — so there is no second
implementation to disagree with the first. The telemetry is appended per turn, never rewritten (P0's rule,
applied at birth). `--emit-session` requires `--founded`, because a manifest describes the production world
and `sim inspect` rebuilds with `founded: true`.

The chronicle is not written headlessly (`ChronicleCollector` has no CLI wiring) and the close record says
so, as an ABSENT companion with explicit nulls — not as a zero-byte file.

**Measured end to end:** `sim run --seed 42 --turns 8 --founded --size 256 --settlements 4 --emit-session`
produced run `e5ab2303caf35ac3`, and `sim inspect --manifest …` on that set reported
`reproduction VERIFIED: 9 turns, hash-for-hash`.

---

## 10. P3 — headless inspection, on the EXISTING verb

`sim inspect --answer TOPIC [--settlement ID]`. No parallel verb was added: `inspect` hosts it.

The `--answer` path **returns before the replay loop**. Every other path through `inspect` answers by
replaying the whole session; that is the wrong instrument for a forensic question, because what happened is
established by the evidence the session left behind, not by a fresh run that could differ from it. The
inspector is handed the FILES and nothing else — no executor, no world, no config — so it structurally
cannot step the simulation. Asserted by `ANSWERINGReadsTheSAVEDRecordAndDoesNOTReRunTheSimulation`, which
deletes the telemetry and shows that the telemetry-backed answers become a stated absence rather than a
fresh run.

Topics, all answered from the saved record: **world** (summary) · **settlements** (history) · **polities**
(history) · **movements** (aggregate in/out, with the pairwise limitation printed beside it every time) ·
**resources** (world-level split; per-settlement `StoreLosses` residual with its identity printed verbatim) ·
**happiness** (final value + branch labels) · **migration** (history) · **artisan** (latch turn AND first
presence, separately tagged) · **events** · **hashes** (pre/post) · **limits** · **all**.

Supporting this is **`TelemetryRecordFile` — the telemetry reader that did not exist anywhere in the
repository before this packet.** The artifact was write-only.

---

## 11. P3 — the reconstruction contract, as a type

`Evidence` is a **typed enum** in `Sim.Core/Observability/Forensic/ForensicSchema.cs`:
`Known` / `Derivable` / `NotRecorded`. It is a **field of the `Answer` record**, not a convention a caller
may forget, so nothing in this layer can print a value without printing how that value was obtained. The CLI
prints it first on every line: `[KNOWN] …`, `[DERIVABLE] …`, `[NOT RECORDED] …`, and closes with a count of
how many answers were NOT RECORDED and the sentence *"A NOT RECORDED answer is never promoted to
DERIVABLE."*

Worked examples of the discipline actually biting:

- **pre-turn state identity** is `DERIVABLE`, never `KNOWN`: only the POST-turn hash is recorded, so
  `pre(N) = post(N-1)` and the answer says so.
- **artisan activation** is `KNOWN` (the `ClassStateRow` latch, READ), while **artisan first presence** is a
  separate `DERIVABLE` answer over `classCounts`. Presence is not activation, and they are not merged.
- **per-settlement `StoreLosses`** is `DERIVABLE` and carries the identity it is a residual OF.
- **the food branch label** is `NOT RECORDED` when `deficitRatio == 0 && demandUnits == 0`, because the
  observer writes `0.0` for an ABSENT `ConsumptionDeficitRow` with no presence flag, so "no row" and "a row
  reading zero" are the same bytes. The housing branch label is `KNOWN`, because `housing.hasRow` exists.
  **The ambiguity is reported, not resolved** — resolving it is the S21 artifact-contract change, which was
  not approved.

---

## 12. P3 — routing the Explain layer headlessly

`sim inspect --explain KIND --settlement ID --turn N [--class C]`, where KIND is
`happiness | needs | migration | chain`.

Before this packet `Sim.Core/Observability/Explain/` — `CausalChain`, `HappinessExplanation`,
`MigrationExplanation`, the needs explanation, `Levers` — had **zero references from `Sim.Cli`**. The deepest
causal machinery in the tree could be seen only in a screenshot of the UI. This routes it and nothing more:
no new state, no new arithmetic, no new mechanics.

It is the ONE place that needs a world pair, and it says so on every answer: the explain layer is a pure
function of `(prev, next, cfg)` and this build persists no world, so the pair is **RECONSTRUCTED BY REPLAY**
and the output carries that sentence above the first line. Only the single requested `(prev, next)` pair is
retained, and only when `--explain` was asked for.

`ExplainPrinter` lives in `Sim.Core/Observability/Forensic/` for the read-isolation reason below.

---

## 13. THE MIGRATION INFORMATION BOUNDARY

The correct query result is carried as a **constant in the code**, `ForensicSchema.MigrationPairwiseAnswer`,
and is emitted verbatim:

> **UNAVAILABLE: pairwise migration destination was not persisted by this build.**

It is written into the forensic artifact's own limitation catalogue, returned by the `movements` topic
**every time the aggregate is returned**, returned again under `migration`, and printed at the foot of
`--explain migration`. The refusals are printed explicitly:

```
This layer does NOT infer destinations from attractiveness.
This layer does NOT rank destinations as historical fact.
This layer does NOT reconstruct the split probabilistically.
This layer does NOT add a synthetic category to stand in for it.
Aggregate inflow beside aggregate outflow is NOT proof of pairwise movement, and is not
presented as any.
```

`--explain migration` does print the candidate destinations with their pull, deficit, grain and happiness,
because those are the INPUTS the mechanism READ — and the output states in full that the list is the input
set in the explanation layer's own order, **not** a ranking of where anyone went, and must not be read as one.

The reason is recorded with it: `MigrationSystem` carries a single `MigrationRemainder` accumulator per
bucket row, mutated across the whole destination loop, so the amount sent to any one destination depends on
every destination considered before it; only the final remainder is serialized and it does not decompose. The
move itself is a `Ledger.Transfer`, which by its own contract records no flow entry. Closing it needs a new
authoritative serialized row — a schema change and a director ruling, not an observability change.

---

## 14. THE HAPPINESS BOUNDARY

**The proposal's copied per-factor calculator is NOT implemented.** What is exposed is exactly what genuinely
public functions give:

- `SettlementHappiness.Of` — the authoritative value. The telemetry record already stores this as a
  RECOMPUTED reading (the observer CALLS the public reader), and the test
  `HappinessInTheRecordISTheAuthoritativeValue_bitForBit_notACopyOfTheFormula` asserts
  `Of(world, settlement, cfg) == recorded` with **exact equality, no tolerance**, over every settlement of
  every turn of a 24-turn run.
- `SettlementHappiness.Factors` — the two factor values, also compared exactly.
- `SettlementHappiness.IsRevoltReady` and `RevoltThreshold`.
- The **branch labels that follow from row presence**, and only those (§11).

**The architectural boundary, reported as the limitation, with file:line so a reviewer can check it rather
than believe it** — `Sim.Core/State/SettlementHappiness.cs`:

| member | visibility | line |
|---|---|---|
| `WeightOf(SimConfig, int, double)` | **`private static`** | **:234** |
| `SustenanceNeedId` | **`private const int`** | **:226** |
| `ShelterNeedId` | **`private const int`** | **:227** |
| the floor, the span and the normalisation | **inline locals inside `Of`** | **:169-219** |

Consequently **raw factor, normalised factor, per-factor weight, per-factor contribution and the aggregate
are NOT DERIVABLE from any public function on stored state**, and this layer does not manufacture them. The
answer `happiness decomposition` is tagged `NOT RECORDED` and carries the table above.

*"Why was happiness 100?"* is therefore answered exactly as far as the simulation exposes it — the value, the
two factors, the branch labels — and no further.

---

## 15. The other limitations carried in the artifact

`ForensicSession.Limitations()` writes five, each with a token, what it is, why, and (where one exists) the
literal answer any such query must return. `sim inspect --answer limits` prints them; they are a reviewer's
first command.

| token | substance |
|---|---|
| `migration-destination-not-persisted` | §13 |
| `happiness-decomposition-not-public` | §14 |
| `store-losses-unsplit` | one residual absorbing spoilage + granary overflow + appropriation + colony provisions. `ConsumptionSystem.BoundStore` is `private static` (:285) and its `annualGrainDemand` argument is assembled at the call site (:200-203) from transient locals, so no public function can be called with it. **The granary extraction was refused, so this stays a stated limitation.** World level IS exact, by ledger reason |
| `no-ai-at-m4` | no system emits an order; `aiEmpires` is 0; the sole polity is `CommandSource.Player`. No AI decision exists to record — different from one that went unrecorded |
| `no-gate-verdict-producer` | nothing in any run artifact references a test run, a gate, a CI run id or a golden, and no producer exists. The `gates` block is empty and `gatesState` says so. It is never populated with a claim nobody measured |

---

## 16. Tests

**`Sim.Ui.Tests/TelemetryAppendTests.cs` — 5** (§4). **`Sim.Tests/Forensic/` — 29**, in three files:

`ForensicIdentityTests.cs` (13): run id content-derived and clock-independent · run id moves with seed,
overrides and order log · run id recomputable from its stated inputs · every embedded config resource by
content in a fixed order · pipeline order and era table recorded · AI count emitted explicitly as zero ·
hash named and stated as not self-identifying its schema, with `CanonicalSchema.Version == 24` asserted ·
nulls explicit, no NaN strings, no `-1` sentinels, every nullable paired with a state · limitation catalogue
present with the migration answer verbatim · round-trip through the reader · a missing close line is readable
and is itself the evidence · an unknown vintage is refused loudly while an unknown key is tolerated · the
manifest gains the reference without moving its tag (both directions).

`SessionEmitterTests.cs` (5): **every run gets a valid manifest** that names the forensic record and whose
whole set is on disk · the close record binds every companion by content and says ABSENT with explicit nulls
for the one that is not there · the final hash on the close record IS the trace's own hash for that turn ·
**two identical headless runs produce the same identity and byte-identical artifacts** · telemetry is one
line per turn and the trace carries turn 0.

`InspectionTests.cs` (11): every answer carries a tag and NOT RECORDED is never promoted · **happiness is the
authoritative value bit for bit** · every recorded migration corresponds to an actual aggregate state
transition (and the world total equals the sum of the per-settlement inflows, exactly, every turn) · every
recorded artisan activation corresponds to the latch row **and the latch was not already set the turn
before** · food and population totals reconcile with the authoritative fields as EXACT long equalities ·
events reference valid entities, no event for a transition that did not happen, and the categories this build
has no mechanism for (forced displacement, war, battle) never appear · orders attributable to their actor and
to its `CommandSource` via the public `EmpireQuery`, with the turn-exact delivery rule pinned (an order
stamped turn 2 lands on the step from turn 2, i.e. the record for turn 3) · stable ids stable · every turn has
a pre and post identity with the pre tagged DERIVABLE · answering reads the saved record and does not re-run ·
the happiness decomposition answer reports the architectural boundary rather than an explanation.

---

## 17. Validation

**Build:** Release, 0 warnings, 0 errors.

**Gates, all three, run on this tree:**

```
scripts/check-banned-constructs.sh   OK
scripts/check-read-isolation.sh      OK
scripts/check-readonly-proof.sh      OK
```

The read-isolation gate matters here and was designed for: it scans `Sim.Cli` and matches PROSE as well as
code. The telemetry reader, the query surface and the explain printer therefore all live under
`Sim.Core/Observability/Forensic/`, which the gate allowlists by path prefix, and `Sim.Cli` stays a thin
flag-parsing shell naming none of the four tokens in code or comment.

**Suites:**

| suite | baseline | this branch |
|---|---|---|
| `Sim.Tests` | 724 passed / **2 failed** / 6 skipped | **753 passed / 2 failed / 6 skipped** |
| `Sim.Ui.Tests` | 289 / 289 | **294 / 294** |

The two reds are the SAME two as the baseline — `Dev_MalthusCorridors_AllInBand(seed: 42)` and
`(seed: 7)` — and are pre-existing. No golden, determinism, replay, save/load or schema test failed.

**The 650-turn comparison, candidate `2807155` vs this tree.** Identical harness compiled against each
worktree, same seed (42), same overrides (size 256, 4 settlements), same fixed `startedAt` string:

| check | result |
|---|---|
| per-turn world hashes, all 650 | **IDENTICAL**, final `bc973a8897a9be14…` on both |
| `orders-….bin` | **BYTE-IDENTICAL** |
| `trace-….csv` | **BYTE-IDENTICAL** |
| `chronicle-….txt` | **BYTE-IDENTICAL** |
| `telemetry-….jsonl` | **BYTE-IDENTICAL** |
| `session-….json` (manifest) | **DIFFERS BY EXACTLY ONE ADDED KEY: `"forensicFile"`.** The `"schema"` value is unchanged at `session-manifest/v2` and no other line differs |
| the new `forensic-….jsonl` | present only on this branch — the only new artifact |

UI behaviour is unchanged: no gameplay UI file was touched, and the only `Sim.Ui` changes are
`UiSession.ExportTelemetry`/`FinalizeTelemetry`/the forensic writers, the `SaveSession` and `OnExiting` call
sites, and one launch-time line in `Program.cs`.

---

## 18. Class A blockers, and what was deliberately not done

**No Class A blocker was hit.** Every approved requirement was met without needing a refused item, with one
requirement met in a reduced form that is stated rather than worked around:

- **"a happiness forensic that explains the value"** is delivered only to the degree the simulation exposes
  it (§14). This is not a blocker but a boundary: the decomposition is reported as `NOT RECORDED` with the
  private members named at file:line, exactly as instructed, and no explanation was manufactured.

Deliberately NOT done, each because it was refused or out of scope:

- **P2 (the per-turn `turn`/`site`/`place`/`move`/`decide`/`order`/`event` record stream) was not approved
  and is not implemented.** P3's answers therefore come from the EXISTING `telemetry/v2` record plus the
  trace, the manifest and the new `run`/`close` records — which is why the telemetry reader had to be built.
- The G5 finding (`Observer.Migration` reading `prev.SmoothedAttractiveness` where the mechanism used the
  updated EMA) is **NOT applied**: per ADR-015 §6 it is a finding awaiting a verdict, not an authorised fix,
  and it touches a file this fence does not open.
- The G17 positional trace↔replay join and the G26 undecoded order target in the legacy `inspect` print path
  are untouched; each is its own packet. (The new `--answer` and `--explain` paths carry the decoded
  settlement and sector.)
- `docs/observability-architecture.md` was **not edited**: it is a T4.19 contract document, and rewriting a
  frozen document needs a ruling. `docs/session-records.md` still says "four files"; correcting it is a
  documentation packet, noted here rather than done.

**NOT MERGED. `main` untouched. `m5-full-build` untouched. Nothing pushed to `main`.**
