# OBSERVABILITY GAPS — what the simulation does not record, and what would close each

T4.19 built the glass box on what the simulation already writes. Where it writes
nothing, the record says `not recorded` and this list says which system would
have to write one more field. **Nothing here was faked in the UI.** Each gap is
cited to the producing system at the T4.19 tree; each has the owning system, the
missing datum, and what the player currently sees instead.

| # | gap | owner | what is missing | what the player sees today | what closes it |
| --- | --- | --- | --- | --- | --- |
| 1 | per-settlement natural vs starvation deaths | `DemographicsSystem` (:265-266) | `SettlementVitalsRow.Deaths` sums both `SinkExact` calls | SETTLEMENT/Population: "deaths N (UNSPLIT: natural + starvation)"; world split is exact from the ledger | one extra `long` on `SettlementVitalsRow` |
| 2 | per-settlement granary CAPACITY, spoilage and overflow | `ConsumptionSystem.BoundStore` (:296-323) | all three computed per settlement, recorded only as world ledger totals (capacity not at all) | SETTLEMENT/Food: "store losses" residual with its identity string, Σ equals the ledger, asserted — **and since T4.20 an explicit "granary capacity / spoilage / overflow, per settlement: not recorded" line naming the §0 reason** | two `long`s on the grain `GoodStockRow` or a `StoreLossRow`; OR making `BoundStore`'s capacity and split PUBLIC STATICS so the observer can RECOMPUTE through the simulation's own code (`docs/t4.20-food-semantics.md` Phase 5 — NOT AUTHORIZED, queued for ruling) |
| 3 | colonization departures per source | `ColonizationSystem` (:294-296, :398-400) | party and provisions leave by `Ledger.Transfer`, recorded nowhere; the colony carries no source id | "colonists departed" residual (exact per source when one founding per step); founding-turn record | a `FoundingRow(Source, Colony, Party, Provisions)` |
| 4 | appropriation raids | `AppropriationSystem` (:161-163) | grain victim → raider unrecorded; the system owns no table | detected only when a settlement's food identity goes negative; the turn is labelled `UnattributedGrainTransfer`, never asserted absent | a `RaidRow(Raider, Victim, Units)` |
| 5 | pairwise migration | `MigrationSystem` (:409-474) | From→To flows, damping, viability products, gap scale are transient | SETTLEMENT/Migration: totals, push (deficit), pull (every settlement's smoothed attractiveness sorted), viability inputs; pairwise lines read "not recorded" | a per-step `MigrationPairRow(From, To, Moved)` |
| 6 | housing built vs decayed | `HousingSystem` (:130-140, :170-179) | only Δdwellings and `LastMaintenanceFraction` per settlement | SETTLEMENT/Food housing block: Δ and maintenance fraction; "built / decayed: not recorded" | two `long`s on `HousingRow` |
| 7 | per-good sinks per settlement | `ProductionSystem`, `HousingSystem`, `ConstructionSystem` | InputsConsumed, ToolWear, HousingMaterials, ConstructionMaterials have no `Last*` field | ECONOMY: world `GoodAccount` exact from the ledger; per settlement only stock/produced/eaten/demands | `Last*Units` fields on `GoodStockRow` |
| 8 | per-bucket vitals | `DemographicsSystem` | births/deaths are settlement totals | cohort counts only | per-cohort vitals row |
| 9 | transient production factors | `ProductionSystem` (:221-225, :369, :412-413) | tool factor, land-vs-labour binding, per-recipe labour cap | chain links "not recorded: computed inside production" | publish the binding constraint per sector per turn |
| 10 | grievance accrual by need | `NeedsGrievanceSystem` | CES aggregate is non-additive; no per-need accrual exists | marginal lift and weighted shortfall, labelled observer-defined | a director ruling on an attribution rule, or none |
| 11 | revolt | `RevoltSystem` | leaves only the removal of a `ControlRow` | TURN: "control lost N" | a `RevoltRow(Settlement, Polity, Turn)` |
| 12 | player trade lever | (design) | grain never trades; trade is autonomous | chain: "grain imports — condition, no lever" | M5+ policy |
| 13 | policy consequence attribution | (design) | the simulation carries no counterfactual | POLICY/HISTORY: "observed on the turns after, not attributed" | a counterfactual replay from the change turn — deterministic, so feasible; a later packet |

Closed during T4.19 rather than recorded: the observer's one-line copy of
`MigrationSystem`'s grain-presence predicate (dropped — the two READ inputs stay);
the observer's copies of `NeedsGrievanceSystem`'s turnover, decay, accrual, Euler
step and `Fill` (the system's arithmetic became callable public pure functions;
zero behaviour change, hash-verified).

Storage: `docs/observability-architecture.md` §7 — ~1.2 KB per settlement-turn,
~25 MB per 6,000-year campaign at today's roster, JSONL beside the session,
everything reconstructible by replay; the bounded window + disk + replay seam is
designed and unimplemented because M4 does not need it.
