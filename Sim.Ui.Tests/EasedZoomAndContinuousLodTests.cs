using System.Globalization;
using Microsoft.Xna.Framework.Input;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.Headless;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Tests;

/// <summary>
/// P2 (M5 map workstream) — THE EASED CAMERA, pure. The wheel and the keys move a TARGET view; <see cref="Camera.Advance"/>
/// eases the shown view toward it by frame-rate-independent exponential smoothing of the visible world rect (centre and
/// inverse zoom by the same fraction). Pinned: the world point under the cursor is fixed once the ease settles — and on
/// every frame of it; N frames of dt/N equal one frame of dt; the ease never overshoots and snaps exactly onto the target;
/// a settled camera is left bit-for-bit alone; the immediate operations leave the camera settled; a drag moves both
/// views together.
/// </summary>
public class EasedCameraTests
{
    private static readonly RectD Map = new(0, 52, 1280, 688);   // the 1280x800 design window's map rect

    /// <summary>A camera over the canonical-size world, settled at <paramref name="zoom"/> centred on (cx, cy).</summary>
    private static Camera At(double zoom, double cx, double cy, RectD? view = null)
    {
        RectD v = view ?? Map;
        var cam = new Camera(1024);
        cam.Clamp(v);
        cam.ZoomAt(v.CenterX, v.CenterY, zoom / cam.Zoom, v);
        cam.Pan((cam.CenterX - cx) * cam.Zoom, (cam.CenterY - cy) * cam.Zoom, v);
        Assert.True(cam.Settled);
        return cam;
    }

    private static int SettleFrames(Camera cam, double dt = 1.0 / 60, int max = 2000)
    {
        for (int i = 0; i < max; i++) if (!cam.Advance(dt)) return i + 1;
        return -1;
    }

    [Fact]
    public void TheWheel_MovesOnlyTheTarget_AndTheShownViewEasesToItExactly()
    {
        Camera cam = At(3.0, 500, 480);
        double z0 = cam.Zoom, x0 = cam.CenterX, y0 = cam.CenterY;
        cam.ZoomToward(900, 300, Math.Pow(1.25, 3), Map);
        Assert.Equal(z0, cam.Zoom);
        Assert.Equal(x0, cam.CenterX);
        Assert.Equal(y0, cam.CenterY);
        Assert.Equal(z0 * Math.Pow(1.25, 3), cam.TargetZoom, 12);
        Assert.False(cam.Settled);
        Assert.True(cam.Advance(1.0 / 60));
        Assert.True(cam.Zoom > z0 && cam.Zoom < cam.TargetZoom, $"one frame: {cam.Zoom}");
        int frames = SettleFrames(cam);
        Assert.InRange(frames, 10, 120);   // it glides (not a jump), and it ends
        Assert.True(cam.Settled);
        Assert.Equal(cam.TargetZoom, cam.Zoom);   // snapped exactly onto the target
        Assert.Equal(cam.TargetCenterX, cam.CenterX);
        Assert.Equal(cam.TargetCenterY, cam.CenterY);
    }

    /// <summary>THE ACCEPTANCE PROPERTY: the world point under the cursor is fixed once the ease settles — over many cursors,
    /// zoom factors in and out, starting zooms and map rects, where the clamp does not intervene; and (the eased visible
    /// rect) on every frame of the ease as well, to 1e-9 world px.</summary>
    [Fact]
    public void TheWorldPointUnderTheCursor_IsFixedOnceTheEaseSettles_AndOnEveryFrameOfIt()
    {
        RectD[] views = [Map, new(0, 52, 848, 688), new(0, 56, 1920, 897)];
        double[] factors = [1.25, Math.Pow(1.25, 4), 1 / 1.25, Math.Pow(1.25, -3)];
        int checkedRuns = 0;
        foreach (RectD v in views)
            foreach (double start in new[] { 2.0, 5.0, 12.0 })
                foreach (double factor in factors)
                    for (int c = 0; c < 5; c++)
                    {
                        double sx = v.X + v.W * (0.15 + 0.17 * c), sy = v.Y + v.H * (0.8 - 0.14 * c);
                        Camera cam = At(start, 512, 512, v);
                        (double ax, double ay) = cam.ScreenToWorld(sx, sy, v);
                        cam.ZoomToward(sx, sy, factor, v);
                        // Only where the clamp leaves the target alone (the clamp wins, as it did for the immediate zoom).
                        double tz = cam.TargetZoom;
                        if (Math.Abs(cam.TargetCenterX - (ax - (sx - v.CenterX) / tz)) > 1e-9
                            || Math.Abs(cam.TargetCenterY - (ay - (sy - v.CenterY) / tz)) > 1e-9) continue;
                        while (cam.Advance(1.0 / 60))
                        {
                            (double fx, double fy) = cam.ScreenToWorld(sx, sy, v);
                            Assert.Equal(ax, fx, 9);
                            Assert.Equal(ay, fy, 9);
                        }
                        Assert.True(cam.Settled);
                        (double wx, double wy) = cam.ScreenToWorld(sx, sy, v);
                        Assert.Equal(ax, wx, 9);
                        Assert.Equal(ay, wy, 9);
                        checkedRuns++;
                    }
        Assert.True(checkedRuns >= 120, $"only {checkedRuns} runs were free of the clamp");
    }

    /// <summary>A run of notches at one cursor, several frames apart (the target moves while the view eases): the point
    /// under the cursor stays put on every frame and once settled.</summary>
    [Fact]
    public void NotchesArrivingDuringTheEase_KeepThePointUnderTheCursor()
    {
        Camera cam = At(2.5, 480, 520);
        double sx = 700, sy = 500;
        (double ax, double ay) = cam.ScreenToWorld(sx, sy, Map);
        for (int notch = 0; notch < 6; notch++)
        {
            cam.ZoomToward(sx, sy, 1.25, Map);
            for (int f = 0; f < 4; f++)
            {
                cam.Advance(1.0 / 60);
                (double fx, double fy) = cam.ScreenToWorld(sx, sy, Map);
                Assert.Equal(ax, fx, 9);
                Assert.Equal(ay, fy, 9);
            }
        }
        Assert.True(SettleFrames(cam) > 0);
        Assert.Equal(2.5 * Math.Pow(1.25, 6), cam.Zoom, 9);
        (double wx, double wy) = cam.ScreenToWorld(sx, sy, Map);
        Assert.Equal(ax, wx, 9);
        Assert.Equal(ay, wy, 9);
    }

    [Theory]
    [InlineData(30.0)]
    [InlineData(60.0)]
    [InlineData(144.0)]
    [InlineData(240.0)]
    public void Advance_IsFrameRateIndependent(double hz)
    {
        // The same target, 0.1 s of easing: one frame of 0.1 s, or 0.1 s at hz frames per second — the same view.
        Camera one = At(2.0, 600, 400), many = At(2.0, 600, 400);
        foreach (Camera c in new[] { one, many }) { c.ZoomToward(300, 600, 5.0, Map); c.GlidePan(-200, 90, Map); }
        Assert.True(one.Advance(0.1));
        int n = (int)Math.Round(hz * 0.1);
        for (int i = 0; i < n; i++) many.Advance(0.1 / n);
        Assert.False(many.Settled);   // still easing: the comparison is not of two snapped views
        Assert.Equal(one.Zoom, many.Zoom, 9);
        Assert.Equal(one.CenterX, many.CenterX, 9);
        Assert.Equal(one.CenterY, many.CenterY, 9);
    }

    [Fact]
    public void Advance_NeverOvershoots_AndOnASettledCameraChangesNothing()
    {
        Camera cam = At(4.0, 300, 700);
        cam.ZoomToward(200, 200, 0.4, Map);
        cam.GlidePan(150, -80, Map);
        double gz = Math.Abs(1 / cam.TargetZoom - 1 / cam.Zoom), gx = Math.Abs(cam.TargetCenterX - cam.CenterX), gy = Math.Abs(cam.TargetCenterY - cam.CenterY);
        int sz = Math.Sign(1 / cam.TargetZoom - 1 / cam.Zoom), sxs = Math.Sign(cam.TargetCenterX - cam.CenterX);
        while (cam.Advance(1.0 / 60))
        {
            double z = Math.Abs(1 / cam.TargetZoom - 1 / cam.Zoom), x = Math.Abs(cam.TargetCenterX - cam.CenterX), y = Math.Abs(cam.TargetCenterY - cam.CenterY);
            Assert.True(z < gz && x <= gx && y <= gy, "the remaining distance must shrink every frame");
            Assert.True(Math.Sign(1 / cam.TargetZoom - 1 / cam.Zoom) is var s && (s == sz || s == 0));
            Assert.True(Math.Sign(cam.TargetCenterX - cam.CenterX) is var t && (t == sxs || t == 0));
            (gz, gx, gy) = (z, x, y);
        }
        double zoom = cam.Zoom, cx = cam.CenterX, cy = cam.CenterY;
        for (int i = 0; i < 100; i++) Assert.False(cam.Advance(1.0 / 60));
        Assert.Equal(zoom, cam.Zoom);
        Assert.Equal(cx, cam.CenterX);
        Assert.Equal(cy, cam.CenterY);
    }

    [Fact]
    public void ImmediateOperations_LeaveTheCameraSettled_ADragMovesBothViews_CenterOnStopsTheEase()
    {
        Camera cam = At(3.0, 512, 512);
        cam.ZoomToward(400, 300, 2.0, Map);
        cam.Advance(1.0 / 60);
        Assert.False(cam.Settled);
        // A drag moves the shown and the target view by the same screen delta: the ground under the hand follows it.
        (double sx0, double sy0) = cam.WorldToScreen(500, 500, Map);
        double tx0 = cam.TargetCenterX, tz = cam.TargetZoom;
        cam.Pan(30, -20, Map);
        (double sx1, double sy1) = cam.WorldToScreen(500, 500, Map);
        Assert.Equal(sx0 + 30, sx1, 9);
        Assert.Equal(sy0 - 20, sy1, 9);
        Assert.Equal(tx0 - 30 / tz, cam.TargetCenterX, 9);
        Assert.False(cam.Settled);
        // The immediate zoom (previews, tests, measurement) leaves it settled.
        cam.ZoomAt(640, 400, 1.5, Map);
        Assert.True(cam.Settled);
        // CenterOn ends an ease where it is, then centres: settled, the settlement at the map rect's centre.
        WorldState world = UiSession.Start(42).World;
        cam.ZoomToward(100, 100, 3.0, Map);
        Assert.False(cam.Settled);
        double shownZoom = cam.Zoom;
        int id = world.Settlements[0].Id.Value;
        Assert.True(CameraFocus.CenterOn(cam, world, id, Map));
        Assert.True(cam.Settled);
        Assert.Equal(shownZoom, cam.Zoom);
        // GlideTo moves only the target: the shown view follows, settling with the settlement centred (where the clamp allows).
        cam.ZoomAt(640, 400, 4.0 / cam.Zoom, Map);
        int other = world.Settlements[^1].Id.Value;
        double before = cam.CenterX;
        Assert.True(CameraFocus.GlideTo(cam, world, other, Map));
        Assert.Equal(before, cam.CenterX);
        Assert.True(SettleFrames(cam) > 1);
        LineGeometry.Vertex p = OverlayMeshes.SettlementPosition(world.Settlements[^1], world.Terrain!.Size);
        double half = Map.W / 2 / cam.Zoom;
        if (p.X > half && p.X < 1024 - half) Assert.Equal(p.X, cam.CenterX, 6);
    }

    [Fact]
    public void ACameraNeverEased_ComputesExactlyWhatTheP1CameraComputed()
    {
        // The P1 camera's arithmetic, inlined: zoom at the cursor, then the clamp — the eased camera's immediate path.
        var cam = new Camera(1024);
        cam.Clamp(Map);
        double zoom = Math.Clamp(1.0, Math.Min(Map.W, Map.H) / 1024.0, Camera.MaxZoom), cx = 512, cy = 512;   // a new camera: zoom 1, clamped
        foreach ((double sx, double sy, double f) in new[] { (300.0, 200.0, 3.0), (1000.0, 600.0, 1.7), (640.0, 396.0, 0.5), (90.0, 700.0, 9.0) })
        {
            double wx = cx + (sx - Map.CenterX) / zoom, wy = cy + (sy - Map.CenterY) / zoom;
            zoom = Math.Clamp(zoom * f, Math.Min(Map.W, Map.H) / 1024.0, Camera.MaxZoom);
            cx = wx - (sx - Map.CenterX) / zoom;
            cy = wy - (sy - Map.CenterY) / zoom;
            double hw = Map.W / 2 / zoom, hh = Map.H / 2 / zoom;
            cx = 2 * hw >= 1024 ? 512 : Math.Clamp(cx, hw, 1024 - hw);
            cy = 2 * hh >= 1024 ? 512 : Math.Clamp(cy, hh, 1024 - hh);
            cam.ZoomAt(sx, sy, f, Map);
            Assert.Equal(zoom, cam.Zoom);
            Assert.Equal(cx, cam.CenterX);
            Assert.Equal(cy, cam.CenterY);
            Assert.True(cam.Settled);
        }
    }
}

/// <summary>
/// P2 — THE RIVER BUFFER IS REBUILT PER ZOOM BAND, NOT PER FRAME. The band mesh keeps every river inside the D-A3 screen
/// clamp at every zoom of its band; the host's <see cref="RiverBandCache"/> rebuilds once per band crossed while the real
/// GameUi's camera eases (the rebuild counter), where the D-A3 trigger (any zoom change) would have rebuilt every frame.
/// </summary>
[Collection("ImGui context")]
public class RiverBandTests : IDisposable
{
    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-riverband-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Sim.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    [Fact]
    public void TheBandMesh_HoldsEveryRiverInsideTheClamp_AtEveryZoomOfItsBand_RankMonotone()
    {
        const int count = 40;
        for (int band = RiverMesh.BandOf(0.1); band <= RiverMesh.BandOf(Camera.MaxZoom); band++)
        {
            (double low, double high) = RiverMesh.BandRange(band);
            Assert.Equal(band, RiverMesh.BandOf(Math.Sqrt(low * high)));
            for (int s = 0; s <= 8; s++)
            {
                double z = low * Math.Pow(high / low, s / 8.0);
                double previous = double.PositiveInfinity;
                for (int rank = 0; rank < count; rank++)
                {
                    double screen = RiverMesh.BandWidthWorld(rank, count, low, high) * z;
                    Assert.InRange(screen, RiverMesh.MinScreenWidthPx - 1e-9, RiverMesh.MaxScreenWidthPx + 1e-9);
                    Assert.True(screen <= previous + 1e-12, $"band {band} zoom {z}: rank {rank} wider than rank {rank - 1}");
                    previous = screen;
                    // Where neither clamp binds at this zoom, exactly the river's own world width (the D-A3 middle band).
                    double w = RiverMesh.WidthForRank(rank, count);
                    if (RiverMesh.MinScreenWidthPx / low <= w && w <= RiverMesh.MaxScreenWidthPx / high)
                        Assert.Equal(w, RiverMesh.BandWidthWorld(rank, count, low, high));
                }
            }
        }
    }

    [Fact]
    public void BuildBand_IsTheSameStripsAsTheDA3Mesh_WithTheBandsWidths()
    {
        Sim.Core.Worldgen.TerrainSet terrain = UiSession.Start(42).World.Terrain!;
        int band = RiverMesh.BandOf(3.0);
        RiverMesh.Vertex[] banded = RiverMesh.BuildBand(terrain, band), perZoom = RiverMesh.Build(terrain, 3.0);
        Assert.NotEmpty(banded);
        Assert.Equal(perZoom.Length, banded.Length);   // the same segments, quad for quad
    }

    /// <summary>THE REBUILD COUNTER: the real GameUi, the wheel eased over many frames; the host's cache, fed the shown zoom
    /// every frame, rebuilds only on the frames the zoom entered another band (at most once per band crossed). The D-A3 trigger ("the zoom changed") is counted alongside, as the
    /// control: it fires on every frame of the ease.</summary>
    [Fact]
    public void TheRiverBuffer_IsNotRebuiltEveryFrameOfAnEase_ButOncePerZoomBandCrossed()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), Path.Combine(_root, "rivers"), Assets());
        var cache = new RiverBandCache();
        Assert.True(cache.Update(h.Ui.Camera.Zoom));   // the first build
        int firstBand = cache.Band!.Value;
        double last = h.Ui.Camera.Zoom;
        int zoomChanges = 0, bandChanges = 0, frames = 0;
        void Count()
        {
            frames++;
            if (h.Ui.Camera.Zoom != last) zoomChanges++;
            if (RiverMesh.BandOf(h.Ui.Camera.Zoom) != RiverMesh.BandOf(last)) bandChanges++;
            last = h.Ui.Camera.Zoom;
            cache.Update(h.Ui.Camera.Zoom);
        }
        RectD map = h.Ui.MapRect;
        h.Wheel(map.CenterX, map.CenterY, 6);
        Count();
        while (!h.Ui.Camera.Settled && frames < 400) { h.Idle(1); Count(); }
        Assert.True(h.Ui.Camera.Settled);
        int crossed = Math.Abs(RiverMesh.BandOf(h.Ui.Camera.Zoom) - firstBand);
        Assert.True(crossed >= 2, $"the ease crossed only {crossed} band(s)");
        // The first build, then one rebuild on exactly the frames whose zoom entered another band — never more than
        // the bands crossed (a frame may cross two at once; the buffer is then built once, for the band it lands in).
        Assert.True(bandChanges >= 1 && bandChanges <= crossed, $"{bandChanges} band changes for {crossed} bands crossed");
        Assert.Equal(1 + bandChanges, cache.Rebuilds);
        // NOT EVERY FRAME: the zoom changed on many frames of the ease (the D-A3 trigger would have rebuilt on each);
        // the buffer was rebuilt on fewer than a third of them.
        Assert.True(3 * cache.Rebuilds < zoomChanges, $"{cache.Rebuilds} rebuilds over {zoomChanges} frames of zoom change");
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void TheHost_RebuildsTheRiverBufferOnlyThroughTheBandCache()
    {
        string src = File.ReadAllText(Path.Combine(RepoRoot(), "Sim.Ui", "SimUiGame.cs"));
        Assert.Contains("if (_riverBands.Update(cam.Zoom)) RebuildRiverBuffer(_riverBands.Band!.Value);", src, StringComparison.Ordinal);
        Assert.Contains("RiverMesh.BuildBand(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("_riverBuiltZoom", src, StringComparison.Ordinal);
        Assert.Equal(2, src.Split("RebuildRiverBuffer(").Length - 1);   // its definition and the one band-gated call
    }
}

/// <summary>
/// P2 — THE CONTINUOUS LEVEL OF DETAIL. The layer opacities are continuous in the visible km and monotone across each band
/// edge (no layer pops in or out); on the plateaus the continuous lens paints exactly what the banded lens painted; the
/// km bands switch on the canonical world where the share bands did; and across both cross-fades, at sampled scales,
/// every drawn element either persists with continuous opacity and geometry or fades in/out from (near) zero.
/// </summary>
public class ContinuousLevelOfDetailTests(CanonicalLensFixture fx) : IClassFixture<CanonicalLensFixture>
{
    private const int W = 1280, H = 800;

    /// <summary>Log-dense visible spans from 40 km to 9000 km, descending (zooming in).</summary>
    private static IEnumerable<double> Spans(int n = 6000)
    {
        double lo = Math.Log2(40), hi = Math.Log2(9000);
        for (int i = 0; i <= n; i++) yield return Math.Pow(2, hi - (hi - lo) * i / n);
    }

    [Fact]
    public void LayerAlpha_IsContinuous_AndMonotoneAcrossEachBandEdge_SoNoLayerPops()
    {
        WorldLayer[] all = Enum.GetValues<WorldLayer>();
        double[] edges = [MapDetail.WorldRegionalKm, MapDetail.RegionalSettlementKm];
        double step = (Math.Log2(9000) - Math.Log2(40)) / 6000;
        double lipschitz = 1.5 / (2 * MapDetail.FadeOctaves);   // the smoothstep's steepest slope, per octave
        MapDetail? prev = null;
        var direction = new Dictionary<(WorldLayer, int), int>();
        foreach (double km in Spans())
        {
            MapDetail d = MapDetail.At(km);
            foreach (WorldLayer l in all)
            {
                double a = d.Alpha(l);
                Assert.InRange(a, 0.0, 1.0);
                if (prev is MapDetail p)
                {
                    double delta = a - p.Alpha(l);
                    Assert.True(Math.Abs(delta) <= lipschitz * step + 1e-12, $"{l} jumps by {delta} at {km} km");
                    for (int e = 0; e < edges.Length; e++)
                    {
                        // Inside a cross-fade window an opacity moves one way only.
                        if (Math.Abs(Math.Log2(km / edges[e])) > MapDetail.FadeOctaves + step || delta == 0) continue;
                        int sign = Math.Sign(delta);
                        if (direction.TryGetValue((l, e), out int was)) Assert.Equal(was, sign);
                        else direction[(l, e)] = sign;
                    }
                }
            }
            if (d.IsPlateau)
            {
                // A plateau is the banded lens: opacity 1 for the band's layers, 0 for the rest, listed in its order.
                var on = new HashSet<WorldLayer>(all.Where(l => d.Alpha(l) > 0));
                Assert.Equal(new HashSet<WorldLayer>(WorldLens.LayersFor(d.Band)), on);
                foreach (WorldLayer l in all) Assert.True(d.Alpha(l) is 0.0 or 1.0);
                Assert.Same(WorldLens.LayersFor(d.Band), d.Layers());
            }
            else
            {
                IReadOnlyList<WorldLayer> layers = d.Layers();
                Assert.Equal(layers.Count, layers.Distinct().Count());
                Assert.Equal(new HashSet<WorldLayer>(all.Where(l => d.Alpha(l) > 0)), new HashSet<WorldLayer>(layers));
            }
            prev = d;
        }
        // Every fading layer was seen to move in each window it fades in (the windows were sampled).
        Assert.Equal(-1, direction[(WorldLayer.MajorSettlements, 0)]);
        Assert.Equal(1, direction[(WorldLayer.SettlementMorphology, 0)]);
        Assert.Equal(-1, direction[(WorldLayer.Territories, 1)]);
        Assert.Equal(1, direction[(WorldLayer.InstitutionTypes, 1)]);
        Assert.Equal(-1, direction[(WorldLayer.InstitutionalPresence, 1)]);
        Assert.Equal(1, direction[(WorldLayer.InstitutionalPresence, 0)]);
    }

    [Fact]
    public void TheBlends_AreExactPlateausOutsideTheirWindows_HalfAtTheEdges_AndTheBandSwitchesThere()
    {
        double f = Math.Pow(2, MapDetail.FadeOctaves);
        foreach ((double edge, Func<MapDetail, double> blend, WorldZoom above, WorldZoom below) in new (double, Func<MapDetail, double>, WorldZoom, WorldZoom)[]
        {
            (MapDetail.WorldRegionalKm, d => d.Regional, WorldZoom.World, WorldZoom.Regional),
            (MapDetail.RegionalSettlementKm, d => d.Settlement, WorldZoom.Regional, WorldZoom.Settlement),
        })
        {
            Assert.Equal(0.0, blend(MapDetail.At(edge * f * 1.0001)));
            Assert.Equal(1.0, blend(MapDetail.At(edge / f / 1.0001)));
            Assert.Equal(0.5, blend(MapDetail.At(edge)), 12);
            Assert.Equal(above, MapDetail.At(edge * 1.0001).Band);
            Assert.Equal(below, MapDetail.At(edge).Band);
            Assert.True(blend(MapDetail.At(edge * 1.0001)) < 0.5 && blend(MapDetail.At(edge / 1.0001)) > 0.5);
        }
        // The two windows are disjoint: at most one cross-fade at any scale.
        Assert.True(MapDetail.RegionalSettlementKm * f < MapDetail.WorldRegionalKm / f);
        foreach (double km in Spans()) { MapDetail d = MapDetail.At(km); Assert.True(d.Regional == 1.0 || d.Settlement == 0.0); }
        // The plateau records are the banded lens's.
        Assert.Equal(new MapDetail(WorldZoom.World, 0, 0, double.NaN), MapDetail.Of(WorldZoom.World));
        Assert.True(MapDetail.Of(WorldZoom.Regional).IsPlateau && MapDetail.Of(WorldZoom.Settlement).IsPlateau);
    }

    /// <summary>The attribution control for the moved threshold pins: on the canonical world (1024 px × 4 km) the km bands
    /// switch where the share bands (55 % / 16 % of the world) did, at every zoom of three map rects, except in the
    /// rounding slivers (2252.8 → 2253 km, 655.36 → 655 km).</summary>
    [Fact]
    public void OnTheCanonicalWorld_TheKmBandsSwitchWhereTheShareBandsDid()
    {
        WorldProjection p = fx.Projection;
        Assert.Equal(1024, p.WorldSize);
        Assert.Equal(4.0, p.KmPerPx);
        int samples = 0, sliver = 0;
        foreach ((double w, double h) in new[] { (1280.0, 688.0), (848.0, 688.0), (1920.0, 897.0) })
        {
            double fit = Math.Min(w, h) / 1024;
            for (double zoom = fit; zoom <= Camera.MaxZoom; zoom *= 1.0007)
            {
                double share = Math.Min(w, h) / zoom / 1024;
                WorldZoom old = share > 0.55 ? WorldZoom.World : share > 0.16 ? WorldZoom.Regional : WorldZoom.Settlement;
                double km = MapDetail.VisibleKmOf(zoom, w, h, p.KmPerPx);
                WorldZoom now = MapDetail.At(zoom, w, h, p.KmPerPx).Band;
                samples++;
                if (old == now) continue;
                sliver++;
                Assert.True(km is > 2252.8 and <= 2253 || km is > 655 and <= 655.36, $"zoom {zoom} ({km} km): {old} -> {now}");
            }
        }
        Assert.True(samples > 10000);
        Assert.True(sliver <= 6, $"{sliver} samples in the rounding slivers");
    }

    /// <summary>ON A PLATEAU THE CONTINUOUS LENS IS THE BANDED LENS, command for command: every band, centred on every
    /// settlement and on the fit, with and without an era theme — the SVG, the accounting and the hit geometry equal.</summary>
    [Fact]
    public void OnAPlateau_TheContinuousPaint_IsTheBandedPaint_CommandForCommand()
    {
        WorldProjection p = fx.Projection;
        int plateaus = 0;
        foreach (EraTheme? theme in new[] { null, EraThemes.For(UiEra.Modern) })
        {
            MapInk ink = theme is null ? MapInk.Default : MapInk.For(theme);
            foreach (double times in new[] { 1.0, 1.4, 3.0, 4.2, 10.0, 25.0, 41.0 })
                foreach (int centre in new[] { -1, 0, p.Settlements.Count - 1 })
                {
                    var cam = new Camera(1024);
                    cam.Clamp(W, H);
                    cam.ZoomAt(W / 2.0, H / 2.0, times, W, H);
                    if (centre >= 0) cam.Pan((cam.CenterX - p.Settlements[centre].X) * cam.Zoom, (cam.CenterY - p.Settlements[centre].Y) * cam.Zoom, W, H);
                    MapDetail detail = MapDetail.At(cam.Zoom, W, H, p.KmPerPx);
                    if (!detail.IsPlateau) continue;
                    plateaus++;
                    var a = new DrawList();
                    var b = new DrawList();
                    var anchor = new LegendAnchor(W - 12, H - 60, 340);
                    LensFrame fa = WorldLens.Paint(a, ApproxTextMeasure.Instance, p, detail, (x, y) => cam.WorldToScreen(x, y, W, H), cam.Zoom,
                        new RectD(0, 0, W, H), p.Settlements[0].Id, ink: ink, theme: theme, legend: anchor, selectedUnit: p.Units[0].Id);
                    LensFrame fb = WorldLens.Paint(b, ApproxTextMeasure.Instance, p, detail.Band, (x, y) => cam.WorldToScreen(x, y, W, H), cam.Zoom,
                        new RectD(0, 0, W, H), p.Settlements[0].Id, ink: ink, theme: theme, legend: anchor, selectedUnit: p.Units[0].Id);
                    Assert.Equal(SvgWriter.Write(b, W, H), SvgWriter.Write(a, W, H));
                    Assert.Equal(fb.LayerDraws, fa.LayerDraws);
                    Assert.Equal(fb.InfoDraws, fa.InfoDraws);
                    Assert.Equal(fb.Painted, fa.Painted);
                    Assert.Same(fb.Layers, fa.Layers);
                    Assert.Equal(fb.SettlementHits, fa.SettlementHits);
                    Assert.Equal(fb.UnitPlacements, fa.UnitPlacements);
                    Assert.Equal(fb.Paths, fa.Paths);
                    Assert.Equal(fb.Institutions, fa.Institutions);
                }
        }
        Assert.True(plateaus >= 30, $"{plateaus} plateau cameras");
    }

    /// <summary>The capital alone (no territory, no other settlement — so nothing enters or leaves the view by the cull):
    /// the projection's own capital, its units and its roads.</summary>
    private WorldProjection CapitalOnly(out SettlementLensView cap)
    {
        WorldProjection p = fx.Projection;
        SettlementLensView c = p.Settlements.FirstOrDefault(s => s.IsCapital && s.Controller == p.PlayerPolity) ?? p.Settlements[0];
        cap = c;
        return new WorldProjection
        {
            WorldSize = p.WorldSize, KmPerPx = p.KmPerPx, Settlements = [c], Units = p.Units.Where(u => u.Location == c.Id).ToList(),
            Roads = p.Roads, Routes = p.Routes, Territory = [], PlayerPolity = p.PlayerPolity, PlayerAge = p.PlayerAge,
            PlayerAgeName = p.PlayerAgeName, Absent = p.Absent,
        };
    }

    /// <summary>What a command IS, apart from where it is and how opaque: its kind, its colours' RGB, its text, its dash.
    /// A cross-fade changes only alpha and geometry, so the multiset of these is constant across a window except for
    /// elements fading in or out.</summary>
    internal static string Key(DrawCmd c)
    {
        static string C(Rgba? x) => x is { } v ? v.R + "," + v.G + "," + v.B : "-";
        return c switch
        {
            RectCmd r => "rect " + C(r.Fill) + " " + C(r.Stroke),
            LineCmd l => "line " + C(l.Color) + " " + (l.Dash is { } d ? d.On + "/" + d.Off : "solid"),
            PolygonCmd g => "poly " + C(g.Fill) + " " + g.Points.Length,
            CircleCmd k => "circle " + C(k.Fill) + " " + C(k.Stroke),
            ArcCmd a => "arc " + C(a.Color),
            TextCmd t => "text " + C(t.Color) + " " + t.Text,
            BezierCmd b => "bezier " + C(b.Color),
            PolylineCmd pl => "polyline " + C(pl.Color) + " " + pl.Points.Length,
            _ => c.GetType().Name,
        };
    }

    /// <summary>How big a command is drawn (its radius, its box's larger side, its type size or its stroke width).</summary>
    private static double Size(DrawCmd c) => c switch
    {
        RectCmd r => Math.Max(r.Rect.W, r.Rect.H),
        LineCmd l => l.Width,
        CircleCmd k => k.R,
        ArcCmd a => a.R,
        TextCmd t => t.Size,
        BezierCmd b => b.Width,
        PolylineCmd pl => pl.Width,
        _ => 0.0,
    };

    private static double Alpha(DrawCmd c)
    {
        static double A(Rgba? x) => x is { } v ? v.A / 255.0 : 0.0;
        return c switch
        {
            RectCmd r => Math.Max(A(r.Fill), A(r.Stroke)),
            LineCmd l => A(l.Color),
            PolygonCmd g => A(g.Fill),
            CircleCmd k => Math.Max(A(k.Fill), A(k.Stroke)),
            ArcCmd a => A(a.Color),
            TextCmd t => A(t.Color),
            BezierCmd b => A(b.Color),
            PolylineCmd pl => A(pl.Color),
            _ => 1.0,
        };
    }

    /// <summary>
    /// CONTINUITY AT SAMPLED SCALES — the acceptance's "no layer pops in or out", measured on what was drawn. The capital
    /// is centred and the zoom stepped ×1.002 through each cross-fade window and 0.3 octave beyond it (in two eras'
    /// hands). Between any two consecutive frames: (1) every kind of element present in both is drawn at opacities that
    /// moved by at most 0.03, and one present in only one frame is drawn at an opacity of at most 0.03 (it faded in or out
    /// from nothing); (2) every kind of element present in both resized by at most 1.5 px, and the name plate and the
    /// formation tokens moved by at most 3 px; (3) the name is ONE run, and the hit geometry holds it and is the outermost
    /// filled circle (P3's properties, in the cross-fades too). At the window's far side the frame is the next band's plateau.
    /// </summary>
    [Fact]
    public void AcrossBothCrossFades_AtSampledScales_NothingPops()
    {
        WorldProjection p = CapitalOnly(out SettlementLensView cap);
        (int frames, int fading) = SweepCrossFades(p, cap, new RectD(0, 0, W, H));
        Assert.True(fading >= 400, $"only {fading} of {frames} frames were inside a cross-fade");
    }

    /// <summary>The cross-fade sweep painted at <paramref name="centre"/> through a camera of zoom 800 × km per px / km
    /// (the 1280×800 window's short side) into <paramref name="viewport"/>; returns (frames, frames inside a cross-fade).</summary>
    internal static (int Frames, int Fading) SweepCrossFades(WorldProjection p, SettlementLensView centre, RectD viewport)
    {
        int frames = 0, fading = 0;
        int selectedUnit = p.Units.Count > 0 ? p.Units[0].Id : -1;
        foreach (EraTheme? theme in new[] { null, EraThemes.For(UiEra.Prehistoric) })
        {
            MapInk ink = theme is null ? MapInk.Default : MapInk.For(theme);
            foreach (double edge in new[] { MapDetail.WorldRegionalKm, MapDetail.RegionalSettlementKm })
            {
                double fromKm = edge * Math.Pow(2, MapDetail.FadeOctaves + 0.3), toKm = edge / Math.Pow(2, MapDetail.FadeOctaves + 0.3);
                List<DrawCmd>? before = null;
                LensFrame? last = null;
                for (double km = fromKm; km >= toKm; km /= 1.002)
                {
                    double zoom = Math.Min(W, H) * p.KmPerPx / km;
                    MapDetail detail = MapDetail.At(km);
                    var d = new DrawList();
                    LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, detail,
                        (x, y) => ((x - centre.X) * zoom + W / 2.0, (y - centre.Y) * zoom + H / 2.0), zoom,
                        viewport, centre.Id, ink: ink, theme: theme, selectedUnit: selectedUnit);
                    frames++;
                    if (!detail.IsPlateau) fading++;
                    // (3) One name, held by its plate; the footprint is the outermost filled circle about the mark.
                    SettlementHit hit = f.SettlementHits.Single(x => x.Id == centre.Id);
                    TextCmd name = Assert.Single(d.Commands.OfType<TextCmd>(), t => t.Text == centre.Name);
                    Assert.True(LensHitGeometryTests.Holds(hit.NamePlate, LensHitGeometryTests.TextBox(name, ApproxTextMeasure.Instance)), $"{km} km: the plate does not hold the name");
                    CircleCmd outer = d.Commands.OfType<CircleCmd>().Where(c => c.Fill is not null && c.Cx == hit.X && c.Cy == hit.Y).MaxBy(c => c.R)!;
                    Assert.Equal(new RectD(outer.Cx - outer.R, outer.Cy - outer.R, 2 * outer.R, 2 * outer.R), hit.Footprint);
                    if (before is not null && last is not null)
                    {
                        // (1) Opacity continuity per kind of element.
                        var was = before.GroupBy(Key).ToDictionary(g => g.Key, g => g.Select(Alpha).OrderBy(x => x).ToList());
                        var now = d.Commands.GroupBy(Key).ToDictionary(g => g.Key, g => g.Select(Alpha).OrderBy(x => x).ToList());
                        foreach (string k in was.Keys.Union(now.Keys))
                        {
                            List<double> a = was.TryGetValue(k, out List<double>? x) ? x : [], b = now.TryGetValue(k, out List<double>? y) ? y : [];
                            List<double> longer = a.Count >= b.Count ? a : b, shorter = a.Count >= b.Count ? b : a;
                            // The extra instances are the faintest of the longer list; they must be near zero.
                            for (int i = 0; i < longer.Count - shorter.Count; i++)
                                Assert.True(longer[i] <= 0.03, $"{km:0.0} km ({detail}): '{k}' pops {(a.Count > b.Count ? "out" : "in")} at opacity {longer[i]:0.000}");
                            for (int i = 0; i < shorter.Count; i++)
                                Assert.True(Math.Abs(longer[i + longer.Count - shorter.Count] - shorter[i]) <= 0.03,
                                    $"{km:0.0} km ({detail}): '{k}' jumps from {shorter[i]:0.000} to {longer[i + longer.Count - shorter.Count]:0.000}");
                        }
                        // (2) Geometry continuity: every kind of element present in both frames in the same number is drawn at
                        // sizes that moved by at most 1.5 px (radii, boxes, type sizes, stroke widths); the name plate and the
                        // formation tokens by at most 3 px. (The hit footprint is the outermost filled circle DRAWN, at any
                        // opacity — P3's rule, asserted above — so it grows the frame a faint footprint fades in, as it should.)
                        var wasSize = before.GroupBy(Key).ToDictionary(g => g.Key, g => g.Select(Size).OrderBy(x => x).ToList());
                        foreach (IGrouping<string, DrawCmd> g in d.Commands.GroupBy(Key))
                        {
                            if (!wasSize.TryGetValue(g.Key, out List<double>? s0) || s0.Count != g.Count()) continue;
                            List<double> s1 = [.. g.Select(Size).OrderBy(x => x)];
                            for (int i = 0; i < s1.Count; i++)
                                Assert.True(Math.Abs(s1[i] - s0[i]) <= 1.5, $"{km:0.0} km ({detail}): '{g.Key}' resizes {s0[i]:0.00} -> {s1[i]:0.00}");
                        }
                        SettlementHit h0 = last.SettlementHits.Single(x => x.Id == centre.Id);
                        Assert.True(Math.Abs(hit.NamePlate.Y - h0.NamePlate.Y) <= 3 && Math.Abs(hit.NamePlate.H - h0.NamePlate.H) <= 3
                            && Math.Abs(hit.NamePlate.W - h0.NamePlate.W) <= 3, $"{km} km: the name plate jumps {h0.NamePlate} -> {hit.NamePlate}");
                        Assert.Equal(last.UnitPlacements.Count, f.UnitPlacements.Count);
                        for (int i = 0; i < f.UnitPlacements.Count; i++)
                        {
                            UnitPlacement u0 = last.UnitPlacements[i], u1 = f.UnitPlacements[i];
                            Assert.True(Math.Abs(u1.X - u0.X) <= 3 && Math.Abs(u1.Y - u0.Y) <= 3 && Math.Abs(u1.Size - u0.Size) <= 0.5,
                                $"{km} km: formation {u1.Id} jumps ({u0.X},{u0.Y},{u0.Size}) -> ({u1.X},{u1.Y},{u1.Size})");
                        }
                    }
                    before = [.. d.Commands];
                    last = f;
                }
                Assert.NotNull(last);
                Assert.True(last!.Detail.IsPlateau, "the sweep must end on the next band's plateau");
            }
        }
        return (frames, fading);
    }

    /// <summary>The formation token is hit where it was drawn, at the size it was drawn — in a cross-fade too.</summary>
    [Fact]
    public void AFormationToken_IsHitAtTheSizeItWasDrawn_InACrossFade()
    {
        WorldProjection p = CapitalOnly(out SettlementLensView cap);
        Assert.NotEmpty(p.Units);
        double zoom = Math.Min(W, H) * p.KmPerPx / MapDetail.WorldRegionalKm;   // the middle of the World ↔ Regional fade
        var cam = new Camera(1024);
        cam.ZoomAt(W / 2.0, H / 2.0, zoom, W, H);
        cam.Pan((cam.CenterX - cap.X) * cam.Zoom, (cam.CenterY - cap.Y) * cam.Zoom, W, H);
        MapDetail detail = MapDetail.At(cam.Zoom, W, H, p.KmPerPx);
        Assert.False(detail.IsPlateau);
        LensFrame f = WorldLens.Paint(new DrawList(), ApproxTextMeasure.Instance, p, detail, (x, y) => cam.WorldToScreen(x, y, W, H), cam.Zoom, new RectD(0, 0, W, H));
        UnitPlacement u = f.UnitPlacements[0];
        Assert.InRange(u.Size, 9.0 + 0.1, 12.0 - 0.1);   // blended, neither band's
        Assert.Equal(u.Id, UnitSelection.HitTest(f, u.X + u.Size + UnitSelection.GripPx - 0.01, u.Y));
        Assert.Equal(-1, UnitSelection.HitTest(f, u.X + u.Size + UnitSelection.GripPx + 0.01, u.Y));
    }

    [Fact]
    public void TheRoadStyles_AtTheirBlendEndpoints_AreTheBandedStyles_AndWidenContinuously()
    {
        int[] classes = [EdgeTypes.DirtPath, EdgeTypes.Trackway, EdgeTypes.BuiltRoad,
            EdgeTypes.PavedRoad, EdgeTypes.MacadamRoad, EdgeTypes.Highway];
        foreach (int cls in classes)
        {
            Assert.Equal(RoadLens.StyleOf(cls, WorldZoom.Regional, MapInk.Default), RoadLens.StyleAt(cls, 0.0, MapInk.Default));
            Assert.Equal(RoadLens.StyleOf(cls, WorldZoom.Settlement, MapInk.Default), RoadLens.StyleAt(cls, 1.0, MapInk.Default));
            IReadOnlyList<RoadStroke> prev = RoadLens.StyleAt(cls, 0.0, MapInk.Default);
            for (int i = 1; i <= 100; i++)
            {
                IReadOnlyList<RoadStroke> s = RoadLens.StyleAt(cls, i / 100.0, MapInk.Default);
                Assert.Equal(prev.Count, s.Count);
                for (int k = 0; k < s.Count; k++)
                {
                    Assert.Equal(prev[k].Color, s[k].Color);
                    Assert.True(s[k].Width >= prev[k].Width && s[k].Width - prev[k].Width <= 0.03, $"class {cls} stroke {k}");
                }
                prev = s;
            }
        }
    }
}

/// <summary>P2 in the live game: the wheel eases through the real GameUi (the target moves, the view glides and settles
/// with the cursor's world point fixed), W A S D glides, and none of it touches the world or the order log.</summary>
[Collection("ImGui context")]
public class EasedZoomUiTests : IDisposable
{
    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-easedzoom-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TheWheel_EasesInTheGame_AndSettlesWithTheCursorsWorldPointFixed_TheWorldUntouched()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), Path.Combine(_root, "wheel"), Assets());
        string hash = WorldHash.ComputeHex(h.Ui.Session.World);
        OrderRecord[] queued = [.. h.Ui.Session.QueuedOrders()];
        RectD map = h.Ui.MapRect;
        // A view free of the clamp: zoom 3 about the world's middle.
        h.Ui.Camera.ZoomAt(map.CenterX, map.CenterY, 3.0 / h.Ui.Camera.Zoom, map);
        h.Idle(1);
        double sx = map.CenterX + 140, sy = map.CenterY - 60;
        (double ax, double ay) = h.Ui.Camera.ScreenToWorld(sx, sy, map);
        double z0 = h.Ui.Camera.Zoom;
        h.Wheel(sx, sy, 2);
        Assert.Equal(z0 * 1.25 * 1.25, h.Ui.Camera.TargetZoom, 9);
        Assert.False(h.Ui.Camera.Settled);   // two frames after the notches the view is still on its way
        Assert.True(h.Ui.Camera.Zoom > z0 && h.Ui.Camera.Zoom < h.Ui.Camera.TargetZoom);
        int frames = h.Settle();
        Assert.True(frames > 0, "the camera did not settle");
        Assert.Equal(h.Ui.Camera.TargetZoom, h.Ui.Camera.Zoom);
        (double wx, double wy) = h.Ui.Camera.ScreenToWorld(sx, sy, map);
        Assert.Equal(ax, wx, 6);
        Assert.Equal(ay, wy, 6);

        // W A S D glides: the target moves while the key is held, the view follows and settles.
        double tx = h.Ui.Camera.TargetCenterX;
        h.Hold(Keys.D, 10);
        Assert.True(h.Ui.Camera.TargetCenterX > tx);
        Assert.True(h.Settle() > 0);
        Assert.Equal(h.Ui.Camera.TargetCenterX, h.Ui.Camera.CenterX);

        Assert.Equal(hash, WorldHash.ComputeHex(h.Ui.Session.World));
        Assert.Equal(queued, h.Ui.Session.QueuedOrders());
        Assert.Empty(h.Problems);
    }
}

/// <summary>P2 — the cross-fades on the ROAD world (a 256-px world with built routes of several classes, one partial):
/// the World network fading out under the cased one, every class's widths blending, the route names fading in. The
/// viewport is unbounded so nothing enters or leaves the view by the cull or the label clip — only the level of detail
/// changes what is drawn.</summary>
public class ContinuousLevelOfDetailRoadTests(RoadWorldFixture fx) : IClassFixture<RoadWorldFixture>
{
    [Fact]
    public void AcrossBothCrossFades_TheNetworkAndTheRouteNames_NeverPop()
    {
        WorldProjection p = WorldProjection.Build(fx.Session.World, fx.Session.Config, id => fx.Session.Names.Name(id), UiPlayer.Empire);
        Assert.NotEmpty(p.Routes);
        Assert.NotEmpty(p.Roads);
        Assert.Contains(p.Routes, r => r.IsPartial);
        SettlementLensView cap = p.Settlements.Single(s => s.Id == fx.Capital);
        (int frames, int fading) = ContinuousLevelOfDetailTests.SweepCrossFades(p, cap, new RectD(-1e6, -1e6, 2e6, 2e6));
        Assert.True(fading >= 400, $"only {fading} of {frames} frames were inside a cross-fade");
    }
}
