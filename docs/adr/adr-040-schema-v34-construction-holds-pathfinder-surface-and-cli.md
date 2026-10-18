# ADR-040 — SCHEMA v34 (`ConstructionHolds`), THE v33 → v34 MIGRATION, THE CONSTRUCTION → ROADDEVELOPMENT READ, THE PATHFINDER SURFACE, AND THE CLI CONTINUATION AND SPLIT REPORT

**Status: PROPOSED — uncommitted, for the Director's review with D-055.**
- Written 2026-10-17 for the batch-4 change on top of the uncommitted batch-3 tree of `m5w-integration` (base
  `de86ace`). Not committed, pushed, merged or packaged. Updated 2026-10-18 at the integration's update stage for T3's
  final revision (rounds 2 and 3): §7, §9 and §10.
- **Governing record:** `docs/d055-batch4-reservations-attribution-allocation-ci-persistence-pathfinding.md` (D-055).
  D-055 holds the decisions (the hold rule, its release table and its priority rule), the supersessions, the
  measurements, the moved pins and the limitations. This ADR is the technical contract.
- **Touches the kernel contract.** The constitution requires an ADR for this.
  - `CanonicalSchema.Version` 33 → **34**, and one readability helper, `CanonicalSchema.IsReadable` (§3).
  - `Snapshot.Load` gains the v34 load refusals (§2).
  - Kernel files changed: `CanonicalSchema.cs` and `Snapshot.cs` (the schema), and `TurnExecutor.cs` — **comments only**
    (§8). The construction → roaddevelopment read is NOT a kernel change (§4).
  - **No order kind** was added, and the order log's wire format (`OrderRecord`, `IoVersion` 1) is UNCHANGED.
  - The snapshot topology trailer is unchanged (v33 and v34 carry it).

## 1. What exists now

| Layer | New state / contract | Owner | Switch (shipped ON) | Attribution control |
|---|---|---|---|---|
| Reservations ("holds"; "set aside" for the player) | `ConstructionHoldRow` (section 61) | ConstructionSystem (sole writer); RoadDevelopmentSystem reads it read-only | `sim.json` `construction.reservations` (loader-refused unless `incremental`) | `TestConfigs.PreReservations` |
| Cylinder pathfinding O1–O6 | none (byte-identical; no state) | Pathfinder, PathBuildSystem, CatchmentSystem | none | the verbatim batch-3 oracle in Sim.Tests (`Pathing/Legacy/`) |
| CLI continuation and split report | `sim run --from-save`, `--split-report`, `sim split-check` | Sim.Cli; `Sim.Core/Observability/SplitProbe`, `SplitReport`, `SplitReportCheck` | none | — |

## 2. Schema v34 — one appended table

Appended after section 60 (ResourceAllocations). No existing row changed width. A 4-byte int count prefix followed by its
rows, written field by field (ADR-005).

| Section | Table | Row (field: width) | Row bytes |
|---|---|---|---|
| 61 | `ConstructionHolds` | `int Purse`: 4 · `int Good`: 4 · `long Units`: 8 | **16** |

- **ExpectedLength** gains `4 + 16·n61`. A world with no hold carries one empty count prefix (4 zero bytes).
- **The purse key.**
  - `Purse ≥ 0` is a polity id: an Empire purse (`Reservations.PolityPurse(polity)`).
  - `Purse < 0` is −1 − settlementId: a single-settlement purse (`Reservations.SettlementPurse(settlement)`).
  - A head's key is `ConstructionQuery.PurseKey(prev, cfg, settlement)`, read on PREV: the controller's polity id when
    `construction.empireStockpile` is on and the settlement is controlled; otherwise its own settlement's key. With the
    rule on each purse's holder set is `ConstructionQuery.PurseHolders` — `Holders` less every place whose own key is
    another's — so the holder sets are disjoint and a place with two control rows belongs to its lowest controller's
    purse alone (D-055 §1.2).
- **Semantics.** One row per (Purse, Good) with Units > 0: the units of that good the purse's unreleased queue heads set
  aside at the end of the step (the walk `ConstructionQuery.HoldsWalk`, D-055 §1.3). Not a conserved stock: holds move
  no goods; only Ledger draws do.
- **Row order:** strictly ascending (Purse, Good), compared as explicit integers.
- **Owner and lifetime.** ConstructionSystem CLEARS the table at the start of EVERY step, whatever the switch, and
  publishes the step's set-asides only with the rule on (`Reservations.Enabled`). The per-step publication pattern of
  `ConstructionLabor` (section 57).
- **The invariant** (`Reservations.InvariantViolation`): sorted, no duplicate (Purse, Good), Units > 0, Good ≥ 0.
  - ConstructionSystem asserts it, with the D-055 hold invariants (i)–(vi) and the goods-registry bound, at the end of
    every step with the rule on ("construction: … (the D-055 hold invariant).").
  - **`Snapshot.Load` refuses** a table that breaks it (`SnapshotFormatException`, "save is corrupt: … (schema v34
    invariant)"). The goods-registry bound is checked only at the end of a step, where content is available.
- **Never an input to construction.** Construction rebuilds the table from scratch every step and never reads it. In the
  SHIPPED pipeline order (construction before roaddevelopment, §4) nothing reads it before construction rebuilds it, so a
  loaded table cannot change the next world. Pinned by the tamper test
  `ConstructionLifecyclePersistenceTests` L10 (a v34 save whose rows are altered or dropped, still valid: the world at K
  differs, the next world equals the untampered continuation's; it kills "the walk seeded from PREV.ConstructionHolds")
  and by `ConstructionHoldsSchemaV34Tests` (`Construction_ClearsTheHoldsEveryStep_WhateverTheSwitch_AndNeverReadsThem`).
- **The checklist was carried out** (D-055 §7, SCHEMA): Write, Read(version) and ExpectedLength; `WorldState` (row,
  interface, concrete, initialiser, Clone); the `SystemCatalog` hand-out; `StateEquals`; the `SnapshotDiff` layout and
  `LayoutFor`; every `Assert.Equal(33, …)` → 34; the tail-offset tests; `SchemaProjection.V33` / `HashAtSchemaV33`, with a
  v34 strip (asserting it removes 0 rows) first in every older projection helper; the forensic readers (none gates on the
  version).
- **POPULATED-table tests** (the constitution's rule for a new serialized row type): `ConstructionHoldsSchemaV34Tests` —
  exact ExpectedLength, bit-exact round trip and hash equality on a populated table, and each load refusal.

## 3. The readable set and the migrations

- **The readable versions are the SET {32, 33, 34}:** `CanonicalSchema.IsReadable(v)`, used by `CanonicalSchema.Read`,
  `Snapshot.Load`, `SaveFiles.MigrationNote` and `SnapshotDiff.LayoutFor`. `OldestReadableVersion` stays 32, the oldest
  member; no gate compares with it. Every other version is refused with the D-008 message.
  - `SessionSchemaNotice` is deliberately NOT routed through `IsReadable`: sessions are replayed, not loaded, and a trace
    hash covers exactly the schema that wrote it, so it keeps schema equality.
- **The streams.** A v33 stream stops after section 60 and a v32 stream after section 58; every missing table is empty.
  `SnapshotDiff`: v34 → Layout, v33 → Layout[..^1], v32 → Layout[..^3]; the walk reaches ExpectedLength.
- **v33 → v34.** `ConstructionHolds` is added empty. **Exact in the shipped pipeline order:** construction clears and
  rebuilds the table before road development reads it, so a migrated v33 world continues exactly as a v34 save of the
  same state. In an order that runs road development first, road works in the first step after a v33 load see no
  set-asides while a v34 save would carry its rows: pinned in both directions by
  `ConstructionReservationTests.TheV33Migration_IsExactInTheShippedOrder_ARoadFirstOrderReadsTheMigratedWorldsEmptyTable`.
- **v32 → v34.** The three tables added since v32 are empty, the terrain is regenerated flat (`TopologyFlags.RecordedFlat`;
  `FirstCylinderSchema` stays 33), and queued entries read progress 0, EnqueuedTurn −1. `SaveFiles.WorldgenFor` handles
  v32 as flat; v33 and v34 use the trailer.
- **The loader's notes** (one stderr line, `SaveFiles.MigrationNote`): D-055 §2 quotes both texts exactly. The v32 text
  keeps batch 3's flat-terrain sentence (a recorded departure from the design's text).
- **The v33 fixtures**, recorded by the BATCH-3 build (base-b3, the CLI stamped `b3-final-v9-0f8f45c9`):
  `Sim.Tests/Fixtures/v33-seed42-turn2.civsnap`, `v33-fixture-orders.bin`, `v33-session-q17/` (manifest, orders, trace),
  with `Sim.Tests/Fixtures/README-v33-fixture.md` (the commands, the order records, the printed hashes and every file's
  sha256).
- **The comparators** (`ConstructionHoldsV33FixtureTests`; every one steps with the shipped executor; the same for the
  real v32 fixture in `ConstructionFixtureContinuationTests`):
  - (a) the loaded save's v33 projection equals the batch-3 build's hash;
  - (b) the loaded world equals an in-process `PreReservations` run of the same orders at the same turn K;
  - (c) its continuation equals that run's continuation, turn by turn;
  - (d) the same after a v34 re-save and reload;
  - (e) anti-vacuity: (c) differs from the all-PreReservations continuation at a pinned turn W (3 on the v33 fixture),
    because a hold binds.
- **UI resume of a v33 session:** the schema-mismatch path (D-008): replayed under batch-4 rules with the four-line notice,
  line 2 "Resumed by replaying its seed and orders (D-008).", trace not checked (`V33SessionResumeTests`;
  `ResumeNoticeUiTests.ASessionResumedUnderSchemaV33_IsACylinderRebuild_…`). `sim inspect` of a v33 session prints
  NotChecked(33) and exits 0. Its limitations (an ADR-034 refusal, a shifted kind-11 slot) are D-055 §9.

## 4. The construction → roaddevelopment read: no kernel change, order as data

- **The read.** Road development must not spend what this step's queue heads set aside (D-055 §1.7). It is handed the
  SAME NEXT `ConstructionHolds` table construction writes, as an `IReadOnlyTable`, by `SystemCatalog.RoadDevelopment`
  (`RoadDevelopmentTables(next.TransportEdges, next.RoadDevelopments, next.GoodStocks, next.ConstructionHolds)`).
  Construction is the table's SOLE WRITER; road development never writes it.
- **No kernel change.** `Sim.Core/Kernel/TurnExecutor.cs` keeps base-b3's behaviour (§8 for its comment-only edit). The
  read uses the design's existing non-kernel pattern for a read of another system's NEXT output — the **GoodStocks
  share**: one NEXT table handed out in `SystemCatalog`, the order in `pipeline.json`, no kernel guard. The precedent is
  ConsumptionSystem's read of the NEXT stock that production wrote earlier in the step (`ConsumptionSystem.cs:46-48`,
  "Reading its own shared Next stock is lawful (owned-table access)"). `SystemCatalog`'s ownership paragraph ("SANCTIONED
  SHARED TABLE — ConstructionHolds (D-055, schema v34), split by WRITER and READER, ORDER AS DATA") is the record.
- **Order is data.** Road development reads the table AS IT STANDS when it runs.
  - **The shipped order** runs construction (10th) before roaddevelopment (22nd), so road works honour what the queue
    heads set aside in the SAME step. **Pinned** by `ConstructionHoldsSchemaV34Tests.TheShippedPipeline_RunsConstructionBeforeRoadDevelopment`,
    which loads the SHIPPED `pipeline.json` through `PipelineLoader` and the shipped catalogue.
  - **Any other order** (a hand-built pipeline): road development reads what NEXT holds at that point of the step — the
    previous step's published set-asides, carried into NEXT by the kernel's PREV → NEXT copy, or a loaded save's rows;
    construction, if it runs later, then clears and rebuilds the table. A reversed order is not refused. Pinned by
    `ConstructionReservationTests.TheEdgeOrderIsData_TheShippedOrderReadsThisStepsHolds_AnotherOrderReadsTheTableAsItStands`
    (from a world where purse 1 sets aside 22 of its 22 timber, a 60-timber road order charges 0 in the shipped order
    whatever was carried in; in a reversed or road-only pipeline 0 / 12 / 22 when NEXT carries 22 (published) / 10 (a
    loaded save's) / no timber rows; road-only leaves the carried rows untouched).
  - **The rule off** (`Reservations.Enabled` false): `RoadDevelopmentQuery.SetAsideWithin` returns 0 whatever the table
    holds, so the rule off is batch 3 in ANY order, for both purse kinds (#22's rule-off rows).
- **History.** The batch's design spec (§1.4, §1.8 #22) first had road development read the table only when construction
  ran earlier (otherwise an empty table), resolved by a kernel edge in `TurnExecutor` (T1a's first revisions:
  `SystemRegistration.SameTurnProducer`, two invokers). The kernel is frozen without Director sign-off, so T1a round 2
  reverted `TurnExecutor.cs` to base-b3's file and adopted order-as-data; the batch-4 orchestrator ruled it on 2026-10-17
  (D-055 §11).
- **The kernel spec's interaction matrix (P-26/Q-66) is still absent.** As for the GoodStocks share, the `SystemCatalog`
  paragraph and this ADR record the edge.

## 5. Read-only query surface added in Sim.Core (no system reads another)

- `Sim.Core/State/Reservations.cs`: `Enabled(cfg)`, `PolityPurse`, `SettlementPurse`, `InvariantViolation`.
- `Sim.Core/State/ConstructionQuery.Holds.cs`: `HoldsWalk` (pure; called by the step, `HeadStatus`, `AllocationFor` /
  `OwnHeadClaims` and the UI's readings), `PurseKey`, `PurseHolders`, `EmpirePursePlaces`, `ReleaseOf` (released =
  invalid: unknown project, no labour adults, `NoConfig` viability). `ConstructionQuery.Incremental.cs`:
  `MaterialCause.HeldForEarlier` (= 5), naming the holder.
- `HeadStatusReport` gains optional trailing fields (Holder, SetAsideAhead, SetAside by good id, Release); `EmpireAllocation`
  gains the building holds.
- `RoadDevelopmentQuery.SetAsideWithin` (the charge-time holding; 0 with the rule off).
- `Sim.Core/State/SetAsideQuery.cs` (track T1b, read by the UI only): `Read` (HeadStatus's own setup), `HeadsUnder` /
  `ForecastGrants`, `Ahead` / `HeldIn` / `Holders`, `DrawnAhead` / `Drawers`, `StepTable` (the coming step's set-asides
  with the pending orders), `ViewersOwnPurse` (the P7 gate), `ViewerTotals`. No system reads it.
- `ConstructionQuery.Blocker` / `BlockerSeenBy` overloads with the set-asides (and the draws ahead); `AvailableActionsQuery.Roads`
  with an `ActionQueryContext` overload.

## 6. The Pathfinder surface (item 5; all byte-identical; purity unchanged)

**Purity.** The Pathfinder header's mandate ("NO persistent caches, NO memoization …") still holds. The change adds no field
and no static to Sim.Core: every new array is a local of one call, nothing is kept across calls or steps, and nothing is
shared between systems.

- **`public static double[] Pathfinder.DistancesTo(TraversalLattice lattice, IReadOnlyWorldState world, int origin,
  ReadOnlySpan<int> targets)`** (O5).
  - Entry i is `Isochrone(origin, double.MaxValue)`'s cost at `targets[i]`, bit for bit; +∞ where that isochrone does not
    reach (an impassable target, an unreachable target, an impassable origin).
  - The same Dijkstra (overlay, Expand, (key, id) heap), stopped as soon as the last awaited PASSABLE target is settled:
    the pops up to the stop are Isochrone's own, a settled g never changes, and a passable target in another component
    keeps the search running to exhaustion, exactly as the full isochrone did.
  - A duplicate target is awaited once and each copy reads the same cost; a target equal to the origin costs 0; an
    impassable target is never awaited; with nothing awaited, or an impassable origin, no search runs; an out-of-range
    target throws `ArgumentOutOfRangeException`.
  - The early exit changes no result; only `sim bench` (the catchment phase's time) can see it (D-055 §6).
  - `CatchmentSystem`'s `SettlementDistances` call it once per settlement with the other settlements' origins in ascending
    order; the rows are unchanged (ascending (From, To), +∞ as the zero-flow sentinel). `Isochrone` keeps its callers
    `AiFoundingPolicy.ReachableNodes` and the UI's resource-overlay reach; catchment membership comes from `Partition`.
- **`public readonly record struct Pathfinder.LaneOverlay(int[] Offsets, int[] Targets, double[] Costs, double
  MinEdgePerUnit)`** (O3): the lane overlay in CSR form; each node keeps the batch-3 lane order.
- **`public static LaneOverlay Pathfinder.BuildOverlay(TraversalLattice lattice, IReadOnlyWorldState world)`**: was private,
  now public, returns the CSR (filled backwards with `--Offsets`: no Dictionary, no cursor array).
- **`public static double Pathfinder.Heuristic(int x, int y, int gx, int gy, int size, bool wrap, double effMin)`** (O4).
- **`public double TraversalLattice.StepCostKnown(int a, int b, bool diagonal)`** (O4, O6): the step cost from a known
  direction, used in FindPath, Expand (Isochrone, Partition, DistancesTo) and RelaxCappedFrom.
- **`public static int MapTopology.DxOnMap(int x1, int x2, int size, bool wrap)`**: the branch-only wrapped dx, equal to
  `MapTopology.Dx` for columns in [0, size). It lives in MapTopology because `MapTopologyGuardTests` flags a raw dx anywhere
  else in Sim.Core.
- **`public static int[] PathBuildSystem.Targets(TraversalLattice lattice, TerrainSet terrain, ReadOnlySpan<int> frontiers,
  IReadOnlyList<bool[]> catchments)`** (O1, O2), exposed for differential testing.
  - Contract: `Targets(…)[i] == ChooseTarget(lattice, terrain, catchments[i], PassableComponent(lattice, frontiers[i]))`
    for every i; `ArgumentException` when the two counts differ.
  - **Within-step locals.** O1: every lattice node's block fertility is computed once per call. O2: each passable
    component is flooded at most once per call; flooded components are kept in a `List<bool[]>` in flood order (never a
    Dictionary or HashSet) and a frontier reuses the first listed component that contains it. Reuse is exact because
    8-adjacency, the seam step included, is symmetric. An impassable frontier floods nothing and its empty set is not
    kept. The arrays are read-only after the flood and dropped when the call returns.
  - PathBuild's step runs in two passes (bank and fix every frontier in settlement order; then choose every target and lay
    the segments in the same order), keeping the batch-3 order of effects. `PassableComponent`'s doc no longer says
    "recomputed per build call".
- **Proof artefacts:** the batch-3 code verbatim in `Sim.Tests/Pathing/Legacy/` (`LegacyPathfinder.cs` with MinHeap,
  LegCosts and the jagged BuildOverlay; `LegacyPathBuild.cs` with ChooseTarget and PassableComponent), and the differential
  tests `LegacyPathfinderDifferentialTests`, `LegacyPathBuildTargetsTests`, `LegacyCatchmentDistancesTests`,
  `MapTopologyBranchDxTests` and `PathBuildStepWiringTests`.

## 7. The CLI: `--from-save`, `--split-report`, `split-check`

- **`sim run --from-save SAVE --turns N [--size PX] [--ai-empires N] [--seed S] [--founded] [--report] [--save-at K --save
  PATH] [--orders PATH] [--hash-log PATH] [--split-report PATH]`** (the usage text's form) continues a saved world in a
  new process (named so as not to collide with Sim.Ui's `--resume`, which replays a session).
  - Loaded by the CLI's one loader (`SaveFiles.Load`: terrain regenerated and hash-checked; v32 and v33 migrated with the
    stderr note). A non-canonical-size save needs `--size`.
  - `--turns N` is N MORE steps; `--save-at` is absolute, in (K, K + N]. The step loop is shared with a run from founding
    (`Cli.Steps`), which saves when the world's turn equals `--save-at`.
  - The seed and world kind are the save's: `--seed` / `--founded` are refused if they contradict it; `--ai-empires`, if
    given, must equal the save's FOUNDING AI Empire count (below), as in a run from founding — the refusal names any
    revolt-born AI polity, which is not counted; `--emit-session` and `--settlements` are refused.
  - **Turn zero** (ADR-034's validation world) is the REGENERATED founding world (`Cli.StartWorld` from the save's seed,
    size, topology, founding settlement count and founding AI Empire count), CHECKED against the save (founding
    settlements, roster prefix, capitals); a mismatch is refused. Validation is therefore the uninterrupted run's, and is
    done up front: a player record the founding world refuses exits 2 with the uninterrupted run's message, before any
    step.
  - **The founding AI Empire count** (`FromSave.FoundingAiEmpires`; T3 round 3): capitals are written only at founding
    and never removed, polities are never removed, and a revolt-born polity gets no capital. So when the AI polities
    holding a Capitals row number fewer than the founding settlements less the player's one, they ARE the founding AI
    Empires; otherwise (every founding settlement is a capital, and founding may have left capital-less Empires) the count
    is the roster's AI polity count. A save made after a revolt therefore continues; a save of a world founded with more
    Empires than settlements in which a revolt then founded an AI polity is refused (its founding count cannot be told).
  - **`--orders` is a PLAYER log:** records stamped before K are dropped unread; a record stamped AT OR AFTER K by a polity
    other than the save's player is refused (the continuation issues the AI's orders itself and would deliver them
    twice); an emitted run log whose AI records all precede K is accepted (the K − 1 boundary is pinned on a real emitted
    log). The AI worldgen comes from `SaveFiles.WorldgenFor`.
- **`--split-report PATH`** (founded worlds only; refused for a toy world, a configuration with the split off, or with the
  hold rule off): JSONL `split-report/v1`, one line per (step, roster Empire with a split): per eligible good S, start,
  E_b, E_r, D_b (the claim), D_r, cap, G_b, holdsBack, the forecast, and the MEASURED building draws, set-asides (the
  published table) and road draws; per head (HoldsWalk, the building order) settlement, slot, project, enqueued, p before
  and after, release, limit, and per good need, budget, draw, hold and — for the binding good — cause, holder and the
  set-aside ahead. It changes no hash and no save (pinned in process and across processes).
- **`sim split-check REPORT [--control CONTROL] [--expect-hold-at K] [--twin TWIN --order-at T:B]`**: the judgement in
  tested C# (`Sim.Core/Observability/SplitReportCheck`, the `CorridorStatus` precedent). P0 non-vacuity; P1 forecast =
  actual and the heads sum to the measured draws and set-asides; P2 E_b + E_r = S, build + holds = G_b ≤ D_b, G_b ≤ cap,
  build + holds + road ≤ S; P3 S − build − holds ≥ min(D_r, E_r) unless guarded; P4 scarcity binds; P5 / P6 surplus both
  ways; P7 competing heads; P8 the control claims more at the first binding step; P9 the control is the b = 100 twin from
  the same world, and its rows satisfy P1, P2, P3, P11 and P12; P10 a HeldForEarlier head whose holder is ahead, sets the
  good aside, and whose draw fell below min(need, budget less the draws ahead); P11 every row's split re-derived in the
  checker's own integer arithmetic (E_b = floor(S·b/100), E_r = S − E_b, cap = E_b + max(0, E_r − D_r), G_b = min(D_b,
  cap), D_r taken as 0 in a guarded row; S, D_b, D_r ≥ 0, b in [0, 100]); P12 every row's walk re-derived — heads
  strictly ascending (EnqueuedTurn, SettlementId, Slot), the walk starting at G_b (S at b = 100), each head resolved
  against what the heads ahead left and each unreleased head drawing and setting aside together min(its need, that
  budget), a released head nothing, and a head reported limited by materials (`Materials`, `Unobtainable`) short of some
  good (its budget below its need); PK `--expect-hold-at K`: world K published a set-aside binding a younger head; PT
  `--twin TWIN --order-at T:B`: the kind-12 order stamped T changed building work's draw in its own delivery step against
  TWIN, the same run without that order (row T at b = B in the report and not in the twin; the twin's earlier rows the
  report's byte for byte; the same S, D_b and D_r at T; the twin's row T satisfying P1, P2, P3, P11 and P12; some good's
  draw different). Failure order P0, P1, P2, P3, P4–P7, P10, P11, P12, P8, P9, PK, PT. Exit 0 pass, 1 failed (each
  property named) or a usage error (`--twin` without `--order-at`, or the reverse), 2 unreadable — including a report
  whose goods are not strictly ascending in id in a row or a head, or with a missing or mistyped field, which the parser
  (`SplitReport.Parse`) names with where it is (`goods[0]: missing field "holds"`).
- The usage text states the v34 migration rules and the three additions, `--ai-empires` as the founding count, and
  split-check's P0–P12 and `--twin`.

## 8. The `ITurnObserver` use, and the `TurnExecutor.cs` comment-only edit

- `--split-report` is a read-only `ITurnObserver` (`Sim.Core/Observability/SplitProbe`). It reads each phase's
  work-in-progress world through the existing `OnPhaseState(string phase, IReadOnlyWorldState next)` window (already in
  base-b3, default body empty), and throws when a step ran no `construction` or no `roaddevelopment` phase, or one twice.
  When `observer` is null — every canonical, golden and replay path, and every CLI run but `sim bench` and `sim run
  --split-report` — the observer is never reached.
- **`Sim.Core/Kernel/TurnExecutor.cs` changes by two documentation comments only:** the `ITurnObserver` summary ("bench
  only") now names the bench and the read-only split report; the `OnPhaseState` remark now names the two CLI runs that pass
  an observer. Every changed line is a `///` line; no code token changed (VERIFIED on the merged tree against base-b3).

## 9. Determinism and conservation

- No banned construct; the three gate scripts pass on every track's tree and on the merged tree.
- The building order's composite key (EnqueuedTurn, SettlementId, Slot) keeps its tie-dense test, and the holds have their
  own (`ConstructionReservationTests.TwentyTiedHeadsAcrossTwoInterleavedPurses_…`: 20 tied heads, two purses
  interleaved). The walk iterates sorted int arrays only. The checker's own building-order comparison
  (`SplitReportCheck.CompareBuildingOrder`, P12) has its tie-dense test too (`SplitReportRederivationTests`: 24 heads, ties
  on turn and settlement, three insertion orders; each of the 23 adjacent swaps fails P12 naming that pair).
- Holds move no goods: every material movement is still `EmpireStockpile.Draw` → `Ledger.Flow` under
  `ReasonIds.ConstructionMaterials`. The hold invariants (D-055 §1.8) assert no fictitious stock, no double counting and
  no negative stock at the end of every step; the property test checks conservation at every step and good.
- O1–O6 are byte-identical to batch 3 on every ci.yml cross-process leg and the 20 × 650 nightly (D-055 §6).
- The allocation leg asserts two-process identity, observer neutrality and a non-empty `ConstructionHolds` across three
  processes, and that the b = 70 order changes building work's draw in its own delivery step (PT) (D-055 §4).

## 10. Test map

- **Schema and migration:** `ConstructionHoldsSchemaV34Tests`, `ConstructionHoldsV33FixtureTests`, `ConstructionSchemaV33Tests`,
  `CliSaveAndSizeMessagesTests`, `ConstructionFixtureContinuationTests`, `CliConstructionLifecycleTests`; Sim.Ui
  `V33SessionResumeTests`, `Batch2SessionResumeTests`, `ChronicleAndResumeTests`, `ResumeNoticeUiTests`.
- **The rule and the read:** `ConstructionReservationTests` (D-055 §1.12), `IncrementalConstructionTests`,
  `EmpireConstructionStockpileTests`, `ResourceAllocationTests`, `IntegratedSaveLoadBatteryTests` (incl. `.Lifecycle`),
  `ConstructionLifecyclePersistenceTests`, `SetAsideReadingTests`.
- **Attribution:** `ReservationsLayoutAttributionTests`, `IncrementalConstructionAttributionTests`, `CylinderAttributionReservationsTests`.
- **Pathfinder:** §6's proof artefacts.
- **CLI and observer:** `RunFromSaveCliTests`, `SplitProbeTests`, `SplitReportCheckTests`, `SplitReportRederivationTests`,
  `AllocationCiLegTests`, `AllocationScenarioTests`, and the ci.yml step "Allocation split, binding, cross-process
  (D-055)".
- **UI:** `ConstructionHoldsUiTests`, `KnowledgeLeakTests.Holds`, `ResourceAllocationUiTests`, `ConstructionLifecycleResumeTests`,
  `ConstructionResumeColonyAndBuiltTests`.
