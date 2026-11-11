using System.Globalization;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Consumption;
using Sim.Core.Systems.Research;

namespace Proto.M7.Econ;

/// <summary>How the probe drives the founded world: no orders (the FOUNDED golden recipe, lane F's baseline) or the
/// canonical research measurement driver of `sim research --auto cheapest` (the generator of
/// scripts/research-calibration/population-trace-seed42.csv).</summary>
public enum ScaleDriver { None = 0, Research = 1 }

/// <summary>One polity at one checkpoint: what the shipped recruitment predicate (RecruitmentQuery.OwnPredicate, ADR-055
/// §6.5) allows, measured from state. Grain in shipped units (one adult-year of food); people in persons.</summary>
public sealed record ScalePoint(
    ulong Seed, ScaleDriver Driver, long Turn, double DtYears, long Population, long Adults, long Soldiers, int Settlements,
    long FreeGrain, long GrainBoundTotal, long AdultBoundExtra, long PredicateBoundExtra, string FirstIdentity, long PersonnelPerFormation,
    long FoodProducedLastStep, long FoodEatenLastStep, double SurplusPerYear, long WorldHashPrefix)
{
    /// <summary>Grain held by the purse before construction set-asides (EmpireStockpile.Available).</summary>
    public long PurseGrain { get; init; }
    /// <summary>The predicate's first refusal at the capital (empty when an identity is recruitable).</summary>
    public string FirstRefusal { get; init; } = "";
}

/// <summary>The real counterfactual at one checkpoint: soldiers the predicate actually accepted when every adult-limited
/// order was issued (lane F's Calibration.MaxRecruitment, horizon 1).</summary>
public sealed record ScaleTwin(ulong Seed, ScaleDriver Driver, long Turn, int OrdersIssued, long SoldiersBefore, long SoldiersAfter,
                               string Binding)
{
    /// <summary>[CB] At most this many RecruitFormation orders per settlement were issued (lane F's maxFormationsPerSettlement);
    /// long.MaxValue = no cap, every adult-limited order (the full twin). A capped twin measures whether the shipped predicate
    /// accepts at least that many formations AT ONCE; it is a lower bound of the full twin, never its value.</summary>
    public long CapPerSettlement { get; init; } = long.MaxValue;
}

/// <summary>
/// Lane D (D-079 D20, Q-L2; DR25): the economic-scale probe. Steps the canonical founded world (<see cref="EconWorld"/>,
/// read-only use of Sim.Core) with either no orders or the canonical research driver, writes the driver polity's population
/// per turn (to compare with the CLI trace), and at checkpoints measures the bounds the SHIPPED predicate puts on force size:
///   grain bound (total personnel) = floor(free purse grain / (upkeepGrainPerPersonYear × supportYears)) — sim.json military;
///   adult bound (extra personnel) = Σ over controlled settlements of (adults − 1) (at least one adult stays);
///   predicate bound (extra) = min(grain bound − soldiers already owned, adult bound), floored at 0.
/// It changes nothing in Sim.*; every number is a read of the stepped state.
/// </summary>
public static class ScaleProbe
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static readonly PolityId Driver = new(1);

    /// <summary>Runs one seed to the largest checkpoint. <paramref name="trace"/> (optional) receives "turn,population" for every
    /// turn of the driver polity (polity 1, as `sim research` defaults). <paramref name="twinAt"/> lists checkpoints at which the
    /// real max-recruitment twin also runs (horizon 1), for either driver (since verification round 2; before, only without orders).</summary>
    public static (List<ScalePoint> Points, List<ScaleTwin> Twins) Run(ulong seed, ScaleDriver driver, long[] checkpoints, long[] twinAt, TextWriter? trace)
        => Run(seed, driver, checkpoints, twinAt, new[] { long.MaxValue }, trace, null);

    /// <summary>
    /// [CB] As <see cref="Run(ulong, ScaleDriver, long[], long[], TextWriter?)"/>, with one twin per entry of
    /// <paramref name="twinCaps"/> at every twin checkpoint, in the order given: long.MaxValue = the full twin (every adult-limited
    /// order); any other positive value = at most that many orders per settlement, a lower bound of the full twin (does the shipped
    /// predicate accept at least that many formations at once?). Why capped twins exist (lane CB, reports/H-CB.md §8): Sim.Core
    /// judges each RecruitFormation order with RecruitmentQuery.OwnerPersonnel, which visits every formation the owner holds and
    /// finds each one's unit row by a linear scan, so the cost of one step grows with the cube of the orders it accepts; the full
    /// twin at research turns 325 and 400 (about 10⁵ to 5 × 10⁵ orders) does not finish. <paramref name="timing"/> (optional)
    /// receives one line per twin with its order count and wall time (Environment.TickCount64; reported only, never fed back).
    /// </summary>
    public static (List<ScalePoint> Points, List<ScaleTwin> Twins) Run(ulong seed, ScaleDriver driver, long[] checkpoints, long[] twinAt,
                                                                       long[] twinCaps, TextWriter? trace, TextWriter? timing)
    {
        foreach (long c in twinCaps)
            if (c <= 0) throw new ArgumentOutOfRangeException(nameof(twinCaps), "a twin cap is a positive number of orders per settlement (long.MaxValue = none)");
        SimConfig cfg = EconWorld.SimCfg;
        ResearchContent content = cfg.Research ?? throw new InvalidOperationException("no research content");
        var orders = new OrderLog();
        TurnExecutor ex = EconWorld.Executor(driver == ScaleDriver.Research ? orders : null);
        WorldState world = EconWorld.Found(seed);
        var baskets = new BasketBook(cfg.Needs!, cfg.Goods!);
        long max = 0;
        foreach (long c in checkpoints) max = Math.Max(max, c);
        var points = new List<ScalePoint>();
        var twins = new List<ScaleTwin>();
        trace?.WriteLine("turn,population");
        if (Contains(checkpoints, 0)) points.Add(Measure(seed, driver, world, cfg, baskets));
        for (long t = 1; t <= max; t++)
        {
            if (driver == ScaleDriver.Research) DriveResearch(world, content, orders);
            world = ex.Step(world);
            trace?.WriteLine(world.Clock.Turn.ToString(Inv) + "," + ResearchQuery.Population(world, Driver).ToString(Inv));
            if (Contains(checkpoints, t)) points.Add(Measure(seed, driver, world, cfg, baskets));
            // [D, verification round 2, D-R2-05] the twin runs for EITHER driver: it was skipped for the research driver, so the
            // economic-scale proposal's research row at turn 25 was the formula, never validated (twins-research.csv held a header).
            if (Contains(twinAt, t))
                foreach (long cap in twinCaps)
                {
                    long w0 = Environment.TickCount64;
                    RecruitTwin tw = Calibration.MaxRecruitment(seed, world, 1, cap);
                    twins.Add(new ScaleTwin(seed, driver, t, tw.OrdersIssued, tw.SoldiersBefore, tw.SoldiersAfter, string.Join(" | ", tw.RefusalsAfter))
                    { CapPerSettlement = cap });
                    timing?.WriteLine("twin seed " + seed.ToString(Inv) + " turn " + t.ToString(Inv) + " cap "
                        + (cap == long.MaxValue ? "none" : cap.ToString(Inv)) + ": " + tw.OrdersIssued.ToString(Inv) + " orders, "
                        + (tw.SoldiersAfter - tw.SoldiersBefore).ToString(Inv) + " accepted, " + (Environment.TickCount64 - w0).ToString(Inv) + " ms");
                }
        }
        return (points, twins);
    }

    /// <summary>The `sim research --auto cheapest` driver, step for step (Sim.Cli/ResearchCli.cs): before each step, a free lane
    /// (two-lane tuning) or the single target (one-lane tuning) gets the cheapest available node by a SetResearchTarget order
    /// stamped with the current turn.</summary>
    public static void DriveResearch(WorldState world, ResearchContent content, OrderLog orders)
    {
        if (!content.Tuning.TwoLanes && !ResearchQuery.TryGetTarget(world, Driver, out _)
            && ResearchQuery.CheapestAvailable(world, content, Driver) is { } pick)
            orders.Append(OrderRecord.From(world.Clock.Turn, Driver, OrderKind.SetResearchTarget, pick.Value, 0.0));
        if (content.Tuning.TwoLanes)
            foreach (ResearchTree lane in (ResearchTree[])[ResearchTree.Technology, ResearchTree.Civics])
                if (!ResearchQuery.TryGetTarget(world, content, Driver, lane, out _)
                    && ResearchQuery.CheapestAvailable(world, content, Driver, lane) is { } lanePick)
                    orders.Append(OrderRecord.From(world.Clock.Turn, Driver, OrderKind.SetResearchTarget, lanePick.Value, 0.0));
    }

    private static bool Contains(long[] a, long v)
    {
        foreach (long x in a) if (x == v) return true;
        return false;
    }

    /// <summary>
    /// The extra personnel the shipped predicate accepts in whole formations of <paramref name="per"/> people. A settlement takes
    /// ⌊(adults − 1) ÷ per⌋ formations: an order there needs MORE adults than its personnel ("at least one stays",
    /// RecruitmentQuery.OwnPredicate: refused when adults ≤ personnel). The purse takes ⌊(grain persons − owned) ÷ per⌋, grain
    /// persons being ⌊free grain ÷ (upkeep × supportYears)⌋ (long.MaxValue = no bound). Result: per × the smaller count; 0 when
    /// no identity is recruitable (per ≤ 0). Material inputs are not modelled (they bind early in some worlds).
    /// </summary>
    public static long PredicateBoundExtra(long per, long grainPersons, long owned, ReadOnlySpan<long> adultsPerSettlement)
    {
        if (per <= 0) return 0;
        long adultForms = 0;
        foreach (long a in adultsPerSettlement) adultForms = checked(adultForms + Math.Max(0, a - 1) / per);
        long grainForms = grainPersons == long.MaxValue ? long.MaxValue : Math.Max(0, grainPersons - owned) / per;
        return checked(Math.Min(grainForms, adultForms) * per);
    }

    /// <summary>The measurement of the driver polity at the current boundary (pure read).</summary>
    public static ScalePoint Measure(ulong seed, ScaleDriver driver, WorldState w, SimConfig cfg, BasketBook baskets)
    {
        PolitySnapshot s = PolityMeasure.Of(w, cfg, baskets, Driver);
        MilitaryConfig m = cfg.Military!;
        MilitaryView view = RecruitmentQuery.ViewOf(w);
        SettlementId[] own = Calibration.Controlled(w, Driver);
        if (!EmpireQuery.TryGetCapital(w, Driver, out SettlementId capital)) capital = own[0];
        long free = DiscoveryQuery.FreeHolding(w, cfg, view.Stocks, view.Holds, EmpireStockpile.Holders(w, capital), new GoodId(cfg.Goods!.GrainId), out _);
        double perPerson = m.UpkeepGrainPerPersonYear * m.SupportYears;
        long grainBound = perPerson <= 0.0 ? long.MaxValue : (long)Math.Floor(free / perPerson);
        long owned = RecruitmentQuery.OwnerPersonnel(view, Driver);
        string first = "(none)", refusal = "";
        long per = 0;
        foreach (SettlementId sid in own)
        {
            UnitIdentity? id = Calibration.FirstRecruitable(w, cfg, Driver, sid, out string why);
            if (id is not null) { first = id.Name; per = id.Recruitment!.Personnel; refusal = ""; break; }
            if (refusal.Length == 0) refusal = why;
        }
        long purse = EmpireStockpile.Available(view.Stocks, EmpireStockpile.Holders(w, capital), new GoodId(cfg.Goods!.GrainId));
        var adults = new long[own.Length];
        long adultExtra = 0;
        for (int i = 0; i < own.Length; i++)
        {
            adults[i] = BandViews.Adults(w.Buckets, own[i]);
            adultExtra = checked(adultExtra + Math.Max(0, adults[i] - 1));
        }
        long extra = PredicateBoundExtra(per, grainBound, owned, adults);
        string hash = WorldHash.ComputeHex(w);
        return new ScalePoint(seed, driver, w.Clock.Turn, w.Clock.DtYears, ResearchQuery.Population(w, Driver), s.Adults, s.Soldiers,
            s.Settlements, free, grainBound, adultExtra, extra, first, per, s.FoodProducedLastStep, s.FoodEatenLastStep, s.FoodSurplusPerYear,
            long.Parse(hash[..12], NumberStyles.HexNumber, Inv)) { PurseGrain = purse, FirstRefusal = refusal };
    }

    public static string Csv(IEnumerable<ScalePoint> points)
    {
        var sb = new StringBuilder("seed,driver,turn,dtYears,population,adults,soldiers,settlements,freeGrain,grainBoundTotal,adultBoundExtra,predicateBoundExtra,firstIdentity,personnelPerFormation,foodProducedLastStep,foodEatenLastStep,surplusPerYear,worldHash48,purseGrain,firstRefusal\n");
        foreach (var p in points)
            sb.Append(string.Join(",", p.Seed.ToString(Inv), p.Driver.ToString(), p.Turn.ToString(Inv), p.DtYears.ToString("R", Inv),
                p.Population.ToString(Inv), p.Adults.ToString(Inv), p.Soldiers.ToString(Inv), p.Settlements.ToString(Inv), p.FreeGrain.ToString(Inv),
                p.GrainBoundTotal.ToString(Inv), p.AdultBoundExtra.ToString(Inv), p.PredicateBoundExtra.ToString(Inv), p.FirstIdentity,
                p.PersonnelPerFormation.ToString(Inv), p.FoodProducedLastStep.ToString(Inv), p.FoodEatenLastStep.ToString(Inv),
                p.SurplusPerYear.ToString("F1", Inv), p.WorldHashPrefix.ToString("x12", Inv), p.PurseGrain.ToString(Inv),
                "\"" + p.FirstRefusal.Replace("\"", "'") + "\"")).Append('\n');
        return sb.ToString();
    }
}

/// <summary>`scale-probe` command: <c>dotnet Proto.M7.Econ.dll scale-probe --seeds 42,1,2,7 --driver research|none
/// --turns 0,25,200 [--twin-at 25,200] [--twin-caps none,220] --out-dir DIR</c>. Writes probe-{driver}.csv, twins-{driver}.csv and one
/// trace-{driver}-seed{S}.csv per seed (turn,population) into DIR. Wall time is reported only. [CB] <c>--twin-caps</c> lists the
/// twins run at every twin checkpoint, in order: "none" = the full twin (the default, as before), a number = at most that many orders
/// per settlement; the twins CSV gains a last column capPerSettlement ("none" or the number), and one timing line per twin is printed.</summary>
public static class ScaleProbeCli
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static bool TryRun(string[] args, out int code)
    {
        code = 0;
        if (args.Length == 0 || args[0] != "scale-probe") return false;
        ulong[] seeds = Parse(Arg(args, "--seeds") ?? "42", s => ulong.Parse(s, Inv));
        long[] turns = Parse(Arg(args, "--turns") ?? "0,25,200", s => long.Parse(s, Inv));
        long[] twinAt = Parse(Arg(args, "--twin-at") ?? "", s => long.Parse(s, Inv));
        long[] twinCaps = Parse(Arg(args, "--twin-caps") ?? "none", s => s == "none" ? long.MaxValue : long.Parse(s, Inv));   // [CB]
        var driver = (Arg(args, "--driver") ?? "none") == "research" ? ScaleDriver.Research : ScaleDriver.None;
        string dir = Arg(args, "--out-dir") ?? ".";
        Directory.CreateDirectory(dir);
        var all = new List<ScalePoint>();
        var twins = new List<ScaleTwin>();
        string tag = driver == ScaleDriver.Research ? "research" : "none";
        foreach (ulong seed in seeds)
        {
            long t0 = Environment.TickCount64;
            using var trace = new StreamWriter(Path.Combine(dir, "trace-" + tag + "-seed" + seed.ToString(Inv) + ".csv"));
            var (pts, tw) = ScaleProbe.Run(seed, driver, turns, twinAt, twinCaps, trace, Console.Out);
            all.AddRange(pts);
            twins.AddRange(tw);
            Console.WriteLine("seed " + seed.ToString(Inv) + " " + tag + " done in " + (Environment.TickCount64 - t0).ToString(Inv) + " ms");
        }
        File.WriteAllText(Path.Combine(dir, "probe-" + tag + ".csv"), ScaleProbe.Csv(all));
        var sb = new StringBuilder("seed,driver,turn,ordersIssued,soldiersBefore,soldiersAfter,binding,capPerSettlement\n");
        foreach (var t in twins)
            sb.Append(string.Join(",", t.Seed.ToString(Inv), t.Driver.ToString(), t.Turn.ToString(Inv), t.OrdersIssued.ToString(Inv),
                t.SoldiersBefore.ToString(Inv), t.SoldiersAfter.ToString(Inv), "\"" + t.Binding.Replace("\"", "'") + "\"",
                t.CapPerSettlement == long.MaxValue ? "none" : t.CapPerSettlement.ToString(Inv))).Append('\n');
        File.WriteAllText(Path.Combine(dir, "twins-" + tag + ".csv"), sb.ToString());
        Console.Write(ScaleProbe.Csv(all));
        return true;
    }

    private static string? Arg(string[] args, string name)
    {
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
        return null;
    }

    private static T[] Parse<T>(string text, Func<string, T> parse)
    {
        string[] parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var r = new T[parts.Length];
        for (int i = 0; i < parts.Length; i++) r[i] = parse(parts[i]);
        return r;
    }
}
