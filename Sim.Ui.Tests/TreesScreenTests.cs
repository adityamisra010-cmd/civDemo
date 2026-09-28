using System.Reflection;
using System.Xml.Linq;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.Render;
using Sim.Ui.Trees;
using Sim.Ui.Trees.View;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// THE TREES / AGES SCREENS — the task's §27 test list, headless, over the draw list and
/// the hit regions the game itself consumes (docs/architecture/the-trees-ui.md §10).
/// </summary>
public class TreesScreenTests
{
    private const double W = 1280, H = 800;

    private static TreesHost Host() => new(TreesContentLoader.DefaultDirectory);

    private static TreesFrame Frame(TreesHost h, double now = 0.0) => h.Frame(W, H, now, ApproxTextMeasure.Instance);

    private static void Act(TreesHost h, TreesAction a, double now = 0.0) => h.Handle(a, Frame(h, now));

    private static IEnumerable<string> Texts(TreesFrame f) => f.Draw.Commands.OfType<TextCmd>().Select(t => t.Text);

    private static int Node(TreesHost h, string id) => h.Graph!.IndexOf(id);

    private static RectD CardHit(TreesFrame f, int node) =>
        f.Hits.Last(x => x.Action is SelectNodeAction s && s.Node == node).Rect;

    private static GlyphSpec CardGlyph(TreesFrame f, int node)
    {
        RectD r = CardHit(f, node);
        return f.Draw.Commands.OfType<GlyphCmd>().Single(g => r.Contains(g.X + g.Size / 2, g.Y + g.Size / 2)).Spec;
    }

    private static void Click(TreesHost h, TreesFrame f, double x, double y)
    {
        TreesAction? a = f.HitAt(x, y);
        Assert.NotNull(a);
        h.Handle(a!, f);
    }

    // --- all seven domains render ------------------------------------------------------------

    [Fact]
    public void AllSevenLenses_RenderInTheNavigationAndAsBandsOfOneCanvas()
    {
        TreesHost h = Host();
        TreesFrame f = Frame(h);
        string[] texts = Texts(f).ToArray();
        foreach (LensDef l in h.Content!.Trees.Lenses)
        {
            Assert.Contains(l.Name, texts);                          // navigation row
            Assert.Contains(l.Name.ToUpperInvariant(), texts);       // band header on the canvas
            Assert.Contains(f.Hits, x => x.Action is SetLensAction s && s.LensId == l.Id);
        }
        Assert.Contains(f.Hits, x => x.Action is SetLensAction { LensId: null });   // the whole civilization
        Assert.Equal(7, h.Layout!.Bands.Count);
    }

    [Fact]
    public void EveryNode_IsGeneratedFromData_AsACardWithItsGlyph_WhenTheGraphIsFitted()
    {
        TreesHost h = Host();
        Act(h, new FitAction());
        TreesFrame f = Frame(h);
        for (int i = 0; i < h.Graph!.Count; i++)
        {
            Assert.Contains(f.Hits, x => x.Action is SelectNodeAction s && s.Node == i);
            GlyphSpec g = CardGlyph(f, i);
            Assert.Equal(h.Graph.Node(i).Base, g.Base);
        }
    }

    // --- typed and cross-lens relationships render --------------------------------------------

    [Fact]
    public void EveryRelationship_IsDrawn_AndCrossLensEdgesConnectDifferentBands()
    {
        TreesHost h = Host();
        Act(h, new FitAction());
        TreesFrame f = Frame(h);
        BezierCmd[] curves = f.Draw.Commands.OfType<BezierCmd>().ToArray();
        Assert.Equal(h.Graph!.Edges.Count, curves.Length);
        GraphCamera cam = h.Ui.Camera;
        RectD canvas = f.Layout.Canvas;
        int crossDrawn = 0;
        foreach (GraphEdge e in h.Graph.Edges.Where(h.Graph.IsCrossLens))
        {
            NodeBox a = h.Layout!.Box(e.FlowFrom), b = h.Layout.Box(e.FlowTo);
            (double _, double ay) = cam.WorldToScreen(a.X, a.CenterY, canvas);
            (double _, double by) = cam.WorldToScreen(b.X, b.CenterY, canvas);
            if (curves.Any(c => System.Math.Abs(c.P0.Y - ay) < 0.5 && System.Math.Abs(c.P3.Y - by) < 0.5)) crossDrawn++;
            Assert.NotEqual(a.Band, b.Band);
        }
        Assert.True(crossDrawn >= 10, $"only {crossDrawn} cross-lens edges found drawn between their bands");
        // Each kind draws in its own ink and stroke: the dotted diffusion pathway is River blue.
        GraphEdge diffusion = h.Graph.Edges.Single(e => e.Kind.Id == "diffusesTo");
        Assert.Contains(curves, c => c.Color.R == Sim.Ui.Art.ParchmentPalette.River.R && c.Dash is not null);
        Assert.False(diffusion.Kind.Layering);
    }

    // --- node state changes render correctly -------------------------------------------------------

    [Fact]
    public void NodeStateChanges_ChangeTheGlyph_RingForResearch_FillAndPipsForRealization()
    {
        TreesHost h = Host();
        Act(h, new FitAction());
        int eu = Node(h, "institution.engineering-university");

        GlyphSpec s0 = CardGlyph(Frame(h, 0.0), eu);                // step 0: researching 45 %
        Assert.Equal(GlyphState.InProgress, s0.State);
        Assert.Equal(NodeVisuals.Quantise(0.45), s0.Progress);
        Assert.Equal(GlyphBase.Portico, s0.Base);
        Assert.Equal(GlyphDomain.Dividers, s0.Domain);

        h.Demo!.SetStep(1);                                          // researched: heavy ring, nothing realized yet
        GlyphSpec s1 = CardGlyph(Frame(h, 1.0), eu);
        Assert.Equal(GlyphState.Complete, s1.State);
        Assert.Equal(0.0, s1.Maturity);
        Assert.Equal(0, s1.Stage);

        h.Demo.SetStep(2);                                           // developing: stage 1, fill = realization
        GlyphSpec s2 = CardGlyph(Frame(h, 2.0), eu);
        Assert.Equal(GlyphState.Complete, s2.State);
        Assert.Equal(1, s2.Stage);
        Assert.Equal(NodeVisuals.Quantise(0.2), s2.Maturity);

        // And every configured state maps through its own content definition.
        TreesDocument c = h.Content!.Trees;
        NodeDef def = c.Nodes.First(n => n.Id == "institution.university");
        foreach (string sid in c.StatesFor(def.Type))
        {
            StateDef st = c.State(sid);
            GlyphSpec g = NodeVisuals.Glyph(def, st, new NodeStatus(def.Id, sid, 0.5, null, 0.5, ""), SizeClass.Px48);
            Assert.Equal(st.GlyphState, g.State);
            Assert.Equal(st.Stage, g.Stage);
        }
    }

    // --- placeholder Ages and milestone progress render -------------------------------------------

    [Fact]
    public void PlaceholderAges_Render_WithoutHistoricalNames()
    {
        TreesHost h = Host();
        Act(h, new SetTabAction(TreesTab.Ages));
        TreesFrame f = Frame(h);
        string[] texts = Texts(f).ToArray();
        foreach (AgeDef a in h.Content!.Ages.Ages) Assert.Contains(a.DisplayName, texts);
        Assert.Contains("AGE 03", texts);
        Assert.Contains("PLACEHOLDER AGE", texts);
        Assert.Contains(texts, t => t.StartsWith("Next transition: Age 04", StringComparison.Ordinal));
        string all = string.Join(" | ", texts);
        foreach (string historical in new[] { "Stone", "Bronze", "Iron", "Classical", "Medieval", "Renaissance", "Industrial", "Modern", "Atomic", "Information" })
            Assert.DoesNotContain(historical, all);
    }

    [Fact]
    public void MilestoneProgress_Renders_AsReported_AndFollowsTheSource()
    {
        TreesHost h = Host();
        Act(h, new SetTabAction(TreesTab.Ages));
        string[] t0 = Texts(Frame(h)).ToArray();
        Assert.Contains("72 %", t0);
        Assert.Contains("Mandatory milestones · 2/4".ToUpperInvariant(), t0);
        Assert.Contains("Supporting milestones · 0/1".ToUpperInvariant(), t0);
        Assert.Contains("50 %", t0);                                   // the partial milestone
        h.Demo!.SetStep(1);
        string[] t1 = Texts(Frame(h, 1.0)).ToArray();
        Assert.Contains("86 %", t1);
        Assert.Contains("Mandatory milestones · 3/4".ToUpperInvariant(), t1);
        h.Demo.SetStep(2);
        string[] t2 = Texts(Frame(h, 2.0)).ToArray();
        Assert.Contains("TRANSITION PENDING → AGE 04", t2);
        // Selecting a milestone shows its checklist entry; its node link opens the Trees.
        Act(h, new SelectMilestoneAction("M_03_03"), 2.0);
        TreesFrame f = Frame(h, 2.0);
        Assert.Contains("Milestone 03.03", Texts(f));
        Hit link = f.Hits.Single(x => x.Action is OpenNodeAction o && o.NodeId == "institution.engineering-university");
        Click(h, f, link.Rect.CenterX, link.Rect.CenterY);
        Assert.Equal(TreesTab.Trees, h.Ui.Tab);
        Assert.Equal(Node(h, "institution.engineering-university"), h.Ui.Selected);
    }

    [Fact]
    public void Ages_MoveForwardOnly_ARegressionReportIsRefusedVisibly()
    {
        TreesContentSet c = TreesContentTests.Shipped();
        var guard = new AgeForwardGuard();
        guard.Observe(new AgeStateSnapshot(1, "t", true, "AGE_03", 0.5, null, []), c.Ages);
        guard.Observe(new AgeStateSnapshot(2, "t", true, "AGE_02", 0.9, null, []), c.Ages);
        Assert.Equal("AGE_03", guard.DisplayedAgeId);
        Assert.True(guard.RegressionReported);
        guard.Observe(new AgeStateSnapshot(3, "t", true, "AGE_04", 0.0, null, []), c.Ages);
        Assert.Equal("AGE_04", guard.DisplayedAgeId);
        Assert.False(guard.RegressionReported);
    }

    // --- filtering works -----------------------------------------------------------------------------

    [Fact]
    public void Filtering_LensStateAgeTypeSearchAndFocus_EmphasiseExactlyTheRightNodes()
    {
        TreesHost h = Host();
        TreeGraph g = h.Graph!;
        TreesStateSnapshot s = h.TreesSource!.Current;
        AgesDocument ages = h.Content!.Ages;
        TreesView V(TreesUiState ui) => TreesQuery.Evaluate(g, s, ui, ages);
        Emphasis E(TreesView v, string id) => v.Nodes[g.IndexOf(id)].Emphasis;

        // Lens: members normal (including a node that is only ALSO in the lens), neighbours context, the rest dimmed or hidden.
        var lens = new TreesUiState { Lens = "INDUSTRY" };
        TreesView lv = V(lens);
        Assert.Equal(Emphasis.Normal, E(lv, "industry.factory"));
        Assert.Equal(Emphasis.Normal, E(lv, "capability.engineering-personnel"));   // alsoIn INDUSTRY
        Assert.Equal(Emphasis.Context, E(lv, "knowledge.engineering-knowledge"));    // feeds Machine Tools
        Assert.Equal(Emphasis.Dimmed, E(lv, "unit.archer"));
        lens.FilterMode = FilterMode.Hide;
        Assert.Equal(Emphasis.Hidden, E(V(lens), "unit.archer"));

        // State filter.
        var state = new TreesUiState();
        state.StateFilter.Add("researched");
        TreesView sv = V(state);
        foreach (NodeView n in sv.Nodes)
            Assert.Equal(n.State.Id == "researched" ? Emphasis.Normal : Emphasis.Dimmed, n.Emphasis);

        // Age filter: open-ended ranges include later Ages; a closed range does not.
        TreesView av = V(new TreesUiState { AgeFilter = "AGE_05" });
        Assert.Equal(Emphasis.Normal, E(av, "application.automobile"));
        Assert.Equal(Emphasis.Normal, E(av, "knowledge.mathematics"));   // AGE_01 onward
        Assert.Equal(Emphasis.Dimmed, E(av, "milestone.m-04-01"));        // AGE_04 only
        Assert.Equal(Emphasis.Dimmed, E(V(new TreesUiState { AgeFilter = "AGE_00" }), "knowledge.mathematics"));

        // Type filter.
        TreesView tv = V(new TreesUiState { TypeFilter = "unit" });
        Assert.Equal(3, tv.Nodes.Count(n => n.Emphasis == Emphasis.Normal));

        // Search: case-insensitive on name or id.
        TreesView qv = V(new TreesUiState { Search = "ENGIN" });
        Assert.Equal(5, qv.MatchCount);
        Assert.True(qv.Nodes[g.IndexOf("application.steam-engine")].SearchMatch);
        Assert.Equal(Emphasis.Dimmed, E(qv, "knowledge.metallurgy"));

        // Focus: prerequisites / downstream of the selection.
        int eu = g.IndexOf("institution.engineering-university");
        TreesView pre = V(new TreesUiState { Selected = eu, Focus = FocusMode.Prerequisites });
        Assert.True(pre.Nodes[g.IndexOf("knowledge.mathematics")].InFocus);
        Assert.Equal(Emphasis.Dimmed, E(pre, "application.automobile"));
        TreesView down = V(new TreesUiState { Selected = eu, Focus = FocusMode.Downstream });
        Assert.True(down.Nodes[g.IndexOf("application.automobile")].InFocus);
        Assert.Equal(Emphasis.Dimmed, E(down, "knowledge.mathematics"));
        Assert.Contains(pre.Edges, e => e.OnPath);
    }

    [Fact]
    public void Filtering_ThroughTheScreen_LegendTogglesAStateFilter_AndClearResetsIt()
    {
        TreesHost h = Host();
        TreesFrame f = Frame(h);
        Hit legendRow = f.Hits.Single(x => x.Action is ToggleStateFilterAction t && t.StateId == "mature");
        Click(h, f, legendRow.Rect.CenterX, legendRow.Rect.CenterY);
        Assert.Contains("mature", h.Ui.StateFilter);
        TreesFrame f2 = Frame(h);
        Assert.Contains(f2.View!.Nodes, n => n.Emphasis == Emphasis.Dimmed);
        Assert.All(f2.View.Nodes.Where(n => n.State.Id == "mature"), n => Assert.Equal(Emphasis.Normal, n.Emphasis));
        Act(h, new ClearFiltersAction());
        Assert.Empty(h.Ui.StateFilter);
    }

    // --- selection and the detail panel ---------------------------------------------------------------

    [Fact]
    public void Selection_ClickingACard_OpensItsDetails_AndThePrerequisiteViewIsNavigable()
    {
        TreesHost h = Host();
        TreesFrame f = Frame(h);
        int eu = Node(h, "institution.engineering-university");
        RectD card = CardHit(f, eu);
        Click(h, f, card.CenterX, card.CenterY);
        Assert.Equal(eu, h.Ui.Selected);

        TreesFrame d = Frame(h);
        string[] texts = Texts(d).ToArray();
        Assert.Contains("Engineering University", texts);
        Assert.Contains("WHY — WHAT PRECEDES IT", texts);
        Assert.Contains("Engineering", texts);
        Assert.Contains("University", texts);
        Assert.Contains(texts, t => t.StartsWith("Availability rule: TBD", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("cost TBD", StringComparison.Ordinal));

        // The prerequisite row selects that node.
        Hit pre = d.Hits.Last(x => x.Action is SelectNodeAction s && s.Node == Node(h, "knowledge.engineering") && s.Center);
        Click(h, d, pre.Rect.CenterX, pre.Rect.CenterY);
        Assert.Equal(Node(h, "knowledge.engineering"), h.Ui.Selected);

        // An empty stretch of canvas clears the selection.
        TreesFrame e = Frame(h);
        RectD canvas = e.Layout.Canvas;
        double ex = canvas.Right - 3, ey = canvas.Bottom - 3;
        Assert.IsType<SelectNodeAction>(e.HitAt(ex, ey));
        Click(h, e, ex, ey);
        Assert.Null(h.Ui.Selected);
    }

    [Fact]
    public void Hits_PanelsSitAboveTheCanvas_AndHostActionsReachTheHost()
    {
        TreesHost h = Host();
        Act(h, new ZoomAction(2.0));
        TreesFrame f = Frame(h);
        // Nothing on the canvas can be clicked through the navigation column.
        for (double y = f.Layout.Nav.Y + 5; y < f.Layout.Nav.Bottom; y += 7)
            Assert.IsNotType<SelectNodeAction>(f.HitAt(f.Layout.Nav.X + 90, y));
        // Two header regions open the Age view: the Age chip and the tab. Click the tab.
        Assert.Equal(2, f.Hits.Count(x => x.Action is SetTabAction { Tab: TreesTab.Ages } && x.Rect.Y < 58));
        Hit ages = f.Hits.Single(x => x.Action is SetTabAction { Tab: TreesTab.Ages } && x.Rect.Y < 58 && x.Rect.X > W / 2);
        Click(h, f, ages.Rect.CenterX, ages.Rect.CenterY);
        Assert.Equal(TreesTab.Ages, h.Ui.Tab);
        TreesFrame g = Frame(h);
        Hit close = g.Hits.Single(x => x.Action is CloseAction);
        Click(h, g, close.Rect.CenterX, close.Rect.CenterY);
        Assert.True(h.CloseRequested);
    }

    [Fact]
    public void BrokenContent_ShowsTheDiagnostics_InsteadOfCrashing()
    {
        string dir = Path.Combine(Path.GetTempPath(), "trees-bad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (string file in new[] { TreesContentLoader.AgesFile, TreesContentLoader.GalleryFile, TreesContentLoader.AnimationsFile })
                File.Copy(Path.Combine(TreesContentLoader.DefaultDirectory, file), Path.Combine(dir, file));
            File.WriteAllText(Path.Combine(dir, TreesContentLoader.TreesFile), "{ \"schema\": \"civ-sim/the-trees@1\", \"lenses\": [ ");
            var h = new TreesHost(dir);
            Assert.False(h.Loaded);
            string[] texts = Texts(Frame(h)).ToArray();
            Assert.Contains("THE TREES — CONTENT DID NOT LOAD", texts);
            Assert.Contains(texts, t => t.Contains(TreesContentLoader.TreesFile, StringComparison.Ordinal));
        }
        finally { Directory.Delete(dir, true); }
    }

    // --- determinism ------------------------------------------------------------------------------------

    [Fact]
    public void Frames_AreDeterministic_TheSameScenarioWritesByteIdenticalWellFormedSvg()
    {
        foreach (TreesPreview.Scenario s in TreesPreview.Scenarios)
        {
            string a = SvgWriter.Write(s.Build(Host()).Frame.Draw, W, H);
            string b = SvgWriter.Write(s.Build(Host()).Frame.Draw, W, H);
            Assert.Equal(a, b);
            XDocument doc = XDocument.Parse(a);
            Assert.Equal("svg", doc.Root!.Name.LocalName);
        }
    }

    // --- animations are state-driven, not authoritative ----------------------------------------------------

    [Fact]
    public void Animations_AreDrivenByReportedStateChanges_AndEnd()
    {
        TreesHost h = Host();
        string eu = "institution.engineering-university";
        Frame(h, 10.0);                                              // baseline: nothing replays
        NodeMotion baseline = h.Animator!.Node(eu, 10.5);
        Assert.Equal(-1.0, baseline.OneShotT);
        Assert.True(baseline.HaloAlpha >= 0.0);                      // researching: the steady pulse only
        Assert.Equal(NodeMotion.Still, h.Animator.Node("knowledge.mathematics", 10.5));

        h.Demo!.Next();                                              // the source reports research complete
        Frame(h, 20.0);
        NodeMotion flash = h.Animator.Node(eu, 20.4);
        Assert.Equal(AnimationKind.FlashRing, flash.OneShot);
        Assert.Equal(0.4 / 1.6, flash.OneShotT, 12);
        Assert.True(flash.NewlyCompleted);
        Assert.Equal(0.0, flash.HaloAlpha);                          // researched does not pulse
        NodeMotion settled = h.Animator.Node(eu, 20.0 + StateAnimator.NewlyWindowSeconds + 1);
        Assert.Equal(NodeMotion.Still, settled);
        Assert.True(h.Animator.Node("capability.engineering-personnel", 20.3).NewlyDiscovered);

        // Re-observing the same snapshot changes nothing.
        Frame(h, 21.0);
        Assert.Equal(flash, h.Animator.Node(eu, 20.4));
    }

    [Fact]
    public void Animations_AreNotAuthoritative_TimeChangesOnlyTheOverlays_NeverAStateOrAGlyph()
    {
        TreesHost h = Host();
        Act(h, new FitAction());
        Frame(h, 0.0);
        h.Demo!.Next();
        TreesStateSnapshot before = h.TreesSource!.Current;
        GlyphCmd[]? reference = null;
        string[]? states = null;
        foreach (double t in new[] { 0.05, 0.3, 0.9, 1.7, 5.0, 30.0 })
        {
            TreesFrame f = Frame(h, t);
            GlyphCmd[] glyphs = f.Draw.Commands.OfType<GlyphCmd>().ToArray();
            string[] st = f.View!.Nodes.Select(n => n.State.Id).ToArray();
            reference ??= glyphs;
            states ??= st;
            Assert.Equal(reference.Select(g => g.Spec), glyphs.Select(g => g.Spec));
            Assert.Equal(states, st);
        }
        Assert.Same(before, h.TreesSource.Current);
        Assert.Equal(before.Nodes, h.TreesSource.Current.Nodes);
    }

    // --- the read-only boundary ------------------------------------------------------------------------------

    [Fact]
    public void ReadOnlyBoundary_TheTreesNamespacesNeverTouchASimCoreType_AndSourcesHaveNoWriters()
    {
        Assembly core = typeof(Sim.Core.State.WorldState).Assembly;
        int members = 0;
        foreach (Type t in typeof(TreesHost).Assembly.GetTypes())
        {
            if (t.Namespace is null || !(t.Namespace == "Sim.Ui.Trees" || t.Namespace.StartsWith("Sim.Ui.Trees.", StringComparison.Ordinal)
                || t.Namespace == "Sim.Ui.Render")) continue;
            foreach (MemberInfo m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                members++;
                IEnumerable<Type> touched = m switch
                {
                    MethodInfo mi => mi.GetParameters().Select(p => p.ParameterType).Append(mi.ReturnType),
                    ConstructorInfo ci => ci.GetParameters().Select(p => p.ParameterType),
                    PropertyInfo pi => [pi.PropertyType],
                    FieldInfo fi => [fi.FieldType],
                    _ => [],
                };
                foreach (Type x in touched)
                    Assert.False(Flatten(x).Any(y => y.Assembly == core), $"{t.Name}.{m.Name} touches a Sim.Core type");
            }
        }
        Assert.True(members > 200, $"only {members} members inspected — vacuous");

        foreach (Type src in new[] { typeof(ITreesStateSource), typeof(IAgeStateSource), typeof(IGalleryStateSource) })
        {
            MemberInfo[] ms = src.GetMembers();
            PropertyInfo only = Assert.Single(src.GetProperties());
            Assert.False(only.CanWrite, $"{src.Name}.{only.Name} is writable");
            Assert.All(src.GetMethods(), mi => Assert.True(mi.IsSpecialName, $"{src.Name}.{mi.Name} is a command on a read-only source"));
        }
    }

    private static IEnumerable<Type> Flatten(Type t)
    {
        Type inner = t.IsByRef || t.IsArray || t.IsPointer ? t.GetElementType()! : t;
        yield return inner;
        if (inner.IsGenericType) foreach (Type a in inner.GetGenericArguments()) foreach (Type y in Flatten(a)) yield return y;
    }

    // --- the camera -----------------------------------------------------------------------------------------

    [Fact]
    public void Camera_ZoomKeepsThePointUnderTheCursor_AndClampsTheZoom()
    {
        var cam = new GraphCamera();
        var vp = new RectD(100, 50, 800, 600);
        cam.Set(1.0, 40, 30);
        (double wx, double wy) = cam.ScreenToWorld(420, 333, vp);
        cam.ZoomAt(420, 333, vp, 1.7);
        (double sx, double sy) = cam.WorldToScreen(wx, wy, vp);
        Assert.Equal(420, sx, 9);
        Assert.Equal(333, sy, 9);
        for (int i = 0; i < 40; i++) cam.ZoomAt(400, 300, vp, 1.5);
        Assert.Equal(GraphCamera.MaxZoom, cam.Zoom);
        for (int i = 0; i < 80; i++) cam.ZoomAt(400, 300, vp, 0.5);
        Assert.Equal(GraphCamera.MinZoom, cam.Zoom);
        cam.Pan(50, -20);
        (double px, double py) = cam.WorldToScreen(0, 0, vp);
        cam.Pan(-50, 20);
        (double qx, double qy) = cam.WorldToScreen(0, 0, vp);
        Assert.Equal(px - 50, qx, 9);
        Assert.Equal(py + 20, qy, 9);
    }
}
