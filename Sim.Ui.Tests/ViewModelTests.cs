using System.Security.Cryptography;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
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
}
