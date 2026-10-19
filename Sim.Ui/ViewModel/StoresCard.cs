using Sim.Ui.Render;
using Sim.Ui.Theme;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.ViewModel;

/// <summary>
/// M5 resources view — THE STRIP'S HOVER CARD, painted as backend-agnostic DrawList commands (the info hint's way): the
/// game replays them onto ImGui's foreground list while the pointer rests on a strip group, and the preview tool writes
/// the same commands to SVG. A raised panel under the band: the group's title, then each line of
/// <see cref="EmpireStores.CardLines"/> wrapped to the card. Painting only; it reads nothing but its arguments.
/// </summary>
public static class StoresCard
{
    /// <summary>The card's text width at UI scale 1.</summary>
    public const double TextWidth = 380;

    /// <summary>The design sizes of the title and the lines.</summary>
    public const double TitleSize = 15, LineSize = 13;

    private static Rgba A(Rgba c, double a) => c with { A = (byte)Math.Clamp(Math.Round(c.A * a), 0, 255) };

    /// <summary>Paints the card with its top-left near (<paramref name="x"/>, <paramref name="y"/>), kept inside the
    /// <paramref name="viewW"/> × <paramref name="viewH"/> view; returns its rect.</summary>
    public static RectD Paint(DrawList d, ITextMeasure m, EraTheme t, double x, double y, double viewW, double viewH,
        string title, IReadOnlyList<string> lines, double scale)
    {
        ArgumentNullException.ThrowIfNull(d);
        ArgumentNullException.ThrowIfNull(m);
        ArgumentNullException.ThrowIfNull(t);
        ArgumentNullException.ThrowIfNull(lines);
        double pad = 10 * scale, width = TextWidth * scale;
        double titleSize = TitleSize * scale, lineSize = LineSize * scale;
        double titleLine = t.Type.Line(titleSize), line = t.Type.Line(lineSize);
        var wrapped = new List<(string Text, bool First)>();
        foreach (string l in lines)
        {
            bool first = true;
            foreach (string w in FlowText.Wrap(m, l, TypeRole.Secondary, scale, width))
            {
                wrapped.Add((w, first));
                first = false;
            }
        }
        double h = pad * 2 + titleLine + 4 * scale + wrapped.Count * line + Math.Max(0, lines.Count - 1) * 3 * scale;
        double w0 = width + pad * 2;
        double cx = Math.Max(0, Math.Min(x, viewW - w0 - 4));
        double cy = Math.Max(0, Math.Min(y, viewH - h - 4));
        var r = new RectD(cx, cy, w0, h);
        d.Rect(r, A(t.Material.PanelRaised, 0.97), A(t.Material.Border, 0.9), Math.Max(1, scale), 4 * scale);
        double ty = cy + pad;
        d.Write(t, cx + pad, ty, title, titleSize, t.TextInk.Accent, TextAlign.Left, FontRole.Heading);
        ty += titleLine + 4 * scale;
        int idx = 0;
        foreach ((string text, bool first) in wrapped)
        {
            if (first && idx > 0) ty += 3 * scale;
            d.Write(t, cx + pad + (first ? 0 : 10 * scale), ty, text, lineSize, idx == 0 ? t.Ink.Text : t.Ink.TextSoft);
            ty += line;
            idx++;
        }
        return r;
    }

    /// <summary>The strip's change mark, a shape (never a glyph the era's face may lack): an up triangle (rising), a down
    /// triangle (falling), a short bar (steady); nothing before the first turn. Centred at (<paramref name="cx"/>,
    /// <paramref name="cy"/>), <paramref name="size"/> tall.</summary>
    public static void Mark(DrawList d, StoreTrend trend, double cx, double cy, double size, Rgba ink)
    {
        ArgumentNullException.ThrowIfNull(d);
        double h = size / 2, w = size * 0.55;
        switch (trend)
        {
            case StoreTrend.Rising:
                d.Polygon([(cx - w, cy + h * 0.8), (cx + w, cy + h * 0.8), (cx, cy - h * 0.8)], ink);
                break;
            case StoreTrend.Falling:
                d.Polygon([(cx - w, cy - h * 0.8), (cx + w, cy - h * 0.8), (cx, cy + h * 0.8)], ink);
                break;
            case StoreTrend.Steady:
                d.Rect(new RectD(cx - w, cy - size * 0.08, 2 * w, size * 0.16), ink);
                break;
        }
    }
}
