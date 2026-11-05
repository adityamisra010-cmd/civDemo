using System.Diagnostics;
using System.Globalization;
using Proto.M7.Rng;
using Proto.M7.Spatial;

/// <summary>
/// Slice C spatial timings (spec §8: "A* on 250x250 grid"). Stopwatch/double are REPORTING ONLY.
/// Usage: dotnet Proto.M7.Bench.dll spatial [reps=7]
/// </summary>
internal static class SpatialBench
{
    private static double Median(List<double> xs) { xs.Sort(); return xs[xs.Count / 2]; }

    public static void Run(string[] args)
    {
        int reps = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 7;
        var costs = TerrainCostTable.PlaceholderTestTable();
        var conceal = ConcealmentTable.PlaceholderTestTable();
        Console.WriteLine($"Proto.M7.Bench spatial: grid 250x250, cell 4000 mm, reps={reps}, cpu={Environment.ProcessorCount}, os={System.Runtime.InteropServices.RuntimeInformation.OSDescription}");

        var open = new TerrainGrid(250, 250);
        var mixed = new TerrainGrid(250, 250);
        for (int y = 0; y < 250; y++)
            for (int x = 0; x < 250; x++)
            {
                ulong r = CounterRng.NextBelow(42, 1, x, y, 0, 0, 100);
                mixed.Set(x, y, r < 15 ? TerrainClass.Wall : r < 18 ? TerrainClass.Water : (TerrainClass)CounterRng.NextBelow(42, 2, x, y, 0, 0, 6));
            }
        mixed.SetClass(0, 0, TerrainClass.Open); mixed.SetClass(249, 249, TerrainClass.Open);
        // Serpentine: a wall every 4th column with a one-cell gap alternating top/bottom => very long path.
        var maze = new TerrainGrid(250, 250);
        for (int x = 3; x < 250; x += 4)
            for (int y = 0; y < 250; y++)
                if (!(((x / 4) % 2 == 0) ? y == 249 : y == 0)) maze.SetClass(x, y, TerrainClass.Wall);
        // Unreachable: goal enclosed by a wall ring => the whole reachable area is expanded.
        var closed = new TerrainGrid(250, 250);
        for (int i = 200; i <= 210; i++) { closed.SetClass(i, 200, TerrainClass.Wall); closed.SetClass(i, 210, TerrainClass.Wall); closed.SetClass(200, i, TerrainClass.Wall); closed.SetClass(210, i, TerrainClass.Wall); }

        void Case(string name, TerrainGrid g, CellCoord s, CellCoord t)
        {
            var pf = new GridPathfinder(g, costs);
            var ms = new List<double>();
            PathResult? res = null;
            for (int r = 0; r < reps + 1; r++)
            {
                var sw = Stopwatch.StartNew();
                res = pf.FindPath(s, t);
                sw.Stop();
                if (r > 0) ms.Add(sw.Elapsed.TotalMilliseconds);       // first run = warm-up (JIT)
            }
            Console.WriteLine($"A* {name,-34}: median {Median(ms),8:F3} ms  found={res!.Found} cells={res.Cells.Length,4} expanded={res.Expanded,6} cost={res.Cost}");
        }
        Case("open, corner to corner", open, new(0, 0), new(249, 249));
        Case("open, (0,0)->(249,120)", open, new(0, 0), new(249, 120));
        Case("mixed terrain 15% walls, corner", mixed, new(0, 0), new(249, 249));
        Case("serpentine maze, corner", maze, new(0, 0), new(249, 249));
        Case("unreachable goal (full expansion)", closed, new(0, 0), new(205, 205));

        // 20 random query pairs on the mixed grid (one pathfinder instance, reused work arrays).
        {
            var pf = new GridPathfinder(mixed, costs);
            var ms = new List<double>();
            long found = 0, expanded = 0;
            for (int r = 0; r < reps; r++)
            {
                var sw = Stopwatch.StartNew();
                for (int q = 0; q < 20; q++)
                {
                    var res = pf.FindPath(new((long)CounterRng.NextBelow(9, 3, q, 0, 0, 0, 250), (long)CounterRng.NextBelow(9, 3, q, 1, 0, 0, 250)),
                                          new((long)CounterRng.NextBelow(9, 3, q, 2, 0, 0, 250), (long)CounterRng.NextBelow(9, 3, q, 3, 0, 0, 250)));
                    if (r == 0) { found += res.Found ? 1 : 0; expanded += res.Expanded; }
                }
                sw.Stop();
                ms.Add(sw.Elapsed.TotalMilliseconds / 20);
            }
            Console.WriteLine($"A* mixed, 20 random pairs              : median {Median(ms),8:F3} ms/query  found={found}/20 mean expanded={expanded / 20}");
        }

        // LOS throughput: 100k random sight lines on the mixed grid (forest/jungle concealment, walls).
        {
            var ms = new List<double>();
            long visible = 0;
            const int N = 100_000;
            for (int r = 0; r < reps; r++)
            {
                visible = 0;
                var sw = Stopwatch.StartNew();
                for (int q = 0; q < N; q++)
                {
                    var a = new Vec2L((long)CounterRng.NextBelow(5, 4, q, 0, 0, 0, 1_000_000), (long)CounterRng.NextBelow(5, 4, q, 1, 0, 0, 1_000_000));
                    var b = new Vec2L(a.X + (long)CounterRng.NextBelow(5, 4, q, 2, 0, 0, 120_001) - 60_000, a.Y + (long)CounterRng.NextBelow(5, 4, q, 3, 0, 0, 120_001) - 60_000);
                    if (LineOfSight.Query(mixed, conceal, a, 1700, b, 1700).Visible) visible++;
                }
                sw.Stop();
                ms.Add(sw.Elapsed.TotalMilliseconds);
            }
            double m = Median(ms);
            Console.WriteLine($"LOS 100k queries (<= 60 m offsets)     : median {m,8:F1} ms  => {N / (m / 1000.0) / 1e6:F2} M queries/s  visible={visible}");
        }

        // Separation: 120 movers (a 60v60-sized crowd) converging, one step.
        {
            var movers = new Mover[120];
            for (int i = 0; i < 120; i++) movers[i] = new Mover(i + 1, new(100_000 + i % 12 * 1_100, 100_000 + i / 12 * 1_100), 400, new(106_000, 105_000));
            var ms = new List<double>();
            for (int r = 0; r < reps + 1; r++)
            {
                var sw = Stopwatch.StartNew();
                Separation.Step(movers, open, costs);
                sw.Stop();
                if (r > 0) ms.Add(sw.Elapsed.TotalMilliseconds);
            }
            Console.WriteLine($"Separation step, 120 movers, grid check : median {Median(ms),8:F3} ms");
        }
    }
}
