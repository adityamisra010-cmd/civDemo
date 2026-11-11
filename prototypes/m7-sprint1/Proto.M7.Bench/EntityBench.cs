using System.Diagnostics;
using System.Globalization;
using Proto.M7.Caps;
using Proto.M7.Spatial;
using Proto.M7.Tactical;

// Lane CB (D-082 DA5): what a battle costs against the persons each side fields, under the one rule (one formation of min(P, cap)
// battle entities standing for P persons) and under the superseded persons rule (READING C1 (a): ceil(P / cap) formations of at most
// cap persons, one person per entity). Heavy infantry (cap 50) on both sides, an open 150 x 100 field of 20 m cells, the knowledge-
// only AI on both sides (TacticalResolver) and the AutoResolver (model 2). The TacticalResolver is timed over its first `pulses`
// pulses (or to its end), so the per-pulse cost is comparable across sizes; the AutoResolver over the whole battle.
// Stopwatch/double for REPORTING ONLY (never fed back into a battle). Usage:
//   dotnet Proto.M7.Bench.dll entities [persons per side, comma list = 50,250,500,1000,2000] [pulses = 10]
static class EntityBench
{
    private const long CellMm = 20_000;
    private const int W = 150, H = 100;

    public static void Run(string[] args)
    {
        var inv = CultureInfo.InvariantCulture;
        long[] sizes = (args.Length > 1 ? args[1] : "50,250,500,1000,2000").Split(',').Select(s => long.Parse(s, inv)).ToArray();
        int pulses = args.Length > 2 ? int.Parse(args[2], inv) : 10;
        Console.WriteLine($"Proto.M7.Bench entities: sizes={string.Join(",", sizes)} pulses={pulses} cpu={Environment.ProcessorCount} os={System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        // warm-up (JIT)
        Tactical(Build(50, false, 99), 2);
        new AutoBattle(Build(50, false, 99), new AutoCoefficients()).Run();
        foreach (long p in sizes)
            foreach (bool da5 in new[] { true, false })
            {
                var setup = Build(p, da5, 1);
                long entities = setup.Formations.Sum(f => (long)f.Count);
                var (tMs, tPulses, ended, buildMs) = Tactical(setup, pulses);
                var sw = Stopwatch.StartNew();
                var auto = new AutoBattle(setup, new AutoCoefficients()).Run();
                sw.Stop();
                Console.WriteLine(string.Create(inv,
                    $"{(da5 ? "DA5 one rule " : "persons rule ")} persons/side {p,6}: formations {setup.Formations.Length,4}, entities {entities,6} | tactical: construct {buildMs,8:F1} ms, {tPulses,3} pulses{(ended ? " (ended)" : "")} {tMs,10:F1} ms = {tMs / Math.Max(1, tPulses),9:F1} ms/pulse | auto: {auto.Pulses,3} pulses {sw.Elapsed.TotalMilliseconds,8:F1} ms, result {auto.ResultHash()[..12]}"));
            }
    }

    private static (double Ms, long Pulses, bool Ended, double BuildMs) Tactical(BattleSetup setup, int pulses)
    {
        var sw = Stopwatch.StartNew();
        var b = new TacticalBattle(setup, new KnowledgeAi(), new KnowledgeAi());
        double build = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        b.Start();
        bool more = true;
        while (more && b.CurrentPulse < pulses) more = b.Advance();
        sw.Stop();
        return (sw.Elapsed.TotalMilliseconds, b.CurrentPulse, !more, build);
    }

    private static Vec2L Cell(long cx, long cy) => new(cx * CellMm + CellMm / 2, cy * CellMm + CellMm / 2);

    /// <summary>Two sides of <paramref name="persons"/> heavy infantry each. DA5: one formation per side, min(P, 50) entities standing
    /// for P (FormationSpec.Persons). Persons rule: FormationRoster.SplitSizes(P, 50) formations of one person per entity, laid out
    /// in columns 4 cells apart toward the side's own edge (rows 10, 14, ..., 86). Sides 580 m apart (columns 60 and 89).</summary>
    private static BattleSetup Build(long persons, bool da5, ulong seed)
    {
        var grid = new TerrainGrid(W, H, CellMm, TerrainClass.Open);
        var forms = new List<FormationSpec>();
        long id = 1;
        for (int side = 0; side < 2; side++)
        {
            long x0 = side == 0 ? 60 : 89, dir = side == 0 ? -1 : 1;
            if (da5)
            {
                long n = DirectorCaps.Values.EntitiesOf(CapFamily.HeavyInfantry, persons);
                forms.Add(new FormationSpec(id++, side, UnitProfiles.HeavyInfantry, checked((int)n), Cell(x0, 48)) { Persons = persons });
            }
            else
            {
                long[] parts = FormationRoster.SplitSizes(persons, 50);
                for (int k = 0; k < parts.Length; k++)
                {
                    long column = k / 20, row = k % 20;
                    forms.Add(new FormationSpec(id++, side, UnitProfiles.HeavyInfantry, checked((int)parts[k]), Cell(x0 + dir * 4 * column, 10 + 4 * row)));
                }
            }
        }
        var obj = new Vec2L[2];
        for (int s = 0; s < 2; s++)
        {
            Int128 sx = 0, sy = 0; long n = 0;
            foreach (var f in forms) if (f.Side == 1 - s) { sx += f.Anchor.X; sy += f.Anchor.Y; n++; }
            obj[s] = new Vec2L((long)(sx / n), (long)(sy / n));
        }
        var sides = new[] { new SideSpec(ExitEdge.West) { Objectives = new[] { obj[0] } }, new SideSpec(ExitEdge.East) { Objectives = new[] { obj[1] } } };
        return new BattleSetup(grid, TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), sides, forms.ToArray(), seed);
    }
}
