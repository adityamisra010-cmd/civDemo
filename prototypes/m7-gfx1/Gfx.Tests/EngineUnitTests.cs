using System.Diagnostics;
using Gfx.Demo;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;

namespace Gfx.Tests;

public class EngineUnitTests
{
    [Fact]
    public void BoneTexturePackLayoutIsJointsTimesThreeColumns()
    {
        var m = Matrix.CreateRotationY(0.3f) * Matrix.CreateTranslation(1, 2, 3);
        var data = BoneTexture.Pack(new[] { new[] { Matrix.Identity, m }, new[] { m, Matrix.Identity } }, 2);
        Assert.Equal(12, data.Length);
        Assert.Equal(new Vector4(m.M11, m.M21, m.M31, m.M41), data[3]);   // row 0, joint 1, column 0
        Assert.Equal(new Vector4(m.M13, m.M23, m.M33, m.M43), data[5]);   // row 0, joint 1, column 2
        Assert.Equal(new Vector4(m.M12, m.M22, m.M32, m.M42), data[7]);   // row 1, joint 0, column 1
        // shader convention: world.x = dot(column0, (p,1))
        var p = new Vector3(0.5f, 1, -2); var w = Vector3.Transform(p, m);
        Assert.Equal(w.X, Vector4.Dot(data[3], new Vector4(p, 1)), 4);
    }

    [Fact]
    public void FrameStatsPercentilesUseNearestRank()
    {
        var v = Enumerable.Range(1, 100).Select(i => (double)i).ToArray();
        Assert.Equal(50, FrameStats.Percentile(v, 50)); Assert.Equal(95, FrameStats.Percentile(v, 95)); Assert.Equal(99, FrameStats.Percentile(v, 99));
        var s = new FrameStats(); s.BeginFrame(); s.RecordDraw(10, 2); s.RecordDraw(5, 1); s.EndFrame();
        Assert.Equal(2, s.LastDrawCalls); Assert.Equal(15, s.LastTriangles); Assert.Equal(3, s.LastInstances);
        Assert.Contains("SOFTWARE", s.Format("SOFTWARE RENDERING (llvmpipe)"));
    }

    [Fact]
    public void SunIsHighAtNoonBelowHorizonAtMidnightAndWarmAtDusk()
    {
        var noon = SunModel.At(12); var night = SunModel.At(0); var dusk = SunModel.At(18.5f);
        Assert.True(noon.DirectionToLight.Y > 0.7f);
        Assert.True(night.IsNight && night.DirectionToLight.Y > 0.3f); // moon key light stays above the horizon
        Assert.True(night.LightColor.Length() < noon.LightColor.Length() / 10);
        Assert.True(dusk.LightColor.X > dusk.LightColor.Z * 2); // warm
        Assert.True(SunModel.At(9).DirectionToLight.X > 0 && SunModel.At(15).DirectionToLight.X < 0); // east → west
    }

    [Fact]
    public void SysInfoFlagsIntelAndSoftware()
    {
        var intel = new SysInfo("Intel", "Intel(R) Iris(R) Xe Graphics", "4.6", "4.60", "3.8.5", Microsoft.Xna.Framework.Graphics.GraphicsProfile.HiDef, 8, 1, "Windows", ".NET");
        Assert.True(intel.IsIntelIGpu); Assert.Contains(intel.Warnings, w => w.StartsWith("IntelIGPU", StringComparison.Ordinal));
        var sw = intel with { GlVendor = "Mesa", GlRenderer = "llvmpipe (LLVM 20.1.2, 256 bits)" };
        Assert.True(sw.IsSoftware); Assert.False(sw.IsIntelIGpu); Assert.Contains("SOFTWARE RENDERING", sw.TimingLabel);
    }

    [Fact]
    public void SceneRegistryFindsTheEngineScene()
    {
        var scenes = SceneRegistry.Discover(typeof(Program).Assembly);
        Assert.Contains(scenes, s => s.Name == "engine-instanced");
        Assert.Equal(scenes.Select(s => s.Name).OrderBy(n => n, StringComparer.Ordinal), scenes.Select(s => s.Name));
    }

    static (int code, string output) RunScan(string dir)
    {
        string script = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../scripts/check-gfx-determinism.sh"));
        var psi = new ProcessStartInfo("bash", new[] { script, dir }) { RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = Process.Start(psi)!;
        string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); p.WaitForExit();
        return (p.ExitCode, o);
    }

    [Fact]
    public void DeterminismScanFlagsFloatAndPassesIntegerCode()
    {
        if (!OperatingSystem.IsLinux()) return;
        string root = Path.Combine(Path.GetTempPath(), "gfx-scan-" + Environment.ProcessId);
        string bad = Path.Combine(root, "bad"), ok = Path.Combine(root, "ok");
        Directory.CreateDirectory(bad); Directory.CreateDirectory(ok);
        File.WriteAllText(Path.Combine(bad, "A.cs"), "class A { long X() => (long)Math.Sqrt(2); var d = new Dictionary<int,int>(); }\n");
        File.WriteAllText(Path.Combine(ok, "B.cs"), "/// float is fine in a comment\nclass B { long X(long a) => a * 3 + 1; }\n");
        var (badCode, badOut) = RunScan(bad);
        Assert.Equal(1, badCode); Assert.Contains("transcendental", badOut); Assert.Contains("Dictionary", badOut);
        var (okCode, okOut) = RunScan(ok);
        Assert.True(okCode == 0, okOut);
        Directory.Delete(root, true);
    }
}
