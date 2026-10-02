using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;
using static Sim.Ui.Theme.ThemeColor;

namespace Sim.Ui.Theme;

/// <summary>The meaning of a state mark — the same five meanings in every era (recognisable icons).</summary>
public enum MarkKind { Completed = 0, Active = 1, Available = 2, Locked = 3, Partial = 4 }

/// <summary>
/// ERA MARKS — the icons and progress devices the screens share, drawn in the era's hand
/// (<see cref="IconTokens"/>, <see cref="ControlTokens"/>). The MEANING of every mark is fixed — a
/// check is completed, a ring with an arc is being researched, a dot is available, a padlock is
/// locked — so a player never relearns the icons; only the hand changes: daubed charcoal at A1,
/// incised clay at A2, cast and forged metal at A3–A4, carved and illuminated at A5–A6, engraved at
/// A7, technical at A8, precise at A9. Seeded wobble only (<see cref="FrameNoise"/>).
/// </summary>
public static class EraMarks
{
    /// <summary>A state mark centred at (<paramref name="cx"/>, <paramref name="cy"/>), radius
    /// <paramref name="r"/>. <paramref name="fraction"/> is the progress an Active mark's arc shows.</summary>
    public static void State(DrawList d, EraTheme t, double cx, double cy, double r, MarkKind kind, double fraction, int id, bool dim = false)
    {
        SemanticTokens s = t.Semantic;
        IconTokens ic = t.Icons;
        double w = Math.Max(1.0, ic.StrokePx * r / 7.0);
        switch (kind)
        {
            case MarkKind.Completed:
            {
                Rgba gold = s.Completed, ink = t.Material.Panel;
                switch (ic.Style)
                {
                    case IconStyle.Daubed:
                        d.Polygon(Blob(cx, cy, r * 1.05, id), gold);
                        d.Polyline(Wobble([(cx - r * 0.5, cy), (cx - r * 0.12, cy + r * 0.42), (cx + r * 0.55, cy - r * 0.45)], ic.WobblePx * 0.4, id, 3), t.Ink.Text, w * 0.9);
                        break;
                    case IconStyle.Cast:
                        d.Polygon([(cx, cy - r * 1.1), (cx + r * 1.1, cy), (cx, cy + r * 1.1), (cx - r * 1.1, cy)], gold);
                        Check(d, cx, cy, r * 0.9, ink, w);
                        break;
                    case IconStyle.Forged:
                    case IconStyle.Technical:
                    {
                        var box = new RectD(cx - r, cy - r, 2 * r, 2 * r);
                        if (ic.Style == IconStyle.Forged) d.Rect(box, gold);
                        else d.Rect(box, Alpha(gold, 0.2), gold, w * 0.8);
                        Check(d, cx, cy, r * 0.9, ic.Style == IconStyle.Forged ? ink : t.Ink.Text, w);
                        break;
                    }
                    case IconStyle.Engraved:
                    {
                        d.Circle(cx, cy, r, null, gold, w * 0.8);
                        for (double k = -r * 0.6; k <= r * 0.6; k += r * 0.3)   // engraved hatching
                        {
                            double h = Math.Sqrt(Math.Max(0, r * r * 0.7 - k * k));
                            d.Line(cx + k - h * 0.5, cy + h * 0.5, cx + k + h * 0.5, cy - h * 0.5, Alpha(gold, 0.45), 0.6);
                        }
                        Check(d, cx, cy, r * 0.8, t.Ink.Text, w * 0.9);
                        break;
                    }
                    case IconStyle.Incised:
                        d.Circle(cx, cy, r, gold, t.Ink.Rule, w * 0.6);
                        d.Circle(cx, cy, r * 0.62, null, Alpha(ink, 0.8), w * 0.5);
                        Check(d, cx, cy, r * 0.7, t.Ink.Text, w * 0.9);
                        break;
                    default:   // Carved, Illuminated, Precise: the disc and its check
                        d.Circle(cx, cy, r, gold, ic.Style == IconStyle.Illuminated ? t.Ink.Rule : null, 0.8);
                        Check(d, cx, cy, r * 0.85, ic.Style == IconStyle.Precise ? ink : t.Ink.Text, w);
                        break;
                }
                break;
            }
            case MarkKind.Active:
            {
                Rgba c = s.Active;
                double f = Math.Max(0.02, Math.Clamp(fraction, 0, 1));
                if (ic.Style == IconStyle.Daubed)
                {
                    d.Polyline(Ring(cx, cy, r, id, ic.WobblePx * 0.5), c, w * 0.7, closed: true);
                    d.Arc(cx, cy, r, 0, 360 * f, c, w * 1.6);
                    d.Circle(cx, cy, r * 0.32, c);
                }
                else if (ic.Style == IconStyle.Technical)
                {
                    d.Circle(cx, cy, r, null, c, w * 0.7);
                    d.Arc(cx, cy, r, 0, 360 * f, c, w * 1.5);
                    d.Line(cx - r * 1.35, cy, cx - r * 0.55, cy, c, 0.8);
                    d.Line(cx + r * 0.55, cy, cx + r * 1.35, cy, c, 0.8);
                    d.Line(cx, cy - r * 1.35, cx, cy - r * 0.55, c, 0.8);
                }
                else
                {
                    d.Circle(cx, cy, r, null, c, w * 0.8);
                    d.Arc(cx, cy, r, 0, 360 * f, c, w * 1.6);
                }
                break;
            }
            case MarkKind.Available:
            {
                Rgba c = s.Available;
                if (ic.Style == IconStyle.Daubed) d.Polygon(Blob(cx, cy, r * 0.62, id), c);
                else if (ic.Style is IconStyle.Incised or IconStyle.Carved or IconStyle.Engraved) d.Circle(cx, cy, r * 0.6, null, c, w);
                else if (ic.Style == IconStyle.Cast) d.Polygon([(cx, cy - r * 0.75), (cx + r * 0.75, cy), (cx, cy + r * 0.75), (cx - r * 0.75, cy)], c);
                else if (ic.Style == IconStyle.Technical) d.Rect(new RectD(cx - r * 0.7, cy - r * 0.7, r * 1.4, r * 1.4), null, c, w * 0.8);
                else d.Circle(cx, cy, r * 0.62, c);
                break;
            }
            default:   // Locked / Partial: the padlock, in grey or in the retained-progress amber
            {
                Rgba c = kind == MarkKind.Partial ? s.Progress : dim ? Alpha(s.Locked, 0.85) : s.Locked;
                if (ic.Style == IconStyle.Daubed)
                {
                    d.Polygon(Blob(cx, cy + r * 0.3, r * 0.72, id), c);
                    d.Polyline(Wobble([(cx - r * 0.42, cy + r * 0.1), (cx - r * 0.42, cy - r * 0.45), (cx, cy - r * 0.78), (cx + r * 0.42, cy - r * 0.45), (cx + r * 0.42, cy + r * 0.1)], ic.WobblePx * 0.35, id, 7), c, w * 0.75);
                }
                else
                {
                    d.Rect(new RectD(cx - r * 0.72, cy - r * 0.14, r * 1.44, r * 1.14), c, null, 1.0, ic.Style == IconStyle.Precise ? r * 0.2 : 0);
                    d.Arc(cx, cy - r * 0.14, r * 0.48, 270, 180, c, w * 0.8);
                }
                break;
            }
        }
    }

    /// <summary>A check mark centred near (cx, cy), size s.</summary>
    public static void Check(DrawList d, double cx, double cy, double s, Rgba c, double w)
    {
        d.Line(cx - s * 0.48, cy, cx - s * 0.12, cy + s * 0.4, c, w);
        d.Line(cx - s * 0.12, cy + s * 0.4, cx + s * 0.54, cy - s * 0.4, c, w);
    }

    /// <summary>A milestone/requirement tick: a check when met, an empty box (or scratched ring at A1)
    /// when not, top-left at (x, y), size s.</summary>
    public static void Tick(DrawList d, EraTheme t, double x, double y, double s, bool met, Rgba c, int id)
    {
        double w = Math.Max(1.2, t.Icons.StrokePx * 0.85);
        if (met)
        {
            if (t.Icons.Style == IconStyle.Daubed)
                d.Polyline(Wobble([(x + 1, y + s * 0.55), (x + s * 0.4, y + s * 0.92), (x + s, y + s * 0.1)], t.Icons.WobblePx * 0.35, id, 9), c, w);
            else Check(d, x + s * 0.5, y + s * 0.5, s * 0.95, c, w);
            return;
        }
        if (t.Icons.Style == IconStyle.Daubed) d.Polyline(Ring(x + s / 2, y + s / 2, s * 0.42, id, t.Icons.WobblePx * 0.4), c, w * 0.8, closed: true);
        else if (t.Edge.Corner is CornerStyle.Organic or CornerStyle.Rounded) d.Circle(x + s / 2, y + s / 2, s * 0.42, null, c, w * 0.8);
        else d.Rect(new RectD(x + 1, y + 1, s - 2, s - 2), null, c, w * 0.8, t.Edge.Corner == CornerStyle.Fine ? 1.5 : 0);
    }

    /// <summary>
    /// PROGRESS in the era's representation inside <paramref name="r"/>: carved tally notches (A1),
    /// clay counters (A2), segmented plates (A3–A5), a bar (A6–A7), a graduated bar (A8–A9).
    /// <paramref name="fraction"/> in [0,1]; <paramref name="fill"/> the progress colour.
    /// </summary>
    public static void Progress(DrawList d, EraTheme t, RectD r, double fraction, Rgba fill, int id)
    {
        double f = double.IsNaN(fraction) ? 0 : Math.Clamp(fraction, 0, 1);
        int n = Math.Max(1, t.Controls.ProgressSegments);
        Rgba empty = Alpha(t.Ink.TextDim, 0.45);
        switch (t.Controls.Progress)
        {
            case ProgressStyle.Notches when t.Icons.Style == IconStyle.Daubed:
            {
                // tally marks: n strokes, groups of five struck through
                int filled = f > 0 ? Math.Max(1, (int)Math.Round(f * n)) : 0;
                double step = r.W / (n + n / 5 + 0.5);
                double x = r.X + step * 0.5;
                for (int i = 0; i < n; i++)
                {
                    if (i > 0 && i % 5 == 0) x += step;
                    double jx = FrameNoise.S(id, 101, i) * 0.6, jy = FrameNoise.S(id, 102, i) * 0.8;
                    d.Line(x + jx, r.Y + jy, x - jx, r.Bottom + jy * 0.5, i < filled ? fill : empty, i < filled ? 1.8 : 1.1);
                    if (i % 5 == 4 && i < filled)
                        d.Line(x - step * 4.3, r.Bottom - r.H * 0.2, x + step * 0.4, r.Y + r.H * 0.25, fill, 1.4);
                    x += step;
                }
                break;
            }
            case ProgressStyle.Notches:
            {
                // clay counters: n small tokens, pressed (filled) for progress made
                int filled = f > 0 ? Math.Max(1, (int)Math.Round(f * n)) : 0;
                double step = r.W / n, rad = Math.Min(step * 0.32, r.H * 0.45);
                for (int i = 0; i < n; i++)
                {
                    double cx = r.X + step * (i + 0.5), cy = r.CenterY;
                    if (i < filled) d.Circle(cx, cy, rad, fill);
                    else d.Circle(cx, cy, rad, null, empty, 0.9);
                }
                break;
            }
            case ProgressStyle.Segments:
            {
                double gap = Math.Max(1.0, r.W / n * 0.12), sw = (r.W - gap * (n - 1)) / n;
                for (int i = 0; i < n; i++)
                {
                    var seg = new RectD(r.X + i * (sw + gap), r.Y, sw, r.H);
                    d.Rect(seg, Alpha(t.Material.PanelSunken, 0.55));
                    double part = Math.Clamp(f * n - i, 0, 1);
                    if (part > 0) d.Rect(new RectD(seg.X, seg.Y, seg.W * part, seg.H), fill);
                    d.Rect(seg, null, Alpha(t.Material.Border, 0.55), 0.6);
                }
                break;
            }
            case ProgressStyle.GraduatedBar:
            {
                d.Rect(r, Alpha(t.Material.PanelSunken, 0.6));
                if (f > 0) d.Rect(new RectD(r.X, r.Y, r.W * f, r.H), fill);
                d.Rect(r, null, Alpha(t.Material.Border, 0.6), 0.6);
                for (int i = 1; i < n; i++)
                {
                    double x = r.X + r.W * i / n;
                    bool major = n >= 10 && i % (n / 2) == 0;
                    d.Line(x, r.Bottom, x, r.Bottom - (major ? r.H : r.H * 0.5), Alpha(t.Material.Border, 0.55), 0.5);
                }
                break;
            }
            default:
                d.Bar(r, f, fill, Alpha(t.Material.Border, 0.6), Alpha(t.Material.PanelSunken, 0.6));
                break;
        }
    }

    /// <summary>The close glyph (×) inside <paramref name="r"/>.</summary>
    public static void Close(DrawList d, EraTheme t, RectD r, Rgba c, int id)
    {
        double ix = r.W * 0.3, iy = r.H * 0.3, w = Math.Max(1.4, t.Icons.StrokePx * 0.8);
        if (t.Icons.Style == IconStyle.Daubed)
        {
            d.Polyline(Wobble([(r.X + ix, r.Y + iy), (r.Right - ix, r.Bottom - iy)], t.Icons.WobblePx * 0.5, id, 11), c, w);
            d.Polyline(Wobble([(r.Right - ix, r.Y + iy), (r.X + ix, r.Bottom - iy)], t.Icons.WobblePx * 0.5, id, 12), c, w);
            return;
        }
        d.Line(r.X + ix, r.Y + iy, r.Right - ix, r.Bottom - iy, c, w);
        d.Line(r.Right - ix, r.Y + iy, r.X + ix, r.Bottom - iy, c, w);
    }

    /// <summary>A transition arrow from x0 to x1 at y (Age from → to, unit from → to).</summary>
    public static void Arrow(DrawList d, EraTheme t, double x0, double y, double x1, Rgba c, int id)
    {
        double w = Math.Max(1.4, t.Icons.StrokePx * 0.9), h = 5 + t.Icons.StrokePx;
        if (t.Icons.Style == IconStyle.Daubed) d.Polyline(PanelFrame.Freehand(x0, y, x1 - h, y, t.Icons.WobblePx * 0.6, id, 13), c, w);
        else d.Line(x0, y, x1 - h, y, c, w);
        d.Polygon([(x1 - h, y - h * 0.6), (x1, y), (x1 - h, y + h * 0.6)], c);
    }

    // ------------------------------------------------------------------ freehand helpers

    /// <summary>A smooth, convex blob of radius r (a low-frequency wobble that keeps the curve convex).</summary>
    private static (double X, double Y)[] Blob(double cx, double cy, double r, int id)
    {
        const int n = 14;
        double p2 = FrameNoise.U(id, 111, 0) * Math.PI * 2, p3 = FrameNoise.U(id, 112, 0) * Math.PI * 2;
        var pts = new (double, double)[n];
        for (int i = 0; i < n; i++)
        {
            double a = Math.PI * 2 * i / n;
            double rr = r * (1 + 0.08 * Math.Sin(2 * a + p2) + 0.05 * Math.Sin(3 * a + p3));
            pts[i] = (cx + Math.Cos(a) * rr, cy + Math.Sin(a) * rr);
        }
        return pts;
    }

    private static (double X, double Y)[] Ring(double cx, double cy, double r, int id, double amp)
    {
        const int n = 16;
        var pts = new (double, double)[n];
        for (int i = 0; i < n; i++)
        {
            double a = Math.PI * 2 * i / n;
            double rr = r + FrameNoise.S(id, 113, i) * amp;
            pts[i] = (cx + Math.Cos(a) * rr, cy + Math.Sin(a) * rr);
        }
        return pts;
    }

    private static (double X, double Y)[] Wobble((double X, double Y)[] pts, double amp, int id, int salt)
    {
        var r = new (double, double)[pts.Length];
        for (int i = 0; i < pts.Length; i++) r[i] = (pts[i].X + FrameNoise.S(id, salt, i * 2) * amp, pts[i].Y + FrameNoise.S(id, salt, i * 2 + 1) * amp);
        return r;
    }
}
