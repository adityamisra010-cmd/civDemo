using Proto.M7.Events;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Outcomes;
using Proto.M7.War.Siege;
using Proto.M7.WarEcon;
using Proto.M7.Tests.Siege;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Outcomes;

/// <summary>Shared builders for the lane W tests. Every number is a TEST INPUT.</summary>
internal static class OutcomeRig
{
    public static readonly CampaignCalendar Cal = SiegeRig.Cal;
    /// <summary>War start; with the flat 7-day schedule pulse n of war 0 is [W0 + 7n d, W0 + 7(n+1) d).</summary>
    public static readonly long W0 = SiegeRig.At(1500, 100);
    public const long PulseMs = 7 * Day;
    public static long B(long n) => W0 + n * PulseMs;
    /// <summary>Independent oracle of the War Pulse index of instant t (flat 7-day pulses from W0).</summary>
    public static long PulseOracle(long t) => (t - W0) / PulseMs;

    public const OutcomeInstitution Coin = OutcomeInstitution.Coinage;

    /// <summary>Polity 0 (leader 100, seat 0), 1 (leader 101, seat 2), 2 (leader 102, seat 4); all hold Coinage.</summary>
    public static OutcomePolity[] Polities() => new[]
    {
        new OutcomePolity(0, 100) { Seat = 0, Treasury = 2_000_000, Granary = 50_000_000, HomeTroops = 1_000, Institutions = Coin },
        new OutcomePolity(1, 101) { Seat = 2, Treasury = 3_000_000, Granary = 50_000_000, HomeTroops = 1_000, Institutions = Coin },
        new OutcomePolity(2, 102) { Seat = 4, Treasury = 1_000_000, Granary = 50_000_000, HomeTroops = 1_000, Institutions = Coin },
    };

    /// <summary>Cities 0, 1 (polity 0), 2, 3 (polity 1), 4 (polity 2) and fort 5 (polity 0, not a city).</summary>
    public static OutcomeSettlement[] Settlements() => new[]
    {
        new OutcomeSettlement(0, true, 0) { Civilians = 10_000, Grain = 5_000_000, Money = 1_000_000, Buildings = 2_000, BaseGrievancePermilleYear = 1_000 },
        new OutcomeSettlement(1, true, 0) { Civilians = 4_000, Grain = 2_000_000, Money = 300_000, Buildings = 800, BaseGrievancePermilleYear = 1_000 },
        new OutcomeSettlement(2, true, 1) { Civilians = 8_000, Grain = 4_000_000, Money = 700_000, Buildings = 1_500, BaseGrievancePermilleYear = 1_000 },
        new OutcomeSettlement(3, true, 1) { Civilians = 3_000, Grain = 1_000_000, Money = 200_000, Buildings = 600, BaseGrievancePermilleYear = 1_000 },
        new OutcomeSettlement(4, true, 2) { Civilians = 5_000, Grain = 2_500_000, Money = 400_000, Buildings = 900, BaseGrievancePermilleYear = 1_000 },
        new OutcomeSettlement(5, false, 0) { Civilians = 200, Grain = 100_000, Buildings = 20 },
    };

    /// <summary>War 0 from W0: polities 1 and 2 (side 0) against polity 0 (side 1).</summary>
    public static OutcomeWar[] Wars() => new[] { new OutcomeWar(0, W0, new[] { 1, 2 }, new[] { 0 }) };

    public static OutcomeFormation[] Formations() => new[]
    {
        new OutcomeFormation(0, 0, 3_000), new OutcomeFormation(1, 0, 1_000), new OutcomeFormation(2, 1, 2_500), new OutcomeFormation(3, 1, 600),
    };

    public sealed record Built(WarOutcomeWorld W, EventScheduler S, Runner R);

    // The raze rule is a Director-level flag with no default in the world (fix r1, W-R1-8); these rigs pass the lane's first
    // proposal (KillThenDisplace) as a TEST INPUT unless a test names the other.

    public static Built Build(Action<EventScheduler, WarOutcomeWorld>? setup = null, bool stops = false, SubTurnOrderLog? replay = null, int[]? ai = null,
                              CaptiveExecutionRule rule = CaptiveExecutionRule.NotPermitted, Func<OutcomePolity[], OutcomePolity[]>? polities = null,
                              Func<OutcomeSettlement[], OutcomeSettlement[]>? settlements = null, OutcomeWar[]? wars = null,
                              OutcomeFormation[]? formations = null, OutcomeCoefficients? k = null, long[]? probes = null,
                              RazePopulationRule raze = RazePopulationRule.KillThenDisplace)
    {
        var s = new EventScheduler(W0);
        var pol = Polities(); if (polities is not null) pol = polities(pol);
        var set = Settlements(); if (settlements is not null) set = settlements(set);
        var w = new WarOutcomeWorld(Cal, SiegeRig.FlatSchedule(), pol, set, wars ?? Wars(), formations ?? Formations(), k ?? OutcomeCoefficients.Proposal, rule, raze, ai);
        w.Begin(s);
        setup?.Invoke(s, w);
        foreach (var p in probes ?? Array.Empty<long>()) WarOutcomeWorld.ScheduleProbe(s, p);
        var sig = WarOutcomeWorld.Significant(false);
        var r = replay is null ? new Runner(s, w, sig, stops) : Runner.CreateReplay(s, w, sig, replay);
        return new Built(w, s, r);
    }

    public static readonly (int, long, long)[] None = Array.Empty<(int, long, long)>();

    /// <summary>Run to the limit, issuing the given orders at each window the runner stops at.</summary>
    public static void Drive(Built b, long limit, Func<long, (int Kind, long A, long B)[]> ordersAt)
    {
        while (true)
        {
            var r = b.R.RunUntil(limit);
            if (r.Status == RunStatus.ReachedLimit) return;
            foreach (var o in ordersAt(r.AtMs)) b.R.IssueOrder(o.Kind, o.A, o.B);
        }
    }

    public static long Pack(int issuer, int target) => WarKinds.Pack(issuer, target);
    public static long Value(long code, long amount) => code * OutcomeKinds.ValueStride + amount;

    public static Int128[] Totals(WarLedger l) => SiegeRig.Totals(l);

    /// <summary>Capture (test device) with war 0.</summary>
    public static void Capture(EventScheduler s, long at, int settlement, int to, long sackers = 5_000)
        => WarOutcomeWorld.ScheduleCapture(s, at, settlement, to, 0, sackers);

    // ------------------------------------------------------------------ bound to lane S

    public sealed record BoundBuilt(WarOutcomeWorld W, SiegeWorld Siege, EventScheduler S, Runner R, long Start, long WarStart);

    /// <summary>
    /// Lane S's standard rig city (installation 0, polity 0, garrison formations 0 and 1) besieged by polity 1 from start, wrapped and
    /// bound by an outcome world in which installation 0 is polity 0's only city (its seat). Flat 7-day theatre pulses from the war start.
    /// </summary>
    public static BoundBuilt BuildBound(long start, long foodDays, bool stops = false, long[]? probes = null, IAssaultResolver? resolver = null,
                                        SubTurnOrderLog? replay = null, int[]? ai = null, RazePopulationRule raze = RazePopulationRule.KillThenDisplace)
    {
        long ws = start - 30 * Day;
        var inst = new InstallationSpec(0, InstallationKind.City, 0, 10, 10, 10_000)
        {
            Civilians = 6_000, Food = SiegeRig.FoodDays(foodDays), Water = 9_000L * 60, WaterInflowPerYear = 9_000L * 360, Missiles = 1_000L * 200,
            Fort = FortKind.StoneWall, Housing = 12_000, ExitX = 11, ExitY = 10,
        };
        var forms = new[]
        {
            new FormationSpec(0, 0, FormationFamily.Infantry, 2_000, 10, 10) { GarrisonedAt = 0 },
            new FormationSpec(1, 0, FormationFamily.Ranged, 1_000, 10, 10) { GarrisonedAt = 0, Ranged = RangedKind.Bow },
        };
        var sp = new SiegeSpec(0, 1, 0, start, AttackerForce.Of(8_000, 2_000, 1_000, 500, RangedKind.Bow, throwers: 10))
        {
            AttackerFood = SiegeScenarios.Food(13_000, 0, 60), AttackerSupplyPerYear = SiegeScenarios.RationYear(13_000, 0), CampCapacity = 20_000,
            AttackerWorksPermille = 300, AttackerAi = null, DefenderAi = null,
        };
        var s = new EventScheduler(ws);
        var schedule = SiegeRig.FlatSchedule();
        var siege = new SiegeWorld(Cal, schedule, ws, start + 20 * SimInstant.MsPerYear, SiegeClock.WarPulse, SiegeCoefficients.Default, SiegeRig.Rates,
            new[] { SiegeRig.Powder, SiegeRig.Powder, SiegeRig.Powder }, ShortfallPolicy.ProRata, 7, new[] { inst }, forms, new[] { sp }, null,
            openingMoney: 1_000_000_000, homeFood: 1_000_000_000_000, homeMunitions: 1_000_000_000_000, homeMissiles: 1_000_000_000_000, homeTroops: 0,
            resolver: resolver);
        var pol = new[]
        {
            new OutcomePolity(0, 100) { Seat = 0, Institutions = Coin },
            new OutcomePolity(1, 101) { Institutions = Coin },
            new OutcomePolity(2, 102) { Institutions = Coin },
        };
        var set = new[] { new OutcomeSettlement(0, true, 0) { Installation = 0, Money = 500_000, Buildings = 1_000, BaseGrievancePermilleYear = 1_000 } };
        var wars = new[] { new OutcomeWar(0, ws, new[] { 1 }, new[] { 0 }) };
        var w = new WarOutcomeWorld(Cal, schedule, pol, set, wars, Array.Empty<OutcomeFormation>(), OutcomeCoefficients.Proposal,
                                    CaptiveExecutionRule.NotPermitted, raze, ai, siege: siege);
        w.Begin(s);
        foreach (var p in probes ?? Array.Empty<long>()) SiegeWorld.ScheduleProbe(s, p);
        var sig = WarOutcomeWorld.Significant(true);
        var r = replay is null ? new Runner(s, w, sig, stops) : Runner.CreateReplay(s, w, sig, replay);
        return new BoundBuilt(w, siege, s, r, start, ws);
    }
}
