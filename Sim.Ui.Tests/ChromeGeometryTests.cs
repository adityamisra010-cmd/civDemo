using System;
using Sim.Ui.Art;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// T4.19 lane D: the two chrome defects from the T4.18 playtest, pinned at
/// the geometry.
///
/// WHAT IS AND IS NOT COVERED. Rendering is not testable headless — ImGui
/// draw lists need a GraphicsDevice, and whether the GPU paints the glyph at
/// the anchor is one eyeball on the gate build. What IS computable before
/// that hop is asserted here: the rects the renderer draws into (it reads
/// the same ChromeGeometry, so the pinned rect IS the drawn rect), their
/// disjointness, and ImGui's label-placement rule applied to them. Every
/// test in this file was run against the pre-packet geometry and failed
/// there; the in-file negative controls reconstruct that geometry so the
/// teeth stay visible without a revert.
/// </summary>
public class ChromeGeometryTests
{
    private static readonly float Fh = UiTheme.FrameHeightPx;

    [Fact]
    public void TheDesignFrameHeight_Is29_FromThe19pxFaceAndFramePadding5()
    {
        // GetFrameHeight() = FontSize + 2 × FramePadding.y. Every rect below
        // that depends on the frame height depends on THIS; a style change
        // that moves it must come here first.
        Assert.Equal(19f, UiTheme.BodyFontPx);
        Assert.Equal(5f, UiTheme.FramePaddingPx.Y);
        Assert.Equal(29f, Fh);
    }

    // ------------------------------------------------------------------
    // DEFECT 1 — the command bar's rule crossed its buttons.
    // ------------------------------------------------------------------

    [Fact]
    public void CommandBar_TheRuleAndTheButtonRow_DoNotIntersect()
    {
        ScreenRect rule = ChromeGeometry.HeaderRule(ChromeGeometry.Command, Fh);
        ScreenRect row = ChromeGeometry.ButtonRow;
        Assert.False(ScreenRect.Overlap(rule, row),
            $"rule y {rule.Y}..{rule.Bottom} crosses the button row y {row.Y}..{row.Bottom}");
        // And the rule is where the packet asked for it: along the TOP edge,
        // inside the frame's border band, as the world | controls boundary.
        Assert.Equal(RulePlacement.TopEdge, ChromeGeometry.Command.Rule);
        Assert.True(rule.Y >= PanelLayout.Command.Y);
        Assert.True(rule.Bottom <= PanelLayout.Command.Y + ChromeGeometry.FrameBorderPx);
    }

    [Fact]
    public void ThePreT419Placement_DidCrossTheButtonRow_MeasuredNotAssumed()
    {
        // The negative control. The old DrawPanelFurniture used the
        // under-title-bar formula in every window: y = top + frameHeight + 2.
        // On the command bar that is 31..39 in a 12..42 button row.
        var old = new ChromeElement(PanelLayout.Command, RulePlacement.UnderTitleLine);
        ScreenRect rule = ChromeGeometry.HeaderRule(old, Fh);
        Assert.Equal(PanelLayout.Command.Y + 31f, rule.Y);
        Assert.Equal(PanelLayout.Command.Y + 39f, rule.Bottom);
        Assert.True(ScreenRect.Overlap(rule, ChromeGeometry.ButtonRow));
    }

    [Fact]
    public void TheButtonRow_FitsInsideTheCommandBar_WithTheExistingMargin()
    {
        ScreenRect bar = ScreenRect.Of(PanelLayout.Command);
        var inner = new ScreenRect(bar.X + PanelLayout.Margin, bar.Y + PanelLayout.Margin,
            bar.Width - 2f * PanelLayout.Margin, bar.Height - 2f * PanelLayout.Margin);
        ScreenRect row = ChromeGeometry.ButtonRow;
        Assert.True(inner.Contains(row), $"row {row} leaves the Margin-inset bar {inner}");
        // 30 px of button in 56 - 24 = 32 px of room: the bar is exactly as
        // deep as its row needs plus the Margin. Pinned so a taller button
        // cannot quietly be pushed into the frame border.
        Assert.Equal(ChromeGeometry.ButtonHeight, row.Height);
        Assert.True(row.Bottom <= bar.Bottom - PanelLayout.Margin);
    }

    [Fact]
    public void EveryCommandBarControl_SitsOnTheRow_InOrder_WithTheDeclaredGaps()
    {
        ScreenRect row = ChromeGeometry.ButtonRow;
        ScreenRect endTurn = ChromeGeometry.EndTurnButton;
        Assert.True(row.Contains(endTurn));
        Assert.Equal(row.X, endTurn.X);   // End Turn leads the row

        ScreenRect prev = endTurn;
        for (int i = 0; i < GameSections.Order.Count; i++)
        {
            ScreenRect nav = ChromeGeometry.NavButton(i);
            Assert.True(row.Contains(nav), $"section slot {i} {nav} leaves the row");
            Assert.Equal(row.Y, nav.Y);
            Assert.Equal(ChromeGeometry.ButtonHeight, nav.Height);
            float gap = i == 0 ? ChromeGeometry.GroupGap : ChromeGeometry.NavGap;
            Assert.Equal(prev.Right + gap, nav.X);
            Assert.False(ScreenRect.Overlap(prev, nav));
            prev = nav;
        }

        // The territory toggle is a GroupGap after the last section and its
        // anchor is still inside the row — with the row's width to spare,
        // since its label width is the renderer's to measure.
        float toggleX = ChromeGeometry.TerritoryToggleX;
        Assert.Equal(prev.Right + ChromeGeometry.GroupGap, toggleX);
        Assert.True(toggleX < row.Right - 100f,
            $"the toggle anchor at {toggleX} leaves under 100 px of the row for its label");
    }

    [Fact]
    public void NavButton_RejectsSlotsOutsideTheRoster()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChromeGeometry.NavButton(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChromeGeometry.NavButton(GameSections.Order.Count));
    }

    [Fact]
    public void StatusBand_TheRuleRunsAlongTheBottomEdge_ClearOfTheTextLine()
    {
        ScreenRect rule = ChromeGeometry.HeaderRule(ChromeGeometry.Status, Fh);
        Assert.Equal(RulePlacement.BottomEdge, ChromeGeometry.Status.Rule);
        Assert.False(ScreenRect.Overlap(rule, ChromeGeometry.StatusTextRow));
        Assert.True(rule.Y >= PanelLayout.Status.Y + PanelLayout.Status.Height - ChromeGeometry.FrameBorderPx);
        Assert.True(rule.Bottom <= PanelLayout.Status.Y + PanelLayout.Status.Height);
    }

    [Fact]
    public void TheBars_KeepTheirWholeContentRegion_TheRuleLivesInTheFrameBorder()
    {
        // The property behind both bar placements: an edge rule may not
        // enter the panel inset by Margin, which is where widgets go.
        foreach (ChromeElement bar in new[] { ChromeGeometry.Status, ChromeGeometry.Command })
        {
            PanelRect p = bar.Panel;
            var content = new ScreenRect(p.X + PanelLayout.Margin, p.Y + PanelLayout.Margin,
                p.Width - 2f * PanelLayout.Margin, p.Height - 2f * PanelLayout.Margin);
            ScreenRect rule = ChromeGeometry.HeaderRule(bar, Fh);
            Assert.False(ScreenRect.Overlap(rule, content), p.Title);
        }
    }

    [Fact]
    public void EveryRule_LiesInsideItsPanel_AndKeepsTheHorizontalInset()
    {
        foreach (ChromeElement e in ChromeGeometry.Elements)
        {
            ScreenRect rule = ChromeGeometry.HeaderRule(e, Fh);
            Assert.True(ScreenRect.Of(e.Panel).Contains(rule), e.Panel.Title);
            Assert.Equal(e.Panel.X + ChromeGeometry.RuleInsetX, rule.X);
            Assert.Equal(e.Panel.Width - 2f * ChromeGeometry.RuleInsetX, rule.Width);
            Assert.Equal(PanelFurniture.HeaderRuleScreenHeightPx, rule.Height);
        }
    }

    [Fact]
    public void TheSelectionCard_KeepsThePrePacketFormula_Verbatim()
    {
        // Not a defect in the playtest and not in this packet's scope: its
        // rule stays exactly where T4.18 put it, so the card's look cannot
        // have moved by accident of the refactor.
        ScreenRect rule = ChromeGeometry.HeaderRule(ChromeGeometry.Selection, Fh);
        Assert.Equal(RulePlacement.UnderTitleLine, ChromeGeometry.Selection.Rule);
        Assert.Equal(PanelLayout.Selection.Y + Fh + 2f, rule.Y);
    }

    [Fact]
    public void TheElementTable_IsTheFourChromeRects_OneEach()
    {
        // The renderer's four furniture calls read from this table; the
        // tests walk it. A fifth element or a missing one is the drift.
        Assert.Equal(4, ChromeGeometry.Elements.Count);
        Assert.Equal(PanelLayout.All.Select(p => p.Title).ToArray(),
            ChromeGeometry.Elements.Select(e => e.Panel.Title).ToArray());
    }

    // ------------------------------------------------------------------
    // DEFECT 2 — the close glyph was not centred in its hitbox.
    // ------------------------------------------------------------------

    [Fact]
    public void CloseButton_IsSquare_FrameHeight_FlushRightInTheHeaderRow_MarginFromThePanelEdge()
    {
        ScreenRect row = ChromeGeometry.HeaderRow(ChromeGeometry.Context, Fh);
        ScreenRect close = ChromeGeometry.CloseButton(ChromeGeometry.Context, Fh);
        Assert.Equal(Fh, close.Width);
        Assert.Equal(Fh, close.Height);
        Assert.True(row.Contains(close), $"close {close} leaves the header row {row}");
        Assert.Equal(row.Y, close.Y);
        Assert.Equal(row.Right, close.Right);
        Assert.Equal(PanelLayout.Context.X + PanelLayout.Context.Width - PanelLayout.Margin, close.Right);
    }

    [Theory]
    [InlineData(4f)]
    [InlineData(6f)]
    [InlineData(9f)]    // ~an 'x' in EB Garamond at 19 px
    [InlineData(13f)]   // the whole padded interior
    public void CloseGlyph_AnchorIsTheRectCentre_AtEveryPlausibleGlyphWidth(float glyphWidth)
    {
        // ImGui's placement rule (ChromeGeometry.LabelAnchor) with the
        // alignment the renderer pushes explicitly. Text height is the font
        // size for a single line. Interior = 29 - 16 by 29 - 10 = 13 × 19,
        // so any glyph up to 13 px wide centres exactly.
        ScreenRect close = ChromeGeometry.CloseButton(ChromeGeometry.Context, Fh);
        (float ax, float ay) = ChromeGeometry.LabelAnchor(close, glyphWidth, UiTheme.BodyFontPx,
            UiTheme.FramePaddingPx.X, UiTheme.FramePaddingPx.Y,
            ChromeGeometry.CloseGlyphAlign, ChromeGeometry.CloseGlyphAlign);
        Assert.Equal(close.CenterX, ax, 3);
        Assert.Equal(close.CenterY, ay, 3);
    }

    [Fact]
    public void ThePreT419CloseButton_PinnedTheGlyphLowAndLeft_MeasuredNotAssumed()
    {
        // The negative control: SameLine(Width - 46) and a 24 × 20 button.
        // Padded interior 8 × 10 under a 9 × 19 glyph, so ImGui's ImMax
        // clamp wins in both axes: the glyph's centre lands 0.5 px right of
        // and 4.5 px BELOW the rect's — the low, off-centre 'x' the playtest
        // saw. The same LabelAnchor that proves the new rect centred proves
        // the old one did not, so the model is not a tautology.
        var old = new ScreenRect(PanelLayout.Context.X + PanelLayout.Context.Width - 46f,
            PanelLayout.Context.Y + PanelLayout.Margin, 24f, 20f);
        (float ax, float ay) = ChromeGeometry.LabelAnchor(old, 9f, UiTheme.BodyFontPx,
            UiTheme.FramePaddingPx.X, UiTheme.FramePaddingPx.Y, 0.5f, 0.5f);
        Assert.Equal(0.5f, ax - old.CenterX, 3);
        Assert.Equal(4.5f, ay - old.CenterY, 3);
    }

    [Fact]
    public void LabelAnchor_IsImGuisRule_NotACentreFunction()
    {
        // Positive control on the model itself: with alignment 0 the anchor
        // is the padded top-left plus half the text, whatever the rect —
        // i.e. the function reads its align argument rather than returning
        // the centre by construction.
        var r = new ScreenRect(100f, 200f, 40f, 40f);
        (float ax, float ay) = ChromeGeometry.LabelAnchor(r, 10f, 20f, 8f, 5f, 0f, 0f);
        Assert.Equal(100f + 8f + 5f, ax, 3);
        Assert.Equal(200f + 5f + 10f, ay, 3);
    }

    [Fact]
    public void ContextPanel_TheHeaderRowAndTheCloseButton_AreClearOfTheRule_AndContentStartsBelowIt()
    {
        // Making the close button frame-height would have re-created defect
        // 1 in this panel: the old under-title formula (y = top + 31) crosses
        // a 12..41 header row. The rule is under the ROW now, and the
        // section content is placed below the rule rather than on it.
        ScreenRect rule = ChromeGeometry.HeaderRule(ChromeGeometry.Context, Fh);
        ScreenRect row = ChromeGeometry.HeaderRow(ChromeGeometry.Context, Fh);
        ScreenRect close = ChromeGeometry.CloseButton(ChromeGeometry.Context, Fh);
        Assert.Equal(RulePlacement.UnderHeaderRow, ChromeGeometry.Context.Rule);
        Assert.False(ScreenRect.Overlap(rule, row));
        Assert.False(ScreenRect.Overlap(rule, close));
        Assert.True(rule.Y >= row.Bottom);
        Assert.True(ChromeGeometry.ContentTop(ChromeGeometry.Context, Fh) >= rule.Bottom);

        // ...and the old formula on this panel WOULD have crossed the row.
        var old = new ChromeElement(PanelLayout.Context, RulePlacement.UnderTitleLine);
        Assert.True(ScreenRect.Overlap(ChromeGeometry.HeaderRule(old, Fh), close));
    }

    [Fact]
    public void TheCloseGlyph_IsPlainAscii_SoEveryLoadedFaceCarriesIt()
    {
        // UiTheme falls back to ImGui's built-in face when a file is missing
        // and the game must still open; a multiplication sign or an icon
        // codepoint would render as a box there. Basic Latin is in every
        // atlas ImGui builds by default and in both OFL faces.
        Assert.Equal("x", ChromeGeometry.CloseGlyph);
        Assert.All(ChromeGeometry.CloseGlyph, c => Assert.True(c < 0x80));
    }

    [Fact]
    public void ScreenRect_Overlap_DetectsIntrusion_IgnoresSharedEdges_NotVacuous()
    {
        // The command bar's rule ENDS where its button row BEGINS (y = 12),
        // so the shared-edge case is the one that matters here.
        var a = new ScreenRect(0, 0, 10, 10);
        Assert.True(ScreenRect.Overlap(a, new ScreenRect(9, 9, 10, 10)));
        Assert.False(ScreenRect.Overlap(a, new ScreenRect(0, 10, 10, 10)));   // edge-adjacent below
        Assert.False(ScreenRect.Overlap(a, new ScreenRect(10, 0, 10, 10)));   // edge-adjacent right
        Assert.True(a.Contains(new ScreenRect(0, 0, 10, 10)));                // closed containment
        Assert.False(a.Contains(new ScreenRect(0, 0, 10, 11)));
    }
}
