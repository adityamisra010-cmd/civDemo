using Proto.M7.Spatial;
using static Proto.M7.Tests.Spatial.SpatialTestSupport;

namespace Proto.M7.Tests.Spatial;

/// <summary>Spec §3: Vec2L/CellOf/SupercoverLine (exact, corner rule defined, direction-independent).</summary>
public class SupercoverTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3999, 0)]
    [InlineData(4000, 1)]
    [InlineData(-1, -1)]
    [InlineData(-4000, -1)]
    [InlineData(-4001, -2)]
    [InlineData(-1_000_000_000, -250_000)]
    [InlineData(1_000_000_000, 250_000)]
    public void CellOf_is_floor_division_and_negative_safe(long x, long expected)
    {
        Assert.Equal(new CellCoord(expected, expected), TerrainGrid.CellOf(new Vec2L(x, x), 4000));
    }

    [Fact]
    public void Distance_is_exact_floor_sqrt_including_range_corners()
    {
        Assert.Equal(5, Vec2L.Distance(new(0, 0), new(3, 4)));
        Assert.Equal(1, Vec2L.Distance(new(0, 0), new(1, 1)));                 // sqrt 2 -> 1
        Assert.Equal(5656, Vec2L.Distance(new(0, 0), new(4000, 4000)));        // 5656.85 -> 5656
        // Corner to corner of the documented range: |d|^2 = 8e18 (Int128); floor sqrt pinned by its defining inequality.
        long L = Vec2L.Distance(new(-1_000_000_000, -1_000_000_000), new(1_000_000_000, 1_000_000_000));
        Assert.Equal(2_828_427_124L, L);
        UInt128 n = (UInt128)8_000_000_000_000_000_000UL;
        Assert.True((UInt128)L * (UInt128)L <= n && (UInt128)(L + 1) * (UInt128)(L + 1) > n);
        Assert.Throws<ArgumentOutOfRangeException>(() => Supercover.Cells(4000, new(0, 0), new(1_000_000_001, 0)));
    }

    [Fact]
    public void Matches_bruteforce_oracle_on_dense_small_lattice()
    {
        // S = 4 with coordinates in [-20, 20]: many exact corner crossings, boundary endpoints and negative coordinates.
        for (int i = 0; i < 3000; i++)
        {
            ulong seed = 11;
            var a = new Vec2L(R(seed, i, 0, 41) - 20, R(seed, i, 1, 41) - 20);
            var b = new Vec2L(R(seed, i, 2, 41) - 20, R(seed, i, 3, 41) - 20);
            if (i % 5 == 0) b = new Vec2L(a.X + (R(seed, i, 4, 9) - 4) * 4, a.Y + (R(seed, i, 4, 9) - 4) * 4); // exact diagonals
            CheckAgainstOracle(4, a, b);
        }
        for (int i = 0; i < 500; i++)
        {
            var a = new Vec2L(R(5, i, 0, 61) - 30, R(5, i, 1, 61) - 30);
            var b = new Vec2L(R(5, i, 2, 61) - 30, R(5, i, 3, 61) - 30);
            CheckAgainstOracle(6, a, b);
        }
    }

    private static void CheckAgainstOracle(long S, Vec2L a, Vec2L b)
    {
        var cells = Supercover.Cells(S, a, b);
        var set = CellSet(cells);
        Assert.Equal(cells.Count, set.Count);                                   // each cell exactly once
        Assert.True(Oracle(S, a, b).SetEquals(set), $"oracle mismatch for {a} -> {b}");
        Assert.Equal(TerrainGrid.CellOf(a, S), new CellCoord(cells[0].X, cells[0].Y));
        long D = Supercover.Denominator(a, b), sum = 0, last = 0;
        CoverCell lastMain = cells[0];
        foreach (var c in cells)
        {
            Assert.True(c.TIn >= last && c.TOut >= c.TIn && c.TOut <= D);       // non-decreasing, within [0, D]
            last = c.TIn;
            if (c.CornerSide) Assert.Equal(c.TIn, c.TOut);
            else { sum += c.TOut - c.TIn; lastMain = c; }
        }
        Assert.Equal(D, sum);                                                   // membership intervals partition [0, 1]
        Assert.Equal(TerrainGrid.CellOf(b, S), new CellCoord(lastMain.X, lastMain.Y));
    }

    [Fact]
    public void Cell_set_is_reversal_invariant()
    {
        for (int i = 0; i < 2000; i++)
        {
            var a = new Vec2L(R(21, i, 0, 81) - 40, R(21, i, 1, 81) - 40);
            var b = new Vec2L(R(21, i, 2, 81) - 40, R(21, i, 3, 81) - 40);
            Assert.True(CellSet(Supercover.Cells(4, a, b)).SetEquals(CellSet(Supercover.Cells(4, b, a))), $"{a} {b}");
        }
    }

    [Fact]
    public void Exact_corner_crossing_visits_both_side_cells()
    {
        // Centre (0,0) -> centre (1,1) passes exactly through the corner (4000,4000).
        var cells = Supercover.Cells(4000, new(2000, 2000), new(6000, 6000));
        Assert.Equal(new[] { (0L, 0L, false), (1L, 0L, true), (0L, 1L, true), (1L, 1L, false) },
                     cells.Select(c => (c.X, c.Y, c.CornerSide)).ToArray());
        Assert.Equal(cells[1].TIn, cells[1].TOut);
        Assert.Equal(Supercover.Denominator(new(2000, 2000), new(6000, 6000)) / 2, cells[1].TIn);   // at t = 1/2
        // Mixed directions (+x, -y) through the corner (4000, 4000): all four cells.
        var mixed = CellSet(Supercover.Cells(4000, new(2000, 6000), new(6000, 2000)));
        Assert.True(mixed.SetEquals(new[] { (0L, 1L), (1L, 1L), (0L, 0L), (1L, 0L) }));
        // A near-miss of the corner by 1 mm touches only three cells.
        Assert.Equal(3, Supercover.Cells(4000, new(2000, 2001), new(6000, 6001)).Count);
    }

    [Fact]
    public void Endpoint_exactly_on_a_corner_includes_all_four_cells()
    {
        var s = CellSet(Supercover.Cells(4000, new(4000, 4000), new(6000, 6000)));       // start corner, heading +,+
        Assert.True(s.SetEquals(new[] { (0L, 0L), (1L, 0L), (0L, 1L), (1L, 1L) }));
        var e = CellSet(Supercover.Cells(4000, new(6000, 6000), new(4000, 4000)));       // end corner, coming -,-
        Assert.True(e.SetEquals(s));
    }

    [Fact]
    public void Half_open_boundary_endpoints()
    {
        // Moving +x and ending exactly on x = 4000: that point belongs to cell 1 (floor), so cell 1 is included.
        Assert.Equal(new[] { 0L, 1L }, Supercover.Cells(4000, new(1000, 1000), new(4000, 1000)).Select(c => c.X).ToArray());
        // Moving -x and ending exactly on x = 4000: never strictly left of the line, so cell 0 is NOT included.
        Assert.Equal(new[] { 1L }, Supercover.Cells(4000, new(7000, 1000), new(4000, 1000)).Select(c => c.X).ToArray());
        // Moving -x from exactly x = 4000: leaves cell 1 at t = 0.
        var c2 = Supercover.Cells(4000, new(4000, 1000), new(1000, 1000));
        Assert.Equal(new[] { 1L, 0L }, c2.Select(c => c.X).ToArray());
        Assert.Equal(0, c2[0].TOut);
        // Axis-parallel along the grid line y = 4000: only the +side row (membership), documented.
        Assert.All(Supercover.Cells(4000, new(1000, 4000), new(15000, 4000)), c => Assert.Equal(1, c.Y));
    }

    [Fact]
    public void Zero_length_segment_is_its_own_cell()
    {
        var c = Supercover.Cells(4000, new(-1, -1), new(-1, -1));
        Assert.Single(c);
        Assert.Equal((-1L, -1L), (c[0].X, c[0].Y));
    }

    [Fact]
    public void Huge_segment_across_the_documented_range_is_exact()
    {
        var a = new Vec2L(-1_000_000_000, -999_999_999);
        var b = new Vec2L(1_000_000_000, 1_000_000_000);
        var cells = Supercover.Cells(1_000_000, a, b);
        long D = Supercover.Denominator(a, b), sum = 0;
        foreach (var c in cells) if (!c.CornerSide) sum += c.TOut - c.TIn;
        Assert.Equal(D, sum);
        Assert.Equal(TerrainGrid.CellOf(b, 1_000_000), new CellCoord(cells[^1].X, cells[^1].Y));
        Assert.Equal(cells.Count, CellSet(cells).Count);
    }
}
