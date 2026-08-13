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
        return WorldgenConfigLoader.Load(stream) is { } c
            ? c with { SizePx = 256, Siting = c.Siting with { SettlementCount = 4 } } // D-025 dev preset
            : throw new InvalidOperationException();
    }

    private static EraTable CanonicalEra()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(stream);
    }

    // A catchment-only pipeline: the other M0 systems are no-ops on a founded
    // world (no regions), so this isolates the behavior under test.
    private static TurnExecutor CatchmentExecutor() =>
        new(CanonicalEra(), [SystemCatalog.Catchment(TestUtil.TestConfigs.Sim())]);

    /// <summary>T3.2b: the catchment budget in cost units, from the TUNE
    /// hinterland radius through the one conversion the system itself uses —
    /// a test that recomputed km→cost by hand would agree with a wrong
    /// implementation.</summary>
    private static double Budget(TraversalLattice lattice) =>
        CatchmentSystem.TravelBudgetCostUnits(TestUtil.TestConfigs.Sim(), lattice);

    private static int OriginOf(WorldState world, TraversalLattice lattice) =>
        CatchmentSystem.OriginLatticeNode(lattice, world.Terrain!.Size, world.Settlements[0].SiteCell);

    /// <summary>T3.8: the settlement's ACTUAL budget — its founding housing
    /// row through the system's own pure SizeTier/TierBudget, so direct-call
    /// witnesses compare against what the system really runs.</summary>
    private static double TierBudgetOf(WorldState world, TraversalLattice lattice, int settlementIndex)
    {
        var sim = TestUtil.TestConfigs.Sim();
        int tier = 0;
        for (int h = 0; h < world.Housing.Count; h++)
            if (world.Housing[h].Settlement == world.Settlements[settlementIndex].Id)
            {
                tier = CatchmentSystem.SizeTier(
                    world.Housing[h].Dwellings.Value, sim.Catchment.SizeDwellingsRef);
                break;
            }
        return CatchmentSystem.TierBudget(sim, lattice, tier);
    }

    [Fact]
    public void Catchment_Twin_Deterministic_AndEqualsDirectIsochrone()
    {
        // N = 1 via the D-029 flag: with a single source the partition and a
        // direct isochrone must coincide EXACTLY (same Dijkstra, same claims)
        // — the continuity proof that T2.3 changed the mechanism, not the
        // single-settlement semantics. Multi-source behavior gets its own
        // partition tests (PluralWorldTests).
        var cfg = Dev();
        TurnExecutor exec = CatchmentExecutor();

        WorldState a = exec.Step(WorldFounding.Found(cfg, TestUtil.TestConfigs.Sim(), seed: 42, settlementsOverride: 1));
        WorldState b = exec.Step(WorldFounding.Found(cfg, TestUtil.TestConfigs.Sim(), seed: 42, settlementsOverride: 1));

        // Twin-deterministic: identical derived tables.
        Assert.True(WorldStates.StateEquals(a, b));
        Assert.Equal(1, a.CatchmentSummaries.Count);

        // Equal to a DIRECT isochrone call from the origin lattice node — at
        // the settlement's ACTUAL budget (T3.8: founding seeds dwellings, so
        // the system runs the TIER budget, derived here through the same pure
        // functions the system uses).
        WorldState founded = WorldFounding.Found(cfg, TestUtil.TestConfigs.Sim(), seed: 42, settlementsOverride: 1);
        TraversalLattice lattice = TraversalLattice.Build(founded.Terrain!);
        int origin = OriginOf(founded, lattice);
        Pathfinder.IsochroneResult iso =
            Pathfinder.Isochrone(lattice, founded, origin, TierBudgetOf(founded, lattice, 0));

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
            expectedFarmland += CatchmentSystem.BlockArableKm2(founded.Terrain!, lattice, iso.Reached[i]);
        }
        // Farmland summed in the SAME ascending-node-id order — bit-exact.
        Assert.Equal(BitConverter.DoubleToInt64Bits(expectedFarmland),
            BitConverter.DoubleToInt64Bits(a.CatchmentSummaries[0].EffectiveArableKm2));
    }

    [Fact]
    public void Catchment_RevisionBump_NextTurnStrictlyGrows_AndFarmlandIncreases()
    {
        var cfg = Dev();
        TurnExecutor exec = CatchmentExecutor();

        // Turn 1: baseline catchment (summaries empty ⇒ stale ⇒ recompute).
        WorldState w1 = exec.Step(WorldFounding.Found(cfg, TestUtil.TestConfigs.Sim(), seed: 42));
        int baselineNodes = w1.CatchmentSummaries[0].NodeCount;
        double baselineFarmland = w1.CatchmentSummaries[0].EffectiveArableKm2;
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
            if (CatchmentSystem.BlockMeanFertility(w1.Terrain!, lattice, node) > 0.0) { far = node; break; }
        }
        Assert.True(far >= 0, "no passable positive-fertility node outside the baseline catchment");

        // Hand-add a fast lane origin↔far, cheap enough to fit the travel budget
        // (test writes state directly; PathBuild owns this from T1.6), and bump
        // the network revision — the ONLY thing that invalidates a catchment.
        w1.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(0), origin));
        w1.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(1), far));
        w1.NetworkEdges.Add(new NetworkEdgeRow(
            new NetworkEdgeId(0), new NetworkNodeId(0), new NetworkNodeId(1),
            EdgeTypes.DirtPath, Cost: Budget(lattice) * 0.5));
        w1.NetworkMeta[0] = new NetworkMetaRow(Revision: 1);

        // Turn 2: reads Prev (revision 1, summaries at revision 0) ⇒ stale ⇒ recompute.
        WorldState w2 = exec.Step(w1);

        Assert.Equal(1, w2.CatchmentSummaries[0].NetworkRevision);
        Assert.True(w2.CatchmentSummaries[0].NodeCount > baselineNodes,
            $"catchment did not grow: {w2.CatchmentSummaries[0].NodeCount} <= {baselineNodes}");
        Assert.True(w2.CatchmentSummaries[0].EffectiveArableKm2 > baselineFarmland,
            $"farmland did not increase: {w2.CatchmentSummaries[0].EffectiveArableKm2} <= {baselineFarmland}");
    }

    [Fact]
    public void Catchment_RevisionUnchanged_RecomputeProvablySkipped()
    {
        var cfg = Dev();
        TurnExecutor exec = CatchmentExecutor();

        // Turn 1 recomputes (LastRecomputeTurn := Prev.Clock.Turn == 0).
        WorldState world = exec.Step(WorldFounding.Found(cfg, TestUtil.TestConfigs.Sim(), seed: 42));
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
        WorldState founded = WorldFounding.Found(cfg, TestUtil.TestConfigs.Sim(), seed: 42);

        TraversalLattice lattice = TraversalLattice.Build(founded.Terrain!);
        int origin = OriginOf(founded, lattice);

        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        Pathfinder.IsochroneResult iso =
            Pathfinder.Isochrone(lattice, founded, origin, TierBudgetOf(founded, lattice, 0));
        double recomputeMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0
                             / System.Diagnostics.Stopwatch.Frequency;

        // End-to-end turn (recompute inside the pipeline) as a cross-check.
        WorldState stepped = CatchmentExecutor().Step(founded);
        // T4.1b WEAKENING (director ruling, itemized in ADR-018 §11). WAS
        // Assert.Equal(iso, partitioned). The equality was correct only for a
        // world where no neighbour is ever close enough to contest a node: at
        // minSpacingKm = 480 the nearest neighbour sat ~30 lattice nodes away.
        // At 95.2 km neighbours contest, and the T2.3 partition removes one
        // node — which is the partitioning WORKING, not failing.
        //
        // UPPER BOUND — partitioning can only REMOVE nodes a nearer neighbour
        // claims, never add: partitioned <= unpartitioned, always.
        Assert.True(stepped.CatchmentSummaries[0].NodeCount <= iso.Reached.Length,
            $"partitioned catchment {stepped.CatchmentSummaries[0].NodeCount} EXCEEDS the " +
            $"unpartitioned isochrone {iso.Reached.Length} — partitioning added nodes, which it " +
            "cannot do; the partition is claiming cells outside the travel budget.");

        // LOWER BOUND — added in the SAME edit so the weakening cannot go
        // silent (an inequality alone passes if partitioning removes EVERY
        // node). DERIVED FROM THE GEOMETRY: a settlement always retains every
        // cell strictly nearer to it than to any neighbour, so at minimum
        // spacing s it keeps at least the disc of radius s/2 = 47.6 km against
        // the 50 km hinterland — an area fraction of (47.6/50)^2 = 0.906.
        // ASYMMETRIC MARGIN (§7.16), stating the weaker side: floor 19 against
        // a measured 21 leaves TWO nodes of headroom, deliberately thin. A
        // generous floor (say half the isochrone) would still pass if
        // partitioning collapsed the catchment to a fifth of its budget, which
        // is precisely the failure this bound exists to catch. If a future
        // spacing change makes 0.906 wrong, DERIVE IT AGAIN — do not lower it
        // to accommodate a red.
        int floorNodes = (int)Math.Floor(0.906 * iso.Reached.Length);
        Assert.True(stepped.CatchmentSummaries[0].NodeCount >= floorNodes,
            $"partitioned catchment {stepped.CatchmentSummaries[0].NodeCount} fell below the " +
            $"geometric floor {floorNodes} (0.906 x {iso.Reached.Length}) — a settlement is losing " +
            "cells NEARER to it than to any neighbour, so the partition is mis-assigning, not " +
            "contesting.");

        Assert.True(iso.Reached.Length > 0);
        Assert.True(recomputeMs < 2000, $"catchment recompute took {recomputeMs:F1} ms");
        Console.WriteLine($"catchment @ 1024²: {iso.Reached.Length} nodes, recompute {recomputeMs:F1} ms");
    }
}
