using Sim.Core.State;

namespace Sim.Core.Observability;

/// <summary>
/// T4.19 — THE SETTLEMENT RECORD (docs/observability-architecture.md §3): one
/// per settlement present in <c>prev.Settlements</c>, per step, plus a FOUNDING
/// record for any settlement present in <c>next</c> but not in <c>prev</c>.
///
/// THERE IS NO SETTLEMENT DIMENSION ON THE LEDGER (§1), so the per-settlement
/// flows are READ from the rows the owning systems wrote this step — Vitals,
/// MigrationFlows, the GoodStockRow <c>Last*</c> fields, ConsumptionDeficits —
/// and the two quantities no row records are RESIDUALS of stated identities.
/// Each residual carries its identity as a string, names what it absorbs, and
/// is cross-checked exactly against the world ledger by the tests in
/// Sim.Tests/Observability. A residual is honest only while everything else in
/// its identity is READ or SUMMED; nothing in it is derived from a formula.
///
/// THE FOUNDING RECORD is the same shape with <see cref="Founded"/> set: the
/// settlement did not exist in prev, so every Opening is 0, no system wrote a
/// row for it this step (they iterate prev.Settlements), and the two residuals
/// come out NEGATIVE — the party and the provisions ARRIVED. That sign is the
/// point: Σ ColonistsDeparted over ALL records (prev settlements and founded
/// ones) is exactly 0 on every turn, founding or not, because a transfer is
/// zero-sum, and Σ StoreLosses over all records equals the ledger's spoilage +
/// overflow on every turn for the same reason. The per-settlement split of a
/// transfer is what the simulation does not record (§8 gaps 3, 4).
/// </summary>
public sealed record SettlementRecord(
    int Settlement,                   // READ  SettlementRow.Id
    long FoundedTurn,                 // READ  SettlementRow.FoundedTurn
    int Controller,                   // READ  ControlRow.Polity on next; −1 when none (EmpireQuery.TryGetController)
    bool Founded,                     // this settlement is new in next (founding record)
    PopulationSection Population,
    FoodSection Food,
    HousingSection Housing,
    EconomySection Economy,
    SocialSection Social,
    MigrationSection Migration,
    PolicySection Policy,
    OrderApplied[] Orders,            // READ  the step's orders whose decoded Settlement is this one
    // T4.21-5 (spec §3.11) — APPENDED, so every field above keeps its position.
    // Both sections are read-only and non-authoritative: every number in them is
    // READ from a prev/next row or RECOMPUTED by calling a PUBLIC static of the
    // simulation on the SAME state (FoodState, FoodHeadroom, MigrationSystem.Plan,
    // DemographicsSystem.Headroom). Nothing here is a second implementation and
    // nothing here is serialized into WorldState.
    FoodStateSection FoodState,
    MigrationPlanSection MigrationPlan);

public sealed record PopulationSection(
    long Opening,                     // SUMMED prev Buckets.Count + prev Notables.Count for this settlement
    long Closing,                     // SUMMED next Buckets.Count + next Notables.Count
    long Children, long Adults, long Elders,   // SUMMED BandViews over next Buckets
    long Notables,                    // SUMMED next Notables.Count (0 in the shipped pipeline — NotableLifecycle is never called)
    long Births,                      // READ  SettlementVitalsRow.Births (next)
    long Deaths,                      // READ  SettlementVitalsRow.Deaths (next) — natural + starvation, UNSPLIT (§8 gap 1)
    long Inflow,                      // READ  MigrationFlowRow.Inflow (next)
    long Outflow,                     // READ  MigrationFlowRow.Outflow (next)
    long ColonistsDeparted,           // RESIDUAL — see Identity
    string ColonistsDepartedIdentity, // the identity and what it absorbs, verbatim
    ClassCount[] ClassCounts);        // SUMMED next Buckets per class, registry order

public readonly record struct ClassCount(int Class, string Name, long Count);

public sealed record FoodSection(
    long GrainOpening,                // READ  prev grain GoodStockRow.Amount
    long GrainClosing,                // READ  next grain GoodStockRow.Amount
    long Harvest,                     // READ  next grain LastProducedUnits (the Harvest source, ProductionSystem.cs:244-247)
    long Eaten,                       // READ  next grain LastConsumptionEatenUnits (the Eaten sink, ConsumptionSystem.Consume)
    long StoreLosses,                 // RESIDUAL — see Identity
    string StoreLossesIdentity,
    long DemandUnits,                 // READ  ConsumptionDeficitRow.DemandUnits (next)
    double DeficitRatio,              // READ  ConsumptionDeficitRow.DeficitRatio (next)
    // T4.20: the good set is BasketBook.FoodGoods — the goods carrying a
    // Sustenance basket line, ascending by good id — which is THE SIMULATION'S
    // OWN rule (ConsumptionSystem.cs:328-333, ClassMobilitySystem.cs:131-135),
    // reached through the sanctioned shared pure reader rather than restated.
    // It is NOT goods.json's "category":"food" string: that is a second rule
    // which merely agrees on shipped data.
    FoodGood[] FoodGoods,             // READ  per good of BasketBook.FoodGoods, ascending by good id
    long FoodObtained,                // SUMMED FoodGoods.Eaten
    // The two food-legibility derivations. Both are per-TURN totals in
    // person-year-equivalents of nutrition — the unit every Sustenance basket
    // line is denominated in — which is what makes a sum across goods, and a
    // subtraction against the requirement, dimensionally sound.
    long FoodProduced,                // SUMMED FoodGoods[].Produced over BasketBook.FoodGoods —
                                      //        the SAME span ClassMobilitySystem sums, by construction
    long FoodBalance);                // DIFFERENCED (cross-sectional, same turn):
                                      //        FoodProduced - DemandUnits, both READ/SUMMED longs of
                                      //        the SAME turn. §0's DIFFERENCED is worded as next-minus-prev
                                      //        (a temporal difference); this is the same kind of exact
                                      //        integer remainder taken across two terms instead of two
                                      //        turns. Flagged in docs/queue.md for a §0 wording ruling.

public readonly record struct FoodGood(int Good, string Name, long Produced, long Demand, long Eaten);

/// <summary>Housing. The Built/Decayed split per settlement is a GAP (§8 gap 6):
/// only Δdwellings is known, and it is what <see cref="DwellingsOpening"/> /
/// <see cref="DwellingsClosing"/> show. A settlement with no HousingRow (a colony
/// before its first HousingSystem turn) reads <see cref="HasRow"/> false and 0.</summary>
public sealed record HousingSection(
    bool HasRow,
    long DwellingsOpening,            // READ  prev HousingRow.Dwellings
    long DwellingsClosing,            // READ  next HousingRow.Dwellings
    double Capacity,                  // DwellingsClosing × cfg.Housing.PersonsPerDwelling (READ × config)
    double Need,                      // Population.Closing / cfg.Housing.PersonsPerDwelling (SUMMED ÷ config)
    double Sufficiency,               // RECOMPUTED SettlementHappiness.HousingSufficiency(next)
    double LastMaintenanceFraction,   // READ  next HousingRow
    double LastLaborUsed,             // READ  next HousingRow
    string BuiltDecayedSplit);        // "GAP: ..." — never a number

public sealed record EconomySection(
    GoodReading[] Goods,              // READ  per good, registry order (next GoodStocks + Prices)
    TradeLeg[] TradeIn,               // READ  next TradeFlows with To == this
    TradeLeg[] TradeOut,              // READ  next TradeFlows with From == this
    double[] SectorShares,            // RECOMPUTED Sectors.Share on the PREV row (in force this step); Default when no row
    bool SectorRowPresent,            // whether prev carried a SectorAllocationRow for this settlement
    double FoodSurplusRatio,          // READ  VariableRow(Variables.FoodSurplusRatio) on next; NaN when absent
    double ArtisanShare,              // READ  VariableRow(Variables.ArtisanShare) on next; NaN when absent
    double TradeVolume,               // READ  VariableRow(Variables.TradeVolume) on next; NaN when absent
    ClassActive[] ClassActive);       // READ  next ClassStateRow per class, registry order

public readonly record struct GoodReading(
    int Good, string Name, long Stock, long Produced, long InputDemand,
    long ConsumptionDemand, long Eaten, double Price);   // Price NaN when no PriceRow yet

public readonly record struct TradeLeg(int Other, int Good, string Name, long Quantity);

public readonly record struct ClassActive(int Class, string Name, int Active);

/// <summary>Social readings. Happiness is <c>SettlementHappiness.Of</c> on NEXT —
/// the public reader, recomputed so a change to its formula changes this record
/// with it. Grievance and satisfaction are READ from the needs system's own rows
/// on next. The §5 explanation is an on-demand query over the same rows (lane A2)
/// and is deliberately NOT stored here — a stored copy is a second one, free to
/// drift.</summary>
public sealed record SocialSection(
    double Happiness,                 // RECOMPUTED SettlementHappiness.Of(next)
    double[] HappinessFactors,        // RECOMPUTED SettlementHappiness.Factors(next): [Food, Housing]
    GrievanceReading[] Grievance,     // READ  next GrievanceRow per class, table order
    NeedReading[] NeedSatisfaction);  // READ  next NeedSatisfactionRow per (class, need), table order

public readonly record struct GrievanceReading(int Class, string Name, double Value);

public readonly record struct NeedReading(int Class, int NeedId, string NeedName, double Value);

/// <summary>
/// What MigrationSystem READ this step, so the flows above can be seen beside
/// their inputs. Migration reads PREV (§3.2 one-turn lag), so every input here is
/// the prev value: the push driver (source deficit → famine flight), the pull
/// signal (smoothed attractiveness, listed for this and every other settlement
/// so the gap the mechanism responds to is visible), and the ADR-012 absolute
/// food gate's two inputs. The pairwise From→To matrix, damping, viability and
/// gap scale are transient — a GAP (§8 gap 5), stated, not recomputed.
/// </summary>
public sealed record MigrationSection(
    double PushDeficitRatio,          // READ  prev ConsumptionDeficitRow.DeficitRatio (0 when no row)
    double PullAttractiveness,        // READ  prev SmoothedAttractivenessRow.Value (NaN when no row)
    AttractivenessReading[] AllAttractiveness,   // READ  prev SmoothedAttractiveness, every settlement, table order
    long PrevGrainStock,              // READ  prev grain Amount — one input of the anyFood gate (MigrationSystem.cs:173-174)
    long PrevGrainHarvest,            // READ  prev grain LastProducedUnits — the other input.
                                      // The gate's PREDICATE on these two (store > 0 || harvest > 0) is
                                      // deliberately NOT recorded: it is MigrationSystem's private
                                      // arithmetic, and copying it here would be a sixth field kind
                                      // §0 does not permit - the day the gate gains a term, a copy lies.
                                      // The two READ inputs are what the record can honestly carry.
    double UnplacedDeparture,         // SUMMED next Buckets.UnplacedDeparture — demand migration could not place (feeds colonization)
    double UnplacedRemainder,         // SUMMED next Buckets.UnplacedRemainder
    string PairwiseFlows);            // "GAP: ..." — never a matrix

public readonly record struct AttractivenessReading(int Settlement, double Value);

/// <summary>Policy: the raw weights DECLARED (the SectorAllocationRow in next, or
/// Sectors.Default when the settlement was never ordered — PathBuildSystem.Upsert
/// writes exactly order.Amount / 100 into the row, PathBuildSystem.cs:83-103, so
/// the row IS the last declaration) beside the shares in force this step.</summary>
public sealed record PolicySection(
    double[] DeclaredWeights,         // READ  next SectorAllocationRow raw weights; Sectors.Default raw when no row
    bool DeclaredRowPresent,
    double[] EffectiveShares);        // RECOMPUTED Sectors.Share on the prev row — same array as Economy.SectorShares

/// <summary>
/// T4.21-5 (spec §3.11, CR-015 mandate item 9) — THE EXCEPTIONAL-FAMINE
/// READOUT, per settlement per step. Read-only and non-authoritative: the
/// classification here is not consulted by any system, it is the SAME static
/// the kernel consults, called on the SAME world.
///
/// EVERY FIELD IS PREV-SIDE, and that is the point. <c>FoodState.Of</c> is the
/// state IN FORCE for the step just observed — the deficit the step read, the
/// disaster multiplier the step's harvest was multiplied by, the sector row the
/// step's production obeyed. A NEXT-side classification would name the state of
/// the step that has not run yet. The one deliberate exception is the PENDING
/// disaster block, which is next-side and named so.
///
/// A FOUNDING RECORD (present in next, absent from prev) has no prev rows at
/// all: <see cref="PrevRowsPresent"/> is false, the classification reads the
/// same absences every system reads (deficit 0, no strike, Sectors.Default ⇒
/// not abandoned ⇒ Normal), and the headroom readings take FoodHeadroom's own
/// null arm (+∞). Nothing is invented for it.
/// </summary>
public sealed record FoodStateSection(
    bool PrevRowsPresent,             // the settlement is a row of prev.Settlements (false on a founding record)
    FoodStateKind State,              // RECOMPUTED FoodState.Of(prev, s, cfg)
    FamineReason Reason,              // RECOMPUTED the same call's out parameter; None outside Famine
    double NominalDeficit,            // RECOMPUTED FoodState.DeficitRatio(prev, s) — d, the classifier's input
    double EffectiveDeficit,          // RECOMPUTED FoodState.EffectiveDeficit(d, state, cfg)
    bool Abandoned,                   // RECOMPUTED FoodState.IsAbandoned(prev, s)
    bool DisasterRowPresent,          // whether prev carried a DisasterRow for this settlement
    int DisasterKind,                 // READ  prev DisasterRow.Kind
    double DisasterSeverity,          // READ  prev DisasterRow.Severity
    double DisasterMultiplierApplied, // READ  prev DisasterRow.Multiplier — what Production multiplied the two
                                      //       food rates by in THIS step (WorldState.cs DisasterRow doc)
    double DisasterRemainingYears,    // READ  prev DisasterRow.RemainingYears
    bool DisasterPendingRowPresent,   // NEXT side, named as pending: the row the step just wrote
    int DisasterPendingKind,          // READ  next DisasterRow.Kind
    double DisasterPendingSeverity,   // READ  next DisasterRow.Severity
    double DisasterPendingMultiplier, // READ  next DisasterRow.Multiplier — what the NEXT step will apply
    double DisasterPendingRemainingYears, // READ  next DisasterRow.RemainingYears
    bool HarvestWeatherRowPresent,
    double HarvestWeatherApplied,     // READ  prev HarvestWeatherRow.Multiplier — permanent, because the
                                      //       F1/G1 decade-variance audit needs it (spec §3.11, R6 (3))
    double FoodLimit,                 // RECOMPUTED FoodHeadroom.Limit(prev, s, cohortWeights, baskets) — N_lim
    double Vacancy,                   // RECOMPUTED FoodHeadroom.Vacancy(prev, ...) — V_j
    double SurplusRatio,              // RECOMPUTED FoodHeadroom.FeedableAtLimit(prev, s, baskets, D) / D = X/D;
                                      //       NaN when prev carries no demand row or D <= 0 (the ratio has no
                                      //       denominator — an ABSENCE, not a zero)
    double Headroom);                 // RECOMPUTED DemographicsSystem.Headroom(prev, s, cfg) — H_0

/// <summary>
/// T4.21-5 (spec §3.11) — WHAT THE MIGRATION PLANNER DECIDED, per settlement.
///
/// Every value here is READ OUT OF <see cref="Sim.Core.Systems.Migration.MigrationPlan"/>
/// built by the PUBLIC static <c>MigrationSystem.Plan(prev, cfg, dt)</c> — the
/// same planner <c>MigrationSystem.Step</c> consumes, on the same prev, at the
/// same dt. There is ONE implementation of this arithmetic and the observer is
/// not it. The only value formed here rather than indexed out of the plan is
/// <see cref="FlightFractionPrime"/>, and it is formed by CALLING the system's
/// own <c>FlightFractionOf</c> and <c>FlightHazardScale</c> statics — not by
/// restating either expression.
///
/// SOURCE SIDE and DESTINATION SIDE are both present because this settlement is
/// both: it is source i of its own flight and destination j of everyone else's.
/// The names say which.
///
/// THE PAIRWISE MATRIX IS STILL NOT RECORDED. Per-destination CHANNEL totals are
/// (the plan carries them); the From→To flows are not, and
/// <see cref="MigrationSection.PairwiseFlows"/> keeps saying so verbatim.
/// </summary>
public sealed record MigrationPlanSection(
    bool PlanRecorded,                // false on a founding record: a settlement absent from prev was never planned
    double DtYears,                   // READ  next.Clock.DtYears — the dt the step integrated, hence Plan's argument
    // --- source side (§3.4) ---
    double ExitOpenness,              // RECOMPUTED plan.ExitOpenness[i] — ω_i = max_j damping × viability
    double FlightFractionPrime,       // RECOMPUTED MigrationSystem.FlightFractionOf(1.0, FlightHazardScale(cfg), ω, d, dt):
                                      //       φ at cohort profile 1, the mix-free gauge of how hard this settlement flees
    double FlightBound,               // RECOMPUTED plan.FlightBound[i] — Σ_b φ_b × count_b, heads, before shares/vacancy
    double FlightOut,                 // RECOMPUTED plan.FlightOut[i] — the flight channel total after the overdraw scale
    double GapOut,                    // RECOMPUTED plan.GapOut[i]
    double GapOutflowCap,             // RECOMPUTED plan.GapOutflowCap[i] — f × M*_i^out; +∞ when the basin is < 2
    double SrcScale,                  // RECOMPUTED plan.SrcScale[i]
    // --- destination side (§3.5) ---
    double FlightIn,                  // RECOMPUTED plan.FlightIn[j], heads
    double GapIn,                     // RECOMPUTED plan.GapIn[j], heads
    double GapInflowCap,              // RECOMPUTED plan.GapInflowCap[j] — f × M*_j^in; +∞ when the basin is < 2
    double DestScale,                 // RECOMPUTED plan.DestScale[j]
    double VacancyCap,                // RECOMPUTED plan.VacancyCap[j] — cap_j = (1 − e^{−k·dt}) × V_j
    double DesiredInflowAe,           // RECOMPUTED plan.DesiredInflowAe[j] — BOTH channels, adult-equivalents
    double VacancyScale,              // RECOMPUTED plan.VacancyScale[j]; < 1 IS "refugees refused"
    string InflowAeByChannel);        // "GAP: ..." — never a number
