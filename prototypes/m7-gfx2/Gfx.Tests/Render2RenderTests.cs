using Gfx.Chars.Animation;
using Gfx.Chars.Crowd;
using Gfx.Chars.Rendering;
using Gfx.Demo.Scenes;
using Gfx.Engine.Cameras;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Gfx.Tests;

/// <summary>
/// A pillar (and optional extra boxes) standing on a large flat slab, drawn with the engine's instanced lit effect,
/// for the screen-space, cascade and TAA tests. Shadow casters can be culled by the cascade caster test.
/// </summary>
sealed class PillarScene : IRenderContent, IDisposable
{
    readonly GpuMesh _box;
    readonly InstanceBuffer<InstanceTransform> _all, _one;
    readonly List<(Matrix world, BoundingSphere bounds, Vector4 tint)> _items = new();
    public int CastersDrawn, CastersCulled;

    public PillarScene(GraphicsDevice gd, IEnumerable<(Vector3 centre, Vector3 half, Vector3 tint)>? extra = null)
    {
        _box = GpuMesh.Create(gd, Primitives.Box(Vector3.Zero, Vector3.One, Color.White));
        Add(new Vector3(0, -0.1f, 0), new Vector3(80, 0.1f, 80), new Vector3(0.30f, 0.28f, 0.25f));
        Add(new Vector3(0, 1, 0), new Vector3(0.5f, 1, 0.5f), new Vector3(0.55f, 0.5f, 0.45f));
        if (extra != null) foreach (var (c, h, t) in extra) Add(c, h, t);
        _all = new InstanceBuffer<InstanceTransform>(gd, _items.Count);
        _all.Set(_items.Select(i => InstanceTransform.From(i.world, i.tint)).ToArray());
        _one = new InstanceBuffer<InstanceTransform>(gd, 1);
    }

    void Add(Vector3 c, Vector3 half, Vector3 tint) =>
        _items.Add((Matrix.CreateScale(half) * Matrix.CreateTranslation(c), new BoundingSphere(c, half.Length()), new Vector4(tint, 1)));

    public void DrawShadowCasters(RenderContext rc)
    {
        var fx = rc.Effects.Get(EffectNames.InstancedDepth);
        foreach (var it in _items)
        {
            if (rc.CoveredByFinerCascade(it.bounds.Center, it.bounds.Radius)) { CastersCulled++; continue; }
            _one.Set(new[] { InstanceTransform.From(it.world, it.tint) });
            _box.DrawInstanced(rc, fx, _one);
            CastersDrawn++;
        }
    }

    public void DrawOpaque(RenderContext rc) => _box.DrawInstanced(rc, rc.Effects.Get(EffectNames.InstancedLit), _all);
    public void Dispose() { _box.Dispose(); _all.Dispose(); _one.Dispose(); }
}

/// <summary>Stage render2 on the device (llvmpipe here): G-buffer-lite, AO, contact shadows, cascades and TAA.</summary>
public class Render2RenderTests
{
    const int W = 320, H = 180;
    static readonly SunState Sun = SunModel.At(11f);

    static ForwardRenderer NewRenderer(GraphicsDevice gd, string switches)
    {
        var r = new ForwardRenderer(gd, TestGfx.Host.Effects);
        r.Settings.FogParams = new Vector4(0f, 0.02f, 0f, 0f);
        r.Settings.ShadowCasterBounds = new BoundingBox(new Vector3(-80, -1, -80), new Vector3(80, 10, 80));
        r.Settings.Features.SetMany("autoexposure=0,bloom=0,grade=0,aa=none,farfield=0");
        if (switches.Length > 0) r.Settings.Features.SetMany(switches);
        return r;
    }

    static Color[] Frame(ForwardRenderer r, IRenderContent c, CameraPose cam, RenderTarget2D rt, string? save = null)
    {
        r.Render(c, cam, Sun, rt, 0f);
        if (save != null) Capture.SavePng(rt, Path.Combine(OutDir, save + ".png"));
        return Capture.ReadPixels(rt);
    }

    static readonly string OutDir = Path.Combine(AppContext.BaseDirectory, "test-output", "render2");

    static float Luma(Color c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;

    /// <summary>Mean luma of the 3×3 pixels around the projection of a world point.</summary>
    static float LumaAt(Color[] px, CameraPose cam, Vector3 world)
    {
        var p = Pixel(cam, world);
        float s = 0;
        for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++) s += Luma(px[Math.Clamp(p.y + j, 0, H - 1) * W + Math.Clamp(p.x + i, 0, W - 1)]);
        return s / 9f;
    }

    static (int x, int y) Pixel(CameraPose cam, Vector3 world)
    {
        var c = Vector4.Transform(new Vector4(world, 1), cam.View * cam.Projection(W / (float)H));
        return ((int)((c.X / c.W * 0.5f + 0.5f) * W), (int)((0.5f - c.Y / c.W * 0.5f) * H));
    }

    /// <summary>The camera sees the pillar from the side, between its sunlit and its shadowed sides.</summary>
    static (CameraPose cam, Vector3 sunSide, Vector3 shadowSide, Vector3 far) SideView(float gap)
    {
        var l = Sun.DirectionToLight;
        var s = Vector3.Normalize(new Vector3(l.X, 0, l.Z)); var p = new Vector3(-s.Z, 0, s.X);
        var cam = new CameraPose(p * 6f + new Vector3(0, 2.6f, 0), new Vector3(0, 0.25f, 0), 0.8f, 0.1f, 400f);
        return (cam, s * (0.5f + gap), -s * (0.5f + gap), p * 2.5f + s * 2.5f);
    }

    /// <summary>
    /// GTAO darkens the ground at the pillar's foot, leaves open flat ground alone, and darkens only the ambient share:
    /// on the sunlit side of the pillar it darkens the ground much less (relative) than on the shadowed side, where all
    /// light is ambient.
    /// </summary>
    [GfxFact]
    public void AmbientOcclusionDarkensOnlyTheAmbientShareAtContacts()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new PillarScene(gd);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            var (cam, sunSide, shadowSide, far) = SideView(0.12f);
            using var off = NewRenderer(gd, "ssao=0,contact=0");
            using var on = NewRenderer(gd, "ssao=1,contact=0");
            var a = Frame(off, scene, cam, rt); var b = Frame(on, scene, cam, rt);
            float rel(Vector3 w) => (LumaAt(a, cam, w) - LumaAt(b, cam, w)) / MathF.Max(1, LumaAt(a, cam, w));
            float shadowRel = rel(shadowSide), sunRel = rel(sunSide);
            Assert.True(shadowRel > 0.08f, $"shadowed foot darkened by {shadowRel:P1}");
            Assert.True(sunRel < 0.5f * shadowRel, $"sunlit foot darkened by {sunRel:P1} vs shadowed {shadowRel:P1}");
            Assert.True(MathF.Abs(LumaAt(a, cam, far) - LumaAt(b, cam, far)) <= 1.5f, "open flat ground unchanged");
            Assert.True(Capture.CountChanged(a, b, 2) > 30);
        });
    }

    /// <summary>
    /// Contact shadows (with the shadow map off, so nothing else shadows), seen from the side away from the sun: the
    /// ground at the pillar's foot darkens; ground beside the pillar, whose sun rays pass it, and open ground do not.
    /// </summary>
    [GfxFact]
    public void ContactShadowsFallAwayFromTheSun()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new PillarScene(gd);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            var l = Sun.DirectionToLight;
            var s = Vector3.Normalize(new Vector3(l.X, 0, l.Z)); var p = new Vector3(-s.Z, 0, s.X);
            var cam = new CameraPose(-s * 5f + p * 1.0f + new Vector3(0, 2.2f, 0), new Vector3(0, 0.2f, 0), 0.8f, 0.1f, 400f);
            Vector3 foot = -s * 0.55f, beside = p * 0.85f - s * 0.3f, open = -s * 3f + p * 2f;
            using var off = NewRenderer(gd, "ssao=0,contact=0"); off.Settings.Shadows = false;
            using var on = NewRenderer(gd, "ssao=0,contact=1"); on.Settings.Shadows = false;
            var a = Frame(off, scene, cam, rt, "contact-off"); var b = Frame(on, scene, cam, rt, "contact-on");
            using var dbg = NewRenderer(gd, "ssao=0,contact=1,debug=contact"); dbg.Settings.Shadows = false;
            Frame(dbg, scene, cam, rt, "contact-debug");
            float rel = (LumaAt(a, cam, foot) - LumaAt(b, cam, foot)) / MathF.Max(1, LumaAt(a, cam, foot));
            Assert.True(rel > 0.10f, $"ground at the foot darkened by {rel:P1}");
            Assert.True(MathF.Abs(LumaAt(a, cam, beside) - LumaAt(b, cam, beside)) <= 1.5f, "ground beside the pillar unchanged");
            Assert.True(MathF.Abs(LumaAt(a, cam, open) - LumaAt(b, cam, open)) <= 1.5f, "open ground unchanged");
        });
    }

    /// <summary>The G-buffer holds the linear view depth (m) and the light shares: the pillar's camera-facing face at
    /// 5.5 m, the sky far away; shadowed ground ambient only, sunlit ground mostly direct.</summary>
    [GfxFact]
    public void GBufferHoldsLinearDepthAndLightShares()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new PillarScene(gd);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            using var r = NewRenderer(gd, "ssao=1");
            var cam = new CameraPose(new Vector3(0, 1, 6), new Vector3(0, 1, 0), 0.8f, 0.1f, 400f);
            Frame(r, scene, cam, rt);
            var ss = r.ScreenSpace!;
            var depth = new float[W * H]; ss.Depth!.GetData(depth);
            Assert.InRange(depth[(H / 2) * W + W / 2], 5.45f, 5.55f);
            Assert.True(depth[2 * W + W / 2] > 1e5f, "sky depth");
            var (cam2, sunSide, shadowSide, far) = SideView(0.3f);
            Frame(r, scene, cam2, rt);
            var split = new Color[W * H]; ss.Split!.GetData(split);
            Color At(Vector3 w) { var p = Pixel(cam2, w); return split[p.y * W + p.x]; }
            var sh = At(shadowSide); var lit = At(far);
            Assert.True(sh.R > 215 && sh.G < 25 && sh.B == 255, $"shadowed ground shares {sh}");
            Assert.True(lit.G > 128 && lit.R < 128 && lit.B == 255, $"sunlit ground shares {lit}");
            Assert.Equal(new Color(0, 0, 0, 255), split[1 * W + 5]);
        });
    }

    /// <summary>Cascade stabilisation: whatever sub-texel move the camera makes, each cascade's window stays on the
    /// texel grid; turning the camera on the spot never changes a cascade's size.</summary>
    [GfxFact]
    public void CascadesStayOnTheirTexelGridAndKeepTheirSize()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var csm = new CascadedShadowMap(gd, 1024);
            var l = Vector3.Normalize(new Vector3(0.4f, 0.7f, 0.3f));
            var baseCam = new CameraPose(new Vector3(10, 8, 30), new Vector3(0, 0, 0), 0.8f, 0.3f, 4000f);
            float[] radius = new float[4];
            for (int step = 0; step < 12; step++)
            {
                var off = new Vector3(0.0137f * step, 0.0071f * step, -0.0093f * step);
                var yaw = Matrix.CreateRotationY(0.21f * step);
                var cam = baseCam with { Position = baseCam.Position + off, Target = baseCam.Position + off + Vector3.Transform(baseCam.Target - baseCam.Position, yaw) };
                csm.Fit(cam, 16f / 9f, l, 4, 800f, null);
                var lightView = Matrix.CreateLookAt(Vector3.Zero, -l, Vector3.Up);
                var inv = Matrix.Invert(lightView);
                for (int k = 0; k < 4; k++)
                {
                    var proj = inv * csm.ViewProj[k];
                    float tx = proj.M41 * csm.TileSize / 2f, ty = proj.M42 * csm.TileSize / 2f;
                    Assert.True(MathF.Abs(tx - MathF.Round(tx)) < 2e-3f && MathF.Abs(ty - MathF.Round(ty)) < 2e-3f, $"cascade {k} step {step}: {tx}, {ty}");
                    if (step == 0) radius[k] = csm.Radius[k]; else Assert.Equal(radius[k], csm.Radius[k]);
                }
            }
        });
    }

    /// <summary>Cascade caster culling skips casters in coarser cascades without changing a single pixel.</summary>
    [GfxFact]
    public void CasterCullingChangesNoPixel()
    {
        TestGfx.Host.Invoke(gd =>
        {
            var boxes = new List<(Vector3, Vector3, Vector3)>();
            for (int i = 0; i < 24; i++)
            {
                float a = i * 0.9f, d = 3f + i * 2.2f;
                boxes.Add((new Vector3(MathF.Cos(a) * d, 0.6f, -MathF.Abs(MathF.Sin(a)) * d), new Vector3(0.4f, 0.6f, 0.4f), new Vector3(0.5f, 0.45f, 0.4f)));
            }
            using var scene = new PillarScene(gd, boxes);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            var cam = new CameraPose(new Vector3(0, 6, 14), new Vector3(0, 0, -20), 0.8f, 0.3f, 1000f);
            using var r1 = NewRenderer(gd, "castercull=1,shadowdist=200");
            using var r0 = NewRenderer(gd, "castercull=0,shadowdist=200");
            scene.CastersCulled = scene.CastersDrawn = 0;
            var on = Frame(r1, scene, cam, rt); int culled = scene.CastersCulled, drawnOn = scene.CastersDrawn;
            scene.CastersCulled = scene.CastersDrawn = 0;
            var off = Frame(r0, scene, cam, rt);
            Assert.Equal(0, scene.CastersCulled);
            Assert.True(culled > 10, $"{culled} casters culled");
            Assert.Equal(scene.CastersDrawn, drawnOn + culled);
            Assert.Equal(0, Capture.CountChanged(on, off));
        });
    }

    /// <summary>The cascade debug view shows several cascades on a long view of the slab.</summary>
    [GfxFact]
    public void CascadeDebugViewShowsTheCascades()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new PillarScene(gd);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            using var r = NewRenderer(gd, "debug=cascades,shadowdist=120");
            var px = Frame(r, scene, new CameraPose(new Vector3(0, 2, 40), new Vector3(0, 0, -40), 0.8f, 0.3f, 400f), rt);
            int red = px.Count(c => c.R > 200 && c.G < 120 && c.B < 120), green = px.Count(c => c.G > 180 && c.R < 120 && c.B < 120),
                blue = px.Count(c => c.B > 200 && c.R < 120 && c.G < 160);
            Assert.True(red > 200 && green > 200 && blue > 100, $"red {red} green {green} blue {blue}");
        });
    }

    /// <summary>
    /// TAA history: kept across small camera moves (the frame then differs from a fresh renderer's first frame at that
    /// pose), dropped on a cut (the frame then equals a fresh renderer's first frame bit for bit), on a reset and after a
    /// frame rendered without TAA.
    /// </summary>
    [GfxFact]
    public void TaaKeepsHistoryOnSmallMovesAndDropsItOnCuts()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new PillarScene(gd);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            var cam = new CameraPose(new Vector3(4, 2.5f, 6), new Vector3(0, 0.8f, 0), 0.8f, 0.1f, 400f);
            Color[] Fresh(CameraPose c) { using var f = NewRenderer(gd, "aa=taa"); return Frame(f, scene, c, rt); }
            using var r = NewRenderer(gd, "aa=taa");
            Frame(r, scene, cam, rt);
            Assert.False(r.LastTaaHistoryKept);
            for (int i = 0; i < 6; i++) { Frame(r, scene, cam, rt); Assert.True(r.LastTaaHistoryKept); }
            var small = cam with { Position = cam.Position + new Vector3(0.05f, 0, 0), Target = cam.Target + new Vector3(0.05f, 0, 0) };
            var kept = Frame(r, scene, small, rt);
            Assert.True(r.LastTaaHistoryKept);
            Assert.True(Capture.CountChanged(kept, Fresh(small), 3) > 100, "the history contributes after a small move");
            var cut = cam with { Position = cam.Position + new Vector3(30f, 0, -50f), Target = new Vector3(20, 0.5f, -60) };
            var dropped = Frame(r, scene, cut, rt);
            Assert.False(r.LastTaaHistoryKept);
            Assert.Equal(0, Capture.CountChanged(dropped, Fresh(cut)));
            Frame(r, scene, cut, rt); Assert.True(r.LastTaaHistoryKept);
            r.ResetAdaptation = true;
            Frame(r, scene, cut, rt); Assert.False(r.LastTaaHistoryKept);
            Frame(r, scene, cut, rt); Assert.True(r.LastTaaHistoryKept);
            // a frame rendered without TAA in between breaks the history's continuity
            r.Settings.Features.Aa = AntiAliasing.Fxaa; Frame(r, scene, cut, rt);
            r.Settings.Features.Aa = AntiAliasing.Taa; Frame(r, scene, cut, rt); Assert.False(r.LastTaaHistoryKept);
        });
    }

    /// <summary>
    /// Ghost rejection: when something disappears without writing motion (here a dark box removed between two frames
    /// of a still camera), the kept history is clipped to what the current frame shows around each pixel, so the box
    /// leaves no trail: one frame later its former place matches a fresh renderer's frame, although the history was kept.
    /// </summary>
    [GfxFact]
    public void TaaRejectsHistoryTheCurrentFrameContradicts()
    {
        TestGfx.Host.Invoke(gd =>
        {
            var box = new Vector3(3f, 0.5f, 0f);   // in front of open, sunlit slab (its background lies 4 m behind the pillar)
            using var with = new PillarScene(gd, new[] { (box, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.03f, 0.03f, 0.03f)) });
            using var without = new PillarScene(gd);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            var cam = new CameraPose(new Vector3(4, 2.5f, 6), new Vector3(0, 0.8f, 0), 0.8f, 0.1f, 400f);
            using var fresh = NewRenderer(gd, "aa=taa");
            var empty = Frame(fresh, without, cam, rt);
            using var r = NewRenderer(gd, "aa=taa");
            Color[] shown = empty;
            for (int i = 0; i < 8; i++) shown = Frame(r, with, cam, rt, i == 7 ? "taa-ghost-before" : null);
            var probe = box + new Vector3(0, 0.5f, 0);
            float boxLuma = LumaAt(shown, cam, probe), groundLuma = LumaAt(empty, cam, probe);
            Assert.True(MathF.Abs(boxLuma - groundLuma) > 40f, $"box {boxLuma:F1} vs what lies behind it {groundLuma:F1}");
            var after = Frame(r, without, cam, rt, "taa-ghost-after");
            Assert.True(r.LastTaaHistoryKept);
            float trail = MathF.Abs(LumaAt(after, cam, probe) - groundLuma);
            Assert.True(trail < 6f, $"the removed box still shows: luma off by {trail:F1} (box {boxLuma:F1}, ground {groundLuma:F1})");
        });
    }

    /// <summary>A still camera: TAA converges towards the 16-sample accumulated still, closer than one unfiltered frame.</summary>
    [GfxFact]
    public void TaaConvergesTowardsTheAccumulatedStill()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new PillarScene(gd);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            var cam = new CameraPose(new Vector3(4, 2.5f, 6), new Vector3(0, 0.8f, 0), 0.8f, 0.1f, 400f);
            using var still = NewRenderer(gd, "samples=16");
            still.RenderStill(scene, cam, Sun, rt, 0f);
            var reference = Capture.ReadPixels(rt);
            using var none = NewRenderer(gd, "aa=none");
            var raw = Frame(none, scene, cam, rt);
            using var taa = NewRenderer(gd, "aa=taa");
            Color[] last = raw;
            for (int i = 0; i < 24; i++) last = Frame(taa, scene, cam, rt);
            Capture.SavePng(rt, Path.Combine(OutDir, "taa-static.png"));
            // compare on the pixels where one unfiltered frame and the still differ (edges): TAA must move them towards the still
            var edge = Enumerable.Range(0, raw.Length).Where(i => Math.Abs(Luma(raw[i]) - Luma(reference[i])) > 6).ToArray();
            double Err(Color[] x) => edge.Average(i => Math.Abs(Luma(x[i]) - Luma(reference[i])));
            double Bias(Color[] x) => Enumerable.Range(0, x.Length).Average(i => Luma(x[i]) - Luma(reference[i]));
            double eRaw = Err(raw), eTaa = Err(last);
            Assert.True(edge.Length > 100, $"{edge.Length} edge pixels");
            Assert.True(eTaa < 0.7 * eRaw, $"edge error: TAA {eTaa:F3} vs one frame {eRaw:F3} over {edge.Length} px; mean bias TAA {Bias(last):F3} raw {Bias(raw):F3}");
        });
    }

    /// <summary>With every render2 switch off, the G2 frame equals the frame without the G-buffer (no stray writes).</summary>
    [GfxFact]
    public void ScreenSpacePassesOffLeaveTheFrameAsBefore()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new PillarScene(gd);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            var cam = new CameraPose(new Vector3(4, 2.5f, 6), new Vector3(0, 0.8f, 0), 0.8f, 0.1f, 400f);
            using var a = NewRenderer(gd, "ssao=0,contact=0");
            var plain = Frame(a, scene, cam, rt);
            Assert.Null(a.ScreenSpace); // no G-buffer was needed
            using var b = NewRenderer(gd, "ssao=1,contact=0,aostrength=0");
            var gbuf = Frame(b, scene, cam, rt);
            Assert.NotNull(b.ScreenSpace);
            Assert.Equal(0, Capture.CountChanged(plain, gbuf, 1));
        });
    }
}
