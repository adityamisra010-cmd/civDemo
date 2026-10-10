using Sim.Ui.Render;
using Sim.Ui.Theme;

namespace Sim.Ui.Tests;

/// <summary>
/// THE TEXT-SIZE CENSUS (M5 polish UR-7; the UI audit's §2.3 measurement turned into a test): every character a
/// DrawList surface sets, by the size it is SET at, against the type scale's roles for the face it is set in — the
/// Caption floor (nothing that carries information below it) and the Body role (most of what the player reads at or
/// above it). Capitals compare with the capitals' sizes; whitespace is not counted.
/// </summary>
public sealed record TextCensus(string Surface, int Chars, int BelowCaption, int AtBody, IReadOnlyList<string> Below)
{
    public double BelowShare => Chars == 0 ? 0 : (double)BelowCaption / Chars;
    public double BodyShare => Chars == 0 ? 0 : (double)AtBody / Chars;

    public override string ToString() => string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"{Surface}: {Chars} chars, {100.0 * BelowShare:0.0}% below Caption, {100.0 * BodyShare:0.0}% at >= Body");

    /// <summary>The face a run is set in: its style's, else the backends' pre-theme face (Plex Serif for numbers).</summary>
    public static TypeFace FaceOf(TextCmd t) => t.Style?.Face ?? (t.Role == FontRole.Numeric ? TypeFace.PlexSerif : TypeFace.Garamond);

    public static bool Caps(TextCmd t) => t.Style is { Case: TextCase.Upper } || (t.Style is null && t.Role == FontRole.Caps);

    public static TextCensus Of(string surface, DrawList d, double uiScale = 1.0)
    {
        int chars = 0, below = 0, body = 0;
        var under = new List<string>();
        foreach (DrawCmd c in d.Commands)
        {
            if (c is not TextCmd t || string.IsNullOrEmpty(t.Text)) continue;
            int n = 0;
            foreach (char ch in t.Text) if (!char.IsWhiteSpace(ch)) n++;
            if (n == 0) continue;
            TypeFace face = FaceOf(t);
            bool caps = Caps(t);
            double floor = TypeScale.Floor(face) * uiScale, bodyPx = TypeScale.Px(TypeRole.Body, face, caps) * uiScale;
            chars += n;
            if (t.Size < floor - 0.011) { below += n; under.Add(t.Text + " @" + t.Size.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)); }
            if (t.Size >= bodyPx - 0.011) body += n;
        }
        return new TextCensus(surface, chars, below, body, under);
    }
}
