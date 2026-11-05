using Proto.M7.Fixed;
using Proto.M7.Spatial;
using static Proto.M7.Tests.Spatial.SpatialTestSupport;

namespace Proto.M7.Tests.Spatial;

/// <summary>
/// Fixes and pins from the verify-C-spatial round (findings C-V-1 .. C-V-9, surviving mutants V1, V2, V3, V5, V7).
/// Every expected value below is derived by hand in the comment next to it; none is a golden captured from a run.
/// </summary>
public class VerificationFixCTests
{
    private static LosResult Los(TerrainGrid g, Vec2L a, Vec2L b, long eye = 1700, long tgt = 1700) => LineOfSight.Query(g, Conceal, a, eye, b, tgt);

    // Origin (2000, 6000), z0 = 1500, vx = 100 m/s, g = 9807: x(t) = 2000 + 100 t, z(t) = 1500 + floor(-9807 t^2 / 2e6).
    private static readonly BallisticShot Shot = new(new(2_000, 6_000), 1_500, 100_000, 0, 0);

    // ---------------- C-V-1 / V7: LOS height on an ASCENDING sight line ----------------

    [Fact]
    public void Los_ascending_line_ridge_threshold_is_exact()
    {
        // The Height_uses_interpolated_line test REVERSED: observer on flat ground at cell 9 (z0 = 0 + 1700 at x = 38000),
        // target on the 20 m hill at cell 0 (z1 = 20000 + 1700 at x = 2000). D = 36000. Over ridge cell 4 (x 16000..20000) the
        // line ENTERS at x = 20000 (n = 18000): 1700 + floor(20000*18000/36000) = 11,700, and EXITS at x = 16000 (n = 22000):
        // 1700 + floor(20000*22000/36000) = 13,922. The block rule uses the minimum, 11,700: a 11,700 ridge does not block,
        // 11,701 does. (Comparing only the exit height, mutant V7, would need 13,923.)
        var g = new TerrainGrid(10, 1);
        g.Set(0, 0, TerrainClass.Hills, 20_000);
        g.Set(4, 0, TerrainClass.Hills, 11_700);
        Assert.True(Los(g, g.CellCenter(9, 0), g.CellCenter(0, 0)).Visible);
        g.Set(4, 0, TerrainClass.Hills, 11_701);
        var r = Los(g, g.CellCenter(9, 0), g.CellCenter(0, 0));
        Assert.Equal((false, LosBlock.Height, new CellCoord(4, 0)), (r.Visible, r.Block, r.BlockCell));
        // And the forward direction agrees (symmetry of the min rule with equal eye and target heights).
        Assert.False(Los(g, g.CellCenter(0, 0), g.CellCenter(9, 0)).Visible);
    }

    [Fact]
    public void Los_height_blocking_is_symmetric_on_random_height_fields()
    {
        // With equal eye and target heights the sight line is the same 3-D segment both ways, and the block rule
        // (top > min(z(TIn), z(TOut)) over the same supercover cells) is direction-free. 20,000 random lines, 400 fields.
        int asym = 0; string first = "";
        for (int i = 0; i < 20_000; i++)
        {
            var g = new TerrainGrid(12, 12);
            for (int y = 0; y < 12; y++) for (int x = 0; x < 12; x++) g.Set(x, y, TerrainClass.Hills, R(900 + (ulong)(i / 50), x, y, 3000));
            var a = new Vec2L(R(77, i, 0, 48_000), R(77, i, 1, 48_000));
            var b = new Vec2L(R(77, i, 2, 48_000), R(77, i, 3, 48_000));
            bool f = Los(g, a, b).Visible, rv = Los(g, b, a).Visible;
            if (f != rv) { if (asym == 0) first = $"i={i} a={a.X},{a.Y} b={b.X},{b.Y} fwd={f}"; asym++; }
        }
        Assert.True(asym == 0, $"asymmetric {asym}/20000; first {first}");
    }

    // ---------------- C-V-2 / V3: corner-side cells are height-tested ----------------

    [Fact]
    public void Los_corner_side_tall_cell_blocks_by_height()
    {
        // Diagonal (1,1) centre (6000,6000) -> (2,2) centre (10000,10000) passes exactly through the corner (8000,8000):
        // the corner rule visits the side cells (2,1) and (1,2). (2,1) is a 50 m hill, the line is at 1700 there => Height.
        var g = new TerrainGrid(4, 4);
        g.Set(2, 1, TerrainClass.Hills, 50_000);
        var r = Los(g, g.CellCenter(1, 1), g.CellCenter(2, 2));
        Assert.Equal((false, LosBlock.Height, new CellCoord(2, 1)), (r.Visible, r.Block, r.BlockCell));
        // Lowered to the line height (1700) the corner-side cell no longer blocks (top > line is required).
        g.Set(2, 1, TerrainClass.Hills, 1_700);
        Assert.True(Los(g, g.CellCenter(1, 1), g.CellCenter(2, 2)).Visible);
    }

    // ---------------- C-V-2 / V5: the aim cell is excluded from the direct-fire height test ----------------

    [Fact]
    public void Direct_fire_at_a_target_standing_on_a_hill_is_not_blocked_by_its_own_cell()
    {
        // Shooter at cell 0 centre (2000, 2000), muzzle 0 + 1500; aim at cell 7 centre (30000, 2000) on a 50 m hill, aim
        // height 50000 + 1500 = 51500. Line enters cell 7 at x = 28000: 1500 + floor(50000*26000/28000) = 47,928 < 50,000,
        // so the aim cell WOULD block if it were tested; it is excluded (the target stands on it) => Hit on disc 9.
        var g = new TerrainGrid(10, 1);
        g.Set(7, 0, TerrainClass.Hills, 50_000);
        var target = new[] { new Disc(9, g.CellCenter(7, 0), 300) };
        var r = Projectiles.DirectFire(g, g.CellCenter(0, 0), 1500, g.CellCenter(7, 0), 1500, target, 1);
        Assert.Equal((FireOutcome.Hit, 9L), (r.Outcome, r.EntityId));
        // The same hill one cell earlier (cell 6, not the aim cell) blocks: there the line is at most
        // 1500 + floor(50000*26000/28000) = 47,928 < 50,000.
        g.Set(7, 0, TerrainClass.Open, 0); g.Set(6, 0, TerrainClass.Hills, 50_000);
        var b = Projectiles.DirectFire(g, g.CellCenter(0, 0), 1500, g.CellCenter(7, 0), 51_500, target, 1);
        Assert.Equal((FireOutcome.Blocked, LosBlock.Height, new CellCoord(6, 0)), (b.Outcome, b.Block, b.BlockCell));
    }

    // ---------------- C-V-2 / V2: ballistic z == ground is an impact ----------------

    [Fact]
    public void Ballistic_z_equal_to_ground_is_an_impact()
    {
        // z(540) = 1500 + floor(-9807*291600/2e6) = 1500 - 1430 = 70; z(550) = 1500 - 1484 = 16 (pinned by the flat-ground test).
        // Ground raised to exactly 16 from x = 56000 (cells 14..29): the step 540 -> 550 (x 56000 -> 57000) ends at z = 16 = ground
        // => Ground at (57000, 6000), t = 550. One mm lower (15) and the shell is still above at 550 => impact at 560.
        var g = new TerrainGrid(30, 3);
        for (int x = 14; x < 30; x++) for (int y = 0; y < 3; y++) g.Set(x, y, TerrainClass.Hills, 16);
        var r = Projectiles.Fly(g, Shot, 10, Array.Empty<Disc>(), 0);
        Assert.Equal(new BallisticResult(BallisticOutcome.Ground, 0, new(57_000, 6_000), 16, 550, 55), r);
        for (int x = 14; x < 30; x++) for (int y = 0; y < 3; y++) g.Set(x, y, TerrainClass.Hills, 15);
        Assert.Equal(560, Projectiles.Fly(g, Shot, 10, Array.Empty<Disc>(), 0).ImpactTimeMs);
    }

    // ---------------- C-V-3 / V1: A* equal-g parent tie-break (lower parent index) ----------------

    [Fact]
    public void Astar_equal_g_from_a_later_expanded_lower_index_parent_replaces_the_parent()
    {
        // 3 x 3, all Open except a Road at (0,0) (so the heuristic scale m = 700), search (2,2) idx 8 -> (0,1) idx 3.
        // Two optimal paths, both g = orth 4,000,000 + diag 5,656,000 = 9,656,000:
        //   via (1,2) idx 7: f = 4,000,000 + h(diag 700*4000*1414/1000 = 3,959,200) = 7,959,200 -> expanded FIRST, offers the goal;
        //   via (1,1) idx 4: f = 5,656,000 + h(orth 700*4000 = 2,800,000) = 8,456,000 -> expanded second (before the goal,
        //   f 9,656,000), offers the goal the SAME g from parent 4 < 7 => the parent becomes 4 (canonical rule).
        // Expected path 8 -> 4 -> 3. Without the rule (mutant V1) the first offer would stick: 8 -> 7 -> 3.
        var g = new TerrainGrid(3, 3);
        g.SetClass(0, 0, TerrainClass.Road);
        var pf = new GridPathfinder(g, Costs);
        Assert.Equal(700, pf.HeuristicCostScale);
        var r = pf.FindPath(new(2, 2), new(0, 1));
        Assert.Equal(9_656_000L, r.Cost);
        Assert.Equal(new[] { 8, 4, 3 }, r.Cells);
        // Neighbour-order independence holds for this canonical choice too.
        for (ulong p = 0; p < 8; p++)
            Assert.Equal(new[] { 8, 4, 3 }, new GridPathfinder(g, Costs, TestSupport.Permutation(8, 3000 + p)).FindPath(new(2, 2), new(0, 1)).Cells);
    }

    // ---------------- C-V-4: the ballistic chord is height-tested in every cell it crosses ----------------

    [Fact]
    public void Ballistic_shell_cannot_pass_through_a_raised_cell_between_samples()
    {
        // 100 m hill at cell (5,1), x in [20000, 24000). x(t) = 2000 + 100 t reaches 20000 at t = 180.
        // Step 10: the step 170 -> 180 ends at x = 20000 in the hill cell: z(180) = 1500 + floor(-9807*32400/2e6) = 1341 <= 100000.
        // Step 300: one step 0 -> 300, chord (2000, 1500) -> (32000, z(300) = 1500 - 442 = 1058), D = 30000; it ENTERS the hill
        // at n = 18000: Z = 1500 + floor(-442*18000/30000) = 1500 - 266 = 1234 <= 100000 => Ground at the face, t = floor(300*0.6) = 180.
        // Before the fix the 300 ms step jumped to x = 32000 and flew on to t = 600 (verify-C C-V-4).
        var g = new TerrainGrid(30, 3);
        g.Set(5, 1, TerrainClass.Hills, 100_000);
        var r10 = Projectiles.Fly(g, Shot, 10, Array.Empty<Disc>(), 0);
        var r300 = Projectiles.Fly(g, Shot, 300, Array.Empty<Disc>(), 0);
        Assert.Equal(new BallisticResult(BallisticOutcome.Ground, 0, new(20_000, 6_000), 1_341, 180, 18), r10);
        Assert.Equal(new BallisticResult(BallisticOutcome.Ground, 0, new(20_000, 6_000), 1_234, 180, 1), r300);
    }

    [Fact]
    public void Ballistic_face_entry_threshold_and_inside_cell_descent_are_exact()
    {
        // Same 300 ms chord. Hill top EXACTLY at the entry height 1234: Z(TIn) <= top => struck at the face (x 20000, t 180).
        var g = new TerrainGrid(30, 3);
        g.Set(5, 1, TerrainClass.Hills, 1_234);
        Assert.Equal(new BallisticResult(BallisticOutcome.Ground, 0, new(20_000, 6_000), 1_234, 180, 1), Projectiles.Fly(g, Shot, 300, Array.Empty<Disc>(), 0));
        // One mm lower (1233): the chord clears the face but leaves the cell at x = 24000 (n = 22000) with
        // Z = 1500 + floor(-442*22000/30000) = 1500 - 325 = 1175 <= 1233 => impact at the exit, t = floor(300*22/30) = 220.
        g.Set(5, 1, TerrainClass.Hills, 1_233);
        Assert.Equal(new BallisticResult(BallisticOutcome.Ground, 0, new(24_000, 6_000), 1_175, 220, 1), Projectiles.Fly(g, Shot, 300, Array.Empty<Disc>(), 0));
        // Lower than the exit height (1174): the shell clears the cell and comes down on flat ground in the NEXT step, 300 -> 600:
        // chord (32000, z(300) = 1058) -> (62000, z(600) = 1500 + floor(-9807*360000/2e6) = -266), D = 30000. Cell 13
        // [52000, 56000): enters at n = 20000 with 1058 + floor(-1324*20000/30000) = 175 > 0, exits at n = 24000 with
        // 1058 + floor(-1324*24000/30000) = -2 <= 0 => Ground at the cell exit (56000, 6000), z = -2, t = 300 + 240 = 540, step 2
        // (the old rule only looked at the 600 ms sample).
        g.Set(5, 1, TerrainClass.Hills, 1_174);
        Assert.Equal(new BallisticResult(BallisticOutcome.Ground, 0, new(56_000, 6_000), -2, 540, 2), Projectiles.Fly(g, Shot, 300, Array.Empty<Disc>(), 0));
    }

    // ---------------- C-V-5: an entity touching a wall face is hit (same rule as direct fire) ----------------

    [Fact]
    public void Ballistic_entity_touching_a_wall_face_is_hit_like_direct_fire()
    {
        // Wall at cell (10,1), face x = 40000, reached on the step 370 -> 380 at its end (n = D). Disc (40300, r 300): entry
        // exactly at x = 40000, t = 1 of the step = the wall entry => HIT (tie goes to the entity, as in DirectFire).
        // z at the impact = z(380) = 1500 + floor(-9807*144400/2e6) = 1500 - 709 = 791.
        var g = new TerrainGrid(30, 3);
        g.SetClass(10, 1, TerrainClass.Wall);
        var onFace = new[] { new Disc(4, new(40_300, 6_000), 300) };
        var b = Projectiles.Fly(g, Shot, 10, onFace, 0);
        Assert.Equal(new BallisticResult(BallisticOutcome.HitEntity, 4, new(40_000, 6_000), 791, 380, 38), b);
        Assert.Equal(FireOutcome.Hit, Projectiles.DirectFire(g, new(2_000, 6_000), 1500, new(60_000, 6_000), 1500, onFace, 0).Outcome);
        // One mm further the wall is entered strictly first, in both.
        var behind = new[] { new Disc(4, new(40_301, 6_000), 300) };
        Assert.Equal(BallisticOutcome.Wall, Projectiles.Fly(g, Shot, 10, behind, 0).Outcome);
        Assert.Equal(FireOutcome.Blocked, Projectiles.DirectFire(g, new(2_000, 6_000), 1500, new(60_000, 6_000), 1500, behind, 0).Outcome);
    }

    // ---------------- C-V-6 / C-V-9: Fly input bounds ----------------

    [Fact]
    public async Task Ballistic_rejects_shots_not_guaranteed_down_within_the_flight_bound()
    {
        var g = new TerrainGrid(30, 3);
        // g = 1 mm/s^2, vz = 10^7: z(3.6e6) = 1500 + (7.2e16 - 1.296e13)/2e6 = 1500 + 35,993,520,000 > -10^9 => rejected
        // up front (it used to run ~2e10 steps).
        // Run on a worker with a 10 s budget so that a missing bound FAILS this test instead of hanging the suite.
        var lowG = new BallisticShot(new(2_000, 6_000), 1_500, 0, 0, 10_000_000);
        var task = Task.Run(() => Record.Exception(() => Projectiles.Fly(g, lowG, 1, Array.Empty<Disc>(), 0, 1)));
        bool done = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))) == task;
        Assert.True(done, "Fly did not return within 10 s for g = 1, vz = 1e7, step 1 ms");
        Assert.IsType<ArgumentOutOfRangeException>(await task);
        // Default gravity, vz at the velocity bound: z(3.6e6) = 1500 + floor((7.2e16 - 1.271e17)/2e6) << -10^9 => accepted.
        // Analytic flight time on flat 0 ground: 2 vz / g = 2*1e7/9807 s = 2,039.36 s; the 1 s step gives the first sample at or
        // past it with z <= 0, i.e. t = 2,040,000 ms, so Steps = 2040 <= MaxFlightMs / step.
        var vertical = new BallisticShot(new(2_000, 6_000), 1_500, 0, 0, 10_000_000);
        var r = Projectiles.Fly(g, vertical, 1000, Array.Empty<Disc>(), 0);
        Assert.Equal((BallisticOutcome.Ground, 2_040_000L, 2_040L), (r.Outcome, r.ImpactTimeMs, r.Steps));
        Assert.True(r.ImpactTimeMs <= Projectiles.MaxFlightMs);
    }

    [Fact]
    public void Ballistic_launch_height_is_range_checked()
    {
        var g = new TerrainGrid(30, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => Projectiles.Fly(g, new BallisticShot(new(2_000, 6_000), long.MaxValue - 10, 0, 0, 10_000), 10, Array.Empty<Disc>(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Projectiles.Fly(g, new BallisticShot(new(2_000, 6_000), BallisticShot.MaxAbsZ0Mm + 1, 0, 0, 0), 10, Array.Empty<Disc>(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Projectiles.Fly(g, new BallisticShot(new(2_000, 6_000), -BallisticShot.MaxAbsZ0Mm - 1, 0, 0, 0), 10, Array.Empty<Disc>(), 0));
        // At the bound it is accepted; at -MaxAbsZ0 it starts below the 0 ground => Ground at t = 0 (documented origin rule).
        Assert.Equal(BallisticOutcome.Ground, Projectiles.Fly(g, new BallisticShot(new(2_000, 6_000), BallisticShot.MaxAbsZ0Mm, 0, 0, 0), 1000, Array.Empty<Disc>(), 0).Outcome);
        var lo = Projectiles.Fly(g, new BallisticShot(new(2_000, 6_000), -BallisticShot.MaxAbsZ0Mm, 0, 0, 0), 10, Array.Empty<Disc>(), 0);
        Assert.Equal((BallisticOutcome.Ground, 0L), (lo.Outcome, lo.ImpactTimeMs));
    }

    // ---------------- C-V-8: grid accessors do not alias off-grid cells ----------------

    [Fact]
    public void Grid_accessors_throw_for_off_grid_cells()
    {
        var g = new TerrainGrid(10, 2);
        g.SetClass(0, 1, TerrainClass.Wall);                     // index 10 = (10, 0) under the old unchecked formula
        Assert.Throws<ArgumentOutOfRangeException>(() => g.ClassAt(10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => g.HeightAt(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => g.Index(0, 2));
        Assert.Equal(TerrainClass.Wall, g.ClassAt(0, 1));
        Assert.Equal(19, g.Index(9, 1));
    }

    // ---------------- C-V-7: what the separation binary search guarantees (documentation corrected) ----------------

    [Fact]
    public void Separation_returns_a_valid_boundary_k_with_the_next_k_invalid()
    {
        // Two movers, the second stationary. For a truncated move the result is q(k) with the segment Pos->q(k) clear of the
        // other disc's open interior and Pos->q(k+1) NOT clear (the boundary the binary search keeps) — not necessarily the
        // largest clear k (the predicate is not monotone under floor rounding).
        int truncated = 0;
        for (int i = 0; i < 4000; i++)
        {
            long ra = 1 + R(9, i, 0, 6), rb = 1 + R(9, i, 1, 6);
            var pa = new Vec2L(R(9, i, 2, 41) - 20, R(9, i, 3, 41) - 20);
            var pb = new Vec2L(R(9, i, 4, 41) - 20, R(9, i, 5, 41) - 20);
            if (Separation.Overlaps(pa, ra, pb, rb)) continue;
            var want = new Vec2L(R(9, i, 6, 61) - 30, R(9, i, 7, 61) - 30);
            var res = Separation.Step(new[] { new Mover(1, pa, ra, want), new Mover(2, pb, rb, pb) });
            Vec2L d = want - pa; long K = Math.Max(Math.Abs(d.X), Math.Abs(d.Y));
            Assert.False(Separation.Overlaps(res[0].Pos, ra, pb, rb));
            if (!res[0].Truncated) continue;
            truncated++;
            long k = -1;                                      // q(j) is monotone, so equal points form one run; lo is its last j
            for (long j = 0; j < K; j++)
                if (new Vec2L(pa.X + FixedMath.MulDivFloor(d.X, j, K), pa.Y + FixedMath.MulDivFloor(d.Y, j, K)) == res[0].Pos) k = j;
            Assert.True(k >= 0);
            Vec2L q = res[0].Pos, q1 = new(pa.X + FixedMath.MulDivFloor(d.X, k + 1, K), pa.Y + FixedMath.MulDivFloor(d.Y, k + 1, K));
            Assert.True(q == pa || !Separation.SegmentEntersOpenDisc(pa, q, pb, ra + rb));
            Assert.True(q1 != pa && Separation.SegmentEntersOpenDisc(pa, q1, pb, ra + rb));
        }
        Assert.True(truncated > 100, $"only {truncated} truncated cases");
    }
}
