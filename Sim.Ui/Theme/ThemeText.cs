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
    /// its first letter is a DECORATED INITIAL, the way a scribe opened a section: rubricated (the
    /// rubric's red) in a title of ink, gilded (gold leaf on a dark keyline) in a title already set in
    /// rubric. In every other era it is set like any title run. The title stays ONE run (its text is
    /// never split, so readers of the command list see the whole title); the initial is that letter
    /// re-set exactly over itself, so it can never collide with the letters that follow it.
    /// </summary>
    public static void Title(this DrawList d, EraTheme t, double x, double y, string text, double designSize, Rgba color)
    {
        d.Write(t, x, y, text, designSize, color, TextAlign.Left, FontRole.Title);
        if (t.Ornament.Motif != Motif.Illumination || text.Length < 2 || !char.IsLetter(text[0])) return;
        string initial = text[..1];
        if (color == t.Material.Accent)
        {
            double k = Math.Max(0.8, t.Type.Size(designSize) / 28.0);
            d.Write(t, x + k, y + k, initial, designSize, t.Ink.Rule, TextAlign.Left, FontRole.Title);
            d.Write(t, x, y, initial, designSize, t.Material.AccentSoft, TextAlign.Left, FontRole.Title);
        }
        else d.Write(t, x, y, initial, designSize, t.Material.Accent, TextAlign.Left, FontRole.Title);
    }

    /// <summary>The width of the run <see cref="Write"/> would set.</summary>
    public static double Width(this ITextMeasure m, EraTheme t, string text, double designSize, FontRole role = FontRole.Body) =>
        m.Width(DrawList.Latin1(text), t.Type.Size(designSize), role, t.Type.For(role));

    /// <summary>The design size at which the SET run fits <paramref name="width"/>: the size itself when
    /// it fits, else scaled down (never below <paramref name="minScale"/> of it). For control and
    /// navigation labels, which keep every word in every era rather than lose one to an ellipsis.</summary>
    public static double FitSize(ITextMeasure m, EraTheme t, string text, double designSize, double width, FontRole role = FontRole.Body,
        double minScale = 0.7)
    {
        double w = m.Width(t, text, designSize, role);
        if (w <= width || w <= 0) return designSize;
        return Math.Max(designSize * minScale, Math.Floor(designSize * width / w * 20.0) / 20.0);
    }

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
