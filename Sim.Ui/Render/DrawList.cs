using Sim.Ui.Art;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Render;

/// <summary>Which face a run of text uses. The backend maps it onto a real font
/// (EB Garamond for labels, IBM Plex Serif for numbers — UiTheme); a run that carries a
/// <see cref="TextStyle"/> (the era theme's typesetting for its role) is set in that style.
/// <c>Title</c> is the large display line of a panel (a node's name in the detail panel, the Age
/// banner); without a style it renders as <c>Heading</c>.</summary>
public enum FontRole { Body, Heading, Numeric, Caps, Title }

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
/// <summary>A run of text. <paramref name="Text"/> is what the caller wrote (Latin-1 normalised);
/// <paramref name="Style"/>, when present, is how it is set (face, weight, tracking, case) — the
/// backends apply it, so the semantic text is never rewritten.</summary>
public sealed record TextCmd(double X, double Y, string Text, double Size, Rgba Color, TextAlign Align, FontRole Role,
    TextStyle? Style = null) : DrawCmd;
/// <summary>An open or closed stroked polyline (hand-drawn outlines, rules, ornaments).</summary>
public sealed record PolylineCmd((double X, double Y)[] Points, Rgba Color, double Width, bool Closed) : DrawCmd;
public sealed record ClipPushCmd(RectD Rect) : DrawCmd;
public sealed record ClipPopCmd : DrawCmd;

/// <summary>
/// A BACKEND-AGNOSTIC DRAW LIST. Screens paint into this; the ImGui backend replays it
/// inside the game, the SVG writer replays it for headless previews and screenshots, and
/// the tests read it directly ("the node was drawn in the Completed colour"). It holds
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

    /// <summary>A stroked polyline; <paramref name="closed"/> joins the last point to the first.</summary>
    public void Polyline((double X, double Y)[] pts, Rgba color, double width = 1.0, bool closed = false) =>
        _cmds.Add(new PolylineCmd(pts, color, width, closed));

    /// <summary>Text is normalised to Latin-1 (<see cref="Latin1"/>): the game's ImGui font
    /// atlas carries only that range (UiTheme), so a character outside it would draw as '?'
    /// in the game while looking fine in an SVG. One normalisation, both backends.</summary>
    public void Text(double x, double y, string text, double size, Rgba color, TextAlign align = TextAlign.Left, FontRole role = FontRole.Body) =>
        _cmds.Add(new TextCmd(x, y, Latin1(text), size, color, align, role));

    /// <summary>As the unstyled overload, set in <paramref name="style"/> (the era theme's
    /// typesetting for the role).</summary>
    public void Text(double x, double y, string text, double size, Rgba color, TextAlign align, FontRole role, TextStyle? style) =>
        _cmds.Add(new TextCmd(x, y, Latin1(text), size, color, align, role, style));

    /// <summary>
    /// THE TYPOGRAPHIC SUBSTITUTIONS \u2014 every character outside Latin-1 the content and the UI are
    /// known to use, with its Latin-1 stand-in. <see cref="Latin1"/> applies them to DrawList text;
    /// the ImGui text boundary (Sim.Ui.Theme.UiText) applies the same table to the font atlas, so
    /// text handed straight to ImGui renders as DrawList text does. One table, both paths.
    /// </summary>
    public static IReadOnlyList<(char From, string To)> Substitutions { get; } =
    [
        ('\u2014', "-"), ('\u2013', "-"), ('\u2212', "-"), ('\u2010', "-"), ('\u2011', "-"),
        ('\u2026', "..."),
        ('\u2192', "->"), ('\u2190', "<-"),
        ('\u2018', "'"), ('\u2019', "'"),
        ('\u201C', "\""), ('\u201D', "\""),
        ('\u2022', "\u00B7"),
        ('\u25C0', "\u00AB"), ('\u2039', "\u00AB"),
        ('\u25B6', "\u00BB"), ('\u25B8', "\u00BB"), ('\u203A', "\u00BB"),
        ('\u0394', "d"),
        ('\u2713', "+"), ('\u2714', "+"),
        ('\u2264', "<="), ('\u2265', ">="),
    ];

    /// <summary>The Latin-1 stand-in for <paramref name="c"/> (a character outside Latin-1), or "?".</summary>
    public static string StandIn(char c)
    {
        foreach ((char from, string to) in Substitutions) if (from == c) return to;
        return "?";
    }

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
            sb.Append(StandIn(c));
        }
        return sb.ToString();
    }


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

    /// <summary>The width of <paramref name="text"/> as SET in <paramref name="style"/> (cased, in
    /// its face, tracked). The default cases the text, measures it in the role's face and adds the
    /// tracking; a backend with real fonts overrides it with the styled face's own metrics.</summary>
    double Width(string text, double size, FontRole role, TextStyle? style)
    {
        if (style is not TextStyle s) return Width(text, size, role);
        string set = s.Apply(text);
        return Width(set, size, role) + (set.Length > 1 ? s.TrackingPx(size) * (set.Length - 1) : 0.0);
    }
}

/// <summary>A per-character-class estimate of EB Garamond / IBM Plex Serif advances,
/// within ~10 % on the UI's strings. Deterministic; used by tests and the SVG preview.</summary>
public sealed class ApproxTextMeasure : ITextMeasure
{
    public static readonly ApproxTextMeasure Instance = new();

    public double Width(string text, double size, FontRole role) =>
        Em(text, role == FontRole.Caps) * size * (role == FontRole.Numeric ? 1.06 : 1.0);

    /// <summary>
    /// The styled width: the cased text in the style's face and weight, plus the tracking. The
    /// per-class factors scale the Garamond table above to each face as the SVG previews set it
    /// (fitted to Chromium's advances for the UI's strings, keeping the table's few-percent margin
    /// over real Garamond): IBM Plex Serif and Plex Sans set lower case and figures a fifth to a
    /// quarter wider than Garamond and capitals slightly narrower; EB Garamond's heavier weights widen
    /// lower case and figures (+11 % / +14 % at 700); the Plex faces embolden synthetically, at their
    /// regular advances.
    /// </summary>
    public double Width(string text, double size, FontRole role, TextStyle? style)
    {
        if (style is not TextStyle s) return Width(text, size, role);
        string set = s.Apply(text);
        bool caps = s.Case == TextCase.Upper || role == FontRole.Caps;
        double k = Math.Clamp((s.Weight - 400) / 300.0, 0.0, 1.5);
        (double upper, double lower, double figure) = s.Face switch
        {
            TypeFace.PlexSerif => (0.96, 1.26, 1.23),
            TypeFace.PlexSans => (0.90, 1.21, 1.26),
            _ => (1.0, 1.0 + 0.11 * k, 1.0 + 0.14 * k),
        };
        double em = 0;
        foreach (char c in set)
            em += Em(c, caps) * (c is >= 'A' and <= 'Z' ? upper : c is >= '0' and <= '9' ? figure : lower);
        double tracking = set.Length > 1 ? s.TrackingPx(size) * (set.Length - 1) : 0.0;
        return em * size + tracking;
    }

    private static double Em(string text, bool caps)
    {
        double em = 0;
        foreach (char c in text) em += Em(c, caps);
        return em;
    }

    private static double Em(char c, bool caps) => c switch
    {
        ' ' => 0.25,
        >= 'A' and <= 'Z' => caps ? 0.72 : 0.66,
        >= '0' and <= '9' => 0.52,
        'i' or 'l' or 'j' or 't' or 'f' or 'r' or '.' or ',' or ':' or ';' or '\'' or '|' or '!' => 0.3,
        'm' or 'w' or 'M' or 'W' => 0.75,
        '·' or '•' => 0.3,
        _ => 0.47,
    };
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
