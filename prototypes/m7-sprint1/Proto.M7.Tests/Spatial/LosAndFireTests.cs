using Proto.M7.Fixed;
using Proto.M7.Spatial;
using static Proto.M7.Tests.Spatial.SpatialTestSupport;

namespace Proto.M7.Tests.Spatial;

/// <summary>Spec §3: LOS (walls, height, vegetation clarity) and direct fire (exact segment-vs-disc, first hit (t, id), blocking).</summary>
public class LosAndFireTests
{
    private static LosResult Los(TerrainGrid g, Vec2L a, Vec2L b, long eye = 1700, long th = 1700) => LineOfSight.Query(g, Conceal, a, eye, b, th);

    [Fact]
    public void Clear_line_on_open_ground()
    {
        var g = new TerrainGrid(10, 10);
        var r = Los(g, g.CellCenter(0, 0), g.CellCenter(9, 6));
        Assert.True(r.Visible); Assert.Equal(Q32.One, r.Clarity); Assert.Equal(LosBlock.None, r.Block);
    }

    [Fact]
    public void Wall_blocks_and_corner_touching_walls_block_a_diagonal()
    {
        var g = new TerrainGrid(10, 10);
        g.SetClass(5, 2, TerrainClass.Wall);
        var r = Los(g, g.CellCenter(0, 2), g.CellCenter(9, 2));
        Assert.False(r.Visible); Assert.Equal(LosBlock.Wall, r.Block); Assert.Equal(new CellCoord(5, 2), r.BlockCell);
        Assert.Equal(Q32.Zero, r.Clarity);
        // Diagonal sight line through the corner shared by one wall cell: blocked by the corner rule.
        var h = new TerrainGrid(4, 4);
        h.SetClass(2, 1, TerrainClass.Wall);
        Assert.Equal(LosBlock.Wall, Los(h, h.CellCenter(1, 1), h.CellCenter(2, 2)).Block);
        // Water does not block sight.
        h.SetClass(2, 1, TerrainClass.Water);
        Assert.True(Los(h, h.CellCenter(1, 1), h.CellCenter(2, 2)).Visible);
    }

    [Fact]
    public void Height_block_threshold_is_exact()
    {
        // Flat ground 0, eye 1000, target height 1000 => the sight line is at 1000 mm everywhere.
        var g = new TerrainGrid(10, 1);
        g.Set(5, 0, TerrainClass.Hills, 1000);
        Assert.True(Los(g, g.CellCenter(0, 0), g.CellCenter(9, 0), 1000, 1000).Visible);          // equal = not above the line
        g.Set(5, 0, TerrainClass.Hills, 1001);
        var r = Los(g, g.CellCenter(0, 0), g.CellCenter(9, 0), 1000, 1000);
        Assert.False(r.Visible); Assert.Equal(LosBlock.Height, r.Block); Assert.Equal(new CellCoord(5, 0), r.BlockCell);
    }

    [Fact]
    public void Height_uses_interpolated_line_and_endpoint_cells_are_excluded()
    {
        // Observer on a 20 m hill (cell 0), ridge of 10 m at cell 4, target on flat ground at cell 9.
        var g = new TerrainGrid(10, 1);
        g.Set(0, 0, TerrainClass.Hills, 20_000);
        g.Set(4, 0, TerrainClass.Hills, 10_000);
        // Line from 21,700 (x=2000) to 1,700 (x=38000); over cell 4 (x 16000..20000) it is >= 21700 - 20000*18000/36000 = 11,700.
        Assert.True(Los(g, g.CellCenter(0, 0), g.CellCenter(9, 0)).Visible);
        g.Set(4, 0, TerrainClass.Hills, 11_701);        // the line's minimum over cell 4 is 21700 - 20000*18000/36000 = 11,700
        Assert.False(Los(g, g.CellCenter(0, 0), g.CellCenter(9, 0)).Visible);
        // The observer's and target's own (raised) cells never block their own sight line.
        var h = new TerrainGrid(3, 1);
        h.Set(0, 0, TerrainClass.Hills, 50_000); h.Set(2, 0, TerrainClass.Hills, 50_000);
        Assert.True(Los(h, h.CellCenter(0, 0), h.CellCenter(2, 0), 0, 0).Visible);
    }

    [Fact]
    public void Vegetation_accumulates_concealment_as_an_exact_Q32_product()
    {
        var g = new TerrainGrid(10, 1);
        g.SetClass(3, 0, TerrainClass.Forest);
        g.SetClass(4, 0, TerrainClass.Forest);
        g.SetClass(9, 0, TerrainClass.Jungle);           // target hides in jungle
        var r = Los(g, g.CellCenter(0, 0), g.CellCenter(9, 0));
        Assert.True(r.Visible);
        Assert.Equal(Q32.FromRatio(1, 2).Mul(Q32.FromRatio(1, 2)).Mul(Q32.FromRatio(1, 4)), r.Clarity);
        Assert.Equal(Q32.FromRatio(1, 16), r.Clarity);
        // The observer's own forest cell does not count (looking OUT of cover); the target's cell does.
        g.SetClass(0, 0, TerrainClass.Forest);
        Assert.Equal(Q32.FromRatio(1, 16), Los(g, g.CellCenter(0, 0), g.CellCenter(9, 0)).Clarity);
        Assert.Equal(Q32.FromRatio(1, 8), Los(g, g.CellCenter(9, 0), g.CellCenter(0, 0)).Clarity);    // reversed: jungle is the observer's
        // Same cell: the target's cell counts.
        Assert.Equal(Q32.FromRatio(1, 2), Los(g, new(100, 100), new(3000, 3000)).Clarity);
    }

    [Fact]
    public void Wall_blocking_is_symmetric_on_random_flat_grids()
    {
        int blocked = 0;
        for (int i = 0; i < 600; i++)
        {
            var g = RandomGrid(16, 16, (ulong)(i / 30 + 3000), 15);
            var a = new Vec2L(R(31, i, 0, 64_000), R(31, i, 1, 64_000));
            var b = new Vec2L(R(31, i, 2, 64_000), R(31, i, 3, 64_000));
            var f = Los(g, a, b); var r = Los(g, b, a);
            Assert.Equal(f.Visible, r.Visible);
            if (!f.Visible) blocked++;
        }
        Assert.True(blocked > 100 && blocked < 590);
    }

    [Fact]
    public void Off_grid_endpoints_are_not_visible()
    {
        var g = new TerrainGrid(4, 4);
        Assert.Equal(LosBlock.OffGrid, Los(g, g.CellCenter(1, 1), new(-1, 2000)).Block);
        Assert.Equal(LosBlock.OffGrid, Los(g, new(16_000, 2000), g.CellCenter(1, 1)).Block);   // x = 16000 is cell 4: off-grid
    }

    // ---------------- segment vs disc ----------------

    [Fact]
    public void Tangent_is_a_hit_and_one_mm_further_is_a_miss()
    {
        Assert.True(Projectiles.SegmentCircle(new(0, 0), new(10_000, 0), new(5_000, 300), 300).Hit);
        Assert.False(Projectiles.SegmentCircle(new(0, 0), new(10_000, 0), new(5_000, 301), 300).Hit);
        var t = Projectiles.SegmentCircle(new(0, 0), new(10_000, 0), new(5_000, 300), 300);
        Assert.Equal(new SegmentHit(true, 50_000_000, 100_000_000), t);          // tangent point t = 1/2 exactly
    }

    [Fact]
    public void Exactly_at_endpoint_and_start_inside()
    {
        var e = Projectiles.SegmentCircle(new(0, 0), new(10_000, 0), new(10_300, 0), 300);
        Assert.True(e.Hit); Assert.Equal(e.TDen, e.TNum);                        // touches only at b: t = 1
        Assert.False(Projectiles.SegmentCircle(new(0, 0), new(10_000, 0), new(10_301, 0), 300).Hit);
        var s = Projectiles.SegmentCircle(new(0, 0), new(10_000, 0), new(100, 100), 300);
        Assert.True(s.Hit); Assert.Equal(0, s.TNum);
        // Disc behind the start: miss.
        Assert.False(Projectiles.SegmentCircle(new(0, 0), new(10_000, 0), new(-301, 0), 300).Hit);
        Assert.True(Projectiles.SegmentCircle(new(0, 0), new(10_000, 0), new(-300, 0), 300).Hit);
    }

    [Fact]
    public void Zero_length_segment()
    {
        Assert.Equal(new SegmentHit(true, 0, 1), Projectiles.SegmentCircle(new(5, 5), new(5, 5), new(5, 305), 300));   // on boundary
        Assert.False(Projectiles.SegmentCircle(new(5, 5), new(5, 5), new(5, 306), 300).Hit);
    }

    [Fact]
    public void Entry_parameter_is_the_exact_root_rounded_down()
    {
        // Through the centre: entry at x = 9700 of 0..20000 -> t = 9700/20000 exactly (perfect square discriminant).
        var h = Projectiles.SegmentCircle(new(0, 0), new(20_000, 0), new(10_000, 0), 300);
        Assert.Equal(new SegmentHit(true, 9_700L * 20_000, 400_000_000), h);
        // Oblique, irrational root: TNum/A <= exact entry < (TNum+1)/A, verified by exact sign checks of |P(t) - c|^2 - r^2.
        Vec2L a = new(-7_000, 3_000), b = new(9_000, -1_000), c = new(1_234, 777); long r = 900;
        var o = Projectiles.SegmentCircle(a, b, c, r);
        Assert.True(o.Hit);
        Assert.True(OutsideOrOn(a, b, c, r, o.TNum, o.TDen));
        Assert.True(Excess(a, b, c, r, o.TNum + 1, o.TDen) < 0);                 // one quantum later is strictly inside
    }

    // |a + d*n/den - c|^2 vs r^2, scaled by den^2 (exact Int128).
    private static Int128 Excess(Vec2L a, Vec2L b, Vec2L c, long r, long n, long den)
    {
        Int128 X = (Int128)(a.X - c.X) * den + (Int128)(b.X - a.X) * n, Y = (Int128)(a.Y - c.Y) * den + (Int128)(b.Y - a.Y) * n;
        return X * X + Y * Y - (Int128)r * r * den * den;
    }
    private static bool OutsideOrOn(Vec2L a, Vec2L b, Vec2L c, long r, long n, long den) => Excess(a, b, c, r, n, den) >= 0;

    [Fact]
    public void Negative_coordinates_mirror_exactly()
    {
        var p = Projectiles.SegmentCircle(new(100, 50), new(9_000, 2_000), new(5_000, 1_400), 350);
        var m = Projectiles.SegmentCircle(new(-100, -50), new(-9_000, -2_000), new(-5_000, -1_400), 350);
        Assert.Equal(p, m);
        Assert.True(p.Hit);
    }

    [Fact]
    public void Huge_coordinates_at_the_documented_range_are_exact()
    {
        Vec2L a = new(-1_000_000_000, -1_000_000_000), b = new(1_000_000_000, 1_000_000_000);
        long r = Disc.MaxRadiusMm;
        var h = Projectiles.SegmentCircle(a, b, new(0, 0), r);
        Int128 A = (Int128)8_000_000_000_000_000_000UL;
        // Centre at the midpoint: P = A/2, |w|^2 = A/4 => discriminant = A r^2 => TNum = A/2 - ceil(sqrt(A r^2)).
        long expected = (long)(A / 2) - (long)SpatialMath.ISqrtCeil((UInt128)(A * r * r));
        Assert.Equal(new SegmentHit(true, expected, (long)A), h);
        Assert.Equal(3_997_171_572_875_253_809L, expected);                         // Python math.isqrt (independent)
        Assert.True(Projectiles.SegmentCircle(a, b, new(1_000_000_000, -1_000_000_000 + 0), r).Hit == false);
        Assert.Throws<ArgumentOutOfRangeException>(() => Projectiles.SegmentCircle(a, new(1_000_000_001, 0), new(0, 0), r));
        Assert.Throws<ArgumentOutOfRangeException>(() => Projectiles.SegmentCircle(a, b, new(0, 0), r + 1));
    }

    [Fact]
    public void First_hit_ordering_t_then_lower_id_independent_of_input_order()
    {
        Vec2L a = new(0, 0), b = new(20_000, 0);
        // Mirror images about the line: identical entry parameter => lower id wins.
        var discs = new[] { new Disc(7, new(5_000, 200), 300), new Disc(3, new(5_000, -200), 300), new Disc(1, new(9_000, 0), 300) };
        for (ulong p = 0; p < 6; p++)
        {
            var perm = TestSupport.Permutation(3, p);
            var shuffled = perm.Select(i => discs[i]).ToArray();
            Assert.True(Projectiles.FirstHit(a, b, shuffled, -1, out long id, out var h));
            Assert.Equal(3, id);
        }
        Assert.True(Projectiles.FirstHit(a, b, discs, 3, out long id2, out _));
        Assert.Equal(7, id2);                                                      // ignoring 3 (the shooter), 7 is next
        Assert.True(Projectiles.FirstHit(a, b, new[] { discs[2], new Disc(2, new(9_000, 0), 300) }, -1, out long id3, out _));
        Assert.Equal(1, id3);                                                      // identical discs: lower id
    }

    // ---------------- direct fire on the grid ----------------

    [Fact]
    public void Direct_fire_hits_or_is_blocked_by_wall_before_the_hit()
    {
        var g = new TerrainGrid(10, 3);
        g.SetClass(3, 0, TerrainClass.Wall);                        // wall face at x = 12000
        Vec2L from = new(2_000, 2_000), aim = new(20_000, 2_000);
        // Target in front of the wall.
        var r1 = Projectiles.DirectFire(g, from, 1500, aim, 1500, new[] { new Disc(5, new(8_000, 2_000), 300) }, 1);
        Assert.Equal(FireOutcome.Hit, r1.Outcome); Assert.Equal(5, r1.EntityId);
        // Target whose disc touches the wall face exactly (entry at x = 12000 = wall entry): a hit (documented).
        var r2 = Projectiles.DirectFire(g, from, 1500, aim, 1500, new[] { new Disc(5, new(12_300, 2_000), 300) }, 1);
        Assert.Equal(FireOutcome.Hit, r2.Outcome);
        // One mm further: the wall is entered strictly before the hit -> blocked.
        var r3 = Projectiles.DirectFire(g, from, 1500, aim, 1500, new[] { new Disc(5, new(12_301, 2_000), 300) }, 1);
        Assert.Equal(FireOutcome.Blocked, r3.Outcome); Assert.Equal(LosBlock.Wall, r3.Block); Assert.Equal(new CellCoord(3, 0), r3.BlockCell);
        // Shooter's own disc is ignored; no target at all -> blocked by the wall on the way to the aim point.
        var r4 = Projectiles.DirectFire(g, from, 1500, aim, 1500, new[] { new Disc(1, from, 300) }, 1);
        Assert.Equal(FireOutcome.Blocked, r4.Outcome);
        // Off the wall row: a clean miss.
        var r5 = Projectiles.DirectFire(g, new(2_000, 6_000), 1500, new(20_000, 6_000), 1500, Array.Empty<Disc>(), 1);
        Assert.Equal(FireOutcome.Miss, r5.Outcome);
    }

    [Fact]
    public void Direct_fire_height_block_and_off_grid()
    {
        var g = new TerrainGrid(10, 1);
        g.Set(4, 0, TerrainClass.Hills, 2_000);
        var target = new[] { new Disc(9, new(30_000, 2_000), 300) };
        var r = Projectiles.DirectFire(g, new(2_000, 2_000), 1500, new(30_000, 2_000), 1500, target, 1);
        Assert.Equal(FireOutcome.Blocked, r.Outcome); Assert.Equal(LosBlock.Height, r.Block);
        g.Set(4, 0, TerrainClass.Hills, 1_500);
        Assert.Equal(FireOutcome.Hit, Projectiles.DirectFire(g, new(2_000, 2_000), 1500, new(30_000, 2_000), 1500, target, 1).Outcome);
        var off = Projectiles.DirectFire(g, new(2_000, 2_000), 1500, new(50_000, 2_000), 1500, Array.Empty<Disc>(), 1);
        Assert.Equal(LosBlock.OffGrid, off.Block);
    }
}
