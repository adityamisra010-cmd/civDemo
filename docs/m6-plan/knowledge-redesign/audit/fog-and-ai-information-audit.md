# Fog of war, resource discovery and AI information — code-grounded audit

**Status: READ-ONLY AUDIT, 2026-10-22. Input to the M6 knowledge redesign; designs nothing.**
- Tree: `/home/user/m5w/dev`, branch `claude/new-session-yavghm`, HEAD `edba2b5` (docs-only commits on top of
  `main` = `m5-exit` = `6e51a3e`; both verified with `git rev-parse` at the start of this audit). LOCAL / REMOTE / MAIN
  are not collapsed: everything below is read from this working tree.
- No file under `Sim.*` was changed. Nothing was built or run. "MEASURED" means read directly from code, tests or
  content at this tree (including grep counts); it never means an execution measurement by this audit. Execution
  figures quoted from records are attributed to the record.
- Paths are repository-relative. Every claim cites `path:line`. Code beats documents; every disagreement found is listed
  in §12.
- Labels: **MEASURED** (read from code/tests/content) · **INFERENCE** (reasoned from measured facts) ·
  **OPEN QUESTION** (needs a ruling or a measurement).
- `docs/m6-spec.md` is a DRAFT, NOT RATIFIED (`docs/m6-spec.md:3-4`); its packets T6.7–T6.9 are PROPOSED.

---

## 0. Headline findings

1. **There is no fog state of any kind.** No known extent, visible set, sighting memory, contact table or
   explored-longitude state exists in `WorldState` or the canonical stream (sections end at 61, `ConstructionHolds`,
   `Sim.Core/Kernel/CanonicalSchema.cs:971-979`; grep for `KnownExtent|sighting|Fog|DiscoverySystem|VisibleNodes` finds
   only comments). `PolityKnowledge.Of` is all-known (`Sim.Core/State/PolityKnowledge.cs:81-85`). MEASURED.
2. **Deposits are per SETTLEMENT, not per place.** A `DepositRow(Settlement, Good, Abundance)` is written once at
   founding or colonization and is immutable (`Sim.Core/State/WorldState.cs:270-279`). The abundance is a terrain
   channel averaged over a 50 km land disc times a spread keyed to the settlement id
   (`Sim.Core/Worldgen/WorldFounding.cs:565-580`). Nine deposit goods collapse onto three channels (moisture,
   elevation, water). MEASURED.
3. **No uranium, coal, oil, iron, salt or other strategic good exists.** The goods roster is 14 goods
   (`Sim.Data/content/goods.json:4-101`); "uranium" occurs in no code or content file. Those resources appear only as
   research-node names and capability strings. MEASURED.
4. **Extraction is ungated.** Every deposit with abundance > 0 is worked by Herding/fishing or Extraction labour from
   turn 1 (`Sim.Core/Systems/Production/ProductionSystem.cs:51-60, 369-415`). Research gates only processing (recipes)
   and labels (`activity.mining`). No research reveals or unlocks a raw resource (`Sim.Core/State/InfoQuery.cs:1530`).
   MEASURED.
5. **One information seam exists and is respected by the player UI.** `PolityKnowledge` (Sim.Core) and
   `PlayerKnowledge` (Sim.Ui) answer `Known/Visible/KnowsSettlement/KnowsUnit/KnowsPolity/ReadsInternals` at the 16 km
   lattice-node grain. Only 2 of the 6 AI policies consult it (`AiFoundingPolicy`, `AiConstructionPolicy`). MEASURED.
6. **AI leak matrix: 53 read sites across the 6 policies and the producer.** 38 read only the polity's own data,
   3 are seam calls, **11 are latent leaks** (correct today, leak once fog exists — all in founding and roads), and
   **1 is active today**: AI polities in one producer call see each other's same-turn founding orders
   (`Sim.Core/Systems/Colonization/AiFoundingPolicy.cs:50-55, 146-157`). Seven further SYSTEMS (not policies) make
   per-polity decisions from global truth (emergent colony siting, migration, trade, path targets, road plans,
   raiding, the founding predicate). §5.
7. **The pathfinder is a deterministic, cache-free, lane-aware Dijkstra/A\***, wrap-aware, with a composite
   (key, node id) heap order. `Isochrone` never enters water nodes and travels along every road and path of every polity,
   so as a sight model it would leave coasts and seas unknown, see along roads, and invert D-039 B4 (high ground costs
   MORE to traverse). Each call allocates and fills about 0.85 MB of 65,536-entry arrays whatever the budget. §4.
8. **Decisions.** RATIFIED: D-039 (stale ghost; scouts B6 open), D-040 (map discovered, monotone, no tech unlock),
   D-052 §5 (fog to M6), ADR-037 (the seam), D-048 (knowledge monotone), D-060 (rival books closed; session files and
   F12 stay world-wide), the D-055 note (v35 reserved to fog). OPEN: D-K (storage/sight), D-J (scouts), D-T (AI
   assumption about unseen land), D-H (schema sequencing), DW-002 (which world a fogged order is judged on), DW-006
   (trade mutually-known). §10.

---

## 1. Map model

### 1.1 Geometry, topology, lattice

| # | Item | Value | Evidence | Label |
|---|---|---|---|---|
| 1.1.1 | Terrain raster | 1024 × 1024 px at 4.0 km/px (a 4,096 km square world) | `Sim.Data/content/worldgen.json:2-3` | MEASURED |
| 1.1.2 | Unit of "where" | Continuous fields + vector network + settlements/catchments; "NO CELLS, ANYWHERE THE EYE OR THE DESIGN CAN SEE" | `docs/d009-d010-map-population-addendum.md:5-13` | MEASURED (frozen record) |
| 1.1.3 | Topology | East-west CYLINDER (`wrapX: true`); x periodic, y keeps hard edges (poles are ocean) | `Sim.Data/content/worldgen.json:45-48`; `Sim.Core/Worldgen/TerrainSet.cs:20-32`; `Sim.Core/Worldgen/Worldgen.cs:22-33` | MEASURED |
| 1.1.4 | Wrap arithmetic | `MapTopology.NeighbourX/Dx/DxOnMap/SquaredPx/WrapCoord`; minimum wrapped size 3 | `Sim.Core/Pathing/MapTopology.cs:28-131` | MEASURED |
| 1.1.5 | Traversal lattice | Stride 4 → 256 × 256 = **65,536 nodes** at 1024 px; "an UNDER-THE-HOOD grid ... never a sim unit of place" | `Sim.Core/Pathing/TraversalLattice.cs:5-10, 101-104, 128`; `Sim.Core/Pathing/Pathfinder.cs:23-24` | MEASURED |
| 1.1.6 | Node size | 16 km per node side (KmPerPx × stride), 256 km² per node | `Sim.Core/Pathing/TraversalLattice.cs:162`; `Sim.Core/Pathing/LatticeGeometry.cs:32-37, 51-53` | MEASURED |
| 1.1.7 | Cost unit | 1 cost unit = 16 km of ideal ground; ≈ 14.4 km on real ground (median node cost ≈ 1.11) | `Sim.Core/Pathing/LatticeGeometry.cs:39-47, 67-68` | MEASURED |
| 1.1.8 | Passability | A node is impassable iff its block's water fraction is ≥ 0.5; rivers never change passability | `Sim.Core/Pathing/TraversalLattice.cs:14-15, 159` | MEASURED |
| 1.1.9 | Node cost | Block-mean movement cost, blended with the river factor along river cells (span-weighted) | `Sim.Core/Pathing/TraversalLattice.cs:17-47, 153-158`; `Sim.Data/content/sim.json:27-28` (riverCostFactor 0.2) | MEASURED |
| 1.1.10 | Knowledge grid | The lattice grid itself: "a cell is known iff the stride block that contains it is" | `Sim.Core/State/PolityKnowledge.cs:21-22, 105-108`; `Sim.Core/Pathing/MapTopology.cs:190-202` | MEASURED |
| 1.1.11 | Discovery granularity (ruled) | D-040 B5 left storage and granularity undesigned and noted the 16 km node collision ("a river is a sub-node feature") | `docs/d040-discovery-and-control.md:82-88` | MEASURED (record) |

### 1.2 Terrain, rivers, water

| # | Item | Value | Evidence | Label |
|---|---|---|---|---|
| 1.2.1 | Layers | elevation, water (1 = water), temperature °C, moisture [0,1], fertility [0,1], movement cost, rivers (1 on river cells) + river polylines | `Sim.Core/Worldgen/TerrainSet.cs:34-41` | MEASURED |
| 1.2.2 | Biome | No biome concept in simulation or UI code (grep `biome` in `Sim.Core`, `Sim.Ui`: 0 hits). The UI's nine terrain "classes" are visual blend weights only | `Sim.Ui/Art/TerrainSplat.cs:5-33` | MEASURED |
| 1.2.3 | Movement field | baseCost 1.0, slopeFactor 60, waterCost 25 | `Sim.Data/content/worldgen.json:32-36` | MEASURED |
| 1.2.4 | Rivers | 12 rivers; D8 routing over a priority-flood copy; deterministic orderings | `Sim.Data/content/worldgen.json:37-44`; `Sim.Core/Worldgen/Hydrology.cs:3-29` | MEASURED |
| 1.2.5 | Sea level / coasts | Sea level from an exact land-fraction quantile (target 0.35); coast is the water raster boundary | `Sim.Core/Worldgen/Worldgen.cs:16-18`; `Sim.Data/content/worldgen.json:4-6` | MEASURED |
| 1.2.6 | Landmasses | One centred continent: radial mask + noise ("the continent sits in open sea") | `Sim.Core/Worldgen/Worldgen.cs:72-80`; `docs/d054-batch3-incremental-construction-allocation-cylinder.md:585-586` | MEASURED |
| 1.2.7 | Sea travel | None: "no vessel, port or sea route exists"; D-040 B4 makes sea travel a network edge type, still OPEN | `Sim.Data/content/research.json:104-108`; `docs/d040-discovery-and-control.md:75-80` | MEASURED |
| 1.2.8 | Discoverable "far lands" | With one landmass and impassable water, every land node a polity can reach is on its own component | INFERENCE from 1.1.8 and 1.2.6 | INFERENCE |

### 1.3 Objects on the map

| # | Item | Value | Evidence | Label |
|---|---|---|---|---|
| 1.3.1 | Settlement | `SettlementRow(Id, SiteCell, FoundedTurn)`; the site is a 4 km terrain cell | `Sim.Core/State/WorldState.cs:63-64` | MEASURED |
| 1.3.2 | Founding | 12 sites, minimum spacing 95.2 km travel cost, chosen by a global argmax over every cell | `Sim.Data/content/worldgen.json:49-57`; `Sim.Core/Worldgen/SettlementSiting.cs:187-247` | MEASURED |
| 1.3.3 | Who holds what at turn 0 | Settlements are dealt ROUND-ROBIN to the player and the AI Empires; each Empire's capital is its first settlement | `Sim.Core/Worldgen/WorldFounding.cs:347-381` | MEASURED |
| 1.3.4 | Formations | `MilitaryUnitRow(..., Location, X, Y, ...)` with continuous px position; one per Empire at its capital; no system moves them | `Sim.Core/State/WorldState.cs:996-1011`; `Sim.Core/Worldgen/WorldFounding.cs:262-296`; `Sim.Ui/ViewModel/UnitCardModel.cs:40-45` | MEASURED |
| 1.3.5 | Catchment | Travel-cost PARTITION of the lattice (lanes included), budget = 50 km ideal ground × size bonus; recomputed only on D-016 events | `Sim.Core/Systems/Catchment/CatchmentSystem.cs:13-26, 53-72, 100-149`; `Sim.Data/content/sim.json:16-18` | MEASURED |
| 1.3.6 | Pairwise distances | `SettlementDistances`: ALL ordered pairs, travel cost over lattice + every lane, +∞ when unreachable | `Sim.Core/Systems/Catchment/CatchmentSystem.cs:151-182` | MEASURED |
| 1.3.7 | Network | `NetworkNodeRow/NetworkEdgeRow` (dirt paths; no owner field), `TransportEdgeRow` roads ("A road has no owner") | `Sim.Core/State/WorldState.cs:49-61`; `Sim.Core/State/RoadDevelopmentQuery.cs:77-81` | MEASURED |

### 1.4 Stored versus derived

| Item | Stored in `WorldState` / canonical stream? | Evidence | Label |
|---|---|---|---|
| Terrain rasters | Referenced, NOT cloned, NOT serialized; only the SHA-256 content hash enters the stream | `Sim.Core/Worldgen/TerrainSet.cs:5-13, 122-161`; `Sim.Core/State/WorldState.cs:1288, 1726`; `Sim.Core/Kernel/CanonicalSchema.cs:294-299`; `Sim.Core/Kernel/Snapshot.cs:48-55` | MEASURED |
| Traversal lattice | Derived per consumer call ("not state, not a cache") | `Sim.Core/Pathing/TraversalLattice.cs:12-15`; `Sim.Core/Systems/Catchment/CatchmentSystem.cs:35-36, 102` | MEASURED |
| Frontier siting raster (scores, floor, lattice) | Derived; cached per system instance, keyed on the terrain reference | `Sim.Core/Systems/Colonization/ColonizationSystem.cs:134-167`; `Sim.Core/Worldgen/SettlementSiting.cs:341-374` | MEASURED |
| Catchments, distances | Stored (CatchmentSystem's owned tables), recomputed on events | `Sim.Core/Systems/Catchment/CatchmentSystem.cs:8-11, 100-106` | MEASURED |
| Deposits | Stored, immutable after founding | `Sim.Core/State/WorldState.cs:270-279` | MEASURED |
| Control / claim / recognition | `Controls` stored and written; `Claims` and `Recognitions` schema-only (no writer except deserialization) | `Sim.Core/State/WorldState.cs:581-619, 670-680`; grep `Claims.Add|Recognitions.Add`: only `Sim.Core/Kernel/CanonicalSchema.cs:1350, 1366` | MEASURED |
| Known extent, visible set, sighting memory, contact | Not present | §9 | MEASURED |

### 1.5 Worldgen determinism

- Hash-based value-noise fBm from SplitMix64 over (seed, salt, cell); no RNG stream state
  (`Sim.Core/Worldgen/Worldgen.cs:5-14, 266-301`). MEASURED.
- Pinned by twin tests, e.g. `HydrologyTests.TwinGeneration_ByteIdentical_IncludingRiverData`
  (`Sim.Tests/Worldgen/HydrologyTests.cs:62`) and `WorldgenRefreshTests.Founding_IsTwinIdentical`
  (`Sim.Tests/Worldgen/WorldgenRefreshTests.cs:139`). MEASURED.
- Founding endowments and deposit spreads use stateless SplitMix hashes keyed by (seed, slot)
  (`Sim.Core/Worldgen/WorldFounding.cs:546-551, 576`). A deterministic sight flood needs no RNG stream
  (`docs/m6-foundations-audit.md:77`, D9). MEASURED (code) / RECORDED (audit).

### 1.6 Seams noted (not designs)
- INFERENCE: three granularities meet at a site. The site is a 4 km cell, knowledge is a 16 km node, deposits read a
  12.5 px (50 km) Euclidean land disc (`Sim.Core/Worldgen/WorldFounding.cs:450-457`), and the catchment is a
  travel-cost partition (1.3.5). A known-extent bitset is 65,536 bits = 8 KiB per polity (`docs/m6-spec.md:128`).

---

## 2. Resources and deposits

### 2.1 How a deposit physically exists

| # | Fact | Evidence | Label |
|---|---|---|---|
| 2.1.1 | Row shape: `DepositRow(SettlementId, GoodId, double Abundance)` — a rate multiplier, NOT a conserved stock; "immutable after founding (depletion is a later milestone if the director wants it)" | `Sim.Core/State/WorldState.cs:270-279` | MEASURED |
| 2.1.2 | Written at turn-zero founding per settlement, in goods-registry order | `Sim.Core/Worldgen/WorldFounding.cs:224-254` | MEASURED |
| 2.1.3 | Written for every colony by the same function (`AddDepositsForSite`), variation slot = the new settlement's id | `Sim.Core/Systems/Colonization/ColonizationSystem.cs:344-346`; `Sim.Core/Worldgen/WorldFounding.cs:553-580` | MEASURED |
| 2.1.4 | Abundance = max(0, channel × (1 + spread × U(seed, slot, 300 + goodId))) | `Sim.Core/Worldgen/WorldFounding.cs:572-579, 478-480` | MEASURED |
| 2.1.5 | Channel = hinterland MEAN over LAND cells of a disc of radius hinterlandRadiusKm / kmPerPx = 12.5 px; three channels only: "moisture" → mean moisture, "elevation" → mean height above sea, anything else ("water") → mean moisture SQUARED | `Sim.Core/Worldgen/WorldFounding.cs:384-476` | MEASURED |
| 2.1.6 | So the spread is keyed to the SETTLEMENT ID, not to the place: the same cell gives a different roll to a different would-be settlement | `Sim.Core/State/SiteAssessment.cs:22-27`; `Sim.Core/State/InfoQuery.cs:1462-1465` | MEASURED |
| 2.1.7 | No per-place (per-cell or per-node) deposit presence exists; "The nine deposit goods collapse onto three channels" | `Sim.Core/State/SiteAssessment.cs:50-52`; `Sim.Core/State/InfoQuery.cs:1529` | MEASURED |
| 2.1.8 | Not depletable: no system writes a `DepositRow` after founding (writers are `WorldFounding`, `ColonizationSystem`, `CanonicalSchema` read) | grep `Deposits` in `Sim.Core` (`Sim.Core/Worldgen/WorldFounding.cs:253`; `Sim.Core/Systems/Colonization/ColonizationSystem.cs:344`; `Sim.Core/Kernel/CanonicalSchema.cs:1197`) | MEASURED |
| 2.1.9 | D-051 §2 (RATIFIED, deferred): availability classes, a common-material floor, regionally correlated richness ("Rare and strategic goods stay rare"); scheduled to M6, coordinated with fog and the P9/P10 overlays | `docs/d051-m5-polish-rulings.md:33-50`; `docs/m5-exit-inventory.md:398-402` | MEASURED (record) |

### 2.2 Resource and deposit kinds in content

The roster is 14 goods (`Sim.Data/content/goods.json:3-101`). Deposit-bearing goods:

| Good (id) | Category | Channel | Spread | Worked by | Evidence |
|---|---|---|---|---|---|
| livestock (2) | food | moisture | 0 | Herding/fishing | `Sim.Data/content/goods.json:13-18`; `Sim.Core/State/LabourActivities.cs:257-261` |
| fish (3) | food | water | 0 | Herding/fishing | `Sim.Data/content/goods.json:20-25` |
| timber (4) | raw | moisture | 0 | Extraction | `Sim.Data/content/goods.json:27-32` |
| stone (5) | raw | elevation | 0 | Extraction | `Sim.Data/content/goods.json:34-39` |
| clay (6) | raw | water | 0 | Extraction | `Sim.Data/content/goods.json:41-46` |
| copper-ore (7) | raw | elevation | 0.8 | Extraction | `Sim.Data/content/goods.json:48-54` |
| tin-ore (8) | raw | elevation | 0.9 | Extraction | `Sim.Data/content/goods.json:56-62` |
| fiber (9) | raw | moisture | 0 | Extraction | `Sim.Data/content/goods.json:64-69` |
| hides (10) | raw | moisture | 0 | Extraction | `Sim.Data/content/goods.json:71-76` |

Non-deposit goods: grain (1, numeraire), bronze (11), tools (12), pottery (13), cloth (14)
(`Sim.Data/content/goods.json:5-11, 77-100`). MEASURED.

**Strategic resources (MEASURED, by grep over `Sim.Data/content/*.json` and all `Sim.*` code):**
- **uranium: absent** from every code and content file. Tracked documents say so: "There is no uranium good and no
  electricity system" (`docs/design/research-capability-ownership.md:1133`); the corpus audit marks `nuclear_fission`'s
  circumstance "Uranium deposit" as having no state carrier (`docs/research-corpus-audit-d045.md:1475`).
  `nuclear_fission` unlocks only capability strings ("nuclear weapon (D-011 deterrence)", "reactor")
  (`Sim.Data/content/research.json:21123-21157`). The frozen Spine rescopes the nuclear layer to
  "deterrence-abstraction only" (`docs/civ-sim-architecture-v3-outline.md:154`).
- coal, oil/petroleum, iron, salt/saltpetre, gold, silver, sulphur, aluminium, horses: present only as research node
  ids, names, prose or unit-family flavour (e.g. `iron_bloomery`, `petroleum_refining`, `gold_silver_native`,
  `salt_extraction` in `Sim.Data/content/research.json`). **None is a good, none has a deposit.**
- copper and tin are the only ores; both ride the elevation channel with independent per-settlement spreads
  (`Sim.Data/content/goods.json:48-62`). INFERENCE: copper and tin are therefore positively correlated by geography
  (same channel) and differ only by the spread draw.

### 2.3 How extraction reads deposits

| # | Fact | Evidence | Label |
|---|---|---|---|
| 2.3.1 | Herding/fishing and Extraction share one shape: the sector's labour pool splits across the settlement's deposit goods ∝ abundance; rate = workers × perWorker × abundance × weather × extraction multiplier, × dt | `Sim.Core/Systems/Production/ProductionSystem.cs:51-60, 363-415` | MEASURED |
| 2.3.2 | No deposit row ⇒ nothing produced ("never from nothing") | `Sim.Core/Systems/Production/ProductionSystem.cs:385` | MEASURED |
| 2.3.3 | Extraction does not read the catchment; only Farming does (arable km² from `CatchmentSummaries`) | `Sim.Core/Systems/Production/ProductionSystem.cs:255-258, 369-415` | MEASURED |
| 2.3.4 | No sector is gated: "no sector is gated: research changes the ACTIVITY a sector expresses" | `Sim.Data/content/research.json:69-72`; `docs/adr/adr-033-m5-integration-pass.md:21-40` | MEASURED |
| 2.3.5 | `activity.mining` (requires `mining_shaft`) and `activity.logging` "join" the Extraction identity — a LABEL, not a gate | `Sim.Data/content/research.json:164-183, 5809-5845, 29345-29356`; `Sim.Core/State/LabourActivities.cs:28-37, 65-74, 207-253` | MEASURED |
| 2.3.6 | The single exception that changes output: Farming harvests wild food until `activity.farming` is eligible (pre-cultivation) | `Sim.Core/State/LabourActivities.cs:103-113` | MEASURED |

### 2.4 Does any research node or capability gate seeing or using a deposit?

- **Seeing: no.** No research or capability reveals a resource; everything is known (§3). The cell card says
  "Resource discovery: No research reveals or unlocks a raw resource, and production works deposits with no research
  gate" (`Sim.Core/State/InfoQuery.cs:1527-1531`). MEASURED.
- **Extracting: no** (2.3.4, 2.3.5). MEASURED.
- **Processing: yes, by recipe.** `recipe.bronze_casting` requires `tin_bronze`; `recipe.pottery_firing` requires
  `pottery_open_fired`; weaving and toolmaking are baseline (`Sim.Data/content/research.json:29401-29425`), plus the
  D-020 predicate `artisan_share > 0.05` on bronze-casting and toolmaking (`Sim.Data/content/goods.json:137-180`);
  `CraftingQuery.IsRecipeAvailable` = knowledge AND predicate (`Sim.Core/State/CraftingQuery.cs:14-18, 50-58`). MEASURED.
- `baseline.exploration` ("Scouting and exploration") is declared NOT simulated: "no mobile agents exist (D-043)"
  (`Sim.Data/content/research.json:56-60`). MEASURED.
- Exploration-flavoured nodes carry only capability strings, e.g. `cartography` "D-040 B1 computed extent made
  explicit" (`Sim.Data/content/research.json:11080-11099`), `telescope` "naval reconnaissance (D-039 B)"
  (`:15369-15387`), aerial reconnaissance (`:19700`), `satellite` "orbital reconnaissance (D-039 B: pointed becomes
  GLOBAL)" (`:20316-20334`). Capability strings are read only for display (`Sim.Core/State/ResearchQuery.cs:615-620`;
  `Sim.Core/State/InfoQuery.cs:415`). MEASURED. D-040 B3 forbids a tech unlock of reach ("a calendar gate wearing a
  tree", `docs/d040-discovery-and-control.md:59-69`); D-054's instruction puts "Satellites research revealing the map"
  explicitly out of scope "for now" (`docs/d054-batch3-incremental-construction-allocation-cylinder.md:124-126`). MEASURED.

### 2.5 SiteAssessment (P8) and its information class

| # | Fact | Evidence | Label |
|---|---|---|---|
| 2.5.1 | A pure query for the player's place card (`InfoKind.Cell`), outside the pipeline | `Sim.Core/State/SiteAssessment.cs:9-32` | MEASURED |
| 2.5.2 | Information class = the PLAYER's stricter class (ADR-037 §7): an unknown cell is REFUSED and nothing of it is read | `Sim.Core/State/SiteAssessment.cs:34-48, 382-384` | MEASURED |
| 2.5.3 | For a known cell, unless the viewer sees the whole world (`SeesAll`), each value whose reach touches unknown ground is withheld as "not judged", never estimated: score factors, floor, rank, spacing, catchment, route, hinterland and deposits, occupancy and claim unless a known settlement decides them | `Sim.Core/State/SiteAssessment.cs:393-511`; `docs/adr/adr-037-player-knowledge-seam.md:174-185` | MEASURED |
| 2.5.4 | `SeesAll` = all-known, or every lattice node visible — always true in production | `Sim.Core/State/SiteAssessment.cs:683-696` | MEASURED |
| 2.5.5 | Deposits: the expected channel value and its ±spread range are shown when the disc is known; the EXACT next roll only when the viewer sees all, because "the next settlement id counts every settlement in the world" | `Sim.Core/State/SiteAssessment.cs:467-511` | MEASURED |
| 2.5.6 | Reach helpers `KnowsSquare/KnowsDiamond/KnowsDisc` mirror the fertility box, the water BFS and the deposit disc (wrap-aware) | `Sim.Core/State/SiteAssessment.cs:698-769` | MEASURED |
| 2.5.7 | The card already splits "Discovered, present, reachable, workable" (the first is always true today) | `Sim.Core/State/InfoQuery.cs:1472-1490` | MEASURED |

### 2.6 P9 resource overlay: masking by knowledge

- Per lattice node, the value a settlement founded at the node's centre cell would be judged by: one layer per deposit
  CHANNEL (no layer per good), plus the siting score and its two factors (`Sim.Ui/World/ResourceOverlay.cs:13-32,
  69-83`). MEASURED.
- An unknown node is not evaluated; a known node whose reach touches undiscovered ground is withheld; the colour ramp
  runs to the largest value DRAWN; region fertility is summed over its own window under a restricted view
  (`Sim.Ui/World/ResourceOverlay.cs:85-95, 344-399`). MEASURED.
- Accessibility is the union of unbounded `Pathfinder.Isochrone` floods from the player's sites, only while `SeesAll`
  (`Sim.Ui/World/ResourceOverlay.cs:42-50, 497-507`). MEASURED.
- Realized ore rolls are shown only for settlements the player knows and controls (`ReadsInternals`)
  (`Sim.Ui/World/ResourceOverlay.cs:538-552`). MEASURED.
- `Qualifies(layer, node)` requires the node known (`Sim.Ui/World/ResourceOverlay.cs:658`). MEASURED.

### 2.7 P10 "now usable" shortcut

- Fires on a REAL eligibility transition of the player's own knowledge (a recipe whose inputs are deposit goods, or a
  sector identity) that gains a settlement the player holds; UI-only history, rebuilt by replay
  (`Sim.Ui/ViewModel/DiscoveryShortcuts.cs:15-20, 67-120`). MEASURED.
- D-052 §8 closed it as built: highlights known deposits only, never reveals unknown tiles; limitation: with no
  per-place deposit presence it outlines every known place whose channel value is > 0 — "the known landmass, not
  deposit locations" (`docs/d052-m5-integration-audit-rulings.md:100-106`). MEASURED (record).

### 2.8 The resource-discovery chain against today's code

| Stage | What exists today | Evidence | Gap a design must fill | Label |
|---|---|---|---|---|
| 1. Deposit exists | Only as a per-SETTLEMENT row at founding; elsewhere only as a terrain-derived expectation (channel of hinterland means) | `Sim.Core/Worldgen/WorldFounding.cs:384-476, 565-580` | A per-place truth that exists before anyone settles (D-051 §2's regional richness is the ratified hook) | MEASURED / INFERENCE |
| 2. Location discovered | Nothing: all-known | `Sim.Core/State/PolityKnowledge.cs:13-19, 81-85` | Known extent (v35 reserved) | MEASURED |
| 3. Resource identified | Not simulated | `Sim.Core/State/InfoQuery.cs:1530` | Per-polity identification knowledge | MEASURED |
| 4. Prospected | UI previews only: expected value ± spread (P8), channel heatmap (P9); exact roll only while seeing all | `Sim.Core/State/SiteAssessment.cs:467-511`; `Sim.Ui/World/ResourceOverlay.cs:69-95` | There is no place-level truth to prospect: the roll is keyed to the settlement id (2.1.6) | MEASURED / INFERENCE |
| 5. Extractable | Every deposit > 0 from turn 1, no gate | `Sim.Core/Systems/Production/ProductionSystem.cs:369-415` | — | MEASURED |
| 6. Processable | Recipes gated by research entity + artisan predicate | `Sim.Core/State/CraftingQuery.cs:50-58`; `Sim.Data/content/research.json:29401-29425` | — | MEASURED |
| 7. Industrial use | Projects consume timber, stone, tools; capability strings ("reactor") are knowledge-only | `Sim.Data/content/goods.json:182-320`; `Sim.Core/State/ResearchQuery.cs:615-620` | No goods beyond the 14; no electricity/fuel system | MEASURED |

---

## 3. Knowledge seams

"Knowledge" has TWO unrelated meanings in the tree (INFERENCE, MEASURED by the types below): (a) **research
knowledge** — completed nodes (`ResearchCompletedRow`, `SettlementKnowledge`, `KnowledgeTransfer`); (b) **information
class** — what a polity may see (`PolityKnowledge`, `PlayerKnowledge`). A fog design must keep them apart.

| Seam | What it exposes | To whom | Fog status | Evidence | Label |
|---|---|---|---|---|---|
| `PolityKnowledge` (Sim.Core) | `Known(node)`, `Visible(node)` (Visible ⊆ Known), `KnowsCell`, `SeesCell`, `KnowsSettlement` (= controls it OR its site is visible), `KnowsUnit` (= owns it OR its position/station is visible), `KnowsPolity` (= itself OR owns a settlement it knows: "contact"), `ReadsInternals` (= controls the settlement; every settlement in a world with no Controls) | AI policies (2 of 6), InfoQuery, SiteAssessment, Sim.Ui | `_known` / `_visible` null ⇒ every answer "yes"; `Of` is THE production entry; its "ONLY future input is S2a's STORED KNOWN EXTENT"; the visible set is to be derived each turn from PREV, never stored | `Sim.Core/State/PolityKnowledge.cs:5-48, 54, 81-85, 137-196` | MEASURED |
| `PolityKnowledge.Restricted` | A stub from explicit node masks (Visible ⊆ Known enforced) | Tests and the headless gate only (source-guarded) | Test seam | `Sim.Core/State/PolityKnowledge.cs:87-103`; `Sim.Ui.Tests/KnowledgeLeakTests.cs:1378-1394` | MEASURED |
| `PlayerKnowledge` (Sim.Ui) | Per-turn cached view for the player's Empire: `Known/Visible/KnowsSettlement/IsHiddenSettlement/ReadsInternals/KnowsUnit/KnowsPolity/KnowsCell`; `ForDeveloper()` opens every KNOWN settlement's books | Every player-facing reader | All-known; rebuilt after every step | `Sim.Ui/ViewModel/PlayerKnowledge.cs:5-149`; `Sim.Ui/UiSession.cs:256-276, 696` | MEASURED |
| `KnowledgeStubs` (Sim.Ui/Headless) | `HidePolity` hides a polity whole (its site blocks, catchment nodes, formation nodes) | Leak tests, the "hidden rival" gate state | Harness | `Sim.Ui/Headless/KnowledgeStubs.cs:6-67`; `Sim.Ui/Headless/PlayabilityGate.cs:149-165` | MEASURED |
| `SettlementKnowledge` | RESEARCH mask of a settlement: controller's completed nodes ∪ the settlement's own (local holder −1 − id) | Recipes, labour labels, the trade gate, the pre-cultivation yield | Not an information seam; local holders exist only for uncontrolled settlements | `Sim.Core/State/SettlementKnowledge.cs:5-63` | MEASURED |
| `KnowledgeTransfer.MergeInto` | Union of completed nodes; never removes a row | Revolt (and any future annexation) | Monotone research knowledge | `Sim.Core/State/KnowledgeTransfer.cs:4-45` | MEASURED |
| `InfoQuery.Card` (cards) | Optional `knowledge` parameter (null = `PolityKnowledge.Of`); formation card reads as "nothing" for an unknown formation; cell card refuses unknown cells; founding card shows party/provisions only where `ReadsInternals` | Player cards (`InfoPanel`) | Seam-routed | `Sim.Core/State/InfoQuery.cs:195-213, 1299-1305, 1355-1535, 1553-1568` | MEASURED |
| `SiteAssessment` | §2.5 | Place card, overlay, placement mode | Withholding rules live, inert in production | `Sim.Core/State/SiteAssessment.cs:34-48` | MEASURED |
| `ResourceOverlay` (P9) / `DiscoveryShortcuts` (P10) | §2.6, §2.7 | Player overlays | Knowledge-masked, inert in production | as cited | MEASURED |
| `FoundingQuery` known-extent arm | "inert until a discovered-extent state exists"; its place is between arms 2 and 3 | Player and AI founding orders, ColonizationSystem intake | Stub | `Sim.Core/State/FoundingQuery.cs:69-71, 175` | MEASURED |
| `PlacementMode` | Unknown cells not hoverable/selectable; under a partial view the refusal REASON is withheld, but the verdict bit is not | Player placement | One-bit channel recorded for the fog packets | `Sim.Ui/ViewModel/PlacementMode.cs:15-24`; `docs/queue.md:1473` | MEASURED |
| ADR-037 | The one-seam decision, the player reader table, the AI rules, the metamorphic gate, the known open channels | — | RATIFIED 2026-10-12 | `docs/adr/adr-037-player-knowledge-seam.md:3-16, 33-162` | MEASURED (record) |

What is stubbed for fog (MEASURED): `PolityKnowledge.Of` all-known (`Sim.Core/State/PolityKnowledge.cs:81-85`); the
inert founding arm (`Sim.Core/State/FoundingQuery.cs:175`); `SiteAssessment.SeesAll` true
(`Sim.Core/State/SiteAssessment.cs:689-696`); the cell card's "Every place is known until a discovered extent is
stored" (`Sim.Core/State/InfoQuery.cs:1475-1477`); HUD totals defined over known/own settlements "the fog ruling may
change what a world total means" (`Sim.Ui/ViewModel/HudModel.cs:56-60`); no `MapLayer.Fog` member
(`Sim.Ui/World/MapLayers.cs:5-22`).

---

## 4. Pathfinder

### 4.1 Functions

| Function | Algorithm | Lanes? | Callers relevant to fog | Evidence | Label |
|---|---|---|---|---|---|
| `FindPath` | A\* with an admissible, consistent straight-line heuristic (cylinder distance), optimal | yes | Founding predicate arm 7; PathBuild routing; RouteQuery | `Sim.Core/Pathing/Pathfinder.cs:65-159, 560-564`; `Sim.Core/State/FoundingQuery.cs:194-195`; `Sim.Core/Systems/PathBuild/PathBuildSystem.cs:245-247` | MEASURED |
| `Isochrone` | Dijkstra truncated at a budget; reached set contiguous and monotone in budget; returns Reached (ascending id), Costs, Boundary | yes | `AiFoundingPolicy.ReachableNodes` (budget MaxValue); UI accessibility (budget +∞) | `Sim.Core/Pathing/Pathfinder.cs:161-217`; `Sim.Core/Systems/Colonization/AiFoundingPolicy.cs:333-340`; `Sim.Ui/World/ResourceOverlay.cs:497-507` | MEASURED |
| `Partition` | One capped Dijkstra PER SOURCE merged by (cost, settlement index); per-source budgets | yes | Catchments | `Sim.Core/Pathing/Pathfinder.cs:219-322`; `Sim.Core/Systems/Catchment/CatchmentSystem.cs:125` | MEASURED |
| `RelaxCappedFrom` | Capped Dijkstra over RAW terrain (no lanes) into a min array | no | Spacing exclusion (founding, siting) | `Sim.Core/Pathing/Pathfinder.cs:324-368`; `Sim.Core/Worldgen/SettlementSiting.cs:382-392, 514-518` | MEASURED |
| `DistancesTo` | Uncapped Dijkstra stopped once every target is settled | yes | All-pairs `SettlementDistances` | `Sim.Core/Pathing/Pathfinder.cs:431-509`; `Sim.Core/Systems/Catchment/CatchmentSystem.cs:151-182` | MEASURED |
| `LegCosts` / `RouteQuery.Find` | Per-leg costs that sum exactly to A\*'s total; budget prefix inclusive | yes | Route previews | `Sim.Core/Pathing/Pathfinder.cs:370-429`; `Sim.Core/State/RouteQuery.cs:5-71` | MEASURED |

### 4.2 Cost model
- Lattice step a→b = mean(nodeCost[a], nodeCost[b]) × (1 or √2) (`Sim.Core/Pathing/TraversalLattice.cs:187-213`);
  node cost from movement cost and rivers (1.1.9). Impassable (water) nodes are never entered
  (`Sim.Core/Pathing/Pathfinder.cs:129, 523, 533`). MEASURED.
- Lanes are built per call (`BuildOverlay`): every PathBuild `NetworkEdge` (both directions) and every TRAVELLED road
  (`TransportEdges`) between the two settlements' origin nodes, costed by `RoadPerformance.TravelCostUnits`
  (`Sim.Core/Pathing/Pathfinder.cs:575-687`). No lane has an owner (1.3.7). MEASURED.

### 4.3 Determinism
- Heap order is the composite (key, node id), never insertion order (`Sim.Core/Pathing/Pathfinder.cs:13-16,
  718-771`, `Less` at `:762-764`); fixed neighbour order W, E, N, S, NW, NE, SW, SE (`:61-63`); strictly-better
  relaxation (`:538-549`); lane order fixed by table row order (`:589-596`); Isochrone's Reached is in ascending id
  (`:198-199`). MEASURED.
- Bit-identity of the batch-4 speed-ups is pinned against the batch-3 code kept verbatim in Sim.Tests
  (`LegacyPathfinderDifferentialTests`, e.g. `Sim.Tests/Pathing/Legacy/LegacyPathfinderDifferentialTests.cs:119`)
  (`Sim.Core/Pathing/Pathfinder.cs:27-43`). MEASURED.
- `Isochrone_Deterministic_Contiguous_Monotone` (`Sim.Tests/Pathing/PathfindingTests.cs:243-283`) and the seam test
  `IsochroneBoundary_SeesTheUnreachedNeighbourAcrossTheSeam` (`Sim.Tests/Pathing/SeamPathfindingTests.cs:190`). MEASURED.

### 4.4 Caching invariant
- "PURITY MANDATE (T1.3): ... NO persistent caches, NO memoization ... defer any caching until a profiling gate
  demands it, and then only via ADR" (`Sim.Core/Pathing/Pathfinder.cs:8-11`); the same for the lattice
  (`Sim.Core/Pathing/TraversalLattice.cs:12-15`) and `LatticeMap` (`Sim.Core/Pathing/LatticeMap.cs:5-8`). MEASURED.
- D-055's pooled scratch arrays (O7) and a lattice per terrain (O9) "are state and would each need an ADR"; R5 recorded
  them NOT IMPLEMENTED (`docs/d055-batch4-reservations-attribution-allocation-ci-persistence-pathfinding.md:936-937,
  1355-1357`). MEASURED (record).
- A reuse WITHIN one call is allowed (PathBuild's per-step component reuse, O2: "a local of the call")
  (`docs/d055-batch4-reservations-attribution-allocation-ci-persistence-pathfinding.md:926`). MEASURED (record).

### 4.5 Performance evidence (no isochrone-specific bench exists)
- The only isochrone timing in code is a sanity test with generous bounds (< 2,000 ms for a full isochrone and a
  cross-continent path at 1024 px); it prints, it does not record (`Sim.Tests/Pathing/PathfindingTests.cs:324-354`).
  MEASURED. OPEN QUESTION: no recorded per-isochrone time exists in the docs (grep `full isochrone` finds no figure).
- `sim bench` (D-055, seed 42, 60 turns, Release, shared 4-CPU host): total 11,843 ms (opt) per seed-42 run; catchment
  phase 3,700–4,240 ms clean and 4,851–5,595 ms with the early exit disabled; 57 catchment recomputes and 684 full
  Dijkstras per run; PathBuild's A\* settles 10,054,809 nodes over 60 turns on seed 42; colonization allocates
  ≈ 499.9 MB per run in `RelaxCappedFrom`'s per-call arrays
  (`docs/d055-batch4-reservations-attribution-allocation-ci-persistence-pathfinding.md:906-912, 956-979`). RECORDED.
- INFERENCE from those figures: a full-lattice uncapped Dijkstra costs single-digit milliseconds on the bench host
  (≤ ≈ 8 ms: 5,595 ms / 684, an upper bound because the phase also builds the lattice and the partition).
- INFERENCE from code: every `Isochrone` call allocates and fills `g` (65,536 doubles), `closed` (65,536 bools) and a
  per-call lane overlay (`offsets` of 65,537 ints, plus lanes) — about 0.85 MB — and invokes a push delegate per
  relaxation, whatever the budget (`Sim.Core/Pathing/Pathfinder.cs:170-196, 513-536, 598-687`). Batch 4 inlined
  this only for `FindPath` (`:32-34`).
- The fog performance risk is registered as R-07 (`docs/m6-plan/m6-risk-register.md:15`). MEASURED (record).

### 4.6 Reachability queries used today

| Consumer | Query | Evidence | Label |
|---|---|---|---|
| Found Settlement legality (player, AI, ColonizationSystem intake) | `FindPath` from the source's origin node to the site's (binary connectivity, no budget) | `Sim.Core/State/FoundingQuery.cs:65-67, 194-195` | MEASURED |
| AI founding pre-filter | Uncapped `Isochrone` over the whole component | `Sim.Core/Systems/Colonization/AiFoundingPolicy.cs:333-340` | MEASURED |
| Emergent colonization | NONE: "it is not a reachability rule (an unreachable cell keeps +∞ spacing and passes rule 4)" | `Sim.Core/Worldgen/SettlementSiting.cs:461-464` | MEASURED |
| Trade, migration, road candidates, governance reach | `SettlementDistances` (finite = reachable) | `Sim.Core/Systems/Trade/TradeArbitrageSystem.cs:136-137, 248-256`; `Sim.Core/Systems/Migration/MigrationSystem.cs:404-414`; `Sim.Core/State/RoadDevelopmentQuery.cs:179-191`; `Sim.Core/State/Governance.cs:94-115` | MEASURED |
| PathBuild targets | Passable component flood, then A\* toward the most fertile unreached node | `Sim.Core/Systems/PathBuild/PathBuildSystem.cs:311-339` | MEASURED |
| UI accessibility | Union of unbounded isochrones from own sites (only while `SeesAll`) | `Sim.Ui/World/ResourceOverlay.cs:497-507` | MEASURED |

### 4.7 Suitability of `Isochrone` as a sight model (INFERENCE unless marked)
- **For:** deterministic and total-ordered (4.3); monotone in budget (MEASURED test, 4.3); wrap-aware; stated in cost
  units the spec already proposes for a sight budget (`docs/m6-spec.md:127`); `Partition` shows the multi-source,
  per-budget pattern.
- **Against:**
  1. Water nodes are never reached, and `Boundary` lists only PASSABLE unreached neighbours
     (`Sim.Core/Pathing/Pathfinder.cs:201-212`): seas, lakes and coastal blocks with ≥ 50 % water would never become
     known through it.
  2. It travels along every lane of every polity (4.2): sight would extend along foreign roads and paths.
  3. Movement cost is not line of sight: high, rough ground costs MORE (slopeFactor 60), so an isochrone sees LESS from
     a mountain — the inverse of D-039 B4 "a vantage point extends effective reconnaissance range"
     (`docs/d039-command-fog-and-siege.md:66-70`). The m5w plan's own applied correction says "sight must not be
     modelled as a movement-cost isochrone (D-039 B4 vantage)" (`docs/m5w-map-workstream-audit-and-plan.md:5, 980`)
     — yet the M6 spec and docket D-K propose `Pathfinder.Isochrone` sight (`docs/m6-spec.md:127, 233-235`;
     `docs/m6-plan/m6-ruling-docket.md:37`). Recorded in §12.
  4. Cost per call (4.5) times sources per turn (settlements grow from 12 toward D-040's 457-slot capacity,
     `docs/d040-discovery-and-control.md:15-19`), and ADR-037's "visible set derived each turn from PREV, never
     stored" (`docs/adr/adr-037-player-knowledge-seam.md:39-41`) means every `PolityKnowledge.Of` call (9 production
     call sites: `Sim.Core/State/InfoQuery.cs:213, 327, 335`; `Sim.Core/Systems/Colonization/AiFoundingPolicy.cs:194,
     296`; `Sim.Core/Systems/Construction/AiConstructionPolicy.cs:108, 250`; `Sim.Ui/ViewModel/PlayerKnowledge.cs:83`;
     plus the stub's geometry-only `Sim.Ui/Headless/KnowledgeStubs.cs:32`) would re-derive sight — against the
     no-cache mandate (4.4). OPEN QUESTION: where the visible set is computed once per step without becoming a cache
     or a second source of truth.

---

## 5. AI information reads — the leak matrix

### 5.1 Inventory of AI decision code (MEASURED)

| Policy | Order kind | Entry | Uses `PolityKnowledge`? | Evidence |
|---|---|---|---|---|
| Producer `AiOrders.For` | — | per AI polity in roster order, then governance | n/a | `Sim.Core/AiOrders.cs:14-77, 104-114` |
| `AiResearchPolicy` | 6 SetResearchTarget | `Decide` | no (reads `IReadOnlyWorldState`) | `Sim.Core/Systems/Research/AiResearchPolicy.cs:48-60` |
| `AgeAdvancePolicy` | 7 AdvanceAge | `Decide` | no | `Sim.Core/Systems/Ages/AgeAdvancePolicy.cs:23-30` |
| `RoadDevelopmentPolicy` | 8 DevelopRoads | `Decide` | no | `Sim.Core/Systems/Roads/RoadDevelopmentPolicy.cs:31-45` |
| `AiConstructionPolicy` | 4 EnqueueConstruction, 11 Cancel | `Decide`, `University`, `Site` | yes (`ReadsInternals`) | `Sim.Core/Systems/Construction/AiConstructionPolicy.cs:81-146, 180-271` |
| `AiFoundingPolicy` | 9 FoundSettlement | `Decide`, `BestSiteFor` | yes (`KnowsCell`) | `Sim.Core/Systems/Colonization/AiFoundingPolicy.cs:57-81, 198-340` |
| `AiGovernance` | 5 SetTaxRate | `OrdersFor` | no | `Sim.Core/Systems/Governance/AiGovernance.cs:38-51` |

There is **no military, threat, diplomacy, migration or trade AI** (grep `threat|hostil|enemy` in `Sim.Core` code: 0
hits; military is a standing capability with no order, `Sim.Core/State/AvailableActionsQuery.cs:549-551`). Migration,
colonization from below, trade and path building are SYSTEMS (§5.3). The producer runs between turns in the UI
(`Sim.Ui/UiSession.cs:636`) and the CLI (`Sim.Cli/Program.cs:386`). MEASURED.

### 5.2 The matrix (one row per read site)

Leak classes: **none** = reads only the polity's own data (or producer bookkeeping); **gated** = a call into the
knowledge seam; **latent** = reads global truth that is legitimate while everything is known but leaks once fog
exists; **active** = an information asymmetry present today, independent of fog.

| # | Policy | Read site | Data read | Decision affected | Via seam? | Leak class | Polity-scoped query it should use (INFERENCE) |
|---|---|---|---|---|---|---|---|
| P1 | Producer | `Sim.Core/AiOrders.cs:60-66` | `Polities` roster; `EmpireQuery.IsExtinct` (controls count) | which polities decide | no | none | — |
| P2 | Producer/Founding | `Sim.Core/AiOrders.cs:59, 73`; `Sim.Core/Systems/Colonization/AiFoundingPolicy.cs:146-157` | the shared `Sites` batch, grown by EARLIER AI polities' same-turn Found Settlement orders; player orders are not visible to the AI (`:53-55`) | founding site (spacing/occupancy) | no | **active** (AI polity B sees AI polity A's unrevealed intent; neither the player nor A has the symmetric view) | a per-issuer batch: standing settlements the issuer knows + the issuer's own queued orders (DW-002's ruling) |
| R1 | Research | `Sim.Core/Systems/Research/AiResearchPolicy.cs:51` | own `PolityRow` command source | gate | no | none | — |
| R2 | Research | `AiResearchPolicy.cs:52` | own `ResearchCompleted` (`Sim.Core/State/ResearchQuery.cs:30-41`) | availability | no | none | — |
| R3 | Research | `AiResearchPolicy.cs:55` | own `ResearchTargets` | keep target | no | none | — |
| R4 | Research | `AiResearchPolicy.cs:94, 246` | own `ResearchCostModifiers` via `EffectiveCost` (`Sim.Core/State/ResearchQuery.cs:279-321`) | goal and node ranking | no | none | — |
| R5 | Research | `AiResearchPolicy.cs:146` | own completed mask via `RoadDevelopmentQuery.BestKnownClass` (`Sim.Core/State/RoadDevelopmentQuery.cs:110-122`) | road goal | no | none | — |
| R6 | Research | `AiResearchPolicy.cs:157` | own completed (`Governance.KnowsTaxation`) | tax goal | no | none | — |
| R7 | Research | `AiResearchPolicy.cs:174` | own completed (`ConstructionQuery.IsKnowledgeEligible`) | university goal | no | none | — |
| R8 | Research | `AiResearchPolicy.cs:216, 220` | own `AgeStates`; milestone facts over own-controlled settlements, own formations, trade flows with an own endpoint (`Sim.Core/State/AgeQuery.cs:172-280`) | core-Age goal | no | none | — |
| A1 | Age | `Sim.Core/Systems/Ages/AgeAdvancePolicy.cs:25` | own command source | gate | no | none | — |
| A2 | Age | `AgeAdvancePolicy.cs:26` | own `AgeStates` (`AgeQuery.NextAge`) | target Age | no | none | — |
| A3 | Age | `AgeAdvancePolicy.cs:28` | own eligibility (`Sim.Core/State/AgeQuery.cs:291-301`) | advance now | no | none | — |
| D1 | Roads | `Sim.Core/Systems/Roads/RoadDevelopmentPolicy.cs:34` | own command source | gate | no | none | — |
| D2 | Roads | `RoadDevelopmentPolicy.cs:36` → `Sim.Core/State/RoadDevelopmentQuery.cs:179-191` | `SettlementDistances` for every pair with ≥ 1 own endpoint: the OTHER endpoint may be any settlement, known or not (existence + travel cost) | candidate routes | no | **latent** (ADR-037 §6 bullet 2) | routes whose other endpoint `PolityKnowledge.KnowsSettlement` |
| D3 | Roads | `RoadDevelopmentPolicy.cs:36` → `RoadDevelopmentQuery.cs:187-189, 213-220` | both endpoints' SITE CELLS (straight-line km) and the 600 km cap | candidacy, length, cost | no | **latent** | the same known-endpoint filter |
| D4 | Roads | `RoadDevelopmentPolicy.cs:36` → `RoadDevelopmentQuery.cs:238-276` | `TransportEdges` rows on own–foreign pairs (class, modernization) | route status, start fraction | no | **latent** | the same known-endpoint filter |
| D5 | Roads | `RoadDevelopmentPolicy.cs:36-38` → `RoadDevelopmentQuery.cs:165-176` | every `TradeFlows` row scanned; used only for pairs with an own endpoint | ranking; 50 % vs 25 % | no | none (own trade) | — |
| D6 | Roads | `RoadDevelopmentPolicy.cs:40` → `RoadDevelopmentQuery.cs:642-658` | `Plan` = D2–D4 again + material costs | the plan | no | **latent** | as D2 |
| D7 | Roads | `RoadDevelopmentPolicy.cs:42` | own settlements (`PayingSettlements`, `RoadDevelopmentQuery.cs:359-372`) | payers | no | none | — |
| D8 | Roads | `RoadDevelopmentPolicy.cs:43` | own `GoodStocks` (`AffordableFraction`) | affordability | no | none | — |
| C1 | Construction | `Sim.Core/Systems/Construction/AiConstructionPolicy.cs:104` | own command source | gate | no | none | — |
| C2 | Construction | `AiConstructionPolicy.cs:108, 250` | `PolityKnowledge.Of(world, polity)` | — | **yes** | gated | — |
| C3 | Construction | `AiConstructionPolicy.cs:117-118` | own queue/progress/labour (`ConstructionQuery.StructuralStall`) after `ReadsInternals` | cancel stalled head | yes (`ReadsInternals` first) | none | — |
| C4 | Construction | `AiConstructionPolicy.cs:127-128, 254-256` | own `ConstructionQueue` after `ReadsInternals` | idle queue | yes | none | — |
| C5 | Construction | `AiConstructionPolicy.cs:130` | own adults (`InstitutionStaffing.LabourAdults`) | skip unstaffed | yes | none | — |
| C6 | Construction | `AiConstructionPolicy.cs:131` | the purse = own Empire's settlements (`Sim.Core/State/ConstructionQuery.cs:533-541`) | reservation | no | none | — |
| C7 | Construction | `AiConstructionPolicy.cs:135` | own `Structures` (`ConstructionQuery.Built`) | build once | no | none | — |
| C8 | Construction | `AiConstructionPolicy.cs:136, 257` | control + own knowledge + purse (`Sim.Core/State/ConstructionQuery.cs:136-149`) | availability | no | none | — |
| C9 | Construction | `AiConstructionPolicy.cs:137, 260` → `:164` | `GoodStocks`, `ConstructionQueue`, `ConstructionProgress` of the own purse | affordability after claims | no | none | — |
| C10 | Construction | `AiConstructionPolicy.cs:206, 221-238` | every `Institutions` row scanned, filtered by owner (`:224-225`); own settlements control-first (`:229`) | university specialty | no | none | — |
| C11 | Construction | `AiConstructionPolicy.cs:209` | own institutions' maturity (`InstitutionEffects.MaturityWeighted`) | specialty tie-break | no | none | — |
| C12 | Construction | `AiConstructionPolicy.cs:258` | own settlement viability (`InstitutionViability.ToFound`) | university site | yes | none | — |
| C13 | Construction | `AiConstructionPolicy.cs:261` | own construction capacity | university site | yes | none | — |
| F1 | Founding | `Sim.Core/Systems/Colonization/AiFoundingPolicy.cs:203-204` | config switch; own command source | gate | no | none | — |
| F2 | Founding | `AiFoundingPolicy.cs:205, 228-240` | `Settlements` rows, control-first; own `FoundedTurn` | interval | no | none | — |
| F3 | Founding | `AiFoundingPolicy.cs:207, 248-268, 365-371` | own `Buckets`, `FoodState.Of`, `FoundingQuery.Departure` on own stocks | source ranking | no | none | — |
| F4 | Founding | `AiFoundingPolicy.cs:209` (frontier from `Sim.Core/State/FoundingQuery.cs:108-111`; `Sim.Core/Worldgen/SettlementSiting.cs:358-374`) | the WHOLE-MAP siting raster: `CandidateScores` = region fertility (±3 px box) × water access (whole-map BFS, cutoff 24 px); `Floor` = median of every positive cell | site valuation and floor | no | **latent** — the D7 channel, quarantined (`Sim.Tests/State/PolityKnowledgeTests.cs:310-366`) | a valuation over known cells only plus the D-T rule for unseen land |
| F5 | Founding | `AiFoundingPolicy.cs:159-167` (used at `:317`) | every settlement's site cell (occupancy) and the spacing exclusion seeded from all of them | candidate filter (unobstructed) | no | **latent** (an unseen settlement's existence and position shape the pick) | standing settlements the issuer knows (DW-002) |
| F6 | Founding | `AiFoundingPolicy.cs:168-173` (used at `:319`) | every `CatchmentNodes` row (claimed mask) | candidate filter (unclaimed) | no | **latent** | catchments of settlements the issuer knows |
| F7 | Founding | `AiFoundingPolicy.cs:307-309` | the source's own site cell | origin | no | none | — |
| F8 | Founding | `AiFoundingPolicy.cs:312, 333-340` (used at `:319`) | uncapped `Pathfinder.Isochrone` over the whole lattice + every lane of every polity | candidate filter (reachable) | no | **latent** ("the reachability flood still crosses the issuer's whole connected component", `:77-80`) | a flood masked to known passable nodes (D-T) |
| F9 | Founding | `AiFoundingPolicy.cs:315` | `knowledge.KnowsCell(cell)` | first pre-filter arm | **yes** | gated | — |
| F10 | Founding | `AiFoundingPolicy.cs:316` | `IsFrontierCandidate`: water at the (known) cell, `Scores[cell]`, `Floor` | candidate filter | partly | **latent** (score and floor integrate unknown terrain) | as F4 |
| F11 | Founding | `AiFoundingPolicy.cs:321, 324, 350-363` | ranking over `f.Scores` | the pick | no | **latent** (moves between KNOWN cells with unknown terrain; ADR-037 §6 measured 36463 → 42559, `docs/adr/adr-037-player-knowledge-seam.md:142-146`) | as F4 |
| F12 | Founding | `AiFoundingPolicy.cs:322, 326-327` → `Sim.Core/State/FoundingQuery.cs:131-197, 222-227` | the predicate: every settlement (occupancy/spacing), `Controls`, the frontier, every `CatchmentNodes` row, the source's books, `FindPath` over the whole lattice + lanes | legality of each candidate | no | **latent** (DW-002; the same bit the placement mode leaks, `docs/queue.md:1473`) | the predicate restricted to what the issuer knows, with the intake applying the same restriction (a ruling) |
| F13 | Founding | `AiFoundingPolicy.cs:194, 296` | `PolityKnowledge.Of` default | — | **yes** | gated | — |
| F14 | Founding | `AiFoundingPolicy.cs:217` | `world.Clock.Turn` (order stamp) | stamp | no | none | — |
| G1 | Governance | `Sim.Core/Systems/Governance/AiGovernance.cs:41` | own command source | gate | no | none | — |
| G2 | Governance | `AiGovernance.cs:42` | own controls count (`IsExtinct`) | gate | no | none | — |
| G3 | Governance | `AiGovernance.cs:43` | own knowledge, Age and seat (`Sim.Core/State/Governance.cs:251-302`) | can levy | no | none | — |
| G4 | Governance | `AiGovernance.cs:45` | own `TaxPolicies` | current rate | no | none | — |
| G5 | Governance | `AiGovernance.cs:46` | own settlements' happiness (`Sim.Core/State/Governance.cs:209-230`; `Sim.Core/State/SettlementHappiness.cs:123-157`) | rate step | no | none | — |

**Counts (MEASURED from the table): 53 read sites — 38 none, 3 gated, 11 latent (D2, D3, D4, D6, F4, F5, F6, F8, F10,
F11, F12), 1 active (P2).** Every latent site is in founding or roads. No AI policy reads a rival's BOOKS (people,
stocks, queues, deposits) — that is pinned (§5.5).

### 5.3 Systems (not policies) that decide for a polity from global truth

| # | System | Site | Global data used | What it decides | Leak class | Evidence | Label |
|---|---|---|---|---|---|---|---|
| N1 | ColonizationSystem (emergent, D-037 B1) | `Sim.Core/Systems/Colonization/ColonizationSystem.cs:224-225` | `ChooseFrontierSite`: argmax over EVERY cell of the map, independent of the source's position; no reachability rule | where a polity's colonists settle (the colony inherits the parent's control, `:335-338`) | latent (D-040 B1; "Emergent B1 siting remains omniscient unless the Director rules otherwise", `docs/m5w-map-workstream-audit-and-plan.md:780`) | `Sim.Core/Worldgen/SettlementSiting.cs:394-447, 461-464` | MEASURED |
| N2 | ColonizationSystem order intake | `ColonizationSystem.cs:272-276` | `FoundingQuery.Check` on PREV with every standing site | whether a player/AI order founds | latent (DW-002) | `Sim.Core/State/FoundingQuery.cs:73-79` | MEASURED |
| N3 | MigrationSystem | `Sim.Core/Systems/Migration/MigrationSystem.cs:404-414` | `SettlementDistances` to every settlement; reads no `Controls` (grep `Controls|EmpireQuery|Polity` in the migration files: 0 hits) | where a polity's people go | latent / OPEN (D-040 B1 names settle, claim, trade — not migration; `docs/m6-spec.md:153-157` expects it to move) | as cited | MEASURED |
| N4 | TradeArbitrageSystem | `Sim.Core/Systems/Trade/TradeArbitrageSystem.cs:124-137, 248-264` | every connected pair where both ends know trade; both ends' prices | flows between settlements of any polities | latent (D-040 B1 "trade ... only within known extent"; DW-006) | `Sim.Core/State/TradeQuery.cs:26-28` | MEASURED |
| N5 | PathBuildSystem | `Sim.Core/Systems/PathBuild/PathBuildSystem.cs:206-221, 311-339` | the most fertile catchment-unreached node of the frontier's whole passable component | where each settlement's dirt path heads (drawn on the map) | latent (D-055 measured all 12 seed-42 settlements routing to ONE shared target, `docs/d055-batch4-reservations-attribution-allocation-ci-persistence-pathfinding.md:906-910`) | as cited | MEASURED |
| N6 | RoadDevelopmentSystem | applies `RoadDevelopmentQuery.Plan` (`Sim.Core/State/RoadDevelopmentQuery.cs:71-75`) | as D2–D4 | which roads are modernized | latent | as cited | MEASURED |
| N7 | AppropriationSystem | `Sim.Core/Systems/Appropriation/AppropriationSystem.cs:156, 238-268` | the settlement holding the most grain ANYWHERE ("Wealth, not distance") | whom a stateless raider robs | latent (dormant: no founded world produces a stateless settlement, INFERENCE from writers — `Sim.Core/Worldgen/WorldFounding.cs:359-366`; `Sim.Core/Systems/Colonization/ColonizationSystem.cs:335-338`; `Sim.Core/Systems/Revolt/RevoltSystem.cs:181-191`) | `AppropriationSystem.cs:78-106` | MEASURED / INFERENCE |
| N8 | CatchmentSystem | `Sim.Core/Systems/Catchment/CatchmentSystem.cs:108-182` | every settlement as a partition source; all pairs | catchment borders, distances (physical substrate) | none (physics; its outputs feed N3, N4, D2) | as cited | MEASURED |
| N9 | GovernanceSystem reach | `Sim.Core/State/Governance.cs:94-115` | own capital → own place cost (over shared lanes) | reach, effective tax | none | as cited | MEASURED |

### 5.4 The quarantined test (`Sim.Tests/State/PolityKnowledgeTests.cs:310-366`)
- What it does (MEASURED): takes the AI's free pick on the one-AI dev rig; hides ONLY the eight lattice nodes around
  the pick's node (`:330-344`); zeroes fertility under unknown nodes, changing no known cell (`:351-354`); re-runs
  `BestSiteFor` with the same restricted knowledge.
- What it shows (MEASURED): the pick stays on a KNOWN cell (`:358`), the known pick's own score moves (`:359`), and the
  pick between known cells CHANGES (`Assert.False(before == after)`, `:361-365`). The property the system ought to have
  is invariance; it is quarantined ("the Cr003Quarantine pattern, not skipped", `:320-323`) so that it FAILS the
  moment S2b closes the channel, telling the reader to restore `Assert.Equal(before, after)` and strike the channel
  from ADR-037 §6 and ADR-036 §8.3.
- Recorded magnitudes: hiding eight nodes moves the pick 36463 → 42559 and that cell's score 48.96 → 16.00; with half
  the map unknown, unknown fertility alone moves 472/468 known cells' scores, flips the candidate arm for 433/2,883
  known cells and moves the floor 18.81 → 19.56/26.54 (`docs/adr/adr-037-player-knowledge-seam.md:138-152`). RECORDED.

### 5.5 What the existing AI metamorphic tests do and do not cover
- `TheAiProducersOrders_DoNotDependOnAnyOtherPolitysBooks` plays 12 turns and, each turn, perturbs every FOREIGN
  settlement's people, stocks, dwellings, deposits, structures, queue/progress, allocation, deficits, every foreign
  formation's experience and other polities' material splits; `AiOrders.For` must be identical; control: emptying the
  AI's own stocks changes its orders (`Sim.Tests/State/PolityKnowledgeTests.cs:368-453`). MEASURED.
- It does NOT perturb foreign SITE CELLS, foreign catchments, foreign paths/roads, `SettlementDistances`, trade
  flows or terrain (`PerturbForeignBooks`, `:373-408`). Under all-known these channels cannot be detected by it; they
  are exactly the latent rows D2–D6, F4–F12 (INFERENCE).
- The tax valve is never exercised by that rig ("it fires no SetTaxRate in its 12-turn A1 rig — INFERRED",
  `docs/m6-foundations-audit.md:63`, C9). RECORDED.
- `AiConstructionPolicy_AsksTheSeamBeforeItReadsAnyQueue` is a structural (regex) pin
  (`Sim.Tests/State/PolityKnowledgeTests.cs:466-487`). MEASURED.

---

## 6. Events, logs and UI

### 6.1 Outputs that carry hidden information today (all-known) and whether they are filtered

| Output | Content | Filtered by knowledge? | Status | Evidence | Label |
|---|---|---|---|---|---|
| Order log (`orders-*.bin`) | every order, including AI orders and AI founding SITES | no | world-wide by decision | `Sim.Ui/UiSession.cs:636`; `docs/d060-rival-internals-isolation.md:82-100` | MEASURED |
| Trace CSV | per turn: world population, settlement count, world grain, WORLD HASH | no | world-wide | `Sim.Core/Kernel/SessionTrace.cs:31-58` | MEASURED |
| Telemetry JSONL | every `SettlementRecord` of every step | no | world-wide | `Sim.Core/Observability/TelemetryWriter.cs:5-11`; `Sim.Core/Observability/Observer.cs:111-119` | MEASURED |
| Forensic JSONL, manifest | run/close records; AI empire count | no | world-wide | `Sim.Ui/UiSession.cs:898-940`; `Sim.Cli/SessionEmitter.cs:9-32` | MEASURED |
| Chronicle file (`chronicle-*.txt`, `RecordAnnalLines`) | core events with figures | P7 hidden-settlement filter only (`AdmitsAnnal` applies to `_annals` too); the collector observes every settlement | world-wide in production | `Sim.Ui/UiSession.cs:33-50, 300-312, 334-340` | MEASURED |
| In-game ANNALS (`AnnalLines`) | core + state events | yes: hidden settlements dropped; at a rival only a figure-free founding is told; state events filtered | gated | `Sim.Ui/UiSession.cs:314-332`; `docs/d060-rival-internals-isolation.md:133-147` | MEASURED |
| F12 / `--dev` panels (TURN, RECORDS, ECONOMY) | every KNOWN empire's books; world price spreads and flows | through `ForDeveloper()` (hidden stays hidden) | world-wide for known, by decision | `Sim.Ui/GameUi.cs:462, 2063, 2076`; `docs/d060-rival-internals-isolation.md:66-79` | MEASURED |
| CLI (`sim run/inspect/replay/explain`) | everything | no | developer tooling | `Sim.Cli/Program.cs`; `Sim.Cli/SessionEmitter.cs:9-32` | MEASURED |
| One-file export (M6 T6.14, planned) | the six session files in one workbook | — | PROPOSED; "no state" | `docs/m6-spec.md:174, 285` | MEASURED (record) |

- INFERENCE: under stored extent, `chronicle-*.txt` would become fog-filtered (it shares the `AdmitsAnnal` filter,
  `Sim.Ui/UiSession.cs:307`) while trace, telemetry and the order log stay world-wide — the session record would mix
  filtered and unfiltered files. OPEN QUESTION for the fog ruling and T6.14.

### 6.2 Player surfaces: the open channels recorded or found

| Channel | Evidence | Status / Label |
|---|---|---|
| Terrain bake and river mesh are drawn WHOLE by the GPU pass (baked once at start; no Fog layer exists) | `Sim.Ui/SimUiGame.cs:206-213, 258-266`; `Sim.Ui/World/MapLayers.cs:48-53`; `docs/adr/adr-037-player-knowledge-seam.md:157` | open, ADR-037 §6 · MEASURED |
| Road panel / road action lists every candidate route, naming foreign endpoints within 600 km | `Sim.Ui/Actions/ActionSurfaceModel.cs:786-810`; `Sim.Core/State/AvailableActionsQuery.cs:458-512`; `docs/adr/adr-037-player-knowledge-seam.md:153-154` | open · MEASURED |
| Placement mode verdict bit (Found here / Not here) about unseen ground | `Sim.Ui/ViewModel/PlacementMode.cs:20-24`; `docs/queue.md:1473` | open, DW-002 · MEASURED |
| A known rival's token footprint and dwelling blocks (sized by its population) | `docs/d060-rival-internals-isolation.md:55-60, 255-258` | Decision A DEFERRED until fog · MEASURED (record) |
| Rival map colour = `polity % palette size`, i.e. polity ids are inferable modulo the palette | `Sim.Ui/World/MapInk.cs:88, 179-186` | minor · MEASURED |
| `KnowsSettlement` for a foreign settlement requires CURRENT sight: with no memory, rivals vanish when unseen; `KnowsPolity` (contact) flickers likewise | `Sim.Core/State/PolityKnowledge.cs:151-186` | INFERENCE (GHOST memory is T6.9) |

### 6.3 The UI raw-read allowlist
- File: `scripts/ui-raw-read-allowlist.txt` — **24 files** (`:18-41`): seam 1, routed 9 (WorldLens, SettlementSelection,
  HudModel, PlayerViews, StateChronicle, UnitCardModel, NeedsPanelModel, HistoryBuffer, ResourceOverlay), own 2
  (ActionSurfaceModel, UiSession), developer 2 (ScreenModels, TurnAuditModel), geometry 2 (CameraFocus,
  OverlayMeshes), harness 8 (KnowledgeStubs, PlayabilityGate ×3, MapFrameCost, EraPreview, AgePreview, R2aPreview).
  MEASURED.
- What it matches: a read of **Settlements, MilitaryUnits, Buckets or Deposits** through a receiver named `world`, `w`,
  `_world`, `World`, `prev`, `next` or `W` in `Sim.Ui/**/*.cs` (`scripts/check-read-isolation.sh:83-111`, pattern
  `:90`). The script is ADVISORY ("a grep cannot tell a guarded read from an unguarded one", `:85-88`); the source guard
  reads the same list, requires every `routed` file to reference `PlayerKnowledge`, rejects stale entries and pins the
  stub's callers (`Sim.Ui.Tests/KnowledgeLeakTests.cs:1306-1394`). CI runs the script (`.github/workflows/ci.yml:20`).
  MEASURED.
- Not covered by either guard (MEASURED by grep of `Sim.Ui` for other world tables): raw reads of `Terrain`,
  `Controls`, `CatchmentNodes`, `NetworkNodes/Edges`, `TransportEdges`, `TradeFlows`, `GoodStocks`, `Prices`,
  `Housing`, `Structures`, `Institutions`, `Notables`, `Polities` — e.g. `Sim.Ui/World/WorldLens.cs` (CatchmentNodes,
  network, Structures, Housing, Notables, TransportEdges, Terrain), `Sim.Ui/ViewModel/PlayerViews.cs` (TradeFlows,
  TransportEdges, GoodStocks, Housing, Structures), `Sim.Ui/ViewModel/StateChronicle.cs` (Institutions, Polities,
  Structures, TransportEdges), `Sim.Ui/World/InstitutionMarkerSource.cs:35-47` (Institutions),
  `Sim.Ui/ViewModel/TradeModel.cs` (TradeFlows, Prices; DEV only), `Sim.Ui/SimUiGame.cs:208` (Terrain). These are
  protected only by the metamorphic tests and by being called from routed readers (INFERENCE).

---

## 7. Control changes

| Event | Code path | What happens to control / roster / knowledge rows | Relevance to monotone knowledge and memory | Label |
|---|---|---|---|---|
| Revolt | `Sim.Core/Systems/Revolt/RevoltSystem.cs:118-192` | Signals from PREV (`:132-141`); a ruler's final place never revolts, all-at-once keeps the capital (`:136, 145-159`); control rows of revolted places removed, order preserved (`:161-174`); each becomes a NEW AI polity with a control row (`:176-188`), a full copy of the parent's completed research (`:189`) and the parent's Age (`:190, 194-209`); no capital, no formation (`:200-202`) | Research knowledge is monotone and copied (D-048 r1–r7, `docs/d048-knowledge-persistence-rulings.md:11-21`). For an extent: the plan copies the parent's extent to the child and keeps the parent's (`docs/m5w-map-workstream-audit-and-plan.md:735, 742`; `docs/m6-spec.md:230-231, 241`). The parent loses SIGHT from that place; under today's seam semantics it then stops KNOWING the settlement unless another source sees it (INFERENCE from `Sim.Core/State/PolityKnowledge.cs:151-160`) — GHOST memory is what keeps it on the map | MEASURED / INFERENCE |
| Colonization (order or emergent) | `Sim.Core/Systems/Colonization/ColonizationSystem.cs:304-389` | settlement row appended (`:323`); control inherited from the parent if any (`:335-338`); deposits written (`:344-346`) | Extent grows by the colony's sight; under emergent siting (N1) a polity can found in land it never saw | MEASURED / INFERENCE |
| Annexation / conquest | none in the simulation | `KnowledgeTransfer.MergeInto` is the union to call; "No annexation or conquest path exists" | D-048 r8 ratifies union (`docs/d048-knowledge-persistence-rulings.md:44-56`); extent union is proposed (`docs/m6-spec.md:231`) | MEASURED |
| Settlement destruction / abandonment | none: `Settlements` is append-only in code (writers: `WorldFounding`, `ColonizationSystem.cs:323`, deserialization) | an emptied settlement keeps its row (chronicle `Extinction` event, `Sim.Core/Chronicle/ChronicleCollector.cs:6-18`) | A remembered ruin can never be "removed" by the sim; memory of it can only go stale | MEASURED / INFERENCE |
| Polity extinction | derived: controls no settlement; the `PolityRow` remains | `Sim.Core/State/EmpireQuery.cs:36-44` | contact and memory of an extinct polity have no carrier | MEASURED |
| Capital loss | `CapitalRow` written only at founding (`Sim.Core/Worldgen/WorldFounding.cs:368-381`); a revolted capital leaves a row the polity no longer controls; reach then 0 (`Sim.Core/State/Governance.cs:94-100, 270-274`) | — | A formation stationed at a lost capital keeps `Location` there (no writer moves it, `Sim.Core/State/WorldState.cs:1002-1007`): "sight from owned formations" would then see from inside a foreign settlement (INFERENCE) | MEASURED / INFERENCE |
| Loss of frontier / control decay | no loss path besides revolt; `ControlRow.Strength` = reach, rewritten each step | `Sim.Core/State/WorldState.cs:610-619` | none today | MEASURED |
| Stateless settlements | reachable only from a stateless parent; founding controls every settlement; revolt creates a polity | `Sim.Core/Worldgen/WorldFounding.cs:359-366`; `Sim.Core/Systems/Colonization/ColonizationSystem.cs:106-117`; `Sim.Core/Systems/Revolt/RevoltSystem.cs:176-188` | local research holders (city-states) are dormant in founded worlds (INFERENCE) | MEASURED / INFERENCE |

---

## 8. Contact and trade

| Topic | What exists | Evidence | Label |
|---|---|---|---|
| Contact | No stored concept. `KnowsPolity` = itself, or a polity with a settlement it knows; all-known today ⇒ every polity "knows" every polity | `Sim.Core/State/PolityKnowledge.cs:175-186` | MEASURED |
| Recognition, claims | Schema only, no writer; D-037 C7 makes recognition govern trade access, now M8 | `Sim.Core/State/WorldState.cs:581-591, 670-680`; `docs/d037-emergent-polities.md:12, 127-129` | MEASURED |
| Trade gate | Both endpoints' settlement research mask must make `trade.entity` eligible; no contact, extent or polity term | `Sim.Core/State/TradeQuery.cs:6-28`; `Sim.Core/Systems/Trade/TradeArbitrageSystem.cs:120-137` | MEASURED |
| Trade reach | Pairwise over `SettlementDistances`; unreachable = 0 flow forever | `Sim.Core/Systems/Trade/TradeArbitrageSystem.cs:12-23, 248-256` | MEASURED |
| Foreign/domestic | `TradeScope` classification only; nothing in the pipeline consults it | `Sim.Core/Systems/Trade/TradeScope.cs:23-50` | MEASURED |
| Mutually-known trade term | "if ruled (M6 per the T4.6 note)" — DW-006 | `docs/m5w-map-workstream-audit-and-plan.md:762`; `docs/m6-plan/deferred-work-register.md:31`; `docs/t4.6-foreign-trade-decision.md:116-132` | OPEN |
| Foreign exposure | `ResearchExposureRow(Polity, Node, Offered)` — an input contract with NO writer; `ResearchSystem` credits it from the shared ≤ 0.40 × BaseCost pool | `Sim.Core/State/WorldState.cs:922-931`; `Sim.Core/Systems/Research/ResearchSystem.cs:5-7, 142-172`; `Sim.Core/State/ResearchQuery.cs:453-463` | MEASURED |
| Diffusion | None. Planned T6.10: a writer from "a named contact carrier (trade flows / routes within known extent)" | `docs/m6-spec.md:129, 210-216, 281` | PROPOSED |
| Hidden dependency | T6.10 waits on T6.8 (contact within known extent) | `docs/m6-plan/m6-ruling-docket.md:71-72` | MEASURED (record) |

---

## 9. Persistence

| Item | Fact | Evidence | Label |
|---|---|---|---|
| Schema version | `CanonicalSchema.Version = 34` | `Sim.Core/Kernel/CanonicalSchema.cs:181` | MEASURED |
| Readable set | {32, 33, 34}; one predicate `IsReadable`; any other version refused (D-008 replay recovery) | `Sim.Core/Kernel/CanonicalSchema.cs:183-195` | MEASURED |
| Append-only premise | "ADDING STATE? Three edits" (Write, Read, ExpectedLength); readable older streams are exact because every newer table was APPENDED (`BytesAppendedSince`) | `Sim.Core/Kernel/CanonicalSchema.cs:14-16, 197-214` | MEASURED |
| Sections | 1 Seed, 2 Clock, 2b terrain flag + content hash, … 59 ConstructionProgress, 60 ResourceAllocations, 61 ConstructionHolds | `Sim.Core/Kernel/CanonicalSchema.cs:284-300, 950-979` | MEASURED |
| Snapshot file | header {"CIVSNAP\0", version, seed, turn} + canonical stream + int32 topology trailer (outside the hash) | `Sim.Core/Kernel/Snapshot.cs:8-46` | MEASURED |
| Terrain | never in a save; regenerated from seed and checked against the stored hash | `Sim.Core/Kernel/Snapshot.cs:48-55` | MEASURED |
| v35 reservation | "S2a moves to v35 and S3 to v36" (the m5w plan's reservations re-noted by D-055); accepted with D-055 at R5 | `docs/d055-batch4-reservations-attribution-allocation-ci-persistence-pathfinding.md:1306-1307, 1355-1357`; `docs/d054-batch3-incremental-construction-allocation-cylinder.md:1234`; `docs/m5w-map-workstream-audit-and-plan.md:1227-1230`; `docs/m6-spec.md:47` | MEASURED (record) |
| "First to land" | D-H (OPEN): the first state packet takes the next version, reservations re-noted; D-061.6 recommends civics take the next landed version and fog's v35 moves by note | `docs/m6-plan/m6-ruling-docket.md:32, 43`; `docs/m6-plan/m6-decision-register.md:16` | OPEN |
| Fog / extent / visibility state | **None**: no row type, table or section; grep finds only comments | §0 item 1 | MEASURED |
| D-052 §5 | "No visibility state ... ship in M5"; the save-format half superseded by v33/v34, "The visibility half STANDS" | `docs/d052-m5-integration-audit-rulings.md:53-66, 217-230` | MEASURED (record) |
| UI resume | by REPLAY of the order log from the manifest's seed through the same per-step observers: UI-side histories (annals, history, now-usable) are rebuilt; the AI producer is not re-run (its orders are in the log) | `Sim.Ui/UiSession.cs:990-1022` | MEASURED |
| `sim run --from-save` | loads a snapshot and continues; "the continuation produces the AI's orders itself" | `Sim.Cli/FromSave.cs:8-38` | MEASURED |
| Consequence | INFERENCE: anything the AI decides from (extent, sighting memory) must be in the canonical stream, or a `--from-save` continuation diverges from the uninterrupted run; a memory read ONLY by presentation could be rebuilt by replay | from the two rows above | INFERENCE |

---

## 10. Recorded decisions about fog, discovery, sight, memory and AI information

| ID | Status | Citation | One-line content |
|---|---|---|---|
| Spine principle 7 | FROZEN | `docs/civ-sim-architecture-v3-outline.md:25` | "AI actors use player-identical verbs and information class. Difficulty = information and friction, never hidden resources." |
| D-039 Part A | RATIFIED (Director design ruling) | `docs/d039-command-fog-and-siege.md:12-46` | Knowledge and latency vary by era, not verbs; POSITION, STRENGTH, OUTCOME are separate channels; no era gate (A5) |
| D-039 B1–B5 | RATIFIED | `docs/d039-command-fog-and-siege.md:48-75` | Recon is bought with food and people, diminishing returns, POINTED not global, distance degrades mildly with vantage extending range, reports reliable but stale |
| D-039 B6 | OPEN | `docs/d039-command-fog-and-siege.md:77-82` | Scouts: a class, a flag or a capability score — "M6's spec owns it" |
| D-039 Part C | RATIFIED; display assigned to M6 by D-052 §5 | `docs/d039-command-fog-and-siege.md:84-100` | LIVE / GHOST / UNKNOWN; the ghost is "the peg on the table"; return to LIVE immediately when observed; visuals not designed |
| D-039 pointer | RATIFIED note | `docs/d039-command-fog-and-siege.md:6-10` | "M6" in D-039 means the M7 Battle Layer under today's numbering |
| D-040 A4, B1 | RATIFIED | `docs/d040-discovery-and-control.md:29-39` | "not a fog-of-war overlay on a known map"; settlement, claim and trade operate only within known extent |
| D-040 B2 | RATIFIED | `docs/d040-discovery-and-control.md:41-54` | Discovery = D-039 reconnaissance at another scale; "Do not invent a second exploration system"; "Once known, land stays known" |
| D-040 B3, B4 | RATIFIED (B4's transport conversation OPEN) | `docs/d040-discovery-and-control.md:59-80` | No technology unlock of reach; sea travel arises from conditions; boats are network edge types |
| D-040 B5, E4 | RATIFIED | `docs/d040-discovery-and-control.md:82-88, 180-181` | Storage, granularity, curves and constants not designed here |
| D-040 D3 | RATIFIED (renumbered) | `docs/d040-discovery-and-control.md:158-164` | Map extent and discovery are the knowledge milestone (now M6) |
| D-052 §2 (ADR-036 §8 (h)) | RATIFIED 2026-10-12 | `docs/d052-m5-integration-audit-rulings.md:27-29` | The AI's information-access leak is accepted until M6's known extent; median 953 km colony distance accepted as evidence |
| D-052 §3 (ADR-037) | RATIFIED 2026-10-12 | `docs/d052-m5-integration-audit-rulings.md:38-44` | One seam; production all-known until a stored extent exists; `Restricted` test-only; open channels are M6's |
| D-052 §5 | RATIFIED 2026-10-12 | `docs/d052-m5-integration-audit-rulings.md:53-66` | Full fog, LIVE/GHOST/UNKNOWN, scouts, persisted sighting memory, extent storage, known-extent restriction of settle/claim/trade, closing ADR-037's channels: all M6 |
| D-052 §8 | RATIFIED (closed as built) | `docs/d052-m5-integration-audit-rulings.md:100-106` | The resource shortcut highlights known places only and never reveals unknown tiles |
| D-054 instruction (W3) | RECORDED Director instruction | `docs/d054-batch3-incremental-construction-allocation-cylinder.md:124-126, 572-576` | Explored-longitude camera limits, exploration-unlocked wrapping and Satellites reveal are "EXPLICITLY OUT OF SCOPE FOR NOW" |
| D-055 note | RATIFIED (D-055 accepted at R5) | `docs/d055-batch4-reservations-attribution-allocation-ci-persistence-pathfinding.md:1306-1307, 1355-1357` | v35 reserved to S2a (known extent), v36 to S3 (Action Points) |
| D-048 | RATIFIED 2026-10-04 | `docs/d048-knowledge-persistence-rulings.md:9-21, 42-56` | Completed knowledge never decays; revolt copies completed research only; annexation is a union |
| D-051 §2 | RATIFIED, deferred; scheduled M6 by R2 | `docs/d051-m5-polish-rulings.md:33-50`; `docs/m5-exit-inventory.md:398-402` | Availability classes, common floor, regional richness; coordinated with fog |
| D-051 §5 | RATIFIED | `docs/d051-m5-polish-rulings.md:79-89` | Found Settlement order; "Emergent founding (D-037 B1) continues unchanged" |
| ADR-036 | RATIFIED 2026-10-12 | `docs/adr/adr-036-found-settlement-order.md:3-16, 95-96, 251-266, 370-406` | The founding predicate with an inert known-extent arm; the AI leak recorded; S2b must narrow the AI's READ as well as its PICK |
| ADR-037 | RATIFIED 2026-10-12 (GHOST moved from M7 to M6) | `docs/adr/adr-037-player-knowledge-seam.md:3-16, 33-226` | The seam, the readers, the AI rules, the metamorphic gate, §6 open channels, §7–§9 P8/P9/P10 addenda |
| D-060 | ACCEPTED at R5 2026-10-20 | `docs/d060-rival-internals-isolation.md:29-100, 248-267` | A known rival's books are closed on every player surface; F12 panels and session files stay world-wide; Decision A (token size) DEFERRED until fog; Decision B (pre-capture prices) intentional |
| M6 spec §1.8, §1.13, §1.14 | DRAFT, not ratified (each row cites ratified records) | `docs/m6-spec.md:41, 46, 47` | Fog is M6; no second exploration system; v35/v36 reservations |
| M6 spec I-8, I-9 | PROPOSED | `docs/m6-spec.md:98-102` | Knowledge monotone; the AI reads the world only through `PolityKnowledge` for anything fog gates; leak tests green with the stub replaced by extent |
| M6 spec §3.2 (sight budget, known extent) | PROPOSED | `docs/m6-spec.md:127-129` | Sight in cost units via `Pathfinder.Isochrone`; vantage only as a dimensionless budget multiplier; extent = bits over 65,536 nodes |
| M6 spec §3.5 F | PROPOSED | `docs/m6-spec.md:229-242` | Extent v35, monotone OR, seeded from founding catchments, revolt copies, annexation unions; sight derived, never stored; display + memory (state if persisted) |
| T6.7 | PROPOSED | `docs/m6-spec.md:278` | S2a: `KnownExtentRow`, `DiscoverySystem`, sight isochrone, seeding, revolt/annex rules, v35 + ADR, `Of` on extent; blocks on D-K, D-J |
| T6.8 | PROPOSED | `docs/m6-spec.md:279` | S2b: settle/claim/trade/roads restricted; AI channels closed; corridor deltas measured; blocks on D-T |
| T6.9 | PROPOSED | `docs/m6-spec.md:280` | S2c: LIVE/GHOST/UNKNOWN, sighting memory, Fog layer, terrain/river cover, Decision A |
| T6.10, T6.16 | PROPOSED | `docs/m6-spec.md:281, 287` | Diffusion via contact within known extent; AI strategy under fog |
| D-K | OPEN (decision-ready) | `docs/m6-plan/m6-ruling-docket.md:37`; `docs/m6-plan/m6-decision-register.md:19, 144-155` | Ratify per-polity bitset, monotone OR, isochrone sight from settlements and formations with one TUNE budget, sighting memory as state |
| D-J | OPEN | `docs/m6-plan/m6-ruling-docket.md:38`; `docs/m6-plan/m6-decision-register.md:18, 130-142` | Scouts: recommended none in M6 (M7's first MobileAgent) |
| D-T | OPEN | `docs/m6-plan/m6-ruling-docket.md:39`; `docs/m6-spec.md:348` | AI assumption about unseen terrain: recommended impassable and worthless |
| D-H | OPEN | `docs/m6-plan/m6-ruling-docket.md:43` | Schema sequencing "first to land" |
| DW-001…DW-006 | DW-001/003 scheduled; DW-002/004/005/006 owe a ruling | `docs/m6-plan/deferred-work-register.md:26-31` | Fog cluster; the judged world of a fogged order; the AI whole-map read; token size; scouts; trade mutually-known |
| m5w plan S2a–S2c and the fog issue | PROPOSED (planning), deferred to M6 by D-052 §5 | `docs/m5w-map-workstream-audit-and-plan.md:725-800, 890-899, 980-995, 1196` | The bitset design, the gating list, the three-state presentation, the critic's sight/vantage and seeding corrections |
| AI constitution §7 | Planning document (not ratified) | `docs/m5-ai-constitution.md:3-5, 124-137` | "A stronger AI should not automatically receive hidden information"; AI symmetry a hard constraint |
| D-043 F12 | INFERRED reading in a ratified record (CR-017 OPEN) | `docs/d043-civilization-progression-ages-and-mobile-agents.md:483-484, 901-904` | An army token must not merge formations observed unequally |

---

## 11. Tests

### 11.1 What exists

| Test file | Methods (MEASURED by attribute count) | What it proves | Evidence |
|---|---|---|---|
| `Sim.Tests/State/PolityKnowledgeTests.cs` | 11 | Seam all-known; grid = lattice; stub semantics; AI ranks only known cells; unknown scores cannot move the pick; terrain under unknown nodes never puts an unknown cell in the pick; the QUARANTINED valuation channel; AI orders independent of foreign books; construction asks the seam first; formation card; deposit-sector card | `:65-565` |
| `Sim.Ui.Tests/KnowledgeLeakTests*.cs` (4 files) | 34 (24 + 4 + 5 + 1) | Metamorphic on VALUES with positive controls: projection/LensFrame/SVG, HUD, views, market, needs, Tab, clicks, cards, place cards, annals, Trends, history, FrameText; source guards; D-060 rival books; holds | `Sim.Ui.Tests/KnowledgeLeakTests.cs:22-36, 165-1561`; `KnowledgeLeakTests.Rivals.cs:196-374`; `KnowledgeLeakTests.Resources.cs:64-222`; `KnowledgeLeakTests.Holds.cs:25` |
| `Sim.Tests/Systems/KnowledgeMonotonicTests.cs` | 9 | Revolt copies knowledge; Eureka stays; annexation union; save/load and replay of both; "KnowledgeNeverDecays_OverAThousandYears" | `:65-231` |
| `Sim.Tests/State/SiteAssessmentTests.cs` | 20 | The place card's withholding rules with all-known controls | file |
| `Sim.Ui.Tests/ResourceOverlayTests.cs` | 18 | Overlay draws nothing on undiscovered ground; terrain/deposits under hidden nodes perturbed | `docs/adr/adr-037-player-knowledge-seam.md:210-213` |
| `Sim.Ui.Tests/DiscoveryShortcutTests.cs` | 21 | The outline contains only known qualifying nodes | `docs/adr/adr-037-player-knowledge-seam.md:223-226` |
| `Sim.Tests/Systems/AiFoundingPolicyTests.cs` | 15 | Tie-dense ranking; predicate binds; an EMULATED known-extent arm narrows the pick to the next-best legal cell | `:70-524`, emulated arm `:229-290` |
| `Sim.Tests/Pathing/PathfindingTests.cs` | — | Isochrone deterministic/contiguous/monotone; lanes expand it; timing bounds | `:243-354` |
| Headless gate "hidden rival" state | — | Tab never reaches a hidden settlement; a click on a hidden site selects nothing; overlay draws nothing there | `Sim.Ui/Headless/PlayabilityGate.cs:149-165`; `Sim.Ui/Headless/PlayabilityGate.Overlay.cs:13` |

### 11.2 What a fog implementation must flip or extend (INFERENCE unless marked)
1. Flip: `Of_IsAllKnown_EveryAnswerIsYes_AndTheBooksAreTheControllers` (`Sim.Tests/State/PolityKnowledgeTests.cs:65-88`)
   and `TheProductionView_IsAllKnown_AndEveryReaderGivesWhatItGaveWithoutKnowledge`
   (`Sim.Ui.Tests/KnowledgeLeakTests.cs:1287-1304`) — production stops being all-known.
2. Flip (designed to): `KnownCellsValuations_StillIntegrateTerrainUnderUnknownNodes_TheRecordedChannelQuarantined`
   (`Sim.Tests/State/PolityKnowledgeTests.cs:310-366`) when S2b closes the channel — restore `Assert.Equal`.
3. Re-anchor every POSITIVE CONTROL that is "the all-known production view" (`Sim.Ui.Tests/KnowledgeLeakTests.cs:22-36`):
   with stored extent the control needs an explicit reveal or stub.
4. Extend the AI metamorphic test beyond books (§5.5): perturb unknown settlements' sites, catchments, roads, paths,
   distances and terrain; add the system-level rows N1–N7 if the fog ruling gates them.
5. Replace the EMULATED arm (`Sim.Tests/Systems/AiFoundingPolicyTests.cs:229-290`) with the real `FoundingQuery` arm and
   a turn-exact "known at t ⇒ legal at t+1, illegal at t" pin (`docs/m5w-map-workstream-audit-and-plan.md:769`).
6. Extend `KnowledgeMonotonicTests` to extent bits: never clear across steps, revolt and loss of control
   (`docs/m6-spec.md:241, 310-312`).
7. Add the CLAUDE.md row-type obligations: a POPULATED-table test per new row (ExpectedLength, bit-exact round trip,
   hash), a turn-exact discovery pin, `PreFog` attribution twins for every moved golden (`docs/m6-spec.md:103-104, 278`).
8. Sight tests that the pathfinder suite cannot give today: water/coast nodes, lanes, vantage (§4.7).
9. Keep: `SourceGuard_TheKnowledgeStub_IsBuiltOnlyByTheHarness_AndNoReaderBuildsASecondView`
   (`Sim.Ui.Tests/KnowledgeLeakTests.cs:1378-1394`) pins `PolityKnowledge.Restricted(` to the stub — a production
   extent must enter through `Of`, not `Restricted`, or this guard fails. MEASURED (the guard's literal match).

---

## 12. Disagreements between documents and code (code wins)

| # | Document says | Code / other record says | Label |
|---|---|---|---|
| 12.1 | `PolityKnowledge` type comment: ghost memory "D-039 C places in M7" | D-052 §5 moved it to M6 (`docs/d052-m5-integration-audit-rulings.md:65-66`); comment still at `Sim.Core/State/PolityKnowledge.cs:27-28` (already listed as stale, `docs/m6-spec.md:332-334`) | MEASURED |
| 12.2 | T6.1 audit D6: `KnowledgeLeakTests*` "31 methods across 4 files" (`docs/m6-foundations-audit.md:74`) | 34 `[Fact]`/`[Theory]` methods (24 + 4 + 5 + 1) at this tree | MEASURED |
| 12.3 | ADR-037 §6: `MapInk.PolityOf`'s colour index is "`polity % roster size`" (`docs/adr/adr-037-player-knowledge-seam.md:157-158`) | `polity % Polities.Count` where `Polities` is the colour PALETTE (`Sim.Ui/World/MapInk.cs:88, 181-185`) | MEASURED |
| 12.4 | M6 spec §3.5 F: extent "seeded from founding catchments" (`docs/m6-spec.md:230-231`) | No catchment row exists at turn 0: founding writes none; only `CatchmentSystem` writes them, in its first step (`Sim.Core/Systems/Catchment/CatchmentSystem.cs:100-149`; grep of `WorldFounding.cs`). The m5w plan's own critic already corrected this (`docs/m5w-map-workstream-audit-and-plan.md:995`) | MEASURED |
| 12.5 | M6 spec §3.2/§3.5 F and docket D-K: sight = `Pathfinder.Isochrone` (`docs/m6-spec.md:127, 233`; `docs/m6-plan/m6-ruling-docket.md:37`) | The m5w plan's applied correction: "sight must not be modelled as a movement-cost isochrone (D-039 B4 vantage)" (`docs/m5w-map-workstream-audit-and-plan.md:5, 980`); `Isochrone` never enters water and follows every lane (§4.7) | MEASURED (tension, unruled) |
| 12.6 | Docket D-K: "memory derived only: lost on resume" (`docs/m6-plan/m6-ruling-docket.md:37`) | UI resume replays every step through the same observers and rebuilds UI-side histories (`Sim.Ui/UiSession.cs:990-998`); what a derived memory loses is a SNAPSHOT continuation (`Sim.Cli/FromSave.cs:8-38`) and any AI read of it | INFERENCE |
| 12.7 | D-060 §3: "A settlement under no empire (after a revolt) has no controller" (`docs/d060-rival-internals-isolation.md:39-40`) | Since R3 a revolted place is controlled by a NEW AI polity (`Sim.Core/Systems/Revolt/RevoltSystem.cs:176-188`) | MEASURED |
| 12.8 | `docs/t4.6-foreign-trade-decision.md:118-121` cites D-037 C7 at `:119-121` | C7 is at `docs/d037-emergent-polities.md:127-129` (citation drift) | MEASURED |
| 12.9 | `UnitCardModel` comment: "`OrderKind` is 1..8" (`Sim.Ui/ViewModel/UnitCardModel.cs:46`) | Kinds 1–9 and 11–13 exist; 10 reserved (`Sim.Core/Kernel/OrderLog.cs:150`) (listed stale by `docs/m6-spec.md:332-334`) | MEASURED |

---

## 13. Open questions and seams (consolidated; none is a design)

1. **Extent seed at turn 0** (§12.4): no catchment exists; a founding-time sight write or a first-step discovery is
   needed, and either moves every golden. OPEN QUESTION.
2. **Initial contact.** Founding deals settlements round-robin across the continent
   (`Sim.Core/Worldgen/WorldFounding.cs:357-366`), so every polity's seed extent may overlap rivals' at turn 0.
   OPEN QUESTION: measure inter-polity distances against any sight budget before ruling D-K.
3. **Coast and sea in the extent** (§4.7 item 1): water nodes are never reached by the pathfinder. OPEN QUESTION.
4. **Sight along foreign lanes and vantage** (§4.7 items 2–3; §12.5). OPEN QUESTION.
5. **Where the visible set is computed once per step** without a cache or a second source of truth (§4.7 item 4).
   OPEN QUESTION.
6. **Which systems are gated by extent** beyond settle/claim/trade: emergent colony siting (N1), migration (N3),
   path targets (N5), road plans (N6), raiding (N7). D-051 §5 says emergent founding "continues unchanged"
   (`docs/d051-m5-polish-rulings.md:86`) against D-040 B1. OPEN QUESTION (m5w fog issue options 2/3,
   `docs/m5w-map-workstream-audit-and-plan.md:893-899`).
7. **The same-turn AI batch** (P2) is an asymmetry today; DW-002's ruling on "which world a fogged order is judged
   against" decides both it and the placement-mode bit. OPEN QUESTION.
8. **D-T**: what the AI assumes of unseen terrain — it decides F4, F8, F10–F12 at once. OPEN QUESTION.
9. **Memory as state**: AI-relevant memory must be serialized for `--from-save` identity (§9 last row). OPEN QUESTION
   (D-K's fourth clause).
10. **Session files under fog** (§6.1): chronicle filtered, telemetry/trace/orders world-wide; T6.14 bundles them.
    OPEN QUESTION.
11. **Per-place deposit truth for a discovery chain** (§2.8 rows 1, 3, 4): today there is nothing place-level to
    discover, identify or prospect, and the roll is keyed to the settlement id. D-051 §2 is the ratified hook.
    OPEN QUESTION.
12. **Strategic resources**: none exists in content (§2.2); any uranium-to-industry chain is new content and new goods.
    MEASURED gap.
13. **Raw-read guard scope** (§6.3): four tables only; other foreign-sensitive tables rely on metamorphic tests.
    INFERENCE.
14. **Formation sight from a lost capital** (§7, capital loss row). INFERENCE.
