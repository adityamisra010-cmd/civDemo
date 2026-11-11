using System.Globalization;
using Gfx.Engine.Cameras;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Hosting;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Demo;

/// <summary>
/// Stage render2: interactive frames along a camera move, for anti-aliasing in motion (TAA against FXAA or none) and
/// its llvmpipe cost. The camera starts at a shot's pose and moves by <c>--pan</c> (m per frame, world axes) and
/// <c>--orbit</c> (degrees per frame about the shot's target); scene time advances 1/30 s per frame. Frames listed in
/// <c>--save</c> are written as PNG. PRESENTATION ONLY.
/// </summary>
public static class MotionCapture
{
    static string? Opt(string[] args, string name, string? def) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : def; }

    public static int Run(GraphicsDevice gd, GfxHost host, Gfx.Engine.Scene.SceneRegistry.Entry e, int w, int h, bool shadows, SysInfo info, string[] args)
    {
        using var scene = e.Create();
        scene.Load(gd, host.Effects);
        using var renderer = new ForwardRenderer(gd, host.Effects);
        scene.Configure(renderer.Settings); renderer.Settings.Shadows = shadows;
        Program.ApplyRenderSwitches(renderer.Settings, args);
        string shotName = Opt(args, "--shot", null) ?? scene.Shots[0].Name;
        var shot = scene.Shots.First(s => s.Name == shotName);
        int frames = int.Parse(Opt(args, "--frames", "16")!, CultureInfo.InvariantCulture);
        var pan = (Opt(args, "--pan", "0,0,0")!).Split(',').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        float orbit = float.Parse(Opt(args, "--orbit", "0")!, CultureInfo.InvariantCulture);
        var save = (Opt(args, "--save", (frames - 1).ToString(CultureInfo.InvariantCulture))!).Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToHashSet();
        string outDir = Opt(args, "--out", Path.Combine(Directory.GetCurrentDirectory(), "captures"))!;
        string tag = Opt(args, "--tag", renderer.Settings.Features.Aa.ToString().ToLowerInvariant())!;
        Directory.CreateDirectory(outDir);
        using var rt = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
        var sun = SunModel.At(shot.HourOfDay);
        Console.WriteLine($"[motion] {e.Name} shot={shot.Name} frames={frames} pan=({string.Join(",", pan.Select(v => v.ToString(CultureInfo.InvariantCulture)))}) orbit={orbit.ToString(CultureInfo.InvariantCulture)}°/frame {w}x{h}");
        Console.WriteLine($"[motion] {renderer.Settings.Features.Describe()}");
        var times = new List<double>();
        renderer.ResetAdaptation = true;
        for (int f = 0; f < frames; f++)
        {
            var c = shot.Camera;
            var off = new Vector3(pan[0], pan[1], pan[2]) * f;
            var rot = Matrix.CreateRotationY(MathHelper.ToRadians(orbit * f));
            var p = Vector3.Transform(c.Position - c.Target, rot) + c.Target + off;
            var cam = c with { Position = p, Target = c.Target + off };
            float t = shot.SceneTime + f / 30f;
            scene.Update(t);
            renderer.Stats.BeginFrame();
            renderer.Render(scene, cam, sun, rt, t);
            var px = Capture.ReadPixels(rt);
            double ms = renderer.Stats.EndFrame();
            times.Add(ms);
            if (save.Contains(f))
            {
                string path = Path.Combine(outDir, $"{e.Name}-{shot.Name}-{tag}-f{f:00}.png");
                Capture.SavePng(rt, path);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "[motion] frame {0} -> {1} history={2} taaframe={3} {4:F1} ms [{5}]",
                    f, path, renderer.LastTaaHistoryKept, renderer.TaaFrameIndex, ms, info.TimingLabel));
            }
        }
        times.Sort();
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "[motion] frame ms p50 {0:F1} min {1:F1} max {2:F1} over {3} frames [{4}]",
            times[times.Count / 2], times[0], times[^1], times.Count, info.TimingLabel));
        return 0;
    }
}
