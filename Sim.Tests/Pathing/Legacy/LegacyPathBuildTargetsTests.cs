using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.PathBuild;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Pathing.Legacy;

/// <summary>
/// Batch 4 (D-055, T5, O1/O2; b4-design §5, review L3-10) — <see cref="PathBuildSystem.Targets"/> IS THE BATCH-3
/// PER-SETTLEMENT TARGET CHOICE. Batch 3 chose each settlement's build target with
/// <c>ChooseTarget(lattice, terrain, catchment, PassableComponent(lattice, frontier))</c>, recomputing every node's
/// block fertility and flooding the frontier's component once per settlement per turn. Targets computes the fertility
/// once per call (O1) and floods each component once per call (O2). <see cref="LegacyPathBuild"/> holds the batch-3
/// members verbatim; the targets must be equal entry for entry:
/// - on hand-built multi-component lattices (the SeamWorld strip FLAT is two islands, wrapped one landmass), with
///   frontiers in different components, repeated frontiers, an impassable frontier, a fully excluded component and a
///   block-constant fertility palette (exact fertility ties, so the (fertility DESC, id ASC) key decides);
/// - for EVERY settlement on EVERY turn 0..40 of the shipped founded worlds of seeds 42 (the bench seed) and 16 (the
///   seam seed), from each turn's state: frontier = the settlement's PathProgress frontier (its origin node before it
///   has one), catchment = its CatchmentNodes rows.
/// </summary>
public class LegacyPathBuildTargetsTests
{
    private const int Horizon = 40;

    /// <summary>The SeamWorld strip (64×64 lattice at stride 4) with a block-constant fertility palette.</summary>
    private static (TerrainSet Terrain, TraversalLattice Lattice) Strip(bool wrap)
    {
        TerrainSet t = SeamWorld.Terrain(wrapX: wrap);
        double[] fertility = new double[t.Size * t.Size];
        double[] palette = [0.2, 0.4, 0.6];
        for (int i = 0; i < fertility.Length; i++)
        {
            if (t.Water[i] >= 0.5) continue;
            int bx = i % t.Size / 4, by = i / t.Size / 4;
            fertility[i] = palette[(int)(SplitMix64.Mix((ulong)(by * 64 + bx) * 0x9E3779B97F4A7C15UL) % 3UL)];
        }
        TerrainSet varied = TerrainSet.FromLayers(t.Size, t.KmPerPx, t.SeaLevel, t.Elevation, t.Water, t.Temperature,
            t.Moisture, fertility, t.MovementCost, t.Rivers, [], wrapX: wrap);
        return (varied, TraversalLattice.Build(varied, TestConfigs.RiverCostFactor()));
    }

    private static int[] LegacyTargets(TraversalLattice lattice, TerrainSet terrain, int[] frontiers, bool[][] catchments)
    {
        var targets = new int[frontiers.Length];
        for (int i = 0; i < frontiers.Length; i++)
            targets[i] = LegacyPathBuild.ChooseTarget(lattice, terrain, catchments[i],
                LegacyPathBuild.PassableComponent(lattice, frontiers[i]));
        return targets;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Targets_IsThePerSettlementChoice_OnTheTwoIslandStrip_AndOnItsCylinder(bool wrap)
    {
        (TerrainSet terrain, TraversalLattice lattice) = Strip(wrap);
        int n = lattice.NodeCount;
        int west = lattice.NodeId(5, 20), east = lattice.NodeId(58, 30), west2 = lattice.NodeId(2, 40),
            ocean = lattice.NodeId(30, 30), east2 = lattice.NodeId(60, 17);
        Assert.True(lattice.IsPassable(west) && lattice.IsPassable(east) && lattice.IsPassable(west2) && lattice.IsPassable(east2));
        Assert.False(lattice.IsPassable(ocean));

        bool[] None() => new bool[n];
        bool[] Rows(int x0, int x1, int y0, int y1)
        {
            var c = new bool[n];
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) c[lattice.NodeId(x, y)] = true;
            return c;
        }
        // East first, then west: the first flood is the east island, so a west frontier must not reuse it.
        int[] frontiers = [east, west, west2, ocean, east2, east, west];
        bool[][] catchments =
        [
            None(),
            Rows(0, 12, 16, 30),        // the north half of the west island excluded
            Rows(0, 64, 0, 64),         // everything excluded: no eligible node, target −1
            None(),                     // impassable frontier: an empty component, target −1
            Rows(52, 64, 16, 32),       // the north half of the east island excluded
            None(),                     // a repeated frontier with the same catchment
            Rows(0, 12, 0, 64),         // the whole west island excluded (−1 flat; the east island on the cylinder)
        ];
        int[] old = LegacyTargets(lattice, terrain, frontiers, catchments);
        int[] now = PathBuildSystem.Targets(lattice, terrain, frontiers, catchments);
        Assert.Equal(old, now);

        // Anti-vacuity: the rig separates the components it claims to.
        int Col(int node) => lattice.Coords(node).X;
        Assert.True(old[0] >= 0 && old[1] >= 0, "a frontier on land has no target");
        Assert.Equal(-1, old[2]);
        Assert.Equal(-1, old[3]);
        Assert.Equal(old[0], old[5]);
        if (!wrap)
        {
            // Two islands: each frontier's target is on its own island, and with the west island excluded a west
            // frontier has nothing left — a reused EAST component would hand it an east target.
            Assert.True(Col(old[0]) >= 52, "flat: the east frontier's target is not on the east island");
            Assert.True(Col(old[1]) < 12, "flat: the west frontier's target is not on the west island");
            Assert.Equal(-1, old[6]);
        }
        else Assert.True(old[6] >= 0 && Col(old[6]) >= 52, "wrapped: one landmass, so the west frontier reaches the east island");

        // Tie-dense: the block-constant palette gives the winning fertility to many nodes of the component, and the
        // lowest id wins.
        double[] fertility = new double[n];
        for (int v = 0; v < n; v++) fertility[v] = LatticeMap.BlockMeanFertility(terrain, lattice, v);
        bool[] component = LegacyPathBuild.PassableComponent(lattice, east);
        int tied = 0;
        for (int v = 0; v < n; v++)
            if (v != old[0] && component[v] && fertility[v] == fertility[old[0]]) tied++;
        Assert.True(tied > 0, "the east frontier's target fertility is unique — the id tie-break is not exercised");

        // Empty input, and the shape check.
        Assert.Empty(PathBuildSystem.Targets(lattice, terrain, ReadOnlySpan<int>.Empty, Array.Empty<bool[]>()));
        Assert.Throws<ArgumentException>(() => PathBuildSystem.Targets(lattice, terrain, frontiers, catchments[..2]));
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(16UL)]
    public void Targets_IsThePerSettlementChoice_ForEverySettlement_OnEveryTurnOfTheFirstForty(ulong seed)
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldgenConfig wg = TestConfigs.Worldgen();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, wg)));
        WorldState w = WorldFounding.Found(wg, cfg, seed);
        TraversalLattice lattice = TraversalLattice.Build(w.Terrain!, cfg.Transport.RiverCostFactor);
        Assert.True(lattice.WrapX, "the shipped world is the cylinder");

        int compared = 0, withTarget = 0, advanced = 0, turnsWithCatchments = 0;
        var distinct = new SortedSet<int>();
        for (int turn = 0; turn <= Horizon; turn++)
        {
            if (turn > 0) w = executor.Step(w);
            int count = w.Settlements.Count;
            var frontiers = new int[count];
            var catchments = new bool[count][];
            for (int s = 0; s < count; s++)
            {
                SettlementRow settlement = w.Settlements[s];
                int origin = LatticeMap.OriginLatticeNode(lattice, w.Terrain!.Size, settlement.SiteCell);
                frontiers[s] = origin;
                for (int i = 0; i < w.PathProgress.Count; i++)
                    if (w.PathProgress[i].Settlement == settlement.Id && w.PathProgress[i].FrontierNode >= 0)
                        frontiers[s] = w.PathProgress[i].FrontierNode;
                if (frontiers[s] != origin) advanced++;
                var inCatchment = new bool[lattice.NodeCount];
                for (int i = 0; i < w.CatchmentNodes.Count; i++)
                    if (w.CatchmentNodes[i].Settlement == settlement.Id) inCatchment[w.CatchmentNodes[i].LatticeNode] = true;
                catchments[s] = inCatchment;
            }
            if (w.CatchmentNodes.Count > 0) turnsWithCatchments++;
            int[] old = LegacyTargets(lattice, w.Terrain!, frontiers, catchments);
            int[] now = PathBuildSystem.Targets(lattice, w.Terrain!, frontiers, catchments);
            for (int s = 0; s < count; s++)
            {
                if (old[s] != now[s])
                    Assert.Fail($"seed {seed} turn {turn} settlement {w.Settlements[s].Id.Value}: batch-3 target {old[s]}, Targets {now[s]} (frontier {frontiers[s]})");
                compared++;
                if (old[s] >= 0) { withTarget++; distinct.Add(old[s]); }
            }
        }
        Console.WriteLine($"seed {seed}: {compared} (settlement, turn) targets compared over turns 0..{Horizon}; "
            + $"{withTarget} with a target, {distinct.Count} distinct; {advanced} frontiers off their origin");
        Assert.True(withTarget > 0, "no settlement had a target");
        Assert.True(advanced > 0, "no frontier ever left its origin — PathBuild never built in the horizon");
        Assert.True(turnsWithCatchments > 0, "no turn had catchments to exclude");
    }
}
