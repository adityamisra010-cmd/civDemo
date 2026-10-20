using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems.PathBuild;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Pathing;

/// <summary>
/// Batch 3 WS3 — PATHFINDING ON THE CYLINDER. On a wrapped lattice columns 0 and Size − 1 are 8-adjacent, and every
/// lattice consumer — A*, Dijkstra (Isochrone, Partition), the capped spacing relax, the passable flood, the leg costs
/// RouteQuery checks against A* exactly, and the origin-node ring scan — crosses the seam the same way. Checked on
/// hand-built FromCosts lattices against an INDEPENDENT reference Dijkstra (modular arithmetic of its own, not
/// MapTopology), and on the SeamWorld terrain. Flat lattices keep their pre-batch-3 answers (the reference run flat).
/// </summary>
public class SeamPathfindingTests
{
    private static readonly WorldState Empty = new(1);

    /// <summary>A size×size lattice with varied, tie-rich costs and an impassable wall down the MIDDLE columns, so on a
    /// cylinder the short way between the two halves runs across the seam.</summary>
    private static TraversalLattice Walled(int size, bool wrap, bool gapInWall = false)
    {
        var costs = new double[size * size];
        var passable = new bool[size * size];
        for (int i = 0; i < costs.Length; i++)
        {
            costs[i] = 1.0 + (i * 7 % 5) * 0.5;   // 1.0 .. 3.0, many exact ties
            int x = i % size, y = i / size;
            passable[i] = !(x == size / 2 || x == size / 2 - 1) || (gapInWall && y == size - 1);
        }
        return TraversalLattice.FromCosts(size, costs, passable, wrapX: wrap);
    }

    /// <summary>Plain O(n²) Dijkstra with its OWN neighbour rule (x modulo size when wrapping) — the optimality reference.</summary>
    private static double[] Reference(TraversalLattice lattice, int from, bool wrap)
    {
        int n = lattice.NodeCount, s = lattice.Size;
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
            int x = best % s, y = best / s;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (ny < 0 || ny >= s) continue;
                    if (wrap) nx = (nx + s) % s;
                    else if (nx < 0 || nx >= s) continue;
                    int nb = ny * s + nx;
                    if (!lattice.IsPassable(nb)) continue;
                    double alt = dist[best] + lattice.StepCost(best, nb);
                    if (alt < dist[nb]) dist[nb] = alt;
                }
        }
        return dist;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AStar_EqualsTheReferenceDijkstra_AllPairs_FlatAndWrapped(bool wrap)
    {
        // 9×9 (odd: no half-world tie) with the wall: every pair, both topologies. If the wrapped heuristic ever
        // overestimated (the raw flat dx does, across the seam), some A* cost would exceed the optimum.
        TraversalLattice lattice = Walled(9, wrap);
        int crossings = 0;
        for (int from = 0; from < lattice.NodeCount; from++)
        {
            if (!lattice.IsPassable(from)) continue;
            double[] reference = Reference(lattice, from, wrap);
            Pathfinder.IsochroneResult iso = Pathfinder.Isochrone(lattice, Empty, from, double.MaxValue);
            for (int to = 0; to < lattice.NodeCount; to++)
            {
                if (!lattice.IsPassable(to)) continue;
                Pathfinder.PathResult r = Pathfinder.FindPath(lattice, Empty, from, to);
                if (reference[to] == double.MaxValue)
                {
                    Assert.False(r.Found, $"{from}->{to} found a path the reference cannot");
                    Assert.True(Array.BinarySearch(iso.Reached, to) < 0);
                    continue;
                }
                Assert.True(r.Found, $"A* found no path {from}->{to}");
                Assert.True(Math.Abs(r.TotalCost - reference[to]) < 1e-12,
                    $"wrap={wrap}: A* {r.TotalCost} != reference {reference[to]} for {from}->{to}");
                int k = Array.BinarySearch(iso.Reached, to);
                Assert.True(k >= 0, $"Isochrone did not reach {to} from {from}");
                Assert.True(Math.Abs(iso.Costs[k] - reference[to]) < 1e-12);
                for (int i = 1; i < r.Nodes.Length; i++)
                    if (Math.Abs(r.Nodes[i] % 9 - r.Nodes[i - 1] % 9) > 1) { crossings++; break; }
            }
        }
        // The wall leaves the halves joined ONLY across the seam: wrapped, every cross-half pair crosses it; flat,
        // none can (and the halves are mutually unreachable — the reference agrees).
        if (wrap) Assert.True(crossings > 0, "no wrapped path crossed the seam — the rig is void");
        else Assert.Equal(0, crossings);
    }

    [Fact]
    public void ColumnsZeroAndLast_AreEightAdjacent_OnlyWhenWrapped()
    {
        TraversalLattice wrapped = Walled(9, true), flat = Walled(9, false);
        int a = wrapped.NodeId(0, 4), b = wrapped.NodeId(8, 4), diag = wrapped.NodeId(8, 5);
        // Cardinal seam step: one leg costing StepCost; a diagonal seam step costs the √2 diagonal.
        Assert.Equal(wrapped.StepCost(a, b), Pathfinder.LegCosts(wrapped, Empty, [a, b])[0]);
        Assert.Equal((wrapped.NodeCost[a] + wrapped.NodeCost[diag]) * 0.5 * TraversalLattice.DiagonalFactor,
            Pathfinder.LegCosts(wrapped, Empty, [a, diag])[0]);
        Assert.Equal(wrapped.StepCost(a, b), Pathfinder.FindPath(wrapped, Empty, a, b).TotalCost);
        Assert.Equal(2, Pathfinder.FindPath(wrapped, Empty, a, b).Nodes.Length);
        // Flat: not neighbours — LegCosts refuses the pair, and the halves are not connected at all.
        Assert.Throws<ArgumentException>(() => Pathfinder.LegCosts(flat, Empty, [a, b]));
        Assert.False(Pathfinder.FindPath(flat, Empty, a, b).Found);
    }

    [Fact]
    public void RouteQuery_AcrossTheSeam_SumsItsLegsExactly_AndDoesNotThrow()
    {
        TraversalLattice lattice = Walled(9, true);
        for (int y0 = 0; y0 < 9; y0++)
            for (int y1 = 0; y1 < 9; y1++)
            {
                RouteQuery.Route route = RouteQuery.Find(lattice, Empty, lattice.NodeId(1, y0), lattice.NodeId(7, y1));
                Assert.True(route.Found);
                Assert.Equal(Pathfinder.FindPath(lattice, Empty, lattice.NodeId(1, y0), lattice.NodeId(7, y1)).TotalCost,
                    route.TotalCost);
            }
    }

    [Fact]
    public void TheShortestPath_CrossesTheSeam_WhenThatIsShorter_AndNotWhenItIsNot()
    {
        // 12-wide lattice of UNIFORM cost (so the optimum is the geometric one) with the wall and a gap in its last
        // row, so both topologies connect: from x=1 to x=10 the short way is 3 steps west across the seam; flat it
        // must detour through the gap.
        const int n = 12;
        var costs = new double[n * n];
        var open = new bool[n * n];
        Array.Fill(costs, 1.0);
        for (int i = 0; i < open.Length; i++) open[i] = !(i % n == 5 || i % n == 6) || i / n == n - 1;
        TraversalLattice wrapped = TraversalLattice.FromCosts(n, costs, open, wrapX: true);
        TraversalLattice flat = TraversalLattice.FromCosts(n, costs, open, wrapX: false);
        int from = wrapped.NodeId(1, 3), to = wrapped.NodeId(10, 3);
        Pathfinder.PathResult w = Pathfinder.FindPath(wrapped, Empty, from, to);
        Pathfinder.PathResult f = Pathfinder.FindPath(flat, Empty, from, to);
        Assert.True(w.Found && f.Found);
        Assert.Equal(new[] { wrapped.NodeId(1, 3), wrapped.NodeId(0, 3), wrapped.NodeId(11, 3), wrapped.NodeId(10, 3) }, w.Nodes);
        Assert.Equal(3.0, w.TotalCost);
        Assert.True(w.TotalCost < f.TotalCost, $"wrapped {w.TotalCost} not shorter than flat {f.TotalCost}");
        // Where the inside way is shorter, the wrapped path stays inside: x=1 to x=4 is 3 steps east, never via the seam.
        Pathfinder.PathResult inside = Pathfinder.FindPath(wrapped, Empty, wrapped.NodeId(1, 3), wrapped.NodeId(4, 3));
        Assert.Equal(new[] { wrapped.NodeId(1, 3), wrapped.NodeId(2, 3), wrapped.NodeId(3, 3), wrapped.NodeId(4, 3) }, inside.Nodes);
    }

    [Fact]
    public void RelaxCappedFrom_AndThePassableFlood_CrossTheSeam()
    {
        TraversalLattice wrapped = Walled(9, true), flat = Walled(9, false);
        int origin = wrapped.NodeId(0, 4), across = wrapped.NodeId(8, 4);
        var w = new double[wrapped.NodeCount];
        var f = new double[flat.NodeCount];
        Array.Fill(w, double.PositiveInfinity);
        Array.Fill(f, double.PositiveInfinity);
        Pathfinder.RelaxCappedFrom(wrapped, origin, 10.0, w);
        Pathfinder.RelaxCappedFrom(flat, origin, 10.0, f);
        Assert.Equal(wrapped.StepCost(origin, across), w[across]);
        Assert.True(double.IsPositiveInfinity(f[across]));

        bool[] wc = PathBuildSystem.PassableComponent(wrapped, origin);
        bool[] fc = PathBuildSystem.PassableComponent(flat, origin);
        Assert.True(wc[across]);
        Assert.False(fc[across]);
        // Wrapped, the two halves are one component; flat, the origin's half only.
        int wn = 0, fn = 0, passable = 0;
        for (int i = 0; i < wc.Length; i++) { if (wc[i]) wn++; if (fc[i]) fn++; if (wrapped.IsPassable(i)) passable++; }
        Assert.Equal(passable, wn);
        Assert.True(fn < passable);
    }

    [Fact]
    public void IsochroneBoundary_SeesTheUnreachedNeighbourAcrossTheSeam()
    {
        // A 6×6 lattice whose only passable nodes are the origin (0, 2) and column 5 (across the seam from it). With a
        // zero budget only the origin is reached; on the cylinder its unreached passable neighbours lie across the seam,
        // so it IS a boundary node; flat it has no passable neighbour at all and is not.
        const int size = 6;
        var costs = new double[size * size];
        var passable = new bool[size * size];
        Array.Fill(costs, 1.0);
        for (int i = 0; i < passable.Length; i++) passable[i] = i == 2 * size || i % size == size - 1;
        TraversalLattice wrapped = TraversalLattice.FromCosts(size, costs, passable, wrapX: true);
        TraversalLattice flat = TraversalLattice.FromCosts(size, costs, passable, wrapX: false);
        int origin = 2 * size;
        Assert.Equal(new[] { origin }, Pathfinder.Isochrone(wrapped, Empty, origin, 0.0).Boundary);
        Assert.Empty(Pathfinder.Isochrone(flat, Empty, origin, 0.0).Boundary);
    }

    [Fact]
    public void OriginLatticeNode_RingScan_WrapsAndOrdersTheRingExplicitlyByNodeId()
    {
        // 6×6 wrapped lattice, every node impassable except three in ring 1 of the site's node (0, 1): (5, 0) — id 5,
        // across the seam and the FIRST one the dy-then-dx scan meets — (1, 0) — id 1, met after it — and (1, 2) — id 13.
        // The key is (ring, node id), so the explicit order must pick id 1, not the first one scanned.
        const int size = 6;
        var costs = new double[size * size];
        var passable = new bool[size * size];
        Array.Fill(costs, 1.0);
        passable[5] = true;   // (5, 0): across the seam, ring 1 of (0, 1)
        passable[1] = true;   // (1, 0): ring 1, smaller id, later in the scan
        passable[13] = true;  // (1, 2): ring 1, larger id
        TraversalLattice wrapped = TraversalLattice.FromCosts(size, costs, passable, wrapX: true);
        TraversalLattice flat = TraversalLattice.FromCosts(size, costs, passable, wrapX: false);
        int site = 1 * size + 0;  // terrain == lattice at stride 1: cell (0, 1)
        Assert.Equal(1, LatticeMap.OriginLatticeNode(wrapped, size, site));
        Assert.Equal(1, LatticeMap.OriginLatticeNode(flat, size, site));

        // Only the seam candidate: wrapped it is ring 1 (one step west across the seam); flat it is ring 5.
        var only = new bool[size * size];
        only[5] = true;
        only[2 * size + 3] = true;   // (3, 2): ring 3 from (0, 1) either way
        TraversalLattice w2 = TraversalLattice.FromCosts(size, costs, only, wrapX: true);
        TraversalLattice f2 = TraversalLattice.FromCosts(size, costs, only, wrapX: false);
        Assert.Equal(5, LatticeMap.OriginLatticeNode(w2, size, site));
        Assert.Equal(2 * size + 3, LatticeMap.OriginLatticeNode(f2, size, site));
    }

    [Fact]
    public void OriginLatticeNode_Wrapped_NeverScansAColumnTwice_OnAnEvenRing()
    {
        // A 4-wide wrapped lattice: from column 0, dx = +2 and dx = −2 name the SAME column. Only one passable node,
        // at (2, 0) — ring 2 at the tie — and the scan must return it (once), not throw or double-count.
        const int size = 4;
        var costs = new double[size * size];
        var passable = new bool[size * size];
        Array.Fill(costs, 1.0);
        passable[2] = true;
        TraversalLattice w = TraversalLattice.FromCosts(size, costs, passable, wrapX: true);
        Assert.Equal(2, LatticeMap.OriginLatticeNode(w, size, 0));
    }

    // ------------------------------------------------------------------ the SeamWorld terrain

    [Fact]
    public void SeamWorld_TheTwoSides_AreOneLandmassOnTheCylinder_AndTwoIslandsFlat()
    {
        double rcf = TestConfigs.RiverCostFactor();
        TraversalLattice wrapped = TraversalLattice.Build(SeamWorld.Terrain(wrapX: true), rcf);
        TraversalLattice flat = TraversalLattice.Build(SeamWorld.Terrain(wrapX: false), rcf);
        Assert.True(wrapped.WrapX);
        Assert.False(flat.WrapX);
        int[] sites = SeamWorld.TwoSides();
        int size = SeamWorld.DefaultSize;
        int west = LatticeMap.OriginLatticeNode(wrapped, size, sites[0]);
        int east = LatticeMap.OriginLatticeNode(wrapped, size, sites[1]);

        RouteQuery.Route route = RouteQuery.Find(wrapped, Empty, west, east);
        Assert.True(route.Found);
        bool crosses = false;
        for (int i = 1; i < route.Nodes.Length; i++)
            if (Math.Abs(route.Nodes[i] % wrapped.Size - route.Nodes[i - 1] % wrapped.Size) == wrapped.Size - 1) crosses = true;
        Assert.True(crosses, "the west-east route does not cross the seam");
        // 40 px = 10 lattice steps of ideal ground (cost 1.0): exactly 10.0 the short way.
        Assert.Equal(10.0, route.TotalCost);
        Assert.False(Pathfinder.FindPath(flat, Empty, west, east).Found);

        // A* equals Dijkstra over the seam world (from both sides, to every reached node).
        foreach (int from in new[] { west, east })
        {
            Pathfinder.IsochroneResult iso = Pathfinder.Isochrone(wrapped, Empty, from, double.MaxValue);
            for (int k = 0; k < iso.Reached.Length; k += 7)
                Assert.Equal(iso.Costs[k], Pathfinder.FindPath(wrapped, Empty, from, iso.Reached[k]).TotalCost, 12);
        }
    }

    /// <summary>W1 fix F10 — A* EQUALS DIJKSTRA ON EVERY PAIR OF THE SEAM WORLD, WITH VARIED GROUND. The sampled check
    /// above runs at uniform cost from two origins; here every passable lattice node of the cylindrical seam world is an
    /// origin, every node it reaches a target, and the land's movement cost varies 1–5 by a fixed hash of the cell, so
    /// the optimum is not the geometric shortest path and the wrapped heuristic is exercised across the seam on uneven
    /// ground. A* (FindPath, heuristic on the wrapped dx) must cost exactly what the Isochrone's Dijkstra reaches the
    /// node at — to 12 significant digits — and must reach exactly the nodes the Dijkstra reaches.</summary>
    [Fact]
    public void SeamWorld_AStarEqualsDijkstra_AllPairs_OnVariedGround()
    {
        TerrainSet t = SeamWorld.Terrain(wrapX: true);
        double[] movement = t.MovementCost.ToArray();
        for (int i = 0; i < movement.Length; i++)
            if (t.Water[i] < 0.5) movement[i] = 1.0 + (int)(Sim.Core.Kernel.SplitMix64.Mix((ulong)i * 0x9E3779B97F4A7C15UL) % 5UL);
        TerrainSet varied = TerrainSet.FromLayers(t.Size, t.KmPerPx, t.SeaLevel, t.Elevation, t.Water, t.Temperature,
            t.Moisture, t.Fertility, movement, t.Rivers, [], wrapX: true);
        TraversalLattice lattice = TraversalLattice.Build(varied, TestConfigs.RiverCostFactor());
        Assert.True(lattice.WrapX);
        int origins = 0, pairs = 0, crossing = 0;
        for (int from = 0; from < lattice.NodeCount; from++)
        {
            if (!lattice.IsPassable(from)) continue;
            origins++;
            Pathfinder.IsochroneResult iso = Pathfinder.Isochrone(lattice, Empty, from, double.MaxValue);
            var reached = new HashSet<int>(iso.Reached);
            for (int to = 0; to < lattice.NodeCount; to++)
            {
                if (!lattice.IsPassable(to)) continue;
                Pathfinder.PathResult r = Pathfinder.FindPath(lattice, Empty, from, to);
                Assert.Equal(reached.Contains(to), r.Found);
                if (!r.Found) continue;
                double dijkstra = iso.Costs[Array.BinarySearch(iso.Reached, to)];
                Assert.True(Math.Abs(r.TotalCost - dijkstra) <= 1e-12 * Math.Max(1.0, dijkstra),
                    $"{from}->{to}: A* {r.TotalCost} != Dijkstra {dijkstra}");
                pairs++;
                for (int i = 1; i < r.Nodes.Length; i++)
                    if (Math.Abs(r.Nodes[i] % lattice.Size - r.Nodes[i - 1] % lattice.Size) == lattice.Size - 1) { crossing++; break; }
            }
        }
        Assert.True(origins > 500, $"only {origins} passable origins — the rig is not the seam world");
        Assert.True(crossing > pairs / 10, $"only {crossing} of {pairs} optimal paths cross the seam — the rig does not exercise it");
    }
}
