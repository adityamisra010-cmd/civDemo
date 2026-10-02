using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Theme;

/// <summary>
/// TEXT IN THE ERA'S HAND — the one place DrawList painters set type from the theme: the run is
/// sized by the era's scale and carries the era's <see cref="TextStyle"/> for its role, and the
/// measuring and fitting helpers measure exactly what will be set. The semantic text is never
/// rewritten (case is applied by the backends), so a test reading <see cref="TextCmd.Text"/> sees
/// what the screen meant to say in every era.
/// </summary>
public static class ThemeText
{
    /// <summary>Writes a run of <paramref name="designSize"/> px (scaled by the era) in the era's
    /// style for <paramref name="role"/>.</summary>
    public static void Write(this DrawList d, EraTheme t, double x, double y, string text, double designSize, Rgba color,
        TextAlign align = TextAlign.Left, FontRole role = FontRole.Body) =>
        d.Text(x, y, text, t.Type.Size(designSize), color, align, role, t.Type.For(role));

    /// <summary>
    /// A panel's display title. In the manuscript era (ornament motif <see cref="Motif.Illumination"/>)
    /// its first letter is an ILLUMINATED INITIAL — rubricated on a square of gold leaf, the way a
    /// medieval scribe opened a section; in every other era it is set like any title run. The title
    /// stays ONE run (its text is never split); the initial is the gold square beneath it and the first
    /// letter re-set in the rubric's red exactly over itself.
    /// </summary>
    public static void Title(this DrawList d, ITextMeasure m, EraTheme t, double x, double y, string text, double designSize, Rgba color)
    {
        bool illuminate = t.Ornament.Motif == Motif.Illumination && text.Length >= 2 && char.IsLetter(text[0]);
        double size = t.Type.Size(designSize);
        if (illuminate)
        {
            double gw = m.Width(t, text[..1], designSize, FontRole.Title);
            var box = new RectD(x - size * 0.14, y - size * 0.08, gw + size * 0.28, size * 1.16);
            d.Rect(box, t.Material.AccentSoft, t.Ink.Rule, 0.9);
            d.Rect(box.Inset(2), null, ThemeColor.Alpha(t.Ink.Rule, 0.45), 0.5);
        }
        d.Write(t, x, y, text, designSize, color, TextAlign.Left, FontRole.Title);
        if (illuminate) d.Write(t, x, y, text[..1], designSize, t.Material.Accent, TextAlign.Left, FontRole.Title);
    }

    /// <summary>The width of the run <see cref="Write"/> would set.</summary>
    public static double Width(this ITextMeasure m, EraTheme t, string text, double designSize, FontRole role = FontRole.Body) =>
        m.Width(DrawList.Latin1(text), t.Type.Size(designSize), role, t.Type.For(role));

    /// <summary>Truncates with an ellipsis so the SET run fits <paramref name="width"/>.</summary>
    public static string Fit(ITextMeasure m, EraTheme t, string text, double designSize, double width, FontRole role = FontRole.Body)
    {
        if (m.Width(t, text, designSize, role) <= width) return text;
        for (int n = text.Length - 1; n > 0; n--)
        {
            string s = text[..n].TrimEnd() + "...";
            if (m.Width(t, s, designSize, role) <= width) return s;
        }
        return "...";
    }

    /// <summary>Word-wraps so every SET line fits <paramref name="width"/>.</summary>
    public static List<string> Wrap(ITextMeasure m, EraTheme t, string text, double designSize, double width, FontRole role = FontRole.Body)
    {
        var lines = new List<string>();
        foreach (string para in text.Split('\n'))
        {
            string line = "";
            foreach (string word in para.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && m.Width(t, candidate, designSize, role) > width) { lines.Add(line); line = word; }
                else line = candidate;
            }
            lines.Add(line);
        }
        return lines;
    }
}
