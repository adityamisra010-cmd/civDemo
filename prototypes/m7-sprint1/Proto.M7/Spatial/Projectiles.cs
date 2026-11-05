using Proto.M7.Fixed;

namespace Proto.M7.Spatial;

/// <summary>An entity footprint: a closed disc (the boundary belongs to the disc). Radius in (0, MaxRadiusMm].</summary>
public readonly record struct Disc(long Id, Vec2L Center, long RadiusMm)
{
    /// <summary>Bound on entity radius (1 km): keeps r^2 * |d|^2 well inside Int128.</summary>
    public const long MaxRadiusMm = 1_000_000;
}

/// <summary>Segment parameter of an intersection: t = TNum / TDen in [0, 1].</summary>
public readonly record struct SegmentHit(bool Hit, long TNum, long TDen);

public enum FireOutcome : byte { Miss = 0, Hit = 1, Blocked = 2 }

public readonly record struct FireResult(FireOutcome Outcome, long EntityId, long TNum, long TDen, LosBlock Block, CellCoord BlockCell);

public enum BallisticOutcome : byte { HitEntity = 0, Ground = 1, Wall = 2, OffGrid = 3 }

public readonly record struct BallisticShot(Vec2L Origin, long Z0Mm, long VxMmPerS, long VyMmPerS, long VzMmPerS)
{
    /// <summary>Velocity bound per component (10 km/s).</summary>
    public const long MaxSpeedMmPerS = 10_000_000;
    /// <summary>Launch-height bound: |Z0Mm| &lt;= 2*10^9 mm (the terrain height bound plus up to 10^9 mm above it). Engineering
    /// bound that keeps the arc arithmetic inside long — PLACEHOLDER (not a Director value; verify-C C-V-9).</summary>
    public const long MaxAbsZ0Mm = 2 * TerrainGrid.MaxAbsHeightMm;
}

public readonly record struct BallisticResult(BallisticOutcome Outcome, long EntityId, Vec2L Impact, long ImpactZMm, long ImpactTimeMs, long Steps);

/// <summary>
/// Projectile and hit queries (spec §3), exact integer geometry.
///
/// SEGMENT vs DISC (closed disc, so TANGENT = HIT, and a segment ending exactly on the boundary hits at t = 1):
///   d = b - a, A = |d|^2, w = c - a, P = w.d. Intersection is decided EXACTLY from the closest point of the segment to c
///   (Int128: |w|^2*A - P^2 &lt;= r^2*A in the interior case; products &lt;= 6.4e37 in the documented range).
///   Entry parameter: t = 0 if a is inside; else t = (P - ceil(sqrt(P^2 - A(|w|^2 - r^2)))) / A — the exact root rounded
///   DOWN to a multiple of 1/A (ceil of the sqrt), so it is never later than the true entry and lies in [0, 1].
///   Zero-length segment (A = 0): hit iff |w| &lt;= r, t = 0/1.
/// FIRST HIT along a segment: minimal (t, entityId) — every disc of one query shares the denominator A, so t compares by
///   numerator; equal t (including two discs whose rounded entries coincide) =&gt; LOWER ID wins.
/// DIRECT FIRE: the first hit, unless a blocking cell (off-grid, Wall, or a height block as in LOS, from muzzle height to
///   aim height; the shooter's cell and the aim cell are excluded from the height test) is ENTERED STRICTLY BEFORE the hit
///   parameter (TIn/D &lt; tHit). A hit point exactly on a wall face is a hit.
/// BALLISTIC ARC: closed-form integer kinematics sampled every stepMs (no accumulated rounding):
///   x(t) = x0 + floor(vx*t/1000), z(t) = z0 + floor((2000*vz*t - g*t^2) / 2,000,000), t in ms, v in mm/s, g in mm/s^2
///   (default 9,807). Between samples the shell moves on the straight 3-D chord (prev, z(k-1)) -&gt; (p(k), z(k)); its height
///   at parameter n/D of the step's supercover is Z(n) = z(k-1) + floor((z(k) - z(k-1)) * n / D) — the LOS interpolation.
///   Impact step = first k whose chord, walked cell by cell over the step's supercover (every cell, as LOS and direct fire
///   do; verify-C C-V-4), meets one of: an off-grid or Wall cell (stop at its entry TIn); a cell whose flat top is at or
///   above the chord where it ENTERS the cell, Z(TIn) &lt;= ground (stop at TIn: the shell strikes the cell's face); or a
///   cell where the chord ends at or below the top, Z(TOut) &lt;= ground (stop at TOut). For the arrival cell TOut = D, so
///   this is exactly the sampled rule "z(k) &lt;= ground(CellOf(p(k)))" with impact (p(k), z(k), t(k)); for an intermediate
///   cell it stops the shell instead of letting it pass through a raised cell between two samples. The impact point and
///   time at a stop parameter n are the chord point at n/D and t(k-1) + floor(stepMs*n/D).
///   Discs: on the impact step the horizontal segment is tested against the discs; the first hit (t, id) counts iff it is
///   AT OR BEFORE the stop parameter (t &lt;= n/D) — so a disc touching a wall face exactly is hit, the same tie rule as
///   DIRECT FIRE (verify-C C-V-5).
///   Input bounds: stepMs in [1, 1000], g in [1, 10^6], |v| &lt;= MaxSpeedMmPerS per component, |z0| &lt;= MaxAbsZ0Mm, and the
///   shot must be guaranteed down by MaxFlightMs: z(MaxFlightMs) &lt;= -TerrainGrid.MaxAbsHeightMm (below every possible
///   ground). Termination: z is a concave parabola with z(MaxFlightMs) &lt; z0, so it is decreasing from MaxFlightMs on and
///   the impact step is at most ceil(MaxFlightMs/stepMs) — at most 3.6*10^6 steps (verify-C C-V-6).
/// </summary>
public static class Projectiles
{
    public const long DefaultGravityMmPerS2 = 9_807;
    /// <summary>Flight-time bound (1 h): every accepted shot is down by then. Engineering bound that caps the step count of
    /// <see cref="Fly"/> — PLACEHOLDER (not a Director value; verify-C C-V-6). With the default gravity every shot inside the
    /// velocity and launch-height bounds satisfies it, so it only rejects low-gravity inputs.</summary>
    public const long MaxFlightMs = 3_600_000;

    public static SegmentHit SegmentCircle(Vec2L a, Vec2L b, Vec2L c, long r)
    {
        Vec2L.RequireInRange(a, nameof(a)); Vec2L.RequireInRange(b, nameof(b)); Vec2L.RequireInRange(c, nameof(c));
        if (r <= 0 || r > Disc.MaxRadiusMm) throw new ArgumentOutOfRangeException(nameof(r));
        Vec2L d = b - a, w = c - a;
        Int128 A = d.LengthSq, ww = w.LengthSq, rr = (Int128)r * r;
        if (A == 0) return ww <= rr ? new SegmentHit(true, 0, 1) : new SegmentHit(false, 0, 1);
        long den = (long)A;
        if (ww <= rr) return new SegmentHit(true, 0, den);                  // starts inside (or on) the disc
        Int128 P = Vec2L.Dot(w, d);
        if (P <= 0) return new SegmentHit(false, 0, den);                    // closest point is a, which is outside
        if (P >= A)
        {
            if (Vec2L.DistanceSq(b, c) > rr) return new SegmentHit(false, 0, den);   // closest point is b
        }
        else if (ww * A - P * P > rr * A) return new SegmentHit(false, 0, den);      // interior closest point outside
        Int128 disc = P * P - A * (ww - rr);
        if (disc < 0) throw new InvalidOperationException("segment-circle invariant: intersecting segment with negative discriminant");
        Int128 num = P - (Int128)SpatialMath.ISqrtCeil((UInt128)disc);
        if (num < 0) num = 0;
        return new SegmentHit(true, (long)num, den);
    }

    /// <summary>First disc hit along a->b by (t, id), skipping ignoreId. Found=false when none.</summary>
    public static bool FirstHit(Vec2L a, Vec2L b, ReadOnlySpan<Disc> discs, long ignoreId, out long entityId, out SegmentHit hit)
    {
        entityId = 0; hit = default;
        bool found = false;
        for (int i = 0; i < discs.Length; i++)
        {
            Disc e = discs[i];
            if (e.Id == ignoreId) continue;
            SegmentHit h = SegmentCircle(a, b, e.Center, e.RadiusMm);
            if (!h.Hit) continue;
            // Same segment => same denominator for every disc: compare numerators, then id.
            if (!found || h.TNum < hit.TNum || (h.TNum == hit.TNum && e.Id < entityId))
            {
                found = true; entityId = e.Id; hit = h;
            }
        }
        return found;
    }

    private struct FireVisitor : ISupercoverVisitor
    {
        public TerrainGrid Grid;
        public CellCoord Shooter, Aim;
        public long Z0, Z1, D;
        public bool HasHit;
        public long HitNum, HitDen;
        public LosBlock Block;
        public CellCoord BlockCell;

        public bool Visit(in CoverCell c)
        {
            // Cells entered at or after the hit parameter cannot block it.
            if (HasHit && (Int128)c.TIn * HitDen >= (Int128)HitNum * D) return false;
            if (!Grid.InBounds(c.X, c.Y)) { Block = LosBlock.OffGrid; BlockCell = new(c.X, c.Y); return false; }
            if (Grid.ClassAt(c.X, c.Y) == TerrainClass.Wall) { Block = LosBlock.Wall; BlockCell = new(c.X, c.Y); return false; }
            bool endpoint = (c.X == Shooter.X && c.Y == Shooter.Y) || (c.X == Aim.X && c.Y == Aim.Y);
            if (!endpoint)
            {
                long zIn = Z0 + FixedMath.MulDivFloor(Z1 - Z0, c.TIn, D), zOut = Z0 + FixedMath.MulDivFloor(Z1 - Z0, c.TOut, D);
                if (Grid.HeightAt(c.X, c.Y) > Math.Min(zIn, zOut)) { Block = LosBlock.Height; BlockCell = new(c.X, c.Y); return false; }
            }
            return true;
        }
    }

    /// <summary>Direct fire from `from` (muzzle at ground+muzzleHeight) toward `aim` (aim point at ground+aimHeight), segment-limited.</summary>
    public static FireResult DirectFire(TerrainGrid grid, Vec2L from, long muzzleHeightMm, Vec2L aim, long aimHeightMm, ReadOnlySpan<Disc> discs, long shooterId)
    {
        CellCoord sc = grid.CellOf(from), ac = grid.CellOf(aim);
        if (!grid.InBounds(sc.X, sc.Y)) return new FireResult(FireOutcome.Blocked, 0, 0, 1, LosBlock.OffGrid, sc);
        bool hasHit = FirstHit(from, aim, discs, shooterId, out long id, out SegmentHit h);
        long aimGround = grid.InBounds(ac.X, ac.Y) ? grid.HeightAt(ac.X, ac.Y) : 0;
        var v = new FireVisitor
        {
            Grid = grid, Shooter = sc, Aim = ac,
            Z0 = grid.HeightAt(sc.X, sc.Y) + muzzleHeightMm, Z1 = aimGround + aimHeightMm,
            D = Supercover.Denominator(from, aim),
            HasHit = hasHit, HitNum = h.TNum, HitDen = hasHit ? h.TDen : 1,
        };
        Supercover.Walk(grid.CellSizeMm, from, aim, ref v);
        if (v.Block != LosBlock.None) return new FireResult(FireOutcome.Blocked, 0, 0, 1, v.Block, v.BlockCell);
        return hasHit ? new FireResult(FireOutcome.Hit, id, h.TNum, h.TDen, LosBlock.None, default)
                      : new FireResult(FireOutcome.Miss, 0, 0, 1, LosBlock.None, default);
    }

    /// <summary>Exact sample of the arc at time tMs (closed form, floor).</summary>
    public static (Vec2L P, long Z) ArcAt(in BallisticShot s, long tMs, long gravityMmPerS2)
    {
        long x = s.Origin.X + FixedMath.DivFloor(s.VxMmPerS * tMs, 1000);
        long y = s.Origin.Y + FixedMath.DivFloor(s.VyMmPerS * tMs, 1000);
        Int128 zn = (Int128)2000 * s.VzMmPerS * tMs - (Int128)gravityMmPerS2 * tMs * tMs;
        Int128 q = zn / 2_000_000;
        if (q * 2_000_000 > zn) q -= 1;                                    // floor for negatives
        return (new Vec2L(x, y), s.Z0Mm + (long)q);
    }

    private struct StepVisitor : ISupercoverVisitor
    {
        public TerrainGrid Grid;
        public long Z0, Z1, D;
        public bool Stopped;
        public BallisticOutcome Kind;
        public long TStop;

        private long ZAt(long n) => Z0 + FixedMath.MulDivFloor(Z1 - Z0, n, D);

        public bool Visit(in CoverCell c)
        {
            if (!Grid.InBounds(c.X, c.Y)) { Stopped = true; Kind = BallisticOutcome.OffGrid; TStop = c.TIn; return false; }
            if (Grid.ClassAt(c.X, c.Y) == TerrainClass.Wall) { Stopped = true; Kind = BallisticOutcome.Wall; TStop = c.TIn; return false; }
            long top = Grid.HeightAt(c.X, c.Y);
            if (ZAt(c.TIn) <= top) { Stopped = true; Kind = BallisticOutcome.Ground; TStop = c.TIn; return false; }
            if (ZAt(c.TOut) <= top) { Stopped = true; Kind = BallisticOutcome.Ground; TStop = c.TOut; return false; }
            return true;
        }
    }

    public static BallisticResult Fly(TerrainGrid grid, in BallisticShot shot, long stepMs, ReadOnlySpan<Disc> discs, long shooterId,
                                      long gravityMmPerS2 = DefaultGravityMmPerS2)
    {
        if (stepMs <= 0 || stepMs > 1000) throw new ArgumentOutOfRangeException(nameof(stepMs), "step must be in [1, 1000] ms");
        if (gravityMmPerS2 <= 0 || gravityMmPerS2 > 1_000_000) throw new ArgumentOutOfRangeException(nameof(gravityMmPerS2));
        long M = BallisticShot.MaxSpeedMmPerS;
        if (Math.Abs(shot.VxMmPerS) > M || Math.Abs(shot.VyMmPerS) > M || Math.Abs(shot.VzMmPerS) > M) throw new ArgumentOutOfRangeException(nameof(shot));
        if (shot.Z0Mm < -BallisticShot.MaxAbsZ0Mm || shot.Z0Mm > BallisticShot.MaxAbsZ0Mm)
            throw new ArgumentOutOfRangeException(nameof(shot), "launch height out of range");
        if (ArcAt(shot, MaxFlightMs, gravityMmPerS2).Z > -TerrainGrid.MaxAbsHeightMm)
            throw new ArgumentOutOfRangeException(nameof(shot), "shot is not guaranteed to land within MaxFlightMs");
        CellCoord oc = grid.CellOf(shot.Origin);
        if (!grid.InBounds(oc.X, oc.Y)) return new BallisticResult(BallisticOutcome.OffGrid, 0, shot.Origin, shot.Z0Mm, 0, 0);
        if (shot.Z0Mm <= grid.HeightAt(oc.X, oc.Y)) return new BallisticResult(BallisticOutcome.Ground, 0, shot.Origin, shot.Z0Mm, 0, 0);

        Vec2L prev = shot.Origin; long prevZ = shot.Z0Mm;
        for (long k = 1; ; k++)
        {
            long t = k * stepMs;
            (Vec2L p, long z) = ArcAt(shot, t, gravityMmPerS2);
            long D = Supercover.Denominator(prev, p);
            var sv = new StepVisitor { Grid = grid, Z0 = prevZ, Z1 = z, D = D };
            Supercover.Walk(grid.CellSizeMm, prev, p, ref sv);
            if (sv.Stopped)
            {
                long tPrev = t - stepMs;
                if (FirstHit(prev, p, discs, shooterId, out long id, out SegmentHit h)
                    && (Int128)h.TNum * D <= (Int128)sv.TStop * h.TDen)
                {
                    Vec2L hp = new(prev.X + FixedMath.MulDivFloor(p.X - prev.X, h.TNum, h.TDen), prev.Y + FixedMath.MulDivFloor(p.Y - prev.Y, h.TNum, h.TDen));
                    long hz = prevZ + FixedMath.MulDivFloor(z - prevZ, h.TNum, h.TDen);
                    return new BallisticResult(BallisticOutcome.HitEntity, id, hp, hz, tPrev + FixedMath.MulDivFloor(stepMs, h.TNum, h.TDen), k);
                }
                Vec2L wp = new(prev.X + FixedMath.MulDivFloor(p.X - prev.X, sv.TStop, D), prev.Y + FixedMath.MulDivFloor(p.Y - prev.Y, sv.TStop, D));
                long wz = prevZ + FixedMath.MulDivFloor(z - prevZ, sv.TStop, D);
                return new BallisticResult(sv.Kind, 0, wp, wz, tPrev + FixedMath.MulDivFloor(stepMs, sv.TStop, D), k);
            }
            prev = p; prevZ = z;
        }
    }
}
