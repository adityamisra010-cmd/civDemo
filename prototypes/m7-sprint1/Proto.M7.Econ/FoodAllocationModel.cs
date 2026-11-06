using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Demographics;
using Sim.Core.Worldgen;

namespace Proto.M7.Econ;

/// <summary>What happens to grain above the granary capacity, and WHEN the capacity is applied relative to the
/// military draw (M7 Phase C lane F; DIR5 §2.11 "investigate granary overflow losses"). Only
/// <see cref="CapBeforeMilitary"/> is the existing rule; the others are PROPOSALS or COUNTERFACTUALS run for comparison.
/// The FATE of the grain above the capacity: lost (<see cref="CapBeforeMilitary"/>, <see cref="AllocateBeforeCap"/>: the
/// shipped GranaryOverflow sink), carried (<see cref="NoCap"/>: kept, spoilage only) or traded (<see cref="TradeCredit"/>).</summary>
public enum OverflowRule
{
    /// <summary>EXISTING ORDER (Sim.Core pipeline): Consumption (slot 6) feeds civilians, spoils, and sinks everything above
    /// granaryYearsOfDemand × annual civilian grain demand as GranaryOverflow; Military (slot 23) draws upkeep afterwards
    /// from the capped store. The overflow is lost before the military can use it.</summary>
    CapBeforeMilitary = 1,
    /// <summary>PROPOSAL: the period's allocation (civilians, growth, military) is made from everything held; spoilage and
    /// the capacity apply to what is left. Overflow = genuinely unclaimed grain only.</summary>
    AllocateBeforeCap = 2,
    /// <summary>COUNTERFACTUAL (bounds the cost of the capacity rule): as <see cref="AllocateBeforeCap"/> with no
    /// capacity — spoilage alone limits the store.</summary>
    NoCap = 3,
    /// <summary>PROPOSAL, BOUNDING ("trade" fate of the overflow; no Sim.Core counterpart): the EXISTING timing of
    /// <see cref="CapBeforeMilitary"/>, but the grain above the capacity is SOLD to an outside partner instead of destroyed —
    /// a conserved Transfer out (ledger reason Exported) that earns a grain-value credit at the numeraire price, which
    /// D-033 pins at 1.0 for grain. The credit buys grain back (Imported) at the same price whenever the physical store
    /// cannot cover a claim: civilians before they eat, and the allocator sees store + credit. No transport cost, no price
    /// movement, an unlimited partner: the MOST a market could return for the overflow. The shipped TradeArbitrage never
    /// moves grain (numeraire gap structurally zero), so this is a rule the existing code does not have. While NO CLAIM IS
    /// SHORT (the allocator never binds) it is food-identical to <see cref="CapBeforeMilitary"/> (only the ledger reason
    /// differs). An UNDRAWN credit is NOT neutral once a claim is short (verification round 1, F-V1): the allocator counts
    /// store + credit, and the growth-headroom grant is not a physical draw, so growth can be granted against the credit
    /// while the physical store goes to the army with zero imports — an accounting effect of this proposal, measured
    /// separately from the buy-back (<see cref="FoodLedger.ImportedForCivilians"/>; generated table §13). The credit
    /// opens at zero at T0: the rule switches on at the scenario's start, as every other rule does.</summary>
    TradeCredit = 4,
    /// <summary>PROPOSAL, BOUNDING (upper estimate): <see cref="TradeCredit"/> as if the world had sold its overflow since
    /// founding — the credit OPENS at the grain the REAL world destroyed as GranaryOverflow in turns 1..T0
    /// (<see cref="FoodScenario.OpeningCredit"/>). An upper estimate: it ignores anything the polity would have bought back
    /// before T0.</summary>
    TradeCreditSinceFounding = 5,
}

/// <summary>What happens to soldiers whose ration is not granted (the "cannot pay" rule; DIR5 §2.9 puts soldier attrition
/// and mortality in M7.14 and D-080 WA4 delegates the realism of a shortfall). None of these is adopted; each is run.</summary>
public enum UnfedConsequence
{
    /// <summary>EXISTING RULE: the shortfall accrues as UnfedUnits and nothing reads it.</summary>
    None = 1,
    /// <summary>PROPOSAL (coefficient-free): the unfed share of the army leaves and returns to civilian life at the end of
    /// the period (a Transfer — people are conserved): the army shrinks to the strength its supply feeds.</summary>
    Desert = 2,
    /// <summary>PROPOSAL (existing law reused): soldiers die at the civilian starvation law
    /// (starvationMortalityMaxPerYear × effective deficit, adult multiplier 1) with deficit = the unfed ration share.</summary>
    Starve = 3,
}

/// <summary>What the growth claim physically is (the comparison's outcome depends on it; neither is ratified).</summary>
public enum GrowthClaim
{
    /// <summary>PRIMARY (the Slice F definition, G = Σ FoodHeadroom.Vacancy × dt, and Sim.Core's own mechanism): the grant
    /// is HEADROOM — permission for the population to grow into it. The grain stays in the store (subject to spoilage and
    /// the capacity); the growing population eats through the civilian requirement of later periods.</summary>
    Headroom = 1,
    /// <summary>BOUNDING SENSITIVITY: the granted growth claim is eaten in the period (a sink), as if growth itself
    /// consumed its whole claim. No Sim.Core counterpart (it also changes peace); it bounds how much the ordering could
    /// cost the military if growth were a real competitor for grain.</summary>
    Consumed = 2,
}

/// <summary>How long one allocation period is.</summary>
public enum SettlementGranularity
{
    /// <summary>One period per War Pulse (DIR5 §2.4: economic effects per pulse), with the PROPOSED pulse lengths of
    /// lane T2 (D-079 Q1, not ratified), split at turn boundaries.</summary>
    WarPulse = 1,
    /// <summary>One period per campaign turn — the granularity of the shipped Sim.Core pipeline (validation only).</summary>
    CampaignTurn = 2,
}

/// <summary>
/// Every constant the model uses, READ from the shipped configuration (sim.json, goods.json, worldgen.json) or the kernel.
/// None is typed in here. Doubles are rates/ratios (Law 7); every stock is a long.
/// </summary>
public sealed record FoodModelParams(
    double[] Fertility,
    double[] Mortality,
    double[] CohortWeights,
    double StarvationMaxPerYear,
    double StarvationChildMultiplier,
    double StarvationElderMultiplier,
    double FertilitySuppressionSlope,
    double ReboundRecoverableFraction,
    double ReboundReleaseRatePerYear,
    double HeadroomRelaxationPerYear,
    double AbsorbableShortfall,
    double SpoilagePerYear,
    double GranaryYearsOfDemand,
    double UpkeepPerPersonYear,
    double SigmaLogYield,
    double HarvestCorrelationYears,
    MovementParams March)
{
    public static FoodModelParams Shipped => From(EconWorld.SimCfg, EconWorld.WorldgenCfg);

    public static FoodModelParams From(SimConfig cfg, WorldgenConfig wg)
    {
        DemographicsConfig d = cfg.Demographics;
        MilitaryConfig m = cfg.Military ?? throw new ArgumentException("sim.json military block is required.");
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException("goods.json is required.");
        // The march bill uses the existing MovementFood arithmetic. Ideal-ground km per lattice cost unit =
        // worldgen kmPerPx × TraversalLattice.DefaultStride (LatticeGeometry.KmPerCostUnitOnIdealGround).
        var march = new MovementParams(m.UpkeepGrainPerPersonYear, cfg.Trade.CostPerBulkCostUnit, goods.ById(goods.GrainId).BulkPerUnit,
            (cfg.Roads ?? throw new ArgumentException("sim.json roads block is required.")).BaselineKmPerDay,
            wg.KmPerPx * TraversalLattice.DefaultStride, SimClock.YearDays, cfg.Transport.RiverCostFactor);
        return new FoodModelParams(d.FertilityPerPersonPerYear, d.MortalityPerYear, cfg.Consumption.CohortWeights,
            d.StarvationMortalityMaxPerYear, d.StarvationChildMultiplier, d.StarvationElderMultiplier, d.FamineFertilitySuppressionSlope,
            d.ReboundRecoverableFraction, d.ReboundReleaseRatePerYear, d.HeadroomRelaxationPerYear, cfg.FoodState.AdaptationAbsorbableShortfall,
            cfg.Consumption.GrainSpoilagePerYear, cfg.Consumption.GranaryYearsOfDemand, m.UpkeepGrainPerPersonYear,
            cfg.HarvestVariance.SigmaLogYield, cfg.HarvestVariance.CorrelationTimeYears, march);
    }

    /// <summary>
    /// PROPOSED War Pulse length (days) for a campaign turn of <paramref name="turnDays"/> — lane T2's table
    /// (reports/G-war-pulse-t2.md §7; D-079 Q1, NOT ratified). Keyed on the shipped turn length, which identifies the band
    /// for every turn length these runs reach (3600 → Neolithic 21 d, 1800 → Bronze/Iron 14 d). Unknown lengths are refused,
    /// never guessed.
    /// </summary>
    public static long WarPulseDays(long turnDays) => turnDays switch
    {
        3600 => 21,
        1800 => 14,
        1080 => 10,
        720 => 7,
        360 => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(turnDays), "no proposed War Pulse length for a " + turnDays.ToString(CultureInfo.InvariantCulture) + "-day turn"),
    };
}

/// <summary>
/// One run of the comparison. The SCENARIO (army, window, regime) is identical for both orderings; only
/// <see cref="Order"/> differs between a pair of runs.
/// </summary>
public sealed record FoodScenario(
    string Army,
    string Regime,
    long StartTurn,
    int HorizonTurns,
    long StandingSoldiers,
    long WarSoldiers,
    bool Campaign,
    long WindowStartDay,
    long WindowLengthDays,
    double HarvestMultiplier,
    bool EmptyGranary,
    AllocationOrder Order,
    OverflowRule Overflow,
    UnfedConsequence Consequence,
    SettlementGranularity Granularity,
    HaulBasis Haul,
    GrowthClaim Growth = GrowthClaim.Headroom,
    long OpeningCredit = 0,
    long PulseDays = 0,
    long MarchLegDays = 0)
{
    // Verification round 2 (F2-1): PulseDays and MarchLegDays are SENSITIVITY inputs, 0 = the default. They are not in
    // Key or in the CSV: the battery never sets them, so its rows keep their bytes.
    public string Key => string.Join("|", Army, Regime, StartTurn.ToString(CultureInfo.InvariantCulture), HorizonTurns.ToString(CultureInfo.InvariantCulture),
        Order.ToString(), Overflow.ToString(), Consequence.ToString(), Granularity.ToString(), Haul.ToString(), Growth.ToString());
}

/// <summary>How long one allocation period and one march leg are (verification round 2, F2-1 and F2-2).</summary>
public static class FoodPeriods
{
    /// <summary>The settlement period for a turn of <paramref name="turnDays"/>: per War Pulse, the scenario's
    /// <see cref="FoodScenario.PulseDays"/> when set (a sensitivity input), else lane T2's PROPOSED length
    /// (<see cref="FoodModelParams.WarPulseDays"/>); per campaign turn, the turn.</summary>
    public static long SettlementDays(FoodScenario sc, long turnDays) => sc.Granularity == SettlementGranularity.WarPulse
        ? (sc.PulseDays > 0 ? sc.PulseDays : FoodModelParams.WarPulseDays(turnDays))
        : turnDays;

    /// <summary>The march leg: the days between two resupplies of a campaigning army, which carries the leg's rations
    /// over the leg (MovementFood bills one leg). <see cref="FoodScenario.MarchLegDays"/> when set (a sensitivity input),
    /// else T2's PROPOSED War Pulse length for the turn — T2 defines a pulse as one operational move. A PHYSICAL
    /// parameter, independent of the settlement period: the haul is billed per sim-year (Law 3), so cutting the same days
    /// into shorter or longer periods does not change it.</summary>
    public static long MarchLegDays(FoodScenario sc, long turnDays) => sc.MarchLegDays > 0 ? sc.MarchLegDays : FoodModelParams.WarPulseDays(turnDays);
}

/// <summary>The model's own ledger: running totals per reason. Grain and people only change through
/// <see cref="FoodPulseModel"/>'s Flow/Transfer helpers, which post here (the kernel Ledger stand-in, as T2's WarLedger).</summary>
public sealed class FoodLedger
{
    public long GrainInitial, Harvest, EatenCivilian, Ration, Haul, Spoilage, Overflow, ScenarioReset, GrowthEaten;
    public long CiviliansInitial, SoldiersInitial, Births, Deaths, Starvation, SoldierStarvation, Mobilised, Demobilised, Deserted;
    /// <summary><see cref="OverflowRule.TradeCredit"/> only (zero under every other rule): grain transferred to the outside
    /// partner (Exported) and bought back (Imported). The partner's account — the polity's credit — is Exported − Imported.</summary>
    public long Exported, Imported, CreditInitial;
    /// <summary>Reporting split of <see cref="Imported"/> (not a separate flow; not hashed): the part bought back so that
    /// CIVILIANS could eat. Imported − ImportedForCivilians = bought back for granted draws (ration, haul, eaten growth).</summary>
    public long ImportedForCivilians;

    public FoodLedger Clone() => (FoodLedger)MemberwiseClone();

    /// <summary>Grain stock implied by the totals.</summary>
    public long GrainImplied => checked(GrainInitial + Harvest + Imported - EatenCivilian - Ration - Haul - Spoilage - Overflow - ScenarioReset - GrowthEaten - Exported);
    /// <summary>The trade credit implied by the totals (grain-value units at the numeraire price 1).</summary>
    public long CreditImplied => checked(CreditInitial + Exported - Imported);
    public long CiviliansImplied => checked(CiviliansInitial + Births - Deaths - Starvation - Mobilised + Demobilised + Deserted);
    public long SoldiersImplied => checked(SoldiersInitial + Mobilised - Demobilised - Deserted - SoldierStarvation);
}

/// <summary>Allocation outcomes summed over the war window (or over the whole run).</summary>
public sealed class AllocationStats
{
    public long Periods, CivilianRequested, CivilianGranted, GrowthDemand, GrowthGranted, RationRequested, RationGranted,
        HaulRequested, HaulGranted, MarchDaysRequested, MarchDaysGranted, PeriodsGrowthReduced, PeriodsMilitaryRestricted,
        PeriodsCivilianShort, PeriodsFullySupplied;

    public void Add(LogisticsDecision d, long civRequested, long civGranted, long rationReq, long rationGot, long haulReq, long haulGot,
        long marchReq, long marchGot)
    {
        Periods++;
        CivilianRequested += civRequested;
        CivilianGranted += civGranted;
        GrowthDemand += d.GrowthDemand;
        GrowthGranted += d.GrowthGranted;
        RationRequested += rationReq;
        RationGranted += rationGot;
        HaulRequested += haulReq;
        HaulGranted += haulGot;
        MarchDaysRequested += marchReq;
        MarchDaysGranted += marchGot;
        if (d.GrowthWasReduced) PeriodsGrowthReduced++;
        if (rationGot < rationReq || marchGot < marchReq) PeriodsMilitaryRestricted++;
        if (civGranted < civRequested) PeriodsCivilianShort++;
        if (rationGot == rationReq && marchGot == marchReq) PeriodsFullySupplied++;
    }
}

/// <summary>State at one horizon checkpoint (turn boundary).</summary>
public sealed record FoodCheckpoint(int TurnsAfter, long Turn, long Civilians, long Soldiers, long GrainStock, FoodLedger Ledger);

public sealed record FoodRunResult(
    ulong Seed,
    FoodScenario Scenario,
    FoodCheckpoint[] Checkpoints,
    long[] PopulationByTurn,
    long[] StockByTurn,
    AllocationStats War,
    AllocationStats Whole,
    long WarSoldiersAtStart,
    long WarSoldiersAtEnd,
    long WarMinStock,
    long WarEndStock,
    long WindowStartDayApplied,
    long WindowEndDayApplied,
    long LargestPeriodDays,
    FoodLedger WindowStartLedger,
    FoodLedger WindowEndLedger,
    FoodLedger FinalLedger,
    long FinalCivilians,
    long FinalSoldiers,
    long FinalGrainStock,
    string StateHash);

/// <summary>
/// THE FOOD-ALLOCATION COMPARISON MODEL (M7 Phase C lane F). A reduced-order, per-period food economy of ONE polity,
/// anchored to a REAL Sim.Core baseline (<see cref="BaselineSeries"/>): it starts from the real world's cohorts, store and
/// soldiers at turn T0, and takes, per turn, the real world's harvest rate, labour, civilian grain need per
/// adult-equivalent and growth headroom limit. It reproduces that baseline when the polity does what the baseline did,
/// and responds to deviations (an army, a bad harvest, an allocation ordering) through measured or shipped rules only:
/// <list type="bullet">
/// <item>Harvest = baseline rate − the measured labour response (REAL RecruitFormation twins, <see cref="LabourTwin"/>)
/// × the regime's harvest multiplier.</item>
/// <item>Civilian grain requirement = Σ cohortWeight × count × the baseline's grain eaten per adult-equivalent-year.</item>
/// <item>Growth demand G = FoodHeadroom-style vacancy (baseline limit scaled by the model's harvest ÷ the baseline's) ×
/// grain need × period length — the Slice F definition ("Σ FoodHeadroom.Vacancy × dt").</item>
/// <item>Military: a standing ration (military.upkeepGrainPerPersonYear) and, when campaigning, a continuous march resupplied
/// every LEG (<see cref="FoodPeriods.MarchLegDays"/>; default T2's proposed pulse: one operational move per pulse), one leg
/// billed by <see cref="MovementFood.FromRouteCost"/> (haul only: the rations ARE the ration claim) and drawn per sim-year
/// (verification round 2, F2-1). The yearly haul grows with the leg: a leg of L days carries L days of rations over L
/// days of route.</item>
/// <item>Allocation: <see cref="LogisticsPriority.Resolve(long, long, long, ReadOnlySpan{MoveRequest}, AllocationOrder)"/>
/// — the ordering under test is the only switch between a pair of runs.</item>
/// <item>Store: spoilage 1 − e^(−s·h) of held grain, granary capacity granaryYearsOfDemand × annual civilian grain demand,
/// in the order <see cref="OverflowRule"/> names.</item>
/// <item>Demography: the DemographicsSystem kernel's arithmetic (fertility, base and starvation mortality, effective
/// deficit dead-zone, fertility suppression, rebound bank, headroom cap with relaxation, fractional aging, integer
/// reconciliation births → aging ascending → deaths → starvation with remainder accumulators), on ONE aggregate cohort
/// vector; the growth grant caps the headroom the kernel sees: H_eff = min(H, G_granted ÷ (grain need × h)).</item>
/// </list>
/// NOT modelled (stated, not hidden): per-settlement stores and relief, classes/cultures, knowledge effects on demography,
/// the health multiplier, non-grain food dynamics (held at the baseline share), migration, weather beyond the regime.
/// Deterministic: no RNG, no clock, no unordered iteration; every stock is a long; conservation is checked every period.
/// </summary>
public sealed class FoodPulseModel
{
    private const int N = Cohorts.Count;
    private readonly FoodModelParams _p;
    private readonly BaselineSeries _s;
    private readonly FoodScenario _sc;

    // ---- state
    private readonly long[] _civ = new long[N];
    private long _soldiers, _stock;
    private long _credit;   // TradeCredit only: grain-value held by the outside partner (Exported − Imported)
    private readonly double[] _remAging = new double[N], _remDeath = new double[N], _remStarve = new double[N];
    private double _remBirth, _reservoir;
    private double _remReq, _remRation, _remSpoil, _remDesert, _remSoldierStarve, _remHaul, _remGrowth;
    private double _prevDeficit;
    private readonly FoodLedger _ledger = new();

    // ---- harvest rate segment. Production labour is the civilian adults at the turn start (Sim.Core's Production reads
    // PREV once per turn), moved only by army transfers inside the turn; so the rate is constant between a turn start, an
    // army event and a window edge. Cumulative-floor integration inside a segment: a segment's total does not depend on
    // how it is cut into periods (dt-correct by construction); the fractional leftover is carried into the next segment.
    private double _segRate, _segCarry;
    private long _segStartDay, _segTaken;
    private bool _segOpen;
    private long _prodAdults;

    // ---- labour response measured at T0
    private readonly double _eps1, _eps2;
    private readonly long _r1;

    private FoodPulseModel(BaselineSeries s, FoodScenario sc, FoodModelParams p)
    {
        _s = s;
        _sc = sc;
        _p = p;
        (LabourTwin small, LabourTwin large) = s.TwinsAt(sc.StartTurn);
        // Elasticities of the harvest to labour, relative to the average product (harvest rate ÷ adults) of the step the
        // twins measured (step T0+2 of the base branch, which IS the baseline: same world, no orders): ε = marginal ÷ average.
        double ap = small.BaseHarvestT2 / small.DtYears / s.At(sc.StartTurn + 1).Adults;
        _eps1 = small.MarginalGrainPerAdultYear / ap;
        _r1 = small.Removed;
        double lossSmall = small.BaseHarvestT2 - small.TwinHarvestT2;
        double lossLarge = large.BaseHarvestT2 - large.TwinHarvestT2;
        _eps2 = large.Removed > small.Removed
            ? (lossLarge - lossSmall) / ((large.Removed - small.Removed) * large.DtYears) / ap
            : _eps1;
    }

    /// <summary>Run one scenario on one real baseline. <paramref name="trace"/> (optional, report/debug only) receives one
    /// line per allocation period; it never feeds back into the run.</summary>
    public static FoodRunResult Run(BaselineSeries s, FoodScenario sc, FoodModelParams p, Action<string>? trace = null)
    {
        if (sc.StartTurn + sc.HorizonTurns >= s.Turns.Length)
            throw new ArgumentOutOfRangeException(nameof(sc), "the baseline does not reach turn " + (sc.StartTurn + sc.HorizonTurns).ToString(CultureInfo.InvariantCulture));
        return new FoodPulseModel(s, sc, p) { _trace = trace }.Execute();
    }

    private Action<string>? _trace;

    /// <summary>The checkpoints reported (turns after T0), clipped to the horizon.</summary>
    public static readonly int[] HorizonCheckpoints = [20, 100, 300];

    private FoodRunResult Execute()
    {
        BaselineTurn start = _s.At(_sc.StartTurn);
        for (int c = 0; c < N; c++) _civ[c] = start.Cohorts[c];
        _soldiers = start.Soldiers;
        _stock = start.GrainStock;
        _ledger.GrainInitial = _stock;
        _ledger.CiviliansInitial = Sum(_civ);
        _ledger.SoldiersInitial = _soldiers;
        if (_sc.EmptyGranary) FlowGrainOut(ref _ledger.ScenarioReset, _stock);
        if (_sc.OpeningCredit != 0)
        {
            if (_sc.OpeningCredit < 0 || !IsTrade(_sc.Overflow))
                throw new ArgumentOutOfRangeException(nameof(_sc), "an opening credit needs a trade rule and must not be negative");
            _credit = _sc.OpeningCredit;
            _ledger.CreditInitial = _credit;
        }

        // The standing army is raised before the first period (scenario day 0).
        if (_sc.StandingSoldiers > _soldiers) Mobilise(_sc.StandingSoldiers - _soldiers);

        var war = new AllocationStats();
        var whole = new AllocationStats();
        var checkpoints = new List<FoodCheckpoint>();
        var pop = new long[_sc.HorizonTurns + 1];
        var stockByTurn = new long[_sc.HorizonTurns + 1];
        pop[0] = Sum(_civ) + _soldiers;
        stockByTurn[0] = _stock;
        long day = 0;
        bool windowOpen = false, windowClosed = false;
        long windowStartApplied = -1, windowEndApplied = -1;
        long warAtStart = 0, warAtEnd = 0, minStock = long.MaxValue, endStock = 0, largest = 0;
        FoodLedger? atOpen = null, atClose = null;
        long windowStart = _sc.WindowStartDay, windowEnd = _sc.WindowStartDay + _sc.WindowLengthDays;

        for (int k = 1; k <= _sc.HorizonTurns; k++)
        {
            long t = _sc.StartTurn + k;
            BaselineTurn row = _s.At(t), prevRow = _s.At(t - 1);
            long turnDays = row.DtDays;
            double dtYears = row.DtYears;
            double yb = row.StepHarvest / dtYears;
            double lb = prevRow.Adults;
            double grainNeed = prevRow.NutritionalPersons > 0 ? row.StepGrainEaten / (prevRow.NutritionalPersons * dtYears) : 0.0;
            double nlimBase = prevRow.HeadroomUnbounded ? double.PositiveInfinity : prevRow.HeadroomLimit;
            long periodDays = FoodPeriods.SettlementDays(_sc, turnDays);
            _turnDays = turnDays;
            _prodAdults = Adults();   // the turn's production labour (PREV, as Sim.Core's Production)
            // Demography runs on the kernel's own grid: blocks of DemographicsSystem.MicroStepYears (180 days) from the turn
            // start, the last one ending at the turn end — exactly the micro-steps DemographicsSystem integrates a turn with.
            _turnEnd = day + turnDays;
            _blockStart = day;
            _blockEnd = Math.Min(day + MicroDays, _turnEnd);
            _grainNeed = grainNeed;
            for (long off = 0; off < turnDays; off += periodDays)
            {
                long len = Math.Min(periodDays, turnDays - off);
                largest = Math.Max(largest, len);
                long periodStart = day + off;
                // WA1 (D-080): an event at instant X applies to the first period whose start S satisfies S ≥ X.
                if (!windowOpen && periodStart >= windowStart)
                {
                    windowOpen = true;
                    windowStartApplied = periodStart;
                    atOpen = _ledger.Clone();
                    if (_sc.WarSoldiers > 0) Mobilise(_sc.WarSoldiers);
                    warAtStart = _soldiers;
                }
                if (windowOpen && !windowClosed && periodStart >= windowEnd)
                {
                    windowClosed = true;
                    windowEndApplied = periodStart;
                    warAtEnd = _soldiers;
                    endStock = _stock;
                    atClose = _ledger.Clone();
                    if (_soldiers > _sc.StandingSoldiers) Demobilise(_soldiers - _sc.StandingSoldiers);
                }
                bool inWindow = windowOpen && !windowClosed;
                Period(periodStart, len, yb, lb, grainNeed, nlimBase, inWindow, inWindow ? war : null, whole);
                if (inWindow) minStock = Math.Min(minStock, _stock);
            }
            day += turnDays;
            pop[k] = Sum(_civ) + _soldiers;
            stockByTurn[k] = _stock;
            for (int h = 0; h < HorizonCheckpoints.Length; h++)
                if (HorizonCheckpoints[h] == k)
                    checkpoints.Add(new FoodCheckpoint(k, t, Sum(_civ), _soldiers, _stock, _ledger.Clone()));
        }
        if (windowOpen && !windowClosed)
        {
            warAtEnd = _soldiers;
            endStock = _stock;
            atClose = _ledger.Clone();
        }
        if (minStock == long.MaxValue) minStock = -1;
        return new FoodRunResult(_s.Seed, _sc, [.. checkpoints], pop, stockByTurn, war, whole, warAtStart, warAtEnd, minStock, endStock,
            windowStartApplied, windowEndApplied, largest, atOpen ?? _ledger.Clone(), atClose ?? _ledger.Clone(), _ledger.Clone(),
            Sum(_civ), _soldiers, _stock, StateHash());
    }

    // ------------------------------------------------------------------ one allocation period

    private void Period(long startDay, long len, double yb, double lb, double grainNeed, double nlimBase, bool inWindow,
        AllocationStats? war, AllocationStats whole)
    {
        double h = len / (double)SimClock.YearDays;
        double shock = inWindow ? _sc.HarvestMultiplier : 1.0;

        // 1. Harvest: the baseline rate adjusted for the labour the polity actually has (measured response), times the
        //    regime's multiplier inside the window. Constant within a segment; a segment restarts at a turn start and at
        //    every labour event (mobilisation, demobilisation, desertion).
        double rate = HarvestRate(yb, lb, _prodAdults) * shock;
        long harvest = TakeHarvest(rate, startDay, len);
        FlowGrainIn(ref _ledger.Harvest, harvest);

        // 2. Claims.
        double nutr = Nutritional();
        long requirement = Carry(ref _remReq, nutr * grainNeed * h);
        double nlim = double.IsPositiveInfinity(nlimBase) ? double.PositiveInfinity : nlimBase * (yb > 0 ? rate / yb : 0.0);
        double vacancy = double.IsPositiveInfinity(nlim) ? 0.0 : Math.Max(0.0, nlim - nutr);
        // Verification round 2 (F2-5): a remainder accumulator, as the civilian requirement and the ration. A per-period
        // ceiling added up to one unit per period (+0.22 % of the claim at 21-day pulses, +0.64 % at 7-day; measured).
        double growthExact = vacancy * grainNeed * h;
        long growthDemand = Carry(ref _remGrowth, growthExact);
        long rationReq = Carry(ref _remRation, _soldiers * _p.UpkeepPerPersonYear * h);
        long marchDaysReq = _sc.Campaign && inWindow && _soldiers > 0 ? len : 0;
        long haulReq = 0;
        if (marchDaysReq > 0)
        {
            // Verification round 2 (F2-1): the haul is billed per sim-year. One leg (the days between two resupplies) is
            // billed by MovementFood — its rations carried over its route — and the period draws len ÷ leg of that bill,
            // with a remainder accumulator: the bill of a year of marching depends on the leg, not on how the year is cut
            // into settlement periods. When a period IS one leg (the battery: 21- and 14-day pulses, no short period
            // inside the window) the claim is the leg's bill exactly, as before. Rounded once per leg, not per period (F2-5).
            long leg = FoodPeriods.MarchLegDays(_sc, _turnDays);
            double costUnits = leg * _p.March.BaselineKmPerDay / _p.March.KmPerCostUnit;
            long legHaul = MovementFood.FromRouteCost(_p.March, 0, costUnits, 0.0, _soldiers, _sc.Haul).HaulUnits;
            haulReq = Carry(ref _remHaul, legHaul * (marchDaysReq / (double)leg));
        }
        MoveRequest[] moves = marchDaysReq > 0
            ? [new MoveRequest(1, 0, rationReq, len), new MoveRequest(2, 1, haulReq, marchDaysReq)]
            : [new MoveRequest(1, 0, rationReq, len)];

        // 3. Allocation in the order the overflow rule names.
        LogisticsDecision d;
        long civEaten;
        bool trade = IsTrade(_sc.Overflow);
        if (_sc.Overflow == OverflowRule.CapBeforeMilitary || trade)
        {
            // TradeCredit: civilians first — the credit buys back what the physical store lacks for their requirement.
            if (trade && _stock < requirement) Reimport(Math.Min(_credit, requirement - _stock), forCivilians: true);
            civEaten = Math.Min(_stock, requirement);
            FlowGrainOut(ref _ledger.EatenCivilian, civEaten);
            SpoilAndCap(h, nutr * grainNeed, cap: true);
            // TradeCredit: the allocator sees the store plus the credit (both orders alike; the order is still the only switch).
            d = LogisticsPriority.Resolve(trade ? checked(_stock + _credit) : _stock, 0, growthDemand, moves, _sc.Order);
        }
        else
        {
            d = LogisticsPriority.Resolve(_stock, requirement, growthDemand, moves, _sc.Order);
            civEaten = d.CivilianGranted;
            FlowGrainOut(ref _ledger.EatenCivilian, civEaten);
        }
        long rationGot = Granted(d, 1), haulGot = Granted(d, 2);
        long marchGot = GrantedDays(d, 2);
        if (trade)
        {
            // Buy back exactly what the granted physical draws need beyond the store (≤ the credit: grants ≤ store + credit).
            long draw = checked(rationGot + haulGot + (_sc.Growth == GrowthClaim.Consumed ? d.GrowthGranted : 0));
            if (draw > _stock) Reimport(draw - _stock, forCivilians: false);
        }
        FlowGrainOut(ref _ledger.Ration, rationGot);
        FlowGrainOut(ref _ledger.Haul, haulGot);
        if (_sc.Growth == GrowthClaim.Consumed) FlowGrainOut(ref _ledger.GrowthEaten, d.GrowthGranted);
        if (_sc.Overflow == OverflowRule.AllocateBeforeCap || _sc.Overflow == OverflowRule.NoCap) SpoilAndCap(h, nutr * grainNeed, cap: _sc.Overflow == OverflowRule.AllocateBeforeCap);

        _trace?.Invoke(string.Join(" ", "day", L(startDay), "len", L(len), "rate", rate.ToString("F1", CultureInfo.InvariantCulture),
            "harvest", L(harvest), "R", L(requirement), "civ", L(civEaten), "G", L(growthDemand), "Gx", growthExact.ToString("R", CultureInfo.InvariantCulture), "g", L(d.GrowthGranted),
            "ration", L(rationGot) + "/" + L(rationReq), "haul", L(haulGot) + "/" + L(haulReq), "stock", L(_stock),
            "spoil", L(_ledger.Spoilage), "over", L(_ledger.Overflow), "civs", L(Sum(_civ)), "sold", L(_soldiers), "stage", d.Stage.ToString())
            + (trade ? " exported " + L(_ledger.Exported) + " imported " + L(_ledger.Imported) + " credit " + L(_credit) : ""));
        war?.Add(d, requirement, civEaten, rationReq, rationGot, haulReq, haulGot, marchDaysReq, marchGot);
        whole.Add(d, requirement, civEaten, rationReq, rationGot, haulReq, haulGot, marchDaysReq, marchGot);

        // 4. Unfed soldiers.
        if (rationReq > 0 && rationGot < rationReq && _soldiers > 0)
        {
            double unfedShare = (rationReq - rationGot) / (double)rationReq;
            if (_sc.Consequence == UnfedConsequence.Desert)
            {
                long leave = Math.Min(_soldiers, Carry(ref _remDesert, _soldiers * unfedShare));
                if (leave > 0) ReturnToCivilians(leave, ref _ledger.Deserted);
            }
            else if (_sc.Consequence == UnfedConsequence.Starve)
            {
                double dEff = FoodState.EffectiveDeficit(unfedShare, FoodStateKind.Normal, _p.AbsorbableShortfall);
                double rateS = _p.StarvationMaxPerYear * dEff;
                long dead = Math.Min(_soldiers, Carry(ref _remSoldierStarve, _soldiers * (1.0 - Math.Exp(-rateS * h))));
                if (dead > 0)
                {
                    _soldiers -= dead;
                    _ledger.SoldierStarvation += dead;
                }
            }
        }
        // 5. Demography: this period's claims and grants are prorated onto the 180-day blocks it covers; every block that
        //    completes is integrated (the growth grant caps the headroom the kernel may fill).
        _blockCapped = !double.IsPositiveInfinity(nlim);
        long cursor = startDay, end = startDay + len;
        while (cursor < end)
        {
            long partEnd = Math.Min(end, _blockEnd);
            double frac = (partEnd - cursor) / (double)len;
            _accReq += requirement * frac;
            _accShort += (requirement - civEaten) * frac;
            _accGrant += d.GrowthGranted * frac;
            _accVacancyDays += vacancy * (partEnd - cursor);
            cursor = partEnd;
            if (partEnd == _blockEnd) RunBlock();
        }
        CheckConservation();
    }

    /// <summary>The demography grid: DemographicsSystem.MicroStepYears × SimClock.YearDays (a test asserts the equality).</summary>
    public const long MicroDays = 180;
    private long _turnEnd, _blockStart, _blockEnd, _turnDays;
    private double _grainNeed, _accReq, _accShort, _accGrant, _accVacancyDays;
    private bool _blockCapped;

    /// <summary>Integrate the completed demography block [_blockStart, _blockEnd) and open the next one.</summary>
    private void RunBlock()
    {
        long days = _blockEnd - _blockStart;
        double h = days / (double)SimClock.YearDays;
        double vacancy = days > 0 ? _accVacancyDays / days : 0.0;
        double grantHeadroom = _grainNeed * h > 0 ? _accGrant / (_grainNeed * h) : vacancy;
        double heff = Math.Min(vacancy, grantHeadroom);
        Demography(days, heff, _blockCapped);
        // The kernel reads the PREVIOUS step's nominal deficit (DemographicsSystem reads PREV); here the previous block's.
        _prevDeficit = _accReq > 0 ? Math.Clamp(_accShort / _accReq, 0.0, 1.0) : 0.0;
        _accReq = _accShort = _accGrant = _accVacancyDays = 0.0;
        _blockStart = _blockEnd;
        _blockEnd = Math.Min(_blockEnd + MicroDays, _turnEnd);
    }

    /// <summary>Whole harvest units for the period [startDay, startDay + len) at <paramref name="rate"/> per year
    /// (cumulative floor within a constant-rate segment; the fractional leftover is carried across a rate change).</summary>
    private long TakeHarvest(double rate, long startDay, long len)
    {
        if (!_segOpen || rate != _segRate)
        {
            if (_segOpen)
            {
                double leftover = _segCarry + _segRate * (startDay - _segStartDay) / SimClock.YearDays - _segTaken;
                _segCarry = Math.Clamp(leftover, 0.0, Math.BitDecrement(1.0));
            }
            _segRate = rate;
            _segStartDay = startDay;
            _segTaken = 0;
            _segOpen = true;
        }
        long cum = (long)Math.Floor(_segCarry + _segRate * (startDay + len - _segStartDay) / SimClock.YearDays);
        long harvest = Math.Max(0, cum - _segTaken);
        _segTaken = Math.Max(_segTaken, cum);
        return harvest;
    }

    private void SpoilAndCap(double h, double annualCivilianGrain, bool cap)
    {
        if (_stock > 0 && _p.SpoilagePerYear > 0.0)
        {
            long spoiled = Math.Min(_stock, Carry(ref _remSpoil, _stock * (1.0 - Math.Exp(-_p.SpoilagePerYear * h))));
            FlowGrainOut(ref _ledger.Spoilage, spoiled);
        }
        if (!cap || !(_p.GranaryYearsOfDemand > 0.0) || !(annualCivilianGrain > 0.0)) return;
        long capacity = (long)Math.Floor(_p.GranaryYearsOfDemand * annualCivilianGrain);
        long over = _stock - capacity;
        if (capacity > 0 && over > 0)
        {
            if (IsTrade(_sc.Overflow))
            {
                FlowGrainOut(ref _ledger.Exported, over);   // a Transfer to the outside partner, not a sink
                _credit = checked(_credit + over);           // credited at the numeraire price 1 (D-033)
            }
            else FlowGrainOut(ref _ledger.Overflow, over);
        }
    }

    /// <summary>The rules under which the overflow is sold for a credit.</summary>
    public static bool IsTrade(OverflowRule rule) => rule == OverflowRule.TradeCredit || rule == OverflowRule.TradeCreditSinceFounding;

    /// <summary>TradeCredit: buy <paramref name="amount"/> grain back from the outside partner at the numeraire price.</summary>
    private void Reimport(long amount, bool forCivilians)
    {
        if (amount <= 0) return;
        if (amount > _credit) throw new InvalidOperationException("trade credit overdraw");
        FlowGrainIn(ref _ledger.Imported, amount);
        if (forCivilians) _ledger.ImportedForCivilians += amount;
        _credit -= amount;
    }

    /// <summary>Harvest rate per year for <paramref name="adults"/> civilian adults: the baseline rate minus the measured
    /// response to the labour difference. Piecewise linear in the removed labour r = Lb − L: ε1·AP per adult up to the
    /// small twin's removal, then the secant to the large twin (ε2·AP); added labour (r &lt; 0) at ε1·AP. AP = the turn's
    /// average product Yb ÷ Lb. Never negative.</summary>
    public double HarvestRate(double yb, double lb, long adults)
    {
        if (!(lb > 0.0)) return Math.Max(0.0, yb);
        double ap = yb / lb;
        double r = lb - adults;
        double loss = r <= 0 ? _eps1 * ap * r
            : r <= _r1 ? _eps1 * ap * r
            : _eps1 * ap * _r1 + _eps2 * ap * (r - _r1);
        return Math.Max(0.0, yb - loss);
    }

    public double Epsilon1 => _eps1;
    public double Epsilon2 => _eps2;

    // ------------------------------------------------------------------ demography (DemographicsSystem arithmetic)

    private void Demography(long len, double hEff, bool capped)
    {
        var pop = new double[N];
        var births = 0.0;
        var deaths = new double[N];
        var starve = new double[N];
        var aging = new double[N];
        for (int c = 0; c < N; c++) pop[c] = _civ[c];
        double dEff = FoodState.EffectiveDeficit(_prevDeficit, FoodStateKind.Normal, _p.AbsorbableShortfall);
        double suppression = Math.Max(0.0, 1.0 - _p.FertilitySuppressionSlope * dEff);
        var baseRate = new double[N];
        var starveRate = new double[N];
        var total = new double[N];
        for (int c = 0; c < N; c++)
        {
            double mult = BandViews.IsChild(c) ? _p.StarvationChildMultiplier : BandViews.IsElder(c) ? _p.StarvationElderMultiplier : 1.0;
            starveRate[c] = _p.StarvationMaxPerYear * dEff * mult;
            baseRate[c] = _p.Mortality[c];
            total[c] = baseRate[c] + starveRate[c];
        }
        double hRem = capped ? Math.Max(0.0, hEff) : 0.0;
        double k = _p.HeadroomRelaxationPerYear;
        double[] w = _p.CohortWeights;
        long remainingDays = len;
        while (remainingDays > 0)
        {
            // Micro-steps of at most DemographicsSystem.MicroStepYears (180 days), the kernel's integration scale.
            long stepDays = Math.Min((long)Math.Round(DemographicsSystem.MicroStepYears * SimClock.YearDays), remainingDays);
            remainingDays -= stepDays;
            double h = stepDays / (double)SimClock.YearDays;
            double advance = h / Cohorts.WidthYears;
            double unsuppressed = 0.0;
            for (int c = 0; c < N; c++)
            {
                double f = _p.Fertility[c];
                if (f <= 0.0) continue;
                unsuppressed += f * pop[c] * W(total[c] * h) * h;
            }
            double born = unsuppressed * suppression;
            double bank = _p.ReboundRecoverableFraction * (unsuppressed - born);
            double release = 0.0;
            if (_prevDeficit == 0.0 && unsuppressed > 0.0)
                release = (_reservoir + bank) * Math.Min(1.0, _p.ReboundReleaseRatePerYear * h);
            double cand = born + release;
            double m = 1.0, nutritionBefore = 0.0;
            if (capped)
            {
                double deathsPre = 0.0, agingDrift = 0.0;
                for (int c = 0; c < N; c++)
                {
                    double survive = Math.Exp(-total[c] * h);
                    nutritionBefore += w[c] * pop[c];
                    deathsPre += pop[c] * (1.0 - survive) * w[c];
                    if (c >= N - 1) continue;
                    agingDrift += pop[c] * survive * advance * (w[c + 1] - w[c]);
                }
                double allowed = (1.0 - Math.Exp(-k * h)) * hRem;
                double newbornNet = W(total[0] * h) * Math.Exp(-total[0] * h) * w[0];
                double bornMax = (allowed + deathsPre - agingDrift) / newbornNet;
                m = cand > 0.0 ? Math.Min(1.0, Math.Max(0.0, bornMax) / cand) : 1.0;
            }
            _reservoir += bank;
            if (_prevDeficit == 0.0 && unsuppressed > 0.0)
            {
                if (m < 1.0) { born *= m; release *= m; }
                _reservoir -= release;
            }
            else if (m < 1.0) born *= m;
            double total0 = born + (_prevDeficit == 0.0 && unsuppressed > 0.0 ? release : 0.0);
            births += total0;
            double survivors = total0 * W(total[0] * h);
            deaths[0] += total0 - survivors;
            pop[0] += survivors;
            for (int c = N - 1; c >= 0; c--)
            {
                double dead = pop[c] * (1.0 - Math.Exp(-baseRate[c] * h));
                pop[c] -= dead;
                deaths[c] += dead;
                double starved = pop[c] * (1.0 - Math.Exp(-starveRate[c] * h));
                pop[c] -= starved;
                starve[c] += starved;
                if (c >= N - 1) continue;
                double moving = pop[c] * advance;
                pop[c] -= moving;
                pop[c + 1] += moving;
                aging[c] += moving;
            }
            if (capped)
            {
                double nutritionAfter = 0.0;
                for (int c = 0; c < N; c++) nutritionAfter += w[c] * pop[c];
                hRem -= nutritionAfter - nutritionBefore;
            }
        }

        // Integer reconciliation, the kernel's pinned order: births → aging ascending → deaths → starvation.
        long b = Carry(ref _remBirth, births);
        _civ[0] = checked(_civ[0] + b);
        _ledger.Births += b;
        for (int c = 0; c < N - 1; c++)
        {
            if (aging[c] <= 0.0) continue;
            long moving = Math.Min(_civ[c], Carry(ref _remAging[c], aging[c]));
            _civ[c] -= moving;
            _civ[c + 1] = checked(_civ[c + 1] + moving);
        }
        for (int c = 0; c < N; c++)
        {
            long dead = Math.Min(_civ[c], Carry(ref _remDeath[c], deaths[c]));
            _civ[c] -= dead;
            _ledger.Deaths += dead;
            long starved = Math.Min(_civ[c], Carry(ref _remStarve[c], starve[c]));
            _civ[c] -= starved;
            _ledger.Starvation += starved;
        }
    }

    /// <summary>w(x) = (1 − e^(−x))/x (DemographicsSystem.W).</summary>
    private static double W(double x) => x < 1e-12 ? 1.0 : (1.0 - Math.Exp(-x)) / x;

    // ------------------------------------------------------------------ people transfers

    /// <summary>Raise <paramref name="n"/> soldiers from the civilian adults (15–59) in proportion to the cohorts. At most
    /// adults − 1 (the existing personnel predicate's "at least one adult stays", aggregated).</summary>
    private void Mobilise(long n)
    {
        n = Math.Min(n, Math.Max(0, Adults() - 1));
        long[] take = Proportional(n, _civ, Cohorts.FirstAdult, Cohorts.FirstElder);
        long moved = 0;
        for (int c = 0; c < N; c++) { _civ[c] -= take[c]; moved += take[c]; }
        _soldiers = checked(_soldiers + moved);
        _ledger.Mobilised += moved;
        _prodAdults -= moved;
    }

    private void Demobilise(long n) => ReturnToCivilians(n, ref _ledger.Demobilised);

    private void ReturnToCivilians(long n, ref long reason)
    {
        n = Math.Min(n, _soldiers);
        long[] give = Proportional(n, _civ, Cohorts.FirstAdult, Cohorts.FirstElder);
        long given = 0;
        for (int c = 0; c < N; c++) { _civ[c] = checked(_civ[c] + give[c]); given += give[c]; }
        _soldiers -= given;
        reason += given;
        _prodAdults += given;
    }

    /// <summary>Split <paramref name="n"/> over cohorts [lo, hi) in proportion to <paramref name="weights"/> (largest
    /// remainder; ties by the lower cohort index). When taking, never more than a cohort holds; capped at the band total.</summary>
    public static long[] Proportional(long n, long[] weights, int lo, int hi)
    {
        var r = new long[weights.Length];
        long total = 0;
        for (int c = lo; c < hi; c++) total = checked(total + weights[c]);
        if (n <= 0) return r;
        if (total <= 0)
        {
            r[lo] = n;   // nobody in the band: a returning soldier lands in its first cohort (taking from an empty band is refused below)
            return r;
        }
        long assigned = 0;
        var frac = new long[weights.Length];
        for (int c = lo; c < hi; c++)
        {
            Int128 prod = (Int128)n * weights[c];
            r[c] = (long)(prod / total);
            frac[c] = (long)(prod % total);
            assigned += r[c];
        }
        long left = n - assigned;
        while (left > 0)
        {
            int best = -1;
            for (int c = lo; c < hi; c++)
                if (best < 0 || frac[c] > frac[best]) best = c;
            r[best]++;
            frac[best] = -1;
            left--;
        }
        return r;
    }

    // ------------------------------------------------------------------ grain flows

    private void FlowGrainIn(ref long reason, long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        _stock = checked(_stock + amount);
        reason = checked(reason + amount);
    }

    private void FlowGrainOut(ref long reason, long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount > _stock) throw new InvalidOperationException("grain overdraw");
        _stock -= amount;
        reason = checked(reason + amount);
    }

    /// <summary>Whole units of a flow with a remainder accumulator (the D-004 convention).</summary>
    private static long Carry(ref double rem, double exact)
    {
        if (!(exact >= 0.0) || !double.IsFinite(exact)) throw new ArgumentOutOfRangeException(nameof(exact));
        double x = exact + rem;
        double whole = Math.Floor(x);
        rem = x - whole;
        return (long)whole;
    }

    private static long Granted(LogisticsDecision d, long id)
    {
        for (int i = 0; i < d.Moves.Length; i++) if (d.Moves[i].Id == id) return d.Moves[i].Granted;
        return 0;
    }

    private static long GrantedDays(LogisticsDecision d, long id)
    {
        for (int i = 0; i < d.Moves.Length; i++) if (d.Moves[i].Id == id) return d.Moves[i].GrantedDays;
        return 0;
    }

    private long Adults()
    {
        long a = 0;
        for (int c = Cohorts.FirstAdult; c < Cohorts.FirstElder; c++) a += _civ[c];
        return a;
    }

    private double Nutritional()
    {
        double n = 0.0;
        for (int c = 0; c < N; c++) n += _p.CohortWeights[c] * _civ[c];
        return n;
    }

    private static string L(long v) => v.ToString(CultureInfo.InvariantCulture);

    private static long Sum(long[] a)
    {
        long s = 0;
        for (int i = 0; i < a.Length; i++) s = checked(s + a[i]);
        return s;
    }

    private void CheckConservation()
    {
        if (_ledger.GrainImplied != _stock) throw new InvalidOperationException("grain not conserved");
        if (_ledger.CiviliansImplied != Sum(_civ)) throw new InvalidOperationException("civilians not conserved");
        if (_ledger.SoldiersImplied != _soldiers) throw new InvalidOperationException("soldiers not conserved");
        if (_ledger.CreditImplied != _credit || _credit < 0) throw new InvalidOperationException("trade credit not conserved");
        if (_ledger.ImportedForCivilians < 0 || _ledger.ImportedForCivilians > _ledger.Imported) throw new InvalidOperationException("import split out of range");
        for (int c = 0; c < N; c++) if (_civ[c] < 0) throw new InvalidOperationException("negative cohort");
        if (_stock < 0 || _soldiers < 0) throw new InvalidOperationException("negative stock");
    }

    /// <summary>SHA-256 over every long of the final state and ledger (determinism evidence; not a logic input).</summary>
    private string StateHash()
    {
        var sb = new StringBuilder();
        for (int c = 0; c < N; c++) sb.Append(_civ[c].ToString(CultureInfo.InvariantCulture)).Append(',');
        sb.Append(_soldiers.ToString(CultureInfo.InvariantCulture)).Append(',').Append(_stock.ToString(CultureInfo.InvariantCulture));
        FoodLedger l = _ledger;
        long[] all = [l.GrainInitial, l.Harvest, l.EatenCivilian, l.Ration, l.Haul, l.Spoilage, l.Overflow, l.ScenarioReset, l.GrowthEaten,
            l.CiviliansInitial, l.SoldiersInitial, l.Births, l.Deaths, l.Starvation, l.SoldierStarvation, l.Mobilised, l.Demobilised, l.Deserted];
        for (int i = 0; i < all.Length; i++) sb.Append(',').Append(all[i].ToString(CultureInfo.InvariantCulture));
        // The trade counters are zero under every other rule (CheckConservation enforces credit = Exported − Imported ≥ 0
        // and only TradeCredit writes them), so they are hashed under TradeCredit only: every pre-existing run keeps its hash.
        if (IsTrade(_sc.Overflow))
            sb.Append(",T,").Append(l.Exported.ToString(CultureInfo.InvariantCulture)).Append(',').Append(l.Imported.ToString(CultureInfo.InvariantCulture))
              .Append(',').Append(_credit.ToString(CultureInfo.InvariantCulture)).Append(',').Append(l.CreditInitial.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }
}
