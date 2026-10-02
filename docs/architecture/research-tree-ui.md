# Knowledge & Technology progression screen

Branch `ui-research-trees`, based on `research-progression-foundation` @ 3e5647f (rebased; that commit renamed six nodes and changed prerequisites, baseline entries and Eurekas — no test used a fixed name it changed). This is UI only: it touches `Sim.Ui/**`, `Sim.Ui.Tests/**` and `docs/architecture/` (this document, the previews and their render script). The simulation is not changed.

## What it is

The screen is a full-screen progression view. Open it with **K** or the **Knowledge [K]** button in the status band. Close it with **Esc**, **K** or the close button.

- **Lens bar.** It shows all seven lenses of the Director ledger §3, in ledger order: KNOWLEDGE & TECHNOLOGY, TECHNIQUES, INSTITUTIONS, INFRASTRUCTURE, INDUSTRY, MILITARY, APPLICATIONS. Each tab carries an honest status: *simulated*, *partial* or *not yet simulated*.
  - **KNOWLEDGE & TECHNOLOGY** is fully functional.
  - **TECHNIQUES** and **APPLICATIONS** list what the completed nodes declare.
  - **INSTITUTIONS** shows the adopted civics (the completed Civics nodes, per ruling 11) and the institutions that the player's knowledge makes eligible.
  - **INFRASTRUCTURE** and **MILITARY** show which entities the player's knowledge makes eligible, and label that as knowledge eligibility only.
  - **INDUSTRY** shows "not yet simulated". No data is invented for any lens.
- **Tabs under KNOWLEDGE & TECHNOLOGY.** The Technology tree and the Civics tree are two separate graphs. Press **1** or **2** to switch between them. The tab bar also shows how many nodes are completed in each tree, a capsule for the current target with its progress (or "target ordered, applies at End Turn"), and a legend that separates node states and AND/OR edges.
- **Canvas.** The canvas is a deterministic layered graph generated from `research.json` through `ResearchContent`:
  - **Columns** come from content `Depth`. A node is moved right if needed so that every in-tree prerequisite comes earlier. Empty columns are removed.
  - **Column header = depth tiers.** A sticky header labels each column "TIER n" with the honest *range* of Ages its nodes carry (for example "Ages II-VII"). Columns are prerequisite depth, and one depth holds nodes of several Ages, so the old "dominant Age per column" header read out of order (Age II before Age I); a range never misstates a card. The Age itself is a **per-card badge** (roman numeral). Ages are metadata only: nothing is gated on them (law 4).
  - **No gutter.** The graph starts at the canvas's left edge; the camera clamp allows at most 16 px of empty field past any edge, so opening on a column-0 frontier no longer leaves half the canvas empty.
  - **Lane chips** sit in each lane's header strip (name, node count, disclosure triangle). The lane whose header has scrolled above the view gets its chip pinned in the control row, so a chip never covers a card. **Clicking a chip collapses or expands the lane** (the layout is recomputed; hidden cards are not painted, hit-tested or drawn in the minimap, and their edges are hidden).
  - **Control row** (opaque, under the tier header): JUMP TO TARGET / JUMP TO FRONTIER (selects the target, or the leftmost available node, and re-expands its lane if collapsed), FIT TREE, and SHOW/HIDE MAP.
  - **Lanes** are Main Trunk plus the content's five subtrees (Military, Medicine, Engineering, Natural Science, Agriculture) in content order. The Civics tree has one lane per domain.
  - **Cross-tree prerequisites** appear as anchor tokens in their own lane. For example, Written law needs one of three Technology writing systems.
- **Edges.** Prerequisite edges are Béziers. A solid edge means *all of*: the prerequisite is in `Predicate.MustHoldAtoms`. A dashed edge means *one of*. Edges from completed nodes are gold. Edges into and out of the hovered or selected node are highlighted.
- **Navigation.** Drag to pan, use the wheel to zoom at the cursor, and use WASD or the arrow keys to scroll. Pan and zoom ease smoothly. Click the minimap to jump. The minimap docks bottom-right on a dark backdrop; if the selected or hovered card would sit under it, it moves to bottom-left, and it can be hidden.
- **Culling.** Cards and edges outside the view are skipped. Cards are drawn with less detail when zoomed out: colour only below 0.28× zoom, and name only below 0.45× zoom.
- **Node cards** show:
  - the name, an Age badge, and a stripe in the branch colour;
  - the state, which is one of Completed (gold check), Researching (cyan ring with a progress arc), Available, Partly researched, or Locked (padlock);
  - the EffectiveCost, plus the discount from university modifiers when there is one;
  - one Eureka pip per Eureka, filled when it has fired;
  - a progress bar when progress is greater than 0;
  - "needs university" when only the research stage blocks the node.
- **Detail panel** (for the hovered or selected node) shows:
  - BaseCost, then each specialised-university factor, then EffectiveCost, with a flag when the floor applies;
  - progress in RP, with an estimate of turns at the current RP pool;
  - the prerequisite expression and each prerequisite, marked done or missing and *required* or *one of*;
  - lock reasons: missing prerequisites, research stage not reached (with the stage expression), or a recursive node waiting for its subtree;
  - the Eureka pool: credited against the 40% ceiling, split by source (Eureka and foreign exposure, plus any exposure on offer);
  - each Eureka's text, weight, maximum credit, and status (fired, holds now, condition, or not evaluable yet together with the system that owns it);
  - university relevance (primary or secondary), what the node opens the way to, and the node's description;
  - a **Set as research target** button.
- **Orders.** Clicking an available node, or the button, returns the `SetResearchTarget` order built by the existing `ResearchOrderFactory`. The game logs it through `UiSession.EmitResearchOrder`, which uses the same factory. The screen never writes state. It marks the order as pending until End Turn, and it does not re-issue an order for the current target.

## Code (pure model and layout, kept apart from rendering)

| File | Role |
|---|---|
| `Sim.Ui/Progression/ResearchGraph.cs` | One tree as a graph built from content: vertices, external anchors, and AND/OR edges equal to `PrerequisiteNodes` |
| `Sim.Ui/Progression/ResearchTreeLayout.cs` | Deterministic layered layout: columns, lanes, Age spans, and barycentre ordering with a (score, vertex) tie-break |
| `Sim.Ui/Progression/ResearchNodeViews.cs` | `ResearchSnapshot`: each node's state, costs, Eurekas, pool and lock reasons, all read through `ResearchQuery` once per world |
| `Sim.Ui/Progression/Lenses.cs` | The seven lenses and their pages, using real data only |
| `Sim.Ui/Progression/ProgressionCamera.cs` | Pan and zoom with eased targets, zoom-at-cursor, and clamping |
| `Sim.Ui/Progression/ProgressionScreen.cs` | UI state, painting into a `DrawList`, hit regions, and input that returns `ProgressionCommand` (an order, never a write) |
| `Sim.Ui/Progression/ProgressionPalette.cs` | Colours |
| `Sim.Ui/Progression/ProgressionPreview.cs` | Headless previews from a real stepped world |
| `Sim.Ui/Render/{DrawList,SvgWriter,StrokeGeometry}.cs` | **Ported** from `claude/civdemo-work-b1z2y4`, with the glyph path removed |
| `Sim.Ui/ImGuiIntegration/DrawListImGuiBackend.cs` | **Ported** from the same branch, with the glyph path removed: replays a `DrawList` into ImGui |
| `Sim.Ui/SimUiGame.cs` | Wiring: the K toggle, the status-band button, input routing while the screen is open, and the background-drawlist render |

### What was reused from the Trees UI foundation (`origin/claude/civdemo-work-b1z2y4`)

- **Ported directly:** the backend-agnostic `DrawList`, `RectD`, `ITextMeasure`/`ApproxTextMeasure`, the `Ink` wrap/fit helpers, `StrokeGeometry` (dashes), `SvgWriter`, the ImGui draw-list backend, and the headless-Chromium screenshot flow (`scripts/trees-preview.sh` became `docs/architecture/research-tree-ui/render-previews.sh`).
- **Re-implemented in its style:** the layered layout, which keeps the old branch's approach of columns, bands, and barycentre sweeps with integer tie-breaks.
- **Not ported:**
  - its demo and placeholder JSON (`the-trees.json`, `demo-state.json`), its content loader and its state sources: this screen is driven only by `ResearchContent` and `WorldState`;
  - the glyph baker: the cards are drawn with vector primitives instead.

## Visual evidence

Run `docs/architecture/research-tree-ui/render-previews.sh [dir]`, which runs `sim-ui --research-preview` and then screenshots the output with headless Chromium. It founds the canonical world with seed 42 (256 px, 4 settlements) through `UiSession`. Whenever the player has no target, it logs the order for the cheapest available Technology node (`ResearchQuery.CheapestAvailable`) and presses End Turn. It stops once 5 nodes are complete and the current target holds partial progress, which happens at turn 87.

| Preview | Shows |
|---|---|
| `research-tree-ui/01-technology-frontier.png` | The Technology tree at the frontier. Root and tuber cultivation is being researched at 5% (21 / 440 RP). Five nodes are completed (gold), the available ones are bright, and the rest are locked. |
| `research-tree-ui/02-technology-overview.png` | All 424 Technology nodes fitted to the canvas (top-aligned): the trunk and five subtree lanes with their chips, and the minimap. |
| `research-tree-ui/07-technology-trunk-collapsed.png` | The same overview with the Main Trunk lane collapsed via its chip ("176 hidden"): the five subtrees move up. |
| `research-tree-ui/03-technology-locked-subtree.png` | Magnetic compass (Military): locked by a missing prerequisite **and** the research stage. Shows its Eureka at the 40% ceiling and its university relevance. |
| `research-tree-ui/04-civics-tree.png` | The separate Civics graph: Technology prerequisites appear as anchor tokens, and Written law's three *one of* edges are dashed. |
| `research-tree-ui/05-lens-institutions.png` | The INSTITUTIONS lens: adopted civics (0 of 6) and knowledge-eligible institutions. |
| `research-tree-ui/06-lens-industry.png` | The INDUSTRY lens: not yet simulated. |

## Tests (`Sim.Ui.Tests/ProgressionScreenTests.cs`)

The tests use one real world, stepped as described above.

- The edges are exactly every node's `PrerequisiteNodes`, in source order. Each edge is AND exactly when it is in `MustHoldAtoms`, and both AND and OR edges exist.
- Technology and Civics are separate graphs: each has its own node count, the Civics tree's anchors are Technology nodes, and the tab switches graph.
- The layout is bit-identical across two computations, places every vertex, has no overlapping boxes, puts every prerequisite in an earlier column, and puts each node in its branch's lane.
- A tie-dense cell-order test checks that equal barycentres break on the vertex index.
- Every node's state, availability, target, progress, EffectiveCost, BaseCost, Eureka pool and lock reason agrees with `ResearchQuery`, compared with exact equality.
- Clicking an available card returns exactly `ResearchOrderFactory.SetTarget(...)`, leaves the world hash unchanged, and does not re-issue on a second click. Locked, completed and current-target cards only select.
- The detail button issues the same order. After `EmitResearchOrder` and End Turn, the node is the target (or completed), and the pending hint is cleared.
- All seven lenses are present. INDUSTRY is not simulated and has no sections. INSTITUTIONS and TECHNIQUES list exactly the completed civics and techniques.
- Culling draws less than a quarter of the nodes at the frontier zoom, and the fitted overview draws all of them. The SVG output is deterministic.
- Zoom-at-cursor keeps the world point under the cursor once the easing settles.
- Every node's Age lies inside its column header's Age range (both trees).
- At the frontier, the leftmost cards start within the edge slack of the canvas's left edge.
- Clicking the Main Trunk chip hides exactly that lane's cards (not hit-testable), leaves other lanes intact, and JUMP TO TARGET re-expands it and selects the target.
- With the selected card placed exactly under the default minimap dock, the minimap does not intersect it (mutant check: removing the dodge fails this test); the map toggle removes the minimap hit region.

## Gaps and notes

- `ResearchContent` has no per-node icon or art, so cards use vector primitives only.
- Lanes are fixed (trunk plus content subtrees). The tall trunk lane is real data; it is handled by collapse/expand and the jump control rather than by compacting the layout.
- Lane collapse state is UI-only and per tree; it is not persisted between sessions.
- The game path, which draws with ImGui's background draw list, could not be run in this container because there is no GL context. Everything except the replay into ImGui is covered headlessly by the same `DrawList` used for the previews.
- Text in the previews is measured with `ApproxTextMeasure`. In the game it is measured with the real fonts.
- Content changes on the base branch (strings, prerequisites, counts) flow through automatically. No IDs are hard-coded except `law_code` and `cordage` in two tests, which the existing tests already rely on.
