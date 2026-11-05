using System.Diagnostics;
using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;

// [TR] Lane TR bench + measurements (Stopwatch/double for REPORTING ONLY):
//   terrain cost   — tactical ms/pulse on a mixed-terrain map vs the open field (same scenarios, same seeds);
//   influence      — attacker/defender casualties of the hill and ford assaults vs flat/open (the unit-test geometry);
//   asymmetry      — per-side outcomes of the mirrored Meeting (control) and the preserved pre-F1 MeetingAsymmetric;
//   long battles   — pulses, events and trace entries of a long attrition duel (no pulse ceiling).
// Usage: dotnet Proto.M7.Bench.dll terrain [seeds=10]
static class TerrainBench
{
    public static void Run(string[] args)
    {
        int seeds = args.Length > 1 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 10;
        Console.WriteLine($"Proto.M7.Bench terrain: seeds={seeds} cpu={Environment.ProcessorCount} os={System.Runtime.InteropServices.RuntimeInformation.OSDescription}");

        Console.WriteLine("== cost: open field vs mixed terrain (KnowledgeAi both sides) ==");
        Cost("50v50 HI open   ", s => TacticalScenarios.Meeting(UnitProfiles.HeavyInfantry, 50, UnitProfiles.HeavyInfantry, 50, s), seeds);
        Cost("50v50 HI terrain", s => OnTerrain(TacticalScenarios.Meeting(UnitProfiles.HeavyInfantry, 50, UnitProfiles.HeavyInfantry, 50, s)), seeds);
        Cost("110v110 CA open   ", s => TacticalScenarios.CombinedArms(s), seeds);
        Cost("110v110 CA terrain", s => OnTerrain(TacticalScenarios.CombinedArms(s)), seeds);

        Console.WriteLine("== influence: 20 HI assault 20 HI holding the crest / far bank (seeds 1..8) ==");
        Influence("flat ", _ => { });
        Influence("hill ", g => { for (int y = 0; y < TacticalScenarios.FieldH; y++) for (int x = 25; x < TacticalScenarios.FieldW; x++) g.Set(x, y, TerrainClass.Open, 10_000); });
        Influence("ford ", g => { for (int y = 0; y < TacticalScenarios.FieldH; y++) for (int x = 23; x <= 24; x++) g.SetClass(x, y, TerrainClass.Ford); });

        Console.WriteLine("== asymmetry: per-side outcomes (50v50 HI, KnowledgeAi both) ==");
        Sides("mirrored Meeting (control)       ", s => TacticalScenarios.Meeting(UnitProfiles.HeavyInfantry, 50, UnitProfiles.HeavyInfantry, 50, s), 40);
        Sides("MeetingAsymmetric (20 m kept)    ", s => TacticalScenarios.MeetingAsymmetric(UnitProfiles.HeavyInfantry, 50, UnitProfiles.HeavyInfantry, 50, s), 40);

        Console.WriteLine("== parity scenarios (tactical side only): DIR5 hidden-stationary rule vs the legacy side-disengage rule ==");
        foreach (var id in new[] { ParityScenarioId.ArchersVsInfantryForest, ParityScenarioId.Ambush, ParityScenarioId.EqualInfantry })
            foreach (var model in new[] { InactivityModel.LegacySideDisengage, InactivityModel.HiddenStationary })
            {
                int w0 = 0, w1 = 0, und = 0; long pulses = 0, killed = 0, routedInactive = 0; int n = 0;
                var reasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
                for (ulong seed = 1; seed <= 10; seed++)
                    foreach (bool mir in new[] { false, true })
                    {
                        var s = ParityScenarios.Build(id, seed, mir, new TacticalRules { Inactivity = model });
                        var b = new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi());
                        var r = b.Run();
                        n++; pulses += r.Pulses;
                        foreach (var f in r.Formations) killed += f.Killed;
                        foreach (var v in b.InactivityVerdicts) if (v.Routed) routedInactive++;
                        if (r.HoldsField == 0) w0++; else if (r.HoldsField == 1) w1++; else und++;
                        string k = r.Reason.ToString();
                        reasons[k] = reasons.TryGetValue(k, out int c) ? c + 1 : 1;
                    }
                Console.WriteLine($"  {id,-24} {model,-20}: side0 held {w0}, side1 held {w1}, undecided {und}; mean pulses {(double)pulses / n:F1}; killed {killed}; inactivity routs {routedInactive}; ends [{string.Join(", ", reasons.Select(kv => $"{kv.Key} {kv.Value}"))}]");
            }

        Console.WriteLine("== parity scenarios (tactical side only): lane-TR terrain coefficients ON vs the Slice-C-equivalent table (OFF) ==");
        foreach (var id in new[] { ParityScenarioId.ArchersVsInfantryForest, ParityScenarioId.Ambush })
            foreach (var on in new[] { false, true })
            {
                int w0 = 0, w1 = 0, und = 0; long pulses = 0, killed0 = 0, killed1 = 0; int n = 0;
                for (ulong seed = 1; seed <= 10; seed++)
                    foreach (bool mir in new[] { false, true })
                    {
                        var s = ParityScenarios.Build(id, seed, mir);
                        if (!on) s = s with { Effects = TerrainEffects.SliceCEquivalent() };
                        var r = new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi()).Run();
                        n++; pulses += r.Pulses; killed0 += r.Formations[0].Killed; killed1 += r.Formations[1].Killed;
                        if (r.HoldsField == 0) w0++; else if (r.HoldsField == 1) w1++; else und++;
                    }
                Console.WriteLine($"  {id,-24} terrain {(on ? "ON " : "OFF")}: A held {w0}, B held {w1}, undecided {und}; mean pulses {(double)pulses / n:F1}; killed A {killed0}, B {killed1}");
            }

        Console.WriteLine("== long battle: attrition duel, hit chance 1/2000 ==");
        foreach (ulong seed in new ulong[] { 17, 18, 19 })
        {
            var rules = new TacticalRules { HitMin = Q32.FromRatio(1, 2_000), HitMax = Q32.FromRatio(1, 2_000) };
            Vec2L a = TacticalScenarios.Cell(20, 16), b = a + new Vec2L(1_000, 0);
            var s = new BattleSetup(TacticalScenarios.OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
                TacticalScenarios.DefaultSides(), new[]
                {
                    new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 1, a) { Entities = new[] { new EntitySpec(10, a) } },
                    new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 1, b) { Entities = new[] { new EntitySpec(20, b) } },
                }, seed) { Rules = rules };
            var bt = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
            var sw = Stopwatch.StartNew();
            var r = bt.Run();
            sw.Stop();
            Console.WriteLine($"  seed {seed}: {r.Reason} pulses={r.Pulses} events={r.EventsExecuted} traceEntries={bt.Scheduler.Trace.Count} " +
                              $"({(double)bt.Scheduler.Trace.Count / r.Pulses:F1}/pulse) {sw.Elapsed.TotalMilliseconds:F0} ms");
        }
    }

    /// <summary>The mixed-terrain map (48 x 32 cells of 20 m): a 10 m ridge of hills, a forest, a river with a ford, a village of
    /// buildings with a road, and a marsh. Forces keep their scenario positions (all on passable cells).</summary>
    public static BattleSetup OnTerrain(BattleSetup s)
    {
        var g = TacticalScenarios.OpenField();
        for (int y = 4; y <= 12; y++) for (int x = 20; x <= 27; x++) g.Set(x, y, TerrainClass.Hills, 10_000);
        for (int y = 18; y <= 26; y++) for (int x = 14; x <= 19; x++) g.SetClass(x, y, TerrainClass.Forest);
        for (int y = 13; y <= 31; y++) g.SetClass(24, y, y >= 15 && y <= 17 ? TerrainClass.Ford : TerrainClass.Water);
        for (int y = 19; y <= 21; y++) for (int x = 28; x <= 30; x++) g.SetClass(x, y, TerrainClass.Building);
        for (int x = 25; x <= 40; x++) g.SetClass(x, 18, TerrainClass.Road);
        for (int y = 24; y <= 30; y++) for (int x = 30; x <= 33; x++) g.SetClass(x, y, TerrainClass.Marsh);
        return s with { Grid = g };
    }

    static void Cost(string name, Func<ulong, BattleSetup> make, int seeds)
    {
        new TacticalBattle(make(1000), new KnowledgeAi(), new KnowledgeAi()).Run();   // JIT warm-up
        var per = new List<double>();
        long pulses = 0, events = 0; double total = 0;
        var reasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < seeds; i++)
        {
            var b = new TacticalBattle(make((ulong)(i + 1)), new KnowledgeAi(), new KnowledgeAi());
            var sw = Stopwatch.StartNew();
            var r = b.Run();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds;
            pulses += r.Pulses; events += r.EventsExecuted; total += ms;
            per.Add(ms / r.Pulses);
            string k = r.Reason.ToString();
            reasons[k] = reasons.TryGetValue(k, out int c) ? c + 1 : 1;
        }
        per.Sort();
        Console.WriteLine($"  {name}: mean pulses {(double)pulses / seeds:F1}, mean events {(double)events / seeds:F0}, " +
                          $"median ms/pulse {per[per.Count / 2]:F2} (min {per[0]:F2}, max {per[^1]:F2}), total {total:F0} ms, ends [{string.Join(", ", reasons.Select(kv => $"{kv.Key} {kv.Value}"))}]");
    }

    static void Influence(string name, Action<TerrainGrid> terrain)
    {
        long ak = 0, dk = 0, atkWins = 0;
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var g = TacticalScenarios.OpenField();
            terrain(g);
            var crest = new Vec2L(500_400, 330_000);
            var s = new BattleSetup(g, TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
                TacticalScenarios.DefaultSides(new[] { crest }, null), new[]
                {
                    new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, TacticalScenarios.Cell(16, 16)),
                    new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 20, crest),
                }, seed);
            var r = new TacticalBattle(s, new KnowledgeAi(), new ScriptedController().Add(0, new OrderRequest(2, OrderKind.Hold))).Run();
            ak += r.Formations[0].Killed; dk += r.Formations[1].Killed;
            if (r.HoldsField == 0) atkWins++;
        }
        Console.WriteLine($"  {name}: attacker killed {ak}, defender killed {dk}, net attacker loss {ak - dk}, attacker held the field {atkWins}/8");
    }

    static void Sides(string name, Func<ulong, BattleSetup> make, int n)
    {
        int lost0 = 0, lost1 = 0, other = 0; long k0 = 0, k1 = 0;
        for (int i = 1; i <= n; i++)
        {
            var r = new TacticalBattle(make((ulong)i), new KnowledgeAi(), new KnowledgeAi()).Run();
            if (r.HoldsField == 1) lost0++; else if (r.HoldsField == 0) lost1++; else other++;
            k0 += r.Formations[0].Killed; k1 += r.Formations[1].Killed;
        }
        Console.WriteLine($"  {name}: {n} seeds — west (side 0) lost {lost0}, east (side 1) lost {lost1}, undecided {other}; killed west {k0}, east {k1}");
    }
}
