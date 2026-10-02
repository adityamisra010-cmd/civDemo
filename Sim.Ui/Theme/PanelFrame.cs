using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;
using static Sim.Ui.Theme.ThemeColor;

namespace Sim.Ui.Theme;

/// <summary>What a frame is for — the same era vocabulary at different weights. A Panel or Modal
/// carries the full treatment (texture, ornament band, fasteners); a Card carries a lighter
/// texture and corner fasteners only; a Chip or Button is a plain cut shape; a Bar is a band
/// (lens and tab bars) with its edge along the bottom.</summary>
public enum FrameKind { Panel = 0, Card = 1, Chip = 2, Button = 3, Bar = 4, Modal = 5, Toast = 6 }

/// <summary>
/// THE PANEL-FRAME PAINTER (ADR-033 D8): one era-aware frame vocabulary, painted into a
/// <see cref="DrawList"/>, used by BOTH the live renderer (behind every ImGui chrome window, replayed
/// by the ImGui backend) and the headless SVG previews — so headless rendering uses the same
/// visual-state logic as the game. A frame NEVER leaves its rect (layout continuity: the era changes
/// how a region is drawn, never where it is), and every irregularity — the chipped stone slab at A1,
/// the hand-smoothed clay tablet at A2 — is seeded from the caller's stable <c>id</c> through
/// <see cref="FrameNoise"/>: the same id draws the same frame on every frame and every run; no time,
/// no Random.
/// </summary>
public static class PanelFrame
{
    /// <summary>Paints a panel frame: <c>PanelFrame.Paint(DrawList, rect, theme, id)</c>.</summary>
    public static void Paint(DrawList d, RectD r, EraTheme t, int id) => Paint(d, r, t, id, FrameKind.Panel);

    /// <summary>Paints a frame of <paramref name="kind"/>; <paramref name="fill"/> and
    /// <paramref name="edge"/> override the material's (a card's state tint and state edge), and
    /// <paramref name="weight"/> scales the edge stroke.</summary>
    public static void Paint(DrawList d, RectD r, EraTheme t, int id, FrameKind kind,
        Rgba? fill = null, Rgba? edge = null, double weight = 1.0)
    {
        if (r.W < 2 || r.H < 2) return;
        MaterialTokens m = t.Material;
        Rgba f = fill ?? (kind switch
        {
            FrameKind.Button => m.PanelRaised,
            FrameKind.Bar => m.Chrome,
            FrameKind.Chip => m.PanelRaised,
            _ => m.Panel,
        });
        Rgba e = edge ?? m.Border;
        double bw = t.Edge.BorderPx * weight * KindStroke(kind);

        // 1. The shape, filled. Every outline is convex by construction (the ImGui backend fills
        //    convex polygons), and inside the rect.
        (double X, double Y)[] outline = Outline(r, t, id, kind);
        if (kind == FrameKind.Bar || t.Edge.Corner == CornerStyle.Square)
            d.Rect(r, f);
        else if (t.Edge.Corner == CornerStyle.Fine)
            d.Rect(r, f, null, 1.0, t.Edge.CornerPx);
        else
            d.Polygon(outline, f);

        // 2. The material's surface texture (seeded).
        double texture = kind switch
        {
            FrameKind.Panel or FrameKind.Modal or FrameKind.Toast => 1.0,
            FrameKind.Bar => 0.7,
            FrameKind.Card => 0.45,
            _ => 0.0,
        };
        if (texture > 0) Texture(d, kind == FrameKind.Bar ? r.Inset(m.GrainSize * 1.4 + 1.0) : Interior(r, t), t, id, texture, kind == FrameKind.Card, onDark: kind == FrameKind.Bar);

        // 3. The edge in the era's hand.
        if (kind == FrameKind.Bar) { BarEdge(d, r, t, id, e, bw); return; }
        Edge(d, r, outline, t, id, kind, e, bw);

        // 4. Ornament and fasteners.
        Ornament(d, r, t, id, kind);
    }

    /// <summary>The convex outline of a frame (screen px), inside <paramref name="r"/>: the chipped
    /// stone slab (Organic), the smoothed tablet (Rounded), the chamfered plaque, or the rect.
    /// Deterministic in (rect, era, id, kind).</summary>
    public static (double X, double Y)[] Outline(RectD r, EraTheme t, int id, FrameKind kind)
    {
        double k = KindScale(kind);
        switch (t.Edge.Corner)
        {
            case CornerStyle.Organic:
            {
                double a = t.Edge.JitterPx * k;
                RectD b = r.Inset(a);
                var pts = new List<(double, double)>();
                double maxCut = Math.Max(2.0, Math.Min(t.Edge.CornerPx * k, Math.Min(b.W, b.H) * 0.3));
                // corner cuts: (along the edge before, along the edge after), per corner
                var cut = new (double In, double Out)[4];
                for (int c = 0; c < 4; c++)
                    cut[c] = (maxCut * (0.35 + 0.65 * FrameNoise.U(id, 11, c * 2)), maxCut * (0.35 + 0.65 * FrameNoise.U(id, 11, c * 2 + 1)));
                (double X, double Y)[] corner = [(b.X, b.Y), (b.Right, b.Y), (b.Right, b.Bottom), (b.X, b.Bottom)];
                for (int c = 0; c < 4; c++)
                {
                    (double X, double Y) p0 = corner[c], p1 = corner[(c + 1) % 4];
                    double len = Math.Sqrt((p1.X - p0.X) * (p1.X - p0.X) + (p1.Y - p0.Y) * (p1.Y - p0.Y));
                    if (len < 1e-9) continue;
                    double ux = (p1.X - p0.X) / len, uy = (p1.Y - p0.Y) / len;
                    double nx = uy, ny = -ux;   // outward normal for a clockwise (screen) rect
                    double s0 = cut[c].Out, s1 = len - cut[(c + 1) % 4].In;
                    if (s1 <= s0) { pts.Add((p0.X + ux * (len / 2), p0.Y + uy * (len / 2))); continue; }
                    int n = Math.Max(2, (int)((s1 - s0) / 36.0));
                    double bulge = a * (0.25 + 0.75 * FrameNoise.U(id, 13, c));
                    for (int i = 0; i <= n; i++)
                    {
                        double u = i / (double)n;
                        double s = s0 + (s1 - s0) * u;
                        double o = bulge * 4.0 * u * (1.0 - u);   // a concave profile: the polygon stays convex
                        pts.Add((p0.X + ux * s + nx * o, p0.Y + uy * s + ny * o));
                    }
                }
                return [.. pts];
            }
            case CornerStyle.Rounded:
            {
                double a = t.Edge.JitterPx * k;
                RectD b = r.Inset(a);
                double rad = Math.Min(t.Edge.CornerPx * k, Math.Min(b.W, b.H) / 2.0);
                var pts = new List<(double, double)>();
                (double Cx, double Cy, double Start)[] arcs =
                [
                    (b.X + rad, b.Y + rad, 180), (b.Right - rad, b.Y + rad, 270),
                    (b.Right - rad, b.Bottom - rad, 0), (b.X + rad, b.Bottom - rad, 90),
                ];
                for (int c = 0; c < 4; c++)
                {
                    for (int i = 0; i <= 4; i++)
                    {
                        double ang = (arcs[c].Start + 90.0 * i / 4.0) * Math.PI / 180.0;
                        pts.Add((arcs[c].Cx + Math.Cos(ang) * rad, arcs[c].Cy + Math.Sin(ang) * rad));
                    }
                    // the smoothed edge to the next corner: a gentle outward swell
                    (double X, double Y) e0 = pts[^1];
                    (double Cx, double Cy, double Start) next = arcs[(c + 1) % 4];
                    double na = next.Start * Math.PI / 180.0;
                    (double X, double Y) e1 = (next.Cx + Math.Cos(na) * rad, next.Cy + Math.Sin(na) * rad);
                    double len = Math.Sqrt((e1.X - e0.X) * (e1.X - e0.X) + (e1.Y - e0.Y) * (e1.Y - e0.Y));
                    if (len < 8) continue;
                    double ux = (e1.X - e0.X) / len, uy = (e1.Y - e0.Y) / len, nx = uy, ny = -ux;
                    double bulge = a * (0.3 + 0.7 * FrameNoise.U(id, 17, c));
                    for (int i = 1; i < 4; i++)
                    {
                        double u = i / 4.0;
                        double o = bulge * 4.0 * u * (1.0 - u);
                        pts.Add((e0.X + ux * len * u + nx * o, e0.Y + uy * len * u + ny * o));
                    }
                }
                return [.. pts];
            }
            case CornerStyle.Chamfered:
            {
                double c = Math.Min(t.Edge.CornerPx * k, Math.Min(r.W, r.H) * 0.3);
                return
                [
                    (r.X + c, r.Y), (r.Right - c, r.Y), (r.Right, r.Y + c), (r.Right, r.Bottom - c),
                    (r.Right - c, r.Bottom), (r.X + c, r.Bottom), (r.X, r.Bottom - c), (r.X, r.Y + c),
                ];
            }
            default:
                return [(r.X, r.Y), (r.Right, r.Y), (r.Right, r.Bottom), (r.X, r.Bottom)];
        }
    }

    /// <summary>How far inside a frame's rect its content should start: clear of the jitter, the
    /// border and any inner rule.</summary>
    public static double ContentInset(EraTheme t, FrameKind kind) =>
        t.Edge.JitterPx * KindScale(kind) + t.Edge.BorderPx + (t.Edge.DoubleRule ? 3.0 : 0.0) + 2.0;

    // ------------------------------------------------------------------ field and rule

    /// <summary>Paints the screen's ground (the full-screen progression field, the modal backdrop):
    /// the field colour, its texture, and the era's field ornament (the graticule of the print era,
    /// the drafting grid of the industrial era).</summary>
    public static void Field(DrawList d, RectD r, EraTheme t, int id)
    {
        MaterialTokens m = t.Material;
        d.Rect(r, m.Field);
        switch (m.Kind)
        {
            case MaterialKind.Stone:
            case MaterialKind.Clay:
            {
                // broad mottling under the pits
                int n = (int)(r.W * r.H / 40000.0);
                for (int i = 0; i < n; i++)
                {
                    double x = r.X + FrameNoise.U(id, 31, i) * r.W, y = r.Y + FrameNoise.U(id, 32, i) * r.H;
                    double rad = 26 + 70 * FrameNoise.U(id, 33, i);
                    d.Circle(x, y, rad, Alpha(i % 2 == 0 ? m.FieldAlt : m.PanelSunken, 0.10 + 0.08 * FrameNoise.U(id, 34, i)));
                }
                break;
            }
            case MaterialKind.Paper:
            {
                // a cartographic graticule
                Rgba g = Alpha(m.Grain, 0.32);
                for (double x = r.X + 60; x < r.Right; x += 120) d.Line(x, r.Y, x, r.Bottom, g, 0.6);
                for (double y = r.Y + 60; y < r.Bottom; y += 120) d.Line(r.X, y, r.Right, y, g, 0.6);
                break;
            }
            case MaterialKind.Drafting:
            {
                Rgba minor = Alpha(m.Grain, 0.14), major = Alpha(m.Grain, 0.30);
                for (double x = r.X; x < r.Right; x += 20) d.Line(x, r.Y, x, r.Bottom, ((int)((x - r.X) / 20) % 5 == 0) ? major : minor, ((int)((x - r.X) / 20) % 5 == 0) ? 0.8 : 0.5);
                for (double y = r.Y; y < r.Bottom; y += 20) d.Line(r.X, y, r.Right, y, ((int)((y - r.Y) / 20) % 5 == 0) ? major : minor, ((int)((y - r.Y) / 20) % 5 == 0) ? 0.8 : 0.5);
                return;
            }
        }
        Texture(d, r, t, id, 0.35, card: false);
    }

    /// <summary>A horizontal rule across <paramref name="r"/> in the era's ornament vocabulary: the
    /// charcoal stroke, the incised line, the chevron band, the riveted bar, the meander, the ruled
    /// line with its lozenge (the parchment header rule), the printer's thick-thin rule, the
    /// dimension line, the hairline.</summary>
    public static void Rule(DrawList d, RectD r, EraTheme t, int id)
    {
        if (r.W < 8) return;
        MaterialTokens m = t.Material;
        double cy = r.Y + r.H / 2.0;
        switch (t.Ornament.Motif)
        {
            case Motif.None:   // A1: a charcoal stroke, freehand
            {
                d.Polyline(Freehand(r.X + 2, cy, r.Right - 2, cy, t.Edge.JitterPx * 0.5, id, 41), t.Ink.Rule, 2.0);
                d.Polyline(Freehand(r.X + 6, cy + 1.2, r.Right - 8, cy + 0.6, t.Edge.JitterPx * 0.4, id, 42), Alpha(t.Ink.Rule, 0.35), 1.2);
                break;
            }
            case Motif.Weave:  // A2: an incised line over a woven band
            {
                d.Line(r.X, cy - 2, r.Right, cy - 2, m.Border, 1.4);
                d.Line(r.X, cy - 0.8, r.Right, cy - 0.8, Alpha(m.PanelRaised, 0.9), 1.0);
                Weave(d, new RectD(r.X, cy + 0.5, r.W, Math.Min(5, r.Bottom - cy)), t);
                break;
            }
            case Motif.Chevron:
            {
                d.Line(r.X, r.Y + 0.5, r.Right, r.Y + 0.5, m.Border, 1.2);
                d.Line(r.X, r.Bottom - 0.5, r.Right, r.Bottom - 0.5, m.Border, 1.2);
                Chevrons(d, new RectD(r.X, r.Y + 1.5, r.W, r.H - 3), Alpha(m.Accent, 0.85), 1.1);
                break;
            }
            case Motif.Rivet:
            {
                d.Rect(new RectD(r.X, cy - 1.8, r.W, 3.6), m.BorderStrong);
                for (double x = r.X + 10; x < r.Right - 6; x += 56) Rivet(d, x, cy, 2.1, t);
                break;
            }
            case Motif.Meander:
            {
                d.Line(r.X, r.Y + 0.5, r.Right, r.Y + 0.5, m.Border, 0.9);
                d.Line(r.X, r.Bottom - 0.5, r.Right, r.Bottom - 0.5, m.Border, 0.9);
                Meander(d, new RectD(r.X + 2, r.Y + 1.8, r.W - 4, r.H - 3.6), Alpha(m.Accent, 0.9), 0.9);
                break;
            }
            case Motif.Illumination:   // A6: the parchment header rule (HeaderRuleBaker's design, in vector)
            {
                double s = r.H / 8.0;
                d.Rect(new RectD(r.X, r.Y + 1.0 * s, r.W, 2.0 * s), t.Ink.Rule);
                d.Rect(new RectD(r.X, r.Y + 4.5 * s, r.W, 0.75 * s), t.Ink.TextSoft);
                for (double x = r.X + 128 * s / 1.0; x < r.Right - 4; x += 256 * s)
                    d.Polygon([(x - 4 * s, r.Y + 2 * s), (x, r.Y), (x + 4 * s, r.Y + 2 * s), (x, r.Y + 4 * s)], t.Ink.Rule);
                break;
            }
            case Motif.Fleuron:
            {
                d.Line(r.X, cy - 1.6, r.Right, cy - 1.6, m.Border, 1.8);
                d.Line(r.X, cy + 1.6, r.Right, cy + 1.6, m.Border, 0.6);
                Fleuron(d, r.CenterX, cy, 4.5, t);
                break;
            }
            case Motif.Dimension:
            {
                Rgba c = m.Border;
                d.Line(r.X + 1, cy, r.Right - 1, cy, c, 0.9);
                d.Line(r.X + 1, cy - 3.5, r.X + 1, cy + 3.5, c, 0.9);
                d.Line(r.Right - 1, cy - 3.5, r.Right - 1, cy + 3.5, c, 0.9);
                d.Polygon([(r.X + 1, cy), (r.X + 7, cy - 2), (r.X + 7, cy + 2)], c);
                d.Polygon([(r.Right - 1, cy), (r.Right - 7, cy - 2), (r.Right - 7, cy + 2)], c);
                for (double x = r.X + 10; x < r.Right - 8; x += 10)
                    d.Line(x, cy, x, cy - ((int)Math.Round((x - r.X) / 10) % 5 == 0 ? 3.0 : 1.6), Alpha(c, 0.8), 0.6);
                break;
            }
            default:   // A9: a hairline with a short accent
            {
                d.Line(r.X, cy, r.Right, cy, m.Hairline, 1.0);
                d.Rect(new RectD(r.X, cy - 1.0, Math.Min(32, r.W), 2.0), m.Accent);
                break;
            }
        }
    }

    // ------------------------------------------------------------------ parts

    private static double KindScale(FrameKind kind) => kind switch
    {
        FrameKind.Card => 0.85,
        FrameKind.Chip => 0.45,
        FrameKind.Button => 0.55,
        FrameKind.Bar => 0.8,
        _ => 1.0,
    };

    private static double KindStroke(FrameKind kind) => kind switch
    {
        FrameKind.Chip => 0.7,
        FrameKind.Button => 0.8,
        FrameKind.Card => 0.85,
        FrameKind.Modal => 1.2,
        _ => 1.0,
    };

    /// <summary>The region texture and ornament may occupy: the rect inset past the jitter and the
    /// deepest corner cut, so nothing drawn inside spills over the cut shape.</summary>
    private static RectD Interior(RectD r, EraTheme t) =>
        r.Inset(t.Edge.JitterPx + t.Edge.CornerPx * 0.55 + 1.5);

    private static void Texture(DrawList d, RectD r, EraTheme t, int id, double scale, bool card, bool onDark = false)
    {
        MaterialTokens m = t.Material;
        if (r.W <= 1 || r.H <= 1 || m.GrainDensity <= 0 || m.GrainAlpha <= 0) return;
        if (onDark)   // the frame material's bar: its grain in a light ink, sparser
            m = m with { Grain = t.Ink.OnChromeSoft, GrainAlpha = m.GrainAlpha * 0.45, PanelRaised = t.Ink.OnChromeSoft, AccentSoft = t.Ink.OnChromeSoft };
        double area = r.W * r.H / 10000.0;
        int n = (int)Math.Min(card ? 40 : 900, Math.Round(area * m.GrainDensity * scale));
        switch (m.Kind)
        {
            case MaterialKind.Stone:
            case MaterialKind.Clay:
            case MaterialKind.Bronze:
            {
                for (int i = 0; i < n; i++)
                {
                    double x = r.X + FrameNoise.U(id, 51, i) * r.W, y = r.Y + FrameNoise.U(id, 52, i) * r.H;
                    double s = m.GrainSize * (0.35 + FrameNoise.U(id, 53, i));
                    double a = m.GrainAlpha * (0.5 + 0.5 * FrameNoise.U(id, 54, i));
                    if (m.Kind == MaterialKind.Bronze)
                        d.Circle(x, y, s, null, Alpha(m.Grain, a), 0.7);   // hammer dimples
                    else if (i % 5 == 4)
                        d.Circle(x, y, s * 0.8, Alpha(m.PanelRaised, a * 1.6));   // a lighter fleck
                    else
                        d.Circle(x, y, s * 0.6, Alpha(m.Grain, a));               // a pit
                }
                if (m.Kind == MaterialKind.Bronze && !card)
                    for (int i = 0; i < Math.Max(1, n / 6); i++)   // verdigris near the edges
                    {
                        bool top = FrameNoise.U(id, 55, i) < 0.5;
                        double x = r.X + FrameNoise.U(id, 56, i) * r.W;
                        double y = top ? r.Y + FrameNoise.U(id, 57, i) * 10 : r.Bottom - FrameNoise.U(id, 57, i) * 10;
                        d.Circle(x, y, 1.5 + 3 * FrameNoise.U(id, 58, i), Alpha(m.AccentSoft, 0.12));
                    }
                if (m.Kind == MaterialKind.Stone && !card && r.W > 120)   // a crack or two
                    for (int i = 0; i < 2; i++)
                    {
                        double x = r.X + (0.1 + 0.8 * FrameNoise.U(id, 59, i)) * r.W, y = r.Y + (0.15 + 0.7 * FrameNoise.U(id, 60, i)) * r.H;
                        double len = 18 + 30 * FrameNoise.U(id, 61, i), ang = FrameNoise.U(id, 62, i) * Math.PI;
                        d.Polyline(Freehand(x, y, x + Math.Cos(ang) * len, y + Math.Sin(ang) * len, 1.6, id + i, 63), Alpha(m.Grain, 0.35), 0.8);
                    }
                break;
            }
            case MaterialKind.Iron:
            {
                for (int i = 0; i < n; i++)   // forge scale: short hammer strokes
                {
                    double x = r.X + FrameNoise.U(id, 71, i) * r.W, y = r.Y + FrameNoise.U(id, 72, i) * r.H;
                    double len = m.GrainSize * (1.2 + 2 * FrameNoise.U(id, 73, i));
                    d.Line(x, y, Math.Min(r.Right, x + len), y + FrameNoise.S(id, 74, i) * 0.8, Alpha(m.Grain, m.GrainAlpha), 0.8);
                }
                break;
            }
            case MaterialKind.Marble:
            {
                // a few long, faint veins (the field carries more than a panel or a card)
                int veins = Math.Max(1, (int)Math.Round(area * m.GrainDensity * scale));
                bool field = scale < 0.5;
                double alpha = m.GrainAlpha * (field ? 1.0 : card ? 0.45 : 0.5);
                for (int i = 0; i < Math.Min(veins, card ? 1 : field ? 12 : 3); i++)
                {
                    double y0 = r.Y + FrameNoise.U(id, 81, i) * r.H, y1 = r.Y + FrameNoise.U(id, 82, i) * r.H;
                    double bend = field ? 0.5 : 0.25;
                    d.Bezier((r.X, y0), (r.X + r.W * 0.35, y0 + FrameNoise.S(id, 83, i) * r.H * bend),
                        (r.X + r.W * 0.65, y1 + FrameNoise.S(id, 84, i) * r.H * bend), (r.Right, y1), Alpha(m.Grain, alpha), 0.7);
                }
                break;
            }
            case MaterialKind.Vellum:
            {
                for (int i = 0; i < n; i++)   // fibres
                {
                    double x = r.X + FrameNoise.U(id, 91, i) * r.W, y = r.Y + FrameNoise.U(id, 92, i) * r.H;
                    double ang = FrameNoise.U(id, 93, i) * Math.PI, len = m.GrainSize * (0.5 + FrameNoise.U(id, 94, i));
                    d.Line(x, y, x + Math.Cos(ang) * len, y + Math.Sin(ang) * len, Alpha(m.Grain, m.GrainAlpha), 0.6);
                }
                break;
            }
            case MaterialKind.Paper:
            {
                if (card) break;
                for (double x = r.X + 12; x < r.Right; x += 24)   // laid paper's chain lines
                    d.Line(x, r.Y, x, r.Bottom, Alpha(m.Grain, m.GrainAlpha * 0.6), 0.5);
                break;
            }
            case MaterialKind.Drafting:
            {
                double step = card ? 12 : 10;
                Rgba g = Alpha(m.Grain, m.GrainAlpha * (card ? 0.35 : 0.5));
                for (double x = r.X + step; x < r.Right; x += step) d.Line(x, r.Y, x, r.Bottom, g, 0.4);
                for (double y = r.Y + step; y < r.Bottom; y += step) d.Line(r.X, y, r.Right, y, g, 0.4);
                break;
            }
        }
    }

    private static void Edge(DrawList d, RectD r, (double X, double Y)[] outline, EraTheme t, int id, FrameKind kind, Rgba e, double bw)
    {
        MaterialTokens m = t.Material;
        switch (t.Edge.Corner)
        {
            case CornerStyle.Organic:
            {
                // charcoal: the outline redrawn freehand, a firm pass and a faint second pass
                d.Polyline(Roughen(outline, t.Edge.JitterPx * 0.45 * KindScale(kind), id, 21), e, bw, closed: true);
                d.Polyline(Roughen(outline, t.Edge.JitterPx * 0.6 * KindScale(kind), id, 22), Alpha(e, 0.3), Math.Max(0.8, bw * 0.55), closed: true);
                break;
            }
            case CornerStyle.Rounded:
            {
                // incised: a dark cut and its light lip just inside
                d.Polyline(Roughen(outline, t.Edge.JitterPx * 0.2, id, 23), e, bw, closed: true);
                d.Polyline(Shrink(outline, r, 1.6), Alpha(m.PanelRaised, 0.85), 1.0, closed: true);
                break;
            }
            case CornerStyle.Chamfered:
            {
                d.Polyline(outline, e, bw, closed: true);
                if (t.Edge.DoubleRule && kind is not (FrameKind.Chip or FrameKind.Button))
                    d.Polyline(Shrink(outline, r, 3.2), Alpha(m.Accent, 0.9), 0.9, closed: true);
                break;
            }
            case CornerStyle.Fine:
                // a hairline: the graphite border at low alpha; a state-coloured edge (cards) at full
                d.Rect(r, null, e == m.Border || e == m.Hairline ? Alpha(e, 0.38) : e, Math.Max(1.0, bw), t.Edge.CornerPx);
                break;
            default:
            {
                double half = bw / 2.0;
                d.Rect(new RectD(r.X + half, r.Y + half, r.W - bw, r.H - bw), null, e, bw);
                if (t.Edge.DoubleRule && kind is not (FrameKind.Chip or FrameKind.Button))
                {
                    double ins = bw + 2.2;
                    d.Rect(r.Inset(ins), null, Alpha(m.Kind == MaterialKind.Vellum ? t.Ink.TextSoft : e, 0.85), 0.6);
                }
                else if (m.Kind == MaterialKind.Iron && kind is FrameKind.Panel or FrameKind.Modal or FrameKind.Card or FrameKind.Toast)
                    d.Rect(r.Inset(bw + 1.6), null, Alpha(m.Hairline, 0.9), 0.8);
                break;
            }
        }
    }

    /// <summary>A bar's lower edge: the era's accent drawn under its frame material (an ochre stroke
    /// under charred wood, a gold line under porphyry, brass under Prussian blue…).</summary>
    private static void BarEdge(DrawList d, RectD r, EraTheme t, int id, Rgba e, double bw)
    {
        Rgba accent = t.Material.Accent;
        double y = r.Bottom - Math.Max(1.0, bw / 2.0);
        if (t.Edge.Corner == CornerStyle.Organic)
            d.Polyline(Freehand(r.X, y - 0.5, r.Right, y - 0.5, t.Edge.JitterPx * 0.35, id, 25), accent, Math.Max(2.0, bw * 0.9));
        else if (t.Edge.Corner == CornerStyle.Fine)
            d.Line(r.X, r.Bottom - 1.0, r.Right, r.Bottom - 1.0, accent, 2.0);
        else
        {
            d.Line(r.X, y, r.Right, y, accent, Math.Max(1.5, bw * 0.8));
            if (t.Edge.DoubleRule) d.Line(r.X, y - bw - 1.8, r.Right, y - bw - 1.8, Alpha(accent, 0.55), 0.7);
        }
    }

    private static void Ornament(DrawList d, RectD r, EraTheme t, int id, FrameKind kind)
    {
        MaterialTokens m = t.Material;
        bool full = kind is FrameKind.Panel or FrameKind.Modal or FrameKind.Toast;
        bool card = kind == FrameKind.Card;
        if (!full && !card) return;
        double ins = t.Edge.BorderPx + 3.0;
        switch (t.Ornament.Motif)
        {
            case Motif.Weave when full && r.W > 80 && r.H > 40:
                Weave(d, new RectD(r.X + 14, r.Y + ins + 1, r.W - 28, 5), t);
                break;
            case Motif.Chevron:
            {
                double c = Math.Min(t.Edge.CornerPx * KindScale(kind), Math.Min(r.W, r.H) * 0.3);
                double s = card ? 1.7 : 2.3;
                // studs at the chamfers
                Stud(d, r.X + c * 0.5 + 1.6, r.Y + c * 0.5 + 1.6, s, t);
                Stud(d, r.Right - c * 0.5 - 1.6, r.Y + c * 0.5 + 1.6, s, t);
                Stud(d, r.Right - c * 0.5 - 1.6, r.Bottom - c * 0.5 - 1.6, s, t);
                Stud(d, r.X + c * 0.5 + 1.6, r.Bottom - c * 0.5 - 1.6, s, t);
                if (full && r.W > 80 && r.H > 40) Chevrons(d, new RectD(r.X + 16, r.Y + ins + 2, r.W - 32, 6), Alpha(m.Accent, 0.75), 1.0);
                break;
            }
            case Motif.Rivet:
            {
                double s = card ? 1.8 : 2.4, o = t.Edge.BorderPx + (card ? 4.0 : 5.5);
                Rivet(d, r.X + o, r.Y + o, s, t); Rivet(d, r.Right - o, r.Y + o, s, t);
                Rivet(d, r.Right - o, r.Bottom - o, s, t); Rivet(d, r.X + o, r.Bottom - o, s, t);
                if (full)
                    for (double x = r.X + o + 64; x < r.Right - o - 30; x += 64)
                    {
                        Rivet(d, x, r.Y + o, s, t);
                        Rivet(d, x, r.Bottom - o, s, t);
                    }
                break;
            }
            case Motif.Meander when full && r.W > 100 && r.H > 50:
            {
                var band = new RectD(r.X + ins + 6, r.Y + ins + 3, r.W - 2 * ins - 12, 7);
                Meander(d, band, Alpha(m.Accent, 0.85), 0.9);
                d.Line(band.X, band.Bottom + 2, band.Right, band.Bottom + 2, Alpha(m.Border, 0.7), 0.6);
                break;
            }
            case Motif.Illumination when full:
            {
                // illuminated corner pieces: gold leaf squares on the inner rule, a rubric dot
                double s = 5.0, o = t.Edge.BorderPx + 2.2 - s / 2.0 + 0.3;
                foreach ((double x, double y) in new[] { (r.X + o, r.Y + o), (r.Right - o - s, r.Y + o), (r.Right - o - s, r.Bottom - o - s), (r.X + o, r.Bottom - o - s) })
                {
                    d.Rect(new RectD(x, y, s, s), m.AccentSoft, t.Ink.Rule, 0.7);
                    d.Circle(x + s / 2, y + s / 2, 0.9, m.Accent);
                }
                break;
            }
            case Motif.Fleuron when full:
            {
                double o = t.Edge.BorderPx + 2.2;
                Fleuron(d, r.X + o + 1, r.Y + o + 1, 2.6, t); Fleuron(d, r.Right - o - 1, r.Y + o + 1, 2.6, t);
                Fleuron(d, r.Right - o - 1, r.Bottom - o - 1, 2.6, t); Fleuron(d, r.X + o + 1, r.Bottom - o - 1, 2.6, t);
                break;
            }
            case Motif.Dimension:
            {
                double o = t.Edge.BorderPx + (card ? 3.5 : 5.0), s = card ? 1.6 : 2.2;
                Bolt(d, r.X + o, r.Y + o, s, t); Bolt(d, r.Right - o, r.Y + o, s, t);
                Bolt(d, r.Right - o, r.Bottom - o, s, t); Bolt(d, r.X + o, r.Bottom - o, s, t);
                if (full && r.W > 80)
                    for (double x = r.X + 20; x < r.Right - 14; x += 10)
                        d.Line(x, r.Y + t.Edge.BorderPx, x, r.Y + t.Edge.BorderPx + ((int)Math.Round((x - r.X) / 10) % 5 == 0 ? 4.0 : 2.0), Alpha(m.Border, 0.7), 0.6);
                break;
            }
            case Motif.Hairline when full && r.W > 60:
                d.Rect(new RectD(r.X + 1, r.Y + 1, 28, 2), m.Accent);
                break;
        }
    }

    // ------------------------------------------------------------------ small devices

    private static void Stud(DrawList d, double x, double y, double s, EraTheme t)
    {
        d.Circle(x, y, s, t.Material.Accent, t.Material.BorderStrong, 0.6);
        d.Circle(x - s * 0.3, y - s * 0.3, s * 0.35, Alpha(t.Material.PanelRaised, 0.8));
    }

    private static void Rivet(DrawList d, double x, double y, double s, EraTheme t)
    {
        d.Circle(x, y, s, t.Material.Border, null);
        d.Circle(x - s * 0.3, y - s * 0.3, s * 0.38, Alpha(t.Material.PanelRaised, 0.75));
    }

    private static void Bolt(DrawList d, double x, double y, double s, EraTheme t)
    {
        d.Circle(x, y, s, null, t.Material.Border, 0.7);
        d.Line(x - s * 0.6, y, x + s * 0.6, y, t.Material.Border, 0.6);
    }

    private static void Fleuron(DrawList d, double x, double y, double s, EraTheme t)
    {
        d.Polygon([(x - s, y), (x, y - s * 0.7), (x + s, y), (x, y + s * 0.7)], t.Material.Border);
        d.Circle(x - s * 1.5, y, s * 0.28, t.Material.Border);
        d.Circle(x + s * 1.5, y, s * 0.28, t.Material.Border);
    }

    private static void Weave(DrawList d, RectD b, EraTheme t)
    {
        Rgba c = Alpha(t.Material.AccentSoft, 0.7), c2 = Alpha(t.Material.Border, 0.45);
        double h = b.H;
        int i = 0;
        for (double x = b.X; x + h < b.Right; x += h * 0.9, i++)
            if (i % 2 == 0) d.Line(x, b.Bottom, x + h * 0.8, b.Y, c, 1.1);
            else d.Line(x, b.Y, x + h * 0.8, b.Bottom, c2, 1.0);
    }

    private static void Chevrons(DrawList d, RectD b, Rgba c, double w)
    {
        double h = b.H;
        if (h <= 0) return;
        for (double x = b.X; x + h * 1.2 < b.Right; x += h * 1.4)
            d.Polyline([(x, b.Bottom), (x + h * 0.6, b.Y), (x + h * 1.2, b.Bottom)], c, w);
    }

    /// <summary>The Greek key, as one polyline per unit.</summary>
    private static void Meander(DrawList d, RectD b, Rgba c, double w)
    {
        double h = b.H;
        if (h <= 2) return;
        double u = h;   // one unit is h wide
        for (double x = b.X; x + u <= b.Right; x += u)
        {
            d.Polyline(
            [
                (x, b.Bottom), (x, b.Y), (x + u * 0.75, b.Y), (x + u * 0.75, b.Y + h * 0.66),
                (x + u * 0.35, b.Y + h * 0.66), (x + u * 0.35, b.Y + h * 0.33),
            ], c, w);
            d.Line(x, b.Bottom, x + u, b.Bottom, c, w);
        }
    }

    // ------------------------------------------------------------------ freehand geometry

    /// <summary>A freehand line from (x0,y0) to (x1,y1): points every ~9 px displaced along the
    /// normal by a smooth seeded wobble of amplitude <paramref name="amp"/>.</summary>
    public static (double X, double Y)[] Freehand(double x0, double y0, double x1, double y1, double amp, int id, int salt)
    {
        double len = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        int n = Math.Max(2, (int)(len / 9.0));
        double ux = len > 0 ? (x1 - x0) / len : 1, uy = len > 0 ? (y1 - y0) / len : 0, nx = -uy, ny = ux;
        var pts = new (double, double)[n + 1];
        double prev = 0;
        for (int i = 0; i <= n; i++)
        {
            double s = i / (double)n;
            double w = i == 0 || i == n ? 0 : prev * 0.55 + FrameNoise.S(id, salt, i) * 0.45;
            prev = w;
            pts[i] = (x0 + ux * len * s + nx * w * amp, y0 + uy * len * s + ny * w * amp);
        }
        return pts;
    }

    /// <summary>A closed outline redrawn freehand: each edge subdivided and wobbled.</summary>
    private static (double X, double Y)[] Roughen((double X, double Y)[] outline, double amp, int id, int salt)
    {
        if (amp <= 0.01) return outline;
        var pts = new List<(double, double)>();
        for (int i = 0; i < outline.Length; i++)
        {
            (double X, double Y) a = outline[i], b = outline[(i + 1) % outline.Length];
            (double X, double Y)[] seg = Freehand(a.X, a.Y, b.X, b.Y, amp, id, salt * 64 + i);
            for (int k = 0; k < seg.Length - 1; k++) pts.Add(seg[k]);
        }
        return [.. pts];
    }

    /// <summary>The outline pulled toward the rect's centre by <paramref name="by"/> px (an inner rule).</summary>
    private static (double X, double Y)[] Shrink((double X, double Y)[] outline, RectD r, double by)
    {
        double cx = r.CenterX, cy = r.CenterY;
        double sx = r.W > 2 * by ? (r.W - 2 * by) / r.W : 1.0, sy = r.H > 2 * by ? (r.H - 2 * by) / r.H : 1.0;
        var pts = new (double, double)[outline.Length];
        for (int i = 0; i < outline.Length; i++) pts[i] = (cx + (outline[i].X - cx) * sx, cy + (outline[i].Y - cy) * sy);
        return pts;
    }
}

/// <summary>
/// SEEDED NOISE for the procedural frames — a SplitMix64 finaliser over (id, salt, index): a pure
/// function, the same bits on every run and every frame. No <c>System.Random</c>, no time.
/// </summary>
public static class FrameNoise
{
    /// <summary>Uniform in [0, 1).</summary>
    public static double U(int id, int salt, int index)
    {
        ulong z = unchecked((ulong)(uint)id * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)salt * 0xC2B2AE3D27D4EB4FUL ^ (ulong)(uint)index * 0x165667B19E3779F9UL);
        z = unchecked(z + 0x9E3779B97F4A7C15UL);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        z ^= z >> 31;
        return (z >> 11) * (1.0 / 9007199254740992.0);
    }

    /// <summary>Uniform in [-1, 1).</summary>
    public static double S(int id, int salt, int index) => U(id, salt, index) * 2.0 - 1.0;
}
