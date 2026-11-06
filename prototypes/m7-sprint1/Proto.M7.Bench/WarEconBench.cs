using System.Diagnostics;
using Proto.M7.Events;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.WarEcon;

// Lane E bench (Stopwatch/double for REPORTING ONLY): wall time of the per-pulse war economy on top of T2's War Pulse world.
// One war, `armies` early-modern field armies (polity 0) marching 180 km into a foreign town within supply range, plus
// `squadrons` sailing squadrons of 12 ships of the line in their home port, over `pulses` 7-day pulses. Reports median ms per run over 5 runs, us per
// army-pulse, and the state hash (identical across runs = deterministic in-process).
// Usage: dotnet Proto.M7.Bench.dll wareconE [armies=8] [squadrons=2] [pulses=520]
static class WarEconBench
{
    public static void Run(string[] args)
    {
        int armies = args.Length > 1 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 8;
        int squadrons = args.Length > 2 ? int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 2;
        int pulses = args.Length > 3 ? int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 520;
        Console.WriteLine($"Proto.M7.Bench wareconE: armies={armies} squadrons={squadrons} pulses={pulses} cpu={Environment.ProcessorCount}");
        var times = new List<double>();
        string? hash = null;
        int rows = 0;
        for (int run = 0; run < 5; run++)
        {
            var sw = Stopwatch.StartNew();
            var (w, n) = RunOnce(armies, squadrons, pulses);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
            string h = w.StateHashHex();
            if (hash is not null && h != hash) throw new InvalidOperationException("non-deterministic run");
            hash = h;
            rows = n;
        }
        times.Sort();
        double med = times[times.Count / 2];
        Console.WriteLine($"median {med:F1} ms per run, {med * 1000.0 / Math.Max(1, rows):F2} us per army-pulse row ({rows} rows), hash {hash![..16]}");
    }

    private static (WarEconomyWorld, int) RunOnce(int armies, int squadrons, int pulses)
    {
        var bandCal = CampaignCalendar.FromJsonFile(Path.Combine(AppContext.BaseDirectory, "fixtures", "era-pacing.json"));
        var table = new long[bandCal.Bands.Count];
        for (int i = 0; i < table.Length; i++) table[i] = 7 * SimInstant.MsPerDay;
        var sched = new WarPulseSchedule(bandCal, table);
        long start = bandCal.TurnStart(bandCal.Bands[3].FirstTurn).Ms + 10 * SimInstant.MsPerDay;
        long stop = start + pulses * 7 * SimInstant.MsPerDay;
        var war = new WarSpec(0, start, stop, new[] { 0, 1 }, 0, 0, new long[2]);
        var s = new EventScheduler(start);
        var inner = new WarTheaterWorld(bandCal, sched, 2, new[] { war }, WarFog.None, SignificantKinds.None, new long[2], 0, 0);
        var land = new Route(new[]
        {
            new RouteSegment(60_000, TerrainClass.Open, RoadClass.DirtPath, false, false, 0),
            new RouteSegment(120_000, TerrainClass.Hills, RoadClass.Track, false, false, 1),
        });
        // Squadrons in their home port (locality 0): victualled every pulse, so the fleet persists for the whole run.
        var sea = new Route(new[] { new RouteSegment(5_000_000, TerrainClass.Water, RoadClass.None, false, false, 0) });
        var list = new List<ArmySetup>();
        for (int i = 0; i < armies; i++)
            list.Add(new ArmySetup(i, 0, 0, i % 3, new ArmyComposition(5000, 600, 200, 700, 600, 150, 0, 20, true), land, 10_000 * (i % 8), true,
                                   100_000, 0, 5_000, new (long, long)[] { (3, 2) }));
        for (int j = 0; j < squadrons; j++)
            list.Add(new ArmySetup(armies + j, 0, 0, 0, new ArmyComposition(0, 0, 0, 0, 0, 0, 0, 0, false) { Vessel = DomainProfiles.ShipOfTheLine, Hulls = 12 },
                                   sea, 1_000_000, false, 5_000_000, 0, 0, Array.Empty<(long, long)>()));
        var tech = WarTech.Coinage | WarTech.Gunpowder | WarTech.Magazines;
        var setup = new WarEconSetup(FoodOrdering.CivilianMilitaryGrowth, LogisticsRates.Proposal(), TerrainLogistics.Proposal(),
            new[]
            {
                new PolitySetup(0, new PolityCapability(tech) { Naval = NavalCapability.Sails | NavalCapability.Gunpowder }, 2_000_000, 40_000_000_000,
                                5_000_000, 1_000_000_000_000, 100_000, 2_000_000, 200_000_000, 0, 0, 0, 0, 0, 0, 50),
                new PolitySetup(1, new PolityCapability(tech), 1_500_000, 5_000_000_000, 1_000_000, 1_000_000_000_000, 100_000, 50_000,
                                1_000_000, 0, 0, 0, 0, 1_000_000, 0, 50),
            },
            new[] { new LocalitySetup(0, 0, 6_000, 4_000_000, 10_000), new LocalitySetup(1, 1, 9_000, 6_000_000, 20_000) { TradeSharePermille = 200 } },
            list.ToArray());
        var w = new WarEconomyWorld(inner, bandCal, setup);
        w.Begin(s);
        var r = new Runner(s, w, SignificantKinds.None, stopOnSignificant: false);
        r.RunUntil(stop + SimInstant.MsPerDay);
        return (w, w.Rows.Count);
    }
}
