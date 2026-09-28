using Sim.Ui.Art;
using Sim.Ui.Art.Glyphs;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Render;

/// <summary>Which face a run of text uses. The backend maps it onto a real font
/// (EB Garamond for labels, IBM Plex Serif for numbers — UiTheme).</summary>
public enum FontRole { Body, Heading, Numeric, Caps }

public enum TextAlign { Left, Center, Right }

/// <summary>An axis-aligned rectangle in screen pixels.</summary>
public readonly record struct RectD(double X, double Y, double W, double H)
{
    public double Right => X + W;
    public double Bottom => Y + H;
    public double CenterX => X + W / 2.0;
    public double CenterY => Y + H / 2.0;
    public bool Contains(double x, double y) => x >= X && x < X + W && y >= Y && y < Y + H;
    public RectD Inset(double d) => new(X + d, Y + d, System.Math.Max(0, W - 2 * d), System.Math.Max(0, H - 2 * d));
    public bool Intersects(RectD o) => X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;
}

/// <summary>One drawing command. Colours are palette colours with an alpha.</summary>
public abstract record DrawCmd;
public sealed record RectCmd(RectD Rect, Rgba? Fill, Rgba? Stroke, double StrokeWidth, double Radius) : DrawCmd;
public sealed record LineCmd(double X0, double Y0, double X1, double Y1, Rgba Color, double Width, (double On, double Off)? Dash) : DrawCmd;
public sealed record BezierCmd((double X, double Y) P0, (double X, double Y) P1, (double X, double Y) P2, (double X, double Y) P3,
    Rgba Color, double Width, (double On, double Off)? Dash) : DrawCmd;
public sealed record PolygonCmd((double X, double Y)[] Points, Rgba Fill) : DrawCmd;
public sealed record CircleCmd(double Cx, double Cy, double R, Rgba? Fill, Rgba? Stroke, double StrokeWidth) : DrawCmd;
/// <summary>A stroked arc, clockwise from 12 o'clock.</summary>
public sealed record ArcCmd(double Cx, double Cy, double R, double StartDeg, double SweepDeg, Rgba Color, double Width) : DrawCmd;
public sealed record TextCmd(double X, double Y, string Text, double Size, Rgba Color, TextAlign Align, FontRole Role) : DrawCmd;
/// <summary>A baked glyph drawn at (X, Y) top-left, <see cref="Size"/> pixels square
/// (the spec's own box is scaled to it), at <see cref="Alpha"/>.</summary>
public sealed record GlyphCmd(double X, double Y, double Size, GlyphSpec Spec, double Alpha) : DrawCmd;
public sealed record ClipPushCmd(RectD Rect) : DrawCmd;
public sealed record ClipPopCmd : DrawCmd;

/// <summary>
/// A BACKEND-AGNOSTIC DRAW LIST. Screens paint into this; the ImGui backend replays it
/// inside the game, the SVG writer replays it for headless previews and screenshots, and
/// the tests read it directly ("the node was drawn with the Complete glyph"). It holds
/// no reference to any renderer, so the same frame renders identically everywhere.
/// </summary>
public sealed class DrawList
{
    private readonly List<DrawCmd> _cmds = [];
    public IReadOnlyList<DrawCmd> Commands => _cmds;

    public void Add(DrawCmd cmd) => _cmds.Add(cmd);

    public void Rect(RectD r, Rgba? fill, Rgba? stroke = null, double strokeWidth = 1.0, double radius = 0.0) =>
        _cmds.Add(new RectCmd(r, fill, stroke, strokeWidth, radius));

    public void Line(double x0, double y0, double x1, double y1, Rgba color, double width = 1.0, (double, double)? dash = null) =>
        _cmds.Add(new LineCmd(x0, y0, x1, y1, color, width, dash));

    public void Bezier((double, double) p0, (double, double) p1, (double, double) p2, (double, double) p3,
        Rgba color, double width = 1.0, (double, double)? dash = null) =>
        _cmds.Add(new BezierCmd(p0, p1, p2, p3, color, width, dash));

    public void Polygon((double X, double Y)[] pts, Rgba fill) => _cmds.Add(new PolygonCmd(pts, fill));

    public void Circle(double cx, double cy, double r, Rgba? fill, Rgba? stroke = null, double strokeWidth = 1.0) =>
        _cmds.Add(new CircleCmd(cx, cy, r, fill, stroke, strokeWidth));

    public void Arc(double cx, double cy, double r, double startDeg, double sweepDeg, Rgba color, double width = 1.0) =>
        _cmds.Add(new ArcCmd(cx, cy, r, startDeg, sweepDeg, color, width));

    /// <summary>Text is normalised to Latin-1 (<see cref="Latin1"/>): the game's ImGui font
    /// atlas carries only that range (UiTheme), so a character outside it would draw as '?'
    /// in the game while looking fine in an SVG. One normalisation, both backends.</summary>
    public void Text(double x, double y, string text, double size, Rgba color, TextAlign align = TextAlign.Left, FontRole role = FontRole.Body) =>
        _cmds.Add(new TextCmd(x, y, Latin1(text), size, color, align, role));

    /// <summary>Map typographic characters outside Latin-1 to Latin-1 stand-ins; anything
    /// else outside the range becomes '?'. Content strings (JSON) flow through here too.</summary>
    public static string Latin1(string text)
    {
        bool clean = true;
        foreach (char c in text) if (c > '\u00FF') { clean = false; break; }
        if (clean) return text;
        var sb = new System.Text.StringBuilder(text.Length + 8);
        foreach (char c in text)
        {
            if (c <= '\u00FF') { sb.Append(c); continue; }
            sb.Append(c switch
            {
                '\u2014' or '\u2013' or '\u2212' or '\u2010' or '\u2011' => "-",
                '\u2026' => "...",
                '\u2192' => "->",
                '\u2190' => "<-",
                '\u2018' or '\u2019' => "'",
                '\u201C' or '\u201D' => "\"",
                '\u2022' => "\u00B7",
                '\u25C0' or '\u2039' => "\u00AB",
                '\u25B6' or '\u25B8' or '\u203A' => "\u00BB",
                '\u0394' => "d",
                '\u2713' or '\u2714' => "+",
                '\u2264' => "<=",
                '\u2265' => ">=",
                _ => "?",
            });
        }
        return sb.ToString();
    }

    public void Glyph(double x, double y, double size, GlyphSpec spec, double alpha = 1.0) =>
        _cmds.Add(new GlyphCmd(x, y, size, spec, alpha));

    public void PushClip(RectD r) => _cmds.Add(new ClipPushCmd(r));
    public void PopClip() => _cmds.Add(new ClipPopCmd());

    /// <summary>A filled progress bar with a hairline frame.</summary>
    public void Bar(RectD r, double fraction, Rgba fill, Rgba frame, Rgba? track = null)
    {
        double f = double.IsNaN(fraction) ? 0.0 : System.Math.Clamp(fraction, 0.0, 1.0);
        if (track is Rgba t) Rect(r, t);
        if (f > 0) Rect(new RectD(r.X, r.Y, r.W * f, r.H), fill);
        Rect(r, null, frame, 1.0);
    }
}

/// <summary>Text width for layout and wrapping. The ImGui backend answers with the real
/// font; <see cref="ApproxTextMeasure"/> is the headless stand-in.</summary>
public interface ITextMeasure
{
    double Width(string text, double size, FontRole role);
}

/// <summary>A per-character-class estimate of EB Garamond / IBM Plex Serif advances,
/// within ~10 % on the UI's strings. Deterministic; used by tests and the SVG preview.</summary>
public sealed class ApproxTextMeasure : ITextMeasure
{
    public static readonly ApproxTextMeasure Instance = new();

    public double Width(string text, double size, FontRole role)
    {
        double em = 0;
        foreach (char c in text)
        {
            em += c switch
            {
                ' ' => 0.25,
                >= 'A' and <= 'Z' => role == FontRole.Caps ? 0.72 : 0.66,
                >= '0' and <= '9' => 0.52,
                'i' or 'l' or 'j' or 't' or 'f' or 'r' or '.' or ',' or ':' or ';' or '\'' or '|' or '!' => 0.3,
                'm' or 'w' or 'M' or 'W' => 0.75,
                '·' or '•' => 0.3,
                _ => 0.47,
            };
        }
        double scale = role == FontRole.Numeric ? 1.06 : 1.0;
        return em * size * scale;
    }
}

/// <summary>Palette helpers shared by the screens: the bible colours (ParchmentPalette)
/// with alpha, and the relation-kind ink tokens resolved to colours.</summary>
public static class Ink
{
    public static Rgba With(Rgba c, double alpha) => new(c.R, c.G, c.B, (byte)System.Math.Round(255.0 * System.Math.Clamp(alpha, 0.0, 1.0)));

    public static Rgba Token(string token) => token switch
    {
        "InkPrimary" => ParchmentPalette.InkPrimary,
        "InkSoft" => ParchmentPalette.InkSoft,
        "Verdigris" => ParchmentPalette.Verdigris,
        "IronRed" => ParchmentPalette.IronRed,
        "GoldLeaf" => ParchmentPalette.GoldLeaf,
        "River" => ParchmentPalette.River,
        _ => ParchmentPalette.InkSoft,
    };

    public static (double, double)? Dash(Trees.StrokeStyle s, double width) => s switch
    {
        Trees.StrokeStyle.Dashed => (6.0 * width, 4.0 * width),
        Trees.StrokeStyle.Dotted => (1.4 * width, 3.2 * width),
        _ => null,
    };

    /// <summary>Word-wrap <paramref name="text"/> to <paramref name="width"/> pixels.</summary>
    public static List<string> Wrap(ITextMeasure m, string text, double size, double width, FontRole role = FontRole.Body)
    {
        var lines = new List<string>();
        foreach (string para in text.Split('\n'))
        {
            string line = "";
            foreach (string word in para.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && m.Width(candidate, size, role) > width) { lines.Add(line); line = word; }
                else line = candidate;
            }
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>Truncate with an ellipsis to fit <paramref name="width"/>.</summary>
    public static string Fit(ITextMeasure m, string text, double size, double width, FontRole role = FontRole.Body)
    {
        if (m.Width(text, size, role) <= width) return text;
        for (int n = text.Length - 1; n > 0; n--)
        {
            string t = text[..n].TrimEnd() + "...";
            if (m.Width(t, size, role) <= width) return t;
        }
        return "...";
    }
}
