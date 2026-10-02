# The action surface — what the civilization can do now (ADR-033 D1/D2, stream U2a)

Branch `m5i-u2a-actions`, cut from `m5-integration` @ `5e7fa35`. UI only (`Sim.Ui/**`, `Sim.Ui.Tests/**`,
`docs/`); the simulation is read through its queries and changed only by orders.

The POLICY section is the ACTION SURFACE. It replaces the five static percentage sliders and "Apply labour
split" (the M3/M4 dashboard): the player sees what the civilization can actually do now, in the hand of its
Age, and more appears only when the authoritative query lists more.

## 1. Derivation — the query decides WHAT

```
ActionSurfaceModel = ActionSurface.Build(world, previousWorld, cfg, eraTable, player, selection, queuedOrders, names, theme)
  = AvailableActionsQuery.For(world, cfg, player, ActionQueryContext.ForNextStep(world, era, queued))   // once
  + LabourActivities.For(world, cfg, player)                                                           // once
  + the owning domains' public readers for what a descriptor names
```

Every block exists iff the query returned that domain's descriptors (labour and construction: for the
selected settlement, or the capital when the selection is not the player's to command). The UI decides no
availability: a locked future action is never listed, and no future control is ever shown disabled. An
action that is legal but not affordable is listed with its blocker ("not yet: needs 40 timber (has 0)").

| block | from | shows |
|---|---|---|
| Work | `labour.*` descriptors + `LabourActivities` | one entry per sector, labelled with every identity it expresses (Gathering → Farming when a crop is known; "Gathering wood & stone · Logging"), what it makes, the running and the queued split, the Empire summary, and on the turn research changed it a "new: Farming (Root and tuber cultivation)" marker |
| Learning | `research.set-target` / `research.clear-target` | the target, progress, RP per turn, retained progress, "idle — choose", a choice queued this turn, Stop (clear), the trees link |
| A new Age | `age.advance.*` (eligible, not ordered) | the advance, which opens the capital's Age panel and its unchanged ADVANCE AGE flow |
| Building | `construction.*` for the settlement | projects with materials, labour and blockers; the queue and its head's blocker; this turn's builders (pool, housing's draw, what is left for a project and path-making) |
| Roads | `roads.develop.*` | the class, ranked routes with `RoadDevelopmentQuery.Describe`'s performance, a plan preview (routes, cost, affordability, payers nearest first) labelled as an ESTIMATE on current stocks — transport is frozen, read only |
| Governance | `governance.tax-edict` | the levy control, the declared levy, legitimacy, per-settlement reach and collection |
| Arms | `military.basic-fighting` (Standing) | the roster card: formation, family line, Age identity, modernization at the next Age — information only (no military order exists, ADR-033 D7) |
| On their own | `standing.*` | one compact list, no controls |
| This turn | the previous world vs this one | research learned, identities changed, domains that appeared |

## 2. The era decides HOW

| `Controls.Granularity` | Ages | labour / levy / road control | numerals |
|---|---|---|---|
| Coarse | A1 | ten laid pebbles (10 points each) | none |
| Simple | A2–A3 | ten clay counters | none |
| Standard | A4–A6 | a notched rule of twenty (5 points each) | yes |
| Fine | A7–A8 | a slider in whole points, − / + | yes |
| Precise | A9 | a precise slider with a graduated rule | yes |

`Density.Level` sets the layout: a short flat list at 1 (A1), domain groups with headers at 2–3, denser
detail at 4–5 (provenance on every entry, every route and collection line). The control is chosen from the
token, never from the era number. A split always places exactly its units (largest remainder, lowest sector
on a tie) and is emitted as the same SectorAllocation batch (`SectorOrderFactory`), units × points each.

## 3. Orders — one dispatch, the simulation's own predicates

`ActionSurfaceScreen.Click` answers with an `ActionCommand`; `ActionDispatch.Apply` sends order commands to
the session's guarded emitters, each refusing what the simulation would refuse (ADR-033 D2):

| command | emitter | builder | guard (the system's predicate) |
|---|---|---|---|
| apply labour | `EmitSectorOrders` | `SectorOrderFactory` | `LabourActivities.CanAllocate` |
| build | `EmitConstructionOrder` | `ConstructionQuery.EnqueueOrder` | `ConstructionQuery.IsProjectAvailable` |
| develop roads | `EmitRoadOrder` | `RoadDevelopmentQuery.DevelopOrder` | the Plan is non-empty, (0, 100], first this turn |
| declare a levy | `EmitTaxOrder` | `Governance.TaxOrder` | `Governance.CanLevyTax` |
| stop research | `ClearResearchTarget` | `ResearchQuery.ClearTargetOrder` | something to clear |
| choose research | (the trees) `EmitResearchOrder` | `ResearchQuery.TargetOrder` | `ResearchQuery.IsAvailable` |
| advance | (the Age panel) `EmitAdvanceAge` | `AgeQuery.AdvanceOrder` | `AgeQuery.CheckAdvance` |

Every order is stamped with the current turn and applies at the next End Turn; a choice made this turn is
shown as queued ("Set: … takes effect at End Turn", "chosen this turn …", "Declared this turn …").

## 4. Around the surface

- The status band shows the research figure (target, % and RP a turn, or "research idle [K]"; it is the
  band's way into the trees) and a compact Age indicator with the full Age name ("Age I Prehistoric / Stone
  Age - not yet") that opens the capital's Age panel on demand. The panel no longer opens over the map when
  the capital is selected.
- End Turn announces the surface's notices in a short toast (research learned, "new: Farming …", "new: the
  tax edict").
- The tax burden is rendered where happiness is explained: an "x tax burden" row in the Grievance tab (declared
  rate × the stored reach; its lever opens POLICY) and a "tax:" line in the settlement overview, once the
  controller has legislated.
- The labour record (declared vs effective, the history of changes; T4.19 B5) stays under the surface, folded.

## 5. Previews

`docs/architecture/action-surface-preview/render-previews.sh` runs `sim-ui --action-preview` and screenshots the
SVGs with headless Chromium: turn 1 on the canonical founded world (seed 42, 12 settlements, A1), and a later
rig — the same world with a crop, a taxation node and a road class known (ResearchCompleted rows, the
constructed part) and the Age row at III, then played through the session (Root and tuber cultivation
researched to completion, a 20 % levy, a granary queued) — at A3 and, with only the Age row changed, at A8.
Each state is painted as the game screen with POLICY open and as the panel alone at full height, by the same
`ActionSurfaceScreen` the game runs; `preview-log.txt` records the SVG hashes (byte-identical across runs).
The rig's Age row is set directly, so its warband was never modernized (no transition ran).

## 6. Limits and open items

- The live ImGui path (the surface replayed into the POLICY child window, the band buttons) could not be shown
  in this container (no GL context); its DrawList is the one the previews show.
- `baseline.primitive_water_transport` (shore-hugging craft) is `simulated: false` in research.json, so the
  query does not list it and the surface does not show it.
- `OrderValidation.ValidateAgainstWorld` checks every order against the turn-0 world (`OrderValidation.cs`,
  the settlement-existence and control checks), so a labour or build order for a settlement founded later (a
  colony) would make replay validation throw although the live step applies it. Read from the code, not
  reproduced in play (no colony is founded in the reference runs); a Sim.Core matter, reported.
- ADR-033 D10 (a finished project's labour is also banked by path-making) is measured but open; the Building
  block says so at the detailed density.
