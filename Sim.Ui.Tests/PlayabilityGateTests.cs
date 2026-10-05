using System.Diagnostics;
using ImGuiNET;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.Headless;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Tests;

/// <summary>
/// THE M5 PLAYABILITY GATE (Director §2, §15; M5 hardening H1). The previous build passed 473 UI tests and crashed
/// on the first click of Research, because no test ever drove the live ImGui UI. These tests drive it: the real
/// per-frame UI (<see cref="GameUi"/>) in a real native ImGui context (<see cref="UiFrameHarness"/>) with clicks,
/// keys, wheel and drags where the player would make them.
///
/// The FULL gate runs as a SEPARATE PROCESS (<c>Sim.Ui --smoke</c>): a native ImGui assertion aborts the process,
/// which in-process would take the whole test host down with it and report nothing useful. The scenario logic also
/// runs in-process here where it is safe (the renderer capability is declared, so a past-16-bit list cannot abort).
/// </summary>
[Collection("ImGui context")]
public class PlayabilityGateTests
{
    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private static string WorkDir(string name) =>
        Path.Combine(Path.GetTempPath(), "sim-ui-gate-tests-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), name);

    private static (int Exit, string Out, string Err) RunSimUi(TimeSpan limit, params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Sim.Ui.dll"));
        foreach (string a in args) psi.ArgumentList.Add(a);
        using Process p = Process.Start(psi)!;
        Task<string> o = p.StandardOutput.ReadToEndAsync(), e = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(limit))
        {
            p.Kill(entireProcessTree: true);
            throw new TimeoutException("Sim.Ui " + string.Join(' ', args) + " did not finish in " + limit + " (non-termination)");
        }
        return (p.ExitCode, o.Result, e.Result);
    }

    // ------------------------------------------------------------------ out of process

    [Fact]
    public void Smoke_AsASeparateProcess_EveryStateEveryControl_Passes()
    {
        (int exit, string output, string err) = RunSimUi(TimeSpan.FromMinutes(20), "--smoke", "--monkey", "200");
        Assert.True(exit == 0, "exit " + exit + "\n" + Tail(output) + "\n" + Tail(err));
        Assert.Contains("SMOKE PASSED", output, StringComparison.Ordinal);
        Assert.Contains("0 FAIL; 0 problems", output, StringComparison.Ordinal);
        foreach (string state in new[] { "A1 turn 1", "one AI", "target set", "research done", "tax available", "age advance", "colony+revolt" })
            Assert.Contains("state: " + state + " - building", output, StringComparison.Ordinal);
    }

    /// <summary>THE CONTROL: the same live UI with the pre-fix renderer configuration (RendererHasVtxOffset NOT
    /// declared), opened on Research at the game's 1280×800 and moved over the tree, ABORTS with the exact assertion
    /// the Director hit. So the gate can see the crash — and passes above only because it is fixed.</summary>
    [Fact]
    public void Control_PreFixRendererConfiguration_AbortsOnTheResearchScreen_WithImGuis16BitAssertion()
    {
        (int exit, string output, string err) = RunSimUi(TimeSpan.FromMinutes(5), "--smoke-control-prefix");
        Assert.NotEqual(0, exit);
        Assert.DoesNotContain("CONTROL DID NOT REPRODUCE", output, StringComparison.Ordinal);
        Assert.Contains("opening Research", output, StringComparison.Ordinal);
        Assert.Contains("Too many vertices in ImDrawList using 16-bit indices", output + err, StringComparison.Ordinal);
    }

    private static string Tail(string s) => s.Length <= 6000 ? s : s[^6000..];

    // ------------------------------------------------------------------ in process

    [Fact]
    public void Gate_A1TurnOne_InProcess_EveryDrawnControlPasses_NoFrameDefect()
    {
        GateReport r = PlayabilityGate.Run(new GateOptions(Assets(), WorkDir("a1"), MonkeySteps: 150, OnlyStates: ["A1 turn 1"]));
        Assert.True(r.Ok, string.Join("\n", r.Problems) + "\n" + r.Details());
        Assert.True(r.Count(GateResult.Pass) > 120, "pass rows " + r.Count(GateResult.Pass));
        // Every player section, the research screen, the Age panel, the map and the formation were exercised.
        foreach (string control in new[] { "nav:Place", "nav:Empire", "nav:Policy", "nav:Institutions", "nav:Annals", "nav:Trends",
                     "band-research (open)", "K key (close / open)", "tab:Civics", "lens:Industry", "lane collapse / expand (Technology)",
                     "scroll through the whole tree (Civics)", "band-age (opens the Age panel)", "end-turn", "Space (End Turn)",
                     "select capital", "select formation (click token)", "unit card states the M7 limitation", "wheel zoom to Settlement" })
            Assert.Contains(r.Rows, row => row.Control == control && row.Result == GateResult.Pass);
    }

    /// <summary>The Director's click, through the real UI at the game's own size: the status band's research figure
    /// opens the tree; moving over it takes the background list past 65,535 vertices — the frames the pre-fix
    /// renderer aborted on — and every one of them passes the 16-bit contract now.</summary>
    [Fact]
    public void ResearchClick_ThroughTheLiveUi_At1280x800_HoverPassesTheOldLimit_AndEveryFrameIsValid()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("click"), Assets());
        Assert.True(h.ClickControl("band-research"));
        Assert.True(h.Ui.ProgressionOpen);
        for (int y = 180; y < 800; y += 40)
            for (int x = 0; x < 876; x += 48) h.MoveTo(x, y);
        Assert.Empty(h.Problems);
        Assert.True(h.MaxListVertices > ushort.MaxValue, "max " + h.MaxListVertices);
    }

    /// <summary>The chrome follows the window: at the design size it is exactly PanelLayout; maximised, the command
    /// bar sits on the bottom edge and the context panel on the right edge, and their controls work there.</summary>
    [Fact]
    public void Chrome_FollowsTheWindow_DesignSizeUnchanged_MaximisedAnchoredToTheEdges()
    {
        using (UiFrameHarness small = UiFrameHarness.Start(UiSession.Start(42), WorkDir("chrome-1280"), Assets()))
        {
            Assert.Equal(PanelLayout.Status, small.Ui.StatusRect);
            Assert.Equal(PanelLayout.Command, small.Ui.CommandRect);
            Assert.Equal(PanelLayout.Context, small.Ui.ContextRect);
            UiControl end = small.Ui.Controls.Find("end-turn")!.Value;
            Assert.Equal(ChromeGeometry.EndTurnButton.Y, end.Y0, 0.5);
        }
        using UiFrameHarness big = UiFrameHarness.Start(UiSession.Start(42), WorkDir("chrome-1920"), Assets(), width: 1920, height: 1009);
        Assert.Equal(1920f, big.Ui.StatusRect.Width);
        Assert.Equal(1009f - PanelLayout.Command.Height, big.Ui.CommandRect.Y);
        Assert.Equal(1920f - PanelLayout.Context.Width - PanelLayout.Margin, big.Ui.ContextRect.X);
        Assert.Equal(1009f - PanelLayout.Status.Height - PanelLayout.Command.Height - 2 * PanelLayout.Margin, big.Ui.ContextRect.Height);
        UiControl bigEnd = big.Ui.Controls.Find("end-turn")!.Value;
        Assert.Equal(big.Ui.CommandRect.Y + (ChromeGeometry.EndTurnButton.Y - PanelLayout.Command.Y), bigEnd.Y0, 0.5);
        Assert.True(big.ClickControl("nav:Policy"));
        Assert.Equal(Section.Policy, big.Ui.OpenSection);
        UiControl close = big.Ui.Controls.Find("close")!.Value;
        Assert.True(close.X1 <= 1920 - PanelLayout.Margin && close.X0 > big.Ui.ContextRect.X);
        long turn = big.Ui.World.Clock.Turn;
        Assert.True(big.ClickControl("end-turn"));
        Assert.Equal(turn + 1, big.Ui.World.Clock.Turn);
        Assert.Empty(big.Problems);
    }

    /// <summary>The context panel's body is one scrolling child for every section: a section opens at its top,
    /// not at the scroll the previous section was left at (found by the gate: POLICY scrolled to its end opened
    /// the developer TURN tab with its own tab row scrolled out of sight).</summary>
    [Fact]
    public void ContextPanel_ANewSectionOpensAtItsTop_NotAtThePreviousSectionsScroll()
    {
        UiSession s = UiSession.Start(42);
        s.EndTurn();   // a played turn: the TURN audit is long enough to scroll
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("scroll"), Assets());
        Assert.True(h.ClickControl("nav:Policy"));
        PanelRect p = h.Ui.ContextRect;
        for (int k = 0; k < 40; k++) h.Wheel(p.X + p.Width / 2, p.Y + p.Height / 2, -3);
        h.Key(Keys.F12);
        Assert.True(h.ClickControl("nav:Developer"));
        float top = ChromeGeometry.ContentTop(ChromeGeometry.Context, ImGui.GetFrameHeight());
        UiControl tab = h.Ui.Controls.Find("dev-Turn")!.Value;
        Assert.True(tab.Y0 >= top - 1, "the DEV tab row opened scrolled out of sight: y " + tab.Y0 + " above " + top);
        Assert.Empty(h.Problems);
    }

    /// <summary>The ADVANCE AGE flow is modal: it darkens the whole window, so the chrome under it must not answer
    /// clicks (found by the gate's review: End Turn and the section buttons stayed live beneath the backdrop, so a
    /// click "outside the dialog" ended the turn under it).</summary>
    [Fact]
    public void AgeFlow_IsModal_TheChromeBeneathItDoesNotAnswerClicks()
    {
        PlayabilityGate.GateState state = PlayabilityGate.States(null).First(st => st.Name == "age advance");
        using UiFrameHarness h = UiFrameHarness.Start(state.Build(), WorkDir("modal"), Assets());
        Assert.True(h.ClickControl("band-age"));
        Sim.Ui.Ages.AgeHitRegion open = h.Ui.Age.Hits.First(x => x.Kind == Sim.Ui.Ages.AgeHit.OpenAdvance);
        h.Click(open.Rect.CenterX, open.Rect.CenterY);
        Assert.True(h.Ui.Age.FlowOpen);
        long turn = h.Ui.World.Clock.Turn;
        UiControl end = h.Ui.Controls.Find("end-turn")!.Value;
        h.Click(end.CenterX, end.CenterY);
        UiControl nav = h.Ui.Controls.Find("nav:Policy")!.Value;
        h.Click(nav.CenterX, nav.CenterY);
        Assert.Equal(turn, h.Ui.World.Clock.Turn);
        Assert.Equal(Section.None, h.Ui.OpenSection);
        Assert.True(h.Ui.Age.FlowOpen);
        h.Key(Keys.Escape);   // the flow's own way out
        Assert.False(h.Ui.Age.FlowOpen);
        Assert.Empty(h.Problems);
    }

    /// <summary>Escape closes the open panel first and exits only when nothing is open — the Age panel included
    /// (it used to fall through to "nothing open" and ask the window to exit).</summary>
    [Fact]
    public void Escape_ClosesTheAgePanel_ItDoesNotExitTheGame()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("esc-age"), Assets());
        Assert.True(h.ClickControl("band-age"));
        Assert.True(h.Ui.AgePanelOpen);
        h.Key(Keys.Escape);
        Assert.False(h.Ui.AgePanelOpen);
        Assert.Equal(0, h.ExitRequests);
        h.Key(Keys.Escape);
        Assert.Equal(1, h.ExitRequests);   // now nothing is open: Escape is the window's exit, as before
    }

    /// <summary>The docked Age panel sits under the selection card, in the column the formation card uses, and
    /// overlaps no chrome window.</summary>
    [Fact]
    public void AgePanel_SitsBelowTheSelectionCard()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("age-rect"), Assets());
        Sim.Ui.Render.RectD r = h.Ui.AgePanelBounds;
        Assert.True(r.Y >= PanelLayout.Selection.Y + PanelLayout.Selection.Height, "Age panel top " + r.Y);
        Assert.Equal(GameUi.UnitCardRect.Y, (float)r.Y);
        Assert.True(r.Bottom <= h.Ui.CommandRect.Y, "Age panel bottom " + r.Bottom);
    }

    // ------------------------------------------------------------------ duplicate ids are seen

    /// <summary>The gate's id-conflict detection has teeth: two widgets sharing an id are reported by the control
    /// register (exhaustively) and by ImGui 1.91's own hover-time detector (its error tooltip).</summary>
    [Fact]
    public void DuplicateImGuiIds_AreReportedByBothDetectors()
    {
        using var gui = new HeadlessImGui(Assets());
        Assert.True(ImGui.GetIO().ConfigDebugHighlightIdConflicts);
        var controls = new UiControls();
        var mouse = new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        bool tooltip = false;
        for (int f = 0; f < 8; f++)   // ImGui's warning shows from the ~5th hovered frame
        {
            gui.BeginFrame(mouse, default, 1.0 / 60);
            controls.BeginFrame();
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 10));
            ImGui.Begin("probe");
            ImGui.Button("same"); controls.Record("first");
            ImGui.Button("same"); controls.Record("second");
            ImGui.End();
            controls.EndFrame();
            gui.EndFrame();
            tooltip |= gui.LastTooltipWindows.Count > 0;
        }
        Assert.Single(controls.Duplicates);
        Assert.Contains("first / second", controls.Duplicates[0], StringComparison.Ordinal);
        Assert.True(tooltip, "ImGui's own conflict tooltip was not seen");
    }

    // ------------------------------------------------------------------ the warband (Director §3)

    [Fact]
    public void Warband_ClickingItsToken_SelectsIt_AndTheCardStatesItCannotMoveUntilTheBattleLayer()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("warband"), Assets());
        MilitaryUnitRow unit = MilitaryQuery.Units(h.Ui.World, UiPlayer.Empire)[0];
        CameraFocus.CenterOn(h.Ui.Camera, h.Ui.World, unit.Location.Value, h.Width, h.Height);
        h.Idle(2);
        UnitPlacement at = Placement(h, unit.Id);
        h.Click(at.X, at.Y);

        Assert.Equal(unit.Id, h.Ui.SelectedUnit);
        UnitCard card = h.Ui.UnitCard!;
        Assert.Equal("Warband", card.Title);
        Assert.True(card.Mine);
        Assert.Equal("Heavy Infantry / Line Infantry", card.Family);
        Assert.Equal("Warband -> Axe warriors -> Bronze-armed infantry", card.Line);
        Assert.Equal("Its Age I form: Warband", card.AgeForm);
        Assert.StartsWith("Stationed at ", card.Station, StringComparison.Ordinal);
        Assert.StartsWith("At the next Age (", card.NextAge, StringComparison.Ordinal);
        Assert.EndsWith("becomes Axe warriors", card.NextAge, StringComparison.Ordinal);
        Assert.Contains("Battle Layer (milestone M7)", card.Limitation, StringComparison.Ordinal);
        Assert.Contains("cannot be moved or given orders yet", card.Limitation, StringComparison.Ordinal);

        // The card's ONLY control is its close button: no control pretends to move the formation.
        var onCard = new List<string>();
        foreach (UiControl c in h.Ui.Controls.Last)
            if (c.X0 >= GameUi.UnitCardRect.X && c.X1 <= GameUi.UnitCardRect.X + GameUi.UnitCardRect.Width
                && c.Y0 >= GameUi.UnitCardRect.Y && c.Y1 <= GameUi.UnitCardRect.Y + GameUi.UnitCardRect.Height)
                onCard.Add(c.Name);
        Assert.Equal(["unit-close"], onCard);
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void Warband_NothingTheMapOffersMovesIt_AndNoOrderIsWritten()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("warband-still"), Assets());
        MilitaryUnitRow unit = MilitaryQuery.Units(h.Ui.World, UiPlayer.Empire)[0];
        CameraFocus.CenterOn(h.Ui.Camera, h.Ui.World, unit.Location.Value, h.Width, h.Height);
        h.Idle(2);
        UnitPlacement at = Placement(h, unit.Id);
        int orders = h.Ui.Session.Orders.Count;
        h.Click(at.X, at.Y);
        // What a player would try: drag the token, right-click elsewhere, click a destination.
        h.Drag(at.X, at.Y, at.X + 150, at.Y + 90);
        h.RightClick(at.X + 200, at.Y + 120);
        h.Click(at.X + 180, at.Y - 60);
        h.ClickControl("end-turn");
        MilitaryUnitRow after = MilitaryQuery.Units(h.Ui.World, UiPlayer.Empire)[0];
        Assert.Equal((unit.Location, unit.X, unit.Y), (after.Location, after.X, after.Y));
        // The only order the session holds is none: the token click and the gestures emit nothing.
        Assert.Equal(orders, h.Ui.Session.Orders.Count);
        // And no order kind exists that could move it (the kernel's vocabulary at this commit).
        foreach (OrderKind k in Enum.GetValues<OrderKind>())
            Assert.DoesNotContain("Move", k.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(8, Enum.GetValues<OrderKind>().Length);
    }

    [Fact]
    public void Warband_Card_ClosesWithItsButton_WithEscape_AndWhenASettlementIsChosen_AndGivesWayToTheAgePanel()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("warband-close"), Assets());
        MilitaryUnitRow unit = MilitaryQuery.Units(h.Ui.World, UiPlayer.Empire)[0];
        void Select()
        {
            CameraFocus.CenterOn(h.Ui.Camera, h.Ui.World, unit.Location.Value, h.Width, h.Height);
            h.Idle(2);
            UnitPlacement at = Placement(h, unit.Id);
            h.Click(at.X, at.Y);
            Assert.Equal(unit.Id, h.Ui.SelectedUnit);
        }
        Select();
        Assert.True(h.ClickControl("unit-close"));
        Assert.Equal(-1, h.Ui.SelectedUnit);

        Select();
        h.Key(Keys.Escape);
        Assert.Equal(-1, h.Ui.SelectedUnit);
        Assert.Equal(0, h.ExitRequests);   // the first Escape closes the card, it does not exit

        Select();
        Assert.True(h.ClickControl("band-age"));
        Assert.True(h.Ui.AgePanelOpen);
        Assert.Equal(-1, h.Ui.SelectedUnit);   // one left-column surface at a time

        h.ClickControl("band-age");
        Select();
        Assert.False(h.Ui.AgePanelOpen);
        // Choosing a settlement puts the formation card away.
        SettlementRow row = h.Ui.World.Settlements[0];
        CameraFocus.CenterOn(h.Ui.Camera, h.Ui.World, row.Id.Value, h.Width, h.Height);
        h.Idle(2);
        LineGeometry.Vertex pos = OverlayMeshes.SettlementPosition(row, h.Ui.World.Terrain!.Size);
        (double sx, double sy) = h.Ui.Camera.WorldToScreen(pos.X, pos.Y, h.Width, h.Height);
        h.Click(sx, sy);
        Assert.Equal(-1, h.Ui.SelectedUnit);
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void UnitHitTest_IsTheTokenRectTheLensDrew_PlusAGrip()
    {
        var frame = new LensFrame(WorldZoom.Regional, [], [7], [], new int[16], [], [], [new UnitPlacement(7, 100, 100, 0)]);
        double s = UnitSelection.TokenSize(WorldZoom.Regional);
        Assert.Equal(7, UnitSelection.HitTest(frame, 100, 100));
        Assert.Equal(7, UnitSelection.HitTest(frame, 100 + s + UnitSelection.GripPx, 100));
        Assert.Equal(-1, UnitSelection.HitTest(frame, 100 + s + UnitSelection.GripPx + 1, 100));
        Assert.Equal(-1, UnitSelection.HitTest(frame, 100, 100 + s * 0.75 + UnitSelection.GripPx + 1));
        Assert.Equal(-1, UnitSelection.HitTest(null, 100, 100));
    }

    private static UnitPlacement Placement(UiFrameHarness h, int unit)
    {
        foreach (UnitPlacement p in h.Ui.LensFrame!.UnitPlacements) if (p.Id == unit) return p;
        throw new InvalidOperationException("formation " + unit + " not drawn");
    }
}
