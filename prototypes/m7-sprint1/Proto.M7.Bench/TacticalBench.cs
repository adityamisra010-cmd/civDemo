using System.Diagnostics;
using Proto.M7.Tactical;

// Slice D bench: 50v50 and 60v60 battles (pulses, events, ms/pulse). Stopwatch/double for REPORTING ONLY.
static class TacticalBench
{
    public static void Run(string[] args)
    {
        if (args.Length > 1 && args[1] == "golden")
        {
            // Prints the replay-test golden (combined arms, seed 42, KnowledgeAi both sides).
            var g = new TacticalBattle(TacticalScenarios.CombinedArms(42), new KnowledgeAi(), new KnowledgeAi()).Run();
            Console.WriteLine($"trace {g.TraceHash}\nstate {g.StateHash}\nresult {g.ResultHash()}\nreason {g.Reason} pulses {g.Pulses} events {g.EventsExecuted}");
            return;
        }
        int seeds = args.Length > 1 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 5;
        Console.WriteLine($"Proto.M7.Bench tactical: seeds={seeds} cpu={Environment.ProcessorCount} os={System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        Case("50v50 heavy_infantry", s => TacticalScenarios.Meeting(UnitProfiles.HeavyInfantry, 50, UnitProfiles.HeavyInfantry, 50, s), seeds);
        Case("60v60 archers", s => TacticalScenarios.Meeting(UnitProfiles.Archers, 60, UnitProfiles.Archers, 60, s), seeds);
        Case("50v60 heavy_infantry vs archers", s => TacticalScenarios.Meeting(UnitProfiles.HeavyInfantry, 50, UnitProfiles.Archers, 60, s), seeds);
        Case("110v110 combined arms", s => TacticalScenarios.CombinedArms(s), seeds);
    }

    static void Case(string name, Func<ulong, BattleSetup> make, int seeds)
    {
        // warm-up (JIT)
        new TacticalBattle(make(1000), new KnowledgeAi(), new KnowledgeAi()).Run();
        var msPerPulse = new List<double>();
        long totPulses = 0, totEvents = 0; double totMs = 0;
        for (int i = 0; i < seeds; i++)
        {
            var b = new TacticalBattle(make((ulong)(i + 1)), new KnowledgeAi(), new KnowledgeAi());
            var sw = Stopwatch.StartNew();
            var r = b.Run();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds;
            totPulses += r.Pulses; totEvents += r.EventsExecuted; totMs += ms;
            msPerPulse.Add(ms / r.Pulses);
            string forms = string.Join(" ", r.Formations.Select(f => $"F{f.FormationId}:{f.Start}={f.Survivors}+{f.Killed}+{f.Surrendered}"));
            Console.WriteLine($"  {name} seed {i + 1}: {r.Reason} side={r.Side} holds={r.HoldsField} pulses={r.Pulses} events={r.EventsExecuted} dropped={r.EventsDropped} {ms:F1} ms ({ms / r.Pulses:F2} ms/pulse, {r.EventsExecuted / (ms / 1000.0) / 1e6:F2} M ev/s) {forms}");
        }
        msPerPulse.Sort();
        Console.WriteLine($"{name}: mean pulses {(double)totPulses / seeds:F1}, mean events {(double)totEvents / seeds:F0}, total {totMs:F0} ms, median ms/pulse {msPerPulse[msPerPulse.Count / 2]:F2}, max ms/pulse {msPerPulse[^1]:F2}");
    }
}
