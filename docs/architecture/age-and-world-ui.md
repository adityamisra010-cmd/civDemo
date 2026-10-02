# Age advancement and world visualization UI

Branch `ui-research-trees`, rebased onto `research-progression-foundation` @ e90aa9a (ADR-031: Ages, the AdvanceAge order, surge emphases, unit families, `MilitaryUnits`, schema v28). This work is UI only. It touches `Sim.Ui/**`, `Sim.Ui.Tests/**` and `docs/architecture/`. It implements D-047 Part 4 D–G and Part 5 (director ruling 2026-10-02) over the ADR-031 read seams `AgeQuery`, `MilitaryQuery` and `AgeAdvancePolicy`. It changes no simulation code, content or golden.

## 1. Wiring (what changed in the session)

- **One config recipe.** All four UI load sites (`UiSession.Start`, `UiSession.ProductionPipeline`, `UiFounding.Found` and `SimUiGame`'s display config) now read `UiFounding.ProductionConfig()`. That is the six-stream `SimConfigLoader.Load(sim, needs, goods, research, ages, unitFamilies)`, the same recipe as `Sim.Cli` (`CliRecipes`, `HeadlessFounding`). Before this change the UI loaded four streams, so:
  - the Age systems were inert;
  - founding laid down no warband;
  - the UI's founded world was not hash-equal to the CLI's canonical founding.

  The two founding-equivalence tests now compare against the six-stream recipe and pass.
- **AI Age orders.** `UiSession.EndTurn()` first appends `AgeAdvancePolicy.OrdersForAi(world, cfg.Ages)` to the order log, then steps. The orders go through the same log and stamping as every player directive, so an AI advance replays from the log. The policy never touches player-commanded polities, and nothing else advances the player.
- **The player's order.** `UiSession.EmitAdvanceAge(toAge, surgeKey)` appends exactly `AgeQuery.AdvanceOrder(world, player, toAge, surgeKey)`, stamped with the current turn. It refuses, appending nothing, when:
  - `AgeQuery.CheckAdvance` rejects the order;
  - an advance is already pending this turn;
  - the session has no Age content.

  `UiSession.QueuedOrders()` returns the not-yet-stepped batch, and `PendingPlayerAdvance` reads `AgeQuery.PendingAdvance` over it.
- **`UiSession.StartFrom(world, …)`** builds a session over an already-founded world. Tests use it for constructed situations, such as an AI polity that is eligible for Age II. It uses the same executor, config and End Turn as `Start`.

## 2. The capital's Age panel (Part 4 D)

**Selecting the player's capital** opens a docked panel (`Sim.Ui/Ages/AgeScreen.PaintPanel`) in the left column under the selection card. The model is `AgePanelModel.Build(world, ages, queuedOrders, player)`, a pure read through `AgeQuery.Status` and `AgeQuery.PendingAdvance`. These are the statics the Age systems compute with, so the panel's "eligible" is the simulation's "eligible".

The panel shows:

- **The Ages.** A banner with the current Age (numeral and full name) and when it was entered, and the next Age by full name.
- **A state band**, in one of three states:
  - *Not yet eligible* — progress only, and **no Advance button exists** (no hit region and no button text, both pinned).
  - *Advancement available — optional* — an active **ADVANCE AGE** button. Nothing happens until the player chooses.
  - *Advancing to {Age} next turn* — the chosen surge emphasis and the End Turn it takes effect at. No button.
- **Requirement chips:** Core *m/n*, Supporting *m/required*, Categories *covered/required*.
- **The milestones:**
  - core milestones, done or not;
  - supporting milestones **grouped by the five categories** (Technological, Material-Economic, Institutional-Social, Systemic, Military Realization), each group marked covered or not covered;
  - each milestone's observed quantity against its threshold (real state, e.g. "Grain surplus 12,647 / 150,000").
- **Still required:** `AgeEligibilityReport.Remaining`, verbatim.
- **When eligible or pending:** a short "Your military will modernize" summary and the selected surge.
- **A link to KNOWLEDGE & TECHNOLOGY.**

## 3. The advance flow (Part 4 E, F)

The flow is a large modal (`AgeScreen.PaintFlow`) built from `AdvanceFlowModel.Build(world, ages, families, player)`.

- **Surge emphasis cards.** There is one card per `AgeQuery.SurgeOptions` entry, in key order. Each card shows:
  - the name;
  - the description;
  - the affected domain;
  - the expected *type* of effect;
  - pills naming the downstream systems;
  - the content's stated shape (`surgeShape`).

  **No numeric value is displayed.** The surge has no implemented formula (ADR-031 §6), and a test asserts that no digit appears in any card text. Decision references such as "D-011" are excluded from that check. Confirm stays disabled until a card is chosen.
- **"Your military will modernize."** This section has two columns, and neither is a UI list:
  - *Your formations:* `MilitaryQuery.Summarize(families, MilitaryQuery.ModernizationPreview(...))`. This is the same `PlanFor` the transition applies, so the preview is exactly what will happen. In the seed-42 world it reads "1 × Warband — kept (no successor in this Age yet)", which is ruling 18's honest outcome for the founding warband entering Age II.
  - *Across the unit-family graph:* `MilitaryQuery.FamilyLineChanges(families, current, next)`, which shows what each family becomes. Examples: "Spear / Anti-Cavalry: first appears — Spearmen", "Ranged Infantry: Slingers → Archers". Families with no realization yet read "not yet realized".
- **Confirm** returns `AgeCommand.Order == AgeQuery.AdvanceOrder(world, player, next, surge)`. The host passes it to `UiSession.EmitAdvanceAge`. The screen writes nothing, and the world hash is pinned unchanged. **Not now** closes the flow, and the player may advance later.

## 4. Age in KNOWLEDGE & TECHNOLOGY and INSTITUTIONS (Part 4, deep UI)

- The tab bar of the full-screen progression view carries an **Age chip** (`ProgressionScreen.Age`, `HitKind.AgeOpen`). The chip shows:
  - the current Age and its full name;
  - one of: core and supporting progress, "ADVANCE AGE available", or "advancing next turn".

  Clicking it returns `ProgressionCommand { OpenAge = true }`. The game then closes the tree, selects the capital, and opens the advance flow when the player is eligible (otherwise the panel).
- The INSTITUTIONS lens already lists the **adopted civics** (completed Civics nodes, ruling 11), as research-tree-ui.md describes. It is unchanged.

## 5. World visualization by zoom (Part 4 G, Part 5)

`Sim.Ui/World/WorldLens.cs` has two parts:

- `WorldProjection.Build(world, cfg, names, player)` is a read-only, aggregated projection.
- `WorldLens.Paint` draws it into a `DrawList`. In the game it goes to the ImGui background list over the map. In previews it goes to SVG over the game's own parchment terrain bake.

This is a slim re-implementation in the spirit of the unmerged `claude/civdemo-work-b1z2y4` world-visualization layer: *the renderer never decides that an entity exists*, and morphology is aggregated. Its demo sources, JSON morphology content and placeholder world were **not** ported, because the director requires real state only.

**Zoom level** is `WorldLens.LevelFor(zoom, viewport, worldSize)`, a pure function of the share of the world the view spans:

| Visible span | Level |
|---|---|
| more than 55% | World |
| more than 16% | Regional |
| otherwise | Settlement |

The level selects the layers (`WorldLens.LayersFor`, a fixed array per level):

| Zoom | Layers (paint order) |
|---|---|
| World | territories (catchment claims tinted by controlling polity), major infrastructure (the built network, dashed), major settlements (sized by population, capital star), military formations, Age banners |
| Regional | territories (lighter), roads (the network with casing), settlement morphology, institutional presence, production signals, military formations, Age banners |
| Settlement | roads, morphology, infrastructure density, institution types (glyph + name + count), population scale, production signals, formations, Age banners |

What each element draws from:

- **Morphology** is aggregated, never one shape per building:
  - The footprint radius comes from population.
  - The ring holds one dwelling block per 25 dwellings, capped.
  - Structures appear as one glyph per kind (granary = domed store, workshop = smoking block), with "× n" at settlement zoom.
  - **The style follows the controller's Age** (`AgeQuery.CurrentAge`). Age I is an open hut ring with round huts. Age II adds a palisade with stakes and square houses. Age III and later have an octagonal wall with towers.
  - Every settlement flies an **Age banner** with the controller's numeral. The Age transition therefore produces a visible civilization-state change (banner I → II, palisade, house blocks), plus the **transition toast** "A NEW AGE BEGINS — AGE II", with the surge and the count of formations modernized. The toast fires only when `EndTurn` observes the player's Age change in the simulation.
- **Formations:** one token per `MilitaryUnits` row, in table order (pinned). Each token is a framed plate in the owner's ink, with one of twelve emblems, one per unit family. At Regional and Settlement zoom it is labelled with the content identity name ("Warband"). A stationed formation sits on its settlement's rim.
- **Infrastructure density:** built network links with an endpoint in the settlement's catchment, and its size tier.
- **Production signals:** the `SectorAllocations` row as a ring of sector arcs, and the leading sector. A settlement with **no allocation row** says "labour split: default (no allocation ordered)" rather than showing a split it does not have.
- **Honest empty states** appear in the legend (`WorldProjection.Absent`):
  - army movement and supply are not simulated;
  - universities exist only as polity-level `ResearchCostModifiers` rows, never placed in a settlement — "none founded" when there are none;
  - "Structures: none built yet".

### 5.1 Map layer ownership (one owner per layer)

Before this pass the lens was drawn on top of the legacy GPU map layers, so territory, paths and settlement marks/names appeared twice. `Sim.Ui/World/MapLayers.cs` (`MapLayerOwnership`) is now the single authority. Each layer has exactly one owner, and the other renderer does not draw it:

| Map layer | Owner | Drawn by | Notes |
|---|---|---|---|
| Terrain (parchment bake) | Map renderer | `SimUiGame.Draw` sprite pass | substrate under everything |
| Rivers (vector mesh) | Map renderer | `SimUiGame.Draw` `_riverVertices` | zoom-rebuilt width (D-A3) |
| Territory | **WorldLens** | `WorldLayer.Territories` | polity-tinted catchment blocks at World/Regional zoom; the in-game `territory` checkbox now gates this layer (`Paint(showTerritory:)`). Legacy per-settlement fills **removed**. |
| Paths / roads | **WorldLens** | `MajorInfrastructure` / `Roads` | dashed network at World zoom, cased roads below. Legacy path mesh **removed**. |
| Settlement markers and names | **WorldLens** | `MajorSettlements`, `SettlementMorphology`, density, population, production | sized mark + capital star at World zoom, aggregated morphology below; the name is drawn here. Legacy marker sprite and name text **removed**; the legacy label *click rect* is still computed for selection (no drawing). |
| Unit tokens | **WorldLens** | `MilitaryFormations` | one per `MilitaryUnits` row |
| Institution markers | **WorldLens** | `InstitutionalPresence` / `InstitutionTypes` | structures and universities, see below |
| Age banners | **WorldLens** | `AgeBanners` | controller's numeral |

`WorldLens.Paint` charges every draw command it emits to exactly one map layer (`LensFrame.LayerDraws`), so ownership is tested by draw-call accounting on a real world, not by inspection.

**Institution markers.** Aggregated and embedded inside the footprint: one glyph per structure kind (`Structures` rows of the settlement, count > 0) and one per specialized-university type the controlling polity holds. Universities are polity-level `ResearchCostModifiers` rows (ADR-029 §9; no system writes them yet), so they are shown in the polity's **capital** — its seat — and nowhere else; no per-settlement placement is invented. Regional zoom: glyphs only. Settlement zoom: a key beside the footprint names each glyph with its aggregated count ("granary x2", "Engineering University x2", count = rows of that type). The five university types (1 Military, 2 Medical, 3 Engineering, 4 Natural Science, 5 Agricultural) each have a distinct ink and emblem on a pedimented hall (crossed blades, cross, gear, orbit, sheaf). `LensFrame.Institutions` lists every marker drawn.

## 6. Tests (`Sim.Ui.Tests/AgeAndWorldUiTests.cs`, 19 cases; `WorldLayerOwnershipTests.cs`, 10 cases)

The fixture is a real seed-42 session (256 px, 4 settlements) played through the real order pathway. It saves its turn-12 world (not eligible), then plays on until `AgeQuery.IsEligible`, which happens at turn 292.

- **Wiring.** Ages and families are loaded, the warband is founded, and `AgeEligibility` rows are written.
- **Panel states:**
  - Not eligible: model equals `AgeQuery.Evaluate` (core, category coverage, remaining); no OpenAdvance hit and no "ADVANCE AGE" text; the flow refuses to open.
  - Eligible: the button and "Advancement available - optional" are shown, and clicking the button opens the flow.
  - Pending: "Advancing to Neolithic / Agricultural next turn" is shown with no button.
- **Confirm.** With no surge chosen, Confirm returns nothing. Clicking a card and then Confirm returns exactly `AgeQuery.AdvanceOrder` with that surge, and the world hash is unchanged. `EmitAdvanceAge` appends exactly that order once, and a second call appends nothing. After End Turn the player is in Age II with `EnteredTurn = decision + 1` and the chosen surge.
- **Never auto-advanced.** The player stays in Age I through three eligible End Turns, and the log has no AdvanceAge order.
- **Preview.** The preview equals `MilitaryQuery.Summarize(ModernizationPreview)` line for line, the family lines equal `FamilyLineChanges(1, 2)`, and the surge cards equal the content.
- **No numeric surge values** in any text drawn inside a surge card.
- **AI orders.** A world is founded with one AI Empire, and its eligibility is constructed (the Sim.Tests `EligibleForA2` precedent). On `UiSession.StartFrom`, End Turn appends exactly `OrdersForAi`, the AI enters Age II and the player stays in Age I.
- **Read-only.** Panel, flow, confirm, projection, lens paint at all zooms and the progression screen leave the world hash unchanged.
- **Knowledge-screen Age chip.** It shows "ADVANCE AGE available" and returns `OpenAge` with no order.
- **Zoom.** `LevelFor` boundaries are checked at 7.0/7.2 and 24/25 on 1000 px / 256 px. Per-zoom layers are the ruled sets and are stable.
- **Formations.** The lens draws one token per `MilitaryUnits` row, in table order, at every zoom, with family keys and identity names from content.
- **Projection.**
  - It equals the tables: settlements, network edges, catchment nodes, and per-settlement population equal to the Σ Buckets.
  - Every Age equals `AgeQuery.CurrentAge`.
  - Painting it twice gives identical SVG.
- **Transition.** The real advance through the session changes the projection's Age 1 → 2, the regional banners from all "I" to "II", and the toast text.

**Layer ownership and institutions** (`WorldLayerOwnershipTests`) run on the **institution fixture** (`InstitutionWorldFixture`, test-only): a seed-42 world founded through `UiSession` and played 6 End Turns, cloned, and given rows of the simulation's own types — `StructureRow` granary ×2 and workshop ×1 in the player's capital, granary ×1 in a second settlement; `ResearchCostModifierRow` for all five university types for the player (Engineering twice) plus one row for a polity that controls nothing — then wrapped by `UiSession.StartFrom`. Nothing of it is reachable from production code.

- Every map layer has exactly one owner; the legacy pass keeps only Terrain and Rivers.
- At every zoom the lens charges zero commands to Terrain/Rivers, every command to exactly one layer, and draws every lens-owned layer at some zoom.
- Territory draws equal `CatchmentNodes.Count` (World/Regional) and path draws equal `NetworkEdges.Count` × strokes (1 dashed, or 2 casing + road) — not doubled; the territory toggle turns the layer off.
- A source guard: `SimUiGame.cs` no longer builds or draws the territory fills, path mesh, marker sprite or name text.
- Institution markers drawn equal exactly the markers implied by `Structures` and `ResearchCostModifiers` (sorted set equality, counts included) at Regional and Settlement zoom; none at World zoom; the "none built / none founded" legend lines disappear.
- Settlement zoom names and counts ("granary x2", "Engineering University x2", the four others); Regional draws glyphs without names.
- The five university glyphs and inks are pairwise distinct; glyphs lie inside the capital's footprint; projection and paint are read-only.

Existing tests: two founding-equivalence tests were updated from the four-stream to the six-stream recipe. This is the canonical CLI recipe, which they were meant to pin.

## 7. Previews (`age-and-world-ui/`)

Run `docs/architecture/age-and-world-ui/render-previews.sh`, which runs `sim-ui --age-preview` and then headless Chromium. It founds seed 42 (256 px, 4 settlements) through `UiSession`. Each turn the player has no target, it logs the screen-equivalent research order toward the next Age's unmet research milestones: the cheapest available node among them and their prerequisites, or else the cheapest Technology node. Then it presses End Turn. **Eligibility for Age II was reached through real orders at turn 292**: core *Cultivated cereals*, plus Fired pottery, Managed flocks, Settled villages, Growing population and Standing warband (4 categories). The surge chosen in the flow is *Age of Works* (key 2). Confirming logs the AdvanceAge order at turn 292, and turn 293 is the first in Age II. `preview-log.txt` records these turns.

| Preview | Shows |
|---|---|
| `01-capital-age-panel-not-eligible.png` | Turn 12: Age panel at regional zoom, *Not yet eligible*, milestones by category, still required |
| `02-capital-age-panel-eligible.png` | Turn 292: *Advancement available — optional* with the ADVANCE AGE button |
| `03-advance-age-surge-and-modernization.png` | The advance flow: six qualitative surge cards (Age of Works chosen), "Your military will modernize" from `MilitaryQuery`, the family graph's changes, confirm |
| `04-capital-age-panel-pending.png` | After confirm: *Advancing to Neolithic / Agricultural next turn*, surge shown |
| `05-world-zoom-world.png` | World zoom (turn 12): territories, network, settlements by size, capital star, warband token, Age I banners |
| `06-world-zoom-regional.png` | Regional zoom: roads, hut-ring morphology, formation label, legend with honest empty states |
| `07-world-zoom-settlement.png` | Settlement zoom on the capital: population and dwellings, infrastructure density, labour split (honestly "default"), warband on the rim |
| `08-after-transition-world.png` | Turn 293: the transition toast and Age II settlements (banner II, palisade, house blocks) |
| `09-capital-age-panel-after-transition.png` | The capital panel in Age II, now tracking Age III requirements |
| `10-institutions-settlement-zoom.png` | **Institution fixture** (test-only), settlement zoom on the capital: granary/workshop and the five university glyphs embedded in the footprint, key with counts |
| `11-institutions-regional-zoom.png` | Institution fixture, regional zoom: the same institutions as glyphs only |

`10`/`11` are rendered by `render-institution-preview.sh`, which runs the test `Preview_InstitutionFixture_Svg` with `CIV_INSTITUTION_PREVIEW_OUT` set (the fixture lives in the test project only) and screenshots the SVGs.

## 8. Gaps (stated, not hidden)

- **The canonical world has no AI Empires** (`worldgen.json` `aiEmpires` = 0). The AI order path is therefore exercised by a constructed world in tests, not by the default game.
- **No structures are built and no universities exist in the real-play previews** (01–09). The preview run issues no construction orders, and no system writes `ResearchCostModifiers`, so the legend there says "Structures: none built yet" / "Universities: none founded". The institution markers are exercised and previewed on the test-only institution fixture (`10`, `11`).
- **Universities are polity-level** in the simulation; the lens shows them in the polity's capital and does not invent a settlement-level placement.
- **The founding warband stays a Warband entering Age II** (ruling 18: no later realization of its family yet). The modernization preview therefore shows a "kept" line. The family-graph column shows the conversions that exist (Slingers → Archers, Spearmen first appearing).
- **Layer duplication resolved** (§5.1): the in-game legacy territory fills, path mesh, marker sprites and name text are no longer drawn; the lens is the only owner of those layers.
- **The preview PNGs are headless-SVG renders of the same `DrawList`** the game replays through ImGui. Fonts are approximated by `ApproxTextMeasure`, so in-game glyph metrics can differ slightly.
