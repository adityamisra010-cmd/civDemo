using ImGuiNET;
using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.Art;
using Sim.Ui.Headless;
using Sim.Ui.ImGuiIntegration;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Tests;

/// <summary>
/// THE RESEARCH-SCREEN CRASH (M5 hardening H1). The Director's playtest aborted on the first click of Research:
/// <c>imgui_draw.cpp:2261 draw_list->_VtxCurrentIdx &lt; (1 &lt;&lt; 16)</c>, "Too many vertices in ImDrawList using
/// 16-bit indices". These tests run the REAL path in a REAL Dear ImGui context, headless (font atlas built on the
/// CPU, no GPU): ProgressionScreen → DrawList → <see cref="DrawListImGuiBackend"/> → ImGui's background draw list
/// → <c>ImGui.Render</c>, then read every draw list's real index buffer (<see cref="ImGuiDrawData.Check"/>).
/// The 473 UI tests that passed before the crash never created an ImGui frame. The world is the canonical founded
/// world at turn 1 (seed 42, <see cref="CanonicalTurnOneFixture"/>) — the state the playtest was in.
/// </summary>
[Collection("ImGui context")]
public class ResearchScreenRenderTests(CanonicalTurnOneFixture fx) : IClassFixture<CanonicalTurnOneFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private const int W = 1280, H = 800;   // the game's window (PanelLayout.DesignWidth × DesignHeight)

    internal static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private sealed record Sweep(int Frames, int MaxListVertices, int RebasedCommands, int OverflowFrames, int DefaultOpenVertices);

    /// <summary>Replays one tree at one Age through the real backend, frame by frame: as it opens, scrolled
    /// top to bottom at zoom 1, fitted (FIT), zoomed fully out and scrolled again, with a node hovered.
    /// Every frame's draw data must satisfy the 16-bit contract.</summary>
    private Sweep Run(HeadlessImGui gui, int age, TreeTab tab, int width = W, int height = H)
    {
        // Checked BEFORE the first frame: without the capability ImGui's assertion aborts the whole test host.
        Assert.True((ImGui.GetIO().BackendFlags & ImGuiBackendFlags.RendererHasVtxOffset) != 0, "RendererHasVtxOffset not declared");
        UiSession s = fx.Session;
        WorldState world = EraPreview.WorldAt(s.World, s.Config.Ages!, Me, age);
        var screen = new ProgressionScreen(s.Config.Research!, Me)
        {
            Theme = EraThemes.For(UiEras.Of(world, s.Config.Ages, Me)),
            Age = Sim.Ui.Ages.AgePanelModel.Build(world, s.Config.Ages, [], Me),
        };
        screen.Refresh(world);
        screen.Tab = tab;
        var backend = new DrawListImGuiBackend(gui.Fonts);
        gui.Resize(width, height);
        int frames = 0, maxV = 0, rebased = 0, overflow = 0, defaultOpen = -1;
        void Frame(Action? arrange = null)
        {
            gui.BeginFrame(default, default, 1.0 / 60);
            arrange?.Invoke();
            DrawList list = screen.Paint(width, height, backend);
            backend.Render(ImGui.GetBackgroundDrawList(), list);
            ImGuiDrawReport r = gui.EndFrame();
            Assert.True(r.Ok, $"A{age} {tab} frame {frames}: " + string.Join("; ", r.Violations));
            Assert.True(r.VtxOffsetDeclared);
            maxV = Math.Max(maxV, r.MaxListVertices);
            rebased += r.RebasedCommands;
            if (r.WouldOverflowWithoutVtxOffset) overflow++;
            if (defaultOpen < 0) defaultOpen = r.MaxListVertices;
            frames++;
        }
        Frame();   // the screen as it opens: zoom 1, the lagging branch's frontier
        screen.Camera.Set(1.0, 0, 0);
        for (int k = 0; k < 120; k++)
        {
            Frame(() => screen.ScrollBy(0, 240));
            screen.Camera.Set(screen.Camera.TargetZoom, screen.Camera.TargetPanX, screen.Camera.TargetPanY);
        }
        Frame(() => screen.FitAll());
        for (int k = 0; k < 12; k++) Frame(() => screen.WheelZoom(width * 0.3, height * 0.5, -3));
        for (int k = 0; k < 40; k++) Frame(() => { screen.ScrollBy(0, 300); screen.Advance(0.2); });
        if (screen.FrontierNode() is int f and >= 0) Frame(() => screen.Focus(f, jump: true));
        Frame(() => screen.PointerMove(width * 0.3, height * 0.5));
        return new Sweep(frames, maxV, rebased, overflow, defaultOpen);
    }

    [Fact]
    public void ResearchScreen_TechnologyAndCivics_AtA1A5A9_ScrolledThrough_StayInside16BitIndices()
    {
        using var gui = new HeadlessImGui(Assets(), W, H);
        Assert.True((ImGui.GetIO().BackendFlags & ImGuiBackendFlags.RendererHasVtxOffset) != 0);
        var a1Tech = Run(gui, 1, TreeTab.Technology);
        foreach (int age in new[] { 1, 5, 9 })
            foreach (TreeTab tab in new[] { TreeTab.Technology, TreeTab.Civics })
            {
                Sweep sw = age == 1 && tab == TreeTab.Technology ? a1Tech : Run(gui, age, tab);
                Assert.True(sw.Frames > 170);
            }

        // THE CONTROL: the A1 Technology tree really does pass 65,535 vertices in ONE draw list (here at FIT and
        // zoomed out) — the frames the pre-fix renderer could not survive — and ImGui split them into rebased
        // commands, every one of which the check above walked index by index.
        Assert.True(a1Tech.MaxListVertices > ushort.MaxValue, $"max {a1Tech.MaxListVertices}");
        Assert.True(a1Tech.OverflowFrames > 0);
        Assert.True(a1Tech.RebasedCommands > 0);
    }

    /// <summary>The window the Director most likely played in (a maximised 1920-wide window): the A1 Technology
    /// tree passes 65,535 vertices on the very frame it OPENS — the first click of Research.</summary>
    [Fact]
    public void ResearchScreen_OpenedInAWideWindow_PassesTheOld16BitLimitOnTheFirstFrame_AndRendersCorrectly()
    {
        using var gui = new HeadlessImGui(Assets(), 1920, 1009);
        Sweep sw = Run(gui, 1, TreeTab.Technology, 1920, 1009);
        Assert.True(sw.DefaultOpenVertices > ushort.MaxValue, $"default open: {sw.DefaultOpenVertices}");
        Assert.True(sw.RebasedCommands > 0);
    }

    [Fact]
    public void DrawPlan_BaseVertexIsListStartPlusVtxOffset_AndEveryIndexStaysInItsList()
    {
        using var gui = new HeadlessImGui(Assets(), 1920, 1009);
        UiSession s = fx.Session;
        var screen = new ProgressionScreen(s.Config.Research!, Me) { Theme = EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me)) };
        screen.Refresh(s.World);
        var backend = new DrawListImGuiBackend(gui.Fonts);
        Assert.True((ImGui.GetIO().BackendFlags & ImGuiBackendFlags.RendererHasVtxOffset) != 0, "RendererHasVtxOffset not declared");
        gui.BeginFrame(default, default, 1.0 / 60);
        backend.Render(ImGui.GetBackgroundDrawList(), screen.Paint(1920, 1009, backend));
        // A second, small list after the big one: its plan must start at the big list's END.
        ImGui.Begin("probe");
        ImGui.TextUnformatted("after the tree");
        ImGui.End();
        ImGuiDrawReport report = gui.EndFrame();
        Assert.True(report.Ok, string.Join("; ", report.Violations));
        Assert.True(report.RebasedCommands > 0);

        ImDrawDataPtr data = ImGui.GetDrawData();
        List<ImGuiDrawCall> plan = ImGuiDrawData.Plan(data);
        int listStart = 0, idxStart = 0, k = 0;
        unsafe
        {
            for (int l = 0; l < data.CmdListsCount; l++)
            {
                ImDrawListPtr list = data.CmdLists[l];
                ushort* idx = (ushort*)list.IdxBuffer.Data;
                for (int c = 0; c < list.CmdBuffer.Size; c++)
                {
                    ImDrawCmdPtr cmd = list.CmdBuffer[c];
                    if (cmd.ElemCount == 0) continue;
                    ImGuiDrawCall call = plan[k++];
                    Assert.Equal(listStart + (int)cmd.VtxOffset, call.BaseVertex);
                    Assert.Equal(idxStart + (int)cmd.IdxOffset, call.StartIndex);
                    Assert.Equal((int)cmd.ElemCount / 3, call.PrimitiveCount);
                    // What the GPU reads: BaseVertex + index — inside THIS list's vertices.
                    for (uint e = 0; e < cmd.ElemCount; e++)
                    {
                        int v = call.BaseVertex + idx[cmd.IdxOffset + e];
                        Assert.InRange(v, listStart, listStart + list.VtxBuffer.Size - 1);
                    }
                }
                listStart += list.VtxBuffer.Size;
                idxStart += list.IdxBuffer.Size;
            }
        }
        Assert.Equal(plan.Count, k);
    }

    // ------------------------------------------------------------------ the cull

    /// <summary>The cull's bounds contain the geometry ImGui REALLY emits for each command kind (each command is
    /// replayed alone into an empty list and every vertex position is read back), so culling against them can
    /// never drop a command that would have touched a visible pixel.</summary>
    [Fact]
    public void CullBounds_ContainEveryVertexImGuiEmits_ForEachCommandKind()
    {
        using var gui = new HeadlessImGui(Assets(), W, H);
        var backend = new DrawListImGuiBackend(gui.Fonts) { Cull = false };
        Rgba ink = ParchmentPalette.InkPrimary;
        var style = new TextStyle(TypeFace.Garamond, 700, 0.12, TextCase.Upper);
        DrawCmd[] cmds =
        [
            new RectCmd(new RectD(100, 100, 80, 30), ink, ink, 3.0, 6.0),
            new LineCmd(10, 10, 300, 90, ink, 4.0, null),
            new LineCmd(10, 10, 300, 90, ink, 2.0, (6.0, 4.0)),
            new BezierCmd((50, 50), (200, -40), (260, 300), (400, 120), ink, 3.0, null),
            new BezierCmd((50, 50), (200, -40), (260, 300), (400, 120), ink, 2.0, (5.0, 3.0)),
            new PolygonCmd([(10, 10), (60, 15), (40, 70)], ink),
            new PolylineCmd([(10, 10), (60, 15), (40, 70), (5, 50)], ink, 5.0, true),
            new CircleCmd(200, 200, 40, ink, ink, 3.0),
            new CircleCmd(200, 200, 0.8, ink, null, 1.0),
            new ArcCmd(300, 300, 50, 30, 250, ink, 4.0),
            new TextCmd(400, 300, "Wheel and axle: WWW mmm", 19, ink, TextAlign.Left, FontRole.Body),
            new TextCmd(400, 300, "Centred WWW", 25, ink, TextAlign.Center, FontRole.Heading),
            new TextCmd(400, 300, "Right 12,345", 17, ink, TextAlign.Right, FontRole.Numeric),
            new TextCmd(400, 300, "tracked bold caps", 14, ink, TextAlign.Center, FontRole.Caps, style),
            new TextCmd(400, 300, "two\nlines", 14, ink, TextAlign.Left, FontRole.Body),
        ];
        foreach (DrawCmd cmd in cmds)
        {
            gui.BeginFrame(default, default, 1.0 / 60);
            ImDrawListPtr dl = ImGui.GetBackgroundDrawList();
            var one = new DrawList();
            one.Add(cmd);
            backend.Render(dl, one);
            RectD b = DrawListCull.Bounds(cmd)!.Value;
            unsafe
            {
                ImDrawVert* v = (ImDrawVert*)dl.VtxBuffer.Data;
                Assert.True(dl.VtxBuffer.Size > 0, cmd.ToString());
                for (int i = 0; i < dl.VtxBuffer.Size; i++)
                    Assert.True(v[i].pos.X >= b.X && v[i].pos.X <= b.Right && v[i].pos.Y >= b.Y && v[i].pos.Y <= b.Bottom,
                        $"{cmd.GetType().Name}: vertex ({v[i].pos.X}, {v[i].pos.Y}) outside {b}");
            }
            gui.EndFrame();
        }
    }

    [Fact]
    public void Cull_DropsOnlyCommandsOutsideTheVisibleRect_AndHonoursTheClipStack()
    {
        Rgba ink = ParchmentPalette.InkPrimary;
        var d = new DrawList();
        d.Rect(new RectD(10, 10, 20, 20), ink);           // 0 visible
        d.Rect(new RectD(-500, 10, 20, 20), ink);         // 1 left of the screen: culled
        d.PushClip(new RectD(100, 100, 100, 100));
        d.Rect(new RectD(150, 150, 10, 10), ink);         // 3 inside the clip
        d.Rect(new RectD(10, 10, 20, 20), ink);           // 4 on screen but outside the clip: culled
        d.Circle(98, 150, 3, ink);                        // 5 its fringe touches the clip edge: kept
        d.PopClip();
        d.Rect(new RectD(10, 10, 20, 20), ink);           // 7 the clip is restored: visible
        d.Text(1270, 400, "a long label running off the right edge", 14, ink);   // 8 starts on screen: kept
        d.Text(-10, 400, "right-aligned off the left", 14, ink, TextAlign.Right); // 9 entirely left: culled
        List<DrawCmd> kept = DrawListCull.Visible(d.Commands, new RectD(0, 0, W, H), out int culled);
        Assert.Equal(3, culled);
        Assert.DoesNotContain(d.Commands[1], kept);
        Assert.DoesNotContain(d.Commands[9], kept);
        Assert.Equal(new[] { 0, 2, 3, 5, 6, 7, 8 }, Indices(d.Commands, kept));
    }

    private static int[] Indices(IReadOnlyList<DrawCmd> all, List<DrawCmd> kept)
    {
        var r = new List<int>();
        int k = 0;
        for (int i = 0; i < all.Count && k < kept.Count; i++)
            if (ReferenceEquals(all[i], kept[k])) { r.Add(i); k++; }
        return [.. r];
    }

    /// <summary>The cull changes nothing on screen: on a scrolled A1 tree, every command it drops is replayed
    /// ALONE through ImGui and the box around every vertex ImGui emits for it misses the visible region it was
    /// painted into (the screen narrowed by the list's clip pushes) — so it could not have lit a pixel.</summary>
    [Fact]
    public void Cull_DropsOnlyCommandsThatCannotLightAVisiblePixel()
    {
        using var gui = new HeadlessImGui(Assets(), W, H);
        UiSession s = fx.Session;
        var screen = new ProgressionScreen(s.Config.Research!, Me) { Theme = EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me)) };
        screen.Refresh(s.World);
        var backend = new DrawListImGuiBackend(gui.Fonts) { Cull = false };
        screen.Paint(W, H, backend);
        screen.Camera.Set(1.0, 0, 900);   // a scrolled view: lanes and tiers cut by the canvas edges
        screen.Camera.Set(screen.Camera.TargetZoom, screen.Camera.TargetPanX, screen.Camera.TargetPanY);
        DrawList list = screen.Paint(W, H, backend);
        var screenRect = new RectD(0, 0, W, H);
        List<DrawCmd> visible = DrawListCull.Visible(list.Commands, screenRect, out int culled);
        Assert.True(culled > 0);
        var keptSet = new HashSet<DrawCmd>(visible, ReferenceEqualityComparer.Instance);
        var clips = new Stack<RectD>();
        RectD clip = screenRect;
        int checkedCount = 0;
        foreach (DrawCmd c in list.Commands)
        {
            if (c is ClipPushCmd push) { clips.Push(clip); clip = DrawListCull.Intersect(clip, push.Rect); continue; }
            if (c is ClipPopCmd) { clip = clips.Pop(); continue; }
            if (keptSet.Contains(c)) continue;
            gui.BeginFrame(default, default, 1.0 / 60);
            ImDrawListPtr dl = ImGui.GetBackgroundDrawList();
            var one = new DrawList();
            one.Add(c);
            backend.Render(dl, one);
            unsafe
            {
                ImDrawVert* v = (ImDrawVert*)dl.VtxBuffer.Data;
                double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
                for (int i = 0; i < dl.VtxBuffer.Size; i++)
                {
                    x0 = Math.Min(x0, v[i].pos.X); x1 = Math.Max(x1, v[i].pos.X);
                    y0 = Math.Min(y0, v[i].pos.Y); y1 = Math.Max(y1, v[i].pos.Y);
                }
                if (dl.VtxBuffer.Size > 0)
                    Assert.False(x0 < clip.Right && clip.X < x1 && y0 < clip.Bottom && clip.Y < y1,
                        $"culled {c.GetType().Name} reaches the visible region {clip}: ({x0},{y0})-({x1},{y1})");
            }
            gui.EndFrame();
            checkedCount++;
        }
        Assert.Equal(culled, checkedCount);
    }
}
