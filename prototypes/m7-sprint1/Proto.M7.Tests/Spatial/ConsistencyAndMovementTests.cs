using Proto.M7.Spatial;
using static Proto.M7.Tests.Spatial.SpatialTestSupport;

namespace Proto.M7.Tests.Spatial;

/// <summary>Spec §3: grid&lt;-&gt;continuous consistency proof test + terrain-aware movement time.</summary>
public class ConsistencyAndMovementTests
{
    /// <summary>
    /// THE consistency property: every segment between consecutive A* waypoints (cell centres) touches ONLY passable cells
    /// under the supercover definition — continuous motion never enters a cell the grid planner considers impassable.
    /// Proof sketch (the test checks it exhaustively on many random grids): an orthogonal centre step runs along a cell
    /// centre line, never on a grid line, so it touches exactly its two (passable) endpoint cells; a diagonal centre step
    /// passes exactly through the shared corner, so by the corner rule it touches the two endpoint cells plus both side
    /// cells — which the no-corner-cutting rule guarantees are passable.
    /// </summary>
    [Fact]
    public void Every_path_segment_touches_only_passable_cells()
    {
        int segments = 0, diagonals = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var g = RandomGrid(24, 20, seed + 500, 22);
            var pf = new GridPathfinder(g, Costs);
            for (int q = 0; q < 5; q++)
            {
                var r = pf.FindPath(new(R(seed, q, 1, 24), R(seed, q, 2, 20)), new(R(seed, q, 3, 24), R(seed, q, 4, 20)));
                if (!r.Found) continue;
                var wp = PathMovement.Waypoints(g, r.Cells);
                for (int i = 1; i < wp.Length; i++)
                {
                    foreach (var c in Supercover.Cells(g.CellSizeMm, wp[i - 1], wp[i]))
                        Assert.True(g.IsPassable(Costs, c.X, c.Y), $"seed {seed}: segment {wp[i - 1]}->{wp[i]} touches blocked cell ({c.X},{c.Y})");
                    Assert.True(Supercover.SegmentPassable(g, Costs, wp[i - 1], wp[i]));
                    segments++;
                    if (wp[i].X != wp[i - 1].X && wp[i].Y != wp[i - 1].Y) diagonals++;
                }
            }
        }
        Assert.True(segments > 1000 && diagonals > 200, $"{segments} segments, {diagonals} diagonal");
    }

    [Fact]
    public void Negative_control_a_corner_cut_would_be_caught_by_the_supercover()
    {
        // . W      the diagonal centre segment (0,0)->(1,1) passes through the corner shared with the two walls; the corner
        // W .      rule makes it touch them, so a planner that cut corners would FAIL the consistency property above.
        var g = new TerrainGrid(2, 2);
        g.SetClass(1, 0, TerrainClass.Wall); g.SetClass(0, 1, TerrainClass.Wall);
        Assert.False(Supercover.SegmentPassable(g, Costs, g.CellCenter(0, 0), g.CellCenter(1, 1)));
        // ...and one wall is enough.
        g.SetClass(0, 1, TerrainClass.Open);
        Assert.False(Supercover.SegmentPassable(g, Costs, g.CellCenter(0, 0), g.CellCenter(1, 1)));
    }

    [Fact]
    public void Movement_time_matches_planner_step_cost_exactly_on_the_default_cell()
    {
        // S = 4000: orthogonal L = 4000 = S, diagonal L = floor(4000*sqrt2) = 5656 = 4000*1414/1000 => time == ceil(stepCost/speed).
        long speed = 1_300;   // mm/s (test input)
        int segs = 0;
        for (ulong seed = 1; seed <= 25; seed++)
        {
            var g = RandomGrid(20, 20, seed + 900, 15);
            var pf = new GridPathfinder(g, Costs);
            var r = pf.FindPath(new(R(seed, 0, 1, 20), R(seed, 0, 2, 20)), new(R(seed, 0, 3, 20), R(seed, 0, 4, 20)));
            if (!r.Found) continue;
            var wp = PathMovement.Waypoints(g, r.Cells);
            long total = 0;
            for (int i = 1; i < wp.Length; i++)
            {
                long stepCost = PathfinderTests.PathCost(g, new[] { r.Cells[i - 1], r.Cells[i] });
                long expected = (stepCost + speed - 1) / speed;
                Assert.Equal(expected, PathMovement.SegmentTimeMs(g, Costs, wp[i - 1], wp[i], speed));
                total += expected; segs++;
            }
            Assert.Equal(total, PathMovement.PolylineTimeMs(g, Costs, wp, speed));
        }
        Assert.True(segs > 50);
    }

    [Fact]
    public void Movement_time_examples_and_terrain_ordering()
    {
        var g = new TerrainGrid(4, 4);
        long v = 1000;                                                            // 1 m/s on cost-1000 ground
        Assert.Equal(4000, PathMovement.SegmentTimeMs(g, Costs, g.CellCenter(0, 0), g.CellCenter(1, 0), v));
        Assert.Equal(5656, PathMovement.SegmentTimeMs(g, Costs, g.CellCenter(0, 0), g.CellCenter(1, 1), v));
        g.SetClass(1, 0, TerrainClass.Road);
        Assert.Equal(3400, PathMovement.SegmentTimeMs(g, Costs, g.CellCenter(0, 0), g.CellCenter(1, 0), v));   // half open, half road
        // Same segment, varying the terrain of both cells: road < open < jungle < mountain in TIME, as the Director orders.
        long T(TerrainClass k) { var h = new TerrainGrid(2, 1, fill: k); return PathMovement.SegmentTimeMs(h, Costs, h.CellCenter(0, 0), h.CellCenter(1, 0), v); }
        Assert.True(T(TerrainClass.Road) < T(TerrainClass.Open));
        Assert.True(T(TerrainClass.Open) < T(TerrainClass.Jungle));
        Assert.True(T(TerrainClass.Jungle) < T(TerrainClass.Mountain));
        Assert.Equal(16000, T(TerrainClass.Mountain));
        // Ceil: 1 mm of open ground at 3 mm/s = 1/3 s -> 334 ms.
        Assert.Equal(334, PathMovement.SegmentTimeMs(g, Costs, new(100, 100), new(101, 100), 3));
        // Through a wall: refused (callers must move only along passable segments).
        g.SetClass(2, 0, TerrainClass.Wall);
        Assert.Throws<InvalidOperationException>(() => PathMovement.SegmentTimeMs(g, Costs, g.CellCenter(1, 0), g.CellCenter(3, 0), v));
        // Unequal split: from x=1000 to x=7000 on row 0 with cell 1 = road: 3000 mm open + 3000 mm road = 3000 + 2100.
        Assert.Equal(5100, PathMovement.SegmentTimeMs(g, Costs, new(1000, 2000), new(7000, 2000), v));
    }

    [Fact]
    public void Position_interpolation_is_exact_at_the_ends()
    {
        Vec2L a = new(-3, 7), b = new(10_000, -2_000);
        Assert.Equal(a, PathMovement.PositionAt(a, b, 0, 777));
        Assert.Equal(b, PathMovement.PositionAt(a, b, 777, 777));
        Assert.Equal(b, PathMovement.PositionAt(a, b, 9999, 777));
        Assert.Equal(new Vec2L(-3 + 5001, 7 - 1004), PathMovement.PositionAt(a, b, 1, 2));   // floor((10003)/2), floor(-2007/2)
    }
}
