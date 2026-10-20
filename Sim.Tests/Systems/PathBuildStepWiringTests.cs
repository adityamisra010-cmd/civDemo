using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.PathBuild;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// Batch 4 (D-055, T5, O1/O2; fix of review finding T5-L2-T5-V2-F1) — PATHBUILD'S STEP WIRING, observed through the
/// tables it writes. Batch 4 split the step into two passes: pass 1 banks every settlement's accrual and fixes its
/// frontier (settlement order) and records the builders; <see cref="PathBuildSystem.Targets"/> then chooses every
/// builder's target from builder k's OWN frontier and OWN Prev catchment; pass 2 lays the segments in settlement
/// order. LegacyPathBuildTargetsTests compares Targets with the batch-3 per-settlement choice, but passes its own
/// frontiers and catchments, so nothing there runs the step's index wiring. These tests pin the three semantics that
/// wiring carries, each where it is visible:
/// - each builder's target lies in ITS OWN frontier's passable component (the global best may sit across the sea and
///   must not stall the loop — PathBuildSystem's class doc): on the two-island seam world in both settlement orders, and
///   on the shipped canonical world of seed 9, whose settlements are founded in two passable components;
/// - each builder's target excludes ITS OWN catchment, not another builder's (a hand-set Prev catchment rig, one turn);
/// - the segments are laid in SETTLEMENT ORDER, so the first of two settlements on one route pays for the shared
///   segment and the second advances along it free, and the edge rows come in settlement order (one turn, exact).
/// </summary>
public class PathBuildStepWiringTests
{
    private const int Size = SeamWorld.DefaultSize;

    /// <summary>A 200-year turn: about one and a half straight ideal-ground segments of founding-sized build labour
    /// (the rigs below assert that premise exactly with a control step). 72,000 days, a divisor of the flat band.</summary>
    private const double RigDtYears = 200.0;

    private static int LatticeOf(IReadOnlyWorldState w, NetworkNodeId id)
    {
        for (int i = 0; i < w.NetworkNodes.Count; i++)
            if (w.NetworkNodes[i].Id == id) return w.NetworkNodes[i].LatticeNode;
        throw new InvalidOperationException($"no network node {id.Value}");
    }

    private static int OriginOf(IReadOnlyWorldState w, TraversalLattice lattice, SettlementId id)
    {
        for (int s = 0; s < w.Settlements.Count; s++)
            if (w.Settlements[s].Id == id) return LatticeMap.OriginLatticeNode(lattice, w.Terrain!.Size, w.Settlements[s].SiteCell);
        throw new InvalidOperationException($"no settlement {id.Value}");
    }

    private static PathProgressRow ProgressOf(IReadOnlyWorldState w, int settlement)
    {
        for (int i = 0; i < w.PathProgress.Count; i++)
            if (w.PathProgress[i].Settlement.Value == settlement) return w.PathProgress[i];
        throw new InvalidOperationException($"no path progress for settlement {settlement}");
    }

    /// <summary>The passable components that hold a settlement of <paramref name="w"/>, labelled 0, 1, … in settlement
    /// order of their first settlement; every other node is −1 (water, or land no settlement stands on). Terrain is
    /// immutable, so these are the components of the whole run.</summary>
    private static int[] Components(IReadOnlyWorldState w, TraversalLattice lattice, out int count)
    {
        var label = new int[lattice.NodeCount];
        Array.Fill(label, -1);
        count = 0;
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            int origin = OriginOf(w, lattice, w.Settlements[s].Id);
            if (label[origin] >= 0) continue;
            bool[] component = PathBuildSystem.PassableComponent(lattice, origin);
            for (int v = 0; v < component.Length; v++) if (component[v]) label[v] = count;
            count++;
        }
        return label;
    }

    /// <summary>
    /// After a run: every edge lies inside one settlement-holding component (no edge joins two, none lies on land no
    /// settlement stands on); every component that holds a builder (a settlement with a progress row) has at least one
    /// edge and at least one builder whose frontier left its origin; every frontier lies in its settlement's origin
    /// component. Returns the per-component edge counts.
    /// </summary>
    private static int[] AssertEachComponentBuildsItsOwnPaths(WorldState w, TraversalLattice lattice, string where)
    {
        int[] label = Components(w, lattice, out int count);
        var edgesIn = new int[count];
        for (int i = 0; i < w.NetworkEdges.Count; i++)
        {
            int a = label[LatticeOf(w, w.NetworkEdges[i].A)], b = label[LatticeOf(w, w.NetworkEdges[i].B)];
            Assert.True(a >= 0 && a == b, $"{where}: edge {i} joins component {a} to component {b}");
            edgesIn[a]++;
        }
        var builders = new int[count];
        var advanced = new int[count];
        for (int i = 0; i < w.PathProgress.Count; i++)
        {
            PathProgressRow p = w.PathProgress[i];
            int origin = OriginOf(w, lattice, p.Settlement);
            int c = label[origin];
            Assert.True(p.FrontierNode >= 0 && label[p.FrontierNode] == c,
                $"{where}: settlement {p.Settlement.Value}'s frontier {p.FrontierNode} is outside its origin's component {c}");
            builders[c]++;
            if (p.FrontierNode != origin) advanced[c]++;
        }
        for (int c = 0; c < count; c++)
        {
            if (builders[c] == 0) continue;
            Assert.True(edgesIn[c] > 0, $"{where}: component {c} holds {builders[c]} builders and no edge");
            Assert.True(advanced[c] > 0, $"{where}: component {c} holds {builders[c]} builders and no frontier left its origin");
        }
        return edgesIn;
    }

    // ------------------------------------------------------------ own component (the frontier wiring)

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnTwoIslands_EachSettlementBuildsOnItsOwnIsland_InEitherSettlementOrder(bool eastFirst)
    {
        SimConfig cfg = TestConfigs.Sim();
        int[] two = SeamWorld.TwoSides();
        WorldState w = SeamWorld.Found(false, eastFirst ? [two[1], two[0]] : two, sim: cfg);
        TraversalLattice lattice = TraversalLattice.Build(w.Terrain!, cfg.Transport.RiverCostFactor);
        int o0 = OriginOf(w, lattice, w.Settlements[0].Id), o1 = OriginOf(w, lattice, w.Settlements[1].Id);
        Assert.False(PathBuildSystem.PassableComponent(lattice, o0)[o1], "premise: the flat seam world is two islands");

        TurnExecutor executor = UniversityRigs.Production(cfg);
        for (int t = 0; t < 40; t++) w = executor.Step(w);

        int[] edgesIn = AssertEachComponentBuildsItsOwnPaths(w, lattice, $"eastFirst={eastFirst}");
        Assert.Equal(2, w.PathProgress.Count);
        for (int s = 0; s < 2; s++)
        {
            PathProgressRow p = ProgressOf(w, s);
            Assert.NotEqual(OriginOf(w, lattice, p.Settlement), p.FrontierNode);   // each island's own builder moved
        }
        Assert.True(edgesIn.Length >= 2 && edgesIn[0] > 0 && edgesIn[1] > 0, "an island got no path");
    }

    [Fact]
    public void ShippedSeed9_FoundsInTwoComponents_AndEveryComponentWithBuildersGetsItsOwnPaths()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 9);
        TraversalLattice lattice = TraversalLattice.Build(w.Terrain!, cfg.Transport.RiverCostFactor);
        Assert.True(lattice.WrapX, "the shipped world is the cylinder");
        int[] label = Components(w, lattice, out _);
        var held = new SortedSet<int>();
        for (int s = 0; s < w.Settlements.Count; s++) held.Add(label[OriginOf(w, lattice, w.Settlements[s].Id)]);
        Assert.True(held.Count > 1, "premise: seed 9's founding settlements span more than one passable component");

        TurnExecutor executor = UniversityRigs.Production(cfg);
        for (int t = 0; t < 40; t++) w = executor.Step(w);

        int[] edgesIn = AssertEachComponentBuildsItsOwnPaths(w, lattice, "seed 9");
        Console.WriteLine($"seed 9 after 40 turns: {w.NetworkEdges.Count} edges, per component [{string.Join(", ", edgesIn)}]");
    }

    // ------------------------------------------------------------ one-turn rigs (PathBuild alone, hand-set Prev catchments)

    /// <summary>The flat seam world (two islands) founded at <paramref name="sites"/>, with each settlement's Prev
    /// catchment set to <paramref name="catchments"/>[s] (lattice nodes).</summary>
    private static (WorldState World, TraversalLattice Lattice) Rig(SimConfig cfg, int[] sites, int[][] catchments)
    {
        WorldState w = SeamWorld.Found(false, sites, sim: cfg);
        for (int s = 0; s < catchments.Length; s++)
            foreach (int node in catchments[s])
                w.CatchmentNodes.Add(new CatchmentNodeRow(w.Settlements[s].Id, node, 0.0));
        return (w, TraversalLattice.Build(w.Terrain!, cfg.Transport.RiverCostFactor));
    }

    private static TurnExecutor PathBuildOnly(SimConfig cfg) =>
        UniversityRigs.Only(new OrderLog(), RigDtYears, SystemCatalog.PathBuild(cfg));

    private static int[] Nodes(TraversalLattice lattice, int x0, int x1, int y0, int y1)
    {
        var nodes = new List<int>();
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) nodes.Add(lattice.NodeId(x, y));
        return [.. nodes];
    }

    /// <summary>The bank one turn accrues for settlement <paramref name="s"/>: the same rig with every node in every
    /// catchment, so no settlement has a target and nothing is spent.</summary>
    private static double ControlBank(SimConfig cfg, int[] sites, int s)
    {
        WorldState probe = SeamWorld.Found(false, sites, sim: cfg);
        int n = TraversalLattice.Build(probe.Terrain!, cfg.Transport.RiverCostFactor).NodeCount;
        var everything = new int[n];
        for (int v = 0; v < n; v++) everything[v] = v;
        var all = new int[sites.Length][];
        for (int k = 0; k < sites.Length; k++) all[k] = everything;
        (WorldState w, _) = Rig(cfg, sites, all);
        WorldState next = PathBuildOnly(cfg).Step(w);
        Assert.Equal(0, next.NetworkEdges.Count);   // the control spends nothing
        return ProgressOf(next, s).Banked;
    }

    [Fact]
    public void EachBuilder_TargetsTheBestNodeOutsideItsOwnCatchment_NotTheFirstBuildersCatchment()
    {
        SimConfig cfg = TestConfigs.Sim();
        // Both settlements on the west island (lattice columns 0..11, rows 16..47); uniform ground and fertility, so every
        // target is the LOWEST-ID passable node outside the builder's catchment. Settlement 0 at node (5, 40); settlement 1
        // at (5, 24). Settlement 0's catchment: the west island's rows 16..27, which hold settlement 1's best node.
        // Settlement 1's catchment: row 16, columns 0..4 — so its own best node is (5, 16), straight north of it.
        int[] sites = [SeamWorld.Cell(20, 160), SeamWorld.Cell(20, 96)];
        WorldState probe = SeamWorld.Found(false, sites, sim: cfg);
        TraversalLattice lattice = TraversalLattice.Build(probe.Terrain!, cfg.Transport.RiverCostFactor);
        int[] c0 = Nodes(lattice, 0, 12, 16, 28), c1 = Nodes(lattice, 0, 5, 16, 17);
        (WorldState w, _) = Rig(cfg, sites, [c0, c1]);
        int o0 = OriginOf(w, lattice, w.Settlements[0].Id), o1 = OriginOf(w, lattice, w.Settlements[1].Id);
        Assert.Equal(lattice.NodeId(5, 40), o0);
        Assert.Equal(lattice.NodeId(5, 24), o1);

        // The semantic target, by the composite key (block fertility DESC, node id ASC) over the builder's component.
        int BestOutside(int frontier, int[] catchment)
        {
            bool[] component = PathBuildSystem.PassableComponent(lattice, frontier);
            var excluded = new bool[lattice.NodeCount];
            foreach (int v in catchment) excluded[v] = true;
            int best = -1;
            double bestFertility = double.NegativeInfinity;
            for (int v = 0; v < lattice.NodeCount; v++)
            {
                if (!component[v] || excluded[v] || !lattice.IsPassable(v)) continue;
                double f = LatticeMap.BlockMeanFertility(w.Terrain!, lattice, v);
                if (f > bestFertility) { bestFertility = f; best = v; }
            }
            return best;
        }
        int own = BestOutside(o1, c1), foreign = BestOutside(o1, c0);
        Assert.Equal(lattice.NodeId(5, 16), own);
        Assert.Equal(lattice.NodeId(0, 28), foreign);   // premise: builder 0's catchment would send builder 1 elsewhere

        // The only shortest route to (5, 16) is straight north; every shortest route to (0, 28) starts west or south-west.
        Pathfinder.PathResult route = Pathfinder.FindPath(lattice, w, o1, own);
        Assert.Equal(lattice.NodeId(5, 23), route.Nodes[1]);
        double c = lattice.StepCost(o1, route.Nodes[1]) * cfg.PathBuild.BuildCostMultiplier;
        double bank = ControlBank(cfg, sites, 1);
        Assert.True(bank >= c && bank < 2 * c, $"premise: one segment's bank, got {bank:R} for a {c:R} segment");

        WorldState next = PathBuildOnly(cfg).Step(w);

        // Settlement 1 laid exactly one segment, toward ITS OWN best node: (5, 24) → (5, 23), and paid for it.
        PathProgressRow p1 = ProgressOf(next, 1);
        Assert.Equal(lattice.NodeId(5, 23), p1.FrontierNode);
        Assert.Equal(BitConverter.DoubleToInt64Bits(bank - c), BitConverter.DoubleToInt64Bits(p1.Banked));
        bool laid = false;
        for (int i = 0; i < next.NetworkEdges.Count; i++)
        {
            int a = LatticeOf(next, next.NetworkEdges[i].A), b = LatticeOf(next, next.NetworkEdges[i].B);
            if (a == o1 || b == o1)
            {
                Assert.Equal((o1, lattice.NodeId(5, 23)), (a, b));
                laid = true;
            }
        }
        Assert.True(laid, "settlement 1 laid no segment from its origin");
    }

    [Fact]
    public void TwoSettlementsOnOneRoute_TheFirstInSettlementOrderPaysForTheSharedSegment_AndEdgesComeInSettlementOrder()
    {
        SimConfig cfg = TestConfigs.Sim();
        // Settlements 0 and 1 share one origin node, (5, 32) on the west island, and one catchment (row 16, columns 0..4),
        // so both target (5, 16) along the one shortest route, straight north. Settlement 2 is on the EAST island at
        // (59, 32), its catchment row 16 columns 52..58, so it targets (59, 16), also straight north. One turn, PathBuild
        // alone, a bank of one segment each.
        int[] sites = [SeamWorld.Cell(20, Size / 2), SeamWorld.Cell(21, Size / 2), SeamWorld.Cell(Size - 20, Size / 2)];
        WorldState probe = SeamWorld.Found(false, sites, sim: cfg);
        TraversalLattice lattice = TraversalLattice.Build(probe.Terrain!, cfg.Transport.RiverCostFactor);
        int[] west = Nodes(lattice, 0, 5, 16, 17), east = Nodes(lattice, 52, 59, 16, 17);
        (WorldState w, _) = Rig(cfg, sites, [west, west, east]);
        int o = OriginOf(w, lattice, w.Settlements[0].Id), o2 = OriginOf(w, lattice, w.Settlements[2].Id);
        Assert.Equal(lattice.NodeId(5, 32), o);
        Assert.Equal(o, OriginOf(w, lattice, w.Settlements[1].Id));
        Assert.Equal(lattice.NodeId(59, 32), o2);
        int r1 = lattice.NodeId(5, 31), r2 = lattice.NodeId(5, 30), q1 = lattice.NodeId(59, 31);
        double c = lattice.StepCost(o, r1) * cfg.PathBuild.BuildCostMultiplier;
        Assert.Equal(c, lattice.StepCost(r1, r2) * cfg.PathBuild.BuildCostMultiplier);
        Assert.Equal(c, lattice.StepCost(o2, q1) * cfg.PathBuild.BuildCostMultiplier);
        double bank0 = ControlBank(cfg, sites, 0), bank1 = ControlBank(cfg, sites, 1), bank2 = ControlBank(cfg, sites, 2);
        foreach (double bank in new[] { bank0, bank1, bank2 })
            Assert.True(bank >= c && bank < 2 * c, $"premise: one segment's bank, got {bank:R} for a {c:R} segment");

        WorldState next = PathBuildOnly(cfg).Step(w);

        // Settlement order: settlement 0 pays for (5,32)→(5,31); settlement 1 advances along it free and pays for
        // (5,31)→(5,30); settlement 2 pays for its own (59,32)→(59,31). The edge rows are appended in that order.
        Assert.Equal(r1, ProgressOf(next, 0).FrontierNode);
        Assert.Equal(r2, ProgressOf(next, 1).FrontierNode);
        Assert.Equal(q1, ProgressOf(next, 2).FrontierNode);
        Assert.Equal(BitConverter.DoubleToInt64Bits(bank0 - c), BitConverter.DoubleToInt64Bits(ProgressOf(next, 0).Banked));
        Assert.Equal(BitConverter.DoubleToInt64Bits(bank1 - c), BitConverter.DoubleToInt64Bits(ProgressOf(next, 1).Banked));
        Assert.Equal(BitConverter.DoubleToInt64Bits(bank2 - c), BitConverter.DoubleToInt64Bits(ProgressOf(next, 2).Banked));
        var edges = new List<(int, int)>();
        for (int i = 0; i < next.NetworkEdges.Count; i++)
        {
            Assert.Equal(i, next.NetworkEdges[i].Id.Value);
            edges.Add((LatticeOf(next, next.NetworkEdges[i].A), LatticeOf(next, next.NetworkEdges[i].B)));
        }
        Assert.Equal([(o, r1), (r1, r2), (o2, q1)], edges);
        Assert.Equal(3, next.NetworkMeta[0].Revision - w.NetworkMeta[0].Revision);
    }
}
