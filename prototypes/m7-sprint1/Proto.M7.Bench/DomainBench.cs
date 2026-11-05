using System.Diagnostics;
using Proto.M7.Tactical;

// Lane N bench: naval line battles (three eras) and the amphibious combined-arms battle. Stopwatch/double for REPORTING ONLY.
// Usage: Proto.M7.Bench domain [seeds=5]   |   Proto.M7.Bench domain golden
static class DomainBench
{
    /// <summary>Culture-invariant formatting of every report line (law 5; verification round 1, N-R1-09).</summary>
    static string Inv(FormattableString s) => FormattableString.Invariant(s);

    public static void Run(string[] args)
    {
        if (args.Length > 1 && args[1] == "golden")
        {
            foreach (var (name, make, c0, c1) in Cases())
            {
                var b = new TacticalBattle(make(42), c0(), c1());
                var r = b.Run();
                Console.WriteLine(Inv($"{name}: trace {r.TraceHash}\n  state {r.StateHash}\n  result {r.ResultHash()}\n  personnel {r.Personnel!.Hash}\n  {r.Reason} side={r.Side} holds={r.HoldsField} pulses={r.Pulses} events={r.EventsExecuted}"));
                foreach (var f in r.Formations) Console.WriteLine(Inv($"    F{f.FormationId} {f.Domain}: {f.Start}={f.Survivors}+{f.Killed}+{f.Surrendered} act={f.ActiveAtEnd} esc={f.Escaped} emb={f.EmbarkedAtEnd} dmgDealt={f.DamageDealt} dmgTaken={f.DamageTaken} attacks={f.AttacksMade} morale={f.MoraleEnd}"));
                foreach (var l in r.Personnel.Lines) Console.WriteLine(Inv($"    P F{l.FormationId} {l.Place} carrier={l.CarrierId} {l.Count}"));
                Console.WriteLine(RamStats(b));                                      // [RAM] D-081 contact counts
            }
            return;
        }
        if (args.Length > 2 && args[1] == "watch")
        {
            int ci = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
            ulong seed = args.Length > 3 ? ulong.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 42;
            var (name, make, c0, c1) = Cases().ElementAt(ci);
            var b = new TacticalBattle(make(seed), c0(), c1());
            Console.WriteLine(Inv($"watch {name} seed {seed}"));
            for (long p = 1; !b.IsOver && p < 400; p++)
            {
                b.RunUntilPulse(p);
                Console.WriteLine(Inv($"-- pulse {b.CurrentPulse} over={b.IsOver}"));
                foreach (var d in b.DomainEntities())
                    Console.WriteLine(Inv($"   {d.Id} F{d.FormationId} s{d.Side} {d.Domain} {d.State} {d.Phase} pos=({d.Pos.X / 1000},{d.Pos.Y / 1000})m hdg={d.Heading * 360 / 65536} v={d.Speed} hp={d.Hp} crew={d.Crew} emb={d.Embarked} fuel={d.Fuel} sorties={d.SortiesLeft}"));
                var groups = b.Entities().GroupBy(e => e.FormationId).Select(g => Inv($"F{g.Key}:") + string.Join(",", g.GroupBy(e => e.State).Select(x => Inv($"{x.Key}={x.Count()}"))));
                Console.WriteLine("   land " + string.Join(" ", groups));
            }
            foreach (var o in b.Orders) Console.WriteLine(Inv($"   order p{o.Pulse} s{o.Side} F{o.Request.FormationId} {o.Request.Kind} tgt={o.Request.TargetFormationId} inv={o.Invalid} budget={o.Budget}"));
            return;
        }
        int seeds = args.Length > 1 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 5;
        Console.WriteLine(Inv($"Proto.M7.Bench domain: seeds={seeds} cpu={Environment.ProcessorCount} os={System.Runtime.InteropServices.RuntimeInformation.OSDescription}"));
        foreach (var (name, make, c0, c1) in Cases()) Case(name, make, c0, c1, seeds);
    }

    /// <summary>[RAM] Executed rams (breach / glance) and non-ram bow collisions, with the closing speeds of the rams.</summary>
    static string RamStats(TacticalBattle b)
    {
        int breach = 0, glance = 0, coll = 0; long minVc = long.MaxValue, maxVc = 0;
        foreach (var e in b.Scheduler.Trace.Entries)
        {
            if (e.Outcome != Proto.M7.Events.TraceOutcome.Executed) continue;
            if (e.Kind == DomainKinds.Ram) { if (e.A == 0) breach++; else glance++; minVc = Math.Min(minVc, e.C); maxVc = Math.Max(maxVc, e.C); }
            else if (e.Kind == DomainKinds.RamCollision) coll++;
        }
        return Inv($"    rams breach={breach} glance={glance} collisions={coll} closing={(breach + glance > 0 ? minVc : 0)}..{maxVc} mm/s");
    }

    static ScriptedController Both(long a, long b) => new ScriptedController().Add(0, new OrderRequest(a, OrderKind.Attack, 0, b));

    static IEnumerable<(string, Func<ulong, BattleSetup>, Func<IFormationController>, Func<IFormationController>)> Cases()
    {
        yield return ("galleys 5v5 (oar, ram)", s => MultiDomainScenarios.LineBattle(DomainProfiles.Galley, 50_000, s), () => Both(1, 2), () => Both(2, 1));
        yield return ("sail line 5v5 (broadside, wind)", s => MultiDomainScenarios.LineBattle(DomainProfiles.ShipOfTheLine, 50_000, s,
            new DomainRules { Wind = true, WindFrom = MultiDomainScenarios.Deg(90) }), () => Both(1, 2), () => Both(2, 1));
        yield return ("turret 5v5 (engine, long range)", s => MultiDomainScenarios.LineBattle(DomainProfiles.TurretWarship, 200_000, s), () => Both(1, 2), () => Both(2, 1));
        yield return ("amphibious combined arms", s => MultiDomainScenarios.Amphibious(s), () => MultiDomainScenarios.AmphibiousPlan(), () => MultiDomainScenarios.AmphibiousDefence());
    }

    static void Case(string name, Func<ulong, BattleSetup> make, Func<IFormationController> c0, Func<IFormationController> c1, int seeds)
    {
        new TacticalBattle(make(1000), c0(), c1()).Run();     // warm-up (JIT)
        var msPerPulse = new List<double>();
        long totPulses = 0, totEvents = 0; double totMs = 0;
        for (int i = 0; i < seeds; i++)
        {
            var b = new TacticalBattle(make((ulong)(i + 1)), c0(), c1());
            var sw = Stopwatch.StartNew();
            var r = b.Run();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds;
            totPulses += r.Pulses; totEvents += r.EventsExecuted; totMs += ms;
            msPerPulse.Add(ms / r.Pulses);
            string forms = string.Join(" ", r.Formations.Select(f => Inv($"F{f.FormationId}:{f.Start}={f.Survivors}+{f.Killed}+{f.Surrendered}")));
            Console.WriteLine(Inv($"  {name} seed {i + 1}: {r.Reason} side={r.Side} holds={r.HoldsField} pulses={r.Pulses} events={r.EventsExecuted} {ms:F1} ms ({ms / r.Pulses:F2} ms/pulse) {forms} people {r.Personnel!.Total}/{r.Personnel.OpeningTotal}"));
        }
        msPerPulse.Sort();
        Console.WriteLine(Inv($"{name}: mean pulses {(double)totPulses / seeds:F1}, mean events {(double)totEvents / seeds:F0}, total {totMs:F0} ms, median ms/pulse {msPerPulse[msPerPulse.Count / 2]:F2}, max ms/pulse {msPerPulse[^1]:F2}"));
    }
}
