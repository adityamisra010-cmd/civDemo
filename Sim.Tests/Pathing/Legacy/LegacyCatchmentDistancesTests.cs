using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Pathing.Legacy;

/// <summary>
/// Batch 4 (D-055, T5, O5; fix of review finding T5-L1-T5-V1-01) — CATCHMENTSYSTEM'S SETTLEMENTDISTANCES ARE THE
/// BATCH-3 FULL-ISOCHRONE FIELD, read at the system seam. Batch 3 filled one n-sized field per settlement from
/// <c>Isochrone(origin, MaxValue)</c> (+∞ where it did not reach) and wrote row (from, to) = field_from[origin_to] in
/// ascending (From, To) order, From ≠ To. Batch 4 asks <see cref="Pathfinder.DistancesTo"/> for the costs at the OTHER
/// settlements' origins only, and wires its k-th answer to the k-th other settlement. The differential tests of
/// DistancesTo check the function; these check the wiring: every row of the table the system writes, bit for bit,
/// against <see cref="LegacyPathfinder"/>'s isochrone on the Prev world the system read.
/// - every catchment recompute in the first turns of the shipped founded worlds of seeds 42 (the bench seed) and 16
///   (the seam seed), where 12 settlements make every wrong index a wrong destination;
/// - a seam-world rig with two settlements on ONE origin lattice node (a target equal to the origin, and a duplicate
///   target from every other settlement) and a third settlement across the sea (an unreachable target, +∞ flat),
///   on both topologies.
/// </summary>
public class LegacyCatchmentDistancesTests
{
    private const int Horizon = 30;

    private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

    /// <summary>The batch-3 table for <paramref name="prev"/>: one full isochrone per settlement, read at every other
    /// settlement's origin, rows in ascending (From, To) order with From ≠ To.</summary>
    private static List<SettlementDistanceRow> Batch3Rows(IReadOnlyWorldState prev, SimConfig cfg)
    {
        TraversalLattice lattice = TraversalLattice.Build(prev.Terrain!, cfg.Transport.RiverCostFactor);
        int count = prev.Settlements.Count;
        var origins = new int[count];
        for (int s = 0; s < count; s++)
            origins[s] = LatticeMap.OriginLatticeNode(lattice, prev.Terrain!.Size, prev.Settlements[s].SiteCell);
        var fields = new double[count][];
        for (int s = 0; s < count; s++)
        {
            LegacyPathfinder.IsochroneResult full = LegacyPathfinder.Isochrone(lattice, prev, origins[s], double.MaxValue);
            var field = new double[lattice.NodeCount];
            Array.Fill(field, double.PositiveInfinity);
            for (int i = 0; i < full.Reached.Length; i++) field[full.Reached[i]] = full.Costs[i];
            fields[s] = field;
        }
        var rows = new List<SettlementDistanceRow>(count * (count - 1));
        for (int from = 0; from < count; from++)
            for (int to = 0; to < count; to++)
            {
                if (from == to) continue;
                rows.Add(new SettlementDistanceRow(prev.Settlements[from].Id, prev.Settlements[to].Id, fields[from][origins[to]]));
            }
        return rows;
    }

    private static void AssertRows(string where, List<SettlementDistanceRow> expected, IReadOnlyWorldState next)
    {
        Assert.Equal(expected.Count, next.SettlementDistances.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            SettlementDistanceRow want = expected[i], got = next.SettlementDistances[i];
            if (want.From != got.From || want.To != got.To || Bits(want.TravelCost) != Bits(got.TravelCost))
                Assert.Fail($"{where}: row {i} is {got.From.Value}->{got.To.Value} {got.TravelCost:R}; batch 3 wrote "
                    + $"{want.From.Value}->{want.To.Value} {want.TravelCost:R}");
        }
    }

    private static double Row(IReadOnlyWorldState w, int from, int to)
    {
        for (int i = 0; i < w.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow d = w.SettlementDistances[i];
            if (d.From.Value == from && d.To.Value == to) return d.TravelCost;
        }
        throw new InvalidOperationException($"no distance row {from}->{to}");
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(16UL)]
    public void SettlementDistances_AreTheBatch3IsochroneAtEveryOtherOrigin_OnEveryRecomputeOfTheFirstTurns(ulong seed)
    {
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor executor = UniversityRigs.Production(cfg);
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen(), cfg, seed);

        int recomputes = 0, rows = 0, finite = 0, maxSettlements = 0;
        var distinct = new SortedSet<long>();
        for (int turn = 1; turn <= Horizon; turn++)
        {
            WorldState prev = w;
            w = executor.Step(prev);
            // The system writes the table only on a recompute (D-016); the summary's LastRecomputeTurn is its proof.
            if (w.CatchmentSummaries.Count == 0 || w.CatchmentSummaries[0].LastRecomputeTurn != prev.Clock.Turn) continue;
            List<SettlementDistanceRow> expected = Batch3Rows(prev, cfg);
            AssertRows($"seed {seed} turn {turn}", expected, w);
            recomputes++;
            rows += expected.Count;
            maxSettlements = Math.Max(maxSettlements, prev.Settlements.Count);
            foreach (SettlementDistanceRow r in expected)
            {
                if (double.IsPositiveInfinity(r.TravelCost)) continue;
                finite++;
                distinct.Add(Bits(r.TravelCost));
            }
        }
        Console.WriteLine($"seed {seed}: {recomputes} recomputes in turns 1..{Horizon}, {rows} rows compared, {finite} finite, "
            + $"{distinct.Count} distinct costs, up to {maxSettlements} settlements");
        // Anti-vacuity: the table was recomputed more than once (the network grew), with enough settlements that a
        // misrouted index lands on another destination, and the destinations' costs differ from one another.
        Assert.True(recomputes > 1, "fewer than two catchment recomputes in the horizon");
        Assert.True(maxSettlements >= 3, "fewer than three settlements: a swapped index could not be told apart");
        Assert.True(distinct.Count > maxSettlements, "the costs are too uniform to tell destinations apart");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SettlementDistances_WithTwoSettlementsOnOneOriginNode_AndOneAcrossTheSea_AreTheBatch3Rows(bool wrap)
    {
        SimConfig cfg = TestConfigs.Sim();
        int size = SeamWorld.DefaultSize;
        // Cells (20, y) and (21, y) lie in one 4-px block: one origin lattice node for settlements 0 and 1. Settlement 2
        // is on the far piece of the strip (an island of its own flat; across the seam on the cylinder).
        int[] sites = [SeamWorld.Cell(20, size / 2), SeamWorld.Cell(21, size / 2), SeamWorld.Cell(size - 20, size / 2)];
        WorldState w0 = SeamWorld.Found(wrap, sites, sim: cfg);
        TraversalLattice lattice = TraversalLattice.Build(w0.Terrain!, cfg.Transport.RiverCostFactor);
        int o0 = LatticeMap.OriginLatticeNode(lattice, size, sites[0]);
        int o1 = LatticeMap.OriginLatticeNode(lattice, size, sites[1]);
        int o2 = LatticeMap.OriginLatticeNode(lattice, size, sites[2]);
        Assert.Equal(o0, o1);   // premise: one origin node for two settlements
        Assert.NotEqual(o0, o2);

        WorldState w1 = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.Catchment(cfg)).Step(w0);
        AssertRows($"wrap={wrap}", Batch3Rows(w0, cfg), w1);

        // What those rows mean (and what any misrouted index would break): the shared node is 0 apart both ways; the
        // third settlement is +∞ from both across the sea, and 10 lattice steps of ideal ground (exactly 10.0) the short
        // way round the cylinder — the same cost from both twins (a duplicate target answered twice).
        Assert.Equal(0.0, Row(w1, 0, 1));
        Assert.Equal(0.0, Row(w1, 1, 0));
        foreach ((int from, int to) in new[] { (0, 2), (1, 2), (2, 0), (2, 1) })
        {
            if (wrap) Assert.Equal(10.0, Row(w1, from, to));
            else Assert.True(double.IsPositiveInfinity(Row(w1, from, to)), $"flat {from}->{to} is finite");
        }
        Assert.Equal(6, w1.SettlementDistances.Count);
    }
}
