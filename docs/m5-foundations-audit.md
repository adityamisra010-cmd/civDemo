# M5 FOUNDATIONS AUDIT — T5.1

**S8 §4.1 requirement 1: the foundations audit is PACKET ONE of every milestone
from M4 onward.** This is M5's.

**Scope is DEPENDENCY, not perturbation, and it is ENUMERATED FROM CODE.** Every
row below is a quantity M5's proposed systems — taxation, authority/state
capacity, legitimacy/opinion, laws-lite — would have to *consume*, followed to its
producer. Nothing here is asserted from memory; every unit is read off the
producing expression and cited, per §4.1's anti-self-certification rule.

**Audited against `dbef61a` (the certified M4 baseline).** Nothing was
implemented, and no mechanic, formula, schema, test, golden, corridor or
quarantine was touched.

---

> **RULED ON — READ THIS FIRST (added after the director's unblocking packet).**
> §0's headline finding asked where an in-kind tax's grain goes. The director
> rejected the premise: taxation is a POLICY acting on an economic FLOW, not a
> transfer of stored goods, so there is no recipient endpoint to be missing. The
> audit's dependency table below is unaffected and was used as written; only the
> headline blocker is void. See `docs/adr/cr-008-…` (now CLOSED) and
> `docs/m5-implementation-inventory.md`.

## §0 THE HEADLINE FINDING

**M5's central mechanism cannot be specified from the current tree, and the reason
is structural rather than a missing number.**

An in-kind tax is a `Ledger.Transfer`, which requires **two** endpoints of the
same conserved quantity. The payer endpoint exists and is well-formed. **The
recipient endpoint does not exist at all**: no treasury, no polity-held stock, no
state-held quantity. `PolityRow` and `CapitalRow` carry no quantities.

That is written up as **CR-008**, with three options and a recommendation, and it
is a director call. **Everything downstream of extraction — state capacity,
enforcement, service provision, and therefore the whole causal governing loop —
is blocked behind that one ruling.** This audit records what the loop would stand
on once it is made.

---

## §1 THE AUDIT TABLE

Per §4.1, four answers per quantity: **(a)** real units and physical possibility ·
**(b)** denomination vs consumption site · **(c)** derived or merely chosen ·
**(d)** visible to tuning.

### 1.1 The tax base — what extraction could actually draw on

| quantity | producer | (a) units | (b) denomination | (c) derived? | (d) tuning |
| --- | --- | --- | --- | --- | --- |
| `GoodStockRow.Amount` | Production / Consumption / Trade, via Ledger | **whole units of one good**, conserved `long` (`WorldState.cs:216-245`) | per **(settlement, good)** — matches an in-kind tax's payer site exactly | n/a — a stock, not a constant | no |
| `GoodStockRow.LastProducedUnits` | `ProductionSystem` | whole units produced **last turn** | per (settlement, good) | n/a | no |
| `GoodStockRow.LastConsumptionEatenUnits` | `ConsumptionSystem` | whole units eaten last turn | per (settlement, good) | n/a | no |
| `PriceRow.Price` | `PriceSystem` | **price in GRAIN**, grain pinned at exactly 1.0 (`PriceSystem.cs:110-118`) | per (settlement, good) | derived each turn | no |
| `TradeFlowRow.Quantity` | `TradeArbitrageSystem` | whole units moved | per (from, to, good) | n/a | no |
| bucket `Count` | Demographics / Migration | **people**, conserved `long` | per (settlement, culture, religion, class, cohort) | n/a | no |

**Finding 1.1-A — a value-denominated tax is available but is a trap.** Because
grain is pinned at 1.0 everywhere, `Σ quantity × price` is a well-formed
grain-denominated valuation of any basket. **That is money in all but name.** A
"tax of 10% of the value of production" would introduce a universal unit of
account through the back door while the operative ruling says M5 taxes IN KIND and
ships no currency. **Denomination discipline:** an in-kind tax must be expressed
as *units of specific goods*, never as a value aggregate.

**Finding 1.1-B — the base must be a FLOW, not the stock.** Taxing
`GoodStockRow.Amount` taxes accumulated wealth and interacts with T4.2's granary
capacity ceiling: a settlement at its cap has already spoiled its surplus, so a
stock tax would fall hardest on settlements that stored nothing. `LastProducedUnits`
is the honest harvest-tithe base and is already per (settlement, good).

### 1.2 Administrative reach — what authority could be computed from

| quantity | producer | (a) units | (b) denomination | (c) derived? | (d) tuning |
| --- | --- | --- | --- | --- | --- |
| `SettlementDistanceRow.TravelCost` | `CatchmentSystem` / traversal lattice | **travel cost** (abstract cost units, river-aware since T4.7) | per (from, to) — exactly the (capital → settlement) shape a reach term needs | derived from terrain + `riverCostFactor` | `transport.riverCostFactor` is tunable |
| `ControlRow.Strength` | `WorldFounding` only | dimensionless in [0,1] | per (polity, place) | **NEITHER — written as the literal 1.0 and never computed or decayed** (`WorldFounding.cs:263`) | no |
| `CapitalRow.Place` | `WorldFounding` only | — | per polity | n/a | no |
| settlement count per polity | derived: `EmpireQuery.ControlledCount` | count | per polity | derived | no |

**Finding 1.2-A — `ControlRow.Strength` is T4.3's reserved slot and is the natural
carrier for administrative reach, but it is currently a written constant.** It is
the one field in the tree whose *stated purpose* matches D-040 C3's ruling that
**control carries a distance term over the network graph, travel cost not
Euclidean**. M5 would be its first computer. **This is a (c) failure by §4.1's
definition — "chosen, never derived" — and it is recorded as such rather than
quietly fixed, because computing it is a mechanism and mechanisms need a spec.**

**Finding 1.2-B — the distance denominator is available and correct.** A reach
term needs (capital → settlement) travel cost; `SettlementDistanceRow` supplies
exactly that pair shape, already river-aware. No new geometry is required.

### 1.3 Legitimacy / opinion inputs — and the D-021 wall

| quantity | producer | (a) units | (b) denomination | (c) derived? | (d) tuning |
| --- | --- | --- | --- | --- | --- |
| `GrievanceRow.Value` | `NeedsGrievanceSystem` | dimensionless stock, **not conserved**, accrues on deprivation and decays | per (settlement, class) | derived | `needs.grievance` tuning block |
| `NeedSatisfactionRow.Value` | `NeedsGrievanceSystem` | satisfaction in **[0,1]** | per (settlement, class, need) | derived | needs weights |
| `SettlementHappiness` (M4) | **derived query, not a row** | **0..100**, not a stock, not serialized | per settlement | derived | `migration.attractivenessHappinessWeight` consumes it |
| `ConsumptionDeficitRow.DeficitRatio` | `ConsumptionSystem` | ratio in [0,1] | per settlement | derived | — |

**Finding 1.3-A — THIS IS THE SECOND BLOCKER, and it is a governance one.**
`scripts/check-read-isolation.sh` forbids any sim-side reference to `Grievances`
or `NeedSatisfactions` outside a path allowlist, enforcing D-021's rule that
**grievance drives no behaviour until M5**. M5 is that milestone — so the gate is
*expected* to change here. But D-021's binding condition is that **"the brakes
install with the gas pedal, never after"**: the unrest valves ship in the same
packet as the unrest. **M5 may not read grievance into behaviour until it also
ships the valves D-021 names.** That is a packet-scoping constraint on M5, not a
number, and it must be settled before any legitimacy mechanism is written.

**Finding 1.3-B — Happiness and legitimacy must not be collapsed.** M4's
`SettlementHappiness` is *material provision*, derived and unserialized. Opinion of
a **government** is a different subject with different inputs (extraction burden,
enforcement, service provision). The M4 ruling protects Happiness's shape
explicitly. Recorded so a future implementer does not "simplify" them into one
mood score.

### 1.4 Constants M5 would inherit

| constant | value | (c) derived? | (d) tuning | note |
| --- | --- | --- | --- | --- |
| `consumption.granaryYearsOfDemand` | 1.5 | **DERIVED**, reference class stated | yes | caps stored surplus; interacts with any stock-based tax (1.1-B) |
| `consumption.grainSpoilagePerYear` | 0.08 | **DERIVED** | yes | per-sim-year, dt-integrated |
| `transport.riverCostFactor` | 0.2 | derived at T4.7 | yes | the reach denominator's driver |
| `migration.attractivenessHappinessWeight` | 0.15 | **CHOSEN within a stated frame** | yes | M4's only happiness consumer |
| `housing.personsPerDwelling` | 6.0 | **DERIVED** (band 5–8) | yes | happiness housing factor |
| needs `aggregation.sigma` | 0.5 | derived from D-035-B | yes | non-compensatory CES |
| needs `satisfactionFloor` | 0.05 | mechanism parameter, stated | yes | anchors the happiness scale |

**No impossible magnitude found under (a).** No constant M5 would consume is
physically absurd, and none is denominated against the wrong consumption site
except the two findings recorded above.

---

## §2 DISPOSITIONS (per S8 §4.1)

| finding | §4.1 class | disposition |
| --- | --- | --- |
| 1.1-A value-denominated tax is money by the back door | (b) denomination hazard | **Denomination rule recorded**: in-kind tax is units of goods, never a value aggregate |
| 1.1-B stock vs flow base | (b) | **Recorded**: the base is a flow (`LastProducedUnits`), not the stock |
| **no recipient endpoint for a Transfer** | **(b) mismatch → CR** | **CR-008 raised. BLOCKS M5's core mechanism.** |
| 1.2-A `ControlRow.Strength` chosen, never derived | **(c) fails, (a)(b)(d) clean** | **Recorded as "chosen, never derived"**; computing it is M5 mechanism work, specced not smuggled |
| 1.3-A D-021 brakes-with-gas-pedal | **governance scope** | **Blocks legitimacy work until M5's valve set is ruled.** Recorded, not resolved |
| 1.3-B happiness ≠ legitimacy | (b) | **Recorded**: separate subjects, separate quantities |

**Two blockers, both director-owned: CR-008 (tax endpoint) and D-021's valve
scoping (1.3-A). Neither is an engineering problem and neither is taken here.**

---

## §3 WHAT THIS AUDIT DID NOT DO

It did not measure a bind ratio or run a sweep, because M5's equations do not
exist yet — §4.1 requires the audit to enumerate *dependencies from code*, which is
what this is. Once CR-008 and 1.3-A are ruled, the M5 spec's §3 must carry its own
dimensional declaration for every new quantity it introduces, and this table is
the input to it.
