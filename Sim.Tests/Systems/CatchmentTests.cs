using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems.Catchment;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T1.4 acceptance (catchment): twin-deterministic and equal to a direct
// isochrone; a hand-added path edge + revision bump makes next turn's catchment
// strictly grow and effective farmland strictly increase (D-016 end-to-end); an
// unchanged revision provably skips recompute (observable); node count + recompute
// ms reported at 1024².
public class CatchmentTests
{
    private static WorldgenConfig Dev()
    {
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        return WorldgenConfigLoader.Load(stream) with { SizePx = 256 };
    }

    private static EraTable CanonicalEra()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(stream);
    }

    // A catchment-only pipeline: the other M0 systems are no-ops on a founded
    // world (no regions), so this isolates the behavior under test.
    private static TurnExecutor CatchmentExecutor() =>
        new(CanonicalEra(), [SystemCatalog.Catchment()]);

    private static int OriginOf(WorldState world, TraversalLattice lattice) =>
        CatchmentSystem.OriginLatticeNode(lattice, world.Terrain!.Size, world.Settlements[0].SiteCell);

    [Fact]
    public void Catchment_Twin_Deterministic_AndEqualsDirectIsochrone()
    {
        var cfg = Dev();
        TurnExecutor exec = CatchmentExecutor();

        WorldState a = exec.Step(WorldFounding.Found(cfg, seed: 42));
        WorldState b = exec.Step(WorldFounding.Found(cfg, seed: 42));

        // Twin-deterministic: identical derived tables.
        Assert.True(WorldStates.StateEquals(a, b));
        Assert.Equal(1, a.CatchmentSummaries.Count);

        // Equal to a DIRECT isochrone call from the origin lattice node.
        WorldState founded = WorldFounding.Found(cfg, seed: 42);
        TraversalLattice lattice = TraversalLattice.Build(founded.Terrain!);
        int origin = OriginOf(founded, lattice);
        Pathfinder.IsochroneResult iso =
            Pathfinder.Isochrone(lattice, founded, origin, CatchmentSystem.TravelBudget);

        // Node-for-node membership and cost, in the system's storage order.
        var nodesForSettlement = new List<CatchmentNodeRow>();
        for (int i = 0; i < a.CatchmentNodes.Count; i++)
            if (a.CatchmentNodes[i].Settlement.Value == 0) nodesForSettlement.Add(a.CatchmentNodes[i]);

        Assert.Equal(iso.Reached.Length, nodesForSettlement.Count);
        Assert.Equal(iso.Reached.Length, a.CatchmentSummaries[0].NodeCount);
        double expectedFarmland = 0.0;
        for (int i = 0; i < iso.Reached.Length; i++)
        {
            Assert.Equal(iso.Reached[i], nodesForSettlement[i].LatticeNode);
            Assert.Equal(iso.Costs[i], nodesForSettlement[i].TravelCost);
            expectedFarmland += CatchmentSystem.BlockFertility(founded.Terrain!, lattice, iso.Reached[i]);
        }
        // Farmland summed in the SAME ascending-node-id order — bit-exact.
        Assert.Equal(BitConverter.DoubleToInt64Bits(expectedFarmland),
            BitConverter.DoubleToInt64Bits(a.CatchmentSummaries[0].EffectiveFarmland));
    }

    [Fact]
    public void Catchment_RevisionBump_NextTurnStrictlyGrows_AndFarmlandIncreases()
    {
        var cfg = Dev();
        TurnExecutor exec = CatchmentExecutor();

        // Turn 1: baseline catchment (summaries empty ⇒ stale ⇒ recompute).
        WorldState w1 = exec.Step(WorldFounding.Found(cfg, seed: 42));
        int baselineNodes = w1.CatchmentSummaries[0].NodeCount;
        double baselineFarmland = w1.CatchmentSummaries[0].EffectiveFarmland;
        Assert.Equal(0, w1.CatchmentSummaries[0].NetworkRevision);

        TraversalLattice lattice = TraversalLattice.Build(w1.Terrain!);
        int origin = OriginOf(w1, lattice);

        // The baseline reached set — to pick a target OUTSIDE it.
        var reached = new HashSet<int>();
        for (int i = 0; i < w1.CatchmentNodes.Count; i++) reached.Add(w1.CatchmentNodes[i].LatticeNode);

        // A passable, positive-fertility node not yet in the catchment: the edge
        // will pull it (and its now-in-budget neighbors) in, growing the reach.
        int far = -1;
        for (int node = 0; node < lattice.NodeCount; node++)
        {
            if (!lattice.IsPassable(node) || reached.Contains(node) || node == origin) continue;
            if (CatchmentSystem.BlockFertility(w1.Terrain!, lattice, node) > 0.0) { far = node; break; }
        }
        Assert.True(far >= 0, "no passable positive-fertility node outside the baseline catchment");

        // Hand-add a fast lane origin↔far, cheap enough to fit the travel budget
        // (test writes state directly; PathBuild owns this from T1.6), and bump
        // the network revision — the ONLY thing that invalidates a catchment.
        w1.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(0), origin));
        w1.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(1), far));
        w1.NetworkEdges.Add(new NetworkEdgeRow(
            new NetworkEdgeId(0), new NetworkNodeId(0), new NetworkNodeId(1),
            EdgeTypes.DirtPath, Cost: CatchmentSystem.TravelBudget * 0.5));
        w1.NetworkMeta[0] = new NetworkMetaRow(Revision: 1);

        // Turn 2: reads Prev (revision 1, summaries at revision 0) ⇒ stale ⇒ recompute.
        WorldState w2 = exec.Step(w1);

        Assert.Equal(1, w2.CatchmentSummaries[0].NetworkRevision);
        Assert.True(w2.CatchmentSummaries[0].NodeCount > baselineNodes,
            $"catchment did not grow: {w2.CatchmentSummaries[0].NodeCount} <= {baselineNodes}");
        Assert.True(w2.CatchmentSummaries[0].EffectiveFarmland > baselineFarmland,
            $"farmland did not increase: {w2.CatchmentSummaries[0].EffectiveFarmland} <= {baselineFarmland}");
    }

    [Fact]
    public void Catchment_RevisionUnchanged_RecomputeProvablySkipped()
    {
        var cfg = Dev();
        TurnExecutor exec = CatchmentExecutor();

        // Turn 1 recomputes (LastRecomputeTurn := Prev.Clock.Turn == 0).
        WorldState world = exec.Step(WorldFounding.Found(cfg, seed: 42));
        long recomputeTurn = world.CatchmentSummaries[0].LastRecomputeTurn;
        Assert.Equal(0, recomputeTurn);
        int nodeCount = world.CatchmentSummaries[0].NodeCount;

        // Turns 2..6 change nothing (revision fixed) ⇒ recompute must be skipped.
        // LastRecomputeTurn is the observable: it would advance to the recomputing
        // turn if the system ran, so its staying at 0 proves the skip. The rows
        // are carried forward verbatim by the double-buffer clone.
        for (int t = 0; t < 5; t++)
        {
            world = exec.Step(world);
            Assert.Equal(0, world.CatchmentSummaries[0].LastRecomputeTurn);
            Assert.Equal(nodeCount, world.CatchmentSummaries[0].NodeCount);
        }
        Assert.True(world.Clock.Turn >= 6); // we really did advance turns while skipping
    }

    [Fact]
    public void Catchment_NodeCount_And_RecomputeMs_Reported_At1024()
    {
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        var cfg = WorldgenConfigLoader.Load(stream); // canonical 1024²
        WorldState founded = WorldFounding.Found(cfg, seed: 42);

        TraversalLattice lattice = TraversalLattice.Build(founded.Terrain!);
        int origin = OriginOf(founded, lattice);

        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        Pathfinder.IsochroneResult iso =
            Pathfinder.Isochrone(lattice, founded, origin, CatchmentSystem.TravelBudget);
        double recomputeMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0
                             / System.Diagnostics.Stopwatch.Frequency;

        // End-to-end turn (recompute inside the pipeline) as a cross-check.
        WorldState stepped = CatchmentExecutor().Step(founded);
        Assert.Equal(iso.Reached.Length, stepped.CatchmentSummaries[0].NodeCount);

        Assert.True(iso.Reached.Length > 0);
        Assert.True(recomputeMs < 2000, $"catchment recompute took {recomputeMs:F1} ms");
        Console.WriteLine($"catchment @ 1024²: {iso.Reached.Length} nodes, recompute {recomputeMs:F1} ms");
    }
}
