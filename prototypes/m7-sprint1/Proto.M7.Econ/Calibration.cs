using System.Globalization;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;

namespace Proto.M7.Econ;

/// <summary>World-level ledger totals at one boundary (running totals; deltas between two boundaries are the flows).</summary>
public readonly record struct LedgerTotals(
    long Harvest, long GrainEaten, long Spoilage, long GranaryOverflow, long FormationUpkeep, long Fodder,
    long Births, long Deaths, long Starvation)
{
    public static LedgerTotals Of(IReadOnlyWorldState w, SimConfig cfg)
    {
        var grain = ConservedQuantityIds.OfGood(new GoodId(cfg.Goods!.GrainId));
        var people = ConservedQuantityIds.Population;
        return new LedgerTotals(
            PolityMeasure.Ledger(w, grain, ReasonIds.Harvest, false),
            PolityMeasure.Ledger(w, grain, ReasonIds.Eaten, true),
            PolityMeasure.Ledger(w, grain, ReasonIds.Spoilage, true),
            PolityMeasure.Ledger(w, grain, ReasonIds.GranaryOverflow, true),
            PolityMeasure.Ledger(w, grain, ReasonIds.FormationUpkeep, true),
            PolityMeasure.Ledger(w, grain, ReasonIds.Fodder, true),
            PolityMeasure.Ledger(w, people, ReasonIds.Births, false),
            PolityMeasure.Ledger(w, people, ReasonIds.Deaths, true),
            PolityMeasure.Ledger(w, people, ReasonIds.Starvation, true));
    }

    public LedgerTotals Minus(LedgerTotals o) => new(Harvest - o.Harvest, GrainEaten - o.GrainEaten, Spoilage - o.Spoilage,
        GranaryOverflow - o.GranaryOverflow, FormationUpkeep - o.FormationUpkeep, Fodder - o.Fodder, Births - o.Births,
        Deaths - o.Deaths, Starvation - o.Starvation);
}

/// <summary>One checkpoint of a baseline run.</summary>
public sealed record Checkpoint(ulong Seed, long Turn, long SimDays, double DtYears, string Hash, LedgerTotals Ledger,
    LedgerTotals LastStep, PolitySnapshot[] Polities, long ElapsedMs, WorldState World);

/// <summary>The counterfactual: the player recruits as many formations as the EXISTING rules allow at turn T.</summary>
public sealed record RecruitTwin(ulong Seed, long Turn, string Identity, long PersonnelPerFormation, int OrdersIssued,
    long SoldiersBefore, long SoldiersAfter, PolitySnapshot BaseAtT1, PolitySnapshot TwinAtT1, PolitySnapshot BaseAtTk,
    PolitySnapshot TwinAtTk, LedgerTotals BaseDelta, LedgerTotals TwinDelta, int Horizon, string FirstRefusal,
    string[] RefusalsAfter, long MaxFormationsPerSettlement);

/// <summary>
/// THE CALIBRATION RUNS (implementation-spec §6): real founded worlds stepped with the existing TurnExecutor (no orders —
/// the golden recipe, <see cref="EconWorld"/>), measured at checkpoints. Everything is read through the public API.
/// Wall-clock time is measured with Environment.TickCount64 ONLY for the report (never fed back into a world).
/// </summary>
public static class Calibration
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static List<Checkpoint> RunBaseline(ulong seed, long[] checkpoints)
    {
        SimConfig cfg = EconWorld.SimCfg;
        TurnExecutor ex = EconWorld.Executor();
        long t0 = Environment.TickCount64;
        WorldState w = EconWorld.Found(seed);
        var result = new List<Checkpoint>();
        LedgerTotals prevTotals = LedgerTotals.Of(w, cfg);
        long max = 0;
        for (int i = 0; i < checkpoints.Length; i++) max = Math.Max(max, checkpoints[i]);
        int next = 0;
        long[] sorted = (long[])checkpoints.Clone();
        Array.Sort(sorted);
        while (next < sorted.Length && sorted[next] == 0)
        {
            result.Add(Snap(seed, w, cfg, prevTotals, prevTotals, t0));
            next++;
        }
        for (long t = 1; t <= max; t++)
        {
            WorldState n = ex.Step(w);
            LedgerTotals totals = LedgerTotals.Of(n, cfg);
            if (next < sorted.Length && sorted[next] == t)
            {
                result.Add(Snap(seed, n, cfg, totals, totals.Minus(prevTotals), t0));
                next++;
            }
            prevTotals = totals;
            w = n;
        }
        return result;
    }

    private static Checkpoint Snap(ulong seed, WorldState w, SimConfig cfg, LedgerTotals totals, LedgerTotals last, long t0) =>
        new(seed, w.Clock.Turn, w.Clock.SimDays, w.Clock.DtYears, WorldHash.ComputeHex(w), totals, last,
            PolityMeasure.All(w, cfg), Environment.TickCount64 - t0, w);

    /// <summary>The player polity (the one CommandSource.Player) of <paramref name="w"/>.</summary>
    public static PolityId Player(IReadOnlyWorldState w)
    {
        for (int i = 0; i < w.Polities.Count; i++)
            if (EmpireQuery.IsPlayerCommanded(w, w.Polities[i].Id)) return w.Polities[i].Id;
        throw new InvalidOperationException("no player polity");
    }

    /// <summary>The settlements <paramref name="p"/> controls, ascending id.</summary>
    public static SettlementId[] Controlled(IReadOnlyWorldState w, PolityId p)
    {
        var list = new List<int>();
        for (int i = 0; i < w.Settlements.Count; i++)
            if (EmpireQuery.ControlsSettlement(w, p, w.Settlements[i].Id)) list.Add(w.Settlements[i].Id.Value);
        list.Sort();
        var r = new SettlementId[list.Count];
        for (int i = 0; i < r.Length; i++) r[i] = new SettlementId(list[i]);
        return r;
    }

    /// <summary>The first identity (family order, line order) the existing §6.5 predicate lets <paramref name="p"/>
    /// recruit at <paramref name="s"/>, or null.</summary>
    public static UnitIdentity? FirstRecruitable(IReadOnlyWorldState w, SimConfig cfg, PolityId p, SettlementId s, out string firstRefusal)
    {
        firstRefusal = "";
        MilitaryView view = RecruitmentQuery.ViewOf(w);
        IReadOnlyList<UnitFamily> families = cfg.UnitFamilies!.Families;
        for (int f = 0; f < families.Count; f++)
            for (int i = 0; i < families[f].Line.Count; i++)
            {
                UnitIdentity id = families[f].Line[i];
                if (id.Recruitment is null) continue;
                RecruitCheck c = RecruitmentQuery.CheckRecruit(w, cfg, view, p, s, id.Key);
                if (c.Ok) return id;
                if (firstRefusal.Length == 0) firstRefusal = id.Name + ": " + c.Reason;
            }
        return null;
    }

    /// <summary>
    /// The max-recruitment counterfactual from <paramref name="atT"/>: the player issues, at every settlement it controls, as
    /// many RecruitFormation orders (OrderKind 16) of the first recruitable identity as the settlement has adults for; the
    /// existing predicate (personnel, support = 1 year of upkeep for ALL formations from the purse's free grain, materials)
    /// accepts as many as it accepts, in log order. Both branches then run <paramref name="horizon"/> turns.
    /// </summary>
    public static RecruitTwin MaxRecruitment(ulong seed, WorldState atT, int horizon, long maxFormationsPerSettlement = long.MaxValue)
    {
        SimConfig cfg = EconWorld.SimCfg;
        PolityId player = Player(atT);
        SettlementId[] own = Controlled(atT, player);
        UnitIdentity? identity = null;
        string refusal = "";
        for (int i = 0; i < own.Length && identity is null; i++) identity = FirstRecruitable(atT, cfg, player, own[i], out refusal);

        var log = new OrderLog();
        int issued = 0;
        if (identity is not null)
        {
            long per = identity.Recruitment!.Personnel;
            for (int i = 0; i < own.Length; i++)
            {
                long adults = BandViews.Adults(atT.Buckets, own[i]);
                long n = Math.Min(maxFormationsPerSettlement, per > 0 ? adults / per : 0);
                for (long k = 0; k < n; k++)
                {
                    log.Append(OrderRecord.From(atT.Clock.Turn, player, OrderKind.RecruitFormation, own[i].Value, identity.Key));
                    issued++;
                }
            }
        }
        TurnExecutor baseEx = EconWorld.Executor();
        TurnExecutor twinEx = EconWorld.Executor(log);
        var baskets = new Sim.Core.Systems.Consumption.BasketBook(cfg.Needs!, cfg.Goods!);
        LedgerTotals start = LedgerTotals.Of(atT, cfg);
        WorldState b = atT, t = atT;
        PolitySnapshot? b1 = null, t1 = null;
        var refusals = new List<string>();
        for (int k = 1; k <= horizon; k++)
        {
            b = baseEx.Step(b);
            t = twinEx.Step(t);
            if (k == 1)
            {
                b1 = PolityMeasure.Of(b, cfg, baskets, player);
                t1 = PolityMeasure.Of(t, cfg, baskets, player);
                // What the existing predicate says NOW at each settlement — the constraint that bound the recruitment.
                if (identity is not null)
                {
                    MilitaryView view = RecruitmentQuery.ViewOf(t);
                    for (int i = 0; i < own.Length; i++)
                    {
                        RecruitCheck c = RecruitmentQuery.CheckRecruit(t, cfg, view, player, own[i], identity.Key);
                        string text = c.Ok ? "Ok (cap reached by the order count)" : c.Verdict + ": " + c.Reason;
                        if (!refusals.Contains(text)) refusals.Add(text);
                    }
                    refusals.Sort(StringComparer.Ordinal);
                }
            }
        }
        long before = PolityMeasure.Of(atT, cfg, baskets, player).Soldiers;
        return new RecruitTwin(seed, atT.Clock.Turn, identity?.Name ?? "(none recruitable)", identity?.Recruitment?.Personnel ?? 0,
            issued, before, t1!.Soldiers, b1!, t1, PolityMeasure.Of(b, cfg, baskets, player), PolityMeasure.Of(t, cfg, baskets, player),
            LedgerTotals.Of(b, cfg).Minus(start), LedgerTotals.Of(t, cfg).Minus(start), horizon, refusal, [.. refusals],
            maxFormationsPerSettlement);
    }

    /// <summary>Movement bills from the player's capital to every other settlement, for each column size.</summary>
    public static List<(SettlementId To, MovementEstimate Full, MovementEstimate Mean)> MovementTable(WorldState w, long personnel)
    {
        SimConfig cfg = EconWorld.SimCfg;
        MovementParams p = MovementParams.From(cfg, w);
        TraversalLattice lattice = MovementFood.Lattice(w, p);
        PolityId player = Player(w);
        if (!EmpireQuery.TryGetCapital(w, player, out SettlementId capital)) capital = Controlled(w, player)[0];
        var rows = new List<(SettlementId, MovementEstimate, MovementEstimate)>();
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            SettlementId to = w.Settlements[i].Id;
            if (to == capital) continue;
            rows.Add((to,
                MovementFood.Estimate(w, p, lattice, capital, to, personnel, HaulBasis.FullLoad),
                MovementFood.Estimate(w, p, lattice, capital, to, personnel, HaulBasis.MeanLoad)));
        }
        return rows;
    }

    public static string F(double v, int decimals = 1) => double.IsFinite(v) ? v.ToString("F" + decimals.ToString(Inv), Inv) : "inf";
    public static string L(long v) => v.ToString(Inv);
}
