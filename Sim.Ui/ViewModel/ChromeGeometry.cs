using System;
using System.Collections.Generic;
using Sim.Ui.Art;

namespace Sim.Ui.ViewModel;

/// <summary>An axis-aligned rectangle in screen pixels, for the furniture and
/// widget rects the chrome computes. Same closed/open convention as
/// <see cref="PanelLayout.Overlap"/>: a shared edge is not an overlap.</summary>
public readonly record struct ScreenRect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public float CenterX => X + Width / 2f;
    public float CenterY => Y + Height / 2f;

    public static ScreenRect Of(in PanelRect p) => new(p.X, p.Y, p.Width, p.Height);

    /// <summary>Strict-interior intersection: positive shared area only.</summary>
    public static bool Overlap(in ScreenRect a, in ScreenRect b) =>
        a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

    /// <summary>Closed containment: <paramref name="inner"/> may touch the
    /// edges of this rect but not cross them.</summary>
    public bool Contains(in ScreenRect inner) =>
        inner.X >= X && inner.Y >= Y && inner.Right <= Right && inner.Bottom <= Bottom;
}

/// <summary>
/// WHERE a chrome element's header rule lies. An explicit placement, not a
/// bool, because the defect it replaces was a placement that had no name:
/// every window got "under the title bar", and three of the four chrome
/// elements have no title bar.
/// </summary>
public enum RulePlacement
{
    /// <summary>Under the first text line: y = top + frameHeight + gap. The
    /// pre-T4.19 formula, kept verbatim for the selection card, whose title
    /// IS its first line.</summary>
    UnderTitleLine,
    /// <summary>Under a frame-height header row inset by Margin — the context
    /// panel, whose header row carries a frame-height close button.</summary>
    UnderHeaderRow,
    /// <summary>Along the panel's top edge, inside the frame border: the
    /// boundary between world above and controls below.</summary>
    TopEdge,
    /// <summary>Along the panel's bottom edge, inside the frame border: the
    /// boundary between status above and world below.</summary>
    BottomEdge,
}

/// <summary>One chrome element: its rect and where its rule goes.</summary>
public readonly record struct ChromeElement(PanelRect Panel, RulePlacement Rule);

/// <summary>Whether a laid-out row fits its bar.</summary>
public enum Fits { Yes, No }

/// <summary>The command bar's row as laid out (UR-3): End Turn, the section buttons in roster order, the territory
/// toggle's left edge; <see cref="Compact"/> when the row had to tighten to fit; <see cref="Fit"/> whether even
/// that fits the bar.</summary>
public sealed record CommandRowLayout(ScreenRect EndTurn, IReadOnlyList<ScreenRect> Nav, float TerritoryX, Fits Fit)
{
    public bool Compact { get; init; }
}

/// <summary>
/// T4.19 lane D — THE CHROME'S FURNITURE AND WIDGET GEOMETRY, as data.
///
/// TWO DEFECTS FROM THE T4.18 PLAYTEST, both the same fault: rects that
/// existed only as literals inside draw calls, so nothing could say whether
/// they crossed.
///
/// 1. DrawPanelFurniture drew the header rule at y = top + GetFrameHeight()
///    + 2 in EVERY window — the right place under a title bar, and none of
///    the T4.18 chrome has a title bar. In the 56 px command bar that is
///    y = 31..39 (frame height 29 at the 19 px body face), which is the
///    middle of the button row at y = 12..42. The rule was drawn across the
///    buttons; the buttons, drawn later, covered it in patches. Confirmed by
///    reading the source, and now checkable: the rule rect and the button-row
///    rect are both here, and a test asserts they are disjoint.
///
/// 2. The context panel's close button was SameLine(Width - 46) and 24×20
///    with FramePadding (8,5) and a 19 px face: the padded interior is 8×10,
///    smaller than the glyph in both axes. ImGui's text alignment is
///    ImMax(pos, pos + slack * align) — CLAMPED to the top-left when the
///    slack is negative — so the glyph sat low and left regardless of
///    ButtonTextAlign. <see cref="LabelAnchor"/> models exactly that clamp;
///    the button is now frame-height square (interior 13×19 ≥ any 'x'), and
///    the test proves the anchor lands on the centre for the new rect and
///    does NOT for the old one.
///
/// Everything here is derived from <see cref="PanelLayout"/> and
/// <see cref="UiTheme"/> constants plus the frame height the renderer
/// measures (the renderer owns font metrics, as with SettlementSelection's
/// label rects). No ImGui: tested headless.
/// </summary>
public static class ChromeGeometry
{
    /// <summary>The nine-slice frame's border band, in screen px — the 12f
    /// that DrawPanelFurniture hands NineSlice. Edge rules sit INSIDE this
    /// band so the content region (panel inset by Margin) stays theirs.</summary>
    public const float FrameBorderPx = 12f;

    /// <summary>Horizontal inset of every rule from the panel edges (the
    /// former literal 6f / width-12f pair).</summary>
    public const float RuleInsetX = 6f;

    /// <summary>Breathing room between a rule and the row above or below it
    /// (the former literal +2f).</summary>
    public const float RuleGap = 2f;

    // Command bar widgets (M5 polish UR-3). End Turn is the PRIMARY verb — the only control here that changes the
    // world — so it is the row's height, wider and accent-filled; the section buttons are a step shorter, centred on
    // the row, each as wide as its MEASURED label plus padding (the fixed 104 px clipped "INSTITUTIONS" and
    // "SETTLEMENT", 102.6 and 93.8 px of label in an 84 px interior).
    /// <summary>The section buttons' height (UR-3: 30 → 32).</summary>
    public const float ButtonHeight = 32f;
    /// <summary>The End Turn button's height — the row's height.</summary>
    public const float EndTurnHeight = 36f;
    public const float EndTurnWidth = 200f;
    /// <summary>The narrowest section button, and the padding on each side of its measured label.</summary>
    public const float NavMinWidth = 104f, NavPadX = 14f;
    /// <summary>Kept for the SVG previews' design row: a section button of a typical label.</summary>
    public const float NavWidth = NavMinWidth;
    /// <summary>Between neighbouring section buttons.</summary>
    public const float NavGap = 6f;
    /// <summary>Between GROUPS: End Turn | sections | territory. Wider than
    /// NavGap because End Turn is the one control here that changes the
    /// world and the gap says so.</summary>
    public const float GroupGap = 24f;
    /// <summary>The row tightened to fit a narrow window (UR-3): a narrower End Turn, tighter label padding and
    /// group gaps — every label still whole.</summary>
    public const float CompactEndTurnWidth = 164f, CompactNavPadX = 8f, CompactNavMinWidth = 72f, CompactGroupGap = 14f;

    /// <summary>ImGui's ButtonTextAlign for the close glyph, set explicitly
    /// around the button rather than inherited from the style, so a future
    /// style change cannot move the glyph.</summary>
    public const float CloseGlyphAlign = 0.5f;

    /// <summary>The glyph on the close button. A plain ASCII 'x': both OFL
    /// faces and ImGui's fallback face carry it, so the button reads the
    /// same whichever font loaded (UiTheme's fallback path is not fatal, and
    /// a multiplication sign or a Private-Use icon would be a box on it).</summary>
    public const string CloseGlyph = "x";

    public static readonly ChromeElement Status = new(PanelLayout.Status, RulePlacement.BottomEdge);
    public static readonly ChromeElement Command = new(PanelLayout.Command, RulePlacement.TopEdge);
    /// <summary>UR-3: the selection card has a header row (its title, the settlement's name, is set in the 27 px
    /// title face, taller than the old first-line formula allowed), so its rule sits under that row.</summary>
    public static readonly ChromeElement Selection = new(PanelLayout.Selection, RulePlacement.UnderHeaderRow);
    public static readonly ChromeElement Context = new(PanelLayout.Context, RulePlacement.UnderHeaderRow);

    /// <summary>Every element that draws furniture, with its placement — the
    /// table the renderer's four call sites read from, so a test can walk
    /// the same table.</summary>
    public static IReadOnlyList<ChromeElement> Elements { get; } = [Status, Command, Selection, Context];

    /// <summary>The header rule's draw rect for one element. <paramref name="scale"/> is the UI scale (UR-1): the
    /// insets and gaps scale with it; the rule art keeps its height.</summary>
    public static ScreenRect HeaderRule(in ChromeElement element, float frameHeight, float scale = 1f)
    {
        PanelRect p = element.Panel;
        float h = PanelFurniture.HeaderRuleScreenHeightPx;
        float y = element.Rule switch
        {
            RulePlacement.UnderTitleLine => p.Y + frameHeight + RuleGap * scale,
            RulePlacement.UnderHeaderRow => HeaderRow(element, frameHeight, scale).Bottom + RuleGap * scale,
            // The strip's ink lives in its top 5.25 px (HeaderRuleBaker: heavy
            // rule 1–3, hairline 4.5–5.25, lozenge 0–4); the frame's own ink
            // line is at rows 2–3 of the border band (measured on
            // assets/ui/panel.png). Ending the strip AT the border band's
            // inner edge puts the heavy rule one paper pixel under the frame
            // line — a double rule — and keeps the content region clear.
            RulePlacement.TopEdge => p.Y + FrameBorderPx * scale - h,
            RulePlacement.BottomEdge => p.Y + p.Height - FrameBorderPx * scale,
            _ => throw new ArgumentOutOfRangeException(nameof(element), element.Rule, "unknown rule placement"),
        };
        return new ScreenRect(p.X + RuleInsetX * scale, y, p.Width - 2f * RuleInsetX * scale, h);
    }

    /// <summary>The header row of a panel: full content width, one frame
    /// height, at the Margin inset. The title text and the close button both
    /// live in it.</summary>
    public static ScreenRect HeaderRow(in ChromeElement element, float frameHeight, float scale = 1f)
    {
        PanelRect p = element.Panel;
        float margin = PanelLayout.Margin * scale;
        return new ScreenRect(p.X + margin, p.Y + margin, p.Width - 2f * margin, frameHeight);
    }

    /// <summary>The close button: square, frame-height, flush with the
    /// header row's right edge (so Margin from the panel edge).</summary>
    public static ScreenRect CloseButton(in ChromeElement element, float frameHeight, float scale = 1f)
    {
        ScreenRect row = HeaderRow(element, frameHeight, scale);
        return new ScreenRect(row.Right - frameHeight, row.Y, frameHeight, frameHeight);
    }

    /// <summary>Where a panel's section content begins: below its rule, with
    /// the same gap the rule keeps from the row above it.</summary>
    public static float ContentTop(in ChromeElement element, float frameHeight, float scale = 1f) =>
        HeaderRule(element, frameHeight, scale).Bottom + RuleGap * scale;

    /// <summary>The text line of the status band, for the disjointness pin:
    /// one primary-figure line at the Margin inset (UR-3: the 24 px KPI figures
    /// are the band's tallest text).</summary>
    public static ScreenRect StatusTextRow => new(
        Status.Panel.X + PanelLayout.Margin, Status.Panel.Y + PanelLayout.Margin,
        Status.Panel.Width - 2f * PanelLayout.Margin, StatusKpiPx);

    /// <summary>The status band's primary figures (TypeScale KPI in Plex) at s = 1.</summary>
    public const float StatusKpiPx = 24f;

    /// <summary>The status band's chips (research, Age): one frame height, centred between the band's top edge and
    /// its bottom-edge rule.</summary>
    public static ScreenRect StatusChipRow(in PanelRect band, float frameHeight, float scale = 1f)
    {
        PanelRect p = band;
        float top = p.Y, bottom = p.Y + p.Height - FrameBorderPx * scale;
        float y = top + (bottom - top - frameHeight) / 2f;
        return new ScreenRect(p.X + PanelLayout.Margin * scale, y, p.Width - 2f * PanelLayout.Margin * scale, frameHeight);
    }

    // ------------------------------------------------------------------
    // The command bar's row.
    // ------------------------------------------------------------------

    /// <summary>The one row every command-bar control sits on: Margin in
    /// from the bar on every side, End Turn's height tall.</summary>
    public static ScreenRect ButtonRow => RowOf(Command.Panel, 1f);

    private static ScreenRect RowOf(in PanelRect bar, float s) => new(
        bar.X + PanelLayout.Margin * s, bar.Y + PanelLayout.Margin * s,
        bar.Width - 2f * PanelLayout.Margin * s, EndTurnHeight * s);

    public static ScreenRect EndTurnButton => DesignRow.EndTurn;

    /// <summary>Section button <paramref name="index"/> of GameSections.Order in the DESIGN row (1280×800, s = 1,
    /// the design label widths) — the SVG previews' row. The game lays its row out from the labels it measures
    /// (<see cref="CommandRow"/>).</summary>
    public static ScreenRect NavButton(int index)
    {
        if (index < 0 || index >= GameSections.Order.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "no such section slot");
        return DesignRow.Nav[index];
    }

    /// <summary>Left edge of the territory toggle in the design row.</summary>
    public static float TerritoryToggleX => DesignRow.TerritoryX;

    /// <summary>A design estimate of a section label's width in the 20 px body face (the previews' row; the game
    /// measures the real atlas).</summary>
    public static float DesignLabelWidth(string label) => label.Length * 10.5f;

    /// <summary>The design estimate of the territory toggle's width (box, gap, label).</summary>
    public const float DesignTerritoryWidth = 130f;

    private static CommandRowLayout DesignRow
    {
        get
        {
            var widths = new float[GameSections.Order.Count];
            for (int i = 0; i < widths.Length; i++) widths[i] = DesignLabelWidth(GameSections.Label(GameSections.Order[i]));
            return CommandRow(PanelLayout.Command, widths, DesignTerritoryWidth);
        }
    }

    /// <summary>
    /// UR-3 — THE COMMAND BAR'S ROW, from measured labels: End Turn (the primary verb, the row's height) at the left;
    /// a GroupGap; the section buttons in roster order, each its label's width plus <see cref="NavPadX"/> a side
    /// (never under <see cref="NavMinWidth"/>), NavGap apart, a step shorter than the row and centred on it; a
    /// GroupGap; the territory toggle. When that does not fit the bar, the row tightens (<c>Compact</c>: a narrower
    /// End Turn, tighter padding and gaps); it never drops or clips a label. All × the UI scale.
    /// </summary>
    public static CommandRowLayout CommandRow(in PanelRect bar, IReadOnlyList<float> labelWidths, float territoryWidth, float scale = 1f)
    {
        ArgumentNullException.ThrowIfNull(labelWidths);
        ScreenRect row = RowOf(bar, scale);
        CommandRowLayout Lay(float endW, float pad, float minW, float group)
        {
            var end = new ScreenRect(row.X, row.Y, endW * scale, EndTurnHeight * scale);
            var nav = new ScreenRect[labelWidths.Count];
            float x = end.Right + group * scale;
            float h = ButtonHeight * scale, y = row.Y + (row.Height - h) / 2f;
            for (int i = 0; i < nav.Length; i++)
            {
                float w = Math.Max(minW * scale, labelWidths[i] + 2f * pad * scale);
                nav[i] = new ScreenRect(x, y, w, h);
                x = nav[i].Right + NavGap * scale;
            }
            float last = nav.Length == 0 ? end.Right : nav[^1].Right;
            float toggle = last + group * scale;
            return new CommandRowLayout(end, nav, toggle, toggle + territoryWidth > row.Right ? Fits.No : Fits.Yes);
        }
        CommandRowLayout wide = Lay(EndTurnWidth, NavPadX, NavMinWidth, GroupGap);
        if (wide.Fit == Fits.Yes) return wide;
        CommandRowLayout compact = Lay(CompactEndTurnWidth, CompactNavPadX, CompactNavMinWidth, CompactGroupGap);
        return compact with { Compact = true };
    }

    // ------------------------------------------------------------------
    // Label placement, ImGui's rule made explicit.
    // ------------------------------------------------------------------

    /// <summary>
    /// The CENTRE of a button label as ImGui places it (RenderTextClippedEx):
    /// the label's top-left is the padded rect's min plus slack × align, and
    /// the slack term is clamped at zero — ImMax(pos, pos + slack * align) —
    /// so a label larger than the padded interior is pinned to the interior's
    /// top-left, not centred. That clamp is the whole of defect 2: the old
    /// 24×20 button had an 8×10 interior under a 19 px face.
    /// </summary>
    public static (float X, float Y) LabelAnchor(
        in ScreenRect button, float textWidth, float textHeight,
        float framePaddingX, float framePaddingY, float alignX, float alignY)
    {
        float minX = button.X + framePaddingX, minY = button.Y + framePaddingY;
        float maxX = button.Right - framePaddingX, maxY = button.Bottom - framePaddingY;
        float px = minX, py = minY;
        if (alignX > 0f) px = Math.Max(px, px + (maxX - px - textWidth) * alignX);
        if (alignY > 0f) py = Math.Max(py, py + (maxY - py - textHeight) * alignY);
        return (px + textWidth / 2f, py + textHeight / 2f);
    }
}
