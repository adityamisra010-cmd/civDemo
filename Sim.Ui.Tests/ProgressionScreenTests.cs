using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Tests;

/// <summary>One real founded world, stepped through the real session and order pathway until
/// some nodes are complete and a target holds partial progress. Shared by the class.</summary>
public sealed class SteppedWorldFixture
{
    public UiSession Session { get; } = ProgressionPreview.SteppedSession();
    public ResearchContent Content => Session.Config.Research!;
}

/// <summary>
/// The KNOWLEDGE &amp; TECHNOLOGY progression screen (docs/architecture/research-tree-ui.md):
/// the graph is the content's graph, the layout is deterministic and complete, the two trees
/// are separate, the seven lenses are present, every node state is ResearchQuery's own answer,
/// and a click produces exactly the order the existing factory builds — never a state write.
/// </summary>
public class ProgressionScreenTests(SteppedWorldFixture fx) : IClassFixture<SteppedWorldFixture>
{
    private static readonly PolityId Me = LaborOrderFactory.PlayerEmpire;
    private const double W = 1600, H = 1000;

    private ProgressionScreen Screen()
    {
        var s = new ProgressionScreen(fx.Content, Me);
        s.Refresh(fx.Session.World);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        return s;
    }

    // ------------------------------------------------------------------ graph

    [Fact]
    public void Edges_AreExactlyThePrerequisites_ClassifiedAndOrFromTheExpression()
    {
        ResearchContent c = fx.Content;
        int orEdges = 0, andEdges = 0;
        foreach (ResearchTree tree in new[] { ResearchTree.Technology, ResearchTree.Civics })
        {
            ResearchGraph g = ResearchGraph.Build(c, tree);
            for (int v = 0; v < g.OwnCount; v++)
            {
                ResearchNode node = g.Node(v);
                var incoming = new List<GraphEdge>();
                foreach (GraphEdge e in g.Edges) if (e.To == v) incoming.Add(e);
                Assert.Equal(node.PrerequisiteNodes.Count, incoming.Count);
                IReadOnlyList<int> must = node.Prerequisite?.MustHoldAtoms() ?? [];
                for (int k = 0; k < incoming.Count; k++)
                {
                    int from = g.Vertices[incoming[k].From].ContentIndex;
                    Assert.Equal(node.PrerequisiteNodes[k], from);
                    bool and = false;
                    foreach (int a in must) if (a == from) and = true;
                    Assert.Equal(and ? EdgeKind.And : EdgeKind.Or, incoming[k].Kind);
                    if (and) andEdges++; else orEdges++;
                }
            }
            // No edge targets an external anchor.
            foreach (GraphEdge e in g.Edges) Assert.False(g.Vertices[e.To].External);
        }
        Assert.True(andEdges > 0);
        Assert.True(orEdges > 0);   // the content has OR prerequisites (e.g. Written law)
        ResearchGraph civ = ResearchGraph.Build(c, ResearchTree.Civics);
        int law = civ.VertexOf(c.IndexOfId("law_code"));
        foreach (GraphEdge e in civ.Edges) if (e.To == law) Assert.Equal(EdgeKind.Or, e.Kind);
    }

    [Fact]
    public void TechnologyAndCivics_AreSeparateGraphs()
    {
        ResearchContent c = fx.Content;
        ResearchGraph tech = ResearchGraph.Build(c, ResearchTree.Technology);
        ResearchGraph civ = ResearchGraph.Build(c, ResearchTree.Civics);
        Assert.Equal(c.TechnologyCount, tech.OwnCount);
        Assert.Equal(c.CivicsCount, civ.OwnCount);
        for (int v = 0; v < tech.OwnCount; v++) Assert.Equal(ResearchTree.Technology, tech.Node(v).Tree);
        for (int v = 0; v < civ.OwnCount; v++) Assert.Equal(ResearchTree.Civics, civ.Node(v).Tree);
        // Civics' cross-tree prerequisites are anchors, never members.
        Assert.True(civ.Vertices.Count > civ.OwnCount);
        for (int v = civ.OwnCount; v < civ.Vertices.Count; v++)
        {
            Assert.True(civ.Vertices[v].External);
            Assert.Equal(ResearchTree.Technology, civ.Node(v).Tree);
        }
        // The screen's tabs switch graphs.
        ProgressionScreen s = Screen();
        HitRegion civTab = Find(s, HitKind.Tab, (int)TreeTab.Civics);
        s.Click(civTab.Rect.CenterX, civTab.Rect.CenterY);
        Assert.Equal(TreeTab.Civics, s.Tab);
        Assert.Same(s.Graphs[1], s.Graph);
    }

    // ------------------------------------------------------------------ layout

    [Fact]
    public void Layout_IsDeterministic_CoversEveryNode_NoOverlap_PrerequisitesPrecede()
    {
        foreach (ResearchTree tree in new[] { ResearchTree.Technology, ResearchTree.Civics })
        {
            ResearchGraph g = ResearchGraph.Build(fx.Content, tree);
            TreeLayout a = ResearchTreeLayout.Compute(g);
            TreeLayout b = ResearchTreeLayout.Compute(ResearchGraph.Build(fx.Content, tree));
            Assert.Equal(g.Vertices.Count, a.Placed.Count);
            for (int v = 0; v < a.Placed.Count; v++)
            {
                Assert.Equal(v, a.Placed[v].Vertex);
                Assert.Equal(a.Placed[v], b.Placed[v]);   // bit-identical geometry
                Assert.True(a.Placed[v].X >= 0 && a.Placed[v].Right <= a.Width);
                Assert.True(a.Placed[v].Y >= 0 && a.Placed[v].Bottom <= a.Height);
            }
            for (int i = 0; i < a.Placed.Count; i++)
                for (int j = i + 1; j < a.Placed.Count; j++)
                {
                    PlacedVertex p = a.Placed[i], q = a.Placed[j];
                    Assert.False(p.X < q.Right && q.X < p.Right && p.Y < q.Bottom && q.Y < p.Bottom, $"{i} overlaps {j}");
                }
            foreach (GraphEdge e in g.Edges) Assert.True(a.Placed[e.From].Column < a.Placed[e.To].Column);
            // Lanes: Technology = trunk + every content subtree; each node in its branch lane.
            if (tree == ResearchTree.Technology)
            {
                Assert.Equal(1 + fx.Content.Branches.Count, a.Lanes.Count - (g.Vertices.Count > g.OwnCount ? 1 : 0));
                for (int v = 0; v < g.OwnCount; v++)
                    Assert.Equal(g.Node(v).Branch, a.Lanes[a.Placed[v].Lane].Branch);
            }
        }
    }

    [Fact]
    public void CellOrder_TieDense_BreaksOnVertexIndex()
    {
        // Every barycentre equal: the order is the vertex index, whatever the input order.
        var keyed = new (double, int)[64];
        for (int i = 0; i < keyed.Length; i++) keyed[i] = (0.5, (i * 37) % 64);
        ResearchTreeLayout.SortByBarycentre(keyed);
        for (int i = 0; i < keyed.Length; i++) Assert.Equal(i, keyed[i].Item2);
        // Mixed: ties inside equal-score groups still break ascending.
        var mixed = new (double, int)[] { (2.0, 9), (1.0, 7), (2.0, 3), (1.0, 1), (2.0, 5) };
        ResearchTreeLayout.SortByBarycentre(mixed);
        Assert.Equal([1, 7, 3, 5, 9], Array.ConvertAll(mixed, k => k.Item2));
    }

    // ------------------------------------------------------------------ state

    [Fact]
    public void NodeStates_AgreeWithResearchQuery_OnASteppedRealWorld()
    {
        IReadOnlyWorldState w = fx.Session.World;
        ResearchContent c = fx.Content;
        ResearchSnapshot snap = ResearchSnapshot.Build(w, c, Me);
        bool[] done = ResearchQuery.CompletedMask(w, c, Me);
        bool stage = ResearchQuery.StageReached(c, done);
        bool hasTarget = ResearchQuery.TryGetTarget(w, Me, out ResearchNodeId target);
        Assert.True(hasTarget);
        int completed = 0;
        for (int i = 0; i < c.Nodes.Count; i++)
        {
            ResearchNodeView v = snap.Nodes[i];
            ResearchNodeId key = c.Nodes[i].Key;
            Assert.Equal(ResearchQuery.IsCompleted(w, Me, key), v.State == NodeState.Completed);
            Assert.Equal(ResearchQuery.IsAvailable(c, i, done, stage), v.Available);
            Assert.Equal(key == target, v.State == NodeState.CurrentTarget);
            Assert.Equal(done[i] ? 0.0 : ResearchQuery.Progress(w, Me, key), v.Progress);
            Assert.Equal(ResearchQuery.EffectiveCost(w, c, Me, i), v.EffectiveCost);
            Assert.Equal(c.Nodes[i].BaseCost, v.BaseCost);
            ResearchQuery.AccelerationPool pool = ResearchQuery.AccelerationPoolOf(w, c, Me, key);
            Assert.Equal(pool.Ceiling, v.EurekaCeiling);
            Assert.Equal(pool.Eureka, v.EurekaCredited);
            Assert.Equal(c.Nodes[i].Eurekas.Count, v.Eurekas.Count);
            if (v.State == NodeState.Locked)
            {
                Assert.NotEqual(LockReason.None, v.Lock);
                Assert.Equal(!ResearchQuery.PrerequisitesMet(c, i, done), v.Lock.HasFlag(LockReason.MissingPrerequisites));
            }
            if (v.State == NodeState.Completed) completed++;
        }
        Assert.True(completed >= ProgressionPreview.CompletedGoal);
        ResearchNodeView t = snap.Nodes[c.IndexOf(target)];
        Assert.True(t.Progress > 0 && t.Fraction < 1.0);   // the one in-progress node
        Assert.Equal(c.IndexOf(target), snap.TargetIndex);
        // Subtree nodes are locked by the research stage while it is not reached.
        if (!snap.StageReached)
            for (int i = 0; i < c.TechnologyCount; i++)
                if (c.Nodes[i].Branch >= 0 && !done[i]) Assert.True(snap.Nodes[i].Lock.HasFlag(LockReason.ResearchStage));
    }

    // ------------------------------------------------------------------ input → orders

    [Fact]
    public void ClickingAnAvailableNode_ReturnsTheFactoryOrder_AndWritesNoState()
    {
        ProgressionScreen s = Screen();
        ResearchContent c = fx.Content;
        string hash = WorldHash.ComputeHex(fx.Session.World);
        int pick = -1;
        for (int i = 0; i < c.TechnologyCount; i++)
            if (s.Snapshot!.Nodes[i].State == NodeState.Available) { pick = i; break; }
        Assert.True(pick >= 0);

        s.Focus(pick, jump: true);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        (double x, double y) = ScreenCenter(s, pick);
        Assert.Equal(pick, s.NodeAt(x, y));
        ProgressionCommand cmd = s.Click(x, y);

        OrderRecord expected = ResearchOrderFactory.SetTarget(fx.Session.World, c, Me, c.Nodes[pick].Key)!.Value;
        Assert.Equal(expected, cmd.Order);
        Assert.Equal(OrderKind.SetResearchTarget, cmd.Order!.Value.Kind);
        Assert.Equal(c.Nodes[pick].Key, cmd.Node);
        Assert.Equal(pick, s.Selected);
        Assert.Equal(pick, s.PendingTarget);
        Assert.Equal(hash, WorldHash.ComputeHex(fx.Session.World));
        // A second click does not re-issue.
        Assert.Null(s.Click(x, y).Order);
    }

    [Fact]
    public void ClickingLockedOrCompletedNodes_OnlySelects()
    {
        ProgressionScreen s = Screen();
        ResearchContent c = fx.Content;
        foreach (NodeState state in new[] { NodeState.Locked, NodeState.Completed, NodeState.CurrentTarget })
        {
            int pick = -1;
            for (int i = 0; i < c.TechnologyCount; i++) if (s.Snapshot!.Nodes[i].State == state) { pick = i; break; }
            Assert.True(pick >= 0, state.ToString());
            s.Focus(pick, jump: true);
            s.Paint(W, H, ApproxTextMeasure.Instance);
            (double x, double y) = ScreenCenter(s, pick);
            ProgressionCommand cmd = s.Click(x, y);
            Assert.Null(cmd.Order);
            Assert.Equal(pick, s.Selected);
            // Locked/completed: the factory refuses too. The current target is still "available" to the
            // factory (re-setting it is a no-op order); the screen does not log a redundant one.
            if (state != NodeState.CurrentTarget)
                Assert.Null(ResearchOrderFactory.SetTarget(fx.Session.World, c, Me, c.Nodes[pick].Key));
        }
    }

    [Fact]
    public void DetailPanelButton_IssuesTheSameOrder_AndTheNextEndTurnAppliesIt()
    {
        var session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        ResearchContent c = session.Config.Research!;
        var s = new ProgressionScreen(c, Me);
        s.Refresh(session.World);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        int pick = c.IndexOfId("cordage");
        s.Selected = pick;
        s.Paint(W, H, ApproxTextMeasure.Instance);
        HitRegion btn = Find(s, HitKind.SetTarget, pick);
        ProgressionCommand cmd = s.Click(btn.Rect.CenterX, btn.Rect.CenterY);
        Assert.Equal(ResearchOrderFactory.SetTarget(session.World, c, Me, c.Nodes[pick].Key), cmd.Order);

        Assert.True(session.EmitResearchOrder(cmd.Node!.Value));
        session.EndTurn();
        s.Refresh(session.World);
        Assert.Equal(-1, s.PendingTarget);   // cleared by the new turn
        NodeState after = s.Snapshot!.Nodes[pick].State;
        Assert.True(after is NodeState.CurrentTarget or NodeState.Completed);
    }

    // ------------------------------------------------------------------ lenses, culling, paint

    [Fact]
    public void SevenLenses_ArePresent_AndOnlyRealDataIsShown()
    {
        Assert.Equal(7, Lenses.All.Count);
        Assert.Equal(["KNOWLEDGE & TECHNOLOGY", "TECHNIQUES", "INSTITUTIONS", "INFRASTRUCTURE", "INDUSTRY", "MILITARY", "APPLICATIONS"],
            Lenses.All.Select(Lenses.Label).ToArray());
        ProgressionScreen s = Screen();
        Assert.Equal(7, s.Hits.Count(h => h.Kind == HitKind.Lens));

        IReadOnlyWorldState w = fx.Session.World;
        LensPage industry = Lenses.Page(Lens.Industry, w, fx.Content, Me);
        Assert.Equal(LensStatus.NotYetSimulated, industry.Status);
        Assert.Empty(industry.Sections);
        // INSTITUTIONS lists exactly the completed civics.
        LensPage inst = Lenses.Page(Lens.Institutions, w, fx.Content, Me);
        int civicsDone = ResearchQuery.CompletedNodes(w, fx.Content, Me, ResearchTree.Civics).Length;
        Assert.Equal(civicsDone, inst.Sections[0].Items.Count);
        // TECHNIQUES lists exactly the techniques of completed nodes.
        int techniques = 0;
        foreach (ResearchNodeId k in ResearchQuery.CompletedNodes(w, fx.Content, Me))
            techniques += fx.Content.Nodes[fx.Content.IndexOf(k)].Techniques.Count;
        Assert.Equal(techniques, Lenses.Page(Lens.Techniques, w, fx.Content, Me).Sections[0].Items.Count);

        HitRegion lens = Find(s, HitKind.Lens, (int)Lens.Military);
        s.Click(lens.Rect.CenterX, lens.Rect.CenterY);
        Assert.Equal(Lens.Military, s.Lens);
        Assert.Equal(Lens.Military, s.Page!.Lens);
    }

    [Fact]
    public void Culling_DrawsOnlyVisibleCards_AndPaintIsDeterministic()
    {
        ProgressionScreen s = Screen();
        List<int> visible = s.VisibleVertices();
        Assert.InRange(visible.Count, 1, s.Graph.Vertices.Count / 4);
        RectD canvas = s.Canvas;
        foreach (int v in visible)
        {
            PlacedVertex p = s.Layout.Placed[v];
            double x0 = s.Camera.ToScreenX(p.X, canvas.X), x1 = s.Camera.ToScreenX(p.Right, canvas.X);
            Assert.True(x1 >= canvas.X && x0 <= canvas.Right);
        }
        // Whole-tree overview still paints every card, once.
        s.FitAll();
        Assert.Equal(s.Graph.Vertices.Count, s.VisibleVertices().Count);

        var a = new ProgressionScreen(fx.Content, Me); a.Refresh(fx.Session.World);
        var b = new ProgressionScreen(fx.Content, Me); b.Refresh(fx.Session.World);
        a.Paint(W, H, ApproxTextMeasure.Instance); b.Paint(W, H, ApproxTextMeasure.Instance);
        Assert.Equal(SvgWriter.Write(a.Paint(W, H, ApproxTextMeasure.Instance), W, H),
                     SvgWriter.Write(b.Paint(W, H, ApproxTextMeasure.Instance), W, H));
    }

    [Fact]
    public void Camera_ZoomAtKeepsThePointUnderTheCursor_AndEases()
    {
        var cam = new ProgressionCamera();
        cam.Set(0.5, 100, 200);
        double wx = cam.ToWorldX(300, 0), wy = cam.ToWorldY(150, 0);
        cam.ZoomAt(300, 150, 2.0);
        for (int i = 0; i < 200 && cam.Advance(1 / 60.0); i++) { }
        Assert.Equal(1.0, cam.Zoom, 9);
        Assert.Equal(wx, cam.ToWorldX(300, 0), 6);
        Assert.Equal(wy, cam.ToWorldY(150, 0), 6);
    }

    [Fact]
    public void TargetCard_IsPaintedWithItsNameAndProgress()
    {
        ProgressionScreen s = Screen();
        int t = s.Snapshot!.TargetIndex!.Value;
        s.Focus(t, jump: true);
        DrawList d = s.Paint(W, H, ApproxTextMeasure.Instance);
        string name = fx.Content.Nodes[t].Name;
        Assert.Contains(d.Commands, cmd => cmd is TextCmd tc && tc.Text.Contains(name, StringComparison.Ordinal));
        Assert.Contains(d.Commands, cmd => cmd is TextCmd tc && tc.Text == "RESEARCHING");
    }

    // ------------------------------------------------------------------ layout polish (rebase on 3e5647f)

    [Fact]
    public void ColumnHeaders_AreDepthTiers_WithAnHonestAgeRange_NotADominantAge()
    {
        foreach (TreeTab tab in new[] { TreeTab.Technology, TreeTab.Civics })
        {
            ProgressionScreen s = Screen();
            s.Tab = tab;
            TreeLayout L = s.Layout;
            (int Lo, int Hi)[] r = ResearchTreeLayout.ColumnAgeRanges(L);
            Assert.Equal(L.Columns, r.Length);
            // Every own node's Age lies inside its column's range: the header never misstates a card.
            for (int v = 0; v < L.Graph.OwnCount; v++)
            {
                int k = ResearchTreeLayout.AgeRank(L.Graph.Node(v).Age);
                if (k == int.MaxValue) continue;
                (int lo, int hi) = r[L.Placed[v].Column];
                Assert.InRange(k, lo, hi);
            }
        }
        Assert.Equal("Age II", ResearchTreeLayout.AgeRangeLabel((2, 2)));
        Assert.Equal("Ages I-IV", DrawList.Latin1(ResearchTreeLayout.AgeRangeLabel((1, 4))));
        Assert.Equal("", ResearchTreeLayout.AgeRangeLabel((0, 0)));
        Assert.Equal("III", ResearchTreeLayout.AgeNumeral("A3"));
    }

    [Fact]
    public void Graph_FillsTheCanvasFromTheLeft_NoGutter()
    {
        ProgressionScreen s = Screen();
        s.Focus(s.Snapshot!.TargetIndex!.Value, jump: true);
        RectD c = s.Canvas;
        // The leftmost column's cards start within the camera's edge slack + layout margin.
        double minX = double.MaxValue;
        foreach (PlacedVertex p in s.Layout.Placed) minX = Math.Min(minX, p.X);
        double sx = s.Camera.ToScreenX(minX, c.X);
        Assert.InRange(sx - c.X, 0, ProgressionCamera.EdgeSlackPx + 60);
    }

    [Fact]
    public void LaneChip_CollapsesAndExpands_HiddenCardsAreNotHitOrPainted()
    {
        ProgressionScreen s = Screen();
        s.Tab = TreeTab.Technology;
        int main = -1;
        foreach (LaneBox l in s.Layout.Lanes) if (l.Id == "main") main = l.Index;
        double openH = s.Layout.Lanes[main].Height;
        s.FitAll();
        s.Paint(W, H, ApproxTextMeasure.Instance);
        HitRegion chip = Find(s, HitKind.LaneToggle, main);
        s.Click(chip.Rect.CenterX, chip.Rect.CenterY);
        Assert.True(s.IsLaneCollapsed(main));
        Assert.True(s.Layout.Lanes[main].Height < openH);
        int hidden = 0;
        foreach (PlacedVertex p in s.Layout.Placed)
            if (p.Lane == main) { Assert.True(p.Hidden); Assert.False(p.Contains(p.X + 1, p.Y)); hidden++; }
        Assert.Equal(s.Layout.Lanes[main].NodeCount, hidden);
        // Other lanes keep every card.
        foreach (PlacedVertex p in s.Layout.Placed) if (p.Lane != main) Assert.False(p.Hidden);
        // Jump-to-target re-expands the target's lane and selects it.
        s.Paint(W, H, ApproxTextMeasure.Instance);
        HitRegion jump = Find(s, HitKind.Frontier, 0);
        s.Click(jump.Rect.CenterX, jump.Rect.CenterY);
        Assert.False(s.IsLaneCollapsed(main));
        Assert.Equal(s.Snapshot!.TargetIndex, s.Selected);
    }

    [Fact]
    public void Minimap_NeverCoversTheSelection_AndIsCollapsible()
    {
        ProgressionScreen s = Screen();
        RectD c = s.Canvas;
        RectD dock = s.MinimapRect();   // nothing selected: the default bottom-right dock
        int ci = s.Snapshot!.TargetIndex!.Value;
        // Put the selected card right under the default dock, then near every other corner.
        foreach ((double fx0, double fy0) in new[] { (dock.CenterX, dock.CenterY), (c.X + 120, c.Bottom - 60), (c.CenterX, c.CenterY) })
        {
            s.Selected = ci;
            PlacedVertex p = s.Layout.Placed[s.Graph.VertexOf(ci)];
            double z = s.Camera.Zoom;
            s.Camera.Set(z, p.CenterX - (fx0 - c.X) / z, p.CenterY - (fy0 - c.Y) / z);
            s.Paint(W, H, ApproxTextMeasure.Instance);
            var card = new RectD(s.Camera.ToScreenX(p.X, c.X), s.Camera.ToScreenY(p.Y, c.Y), p.W * s.Camera.Zoom, p.H * s.Camera.Zoom);
            Assert.False(s.MinimapRect().Inset(-4).Intersects(card), $"minimap covers the selection at ({fx0},{fy0})");
        }
        HitRegion toggle = Find(s, HitKind.MinimapToggle, 0);
        s.Click(toggle.Rect.CenterX, toggle.Rect.CenterY);
        Assert.False(s.MinimapVisible);
        s.Paint(W, H, ApproxTextMeasure.Instance);
        Assert.DoesNotContain(s.Hits, h => h.Kind == HitKind.Minimap);
    }

    private static HitRegion Find(ProgressionScreen s, HitKind kind, int arg)
    {
        foreach (HitRegion h in s.Hits) if (h.Kind == kind && h.Arg == arg) return h;
        throw new InvalidOperationException($"no {kind} {arg}");
    }

    private static (double, double) ScreenCenter(ProgressionScreen s, int contentIndex)
    {
        PlacedVertex p = s.Layout.Placed[s.Graph.VertexOf(contentIndex)];
        RectD c = s.Canvas;
        return (s.Camera.ToScreenX(p.CenterX, c.X), s.Camera.ToScreenY(p.CenterY, c.Y));
    }
}
