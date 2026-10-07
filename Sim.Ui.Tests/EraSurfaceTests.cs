using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Ui.Ages;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-033 D8 — THE ERA LANGUAGE ON THE PAINTED SURFACES, on the research preview's real stepped
/// world with only the player's Age row differing (<see cref="EraPreview.WorldAt"/>): what A1, A2, A5,
/// A8 and A9 actually draw; continuity (the same hit regions, layout and navigation in every era, the
/// full Age names on every card); the transition panel; preview determinism; read-only painting.
/// </summary>
public class EraSurfaceTests(SteppedWorldFixture fx) : IClassFixture<SteppedWorldFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private const double W = 1600, H = 1000;
    private AgeContent Ages => fx.Session.Config.Ages!;

    private WorldState WorldAt(UiEra era) => EraPreview.WorldAt(fx.Session.World, Ages, Me, era.Ordinal());

    /// <summary>The tree as it opens in <paramref name="era"/>, the era DERIVED from the world.</summary>
    private (ProgressionScreen Screen, DrawList List) Tree(UiEra era, TreeTab tab = TreeTab.Technology)
    {
        WorldState w = WorldAt(era);
        var s = new ProgressionScreen(fx.Content, Me)
        {
            Theme = EraThemes.For(UiEras.Of(w, Ages, Me)),
            Age = AgePanelModel.Build(w, Ages, [], Me),
            Tab = tab,
        };
        s.Refresh(w);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        return (s, s.Paint(W, H, ApproxTextMeasure.Instance));
    }

    private static List<TextCmd> Texts(DrawList d) => d.Commands.OfType<TextCmd>().ToList();

    /// <summary>The commands painted inside the visible card rects of the tree canvas.</summary>
    private static List<DrawCmd> InCards(ProgressionScreen s, DrawList d)
    {
        var cards = new List<RectD>();
        foreach (int v in s.VisibleVertices())
        {
            PlacedVertex p = s.Layout.Placed[v];
            if (s.Graph.Vertices[v].External || p.Hidden) continue;
            RectD c = s.Canvas;
            cards.Add(new RectD(s.Camera.ToScreenX(p.X, c.X), s.Camera.ToScreenY(p.Y, c.Y), p.W * s.Camera.Zoom, p.H * s.Camera.Zoom));
        }
        bool In(double x, double y) { foreach (RectD r in cards) if (r.Contains(x, y)) return true; return false; }
        var r0 = new List<DrawCmd>();
        foreach (DrawCmd c in d.Commands)
        {
            bool inside = c switch
            {
                PolylineCmd pl => In(pl.Points[0].X, pl.Points[0].Y),
                TextCmd t => In(t.X + 1, t.Y + 1),
                LineCmd l => In(l.X0, l.Y0),
                RectCmd rc => In(rc.Rect.CenterX, rc.Rect.CenterY),
                PolygonCmd pg => In(pg.Points[0].X + 0.5, pg.Points[0].Y + 0.5),
                CircleCmd ci => In(ci.Cx, ci.Cy),
                _ => false,
            };
            if (inside) r0.Add(c);
        }
        return r0;
    }

    // ------------------------------------------------------------------ what each era paints

    [Fact]
    public void A1_TheTreeIsPaintedPrimitive()
    {
        (ProgressionScreen s, DrawList d) = Tree(UiEra.Prehistoric);
        Assert.Equal(UiEra.Prehistoric, s.Theme.Era);
        List<DrawCmd> cards = InCards(s, d);
        // Hand-cut slabs: freehand closed charcoal outlines, no ruled rectangles for the frames.
        Assert.True(cards.Count(c => c is PolylineCmd { Closed: true }) >= 10);
        Assert.DoesNotContain(cards, c => c is RectCmd r && r.Stroke is not null && r.Fill is null && r.Rect.W > 100);
        // Primitive node presentation: no prerequisite/dependent stubs, no Eureka pips, no discounts.
        Assert.DoesNotContain(Texts(d), t => t.Text.StartsWith('-') && t.Text.EndsWith('%'));
        // The research target's retained progress is carved as tally notches (short strokes), not a bar.
        int target = s.Snapshot!.TargetIndex!.Value;
        PlacedVertex p = s.Layout.Placed[s.Graph.VertexOf(target)];
        RectD c0 = s.Canvas;
        double bottom = s.Camera.ToScreenY(p.Bottom, c0.Y), left = s.Camera.ToScreenX(p.X, c0.X);
        int notches = d.Commands.Count(c => c is LineCmd l && Math.Abs(l.Y1 - l.Y0) > 2 && l.Y0 > bottom - 20 && l.Y0 < bottom
            && l.X0 > left && l.X0 < left + p.W);
        Assert.True(notches >= 8, $"notches {notches}");
        // Heavy old-style type; no modern face anywhere.
        Assert.All(Texts(d), t => Assert.NotEqual(TypeFace.PlexSans, t.Style!.Value.Face));
        Assert.Contains(Texts(d), t => t.Role == FontRole.Heading && t.Style!.Value.Bold);
    }

    [Fact]
    public void A2_DiffersAppropriately_ClayTabletsAndCounters()
    {
        (ProgressionScreen s1, DrawList d1) = Tree(UiEra.Prehistoric);
        (ProgressionScreen s2, DrawList d2) = Tree(UiEra.Neolithic);
        Assert.NotEqual(SvgWriter.Write(d1, W, H), SvgWriter.Write(d2, W, H));
        // Progress as clay counters (circles), Eureka pips appear (density grows), still no stubs.
        int target = s2.Snapshot!.TargetIndex!.Value;
        PlacedVertex p = s2.Layout.Placed[s2.Graph.VertexOf(target)];
        double bottom = s2.Camera.ToScreenY(p.Bottom, s2.Canvas.Y);
        Assert.True(d2.Commands.Count(c => c is CircleCmd ci && ci.Cy > bottom - 16 && ci.Cy < bottom) >= 8);
        static int Pips(List<DrawCmd> cards) => cards.Count(c => c is CircleCmd { R: 3.3 });
        Assert.Equal(0, Pips(InCards(s1, d1)));
        Assert.True(Pips(InCards(s2, d2)) > 0);
        // Smoother tablets: the outline's wobble is a fraction of the slab's.
        Assert.True(s2.Theme.Edge.JitterPx < s1.Theme.Edge.JitterPx);
    }

    [Fact]
    public void A5_IsVisiblyMoreAdvancedThanA2_StructuredCardsAndAccumulatedKnowledge()
    {
        (ProgressionScreen s2, DrawList d2) = Tree(UiEra.Neolithic);
        (ProgressionScreen s5, DrawList d5) = Tree(UiEra.Classical);
        // Prerequisite / dependent stubs (cross-branch structure) appear on the cards.
        int stubs2 = InCards(s2, d2).Count(c => c is PolygonCmd pg && pg.Points.Length == 3);
        int stubs5 = InCards(s5, d5).Count(c => c is PolygonCmd pg && pg.Points.Length == 3);
        Assert.Equal(0, stubs2);
        Assert.True(stubs5 >= 2 * s5.VisibleVertices().Count(v => !s5.Graph.Vertices[v].External) - 4);
        // The lane chips count what is known ("done/total"); the A2 chips show only totals.
        Assert.Contains(Texts(d5), t => System.Text.RegularExpressions.Regex.IsMatch(t.Text, @"^\d+/\d+$"));
        Assert.DoesNotContain(Texts(d2), t => System.Text.RegularExpressions.Regex.IsMatch(t.Text, @"^\d+/\d+$"));
        // Inscriptional capitals for the display titles; denser leading.
        Assert.Contains(Texts(d5), t => t.Role == FontRole.Title && t.Style!.Value.Case == TextCase.Upper);
        Assert.True(s5.Theme.Type.LineHeight < s2.Theme.Type.LineHeight);
    }

    [Fact]
    public void A8_TheTreeIsIndustrial_A9_TheTreeIsModern()
    {
        (ProgressionScreen s8, DrawList d8) = Tree(UiEra.Industrial);
        (ProgressionScreen s9, DrawList d9) = Tree(UiEra.Modern);
        // A8: technical lettering, a drafting grid under the tree, the estimate to complete.
        Assert.Contains(Texts(d8), t => t.Role == FontRole.Caps && t.Style!.Value.Face == TypeFace.PlexSans && t.Style.Value.TrackingEm >= 0.1);
        Assert.True(d8.Commands.Count(c => c is LineCmd l && (l.X0 == l.X1 || l.Y0 == l.Y1) && l.Width <= 0.8) > 100);
        // The estimate to complete: the target's state line (UR-4: every era now — "Researching · 13% · ~7 turns").
        Assert.Contains(Texts(d8), t => System.Text.RegularExpressions.Regex.IsMatch(t.Text, @"^Researching .* ~\d+ turns$"));
        // A9: clean type, no texture, hairline cards (no freehand or double outlines on the cards).
        Assert.All(Texts(d9), t => Assert.Equal(TypeFace.PlexSans, t.Style!.Value.Face));
        Assert.DoesNotContain(InCards(s9, d9), c => c is PolylineCmd);
        Assert.Equal(0.0, s9.Theme.Material.GrainDensity);
    }

    // ------------------------------------------------------------------ continuity

    [Fact]
    public void Continuity_LayoutAndHitRegionsAreIdenticalInEveryEra()
    {
        foreach (TreeTab tab in new[] { TreeTab.Technology, TreeTab.Civics })
        {
            (ProgressionScreen a, _) = Tree(UiEra.Prehistoric, tab);
            foreach (UiEra era in UiEras.All)
            {
                (ProgressionScreen s, _) = Tree(era, tab);
                Assert.Equal(a.Canvas, s.Canvas);
                Assert.Equal(a.MinimapRect(), s.MinimapRect());
                Assert.Equal(a.Layout.Placed, s.Layout.Placed);
                Assert.Equal(a.Hits, s.Hits);   // every lens, tab, lane chip, control, card target and chip
            }
        }
        // The lens pages too.
        foreach (Lens lens in Lenses.All)
        {
            List<HitRegion>? first = null;
            foreach (UiEra era in UiEras.All)
            {
                (ProgressionScreen s, _) = Tree(era);
                s.SetLens(lens);
                s.Paint(W, H, ApproxTextMeasure.Instance);
                first ??= [.. s.Hits];
                Assert.Equal(first, s.Hits);
            }
        }
        // The Age panel's regions.
        List<AgeHitRegion>? age = null;
        foreach (UiEra era in UiEras.All)
        {
            WorldState w = WorldAt(UiEra.Bronze);   // same Age content, only the theme differs
            var screen = new AgeScreen(Me) { Theme = EraThemes.For(era) };
            screen.Refresh(w, Ages, fx.Session.Config.UnitFamilies, []);
            screen.PaintPanel(new DrawList(), ApproxTextMeasure.Instance, new RectD(12, 202, 440, 530), "Capital");
            age ??= [.. screen.Hits];
            Assert.Equal(age, screen.Hits);
        }
    }

    [Fact]
    public void Continuity_TheSameNavigationExistsInEveryEra_AndEveryCardKeepsItsFullAgeName()
    {
        string[] nav = ["KNOWLEDGE & TECHNOLOGY", "TECHNIQUES", "INSTITUTIONS", "INFRASTRUCTURE", "INDUSTRY", "MILITARY", "APPLICATIONS",
            "TECHNOLOGY", "CIVICS", "LAGGING BRANCH", "FIT", "HIDE MAP", "RESEARCHING", "PROGRESSION"];
        foreach (UiEra era in UiEras.All)
        {
            (ProgressionScreen s, DrawList d) = Tree(era);
            var texts = Texts(d).Select(t => t.Text).ToHashSet();
            foreach (string label in nav) Assert.Contains(label, texts);
            Assert.Contains(texts, x => x is "JUMP TO TARGET" or "JUMP TO FRONTIER" || x.StartsWith("JUMP TO", StringComparison.Ordinal));
            // Every visible card names its Age ON THAT CARD, in every era — the numeral beside the cost (UR-4, dated
            // 2026-10-06: the full name moved to the tier strip right above the card, the detail panel and the hover
            // tip, so the card's name can be set at the body role, whole) — checked per card, and every tier strip in
            // view names its Ages in full.
            List<TextCmd> runs = Texts(d);
            RectD canvas = s.Canvas;
            foreach (int v in s.VisibleVertices())
            {
                PlacedVertex p = s.Layout.Placed[v];
                if (s.Graph.Vertices[v].External || p.Hidden) continue;
                var card = new RectD(s.Camera.ToScreenX(p.X, canvas.X), s.Camera.ToScreenY(p.Y, canvas.Y), p.W * s.Camera.Zoom, p.H * s.Camera.Zoom);
                string numeral = ResearchTreeLayout.AgeNumeral(s.Graph.Node(v).Age);
                Assert.True(runs.Any(r => (r.Text.EndsWith("Age " + numeral, StringComparison.Ordinal) || r.Text.EndsWith(" " + numeral, StringComparison.Ordinal))
                    && r.Text.Contains(" RP", StringComparison.Ordinal) && card.Contains(r.X + 1, r.Y + 1)), $"{era}: card {v} lacks its Age \"{numeral}\"");
            }
            foreach (TierBand tb in s.Layout.Tiers)
            {
                double y0 = s.Camera.ToScreenY(tb.Y0, canvas.Y);
                if (y0 < canvas.Y || y0 > canvas.Bottom - 40 || tb.Lo == 0) continue;
                string ages = DrawList.Latin1(ResearchTreeLayout.AgeRangeLabel((tb.Lo, tb.Hi)));
                Assert.Contains(runs, r => r.Text == ages);
            }
        }
        // Each lane chip, a navigation control, names its lane in full on the chip, in both trees and
        // every era (it shrinks to fit rather than lose a word).
        foreach (UiEra era in UiEras.All)
            foreach (TreeTab tab in new[] { TreeTab.Technology, TreeTab.Civics })
            {
                (ProgressionScreen s, DrawList d) = Tree(era, tab);
                List<TextCmd> runs = Texts(d);
                foreach (LaneBox lane in s.Layout.Lanes)
                {
                    RectD chip = s.Hits.Single(h => h.Kind == HitKind.LaneToggle && h.Arg == lane.Index).Rect;
                    string name = lane.Name;   // UR-4: mixed case at the caption role (capitals did not fit at 1600 px)
                    Assert.True(runs.Any(r => r.Text == name && chip.Contains(r.X + 1, r.Y + 1)), $"{era} {tab}: lane chip lacks \"{name}\"");
                }
            }
        // The game's section roster is data, the same list whatever the era.
        Assert.Equal(7, GameSections.Order.Count);
    }

    [Fact]
    public void Continuity_TheChromeFurnitureStaysOnItsRects_InEveryEra()
    {
        foreach (UiEra era in UiEras.All)
            foreach (ChromeElement e in ChromeGeometry.Elements)
            {
                var d = new DrawList();
                ChromeFurniture.Paint(d, EraThemes.For(era), e, Art.UiTheme.FrameHeightPx);
                PanelRect p = e.Panel;
                foreach (DrawCmd c in d.Commands)
                {
                    (double x0, double y0, double x1, double y1) = c switch
                    {
                        RectCmd r => (r.Rect.X, r.Rect.Y, r.Rect.Right, r.Rect.Bottom),
                        LineCmd l => (Math.Min(l.X0, l.X1), Math.Min(l.Y0, l.Y1), Math.Max(l.X0, l.X1), Math.Max(l.Y0, l.Y1)),
                        CircleCmd ci => (ci.Cx - ci.R, ci.Cy - ci.R, ci.Cx + ci.R, ci.Cy + ci.R),
                        PolygonCmd pg => (pg.Points.Min(q => q.X), pg.Points.Min(q => q.Y), pg.Points.Max(q => q.X), pg.Points.Max(q => q.Y)),
                        PolylineCmd pl => (pl.Points.Min(q => q.X), pl.Points.Min(q => q.Y), pl.Points.Max(q => q.X), pl.Points.Max(q => q.Y)),
                        _ => (p.X, p.Y, p.X, p.Y),
                    };
                    Assert.True(x0 >= p.X - 2.5 && y0 >= p.Y - 2.5 && x1 <= p.X + p.Width + 2.5 && y1 <= p.Y + p.Height + 2.5, $"{era} {p.Title} {c}");
                }
            }
    }

    // ------------------------------------------------------------------ the Age transition

    [Fact]
    public void TheTransitionPanel_NamesTheNewAgeInFull_InTheNewEra()
    {
        Assert.Equal("Your civilization has entered the Bronze Age", AgeScreen.EnteredLine("Bronze Age"));
        Assert.Equal("Your civilization has entered the Neolithic / Agricultural Age", AgeScreen.EnteredLine("Neolithic / Agricultural"));
        var screen = new AgeScreen(Me) { Theme = EraThemes.For(UiEra.Neolithic) };
        screen.ShowTransition(2, Ages.Age(2).Name, Ages.Surges[0].Name, 1);
        screen.HoldToast();
        var d = new DrawList();
        screen.PaintToast(d, ApproxTextMeasure.Instance, 1600, 60);
        List<TextCmd> t = Texts(d);
        Assert.Contains(t, x => x.Text == "A NEW AGE BEGINS - AGE II");
        Assert.Contains(t, x => x.Text == "Your civilization has entered the Neolithic / Agricultural Age");
        Assert.Contains(t, x => x.Text == "Records are now kept in incised clay and woven reed.");
        Assert.Contains(t, x => x.Text.Contains("1 formation(s) modernized", StringComparison.Ordinal));
        // Restrained: one panel, top centre, no wider than 760 px.
        Assert.All(d.Commands.OfType<RectCmd>(), r => Assert.True(r.Rect.W <= 760));
    }

    // ------------------------------------------------------------------ previews, determinism, read-only

    [Fact]
    public void Previews_AreByteIdenticalAcrossRuns_AndEachEraIsDerivedFromTheWorld()
    {
        foreach (UiEra era in new[] { UiEra.Prehistoric, UiEra.Classical, UiEra.Modern })
        {
            WorldState w = WorldAt(era);
            Assert.Equal(era, EraPreview.ThemeOf(fx.Session, w).Era);
            Assert.Equal(EraPreview.TreeSvg(fx.Session, w, null), EraPreview.TreeSvg(fx.Session, EraPreview.WorldAt(fx.Session.World, Ages, Me, era.Ordinal()), null));
            Assert.Equal(EraPreview.AgeSvg(fx.Session, w, null), EraPreview.AgeSvg(fx.Session, w, null));
            Assert.Equal(EraPreview.ChromeSvg(fx.Session, w, null, null), EraPreview.ChromeSvg(fx.Session, w, null, null));
        }
        // Only the Age differs between the eras' worlds.
        WorldState a = WorldAt(UiEra.Iron), b = WorldAt(UiEra.Industrial);
        Assert.Equal(4, AgeQuery.CurrentAge(a, Ages, Me));
        Assert.Equal(8, AgeQuery.CurrentAge(b, Ages, Me));
        Assert.Equal(a.Clock.Turn, b.Clock.Turn);
        Assert.Equal(ResearchQuery.CompletedNodes(a, fx.Content, Me), ResearchQuery.CompletedNodes(b, fx.Content, Me));
    }

    [Fact]
    public void PaintingEverySurfaceInEveryEra_NeverChangesTheWorld()
    {
        WorldState live = fx.Session.World;
        string liveHash = WorldHash.ComputeHex(live);
        foreach (UiEra era in UiEras.All)
        {
            WorldState w = WorldAt(era);
            string hash = WorldHash.ComputeHex(w);
            Tree(era);
            EraPreview.AgeSvg(fx.Session, w, null);
            EraPreview.ChromeSvg(fx.Session, w, null, null);
            Assert.Equal(hash, WorldHash.ComputeHex(w));
        }
        Assert.Equal(liveHash, WorldHash.ComputeHex(live));
    }
}
