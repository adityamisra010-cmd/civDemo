using Sim.Core.Pathing;

namespace Sim.Tests.Pathing;

/// <summary>
/// Batch 3 WS3 — THE TOPOLOGY CHOKEPOINT (<see cref="MapTopology"/>): the integer x delta, neighbour and distance every
/// sim site now reads, pinned exhaustively on small sizes (tie-dense: every pair of columns, even and odd sizes), with
/// the flat answers proven equal to the arithmetic each site used before batch 3.
/// </summary>
public class MapTopologyTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(64)]
    public void Dx_Wrapped_IsTheShortestSignedDelta_ForEveryPairOfColumns(int size)
    {
        for (int a = 0; a < size; a++)
            for (int b = 0; b < size; b++)
            {
                int d = MapTopology.Dx(a, b, size, wrap: true);
                // It lands on b: a + d ≡ b (mod size).
                Assert.Equal(b, ((a + d) % size + size) % size);
                // It is the SHORTEST such delta: no other representative is shorter.
                int flat = b - a;
                int best = int.MaxValue;
                foreach (int k in new[] { -1, 0, 1 }) best = Math.Min(best, Math.Abs(flat + k * size));
                Assert.Equal(best, Math.Abs(d));
                // In (−size/2, size/2] with integer division (the documented interval).
                Assert.True(d > -(size / 2) - (size % 2 == 1 ? 1 : 0) && d <= size / 2, $"size {size}: Dx({a},{b}) = {d}");
                // Distance is symmetric even where the sign is not.
                Assert.Equal(Math.Abs(d), MapTopology.AbsDx(b, a, size, wrap: true));
            }
    }

    [Fact]
    public void Dx_AtTheExactHalfWorldTie_ResolvesEastFromBothEnds()
    {
        // Even size: columns 0 and 4 of 8 are equally far both ways. The documented convention is +size/2 from BOTH
        // ends, so Dx(a, b) != −Dx(b, a) exactly there — and nowhere else.
        Assert.Equal(4, MapTopology.Dx(0, 4, 8, wrap: true));
        Assert.Equal(4, MapTopology.Dx(4, 0, 8, wrap: true));
        Assert.Equal(4, MapTopology.Dx(7, 3, 8, wrap: true));
        int asymmetric = 0;
        for (int a = 0; a < 8; a++)
            for (int b = 0; b < 8; b++)
                if (MapTopology.Dx(a, b, 8, true) != -MapTopology.Dx(b, a, 8, true)) asymmetric++;
        Assert.Equal(8, asymmetric);   // exactly the 8 ordered tie pairs (a, a ± 4)
        // Odd size: no tie at all — antisymmetric everywhere.
        for (int a = 0; a < 9; a++)
            for (int b = 0; b < 9; b++)
                Assert.Equal(MapTopology.Dx(a, b, 9, true), -MapTopology.Dx(b, a, 9, true));
    }

    [Fact]
    public void Dx_Flat_IsThePlainDifference_AndNeighbourXIsOffTheMapAtTheEdges()
    {
        for (int a = 0; a < 7; a++)
            for (int b = 0; b < 7; b++)
                Assert.Equal(b - a, MapTopology.Dx(a, b, 7, wrap: false));
        // The flat vs wrapped neighbour table at both edges.
        Assert.Equal(-1, MapTopology.NeighbourX(0, -1, 10, wrap: false));
        Assert.Equal(-1, MapTopology.NeighbourX(9, 1, 10, wrap: false));
        Assert.Equal(9, MapTopology.NeighbourX(0, -1, 10, wrap: true));
        Assert.Equal(0, MapTopology.NeighbourX(9, 1, 10, wrap: true));
        Assert.Equal(5, MapTopology.NeighbourX(4, 1, 10, wrap: false));
        Assert.Equal(5, MapTopology.NeighbourX(4, 1, 10, wrap: true));
        Assert.Equal(7, MapTopology.NeighbourX(2, -5, 10, wrap: true));
        Assert.Equal(-1, MapTopology.NeighbourX(2, -5, 10, wrap: false));
    }

    [Fact]
    public void SquaredPx_Flat_EqualsThePreBatch3Formula_AndWrappedIsTheMinimumOverCopies()
    {
        const int size = 12;
        for (int a = 0; a < size * size; a++)
            for (int b = 0; b < size * size; b++)
            {
                // The expression RoadDevelopmentQuery.SquaredPx, GeographicDistance.Km and SiteAssessment wrote inline.
                long dx = a % size - b % size, dy = a / size - b / size;
                Assert.Equal(dx * dx + dy * dy, MapTopology.SquaredPx(a, b, size, wrap: false));
                long best = long.MaxValue;
                foreach (int k in new[] { -1, 0, 1 })
                {
                    long cx = dx + k * size;
                    best = Math.Min(best, cx * cx + dy * dy);
                }
                Assert.Equal(best, MapTopology.SquaredPx(a, b, size, wrap: true));
            }
        // The seam pair: columns 0 and 11 of the same row are ONE pixel apart on the cylinder.
        Assert.Equal(1, MapTopology.SquaredPx(3 * size + 0, 3 * size + 11, size, wrap: true));
        Assert.Equal(121, MapTopology.SquaredPx(3 * size + 0, 3 * size + 11, size, wrap: false));
        Assert.Equal(4.0, MapTopology.Km(0, 11, size, 4.0, wrap: true));
    }

    [Fact]
    public void WrapCoord_NormalisesIntoTheHalfOpenRange_IncludingTheRoundingEdge()
    {
        Assert.Equal(0.0, MapTopology.WrapCoord(0.0, 1024.0));
        Assert.Equal(1023.5, MapTopology.WrapCoord(-0.5, 1024.0));
        Assert.Equal(0.25, MapTopology.WrapCoord(1024.25, 1024.0));
        Assert.Equal(0.0, MapTopology.WrapCoord(1024.0, 1024.0));
        Assert.Equal(1.0, MapTopology.WrapCoord(-2047.0, 1024.0));
        // −1e−17 + 1024 rounds to exactly 1024 in double; the explicit fold keeps the result in [0, size).
        double r = MapTopology.WrapCoord(-1e-17, 1024.0);
        Assert.True(r >= 0.0 && r < 1024.0, $"WrapCoord(-1e-17) = {r}");
        Assert.True(double.IsNaN(MapTopology.WrapCoord(double.NaN, 1024.0)));
        // NearestCopy: the copy of x nearest the reference — the short way round.
        Assert.Equal(-4.0, MapTopology.NearestCopy(1020.0, 10.0, 1024.0));
        Assert.Equal(1030.0, MapTopology.NearestCopy(6.0, 1000.0, 1024.0));
        Assert.Equal(600.0, MapTopology.NearestCopy(600.0, 500.0, 1024.0));
        // The exact half-world tie goes east (the Dx convention).
        Assert.Equal(512.0, MapTopology.NearestCopy(512.0, 0.0, 1024.0));
        Assert.Equal(512.0, MapTopology.NearestCopy(-512.0, 0.0, 1024.0));
    }

    [Fact]
    public void WrapX_Integer_NormalisesIntoRange()
    {
        Assert.Equal(1023, MapTopology.WrapX(-1, 1024));
        Assert.Equal(0, MapTopology.WrapX(1024, 1024));
        Assert.Equal(5, MapTopology.WrapX(5, 1024));
        Assert.Equal(1023, MapTopology.WrapX(-1L, 1024));
        Assert.Equal(3, MapTopology.WrapX(1024L * 1000 + 3, 1024));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AWrappedAxis_NarrowerThanThreeColumns_IsRefused(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapTopology.RequireWrapSize(size, wrap: true));
        MapTopology.RequireWrapSize(size, wrap: false);   // flat: any size
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TraversalLattice.FromCosts(size, new double[size * size], new bool[size * size], wrapX: true));
    }

    [Fact]
    public void LatticeGrid_AndNodeOfCell_RestateTheOldClampedMappingExactly()
    {
        foreach (int terrainSize in new[] { 16, 250, 256, 1024 })
        {
            (int lsize, int block) = MapTopology.LatticeGrid(terrainSize, TraversalLattice.DefaultStride);
            // The PolityKnowledge.Grid / LatticeMap arithmetic before batch 3.
            int oldL = Math.Max(1, terrainSize / TraversalLattice.DefaultStride);
            Assert.Equal(oldL, lsize);
            Assert.Equal(Math.Max(1, terrainSize / oldL), block);
            for (int cell = 0; cell < terrainSize * terrainSize; cell += 97)
            {
                int ox = Math.Min(cell % terrainSize / block, lsize - 1), oy = Math.Min(cell / terrainSize / block, lsize - 1);
                Assert.Equal(oy * lsize + ox, MapTopology.NodeOfCell(cell, terrainSize, lsize, block));
            }
        }
    }

    /// <summary>W2 (rendering stage): UnwrapNear leaves a point that is already the nearest copy BIT FOR BIT (a primitive
    /// that does not cross the seam keeps its coordinates), and otherwise returns the nearest copy — tie-dense over every
    /// half-cell pair of a small world, with the half-world tie resolved east (NearestCopy's convention).</summary>
    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(256)]
    public void UnwrapNear_KeepsTheNearestCopyBitForBit_AndOtherwiseMovesToIt(int size)
    {
        for (int a = 0; a < size; a++)
            for (int b = 0; b < size; b++)
            {
                double ax = a + 0.5, bx = b + 0.5;
                double u = MapTopology.UnwrapNear(bx, ax, size);
                double d = u - ax;
                Assert.True(d > -size / 2.0 && d <= size / 2.0, $"size {size}: {bx} from {ax} -> {u}");
                Assert.Equal(0.0, Math.IEEERemainder(u - bx, size));   // a copy of b (a whole number of circumferences)
                if (bx - ax > -size / 2.0 && bx - ax <= size / 2.0) Assert.Equal(BitConverter.DoubleToInt64Bits(bx), BitConverter.DoubleToInt64Bits(u));
                // Exactly the integer short way: the column a + Dx(a, b).
                Assert.Equal(a + MapTopology.Dx(a, b, size, wrap: true) + 0.5, u);
            }
        Assert.True(double.IsNaN(MapTopology.UnwrapNear(double.NaN, 1, size)));
        // A non-crossing pair whose NearestCopy arithmetic (ref + (x − ref)) would round: x itself, untouched.
        Assert.Equal(BitConverter.DoubleToInt64Bits(0.1), BitConverter.DoubleToInt64Bits(MapTopology.UnwrapNear(0.1, 0.7, size)));
        Assert.NotEqual(0.1, MapTopology.NearestCopy(0.1, 0.7, size));   // (control: that arithmetic does round here)
    }

    [Fact]
    public void NodeCentreCell_AndBlockPx_RestateTheOverlayAndMeshArithmeticExactly()
    {
        foreach (int size in new[] { 16, 250, 256, 1024 })
        {
            (int lsize, int block) = MapTopology.LatticeGrid(size, TraversalLattice.DefaultStride);
            Assert.Equal(block, MapTopology.BlockPx(size, lsize));
            Assert.Equal(size / lsize, MapTopology.BlockPx(size, lsize));   // OverlayMeshes.LatticeStride's old terrainSize / lattice.Size
            for (int node = 0; node < lsize * lsize; node++)
            {
                // ResourceOverlay.SampleCell's pre-W2 arithmetic.
                int x = Math.Min(size - 1, node % lsize * block + block / 2);
                int y = Math.Min(size - 1, node / lsize * block + block / 2);
                Assert.Equal(y * size + x, MapTopology.NodeCentreCell(node, size, lsize, block));
            }
        }
    }
}
