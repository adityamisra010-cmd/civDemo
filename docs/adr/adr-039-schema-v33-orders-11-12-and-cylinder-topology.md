# ADR-039 — SCHEMA v33, ORDER KINDS 11 AND 12, THE SNAPSHOT TOPOLOGY TRAILER, THE v32 MIGRATION AND THE EAST-WEST TOPOLOGY SEAM

**Status: PROPOSED — uncommitted, for the Director's review with D-054.**
- Written 2026-10-15 for the batch-3 change on top of the uncommitted batch-2 tree of `m5w-integration` (base
  `de86ace`). Not committed, pushed, merged or packaged.
- **Governing record:** `docs/d054-batch3-incremental-construction-allocation-cylinder.md` (D-054). D-054 holds the
  decisions, the supersessions, the measurements and the Director's questions. This ADR is the technical contract.
- **Touches the kernel contract.** The constitution requires an ADR for this, and the kernel is frozen without Director
  sign-off, which this record asks for.
  - `CanonicalSchema.Version` 32 → **33**, plus `OldestReadableVersion` = 32.
  - Two `OrderKind` values: **11** and **12**.
  - The **snapshot file layout** gains a trailer.
  - **The first explicit save migration** (v32 → v33).
  - Kernel files: `CanonicalSchema.cs`, `OrderLog.cs`, `OrderValidation.cs`, `Snapshot.cs`, and the new
    `TopologyFlags.cs`. Fix round 1 added to `TopologyFlags.cs` the constant `FirstCylinderSchema` = 33 and the pure
    function `RecordedFlat(v)` = `v < FirstCylinderSchema`: the topology a RECORD of schema v implies (§7).
  - The order log's wire format (`OrderRecord`, `IoVersion` 1) is UNCHANGED.
- **Corrected 2026-10-15 for `b3-final-r3.patch`** (sha256 `855a1402…`; first written against the
  combine stage's `5ab74d3e…`): §4 (the CLI loader and its trailer refusal), §5 (what binds the flag), §6 (the size
  refusal's wording), §7 (old sessions, `sim inspect`), §11 (tests and mutants). The contract in §2, §3, §8, §9 and §10
  did not change. Corrected again the same day by final-round fix 1 (D-054's header): §7 (the pre-fix batch-2
  sessions carried orders) and §11 (the battery's part-built save point is world 8, and the quantizing mutant is no
  longer equivalent). The contract did not change. Corrected again the same day by final-round fix 3 (D-054's record
  history): §4 (the trailer refusal's remedy wording) and §6 (when the size refusal adds its founding sentence). The
  contract did not change.

## 1. What exists now

| Layer | New state / contract | Owner | Switch (shipped ON) | Attribution control |
|---|---|---|---|---|
| WS1 incremental construction | `ConstructionProgressRow` (section 59); kind 11 `CancelConstruction` | ConstructionSystem | `sim.json` `construction.incremental` | `TestConfigs.PreIncrementalConstruction` (also turns WS2 off) |
| WS2 building/roads split | `ResourceAllocationRow` (section 60); kind 12 `SetResourceAllocation` | ConstructionSystem | `sim.json` `construction.allocationRatios` (loader-refused unless `incremental` and `empireStockpile`) | `TestConfigs.PreAllocationRatios` |
| WS3 cylinder | `TerrainSet.WrapX`; the snapshot topology trailer; `Pathing/MapTopology.cs` | Worldgen (the only reader of the config field) | `worldgen.json` `topology.wrapX` | `TestConfigs.PreCylinderWorldgen` |

**The batch off is a PAIR** (§8): `PreD054Sim` (WS1 and WS2) and `PreD054Worldgen` (WS3).

## 2. Schema v33 — two appended tables

Both tables are appended after section 58 (TaxGrievances). No existing row changed width: `ConstructionQueueRow` keeps
its 12 bytes, and its call sites are untouched. Each table is a 4-byte int count prefix followed by its rows, written field
by field (ADR-005).

| Section | Table | Row (field: width) | Row bytes |
|---|---|---|---|
| 59 | `ConstructionProgress` | `SettlementId Settlement`: 4 · `int Slot`: 4 · `int ProjectId`: 4 · `long EnqueuedTurn`: 8 · `long Progress`: 8 | **28** |
| 60 | `ResourceAllocations` | `PolityId Polity`: 4 · `int BuildingsPercent`: 4 | **8** |

- **ExpectedLength** gains `4 + 28·n59 + 4 + 8·n60`. A world with neither table populated carries two empty count
  prefixes (8 zero bytes) and nothing else.
  - The combine stage measured this on six shipped turn-300 worlds: the v33 stream is the v32 stream plus 8 zero bytes.
- **`ConstructionProgress` semantics.**
  - Progress p is in [0, N − 1] while queued, with N = `ConstructionQuery.ProgressUnits` = 1,000,000. Reaching N
    completes the entry, and its rows are removed in the same step.
  - `EnqueuedTurn` is PREV's clock in the step that accepted the order. It is the building order's FIFO key.
  - Not a conserved stock: it measures work, not goods or people.
- **`ResourceAllocations` semantics.**
  - One row per Empire. `BuildingsPercent` is in [0, 100]; roads get the rest.
  - ABSENCE = 100 (building first, the batch-2 behaviour).
- **The invariant**, one statement: `ConstructionQuery.ProgressInvariantViolation` (plus
  `ResourceAllocation.InvariantViolation`).
  - Queue slots are unique per settlement.
  - Every progress row has a queue row with the same (Settlement, Slot, ProjectId), at most one per (Settlement, Slot),
    with Progress in [0, N − 1].
  - The converse is optional: a queue entry with no progress row reads p = 0 and EnqueuedTurn = −1.
  - There is one split row per Empire, with a share in [0, 100].
  - ConstructionSystem asserts this at the end of every step (`InvalidOperationException`). `Snapshot.Load` refuses a
    save that breaks it (`SnapshotFormatException`, "save is corrupt: … (schema v33 invariant)").
- **Row order.** Progress rows are appended at enqueue in intake order (or at an absent-row entry's first progress),
  updated in place, and removed preserving the relative order of the rest. Split rows are upserted in place.
- **The checklist was carried out:**
  - Write, Read(version) and ExpectedLength;
  - WorldState (row, interface, concrete, initialiser, Clone);
  - the `NetworkOverlayView` forward;
  - the `SystemCatalog` hand-out;
  - `StateEquals`;
  - the `SnapshotDiff` layout and its version dispatch;
  - every `Assert.Equal(32, …)` → 33;
  - the tail-offset tests;
  - `SchemaProjection.V32` / `HashAtSchemaV32`, with the v33 strip (now asserting it removes 0 rows) in every older
    projection helper;
  - the header docs of `CanonicalSchema` and `Snapshot`.
- **Populated-table proof.** `ConstructionSchemaV33Tests`, 21 cases. Both tables are populated, rows are out of key
  order, `int.MaxValue` keys are used and EnqueuedTurn is negative. Each case checks the exact ExpectedLength, a bit-exact
  round trip, hash equality and a byte-identical re-save.

## 3. Order kinds 11 and 12

**Kind 10 stays RESERVED** for MoveFormation (M7; D-052 §4). It is never assigned, and the unknown-kind message says so.

**Kind 11 `CancelConstruction` (WS1).** Payload: TargetId = the settlement id; Amount = the queue SLOT, a whole number.
- **Load validation** (`OrderLog.ValidateRecord`): TargetId ≥ 0, and Amount satisfies `ConstructionSlotValid`: finite,
  whole, in [0, int.MaxValue]. NaN and ±∞ fail. Otherwise `SnapshotFormatException`.
- **World validation** (`OrderValidation`): the EnqueueConstruction rule. When the world has Controls, the issuer must
  control the settlement.
- **Delivery-time validation (mandatory)** in `ConstructionQuery.CancelOrderValid`, on PREV and this step's (NEXT)
  queue:
  - a roster actor (when the world has Polities);
  - control of the settlement on PREV (when it has Controls);
  - the payload re-checked;
  - the slot present.

  A failing order changes nothing: it is never floored and never clamped. Load and world validation see only the
  turn-0 world and only loaded logs, so they cannot be the rule's implementation (`ConstructionSystem.cs`, "WHY CANCELS
  AND ENQUEUES ARE CHECKED HERE").
- **Effect.** It removes the queue row AND its progress row from NEXT. **No refund.** It is applied in ConstructionSystem
  step 3, after the kind-12 upserts and before the kind-4 enqueues and any building work.
  - DELIVERY: an order stamped t removes the entry in the step t → t+1. The state of turn t+1 no longer holds it.
  - It is honoured with `construction.incremental` off too (D-054 §1 deviation).
- `TurnRecord.OrderApplied.Settlement` maps kind 11 to its TargetId.

**Kind 12 `SetResourceAllocation` (WS2).** Payload: TargetId = the issuing Empire's own PolityId (the SetTaxRate
convention); Amount = BuildingsPercent.
- **Load validation:** TargetId ≥ 0, and Amount satisfies `AllocationPercentValid`: a whole number in [0, 100], NaN
  fails.
- **World validation:** TargetId == ActorId.
- **Delivery validation** in `ConstructionQuery.AllocationOrderValid`: the switch is on; TargetId == ActorId ≥ 0; the
  payload is re-checked; the actor is a roster Empire on PREV.
- **Effect.** It upserts the Empire's row in NEXT at ConstructionSystem step 2, in log order, so the last valid order of
  an Empire in a step wins.
  - DELIVERY: an order stamped t governs the split of the step t → t+1 itself (the SetResearchTarget precedent; pinned
    turn-exact).

**One shared predicate per kind**, used by both ConstructionSystem and the UI emitters (`UiSession.EmitCancelConstruction`,
`EmitAllocationOrder`; ADR-033 D2).

**CI.** `scripts/gen-sample-orders.py --founded-mix` issues:
- three granaries at turn 10;
- a kind-11 cancel of non-head slot 2 at turn 11;
- a cancel of a non-existent slot 7 at turn 12;
- kind 12 at 30 (turn 40) and 70 (turn 200).

ci.yml's kind check is EXACT: the run log's kinds must equal `set(range(3, 10)) | {11, 12}`.

## 4. The snapshot file layout and the topology trailer

```
header { magic "CIVSNAP\0" (8), int32 version, uint64 seed, int64 turn }   = 28 bytes
canonical state stream (sections 1..60)                                    = ExpectedLength
int32 topology                                                              = 4 bytes   (v33 only)
```

- **The trailer.** `TopologyFlags`: bit 0 = `WrapX` (the world is an east-west cylinder); every other bit is reserved.
  It is written as `TopologyFlags.Of(world)`: 1 when the world's terrain has `WrapX`, 0 otherwise, and always 0 for a
  terrain-less world. `TopologyFlags.TerrainWrapsX` is the one reader of a TerrainSet's topology here.
- **The trailer is OUTSIDE the canonical stream.** The world hash (SHA-256 of the stream) and `sim hash` never see it.
- **`Snapshot.Load` refuses:**
  - a missing or truncated trailer on a v33 file ("save is truncated: the schema-v33 topology trailer … is missing");
  - unknown bits ("save carries unknown topology bits …");
  - bit 0 set on a save made without terrain ("save is corrupt: it records an east-west wrapping world but was made
    without terrain");
  - **a disagreement with the supplied TerrainSet**, checked after the content-hash check. A wrapped save loaded onto
    flat terrain reads "topology mismatch: the save was made on an east-west WRAPPING world but the provided terrain is
    flat". A flat save, including every v32 save, loaded onto wrapped terrain reads "topology mismatch: the save was made
    on a FLAT world (every schema-v32 save is flat) but the provided terrain wraps east-west". Both continue "— regenerate
    the TerrainSet with the matching worldgen topology (worldgen.json topology.wrapX) and pass it to Snapshot.Load."
- **Proof.** Every refusal, a cylinder save writing 1, a flat save writing 0, and a populated round trip on a wrapped
  world are pinned in `ConstructionSchemaV33Tests`. A wrapped round trip's file length is 28 + ExpectedLength + 4. The
  merge stage's semantic mutant (the flag reverted to constant false) fails two of them.
- **The CLI loader** (`Sim.Cli/SaveFiles.cs`, used by `sim hash` and `sim diff`; the CLI prints a refusal as
  "error: " + message and exits 2).
  - **It regenerates a v33 save with the topology its trailer names** (fix round 1, verifier F2):
    `SaveFiles.WorldgenFor(version, shipped, size, trailer)`. Before, every v33 save was regenerated as the shipped
    cylinder, so a FLAT v33 save (for example an attribution-control build's) was refused as "terrain mismatch: …".
    Pinned by `CliSaveAndSizeMessagesTests.AFlatV33Save_IsRegeneratedWithTheTopologyItsTrailerNames_AndSimHashPrintsItsHash`.
  - **Consequence:** `Snapshot.Load`'s own "topology mismatch: the save was made on …" branch above is unreachable from
    CLI v33 loads, because the terrain always matches the trailer. It still guards API callers that pass their own
    TerrainSet.
  - **A flipped wrap bit** (the nits round, verifier RC1-2). Fix round 1's report said a corrupt tail was "still refused
    with the same messages"; that was false for a flipped bit 0, which was measured printing "error: terrain mismatch:
    the provided TerrainSet's content hash does not equal the hash this save was made against — wrong seed or worldgen
    config." with rc 2. Now, when `Snapshot.Load` refuses a v33 save whose trailer has only known bits,
    `SaveFiles.LoadsUnderTheOtherTopology` flips bit 0 back, regenerates the terrain under that other topology and loads
    again. ONLY if that load succeeds is the refusal replaced, by `SaveFiles.TrailerTopologyMismatch`:
    - a cylinder save whose bit 0 was cleared: "topology mismatch: the save's topology trailer (its last 4 bytes) has
      bit 0 (east-west wrap) CLEAR, naming a FLAT world, but the save was made on an east-west WRAPPING world — with
      bit 0 set it loads intact against that terrain. The trailer is corrupt, so the save is refused; set bit 0 again
      to load it, or save the world again from the session that made it."
    - a flat v33 save whose bit 0 was set: "topology mismatch: the save's topology trailer (its last 4 bytes) has bit 0
      (east-west wrap) SET, naming an east-west WRAPPING world, but the save was made on a FLAT world — with bit 0
      clear it loads intact against that terrain. The trailer is corrupt, so the save is refused; clear bit 0 again to
      load it, or save the world again from the session that made it."
    - Final-round fix 3 (verifier RC1-2-REMEDY-WORDING, 2026-10-15) reworded the remedy. It read "… set bit 0 again, or
      save the world again, to load it." (and "clear" for the second), which offered the `sim hash` / `sim diff` user,
      who holds only the file, a remedy that needs the session that made it. Both texts are pinned whole by
      `CliSaveAndSizeMessagesTests.SimHash_OfASaveWhoseTrailerBitWasFlipped_IsRefusedAsATopologyMismatch_NamingTheBit`.
  - Every other refusal keeps its own message, unchanged: a wrong `--size` or seed ("terrain mismatch: …"), a truncated
    file, unknown bits, a flipped trailer combined with a wrong size, and a size the other topology refuses. Nothing
    loads that was refused before; the recheck is diagnostic only, and it treats any exception of its re-load as "not
    explained" (D-054 §9).
  - Pinned by `SimHash_OfASaveWhoseTrailerBitWasFlipped_IsRefusedAsATopologyMismatch_NamingTheBit` (a flat and a
    cylinder save, through `SaveFiles.Load` and the real `sim hash` process) and
    `ASaveRefusedForAnotherReason_KeepsItsOwnRefusal_UnderTheTrailerRecheck`. The mutants "the recheck never fires" and
    "the recheck always fires" are both killed.

## 5. `TerrainSet.WrapX` is not in the content hash; the world hash ignores the flag

`TerrainSet.WrapX` is set by `Worldgen.Generate` from the config. It is **NOT folded into `TerrainSet.ContentHash`**,
which covers the rasters only, so **the world hash does not see the flag either**.
- **Consequence:** two worlds whose terrain rasters are identical hash equal whether their TerrainSets wrap or not.
  - For a GENERATED world this cannot happen in practice. A wrapped world's rasters differ from the flat ones at every
    seed (the y-only taper, the periodic noise, the wrapped hydrology), so its content hash and every world hash differ
    from turn 0.
  - It CAN happen for a hand-built `TerrainSet.FromLayers(…, wrapX)` rig with the same layers both ways.
- **What binds the flag instead:**
  - in a save, the trailer (§4);
  - in a live session, the TerrainSet it holds;
  - in an order log or a UI session manifest, nothing directly. A manifest records its schema, and
    `TopologyFlags.RecordedFlat(schema)` makes every pre-v33 record flat (§7). A v33 session is rebuilt with the reading
    build's `worldgen.json`, so a session founded with `UiSession.Start`'s `wrapXOverride` is not resumable from its
    log (D-054 §10 Q9).
- `TraversalLattice.WrapX` is copied from the terrain at `Build`. `TraversalLattice.FromCosts(…, wrapX = false)` keeps
  its flat default for hand-built lattices.

## 6. The topology seam: `Sim.Core/Pathing/MapTopology.cs`

- **The API.**
  - **Integer first:** `NeighbourX(x, dx, size, wrap)` (−1 off-map when flat), `WrapX(x, size)`, and
    `Dx(x1, x2, size, wrap)`.
  - `Dx` is the signed short-way delta. It is computed as `d = ((x2 − x1) % size + size) % size; if (d > size / 2) d -=
    size`. An exact tie on an even size resolves to +size/2, so Dx(a, b) ≠ −Dx(b, a) at the tie; this is documented and
    tie-dense tested.
  - Also `AbsDx`, `SquaredPx(cellA, cellB, size, wrap)` and `Km(…)`.
  - **Doubles for continuous positions and the camera:** `WrapCoord`, `NearestCopy` and `UnwrapNear`.
  - **Lattice geometry:** `BlockPx`, `NodeCentreCell`, `LatticeGrid` and `NodeOfCell`.
  - A wrapped axis needs at least 3 columns (`RequireWrapSize`).
  - With `wrap` false every function returns exactly the flat arithmetic it replaced.
- **What routes through it.**
  - Every x delta, x neighbour and x normalisation in Sim.Core and Sim.Ui goes through MapTopology. The only exceptions
    are `TraversalLattice` (its own id↔coordinates) and `Worldgen` (it builds the rasters before any TerrainSet exists,
    and its neighbours go through `NeighbourX`).
  - The sim sites: the Pathfinder (the A* heuristic stays admissible on the wrapped dx), `LatticeMap.OriginLatticeNode`
    (a wrapped Chebyshev ring scan, sorted explicitly by (ring, node id)), PathBuild's passable component,
    `PolityKnowledge`, `GeographicDistance`, `RoadDevelopmentQuery.SquaredPx`, `SiteAssessment`, siting, founding, and
    a formation's X.
- **The source guard** `MapTopologyGuardTests`.
  - It fails when a Sim.Core or Sim.Ui file outside the chokepoint writes one of nine idioms. These include two
    structural ones: every column extraction (`% size`) and every x bound against a size.
  - Its allowlist is per line, with an occurrence count and a reason, so a copy of an allowed line still fails, and so
    does a stale entry.
  - It also enforces the CONFIG rule: only `Worldgen.Generate` (and the loader that validates it) reads
    `WorldgenConfig.Topology`. Everything else reads `TerrainSet.WrapX`, so no system can disagree with the terrain it
    steps.
  - A future movement system (M7, kind 10) cannot bypass the topology without failing it.
- **Worldgen's refusals** (`WorldgenConfigException`, actionable):
  - wrapX with `size % TraversalLattice.DefaultStride != 0` or a lattice narrower than 3 nodes. The message states the
    cylinder's own rule (a multiple of 4 px, at least 12 px) and names the nearest sizes the cylinder takes. Only when a
    size it names is below 124 px (the size founding was measured to succeed from at the default settlement count) does
    it add that settlement siting can refuse maps this small, with its own message; it names no founding size
    (final-round fix 3, verifier RC1-4-FLOOR-NOT-NAMED). D-054 §3 quotes it in full.
  - a non-integer noise period. The message names the octave.

## 7. The v32 → v33 migration (the first explicit one; superseding ADR-005's "no migration" for this step)

- **`Snapshot.Load` accepts version 33, and version 32 (`OldestReadableVersion`).**
  - A v32 file is the header plus a stream ending after section 58, with NO trailer.
  - `CanonicalSchema.Read(reader, 32, …)` stops after section 58 and leaves both new tables empty. This is exact: under
    v32 no queue entry had partial progress (M4-D) and no Empire had a split.
  - Migrated queue entries read p = 0 and EnqueuedTurn −1, and are served first in the building order, by settlement,
    then slot.
  - The save loads as a FLAT world (topology 0), so the caller must supply a flat TerrainSet.
  - **Every other version is refused** with the D-008 message: seed plus order-log replay remains the recovery path.
- **The fixture.** `Sim.Tests/Fixtures/v32-seed42-turn40.civsnap` is a real v32 save written by the batch-2 build, with a
  NON-EMPTY queue: two granaries at settlement 0. Its command and order log are in `README-v32-fixture.md`.
  - The v33 build loads it, and `SchemaProjection.HashAtSchemaV32(loaded)` equals the v32 build's reported hash,
    `b000b2ee26cc02310e6e379f9b7c4bad2b9568e41e6080be85056b3ebdb92361`.
  - `SnapshotDiff` dispatches on the header version (`LayoutFor(32 | 33)`) and walks both.
- **The CLI** (`Sim.Cli/SaveFiles.cs`, the one snapshot loader, used by `hash` and `diff`).
  - `SaveFiles.WorldgenFor(version, …)` regenerates a save of a schema with `TopologyFlags.RecordedFlat` (v32) with the
    topology switch off. It SETS the config field, which the source guard's config rule allows. A v33 save is
    regenerated with the topology its trailer names (§4).
  - Before the fix, `sim hash` of the fixture failed: "error: terrain mismatch …", rc 2.
  - After it: `3eb392033251bba411760f1090a7fee6f77f82ca25b0f566f7382ca67889461e`, rc 0. This is the v33 hash of the
    migrated world; the v32 build printed `b000b2ee…` for the same world without the two empty tables.
  - `sim hash` and `sim diff` of a v32 save print a one-line `SaveFiles.MigrationNote` on STDERR ("note: … is a
    schema-v32 save, migrated to v33 on load (D-054): its terrain regenerated flat …"). Stdout stays exactly the hash or
    the diff a script parses.
- **UI sessions are not migrated; they are rebuilt.** A UI session is a manifest plus an order log, with no snapshot. A
  session whose manifest records another schema takes the existing D-008 schema-mismatch path: re-founded from its seed
  and replayed from its orders without the trace check, with the ADR-034 order checks and the resume notice.
  - **Its topology (fix round 1, verifier F1).** `UiSession.Resume` founds a session whose schema is
    `TopologyFlags.RecordedFlat` (every pre-v33 session) FLAT (`wrapXOverride: false`), on the map it was played on;
    any other session with the shipped worldgen. Before fix round 1 every session was re-founded on the shipped
    cylinder: a batch-2 session with FoundSettlement orders was refused (exit 2), one without FoundSettlement orders
    (its log held only sector-allocation orders, plus, at 256 px, one AI research order) resumed onto a different map,
    and one at 250 px was refused by the size rule.
  - **The notice** keeps four lines (ADR-034 §4). For a `RecordedFlat` schema line 2 reads "Replayed from its seed and
    orders on the flat map v32 used (D-008)."; for any other schema it stays "Resumed by replaying its seed and orders
    (D-008)."
  - **Proof on a real batch-2 record.** `Sim.Tests/Fixtures/v32-session-found/` (manifest, orders, trace; README with
    the command and sha256s) was written by the batch-2 build: seed 42, two FoundSettlement orders legal only on the
    flat world, reported hash `6157dfde…` at turn 8. `Batch2SessionResumeTests` (Sim.Ui.Tests, which links the
    fixture): the resume reaches turn 8 with WrapX false and 14 settlements, and its v32 projection equals `6157dfde…`;
    the same log on the shipped cylinder is refused at the second FoundSettlement; a 250-px session stamped v32 resumes
    flat. The simulation rules on replay are this build's (WS1 and WS2 on), so a batch-2 session where construction
    waited may still differ (D-054 §4, §10 Q9).
- **`sim inspect` of a session.** It rebuilds with the same rule as `UiSession.Resume` (a `wrapXOverride` on
  `HeadlessFounding.Found` / `StartWorld`, which no command line sets). One condition,
  `SessionSchemaNotice.HashesChecked(schema)` = `schema == CanonicalSchema.Version`, drives the platform block
  (`PlatformNotice.For(manifestPlatform, runningPlatform, hashesChecked)`, a new optional parameter defaulting to true),
  the recipe line (`SessionSchemaNotice.Reproduce(manifest, manifestFile, turns)`) and the verdict.
  - **Under this build's schema, with at least one traced turn,** nothing changed: the full platform block,
    "  reproduce with: sim replay …", and
    `Divergence` (for example "reproduction VERIFIED: 301 turns, hash-for-hash.").
  - **Under another schema** no hash is compared. The verdict is `SessionSchemaNotice.NotChecked`: "trace not checked:
    the session was recorded under save schema v<N> and this build is v33, so no replayed hash can equal a recorded one
    (D-008). Replayed from its seed and orders on the flat map v<N> used; the rows below are this replay's." (the clause
    "on the flat map v<N> used" only for a `RecordedFlat` schema). Only the plain platform line prints: "  platform
    played on <rid> (…); inspecting on <rid> (…); reference is linux-x64 (ADR-022)".
  - **The recipe for a pre-v33 session** (fix round 2, verifier RC1-1), three lines:
    "  reproduce with: sim inspect --manifest <manifest file> (it rebuilds a v32 session on the flat map v32 used)",
    "          not with this build's sim replay: it founds the cylinder this build ships (schema v33), not that flat
    map." and "          The build that recorded the session (<buildSha>, <buildDate>) replays it with: <sim replay
    recipe>". Fix round 2 ran that recipe on the batch-2 build: exit 0 with the traces' final hashes (`6157dfde…` for
    the fixture, `2786a4b7…` for a 250-px session). On fix round 1's tree, whose `sim replay` is this build's, it exited 2
    (the FoundSettlement refusal, and the cylinder's size refusal at 250 px).
  - **The recipe for a session a NEWER schema recorded** (the nits round, verifier RC2-1), two lines:
    "  reproduce with: the build that recorded the session (<buildSha>, <buildDate>): <sim replay recipe>" and
    "          not with this build's sim replay: this build is schema v33 and the session was recorded under v<N>, so no
    replay here reproduces its hashes (D-008)." Before, such a session got this build's `sim replay` recipe above "trace
    not checked", and the verifier's mutant `hashesChecked = !RecordedFlat(…)` survived; both are fixed and the mutant
    is killed.
  - **An empty trace** (the nits round, verifier RC2-2), under ANY schema including this build's own: the header, the
    plain platform line, a blank line and "no turns recorded in <trace file> — nothing to inspect." No recipe is printed.
    Before, a v33 empty session also printed the full platform block and a `--turns 0` recipe.
  - **`sim replay` has no flat path.** It always founds the shipped cylinder, so it cannot rebuild a pre-v33 log; that
    is why the pre-v33 recipe names `sim inspect`. A `--manifest`-driven flat override was declined as scope widening
    (D-054 §10 Q9).
  - Pinned in `CliSaveAndSizeMessagesTests` (`SimInspect_OfABatch2Session…`, `SimInspect_OfASessionThisBuildRecorded…`,
    `SimInspect_OfASessionANewerSchemaRecorded…`, `SimInspect_OfAnEmptyTrace…`, `TheReproduceLine_…` ×2,
    `ARecordedSchemaBeforeV33_IsFlat…`) and `PlatformNoticeTests.WhenNoHashIsCompared_OnlyThePlainPlatformLinePrints_InEveryCase`.

## 8. Attribution switches and the PreD054 pair

- **The SimConfig half: `TestConfigs.PreD054Sim(cfg)`** = `PreAllocationRatios(PreIncrementalConstruction(cfg))`.
  - Every older SimConfig twin strips it by the R4 rule (an older layer twin strips every newer layer): `PreD053`, and
    through it the `PreForager` chain, `PreTimeAveragedHarvest`, `PreTaxAge` and `PreAiFounding`.
- **The worldgen half: `TestConfigs.PreD054Worldgen(wg)`** = `PreCylinderWorldgen(wg)` = `wg with { Topology = null }`.
  - WS3's switch lives in `worldgen.json`, so no SimConfig twin can strip it. A control that FOUNDS a world must also
    found it on this half.
- **`TestConfigs.FoundPreD054(seed, sim, worldgen?, settlements?)`** founds on both halves.
- **The reproduction.** With both halves off and `SchemaProjection.V32`, every batch-2 reference reproduces byte for
  byte (`D054PairAttributionTests`, 5 cases, `[Trait suite=determinism]`): founded `937577ff…`, AI `b2e4f333…`, driven
  `4f4e0b81…`, toy `5994e978…` and first-reign `467f2cb6…`.
- **`TheShippedContent_SwitchesAllThreeBatch3LayersOn`** reads the raw `sim.json` and `worldgen.json` and pins all three
  switches ON. It also checks that the pair strips exactly those.
- **Single-layer controls:**
  - `IncrementalConstructionAttributionTests` (the SimConfig half; its worlds are founded flat);
  - `AllocationRatiosAttributionTests`;
  - `WrapAttributionTests` (the worldgen half; batch-2 literals compared through V32).
- **Live cylinder-alone links** are pinned beside the moved goldens: `FoundedGoldenHashAtV32`, `GoldenAtV32`,
  `PostR1GoldenAtV32` and `S1cTurn300AtV32` (D-054 §7).

## 9. `TerrainSet.FromLayers` (recorded under ADR-008)

`TerrainSet.FromLayers(size, kmPerPx, seaLevel, elevation, water, temperature, moisture, fertility, movementCost,
rivers, riverPolylines, wrapX)` is a PUBLIC test and tool factory. It exists for rigs that need terrain worldgen cannot
produce, above all the `SeamWorld` fixture: a land strip crossing x = 0 / W − 1 with settlements on both sides.
- Each layer must be Size² long and is COPIED, so the set stays immutable.
- Every polyline cell must be on the map.
- The content hash is computed exactly as for a generated set.
- The game never calls it: every played world comes from `Worldgen.Generate`.
- ADR-008's decision (immutable, outside Clone, hash folded into every world hash) is unchanged.

## 10. Determinism and conservation

- **Determinism.**
  - No banned construct; the three gate scripts pass.
  - The building order's composite key (EnqueuedTurn, SettlementId, Slot) has a tie-dense test with ≥ 17 tied heads
    (`BuildingOrderTieDenseTests`).
  - `MapTopology.Dx`'s tie at size/2 is pinned.
  - The origin ring scan sorts by (ring, node id).
  - Two-process byte identity holds for the founded seed-42 and seed-16 (seam) worlds and the AI founded-mix leg with
    kinds 11 and 12. Replay and `sim inspect` VERIFIED.
- **Conservation.**
  - Every material movement is `EmpireStockpile.Draw` → `Ledger.Flow` sinks under `ReasonIds.ConstructionMaterials`,
    with `OverdrawPolicy.Throw`.
  - Lockstep draws total exactly the cost.
  - A cancelled entry's delivered units never return.
  - Under the split no stock goes negative, and the ledger audits balance exactly in the competing-heads-plus-road tests.
  - A 300-turn, one-AI world (seed 42, shipped configs, player granary orders keeping heads part-built) with every
    batch-3 layer on reconciles every good on every turn in Observer, TurnRecord, TelemetryWriter and TurnAuditModel,
    with no DISCREPANCY (`IncrementalConstructionTelemetryTests`, `IncrementalConstructionTurnAuditRunTests`).

## 11. Test map

- **WS1:**
  - `IncrementalConstructionTests` and `ConstructionTerminationTests` (including, from fix round 1, the unobtainable
    crafted input and the project gone from the content);
  - `InstitutionLifecycleTests.AUniversityBuiltPieceByPiece_FoundsNothingWhilePartBuilt_ThenExactlyOneInstitution_AndNoLaterStepFoundsAnother`;
  - `IncrementalConstructionIntegrationTests` (granary turn-exact, same-step production and trade, the AI stall cancel);
  - `IncrementalConstructionTelemetryTests`;
  - `BuildingOrderTieDenseTests`, `ConstructionOrderKindsTests` and `ProjectCardSectorLabelTests`;
  - Sim.Ui: `IncrementalConstructionUiTests` (including the order-log resume round trip with a part-built head, a cancel
    and a split) and `IncrementalConstructionTurnAuditRunTests`.
- **WS2:** `ResourceAllocationTests` (split grid, starvation guard, b = 100 byte identity, validation, turn-exact,
  persistence) and Sim.Ui `ResourceAllocationUiTests`.
- **Schema, trailer and migration:** `ConstructionSchemaV33Tests`, `CliSaveAndSizeMessagesTests` (the CLI loader, the
  trailer recheck, the size refusal, `sim inspect`), `PlatformNoticeTests`, and the save/load battery's part-built
  point `IntegratedSaveLoadBatteryTests.SaveLoad_MidPartBuiltConstructionHead` (world 8, p = 231,666, since
  final-round fix 1: world 7's progress table was world 6's, which the battery's tax save already saves).
- **Old sessions:** Sim.Ui `Batch2SessionResumeTests` (the real v32 fixture), `ChronicleAndResumeTests.Resume_OfASessionRecordedUnderAnOlderSchema…`
  and `ResumeNoticeUiTests` (both rigged flat, as a v32 build recorded).
- **WS3:**
  - `MapTopologyTests` and `MapTopologyGuardTests`;
  - `NonStrideLatticeGeometryTests`;
  - `SeamPathfindingTests`, `SeamWorldSystemTests`, `SeamSitingWiringTests` and `SeamCrossingShippedWorldTests` (the
    seed-16 route and distance, and the seed-16 seam migration under a turn-0 knowledge rig);
  - `CylinderWorldgenTests`;
  - Sim.Ui: `CylinderCameraTests`, `CylinderLensTests`, `CylinderBakeTests`, `CylinderOverlayTests`,
    `CylinderSessionTests`, `CylinderFrameCostTests` and `CylinderLaunchRefusalTests`.
- **Attribution:** §8.
- **Mutants.** Each stage recorded its mutant kills with semantic tests in its own results, outside the repository.
  - The known SURVIVORS are D-054's: the seam cross-fade mutants F9a, F9b and F9c (teeth without aim, ADR-015 §7.2).
  - Verifier survivors since closed: MU1 and MU2 (the Unobtainable and UnknownProject classifications; fix round 1) and
    MF (`hashesChecked` keyed on `!RecordedFlat`; the nits round).
  - Progress quantized to 1,000 on load was an equivalent mutant for `SaveLoad_MidPartBuiltConstructionHead` alone
    while it saved world 7 (p = 145,000); the battery's Age and Tax saves (p = 32,777 and 107,777) killed it. Since
    final-round fix 1 the test saves world 8 (p = 231,666) and kills it alone (D-054 §8).

## Note (2026-10-16, D-055; batch 4 track T2) — correction to §8: not every `IncrementalConstructionAttributionTests` world is flat

§8's list of single-layer controls describes `IncrementalConstructionAttributionTests` as "the SimConfig half; its worlds
are founded flat". That parenthesis is wrong for one of the class's facts. The text above stays as written (this note is
append-only); the note supersedes the parenthesis for that fact only.

- **Flat or terrain-less, as §8 says.** The golden reproductions are flat or have no terrain:
  - `FoundedGoldenSeed42Turn300_…`, `DrivenGoldenSeed42Turn300_…` and `AiWorldSeed42Turn300_…` found on
    `TestConfigs.PreD054Worldgen()`, the flat pre-batch-3 world;
  - `ToyGoldenSeed42Turn200_AndFirstReignTurn40_…` runs the terrain-less toy world, and its first-reign leg is flat.
- **On the cylinder.** The per-layer turn-exact proof `PerLayer_IncrementalConstruction_APlayersGranaryDivergesExactlyAtTheFirstStepItIsPartBuilt`
  founds and steps `TestConfigs.DevWorldgen()`. That is worldgen.json as shipped, at 256 px with four settlements, so
  `topology.wrapX` is true. 256 is a multiple of the 4-px lattice stride, so the shipped mode applies: the world is the
  cylinder.
  - VERIFIED: `CylinderAttributionDevStarvedTests` founds the same `DevWorldgen()` through the F7 harness, whose `Run`
    asserts `Terrain.WrapX` on every arm, and passes on base-b3 + T1a + T2. The tested revision (its tree hash) and the
    run are recorded in D-055's T2 notes.
- **What this changes.** The incremental-construction layer had a turn-exact attribution on the shipped cylinder before
  batch 4. Its AI-world absence (D-055 F7 rig #14) and the reservations switch built on it (rig #16) are the
  `CylinderAttribution*` rigs listed in D-055.

## Note (2026-10-17, D-055; batch 4 integration) — schema v34 follows v33; what in this ADR it changes

Appended at the file's end; the text above stays as written (D-054 §10 Q19 is unruled). The contract for v34 is
`docs/adr/adr-040-schema-v34-construction-holds-pathfinder-surface-and-cli.md` (ADR-040); the decisions are D-055.

- **v33 is no longer the current schema.** `CanonicalSchema.Version` is 34: section 61 `ConstructionHolds` (16-byte rows)
  is appended after section 60. §2's two tables, their rows, their invariant and order kinds 11 and 12 are unchanged, and
  the order log's wire format is unchanged (batch 4 adds no order kind).
- **The readable versions are the set {32, 33, 34}** (`CanonicalSchema.IsReadable`). `OldestReadableVersion` stays 32, but no
  gate compares with it any more. §7's "accepts version 33, and version 32" now reads: accepts 34, 33 and 32; a v33 stream
  is migrated with `ConstructionHolds` empty, and a v32 stream with all three later tables empty and its terrain flat.
  The migration notes the loader prints are D-055 §2's texts ("migrated from save schema v32: …" / "… v33: …"), no longer
  §7's "migrated to v33 on load (D-054)".
- **Messages that named "v33" as this build's schema** now name v34, and a v33 session is now an older-schema session:
  `sim inspect` prints NotChecked(33) for it, and its UI resume takes the D-008 path (a cylinder rebuild,
  `TopologyFlags.FirstCylinderSchema` still 33, so `RecordedFlat` is unchanged). The re-pinned message tests are
  `CliSaveAndSizeMessagesTests` (incl. `TheReproduceLine_IsThisBuildsReplay_FromV34…`) and
  `ResumeNoticeUiTests.ASessionResumedUnderSchemaV33_IsACylinderRebuild_WithTheCylinderNoticeLine_AndItsTraceIsNotChecked`.
- **§8, the PreD054 pair:** `PreD054Sim` strips the D-055 reservations layer as well (through `PreIncrementalConstruction`,
  the R4 rule); `PreAllocationRatios` keeps it (the recorded R4 exception, D-055 §1.1). With the pair and
  `SchemaProjection.V32`, the batch-2 references still reproduce byte for byte (`D054PairAttributionTests`, passing on the
  merged batch-4 tree).
- **§8's "its worlds are founded flat"** is corrected by the 2026-10-16 note above (track T2); it is not repeated here.
- **§10:** the cross-process coverage gains the allocation leg (D-055 §4), which asserts a non-empty `ConstructionHolds`
  across three processes.

## Note (2026-10-19, D-058) — a turn's construction capacity carries down the queue; what in this ADR it changes

Appended at the file's end; the text above stays as written. Decisions: `docs/d058-build-one-at-a-time-capacity-carries-within-the-turn.md`.

- **The Director's ruling** ("Build one at a time … it should show in the next turn as 7 build, 1 under construction, 1
  not started"). The v33 tables are unchanged: `ConstructionProgress` rows, their invariant (p in [0, N − 1], one row
  per entry) and order kinds 11/12 (and D-056's 13). What changes is how many entries one step may move. When a head
  completes, the settlement's leftover capacity carries its queue's next entry into the same step, at that entry's own
  place in the building order and under the D-055 set-aside rules. A step can therefore complete several entries of one
  queue and leave the next part-built. Switch: `sim.json construction.continueWithinTurn` (shipped on; the loader
  refuses it without `incremental`); control `TestConfigs.PreContinueWithinTurn`.
- **Schema.** No version, section, row or field change: schema stays v34, IoVersion 1, readable set {32, 33, 34}.
- **`ConstructionLabor`** (v31) stays one row per settlement; with a continuation it is the sum of what the step's
  entries used.
- **Hashes.** Any world in which a completing head has a queued successor and capacity left now differs from the
  batch-4 world from that step on. No golden of the suite or of ci.yml moved (D-058 §7).
