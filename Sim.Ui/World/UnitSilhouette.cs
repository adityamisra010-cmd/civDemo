using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.World;

/// <summary>The role shape a formation's silhouette takes, from its identity's FAMILY (<see cref="UnitSilhouette.RoleOf"/>).</summary>
public enum SilhouetteRole { Line = 0, Spear = 1, Loose = 2, Missile = 3, Mounted = 4, Engine = 5, Ship = 6, Submarine = 7, Carrier = 8, Air = 9 }

/// <summary>
/// M6 E1.5 — A FORMATION'S SILHOUETTE, keyed by its CURRENT IDENTITY (D-052 §6 "richer unit art (authored silhouettes)";
/// D-038 C3: "where people must appear they are silhouettes or tokens"; C1: NO FIGURE, not now, not later). A silhouette
/// is the FORMATION AS AN OBJECT seen from above the paper — a rank block with its standard, a spear hedge, a loose screen, a
/// mounted wedge, an engine on wheels, a hull with its sails, an aircraft's plan — never a person: no head, no limb, no
/// face, no frame of animation. It is built ONLY of convex polygons of at most <see cref="MaxVertices"/> vertices, at most
/// <see cref="MaxPolygons"/> of them (shadows included), under the one light (<see cref="ObjectPaint.Solid"/>: each solid
/// casts the one contact shadow), inside the token plate's box above the form pips. The identity chooses the role (its
/// family) and the variant (its place in the family's line, <see cref="UnitEmblem.Form"/>), so two identities of one family
/// differ and every token of one identity is drawn alike. The C1 guard is <c>ObjectLayerTests.TheSilhouette_IsConvexSolids_NeverAFigure_…</c>.
/// </summary>
public static class UnitSilhouette
{
    /// <summary>The most vertices one silhouette polygon may have.</summary>
    public const int MaxVertices = 8;

    /// <summary>The most polygons one silhouette may emit (shadows included).</summary>
    public const int MaxPolygons = 12;

    /// <summary>The role shape of family <paramref name="family"/> (the unit-families.json keys).</summary>
    public static SilhouetteRole RoleOf(int family) => family switch
    {
        1 or 22 => SilhouetteRole.Loose,         // light infantry, reconnaissance
        2 => SilhouetteRole.Spear,               // spear / anti-cavalry
        4 => SilhouetteRole.Missile,             // ranged infantry
        5 or 6 or 7 => SilhouetteRole.Mounted,   // light, heavy and ranged cavalry
        8 or 14 => SilhouetteRole.Engine,        // siege, air defence
        9 or 10 or 17 => SilhouetteRole.Ship,    // naval combat, transport, raiders
        18 => SilhouetteRole.Submarine,
        19 => SilhouetteRole.Carrier,
        11 or 12 or 20 or 21 or 23 => SilhouetteRole.Air,   // aviation, missiles, rotary, unmanned, strike
        _ => SilhouetteRole.Line,                // heavy / line infantry and anything unknown
    };

    /// <summary>
    /// Paints the silhouette of <paramref name="emblem"/> on a token plate of half-size <paramref name="s"/> centred on
    /// (<paramref name="x"/>, <paramref name="y"/>): within x ± 0.85s and y − 0.62s … y + 0.44s (the pips sit below, from
    /// y + 0.54s), every solid in the plate's pale emblem ink, its body in the shade stop and its lit facet in the base.
    /// </summary>
    public static void Paint(DrawList d, MapInk k, double x, double y, double s, UnitEmblem emblem)
    {
        int v = Math.Max(1, emblem.Form);
        Rgba pale = k.EmblemMark, body = MapInk.Shade(k.EmblemMark);
        double top = y - 0.62 * s, foot = y + 0.44 * s, left = x - 0.85 * s, right = x + 0.85 * s;
        void Block(double x0, double y0, double x1, double y1, double rise)
        {
            // A rank block seen from above the paper: its front face and its top, the top lit.
            (double X, double Y)[] hull = [(x0, y1), (x1, y1), (x1 + rise, y0 + rise * 0.2), (x1 + rise, y0 - rise * 0.5), (x0 + rise, y0 - rise * 0.5), (x0, y0)];
            ObjectPaint.Solid(d, k, ObjectPaint.Hull(hull), body);
            d.Polygon([(x0, y0), (x1, y0), (x1 + rise, y0 - rise * 0.5), (x0 + rise, y0 - rise * 0.5)], pale);
        }
        void Standard(double px, double base0, double h, bool pennant)
        {
            double pw = Math.Max(0.6, s * 0.06);
            d.Polygon(ObjectPaint.Box(px - pw, base0 - h, px + pw, base0), body);
            double fh = h * 0.34, fw = s * 0.34;
            d.Polygon(pennant
                ? [(px + pw, base0 - h), (px + pw + fw, base0 - h + fh / 2), (px + pw, base0 - h + fh)]
                : ObjectPaint.Box(px + pw, base0 - h, px + pw + fw * 0.8, base0 - h + fh), pale);
        }
        switch (RoleOf(emblem.Glyph))
        {
            case SilhouetteRole.Line:
            {
                bool two = v >= 4;
                double bw = two ? 0.62 * s : 1.0 * s, gap = 0.1 * s, by0 = foot - 0.36 * s;
                if (two)
                {
                    Block(left + 0.06 * s, by0, left + 0.06 * s + bw, foot, 0.12 * s);
                    Block(left + 0.06 * s + bw + gap, by0, left + 0.06 * s + 2 * bw + gap, foot, 0.12 * s);
                }
                else Block(x - bw / 2 - 0.06 * s, by0, x + bw / 2 - 0.06 * s, foot, 0.14 * s);
                Standard(x - 0.06 * s, by0 - 0.06 * s, (foot - top) * 0.62, v % 2 == 1);
                break;
            }
            case SilhouetteRole.Spear:
            {
                double by0 = foot - 0.34 * s, x0 = x - 0.62 * s, x1 = x + 0.5 * s;
                Block(x0, by0, x1, foot, 0.12 * s);
                int spikes = 2 + Math.Min(v, 3);
                for (int i = 0; i < spikes; i++)
                {
                    double sx = x0 + (x1 - x0) * (i + 0.5) / spikes;
                    d.Polygon([(sx - 0.05 * s, by0 - 0.08 * s), (sx + 0.02 * s, top + 0.04 * s), (sx + 0.06 * s, by0 - 0.08 * s)], body);
                }
                break;
            }
            case SilhouetteRole.Loose:
            {
                double bs = 0.26 * s;
                (double, double)[] at = v % 2 == 1 ? [(-0.5, 0.1), (0.05, -0.18), (0.42, 0.16)] : [(-0.46, -0.12), (0.0, 0.18), (0.46, -0.1)];
                foreach ((double ax, double ay) in at)
                {
                    double cx = x + ax * s, cy = y + ay * s;
                    Block(cx - bs / 2, cy - bs * 0.3, cx + bs / 2, cy + bs * 0.6, 0.08 * s);
                }
                break;
            }
            case SilhouetteRole.Missile:
            {
                double by0 = foot - 0.32 * s;
                Block(x - 0.6 * s, by0, x + 0.5 * s, foot, 0.12 * s);
                // The volley's chevron above the rank: one low, wide lit triangle per form band.
                int arcs = 1 + (v - 1) % 2;
                for (int i = 0; i < arcs; i++)
                {
                    double ay = by0 - 0.1 * s - i * 0.2 * s;
                    d.Polygon([(x - 0.5 * s, ay), (x + 0.1 * s, ay - 0.22 * s), (x + 0.6 * s, ay)], i == 0 ? pale : body);
                }
                break;
            }
            case SilhouetteRole.Mounted:
            {
                double len = (emblem.Glyph == 6 ? 0.82 : 0.66) * s, half = (emblem.Glyph == 6 ? 0.34 : 0.26) * s;
                double cx = x - 0.12 * s, cy = y + 0.04 * s;
                (double X, double Y)[] wedge = [(cx - len, cy - half), (cx + len, cy), (cx - len, cy + half)];
                ObjectPaint.Solid(d, k, wedge, body);
                d.Polygon([(cx - len, cy - half), (cx + len, cy), (cx - len * 0.3, cy - half * 0.05)], pale);
                if (emblem.Glyph == 7 || v >= 4) d.Polygon([(cx - len * 0.6, cy - half - 0.18 * s), (cx - len * 0.1, cy - half - 0.02 * s), (cx - len * 0.6, cy - half + 0.02 * s)], body);
                break;
            }
            case SilhouetteRole.Engine:
            {
                double cy = foot - 0.18 * s;
                ObjectPaint.Solid(d, k, ObjectPaint.Box(x - 0.55 * s, cy - 0.22 * s, x + 0.35 * s, cy + 0.08 * s), body);
                double armTop = v >= 4 ? top + 0.06 * s : top + 0.2 * s;
                d.Polygon([(x - 0.12 * s, cy - 0.2 * s), (x + 0.02 * s, cy - 0.22 * s), (x + 0.62 * s, armTop + 0.06 * s), (x + 0.52 * s, armTop)], pale);
                foreach (double wx in new[] { x - 0.4 * s, x + 0.2 * s })
                    d.Polygon(ObjectPaint.Ngon(wx, cy + 0.12 * s, 0.13 * s, 0.13 * s, 6), body);
                break;
            }
            case SilhouetteRole.Ship:
            {
                double deck = foot - 0.24 * s;
                (double X, double Y)[] hull = [(left + 0.02 * s, deck), (right - 0.1 * s, deck), (right - 0.28 * s, foot), (left + 0.2 * s, foot)];
                ObjectPaint.Solid(d, k, hull, body);
                d.Polygon([(left + 0.02 * s, deck), (right - 0.1 * s, deck), (right - 0.14 * s, deck + 0.06 * s), (left + 0.06 * s, deck + 0.06 * s)], pale);
                if (v <= 4)
                {
                    int masts = v >= 3 ? 2 : 1;
                    for (int m = 0; m < masts; m++)
                    {
                        double mx = masts == 1 ? x - 0.05 * s : x - 0.32 * s + m * 0.56 * s;
                        d.Polygon([(mx, deck - 0.04 * s), (mx, top + 0.02 * s), (mx + 0.36 * s, deck - 0.06 * s)], pale);
                    }
                }
                else ObjectPaint.Solid(d, k, ObjectPaint.Box(x - 0.2 * s, deck - 0.3 * s, x + 0.22 * s, deck), body);
                break;
            }
            case SilhouetteRole.Submarine:
            {
                double cy = y + 0.12 * s;
                ObjectPaint.Solid(d, k, ObjectPaint.Ngon(x, cy, 0.8 * s, 0.14 * s, 8), body);
                d.Polygon(ObjectPaint.Box(x - 0.12 * s, cy - 0.34 * s, x + 0.1 * s, cy - 0.1 * s), pale);
                break;
            }
            case SilhouetteRole.Carrier:
            {
                double deck = foot - 0.2 * s;
                ObjectPaint.Solid(d, k, [(left, deck), (right, deck), (right - 0.14 * s, foot), (left + 0.14 * s, foot)], body);
                d.Polygon(ObjectPaint.Box(left, deck - 0.06 * s, right, deck), pale);
                ObjectPaint.Solid(d, k, ObjectPaint.Box(x + 0.3 * s, deck - 0.34 * s, x + 0.48 * s, deck - 0.06 * s), body);
                break;
            }
            case SilhouetteRole.Air:
            {
                double cy = y - 0.06 * s, span = (v >= 3 ? 0.55 : 0.75) * s;
                ObjectPaint.Solid(d, k, [(x + 0.8 * s, cy), (x - 0.5 * s, cy - span * 0.62), (x - 0.5 * s, cy + span * 0.62)], body);
                d.Polygon(ObjectPaint.Box(x - 0.72 * s, cy - 0.06 * s, x + 0.66 * s, cy + 0.06 * s), pale);
                break;
            }
        }
    }
}
