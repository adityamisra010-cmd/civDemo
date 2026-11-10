using Gfx.Engine.Cameras;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Perf.Measurement;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Environment;
using Gfx.Proc.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Tests;

/// <summary>
/// G2 integration of chars + env (2026-11-10). The perf harness measures every frame through a <see cref="ContentTap"/>
/// wrapped round the scene, and the renderer finds stage env's optional passes by the type of the content it is handed:
/// env scenes draw all their water in the transparent pass (<see cref="ITransparentContent"/>) and ask for a planar
/// reflection (<see cref="IPlanarReflectionContent"/>). A tap that hid them measured env frames with no water and no
/// reflection pass, while the harness's review capture (drawn untapped) had both. CPU heavy (an environment build), so
/// it runs serially with the env classes.
/// </summary>
[Collection(EnvSerialCollection.Name)]
public class PerfEnvTapTests
{
    static readonly EnvironmentOptions Small = new(ImpostorFrames: 4, ImpostorFrameSize: 32, MaxVariantsPerSpecies: 1);

    /// <summary>Environment content that counts the calls the renderer makes into it.</summary>
    sealed class Probe(EnvironmentScenery env) : IRenderContent, ITransparentContent, IPlanarReflectionContent
    {
        public int Opaque, ReflectionOpaque, Transparent;
        public void Reset() => Opaque = ReflectionOpaque = Transparent = 0;
        public void DrawShadowCasters(RenderContext rc) => env.DrawShadowCasters(rc);
        public void DrawOpaque(RenderContext rc) { if (rc.IsReflectionPass) ReflectionOpaque++; else Opaque++; env.DrawOpaque(rc); }
        public bool NeedsSceneColor(RenderContext rc) => env.NeedsSceneColor(rc);
        public void DrawTransparent(RenderContext rc) { Transparent++; env.DrawTransparent(rc); }
        public Vector4? ReflectionPlane(RenderContext rc) => env.ReflectionPlane(rc);
        public float ReflectionScale => 0.5f;
    }

    /// <summary>
    /// A river view, in the G2 frame (environment water, refraction copy, planar reflection) and in the G1 frame (G1
    /// water, drawn in the transparent pass): the tapped frame equals the untapped frame pixel for pixel, every pass
    /// reaches the scene through the tap, and the tap's opaque count and markers are the main scene pass's (the mirrored
    /// redraw and the water are the named segments "reflection" and "transparent"), so the GPU pass split's opaque span
    /// is not the reflection's.
    /// </summary>
    [GfxFact]
    public void TappedEnvironmentFrameDrawsTheUntappedFramesWaterAndReflection()
    {
        TestGfx.Host.Invoke(gd =>
        {
            var grid = BattleTerrainGenerator.Generate(TerrainTemperateScene.FixtureInputs, out var trace);
            using var env = new EnvironmentScenery(gd, TestGfx.Host.Effects, grid, null, Small, null, true, trace);
            var probe = new Probe(env);
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            var m = env.Terrain.Mesh;
            int mid = trace.RiverCells[trace.RiverCells.Length / 2];
            var rp = m.SamplePosition(mid % grid.Width, mid / grid.Width);
            var at = new Vector2(rp.X + 30, rp.Z + 10);
            var cam = new CameraPose(new Vector3(at.X, m.HeightAt(at.X, at.Y) + 8f, at.Y), new Vector3(rp.X, m.HeightAt(rp.X, rp.Z), rp.Z),
                0.85f, 0.3f, 3000f);
            var gl = GlApi.TryCreate(out _);
            using var timer = gl is { HasTimerQueries: true } ? new GpuTimer(gl) : null;
            long frameId = 0;
            foreach (bool g1Frame in new[] { false, true })
            {
                using var r = new ForwardRenderer(gd, TestGfx.Host.Effects);
                r.Settings.FogParams = env.Terrain.Weather.FogParams; r.Settings.ShadowCasterBounds = env.Terrain.CasterBounds;
                if (g1Frame) r.Settings.Features = RenderFeatures.G1();
                Color[] Frame(IRenderContent c)
                {
                    probe.Reset();
                    r.ResetAdaptation = true;
                    r.Render(c, cam, SunModel.At(10.5f), rt, 3f);
                    return Capture.ReadPixels(rt);
                }
                var expectedCalls = (1, g1Frame ? 0 : 1, 1); // main opaque, mirrored opaque (G2 only: the river is in view), water
                var direct = Frame(probe);
                Assert.Equal(expectedCalls, (probe.Opaque, probe.ReflectionOpaque, probe.Transparent));
                Assert.Equal(0, Capture.CountChanged(direct, Frame(probe))); // the device is deterministic for this frame

                var tap = new ContentTap(probe, timer);
                timer?.BeginFrame(++frameId);
                var tapped = Frame(tap);
                timer?.EndFrame();
                Assert.Equal(expectedCalls, (probe.Opaque, probe.ReflectionOpaque, probe.Transparent));
                Assert.Equal(0, Capture.CountChanged(direct, tapped));
                Assert.Equal(1, tap.OpaqueCalls);
                var segments = tap.SegmentsMs().Select(s => s.name).ToArray();
                Assert.Equal(g1Frame ? new[] { ContentTap.SegTransparent } : new[] { ContentTap.SegReflection, ContentTap.SegTransparent }, segments);

                if (timer == null) continue;
                timer.FinishAll();
                GpuFrame? frame = null;
                while (timer.TryDequeue(out var f)) frame = f;
                Assert.NotNull(frame);
                var names = frame!.Names;
                Assert.Single(names, n => n == ContentTap.MarkOpaqueBegin);
                int ob = Array.IndexOf(names, ContentTap.MarkOpaqueBegin), oe = Array.IndexOf(names, ContentTap.MarkOpaqueEnd);
                int tb = Array.IndexOf(names, "seg.begin:" + ContentTap.SegTransparent), re = Array.IndexOf(names, "seg.end:" + ContentTap.SegReflection);
                Assert.True(ob >= 0 && oe > ob && tb > oe, string.Join(" ", names));
                Assert.True(g1Frame ? re < 0 : re >= 0 && re < ob, string.Join(" ", names));
            }
        });
    }
}
