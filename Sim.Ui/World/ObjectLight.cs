using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.World;

/// <summary>
/// M6 E1.1 — THE OBJECT LAYER'S ONE LIGHT (D-038 Part B1: "a single global light direction, stated once as a constant and
/// never varied per asset, with a contact shadow"; style bible §1 two-layer rule, §8 object-layer conventions).
/// <para>The substrate (paper, washes, coasts, rivers, territory tint, UI furniture) stays lit flat — nothing here touches
/// it. Every OBJECT the lens draws (relief, forests, fields, settlement parts, dwellings, formation silhouettes) is lit by
/// this one light, from the upper left (north-west, the cartographer's convention), and casts ONE kind of shadow — the
/// contact shadow of <see cref="ObjectPaint.Solid"/>: its silhouette translated along <see cref="ShadowDirX"/>,
/// <see cref="ShadowDirY"/> by <see cref="ShadowLength"/> × its height. No painter states a direction of its own (the
/// single-light test proves every shadow's offset is this constant's), and every lit and shade face takes the stops
/// below (<see cref="MapInk.Lit"/>, <see cref="MapInk.Shade"/>).</para>
/// Presentation only: nothing in the simulation reads it (ADR-009).
/// </summary>
public static class ObjectLight
{
    /// <summary>The direction the light COMES FROM, degrees clockwise from screen north: 315 = the north-west.</summary>
    public const double FromDeg = 315.0;

    /// <summary>The shadow's direction on the paper, a screen-space unit vector (x right, y down): away from the light and
    /// flattened onto the ground plane — two east to one south, (2, 1)/√5. Stated once; never varied per asset.</summary>
    public static readonly double ShadowDirX = 2.0 / Math.Sqrt(5.0), ShadowDirY = 1.0 / Math.Sqrt(5.0);

    /// <summary>The contact shadow's offset as a fraction of the casting object's height on screen.</summary>
    public const double ShadowLength = 0.16;

    /// <summary>The contact shadow's opacity (<see cref="MapInk.ObjectShadow"/> at this alpha).</summary>
    public const double ShadowAlpha = 0.32;

    /// <summary>The LIT stop: a face turned to the light is its base moved this far toward the paper (bible PaperLight).</summary>
    public const double LitStop = 0.38;

    /// <summary>The SHADE stop: a face turned away is its base moved this far toward the ink (bible InkPrimary).</summary>
    public const double ShadeStop = 0.30;

    /// <summary>Below this height (screen px) an object is drawn FLAT, with no shadow and no faces: a shadow under a pixel
    /// is invisible, and the commands would only cost frame time. One threshold for every object.</summary>
    public const double MinShadedPx = 3.0;

    /// <summary>The contact shadow's offset for an object <paramref name="heightPx"/> tall on screen.</summary>
    public static (double Dx, double Dy) ShadowOffset(double heightPx) =>
        (ShadowDirX * ShadowLength * heightPx, ShadowDirY * ShadowLength * heightPx);
}

/// <summary>
/// M6 E1.1 — THE SHARED OBJECT PAINTER: the one place a contact shadow is drawn (<see cref="Solid"/>) and the convex
/// geometry every object part is built from. The ImGui backend fills CONVEX polygons only, so every polygon an object
/// emits is convex by construction (a hull, a box, a triangle, a regular n-gon).
/// </summary>
public static class ObjectPaint
{
    /// <summary>
    /// A SOLID OBJECT: its contact shadow — <paramref name="silhouette"/> translated by
    /// <see cref="ObjectLight.ShadowOffset"/> of its own height, in <see cref="MapInk.ObjectShadow"/> at
    /// <see cref="ObjectLight.ShadowAlpha"/> — then the silhouette itself filled with <paramref name="body"/> (the caller
    /// draws its lit and shade faces over it). The shadow is ALWAYS immediately followed by its caster, with the same
    /// vertex count: the single-light test pairs them so. Below <see cref="ObjectLight.MinShadedPx"/> no shadow is cast.
    /// </summary>
    public static void Solid(DrawList d, MapInk k, (double X, double Y)[] silhouette, Rgba body)
    {
        double top = double.PositiveInfinity, bottom = double.NegativeInfinity;
        foreach ((double _, double y) in silhouette) { top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
        double h = bottom - top;
        if (h >= ObjectLight.MinShadedPx)
        {
            (double dx, double dy) = ObjectLight.ShadowOffset(h);
            var shadow = new (double X, double Y)[silhouette.Length];
            for (int i = 0; i < silhouette.Length; i++) shadow[i] = (silhouette[i].X + dx, silhouette[i].Y + dy);
            // M6 integration (E1 × the continuous level of detail, EasedZoomAndContinuousLodTests): the shadow must not POP. It
            // fades in as its caster grows from MinShadedPx to twice that, and with the caster's own opacity, so a zoom that
            // carries an object across the threshold (or cross-fades it in) never shows a shadow at full strength at once.
            double fade = Math.Clamp((h - ObjectLight.MinShadedPx) / ObjectLight.MinShadedPx, 0.0, 1.0) * (body.A / 255.0);
            d.Polygon(shadow, fade >= 1.0 ? ShadowInk(k) : Ink.With(k.ObjectShadow, ObjectLight.ShadowAlpha * fade));
        }
        d.Polygon(silhouette, body);
    }

    /// <summary>The shadow fill: <see cref="MapInk.ObjectShadow"/> at <see cref="ObjectLight.ShadowAlpha"/>.</summary>
    public static Rgba ShadowInk(MapInk k) => Ink.With(k.ObjectShadow, ObjectLight.ShadowAlpha);

    /// <summary>An axis-aligned box as a polygon (clockwise from the top-left).</summary>
    public static (double X, double Y)[] Box(double x0, double y0, double x1, double y1) =>
        [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];

    /// <summary>A regular n-gon of radius <paramref name="rx"/> × <paramref name="ry"/> about (cx, cy), the first vertex at
    /// 12 o'clock (convex).</summary>
    public static (double X, double Y)[] Ngon(double cx, double cy, double rx, double ry, int n)
    {
        var p = new (double X, double Y)[n];
        for (int i = 0; i < n; i++)
        {
            double a = 2 * Math.PI * i / n;
            p[i] = (cx + rx * Math.Sin(a), cy - ry * Math.Cos(a));
        }
        return p;
    }

    /// <summary>THE CONVEX HULL of <paramref name="pts"/> (Andrew's monotone chain; points sorted by the composite key
    /// (x, y) — exact doubles, ties resolved by the sort's total order on the pair), clockwise on screen (y down). Collinear
    /// points are dropped. A degenerate input returns its distinct points.</summary>
    public static (double X, double Y)[] Hull(IReadOnlyList<(double X, double Y)> pts)
    {
        var p = new List<(double X, double Y)>(pts);
        p.Sort(static (a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        var dedup = new List<(double X, double Y)>(p.Count);
        foreach ((double X, double Y) q in p) if (dedup.Count == 0 || dedup[^1] != q) dedup.Add(q);
        if (dedup.Count < 3) return [.. dedup];
        static double Cross((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) =>
            (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var hull = new (double X, double Y)[2 * dedup.Count];
        int k = 0;
        for (int i = 0; i < dedup.Count; i++)
        {
            while (k >= 2 && Cross(hull[k - 2], hull[k - 1], dedup[i]) <= 0) k--;
            hull[k++] = dedup[i];
        }
        for (int i = dedup.Count - 2, t = k + 1; i >= 0; i--)
        {
            while (k >= t && Cross(hull[k - 2], hull[k - 1], dedup[i]) <= 0) k--;
            hull[k++] = dedup[i];
        }
        return hull[..(k - 1)];
    }

    /// <summary>Whether the polygon is convex (every turn the same way, collinear runs allowed) — the C1 guard's and the
    /// backend's requirement.</summary>
    public static bool IsConvex(IReadOnlyList<(double X, double Y)> pts)
    {
        int n = pts.Count;
        if (n < 3) return true;
        int sign = 0;
        for (int i = 0; i < n; i++)
        {
            (double X, double Y) a = pts[i], b = pts[(i + 1) % n], c = pts[(i + 2) % n];
            double cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (Math.Abs(cross) < 1e-9) continue;
            int s = Math.Sign(cross);
            if (sign == 0) sign = s;
            else if (s != sign) return false;
        }
        return true;
    }

    /// <summary>
    /// A BUILDING under the one light: a wall box <paramref name="w"/> wide, <paramref name="h"/> tall, standing on
    /// (<paramref name="cx"/>, <paramref name="baseY"/>), with a roof <paramref name="roofH"/> tall of
    /// <paramref name="roof"/> form, and a side wall receding to the east by <paramref name="depth"/> (the shade side —
    /// the light is from the north-west). Drawn as: the solid (shadow + hull in the shade of the wall), the front wall,
    /// the roof's lit (west) and shade (east) pitches. Every polygon convex.
    /// </summary>
    public static void Building(DrawList d, MapInk k, double cx, double baseY, double w, double h, double roofH, double depth,
        RoofForm roof, Rgba wall, Rgba roofInk)
    {
        double x0 = cx - w / 2, x1 = cx + w / 2, top = baseY - h, apex = top - roofH;
        double dx = depth, dy = -depth * 0.5;   // the receding side, up and to the east
        var outline = new List<(double X, double Y)>
        {
            (x0, baseY), (x1, baseY), (x1 + dx, baseY + dy), (x1 + dx, top + dy), (x0, top),
        };
        switch (roof)
        {
            case RoofForm.Gable: outline.Add((cx, apex)); outline.Add((cx + dx, apex + dy)); break;
            case RoofForm.Dome: outline.AddRange(DomeLit(cx, top, w * 0.42, roofH)); break;
            default: break;   // Flat: the hull is the box and its side
        }
        (double X, double Y)[] hull = Hull(outline);
        if (h + roofH < ObjectLight.MinShadedPx)
        {
            d.Polygon(hull, wall);   // FLAT below the shading threshold: one polygon, no faces, no shadow
            return;
        }
        Solid(d, k, hull, MapInk.Shade(wall));
        d.Polygon(Box(x0, top, x1, baseY), wall);
        switch (roof)
        {
            case RoofForm.Gable:
                d.Polygon([(x0, top), (cx, apex), (cx, top)], MapInk.Lit(roofInk));
                d.Polygon([(cx, apex), (cx + dx, apex + dy), (x1 + dx, top + dy), (x1, top), (cx, top)], roofInk);
                break;
            case RoofForm.Dome:
                d.Polygon(DomeLit(cx, top, w * 0.42, roofH), MapInk.Lit(roofInk));
                break;
            default:
                d.Polygon([(x0, top), (x1, top), (x1 + dx, top + dy), (x0 + dx, top + dy)], MapInk.Lit(roofInk));
                break;
        }
    }

    /// <summary>The lit dome: the upper half-ellipse of radius <paramref name="rx"/> × <paramref name="ry"/> standing on
    /// (cx, top) — convex.</summary>
    private static (double X, double Y)[] DomeLit(double cx, double top, double rx, double ry)
    {
        const int n = 7;
        var p = new (double X, double Y)[n];
        for (int i = 0; i < n; i++)
        {
            double a = Math.PI * i / (n - 1);   // 0 .. π: from the west foot over the crown to the east foot
            p[i] = (cx - rx * Math.Cos(a), top - ry * Math.Sin(a));
        }
        return p;
    }
}

/// <summary>A building's roof form (E1.4: the register chooses it from realized state, never from the Age).</summary>
public enum RoofForm { Flat = 0, Gable = 1, Dome = 2 }
