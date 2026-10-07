using Sim.Ui.Render;

namespace Sim.Ui.Theme;

/// <summary>
/// ERA-INVARIANT TEXT FLOW (M5 polish, UI readability UR-4/UR-5). A panel whose content flows — the Age panel, the
/// Advance-Age flow, the research detail panel — must still keep its regions and hit rects the same in every era
/// (ADR-033 D8 continuity: nothing measured in an era's type moves a region). So the flow is computed in a REFERENCE
/// type — EB Garamond at the role's size × the largest era size scale — measured unstyled and widened for the widest
/// era's weight and tracking: line breaks and line slots are the same in every era, and each era's own run (Garamond
/// or Plex, plain or tracked capitals) always fits the slot it is set in.
/// </summary>
public static class FlowText
{
    /// <summary>The largest era size scale (A1's heavier, larger primitive hand); pinned ≥ every era's.</summary>
    public const double MaxSizeScale = 1.06;

    /// <summary>The leading of a reference line slot, × the reference size.</summary>
    public const double SlotLeading = 1.28;

    /// <summary>The widening of an unstyled measure for the widest era's weight (double-strike bold) and tracking.</summary>
    public const double BoldWiden = 1.06, CapsWiden = 1.16;

    /// <summary>The reference size of <paramref name="role"/> at UI scale <paramref name="scale"/> (Garamond × the
    /// largest era scale).</summary>
    public static double RefPx(TypeRole role, double scale, bool caps = false) =>
        TypeScale.Px(role, TypeFace.Garamond, caps) * MaxSizeScale * scale;

    /// <summary>The era-invariant height of one line of <paramref name="role"/>.</summary>
    public static double Slot(TypeRole role, double scale, bool caps = false) => RefPx(role, scale, caps) * SlotLeading;

    /// <summary>The era-invariant width of a run (the widest era's set width, estimated from the reference).</summary>
    public static double Width(ITextMeasure m, string text, TypeRole role, double scale, FontRole font = FontRole.Body)
    {
        bool caps = font == FontRole.Caps;
        string set = caps ? text.ToUpperInvariant() : text;
        double w = m.Width(DrawList.Latin1(set), RefPx(role, scale, caps), font == FontRole.Numeric ? FontRole.Numeric : FontRole.Body);
        return w * (caps ? CapsWiden : font is FontRole.Heading or FontRole.Title ? BoldWiden : 1.0);
    }

    /// <summary>Word-wraps by the era-invariant width: the same lines in every era.</summary>
    public static List<string> Wrap(ITextMeasure m, string text, TypeRole role, double scale, double width, FontRole font = FontRole.Body)
    {
        var lines = new List<string>();
        foreach (string para in text.Split('\n'))
        {
            string line = "";
            foreach (string word in para.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && Width(m, candidate, role, scale, font) > width) { lines.Add(line); line = word; }
                else line = candidate;
            }
            lines.Add(line);
        }
        return lines;
    }
}
