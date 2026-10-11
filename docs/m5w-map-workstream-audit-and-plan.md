# M5 Map, Exploration, Settlement and UI — Architecture Audit and Plan

_2026-10-10. Read-only audit by ten parallel auditors + synthesis + completeness critic, on `m5p-d051-fixes` @ `82bde01`. Workstream base: `m5w-integration` (local, cut from `82bde01`). Nothing pushed._

Corrections from the completeness critic are applied below: the candidates differ in **three** UI-side files (ActionSurfaceModel.cs plus two test files), not one; the DD-13 hold is an **unruled deferral**, not a prohibition; sight must not be modelled as a movement-cost isochrone (D-039 B4 vantage); Trends/HistoryBuffer world scope is a fog-leak channel.

## Architecture audit: map, exploration, settlement and UI workstream

**Scope and method.** Tree: `/home/user/m5p-fixes` @ 82bde01. Everything below comes from reading the code; dotnet was not run.

### Repository state (verified with git)
- **82bde01 (`m5p-d051-fixes`) exists only locally.** `git branch -r --contains` returns nothing.
- **2dc6af2 is already on the remote** as `origin/claude/new-session-yavghm`, with CI and ui-artifact green. `current-state.md:11` says "NOT pushed", which is stale.
- **`origin/main` (93270cd) cannot serve as a base.** It is at schema v25 with order kinds 1–4 and has none of the M5 systems listed below.
- **82bde01 carries two RED tests:** `Dev_MalthusCorridors_AllInBand` for seeds 42 and 7. CR-020, which covers them, is OPEN.
- **The two candidates differ in Sim.Ui by one file only:** `Sim.Ui/Actions/ActionSurfaceModel.cs`.

### Systems to reuse (extend these; do not duplicate them)
- **Part 1, map:**
  - `Camera` (`Sim.Ui/ViewModel/Camera.cs:9-78`) is pure double-precision state and Sim.Core never reads it.
  - `ProgressionCamera.Advance` (`Progression/ProgressionCamera.cs:8-89`) is the existing eased-target pattern.
  - `WorldLens.LevelFor`/`LayersFor` (`World/WorldLens.cs:303-324`) select the zoom level and its layers.
  - `MapLayerOwnership` (`World/MapLayers.cs:31-64`) gives each map layer exactly one owner.
  - `DrawList` replays to both ImGui and SVG; `PanelLayout` (`ViewModel/PanelLayout.cs:42-157`) defines the chrome geometry.
- **Part 2, units:**
  - `MilitaryUnitRow.X/Y/Location` (`State/WorldState.cs:944-946`) is the only place a unit's position is stored.
  - `Pathfinder.FindPath` (`Pathing/Pathfinder.cs:56-96`), over `TraversalLattice` with the road overlay, is the only authority for movement cost.
  - `MilitaryQuery.PlanFor` is the one function both the preview and the applied result call, so the preview always equals what is applied.
- **Part 3, fog:**
  - `TraversalLattice` nodes (16 km) can serve as the extent grid.
  - `Pathfinder.Isochrone` (`:106`) can compute sight.
  - `CatchmentNodes` can seed a polity's starting extent; `KnowledgeTransfer` already has the right union semantics for inheritance.
  - `WorldProjection.Build` (`WorldLens.cs:100-244`) is the single choke point through which every map layer is built.
- **Part 4, founding:**
  - `ColonizationSystem.Found` (`Systems/Colonization/ColonizationSystem.cs:235-320`) already takes the site cell as a parameter.
  - `ProvisionsFor`/`TransferProvisions` (`:383-414`) compute and move the colonists' grain.
  - The siting checks are inline at `Worldgen/SettlementSiting.cs:361-387`.
  - ADR-034's order deferral is in `Kernel/OrderValidation.cs:16-201`.
  - `ConstructionQuery` is the precedent for one predicate shared by the UI and the system; `AiOrders.cs:44-64` is the AI's order seam.
- **Part 5, resources and suitability:**
  - `SettlementSiting.CandidateScores` (`:71-86`) is the only authoritative suitability score.
  - `WorldFounding.AddDepositsForSite` (`:477-499`), with the private `HinterlandMeans` (`:359-388`), is the only deposit function.
- **Part 6, discovery shortcut:** `ResearchQuery.CompletedBetween` (`:67-76`) plus the `UiSession.Step` observers, which Resume rebuilds by replay (`UiSession.cs:385-404, 698-746`).
- **Part 7, Age themes:** `UiEras.Of` (`Theme/UiEra.cs:44-48`), the `EraTheme` tokens, and `MapInk.For` (`World/MapInk.cs:39-51`), the map's only colour source.
- **Part 8, settlement growth:**
  - `InstitutionMarkerSource` already reads `InstitutionRow`. `age-and-world-ui.md` §9.4 still says it returns nothing, which is stale.
  - `InstitutionsQuery.Lifecycle/Stage` (`:114-138`) and `RoadLens` already read state; roads already reflect state.
  - The `Composition.AssignSlots` algorithm on the unmerged `origin/claude/civdemo-work-b1z2y4` is worth porting, but only the algorithm. Its demo content was deliberately rejected (`age-and-world-ui.md:92`).
- **Validation:**
  - Live-UI harnesses: `UiFrameHarness` and the 9-state `PlayabilityGate`, including its `ShiftCheck` invariance idiom.
  - Persistence: the `TaxGrievanceSchemaTests` populated-table template, `IntegratedPinAttributionTests`, and `IntegratedSaveLoadBatteryTests`.

### Gaps (absent today)
- **Part 1, map:**
  - There is no map-viewport rect. Fit, clamp, level of detail and hit tests all use the full window, which the chrome covers (`GameUi.cs:486, 731-738`; `Camera.cs:24-26`).
  - Zoom jumps instantly by ×1.25 per notch, and level of detail has three discrete bands.
  - `RiverMesh` is rebuilt on every zoom change (`SimUiGame.cs:238-251`).
  - Settlement hit rectangles do not match the drawn names (`GameUi.cs:871-894` vs `WorldLens.cs:569-571`).
  - There is no frame-cost tool for the map; `Headless/FrameCost.cs` covers the Research screen only.
  - The finest state is 4 km (D-015), so any settlement-scale detail can only be decorative.
- **Part 2, units:** there is no move order, AP, zone of control or combat statistic. `OrderKind` stops at 8 (`Kernel/OrderLog.cs:7-122`), and `AgeTransitionSystem` is the only writer of `MilitaryUnits` (`SystemCatalog.cs:386-398`).
- **Part 3, fog:**
  - No ref anywhere holds per-polity extent or visibility state.
  - Hidden information leaks today through the projection, hit test and Tab, the labels, the HUD world totals (`HudModel.cs:89-90`), the annals (`StateChronicle.cs:41-139`) and the formation cards (`InfoQuery.cs:994-1034`).
  - Emergent colonisation takes the best site on the whole map (`ColonizationSystem.cs:211-212`).
  - Road route planning admits undiscovered endpoints (`RoadDevelopmentQuery.cs:150-189`).
- **Part 4, founding:**
  - There is no `FoundSettlement` order.
  - Reachability is not checked, and a test pins that (`ColonizationTests.cs:852-884`).
  - "Unclaimed" has no meaning at land level: `ClaimRow` has no writer (`WorldState.cs:580-591`).
- **Part 5, resources:**
  - There is no per-cell resource quantity.
  - The nine deposit goods collapse onto three terrain channels.
  - Ore spread is keyed by settlement id, not by cell (`WorldFounding.cs:487-497`).
  - D-051 §2 (resource availability classes) is not implemented.
- **Part 6, discovery shortcut:**
  - No research unlocks a resource. Extraction is not gated by research (`ProductionSystem.cs:346-392`; ADR-033 D1).
  - `ResearchCompletedRow` has no turn field (`WorldState.cs:837`).
- **Part 7, Age themes:** on the map, era theming changes ink only. Settlement composition is pinned identical at every Age (`WorldMapStateTests.cs:529`).
- **Part 8, settlement growth:**
  - Placement reflows whenever a count changes (`WorldLens.cs:497-505, 531-535`).
  - Institutions are not staged by count, and the footprint is a circle.
- **Headless testing:**
  - The harness cannot read text drawn by ImGui widgets.
  - The lens DrawList is not kept after the frame (`GameUi.cs:731-747`).
  - Nothing checks that every control is covered by the gate.
  - The GPU terrain pass never appears in headless tests.

### Ratified constraints that shape the plan
- **Settlement founding:** D-051 §5 is RATIFIED for M5 (`d051:65-75`).
- **Discovery:** D-040 A4/B1/B2/D3 rule that the map is a discovered extent, not an overlay; that there is one exploration mechanism; and that discovery belongs to M6.
- **Reconnaissance and unit fog:**
  - D-039 B1 makes reconnaissance cost food and people; B6 (how scouts are represented) is open.
  - D-039 C rules LIVE/GHOST/UNKNOWN display for enemy formations, scheduled for M7.
- **Movement:**
  - Movement belongs to M7 (`milestones.md:38-43`); ADR-033 D7 escalated its scheduling.
  - CR-017 §3 (Action Capacity vs Law 3) and §5 (off-road movement vs D-009) are OPEN.
- **Visual milestone:** D-038 E1/H7 assign the object tier to an inserted visual milestone that the rebased roadmap table omits.
- **Visual content rules:**
  - D-038 C1 (no anatomically detailed figures), H2 (districts abstracted) and H5 (no era gates on what is drawn).
  - D-043 D1–D3: population sets a settlement's visual class; institutions are aggregated per type and staged by count; visual change is gradual.
  - The style bible keeps the map substrate the same in every Age.
  - ADR-033 D8: the Age changes HOW things are drawn, not WHAT.
  - The DD-13 hold forbids generic Age effects, so Age themes stay presentation only.
- **Kernel contract:**
  - The OrderRecord shape is fixed.
  - Each table has one writer.
  - Saves load only at the exact schema version (D-008).
  - Laws 6 and 7: every explanation derives from state, and the AI gets the player's information class.

### Presentation vs simulation
**Presentation-only, can proceed now (S8 §1 LIVING class, or already ruled):**
- Map viewport, eased zoom, continuous level of detail and render-only detail-on-zoom.
- Hit geometry, legend and frame-cost measurement.
- An all-known player-knowledge seam that later becomes the fog filter.
- The site inspection card.
- Stable placement for the existing placeholder glyphs.
- Era "hand" tokens (line, ornament and plate style per Age).

**Simulation or persistence:**
- Ratified and ready to build: the Found Settlement order and AI policy, and UI resume hardening.
- Waiting on the Director: known extent (schema v33), extent gating, and movement and AP.

### Risks
- **Any schema bump moves every golden and strands playtest sessions.** Resume ignores `manifest.SchemaVersion` and throws on the trace mismatch (`UiSession.cs:698-746`). Packet S0 fixes this first.
- **Fog applied only in the projection would still leak** through ImGui text, which the harness cannot see. Packet P6 adds the means to check it.
- **Order-kind and schema numbers collide** if packets land in parallel: kind 9 and v33 go to whichever lands first (`CanonicalSchema.cs:80-85` precedent).
- **Some tests and gate rows fail by design** and must be changed deliberately inside the packet that breaks them:
  - immobility gate rows (`PlayabilityGate.cs:1317-1337`);
  - LOD thresholds (`WorldMapStateTests.cs:819-836`);
  - the path-count pin (`WorldLayerOwnershipTests.cs:150-171`);
  - the every-Age composition pin;
  - the `MapLayer` registry.
- **Emergent colonisation is almost dormant** (CR-020 §B.2), so founding tests must use order-driven or stranded rigs.
- **Early turns last 10 years**, which makes any distance-per-turn AP budget meaningless.
- **Push hazards:**
  - Local `claude/new-session-yavghm` in `/home/user/civDemo` sits at 93270cd, behind its remote at 2dc6af2; a force-push from there would drop the only remote pointer to the candidate.
  - `m5p-integration` has its upstream misconfigured to `origin/m5-hardening`.
  - The worktrees `wt-weather`, `wt-wverify` and `wt-wbat` contain uncommitted changes and must not be used for verification.

## Implementation plan — packets, branches, dependencies

| Packet | Title | Parts | Kind | Depends on | Wave | Branch |
|---|---|---|---|---|---|---|
| W0 | Docs freshness and workstream coupling map | all | docs-only | — | docs | `m5w-w0-freshness-coupling-map` |
| S1a | Siting and founding read seams (refactor, no behaviour change) | 4, 5 (prerequisite) | simulation | — | A | `m5w-s1a-siting-read-seams` |
| S1b | Found Settlement order (D-051 §5) through the existing colonisation path | 4 | simulation | S1a | C | `m5w-s1b-found-settlement-order` |
| P6 | Headless observability: frame-text log, retained lens commands, gate coverage, camera invariance | all (validation for 1, 3, 4, 5) | presentation-only | — | A | `m5w-p6-headless-observability` |
| P1 | Map viewport rect and a MapRect-aware camera | 1 | presentation-only | — | B | `m5w-p1-map-viewport` |
| P4 | Map frame-cost measurement and a projection cost fix | 1, 8 (baseline for 3 and 5) | presentation-only | — | A | `m5w-p4-map-frame-cost` |
| S0 | UI resume hardening: schema-version mismatch and ADR-034 checks | 2, 3, 4 (persistence prerequisite) | persistence | — | A | `m5w-s0-resume-hardening` |
| P3 | Hit geometry from LensFrame, and an in-game map legend | 1 (prerequisite for 3 and 8) | presentation-only | P1 | B | `m5w-p3-lens-hit-geometry-legend` |
| S1c | AI founding policy using the same order and predicate | 4 | simulation | S1b | C | `m5w-s1c-ai-founding-policy` |
| P7 | Player-knowledge read seam (all-known) and leak hardening | 3 (prerequisite), 5 | presentation-only | P3, P6 | C | `m5w-p7-player-knowledge-seam` |
| P8 | Site and tile inspection card (read-only; candidate suitability decomposed) | 4, 5 | presentation-only | S1a, P1, P7 | C | `m5w-p8-site-inspection` |
| S1d | Found Settlement UI: placement mode and order emission | 4 | presentation-only | S1b, P8, P3 | C | `m5w-s1d-found-settlement-ui` |
| P2 | Eased zoom and continuous level of detail | 1 | presentation-only | P1, P4 | B | `m5w-p2-eased-zoom-continuous-lod` |
| P12a | Stable placement and size legibility for the existing settlement glyphs | 8 | presentation-only | P3 | C | `m5w-p12a-settlement-placement-stability` |
| P13 | Era map hand tokens (HOW only) | 7 | presentation-only | — | A | `m5w-p13-era-map-hand-tokens` |
| P5 | Render-only detail-on-zoom layer | 1 | presentation-only | P2, P4 | B | `m5w-p5-detail-on-zoom` |
| W0b | Record the Director's rulings for this workstream (D-052) and any priced overrides | all | docs-only | W0 | docs (after rulings) | `m5w-w0b-director-rulings-record` |
| P9 | Resource and suitability overlay layers with legend and opacity | 5 | presentation-only | W0b, P7, P8, P2 | D (after ruling 4) | `m5w-p9-resource-overlays` |
| P10 | Resource-discovery shortcut (UI-only, turn-expiring) | 6 | presentation-only | P9 | D (after P9) | `m5w-p10-discovery-shortcut` |
| P11 | Unit token representation and an honest detail window | 2 | presentation-only | W0b, P3, P7 | D (after ruling 3) | `m5w-p11-unit-token-detail` |
| P12b | Composed settlement sprite: parts, global light, count staging, organic footprint | 8 | presentation-only | W0b, P12a | D (after ruling 4) | `m5w-p12b-composed-settlement-sprite` |
| P14 | Age-styled object layer: register, relief overlays, unit silhouettes | 7 | presentation-only | W0b, P13, P12b | D (after ruling 4) | `m5w-p14-era-object-register` |
| S2a | Known extent state (D-040) and discovery system — schema v33 | 3 | mixed | W0b, S0, P7 | E (after ruling 2) | `m5w-s2a-known-extent` |
| S2b | Gate player and AI actions on known extent | 3, 4 | simulation | S2a, S1b, S1c | E | `m5w-s2b-extent-gating` |
| S2c | Fog presentation: three-state map, filtered selectors, annals and cards | 3, 5 | presentation-only | S2a, P7, P6 | E | `m5w-s2c-fog-presentation` |
| S3a | Formation move order and the MilitaryUnits ownership ADR | 2 | simulation | W0b, S1a | F (after ruling 1) | `m5w-s3a-formation-move-order` |
| S3b | Action Capacity state and budget truncation | 2 | persistence | S3a | F | `m5w-s3b-action-capacity` |
| S3c | Move UI: right-click and Move button, route/cost preview, AP in the detail window | 2 | presentation-only | S3a, S3b, P11, S2c | F | `m5w-s3c-move-ui` |

## Packet detail

### W0 — Docs freshness and workstream coupling map

Docs only, all append-only, no code.

(a) A dated pointer in current-state.md recording:
- 2dc6af2 is REMOTE as origin/claude/new-session-yavghm, with CI runs 38032099613 and 38032099661 green;
- 82bde01 is LOCAL only;
- the base and naming used by the workstream.

(b) Append notes on stale statements:
- age-and-world-ui.md §9.4: the institution seam reads InstitutionRow;
- current-state's claim that civdemo-work-b1z2y4 is unrelated: it is 22 commits ahead, with world-visualization code;
- ColonizationTests.cs:864-866: its provenance claim is absent from t4.4-review-record.md.

(c) A coupling map for the eight parts covering:
- the tables, systems, pins and gate rows each part touches;
- order-kind and schema-number reservations: kind 9 = FoundSettlement, kind 10 = MoveFormation, v33 = the first schema bump to land;
- which packets move goldens and which do not.

(d) queue.md lines for the 'better way' items.

**Acceptance:**
- Every factual claim cites a file:line or a git command whose output the author measured.
- No frozen or ratified text is rewritten; every change is an append-only note (GOV-4 §6).
- LOCAL, REMOTE and MAIN are stated separately for every branch named.

**Tests:**
- None (docs). The banned-constructs and read-isolation scripts are unaffected and still run.

### S1a — Siting and founding read seams (refactor, no behaviour change)

Extract a pure SettlementSiting.IsLegalFrontierCell(frontier, terrain, spacing, occupied, cell). ChooseFrontierSite then calls it, so the check is no longer inline at :361-387.

Make these private helpers public pure functions, with no behaviour change: BoxSum, the water-access distance factor, IsOccupied, and WorldFounding.HinterlandMeans.

Add a read-only route-cost query wrapping Pathfinder.FindPath. It returns per-node cumulative cost (TraversalLattice.StepCost plus lane cost) and the reachable prefix under a cost budget. Found Settlement previews share it, and any future M7 movement will too.

No new state and no new constant.

**Acceptance:**
- Every golden is byte-identical: toy, founded, first-reign, driven, and ci.yml FOUNDED_GOLDEN on the base tree.
- ChooseFrontierSite returns identical sites over a 20-seed sweep, before and after.
- The route query's final cumulative cost equals FindPath.TotalCost bit-exactly, on random pairs with and without built roads.
- The public HinterlandMeans field, evaluated at each founded site, reproduces that site's DepositRow bit-exactly for goods whose spread is 0.
- The banned-constructs, read-isolation and readonly-proof scripts are green; inherited reds are listed.

**Tests:**
- Equality tests before/after the refactor (sites, goldens).
- Route-query vs FindPath cost equality.
- Tie-dense test for the route query's composite (cost, node id) ordering.
- A field-equals-DepositRow test across seeds, including colony-founded settlements.

### S1b — Found Settlement order (D-051 §5) through the existing colonisation path

ADR-036 defines OrderKind 9 FoundSettlement inside the fixed record shape: TargetId = source settlement id; Amount = the site cell as an exact whole number below Size².

Validation:
- a load-time range case in ValidateRecord;
- the source settlement joins both settlement-target kind lists in OrderValidation (:88-89, :186-187), with the ADR-034 deferral.

A pure FoundingQuery.Check returns a reason enum. Its arms:
- the issuer controls the source;
- the site is land, with score > 0 and at or above the floor;
- the site is not occupied, is spacing-legal, and is unclaimed (its origin node is in no CatchmentNodes row);
- the site is reachable (FindPath on the road-aware lattice);
- the site is known (inert, all-known, until extent exists);
- party ≥1, the source keeps ≥1 person per non-empty bucket, and ProvisionsFor ≥1.

ColonizationSystem.Step gains an order-intake loop that runs in log order before the emergent loop. It:
- allocates the party per bucket by a deterministic proportional floor with a stable bucket-index remainder (the party fraction is content TUNE);
- calls the existing Found(siteCell) and TransferProvisions;
- grows spacing and occupancy exactly as at :220-226.

Founding is instantaneous. Delivery: an order stamped t founds in step t→t+1. The first legal order per source per turn wins.

Also updated in this packet:
- the TurnRecord decode;
- the ResearchOrderTests first-unknown-kind case (moves to 10);
- the IntegratedSaveLoadBattery and ci.yml kind lists;
- the SystemCatalog GoodStocks share record;
- the research.json providedBy text and the AvailableActionsQuery descriptor.

The player path only: the AI policy is S1c.

**Acceptance:**
- Goldens with no orders are byte-identical; this is the proof that emergent B1 'continues unchanged'.
- Turn-exact pin: an order stamped t yields a SettlementRow in state t+1 with FoundedTurn t+1, a catchment row in t+2, control Strength 0.0 in t+1 and t+2, and reach from t+3.
- People and grain are conserved exactly (Ledger audit, exact equality), and the source never empties a bucket.
- Every rejection arm (uncontrolled, unknown source, water, occupied, inside spacing, inside a catchment, unreachable, empty granary, party 0) gives a world hash equal to the no-order step.
- An order AvailableActionsQuery lists is exactly an order ColonizationSystem accepts, and vice versa.
- `sim run --orders`, `sim replay` and `sim inspect` VERIFY a log containing kind 9, including an ADR-034 case where the source is a colony founded mid-game.
- A mid-run save/load followed by continuation equals the uninterrupted run, hash for hash, every turn.
- The banned-constructs, read-isolation and readonly-proof scripts are green; inherited reds are listed by name.

**Tests:**
- FoundSettlementOrderTests: delivery pin, conservation, one test per rejection arm, and same-turn multiplicity (first legal order wins).
- OrderLog load-validation theory: non-whole, negative, NaN and ≥Size² cells; negative source.
- A tie-dense test of the party allocator's remainder rule.
- IntegratedSaveLoadBattery with save points between issue and effect.
- A cross-process leg: the founded run with a scripted kind-9 log is replayed hash-equal.
- Mutants (stamping at t instead of t+1, skipping reachability, omitting the provisions transfer, emergent loop before order loop), each killed by a semantic test and bounded by a stated multiple of the clean baseline.

**Data gaps:** 'Unclaimed' has no land-level state; the catchment-node reading goes in the ADR, for the Director's ruling. Party size cannot fit in the record, so it is a content TUNE fraction. 'Enough food' is the source's own grain (D-051 §6 deferred). The 'known extent' arm is inert until S2a. Emergent B1 founding is near-dormant (CR-020 §B.2), so rigs must be order-driven.

### P6 — Headless observability: frame-text log, retained lens commands, gate coverage, camera invariance

(1) A read-only per-frame text log, Ui.FrameText. It records every string passed to GameUi's ImGui text helpers and every TextCmd that DrawListImGuiBackend replays.

(2) The last frame's lens DrawList commands, exposed read-only as Ui.LastLensCommands.

(3) A gate control-coverage check. Per state, every UiControls name and every Hits kind seen must have a GateRow, or an allowlist entry with a reason.

(4) The gate rows for drag pan, wheel zoom and WASD additionally assert that WorldHash and QueuedOrders are unchanged (the ShiftCheck idiom).

**Acceptance:**
- FrameText contains the selected settlement's name from the selection card, and contains no settlement name when nothing is selected.
- A fixture control recorded without a gate row makes the coverage check fail.
- A mutant where a camera operation queues an order is caught by the camera rows.
- `--smoke` is green on all 9 states, and the world hash is unchanged by the recorders (they only observe).

**Tests:**
- FrameText unit tests in the [Collection("ImGui context")] collection.
- A semantic test for the coverage-check mutant.
- Camera-row invariance tests.
- The existing Sim.Ui.Tests stay green.

### P1 — Map viewport rect and a MapRect-aware camera

Derive MapRect each frame from Placed(Status/Command/Context).

Camera gains a viewport origin offset. With it:
- MinZoom fits the MapRect;
- Clamp works against the MapRect;
- CameraFocus.CenterOn lands at the MapRect's centre;
- LevelFor, Paint, the hit tests and SimUiGame's render matrix receive the MapRect.

The map is still drawn full-window beneath the chrome. Camera stays pure UI state.

**Acceptance:**
- ScreenToWorld and WorldToScreen round-trip inside the MapRect; the world point under the cursor stays fixed during zoom with an offset.
- At the clamp, no world sits under the status or command bar at fit zoom.
- CenterOn places each settlement within 0.5 px of the MapRect centre, where the clamp allows.
- Opening the context panel re-fits the view without moving the world point under the cursor.
- No camera operation changes the world hash or the order log.
- The PanelLayout pins (>85% clear, >55% with a panel open) are kept, or re-pinned with a recorded reason.
- The gate is green at 1080x640 and 1920x1009.

**Tests:**
- CameraTests extended for the offset viewport.
- PanelLayoutTests.
- A read-only hash test.
- Gate map rows (drag, wheel, WASD, CenterOn).

### P4 — Map frame-cost measurement and a projection cost fix

Add `Sim.Ui --frame-cost map`. It runs WorldLens.Paint plus the ImGui replay, sweeping pan and zoom through every band, at 1280x800, 1920x1009, 2560x1440 and 3840x2160. It runs on the canonical world and on a rigged world with a high settlement count. It reports median ms, max list vertices, commands and culled commands.

In WorldProjection.Build, replace the per-turn TraversalLattice.Build with the Terrain.Size/4 stride.

This is measurement only. No budget is set (ADR-009).

**Acceptance:**
- The tool runs headless, exits 0 and prints a per-configuration table; results are recorded with the commit SHA.
- WorldProjection output (SVG bytes and LensFrame) is identical before and after the stride change.
- The flag is registered in UiArgs.Usage and in a bounded process test.

**Tests:**
- Projection equality before and after the change.
- UiArgsUsageTests.
- A bounded process test for the new mode.

**Data gaps:** No fixture with a high settlement count exists. The smallest solution clones a world and appends settlements built with the simulation's own row types (the InstitutionWorldFixture pattern). The rigged world is used for UI measurement only and is never persisted.

### S0 — UI resume hardening: schema-version mismatch and ADR-034 checks

UiSession.Resume compares manifest.SchemaVersion with CanonicalSchema.Version. On a mismatch it replays from seed and log without the trace-hash check (the D-008 recovery path) and shows an explicit notice that the session was recorded under schema vN.

ReplayTo calls OrderValidation.ValidateAgainstWorld once and ValidateAtDelivery before each Step, adopting ADR-034 §4.

The behaviour is recorded as an append note in ADR-034.

**Acceptance:**
- A session whose manifest carries an older SchemaVersion resumes by replay, shows the notice, and continues hash-identically to a fresh replay.
- A log containing an order with an illegal deferred id is rejected on resume with the same diagnostic that `sim replay` gives.
- All goldens are unchanged.

**Tests:**
- ChronicleAndResumeTests: the version-mismatch path.
- UiSessionReplayTests: the validation path, following the OrderDeliveryValidationTests pattern.

### P3 — Hit geometry from LensFrame, and an in-game map legend

WorldLens.Paint records the settlement name-plate and footprint rects in LensFrame, as it already does for UnitPlacements. SettlementSelection, label clicks and Tab cycling hit-test that geometry with the composite key (distance, id). The legacy rect computation in DrawNameLabels (GameUi.cs:871-894) is deleted.

A small legend is painted through DrawList (never as an ImGui tooltip) in the era's hand. It lists LensFrame.Layers plus the WorldProjection.Absent lines.

**Acceptance:**
- At sampled zooms across every band, each hit rect contains the drawn name's TextCmd bounds and the footprint.
- Clicking a drawn name in the harness selects that settlement.
- Ties are broken by id.
- The legend lists exactly the layers drawn this frame plus the Absent lines.
- No tooltip window appears, and the world hash is unchanged.

**Tests:**
- Tie-dense selection test.
- A drawn-versus-hit containment test.
- A harness click test.
- Legend accounting through MapLayer Charge.

### S1c — AI founding policy using the same order and predicate

An AiFoundingPolicy in AiOrders. It emits FoundSettlement records through the same factory and FoundingQuery predicate the player uses.

Sites are ranked by the existing CandidateScores with the composite key (score DESC, cell ASC), limited to cells reachable from the source.

The trigger is a conservative, deterministic rule over existing readouts: the source has provisions and an admissible site exists. Its threshold is a TUNE value, recorded.

The ci.yml AI-leg kind range is extended to cover 9. The AI's read of the whole map is recorded as a known leak until S2b.

**Acceptance:**
- The AI emits only orders that FoundingQuery accepts; a player and an AI order for the same case produce identical records.
- AI goldens are re-pinned with CAUSE comments and a control showing goldens without orders unchanged.
- The 600-turn AI leg is VERIFIED cross-process.
- How far the Age milestones (a4_expansion, a6_towns) move is measured and reported.

**Tests:**
- Tie-dense site-ranking test.
- Player-versus-AI record equality (the RoadDevelopmentTests AiAndPlayer_* pattern).
- Golden attribution control.
- The cross-process AI leg.

**Data gaps:** No ratified AI expansion policy exists; m5-ai-constitution.md is a planning document. The smallest rule is the TUNE-gated trigger above, flagged for the Director at handback.

### P7 — Player-knowledge read seam (all-known) and leak hardening

One Sim.Ui PlayerKnowledge view, built per turn from a Sim.Core read seam. It answers Known(node), Visible(node) and KnowsPolity. It is all-known until S2a lands, so behaviour stays byte-identical.

Every player-facing reader goes through it:
- WorldProjection.Build (observer argument);
- SettlementSelection hit test and CycleNext, and the name labels;
- HudModel world totals;
- annals, filtered by subject in UiSession;
- PlayerViews.Settlement and Units;
- InfoQuery.FormationCard and UnitCardModel for foreign units.

Also in this packet:
- raw settlement ids are hidden from player text;
- InfoQuery.DepositSector no longer scans foreign settlements;
- check-read-isolation.sh is extended with an allowlist for raw Sim.Ui reads of Settlements, MilitaryUnits, Buckets and Deposits;
- a source-guard test is added.

PlayerKnowledge is never a second source of truth. Its only future input is S2a's stored extent.

**Acceptance:**
- With the all-known seam, every SVG preview, LensFrame and gate report is byte-identical to before.
- With a test-only knowledge stub that hides one polity, perturbing that polity's hidden settlements, units and stocks leaves these identical: WorldProjection, LensFrame, PlayerViews, annals, HUD totals and FrameText.
- A hidden settlement cannot be selected by click or by Tab.
- The read-isolation script fails on a new raw read outside the allowlist.

**Tests:**
- Metamorphic non-interference tests with the stub.
- A source-guard test.
- A 'Build ignores knowledge' mutant killed by a semantic test.
- Gate rows: 'Tab never reaches a hidden settlement' and 'a click on a hidden position selects nothing'.

**Data gaps:** No authoritative extent exists yet; the all-known default is a placeholder for S2a. The HUD world-total semantics under fog follow the fog ruling; the default is a sum over known settlements.

### P8 — Site and tile inspection card (read-only; candidate suitability decomposed)

A read-only Sim.Core/State/SiteAssessment that returns only authoritative values for one cell:
- region fertility sum, water-access distance and fraction, the unjittered score and its percentile against the floor;
- spacing, occupancy and catchment-owner status;
- the terrain fields at the cell;
- hinterland moisture and elevation means;
- expected deposits per channel (ore spread shown as a range, or the exact roll for id maxId+1 labelled 'if founded next', via AddDepositsForSite on a scratch table);
- would-be arable km² via Pathfinder.Partition;
- the nearest settlements, and the route and cost from a chosen source (S1a query);
- each FoundingQuery reason.

InfoKind.Cell gets a card registered from the lens, with a painted hint (no ImGui tooltip). It refuses cells PlayerKnowledge does not know. Cells are picked with Camera.ScreenToWorld against the MapRect.

There is no composite suitability score (D-043 C2).

**Acceptance:**
- Each value equals the simulation function's own value; the 'if founded next' deposit preview equals the DepositRow of a colony actually founded at that cell with that id, bit-exactly.
- The world hash is unchanged by assessment and by opening the card.
- Unknown cells are refused (tested with the P7 stub).
- The gate InfoArea (ShiftCheck) row is green.
- The card shows the factors separately, with no weighted composite.

**Tests:**
- Value-equality tests against SettlementSiting, WorldFounding and Pathfinder.
- A read-only hash test.
- A refusal test.
- A tie-dense test for nearest-settlement ordering (distance, id).
- A gate row.

**Data gaps:** There is no per-cell resource quantity: deposits are per settlement, and ore spread is keyed by settlement id. The card therefore shows expected channel values, labelled as such. The nine deposit goods collapse onto three channels. D-051 §2 will change the deposit function, so the card must keep calling that one function and never copy it.

### S1d — Found Settlement UI: placement mode and order emission

A 'Found settlement' action on the action surface for an owned source opens a placement mode on the map.

Hovering a cell shows the painted hint plus the docked Site card (P8), with:
- the legality reason from FoundingQuery;
- the route and cost from the source (S1a);
- the party and provisions cost.

Clicking emits through a single UiSession.EmitFoundSettlement (FoundSettlementOrderFactory), guarded by the same predicate. Esc cancels.

Controls.Record is called for every control, a placement GateState and gate rows are added, and the hotkeys are added to the monkey's key list.

**Acceptance:**
- The UI never queues an order the system would reject (the ActionEmitterGuardTests pattern).
- A UI-queued order replays hash-for-hash (the FoundingEquivalenceTests pattern).
- Clicking an illegal cell queues nothing and shows the reason.
- Hover and camera operations change neither the world hash nor the order queue.
- Gate coverage is green, including the new controls.

**Tests:**
- A UiFrameHarness flow: select source, hover, click, End Turn, colony present in state t+1.
- An illegal-click test.
- An emitter-guard test.
- Gate rows and a monkey run.

**Data gaps:** None beyond S1b's.

### P2 — Eased zoom and continuous level of detail

Camera holds target and shown zoom and centre, eased by frame-rate-independent exponential smoothing (the ProgressionCamera.Advance pattern).

Size, alpha and label-role functions become continuous in screen scale, with layers cross-faded. WorldZoom is kept as a derived band for the gate and tests. The level-of-detail bands are defined by visible km.

RiverMesh is rebuilt only when the zoom crosses a quantized band (2^(1/4) steps).

The threshold pins and gate wheel rows are updated deliberately, each with a recorded reason.

**Acceptance:**
- The world point under the cursor is fixed once the ease settles.
- The river buffer is not rebuilt every frame during an ease (asserted with a rebuild counter).
- Layer alpha is monotone across band edges, so no layer pops in or out.
- The derived WorldZoom bands are documented.
- The P4 map sweep is re-run and reported against its baseline.
- The gate is green with the restated wheel rows.

**Tests:**
- Eased-camera property tests.
- A river rebuild-count test.
- Continuity tests at sampled scales.
- The deliberately updated threshold pins.

### P12a — Stable placement and size legibility for the existing settlement glyphs

Replace the index- and count-spaced rings (WorldLens.cs:497-505, 531-535) with fixed slot lattices.

Placement:
- Claim priority is keyed on InstitutionRow.Id/FoundedTurn and on the Structures (ProjectId, ordinal) row order.
- Each slot is chosen by a stable 64-bit hash with deterministic probing. This ports the b1z2y4 AssignSlots algorithm, not its content.
- Dwelling blocks fill a fixed lot order and are hidden in place under occupied slots. Extra rings replace the hard cap of 48.

Size: a population-primary visual class on a compressive scale (D-043 D1), with data-defined thresholds.

Only the existing glyphs are used: no new parts, art or count staging. Those are P12b.

**Acceptance:**
- A later arrival never moves an earlier part, and a change of count, zoom or pan moves nothing already placed.
- The result is identical for any table input order.
- Mark size is strictly monotone in population across the measured range, with no saturation below a stated population.
- The every-Age composition pin and the read-only world-hash test still pass.
- The LensFrame hit rects follow the placed parts.

**Tests:**
- Tie-dense slot-ordering test.
- Stability property tests on populated fixtures (InstitutionWorldFixture, RoadWorldFixture).
- A monotone legibility test.
- Slots identical after Resume and across processes (composition is a pure function of state).

**Data gaps:** StructureRow has no per-instance turn. (ProjectId, ordinal) keys would relocate parts only if counts fell, and structures are append-only today (WorldState.cs:756-765).

### P13 — Era map hand tokens (HOW only)

Following era-ui.md §8, add map presentation tokens to EraTheme and MapInk:
- glyph stroke weight and hand;
- token plate shape;
- banner and name-plate ornament.

Every era gets a value; the tokens are lerped by ThemeLerp and included in Canonical(). Painters switch on tokens, never on the era number.

The terrain substrate and the state-driven composition are untouched, and no Age-keyed gameplay effect is introduced (DD-13).

**Acceptance:**
- LensFrame geometry and hit regions are identical in all nine eras; only tokenised ink and hand differ.
- The nine-era map previews are byte-identical across runs.
- The TextCensus readability floors hold in every era.
- The every-Age composition pin passes unchanged, or is re-scoped only to 'geometry and composition identical, hand may differ' with the reason recorded.

**Tests:**
- EraSurfaceTests continuity extended to the map lens.
- MapInkEraTests.
- A preview byte-identity test.
- TextCensus.

### P5 — Render-only detail-on-zoom layer

The queue.md:3 fBm terrain detail, resampled at view resolution and blended in above a zoom threshold. It gets a new MapLayer with a single GPU owner and is bound to ParchmentPalette. MaxZoom is raised only together with it.

The detail is decorative: it must never suggest resources or terrain classes the simulation lacks (D-015, 4 km is the finest state), and the legend says so.

**Acceptance:**
- The layer has one registered owner and the registry pins are updated deliberately.
- Samples are byte-deterministic per seed and stay on-palette.
- The legend marks the layer decorative.
- The P4 sweep is reported; the world hash is unchanged.

**Tests:**
- Determinism and palette-binding tests.
- The registry/ownership test.
- A read-only hash test.

**Data gaps:** No state exists below 4 km (D-015). The detail is decorative by construction.

### W0b — Record the Director's rulings for this workstream (D-052) and any priced overrides

Record the Director's rulings on the governance issues verbatim as D-052. Add dated append-only notes where they apply: milestones.md, D-038 E1, D-040 A4, D-043 D2, the style bible. Write a priced S8 §2 override ADR for any part pulled ahead of its milestone, stating what breaks, which tests and docs change, and the schedule price. Record the CR-017 §3/§5 rulings if they are given.

**Acceptance:**
- Each ruling is quoted and cited; no frozen text is rewritten.
- Every override is priced per S8 §2.

**Tests:**
- None (docs).

**Data gaps:** Waits on the Director.

### P9 — Resource and suitability overlay layers with legend and opacity

Expected-abundance rasters per channel (moisture; water proximity = moisture²; elevation) and the suitability-factor rasters (region fertility, water access, floor and spacing legality). They are computed at lattice-node granularity (256²), once per session, from immutable terrain through the public S1a functions.

The overlays form one lens-owned MapLayer. Toggle and opacity are UI state, with plain-text legend rows:
- which goods each channel drives;
- 'expected abundance (rate multiplier); actual varies ±spread';
- discovered / present / accessible / extractable, each from an existing predicate.

Realized ore rolls appear as per-settlement markers from DepositRow. Everything is masked by PlayerKnowledge.

**Acceptance:**
- The field at each founded site equals that site's DepositRow for goods with spread 0.
- No layer per good claims differences the simulation does not have.
- The world hash is unchanged.
- Output is identical when terrain or deposits under unknown nodes differ (P7 stub).
- The bake is byte-deterministic, frame cost is reported, and gate rows cover toggle and opacity.

**Tests:**
- Field-equals-DepositRow test.
- Metamorphic leak test.
- Bake determinism test.
- Layer accounting; gate rows.

**Data gaps:** There is no per-cell resource density. D-051 §2 (availability classes, regional richness) is not implemented and will replace the deposit function. Presence is always true until §2 introduces scarcity.

### P10 — Resource-discovery shortcut (UI-only, turn-expiring)

A session history kept in UiSession.Step. On an IsKnowledgeEligible transition of a content-declared entity whose effect is real (activity.farming opens the food-potential overlay; a recipe whose inputs include a deposit good, e.g. bronze_casting, opens the copper/tin channel), it offers a 'show on map' shortcut that opens the overlay masked by known extent.

The shortcut expires when Clock.Turn ≥ stamp + N, where N is TUNE UI content. Replay rebuilds the history on Resume. It is labelled 'now usable', never 'discovered'.

**Acceptance:**
- The shortcut fires only on an eligibility transition, and no node ids appear in code.
- Expiry is turn-exact at turn T+N.
- The history is identical after Resume.
- The world hash is unchanged.
- A gate row passes in the 'research done' state.

**Tests:**
- Turn-exact expiry pin.
- Resume rebuild equality.
- Transition-only firing test.
- Gate row.

**Data gaps:** Research unlocks no resource in the simulation (extraction is ungated; ResearchCompletedRow has no turn), so the trigger is a reinterpretation awaiting the part 6 ruling.

### P11 — Unit token representation and an honest detail window

Following the recommended unit-representation ruling:
- one token per formation, with a provisional silhouette emblem keyed by its current Identity;
- a detail window showing only MilitaryUnitRow fields plus family, Age form and next-Age plan, with 'strength / attack: not simulated';
- rival formations shown only when visible.

If stacks are ruled instead: a per-(anchor, identity) ×N token with a stack picker. It keeps per-row hit tests and never merges formations observed unequally.

**Acceptance:**
- Tokens drawn equal rows exactly (or stacks equal groups exactly).
- No statistic is invented.
- Fan or stack order is tie-dense, and every row can be hit-tested.
- The anatomy fence is respected: silhouette or token only.

**Tests:**
- Token/row accounting.
- Tie-dense ordering.
- Card field provenance test.
- Leak test with the P7 stub.

**Data gaps:** There is no unit count, manpower or combat statistic; every empire has exactly one Warband.

### P12b — Composed settlement sprite: parts, global light, count staging, organic footprint

One ObjectLight constant with a contact shadow.

Parts authored once, procedurally (D-038 D1): tier forms, dwelling block, granary, workshop, university halls. They are composited at draw time from SettlementComposition.

Also:
- D-043 D2 count stages (1/10/20/30) per type, with the number shown at high zoom;
- an under-construction state glyph from InstitutionsQuery.Lifecycle;
- an organic footprint polygon derived from catchment travel cost and adjacent network edges. It is presentation only, with no farmland consumption.

**Acceptance:**
- The parts drawn equal exactly what state implies.
- There is one light angle and ground plane.
- P12a's stability properties still hold, and the H8 limit is applied as ruled.
- The every-Age pin and the world hash are unchanged.
- Frame cost at 300–800 settlements is reported.

**Tests:**
- Parts-from-state tests on populated fixtures.
- Stability property tests.
- Footprint determinism test.
- Frame-cost sweep.

**Data gaps:** Only 7 constructible projects exist. Walls, temples and similar buildings are knowledge only, so they get no parts until M6 realizes them.

### P14 — Age-styled object layer: register, relief overlays, unit silhouettes

Per the Age-theme ruling: a pure register function derived from realized state (structures, institutions, road classes, formation Identity) and the controller's Age, never from the knowledge mask. Built parts are drawn in that register.

Terrain gains object-layer relief, forest and field overlays; the substrate is untouched. Unit silhouettes are keyed by Identity, with every element sharing the one global light.

**Acceptance:**
- Painters switch only on registers and tokens.
- No form is drawn without the state that implies it.
- Geometry and hit regions are identical across eras.
- The style bible's substrate rule holds.

**Tests:**
- Register-from-state tests.
- Nine-era continuity tests.
- Preview byte-identity.

**Data gaps:** Built-state vocabulary is thin until M6, so era differences will be modest.

### S2a — Known extent state (D-040) and discovery system — schema v33

KnownExtentRow(PolityId Holder, int Word, ulong Bits) over TraversalLattice nodes (word = node >> 6):
- rows sorted by (Holder, Word), nonzero words only;
- bits are only ever ORed in (monotone);
- holders are roster polities only;
- seeded at founding from each polity's CatchmentNodes.

A new single-owner DiscoverySystem (SystemId 29) ORs in the visible set each step. Sight.VisibleNodes is the isochrone from controlled settlements (at the existing hinterland budget) and from owned formations. There is no scout bonus and no new constant.

RevoltSystem appends the newborn polity's inherited copy, as a sanctioned share recorded in the SystemCatalog header; annexation takes the union. Pure Known and Visible queries live in Sim.Core/State.

Schema plumbing: Snapshot, Clone, StateEquals, SnapshotDiff layout, and the PathBuildSystem forward. Run `sim bench` before and after.

**Acceptance:**
- Populated-table test: ExpectedLength is 16 bytes per row plus the prefix; the round trip is bit-exact for masks 0, ~0UL and a single high bit; hash equality holds; flipping one bit moves the hash; Clone is independent.
- V33 attribution controls on all four goldens, with the founding-seeded rows stripped.
- Bits never clear across steps, including through revolt and loss of control.
- Turn-exact discovery pin: a colony or unit at a node in state t makes it known in state t+1.
- Snapshot save/load and UI Resume reproduce identical extent rows; the cross-process leg is VERIFIED.
- The `sim bench` bound is stated in the ADR.

**Tests:**
- KnownExtentSchemaTests.
- A monotonicity property test.
- A revolt-inheritance test (copy taken at separation).
- The discovery delivery pin.
- The save/load battery and cross-process determinism.
- Mutants (clearing a bit; sight from foreign units), each killed by a semantic test.

**Data gaps:** D-040 B5 left storage and granularity undesigned; the lattice-node bitset is proposed. The sight budget reuses hinterlandRadiusKm. Scouts are excluded until D-039 B6 / M7.

### S2b — Gate player and AI actions on known extent

Turn on the extent checks:
- FoundingQuery's known-extent arm goes live;
- RoadDevelopmentQuery.Routes requires the other endpoint to be known;
- TradeQuery.CanTrade gains a mutually-known term, if ruled (M6 per the T4.6 note);
- AiConstructionPolicy tests control before reading the queue;
- AI founding candidates go through Known.

An emergent-colonisation admissible mask is applied only if the Director picks option 3 of the fog issue.

**Acceptance:**
- Turn-exact pin: land first known in state t is legal for an order delivered at t+1 and illegal at t.
- Metamorphic AI test: perturbing rows outside the extent leaves AiOrders.For and RoadDevelopmentQuery.Routes identical.
- Player and AI use the same predicate.
- Golden moves are attributed with CAUSE, and corridor teeth are measured.

**Tests:**
- Delivery pin.
- Metamorphic non-interference tests.
- An 'unexplored superior site is never chosen' test.
- Golden attribution.

**Data gaps:** Emergent B1 siting remains omniscient unless the Director rules otherwise.

### S2c — Fog presentation: three-state map, filtered selectors, annals and cards

PlayerKnowledge reads S2a.

A Fog MapLayer with one owner (a lens DrawList veil at lattice granularity, or a GPU mask texture uploaded per turn):
- unexplored: plain parchment, the land absent;
- explored but unseen: desaturated terrain only;
- visible: live.

What each state shows:
- settlements and units: own ∪ visible;
- territory, paths and routes: explored;
- non-visible rival formations: UNKNOWN (no GHOST until M7).

The developer reveal (_developer) never feeds orders.

**Acceptance:**
- UI leakage test: no forbidden token in WorldProjection, LensFrame, TextCmd, FrameText, InfoRegistry, Tab, clicks, annals or HUD totals; a 'Build ignores fog' mutant is killed.
- The same assertions hold after Resume.
- Frame cost is reported.
- A partly-explored GateState is green.

**Tests:**
- The fog leak test from the ui-tests report.
- An optional pixel check with the CPU rasteriser.
- A Resume test.
- Gate rows.

**Data gaps:** Ghost memory needs persisted sightings, which is M7.

### S3a — Formation move order and the MilitaryUnits ownership ADR

An ADR re-splits or extends MilitaryUnits ownership: no second writer and no second position table.

OrderKind 10 MoveFormation: TargetId = unit id, Amount = destination cell.

Legality is decided at consumption on PREV through the S1a route query:
- the unit is owned;
- it is a land family (naval and air are rejected);
- a path exists.

Positions snap to lattice nodes or lane endpoints. The absence of ZOC is recorded. Unit ids get the ADR-034 deferral. Delivery is t→t+1, and modernization timing is preserved.

**Acceptance:**
- Turn-exact pin: an order stamped t changes X/Y first in state t+1.
- An illegal move gives a hash equal to the no-order step.
- The preview equals what is applied (same query).
- UnitModernizationTests stay green.
- The replay and save/load battery includes move orders.

**Tests:**
- Delivery pin, rejection tests and a tie-dense path test.
- OrderLog load validation for kind 10.
- Mutants, each killed by a semantic test.

**Data gaps:** There is no AP until S3b. Road lanes have no intermediate positions. The terrain cost has no forest, hill or desert term.

### S3b — Action Capacity state and budget truncation

AC max is derived from unit-families content. AC remaining is persisted, either by widening MilitaryUnitRow or in an agent-generic MobileAgentState table: v34 if S2a took v33, otherwise v33. Paths are truncated at the budget, and refresh follows the CR-017 §3 ruling.

**Acceptance:**
- A populated or widened-row test (the SnapshotTests v22 pattern).
- An AC exhaustion pin and a refresh turn-exact pin.
- Goldens attributed with CAUSE.

**Tests:**
- Schema test.
- Exhaustion and refresh pins.
- Golden attribution.

**Data gaps:** The AC refresh interval against Law 3 waits on CR-017 §3.

### S3c — Move UI: right-click and Move button, route/cost preview, AP in the detail window

A right-click and the Move button both call one UiSession.EmitMoveOrder through a MoveOrderFactory.

The route and cost preview comes from the same query, cached per turn in the UI. The detail window shows AP remaining and max.

Updated in this packet: LimitationText, AvailableActionsQuery.Military and the ActionSurface note. The gate's immobility rows are rewritten deliberately, and the monkey's right-click now emits orders.

**Acceptance:**
- The right-click and the button append byte-identical OrderRecords.
- The previewed cost equals the charged cost.
- An illegal destination queues nothing and shows the reason.
- The preview never reveals unknown nodes.

**Tests:**
- Harness equivalence of the two paths.
- Preview-equals-applied test.
- Fog leak test on the preview.
- Gate rows.

**Data gaps:** None beyond S3a and S3b.

## Governance issues raised by the audit (full options)

### Does D-038 E1's inserted visual milestone still exist after the 2026-10-03 roadmap rebase, and which parts of this workstream may start before it?

- **Parts:** 5 (map modes, legend, heatmaps), 7 (object-tier art), 8 (composed sprites, count staging, organic footprint), 2 (unit art)
- **Ratified sources:** D-038 E1 (docs/d038-visual-target.md:51), F3 (:67), H7 (:193-199); docs/queue.md:96-108 (the symbology packet absorbs the legend and map-mode system); docs/milestones.md:15-28 (rebased table, frozen per spine-s8:18-21, has no visual milestone) and :93-94 ('every other ratified decision unchanged'); ADR-033 D8
- **Why it needs the Director:** Two ratified records disagree by omission about whether this milestone exists and where it sits. GOV-4 §1 says an agent may not dissolve such a conflict itself. S8 §4 also forbids writing the milestone's spec while M5 is open.
- **Options:**
  1. E1 stands: the visual milestone sits between M5 and M6. Map modes (part 5), the object tier (part 7) and sprites, staging and footprint (part 8) wait for M5's exit and a §4.1 spec. M5 keeps only stable placement for the placeholder glyphs (P12a), era hand tokens (P13) and read-only site inspection (P8).
  2. As option 1, but read-only data overlays (P9 heatmaps, P10 shortcut) are ruled M5 LIVING presentation (S8 §1) and proceed now. They carry no art and use a plain-text legend. Only object-tier art waits.
  3. Priced S8 §2 override: this request becomes the visual milestone, started now in parallel with finishing M5, with its §4.1 spec written now.
- **Audit recommendation:** Option 2, recorded as dated append-only notes in milestones.md and D-038 E1. Data overlays carry no art that would later be redrawn, which is the reason H7 gives for waiting. Composed sprites still benefit from M5 content settling first.

### Fog of war / discovered extent: pull D-040's computed known extent forward from M6, and on what terms (sight, scouts, three-state display, gating emergent colonisation)?

- **Parts:** 3, plus the 'discovered territory only' rule in part 4 and 'respect fog' in part 5
- **Ratified sources:** D-040 A4 (docs/d040-discovery-and-control.md:29-31), B1-B2 (:37-54), B5 (:82-88), D3 and rebase pointer (:158-164, M6); D-039 B1 (d039:50-55), B6 open (:77-82), C1-C4 (:86-100, M7); D-051 §5 'emergent founding continues unchanged' (d051:65-75); CLAUDE.md 'Never implement ahead of the ratified spec'
- **Why it needs the Director:** Discovery is owned by M6 on the frozen ladder, so building it now needs a priced override. The request's three-state overlay framing contradicts A4's literal 'not a fog-of-war overlay'. Giving scouts free sight contradicts D-039 B1 and decides the open B6 by implementation. Gating emergent colonisation by extent (D-040 B1) contradicts D-051 §5's 'unchanged'. Persisting extent moves every golden and breaks v32 saves.
- **Options:**
  1. Keep discovery in M6. Ship only the all-known PlayerKnowledge seam and leak hardening (P7) now; discovery state and fog follow the M6 spec.
  2. Priced override pulling in a minimal D-040 extent. It is a per-polity, monotone bitset over TraversalLattice nodes (schema v33). Sight comes only from controlled catchments and owned formations, with no scout bonus until D-039 B6 / M7. The three states are a presentation of stored extent plus derived visibility, recorded as a dated note on A4. It gates Found Settlement, road routes and (per M6/T4.6) trade. Emergent siting stays unchanged and is recorded as a known D-040 B1 deviation. Unit GHOST display is deferred to M7.
  3. As option 2, and also gate emergent colonisation by the source's known extent, reconciling D-040 B1 over D-051 §5's 'unchanged'. This accepts further corridor and golden movement.
- **Audit recommendation:** Option 2. The override ADR prices the golden move and the v32 save break, and the packet sequence is S0 then S2a/S2b/S2c. Reject a UI-only overlay drawn over a globally known simulation: A4/B1 forbid exactly that.

### Unit movement, Action Points and the move order: M7 scope with two OPEN frozen-item conflicts

- **Parts:** 2
- **Ratified sources:** docs/milestones.md:38-43 (military realization is M7); ADR-033 D7 (adr-033:151-158, scheduling ESCALATED); D-051 'Not changed: the Battle Layer deferral (M7)' (d051:106-109); CR-017 §3 (cr-017:242-320) and §5 (:389-458), both OPEN; ADR-031 §2.3 and SystemCatalog.cs:386-398 (AgeTransitionSystem is the sole writer of MilitaryUnits); D-039 E3 (no 'movement game'); era-pacing.json (dt from 10 to 0.5 years)
- **Why it needs the Director:** The ladder order is frozen, so pulling movement forward needs a priced override. Two CRs on frozen items are open: Law 3 (per-turn budgets) and D-009 (armies path on the network graph). Agents may not choose CR options. Movement also requires re-splitting ownership of a kernel table.
- **Options:**
  1. Keep movement in M7. No move order and no AP in this workstream. The read-only route/cost query (S1a) ships now for Found Settlement and is reused at M7. Unit card text stays as it is.
  2. Priced override for strategic movement of existing formations now. Rule CR-017 §3 Option B (Action Capacity is a per-turn decision budget, exempt from Law 3) and §5 Option A (the network is fast lanes; off-road movement samples the terrain lattice). An ADR re-splits MilitaryUnits ownership. ZOC, combat stats and naval/air movement are excluded, and each exclusion is recorded.
  3. Rule CR-017 §5 only and ship a move order without AP: one destination per turn, legal when the unit is owned and a land path exists. AP waits for the §3 ruling / M7.
- **Audit recommendation:** Option 1. Every empire has exactly one Warband, nothing recruits more (unit-families.json formationsPerPolity 1), and 10-year early turns make any distance-per-turn budget meaningless. M7 must also consume M6 capability state. Movement now buys little play and pre-empts two open CRs. If the Director wants movement anyway, choose Option 2, not 3: an AP-less order would have to be rewritten once §3 is ruled.

### What 'one representative character per unit type with a count label' means against the anatomy fence and the one-agent-one-token rule

- **Parts:** 2, 7
- **Ratified sources:** D-038 C1-C3 (docs/d038-visual-target.md:27-35: 'not now, not later'); D-043 B1 'Do not finalize visual representations yet' (d043:384) and Part E (:771); director ledger §24 (director-architecture-and-decision-ledger.md:1060-1081: one reported agent -> one visual token) and §17.4 (:884-891); D-043 B5/F12 (formations observed unequally must not be merged)
- **Why it needs the Director:** The request can be read two ways. As a figure, it is forbidden by C1 unless C1 is superseded. As several formations shown as one token with a count, it is forbidden by §24. Only the Director can say which reading he meant, and whether either ratified rule yields.
- **Options:**
  1. One silhouette/emblem token per formation, keyed by its current Identity (provisional per B1). The count label shows the formation's internal composition, which does not exist until M7 recruitment, so no label is shown now. §24 is upheld.
  2. One stack token per (anchor, identity) showing ×N formations, with a stack picker. Amend §24 for co-located formations of one owner that are observed equally.
  3. A figure-style representative character, superseding D-038 C1.
- **Audit recommendation:** Option 1. Every empire has exactly one Warband, so a count label would always read 1. Revisit stacks at M7, when recruitment exists.

### Rendering institutions individually at close zoom, and 'districts densify', against D-043 D2, ledger §16, D-038 H2 and D-009

- **Parts:** 8
- **Ratified sources:** D-043 D2 (docs/d043-civilization-progression-ages-and-mobile-agents.md:668-697: 'Do not turn every simulation institution into an individually rendered building'); ledger §16 (:816-830); D-038 H2 (d038:96-99); D-009 (d009-d010-map-population-addendum.md:15: districts abstracted); D-038 H8 legibility limit OPEN (:201-205)
- **Why it needs the Director:** The request contradicts the letter of recent ratified rulings. H8, the open legibility limit, is assigned to the visual milestone.
- **Options:**
  1. Uphold D2: one aggregated visual per institution type, with data-defined count stages (1/10/20/30) and the number shown at high zoom. 'Densify' means the one composed sprite gains dwelling rings and parts.
  2. Amend D2/§16: individual institution parts inside the footprint at close zoom, in presentation-only stable slots (no coordinates in the simulation), aggregated at lower zoom.
  3. A hybrid that also answers H8: individual parts up to a legibility cap per settlement, and an aggregated stage beyond it.
- **Audit recommendation:** Option 1 now. Decide H8 (and with it Option 3) at the visual milestone once parts exist, as H8 itself directs.

### Age-specific terrain and architecture against the substrate invariance, D-038 H5 and ADR-033 D8

- **Parts:** 7
- **Ratified sources:** docs/style-bible-parchment.md §1 and the 2026-10-02 note (:21-22, :129-144: the substrate is not re-themed); D-038 B1 (substrate lit flat forever), H5 (no era gate); ADR-033 D8 (adr-033:160-172: the Age changes HOW; no theme change per researched node); age-and-world-ui.md §9.3 and the pin at WorldMapStateTests.cs:529; D-043 DD-13 HOLD (no generic Age effects) and DD-T8 (d043:1285, the Age-to-register binding, open)
- **Why it needs the Director:** Age-themed terrain contradicts the substrate invariance directly. Choosing architecture by Age or by knowledge contradicts H5 and D8 unless it is defined as a style register applied to what is actually built. What drives that register (Age, realized state or knowledge) is unruled.
- **Options:**
  1. Vary only HOW things are drawn: era inks, hand, ornament and type through EraTheme/MapInk (P13). Substrate and building forms stay unchanged.
  2. An object-layer era register, built at the visual milestone. Built parts are drawn in a register derived from realized state (structures, institutions, road classes, formation Identity) plus the controller's Age, never from the knowledge mask. Terrain gains object-layer relief, forest and field overlays; the substrate is untouched.
  3. Supersede the substrate invariance and re-bake the terrain washes per Age.
- **Audit recommendation:** Option 1 in M5, and Option 2 at the visual milestone. Reject Option 3: it rewrites the single-cartographer substrate rule without adding any content driven by state.

### D-051 §5 Found Settlement leaves its legality terms undefined: 'unclaimed', 'enough people and food', party size, and instant founding vs a travelling settler

- **Parts:** 4
- **Ratified sources:** docs/d051-m5-polish-rulings.md:65-75 (§5); docs/t4.4-review-record.md §8 (:302-314: no threshold, minimum population or TUNE on B1); ClaimRow is schema-only (WorldState.cs:580-591); ColonizationTests.cs:852-884 (unreachable land passes spacing); D-040 B1; D-051 §6 deferred (d051:100-103)
- **Why it needs the Director:** The ruling says the order is 'legal only under stated conditions' but states none. 'Unclaimed' has no land-level meaning in state. 'In the style of Civ VI settlers' could mean a travelling unit, which would make part 4 depend on movement (previous issue). These define a Director ruling; they are not implementation choices.
- **Options:**
  1. Instant founding through ColonizationSystem.Found. Unclaimed: the cell is not occupied, is spacing-legal, and its origin lattice node lies in no settlement's catchment. Reachable: Pathfinder.FindPath(...).Found. Enough: party ≥1, the source keeps ≥1 person in every non-empty bucket, and ProvisionsFor ≥1 grain. Party size is a content TUNE fraction of the source, floor-allocated per bucket. The 'discovered' check is inert until extent exists.
  2. As option 1, plus a minimum party and a years-of-food floor. These are new TUNE thresholds on the order path only; emergent B1 founding is untouched.
  3. The settler is a MobileAgent that travels to the site. This needs the movement ruling and a conserved carrier for people in transit.
- **Audit recommendation:** Option 1, recorded in ADR-036 and confirmed at S1b's handback. It adds no constant beyond the party fraction and keeps emergent founding byte-identical.

### Part 6 assumes research 'newly unlocks a resource', which has no counterpart in the simulation

- **Parts:** 6 (and 5)
- **Ratified sources:** Sim.Data/content/research.json (no resource entity kind; baseline sectors are never gated); ADR-033 D1 (activity identities are presentation only); ledger §23 (:1040-1056: effects.immediate stays empty); D-040 B3 (no tech unlock of reach); ResearchCompletedRow has no turn field (WorldState.cs:837)
- **Why it needs the Director:** Ores, stone and timber are extracted from turn 1. A real trigger would need a simulation gate that contradicts ratified records and moves every golden. A UI-only reading changes what the Director asked for.
- **Options:**
  1. UI-only. The shortcut fires on real eligibility transitions: activity.farming opens the food-potential layer, and a recipe whose inputs are deposit goods (e.g. bronze_casting) opens the copper/tin layer. It is labelled 'now usable' and expires after N turns, using session history that replay rebuilds.
  2. Add a content-declared research-to-good relation (data only), read only by the UI, with no simulation effect.
  3. Gate raw extraction on research in the simulation. This amends ADR-033 D1 and the baseline records and changes the economy from turn 1.
- **Audit recommendation:** Option 1.

### Base branch for the workstream: CR-020 rulings, approval to push m5p-d051-fixes, and the 'cut from main' convention

- **Parts:** all
- **Ratified sources:** docs/adr/cr-020-d051-tools-and-weather-vs-measured-behaviour.md (OPEN; §A recommends option 1 and to decide 2 vs 3 after the playtest; §B recommends option 1); docs/gov-4-repository-freshness.md §2, §5 (LOCAL/REMOTE/MAIN are distinct; no push or merge without instruction); docs/gov-3-execution-protocol.md B6 (stop on reds); CLAUDE.md branch convention (cut from main; main is v25 and pre-M5); docs/current-state.md:3-19
- **Why it needs the Director:** 82bde01 exists only locally and carries two RED frozen dev-corridor teeth that wait on CR-020. Pushing it needs approval. The 'cut from main' convention cannot hold, because main lacks every M5 system the workstream reuses.
- **Options:**
  1. Rule CR-020 §A and §B as recommended (Option 1 each), approve pushing m5p-d051-fixes, and name 82bde01 as the base of m5w-integration. The two reds are recorded as ruled and inherited.
  2. Defer CR-020 and name 2dc6af2 (pushed, CI green) as the base. Simulation packets that move goldens wait and are re-pinned after the ruling.
  3. Rule CR-020 §B so that the old goldens are restored (time-averaged weather off by default) on a new tip, and use that tip as the base.
- **Audit recommendation:** Option 1. Until that ruling arrives, presentation-only packets start from 2dc6af2 under the art-substrate precedent in CLAUDE.md's milestone line (the non-UI diff is provably empty; rebase when it merges). Simulation packets are developed locally on 82bde01 and are not pushed until approval.

## Completeness critic — open items carried into the packets

- **Terrain obstruction and vantage (D-039 B4) for sight** — Add a governance option or an S2a dataGap: sight is not movement cost. Either define a vantage term from the Elevation raster, which needs a ruling, or record explicitly that the isochrone proxy inverts B4 and must not ship as 'terrain obstruction'.
- **Scout advantage: no director option, and the plan contradicts itself** — Add an explicit scout option to the fog governance issue. Also list passive settlement and formation sight as its own named deviation from D-040 B2 / D-039 B1, or drop formation sight.
- **Fog leak through Trends 'world' scope and HistoryBuffer, and through PlotLines** — Add HistoryBuffer/TrendsModel world scope (and MarketModel/NeedsPanelModel for visible foreign settlements) to P7's reader list. Add a metamorphic test on the series values, not only on text.
- **FrameText completeness guard** — Add a source-guard test to P6: no direct ImGui text or widget call outside the recording helpers, or an allowlist with reasons. Add a semantic mutant that bypasses the recorder.
- **Windows artifact / windows-smoke as an acceptance criterion** — Add 'windows-smoke green on a pushed ref (claude/** or dispatch)' to the acceptance of every UI-touching packet. Make push approval an explicit W0b/Director item.
- **Cross-process and cross-platform determinism for later simulation packets** — Add a ci.yml determinism-xproc leg to S2b, S3a and S3b. Add a CR-013/xplat note and a dispatch run of xplat-surveillance to S2a's acceptance.
- **D-051 §2 (resource availability classes) has no packet and no sequencing** — Either add a D-051 §2 simulation packet, which moves goldens, ahead of P9, or record a Director sequencing decision in W0b.
- **Unblocked AI information-class work parked in blocked S2b** — Move the reorder and the own-polity metamorphic AI test into an unblocked packet (S1c or P7).
- **Farmland consumption / organic footprint (frozen D-009:15)** — Add a governance item: assign an owner to farmland consumption, or rule that the footprint may be drawn without it.
- **Per-packet milestone/governing record, and S8 §4.1 items** — Add milestone and governing-record fields to each packet. Extend W0 with a dimensional declaration (Law 3 for sight and extent under dt 10→0.5) and a corridor-independence statement.
- **'Much larger viewport' (part 1) not delivered** — Add a fit-to-width or collapsible-chrome option to P1, with deliberate re-pins, or record that the request is re-scoped.
- **Gate states for the new scope and the gate's runtime budget** — Add large-window and per-Age gate states to P1 and P13, and state the gate-runtime impact.
- **Re-verification after rebase** — Add a post-rebase re-verification step (suite, gate, goldens) to each early P-packet's handback.
- **D-051 §3 forecast vs fog** — Add one line to W0's coupling map or a queue entry.

- Correction: S2a: KnownExtent is 'seeded at founding from each polity's CatchmentNodes', with 'founding-seeded rows stripped' in the V33 attribution controls. → At turn 0 the extent is empty unless founding computes it (a new founding write). Redesign the seed, for example DiscoverySystem's first step or an explicit founding isochrone, and restate the golden-attribution plan.
- Correction: S1b test: 'OrderLog load-validation theory: non-whole, negative, NaN and ≥Size² cells'. → Check ≥Size² in ValidateAgainstWorld (or at delivery). Keep only whole-number, non-negative and NaN in ValidateRecord.
- Correction: Presentation packets may start from 2dc6af2 'following CLAUDE.md's art-substrate precedent'. → Present it as a request for a Director ruling, not as an existing precedent that licenses the work.
- Correction: P3 (in-game legend) and P12a (population-primary size classes, extra dwelling rings) are presentation-only and unblocked. → Mark P3's legend and P12a's size-tier half as blocked on the visual-milestone ruling, or name them explicitly in governance option 2. Cite E3 and the E4 rebase annotation ('the reasoning applies to M7') in that issue.
- Correction: 'The two candidates differ in Sim.Ui by one file only: ActionSurfaceModel.cs.' → State it as three UI-side files, including the two test files. P8 and P11 touch InfoPanelTests and will conflict on rebase.
- Correction: W0 (a) records 'the base and naming used by the workstream' in current-state.md now. → Limit W0's current-state note to measured facts (2dc6af2 REMOTE with CI green; 82bde01 LOCAL). Record the base only after the ruling (W0b).
- Correction: P9 acceptance: 'The field at each founded site equals that site's DepositRow for goods with spread 0' for rasters 'computed at lattice-node granularity (256²)'. → Evaluate the exact-equality check through the S1a function at the site cell. Label the node raster as an approximation.
- Correction: P7: extend check-read-isolation.sh as the mechanical leak gate for raw Sim.Ui reads of Settlements, MilitaryUnits, Buckets and Deposits. → Do not rely on it as the leak proof. Make the metamorphic tests the gate and treat the grep as advisory, or write a dedicated Roslyn or source-guard test.
- Correction: S1c is blockedOnDirector=false while it re-pins AI goldens on 82bde01. → Mark S1c as blocked on the CR-020 ruling plus explicit golden-move authorization.
- Correction: 'The DD-13 hold forbids generic Age effects.' → Call it an unruled deferral, not a prohibition.
- Correction: S3c depends on S2c (fog presentation). → Make the 'preview never reveals unknown nodes' criterion conditional on S2a, and drop the hard dependency.

## 2026-10-11 — status

_Appended 2026-10-11 by packet W0 (branch `m5w-w0-docs`, cut from `m5w-integration` @ `6b624d45`; LOCAL, not pushed,
not merged). Everything above this heading is the 2026-10-10 audit and plan, kept verbatim. Every fact below was
measured by the W0 agent, and the git or `gh` command, or the file and line, is named beside it. Nothing here is a Director ruling, and no packet listed as
delivered has had an acceptance ruling recorded._

### Repository state, measured 2026-10-11

LOCAL, REMOTE and MAIN, kept apart for every ref named (GOV-4 §2):

| Ref | Commit | LOCAL | REMOTE | MAIN |
|---|---|---|---|---|
| `main` | `93270cd` | yes | yes: `git ls-remote origin` gives `refs/heads/main` = `93270cd` | is main |
| `m5p-integration`; on the remote, `claude/new-session-yavghm` | `2dc6af2` | yes | yes: `git ls-remote origin` gives `refs/heads/claude/new-session-yavghm` = `2dc6af27…` | no: `git merge-base --is-ancestor 2dc6af2 93270cd` fails |
| `m5p-d051-fixes` | `82bde01` | yes | no | no |
| `m5w-integration` | `6b624d45` | yes | no | no |
| every `m5w-*` packet branch below | (table) | yes | no | no |

How "REMOTE: no" was measured:
- `git ls-remote origin` lists no `m5w-*` ref, and no ref points at `82bde01` or `6b624d45`.
- Every remote head is present locally. `git merge-base --is-ancestor` of `82bde01`, and of `6b624d45`, against each
  remote head fails for all of them, so no remote branch contains either commit.

How "MAIN: no" was measured: `git merge-base --is-ancestor <commit> 93270cd` fails for `2dc6af2`, `82bde01` and
`6b624d45`.

CI on `2dc6af2` (measured with `gh run view` and `gh api repos/…/actions/runs/38032099661/artifacts`):
- **Run `38032099613`**, workflow CI, event push, branch `claude/new-session-yavghm`, head `2dc6af27…`: conclusion
  success. The build-and-test, determinism, determinism-xproc and calibration jobs succeeded; calibration-nightly was
  skipped.
- **Run `38032099661`**, workflow ui-artifact, same head: conclusion success. The ui-artifact and windows-smoke jobs
  succeeded. The artifact `sim-ui-win-x64-2dc6af2` is not expired.

None of `82bde01` or the commits after it is on the remote, so no CI can have run on any of them.

`m5w-integration` was cut from `82bde01`: `git merge-base m5w-integration 82bde01` is `82bde01`, and
`git rev-list --count 82bde01..6b624d45` is 103. Which commit the workstream should be based on is still an OPEN
Director ruling ("Base branch for the workstream", above). That the tree is built on `82bde01` is a fact about the tree,
not a ruling.

### Packets delivered on `m5w-integration` @ `6b624d45`

For every branch below:
- its head is an ancestor of `m5w-integration` (`git merge-base --is-ancestor <branch> m5w-integration`);
- its merge base with `82bde01` is `82bde01`;
- it is LOCAL only: not pushed, and not merged to main.

| Packet | Branch | Head | Entered `m5w-integration` through |
|---|---|---|---|
| S1a | `m5w-s1a-siting-read-seams` | `b8d2108` | merge `a7eea07` |
| S0 | `m5w-s0-resume-hardening` | `6e9d05e` | merge `5e62a5f` |
| P4 | `m5w-p4-map-frame-cost` | `3ca52da` | merge `3fdbfb7` |
| P13 | `m5w-p13-era-map-hand-tokens` | `bf5f3bb` | merge `cd7b6cc` |
| P6 | `m5w-p6-headless-observability` | `5a44eaa` | merge `8536661` |
| S1b | `m5w-s1b-found-settlement-order` | `3d8aacc` | merge `6b5d90d` (S1b is an ancestor of S1c) |
| S1c | `m5w-s1c-ai-founding-policy` | `9ff6452` | merge `6b5d90d` |
| P1 | `m5w-p1-map-viewport` | `1b54ce6` | merge `6f269bc` (P1 and P3 are ancestors of P2) |
| P3 | `m5w-p3-lens-hit-geometry-legend` | `9e066b2` | merge `6f269bc` |
| P2 | `m5w-p2-eased-zoom-continuous-lod` | `7e4464f` | merge `6f269bc` |
| P7 | `m5w-p7-player-knowledge-seam` | `c58d9b0` | merges `e6facc6`, `f64714c` and `54ea6a3` (P7 is an ancestor of P10, S1d and P11) |
| P8 | `m5w-p8-site-inspection` | `efef5da` | merges `e6facc6` and `f64714c` (P8 is an ancestor of P10 and S1d) |
| P9 | `m5w-p9-resource-overlays` | `c0dc157` | merge `e6facc6` (P9 is an ancestor of P10) |
| P10 | `m5w-p10-discovery-shortcut` | `888eda3` | merge `e6facc6` |
| S1d | `m5w-s1d-found-settlement-ui` | `50e94ce` | merge `f64714c` |
| P11 | `m5w-p11-unit-token-detail` | `54cb603` | merge `54ea6a3` |
| P12a | `m5w-p12a-settlement-placement-stability` | `f495bcb` | merge `1f548c5` |

Each merge's second parent is the branch head shown (`git log -1 --format=%p <merge>`).

Six commits on the first-parent line of `m5w-integration` come from no packet branch (`git log --first-parent
82bde01..6b624d45`, excluding merges):
- `55640ae`, `b49ae20`, `d452b7e`, `a96c7ca` and `150cab3` are integration tests;
- `6b624d4` is a `docs/queue.md` line: FOUND, NOT FIXED. With five or six overlay layers shown at 1280×800, the overlay's
  key is taller than the clear map.

**In progress, not delivered:** `m5w-p9b-overlay-key-fit` exists at `6b624d4`, with no commit of its own
(`git rev-list --count m5w-integration..m5w-p9b-overlay-key-fit` is 0). P9b is not in the plan's table above.

**This packet, W0**, is on `m5w-w0-docs` and is not on `m5w-integration`. It adds:
- this document;
- this section;
- a dated entry in `docs/current-state.md`;
- a dated note correcting `docs/architecture/age-and-world-ui.md` §9.4.

It does not carry these items of the plan's W0:
- (b)'s second and third stale-statement notes (b1z2y4; `ColonizationTests.cs:864-866`);
- (c), the coupling map and the order-kind and schema reservations;
- (d), the queue lines.

For (a), the `current-state.md` entry follows the completeness critic's correction (line 1000): it records only
measured facts and names no base.

### Packets blocked on Director rulings

None of these packets has a branch (`git for-each-ref refs/heads/m5w-*`). No Director ruling for this workstream is
recorded anywhere: there is no D-052 and no `docs/d05[2-9]*` file. The table names each ruling by the heading of its
governance issue above.

The Wave column's "after ruling N" numbers in the plan's table are not defined anywhere in this document. They do not
follow the order of the governance section, so they are not used here.

| Packet | Awaits |
|---|---|
| W0b | The Director's rulings themselves, which W0b records verbatim as D-052, with any priced S8 §2 override ADR and the CR-017 §3/§5 rulings if given. That means every governance issue above, plus two critic items: push approval ("Windows artifact / windows-smoke") and the D-051 §2 sequencing decision. |
| P5 | A ruling on whether render-only terrain detail is M5 work or waits for the visual milestone. The plan does not record this hold. It lists "render-only detail-on-zoom" under "can proceed now" (line 113), and P5's row names no ruling. The workflow holds P5 with the packets that await rulings. The nearest governance issue is "Does D-038 E1's inserted visual milestone still exist…", but that issue covers parts 5, 7, 8 and 2, while P5 is part 1. None of its options names P5. Option 1 keeps P12a, P13 and P8 in M5, and option 2 adds P9 and P10. |
| P12b | Three rulings: <br>• the visual-milestone issue (composed sprites, count staging and organic footprint wait under options 1 and 2); <br>• "Rendering institutions individually at close zoom…" (D-043 D2 and the open D-038 H8 limit, which P12b's acceptance applies "as ruled"); <br>• the critic's "Farmland consumption / organic footprint (frozen D-009:15)" item. |
| P14 | Three rulings: <br>• "Age-specific terrain and architecture…" (P14 is that issue's option 2, at the visual milestone); <br>• the visual-milestone issue; <br>• for its unit silhouettes, "What 'one representative character per unit type…' means…". |
| S2a | "Fog of war / discovered extent…": a priced S8 §2 override that pulls D-040's extent forward from M6 (schema v33). With it go the critic's sight items, vantage (D-039 B4) and scouts (D-039 B6, open). Because a schema bump moves every golden, it also waits on "Base branch for the workstream…" (CR-020, OPEN). |
| S2b | The fog issue, specifically option 2 versus option 3: whether emergent colonisation is gated by extent (D-040 B1 against D-051 §5's "unchanged"). Two further questions are also open: <br>• which world a fogged issuer's Found Settlement order is judged against (`docs/queue.md`, the S1d line; ADR-036 §8 item (h)); <br>• trade's mutually-known term, "if ruled". <br>S2b also needs S2a. |
| S2c | The fog issue: the three-state display against D-040 A4's "not a fog-of-war overlay", with GHOST deferred to M7. S2c also needs S2a. |
| S3a | "Unit movement, Action Points and the move order…" (keep movement in M7, or a priced override), together with CR-017 §5 (off-road movement against D-009, OPEN). The MilitaryUnits ownership re-split rides on that ruling. |
| S3b | The movement issue, and CR-017 §3 (Action Capacity against Law 3, OPEN). |
| S3c | The movement issue, through S3a and S3b. |

### Open rulings that delivered packets already rest on

The delivered packets proceeded on these. None is ruled.
- **ADR-036 is PROPOSED** (`docs/adr/adr-036-found-settlement-order.md:3`). Its §2–§3 legality terms are "the defaults
  the Director was told the workstream would use unless he objects; they await his confirmation". This covers the
  issue "D-051 §5 Found Settlement leaves its legality terms undefined…" and rests under S1b, S1c and S1d.
- **ADR-036 §8, "For the Director"** (`:359`), lists three items:
  - (g) the AI expansion rule and its values, 350 years and 500 people;
  - (h) the known leak;
  - (i) the reading of `a4_expansion`.
- **S1c re-pinned the AI pins with cause** (ADR-036 §8.1, `AiEmpireIntegrationTests`). The critic's correction (line 1003)
  asked that S1c be marked blocked on the CR-020 ruling plus an explicit golden-move authorization. Neither is recorded.
- **ADR-037 is PROPOSED** (`docs/adr/adr-037-player-knowledge-seam.md:3`). P7 rests on it, and so does every later
  packet that reads through the knowledge seam.
- **P10 follows "the Director's default (workstream part 6, the audit's Option 1 as told)"**
  (`docs/m5-playability-gate.md:1166`). No ruling is recorded.
- **P11 follows "the DEFAULT the Director was given for the unit-representation issue (no objection yet…)"**
  (`docs/m5-playability-gate.md:1441-1442`). No ruling is recorded.
- **The visual-milestone issue is unruled.** P9 and P10 are the read-only overlays that its option 2 would rule M5
  LIVING presentation. The critic's correction (line 998) asked for P3's legend and P12a's size-tier half to be marked
  blocked on this ruling, or named in option 2. All four packets are delivered.
- **"Base branch for the workstream…" is unruled.**
  - CR-020 is OPEN (`docs/adr/cr-020-d051-tools-and-weather-vs-measured-behaviour.md:3`).
  - Pushing `m5p-d051-fixes` is not approved.
  - The "cut from main" convention is not reconciled.
  - The critic's correction (line 997) applies: starting presentation packets from `2dc6af2` under CLAUDE.md's
    art-substrate precedent is a request for a ruling, not a licence. In fact, every packet branch above descends
    from `82bde01`.
- **CR-017 is OPEN** (`docs/adr/cr-017-ages-surge-and-mobile-agents-vs-frozen-items.md:3`). P11 shows no Move control
  and no right-click order, pending its ruling.

### Inherited reds (CR-020), measured on this packet's tree

**The test run.** The tests ran on this branch's tree, whose code is `6b624d45`'s (the tree differs from it in `docs/`
only), on 2026-10-11. The commands were `dotnet build Sim.slnx -c Release`, then
`dotnet test Sim.Tests/Sim.Tests.csproj -c Release --no-build`.

**Result: 1,699 passed, 2 failed, 13 skipped** (1,714 tests, 41 m 37 s). The two failures are:
- `CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand(seed: 7)`: "Value not in range, Range: (71000 - 119000),
  Actual: 124455".
- `CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand(seed: 42)`: "seed 42: 8.71773E-05 fell below 0.75× its
  recorded value 0.000248624 …".

These are exactly the values CR-020 records (`docs/adr/cr-020-d051-tools-and-weather-vs-measured-behaviour.md:61-62`).
They stay RED until CR-020 §B is ruled; it is OPEN. No other test failed.

**Other checks on the same tree:**
- **Sim.Ui.Tests:** 830 passed out of 830 (`dotnet test Sim.Ui.Tests/Sim.Ui.Tests.csproj -c Release --no-build`, 12 m 35 s).
- **Smoke run** (`dotnet Sim.Ui/bin/Release/net10.0/Sim.Ui.dll --smoke --monkey 200`): SMOKE PASSED. It covered 12 states
  and 2,884 checks: 2,735 passed, 149 were not offered, and 0 failed. The monkey made 2,400 actions.
- **Gate scripts:** `scripts/check-banned-constructs.sh`, `check-read-isolation.sh` and `check-readonly-proof.sh` all
  report OK.
- **Research checks:** the three research `--check` scripts that ci.yml runs report the docs current.
