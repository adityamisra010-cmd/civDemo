namespace Sim.Ui.Render;

/// <summary>
/// OFF-SCREEN CULLING for a <see cref="DrawList"/> before it is replayed into a renderer (M5 hardening H1).
/// A screen paints in screen pixels and may paint more than is visible (a tree scrolled half off the canvas, a
/// field texture under a bar, an action surface scrolled inside its panel). ImGui does not discard geometry
/// outside the clip rect: every command still costs its vertices, and a 16-bit-index draw list holds at most
/// 65,536 of them (the Research-screen crash). This pass drops every command whose CONSERVATIVE bounds lie
/// entirely outside the visible rect — the replay target's clip rect intersected with the list's own clip
/// pushes — and keeps everything else, so what is on screen is unchanged by construction.
///
/// Pure and headless: bounds are over-estimates (a text run is bounded by one em per character plus its
/// tracking and bold offset; every stroke by its width; every shape by an anti-aliasing margin), so a command
/// that could touch a visible pixel is never culled. Hit regions are untouched — screens compute them while
/// painting, independently of what is replayed.
/// </summary>
public static class DrawListCull
{
    /// <summary>Pixels added around every bound: ImGui's anti-aliased fringe reaches ~1 px past the geometry.</summary>
    public const double FringePx = 2.0;

    /// <summary>The commands of <paramref name="list"/> that can touch a pixel of <paramref name="visible"/>,
    /// in their original order. Clip pushes and pops are always kept (their pairing is preserved) and narrow the
    /// visible rect exactly as ImGui's <c>PushClipRect(intersect_with_current: true)</c> does.</summary>
    public static List<DrawCmd> Visible(IReadOnlyList<DrawCmd> list, RectD visible, out int culled)
    {
        var kept = new List<DrawCmd>(list.Count);
        var stack = new Stack<RectD>();
        RectD clip = visible;
        culled = 0;
        foreach (DrawCmd cmd in list)
        {
            switch (cmd)
            {
                case ClipPushCmd push:
                    stack.Push(clip);
                    clip = Intersect(clip, push.Rect);
                    kept.Add(cmd);
                    continue;
                case ClipPopCmd:
                    clip = stack.Count > 0 ? stack.Pop() : visible;
                    kept.Add(cmd);
                    continue;
            }
            if (Bounds(cmd) is RectD b && !Touches(b, clip)) { culled++; continue; }
            kept.Add(cmd);
        }
        return kept;
    }

    /// <summary>The conservative screen bounds of a drawing command (including stroke width and the AA fringe),
    /// or null for a command with no extent (clip push/pop) or an unknown kind, which is always kept.</summary>
    public static RectD? Bounds(DrawCmd cmd)
    {
        switch (cmd)
        {
            case RectCmd r:
                return Grow(r.Rect, (r.Stroke is null ? 0 : r.StrokeWidth) + FringePx);
            case LineCmd l:
                return Span(Math.Min(l.X0, l.X1), Math.Min(l.Y0, l.Y1), Math.Max(l.X0, l.X1), Math.Max(l.Y0, l.Y1), l.Width + FringePx);
            case BezierCmd b:
            {
                // A cubic Bezier lies inside the convex hull of its four control points.
                double x0 = Math.Min(Math.Min(b.P0.X, b.P1.X), Math.Min(b.P2.X, b.P3.X));
                double x1 = Math.Max(Math.Max(b.P0.X, b.P1.X), Math.Max(b.P2.X, b.P3.X));
                double y0 = Math.Min(Math.Min(b.P0.Y, b.P1.Y), Math.Min(b.P2.Y, b.P3.Y));
                double y1 = Math.Max(Math.Max(b.P0.Y, b.P1.Y), Math.Max(b.P2.Y, b.P3.Y));
                return Span(x0, y0, x1, y1, b.Width + FringePx);
            }
            case PolygonCmd p:
                return Points(p.Points, FringePx);
            case PolylineCmd pl:
                return Points(pl.Points, pl.Width + FringePx);
            case CircleCmd c:
            {
                double r = c.R + (c.Stroke is null ? 0 : c.StrokeWidth) + FringePx;
                return new RectD(c.Cx - r, c.Cy - r, 2 * r, 2 * r);
            }
            case ArcCmd a:
            {
                double r = a.R + a.Width + FringePx;
                return new RectD(a.Cx - r, a.Cy - r, 2 * r, 2 * r);
            }
            case TextCmd t:
            {
                if (string.IsNullOrEmpty(t.Text)) return null;
                // One em per character is wider than any glyph of the shipped faces; tracking and the bold
                // re-strike are added on top. Height: ascent above the baseline box and descenders below.
                double track = t.Style is TextStyle s ? Math.Abs(s.TrackingPx(t.Size)) * t.Text.Length : 0;
                double bold = t.Style is { Bold: true } ? Math.Max(0.45, t.Size / 30.0) : 0;
                double w = t.Text.Length * t.Size + track + bold;
                double x = t.Align switch { TextAlign.Center => t.X - w / 2, TextAlign.Right => t.X - w, _ => t.X };
                int lines = 1;
                foreach (char ch in t.Text) if (ch == '\n') lines++;
                return Grow(new RectD(x, t.Y - 0.5 * t.Size, w, (lines + 1) * t.Size), FringePx);
            }
            default:
                return null;
        }
    }

    /// <summary>Whether two rects share any point (edges inclusive — a hairline on the boundary is kept).</summary>
    public static bool Touches(RectD a, RectD b) => a.X <= b.Right && b.X <= a.Right && a.Y <= b.Bottom && b.Y <= a.Bottom;

    public static RectD Intersect(RectD a, RectD b)
    {
        double x0 = Math.Max(a.X, b.X), y0 = Math.Max(a.Y, b.Y);
        double x1 = Math.Min(a.Right, b.Right), y1 = Math.Min(a.Bottom, b.Bottom);
        return new RectD(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
    }

    private static RectD Grow(RectD r, double d) => new(r.X - d, r.Y - d, r.W + 2 * d, r.H + 2 * d);

    private static RectD Span(double x0, double y0, double x1, double y1, double pad) =>
        new(x0 - pad, y0 - pad, (x1 - x0) + 2 * pad, (y1 - y0) + 2 * pad);

    private static RectD? Points((double X, double Y)[] pts, double pad)
    {
        if (pts.Length == 0) return null;
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach ((double x, double y) in pts)
        {
            if (x < x0) x0 = x;
            if (x > x1) x1 = x;
            if (y < y0) y0 = y;
            if (y > y1) y1 = y;
        }
        return Span(x0, y0, x1, y1, pad);
    }
}
