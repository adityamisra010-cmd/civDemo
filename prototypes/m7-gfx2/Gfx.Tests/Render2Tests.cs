using Gfx.Chars.Animation;
using Gfx.Chars.Crowd;
using Gfx.Chars.Format;
using Gfx.Chars.Rendering;
using Gfx.Demo.Scenes;
using Gfx.Engine.Cameras;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Presentation;
using Gfx.Proc.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Gfx.Tests;

/// <summary>
/// Stage render2, CPU side: G-buffer-lite outputs of every effect, light-share reporting, cascade stabilisation and
/// caster culling, TAA history policy, crowd LOD rule, looks, animation bank, CPU occlusion buffer and the terrain
/// occluders. No device needed (except where a test says so).
/// </summary>
// CPU heavy (effect builds, animation-bank bakes, terrain occluders): serial with the env tests, away from the perf
// stage's wall-clock tests (see EnvSerialCollection; WarmupLastsAtLeastItsMinimumTime failed once in a full run
// of this stage's suite with these classes in the parallel phase, and passed when run alone).
[Collection(EnvSerialCollection.Name)]
public class Render2MathTests
{
    static IEnumerable<EffectBuilder> AllBuilders()
    {
        foreach (var src in GfxDiscovery.Instances<IEffectSource>(typeof(Render2MathTests).Assembly))
            foreach (var b in src.CreateEffects()) { b.BuildBytes(); yield return b; }
    }

    /// <summary>Every pixel program writes the three G-buffer targets after its colour and never gl_FragColor (it would
    /// broadcast the colour into the depth and share targets).</summary>
    [Fact]
    public void EveryEffectWritesTheGBufferOutputs()
    {
        int n = 0;
        foreach (var b in AllBuilders())
        {
            Assert.DoesNotContain("gl_FragColor", b.PixelGlsl);
            for (int t = 0; t < 4; t++) Assert.Contains($"gl_FragData[{t}]", b.PixelGlsl);
            n++;
        }
        Assert.True(n >= 50, $"{n} effects");
    }

    static readonly string[] Reporters = { "gbAmbient(", "shadePbrFull(", "shadePbr(", "shadeLit(", "shadeSurface(", "shadeFoliage(", "shadeSkin(" };

    /// <summary>
    /// Every surface effect whose main body runs the scene's fog (so its pixel is a lit surface) reports its ambient
    /// term through one of the reporting lighting functions; otherwise AO would silently skip it.
    /// </summary>
    [Fact]
    public void LitSurfaceEffectsReportTheirAmbientLight()
    {
        var missing = new List<string>();
        int lit = 0;
        foreach (var b in AllBuilders())
        {
            if (b.IsSkyGBuffer || b.IsBlendedGBuffer) continue;
            string g = b.PixelGlsl;
            int body = g.IndexOf("void gfx_main()", StringComparison.Ordinal);
            string main = g[body..];
            if (!main.Contains("applyFog(", StringComparison.Ordinal) && !main.Contains("aerialPerspective(", StringComparison.Ordinal)) continue;
            lit++;
            if (!Reporters.Any(r => main.Contains(r, StringComparison.Ordinal))) missing.Add(b.Name);
        }
        Assert.True(lit >= 15, $"{lit} lit surface effects");
        Assert.Empty(missing);
        // and each reporting function reports (or calls one that does)
        var lib = GlslLib.Lighting + CharEffects.SurfaceLib + Gfx.Proc.Environment.EnvGlsl.Foliage;
        foreach (var f in new[] { "shadePbrFull", "shadeSurface", "shadeFoliage" })
        {
            int at = lib.IndexOf("vec3 " + f + "(", StringComparison.Ordinal);
            Assert.True(at >= 0, f);
            int end = lib.IndexOf("\n}", at, StringComparison.Ordinal);
            Assert.Contains("gbAmbient(", lib[at..end]);
        }
    }

    /// <summary>The applyFog/aerial perspective records the surface radiance and its transmittance (the shares' base).</summary>
    [Fact]
    public void FogRecordsTheSurfaceAndItsTransmittance()
    {
        string l = GlslLib.Lighting;
        int ap = l.IndexOf("vec3 aerialPerspective(", StringComparison.Ordinal), apEnd = l.IndexOf("\n}", ap, StringComparison.Ordinal);
        Assert.Contains("gbSurface(c0, T *", l[ap..apEnd]);
        int af = l.IndexOf("vec3 applyFog(", StringComparison.Ordinal), afEnd = l.IndexOf("\n}", af, StringComparison.Ordinal);
        Assert.Contains("gbSurface(c, vec3(1.0 - f))", l[af..afEnd]);
    }

    // ------------------------------------------------------------------------------------------------ cascades

    /// <summary>Caster culling: a sphere well inside the finer cascade's inner square (with its shadow throw inside the
    /// depth range) is covered; one near its edge, beyond it, or whose throw leaves the depth range is not.</summary>
    [Fact]
    public void CasterCullingKeepsCastersWhoseShadowCanLeaveTheFinerCascade()
    {
        var l = new Vector3(0, 1, 0);
        var view = Matrix.CreateLookAt(Vector3.Zero, -l, Vector3.Forward);
        var proj = Matrix.CreateOrthographicOffCenter(-10, 10, -10, 10, -100, 100);
        var m = view * proj;
        float margin = 1f - 8f / 4096f;
        Assert.True(CascadeCulling.Covered(m, margin, l, new Vector3(0, 0, 0), 1f, 30f));
        Assert.True(CascadeCulling.Covered(m, margin, l, new Vector3(7.5f, 0, 0), 1f, 30f));
        Assert.False(CascadeCulling.Covered(m, margin, l, new Vector3(8.5f, 0, 0), 1f, 30f));   // past 0.9 × margin
        Assert.False(CascadeCulling.Covered(m, margin, l, new Vector3(0, 0, -9.5f), 1f, 30f));
        Assert.False(CascadeCulling.Covered(m, margin, l, new Vector3(30f, 0, 0), 1f, 30f));    // outside
        Assert.False(CascadeCulling.Covered(m, margin, l, new Vector3(0, -80f, 0), 1f, 30f));   // the throw leaves the range
        Assert.True(CascadeCulling.Covered(m, margin, l, new Vector3(0, -60f, 0), 1f, 30f));
    }

    // ------------------------------------------------------------------------------------------------ TAA policy

    static CameraPose Cam(Vector3 p, Vector3 t, float fov = 0.8f) => new(p, t, fov, 0.3f, 4000f);

    /// <summary>Small moves keep the TAA history; cuts (large jumps, big turns, zooms) drop it.</summary>
    [Fact]
    public void TaaHistorySurvivesSmallMovesAndNotCuts()
    {
        var a = Cam(new Vector3(0, 10, 30), new Vector3(0, 0, 0));
        Assert.True(TaaHistory.Continuous(a, a));
        Assert.True(TaaHistory.Continuous(a, a with { Position = a.Position + new Vector3(0.5f, 0, 0), Target = a.Target + new Vector3(0.5f, 0, 0) }));
        Assert.True(TaaHistory.Continuous(a, a with { Position = a.Position + new Vector3(3.9f, 0, 0) }));
        Assert.False(TaaHistory.Continuous(a, a with { Position = a.Position + new Vector3(50f, 0, 0), Target = a.Target + new Vector3(50f, 0, 0) }));
        var turned = a with { Target = a.Position + Vector3.Transform(a.Target - a.Position, Matrix.CreateRotationY(MathHelper.ToRadians(30))) };
        Assert.False(TaaHistory.Continuous(a, turned));
        var nudged = a with { Target = a.Position + Vector3.Transform(a.Target - a.Position, Matrix.CreateRotationY(MathHelper.ToRadians(5))) };
        Assert.True(TaaHistory.Continuous(a, nudged));
        Assert.False(TaaHistory.Continuous(a, a with { FovYRadians = a.FovYRadians + 0.1f }));
        // the jump allowance grows with the focus distance (15 % of it), never below 4 m
        var far = Cam(new Vector3(0, 0, 100), Vector3.Zero);
        Assert.True(TaaHistory.Continuous(far, far with { Position = far.Position + new Vector3(10, 0, 0), Target = new Vector3(10, 0, 0) }));
        var near = Cam(new Vector3(0, 0, 10), Vector3.Zero);
        Assert.False(TaaHistory.Continuous(near, near with { Position = near.Position + new Vector3(5, 0, 0), Target = new Vector3(5, 0, 0) }));
    }

    /// <summary>The still jitter and the TAA jitter share one deterministic Halton(2,3) sequence inside ±0.5 px.</summary>
    [Fact]
    public void TaaJitterIsDeterministicAndInsideThePixel()
    {
        var seen = new HashSet<(float, float)>();
        for (int i = 0; i < TaaHistory.Sequence; i++)
        {
            var j = ForwardRenderer.Jitter(i);
            Assert.InRange(j.X, -0.5f, 0.5f); Assert.InRange(j.Y, -0.5f, 0.5f);
            Assert.Equal(j, ForwardRenderer.Jitter(i));
            Assert.True(seen.Add((j.X, j.Y)));
        }
    }

    // ------------------------------------------------------------------------------------------------ crowd LOD, looks

    /// <summary>The crowd LOD rule: finer levels for larger soldiers, a cross-fade band with complementary halves around
    /// each threshold, monotonic in the projected size, the bias scaling the size and the force overriding it.</summary>
    [Fact]
    public void CrowdLevelsFollowProjectedSizeWithComplementaryFades()
    {
        var s = CrowdLodSettings.Default;
        Span<(int level, float fade, float sign)> o = stackalloc (int, float, float)[2];
        Assert.Equal(1, s.Assign(200f, true, -1, o)); Assert.Equal((0, 0f, 0f), o[0]);
        Assert.Equal(1, s.Assign(10f, true, -1, o)); Assert.Equal(2, o[0].level);
        Assert.Equal(1, s.Assign(50f, true, -1, o)); Assert.Equal(1, o[0].level);
        Assert.Equal(2, s.Assign(s.Lod1Px, true, -1, o));
        Assert.Equal(0, o[0].level); Assert.Equal(1, o[1].level);
        Assert.Equal(o[0].fade, o[1].fade); Assert.Equal(1f, o[0].sign); Assert.Equal(-1f, o[1].sign);
        Assert.InRange(o[0].fade, 0.49f, 0.51f);
        Assert.Equal(2, s.Assign(s.Lod2Px, true, -1, o));
        Assert.Equal(1, o[0].level); Assert.Equal(2, o[1].level);
        // monotonic: shrinking soldiers never move to a finer level, and a fade only ever pairs neighbouring levels
        int prevLo = 0; float prevFade = -1; int prevPair = -1;
        for (float px = 300f; px > 1f; px *= 0.97f)
        {
            int n = s.Assign(px, true, -1, o);
            int lo = o[0].level;
            Assert.True(lo >= prevLo, $"px {px}: level {lo} after {prevLo}");
            prevLo = lo;
            if (n == 2)
            {
                Assert.Equal(lo + 1, o[1].level);
                Assert.InRange(o[0].fade, 0f, 1f);
                if (prevPair == lo) Assert.True(o[0].fade >= prevFade, "the fade grows as the soldier shrinks");
                prevPair = lo; prevFade = o[0].fade;
            }
        }
        // without the fade: one level, hard switches at the thresholds
        Assert.Equal(1, s.Assign(s.Lod1Px - 0.01f, false, -1, o)); Assert.Equal(1, o[0].level);
        // bias doubles the distance of each switch; force wins
        Assert.Equal(1, (s with { Bias = 2f }).Assign(s.Lod1Px * 0.55f * (1 + s.FadeBand), true, -1, o)); Assert.Equal(0, o[0].level);
        Assert.Equal(1, s.Assign(500f, true, 2, o)); Assert.Equal((2, 0f, 0f), o[0]);
        // shadows and reflections: one level, one coarser than the view's, at least lod2
        Assert.Equal(1, s.ShadowLevel(500f, -1)); Assert.Equal(2, s.ShadowLevel(50f, -1)); Assert.Equal(2, s.ShadowLevel(5f, -1));
    }

    /// <summary>Per-soldier looks stay inside their bounds, are deterministic per key and actually vary.</summary>
    [Fact]
    public void CrowdLooksStayInsideTheirBounds()
    {
        var hues = new HashSet<float>();
        for (uint k = 0; k < 20000; k++)
        {
            var l = CrowdLook.FromKey(k);
            Assert.Equal(l, CrowdLook.FromKey(k));
            Assert.InRange(l.DyeHue, -CrowdLook.MaxDyeHue, CrowdLook.MaxDyeHue);
            Assert.InRange(l.DyeValue, CrowdLook.MinDyeValue, CrowdLook.MaxDyeValue);
            Assert.InRange(l.SkinTone, -1f, 1f);
            Assert.InRange(l.Mud, 0f, 1f); Assert.InRange(l.Tarnish, 0f, 1f); Assert.InRange(l.Seed, 0f, 1f);
            Assert.InRange(l.Height, CrowdLook.MinHeight, CrowdLook.MaxHeight);
            Assert.InRange(l.Width, CrowdLook.MinWidth, CrowdLook.MaxWidth);
            hues.Add(MathF.Round(l.DyeHue, 3));
        }
        Assert.True(hues.Count > 50, $"{hues.Count} distinct hue shifts");
    }

    // ------------------------------------------------------------------------------------------------ occlusion

    static OcclusionBuffer WallBuffer(out CameraPose cam)
    {
        cam = new CameraPose(new Vector3(0, 1.7f, 0), new Vector3(0, 1.7f, -10), 0.9f, 0.3f, 1000f);
        var ob = new OcclusionBuffer(128, 72);
        ob.Begin(cam.View, cam.Projection(16f / 9f), cam.Near);
        ob.AddBox(Matrix.CreateScale(4, 2, 0.2f) * Matrix.CreateTranslation(0, 2, -10)); // wall: x ±4, y 0..4, z -10
        ob.End();
        return ob;
    }

    /// <summary>The CPU occlusion buffer hides what is wholly behind the wall and keeps everything that shows: over the
    /// top, past its end, in front of it, or crossing the near plane.</summary>
    [Fact]
    public void OcclusionBufferHidesOnlyWhatIsWhollyBehindTheOccluder()
    {
        var ob = WallBuffer(out _);
        Assert.True(ob.TrianglesRasterized >= 10);
        Assert.True(ob.IsSphereOccluded(new Vector3(0, 1, -20), 1f));
        Assert.True(ob.IsSphereOccluded(new Vector3(-5, 1, -25), 1f));
        Assert.False(ob.IsSphereOccluded(new Vector3(0, 6.0f, -20), 0.8f));    // over the wall top (the top hides y < 6.3 at 20 m)
        Assert.False(ob.IsSphereOccluded(new Vector3(8.5f, 1, -20), 1f));      // past the wall's end (it ends at x = 8 at 20 m)
        Assert.False(ob.IsSphereOccluded(new Vector3(0, 1, -5), 1f));          // in front of the wall
        Assert.False(ob.IsSphereOccluded(new Vector3(0, 1.7f, -0.2f), 1f));    // crossing the near plane
        Assert.False(ob.IsSphereOccluded(new Vector3(0, 1, 30), 1f));          // behind the camera
        // the eroded buffer never claims an occluder pixel the raw raster left uncovered
        for (int y = 0; y < ob.Height; y++)
            for (int x = 0; x < ob.Width; x++)
                if (float.IsPositiveInfinity(ob.RawDepthAt(x, y))) Assert.True(float.IsPositiveInfinity(ob.DepthAt(x, y)));
    }

    /// <summary>
    /// Silhouettes are conservative. A sphere whose centre is behind the wall but which reaches past its edge is kept.
    /// And to the sub-pixel: the rasteriser covers a pixel whose centre lies inside an occluder although part of the
    /// pixel lies outside it; a small sphere that straddles the wall's edge inside such a pixel is kept only because the
    /// erosion gives that pixel its uncovered neighbour's depth.
    /// </summary>
    [Fact]
    public void OcclusionSilhouettesAreConservative()
    {
        var ob = WallBuffer(out _);
        // centre at x = 7.2 (inside the wall's ±8 m at 20 m) with a radius reaching past the edge
        Assert.False(ob.IsSphereOccluded(new Vector3(7.2f, 1, -20), 1.0f));
        // and well inside it, the same sphere is hidden
        Assert.True(ob.IsSphereOccluded(new Vector3(4f, 1, -20), 1.0f));

        // the sub-pixel case: a wall whose right edge falls at 0.8 of pixel column c
        const int W = 128, H = 72, c = 90;
        var cam = new CameraPose(Vector3.Zero, new Vector3(0, 0, -10), 0.9f, 0.3f, 1000f);
        var proj = cam.Projection(16f / 9f);
        static float Ndc(float px) => px / W * 2f - 1f;
        float edge = Ndc(c + 0.8f);
        float xe = edge * 10f / proj.M11;                   // the edge at 10 m
        var sub = new OcclusionBuffer(W, H);
        sub.Begin(cam.View, proj, cam.Near);
        sub.AddBox(Matrix.CreateScale((xe + 6f) / 2f, 3f, 0.005f) * Matrix.CreateTranslation((xe - 6f) / 2f, 0, -10f));
        sub.End();
        Assert.False(float.IsPositiveInfinity(sub.RawDepthAt(c, H / 2)), "column c is rasterised as covered");
        Assert.True(float.IsPositiveInfinity(sub.RawDepthAt(c + 1, H / 2)), "column c + 1 is not");
        // a 2 cm sphere at 20 m centred on the edge's line of sight: its screen rectangle lies inside column c, half of
        // it past the edge (visible), so it must be kept
        Assert.False(sub.IsSphereOccluded(new Vector3(edge * 20f / proj.M11, 0, -20f), 0.02f));
        // the same sphere five columns inside the wall is hidden
        Assert.True(sub.IsSphereOccluded(new Vector3(Ndc(c - 4.5f) * 20f / proj.M11, 0, -20f), 0.02f));
    }

    /// <summary>
    /// Wall occluders stay inside the palisade on sloping, bumpy ground: along each box, its top lies under the lowest
    /// real log top (logs stand at least the nominal height − 0.3 m above the ground under them), and the two segments
    /// beside the gate (the first post) stay open.
    /// </summary>
    [Fact]
    public void WallOccludersNeverRiseAboveTheWall()
    {
        static float Ground(float x, float z) => 0.12f * x + 0.4f * MathF.Sin(z * 0.7f) + 0.3f * MathF.Cos(x * 1.3f);
        var ring = new List<Vector2>();
        for (int i = 0; i < 64; i++) { float a = i * MathHelper.TwoPi / 64; ring.Add(new Vector2(MathF.Cos(a), MathF.Sin(a)) * 46f); }
        const float height = 3.4f, gateHalf = 1.9f;
        var boxes = BattleOccluders.WallBoxes(ring, height, Ground);
        Assert.Equal(62, boxes.Count);
        float worst = float.MinValue;
        foreach (var m in boxes)
        {
            for (int k = 0; k <= 40; k++)
            {
                var top = Vector3.Transform(new Vector3(0, 1, -1 + k / 20f), m);
                worst = MathF.Max(worst, top.Y - (Ground(top.X, top.Z) + height - 0.3f));
            }
            foreach (float e in new[] { -1f, 1f })
            {
                var end = Vector3.Transform(new Vector3(0, 0, e), m);
                Assert.True(Vector2.Distance(new Vector2(end.X, end.Z), ring[0]) > gateHalf, "a box reaches into the gate");
            }
        }
        Assert.True(worst <= 1e-3f, $"a box top rises {worst:F3} m above the lowest log top under it");
    }

    /// <summary>The terrain occluder of a battle map never rises above the true ground (the conservative bound).</summary>
    [Fact]
    public void TerrainOccludersStayUnderTheGround()
    {
        var m = TerrainMeshData.Build(BattleTerrainGenerator.Generate(TerrainTemperateScene.FixtureInputs));
        var occ = new BattleOccluders(m);
        Assert.True(occ.TerrainTriangles > 10000);
        uint h = 12345;
        int above = 0, n = 0; float worst = 0;
        for (int i = 0; i < 4000; i++)
        {
            h = h * 1664525u + 1013904223u; float x = ((h >> 8) / 16777216f * 2 - 1) * (m.HalfExtentX - 20);
            h = h * 1664525u + 1013904223u; float z = ((h >> 8) / 16777216f * 2 - 1) * (m.HalfExtentZ - 20);
            float d = occ.OccluderHeightAt(x, z, m.HalfExtentX, m.HalfExtentZ) - m.HeightAt(x, z);
            n++;
            if (d > 1e-3f) { above++; worst = MathF.Max(worst, d); }
        }
        Assert.True(above == 0, $"{above} of {n} samples above the ground (worst {worst} m)");
    }
}
