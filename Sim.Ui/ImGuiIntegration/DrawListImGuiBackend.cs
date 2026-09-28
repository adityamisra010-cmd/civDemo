using System.Numerics;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using Sim.Ui.Art;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.ImGuiIntegration;

/// <summary>
/// THE IMGUI BACKEND for <see cref="DrawList"/> — replays the backend-agnostic commands
/// into an ImGui draw list inside the game, and measures text with the real fonts.
/// Glyphs are baked by GlyphBaker on first use and cached as textures keyed by their
/// normalised spec. The painters quantise every fraction to 1/32, so the cache stays
/// small; <see cref="MaxGlyphTextures"/> is a hard cap against a pathological content
/// set (past it, an uncached glyph is skipped rather than bound — the renderer has no
/// unbind, so the cap is what bounds texture memory).
/// </summary>
internal sealed class DrawListImGuiBackend : ITextMeasure
{
    public const int MaxGlyphTextures = 4096;

    private readonly ImGuiRenderer _renderer;
    private readonly GraphicsDevice _device;
    private readonly UiTheme.Fonts? _fonts;
    private readonly Dictionary<GlyphSpec, IntPtr> _glyphs = new();
    private readonly List<Texture2D> _owned = new();

    public DrawListImGuiBackend(ImGuiRenderer renderer, GraphicsDevice device, UiTheme.Fonts? fonts)
    {
        _renderer = renderer;
        _device = device;
        _fonts = fonts;
    }

    public int CachedGlyphs => _glyphs.Count;

    private ImFontPtr Font(FontRole role) => _fonts is null
        ? ImGui.GetFont()
        : role switch
        {
            FontRole.Numeric => _fonts.Numeric,
            FontRole.Heading => _fonts.Header,
            _ => _fonts.Body,
        };

    public double Width(string text, double size, FontRole role) =>
        string.IsNullOrEmpty(text) ? 0.0 : Font(role).CalcTextSizeA((float)size, float.MaxValue, 0f, text).X;

    private static uint Col(Rgba c) => ((uint)c.A << 24) | ((uint)c.B << 16) | ((uint)c.G << 8) | c.R;
    private static Vector2 V(double x, double y) => new((float)x, (float)y);

    public void Render(ImDrawListPtr dl, DrawList list)
    {
        foreach (DrawCmd cmd in list.Commands)
        {
            switch (cmd)
            {
                case RectCmd r:
                    if (r.Fill is Rgba f) dl.AddRectFilled(V(r.Rect.X, r.Rect.Y), V(r.Rect.Right, r.Rect.Bottom), Col(f), (float)r.Radius);
                    if (r.Stroke is Rgba s) dl.AddRect(V(r.Rect.X, r.Rect.Y), V(r.Rect.Right, r.Rect.Bottom), Col(s), (float)r.Radius, ImDrawFlags.None, (float)r.StrokeWidth);
                    break;
                case LineCmd l:
                    if (l.Dash is (double on, double off)) Dashed(dl, [(l.X0, l.Y0), (l.X1, l.Y1)], on, off, Col(l.Color), l.Width);
                    else dl.AddLine(V(l.X0, l.Y0), V(l.X1, l.Y1), Col(l.Color), (float)l.Width);
                    break;
                case BezierCmd b:
                    if (b.Dash is (double bon, double boff))
                    {
                        var pts = new (double, double)[25];
                        for (int i = 0; i <= 24; i++)
                            pts[i] = Sim.Ui.Trees.View.TreesCanvasPainter.BezierAt(b.P0, b.P1, b.P2, b.P3, i / 24.0);
                        Dashed(dl, pts, bon, boff, Col(b.Color), b.Width);
                    }
                    else dl.AddBezierCubic(V(b.P0.X, b.P0.Y), V(b.P1.X, b.P1.Y), V(b.P2.X, b.P2.Y), V(b.P3.X, b.P3.Y), Col(b.Color), (float)b.Width, 24);
                    break;
                case PolygonCmd p:
                {
                    if (p.Points.Length < 3) break;
                    var pts = new Vector2[p.Points.Length];
                    for (int i = 0; i < pts.Length; i++) pts[i] = V(p.Points[i].X, p.Points[i].Y);
                    dl.AddConvexPolyFilled(ref pts[0], pts.Length, Col(p.Fill));
                    break;
                }
                case CircleCmd c:
                    if (c.Fill is Rgba cf) dl.AddCircleFilled(V(c.Cx, c.Cy), (float)c.R, Col(cf), 0);
                    if (c.Stroke is Rgba cs) dl.AddCircle(V(c.Cx, c.Cy), (float)c.R, Col(cs), 0, (float)c.StrokeWidth);
                    break;
                case ArcCmd a:
                {
                    // Clockwise from 12 o'clock → ImGui's angle (from +x, y down).
                    float a0 = (float)((a.StartDeg - 90.0) * Math.PI / 180.0);
                    float a1 = (float)((a.StartDeg + a.SweepDeg - 90.0) * Math.PI / 180.0);
                    dl.PathArcTo(V(a.Cx, a.Cy), (float)a.R, a0, a1, 0);
                    dl.PathStroke(Col(a.Color), ImDrawFlags.None, (float)a.Width);
                    break;
                }
                case TextCmd t:
                {
                    if (string.IsNullOrEmpty(t.Text)) break;
                    ImFontPtr font = Font(t.Role);
                    double w = t.Align == TextAlign.Left ? 0 : font.CalcTextSizeA((float)t.Size, float.MaxValue, 0f, t.Text).X;
                    double x = t.Align switch { TextAlign.Center => t.X - w / 2, TextAlign.Right => t.X - w, _ => t.X };
                    dl.AddText(font, (float)t.Size, V(x, t.Y), Col(t.Color), t.Text);
                    break;
                }
                case GlyphCmd g:
                {
                    IntPtr id = Glyph(g.Spec);
                    if (id == IntPtr.Zero) break;
                    uint tint = ((uint)Math.Round(255 * Math.Clamp(g.Alpha, 0, 1)) << 24) | 0x00FFFFFFu;
                    dl.AddImage(id, V(g.X, g.Y), V(g.X + g.Size, g.Y + g.Size), Vector2.Zero, Vector2.One, tint);
                    break;
                }
                case ClipPushCmd cp:
                    dl.PushClipRect(V(cp.Rect.X, cp.Rect.Y), V(cp.Rect.Right, cp.Rect.Bottom), true);
                    break;
                case ClipPopCmd:
                    dl.PopClipRect();
                    break;
            }
        }
    }

    private IntPtr Glyph(GlyphSpec spec)
    {
        GlyphSpec key = spec.Normalize();
        if (_glyphs.TryGetValue(key, out IntPtr id)) return id;
        if (_glyphs.Count >= MaxGlyphTextures) return IntPtr.Zero;
        ArtImage img = GlyphBaker.Bake(key);
        var tex = new Texture2D(_device, img.Width, img.Height, false, SurfaceFormat.Color);
        tex.SetData(img.Rgba);
        _owned.Add(tex);
        id = _renderer.BindTexture(tex);
        _glyphs[key] = id;
        return id;
    }

    /// <summary>Dash a polyline by arc length (ImGui has no dashed strokes).</summary>
    private static void Dashed(ImDrawListPtr dl, (double X, double Y)[] pts, double on, double off, uint col, double width)
    {
        double period = on + off;
        if (period <= 0) return;
        double s = 0;   // arc length at the start of the current segment
        for (int i = 1; i < pts.Length; i++)
        {
            (double x0, double y0) = pts[i - 1];
            (double x1, double y1) = pts[i];
            double len = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            if (len < 1e-9) continue;
            double t = 0;
            while (t < len)
            {
                double phase = (s + t) % period;
                bool drawing = phase < on;
                double run = Math.Min(len - t, drawing ? on - phase : period - phase);
                if (drawing)
                {
                    double ax = x0 + (x1 - x0) * t / len, ay = y0 + (y1 - y0) * t / len;
                    double bx = x0 + (x1 - x0) * (t + run) / len, by = y0 + (y1 - y0) * (t + run) / len;
                    dl.AddLine(V(ax, ay), V(bx, by), col, (float)width);
                }
                t += run;
            }
            s += len;
        }
    }
}
