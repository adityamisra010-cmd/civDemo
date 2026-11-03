using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Tests;

/// <summary>
/// DIRECTOR PLAYTEST 2026-11, the research trees (both tabs): "When I hover on a civic, the blue lines show the
/// dependencies. But I can't see the whole screen, so I scroll up to see what it depended on; the scroll moves the hover
/// away and I lose it … if I click, it stays selected and its prerequisites light up — orange — so when I scroll up I know
/// exactly which node is the prerequisite. Yellow means researched." A click PINS a card (no order), a double-click orders
/// it as a click used to, the pin and its direct prerequisites are drawn in <see cref="SemanticTokens.Pinned"/> and
/// survive every camera move and hovering other cards, the detail panel stays on the pin, the overview strip marks them,
/// and an off-screen marker brings an out-of-view prerequisite back. And: "I can't see accumulated civics points anywhere."
/// </summary>
public class ResearchPinTests(SteppedWorldFixture fx) : IClassFixture<SteppedWorldFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private const double W = 1920, H = 1080;   // docked: the detail panel beside the tree

    private ProgressionScreen Screen(TreeTab tab = TreeTab.Technology, double w = W, double h = H)
    {
        var s = new ProgressionScreen(fx.Content, Me) { Tab = tab };
        s.Refresh(fx.Session.World);
        s.Paint(w, h, ApproxTextMeasure.Instance);
        return s;
    }

    private static TreeTab TabOf(ResearchContent c, int ci) => c.Nodes[ci].Tree == ResearchTree.Civics ? TreeTab.Civics : TreeTab.Technology;

    private static (double X, double Y) Centre(ProgressionScreen s, int ci)
    {
        PlacedVertex p = s.Layout.Placed[s.Graph.VertexOf(ci)];
        RectD c = s.Canvas;
        return (s.Camera.ToScreenX(p.CenterX, c.X), s.Camera.ToScreenY(p.CenterY, c.Y));
    }

    private static RectD CardRect(ProgressionScreen s, int vertex)
    {
        PlacedVertex p = s.Layout.Placed[vertex];
        RectD c = s.Canvas;
        double z = s.Camera.Zoom;
        return new RectD(s.Camera.ToScreenX(p.X, c.X), s.Camera.ToScreenY(p.Y, c.Y), p.W * z, p.H * z);
    }

    /// <summary>The content's own answer: a node's direct prerequisites, distinct and ascending.</summary>
    private static int[] Prerequisites(ResearchContent c, int ci) => [.. new SortedSet<int>(c.Nodes[ci].PrerequisiteNodes)];

    private static bool IsPinnedInk(DrawCmd cmd, Rgba pinned) => cmd switch
    {
        LineCmd l => l.Color == pinned,
        PolylineCmd p => p.Color == pinned,
        PolygonCmd g => g.Fill == pinned,
        RectCmd r => r.Fill == pinned || r.Stroke == pinned,
        CircleCmd o => o.Fill == pinned || o.Stroke == pinned,
        TextCmd t => t.Color == pinned,
        _ => false,
    };

    /// <summary>A node of <paramref name="tree"/> with an available state the screen may order (not the target).</summary>
    private int AvailableNode(ProgressionScreen s, ResearchTree tree)
    {
        for (int i = 0; i < fx.Content.Nodes.Count; i++)
            if (fx.Content.Nodes[i].Tree == tree && s.Snapshot!.Nodes[i] is { Available: true, IsTarget: false }) return i;
        return -1;
    }

    // ================================================================== click pins, double-click orders

    [Theory]
    [InlineData(TreeTab.Technology)]
    [InlineData(TreeTab.Civics)]
    public void ASingleClick_PinsTheCard_AndReturnsNoOrder_TheDoubleClickReturnsTheFactorysOrder(TreeTab tab)
    {
        ProgressionScreen s = Screen(tab);
        ResearchTree tree = tab == TreeTab.Civics ? ResearchTree.Civics : ResearchTree.Technology;
        int pick = AvailableNode(s, tree);
        Assert.True(pick >= 0, "anti-vacuity: an available node in " + tab);
        string hash = WorldHash.ComputeHex(fx.Session.World);
        s.Focus(pick, jump: true);
        s.Selected = -1;
        s.Paint(W, H, ApproxTextMeasure.Instance);
        (double x, double y) = Centre(s, pick);

        ProgressionCommand click = s.Click(x, y);
        Assert.Equal(ProgressionCommand.None, click);   // no order, no node, nothing for the host
        Assert.Equal(pick, s.Pin);
        Assert.Equal(pick, s.LastCardClick);
        Assert.Equal(-1, s.PendingTarget);

        ProgressionCommand twice = s.DoubleClick(x, y, pick);
        OrderRecord? factory = ResearchOrderFactory.SetTarget(fx.Session.World, fx.Content, Me, fx.Content.Nodes[pick].Key);
        Assert.NotNull(factory);
        Assert.Equal(factory, twice.Order);
        Assert.Equal(fx.Content.Nodes[pick].Key, twice.Node);
        Assert.Equal(pick, s.PendingTarget);
        Assert.Equal(pick, s.Pin);
        Assert.Equal(hash, WorldHash.ComputeHex(fx.Session.World));
    }

    [Fact]
    public void ADoubleClick_OnALockedNode_WithRoutingOn_OrdersItAsTheDestination_ButASingleClickDoesNot()
    {
        Assert.True(fx.Content.Tuning.DestinationRouting);
        ProgressionScreen s = Screen();
        int pick = -1;
        for (int i = 0; i < fx.Content.TechnologyCount && pick < 0; i++) if (s.Snapshot!.Nodes[i].State == NodeState.Locked) pick = i;
        Assert.True(pick >= 0);
        s.Focus(pick, jump: true);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        (double x, double y) = Centre(s, pick);
        Assert.Null(s.Click(x, y).Order);
        ProgressionCommand cmd = s.DoubleClick(x, y, pick);
        Assert.Equal(ResearchOrderFactory.SetTarget(fx.Session.World, fx.Content, Me, fx.Content.Nodes[pick].Key), cmd.Order);
        Assert.NotNull(cmd.Order);
    }

    [Fact]
    public void ADoubleClick_ThatLandsOnAnotherCardThanTheFirst_OrdersNothing()
    {
        ProgressionScreen s = Screen();
        int a = AvailableNode(s, ResearchTree.Technology);
        s.Focus(a, jump: true);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        int b = -1;
        foreach (int v in s.VisibleVertices())
        {
            int ci = s.Graph.Vertices[v].ContentIndex;
            (double bx, double by) = Centre(s, ci);
            if (ci != a && !s.Layout.Placed[v].Hidden && s.NodeAt(bx, by) == ci) { b = ci; break; }
        }
        Assert.True(b >= 0);
        (double x, double y) = Centre(s, b);
        ProgressionCommand cmd = s.DoubleClick(x, y, a);   // the first click pinned a; this one landed on b
        Assert.Null(cmd.Order);
        Assert.Equal(b, s.Pin);
    }

    /// <summary>In a window too narrow for the docked column (below <see cref="ProgressionScreen.DockMinWidth"/>, D-072) the
    /// first click's pin opens the detail drawer over the tree's right edge — over the very card, when it sits there. The
    /// second click of the double-click still belongs to that card (its own place), and orders it; a lone click there
    /// afterwards is the drawer's (it orders nothing).</summary>
    [Fact]
    public void ADoubleClick_OnACardTheFirstClicksDrawerCovers_StillOrdersThatCard()
    {
        const double nw = 960, nh = 640;
        ProgressionScreen s = Screen(TreeTab.Technology, nw, nh);
        Assert.False(s.Docked);
        RectD drawer = s.DetailRect;   // where the drawer opens (the geometry does not depend on the pin)
        int pick = -1;
        double px = 0, py = 0;
        for (int ci = 0; ci < fx.Content.TechnologyCount && pick < 0; ci++)
        {
            if (s.Snapshot!.Nodes[ci] is not { Available: true, IsTarget: false }) continue;
            // Scroll the card's row into view; then it lies under the drawer when its centre is right of the drawer's edge.
            PlacedVertex p = s.Layout.Placed[s.Graph.VertexOf(ci)];
            s.Camera.Set(1.0, s.Camera.TargetPanX, p.Y - 100);
            s.Paint(nw, nh, ApproxTextMeasure.Instance);
            (double x, double y) = Centre(s, ci);
            if (drawer.Contains(x, y) && s.NodeAt(x, y) == ci) { pick = ci; px = x; py = y; }
        }
        if (pick < 0)
        {
            // No available card under the drawer in this world: any card there (a locked one is ordered as the destination).
            for (int v = 0; v < s.Graph.OwnCount && pick < 0; v++)
            {
                int ci = s.Graph.Vertices[v].ContentIndex;
                if (s.Snapshot!.Nodes[ci].State is NodeState.Completed or NodeState.CurrentTarget) continue;
                s.Camera.Set(1.0, s.Camera.TargetPanX, s.Layout.Placed[v].Y - 100);
                s.Paint(nw, nh, ApproxTextMeasure.Instance);
                (double x, double y) = Centre(s, ci);
                if (drawer.Contains(x, y) && s.NodeAt(x, y) == ci) { pick = ci; px = x; py = y; }
            }
        }
        Assert.True(pick >= 0, "anti-vacuity: a card where the drawer opens");
        Assert.Null(s.Click(px, py).Order);
        s.Paint(nw, nh, ApproxTextMeasure.Instance);
        Assert.True(s.DrawerOpen);
        Assert.Equal(-1, s.NodeAt(px, py));   // the drawer now covers it
        ProgressionCommand cmd = s.DoubleClick(px, py, pick);
        Assert.Equal(ResearchOrderFactory.SetTarget(fx.Session.World, fx.Content, Me, fx.Content.Nodes[pick].Key), cmd.Order);
        Assert.NotNull(cmd.Order);
        // Without the first click's card (a lone click), the drawer answers: nothing is ordered.
        Assert.Null(s.DoubleClick(px, py).Order);
    }

    [Fact]
    public void TheHostsDoubleClickWindow_IsFrameTime_AndTravel_OnTheSameCard()
    {
        Assert.Equal(0.4, CardDoubleClick.WindowSeconds);
        var d = new CardDoubleClick();
        Assert.Equal(-1, d.Pending(0.0, 10, 10));   // nothing before the first click
        d.Record(1.0, 100, 100, 7);
        Assert.Equal(7, d.Pending(1.4, 100, 100));   // at the window's end
        Assert.Equal(-1, d.Pending(1.4001, 100, 100));   // too late
        Assert.Equal(7, d.Pending(1.2, 106, 94));   // within the travel
        Assert.Equal(-1, d.Pending(1.2, 107, 100));   // too far
        Assert.Equal(-1, d.Pending(0.9, 100, 100));   // a clock that ran backwards is not a double-click
        d.Record(1.0, 100, 100, -1);   // a click on chrome or empty canvas
        Assert.Equal(-1, d.Pending(1.1, 100, 100));
        d.Record(2.0, 50, 50, 3);
        d.Reset();
        Assert.Equal(-1, d.Pending(2.1, 50, 50));
    }

    // ================================================================== the orange set

    /// <summary>For EVERY node of both trees: pinned, the orange set is exactly its direct prerequisites — required, "one
    /// of" and the other tree's — as the content declares them.</summary>
    [Fact]
    public void TheOrangeSet_IsExactlyTheDirectPrerequisites_RequiredOneOfAndCrossTree_ForEveryNode()
    {
        ResearchContent c = fx.Content;
        ProgressionScreen s = Screen();
        int withOr = 0, withCross = 0;
        for (int ci = 0; ci < c.Nodes.Count; ci++)
        {
            s.Tab = TabOf(c, ci);
            s.Selected = ci;
            Assert.Equal(ci, s.Pin);
            int[] want = Prerequisites(c, ci);
            Assert.Equal(want, s.PinnedPrerequisites());
            int v = s.Graph.VertexOf(ci);
            foreach (GraphEdge e in s.Graph.Edges)
            {
                if (e.To != v) continue;
                if (e.Kind == EdgeKind.Or) withOr++;
                if (s.Graph.Vertices[e.From].External) withCross++;
            }
        }
        Assert.True(withOr > 0, "anti-vacuity: some pinned node has a 'one of' prerequisite");
        Assert.True(withCross > 0, "anti-vacuity: some pinned node has a prerequisite in the other tree");
        s.Selected = -1;
        Assert.Empty(s.PinnedPrerequisites());
    }

    /// <summary>A civic with a "one of" prerequisite and a Technology prerequisite (the "From Technology" column), or the
    /// first with each; content index.</summary>
    private int OrAndCrossNode()
    {
        ResearchGraph g = ResearchGraph.Build(fx.Content, ResearchTree.Civics);
        int fallback = -1;
        for (int v = 0; v < g.OwnCount; v++)
        {
            bool or = false, cross = false;
            foreach (GraphEdge e in g.Edges)
                if (e.To == v) { or |= e.Kind == EdgeKind.Or; cross |= g.Vertices[e.From].External; }
            if (or && cross) return g.Vertices[v].ContentIndex;
            if ((or || cross) && fallback < 0) fallback = g.Vertices[v].ContentIndex;
        }
        return fallback;
    }

    [Fact]
    public void ThePaintedOrange_IsExactlyThePinsPrerequisitesCards_AndItsIncomingEdges_ThickerAndDashedForOneOf_AndNoneUnpinned()
    {
        int tech = -1;
        for (int i = 0; i < fx.Content.Nodes.Count && tech < 0; i++)
            if (fx.Content.Nodes[i].Tree == ResearchTree.Technology && Prerequisites(fx.Content, i).Length >= 2) tech = i;
        int dashedSeen = 0, crossSeen = 0;
        foreach (int pick in new[] { OrAndCrossNode(), tech })
        {
            Assert.True(pick >= 0);
            ProgressionScreen s = Screen(TabOf(fx.Content, pick));
            s.Selected = pick;
            s.FitAll();   // every card in view
            DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
            Rgba pinned = s.Theme.Semantic.Pinned;
            RectD canvas = s.Canvas, strip = s.MinimapRect();
            int pv = s.Graph.VertexOf(pick);

            // Cards: every orange outline (a closed polyline on the canvas) surrounds exactly the pin or one of its prerequisites.
            var outlined = new SortedSet<int>();
            foreach (PolylineCmd p in d.Commands.OfType<PolylineCmd>().Where(p => p.Color == pinned && p.Closed))
            {
                double cx = (p.Points.Min(q => q.X) + p.Points.Max(q => q.X)) / 2, cy = (p.Points.Min(q => q.Y) + p.Points.Max(q => q.Y)) / 2;
                Assert.True(canvas.Contains(cx, cy) && !strip.Contains(cx, cy));
                int hit = -1;
                for (int v = 0; v < s.Graph.Vertices.Count; v++) if (CardRect(s, v).Contains(cx, cy)) hit = v;
                Assert.True(hit >= 0, "an orange outline around no card");
                outlined.Add(s.Graph.Vertices[hit].ContentIndex);
            }
            var visible = new HashSet<int>(s.VisibleVertices().Select(v => s.Graph.Vertices[v].ContentIndex));
            var want = new SortedSet<int>(Prerequisites(fx.Content, pick)) { pick };
            want.IntersectWith(visible);
            Assert.True(want.Count >= 2, "anti-vacuity: the pin and a prerequisite in view");
            Assert.Equal(want, outlined);

            // Edges: the orange segments are exactly those of the edges INTO the pin; "one of" ones dashed; thicker than ordinary.
            int segments = 0, dashed = 0;
            foreach (GraphEdge e in s.Graph.Edges)
            {
                if (e.To != pv) continue;
                int n = ResearchTreeLayout.Route(s.Layout, e).Length - 1;
                segments += n;
                if (e.Kind == EdgeKind.Or) dashed += n;
            }
            LineCmd[] orange = [.. d.Commands.OfType<LineCmd>().Where(l => l.Color == pinned)];
            Assert.Equal(segments, orange.Length);
            Assert.Equal(dashed, orange.Count(l => l.Dash is not null));
            dashedSeen += dashed;
            foreach (int ci in outlined) if (s.Graph.Vertices[s.Graph.VertexOf(ci)].External) crossSeen++;
            double ordinary = Math.Max(2.2, s.Theme.Icons.StrokePx) * Math.Max(0.6, s.Camera.Zoom);
            Assert.All(orange, l => Assert.True(l.Width >= 2 * ordinary - 1e-9, "orange edge " + l.Width + " vs ordinary " + ordinary));
            Assert.DoesNotContain(d.Commands.OfType<LineCmd>(), l => l.Y0 >= canvas.Y && l.Color == s.Theme.Semantic.Prerequisite);   // pinned: the orange replaces the hover ink

            // Unpinned: not one command in the orange.
            s.Selected = -1;
            s.PointerMove(-1, -1);
            DrawList none = s.Paint(W, H, ApproxTextMeasure.Instance);
            Assert.DoesNotContain(none.Commands, cmd => IsPinnedInk(cmd, pinned));
        }
        Assert.True(dashedSeen > 0, "anti-vacuity: a dashed 'one of' edge painted orange");
        Assert.True(crossSeen > 0, "anti-vacuity: a 'From Technology' card outlined orange");
    }

    /// <summary>Hovering ANOTHER card while one is pinned: the persistent highlight stays the pin's (its prerequisites
    /// orange, its dependents outlined in the dependent ink), the hovered card adds its transient highlight on top, and the
    /// detail panel stays on the pin while the hover tip names the hovered card.</summary>
    [Fact]
    public void HoveringAnotherCard_KeepsThePinsHighlightAndPanel_AndAddsTheHoveredCardsOnTop()
    {
        ProgressionScreen s = Screen();
        ResearchContent c = fx.Content;
        // A pin with prerequisites and dependents, and another card in view that is neither.
        int[] Dependents(int ci)
        {
            var r = new List<int>();
            for (int k = 0; k < c.Nodes.Count; k++) if (c.Nodes[k].Tree == c.Nodes[ci].Tree && c.Nodes[k].PrerequisiteNodes.Contains(ci)) r.Add(k);
            return [.. r];
        }
        int pin = -1;
        for (int ci = 0; ci < c.Nodes.Count && pin < 0; ci++)
            if (c.Nodes[ci].Tree == ResearchTree.Technology && Prerequisites(c, ci).Length > 0 && Dependents(ci).Length > 0) pin = ci;
        Assert.True(pin >= 0);
        s.Focus(pin, jump: true);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        (int[] pinPre, int[] pinDep) = s.Highlight();
        int other = -1;
        foreach (int v in s.VisibleVertices())
        {
            int ci = s.Graph.Vertices[v].ContentIndex;
            (double x, double y) = Centre(s, ci);
            if (ci == pin || pinPre.Contains(ci) || pinDep.Contains(ci) || s.Graph.Vertices[v].External || s.NodeAt(x, y) != ci) continue;
            other = ci;
            break;
        }
        Assert.True(other >= 0);
        (double ox, double oy) = Centre(s, other);
        s.PointerMove(ox, oy);
        Assert.Equal(other, s.Hovered);
        Assert.Equal(pin, s.Pin);
        Assert.Equal(pin, s.FocusNode);
        Assert.Equal(pin, s.DetailNode);
        // The persistent highlight is the pin's, not the hovered card's; the transient one is the hovered card's.
        int[] dep = Dependents(pin);
        Assert.Equal(Prerequisites(c, pin), s.Highlight().Prerequisites);
        Assert.Equal(dep, s.Highlight().Dependents);
        Assert.Equal(Prerequisites(c, other), s.HoverHighlight().Prerequisites);

        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        // The pin's dependents in view keep their dependent-ink outline (the existing style).
        foreach (int ci in dep)
        {
            int v = s.Graph.VertexOf(ci);
            RectD r = CardRect(s, v);
            if (!s.Canvas.Contains(r.CenterX, r.CenterY) || s.Obscured(r.CenterX, r.CenterY)) continue;
            Assert.Contains(d.Commands.OfType<PolylineCmd>(), p => p.Color == s.Theme.Semantic.Dependent && p.Closed
                && r.Contains((p.Points.Min(q => q.X) + p.Points.Max(q => q.X)) / 2, (p.Points.Min(q => q.Y) + p.Points.Max(q => q.Y)) / 2));
        }
        // The orange set is still drawn (the pin's prerequisites), and the hovered card's own edges are drawn in the hover inks.
        Assert.Contains(d.Commands, cmd => cmd is PolylineCmd p && p.Color == s.Theme.Semantic.Pinned);
        bool hoverEdges = d.Commands.OfType<LineCmd>().Any(l => l.Y0 >= s.Canvas.Y && (l.Color == s.Theme.Semantic.Prerequisite || l.Color == s.Theme.Semantic.Dependent));
        Assert.Equal(s.HoverHighlight().Prerequisites.Length + s.HoverHighlight().Dependents.Length > 0, hoverEdges);
        // The docked panel shows the pin, exactly as with no hover; the hover tip names the hovered card (one more run of
        // its name than without the hover).
        RectD panel = s.DetailRect;
        List<string> PanelTexts(DrawList dl) => [.. dl.Commands.OfType<TextCmd>().Where(t => panel.Contains(t.X, t.Y)).Select(t => t.Text)];
        int Named(DrawList dl) => dl.Commands.OfType<TextCmd>().Count(t => !panel.Contains(t.X, t.Y) && t.Text == c.Nodes[other].Name);
        s.PointerMove(-1, -1);
        DrawList still = s.Paint(W, H, ApproxTextMeasure.Instance);
        Assert.Equal(PanelTexts(still), PanelTexts(d));
        Assert.Equal(Named(still) + 1, Named(d));
    }

    [Fact]
    public void ThePin_SurvivesTheWheel_Scrolling_Zooming_ADrag_TheOverviewStrip_AndHover()
    {
        ProgressionScreen s = Screen();
        int pick = AvailableNode(s, ResearchTree.Technology);
        s.Focus(pick, jump: true);
        s.Selected = -1;
        s.Paint(W, H, ApproxTextMeasure.Instance);
        (double x, double y) = Centre(s, pick);
        s.Click(x, y);
        Assert.Equal(pick, s.Pin);
        int[] orange = s.PinnedPrerequisites();
        RectD c = s.Canvas;
        s.Wheel(c.X + c.W * 0.4, c.Y + c.H * 0.5, -3);
        s.ScrollBy(0, 400);
        s.WheelZoom(c.X + c.W * 0.4, c.Y + c.H * 0.5, -2);
        s.Drag(0, 250);
        for (int k = 0; k < 60; k++) s.Advance(1.0 / 60);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        HitRegion strip = s.Hits.Single(h => h.Kind == HitKind.Minimap);
        s.Click(strip.Rect.CenterX, strip.Rect.Y + strip.Rect.H * 0.9);
        s.PointerMove(c.X + c.W * 0.3, c.Y + c.H * 0.6);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        Assert.Equal(pick, s.Pin);
        Assert.Equal(pick, s.DetailNode);
        Assert.Equal(orange, s.PinnedPrerequisites());
        Assert.Equal(-1, s.PendingTarget);
    }

    [Fact]
    public void ThePin_IsCleared_ByEmptyCanvas_TheDrawersClose_AndEscapesClearPin()
    {
        // Empty canvas (docked).
        ProgressionScreen s = Screen();
        int pick = AvailableNode(s, ResearchTree.Technology);
        s.Focus(pick, jump: true);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        RectD c = s.Canvas;
        (double X, double Y)? empty = null;
        for (double y = c.Y + 4; y < c.Bottom - 4 && empty is null; y += 7)
            for (double x = c.X + 4; x < c.X + s.TreeViewportWidth - 4 && empty is null; x += 7)
                if (s.NodeAt(x, y) < 0 && !s.Obscured(x, y) && !s.Hits.Any(h => h.Rect.Contains(x, y))) empty = (x, y);
        Assert.NotNull(empty);
        Assert.Equal(ProgressionCommand.None, s.Click(empty!.Value.X, empty.Value.Y));
        Assert.Equal(-1, s.Pin);
        Assert.Equal(-1, s.DetailNode);

        // The drawer's close button (a window too narrow for the docked column, D-072).
        ProgressionScreen n = Screen(TreeTab.Technology, 960, 640);
        n.Focus(pick, jump: true);
        n.Paint(960, 640, ApproxTextMeasure.Instance);
        Assert.True(n.DrawerOpen);
        HitRegion close = n.Hits.Single(h => h.Kind == HitKind.CloseDetail);
        n.Click(close.Rect.CenterX, close.Rect.CenterY);
        Assert.Equal(-1, n.Pin);
        Assert.False(n.DrawerOpen);

        // Escape's path: ClearPin clears it and says so only when there was one.
        s.Focus(pick, jump: true);
        Assert.True(s.ClearPin());
        Assert.Equal(-1, s.Pin);
        Assert.False(s.ClearPin());
    }

    // ================================================================== off screen: the marker and the overview strip

    /// <summary>A tree node with a prerequisite card above its own; content index.</summary>
    private static int WithPrerequisiteAbove(ProgressionScreen s)
    {
        for (int v = 0; v < s.Graph.OwnCount; v++)
            foreach (GraphEdge e in s.Graph.Edges)
                if (e.To == v && s.Layout.Placed[e.From].Y < s.Layout.Placed[v].Y) return s.Graph.Vertices[v].ContentIndex;
        return -1;
    }

    [Theory]
    [InlineData(TreeTab.Technology)]
    [InlineData(TreeTab.Civics)]
    public void TheOffscreenMarker_AppearsOnlyWhileAPrerequisiteIsOutOfView_AndItsClickBringsTheNearestIn(TreeTab tab)
    {
        ProgressionScreen s = Screen(tab);
        int pick = WithPrerequisiteAbove(s);
        Assert.True(pick >= 0);
        s.Focus(pick, jump: true);
        // Centred on the pin: a marker exactly for each edge that has a prerequisite out of view.
        s.Paint(W, H, ApproxTextMeasure.Instance);
        foreach (bool below in new[] { false, true })
            Assert.Equal(s.PinnedOffscreen(below).Count > 0, s.Hits.Any(h => h.Kind == HitKind.PinnedOffscreen && h.Arg == (below ? 1 : -1)));
        // The pinned card's own prerequisites all in view (the camera on them): no marker.
        s.FitAll();
        s.Paint(W, H, ApproxTextMeasure.Instance);
        if (s.Layout.Height * s.Camera.Zoom <= s.Canvas.H)
        {
            Assert.Empty(s.PinnedOffscreen(below: false));
            Assert.DoesNotContain(s.Hits, h => h.Kind == HitKind.PinnedOffscreen);
        }
        // The pinned card at the canvas's top: its prerequisites above are out of view, and the marker says how many.
        PlacedVertex p = s.Layout.Placed[s.Graph.VertexOf(pick)];
        s.Camera.Set(1.0, s.Camera.TargetPanX, p.Y - 20);
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        List<int> above = s.PinnedOffscreen(below: false);
        Assert.NotEmpty(above);
        HitRegion marker = s.Hits.Single(h => h.Kind == HitKind.PinnedOffscreen && h.Arg == -1);
        Assert.Contains(d.Commands.OfType<TextCmd>(), t => t.Text == ProgressionScreen.OffscreenLabel(above.Count, below: false));
        Assert.True(marker.Rect.Y >= s.Canvas.Y && marker.Rect.Bottom <= s.Canvas.Y + s.Canvas.H / 3, "the marker sits at the top edge");
        // A card under the marker is not hovered or clicked through it.
        Assert.True(s.Obscured(marker.Rect.CenterX, marker.Rect.CenterY));
        // Its click scrolls the nearest one wholly into view; the pin stays; nothing is ordered.
        int nearest = above[0];
        ProgressionCommand cmd = s.Click(marker.Rect.CenterX, marker.Rect.CenterY);
        Assert.Equal(ProgressionCommand.None, cmd);
        for (int k = 0; k < 120; k++) s.Advance(1.0 / 60);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        PlacedVertex n = s.Layout.Placed[s.Graph.VertexOf(nearest)];
        RectD c = s.Canvas;
        Assert.InRange(s.Camera.ToScreenY(n.Y, c.Y), c.Y, c.Bottom);
        Assert.InRange(s.Camera.ToScreenY(n.Bottom, c.Y), c.Y, c.Bottom);
        Assert.DoesNotContain(nearest, s.PinnedOffscreen(below: false));
        Assert.Equal(pick, s.Pin);
        // No pin: no marker, whatever the scroll.
        s.Selected = -1;
        s.Camera.Set(1.0, s.Camera.TargetPanX, p.Y - 20);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        Assert.DoesNotContain(s.Hits, h => h.Kind == HitKind.PinnedOffscreen);
    }

    /// <summary>Tie-dense (CLAUDE.md: an ordering over double-valued keys breaks ties by id): the off-screen prerequisites
    /// are ordered by their distance out of view, then by content index — over every node of both trees pinned at the
    /// canvas's top, where prerequisites on one tier row tie on the distance.</summary>
    [Fact]
    public void TheOffscreenOrder_IsDistanceThenContentIndex_AndTiesOccur()
    {
        int ties = 0;
        foreach (TreeTab tab in new[] { TreeTab.Technology, TreeTab.Civics })
        {
            ProgressionScreen s = Screen(tab);
            for (int v = 0; v < s.Graph.OwnCount; v++)
            {
                int ci = s.Graph.Vertices[v].ContentIndex;
                s.Selected = ci;
                s.Camera.Set(1.0, s.Camera.TargetPanX, s.Layout.Placed[v].Y - 20);
                List<int> above = s.PinnedOffscreen(below: false);
                double y0 = s.Camera.ToWorldY(s.Canvas.Y, s.Canvas.Y);
                for (int k = 0; k + 1 < above.Count; k++)
                {
                    double ga = y0 - s.Layout.Placed[s.Graph.VertexOf(above[k])].Y, gb = y0 - s.Layout.Placed[s.Graph.VertexOf(above[k + 1])].Y;
                    Assert.True(ga < gb || (ga == gb && above[k] < above[k + 1]), "order at " + k + " for node " + ci);
                    if (ga == gb) ties++;
                }
            }
        }
        Assert.True(ties > 0, "anti-vacuity: some prerequisites tie on their distance");
    }

    [Fact]
    public void TheOverviewStrip_MarksThePinAndEachOrangePrerequisite_AtItsHeight_InTheOrange()
    {
        ProgressionScreen s = Screen(TreeTab.Civics);
        int pick = OrAndCrossNode();
        s.Focus(pick, jump: true);
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        RectD r = s.MinimapRect();
        Rgba pinned = s.Theme.Semantic.Pinned;
        double sx = r.W / Math.Max(1, s.Layout.Width), sy = r.H / Math.Max(1, s.Layout.Height);
        RectCmd[] marks = [.. d.Commands.OfType<RectCmd>().Where(c => c.Fill == pinned)];
        var want = new List<int>(s.PinnedPrerequisites()) { pick };
        foreach (int ci in want)
        {
            PlacedVertex p = s.Layout.Placed[s.Graph.VertexOf(ci)];
            double my = r.Y + p.Y * sy;
            Assert.Contains(marks, m => Math.Abs(m.Rect.Y - my) < 1e-9 && Math.Abs(m.Rect.X - (r.X + p.X * sx)) < 1e-9);   // the node's own mark
            Assert.Contains(marks, m => m.Rect.Right <= r.X && Math.Abs(m.Rect.CenterY - (my + Math.Max(3, p.H * sy) / 2)) < 1e-9);   // the tab
        }
        // Two marks per orange card, one more for the pin (its tab on the right too): nothing else in the orange there.
        Assert.Equal(2 * want.Count + 1, marks.Count(m => m.Rect.X >= r.X - 8 && m.Rect.Right <= r.Right + 8));
    }

    // ================================================================== D-073: the pin is carried across the tabs

    /// <summary>The content index of the node with data key <paramref name="key"/>.</summary>
    private int NodeOfKey(int key)
    {
        for (int i = 0; i < fx.Content.Nodes.Count; i++) if (fx.Content.Nodes[i].Key.Value == key) return i;
        return -1;
    }

    /// <summary>D-073 (Director playtest 2026-11-03): EVERY node, pinned and then viewed on EITHER tab, stays the pin, and
    /// the orange set on the tab shown is exactly its direct prerequisites that have a card there — on its own tab all of
    /// them (unchanged), on the other tab the ones that live in that tree (or show there as a "From …" token).</summary>
    [Fact]
    public void ThePin_IsCarriedToTheOtherTab_ForEveryNode_AndItsOrangeSetThereIsItsPrerequisitesWithACardThere()
    {
        ResearchContent c = fx.Content;
        ProgressionScreen s = Screen();
        int carriedWithOrange = 0, tokenWithOrange = 0;
        for (int ci = 0; ci < c.Nodes.Count; ci++)
            foreach (TreeTab tab in new[] { TreeTab.Technology, TreeTab.Civics })
            {
                s.Tab = tab;
                s.Selected = ci;
                Assert.Equal(ci, s.Pin);
                Assert.Equal(ci, s.DetailNode);
                int[] want = [.. Prerequisites(c, ci).Where(p => s.Graph.VertexOf(p) >= 0)];
                Assert.Equal(want, s.PinnedPrerequisites());
                if (tab == TabOf(c, ci)) { Assert.Equal(Prerequisites(c, ci), want); continue; }
                if (want.Length == 0) continue;
                carriedWithOrange++;
                if (s.Graph.VertexOf(ci) >= 0) tokenWithOrange++;
            }
        Assert.True(carriedWithOrange > 0, "anti-vacuity: a node pinned on one tab lights prerequisites on the other");
        Assert.True(tokenWithOrange > 0, "anti-vacuity: a node shown as a 'From …' token on the other tab, pinned there, lights its prerequisites");
        s.Selected = -1;
        Assert.Equal(-1, s.Pin);
        Assert.Empty(s.PinnedPrerequisites());
    }

    /// <summary>D-073, the Director's case: Written law pinned with a single click on the Civics tab, then the Technology
    /// tab opened. The pin holds there: its writing prerequisites (cuneiform, hieroglyphic, Chinese script — "one of") are
    /// outlined in the orange and nothing else is; the cards that are not its prerequisites recede; the overview strip marks
    /// them; the detail panel stays on Written law; Escape clears it; and back on the Civics tab it is still pinned with its
    /// orange edges.</summary>
    [Fact]
    public void WrittenLaw_PinnedOnCivics_KeepsItsTechnologyPrerequisitesOrange_OnTheTechnologyTab()
    {
        ResearchContent c = fx.Content;
        int law = NodeOfKey(1001);
        Assert.True(law >= 0 && c.Nodes[law].Tree == ResearchTree.Civics, "Written law is the civic with key 1001");
        ProgressionScreen s = Screen(TreeTab.Civics);
        s.Focus(law, jump: true);
        Assert.Equal(law, s.Pin);

        s.Tab = TreeTab.Technology;
        s.FitAll();   // every card in view
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        Assert.Equal(law, s.Pin);
        Assert.Equal(law, s.DetailNode);
        Assert.True(s.Graph.VertexOf(law) < 0, "Written law has no card on the Technology tab");
        int[] want = s.PinnedPrerequisites();
        Assert.Equal(Prerequisites(c, law), want);   // every prerequisite of Written law is a technology
        Assert.True(want.Length >= 2, "anti-vacuity: Written law needs one of several writing technologies");
        Assert.All(want, ci => Assert.Equal(ResearchTree.Technology, c.Nodes[ci].Tree));

        // The orange outlines surround exactly the visible prerequisite cards (no pin card on this tab).
        Rgba pinned = s.Theme.Semantic.Pinned;
        RectD canvas = s.Canvas, strip = s.MinimapRect();
        var outlined = new SortedSet<int>();
        foreach (PolylineCmd p in d.Commands.OfType<PolylineCmd>().Where(p => p.Color == pinned && p.Closed))
        {
            double cx = (p.Points.Min(q => q.X) + p.Points.Max(q => q.X)) / 2, cy = (p.Points.Min(q => q.Y) + p.Points.Max(q => q.Y)) / 2;
            if (!canvas.Contains(cx, cy) || strip.Contains(cx, cy)) continue;
            int hit = -1;
            for (int v = 0; v < s.Graph.Vertices.Count; v++) if (CardRect(s, v).Contains(cx, cy)) hit = v;
            Assert.True(hit >= 0, "an orange outline around no card");
            outlined.Add(s.Graph.Vertices[hit].ContentIndex);
        }
        var visible = new HashSet<int>(s.VisibleVertices().Select(v => s.Graph.Vertices[v].ContentIndex));
        var wantVisible = new SortedSet<int>(want);
        wantVisible.IntersectWith(visible);
        Assert.True(wantVisible.Count >= 1, "anti-vacuity: a writing technology in view");
        Assert.Equal(wantVisible, outlined);

        // The overview strip marks each prerequisite (its own mark and its tab), and nothing for a pin card it does not have.
        RectD r = s.MinimapRect();
        RectCmd[] marks = [.. d.Commands.OfType<RectCmd>().Where(m => m.Fill == pinned && m.Rect.X >= r.X - 8 && m.Rect.Right <= r.Right + 8)];
        Assert.Equal(2 * want.Length, marks.Length);

        // The same view with nothing pinned: no orange anywhere.
        s.Selected = -1;
        DrawList none = s.Paint(W, H, ApproxTextMeasure.Instance);
        Assert.DoesNotContain(none.Commands, cmd => IsPinnedInk(cmd, pinned));

        // Escape clears the carried pin; pinned again, the Civics tab still shows it with its orange edges.
        s.Selected = law;
        Assert.True(s.ClearPin());
        Assert.Equal(-1, s.Pin);
        s.Selected = law;
        s.Tab = TreeTab.Civics;
        Assert.Equal(law, s.Pin);
        Assert.Equal(Prerequisites(c, law), s.PinnedPrerequisites());
        s.Focus(law, jump: true);
        DrawList back = s.Paint(W, H, ApproxTextMeasure.Instance);
        Assert.Contains(back.Commands, cmd => cmd is LineCmd l && l.Color == pinned);
    }

    /// <summary>D-073: with a carried pin whose prerequisites are out of view on this tab, the off-screen marker appears and
    /// its click brings the nearest one in, keeping the pin.</summary>
    [Fact]
    public void ACarriedPin_ShowsTheOffscreenMarker_ForItsPrerequisitesOnThisTab_AndItsClickBringsOneIn()
    {
        ResearchContent c = fx.Content;
        int law = NodeOfKey(1001);
        ProgressionScreen s = Screen(TreeTab.Technology);
        // Scroll the Technology tab to the far end from the writing technologies, then pin Written law (as from Civics).
        int far = -1; double farGap = -1;
        PlacedVertex anyWriting = s.Layout.Placed[s.Graph.VertexOf(Prerequisites(c, law)[0])];
        for (int v = 0; v < s.Graph.OwnCount; v++)
        {
            double gap = Math.Abs(s.Layout.Placed[v].Y - anyWriting.Y);
            if (!s.Layout.Placed[v].Hidden && gap > farGap) { farGap = gap; far = s.Graph.Vertices[v].ContentIndex; }
        }
        s.Focus(far, jump: true);
        s.Selected = law;
        s.Paint(W, H, ApproxTextMeasure.Instance);
        bool below = s.PinnedOffscreen(below: true).Count > 0;
        List<int> off = s.PinnedOffscreen(below);
        Assert.True(off.Count > 0, "anti-vacuity: a writing technology out of view");
        Assert.Contains(s.Hits, h => h.Kind == HitKind.PinnedOffscreen);
        s.ScrollToPinnedOffscreen(below);
        for (int i = 0; i < 400; i++) s.Advance(1.0 / 60);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        Assert.Equal(law, s.Pin);
        Assert.DoesNotContain(off[0], s.PinnedOffscreen(below));
    }

    // ================================================================== banked Civics Points

    [Fact]
    public void TheCivicsTab_ShowsTheBank_AsTheQuerysBalance_AndTheTechnologyTabItsRateAsBefore()
    {
        ProgressionScreen s = Screen(TreeTab.Civics);
        Assert.True(s.Snapshot!.TwoLanes);
        double bank = ResearchQuery.CivicsPointsBalance(fx.Session.World, Me);
        Assert.True(bank > 0, "anti-vacuity: the stepped world has banked Civics Points");
        Assert.Equal(bank, s.Snapshot.CivicsBalance);
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        var top = d.Commands.OfType<TextCmd>().Where(t => t.Y < s.LensBarH).Select(t => t.Text).ToList();
        string figure = ProgressionScreen.CivicsBankFigure(bank);
        Assert.Equal(bank.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " CP", figure);
        Assert.Contains(figure, top);
        Assert.Contains(ProgressionScreen.CivicsBankCaption(s.Snapshot.CivicsPerTurn), top);
        Assert.StartsWith("banked · +", ProgressionScreen.CivicsBankCaption(s.Snapshot.CivicsPerTurn));

        s.Tab = TreeTab.Technology;
        var tech = s.Paint(W, H, ApproxTextMeasure.Instance).Commands.OfType<TextCmd>().Where(t => t.Y < s.LensBarH).Select(t => t.Text).ToList();
        Assert.Contains(s.Snapshot.PointsPerTurn.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), tech);
        Assert.Contains("research / turn", tech);
        Assert.DoesNotContain(tech, t => t.Contains("CP", StringComparison.Ordinal));
    }

    [Fact]
    public void TheCivicsChipsHoverCard_StatesTheBankAndTheRate_FromTheCivicsLine()
    {
        var line = new Sim.Ui.Actions.CivicsLine(null, null, false, 22.04, 412.26, false);
        var block = new Sim.Ui.Actions.ResearchBlock(null, null, false, 10, 3, [], false, line);
        Assert.Equal(["Banked: 412.3 CP (kept until spent, never lost)", "Per turn: +22.0 CP",
            "No civic chosen: the points stay banked until you choose one [C]."], StatusFigures.CivicsCardLines(block));
        Assert.Equal("Civics idle - choose [C] · 412.3 CP banked", StatusFigures.Civics(block)!.Text);
        Assert.Empty(StatusFigures.CivicsCardLines(block with { Civics = null }));
        Assert.Equal("412.3 CP", StatusFigures.Bank(412.26));
        Assert.Equal(StatusFigures.Bank(412.26), ProgressionScreen.CivicsBankFigure(412.26));   // one format everywhere
    }

    // ================================================================== the orange token

    /// <summary>The orange's separation and readability (ΔE*ab, <see cref="ThemeColor.DeltaE"/>) in all nine eras.</summary>
    public const double PinnedSeparation = 25.0;

    [Fact]
    public void ThePinnedOrange_IsClearlyApart_FromGoldTealAndEveryOtherOrange_AndReadsOnEveryCardFill_InAllNineEras()
    {
        Assert.Equal(9, EraThemes.All.Count);
        foreach (EraTheme t in EraThemes.All)
        {
            SemanticTokens s = t.Semantic;
            Rgba o = s.Pinned;
            Assert.True(SemanticFamilies.Pinned.Contains(ThemeColor.Hue(o)), $"{t.Era} hue {ThemeColor.Hue(o):0.0}");
            Assert.True(ThemeColor.Saturation(o) >= 0.95, $"{t.Era}: the signal is not muted");
            // Apart from: researched gold, the researching / target teal, the hover prerequisite, retained progress, the
            // Engineering lane (its tier boxes and stripes), the era's accent, danger, and the trunk's gold lane.
            foreach ((string name, Rgba other) in new (string, Rgba)[] { ("Completed", s.Completed), ("Active", s.Active),
                         ("Prerequisite", s.Prerequisite), ("Progress", s.Progress), ("Engineering", s.Lanes.Engineering),
                         ("Accent", t.Material.Accent), ("Danger", s.Danger), ("Main", s.Lanes.Main), ("Infrastructure", s.Infrastructure) })
                Assert.True(ThemeColor.DeltaE(o, other) >= PinnedSeparation, $"{t.Era}: Pinned vs {name} dE {ThemeColor.DeltaE(o, other):0.0}");
            // Readable as a two-tone stroke on every card fill: the casing (the body ink) stands off the fill, the orange off the casing.
            foreach (Rgba fill in new[] { s.CompletedFill, s.ActiveFill, s.AvailableFill, s.LockedFill, t.Material.Panel, t.Material.Field })
                Assert.True(ThemeColor.Contrast(t.Ink.Text, fill) >= 4.5, $"{t.Era}: casing on fill {ThemeColor.Contrast(t.Ink.Text, fill):0.00}");
            Assert.True(ThemeColor.Contrast(o, t.Ink.Text) >= 4.5, $"{t.Era}: orange on its casing {ThemeColor.Contrast(o, t.Ink.Text):0.00}");
        }
    }
}
