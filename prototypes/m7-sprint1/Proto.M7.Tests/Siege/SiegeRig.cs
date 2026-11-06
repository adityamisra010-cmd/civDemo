using Proto.M7.Events;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Siege;
using Proto.M7.WarEcon;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Siege;

/// <summary>Shared builders for the lane S tests. Every number is a TEST INPUT.</summary>
internal static class SiegeRig
{
    public static readonly CampaignCalendar Cal = ShippedCalendar();

    /// <summary>T2's PROPOSED War Pulse lengths (for Director review; NOT decided).</summary>
    public static WarPulseSchedule ProposedSchedule() => Schedule(WarPulseT2Tests.ProposedPulseDays);

    public static WarPulseSchedule Schedule(long[] days)
    {
        var t = new long[Cal.Bands.Count];
        for (int i = 0; i < t.Length; i++) t[i] = days[i] * Day;
        return new WarPulseSchedule(Cal, t);
    }

    /// <summary>A flat 7-day pulse in every band (so a date shift changes no pulse length: Law 4 metamorphic tests).</summary>
    public static WarPulseSchedule FlatSchedule() => Schedule(new long[] { 7, 7, 7, 7, 7, 7, 7 });

    public static long At(long year, long day) => SiegeHarness.InstantOf(Cal, year, day);

    /// <summary>TEST INPUT: lane E's PROPOSED rates with the BASE desertion rate off, so garrison head counts stay exact in tests
    /// that are not about desertion. Base desertion has its own test (GarrisonTests.BaseDesertion_...).</summary>
    public static readonly LogisticsRates Rates = LogisticsRates.Proposal() with { DesertionBasePermilleYear = 0 };

    /// <summary>TEST INPUT: lane E's rates with every desertion term off (for tests about something else that must not lose men).</summary>
    public static readonly LogisticsRates NoDesertion = Rates with
    {
        DesertionLowMoralePermilleYear = 0, DesertionPerArrearsMonthPermilleYear = 0, DesertionHungerPermilleYear = 0, DesertionMutinyPermilleYear = 0,
    };

    /// <summary>TEST INPUT: every rig polity holds Coinage and Gunpowder (lane E capability flags), nothing else.</summary>
    public static readonly PolityCapability Powder = new(WarTech.Coinage | WarTech.Gunpowder);

    /// <summary>Food (gm) for the rig city's 3,000 garrison and 6,000 civilians for <paramref name="days"/> days at full ration.</summary>
    public static long FoodDays(long days, long soldiers = 3_000, long civilians = 6_000) => SiegeScenarios.Food(soldiers, civilians, days);

    public sealed record Built(SiegeWorld W, EventScheduler S, Runner R, long Start);

    /// <summary>
    /// A standard medieval-style city siege: installation 0 (city at 10,10, polity 0), garrison formations 0 (infantry 2,000) and
    /// 1 (bowmen 1,000), mobile formations of polity 0 at the city: 2 infantry 500, 3 naval 100, 5 air defence 200; formation 4
    /// infantry 300 of polity 1 at the city. Siege 0 by polity 1 starting at start.
    /// </summary>
    public static Built Build(long start, SiegeClock? clock = null, WarPulseSchedule? schedule = null, Func<SiegeSpec, SiegeSpec>? siege = null,
                              Func<InstallationSpec, InstallationSpec>? inst = null, FormationSpec[]? formations = null, RaidSpec[]? raids = null,
                              ShortfallPolicy policy = ShortfallPolicy.ProRata, SiegeCoefficients? k = null, bool stops = false,
                              SignificantKinds? sig = null, SubTurnOrderLog? replayLog = null, long money = 1_000_000_000, long warStart = -1,
                              ulong seed = 7, IAssaultResolver? resolver = null, long homeTroops = 0, bool noSiege = false, long[]? probes = null,
                              long homeFood = 1_000_000_000_000, LogisticsRates? rates = null, PolityCapability[]? caps = null, long homeMunitions = 1_000_000_000_000)
    {
        var i = new InstallationSpec(0, InstallationKind.City, 0, 10, 10, 10_000)
        {
            Civilians = 6_000, Food = FoodDays(120), Water = 9_000L * 60, WaterInflowPerYear = 9_000L * 360, Missiles = 1_000L * 200,
            Fort = FortKind.StoneWall, Housing = 12_000, ExitX = 11, ExitY = 10,
        };
        if (inst is not null) i = inst(i);
        formations ??= new[]
        {
            new FormationSpec(0, 0, FormationFamily.Infantry, 2_000, 10, 10) { GarrisonedAt = 0 },
            new FormationSpec(1, 0, FormationFamily.Ranged, 1_000, 10, 10) { GarrisonedAt = 0, Ranged = RangedKind.Bow },
            new FormationSpec(2, 0, FormationFamily.Infantry, 500, 10, 10),
            new FormationSpec(3, 0, FormationFamily.Naval, 100, 10, 10),
            new FormationSpec(4, 1, FormationFamily.Infantry, 300, 10, 10),
            new FormationSpec(5, 0, FormationFamily.AirDefence, 200, 10, 10) { Ranged = RangedKind.Rifle },
        };
        var sp = new SiegeSpec(0, 1, 0, start, AttackerForce.Of(8_000, 2_000, 1_000, 500, RangedKind.Bow, throwers: 10))
        {
            AttackerFood = SiegeScenarios.Food(13_000, 0, 60), AttackerSupplyPerYear = SiegeScenarios.RationYear(13_000, 0), CampCapacity = 20_000,
            AttackerWorksPermille = 300,
            AttackerAi = null, DefenderAi = null,
        };
        if (siege is not null) sp = siege(sp);
        long ws = warStart >= 0 ? warStart : start - 30 * Day;
        var s = new EventScheduler(ws);
        var w = new SiegeWorld(Cal, schedule ?? ProposedSchedule(), ws, start + 20 * SimInstant.MsPerYear, clock ?? SiegeClock.WarPulse, k ?? SiegeCoefficients.Default,
            rates ?? Rates, caps ?? new[] { Powder, Powder, Powder }, policy, seed, new[] { i }, formations, noSiege ? Array.Empty<SiegeSpec>() : new[] { sp }, raids,
            openingMoney: money, homeFood: homeFood, homeMunitions: homeMunitions, homeMissiles: 1_000_000_000_000, homeTroops: homeTroops, resolver: resolver);
        w.Begin(s);
        foreach (var p in probes ?? Array.Empty<long>()) SiegeWorld.ScheduleProbe(s, p);
        var sk = sig ?? new SignificantKinds(SiegeKinds.Probe, SiegeKinds.SiegeEnded, SiegeKinds.AssaultResolved);
        var r = replayLog is null ? new Runner(s, w, sk, stops) : Runner.CreateReplay(s, w, sk, replayLog);
        return new Built(w, s, r, start);
    }

    /// <summary>Run (stops off) until the given instant.</summary>
    public static void RunTo(Built b, long t) => b.R.RunUntil(t);

    /// <summary>Exact per-commodity totals of the ledger.</summary>
    /// <summary>One past the highest commodity code (the enum is shared with other lanes and grows by appending).</summary>
    public static readonly int CommodityCount = MaxCommodity() + 1;

    private static int MaxCommodity()
    {
        int m = 0;
        foreach (var c in Enum.GetValues<Commodity>()) m = Math.Max(m, (int)c);
        return m;
    }

    public static Int128[] Totals(WarLedger l)
    {
        var t = new Int128[CommodityCount];
        for (int c = 0; c < CommodityCount; c++) t[c] = l.Total((Commodity)c);
        return t;
    }

    /// <summary>Per-commodity totals of a snapshot of balances (the commodities come from the live ledger: accounts never change commodity).</summary>
    public static Int128[] Totals(WarLedger l, long[] balances)
    {
        var t = new Int128[CommodityCount];
        for (int a = 0; a < balances.Length; a++) t[(int)l.CommodityOf(a)] += balances[a];
        return t;
    }

    public static long Pack(int issuer, int target) => WarKinds.Pack(issuer, target);
}
