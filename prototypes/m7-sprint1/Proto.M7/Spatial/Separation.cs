using Proto.M7.Fixed;

namespace Proto.M7.Spatial;

/// <summary>A mover for one separation step: disc (Id, Pos, Radius) and the position it wants to reach this step.</summary>
public readonly record struct Mover(long Id, Vec2L Pos, long RadiusMm, Vec2L Desired);

/// <summary>Resolved position of one mover after the step (results are in ascending Id order).</summary>
public readonly record struct MoverResult(long Id, Vec2L Pos, bool Truncated);

/// <summary>
/// Collision / separation for one movement step (spec §3).
/// - Movers are processed in ASCENDING ID (the input list order is irrelevant: it is sorted by the unique id first).
/// - Each mover's straight move Pos-&gt;Desired is truncated so that its disc never ENTERS the open interior of any other disc
///   (overlap = centre distance^2 &lt; (ri + rj)^2; touching is allowed). The obstacles are ALL other discs at their CURRENT
///   positions: the committed (already moved, lower-id) discs at their new positions and the not-yet-moved (higher-id) discs
///   at their old positions. (Spec text says "a disc already committed this step"; including the not-yet-moved discs too is
///   required for the invariant "no overlap after any step" — otherwise a lower id could step onto a higher id that is then
///   unable to leave.) Invariant: non-overlapping in =&gt; non-overlapping out (tested in a crowded scenario).
/// - Truncation is integer: candidate end points q(k) = Pos + floor(d*k/K), K = max(|dx|,|dy|). Valid(k) = the ACTUAL
///   segment Pos-&gt;q(k) enters no disc interior (exact Int128 closest-point test) and, when a grid is given, touches only
///   passable cells (supercover); Valid(0) always holds. If Valid(K) the full move is taken; otherwise a binary search over
///   [0, K] keeps the invariant Valid(lo) &amp;&amp; !Valid(hi) and returns lo: a k with Valid(k) and !Valid(k+1) — a
///   deterministic BOUNDARY of the valid set, NOT necessarily the largest valid k. Floor rounding of q(k) makes Valid
///   non-monotone in k, so a larger valid k can exist beyond an invalid one (verify-C C-V-7: 17 of 3,641 small random cases).
///   Because the returned k is itself checked on the realised segment, the no-overlap invariant holds regardless.
/// Cost O(n^2 log K) per step — fine for the prototype's formations (100-120 entities); a broad phase is an optimisation, not
/// a semantic change.
/// </summary>
public static class Separation
{
    public static MoverResult[] Step(ReadOnlySpan<Mover> input, TerrainGrid? grid = null, TerrainCostTable? costs = null)
    {
        if ((grid is null) != (costs is null)) throw new ArgumentException("grid and costs must be given together");
        var m = input.ToArray();
        Array.Sort(m, static (x, y) => x.Id.CompareTo(y.Id));
        for (int i = 0; i < m.Length; i++)
        {
            if (i > 0 && m[i].Id == m[i - 1].Id) throw new ArgumentException("duplicate mover id " + m[i].Id);
            if (m[i].RadiusMm <= 0 || m[i].RadiusMm > Disc.MaxRadiusMm) throw new ArgumentOutOfRangeException(nameof(input), "radius out of range");
            Vec2L.RequireInRange(m[i].Pos, nameof(input)); Vec2L.RequireInRange(m[i].Desired, nameof(input));
        }
        var pos = new Vec2L[m.Length];
        for (int i = 0; i < m.Length; i++) pos[i] = m[i].Pos;
        for (int i = 0; i < m.Length; i++)
            for (int j = i + 1; j < m.Length; j++)
                if (Overlaps(pos[i], m[i].RadiusMm, pos[j], m[j].RadiusMm)) throw new ArgumentException($"initial overlap between {m[i].Id} and {m[j].Id}");

        var result = new MoverResult[m.Length];
        for (int i = 0; i < m.Length; i++)
        {
            Vec2L a = pos[i], d = m[i].Desired - a;
            long K = Math.Max(Math.Abs(d.X), Math.Abs(d.Y));
            if (K == 0) { result[i] = new MoverResult(m[i].Id, a, false); continue; }
            long best;
            if (Valid(m, pos, i, a, d, K, K, grid, costs)) best = K;
            else
            {
                long lo = 0, hi = K;                    // Valid(lo) holds (staying put is always valid), Valid(hi) fails
                while (hi - lo > 1)
                {
                    long mid = lo + (hi - lo) / 2;
                    if (Valid(m, pos, i, a, d, mid, K, grid, costs)) lo = mid; else hi = mid;
                }
                best = lo;
            }
            pos[i] = At(a, d, best, K);
            result[i] = new MoverResult(m[i].Id, pos[i], best != K);
        }
        return result;
    }

    public static bool Overlaps(Vec2L p, long rp, Vec2L q, long rq)
    {
        Int128 R = rp + rq;
        return Vec2L.DistanceSq(p, q) < R * R;
    }

    private static Vec2L At(Vec2L a, Vec2L d, long k, long K)
        => new(a.X + FixedMath.MulDivFloor(d.X, k, K), a.Y + FixedMath.MulDivFloor(d.Y, k, K));

    private static bool Valid(Mover[] m, Vec2L[] pos, int i, Vec2L a, Vec2L d, long k, long K, TerrainGrid? grid, TerrainCostTable? costs)
    {
        Vec2L q = At(a, d, k, K);
        if (q == a) return true;
        long ri = m[i].RadiusMm;
        long minX = Math.Min(a.X, q.X), maxX = Math.Max(a.X, q.X), minY = Math.Min(a.Y, q.Y), maxY = Math.Max(a.Y, q.Y);
        for (int j = 0; j < m.Length; j++)
        {
            if (j == i) continue;
            long R = ri + m[j].RadiusMm;
            Vec2L c = pos[j];
            if (c.X + R <= minX || c.X - R >= maxX || c.Y + R <= minY || c.Y - R >= maxY) continue;   // cannot reach the open interior
            if (SegmentEntersOpenDisc(a, q, c, R)) return false;
        }
        if (grid is not null && !Supercover.SegmentPassable(grid, costs!, a, q)) return false;
        return true;
    }

    /// <summary>True iff some point of the closed segment a-&gt;b is at distance &lt; R from c (exact Int128).</summary>
    public static bool SegmentEntersOpenDisc(Vec2L a, Vec2L b, Vec2L c, long R)
    {
        Vec2L d = b - a, w = c - a;
        Int128 A = d.LengthSq, RR = (Int128)R * R;
        if (A == 0) return w.LengthSq < RR;
        Int128 P = Vec2L.Dot(w, d);
        if (P <= 0) return w.LengthSq < RR;
        if (P >= A) return Vec2L.DistanceSq(b, c) < RR;
        return w.LengthSq * A - P * P < RR * A;
    }
}
