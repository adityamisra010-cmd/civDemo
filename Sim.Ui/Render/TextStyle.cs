namespace Sim.Ui.Render;

/// <summary>The three faces the UI ships in <c>assets/fonts</c>, all SIL OFL 1.1: EB Garamond (the
/// bible's humanist serif), IBM Plex Serif (the lining-figure companion) and IBM Plex Sans (the
/// industrial and modern eras' face, docs/architecture/era-ui.md).</summary>
public enum TypeFace { Garamond = 0, PlexSerif = 1, PlexSans = 2 }

/// <summary>How a run of text is cased when it is SET. The text a <see cref="TextCmd"/> carries is
/// never rewritten — tests and hit-tests read what the caller wrote — the backends apply the case
/// when they draw or measure it.</summary>
public enum TextCase { AsWritten = 0, Upper = 1 }

/// <summary>
/// THE TYPESETTING OF A RUN: face, weight (CSS 100–900; the ImGui backend renders 600 and above as
/// a double strike, the SVG writer as font-weight, so the game and the previews agree), tracking
/// (extra space after every letter, in em) and case. Carried by <see cref="TextCmd.Style"/>; a null
/// style is the pre-theme default for the run's <see cref="FontRole"/>, so painters that predate
/// the era theme render exactly as before.
/// </summary>
public readonly record struct TextStyle(TypeFace Face, int Weight, double TrackingEm, TextCase Case)
{
    /// <summary>Weight at which both backends embolden.</summary>
    public const int BoldWeight = 600;

    public bool Bold => Weight >= BoldWeight;

    /// <summary>The characters as set: <paramref name="text"/> cased per <see cref="Case"/>. Stays in
    /// Latin-1 (a letter whose upper case lies outside it, e.g. ÿ → Ÿ, keeps its own form), so the
    /// atlas and the SVG agree on every glyph.</summary>
    public string Apply(string text)
    {
        if (Case != TextCase.Upper || string.IsNullOrEmpty(text)) return text;
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char u = char.ToUpperInvariant(chars[i]);
            if (u <= 'ÿ') chars[i] = u;
        }
        return new string(chars);
    }

    /// <summary>Extra advance after each letter but the last, in px, at <paramref name="size"/>.</summary>
    public double TrackingPx(double size) => TrackingEm * size;
}
