using ImGuiNET;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.Actions;
using Sim.Ui.Art;
using Sim.Ui.Headless;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Xunit;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Tests;

/// <summary>
/// M5 POLISH, UI READABILITY (Director directive 2026-10-06 §2; packets UR-1 tokens and scale, UR-2 calm reading
/// surface, UR-3 chrome, UR-6 action surface). The type scale and its floors, the UI scale's arithmetic, the atlas
/// ladder (no run minified), the text inks' contrast floors in every era (after the interface's share of the fibre
/// overlay), the state fills' separation, the overlay split, the calm content rects, the research veil that no longer
/// dims words, the measured command bar, and the action surface's control states, block order and blockers.
/// </summary>
public class ReadabilityTests(CanonicalTurnOneFixture fx) : IClassFixture<CanonicalTurnOneFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    // ------------------------------------------------------------------ UR-1: the type scale

    [Fact]
    public void TypeScale_RolesDescend_TheCaptionIsTheFloor_AndBodyIsAnEightPixelXHeightInEveryFace()
    {
        foreach (TypeFace face in new[] { TypeFace.Garamond, TypeFace.PlexSerif, TypeFace.PlexSans })
        {
            double display = TypeScale.Px(TypeRole.Display, face), title = TypeScale.Px(TypeRole.Title, face),
                heading = TypeScale.Px(TypeRole.Heading, face), body = TypeScale.Px(TypeRole.Body, face),
                secondary = TypeScale.Px(TypeRole.Secondary, face), caption = TypeScale.Px(TypeRole.Caption, face);
            Assert.True(display > title && title > heading && heading >= body && body > secondary && secondary > caption, face.ToString());
            foreach (TypeRole role in TypeScale.Roles)
            {
                Assert.True(TypeScale.Px(role, face) >= TypeScale.Floor(face), face + " " + role);
                Assert.True(TypeScale.Px(role, face, caps: true) >= TypeScale.Floor(face), face + " caps " + role);
            }
            // Sizes are set by x-height: the body reads the same size in Garamond and in Plex.
            Assert.InRange(TypeScale.XHeight(face, body), 7.9, 8.3);
        }
        // The spec's reference sizes (1080p, s = 1).
        Assert.Equal(20, TypeScale.Px(TypeRole.Body, TypeFace.Garamond));
        Assert.Equal(16, TypeScale.Px(TypeRole.Body, TypeFace.PlexSans));
        Assert.Equal(17, TypeScale.Px(TypeRole.Data, TypeFace.PlexSerif));
        Assert.Equal(24, TypeScale.Px(TypeRole.Kpi, TypeFace.PlexSerif));
        Assert.Equal(27, TypeScale.Px(TypeRole.Title, TypeFace.Garamond));
        Assert.Equal(15, TypeScale.Px(TypeRole.Caption, TypeFace.Garamond));
        Assert.Equal(13, TypeScale.Px(TypeRole.Caption, TypeFace.PlexSans));
        Assert.Equal(14, TypeScale.Px(TypeRole.Caption, TypeFace.PlexSans, caps: true));
    }

    [Fact]
    public void NoEra_ShrinksTheType()
    {
        foreach (EraTheme t in EraThemes.All) Assert.True(t.Type.SizeScale >= 1.0, t.Era + " " + t.Type.SizeScale);
        // A7–A9 (0.98, 0.95, 0.94 before UR-1) are the reference size; the early eras keep their heavier, larger hand.
        Assert.Equal(1.0, EraThemes.For(UiEra.Modern).Type.SizeScale);
        Assert.Equal(1.06, EraThemes.For(UiEra.Prehistoric).Type.SizeScale);
    }

    // ------------------------------------------------------------------ UR-1: the UI scale

    [Fact]
    public void UiScale_FollowsTheWindowHeight_SnapsToEighths_NeverShrinksBelowTheReference()
    {
        Assert.Equal(1.0, UiScale.Auto(1080));
        Assert.Equal(1.0, UiScale.Auto(768));     // 1366×768: the layout reflows, the type does not shrink
        Assert.Equal(1.0, UiScale.Auto(800));
        Assert.Equal(1.375, UiScale.Auto(1440));  // 1.333 snaps to the nearest eighth
        Assert.Equal(2.0, UiScale.Auto(2160));
        Assert.Equal(2.0, UiScale.Auto(4320));    // clamped
        Assert.Equal(1.5, UiScale.Auto(1080, osDpiScale: 1.5));   // a 150 % display at 1080 p
        Assert.Equal(1.0, UiScale.Auto(1080, osDpiScale: double.NaN));
        Assert.Equal(1.25, UiScale.Snap(1.2));
        Assert.Equal(1.125, UiScale.Snap(1.1));
    }

    [Fact]
    public void UiScale_HasHysteresis_SoAWindowDraggedAcrossAStepDoesNotRebuildBackAndForth()
    {
        Assert.Equal(1.0, UiScale.Follow(1.0, 1250));    // raw 1.157: within 3/16 of 1.0 — kept
        Assert.Equal(1.25, UiScale.Follow(1.0, 1300));   // raw 1.204: left the band — snapped
        Assert.Equal(1.375, UiScale.Follow(1.375, 1300)); // and back down: kept until it leaves the band
        Assert.Equal(1.125, UiScale.Follow(1.375, 1180)); // raw 1.093
        Assert.Equal(1.0, UiScale.Follow(1.0, 600));      // below the reference: never below 1
    }

    [Fact]
    public void UiScale_UserSteps_StepClampAndParse()
    {
        Assert.Equal([0.9, 1.0, 1.1, 1.25, 1.5], UiScale.UserSteps);
        Assert.Equal(1.1, UiScale.StepUser(1.0, +1));
        Assert.Equal(1.0, UiScale.StepUser(1.1, -1));
        Assert.Equal(1.5, UiScale.StepUser(1.5, +1));   // clamped at the ends
        Assert.Equal(0.9, UiScale.StepUser(0.9, -1));
        Assert.Equal(1.25, UiScale.ParseUser("1.25"));
        Assert.Equal(1.5, UiScale.ParseUser("2"));       // an unknown value snaps to the nearest step
        Assert.Null(UiScale.ParseUser("large"));
        Assert.Null(UiScale.ParseUser("-1"));
        Assert.Equal(1.375 * 1.1, UiScale.Effective(1.375, 1.1), 12);
        Assert.Equal(1.25, UiArgs.UserScale(["--seed", "7", "--ui-scale", "1.25"]));
        Assert.Equal(1.0, UiArgs.UserScale(["--seed", "7"]));
    }

    // ------------------------------------------------------------------ UR-1: the atlas ladder

    [Fact]
    public void TheRasterLadder_HoldsEveryRoleSize_AndNoRunIsMinifiedByMoreThanTenPercent()
    {
        foreach (double scale in new[] { 1.0, 1.375, 2.0 })
            foreach (TypeFace face in new[] { TypeFace.Garamond, TypeFace.PlexSerif, TypeFace.PlexSans })
            {
                float[] sizes = UiTheme.RasterSizes(face, scale);
                for (int i = 1; i < sizes.Length; i++) Assert.True(sizes[i] > sizes[i - 1], "ascending, distinct");
                foreach (double px in TypeScale.SizesFor(face))
                    Assert.Contains(UiTheme.ScaledPx(px, scale), sizes);
                // The smallest raster at least as large as a run (Fonts.Face's rule) is never more than 10 % larger.
                for (double px = 10.0; px <= 40.0; px += 0.25)
                {
                    float raster = sizes[^1];
                    foreach (float s in sizes) if (s >= px - 0.01) { raster = s; break; }
                    Assert.True(px / raster >= 0.9, $"{face} at s {scale}: {px} px drawn from the {raster} px raster");
                }
            }
    }

    [Collection("ImGui context")]
    public class Atlas
    {
        [Fact]
        public void TheRealAtlas_SetsEveryRoleFromItsOwnRaster_AtEveryScale()
        {
            foreach (double scale in new[] { 1.0, 1.375 })
            {
                using var gui = new HeadlessImGui(Assets(), 1920, 1080, uiScale: scale);
                Assert.Equal((float)scale, gui.Fonts.Scale);
                foreach (TypeFace face in new[] { TypeFace.Garamond, TypeFace.PlexSerif, TypeFace.PlexSans })
                    foreach (TypeRole role in TypeScale.Roles)
                    {
                        float px = gui.Fonts.RolePx(face, role);
                        Assert.Equal(UiTheme.ScaledPx(TypeScale.Px(role, face), scale), px);
                        Assert.Equal(px, gui.Fonts.Face(face, px).FontSize);
                    }
                // A map label at 12.5 px is set from the 13 px raster, not the 25 px one at half size (UR-1).
                Assert.Equal(13f, gui.Fonts.Face(TypeFace.Garamond, 12.5).FontSize);
                Assert.Contains("atlas ", gui.Fonts.Note, StringComparison.Ordinal);
            }
        }

        /// <summary>UR-3: every section label fits its command-bar button — measured with the real atlas in every
        /// era's body face — at the narrowest window and at the scales the reference sizes grow to; the row never
        /// leaves the bar.</summary>
        [Fact]
        public void EveryNavLabelFitsItsButton_TheRowFitsTheNarrowestWindow_InEveryEraAndScale()
        {
            foreach (double scale in new[] { 1.0, 1.375, 2.0 })
            {
                using var gui = new HeadlessImGui(Assets(), 1920, 1080, uiScale: scale);
                foreach (bool developer in new[] { false, true })
                {
                    IReadOnlyList<Section> roster = GameSections.Roster(developer);
                    var widths = new float[roster.Count];
                    for (int i = 0; i < widths.Length; i++)
                        foreach (EraTheme t in EraThemes.All)
                        {
                            ImFontPtr body = gui.Fonts.For(t).Body;
                            widths[i] = Math.Max(widths[i], body.CalcTextSizeA(body.FontSize, float.MaxValue, 0f, GameSections.Label(roster[i])).X);
                        }
                    float s = (float)scale;
                    var bar = new PanelRect("##command", 0, 0, PanelLayout.MinWindowWidth * s, PanelLayout.Command.Height * s);
                    CommandRowLayout row = ChromeGeometry.CommandRow(bar, widths, 130f * s, s);
                    Assert.Equal(Fits.Yes, row.Fit);
                    for (int i = 0; i < widths.Length; i++)
                    {
                        float pad = (row.Compact ? ChromeGeometry.CompactNavPadX : ChromeGeometry.NavPadX) * s;
                        Assert.True(row.Nav[i].Width >= widths[i] + 2 * pad - 0.01f, $"{roster[i]} at s {scale}");
                    }
                    Assert.True(row.TerritoryX + 130f * s <= bar.Width - PanelLayout.Margin * s);
                }
            }
        }
    }

    [Fact]
    public void TheCommandRow_IsWideAtTheDesignWindow_AndTightensOnlyWhenItMustNeverClipping()
    {
        float[] labels = [110, 70, 64, 128, 74, 68, 40];   // the developer roster's labels, in px
        CommandRowLayout wide = ChromeGeometry.CommandRow(PanelLayout.Command, labels, 130f);
        Assert.False(wide.Compact);
        Assert.Equal(Fits.Yes, wide.Fit);
        Assert.Equal(ChromeGeometry.EndTurnWidth, wide.EndTurn.Width);
        Assert.Equal(ChromeGeometry.EndTurnHeight, wide.EndTurn.Height);
        for (int i = 0; i < labels.Length; i++)
        {
            Assert.Equal(Math.Max(ChromeGeometry.NavMinWidth, labels[i] + 2 * ChromeGeometry.NavPadX), wide.Nav[i].Width);
            Assert.True(wide.Nav[i].Height < wide.EndTurn.Height);                         // End Turn is the primary
            Assert.Equal(wide.EndTurn.CenterY, wide.Nav[i].CenterY, 3);                     // centred on the row
            if (i > 0) Assert.Equal(wide.Nav[i - 1].Right + ChromeGeometry.NavGap, wide.Nav[i].X);
        }
        Assert.Equal(wide.EndTurn.Right + ChromeGeometry.GroupGap, wide.Nav[0].X);
        // At 1080 px the same row tightens — every label still whole.
        var narrow = new PanelRect("##command", 0, 0, PanelLayout.MinWindowWidth, PanelLayout.Command.Height);
        CommandRowLayout tight = ChromeGeometry.CommandRow(narrow, labels, 130f);
        Assert.True(tight.Compact);
        Assert.Equal(Fits.Yes, tight.Fit);
        for (int i = 0; i < labels.Length; i++) Assert.True(tight.Nav[i].Width >= labels[i] + 2 * ChromeGeometry.CompactNavPadX);
    }

    // ------------------------------------------------------------------ UR-1: inks and fills

    /// <summary>The interface's share of the fibre overlay (FibreOverlay.InterfaceAmplitude) over the shipped
    /// asset: the factor every rendered ground is multiplied by on average.</summary>
    private static double InterfaceOverlayMean() => FibreOverlay.MeanFactor(FibreOverlay.Soft(AssetLibrary.Load(Assets()).Get("parchment/grain")));

    private static Rgba Darkened(Rgba c, double factor) =>
        new((byte)Math.Round(c.R * factor), (byte)Math.Round(c.G * factor), (byte)Math.Round(c.B * factor), 255);

    [Fact]
    public void TextInks_MeetTheSpecFloors_InEveryEra_OnTheTokensAndUnderTheOverlay()
    {
        double overlay = InterfaceOverlayMean();
        Assert.InRange(overlay, 0.94, 0.97);   // ≈ 0.952 (the full fibre's 0.864 at 35 % depth)
        foreach (EraTheme t in EraThemes.All)
        {
            Rgba panel = t.Material.Panel, rendered = Darkened(panel, overlay);
            TextInks ink = t.TextInk;
            foreach ((string name, Rgba c) in new[] { ("accent", ink.Accent), ("progress", ink.Progress), ("active", ink.Active),
                         ("positive", ink.Positive), ("danger", ink.Danger), ("knowledge", ink.Knowledge), ("military", ink.Military) })
            {
                Assert.True(ThemeColor.Contrast(c, panel) >= TextInks.Floor, $"{t.Era} {name} ink {ThemeColor.Contrast(c, panel):0.00}");
                Assert.True(ThemeColor.Contrast(c, rendered) >= 4.5, $"{t.Era} {name} ink rendered {ThemeColor.Contrast(c, rendered):0.00}");
                Assert.True(ThemeColor.Contrast(c, t.Material.PanelRaised) >= 4.5, $"{t.Era} {name} on raised");
            }
            // The text ink keeps its family's hue (it is the pigment darkened toward the ink, not a new colour).
            Assert.True(ThemeColor.HueDistance(ThemeColor.Hue(ink.Progress), ThemeColor.Hue(t.Semantic.Progress)) <= 12, $"{t.Era} progress hue");
            // Primary text holds 7:1 rendered; secondary and tertiary inks hold 4.5:1 rendered (TextDim ≥ 5.0 on tokens).
            Assert.True(ThemeColor.Contrast(t.Ink.Text, rendered) >= 7.0, $"{t.Era} text rendered");
            Assert.True(ThemeColor.Contrast(t.Ink.TextSoft, rendered) >= 4.5, $"{t.Era} soft rendered");
            Assert.True(ThemeColor.Contrast(t.Ink.TextDim, panel) >= EraThemes.TextDimFloor, $"{t.Era} dim {ThemeColor.Contrast(t.Ink.TextDim, panel):0.00}");
            Assert.True(ThemeColor.Contrast(t.Ink.TextDim, rendered) >= 4.5, $"{t.Era} dim rendered");
        }
    }

    [Fact]
    public void StateFills_AvailableAndLocked_AreSeparatedBySurface_InEveryEra()
    {
        foreach (EraTheme t in EraThemes.All)
        {
            double sep = ThemeColor.Contrast(t.Semantic.AvailableFill, t.Semantic.LockedFill);
            Assert.True(sep >= EraThemes.StateFillSeparation, $"{t.Era} available vs locked fill {sep:0.00}");
            Assert.True(ThemeColor.Lightness(t.Semantic.AvailableFill) > ThemeColor.Lightness(t.Semantic.LockedFill), $"{t.Era} locked recedes");
            // Words on either fill stay readable: the name ink on the available fill, the locked ink (TextSoft) at
            // the locked floor (≥ 3:1 with the lock mark as the non-colour cue).
            Assert.True(ThemeColor.Contrast(t.Ink.Text, t.Semantic.AvailableFill) >= 7.0, $"{t.Era} text on available");
            Assert.True(ThemeColor.Contrast(t.Ink.TextSoft, t.Semantic.LockedFill) >= 3.0, $"{t.Era} soft on locked");
            Assert.True(ThemeColor.Contrast(t.Ink.Text, t.Semantic.LockedFill) >= 6.0, $"{t.Era} text on locked");
        }
    }

    [Fact]
    public void TheImGuiStyle_SetsDisabledTextInTheFlooredDimInk_Opaque()
    {
        foreach (EraTheme t in EraThemes.All)
        {
            System.Numerics.Vector4 c = UiTheme.StyleFor(t).Color(ImGuiCol.TextDisabled);
            Assert.Equal(1f, c.W);
            Assert.Equal(t.Ink.TextDim.R / 255f, c.X);
        }
    }

    // ------------------------------------------------------------------ UR-2: the overlay split and calm panels

    [Fact]
    public void TheOverlaySplit_GivesTheMapTheFullFibre_AndTheInterfaceItsSoftShare()
    {
        for (int v = 0; v <= 255; v++)
        {
            byte b = (byte)v;
            byte soft = FibreOverlay.SoftByte(b), rest = FibreOverlay.RestByte(b);
            Assert.True(soft >= b, "the interface is never darker than the full fibre");
            Assert.InRange(rest * soft / 255.0, v - 1.0, v + 1.0);   // the map's two passes multiply back to the fibre
            Assert.Equal((byte)Math.Round(255.0 - FibreOverlay.InterfaceAmplitude * (255 - v), MidpointRounding.AwayFromZero), soft);
        }
        ArtImage fibre = AssetLibrary.Load(Assets()).Get("parchment/grain");
        Assert.True(FibreOverlay.MeanFactor(FibreOverlay.Soft(fibre)) > FibreOverlay.MeanFactor(fibre) + 0.05);
    }

    /// <summary>Every texture mark centred over a panel's or a card's content rect is faint (≤ CalmAlpha) and the
    /// marks there are at most a quarter of the band's density; the era's texture lives in the frame band.</summary>
    [Fact]
    public void PanelAndCardTextures_AreCalmOverTheContent_InEveryEra()
    {
        var r = new RectD(20, 30, 420, 640);
        RectD content = PanelFrame.ContentRect(r);
        foreach (EraTheme t in EraThemes.All)
            foreach (FrameKind kind in new[] { FrameKind.Panel, FrameKind.Card, FrameKind.Modal })
            {
                var d = new DrawList();
                PanelFrame.Paint(d, r, t, 17, kind);
                int inside = 0, band = 0;
                foreach (DrawCmd c in d.Commands)
                {
                    (double X, double Y)? at = c switch
                    {
                        CircleCmd ci when ci.R < 8 => (ci.Cx, ci.Cy),
                        LineCmd l when Math.Abs(l.X1 - l.X0) + Math.Abs(l.Y1 - l.Y0) < 30 => ((l.X0 + l.X1) / 2, (l.Y0 + l.Y1) / 2),
                        _ => null,
                    };
                    if (at is not (double x, double y)) continue;
                    bool over = content.Inset(1).Contains(x, y);
                    byte alpha = c switch { CircleCmd ci => (ci.Fill ?? ci.Stroke)!.Value.A, LineCmd l => l.Color.A, _ => 255 };
                    if (over)
                    {
                        inside++;
                        Assert.True(alpha <= Math.Round(255 * PanelFrame.CalmAlpha), $"{t.Era} {kind}: a mark of alpha {alpha} over the content at ({x:0},{y:0})");
                    }
                    else band++;
                }
                // Sparse: per unit area the content holds well under half the band's marks (one in CalmKeepEvery of
                // the material's density, against the band's full density).
                double contentArea = content.W * content.H, bandArea = r.W * r.H - contentArea;
                if (kind == FrameKind.Card)   // a card's texture is capped at 40 marks: a quarter of them, at most
                    Assert.True(inside <= 40 / PanelFrame.CalmKeepEvery + 2, $"{t.Era} card: {inside} marks over the content");
                else if (band > 0)
                    Assert.True(inside / contentArea <= 0.5 * band / bandArea,
                        $"{t.Era} {kind}: {inside} marks over the content, {band} in the band");
            }
    }

    // ------------------------------------------------------------------ UR-2: the research veil

    [Fact]
    public void ResearchFocus_RecedesUnrelatedCards_ButNeverDimsTheirWords()
    {
        UiSession s = fx.Session;
        var screen = new ProgressionScreen(s.Config.Research!, Me);
        screen.Refresh(s.World);
        DrawList plain = screen.Paint(1920, 1080, ApproxTextMeasure.Instance);
        var plainText = new Dictionary<(double, double, string), Rgba>();
        foreach (DrawCmd c in plain.Commands) if (c is TextCmd tc) plainText[(Math.Round(tc.X, 2), Math.Round(tc.Y, 2), tc.Text)] = tc.Color;

        screen.Selected = s.Config.Research!.IndexOf(ResearchQuery.CheapestAvailable(
            s.World, s.Config.Research, Me, Sim.Core.Systems.Research.ResearchTree.Technology)!.Value);
        DrawList focused = screen.Paint(1920, 1080, ApproxTextMeasure.Instance);
        EraTheme t = screen.Theme;
        int compared = 0;
        var texts = new List<TextCmd>();
        foreach (DrawCmd c in focused.Commands)
        {
            if (c is TextCmd tc)
            {
                texts.Add(tc);
                if (plainText.TryGetValue((Math.Round(tc.X, 2), Math.Round(tc.Y, 2), tc.Text), out Rgba before)) { Assert.Equal(before, tc.Color); compared++; }
            }
            // No veil: nothing in the field's colour is laid over words already set.
            if (c is RectCmd { Fill: Rgba f } rc && f.R == t.Material.Field.R && f.G == t.Material.Field.G && f.B == t.Material.Field.B && f.A < 255)
                foreach (TextCmd under in texts) Assert.False(rc.Rect.Contains(under.X + 1, under.Y + 1), "a veil over '" + under.Text + "'");
        }
        Assert.True(compared > 100, "the cards' words were compared (" + compared + ")");
    }

    // ------------------------------------------------------------------ UR-6: the action surface

    private (ActionSurfaceScreen Screen, DrawList List, ActionSurfaceModel Model) Surface(UiSession s, EraTheme? theme = null, (double, double)? pointer = null)
    {
        int cap = EmpireQuery.TryGetCapital(s.World, Me, out SettlementId c) ? c.Value : -1;
        EraTheme th = theme ?? EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me));
        ActionSurfaceModel model = ActionSurface.ForSession(s, UiSession.ProductionEra(), cap, th);
        var screen = new ActionSurfaceScreen { Theme = th };
        screen.Refresh(model, s.World, s.Config, Me, id => s.Names.Name(id));
        if (pointer is (double px, double py)) screen.PointerMove(px, py);
        var d = new DrawList();
        screen.Paint(d, ApproxTextMeasure.Instance, 0, 0, 452);
        return (screen, d, model);
    }

    [Fact]
    public void ABlockedBuild_LooksDisabled_AnAvailableOneIsPrimary_AndBothStillAnswerTheClick()
    {
        (ActionSurfaceScreen screen, DrawList d, ActionSurfaceModel model) = Surface(fx.Session);
        EraTheme t = screen.Theme;
        Assert.NotNull(model.Construction);
        int checkedBlocked = 0;
        foreach (ActionHit h in screen.Hits)
        {
            if (h.Kind != ActionHitKind.Build) continue;
            ProjectEntry p = model.Construction!.Projects.First(x => x.ProjectId == h.B);
            bool dashed = d.Commands.Any(c => c is LineCmd { Dash: not null } l && h.Rect.Inset(-1).Contains(l.X0, l.Y0));
            TextCmd label = d.Commands.OfType<TextCmd>().First(c => c.Text == "Build" && h.Rect.Contains(c.X, c.Y + 2));
            if (p.Blocker is not null)
            {
                Assert.True(dashed, p.Name + ": a blocked Build has the disabled plate's dashed edge");
                Assert.Equal(t.Ink.TextSoft, label.Color);
                checkedBlocked++;
            }
            else
            {
                Assert.False(dashed);
                Assert.Equal(t.Ink.Text, label.Color);
            }
            // ADR-033 D3: materials are affordability, not legality — the click still asks to queue it.
            Assert.Equal(ActionCommandKind.Enqueue, screen.Click(h.Rect.CenterX, h.Rect.CenterY).Kind);
        }
        Assert.True(checkedBlocked >= 1, "turn 1 has blocked projects");
    }

    [Fact]
    public void Blockers_AreSetInTheProgressTextInk_WithTheMissingQuantityEmphasised()
    {
        (ActionSurfaceScreen screen, DrawList d, ActionSurfaceModel model) = Surface(fx.Session);
        EraTheme t = screen.Theme;
        ProjectEntry blocked = model.Construction!.Projects.First(p => p.Blocker is not null);
        Assert.Contains("(has ", blocked.Blocker!, StringComparison.Ordinal);
        List<TextCmd> texts = d.Commands.OfType<TextCmd>().ToList();
        // "not yet:" in the progress family's text ink (≥ 5.5:1), never the pigment (1.88:1 rendered before).
        TextCmd lead = texts.First(c => c.Text == "not");
        Assert.Equal(t.TextInk.Progress, lead.Color);
        // The missing quantity is set bold in the body ink.
        string qty = blocked.Blocker!.Split(' ')[1];   // "needs 40 timber (has 0)…" → "40"
        Assert.Contains(texts, c => c.Text == qty && c.Role == FontRole.Heading && c.Color == t.Ink.Text);
        // Nothing on the surface is set below the caption floor.
        foreach (TextCmd c in texts)
            Assert.True(c.Size >= TypeScale.Floor(c.Style?.Face ?? TypeFace.Garamond) - 0.01, $"'{c.Text}' at {c.Size} px");
    }

    [Fact]
    public void TheBlocks_AreInDecisionOrder_TaxBeforeBuilding()
    {
        UiSession s = UiSession.StartFrom(TaxReady(fx.Session), fx.Session.World.Seed);
        (ActionSurfaceScreen screen, DrawList d, ActionSurfaceModel model) = Surface(s);
        Assert.NotNull(model.Governance);
        Assert.NotNull(model.Construction);
        double Y(ActionHitKind kind) => screen.Hits.First(h => h.Kind == kind).Rect.Y;
        Assert.True(Y(ActionHitKind.LabourSlot) < Y(ActionHitKind.ResearchOpen));
        Assert.True(Y(ActionHitKind.ResearchOpen) < Y(ActionHitKind.TaxSlot));
        Assert.True(Y(ActionHitKind.TaxSlot) < Y(ActionHitKind.Build), "the tax edict comes before building");
        _ = d;
    }

    private static WorldState TaxReady(UiSession from)
    {
        WorldState w = from.World.Clone();
        Sim.Core.Systems.Research.ResearchContent research = from.Config.Research!;
        w.ResearchCompleted.Add(new ResearchCompletedRow(Me, research.Nodes[research.IndexOfId("taxation")].Key));
        TaxAgeRig.EnterTaxAge(w, from.Config, Me);
        return w;
    }

    [Fact]
    public void DrawListControls_AnswerHover()
    {
        (ActionSurfaceScreen screen, DrawList calm, _) = Surface(fx.Session);
        ActionHit open = screen.Hits.First(h => h.Kind == ActionHitKind.ResearchOpen);
        (_, DrawList hovered, _) = Surface(fx.Session, pointer: (open.Rect.CenterX, open.Rect.CenterY));
        static string Svg(DrawList d) => SvgWriter.Write(d, 460, 1600);
        Assert.NotEqual(Svg(calm), Svg(hovered));
        // A pebble slot under the pointer is ringed.
        ActionHit slot = screen.Hits.First(h => h.Kind == ActionHitKind.LabourSlot);
        (_, DrawList onSlot, _) = Surface(fx.Session, pointer: (slot.Rect.CenterX, slot.Rect.CenterY));
        Assert.True(onSlot.Commands.Count > calm.Commands.Count);
    }

    // ------------------------------------------------------------------ UR-3: player views carry figures at A1

    [Fact]
    public void PlayerViews_ShowTheirFigures_AtTheFirstEra_InAValueColumn()
    {
        UiSession s = fx.Session;
        int cap = EmpireQuery.TryGetCapital(s.World, Me, out SettlementId c) ? c.Value : -1;
        PlayerView place = PlayerViews.Settlement(s.World, s.Config, Me, null, cap, s.Names.Name, density: 1);
        Assert.Equal(s.Names.Name(cap), place.Title);   // the subject is the title
        Assert.Equal("yours", place.Subtitle);
        ViewLine people = place.Blocks[0].Rows[0];
        Assert.Equal("People live here", people.Text);
        Assert.EndsWith(" people", people.Figure, StringComparison.Ordinal);
        PlayerView empire = PlayerViews.Empire(s.World, s.Config, Me, s.Names.Name, density: 1);
        ViewLine grain = empire.Blocks[1].Rows[0];
        Assert.Equal("Grain in your granaries", grain.Text);
        Assert.NotEqual("", grain.Figure);
    }
}
