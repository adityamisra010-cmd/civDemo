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
- **Canvas — one scroll axis (vertical).** The canvas is a deterministic layered drawing generated from `research.json` through `ResearchContent`. Director feedback (two items) set two rules: the tree may be scrolled in only one direction, and its background must not be a tangle of lines. So:
  - **Tiers run top to bottom.** A tier is content `Depth`, pushed down where needed so that every in-tree prerequisite sits in an earlier tier. Empty tiers are removed. Each tier band opens with a label strip, for example "TIER 3 Ages I-VI - Prehistoric / Stone Age to Medieval". The strip gives the honest *range* of Ages in the tier, always with the full Age names. Ages are metadata only (law 4).
  - **Lanes run across the width, always in the same left-to-right order.** Technology has the external lane (only when present), then Main Trunk, then Military, Medicine, Engineering, Natural Science and Agriculture in content order. Civics has the "From Technology" anchor lane and then one lane per domain.
  - **Each lane has a segment in each tier.** Most lanes are empty in most tiers (the trunk owns tiers 1-14, and Engineering owns the late tiers). With fixed full-height lane columns the drawing was about 30,000 px tall and mostly empty. Instead, each tier divides the width among the lanes that have nodes in that tier. Each of those lanes gets a *segment*: a box tinted in the lane's hue, with a coloured cap and the lane's name. A cell's nodes wrap into rows of the segment's slot count.
  - **Allocating slots.** The number of slots across is fixed by the viewport (`floor(width / (min card + gap))`). Slots go one at a time to the lane where the extra slot reduces the rows most. The objective is to minimise (max rows, sum of rows), and ties go to the lowest lane index. Only integers are used, so the result is deterministic.
  - **The width always fits.** `TreeLayoutOptions.ViewportWidth` is the canvas width minus the overview strip. The layout is recomputed whenever that width changes (window resize, map toggle, lane collapse), so at the default zoom (1.0) there is never horizontal overflow. The camera locks the x axis whenever the drawing fits. Sideways drag, sideways keys and the wheel never move the view horizontally. The wheel scrolls vertically, and **Ctrl+wheel** zooms. Zoom is still available. Above 1.0 the drawing may become wider than the view, and only then can it pan sideways.
  - **Sticky lane header.** Above the canvas there is one chip per lane, in lane order: name, node count, and a disclosure triangle. **Clicking a chip collapses or expands the lane.** A collapsed lane takes no width in any tier, and its cards are not painted or hit-tested.
  - **Control row.** It shows the tier at the top of the view (with its full Age range), the highlight legend (*requires*, *leads to*, dashed *one of*), and four buttons: JUMP TO TARGET (or JUMP TO FRONTIER when there is no target in this tree; it selects the node and re-expands its lane), LAGGING BRANCH (returns to the default view), FIT, and HIDE/SHOW MAP.
  - **Overview strip** (replaces the minimap). It is a narrow vertical bar docked to the right of the tree viewport and outside it, so it can never cover a card. It shows every card coloured by state and the visible band. A click scrolls to that height.
- **Default scroll: the least-developed branch.** When a tree opens, it is shown at zoom 1, scrolled so that the frontier of the least-developed branch sits about 28% down the canvas. It is neither the target nor the most advanced research. Nothing is selected.
  - *Definition* (`ResearchTreeLayout.LeastDevelopedFrontier`): consider each non-external lane that has nodes. The lane's **frontier** is its not-yet-completed node (available, target, partial or locked) with the smallest key (tier, slot in its cell, vertex index), which is its earliest node along the scroll axis. The **least-developed lane** is the one whose frontier tier is smallest. Ties go to the lowest lane index (stable lane order). Collapse state is ignored, because it is UI state and tiers come from content. If every node is complete, the view opens at the top.
  - In the seed-42 world this is the Main Trunk in tier 1. The tests also build worlds where the answer is a mid-tier Medicine node lying above the target and the most modern research.
- **Edges: no background lines.** Nothing is drawn between cards by default. Each card carries short **stubs**:
  - an orange up-triangle with its prerequisite count and a blue down-triangle with its dependent count;
  - a **cross-lane port chip** that names prerequisites living in another lane or in the other tree. One such prerequisite reads "needs Artificial satellite (Engineering)". Several read "needs 3: Engineering, Natural Science".
- **Hover/selection highlight.** Hovering a card (or, failing that, selecting one) does three things:
  - Its **direct prerequisites** get an orange ring, and their edges are drawn in orange.
  - Its **direct dependents** get a blue ring, and their edges are drawn in blue.
  - **Everything else is dimmed.**
  - Edges are **orthogonal routes** (`ResearchTreeLayout.Route`). Horizontal runs use only row gaps, which every lane's rows in a tier share, so no card sits in them. Vertical runs use the prerequisite's own column for the short elbow from the last row of a tier into the first row of the next tier. Otherwise they use one of two reserved **edge spines** at the left and right edges, whichever is nearer, so a bundle of edges runs together. A test checks that no route of any edge in either tree passes under a card.
  - Dashed lines mean *one of* and solid lines mean *all of* (`Predicate.MustHoldAtoms`).
- **Culling.** Cards outside the view are skipped. Cards are drawn with less detail when zoomed out: colour only below 0.28×, and name only below 0.45×.
- **Node cards** (about 150-214 px wide, sized to the viewport) show:
  - the name, the state icon, and a stripe in the branch colour;
  - the EffectiveCost (plus any university discount), and Eureka pips (filled once fired);
  - **the Age as numeral plus full name**, for example "II · Neolithic / Agricultural", never a bare numeral;
  - the dependency stubs and the cross-lane port (or "needs university" when only the research stage blocks the node);
  - a thin progress bar when progress is greater than 0.
  - The states are Completed (gold check), Researching (cyan ring with a progress arc), Available, Partly researched, and Locked (padlock).
- **Detail panel** (for the hovered or selected node) shows:
  - BaseCost, then each specialised-university factor, then EffectiveCost, with a flag when the floor applies;
  - progress in RP, with an estimate of turns at the current RP pool;
  - the full Age name in the header ("MILITARY · AGE IX - MODERN / CONTEMPORARY");
  - the prerequisite expression and each prerequisite, marked done or missing and *required* or *one of*, with its lane named when it lives in another lane or tree;
  - lock reasons: missing prerequisites, research stage not reached (with the stage expression), or a recursive node waiting for its subtree;
  - the Eureka pool: credited against the 40% ceiling, split by source (Eureka and foreign exposure, plus any exposure on offer);
  - each Eureka's text, weight, maximum credit, and status (fired, holds now, condition, or not evaluable yet together with the system that owns it);
  - university relevance (primary or secondary), what the node opens the way to, and the node's description;
  - a **Set as research target** button.
- **Orders.** Clicking an available node, or the button, returns the `SetResearchTarget` order built by the existing `ResearchOrderFactory`. The game logs it through `UiSession.EmitResearchOrder`, which uses the same factory. The screen never writes state. It marks the order as pending until End Turn, and it does not re-issue an order for the current target.

## Readability (M5 polish UR-4, dated 2026-10-06)

> **DATED NOTE 2026-10-06 (Director directive 2026-10-06 §2; M5 polish UI readability, packet UR-4).** The text
> above describes the screen as built; these points supersede it where they differ, and are kept beside it.
>
> - **Type by role, × the UI scale.** Every run is set at a `TypeScale` role (Caption is the floor) × `Scale` (the UI
>   scale the host passes); every strip, card, gap and hit rect is a reference length × `Scale`. Strips: lens bar
>   66, tab bar 58, lane header 40, control row 44 px at s = 1. Chrome controls answer hover.
> - **The card** is ~208–232 × 116 px at s = 1 (`TreeLayoutOptions`: MinCardWidth 200, CardHeight 116): the NAME at
>   the Body role in the heading face, whole, on up to two lines (`ProgressionScreen.TwoLines`: breaks at spaces or
>   hyphens; every one of the 598 names fits the narrowest card in every era, pinned with the real atlas); the COST
>   and the Age NUMERAL at the Data role ("1,050 RP · Age II"); the STATE LINE at the Body role in the state's ink —
>   "Needs Controlled fire (Lane)" (the first missing prerequisite), "Available · ~9 turns", "Researching · 13% · ~7
>   turns", "Known". The estimate to complete is on every era's card (it was A7+).
> - **The full Age name left the card**: it is the tier strip's (right above every card group, now at the Body role),
>   the detail panel's and the hover tip's. "Every card shows its numeral and full name" above no longer holds; the
>   card keeps the numeral beside the cost (a Director item in the stage-B report: restoring a full-name row costs the
>   Body-share census its margin).
> - **The minimum card width holds at every window.** `S` is what the width holds at the minimum card width — it was
>   forced up to the lane count (cards 123 px at 1366 × 768). A tier whose lanes do not fit side by side WRAPS its
>   lanes into further lane rows (`LaneSegment.RowBase`, `LaneWrapGap`); routes still never pass under a card.
> - **The detail panel** is 440 px × s, DOCKED from 1600 px × s; below that it is an overlay DRAWER over the tree's
>   right edge, left of the overview strip and under the lane header and control row, opened by selecting a node and
>   closed by its × or Escape (the first Escape closes the drawer, the next the screen). Its content is in decision
>   order — the node (branch, Age in full, name, state), COST and turns, the ACTION directly under them, ENABLES (the
>   content's capabilities, techniques, applications, unlocked entities, and the nodes it leads to), REQUIRES,
>   EUREKA, UNIVERSITY, ABOUT — and it scrolls (wheel; a bar and a "more below" mark), never dropping a line.
> - **State fills** (Known, Target, Available, Locked) are distinct surfaces: `EraThemes.DistinctStates` tints Known
>   and Target the least that puts every pair ≥ ΔE 12 where the era allows (≥ 8 in every era, pinned), with the body
>   ink ≥ 7:1 on each. Selected: a 2.5 px strong border; hovered: the strong border and a 2 px lift.
> - **Lane chips** name their lane in mixed case at the Caption role (capitals did not fit 1600 px); the lane segments
>   and the tier strips are set at the Body role on the field, in inks measured against the field.

## Code (pure model and layout, kept apart from rendering)

| File | Role |
|---|---|
| `Sim.Ui/Progression/ResearchGraph.cs` | One tree as a graph built from content: vertices, external anchors, and AND/OR edges equal to `PrerequisiteNodes` |
| `Sim.Ui/Progression/ResearchTreeLayout.cs` | Deterministic single-axis layout: tiers top-to-bottom, per-tier lane segments fitted to the viewport width, barycentre ordering with a (score, vertex) tie-break, orthogonal edge routes, the least-developed frontier, and the full Age names |
| `Sim.Ui/Progression/ResearchNodeViews.cs` | `ResearchSnapshot`: each node's state, costs, Eurekas, pool and lock reasons, all read through `ResearchQuery` once per world |
| `Sim.Ui/Progression/Lenses.cs` | The seven lenses and their pages, using real data only |
| `Sim.Ui/Progression/ProgressionCamera.cs` | Pan and zoom with eased targets and zoom-at-cursor. The clamp locks the x axis whenever the drawing fits. |
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
| `research-tree-ui/01-technology-default-open.png` | **The default-opened view.** The tree is at zoom 1, the width is fitted with no horizontal overflow, and the view is scrolled to the least-developed branch's frontier: the Main Trunk, tier 1. Five nodes are completed (gold). Root and tuber cultivation is the target at 5%. Tier 2 wraps the trunk's nodes into rows of six, and a Military segment appears in tier 3. There are no background lines. |
| `research-tree-ui/02-technology-hover-highlight.png` | **Hover highlight.** Satellite navigation (Military, tier 27) is hovered. Its three prerequisites in Engineering and Natural Science are ringed orange, and their routes run up the left spine. Its three dependents are ringed blue. Everything else is dimmed. |
| `research-tree-ui/03-technology-target.png` | JUMP TO TARGET: the target is selected and its dependents are highlighted. |
| `research-tree-ui/04-technology-locked-subtree.png` | A subtree node locked by a missing prerequisite **and** the research stage, with its Eureka and university relevance. |
| `research-tree-ui/05-technology-overview.png` | FIT: the overview zoom of all 31 tiers as one vertical strip, with the overview strip on the right. |
| `research-tree-ui/06-technology-trunk-collapsed.png` | The Main Trunk collapsed via its header chip ("176 hidden"). Its width goes to the subtrees, so tiers 1-2 are empty strips. |
| `research-tree-ui/07-civics-default-open.png` | The Civics tree as it opens. Technology prerequisites appear as anchor tokens in the "From Technology" lane. |
| `research-tree-ui/08-civics-hover-highlight.png` | Written law hovered: its three *one of* Technology anchors are dashed orange, and Systematic law (its dependent) is blue. |
| `research-tree-ui/09-lens-institutions.png` | The INSTITUTIONS lens: adopted civics (0 of 6) and knowledge-eligible institutions. |
| `research-tree-ui/10-lens-industry.png` | The INDUSTRY lens: not yet simulated. |

## Tests (`Sim.Ui.Tests/ProgressionScreenTests.cs`)

The tests use one real world, stepped as described above.

- The edges are exactly every node's `PrerequisiteNodes`, in source order. Each edge is AND exactly when it is in `MustHoldAtoms`, and both AND and OR edges exist.
- Technology and Civics are separate graphs: each has its own node count, the Civics tree's anchors are Technology nodes, and the tab switches graph.
- The layout is bit-identical across two computations, places every vertex, has no overlapping boxes, puts every prerequisite in an earlier tier **and above its dependent** (`From.Bottom < To.Y`), and puts each node in its branch's lane.
- A tie-dense cell-order test checks that equal barycentres break on the vertex index.
- Every node's state, availability, target, progress, EffectiveCost, BaseCost, Eureka pool and lock reason agrees with `ResearchQuery`, compared with exact equality.
- Clicking an available card returns exactly `ResearchOrderFactory.SetTarget(...)`, leaves the world hash unchanged, and does not re-issue on a second click. Locked, completed and current-target cards only select.
- The detail button issues the same order. After `EmitResearchOrder` and End Turn, the node is the target (or completed), and the pending hint is cleared.
- All seven lenses are present, and only real data is shown.
- **Single axis.** This test is a theory over 1024×700, 1280×800, 1600×1000, 1920×1080 and 2560×1440, run for both trees. It checks that:
  - the default zoom is 1;
  - the layout width and every card's right edge fit the tree viewport;
  - no card overlaps another;
  - the tiers are ordered;
  - sideways scrolling, dragging and the wheel leave `PanX` at exactly 0.
- The wheel scrolls exactly one step vertically, and Ctrl+wheel zooms.
- **Least developed.** Pure tests on constructed completion masks:
  - (A) a mid-tier Medicine laggard wins over an Engineering target placed 6 tiers later, and lies above it;
  - (B) a trunk node further back wins instead;
  - (C) tie-dense: two lanes have frontiers in the same tier, and the lower lane index wins;
  - when every node is complete, the result is -1.
  - On the screen, for both trees: `DefaultFrontierNode` equals the pure definition over the snapshot, its card is in view on open, nothing is selected, and JUMP TO TARGET still selects the target.
- **Hover.** For 40+ nodes across both trees, `Highlight()` returns exactly the content's direct prerequisites and the nodes that name this node as a prerequisite. Orange or blue edges are painted exactly when that side is non-empty. With no focus, no edge is painted.
- **Routes.** Every edge of both trees is routed orthogonally from the prerequisite's bottom to the dependent's top, and no segment passes under any card.
- **Ages.** `AgeLabel` gives "Age N - full name" for all nine Ages. Every visible card paints its numeral and full name. The tier strips name the Ages in full, no text is a bare "Age II", and the detail header carries the full name.
- Culling: the default view draws less than a quarter of the nodes. The overview draws more, each once, including every card in view. The SVG output is deterministic.
- Zoom-at-cursor keeps the world point under the cursor.
- Every node's Age lies inside its tier's Age range.
- Clicking the Main Trunk chip hides exactly that lane's cards and removes its segments, the width still fits, and JUMP TO TARGET re-expands the lane.
- The overview strip lies right of the tree viewport, and every card ends left of it. Hiding the strip gives the tree its width back.

## Gaps and notes

- `ResearchContent` has no per-node icon or art, so cards use vector primitives only.
- Lanes are fixed (trunk plus content subtrees) and keep their order. A lane's horizontal position varies from tier to tier because each tier shares the width only among the lanes present in it. Lane identity is carried by hue, a cap and a name on every segment, and by the fixed order.
- Long edges (many tiers) route through a spine at the tree's edge, so they are long, but they are drawn only on hover and run in channels that hold no cards.
- Lane collapse state is UI-only and per tree; it is not persisted between sessions.
- The game path, which draws with ImGui's background draw list, could not be run in this container because there is no GL context. Everything except the replay into ImGui is covered headlessly by the same `DrawList` used for the previews.
- Text in the previews is measured with `ApproxTextMeasure`. In the game it is measured with the real fonts.
- Content changes on the base branch (strings, prerequisites, counts) flow through automatically. No IDs are hard-coded except `law_code` and `cordage` in two tests, which the existing tests already rely on.
