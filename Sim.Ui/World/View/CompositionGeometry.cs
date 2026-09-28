using Sim.Ui.World.Content;

namespace Sim.Ui.World.View;

/// <summary>One lot of a composition lattice, in sprite units around the settlement centre
/// (x right, y down; angles clockwise from 12 o'clock).</summary>
public readonly record struct LotGeometry(int Ring, int Index, double AngleDeg, double X, double Y);

/// <summary>
/// The fixed geometry of a composed settlement sprite. Pure functions of the content layout
/// (and, for residential blocks, a per-settlement rotation drawn from the stable hash): no
/// stage, population or structure count enters, so a lot never moves. Trig produces final
/// coordinates only; every yes/no decision downstream compares these fixed numbers.
/// </summary>
public static class CompositionGeometry
{
    /// <summary>Structure slots, ring-major (ring 0 first), lot index ascending within a ring.</summary>
    public static LotGeometry[] SlotCentres(CompositionLayout layout) => Lots(layout.SlotRings, _ => 0.0);

    /// <summary>Residential block lots for one settlement, ring-major. Each ring is rotated by
    /// a whole number of tenths of a degree drawn from (settlement key, ring) so settlements do
    /// not all look stamped from one die; the rotation never changes for a settlement.</summary>
    public static LotGeometry[] BlockCentres(CompositionLayout layout, string settlementKey) =>
        Lots(layout.BlockRings, ring => StableHash.Index(StableHash.Of(settlementKey, "block-ring-" + ring.ToString(System.Globalization.CultureInfo.InvariantCulture)), 3600) / 10.0);

    private static LotGeometry[] Lots(IReadOnlyList<LotRing> rings, Func<int, double> rotationDeg)
    {
        int n = 0;
        foreach (LotRing r in rings) n += r.Lots;
        var lots = new LotGeometry[n];
        int at = 0;
        for (int ri = 0; ri < rings.Count; ri++)
        {
            LotRing ring = rings[ri];
            double rot = rotationDeg(ri);
            for (int k = 0; k < ring.Lots; k++)
            {
                double deg = ring.AngleDeg(k) + rot;
                double a = deg * Math.PI / 180.0;
                lots[at++] = new LotGeometry(ri, k, deg, ring.Radius * Math.Sin(a), -ring.Radius * Math.Cos(a));
            }
        }
        return lots;
    }

    /// <summary>Two axis-aligned squares (centre, side) overlap.</summary>
    public static bool BoxesOverlap(double ax, double ay, double aSize, double bx, double by, double bSize)
    {
        double reach = (aSize + bSize) / 2.0;
        return Math.Abs(ax - bx) < reach && Math.Abs(ay - by) < reach;
    }

    /// <summary>An axis-aligned square (centre, side) lies wholly outside a circle at the origin.</summary>
    public static bool BoxClearsCircle(double x, double y, double size, double radius)
    {
        double h = size / 2.0;
        double nx = Math.Clamp(0.0, x - h, x + h), ny = Math.Clamp(0.0, y - h, y + h);
        return nx * nx + ny * ny >= radius * radius;
    }
}
