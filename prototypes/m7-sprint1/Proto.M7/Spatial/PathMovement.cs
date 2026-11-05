using Proto.M7.Fixed;

namespace Proto.M7.Spatial;

/// <summary>
/// Path following and terrain-aware movement time (spec §3).
/// Entities move along the CELL-CENTRE polyline of an A* path. Movement time along a straight segment a-&gt;b is
///   time_ms = ceil( sum over supercover membership cells of  (TOut - TIn)/D * L * cost[cell] / speed )
/// with L = floor(|b - a|) mm (exact ISqrt), cost x1000 per mm, speed in mm/s: cost/1000 (cost-mm per mm) * 1000 (ms per s)
/// cancel, so ms = mm * cost / speed. Everything is one Int128 numerator over one denominator, rounded UP once
/// (a move is never early). Corner-side cells have zero length and contribute nothing.
/// Consistency with the planner: on the default 4,000 mm cell an orthogonal centre step has L = S and a diagonal one
/// L = floor(S*sqrt 2) = 5,656 = S*1414/1000, so SegmentTimeMs == ceil(A* step cost / speed) exactly (tested).
/// </summary>
public static class PathMovement
{
    /// <summary>Cell-centre waypoints of a path (linear indices from <see cref="GridPathfinder"/>).</summary>
    public static Vec2L[] Waypoints(TerrainGrid grid, ReadOnlySpan<int> cells)
    {
        var w = new Vec2L[cells.Length];
        for (int i = 0; i < cells.Length; i++) w[i] = grid.CellCenter(grid.IndexX(cells[i]), grid.IndexY(cells[i]));
        return w;
    }

    private struct Accumulate : ISupercoverVisitor
    {
        public TerrainGrid Grid;
        public TerrainCostTable Costs;
        public Int128 Weighted;   // sum (TOut - TIn) * cost
        public bool Visit(in CoverCell c)
        {
            if (!Grid.IsPassable(Costs, c.X, c.Y)) return false;
            if (!c.CornerSide) Weighted += (Int128)(c.TOut - c.TIn) * Costs.Cost(Grid.ClassAt(c.X, c.Y));
            return true;
        }
    }

    /// <summary>
    /// Time (ms, ceil) to move straight from a to b at base speed (mm/s on cost-1000 ground). Throws if the segment touches
    /// an impassable or off-grid cell (callers must only move along passable segments — the path-consistency property).
    /// </summary>
    public static long SegmentTimeMs(TerrainGrid grid, TerrainCostTable costs, Vec2L a, Vec2L b, long baseSpeedMmPerS)
    {
        if (baseSpeedMmPerS <= 0) throw new ArgumentOutOfRangeException(nameof(baseSpeedMmPerS));
        var v = new Accumulate { Grid = grid, Costs = costs };
        if (!Supercover.Walk(grid.CellSizeMm, a, b, ref v))
            throw new InvalidOperationException("segment touches an impassable or off-grid cell");
        long L = Vec2L.Distance(a, b);
        long D = Supercover.Denominator(a, b);
        Int128 num = v.Weighted * L;                 // <= 4e18 * 1e6 * 2.9e9 ~ 1.2e34 < Int128 max
        Int128 den = (Int128)D * baseSpeedMmPerS;
        Int128 q = num / den;
        if (q * den < num) q += 1;
        return (long)q;
    }

    /// <summary>Total time along a waypoint polyline (sum of per-segment ceilings).</summary>
    public static long PolylineTimeMs(TerrainGrid grid, TerrainCostTable costs, ReadOnlySpan<Vec2L> waypoints, long baseSpeedMmPerS)
    {
        long t = 0;
        for (int i = 1; i < waypoints.Length; i++) t += SegmentTimeMs(grid, costs, waypoints[i - 1], waypoints[i], baseSpeedMmPerS);
        return t;
    }

    /// <summary>
    /// Position after moving for elapsedMs along a->b (linear in time over the segment's total time, floor per axis).
    /// Simple constant-speed-per-segment interpolation (the segment's average terrain speed); exact at 0 and at the end.
    /// </summary>
    public static Vec2L PositionAt(Vec2L a, Vec2L b, long elapsedMs, long segmentTimeMs)
    {
        if (segmentTimeMs <= 0 || elapsedMs >= segmentTimeMs) return b;
        if (elapsedMs <= 0) return a;
        return new Vec2L(a.X + FixedMath.MulDivFloor(b.X - a.X, elapsedMs, segmentTimeMs),
                         a.Y + FixedMath.MulDivFloor(b.Y - a.Y, elapsedMs, segmentTimeMs));
    }
}
