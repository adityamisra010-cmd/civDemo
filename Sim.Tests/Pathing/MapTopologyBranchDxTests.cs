using Sim.Core.Pathing;
using Sim.Tests.Pathing.Legacy;

namespace Sim.Tests.Pathing;

/// <summary>
/// Batch 4 (D-055, T5, O4; b4-design §5 "an exhaustive Dx test") — THE BRANCH-ONLY WRAPPED DX AND THE HEURISTIC BUILT ON
/// IT. The optimised A* computes h at each pushed neighbour from coordinates it already holds, with
/// <see cref="MapTopology.DxOnMap"/> (two branches, no modulo) in place of <see cref="MapTopology.Dx"/>. Both are claimed
/// equal on the domain the pathfinder uses — two columns already on the map — so this test enumerates that domain
/// completely: sizes 2..12 (every small odd and even case, the +size/2 east tie of an even size included), 63, 64 and
/// 256 (the shipped lattice), both topology flags, every (x1, x2). The heuristic doubles are then compared BIT FOR BIT
/// with the batch-3 <c>Heuristic(lattice, node, gx, gy, effMin)</c> (LegacyPathfinder, verbatim) on a lattice of each
/// size: every (x, gx) and, up to 12, every (y, gy); at 63, 64 and 256 three rows each way.
/// </summary>
public class MapTopologyBranchDxTests
{
    private static readonly int[] Sizes = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 63, 64, 256];

    [Fact]
    public void DxOnMap_IsDx_ForEveryPairOfColumnsOnTheMap_EverySize_BothTopologies()
    {
        long pairs = 0, ties = 0, wrappedShort = 0;
        foreach (int size in Sizes)
            foreach (bool wrap in new[] { false, true })
                for (int x1 = 0; x1 < size; x1++)
                    for (int x2 = 0; x2 < size; x2++)
                    {
                        int expected = MapTopology.Dx(x1, x2, size, wrap);
                        int got = MapTopology.DxOnMap(x1, x2, size, wrap);
                        if (got != expected)
                            Assert.Fail($"DxOnMap({x1}, {x2}, {size}, {wrap}) = {got}, Dx = {expected}");
                        pairs++;
                        if (wrap && size % 2 == 0 && expected == size / 2) ties++;
                        if (wrap && expected != x2 - x1) wrappedShort++;
                    }
        // Anti-vacuity: the even-size tie (resolved east, +size/2, from BOTH ends) and the short-way-round branch are
        // both on the enumerated domain.
        Assert.True(ties > 0, "no half-world tie was enumerated");
        Assert.True(wrappedShort > 0, "no pair went the short way round");
        Assert.Equal(SumOfSquares() * 2, pairs);
    }

    private static long SumOfSquares()
    {
        long s = 0;
        foreach (int size in Sizes) s += (long)size * size;
        return s;
    }

    [Fact]
    public void Heuristic_IsTheBatch3Heuristic_BitForBit_EveryColumnPair_EverySize_BothTopologies()
    {
        // effMin values: the lattice minimum a FromCosts lattice gives, ideal ground, the seed-42 bench's 0.2-ish
        // river-block bound, and an awkward binary fraction.
        double[] effMins = [1.0, 0.2, 0.137, 1.0 / 3.0, 2.5];
        long compared = 0, ties = 0;
        foreach (int size in Sizes)
            foreach (bool wrap in new[] { false, true })
            {
                if (wrap && size < MapTopology.MinWrapSize) continue;   // no such lattice exists (RequireWrapSize)
                var costs = new double[size * size];
                var passable = new bool[size * size];
                Array.Fill(costs, 1.0);
                Array.Fill(passable, true);
                TraversalLattice lattice = TraversalLattice.FromCosts(size, costs, passable, wrapX: wrap);
                int[] rows = size <= 12 ? AllRows(size) : [0, size / 2, size - 1];
                foreach (double effMin in effMins)
                    foreach (int y in rows)
                        foreach (int gy in rows)
                            for (int x = 0; x < size; x++)
                                for (int gx = 0; gx < size; gx++)
                                {
                                    double old = LegacyPathfinder.Heuristic(lattice, lattice.NodeId(x, y), gx, gy, effMin);
                                    double now = Pathfinder.Heuristic(x, y, gx, gy, size, wrap, effMin);
                                    if (BitConverter.DoubleToInt64Bits(old) != BitConverter.DoubleToInt64Bits(now))
                                        Assert.Fail($"h({x},{y} -> {gx},{gy}; size {size}, wrap {wrap}, effMin {effMin:R}) = {now:R}, batch 3 {old:R}");
                                    compared++;
                                    if (wrap && size % 2 == 0 && MapTopology.Dx(gx, x, size, wrap) == size / 2) ties++;
                                }
            }
        Assert.True(ties > 0, "no half-world tie reached the heuristic");
        Assert.True(compared > 1_000_000, $"only {compared} heuristic values compared");
    }

    private static int[] AllRows(int size)
    {
        var rows = new int[size];
        for (int i = 0; i < size; i++) rows[i] = i;
        return rows;
    }
}
