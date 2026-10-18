# CR-021 — ITEMS 3, 4 AND 6 OF THE DIRECTOR'S 2026-10-13 BATCH AGAINST FROZEN DECISIONS

**Status: RESOLVED 2026-10-15 (D-054), by the Director's 2026-10-14 instruction (batch 3)** — items 3, 4 and 6 are
built; item 6 as the unrestricted cylinder, with its exploration gating and Satellites out of scope. *As first
written:* **OPEN — ESCALATED TO THE DIRECTOR.**
- Raised 2026-10-13 on the uncommitted `m5w-integration` working tree (base `de86ace`), under CLAUDE.md's STOP rule
  and the batch's own stop conditions:
  - "Stop and report if a required change would violate an existing frozen decision, require a save-format change,
    or materially expand scope";
  - item 4: "report the exact blocker";
  - item 6: "stop and provide a specific impact analysis and smallest safe implementation plan".
- Items 1, 2 and 5 of the same batch are implemented (D-053). This CR covers only the three items that were not.
- Every file:line was checked against `de86ace` by the agent raising it.

> **2026-10-15 — RESOLVED by the Director's 2026-10-14 instruction (`docs/d054-batch3-incremental-construction-allocation-cylinder.md`;
> contract `docs/adr/adr-039-schema-v33-orders-11-12-and-cylinder-topology.md`).** The analysis below is kept as
> written.
> - **§A (item 3): built** as incremental construction (D-054 §1). It is NOT §A.3 option 2's per-good
>   `ConstructionProgressRow(Settlement, Slot, Good, long Delivered)`. It is ONE lockstep progress per queue entry:
>   `ConstructionProgressRow(Settlement, Slot, ProjectId, EnqueuedTurn, Progress)`, schema v33 section 59. A cancel order
>   now exists, and still refunds nothing (a Director question, D-054 §10 Q1).
> - **§B (item 4): built** as the building/roads split (D-054 §2). The instruction asked the implementation to "Specify
>   and implement consistent semantics for both available supply and requested demand". The semantics specified are
>   guaranteed shares of supply, never amounts consumed, with the default open for confirmation (D-054 §10 Q2).
>   - **Renumbered against §B.3:** order kind **11** is `CancelConstruction` and kind **12** is
>     `SetResourceAllocation`, not `SetResourcePriority = 11`. Kind 10 stays reserved for MoveFormation.
>   - **The row** is `ResourceAllocationRow(PolityId Polity, int BuildingsPercent)` (a whole number, section 60), not
>     `ResourcePriorityRow(PolityId, double BuildingSharePercent)`.
> - **§C (item 6):**
>   - **Built:** W2, the true cylinder in worldgen and simulation, and W3, its rendering, both UNRESTRICTED.
>   - **Out of scope "for now", by the instruction:** W1 (known extent, explored-longitude gating, Satellites). Nothing
>     of W1 is built or faked.
>   - **C.5's rulings:** 1 (timing) was not ruled as asked: W1 was not built (out of scope "for now"), so D-052 §5's
>     visibility half stands. v33 did land in M5, but for WS1/WS2's tables (superseding D-052 §5's save-format half),
>     so W1's known extent takes v34 (D-054 §5 items 6 and 13). 2 (topology) is answered: a cylinder, amending ADR-032
>     FR3 and the x half of T3.1(b). 3 (what Satellites reveal) is NOT ruled: it is out of scope. *(Ruling 1 corrected
>     2026-10-15 by final-round fix 1, verifier REC-CR021-C5; it first read "1 (timing) is answered: v33 lands in M5,
>     superseding D-052 §5's save-format clause".)*

**Not a request to change the batch.** Each item is reported with its exact blocker and the smallest safe plan. No
partial or substitute version of any of them was built.

| Item | Blocker in one line | Smallest safe plan |
|---|---|---|
| 3 Incremental construction | M4-D CERTIFIED "built whole … no fractional progress"; progress needs a stored field (v33) | §A option 2 at the first schema bump, if still wanted after playtesting with the purse |
| 4 Editable buildings/roads ratio | needs a per-empire table (v33) + a new order kind (frozen kernel) + a ruled meaning | §B option 1 |
| 6 Exploration-gated east-west wrap | no explored-extent state (M6, v33); a cylinder reopens ADR-032 FR3 / D-015 / T3.1(b); render-only is ruled out | §C option 1 (W1 → W3, W2 ruled separately) |

---

## A. Item 3 — construction start and incremental resource consumption

### A.1 Frozen items in conflict
- **M4-D, CERTIFIED** (`docs/milestones.md:421`, `docs/current-state.md:482-487`): "only its head is eligible, and that
  head is either built whole this turn or waits unchanged. No build timer, no fractional progress, no construction
  currency, no second labour pool. … Materials leave settlement good stocks all-or-nothing under
  `ReasonIds.ConstructionMaterials`."
- **The row contract** (`Sim.Core/State/WorldState.cs:748-753`): `ConstructionQueueRow(SettlementId Settlement, int Slot,
  int ProjectId)` — "There is NO progress field here, deliberately: no percent, no turns remaining, no completion turn."
- **D-052 §5:** no save-format change in M5; `CanonicalSchema.Version` stays 32 (`Sim.Core/Kernel/CanonicalSchema.cs:155`).

### A.2 Evidence
- Partial progress is state that must survive a turn boundary and a save: "how much of this head's cost has already been
  delivered". The queue row has three fields and no other table is keyed by (settlement, slot) or can hold
  per-project delivered units. Persisting it means a new row field or a new table, so v33.
- Without persisted progress, the materials a project received at turn t are gone by t+1. The sim has nowhere to read
  them back, and a resumed session would not know them either.
- **Considered and rejected:** "work-in-progress" goods in `goods.json`, held in `GoodStocks`.
  - For: it avoids the version bump.
  - Against: it adds hidden goods to every settlement and changes every world hash. It still reverses M4-D. It also
    overloads a conserved table with a meaning that table was never given.
- **What this batch delivers toward item 3 without reopening anything:**
  - *No double allocation between projects and roads.* `construction` runs before `roaddevelopment` (pipeline.json lines
    12 and 24), and both read and draw the same turn-in-progress `GoodStocks`. A unit a project consumed can never also
    pay for a road in the same step (tested in item 1's track).
  - *Construction start.* With the empire purse (item 1), a head is built the first turn its empire holds the whole
    cost and the settlement has the labour. The waits measured in the Director's playtest session were material waits
    ("needs 20 stone (has N)"), and those are what the purse removes.

### A.3 Options (≤3)
1. **Keep M4-D for the M5 playtest (recommended).** Built whole, now against the empire purse. Measure in the next
   playtest whether waits remain.
2. **Incremental consumption at the first schema bump (M6):**
   - Append `ConstructionProgressRow(Settlement, Slot, Good, long Delivered)` (v33; empty when nothing is in
     progress).
   - Each turn the head draws up to a ruled per-turn delivery, through `Ledger.Flow` sinks under
     `ConstructionMaterials`. It completes when every input is delivered and the labour exists.
   - Rows are cleared on completion. No refunds, because no cancel order exists; a cancel order would need its own
     ruling.
3. **Labour-only progress** (materials whole, labour accrues): this needs the same progress field, so it has the same
   blast radius as option 2 and less benefit.

### A.4 Blast radius (option 2)
- CanonicalSchema: the width table, the anti-padding proof, and a POPULATED-table test.
- ConstructionSystem, ConstructionQuery (blocker text, `Producible`), AiConstructionPolicy, and the
  ActionSurface/InfoQuery cards (a progress display).
- An ADR that reverses M4-D.
- A re-pin of every AI-world golden. Calibration worlds are unaffected (they build nothing).
- It must be co-ordinated with the m5w plan's v33 reservation (`docs/m5w-map-workstream-audit-and-plan.md:192`).

### A.5 Recommendation
Option 1 now. Option 2 only if the next playtest still shows long waits with the purse in place, and then ruled
together with item 4 so that one schema bump carries both.

---

## B. Item 4 — player-editable allocation ratio between buildings and roads

### B.1 Frozen items in conflict
- **D-052 §5:** no M5 save-format change (as above).
- **The kernel is frozen after M0** (CLAUDE.md, "Workflow per session"). `OrderKind` lives in `Sim.Core/Kernel/OrderLog.cs`.
  - Kinds 1–9 are taken.
  - Kind 10 is earmarked for `MoveFormation` (M7; `docs/m5w-map-workstream-audit-and-plan.md:192`, D-052 §4).

### B.2 Evidence — the exact blocker
A ratio that is "player-editable and persisted end to end" needs two things.
1. **A stored per-empire value** that the systems read every turn and that a save and resume carry.
   - The only per-polity policy row is `TaxPolicyRow(PolityId, double Rate)` (`WorldState.cs:1022`).
   - The other per-polity rows hold research, Age, claims and control. None can carry a buildings/roads split
     without changing its meaning.
   - A new row type is a new table, so it is v33.
2. **An order that sets it**, so that replay reproduces it.
   - No existing kind can. `DevelopRoads` (kind 8) has a TargetId that is "reserved and must be 0" and an Amount that
     is the road percentage.
   - Reusing the reserved TargetId changes a kernel load-validation contract.
   - It would also last only the order's own turn. That is not a standing policy.
   - Construction would have to read another system's order kind.

There is also a **semantic gap that needs a ruling**, not just plumbing:
- Roads are a one-shot action: one `DevelopRoads` per empire per turn, applied in the step t→t+1.
- Construction runs every turn from the queue head.
- So a "ratio between buildings and roads" needs a defined meaning. One candidate: "of each good in the purse at turn
  start, construction may draw at most B %; the rest is held back for a road order issued that turn; anything not used
  stays in the purse". That is a reservation rule, and it changes nothing on turns with no road order.

**Delivered instead in this batch:** a fixed and documented priority. Construction draws first, in ascending
settlement-row order; roads see the remainder. No unit is spent twice. This is the existing pipeline order, now written
down and tested.

### B.3 Options (≤3)
1. **Rule the semantics now and build it at the first schema bump (recommended).**
   - Append `ResourcePriorityRow(PolityId Polity, double BuildingSharePercent)` (v33; empty means today's
     construction-first order).
   - Add `OrderKind.SetResourcePriority = 11`, following the `SetTaxRate` precedent:
     - load validation: Amount in [0,100], TargetId ≥ 0;
     - `OrderValidation`: actor = target;
     - delivery t→t+1.
   - ConstructionSystem caps its per-turn draw of each good at the share of the turn-start purse.
   - Add a slider in the empire policy panel. The AI issues none (it gets the default).
   - Update the ci.yml order-kind range and add a populated-table test.
2. **A UI-only preference:** rejected. It is not persisted in the simulation, and replay cannot see it.
3. **Encode the split in `DevelopRoads`' reserved TargetId:** no schema change, but it is a kernel-contract change. It
   lasts one turn and couples construction to another system's order. Rejected as inconsistent.

### B.4 Blast radius (option 1)
- CanonicalSchema, OrderLog and OrderValidation (kernel sign-off).
- The ci.yml cross-process kind range.
- ConstructionSystem and ConstructionQuery: the blocker text must say "held back for roads".
- A UI panel row.
- AI goldens move only if the AI is given a policy.

### B.5 Recommendation
Option 1, with the meaning ruled first: the reservation reading above, or another. Land it in the same v33 as A option
2 if that is also wanted.

---

## C. Item 6 — east-west wrap gated by explored longitudes, with Satellites revealing everything

The Director's instruction is to stop rather than ship a visual-only substitute if true cylindrical topology cannot be
implemented coherently without reopening frozen decisions. It cannot. This section is the required impact analysis
and the smallest safe plan. Every file:line below was checked against `de86ace`.

### C.1 Frozen items in conflict
1. **D-052 §5 (RATIFIED), on exploration state.**
   - "discovered-extent storage and visibility persistence (the m5w plan's S2a–S2c)" are M6 work.
   - "No visibility state and no save-format change ship in M5: `CanonicalSchema.Version` stays 32."
   - Gating by *explored* longitudes needs exactly that state.
2. **D-040 B1 / B5 (ratified design).** Knowledge is "a **computed extent**, not a visibility flag", and its storage
   is "NOT DESIGNED HERE": it is assigned to the S2a packet. `Sim.Core/State/PolityKnowledge.cs:13-19` confirms the
   tree is "ALL-KNOWN TODAY. No known-extent state exists".
3. **ADR-032 §12.3, Final Ruling 3 (ACCEPTED, LOCKED).**
   - The road length invariant is "the straight-line (flat-raster Euclidean) distance … The map is a flat square raster
     (no wrap), so this is the existing geometry" (`docs/adr/adr-032-transport-inter-city-road-foundation.md:435-437`).
   - This is the repository's only explicit no-wrap statement, and it is locked.
4. **D-015 (CLOSED):** "a 4,096 km square world" (`docs/m1-walking-skeleton-spec.md:8`).
5. **D-009:** one Europe-class landmass in open sea.
6. **T3.1(b) edge taper (closed in the queue):** "no land touches any world edge", pinned by
   `WorldgenRefreshTests.NoLandAtTheWorldEdge_TenSeeds`.
7. **D-039 B3 (closes D-014):** "RECONNAISSANCE IS POINTED, NOT GLOBAL". "Satellites revealing everything" has to be
   reconciled with it.
   - The content already anticipates two different reveals:
     - `research.json` `satellite`: "orbital reconnaissance (D-039 B: pointed becomes GLOBAL)";
     - `gps`: "D-040 B1 extent: known everywhere".
   - Both are knowledge-only today (class C in `docs/research-gameplay-unlock-audit.md:510,545`).

### C.2 Evidence
**(a) There is no exploration state to gate on.**
- `WorldState` has no explored, visible, known or fog table.
- `PolityKnowledge` is a read seam whose only future input is "S2a's STORED KNOWN EXTENT".
- `Sim.Ui/ViewModel/PlayerKnowledge.cs` is a per-turn cache of that seam, "never a second source of truth".
- So "explored longitudes" cannot be computed today. Faking it in the UI would create the second source of truth that
  the seam forbids.

**(b) The world model is flat in every layer.** Code that would change under a wrap:

| Layer | Code that assumes a non-wrapping x axis |
|---|---|
| Pathing | `Pathfinder.cs:144, 293, 377` edge clamps; `:345` adjacency `Math.Abs(ax-bx) <= 1`; `:406-410` A* heuristic on raw dx; `:509-514` `MinPerUnit`; `LatticeMap.cs:216` origin search; `TraversalLattice.cs:69-71, 173-178` |
| Distances | `GeographicDistance.cs:28-29` (also the weather kernel, `HarvestWeatherSystem.cs:278`); `RoadDevelopmentQuery.cs:201-207` (`GeographicKm`, FR3), `:219-223` (`SquaredPx`), `:370-388` (nearest-first purse order, now also used by this batch's empire stockpile); `SiteAssessment.cs:543-551, 661, 701-748` (knowledge reach shapes) |
| Siting and catchment | `SettlementSiting.cs:111-137` (edge-clamped integral image), `:268-291`; `WorldFounding.cs:377-394`; `CatchmentSystem.cs:125, 162-175` (`SettlementDistances`: migration, trade, governance reach and road candidacy all read it) |
| Worldgen | `Worldgen.cs:65-67` x edge taper (`edgeTaperPx` 24); `:215-256` value noise is not periodic in x; `:48-52` radial continental mask; `Hydrology.cs:10-14, 85-90, 143-144` (border sinks are terminal river mouths) |
| Rendering (Sim.Ui) | `Camera.cs:83-85, 250-258` (contain fit, x clamp); `SimUiGame.cs:325-337` (one terrain texture, `LinearClamp`; one river buffer); `WorldLens.cs:183, 199, 716, 751` (one copy of each settlement and route); `MapCells.cs:22-27`; `SettlementSelection.cs:71-80`; `ResourceOverlay.cs:222-223, 295-298`; `TerrainBaker`; `ParchmentBaker` (bake not x-periodic; measured seam ΔRGB 20.2 against 13.3–13.9 for neighbouring columns) |

**(c) A lattice-only wrap would be a sham.**
- The taper forces an ocean ring, and water nodes are impassable on the traversal lattice
  (`TraversalLattice.cs:142`, `passable = waterSum/cells < 0.5`).
- Wrapping neighbour enumeration alone therefore connects only ocean to ocean: no route, catchment, road or colony
  changes.
- For land to meet across the seam, worldgen must change: remove the x taper, make the noise periodic, and re-shape
  the radial mask. That changes the terrain `ContentHash` and every world.

**(d) A render-only wrap is the substitute the Director ruled out.** Measured in the playtest investigation, VERIFIED:
- At today's zoom floor the map rect spans 1.86–2.15 world widths, so the player would see two copies of the only
  continent, about 96–130 screen px apart.
- Routes, roads and the AI would still treat the seam as the world's edge.
- Painting three copies costs 2.17–2.29× the commands at the contain floor.

**(e) Pins that move under a true topology change.**
- **World-hash pins:** about 60 hex constants in 7 classes:
  - `IntegratedPinAttributionTests` (52)
  - `DrivenGoldenTests`
  - `SnapshotTests` (plus `ci.yml` `FOUNDED_GOLDEN`)
  - `FirstReignTests`
  - `AiFoundingAttributionTests`
  - `SettlementCompositionTests`
  - `FoundingVariationPinTests`
- **Behavioural pins of flat geometry:**
  - `NoLandAtTheWorldEdge_TenSeeds`
  - `Rivers_FlowMonotonicallyDownhill_ToSeaOrDocumentedSink`
  - `AStar_EqualsDijkstra_AllPairs_OnHandcraftedLattice`
  - `Pan_IsClampedToWorldBounds`
  - `MapRect_Clamp_…`
  - `CameraFocus_…OrClampedAtTheWorldsEdge`
  - the ADR-032 FR3 geographic-length tests
- **Calibration:** the calibration worlds are regenerated too. The frozen CR-020 references would have to be
  re-measured, so this is a calibration event, not a refactor.

### C.3 Options (≤3)
1. **Staged and ruled (recommended): W1 → W2 → W3, each its own packet with an attribution switch.**
   - **W1 — known extent (the m5w plan's S2a; M6 per D-052 §5; schema v33).**
     - Per-polity known extent at lattice-node granularity: the 16 km `Grid` already in `PolityKnowledge`.
     - Explored-longitude coverage is a pure derived query: the lattice columns holding any known node. It is never
       stored.
     - "Satellites reveal everything" is a computed rule: a polity that has completed the ruled node knows every node.
       It is applied in the extent system, not in the UI.
   - **W2 — cylindrical topology (needs a CR ruling amending ADR-032 FR3 and recording a D-015 / T3.1(b)
     amendment).**
     - Modulo-x neighbours in `Pathfinder`/`TraversalLattice`/`LatticeMap`, with a wrapped-dx heuristic so A* stays
       admissible.
     - One shared `WrappedDx` used by `GeographicDistance`, `GeographicKm`, `SquaredPx`, `SiteAssessment`'s reach
       shapes, siting `BoxSum` and `HinterlandMeans`.
     - Periodic x noise, a poles-only taper, and hydrology border sinks on the poles only.
     - Every golden re-pinned once with the attribution switch. The calibration references are re-measured under a
       Director ruling.
   - **W3 — rendering.**
     - One circumference drawn around the camera (no world point painted twice). The x pan is unclamped only when the
       player's explored coverage spans 360°; otherwise the pan is clamped to the explored longitude band.
     - The camera floor is keyed on world geometry, so the map fills the screen.
     - A periodic parchment bake or a seam cross-fade.
     - Picking and focus go to the nearest copy.
     - Screen-space furniture (overlay key, legend) is painted once.
2. **W1 and W3 without W2 (fog-gated camera on the flat world).** No topology change, but no wrap either. It meets
   "fill the screen" (camera floor) and "explored-longitude gating" (pan clamped to coverage) at M6, and is honest
   because nothing pretends the seam is crossable.
3. **Render-only wrap now.** Rejected by the Director's own instruction (C.2 d).

### C.4 Separation of rendering from the world model
- **World model (Sim.Core):** the extent state and its system (W1); topology, distances and worldgen (W2). These
  change saves, replays and goldens.
- **Rendering (Sim.Ui):** the camera clamp/floor, copy selection, picking, the bake seam and per-copy accounting
  (W3). These change no world hash. W3 reads coverage only through the `PolityKnowledge` → `PlayerKnowledge` seam.

### C.5 Rulings needed
1. **Timing.** Whether W1 stays in M6 (D-052 §5), or the Director amends §5 so that v33 lands in M5.
2. **Topology.** Whether the world becomes a cylinder (W2). That amends ADR-032 FR3 and records a D-015 / T3.1(b)
   amendment. If not, option 2.
3. **What Satellites reveal.** The Director's item 6 names Satellites: "upon completing Satellites, instantly reveal
   the entire map and enable unrestricted east-west wraparound". Confirm three readings:
   - "the entire map" means the geography, the known extent of D-040. Live reconnaissance of units stays pointed
     (D-039 B3).
   - The content's `gps` capability text ("D-040 B1 extent: known everywhere") moves to `satellite`.
   - A research completion is computed state, so this reveal is not a Law-4 calendar gate (D-040 B3 objected to a node
     *opening sea travel*, not to a node raising knowledge).

### C.6 Recommendation
- Rule 1 and 3 now.
- Build W1 at M6's first schema bump, and W3 against it.
- Decide W2 separately: it is a calibration event with the largest blast radius in this batch, and on today's
  ocean-ringed single continent it buys little gameplay. The seam is open sea either way.

### C.7 The Director's item-6 tests, by the stage that can carry them

| Test asked for | Stage | Where it would live |
|---|---|---|
| deterministic coverage evaluation; partial exploration leaving a gap; coverage across the seam | W1 | Sim.Tests: pure coverage query over the stored extent (tie-free: per-column boolean) |
| previously explored but currently hidden terrain counts | W1 | Sim.Tests: extent is stored and monotone; visibility is derived and never stored (D-040 B1) |
| Satellites instantly reveal all geography and enable wrapping | W1 + W3 | Sim.Tests (extent = all on completion, turn-exact delivery pin); Sim.Ui.Tests (wrap unlocked that frame) |
| bounded scrolling before full coverage; wrap only after all longitudes | W3 | Sim.Ui.Tests: camera clamp keyed on coverage |
| no information leakage (seam, duplicate terrain, tooltips, labels, minimap) | W3 | Sim.Ui.Tests: KnowledgeLeakTests metamorphic cases, extended per copy |
| no duplicate cities; coordinate and hit-testing at the seam | W3 (+ W2 for sim coordinates) | Sim.Ui.Tests: one painted copy per settlement, nearest-copy picking |
| bounded vertical movement | W3 | Sim.Ui.Tests: y clamp unchanged |

None of these can be written truthfully today: there is no stored extent to cover, so every one would be a test of a stub.
