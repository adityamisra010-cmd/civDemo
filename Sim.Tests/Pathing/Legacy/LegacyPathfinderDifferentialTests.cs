using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Case = Sim.Tests.Pathing.Legacy.DifferentialLattices.Case;

namespace Sim.Tests.Pathing.Legacy;

/// <summary>
/// Batch 4 (D-055, T5; b4-design §5) — THE BYTE-IDENTITY PROOF OF THE PATHFINDING OPTIMISATIONS, function by function.
/// <see cref="LegacyPathfinder"/> is the batch-3 Pathfinder verbatim; every public entry point of the optimised
/// <see cref="Pathfinder"/> must return its answer BIT FOR BIT on the <see cref="DifferentialLattices"/> (tie-dense,
/// even and odd, flat and wrapped, with duplicate, self-anchored, seam-crossing and travelled-road lanes):
/// - FindPath (O3 CSR overlay + O4 inline expansion): Found, the whole node sequence, and TotalCost's bits;
/// - Isochrone and Partition (O3 + O6): Reached, Costs bits and Boundary; Owner and Cost bits;
/// - RelaxCappedFrom (O6): the relaxed array's bits;
/// - LegCosts (O3): every leg's bits, and the same refusals;
/// - DistancesTo (O5): the full batch-3 isochrone's cost at each target (+∞ where it does not reach), duplicates,
///   unreachable and impassable targets included;
/// - BuildOverlay (O3): the CSR slice of every node equals the batch-3 jagged lane list, in order;
/// - StepCostKnown (O4/O6): StepCost's bits on every node and direction, seam steps included.
/// Comparisons are exact (DoubleToInt64Bits), never within a tolerance: the existing A*-equals-Dijkstra tests compare
/// costs to 1e-12, which is necessary but not sufficient for an unchanged world hash.
/// </summary>
public class LegacyPathfinderDifferentialTests
{
    private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

    private static bool InStride(int i, int j, int stride) => stride <= 1 || ((j - i) % stride + stride) % stride == 0;

    private static bool Adjacent(TraversalLattice lattice, int a, int b)
    {
        (int ax, int ay) = lattice.Coords(a);
        (int bx, int by) = lattice.Coords(b);
        return a != b && MapTopology.AbsDx(ax, bx, lattice.Size, lattice.WrapX) <= 1 && Math.Abs(ay - by) <= 1;
    }

    private static int FirstImpassable(TraversalLattice lattice)
    {
        for (int i = 0; i < lattice.NodeCount; i++) if (!lattice.IsPassable(i)) return i;
        return -1;
    }

    // ------------------------------------------------------------------ FindPath (O3 + O4)

    [Theory]
    [MemberData(nameof(DifferentialLattices.Theory), MemberType = typeof(DifferentialLattices))]
    public void FindPath_IsTheBatch3AStar_BitForBit_OnEveryPair(string name)
    {
        Case c = DifferentialLattices.Get(name);
        TraversalLattice lattice = c.Lattice;
        int[] passable = DifferentialLattices.PassableNodes(lattice);
        int pairs = 0, found = 0, seamPaths = 0, laneLegs = 0, tieWitnesses = 0;
        for (int i = 0; i < passable.Length; i++)
            for (int j = 0; j < passable.Length; j++)
            {
                if (!InStride(i, j, c.TargetStride)) continue;
                int from = passable[i], to = passable[j];
                LegacyPathfinder.PathResult old = LegacyPathfinder.FindPath(lattice, c.World, from, to);
                Pathfinder.PathResult now = Pathfinder.FindPath(lattice, c.World, from, to);
                if (old.Found != now.Found || Bits(old.TotalCost) != Bits(now.TotalCost)
                    || !old.Nodes.AsSpan().SequenceEqual(now.Nodes))
                    Assert.Fail($"{name}: FindPath {from}->{to} differs: batch 3 found={old.Found} cost={old.TotalCost:R} "
                        + $"[{string.Join(",", old.Nodes)}]; now found={now.Found} cost={now.TotalCost:R} [{string.Join(",", now.Nodes)}]");
                pairs++;
                if (!old.Found) continue;
                found++;
                bool seam = false;
                for (int k = 1; k < old.Nodes.Length; k++)
                {
                    int dx = Math.Abs(lattice.Coords(old.Nodes[k]).X - lattice.Coords(old.Nodes[k - 1]).X);
                    if (!Adjacent(lattice, old.Nodes[k - 1], old.Nodes[k])) laneLegs++;
                    else if (dx > 1) seam = true;
                }
                if (seam) seamPaths++;
                // A tie witness: the reverse search costs the same bits but takes another node set — the (f, id) key, not
                // the geometry, chose between equal routes. The batch-3 tie resolution is what these pairs pin.
                if (j < i)
                {
                    LegacyPathfinder.PathResult back = LegacyPathfinder.FindPath(lattice, c.World, to, from);
                    if (back.Found && Bits(back.TotalCost) == Bits(old.TotalCost) && back.Nodes.Length == old.Nodes.Length)
                    {
                        bool same = true;
                        for (int k = 0; k < old.Nodes.Length; k++)
                            if (old.Nodes[k] != back.Nodes[old.Nodes.Length - 1 - k]) { same = false; break; }
                        if (!same) tieWitnesses++;
                    }
                }
            }

        // Impassable endpoints: both refuse identically.
        int water = FirstImpassable(lattice);
        if (water >= 0)
            foreach ((int from, int to) in new[] { (passable[0], water), (water, passable[0]), (water, water) })
            {
                LegacyPathfinder.PathResult old = LegacyPathfinder.FindPath(lattice, c.World, from, to);
                Pathfinder.PathResult now = Pathfinder.FindPath(lattice, c.World, from, to);
                Assert.False(old.Found);
                Assert.False(now.Found);
                Assert.Empty(now.Nodes);
                Assert.Equal(Bits(old.TotalCost), Bits(now.TotalCost));
            }

        Console.WriteLine($"{name}: {pairs} pairs, {found} found, {seamPaths} cross the seam, {laneLegs} lane legs, {tieWitnesses} tie witnesses");
        // Anti-vacuity: the rig exercises what it claims.
        Assert.True(found > 0, $"{name}: no pair connected");
        if (lattice.WrapX)
            Assert.True(seamPaths > 0, $"{name}: no batch-3 path crosses the seam — the rig does not exercise it");
        if (name.Contains("lanes", StringComparison.Ordinal))
            Assert.True(laneLegs > 0, $"{name}: no path takes a lane — the lane half is vacuous");
        if (name.StartsWith("tie", StringComparison.Ordinal) || name == "seam" || name.StartsWith("walled", StringComparison.Ordinal))
            Assert.True(tieWitnesses > 0, $"{name}: no equal-cost alternative route was resolved by the tie-break — not tie-dense");
    }

    // ------------------------------------------------------------------ Isochrone (O3 + O6)

    [Theory]
    [MemberData(nameof(DifferentialLattices.Theory), MemberType = typeof(DifferentialLattices))]
    public void Isochrone_IsTheBatch3Isochrone_BitForBit(string name)
    {
        Case c = DifferentialLattices.Get(name);
        double[] budgets = [0.0, 1.0, 2.0, 2.5, 6.0, double.MaxValue, double.PositiveInfinity];
        int[] passable = DifferentialLattices.PassableNodes(c.Lattice);
        var origins = new List<int>();
        for (int i = 0; i < passable.Length; i += c.TargetStride) origins.Add(passable[i]);
        int water = FirstImpassable(c.Lattice);
        if (water >= 0) origins.Add(water);
        int boundaries = 0;
        foreach (int origin in origins)
            foreach (double budget in budgets)
            {
                LegacyPathfinder.IsochroneResult old = LegacyPathfinder.Isochrone(c.Lattice, c.World, origin, budget);
                Pathfinder.IsochroneResult now = Pathfinder.Isochrone(c.Lattice, c.World, origin, budget);
                Assert.True(old.Reached.AsSpan().SequenceEqual(now.Reached), $"{name}: Reached differs from {origin} at {budget:R}");
                Assert.True(old.Boundary.AsSpan().SequenceEqual(now.Boundary), $"{name}: Boundary differs from {origin} at {budget:R}");
                Assert.Equal(old.Costs.Length, now.Costs.Length);
                for (int k = 0; k < old.Costs.Length; k++)
                    if (Bits(old.Costs[k]) != Bits(now.Costs[k]))
                        Assert.Fail($"{name}: Isochrone cost of {old.Reached[k]} from {origin} at {budget:R}: {old.Costs[k]:R} != {now.Costs[k]:R}");
                boundaries += now.Boundary.Length;
            }
        Assert.True(boundaries > 0, $"{name}: no isochrone has a boundary — every budget flooded everything");
    }

    // ------------------------------------------------------------------ Partition (O3 + O6)

    [Theory]
    [MemberData(nameof(DifferentialLattices.Theory), MemberType = typeof(DifferentialLattices))]
    public void Partition_IsTheBatch3Partition_BitForBit(string name)
    {
        Case c = DifferentialLattices.Get(name);
        int[] passable = DifferentialLattices.PassableNodes(c.Lattice);
        int water = FirstImpassable(c.Lattice);
        int P(int k) => passable[(int)(Sim.Core.Kernel.SplitMix64.Mix((ulong)k * 31UL + 7UL) % (ulong)passable.Length)];
        int[][] originSets =
        [
            [P(1), P(2), P(3), P(4)],
            [P(5), P(6), P(5), P(7)],                              // two sources on one node (the index tie-break)
            water >= 0 ? [P(8), water, P(9)] : [P(8), P(9)],      // an impassable source claims nothing
            [P(10)],
        ];
        int contested = 0;
        foreach (int[] origins in originSets)
        {
            foreach (double budget in new[] { 2.0, 5.0, double.MaxValue })
            {
                LegacyPathfinder.PartitionResult old = LegacyPathfinder.Partition(c.Lattice, c.World, origins, budget);
                Pathfinder.PartitionResult now = Pathfinder.Partition(c.Lattice, c.World, origins, budget);
                AssertPartition(name, old, now);
                for (int i = 0; i < old.Owner.Length; i++) if (old.Owner[i] > 0) contested++;
            }
            // T3.8 per-source budgets (unequal).
            double[] unequal = new double[origins.Length];
            for (int s = 0; s < unequal.Length; s++) unequal[s] = 1.5 + 2.25 * s;
            AssertPartition(name, LegacyPathfinder.Partition(c.Lattice, c.World, origins, unequal),
                Pathfinder.Partition(c.Lattice, c.World, origins, unequal));
        }
        Assert.True(contested > 0, $"{name}: no node is owned by a source other than the first — the partition is vacuous");
    }

    private static void AssertPartition(string name, LegacyPathfinder.PartitionResult old, Pathfinder.PartitionResult now)
    {
        Assert.True(old.Owner.AsSpan().SequenceEqual(now.Owner), $"{name}: Partition owners differ");
        Assert.Equal(old.Cost.Length, now.Cost.Length);
        for (int i = 0; i < old.Cost.Length; i++)
            if (Bits(old.Cost[i]) != Bits(now.Cost[i]))
                Assert.Fail($"{name}: Partition cost of node {i}: {old.Cost[i]:R} != {now.Cost[i]:R}");
    }

    // ------------------------------------------------------------------ RelaxCappedFrom (O6)

    [Theory]
    [MemberData(nameof(DifferentialLattices.Theory), MemberType = typeof(DifferentialLattices))]
    public void RelaxCappedFrom_IsTheBatch3Relax_BitForBit_AccumulatingOverOrigins(string name)
    {
        Case c = DifferentialLattices.Get(name);
        int n = c.Lattice.NodeCount;
        int[] passable = DifferentialLattices.PassableNodes(c.Lattice);
        int touched = 0;
        foreach (double cap in new[] { 0.0, 2.0, 5.5, double.MaxValue })
        {
            var old = new double[n];
            var now = new double[n];
            Array.Fill(old, double.PositiveInfinity);
            Array.Fill(now, double.PositiveInfinity);
            for (int i = 0; i < passable.Length; i += c.TargetStride)
            {
                LegacyPathfinder.RelaxCappedFrom(c.Lattice, passable[i], cap, old);
                Pathfinder.RelaxCappedFrom(c.Lattice, passable[i], cap, now);
                for (int k = 0; k < n; k++)
                {
                    if (Bits(old[k]) != Bits(now[k]))
                        Assert.Fail($"{name}: RelaxCappedFrom({passable[i]}, cap {cap:R}) node {k}: {old[k]:R} != {now[k]:R}");
                    // A node held at a cost strictly between 0 and +∞ at this point was relaxed past an origin. (Once
                    // every passable node has been an origin itself, the accumulated minimum is 0 everywhere.)
                    if (now[k] > 0.0 && !double.IsPositiveInfinity(now[k])) touched++;
                }
            }
        }
        Assert.True(touched > 0, $"{name}: no node was relaxed past its origin");
    }

    // ------------------------------------------------------------------ LegCosts (O3)

    [Theory]
    [MemberData(nameof(DifferentialLattices.Theory), MemberType = typeof(DifferentialLattices))]
    public void LegCosts_IsTheBatch3LegCosts_BitForBit_AndRefusesIdentically(string name)
    {
        Case c = DifferentialLattices.Get(name);
        TraversalLattice lattice = c.Lattice;
        int[] passable = DifferentialLattices.PassableNodes(lattice);
        (int[][] laneTargets, _, _) = LegacyPathfinder.BuildOverlay(lattice, c.World);
        int legs = 0, laneLegs = 0;

        // Every lattice step and every lane of every passable node, as a two-node path.
        int[] dxs = [-1, 1, 0, 0, -1, 1, -1, 1];
        int[] dys = [0, 0, -1, 1, -1, -1, 1, 1];
        foreach (int a in passable)
        {
            (int x, int y) = lattice.Coords(a);
            for (int d = 0; d < 8; d++)
            {
                int nx = MapTopology.NeighbourX(x, dxs[d], lattice.Size, lattice.WrapX), ny = y + dys[d];
                if (nx < 0 || ny < 0 || ny >= lattice.Size) continue;
                int b = lattice.NodeId(nx, ny);
                if (!lattice.IsPassable(b)) continue;
                AssertLegs(name, lattice, c.World, [a, b]);
                legs++;
            }
            foreach (int t in laneTargets[a])
            {
                if (!lattice.IsPassable(t)) continue;
                AssertLegs(name, lattice, c.World, [a, t]);
                laneLegs++;
            }
        }
        // The legs of batch-3 FindPath routes (every 5th pair on the strided sweep).
        int routes = 0;
        for (int i = 0; i < passable.Length; i += 1)
            for (int j = i % 5; j < passable.Length; j += 5 * c.TargetStride)
            {
                LegacyPathfinder.PathResult r = LegacyPathfinder.FindPath(lattice, c.World, passable[i], passable[j]);
                if (!r.Found) continue;
                AssertLegs(name, lattice, c.World, r.Nodes);
                routes++;
            }
        // The trivial sequences.
        AssertLegs(name, lattice, c.World, []);
        AssertLegs(name, lattice, c.World, [passable[0]]);
        // The refusals: an impassable node, an out-of-range node, and two nodes no step or lane joins.
        int water = FirstImpassable(lattice);
        if (water >= 0) AssertSameRefusal(lattice, c.World, [passable[0], water]);
        AssertSameRefusal(lattice, c.World, [passable[0], lattice.NodeCount]);
        foreach (int far in passable)
        {
            if (Adjacent(lattice, passable[0], far) || far == passable[0]) continue;
            bool laned = false;
            foreach (int t in laneTargets[passable[0]]) if (t == far) laned = true;
            if (laned) continue;
            AssertSameRefusal(lattice, c.World, [passable[0], far]);
            break;
        }
        Assert.True(legs > 0 && routes > 0, $"{name}: {legs} step legs, {routes} routes");
        if (name.Contains("lanes", StringComparison.Ordinal))
            Assert.True(laneLegs > 0, $"{name}: no lane leg was compared");
    }

    private static void AssertLegs(string name, TraversalLattice lattice, IReadOnlyWorldState world, int[] nodes)
    {
        double[] old = LegacyPathfinder.LegCosts(lattice, world, nodes);
        double[] now = Pathfinder.LegCosts(lattice, world, nodes);
        Assert.Equal(old.Length, now.Length);
        for (int k = 0; k < old.Length; k++)
            if (Bits(old[k]) != Bits(now[k]))
                Assert.Fail($"{name}: LegCosts leg {k} of [{string.Join(",", nodes)}]: {old[k]:R} != {now[k]:R}");
    }

    private static void AssertSameRefusal(TraversalLattice lattice, IReadOnlyWorldState world, int[] nodes)
    {
        ArgumentException old = Assert.ThrowsAny<ArgumentException>(() => LegacyPathfinder.LegCosts(lattice, world, nodes));
        ArgumentException now = Assert.ThrowsAny<ArgumentException>(() => Pathfinder.LegCosts(lattice, world, nodes));
        Assert.Equal(old.GetType(), now.GetType());
        Assert.Equal(old.Message, now.Message);
    }

    // ------------------------------------------------------------------ DistancesTo (O5)

    [Theory]
    [MemberData(nameof(DifferentialLattices.Theory), MemberType = typeof(DifferentialLattices))]
    public void DistancesTo_IsTheBatch3FullIsochroneAtItsTargets_BitForBit(string name)
    {
        Case c = DifferentialLattices.Get(name);
        TraversalLattice lattice = c.Lattice;
        int n = lattice.NodeCount;
        int[] passable = DifferentialLattices.PassableNodes(lattice);
        int water = FirstImpassable(lattice);
        var everyNode = new int[n];
        for (int i = 0; i < n; i++) everyNode[i] = i;
        int infinite = 0, finite = 0, subsets = 0;

        var origins = new List<int>();
        for (int i = 0; i < passable.Length; i += c.TargetStride) origins.Add(passable[i]);
        if (water >= 0) origins.Add(water);   // an impassable origin reaches nothing: every target +∞
        foreach (int origin in origins)
        {
            // The batch-3 full isochrone, read the way CatchmentSystem read it: +∞ where it did not reach.
            LegacyPathfinder.IsochroneResult full = LegacyPathfinder.Isochrone(lattice, c.World, origin, double.MaxValue);
            var field = new double[n];
            Array.Fill(field, double.PositiveInfinity);
            for (int i = 0; i < full.Reached.Length; i++) field[full.Reached[i]] = full.Costs[i];

            // Every node at once (passable, impassable, unreachable): the search runs to exhaustion.
            double[] all = Pathfinder.DistancesTo(lattice, c.World, origin, everyNode);
            for (int t = 0; t < n; t++)
            {
                if (Bits(all[t]) != Bits(field[t]))
                    Assert.Fail($"{name}: DistancesTo({origin}) at {t}: {all[t]:R} != batch-3 {field[t]:R}");
                if (double.IsPositiveInfinity(all[t])) infinite++; else finite++;
            }

            // Small target sets (the catchment's shape: a few other origins), with duplicates and the origin itself —
            // the early exit fires before the flood is complete.
            for (int k = 0; k < 6; k++)
            {
                ulong h = Sim.Core.Kernel.SplitMix64.Mix((ulong)origin * 131UL + (ulong)k);
                int size = 1 + (int)(h % 5UL);
                var targets = new int[size + (k == 3 ? 2 : 0)];
                for (int m = 0; m < size; m++)
                    targets[m] = passable[(int)(Sim.Core.Kernel.SplitMix64.Mix(h + (ulong)m) % (ulong)passable.Length)];
                if (k == 3) { targets[size] = targets[0]; targets[size + 1] = origin; }   // a duplicate and the origin
                if (k == 4 && water >= 0) targets[0] = water;                           // an impassable target
                double[] got = Pathfinder.DistancesTo(lattice, c.World, origin, targets);
                Assert.Equal(targets.Length, got.Length);
                for (int m = 0; m < targets.Length; m++)
                    if (Bits(got[m]) != Bits(field[targets[m]]))
                        Assert.Fail($"{name}: DistancesTo({origin}, [{string.Join(",", targets)}])[{m}]: {got[m]:R} != batch-3 {field[targets[m]]:R}");
                subsets++;
            }
        }
        // No target: nothing to search.
        Assert.Empty(Pathfinder.DistancesTo(lattice, c.World, passable[0], ReadOnlySpan<int>.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => Pathfinder.DistancesTo(lattice, c.World, passable[0], new[] { n }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Pathfinder.DistancesTo(lattice, c.World, passable[0], new[] { -1 }));
        Assert.True(finite > 0 && subsets > 0, $"{name}: {finite} finite, {subsets} subsets");
        Assert.True(infinite > 0, $"{name}: no unreached target — the +∞ branch is not exercised");
    }

    // ------------------------------------------------------------------ the CSR overlay (O3)

    [Theory]
    [MemberData(nameof(DifferentialLattices.Theory), MemberType = typeof(DifferentialLattices))]
    public void Overlay_TheCsrSliceOfEveryNode_IsTheBatch3JaggedLaneList_InOrder(string name)
    {
        Case c = DifferentialLattices.Get(name);
        AssertOverlay(name, c.Lattice, c.World);
    }

    [Fact]
    public void Overlay_OnALaneWorld_HasLaneListsWhoseOrderIsVisible_AndAnEmptyWorldHasNone()
    {
        // The lane order is a CONTRACT (the class remarks: "the network overlay is built in edge-table row order"), so
        // the rig must contain a node whose lanes, reversed, are a DIFFERENT list — else a reversed fill would pass.
        Case c = DifferentialLattices.Get("lanes12-wrap");
        Pathfinder.LaneOverlay o = Pathfinder.BuildOverlay(c.Lattice, c.World);
        int visible = 0;
        for (int v = 0; v < c.Lattice.NodeCount; v++)
        {
            int lo = o.Offsets[v], hi = o.Offsets[v + 1];
            for (int k = 0; k < (hi - lo) / 2; k++)
            {
                int a = lo + k, b = hi - 1 - k;
                if (o.Targets[a] != o.Targets[b] || Bits(o.Costs[a]) != Bits(o.Costs[b])) { visible++; break; }
            }
        }
        Assert.True(visible > 0, "no node's lane list differs from its reverse — the order is not observable");

        TraversalLattice bare = DifferentialLattices.RandomTie(12, wrap: true, seed: 3);
        Pathfinder.LaneOverlay none = Pathfinder.BuildOverlay(bare, new WorldState(1));
        Assert.Equal(bare.NodeCount + 1, none.Offsets.Length);
        Assert.All(none.Offsets, x => Assert.Equal(0, x));
        Assert.Empty(none.Targets);
        Assert.Empty(none.Costs);
        Assert.Equal(double.MaxValue, none.MinEdgePerUnit);
        AssertOverlay("bare", bare, new WorldState(1));
    }

    private static void AssertOverlay(string name, TraversalLattice lattice, IReadOnlyWorldState world)
    {
        (int[][] oldTargets, double[][] oldCosts, double oldMin) = LegacyPathfinder.BuildOverlay(lattice, world);
        Pathfinder.LaneOverlay now = Pathfinder.BuildOverlay(lattice, world);
        int n = lattice.NodeCount;
        Assert.Equal(n + 1, now.Offsets.Length);
        Assert.Equal(0, now.Offsets[0]);
        Assert.Equal(now.Targets.Length, now.Offsets[n]);
        Assert.Equal(now.Targets.Length, now.Costs.Length);
        Assert.Equal(Bits(oldMin), Bits(now.MinEdgePerUnit));
        int lanes = 0;
        for (int v = 0; v < n; v++)
        {
            int lo = now.Offsets[v], hi = now.Offsets[v + 1];
            Assert.True(hi >= lo, $"{name}: offsets decrease at {v}");
            Assert.True(oldTargets[v].Length == hi - lo, $"{name}: node {v} has {oldTargets[v].Length} lanes in batch 3, {hi - lo} now");
            for (int k = 0; k < oldTargets[v].Length; k++)
            {
                if (oldTargets[v][k] != now.Targets[lo + k] || Bits(oldCosts[v][k]) != Bits(now.Costs[lo + k]))
                    Assert.Fail($"{name}: node {v} lane {k}: batch 3 ({oldTargets[v][k]}, {oldCosts[v][k]:R}), now ({now.Targets[lo + k]}, {now.Costs[lo + k]:R})");
            }
            lanes += hi - lo;
        }
        Assert.Equal(lanes, now.Targets.Length);
    }

    // ------------------------------------------------------------------ StepCostKnown (O4 / O6)

    public static IEnumerable<object[]> StepLattices()
    {
        foreach (string name in DifferentialLattices.Names) yield return [name];
        yield return ["canon42"];   // the shipped 1024-px cylinder of seed 42 (the bench world): 65,536 nodes
    }

    [Theory]
    [MemberData(nameof(StepLattices))]
    public void StepCostKnown_IsStepCost_OnEveryNodeAndDirection_SeamStepsIncluded(string name)
    {
        TraversalLattice lattice = name == "canon42"
            ? TraversalLattice.Build(Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.Worldgen(), 42), TestConfigs.RiverCostFactor())
            : DifferentialLattices.Get(name).Lattice;
        // The pathfinder's fixed offsets: W, E, N, S, NW, NE, SW, SE — diagonal iff d ≥ 4.
        int[] dxs = [-1, 1, 0, 0, -1, 1, -1, 1];
        int[] dys = [0, 0, -1, 1, -1, -1, 1, 1];
        int steps = 0, seamSteps = 0, flagMatters = 0;
        for (int a = 0; a < lattice.NodeCount; a++)
        {
            (int x, int y) = lattice.Coords(a);
            for (int d = 0; d < 8; d++)
            {
                int nx = MapTopology.NeighbourX(x, dxs[d], lattice.Size, lattice.WrapX), ny = y + dys[d];
                if (nx < 0 || ny < 0 || ny >= lattice.Size) continue;
                int b = lattice.NodeId(nx, ny);
                double expected = lattice.StepCost(a, b);
                double known = lattice.StepCostKnown(a, b, d >= 4);
                if (Bits(expected) != Bits(known))
                    Assert.Fail($"{name}: StepCostKnown({a}, {b}, {d >= 4}) = {known:R} != StepCost {expected:R} (d = {d})");
                steps++;
                if (Math.Abs(nx - x) > 1) seamSteps++;
                if (Bits(lattice.StepCostKnown(a, b, d < 4)) != Bits(expected)) flagMatters++;
            }
        }
        Assert.True(steps > 0);
        Assert.True(flagMatters > 0, $"{name}: the diagonal flag never changes the cost — the comparison is vacuous");
        if (lattice.WrapX) Assert.True(seamSteps > 0, $"{name}: no seam step was compared");
        else Assert.Equal(0, seamSteps);
    }
}
