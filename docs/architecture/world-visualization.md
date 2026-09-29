# World visualization foundation — buildings, cities, institutions, infrastructure, mobile agents

**Status:** UI foundation on branch `claude/civdemo-work-b1z2y4`, not ratified, not merged. Sim.Ui only:
`git diff 78e6b58 -- Sim.Core Sim.Data Sim.Cli Sim.Tests .github` is empty. **No gameplay mechanic is implemented
here.** Nothing in this layer decides that an entity exists, grows, matures, moves or fights; it draws what a source
reports. Every question the task left to the Director is in the register (§13), not in the code.

Code: `Sim.Ui/World/**`. Content: `Sim.Ui/UiContent/world/{morphology,demo-world}.json` (copied beside the exe as
`ui-content/world/`). Tests: `Sim.Ui.Tests/World/**`. Preview: `sim-ui --world-preview [dir]`,
`scripts/world-preview.sh`, frames in [`world-preview/`](world-preview/).

## 1. The rule this layer is built around

> The renderer NEVER decides that a gameplay entity exists.

Every settlement, structure, infrastructure node or edge, resource and mobile agent on screen is one a **source**
reported. Every aggregate on screen (a `x3` badge, a `+1` cluster token, a legend line) is a count **of reports**. When
a source does not report something — an input, a position, a state — the view draws the base form and says "not
reported"; it never substitutes another value (an age, a settlement size, a count) for the missing one.

## 2. The pipeline

```
Sim.Core  (authoritative; unchanged; never written by this layer)
   │  IReadOnlyWorldState — tables that do not compile a write
   ▼
LIVE adapters  Sim.Ui/World/Live         DEMO source  Sim.Ui/World/Demo  (PLACEHOLDER)
   │  map existing rows to reports          │  demo-world.json, a stepped timeline
   └──────────────┬─────────────────────────┘
                  ▼
Six narrow read-only sources (Sim.Ui/World/WorldReports.cs), each returning an immutable, key-sorted Snapshot:
   ISettlementViewSource  IPolityViewSource  IStructureViewSource
   IInfrastructureViewSource (nodes + edges)  IResourceViewSource  IMobileAgentViewSource
   bundled as WorldSources (a record of references: no behaviour, no manager). A scene reads ONE bundle.
                  ▼
WorldViewBuilder (pure) + visual morphology (morphology.json)  →  WorldView
   stage chosen, composition slot assigned, aggregation decided, agent anchored, polity ink looked up
                  ▼
WorldScene (pure): projection + level of detail → SceneGeometry (computed once) → per-kind painters
   → nine layer draw lists, concatenated in a fixed order → WorldFrame { DrawList, layer spans, HitIndex }
                  ▼
DrawListImGuiBackend (game)  |  SvgWriter (preview, screenshots)  |  the tests (they read the draw list)
```

## 3. Four kinds of state

| kind | what it is | where it lives | who may change it |
| --- | --- | --- | --- |
| **authoritative gameplay** | the simulation's tables | `Sim.Core` `WorldState`, read through `IReadOnlyWorldState` | the simulation only |
| **view** (reports) | what a source says exists and what it reports about it | `*Report`, `Snapshot<T>`, `WorldSources`; built by the live adapters or the demo source | nobody — immutable per snapshot |
| **demo** (placeholder) | authored demonstration content standing in for producers that do not exist yet | `DemoWorldSource` + `demo-world.json`; its one mutable value is the chosen step | the preview control (Left/Right, `SetStep`) |
| **visual** | how reports are drawn: stage, slot, cluster, ink, LOD, layout, selection, hover, camera | `WorldView`, `WorldFrame`, `WorldUiState`, `morphology.json` | the view (pure functions) and UI input |

The inspector tags every line it prints as **reported**, **view** (a visual choice) or **not reported** (§9.4).

## 4. The boundary (reports and sources)

- **Stable ids.** `WorldEntityId(Kind, Key)`: the kind plus the key the SOURCE assigned — the authoritative id for a
  live source (zero-padded `D10`, so ordinal order is numeric order and every `(score, id)` tie-break agrees with the
  game's `SettlementSelection`), the content key for the demo. The view never mints an id for an entity. The only
  view-minted key is a cluster token's (`settlement/cluster:type`), and selecting a cluster selects its first member.
- **Owner.** `PolityKey` plus a `PolityRelation` — Controller (a settlement's `ControlRow`), Allegiance (a notable's),
  Owner (demo only). The simulation keeps these distinct (D-037/D-042); the panel prints the relation, never a generic
  "owner". M4-D structures belong to the settlement, so the live structure report has no polity.
- **Position.** A continuous `WorldPoint(double X, double Y)` for settlements, nodes and agents. **Structures have no
  position of their own** (D-038 H2, §6). An agent is at exactly one of: a position, a `GraphLocation(edge, fraction)`
  (the ruled shape for armies — D-009, `d009-d010:19, :51`), or a settlement (`AttachedSettlementKey`, how the
  simulation places a notable). An agent whose anchor does not resolve is not drawn — never placed at a default.
- **Visibility.** `ReportedVisibility` is a presentation vocabulary a source maps onto: `NotModelled` (live today —
  drawn, and the panel says "visibility not modelled: omniscient view"), `Visible`, `Remembered` (ghosted, not
  selectable), `Hidden` (not drawn, not hit, not counted). It is NOT the knowledge model: the ruled shape of that is a
  computed per-polity extent with separable, lagged position and strength channels (D-040 B1 `d040:37`, D-039 A2
  `d039:13`), owned by M7 (D-040 D3 `d040:158`). `WorldSources.Observer` names whose knowledge the bundle is (today
  always an omniscient observer). A hidden settlement anchors nobody and its structures and
  resources are neither drawn nor counted; the inspector says only "hidden" about any hidden entity.
- **Provenance.** A view is DEMO / PLACEHOLDER if its bundle says so **or any of its snapshots** does, so a
  mixed bundle can never be labelled LIVE; the live map refuses to draw a placeholder view at all.
- **Inputs.** Named numbers (`ReportedInput`) and named labels (`ReportedLabel`) carried through unchanged; content
  chooses which one drives a visual stage (§5), so a new authoritative input is a content edit, not a renderer change.
- **Get-only.** Every source member is a getter; every report is an immutable record (pinned by
  `WorldBoundaryTests`). Only `Sim.Ui.World.Live` names a `Sim.Core` type (pinned by reflection).

### 4.1 What the live adapters map (and what they do not)

| source | live producer (existing rows) | notes |
| --- | --- | --- |
| settlements | `Settlements` + Σ `Buckets.Count` + `CatchmentSummaries.SizeTier` + `Housing.Dwellings` + `EmpireQuery.TryGetController` | position = the site-cell centre the game's marker uses |
| polities | `Polities` | display name "Polity #n (player)" — the simulation names no polities (WV-05) |
| structures | `Structures` (M4-D `StructureRow`, a count per settlement × project) | one report per row with `Count > 0`, `Multiplicity = Count`, no polity; never expanded into per-building ids. `Established` = the row's index: ConstructionSystem appends a row on a project's first completion and nothing reorders or removes rows, so the index is the order structures first stood — a granary built after a workshop never displaces it. `ConstructionQueue` rows are **not** drawn (M4-D has no progress; DD-08) |
| infrastructure | `NetworkNodes` (junctions at their lattice node) + `NetworkEdges` (transport links) | the one graph (D-009 ¶2). On the live map the game already draws the network, so the layer does not redraw it |
| resources | `Deposits` (good, abundance) | |
| mobile agents | `Notables` with `Count > 0`, at their settlement, display type "person" | a vacated row (death, or the old row of a defection) is not a person and is not reported |
| — | armies, formations, fleets, roles for people, bands, cultural groups | **no live producer exists**: nothing is shown |

## 5. Visual morphology (`morphology.json`)

Every number in the file is a view choice. Loader: `WorldContentLoader` — strict JSON (unknown members, duplicate
keys, nulls in lists are errors), every text Latin-1 (the game's font atlas draws nothing else), and a list of
diagnostics rather than a crash or a partial result.

- **Settlement stages.** `settlementStages.drivers` in priority order; the first driver whose input the source REPORTS
  picks the stage. Live reports `sizeTier` (the simulation's own quantized size step, D-017 CLOSED `m3-spec:15`):
  mapped one to one and shown by number ("sizeTier 2 (stage 3 of 5)"), **never named Town/City** (WV-01). A driver
  whose provenance is `demonstration` (the `population` thresholds) **never stages a live bundle**: a live settlement
  with no sizeTier yet (every settlement at turn 0; a colony on its founding turn) draws the base footprint UNNAMED,
  "base footprint (sizeTier not reported)". Only placeholder bundles use the demonstration thresholds. A stage sets
  only how many residential blocks the composed sprite shows.
- **Structure stages** (`visualTypes[].stages`): cumulative — each stage lists only the parts it **adds**; stage k is the
  union of stages 0..k, so an institution grows and is never replaced by another icon, and an earlier part never moves
  (pinned per type). `stageBy` is either a numeric input (`capacity`, `served`, `staff`, …; `multiplicity` is built in)
  or a label through an ordinal table (`maturity`: NEW/DEVELOPING/ESTABLISHED/MATURE). Stage = the LAST stage whose
  min ≤ value (inclusive). Missing, non-finite or negative → the base stage, and the panel says "not reported".
  Inputs named for time (`established`, `turn`, `year`, `date`, `age`, `era`, `elapsed`) are rejected by the loader:
  a stage keyed to time would be the time-keyed maturation CR-001 closed, and a calendar gate (Law 4).
- **Parts** are drawn in lot-local units (the lot is `[-0.5, 0.5]²`); the loader rejects a part that leaves its lot, so no
  stage can overflow into a neighbour. Parts cast their contact shadow along `ObjectLight` (one light, D-038 B1/H4).
- **Icons** reuse the glyph grammar: the type's base + mark (the gallery's mapping — university Portico+Letters,
  hospital Hall+Medicine, factory Tower+Industry, research institute Dome+Flask), the **specialization's** mark when one
  is reported, and the reported **state** through the existing `GlyphState` alphabet (one "under construction"
  treatment: `InProgress`, never a second one). Icons carry **no maturity-stage pips**: the grammar's pips mean
  building maturity, which no source reports (DD-T5/DD-T9 open); the visual stage is shown by the parts and, tagged
  "view", in the details.
- **Infrastructure types** style edges by stage (road: track → road → paved road, by the reported `grade`).
- **Agent types** choose the silhouette family by category: military → `Standard` (banner), person → `Emblem` (the
  object mark alone), group → `Node` (a circled mark). Never a figure (D-038 C1/C3); the loader rejects a mismatch.
  (The Trees gallery draws role-bearing people as `Standard` unit classes; the map draws them as persons — see WV-07.)
  An agent with no reported count is drawn at the BASE size band, never at a size it did not report.
- **Aggregation** (`individualUpTo`, provisional — D-038 H8 is unruled, WV-03): per settlement and visual type the first
  N reports by (established, key) draw individually; the rest share one cluster token. One report is always one token;
  its multiplicity is a `xN` badge. The civilization legend sums multiplicity over drawable reports.
- **Four object marks** were appended to `GlyphDomain` (Lyre 33, Compass 34, Anchor 35, Drop 36). Every pre-existing
  glyph bakes byte-identically: 25,578 specs, SHA-256 `584A32A8…B657` measured on `78e6b58` and asserted on HEAD by
  `GlyphByteIdentityTests` (reference platform, ADR-022).

## 6. Composition and placement (task Part 6 under D-038 H2/H3/H4)

D-038 H2 (`d038:96`): districts inside a settlement are abstracted (`d009-d010:15`) — "a university is NOT an object at
coordinates inside a settlement; it is a CAPABILITY THE SETTLEMENT HAS". H3 (`d038:146`) adopts a composed settlement
sprite and rejects a glyph ring as the primary treatment; H4 (`d038:163`) solves the combinatorics by parts assembled at
draw time. The task asks for deterministic building placement within a settlement. Both are honoured by drawing each
structure as a **part of its settlement's one composed sprite**, in a **composition slot** — sprite units, never world
coordinates, never shown as a position, never a hit result other than (the structure's id). This interpretation is
registered for the Director (WV-02); districts and clusters are not implemented.

- **Fixed lattices.** Structure slots (rings of 6/12/18 in sprite units) and residential block lots (twelve rings, 468
  lots — enough for the largest stage with every slot occupied) are fixed by content; neither depends on stage,
  population or structure count. The loader proves no two slots overlap, bounds every lattice (at most 24 rings, 360
  lots a ring, 4,096 lots) before building it, and rejects a stage with more blocks than the lattice has.
- **Priority.** Claims (the individually drawn structures and cluster tokens) are ordered by (established ascending,
  unreported last; key ordinal). The demo DERIVES `Established` as an entity's first step and the live adapter reports
  the StructureRow's append-only index, so in both a later arrival ranks after what already stands.
- **Slot.** For each claim: `h = fmix64(FNV-1a64(UTF-8(settlementKey) · 0x00 · UTF-8(claimKey)))`; rings are tried
  inner to outer; on ring r the probe starts at `h mod n_r` and steps +1 (wrapping). At most every slot is probed once;
  if all are taken the claim goes to a fixed spill position. No loop, no `GetHashCode`, no process-dependent value
  (the hash is pinned by literal vectors).
- **Blocks.** The stage shows the first `blocks` lots in a fixed order (rings inner to outer, each ring in an even fill
  order) that do not lie under an occupied slot. A block under a slot is hidden where it stands; the next lot in the
  fixed order is shown instead — nothing is moved or re-indexed.
- **Order of operations.** Stage → slots over ALL of a settlement's reports whatever their visibility → visibility
  filter → level of detail → viewport. Later steps only filter; none re-places, so no filter can move a building.
- **Guarantees (tested):** identical reports → identical slots, in any input order, in any process; a later arrival
  never moves an earlier structure (demo and live); a settlement stage change, a structure stage change, another
  structure's visibility, a zoom or a pan move nothing; across every consecutive pair of demo steps no structure jumps.
  **The one accepted relocation is a removal:** it never moves a claim that ranks before the removed one; claims ranked
  after it may move. Without aggregation a moved claim only moves EARLIER in its own probe sequence; with aggregation a
  removal can promote a clustered report or re-rank the cluster token, so a later claim may also move later (review
  det-5 / place-3). A source that reports no arrival order at all falls back to key order, and then an arrival with a
  LOWER key can displace — no shipped source is in that case.

## 7. The demo world (`demo-world.json`) — DEMONSTRATION / PLACEHOLDER

`"placeholder": true` and a DEMO notice are mandatory; every report's note says DEMO; every frame carries the red
"DEMO / PLACEHOLDER WORLD - not simulation output" banner; the demo draws on its own paper, never on the live map.
Timeline semantics: an entity exists from its first entry; values merge forward; inputs and labels merge **by name**
(null deletes one); `removed` ends it from that step (exclusive, and must be last); entries apply in step order
whatever the file order; duplicate (entity, step) entries are errors. Construction progress (`progress`, `percent`,
`turnsRemaining`) is rejected: M4-D has no progress and DD-08 is open.

| step | task state (Part 5) | featured city Veyra (population, demo) | what changes |
| --- | --- | --- | --- |
| A | State A — small settlement | 1,500 → 6 blocks | no university, no hospital; one dotted track |
| A2 | growing | 6,000 | more blocks; the track becomes a road; a granary |
| B | State B — first university and hospital | 14,000 (Town) | University #123 (stage 1), Hospital #7 (stage 1), granaries ×2, a workshop; more roads |
| C | State C — developing city | 45,000 (City) | #123 expands (wings); University #131; Hospital #7 ward wing; Factories #40 and #41; reservoir; aqueduct, port, paved road; 10 universities across the demo world |
| D | State D — large city | 180,000 (Large city) | #123 campus; #131 academic block; #140; Hospital #7 medical complex, #19; three factories; military academy; research institute; rail; 20 universities |
| E | civilization scale | 240,000 | 30 universities reported across the demo world (all polities) in 8 settlements; a fifth Veyra university joins a cluster token; #155 under construction (state only) |

Mobile agents (Part 14): Army #184 (500 personnel), #77 (50), #12 (5,000), Formation #21 and the tank formation stand
ON the road graph (edge + fraction — D-009's ruled shape; the view projects the continuous point); a remembered sighting
(Army #9, ghosted, at its last known map point) and a hidden army; a fleet at sea; a hero, a scientist and an engineer
placed deliberately overlapping, an artist, a musician; a leader and a notable placed AT Veyra; "The Band" (4 members),
a cultural group and an expedition — people and groups at free fractional coordinates, some agents with headings. The
off-graph positions (the fleet, the sighting, the people) are registered as WV-09.

## 8. Mobile agents

One report is one token, whatever it stands for: Army #184 with 500 personnel is one banner with "500" above it, one
hit region — the personnel count (a reported number) is not the render-entity count (one). The token is the
category's silhouette inside the owner's ink ring; an optional heading tick (normalised to [0, 360)); a count badge.
Tokens sit at their true continuous positions — overlap is allowed — and only LABELS declutter: clusters are the
connected components of "within 30 px", measured on world deltas × zoom (a pan never regroups them), over every
drawable agent before any viewport cull; a cluster's labels stack beside its lowest-id member in id order, and each
stacked label is a hit region for its agent. Settlement-attached agents fan beside the settlement, outside its sprite
and its click target, each at its own **fan lot**: a stable hash of (settlement, agent) into twelve fixed angles, probed
in arrival order (established, then key) — so an unrelated arrival never moves anyone; the fan's radius follows the
sprite's current size (a reported growth moves it outward with the footprint). **There is no movement, speed, pathfinding, combat, strength, morale, supply or Action Capacity
anywhere in this layer** (DD-06, DD-07, DD-10).

## 9. The scene

### 9.1 Layers (Part 11)
Fixed order, each painted into its own list: Background → SettlementFootprint → Infrastructure → Buildings →
Resources → MobileAgents → Labels → Selection → TransientUi. Within EVERY layer (labels included) entities draw in
DESCENDING id order, so the lowest id is on top — the same entity the hit ranking picks.

### 9.2 Level of detail
Sprite pixels per sprite unit = clamp(camera px-per-world-unit × `worldPerUnit`, min, max) — it grows with zoom and then
stops, so a sprite never implies land it does not occupy. Far: footprint only. Mid: the structures' composed building
PARTS in their slots (the composed sprite is the primary treatment at every level — never a ring of icons, D-038 H3),
with a small state glyph only when a source reports a state other than operational (H6). Near: the parts plus a glyph
badge (type or specialization mark, state ring) and structure labels. Detail is added with size.

### 9.3 Selection (Part 12)
`HitIndex`: a click is admitted by any region containing it; candidates rank by class priority DESC, the classes
following the LAYER order (stacked label, agent, resource, structure, infrastructure, settlement), then — because
within a class the region is what is drawn and the lowest id is drawn on top — by `WorldEntityId` ASC: what is clicked
is what is seen. The one exception is a region larger than its drawing (a settlement's standard 44 px target), ranked
(squared distance ASC, id ASC) like `SettlementSelection`. Hit rects are measured with the headless text measure in
every backend. A selection the current state no longer
reports is kept (it returns if the entity does), draws no ring, and the panel says "not reported in the current state";
it is never re-targeted by index or proximity. One `WorldUiState` per bundle: a selection never crosses live ↔ demo.

### 9.4 Details (the inspector)
`WorldInspector.Details(view, morphology, id)` is pure (no camera): it prints report fields and the view's own choices,
each tagged reported / view / not reported, world coordinates only, and the source's provenance ("DEMO / PLACEHOLDER"
or "LIVE simulation - read-only"). University #123 shows Type, Settlement, Owner, Visual stage (with the driver and the
view threshold that chose it), State, Specialization, its inputs, first reported, "a capability of its settlement - no
map position of its own". Army #184 shows Type, Personnel, Owner, Position, Heading (display only), Visibility. There
is no line for strength, speed or morale.

### 9.5 In the game
**V** cycles Off → **Live** → **Demo** → Off (not while the Trees overlay or a text field has the keyboard).
- **Live**: the world layer is drawn on the ImGui background list — over the terrain, network and SpriteBatch markers,
  UNDER the name labels and all chrome — through the game's own camera. It adds only what the game does not draw
  (composed footprints from Mid detail up, M4-D structures, deposits, notables); the terrain, the network and the
  settlement names stay the game's. A click goes to `SettlementSelection` FIRST (its marker and its name label are
  drawn over the layer); only a click it does not admit reaches the world layer (`WorldOverlayHost.LiveClick`), which
  claims structures, resources and people, painted with the CURRENT camera, and shows them in a small read-only card
  under the selection card. The game's `_selected` is passed IN to be highlighted; nothing flows back out, and no
  world selection ever reaches an order.
- **Demo**: a full-screen overlay on its own paper with its own camera and the DEMO banner. Drag pans, the wheel zooms,
  a click selects, Left/Right step the demo timeline (a preview control that changes only the demo's step), V or Esc
  closes. Map input is suppressed under it.

## 10. Preview and screenshots

`sim-ui --world-preview <dir>` paints `WorldPreview.Scenarios` to SVG with no window and no simulation;
`scripts/world-preview.sh <dir>` then screenshots each at 1280×800 with headless Chromium. The same table drives the
tests (each scenario renders bit-identically twice, carries the DEMO banner, and its selection exists at its step).

| # | frame | step | camera | selected |
| --- | --- | --- | --- | --- |
| 1 | small settlement | A | Veyra, 25 px/unit (near) | Veyra |
| 2 | growing settlement | A2 | same | Veyra |
| 3 | city with its first university | B | same | University #123 |
| 4 | university expanded | C | same | University #123 |
| 5 | multiple universities across cities | D | 7.6 px/unit (mid) | — |
| 6 | developed city, several institution types | D | Veyra, 25 px/unit | Hospital #7 |
| 7 | army with 500 displayed | D | 12 px/unit | Army #184 |
| 8 | several mobile agents | D | 16 px/unit | The Band |
| 9 | city, military and people | D | 9.5 px/unit | Formation #21 |
| 10 | zoomed-out civilization | E | whole world (far) | — |

## 11. Demonstration-only and view-only values

Demonstration (demo file): every population, capacity, served, staff, maturity label, multiplicity, count, name,
position, graph location, heading, visibility and step in `demo-world.json`; the demo polities, their names and ink seeds; the water.
View-only (morphology file): the settlement `population` thresholds 0/3,000/10,000/40,000/150,000 and the stage names;
block counts per stage; every structure stage threshold (university capacity 0/800/2,000/4,000; hospital served
0/20,000/80,000; factory staff 0/400/1,500; research capacity 0/600; reservoir capacity 0/5,000; granary and workshop
multiplicity 0/2; academy maturity ordinals); the parts; road grades; `individualUpTo` 4; LOD thresholds 7.5/14 px per
unit; sprite scale 1.0 world/unit clamped 1.2–28 px; lattice radii and counts (3 slot rings, 12 block rings);
agent size bands 0/1,000/10,000 → 0.86/1.0/1.14 (no count → 0.86); token sizes 26/32/40 px; the 12-angle fan; the
30 px declutter radius; polity inks and the ink-by-id rule. None is read by
the simulation.

## 12. Tests (Part 15) — `Sim.Ui.Tests/World`

| file | pins |
| --- | --- |
| `WorldContentTests` | shipped content loads with no diagnostics; placeholder marking; every required category; malformed content → diagnostics (ascending stages, part outside its lot, time-keyed input, overlapping slots, unknown glyph, non-Latin-1, person-as-banner, unknown member / duplicate key / null; demo placeholder, progress, dangling refs, two locations, removal, duplicate step) |
| `WorldPlacementTests` | FNV vectors and a pinned hash; permutation/repeat identity (bypassing the snapshot sort); tie-dense priority; later arrival never moves earlier; removal moves only earlier-in-probe; bounded spill; stage change, structure stage change, visibility change and crossing the aggregation limit move nothing; blocks hidden in place; fill order; demo timeline continuity |
| `WorldMorphologyTests` | cumulative parts per type; inclusive thresholds at every boundary (`BitDecrement`); unusable driver → base; no substitution; ordinal labels; driver chosen by content alone (capacity / label / multiplicity); SizeTier preferred and never named; heading normalisation; demo institutions appear and grow; 1/10/20/30 summary; clusters |
| `WorldSceneTests` | one army = one token, one count label, one hit region (50/500/5,000); Army #184 details are reported state only; The Band; silhouettes; continuous coordinates and linear projection; graph location; unresolved agents; attached people outside the settlement target; layer contiguity and order; selection at every zoom; slot hit; tie-dense coincident tokens (hit = drawn on top); remembered/hidden; pan-invariant declutter; render twice bit-identical and SVG-identical; step change changes the picture and not the UI state; painting leaves reports unchanged; missing selection; LOD; Latin-1; banner per scenario |
| `LiveWorldTests` | a full live frame leaves `WorldHash` unchanged; settlement rows, population, SizeTier, positions, key order; live stages never named; M4-D structures one per row, never expanded; notables born/defected/died; deposits; snapshot reuse; Latin-1 |
| `WorldBoundaryTests` | only `Sim.Ui.World.Live` names a Sim.Core type; no Trees model type; sources get-only, reports immutable; banned-construct scan of `Sim.Ui/World/**`; the four new marks distinct and object-named |
| `GlyphByteIdentityTests` | 25,578 pre-existing glyph specs bake byte-identically to `78e6b58` |
| `WorldReviewRegressionTests` | one pin per confirmed review finding (§15): attached-people lots, overlap hit order and stacked labels, hidden-resource gaps, removal under aggregation, headings, unreported counts, loader bounds and fallbacks, block counts per stage, mixed-bundle provenance, Mid-detail parts, the live-project map, one light, the contact sheet, hidden/remembered, the demo camera, the live click rule, label order, deep report immutability, declutter under large pans |

### 12.1 Mutation battery (one fix reverted at a time)

Run in a scratch worktree pinned to `e6b89a1` (re-run of four at `8a19711`), each step bounded by `timeout 300` (the clean
world suite takes 17–23 s including the build). A mutant counts as killed only by the pin written for it.

| mutant (the fix reverted) | result |
| --- | --- |
| M1 demonstration drivers stage live bundles | killed — `LiveStages_WithoutSizeTier_…` (also `SettlementStage_Prefers…`) |
| M2 live structures without arrival order | killed — `LiveStructures_ALaterProject_…` |
| M3 fan lots ignore arrival order | killed — `Det2_AttachedPeople_KeepTheirLot_…` |
| M3b fan lots by dense rank instead of hash | survived the first battery; killed after the pin was sharpened — `Det2_AnAgentOnItsPreferredLot_…` |
| M4 hits ranked by distance within a class | survived the first battery (the click point was equidistant); killed after sharpening — `Det3_PartlyOverlappingTokens_…` |
| M4b stacked labels in the token class | killed — `Det3_AStackedLabel_…` |
| M5 unreported count drawn at 1.0 | killed — `Semantics5_…` |
| M6 maturity pips reinstated | killed — `StructureIcons_CarryNoMaturityPips` |
| M7 cluster selectable through a remembered member | killed — `Vis2_…` |
| M8 live-project map consulted second | killed — `Arch6_…` |
| M9 resource row indexes only visible entries | killed — `Det4_…` |
| M10 heading keeps −0.0 | killed — `Det6_Headings_…` |
| M11 per-ring lot bound removed | **survived — equivalent**: the total-lots bound alone stops the loader before any lattice is built |
| M12b "at least one infrastructure type" removed | killed — `Load2_…` |
| M13 block lattice back to eight rings | killed — `Blocks1_…` |
| M14 provenance from the bundle flag only | killed — `Arch3_…` |
| M15 live map draws a placeholder view | killed — `Arch3_…` |
| M16 glyph ring at Mid detail | killed — `Arch5_…` |
| M17 shadow sign reversed | killed — `Arch7_…` |
| M18 hidden settlement anchors agents | killed — `Vis1_…` |
| M19b inspector prints hidden state | killed — `Vis1_…` |
| M20 world hit beats the settlement click | killed — `Auth3_…` |
| M21 labels drawn ascending | killed — `Doc1_…` |
| M22 demo camera's non-positive fallback removed | **survived — equivalent**: unreachable with the current fit rules (defence in depth) |

(M12 and M19 were first written as `if (false)`, which does not compile under warnings-as-errors; M12b/M19b are the
compilable forms.)

## 13. Decision register

Existing ids are reused; `WV-nn` is minted only for questions no register holds. RULED items are constraints this layer
obeys; OPEN items are the Director's, and the view slot is where the answer plugs in without a renderer rewrite.

| id | question | status | authority | view slot |
| --- | --- | --- | --- | --- |
| **WV-01** | What makes a settlement a town or a city — are settlement stages named, and by what? | OPEN | D-017 CLOSED (`m3-spec:15`: size drives the render-side footprint); D-038 H3 | `settlementStages.drivers` (live: `sizeTier`, unnamed; demo: `population`, named, DEMONSTRATION) |
| **WV-02** | Task Part 6 (placement / districts / clusters inside a settlement) vs D-038 H2/H3 and `d009-d010:15` (districts abstracted; no internal map; glyph ring rejected) | OPEN — implemented provisionally as composition slots inside the one composed sprite (H4); no structure has world coordinates | D-038 H2 `:96`, H3 `:146`, H4 `:163` | `CompositionLayout`, `Composition` (§6) |
| **WV-03** | Past the parts-legibility limit: merge, abstract upward, or show only the most significant? How many individually? | OPEN | D-038 H8 `:201` (explicitly unruled) | `aggregation.individualUpTo` + cluster token; order (established, key) is arbitrary, not a significance judgement |
| **WV-04** | Object-tier composition (settlement sprites, building parts, agent tokens) authored ahead of the inserted visual milestone | OPEN — extends X-VIS-1 | D-038 E1 `:51`, F3 `:67`, H7 `:193`; audit X-VIS-1 | removable by deleting `Sim.Ui/World`, `Sim.Ui/UiContent/world`, `Sim.Ui.Tests/World`, the preview flag/script and the `SimUiGame` hooks; the additive grammar pieces (four appended marks, `ObjectLight.ShadowOffsetPx`, the sheet's row formula) change no earlier bake |
| **WV-05** | Polity names and map colours | OPEN | D-042 (roster: identity + command source only) | `PolityReport.DisplayName`, `InkSeed`; `polityInks` (ink = seed mod n, by id alone) |
| **WV-06** | Are bands and cultural groups simulation entities, and what carries their members? | OPEN — no carrier; demo only | D-038 C1/C3 (depiction as tokens — RULED) | `AgentReport` with category `group`, `Count`, `Members` |
| **WV-07** | Roles for important people (scientist, engineer, artist, musician, hero, leader) — and one depiction: the Trees gallery draws them as `Standard` unit classes, the map as `Emblem` persons | OPEN — `NotableRow` has no role; live notables are "person" | D-038 C1 (tokens only — RULED); T4.8 notables | `AgentReport.DisplayType`; `agentTypes[].glyph` |
| **WV-08** | The map scale for visual sizing (how big a city sprite is relative to terrain; token sizes) | OPEN — view choice | D-009 sprawl ("footprints ... consuming real farmland") is not implemented | `sprite`, `lod`, token sizes |
| **WV-09** | Off-graph positions: a fleet at sea (D-009 names no sea-lane edge), a remembered sighting at its last known point (D-039 A2's lagged position channel), people and groups at free coordinates (task Part 9) | OPEN — the demo uses free positions only for these; land armies stand on the graph | D-009 `d009-d010:19, :51`; D-039 A2 `d039:13` | `AgentReport.Position` vs `GraphLocation` |
| DD-11 / CR-010 | What IS an institution; which milestone owns it; is a university one entity, a count, or a capability? | OPEN | audit §5; C12 | `IStructureViewSource`; `StructureReport` (per-entity OR a multiplicity row) |
| DD-T5 | What "realized / operational / mature" measure, and who reports stage and progress | OPEN | the-trees-ui §16; C13 (D-038 H2/H5 bind the display side) | `visualTypes[].stageBy` (input or label), `StructureReport.State` |
| DD-08 | Construction points / banked progress | OPEN (M4-D has none — RULED for M4) | audit §5; C14, C15 | `StructureReport.State` only ("under-construction" if a source reports it); demo forbids progress |
| DD-10 | Placement of strategic armies, the AutoResolver, sieges | OPEN | audit §5; C19 | `IMobileAgentViewSource`; armies march on the graph (D-009 — RULED) → `AgentReport.GraphLocation` |
| DD-06 / DD-07 | War pulses; Action Capacity | OPEN | audit §5 | none — the view has no movement, cadence or capacity |
| DD-09 / DD-T9 | Veterancy, personnel and building-maturity carriers | OPEN | audit §5; the-trees-ui §16 | `AgentReport.Count` (personnel, reported only) |
| D-011 §2 | Army personnel are real people moved by the Ledger | RULED | D-011 §2 | `AgentReport.Count` is reported, never derived |
| D-009 ¶2 | Infrastructure is one multi-modal graph of typed nodes and edges | RULED | `d009-d010:12`; C20 | `IInfrastructureViewSource` (nodes + edges); a port is a node type |
| D-040 B1 / D-039 A2 | Knowledge is a computed per-polity extent; position and strength are separable lagged channels; M7 | RULED shape, OPEN producer | `d040:37, :158`; `d039:13` | `ReportedVisibility`, `WorldSources.Observer` (live: NotModelled, omniscient) |
| DD-04 / DD-T8 | Ages as label or input; Age → visual register | OPEN | audit §5 | not used: every glyph here is `EraRegister.Primitive` |

## 14. What this is not

No Age rules, attainment or surge; no global Δt; no research points, tech costs, knowledge generation, decay or
diffusion; no institutional economic effect; no construction economy; no catch-up; no combat, military strength,
movement or Action Capacity; no population, city-growth, economic or migration simulation; no person mortality or
defection logic; no progression-graph authority. The demo's numbers are authored, not simulated.

## 15. Review record

An adversarial review of `4093910` ran six finders (authority, gameplay rules, determinism, correctness, test teeth,
architecture), each followed by an independent verifier in its own worktree pinned to `4093910` that tried to refute
every finding and measured before confirming (scratch probes and bounded runs). **39 findings: 36 confirmed, 3
refuted, 0 undecided.** No fix was applied before its verdict. All 36 confirmed findings are fixed (commits `cf57e51`
to `9a20b86`; duplicates across dimensions fixed once). The verifiers' severity notes were taken into account — several
"major" items had no visible in-game effect today (e.g. the turn-0 demonstration stage) — but each property was still one
the system ought to have. Refuted: per-snapshot placeholder flags never read (auth-4: no shipped mixed bundle — hardened
anyway under arch-3), the legend pooling polities (semantics-4: wording only; the demo now says "across the demo world"),
and gallery vs map glyphs (arch-4: intended and documented; noted under WV-07).

Main fixes: live settlements are never staged by demonstration thresholds; live structures compose in StructureRow
order; attached people take stable hash fan lots; hits rank what is drawn on top; no maturity pips without a maturity
source; Mid detail draws composed parts; hidden/remembered never leak through anchors, clusters, the legend or the
inspector; loader bounds and fallbacks; one light (`ObjectLight.ShadowOffsetPx`, additive — no bake changes); contact
sheet rows follow the alphabet (`glyph-sheet-v1.png` regenerated: the four appended marks fill row 23); land armies on
the road graph; the live click goes to `SettlementSelection` first; the removal guarantee in §6 corrected. Mutation
evidence: §12.1.

## 16. D-043 status note (2026-09-29, append-only; nothing above is rewritten)

The Director's rulings of 2026-09-29 (`docs/d043-civilization-progression-ages-and-mobile-agents.md`)
bear on the §13 register. **No row above is edited, and each keeps its status as written.** The
rulings are D-043's, their collisions with frozen items are CR-017's, and the map from each row to
what was ruled is D-043 PART G. No code or content was changed by this note.

- **WV-01.** Settlement visual class is "primarily determined by population" (D-043 D1).
  - The live `sizeTier` driver diverges from this, as does the rule that a demonstration
    (population) driver never stages a live settlement (§5, D-043 F36).
  - Thresholds and names remain open.
- **WV-02 and WV-03.** Institutions are embedded in the footprint and aggregated per type, with
  count stages 1 small, 10 expanded, 20 campus and 30 complex, and the number shown at high zoom
  (D-043 D2). How many distinct types one footprint shows stays with D-038 H8. Three things here
  diverge:
  - `individualUpTo` 4;
  - capacity-based university stages;
  - the step B–E legend, whose 1/10/20/30 are civilization-wide counts rather than
    per-settlement stages (§5, §7; D-043 F37).
- **WV-06 and WV-07.** Bands and cultural groups are MobileAgents. So are role-bearing people,
  whether special people or others (D-043 B1, B3, B6). What carries their members is D-043 F13
  and CR-017 §7.
- **WV-09 and DD-10.** MobileAgents may move off-road at continuous x/y (D-043 B7); the collision
  with D-009/D-010 is CR-017 §5. Until CR-017 §5 is ruled, "the ruled shape for armies" (§4, §7,
  §13's DD-10 row; `WorldReports.cs`) is a stale characterisation (D-043 F34). The data model
  already carries a continuous position.
- **DD-06 and DD-07.** War Pulses and Action Capacity are now defined at design level only
  (D-043 B2, B8; CR-017 §3, §6). §8's "no movement … anywhere in this layer" diverges from
  D-043 D4, under which MobileAgent movement is animated continuously (D-043 F39).
- **DD-08.** Construction capacity is not bankable (D-043 C1).
- **DD-11.** Institutions are implied to be countable instances per settlement and type (D-043
  C2, D2).
- **DD-04 and DD-T8.** Nine named Ages (D-043 A1). Whether the Age may be read is CR-017 §1; the
  visual register binding is still open.
- **Visual pace.** Change is gradual and noticeable over ~10–15 turns (D-043 D3). The only live
  stage, `sizeTier`, changes "a handful of times per campaign" (D-043 F38).
