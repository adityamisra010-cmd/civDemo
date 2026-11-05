using Proto.M7.Fixed;

namespace Proto.M7.Spatial;

/// <summary>
/// Heading-based kinematics for vessels and aircraft (lane N). Integer only: headings are <see cref="FixedTrig"/> binary
/// angle units, speeds mm/s, accelerations mm/s^2, turn rates angle units per second, time in ms. Every per-step quantity is
/// derived from a per-second rate and the step length (dt-correct, CLAUDE.md law 3): a turn of rate w over dt ms is at most
/// floor(w * dt / 1000) units; a speed change is at most floor(a * dt / 1000) mm/s; a displacement is speed * dt / 1000 mm
/// along the heading, rounded toward zero per axis (sign-symmetric, so mirror-image movers move mirror-image distances).
/// </summary>
public static class HeadingMotion
{
    /// <summary>Largest heading change allowed over dtMs at turn rate w (angle units per second).</summary>
    public static long MaxTurn(long turnRatePerS, long dtMs) => FixedMath.MulDivFloor(turnRatePerS, dtMs, 1000);

    /// <summary>
    /// Turns from heading toward desired by at most maxTurn units, along the shorter way. Exactly opposite headings
    /// (difference -HalfTurn) turn in the negative (clockwise) sense — a fixed, documented tie rule.
    /// </summary>
    public static int TurnToward(int heading, int desired, long maxTurn)
    {
        if (maxTurn < 0) throw new ArgumentOutOfRangeException(nameof(maxTurn));
        int d = FixedTrig.Diff(heading, desired);
        if (d > maxTurn) d = (int)maxTurn;
        else if (d < -maxTurn) d = (int)-maxTurn;
        return FixedTrig.Norm((long)heading + d);
    }

    /// <summary>Speed moved toward target by at most accel * dt / 1000, clamped to [min, max].</summary>
    public static long Approach(long speed, long target, long accelPerS2, long dtMs, long min, long max)
    {
        long dv = FixedMath.MulDivFloor(accelPerS2, dtMs, 1000);
        long s = speed;
        if (target > s) s = Math.Min(target, s + dv);
        else if (target < s) s = Math.Max(target, s - dv);
        if (s < min) s = min;
        if (s > max) s = max;
        return s;
    }

    /// <summary>Displacement of distanceMm along heading (toward-zero rounding per axis).</summary>
    public static Vec2L Offset(int heading, long distanceMm)
        => new(FixedTrig.ScaleCos(distanceMm, heading), FixedTrig.ScaleSin(distanceMm, heading));

    /// <summary>Distance covered in dtMs at speed mm/s (floor).</summary>
    public static long Distance(long speedMmPerS, long dtMs) => FixedMath.MulDivFloor(speedMmPerS, dtMs, 1000);

    /// <summary>
    /// Arrival speed cap: the largest speed from which the vessel can still stop within distMm at deceleration a,
    /// floor(sqrt(2 * a * dist)) (exact integer square root).
    /// </summary>
    public static long BrakingSpeed(long distMm, long decelPerS2)
    {
        if (distMm <= 0 || decelPerS2 <= 0) return 0;
        return (long)FixedMath.ISqrt((UInt128)((Int128)2 * decelPerS2 * distMm));
    }
}

/// <summary>
/// The NAVAL VIEW of a tactical grid: the same width, height and cell size, with every Water cell mapped to Open and every
/// other cell mapped to Wall (heights zero). Running the unchanged Slice C planner, separation step and exit field on this
/// view gives water-only movement with no new pathfinding code: a vessel path never touches a land cell, and a vessel's
/// separation step never enters one (both by the existing passability rules). The view is for MOVEMENT only; line of sight
/// and fire always use the real grid.
/// </summary>
public static class NavalView
{
    public static TerrainGrid Build(TerrainGrid g)
    {
        var v = new TerrainGrid(g.Width, g.Height, g.CellSizeMm, TerrainClass.Wall);
        for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
                if (g.ClassAt(x, y) == TerrainClass.Water) v.Set(x, y, TerrainClass.Open);
        return v;
    }

    public static bool IsWater(TerrainGrid g, long cx, long cy) => g.InBounds(cx, cy) && g.ClassAt(cx, cy) == TerrainClass.Water;

    /// <summary>True when the position lies on an in-bounds water cell.</summary>
    public static bool OnWater(TerrainGrid g, Vec2L p)
    {
        var c = g.CellOf(p);
        return IsWater(g, c.X, c.Y);
    }
}
