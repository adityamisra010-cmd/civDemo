using Proto.M7.Spatial;
using static Proto.M7.Tests.Spatial.SpatialTestSupport;

namespace Proto.M7.Tests.Spatial;

/// <summary>Spec §3: ballistic arc (integer kinematics) + collision/separation by ascending id.</summary>
public class BallisticAndSeparationTests
{
    // Origin (2000, 6000), z0 = 1500 mm, vx = 100 m/s, flat ground 0, step 10 ms, g = 9807 mm/s^2.
    // Hand derivation: z(t) = 1500 + floor(-9807 t^2 / 2e6) <= 0  <=>  t >= 553.09 ms  =>  first sample t = 560 ms
    // (z(550) = 1500 - 1484 = 16 > 0; z(560) = 1500 - 1538 = -38 <= 0); x(560) = 2000 + 56000 = 58000.
    private static readonly BallisticShot Shot = new(new(2_000, 6_000), 1_500, 100_000, 0, 0);

    [Fact]
    public void Flat_ground_impact_matches_hand_derivation()
    {
        var g = new TerrainGrid(30, 3);
        var r = Projectiles.Fly(g, Shot, 10, Array.Empty<Disc>(), 0);
        Assert.Equal(new BallisticResult(BallisticOutcome.Ground, 0, new(58_000, 6_000), -38, 560, 56), r);
        Assert.Equal((new Vec2L(57_000, 6_000), 16L), Projectiles.ArcAt(Shot, 550, Projectiles.DefaultGravityMmPerS2));
    }

    [Fact]
    public void Entity_is_hit_only_on_the_impact_step()
    {
        var g = new TerrainGrid(30, 3);
        // On the impact step segment (57000 -> 58000): entry at x = 57200 -> t = 0.2 of the step -> 552 ms.
        var hit = Projectiles.Fly(g, Shot, 10, new[] { new Disc(4, new(57_500, 6_000), 300) }, 0);
        Assert.Equal(BallisticOutcome.HitEntity, hit.Outcome);
        Assert.Equal(4, hit.EntityId);
        Assert.Equal(new Vec2L(57_200, 6_000), hit.Impact);
        Assert.Equal(552, hit.ImpactTimeMs);
        // Mid-flight the shell is overhead: an entity at x = 30000 is not hit.
        Assert.Equal(BallisticOutcome.Ground, Projectiles.Fly(g, Shot, 10, new[] { new Disc(4, new(30_000, 6_000), 300) }, 0).Outcome);
        // Tie at identical t on the impact step: lower id.
        var two = new[] { new Disc(9, new(57_500, 6_200), 300), new Disc(8, new(57_500, 5_800), 300) };
        Assert.Equal(8, Projectiles.Fly(g, Shot, 10, two, 0).EntityId);
        Assert.Equal(8, Projectiles.Fly(g, Shot, 10, two.Reverse().ToArray(), 0).EntityId);
    }

    [Fact]
    public void Wall_hill_and_off_grid_stop_the_arc()
    {
        var w = new TerrainGrid(30, 3);
        w.SetClass(10, 1, TerrainClass.Wall);                                      // x in [40000, 44000)
        var rw = Projectiles.Fly(w, Shot, 10, Array.Empty<Disc>(), 0);
        Assert.Equal(BallisticOutcome.Wall, rw.Outcome);
        Assert.Equal(new Vec2L(40_000, 6_000), rw.Impact);
        Assert.Equal(380, rw.ImpactTimeMs);                                        // x(t) = 2000 + 100 t = 40000 at t = 380
        // Hill: cells x >= 8 raised to 500 mm => z(450) = 507 > 500, z(460) = 462 <= 500 => ground at 460 ms, x = 48000.
        var h = new TerrainGrid(30, 3);
        for (int x = 8; x < 30; x++) for (int y = 0; y < 3; y++) h.Set(x, y, TerrainClass.Hills, 500);
        var rh = Projectiles.Fly(h, Shot, 10, Array.Empty<Disc>(), 0);
        Assert.Equal((BallisticOutcome.Ground, new Vec2L(48_000, 6_000), 460L), (rh.Outcome, rh.Impact, rh.ImpactTimeMs));
        // Grid only 10 cells wide: leaves the field at x = 40000 (t = 380).
        var o = Projectiles.Fly(new TerrainGrid(10, 3), Shot, 10, Array.Empty<Disc>(), 0);
        Assert.Equal((BallisticOutcome.OffGrid, new Vec2L(40_000, 6_000), 380L), (o.Outcome, o.Impact, o.ImpactTimeMs));
    }

    [Fact]
    public void Arc_samples_are_closed_form_and_independent_of_step_size()
    {
        var s = new BallisticShot(new(1_000, 1_000), 2_000, 37_123, -5_001, 41_999);
        for (long t = 0; t <= 9_000; t += 10)
            Assert.Equal(Projectiles.ArcAt(s, t, 9_807), Projectiles.ArcAt(s, t, 9_807));
        // A lob over a wall lands beyond it (high vz): wall in cell 3 of row 0, landing far past it.
        var g = new TerrainGrid(200, 1);
        g.SetClass(3, 0, TerrainClass.Wall);
        var lob = new BallisticShot(new(2_000, 2_000), 1_500, 30_000, 0, 30_000);
        Assert.Equal(BallisticOutcome.Wall, Projectiles.Fly(g, lob, 10, Array.Empty<Disc>(), 0).Outcome);  // walls are infinitely tall (documented)
        g.SetClass(3, 0, TerrainClass.Open);
        var land = Projectiles.Fly(g, lob, 10, Array.Empty<Disc>(), 0);
        var land5 = Projectiles.Fly(g, lob, 5, Array.Empty<Disc>(), 0);
        Assert.Equal(BallisticOutcome.Ground, land.Outcome);
        Assert.True(land5.ImpactTimeMs <= land.ImpactTimeMs && land.ImpactTimeMs - land5.ImpactTimeMs < 10);  // finer step finds impact no later
    }

    // ---------------- separation ----------------

    [Fact]
    public void Head_on_pair_resolves_by_ascending_id()
    {
        var a = new Mover(1, new(0, 0), 100, new(1_000, 0));
        var b = new Mover(2, new(2_000, 0), 100, new(1_000, 0));
        var r = Separation.Step(new[] { b, a });
        Assert.Equal(new MoverResult(1, new(1_000, 0), false), r[0]);          // id 1 moves first, unobstructed
        Assert.Equal(new MoverResult(2, new(1_200, 0), true), r[1]);           // id 2 stops exactly touching (distance 200)
        // Swap the ids: the other one wins.
        var r2 = Separation.Step(new[] { a with { Id = 2 }, b with { Id = 1 } });
        Assert.Equal(new Vec2L(1_000, 0), r2[0].Pos);
        Assert.Equal(new Vec2L(800, 0), r2[1].Pos);
    }

    [Fact]
    public void No_tunnelling_through_a_small_disc_and_stationary_discs_are_obstacles()
    {
        // A fast mover would land beyond a small stationary disc; it must stop before entering it.
        var m = new Mover(5, new(0, 0), 50, new(10_000, 0));
        var s = new Mover(9, new(5_000, 0), 10, new(5_000, 0));                // higher id, not yet moved: still an obstacle
        var r = Separation.Step(new[] { m, s });
        Assert.Equal(new Vec2L(4_940, 0), r[0].Pos);
        Assert.True(r[0].Truncated);
        Assert.Equal(new Vec2L(5_000, 0), r[1].Pos);
    }

    [Fact]
    public void Movers_do_not_enter_impassable_cells()
    {
        var g = new TerrainGrid(5, 1);
        g.SetClass(3, 0, TerrainClass.Wall);                                     // x in [12000, 16000)
        var r = Separation.Step(new[] { new Mover(1, new(2_000, 2_000), 300, new(18_000, 2_000)) }, g, Costs);
        Assert.Equal(new Vec2L(11_999, 2_000), r[0].Pos);                        // last mm before the wall cell
    }

    [Fact]
    public void Invalid_inputs_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => Separation.Step(new[] { new Mover(1, new(0, 0), 100, new(0, 0)), new Mover(1, new(1_000, 0), 100, new(0, 0)) }));
        Assert.Throws<ArgumentException>(() => Separation.Step(new[] { new Mover(1, new(0, 0), 100, new(0, 0)), new Mover(2, new(199, 0), 100, new(0, 0)) }));
        // Exactly touching is not an overlap.
        Assert.Equal(2, Separation.Step(new[] { new Mover(1, new(0, 0), 100, new(0, 0)), new Mover(2, new(200, 0), 100, new(0, 0)) }).Length);
    }

    [Fact]
    public void Crowded_scenario_never_overlaps_and_is_independent_of_input_order()
    {
        // 100 discs (radius 300-499 mm) on a 10 x 10 lattice with 1100 mm spacing, all converging on one point; 40 steps,
        // each step at most 600 mm toward the goal. Three input permutations must agree step by step, and no pair may overlap.
        const int n = 100;
        var start = new Mover[n];
        for (int i = 0; i < n; i++)
            start[i] = new Mover(1000 + (long)TestSupport.Permutation(n, 5)[i], new(i % 10 * 1_100, i / 10 * 1_100), 300 + R(41, i, 0, 200), default);
        Vec2L goal = new(5_000, 5_000);
        Vec2L[]? reference = null;
        long initialSum = 0, finalSum = 0; int truncations = 0;
        foreach (var m in start) initialSum += Vec2L.Distance(m.Pos, goal);
        for (ulong perm = 0; perm < 3; perm++)
        {
            var cur = (Mover[])start.Clone();
            var trace = new List<Vec2L>();
            for (int step = 0; step < 40; step++)
            {
                var order = TestSupport.Permutation(n, 700 + perm * 31 + (ulong)step);
                var input = new Mover[n];
                for (int k = 0; k < n; k++)
                {
                    var m = cur[order[k]];
                    Vec2L d = goal - m.Pos;
                    long len = d.Length;
                    Vec2L want = len <= 600 ? goal : new(m.Pos.X + d.X * 600 / len, m.Pos.Y + d.Y * 600 / len);
                    input[k] = m with { Desired = want };
                }
                var res = Separation.Step(input);
                for (int k = 1; k < n; k++) Assert.True(res[k - 1].Id < res[k].Id);
                var byId = new Dictionary<long, Mover>();
                foreach (var m in cur) byId[m.Id] = m;
                for (int k = 0; k < n; k++) byId[res[k].Id] = byId[res[k].Id] with { Pos = res[k].Pos };
                cur = byId.Values.OrderBy(m => m.Id).ToArray();
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                        Assert.False(Separation.Overlaps(cur[i].Pos, cur[i].RadiusMm, cur[j].Pos, cur[j].RadiusMm), $"overlap after step {step}");
                foreach (var r in res) { trace.Add(r.Pos); if (perm == 0 && r.Truncated) truncations++; }
            }
            if (perm == 0) foreach (var m in cur) finalSum += Vec2L.Distance(m.Pos, goal);
            if (reference is null) reference = trace.ToArray();
            else Assert.Equal(reference, trace.ToArray());
        }
        // Not a vacuous pass: the crowd compressed (> 20% less total distance; it jams by design) and contention (truncation) actually happened a lot.
        Assert.True(finalSum * 5 < initialSum * 4, $"initial {initialSum} final {finalSum}");
        Assert.True(truncations > 500, $"truncations {truncations}");
    }
}
