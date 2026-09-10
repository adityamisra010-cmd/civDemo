# EXPLAIN QUERIES — T4.19 lane A2, the on-demand "why"

Branch `t4.19-lane-explain`, cut from `t4.19-glass-box` at `ec9daf7`. Builds the
§5 and §6 queries of `docs/observability-architecture.md` to the letter of its §0
rule. **Nothing here is stored, serialized, or read by any system.** Every query
is a pure function of `(prev, next, cfg, ids)` and lives in
`Sim.Core/Observability/Explain/`.

Every line number below was read from source on this branch at the commit this
document ships in (NeedsGrievanceSystem.cs numbers are POST the A2-FIX
extraction of §0.1, which moved every line after the gate array). A link that
is not a real modelled dependency in that source does not appear. §9 records
the verifier's findings against the first cut and what closed each.

---

## 0. The rule, applied

Each field of each record is exactly one `LinkKind` — READ, SUMMED, DIFFERENCED,
RECOMPUTED, or GAP — and says which. There is no sixth kind. Where the doc says
GAP the record carries a string that says *not recorded* and names the line
where the simulation computes and discards the quantity; nothing is filled in.

The public simulation functions the queries RECOMPUTE through, and nothing
else — every one of them is a function the owning system itself calls:

| function | where the system calls it | line |
| --- | --- | --- |
| `NeedsGrievanceSystem.IsTierAGate(needId)` | gate marking in Step | NeedsGrievanceSystem.cs:265 → 401-405 |
| `NeedsGrievanceSystem.AggregateSatisfaction(sat, isGate, weight, tuning, scratch)` — `ApplyTierAGate` then `Aggregate`, or the expectation when nothing is bound | Step | 270-271 → 448-459 |
| `NeedsGrievanceSystem.TurnoverPerYear(births, deaths, pop, rowDt)` | Step | 202 → 428-432 |
| `NeedsGrievanceSystem.DecayRatePerYear(tuning, turnover)` | Step | 205 → 436-440 |
| `NeedsGrievanceSystem.AccrualPerYear(W, S)` | Step | 272 → 464-465 |
| `NeedsGrievanceSystem.TurnAccrual`, `.TurnDecay`, `.StepGrievance(prev, a, d, dt)` | Step | 281-282 → 468-483 |
| `NeedsGrievanceSystem.Fill(world, settlement, good)` / `.Fill(in GoodStockRow)` | Satisfaction | 323, 330, 340 → 378-396 |
| `SettlementHappiness.Of` / `.Factors` / `.HousingSufficiency` | MigrationSystem | SettlementHappiness.cs:169, 95, 138 |
| `Sectors.Share` | Production, Housing, Construction | WorldState.cs:350 |

### 0.1 The extraction, and why it was necessary

The first cut of this lane restated the system's PRIVATE arithmetic inside
`GrievanceExplanation` — turnover `(B+D)/pop/dt`, `decay = base + (1−inherit)·turnover`,
`accrual = W·max(0, 1−S)`, the Euler step `max(0, g + a·dt − d·g·dt)` — and
`CausalChain` restated the private three-case `Fill`, and labelled the results
RECOMPUTED. The verifier (§9, D1) correctly ruled that architecture §0
reserves RECOMPUTED for a CALL to a public simulation function: a copied
formula is a second simulation, and the mutant of §9 D2 proved the copy was
tied to nothing. The fix is the one §0 implies — the arithmetic became
callable. NeedsGrievanceSystem.Step now computes nothing inline that is not
one of the public static PURE functions in the table above, and the observer
calls those same functions on the same reads.

**Zero behaviour change, measured, not inferred.** Each extracted body is the
expression it replaced, operand for operand and in the same association (the
Euler step is evaluated as `(prev + TurnAccrual) − TurnDecay`, the tree C#
built for the inline expression; .NET does not contract `a·b + c` into a fused
multiply-add). `sim run --seed 42 --turns 50 --founded --hash-log` on the parent
commit `cc7b5de` in a detached control worktree and on this tree:
`88321466b6fa395a38fbd26029eb5107188d25cc69fe91ee389daf437e79c319` both, and
the 50 per-turn hashes are byte-identical (`cmp`). Every golden, snapshot,
replay, pin, NeedsGrievance and Revolt test passes unmoved (§7).

**No constant is restated.** The expectation baseline (`Expectation = 1.0`,
NeedsGrievanceSystem.cs:86) stays private and lives inside
`AccrualPerYear` and `AggregateSatisfaction`; the observer never names it.

**The gate is a predicate, not an array.** `TierAGateNeedIds` is private again
(NeedsGrievanceSystem.cs:95); the observer — and anyone else — asks
`NeedsGrievanceSystem.IsTierAGate(needId)`. A public mutable `int[]` is a lever
any caller can pull (§9, D4); a predicate over it is not.

---

## 1. `GrievanceExplanation.For(prev, next, cfg, settlement, class)`

### 1.1 What it reproduces, line by line

`NeedsGrievanceSystem.Step` (NeedsGrievanceSystem.cs:148-285) reads Prev and
writes Next. The query repeats its reads, in its order, from the same tables:

| quantity | kind | source | system line |
| --- | --- | --- | --- |
| `Total` | READ | `next.Grievances` | written at 299 |
| `Previous` | READ | `prev.Grievances` (0 if absent) | 287-293 |
| `Delta` | DIFFERENCED | Total − Previous | — |
| `DtYears` | READ | `next.Clock.DtYears` — the dt the executor integrated with (TurnExecutor.cs:86-87, 108; SimClock.cs:16) | `ctx.DtYears`, 155 |
| `SettlementPopulation` | SUMMED | `prev.Buckets` | 176-178 |
| `TurnoverPerYear` | RECOMPUTED | `NeedsGrievanceSystem.TurnoverPerYear(row.Births, row.Deaths, pop, row.DtYears)` on the READ `prev.SettlementVitals` row; 0 without a row | 197-204 |
| `DecayRatePerYear` | RECOMPUTED | `NeedsGrievanceSystem.DecayRatePerYear(tuning, turnover)` | 205 |
| `ClassPopulation` | SUMMED | `prev.Buckets` for the class | 227-230 |
| per-need `Satisfaction` | READ | `next.NeedSatisfactions` — the rows the system PUBLISHED this step, walked in registry order (registry ids are loader-enforced ascending, NeedsConfig.cs:174-176) | 240-268 |
| `IsTierAGate` | RECOMPUTED | `NeedsGrievanceSystem.IsTierAGate(need.Id)` | 265, 401-405 |
| `Aggregate` (S) | RECOMPUTED | `NeedsGrievanceSystem.AggregateSatisfaction` on the same spans, same tuning | 270-271 |
| `WeightSum` (W) | config | Σ raw registry weights of the needs that published a row | 266 |
| `AccrualPerYear` | RECOMPUTED | `NeedsGrievanceSystem.AccrualPerYear(W, S)` | 272 |
| `Accrual`, `Decay` | RECOMPUTED | `NeedsGrievanceSystem.TurnAccrual(a, dt)`; `.TurnDecay(d, Previous, dt)` | 468, 472-473 |
| `Recomputed` | RECOMPUTED | `NeedsGrievanceSystem.StepGrievance(Previous, a, d, dt)`; 0 when class or settlement population is 0 (187-193, 231-235); Previous when the settlement was not in `prev.Settlements` | 281-282, 481-483 |

The association is the system's because the function IS the system's:
`StepGrievance` evaluates `(previous + TurnAccrual) − TurnDecay`, the tree C#
built for the inline `gPrev + accrualPerYear * dt - decayRate * gPrev * dt` it
replaced, and Step calls it (281-282).

### 1.2 The components, and why the attribution is observer-defined

`W·(1−S)` is a CES aggregate with σ = 0.5 (a weighted harmonic mean after the
gate). **It is not additive over needs, so no per-need contribution to
grievance exists in the simulation** (architecture §8 item 10). The record
therefore carries, per bound need, two numbers and labels neither as "its
share":

- `WeightedShortfall = w·(1 − s)` — the registry weight times the READ
  shortfall; the plain reading of "how unmet, how much it matters".
- `MarginalLift = S(s with s_n := 1) − S` — how much the aggregate would rise
  if this need alone were fully met, RECOMPUTED by copying the satisfaction
  span, setting one entry to 1.0, and calling
  `NeedsGrievanceSystem.AggregateSatisfaction` again; the subtraction of the
  two RECOMPUTED values is the only arithmetic. The gate is re-applied to the
  lifted vector, so a gate need's lift includes the release of the upper
  needs' collapsed weights.

`MarginalLift` is what the primary is ranked by, because it respects the gate:
on the homeless rig (§7) Comfort at s = 0.64 has lift **exactly 0.0** while
Shelter sits at 0 — the collapse factor `(1 − severity)^collapse` is 0 with
severity 1 and collapse 1.0 (NeedsAggregation.cs:240, 253), so lifting Comfort
changes nothing. `AttributionNote` on the type states all of this in one
sentence and every consumer can print it.

Unbound registry needs (Safety, Health, Belonging/Faith, Dignity/Liberty,
Prospects) are listed with `Bound = false`, NaN values and the note
`not yet simulated` — never omitted. A registry-bound need for which this class
declares no basket (unreachable in shipped data; NeedsGrievanceSystem.cs:258)
is listed the same way with a note saying so; a settlement founded THIS step
(not in Prev, so never stepped — the §2.4 colony) is listed with the note
`founded this step`.

### 1.3 The primary, and the tie-break rule

`PrimaryNeedId = argmax MarginalLift` over the bound needs that published a
row, ties broken by **LOWEST need id** — composite key `(lift DESC, needId ASC)`.
It is implemented as an explicit two-half compare in the shape of
`AppropriationSystem.RichestOtherGrainRow` (AppropriationSystem.cs:260-262):

```
if (!(best >= 0 && (lift < bestLift || (lift == bestLift && need.Id > bestId))))
    { best = r; bestLift = lift; bestId = need.Id; }
```

Never a strictly-greater scan — that would return the right answer only
because the registry loader keeps ids ascending, which is a property of the
loader, not of the argmax. Two tests pin it (§7): a **bit-exact four-way tie**
where every bound need is equally unmet and id 1 wins, and a world whose
satisfaction rows are inserted in **descending** id order that yields the same
primary, the same aggregate and the same per-need values as the ascending one.
The tie is exact by construction (four equal weights → normalised 0.25, s = 0.5
at ρ = −1 → s^ρ = 2, every CES term dyadic), and the one arithmetic
precondition, `Math.Pow(0.5, −1) == 2`, is asserted first.

### 1.4 Exactness — the tolerance, and why it is zero

`Recomputed == Total` is asserted with **exact equality, no epsilon**, on every
(settlement, class) of every step of a fed 8-turn run (96 pairs, 96 exact, 32
with G > 0) and a starved run (60 pairs, 60 exact). A recomputation through the
same public functions on the same doubles is not an approximation; the only
way it can differ is the system calling different functions, or the same ones
on different operands, than the observer reads — which is the event the
assertion exists to catch. It is not tautological: the observer's operands are
its own reads (Prev buckets, the Prev vitals row, the Next satisfaction rows,
the Prev grievance row), never the system's locals.

---

## 2. `CausalChain` — the §5 chains, link by link, verified in source

`CausalChain.ForNeed(prev, next, cfg, settlement, class, needId)` dispatches on
the need's DATA binding exactly as the system does (NeedsGrievanceSystem.cs:243-260):
unbound → one GAP link `NotSimulated`; `source = housingStock` → Shelter;
`BasketBook.SustenanceNeedId` → Sustenance; any other basket-bound need → the
crafted-goods chain. Each `Link` carries `Node`, `Value`, `Kind`, `World`
(Prev / Next / Config / None), `SourceTable`, `SourceIndex` and a `Note` citing
the line it was read from.

**The one-turn lag, stated on the links.** A satisfaction on Next was computed
from fill ratios on Prev (Fill reads Prev, NeedsGrievanceSystem.cs:378-396).
Those fills were written by the consumption step that produced Prev, from the
harvest recorded on Prev; that harvest was produced under the inputs of the
turn before Prev. The inputs the chain reads off Prev (shares, arable land,
weather, tool stock) are the ones IN FORCE for the step Prev→Next — they drive
Next's harvest, not the harvest the chain shows. Every such link's Note says so.

### 2.1 Sustenance

| # | node | kind | source | verified at |
| --- | --- | --- | --- | --- |
| 1 | `SustenanceSatisfaction` | READ | `next.NeedSatisfactions` | s = clamp(got/wanted) × VarietyFactor, staple substitution: NeedsGrievanceSystem.cs:303-351 (Sustenance branch 310-334, variety 346-350) |
| 2 | per basket good: `FoodGoodEaten`, `FoodGoodDemanded` READ; `FoodGoodFill` **RECOMPUTED** | READ / RECOMPUTED | `prev.GoodStocks`; `NeedsGrievanceSystem.Fill(in row)` on that row | the basket lines the system iterates: `BasketBook.Basket(cls, need)` BasketBook.cs:104-114; the fill is the system's own three-case function (no row → 1.0, through `Fill(world, s, good)` with the note `No row`; demand 0 → stock > 0 ? 1 : 0; else the clamped quotient) NeedsGrievanceSystem.cs:378-396, and the note says which case applied |
| 3 | `DeficitRatio`, `NutritionalDemand` | READ | `prev.ConsumptionDeficits` | ratio = (required − obtained)/required, substitution counted once: ConsumptionSystem.cs:183-190 |
| 4 | `GrainStore` | READ | `prev.GoodStocks` grain `Amount` | post harvest, eating, spoilage, overflow: ConsumptionSystem.cs:192-203 |
| 5 | `GrainHarvest` | READ | grain `LastProducedUnits` | Farm credits `harvested` and publishes it: ProductionSystem.cs:243-247 |
| 6 | `GrainEaten` | READ | grain `LastConsumptionEatenUnits` | ConsumptionSystem.cs:224-229. **Eaten > Harvest is the store-drawdown reading**; in a standing famine Eaten == Harvest < demand |
| 7 | `FarmingShare` | RECOMPUTED | `Sectors.Share(prev row or Default, Farming)` | farmLabor = share × adults: ProductionSystem.cs:148-158; Default when no row: 148 |
| 8 | `ArableLand` | READ | `prev.CatchmentSummaries.EffectiveArableKm2` | landSide = arable × Yield: ProductionSystem.cs:202-207, 223 |
| 9 | `HarvestWeather` | READ (GAP when no row) | `prev.HarvestWeather.Multiplier` | applied after the Leontief minimum: ProductionSystem.cs:239; absent row = 1.0: 329-337; mean-one AR(1): HarvestWeatherSystem.cs:30-33 |
| 10 | `ToolsStock` | READ | `prev.GoodStocks` tools `Amount` | equipRatio from the PREV tool stock: ProductionSystem.cs:211-221 |
| 11 | `ToolFactor` | **GAP** | — | `1 + ToolYieldBonusMax × equipRatio` computed and discarded: ProductionSystem.cs:221 (§8 item 9) |
| 12 | `LandVsLabourBinding` | **GAP** | — | which side of `min(landSide, laborSide)` bound: ProductionSystem.cs:223-225 (§8 item 9) |
| 13 | `HerdingShare` | RECOMPUTED | `Sectors.Share(…, Herding)` | the food-deposit pool: ProductionSystem.cs:171-175 |
| 14 | per food deposit: `DepositFoodProduced` | READ | that good's `LastProducedUnits` | workers × OutputPerHerder × abundance × weather × dt: ProductionSystem.cs:300-315 |
| 15 | per food deposit: `DepositAbundance` | READ | `prev.Deposits.Abundance` | sets split and rate: ProductionSystem.cs:300-306 |
| 16 | `GrainImports` | **GAP** | — | grain is the numeraire, price gap structurally zero, **never trades**: TradeArbitrageSystem.cs:63-67, 133-137 (§8 item 12) |

"Food deposit" = a `DepositRow` for this settlement whose good's registry
`category` is `food` (GoodsConfig.cs:18) — livestock and fish in shipped data.

### 2.2 Shelter

| # | node | kind | source | verified at |
| --- | --- | --- | --- | --- |
| 1 | `ShelterSatisfaction` | READ | `next.NeedSatisfactions` | min(1, dwellings × PersonsPerDwelling / pop) on Prev: NeedsGrievanceSystem.cs:133-146, dispatched at 246-253 |
| 2 | `HousingSufficiency` | RECOMPUTED | `SettlementHappiness.HousingSufficiency(prev)` | the same expression, clamped: SettlementHappiness.cs:138-159. **No housing row** (a colony before HousingSystem's first step, HousingSystem.cs:96-101): still RECOMPUTED — the function DEFINES nobody-to-house as 1.0 and people-and-no-row as 0.0 (SettlementHappiness.cs:149, 158) — with `SourceIndex = −1` and a note beginning `No row`; the `Dwellings` link beneath it is the GAP |
| 3 | `Population` | SUMMED | `prev.Buckets` | NeedsGrievanceSystem.cs:135-137 |
| 4 | `PersonsPerDwelling` | READ (config) | `cfg.Housing.PersonsPerDwelling` | sim.json housing |
| 5 | `Dwellings` | READ | `prev.Housing.Dwellings` | HousingSystem.cs:107 |
| 6 | `DwellingsDelta` | DIFFERENCED | next − prev dwellings | = built − decayed, split not recorded (§8 item 6) |
| 7 | `MaintenanceFraction` | READ | `prev.Housing.LastMaintenanceFraction` | m = min over materials of avail/demand: HousingSystem.cs:109-117; decay exp(−(1−m)·dt/τ): 127-140; published: 191 |
| 8 | `ConstructionLabourUsed` | READ | `prev.Housing.LastLaborUsed` | HousingSystem.cs:183, 192 |
| 9 | `ConstructionShare` | RECOMPUTED | `Sectors.Share(…, Construction)` | builderYears, laborCap: HousingSystem.cs:145-150, 160-161 |
| 10 | `TimberStock` | READ | `prev.GoodStocks` timber `Amount` | upkeep demand vs stock: HousingSystem.cs:111-115; timberCap: 162-164 |
| 11 | `ClayStock` | READ — **only when the data draws clay** | `prev.GoodStocks` clay | wired at HousingSystem.cs:114-117, 165-167 but `buildClayPerDwelling = upkeepClayPerDwellingYear = 0.0` in sim.json (structural earth is a non-good); a zero coefficient binds nothing, so the link appears only if either coefficient is > 0 — pinned by a test that flips it |
| 12 | `ExtractionShare` | RECOMPUTED | `Sectors.Share(…, Extraction)` | timber is a deposit good of the extraction pool: ProductionSystem.cs:176-180, 273-316 |
| 13 | `BuiltDecayedSplit` | **GAP** | — | two Ledger flows, world totals only: HousingSystem.cs:130-140, 170-179 (§8 item 6) |
| 14 | `BuildBindingConstraint` | **GAP** | — | `min(min(deficit, laborCap), min(timberCap, clayCap))`: HousingSystem.cs:168 (§8 item 9) |

Housing reads its own SHARED stock table live (HousingSystem.cs:87, 113), so the
timber value shown is the post-step Prev stock — the same row the next step's
maintenance will read.

### 2.3 Comfort (any basket-bound, non-Sustenance need)

| # | node | kind | source | verified at |
| --- | --- | --- | --- | --- |
| 1 | `ComfortSatisfaction` | READ | `next.NeedSatisfactions` | no substitution: NeedsGrievanceSystem.cs:335-344 |
| 2 | per basket good: `ComfortGoodEaten`, `ComfortGoodDemanded` READ; `ComfortGoodFill` RECOMPUTED | READ / RECOMPUTED | `prev.GoodStocks`; `NeedsGrievanceSystem.Fill(in row)` | same Fill cases as 2.1 #2 |
| 3 | per basket good: `CraftOutputProduced` | READ | `LastProducedUnits` | zeroed then credited per turn: ProductionSystem.cs:134-141, 442 |
| 4 | per recipe whose `Output.Good` is the basket good, per input: `CraftInputStock` | READ | input `Amount` | Leontief input cap: ProductionSystem.cs:406-414; the recipe book is `cfg.Goods.Recipes` (goods.json: pottery-firing ← clay 2.0 + timber 0.5; weaving ← fiber 3.0) |
| 5 | per input: `CraftInputDemand` | READ | input `LastInputDemandUnits` | what recipes WANTED from labour alone, pre-cap: ProductionSystem.cs:386-404 |
| 6 | `CraftingShare` | RECOMPUTED | `Sectors.Share(…, Crafting)` | pool split equally across available recipes: ProductionSystem.cs:181-182, 366-369 |
| 7 | `ExtractionShare` | RECOMPUTED | `Sectors.Share(…, Extraction)` | the raw inputs are deposit goods: ProductionSystem.cs:176-180 |
| 8 | `RecipeLabourCap` | **GAP** | — | laborCapPerYear and which of labour/input bound: ProductionSystem.cs:369, 382-384, 412-413 (§8 item 9) |

Recipe availability (`requires` predicates, ProductionSystem.cs:356-364) is not
a link: pottery-firing and weaving carry no predicate in shipped data, and a
predicate's inputs are published variables, not stocks.

### 2.4 Source-row existence is tested, not trusted

Every READ and DIFFERENCED link's `(World, SourceTable, SourceIndex)` is
resolved by the test against the named world's table and must index a real row;
SUMMED links must sum over a non-empty table; RECOMPUTED links with a row cite
a real one and those without say `No row` in their note; every GAP has
`SourceIndex = −1` and a non-empty note. Checked on every chain of every
(settlement, class, need) of every step of a fed 4-turn run and a starved
12-turn run, plus both happiness chains of every settlement (§7) — and, since
A2-FIX, on a **colony**: three fed turns, settlement 0's buckets seeded with
unplaced-departure demand (the T4.4 construction), one step of the
Colonization system alone (the full pipeline's Migration rewrites the demand
before Colonization reads it, MigrationSystem.cs:319). Next then carries a
settlement absent from Prev, populated, with no `HousingRow`, and every chain
of its every need and class plus both happiness chains pass the same checker;
the sufficiency link reads 1.0 on the Shelter chain (Prev holds nobody there)
and 0.0 on the happiness chain (Next holds people and no row), both with
`No row` in the note. The first cut would have failed this checker on that
world; no earlier rig founded a colony, so it never ran.

---

## 3. Levers — `Levers.For(ChainNode)`

M4 has exactly one player policy: the five-sector labour allocation per
settlement (`OrderKind.SectorAllocation`, D-032; the legacy `LaborAllocation`
maps onto the same row). `OrderKind.EnqueueConstruction` exists (M4-D) and
reaches **no** chain node: a completed structure feeds no need, and the queue
only competes for the construction pool (ConstructionSystem.cs:125-145 subtracts
housing's draw; housing does not subtract construction's), so pulling it cannot
raise a dwelling count. Every non-None lever below is therefore
`SectorAllocation` by sector, and the test walks the whole `ChainNode` enum
asserting exactly that.

| node(s) | lever | reason (the mechanism path; never advice) |
| --- | --- | --- |
| SustenanceSatisfaction, FoodGood*, DeficitRatio | farming, herding | grain by the farming share; livestock and fish by the herding share |
| GrainStore, GrainHarvest, GrainEaten, FarmingShare | farming | farm labour = farming share × adults is the Leontief's labour side |
| ToolsStock | crafting, extraction | tools are crafted (toolmaking, goods.json) from bronze cast from extracted ore |
| ToolFactor (GAP) | crafting | transient; its stored input is the tools stock |
| LandVsLabourBinding (GAP) | farming | transient; the labour side is the farming share — the land side has no lever |
| DepositFoodProduced, HerdingShare | herding | the herding pool |
| ShelterSatisfaction, HousingSufficiency, Dwellings, DwellingsDelta | construction, extraction | built = min(deficit, labour cap, timber cap) |
| MaintenanceFraction | extraction | upkeep timber against the extraction pool's stock |
| ConstructionLabourUsed, ConstructionShare | construction | builder-years = construction share × adults × dt |
| TimberStock, ClayStock, CraftInputStock, ExtractionShare | extraction | deposit goods of the extraction pool |
| BuiltDecayedSplit, BuildBindingConstraint (GAPs) | construction, extraction | transient; stored inputs are labour and timber |
| ComfortSatisfaction, ComfortGood*, CraftOutputProduced | crafting, extraction | output = min(labour cap, every input cap) |
| CraftingShare, CraftInputDemand, RecipeLabourCap (GAP) | crafting | the crafting pool |
| **NutritionalDemand** | **None** | a population fact; demographics and migration move it |
| **ArableLand** | **None** | the catchment's fertility-weighted land — a condition of the site |
| **HarvestWeather** | **None** | mean-one stochastic weather — a condition |
| **DepositAbundance** | **None** | a founding endowment — a condition |
| **GrainImports** | **None** | grain never trades (numeraire pinned at 1.0) and trade is autonomous — no player trade lever in M4 |
| **Population** | **None** | Σ bucket counts; no order moves it |
| **PersonsPerDwelling** | **None** | a tuning constant |
| **NotSimulated** | **None** | nothing reaches a need that is not computed |

---

## 4. `HappinessExplanation.For(world, cfg, settlement)`

`Happiness` = `SettlementHappiness.Of` (RECOMPUTED, SettlementHappiness.cs:169-212);
`Factors[Food, Housing]` = `SettlementHappiness.Factors` (95-107). Food hangs the
§2.1 food-supply block (`DeficitRatio` downward, rows 3-16) on the SAME world;
Housing hangs the §2.2 housing block (rows 2-14 minus `DwellingsDelta`, which
needs two worlds and is omitted rather than reported as 0). Same builders as
the needs chains, so happiness and grievance cannot name different causes for
the same shortfall.

`ScopeNote`, carried on the type: **Comfort and the Tier-A gate are absent from
happiness by design** — happiness feeds migration (a behaviour) and D-021
forbids the needs tables from driving behaviour before M5
(SettlementHappiness.cs:27-38). It is deliberately not the number that accrues
grievance, and the record says so where it is read.

---

## 5. `MigrationExplanation.For(prev, next, cfg, settlement)`

| field | kind | source | verified at |
| --- | --- | --- | --- |
| `Inflow`, `Outflow` | READ | `next.MigrationFlows` | rows rebuilt every turn: MigrationSystem.cs:146-148; accumulated: 469-470 |
| `PushDeficit` | READ | `prev.ConsumptionDeficits` (0 if absent) | the source deficit: MigrationSystem.cs:182-184; famine flight = FamineFlightFactor × deficit_src, gap-independent: 20-25, 42-46, 297, 400-402 |
| `Pull` | READ | `next.SmoothedAttractiveness` | the EMA value UPDATED this step and stored on Next is the one the gaps used: MigrationSystem.cs:241-255 (update), 360, 401 (use) |
| `Self`, `Others[]` | READ / RECOMPUTED | per settlement in `prev.Settlements`: smoothed (next), destination deficit (prev), grain stock + last harvest and `GrainPresent = stock > 0 ∨ harvest > 0` (prev), `Happiness = SettlementHappiness.Of(prev)` | the viability inputs: MigrationSystem.cs:170-174 (absolute food gate), 225-235 (deficit repulsion × happiness factor) |
| `Others[]` order | — | **(SmoothedAttractiveness DESC, Id ASC)**, an insertion sort on the explicit two-half predicate — no comparer over doubles alone; pinned by a tie-dense test with rows inserted out of order | — |
| `UnplacedDeparture` | SUMMED | `next.Buckets.UnplacedDeparture` | written when NO reachable destination is viable: MigrationSystem.cs:317-343 (`UnplacedRemainder` in the packet text is this field: the row's departure readout, BucketRow) |
| `PairwiseFlows` | **GAP** | string | executed as transfers, totals only survive: MigrationSystem.cs:409-474 (§8 item 5) |
| `Damping` | **GAP** | string | exp(−cost/D) built per step: MigrationSystem.cs:258-277 |
| `ViabilityProducts` | **GAP** | string | per-destination products discarded: MigrationSystem.cs:225-235 |
| `GapScale` | **GAP** | string | f × m* per pair: MigrationSystem.cs:348-382 |

The gaps are fields, not recomputations, on purpose: recomputing the matrix
from Prev is possible and would be a second implementation of MigrationSystem
that silently lies the day the system changes.

---

## 6. What this lane does NOT do

- No simulation formula is re-implemented. The only arithmetic outside calls
  to public simulation functions is the per-need shortfall `w·(1−s)` (READ ×
  config, labelled observer-defined), the `MarginalLift` subtraction of two
  RECOMPUTED aggregates, and `Delta = Total − Previous` (DIFFERENCED). The fill
  quotient the first cut took itself is gone: the fill is
  `NeedsGrievanceSystem.Fill` on the cited row.
- Nothing is stored. No `WorldState` field, no schema change (v24 stands), no
  table, no observer object that survives the call.
- No system reads any of it; `check-read-isolation.sh` passes with
  `Sim.Core/Observability/` on the allowlist that `t4.19-glass-box` added.
- No golden moved. Every golden, snapshot, pin, NeedsGrievance and Revolt test
  passes at this commit; the full suite's only two failures are a pre-existing,
  quarantined calibration tooth that fails identically on the base commit (§7).

---

## 7. Evidence, measured at this commit

Rigs (`Sim.Tests/Observability/ExplainRigs.cs`): the D-025 dev preset (256²,
N = 4), seed 42, the production pipeline. **Fed** = the never-ordered
subsistence default (so `SectorAllocations` has NO rows and `Sectors.Default`
is what every consumer applies — the share links say `No row: Sectors.Default
in force`). **Starved** = settlement 0 ordered farming 0 / herding 0 /
extraction 45 / crafting 45 / construction 10 at turn 1. **Homeless** = three
fed turns, then every dwelling of settlement 0 sunk through the Ledger
(HousingDecayed, clamp) on the world that becomes Prev, then one step.

Measured on the starved rig (all doubles printed with "R"):

- Orders at turn 1 land in world 2 (world 1 has no `SectorAllocations` row
  for settlement 0, world 2 has one); world 3 is the first with a positive
  deficit: 0.7722222222222223 (dt 10). All three facts are PINNED turn-exact
  in `Starved_PrimaryIsSustenance_AndChainShowsHarvestBelowEaten` (T1.9
  precedent: an order-delivery semantic gets its own pin). Δgrievance(s0,
  peasants) across w3→w4: 15.812544944656029 → 28.171941066285356; w5 reads
  36.36901313219887.
- Explanation for prev = w3, next = w4: `Recomputed == Total ==
  28.171941066285356` (bit-exact); S = 0.31312349024984526, W =
  2.1999999999999997 (1.0 + 0.9 + 0.3 in double, printed as it is),
  accrual/yr 1.5111283214503404, turnover/yr 0.08268792710706149, decay rate
  0.017403189066059227, Accrual 15.111283214503404, Decay 2.751887092874079.
- Components: Sustenance s = 0.22748538011695907, shortfall 0.7725…, **lift
  0.6868765097501548**; Shelter s = 1, lift 0; Comfort s = 1, lift 0. Primary
  = 1 (Sustenance).
- Chain: grain harvest **0** (the ordered zero farming, read back) < grain
  eaten **436**; grain demanded 3078, fill 0.1417; store 0 post-step; farming
  share 0 and herding share 0 (RECOMPUTED from the ordered row,
  `SectorAllocations[0]`); arable 18378.302347770277 km²; weather multiplier
  0.6133211795811901 (READ, a row exists); tools 0; livestock and fish fills
  1.0 (eaten what was demanded from the stores the herding share had built);
  ToolFactor, LandVsLabourBinding, GrainImports = GAP.
- Migration, same step: outflow **398 of 439** (famine flight, push
  0.7722…), inflow 0; pull 2.9578668416234617; others sorted 1 (2.8739…),
  2 (2.6233…), 3 (2.5504…), each deficit 0, grain present, happiness 100;
  self as destination: deficit 0.7722…, no grain, happiness
  32.54228240033001. By world 8 the settlement has emptied and its deficit
  reads 0 again (no requirement) — which is why the twin test steps until it
  starves rather than to a fixed turn.
- Happiness at w3: 32.54228240033001, Food 0.22777777777777775 (= 1 − 0.7722…),
  Housing 1.

Measured on the homeless rig: Shelter s = 0, lift **0.8286599213149356**;
Sustenance s = 0.9102608695652173, lift 8.498494109522581E-05; Comfort
s = 0.6397686746987952, lift **0** (weight collapsed by the gate). Primary = 2.
Total 33.67622615062303 from 15.812544944656029.

Measured on the fed rig, w3→w4, settlement 0 peasants: Sustenance 0.9103
(the shipped-diet variety cap), Shelter 1 (lift 0), Comfort 0.6398; primary =
Comfort (lift 0.0655 > 0.0371) — the never-ordered world's standing grievance
is comfort, as T3.5's header predicted.

Exactness: fed 8 turns, 96 (settlement, class, step) pairs, 96 bit-exact, 32
with G > 0 (the two unemerged classes carry 0); starved run 60 pairs, 60
bit-exact (20 with G > 0). Migrants moved over the fed 10-turn run — Σ
`MigrationFlowRow.Inflow` over every row of worlds 1..10 — is 102, so the
flow READ assertions are not trivial.

Gates at the first cut (`cc7b5de`): `dotnet build -c Release` 0 warnings 0
errors; `Sim.Tests.Observability` 19/19; full `Sim.Tests` 658 passed / 6
skipped / 2 failed in 16 m 6 s. **The 2 failures are
`CalibrationBatteryTests.Dev_MalthusCorridors_AllInBand` at seeds 42 and 7
("3 / 4 starvation deaths"), the cr-003 §7.6 quarantine tooth, and they fail
with the same message on the UNTOUCHED base `ec9daf7` built in a separate
control worktree** — pre-existing, not this lane's, not actioned (the message
itself says a director ruling on CR-003 is the only thing that changes it).

Gates at this commit (A2-FIX), all measured on this tree: `dotnet build -c
Release` 0 warnings 0 errors; `Sim.Tests.Observability` 24/24 (3 s); the
families the fix could move — Observability + NeedsGrievanceTests +
RevoltTests + DrivenGoldenTests + SnapshotTests + CiPinAgreementTests +
IntegratedPinAttributionTests + ReplayTests + ReplayReportTests — 87/87 in
1 m 46 s; full `Sim.Tests` 663 passed / 6 skipped / 2 failed in 11 m 36 s, the
2 being the same `Dev_MalthusCorridors_AllInBand` seeds 42 and 7 with the
same "3 / 4 starvation deaths" message as at `cc7b5de` and at the untouched
base — pre-existing, not actioned. No golden, snapshot, pin or replay moved.
`check-banned-constructs.sh`, `check-read-isolation.sh`,
`check-readonly-proof.sh` all OK. Before/after hash of the 50-turn founded
run: §0.1.

---

## 8. Gaps recorded by this lane (nothing filled)

Restated from architecture §8 as they surface in these records, plus one of
this lane's own:

| where it shows | gap | owning line |
| --- | --- | --- |
| Sustenance chain | tool factor; land-vs-labour binding side | ProductionSystem.cs:221, 223-225 (§8.9) |
| Sustenance chain | grain imports — none exist, no trade lever | TradeArbitrageSystem.cs:133-137 (§8.12) |
| Shelter chain | built vs decayed split | HousingSystem.cs:130-140, 170-179 (§8.6) |
| Shelter chain | which build cap bound | HousingSystem.cs:168 (§8.9) |
| Comfort chain | per-recipe labour cap; labour-vs-input binding | ProductionSystem.cs:369, 412-413 (§8.9) |
| Grievance | per-need accrual — not a simulation quantity; attribution observer-defined | (§8.10) |
| Migration | pairwise flows, damping, viability products, gap scale | MigrationSystem.cs:409-474, 258-277, 225-235, 348-382 (§8.5) |
| Any chain | a satisfaction row absent (no members / extinct / founded this step) | NeedsGrievanceSystem.cs:187-193, 231-235 |
| Shelter / happiness Housing | no housing row yet (a colony before HousingSystem's first step) — the `Dwellings` link; the sufficiency above it is RECOMPUTED with `No row` | HousingSystem.cs:96-101 |

The first cut listed "`Expectation` is private (1.0 restated)" here as a gap
of its own making; it is gone — the constant is inside the public functions
the observer calls (§0.1).

---

## 9. A2-FIX — the verifier's findings on the first cut, and their closure

| # | finding (verdict against `cc7b5de`) | closure | evidence |
| --- | --- | --- | --- |
| D1 | `GrievanceExplanation` re-implemented the system's private turnover, decay rate, accrual and Euler step, (labelled RECOMPUTED — a label §0 reserves for calls to PUBLIC simulation functions), and `CausalChain.FillLinks` the private three-case `Fill` (its quotient labelled READ, which it was not — it was a formula) | the arithmetic was extracted from `NeedsGrievanceSystem` into public static pure functions the system itself calls (§0 table); the observer calls those | 50-turn founded hash identical before/after (§0.1); every golden/snapshot/replay/pin unmoved (§7); `AccrualAndDecay_ReproduceTheSystemsStock_Exactly` still bit-exact on 156 pairs |
| D2 | a mutant replacing `Fill` case 3 with `1.0` survived the whole suite: the copied fill was tied to nothing | `ExplainRecomputedFunctionTests`: `Fill` pinned on four case-3 points including both clamp ends, both case-2 branches and case 1; the chain's fill asserted equal to `NeedsGrievanceSystem.Fill` on the cited row AND to the quotient of the two READ links beside it on the starved world (grain 436/3078), with the system's published Sustenance satisfaction below 0.5 | the verifier's mutant (the observer's copy) is no longer expressible — there is no copy; the equivalent mutation in `NeedsGrievanceSystem.Fill` (case 3 → 1.0), applied in a separate tree built from this commit and run against the Observability + NeedsGrievanceTests filter (41 tests, 3 s clean, bounded 15 min), kills 6: the two new tests, `Starved_PrimaryIsSustenance_AndChainShowsHarvestBelowEaten`, and three pre-existing `NeedsGrievanceTests` (`Satisfaction_ClampsAtBothEnds`, `AntiTautology_SatisfactionRESPONDSToTheStandard_PerturbedEitherWay`, `Famine_RaisesGrievance_InTheStarvationWindow`) |
| D3 | `HousingSupply` emitted a RECOMPUTED sufficiency citing `Housing[−1]` with a note lacking `No row` for a settlement with no `HousingRow` (a colony before HousingSystem steps it); the lane's own checker would have failed there, and no rig reached it | the no-row case carries a `No row` note naming both defined values (1.0 nobody, 0.0 unhoused); a colony rig was added and the checker walks every chain of the colony (§2.4) | `Colony_WithoutAHousingRow_EveryLinkStillCitesHonestly` |
| D4 | `TierAGateNeedIds` was made a public mutable `int[]`; any caller could write it | private again; `public static bool IsTierAGate(int)` is the only read | `IsTierAGate_MarksExactlySustenanceShelterSafety_AndTheObserverAgrees` |
| D5 | the doc called `0.7722222222222223` "pinned turn-exact" when it was only a comment | asserted bit-exact at world 3 in `Starved_PrimaryIsSustenance_AndChainShowsHarvestBelowEaten` | the assertion passes at this commit |
| D7 | the lane needs a rebase before merge | NOT done — the integrator merges; this tree is still cut from `ec9daf7` | `git merge-base` unchanged |
