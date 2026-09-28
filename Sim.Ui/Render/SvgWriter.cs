using System.Globalization;
using System.Text;
using Sim.Ui.Art;
using Sim.Ui.Art.Glyphs;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Render;

/// <summary>
/// THE SVG BACKEND — replays a <see cref="DrawList"/> as a standalone SVG document, for
/// headless previews and screenshots (MonoGame cannot open a GL context in the build
/// container, so the in-engine path cannot be captured there). Glyphs are baked by the
/// same GlyphBaker the game uses and embedded once each as PNG symbols. Output is
/// deterministic: invariant number formatting and first-use ordering throughout.
/// </summary>
public static class SvgWriter
{
    /// <param name="fontDirectory">Directory holding EBGaramond-Variable.ttf and
    /// IBMPlexSerif-Regular.ttf (assets/fonts); null falls back to generic serif faces.</param>
    public static string Write(DrawList list, double width, double height, string? fontDirectory = null)
    {
        var sb = new StringBuilder();
        var symbols = new StringBuilder();
        var glyphIds = new Dictionary<GlyphSpec, string>();
        int clipId = 0;
        int openGroups = 0;

        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"{N(width)}\" height=\"{N(height)}\" viewBox=\"0 0 {N(width)} {N(height)}\">\n");
        sb.Append("<style>\n");
        if (fontDirectory is not null)
        {
            string garamond = new Uri(Path.Combine(Path.GetFullPath(fontDirectory), "EBGaramond-Variable.ttf")).AbsoluteUri;
            string plex = new Uri(Path.Combine(Path.GetFullPath(fontDirectory), "IBMPlexSerif-Regular.ttf")).AbsoluteUri;
            sb.Append(CultureInfo.InvariantCulture, $"@font-face {{ font-family: 'EB Garamond'; src: url('{garamond}'); font-weight: 400 800; }}\n");
            sb.Append(CultureInfo.InvariantCulture, $"@font-face {{ font-family: 'IBM Plex Serif'; src: url('{plex}'); }}\n");
        }
        sb.Append(".b { font-family: 'EB Garamond', Georgia, serif; }\n");
        sb.Append(".h { font-family: 'EB Garamond', Georgia, serif; font-weight: 600; }\n");
        sb.Append(".c { font-family: 'EB Garamond', Georgia, serif; font-weight: 600; letter-spacing: 0.06em; }\n");
        sb.Append(".n { font-family: 'IBM Plex Serif', Georgia, serif; }\n");
        sb.Append("</style>\n");
        int defsAt = sb.Length;

        foreach (DrawCmd cmd in list.Commands)
        {
            switch (cmd)
            {
                case RectCmd r:
                    sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(r.Rect.X)}\" y=\"{N(r.Rect.Y)}\" width=\"{N(r.Rect.W)}\" height=\"{N(r.Rect.H)}\"");
                    if (r.Radius > 0) sb.Append(CultureInfo.InvariantCulture, $" rx=\"{N(r.Radius)}\"");
                    Fill(sb, r.Fill);
                    Stroke(sb, r.Stroke, r.StrokeWidth, null);
                    sb.Append("/>\n");
                    break;
                case LineCmd l:
                    sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{N(l.X0)}\" y1=\"{N(l.Y0)}\" x2=\"{N(l.X1)}\" y2=\"{N(l.Y1)}\"");
                    Stroke(sb, l.Color, l.Width, l.Dash);
                    sb.Append(" stroke-linecap=\"round\"/>\n");
                    break;
                case BezierCmd b:
                    sb.Append(CultureInfo.InvariantCulture,
                        $"<path d=\"M{N(b.P0.X)} {N(b.P0.Y)} C{N(b.P1.X)} {N(b.P1.Y)} {N(b.P2.X)} {N(b.P2.Y)} {N(b.P3.X)} {N(b.P3.Y)}\" fill=\"none\"");
                    Stroke(sb, b.Color, b.Width, b.Dash);
                    sb.Append(" stroke-linecap=\"round\"/>\n");
                    break;
                case PolygonCmd p:
                    sb.Append("<polygon points=\"");
                    for (int i = 0; i < p.Points.Length; i++)
                        sb.Append(CultureInfo.InvariantCulture, $"{(i > 0 ? " " : "")}{N(p.Points[i].X)},{N(p.Points[i].Y)}");
                    sb.Append('"');
                    Fill(sb, p.Fill);
                    sb.Append("/>\n");
                    break;
                case CircleCmd c:
                    sb.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{N(c.Cx)}\" cy=\"{N(c.Cy)}\" r=\"{N(c.R)}\"");
                    Fill(sb, c.Fill);
                    Stroke(sb, c.Stroke, c.StrokeWidth, null);
                    sb.Append("/>\n");
                    break;
                case ArcCmd a:
                {
                    double sweep = System.Math.Clamp(a.SweepDeg, 0, 359.99);
                    (double x0, double y0) = Polar(a.Cx, a.Cy, a.R, a.StartDeg);
                    (double x1, double y1) = Polar(a.Cx, a.Cy, a.R, a.StartDeg + sweep);
                    sb.Append(CultureInfo.InvariantCulture,
                        $"<path d=\"M{N(x0)} {N(y0)} A{N(a.R)} {N(a.R)} 0 {(sweep > 180 ? 1 : 0)} 1 {N(x1)} {N(y1)}\" fill=\"none\"");
                    Stroke(sb, a.Color, a.Width, null);
                    sb.Append("/>\n");
                    break;
                }
                case TextCmd t:
                {
                    string cls = t.Role switch { FontRole.Heading => "h", FontRole.Caps => "c", FontRole.Numeric => "n", _ => "b" };
                    string anchor = t.Align switch { TextAlign.Center => "middle", TextAlign.Right => "end", _ => "start" };
                    sb.Append(CultureInfo.InvariantCulture,
                        $"<text class=\"{cls}\" x=\"{N(t.X)}\" y=\"{N(t.Y + t.Size * 0.82)}\" font-size=\"{N(t.Size)}\" text-anchor=\"{anchor}\"");
                    Fill(sb, t.Color);
                    sb.Append('>').Append(Escape(t.Text)).Append("</text>\n");
                    break;
                }
                case GlyphCmd g:
                {
                    GlyphSpec key = g.Spec.Normalize();
                    if (!glyphIds.TryGetValue(key, out string? id))
                    {
                        id = "g" + glyphIds.Count.ToString(CultureInfo.InvariantCulture);
                        glyphIds[key] = id;
                        ArtImage img = GlyphBaker.Bake(key);
                        string b64 = Convert.ToBase64String(PngCodec.Encode(img));
                        symbols.Append(CultureInfo.InvariantCulture,
                            $"<symbol id=\"{id}\" viewBox=\"0 0 {img.Width} {img.Height}\"><image width=\"{img.Width}\" height=\"{img.Height}\" href=\"data:image/png;base64,{b64}\"/></symbol>\n");
                    }
                    sb.Append(CultureInfo.InvariantCulture, $"<use href=\"#{id}\" x=\"{N(g.X)}\" y=\"{N(g.Y)}\" width=\"{N(g.Size)}\" height=\"{N(g.Size)}\"");
                    if (g.Alpha < 0.999) sb.Append(CultureInfo.InvariantCulture, $" opacity=\"{N(g.Alpha)}\"");
                    sb.Append("/>\n");
                    break;
                }
                case ClipPushCmd cp:
                {
                    string id = "c" + (clipId++).ToString(CultureInfo.InvariantCulture);
                    symbols.Append(CultureInfo.InvariantCulture,
                        $"<clipPath id=\"{id}\"><rect x=\"{N(cp.Rect.X)}\" y=\"{N(cp.Rect.Y)}\" width=\"{N(cp.Rect.W)}\" height=\"{N(cp.Rect.H)}\"/></clipPath>\n");
                    sb.Append(CultureInfo.InvariantCulture, $"<g clip-path=\"url(#{id})\">\n");
                    openGroups++;
                    break;
                }
                case ClipPopCmd:
                    if (openGroups > 0) { sb.Append("</g>\n"); openGroups--; }
                    break;
            }
        }
        while (openGroups-- > 0) sb.Append("</g>\n");
        sb.Append("</svg>\n");
        sb.Insert(defsAt, "<defs>\n" + symbols + "</defs>\n");
        return sb.ToString();
    }

    private static (double, double) Polar(double cx, double cy, double r, double deg)
    {
        double a = deg * System.Math.PI / 180.0;
        return (cx + System.Math.Sin(a) * r, cy - System.Math.Cos(a) * r);
    }

    private static void Fill(StringBuilder sb, Rgba? c)
    {
        if (c is not Rgba f) { sb.Append(" fill=\"none\""); return; }
        sb.Append(CultureInfo.InvariantCulture, $" fill=\"{Hex(f)}\"");
        if (f.A < 255) sb.Append(CultureInfo.InvariantCulture, $" fill-opacity=\"{N(f.A / 255.0)}\"");
    }

    private static void Stroke(StringBuilder sb, Rgba? c, double width, (double On, double Off)? dash)
    {
        if (c is not Rgba s) return;
        sb.Append(CultureInfo.InvariantCulture, $" stroke=\"{Hex(s)}\" stroke-width=\"{N(width)}\"");
        if (s.A < 255) sb.Append(CultureInfo.InvariantCulture, $" stroke-opacity=\"{N(s.A / 255.0)}\"");
        if (dash is (double on, double off)) sb.Append(CultureInfo.InvariantCulture, $" stroke-dasharray=\"{N(on)} {N(off)}\"");
    }

    private static string Hex(Rgba c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static string N(double v) =>
        (System.Math.Round(v, 2) + 0.0).ToString("0.##", CultureInfo.InvariantCulture);

    private static string Escape(string s) =>
        s.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
}
