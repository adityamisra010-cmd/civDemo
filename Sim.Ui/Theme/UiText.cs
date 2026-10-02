using Sim.Ui.Render;

namespace Sim.Ui.Theme;

/// <summary>
/// THE IMGUI TEXT BOUNDARY — the atlas-side half of DrawList's Latin-1 normalisation. Text handed
/// straight to <c>ImGui.TextUnformatted</c> (the chrome's panels) never passes through
/// <see cref="DrawList.Latin1"/>, so an em dash in a view-model string drew as '?' (recon-ui §3).
/// Rather than fix strings one by one, the boundary is fixed where every ImGui string becomes
/// glyphs — the font atlas — with the SAME table DrawList uses (<see cref="DrawList.Substitutions"/>):
/// <list type="bullet">
/// <item>a character whose stand-in is ONE Latin-1 character (— – − ‐ ‑ ' ' " " • ‹ › ◀ ▶ ▸ Δ ✓ ✔) is
/// remapped in every face to that character's glyph — it renders exactly as DrawList text does;</item>
/// <item>a character whose stand-in is several characters (… → ← ≤ ≥) cannot be expressed as a glyph
/// remap, so its real glyph joins the atlas (all three faces carry it — measured with fontTools).</item>
/// </list>
/// Pure data here; <c>UiTheme.LoadFonts</c> applies it. The atlas stays Latin-1 plus these few code
/// points.
/// </summary>
public static class UiText
{
    /// <summary>Single-character substitutions applied as glyph remaps (from → to).</summary>
    public static IReadOnlyList<(char From, char To)> Remaps { get; } = BuildRemaps();

    /// <summary>Characters whose stand-in is several characters: their real glyphs are rasterised.</summary>
    public static IReadOnlyList<char> RealGlyphs { get; } = BuildReal();

    /// <summary>The atlas glyph ranges: Latin-1 (0x20–0xFF) plus <see cref="RealGlyphs"/>, as ImGui's
    /// zero-terminated list of inclusive [first, last] pairs.</summary>
    public static ushort[] GlyphRanges()
    {
        var r = new List<ushort> { 0x0020, 0x00FF };
        var real = new List<char>(RealGlyphs);
        real.Sort();
        foreach (char c in real) { r.Add(c); r.Add(c); }
        r.Add(0);
        return [.. r];
    }

    private static (char, char)[] BuildRemaps()
    {
        var list = new List<(char, char)>();
        foreach ((char from, string to) in DrawList.Substitutions)
            if (to.Length == 1) list.Add((from, to[0]));
        return [.. list];
    }

    private static char[] BuildReal()
    {
        var list = new List<char>();
        foreach ((char from, string to) in DrawList.Substitutions)
            if (to.Length > 1) list.Add(from);
        return [.. list];
    }
}
