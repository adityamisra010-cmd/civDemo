using ImGuiNET;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Actions;
using Sim.Ui.Ages;
using Sim.Ui.Headless;
using Sim.Ui.ImGuiIntegration;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Tests;

/// <summary>The worlds the stage-B readability tests read: the canonical turn-1 world, the same world at A3/A6/A9
/// (only the Age differs), and a small session played until it may advance to Age II (the flow is offered).</summary>
public sealed class ReadabilityWorldsFixture
{
    public UiSession Session { get; } = UiSession.Start(42);
    public UiSession Eligible { get; }

    public ReadabilityWorldsFixture()
    {
        Eligible = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        AgePreview.PlayToEligible(Eligible);
    }

    public WorldState At(int age) => age == 1 ? Session.World : EraPreview.WorldAt(Session.World, Session.Config.Ages!, UiPlayer.Empire, age);
}

/// <summary>
/// M5 POLISH, UI READABILITY — STAGE B (Director directive 2026-10-06 §2; packets UR-4 Research, UR-5 Age, UR-7
/// verification): the research card's whole name at the body role, its minimum width honoured at every window (lanes
/// wrap instead), the detail panel as a drawer in narrow windows and in decision order, distinct state fills; the Age
/// panel that scrolls and never drops a line, its priority order and chips at 7:1, the flow's 48 px decision and a
/// grid that fits 1366; the map's labels by role; and the text-size census of every DrawList surface in four eras —
/// nothing below Caption and most at the Body role — with the real font atlas.
/// </summary>
[Collection("ImGui context")]
public class ReadabilityStageBTests(ReadabilityWorldsFixture fx) : IClassFixture<ReadabilityWorldsFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");
    private ResearchContent Content => fx.Session.Config.Research!;
    private Sim.Core.Systems.Ages.AgeContent Ages => fx.Session.Config.Ages!;

    private ProgressionScreen Research(WorldState w, double width, double height, ITextMeasure m, TreeTab tab = TreeTab.Technology, double scale = 1.0)
    {
        var s = new ProgressionScreen(Content, Me)
        {
            Theme = EraThemes.For(UiEras.Of(w, Ages, Me)),
            Age = AgePanelModel.Build(w, Ages, [], Me),
            Scale = scale,
        };
        s.Refresh(w);
        s.Tab = tab;
        s.Paint(width, height, m);
        return s;
    }

    // ================================================================== UR-4: the research card

    /// <summary>Every one of the content's names fits a card of the MINIMUM width on two lines at the body role in
    /// every era — the primary label is never cut (it was "…" for 94 of 598 names at 1920×1080, 9–13 characters at 1366).</summary>
    [Fact]
    public void EveryResearchName_FitsTwoLines_OnTheNarrowestCard_InEveryEra_WithTheRealAtlas()
    {
        using var gui = new HeadlessImGui(Assets(), 1920, 1080);
        var m = new DrawListImGuiBackend(gui.Fonts);
        double minW = new TreeLayoutOptions().MinCardWidth;
        foreach (EraTheme t in EraThemes.All)
        {
            double size = TypeScale.Px(t, TypeRole.Body, FontRole.Heading);
            (double w1, double w2) = ProgressionScreen.NameWidths(t, minW, 1.0);
            foreach (ResearchNode n in Content.Nodes)
            {
                (string l1, string l2) = ProgressionScreen.TwoLines(m, t, n.Name, size, w1, w2, FontRole.Heading);
                Assert.True(m.Width(t, l1, size, FontRole.Heading) <= w1 + 0.01, $"{t.Era} '{n.Name}' line 1");
                Assert.True(m.Width(t, l2, size, FontRole.Heading) <= w2 + 0.01, $"{t.Era} '{n.Name}' does not fit two lines");
                Assert.Equal(n.Name, l2.Length == 0 ? l1 : l1.EndsWith('-') ? l1 + l2 : l1 + " " + l2);
            }
        }
    }

    [Theory]
    [InlineData(1080, 640, 1.0)]
    [InlineData(1280, 800, 1.0)]
    [InlineData(1366, 768, 1.0)]
    [InlineData(1920, 1080, 1.0)]
    [InlineData(2560, 1440, 1.375)]
    [InlineData(3840, 2160, 2.0)]
    public void Cards_AreNeverNarrowerThanTheMinimum_LanesWrapInstead_AndTheWidthStillFits(int w, int h, double scale)
    {
        foreach (TreeTab tab in new[] { TreeTab.Technology, TreeTab.Civics })
        {
            ProgressionScreen s = Research(fx.Session.World, w, h, ApproxTextMeasure.Instance, tab, scale);
            TreeLayout L = s.Layout;
            double min = new TreeLayoutOptions().MinCardWidth * scale;
            Assert.True(L.Width <= s.TreeViewportWidth + 1e-9);
            foreach (PlacedVertex p in L.Placed)
            {
                if (p.Hidden) continue;
                Assert.True(p.W >= min - 1e-9, $"{w}x{h} {tab}: card {p.Vertex} is {p.W:0.0} px (< {min})");
                Assert.True(p.X >= 0 && p.Right <= L.Width + 1e-9);
            }
            for (int i = 0; i < L.Placed.Count; i++)
                for (int j = i + 1; j < L.Placed.Count; j++)
                {
                    PlacedVertex p = L.Placed[i], q = L.Placed[j];
                    if (p.Hidden || q.Hidden) continue;
                    Assert.False(p.X < q.Right && q.X < p.Right && p.Y < q.Bottom && q.Y < p.Bottom, $"{i} overlaps {j}");
                }
        }
    }

    /// <summary>A tier whose lanes cannot sit side by side at the minimum card width WRAPS its lanes into further lane
    /// rows (it used to force the cards below the minimum: S = max(lanes, …)); the lane order holds, nothing overlaps,
    /// prerequisites stay above their dependents, and the routes still run clear of every card.</summary>
    [Fact]
    public void ANarrowTier_WrapsItsLanes_InsteadOfShrinkingTheCards()
    {
        ResearchGraph g = ResearchGraph.Build(Content, ResearchTree.Technology);
        TreeLayout L = ResearchTreeLayout.Compute(g, TreeLayoutOptions.Scaled(1.0, 700));   // two cards across
        Assert.Contains(L.Segments, s => s.RowBase > 0);
        foreach (PlacedVertex p in L.Placed) if (!p.Hidden) Assert.True(p.W >= L.Options.MinCardWidth - 1e-9);
        foreach (GraphEdge e in g.Edges) Assert.True(L.Placed[e.From].Bottom < L.Placed[e.To].Y);
        foreach (LaneSegment a in L.Segments)
            foreach (LaneSegment b in L.Segments)
                if (a.Tier == b.Tier && a.RowBase == b.RowBase && a.Lane < b.Lane) Assert.True(a.Right <= b.X + 1e-9, "lane order within a lane row");
        foreach (GraphEdge e in g.Edges)
        {
            (double X, double Y)[] pts = ResearchTreeLayout.Route(L, e);
            for (int k = 0; k + 1 < pts.Length; k++)
                foreach (PlacedVertex p in L.Placed)
                {
                    if ((p.Vertex == e.From && k == 0) || (p.Vertex == e.To && k == pts.Length - 2) || p.Hidden) continue;
                    (double x0, double y0) = pts[k]; (double x1, double y1) = pts[k + 1];
                    Assert.False(Math.Min(x0, x1) < p.Right - 0.5 && Math.Max(x0, x1) > p.X + 0.5 && Math.Min(y0, y1) < p.Bottom - 0.5 && Math.Max(y0, y1) > p.Y + 0.5,
                        $"edge {e.From}->{e.To} under card {p.Vertex}");
                }
        }
    }

    [Fact]
    public void ACard_SaysWhyItIsLocked_OrHowLongItWouldTake_InItsStateLine()
    {
        ProgressionScreen s = Research(fx.Session.World, 1920, 1080, ApproxTextMeasure.Instance);
        int checkedLocked = 0, checkedAvailable = 0;
        foreach (int v in s.VisibleVertices())
        {
            if (s.Graph.Vertices[v].External) continue;
            int ci = s.Graph.Vertices[v].ContentIndex;
            ResearchNodeView n = s.Snapshot!.Nodes[ci];
            string line = s.StateLine(ci);
            if (n.State == NodeState.Available)
            {
                Assert.Matches(@"^Available · ~\d+ turns$", line);
                checkedAvailable++;
            }
            else if (n.State == NodeState.Locked && n.Lock.HasFlag(LockReason.MissingPrerequisites))
            {
                PrereqView first = n.Prerequisites.First(p => !p.Completed && p.Kind == EdgeKind.And);
                Assert.StartsWith("Needs " + first.Name, line, StringComparison.Ordinal);
                checkedLocked++;
            }
        }
        Assert.True(checkedAvailable > 0 && checkedLocked > 0);
    }

    // ================================================================== UR-4: the detail panel

    [Fact]
    public void BelowSixteenHundred_TheDetailIsADrawer_ThatOpensOnSelect_LeavesTheStripFree_AndCloses()
    {
        ProgressionScreen s = Research(fx.Session.World, 1366, 768, ApproxTextMeasure.Instance);
        Assert.False(s.Docked);
        Assert.False(s.DrawerOpen);
        Assert.Equal(1366 - s.StripW, s.TreeViewportWidth, 6);   // the tree takes the whole window
        int pick = s.FrontierNode();
        s.Focus(pick, jump: true);
        s.Paint(1366, 768, ApproxTextMeasure.Instance);
        Assert.True(s.DrawerOpen);
        RectD drawer = s.DetailRect;
        Assert.True(drawer.Right <= s.MinimapRect().X, "the overview strip stays usable beside the drawer");
        Assert.True(drawer.Y >= s.Canvas.Y, "the lane header and the control row stay usable above it");
        // A card under the drawer is neither hovered nor clicked.
        foreach (PlacedVertex p in s.Layout.Placed)
        {
            double cx = s.Camera.ToScreenX(p.CenterX, s.Canvas.X), cy = s.Camera.ToScreenY(p.CenterY, s.Canvas.Y);
            if (p.Hidden || !drawer.Contains(cx, cy)) continue;
            Assert.Equal(-1, s.NodeAt(cx, cy));
            Assert.True(s.Obscured(cx, cy));
        }
        Assert.Contains(s.Hits, h => h.Kind == HitKind.SetTarget);   // the action is in the drawer
        HitRegion close = s.Hits.Single(h => h.Kind == HitKind.CloseDetail);
        s.Click(close.Rect.CenterX, close.Rect.CenterY);
        Assert.False(s.DrawerOpen);
        Assert.Equal(-1, s.Selected);
        // Escape's path: CloseDrawer closes it, and says so only when there was one.
        s.Focus(pick, jump: true);
        Assert.True(s.CloseDrawer());
        Assert.False(s.CloseDrawer());
        // From 1600 px it is docked beside the tree.
        ProgressionScreen wide = Research(fx.Session.World, 1920, 1080, ApproxTextMeasure.Instance);
        Assert.True(wide.Docked);
        Assert.Equal(1920 - wide.DetailW - wide.StripW, wide.TreeViewportWidth, 6);
    }

    [Fact]
    public void TheDetailPanel_IsInDecisionOrder_WithTheActionDirectlyUnderTheCost()
    {
        ProgressionScreen s = Research(fx.Session.World, 1920, 1080, ApproxTextMeasure.Instance);
        int pick = s.FrontierNode();
        s.Focus(pick, jump: true);
        s.PointerMove(-1, -1);
        DrawList d = s.Paint(1920, 1080, ApproxTextMeasure.Instance);
        RectD panel = s.DetailRect;
        double Y(string heading) => d.Commands.OfType<TextCmd>().First(t => t.Text.StartsWith(heading, StringComparison.Ordinal) && panel.Contains(t.X + 1, t.Y + 1)).Y;
        HitRegion cta = s.Hits.Single(h => h.Kind == HitKind.SetTarget);
        double cost = Y("COST"), enables = Y("ENABLES"), requires = Y("REQUIRES"), eureka = Y("EUREKA"), uni = Y("UNIVERSITY"), about = Y("ABOUT");
        Assert.True(cost < cta.Rect.Y && cta.Rect.Bottom < enables, "the action sits under the cost, before what it enables");
        Assert.True(enables < requires && requires < eureka && eureka < uni && uni < about);
        Assert.True(cta.Rect.H >= 44, "a primary action at least 44 px tall");
        // ENABLES reads the content: a node that declares capabilities lists them.
        ResearchNode node = Content.Nodes[pick];
        if (node.Capabilities.Count > 0)
            Assert.Contains(d.Commands.OfType<TextCmd>(), t => panel.Contains(t.X + 1, t.Y + 1) && t.Text.Contains(node.Capabilities[0].Split(' ')[0], StringComparison.Ordinal));
    }

    /// <summary>ADR-033 D8 continuity, with a node selected: the detail panel flows in the era-invariant reference
    /// type, so its action (and every other region) sits at the same rect in every era — docked and as a drawer.</summary>
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1366, 768)]
    public void WithANodeSelected_EveryRegionIsTheSameInEveryEra(int w, int h)
    {
        List<HitRegion>? first = null;
        foreach (EraTheme theme in EraThemes.All)
        {
            var s = new ProgressionScreen(Content, Me) { Theme = theme, Age = AgePanelModel.Build(fx.Session.World, Ages, [], Me) };
            s.Refresh(fx.Session.World);
            s.Paint(w, h, ApproxTextMeasure.Instance);
            s.Focus(s.FrontierNode(), jump: true);
            s.PointerMove(-1, -1);
            s.Paint(w, h, ApproxTextMeasure.Instance);
            Assert.Contains(s.Hits, x => x.Kind == HitKind.SetTarget);
            first ??= [.. s.Hits];
            Assert.Equal(first, s.Hits);
        }
    }

    [Fact]
    public void TheStateFills_KnownTargetAvailableLocked_AreDistinctSurfaces_AndKeepTheirWords_InEveryEra()
    {
        foreach (EraTheme t in EraThemes.All)
        {
            SemanticTokens s = t.Semantic;
            (string, Rgba)[] fills = [("known", s.CompletedFill), ("target", s.ActiveFill), ("available", s.AvailableFill), ("locked", s.LockedFill)];
            for (int i = 0; i < fills.Length; i++)
            {
                Assert.True(ThemeColor.Contrast(t.Ink.Text, fills[i].Item2) >= EraThemes.StateFillTextFloor, $"{t.Era} text on {fills[i].Item1}");
                for (int j = i + 1; j < fills.Length; j++)
                {
                    double de = ThemeColor.DeltaE(fills[i].Item2, fills[j].Item2);
                    Assert.True(de >= 8.0, $"{t.Era} {fills[i].Item1} vs {fills[j].Item1}: dE {de:0.0}");
                }
            }
            // Each fill is its own family's tint: the target is nearer the Active pigment than the known card is, and
            // the known card nearer the Completed gold than the target is.
            Assert.True(ThemeColor.DeltaE(s.ActiveFill, s.Active) < ThemeColor.DeltaE(s.CompletedFill, s.Active), $"{t.Era} target family");
            Assert.True(ThemeColor.DeltaE(s.CompletedFill, s.Completed) < ThemeColor.DeltaE(s.ActiveFill, s.Completed), $"{t.Era} known family");
        }
        // Where the era's mood already separates them, the least tint is kept; the target is ≥ 12 in most eras.
        int met = EraThemes.All.Count(t => Min(t.Semantic) >= EraThemes.StateFillDistinct);
        Assert.True(met >= 5, $"{met} eras meet dE {EraThemes.StateFillDistinct}");
        static double Min(SemanticTokens s)
        {
            Rgba[] f = [s.CompletedFill, s.ActiveFill, s.AvailableFill, s.LockedFill];
            double m = double.MaxValue;
            for (int i = 0; i < 4; i++) for (int j = i + 1; j < 4; j++) m = Math.Min(m, ThemeColor.DeltaE(f[i], f[j]));
            return m;
        }
    }

    [Fact]
    public void TheResearchChrome_AnswersHover()
    {
        ProgressionScreen s = Research(fx.Session.World, 1920, 1080, ApproxTextMeasure.Instance);
        s.PointerMove(-1, -1);
        string calm = SvgWriter.Write(s.Paint(1920, 1080, ApproxTextMeasure.Instance), 1920, 1080);
        foreach (HitKind kind in new[] { HitKind.Fit, HitKind.Tab, HitKind.LaneToggle, HitKind.Close })
        {
            HitRegion r = s.Hits.First(h => h.Kind == kind && !(kind == HitKind.Tab && h.Arg == (int)s.Tab));
            s.PointerMove(r.Rect.CenterX, r.Rect.CenterY);
            Assert.NotEqual(calm, SvgWriter.Write(s.Paint(1920, 1080, ApproxTextMeasure.Instance), 1920, 1080));
            s.PointerMove(-1, -1);
        }
    }

    // ================================================================== UR-5: the Age panel and the flow

    private AgeScreen AgeOf(UiSession session, WorldState w, EraTheme? theme = null)
    {
        var a = new AgeScreen(Me) { Theme = theme ?? EraThemes.For(UiEras.Of(w, Ages, Me)) };
        a.Refresh(w, Ages, session.Config.UnitFamilies, session.QueuedOrders());
        return a;
    }

    /// <summary>The panel at the heights the small windows leave it (1080 × 640, 1280 × 800, 1366 × 768): scrolled
    /// to its foot, every category, every milestone and every STILL REQUIRED line has been painted in view — nothing
    /// is silently dropped (it used to `break`, losing three categories and the whole STILL REQUIRED block).</summary>
    [Theory]
    [InlineData(260)]
    [InlineData(360)]
    [InlineData(420)]
    public void TheAgePanel_Scrolls_AndEveryLineIsReachable_AtSmallWindows(int height)
    {
        AgeScreen a = AgeOf(fx.Session, fx.Session.World);
        AgePanelModel p = a.Panel;
        var rect = new RectD(12, 200, 480, height);
        var seen = new HashSet<string>();
        for (int k = 0; k < 60; k++)
        {
            var d = new DrawList();
            a.PaintPanel(d, ApproxTextMeasure.Instance, rect, "Bigen");
            RectD view = new(rect.X, rect.Y + 40, rect.W, rect.H - 100);
            foreach (TextCmd t in d.Commands.OfType<TextCmd>()) if (t.Y >= rect.Y + 40 && t.Y <= rect.Bottom - 60) seen.Add(t.Text);
            Assert.Contains(a.Hits, h => h.Kind == AgeHit.OpenKnowledge);   // the footer is always there
            if (k > 0 && a.PanelOverflow <= 0) break;
            a.Wheel(rect.CenterX, rect.CenterY, -1);
        }
        string all = string.Join(" ", seen);
        foreach (CategoryGroup g in p.Categories)
        {
            Assert.Contains(g.Name, all, StringComparison.Ordinal);
            foreach (MilestoneLine l in g.Milestones) Assert.Contains(l.Name.Split(' ')[0], all, StringComparison.Ordinal);
        }
        foreach (string r in p.Remaining) Assert.Contains(r.Split(' ')[0], all, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAgePanel_IsInPriorityOrder_AndItsChipsAreTheBodyInkAtSevenToOne()
    {
        AgeScreen a = AgeOf(fx.Session, fx.Session.World);
        var d = new DrawList();
        a.PaintPanel(d, ApproxTextMeasure.Instance, new RectD(12, 200, 480, 2000), "Bigen");
        List<TextCmd> t = d.Commands.OfType<TextCmd>().ToList();
        double Y(string s) => t.First(x => x.Text.StartsWith(s, StringComparison.Ordinal)).Y;
        Assert.True(Y("AGE I") < Y("Not yet eligible") && Y("Not yet eligible") < Y("STILL REQUIRED") && Y("STILL REQUIRED") < Y("CORE")
            && Y("CORE") < Y("CORE MILESTONES") && Y("CORE MILESTONES") < Y("SUPPORTING  -"));
        foreach (EraTheme theme in EraThemes.All)
        {
            AgeScreen e = AgeOf(fx.Session, fx.Session.World, theme);
            var de = new DrawList();
            e.PaintPanel(de, ApproxTextMeasure.Instance, new RectD(12, 200, 480, 2000), "Bigen");
            TextCmd chip = de.Commands.OfType<TextCmd>().First(x => x.Text.StartsWith("0 / ", StringComparison.Ordinal) || x.Text.StartsWith("1 / ", StringComparison.Ordinal));
            Assert.Equal(theme.Ink.Text, chip.Color);
            Assert.True(ThemeColor.Contrast(chip.Color, theme.Material.PanelRaised) >= 7.0, $"{theme.Era} chip");
        }
    }

    [Fact]
    public void TheAdvanceFlow_Has48PxDecisions_AGridThatFits1366_AndScrollsRatherThanDrops()
    {
        UiSession s = fx.Eligible;
        AgeScreen a = AgeOf(s, s.World);
        a.OpenFlow();
        Assert.True(a.FlowOpen);
        var d = new DrawList();
        a.PaintFlow(d, ApproxTextMeasure.Instance, 1366, 768);
        AgeHitRegion cancel = a.Hits.Single(h => h.Kind == AgeHit.Cancel);
        Assert.Equal(48, cancel.Rect.H, 6);
        Assert.DoesNotContain(a.Hits, h => h.Kind == AgeHit.Confirm);   // no surge chosen: CONFIRM is disabled
        List<AgeHitRegion> cards = a.Hits.Where(h => h.Kind == AgeHit.Surge).ToList();
        Assert.True(cards.Count >= 3);
        Assert.Equal(3, cards.Select(c => Math.Round(c.Rect.X)).Distinct().Count());   // three across at 1366
        foreach (AgeHitRegion c in cards) Assert.True(c.Rect.X >= 0 && c.Rect.Right <= 1366);
        a.Click(cards[0].Rect.CenterX, cards[0].Rect.CenterY);
        a.PaintFlow(d = new DrawList(), ApproxTextMeasure.Instance, 1366, 768);
        AgeHitRegion confirm = a.Hits.Single(h => h.Kind == AgeHit.Confirm);
        Assert.Equal(48, confirm.Rect.H, 6);
        // The modernization lies below the fold at 1366 × 768: the body scrolls to it.
        Assert.True(a.FlowOverflow > 0);
        for (int k = 0; k < 40 && a.FlowOverflow > 0; k++) { a.Wheel(683, 400, -1); a.PaintFlow(d = new DrawList(), ApproxTextMeasure.Instance, 1366, 768); }
        Assert.Contains(d.Commands.OfType<TextCmd>(), t => t.Text.StartsWith("YOUR FORMATIONS", StringComparison.Ordinal) && t.Y > 0 && t.Y < 768);
    }

    // ================================================================== UR-7: the map, and the census

    [Fact]
    public void TheMapsNames_AreSetAtTheBodyRole_InTheErasHand()
    {
        WorldState w = fx.Session.World;
        WorldProjection p = WorldProjection.Build(w, fx.Session.Config, id => fx.Session.Names.Name(id), Me);
        foreach (int age in new[] { 1, 9 })
        {
            EraTheme t = EraThemes.For((UiEra)age);
            var d = new DrawList();
            WorldLens.Paint(d, ApproxTextMeasure.Instance, p, WorldZoom.World, (x, y) => (x + 400, y + 20), 1.0, new RectD(0, 0, 1920, 1080), theme: t, uiScale: 1.0);
            List<TextCmd> names = d.Commands.OfType<TextCmd>().Where(c => c.Role == FontRole.Heading).ToList();
            Assert.Equal(p.Settlements.Count, names.Count);
            foreach (TextCmd n in names)
            {
                Assert.Equal(t.Type.For(FontRole.Heading), n.Style);
                Assert.True(n.Size >= TypeScale.Px(TypeRole.Body, n.Style!.Value.Face) - 0.01, $"A{age} '{n.Text}' at {n.Size}");
            }
            // × the UI scale.
            var big = new DrawList();
            WorldLens.Paint(big, ApproxTextMeasure.Instance, p, WorldZoom.World, (x, y) => (x + 400, y + 20), 1.0, new RectD(0, 0, 1920, 1080), theme: t, uiScale: 2.0);
            Assert.Equal(2.0 * names[0].Size, big.Commands.OfType<TextCmd>().First(c => c.Role == FontRole.Heading).Size, 6);
        }
    }

    /// <summary>
    /// THE CENSUS (UR-7; the audit's §2.3 measurement as a test). For A1, A3, A6 and A9, the Research screen (the
    /// technology tree as it opens, with a node selected, and the civics tree), the Age panel, the Advance-Age flow,
    /// the Policy action surface and the map — painted at 1920 × 1080 with the REAL font atlas: no character below the
    /// Caption floor, and at least 60 % of them at or above the Body role. (Before UR-1…UR-7: 64–100 % of the
    /// Research, Age and Policy characters were below 14 px; the most common size on the tree was 11 px.)
    /// </summary>
    [Fact]
    public void TheCensus_NothingBelowCaption_MostAtBody_OnEverySurface_InFourEras()
    {
        using var gui = new HeadlessImGui(Assets(), 1920, 1080);
        var m = new DrawListImGuiBackend(gui.Fonts);
        var results = new List<TextCensus>();
        foreach (int age in new[] { 1, 3, 6, 9 })
        {
            WorldState w = fx.At(age);
            EraTheme theme = EraThemes.For(UiEras.Of(w, Ages, Me));
            Assert.Equal(age, theme.Ordinal);
            string era = "A" + age + " ";
            // Research.
            ProgressionScreen r = Research(w, 1920, 1080, m);
            results.Add(TextCensus.Of(era + "research tech", r.Paint(1920, 1080, m)));
            if (ResearchQuery.CheapestAvailable(w, Content, Me, ResearchTree.Technology) is ResearchNodeId n)
            {
                r.Focus(Content.IndexOf(n), jump: true);
                results.Add(TextCensus.Of(era + "research tech selected", r.Paint(1920, 1080, m)));
            }
            ProgressionScreen civ = Research(w, 1920, 1080, m, TreeTab.Civics);
            results.Add(TextCensus.Of(era + "research civics", civ.Paint(1920, 1080, m)));
            // The Age panel and the flow.
            AgeScreen a = AgeOf(fx.Session, w);
            var dp = new DrawList();
            a.PaintPanel(dp, m, new RectD(12, 260, 480, 740), "Bigen");
            results.Add(TextCensus.Of(era + "age panel", dp));
            AgeScreen f = AgeOf(fx.Eligible, fx.Eligible.World, theme);
            f.OpenFlow();
            var df = new DrawList();
            f.PaintFlow(df, m, 1920, 1080);
            results.Add(TextCensus.Of(era + "advance flow", df));
            // Policy.
            UiSession s = age == 1 ? fx.Session : UiSession.StartFrom(w, fx.Session.World.Seed);
            int cap = EmpireQuery.TryGetCapital(s.World, Me, out SettlementId c) ? c.Value : -1;
            var policy = new ActionSurfaceScreen { Theme = theme };
            policy.Refresh(ActionSurface.ForSession(s, UiSession.ProductionEra(), cap, theme), s.World, s.Config, Me, id => s.Names.Name(id));
            var dpol = new DrawList();
            policy.Paint(dpol, m, 0, 0, 452);
            results.Add(TextCensus.Of(era + "policy", dpol));
            // The map at World zoom.
            WorldProjection p = WorldProjection.Build(w, fx.Session.Config, id => fx.Session.Names.Name(id), Me);
            var dm = new DrawList();
            WorldLens.Paint(dm, m, p, WorldZoom.World, (x, y) => (x + 448, y + 28), 1.0, new RectD(0, 0, 1920, 1080), ink: MapInk.For(theme), theme: theme);
            results.Add(TextCensus.Of(era + "map", dm));
        }
        var report = string.Join("\n", results.Select(x => x.ToString()));
        foreach (TextCensus x in results)
        {
            Assert.True(x.Chars > 0, x.Surface);
            Assert.True(x.BelowCaption == 0, x + " — below: " + string.Join("; ", x.Below.Take(8)) + "\n" + report);
            Assert.True(x.BodyShare >= 0.60, x + "\n" + report);
        }
    }

    // ================================================================== the vertex budget at large windows

    /// <summary>The 16-bit regression at the large windows and their UI scale (UR-4's bigger type grows the A1 tree's
    /// lists): 2560 × 1440 at s = 1.375 and 3840 × 2160 at s = 2, opened, scrolled and fitted, every frame inside the
    /// draw-data contract (RendererHasVtxOffset; nothing asserts).</summary>
    [Theory]
    [InlineData(2560, 1440, 1.375)]
    [InlineData(3840, 2160, 2.0)]
    public void TheResearchScreen_AtTheLargeWindowsAndTheirScale_StaysInsideTheDrawDataContract(int w, int h, double scale)
    {
        using var gui = new HeadlessImGui(Assets(), w, h, uiScale: scale);
        Assert.True((ImGui.GetIO().BackendFlags & ImGuiBackendFlags.RendererHasVtxOffset) != 0);
        var m = new DrawListImGuiBackend(gui.Fonts);
        ProgressionScreen s = Research(fx.Session.World, w, h, m, scale: scale);
        int maxV = 0;
        void Frame(Action? arrange = null)
        {
            gui.BeginFrame(default, default, 1.0 / 60);
            arrange?.Invoke();
            m.Render(ImGui.GetBackgroundDrawList(), s.Paint(w, h, m));
            ImGuiDrawReport r = gui.EndFrame();
            Assert.True(r.Ok, string.Join("; ", r.Violations));
            maxV = Math.Max(maxV, r.MaxListVertices);
        }
        Frame();
        for (int k = 0; k < 20; k++) { Frame(() => s.ScrollBy(0, 600 * scale)); s.Camera.Set(s.Camera.TargetZoom, s.Camera.TargetPanX, s.Camera.TargetPanY); }
        Frame(() => s.FitAll());
        if (s.FrontierNode() is int f and >= 0) Frame(() => s.Focus(f, jump: true));
        foreach (TreeTab tab in new[] { TreeTab.Civics, TreeTab.Technology }) Frame(() => s.Tab = tab);
        Assert.True(maxV > 0);
    }
}
