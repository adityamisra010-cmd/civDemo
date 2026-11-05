using Gfx.Engine.Cameras;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Units;
using Gfx.Units.Demo;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Tests;

/// <summary>Lane U: skeleton/clip bake, per-entity variation, battle-trace fixture, playback, instanced skinning.</summary>
public class UnitsTests
{
    static readonly Lazy<BattleTrace> Fixture = new(() => BattleTrace.Load(BattleTrace.FindFixture(BattleTrace.DefaultFixture)));
    const string PinnedFixtureHash = "6733FBA5821BC5E669F69A880D0D3D5A0B25A94ABBE506BD91BA7C9FDECD2561";

    static List<VertexSkinned> AllVertices()
    {
        var v = new List<VertexSkinned>(SoldierMeshes.Body(6).Vertices);
        v.AddRange(SoldierMeshes.Body(4).Vertices);
        foreach (var p in Enum.GetValues<Part>()) v.AddRange(SoldierMeshes.Equipment(p).Vertices);
        return v;
    }

    // ---------------------------------------------------------------- skeleton + bake

    [Fact]
    public void SkeletonIsWellFormedAndRestPoseIsIdentitySkin()
    {
        Assert.Equal(Skeleton.JointCount, Skeleton.Names.Length);
        Assert.Equal(Skeleton.JointCount, Skeleton.Bind.Length);
        for (int j = 1; j < Skeleton.JointCount; j++) Assert.InRange(Skeleton.Parent[j], 0, j - 1);
        foreach (var m in Skeleton.SkinMatrices(Pose.Rest()))
            Assert.True(MatrixDiff(m, Matrix.Identity) < 1e-6f);
        foreach (var v in AllVertices())
        {
            var w = v.Weights; Assert.True(MathF.Abs(w.X + w.Y + w.Z + w.W - 1f) < 1e-5f, "weights must sum to 1");
            var j = v.Joints.ToVector4(); Assert.True(j.X < Skeleton.JointCount && j.Y < Skeleton.JointCount);
        }
    }

    static float MatrixDiff(in Matrix a, in Matrix b)
    {
        float d = 0; var x = a - b;
        foreach (var f in new[] { x.M11, x.M12, x.M13, x.M14, x.M21, x.M22, x.M23, x.M24, x.M31, x.M32, x.M33, x.M34, x.M41, x.M42, x.M43, x.M44 }) d = MathF.Max(d, MathF.Abs(f));
        return d;
    }

    /// <summary>Each baked row equals the exact pose at its frame phase, and the texture packing round-trips it.</summary>
    [Fact]
    public void BakedRowsEqualExactPosesAndPackRoundTrips()
    {
        var rows = ClipLibrary.BakeRows();
        Assert.Equal(ClipLibrary.RowCount, rows.Count);
        var packed = BoneTexture.Pack(rows, Skeleton.JointCount);
        Assert.Equal(Skeleton.JointCount * 3 * ClipLibrary.RowCount, packed.Length);
        foreach (var c in ClipLibrary.Clips)
            for (int k = 0; k < ClipLibrary.FramesPerClip; k++)
            {
                int r = ClipLibrary.FirstRow(c.Id) + k;
                var exact = Skeleton.SkinMatrices(ClipLibrary.Evaluate(c.Id, ClipLibrary.FramePhase(c.Id, k)));
                for (int j = 0; j < Skeleton.JointCount; j++)
                {
                    Assert.True(MatrixDiff(rows[r][j], exact[j]) == 0f);
                    var m = exact[j]; int o = (r * Skeleton.JointCount + j) * 3;
                    Assert.Equal(new Vector4(m.M11, m.M21, m.M31, m.M41), packed[o]);
                    Assert.Equal(new Vector4(m.M13, m.M23, m.M33, m.M43), packed[o + 2]);
                }
            }
    }

    /// <summary>
    /// Error bound of the GPU path (two baked rows, matrix lerp — <c>BoneTexture.SkinCpu</c> is the shader's CPU twin)
    /// against exact per-time evaluation, over every vertex of body + equipment, at 160 off-frame times per clip.
    /// Measured (32 frames/clip): ≤ 2.8 cm for all clips except fire-ranged (12.2 cm: the 0.16 s string release).
    /// </summary>
    [Fact]
    public void BlendedBakeStaysWithinErrorBoundOfExactSkinning()
    {
        var rows = ClipLibrary.BakeRows(); var verts = AllVertices();
        foreach (var c in ClipLibrary.Clips)
        {
            float bound = c.Id == ClipId.FireRanged ? 0.13f : 0.03f, maxErr = 0;
            for (int k = 0; k < 160; k++)
            {
                float t = c.Duration * (k + 0.37f) / 160f;
                var (a, b, w) = ClipLibrary.Sample(c.Id, t);
                var exact = Skeleton.SkinMatrices(ClipLibrary.Evaluate(c.Id, ClipLibrary.Phase(c.Id, t)));
                foreach (var v in verts)
                {
                    var (p, _) = BoneTexture.SkinCpu(v, rows[a], rows[b], w);
                    var (q, _) = BoneTexture.SkinCpu(v, exact, exact, 0);
                    maxErr = MathF.Max(maxErr, Vector3.Distance(p, q));
                }
            }
            Assert.True(maxErr <= bound, $"{c.Name}: max error {maxErr:F4} m > bound {bound} m");
        }
    }

    [Fact]
    public void SampleWrapsLoopsAndClampsOneShots()
    {
        int walk0 = ClipLibrary.FirstRow(ClipId.Walk), F = ClipLibrary.FramesPerClip;
        var d = ClipLibrary.Clips[(int)ClipId.Walk].Duration;
        var (a, b, w) = ClipLibrary.Sample(ClipId.Walk, d * (F - 0.5f) / F);
        Assert.Equal((walk0 + F - 1, walk0), (a, b)); Assert.Equal(0.5f, w, 3);
        var s1 = ClipLibrary.Sample(ClipId.Walk, 0.3f); var s2 = ClipLibrary.Sample(ClipId.Walk, 0.3f + 2 * d);
        Assert.Equal((s1.rowA, s1.rowB), (s2.rowA, s2.rowB)); Assert.Equal(s1.blend, s2.blend, 3);
        int death0 = ClipLibrary.FirstRow(ClipId.Death);
        var end = ClipLibrary.Sample(ClipId.Death, 100f);
        Assert.Equal(death0 + F - 1, end.rowB); Assert.Equal(1f, end.blend);
        var st = ClipLibrary.SampleStepped(ClipId.Run, 0.123f);
        Assert.Equal(st.rowA, st.rowB); Assert.Equal(0f, st.blend);
    }

    // ---------------------------------------------------------------- variation

    [Fact]
    public void VariationIsDeterministicPinnedAndVaried()
    {
        Assert.Equal(SoldierLook.For(1234, Kit.Spearman), SoldierLook.For(1234, Kit.Spearman));
        // pinned (integer-only derivation: platform-exact)
        Assert.Equal(new SoldierLook(Kit.Spearman, (int)Part.HelmetCrested, 1054, 988, 111, 1, 967, 1330), SoldierLook.For(1234, Kit.Spearman));
        var distinct = new HashSet<(int, int, int, int)>();
        var helmets = new HashSet<int>();
        for (long id = 0; id < 300; id++)
        {
            var l = SoldierLook.For(id, Kit.Archer);
            Assert.InRange(l.HeightPermille, 930, 1070); Assert.InRange(l.WidthPermille, 920, 1100);
            Assert.InRange(l.SkinTone, 0, 255); Assert.InRange(l.ClothShade, 0, 3); Assert.InRange(l.AnimOffsetMs, 0, 1999);
            distinct.Add((l.Helmet, l.HeightPermille, l.ClothShade, l.SkinTone)); helmets.Add(l.Helmet);
        }
        Assert.True(distinct.Count > 290, $"only {distinct.Count} distinct looks of 300");
        Assert.Equal(3, helmets.Count); // none, cone, kettle
        Assert.NotEqual(SoldierLook.For(7, Kit.Archer), SoldierLook.For(7, Kit.Musketeer));
    }

    // ---------------------------------------------------------------- fixture + playback

    [Fact]
    public void FixtureParsesWithExpectedShapeAndPinnedHash()
    {
        var t = Fixture.Value;
        Assert.StartsWith("fixture exported from prototype snapshot 2026-11-04", t.Label);
        Assert.Equal(4, t.Formations.Count); Assert.Equal(220, t.Entities.Count);
        Assert.Equal(104, t.StepCount); Assert.Equal(2000, t.StepMs); Assert.True(t.Over);
        Assert.Equal(1802, t.Events.Count);
        for (int i = 0; i < t.Entities.Count; i++) Assert.Equal(i, t.IndexOf(t.Entities[i].Id));
        for (int s = 1; s < t.StepCount; s++) Assert.Equal(t.StepTimeMs(s - 1) + t.StepMs, t.StepTimeMs(s));
        Assert.Equal(PinnedFixtureHash, t.Hash());
        Assert.Throws<InvalidDataException>(() => BattleTrace.Parse("{\"format\":\"other\"}"u8));
    }

    static FormationPlayback NewPlayback() => new(Fixture.Value, new Vector2(480_000, 330_000));

    [Fact]
    public void PlaybackInterpolatesPositionsAndDerivesFacing()
    {
        var t = Fixture.Value; var pb = NewPlayback(); var f = new SoldierFrame[pb.EntityCount];
        int s = 30, e = 0; float sec = (t.StepTimeMs(s) - t.StepTimeMs(0)) / 1000f;
        pb.Evaluate(sec, f);
        var (x0, y0) = t.PosMm(s, e); Assert.Equal(pb.ToWorld(x0, y0), f[e].Position);
        pb.Evaluate(sec + 1f, f); // half-way to the next 2 s step
        var (x1, y1) = t.PosMm(s + 1, e);
        var mid = (pb.ToWorld(x0, y0) + pb.ToWorld(x1, y1)) / 2;
        Assert.True(Vector3.Distance(mid, f[e].Position) < 1e-4f, $"{mid} vs {f[e].Position}");
        // entity 0 (side 0) advances east (+X): yaw ≈ +90°
        Assert.True(x1 > x0);
        Assert.InRange(f[e].Yaw, MathF.PI / 2 - 0.35f, MathF.PI / 2 + 0.35f);
    }

    [Fact]
    public void ClipSelectionFollowsTraceStatesAndEvents()
    {
        var t = Fixture.Value; var pb = NewPlayback(); var f = new SoldierFrame[pb.EntityCount];
        float T0 = t.StepTimeMs(0) / 1000f;
        // Dead → death clip timed from the first snapshot showing Dead
        int deadE = -1, deadS = -1;
        for (int s = 0; s < t.StepCount && deadE < 0; s++)
            for (int e = 0; e < t.Entities.Count; e++)
                if (t.State(s, e) == TraceState.Dead) { deadE = e; deadS = s; break; }
        Assert.True(deadE >= 0);
        float ds = t.StepTimeMs(deadS) / 1000f - T0;
        pb.Evaluate(ds + 0.5f, f);
        Assert.Equal(ClipId.Death, f[deadE].Clip); Assert.Equal(0.5f, f[deadE].ClipTime, 3);
        pb.Evaluate(ds - 0.5f, f);
        Assert.NotEqual(ClipId.Death, f[deadE].Clip);

        // melee Fire → attack-melee only while the trace says so; facing the target
        var fire = t.Events.First(x => x.Kind == TraceEventKind.Fire && x.A == 1);
        int src = t.IndexOf(fire.Source);
        bool damagedNear = t.Events.Any(x => x.Kind == TraceEventKind.Damage && x.Target == fire.Source && Math.Abs(x.TimeMs - fire.TimeMs) <= 600);
        pb.Evaluate(fire.TimeMs / 1000f - T0 + 0.25f, f);
        if (!damagedNear) Assert.Equal(ClipId.AttackMelee, f[src].Clip);

        // Damage → hit-react within 0.6 s, never before the event
        var dmg = t.Events.First(x => x.Kind == TraceEventKind.Damage);
        int tg = t.IndexOf(dmg.Target);
        pb.Evaluate(dmg.TimeMs / 1000f - T0 + 0.2f, f);
        if (t.State(pb.StepAt(dmg.TimeMs), tg) == TraceState.Active) Assert.Equal(ClipId.HitReact, f[tg].Clip);
        pb.Evaluate(dmg.TimeMs / 1000f - T0 - 0.05f, f);
        Assert.NotEqual(ClipId.HitReact, f[tg].Clip);

        // positions never depend on clip choice: equal to the lerp of fixture positions for every entity
        float probe = dmg.TimeMs / 1000f - T0 + 0.2f; pb.Evaluate(probe, f);
        int st = pb.StepAt(t.StepTimeMs(0) + (long)MathF.Floor(probe * 1000f));
        float u = (probe * 1000f + t.StepTimeMs(0) - t.StepTimeMs(st)) / t.StepMs;
        for (int e = 0; e < f.Length; e++)
        {
            var (ax, ay) = t.PosMm(st, e); var (bx, by) = t.PosMm(st + 1, e);
            Assert.True(Vector3.Distance(Vector3.Lerp(pb.ToWorld(ax, ay), pb.ToWorld(bx, by), u), f[e].Position) < 1e-3f);
        }
    }

    /// <summary>Renderer invariance (CPU path): playing the whole battle never changes the frozen fixture.</summary>
    [Fact]
    public void PlaybackNeverWritesTheFixture()
    {
        var path = BattleTrace.FindFixture(BattleTrace.DefaultFixture);
        var bytes = File.ReadAllBytes(path);
        var t = BattleTrace.Parse(bytes); string before = t.Hash();
        var pb = new FormationPlayback(t, new Vector2(480_000, 330_000)); var f = new SoldierFrame[pb.EntityCount];
        for (float s = 0; s <= pb.DurationSeconds; s += 0.25f) pb.Evaluate(s, f);
        Assert.Equal(before, t.Hash());
        Assert.Equal(PinnedFixtureHash, before);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    // ---------------------------------------------------------------- GPU

    sealed class CrowdContent(SoldierCrowd crowd, SoldierFrame[] frames) : IRenderContent
    {
        public void DrawShadowCasters(RenderContext rc) { crowd.Prepare(frames, rc.Camera.Position, rc.Frustum); crowd.Draw(rc); }
        public void DrawOpaque(RenderContext rc) { crowd.Prepare(frames, rc.Camera.Position, rc.Frustum); crowd.Draw(rc); }
    }

    /// <summary>Instancing smoke: 100 GPU-skinned soldiers cover the frame; the pose row read in the VS moves pixels; LODs engage.</summary>
    [GfxFact]
    public void InstancedSkinnedSoldiersRenderAndPoseMatters()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var crowd = new SoldierCrowd(gd);
            var frames = new SoldierFrame[100];
            void Fill(ClipId clip, float t)
            {
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = new SoldierFrame
                    {
                        Position = new Vector3((i % 10 - 4.5f) * 1.5f, 0, -(i / 10) * 1.5f), Yaw = 0, Clip = clip, ClipTime = t,
                        Visible = true, Side = i % 2, Look = SoldierLook.For(i, (Kit)(i % 4)),
                    };
            }
            using var r = new ForwardRenderer(gd, TestGfx.Host.Effects); r.Settings.Shadows = false;
            using var rt = new RenderTarget2D(gd, 256, 192, false, SurfaceFormat.Color, DepthFormat.None);
            var cam = new CameraPose(new Vector3(0, 4, 12), new Vector3(0, 1, -6)); var sun = SunModel.At(11);
            Fill(ClipId.Idle, 0.2f); r.Render(new CrowdContent(crowd, frames), cam, sun, rt);
            var idle = Capture.ReadPixels(rt);
            Capture.SavePng(rt, Path.Combine(AppContext.BaseDirectory, "test-output", "units-instancing-smoke.png"));
            Fill(ClipId.AttackMelee, 0.55f); r.Render(new CrowdContent(crowd, frames), cam, sun, rt);
            var attack = Capture.ReadPixels(rt);
            int covered = Capture.CountDifferentFrom(idle.AsSpan(idle.Length / 3), idle[^1], 10);
            Assert.True(crowd.Lod0Count > 0 && crowd.Lod1Count + crowd.Lod0Count == 100, $"lod0={crowd.Lod0Count} lod1={crowd.Lod1Count} lod2={crowd.Lod2Count}");
            Assert.True(covered > 4000, $"soldiers cover only {covered} px");
            Assert.True(Capture.CountChanged(idle, attack, 10) > 500, "pose rows did not move vertices");
            Assert.True(r.Stats.DrawCalls > 5, $"draws={r.Stats.DrawCalls}"); // body + parts per LOD, one instanced draw each
        });
    }

    /// <summary>Renderer invariance (GPU path): rendering the fixture-driven clash scene leaves the fixture hash unchanged.</summary>
    [GfxFact]
    public void ClashSceneRenderLeavesFixtureUnchanged()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new UnitsClashScene();
            scene.Load(gd, TestGfx.Host.Effects);
            string before = scene.Trace.Hash();
            using var r = new ForwardRenderer(gd, TestGfx.Host.Effects); scene.Configure(r.Settings);
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            var shot = scene.Shots.First(s => s.Name == "melee");
            scene.Update(shot.SceneTime);
            r.Render(scene, shot.Camera, SunModel.At(shot.HourOfDay), rt, shot.SceneTime);
            var px = Capture.ReadPixels(rt);
            Assert.True(Capture.CountDifferentFrom(px.AsSpan(px.Length / 3), px[^1], 10) > 3000);
            Assert.Equal(before, scene.Trace.Hash());
            Assert.Equal(PinnedFixtureHash, before);
        });
    }
}
