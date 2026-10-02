using System.Globalization;
using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Ages;
using Sim.Ui.Art;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Tests;

/// <summary>A real seed-42 founding (256 px, 4 settlements) through the real session: the world the
/// era derivation and the save/load round trip read. Turn 0, founding Age (I).</summary>
public sealed class FoundedSessionFixture
{
    public UiSession Session { get; } = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
}

/// <summary>
/// ADR-033 D8 — THE AGE-DERIVED VISUAL LANGUAGE, at the token level: the one derivation from the
/// authoritative Age, the nine eras' ordinal character (A1 primitive … A9 modern), continuity (the
/// semantic hue families, the era-invariant ImGui geometry, frames that never leave their rect),
/// determinism, the cross-fade, save/load, and the absence of any write to the simulation.
/// </summary>
public class EraThemeTests(FoundedSessionFixture fx) : IClassFixture<FoundedSessionFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private AgeContent Ages => fx.Session.Config.Ages!;

    /// <summary>A copy of <paramref name="w"/> whose ONLY difference is the player's Age row.</summary>
    internal static WorldState AtAge(WorldState w, PolityId polity, int age, int surge)
    {
        WorldState c = w.Clone();
        var row = new AgeStateRow(polity, age, c.Clock.Turn, surge, c.Clock.Turn);
        for (int i = 0; i < c.AgeStates.Count; i++)
            if (c.AgeStates[i].Polity.Value == polity.Value) { c.AgeStates[i] = row; return c; }
        c.AgeStates.Add(row);
        return c;
    }

    private static EraTheme T(UiEra e) => EraThemes.For(e);

    // ------------------------------------------------------------------ the derivation

    [Fact]
    public void Era_IsDerivedFromTheAuthoritativeAge_ByOnePureFunction()
    {
        WorldState w = fx.Session.World;
        Assert.Equal(1, AgeQuery.CurrentAge(w, Ages, Me));
        Assert.Equal(UiEra.Prehistoric, UiEras.Of(w, Ages, Me));
        for (int age = 1; age <= 9; age++)
        {
            WorldState a = AtAge(w, Me, age, Ages.Surges[0].Key);
            Assert.Equal(age, AgeQuery.CurrentAge(a, Ages, Me));
            Assert.Equal(UiEras.FromAge(AgeQuery.CurrentAge(a, Ages, Me)), UiEras.Of(a, Ages, Me));
            Assert.Equal(age, UiEras.Of(a, Ages, Me).Ordinal());
        }
        // No Age content: no Age state, the first era. Out-of-range keys clamp.
        Assert.Equal(UiEra.Prehistoric, UiEras.Of(w, null, Me));
        Assert.Equal(UiEra.Prehistoric, UiEras.FromAge(0));
        Assert.Equal(UiEra.Modern, UiEras.FromAge(12));
        Assert.Equal(9, UiEras.All.Count);
        for (int i = 0; i < 9; i++) Assert.Equal(i + 1, UiEras.All[i].Ordinal());
    }

    [Fact]
    public void ChangingTheAge_ChangesTheDerivedPresentation()
    {
        WorldState w = fx.Session.World;
        string previous = "";
        for (int age = 1; age <= 9; age++)
        {
            EraTheme theme = EraThemes.For(UiEras.Of(AtAge(w, Me, age, Ages.Surges[0].Key), Ages, Me));
            Assert.Equal(age, theme.Ordinal);
            string canon = theme.Canonical();
            Assert.NotEqual(previous, canon);   // every Age step changes the presentation
            previous = canon;
        }
        // …and the drawn frame changes with it.
        string Svg(UiEra e)
        {
            var d = new DrawList();
            PanelFrame.Paint(d, new RectD(10, 10, 300, 160), T(e), 7);
            return SvgWriter.Write(d, 320, 180);
        }
        Assert.NotEqual(Svg(UiEra.Prehistoric), Svg(UiEra.Neolithic));
        Assert.NotEqual(Svg(UiEra.Industrial), Svg(UiEra.Modern));
    }

    // ------------------------------------------------------------------ the nine eras' character

    [Fact]
    public void A1_IsThePrimitivePresentation()
    {
        EraTheme a1 = T(UiEra.Prehistoric);
        Assert.Equal(MaterialKind.Stone, a1.Material.Kind);
        Assert.Equal(1.0, a1.Edge.Roughness);
        Assert.True(a1.Edge.JitterPx >= 2.5);
        Assert.Equal(CornerStyle.Organic, a1.Edge.Corner);
        Assert.Equal(0, a1.Ornament.Level);              // minimal ornamentation
        Assert.Equal(1, a1.Density.Level);               // less information density
        Assert.Equal(1, a1.Density.CardDetail);          // primitive node cards
        Assert.Equal(ControlGranularity.Coarse, a1.Controls.Granularity);
        Assert.Equal(ProgressStyle.Notches, a1.Controls.Progress);   // simple progress notches
        Assert.Equal(IconStyle.Daubed, a1.Icons.Style);  // hand-drawn marks
        Assert.True(a1.Icons.WobblePx > 0);
        Assert.Equal(0, a1.Charts.Sophistication);
        foreach (EraTheme t in EraThemes.All)            // the largest type and the loosest leading
        {
            Assert.True(a1.Type.SizeScale >= t.Type.SizeScale);
            Assert.True(a1.Type.LineHeight >= t.Type.LineHeight);
            Assert.True(a1.Edge.Roughness >= t.Edge.Roughness);
        }
        Assert.Equal(TypeFace.Garamond, a1.Type.Heading.Face);
        Assert.True(a1.Type.Heading.Bold);                // heavy, limited hierarchy

        // The painted frame is a hand-cut slab, not a rectangle: a non-rectilinear fill and a
        // freehand outline.
        var d = new DrawList();
        PanelFrame.Paint(d, new RectD(20, 20, 300, 180), a1, 3);
        PolygonCmd slab = Assert.IsType<PolygonCmd>(d.Commands[0]);
        Assert.True(slab.Points.Length > 8);
        Assert.Contains(slab.Points, p => p.X != 20 + a1.Edge.JitterPx && p.X != 320 - a1.Edge.JitterPx && p.Y != 20 + a1.Edge.JitterPx && p.Y != 200 - a1.Edge.JitterPx);
        Assert.Contains(d.Commands, c => c is PolylineCmd { Closed: true });
        Assert.DoesNotContain(d.Commands, c => c is RectCmd);
    }

    [Fact]
    public void A2_DiffersAppropriatelyFromA1()
    {
        EraTheme a1 = T(UiEra.Prehistoric), a2 = T(UiEra.Neolithic);
        Assert.Equal(MaterialKind.Clay, a2.Material.Kind);   // clay and woven reed
        Assert.Equal(Motif.Weave, a2.Ornament.Motif);
        Assert.True(a2.Edge.Roughness < a1.Edge.Roughness);   // more structured
        Assert.True(a2.Edge.Roughness > 0);                   // …but still hand-made
        Assert.Equal(CornerStyle.Rounded, a2.Edge.Corner);
        Assert.True(a2.Ornament.Level > a1.Ornament.Level);
        Assert.True(a2.Density.Level > a1.Density.Level);     // information density can increase
        Assert.True(a2.Density.CardDetail > a1.Density.CardDetail);   // cleaner, fuller nodes
        Assert.True(a2.Controls.Granularity > a1.Controls.Granularity);
        Assert.True(a2.Charts.Sophistication > a1.Charts.Sophistication);
        Assert.Equal(IconStyle.Incised, a2.Icons.Style);
        Assert.True(a2.Icons.WobblePx < a1.Icons.WobblePx);
        // The woven band is painted on a panel.
        var d = new DrawList();
        PanelFrame.Paint(d, new RectD(0, 0, 320, 200), a2, 5);
        int weave = 0;
        foreach (DrawCmd c in d.Commands)
            if (c is LineCmd l && l.Y0 < 20 && Math.Abs(l.X1 - l.X0) > 1 && Math.Abs(l.Y1 - l.Y0) > 1) weave++;
        Assert.True(weave >= 20, $"weave strokes {weave}");
    }

    [Fact]
    public void A5_IsMeasurablyMoreAdvancedThanA2()
    {
        EraTheme a2 = T(UiEra.Neolithic), a5 = T(UiEra.Classical);
        Assert.True(a5.Edge.Roughness < a2.Edge.Roughness);        // roughness ↓
        Assert.Equal(0.0, a5.Edge.Roughness);
        Assert.True(a5.Density.Level > a2.Density.Level);          // density ↑
        Assert.True(a5.Density.CardDetail > a2.Density.CardDetail);
        Assert.True(a5.Controls.Granularity > a2.Controls.Granularity);
        Assert.True(a5.Charts.Sophistication > a2.Charts.Sophistication);
        Assert.True(a5.Ornament.Level > a2.Ornament.Level);         // architectural ornament
        Assert.Equal(Motif.Meander, a5.Ornament.Motif);
        Assert.True(a5.Edge.DoubleRule);                            // structured hierarchy
        Assert.Equal(TextCase.Upper, a5.Type.Title.Case);           // inscriptional capitals
        Assert.True(a5.Type.Title.TrackingEm > a2.Type.Title.TrackingEm);
        Assert.True(a5.Type.LineHeight < a2.Type.LineHeight);
        Assert.Equal(MaterialKind.Marble, a5.Material.Kind);
        // A3–A5 rise monotonically on every sophistication axis.
        for (int e = 2; e < 5; e++)
        {
            EraTheme lo = T((UiEra)e), hi = T((UiEra)(e + 1));
            Assert.True(hi.Edge.Roughness <= lo.Edge.Roughness);
            Assert.True(hi.Density.Level >= lo.Density.Level);
            Assert.True(hi.Charts.Sophistication >= lo.Charts.Sophistication);
            Assert.True(hi.Controls.Granularity >= lo.Controls.Granularity);
        }
    }

    [Fact]
    public void A8_IndustrialPresentationExists()
    {
        EraTheme a8 = T(UiEra.Industrial);
        Assert.Equal(MaterialKind.Drafting, a8.Material.Kind);       // engineering drawings
        Assert.Equal(Motif.Dimension, a8.Ornament.Motif);            // dimension lines
        Assert.True(a8.Edge.Fasteners);                              // bolts
        Assert.Equal(TypeFace.PlexSans, a8.Type.Heading.Face);       // technical lettering
        Assert.Equal(TypeFace.PlexSans, a8.Type.Numeric.Face);
        Assert.True(a8.Type.Caps.TrackingEm >= 0.1);
        Assert.Equal(ProgressStyle.GraduatedBar, a8.Controls.Progress);
        Assert.True(a8.Charts.GridLines && a8.Charts.Ticks && a8.Charts.Labels);
        Assert.Equal(5, a8.Density.Level);                           // denser information
        // The ground is a drafting grid.
        var d = new DrawList();
        PanelFrame.Field(d, new RectD(0, 0, 400, 300), a8, 1);
        Assert.True(d.Commands.Count(c => c is LineCmd) >= 30);
    }

    [Fact]
    public void A9_ModernPresentationExists_AndIsStillThisCivilizationsInterface()
    {
        EraTheme a9 = T(UiEra.Modern);
        Assert.Equal(MaterialKind.Glass, a9.Material.Kind);
        Assert.Equal(0.0, a9.Material.GrainDensity);                 // clean surfaces
        Assert.Equal(0.0, a9.Edge.Roughness);
        Assert.Equal(CornerStyle.Fine, a9.Edge.Corner);              // precise geometry
        Assert.Equal(0, a9.Ornament.Level);
        Assert.Equal(ControlGranularity.Precise, a9.Controls.Granularity);   // compact controls
        Assert.Equal(4, a9.Charts.Sophistication);                   // sophisticated charts
        foreach (EraTheme t in EraThemes.All)
        {
            Assert.True(a9.Density.Level >= t.Density.Level);         // high density
            Assert.True(a9.Type.SizeScale <= t.Type.SizeScale);
        }
        foreach (FontRole r in new[] { FontRole.Body, FontRole.Heading, FontRole.Title, FontRole.Numeric, FontRole.Caps })
            Assert.Equal(TypeFace.PlexSans, a9.Type.For(r).Face);    // clean typography
        // Not a generic dashboard: the warm metallic accent that has run through every Age (ochre,
        // terracotta, bronze, ember, gold, gold leaf … brass), never a stock blue.
        foreach (EraTheme t in new[] { T(UiEra.Prehistoric), T(UiEra.Bronze), T(UiEra.Classical), T(UiEra.Industrial), a9 })
            Assert.InRange(ThemeColor.Hue(t.Material.Accent), 10, 45);
        Assert.True(ThemeColor.Hue(a9.Material.Field) < 90 || ThemeColor.Saturation(a9.Material.Field) < 0.05);   // warm neutral
    }

    [Fact]
    public void ParchmentEra_IsTheStyleBiblePalette()
    {
        EraTheme a6 = T(UiEra.Medieval);
        Assert.Equal(ParchmentPalette.PaperMid, a6.Material.Field);
        Assert.Equal(ParchmentPalette.PaperLight, a6.Material.Panel);
        Assert.Equal(ParchmentPalette.PaperShade, a6.Material.PanelSunken);
        Assert.Equal(ParchmentPalette.InkPrimary, a6.Ink.Text);
        Assert.Equal(ParchmentPalette.InkSoft, a6.Ink.TextSoft);
        Assert.Equal(ParchmentPalette.IronRed, a6.Material.Accent);
        Assert.Equal(ParchmentPalette.GoldLeaf, a6.Material.AccentSoft);
        Assert.Equal(ParchmentPalette.GoldLeaf, a6.Semantic.Completed);
        Assert.Equal(ParchmentPalette.IronRed, a6.Semantic.Danger);
        Assert.Equal(ParchmentPalette.Verdigris, a6.Semantic.Positive);
        // The map's ink (derived from the theme: MapInk.For, the one map-ink record) is today's WorldLens
        // literals in the parchment era — the whole record, not a subset.
        Assert.Equal(Sim.Ui.World.MapInk.Default, Sim.Ui.World.MapInk.For(a6));
        Assert.Equal(Rgba.Hex(0x3A2E1F), Sim.Ui.World.MapInk.For(a6).Ink);
        Assert.Equal(Rgba.Hex(0x1E1810), Sim.Ui.World.MapInk.For(a6).GlyphInk);
    }

    // ------------------------------------------------------------------ continuity

    [Fact]
    public void Continuity_SemanticColoursStayInTheirHueFamily_InEveryEra()
    {
        foreach (EraTheme t in EraThemes.All)
            foreach ((SemanticFamily f, Func<SemanticTokens, Rgba> token) in SemanticFamilies.All)
            {
                Rgba c = token(t.Semantic);
                if (f.Neutral) Assert.True(ThemeColor.Saturation(c) <= SemanticFamilies.NeutralMaxSaturation, $"{t.Era} {f.Name} saturation {ThemeColor.Saturation(c)}");
                else Assert.True(f.Contains(ThemeColor.Hue(c)), $"{t.Era} {f.Name} hue {ThemeColor.Hue(c):0.0} outside [{f.Lo},{f.Hi}]");
            }
        // The state colours a card is judged by stay distinguishable from each other in every era.
        foreach (EraTheme t in EraThemes.All)
        {
            Rgba[] states = [t.Semantic.Available, t.Semantic.Active, t.Semantic.Completed, t.Semantic.Locked, t.Semantic.Danger];
            for (int i = 0; i < states.Length; i++)
                for (int j = i + 1; j < states.Length; j++)
                    Assert.True(Distance(states[i], states[j]) > 40, $"{t.Era}: states {i} and {j} too close");
        }
        // Lane hues keep their family too (each lane reads as one colour from A1 to A9).
        string[] lanes = ["main", "military", "medicine", "engineering", "natural_science", "agriculture", "civics"];
        foreach (string lane in lanes)
        {
            double h0 = ThemeColor.Hue(T(UiEra.Prehistoric).Semantic.Lanes.Of(lane));
            foreach (EraTheme t in EraThemes.All)
                Assert.True(ThemeColor.HueDistance(h0, ThemeColor.Hue(t.Semantic.Lanes.Of(lane))) <= 8, $"{lane} {t.Era}");
        }
    }

    private static double Distance(Rgba a, Rgba b) =>
        Math.Sqrt((a.R - b.R) * (a.R - b.R) + (a.G - b.G) * (a.G - b.G) + (a.B - b.B) * (a.B - b.B));

    [Fact]
    public void Continuity_OneGroundPolarity_AndLegibleInkInEveryEra()
    {
        // No Age flips the interface from dark to light: in every era the content is a light record
        // surface with dark ink, and the bars are the era's darker frame material with light ink.
        foreach (EraTheme t in EraThemes.All)
        {
            MaterialTokens m = t.Material;
            Assert.True(ThemeColor.Lightness(m.Field) > 0.55, $"{t.Era} field");
            Assert.True(ThemeColor.Lightness(m.Panel) > 0.7, $"{t.Era} panel");
            Assert.True(ThemeColor.Lightness(m.Chrome) < 0.35, $"{t.Era} chrome: the frame material is the darkest surface");
            Assert.True(ThemeColor.Contrast(t.Ink.Text, m.Panel) >= 7.0, $"{t.Era} text/panel");
            Assert.True(ThemeColor.Contrast(t.Ink.Text, m.Field) >= 4.5, $"{t.Era} text/field");
            Assert.True(ThemeColor.Contrast(t.Ink.TextSoft, m.Panel) >= 4.5, $"{t.Era} soft/panel");
            Assert.True(ThemeColor.Contrast(t.Ink.TextDim, m.Panel) >= 3.0, $"{t.Era} dim/panel");
            Assert.True(ThemeColor.Contrast(t.Ink.OnChrome, m.Chrome) >= 7.0, $"{t.Era} on-chrome");
            Assert.True(ThemeColor.Contrast(t.Ink.OnChromeSoft, m.Chrome) >= 4.5, $"{t.Era} on-chrome soft");
            Assert.True(ThemeColor.Contrast(t.Ink.OnChromeAccent, m.Chrome) >= 4.5, $"{t.Era} on-chrome accent");
        }
        // Adjacent eras evolve rather than switch: the ground's lightness moves by small steps.
        for (int e = 1; e < 9; e++)
            Assert.True(Math.Abs(ThemeColor.Lightness(T((UiEra)e).Material.Field) - ThemeColor.Lightness(T((UiEra)(e + 1)).Material.Field)) < 0.12, $"A{e}->A{e + 1}");
    }

    [Fact]
    public void Continuity_TheImGuiGeometry_IsEraInvariant()
    {
        UiTheme.StyleSpec first = UiTheme.StyleFor(T(UiEra.Prehistoric));
        foreach (EraTheme t in EraThemes.All)
        {
            UiTheme.StyleSpec s = UiTheme.StyleFor(t);
            // The chrome geometry (ChromeGeometry, PanelLayout) is computed from these: identical.
            Assert.Equal(UiTheme.WindowPaddingPx, s.WindowPadding);
            Assert.Equal(UiTheme.FramePaddingPx.Y, s.FramePadding.Y);
            Assert.Equal(first.Colors.Count, s.Colors.Count);
            // The style's colours ARE the era's tokens.
            Assert.Equal(Vec(t.Ink.Text, 1.0), s.Color(ImGuiNET.ImGuiCol.Text));
            Assert.Equal(Vec(t.Material.Panel, 0.0), s.Color(ImGuiNET.ImGuiCol.WindowBg));   // the era frame is the surface
            Assert.Equal(Vec(t.Material.PanelRaised, 0.98), s.Color(ImGuiNET.ImGuiCol.PopupBg));
            // A pressed control stays light enough for the dark ink in every era.
            Assert.True(ThemeColor.Contrast(t.Ink.Text, ThemeColor.Mix(t.Material.PanelRaised, t.Material.Accent, 0.34)) >= 4.5, $"{t.Era} pressed");
            Assert.Equal(Vec(t.Material.Border, 0.75), s.Color(ImGuiNET.ImGuiCol.Border));
            Assert.Equal((float)t.Controls.GrabPx, s.GrabMinSize);
        }
        Assert.Equal(29f, UiTheme.FrameHeightPx);
        // Density shows in the spacing: the modern era is tighter than the primitive one.
        Assert.True(UiTheme.StyleFor(T(UiEra.Modern)).ItemSpacing.Y < first.ItemSpacing.Y);
        Assert.True(UiTheme.StyleFor(T(UiEra.Modern)).FramePadding.X < first.FramePadding.X);
    }

    private static System.Numerics.Vector4 Vec(Rgba c, double a) => new(c.R / 255f, c.G / 255f, c.B / 255f, (float)a);

    [Fact]
    public void Frames_NeverLeaveTheirRect_AreConvex_AndAreSeededByStableIds()
    {
        var r = new RectD(40, 30, 260, 140);
        foreach (EraTheme t in EraThemes.All)
            foreach (FrameKind k in Enum.GetValues<FrameKind>())
            {
                (double X, double Y)[] o = PanelFrame.Outline(r, t, 11, k);
                foreach ((double x, double y) in o)
                {
                    Assert.InRange(x, r.X - 1e-9, r.Right + 1e-9);
                    Assert.InRange(y, r.Y - 1e-9, r.Bottom + 1e-9);
                }
                Assert.True(Convex(o), $"{t.Era} {k} outline not convex");
                var d = new DrawList();
                PanelFrame.Paint(d, r, t, 11, k);
                foreach (DrawCmd c in d.Commands) AssertInside(c, r, 0.75);
                // Same id → byte-identical frame.
                var d2 = new DrawList();
                PanelFrame.Paint(d2, r, t, 11, k);
                Assert.Equal(SvgWriter.Write(d, 400, 300), SvgWriter.Write(d2, 400, 300));
            }
        // The primitive slab differs by id; the modern frame does not depend on it.
        string Frame(EraTheme t, int id) { var d = new DrawList(); PanelFrame.Paint(d, r, t, id, FrameKind.Card); return SvgWriter.Write(d, 400, 300); }
        Assert.NotEqual(Frame(T(UiEra.Prehistoric), 1), Frame(T(UiEra.Prehistoric), 2));
        Assert.Equal(Frame(T(UiEra.Modern), 1), Frame(T(UiEra.Modern), 2));
    }

    private static bool Convex((double X, double Y)[] p)
    {
        int sign = 0;
        for (int i = 0; i < p.Length; i++)
        {
            (double X, double Y) a = p[i], b = p[(i + 1) % p.Length], c = p[(i + 2) % p.Length];
            double cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (Math.Abs(cross) < 1e-9) continue;
            int s = Math.Sign(cross);
            if (sign == 0) sign = s;
            else if (s != sign) return false;
        }
        return true;
    }

    /// <summary>Every command lies inside the rect, allowing half a stroke of slack.</summary>
    private static void AssertInside(DrawCmd c, RectD r, double slack)
    {
        void P(double x, double y)
        {
            Assert.InRange(x, r.X - slack - 1.6, r.Right + slack + 1.6);
            Assert.InRange(y, r.Y - slack - 1.6, r.Bottom + slack + 1.6);
        }
        switch (c)
        {
            case RectCmd rc: P(rc.Rect.X, rc.Rect.Y); P(rc.Rect.Right, rc.Rect.Bottom); break;
            case LineCmd l: P(l.X0, l.Y0); P(l.X1, l.Y1); break;
            case PolygonCmd pg: foreach ((double x, double y) in pg.Points) P(x, y); break;
            case PolylineCmd pl: foreach ((double x, double y) in pl.Points) P(x, y); break;
            case CircleCmd ci: P(ci.Cx - ci.R, ci.Cy - ci.R); P(ci.Cx + ci.R, ci.Cy + ci.R); break;
            case BezierCmd b: P(b.P0.X, b.P0.Y); P(b.P3.X, b.P3.Y); break;
        }
    }

    // ------------------------------------------------------------------ determinism

    [Fact]
    public void Determinism_TwoDerivationsAreByteIdentical()
    {
        foreach (UiEra e in UiEras.All)
        {
            EraTheme a = EraThemes.Build(e), b = EraThemes.Build(e);
            Assert.NotSame(a, b);
            Assert.Equal(a.Canonical(), b.Canonical());
            Assert.Equal(ThemeCanon.Hash(a), ThemeCanon.Hash(b));
            Assert.Equal(a.Canonical(), EraThemes.For(e).Canonical());   // the memo is the build
            Assert.Same(EraThemes.For(e), EraThemes.For(e));
        }
        // The canonical text covers every token group. (The map's ink is not a theme token group: it is the
        // world layer's one MapInk record, derived from the theme by MapInk.For — MapInkEraTests.)
        string c = EraThemes.For(UiEra.Bronze).Canonical();
        foreach (string group in new[] { "Material.Field=", "Ink.Text=", "Semantic.Completed=", "Semantic.Lanes.Civics=", "Edge.Roughness=",
            "Ornament.Motif=", "Type.Caps=", "Density.Level=", "Controls.Progress=", "Icons.Style=", "Charts.Sophistication=" })
            Assert.Contains(group, c, StringComparison.Ordinal);
        Assert.DoesNotContain("Map.", c, StringComparison.Ordinal);
        // Nine distinct presentations.
        var hashes = new HashSet<string>();
        foreach (EraTheme t in EraThemes.All) hashes.Add(ThemeCanon.Hash(t));
        Assert.Equal(9, hashes.Count);
    }

    // ------------------------------------------------------------------ the transition cross-fade

    [Fact]
    public void CrossFade_StartsAtTheOldEra_EndsAtTheNewOne_AndSwitchesDiscreteTokensAtTheMidpoint()
    {
        EraTheme a = T(UiEra.Prehistoric), b = T(UiEra.Neolithic);
        Assert.Equal(a.Canonical(), ThemeLerp.Between(a, b, 0).Canonical());
        Assert.Equal(b.Canonical(), ThemeLerp.Between(a, b, 1).Canonical());
        EraTheme early = ThemeLerp.Between(a, b, 0.25), late = ThemeLerp.Between(a, b, 0.75);
        Assert.Equal(MaterialKind.Stone, early.Material.Kind);
        Assert.Equal(MaterialKind.Clay, late.Material.Kind);
        Assert.InRange(early.Edge.Roughness, b.Edge.Roughness, a.Edge.Roughness);
        // Colours move monotonically from one era's to the other's.
        double d0 = Distance(a.Material.Field, b.Material.Field);
        Assert.True(Distance(early.Material.Field, b.Material.Field) < d0);
        Assert.True(Distance(late.Material.Field, b.Material.Field) < Distance(early.Material.Field, b.Material.Field));

        var fade = new EraTransition(a, b);
        Assert.Equal(a.Canonical(), fade.Current.Canonical());
        fade.Advance(EraTransition.Seconds / 2);
        Assert.False(fade.Done);
        fade.Advance(EraTransition.Seconds);
        Assert.True(fade.Done);
        Assert.Same(b, fade.Current);   // the end state is exactly the new era (what headless renders)
    }

    // ------------------------------------------------------------------ save/load and read-only

    [Fact]
    public void SaveLoad_PreservesTheAge_AndThereforeTheIdenticalTheme()
    {
        WorldState w = AtAge(fx.Session.World, Me, 5, Ages.Surges[^1].Key);   // a non-founding Age
        Assert.Equal(UiEra.Classical, UiEras.Of(w, Ages, Me));
        var stream = new MemoryStream();
        Snapshot.Save(w, stream);
        stream.Position = 0;
        WorldState loaded = Snapshot.Load(stream, w.Terrain);
        Assert.Equal(WorldHash.ComputeHex(w), WorldHash.ComputeHex(loaded));
        Assert.Equal(5, AgeQuery.CurrentAge(loaded, Ages, Me));
        UiEra era = UiEras.Of(loaded, Ages, Me);
        Assert.Equal(UiEras.Of(w, Ages, Me), era);
        Assert.Equal(EraThemes.Build(UiEras.Of(w, Ages, Me)).Canonical(), EraThemes.Build(era).Canonical());
        Assert.Equal(UiTheme.StyleFor(EraThemes.For(UiEras.Of(w, Ages, Me))), UiTheme.StyleFor(EraThemes.For(era)), new StyleComparer());
    }

    private sealed class StyleComparer : IEqualityComparer<UiTheme.StyleSpec>
    {
        public bool Equals(UiTheme.StyleSpec? x, UiTheme.StyleSpec? y) =>
            x is not null && y is not null && (x with { Colors = [] }) == (y with { Colors = [] }) && x.Colors.SequenceEqual(y.Colors);
        public int GetHashCode(UiTheme.StyleSpec obj) => 0;
    }

    [Fact]
    public void DerivingAndApplyingTheTheme_NeverMutatesSimulationState()
    {
        WorldState w = AtAge(fx.Session.World, Me, 3, Ages.Surges[0].Key);
        string hash = WorldHash.ComputeHex(w);
        string live = WorldHash.ComputeHex(fx.Session.World);
        for (int k = 0; k < 3; k++)
        {
            EraTheme t = EraThemes.For(UiEras.Of(w, Ages, Me));
            UiTheme.StyleFor(t);
            var d = new DrawList();
            PanelFrame.Field(d, new RectD(0, 0, 800, 500), t, 1);
            foreach (FrameKind kind in Enum.GetValues<FrameKind>()) PanelFrame.Paint(d, new RectD(10, 10, 300, 200), t, 2, kind);
            var fade = new EraTransition(EraThemes.For(UiEra.Prehistoric), t);
            fade.Advance(0.5);
            _ = fade.Current.Canonical();
            SvgWriter.Write(d, 800, 500);
        }
        Assert.Equal(hash, WorldHash.ComputeHex(w));
        Assert.Equal(live, WorldHash.ComputeHex(fx.Session.World));
        // The era is not stored anywhere in the world: changing it is impossible without an Age row.
        Assert.Equal(UiEra.Bronze, UiEras.Of(w, Ages, Me));
        Assert.Equal(UiEra.Prehistoric, UiEras.Of(fx.Session.World, Ages, Me));
    }

    // ------------------------------------------------------------------ the text boundary

    [Fact]
    public void TextBoundary_UsesDrawListsSubstitutionTable()
    {
        // Every single-character substitution is a glyph remap; every multi-character one a real glyph.
        foreach ((char from, string to) in DrawList.Substitutions)
        {
            if (to.Length == 1) Assert.Contains((from, to[0]), UiText.Remaps);
            else Assert.Contains(from, UiText.RealGlyphs);
        }
        Assert.Contains(('—', '-'), UiText.Remaps);   // the em dash renders as DrawList's hyphen
        ushort[] ranges = UiText.GlyphRanges();
        Assert.Equal(0, ranges[^1]);
        Assert.Equal((ushort)0x20, ranges[0]);
        Assert.Equal((ushort)0xFF, ranges[1]);
        foreach (char c in UiText.RealGlyphs) Assert.Contains((ushort)c, ranges);
        // Remapped code points are NOT rasterised (their stand-in's glyph is used).
        foreach ((char from, char _) in UiText.Remaps) Assert.DoesNotContain((ushort)from, ranges);
        Assert.Equal("a - b ... c -> d", DrawList.Latin1("a — b … c → d"));
    }

    [Fact]
    public void TextStyle_SetsCaseWithoutRewritingTheText_AndStaysLatin1()
    {
        var caps = new TextStyle(TypeFace.Garamond, 600, 0.1, TextCase.Upper);
        Assert.Equal("BRONZE WORKING", caps.Apply("Bronze working"));
        Assert.Equal("ÿ", caps.Apply("ÿ"));   // ÿ's upper case lies outside Latin-1: kept
        var d = new DrawList();
        d.Write(T(UiEra.Classical), 0, 0, "Bronze working", 14, Rgba.Hex(0), TextAlign.Left, FontRole.Title);
        TextCmd t = Assert.IsType<TextCmd>(d.Commands[0]);
        Assert.Equal("Bronze working", t.Text);          // the semantic text is untouched
        Assert.Equal(TextCase.Upper, t.Style!.Value.Case);
        Assert.Equal(T(UiEra.Classical).Type.Size(14), t.Size);
        string svg = SvgWriter.Write(d, 300, 40);
        Assert.Contains(">BRONZE WORKING</text>", svg, StringComparison.Ordinal);   // the SVG sets it cased
        // Measurement includes the case and the tracking.
        double plain = ApproxTextMeasure.Instance.Width("Bronze working", 14, FontRole.Heading);
        Assert.True(ApproxTextMeasure.Instance.Width(T(UiEra.Classical), "Bronze working", 14, FontRole.Title) > plain);
        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"{0.1 * 14:0.##}"), (caps.TrackingPx(14)).ToString("0.##", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Typography_UsesOnlyWeightsTheGameCanRender()
    {
        // The atlas holds each face's regular instance and the ImGui backend emboldens at 600+, so a
        // weight strictly between 400 and 600 would show in the previews and never in the game.
        foreach (EraTheme t in EraThemes.All)
            foreach (FontRole role in Enum.GetValues<FontRole>())
            {
                int w = t.Type.For(role).Weight;
                Assert.True(w == 400 || w >= TextStyle.BoldWeight, $"{t.Era} {role}: weight {w}");
            }
    }

    [Fact]
    public void PreviewMeasure_FollowsTheFaceAndWeight_TheWayThePreviewsSetThem()
    {
        var m = ApproxTextMeasure.Instance;
        var garamond = new TextStyle(TypeFace.Garamond, 400, 0.0, TextCase.AsWritten);
        var sans = garamond with { Face = TypeFace.PlexSans };
        var serif = garamond with { Face = TypeFace.PlexSerif };
        const string lower = "cultivation of roots", caps = "TECHNOLOGY";
        // Regular Garamond IS the table: styled and unstyled agree.
        Assert.Equal(m.Width(lower, 14, FontRole.Body), m.Width(lower, 14, FontRole.Body, garamond), 9);
        // The Plex faces set lower case a fifth wider than Garamond and capitals narrower.
        foreach (TextStyle plex in new[] { sans, serif })
        {
            Assert.True(m.Width(lower, 14, FontRole.Body, plex) > 1.18 * m.Width(lower, 14, FontRole.Body, garamond), plex.Face.ToString());
            Assert.True(m.Width(caps, 14, FontRole.Body, plex) < m.Width(caps, 14, FontRole.Body, garamond), plex.Face.ToString());
        }
        // Garamond's bold instance widens lower case; the Plex faces embolden synthetically, at
        // their regular advances.
        Assert.True(m.Width(lower, 14, FontRole.Body, garamond with { Weight = 700 }) > 1.08 * m.Width(lower, 14, FontRole.Body, garamond));
        Assert.Equal(m.Width(lower, 14, FontRole.Body, sans), m.Width(lower, 14, FontRole.Body, sans with { Weight = 600 }), 9);
    }
}
