# OBSERVABILITY ARCHITECTURE — T4.19, the glass box

Branch `t4.19-glass-box`, cut from `feaf218` (T4.18). **Not merged to main; M5 untouched.**

The director's ruling: *more simulation depth + more player observability*. The
player must be able to see what happened, where, why, what caused that cause,
what he can change, and what that change is expected to do. This record is the
contract every T4.19 lane builds against. Everything in it was read from source
at `feaf218` by six independent readers whose findings are cited by file:line in
their reports; nothing below is designed from memory.

---

## §0 THE ONE RULE

**The logger observes. The simulation calculates. The UI reads. The player
issues orders.** Observability must not become a second simulation.

Every field in every record is therefore one of exactly five things, and the
record type says which:

| kind | meaning | example |
| --- | --- | --- |
| **READ** | copied from a row a system wrote | `SettlementVitalsRow.Births` |
| **SUMMED** | an integer sum over READ rows | population = Σ `BucketRow.Count` |
| **DIFFERENCED** | next − prev of a READ or SUMMED quantity | Δ`LedgerFlowRow.TotalSunk` for reason 14 |
| **RESIDUAL** | the remainder of a stated identity whose every other term is READ/SUMMED/DIFFERENCED; carries the identity and the NAME of what it absorbs | per-settlement store losses |
| **RECOMPUTED** | a call to a PUBLIC static simulation function on stored state — never a private re-implementation | `SettlementHappiness.Of`, `NeedsAggregation.Aggregate`, `Sectors.Share` |

Nothing else is permitted. If a quantity would need a formula the simulation
does not expose, it is a **GAP** (§8) and the record says so, rather than an
observer-side copy of the formula that will drift.

---

## §1 WHAT IS AUTHORITATIVE, AND AT WHAT LEVEL

The two instruments, and the boundary between them, measured from source:

**World level — the Ledger.** Every conserved stock moves only through
`Ledger.Flow` (source/sink, records a `LedgerFlowRow(Quantity, Reason,
TotalSourced, TotalSunk)`, **cumulative for the whole run**) or
`Ledger.Transfer` (stock to stock, **records nothing**). So the first difference
of the flow table between prev and next is the exact world-level source and sink
by reason for that turn, to the unit. Reasons in shipped data: InitialEndowment
2, Harvest 3, Eaten 4, Births 5, Deaths 6, Starvation 7, Produced 8,
InputsConsumed 9, ToolWear 10, HousingBuilt 11, HousingDecayed 12,
HousingMaterials 13, Spoilage 14, GranaryOverflow 15, ConstructionMaterials 16.

**Settlement level — the systems' own rows.** There is no settlement dimension
on the ledger. What exists per settlement per turn, written by the owning
system: `SettlementVitalsRow(Births, Deaths)` (deaths = natural + starvation,
**unsplit**), `MigrationFlowRow(Inflow, Outflow)`, `ConsumptionDeficitRow
(DeficitRatio, DemandUnits)`, `GoodStockRow.{LastProducedUnits,
LastConsumptionDemandUnits, LastConsumptionEatenUnits, LastInputDemandUnits}`
per good, `TradeFlowRow(From, To, Good, Quantity)` (non-grain only),
`HousingRow.{Dwellings, LastMaintenanceFraction, LastLaborUsed}`,
`NeedSatisfactionRow(Settlement, Class, NeedId, Value)`, `GrievanceRow
(Settlement, Class, Value)`, `SmoothedAttractivenessRow`, `SectorAllocationRow`,
`VariableRow` (food_surplus_ratio, artisan_share, population, trade_volume),
`PriceRow`/`PriceTermRow`, `ClassStateRow`, `ControlRow`, `BucketRow`.

**Transfers that are recorded nowhere per settlement** (the complete list, from
an exhaustive grep of `Ledger.Transfer` call sites): cohort aging (intra), class
mobility (intra), migration (recorded as totals only), **colonization party and
provisions**, **appropriation raids**, trade (recorded in `TradeFlowRow`).

---

## §2 THE TURN RECORD (world)

One per completed step, built from `(prev, next, cfg, ordersAppliedThisStep)`.

```
TurnRecord
  Turn, Year, DtYears                                  READ  (next.Clock)
  Stocks[]  one per conserved quantity present:
    Quantity, Opening, Closing                         SUMMED (prev / next carriers)
    Sources[] { Reason, Units }                        DIFFERENCED (ledger TotalSourced)
    Sinks[]   { Reason, Units }                        DIFFERENCED (ledger TotalSunk)
    Reconciles                                         Opening + ΣSources − ΣSinks == Closing, EXACTLY
    Discrepancy                                        the integer difference when it does not
  Population: Opening, Births, NaturalDeaths, Starvation, Closing, Reconciles
  Grain:      Opening, Endowment, Harvest, Eaten, Spoilage, Overflow, Closing, Reconciles
  Dwellings:  Opening, Built, Decayed, Closing, Reconciles
  Goods[]:    per non-grain good — Opening, Produced, InputsConsumed, ToolWear, Eaten,
              HousingMaterials, ConstructionMaterials, Closing, Reconciles
  Flows:      MigrantsMoved (Σ Inflow), SettlementsFounded (count diff),
              ControlLost (ControlRows present in prev and absent in next),
              TradeUnits (Σ TradeFlowRow.Quantity), TradeFlowCount
  Orders[]:   { Index, Turn, Actor, Kind, TargetId, Amount }   READ (the log rows whose Turn == prev.Turn)
  Policy[]:   per settlement — effective sector shares IN FORCE this step   RECOMPUTED (Sectors.Share on prev row)
  Causes:     Population Δ = Births − NaturalDeaths − Starvation   (exact, by construction)
              Grain Δ     = Harvest − Eaten − Spoilage − Overflow (+ Endowment at founding)
```

**Reconciliation is a test, not a hope.** `Reconciles` is asserted true on the
founded and driven worlds for every one of 300 turns. The T4.18 investigation
already showed the grain identity closes to the unit on the director's own
session (82,041 − 38,391 − 24,029 − 13,869 = 5,752); this makes it a standing
property. A turn that does not reconcile is a **simulation defect** and the
record says so with the integer discrepancy — never rounded away.

Notables carry population too (`NotableRow.Count`) but nothing in the shipped
pipeline calls `NotableLifecycle`, so the carrier is summed and stays zero.

---

## §3 THE SETTLEMENT RECORD

One per settlement present in `prev.Settlements`, per step. A settlement founded
THIS step gets a founding record (population = party, grain = provisions,
everything else absent — the systems iterate `prev.Settlements`).

```
SettlementRecord
  IDENTITY   Settlement, FoundedTurn, Controller (PolityId or none)          READ
  POPULATION Opening, Closing (Σ Buckets), Children/Adults/Elders (BandViews)  SUMMED
             Births, Deaths (unsplit)                                          READ   (Vitals)
             Inflow, Outflow                                                   READ   (MigrationFlows)
             ColonistsDeparted = Opening + Births − Deaths + Inflow − Outflow − Closing
                                                                               RESIDUAL — absorbs ONLY colonization;
                                                                               asserted 0 on every turn no settlement was founded
             ClassCounts[] per class                                           SUMMED
  FOOD       GrainOpening, GrainClosing                                        READ   (GoodStocks grain Amount)
             Harvest      = grain.LastProducedUnits                            READ
             Eaten        = grain.LastConsumptionEatenUnits                    READ
             StoreLosses  = Opening + Harvest − Eaten − Closing               RESIDUAL — absorbs spoilage + granary
                                                                               overflow (unsplit per settlement) AND any
                                                                               appropriation transfer AND colony provisions;
                                                                               Σ over settlements == Δledger(Spoilage) +
                                                                               Δledger(Overflow) on any turn with no
                                                                               founding and no raid — asserted
             DemandUnits, DeficitRatio                                         READ   (ConsumptionDeficits)
             FoodGoods[] (grain, livestock, fish): Produced, Demand, Eaten     READ
             FoodObtained = Σ FoodGoods.Eaten
  HOUSING    Dwellings (open/close), Capacity = Dwellings × PersonsPerDwelling,
             Need = Population / PersonsPerDwelling, Sufficiency               RECOMPUTED (SettlementHappiness.HousingSufficiency)
             LastMaintenanceFraction, LastLaborUsed                            READ
             (Built / Decayed split: GAP — only Δ is known)
  ECONOMY    Goods[] per good: Stock, Produced, InputDemand, ConsumptionDemand, Eaten, Price   READ
             TradeIn[] / TradeOut[] per good                                   READ   (TradeFlows)
             SectorShares[5] in force this step                                RECOMPUTED (Sectors.Share on prev SectorAllocations)
             Variables: food_surplus_ratio, artisan_share, trade_volume        READ
             ClassActive[] latches                                             READ
  SOCIAL     Happiness, HappinessFactors[Food, Housing]                        RECOMPUTED (SettlementHappiness.Of / Factors on next)
             Grievance[] per class, NeedSatisfaction[] per class per need      READ
             (the §5 explanation is a separate on-demand QUERY over the same state, not a
              stored field — it is derivable from the rows above, so storing it would be
              a second copy free to drift)
  MIGRATION  Push: DeficitRatio (famine flight driver)                         READ
             Pull: SmoothedAttractiveness (this and every other settlement)    READ
             GrainPresent (viability gate input), UnplacedRemainder            READ
             (pairwise From→To flows, damping, viability, gap scale: GAP)
  POLICY     SectorShares (above); DeclaredWeights (last SectorAllocation batch targeting this
             settlement, or Sectors.Default if never ordered)                  READ (order log)
  ORDERS     Orders[] applied this step targeting this settlement: Index, Actor, Kind, Sector, Amount   READ
```

---

## §4 POLICY HISTORY

M4 has exactly one player policy: the five-sector labour allocation per
settlement (D-032). The history is derived from two authoritative sources and
nothing else: the **order log** (what was declared, by whom, when — every record
carries `Turn`, `ActorId`, and its index in the log is its order number) and the
per-turn **`SectorAllocationRow`** (what was in force).

```
PolicyChange { Turn, Year, Settlement, Sector, OldWeight, NewWeight, Actor, OrderIndex }
PolicyState  { Turn, Settlement, Declared[5], Effective[5] }   one per settlement per turn
```

A turn with no change still gets a `PolicyState`, so the player can read what
was in force on any turn without inferring it from the last change. The
"declared 10% → 15%, changed by player, order #104" reading the packet asks for
is `PolicyChange` verbatim. When M5's taxation lands, it is a second policy in
the same two structures — nothing here is sector-specific except the array width.

**Consequences (B5)** are shown as the settlement's record on the turns after a
change — what actually happened — and labelled as observation, never as
attribution: the simulation does not carry a counterfactual, and the record
must not pretend to.

---

## §5 GRIEVANCE: THE COMPONENTS, THE PRIMARY, THE CAUSE, THE LEVER

**What grievance actually is** (NeedsGrievanceSystem, read in full): per
(settlement, class), a persistent stock `G` updated as
`G' = max(0, G + W·max(0, 1 − S)·dt − decay·G·dt)`, where `S` is a CES aggregate
(σ = 0.5, weighted harmonic mean) over the class's bound needs' satisfactions
after the Tier-A gate reweights them, and `W` is the sum of bound weights. The
inputs `s_n` are stored (`NeedSatisfactionRow`); `S`, `W`, the accrual and the
decay are transient.

**The components are the needs.** In shipped data three are bound: Sustenance
(1, w 1.0), Shelter (2, w 0.9), Comfort (6, w 0.3). Five more exist unbound and
contribute exactly nothing; they are listed as `not yet simulated` rather than
omitted, so the player can see the registry is bigger than the model.

**The decomposition is observer-defined, and says so.** `W·(1−S)` is not additive
over needs (CES), so no per-need contribution is a simulation quantity. The
record carries, per bound need:

- `Satisfaction s_n` — READ.
- `WeightedShortfall = w_n·(1 − s_n)` — READ × config, the plain reading of
  "how unmet, how much it matters".
- `MarginalLift = S(s with s_n := 1) − S` — RECOMPUTED through the public
  `NeedsAggregation.ApplyTierAGate` + `Aggregate`: how much the aggregate would
  rise if this need alone were fully met. This is the attribution the panel
  ranks by, because it respects the gate (a starving class's Comfort shortfall
  is correctly near-worthless).

**Primary grievance = argmax MarginalLift, ties broken by LOWEST need id** —
a composite key `(lift DESC, needId ASC)`, the convention Appropriation uses
(`stock DESC, id ASC`), with a tie-dense test in which every bound need is
equally unmet. Never table order, never a double-only compare.

**Accrual versus decay** for the turn's ΔG is RECOMPUTED from prev state and
config through the same public functions; the Tier-A gate need ids are a private
constant on the system and are exposed as a public static readonly (a
visibility change, zero behaviour) so the observer cannot drift from it.

**The cause behind each component, one link at a time, each verified in source:**

| need | satisfaction ← | ← | lever (existing, M4) |
| --- | --- | --- | --- |
| Sustenance | fill ratio per food good = Eaten/Demand (prev GoodStockRow), × variety factor | grain harvest = min(arable × yield, farm labour × output × tools) × weather; livestock/fish = herding labour × abundance × weather | **farming share, herding share** (POLICY sliders). Imports: none — grain never trades (GAP: no player trade lever) |
| Shelter | dwellings × 6 / population (prev Housing, Buckets) | built = min(deficit, construction labour, timber, clay); decayed by unmet upkeep | **construction share**; timber via **extraction share** |
| Comfort | fill of pottery and cloth | crafting labour split equally across available recipes; inputs clay, timber, fiber from extraction | **crafting share, extraction share** |

Every input named in the middle column is READ from a stored row
(`CatchmentSummaryRow.EffectiveArableKm2`, `HarvestWeatherRow.Multiplier`, tools
stock, sector shares, `HousingRow.LastLaborUsed`, per-good stocks and
`LastInputDemandUnits`). The chain therefore ends at the deepest **stored**
input, which is what the packet asks for; where the next link is transient (the
tool factor, the per-recipe labour cap) it is a GAP and the panel says
"computed inside production, not recorded".

**When no lever reaches a cause the record says so.** Weather, arable land,
deposit abundance and terrain have no M4 lever; the explanation names them as
conditions, not recommendations. No mechanic is invented to make a tooltip
actionable.

---

## §6 HAPPINESS AND MIGRATION EXPLANATIONS

**Happiness** is `SettlementHappiness.Of` (RECOMPUTED, the public reader) with
its two factors Food = 1 − DeficitRatio and Housing = dwellings×6/pop. The
explanation reuses §5's Sustenance and Shelter chains. It is stated plainly
that happiness omits Comfort and applies no Tier-A gate — it is deliberately
not the same number as the needs aggregate.

**Migration** exposes what is stored: outflow and inflow; the push driver
(source deficit → famine flight, gap-independent); the pull signal (smoothed
attractiveness, listed for this settlement and every other so the gap the
mechanism responds to is visible); the viability inputs (destination deficit,
grain presence, happiness). The pairwise realised flows, damping matrix and
viability products are computed transiently and discarded — a **GAP**, recorded
in §8, not recomputed here even though it would be possible, because a
recomputation is a second implementation of MigrationSystem and the day the
system changes it would silently lie.

---

## §7 STORAGE AND RECONSTRUCTION

**Everything above is a pure function of `(prev, next, cfg, orders)`.** That is
the whole storage strategy: records are never a source of truth, so they can
always be rebuilt by replaying the order log through the same observer, and
`sim inspect --telemetry` does exactly that headlessly.

**In a played session:** `ObservationLog` holds every `TurnRecord` and every
`SettlementRecord` in memory, and `UiSession` appends one JSONL line per turn to
`runs\telemetry-<stamp>.jsonl` beside the four T4.17 files (streaming append;
doubles in "R" format, InvariantCulture; deterministic — asserted byte-identical
across two runs of the same session).

**Cost, measured on the shipped roster:** a `SettlementRecord` is ~60 scalar
fields plus 14 goods × 6 fields, ≈ 1.2 KB; 12 settlements × 1,730 turns (a
6,000-year campaign at dt 10) ≈ 25 MB in memory, ~40 MB JSONL. Trivial today.
**Where it stops being trivial:** colonization can take the roster to 100+
settlements (T4.4 measured 178 on a defective branch), which is ~200 MB in
memory over a campaign. The design already has the answer — a bounded in-memory
window (last N turns, N = 200 by default) plus the JSONL on disk plus
reconstruction by replay for anything older — and the UI reads through one
`IObservationHistory` so the window/disk/replay split is invisible to it. The
window is NOT implemented in T4.19 because nothing in M4 needs it; the seam is.

---

## §8 OBSERVABILITY GAPS — WHAT THE SIMULATION DOES NOT RECORD

Each of these needs the OWNING SYSTEM to write one more field or row. None is
faked; each appears in the UI as `not recorded` where it would otherwise sit.

1. **Per-settlement natural vs starvation deaths** — `SettlementVitalsRow.Deaths`
   sums both `SinkExact` calls (DemographicsSystem.cs:265-266). One extra long.
2. **Per-settlement spoilage and granary overflow** — computed per settlement in
   `ConsumptionSystem.BoundStore`, recorded only as world ledger totals. Two
   longs on `GoodStockRow` or a `StoreLossRow`.
3. **Colonization departures** — party and provisions leave the source by
   `Ledger.Transfer` and are recorded nowhere; the colony carries no source id.
4. **Appropriation raids** — grain moves victim → raider unrecorded;
   `AppropriationSystem` owns no table.
5. **Pairwise migration** — only per-settlement totals; the From→To matrix,
   damping, viability and gap scale are transient.
6. **Housing built vs decayed** — only Δdwellings and `LastMaintenanceFraction`.
7. **Per-good sinks per settlement** — InputsConsumed, ToolWear,
   HousingMaterials, ConstructionMaterials have no `Last*` field.
8. **Per-bucket vitals** — births and deaths by cohort/class are settlement
   totals only.
9. **The transient production factors** — tool factor, per-recipe labour cap,
   arable-versus-labour binding constraint — are computed and discarded.
10. **Grievance accrual by need** — not a simulation quantity (CES); the panel's
    attribution is observer-defined and labelled.
11. **Revolt** — leaves only the removal of a `ControlRow`.
12. **Player trade lever** — grain never trades and trade is autonomous; the
    Sustenance chain's "imports" branch has no lever in M4.

---

## §9 WHAT THIS RECORD DOES NOT AUTHORISE

No simulation constant moves for observability. No system reads any record.
No record is serialized into `WorldState` or the canonical schema. No UI
component computes a simulation quantity from a private formula. Where §5 and §6
recompute, they call public static functions the simulation itself calls, so a
change to the simulation changes the explanation with it.
