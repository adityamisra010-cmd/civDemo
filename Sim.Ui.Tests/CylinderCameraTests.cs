using Sim.Core.State;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// BATCH 3 WS3 — THE WRAPPED CAMERA, pure (no ImGui): <see cref="Camera"/> built with <c>wrapX: true</c> normalises x into
/// [0, W), pans across the seam continuously in both directions, eases and glides the SHORT way round, keeps its y clamp,
/// and floors its zoom at one circumference across the WINDOW (so a panel toggle neither re-zooms nor changes the band);
/// <see cref="Camera.VisibleCopies"/> answers which copies of the world a window shows; the LOD keying of a wrapped world
/// (<see cref="MapDetail.Across"/>) puts the floor on a plateau at every gate window, panel shown or hidden; MapCells picks
/// the normalised cell at the seam; the HUD's camera line is normalised. The flat camera is pinned unchanged by the
/// existing camera tests (ViewModelTests, MapViewportTests, EasedZoomAndContinuousLodTests).
/// </summary>
public class CylinderCameraTests
{
    private const int World = 1024;
    private static readonly RectD Map = new(0, 52, 1280, 688);   // the design window's clear map rect

    private static Camera Wrapped(int size = World, double window = 1280)
    {
        var cam = new Camera(size, wrapX: true) { WindowWidth = window };
        cam.Clamp(Map);
        return cam;
    }

    /// <summary>Moves the (settled) camera's centre to world x <paramref name="x"/> by an immediate pan.</summary>
    private static void CentreAt(Camera cam, double x, RectD view)
    {
        cam.Stop();
        cam.Pan(-cam.DeltaX(cam.CenterX, x) * cam.Zoom, 0, view);
    }

    // ------------------------------------------------------------------ VisibleCopies

    [Theory]
    [InlineData(0.0, 1024.0, new[] { 0 })]            // exactly the world
    [InlineData(10.0, 400.0, new[] { 0 })]            // inside it
    [InlineData(-20.0, 900.0, new[] { -1, 0 })]       // straddles the western seam
    [InlineData(600.0, 1500.0, new[] { 0, 1 })]       // straddles the eastern seam
    [InlineData(-1024.0, -10.0, new[] { -1 })]        // wholly one circumference west
    [InlineData(1024.0, 1100.0, new[] { 1 })]         // wholly one circumference east (half-open: 1024 is copy 1)
    [InlineData(-30.0, 1054.0, new[] { -1, 0, 1 })]   // wider than the world (never at the floor): all three
    public void VisibleCopies_AreTheCopiesTheWindowIntersects(double x0, double x1, int[] want)
    {
        var cam = new Camera(World, wrapX: true);
        Assert.Equal(want, cam.VisibleCopies(new RectD(x0, 0, x1 - x0, 100)));
        Assert.Equal(new[] { 0 }, new Camera(World).VisibleCopies(new RectD(x0, 0, x1 - x0, 100)));   // flat: the one world
    }

    [Fact]
    public void AtTheFloor_TheWindowSpansExactlyOneCircumference_AndShowsAtMostTwoCopies()
    {
        foreach (double centre in new[] { 0.0, 3.25, 511.0, 512.0, 1000.0, 1023.75 })
        {
            Camera cam = Wrapped();
            CentreAt(cam, centre, Map);
            RectD win = cam.ScreenRectToWorld(new RectD(0, 0, 1280, 800), Map);
            Assert.Equal(World, win.W, 9);   // the floor: the window is one circumference wide
            int[] ks = cam.VisibleCopies(win);
            Assert.InRange(ks.Length, 1, 2);
            Assert.Contains(0, ks);
        }
    }

    // ------------------------------------------------------------------ x normalised, pan across the seam

    [Fact]
    public void Pan_AcrossTheSeam_WestThenEast_IsContinuous_AndEveryVisiblePointMovesOnePixelPerPixel()
    {
        Camera cam = Wrapped();
        double z = cam.Zoom;
        Assert.Equal(1280.0 / World, z);
        CentreAt(cam, 2.0, Map);
        double[] points = [1.5, 40.5, 300.5, 700.5, 980.5, 1022.5];
        double y = cam.CenterY;
        // WEST: drag the world right one screen px at a time, across the flip point (x = 0 → 1023.x).
        bool flipped = false;
        for (int step = 0; step < 8; step++)
        {
            double before = cam.CenterX;
            var screens = new double[points.Length];
            for (int i = 0; i < points.Length; i++) screens[i] = cam.WorldToScreenNearest(points[i], y, Map).X;
            cam.Pan(1, 0, Map);
            Assert.InRange(cam.CenterX, 0.0, World - 1e-9);
            Assert.Equal(-1.0 / z, cam.DeltaX(before, cam.CenterX), 9);   // the same world step, whichever side of the seam
            flipped |= cam.CenterX > before;
            for (int i = 0; i < points.Length; i++)
                Assert.Equal(screens[i] + 1.0, cam.WorldToScreenNearest(points[i], y, Map).X, 6);
        }
        Assert.True(flipped, "the pan never crossed the seam");
        // EAST: back across it.
        flipped = false;
        for (int step = 0; step < 8; step++)
        {
            double before = cam.CenterX;
            var screens = new double[points.Length];
            for (int i = 0; i < points.Length; i++) screens[i] = cam.WorldToScreenNearest(points[i], y, Map).X;
            cam.Pan(-1, 0, Map);
            Assert.InRange(cam.CenterX, 0.0, World - 1e-9);
            Assert.Equal(1.0 / z, cam.DeltaX(before, cam.CenterX), 9);
            flipped |= cam.CenterX < before;
            for (int i = 0; i < points.Length; i++)
                Assert.Equal(screens[i] - 1.0, cam.WorldToScreenNearest(points[i], y, Map).X, 6);
        }
        Assert.True(flipped, "the pan back never crossed the seam");
        Assert.Equal(2.0, cam.CenterX, 9);
    }

    [Fact]
    public void ALongPan_GoesRoundTheWorld_AndComesBackToTheSamePlace()
    {
        Camera cam = Wrapped();
        CentreAt(cam, 100.0, Map);
        double start = cam.CenterX;
        for (int i = 0; i < World; i++) cam.Pan(-cam.Zoom, 0, Map);   // one world px east per step: once round
        Assert.Equal(start, cam.CenterX, 6);
        Assert.InRange(cam.CenterX, 0.0, World - 1e-9);
    }

    // ------------------------------------------------------------------ ease and glide: the short way

    [Theory]
    [InlineData(1000.0, 20.0)]   // east across the seam: +44, never −980
    [InlineData(20.0, 1000.0)]   // west across the seam: −44, never +980
    [InlineData(100.0, 500.0)]   // no seam: +400
    public void GlidePan_EasesTheShortWayRound_BothDirections(double from, double to)
    {
        Camera cam = Wrapped();
        CentreAt(cam, from, Map);
        double shortWay = MapTopologyDelta(from, to);
        cam.GlidePan(-shortWay * cam.TargetZoom, 0, Map);
        Assert.Equal(Sim.Core.Pathing.MapTopology.WrapCoord(to, World), cam.TargetCenterX, 9);
        double travelled = 0, prev = cam.CenterX;
        int frames = 0;
        while (cam.Advance(1.0 / 60) && frames++ < 600)
        {
            double step = cam.DeltaX(prev, cam.CenterX);
            Assert.True(Math.Sign(step) == Math.Sign(shortWay) || step == 0, $"a frame stepped {step} against the short way {shortWay}");
            Assert.InRange(cam.CenterX, 0.0, World - 1e-9);
            travelled += step;
            prev = cam.CenterX;
        }
        travelled += cam.DeltaX(prev, cam.CenterX);
        Assert.True(cam.Settled);
        Assert.Equal(shortWay, travelled, 6);
        Assert.Equal(cam.TargetCenterX, cam.CenterX);
    }

    private static double MapTopologyDelta(double from, double to)
    {
        double d = to - from;
        if (d > World / 2.0) d -= World;
        if (d <= -World / 2.0) d += World;
        return d;
    }

    [Fact]
    public void CameraFocusGlideTo_ASettlementAcrossTheSeam_TakesTheShortWay_BothDirections()
    {
        WorldState world = UiSession.Start(42, 256, 4).World.Clone();
        Assert.True(world.Terrain!.WrapX);
        int size = world.Terrain.Size, y = size / 2;
        // Two settlements moved to either side of the seam (positions only: the camera reads nothing else).
        SettlementRow a = world.Settlements[0], b = world.Settlements[1];
        world.Settlements[0] = a with { SiteCell = y * size + 3 };            // x = 3.5
        world.Settlements[1] = b with { SiteCell = y * size + size - 4 };     // x = 252.5
        var cam = new Camera(size, wrapX: true) { WindowWidth = 1280 };
        cam.Clamp(Map);
        foreach ((int from, int to, double expected) in new[] { (1, 0, 7.0), (0, 1, -7.0) })
        {
            Assert.True(CameraFocus.CenterOn(cam, world, world.Settlements[from].Id.Value, Map));
            double start = cam.CenterX;
            Assert.True(CameraFocus.GlideTo(cam, world, world.Settlements[to].Id.Value, Map));
            Assert.Equal(expected, cam.DeltaX(start, cam.TargetCenterX), 9);   // the short way: 7 px, not 249
            double travelled = 0, prev = cam.CenterX;
            for (int f = 0; f < 600 && cam.Advance(1.0 / 60); f++) { travelled += cam.DeltaX(prev, cam.CenterX); prev = cam.CenterX; }
            travelled += cam.DeltaX(prev, cam.CenterX);
            Assert.Equal(expected, travelled, 6);
            Assert.Equal(OverlayMeshes.SettlementPosition(world.Settlements[to], size).X, cam.CenterX, 9);
        }
    }

    // ------------------------------------------------------------------ the floor and the y clamp

    [Fact]
    public void TheFloor_IsTheWindowWidthOverTheWorld_TheSameWithAPanelOpenOrClosed()
    {
        var cam = new Camera(World, wrapX: true) { WindowWidth = 1280 };
        RectD closed = PanelLayout.ClearMapRect, open = PanelLayout.MapRectWithContextOpen;
        Assert.True(open.W < closed.W);
        Assert.Equal(1280.0 / World, cam.MinZoom(closed));
        Assert.Equal(cam.MinZoom(closed), cam.MinZoom(open));
        // A view at the floor stays at the floor through Reframe (the panel opens and closes): no re-zoom.
        cam.Clamp(closed);
        Assert.Equal(1280.0 / World, cam.Zoom);
        cam.Reframe(closed, open, open.CenterX, open.CenterY);
        Assert.Equal(1280.0 / World, cam.Zoom);
        cam.Reframe(open, closed, closed.CenterX, closed.CenterY);
        Assert.Equal(1280.0 / World, cam.Zoom);
        // The flat camera's floor is the map rect's (control: it changes with the panel).
        var flat = new Camera(World);
        Assert.NotEqual(flat.MinZoom(closed), flat.MinZoom(new RectD(0, 52, 400, 300)));
    }

    [Fact]
    public void ATinyWorld_WhoseFloorExceedsMaxZoom_StaysAtTheFloor_NeverWiderThanOneCircumference()
    {
        var cam = new Camera(12, wrapX: true) { WindowWidth = 1280 };   // the smallest --size a cylinder accepts
        cam.Clamp(Map);
        Assert.Equal(1280.0 / 12, cam.Zoom);
        cam.ZoomToward(Map.CenterX, Map.CenterY, 1.25, Map);
        cam.ZoomAt(Map.CenterX, Map.CenterY, 0.5, Map);
        Assert.Equal(1280.0 / 12, cam.Zoom);
        Assert.Equal(1280.0 / 12, cam.TargetZoom);
        // The flat camera's ceiling is untouched (MaxZoom), whatever its floor.
        var flat = new Camera(1024);
        flat.ZoomAt(Map.CenterX, Map.CenterY, 1000, Map);
        Assert.Equal(Camera.MaxZoom, flat.Zoom);
    }

    [Fact]
    public void AResize_ReframesAtTheNewWindowsFloor()
    {
        var cam = new Camera(World, wrapX: true) { WindowWidth = 1280 };
        cam.Clamp(Map);
        var wider = new RectD(0, 52, 1920, 897);
        cam.Reframe(Map, wider, wider.CenterX, wider.CenterY, 1920);
        Assert.Equal(1920.0, cam.WindowWidth);
        Assert.Equal(1920.0 / World, cam.Zoom);   // a view at the floor re-fits to the new window's floor
    }

    /// <summary>W2 review F6: a SHRINKING resize at the floor with a right-side panel open. The view was at the OLD window's
    /// floor (1920 / W), judged at the old window's width — the map rect left of the panel (1500 px) is narrower than that
    /// window, so a floor judged at the NEW window's width (max(1280, 1500) / W) would not recognise the view as at-fit and
    /// would keep the old zoom, wider than the new floor.</summary>
    [Fact]
    public void AShrinkingResizeAtTheFloor_WithAPanelOpen_RefitsToTheNewWindowsFloor()
    {
        var from = new RectD(0, 52, 1500, 897);   // a 1920-px window, the context panel open on the right
        var to = new RectD(0, 52, 900, 688);      // the window shrunk to 1280 px, the panel still open
        var cam = new Camera(World, wrapX: true) { WindowWidth = 1920 };
        cam.Clamp(from);
        Assert.Equal(1920.0 / World, cam.Zoom);   // at the old floor
        cam.Reframe(from, to, to.CenterX, to.CenterY, 1280);
        Assert.Equal(1280.0, cam.WindowWidth);
        Assert.Equal(1280.0 / World, cam.Zoom);   // re-fitted: one circumference across the new window
        Assert.Equal(1280.0 / World, cam.TargetZoom);
        Assert.True(cam.Settled);
        // A view zoomed IN past the old floor is not at fit: it keeps its zoom (control).
        var zoomed = new Camera(World, wrapX: true) { WindowWidth = 1920 };
        zoomed.Clamp(from);
        zoomed.ZoomAt(from.CenterX, from.CenterY, 2.0, from);
        zoomed.Reframe(from, to, to.CenterX, to.CenterY, 1280);
        Assert.Equal(2.0 * 1920.0 / World, zoomed.Zoom, 12);
    }

    /// <summary>W2 review F6: a window TALLER than wide floors on the map rect's height, so the view never runs past the poles:
    /// at the floor the map rect shows exactly the world's height (y centred), never void above or below it.</summary>
    [Fact]
    public void APortraitWindow_FloorsOnTheMapRectsHeight_NeverShowingPastThePoles()
    {
        var portrait = new RectD(0, 52, 800, 1168);   // an 800 × 1280 window
        var cam = new Camera(World, wrapX: true) { WindowWidth = 800 };
        cam.Clamp(portrait);
        Assert.Equal(portrait.H / World, cam.MinZoom(portrait));
        Assert.Equal(portrait.H / World, cam.Zoom);
        Assert.True(portrait.H / cam.Zoom <= World * (1 + 1e-12), "the floor shows past the poles: " + (portrait.H / cam.Zoom));
        Assert.Equal(World / 2.0, cam.CenterY, 9);
        cam.ZoomAt(portrait.CenterX, portrait.CenterY, 0.5, portrait);   // zooming out stops at the floor
        Assert.Equal(portrait.H / World, cam.Zoom);
        // Landscape, the same rule floors on the window's width (control).
        Assert.Equal(1280.0 / World, Wrapped().MinZoom(Map));
    }

    [Fact]
    public void Y_IsStillClampedAtThePoles()
    {
        Camera cam = Wrapped();
        double halfView = Map.H / 2.0 / cam.Zoom;
        cam.Pan(0, 100000, Map);   // drag the world far down: the view runs into the north pole
        Assert.Equal(halfView, cam.CenterY, 9);
        cam.Pan(0, -200000, Map);
        Assert.Equal(World - halfView, cam.CenterY, 9);
        cam.GlidePan(0, 100000, Map);
        Assert.Equal(halfView, cam.TargetCenterY, 9);
        cam.ZoomToward(Map.CenterX, Map.Y + 1, 1.25, Map);   // a notch at the top edge cannot pull the view past the pole
        Assert.True(cam.TargetCenterY >= Map.H / 2.0 / cam.TargetZoom - 1e-9);
    }

    // ------------------------------------------------------------------ LOD bands at the floor

    private static (RectD Hidden, RectD Shown) MapRects(int w, int h, double s)
    {
        float fs = (float)s;
        var status = PanelLayout.Status with { Height = PanelLayout.Status.Height * fs, Width = w };
        float commandH = PanelLayout.Command.Height * fs;
        var command = PanelLayout.Command with { Y = h - commandH, Height = commandH, Width = w };
        float cw = PanelLayout.ContextWidth(w, fs), margin = PanelLayout.Margin * fs;
        var context = PanelLayout.Context with
        {
            X = w - cw - margin, Y = status.Height + margin, Width = cw,
            Height = Math.Max(120f, h - status.Height - commandH - 2f * margin),
        };
        return (PanelLayout.MapRect(w, h, status, command, null), PanelLayout.MapRect(w, h, status, command, context));
    }

    public static TheoryData<int, int, double> GateWindows => new()
    {
        { 1080, 640, 1.0 }, { 1280, 800, 1.0 }, { 1382, 778, 1.0 }, { 1920, 1009, 1.0 }, { 2560, 1440, 1.375 },
    };

    /// <summary>The floor is on a PLATEAU (no cross-fade) at every gate window, panel shown or hidden, on the canonical world
    /// (the World band: the whole circumference, 4096 km) and on a 256-px world (the Regional band, 1024 km) — the same
    /// band and the same zoom with the panel shown as hidden — and one wheel notch in from the floor is still that plateau.</summary>
    [Theory]
    [MemberData(nameof(GateWindows))]
    public void TheFloor_IsOnAPlateau_AtEveryGateWindow_PanelShownOrHidden(int w, int h, double s)
    {
        (RectD hidden, RectD shown) = MapRects(w, h, s);
        Assert.True(shown.W < hidden.W);
        foreach ((int size, WorldZoom band) in new[] { (1024, WorldZoom.World), (256, WorldZoom.Regional) })
        {
            var cam = new Camera(size, wrapX: true) { WindowWidth = w };
            double floor = cam.MinZoom(hidden);
            Assert.Equal(floor, cam.MinZoom(shown));
            Assert.Equal((double)w / size, floor);
            MapDetail at = MapDetail.Across(floor, w, 4.0);
            Assert.True(at.IsPlateau, $"{w}x{h} {size}px: the floor ({at.VisibleKm:0} km) is in a cross-fade");
            Assert.Equal(band, at.Band);
            MapDetail notch = MapDetail.Across(floor * 1.25, w, 4.0);
            Assert.True(notch.IsPlateau && notch.Band == band, $"{w}x{h} {size}px: one notch in from the floor left the plateau");
            // CONTROL — the measured reason the wrapped world is keyed on the WINDOW's width (a deviation from the spec's
            // "map rect's width", recorded): keyed on the map rect's width, a 256-px world's floor with the panel shown is
            // inside the Settlement cross-fade at every gate window (614 / 678 / 704 / 733 / 761 km against [551, 779]).
            if (size == 256)
                Assert.False(MapDetail.At(MapDetail.VisibleKmAcross(floor, shown.W, 4.0)).IsPlateau,
                    $"{w}x{h}: keyed on the map rect's width the shown-panel floor would be a plateau — the deviation's reason is gone");
        }
    }

    [Fact]
    public void EveryPlateau_SpansAtLeastOneWheelNotch_AndTheCrossFadeWindowsDoNotOverlap()
    {
        double fade = Math.Pow(2, MapDetail.FadeOctaves);
        double settlementTop = MapDetail.RegionalSettlementKm / fade, regionalLow = MapDetail.RegionalSettlementKm * fade;
        double regionalTop = MapDetail.WorldRegionalKm / fade, worldLow = MapDetail.WorldRegionalKm * fade;
        Assert.True(regionalLow < regionalTop, "the two cross-fade windows overlap");
        Assert.True(regionalTop / regionalLow >= 1.25, "the Regional plateau is narrower than a notch");
        foreach ((int w, _, _) in new[] { (1080, 640, 1.0), (1280, 800, 1.0), (1382, 778, 1.0), (1920, 1009, 1.0), (2560, 1440, 1.375) })
        {
            double maxZoomKm = MapDetail.VisibleKmAcross(Camera.MaxZoom, w, 4.0);
            Assert.True(settlementTop / maxZoomKm >= 1.25, $"{w}: the Settlement plateau is narrower than a notch");
            double canonicalFloorKm = MapDetail.VisibleKmAcross((double)w / 1024, w, 4.0);
            Assert.Equal(4096.0, canonicalFloorKm, 9);
            Assert.True(canonicalFloorKm / worldLow >= 1.25, "the canonical World plateau is narrower than a notch");
            double smallFloorKm = MapDetail.VisibleKmAcross((double)w / 256, w, 4.0);
            Assert.True(smallFloorKm / regionalLow >= 1.25, "the 256-px floor's Regional plateau is narrower than a notch");
        }
    }

    // ------------------------------------------------------------------ MapCells at the seam, the HUD line

    [Fact]
    public void MapCells_AtTheSeam_PicksTheNormalisedCellDrawnUnderThePointer()
    {
        Camera cam = Wrapped(256);
        cam.ZoomAt(Map.CenterX, Map.CenterY, 16.0 / cam.Zoom, Map);   // a cell 16 px wide: its footprint is its region
        CentreAt(cam, 0.0, Map);   // the seam at the map rect's centre
        double z = cam.Zoom;
        Assert.Equal(16.0, z, 12);
        int size = 256, yCell = (int)cam.CenterY;
        double sy = cam.WorldToScreen(0, yCell + 0.5, Map).Y;
        // Just west of the seam: world x −0.5 is column 255; just east: x +0.5 is column 0.
        int west = MapCells.At(cam, Map, Map.CenterX - 0.5 * z, sy, size);
        int east = MapCells.At(cam, Map, Map.CenterX + 0.5 * z, sy, size);
        Assert.Equal(yCell * size + 255, west);
        Assert.Equal(yCell * size + 0, east);
        // Each cell's footprint, at the copy under the pointer, holds the pointer (the cell drawn there).
        RectD rw = MapCells.Region(cam, Map, west, size, Map.CenterX - 0.5 * z, sy);
        RectD re = MapCells.Region(cam, Map, east, size, Map.CenterX + 0.5 * z, sy);
        Assert.True(rw.Contains(Map.CenterX - 0.5 * z, sy) && rw.Right <= Map.CenterX + 1e-9, $"west footprint {rw}");
        Assert.True(re.Contains(Map.CenterX + 0.5 * z, sy) && re.X >= Map.CenterX - 1e-9, $"east footprint {re}");
        // A pointer a whole circumference west picks the same cell (any copy is the same place); the flat camera refuses a
        // point off its map (control).
        Assert.Equal(east, MapCells.At(cam, Map, Map.CenterX + 0.5 * z - size * z, sy, size));
        var flat = new Camera(size);
        flat.ZoomAt(Map.CenterX, Map.CenterY, 4, Map);
        Assert.Equal(-1, MapCells.At(flat, Map, -100000, sy, size));
    }

    [Fact]
    public void HudCameraLine_IsNormalised_OnAWrappedWorld()
    {
        Assert.Equal("camera (0, 10) zoom 1.25x", HudModel.CameraLine(1023.6, 10, 1.25, 1024));
        Assert.Equal("camera (1023, 10) zoom 1.25x", HudModel.CameraLine(1023.4, 10, 1.25, 1024));
        Assert.Equal("camera (5, 10) zoom 1.25x", HudModel.CameraLine(-1019, 10, 1.25, 1024));
        Assert.Equal(HudModel.CameraLine(1023.6, 10, 1.25), HudModel.CameraLine(1023.6, 10, 1.25, 0));   // flat: unchanged
    }
}
