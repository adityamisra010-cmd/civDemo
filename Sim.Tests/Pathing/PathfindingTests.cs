using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Worldgen;

namespace Sim.Tests.Pathing;

// T1.3 acceptance: path determinism (FsCheck, node sequences), A* == Dijkstra
// all-pairs on a handcrafted lattice, triangle inequality, isochrone properties,
// fast-lane reroute + expansion, measured timings.
public class PathfindingTests
{
    private static WorldgenConfig Dev()
    {
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        return WorldgenConfigLoader.Load(stream) with { SizePx = 256 };
    }

    private static (TraversalLattice Lattice, WorldState World) DevWorld(ulong seed)
    {
        var terrain = Sim.Core.Worldgen.Worldgen.Generate(Dev(), seed);
        var world = new WorldState(seed) { Terrain = terrain };
        return (TraversalLattice.Build(terrain), world);
    }

    private static int[] PassableNodes(TraversalLattice lattice)
    {
        var nodes = new List<int>();
        for (int i = 0; i < lattice.NodeCount; i++)
            if (lattice.IsPassable(i)) nodes.Add(i);
        return [.. nodes];
    }

    // The continental mask still leaves islands; endpoint pairs must share a
    // component to be connected. Largest component, ascending node id.
    private static int[] LargestComponentNodes(TraversalLattice lattice)
    {
        var component = new int[lattice.NodeCount];
        Array.Fill(component, -1);
        var sizes = new List<int>();
        var queue = new Queue<int>();
        for (int start = 0; start < lattice.NodeCount; start++)
        {
            if (!lattice.IsPassable(start) || component[start] != -1) continue;
            int id = sizes.Count, size = 0;
            component[start] = id;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                size++;
                (int x, int y) = lattice.Coords(i);
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= lattice.Size || ny >= lattice.Size) continue;
                    int nb = ny * lattice.Size + nx;
                    if (lattice.IsPassable(nb) && component[nb] == -1)
                    {
                        component[nb] = id;
                        queue.Enqueue(nb);
                    }
                }
            }
            sizes.Add(size);
        }
        int biggest = sizes.IndexOf(sizes.Max());
        var result = new List<int>();
        for (int i = 0; i < lattice.NodeCount; i++)
            if (component[i] == biggest) result.Add(i);
        return [.. result];
    }

    [Property(MaxTest = 30)]
    public Property PathDeterminism_TwinQueries_IdenticalNodeSequences()
    {
        var gen = from seed in Gen.Choose(1, 5)
                  from a in Gen.Choose(0, int.MaxValue - 1)
                  from b in Gen.Choose(0, int.MaxValue - 1)
                  select (seed, a, b);
        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            // Fully independent twins: fresh terrain, lattice, world per side.
            var (latticeA, worldA) = DevWorld((ulong)t.seed);
            var (latticeB, worldB) = DevWorld((ulong)t.seed);
            int[] nodes = PassableNodes(latticeA);
            int from = nodes[t.a % nodes.Length], to = nodes[t.b % nodes.Length];

            Pathfinder.PathResult ra = Pathfinder.FindPath(latticeA, worldA, from, to);
            Pathfinder.PathResult rb = Pathfinder.FindPath(latticeB, worldB, from, to);

            if (ra.Found != rb.Found) return false;
            if (!ra.Found) return true;
            // Identical NODE SEQUENCES, not merely identical costs.
            return ra.Nodes.AsSpan().SequenceEqual(rb.Nodes)
                && ra.TotalCost.Equals(rb.TotalCost);
        });
    }

    [Fact]
    public void AStar_EqualsDijkstra_AllPairs_OnHandcraftedLattice()
    {
        // 6×6 lattice, varied known costs, one impassable pond. If the heuristic
        // ever overestimated, some A* cost would exceed Dijkstra's optimum.
        const int size = 6;
        var costs = new double[size * size];
        var passable = new bool[size * size];
        for (int i = 0; i < costs.Length; i++)
        {
            costs[i] = 1.0 + (i * 7 % 5) * 1.5;   // 1.0 .. 7.0, deterministic variety
            passable[i] = true;
        }
        passable[14] = false; // (2,2)
        passable[15] = false; // (3,2)
        var lattice = TraversalLattice.FromCosts(size, costs, passable);
        var world = new WorldState(1); // empty network

        for (int from = 0; from < size * size; from++)
        {
            if (!passable[from]) continue;
            double[] dijkstra = ReferenceDijkstra(lattice, from);
            for (int to = 0; to < size * size; to++)
            {
                if (!passable[to]) continue;
                Pathfinder.PathResult r = Pathfinder.FindPath(lattice, world, from, to);
                if (dijkstra[to] == double.MaxValue)
                {
                    Assert.False(r.Found);
                    continue;
                }
                Assert.True(r.Found, $"A* found no path {from}->{to}");
                Assert.True(Math.Abs(r.TotalCost - dijkstra[to]) < 1e-12,
                    $"A* cost {r.TotalCost} != Dijkstra {dijkstra[to]} for {from}->{to}");
            }
        }
    }

    [Fact]
    public void PathDeterminism_TieRichLattice_TwinNodeSequencesIdentical_AllPairs()
    {
        // Adversarial-review finding (T1.3): the FsCheck twin property runs on
        // fractal terrain where exact double f-ties essentially never occur, so a
        // NONDETERMINISTIC tie-break (e.g. salted by runtime identity hash) passed
        // it. This test runs twins on the tie-DENSE handcrafted lattice — small
        // integer-ish costs produce many exact f-ties — and asserts identical
        // NODE SEQUENCES, binding the (f, node id) mandate where it matters.
        const int size = 6;
        var costs = new double[size * size];
        var passable = new bool[size * size];
        for (int i = 0; i < costs.Length; i++)
        {
            costs[i] = 1.0 + (i * 7 % 5) * 1.5;
            passable[i] = true;
        }
        passable[14] = false;
        passable[15] = false;
        var lattice = TraversalLattice.FromCosts(size, costs, passable);
        var world = new WorldState(1);

        for (int from = 0; from < size * size; from++)
        {
            if (!passable[from]) continue;
            for (int to = 0; to < size * size; to++)
            {
                if (!passable[to]) continue;
                Pathfinder.PathResult a = Pathfinder.FindPath(lattice, world, from, to);
                Pathfinder.PathResult b = Pathfinder.FindPath(lattice, world, from, to);
                Assert.Equal(a.Found, b.Found);
                if (!a.Found) continue;
                Assert.True(a.Nodes.AsSpan().SequenceEqual(b.Nodes),
                    $"twin node sequences differ for {from}->{to}");
                Assert.Equal(BitConverter.DoubleToInt64Bits(a.TotalCost),
                    BitConverter.DoubleToInt64Bits(b.TotalCost));
            }
        }
    }

    // Plain Dijkstra, independent implementation (no heuristic) — the optimality reference.
    private static double[] ReferenceDijkstra(TraversalLattice lattice, int from)
    {
        int n = lattice.NodeCount;
        var dist = new double[n];
        var done = new bool[n];
        Array.Fill(dist, double.MaxValue);
        dist[from] = 0.0;
        for (int iter = 0; iter < n; iter++)
        {
            int best = -1;
            double bestD = double.MaxValue;
            for (int i = 0; i < n; i++)
                if (!done[i] && dist[i] < bestD) { bestD = dist[i]; best = i; }
            if (best == -1) break;
            done[best] = true;
            (int x, int y) = lattice.Coords(best);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= lattice.Size || ny >= lattice.Size) continue;
                int nb = ny * lattice.Size + nx;
                if (!lattice.IsPassable(nb)) continue;
                double alt = dist[best] + lattice.StepCost(best, nb);
                if (alt < dist[nb]) dist[nb] = alt;
            }
        }
        return dist;
    }

    [Fact]
    public void TriangleInequality_AcrossRandomTriples()
    {
        // Arithmetic note: costs are double sums accumulated in different orders
        // for the three legs, so exact equality is not guaranteed; the tolerance
        // 1e-9 × cost covers double non-associativity (a few ULPs of ~1e2-1e4
        // magnitudes), far below any real violation.
        var (lattice, world) = DevWorld(seed: 42);
        int[] nodes = PassableNodes(lattice);
        var picks = new int[18];
        ulong x = 12345;
        for (int i = 0; i < picks.Length; i++)
        {
            x = Sim.Core.Kernel.SplitMix64.Mix(x + 1);
            picks[i] = nodes[(int)(x % (ulong)nodes.Length)];
        }
        for (int t = 0; t < picks.Length; t += 3)
        {
            int a = picks[t], b = picks[t + 1], c = picks[t + 2];
            Pathfinder.PathResult ab = Pathfinder.FindPath(lattice, world, a, b);
            Pathfinder.PathResult bc = Pathfinder.FindPath(lattice, world, b, c);
            Pathfinder.PathResult ac = Pathfinder.FindPath(lattice, world, a, c);
            if (!ab.Found || !bc.Found || !ac.Found) continue; // disconnected triple
            double lhs = ac.TotalCost, rhs = ab.TotalCost + bc.TotalCost;
            Assert.True(lhs <= rhs + 1e-9 * Math.Max(1.0, rhs),
                $"triangle violated: d({a},{c})={lhs} > d({a},{b})+d({b},{c})={rhs}");
        }
    }

    [Fact]
    public void Isochrone_Deterministic_Contiguous_Monotone()
    {
        var (lattice, world) = DevWorld(seed: 42);
        int origin = PassableNodes(lattice)[0];

        Pathfinder.IsochroneResult small = Pathfinder.Isochrone(lattice, world, origin, 200.0);
        Pathfinder.IsochroneResult smallTwin = Pathfinder.Isochrone(lattice, world, origin, 200.0);
        Pathfinder.IsochroneResult large = Pathfinder.Isochrone(lattice, world, origin, 400.0);

        // Twin-deterministic (node sets and costs).
        Assert.True(small.Reached.AsSpan().SequenceEqual(smallTwin.Reached));
        Assert.True(small.Costs.AsSpan().SequenceEqual(smallTwin.Costs));

        // Monotone: larger budget reaches a superset.
        var largeSet = new HashSet<int>(large.Reached);
        foreach (int node in small.Reached)
            Assert.Contains(node, largeSet);

        // Contiguous: one flood component — BFS over reached set from origin
        // covers everything reached.
        var reachedSet = new HashSet<int>(small.Reached);
        var seen = new HashSet<int> { origin };
        var queue = new Queue<int>();
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            (int cx, int cy) = lattice.Coords(i);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = cx + dx, ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= lattice.Size || ny >= lattice.Size) continue;
                int nb = ny * lattice.Size + nx;
                if (reachedSet.Contains(nb) && seen.Add(nb)) queue.Enqueue(nb);
            }
        }
        Assert.Equal(reachedSet.Count, seen.Count);
    }

    [Fact]
    public void FastLaneEdge_ReroutesPath_AndExpandsIsochrone()
    {
        var (lattice, world) = DevWorld(seed: 42);
        int[] nodes = LargestComponentNodes(lattice);
        int from = nodes[0], to = nodes[^1];

        Pathfinder.PathResult before = Pathfinder.FindPath(lattice, world, from, to);
        // Budget small enough that the isochrone is LOCAL (step costs are ~1-2
        // in lattice units; 20 ≈ a dozen steps) — otherwise it floods the whole
        // component and no fast lane could expand it.
        const double isoBudget = 20.0;
        Pathfinder.IsochroneResult isoBefore = Pathfinder.Isochrone(lattice, world, from, isoBudget);
        Assert.True(before.Found);
        Assert.True(before.TotalCost > isoBudget, "endpoints too close for the expansion proof");

        // Hand-add a dirt-path fast lane directly between the endpoints, far
        // cheaper than walking AND cheap enough to fit inside the isochrone
        // budget below (test setup writes state directly; system write
        // ownership starts at T1.6).
        double edgeCost = Math.Min(before.TotalCost * 0.1, isoBudget * 0.5);
        world.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(0), from));
        world.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(1), to));
        world.NetworkEdges.Add(new NetworkEdgeRow(
            new NetworkEdgeId(0), new NetworkNodeId(0), new NetworkNodeId(1),
            EdgeTypes.DirtPath, Cost: edgeCost));

        Pathfinder.PathResult after = Pathfinder.FindPath(lattice, world, from, to);
        Assert.True(after.Found);
        Assert.True(after.TotalCost < before.TotalCost, "fast lane did not reduce path cost");
        Assert.Equal(2, after.Nodes.Length);            // path is now the edge itself
        Assert.Equal(from, after.Nodes[0]);
        Assert.Equal(to, after.Nodes[1]);

        Pathfinder.IsochroneResult isoAfter = Pathfinder.Isochrone(lattice, world, from, isoBudget);
        Assert.True(isoAfter.Reached.Length > isoBefore.Reached.Length,
            "fast lane did not expand the isochrone");
    }

    [Fact]
    public void MeasuredTimings_CrossContinentPath_And_FullIsochrone_At1024()
    {
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        var cfg = WorldgenConfigLoader.Load(stream);
        var terrain = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed: 42);
        var lattice = TraversalLattice.Build(terrain);   // 256² nodes
        var world = new WorldState(42) { Terrain = terrain };
        int[] nodes = LargestComponentNodes(lattice);
        int from = nodes[0], to = nodes[^1];             // extremes of the main landmass

        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        Pathfinder.PathResult path = Pathfinder.FindPath(lattice, world, from, to);
        double pathMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0
                        / System.Diagnostics.Stopwatch.Frequency;

        t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        Pathfinder.IsochroneResult iso = Pathfinder.Isochrone(lattice, world, from, double.MaxValue / 4);
        double isoMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0
                       / System.Diagnostics.Stopwatch.Frequency;

        // No perf gate yet (spec): assert generous sanity bounds, report actuals.
        Assert.True(path.Found);
        Assert.True(iso.Reached.Length > 1000);
        Assert.True(pathMs < 2000, $"cross-continent path took {pathMs:F1} ms");
        Assert.True(isoMs < 2000, $"full isochrone took {isoMs:F1} ms");

        // Reported via test output for the acceptance record.
        Console.WriteLine($"cross-continent path: {pathMs:F1} ms, {path.Nodes.Length} nodes, cost {path.TotalCost:F1}");
        Console.WriteLine($"full isochrone: {isoMs:F1} ms, {iso.Reached.Length} nodes reached");
    }
}
