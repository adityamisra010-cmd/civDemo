using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Proto.M7.Econ;

/// <summary>An epoch of the battery: the real turn the runs start from and how far they run.</summary>
public sealed record EpochPlan(long StartTurn, int HorizonTurns);

/// <summary>A shortage regime applied during the window: harvest × e^(−k·σ) with σ = harvestVariance.sigmaLogYield (the
/// shipped yearly log-yield spread) and k = <see cref="SigmaMultiple"/>; optionally the store emptied at the start.</summary>
public sealed record RegimeSpec(string Name, int SigmaMultiple, bool EmptyGranary);

public sealed record FoodPlan(
    EpochPlan[] Epochs,
    string[] Armies,
    RegimeSpec[] Regimes,
    AllocationOrder[] Orders,
    OverflowRule[] Overflows,
    UnfedConsequence[] Consequences,
    bool MeanHaulSensitivity,
    bool Validation,
    bool GrowthSensitivity = false,
    bool GranularitySensitivity = false);

public sealed class FoodBattery
{
    public required BaselineSeries[] Series { get; init; }
    public required FoodModelParams Params { get; init; }
    public required FoodPlan Plan { get; init; }
    public List<FoodRunResult> Runs { get; } = new();
    public List<FoodRunResult> Validation { get; } = new();
}

/// <summary>Derived numbers for one run (and its PEACE counterpart, same seed/epoch/regime/order/rules).</summary>
public sealed record RunMetrics(
    long Pop20, long Pop100, long Pop300,
    double Gap20, double Gap100, double Gap300,
    long CivStarvation, long CivStarvationVsPeace, long Births, long BirthsVsPeace,
    double RationSupply, double MarchAvailability, double GrowthServed,
    double ShareGrowthReduced, double ShareMilitaryRestricted, long PeriodsCivilianShort,
    double FedSoldierYears, long SoldiersWarStart, long SoldiersWarEnd, long Deserted, long SoldierStarved,
    long MinStock, long EndStock, long OverflowWar, long SpoilageWar, long HaulWar, long RationWar,
    long OverflowHorizon, long SpoilageHorizon, int RecoveryTurn,
    double WholeRationSupply, double WholeGrowthServed, long WholeCivilianShortPeriods, long GrowthEatenHorizon,
    long ExportedWar = 0, long ImportedWar = 0, long ExportedHorizon = 0, long ImportedHorizon = 0, long CreditEnd = 0,
    long ImportedCivWar = 0, long ImportedCivHorizon = 0, long RationHorizon = 0, long HaulHorizon = 0, long OpeningCredit = 0);

/// <summary>
/// THE COMPARISON BATTERY (M7 Phase C lane F). Every run of a pair is identical except <see cref="AllocationOrder"/>.
/// Reporting code only (no world access): loops, no LINQ, culture-invariant formatting.
/// </summary>
public static class FoodComparison
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static readonly string[] AllArmies = ["PEACE", "GARRISON-500", "GARRISON-1000", "WAR-500", "WAR-1000", "WAR-LARGE"];

    public static FoodPlan DefaultPlan() => new(
        [new EpochPlan(25, 300), new EpochPlan(200, 100)],
        AllArmies,
        [new RegimeSpec("NORMAL", 0, false), new RegimeSpec("BAD-1s", 1, false), new RegimeSpec("SEVERE-2s", 2, false), new RegimeSpec("SEVERE-2s+EMPTY", 2, true)],
        [AllocationOrder.CivilianGrowthMilitary, AllocationOrder.CivilianMilitaryGrowth],
        // TradeCredit last: the rows of the three earlier rules keep their CSV positions and bytes (the 2026-11-05 battery).
        [OverflowRule.CapBeforeMilitary, OverflowRule.AllocateBeforeCap, OverflowRule.NoCap, OverflowRule.TradeCredit, OverflowRule.TradeCreditSinceFounding],
        [UnfedConsequence.None, UnfedConsequence.Desert, UnfedConsequence.Starve],
        MeanHaulSensitivity: true,
        Validation: true,
        GrowthSensitivity: true,
        GranularitySensitivity: true);

    /// <summary>Only the model-validation runs (PEACE, NORMAL, both granularities) — a fast check of the model's fidelity.</summary>
    public static FoodPlan ValidationPlan() => DefaultPlan() with { Armies = [], MeanHaulSensitivity = false, GrowthSensitivity = false, GranularitySensitivity = false };

    /// <summary>The scenario for one cell. Army sizes are TOTAL soldiers; the founding warband (the baseline's soldiers at
    /// T0) counts toward them. WAR-LARGE = the most the EXISTING recruitment rules accepted at T0 (the large labour twin).</summary>
    public static FoodScenario Scenario(BaselineSeries s, FoodModelParams p, EpochPlan e, string army, RegimeSpec regime,
        AllocationOrder order, OverflowRule overflow, UnfedConsequence consequence, HaulBasis haul,
        SettlementGranularity granularity = SettlementGranularity.WarPulse, GrowthClaim growth = GrowthClaim.Headroom)
    {
        long baseSoldiers = s.At(e.StartTurn).Soldiers;
        long window = (long)Math.Round(p.HarvestCorrelationYears * Sim.Core.Kernel.SimClock.YearDays);
        double mult = Math.Exp(-regime.SigmaMultiple * p.SigmaLogYield);
        long standing = baseSoldiers, war = 0;
        bool campaign = false;
        switch (army)
        {
            case "PEACE": break;
            case "GARRISON-500": standing = Math.Max(baseSoldiers, 500); break;
            case "GARRISON-1000": standing = Math.Max(baseSoldiers, 1000); break;
            case "WAR-500": war = Math.Max(0, 500 - baseSoldiers); campaign = true; break;
            case "WAR-1000": war = Math.Max(0, 1000 - baseSoldiers); campaign = true; break;
            case "WAR-LARGE": war = s.TwinsAt(e.StartTurn).Large.Removed; campaign = true; break;
            default: throw new ArgumentOutOfRangeException(nameof(army), army);
        }
        long opening = 0;
        if (overflow == OverflowRule.TradeCreditSinceFounding)
            for (long t = 1; t <= e.StartTurn; t++) opening = checked(opening + s.At(t).StepOverflow);   // the REAL world's GranaryOverflow to T0
        return new FoodScenario(army, regime.Name, e.StartTurn, e.HorizonTurns, standing, war, campaign, 0, window, mult,
            regime.EmptyGranary, order, overflow, consequence, granularity, haul, growth, opening);
    }

    /// <summary>Overflow rules of the per-campaign-turn sensitivity (TradeCredit last, as in the primary grid).</summary>
    private static readonly OverflowRule[] GranularityRules = [OverflowRule.CapBeforeMilitary, OverflowRule.AllocateBeforeCap, OverflowRule.TradeCredit, OverflowRule.TradeCreditSinceFounding];

    public static FoodBattery RunBattery(BaselineSeries[] series, FoodPlan plan) => RunBattery(series, plan, FoodModelParams.Shipped);

    public static FoodBattery RunBattery(BaselineSeries[] series, FoodPlan plan, FoodModelParams p)
    {
        var b = new FoodBattery { Series = series, Params = p, Plan = plan };
        for (int si = 0; si < series.Length; si++)
        {
            BaselineSeries s = series[si];
            for (int ei = 0; ei < plan.Epochs.Length; ei++)
            {
                EpochPlan e = plan.Epochs[ei];
                if (e.StartTurn + e.HorizonTurns >= s.Turns.Length) continue;
                for (int ai = 0; ai < plan.Armies.Length; ai++)
                    for (int ri = 0; ri < plan.Regimes.Length; ri++)
                        for (int oi = 0; oi < plan.Overflows.Length; oi++)
                            for (int ci = 0; ci < plan.Consequences.Length; ci++)
                                for (int qi = 0; qi < plan.Orders.Length; qi++)
                                    b.Runs.Add(FoodPulseModel.Run(s, Scenario(s, p, e, plan.Armies[ai], plan.Regimes[ri], plan.Orders[qi],
                                        plan.Overflows[oi], plan.Consequences[ci], HaulBasis.FullLoad), p));
                if (plan.MeanHaulSensitivity && ei == 0)
                {
                    string[] armies = ["PEACE", "WAR-500", "WAR-1000"];
                    for (int ai = 0; ai < armies.Length; ai++)
                        for (int ri = 0; ri < plan.Regimes.Length; ri++)
                            for (int ci = 0; ci < plan.Consequences.Length; ci++)
                                for (int qi = 0; qi < plan.Orders.Length; qi++)
                                    b.Runs.Add(FoodPulseModel.Run(s, Scenario(s, p, e, armies[ai], plan.Regimes[ri], plan.Orders[qi],
                                        OverflowRule.CapBeforeMilitary, plan.Consequences[ci], HaulBasis.MeanLoad), p));
                }
                if (plan.GrowthSensitivity)
                {
                    string[] armies = ["PEACE", "GARRISON-1000", "WAR-500", "WAR-1000"];
                    UnfedConsequence[] cons = [UnfedConsequence.None, UnfedConsequence.Desert];
                    for (int ai = 0; ai < armies.Length; ai++)
                        for (int ri = 0; ri < plan.Regimes.Length; ri++)
                            for (int ci = 0; ci < cons.Length; ci++)
                                for (int qi = 0; qi < plan.Orders.Length; qi++)
                                    b.Runs.Add(FoodPulseModel.Run(s, Scenario(s, p, e, armies[ai], plan.Regimes[ri], plan.Orders[qi],
                                        OverflowRule.CapBeforeMilitary, cons[ci], HaulBasis.FullLoad, SettlementGranularity.WarPulse, GrowthClaim.Consumed), p));
                }
                if (plan.GranularitySensitivity)
                {
                    string[] armies = ["PEACE", "GARRISON-500", "GARRISON-1000"];
                    OverflowRule[] rules = GranularityRules;
                    for (int ai = 0; ai < armies.Length; ai++)
                        for (int oi = 0; oi < rules.Length; oi++)
                            for (int qi = 0; qi < plan.Orders.Length; qi++)
                                b.Runs.Add(FoodPulseModel.Run(s, Scenario(s, p, e, armies[ai], plan.Regimes[0], plan.Orders[qi],
                                    rules[oi], UnfedConsequence.None, HaulBasis.FullLoad, SettlementGranularity.CampaignTurn), p));
                }
                if (plan.Validation)
                {
                    SettlementGranularity[] gs = [SettlementGranularity.WarPulse, SettlementGranularity.CampaignTurn];
                    for (int gi = 0; gi < gs.Length; gi++)
                        b.Validation.Add(FoodPulseModel.Run(s, Scenario(s, p, e, "PEACE", plan.Regimes[0], AllocationOrder.CivilianMilitaryGrowth,
                            OverflowRule.CapBeforeMilitary, UnfedConsequence.None, HaulBasis.FullLoad, gs[gi]), p));
                }
            }
        }
        return b;
    }

    // ------------------------------------------------------------------ metrics

    public static FoodRunResult? PeaceOf(FoodBattery b, FoodRunResult r)
    {
        FoodScenario x = r.Scenario;
        for (int i = 0; i < b.Runs.Count; i++)
        {
            FoodRunResult q = b.Runs[i];
            FoodScenario y = q.Scenario;
            if (q.Seed == r.Seed && y.Army == "PEACE" && y.Regime == x.Regime && y.StartTurn == x.StartTurn && y.HorizonTurns == x.HorizonTurns
                && y.Order == x.Order && y.Overflow == x.Overflow && y.Consequence == x.Consequence && y.Granularity == x.Granularity && y.Haul == x.Haul
                && y.Growth == x.Growth)
                return q;
        }
        return null;
    }

    public static RunMetrics Metrics(FoodRunResult r, FoodRunResult? peace, FoodModelParams p)
    {
        FoodCheckpoint? last = r.Checkpoints.Length > 0 ? r.Checkpoints[^1] : null;
        FoodCheckpoint? lastPeace = peace is not null && peace.Checkpoints.Length > 0 ? peace.Checkpoints[^1] : null;
        long Pop(FoodRunResult x, int k) => k < x.PopulationByTurn.Length ? x.PopulationByTurn[k] : -1;
        double Gap(int k)
        {
            if (peace is null || k >= r.PopulationByTurn.Length) return double.NaN;
            long a = r.PopulationByTurn[k], q = peace.PopulationByTurn[k];
            return q > 0 ? 100.0 * (a - q) / q : double.NaN;
        }
        AllocationStats w = r.War;
        double Ratio(long num, long den) => den > 0 ? num / (double)den : 1.0;
        int recovery = -1;
        if (peace is not null)
        {
            int n = r.PopulationByTurn.Length;
            for (int k = n - 1; k >= 1; k--)
            {
                long q = peace.PopulationByTurn[k];
                long a = r.PopulationByTurn[k];
                if (q <= 0 || Math.Abs(a - q) * 100 > q) break;   // outside 1 %
                recovery = k;
            }
        }
        long civStarve = last?.Ledger.Starvation ?? 0;
        long births = last?.Ledger.Births ?? 0;
        return new RunMetrics(
            Pop(r, 20), Pop(r, 100), Pop(r, 300), Gap(20), Gap(100), Gap(300),
            civStarve, lastPeace is null ? 0 : civStarve - lastPeace.Ledger.Starvation,
            births, lastPeace is null ? 0 : births - lastPeace.Ledger.Births,
            Ratio(w.RationGranted, w.RationRequested), Ratio(w.MarchDaysGranted, w.MarchDaysRequested), Ratio(w.GrowthGranted, w.GrowthDemand),
            w.Periods > 0 ? w.PeriodsGrowthReduced / (double)w.Periods : 0.0, w.Periods > 0 ? w.PeriodsMilitaryRestricted / (double)w.Periods : 0.0,
            w.PeriodsCivilianShort, w.RationGranted / p.UpkeepPerPersonYear, r.WarSoldiersAtStart, r.WarSoldiersAtEnd,
            last?.Ledger.Deserted ?? 0, last?.Ledger.SoldierStarvation ?? 0, r.WarMinStock, r.WarEndStock,
            r.WindowEndLedger.Overflow - r.WindowStartLedger.Overflow, r.WindowEndLedger.Spoilage - r.WindowStartLedger.Spoilage,
            r.WindowEndLedger.Haul - r.WindowStartLedger.Haul, r.WindowEndLedger.Ration - r.WindowStartLedger.Ration,
            last?.Ledger.Overflow ?? 0, last?.Ledger.Spoilage ?? 0, recovery,
            Ratio(r.Whole.RationGranted, r.Whole.RationRequested), Ratio(r.Whole.GrowthGranted, r.Whole.GrowthDemand), r.Whole.PeriodsCivilianShort,
            last?.Ledger.GrowthEaten ?? 0,
            r.WindowEndLedger.Exported - r.WindowStartLedger.Exported, r.WindowEndLedger.Imported - r.WindowStartLedger.Imported,
            last?.Ledger.Exported ?? 0, last?.Ledger.Imported ?? 0, r.FinalLedger.CreditImplied,
            r.WindowEndLedger.ImportedForCivilians - r.WindowStartLedger.ImportedForCivilians, r.FinalLedger.ImportedForCivilians,
            r.FinalLedger.Ration, r.FinalLedger.Haul, r.FinalLedger.CreditInitial);
    }

    // ------------------------------------------------------------------ CSV (every run; the determinism fingerprint)

    public static string Csv(FoodBattery b)
    {
        var sb = new StringBuilder();
        sb.Append("seed,army,regime,t0,horizon,order,overflow,consequence,granularity,haul,growth,pop20,pop100,pop300,gap20,gap100,gap300,civStarve,civStarveVsPeace,births,birthsVsPeace,")
          .Append("rationSupply,marchAvail,growthServed,shareGrowthReduced,shareMilRestricted,periodsCivShort,fedSoldierYears,soldiersWarStart,soldiersWarEnd,deserted,soldierStarved,")
          .Append("minStock,endStock,overflowWar,spoilageWar,haulWar,rationWar,overflowH,spoilageH,recoveryTurn,wholeRationSupply,wholeGrowthServed,wholeCivShortPeriods,growthEatenH,")
          .Append("windowStartDay,windowEndDay,finalCivilians,finalSoldiers,finalStock,stateHash\n");
        AppendRows(sb, b, b.Runs);
        AppendRows(sb, b, b.Validation);
        return sb.ToString();
    }

    private static void AppendRows(StringBuilder sb, FoodBattery b, List<FoodRunResult> runs)
    {
        for (int i = 0; i < runs.Count; i++)
        {
            FoodRunResult r = runs[i];
            FoodScenario s = r.Scenario;
            RunMetrics m = Metrics(r, ReferenceEquals(runs, b.Validation) ? null : PeaceOf(b, r), b.Params);
            sb.Append(r.Seed.ToString(Inv)).Append(',').Append(s.Army).Append(',').Append(s.Regime).Append(',').Append(L(s.StartTurn)).Append(',')
              .Append(s.HorizonTurns.ToString(Inv)).Append(',').Append(OrderTag(s.Order)).Append(',').Append(s.Overflow).Append(',').Append(s.Consequence)
              .Append(',').Append(s.Granularity).Append(',').Append(s.Haul).Append(',').Append(s.Growth).Append(',')
              .Append(L(m.Pop20)).Append(',').Append(L(m.Pop100)).Append(',').Append(L(m.Pop300)).Append(',').Append(F(m.Gap20, 3)).Append(',').Append(F(m.Gap100, 3)).Append(',').Append(F(m.Gap300, 3)).Append(',')
              .Append(L(m.CivStarvation)).Append(',').Append(L(m.CivStarvationVsPeace)).Append(',').Append(L(m.Births)).Append(',').Append(L(m.BirthsVsPeace)).Append(',')
              .Append(F(m.RationSupply, 6)).Append(',').Append(F(m.MarchAvailability, 6)).Append(',').Append(F(m.GrowthServed, 6)).Append(',').Append(F(m.ShareGrowthReduced, 6)).Append(',')
              .Append(F(m.ShareMilitaryRestricted, 6)).Append(',').Append(L(m.PeriodsCivilianShort)).Append(',').Append(F(m.FedSoldierYears, 3)).Append(',').Append(L(m.SoldiersWarStart)).Append(',')
              .Append(L(m.SoldiersWarEnd)).Append(',').Append(L(m.Deserted)).Append(',').Append(L(m.SoldierStarved)).Append(',').Append(L(m.MinStock)).Append(',').Append(L(m.EndStock)).Append(',')
              .Append(L(m.OverflowWar)).Append(',').Append(L(m.SpoilageWar)).Append(',').Append(L(m.HaulWar)).Append(',').Append(L(m.RationWar)).Append(',')
              .Append(L(m.OverflowHorizon)).Append(',').Append(L(m.SpoilageHorizon)).Append(',').Append(m.RecoveryTurn.ToString(Inv)).Append(',')
              .Append(F(m.WholeRationSupply, 6)).Append(',').Append(F(m.WholeGrowthServed, 6)).Append(',').Append(L(m.WholeCivilianShortPeriods)).Append(',').Append(L(m.GrowthEatenHorizon)).Append(',')
              .Append(L(r.WindowStartDayApplied)).Append(',').Append(L(r.WindowEndDayApplied)).Append(',').Append(L(r.FinalCivilians)).Append(',').Append(L(r.FinalSoldiers))
              .Append(',').Append(L(r.FinalGrainStock)).Append(',').Append(r.StateHash).Append('\n');
        }
    }

    public static string CsvHash(FoodBattery b) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Csv(b))));

    /// <summary>
    /// Verification round 1 (F-V11): the per-run TRADE record the main CSV leaves out — one row for every run under a trade
    /// rule, in battery order. A separate file so the main CSV keeps its bytes and hash. War = the window (as the main CSV);
    /// H = the whole horizon (the final ledger). importedCiv = bought back so that civilians could eat; imported − importedCiv
    /// = bought back for granted draws; ration/haul = the grain the army drew (granted), so (ration + haul + growthEaten) −
    /// (imported − importedCiv) = draws served from the physical store.
    /// </summary>
    public static string TradeCsv(FoodBattery b)
    {
        var sb = new StringBuilder();
        sb.Append("seed,army,regime,t0,horizon,order,overflow,consequence,granularity,haul,growth,openingCredit,")
          .Append("exportedWar,importedWar,importedCivWar,rationWar,haulWar,exportedH,importedH,importedCivH,rationH,haulH,growthEatenH,creditEnd,stateHash\n");
        for (int i = 0; i < b.Runs.Count; i++)
        {
            FoodRunResult r = b.Runs[i];
            FoodScenario s = r.Scenario;
            if (!FoodPulseModel.IsTrade(s.Overflow)) continue;
            FoodLedger w0 = r.WindowStartLedger, w1 = r.WindowEndLedger, f = r.FinalLedger;
            sb.Append(r.Seed.ToString(Inv)).Append(',').Append(s.Army).Append(',').Append(s.Regime).Append(',').Append(L(s.StartTurn)).Append(',')
              .Append(s.HorizonTurns.ToString(Inv)).Append(',').Append(OrderTag(s.Order)).Append(',').Append(s.Overflow).Append(',').Append(s.Consequence)
              .Append(',').Append(s.Granularity).Append(',').Append(s.Haul).Append(',').Append(s.Growth).Append(',').Append(L(f.CreditInitial)).Append(',')
              .Append(L(w1.Exported - w0.Exported)).Append(',').Append(L(w1.Imported - w0.Imported)).Append(',').Append(L(w1.ImportedForCivilians - w0.ImportedForCivilians)).Append(',')
              .Append(L(w1.Ration - w0.Ration)).Append(',').Append(L(w1.Haul - w0.Haul)).Append(',')
              .Append(L(f.Exported)).Append(',').Append(L(f.Imported)).Append(',').Append(L(f.ImportedForCivilians)).Append(',')
              .Append(L(f.Ration)).Append(',').Append(L(f.Haul)).Append(',').Append(L(f.GrowthEaten)).Append(',').Append(L(f.CreditImplied)).Append(',')
              .Append(r.StateHash).Append('\n');
        }
        return sb.ToString();
    }

    public static string TradeCsvHash(FoodBattery b) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(TradeCsv(b))));

    // ------------------------------------------------------------------ the generated report

    public static string Report(FoodBattery b, long elapsedMs)
    {
        var sb = new StringBuilder();
        FoodModelParams p = b.Params;
        sb.Append("# Lane F — food-allocation comparison: generated tables\n\n");
        sb.Append("Generated by `Proto.M7.Econ food-compare` from REAL Sim.Core baselines (`food-baseline`). ")
          .Append(L(b.Runs.Count)).Append(" battery runs + ").Append(L(b.Validation.Count)).Append(" validation runs in ").Append(L(elapsedMs))
          .Append(" ms. CSV SHA-256 `").Append(CsvHash(b)).Append("`.\n\n");
        sb.Append("Orders: **CGM** = civilian → growth → military; **CMG** = civilian → military → growth. Neither is adopted (D-079 Q3).\n\n");
        sb.Append("Shipped constants read: spoilage ").Append(F(p.SpoilagePerYear, 3)).Append("/yr, granary ").Append(F(p.GranaryYearsOfDemand, 2))
          .Append(" y of demand, upkeep ").Append(F(p.UpkeepPerPersonYear, 2)).Append("/person-yr, starvation max ").Append(F(p.StarvationMaxPerYear, 3))
          .Append("/yr, absorbable shortfall ").Append(F(p.AbsorbableShortfall, 2)).Append(", fertility suppression slope ").Append(F(p.FertilitySuppressionSlope, 1))
          .Append(", headroom k ").Append(F(p.HeadroomRelaxationPerYear, 4)).Append("/yr, sigmaLogYield ").Append(F(p.SigmaLogYield, 4))
          .Append(" (regime multipliers e^-1s ").Append(F(Math.Exp(-p.SigmaLogYield), 4)).Append(", e^-2s ").Append(F(Math.Exp(-2 * p.SigmaLogYield), 4))
          .Append("), window = correlationTimeYears ").Append(F(p.HarvestCorrelationYears, 1)).Append(" y; march: ").Append(F(p.March.BaselineKmPerDay, 0))
          .Append(" km/day, ").Append(F(p.March.KmPerCostUnit, 0)).Append(" km/cost unit, haul ").Append(F(p.March.HaulGrainPerBulkCostUnit, 2)).Append(" per bulk·cost unit.\n\n");

        InputsTable(sb, b);
        ValidationTable(sb, b);
        RealOverflowTables(sb, b);
        string[] armies = b.Plan.Armies;
        for (int ei = 0; ei < b.Plan.Epochs.Length; ei++)
        {
            EpochPlan e = b.Plan.Epochs[ei];
            HeadlineTables(sb, b, e, OverflowRule.CapBeforeMilitary, UnfedConsequence.None, HaulBasis.FullLoad, armies);
        }
        for (int ei = 0; ei < b.Plan.Epochs.Length; ei++)
            ConsequenceTable(sb, b, b.Plan.Epochs[ei]);
        for (int ei = 0; ei < b.Plan.Epochs.Length; ei++)
            OverflowTable(sb, b, b.Plan.Epochs[ei]);
        HaulTable(sb, b);
        DivergenceTable(sb, b);
        PairedDifferenceTable(sb, b);
        GrowthSemanticsTable(sb, b);
        GranularityTable(sb, b);
        TradeTable(sb, b);
        TradeSourceTable(sb, b);
        return sb.ToString();
    }

    private static void InputsTable(StringBuilder sb, FoodBattery b)
    {
        sb.Append("## 1. Inputs measured on the real worlds (per seed and epoch)\n\n");
        sb.Append("| seed | T0 | pop | adults | soldiers | grain stock | harvest/yr (step T0+1) | grain need /adult-eq/yr | headroom vacancy | granary cap | labour twin small: removed, marginal/adult-yr, eps1 | large: removed, eps2 | WAR-LARGE total |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        for (int si = 0; si < b.Series.Length; si++)
        {
            BaselineSeries s = b.Series[si];
            for (int ei = 0; ei < b.Plan.Epochs.Length; ei++)
            {
                EpochPlan e = b.Plan.Epochs[ei];
                if (e.StartTurn + e.HorizonTurns >= s.Turns.Length) continue;
                BaselineTurn t0 = s.At(e.StartTurn), t1 = s.At(e.StartTurn + 1);
                (LabourTwin small, LabourTwin large) = s.TwinsAt(e.StartTurn);
                double need = t1.StepGrainEaten / (t0.NutritionalPersons * t1.DtYears);
                double ap = small.BaseHarvestT2 / small.DtYears / s.At(e.StartTurn + 1).Adults;
                double eps1 = small.MarginalGrainPerAdultYear / ap;
                double eps2 = large.Removed > small.Removed
                    ? ((large.BaseHarvestT2 - large.TwinHarvestT2) - (small.BaseHarvestT2 - small.TwinHarvestT2)) / ((large.Removed - small.Removed) * large.DtYears) / ap
                    : eps1;
                sb.Append("| ").Append(s.Seed.ToString(Inv)).Append(" | ").Append(L(e.StartTurn)).Append(" | ").Append(L(t0.Population + t0.Soldiers))
                  .Append(" | ").Append(L(t0.Adults)).Append(" | ").Append(L(t0.Soldiers)).Append(" | ").Append(L(t0.GrainStock))
                  .Append(" | ").Append(F(t1.StepHarvest / t1.DtYears, 0)).Append(" | ").Append(F(need, 4))
                  .Append(" | ").Append(F(Math.Max(0, t0.HeadroomLimit - t0.NutritionalPersons), 0))
                  .Append(" | ").Append(F(Math.Floor(b.Params.GranaryYearsOfDemand * t0.NutritionalPersons * need), 0))
                  .Append(" | ").Append(L(small.Removed)).Append(", ").Append(F(small.MarginalGrainPerAdultYear, 3)).Append(", ").Append(F(eps1, 3))
                  .Append(" | ").Append(L(large.Removed)).Append(", ").Append(F(eps2, 3))
                  .Append(" | ").Append(L(large.Removed + t0.Soldiers)).Append(" |\n");
            }
        }
        sb.Append('\n');
    }

    private static void ValidationTable(StringBuilder sb, FoodBattery b)
    {
        sb.Append("## 2. Model validation against the real Sim.Core baseline (PEACE, NORMAL, existing overflow order)\n\n");
        sb.Append("Population (civilians + soldiers) and summed grain flows over the horizon; error = model − real.\n\n");
        sb.Append("| seed | T0 | granularity | longest period d | pop +20 model / real (err %) | pop +100 | pop +300 | Σ harvest model / real (err %) | Σ civ eaten | Σ spoil+overflow | end stock model / real |\n|---|---|---|---|---|---|---|---|---|---|---|\n");
        for (int i = 0; i < b.Validation.Count; i++)
        {
            FoodRunResult r = b.Validation[i];
            BaselineSeries s = SeriesOf(b, r.Seed);
            long t0 = r.Scenario.StartTurn;
            int hz = r.Scenario.HorizonTurns;
            FoodCheckpoint last = r.Checkpoints[^1];
            long realHarvest = 0, realEaten = 0, realLoss = 0;
            for (long t = t0 + 1; t <= t0 + last.TurnsAfter; t++)
            {
                BaselineTurn x = s.At(t);
                realHarvest += x.StepHarvest;
                realEaten += x.StepGrainEaten;
                realLoss += x.StepSpoilage + x.StepOverflow;
            }
            sb.Append("| ").Append(r.Seed.ToString(Inv)).Append(" | ").Append(L(t0)).Append(" | ").Append(r.Scenario.Granularity).Append(" | ").Append(L(r.LargestPeriodDays));
            int[] ks = [20, 100, 300];
            for (int j = 0; j < ks.Length; j++)
            {
                if (ks[j] > hz) { sb.Append(" | —"); continue; }
                long model = r.PopulationByTurn[ks[j]];
                BaselineTurn real = s.At(t0 + ks[j]);
                long rp = real.Population + real.Soldiers;
                sb.Append(" | ").Append(L(model)).Append(" / ").Append(L(rp)).Append(" (").Append(Pct(model - rp, rp)).Append(")");
            }
            sb.Append(" | ").Append(L(last.Ledger.Harvest)).Append(" / ").Append(L(realHarvest)).Append(" (").Append(Pct(last.Ledger.Harvest - realHarvest, realHarvest)).Append(")")
              .Append(" | ").Append(L(last.Ledger.EatenCivilian)).Append(" / ").Append(L(realEaten))
              .Append(" | ").Append(L(last.Ledger.Spoilage + last.Ledger.Overflow)).Append(" / ").Append(L(realLoss))
              .Append(" | ").Append(L(last.GrainStock)).Append(" / ").Append(L(s.At(t0 + last.TurnsAfter).GrainStock)).Append(" |\n");
        }
        sb.Append('\n');
    }

    private static void RealOverflowTables(StringBuilder sb, FoodBattery b)
    {
        sb.Append("## 3. Granary overflow in the REAL Sim.Core (measured; no model)\n\n");
        sb.Append("### 3a. Baseline (order-less world), the 20 turns after each epoch start\n\n");
        sb.Append("| seed | turns | Σ harvest | Σ civilian grain eaten | Σ spoilage | Σ granary overflow | overflow % of harvest | (spoilage+overflow) % of harvest | Σ formation upkeep |\n|---|---|---|---|---|---|---|---|---|\n");
        for (int si = 0; si < b.Series.Length; si++)
        {
            BaselineSeries s = b.Series[si];
            for (int ei = 0; ei < b.Plan.Epochs.Length; ei++)
            {
                long t0 = b.Plan.Epochs[ei].StartTurn;
                if (t0 + 20 >= s.Turns.Length) continue;
                long h = 0, e = 0, sp = 0, o = 0, u = 0;
                for (long t = t0 + 1; t <= t0 + 20; t++)
                {
                    BaselineTurn x = s.At(t);
                    h += x.StepHarvest; e += x.StepGrainEaten; sp += x.StepSpoilage; o += x.StepOverflow; u += x.StepUpkeep;
                }
                sb.Append("| ").Append(s.Seed.ToString(Inv)).Append(" | ").Append(L(t0 + 1)).Append("–").Append(L(t0 + 20)).Append(" | ").Append(L(h)).Append(" | ").Append(L(e))
                  .Append(" | ").Append(L(sp)).Append(" | ").Append(L(o)).Append(" | ").Append(Pct(o, h)).Append(" | ").Append(Pct(sp + o, h)).Append(" | ").Append(L(u)).Append(" |\n");
            }
        }
        sb.Append("\n### 3b. Real recruitment twins (RecruitFormation, Warband × 20): upkeep that went UNFED while overflow and spoilage destroyed grain in the same step\n\n");
        sb.Append("| seed | from turn | soldiers | turns | turns with unfed > 0 | Σ unfed units | Σ spoilage + overflow in those turns | Σ spoilage + overflow (all turns) | Σ civilian starvation | stock at end |\n|---|---|---|---|---|---|---|---|---|---|\n");
        for (int si = 0; si < b.Series.Length; si++)
        {
            BaselineSeries s = b.Series[si];
            for (int i = 0; i < s.OverflowTwins.Length; i++)
            {
                OverflowTwin o = s.OverflowTwins[i];
                long unfedTurns = 0, unfed = 0, lossUnfed = 0, loss = 0, starve = 0;
                for (int k = 0; k < o.Turns.Length; k++)
                {
                    OverflowTwinTurn x = o.Turns[k];
                    loss += x.Spoilage + x.Overflow;
                    starve += x.Starvation;
                    if (x.UnfedAccrued > 0) { unfedTurns++; unfed += x.UnfedAccrued; lossUnfed += x.Spoilage + x.Overflow; }
                }
                sb.Append("| ").Append(s.Seed.ToString(Inv)).Append(" | ").Append(L(o.AtTurn)).Append(" | ").Append(L(o.Turns.Length > 0 ? o.Turns[0].Soldiers : 0))
                  .Append(" | ").Append(L(o.Turns.Length)).Append(" | ").Append(L(unfedTurns)).Append(" | ").Append(L(unfed)).Append(" | ").Append(L(lossUnfed))
                  .Append(" | ").Append(L(loss)).Append(" | ").Append(L(starve)).Append(" | ").Append(L(o.Turns.Length > 0 ? o.Turns[^1].GrainStock : 0)).Append(" |\n");
            }
        }
        sb.Append('\n');
    }

    /// <summary>Mean over seeds of one metric for a cell.</summary>
    private static double Mean(List<RunMetrics> ms, Func<RunMetrics, double> f)
    {
        double s = 0.0;
        int n = 0;
        for (int i = 0; i < ms.Count; i++)
        {
            double v = f(ms[i]);
            if (double.IsNaN(v)) continue;
            s += v;
            n++;
        }
        return n > 0 ? s / n : double.NaN;
    }

    private static List<RunMetrics> Cell(FoodBattery b, long t0, string army, string regime, AllocationOrder order, OverflowRule overflow,
        UnfedConsequence consequence, HaulBasis haul, GrowthClaim growth = GrowthClaim.Headroom,
        SettlementGranularity granularity = SettlementGranularity.WarPulse)
    {
        var list = new List<RunMetrics>();
        for (int i = 0; i < b.Runs.Count; i++)
        {
            FoodRunResult r = b.Runs[i];
            FoodScenario s = r.Scenario;
            if (s.StartTurn != t0 || s.Army != army || s.Regime != regime || s.Order != order || s.Overflow != overflow
                || s.Consequence != consequence || s.Haul != haul || s.Granularity != granularity || s.Growth != growth) continue;
            list.Add(Metrics(r, PeaceOf(b, r), b.Params));
        }
        return list;
    }

    private static void HeadlineTables(StringBuilder sb, FoodBattery b, EpochPlan e, OverflowRule overflow, UnfedConsequence consequence,
        HaulBasis haul, string[] armies)
    {
        sb.Append("## 4. Headline — T0 = ").Append(L(e.StartTurn)).Append(", horizon ").Append(e.HorizonTurns.ToString(Inv))
          .Append(" turns; overflow rule ").Append(overflow).Append(" (existing), unfed consequence ").Append(consequence).Append(" (existing), haul ").Append(haul)
          .Append(" — mean over seeds\n\n");
        sb.Append("### 4a. The 3-year window (war or bad harvest): allocation outcomes\n\n");
        sb.Append("| army | regime | order | ration supplied % | march days granted % | growth claim served % | periods growth cut % | periods military restricted % | civ-short periods | fed soldier-years | soldiers start→end | min stock | overflow in window | spoilage in window | haul eaten in window |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        for (int ai = 0; ai < armies.Length; ai++)
            for (int ri = 0; ri < b.Plan.Regimes.Length; ri++)
                for (int qi = 0; qi < b.Plan.Orders.Length; qi++)
                {
                    List<RunMetrics> ms = Cell(b, e.StartTurn, armies[ai], b.Plan.Regimes[ri].Name, b.Plan.Orders[qi], overflow, consequence, haul);
                    if (ms.Count == 0) continue;
                    sb.Append("| ").Append(armies[ai]).Append(" | ").Append(b.Plan.Regimes[ri].Name).Append(" | ").Append(OrderTag(b.Plan.Orders[qi]))
                      .Append(" | ").Append(F(100 * Mean(ms, m => m.RationSupply), 1)).Append(" | ").Append(F(100 * Mean(ms, m => m.MarchAvailability), 1))
                      .Append(" | ").Append(F(100 * Mean(ms, m => m.GrowthServed), 1)).Append(" | ").Append(F(100 * Mean(ms, m => m.ShareGrowthReduced), 1))
                      .Append(" | ").Append(F(100 * Mean(ms, m => m.ShareMilitaryRestricted), 1)).Append(" | ").Append(F(Mean(ms, m => m.PeriodsCivilianShort), 1))
                      .Append(" | ").Append(F(Mean(ms, m => m.FedSoldierYears), 0)).Append(" | ").Append(F(Mean(ms, m => m.SoldiersWarStart), 0)).Append("→").Append(F(Mean(ms, m => m.SoldiersWarEnd), 0))
                      .Append(" | ").Append(F(Mean(ms, m => m.MinStock), 0)).Append(" | ").Append(F(Mean(ms, m => m.OverflowWar), 0))
                      .Append(" | ").Append(F(Mean(ms, m => m.SpoilageWar), 0)).Append(" | ").Append(F(Mean(ms, m => m.HaulWar), 0)).Append(" |\n");
                }
        sb.Append("\n### 4b. Population consequences (gap = vs PEACE in the same regime, same order)\n\n");
        sb.Append("| army | regime | order | pop +20 | gap +20 % | pop +100 | gap +100 % | pop +300 | gap +300 % | civ starvation (cum.) | of which vs PEACE | births vs PEACE | recovered (gap ≤1 % from turn) | Σ overflow | Σ spoilage |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        for (int ai = 0; ai < armies.Length; ai++)
            for (int ri = 0; ri < b.Plan.Regimes.Length; ri++)
                for (int qi = 0; qi < b.Plan.Orders.Length; qi++)
                {
                    List<RunMetrics> ms = Cell(b, e.StartTurn, armies[ai], b.Plan.Regimes[ri].Name, b.Plan.Orders[qi], overflow, consequence, haul);
                    if (ms.Count == 0) continue;
                    sb.Append("| ").Append(armies[ai]).Append(" | ").Append(b.Plan.Regimes[ri].Name).Append(" | ").Append(OrderTag(b.Plan.Orders[qi]))
                      .Append(" | ").Append(F(Mean(ms, m => m.Pop20), 0)).Append(" | ").Append(F(Mean(ms, m => m.Gap20), 2))
                      .Append(" | ").Append(F(Mean(ms, m => m.Pop100), 0)).Append(" | ").Append(F(Mean(ms, m => m.Gap100), 2))
                      .Append(" | ").Append(e.HorizonTurns >= 300 ? F(Mean(ms, m => m.Pop300), 0) : "—").Append(" | ").Append(e.HorizonTurns >= 300 ? F(Mean(ms, m => m.Gap300), 2) : "—")
                      .Append(" | ").Append(F(Mean(ms, m => m.CivStarvation), 0)).Append(" | ").Append(F(Mean(ms, m => m.CivStarvationVsPeace), 0))
                      .Append(" | ").Append(F(Mean(ms, m => m.BirthsVsPeace), 0)).Append(" | ").Append(RecoveryText(ms))
                      .Append(" | ").Append(F(Mean(ms, m => m.OverflowHorizon), 0)).Append(" | ").Append(F(Mean(ms, m => m.SpoilageHorizon), 0)).Append(" |\n");
                }
        sb.Append('\n');
    }

    private static string RecoveryText(List<RunMetrics> ms)
    {
        int never = 0, max = 0;
        for (int i = 0; i < ms.Count; i++)
        {
            if (ms[i].RecoveryTurn < 0) never++;
            else max = Math.Max(max, ms[i].RecoveryTurn);
        }
        return never == ms.Count ? "never" : (never > 0 ? "≤" + max.ToString(Inv) + " (" + never.ToString(Inv) + " never)" : "≤" + max.ToString(Inv));
    }

    private static void ConsequenceTable(StringBuilder sb, FoodBattery b, EpochPlan e)
    {
        sb.Append("## 5. Unfed-soldier consequence (PROPOSALS) — T0 = ").Append(L(e.StartTurn)).Append(", overflow CapBeforeMilitary, mean over seeds\n\n");
        sb.Append("| army | regime | consequence | order | ration supplied % | soldiers start→end | deserted (cum.) | soldiers starved (cum.) | fed soldier-years | civ starvation vs PEACE | gap +20 % | gap +100 % |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        string[] armies = ["GARRISON-1000", "WAR-500", "WAR-1000", "WAR-LARGE"];
        for (int ai = 0; ai < armies.Length; ai++)
            for (int ri = 0; ri < b.Plan.Regimes.Length; ri++)
                for (int ci = 0; ci < b.Plan.Consequences.Length; ci++)
                    for (int qi = 0; qi < b.Plan.Orders.Length; qi++)
                    {
                        List<RunMetrics> ms = Cell(b, e.StartTurn, armies[ai], b.Plan.Regimes[ri].Name, b.Plan.Orders[qi], OverflowRule.CapBeforeMilitary,
                            b.Plan.Consequences[ci], HaulBasis.FullLoad);
                        if (ms.Count == 0) continue;
                        sb.Append("| ").Append(armies[ai]).Append(" | ").Append(b.Plan.Regimes[ri].Name).Append(" | ").Append(b.Plan.Consequences[ci]).Append(" | ").Append(OrderTag(b.Plan.Orders[qi]))
                          .Append(" | ").Append(F(100 * Mean(ms, m => m.RationSupply), 1)).Append(" | ").Append(F(Mean(ms, m => m.SoldiersWarStart), 0)).Append("→").Append(F(Mean(ms, m => m.SoldiersWarEnd), 0))
                          .Append(" | ").Append(F(Mean(ms, m => m.Deserted), 0)).Append(" | ").Append(F(Mean(ms, m => m.SoldierStarved), 0)).Append(" | ").Append(F(Mean(ms, m => m.FedSoldierYears), 0))
                          .Append(" | ").Append(F(Mean(ms, m => m.CivStarvationVsPeace), 0)).Append(" | ").Append(F(Mean(ms, m => m.Gap20), 2)).Append(" | ").Append(F(Mean(ms, m => m.Gap100), 2)).Append(" |\n");
                    }
        sb.Append('\n');
    }

    private static void OverflowTable(StringBuilder sb, FoodBattery b, EpochPlan e)
    {
        sb.Append("## 6. Granary overflow rule (existing vs PROPOSALS) — T0 = ").Append(L(e.StartTurn)).Append(", consequence None, mean over seeds\n\n");
        sb.Append("TradeCredit and TradeCreditSinceFounding (PROPOSALS, bounding) sink nothing: their overflow is EXPORTED for a credit and bought back when short (columns exported / imported).\n\n");
        sb.Append("| army | regime | overflow rule | order | overflow in window | spoilage in window | ration supplied % | march granted % | growth served % | min stock | Σ overflow (horizon) | Σ spoilage (horizon) | pop +100 | gap +100 % | exported in window | imported in window | Σ exported (horizon) | Σ imported (horizon) |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        string[] armies = ["PEACE", "GARRISON-1000", "WAR-500", "WAR-1000"];
        for (int ai = 0; ai < armies.Length; ai++)
            for (int ri = 0; ri < b.Plan.Regimes.Length; ri++)
                for (int oi = 0; oi < b.Plan.Overflows.Length; oi++)
                    for (int qi = 0; qi < b.Plan.Orders.Length; qi++)
                    {
                        List<RunMetrics> ms = Cell(b, e.StartTurn, armies[ai], b.Plan.Regimes[ri].Name, b.Plan.Orders[qi], b.Plan.Overflows[oi],
                            UnfedConsequence.None, HaulBasis.FullLoad);
                        if (ms.Count == 0) continue;
                        sb.Append("| ").Append(armies[ai]).Append(" | ").Append(b.Plan.Regimes[ri].Name).Append(" | ").Append(b.Plan.Overflows[oi]).Append(" | ").Append(OrderTag(b.Plan.Orders[qi]))
                          .Append(" | ").Append(F(Mean(ms, m => m.OverflowWar), 0)).Append(" | ").Append(F(Mean(ms, m => m.SpoilageWar), 0))
                          .Append(" | ").Append(F(100 * Mean(ms, m => m.RationSupply), 1)).Append(" | ").Append(F(100 * Mean(ms, m => m.MarchAvailability), 1))
                          .Append(" | ").Append(F(100 * Mean(ms, m => m.GrowthServed), 1)).Append(" | ").Append(F(Mean(ms, m => m.MinStock), 0))
                          .Append(" | ").Append(F(Mean(ms, m => m.OverflowHorizon), 0)).Append(" | ").Append(F(Mean(ms, m => m.SpoilageHorizon), 0))
                          .Append(" | ").Append(F(Mean(ms, m => m.Pop100), 0)).Append(" | ").Append(F(Mean(ms, m => m.Gap100), 2))
                          .Append(" | ").Append(F(Mean(ms, m => m.ExportedWar), 0)).Append(" | ").Append(F(Mean(ms, m => m.ImportedWar), 0))
                          .Append(" | ").Append(F(Mean(ms, m => m.ExportedHorizon), 0)).Append(" | ").Append(F(Mean(ms, m => m.ImportedHorizon), 0)).Append(" |\n");
                    }
        sb.Append('\n');
    }

    private static void HaulTable(StringBuilder sb, FoodBattery b)
    {
        if (!b.Plan.MeanHaulSensitivity) return;
        EpochPlan e = b.Plan.Epochs[0];
        sb.Append("## 7. Haul basis sensitivity (FULL vs MEAN load; the choice is open) — T0 = ").Append(L(e.StartTurn)).Append(", CapBeforeMilitary, consequence None\n\n");
        sb.Append("| army | regime | haul | order | ration supplied % | march granted % | growth served % | haul eaten in window | gap +20 % | gap +100 % |\n|---|---|---|---|---|---|---|---|---|---|\n");
        string[] armies = ["WAR-500", "WAR-1000"];
        HaulBasis[] hs = [HaulBasis.FullLoad, HaulBasis.MeanLoad];
        for (int ai = 0; ai < armies.Length; ai++)
            for (int ri = 0; ri < b.Plan.Regimes.Length; ri++)
                for (int hi = 0; hi < hs.Length; hi++)
                    for (int qi = 0; qi < b.Plan.Orders.Length; qi++)
                    {
                        List<RunMetrics> ms = Cell(b, e.StartTurn, armies[ai], b.Plan.Regimes[ri].Name, b.Plan.Orders[qi], OverflowRule.CapBeforeMilitary, UnfedConsequence.None, hs[hi]);
                        if (ms.Count == 0) continue;
                        sb.Append("| ").Append(armies[ai]).Append(" | ").Append(b.Plan.Regimes[ri].Name).Append(" | ").Append(hs[hi]).Append(" | ").Append(OrderTag(b.Plan.Orders[qi]))
                          .Append(" | ").Append(F(100 * Mean(ms, m => m.RationSupply), 1)).Append(" | ").Append(F(100 * Mean(ms, m => m.MarchAvailability), 1))
                          .Append(" | ").Append(F(100 * Mean(ms, m => m.GrowthServed), 1)).Append(" | ").Append(F(Mean(ms, m => m.HaulWar), 0))
                          .Append(" | ").Append(F(Mean(ms, m => m.Gap20), 2)).Append(" | ").Append(F(Mean(ms, m => m.Gap100), 2)).Append(" |\n");
                    }
        sb.Append('\n');
    }

    /// <summary>Where the orders diverge QUALITATIVELY, pair by pair (same seed and everything else): counts of seeds.</summary>
    private static void DivergenceTable(StringBuilder sb, FoodBattery b)
    {
        sb.Append("## 8. Qualitative divergence between the orders (pairs identical except the order; counts of seeds)\n\n");
        sb.Append("Columns: **pairs** = seeds with both runs; **ident** = byte-identical final state; **ration** = one order feeds the army in full and the other does not; ")
          .Append("**growth** = one serves the growth claim in full and the other does not; **civ starve** = civilian starvation (vs PEACE) > 0 under one order only; ")
          .Append("**soldiers** = different army size at the window's end; **pop +20 / +100 / +300: CGM>CMG** = seeds where CGM ends with more people; **crossover** = the sign of (CGM − CMG) population flips between +20 and the last checkpoint.\n\n");
        sb.Append("| T0 | army | regime | overflow | consequence | pairs | ident | ration | growth | civ starve | soldiers | pop+20 CGM>CMG | pop+100 CGM>CMG | pop+300 CGM>CMG | crossover |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        for (int ei = 0; ei < b.Plan.Epochs.Length; ei++)
            for (int ai = 0; ai < b.Plan.Armies.Length; ai++)
                for (int ri = 0; ri < b.Plan.Regimes.Length; ri++)
                    for (int oi = 0; oi < b.Plan.Overflows.Length; oi++)
                        for (int ci = 0; ci < b.Plan.Consequences.Length; ci++)
                        {
                            EpochPlan e = b.Plan.Epochs[ei];
                            int pairs = 0, ident = 0, ration = 0, growth = 0, starve = 0, soldiers = 0, p20 = 0, p100 = 0, p300 = 0, cross = 0;
                            for (int si = 0; si < b.Series.Length; si++)
                            {
                                FoodRunResult? a = Find(b, b.Series[si].Seed, e.StartTurn, b.Plan.Armies[ai], b.Plan.Regimes[ri].Name, AllocationOrder.CivilianGrowthMilitary, b.Plan.Overflows[oi], b.Plan.Consequences[ci]);
                                FoodRunResult? m = Find(b, b.Series[si].Seed, e.StartTurn, b.Plan.Armies[ai], b.Plan.Regimes[ri].Name, AllocationOrder.CivilianMilitaryGrowth, b.Plan.Overflows[oi], b.Plan.Consequences[ci]);
                                if (a is null || m is null) continue;
                                pairs++;
                                if (a.StateHash == m.StateHash) ident++;
                                RunMetrics ma = Metrics(a, PeaceOf(b, a), b.Params), mm = Metrics(m, PeaceOf(b, m), b.Params);
                                if ((ma.RationSupply >= 1.0) != (mm.RationSupply >= 1.0)) ration++;
                                if ((ma.GrowthServed >= 1.0) != (mm.GrowthServed >= 1.0)) growth++;
                                if ((ma.CivStarvationVsPeace > 0) != (mm.CivStarvationVsPeace > 0)) starve++;
                                if (ma.SoldiersWarEnd != mm.SoldiersWarEnd) soldiers++;
                                if (ma.Pop20 > mm.Pop20) p20++;
                                if (ma.Pop100 > mm.Pop100) p100++;
                                if (ma.Pop300 > mm.Pop300) p300++;
                                long d20 = ma.Pop20 - mm.Pop20;
                                long dLast = e.HorizonTurns >= 300 ? ma.Pop300 - mm.Pop300 : ma.Pop100 - mm.Pop100;
                                if ((d20 > 0 && dLast < 0) || (d20 < 0 && dLast > 0)) cross++;
                            }
                            if (pairs == 0) continue;
                            sb.Append("| ").Append(L(e.StartTurn)).Append(" | ").Append(b.Plan.Armies[ai]).Append(" | ").Append(b.Plan.Regimes[ri].Name).Append(" | ").Append(b.Plan.Overflows[oi])
                              .Append(" | ").Append(b.Plan.Consequences[ci]).Append(" | ").Append(I(pairs)).Append(" | ").Append(I(ident)).Append(" | ").Append(I(ration)).Append(" | ").Append(I(growth))
                              .Append(" | ").Append(I(starve)).Append(" | ").Append(I(soldiers)).Append(" | ").Append(I(p20)).Append(" | ").Append(I(p100))
                              .Append(" | ").Append(e.HorizonTurns >= 300 ? I(p300) : "—").Append(" | ").Append(I(cross)).Append(" |\n");
                        }
        sb.Append('\n');
    }

    /// <summary>Per-seed paired differences CGM − CMG (identical inputs): mean and the range over seeds.</summary>
    private static void PairedDifferenceTable(StringBuilder sb, FoodBattery b)
    {
        sb.Append("## 9. Paired differences CGM − CMG, per seed (CapBeforeMilitary, consequence None, FULL haul): mean [min, max] over seeds\n\n");
        sb.Append("| T0 | army | regime | Δ ration supplied pp | Δ march granted pp | Δ growth served pp | Δ fed soldier-years | Δ min stock | Δ pop +20 | Δ pop +100 | Δ pop +300 | Δ civ starvation | Δ births |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        string[] armies = ["GARRISON-500", "GARRISON-1000", "WAR-500", "WAR-1000", "WAR-LARGE"];
        for (int ei = 0; ei < b.Plan.Epochs.Length; ei++)
            for (int ai = 0; ai < armies.Length; ai++)
                for (int ri = 0; ri < b.Plan.Regimes.Length; ri++)
                {
                    EpochPlan e = b.Plan.Epochs[ei];
                    var d = new List<double[]>();
                    for (int si = 0; si < b.Series.Length; si++)
                    {
                        FoodRunResult? a = Find(b, b.Series[si].Seed, e.StartTurn, armies[ai], b.Plan.Regimes[ri].Name, AllocationOrder.CivilianGrowthMilitary, OverflowRule.CapBeforeMilitary, UnfedConsequence.None);
                        FoodRunResult? m = Find(b, b.Series[si].Seed, e.StartTurn, armies[ai], b.Plan.Regimes[ri].Name, AllocationOrder.CivilianMilitaryGrowth, OverflowRule.CapBeforeMilitary, UnfedConsequence.None);
                        if (a is null || m is null) continue;
                        RunMetrics x = Metrics(a, PeaceOf(b, a), b.Params), y = Metrics(m, PeaceOf(b, m), b.Params);
                        d.Add([100 * (x.RationSupply - y.RationSupply), 100 * (x.MarchAvailability - y.MarchAvailability), 100 * (x.GrowthServed - y.GrowthServed),
                            x.FedSoldierYears - y.FedSoldierYears, x.MinStock - y.MinStock, x.Pop20 - y.Pop20, x.Pop100 - y.Pop100,
                            e.HorizonTurns >= 300 ? x.Pop300 - y.Pop300 : double.NaN, x.CivStarvation - y.CivStarvation, x.Births - y.Births]);
                    }
                    if (d.Count == 0) continue;
                    sb.Append("| ").Append(L(e.StartTurn)).Append(" | ").Append(armies[ai]).Append(" | ").Append(b.Plan.Regimes[ri].Name);
                    int[] dec = [1, 1, 1, 0, 0, 0, 0, 0, 0, 0];
                    for (int k = 0; k < 10; k++) sb.Append(" | ").Append(Range(d, k, dec[k]));
                    sb.Append(" |\n");
                }
        sb.Append('\n');
    }

    private static string Range(List<double[]> rows, int k, int dec)
    {
        double sum = 0, min = double.PositiveInfinity, max = double.NegativeInfinity;
        int n = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            double v = rows[i][k];
            if (double.IsNaN(v)) continue;
            sum += v; n++;
            min = Math.Min(min, v); max = Math.Max(max, v);
        }
        if (n == 0) return "—";
        return F(sum / n, dec) + " [" + F(min, dec) + ", " + F(max, dec) + "]";
    }

    private static void GrowthSemanticsTable(StringBuilder sb, FoodBattery b)
    {
        if (!b.Plan.GrowthSensitivity) return;
        sb.Append("## 10. Growth-claim semantics (BOUNDING SENSITIVITY): the granted growth claim is EATEN (no Sim.Core counterpart) — CapBeforeMilitary, FULL haul, mean over seeds\n\n");
        sb.Append("Gaps are against PEACE under the same semantics (which itself differs from the real baseline: growth eats what would otherwise be stored or overflow).\n\n");
        sb.Append("| T0 | army | regime | consequence | order | ration supplied % | march granted % | growth served % | civ-short periods | soldiers start→end | growth eaten (horizon) | Σ overflow | pop +20 | gap +20 % | pop +100 | gap +100 % |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        string[] armies = ["PEACE", "GARRISON-1000", "WAR-500", "WAR-1000"];
        UnfedConsequence[] cons = [UnfedConsequence.None, UnfedConsequence.Desert];
        for (int ei = 0; ei < b.Plan.Epochs.Length; ei++)
            for (int ai = 0; ai < armies.Length; ai++)
                for (int ri = 0; ri < b.Plan.Regimes.Length; ri++)
                    for (int ci = 0; ci < cons.Length; ci++)
                        for (int qi = 0; qi < b.Plan.Orders.Length; qi++)
                        {
                            EpochPlan e = b.Plan.Epochs[ei];
                            List<RunMetrics> ms = Cell(b, e.StartTurn, armies[ai], b.Plan.Regimes[ri].Name, b.Plan.Orders[qi], OverflowRule.CapBeforeMilitary, cons[ci],
                                HaulBasis.FullLoad, GrowthClaim.Consumed);
                            if (ms.Count == 0) continue;
                            sb.Append("| ").Append(L(e.StartTurn)).Append(" | ").Append(armies[ai]).Append(" | ").Append(b.Plan.Regimes[ri].Name).Append(" | ").Append(cons[ci])
                              .Append(" | ").Append(OrderTag(b.Plan.Orders[qi])).Append(" | ").Append(F(100 * Mean(ms, m => m.RationSupply), 1))
                              .Append(" | ").Append(F(100 * Mean(ms, m => m.MarchAvailability), 1)).Append(" | ").Append(F(100 * Mean(ms, m => m.GrowthServed), 1))
                              .Append(" | ").Append(F(Mean(ms, m => m.PeriodsCivilianShort), 1)).Append(" | ").Append(F(Mean(ms, m => m.SoldiersWarStart), 0)).Append("→").Append(F(Mean(ms, m => m.SoldiersWarEnd), 0))
                              .Append(" | ").Append(F(Mean(ms, m => m.GrowthEatenHorizon), 0)).Append(" | ").Append(F(Mean(ms, m => m.OverflowHorizon), 0))
                              .Append(" | ").Append(F(Mean(ms, m => m.Pop20), 0)).Append(" | ").Append(F(Mean(ms, m => m.Gap20), 2))
                              .Append(" | ").Append(F(Mean(ms, m => m.Pop100), 0)).Append(" | ").Append(F(Mean(ms, m => m.Gap100), 2)).Append(" |\n");
                        }
        sb.Append('\n');
    }

    private static void GranularityTable(StringBuilder sb, FoodBattery b)
    {
        if (!b.Plan.GranularitySensitivity) return;
        sb.Append("## 11. Settlement granularity: one allocation per CAMPAIGN TURN (the shipped pipeline's period) vs per War Pulse — standing armies, NORMAL, consequence None, mean over seeds\n\n");
        sb.Append("Whole-horizon allocation outcomes (the standing army is fed every period of the horizon).\n\n");
        sb.Append("| T0 | army | granularity | overflow rule | order | ration supplied % (whole run) | growth served % (whole run) | civ-short periods | Σ overflow | Σ spoilage | pop +20 | gap +20 % | pop +100 | gap +100 % | Σ exported | Σ imported |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        string[] armies = ["PEACE", "GARRISON-500", "GARRISON-1000"];
        OverflowRule[] rules = GranularityRules;
        SettlementGranularity[] gs = [SettlementGranularity.WarPulse, SettlementGranularity.CampaignTurn];
        for (int ei = 0; ei < b.Plan.Epochs.Length; ei++)
            for (int ai = 0; ai < armies.Length; ai++)
                for (int gi = 0; gi < gs.Length; gi++)
                    for (int oi = 0; oi < rules.Length; oi++)
                        for (int qi = 0; qi < b.Plan.Orders.Length; qi++)
                        {
                            EpochPlan e = b.Plan.Epochs[ei];
                            List<RunMetrics> ms = Cell(b, e.StartTurn, armies[ai], b.Plan.Regimes[0].Name, b.Plan.Orders[qi], rules[oi], UnfedConsequence.None,
                                HaulBasis.FullLoad, GrowthClaim.Headroom, gs[gi]);
                            if (ms.Count == 0) continue;
                            sb.Append("| ").Append(L(e.StartTurn)).Append(" | ").Append(armies[ai]).Append(" | ").Append(gs[gi]).Append(" | ").Append(rules[oi])
                              .Append(" | ").Append(OrderTag(b.Plan.Orders[qi])).Append(" | ").Append(F(100 * Mean(ms, m => m.WholeRationSupply), 1))
                              .Append(" | ").Append(F(100 * Mean(ms, m => m.WholeGrowthServed), 1)).Append(" | ").Append(F(Mean(ms, m => m.WholeCivilianShortPeriods), 1))
                              .Append(" | ").Append(F(Mean(ms, m => m.OverflowHorizon), 0)).Append(" | ").Append(F(Mean(ms, m => m.SpoilageHorizon), 0))
                              .Append(" | ").Append(F(Mean(ms, m => m.Pop20), 0)).Append(" | ").Append(F(Mean(ms, m => m.Gap20), 2))
                              .Append(" | ").Append(F(Mean(ms, m => m.Pop100), 0)).Append(" | ").Append(F(Mean(ms, m => m.Gap100), 2))
                              .Append(" | ").Append(F(Mean(ms, m => m.ExportedHorizon), 0)).Append(" | ").Append(F(Mean(ms, m => m.ImportedHorizon), 0)).Append(" |\n");
                        }
        sb.Append('\n');
    }

    /// <summary>The TRADE fate of the overflow (TradeCredit, PROPOSAL bounding) against the existing loss flow
    /// (CapBeforeMilitary, same timing): what a lossless outside market would give back, per order.</summary>
    private static void TradeTable(StringBuilder sb, FoodBattery b)
    {
        bool any = false;
        for (int i = 0; i < b.Plan.Overflows.Length; i++) any |= FoodPulseModel.IsTrade(b.Plan.Overflows[i]);
        if (!any) return;
        sb.Append("## 12. Fate of the overflow: TRADED (TradeCredit, TradeCreditSinceFounding: PROPOSALS, bounding) against LOST (CapBeforeMilitary, existing) — same timing, per War Pulse, FULL haul, mean over seeds\n\n");
        sb.Append("TradeCredit sells the grain above the capacity at the numeraire price 1 (D-033) for a credit and buys it back at the same price, with no transport cost, ")
          .Append("whenever the store cannot cover civilians or a granted claim. Its credit opens at 0 at T0. TradeCreditSinceFounding opens it at the grain the REAL world ")
          .Append("destroyed as GranaryOverflow in turns 1..T0 (an upper estimate). Credit at end = opening + Σ exported − Σ imported.\n\n");
        sb.Append("| T0 | army | regime | consequence | rule | order | ration supplied % | march granted % | growth served % | civ-short periods | soldiers start→end | exported in window | imported in window | Σ overflow sunk (horizon) | Σ exported (horizon) | Σ imported (horizon) | credit at end | pop +100 | gap +100 % |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        string[] armies = ["PEACE", "GARRISON-1000", "WAR-500", "WAR-1000", "WAR-LARGE"];
        UnfedConsequence[] cons = [UnfedConsequence.None, UnfedConsequence.Desert];
        OverflowRule[] rules = [OverflowRule.CapBeforeMilitary, OverflowRule.TradeCredit, OverflowRule.TradeCreditSinceFounding];
        for (int ei = 0; ei < b.Plan.Epochs.Length; ei++)
            for (int ai = 0; ai < armies.Length; ai++)
                for (int ri = 0; ri < b.Plan.Regimes.Length; ri++)
                    for (int ci = 0; ci < cons.Length; ci++)
                        for (int oi = 0; oi < rules.Length; oi++)
                            for (int qi = 0; qi < b.Plan.Orders.Length; qi++)
                            {
                                EpochPlan e = b.Plan.Epochs[ei];
                                if (armies[ai] == "PEACE" && cons[ci] != UnfedConsequence.None) continue;
                                List<RunMetrics> ms = Cell(b, e.StartTurn, armies[ai], b.Plan.Regimes[ri].Name, b.Plan.Orders[qi], rules[oi], cons[ci], HaulBasis.FullLoad);
                                if (ms.Count == 0) continue;
                                sb.Append("| ").Append(L(e.StartTurn)).Append(" | ").Append(armies[ai]).Append(" | ").Append(b.Plan.Regimes[ri].Name).Append(" | ").Append(cons[ci])
                                  .Append(" | ").Append(rules[oi]).Append(" | ").Append(OrderTag(b.Plan.Orders[qi]))
                                  .Append(" | ").Append(F(100 * Mean(ms, m => m.RationSupply), 1)).Append(" | ").Append(F(100 * Mean(ms, m => m.MarchAvailability), 1))
                                  .Append(" | ").Append(F(100 * Mean(ms, m => m.GrowthServed), 1)).Append(" | ").Append(F(Mean(ms, m => m.PeriodsCivilianShort), 1))
                                  .Append(" | ").Append(F(Mean(ms, m => m.SoldiersWarStart), 0)).Append("→").Append(F(Mean(ms, m => m.SoldiersWarEnd), 0))
                                  .Append(" | ").Append(F(Mean(ms, m => m.ExportedWar), 0)).Append(" | ").Append(F(Mean(ms, m => m.ImportedWar), 0))
                                  .Append(" | ").Append(F(Mean(ms, m => m.OverflowHorizon), 0)).Append(" | ").Append(F(Mean(ms, m => m.ExportedHorizon), 0))
                                  .Append(" | ").Append(F(Mean(ms, m => m.ImportedHorizon), 0)).Append(" | ").Append(F(Mean(ms, m => m.CreditEnd), 0))
                                  .Append(" | ").Append(F(Mean(ms, m => m.Pop100), 0)).Append(" | ").Append(F(Mean(ms, m => m.Gap100), 2)).Append(" |\n");
                            }
        sb.Append('\n');
    }

    /// <summary>Verification round 1 (F-V1): where the army's grain came from under TradeCredit — bought back with the
    /// credit, or drawn from the physical store that the loss flow leaves to the growth-headroom claim. Per campaign turn
    /// (whole horizon; the §11 cells) and per War Pulse (the war window; the §6.6 cells).</summary>
    private static void TradeSourceTable(StringBuilder sb, FoodBattery b)
    {
        bool any = false;
        for (int i = 0; i < b.Plan.Overflows.Length; i++) any |= b.Plan.Overflows[i] == OverflowRule.TradeCredit;
        if (!any) return;
        sb.Append("## 13. Where the army's grain came from under TradeCredit: bought back, or released from the physical store (verification round 1) — consequence None, FULL haul, mean over seeds\n\n");
        sb.Append("Trade CSV SHA-256 `").Append(TradeCsvHash(b)).Append("` (one row per trade-rule run; per-seed values of every trade column).\n\n");
        sb.Append("The allocator under TradeCredit counts store + credit. The growth-headroom grant is not a physical draw, so it can be granted against the credit ")
          .Append("while the physical store is released to the army: an accounting effect that needs no import. Draws = ration + haul granted (growth is headroom here). ")
          .Append("Bought back = imports for draws (imports for civilians are shown separately). From the store = draws − bought back. ")
          .Append("Span: the whole horizon per campaign turn, the war window per War Pulse.\n\n");
        sb.Append("| granularity | T0 | army | regime | order | draws, loss flow (CapBeforeMilitary) | draws, TradeCredit | of which bought back | of which from the store | store share % | imports for civilians | growth served %, loss → trade |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        (SettlementGranularity G, string Army, string Regime)[] rows =
        [
            (SettlementGranularity.CampaignTurn, "PEACE", "NORMAL"), (SettlementGranularity.CampaignTurn, "GARRISON-500", "NORMAL"),
            (SettlementGranularity.CampaignTurn, "GARRISON-1000", "NORMAL"),
            (SettlementGranularity.WarPulse, "GARRISON-1000", "SEVERE-2s"), (SettlementGranularity.WarPulse, "WAR-500", "BAD-1s"),
            (SettlementGranularity.WarPulse, "WAR-1000", "NORMAL"), (SettlementGranularity.WarPulse, "WAR-1000", "BAD-1s"),
            (SettlementGranularity.WarPulse, "WAR-1000", "SEVERE-2s"), (SettlementGranularity.WarPulse, "WAR-LARGE", "NORMAL"),
        ];
        for (int ei = 0; ei < b.Plan.Epochs.Length; ei++)
            for (int ri = 0; ri < rows.Length; ri++)
                for (int qi = 0; qi < b.Plan.Orders.Length; qi++)
                {
                    EpochPlan e = b.Plan.Epochs[ei];
                    (SettlementGranularity g, string army, string regime) = rows[ri];
                    bool turn = g == SettlementGranularity.CampaignTurn;
                    List<RunMetrics> lost = Cell(b, e.StartTurn, army, regime, b.Plan.Orders[qi], OverflowRule.CapBeforeMilitary, UnfedConsequence.None,
                        HaulBasis.FullLoad, GrowthClaim.Headroom, g);
                    List<RunMetrics> sold = Cell(b, e.StartTurn, army, regime, b.Plan.Orders[qi], OverflowRule.TradeCredit, UnfedConsequence.None,
                        HaulBasis.FullLoad, GrowthClaim.Headroom, g);
                    if (lost.Count == 0 || sold.Count == 0) continue;
                    double Draws(RunMetrics m) => turn ? m.RationHorizon + m.HaulHorizon : m.RationWar + m.HaulWar;
                    double Bought(RunMetrics m) => turn ? m.ImportedHorizon - m.ImportedCivHorizon : m.ImportedWar - m.ImportedCivWar;
                    double drawsSold = Mean(sold, Draws), bought = Mean(sold, Bought);
                    sb.Append("| ").Append(turn ? "CampaignTurn" : "WarPulse").Append(" | ").Append(L(e.StartTurn)).Append(" | ").Append(army).Append(" | ").Append(regime)
                      .Append(" | ").Append(OrderTag(b.Plan.Orders[qi])).Append(" | ").Append(F(Mean(lost, Draws), 0)).Append(" | ").Append(F(drawsSold, 0))
                      .Append(" | ").Append(F(bought, 0)).Append(" | ").Append(F(drawsSold - bought, 0))
                      .Append(" | ").Append(drawsSold > 0 ? F(100.0 * (drawsSold - bought) / drawsSold, 1) : "n/a")
                      .Append(" | ").Append(F(Mean(sold, m => turn ? m.ImportedCivHorizon : m.ImportedCivWar), 0))
                      .Append(" | ").Append(F(100 * Mean(lost, m => turn ? m.WholeGrowthServed : m.GrowthServed), 1)).Append(" → ")
                      .Append(F(100 * Mean(sold, m => turn ? m.WholeGrowthServed : m.GrowthServed), 1)).Append(" |\n");
                }
        sb.Append('\n');
    }

    private static FoodRunResult? Find(FoodBattery b, ulong seed, long t0, string army, string regime, AllocationOrder order, OverflowRule overflow, UnfedConsequence consequence)
    {
        for (int i = 0; i < b.Runs.Count; i++)
        {
            FoodRunResult r = b.Runs[i];
            FoodScenario s = r.Scenario;
            if (r.Seed == seed && s.StartTurn == t0 && s.Army == army && s.Regime == regime && s.Order == order && s.Overflow == overflow
                && s.Consequence == consequence && s.Haul == HaulBasis.FullLoad && s.Granularity == SettlementGranularity.WarPulse
                && s.Growth == GrowthClaim.Headroom) return r;
        }
        return null;
    }

    private static BaselineSeries SeriesOf(FoodBattery b, ulong seed)
    {
        for (int i = 0; i < b.Series.Length; i++) if (b.Series[i].Seed == seed) return b.Series[i];
        throw new ArgumentException("no series for seed " + seed.ToString(Inv));
    }

    public static string OrderTag(AllocationOrder o) => o == AllocationOrder.CivilianGrowthMilitary ? "CGM" : "CMG";
    private static string Pct(long num, long den) => den != 0 ? F(100.0 * num / den, 2) + " %" : "n/a";
    private static string F(double v, int d) => double.IsFinite(v) ? v.ToString("F" + d.ToString(Inv), Inv) : "n/a";
    private static string L(long v) => v.ToString(Inv);
    private static string I(int v) => v.ToString(Inv);
}
