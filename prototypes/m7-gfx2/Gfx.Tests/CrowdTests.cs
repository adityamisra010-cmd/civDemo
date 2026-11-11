using Gfx.Chars.Animation;
using Gfx.Chars.Crowd;
using Gfx.Chars.Format;
using Gfx.Chars.Rendering;
using Gfx.Demo.Scenes;
using Gfx.Engine.Assets;
using Gfx.Engine.Cameras;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Gfx.Tests;

/// <summary>
/// Stage render2: the instanced crowd of stage-chars soldiers. CPU tests need the chars bake (they skip without it);
/// device tests need a device too.
/// </summary>
// CPU heavy (effect builds, animation-bank bakes, terrain occluders): serial with the env tests, away from the perf
// stage's wall-clock tests (see EnvSerialCollection; WarmupLastsAtLeastItsMinimumTime failed once in a full run
// of this stage's suite with these classes in the parallel phase, and passed when run alone).
[Collection(EnvSerialCollection.Name)]
public class CrowdTests
{
    static string Chars => AssetRoot.Baked("chars");

    static (Skeleton, CharacterFile) Identity(string id)
    {
        var f = CharacterFile.Load(Path.Combine(Chars, id + ".g2ch"));
        var clips = CharacterFile.Load(Path.Combine(Chars, id + "__clips.g2ch"));
        return (new Skeleton(f.Bones), clips);
    }

    /// <summary>
    /// A bank row holds exactly the clip frame's pose: the shader's CPU twin, fed one frame of one clip, skins a vertex
    /// to the same place as the stage-chars forward kinematics posed from the clip directly (an independent route
    /// through <see cref="ClipSampler"/> and <see cref="Pose"/>).
    /// </summary>
    [CharsBakeFact]
    public void BankRowsHoldTheClipFrames()
    {
        var ids = new[] { Identity("a1"), Identity("b3") };
        var bank = CrowdAnimationBank.BuildCpu(ids);
        foreach (int who in new[] { 0, 1 })
        {
            var (sk, clips) = ids[who];
            int c = CrowdAnimationBank.ClipIndex("thrust");
            var clip = clips.Clip("thrust")!;
            int f = 23;
            var pose = new Pose(sk.Count);
            ClipSampler.Sample(clip, f / clip.Fps, pose);
            var g = new Quaternion[sk.Count]; var h = new Vector3[sk.Count]; var skin = new Matrix[sk.Count];
            Fk.Solve(sk, pose, g, h, sk["pelvis"]);
            Fk.SkinMatrices(sk, g, h, Matrix.Identity, skin);
            int row = bank.FirstRow(who, c) + f;
            var anim = new Vector4(row, row, 0, 0);
            foreach (var (bone, p) in new[] { ("hand_r", new Vector3(-0.6f, 1.3f, 0.1f)), ("foot_l", new Vector3(0.1f, 0.08f, 0.05f)), ("head", new Vector3(0, 1.6f, 0)) })
            {
                int b = sk[bone];
                var expect = Vector3.Transform(p, skin[b]);
                var got = bank.SkinCpu(p, new[] { b, 0, 0, 0 }, new[] { 1f, 0, 0, 0 }, anim, anim);
                Assert.True(Vector3.Distance(expect, got) < 1e-4f, $"{bone}: {expect} vs {got}");
            }
        }
    }

    /// <summary>Looping clips wrap, one-shot clips clamp to their last frame; rows stay inside each clip.</summary>
    [CharsBakeFact]
    public void BankSamplingWrapsLoopsAndClampsOneShots()
    {
        var bank = CrowdAnimationBank.BuildCpu(new[] { Identity("a2") });
        int idle = CrowdAnimationBank.ClipIndex("idle"), thrust = CrowdAnimationBank.ClipIndex("thrust");
        var ci = bank.Clip(0, idle); var ct = bank.Clip(0, thrust);
        Assert.True(ci.Loop); Assert.False(ct.Loop);
        var (r0, r1, a) = bank.Sample(0, idle, ci.Duration + 0.5f / ci.Fps);
        Assert.Equal(bank.FirstRow(0, idle), r0); Assert.Equal(bank.FirstRow(0, idle) + 1, r1); Assert.InRange(a, 0.49f, 0.51f);
        var (t0, t1, _) = bank.Sample(0, thrust, 1000f);
        Assert.Equal(bank.FirstRow(0, thrust) + ct.Frames - 1, t0); Assert.Equal(t0, t1);
        for (float t = 0; t < 10; t += 0.137f)
        {
            var (x0, x1, _) = bank.Sample(0, idle, t);
            Assert.InRange(x0, bank.FirstRow(0, idle), bank.FirstRow(0, idle) + ci.Frames - 1);
            Assert.InRange(x1, bank.FirstRow(0, idle), bank.FirstRow(0, idle) + ci.Frames - 1);
        }
    }

    /// <summary>Eight identities overflow one 4,096-row column: rows wrap into further columns and still read back the
    /// right frame (the same bone of the same frame in identity 7 equals a bank built for that identity alone).</summary>
    [CharsBakeFact]
    public void BankColumnsWrapPastTheHiDefLimit()
    {
        var ids = CrowdCast.Variants.Select(v => Identity(v.identity)).ToArray();
        var all = CrowdAnimationBank.BuildCpu(ids);
        Assert.True(all.Rows > CrowdAnimationBank.RowsPerColumn, $"{all.Rows} rows");
        Assert.True(all.Columns >= 2);
        var last = CrowdAnimationBank.BuildCpu(new[] { ids[^1] });
        int c = CrowdAnimationBank.ClipIndex("death_back");
        for (int f = 0; f < last.Clip(0, c).Frames; f += 7)
        {
            int rowAll = all.FirstRow(ids.Length - 1, c) + f, rowOne = last.FirstRow(0, c) + f;
            for (int b = 0; b < all.Bones; b += 5)
                for (int k = 0; k < 3; k++)
                    Assert.Equal(last.Texel(rowOne, b, k), all.Texel(rowAll, b, k));
        }
    }

    /// <summary>The crowd LOD3 bake matches its manifest and sits between half of LOD2 and 300 triangles.</summary>
    [Fact]
    public void CrowdBakeMatchesItsManifest()
    {
        string dir = AssetRoot.Baked("crowd"), man = Path.Combine(dir, "MANIFEST.tsv");
        if (!File.Exists(man)) return; // the bake is optional: without it level 2 draws lod2 (CrowdRenderer.HasLod3)
        int n = 0;
        foreach (var line in File.ReadAllLines(man))
        {
            var p = line.Split('\t');
            var bytes = File.ReadAllBytes(Path.Combine(dir, p[0]));
            Assert.Equal(p[1], Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
            var f = CharacterFile.Parse(bytes);
            var m = f.Mesh("lod3")!;
            int src = f.Info["source_triangles"]!.GetValue<int>();
            Assert.InRange(m.TriangleCount, 300, src / 2);
            n++;
        }
        Assert.Equal(9, n);
    }

    // ------------------------------------------------------------------------------------------------ on the device

    const int W = 256, H = 256;

    sealed class Probe(CrowdRenderer crowd, Action<RenderContext> draw) : IRenderContent
    {
        public void DrawShadowCasters(RenderContext rc) { }
        public void DrawOpaque(RenderContext rc) => draw(rc);
        public CrowdRenderer Crowd => crowd;
    }

    static ForwardRenderer NewRenderer(GraphicsDevice gd, string switches = "")
    {
        var r = new ForwardRenderer(gd, TestGfx.Host.Effects);
        r.Settings.Shadows = false;
        r.Settings.Features.SetMany("autoexposure=0,bloom=0,grade=0,aa=none,farfield=0,ssao=0,contact=0");
        if (switches.Length > 0) r.Settings.Features.SetMany(switches);
        return r;
    }

    static readonly CameraPose Cam = new(new Vector3(0, 1.1f, 5.5f), new Vector3(0, 1.0f, 0), 0.7f, 0.1f, 200f);

    static Color[] Draw(GraphicsDevice gd, ForwardRenderer r, CrowdRenderer crowd, RenderTarget2D rt, Action<RenderContext> draw)
    {
        r.Render(new Probe(crowd, draw), Cam, SunModel.At(11f), rt, 0f);
        return Capture.ReadPixels(rt);
    }

    /// <summary>Pixels that differ from the sky behind the soldier (the background is the analytic sky only).</summary>
    static (int minX, int minY, int maxX, int maxY, int count) Silhouette(Color[] sky, Color[] px)
    {
        int x0 = W, y0 = H, x1 = -1, y1 = -1, n = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var a = sky[y * W + x]; var b = px[y * W + x];
                if (Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) <= 6) continue;
                n++; x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
            }
        return (x0, y0, x1, y1, n);
    }

    /// <summary>
    /// GPU skinning from the bank texture puts the soldier where the CPU twin of the shader puts it: the bounding box of
    /// the drawn silhouette matches the projected bounding box of every LOD1 vertex skinned on the CPU, within 2 px.
    /// </summary>
    [CharsGfxFact]
    public void CrowdSkinningMatchesTheCpuReference()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var loader = new CharStage(gd);
            using var crowd = new CrowdRenderer(gd, loader, CrowdCast.Variants);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            using var r = NewRenderer(gd);
            var sky = Draw(gd, r, crowd, rt, rc => { });
            int thrust = CrowdRenderer.Clip("thrust");
            var world = Matrix.CreateRotationY(0.6f);
            var vp = Cam.View * Cam.Projection(1f);
            Span<int> j = stackalloc int[4]; Span<float> w = stackalloc float[4];
            // variant 0's rows lie in the bank's first column, variant 7's in its last (the shader's column wrap)
            Assert.True(crowd.Bank.Columns >= 2, $"{crowd.Bank.Columns} bank column(s)");
            foreach (int variant in new[] { 0, 7 })
            {
                var inst = crowd.Instance(variant, world, thrust, 0.9f, thrust, 0.9f, 0, CrowdLook.FromKey(7) with { Height = 1, Width = 1 });
                Assert.Equal(variant == 0 ? 0 : crowd.Bank.Columns - 1, (int)inst.AnimA.X / CrowdAnimationBank.RowsPerColumn);
                var px = Draw(gd, r, crowd, rt, rc => crowd.DrawRaw(rc, variant, 0, new[] { inst }));
                var (x0, y0, x1, y1, n) = Silhouette(sky, px);
                Assert.True(n > 1500, $"variant {variant}: {n} soldier pixels");
                // CPU: skin every vertex of the variant's lod1 mesh with the instance's rows, transform, project
                var (identity, outfit) = CrowdCast.Variants[variant];
                var mesh = loader.Asset(identity + "__" + outfit).File.Mesh("lod1")!;
                float cx0 = float.MaxValue, cy0 = float.MaxValue, cx1 = float.MinValue, cy1 = float.MinValue;
                for (int v = 0; v < mesh.VertexCount; v++)
                {
                    for (int k = 0; k < 4; k++) { j[k] = mesh.Joints[4 * v + k]; w[k] = mesh.Weights[4 * v + k] / 255f; }
                    var cp = crowd.Bank.SkinCpu(mesh.Positions[v], j, w, inst.AnimA, inst.AnimB);
                    var c = Vector4.Transform(new Vector4(Vector3.Transform(cp, world), 1), vp);
                    float sx = (c.X / c.W * 0.5f + 0.5f) * W, sy = (0.5f - c.Y / c.W * 0.5f) * H;
                    cx0 = MathF.Min(cx0, sx); cx1 = MathF.Max(cx1, sx); cy0 = MathF.Min(cy0, sy); cy1 = MathF.Max(cy1, sy);
                }
                Assert.True(MathF.Abs(x0 - cx0) <= 2.5f && MathF.Abs(x1 - cx1) <= 2.5f && MathF.Abs(y0 - cy0) <= 2.5f && MathF.Abs(y1 - cy1) <= 2.5f,
                    $"variant {variant}: GPU box ({x0},{y0})-({x1},{y1}) vs CPU ({cx0:F1},{cy0:F1})-({cx1:F1},{cy1:F1})");
            }
        });
    }

    /// <summary>The two halves of a level cross-fade are exact complements: a soldier drawn twice at one level with the
    /// same fade, once outgoing and once incoming, is the soldier drawn once, bit for bit.</summary>
    [CharsGfxFact]
    public void LodCrossFadeMasksAreExactComplements()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var loader = new CharStage(gd);
            using var crowd = new CrowdRenderer(gd, loader, CrowdCast.Variants);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            using var r = NewRenderer(gd);
            int idle = CrowdRenderer.Clip("idle");
            var inst = crowd.Instance(5, Matrix.Identity, idle, 1.3f, idle, 1.3f, 0, CrowdLook.FromKey(11));
            var whole = Draw(gd, r, crowd, rt, rc => crowd.DrawRaw(rc, 5, 1, new[] { inst }));
            foreach (float fade in new[] { 0.23f, 0.5f, 0.81f })
            {
                var outgoing = inst; outgoing.Lod.X = fade; outgoing.Lod.Y = 1;
                var incoming = inst; incoming.Lod.X = fade; incoming.Lod.Y = -1;
                var halves = Draw(gd, r, crowd, rt, rc => crowd.DrawRaw(rc, 5, 1, new[] { outgoing, incoming }));
                Assert.Equal(0, Capture.CountChanged(whole, halves));
                var half = Draw(gd, r, crowd, rt, rc => crowd.DrawRaw(rc, 5, 1, new[] { outgoing }));
                Assert.True(Capture.CountChanged(whole, half, 6) > 100, $"fade {fade}: one half alone is not the whole soldier");
            }
        });
    }

    /// <summary>Two-clip blending: weight 1 is clip B itself bit for bit, weight 0 clip A, and a half blend differs from both.</summary>
    [CharsGfxFact]
    public void TwoClipBlendEndsAreTheClipsThemselves()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var loader = new CharStage(gd);
            using var crowd = new CrowdRenderer(gd, loader, CrowdCast.Variants);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            using var r = NewRenderer(gd);
            int idle = CrowdRenderer.Clip("idle"), block = CrowdRenderer.Clip("block");
            var look = CrowdLook.FromKey(3);
            Color[] With(int ca, float ta, int cb, float tb, float w) =>
                Draw(gd, r, crowd, rt, rc => crowd.DrawRaw(rc, 2, 0, new[] { crowd.Instance(2, Matrix.Identity, ca, ta, cb, tb, w, look) }));
            var a = With(idle, 2.0f, idle, 2.0f, 0f);
            var b = With(block, 0.7f, block, 0.7f, 0f);
            int ab = Capture.CountChanged(a, b, 6);
            Assert.True(ab > 300, $"the two clips pose the soldier differently ({ab} px)");
            Assert.Equal(0, Capture.CountChanged(a, With(idle, 2.0f, block, 0.7f, 0f)));   // weight 0 never reads clip B
            // weight 1: mix(a, b, 1) is b up to the last bit of the matrix lerp (the driver may evaluate it as a + (b - a)),
            // so a handful of silhouette pixels may flip; the pose itself is clip B's
            int b1 = Capture.CountChanged(b, With(idle, 2.0f, block, 0.7f, 1f));
            Assert.True(b1 <= 12, $"weight 1 differs from clip B in {b1} px");
            var mid = With(idle, 2.0f, block, 0.7f, 0.5f);
            int ma = Capture.CountChanged(mid, a, 6), mb = Capture.CountChanged(mid, b, 6);
            Assert.True(ma > 100 && mb > 100, $"half blend vs A {ma} px, vs B {mb} px");
        });
    }

    /// <summary>Per-soldier variation shows: the same variant with two looks differs in colour, the same look twice does not.</summary>
    [CharsGfxFact]
    public void LooksVaryTheSoldier()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var loader = new CharStage(gd);
            using var crowd = new CrowdRenderer(gd, loader, CrowdCast.Variants);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            using var r = NewRenderer(gd);
            int idle = CrowdRenderer.Clip("idle");
            var la = new CrowdLook(0.03f, 1.1f, 0.8f, 0.9f, 0.1f, 1, 1, 0.2f);
            var lb = new CrowdLook(-0.03f, 0.82f, -0.8f, 0.1f, 0.9f, 1, 1, 0.7f);
            Color[] With(CrowdLook l) => Draw(gd, r, crowd, rt, rc => crowd.DrawRaw(rc, 1, 0, new[] { crowd.Instance(1, Matrix.Identity, idle, 1, idle, 1, 0, l) }));
            var a = With(la);
            Assert.Equal(0, Capture.CountChanged(a, With(la)));
            Assert.True(Capture.CountChanged(a, With(lb), 4) > 400);
        });
    }

    /// <summary>
    /// The variation and blending switches, through the agent path (<see cref="CrowdRenderer.SetAgents"/> and
    /// <see cref="CrowdRenderer.DrawOpaque"/>): with looks off a varied soldier is drawn exactly as the same soldier in the
    /// neutral look; with clip blending off a 0.7 blend is drawn exactly as clip B alone and a 0.3 blend as clip A alone,
    /// while with blending on the 0.7 blend is neither.
    /// </summary>
    [CharsGfxFact]
    public void LooksAndBlendSwitchesGiveTheNeutralSoldierAndTheHeavierClip()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var loader = new CharStage(gd);
            using var crowd = new CrowdRenderer(gd, loader, CrowdCast.Variants);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            using var r = NewRenderer(gd);
            int idle = CrowdRenderer.Clip("idle"), block = CrowdRenderer.Clip("block");
            static CrowdAgent Agent(CrowdLook look, int ca, float ta, int cb, float tb, float w) => new()
            { Position = Vector3.Zero, Yaw = 0.3f, Variant = 2, ClipA = ca, TimeA = ta, ClipB = cb, TimeB = tb, Blend = w, Look = look };
            Color[] Frame(CrowdAgent a, bool looks, bool blend)
            {
                crowd.SetAgents(new[] { a });
                crowd.Looks = looks; crowd.ClipBlend = blend;
                return Draw(gd, r, crowd, rt, rc => crowd.DrawOpaque(rc));
            }
            var varied = new CrowdLook(0.03f, 1.1f, 0.8f, 0.9f, 0.6f, 1.04f, 0.97f, 0.2f);
            var neutral = Frame(Agent(CrowdLook.Neutral, idle, 1.3f, idle, 1.3f, 0), true, true);
            Assert.True(Capture.CountChanged(Frame(Agent(varied, idle, 1.3f, idle, 1.3f, 0), true, true), neutral, 4) > 300, "the varied look shows");
            Assert.Equal(0, Capture.CountChanged(neutral, Frame(Agent(varied, idle, 1.3f, idle, 1.3f, 0), false, true)));
            var clipA = Frame(Agent(CrowdLook.Neutral, idle, 2.0f, idle, 2.0f, 0), true, true);
            var clipB = Frame(Agent(CrowdLook.Neutral, block, 0.7f, block, 0.7f, 0), true, true);
            Assert.True(Capture.CountChanged(clipA, clipB, 6) > 300, "the two clips pose the soldier differently");
            Assert.Equal(0, Capture.CountChanged(clipB, Frame(Agent(CrowdLook.Neutral, idle, 2.0f, block, 0.7f, 0.7f), true, false)));
            Assert.Equal(0, Capture.CountChanged(clipA, Frame(Agent(CrowdLook.Neutral, idle, 2.0f, block, 0.7f, 0.3f), true, false)));
            var mixed = Frame(Agent(CrowdLook.Neutral, idle, 2.0f, block, 0.7f, 0.7f), true, true);
            Assert.True(Capture.CountChanged(mixed, clipB, 6) > 50 && Capture.CountChanged(mixed, clipA, 6) > 50, "blending on: the 0.7 blend is neither clip");
        });
    }

    /// <summary>
    /// Object motion for TAA: with history, a soldier whose feet moved writes its screen motion into the G-buffer (and the
    /// flag that the resolve must use it), the sky writes none; the motion equals the projected move within 10 %.
    /// </summary>
    [CharsGfxFact]
    public void MovingSoldiersWriteTheirScreenMotion()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var loader = new CharStage(gd);
            using var crowd = new CrowdRenderer(gd, loader, CrowdCast.Variants);
            using var rt = new RenderTarget2D(gd, W, H, false, SurfaceFormat.Color, DepthFormat.None);
            using var r = NewRenderer(gd, "aa=taa");
            int idle = CrowdRenderer.Clip("idle");
            var inst = crowd.Instance(4, Matrix.Identity, idle, 1, idle, 1, 0, CrowdLook.FromKey(1));
            inst.PrevDelta = new Vector4(0.1f, 0, 0, 0); // the feet moved 10 cm along +x since the previous frame
            void D(RenderContext rc) => crowd.DrawRaw(rc, 4, 0, new[] { inst });
            Draw(gd, r, crowd, rt, D);   // first frame: no history, no motion written
            Draw(gd, r, crowd, rt, D);
            var vel = new Microsoft.Xna.Framework.Graphics.PackedVector.HalfVector4[W * H];
            r.ScreenSpace!.Velocity!.GetData(vel);
            var centre = vel[(H / 2) * W + W / 2].ToVector4();
            var sky = vel[3 * W + 3].ToVector4();
            Assert.Equal(1f, centre.Z); Assert.Equal(0f, sky.Z);
            var vp = Cam.View * Cam.Projection(1f);
            var p0 = Vector4.Transform(new Vector4(0, 1.0f, 0, 1), vp); var p1 = Vector4.Transform(new Vector4(-0.1f, 1.0f, 0, 1), vp);
            float expect = p0.X / p0.W - p1.X / p1.W;
            Assert.InRange(centre.X, expect * 0.9f, expect * 1.1f);
            Assert.InRange(centre.Y, -0.02f * expect - 1e-3f, 0.02f * expect + 1e-3f);
        });
    }

    /// <summary>The crowd scene draws its soldiers and only reads the authoritative grid (rendering never writes it).</summary>
    [CharsGfxFact]
    public void CrowdSceneRendersAndNeverWritesTheGrid()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new CrowdFormationScene();
            scene.Load(gd, TestGfx.Host.Effects);
            string before = scene.Grid.HashHex();
            using var r = new ForwardRenderer(gd, TestGfx.Host.Effects);
            scene.Configure(r.Settings);
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            var shot = scene.Shots.First(s => s.Name == "wide");
            scene.Update(shot.SceneTime);
            r.Render(scene, shot.Camera, SunModel.At(shot.HourOfDay), rt, shot.SceneTime);
            Assert.Contains("agents=220", scene.FrameReport());
            Assert.Contains("drawn=220", scene.FrameReport());
            Assert.Equal(before, scene.Grid.HashHex());
            Assert.Equal(before, BattleTerrainGenerator.Generate(TerrainTemperateScene.FixtureInputs).HashHex());
        });
    }

    /// <summary>The crowd layer, like the hero characters, references no simulation assembly (DR42).</summary>
    [Fact]
    public void CrowdLayerReferencesNoSimulationAssembly()
    {
        var refs = typeof(CrowdRenderer).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToArray();
        Assert.DoesNotContain(refs, n => n.StartsWith("Sim.", StringComparison.Ordinal));
    }
}
