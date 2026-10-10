using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Roads;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;

namespace Sim.Tests.Pathing;

/// <summary>
/// M5 S1a — THE READ-ONLY ROUTE QUERY. Claims, each measured:
///   1. On random pairs, with and without built roads, the route is FindPath's (same node
///      sequence) and its final cumulative cost equals FindPath's TotalCost BIT FOR BIT.
///   2. Every per-node cumulative cost is the previous one plus that leg's cost, and the leg's cost
///      is a TraversalLattice.StepCost or a lane cost — recomputed here from the rows the test
///      wrote, independently of Pathfinder's overlay.
///   3. Among equal-cost routes the one returned is decided by the (f, node id) composite heap key
///      (tie-dense, hand-derived pins).
///   4. Parallel edges cost the cheapest, as A*'s strictly-better relaxation leaves them.
///   5. The budget prefix is inclusive at the exact boundary.
///   6. The query writes nothing (world hash unchanged).
/// </summary>
public class RouteQueryTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly RoadsConfig Roads = Cfg.Roads!;

    private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

    private static bool Adjacent(TraversalLattice lattice, int a, int b)
    {
        (int ax, int ay) = lattice.Coords(a);
        (int bx, int by) = lattice.Coords(b);
        return a != b && Math.Abs(ax - bx) <= 1 && Math.Abs(ay - by) <= 1;
    }

    private static TraversalLattice Uniform(int size, double cost = 1.0)
    {
        var costs = new double[size * size];
        var passable = new bool[size * size];
        Array.Fill(costs, cost);
        Array.Fill(passable, true);
        return TraversalLattice.FromCosts(size, costs, passable);
    }

    /// <summary>A lane the test itself wrote: its two lattice endpoints and its cost.</summary>
    private readonly record struct Lane(int A, int B, double Cost);

    /// <summary>Asserts claims 1 and 2 for one pair; returns the number of lane legs used.</summary>
    private static int AssertRouteAgrees(
        TraversalLattice lattice, WorldState world, IReadOnlyList<Lane> lanes, int from, int to, ref int found)
    {
        Pathfinder.PathResult path = Pathfinder.FindPath(lattice, world, from, to);
        RouteQuery.Route route = RouteQuery.Find(lattice, world, from, to);
        Assert.Equal(path.Found, route.Found);
        if (!path.Found)
        {
            Assert.Empty(route.Nodes);
            Assert.Equal(0, route.ReachablePrefixLength(double.PositiveInfinity));
            return 0;
        }
        found++;
        Assert.Equal(path.Nodes, route.Nodes);
        Assert.Equal(route.Nodes.Length, route.Cumulative.Length);
        Assert.Equal(Bits(path.TotalCost), Bits(route.TotalCost));
        Assert.Equal(Bits(path.TotalCost), Bits(route.Cumulative[^1]));
        Assert.Equal(Bits(0.0), Bits(route.Cumulative[0]));

        int laneLegs = 0;
        for (int k = 1; k < route.Nodes.Length; k++)
        {
            int a = route.Nodes[k - 1], b = route.Nodes[k];
            // The leg, from first principles: the lattice step if a and b are 8-neighbours, and every
            // lane the test wrote between them; the cheapest.
            bool joined = Adjacent(lattice, a, b);
            double leg = joined ? lattice.StepCost(a, b) : double.MaxValue;
            bool lane = false;
            foreach (Lane l in lanes)
            {
                if (!((l.A == a && l.B == b) || (l.A == b && l.B == a))) continue;
                if (!joined || l.Cost < leg) { leg = l.Cost; lane = true; }
                joined = true;
            }
            Assert.True(joined, $"leg {a}->{b} of route {from}->{to} is neither a step nor a lane");
            Assert.Equal(Bits(route.Cumulative[k - 1] + leg), Bits(route.Cumulative[k]));
            Assert.True(route.Cumulative[k] >= route.Cumulative[k - 1]);
            if (lane) laneLegs++;
        }
        return laneLegs;
    }

    private static int[] RandomPassable(TraversalLattice lattice, int count, ulong salt)
    {
        var passable = new List<int>();
        for (int i = 0; i < lattice.NodeCount; i++) if (lattice.IsPassable(i)) passable.Add(i);
        var picks = new int[count];
        ulong x = salt;
        for (int i = 0; i < count; i++)
        {
            x = SplitMix64.Mix(x + 1);
            picks[i] = passable[(int)(x % (ulong)passable.Count)];
        }
        return picks;
    }

    [Fact]
    public void RandomPairs_NoRoads_FinalCostEqualsFindPath_BitExact_AndEveryLegIsAStep()
    {
        int found = 0;
        foreach (ulong seed in new ulong[] { 1, 42 })
        {
            WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, seed);
            Assert.Equal(0, w.NetworkEdges.Count + w.TransportEdges.Count);
            TraversalLattice lattice = TraversalLattice.Build(w.Terrain!, Cfg.Transport.RiverCostFactor);
            int[] picks = RandomPassable(lattice, 80, seed * 7919);
            for (int i = 0; i < picks.Length; i += 2)
                Assert.Equal(0, AssertRouteAgrees(lattice, w, [], picks[i], picks[i + 1], ref found));
        }
        Console.WriteLine($"S1a route query, no roads: {found} of 80 random pairs connected.");
        Assert.True(found >= 40, $"only {found} of 80 random pairs were connected");
    }

    [Fact]
    public void RandomPairs_WithBuiltRoads_FinalCostEqualsFindPath_BitExact_AndLanesAreUsed()
    {
        int found = 0, laneLegs = 0;
        foreach (ulong seed in new ulong[] { 1, 42 })
        {
            WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, seed);
            TraversalLattice lattice = TraversalLattice.Build(w.Terrain!, Cfg.Transport.RiverCostFactor);
            double kmPerCost = LatticeGeometry.KmPerCostUnitOnIdealGround(lattice);
            var lanes = new List<Lane>();

            // ADR-032 road routes between every settlement pair, cycling the road classes, each as
            // long as walking there (so the road's speed factor makes the lane the cheaper way).
            int id = 0;
            for (int a = 0; a < w.Settlements.Count; a++)
            for (int b = a + 1; b < w.Settlements.Count; b++)
            {
                int na = LatticeMap.OriginLatticeNode(lattice, w.Terrain!.Size, w.Settlements[a].SiteCell);
                int nb = LatticeMap.OriginLatticeNode(lattice, w.Terrain!.Size, w.Settlements[b].SiteCell);
                Pathfinder.PathResult walk = Pathfinder.FindPath(lattice, w, na, nb);
                if (!walk.Found || na == nb) continue;
                int cls = EdgeTypes.RoadClasses[id % EdgeTypes.RoadClasses.Length];
                var row = new TransportEdgeRow(id++, w.Settlements[a].Id, w.Settlements[b].Id, cls,
                    TransportModes.Road, TransportEdgeStates.Complete,
                    RoadPerformance.EffectiveCapacity(Roads, cls, cls, 0.0), walk.TotalCost * kmPerCost,
                    0, 1, 1, cls, 0.0, RoadPerformance.EffectiveCostFactor(Roads, cls, cls, 0.0));
                w.TransportEdges.Add(row);
                lanes.Add(new Lane(na, nb, RoadPerformance.TravelCostUnits(row, kmPerCost)));
            }
            Assert.True(w.TransportEdges.Count > 0);

            // PathBuild-style network fast lanes between random node pairs, at a third of walking.
            int[] anchors = RandomPassable(lattice, 12, seed * 104729);
            for (int i = 0; i < anchors.Length; i += 2)
            {
                Pathfinder.PathResult walk = Pathfinder.FindPath(lattice, w, anchors[i], anchors[i + 1]);
                if (!walk.Found || anchors[i] == anchors[i + 1]) continue;
                int n = w.NetworkNodes.Count;
                w.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(n), anchors[i]));
                w.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(n + 1), anchors[i + 1]));
                double cost = walk.TotalCost / 3.0;
                w.NetworkEdges.Add(new NetworkEdgeRow(new NetworkEdgeId(w.NetworkEdges.Count),
                    new NetworkNodeId(n), new NetworkNodeId(n + 1), EdgeTypes.DirtPath, cost));
                lanes.Add(new Lane(anchors[i], anchors[i + 1], cost));
            }
            Assert.True(w.NetworkEdges.Count > 0);

            string before = WorldHash.ComputeHex(w);
            int[] picks = RandomPassable(lattice, 80, seed * 7919);
            for (int i = 0; i < picks.Length; i += 2)
                laneLegs += AssertRouteAgrees(lattice, w, lanes, picks[i], picks[i + 1], ref found);
            // Settlement-to-settlement routes, where the road lanes are the point.
            for (int a = 0; a < w.Settlements.Count; a++)
            for (int b = 0; b < w.Settlements.Count; b++)
            {
                if (a == b) continue;
                laneLegs += AssertRouteAgrees(lattice, w, lanes,
                    LatticeMap.OriginLatticeNode(lattice, w.Terrain!.Size, w.Settlements[a].SiteCell),
                    LatticeMap.OriginLatticeNode(lattice, w.Terrain!.Size, w.Settlements[b].SiteCell), ref found);
            }
            Assert.Equal(before, WorldHash.ComputeHex(w));           // read-only
        }
        Console.WriteLine($"S1a route query with roads: {found} connected routes, {laneLegs} lane legs.");
        Assert.True(found >= 40, $"only {found} pairs were connected");
        Assert.True(laneLegs >= 10, $"only {laneLegs} lane legs were taken — the road half is vacuous");
    }

    // ------------------------------------------------------------------ tie-dense: the (f, id) key

    [Fact]
    public void TieDense_EqualCostRoutes_AreDecidedByTheNodeIdTieBreak()
    {
        // 4×4 lattice, every node cost 1, ids row-major (id = 4y + x). From (0,0)=0 to (2,1)=6 there
        // are exactly two optimal routes, E-then-SE through (1,0)=1 and SE-then-E through (1,1)=5,
        // and they cost the SAME double (1 + √2 and √2 + 1 are one IEEE sum). A* reaches both
        // intermediate nodes with the same f = 1 + √2 (h is the straight-line distance × 1), so the
        // heap's (f, node id) key pops node 1 FIRST; node 1 relaxes node 6, and node 5's equal
        // relaxation is not strictly better, so node 6 keeps parent 1. Under any other tie rule
        // (insertion order, larger id first) the route would run through node 5.
        TraversalLattice lattice = Uniform(4);
        var world = new WorldState(1);

        RouteQuery.Route r = RouteQuery.Find(lattice, world, 0, 6);
        Assert.Equal([0, 1, 6], r.Nodes);
        Assert.Equal([0.0, 1.0, 1.0 + TraversalLattice.DiagonalFactor], r.Cumulative);
        // It IS a tie: the other route sums to the same bits.
        double[] other = Pathfinder.LegCosts(lattice, world, [0, 5, 6]);
        Assert.Equal(Bits(r.TotalCost), Bits(0.0 + other[0] + other[1]));

        // Reversed: from 6 to 0 the tied intermediates are again 1 and 5 (f = √2 + 1 = 1 + √2),
        // node 1 pops first, and node 0 (f = g, id 0) pops before node 5 — the route is 6, 1, 0.
        RouteQuery.Route back = RouteQuery.Find(lattice, world, 6, 0);
        Assert.Equal([6, 1, 0], back.Nodes);
        Assert.Equal(Bits(r.TotalCost), Bits(back.TotalCost));

        // A three-way tie: (0,0) → (2,2)=10 straight diagonal is unique, but (0,0) → (3,1)=7 has
        // three optimal routes (the diagonal leg first, second or third); ids pick 0,1,2,7.
        RouteQuery.Route three = RouteQuery.Find(lattice, world, 0, 7);
        Assert.Equal([0, 1, 2, 7], three.Nodes);
        foreach (int[] alt in new[] { new[] { 0, 1, 6, 7 }, new[] { 0, 5, 6, 7 } })
        {
            double[] legs = Pathfinder.LegCosts(lattice, world, alt);
            Assert.Equal(Bits(three.TotalCost), Bits(0.0 + legs[0] + legs[1] + legs[2]));
        }
    }

    [Fact]
    public void TieDense_AllPairs_RouteIsFindPaths_AndTwinCallsAreIdentical()
    {
        // Every pair on a uniform 6×6 lattice with a pond and one fast lane: exact f-ties everywhere.
        const int size = 6;
        var costs = new double[size * size];
        var passable = new bool[size * size];
        Array.Fill(costs, 1.0);
        Array.Fill(passable, true);
        passable[14] = passable[15] = passable[20] = false;
        var lattice = TraversalLattice.FromCosts(size, costs, passable);
        var world = new WorldState(1);
        world.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(0), 0));
        world.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(1), 35));
        world.NetworkEdges.Add(new NetworkEdgeRow(new NetworkEdgeId(0), new NetworkNodeId(0), new NetworkNodeId(1),
            EdgeTypes.DirtPath, 3.0));
        Lane[] lanes = [new Lane(0, 35, 3.0)];

        int found = 0;
        for (int from = 0; from < size * size; from++)
        for (int to = 0; to < size * size; to++)
        {
            if (!passable[from] || !passable[to]) continue;
            AssertRouteAgrees(lattice, world, lanes, from, to, ref found);
            RouteQuery.Route a = RouteQuery.Find(lattice, world, from, to);
            RouteQuery.Route b = RouteQuery.Find(lattice, world, from, to);
            Assert.Equal(a.Nodes, b.Nodes);
            Assert.Equal(a.Cumulative, b.Cumulative);
        }
        Assert.Equal(33 * 33, found);
    }

    // ------------------------------------------------------------------ parallel edges

    [Fact]
    public void ParallelEdges_TheLegCostsTheCheapest_ExactlyAsAStarRelaxes()
    {
        TraversalLattice lattice = Uniform(4);
        double Route01(params double[] laneCosts)
        {
            var world = new WorldState(1);
            world.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(0), 0));
            world.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(1), 1));
            for (int i = 0; i < laneCosts.Length; i++)
                world.NetworkEdges.Add(new NetworkEdgeRow(new NetworkEdgeId(i), new NetworkNodeId(i % 2),
                    new NetworkNodeId(1 - i % 2), EdgeTypes.DirtPath, laneCosts[i]));
            RouteQuery.Route r = RouteQuery.Find(lattice, world, 0, 1);
            Assert.Equal([0, 1], r.Nodes);
            Assert.Equal(Bits(Pathfinder.FindPath(lattice, world, 0, 1).TotalCost), Bits(r.TotalCost));
            return r.TotalCost;
        }
        Assert.Equal(1.0, Route01());                 // the step alone
        Assert.Equal(0.25, Route01(0.25));            // a cheaper lane beside the step
        Assert.Equal(1.0, Route01(3.0));              // a dearer lane loses to the step
        Assert.Equal(1.0, Route01(1.0));              // an equal lane: the same value either way
        Assert.Equal(0.3, Route01(0.5, 0.3, 0.7));    // three parallel lanes, both directions: the cheapest
    }

    // ------------------------------------------------------------------ budget prefix and edges

    [Fact]
    public void ReachablePrefix_IsInclusiveAtTheExactBoundary()
    {
        TraversalLattice lattice = Uniform(4);
        RouteQuery.Route r = RouteQuery.Find(lattice, new WorldState(1), 0, 3);
        Assert.Equal([0, 1, 2, 3], r.Nodes);
        Assert.Equal([0.0, 1.0, 2.0, 3.0], r.Cumulative);

        Assert.Equal(3, r.ReachablePrefixLength(2.0));                       // cost == budget: reached
        Assert.Equal(2, r.ReachablePrefixLength(Math.BitDecrement(2.0)));    // one ULP short: not
        Assert.Equal(1, r.ReachablePrefixLength(0.0));                       // the origin costs nothing
        Assert.Equal(4, r.ReachablePrefixLength(3.0));
        Assert.Equal(4, r.ReachablePrefixLength(double.PositiveInfinity));
        Assert.Equal(0, r.ReachablePrefixLength(-1.0));
        Assert.Equal(0, r.ReachablePrefixLength(double.NaN));
    }

    [Fact]
    public void NoRoute_TrivialRoute_AndNonPaths()
    {
        var costs = new double[16];
        var passable = new bool[16];
        Array.Fill(costs, 1.0);
        Array.Fill(passable, true);
        passable[5] = false;
        var lattice = TraversalLattice.FromCosts(4, costs, passable);
        var world = new WorldState(1);

        RouteQuery.Route none = RouteQuery.Find(lattice, world, 0, 5);      // impassable endpoint
        Assert.False(none.Found);
        Assert.Empty(none.Nodes);
        Assert.Empty(none.Cumulative);
        Assert.Equal(0.0, none.TotalCost);
        Assert.Equal(Pathfinder.FindPath(lattice, world, 0, 5).TotalCost, none.TotalCost);

        RouteQuery.Route self = RouteQuery.Find(lattice, world, 6, 6);
        Assert.Equal([6], self.Nodes);
        Assert.Equal([0.0], self.Cumulative);

        Assert.Empty(Pathfinder.LegCosts(lattice, world, []));
        Assert.Empty(Pathfinder.LegCosts(lattice, world, [6]));
        Assert.Throws<ArgumentException>(() => Pathfinder.LegCosts(lattice, world, [0, 2]));   // no edge
        Assert.Throws<ArgumentException>(() => Pathfinder.LegCosts(lattice, world, [0, 5]));   // impassable
        Assert.Throws<ArgumentException>(() => Pathfinder.LegCosts(lattice, world, [0, 16]));  // off the lattice
    }
}
