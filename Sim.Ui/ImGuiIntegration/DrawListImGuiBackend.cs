using System.Numerics;
using ImGuiNET;
using Sim.Ui.Art;
using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.ImGuiIntegration;

/// <summary>
/// THE IMGUI BACKEND for <see cref="DrawList"/> — replays the backend-agnostic commands
/// into an ImGui draw list inside the game, and measures text with the real fonts.
/// Ported from the Trees UI foundation (claude/civdemo-work-b1z2y4) without the glyph path.
/// </summary>
public sealed class DrawListImGuiBackend : ITextMeasure
{
    private readonly UiTheme.Fonts? _fonts;

    public DrawListImGuiBackend(UiTheme.Fonts? fonts) => _fonts = fonts;

    /// <summary>The face for a run: its style's face (era typesetting), else the role's pre-theme face (Plex Serif
    /// for numbers, EB Garamond otherwise) — in both cases the smallest raster at least as large as the run
    /// (UiTheme.Fonts.Face), so no run is drawn from a raster minified by more than one ladder step. (Before UR-1 an
    /// unstyled heading — the map's names at 12.5 px — was the 25 px raster at half size.)</summary>
    private ImFontPtr Font(FontRole role, TextStyle? style, double size)
    {
        if (_fonts is null) return ImGui.GetFont();
        if (style is TextStyle s) return _fonts.Face(s.Face, size);
        return _fonts.Face(role == FontRole.Numeric ? TypeFace.PlexSerif : TypeFace.Garamond, size);
    }

    public double Width(string text, double size, FontRole role) =>
        string.IsNullOrEmpty(text) ? 0.0 : Font(role, null, size).CalcTextSizeA((float)size, float.MaxValue, 0f, text).X;

    /// <summary>The width as SET: the cased text in the style's face, plus the tracking between glyphs.</summary>
    public double Width(string text, double size, FontRole role, TextStyle? style)
    {
        if (style is not TextStyle s) return Width(text, size, role);
        if (string.IsNullOrEmpty(text)) return 0.0;
        string set = s.Apply(text);
        double w = Font(role, s, size).CalcTextSizeA((float)size, float.MaxValue, 0f, set).X;
        return w + (set.Length > 1 ? s.TrackingPx(size) * (set.Length - 1) : 0.0);
    }

    private static uint Col(Rgba c) => ((uint)c.A << 24) | ((uint)c.B << 16) | ((uint)c.G << 8) | c.R;
    private static Vector2 V(double x, double y) => new((float)x, (float)y);

    /// <summary>Commands dropped by the off-screen cull in the most recent <see cref="Render"/> call.</summary>
    public int LastCulled { get; private set; }

    /// <summary>Commands replayed into ImGui by the most recent <see cref="Render"/> call.</summary>
    public int LastReplayed { get; private set; }

    /// <summary>Whether <see cref="Render"/> culls off-screen commands first (on in the game; the frame-cost
    /// measurement turns it off to show what the cull saves).</summary>
    public bool Cull { get; set; } = true;

    /// <summary>
    /// Replays <paramref name="list"/> into <paramref name="dl"/>. M5 hardening H1: commands that cannot touch
    /// the draw list's CURRENT clip rect (the display for the background/foreground lists, the window's visible
    /// region for a window list) are dropped first (<see cref="DrawListCull"/>), so a screen that paints more
    /// than is visible no longer pays ImGui vertices for it. Nothing visible changes: ImGui would have clipped
    /// those commands to nothing.
    /// </summary>
    public void Render(ImDrawListPtr dl, DrawList list)
    {
        IReadOnlyList<DrawCmd> commands = list.Commands;
        LastCulled = 0;
        if (Cull)
        {
            Vector2 cmin = dl.GetClipRectMin(), cmax = dl.GetClipRectMax();
            commands = DrawListCull.Visible(list.Commands,
                new RectD(cmin.X, cmin.Y, Math.Max(0, cmax.X - cmin.X), Math.Max(0, cmax.Y - cmin.Y)), out int culled);
            LastCulled = culled;
        }
        LastReplayed = commands.Count;
        foreach (DrawCmd cmd in commands)
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
                            pts[i] = StrokeGeometry.BezierAt(b.P0, b.P1, b.P2, b.P3, i / 24.0);
                        Dashed(dl, pts, bon, boff, Col(b.Color), b.Width);
                    }
                    else dl.AddBezierCubic(V(b.P0.X, b.P0.Y), V(b.P1.X, b.P1.Y), V(b.P2.X, b.P2.Y), V(b.P3.X, b.P3.Y), Col(b.Color), (float)b.Width, 24);
                    break;
                case PolygonCmd p:
                {
                    if (p.Points.Length < 3) break;
                    (double X, double Y)[] cw = StrokeGeometry.Clockwise(p.Points);   // ImGui AA fringe goes outside only for clockwise
                    var pts = new Vector2[cw.Length];
                    for (int i = 0; i < pts.Length; i++) pts[i] = V(cw[i].X, cw[i].Y);
                    dl.AddConvexPolyFilled(ref pts[0], pts.Length, Col(p.Fill));
                    break;
                }
                case PolylineCmd pl:
                {
                    if (pl.Points.Length < 2) break;
                    var pts = new Vector2[pl.Points.Length];
                    for (int i = 0; i < pts.Length; i++) pts[i] = V(pl.Points[i].X, pl.Points[i].Y);
                    dl.AddPolyline(ref pts[0], pts.Length, Col(pl.Color), pl.Closed ? ImDrawFlags.Closed : ImDrawFlags.None, (float)pl.Width);
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
                    if (t.Style is TextStyle st) { Styled(dl, t, st); break; }
                    ImFontPtr font = Font(t.Role, null, t.Size);
                    double w = t.Align == TextAlign.Left ? 0 : font.CalcTextSizeA((float)t.Size, float.MaxValue, 0f, t.Text).X;
                    double x = t.Align switch { TextAlign.Center => t.X - w / 2, TextAlign.Right => t.X - w, _ => t.X };
                    dl.AddText(font, (float)t.Size, V(x, t.Y), Col(t.Color), t.Text);
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

    /// <summary>A run set in an era style: cased, in the style's face, tracked glyph by glyph when the
    /// tracking is non-zero, and struck twice (a hair to the right) when the weight is bold — ImGui
    /// rasterises one weight per face, and this is the SVG writer's font-weight seen on screen.</summary>
    private void Styled(ImDrawListPtr dl, TextCmd t, TextStyle st)
    {
        ImFontPtr font = Font(t.Role, st, t.Size);
        string set = st.Apply(t.Text);
        float size = (float)t.Size;
        double track = st.TrackingPx(t.Size);
        double w = Width(t.Text, t.Size, t.Role, st);
        double x = t.Align switch { TextAlign.Center => t.X - w / 2, TextAlign.Right => t.X - w, _ => t.X };
        uint col = Col(t.Color);
        int strikes = st.Bold ? 2 : 1;
        double bold = Math.Max(0.45, t.Size / 30.0);
        for (int k = 0; k < strikes; k++)
        {
            double ox = x + k * bold;
            if (track == 0) { dl.AddText(font, size, V(ox, t.Y), col, set); continue; }
            for (int i = 0; i < set.Length; i++)
            {
                string glyph = set[i].ToString();
                dl.AddText(font, size, V(ox, t.Y), col, glyph);
                ox += font.CalcTextSizeA(size, float.MaxValue, 0f, glyph).X + track;
            }
        }
    }

    /// <summary>Dash a polyline by arc length (ImGui has no dashed strokes). The geometry is
    /// StrokeGeometry.Dashes — pure, headless-tested, guaranteed to terminate.</summary>
    private static void Dashed(ImDrawListPtr dl, (double X, double Y)[] pts, double on, double off, uint col, double width)
    {
        foreach (((double X, double Y) a, (double X, double Y) b) in StrokeGeometry.Dashes(pts, on, off))
            dl.AddLine(V(a.X, a.Y), V(b.X, b.Y), col, (float)width);
    }
}
