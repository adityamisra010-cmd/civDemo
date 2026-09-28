# THE TREES — PROCEDURAL UI FOUNDATION AND EDITABLE DATA PLACEHOLDERS

**Status:** foundation built on the post-M4 working branch `claude/civdemo-work-b1z2y4` (not
`main`). UI and data infrastructure only. **No simulation file was touched.** The diff against
the M4 closure commit `de5e00e` over `Sim.Core Sim.Data Sim.Cli Sim.Tests .github` is empty.

Companion documents:

- [`ages-ui.md`](ages-ui.md): the Age view and the Age data structure.
- [`the-trees-content-schema.md`](the-trees-content-schema.md): the editable JSON, field by field.
- [`pre-m5-visual-system.md`](pre-m5-visual-system.md): the glyph grammar this builds on. Its
  §11 is the append-only amendment for this work.
- [`pre-m5-repository-audit.md`](pre-m5-repository-audit.md): why none of this state exists in
  the simulation yet, and the Director decisions DD-01..DD-12 cited below.

---

## §0 The rules everything below obeys

1. **The UI is a read-only observer.** It learns every state (node state, research points, Age
   progress, maturity, veterancy) through read-only interfaces, and it has no path to write any
   of it back. There is no order, no command and no setter. The reflection test
   `ReadOnlyBoundary_TheTreesNamespacesNeverTouchASimCoreType_AndSourcesHaveNoWriters` pins it.
2. **Content feeds the graph model, and the graph model feeds rendering.** Names, types,
   relationships, state vocabularies, Ages, milestones, buildings, units, icons and animations are
   JSON under `Sim.Ui/UiContent/trees/`. No technology, Age or milestone is named in C#.
3. **Everything placeholder says so.** The content carries `placeholder: true` flags, and the
   screens draw `DEMO CONTENT`, `PLACEHOLDER AGE`, `PLACEHOLDER — NOT GAMEPLAY OBJECTS` and the
   state source's label.
4. **Decisions stop at an interface.** Where a rule belongs to the simulation, a slot exists and
   the screen prints "TBD", "not implemented" or "reported by …". Such rules include the unlock
   rule, research cost, Age completion, the Δt rule, catch-up, diffusion and veterancy effects.
   §16 lists those decisions.

---

## §1 The seven lenses

The Trees are **seven player-facing lenses onto one graph**. A lens is a view, never a separate
tree.

| lens | meaning (task §2) | emblem mark |
| --- | --- | --- |
| KNOWLEDGE | what the civilization knows | `Knowledge` (oil lamp) |
| TECHNIQUES | what it knows how to do reliably | `Techniques` (hammer) |
| INSTITUTIONS | organizations that preserve, teach and expand capability | `Institutions` (pediment) |
| INFRASTRUCTURE | physical systems and networks | `Infrastructure` (arch bridge) |
| INDUSTRY | production capabilities and capacity | `Industry` (sawtooth works) |
| MILITARY | capabilities, formations, doctrine, equipment, training, logistics, veterancy | `Military` (chevron) |
| APPLICATIONS | tangible things produced or used | `Applications` (crate) |

The list is content: `lenses[]` in `the-trees.json`. Adding an eighth lens is a JSON edit. The
layout gains a band and the navigation gains a row.

## §2 The one graph

- **One node list and one typed edge list** (`TreeGraph`). A node has a primary lens (`domain`)
  and may appear in others (`alsoIn`). The demo's *Engineering personnel* lives in INSTITUTIONS
  and also appears in INDUSTRY.
- **Lenses are bands of one drawing** (`TreeLayout`). Columns come from the layering
  relationships, so what leads to what reads left to right. Each lens is a horizontal band.
  Cross-lens edges visibly cross bands.
- **Focusing a lens** keeps the others: its members stay normal, their direct neighbours in other
  lenses show as context, and the rest are dimmed (or hidden, by choice).
- **Feedback is first-class.** A non-layering kind (`improves`, `supports`, `diffusesTo`,
  `relatedTo`) never moves a node, so it may point backward. The demo's *Engineering knowledge
  improves Engineering University* closes a loop. The loader rejects a cycle among layering
  kinds and names it.

The demo chain from task §22 is the graph's own shortest directed path. It crosses four lenses
and is pinned by `TheDemoChain_IsOneDirectedPath_ThatCrossesTheLenses`:

Mathematics → Engineering → Engineering University → Engineering personnel → Engineering knowledge
→ Machine Tools → Factory → Industrial production → Automobile.

## §3 Node taxonomy

Twelve node types, all content (`nodeTypes[]`). Each binds a glyph base, a default mark and a
**state set**, the states a node of that type may occupy. One generic, data-driven node renders
every type, with no per-type component.

| type | base | default mark | state set |
| --- | --- | --- | --- |
| knowledge | Node (circle) | Knowledge | research |
| technique | Hexagon | Techniques | realization |
| institution | Portico | Institutions | realization |
| infrastructure | Link | Infrastructure | realization |
| industry | Tower | Industry | realization |
| military-capability | Formation | Military | realization |
| unit | Standard (banner) | Spear (per node: Bow, Horseshoe, Rifles…) | realization |
| doctrine | Shield | Military | realization |
| application | Heap | Applications | realization |
| milestone | Lozenge | Hourglass | milestone |
| policy | Scroll | Civic | realization |
| capability | Emblem | Links | realization |

A node may override its base and mark (`icon`) and its drawing register (`visualStyle.era`).

## §4 Relationship taxonomy

Thirteen kinds, all content (`relationKinds[]`). Each is a **visualization/data concept**; none
is simulation logic. `flow` maps the authored sentence onto the left-to-right direction:
"A requires B" flows from B to A. `layering` says whether the kind orders the columns.

| kind | verb | flow | layering | stroke / ink |
| --- | --- | --- | --- | --- |
| prerequisite | is a prerequisite of | forward | yes | solid / InkPrimary |
| enables | enables | forward | yes | solid / InkSoft |
| feeds | feeds into | forward | yes | dashed / InkSoft |
| requires | requires | reverse | yes | solid / InkPrimary |
| produces | produces | forward | yes | solid / Verdigris |
| institutionalizes | is institutionalized as | forward | yes | solid / GoldLeaf |
| diffusesTo | diffuses to | forward | no | dotted / River |
| derivedFrom | is derived from | reverse | yes | dashed / InkSoft |
| improves | improves | forward | no | dashed / Verdigris |
| supports | supports | forward | no | dotted / InkSoft |
| unlocks | unlocks | forward | yes | solid / InkPrimary |
| dependsOn | depends on | reverse | yes | dashed / InkPrimary |
| relatedTo | is related to | none | no | dotted / InkSoft, no arrow |

`relatedTo` was added so the node model's `related*` lists have an edge to become. A
relationship can be written two ways, and both resolve into the same edge list:

- in `edges[]`;
- inline on a node, using `prerequisites`, `enables`, `dependsOn`, `feedsInto` or the `related*`
  lists.

## §5 Visual states

**Research and realization are two axes, drawn in two places.** The ring is research: dashed for
locked, finely dashed for discovered, thin for available, an arc while researching, heavy once
researched. The fill (maturity wash) and the four stage pips are realization. *Researched* is a
heavy ring over an empty body, so it is visibly not *realized*.

| state | track | ring (`GlyphState`) | fill | pips |
| --- | --- | --- | --- | --- |
| locked | research | Locked | — | 0 |
| discovered | research | Discovered | — | 0 |
| available | research | Available | — | 0 |
| researching | research | InProgress (arc = progress) | research progress | 0 |
| stalled | research | Stalled | research progress | 0 |
| researched | research | Complete | — | 0 |
| developing | realization | Complete | realization progress | 1 |
| partially realized | realization | Complete | realization progress | 2 |
| operational | realization | Complete | realization progress | 3 |
| mature | realization | Complete | full | 4 |
| degraded | realization | Decayed | realization progress | 0 |
| in progress | milestone | InProgress | milestone progress | 0 |
| achieved | milestone | Complete | full | 0 |

- **Configurable per node type.** The state sets `research`, `realization` and `milestone`
  decide which states a type may occupy. Knowledge uses `research` only: it is never "realized"
  and has no degraded state, matching the concept that knowledge does not decay.
- **Degraded** shows a capability loss without touching research or the Age, per task §3.
- **Interaction overlays** are computed by the view, not stored in content:

  | overlay | drawn as |
  | --- | --- |
  | selected | gold border and glow |
  | hovered | ink border |
  | highlighted (search match) | gold accent bar |
  | prerequisite path | verdigris border and edges |
  | disabled (filtered out, or an invalid reported state) | dim, with an iron-red inset |
  | newly completed / newly discovered | `NEW` / `FOUND` tags from the animator |

- The legend draws every declared state with the glyph it produces. Clicking a legend state
  toggles the state filter.

## §6 Procedural asset architecture

Everything is composed by the existing glyph grammar (`Sim.Ui/Art/Glyphs`), which this work
**extended additively**:

- eight bases: Hexagon, Shield, Scroll, Standard, Field, Monument, Star, Emblem;
- 24 object marks;
- the `Discovered` state;
- maturity stage pips.

A 7,440-spec sweep of the v0 grammar hashes to the same SHA-256 before and after, so every
pre-existing bake is byte-identical. Contact sheet: `glyph-sheet-v1.png`.

| icon family | composition |
| --- | --- |
| node icons | node-type base + node mark + state ring + wash + stage pips |
| building icons | building base (Field, Hall, Tower, Portico, Dome, Ring, Monument) + mark + register + maturity stage + operational status |
| unit icons | Standard + class object mark + register + veterancy chevrons + deployment state |
| institution icons | Portico + specialization mark (Letters, Medicine, Dividers) |
| resource / system icons | Emblem + mark (Heap's centre slot swallows solid marks, see GlyphGrammarTests) |
| domain (lens) icons | Emblem + lens mark |
| Age icons | Star + Hourglass (content: `ages[].icon`) |
| state overlays | ring alphabet, hatch, arc |
| maturity indicators | wash + stage pips |
| connection / path indicators | relation-kind strokes, arrowheads, the verdigris path, diffusion dots |

**The anatomy fence holds.** No base or mark is a figure. A unit is a banner carrying an object:
Spear, Bow, Horseshoe, Rifles, Cannon, Tracks, Aircraft, Ship, Eye, StarOfCommand, Laurel, Brush,
Flask, Dividers. The mark enum is named for objects, and a test rejects person or role words. No
image file is authored: every icon is baked on demand, cached as a texture in game and embedded
as PNG in previews.

## §7 The state boundary

| interface | carries | today's implementation |
| --- | --- | --- |
| `ITreesStateSource` | node state id, research progress and points, realization progress; research header (points/turn, current target); civilization name | `DemoStateSource` (PLACEHOLDER) or `NoStateSource` |
| `IAgeStateSource` | current Age, reported progress, transition status, milestone completion / progress / evidence | same |
| `IGalleryStateSource` | building and unit display states, the diffusion sample stage | same |

- Snapshots are immutable and sorted, and carry a `Sequence` that changes when the state
  changes. The interfaces have one get-only member each.
- `DemoStateSource` reads `demo-state.json`, a four-step script. Each step lists only its
  changes. It enforces the invariants the real state will have to meet: a state is in the type's
  set, fractions lie in [0,1], and Ages go forward only. Its step buttons (◀ ▶) are a preview
  control that never reaches the simulation.
- The one live value is `SessionContext`: turn, world year and the simulation's own Δt, read
  from the session clock by the host. The Trees namespaces never see a `Sim.Core` type.
- `AgeForwardGuard` refuses to *display* an Age regression, keeps the later Age, and prints a
  warning.
- **When the simulation grows this state**, an adapter over the read-only world or observation
  records implements the same three interfaces. Nothing above them changes. What that state *is*
  is the knowledge packet's decision (DD-05) and the Age packet's (DD-04), not this UI's.

## §8 The screens

The overlay covers the whole window. It opens with **T** or the **The Trees [T]** button at the
right end of the command bar. That button sits outside `GameSections.Order`, so the seven
sections and their digit keys are untouched, and it is pinned by `ChromeGeometryTests`.

- **Escape** closes the overlay first.
- While the overlay is open, map pan, zoom and click, WASD, the digit keys, Tab and Space (End
  Turn) are suppressed. The overlay covers the world, and nothing under it acts.

**Header** (all tabs):

- the civilization name;
- the current Age with its reported progress and any pending transition;
- the research line, as reported;
- the session clock when hosted in the game;
- tabs THE TREES / AGES / GALLERY, and close.

**THE TREES tab:**

- **Lens navigation.** *Civilization* plus the seven lenses, each with researched and total
  counts.
- **Age filter** chips.
- **Age context.** The current Age, its progress, the count of mandatory milestones met, and the
  milestones that reference the selected node.
- **State-source panel.** A PLACEHOLDER tag, the step controls and Reload.
- **Toolbar.** Search, focus (Prerequisites / Downstream / Lineage), Dim / Hide, a type filter,
  Clear, and zoom − + fit.
- **Canvas.** Pannable and zoomable. Band strips with sticky lens labels, cards with glyph, name,
  state and a progress bar, edges in their kind's stroke, and overlays and animations. The
  opening view fits every band at a readable zoom (≥ 55 %). Labels keep a legibility floor and
  hide only when zoomed far out.
- **Details panel.** Identity, the DEMO tag, and state on both axes: research as a % or
  "complete", RP and "cost TBD"; realization as stage x/4 and a %. Then:
  - **Why — what precedes it**: each direct prerequisite with its state, clickable, plus
    "Availability rule: TBD (not implemented). The state shown is what \<source\> reports."
  - Leads to, other relationships, Ages, linked milestones, description, historical references,
    director notes and the id.
- **Legend.** Every state with its glyph (click to filter), the overlays and every relation kind
  in its stroke.

**AGES tab:** see [`ages-ui.md`](ages-ui.md).

**GALLERY tab:**

- the 15 building and institution placeholders, with maturity stage NEW / DEVELOPING /
  ESTABLISHED / MATURE, construction progress, personnel, specialization, Age built and
  operational status;
- the 14 unit placeholders, with deployment state, veterancy chevrons, strength, experience,
  cohesion, training, recovery and doctrine;
- the animation samples, including the diffusion pathway.

**Interaction model.** A frame is a draw list plus hit regions. A click resolves to the topmost
region's `TreesAction`. UI actions change `TreesUiState` only. Host actions (close, reload,
preview step, search focus) go to `TreesHost`. The canvas background deselects, and card hit
regions are clipped to the canvas so no panel can be clicked through.

## §9 Animation architecture

`StateAnimator` **observes** snapshots with the time the UI first saw them. Every sample is a pure
function of (observations, query time): no random number generator, no wall clock and no frame
count. The first observation is the baseline and animates nothing, so opening the screen never
replays history.

**Never authoritative.** The animator returns plain motion values: halo alpha and scale, a
one-shot kind and t, and flow-dot positions. A node's glyph comes from its reported state alone.
`Animations_AreNotAuthoritative_TimeChangesOnlyTheOverlays_NeverAStateOrAGlyph` paints frames at
six times and proves identical glyph specs and states. The snapshot object is untouched.

Animations are content (`animations.json`). A *steady* animation loops while a reported process
state holds (`appliesTo`). A *one-shot* plays on a transition (`trigger`).

| task §13 sequence | how it is shown |
| --- | --- |
| RESEARCH: idle → researching → progress pulse → completion → settled | `research-pulse` (halo while researching), `research-complete` (flash ring on entering researched), then still; `NEW` tag for 12 s |
| BUILDING: planned → construction → partial → completed → maturing → mature | operational-status glyph; `construction` sweep on the bar while under construction; `building-matured` pip flash on a stage rise |
| UNIT: recruiting → training → ready → deployed → experienced | `unit-recruiting` / `unit-training` pulses; `unit-veterancy` flash when chevrons rise |
| KNOWLEDGE DIFFUSION: source → contact channel → exposure → absorption → local development | `diffusion-flow` dots along declared `diffusesTo` pathways, and the five-station sample in the gallery driven by the reported stage. Diffusion itself is NOT implemented |
| AGE TRANSITION: current → transition indication → new Age → settled | `age-transition` sweep on the banner and the next Age while pending; `age-entered` flash on the new Age, then settled |

The earlier convention "nothing moves while the state it shows is unchanged"
(`pre-m5-visual-system.md` §5) is amended append-only in that document's §11. Steady loops are
allowed **only** while a reported *process* state holds. §15 records this conflict.

## §10 Data and content separation

`ui-content/trees/` is copied beside the executable from `Sim.Ui/UiContent/trees/`. It contains:

- `the-trees.json`: lenses, states, state sets, node types, relation kinds, nodes and edges;
- `ages.json`: milestone categories, Ages and milestones;
- `gallery.json`: maturity stages, statuses, veterancy levels, buildings and units;
- `animations.json`: presentation animations;
- `demo-state.json`: the PLACEHOLDER state script.

`TreesContentLoader` parses strictly, so an unknown field is an error and a typo cannot silently
drop an edit. It validates every reference across the four content files, with a JSON path per
diagnostic. A broken file shows its diagnostics on screen with a Reload button, and never
crashes. **Reload** re-reads the directory in-game, so the Director edits the JSON and presses
Reload. The field reference is [`the-trees-content-schema.md`](the-trees-content-schema.md).

The Director can replace these without touching a component:

- names and descriptions;
- milestone definitions;
- prerequisites and relationships;
- research costs (`researchCost.points`);
- Age associations;
- icons (`icon.base` / `icon.mark`);
- animations;
- the lens list;
- the node-type list;
- the state vocabulary.

## §11 Preview and screenshots

`sim-ui --trees-preview [dir]` paints ten scenarios to SVG through the same `TreesHost` and
painters the game uses. It opens no window and founds no world.

`scripts/trees-preview.sh [dir]` then screenshots each to a 1280×800 PNG with a headless
Chromium. Prefer the headless shell: the full browser's new headless mode clips the bottom 88 px.
MonoGame cannot open a GL context in the build container, so the in-engine path cannot be captured
there.

Committed screenshots are in `docs/architecture/trees-preview/`:

| file | shows |
| --- | --- |
| 01-trees-overview | the whole graph, seven bands, legend |
| 02-trees-selected-prerequisites | selection, prerequisite focus, the why-view |
| 03-trees-lineage-zoomed | the demo chain as a lineage, zoomed |
| 04-trees-lens-industry-search | a lens with cross-lens context, and a search |
| 05-trees-filters | state + Age filters in hide mode |
| 06-trees-state-change | a step change: flash, `NEW` / `FOUND` tags |
| 07-ages-current | Age 03 at 72 %, the checklist |
| 08-ages-transition-pending | transition pending, a milestone selected |
| 09-ages-new-age | the new Age, entry flash |
| 10-gallery | buildings, units, animation samples |

## §12 Tests

The task §27 list, mapped to tests in `Sim.Ui.Tests`:

| §27 item | test(s) |
| --- | --- |
| all seven domains render | `AllSevenLenses_RenderInTheNavigationAndAsBandsOfOneCanvas` |
| nodes can be loaded from data | `ShippedContent_Loads_…`, `Nodes_AreData_AddingAndRenamingANodeInJson_ChangesTheGraph`, `EveryNode_IsGeneratedFromData_…` |
| nodes can have typed relationships | `TypedRelationships_InlineFieldsAndTheEdgeList_ResolveIntoOneTypedEdgeList`, `Queries_UpstreamDownstreamAndFeedback_…` |
| graph renders cross-domain relationships | `EveryRelationship_IsDrawn_AndCrossLensEdgesConnectDifferentBands`, `TheDemoChain_IsOneDirectedPath_ThatCrossesTheLenses` |
| node state changes render correctly | `NodeStateChanges_ChangeTheGlyph_RingForResearch_FillAndPipsForRealization`, `DemoState_StepsMergeOverThePreviousStep_…` |
| placeholder Ages render | `PlaceholderAges_Render_WithoutHistoricalNames`, `Ages_MoveForwardOnly_…` |
| milestone progress renders | `MilestoneProgress_Renders_AsReported_AndFollowsTheSource` |
| filtering works | `Filtering_LensStateAgeTypeSearchAndFocus_…`, `Filtering_ThroughTheScreen_LegendTogglesAStateFilter_…` |
| selection / detail panel works | `Selection_ClickingACard_OpensItsDetails_AndThePrerequisiteViewIsNavigable`, `Hits_PanelsSitAboveTheCanvas_…`, `EveryPaintedString_IsLatin1_…` |
| procedural glyph generation remains deterministic | `GlyphGrammarTests` (14), `Frames_AreDeterministic_TheSameScenarioWritesByteIdenticalWellFormedSvg` |
| animations are state-driven rather than authoritative | `Animations_AreDrivenByReportedStateChanges_AndEnd`, `Animations_AreNotAuthoritative_…` |
| existing tests remain green | the full `Sim.Ui.Tests` and `Sim.Tests` runs; no existing test was weakened |

Also:

- validation: `Validation_UnknownReferencesTyposAndLayeringCycles_…`,
  `DemoState_StatesOutsideTheTypesSet_AndAgeRegression_AreRejected`,
  `BrokenContent_ShowsTheDiagnostics_…`, `Reload_KeepsTheTabLensAndSelection_ByNodeId`;
- layout: `Layout_IsDeterministic_…`, `Layout_TieDense_EqualBarycentresBreakOnTheNodeIndex`;
- boundary: `ReadOnlyBoundary_…`;
- camera and chrome: `Camera_ZoomKeepsThePointUnderTheCursor_…`, `TreesButton_SitsFlushRight_…`.

## §13 Placeholder status

- **Everything in the shipped content is demonstration content.** That covers the 31 nodes, the
  relationships, the Age assignments, the six Ages `AGE_00`..`AGE_05` and their 23 milestones,
  the 15 buildings, the 14 units and the whole state script. None of it is historical, final or
  balanced.
- Node names follow task §21. The Age and milestone names are neutral ("Age 03",
  "Milestone 03.03"). Every placeholder Age uses the neutral register: the Age-to-style binding is
  a Director decision.
- The demo civilization's numbers are arbitrary display values: research points, personnel,
  experience and cohesion. **Personnel is not the simulation's conserved population.**

## §14 Intentionally NOT implemented

- research formulas, costs and the unlock / availability rule;
- a generic "science = capability" shortcut;
- knowledge state in the simulation, diffusion, exposure and absorptive capacity;
- Age advancement, the milestone completion rule, Age progress computation, variable Δt, the
  global Δt boundary rule and catch-up effects;
- institutional emergence, construction mechanics, building effects and maturity rules;
- combat statistics, military balance, veterancy effects or multipliers, and Action Capacity;
- new authoritative or serialized state, a universal capability system, and any UI to simulation
  write.

The UI prints "TBD", "not implemented" or "reported by …" at each of these boundaries.

## §15 Architecture conflicts discovered

1. **Animation convention vs task §13.** `pre-m5-visual-system.md` §5 banned idle loops and
   "glow" and said nothing moves while state is unchanged. The task asks for a research progress
   pulse, construction and training motion, and diffusion dots. **Resolution taken:** loops only
   while a reported *process* state holds, and one-shots only on observed transitions, recorded
   append-only in that document's §11. Needs ratification: **DD-T1**.
2. **Era registers read as Age names.** The grammar's `EraRegister` members (Classical, Medieval,
   Industrial, Modern) are v0 names. Binding them to placeholder Ages would label unnamed Ages
   historically, against task §3. **Resolution taken:** placeholder Ages use the neutral register.
   Object registers such as the Factory's Industrial register remain drawing styles. Renaming the
   enum is out of scope. Relates to **DD-04**.
3. **Heap as a resource base.** The v0 Heap's mark slot is the crossing of its disc outlines, so
   solid marks vanish there (Anvil: 0.26 % of the box). v0 is kept byte-stable. Resource icons
   use the Emblem base. Documented in `GlyphGrammarTests`.
4. **The game's font atlas is Latin-1 only.** `UiTheme` loads EB Garamond and IBM Plex Serif
   with ImGui's default range, so arrows, check marks, "…", "—" and "Δ" would draw as "?" in the
   game while looking right in an SVG. This follows the existing convention (`ChromeGeometry`'s
   ASCII close glyph). **Resolution taken:**
   - every draw-list text is normalised to Latin-1 (`DrawList.Latin1`), identically for both
     backends;
   - status marks are drawn shapes;
   - `EveryPaintedString_IsLatin1_SoTheGameAtlasCanDrawIt` pins it.

   Extending the atlas ranges would touch the whole UI's fonts and is left to the Director.
5. **No simulation producer exists** for any Trees or Age state. This is known from the audit
   (§1, C07–C11). All state is behind the §7 interfaces, with a placeholder source.

## §16 Decisions that require Director approval

| id | decision | where the slot is |
| --- | --- | --- |
| DD-T1 | ratify the process-state animation rule (§15.1), or return to transitions-only | `animations.json`; `pre-m5-visual-system.md` §11 |
| DD-T2 | the real lens list, node-type list and state vocabulary, per type | `the-trees.json` `lenses` / `nodeTypes` / `stateSets` |
| DD-T3 | whether each relation kind is also a simulation relationship, and which one gates availability | `relationKinds[].layering`; the "Availability rule: TBD" line |
| DD-T4 | where research points and costs come from (no formula exists; DD-05) | `ResearchHeader`, `researchCost` |
| DD-T5 | what "realized / operational / mature" measure in the simulation, and who reports stage and progress | `NodeStatus.RealizationProgress`, state `stage` |
| DD-T6 | the Age list, milestones, completion rule and progress definition (DD-04) | `ages.json`; `IAgeStateSource.CurrentProgress` |
| DD-T7 | the Δt-per-Age and catch-up semantics (DD-02, DD-04) | `ages[].deltaT`, `ages[].catchUp` |
| DD-T8 | the Age-to-visual-register binding, and whether `EraRegister` names should change | `ages[].visualTheme.register` |
| DD-T9 | the carriers of building maturity, personnel and veterancy (DD-08, DD-09) | `IGalleryStateSource` |

## §17 Files and removal

| area | path |
| --- | --- |
| content | `Sim.Ui/UiContent/trees/*.json` (copied to `ui-content/trees/`) |
| model, loader, graph, layout, state boundary | `Sim.Ui/Trees/*.cs` |
| screens, animator, host, preview | `Sim.Ui/Trees/View/*.cs` |
| draw list and SVG backend | `Sim.Ui/Render/*.cs` |
| ImGui backend | `Sim.Ui/ImGuiIntegration/DrawListImGuiBackend.cs` |
| game wiring | `SimUiGame` (the T key, the overlay, the command-bar button); `ChromeGeometry.TreesButton` |
| glyph grammar extension | `Sim.Ui/Art/Glyphs/*` |
| headless entry | `Sim.Ui/Program.cs` (`--trees-preview`); `scripts/trees-preview.sh`; `PngCodec.Encode` |
| tests | `Sim.Ui.Tests/TreesContentTests.cs`, `TreesScreenTests.cs`, and additions to `GlyphGrammarTests.cs` and `ChromeGeometryTests.cs` |

To remove all of it:

- delete the paths above that are new: the content, `Sim.Ui/Trees/`, `Sim.Ui/Render/`, the ImGui
  backend, the preview script and the two new test files;
- revert the touched files: `SimUiGame.cs`, `Program.cs`, `ChromeGeometry.cs`, `PngCodec.cs`, the
  glyph grammar extension and its test additions, and the two `.csproj` item groups.

The simulation projects are unaffected either way.
