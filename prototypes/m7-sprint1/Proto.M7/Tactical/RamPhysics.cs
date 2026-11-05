using Proto.M7.Fixed;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>How a bow contact under the ram directive is classified (D-081). Stable codes (event field A of a Ram event).</summary>
public enum RamOutcome : byte
{
    /// <summary>A ram that breaches the target's side: hull and crew damage, the beak is embedded and must be backed out.</summary>
    Breach = 0,
    /// <summary>A ram at ramming speed whose blow glances off (impact angle below the glance angle: the beak skids along the
    /// planking): no breach. It is still the rammer's one ram on that hull (D-081 RM1, reading RAM-R1).</summary>
    Glance = 1,
}

/// <summary>Geometry and kinematics of one bow contact, all integer (mm, mm/s, binary angle units).</summary>
public readonly record struct RamImpact(long ClosingMmPerS, long NormalMmPerS, long TangentialMmPerS, int ImpactAngle, bool IsRam, bool Breach);

/// <summary>
/// RAM LANE (D-081) — the pure equations of ramming. No battle state; every function is integer-only and deterministic, so
/// the boundaries can be tested exactly (fixed-point, no epsilon). Every coefficient they are called with is a PLACEHOLDER
/// with the cited rationale in <see cref="DomainProfiles.Galley"/> and <see cref="DomainRules"/>.
///
/// Notation: rammer a, target b; v_a, v_b = velocity vectors (way speed along the heading); w = v_a - v_b (closing velocity).
///  - closing speed      v_c = w . (p_b - p_a) / |p_b - p_a|           (rate at which the centres close; &gt; 0 = an impact)
///  - normal component   v_n = |w x k_b|, k_b = unit keel vector of b    (component of w perpendicular to b's side: the speed
///                       at which the beak drives into the planking). b's velocity lies along k_b, so v_n = |v_a x k_b|
///                       exactly; it is computed from v_a so that the per-axis rounding of v_b cannot leak into it, which
///                       makes v_n &lt;= the rammer's own way speed exact.
///  - tangential         v_t = |w . k_b|                                 (sliding along b's side; b's own motion enters here)
///  - impact angle       theta = atan2(v_n, v_t) in [0, 90 deg]           (90 deg = perpendicular to b's keel)
/// [r2 RAM2-V1, V2] A contact is a RAM iff v_n &gt;= the rammer class's ramming speed (D-081 RM3, RM4: the closing velocity
/// component normal to the target's hull, the same speed the damage uses); below it, it is a collision, not a ram. Only the
/// rammer's own way can supply v_n, so a hull that has not built ramming speed can never ram (RM3) — a target steaming into
/// its bow adds only tangential sliding. (Round 1 gated on the centre-line v_c: full-way beak strikes off the target's
/// centre were filed as collisions with no damage, and a hull lying still was credited with rams the target ran into.)
/// A ram BREACHES iff theta &gt;= the glance angle; below it the beak skids along the planking whatever the speed (glance).
/// Energy: the kinetic energy of the normal component is proportional to v_n^2 (the rammer's mass is a class constant, so it
/// sits inside the class coefficients); every effect scales as amount * v_n^2 / v_ref^2, v_ref = the rammer's MaxSpeed.
/// </summary>
public static class RamPhysics
{
    /// <summary>Velocity vector of a hull at speed s (mm/s) along heading h (toward-zero rounding per axis).</summary>
    public static Vec2L Velocity(int heading, long speed) => HeadingMotion.Offset(heading, speed);

    /// <summary>Classifies a contact. pa/pb are the hull centres, ha/hb the headings, sa/sb the way speeds.</summary>
    public static RamImpact Classify(Vec2L pa, int ha, long sa, Vec2L pb, int hb, long sb, long ramSpeed, int glanceAngle)
    {
        Vec2L va = Velocity(ha, sa);
        Vec2L w = va - Velocity(hb, sb);
        Vec2L dp = pb - pa;
        long dist = dp.Length;
        long vc = dist == 0 ? 0 : (long)FloorDiv(Vec2L.Dot(w, dp), dist);
        Int128 cross = (Int128)va.X * FixedTrig.Sin(hb) - (Int128)va.Y * FixedTrig.Cos(hb);   // = w x k_b (v_b || k_b)
        Int128 dot = (Int128)w.X * FixedTrig.Cos(hb) + (Int128)w.Y * FixedTrig.Sin(hb);
        long vn = (long)((cross < 0 ? -cross : cross) >> FixedTrig.OneShift);
        long vt = (long)((dot < 0 ? -dot : dot) >> FixedTrig.OneShift);
        int theta = FixedTrig.Atan2(vn, vt);
        bool ram = vn >= ramSpeed;                                       // [r2 V1, V2] the normal speed, as the damage
        bool breach = ram && theta >= glanceAngle;
        return new RamImpact(vc, vn, vt, theta, ram, breach);
    }

    /// <summary>floor(amount * v^2 / vref^2), exact (Int128). The kinetic-energy scaling of every ram effect.</summary>
    public static long EnergyScaled(long amount, long v, long vref)
    {
        if (vref <= 0) throw new ArgumentOutOfRangeException(nameof(vref));
        if (amount <= 0 || v <= 0) return 0;
        return (long)((Int128)amount * v * v / ((Int128)vref * vref));
    }

    /// <summary>floor(amount * permille * v^2 / (1000 * vref^2)), exact.</summary>
    public static long EnergyScaledPermille(long amount, long permille, long v, long vref)
    {
        if (vref <= 0) throw new ArgumentOutOfRangeException(nameof(vref));
        if (amount <= 0 || permille <= 0 || v <= 0) return 0;
        return (long)((Int128)amount * permille * v * v / ((Int128)1000 * vref * vref));
    }

    /// <summary>
    /// The smallest surface gap (mm) from which a hull at way speed v0, pointed at a stationary target and pulling at full
    /// power (speed += floor(accel * dt / 1000) per step, capped at vmax; displacement floor(speed * dt / 1000) per step — the
    /// movement integration of <see cref="HeadingMotion"/>), is still short of contact (gap &gt; beakMm) at the end of every
    /// step before the first step at which its speed reaches vRam. 0 when v0 &gt;= vRam; long.MaxValue when vRam can never be
    /// reached (vmax &lt; vRam or no acceleration). This is d = (vRam^2 - v0^2) / (2a) of D-081 RM3, integrated exactly in the
    /// simulation's own steps, plus the beak's reach.
    /// </summary>
    public static long RunUpGapMm(long v0, long vRam, long vmax, long accel, long dtMs, long beakMm)
    {
        if (v0 >= vRam) return 0;
        long dv = FixedMath.MulDivFloor(accel, dtMs, 1000);
        if (vmax < vRam || dv <= 0) return long.MaxValue;
        long sp = Math.Max(0, v0), x = 0;
        while (true)
        {
            long next = Math.Min(vmax, sp + dv);
            if (next >= vRam) return checked(x + beakMm + 1);
            x = checked(x + HeadingMotion.Distance(next, dtMs));
            sp = next;
        }
    }

    /// <summary>
    /// [r2 RAM2-V1] The own way speed a run along heading <paramref name="runHeading"/> needs for its normal component on a
    /// hull with keel heading <paramref name="targetHeading"/> to reach <paramref name="ramSpeed"/>: ceil(v_ram / |sin a|), a
    /// = the run's angle to the keel (exactly v_ram on a perpendicular run); long.MaxValue along the keel line.
    /// </summary>
    public static long RequiredWayMmPerS(long ramSpeed, int runHeading, int targetHeading)
    {
        long s = FixedTrig.Sin(FixedTrig.Diff(targetHeading, runHeading));
        if (s < 0) s = -s;
        if (s == 0) return long.MaxValue;
        return (long)(((Int128)ramSpeed * FixedTrig.One + s - 1) / s);
    }

    /// <summary>Diameter (mm) of the turning circle at speed v (mm/s) and turn rate w (angle units per second):
    /// 2v / omega = v * Turn / (pi * w), pi = 355/113 (floor).</summary>
    public static long TurnDiameterMm(long speed, long turnRatePerS)
    {
        if (turnRatePerS <= 0) throw new ArgumentOutOfRangeException(nameof(turnRatePerS));
        return (long)((Int128)Math.Max(0, speed) * FixedTrig.Turn * 113 / ((Int128)355 * turnRatePerS));
    }

    /// <summary>Time (ms) to back a beak embedded depthMm out of a breach from rest at backing acceleration aBack (mm/s^2):
    /// t = sqrt(2 depth / aBack), rounded UP to the ms (exact integer square root).</summary>
    public static long FreeRamMs(long depthMm, long aBack)
    {
        if (aBack <= 0) throw new ArgumentOutOfRangeException(nameof(aBack));
        if (depthMm <= 0) return 0;
        Int128 num = (Int128)2 * depthMm * 1_000_000;
        Int128 x = (num + aBack - 1) / aBack;                        // ceil(2 depth 10^6 / aBack) = t^2 in ms^2
        long r = (long)FixedMath.ISqrt((UInt128)x);
        if ((Int128)r * r < x) r++;
        return r;
    }

    /// <summary>Angle between the line target -&gt; point and the target's keel line, in [0, 90 deg] (90 = on its beam).</summary>
    public static int ApproachAngle(Vec2L target, int targetHeading, Vec2L from)
    {
        Vec2L r = from - target;
        Int128 cross = (Int128)r.X * FixedTrig.Sin(targetHeading) - (Int128)r.Y * FixedTrig.Cos(targetHeading);
        Int128 dot = (Int128)r.X * FixedTrig.Cos(targetHeading) + (Int128)r.Y * FixedTrig.Sin(targetHeading);
        long n = (long)((cross < 0 ? -cross : cross) >> FixedTrig.OneShift);
        long t = (long)((dot < 0 ? -dot : dot) >> FixedTrig.OneShift);
        return FixedTrig.Atan2(n, t);
    }

    /// <summary>The beam normal of the target on the side of the point (port when the point lies on the keel line: tie rule),
    /// as a heading.</summary>
    public static int BeamToward(Vec2L target, int targetHeading, Vec2L from)
    {
        Vec2L r = from - target;
        // z of (keel x r): > 0 => the point lies to port (left of the heading).
        Int128 z = (Int128)FixedTrig.Cos(targetHeading) * r.Y - (Int128)FixedTrig.Sin(targetHeading) * r.X;
        return FixedTrig.Norm((long)targetHeading + (z >= 0 ? FixedTrig.Quarter : -FixedTrig.Quarter));
    }

    /// <summary>
    /// Ram target choice (D-081 RM2): the index minimising the composite key (distance², id) over the eligible candidates
    /// (tracked, on the field, not yet rammed by this hull); -1 if none. Input order is irrelevant.
    /// </summary>
    public static int ArgMinTarget(ReadOnlySpan<long> ids, ReadOnlySpan<Int128> distSq, ReadOnlySpan<bool> eligible)
    {
        int best = -1;
        for (int i = 0; i < ids.Length; i++)
        {
            if (!eligible[i]) continue;
            if (best < 0 || distSq[i] < distSq[best] || (distSq[i] == distSq[best] && ids[i] < ids[best])) best = i;
        }
        return best;
    }

    private static Int128 FloorDiv(Int128 a, long b)
    {
        Int128 q = a / b;
        if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
        return q;
    }
}
