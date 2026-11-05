namespace Proto.M7.Spatial;

/// <summary>
/// One cell visited by a supercover walk, with the PARAMETER INTERVAL [TIn, TOut] (both over the walk's common
/// denominator <see cref="Supercover.Denominator"/>) during which the segment's moving point is inside it.
/// CornerSide = the cell is touched only at a single lattice-corner point (TIn == TOut), by the corner rule.
/// </summary>
public readonly record struct CoverCell(long X, long Y, long TIn, long TOut, bool CornerSide);

/// <summary>Streaming visitor for <see cref="Supercover.Walk"/>; return false to stop the walk early.</summary>
public interface ISupercoverVisitor
{
    bool Visit(in CoverCell c);
}

/// <summary>
/// Exact integer supercover traversal of a segment a-&gt;b over a square lattice of cell size S (spec §3).
///
/// DEFINITION of the cell set (direction-independent; the tests check it against a brute-force oracle and under reversal):
///   (1) MEMBERSHIP: every cell (half-open, as in CellOf) that contains at least one point of the CLOSED segment;
///   (2) CORNER RULE: if the segment is not axis-parallel and passes through a lattice corner P (including an endpoint
///       lying exactly on a corner), all four cells sharing P are included — "a corner crossing visits both side cells".
///   An axis-parallel segment running exactly along a grid line therefore covers only the +side row/column (membership),
///   which is what the moving point occupies.
/// Each cell is emitted exactly once, in non-decreasing TIn order. Corner-side cells have TIn == TOut.
///
/// Parameters are exact rationals over D = max(|dx|,1) * max(|dy|,1) (&lt;= 4*10^18 in the documented range): crossing a
/// vertical grid line happens at distX/|dx| = distX*|dy|/D, so all comparisons are exact integer comparisons.
/// Crossing rule (half-open cells): moving +x the point enters cell cx+1 AT x = (cx+1)S (t &lt;= 1 allowed); moving -x it enters
/// cx-1 only strictly after x = cx*S (t &lt; 1 required). Same for y.
/// </summary>
public static class Supercover
{
    public static long Denominator(Vec2L a, Vec2L b)
    {
        long adx = Math.Abs(b.X - a.X), ady = Math.Abs(b.Y - a.Y);
        return Math.Max(adx, 1) * Math.Max(ady, 1);
    }

    /// <summary>Walks the supercover of a-&gt;b, calling the visitor per cell. Returns false iff the visitor stopped it.</summary>
    public static bool Walk<TV>(long cellSizeMm, Vec2L a, Vec2L b, ref TV visitor) where TV : struct, ISupercoverVisitor
    {
        if (cellSizeMm <= 0) throw new ArgumentOutOfRangeException(nameof(cellSizeMm));
        Vec2L.RequireInRange(a, nameof(a));
        Vec2L.RequireInRange(b, nameof(b));
        long S = cellSizeMm;
        CellCoord c0 = TerrainGrid.CellOf(a, S), c1 = TerrainGrid.CellOf(b, S);
        long dx = b.X - a.X, dy = b.Y - a.Y;
        long adx = Math.Abs(dx), ady = Math.Abs(dy);
        long denX = Math.Max(adx, 1), denY = Math.Max(ady, 1);
        long D = denX * denY;
        int sx = Math.Sign(dx), sy = Math.Sign(dy);
        long cx = c0.X, cy = c0.Y;

        if (sx == 0 && sy == 0) return visitor.Visit(new CoverCell(cx, cy, 0, D, false));

        bool diagonal = sx != 0 && sy != 0;
        bool startCorner = diagonal && a.X - c0.X * S == 0 && a.Y - c0.Y * S == 0;
        bool endCorner = diagonal && b.X - c1.X * S == 0 && b.Y - c1.Y * S == 0;

        // Distance (mm, >= 0) from a to the next grid line in the direction of travel on each axis.
        long distX = sx > 0 ? (cx + 1) * S - a.X : sx < 0 ? a.X - cx * S : 0;
        long distY = sy > 0 ? (cy + 1) * S - a.Y : sy < 0 ? a.Y - cy * S : 0;
        long tIn = 0;
        bool first = true;

        while (true)
        {
            bool canX = sx > 0 ? distX <= adx : sx < 0 && distX < adx;
            bool canY = sy > 0 ? distY <= ady : sy < 0 && distY < ady;
            long nx = canX ? distX * denY : long.MaxValue;
            long ny = canY ? distY * denX : long.MaxValue;
            long tOut = (canX || canY) ? Math.Min(nx, ny) : D;
            if (!visitor.Visit(new CoverCell(cx, cy, tIn, tOut, false))) return false;
            if (first)
            {
                first = false;
                // Start-corner rule. The walker itself covers a (-,-) start (both crossings at t = 0); otherwise the cells
                // that are neither a's own cell nor the quadrant the segment heads into are emitted here as corner sides.
                if (startCorner && !(sx < 0 && sy < 0))
                {
                    long qx = sx > 0 ? c0.X : c0.X - 1, qy = sy > 0 ? c0.Y : c0.Y - 1;
                    if (!EmitCornerQuad(ref visitor, c0.X, c0.Y, c0.X, c0.Y, qx, qy, 0)) return false;
                }
            }
            if (!canX && !canY) break;
            if (nx < ny) { cx += sx; distX += S; }
            else if (ny < nx) { cy += sy; distY += S; }
            else
            {
                // Simultaneous crossing = the segment passes exactly through a lattice corner: both side cells.
                if (!visitor.Visit(new CoverCell(cx + sx, cy, tOut, tOut, true))) return false;
                if (!visitor.Visit(new CoverCell(cx, cy + sy, tOut, tOut, true))) return false;
                cx += sx; cy += sy; distX += S; distY += S;
            }
            tIn = tOut;
        }
        if (cx != c1.X || cy != c1.Y) throw new InvalidOperationException("supercover invariant violated: walk did not end in CellOf(b)");
        // End-corner rule: the walker covers a (+,+) end (both crossings at t = 1); otherwise emit the missing cells.
        if (endCorner && !(sx > 0 && sy > 0))
        {
            long px = sx > 0 ? c1.X - 1 : c1.X, py = sy > 0 ? c1.Y - 1 : c1.Y;
            if (!EmitCornerQuad(ref visitor, c1.X, c1.Y, c1.X, c1.Y, px, py, D)) return false;
        }
        return true;
    }

    /// <summary>Emits the cells of the 2x2 block around the corner at (cornerCx*S, cornerCy*S) other than the two given main cells.</summary>
    private static bool EmitCornerQuad<TV>(ref TV v, long cornerCx, long cornerCy, long m1x, long m1y, long m2x, long m2y, long t)
        where TV : struct, ISupercoverVisitor
    {
        for (long oy = -1; oy <= 0; oy++)
            for (long ox = -1; ox <= 0; ox++)
            {
                long x = cornerCx + ox, y = cornerCy + oy;
                if ((x == m1x && y == m1y) || (x == m2x && y == m2y)) continue;
                if (!v.Visit(new CoverCell(x, y, t, t, true))) return false;
            }
        return true;
    }

    private struct Collect : ISupercoverVisitor
    {
        public List<CoverCell> Out;
        public bool Visit(in CoverCell c) { Out.Add(c); return true; }
    }

    /// <summary>The supercover as a list (traversal order, each cell once).</summary>
    public static List<CoverCell> Cells(long cellSizeMm, Vec2L a, Vec2L b)
    {
        var v = new Collect { Out = new List<CoverCell>() };
        Walk(cellSizeMm, a, b, ref v);
        return v.Out;
    }

    private struct PassableCheck : ISupercoverVisitor
    {
        public TerrainGrid Grid;
        public TerrainCostTable Costs;
        public bool Visit(in CoverCell c) => Grid.IsPassable(Costs, c.X, c.Y);
    }

    /// <summary>True iff every supercover cell of a-&gt;b is in bounds and passable (continuous motion never enters a blocked cell).</summary>
    public static bool SegmentPassable(TerrainGrid grid, TerrainCostTable costs, Vec2L a, Vec2L b)
    {
        var v = new PassableCheck { Grid = grid, Costs = costs };
        return Walk(grid.CellSizeMm, a, b, ref v);
    }
}
