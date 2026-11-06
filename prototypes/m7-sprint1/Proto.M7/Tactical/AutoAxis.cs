using Proto.M7.Fixed;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>
/// [P] The AutoResolver's battle axis on TERRAIN (model v2): the least-cost route between the two sides' mean anchors — the
/// tactical planner itself (<see cref="GridPathfinder"/> with lane TR's Naismith climb), so a ford, a defile, a street or the
/// cheaper way round a hill is the axis, exactly as a tactical formation would march it. A polyline of mm points:
/// [mean anchor 0, centres of the path's interior cells, mean anchor 1], parameterised by arc length s in mm (side 0 at
/// s = 0 advances toward +s). When no passable path exists the axis is the straight segment (Sprint 1 geometry).
/// Integer only: segment lengths are exact floor square roots; a point at arc length s is interpolated with
/// sign-symmetric rounding toward zero (mirror images stay mirror images to the mm).
/// [P fix r2, P2-V3] The axis is EXTENDED beyond both ends along its end segments: s &lt; 0 lies behind side 0's mean anchor and
/// s &gt; Length behind side 1's, so a formation deployed in depth keeps its true depth (as the Sprint 1 straight axis did:
/// x = dot / length, unclamped). Until round 2 both <see cref="At"/> and <see cref="Project"/> clamped to [0, Length], which
/// moved every rear echelon up to its side's mean anchor (halving the battery's 40 m depths).
/// </summary>
public sealed class AutoAxis
{
    private readonly Vec2L[] _p;
    private readonly long[] _cum;

    private AutoAxis(Vec2L[] p)
    {
        _p = p;
        _cum = new long[p.Length];
        for (int i = 1; i < p.Length; i++) _cum[i] = checked(_cum[i - 1] + Vec2L.Distance(p[i - 1], p[i]));
    }

    /// <summary>Total arc length, mm.</summary>
    public long Length => _cum[^1];
    public IReadOnlyList<Vec2L> Points => _p;
    /// <summary>True iff the axis follows a planned path (false: the straight fallback).</summary>
    public bool OnPath { get; private init; }

    public static AutoAxis Straight(Vec2L a, Vec2L b) => new(new[] { a, b });

    /// <summary>An axis through explicit points (tests): at least two, no zero-length segment.</summary>
    public static AutoAxis Of(params Vec2L[] points)
    {
        if (points.Length < 2) throw new ArgumentException("an axis needs two points");
        for (int i = 1; i < points.Length; i++) if (points[i] == points[i - 1]) throw new ArgumentException("zero-length segment");
        return new AutoAxis((Vec2L[])points.Clone());
    }

    public static AutoAxis Build(TerrainGrid grid, TerrainCostTable costs, TerrainEffects fx, Vec2L s0, Vec2L s1)
    {
        var c0 = grid.CellOf(s0); var c1 = grid.CellOf(s1);
        if (!grid.IsPassable(costs, c0.X, c0.Y) || !grid.IsPassable(costs, c1.X, c1.Y)) return Straight(s0, s1);
        var pr = new GridPathfinder(grid, costs, default, fx).FindPath(c0, c1);
        if (!pr.Found) return Straight(s0, s1);
        var pts = new List<Vec2L> { s0 };
        for (int i = 1; i < pr.Cells.Length - 1; i++) pts.Add(grid.CellCenter(grid.IndexX(pr.Cells[i]), grid.IndexY(pr.Cells[i])));
        pts.Add(s1);
        // drop zero-length segments (an anchor exactly at an interior centre)
        var clean = new List<Vec2L> { pts[0] };
        for (int i = 1; i < pts.Count; i++) if (pts[i] != clean[^1]) clean.Add(pts[i]);
        if (clean.Count < 2) return Straight(s0, s1);
        return new AutoAxis(clean.ToArray()) { OnPath = true };
    }

    private static long TowardZero(long v, long num, long den) => v >= 0 ? FixedMath.MulDivFloor(v, num, den) : -FixedMath.MulDivFloor(-v, num, den);

    /// <summary>The point at arc length s; [P fix r2, P2-V3] beyond either end, on the end segment's line extended (rounded
    /// toward zero, sign-symmetric).</summary>
    public Vec2L At(long s)
    {
        if (s < 0)
        {
            var d0 = _p[1] - _p[0]; long seg0 = _cum[1];
            return new Vec2L(_p[0].X - TowardZero(d0.X, -s, seg0), _p[0].Y - TowardZero(d0.Y, -s, seg0));
        }
        if (s == 0) return _p[0];
        if (s >= Length)
        {
            int n = _p.Length - 1;
            var dn = _p[n] - _p[n - 1]; long segn = _cum[n] - _cum[n - 1];
            return new Vec2L(_p[n].X + TowardZero(dn.X, s - Length, segn), _p[n].Y + TowardZero(dn.Y, s - Length, segn));
        }
        int i = Array.BinarySearch(_cum, s);
        if (i >= 0) return _p[i];
        i = ~i - 1;                                  // _cum[i] < s < _cum[i + 1]
        long seg = _cum[i + 1] - _cum[i], t = s - _cum[i];
        var a = _p[i]; var d = _p[i + 1] - a;
        return new Vec2L(a.X + TowardZero(d.X, t, seg), a.Y + TowardZero(d.Y, t, seg));
    }

    /// <summary>Arc length of the axis point nearest to p, by composite key (distance², segment index): the lower segment
    /// wins a tie. Along a segment the foot of the perpendicular is dot / length (rounded toward zero), clamped to the segment —
    /// [P fix r2, P2-V3] except before the first segment's start and past the last segment's end, where the extended axis
    /// continues (negative arc length behind side 0, beyond Length behind side 1).</summary>
    public long Project(Vec2L p)
    {
        long best = 0; Int128 bestD = Int128.MaxValue;
        int last = _p.Length - 2;
        for (int i = 0; i + 1 < _p.Length; i++)
        {
            long seg = _cum[i + 1] - _cum[i];
            if (seg <= 0) continue;
            Int128 dot = Vec2L.Dot(p - _p[i], _p[i + 1] - _p[i]);
            Int128 foot = dot / seg;                                             // toward zero
            long along = (long)(foot < 0 ? (i == 0 ? foot : 0) : foot > seg ? (i == last ? foot : seg) : foot);
            long s = _cum[i] + along;
            Int128 d = Vec2L.DistanceSq(p, At(s));
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }
}
