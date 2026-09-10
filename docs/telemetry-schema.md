# TELEMETRY SCHEMA — `telemetry/v1` (T4.19, lane A1)

The record types in `Sim.Core/Observability/`, the JSONL `sim inspect --telemetry`
and a played session write, and the identities the tests assert. Built to
`docs/observability-architecture.md` §2, §3, §4, §7; every field carries its
§0 KIND. Nothing here is serialized into `WorldState` or `CanonicalSchema`
(schema stays v24, pinned in `TelemetryTests`); no system references the
namespace (asserted at source level in the same test).

**KIND legend (§0):** READ — copied from a row a system wrote · SUMMED — integer
sum over READ rows · DIFF — next − prev of a READ/SUMMED quantity · RESIDUAL —
remainder of a stated identity, identity carried as a string · RECOMP — a call
to a PUBLIC simulation function on stored state · GAP — a string, never a number.

**Which world?** `prev` is the state the step read; `next` is the state it wrote.
Openings are prev, closings are next, "this step" rows (Vitals, MigrationFlows,
`Last*`, ConsumptionDeficits, TradeFlows, NeedSatisfactions) are next —
the systems write them for prev.Settlements during the step. Migration's
INPUTS are prev, because MigrationSystem reads prev (§3.2 one-turn lag).

**Order delivery rule (cited):** `TurnExecutor.Step` calls
`_orders?.BatchFor(prev.Clock.Turn)`; an order with `Turn == t` is applied by
the step that transforms turn-t state into turn-(t+1) state. `OrderApplied.For(log,
prev.Clock.Turn)` makes the same selection, in log order, carrying each row's
log index as its order number.

---

## 1. File shape

One line per observed step (turn 1 is the first — turn 0 is the world before
any step and has no record):

```
{ "schema": "telemetry/v1", "turn": <TurnRecord>, "settlements": [ <SettlementRecord>… ] }
```

Written by `TelemetryWriter.WriteTurn` (Utf8JsonWriter, doubles round-trippable,
InvariantCulture). Arrays are in table/registry index order. Non-finite doubles
are the strings `"NaN"`, `"Infinity"`, `"-Infinity"` — an absent reading (no price
row yet, no attractiveness row yet) must never read as 0. Byte-identical across
two identical sessions (asserted, `TelemetryTests`; measured again on the CLI:
two `sim inspect --telemetry` runs `cmp` equal).

Files: `runs/telemetry-<stamp>.jsonl` beside the four T4.17 files, named in the
manifest's `telemetryFile` (a manifest field; a pre-T4.19 manifest reads back
with it empty). `UiSession.EndTurn` observes after the step and before the
chronicle — order immaterial, both read-only over the same pair. The file is
rewritten in full on every save, like the trace.

`sim inspect --settlement ID --turn N` prints one settlement record, indented,
through the same writer (`TelemetryWriter.WriteSettlement`), so "in full" holds
by construction.

---

## 2. TurnRecord (§2)

| field | kind | source |
| --- | --- | --- |
| `turn`, `year`, `dtYears` | READ | `next.Clock.Turn`, `.WorldDateYears`, `.DtYears` |
| `stocks[]` | — | one per conserved quantity with a carrier row in prev or next or a ledger row; ascending quantity id (population 3, dwellings 4, goods 100+id; biomass 1 / toyGood 2 only on the toy world) |
| `stocks[].opening`, `.closing` | SUMMED | Population: `BucketRow.Count` + `NotableRow.Count` · Dwellings: `HousingRow.Dwellings` · good g: `GoodStockRow.Amount` where `Good == g` — the same carriers `ConservationAuditor` sums |
| `stocks[].sources[]/sinks[] {reason, name, units}` | DIFF | `LedgerFlowRow.TotalSourced/TotalSunk` (cumulative for the run) next − prev per (quantity, reason); rows only ever append (`Ledger.AddFlowRow`) so a row absent from prev differences against 0; ascending reason id; `name` is `ReasonNames` (display only) |
| `stocks[].reconciles`, `.discrepancy` | — | `opening + Σsources − Σsinks == closing` EXACTLY; discrepancy = closing − (opening + Σsources − Σsinks) |
| `population {opening, births, naturalDeaths, starvation, closing, reconciles, discrepancy}` | SUMMED / DIFF | reasons Births 5 (source), Deaths 6, Starvation 7 (sinks) on quantity 3. Notables are summed and stay 0: nothing in the shipped pipeline calls `NotableLifecycle` |
| `grain {opening, endowment, harvest, eaten, spoilage, overflow, closing, …}` | SUMMED / DIFF | reasons InitialEndowment 2, Harvest 3 (sources), Eaten 4, Spoilage 14, GranaryOverflow 15 (sinks) on the grain good's quantity. Endowment is 0 on every step (founding-time only); carried so it can never break a step |
| `dwellings {opening, built, decayed, closing, …}` | SUMMED / DIFF | HousingBuilt 11, HousingDecayed 12 on quantity 4 |
| `goods[]` (every non-grain good, registry order) `{good, name, opening, produced, inputsConsumed, toolWear, eaten, housingMaterials, constructionMaterials, closing, reconciles, discrepancy}` | SUMMED / DIFF | Produced 8 (source); InputsConsumed 9, ToolWear 10, Eaten 4, HousingMaterials 13, ConstructionMaterials 16 (sinks). A reason outside this list still reconciles in `stocks[]` and shows here as a discrepancy — that is the point of the two views |
| `flows.migrantsMoved` | SUMMED | `MigrationFlowRow.Inflow` over next (Σ inflow == Σ outflow, asserted) |
| `flows.settlementsFounded` | DIFF | `next.Settlements.Count − prev.Settlements.Count` |
| `flows.controlLost` | DIFF | `ControlRow` (Polity, Place) present in prev and absent in next |
| `flows.tradeUnits`, `.tradeFlowCount` | SUMMED / READ | `TradeFlowRow.Quantity`, `TradeFlows.Count` on next |
| `flows.unattributedGrainTransfer` | — | true when any NON-founded settlement's `food.storeLosses < 0` this turn: the honest appropriation detector (§3 below) |
| `orders[] {index, turn, actor, kind, targetId, settlement, sector, amount}` | READ | `OrderRecord` rows with `Turn == prev.Clock.Turn`; `index` = position in the OrderLog; `settlement`/`sector` decoded per `OrderKind` doc (SectorAllocation: `targetId >> 3`, `& 7`) |
| `policy[] {settlement, shares[5]}` | RECOMP | `Sectors.Share` on the PREV `SectorAllocationRow` — the row Production/Housing/PathBuild read this step; `Sectors.Default` when no row (ProductionSystem.cs:148, HousingSystem.cs:146, PathBuildSystem.cs:125) |
| `causes {populationDelta, populationExplained, grainDelta, grainExplained}` | DIFF | delta = closing − opening; explained = the account's named legs. Equal whenever the account reconciles — an identity, not a finding |

**Asserted** (`WorldReconciliationTests`): every `stocks[]`, `population`,
`grain`, `dwellings` and all 13 `goods[]` reconcile on every one of 300 turns on
the founded seed-42 world and on the driven (T3.11 orders) world; `causes` equal;
16 stocks present, ascending. Non-vacuity pinned to measured turns: births,
natural deaths, migrants, harvest and spoilage all > 0 on turn 2 of both worlds;
overflow turn 1; starvation turn 55 (founded) / 7 (driven); trade turn 41 / 7;
decay turn 25 (driven); pottery produced AND consumed on driven turn 5. Teeth:
one person sourced through a scratch ledger fails the population account by
exactly 1 and leaves grain reconciling.

---

## 3. SettlementRecord (§3)

One per settlement in `prev.Settlements` (in that order) then one FOUNDING record
per settlement new in `next` (`founded: true`). The founding record is the same
shape: every opening is 0, no system wrote a row for it (they iterate
`prev.Settlements`), so both residuals come out NEGATIVE — the party and the
provisions arrived. Summing over all records therefore closes on founding turns
too.

### identity
| field | kind | source |
| --- | --- | --- |
| `settlement`, `foundedTurn` | READ | `SettlementRow.Id`, `.FoundedTurn` (colonization stamps `prev.Clock.Turn + 1`) |
| `controller` | READ | `EmpireQuery.TryGetController(next)` → `PolityId`; −1 when none (colonies of stateless parents) |
| `founded` | — | new in next |

### population
| field | kind | source |
| --- | --- | --- |
| `opening`, `closing` | SUMMED | `BucketRow.Count` + `NotableRow.Count` for this settlement, prev / next |
| `children`, `adults`, `elders` | RECOMP | `BandViews.Children/Adults/Elders(next.Buckets)` |
| `notables` | SUMMED | `NotableRow.Count` on next (0 in the shipped pipeline) |
| `births`, `deaths` | READ | `SettlementVitalsRow` (next); deaths = natural + starvation, UNSPLIT (§8 gap 1) |
| `inflow`, `outflow` | READ | `MigrationFlowRow` (next) |
| `colonistsDeparted` | RESIDUAL | `opening + births − deaths + inflow − outflow − closing`; absorbs ONLY colonization (party leaves by `Ledger.Transfer`, ColonizationSystem.Found step 4, recorded nowhere — §8 gap 3) |
| `colonistsDepartedIdentity` | — | the identity string, verbatim (`Observer.ColonistsDepartedIdentity`) |
| `classCounts[] {class, name, count}` | SUMMED | `BucketRow.Count` per registry class on next |

**Asserted** (`SettlementIdentityTests`): `colonistsDeparted == 0` for every
settlement on every turn of both 300-turn worlds (neither founds — 12 throughout;
>1000 settlement-turns carry births, deaths AND migration); Σ over all records
== 0 on every turn; per-settlement births / deaths / inflow / outflow / opening /
closing sum to the ledger legs; `closing == Σ classCounts + notables ==
children + adults + elders + notables`. On the founding world (stranded-source
rig under the FULL pipeline, seed 1): settlement 12 founded on turn 2 from
settlement 0; the colony's record has opening 0, closing = party (143 measured),
births = deaths = inflow = outflow = 0, `colonistsDeparted == −closing`, no
housing row, no consumption row, no orders; the source's residual == the party;
turns 3–6 are ordinary again (residual 0 everywhere, colony has its housing row).
FirstReign founds nothing in 40 turns on this tree (1 settlement — the "1 → 17"
trajectory was a defect of an earlier T4.4 revision, per FirstReignTests), which
is why the rig is the founding world.

### food
| field | kind | source |
| --- | --- | --- |
| `grainOpening`, `grainClosing` | READ | grain `GoodStockRow.Amount`, prev / next |
| `harvest` | READ | grain `LastProducedUnits` on next — the Harvest source's amount (ProductionSystem.cs:244-247) |
| `eaten` | READ | grain `LastConsumptionEatenUnits` on next — the Eaten sink's amount (ConsumptionSystem.Consume) |
| `storeLosses` | RESIDUAL | `grainOpening + harvest − eaten − grainClosing`; absorbs spoilage + granary overflow (per settlement by nothing, §8 gap 2), appropriation transfers (§8 gap 4), colony provisions (§8 gap 3) |
| `storeLossesIdentity` | — | `Observer.StoreLossesIdentity` |
| `demandUnits`, `deficitRatio` | READ | `ConsumptionDeficitRow` (next); 0 / 0.0 when no row (founding record) |
| `foodGoods[] {good, name, produced, demand, eaten}` | READ | goods of category `food` (grain, livestock, fish): `LastProducedUnits`, `LastConsumptionDemandUnits`, `LastConsumptionEatenUnits` on next |
| `foodObtained` | SUMMED | Σ `foodGoods[].eaten` |

**Asserted** (`StoreLossTests`): Σ `storeLosses` over ALL records ==
`grain.spoilage + grain.overflow` on every turn of both 300-turn worlds and of
the founding world (founding turn included — without the colony's negative
record the prev settlements over-count by exactly the provisions); per-settlement
harvest / eaten / openings / closings sum to the ledger legs; no non-founded
settlement is ever negative and `unattributedGrainTransfer` is never set on
these worlds (>2,500 settlement-turns with positive loss — 2,895 measured,
driven). **Appropriation, honestly:** no raid rule is re-derived. A raid is an
unrecorded transfer, so the raider's residual goes negative while the world sum
still closes; the turn is LABELLED (`flows.unattributedGrainTransfer`). Teeth: a
50-grain `Ledger.Transfer` between two stocks gives −50 / +50, the label, and a
world sum of 0.

### housing
| field | kind | source |
| --- | --- | --- |
| `hasRow` | READ | a `HousingRow` exists on next (colonies start homeless — HousingSystem creates the row on their first turn) |
| `dwellingsOpening`, `dwellingsClosing` | READ | `HousingRow.Dwellings` prev / next |
| `capacity` | READ × config | `dwellingsClosing × cfg.Housing.PersonsPerDwelling` |
| `need` | SUMMED ÷ config | `population.closing / PersonsPerDwelling` |
| `sufficiency` | RECOMP | `SettlementHappiness.HousingSufficiency(next)` |
| `lastMaintenanceFraction`, `lastLaborUsed` | READ | `HousingRow` on next |
| `builtDecayedSplit` | GAP | string (§8 gap 6) |

### economy
| field | kind | source |
| --- | --- | --- |
| `goods[] {good, name, stock, produced, inputDemand, consumptionDemand, eaten, price}` | READ | `GoodStockRow` (next) per registry good; `PriceRow.Price` on next, `"NaN"` when no row |
| `tradeIn[]`, `tradeOut[] {other, good, name, quantity}` | READ | `TradeFlowRow` on next with `To == this` / `From == this` |
| `sectorShares[5]` | RECOMP | `Sectors.Share` on the PREV row (in force this step); `Sectors.Default` when none |
| `sectorRowPresent` | READ | prev carried a row |
| `foodSurplusRatio`, `artisanShare`, `tradeVolume` | READ | `VariableRow` ids 1, 2, 4 on next; `"NaN"` when absent |
| `classActive[] {class, name, active}` | READ | `ClassStateRow.Active` on next |

### social
| field | kind | source |
| --- | --- | --- |
| `happiness` | RECOMP | `SettlementHappiness.Of(next)` — equal to the same call on the final world (asserted) |
| `happinessFactors[2]` | RECOMP | `SettlementHappiness.Factors(next)`: [Food, Housing] |
| `grievance[] {class, name, value}` | READ | `GrievanceRow` on next, table order |
| `needSatisfaction[] {class, need, name, value}` | READ | `NeedSatisfactionRow` on next, table order |

The §5 explanation (components, primary, lever) is an on-demand query over these
rows (lane A2, `Sim.Core/Observability/Explain/`), deliberately not stored.

### migration (what MigrationSystem READ — prev)
| field | kind | source |
| --- | --- | --- |
| `pushDeficitRatio` | READ | prev `ConsumptionDeficitRow.DeficitRatio` (famine-flight driver); 0 when no row |
| `pullAttractiveness` | READ | prev `SmoothedAttractivenessRow.Value`; `"NaN"` when no row (turn 1, asserted) |
| `allAttractiveness[] {settlement, value}` | READ | every prev row, table order — the gap the mechanism responds to |
| `prevGrainStock`, `prevGrainHarvest` | READ | prev grain `Amount`, `LastProducedUnits` — the two inputs of the ADR-012 absolute food gate (MigrationSystem.cs:173-174) |
| `unplacedDeparture`, `unplacedRemainder` | SUMMED | `BucketRow.UnplacedDeparture/UnplacedRemainder` on next — the demand migration could not place, which colonization draws from |
| `pairwiseFlows` | GAP | string (§8 gap 5) |

### policy
| field | kind | source |
| --- | --- | --- |
| `declaredWeights[5]` | READ | `SectorAllocationRow` raw weights on next — PathBuildSystem.Upsert writes exactly `order.Amount / 100` (PathBuildSystem.cs:83-103), so the row IS the last declaration; `Sectors.Default` raw when never ordered (0.55 / 0.15 / 0.10 / 0.12 / 0.08, WorldState.cs) |
| `declaredRowPresent` | READ | next carried a row |
| `effectiveShares[5]` | RECOMP | same array as `economy.sectorShares` (prev, in force) |

### orders
`orders[]` — the step's `OrderApplied` rows whose decoded settlement is this one.

---

## 4. PolicyHistory (§4) — in memory, on `IObservationHistory`

```
PolicyChange { turn, year, settlement, sector, oldWeight, newWeight, actor, orderIndex }
PolicyState  { turn, settlement, declared[5], effective[5] }     one per settlement per turn
```

| field | kind | source |
| --- | --- | --- |
| `turn`, `year` | READ | `next.Clock` — the turn on whose state the new weight is first visible |
| `oldWeight`, `newWeight` | READ | prev row raw weight (or Default) → next row raw weight; a change is `old != new`, exact |
| `actor`, `orderIndex` | READ | the applied order targeting this settlement and sector (SectorAllocation with that sector, or LaborAllocation which sets all five); the LAST in log order stands because Upsert is applied in batch order. −1 / −1 when nothing explains the change (unreachable through the pipeline — only Upsert writes the row — recorded rather than dropped) |
| `declared[5]`, `effective[5]` | READ / RECOMP | next row raw weights (Default when none) / `Sectors.Share` on it |

**Asserted** (`PolicyHistoryTests`): driven world — exactly 56 changes (60 orders,
4 no-ops where group C's extraction equals the default), all on turn 3, year 30,
actor 1, `orderIndex == settlement × 5 + sector`, old = Default raw, new = mix /
100; 12 × 300 states, Default through turn 2 and the mix from turn 3, `effective`
equal to `Sectors.Share` recomputed on `declared`. **Turn-exact lag pin:** the
turn-3 TurnRecord's in-force `policy[]` is still the default; turn 4 carries the
mix; the turn-3 SettlementRecord has `sectorRowPresent false` and
`declaredRowPresent true`; the turn-3 `orders[]` holds the 60 orders with
indices 0..59 and settlement 7's record holds its 5 (first index 35). Founded
world — 0 changes, 3,600 default states. Legacy `LaborAllocation` (two orders,
same target, same turn): 5 changes at turn 2 (0.55→0.4, 0.15→0, 0.10→0, 0.12→0,
0.08→0.6) all attributed to order index 1, the last.

No argmax over doubles exists in this lane (attribution picks the last integer
index), so no tie-dense test is owed here; lane A2's primary-grievance argmax
carries one.

---

## 5. ObservationLog / IObservationHistory (§7)

`ObservationLog.Observe(prev, next, cfg, OrderApplied.For(orders, prev.Clock.Turn))`
once per step, contiguous turns enforced (a gap throws — a log with holes
misaligns every series silently). It retains records only, never a world.

`IObservationHistory`: `Observations`, `FirstTurn`, `LastTurn`, `At(turn)`,
`Settlement(turn, id)`, `Series(id, SeriesKey, Span<double>, classId)`,
`PolicyChanges`, `PolicyStates`. `SeriesKey`: Population, Food (grainClosing),
Deficit, Happiness, Grievance (by class), Inflow, Outflow, Births, Deaths,
Harvest, Eaten, Dwellings — each read off the record field named in the enum's
comment; NaN where the settlement has no record on a turn (asserted with a
non-existent id).

The bounded in-memory window is NOT implemented (§7: nothing in M4 needs it); the
seam is.

---

## 6. The fence (§9) — asserted

- `TelemetryTests.NoTelemetryInducedChange…`: founded seed 42, 50 turns, one
  executor observed after every step and one never — `WorldHash` equal and
  `StateEquals` true; the log holds 50 observations with births > 0.
- `TelemetryTests.NoSystemAndNoKernelFile…`: no file under `Sim.Core/Systems`,
  `Kernel`, `State`, `Worldgen` mentions `Sim.Core.Observability` or
  `ObservationLog`; `CanonicalSchema.Version == 24`.
- `Sim.Ui.Tests.SessionRecordTests.ThePlayedSessionREPRODUCES…` (pre-existing):
  a `UiSession` — which now observes every End Turn — replays hash-for-hash
  against a bare executor; and `T419_TheSessionObservesEveryEndTurn…`: 6 End
  Turns → 6 observations, 6 JSONL lines, manifest `telemetryFile`.
- Observers take `IReadOnlyWorldState` on both sides: a write does not compile.
