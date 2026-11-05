using Proto.M7.Fixed;
using Proto.M7.Spatial;
using static Proto.M7.Tests.Spatial.SpatialTestSupport;

namespace Proto.M7.Tests.Spatial;

/// <summary>Spec §3: input tables + validator, integer A* (octile 1414/1000, no corner cutting, (f,h,index) key).</summary>
public class PathfinderTests
{
    [Fact]
    public void Placeholder_tables_validate_and_respect_the_Directors_ordering()
    {
        var c = Costs;
        Assert.True(c.Cost(TerrainClass.Road) < c.Cost(TerrainClass.Open));
        Assert.True(c.Cost(TerrainClass.Open) < c.Cost(TerrainClass.Jungle));
        Assert.True(c.Cost(TerrainClass.Jungle) < c.Cost(TerrainClass.Mountain));
        Assert.False(c.IsPassable(TerrainClass.Water));
        Assert.False(c.IsPassable(TerrainClass.Wall));
        Assert.Equal(700, c.Cost(TerrainClass.Road));                      // cited: roads.classes road_built 0.7
        Assert.Equal(700, c.MinPassableCost);
        _ = Conceal;
    }

    //                     Road Open Forest Jungle Hills Mountain Water Wall
    [Theory]
    [InlineData(new long[] { 1000, 1000, 1500, 2500, 1800, 4000, 0, 0, 3000, 3000, 0 }, "Road must be cheaper")]
    [InlineData(new long[] { 1100, 1000, 1500, 2500, 1800, 4000, 0, 0, 3000, 3000, 0 }, "Road must be cheaper")]
    [InlineData(new long[] { 700, 1000, 1500, 1000, 1800, 4000, 0, 0, 3000, 3000, 0 }, "Jungle must be dearer")]
    [InlineData(new long[] { 700, 1000, 1500, 2500, 1800, 2500, 0, 0, 3000, 3000, 0 }, "Mountain must be dearer")]
    [InlineData(new long[] { 700, 1000, 1500, 2500, 1800, 2400, 0, 0, 3000, 3000, 0 }, "Mountain must be dearer")]
    [InlineData(new long[] { 700, 1000, 1500, 2500, 1800, 4000, 5, 0, 3000, 3000, 0 }, "Water must be impassable")]
    [InlineData(new long[] { 700, 1000, 1500, 2500, 1800, 4000, 0, 5, 3000, 3000, 0 }, "Wall must be impassable")]
    [InlineData(new long[] { 700, 1000, 0, 2500, 1800, 4000, 0, 0, 3000, 3000, 0 }, "must be passable")]
    [InlineData(new long[] { 700, 1000, 1500, 2500, 1800, 4000, 0, 0 }, "expected 11")]   // [TR] 8 classes are now too few (11)
    [InlineData(new long[] { 700, 1000, 1500, 2500, 1800, 1_000_001, 0, 0, 3000, 3000, 0 }, "outside")]
    [InlineData(new long[] { -1, 1000, 1500, 2500, 1800, 4000, 0, 0, 3000, 3000, 0 }, "outside")]
    public void Cost_validator_rejects_violations(long[] table, string why)
    {
        var e = Assert.Throws<ArgumentException>(() => TerrainCostTable.Create(table));
        Assert.Contains(why, e.Message);
    }

    [Fact]
    public void Forest_and_hills_position_is_not_constrained()
    {
        // Only road<open<jungle<mountain is binding; Forest dearer than Mountain must still validate.
        Assert.Null(TerrainCostTable.Validate(new long[] { 700, 1000, 9000, 2500, 1, 4000, 0, 0, 3000, 3000, 0 }));   // [TR] + Marsh, Ford, Building
    }

    [Fact]
    public void Concealment_validator()
    {
        Q32 h = Q32.FromRatio(1, 2), o = Q32.One;
        // [TR] 11 classes (Marsh, Ford, Building appended).
        Assert.Contains("Road and Open", ConcealmentTable.Validate(new[] { h, o, h, h, o, o, o, o, o, o, Q32.Zero }));
        Assert.Contains("Forest and Jungle", ConcealmentTable.Validate(new[] { o, o, o, h, o, o, o, o, o, o, Q32.Zero }));
        Assert.Contains("not in [0,1]", ConcealmentTable.Validate(new[] { o, o, h, h, new Q32(Q32.OneRaw + 1), o, o, o, o, o, Q32.Zero }));
        Assert.Null(ConcealmentTable.Validate(new[] { o, o, h, h, o, o, o, Q32.Zero, o, o, Q32.Zero }));
        Assert.Contains("expected 11", ConcealmentTable.Validate(new[] { o, o, h, h, o, o, o, Q32.Zero }));
    }

    [Fact]
    public void Straight_open_corridor_cost_and_cells()
    {
        var g = new TerrainGrid(10, 1);
        var r = new GridPathfinder(g, Costs).FindPath(new(0, 0), new(9, 0));
        Assert.True(r.Found);
        Assert.Equal(9 * 4000L * 1000, r.Cost);
        Assert.Equal(Enumerable.Range(0, 10).ToArray(), r.Cells);
        var d = new TerrainGrid(5, 5);
        var rd = new GridPathfinder(d, Costs).FindPath(new(0, 0), new(4, 4));
        Assert.Equal(4 * (2000L * 4000 * 1414 / 2000), rd.Cost);                 // 4 diagonal steps of floor(2000*4000*1.414/2)
        Assert.Equal(5, rd.Cells.Length);
    }

    [Fact]
    public void Trivial_and_unreachable_cases()
    {
        var g = new TerrainGrid(6, 6);
        var pf = new GridPathfinder(g, Costs);
        var same = pf.FindPath(new(2, 2), new(2, 2));
        Assert.True(same.Found); Assert.Equal(0, same.Cost); Assert.Equal(new[] { g.Index(2, 2) }, same.Cells);
        for (int y = 0; y < 6; y++) g.SetClass(3, y, TerrainClass.Water);
        pf.RefreshCosts();
        Assert.False(pf.FindPath(new(0, 0), new(5, 5)).Found);
        Assert.False(pf.FindPath(new(0, 0), new(3, 3)).Found);                   // impassable goal
        Assert.False(pf.FindPath(new(0, 0), new(9, 9)).Found);                   // off-grid goal
    }

    [Fact]
    public void No_corner_cutting_between_diagonal_walls()
    {
        // . W
        // W .   the only connection (0,0)-(1,1) is a corner cut => no path.
        var g = new TerrainGrid(2, 2);
        g.SetClass(1, 0, TerrainClass.Wall); g.SetClass(0, 1, TerrainClass.Wall);
        Assert.False(new GridPathfinder(g, Costs).FindPath(new(0, 0), new(1, 1)).Found);
        // With one side open the path must go round it orthogonally (3 cells, two orthogonal steps).
        g.SetClass(0, 1, TerrainClass.Open);
        var r = new GridPathfinder(g, Costs).FindPath(new(0, 0), new(1, 1));
        Assert.True(r.Found);
        Assert.Equal(new[] { g.Index(0, 0), g.Index(0, 1), g.Index(1, 1) }, r.Cells);
        Assert.Equal(2 * 4000L * 1000, r.Cost);
    }

    [Fact]
    public void Road_is_preferred_when_cheaper_and_mountains_are_avoided()
    {
        // 11 x 3: row 0 road, row 1 open (start/goal), row 2 mountain.
        var g = new TerrainGrid(11, 3);
        for (int x = 0; x < 11; x++) { g.SetClass(x, 0, TerrainClass.Road); g.SetClass(x, 2, TerrainClass.Mountain); }
        var r = new GridPathfinder(g, Costs).FindPath(new(0, 1), new(10, 1));
        Assert.True(r.Found);
        Assert.Contains(r.Cells, c => g.IndexY(c) == 0);
        Assert.DoesNotContain(r.Cells, c => g.IndexY(c) == 2);
        Assert.True(r.Cost < 10 * 4000L * 1000);                                // cheaper than the straight open line
    }

    [Fact]
    public void Astar_is_optimal_against_independent_Dijkstra_and_paths_are_legal()
    {
        int checkedPaths = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var g = RandomGrid(18, 16, seed, 18);
            var pf = new GridPathfinder(g, Costs);
            for (int q = 0; q < 4; q++)
            {
                var s = new CellCoord(R(seed, q, 100, 18), R(seed, q, 101, 16));
                var t = new CellCoord(R(seed, q, 102, 18), R(seed, q, 103, 16));
                var r = pf.FindPath(s, t);
                long dj = DijkstraCost(g, Costs, s, t);
                if (dj < 0) { Assert.False(r.Found); continue; }
                Assert.True(r.Found);
                Assert.Equal(dj, r.Cost);
                Assert.True(pf.Heuristic((int)s.X, (int)s.Y, (int)t.X, (int)t.Y) <= r.Cost);      // admissible at the start
                Assert.Equal(PathCost(g, r.Cells), r.Cost);
                checkedPaths++;
            }
        }
        Assert.True(checkedPaths > 60, $"only {checkedPaths} reachable cases");
    }

    /// <summary>Recomputes a path's cost step by step and asserts legality (adjacent, passable, no corner cut).</summary>
    internal static long PathCost(TerrainGrid g, int[] cells)
    {
        long total = 0;
        for (int i = 1; i < cells.Length; i++)
        {
            int ax = g.IndexX(cells[i - 1]), ay = g.IndexY(cells[i - 1]), bx = g.IndexX(cells[i]), by = g.IndexY(cells[i]);
            int dx = bx - ax, dy = by - ay;
            Assert.True(Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1 && (dx != 0 || dy != 0));
            Assert.True(g.IsPassable(Costs, bx, by));
            long ca = Costs.Cost(g.ClassAt(ax, ay)), cb = Costs.Cost(g.ClassAt(bx, by));
            if (dx != 0 && dy != 0)
            {
                Assert.True(g.IsPassable(Costs, bx, ay) && g.IsPassable(Costs, ax, by), "corner cut");
                total += (ca + cb) * g.CellSizeMm * 1414 / 2000;
            }
            else total += (ca + cb) * g.CellSizeMm / 2;
        }
        return total;
    }

    [Fact]
    public void Tie_dense_open_field_gives_one_path_independent_of_neighbour_order()
    {
        // Open 40 x 40: an enormous number of equal-cost optimal paths between (0,0) and (39,25).
        var g = new TerrainGrid(40, 40);
        int[] reference = new GridPathfinder(g, Costs).FindPath(new(0, 0), new(39, 25)).Cells;
        for (ulong p = 0; p < 12; p++)
        {
            int[] order = TestSupport.Permutation(8, 1000 + p);
            var pf = new GridPathfinder(g, Costs, order);
            for (int rep = 0; rep < 2; rep++)                                     // reused work arrays give the same answer
                Assert.Equal(reference, pf.FindPath(new(0, 0), new(39, 25)).Cells);
        }
        // Same on a tie-dense field with obstacles (uniform cost, scattered walls).
        var w = RandomGrid(30, 30, 77, 12);
        for (int y = 0; y < 30; y++) for (int x = 0; x < 30; x++) if (w.ClassAt(x, y) != TerrainClass.Wall) w.SetClass(x, y, TerrainClass.Open);
        w.SetClass(0, 0, TerrainClass.Open); w.SetClass(29, 29, TerrainClass.Open);
        var r0 = new GridPathfinder(w, Costs).FindPath(new(0, 0), new(29, 29));
        Assert.True(r0.Found);
        for (ulong p = 0; p < 12; p++)
            Assert.Equal(r0.Cells, new GridPathfinder(w, Costs, TestSupport.Permutation(8, 2000 + p)).FindPath(new(0, 0), new(29, 29)).Cells);
    }

    [Fact]
    public void Tie_break_prefers_lower_h_then_lower_index_pinned()
    {
        // Open 3 x 3 from (0,0) to (2,1): optimal = one diagonal + one orthogonal (g = 9,656,000 either way).
        // Heuristic scale m = 1000 (only Open in the grid), S = 4000: orth unit 4,000,000, diag unit 5,656,000.
        // (1,0) idx 1: g 4,000,000 + h 5,656,000;  (1,1) idx 4: g 5,656,000 + h 4,000,000 => EQUAL f 9,656,000.
        // Lower h wins => (1,1) expands first and offers the goal (f 9,656,000, h 0), which then beats (1,0) on h.
        // => path via (1,1). Derived by hand from the (f, h, index) key.
        var g = new TerrainGrid(3, 3);
        var pf = new GridPathfinder(g, Costs);
        Assert.Equal(1000, pf.HeuristicCostScale);
        var r = pf.FindPath(new(0, 0), new(2, 1));
        Assert.Equal(new[] { g.Index(0, 0), g.Index(1, 1), g.Index(2, 1) }, r.Cells);
        // With a road somewhere in the grid the scale drops to 700, the f tie disappears ((1,0) f 7,959,200 < (1,1) f 8,456,000),
        // (1,1) later offers the goal the SAME g from parent 4 > 1 => the lower parent index (1) is kept => via (1,0).
        var gr = new TerrainGrid(3, 3);
        gr.SetClass(2, 2, TerrainClass.Road);
        var pr = new GridPathfinder(gr, Costs);
        Assert.Equal(700, pr.HeuristicCostScale);
        Assert.Equal(new[] { gr.Index(0, 0), gr.Index(1, 0), gr.Index(2, 1) }, pr.FindPath(new(0, 0), new(2, 1)).Cells);
        Assert.Equal(9_656_000L, r.Cost);
    }
}
