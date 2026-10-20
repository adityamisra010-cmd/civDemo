using System.Security.Cryptography;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

// T1.7 automated acceptance: view-model/logic only — pure camera math, color
// mapping, texture-bake byte determinism. No Game, no window, CI-headless.
public class CameraTests
{
    private const int W = 1280, H = 800;

    [Fact]
    public void ScreenToWorld_RoundTrips_ThroughWorldToScreen()
    {
        var cam = new Camera(1024);
        cam.ZoomAt(400, 300, 2.5, W, H);
        cam.Pan(37, -90, W, H);

        (double wx, double wy) = cam.ScreenToWorld(777, 123, W, H);
        (double sx, double sy) = cam.WorldToScreen(wx, wy, W, H);
        Assert.Equal(777.0, sx, precision: 9);
        Assert.Equal(123.0, sy, precision: 9);
    }

    [Fact]
    public void ZoomAt_KeepsTheWorldPointUnderTheCursorFixed()
    {
        // THE zoom invariant: the terrain under the mouse must not slide.
        var cam = new Camera(1024);
        (double beforeX, double beforeY) = cam.ScreenToWorld(900, 650, W, H);
        cam.ZoomAt(900, 650, 1.6, W, H);
        (double afterX, double afterY) = cam.ScreenToWorld(900, 650, W, H);
        Assert.Equal(beforeX, afterX, precision: 9);
        Assert.Equal(beforeY, afterY, precision: 9);

        // And again zooming OUT (still clamped inside bounds).
        cam.ZoomAt(100, 100, 1.0 / 1.3, W, H);
        (double outX, double outY) = cam.ScreenToWorld(100, 100, W, H);
        (double backX, double backY) = cam.ScreenToWorld(100, 100, W, H);
        Assert.Equal(outX, backX, precision: 12);
        Assert.Equal(outY, backY, precision: 12);
    }

    [Fact]
    public void Pan_IsClampedToWorldBounds()
    {
        var cam = new Camera(1024);
        cam.ZoomAt(W / 2.0, H / 2.0, 4.0, W, H); // zoomed in enough to pan
        cam.Pan(1e9, 1e9, W, H);                 // slam toward negative world
        (double wx, double wy) = cam.ScreenToWorld(0, 0, W, H);
        Assert.True(wx >= -1e-9, $"view left edge escaped the world: {wx}");
        Assert.True(wy >= -1e-9, $"view top edge escaped the world: {wy}");

        cam.Pan(-1e9, -1e9, W, H);
        (double rx, double ry) = cam.ScreenToWorld(W, H, W, H);
        Assert.True(rx <= 1024 + 1e-9, $"view right edge escaped the world: {rx}");
        Assert.True(ry <= 1024 + 1e-9, $"view bottom edge escaped the world: {ry}");
    }

    [Fact]
    public void Zoom_IsClampedToMinAndMax()
    {
        var cam = new Camera(1024);
        cam.ZoomAt(0, 0, 1e12, W, H);
        Assert.Equal(Camera.MaxZoom, cam.Zoom);
        cam.ZoomAt(0, 0, 1e-12, W, H);
        Assert.Equal(cam.MinZoom(W, H), cam.Zoom);
    }

    // ------------------------------------------------------------------ P1: the map rect (an offset viewport)

    /// <summary>The design window's map rects (PanelLayout): the band between the bars, and that band left of an open
    /// context panel — the rect's origin is NOT the window's (it starts under the 52 px status band).</summary>
    private static readonly RectD Band = PanelLayout.ClearMapRect;
    private static readonly RectD BandWithPanel = PanelLayout.MapRectWithContextOpen;

    [Fact]
    public void MapRect_ScreenToWorld_RoundTrips_ThroughWorldToScreen_InsideTheRect()
    {
        foreach (RectD map in new[] { Band, BandWithPanel, new RectD(37.5, 91.25, 611, 433) })
        {
            var cam = new Camera(1024);
            cam.ZoomAt(map.CenterX, map.CenterY, 2.5, map);
            cam.Pan(37, -90, map);
            foreach ((double sx, double sy) in new[] { (map.X + 1, map.Y + 1), (map.CenterX, map.CenterY), (map.Right - 1, map.Bottom - 1), (map.X + 0.3 * map.W, map.Y + 0.8 * map.H) })
            {
                (double wx, double wy) = cam.ScreenToWorld(sx, sy, map);
                (double bx, double by) = cam.WorldToScreen(wx, wy, map);
                Assert.Equal(sx, bx, 9);
                Assert.Equal(sy, by, 9);
            }
            // The rect's centre shows the camera's centre: the origin offset is part of the projection.
            (double cx, double cy) = cam.WorldToScreen(cam.CenterX, cam.CenterY, map);
            Assert.Equal(map.CenterX, cx, 12);
            Assert.Equal(map.CenterY, cy, 12);
        }
    }

    [Fact]
    public void MapRect_ZoomAt_KeepsTheWorldPointUnderTheCursorFixed_WithAnOffsetOrigin()
    {
        RectD map = BandWithPanel;
        var cam = new Camera(1024);
        cam.ZoomAt(map.CenterX, map.CenterY, 3.0, map);   // zoomed in: the clamp never reaches these cursors' world points
        foreach ((double sx, double sy, double f) in new[] { (300.0, 400.0, 1.6), (700.0, 120.0, 1.25), (120.0, 600.0, 1 / 1.3) })
        {
            (double bx, double by) = cam.ScreenToWorld(sx, sy, map);
            cam.ZoomAt(sx, sy, f, map);
            (double ax, double ay) = cam.ScreenToWorld(sx, sy, map);
            Assert.Equal(bx, ax, 9);
            Assert.Equal(by, ay, 9);
        }
    }

    /// <summary>THE FIT-TO-MAPRECT PIN: at the fit zoom the whole world is inside the map rect — no world under the
    /// status band, the command bar or an open context panel — and it spans the rect's shorter side exactly. The
    /// control: the pre-P1 camera fitted the WINDOW, and its fit put world rows under a bar at every one of these
    /// windows (112 px of world height at 1280x800).</summary>
    [Theory]
    [InlineData(1280, 800, 1.0)]
    [InlineData(1920, 1009, 1.0)]
    [InlineData(2560, 1440, 1.375)]
    [InlineData(1080, 640, 1.0)]
    [InlineData(1080, 1000, 1.0)]   // tall enough that the panel-open rect is narrower than it is high
    public void MapRect_AtTheFit_NoWorldSitsUnderTheBars_AndTheWholeWorldIsInTheRect(int w, int h, double scale)
    {
        float s = (float)scale;
        var status = new PanelRect("##status", 0, 0, w, 52 * s);
        var command = new PanelRect("##command", 0, h - 60 * s, w, 60 * s);
        float cw = PanelLayout.ContextWidth(w, s);
        var context = new PanelRect("##context", w - cw - 12 * s, 64 * s, cw, h - 112 * s - 24 * s);
        foreach (PanelRect? panel in new PanelRect?[] { null, context })
        {
            RectD map = PanelLayout.MapRect(w, h, status, command, panel);
            var cam = new Camera(1024);
            cam.ZoomAt(map.CenterX, map.CenterY, 1e-12, map);   // slam to the fit
            Assert.Equal(cam.MinZoom(map), cam.Zoom);
            for (int pass = 0; pass < 2; pass++)
            {
                (double x0, double y0) = cam.WorldToScreen(0, 0, map);
                (double x1, double y1) = cam.WorldToScreen(1024, 1024, map);
                Assert.True(y0 >= map.Y - 1e-9 && y1 <= map.Bottom + 1e-9, $"world rows under a bar at {w}x{h}: {y0}..{y1} vs {map.Y}..{map.Bottom}");
                Assert.True(x0 >= map.X - 1e-9 && x1 <= map.Right + 1e-9, $"world columns outside the map rect at {w}x{h}: {x0}..{x1} vs {map.X}..{map.Right}");
                Assert.Equal(Math.Min(map.W, map.H), x1 - x0, 9);   // a FIT: the shorter side, exactly
                cam.Pan(1e6, -1e6, map);                             // and a fitted world cannot be panned off it
            }
        }
        var old = new Camera(1024);
        old.ZoomAt(0, 0, 1e-12, w, h);
        double oy0 = old.WorldToScreen(0, 0, w, h).Y, oy1 = old.WorldToScreen(1024, 1024, w, h).Y;
        Assert.True(oy0 < status.Height || oy1 > command.Y, $"control: the window fit kept the world off the bars ({oy0}..{oy1})");
    }

    [Fact]
    public void MapRect_Clamp_KeepsTheRectOverTheWorld_AndEveryEdgeOfTheWorldReachesTheClearMap()
    {
        RectD map = BandWithPanel;
        var cam = new Camera(1024);
        cam.ZoomAt(map.CenterX, map.CenterY, 4.0, map);
        cam.Pan(1e9, 1e9, map);   // toward the world's top-left
        (double tlx, double tly) = cam.WorldToScreen(0, 0, map);
        Assert.Equal(map.X, tlx, 9);   // the world's corner at the clear map's corner, not under the status band
        Assert.Equal(map.Y, tly, 9);
        cam.Pan(-1e9, -1e9, map);
        (double brx, double bry) = cam.WorldToScreen(1024, 1024, map);
        Assert.Equal(map.Right, brx, 9);   // the world's right edge at the panel's edge, not under it
        Assert.Equal(map.Bottom, bry, 9);  // its bottom edge on the command bar's top edge
        // The control: the window clamp parked the world's corner at the window's (0, 0) — under the status band.
        var old = new Camera(1024);
        old.ZoomAt(640, 400, 4.0, W, H);
        old.Pan(1e9, 1e9, W, H);
        Assert.Equal(0.0, old.WorldToScreen(0, 0, W, H).Y, 9);
    }

    /// <summary>"Opening the context panel re-fits the view without moving the world point under the cursor": at the
    /// same zoom the reframe moves nothing at all — every world point keeps its screen position — while the map rect
    /// narrows to the panel's edge.</summary>
    [Fact]
    public void Reframe_OpeningThePanel_KeepsTheWorldPointUnderTheCursor_AndTheWholePicture()
    {
        var cam = new Camera(1024);
        cam.ZoomAt(Band.CenterX, Band.CenterY, 3.0, Band);
        cam.Pan(-40, 25, Band);
        (double X, double Y)[] probes = [(300, 400), (100, 90), (820, 700), (512, 512)];
        var before = new (double X, double Y)[probes.Length];
        for (int i = 0; i < probes.Length; i++) before[i] = cam.ScreenToWorld(probes[i].X, probes[i].Y, Band);
        double zoom = cam.Zoom;

        cam.Reframe(Band, BandWithPanel, 300, 400);

        Assert.Equal(zoom, cam.Zoom);
        for (int i = 0; i < probes.Length; i++)
        {
            (double wx, double wy) = cam.ScreenToWorld(probes[i].X, probes[i].Y, BandWithPanel);
            Assert.Equal(before[i].X, wx, 9);
            Assert.Equal(before[i].Y, wy, 9);
        }
        // And closing it again is the same identity the other way.
        cam.Reframe(BandWithPanel, Band, 300, 400);
        (double bx, double by) = cam.ScreenToWorld(300, 400, Band);
        Assert.Equal(before[0].X, bx, 9);
        Assert.Equal(before[0].Y, by, 9);
    }

    /// <summary>A view at the fit RE-FITS: when the rect narrows (a panel opens in a tall window) the zoom falls to the
    /// new fit and the whole world is framed in the new rect; when it widens again the zoom rises back to that fit.
    /// A zoom below a new, larger fit is raised to it.</summary>
    [Fact]
    public void Reframe_AFitViewReFitsTheNewRect_AndAZoomBelowTheNewFitIsRaisedToIt()
    {
        var status = new PanelRect("##status", 0, 0, 1080, 52);
        var command = new PanelRect("##command", 0, 940, 1080, 60);
        RectD wide = PanelLayout.MapRect(1080, 1000, status, command, null);                                     // 1080 x 888
        RectD narrow = PanelLayout.MapRect(1080, 1000, status, command, new PanelRect("##context", 648, 64, 420, 864)); // 648 x 888
        var cam = new Camera(1024);
        cam.Clamp(wide);
        cam.ZoomAt(500, 500, 1e-12, wide);
        Assert.Equal(888 / 1024.0, cam.Zoom, 12);

        cam.Reframe(wide, narrow, 900, 500);   // the cursor over the panel that just opened
        Assert.Equal(cam.MinZoom(narrow), cam.Zoom);
        Assert.Equal(648 / 1024.0, cam.Zoom, 12);
        (double x0, double y0) = cam.WorldToScreen(0, 0, narrow);
        (double x1, double y1) = cam.WorldToScreen(1024, 1024, narrow);
        Assert.True(x0 >= narrow.X - 1e-9 && x1 <= narrow.Right + 1e-9 && y0 >= narrow.Y - 1e-9 && y1 <= narrow.Bottom + 1e-9);

        cam.Reframe(narrow, wide, 300, 300);
        Assert.Equal(cam.MinZoom(wide), cam.Zoom);

        var below = new Camera(1024);
        below.ZoomAt(300, 300, 1e-12, narrow);
        below.ZoomAt(300, 300, 1.1, narrow);   // above the narrow fit, below the wide one
        Assert.True(below.Zoom < below.MinZoom(wide));
        below.Reframe(narrow, wide, 300, 300);
        Assert.Equal(below.MinZoom(wide), below.Zoom);
    }

    /// <summary>The render matrix is built from <see cref="Camera.Transform"/>: it must be WorldToScreen, exactly.</summary>
    [Fact]
    public void Transform_IsWorldToScreen_ThroughTheMapRect()
    {
        foreach (RectD map in new[] { Band, BandWithPanel, Camera.Window(W, H) })
        {
            var cam = new Camera(1024);
            cam.ZoomAt(map.CenterX + 50, map.CenterY - 30, 2.75, map);
            (double scale, double ox, double oy) = cam.Transform(map);
            foreach ((double wx, double wy) in new[] { (0.0, 0.0), (1024.0, 1024.0), (333.3, 701.9), (cam.CenterX, cam.CenterY) })
            {
                (double sx, double sy) = cam.WorldToScreen(wx, wy, map);
                Assert.Equal(sx, wx * scale + ox, 9);
                Assert.Equal(sy, wy * scale + oy, 9);
            }
        }
    }

    /// <summary>The (width, height) overloads ARE the rect at the window's origin — bit for bit, through a run of every
    /// operation — so every headless paint and preview that frames the full window computes what it did before P1.</summary>
    [Fact]
    public void WindowOverloads_AreTheMapRectAtTheOrigin_BitForBit()
    {
        var a = new Camera(1024);
        var b = new Camera(1024);
        RectD win = Camera.Window(W, H);
        a.ZoomAt(400, 300, 2.5, W, H); b.ZoomAt(400, 300, 2.5, win);
        a.Pan(37, -90, W, H); b.Pan(37, -90, win);
        a.ZoomAt(1100, 700, 1 / 1.7, W, H); b.ZoomAt(1100, 700, 1 / 1.7, win);
        a.Pan(-1e5, 3, W, H); b.Pan(-1e5, 3, win);
        a.Clamp(W, H); b.Clamp(win);
        Assert.Equal(a.Zoom, b.Zoom);
        Assert.Equal(a.CenterX, b.CenterX);
        Assert.Equal(a.CenterY, b.CenterY);
        Assert.Equal(a.MinZoom(W, H), b.MinZoom(win));
        Assert.Equal(a.ScreenToWorld(777, 123, W, H), b.ScreenToWorld(777, 123, win));
        Assert.Equal(a.WorldToScreen(5, 1000, W, H), b.WorldToScreen(5, 1000, win));
    }
}

public class PaletteTests
{
    [Fact]
    public void LandGradient_IsSmooth_NoAdjacentJumps()
    {
        // Smooth shading mandate (D-023): sample the land ramp densely; adjacent
        // samples may differ by only a few units per channel — a banding bug
        // (e.g. quantized stops) would produce a big step somewhere.
        TerrainPalette.Rgba previous = TerrainPalette.Land(0.0);
        for (int i = 1; i <= 1000; i++)
        {
            TerrainPalette.Rgba current = TerrainPalette.Land(i / 1000.0);
            Assert.True(Math.Abs(current.R - previous.R) <= 3
                     && Math.Abs(current.G - previous.G) <= 3
                     && Math.Abs(current.B - previous.B) <= 3,
                $"gradient jump at t={i / 1000.0}: {previous} -> {current}");
            previous = current;
        }
    }

    [Fact]
    public void WaterAndLand_AreDistinct_AndRiversDistinctFromDeepWater()
    {
        TerrainPalette.Rgba coast = TerrainPalette.Land(0.0);
        TerrainPalette.Rgba shallow = TerrainPalette.Water(0.0);
        // Channel distance large enough that the coastline reads at a glance.
        int distance = Math.Abs(coast.R - shallow.R) + Math.Abs(coast.G - shallow.G)
                     + Math.Abs(coast.B - shallow.B);
        Assert.True(distance > 60, $"water/land too similar: {distance}");

        TerrainPalette.Rgba river = TerrainPalette.RiverColor;
        TerrainPalette.Rgba deep = TerrainPalette.Water(1.0);
        Assert.True(Math.Abs(river.R - deep.R) + Math.Abs(river.G - deep.G)
                  + Math.Abs(river.B - deep.B) > 60, "rivers vanish into deep water");
    }

    [Fact]
    public void OutOfRangeInputs_Clamp_NeverThrow()
    {
        Assert.Equal(TerrainPalette.Land(0.0), TerrainPalette.Land(-5.0));
        Assert.Equal(TerrainPalette.Land(1.0), TerrainPalette.Land(7.0));
        Assert.Equal(TerrainPalette.Water(1.0), TerrainPalette.Water(42.0));
    }
}

public class TerrainBakerTests
{
    private static TerrainSet DevTerrain(ulong seed)
    {
        using var stream = global::Sim.Data.DataFiles.OpenWorldgen();
        WorldgenConfig cfg = WorldgenConfigLoader.Load(stream) with { SizePx = 256 };
        return Worldgen.Generate(cfg, seed);
    }

    [Fact]
    public void Bake_IsByteDeterministic_ForAFixedSeed()
    {
        // Two independent generations + bakes of the same seed: byte-identical.
        byte[] a = TerrainBaker.Bake(DevTerrain(42));
        byte[] b = TerrainBaker.Bake(DevTerrain(42));
        Assert.Equal(SHA256.HashData(a), SHA256.HashData(b));
        Assert.Equal(256 * 256 * 4, a.Length);
    }

    [Fact]
    public void Bake_DifferentSeeds_DifferentPixels_AndAllOpaque()
    {
        byte[] a = TerrainBaker.Bake(DevTerrain(42));
        byte[] c = TerrainBaker.Bake(DevTerrain(43));
        Assert.NotEqual(SHA256.HashData(a), SHA256.HashData(c));
        for (int i = 3; i < a.Length; i += 4)
            if (a[i] != 0xFF) Assert.Fail($"non-opaque texel at {i / 4}");
    }

    [Fact]
    public void Bake_DoesNotPaintRivers_TexelsAreUnderlyingTerrain()
    {
        // T1.7 re-gate: rivers left the texture (raster texels staircase at
        // zoom) — every river cell now shows its underlying land color; the
        // vector RiverMesh draws the river on top.
        TerrainSet terrain = DevTerrain(42);
        byte[] pixels = TerrainBaker.Bake(terrain);
        ReadOnlySpan<double> elevation = terrain.Elevation;
        ReadOnlySpan<double> rivers = terrain.Rivers;

        double maxElev = double.MinValue;
        for (int i = 0; i < elevation.Length; i++) maxElev = Math.Max(maxElev, elevation[i]);
        double landSpan = maxElev - terrain.SeaLevel;

        int riverCells = 0;
        for (int i = 0; i < rivers.Length; i++)
        {
            if (rivers[i] < 0.5) continue;
            riverCells++; // river cells are on land (T1.2 invariant)
            TerrainPalette.Rgba expected =
                TerrainPalette.Land((elevation[i] - terrain.SeaLevel) / landSpan);
            Assert.Equal(expected.R, pixels[i * 4]);
            Assert.Equal(expected.G, pixels[i * 4 + 1]);
            Assert.Equal(expected.B, pixels[i * 4 + 2]);
        }
        Assert.True(riverCells > 0, "dev terrain generated no river cells — vacuous");
    }
}

public class RiverMeshTests
{
    private static TerrainSet DevTerrain(ulong seed)
    {
        using var stream = global::Sim.Data.DataFiles.OpenWorldgen();
        WorldgenConfig cfg = WorldgenConfigLoader.Load(stream) with { SizePx = 256 };
        return Worldgen.Generate(cfg, seed);
    }

    [Fact]
    public void Build_IsDeterministic_SixVerticesPerSegment()
    {
        TerrainSet terrain = DevTerrain(42);
        RiverMesh.Vertex[] a = RiverMesh.Build(terrain);
        RiverMesh.Vertex[] b = RiverMesh.Build(terrain);
        Assert.Equal(a, b);
        Assert.True(a.Length > 0, "no river geometry — vacuous");
        Assert.Equal(0, a.Length % 6); // two triangles per segment quad
    }

    [Fact]
    public void WidthForRank_FallsMonotonically_WithinTuneBounds()
    {
        const int count = 12;
        double previous = double.MaxValue;
        for (int rank = 0; rank < count; rank++)
        {
            double width = RiverMesh.WidthForRank(rank, count);
            Assert.True(width <= previous, $"width rose at rank {rank}");
            Assert.InRange(width, RiverMesh.MinWidthWorldPx, RiverMesh.MaxWidthWorldPx);
            previous = width;
        }
        Assert.Equal(RiverMesh.MaxWidthWorldPx, RiverMesh.WidthForRank(0, count));
        Assert.Equal(RiverMesh.MinWidthWorldPx, RiverMesh.WidthForRank(count - 1, count));
    }

    [Fact]
    public void PolylineVertices_TransformToScreen_ConsistentlyWithTheCamera()
    {
        // The polyline->screen path the renderer uses: world vertex through the
        // camera transform. Pinned against hand-computed values and the
        // round-trip identity.
        TerrainSet terrain = DevTerrain(42);
        ReadOnlySpan<int> line = terrain.RiverPolyline(0);
        RiverMesh.Vertex head = RiverMesh.CellCenter(line[0], terrain.Size);
        Assert.Equal(line[0] % terrain.Size + 0.5, head.X);
        Assert.Equal(line[0] / terrain.Size + 0.5, head.Y);

        var cam = new Camera(terrain.Size);
        cam.ZoomAt(300, 200, 3.0, 1280, 800);
        (double sx, double sy) = cam.WorldToScreen(head.X, head.Y, 1280, 800);
        (double wx, double wy) = cam.ScreenToWorld(sx, sy, 1280, 800);
        Assert.Equal(head.X, wx, precision: 9);
        Assert.Equal(head.Y, wy, precision: 9);

        // Hand-computed: screen = (world - center) * zoom + viewport/2.
        Assert.Equal((head.X - cam.CenterX) * cam.Zoom + 640.0, sx, precision: 9);
        Assert.Equal((head.Y - cam.CenterY) * cam.Zoom + 400.0, sy, precision: 9);
    }

    // --- D-A3: screen-clamped river width (docs/art-gate-defects.md) --------

    [Fact]
    public void ScreenWidth_RankOrdering_HoldsAtEveryZoom_AndStaysInsideTheClamps()
    {
        // The packet's constraint, pinned as a property over a zoom sweep
        // spanning world-fit to the 32x maximum: a higher-discharge river
        // (lower rank index) is NEVER thinner on screen than a lower one,
        // and every width sits inside [MinScreenWidthPx, MaxScreenWidthPx].
        const int count = 12;
        foreach (double zoom in new[] { 0.25, 0.5, 1.0, 2.0, 4.0, 8.0, 16.0, 32.0 })
        {
            double previous = double.MaxValue;
            for (int rank = 0; rank < count; rank++)
            {
                double w = RiverMesh.ScreenWidthForRank(rank, count, zoom);
                Assert.True(w <= previous,
                    $"rank {rank} wider on screen than rank {rank - 1} at zoom {zoom}");
                Assert.InRange(w, RiverMesh.MinScreenWidthPx, RiverMesh.MaxScreenWidthPx);
                previous = w;
            }
        }
    }

    [Fact]
    public void ScreenWidth_IsFlatAtTheEnds_AndScalesInTheMiddle()
    {
        const int count = 12;
        // Zoomed far out: everything rides the hairline floor — flat, but
        // still weakly ordered (the un-ranking is the floor's cost, stated).
        Assert.Equal(RiverMesh.MinScreenWidthPx, RiverMesh.ScreenWidthForRank(0, count, 0.25));
        Assert.Equal(RiverMesh.MinScreenWidthPx, RiverMesh.ScreenWidthForRank(count - 1, count, 0.25));
        // Zoomed far in: the ceiling holds — a 2.4 world-px river at 32x
        // would be 76.8 px unclamped; it draws at MaxScreenWidthPx.
        Assert.Equal(RiverMesh.MaxScreenWidthPx, RiverMesh.ScreenWidthForRank(0, count, 32.0));
        // Middle band: unclamped, so the response is exactly world x zoom —
        // approach still reads as approach.
        Assert.Equal(RiverMesh.WidthForRank(3, count) * 2.0,
            RiverMesh.ScreenWidthForRank(3, count, 2.0), precision: 12);
        // And at 1:1 the defaults are inside the clamps, so the legacy
        // Build(terrain) mesh is bit-identical to Build(terrain, 1.0).
        TerrainSet terrain = DevTerrain(42);
        Assert.Equal(RiverMesh.Build(terrain), RiverMesh.Build(terrain, 1.0));
    }

    [Fact]
    public void Build_AtZoom_LandsEachRankAtItsClampedScreenWidth()
    {
        // The mesh stays in world units; after the camera scale the quad must
        // span exactly ScreenWidthForRank on screen. Measure the built quad's
        // half-width directly: for a segment, the first two vertices sit a
        // half-width either side of the axis, so their distance is the world
        // width; times zoom = screen width.
        TerrainSet terrain = DevTerrain(42);
        foreach (double zoom in new[] { 0.5, 32.0 })
        {
            RiverMesh.Vertex[] mesh = RiverMesh.Build(terrain, zoom);
            // Rank 0 owns the first quad. In LineGeometry's layout vertices
            // 0 and 2 (a1/a2) are the two corners ACROSS the strip at the
            // segment start — their distance is the full world width.
            double dx = mesh[2].X - mesh[0].X, dy = mesh[2].Y - mesh[0].Y;
            double worldWidth = Math.Sqrt(dx * dx + dy * dy);
            double screenWidth = worldWidth * zoom;
            Assert.Equal(RiverMesh.ScreenWidthForRank(0, terrain.RiverPolylineCount, zoom),
                screenWidth, precision: 9);
        }
    }
}
