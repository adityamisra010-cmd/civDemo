using System.Globalization;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Consumption;

namespace Proto.M7.Econ;

/// <summary>
/// M7 Phase C lane F. One turn of a REAL Sim.Core world (the canonical founded world, <see cref="EconWorld"/>, stepped with
/// the shipped TurnExecutor and no orders), measured at the boundary AFTER step <see cref="Turn"/>. World-level: the
/// calibration world has one polity that controls every settlement (aiEmpires 0), so the world ledger is that polity's.
/// Stocks are the state after the step; the "Step…" fields are ledger flows over the step that produced it.
/// Everything is READ through the public API; nothing is written to the world.
/// </summary>
public sealed record BaselineTurn(
    long Turn,
    long DtDays,
    long[] Cohorts,
    long Soldiers,
    long UnfedUnits,
    long GrainStock,
    long Adults,
    double NutritionalPersons,
    double HeadroomLimit,
    bool HeadroomUnbounded,
    long CivilianRequirement,
    long StepHarvest,
    long StepGrainEaten,
    long StepSpoilage,
    long StepOverflow,
    long StepUpkeep,
    long StepFodder,
    long StepBirths,
    long StepDeaths,
    long StepStarvation,
    long StepOtherGrainNet,
    long FoodProducedStep,
    long FoodEatenStep,
    string Hash16)
{
    public double DtYears => DtDays / (double)Sim.Core.Kernel.SimClock.YearDays;

    public long Population
    {
        get
        {
            long p = 0;
            for (int c = 0; c < Cohorts.Length; c++) p = checked(p + Cohorts[c]);
            return p;
        }
    }
}

/// <summary>The labour-response twin at one epoch turn (REAL Sim.Core): RecruitFormation orders remove adults from the
/// buckets at step T+1 (Military, slot 23 — after Production, slot 4), so the step-T+2 harvest is the first one produced
/// with less labour. Marginal grain per removed adult-year = (base − twin harvest of step T+2) ÷ (removed × dt).</summary>
public sealed record LabourTwin(long Turn, long Removed, long BaseHarvestT2, long TwinHarvestT2, double DtYears)
{
    public double MarginalGrainPerAdultYear => Removed > 0 && DtYears > 0 ? (BaseHarvestT2 - TwinHarvestT2) / (Removed * DtYears) : 0.0;
}

/// <summary>One turn of a REAL recruitment twin (granary-overflow investigation): the twin's per-step grain flows and the
/// upkeep that went unfed although overflow and spoilage destroyed grain in the same step.</summary>
public sealed record OverflowTwinTurn(long Turn, long Soldiers, long GrainStock, long Harvest, long Eaten, long Spoilage,
    long Overflow, long UpkeepDrawn, long UnfedAccrued, long Starvation, long Births, long Population);

public sealed record OverflowTwin(ulong Seed, long AtTurn, long Recruited, OverflowTwinTurn[] Turns);

/// <summary>Everything the comparison needs from one seed's real world.</summary>
public sealed record BaselineSeries(ulong Seed, BaselineTurn[] Turns, LabourTwin[] Twins, OverflowTwin[] OverflowTwins, long ElapsedMs)
{
    /// <summary>The row of turn <paramref name="turn"/> (rows are contiguous from turn 0).</summary>
    public BaselineTurn At(long turn) => Turns[turn];

    /// <summary>The two labour twins at <paramref name="turn"/>: (small, large), ordered by Removed.</summary>
    public (LabourTwin Small, LabourTwin Large) TwinsAt(long turn)
    {
        LabourTwin? a = null, b = null;
        for (int i = 0; i < Twins.Length; i++)
        {
            if (Twins[i].Turn != turn) continue;
            if (a is null) a = Twins[i];
            else b = Twins[i];
        }
        if (a is null || b is null) throw new InvalidOperationException("no labour twins at turn " + turn.ToString(CultureInfo.InvariantCulture));
        return a.Removed <= b.Removed ? (a, b) : (b, a);
    }
}

/// <summary>
/// Measures <see cref="BaselineSeries"/> on REAL founded worlds. Wall time via Environment.TickCount64, report only.
/// </summary>
public static class FoodBaseline
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Formations per settlement for the two labour twins and the overflow twins (Warband, 20 personnel each):
    /// 2 → 480 recruits (with the founding warband 500 soldiers, the low end of DIR5's "500–1,000 readily achievable"),
    /// 4 → 960 (980, the high end). The large labour twin uses as many as the settlement's adults allow.</summary>
    public const long SmallTwinPerSettlement = 4;
    public static readonly long[] OverflowTwinCaps = [2, 4];

    public static BaselineSeries Measure(ulong seed, long maxTurn, long[] twinTurns, long overflowTwinHorizon)
    {
        SimConfig cfg = EconWorld.SimCfg;
        TurnExecutor ex = EconWorld.Executor();
        var baskets = new BasketBook(cfg.Needs!, cfg.Goods!);
        long t0 = Environment.TickCount64;
        WorldState w = EconWorld.Found(seed);
        var rows = new List<BaselineTurn>();
        var twins = new List<LabourTwin>();
        var overflow = new List<OverflowTwin>();
        LedgerTotals prev = LedgerTotals.Of(w, cfg);
        long prevStock = GrainStock(w, cfg);
        rows.Add(Row(w, cfg, baskets, prev, prev, prevStock));
        for (long t = 1; t <= maxTurn; t++)
        {
            if (Array.IndexOf(twinTurns, t - 1) >= 0)
            {
                twins.AddRange(LabourTwins(w));
                for (int c = 0; c < OverflowTwinCaps.Length; c++)
                    overflow.Add(RunOverflowTwin(seed, w, OverflowTwinCaps[c], overflowTwinHorizon));
            }
            WorldState n = ex.Step(w);
            LedgerTotals now = LedgerTotals.Of(n, cfg);
            rows.Add(Row(n, cfg, baskets, now, prev, prevStock));
            prev = now;
            prevStock = GrainStock(n, cfg);
            w = n;
        }
        return new BaselineSeries(seed, [.. rows], [.. twins], [.. overflow], Environment.TickCount64 - t0);
    }

    public static long GrainStock(IReadOnlyWorldState w, SimConfig cfg)
    {
        int grain = cfg.Goods!.GrainId;
        long s = 0;
        for (int g = 0; g < w.GoodStocks.Count; g++)
            if (w.GoodStocks[g].Good.Value == grain) s = checked(s + w.GoodStocks[g].Amount.Value);
        return s;
    }

    private static BaselineTurn Row(WorldState w, SimConfig cfg, BasketBook baskets, LedgerTotals now, LedgerTotals before, long stockBefore)
    {
        var cohorts = new long[Cohorts.Count];
        long adults = 0;
        double nutr = 0.0;
        double[] weights = cfg.Consumption.CohortWeights;
        for (int b = 0; b < w.Buckets.Count; b++)
        {
            BucketRow row = w.Buckets[b];
            cohorts[row.CohortIdx] = checked(cohorts[row.CohortIdx] + row.Count.Value);
            if (BandViews.IsAdult(row.CohortIdx)) adults = checked(adults + row.Count.Value);
        }
        for (int c = 0; c < cohorts.Length; c++) nutr += weights[c] * cohorts[c];
        double limit = 0.0;
        bool unbounded = false;
        long demand = 0;
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            double l = FoodHeadroom.Limit(w, w.Settlements[s].Id, weights, baskets);
            if (double.IsPositiveInfinity(l)) unbounded = true;
            else limit += l;
        }
        for (int d = 0; d < w.ConsumptionDeficits.Count; d++) demand = checked(demand + w.ConsumptionDeficits[d].DemandUnits);
        long soldiers = 0, unfed = 0;
        for (int i = 0; i < w.FormationPersonnel.Count; i++)
        {
            soldiers = checked(soldiers + w.FormationPersonnel[i].Count.Value);
            unfed = checked(unfed + w.FormationPersonnel[i].UnfedUnits);
        }
        GoodsConfig goods = cfg.Goods!;
        long foodProduced = 0, foodEaten = 0;
        for (int g = 0; g < w.GoodStocks.Count; g++)
        {
            GoodStockRow row = w.GoodStocks[g];
            if (!goods.Contains(row.Good.Value) || goods.ById(row.Good.Value).Category != "food") continue;
            foodProduced = checked(foodProduced + row.LastProducedUnits);
            foodEaten = checked(foodEaten + row.LastConsumptionEatenUnits);
        }
        LedgerTotals d1 = now.Minus(before);
        long stock = GrainStock(w, cfg);
        // Residual of the grain identity over the step: every grain source/sink the totals above do not name (0 on
        // order-less worlds; recorded, not assumed).
        long other = stock - stockBefore - d1.Harvest + d1.GrainEaten + d1.Spoilage + d1.GranaryOverflow + d1.FormationUpkeep + d1.Fodder;
        return new BaselineTurn(w.Clock.Turn, w.Clock.DtDays, cohorts, soldiers, unfed, stock, adults, nutr, limit, unbounded, demand,
            d1.Harvest, d1.GrainEaten, d1.Spoilage, d1.GranaryOverflow, d1.FormationUpkeep, d1.Fodder, d1.Births, d1.Deaths, d1.Starvation,
            other, foodProduced, foodEaten, WorldHash.ComputeHex(w)[..16]);
    }

    /// <summary>The two labour twins at <paramref name="atT"/> (small = 4 Warbands per settlement, large = as many as the
    /// adults allow), each against the shared order-less branch.</summary>
    public static LabourTwin[] LabourTwins(WorldState atT)
    {
        SimConfig cfg = EconWorld.SimCfg;
        TurnExecutor baseEx = EconWorld.Executor();
        WorldState b1 = baseEx.Step(atT);
        WorldState b2 = baseEx.Step(b1);
        long baseHarvest = LedgerTotals.Of(b2, cfg).Harvest - LedgerTotals.Of(b1, cfg).Harvest;
        long[] caps = [SmallTwinPerSettlement, long.MaxValue];
        var result = new LabourTwin[caps.Length];
        for (int i = 0; i < caps.Length; i++)
        {
            OrderLog log = RecruitLog(atT, caps[i], out _);
            TurnExecutor tx = EconWorld.Executor(log);
            WorldState t1 = tx.Step(atT);
            WorldState t2 = tx.Step(t1);
            long removed = Soldiers(t1) - Soldiers(b1);
            long twinHarvest = LedgerTotals.Of(t2, cfg).Harvest - LedgerTotals.Of(t1, cfg).Harvest;
            result[i] = new LabourTwin(atT.Clock.Turn, removed, baseHarvest, twinHarvest, b2.Clock.DtYears);
        }
        return result;
    }

    /// <summary>A REAL recruitment twin from <paramref name="atT"/>: <paramref name="perSettlement"/> Warbands per settlement,
    /// then <paramref name="horizon"/> turns; every step's grain flows and unfed upkeep are recorded.</summary>
    public static OverflowTwin RunOverflowTwin(ulong seed, WorldState atT, long perSettlement, long horizon)
    {
        SimConfig cfg = EconWorld.SimCfg;
        OrderLog log = RecruitLog(atT, perSettlement, out _);
        TurnExecutor tx = EconWorld.Executor(log);
        WorldState w = atT;
        LedgerTotals prev = LedgerTotals.Of(w, cfg);
        long prevUnfed = Unfed(w);
        long before = Soldiers(atT);
        var turns = new OverflowTwinTurn[horizon];
        long recruited = 0;
        for (long k = 0; k < horizon; k++)
        {
            WorldState n = tx.Step(w);
            LedgerTotals now = LedgerTotals.Of(n, cfg);
            LedgerTotals d = now.Minus(prev);
            long unfed = Unfed(n);
            if (k == 0) recruited = Soldiers(n) - before;
            long pop = 0;
            for (int b = 0; b < n.Buckets.Count; b++) pop = checked(pop + n.Buckets[b].Count.Value);
            turns[k] = new OverflowTwinTurn(n.Clock.Turn, Soldiers(n), GrainStock(n, cfg), d.Harvest, d.GrainEaten, d.Spoilage,
                d.GranaryOverflow, d.FormationUpkeep, unfed - prevUnfed, d.Starvation, d.Births, pop);
            prev = now;
            prevUnfed = unfed;
            w = n;
        }
        return new OverflowTwin(seed, atT.Clock.Turn, recruited, turns);
    }

    private static long Soldiers(IReadOnlyWorldState w)
    {
        long s = 0;
        for (int i = 0; i < w.FormationPersonnel.Count; i++) s = checked(s + w.FormationPersonnel[i].Count.Value);
        return s;
    }

    private static long Unfed(IReadOnlyWorldState w)
    {
        long s = 0;
        for (int i = 0; i < w.FormationPersonnel.Count; i++) s = checked(s + w.FormationPersonnel[i].UnfedUnits);
        return s;
    }

    /// <summary>RecruitFormation orders (kind 16) of the first identity the existing §6.5 predicate accepts, at most
    /// <paramref name="perSettlement"/> per settlement and never more than the settlement's adults allow.</summary>
    private static OrderLog RecruitLog(WorldState atT, long perSettlement, out int issued)
    {
        SimConfig cfg = EconWorld.SimCfg;
        PolityId player = Calibration.Player(atT);
        SettlementId[] own = Calibration.Controlled(atT, player);
        UnitIdentity? identity = null;
        for (int i = 0; i < own.Length && identity is null; i++) identity = Calibration.FirstRecruitable(atT, cfg, player, own[i], out _);
        var log = new OrderLog();
        issued = 0;
        if (identity is null) return log;
        long per = identity.Recruitment!.Personnel;
        for (int i = 0; i < own.Length; i++)
        {
            long adults = BandViews.Adults(atT.Buckets, own[i]);
            long n = Math.Min(perSettlement, per > 0 ? adults / per : 0);
            for (long k = 0; k < n; k++)
            {
                log.Append(OrderRecord.From(atT.Clock.Turn, player, OrderKind.RecruitFormation, own[i].Value, identity.Key));
                issued++;
            }
        }
        return log;
    }

    // ------------------------------------------------------------------ text round trip (the battery reads saved series)

    /// <summary>A line-oriented, culture-invariant text form (one row per line), so the battery can run on saved real
    /// measurements without re-stepping the worlds. Round-trips exactly (doubles as "R").</summary>
    public static string ToText(BaselineSeries s)
    {
        var sb = new StringBuilder();
        sb.Append("series ").Append(s.Seed.ToString(Inv)).Append(' ').Append(s.ElapsedMs.ToString(Inv)).Append('\n');
        foreach (BaselineTurn r in s.Turns)
        {
            sb.Append("turn ").Append(L(r.Turn)).Append(' ').Append(L(r.DtDays)).Append(' ');
            for (int c = 0; c < r.Cohorts.Length; c++) sb.Append(L(r.Cohorts[c])).Append(c + 1 < r.Cohorts.Length ? ',' : ' ');
            sb.Append(L(r.Soldiers)).Append(' ').Append(L(r.UnfedUnits)).Append(' ').Append(L(r.GrainStock)).Append(' ').Append(L(r.Adults))
              .Append(' ').Append(D(r.NutritionalPersons)).Append(' ').Append(D(r.HeadroomLimit)).Append(' ').Append(r.HeadroomUnbounded ? '1' : '0')
              .Append(' ').Append(L(r.CivilianRequirement)).Append(' ').Append(L(r.StepHarvest)).Append(' ').Append(L(r.StepGrainEaten))
              .Append(' ').Append(L(r.StepSpoilage)).Append(' ').Append(L(r.StepOverflow)).Append(' ').Append(L(r.StepUpkeep)).Append(' ').Append(L(r.StepFodder))
              .Append(' ').Append(L(r.StepBirths)).Append(' ').Append(L(r.StepDeaths)).Append(' ').Append(L(r.StepStarvation)).Append(' ').Append(L(r.StepOtherGrainNet))
              .Append(' ').Append(L(r.FoodProducedStep)).Append(' ').Append(L(r.FoodEatenStep)).Append(' ').Append(r.Hash16).Append('\n');
        }
        foreach (LabourTwin t in s.Twins)
            sb.Append("labour ").Append(L(t.Turn)).Append(' ').Append(L(t.Removed)).Append(' ').Append(L(t.BaseHarvestT2)).Append(' ')
              .Append(L(t.TwinHarvestT2)).Append(' ').Append(D(t.DtYears)).Append('\n');
        foreach (OverflowTwin o in s.OverflowTwins)
        {
            sb.Append("otwin ").Append(L(o.AtTurn)).Append(' ').Append(L(o.Recruited)).Append(' ').Append(o.Turns.Length.ToString(Inv)).Append('\n');
            foreach (OverflowTwinTurn x in o.Turns)
                sb.Append("ot ").Append(L(x.Turn)).Append(' ').Append(L(x.Soldiers)).Append(' ').Append(L(x.GrainStock)).Append(' ').Append(L(x.Harvest))
                  .Append(' ').Append(L(x.Eaten)).Append(' ').Append(L(x.Spoilage)).Append(' ').Append(L(x.Overflow)).Append(' ').Append(L(x.UpkeepDrawn))
                  .Append(' ').Append(L(x.UnfedAccrued)).Append(' ').Append(L(x.Starvation)).Append(' ').Append(L(x.Births)).Append(' ').Append(L(x.Population)).Append('\n');
        }
        return sb.ToString();
    }

    public static BaselineSeries FromText(string text)
    {
        string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        ulong seed = 0;
        long elapsed = 0;
        var rows = new List<BaselineTurn>();
        var twins = new List<LabourTwin>();
        var otwins = new List<OverflowTwin>();
        for (int i = 0; i < lines.Length; i++)
        {
            string[] f = lines[i].Split(' ');
            switch (f[0])
            {
                case "series":
                    seed = ulong.Parse(f[1], Inv);
                    elapsed = long.Parse(f[2], Inv);
                    break;
                case "turn":
                {
                    string[] cs = f[3].Split(',');
                    var cohorts = new long[cs.Length];
                    for (int c = 0; c < cs.Length; c++) cohorts[c] = long.Parse(cs[c], Inv);
                    rows.Add(new BaselineTurn(long.Parse(f[1], Inv), long.Parse(f[2], Inv), cohorts, P(f[4]), P(f[5]), P(f[6]), P(f[7]),
                        double.Parse(f[8], Inv), double.Parse(f[9], Inv), f[10] == "1", P(f[11]), P(f[12]), P(f[13]), P(f[14]), P(f[15]),
                        P(f[16]), P(f[17]), P(f[18]), P(f[19]), P(f[20]), P(f[21]), P(f[22]), P(f[23]), f[24]));
                    break;
                }
                case "labour":
                    twins.Add(new LabourTwin(P(f[1]), P(f[2]), P(f[3]), P(f[4]), double.Parse(f[5], Inv)));
                    break;
                case "otwin":
                {
                    long at = P(f[1]), rec = P(f[2]);
                    int n = int.Parse(f[3], Inv);
                    var turns = new OverflowTwinTurn[n];
                    for (int k = 0; k < n; k++)
                    {
                        string[] g = lines[++i].Split(' ');
                        turns[k] = new OverflowTwinTurn(P(g[1]), P(g[2]), P(g[3]), P(g[4]), P(g[5]), P(g[6]), P(g[7]), P(g[8]), P(g[9]), P(g[10]), P(g[11]), P(g[12]));
                    }
                    otwins.Add(new OverflowTwin(seed, at, rec, turns));
                    break;
                }
                default:
                    throw new FormatException("unknown line kind '" + f[0] + "'");
            }
        }
        for (int r = 0; r < rows.Count; r++)
            if (rows[r].Turn != r) throw new FormatException("series rows must be contiguous from turn 0");
        return new BaselineSeries(seed, [.. rows], [.. twins], [.. otwins], elapsed);
    }

    private static long P(string s) => long.Parse(s, Inv);
    private static string L(long v) => v.ToString(Inv);
    private static string D(double v) => v.ToString("R", Inv);
}
