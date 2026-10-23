using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Headless;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// M5 map workstream P9b — THE OVERLAY'S KEY FITS THE CLEAR MAP (the m5w integration finding, docs/queue.md: with five or
/// six layers shown at 1280x800 the key was 753.8 / 840.0 px tall over a 688 px clear map, its top off the window, and its
/// 392 x 840 px box took the clicks over the map's centre). Measured through the real UI (<see cref="UiFrameHarness"/>):
/// <list type="bullet">
/// <item>FIT — at the 1080x640 floor, 1280x800 and 1920x1009, with 1 … 6 layers shown, the key's box lies inside the clear
/// map and keeps to its half of it (never over the centre line, where the camera frames what the player focuses on);</item>
/// <item>EVERY ROW REACHABLE — with every layer shown the wheel over the key scrolls it a whole row a notch, from its top to
/// its last row, never zooming the map; every row is drawn at some scroll, and at each scroll FrameText (lens) holds
/// exactly the drawn rows' text, line by line — so scrolled through, FrameText holds every line of the key;</item>
/// <item>THE CLICK — with six layers shown and the formation card open (the overlays tab and the key moved right of it, the
/// key's column over the map's centre), a click on the player's own formation token, framed at the map's centre,
/// selects it.</item>
/// </list>
/// </summary>
[Collection("ImGui context")]
public sealed class OverlayKeyFitTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-p9b-tests-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private string WorkDir(string name) => Path.Combine(_root, name);

    private static string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    private static string Box(RectD r) => "x " + F(r.X) + ".." + F(r.Right) + ", y " + F(r.Y) + ".." + F(r.Bottom);

    /// <summary>Shows every overlay layer through its checkbox, in index order (the controls unfolded for it), and returns
    /// the key after each one.</summary>
    private static List<OverlayKey> ShowEveryLayer(UiFrameHarness h)
    {
        if (!h.Ui.OverlayControlsOpen) Assert.True(h.ClickControl(GameUi.OverlaysControl));
        var keys = new List<OverlayKey>();
        foreach (OverlayLayer l in h.Ui.OverlayLayers)
        {
            Assert.True(h.ClickControl(l.Control), "no checkbox for " + l.Key);
            h.Idle(1);
            Assert.True(h.Ui.OverlayShown(l.Index));
            keys.Add(h.Ui.LensFrame!.Overlay!.Key!);
        }
        return keys;
    }

    /// <summary>The key lies inside the clear map and keeps off its centre line (the band a margin either side of it).</summary>
    private static void AssertFits(UiFrameHarness h, OverlayKey key, string what)
    {
        RectD map = h.Ui.MapRect;
        Assert.True(key.Rect.X >= map.X && key.Rect.Y >= map.Y && key.Rect.Right <= map.Right && key.Rect.Bottom <= map.Bottom,
            what + ": the key " + Box(key.Rect) + " leaves the clear map " + Box(map));
        Assert.True(key.Rect.H <= map.H, what + ": the key is " + F(key.Rect.H) + " px tall, the clear map " + F(map.H));
        double band = PanelLayout.Margin * h.Ui.Scale;
        Assert.False(key.Rect.Y < map.CenterY + band && key.Rect.Bottom > map.CenterY - band,
            what + ": the key " + Box(key.Rect) + " lies over the clear map's centre line y " + F(map.CenterY));
        Assert.True(key.RowsDrawn >= 1 && key.Scroll >= 0 && key.Scroll <= key.MaxScroll && key.Scroll + key.RowsDrawn <= key.Rows.Count,
            what + ": scroll " + key.Scroll + "/" + key.MaxScroll + ", " + key.RowsDrawn + " of " + key.Rows.Count + " rows drawn");
    }

    public static TheoryData<int, int> Windows() => new()
    {
        { PanelLayout.MinWindowWidth, PanelLayout.MinWindowHeight },
        { 1280, 800 },
        { 1920, 1009 },
    };

    /// <summary>FIT at every supported window, with 1 … 6 layers shown, the controls unfolded and folded, the legend shown,
    /// then the formation card open as well (the tab and the key move right of it). Non-vacuous: at 1280x800 the six-layer
    /// key's rows do not all fit (840 px of them before P9b), so it scrolls; at the 1080x640 floor with the card and the
    /// legend open there is no room beside the tab and the key stands ABOVE it — wholly in the map's upper half while the
    /// controls are unfolded (the tab's top is above the centre line), in its lower half while they are folded.</summary>
    [Theory]
    [MemberData(nameof(Windows))]
    public void TheKey_FitsTheClearMap_AndKeepsOffItsCentreLine_WithOneToSixLayers(int width, int height)
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.StartWith(TestFog.PreDiscovery, 42), WorkDir("fit-" + width), Assets(), width: width, height: height);
        List<OverlayKey> keys = ShowEveryLayer(h);
        Assert.Equal(6, keys.Count);
        for (int i = 0; i < keys.Count; i++) AssertFits(h, keys[i], (i + 1) + " layers at " + width + "x" + height);
        Assert.True(h.ClickControl(GameUi.OverlaysControl));   // fold the controls: the layers stay shown, the tab narrows
        h.Idle(1);
        AssertFits(h, h.Ui.LensFrame!.Overlay!.Key!, "folded at " + width + "x" + height);
        h.Key(Microsoft.Xna.Framework.Input.Keys.L);   // the legend over the bottom-right corner
        h.Idle(1);
        Assert.True(h.Ui.ShowLegend);
        AssertFits(h, h.Ui.LensFrame!.Overlay!.Key!, "with the legend at " + width + "x" + height);
        if (width == 1280) Assert.True(keys[5].MaxScroll > 0, "the six-layer key fits at 1280x800 - the rig is void");

        // The formation card open too: the player's formation, its capital framed at the map's centre (off the key).
        MilitaryUnitRow mine = MilitaryQuery.Units(h.Ui.World, UiPlayer.Empire)[0];
        int site = -1;
        for (int i = 0; i < h.Ui.World.Settlements.Count; i++) if (h.Ui.World.Settlements[i].Id == mine.Location) site = h.Ui.World.Settlements[i].SiteCell;
        CameraFocus.CenterOnCell(h.Ui.Camera, site, h.Ui.World.Terrain!.Size, h.Ui.MapRect, 4.0);
        h.Idle(2);
        UnitPlacement own = h.Ui.LensFrame!.UnitPlacements.Single(p => p.Id == mine.Id);
        h.Click(own.X, own.Y);
        h.Idle(1);
        Assert.Equal(mine.Id, h.Ui.SelectedUnit);
        var above = new List<bool>();
        foreach (bool unfolded in new[] { true, false })
        {
            if (h.Ui.OverlayControlsOpen != unfolded) Assert.True(h.ClickControl(GameUi.OverlaysControl));
            h.Idle(1);
            OverlayKey key = h.Ui.LensFrame!.Overlay!.Key!;
            string what = "card and legend open, controls " + (unfolded ? "unfolded" : "folded") + " at " + width + "x" + height;
            AssertFits(h, key, what);
            if (key.Rect.X == h.Ui.OverlayTabRect.X) above.Add(key.Rect.Bottom <= h.Ui.MapRect.CenterY);   // the key stands above the tab
        }
        if (width == PanelLayout.MinWindowWidth)
            Assert.Equal([true, false], above);   // above the tab: in the upper half (unfolded), in the lower half (folded)
        Assert.Empty(h.Problems);
    }

    /// <summary>The key's lines in FrameText (lens): the entries after its title, matched line by line against the drawn
    /// rows' text (a row's lines joined by one space are the row, as the lens wraps it). Returns the count matched.</summary>
    private static int AssertFrameTextIsTheDrawnRows(UiFrameHarness h, OverlayKey key, string what)
    {
        IReadOnlyList<string> lens = h.Ui.FrameText.Texts(FrameSurface.Lens);
        int at = -1;
        for (int i = 0; i < lens.Count; i++) if (lens[i] == ResourceOverlay.KeyTitle) at = i;
        Assert.True(at >= 0, what + ": the key's title is not in FrameText (lens)");
        int next = at + 1;
        foreach (string row in key.DrawnRows)
        {
            string want = string.Join(" ", row.Split(' ', StringSplitOptions.RemoveEmptyEntries)), got = "";
            while (got.Length < want.Length && next < lens.Count) got = got.Length == 0 ? lens[next++] : got + " " + lens[next++];
            Assert.True(got == want, what + ": FrameText has '" + got + "' where the key draws '" + want + "'");
        }
        // Nothing of the row after the window is drawn.
        if (key.Scroll + key.RowsDrawn < key.Rows.Count && next < lens.Count)
        {
            string hidden = key.Rows[key.Scroll + key.RowsDrawn];
            Assert.False(hidden.StartsWith(lens[next], StringComparison.Ordinal), what + ": a row past the window is drawn: " + lens[next]);
        }
        return next - at - 1;
    }

    /// <summary>EVERY ROW REACHABLE: with every layer shown the wheel over the key scrolls it — down a row a notch to its last
    /// row, then back up to its top — and never zooms the map; every row is drawn at some scroll, and at every scroll
    /// FrameText (lens) holds exactly the rows drawn, so scrolled through it holds every line of the key. Showing or hiding
    /// a layer puts the key back at its top. A wheel off the key still zooms the map (control).</summary>
    [Theory]
    [InlineData(1080, 640)]
    [InlineData(1280, 800)]
    public void EveryRow_IsReachableByTheWheelOverTheKey_AndFrameTextHoldsEveryLineScrolledThrough(int width, int height)
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.StartWith(TestFog.PreDiscovery, 42), WorkDir("scroll-" + width), Assets(), width: width, height: height);
        ShowEveryLayer(h);
        Assert.True(h.ClickControl(GameUi.OverlaysControl));
        h.Idle(2);
        OverlayKey key = h.Ui.LensFrame!.Overlay!.Key!;
        Assert.Equal(0, key.Scroll);
        Assert.True(key.MaxScroll > 0, "the key fits: nothing to scroll - the rig is void");
        IReadOnlyList<string> all = key.Rows;
        double zoom = h.Ui.Camera.TargetZoom;
        (double x, double y) = (key.Rect.CenterX, key.Rect.CenterY);
        h.MoveTo(x, y);
        var drawn = new SortedSet<int>();
        var lines = new List<string>();
        int notches = 0;
        while (true)
        {
            key = h.Ui.LensFrame!.Overlay!.Key!;
            Assert.Equal(all, key.Rows);   // scrolling changes what is drawn, never what the key says
            AssertFits(h, key, "scroll " + key.Scroll);
            Assert.True(key.Rect.Contains(x, y), "the pointer left the key");
            for (int r = key.Scroll; r < key.Scroll + key.RowsDrawn; r++) drawn.Add(r);
            AssertFrameTextIsTheDrawnRows(h, key, "scroll " + key.Scroll);
            lines.AddRange(h.Ui.FrameText.Texts(FrameSurface.Lens));
            if (key.Scroll == key.MaxScroll) break;
            int before = key.Scroll;
            h.Wheel(x, y, -1);
            notches++;
            Assert.Equal(before + GameUi.OverlayKeyRowsPerNotch, h.Ui.LensFrame!.Overlay!.Key!.Scroll);
            Assert.True(notches <= all.Count, "the wheel never reaches the key's last row");
        }
        Assert.Equal(key.Rows.Count, key.Scroll + key.RowsDrawn);   // at the bottom the last row is drawn
        Assert.Equal(Enumerable.Range(0, all.Count), drawn);         // every row, drawn at some scroll
        foreach (string row in all)
            foreach (string word in row.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                Assert.Contains(lines, l => l.Contains(word, StringComparison.Ordinal));
        h.Wheel(x, y, -3);   // past the bottom: it stays
        Assert.Equal(key.MaxScroll, h.Ui.LensFrame!.Overlay!.Key!.Scroll);
        Assert.Equal(zoom, h.Ui.Camera.TargetZoom);   // the wheel over the key never zoomed the map
        h.Wheel(x, y, 2);
        Assert.Equal(key.MaxScroll - 2 * GameUi.OverlayKeyRowsPerNotch, h.Ui.LensFrame!.Overlay!.Key!.Scroll);

        // A layer hidden: a new key, read from its top.
        Assert.True(h.ClickControl(GameUi.OverlaysControl));
        Assert.True(h.ClickControl(h.Ui.OverlayLayers[0].Control));
        h.Idle(1);
        Assert.Equal(0, h.Ui.LensFrame!.Overlay!.Key!.Scroll);

        // Control: off the key, the wheel zooms the map.
        RectD map = h.Ui.MapRect;
        h.MoveTo(map.CenterX, map.Y + map.H * 0.25);   // the pointer over the map before the wheel turns, as a player's is
        h.Wheel(map.CenterX, map.Y + map.H * 0.25, 1);
        Assert.NotEqual(zoom, h.Ui.Camera.TargetZoom);
        Assert.Empty(h.Problems);
    }

    /// <summary>
    /// THE CLICK (the finding's own case): the one-AI seed-42 world after six turns; the formation card open on the AI's
    /// formation (so the overlays tab and the key stand right of the card, the key's column over the map's centre); the
    /// player's capital framed at the map's centre; every layer shown. A click on the player's own formation token there
    /// selects it — before P9b the 840 px key lay over it and the click selected nothing (measured: the selection stayed on
    /// the AI's formation). Control: the token lies in the key's column, so only the key's height keeps it clear.
    /// </summary>
    [Fact]
    public void WithSixLayersAndTheCardOpen_AClickOnTheOwnFormationTokenAtTheMapCentre_SelectsIt()
    {
        UiSession s = UiSession.StartWith(TestFog.PreDiscovery, 42, aiEmpiresOverride: 1);
        for (int t = 0; t < 6; t++) s.EndTurn();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("click"), Assets());
        MilitaryUnitRow mine = MilitaryQuery.Units(s.World, UiPlayer.Empire)[0];
        MilitaryUnitRow theirs = default;
        bool found = false;
        for (int i = 0; i < s.World.MilitaryUnits.Count && !found; i++)
            if (s.World.MilitaryUnits[i].Owner.Value != UiPlayer.Empire.Value) { theirs = s.World.MilitaryUnits[i]; found = true; }
        Assert.True(found, "the world has no AI formation");

        // The card on the AI's formation.
        Assert.True(CameraFocus.CenterOn(h.Ui.Camera, h.Ui.World, theirs.Location.Value, h.Ui.MapRect));
        h.Idle(2);
        UnitPlacement them = h.Ui.LensFrame!.UnitPlacements.Single(p => p.Id == theirs.Id);
        h.Click(them.X, them.Y);
        h.Idle(1);
        Assert.Equal(theirs.Id, h.Ui.SelectedUnit);

        // Every layer shown, the controls folded; the player's capital at the map's centre.
        ShowEveryLayer(h);
        Assert.True(h.ClickControl(GameUi.OverlaysControl));
        int capSite = -1;
        for (int i = 0; i < s.World.Settlements.Count; i++) if (s.World.Settlements[i].Id == mine.Location) capSite = s.World.Settlements[i].SiteCell;
        Assert.True(capSite >= 0);
        CameraFocus.CenterOnCell(h.Ui.Camera, capSite, s.World.Terrain!.Size, h.Ui.MapRect, 4.0);
        h.Idle(2);
        Assert.Equal(theirs.Id, h.Ui.SelectedUnit);
        RectD map = h.Ui.MapRect;
        OverlayKey key = h.Ui.LensFrame!.Overlay!.Key!;
        UnitPlacement own = h.Ui.LensFrame!.UnitPlacements.Single(p => p.Id == mine.Id);
        Assert.True(Math.Abs(own.Y - map.CenterY) <= 1 && Math.Abs(own.X - map.CenterX) <= 120,
            "the player's token at " + F(own.X) + "," + F(own.Y) + " is not at the map's centre " + F(map.CenterX) + "," + F(map.CenterY));
        Assert.True(own.X >= key.Rect.X && own.X <= key.Rect.Right,
            "the token at x " + F(own.X) + " is not in the key's column " + Box(key.Rect) + " - the rig is void");
        Assert.Equal(mine.Id, UnitSelection.HitTest(h.Ui.LensFrame, own.X, own.Y));   // the token is what lies there

        h.Click(own.X, own.Y);
        h.Idle(1);
        Assert.Equal(mine.Id, h.Ui.SelectedUnit);   // THE CLICK: it reached the map and selected the token
        // Why: the key keeps off the token (its box ends below the centre line) while still saying all it says (it scrolls).
        key = h.Ui.LensFrame!.Overlay!.Key!;
        Assert.False(key.Rect.Contains(own.X, own.Y), "the key " + Box(key.Rect) + " lies over the token at " + F(own.X) + "," + F(own.Y));
        Assert.True(key.MaxScroll > 0, "the six-layer key fits whole here - it does not show the scroll at work");
        Assert.Empty(h.Ui.Session.QueuedOrders());
        Assert.Empty(h.Problems);
    }
}
