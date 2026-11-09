using System.Security.Cryptography;
using Gfx.Demo.Scenes;
using Gfx.Engine.Assets;
using Gfx.Engine.Atmosphere;
using Gfx.Engine.Cameras;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Lighting;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Presentation;
using Gfx.Proc.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Tests;

/// <summary>A Fact that skips when the render1 baked assets are not on disk (scripts/bake-render1.sh not run).</summary>
public sealed class BakedAssetFactAttribute : FactAttribute
{
    public BakedAssetFactAttribute()
    {
        if (!File.Exists(AssetRoot.Baked("render1/terrain/MANIFEST.tsv"))) Skip = "baked render1 assets missing: run scripts/bake-render1.sh";
    }
}

/// <summary>A GfxFact that also needs the pinned CC0 HDRI on disk.</summary>
public sealed class HdriGfxFactAttribute : FactAttribute
{
    public HdriGfxFactAttribute()
    {
        if (!TestGfx.Available) Skip = "no graphics device: " + TestGfx.Reason;
        else if (!File.Exists(AssetRoot.Raw(AssetRoot.DefaultHdri))) Skip = "HDRI missing: run scripts/fetch-assets.sh";
    }
}

/// <summary>Stage render1: CPU maths of the atmosphere, image-based lighting, colour grading and switches.</summary>
public class Render1MathTests
{
    static Vector3d Dir(double azDeg, double elDeg)
    {
        double a = azDeg * Math.PI / 180, e = elDeg * Math.PI / 180;
        return new Vector3d(Math.Sin(a) * Math.Cos(e), Math.Sin(e), Math.Cos(a) * Math.Cos(e));
    }

    static readonly Lazy<AtmosphereModel> Clear = new(() => new AtmosphereModel(1.0));

    [Fact]
    public void ClearNoonSkyIsBlueAtTheZenithAndBrighterAtTheHorizon()
    {
        var m = Clear.Value; var sun = Dir(0, 60);
        var zenith = m.SkyRadiance(150, new Vector3d(0, 1, 0), sun);
        var horizon = m.SkyRadiance(150, Dir(180, 2), sun);
        Assert.True(zenith.B > zenith.G && zenith.G > zenith.R, $"zenith {zenith}");
        Assert.True(horizon.Luminance > 2 * zenith.Luminance, $"horizon {horizon.Luminance} vs zenith {zenith.Luminance}");
    }

    [Fact]
    public void SunlightReddensTowardsTheHorizonAndIsWhiteBalancedAt60Degrees()
    {
        var m = Clear.Value;
        double Ratio(double el) { var e = m.SunIrradiance(150, Math.Sin(el * Math.PI / 180)); return e.R / e.B; }
        double r60 = Ratio(60), r11 = Ratio(11), r1 = Ratio(1);
        Assert.InRange(r60, 0.97, 1.03);
        Assert.True(r11 > 1.5 * r60 && r1 > 5 * r11, $"R/B at 60° {r60:F3}, 11° {r11:F3}, 1° {r1:F3}");
        Assert.Equal(0, m.Transmittance(150, -0.1).G); // below the horizon the planet blocks the sun
    }

    [Fact]
    public void SkyLightIsAPlausibleShareOfSunlight()
    {
        var m = Clear.Value; var sun = Dir(0, 60);
        m.SkyViewTable(150, sun, out var eSky);
        double direct = m.SunIrradiance(150, Math.Sin(Math.PI / 3)).Luminance * Math.Sin(Math.PI / 3);
        Assert.InRange(eSky.Luminance / direct, 0.05, 0.3);
    }

    [Fact]
    public void SkyTableElevationMappingRoundTripsAndIsDeterministic()
    {
        for (int i = -90; i <= 90; i += 5)
        {
            double el = i * Math.PI / 180;
            Assert.Equal(el, AtmosphereModel.VToElevation(AtmosphereModel.ElevationToV(el)), 9);
        }
        Assert.Equal(0.5, AtmosphereModel.ElevationToV(0));
        var a = Clear.Value.SkyViewTable(150, Dir(0, 20));
        var b = new AtmosphereModel(1.0).SkyViewTable(150, Dir(0, 20));
        Assert.Equal(a, b);
    }

    [Fact]
    public void IrradianceShOfAConstantEnvironmentIsPiTimesRadiance()
    {
        var img = new EquirectImage(64, 32);
        Array.Fill(img.Rgb, 0.7f);
        var sh = IblMath.IrradianceSh9(img);
        foreach (var n in new[] { new Vector3d(0, 1, 0), new Vector3d(1, 0, 0), new Vector3d(0, 0, -1), new Vector3d(0.6, -0.8, 0) })
            Assert.Equal(Math.PI * 0.7, IblMath.EvalSh9(sh, n).G, 2);
    }

    [Fact]
    public void IrradianceShOfAnUpperHemisphereLightsUpAndNotDown()
    {
        var img = new EquirectImage(128, 64);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 128 * 3; x++) img.Rgb[y * 128 * 3 + x] = 1f;
        var sh = IblMath.IrradianceSh9(img);
        // exact: E(up) = π, E(down) = 0; order-2 SH approximates the step within a few per cent
        Assert.InRange(IblMath.EvalSh9(sh, new Vector3d(0, 1, 0)).R / Math.PI, 0.94, 1.06);
        Assert.InRange(IblMath.EvalSh9(sh, new Vector3d(0, -1, 0)).R / Math.PI, -0.06, 0.06);
        Assert.InRange(IblMath.EvalSh9(sh, new Vector3d(1, 0, 0)).R / Math.PI, 0.45, 0.55);
    }

    [Fact]
    public void PrefilteredConstantEnvironmentStaysConstantAtEveryLevel()
    {
        var img = new EquirectImage(64, 32);
        Array.Fill(img.Rgb, 0.25f);
        var levels = IblMath.PrefilterSpecular(img, 64, 32);
        Assert.Equal(7, levels.Count); // 64x32 .. 1x1
        foreach (var l in levels) foreach (var v in l.Rgb) Assert.Equal(0.25f, v, 4);
    }

    [Fact]
    public void BrdfTableIsBoundedAndReflectsEverythingForF0OfOne()
    {
        var t = IblMath.BrdfLut(32, 256);
        // 256-sample Hammersley quadrature overshoots unity by up to ~2e-4 near normal incidence (roughness ≈ 0.2)
        for (int i = 0; i < t.Length; i += 4) { Assert.InRange(t[i], 0f, 1.001f); Assert.InRange(t[i + 1], 0f, 1.001f); Assert.True(t[i] + t[i + 1] <= 1.001f); }
        // F0 = 1 at the smoothest row, normal incidence: scale + bias ≈ 1 (all energy reflected)
        int o = (0 * 32 + 31) * 4;
        Assert.InRange(t[o] + t[o + 1], 0.95f, 1.0001f);
        // rougher surfaces lose energy to single-scattering GGX (no multiple-scattering compensation)
        int r = (31 * 32 + 16) * 4;
        Assert.True(t[r] + t[r + 1] < t[o] + t[o + 1]);
    }

    [Fact]
    public void HammersleyIsDeterministicAndInTheUnitSquare()
    {
        var seen = new HashSet<(double, double)>();
        for (int i = 0; i < 64; i++)
        {
            var p = IblMath.Hammersley(i, 64);
            Assert.InRange(p.x, 0, 1); Assert.InRange(p.y, 0, 1);
            Assert.True(seen.Add(p));
            Assert.Equal(p, IblMath.Hammersley(i, 64));
        }
    }

    [Fact]
    public void RadianceHdrFlatEncodingRoundTripsWithinMantissaPrecision()
    {
        var img = new EquirectImage(16, 8);
        for (int i = 0; i < img.Rgb.Length; i++) img.Rgb[i] = 0.001f + (i * 37 % 101) * 0.37f;
        var back = RadianceHdr.Decode(RadianceHdr.EncodeFlat(img));
        Assert.Equal(img.Width, back.Width); Assert.Equal(img.Height, back.Height);
        for (int p = 0; p < img.Width * img.Height; p++)
        {
            float m = MathF.Max(img.Rgb[3 * p], MathF.Max(img.Rgb[3 * p + 1], img.Rgb[3 * p + 2]));
            for (int c = 0; c < 3; c++) Assert.True(MathF.Abs(back.Rgb[3 * p + c] - img.Rgb[3 * p + c]) <= m / 128f + 1e-6f);
        }
    }

    [Fact]
    public void RadianceHdrRunLengthScanlinesDecode()
    {
        // one 8-pixel RLE scanline: R = run of 8×200, G = literal 0..7, B = run 4×10 + literal 4 values, E = run 8×129
        var head = System.Text.Encoding.ASCII.GetBytes("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y 1 +X 8\n");
        var body = new List<byte> { 2, 2, 0, 8 };
        body.AddRange(new byte[] { 128 + 8, 200 });
        body.AddRange(new byte[] { 8, 0, 1, 2, 3, 4, 5, 6, 7 });
        body.AddRange(new byte[] { 128 + 4, 10, 4, 11, 12, 13, 14 });
        body.AddRange(new byte[] { 128 + 8, 129 });
        var img = RadianceHdr.Decode(head.Concat(body).ToArray());
        float f = MathF.ScaleB(1f, 129 - 136);
        Assert.Equal(200 * f, img.Rgb[0]); Assert.Equal(3 * f, img.Rgb[3 * 3 + 1]); Assert.Equal(13 * f, img.Rgb[6 * 3 + 2]);
    }

    [Fact]
    public void IdentityGradeIsIdentityAndHouseLookIsMonotonic()
    {
        var id = ColorGradeLut.Identity();
        foreach (var (r, g, b) in new[] { (0.0, 0.0, 0.0), (0.13, 0.5, 0.91), (1.0, 0.33, 0.07), (0.5, 0.5, 0.5) })
        {
            var o = id.Apply(r, g, b);
            Assert.Equal(r, o.r, 5); Assert.Equal(g, o.g, 5); Assert.Equal(b, o.b, 5);
        }
        var house = ColorGradeLut.House(); double prev = -1;
        for (int i = 0; i <= 64; i++)
        {
            double v = i / 64.0; var o = house.Apply(v, v, v);
            double l = 0.2126 * o.r + 0.7152 * o.g + 0.0722 * o.b;
            Assert.True(l > prev, $"grade not monotonic at {v}"); prev = l;
        }
    }

    [Fact]
    public void CubeLutFilesLoad()
    {
        string path = Path.Combine(Path.GetTempPath(), $"render1-{Environment.ProcessId}.cube");
        var lines = new List<string> { "TITLE \"identity 2\"", "LUT_3D_SIZE 2" };
        for (int b = 0; b < 2; b++) for (int g = 0; g < 2; g++) for (int r = 0; r < 2; r++) lines.Add($"{r} {g} {b}");
        File.WriteAllLines(path, lines);
        try
        {
            var lut = ColorGradeLut.LoadCube(path);
            var o = lut.Apply(0.25, 0.5, 0.75);
            Assert.Equal(0.25, o.r, 5); Assert.Equal(0.5, o.g, 5); Assert.Equal(0.75, o.b, 5);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void StillJitterIsDeterministicCentredAndDistinct()
    {
        var js = Enumerable.Range(0, 16).Select(ForwardRenderer.Jitter).ToArray();
        Assert.Equal(js, Enumerable.Range(0, 16).Select(ForwardRenderer.Jitter).ToArray());
        Assert.Equal(16, js.Distinct().Count());
        foreach (var j in js) { Assert.InRange(j.X, -0.5f, 0.5f); Assert.InRange(j.Y, -0.5f, 0.5f); }
        Assert.InRange(js.Average(j => j.X), -0.06, 0.06); Assert.InRange(js.Average(j => j.Y), -0.06, 0.06);
    }

    [Fact]
    public void SwitchesParseAndTheG1PresetIsTheG1Frame()
    {
        Assert.True(RenderFeatures.G1().IsG1Frame);
        var f = new RenderFeatures();
        Assert.False(f.IsG1Frame);
        f.SetMany("tonemap=agx, aa=none,samples=8,ibl=hdri,shadows=single,haze=2.5");
        Assert.Equal(ToneMapper.AgX, f.ToneMapper); Assert.Equal(AntiAliasing.None, f.Aa); Assert.Equal(8, f.StillSamples);
        Assert.Equal(IblSource.Hdri, f.Ibl); Assert.Equal(ShadowTechnique.Single, f.ShadowTechnique); Assert.Equal(2.5f, f.Haze);
        Assert.Throws<ArgumentException>(() => f.Set("nosuch", "1"));
        Assert.Throws<ArgumentException>(() => f.Set("pbr", "maybe"));
        var g = RenderFeatures.G1(); g.Set("bloom", "1");
        Assert.False(g.IsG1Frame);
        Assert.Contains("tonemap=AgX", f.Describe());
    }

    [Fact]
    public void FarFieldStartsOnTheMapEdgeAndOnlyCoastalMapsGetASea()
    {
        var t = TerrainMeshData.Build(BattleTerrainGenerator.Generate(TerrainTemperateScene.FixtureInputs));
        var ff = FarFieldMesh.Build(t, 7u);
        int P = 4 * FarFieldMesh.SidesSegments;
        for (int k = 0; k < P; k += 13)
        {
            var v = ff.Land.Vertices[k].Position;
            Assert.Equal(t.HeightAt(v.X, v.Z) - 1.0f, v.Y, 3);
            Assert.Equal(t.HalfExtentX, MathF.Max(MathF.Abs(v.X), MathF.Abs(v.Z)), 3);
        }
        var outer = ff.Land.Vertices[(ff.Rings - 1) * P].Position;
        Assert.Equal(FarFieldMesh.OuterHalfSize, MathF.Max(MathF.Abs(outer.X), MathF.Abs(outer.Z)), 1);
        Assert.Null(ff.Sea);
        var arid = TerrainMeshData.Build(BattleTerrainGenerator.Generate(TerrainAridScene.FixtureInputs));
        var ffa = FarFieldMesh.Build(arid, 7u);
        Assert.NotNull(ffa.Sea);
        // seaward far land sinks below the sea surface
        var seaward = ffa.Land.Vertices.Where(v => v.Position.Z > 4000 && MathF.Abs(v.Position.X) < 2000).ToArray();
        Assert.NotEmpty(seaward);
        Assert.All(seaward, v => Assert.True(v.Position.Y < 0, $"z={v.Position.Z} y={v.Position.Y}"));
    }

    /// <summary>Populated bake check: every baked file has a valid header, a full mip chain and its manifest hash.</summary>
    [BakedAssetFact]
    public void BakedTerrainSetsMatchTheirManifest()
    {
        string dir = AssetRoot.Baked("render1/terrain");
        var rows = File.ReadAllLines(Path.Combine(dir, "MANIFEST.tsv")).Where(l => !l.StartsWith('#')).Select(l => l.Split('\t')).ToArray();
        Assert.Equal(10, rows.Length);
        foreach (var r in rows)
        {
            string path = Path.Combine(dir, r[0]);
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(r[1], Convert.ToHexStringLower(SHA256.HashData(bytes)));
            Assert.Equal(long.Parse(r[2], System.Globalization.CultureInfo.InvariantCulture), bytes.LongLength);
            var (h, levels) = BakedTexture.Read(path);
            Assert.Equal(1024, h.Width); Assert.Equal(1024, h.Height); Assert.Equal(11, h.Levels);
            Assert.Equal(4, levels[^1].Length);
        }
        var calib = File.ReadAllText(Path.Combine(dir, "calibration.json"));
        Assert.Contains("\"mean_linear_luminance_baked\"", calib);
    }
}

/// <summary>Stage render1: GPU behaviour of the G2 frame (headless llvmpipe here; these prove behaviour, not speed).</summary>
public class Render1RenderTests
{
    static Color[] Render(GraphicsDevice gd, DemoScene scene, ForwardRenderer r, CaptureShot shot, RenderTarget2D rt, bool still = true)
    {
        scene.Update(shot.SceneTime);
        if (still) r.RenderStill(scene, shot.Camera, SunModel.At(shot.HourOfDay), rt, shot.SceneTime);
        else r.Render(scene, shot.Camera, SunModel.At(shot.HourOfDay), rt, shot.SceneTime);
        return Capture.ReadPixels(rt);
    }

    static double MeanLuma(Color[] px) => px.Average(c => 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B);

    [GfxFact]
    public void G2FrameDrawsABlueSkyAboveTheTerrain()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new TerrainTemperateScene(); scene.Load(gd, TestGfx.Host.Effects);
            using var r = new ForwardRenderer(gd, TestGfx.Host.Effects); scene.Configure(r.Settings);
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            var px = Render(gd, scene, r, scene.Shots.Single(s => s.Name == "tactical-low"), rt);
            var top = px[10 * 320 + 200];
            Assert.True(top.B > top.R + 30, $"sky {top}");
            Assert.True(Capture.DistinctColors(px) > 2000);
            Assert.Equal("analytic sky", r.Environment!.SourceDescription);
        });
    }

    /// <summary>Every G2 switch is a real A/B: switching it off changes the image of a terrain close-up.</summary>
    [GfxFact]
    public void EveryRenderSwitchChangesTheImage()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new TerrainTemperateScene(); scene.Load(gd, TestGfx.Host.Effects);
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            var shot = scene.Shots.Single(s => s.Name == "tactical-low");
            Color[] With(string? sw)
            {
                using var r = new ForwardRenderer(gd, TestGfx.Host.Effects); scene.Configure(r.Settings);
                if (sw != null) r.Settings.Features.SetMany(sw);
                return Render(gd, scene, r, shot, rt);
            }
            var g2 = With(null);
            foreach (var sw in new[] { "sky=legacy", "aerial=0", "pbr=0", "ibl=none", "shadows=single", "tonemap=acesfit", "autoexposure=0",
                                       "bloom=0", "grade=0", "aa=none", "terrain=procedural", "aniso=1", "hex=0", "macro=0" })
            {
                int changed = Capture.CountChanged(g2, With(sw), 2);
                Assert.True(changed > 200, $"{sw}: only {changed} px changed");
            }
        });
    }

    [GfxFact]
    public void AccumulatedStillsAreDeterministicAndKeepTheirBrightness()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new BattlefieldScene(); scene.Load(gd, TestGfx.Host.Effects);
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            var shot = scene.Shots.Single(s => s.Name == "follow-low");
            Color[] With(int samples)
            {
                using var r = new ForwardRenderer(gd, TestGfx.Host.Effects); scene.Configure(r.Settings);
                r.Settings.Features.SetMany($"samples={samples},aa=none");
                return Render(gd, scene, r, shot, rt);
            }
            var one = With(1); var a = With(4); var b = With(4);
            Assert.Equal(0, Capture.CountChanged(a, b, 0));
            Assert.InRange(MeanLuma(a) / MeanLuma(one), 0.97, 1.03);
            Assert.True(Capture.CountChanged(one, a, 6) > 100, "jitter should soften edges");
        });
    }

    /// <summary>Temporal adaptation: after a cut from noon to dusk the image brightens frame by frame, not at once.</summary>
    [GfxFact]
    public void AutoExposureAdaptsGraduallyAfterACut()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new TerrainTemperateScene(); scene.Load(gd, TestGfx.Host.Effects);
            using var r = new ForwardRenderer(gd, TestGfx.Host.Effects); scene.Configure(r.Settings);
            r.Settings.Features.SetMany("aa=none,bloom=0");
            using var rt = new RenderTarget2D(gd, 160, 90, false, SurfaceFormat.Color, DepthFormat.None);
            var cam = scene.Shots.Single(s => s.Name == "day").Camera;
            for (int f = 0; f < 3; f++) { r.Render(scene, cam, SunModel.At(12f), rt, f / 30f); }
            var lumas = new List<double>();
            for (int f = 3; f < 40; f++) { r.Render(scene, cam, SunModel.At(18.55f), rt, f / 30f); lumas.Add(MeanLuma(Capture.ReadPixels(rt))); }
            Assert.True(lumas[^1] > lumas[0] * 1.5, $"no adaptation: {lumas[0]:F1} → {lumas[^1]:F1}");
            for (int i = 1; i < lumas.Count; i++) Assert.True(lumas[i] >= lumas[i - 1] - 0.5, $"frame {i} darker than {i - 1}");
            Assert.True(lumas[1] < lumas[^1] * 0.9, "adapted in one frame");
        });
    }

    [HdriGfxFact]
    public void HdriImageBasedLightingLoadsAndDiffersFromSkyLighting()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new Render1MaterialsScene(); scene.Load(gd, TestGfx.Host.Effects);
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            var shot = scene.Shots.Single(s => s.Name == "noon");
            using var rs = new ForwardRenderer(gd, TestGfx.Host.Effects); scene.Configure(rs.Settings);
            var sky = Render(gd, scene, rs, shot, rt);
            using var rh = new ForwardRenderer(gd, TestGfx.Host.Effects); scene.Configure(rh.Settings); rh.Settings.Features.Set("ibl", "hdri");
            var hdri = Render(gd, scene, rh, shot, rt);
            Assert.StartsWith("HDRI harties_cliff_view_4k.hdr", rh.Environment!.SourceDescription);
            Assert.True(Capture.CountChanged(sky, hdri, 4) > 500);
            Assert.InRange(MeanLuma(hdri) / MeanLuma(sky), 0.7, 1.3); // scaled to the analytic sky's irradiance
        });
    }

    /// <summary>Cascades nest, cover their view slices, share one affine light space and reach every caster up-sun.</summary>
    [GfxFact]
    public void CascadesCoverTheirSlicesAndReachCastersTowardsTheSun()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var csm = new CascadedShadowMap(gd, 512);
            var cam = new CameraPose(new Vector3(0, 60, 300), new Vector3(0, 0, 0), 0.8f, 0.5f, 4000f);
            var toSun = Vector3.Normalize(new Vector3(-1, 0.08f, 0.2f)); // low dusk sun
            var bounds = new BoundingBox(new Vector3(-512, -40, -512), new Vector3(512, 90, 512));
            csm.Fit(cam, 16f / 9f, toSun, 4, 1600, bounds);
            for (int k = 1; k < 4; k++) Assert.True(csm.SplitFar[k] > csm.SplitFar[k - 1] && csm.Radius[k] > csm.Radius[k - 1]);
            Assert.Equal(1600f, csm.SplitFar[3], 1);
            float U(uint k) => PNoise.U01(PNoise.Hash(k)); // deterministic probe points
            for (uint i = 0; i < 200; i++)
            {
                var p = new Vector3(U(3 * i) * 1000 - 500, U(3 * i + 1) * 100, U(3 * i + 2) * 1000 - 500);
                var c0 = Vector4.Transform(new Vector4(p, 1), csm.ViewProj[0]);
                for (int k = 1; k < 4; k++)
                {
                    var ck = Vector4.Transform(new Vector4(p, 1), csm.ViewProj[k]);
                    var s = csm.Scale[k]; var o = csm.Offset[k];
                    Assert.Equal(ck.X, c0.X * s.X + o.X, 2); Assert.Equal(ck.Y, c0.Y * s.Y + o.Y, 2); Assert.Equal(ck.Z, c0.Z * s.Z + o.Z, 3);
                }
            }
            // a caster 450 m up-sun of the view target is inside the far cascade's depth range: G1's single map clipped
            // casters more than 2 radii (≈ 300-400 m) up-sun, so low-sun scenes lost their long shadows
            var far = Vector4.Transform(new Vector4(cam.Target + toSun * 450, 1), csm.ViewProj[3]);
            Assert.InRange(far.Z / far.W, 0f, 1f);
        });
    }

    /// <summary>The G1 preset frame and the full G2 frame are both repeatable, and neither writes the grid or the trace.</summary>
    [GfxFact]
    public void G2RenderingIsRepeatableAndNeverWritesTheGridOrTrace()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new BattlefieldScene(); scene.Load(gd, TestGfx.Host.Effects);
            var grid0 = scene.Grid.HashHex(); var trace0 = scene.Trace.Hash();
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            foreach (var preset in new[] { "g1", "g2" })
            {
                Color[] Once()
                {
                    using var r = new ForwardRenderer(gd, TestGfx.Host.Effects); scene.Configure(r.Settings);
                    if (preset == "g1") r.Settings.Features = RenderFeatures.G1(); else r.Settings.Features.SetMany("samples=2,ibl=sky");
                    Color[] last = Array.Empty<Color>();
                    foreach (var shot in scene.Shots) last = Render(gd, scene, r, shot, rt);
                    return last;
                }
                Assert.Equal(0, Capture.CountChanged(Once(), Once(), 0));
            }
            Assert.Equal(grid0, scene.Grid.HashHex());
            Assert.Equal(trace0, scene.Trace.Hash());
        });
    }
}
