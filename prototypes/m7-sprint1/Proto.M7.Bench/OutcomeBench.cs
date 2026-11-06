using System.Diagnostics;
using System.Globalization;
using Proto.M7.Events;
using Proto.M7.Tactical;
using Proto.M7.Time;
using Proto.M7.War.Outcomes;

/// <summary>
/// Lane W: cost of the war-outcome layer (Stopwatch and the IEEE type for REPORTING ONLY; nothing measured feeds the simulation).
/// Usage: dotnet Proto.M7.Bench.dll outcomes [polities=64] [cities=2000] [captures=4000] [reps=3]
///   1. control churn: `captures` captures at distinct instants (each settles the leader rule over every polity), every 4th sacked
///      by an AI victor; wall time per capture, final state hash (replay identity across reps);
///   2. collapse: the CombinedArms tactical battle collapsed at 100 persons per entity (FromTactical + the world's flows);
///   3. record export: every table encoded (bytes, time).
/// </summary>
static class OutcomeBench
{
    public static void Run(string[] args)
    {
        int polities = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 64;
        int cities = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 2_000;
        int captures = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 4_000;
        int reps = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 3;
        var cal = CampaignCalendar.FromJsonFile(Path.Combine(AppContext.BaseDirectory, "fixtures", "era-pacing.json"));
        var table = new long[cal.Bands.Count];
        for (int i = 0; i < table.Length; i++) table[i] = 7 * SimInstant.MsPerDay;
        var sched = new WarPulseSchedule(cal, table);
        long w0 = (5_500L * SimInstant.YearDays + 100) * SimInstant.MsPerDay;   // year 1500, day 100
        Console.WriteLine($"Proto.M7.Bench outcomes: polities={polities} cities={cities} captures={captures} reps={reps} cpu={Environment.ProcessorCount}");

        string? firstHash = null;
        var times = new List<double>();
        for (int r = 0; r < reps; r++)
        {
            var pol = new OutcomePolity[polities];
            for (int p = 0; p < polities; p++) pol[p] = new OutcomePolity(p, 1_000 + p) { Seat = p, Treasury = 1_000_000, Granary = 10_000_000, Institutions = OutcomeInstitution.Coinage };
            var set = new OutcomeSettlement[cities];
            for (int c = 0; c < cities; c++)
                set[c] = new OutcomeSettlement(c, true, c % polities) { Civilians = 5_000, Grain = 1_000_000, Money = 100_000, Buildings = 500, BaseGrievancePermilleYear = 1_000 };
            var side0 = new List<int>(); var side1 = new List<int>();
            for (int p = 0; p < polities; p++) (p % 2 == 0 ? side0 : side1).Add(p);
            var wars = new[] { new OutcomeWar(0, w0, side0.ToArray(), side1.ToArray()) };
            var ai = new int[polities];
            for (int p = 0; p < polities; p++) ai[p] = p % 4 == 0 ? (int)Disposition.Sack : (int)Disposition.Occupy;
            var s = new EventScheduler(w0);
            var w = new WarOutcomeWorld(cal, sched, pol, set, wars, Array.Empty<OutcomeFormation>(), OutcomeCoefficients.Proposal, CaptiveExecutionRule.NotPermitted,
                                        RazePopulationRule.KillThenDisplace, ai);
            w.Begin(s);
            // a deterministic churn: capture k takes city (k * 7919) mod cities for polity (owner + 1 + k mod 3) mod polities
            var owner = new int[cities];
            for (int c = 0; c < cities; c++) owner[c] = c % polities;
            for (int k = 0; k < captures; k++)
            {
                int c = (int)((long)k * 7_919 % cities);
                int to = (owner[c] + 1 + k % 3) % polities;
                owner[c] = to;
                // outside any war (fix r1): the churn's victor sequence is not war-consistent (allies and eliminated polities take
                // cities), and the capture device now refuses a war in which the parties are not opposed
                WarOutcomeWorld.ScheduleCapture(s, w0 + (k + 1) * 3_600_000L, c, to, -1, 2_000);
            }
            var runner = new Runner(s, w, SignificantKinds.None, stopOnSignificant: false);
            var sw = Stopwatch.StartNew();
            runner.RunUntil(w0 + (captures + 2) * 3_600_000L);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
            string h = w.StateHashHex();
            firstHash ??= h;
            Console.WriteLine($"  rep {r}: {sw.Elapsed.TotalMilliseconds:F1} ms ({sw.Elapsed.TotalMilliseconds * 1000 / captures:F1} us per capture), fates={w.Fates.Count} battles={w.Battles.Count} hash={h[..16]} same={h == firstHash}");
            if (r == reps - 1)
            {
                var enc = Stopwatch.StartNew();
                var tables = w.EncodedTables();
                enc.Stop();
                long bytes = 0; foreach (var t in tables) bytes += t.Length;
                Console.WriteLine($"  record export: {tables.Count} tables, {bytes} bytes, {enc.Elapsed.TotalMilliseconds:F2} ms");
            }
        }
        times.Sort();
        Console.WriteLine($"control churn: median {times[times.Count / 2]:F1} ms for {captures} captures");

        // collapse of the CombinedArms battle at 100 persons per entity
        var setup = TacticalScenarios.CombinedArms(42);
        var battle = new TacticalBattle(setup, new KnowledgeAi(), new KnowledgeAi());
        battle.Run();
        var res = battle.Result!;
        var cs = new List<double>();
        for (int r = 0; r < reps; r++)
        {
            var pol = new[] { new OutcomePolity(0, 1) { Treasury = 1, Granary = 1_000_000 }, new OutcomePolity(1, 2) { Treasury = 1, Granary = 1_000_000 } };
            var forms = new[] { new OutcomeFormation(0, 0, 5_000), new OutcomeFormation(1, 0, 6_000), new OutcomeFormation(2, 1, 5_000), new OutcomeFormation(3, 1, 6_000) };
            var s = new EventScheduler(w0);
            var w = new WarOutcomeWorld(cal, sched, pol, Array.Empty<OutcomeSettlement>(), new[] { new OutcomeWar(0, w0, new[] { 0 }, new[] { 1 }) }, forms,
                                        OutcomeCoefficients.Proposal, CaptiveExecutionRule.NotPermitted, RazePopulationRule.KillThenDisplace);
            w.Begin(s);
            var sw = Stopwatch.StartNew();
            var inputs = BattleCollapse.FromTactical(battle, setup, new (long, int)[] { (1, 0), (2, 1), (3, 2), (4, 3) });
            w.ScheduleBattle(s, w0 + SimInstant.MsPerDay, new FieldBattle(0, w0, 0, 1, BattleMechanism.Tactical, (byte)res.Reason, res.HoldsField, inputs));
            new Runner(s, w, SignificantKinds.None, false).RunUntil(w0 + 2 * SimInstant.MsPerDay);
            sw.Stop();
            cs.Add(sw.Elapsed.TotalMilliseconds);
        }
        cs.Sort();
        Console.WriteLine($"collapse (220 entities, 22,000 persons): median {cs[cs.Count / 2]:F3} ms");
    }
}
