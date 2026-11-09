using Gfx.Demo.Scenes;
using Gfx.Engine.Cameras;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Tests;

/// <summary>Headless render tests (skip without a device). Pattern for other lanes: all GPU work inside Host.Invoke.</summary>
public class RenderTests
{
    static string OutDir => Path.Combine(AppContext.BaseDirectory, "test-output");

    [GfxFact]
    public void EngineSceneCaptureIsNonEmptyAndShadowsDarkenIt()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new EngineInstancedScene();
            scene.Load(gd, TestGfx.Host.Effects);
            using var r = new ForwardRenderer(gd, TestGfx.Host.Effects);
            scene.Configure(r.Settings);
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            var shot = scene.Shots[0]; var sun = SunModel.At(shot.HourOfDay);

            r.Render(scene, shot.Camera, sun, rt);
            var lit = Capture.ReadPixels(rt);
            string png = Path.Combine(OutDir, "engine-capture-test.png");
            Capture.SavePng(rt, png);
            Assert.True(new FileInfo(png).Length > 1000);
            Assert.True(Capture.DistinctColors(lit) > 200, "image is (nearly) flat");
            // non-sky content: the lower half must contain many pixels differing from the sky's corner colour
            var sky = lit[0];
            int nonSky = Capture.CountDifferentFrom(lit.AsSpan(lit.Length / 2), sky, 12);
            Assert.True(nonSky > lit.Length / 4, $"nonSky={nonSky}");
            Assert.True(scene.LastVisibleBoxes > 0 && scene.LastVisibleBoxes < scene.BoxCount, $"culling kept {scene.LastVisibleBoxes}/{scene.BoxCount}");

            r.Settings.Shadows = false;
            r.Render(scene, shot.Camera, sun, rt);
            var unlit = Capture.ReadPixels(rt);
            int darker = 0;
            for (int i = 0; i < lit.Length; i++) if (lit[i].R + lit[i].G + lit[i].B + 12 < unlit[i].R + unlit[i].G + unlit[i].B) darker++;
            Assert.True(darker > lit.Length / 100, $"shadowed pixels={darker}");
        });
    }

    sealed class BarContent(GpuMesh mesh, InstanceBuffer<SkinnedInstance>? skinned, InstanceBuffer<InstanceTransform>? rigid, BoneTexture bones) : IRenderContent
    {
        public void DrawShadowCasters(RenderContext rc) { }
        public void DrawOpaque(RenderContext rc)
        {
            rc.BindBones(bones);
            if (skinned != null) mesh.DrawInstanced(rc, rc.Effects.Get(EffectNames.SkinnedLit), skinned);
            if (rigid != null) mesh.DrawInstanced(rc, rc.Effects.Get(EffectNames.InstancedLit), rigid);
        }
    }

    /// <summary>
    /// VTF smoke on a 2-joint bone texture: (1) the pose row read in the VERTEX stage changes the image;
    /// (2) GPU skinning matches CPU skinning (same mesh skinned on the CPU, drawn rigid) to within a few pixels.
    /// </summary>
    [GfxFact]
    public void BoneTextureVertexFetchSkinsLikeTheCpuReference()
    {
        TestGfx.Host.Invoke(gd =>
        {
            var fx = TestGfx.Host.Effects;
            var bar = Primitives.SkinnedBar(Color.White);
            var rest = new[] { Matrix.Identity, Matrix.Identity };
            var bent = new[] { Matrix.Identity, Matrix.CreateTranslation(0, -1, 0) * Matrix.CreateRotationZ(MathHelper.ToRadians(70)) * Matrix.CreateTranslation(0, 1, 0) };
            using var bones = BoneTexture.Create(gd, new[] { rest, bent }, 2);
            using var gpuMesh = GpuMesh.Create(gd, bar);
            var cpuVerts = bar.Vertices.Select(v => { var (p, n) = BoneTexture.SkinCpu(v, bent, bent, 0); return new VertexPNC(p, Vector3.Normalize(n), v.Color); }).ToArray();
            using var cpuMesh = GpuMesh.Create(gd, new MeshData<VertexPNC>(cpuVerts, bar.Indices));
            using var inst = new InstanceBuffer<SkinnedInstance>(gd, 1);
            using var rigid = new InstanceBuffer<InstanceTransform>(gd, 1);
            rigid.Set(new[] { InstanceTransform.From(Matrix.Identity) });
            using var r = new ForwardRenderer(gd, fx); r.Settings.Shadows = false;
            using var rt = new RenderTarget2D(gd, 200, 200, false, SurfaceFormat.Color, DepthFormat.None);
            var cam = new CameraPose(new Vector3(0, 1.2f, 5), new Vector3(0, 1.2f, 0)); var sun = SunModel.At(11);

            Color[] Gpu(int row)
            {
                inst.Set(new[] { SkinnedInstance.From(Matrix.Identity, Vector4.One, row, row, 0) });
                r.Render(new BarContent(gpuMesh, inst, null, bones), cam, sun, rt);
                return Capture.ReadPixels(rt);
            }
            var restPx = Gpu(0); var bentPx = Gpu(1);
            r.Render(new BarContent(cpuMesh, null, rigid, bones), cam, sun, rt);
            var cpuPx = Capture.ReadPixels(rt);
            Capture.SavePng(rt, Path.Combine(OutDir, "vtf-cpu-reference.png"));

            int poseDiff = Capture.CountChanged(restPx, bentPx, 8);
            int gpuVsCpu = Capture.CountChanged(bentPx, cpuPx, 8);
            int covered = Capture.CountDifferentFrom(cpuPx, cpuPx[0], 8);
            Assert.True(poseDiff > 1500, $"pose row did not move vertices: {poseDiff} px changed");
            Assert.True(covered > 1500, $"bar not visible: {covered}");
            Assert.True(gpuVsCpu < covered / 50, $"GPU vs CPU skinning differ in {gpuVsCpu} px (covered {covered})");
        });
    }
}
