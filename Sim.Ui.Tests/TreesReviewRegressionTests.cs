using System.Text.Json.Nodes;
using Sim.Ui.Render;
using Sim.Ui.Trees;
using Sim.Ui.Trees.View;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// Regressions from the adversarial review of the Trees/Ages foundation (workflow
/// wf_40fe2a9f-261, pinned to 310f831). Each test reproduces a reported defect: the five
/// the independent verifiers CONFIRMED, and the unverified ones this session measured
/// failing before fixing them (CLAUDE.md: no finding is actionable on the finder's word).
/// </summary>
public class TreesReviewRegressionTests
{
    private const double W = 1280, H = 800;
    private static TreesHost Host() => new(TreesContentLoader.DefaultDirectory);
    private static TreesFrame Frame(TreesHost h, double now = 0.0, double height = H) => h.Frame(W, height, now, ApproxTextMeasure.Instance);
    private static void Act(TreesHost h, TreesAction a, double now = 0.0) => h.Handle(a, Frame(h, now));

    // --- confirmed (high): the dash walker must terminate --------------------------------------------

    [Fact]
    public void Dashes_Terminate_ForTheLegendLine_ThatFrozeTheGame()
    {
        // The exact case the verifier traced: a dotted legend line, 26 px at width 1.6
        // (on 2.24, off 5.12). The old loop stalled at t = 16.96 with a 1.3e-15 step.
        var line = new (double, double)[] { (0, 0), (26, 0) };
        var d = StrokeGeometry.Dashes(line, 1.4 * 1.6, 3.2 * 1.6);
        Assert.InRange(d.Count, 3, 5);
        Assert.All(d, seg => Assert.True(seg.B.X > seg.A.X && seg.B.X <= 26.0 + 1e-9));
    }

    [Fact]
    public void Dashes_Terminate_ForEveryDashedStrokeTheScreensPaint_AcrossZoomAndPan()
    {
        // Every dashed line and curve in every preview scenario, then the Trees canvas over a
        // zoom × pan sweep (the verifier found 210 of 712 stalling there). The walker is also
        // bounded by MaxSteps, so the assertion is on the step economy: dashes, not the cap.
        int checkedStrokes = 0;
        void CheckFrame(TreesFrame f)
        {
            foreach (DrawCmd c in f.Draw.Commands)
            {
                (double X, double Y)[]? pts = null; (double On, double Off)? dash = null;
                if (c is LineCmd l && l.Dash is not null) { pts = [(l.X0, l.Y0), (l.X1, l.Y1)]; dash = l.Dash; }
                if (c is BezierCmd b && b.Dash is not null)
                {
                    pts = new (double, double)[25];
                    for (int i = 0; i <= 24; i++) pts[i] = StrokeGeometry.BezierAt(b.P0, b.P1, b.P2, b.P3, i / 24.0);
                    dash = b.Dash;
                }
                if (pts is null) continue;
                double len = 0;
                for (int i = 1; i < pts.Length; i++) len += System.Math.Sqrt(System.Math.Pow(pts[i].X - pts[i - 1].X, 2) + System.Math.Pow(pts[i].Y - pts[i - 1].Y, 2));
                var segs = StrokeGeometry.Dashes(pts, dash!.Value.On, dash.Value.Off);
                // One segment per dash, plus splits at polyline vertices, plus at most one
                // sub-pixel sliver per dash boundary from the MinStep floor: 2x is a
                // "no runaway" bound (the old loop never returned at all).
                int bound = 2 * ((int)(len / (dash.Value.On + dash.Value.Off)) + pts.Length) + 4;
                Assert.InRange(segs.Count, 0, bound);
                checkedStrokes++;
            }
        }
        foreach (TreesPreview.Scenario sc in TreesPreview.Scenarios) CheckFrame(sc.Build(Host()).Frame);
        TreesHost h = Host();
        foreach (double z in new[] { 0.3, 0.45, 0.55, 0.8, 1.0, 1.35, 1.9, 2.4 })
            foreach ((double ox, double oy) in new[] { (0.0, 0.0), (137.3, 41.7), (611.1, 250.9), (-80.5, -33.3) })
            {
                h.Ui.Camera.Set(z, ox, oy);
                CheckFrame(Frame(h));
            }
        Assert.True(checkedStrokes > 500, $"only {checkedStrokes} dashed strokes checked — vacuous");
    }

    [Fact]
    public void Dashes_AreBounded_ForPathologicalInput()
    {
        Assert.Empty(StrokeGeometry.Dashes([(0, 0), (10, 0)], 0, 1));
        Assert.Empty(StrokeGeometry.Dashes([(0, 0), (10, 0)], double.NaN, 1));
        Assert.Empty(StrokeGeometry.Dashes([(0, 0)], 1, 1));
        // A segment so long that one MinStep is below its ulp still terminates, capped.
        var huge = StrokeGeometry.Dashes([(0, 0), (1e13, 0)], 1e-7, 1e-7);
        Assert.InRange(huge.Count, 1, StrokeGeometry.MaxSteps);
    }

    [Fact]
    public void Polygons_AreHandedToTheBackendClockwise()
    {
        (double, double)[] ccw = [(0, 0), (0, 10), (10, 10)];   // counter-clockwise on screen (y down)
        Assert.True(StrokeGeometry.SignedArea2(ccw) < 0);
        Assert.True(StrokeGeometry.SignedArea2(StrokeGeometry.Clockwise(ccw)) > 0);
        (double, double)[] cw = [(0, 0), (10, 0), (10, 10)];
        Assert.Equal(cw, StrokeGeometry.Clockwise(cw));
    }

    // --- confirmed: preview stepping must not trip the forward-only guard ------------------------

    [Fact]
    public void PreviewStepBack_ReBaselinesTheAgeGuard_TheAgeViewFollowsTheChosenStep()
    {
        TreesHost h = Host();
        for (int i = 0; i < 3; i++) Act(h, new PreviewStepAction(+1), i);     // step 3: AGE_04
        Assert.Equal("AGE_04", h.AgeSource!.Current.CurrentAgeId);
        Act(h, new PreviewStepAction(-1), 4);                                  // step 2: AGE_03 again
        TreesFrame f = Frame(h, 5);
        Assert.False(h.AgeGuard.RegressionReported, "a preview jump is a chosen snapshot, not a regression report");
        Assert.Contains("AGE 03", f.Draw.Commands.OfType<TextCmd>().Select(t => t.Text));
    }

    // --- confirmed: content that must be diagnosed, not crash --------------------------------------

    [Fact]
    public void AMilestoneListedByNoAgeAndWithoutAnAge_IsAnError_NotACrash()
    {
        ContentLoadResult r = TreesContentTests.LoadEdited(_ => { }, a =>
        {
            a["milestones"]!.AsArray().Add(new JsonObject
            {
                ["id"] = "M_ORPHAN", ["name"] = "Orphan", ["category"] = "systemic",
                ["nodeRefs"] = new JsonArray("knowledge.mathematics"),
            });
        });
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.Message.Contains("M_ORPHAN"));
    }

    [Fact]
    public void ANullListElement_IsADiagnostic_NotACrash()
    {
        ContentLoadResult r = TreesContentTests.LoadEdited(t => t["nodes"]![0]!["alsoIn"] = new JsonArray((JsonNode?)null));
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.Path.Contains("nodes[0].alsoIn") && e.Message.Contains("null"));
        ContentLoadResult r2 = TreesContentTests.LoadEdited(t => t["nodes"]!.AsArray().Add((JsonNode?)null));
        Assert.False(r2.Ok);
    }

    [Fact]
    public void AnUnboundedColumnHint_IsAnError_NotAnOverflow()
    {
        ContentLoadResult r = TreesContentTests.LoadEdited(t => t["nodes"]![0]!["column"] = int.MaxValue);
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.Path == "nodes[0].column");
    }

    // --- measured here: the details panel scrolls as one body ---------------------------------------

    [Fact]
    public void DetailsScroll_MovesTheWholeBody_AndTheContentHeightDoesNotGrow()
    {
        TreesHost h = Host();
        Act(h, new SelectNodeAction(h.Graph!.IndexOf("institution.engineering-university")));
        TreesFrame f0 = Frame(h, 0, 600);
        double y0 = IdTextY(f0, "institution.engineering-university");
        h.ScrollDetails(120, f0);
        TreesFrame f1 = Frame(h, 0, 600);
        Assert.Equal(y0 - h.Ui.DetailScroll, IdTextY(f1, "institution.engineering-university"), 6);
        Assert.Equal(f0.DetailContentHeight, f1.DetailContentHeight, 6);
    }

    private static double IdTextY(TreesFrame f, string id) => f.Draw.Commands.OfType<TextCmd>().Single(t => t.Text == id).Y;

    // --- measured here: filters and buttons do what they say ------------------------------------------

    [Fact]
    public void Clear_AlsoClearsAFocusedLens_SoAnEnabledClearAlwaysDoesSomething()
    {
        TreesHost h = Host();
        Act(h, new SetLensAction("INDUSTRY"));
        Act(h, new ClearFiltersAction());
        Assert.Null(h.Ui.Lens);
    }

    [Fact]
    public void HideMode_NeverDrawsAPathEdgeFromAHiddenNode()
    {
        TreesHost h = Host();
        TreeGraph g = h.Graph!;
        var ui = new TreesUiState { FilterMode = FilterMode.Hide, TypeFilter = "institution", Selected = g.IndexOf("institution.engineering-university") };
        TreesView v = TreesQuery.Evaluate(g, h.TreesSource!.Current, ui, h.Content!.Ages);
        foreach (EdgeView e in v.Edges)
            if (v.Nodes[e.Edge.From].Emphasis == Emphasis.Hidden || v.Nodes[e.Edge.To].Emphasis == Emphasis.Hidden)
                Assert.Equal(Emphasis.Hidden, e.Emphasis);
    }

    [Fact]
    public void EmptyNodeTypes_AreAnError_NotAFrameCrash()
    {
        ContentLoadResult r = TreesContentTests.LoadEdited(t =>
        {
            t["nodeTypes"] = new JsonArray();
            t["nodes"] = new JsonArray();
            t["edges"] = new JsonArray();
        }, a => { foreach (JsonNode? m in a["milestones"]!.AsArray()) m!["nodeRefs"] = new JsonArray(); });
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.Path == "nodeTypes");
    }

    // --- measured here: strict content means strict --------------------------------------------------

    [Fact]
    public void NumericAndCommaCombinedEnumValues_AreRejected()
    {
        Assert.False(TreesContentTests.LoadEdited(t => t["relationKinds"]![0]!["flow"] = "1").Ok);
        Assert.False(TreesContentTests.LoadEdited(t => t["states"]![0]!["track"] = "2").Ok);
        Assert.False(TreesContentTests.LoadEdited(t => t["nodeTypes"]![0]!["mark"] = "Agriculture, Craft").Ok);
        Assert.False(TreesContentTests.LoadEdited(t => t["states"]![0]!["glyphState"] = "Available, InProgress").Ok);
    }

    [Fact]
    public void ADuplicateJsonKey_IsRejected()
    {
        string trees = TreesContentTests.ReadShipped(TreesContentLoader.TreesFile)
            .Replace("\"prerequisites\": [\"knowledge.mathematics\"], \"placeholder\": true },",
                     "\"prerequisites\": [\"knowledge.mathematics\"], \"prerequisites\": [\"knowledge.metallurgy\"], \"placeholder\": true },");
        Assert.Contains("\"prerequisites\": [\"knowledge.metallurgy\"]", trees);
        ContentLoadResult r = TreesContentLoader.LoadFromJson(trees, TreesContentTests.ReadShipped(TreesContentLoader.AgesFile),
            TreesContentTests.ReadShipped(TreesContentLoader.GalleryFile), TreesContentTests.ReadShipped(TreesContentLoader.AnimationsFile));
        Assert.False(r.Ok);
    }

    [Fact]
    public void DuplicateAlsoInLenses_AreRejected()
    {
        ContentLoadResult r = TreesContentTests.LoadEdited(t => t["nodes"]![0]!["alsoIn"] = new JsonArray("INDUSTRY", "INDUSTRY"));
        Assert.False(r.Ok);
    }

    [Fact]
    public void AnimationTargets_ThatNameNoStateOrKind_AreWarned()
    {
        JsonNode anims = JsonNode.Parse(TreesContentTests.ReadShipped(TreesContentLoader.AnimationsFile))!;
        anims["animations"]![0]!["appliesTo"] = "node-state:researchng";
        ContentLoadResult r = TreesContentLoader.LoadFromJson(TreesContentTests.ReadShipped(TreesContentLoader.TreesFile),
            TreesContentTests.ReadShipped(TreesContentLoader.AgesFile), TreesContentTests.ReadShipped(TreesContentLoader.GalleryFile), anims.ToJsonString());
        Assert.Contains(r.Diagnostics, d => d.Message.Contains("researchng"));
    }

    [Fact]
    public void ADemoStepAgesBlockWithoutProgress_KeepsThePreviousProgress()
    {
        TreesContentSet c = TreesContentTests.Shipped();
        JsonNode s = JsonNode.Parse(TreesContentTests.ReadShipped(TreesContentLoader.DemoStateFile))!;
        s["steps"]![2]!["ages"]!.AsObject().Remove("progress");
        (DemoStateSource? src, IReadOnlyList<ContentDiagnostic> d) = DemoStateSource.Load(s.ToJsonString(), c);
        Assert.NotNull(src);
        src!.SetStep(2);
        Assert.Equal(0.86, src.Ages.CurrentProgress);
    }

    // --- measured here: the NEW / FOUND markers follow roles, not hard-coded state ids ----------------

    [Fact]
    public void NewlyCompletedAndDiscovered_FollowTheStateRoles_NotTheirIds()
    {
        JsonNode trees = JsonNode.Parse(TreesContentTests.ReadShipped(TreesContentLoader.TreesFile))!;
        string text = trees.ToJsonString().Replace("\"researched\"", "\"known\"");
        TreesContentSet c = TreesContentLoader.LoadFromJson(text, TreesContentTests.ReadShipped(TreesContentLoader.AgesFile),
            TreesContentTests.ReadShipped(TreesContentLoader.GalleryFile),
            TreesContentTests.ReadShipped(TreesContentLoader.AnimationsFile).Replace("node-enter:researched", "node-enter:known")).Content!;
        Assert.NotNull(c);
        (DemoStateSource? s, _) = DemoStateSource.Load(TreesContentTests.ReadShipped(TreesContentLoader.DemoStateFile).Replace("\"researched\"", "\"known\""), c);
        var anim = new StateAnimator(c.Animations);
        anim.Observe(c.Trees, s!.Trees, s.Ages, s.Gallery, c.Ages, c.Gallery, 0);
        s.Next();
        anim.Observe(c.Trees, s.Trees, s.Ages, s.Gallery, c.Ages, c.Gallery, 10);
        Assert.True(anim.Node("institution.engineering-university", 10.2).NewlyCompleted);
        Assert.True(anim.Node("capability.engineering-personnel", 10.2).NewlyDiscovered);
    }

    // --- measured here: a regressed report is not paired with the later Age --------------------------

    [Fact]
    public void ARegressedReport_DoesNotLendItsProgressToTheLaterAgeShown()
    {
        TreesContentSet c = TreesContentTests.Shipped();
        var guard = new AgeForwardGuard();
        guard.Observe(new AgeStateSnapshot(1, "t", true, "AGE_04", 0.05, null, []), c.Ages);
        var regressed = new AgeStateSnapshot(2, "t", true, "AGE_03", 1.0, new AgeTransitionStatus(true, "AGE_04", ""), []);
        guard.Observe(regressed, c.Ages);
        TreesHost h = Host();
        var input = new TreesFrameInput(c, h.Graph!, h.Layout!, h.TreesSource!.Current, regressed, h.GallerySource!.Current,
            new TreesUiState(), new StateAnimator(c.Animations), 0, W, H, ApproxTextMeasure.Instance, AgeGuard: guard);
        Assert.Equal("AGE_04", input.CurrentAgeId);
        Assert.Null(input.CurrentAgeProgress);
        Assert.Null(input.CurrentTransition);
    }
}
