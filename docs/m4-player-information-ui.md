# T4.19 LANE B — THE PLAYER INFORMATION UI

Branch `t4.19-lane-ui`, cut from `t4.19-glass-box` at `2a436f1` (lanes A1, A2,
C, D, E merged). **Not merged to main.** Every change is in `Sim.Ui`,
`Sim.Ui.Tests` and this file; `git diff --stat` against `2a436f1` touches no
simulation project, so no constant, equation, schema, golden, corridor or
quarantine could have moved. Where the UI needed a read the core lanes did not
provide it was built in `Sim.Ui/ViewModel` over the public records (stated per
item below).

The contract is `docs/observability-architecture.md`. The UI READS
(`IObservationHistory` on `UiSession.Observations` — one seam, no second log),
the explain queries (`GrievanceExplanation.For`, `CausalChain.ForNeed`,
`Levers.For`, `HappinessExplanation.For`, `MigrationExplanation.For`), and the
two worlds those queries take. Nothing here computes a simulation quantity;
where a query says GAP the panel prints "not recorded" and never a number.

---

## §1 THE SCREEN — BEFORE AND AFTER

### Before (T4.18)

```
status band: turn · year · world pop (N settlements)
selection card: name · pop · food                              268×104
command bar: [End Turn] [POLICY][ECONOMY][POPULATION][MARKET][ANNALS][TRENDS][MORE]  ☑ territory
one context panel, 396 px, right column
```

### After (T4.19 lane B)

```
status band: turn · year · [world pop N] (N settlements) · [food F] · last turn: pop -289 · food -930 · 2 in deficit
selection card: name · [pop …] · [food …] · [happiness …] [grievance …]      268×132
command bar: [End Turn] [TURN][SETTLEMENT][POLICY][ECONOMY][ANNALS][TRENDS][MORE]  ☑ territory
one context panel, 396 px, right column — unchanged rect (ChromeGeometry, lane D)
keys: 1..7 open the sections in that order · Escape closes the panel (exits with none open, as before)
```

`[…]` marks a clickable figure (a Selectable sized to its own text). The T4.18
architecture stands: world, status band, selection card, command bar, ONE
contextual panel; `ChromeGeometry` owns every chrome rect and nothing was
re-derived. The default screen is exactly as uncluttered as T4.18 left it —
measured, not asserted:

| | T4.18 | lane B |
| --- | --- | --- |
| clear map with nothing open | 890,880 px = **87.0 %** of 1,024,000 | **87.0 %** (unchanged: the bars are unchanged) |
| map with a section open | 606,912 px = 59.3 % | 59.3 % (the context rect is unchanged) |
| selection card over the map band | 268×104 = 3.1 % | 268×132 = **4.0 %** (< 10 %, pinned) |

The one geometry change: the selection card grew 104 → 132 px to carry a
third data line (happiness · grievance). WHY 132: the card's lines are the
17 px numeric face at 7 px item spacing under a 19 px title at 12 px padding —
three data lines end at y ≈ 110 inside the window, past 104 — so the old height
would have clipped the two figures the packet makes clickable. `PanelLayoutTests`
and `ChromeGeometryTests` stay green (the selection card's rule formula is
verbatim; the card's fraction of the map band is pinned under a tenth).

---

## §2 THE SECTION / TAB MAP — what each shows, from which record

| section / tab | shows | from |
| --- | --- | --- |
| **TURN** (1) — B3 | header (turn, year, dt, orders applied); **WHAT CHANGED**: population (births / natural deaths / starvation), grain (harvest / eaten / spoilage / overflow, endowment when non-zero), dwellings (built / decayed), migrants moved (per-settlement in/out beneath), settlements founded (the founding records beneath), control lost, trade units (flow count, the unattributed-transfer flag) — each line a Selectable that unfolds its account; **WHERE**: the settlements with the largest \|Δpopulation\| and the largest deficit, each row a click that selects AND centres; **WHY**: the CausesRecord identities verbatim and the Reconciles flag as "reconciles to the unit" / "DISCREPANCY n - simulation defect" | latest `TurnRecord` (§2) + its `SettlementRecord[]` (§3) — `TurnAuditModel` |
| **SETTLEMENT** (2) — B4, tabs are a button row inside the panel, one open, UI-only | | |
| · Overview | the live card figures, then the record headline: population (Δ), grain (Δ, deficit), happiness with its two factors, dwellings + sufficiency, grievance per class, "founded THIS turn" when so | `SettlementRecord` — `SettlementInspectorModel.OverviewLines` |
| · Population | opening → closing, cohorts, births, deaths (labelled UNSPLIT, §8 gap 1), inflow / outflow, colonists departed (residual) WITH its identity string, class counts; then the T3.9a per-class needs block (the former POPULATION section, unchanged) | `PopulationSection` + `NeedsPanelModel` |
| · Food | `grain open + harvest − eaten − store losses = close` WITH the identity string, demand / obtained / deficit, per food good produced / demand / eaten, dwellings line, built-vs-decayed as "not recorded: …" | `FoodSection`, `HousingSection` |
| · Economy | the record's shares in force (flagged "default, never ordered"), variables, class latches, trade legs; then the former MARKET section unchanged: goods / prices / last move rows, the PriceTerms decomposition, the price series | `EconomySection` + `MarketModel` + `HistoryBuffer.Price` |
| · Grievance — **the centre** (A6–A9, B6) | happiness line (click → the two factors, each click → its chain + lever; the ScopeNote saying happiness omits Comfort and the gate); the AttributionNote; per class present: total, Δ this turn, accrual vs decay with "reproduces exactly"; **PRIMARY first** with its MarginalLift; every bound need as a contributor row (satisfaction, weighted shortfall, marginal lift, gate flag); unbound needs under "not yet simulated"; click a contributor → its CausalChain, one link per line "label  value  (kind, source)", GAP links "label: not recorded: note"; at the bottom the lever — the sectors that reach it with an **open POLICY** button, or the honest None reason | `HappinessExplanation.For(next)`, `GrievanceExplanation.For(prev, next)` per class with members, `CausalChain.ForNeed`, `Levers.For` — `GrievanceViewModel`, composed in `ScreenModels.Grievance` |
| · Migration | inflow / outflow; push (prev deficit), pull (prev smoothed attractiveness), food-gate inputs, unplaced; self as destination; **others sorted (attractiveness DESC, id ASC)** with deficit / grain presence / happiness; the push reading; pairwise flows, damping, viability products, gap scale each as "not recorded: …" | `MigrationSection` + `MigrationExplanation.For(prev, next)` |
| · Orders | orders applied this step targeting this settlement: index / actor / kind / sector / amount | `SettlementRecord.Orders` |
| **POLICY** (3) — B5, C3 | the policy list (one entry: labour allocation; M5's taxation is the second in the same shape); the T4.18 fixed-sum sliders unchanged; **CURRENT**: the running bars plus declared vs effective side by side per sector; **HISTORY**: `PolicyChange` newest first — "turn 24 · farming 55% -> 73% · player · order #104" — and under each the settlement's record headline (pop, food, deficit, happiness) on the following turns, labelled **"observed on the turns after, not attributed (no counterfactual exists)"**; the per-turn `PolicyState` table behind a checkbox | `IObservationHistory.PolicyChanges / PolicyStates`, `IObservationHistory.Settlement(turn, id)` — `PolicyHistoryModel` |
| **ECONOMY** (4) | the T3.9b trade summary / flows / goods rows unchanged; plus the **world GoodAccount table** from the latest TurnRecord: opening, produced, inputs consumed, tool wear, eaten, housing materials, construction materials, closing, "ok" / "DISCREPANCY n" | `TradeModel` + `TurnRecord.Goods` — `TurnAuditModel.GoodAccountLines` |
| **ANNALS** (5) | unchanged | chronicle |
| **TRENDS** (6) — B2 | ONE graph (the T4.18 PlotLarge, one implementation); metrics: every `SeriesKey` — population, food, deficit, happiness, grievance per registry class, inflow, outflow, births, deaths, harvest, eaten, dwellings — with world / settlement scope, plus **price** from the T3.9a `HistoryBuffer` (the only series the observation history lacks); the scope note states what a world value IS: a sum for extensive keys, an unweighted mean for deficit / happiness / grievance ("not a simulation quantity") | `IObservationHistory.Series` — `TrendsModel` |
| **MORE** (7) | unchanged, plus the session file list: manifest, orders, chronicle, trace, telemetry paths | `UiSession.*Path` — `SessionFilesModel` |

**Nothing the old POPULATION and MARKET sections showed became unreachable**:
the roster test now covers every `Section` AND every `SettlementTab` exactly
once, and the two former sections' bodies are drawn verbatim inside the
Population and Economy tabs (`SimUiGame.DrawSettlementPopulation`,
`DrawSettlementEconomy`).

**The world series** (`TrendsModel.World`) is built by summing the SETTLEMENT
series read through `IObservationHistory.Series`, one call per settlement id
ever observed — deliberately, so the UI holds no second copy of the
key → record-field mapping that could drift from `ObservationLog`. Pinned: the
world population series equals `TurnRecord.Population.Closing` on every turn
of a played session.

**Reads built in the UI that the core lanes did not provide** (all over public
records, none a simulation formula): the WHERE ranking (`TurnAuditModel`, a
composite-key insertion sort over `SettlementRecord` fields); the contributor
ranking (`GrievanceViewModel.Rank`, the query's own key re-applied to the
whole list); the world-scope series (`TrendsModel.World`, sums / means over the
seam); the policy consequences (`PolicyHistoryModel.Consequences`, a read of
`Settlement(turn, id)` for the turns after a change).

**Two additions to `UiSession`**, both UI-side: `PreviousWorld` (the world the
last End Turn stepped from — the explain queries take `(prev, next)` and the
observation log deliberately retains no world; `TurnExecutor.Step` never
mutates prev and returns a fresh clone, so the reference is the same immutable
pair the observer read) and `Config` (the loaded `SimConfig`, so the queries
read the registry the simulation ran with).

---

## §3 CLICK-TO-EXPLAIN — the routes (B1)

`ExplainRouting.For(figure)` is a pure table, walked by a test so a clickable
figure cannot exist without a destination:

| clicked figure | opens |
| --- | --- |
| status band · world pop | TURN, population account unfolded |
| status band · food | TURN, grain account unfolded |
| selection card · pop | SETTLEMENT / Population |
| selection card · food | SETTLEMENT / Food |
| selection card · happiness | SETTLEMENT / Grievance with the happiness factors unfolded |
| selection card · grievance | SETTLEMENT / Grievance |

Then inside the surfaces: TURN line → its account legs; WHERE row → select +
centre (`CameraFocus.CenterOn`: `OverlayMeshes.SettlementPosition` →
`Camera.WorldToScreen` → `Camera.Pan(vw/2 − sx, vh/2 − sy)`, so the camera's
own clamp applies and an edge settlement lands as near the centre as the world
allows — both cases tested); Grievance contributor → chain → lever → **open
POLICY**; happiness → factor → chain → lever.

---

## §4 TYPOGRAPHY AND VOCABULARY

Every string is a view-model-owned InvariantCulture string rendered through
`TextUnformatted`; data lines sit in `PushDataFont` blocks per the §3 rule
(the numeric face per BLOCK, never per line); the widgets are the ones already
in use — Button, Selectable, Checkbox, RadioButton, SliderInt, ProgressBar,
PlotLines, BeginChild — plus nothing. No new colour: the pressed state of a
tab or metric button reuses the command bar's `ButtonActive` convention.
Panel bodies are pure view-model classes (`TurnAuditModel`,
`SettlementInspectorModel`, `GrievanceViewModel`, `PolicyHistoryModel`,
`TrendsModel`, `ExplainRouting`, `CameraFocus`, `SessionFilesModel`) composed
by `ScreenModels`; `SimUiGame` only renders.

---

## §5 THE FINAL DIRECTOR TEST — twelve questions, the exact path, the record

| # | question | click path | record / query behind it — or the missing layer |
| --- | --- | --- | --- |
| 1 | why did world population change? | status band **world pop** → TURN, population unfolded: `births +B`, `natural deaths −N`, `starvation −S`, "reconciles to the unit"; WHY: `population delta = births − natural deaths − starvation`, exactly | `TurnRecord.Population` (SUMMED carriers, DIFFERENCED ledger reasons 5/6/7), `TurnRecord.Causes` |
| 2 | why did settlement X lose population? | TURN / WHERE "largest population change" → click X (selects, centres) → selection card **pop** → SETTLEMENT / Population: opening → closing, births, deaths, inflow, outflow, colonists departed + identity | `SettlementRecord.Population` (Vitals, MigrationFlows READ; residual with identity). **Missing layer:** deaths are natural + starvation UNSPLIT per settlement (§8 gap 1) — the tab says so on the line |
| 3 | why are people in X unhappy? | selection card **happiness** → SETTLEMENT / Grievance, factors unfolded: Food (1 − deficit) and Housing (dwellings × 6 / pop), each → chain → lever | `HappinessExplanation.For(next)`; the ScopeNote states happiness omits Comfort and the Tier-A gate by design |
| 4 | what is the primary grievance? | selection card **grievance** → SETTLEMENT / Grievance → per class "primary grievance: Sustenance (marginal lift 0.6869 …)", the PRIMARY row first | `GrievanceExplanation.PrimaryNeedId` = argmax MarginalLift, (lift DESC, need id ASC) |
| 5 | what are its contributors? | same tab: every bound need as a row — satisfaction, weighted shortfall w·(1−s), marginal lift S(s_n:=1) − S, gate flag; unbound needs under "not yet simulated" | `GrievanceExplanation.Needs`. **Stated, not hidden:** the decomposition is observer-defined (CES is not additive) — the AttributionNote is printed above the blocks |
| 6 | what caused the largest contributor? | click the PRIMARY row → its chain: satisfaction ← food fills ← deficit ← grain store / harvest / eaten ← farming share, arable land, weather, tools ← GAPs "tool factor: not recorded: …", "land-vs-labour binding: not recorded: …", "grain imports: not recorded: …" | `CausalChain.ForNeed(prev, next)`; each link cites world, table and row index |
| 7 | which lever? | bottom of the chain: "lever: labour allocation - farming, herding (…)" → **open POLICY**; or "lever: none - <reason>" for weather, arable land, abundance, population, imports | `Levers.For(head node)`. **Missing layer, honestly:** no player trade lever exists in M4 (§8 gap 12), so the imports branch names none |
| 8 | what was my policy last turn? | POLICY → HISTORY: "turn 24 · farming 55% -> 73% · player · order #104"; or the per-turn table: turn N declared / effective | `PolicyChange`, `PolicyState` (order log + `SectorAllocationRow`, nothing else) |
| 9 | what is it now? | POLICY → CURRENT: the running bars + declared vs effective per sector "(in force on turn N)" | latest `PolicyState`; the sliders' snap is `Sectors.Share` on the live row |
| 10 | what changed because of my decision? | POLICY → HISTORY → under each change: "observed on the turns after, not attributed" — turn t+1.. pop / food / deficit / happiness | `IObservationHistory.Settlement(t, id)`. **Missing layer:** attribution of consequences to a decision is NOT possible — the simulation carries no counterfactual; shown as observed, labelled on every block |
| 11 | how has this metric changed over turns? | TRENDS → metric button → world scope → the graph + "latest v over N turn(s)" + the scope note | `IObservationHistory.Series` summed per turn (`TrendsModel.World`); intensive keys are a mean and say so |
| 12 | same at settlement level? | TRENDS → metric → the settlement radio (the selected one) | `IObservationHistory.Series(id, key)` — NaN before founding plots flat |

---

## §6 VERIFICATION, measured at this commit

- `git diff --stat 2a436f1` — `Sim.Ui/`, `Sim.Ui.Tests/`, this file. No simulation project.
- `dotnet build -c Release`: 0 warnings, 0 errors (`grep -c warning` on the build log: 0).
- `Sim.Ui.Tests` (Release): **225 passed, 0 failed, 0 skipped** — 208 pre-existing
  (two T4.18 pins re-aimed at the packet's roster: the leading section is now TURN
  by the packet's order, and the toggle example uses SETTLEMENT) + 17 new in
  `GlassBoxUiTests`: routing covers every figure; TURN lines reproduce a hand-built
  record numerically and print the two flag wordings; WHERE is (magnitude DESC,
  id ASC) with a tie-dense case (three equal magnitudes in descending id order);
  the grievance ranking is tie-dense (four equal lifts, descending ids → ascending);
  GAP links render "not recorded:"; on the STARVED session (settlement 0 ordered to
  zero farming and herding through `EmitSectorOrders`, stepped to its first deficit
  — turn 3 — and once more) the primary is Sustenance, first, its chain carries GAP
  links and the farming share read back as 0, the lever is farming + herding, the
  five unbound needs are listed; policy history renders old / new / actor / order
  index and, on a played session with a turn-2 order, the turn-3 change with
  observed turns 4–6 beneath; every `SeriesKey` returns one value per observed
  turn at both scopes and the world population series equals the TurnRecord's
  closing population; camera centring lands interior settlements at the viewport
  centre to 1e-9 and edge / world-fit cases at the clamp; opening every section
  and tab and building every view model five times on the starved session leaves
  the world hash, the previous world's hash, the order log and the observation
  count unchanged.
- `Sim.Tests` filtered to `Sim.Tests.Observability` (Observability + Explain, the
  only core tests this lane may touch — and touched nothing): **34 passed, 6 failed**
  on this tree. The 6 fail with `LedgerOverdrawException: sinking 67 exceeds
  available stock 66` thrown from `ProductionSystem.Craft` (ProductionSystem.cs:430)
  on the DRIVEN (T3.11-orders) world — the CR-014 defect ("the Craft input cap
  ignores the banked remainder and the ledger throws", commit `2eb1be8`, on the base
  branch before this lane was cut). **Control, measured:** the same filter on the UNTOUCHED base `2a436f1` built in a
  separate worktree fails the SAME six tests — `PolicyHistoryTests.Driven_RecordsAState…`,
  `…Driven_RecordsEveryChange…`, `TelemetryTests.ASettlementRecord_PrintsInFull…`,
  `WorldReconciliationTests.Driven_Seed42_300Turns…`, `StoreLossTests.Driven_SumOfStoreLosses…`,
  `SettlementIdentityTests.Driven_NoFounding…` — 34 passed / 6 failed there too.
  Pre-existing, not this lane's, not actioned (the fix is a simulation change under a
  CR ruling, outside `Sim.Ui`). Every Observability and Explain test that passes on
  the base passes here; nothing in `Sim.Core/Observability` changed.
- `check-banned-constructs.sh` OK · `check-read-isolation.sh` OK · `check-readonly-proof.sh` OK.

---

## §7 WHAT WAS DELIBERATELY NOT DONE

1. **No per-settlement sparkline row on Overview** — the packet says keep
   Overview to text; TRENDS is the one graph.
2. **No attribution of consequences** (§5 #10): observed, labelled, never inferred.
3. **No recomputation of any GAP** — the tool factor, the binding side, the
   pairwise migration matrix, the built / decayed split. Each is printed as
   "not recorded: <where it is computed and discarded>" from the query's own note.
4. **No new colour, widget or font**; no ChromeGeometry rect re-derived; the
   context panel rect is lane D's, untouched.
5. **CR-014 not actioned**: a simulation defect, outside this lane's files; its
   presence on the base tree is measured (§6), not inferred.
