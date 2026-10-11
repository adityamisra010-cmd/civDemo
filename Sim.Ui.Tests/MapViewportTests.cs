using Microsoft.Xna.Framework.Input;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.Headless;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// P1 (M5 map workstream) — CameraFocus through the MAP RECT, pure: on the canonical world, every settlement the
/// camera's clamp lets reach the centre lands within 0.5 px of the map rect's centre — not the window's — and every
/// other one stops at the clamp bound. The panel-open rect is used because its centre is 216 px from the window's:
/// a CenterOn aimed at the window would miss by that much.
/// </summary>
public class MapRectFocusTests
{
    [Fact]
    public void CenterOn_LandsEachSettlementWithinHalfAPixelOfTheMapRectsCentre_WhereTheClampAllows()
    {
        WorldState world = UiSession.Start(42).World;
        int size = world.Terrain!.Size;
        RectD map = PanelLayout.MapRectWithContextOpen;   // (0, 52, 848, 688)
        Assert.True(Math.Abs(map.CenterX - PanelLayout.DesignWidth / 2.0) > 200);
        int centred = 0, clamped = 0;
        foreach (double zoom in new[] { Camera.MaxZoom, 4.0 })
        {
            for (int i = 0; i < world.Settlements.Count; i++)
            {
                var cam = new Camera(size);
                cam.ZoomAt(map.CenterX, map.CenterY, zoom, map);
                double halfW = map.W / 2.0 / cam.Zoom, halfH = map.H / 2.0 / cam.Zoom;
                Assert.True(CameraFocus.CenterOn(cam, world, world.Settlements[i].Id.Value, map));
                LineGeometry.Vertex pos = OverlayMeshes.SettlementPosition(world.Settlements[i], size);
                (double sx, double sy) = cam.WorldToScreen(pos.X, pos.Y, map);
                bool interiorX = pos.X >= halfW && pos.X <= size - halfW;
                bool interiorY = pos.Y >= halfH && pos.Y <= size - halfH;
                if (interiorX) Assert.True(Math.Abs(sx - map.CenterX) <= 0.5, $"settlement {i}: x {sx} vs the map centre {map.CenterX}");
                else Assert.True(cam.CenterX == halfW || cam.CenterX == size - halfW);
                if (interiorY) Assert.True(Math.Abs(sy - map.CenterY) <= 0.5, $"settlement {i}: y {sy} vs the map centre {map.CenterY}");
                else Assert.True(cam.CenterY == halfH || cam.CenterY == size - halfH);
                if (interiorX && interiorY) centred++; else clamped++;
            }
        }
        Assert.True(centred >= 1, "no settlement of the canonical world is interior");
    }
}

/// <summary>
/// P1 — THE MAP RECT IN THE LIVE UI: the rect follows the placed chrome at every window; the hit test, the lens and
/// the level of detail go through it; opening a panel reframes without moving the world under the cursor; the
/// context panel and the selection card fold, and the map takes the space back; and none of it — pan, zoom, WASD,
/// CenterOn, folds, a window resize — changes the world hash or the order log.
/// </summary>
[Collection("ImGui context")]
public class MapViewportUiTests : IDisposable
{
    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-mapviewport-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    private string WorkDir(string name) => Path.Combine(_root, name);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private static RectD Band(UiFrameHarness h)
    {
        PanelRect st = h.Ui.StatusRect, cm = h.Ui.CommandRect;
        double top = (double)st.Y + st.Height;
        return new RectD(0, top, h.Width, Math.Min(h.Height, cm.Y) - top);
    }

    /// <summary>The map rect is the band between the PLACED bars at every window (the UI scale moves them at 2560x1440),
    /// narrows to the context panel's left edge when a section opens, and takes the whole band back when the panel
    /// folds to its tab — the section staying open. H folds every panel; the selection card folds to its title.</summary>
    [Theory]
    [InlineData(1080, 640)]
    [InlineData(1280, 800)]
    [InlineData(1920, 1009)]
    [InlineData(2560, 1440)]
    public void MapRect_FollowsThePlacedChrome_AndTakesTheColumnBackWhenItFolds(int w, int hh)
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("rect-" + w), Assets(), width: w, height: hh);
        RectD band = Band(h);
        Assert.Equal(band, h.Ui.MapRect);
        Assert.False(h.Ui.ContextShown);

        Assert.True(h.ClickControl("nav:Policy"));
        Assert.True(h.Ui.ContextShown);
        Assert.Equal(band with { W = h.Ui.ContextRect.X }, h.Ui.MapRect);
        Assert.NotNull(h.Ui.Controls.Find("panel-fold"));

        Assert.True(h.ClickControl("panel-fold"));
        h.Idle(1);
        Assert.True(h.Ui.ContextFolded);
        Assert.Equal(Section.Policy, h.Ui.OpenSection);   // folded, not closed
        Assert.Equal(band, h.Ui.MapRect);
        Assert.Null(h.Ui.Controls.Find("close"));           // the panel is not drawn...
        PanelRect tab = h.Ui.ContextTabRect;                // ...its tab is, at the top right, between the bars
        Assert.True(tab.Width > 0 && tab.X + tab.Width <= w && tab.Y >= band.Y && tab.Y + tab.Height <= band.Bottom,
            $"tab {tab} outside the band {band}");
        Assert.NotNull(h.Ui.Controls.Find("panel-unfold"));

        h.Key(Keys.H);   // every panel folded already but the card: H folds the card too
        Assert.True(h.Ui.SelectionFolded && h.Ui.ContextFolded);
        Assert.Equal(band, h.Ui.MapRect);
        h.Key(Keys.H);   // all folded: H unfolds them
        h.Idle(1);
        Assert.False(h.Ui.SelectionFolded || h.Ui.ContextFolded);
        Assert.True(h.Ui.ContextShown);
        Assert.Equal(band with { W = h.Ui.ContextRect.X }, h.Ui.MapRect);
        Assert.Empty(h.Problems);
    }

    /// <summary>THE HIT TEST GOES THROUGH THE MAP RECT. With a section open the map rect's centre is 216 px left of the
    /// window's, so a click on a settlement centred by CenterOn selects it only if the click is resolved through the
    /// same rect the lens drew it through — and the lens's World-zoom mark for that settlement is drawn exactly there.</summary>
    [Fact]
    public void MapClick_WithThePanelOpen_SelectsTheSettlementWhereTheLensDrewIt()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("click"), Assets());
        Assert.True(h.ClickControl("nav:Policy"));
        RectD map = h.Ui.MapRect;
        Assert.Equal(848.0, map.W);
        WorldState w = h.Ui.World;
        int pick = -1;
        for (int i = w.Settlements.Count - 1; i >= 0 && pick < 0; i--)
            if (w.Settlements[i].Id.Value != h.Ui.SelectedSettlement) pick = i;
        Assert.True(pick >= 0);
        int id = w.Settlements[pick].Id.Value;
        Assert.True(CameraFocus.CenterOn(h.Ui.Camera, w, id, map));
        h.Idle(2);
        LineGeometry.Vertex pos = OverlayMeshes.SettlementPosition(w.Settlements[pick], w.Terrain!.Size);
        (double sx, double sy) = h.Ui.Camera.WorldToScreen(pos.X, pos.Y, h.Ui.MapRect);
        Assert.True(h.Ui.MapRect.Contains(sx, sy));
        Assert.Equal(WorldZoom.World, h.Ui.LensFrame!.Zoom);
        Assert.Contains(h.Ui.LastLensCommands, c => c is CircleCmd k && Math.Abs(k.Cx - sx) < 1e-6 && Math.Abs(k.Cy - sy) < 1e-6);

        h.Click(sx, sy);
        Assert.Equal(id, h.Ui.SelectedSettlement);
        Assert.Equal(Section.Policy, h.Ui.OpenSection);
        Assert.Empty(h.Problems);
    }

    /// <summary>THE LEVEL OF DETAIL IS THE MAP RECT'S. One wheel notch from zoom 1 at 1280x800 shows 688/1.25 = 550 px of
    /// world across the map rect's short side (0.537 of the world: Regional), where the window's would have been 640 px
    /// (0.625: World).</summary>
    [Fact]
    public void LevelOfDetail_IsTheMapRectsVisibleSpan_NotTheWindows()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("lod"), Assets());
        Assert.Equal(1.0, h.Ui.Camera.Zoom);
        RectD map = h.Ui.MapRect;
        h.Wheel(map.CenterX, map.CenterY, 1);
        Assert.Equal(1.25, h.Ui.Camera.Zoom, 12);
        Assert.Equal(WorldZoom.World, WorldLens.LevelFor(h.Ui.Camera.Zoom, h.Width, h.Height, h.Ui.Camera.WorldSize));   // control
        Assert.Equal(WorldZoom.Regional, WorldLens.LevelFor(h.Ui.Camera.Zoom, map.W, map.H, h.Ui.Camera.WorldSize));
        Assert.Equal(WorldZoom.Regional, h.Ui.LensFrame!.Zoom);
    }

    /// <summary>"Opening the context panel re-fits the view without moving the world point under the cursor": zoomed
    /// in, a section opened by its digit key (the cursor stays on the map) leaves the world under the cursor where it
    /// was, and closing it does too; at the fit, the view re-fits the narrower rect — the whole world inside it.</summary>
    [Fact]
    public void OpeningThePanel_ReframesWithoutMovingTheWorldPointUnderTheCursor_AndAFitViewReFits()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("reframe"), Assets());
        RectD band = h.Ui.MapRect;
        h.Wheel(band.CenterX, band.CenterY, 3);
        h.MoveTo(300, 400);
        (double wx, double wy) = h.Ui.Camera.ScreenToWorld(300, 400, h.Ui.MapRect);
        double zoom = h.Ui.Camera.Zoom;

        h.Key(Keys.D1);
        h.Idle(1);
        Assert.True(h.Ui.ContextShown);
        Assert.True(h.Ui.MapRect.W < band.W);
        (double ax, double ay) = h.Ui.Camera.ScreenToWorld(300, 400, h.Ui.MapRect);
        Assert.Equal(wx, ax, 9);
        Assert.Equal(wy, ay, 9);
        Assert.Equal(zoom, h.Ui.Camera.Zoom);

        h.Key(Keys.Escape);   // closes the section: the rect widens back, the world stays put
        h.Idle(1);
        Assert.Equal(band, h.Ui.MapRect);
        (double cx, double cy) = h.Ui.Camera.ScreenToWorld(300, 400, h.Ui.MapRect);
        Assert.Equal(wx, cx, 9);
        Assert.Equal(wy, cy, 9);

        for (int k = 0; k < 20; k++) h.Wheel(band.CenterX, band.CenterY, -2);   // out to the fit
        Assert.Equal(h.Ui.Camera.MinZoom(band), h.Ui.Camera.Zoom);
        h.Key(Keys.D1);
        h.Idle(1);
        RectD open = h.Ui.MapRect;
        Assert.Equal(h.Ui.Camera.MinZoom(open), h.Ui.Camera.Zoom);
        (double x0, double y0) = h.Ui.Camera.WorldToScreen(0, 0, open);
        (double x1, double y1) = h.Ui.Camera.WorldToScreen(h.Ui.Camera.WorldSize, h.Ui.Camera.WorldSize, open);
        Assert.True(x0 >= open.X - 1e-9 && x1 <= open.Right + 1e-9 && y0 >= open.Y - 1e-9 && y1 <= open.Bottom + 1e-9,
            $"the fitted world {x0},{y0}..{x1},{y1} is not inside the map rect {open}");
        Assert.Empty(h.Problems);
    }

    /// <summary>The folded column's rules: its own nav button and its digit UNFOLD it (they do not close it); another
    /// section opens unfolded; Escape still closes it; the pinned info card folds and unfolds the same way.</summary>
    [Fact]
    public void FoldedColumn_ItsButtonAndDigitUnfoldIt_AnotherSectionOpensUnfolded_EscapeCloses_TheInfoCardFoldsToo()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("fold"), Assets());
        IReadOnlyList<Section> roster = GameSections.Roster(false);
        int digit = 0;
        for (int i = 0; i < roster.Count; i++) if (roster[i] == Section.Policy) digit = i + 1;
        Assert.True(digit > 0);
        Section other = roster[0] == Section.Policy ? roster[1] : roster[0];

        void Fold()
        {
            Assert.True(h.ClickControl("panel-fold"));
            h.Idle(1);
            Assert.True(h.Ui.ContextFolded);
        }

        Assert.True(h.ClickControl("nav:Policy"));
        Fold();
        Assert.True(h.ClickControl("nav:Policy"));   // its own button: unfold, not close
        h.Idle(1);
        Assert.Equal(Section.Policy, h.Ui.OpenSection);
        Assert.True(h.Ui.ContextShown);

        Fold();
        h.Key(Keys.D0 + digit);                       // its digit: unfold
        h.Idle(1);
        Assert.True(h.Ui.ContextShown);

        Fold();
        Assert.True(h.ClickControl("nav:" + other)); // another section opens unfolded
        h.Idle(1);
        Assert.Equal(other, h.Ui.OpenSection);
        Assert.True(h.Ui.ContextShown);

        Fold();
        h.Key(Keys.Escape);                           // Escape closes the folded section
        h.Idle(1);
        Assert.Equal(Section.None, h.Ui.OpenSection);
        Assert.False(h.Ui.ContextFolded || h.Ui.ContextShown);
        Assert.Equal(Band(h), h.Ui.MapRect);

        h.Ui.OpenInfo(InfoSubject.Research);          // the pinned card takes the column...
        h.Idle(2);
        Assert.True(h.Ui.ContextShown);
        Fold();                                       // ...folds to its tab...
        Assert.True(h.Ui.Inspector.IsOpen);
        Assert.Equal(Band(h), h.Ui.MapRect);
        Assert.True(h.ClickControl("panel-unfold"));  // ...and comes back
        h.Idle(1);
        Assert.True(h.Ui.ContextShown && h.Ui.Inspector.IsOpen);
        Assert.Empty(h.Problems);
    }

    /// <summary>Folds the right-hand column by its header button and checks it folded.</summary>
    private static void FoldColumn(UiFrameHarness h)
    {
        Assert.True(h.ClickControl("panel-fold"));
        h.Idle(1);
        Assert.True(h.Ui.ContextFolded);
    }

    /// <summary>The column shows <paramref name="section"/> as a panel: open, not folded, its close button drawn.</summary>
    private static void AssertShown(UiFrameHarness h, Section section)
    {
        Assert.Equal(section, h.Ui.OpenSection);
        Assert.False(h.Ui.Inspector.IsOpen);
        Assert.False(h.Ui.ContextFolded, section + " came back folded");
        Assert.True(h.Ui.ContextShown);
        Assert.NotNull(h.Ui.Controls.Find("close"));
    }

    /// <summary>THE FOLD BELONGS TO THE CONTENT SHOWN (P1 review finding: the fold was remembered after its panel closed,
    /// so the panel reopened folded — only its tab drawn). A folded section closed by Escape and reopened by its button
    /// or its digit is shown; a section folded, another section opened (by button or digit), and the first reopened,
    /// shows the first; a section folded, the info card opened over it and closed, shows the section. "Another section,
    /// or another info subject, opens unfolded" (docs/m5-playability-gate.md §3.3) holds however the content came back.</summary>
    [Fact]
    public void FoldedColumn_IsForgottenWhenItsContentGoes_SoReopeningItShowsThePanel()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("fold-forget"), Assets());
        IReadOnlyList<Section> roster = GameSections.Roster(false);
        int digit = 0;
        for (int i = 0; i < roster.Count; i++) if (roster[i] == Section.Policy) digit = i + 1;
        Assert.True(digit > 0);
        int otherDigit = roster[0] == Section.Policy ? 2 : 1;
        Section other = roster[otherDigit - 1];

        Assert.True(h.ClickControl("nav:Policy"));
        FoldColumn(h);
        h.Key(Keys.Escape);                            // Escape closes the folded section...
        h.Idle(1);
        Assert.Equal(Section.None, h.Ui.OpenSection);
        Assert.True(h.ClickControl("nav:Policy"));    // ...and its button reopens it SHOWN
        h.Idle(1);
        AssertShown(h, Section.Policy);

        FoldColumn(h);
        h.Key(Keys.Escape);
        h.Idle(1);
        h.Key(Keys.D0 + digit);                        // the same by its digit
        h.Idle(1);
        AssertShown(h, Section.Policy);

        FoldColumn(h);
        Assert.True(h.ClickControl("nav:" + other));  // fold A, open B by its button...
        h.Idle(1);
        AssertShown(h, other);
        Assert.True(h.ClickControl("nav:Policy"));    // ...reopen A: shown
        h.Idle(1);
        AssertShown(h, Section.Policy);

        FoldColumn(h);
        h.Key(Keys.D0 + otherDigit);                   // the same by digits
        h.Idle(1);
        AssertShown(h, other);
        h.Key(Keys.D0 + digit);
        h.Idle(1);
        AssertShown(h, Section.Policy);

        FoldColumn(h);
        h.Ui.OpenInfo(InfoSubject.Research);           // fold A, the info card takes the column...
        h.Idle(2);
        Assert.True(h.Ui.Inspector.IsOpen && h.Ui.ContextShown);
        Assert.True(h.ClickControl("info-close"));    // ...closed by its button: A comes back shown
        h.Idle(1);
        AssertShown(h, Section.Policy);
        Assert.Empty(h.Problems);
    }

    /// <summary>The pinned info card's fold is the card's: closed by Escape and opened again on the same subject it is
    /// shown; folded, and opened again on the same subject (Shift+click on the research figure), it is shown — the
    /// player asked to see it. And a SECTION action never unfolds the card over it: with the card folded over an open
    /// section, that section's button (which closes the section under the card) and a click-to-explain route (which
    /// opens one under it) leave the card folded — they change nothing the column shows (the P1 review finding's
    /// related case: closing the section once unfolded the card).</summary>
    [Fact]
    public void FoldedInfoCard_IsShownWhenOpenedAgain_AndASectionActionUnderItLeavesItFolded()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("fold-info"), Assets());
        void ShiftClickResearch()
        {
            UiControl chip = h.Ui.Controls.Find("band-research") ?? throw new InvalidOperationException("no research figure");
            h.ShiftClick(chip.CenterX, chip.CenterY);
            h.Idle(1);
        }

        ShiftClickResearch();
        Assert.True(h.Ui.Inspector.IsOpen && h.Ui.ContextShown);
        InfoSubject subject = h.Ui.Inspector.Subject!.Value;
        FoldColumn(h);
        h.Key(Keys.Escape);                            // Escape closes the folded card...
        h.Idle(1);
        Assert.False(h.Ui.Inspector.IsOpen);
        ShiftClickResearch();                          // ...and the same subject opens SHOWN
        Assert.Equal(subject, h.Ui.Inspector.Subject);
        Assert.True(h.Ui.ContextShown && !h.Ui.ContextFolded);

        FoldColumn(h);
        ShiftClickResearch();                          // folded, the same subject asked for again: shown
        Assert.Equal(subject, h.Ui.Inspector.Subject);
        Assert.True(h.Ui.ContextShown && !h.Ui.ContextFolded);

        h.Key(Keys.Escape);
        h.Idle(1);
        Assert.True(h.ClickControl("nav:Policy"));
        ShiftClickResearch();                          // the card over an open section...
        FoldColumn(h);
        Assert.True(h.ClickControl("nav:Policy"));    // ...its button closes the section under the card...
        h.Idle(1);
        Assert.Equal(Section.None, h.Ui.OpenSection);
        Assert.True(h.Ui.Inspector.IsOpen && h.Ui.ContextFolded && !h.Ui.ContextShown);   // ...the card stays folded
        Assert.True(h.ClickControl("fig:SettlementPopulation"));                          // a route opens one under it...
        h.Idle(1);
        Assert.NotEqual(Section.None, h.Ui.OpenSection);
        Assert.True(h.Ui.Inspector.IsOpen && h.Ui.ContextFolded && !h.Ui.ContextShown);   // ...the card stays folded
        Assert.Empty(h.Problems);
    }

    /// <summary>CLICK-TO-EXPLAIN INTO A FOLDED SECTION SHOWS IT (P1 review finding: the route re-opened the section that
    /// was already open but folded, the fold stayed, and the explanation stayed hidden — the click did nothing visible).</summary>
    [Fact]
    public void AFigureRouteIntoAFoldedSection_UnfoldsIt()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("fold-route"), Assets());
        Assert.True(h.ClickControl("fig:SettlementPopulation"));
        h.Idle(1);
        Section routed = h.Ui.OpenSection;
        Assert.NotEqual(Section.None, routed);
        AssertShown(h, routed);
        FoldColumn(h);
        Assert.Equal(routed, h.Ui.OpenSection);
        Assert.True(h.ClickControl("fig:SettlementPopulation"));   // the same figure, its section folded
        h.Idle(1);
        AssertShown(h, routed);
        Assert.Empty(h.Problems);
    }

    /// <summary>The selection card folds to its title row: its figure rows are not drawn, it is shorter, and the left
    /// column under it (the docked Age panel) moves up with it; unfolding restores it exactly.</summary>
    [Fact]
    public void SelectionCard_FoldsToItsTitle_AndTheLeftColumnFollowsIt()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("card"), Assets());
        PanelRect open = h.Ui.SelectionRect;
        // The card's figure rows: "fig:" controls in the card's column at or below its top (the status band's figures
        // are above it), wherever they are drawn — inside the card's rect or spilling past it.
        int Figures()
        {
            PanelRect r = h.Ui.SelectionRect;
            int n = 0;
            foreach (UiControl c in h.Ui.Controls.Last)
                if (c.Name.StartsWith("fig:", StringComparison.Ordinal) && c.Y0 >= r.Y && c.X0 >= r.X && c.X0 < r.X + r.Width) n++;
            return n;
        }
        Assert.True(Figures() > 0);
        Assert.True(h.ClickControl("card-fold"));
        h.Idle(1);
        Assert.True(h.Ui.SelectionFolded);
        Assert.True(h.Ui.SelectionRect.Height < open.Height, $"{h.Ui.SelectionRect.Height} vs {open.Height}");
        Assert.Equal(0, Figures());
        Assert.True(h.ClickControl("band-age"));   // the docked Age panel starts under the card as drawn
        Assert.Equal(h.Ui.SelectionRect.Y + h.Ui.SelectionRect.Height + PanelLayout.Margin * h.Ui.Scale, h.Ui.AgePanelBounds.Y, 3);
        Assert.True(h.ClickControl("band-age"));
        Assert.True(h.ClickControl("card-fold"));
        h.Idle(1);
        Assert.False(h.Ui.SelectionFolded);
        Assert.Equal(open, h.Ui.SelectionRect);
        Assert.True(Figures() > 0);
        Assert.Empty(h.Problems);
    }

    /// <summary>THE READ-ONLY PIN: the camera and the folds are UI state. Drag, the wheel both ways, W A S D, CenterOn,
    /// a panel opened, folded, unfolded, H twice, the card folded and unfolded, and a window resize (the reframe) — and
    /// the world hash and the queued orders are exactly what they were.</summary>
    [Fact]
    public void CameraAndFolds_ChangeNeitherTheWorldHashNorTheOrderLog()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("readonly"), Assets());
        string hash = WorldHash.ComputeHex(h.Ui.Session.World);
        OrderRecord[] queued = [.. h.Ui.Session.QueuedOrders()];
        long turn = h.Ui.World.Clock.Turn;
        RectD map = h.Ui.MapRect;
        double cx = h.Ui.Camera.CenterX, cy = h.Ui.Camera.CenterY, zoom = h.Ui.Camera.Zoom;

        h.Drag(map.CenterX, map.CenterY + 30, map.CenterX + 120, map.CenterY + 70);
        h.Wheel(map.CenterX, map.CenterY, 4);
        h.Wheel(map.CenterX, map.CenterY, -2);
        h.Hold(Keys.D, 10);
        h.Hold(Keys.S, 10);
        Assert.True(CameraFocus.CenterOn(h.Ui.Camera, h.Ui.World, h.Ui.World.Settlements[0].Id.Value, h.Ui.MapRect));
        h.Idle(2);
        Assert.True(h.ClickControl("nav:Policy"));
        Assert.True(h.ClickControl("panel-fold"));
        Assert.True(h.ClickControl("panel-unfold"));
        h.Key(Keys.H);
        h.Key(Keys.H);
        Assert.True(h.ClickControl("card-fold"));
        Assert.True(h.ClickControl("card-fold"));
        h.Gui.Resize(1920, 1009);
        h.Idle(2);
        Assert.NotEqual(map, h.Ui.MapRect);   // the reframe ran
        Assert.True(h.Ui.Camera.CenterX != cx || h.Ui.Camera.CenterY != cy || h.Ui.Camera.Zoom != zoom);   // the camera moved

        Assert.Equal(turn, h.Ui.World.Clock.Turn);
        Assert.Equal(hash, WorldHash.ComputeHex(h.Ui.Session.World));
        Assert.Equal(queued, h.Ui.Session.QueuedOrders());
        Assert.Empty(h.Problems);
    }
}
